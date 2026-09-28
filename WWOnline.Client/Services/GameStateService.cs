using System.Collections.Concurrent;
using Serilog;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

public class GameStateService : IGameStateService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<GameStateService>();
    private readonly ConcurrentDictionary<string, GameState> _playerStates = new();
    private GameState? _currentState;
    private readonly object _stateLock = new();
    
    public GameState? CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState?.Clone();
            }
        }
    }
    
    public Dictionary<string, GameState> PlayerStates => new(_playerStates);
    
    public event EventHandler<GameStateChangedEventArgs>? StateChanged;
    public event EventHandler<PlayerStateReceivedEventArgs>? PlayerStateReceived;
    
    public void UpdateLocalState(GameState newState)
    {
        if (newState == null)
        {
            Logger.Warning("Attempted to update local state with null value");
            return;
        }

        GameState? oldState;

        lock (_stateLock)
        {
            // Fast-path: if checksum hasn't changed, skip deep comparison
            if (_currentState != null && _currentState.Checksum == newState.Checksum)
            {
                _currentState.Timestamp = DateTime.UtcNow;
                return;
            }

            oldState = _currentState;
            _currentState = newState.Clone();
            _currentState.Timestamp = DateTime.UtcNow;
        }

        var changes = DetectChanges(oldState, newState);

        if (changes.Count > 0)
        {
            // Only log significant changes
            if (changes.Contains("Health") || changes.Contains("Location") || changes.Contains("Initial state") || changes.Contains("Position"))
            {
                Logger.Information("Game state updated: {Changes}", string.Join(", ", changes));
            }

            StateChanged?.Invoke(this, new GameStateChangedEventArgs(oldState!, newState, changes));
        }
    }
    
    public void ProcessReceivedState(string playerId, GameState receivedState)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            Logger.Warning("Received state with empty player ID");
            return;
        }

        if (receivedState == null)
        {
            Logger.Warning("Received null state for player {PlayerId}", playerId);
            return;
        }

        // Validate checksum
        if (!receivedState.ValidateChecksum())
        {
            Logger.Warning("Received state from {PlayerId} failed checksum validation", playerId);
            return;
        }

        bool isNewPlayer = !_playerStates.ContainsKey(playerId);
        _playerStates[playerId] = receivedState.Clone();

        Logger.Debug("Processed game state from {PlayerId} (New: {IsNew})", playerId, isNewPlayer);
        LogStateDetails(receivedState);

        PlayerStateReceived?.Invoke(this, new PlayerStateReceivedEventArgs(playerId, receivedState, isNewPlayer));
    }
    
    public GameState? GetPlayerState(string playerId)
    {
        if (_playerStates.TryGetValue(playerId, out var state))
        {
            return state.Clone();
        }
        return null;
    }
    
    public void ClearAllStates()
    {
        _playerStates.Clear();
        Logger.Information("Cleared all player states");
    }
    
    public void RemovePlayerState(string playerId)
    {
        if (_playerStates.TryRemove(playerId, out _))
        {
            Logger.Information("Removed state for player {PlayerId}", playerId);
        }
    }
    
    private List<string> DetectChanges(GameState? oldState, GameState newState)
    {
        var changes = new List<string>();
        
        if (oldState == null)
        {
            changes.Add("Initial state");
            return changes;
        }
        
        // Check player state changes
        if (oldState.Player.CurrentHealth != newState.Player.CurrentHealth)
            changes.Add("Health");
        if (oldState.Player.RupeeCount != newState.Player.RupeeCount)
            changes.Add("Rupees");
        if (oldState.Player.CurrentSector != newState.Player.CurrentSector)
            changes.Add("Location");
        if (oldState.Player.CurrentSword != newState.Player.CurrentSword)
            changes.Add("Sword");
        if (oldState.Player.CurrentShield != newState.Player.CurrentShield)
            changes.Add("Shield");
            
        // Check position changes (with small threshold to avoid spam)
        const float positionThreshold = 1.0f; // Only detect if moved more than 1 unit
        if (Math.Abs(oldState.Player.PositionX - newState.Player.PositionX) > positionThreshold ||
            Math.Abs(oldState.Player.PositionY - newState.Player.PositionY) > positionThreshold ||
            Math.Abs(oldState.Player.PositionZ - newState.Player.PositionZ) > positionThreshold)
        {
            changes.Add("Position");
        }
        
        // Check inventory changes
        for (int i = 0; i < oldState.Inventory.Items.Length; i++)
        {
            if (oldState.Inventory.Items[i] != newState.Inventory.Items[i])
            {
                changes.Add($"Inventory[{i}]");
            }
        }
        
        // Check collection changes
        if (oldState.Inventory.TriforceShards != newState.Inventory.TriforceShards)
            changes.Add("TriforceShards");
        if (oldState.Inventory.PearlsBitfield != newState.Inventory.PearlsBitfield)
            changes.Add("Pearls");
        if (oldState.Inventory.SongsBitfield != newState.Inventory.SongsBitfield)
            changes.Add("Songs");
        
        return changes;
    }
    
    private void LogStateDetails(GameState state)
    {
        Logger.Debug("State details - Hearts: {Hearts}/{MaxHearts}, Rupees: {Rupees}, Sector: {Sector}",
            state.Player.CurrentHealth,
            state.Player.MaxHealth,
            state.Player.RupeeCount,
            state.Player.CurrentSector);
    }
}