using WWOnline.Patcher.Config;
using WWOnline.Patcher.WorldData;

namespace WWOnline.Patcher.Pipeline.Steps;

/// <summary>
/// Step 7: build the shared world's switch table (<see cref="SwitchTable"/>) from the player's own
/// vanilla stage data and write it next to the patched game. Nothing Nintendo-derived goes in the repo.
/// </summary>
public class BuildSwitchTableStep : IPipelineStep
{
    private readonly PatcherConfig _config;

    public BuildSwitchTableStep(PatcherConfig config) => _config = config;

    public string Name => "Build switch table";
    public int StepNumber => 7;

    public Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
    {
        progress.Report(StepNumber, Name, SwitchTableBuilder.BuildAndWrite(_config.ResolvedVanillaGamePath, _config.GamePath));
        return Task.CompletedTask;
    }
}
