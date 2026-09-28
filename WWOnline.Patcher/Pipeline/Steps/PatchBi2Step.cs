using System.Buffers.Binary;
using WWOnline.Patcher.Config;

namespace WWOnline.Patcher.Pipeline.Steps;

/// <summary>
/// Step 6: Patch bi2.bin for 48MB memory.
/// </summary>
public class PatchBi2Step : IPipelineStep
{
    private readonly PatcherConfig _config;

    public PatchBi2Step(PatcherConfig config) => _config = config;

    public string Name => "Patch bi2.bin";
    public int StepNumber => 6;

    public Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default)
    {
        if (!File.Exists(_config.Bi2Path))
        {
            progress.Report(StepNumber, Name, "bi2.bin not found, skipping");
            return Task.CompletedTask;
        }

        var data = File.ReadAllBytes(_config.Bi2Path);

        // Write 48MB (0x03000000) at offset 0x04 (big-endian)
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x04, 4), 0x03000000);

        File.WriteAllBytes(_config.Bi2Path, data);
        progress.Report(StepNumber, Name, "Patched bi2.bin for 48MB memory");

        return Task.CompletedTask;
    }
}
