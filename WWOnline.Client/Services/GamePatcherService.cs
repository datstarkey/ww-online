using System.Buffers.Binary;
using System.Security.Cryptography;
using Serilog;
using WWOnline.Data;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Dol;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.BinaryFormats.Rel;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Pipeline;

namespace WWOnline.Services;

/// <summary>
/// Patches a fresh copy of the game. Prefers a live compile of the C puppet sources
/// via the Patcher pipeline (requires devkitPPC + GameMod/config.json) so edits to
/// puppet.c / link_draw_hook.c land in the REL. Falls back to the pre-built artifacts
/// shipped in PatchData/ when the dev toolchain isn't available.
///
/// Both paths apply the player's optional patch selection (OptionalPatchCatalogService, saved in
/// GameSettings.OptionalPatches) and write the build stamp, so a changed selection shows up as
/// "re-patch needed".
/// </summary>
public class GamePatcherService
{
    private static readonly ILogger Logger = Log.ForContext<GamePatcherService>();

    private readonly OptionalPatchCatalogService _patchCatalog;

    /// <param name="patchCatalog">The catalogue and saved selection.</param>
    /// <param name="patchDataPath">Pre-built PatchData folder (null = <see cref="DefaultPatchDataPath"/>).</param>
    public GamePatcherService(OptionalPatchCatalogService patchCatalog, string? patchDataPath = null)
    {
        _patchCatalog = patchCatalog;
        PatchDataPath = patchDataPath ?? DefaultPatchDataPath;
    }

    /// <summary>
    /// Pre-built fallback artifacts shipped next to the client: the release workflow's
    /// --build-patchdata output (with patchdata-stamp.json), or in dev builds whatever the .csproj
    /// copied from GameMod/ (no stamp).
    /// </summary>
    public static string DefaultPatchDataPath => Path.Combine(AppContext.BaseDirectory, "PatchData");

    /// <summary>The PatchData folder this service patches from.</summary>
    public string PatchDataPath { get; }

    /// <summary>
    /// Validate that the vanilla game path is the extracted US game (<see cref="GameFolderCheck.CheckVanilla"/>).
    /// </summary>
    public string? ValidateVanillaPath(string vanillaPath) => GameFolderCheck.CheckVanilla(vanillaPath).Error;

    /// <summary>
    /// Copy vanilla game files to the output folder and apply all patches. Prefers a live
    /// compile of the C puppet sources via the Patcher pipeline; falls back to the pre-built
    /// PatchData/ artifacts when devkitPPC or the GameMod/ sources aren't present.
    /// Returns null on success, or an error message on failure.
    /// </summary>
    public Task<string?> PatchGameAsync(string vanillaPath, string outputPath, IProgress<string>? progress = null) =>
        PatchGameAsync(vanillaPath, outputPath, _patchCatalog.GetSelectedIds(), progress);

    /// <summary>
    /// Raised when a patch run ends, successful or not (a failed run may have removed the build
    /// stamp): the output folder and the error (null on success). May fire on a background thread.
    /// PatchOptionsViewModel re-checks the build status on it.
    /// </summary>
    public event Action<string, string?>? PatchFinished;

    /// <summary>
    /// As above with an explicit optional patch selection (ids from OptionalPatchCatalogService).
    /// </summary>
    public async Task<string?> PatchGameAsync(string vanillaPath, string outputPath,
        IReadOnlyCollection<string> optionalPatchIds, IProgress<string>? progress = null)
    {
        string? error = "Patching did not finish."; // until the core returns (it may throw)
        try
        {
            error = await PatchGameCoreAsync(vanillaPath, outputPath, optionalPatchIds, progress);
            return error;
        }
        finally
        {
            try { PatchFinished?.Invoke(outputPath, error); }
            catch (Exception ex) { Logger.Error(ex, "[patches] a PatchFinished handler failed"); }
        }
    }

