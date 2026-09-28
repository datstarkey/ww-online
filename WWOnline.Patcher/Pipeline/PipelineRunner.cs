using WWOnline.Patcher.Config;
using WWOnline.Patcher.Pipeline.Steps;
using WWOnline.Patcher.Tools;

namespace WWOnline.Patcher.Pipeline;

/// <summary>
/// Orchestrates the build pipeline. Supports Full/Compile/Patches/PatchData modes.
/// </summary>
public class PipelineRunner
{
    private readonly PatcherConfig _config;
    private readonly DevKitPpcToolchain _toolchain;

    public PipelineRunner(PatcherConfig config)
    {
        _config = config;
        _toolchain = new DevKitPpcToolchain(config);
    }

    public enum BuildMode
    {
        /// <summary>Full build (all 6 steps).</summary>
        Full,
        /// <summary>Quick compile only (steps 1-3).</summary>
        Compile,
        /// <summary>Assemble + apply patches only (steps 4-5).</summary>
        Patches,
        /// <summary>
        /// Steps 2-4 without a game: build the REL (not inserted anywhere), generate the hook ASM
        /// and assemble every patch. The output PatchDataBuilder packages (--build-patchdata).
        /// </summary>
        PatchData,
    }

    /// <summary>
    /// Run the pipeline in the specified mode.
    /// </summary>
    public async Task RunAsync(BuildMode mode, PipelineProgress? progress = null,
        CancellationToken ct = default)
    {
        progress ??= new PipelineProgress();
        var steps = GetSteps(mode);

        // Fail before touching the game if the optional patch selection is invalid
        // (unknown ids, conflicting patches, malformed catalogue header).
        _config.ResolveOptionalPatches();

        // Any run on a game invalidates its stamp; only a completed Full build earns a new one.
        // (A PatchData build has no game.)
        if (mode != BuildMode.PatchData)
            BuildStamp.Delete(_config);

        foreach (var step in steps)
        {
            progress.Report(step.StepNumber, step.Name, $"Starting {step.Name}...");

            try
            {
                await step.ExecuteAsync(progress, ct);
                progress.Report(step.StepNumber, step.Name, $"{step.Name} completed.",
                    PipelineStatus.Completed);
            }
            catch (Exception ex)
            {
                progress.Report(step.StepNumber, step.Name,
                    $"{step.Name} failed: {ex.Message}", PipelineStatus.Failed);
                throw;
            }
        }

        if (mode == BuildMode.Full)
            BuildStamp.Write(_config);
    }

    private List<IPipelineStep> GetSteps(BuildMode mode)
    {
        return mode switch
        {
            BuildMode.Full =>
            [
                new ResetGameFilesStep(_config),
                new BuildRelStep(_config, _toolchain),
                new GenerateHookAsmStep(_config, _toolchain),
                new AssemblePatchesStep(_config, _toolchain),
                new ApplyPatchesStep(_config),
                new PatchBi2Step(_config),
            ],
            BuildMode.Compile =>
            [
                new ResetGameFilesStep(_config),
                new BuildRelStep(_config, _toolchain),
                new GenerateHookAsmStep(_config, _toolchain),
            ],
            BuildMode.Patches =>
            [
                new AssemblePatchesStep(_config, _toolchain),
                new ApplyPatchesStep(_config),
            ],
            BuildMode.PatchData =>
            [
                new BuildRelStep(_config, _toolchain) { InsertIntoRelsArc = false },
                new GenerateHookAsmStep(_config, _toolchain),
                new AssemblePatchesStep(_config, _toolchain),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
