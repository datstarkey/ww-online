using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;

namespace WWOnline.Patcher.Pipeline.Steps;

/// <summary>
/// Step 1: Reset the output game to vanilla by copying main.dol / RELS.arc / bi2.bin from the
/// configured vanilla game (config.json vanilla_game_path, fallback GameMod/vanilla/), plus every
/// other file an optional patch can change (loose RELs, archives, videos), so a patch that is no
/// longer selected is really undone.
/// </summary>
public class ResetGameFilesStep : IPipelineStep
{
    private readonly PatcherConfig _config;

    public ResetGameFilesStep(PatcherConfig config) => _config = config;

    public string Name => "Reset Game Files";
    public int StepNumber => 1;

    public Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
    {
        // Ensure target directories exist
        Directory.CreateDirectory(_config.SysPath);
        Directory.CreateDirectory(_config.FilesPath);

        // Copy vanilla main.dol
        if (!File.Exists(_config.VanillaMainDolPath))
            throw new FileNotFoundException($"Vanilla main.dol not found at {_config.VanillaMainDolPath} (set vanilla_game_path in config.json)");
        File.Copy(_config.VanillaMainDolPath, _config.MainDolPath, overwrite: true);
        progress.Report(StepNumber, Name, "Reset main.dol");

        // Copy vanilla RELS.arc
        if (!File.Exists(_config.VanillaRelsArcPath))
            throw new FileNotFoundException($"Vanilla RELS.arc not found at {_config.VanillaRelsArcPath} (set vanilla_game_path in config.json)");
        File.Copy(_config.VanillaRelsArcPath, _config.RelsArcPath, overwrite: true);
        progress.Report(StepNumber, Name, "Reset RELS.arc");

        // Copy vanilla bi2.bin (optional)
        if (File.Exists(_config.VanillaBi2Path))
        {
            File.Copy(_config.VanillaBi2Path, _config.Bi2Path, overwrite: true);
            progress.Report(StepNumber, Name, "Reset bi2.bin");
        }

        GamePatchApplier.RestoreTouchedFiles(_config.LoadOptionalPatchCatalog(), _config.GamePath, _config.ResolvedVanillaGamePath,
            message => progress.Report(StepNumber, Name, message));

        return Task.CompletedTask;
    }
}
