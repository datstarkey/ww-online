using WWOnline.Patcher.Config;
using WWOnline.Patcher.Tools;

namespace WWOnline.Patcher.Pipeline.Steps;

/// <summary>
/// Step 3: Compile link_draw_hook.c and generate link_draw_hook.asm.
/// </summary>
public class GenerateHookAsmStep : IPipelineStep
{
    private readonly PatcherConfig _config;
    private readonly DevKitPpcToolchain _toolchain;

    public GenerateHookAsmStep(PatcherConfig config, DevKitPpcToolchain toolchain)
    {
        _config = config;
        _toolchain = toolchain;
    }

    public string Name => "Generate Hook ASM";
    public int StepNumber => 3;

    public async Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
    {
        var generator = new HookAsmGenerator(_toolchain, _config);

        progress.Report(StepNumber, Name, "Compiling link_draw_hook.c...");
        var asmPath = await generator.GenerateHookAsmFileAsync(ct);

        progress.Report(StepNumber, Name, $"Generated {asmPath}");
    }
}
