using System.Numerics;

namespace WWOnline.Patcher.WorldData;

/// <summary>Where a small key comes from: a chest (dSv_memBit_c::mTbox bit) or a placed item (mItem bit).</summary>
public enum SmallKeySourceKind
{
    Chest,
    Item,
}

/// <summary>One small key placed in a dungeon, and the save flag that says it was taken.</summary>
/// <param name="Bit">The chest's tbox number (0-31) or the item's memory item bit (0-31).</param>
/// <param name="Stage">The stage it was found in (for the logs).</param>
/// <param name="Room">The room it is placed in, or -1 for Stage.arc.</param>
public readonly record struct SmallKeySource(SmallKeySourceKind Kind, int Bit, string Stage, int Room);

/// <summary>One small-key door (daDoor10_c / daDoor12_c with chkMakeKey() == 1) and its memory switch.</summary>
public readonly record struct SmallKeyDoor(int Switch, string Stage, int Room);

/// <summary>
/// One dungeon save slot's small-key bookkeeping: every key source and every key door. The small keys a
/// player holds there are <c>keys taken - doors opened</c>, both read from world flags that the shared
/// world already OR-merges (<see cref="Derive"/>), so every player computes the same count.
/// </summary>
public sealed class SmallKeyDungeon
{
    public required int Slot { get; init; }
    public required IReadOnlyList<SmallKeySource> Sources { get; init; }
    public required IReadOnlyList<SmallKeyDoor> Doors { get; init; }

    /// <summary>Key sources whose flag isn't a memory tbox/item bit (a zone or no item bit): they can't be counted
    /// from the save, so a key from one shows up as a key the table doesn't know (logged at runtime).</summary>
    public IReadOnlyList<string> Untracked { get; init; } = [];

    /// <summary>mTbox bits of the key chests.</summary>
    public uint TboxMask => Mask(SmallKeySourceKind.Chest);

    /// <summary>mItem bits of the placed keys.</summary>
    public uint ItemMask => Mask(SmallKeySourceKind.Item);

    /// <summary>mSwitch[4] bits of the key doors.</summary>
    public uint[] SwitchMask
    {
        get
        {
            var w = new uint[4];
            foreach (var d in Doors) w[d.Switch >> 5] |= 1u << (d.Switch & 31);
            return w;
        }
    }

    private uint Mask(SmallKeySourceKind kind)
    {
        uint m = 0;
        foreach (var s in Sources)
            if (s.Kind == kind) m |= 1u << s.Bit;
        return m;
    }

    /// <summary>Keys taken (their flags set) in these memBit words.</summary>
    public int Taken(uint tbox, uint item) =>
        BitOperations.PopCount(tbox & TboxMask) + BitOperations.PopCount(item & ItemMask);

    /// <summary>Key doors opened (their switches set).</summary>
    public int Opened(IReadOnlyList<uint> switches)
    {
        var mask = SwitchMask;
        int n = 0;
        for (int i = 0; i < 4 && i < switches.Count; i++) n += BitOperations.PopCount(switches[i] & mask[i]);
        return n;
    }

    /// <summary>
    /// Keys taken minus doors opened: the small keys a player holds in this dungeon. Can be negative when
    /// flags from another save disagree (two players each opened a door with the same last key) or a key
    /// came from a source the table doesn't know; the caller clamps it.
    /// </summary>
    public int Derive(uint tbox, uint item, IReadOnlyList<uint> switches) => Taken(tbox, item) - Opened(switches);
}

/// <summary>
/// The small keys of every dungeon, built from the player's own stage files (<see cref="SmallKeyTableBuilder"/>);
/// never in the repo (it is derived from Nintendo's stage data).
/// </summary>
public sealed class SmallKeyTable
{
    /// <summary>How many stages the table was built from (a sanity figure for the logs).</summary>
    public int StageCount { get; init; }

    /// <summary>Save slot → its small keys. Only slots with a key door or a key source.</summary>
    public IReadOnlyDictionary<int, SmallKeyDungeon> Dungeons { get; init; } = new Dictionary<int, SmallKeyDungeon>();

    public SmallKeyDungeon? For(int slot) => Dungeons.GetValueOrDefault(slot);

    /// <summary>"slot 3: 4 keys / 4 doors, ..." for the logs.</summary>
    public string Summary() => string.Join(", ", Dungeons.Values.OrderBy(d => d.Slot).Select(d =>
        $"slot {d.Slot}: {d.Sources.Count} key(s) / {d.Doors.Count} door(s)" +
        (d.Untracked.Count > 0 ? $" (+{d.Untracked.Count} untracked)" : "")));
}
