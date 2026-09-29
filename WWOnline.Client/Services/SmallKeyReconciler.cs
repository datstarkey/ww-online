using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>One stage slot's small-key state in the game: its memBit flags and mKeyNum.</summary>
public readonly record struct SlotKeyState(uint Tbox, uint Item, uint[] Switch, int KeyNum, byte DungeonItem)
{
    public static SlotKeyState Empty { get; } = new(0, 0, new uint[4], 0, 0);
}

/// <summary>What the game looks like this tick, for <see cref="SmallKeyReconciler"/>.</summary>
/// <param name="Slots">All 16 stage slots: the current one from the live copy, the others from the save.</param>
/// <param name="CurrentSlot">The current stage's save slot, or null when unknown.</param>
/// <param name="KeyHud">The current stage shows the small-key HUD (dStage_stagInfo_ChkKeyDisp).</param>
/// <param name="Idle">Nothing is about to change the current count on its own: no key change queued for the HUD,
/// no event (a key pickup's get-item demo, a door's unlock), no pause menu.</param>
/// <param name="Stage">Stage name and room, for the logs.</param>
public sealed record SmallKeyObservation(SlotKeyState[] Slots, int? CurrentSlot, bool KeyHud, bool Idle, string Stage = "", int Room = -1);

/// <summary>How a count reaches the game.</summary>
public enum KeyWriteKind
{
    /// <summary>Current stage with the key HUD: queue the difference in play.mItemKeyNumCount, so the HUD animates it.</summary>
    Pending,
    /// <summary>Current stage without the key HUD: write the live mKeyNum (a pending change would linger there).</summary>
    Live,
    /// <summary>Another stage slot: write its saved mKeyNum (the live copy is loaded from it on entry).</summary>
    Saved,
}

/// <summary>A write the service makes: <see cref="Value"/> is the delta for Pending, the count otherwise.</summary>
public readonly record struct KeyWrite(int Slot, KeyWriteKind Kind, int Value, int From, int To);

/// <summary>
/// Shared small keys by derivation, not by counting: in a dungeon, the keys a player holds are the key
/// sources whose flag is set minus the key doors whose switch is set (<see cref="SmallKeyDungeon.Derive"/>).
/// Those flags are world flags the shared world already OR-merges, so every player derives the same
/// count; nothing is counted twice, a race or a reconnect can't skew it, and a key spent on a door that
/// was already open (its lock not yet re-created by live world) is handed back on the next tick.
/// Every tick this sets each dungeon's mKeyNum to the derived count:
/// <list type="bullet">
/// <item>The current dungeon only while <see cref="SmallKeyObservation.Idle"/> has held for
/// <see cref="IdleTicksRequired"/> ticks: a pickup sets its flag at once but adds its key at the end of
/// its get-item demo, and a door queues its -1 in its unlock event, so the game finishes its own change first.</item>
/// <item>Other dungeons at once, through their saved copy.</item>
/// </list>
/// A key from a source the table doesn't know (a randomizer, modded stage data) raises the game's count
/// past the derived one with no flag behind it. That key is kept for this player (<c>surplus</c>, added to
/// the derived count for the rest of the session) and logged with the stage and room so the table can
/// learn the source; other players don't get it. A derived count below zero is clamped and logged.
/// Not thread-safe: one caller (the service's tick).
/// </summary>
public sealed class SmallKeyReconciler
{
    /// <summary>The HUD shows two digits and dMeter_keyMove clamps to 0..99 (d_meter.cpp:5228-5232).</summary>
    public const int MaxKeys = 99;

    /// <summary>Ticks the current stage must stay idle before its count is written (0.5 s at 4 Hz).</summary>
    public const int IdleTicksRequired = 2;

    /// <summary>A game count rise within this many ticks (10 s) of a key flag being set is that pickup's key.</summary>
    public const int RecentPickupTicks = 40;

    /// <summary>Ticks (2 s) to wait for a write to show before taking the game's count as it is again.</summary>
    public const int WriteLandTicks = 8;

    private sealed class SlotTrack
    {
        public int? PrevGame;
        public int? PrevRaw;
        public long LastRiseTick = long.MinValue / 2;
        public int Surplus;
        public int? LoggedNegative;
        /// <summary>A count we wrote that hasn't shown yet: until it does (or times out), a change isn't the game's own.</summary>
        public int? Awaiting;
        public long AwaitUntil;
    }

    private readonly Dictionary<int, SlotTrack> _slots = new();
    private int _idleTicks;

    /// <summary>Forget what was seen (the rule went off, or the game detached). Keys kept from unknown sources stay.</summary>
    public void Reset()
    {
        foreach (var t in _slots.Values)
        {
            t.PrevGame = null;
            t.PrevRaw = null;
            t.LoggedNegative = null;
            t.Awaiting = null;
        }
        _idleTicks = 0;
    }

