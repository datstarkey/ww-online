using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;

namespace WWOnline.Patcher.Pipeline.Steps;

/// <summary>
/// Step 5: apply the required diffs plus the selected optional patches (config.OptionalPatches,
/// null = catalogue defaults) to main.dol / RELs, then the selected patches' @edit file edits.
/// </summary>
public class ApplyPatchesStep : IPipelineStep
{
    private readonly PatcherConfig _config;

    public ApplyPatchesStep(PatcherConfig config) => _config = config;

    public string Name => "Apply Patches";
    public int StepNumber => 5;

    public Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
    {
        if (!Directory.Exists(_config.PatchDiffsPath))
            throw new DirectoryNotFoundException(
                $"patch_diffs directory not found at {_config.PatchDiffsPath}. Run assemble step first.");

        var catalog = _config.LoadOptionalPatchCatalog();
        var selection = PatchPlan.ResolveStrict(catalog, _config.OptionalPatches);
        progress.Report(StepNumber, Name, "Optional patches: " + (selection.Ids.Count == 0 ? "none" : string.Join(", ", selection.Ids)));

        var diffs = PatchPlan.LoadDiffs(_config.PatchDiffsPath, catalog, selection.Ids);
        var regions = ProtectedRegions.FromPuppetSharedHeader(_config.PuppetSharedHeaderPath);
        var applier = new GamePatchApplier(regions, message => progress.Report(StepNumber, Name, message));
        applier.Apply(diffs, selection.Ids.Select(id => catalog.Find(id)!).ToList(),
            new GamePatchApplier.Targets(_config.GamePath, _config.ResolvedVanillaGamePath, _config.FreeSpaceOffsetsPath, _config.AssetsPath));

        progress.Report(StepNumber, Name, $"Applied {diffs.Count} patch diff(s)");
        return Task.CompletedTask;
    }
}
