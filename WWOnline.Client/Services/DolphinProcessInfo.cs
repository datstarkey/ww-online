namespace WWOnline.Services;

public record DolphinProcessInfo(int ProcessId, string WindowTitle)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(WindowTitle)
            ? $"Dolphin (PID {ProcessId})"
            : $"{WindowTitle} (PID {ProcessId})";
}
