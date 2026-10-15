namespace WWOnline.Native;

/// <summary>
/// The multiplayer half of tww.h (API version 2). Game thread only, between frames: reach it through
/// <see cref="INativeGame.Post"/> / <see cref="INativeGame.Invoke{T}"/>.
/// </summary>
public interface ITwwApi
{
    bool GetLocalState(out TwwLocalState state);
    bool GetLocation(out TwwLocation location);
    /// <param name="peer">Null: the slot is inactive (its puppet is deleted).</param>
    void SetPeer(int slot, TwwPeerState? peer);
    /// <summary>dSv_info_c's size: the save window from the GameInfo base.</summary>
    uint SaveSize();
    /// <returns>Bytes copied (0 past the end).</returns>
    uint SaveRead(uint offset, Span<byte> destination);
    /// <returns>Bytes copied (0 past the end).</returns>
    uint SaveWrite(uint offset, ReadOnlySpan<byte> source);
}

/// <summary>What the game thread published after its last frame.</summary>
/// <param name="InStage">tww_get_location's answer: in a stage.</param>
public sealed record NativeSnapshot(uint Frame, TwwLocalState Local, TwwLocation Location, bool InStage)
{
    /// <summary>When the frame's state was sampled (<see cref="GameHost"/>'s frame clock): the local puppet's send time.</summary>
    public DateTime Time { get; init; } = DateTime.UtcNow;
}

/// <summary>The embedded game as the sync services see it (<see cref="GameHost"/>; a fake in tests).</summary>
public interface INativeGame
{
    /// <summary>tww_init succeeded and frames are running.</summary>
    bool IsRunning { get; }

    /// <summary><see cref="IsRunning"/> changed. Any thread.</summary>
    event Action<bool>? RunningChanged;

    /// <summary>The state after the last frame, or null before the first.</summary>
    NativeSnapshot? Snapshot { get; }

    /// <summary>Run <paramref name="action"/> on the game thread before the next frame (fire and forget).</summary>
    void Post(Action<ITwwApi> action);

    /// <summary>Run <paramref name="func"/> on the game thread before the next frame and wait for its result.</summary>
    /// <exception cref="InvalidOperationException">The game isn't running (or stopped before it ran).</exception>
    /// <exception cref="TimeoutException">The game thread didn't get to it in time.</exception>
    T Invoke<T>(Func<ITwwApi, T> func);
}
