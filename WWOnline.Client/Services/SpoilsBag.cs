using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The spoils bag as the game stores it (tww-decomp d_save.h): the item in each of the 8 menu slots
/// (dSv_player_bag_item_c::mBeast[8], 0xFF = empty) and the count of each TYPE
/// (dSv_player_bag_item_record_c::mBeastNum[8], indexed by dBeastIndex_e, not by slot).
/// </summary>
public sealed class SpoilsBagSlots
{
    public const byte Empty = 0xFF;

    public byte[] Items { get; }
    public byte[] Nums { get; }

    public SpoilsBagSlots(byte[] items, byte[] nums)
    {
        if (items.Length != SpoilsCounts.TypeCount || nums.Length != SpoilsCounts.TypeCount)
            throw new ArgumentException($"a spoils bag has {SpoilsCounts.TypeCount} slots");
        Items = (byte[])items.Clone();
        Nums = (byte[])nums.Clone();
    }

    public bool SameAs(SpoilsBagSlots other) => Items.AsSpan().SequenceEqual(other.Items) && Nums.AsSpan().SequenceEqual(other.Nums);

    /// <summary>The bag slot showing spoil <paramref name="type"/> (dBeastIndex_e), or -1.</summary>
    public int SlotOf(int type) => Array.IndexOf(Items, SpoilsCounts.Types[type].ItemNo);

    /// <summary>
    /// The count of each type the menu shows: its mBeastNum (at most 99) while the type has a slot, else 0
    /// (d_meter empties a type's slot when its count reaches 0).
    /// </summary>
    public SpoilsCounts Count()
    {
        var counts = new int[SpoilsCounts.TypeCount];
        for (int t = 0; t < SpoilsCounts.TypeCount; t++)
            counts[t] = SlotOf(t) >= 0 ? Math.Min((int)Nums[t], SpoilsCounts.MaxCount) : 0;
        return new SpoilsCounts(counts);
    }

    public override string ToString() =>
        string.Join(" ", Items.Select(i => i == Empty ? "--" : $"{i:X2}")) + " | " + string.Join(" ", Nums);
}

/// <summary>
/// How a room total goes into this game's spoils bag: the way the game itself does it for a pickup, sale or
/// trade (item_func_skull_necklace and friends, npc trades): a type that arrives takes the first free slot
/// (setBeastItem), and every count change is queued on play.mItemBeastNumCounts for d_meter to apply next
/// frame (clamped to 0-99; a type at 0 leaves its slot and any X/Y/Z button, setBeastItemEmpty). The menu and
/// HUD update as for a normal pickup. Pure: <see cref="SpoilsBagMemory"/> reads and writes.
/// </summary>
public static class SpoilsBag
{
    /// <summary>What to write: the new slot items (types that arrive), the pending count change per type, and the
    /// counts the bag will hold once d_meter has applied them (the target, except a type with no free slot).</summary>
    public sealed record Plan(byte[] Items, short[] Pending, SpoilsCounts Expected)
    {
        public bool IsEmpty => Pending.All(p => p == 0);
    }

    public static Plan PlanFor(SpoilsBagSlots bag, SpoilsCounts target)
    {
        var items = (byte[])bag.Items.Clone();
        var pending = new short[SpoilsCounts.TypeCount];
        var expected = bag.Count().Counts;
        for (int t = 0; t < SpoilsCounts.TypeCount; t++)
        {
            int want = Math.Clamp(target[t], 0, SpoilsCounts.MaxCount);
            int num = bag.Nums[t];
            bool hasSlot = Array.IndexOf(items, SpoilsCounts.Types[t].ItemNo) >= 0;
            if (want > 0)
            {
                if (!hasSlot)
                {
                    int free = Array.IndexOf(items, SpoilsBagSlots.Empty);
                    if (free < 0) continue; // no room (a slot holds something unknown): leave this type
                    items[free] = SpoilsCounts.Types[t].ItemNo;
                }
                if (want != num) pending[t] = (short)(want - num); // a missing slot with the right count needs only the slot
                expected[t] = want;
            }
            else if (hasSlot || num != 0)
            {
                // At least -1 so d_meter runs its "reached 0" branch and empties the slot even if num is 0.
                pending[t] = (short)-Math.Max(num, 1);
                expected[t] = 0;
            }
        }
        return new Plan(items, pending, new SpoilsCounts(expected));
    }

