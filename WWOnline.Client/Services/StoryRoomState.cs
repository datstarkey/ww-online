using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// What the Room story flags page needs to know about the room: connected, the Shared story rule,
/// whether this client owns the room, and the room's flags. An interface so the page's view-model
/// can be tested without a hub connection.
/// </summary>
public interface IStoryRoom
{
    bool IsConnected { get; }

    /// <summary>The room's Shared story rule (meaningless while offline).</summary>
    bool SharedStory { get; }

    bool IsOwner { get; }

    /// <summary>The room's story flags as this client knows them, or null (not joined / rule off).</summary>
    StoryFlags? RoomFlags { get; }

    /// <summary>Raised (any thread) when the connection, the rules or the room owner change.</summary>
    event Action? Changed;
}

/// <summary>The live <see cref="IStoryRoom"/>: the room rules, the hub connection and the story sync's room copy.</summary>
public sealed class StoryRoomState : IStoryRoom, IDisposable
{
    private readonly RoomSettingsService _room;
    private readonly SignalRClientService _signalR;
    private readonly StorySyncService _story;

    public StoryRoomState(RoomSettingsService room, SignalRClientService signalR, StorySyncService story)
    {
        _room = room;
        _signalR = signalR;
        _story = story;
        _room.Changed += OnRulesChanged;
        _signalR.Connected += OnConnectionChanged;
        _signalR.ConnectionLost += OnConnectionChanged;
    }

    public bool IsConnected => _signalR.IsConnected;
    public bool SharedStory => _room.Current.SharedStory;
    public bool IsOwner => _room.IsOwner;
    public StoryFlags? RoomFlags => _story.RoomFlags;

    public event Action? Changed;

    private void OnRulesChanged(RoomSettings _) => Changed?.Invoke();
    private void OnConnectionChanged() => Changed?.Invoke();

    public void Dispose()
    {
        _room.Changed -= OnRulesChanged;
        _signalR.Connected -= OnConnectionChanged;
        _signalR.ConnectionLost -= OnConnectionChanged;
    }
}
