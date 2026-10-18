namespace WWOnline.Services;

/// <summary>
/// The player-facing words for the game, which depend on the backend chosen for this run
/// (<see cref="App.AddGameBackend"/>): Dolphin, or the built-in game. One place, so no native page talks about Dolphin.
/// </summary>
public static class GameText
{
    /// <summary>The built-in game runs instead of Dolphin. Set once at startup.</summary>
    public static bool IsNative { get; set; }

    /// <summary>The sidebar's name for the game page.</summary>
    public static string PageName => IsNative ? "Game" : "Dolphin";

    /// <summary>The sidebar player card's status line.</summary>
    public static string LinkText(bool connected) => IsNative
        ? connected ? "Game running" : "Game not started"
        : connected ? "Dolphin linked" : "Dolphin not linked";

    /// <summary>A command needs the game, and it isn't running.</summary>
    public static string NotConnected => IsNative ? "The game isn't running" : "Dolphin not connected";

    /// <summary>How to get the game going, as the start of a sentence ("… to see your items.").</summary>
    public static string StartHint => IsNative ? "Host or join a room to start the game" : "Attach to Dolphin on the Dolphin page";
}
