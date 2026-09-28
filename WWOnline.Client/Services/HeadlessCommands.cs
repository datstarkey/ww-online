using Serilog;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Pipeline;

namespace WWOnline.Services;

/// <summary>
/// Non-UI entry points used by dev-test.ps1 and the release workflow, so scripts drive the exact
/// same C# code paths as the app instead of re-implementing them. Results go to the log (and the
/// exit code):
///
///   --check-build      0 = patched game is up to date, 2 = stale, 3 = no stamp, 1 = error.
///                      Dev (GameMod/config.json found): config.json's game_path against the
///                      GameMod sources. Installed: the settings' game path against the app's
///                      PatchData/patchdata-stamp.json (<see cref="GameBuildCheck"/>).
///   --patch            0 = full patch pipeline succeeded (stamp written), 1 = failed (dev only)
///   --patches ids      optional patch selection for both ("a,b", "none", "default"). --patch
///                      without it applies the catalogue defaults; --check-build without it only
///                      checks the sources of whatever selection the game was built with (dev),
///                      or compares with the saved selection (installed).
///   --build-patchdata  0 = PatchData folder built, 1 = failed. Needs only GameMod sources and
///                      devkitPPC (see <see cref="PatchDataBuilder"/>).
/// </summary>
public static class HeadlessCommands
{
    public const int ExitOk = 0;
    public const int ExitError = 1;
    public const int ExitStale = 2;
    public const int ExitNoStamp = 3;

    private static readonly ILogger Logger = Log.ForContext(typeof(HeadlessCommands));

    public static int Run(StartupOptions opts)
    {
        try
        {
            if (opts.BuildPatchData != null)
                return RunBuildPatchData(opts.BuildPatchData, opts.GameModPath);

            var gameMod = GamePatcherService.FindGameModFolder();
            // null = catalogue defaults for --patch, "don't compare the selection" for --check-build.
            var selection = opts.Patches == null ? null : OptionalPatchCatalog.ParseIdList(opts.Patches);
            if (opts.Patch)
            {
                if (gameMod == null) throw new DirectoryNotFoundException(NoConfigMessage);
                var config = PatcherConfig.LoadFromConfigJson(gameMod);
                config.OptionalPatches = selection;
                int patchResult = RunPatch(config);
                if (patchResult != ExitOk) return patchResult;
            }
            return CheckBuild(opts.Patches != null, selection);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[headless] failed");
            return ExitError;
        }
    }

    /// <summary>Game build freshness for the startup log line (dev or installed; null when it can't tell).</summary>
    public static BuildStamp.CheckResult? TryCheckBuild()
    {
        try { return CheckDefaultGame(hasSelection: false, selection: null).Result; }
        catch (Exception ex)
        {
            Logger.Warning("[build-check] could not check game build stamp: {Reason}", ex.Message);
            return null;
        }
    }

    private static string NoConfigMessage => "GameMod/config.json not found above " + AppContext.BaseDirectory +
                                             " (copy GameMod/config.example.json to GameMod/config.json)";

    /// <summary>
    /// The game to check and against what: dev = config.json's game_path vs the GameMod sources
    /// (selection compared only when given); installed = the settings' game path vs PatchData
    /// (the given selection, else the saved one).
    /// </summary>
    private static (string GamePath, BuildStamp.CheckResult Result) CheckDefaultGame(bool hasSelection, IReadOnlyList<string>? selection)
    {
        var gameMod = GamePatcherService.FindGameModFolder();
        if (gameMod != null)
        {
            var config = PatcherConfig.LoadFromConfigJson(gameMod);
            var desired = hasSelection ? selection ?? config.LoadOptionalPatchCatalog().DefaultIds : null;
            return (config.GamePath, GameBuildCheck.Check(config.GamePath, desired, gameMod, GamePatcherService.DefaultPatchDataPath));
        }

        var settings = new GameSettingsService();
        var gamePath = settings.Load().GamePath;
        if (string.IsNullOrWhiteSpace(gamePath))
            throw new InvalidOperationException("No patched game folder is set (Settings → Patched game folder), and no GameMod/config.json was found.");
        var catalog = new OptionalPatchCatalogService(settings);
        var wanted = hasSelection ? catalog.Resolve(selection) : catalog.GetSelectedIds();
        return (gamePath, GameBuildCheck.Check(gamePath, wanted, null, GamePatcherService.DefaultPatchDataPath));
    }

