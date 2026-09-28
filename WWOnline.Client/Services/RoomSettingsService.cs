using Serilog;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The room's rules (shared wallet / world / items / story) as last pushed by the server, plus whether
/// this client is the room owner. Sync services check <see cref="Current"/> every tick; the
/// dashboard shows it and lets the room owner change it.
/// </summary>
public class RoomSettingsService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RoomSettingsService>();
    private readonly SignalRClientService _signalR;

    public RoomSettings Current { get; private set; } = new();

    public bool IsOwner =>
        !string.IsNullOrEmpty(Current.OwnerConnectionId) &&
        Current.OwnerConnectionId == _signalR.Connection?.ConnectionId;

    /// <summary>Raised on a background thread whenever the rules or room owner change.</summary>
    public event Action<RoomSettings>? Changed;

    public RoomSettingsService(SignalRClientService signalR)
    {
        _signalR = signalR;
        _signalR.RoomSettingsReceived += Apply;
        _signalR.Connected += OnConnected;
    }

    /// <summary>Pull the rules (and owner) after every connect / reconnect rather than rely on a push.</summary>
    private void OnConnected() => _ = Task.Run(async () =>
    {
        try
        {
            var settings = await _signalR.GetRoomSettingsAsync();
            if (settings != null) Apply(settings);
        }
        catch (Exception ex) { Logger.Warning(ex, "[room] failed to fetch room settings"); }
    });

    public Task SetAsync(bool sharedWallet, bool sharedWorld, bool sharedItems, bool sharedStory)
    {
        var requested = Current.Clone();
        requested.SharedWallet = sharedWallet;
        requested.SharedWorld = sharedWorld;
        requested.SharedItems = sharedItems;
        requested.SharedStory = sharedStory;
        return _signalR.SetRoomSettingsAsync(requested);
    }

    /// <summary>Room owner: set every rule at once from a preset (Full sync = all on, Co-op = all off).</summary>
    public Task SetPresetAsync(RoomPreset preset)
    {
        var requested = Current.Clone().ApplyPreset(preset);
        return SetAsync(requested.SharedWallet, requested.SharedWorld, requested.SharedItems, requested.SharedStory);
    }

    private void Apply(RoomSettings settings)
    {
        var old = Current;
        Current = settings;
        if (old.SharedWallet != settings.SharedWallet || old.SharedWorld != settings.SharedWorld ||
            old.SharedItems != settings.SharedItems || old.SharedStory != settings.SharedStory ||
            old.OwnerConnectionId != settings.OwnerConnectionId)
        {
            Logger.Information("[room] rules: {Rules}; owner {Owner}{Me}",
                settings.RulesSummary(), settings.OwnerName, IsOwner ? " (me)" : "");
        }
        Changed?.Invoke(settings);
    }

    public void Dispose()
    {
        _signalR.RoomSettingsReceived -= Apply;
        _signalR.Connected -= OnConnected;
    }
}
