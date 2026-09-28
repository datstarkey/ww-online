namespace WWOnline.Patcher.Tools;

/// <summary>
/// Result from running a devkitPPC tool via Process.Start.
/// </summary>
public record ToolResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;

    public void EnsureSuccess(string toolDescription)
    {
        if (!Success)
        {
            throw new InvalidOperationException(
                $"{toolDescription} failed (exit code {ExitCode}).\n" +
                $"stderr: {StandardError}\n" +
                $"stdout: {StandardOutput}");
        }
    }
}
