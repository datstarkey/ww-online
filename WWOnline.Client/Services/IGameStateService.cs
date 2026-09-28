using WWOnline.Shared.Models;

namespace WWOnline.Services;

public interface IGameStateService
{
    /// <summary>
    /// Current local game state
    /// </summary>
    GameState? CurrentState { get; }
    
    /// <summary>
    /// Dictionary of all connected players' states
    /// </summary>
    Dictionary<string, GameState> PlayerStates { get; }
    
    /// <summary>
    /// Event fired when local game state changes
    /// </summary>
    event EventHandler<GameStateChangedEventArgs>? StateChanged;
    
    /// <summary>
    /// Event fired when another player's state is received
    /// </summary>
    event EventHandler<PlayerStateReceivedEventArgs>? PlayerStateReceived;
    
    /// <summary>
    /// Update the local game state
    /// </summary>
    void UpdateLocalState(GameState newState);

    /// <summary>
    /// Process received state from another player
    /// </summary>
    void ProcessReceivedState(string playerId, GameState receivedState);
    
    /// <summary>
    /// Get a specific player's state
    /// </summary>
    GameState? GetPlayerState(string playerId);
    
    /// <summary>
    /// Clear all player states
    /// </summary>
    void ClearAllStates();
    
    /// <summary>
    /// Remove a specific player's state
    /// </summary>
    void RemovePlayerState(string playerId);
}

public class GameStateChangedEventArgs : EventArgs
{
    public GameState OldState { get; }
    public GameState NewState { get; }
    public List<string> ChangedProperties { get; }
    
    public GameStateChangedEventArgs(GameState oldState, GameState newState, List<string> changedProperties)
    {
        OldState = oldState;
        NewState = newState;
        ChangedProperties = changedProperties;
    }
}

public class PlayerStateReceivedEventArgs : EventArgs
{
    public string PlayerId { get; }
    public GameState State { get; }
    public bool IsNewPlayer { get; }
    
    public PlayerStateReceivedEventArgs(string playerId, GameState state, bool isNewPlayer)
    {
        PlayerId = playerId;
        State = state;
        IsNewPlayer = isNewPlayer;
    }
}