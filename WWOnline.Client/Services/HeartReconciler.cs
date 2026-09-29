using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>A max-life change to queue for the HUD (<see cref="HeartMemory.ApplyDelta"/>).</summary>
public readonly record struct HeartWrite(int Delta, int From, int To);

/// <summary>One [hearts] log line; <see cref="Warn"/> for the loud ones (a heart no flag explains).</summary>
public readonly record struct HeartLogLine(bool Warn, string Text);

/// <summary>
/// Derived max health: <c>12 + 4 × Heart Containers taken + Pieces of Heart taken</c>, each source counted once when
/// its flag is set in this game or its ID is in the room's grow-only heart-source set (<see cref="HeartTable.Tally"/>),
/// so the players of a room derive the same value and a piece two players both pick up (its flag not synced, or a
/// reward that isn't flag-gated for the second player) is counted once.
/// Every tick this sets mMaxLife to the derived value, only while <see cref="HeartObservation.Idle"/> has held for a
/// while: a pickup adds its max life during its get-item demo and some rewards set their flag at the end of it, so
/// the game always finishes its own change first. A rise (a synced piece) waits <see cref="IdleTicksUp"/> ticks, a
/// fall (a piece counted twice, or a save with too many hearts) <see cref="IdleTicksDown"/>.
/// A rise of the game's own max that no heart source explains (no source added, here or in the room, in the last
/// <see cref="RecentFlagTicks"/> ticks) is logged loudly with the stage and room: either the same piece given twice
/// or a source the catalogue doesn't know. It is not kept: the next write sets the derived value.
/// Not thread-safe: one caller (the service's tick).
/// </summary>
public sealed class HeartReconciler
{
    /// <summary>Ticks the game must stay idle before a rise is written (0.5 s at 4 Hz).</summary>
    public const int IdleTicksUp = 2;

    /// <summary>Ticks the game must stay idle before a fall is written (2 s): a reward that sets its flag just after
    /// its demo (a letter, the withered trees' heart) gets time to.</summary>
    public const int IdleTicksDown = 8;

    /// <summary>A rise of the game's max within this many ticks (10 s) of a source's flag being set is that source.</summary>
    public const int RecentFlagTicks = 40;

    /// <summary>Ticks (2 s) to wait for a write to show before taking the game's max as it is again.</summary>
    public const int WriteLandTicks = 8;

    private int? _prevGame;
    private int? _prevDerived;
    private long _lastFlagRise = long.MinValue / 2;
    private int? _awaiting;
    private long _awaitUntil;
    private int _idleTicks;
    private bool _reported;

    /// <summary>Forget what was seen (the rule went off, or the game detached): the next step reports the save again.</summary>
    public void Reset()
    {
        _prevGame = null;
        _prevDerived = null;
        _awaiting = null;
        _idleTicks = 0;
        _reported = false;
    }

    /// <summary>The scene isn't settled (a stage change, no Link): the game isn't idle.</summary>
    public void Unsettled() => _idleTicks = 0;

    /// <summary>One tick: the write (if any) that brings mMaxLife to the derived value. Log lines go to <paramref name="log"/>.</summary>
    public HeartWrite? Step(HeartTable table, HeartObservation obs, ulong roomSources, long tick, List<HeartLogLine> log)
    {
        _idleTicks = obs.Idle ? _idleTicks + 1 : 0;
        var tally = table.Tally(obs.Flags, roomSources);
        int derived = tally.MaxLife;
        int game = obs.MaxLife;
        if (_prevDerived is int pd && derived > pd) _lastFlagRise = tick;

        // Our last write still landing (queued for the HUD): not the game's own change.
        bool ours = _awaiting.HasValue;
        if (ours && (game == _awaiting || tick > _awaitUntil)) _awaiting = null;

        if (!_reported)
        {
            _reported = true;
            log.Add(new(false, $"this save has max health {game} ({Hearts(game)}); its flags and the room's heart sources derive {derived} ({Describe(table, tally)})" +
                               (game == derived ? "" : $" — {(game > derived ? "correcting it down" : "raising it")} to {derived} once the game is idle")));
        }
        else if (!ours && _prevGame is int pg && game > pg && game > derived && tick - _lastFlagRise > RecentFlagTicks)
        {
            log.Add(new(true, $"max health rose {pg} → {game} in {Where(obs)} with no heart source added in the last {RecentFlagTicks / 4} s: " +
                              $"a Piece of Heart / Heart Container given twice, or a source the heart catalogue doesn't know — " +
                              $"not kept, max health goes back to the derived {derived} ({Describe(table, tally)})"));
        }
        _prevGame = game;
        _prevDerived = derived;

        if (game == derived || ours) return null;
        if (_idleTicks < (derived > game ? IdleTicksUp : IdleTicksDown)) return null;

        _idleTicks = 0; // one queued change at a time: the next ticks see it land first
        _awaiting = derived;
        _awaitUntil = tick + WriteLandTicks;
        log.Add(new(false, $"max health {game} → {derived} ({Hearts(game)} → {Hearts(derived)}; {Describe(table, tally)}) in {Where(obs)}" +
                           (derived < game ? ", current health clamped by the game" : "")));
        return new HeartWrite(derived - game, game, derived);
    }

    /// <summary>"2/6 containers, 13/44 pieces".</summary>
    public static string Describe(HeartTable table, HeartTally tally) =>
        $"{tally.Containers}/{table.ContainersTotal} containers, {tally.Pieces}/{table.PiecesTotal} pieces";

    private static string Hearts(int quarters) => quarters % 4 == 0 ? $"{quarters / 4} hearts" : $"{quarters / 4} {quarters % 4}/4 hearts";

    private static string Where(HeartObservation obs) =>
        obs.Stage.Length == 0 ? "an unknown stage" : obs.Room >= 0 ? $"stage {obs.Stage} room {obs.Room}" : $"stage {obs.Stage}";
}