    /// <summary>Keys this player holds from sources the table doesn't know (per slot).</summary>
    public int Surplus(int slot) => _slots.TryGetValue(slot, out var t) ? t.Surplus : 0;

    /// <summary>The count a dungeon should hold: derived from its flags plus any unknown-source keys, clamped to 0..99.</summary>
    public int Target(SmallKeyDungeon dungeon, SlotKeyState s) =>
        Math.Clamp(dungeon.Derive(s.Tbox, s.Item, s.Switch) + Surplus(dungeon.Slot), 0, MaxKeys);

    /// <summary>
    /// One tick: the writes that bring every key dungeon's count to its derived value. Log lines for
    /// anything worth a line go to <paramref name="log"/>.
    /// </summary>
    public List<KeyWrite> Step(SmallKeyTable table, SmallKeyObservation obs, long tick, List<string> log)
    {
        _idleTicks = obs.Idle ? _idleTicks + 1 : 0;
        var writes = new List<KeyWrite>();
        foreach (var dungeon in table.Dungeons.Values)
        {
            // A dungeon is a save slot with key doors; never touch the count anywhere else.
            if (dungeon.Doors.Count == 0 || dungeon.Slot < 0 || dungeon.Slot >= obs.Slots.Length) continue;
            if (!_slots.TryGetValue(dungeon.Slot, out var t)) _slots[dungeon.Slot] = t = new SlotTrack();

            var s = obs.Slots[dungeon.Slot];
            bool current = dungeon.Slot == obs.CurrentSlot;
            int raw = dungeon.Derive(s.Tbox, s.Item, s.Switch);
            int game = s.KeyNum;
            if (t.PrevRaw is int pr && raw > pr) t.LastRiseTick = tick;

            // Our last write still landing (a queued HUD change, or one the game overwrote): not the game's own change.
            bool ours = t.Awaiting.HasValue;
            if (ours && (game == t.Awaiting || tick > t.AwaitUntil)) t.Awaiting = null;

            // The game gained a key no flag explains: a source the table doesn't know. Keep it for this player.
            if (current && !ours && t.PrevGame is int pg && game > pg)
            {
                int before = Math.Clamp(raw + t.Surplus, 0, MaxKeys);
                if (game > before && tick - t.LastRiseTick > RecentPickupTicks)
                {
                    int extra = Math.Min(game - pg, game - before);
                    t.Surplus += extra;
                    log.Add($"slot {dungeon.Slot}: the game gained {extra} small key(s) that no known key flag explains, " +
                            $"in {Where(obs)} — keeping {(extra == 1 ? "it" : "them")} for this player (unknown key source, add it to the key table)");
                }
            }

            int unclamped = raw + t.Surplus;
            int target = Math.Clamp(unclamped, 0, MaxKeys);
            if (unclamped < 0 && t.LoggedNegative != unclamped)
                log.Add($"slot {dungeon.Slot}: {Describe(dungeon, s)} derives {unclamped} small key(s) — clamped to 0 " +
                        "(more key doors open than keys taken: another save's flags, or a key source the table doesn't know)");
            t.LoggedNegative = unclamped < 0 ? unclamped : null;
            t.PrevRaw = raw;
            t.PrevGame = game;

            if (game == target) continue;
            KeyWrite write;
            if (current)
            {
                // Let a pickup's demo or a door's unlock finish adding / spending its own key first.
                if (_idleTicks < IdleTicksRequired) continue;
                write = obs.KeyHud
                    ? new KeyWrite(dungeon.Slot, KeyWriteKind.Pending, target - game, game, target)
                    : new KeyWrite(dungeon.Slot, KeyWriteKind.Live, target, game, target);
                _idleTicks = 0; // one queued change at a time: the next tick sees it land first
            }
            else
            {
                write = new KeyWrite(dungeon.Slot, KeyWriteKind.Saved, target, game, target);
            }
            t.Awaiting = target; // our own write is not a pickup
            t.AwaitUntil = tick + WriteLandTicks;
            writes.Add(write);
            log.Add($"slot {dungeon.Slot}: small keys {game} → {target} ({Describe(dungeon, s)}" +
                    (t.Surplus > 0 ? $", +{t.Surplus} from unknown sources" : "") +
                    (current ? $", current stage, {(obs.KeyHud ? "via the HUD" : "live")})" : ", saved)"));
        }
        return writes;
    }

    private static string Describe(SmallKeyDungeon d, SlotKeyState s) =>
        $"{d.Taken(s.Tbox, s.Item)}/{d.Sources.Count} key(s) taken, {d.Opened(s.Switch)}/{d.Doors.Count} door(s) open";

    private static string Where(SmallKeyObservation obs) =>
        obs.Stage.Length == 0 ? "an unknown stage" : obs.Room >= 0 ? $"stage {obs.Stage} room {obs.Room}" : $"stage {obs.Stage}";
}
