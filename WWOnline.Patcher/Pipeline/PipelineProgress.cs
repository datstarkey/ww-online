namespace WWOnline.Patcher.Pipeline;

/// <summary>
/// Progress reporting for the build pipeline.
/// </summary>
public class PipelineProgress
{
    public event Action<PipelineProgressEvent>? OnProgress;

    public void Report(int stepNumber, string stepName, string message, PipelineStatus status = PipelineStatus.Running)
    {
        OnProgress?.Invoke(new PipelineProgressEvent(stepNumber, stepName, message, status));
    }
}

public record PipelineProgressEvent(int StepNumber, string StepName, string Message, PipelineStatus Status);

public enum PipelineStatus
{
    Running,
    Completed,
    Failed,
}
