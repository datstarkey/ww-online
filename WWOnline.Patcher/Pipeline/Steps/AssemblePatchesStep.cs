using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Tools;

namespace WWOnline.Patcher.Pipeline.Steps;

/// <summary>
/// Step 4: assemble the required .asm patches (src/patches/*.asm) and EVERY optional patch
/// (src/patches/optional/*.asm) into YAML diffs: patch_diffs/&lt;name&gt;_diff.yaml and
/// patch_diffs/optional/&lt;id&gt;_diff.yaml. Assembling all optional patches regardless of the
/// selection keeps free-space addresses stable and lets the client's pre-built PatchData apply
/// any selection. Which ones are applied is ApplyPatchesStep's job.
/// </summary>
public class AssemblePatchesStep : IPipelineStep
{
    private readonly PatcherConfig _config;
    private readonly DevKitPpcToolchain _toolchain;

    public AssemblePatchesStep(PatcherConfig config, DevKitPpcToolchain toolchain)
    {
        _config = config;
        _toolchain = toolchain;
    }

    public string Name => "Assemble Patches";
    public int StepNumber => 4;

    public async Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
    {
        CheckFreeSpaceStart();

        Directory.CreateDirectory(_config.PatchDiffsPath);
        Directory.CreateDirectory(_config.OptionalPatchDiffsPath);
        foreach (var oldDiff in Directory.GetFiles(_config.PatchDiffsPath, "*" + PatchDiffFile.Suffix)
                     .Concat(Directory.GetFiles(_config.OptionalPatchDiffsPath, "*" + PatchDiffFile.Suffix)))
            File.Delete(oldDiff);

        var required = GetRequiredPatchFiles(_config);
        var catalog = _config.LoadOptionalPatchCatalog();
        var optional = catalog.Patches.Where(p => p.HasAsm).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();

        if (required.Count == 0)
        {
            progress.Report(StepNumber, Name, "No .asm patch files found");
            return;
        }
        progress.Report(StepNumber, Name, $"Found {required.Count} required and {optional.Count} optional patch files");

        using var asmParser = new AsmParser(_toolchain, _config);

        foreach (var patchPath in required)
            await AssembleAsync(asmParser, patchPath, _config.PatchDiffsPath, progress, ct);
        foreach (var patch in optional)
            await AssembleAsync(asmParser, patch.SourcePath, _config.OptionalPatchDiffsPath, progress, ct);

        WriteCustomSymbols(_config.CustomSymbolsPath, asmParser.CustomSymbols);
    }

    /// <summary>
    /// The required patches, in assembly order (by file name): every src/patches/*.asm except the
    /// macro include, plus the generated link_draw_hook.asm from wherever this build put it (an
    /// in-tree copy is ignored when the build output lives elsewhere).
    /// </summary>
    public static IReadOnlyList<string> GetRequiredPatchFiles(PatcherConfig config)
    {
        var files = Directory.GetFiles(config.PatchesSrcPath, "*.asm")
            .Where(f => Path.GetFileName(f) is not ("asm_macros.asm" or PatcherConfig.HookAsmFileName))
            .ToList();
        if (File.Exists(config.GeneratedHookAsmPath)) files.Add(config.GeneratedHookAsmPath);
        return files.OrderBy(Path.GetFileName, StringComparer.Ordinal).ToList();
    }

    private async Task AssembleAsync(AsmParser asmParser, string patchPath, string outputDir, PipelineProgress progress, CancellationToken ct)
    {
        var patchName = Path.GetFileNameWithoutExtension(patchPath);
        progress.Report(StepNumber, Name, $"Assembling {patchName}...");
        var diff = await asmParser.AssemblePatchAsync(patchPath, ct);

        var files = diff.FilePatches.ToDictionary(
            f => f.Key,
            f => (IReadOnlyList<PatchChunk>)f.Value.Select(c => new PatchChunk(c.Key, c.Value.Data, c.Value.Relocations.ToList())).ToList());
        var diffPath = Path.Combine(outputDir, patchName + PatchDiffFile.Suffix);
        PatchDiffFile.Write(diffPath, files);
        progress.Report(StepNumber, Name, $"Created {Path.GetRelativePath(_config.PatchDiffsPath, diffPath)}");
    }

    /// <summary>
    /// @NextFreeSpace in main.dol must start past our scratch region (puppet_shared.h), which sits
    /// right where the DOL's free space used to begin. DolPatcher's Text2 section then covers the
    /// scratch block (zero-filled) and moves the boot stack above the custom code.
    /// </summary>
    private void CheckFreeSpaceStart()
    {
        if (!File.Exists(_config.PuppetSharedHeaderPath)) return;
        var regions = ProtectedRegions.FromPuppetSharedHeader(_config.PuppetSharedHeaderPath);
        var offsets = PatcherConfig.LoadFreeSpaceOffsets(_config.FreeSpaceOffsetsPath);
        if (offsets.TryGetValue("sys/main.dol", out var start) && start < regions.FirstFreeSpaceAddress)
            throw new InvalidOperationException(
                $"free_space_start_offsets.txt puts main.dol free space at 0x{start:X8}, inside the scratch region; " +
                $"it must be at least 0x{regions.FirstFreeSpaceAddress:X8} (SCRATCH_REGION_END).");
    }

    private static void WriteCustomSymbols(string path, Dictionary<string, Dictionary<string, uint>> symbols)
    {
        using var writer = new StreamWriter(path);
        foreach (var (filePath, fileSymbols) in symbols)
        {
            writer.WriteLine($"{filePath}:");
            foreach (var (name, address) in fileSymbols)
                writer.WriteLine($"  {name}: 0x{address:X8}");
        }
    }
}
