using WWOnline.Patcher.Config;
using WWOnline.Patcher.Tools;

namespace WWOnline.Patcher.Pipeline.Steps;

/// <summary>
/// Step 2: Compile puppet.c -> ELF -> REL (BuildPath/d_a_puppet.rel), and insert it into the
/// game's RELS.arc unless <see cref="InsertIntoRelsArc"/> is off (PatchData builds have no game).
/// </summary>
public class BuildRelStep : IPipelineStep
{
    private readonly PatcherConfig _config;
    private readonly DevKitPpcToolchain _toolchain;

    // Default build parameters for the puppet REL
    public string CSourcePath { get; set; } = "./src/puppet_link/puppet.c";
    public uint ModuleId { get; set; } = 0x58;
    public string ActorProfileName { get; set; } = "g_profile_PUPPET";

    /// <summary>False = only build the REL (--build-patchdata: no game files to insert it into).</summary>
    public bool InsertIntoRelsArc { get; set; } = true;

    /// <summary>File name of the built REL (in BuildPath, and in PatchData/).</summary>
    public const string RelFileName = "d_a_puppet.rel";

    public BuildRelStep(PatcherConfig config, DevKitPpcToolchain toolchain)
    {
        _config = config;
        _toolchain = toolchain;
    }

    public string Name => "Build REL";
    public int StepNumber => 2;

    public async Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_config.BuildPath);

        var cSrcPath = Path.IsPathRooted(CSourcePath)
            ? CSourcePath
            : Path.Combine(_config.GameModPath, CSourcePath);

        var baseName = Path.GetFileNameWithoutExtension(cSrcPath);
        var linkerScript = Path.Combine(_config.IncludePath, "ww_linker.ld");

        // Step 1: Compile C to ELF object
        var elfPath = Path.Combine(_config.BuildPath, baseName + ".o");
        progress.Report(StepNumber, Name, "Compiling C to object file...");
        var compileResult = await _toolchain.CompileAsync(cSrcPath, elfPath, ct: ct);
        compileResult.EnsureSuccess("Compile puppet.c");

        // Step 2: Link with relocatable flag
        var linkedElfPath = Path.Combine(_config.BuildPath, baseName + "_linked.o");
        progress.Report(StepNumber, Name, "Linking...");
        var linkResult = await _toolchain.LinkRelocatableAsync(linkerScript, elfPath, linkedElfPath, ct: ct);
        linkResult.EnsureSuccess("Link puppet ELF");

        // Step 3: Disassemble for debugging (optional, non-fatal)
        var disasmElfPath = Path.Combine(_config.BuildPath, baseName + "_disassembled_elf.asm");
        var dumpResult = await _toolchain.ObjDumpAsync(linkedElfPath, showRelocations: true, ct: ct);
        if (dumpResult.Success)
            await File.WriteAllTextAsync(disasmElfPath, dumpResult.StandardOutput, ct);

        // Step 4: Convert ELF to REL (and insert into RELS.arc)
        var outRelPath = Path.Combine(_config.BuildPath, $"d_a_{baseName}.rel");
        progress.Report(StepNumber, Name, InsertIntoRelsArc ? "Converting ELF to REL..." : "Converting ELF to REL (not inserted: no game)...");
        ElfToRelConverter.ConvertAndInsertIntoRelsArc(
            linkedElfPath, outRelPath, ModuleId, ActorProfileName, InsertIntoRelsArc ? _config.RelsArcPath : null);

        // Step 5: Disassemble REL for debugging (optional, non-fatal)
        var disasmRelPath = Path.Combine(_config.BuildPath, baseName + "_disassembled_rel.asm");
        var relDumpResult = await _toolchain.ObjDumpAsync(outRelPath, isBinary: true, ct: ct);
        if (relDumpResult.Success)
            await File.WriteAllTextAsync(disasmRelPath, relDumpResult.StandardOutput, ct);

        progress.Report(StepNumber, Name, $"Built {outRelPath}");
    }
}
