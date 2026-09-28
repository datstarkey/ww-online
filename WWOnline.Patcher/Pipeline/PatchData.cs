using System.Security.Cryptography;
using System.Text.Json;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Pipeline.Steps;

namespace WWOnline.Patcher.Pipeline;

/// <summary>One file in a PatchData folder ('/'-separated path relative to it).</summary>
public sealed record PatchDataFile(string Path, long Size, string Sha256);

/// <summary>
/// PatchData/patchdata-stamp.json: what a pre-built PatchData folder was built from and what it
/// contains. It records the per-file source hashes (<see cref="BuildStamp.SourceEntry"/>) of the
/// required sources and of each optional patch separately, so the source hash of ANY selection
/// can be recomputed without the sources: <see cref="ComputeSourceHash"/> gives exactly what
/// <see cref="BuildStamp.ComputeSourceHash(PatcherConfig, IReadOnlyCollection{string})"/> gives
/// for the same sources. The client writes that hash into the game's stamp after a pre-built
/// patch, and an installed app compares the two to spot an update that changed the game code.
/// </summary>
/// <param name="Format">Stamp format (<see cref="CurrentFormat"/>).</param>
/// <param name="RequiredSources">Sources every build uses (BuildStamp.GetRequiredSourceFiles).</param>
/// <param name="OptionalPatchSources">Per optional patch id: its .asm and assets.</param>
/// <param name="DefaultOptionalPatches">The catalogue defaults at build time.</param>
/// <param name="DefaultSourceHash">The source hash of the default selection (informational).</param>
/// <param name="Files">Every file in the folder except this stamp.</param>
public sealed record PatchDataStamp(
    int Format,
    DateTime BuiltAtUtc,
    BuildStamp.SourceEntry[] RequiredSources,
    Dictionary<string, BuildStamp.SourceEntry[]> OptionalPatchSources,
    string[] DefaultOptionalPatches,
    string DefaultSourceHash,
    PatchDataFile[] Files)
{
    public const int CurrentFormat = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>The source hash of a build with these optional patches (unknown ids add no files).</summary>
    public string ComputeSourceHash(IReadOnlyCollection<string> optionalPatchIds) =>
        BuildStamp.ComputeSourceHash(
            RequiredSources.Concat(optionalPatchIds.SelectMany(id => OptionalPatchSources.GetValueOrDefault(id) ?? [])),
            optionalPatchIds);

    /// <summary>The game stamp a pre-built patch with <paramref name="optionalPatchIds"/> writes.</summary>
    public BuildStamp.Stamp CreateGameStamp(IReadOnlyCollection<string> optionalPatchIds) =>
        new(ComputeSourceHash(optionalPatchIds), DateTime.UtcNow,
            RequiredSources.Length + optionalPatchIds.Sum(id => OptionalPatchSources.GetValueOrDefault(id)?.Length ?? 0),
            optionalPatchIds.Distinct(StringComparer.Ordinal).OrderBy(i => i, StringComparer.Ordinal).ToArray(), "prebuilt");

    /// <summary>
    /// The stamp for the sources under <paramref name="config"/>'s GameMod (Files empty). Every
    /// catalogue patch is recorded, since PatchData carries all of them.
    /// </summary>
    public static PatchDataStamp FromSources(PatcherConfig config)
    {
        var catalog = config.LoadOptionalPatchCatalog();
        var required = BuildStamp.HashSourceFiles(config, BuildStamp.GetRequiredSourceFiles(config)).ToArray();
        var optional = catalog.Patches
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToDictionary(p => p.Id, p => BuildStamp.HashSourceFiles(config, BuildStamp.GetOptionalPatchSourceFiles(config, p.Id)).ToArray(),
                StringComparer.Ordinal);
        var defaults = catalog.DefaultIds.ToArray();
        var stamp = new PatchDataStamp(CurrentFormat, DateTime.UtcNow, required, optional, defaults, "", []);
        return stamp with { DefaultSourceHash = stamp.ComputeSourceHash(defaults) };
    }

    /// <summary>Same sources (the build time and file list aside)?</summary>
    public bool SameSources(PatchDataStamp other) =>
        RequiredSources.SequenceEqual(other.RequiredSources) &&
        OptionalPatchSources.Count == other.OptionalPatchSources.Count &&
        OptionalPatchSources.All(kv => other.OptionalPatchSources.TryGetValue(kv.Key, out var o) && kv.Value.SequenceEqual(o));

    public void Write(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));

    /// <summary>Null when the file is missing; throws InvalidDataException when it is unreadable.</summary>
    public static PatchDataStamp? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var stamp = JsonSerializer.Deserialize<PatchDataStamp>(File.ReadAllText(path))
                ?? throw new InvalidDataException("empty");
            if (stamp.Format != CurrentFormat)
                throw new InvalidDataException($"format {stamp.Format}, expected {CurrentFormat}");
            if (stamp.RequiredSources == null || stamp.OptionalPatchSources == null || stamp.Files == null)
                throw new InvalidDataException("missing fields");
            return stamp;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            throw new InvalidDataException($"{path} is not a valid PatchData stamp: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// The pre-built patch data an installed app patches the game from (no devkitPPC, no GameMod):
/// <code>
/// PatchData/
///   d_a_puppet.rel                       the puppet REL (module 0x58), inserted into RELS.arc
///   patch_diffs/&lt;required&gt;_diff.yaml    always applied (use_extra_memory, link_draw_hook)
///   patch_diffs/optional/&lt;id&gt;_diff.yaml every optional patch with ASM (any selection can apply)
///   optional/&lt;id&gt;.asm                   the optional patch catalogue (the headers are what's read)
///   assets/&lt;file&gt;                       files replace-file edits copy into the game
///   free_space_start_offsets.txt         where free space starts (DolPatcher's Text2 layout)
///   patchdata-stamp.json                 PatchDataStamp: source hashes + file list
/// </code>
/// Built by <see cref="PatchDataBuilder"/> (--build-patchdata; the release workflow runs it).
/// </summary>
public sealed class PatchDataFolder(string root)
{
    public const string StampFileName = "patchdata-stamp.json";
    public const string PatchDiffsDirectoryName = "patch_diffs";
    public const string AssetsDirectoryName = "assets";
    public const string FreeSpaceOffsetsFileName = "free_space_start_offsets.txt";

    public string Root { get; } = root;
    public string RelPath => Path.Combine(Root, BuildRelStep.RelFileName);
    public string PatchDiffsPath => Path.Combine(Root, PatchDiffsDirectoryName);
    public string CatalogPath => Path.Combine(Root, OptionalPatchCatalog.DirectoryName);
    public string AssetsPath => Path.Combine(Root, AssetsDirectoryName);
    public string FreeSpaceOffsetsPath => Path.Combine(Root, FreeSpaceOffsetsFileName);
    public string StampPath => Path.Combine(Root, StampFileName);

    public OptionalPatchCatalog LoadCatalog() => OptionalPatchCatalog.Load(CatalogPath);

    /// <summary>See <see cref="PatchDataStamp.Read"/>.</summary>
    public PatchDataStamp? ReadStamp() => PatchDataStamp.Read(StampPath);

    /// <summary>
    /// Files the stamp lists that are missing or differ, and files it doesn't list (empty = intact).
    /// An unlisted file matters: an extra patch_diffs/*_diff.yaml would be applied as a required patch.
    /// </summary>
    public IReadOnlyList<string> Verify(PatchDataStamp stamp)
    {
        var problems = new List<string>();
        foreach (var file in stamp.Files)
        {
            var path = Path.Combine(Root, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) problems.Add($"{file.Path} is missing");
            else if (new FileInfo(path).Length != file.Size || HashFile(path) != file.Sha256) problems.Add($"{file.Path} differs");
        }
        var listed = new HashSet<string>(stamp.Files.Select(f => f.Path), StringComparer.Ordinal);
        problems.AddRange(RelativeFiles().Where(f => !listed.Contains(f.Relative)).Select(f => $"{f.Relative} is not in the stamp"));
        return problems;
    }

    /// <summary>Every file under the folder except the stamp, '/'-separated, ordinal order.</summary>
    public PatchDataFile[] ListFiles() => RelativeFiles()
        .Select(f => new PatchDataFile(f.Relative, new FileInfo(f.Full).Length, HashFile(f.Full)))
        .ToArray();

    private IEnumerable<(string Full, string Relative)> RelativeFiles() =>
        (Directory.Exists(Root) ? Directory.GetFiles(Root, "*", SearchOption.AllDirectories) : [])
        .Select(f => (Full: f, Relative: Path.GetRelativePath(Root, f).Replace('\\', '/')))
        .Where(f => f.Relative != StampFileName)
        .OrderBy(f => f.Relative, StringComparer.Ordinal);

    internal static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

/// <summary>
/// Builds a complete PatchData folder from GameMod sources with devkitPPC and no game files
/// (--build-patchdata; the release workflow runs it on Linux). It runs the pipeline's own steps
/// (<see cref="PipelineRunner.BuildMode.PatchData"/>: build the REL, generate the hook ASM, assemble
/// every required and optional patch) into a temp folder, so the source tree is never written, then
/// packages the result with the catalogue, assets and <see cref="PatchDataStamp"/>.
/// </summary>
public static class PatchDataBuilder
{
    /// <param name="gameModPath">The GameMod/ folder (sources only; config.json is not read).</param>
    /// <param name="devKitPpcPath">devkitPPC root (<see cref="PatcherConfig.DefaultDevKitPpcPath"/> when unsure).</param>
    /// <param name="outDir">Created if missing. An existing folder must be empty or a previous PatchData
    /// (it has a patchdata-stamp.json); it is replaced.</param>
    public static async Task<PatchDataStamp> BuildAsync(string gameModPath, string devKitPpcPath, string outDir,
        PipelineProgress? progress = null, CancellationToken ct = default)
    {
        progress ??= new PipelineProgress();
        gameModPath = Path.GetFullPath(gameModPath);
        outDir = Path.GetFullPath(outDir);

        var intermediate = Path.Combine(Path.GetTempPath(), "wwo-patchdata-" + Guid.NewGuid().ToString("N")[..12]);
        var config = PatcherConfig.Create(gamePath: "", devKitPpcPath, gameModPath);
        config.BuildOutputPath = intermediate;
        ValidateSources(config);
        ValidateOutputDirectory(outDir, gameModPath);
        config.ValidateToolchain();

        try
        {
            // Hash before and after: a source edited mid-build would leave a stamp that doesn't
            // describe what was compiled.
            var before = PatchDataStamp.FromSources(config);
            await new PipelineRunner(config).RunAsync(PipelineRunner.BuildMode.PatchData, progress, ct);
            var after = PatchDataStamp.FromSources(config);
            if (!before.SameSources(after))
                throw new InvalidOperationException("GameMod sources changed during the build; run it again.");

            // Only now replace the previous output, so a failed build leaves it as it was.
            ValidateOutputDirectory(outDir, gameModPath);
            ClearOutputDirectory(outDir);
            var stamp = Package(config, outDir, after);
            progress.Report(0, "PatchData", $"PatchData written to {outDir}: {stamp.Files.Length} files, " +
                $"{stamp.OptionalPatchSources.Count} optional patches, default source hash {stamp.DefaultSourceHash[..12]}",
                PipelineStatus.Completed);
            return stamp;
        }
        finally
        {
            try { if (Directory.Exists(intermediate)) Directory.Delete(intermediate, recursive: true); }
            catch (IOException) { /* best effort */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
        }
    }

    /// <summary>
    /// Copy a finished build (<paramref name="config"/>'s BuildPath and PatchDiffsPath) plus the
    /// catalogue, the assets the patches need and free_space_start_offsets.txt into
    /// <paramref name="outDir"/>, then write the stamp (with the file list).
    /// </summary>
    public static PatchDataStamp Package(PatcherConfig config, string outDir, PatchDataStamp sources)
    {
        var folder = new PatchDataFolder(outDir);
        Directory.CreateDirectory(outDir);

        var rel = Path.Combine(config.BuildPath, BuildRelStep.RelFileName);
        if (!File.Exists(rel)) throw new FileNotFoundException($"The build produced no {BuildRelStep.RelFileName}", rel);
        File.Copy(rel, folder.RelPath, overwrite: true);

        var catalog = config.LoadOptionalPatchCatalog();
        var required = PatchPlan.RequiredDiffFiles(config.PatchDiffsPath);
        if (required.Count == 0) throw new FileNotFoundException($"The build produced no required patch diffs in {config.PatchDiffsPath}");
        CopyInto(required, folder.PatchDiffsPath);
        CopyInto(catalog.Patches.Where(p => p.HasAsm).Select(p =>
        {
            var diff = PatchPlan.OptionalDiffPath(config.PatchDiffsPath, p.Id);
            return File.Exists(diff) ? diff : throw new FileNotFoundException($"Optional patch {p.Id} was not assembled", diff);
        }), PatchPlan.OptionalDiffsDirectory(folder.PatchDiffsPath));

        CopyInto(catalog.Patches.Select(p => p.SourcePath), folder.CatalogPath);
        CopyInto(catalog.Patches.SelectMany(p => p.Edits.OfType<ReplaceFileEdit>()).Select(e => e.AssetName).Distinct(StringComparer.Ordinal)
            .Select(name =>
            {
                var asset = Path.Combine(config.AssetsPath, name);
                return File.Exists(asset) ? asset : throw new FileNotFoundException($"Asset {name} (a replace-file edit) is missing", asset);
            }), folder.AssetsPath);

        if (!File.Exists(config.FreeSpaceOffsetsPath)) throw new FileNotFoundException("free_space_start_offsets.txt is missing", config.FreeSpaceOffsetsPath);
        File.Copy(config.FreeSpaceOffsetsPath, folder.FreeSpaceOffsetsPath, overwrite: true);

        var stamp = sources with { BuiltAtUtc = DateTime.UtcNow, Files = folder.ListFiles() };
        stamp.Write(folder.StampPath);
        return stamp;
    }

    private static void CopyInto(IEnumerable<string> files, string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var file in files)
            File.Copy(file, Path.Combine(dir, Path.GetFileName(file)), overwrite: true);
    }

    private static void ValidateSources(PatcherConfig config)
    {
        foreach (var required in new[]
                 {
                     Path.Combine(config.PuppetLinkSrcPath, "puppet.c"),
                     Path.Combine(config.PuppetLinkSrcPath, "link_draw_hook.c"),
                     config.PuppetSharedHeaderPath,
                     config.LinkerScriptPath,
                     config.FreeSpaceOffsetsPath,
                 })
        {
            if (!File.Exists(required))
                throw new FileNotFoundException($"GameMod source not found: {required} (is {config.GameModPath} the GameMod folder?)", required);
        }
    }

    /// <summary>
    /// The output folder must be new, empty, or a previous PatchData build (it has a
    /// patchdata-stamp.json), not inside GameMod/ and not a folder that contains GameMod/ (or a drive
    /// root): never clear a folder that isn't clearly ours.
    /// </summary>
    public static void ValidateOutputDirectory(string outDir, string gameModPath)
    {
        outDir = Path.GetFullPath(outDir);
        gameModPath = Path.GetFullPath(gameModPath);
        if (IsSameOrInside(outDir, gameModPath))
            throw new InvalidOperationException($"The PatchData output folder can't be inside GameMod/: {outDir}");
        if (IsSameOrInside(gameModPath, outDir) || Path.GetPathRoot(outDir) == outDir)
            throw new InvalidOperationException($"The PatchData output folder can't contain GameMod/ or be a drive root: {outDir}");
        if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any() &&
            !File.Exists(Path.Combine(outDir, PatchDataFolder.StampFileName)))
            throw new InvalidOperationException(
                $"{outDir} is not empty and is not a previous PatchData build (no {PatchDataFolder.StampFileName}); pick an empty or new folder.");
    }

    /// <summary>Is <paramref name="path"/> <paramref name="folder"/> or somewhere under it (both full paths)?</summary>
    private static bool IsSameOrInside(string path, string folder)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative == "." ||
               !(relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative));
    }

    /// <summary>Empty <paramref name="outDir"/> (after <see cref="ValidateOutputDirectory"/>).</summary>
    private static void ClearOutputDirectory(string outDir)
    {
        if (Directory.Exists(outDir) && Directory.EnumerateFileSystemEntries(outDir).Any())
            Directory.Delete(outDir, recursive: true);
        Directory.CreateDirectory(outDir);
    }
}
