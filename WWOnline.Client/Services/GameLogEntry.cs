using Avalonia.Media;

namespace WWOnline.Services;

public enum GameLogEventType
{
    Joined,
    Left,
    ZoneChanged,
    Info
}

public class GameLogEntry
{
    // Cached tag brushes (dark design palette) — one set shared across all entries.
    private static IBrush Fg(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static readonly (IBrush fg, IBrush bg) JoinTag = (Fg("#7EE29A"), Fg("#123026"));
    private static readonly (IBrush fg, IBrush bg) LeftTag = (Fg("#FF9A9A"), Fg("#2A1618"));
    private static readonly (IBrush fg, IBrush bg) ZoneTag = (Fg("#7BE6D8"), Fg("#0F2A2A"));
    private static readonly (IBrush fg, IBrush bg) InfoTag = (Fg("#F4C76A"), Fg("#2A2311"));

    public DateTime Timestamp { get; init; } = DateTime.Now;
    public GameLogEventType EventType { get; init; }
    public string PlayerName { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Zone { get; init; }

    public string TimestampText => Timestamp.ToString("HH:mm:ss");

    public string EventTypeText => EventType switch
    {
        GameLogEventType.Joined => "JOIN",
        GameLogEventType.Left => "LEFT",
        GameLogEventType.ZoneChanged => "ZONE",
        GameLogEventType.Info => "INFO",
        _ => "---"
    };

    private (IBrush fg, IBrush bg) Tag => EventType switch
    {
        GameLogEventType.Joined => JoinTag,
        GameLogEventType.Left => LeftTag,
        GameLogEventType.ZoneChanged => ZoneTag,
        _ => InfoTag
    };

    public IBrush TagForeground => Tag.fg;
    public IBrush TagBackground => Tag.bg;
}