    /// <summary>mBeastFlags bits (dBeastIndex_e) for the types <paramref name="items"/> holds: what
    /// onGetItemBeast sets when one is obtained. ORed in, never cleared.</summary>
    public static byte GetFlagsFor(byte[] items)
    {
        byte flags = 0;
        for (int t = 0; t < SpoilsCounts.TypeCount; t++)
            if (Array.IndexOf(items, SpoilsCounts.Types[t].ItemNo) >= 0) flags |= (byte)(1 << t);
        return flags;
    }

    /// <summary>
    /// The sync point once the game has applied a <see cref="Plan"/> (its pending counts are back to 0): the
    /// planned counts, not the bag's current ones, so a pickup or sale that landed meanwhile is still sent as a
    /// local change. A type still showing exactly what it had before, though it should have changed, didn't
    /// take the write: its current count is used, so the unapplied difference isn't sent back.
    /// </summary>
    public static SpoilsCounts SettledBaseline(SpoilsCounts expected, SpoilsCounts before, SpoilsCounts current, out bool writeLost)
    {
        writeLost = false;
        var b = new int[SpoilsCounts.TypeCount];
        for (int t = 0; t < SpoilsCounts.TypeCount; t++)
        {
            bool lost = current[t] == before[t] && expected[t] != before[t];
            writeLost |= lost;
            b[t] = lost ? current[t] : expected[t];
        }
        return new SpoilsCounts(b);
    }
}

/// <summary>Reads and writes the spoils bag in game memory (<see cref="GameMemoryAddresses.Inventory.SpoilsItems"/>).</summary>
public static class SpoilsBagMemory
{
    private const uint Start = GameMemoryAddresses.Inventory.SpoilsItems;
    private const int NumsOffset = (int)(GameMemoryAddresses.Inventory.SpoilsNums - Start);
    private const int Length = NumsOffset + SpoilsCounts.TypeCount;

    /// <summary>The player has the spoils bag (inventory slot 4 holds dItemNo_SPOILS_BAG_e).</summary>
    public static bool OwnsBag(IDolphinService dolphin) =>
        dolphin.Read(GameMemoryAddresses.Inventory.SpoilsBag) == ItemIDs.MainItems.SpoilsBag;

    /// <summary>Slots and counts in one read, or null.</summary>
    public static SpoilsBagSlots? Read(IDolphinService dolphin)
    {
        var b = dolphin.ReadMemory(Start, Length);
        if (b == null || b.Length < Length) return null;
        return new SpoilsBagSlots(b[..SpoilsCounts.TypeCount], b[NumsOffset..(NumsOffset + SpoilsCounts.TypeCount)]);
    }

    /// <summary>play.mItemBeastNumCounts, or null. Non-zero: the game hasn't applied a count change yet.</summary>
    public static short[]? ReadPending(IDolphinService dolphin)
    {
        var b = dolphin.ReadMemory(GameMemoryAddresses.Inventory.PendingSpoilsDeltas, 2 * SpoilsCounts.TypeCount);
        if (b == null || b.Length < 2 * SpoilsCounts.TypeCount) return null;
        var p = new short[SpoilsCounts.TypeCount];
        for (int t = 0; t < p.Length; t++) p[t] = (short)(b[2 * t] << 8 | b[2 * t + 1]);
        return p;
    }

