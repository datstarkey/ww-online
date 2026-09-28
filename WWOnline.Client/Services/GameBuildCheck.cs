using Serilog;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Pipeline;

namespace WWOnline.Services;

/// <summary>
/// Is a patched game still what this app would build? The one implementation behind the Settings
/// page's build status (OptionalPatchCatalogService.CheckGameBuild), the startup log line and the
/// headless --check-build.
/// <list type="bullet">
/// <item><b>Dev</b> (GameMod/config.json found): the game's stamp against the GameMod sources, like
/// dev-test (<see cref="BuildStamp.Check"/>).</item>
/// <item><b>Installed</b> (no GameMod): the game's stamp against the app's own
/// PatchData/patchdata-stamp.json (<see cref="BuildStamp.CheckAgainstPatchData"/>). An app update
/// that changed the game code reads as stale; one that didn't stays fresh.</item>
/// <item>PatchData without a stamp (a dev build's copy of GameMod/build): the selection only.</item>
/// </list>
/// </summary>
public static class GameBuildCheck
{
    private static readonly ILogger Logger = Log.ForContext(typeof(GameBuildCheck));

    /// <param name="gamePath">The patched game folder.</param>
    /// <param name="desired">The optional patches that should be in the game (null = don't compare the
    /// selection, only whether the game matches the code for the selection it was built with).</param>
    /// <param name="gameModPath">GameMod/ in dev (<see cref="GamePatcherService.FindGameModFolder"/>), else null.</param>
    /// <param name="patchDataPath">The app's PatchData folder.</param>
    public static BuildStamp.CheckResult Check(string gamePath, IReadOnlyCollection<string>? desired, string? gameModPath,
        string patchDataPath)
    {
        if (gameModPath != null)
            return BuildStamp.Check(PatcherConfig.Create(gamePath, "", gameModPath), desired);

        PatchDataStamp? stamp = null;
        try { stamp = new PatchDataFolder(patchDataPath).ReadStamp(); }
        catch (InvalidDataException ex) { Logger.Warning("[build-check] {Reason}; checking the selection only", ex.Message); }
        if (stamp != null)
            return BuildStamp.CheckAgainstPatchData(gamePath, stamp, desired);

        if (desired != null)
            return BuildStamp.CheckSelection(gamePath, desired);
        var recorded = BuildStamp.Read(gamePath);
        return recorded == null
            ? new BuildStamp.CheckResult(BuildStamp.Status.Missing, "", null)
            : new BuildStamp.CheckResult(BuildStamp.Status.Fresh, recorded.SourceHash, recorded);
    }
}
