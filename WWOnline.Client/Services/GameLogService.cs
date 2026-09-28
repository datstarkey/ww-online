using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Serilog;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Maintains a rolling, UI-bindable log of multiplayer events (joins, leaves, zone changes)
/// derived from SignalRClientService callbacks. Client-only — no shared history on reconnect.
/// </summary>
public class GameLogService : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<GameLogService>();
    private const int MaxEntries = 500;

    private readonly SignalRClientService _signalR;
    private readonly ConcurrentDictionary<string, string> _playerNames = new();
    private readonly ConcurrentDictionary<string, string> _lastZone = new();
    private bool _disposed;

    public ObservableCollection<GameLogEntry> Entries { get; } = new();

    public GameLogService(SignalRClientService signalR)
    {
        _signalR = signalR;
        _signalR.PlayerJoined += OnPlayerJoined;
        _signalR.PlayerLeft += OnPlayerLeft;
        _signalR.PuppetDataReceived += OnPuppetData;
    }

    public void Clear()
    {
        Post(() => Entries.Clear());
    }

    private void OnPlayerJoined(string connectionId, string playerName)
    {
        var name = string.IsNullOrWhiteSpace(playerName) ? ShortId(connectionId) : playerName;
        _playerNames[connectionId] = name;
        Append(new GameLogEntry
        {
            EventType = GameLogEventType.Joined,
            PlayerName = name,
            Message = $"{name} joined the game"
        });
    }

    private void OnPlayerLeft(string connectionId)
    {
        var name = _playerNames.TryGetValue(connectionId, out var n) ? n : ShortId(connectionId);
        _playerNames.TryRemove(connectionId, out _);
        _lastZone.TryRemove(connectionId, out _);
        Append(new GameLogEntry
        {
            EventType = GameLogEventType.Left,
            PlayerName = name,
            Message = $"{name} left the game"
        });
    }

    private void OnPuppetData(PuppetData data)
    {
        if (string.IsNullOrEmpty(data.PlayerId))
            return;

        // Learn/refresh names in case we missed the PlayerJoined event
        if (!string.IsNullOrWhiteSpace(data.PlayerName))
            _playerNames[data.PlayerId] = data.PlayerName;

        var zone = FormatZone(data.StageName, data.RoomNumber);
        if (string.IsNullOrWhiteSpace(zone))
            return;

        var prev = _lastZone.TryGetValue(data.PlayerId, out var p) ? p : null;
        if (prev == zone)
            return;

        _lastZone[data.PlayerId] = zone;

        // Don't log the first zone we ever see for a player as a "change" —
        // treat it as the player entering the world.
        if (prev == null)
            return;

        var name = _playerNames.TryGetValue(data.PlayerId, out var n) ? n : ShortId(data.PlayerId);
        Append(new GameLogEntry
        {
            EventType = GameLogEventType.ZoneChanged,
            PlayerName = name,
            Zone = zone,
            Message = $"{name} moved to {zone}"
        });
    }

    private void Append(GameLogEntry entry)
    {
        Post(() =>
        {
            Entries.Insert(0, entry);
            while (Entries.Count > MaxEntries)
                Entries.RemoveAt(Entries.Count - 1);
        });
    }

    private static void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    private static string FormatZone(string stageName, byte roomNumber)
    {
        if (string.IsNullOrWhiteSpace(stageName))
            return "";
        return $"{stageName} (room {roomNumber})";
    }

    private static string ShortId(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
            return "unknown";
        return connectionId.Length > 8 ? connectionId[..8] : connectionId;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _signalR.PlayerJoined -= OnPlayerJoined;
        _signalR.PlayerLeft -= OnPlayerLeft;
        _signalR.PuppetDataReceived -= OnPuppetData;
    }
}