    /// <summary>
    /// Write <paramref name="plan"/> over <paramref name="expected"/>, but only if the bag still is
    /// <paramref name="expected"/> and nothing is pending right now (the game may have picked up, sold or traded
    /// something since it was read). New slots first, then the "ever obtained" flags, then the pending counts.
    /// </summary>
    public static BagWriteResult Write(IDolphinService dolphin, SpoilsBagSlots expected, SpoilsBag.Plan plan)
    {
        if (Read(dolphin) is not { } now || !now.SameAs(expected)) return BagWriteResult.Raced;
        if (ReadPending(dolphin) is not { } pending || pending.Any(p => p != 0)) return BagWriteResult.Raced;

        if (!BagMemory.WriteChanged(dolphin, GameMemoryAddresses.Inventory.SpoilsItems, expected.Items, plan.Items))
            return BagWriteResult.Failed;

        byte flags = SpoilsBag.GetFlagsFor(plan.Items);
        if (flags != 0 && dolphin.ReadMemory(GameMemoryAddresses.Inventory.SpoilsGetFlags, 1) is [var got] && (got | flags) != got)
            dolphin.WriteMemory(GameMemoryAddresses.Inventory.SpoilsGetFlags, [(byte)(got | flags)]);

        for (int t = 0; t < SpoilsCounts.TypeCount; t++)
        {
            short p = plan.Pending[t];
            if (p == 0) continue;
            if (!dolphin.WriteMemory(GameMemoryAddresses.Inventory.PendingSpoilsDeltas + (uint)(2 * t), [(byte)(p >> 8), (byte)p]))
                return BagWriteResult.Failed;
        }
        return BagWriteResult.Written;
    }
}

/// <summary>What a compare-and-swap bag write did.</summary>
public enum BagWriteResult
{
    /// <summary>Written.</summary>
    Written,
    /// <summary>The bag changed since it was read: nothing written; the caller re-reads.</summary>
    Raced,
    /// <summary>A write failed: the bag may be partly written; the caller re-reads it.</summary>
    Failed,
}

/// <summary>Byte-level helpers the bag writers share.</summary>
public static class BagMemory
{
    /// <summary>dItemBtn_COUNT_e: X, Y, Z.</summary>
    public const int ButtonCount = 3;

    /// <summary>dInvSlot_NONE_e / dItemNo_NONE_e.</summary>
    public const byte NoSlot = 0xFF;

    /// <summary>Bit i set = bag slot i (inventory slot <paramref name="firstInvSlot"/> + i) is on X, Y or Z
    /// (<paramref name="selectSlots"/>: the save's mSelectItem).</summary>
    public static int EquippedMask(int firstInvSlot, int slotCount, ReadOnlySpan<byte> selectSlots)
    {
        int mask = 0;
        for (int b = 0; b < Math.Min(ButtonCount, selectSlots.Length); b++)
        {
            int slot = selectSlots[b] - firstInvSlot;
            if (slot >= 0 && slot < slotCount) mask |= 1 << slot;
        }
        return mask;
    }

    /// <summary>
    /// What dComIfGp_setSelectItem (d_com_inf_game.h) does after a bag slot changes under a button: a button on a
    /// slot that is now empty is cleared (save slot and play item 0xFF), and one on a slot that now holds another
    /// item shows that item. <paramref name="firstInvSlot"/> is the bag's first inventory slot number (bait 36,
    /// delivery 48). Returns the new save select slots and play select items (X, Y, Z), and whether anything changed.
    /// </summary>
    public static (byte[] SaveSelect, byte[] PlaySelect, bool Changed) FixButtons(
        int firstInvSlot, byte[] before, byte[] after, ReadOnlySpan<byte> saveSelect, ReadOnlySpan<byte> playSelect)
    {
        var save = saveSelect.ToArray();
        var play = playSelect.ToArray();
        bool changed = false;
        for (int b = 0; b < Math.Min(ButtonCount, Math.Min(save.Length, play.Length)); b++)
        {
            int slot = save[b] - firstInvSlot;
            if (slot < 0 || slot >= after.Length || before[slot] == after[slot]) continue;
            if (after[slot] == NoSlot)
            {
                save[b] = NoSlot;
                play[b] = NoSlot;
            }
            else
            {
                play[b] = after[slot];
            }
            changed = true;
        }
        return (save, play, changed);
    }

    /// <summary>Write the span of <paramref name="next"/> that differs from <paramref name="old"/>, if any.</summary>
    public static bool WriteChanged(IDolphinService dolphin, uint address, byte[] old, byte[] next)
    {
        int first = -1, last = -1;
        for (int i = 0; i < next.Length; i++)
        {
            if (old[i] == next[i]) continue;
            if (first < 0) first = i;
            last = i;
        }
        return first < 0 || dolphin.WriteMemory(address + (uint)first, next[first..(last + 1)]);
    }
}
