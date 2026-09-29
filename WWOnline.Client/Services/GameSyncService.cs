using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using WWOnline.Hubs;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Orchestrates the full game state sync pipeline between local services and the SignalR server.
/// Subscribes to inbound SignalR events and routes them to PuppetSyncService/GameStateService.
/// Broadcasts local state changes and puppet data outbound to the server.
/// </summary>
public class GameSyncService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GameSyncService>();

    private readonly SignalRClientService _signalR;
    private readonly PuppetSyncService _puppetSync;
    private readonly IGameStateService _gameState;
    private readonly RoomInventorySyncService _roomInventorySync;
    private readonly WorldFlagSyncService _worldFlagSync;
    private readonly SharedWalletService _sharedWallet;
    private readonly SharedBaitService _sharedBait;
    private readonly SharedSpoilsService _sharedSpoils;
    private readonly SharedDeliveryService _sharedDelivery;
    private readonly StorySyncService _storySync;
    private readonly SharedSmallKeyService _smallKeys;
    private readonly SharedHeartService _hearts;
    private readonly RoomSwitchSyncService _roomSwitchSync;
    private readonly PlayerEventService _playerEvents;

    private System.Timers.Timer? _puppetBroadcastTimer;
    private System.Timers.Timer? _gameStateBroadcastTimer;
    private const int GameStateBroadcastIntervalMs = 500; // 2Hz, unconditional
    private int _isBroadcasting;
    private int _isBroadcastingGameState;
    private bool _running;
    private bool _disposed;

    public bool IsRunning => _running;

    public GameSyncService(
        SignalRClientService signalR,
        PuppetSyncService puppetSync,
        IGameStateService gameState,
        RoomInventorySyncService roomInventorySync,
        WorldFlagSyncService worldFlagSync,
        SharedWalletService sharedWallet,
        SharedBaitService sharedBait,
        SharedSpoilsService sharedSpoils,
        SharedDeliveryService sharedDelivery,
        StorySyncService storySync,
        SharedSmallKeyService smallKeys,
        SharedHeartService hearts,
        RoomSwitchSyncService roomSwitchSync,
        PlayerEventService playerEvents)
    {
        _signalR = signalR;
        _puppetSync = puppetSync;
        _gameState = gameState;
        _roomInventorySync = roomInventorySync;
        _worldFlagSync = worldFlagSync;
        _sharedWallet = sharedWallet;
        _sharedBait = sharedBait;
        _sharedSpoils = sharedSpoils;
        _sharedDelivery = sharedDelivery;
        _storySync = storySync;
        _smallKeys = smallKeys;
        _hearts = hearts;
        _roomSwitchSync = roomSwitchSync;
        _playerEvents = playerEvents;
    }

    /// <summary>
    /// Start the sync pipeline. Subscribes to all events and begins broadcasting.
    /// Call after Dolphin is connected and memory monitor is running.
    /// </summary>
    public void Start()
    {
        if (_running)
        {
            Logger.Warning("GameSyncService already running");
            return;
        }

        Logger.Information("Starting GameSyncService");

        // Subscribe to inbound SignalR events
        _signalR.PuppetDataReceived += OnPuppetDataReceived;
        _signalR.PlayerGameStateReceived += OnPlayerGameStateReceived;
        _signalR.PlayerJoined += OnPlayerJoined;
        _signalR.PlayerLeft += OnPlayerLeft;
        _signalR.ConnectionLost += OnConnectionLost;

        // Players who joined before we started (we start when Dolphin attaches, usually after the
        // room's PlayerJoined events): take their names from the roster for the tags over puppets.
        foreach (var (id, name) in _signalR.Players)
            _puppetSync.SetPlayerName(id, name);

        // Start puppet sync (writes remote puppet data to Dolphin memory at 20Hz)
        _puppetSync.Start();

        // Shared world: chests / switches / collected items OR-merged across players
        _worldFlagSync.Start();
        // ...and the live dungeon / room switches of players in the same dungeon or room
        _roomSwitchSync.Start();
        _sharedWallet.Start();
        // Shared bait bag: All-Purpose Bait / Hyoi Pear counts, one room total
        _sharedBait.Start();
        // Shared spoils bag: one count per spoil type, one room total
        _sharedSpoils.Start();
        // Shared delivery bag: the quest items (trade goods, letters...), one room bag
        _sharedDelivery.Start();
        // Shared items: the room's inventory / equipment / upgrades, owned by the server
        _roomInventorySync.Start();
        // Shared story: event flags (story, cutscenes, side quests, NPC state) OR-merged across players
        _storySync.Start();
        // Shared small keys: each dungeon's count derived from the shared world's flags
        _smallKeys.Start();
        // Derived max health: containers + pieces whose flag the shared world / story holds
        _hearts.Start();
        // Other players' projectiles: the REL's events block <-> the hub (bombs, cannon)
        _playerEvents.Start();

        // Start puppet broadcast timer (reads local player and sends to server at 20Hz)
        _puppetBroadcastTimer = new System.Timers.Timer(50); // 20Hz — fast combo swings (~0.3s) need the extra samples
        _puppetBroadcastTimer.Elapsed += (_, _) =>
        {
            if (Interlocked.CompareExchange(ref _isBroadcasting, 1, 0) != 0) return;
            _ = Task.Run(async () =>
            {
                try { await BroadcastLocalPuppetData(); }
                catch (Exception ex) { Logger.Error(ex, "Puppet broadcast failed"); }
                finally { Interlocked.Exchange(ref _isBroadcasting, 0); }
            });
        };
        _puppetBroadcastTimer.AutoReset = true;
        _puppetBroadcastTimer.Start();

        // Broadcast game state unconditionally at 2Hz so peers see presence + stats even when values aren't changing.
        _gameStateBroadcastTimer = new System.Timers.Timer(GameStateBroadcastIntervalMs);
        _gameStateBroadcastTimer.Elapsed += (_, _) =>
        {
            if (Interlocked.CompareExchange(ref _isBroadcastingGameState, 1, 0) != 0) return;
            _ = Task.Run(async () =>
            {
                try { await BroadcastLocalGameState(); }
                catch (Exception ex) { Logger.Error(ex, "GameState broadcast failed"); }
                finally { Interlocked.Exchange(ref _isBroadcastingGameState, 0); }
            });
        };
        _gameStateBroadcastTimer.AutoReset = true;
        _gameStateBroadcastTimer.Start();

        _running = true;
        Logger.Information("GameSyncService started — inbound routing + outbound broadcast active");
    }

    /// <summary>
    /// Stop the sync pipeline. Unsubscribes from all events, stops timers, clears remote state.
    /// </summary>
    public void Stop()
    {
        if (!_running)
            return;

        Logger.Information("Stopping GameSyncService");

        // Unsubscribe from inbound SignalR events
        _signalR.PuppetDataReceived -= OnPuppetDataReceived;
        _signalR.PlayerGameStateReceived -= OnPlayerGameStateReceived;
        _signalR.PlayerJoined -= OnPlayerJoined;
        _signalR.PlayerLeft -= OnPlayerLeft;
        _signalR.ConnectionLost -= OnConnectionLost;

        // Stop puppet broadcast timer
        _puppetBroadcastTimer?.Stop();
        _puppetBroadcastTimer?.Dispose();
        _puppetBroadcastTimer = null;

        // Stop game state broadcast timer
        _gameStateBroadcastTimer?.Stop();
        _gameStateBroadcastTimer?.Dispose();
        _gameStateBroadcastTimer = null;

        _worldFlagSync.Stop();
        _roomSwitchSync.Stop();
        _sharedWallet.Stop();
        _sharedBait.Stop();
        _sharedSpoils.Stop();
        _sharedDelivery.Stop();
        _roomInventorySync.Stop();
        _storySync.Stop();
        _smallKeys.Stop();
        _hearts.Stop();
        _playerEvents.Stop();

        // Stop puppet sync (clears shared memory and slot assignments)
        _puppetSync.Stop();

        // Clear remote player states
        _gameState.ClearAllStates();

        _running = false;
        Logger.Information("GameSyncService stopped");
    }

    // === Inbound handlers (Server → Local) ===

    private void OnPuppetDataReceived(PuppetData data)
    {
        if (string.IsNullOrEmpty(data.PlayerId))
            return;

        _puppetSync.UpdateRemotePuppet(data.PlayerId, data);
    }

    private void OnPlayerGameStateReceived(string senderId, GameState state)
    {
        _gameState.ProcessReceivedState(senderId, state);
    }

    private void OnPlayerJoined(string connectionId, string playerName)
    {
        // Don't allocate a puppet slot for ourselves — the server broadcasts PlayerJoined
        // to Clients.All (including self), but we don't render our own puppet.
        // Slots are a shared, ordered resource the in-game C code consumes sequentially
        // (0..desiredCount-1), so a self-slot at 0 makes real peers at slot 1+ invisible.
        if (connectionId == _signalR.Connection?.ConnectionId)
            return;

        _puppetSync.SetPlayerName(connectionId, playerName); // shown above their puppet
        int slot = _puppetSync.AssignSlot(connectionId);
        if (slot >= 0)
            Logger.Information("Player {Name} ({Id}) assigned to puppet slot {Slot}", playerName, connectionId, slot);
        else
            Logger.Warning("No puppet slot available for {Name} ({Id})", playerName, connectionId);
    }

    private void OnPlayerLeft(string connectionId)
    {
        _puppetSync.ReleaseSlot(connectionId);
        _gameState.RemovePlayerState(connectionId);
        Logger.Information("Player {Id} removed from sync", connectionId);
    }

    // Puppets are wiped when the connection drops; after a (re)connect SignalRClientService
    // re-announces the players already in the room, which re-assigns their slots.
    private void OnConnectionLost()
    {
        Logger.Information("Connection lost — wiping puppet state");
        _puppetSync.OnConnectionLost();
        _gameState.ClearAllStates();
    }

    // === Outbound handlers (Local → Server) ===

    private async Task BroadcastLocalGameState()
    {
        var connection = _signalR.Connection;
        if (connection?.State != HubConnectionState.Connected)
            return;

        var state = _gameState.CurrentState;
        if (state == null)
            return;

        // Checksum is recalculated every broadcast because CurrentState.Clone() copies the
        // cached value but we want the server-side ValidateChecksum to pass against fresh data.
        state.Checksum = state.CalculateChecksum();

        try
        {
            await connection.InvokeAsync(HubConstants.SendPlayerGameState, state);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to send game state");
        }
    }

    private async Task BroadcastLocalPuppetData()
    {
        var connection = _signalR.Connection;
        if (connection?.State != HubConnectionState.Connected)
            return;

        var data = _puppetSync.ReadLocalPlayerState();
        if (data == null)
            return;

        try
        {
            await connection.InvokeAsync(HubConstants.SendPuppetData, data);
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to send puppet data");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }
}