    private async Task<string?> PatchGameCoreAsync(string vanillaPath, string outputPath,
        IReadOnlyCollection<string> optionalPatchIds, IProgress<string>? progress)
    {
        // Both paths read the vanilla files from the UI's vanilla game folder (the live pipeline
        // overrides config.json's vanilla_game_path with it), so validate it up front.
        var validationError = ValidateVanillaPath(vanillaPath) ?? GameFolderCheck.CheckPatchedFolder(vanillaPath, outputPath);
        if (validationError != null) return validationError;

        // The patch steps only rewrite the files they change, so a new (empty) output folder first
        // gets the rest of the game: Dolphin boots sys/main.dol only from a complete extracted disc.
        try
        {
            await Task.Run(() => CopyMissingGameFiles(vanillaPath, outputPath, progress));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Error(ex, "Copying the game into {OutputPath} failed", outputPath);
            return $"Couldn't copy the game into {outputPath}: {ex.Message}";
        }

        PatchSelection selection;
        try
        {
            selection = PatchPlan.ResolveStrict(_patchCatalog.Catalog, optionalPatchIds);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
        progress?.Report("Optional patches: " + (selection.Ids.Count == 0 ? "none" : string.Join(", ", selection.Ids)));

        // 1) Live compile path: requires GameMod/config.json + devkitPPC installed.
        //    This is the only path that picks up edits to puppet.c / link_draw_hook.c.
        var gameModPath = FindGameModFolder();
        if (gameModPath != null)
        {
            // Off the UI thread: the pipeline steps and GamePatchApplier (Yaz0 re-compression, file
            // restores) are synchronous. Progress<T> still reports on the caller's context.
            var liveCompileError = await Task.Run(() => TryRunLiveCompileAsync(gameModPath, vanillaPath, outputPath, selection.Ids, progress));
            if (liveCompileError == null)
                return null; // success

            Logger.Warning(
                "Live compile unavailable, falling back to pre-built PatchData artifacts: {Reason}",
                liveCompileError);
            progress?.Report($"Live compile unavailable ({liveCompileError}). Using bundled pre-built patch...");
        }
        else
        {
            Logger.Information("GameMod folder not found; using pre-built PatchData artifacts.");
        }

        // 2) Fallback path: apply YAML diffs + insert the pre-built REL shipped in PatchData/.
        return await Task.Run(() => ApplyPreBuiltAsync(vanillaPath, outputPath, selection.Ids, progress));
    }

    /// <summary>
    /// Attempt a full live compile via PipelineRunner. Returns null on success, or a short
    /// reason string on failure (which the caller logs and uses to trigger the fallback).
    /// </summary>
    private async Task<string?> TryRunLiveCompileAsync(string gameModPath, string vanillaPath, string outputPath,
        IReadOnlyList<string> optionalPatchIds, IProgress<string>? progress)
    {
        PatcherConfig config;
        try
        {
            config = PatcherConfig.LoadFromConfigJson(gameModPath);
            config.GamePath = outputPath;         // the UI owns the output location...
            config.VanillaGamePath = vanillaPath; // ...and the vanilla source
            config.OptionalPatches = optionalPatchIds;
            config.Validate();                    // throws if devkitPPC or game files missing
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        try
        {
            Directory.CreateDirectory(Path.Combine(outputPath, "sys"));
            Directory.CreateDirectory(Path.Combine(outputPath, "files"));

            progress?.Report("Live compile via PipelineRunner (devkitPPC detected)...");

            var pipelineProgress = new PipelineProgress();
            if (progress != null)
            {
                pipelineProgress.OnProgress += ev =>
                    progress.Report($"[{ev.StepNumber}] {ev.StepName}: {ev.Message}");
            }

            var runner = new PipelineRunner(config);
            await runner.RunAsync(PipelineRunner.BuildMode.Full, pipelineProgress);

            progress?.Report("Live compile complete.");
            Logger.Information("Live compile succeeded. Output: {OutputPath}", outputPath);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Live compile failed");
            return ex.Message;
        }
    }

    /// <summary>
    /// Original pre-built flow: copy vanilla, apply YAML diffs, insert shipped REL, patch bi2.
    /// Used as a fallback when devkitPPC / GameMod isn't available (e.g. end-user release).
    /// </summary>
    private async Task<string?> ApplyPreBuiltAsync(string vanillaPath, string outputPath,
        IReadOnlyList<string> optionalPatchIds, IProgress<string>? progress)
    {
        try
        {
            progress?.Report("Creating output directories...");
            var outputSys = Path.Combine(outputPath, "sys");
            var outputFiles = Path.Combine(outputPath, "files");
            Directory.CreateDirectory(outputSys);
            Directory.CreateDirectory(outputFiles);
            BuildStamp.Delete(outputPath); // only a completed patch earns a stamp

            // Release PatchData carries a stamp: check the files against it before touching the game.
            var patchData = new PatchDataFolder(PatchDataPath);
            var patchDataStamp = patchData.ReadStamp();
            if (patchDataStamp != null)
            {
                var problems = patchData.Verify(patchDataStamp);
                if (problems.Count > 0)
                    return "The app's patch data is damaged (" + string.Join("; ", problems.Take(3)) +
                           "). Reinstall WW-Online, or update it.";
            }

            var catalog = _patchCatalog.Catalog;
            var patchDiffsDir = patchData.PatchDiffsPath;
            var diffs = PatchPlan.LoadDiffs(patchDiffsDir, catalog, optionalPatchIds); // fail before copying anything

            progress?.Report("Copying vanilla game files...");
            await CopyFileAsync(
                Path.Combine(vanillaPath, "sys", "main.dol"),
                Path.Combine(outputSys, "main.dol"));
            await CopyFileAsync(
                Path.Combine(vanillaPath, "files", "RELS.arc"),
                Path.Combine(outputFiles, "RELS.arc"));
            await CopyFileAsync(
                Path.Combine(vanillaPath, "sys", "bi2.bin"),
                Path.Combine(outputSys, "bi2.bin"));
            GamePatchApplier.RestoreTouchedFiles(catalog, outputPath, vanillaPath, m => Logger.Information("[patches] {Message}", m));

            progress?.Report("Inserting puppet module into RELS.arc...");
            InsertPuppetRel(patchData.RelPath, Path.Combine(outputFiles, "RELS.arc"));

            progress?.Report("Applying patches...");
            var applier = new GamePatchApplier(OptionalPatchCatalogService.ProtectedRegions,
                m => Logger.Information("[patches] {Message}", m));
            applier.Apply(diffs, optionalPatchIds.Select(id => catalog.Find(id)!).ToList(),
                new GamePatchApplier.Targets(outputPath, vanillaPath, patchData.FreeSpaceOffsetsPath, patchData.AssetsPath));

            progress?.Report("Patching bi2.bin for 48MB memory...");
            PatchBi2(Path.Combine(outputSys, "bi2.bin"));

            BuildStamp.Write(outputPath, patchDataStamp?.CreateGameStamp(optionalPatchIds)
                                         ?? UnstampedPatchDataStamp(patchData, patchDiffsDir, catalog, optionalPatchIds));

            progress?.Report("Patching complete (pre-built).");
            Logger.Information("Pre-built patching completed. Output: {OutputPath}", outputPath);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Pre-built patching failed");
            return $"Patching failed: {ex.Message}";
        }
    }

    /// <summary>
    /// PatchData without a stamp (a dev build's copy of GameMod/build): hash the inputs themselves.
    /// Only good for noticing a changed selection; the sources check always reads it as stale.
    /// </summary>
    private static BuildStamp.Stamp UnstampedPatchDataStamp(PatchDataFolder patchData, string patchDiffsDir,
        OptionalPatchCatalog catalog, IReadOnlyList<string> optionalPatchIds)
    {
        var inputs = PatchPlan.GetDiffFiles(patchDiffsDir, catalog, optionalPatchIds).Select(f => f.Path)
            .Append(patchData.RelPath)
            .Concat(optionalPatchIds.Select(id => catalog.Find(id)!.SourcePath))
            .ToList();
        return new BuildStamp.Stamp(HashFiles(inputs), DateTime.UtcNow, inputs.Count, optionalPatchIds.ToArray(), "prebuilt");
    }

    private static string HashFiles(IEnumerable<string> paths)
    {
        using var sha = SHA256.Create();
        foreach (var path in paths)
        {
            var name = System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\n");
            sha.TransformBlock(name, 0, name.Length, null, 0);
            var bytes = File.ReadAllBytes(path);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    /// <summary>
    /// Walk up from the executable looking for GameMod/config.json. Works when running
    /// from bin/Debug/net9.0/ during development; returns null for release builds where the
    /// dev folder isn't shipped.
    /// </summary>
    internal static string? FindGameModFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "GameMod", PatcherConfig.ConfigFileName);
            if (File.Exists(candidate))
                return Path.Combine(dir.FullName, "GameMod");
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Copy every file of the extracted game that <paramref name="outputPath"/> doesn't have yet
    /// (all of it the first time, nothing after that). Existing files are left alone: the patch steps
    /// overwrite the ones they change, and GamePatchApplier restores the ones optional patches touch.
    /// </summary>
    /// <returns>How many files were copied.</returns>
    public static int CopyMissingGameFiles(string vanillaPath, string outputPath, IProgress<string>? progress = null)
    {
        var vanilla = Path.GetFullPath(vanillaPath);
        var missing = Directory.EnumerateFiles(vanilla, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(vanilla, f))
            .Where(rel => !string.Equals(rel, BuildStamp.FileName, StringComparison.OrdinalIgnoreCase))
            .Where(rel => !File.Exists(Path.Combine(outputPath, rel)))
            .ToList();
        if (missing.Count == 0) return 0;

        Logger.Information("Copying {Count} game files from {Vanilla} into {Output}", missing.Count, vanilla, outputPath);
        progress?.Report($"Copying the game into the patched folder (first time only): {missing.Count} files...");
        for (int i = 0; i < missing.Count; i++)
        {
            var target = Path.Combine(outputPath, missing[i]);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(vanilla, missing[i]), target, overwrite: false);
            if ((i + 1) % 250 == 0)
                progress?.Report($"Copying the game into the patched folder (first time only): {i + 1} of {missing.Count} files...");
        }
        return missing.Count;
    }

    private static async Task CopyFileAsync(string source, string destination)
    {
        var sourceBytes = await File.ReadAllBytesAsync(source);
        await File.WriteAllBytesAsync(destination, sourceBytes);
    }

    private static void InsertPuppetRel(string puppetRelPath, string relsArcPath)
    {
        if (!File.Exists(puppetRelPath))
            throw new FileNotFoundException($"Puppet REL not found at {puppetRelPath}");

        var puppetRelData = File.ReadAllBytes(puppetRelPath);

        // Read the puppet REL to get its module ID and find the profile location
        var puppetRel = new RelFile();
        puppetRel.Read(puppetRelData);
        var relId = puppetRel.Id; // Should be 0x58

        // Find g_profile_PUPPET in the REL by scanning for the known signature:
        //   mLayerID = -3 (0xFFFFFFFD), mListID = 3, mListPrio = -3, mPName = 0x58
        var (profileSectionIdx, profileOffset) = FindProfileInRel(puppetRel, relId);
        Logger.Information("Found puppet profile at section {Section} offset 0x{Offset:X}",
            profileSectionIdx, profileOffset);

        // Open RELS.arc
        var arcData = File.ReadAllBytes(relsArcPath);
        var relsArc = new RarcArchive();
        relsArc.Read(arcData);

        // Update profile list to reference our puppet's profile
        UpdateProfileList(relsArc, relId, profileSectionIdx, profileOffset);

        // Find and replace the existing REL with matching module ID (other RELs stay compressed)
        if (relsArc.ReplaceRelById(relId, puppetRelData))
            Logger.Information("Replaced REL with module ID 0x{Id:X2} in RELS.arc", relId);
        else
            throw new InvalidOperationException(
                $"REL with module ID 0x{relId:X2} not found in RELS.arc. " +
                "The vanilla RELS.arc may not contain a placeholder for this actor.");

        var savedArc = relsArc.SaveChanges();
        File.WriteAllBytes(relsArcPath, savedArc);
        Logger.Information("RELS.arc updated with puppet module");
    }

    /// <summary>
    /// Find g_profile_PUPPET in the REL by scanning for the actor_process_profile_definition signature.
    /// The profile has: mLayerID=-3 (0xFFFFFFFD), followed by mListID=3/mListPrio=-3 (0x0003FFFD),
    /// followed by mPName matching the actor ID.
    /// </summary>
    private static (int SectionIndex, uint Offset) FindProfileInRel(RelFile rel, uint actorId)
    {
        for (int i = 0; i < rel.Sections.Count; i++)
        {
            var section = rel.Sections[i];
            if (section.Data.Length < 12) continue;

            for (int offset = 0; offset <= section.Data.Length - 12; offset += 4)
            {
                // Check for mLayerID = -3 (0xFFFFFFFD)
                var val0 = BinaryPrimitives.ReadUInt32BigEndian(section.Data.AsSpan(offset, 4));
                if (val0 != 0xFFFFFFFD) continue;

                // Check for mListID=3, mListPrio=-3 → 0x0003FFFD
                var val1 = BinaryPrimitives.ReadUInt32BigEndian(section.Data.AsSpan(offset + 4, 4));
                if (val1 != 0x0003FFFD) continue;

                // Check mPName (upper 16 bits of next word). Current builds use the spawned proc
                // name (PUPPET_PROC_NAME); older pre-built RELs used the module ID.
                var val2 = BinaryPrimitives.ReadUInt16BigEndian(section.Data.AsSpan(offset + 8, 2));
                if (val2 == PuppetLayout.PUPPET_PROC_NAME || val2 == actorId)
                {
                    return (i, (uint)offset);
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not find actor profile with ID 0x{actorId:X2} in puppet REL sections");
    }

    /// <summary>
    /// Update f_pc_profile_lst.rel in RELS.arc so the game can find our puppet's profile.
    /// Without this, the game doesn't know where g_profile_PUPPET is in the new REL.
    /// </summary>
    private static void UpdateProfileList(RarcArchive relsArc, uint relId,
        int profileSectionIdx, uint profileOffset)
    {
        var profileListEntry = relsArc.GetFileEntry("f_pc_profile_lst.rel")
            ?? throw new InvalidOperationException("f_pc_profile_lst.rel not found in RELS.arc");

        var profileList = new RelFile();
        profileList.Read(profileListEntry.Data!);

        if (!profileList.RelocationEntriesForModule.ContainsKey(relId))
            throw new InvalidOperationException(
                $"REL ID 0x{relId:X2} not found in profile list relocations");

        var relocations = profileList.RelocationEntriesForModule[relId];
        if (relocations.Count == 0)
            throw new InvalidOperationException(
                $"No relocation entries for REL ID 0x{relId:X2} in profile list");

        // Update the first relocation to point to our puppet's profile
        relocations[0].SymbolAddress = profileOffset;
        relocations[0].SectionNumToRelocateAgainst = (byte)profileSectionIdx;

        Logger.Information(
            "Updated profile list relocation for module 0x{Id:X2}: section={Section} offset=0x{Offset:X}",
            relId, profileSectionIdx, profileOffset);

        profileList.SaveChanges(preserveSectionDataOffsets: true);
        profileListEntry.ReplaceContents(profileList.GetData()); // stays Yaz0, like the vanilla entry
    }

    private static void PatchBi2(string bi2Path)
    {
        var data = File.ReadAllBytes(bi2Path);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x04, 4), 0x03000000);
        File.WriteAllBytes(bi2Path, data);
    }
}
