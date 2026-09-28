namespace WWOnline.Patcher.Pipeline;

/// <summary>
/// Interface for a single step in the build pipeline.
/// </summary>
public interface IPipelineStep
{
    string Name { get; }
    int StepNumber { get; }
    Task ExecuteAsync(PipelineProgress progress, CancellationToken ct = default);
}