    private static int CheckBuild(bool hasSelection, IReadOnlyList<string>? selection)
    {
        var (gamePath, result) = CheckDefaultGame(hasSelection, selection);
        var message = $"[build-check] {gamePath}: {result.Describe()}";
        if (result.Status == BuildStamp.Status.Fresh) Logger.Information(message);
        else Logger.Error(message);

        return result.Status switch
        {
            BuildStamp.Status.Fresh => ExitOk,
            BuildStamp.Status.Stale => ExitStale,
            _ => ExitNoStamp,
        };
    }

    private static int RunPatch(PatcherConfig config)
    {
        config.Validate();
        var optional = config.ResolveOptionalPatches(); // throws on unknown ids / conflicts
        Logger.Information("[patch] full pipeline → {GamePath}; optional patches: {Patches}", config.GamePath,
            optional.Ids.Count == 0 ? "none" : string.Join(", ", optional.Ids));

        var progress = new PipelineProgress();
        progress.OnProgress += ev =>
            Logger.Information("[patch] [{Step}] {Name}: {Message}", ev.StepNumber, ev.StepName, ev.Message);

        try
        {
            new PipelineRunner(config).RunAsync(PipelineRunner.BuildMode.Full, progress).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[patch] pipeline failed");
            return ExitError;
        }

        Logger.Information("[patch] complete");
        return ExitOk;
    }

    /// <summary>--build-patchdata: GameMod sources + devkitPPC → a release PatchData folder.</summary>
    private static int RunBuildPatchData(string outDir, string? gameModOption)
    {
        if (string.IsNullOrWhiteSpace(outDir))
        {
            Logger.Error("[patchdata] --build-patchdata needs an output folder: --build-patchdata <dir>");
            return ExitError;
        }
        var gameMod = gameModOption ?? FindGameModSources();
        if (gameMod == null)
        {
            Logger.Error("[patchdata] GameMod sources not found above {Cwd} or {App}; pass --gamemod <dir>",
                Environment.CurrentDirectory, AppContext.BaseDirectory);
            return ExitError;
        }
        var devkit = PatcherConfig.DefaultDevKitPpcPath();
        Logger.Information("[patchdata] building PatchData → {Out} from {GameMod} with devkitPPC {DevKit}",
            Path.GetFullPath(outDir), Path.GetFullPath(gameMod), devkit);

        var progress = new PipelineProgress();
        progress.OnProgress += ev =>
            Logger.Information("[patchdata] [{Step}] {Name}: {Message}", ev.StepNumber, ev.StepName, ev.Message);
        try
        {
            var stamp = PatchDataBuilder.BuildAsync(gameMod, devkit, outDir, progress).GetAwaiter().GetResult();
            foreach (var file in stamp.Files)
                Logger.Information("[patchdata]   {Size,9}  {Path}", file.Size, file.Path);
            Logger.Information("[patchdata] complete");
            return ExitOk;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[patchdata] failed");
            return ExitError;
        }
    }

    /// <summary>
    /// GameMod/ with its sources (config.json not needed), above the working directory or the app:
    /// `dotnet run --project WWOnline.Client` from the repo root finds it from either.
    /// </summary>
    internal static string? FindGameModSources()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "GameMod");
                if (File.Exists(Path.Combine(candidate, "src", "puppet_link", "puppet_shared.h")))
                    return candidate;
            }
        }
        return null;
    }
}
