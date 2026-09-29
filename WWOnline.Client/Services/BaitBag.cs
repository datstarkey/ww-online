using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The bait bag's 8 slots as the game stores them (tww-decomp d_save.h): the item in each slot
/// (dSv_player_bag_item_c::mBait[8]; <see cref="Empty"/> = 0xFF) and its count
/// (dSv_player_bag_item_record_c::mBaitNum[8]). See <see cref="BaitCounts"/> for what the counts mean.
/// </summary>
public sealed class BaitBagSlots
{
    public const byte Empty = 0xFF;
    public const byte Bait = ItemIDs.Bait.AllPurposeBait;
    public const byte Pear = ItemIDs.Bait.HyoiPear;

    public byte[] Items { get; }
    public byte[] Nums { get; }

    public BaitBagSlots(byte[] items, byte[] nums)
    {
        if (items.Length != BaitCounts.SlotCount || nums.Length != BaitCounts.SlotCount)
            throw new ArgumentException($"a bait bag has {BaitCounts.SlotCount} slots");
        Items = (byte[])items.Clone();
        Nums = (byte[])nums.Clone();
    }

    public BaitBagSlots Clone() => new(Items, Nums);

    public bool SameAs(BaitBagSlots other) => Items.AsSpan().SequenceEqual(other.Items) && Nums.AsSpan().SequenceEqual(other.Nums);

    /// <summary>A slot's All-Purpose Bait uses (its count, at most 3), 0 for any other slot.</summary>
    public int Uses(int slot) => Items[slot] == Bait ? Math.Min((int)Nums[slot], BaitCounts.UsesPerSlot) : 0;

    /// <summary>
    /// What the bag holds: All-Purpose Bait uses summed over its slots, and one Hyoi Pear per pear slot
    /// (the game sets a pear slot's count to 3 but never reads it: using a pear empties the slot).
    /// Any other item number in a slot is left alone and counts as neither.
    /// </summary>
    public BaitCounts Count()
    {
        int bait = 0, pears = 0;
        for (int i = 0; i < BaitCounts.SlotCount; i++)
        {
            bait += Uses(i);
            if (Items[i] == Pear) pears++;
        }
        return new BaitCounts(bait, pears);
    }

    public override string ToString() =>
        string.Join(" ", Enumerable.Range(0, BaitCounts.SlotCount).Select(i => Items[i] switch
        {
            Empty => "--",
            Bait => $"B{Nums[i]}",
            Pear => "P",
            _ => $"?{Items[i]:X2}",
        }));
}

/// <summary>
/// Writes a room total into this game's bait bag the way the game itself would lay it out, changing as
/// little as possible so the menu and the X/Y/Z buttons stay put:
///   - Removed pears come out of the highest-numbered pear slots, avoiding slots on a button.
///   - Removed bait uses come off the emptiest bait slots first (a slot at 0 empties, like
///     setBaitItemEmpty), again avoiding slots on a button.
///   - Added bait tops up partly used bait slots to 3, then fills the first empty slots (3 each, like
///     setBaitItem); added pears fill the first empty slots.
///   - If that doesn't fit, the bait is repacked into full slots (keeping the slots on a button) first.
/// Pure: the caller reads and writes game memory (<see cref="BaitBagMemory"/>).
/// </summary>
public static class BaitBag
{
    /// <summary>dInvSlot_BaitFirst_e (d_com_inf_game.h): the inventory slot number of bag slot 0, as the
    /// save's X/Y/Z select items store it.</summary>
    public const int FirstInvSlot = 36;
    public const int ButtonCount = 3; // dItemBtn_COUNT_e: X, Y, Z
    public const byte NoSlot = 0xFF;  // dInvSlot_NONE_e / dItemNo_NONE_e

    /// <summary>Bit i set = bag slot i is on X, Y or Z (<paramref name="selectSlots"/>: the save's mSelectItem).</summary>
    public static int EquippedMask(ReadOnlySpan<byte> selectSlots)
    {
        int mask = 0;
        for (int b = 0; b < Math.Min(ButtonCount, selectSlots.Length); b++)
        {
            int slot = selectSlots[b] - FirstInvSlot;
            if (slot >= 0 && slot < BaitCounts.SlotCount) mask |= 1 << slot;
        }
        return mask;
    }

    /// <summary>
    /// <paramref name="current"/> changed to hold <paramref name="target"/>. If the bag can't hold it all
    /// (slots taken by unknown items) the result holds what fits: check its <see cref="BaitBagSlots.Count"/>.
    /// </summary>
    public static BaitBagSlots Apply(BaitBagSlots current, BaitCounts target, int equippedMask = 0)
    {
        var s = current.Clone();
        var have = s.Count();
        bool Equipped(int i) => (equippedMask >> i & 1) != 0;
        int EquippedRank(int i) => Equipped(i) ? 1 : 0;
        var slots = Enumerable.Range(0, BaitCounts.SlotCount).ToArray();

        // Pears off: highest slots first, the ones on a button last.
        int pearsOff = have.Pears - Math.Max(target.Pears, 0);
        foreach (int i in slots.Where(i => s.Items[i] == BaitBagSlots.Pear)
                               .OrderBy(EquippedRank).ThenByDescending(i => i).Take(Math.Max(pearsOff, 0)).ToList())
            Clear(s, i);

        // Bait uses off: the emptiest slots first (a partly used slot goes before a full one), buttons last.
        int baitOff = have.Bait - Math.Max(target.Bait, 0);
        while (baitOff > 0)
        {
            int pick = slots.Where(i => s.Uses(i) > 0)
                            .OrderBy(EquippedRank).ThenBy(s.Uses).ThenByDescending(i => i)
                            .DefaultIfEmpty(-1).First();
            if (pick < 0) break;
            int take = Math.Min(s.Uses(pick), baitOff);
            s.Nums[pick] = (byte)(s.Uses(pick) - take);
            baitOff -= take;
            if (s.Nums[pick] == 0) Clear(s, pick);
        }

        int baitOn = Math.Max(target.Bait - have.Bait, 0);
        int pearsOn = Math.Max(target.Pears - have.Pears, 0);
        Add(s, ref baitOn, ref pearsOn);
        if (baitOn > 0 || pearsOn > 0)
        {
            Repack(s, equippedMask);
            Add(s, ref baitOn, ref pearsOn);
        }

        // A bait slot left at 0 uses is empty to the game (setBaitItemEmpty empties it at 0).
        foreach (int i in slots)
            if (s.Items[i] == BaitBagSlots.Bait && s.Nums[i] == 0) Clear(s, i);
        return s;
    }

    /// <summary>Top up bait slots, then fill empty slots with bait (3 a slot) and pears.</summary>
    private static void Add(BaitBagSlots s, ref int baitOn, ref int pearsOn)
    {
        for (int i = 0; i < BaitCounts.SlotCount && baitOn > 0; i++)
        {
            if (s.Items[i] != BaitBagSlots.Bait) continue;
            int add = Math.Min(BaitCounts.UsesPerSlot - s.Uses(i), baitOn);
            if (add <= 0) continue;
            s.Nums[i] = (byte)(s.Uses(i) + add);
            baitOn -= add;
        }
        for (int i = 0; i < BaitCounts.SlotCount && baitOn > 0; i++)
        {
            if (s.Items[i] != BaitBagSlots.Empty) continue;
            int add = Math.Min(BaitCounts.UsesPerSlot, baitOn);
            s.Items[i] = BaitBagSlots.Bait;
            s.Nums[i] = (byte)add;
            baitOn -= add;
        }
        for (int i = 0; i < BaitCounts.SlotCount && pearsOn > 0; i++)
        {
            if (s.Items[i] != BaitBagSlots.Empty) continue;
            s.Items[i] = BaitBagSlots.Pear;
            s.Nums[i] = BaitCounts.UsesPerSlot; // setBaitItem sets 3 for any bait item
            pearsOn--;
        }
    }

    /// <summary>Pack the bait uses into as few full slots as possible, keeping the slots on a button first.</summary>
    private static void Repack(BaitBagSlots s, int equippedMask)
    {
        var baitSlots = Enumerable.Range(0, BaitCounts.SlotCount)
            .Where(i => s.Items[i] == BaitBagSlots.Bait)
            .OrderByDescending(i => equippedMask >> i & 1).ThenBy(i => i).ToList();
        int uses = baitSlots.Sum(s.Uses);
        foreach (int i in baitSlots)
        {
            int put = Math.Min(BaitCounts.UsesPerSlot, uses);
            uses -= put;
            if (put == 0) Clear(s, i);
            else s.Nums[i] = (byte)put;
        }
    }

    private static void Clear(BaitBagSlots s, int i)
    {
        s.Items[i] = BaitBagSlots.Empty;
        s.Nums[i] = 0;
    }

    /// <summary>
    /// What dComIfGp_setSelectItem does after a bag slot changes under a button: a button on a slot that
    /// is now empty is cleared (save slot and play item 0xFF), and one on a slot that now holds another item
    /// shows that item. Returns the new save select slots and play select items (X, Y, Z), and whether
    /// anything changed.
    /// </summary>
    public static (byte[] SaveSelect, byte[] PlaySelect, bool Changed) FixButtons(
        BaitBagSlots before, BaitBagSlots after, ReadOnlySpan<byte> saveSelect, ReadOnlySpan<byte> playSelect)
    {
        var save = saveSelect.ToArray();
        var play = playSelect.ToArray();
        bool changed = false;
        for (int b = 0; b < Math.Min(ButtonCount, Math.Min(save.Length, play.Length)); b++)
        {
            int slot = save[b] - FirstInvSlot;
            if (slot < 0 || slot >= BaitCounts.SlotCount || before.Items[slot] == after.Items[slot]) continue;
            if (after.Items[slot] == BaitBagSlots.Empty)
            {
                save[b] = NoSlot;
                play[b] = NoSlot;
            }
            else
            {
                play[b] = after.Items[slot];
            }
            changed = true;
        }
        return (save, play, changed);
    }

    /// <summary>mBaitFlags bits for the types <paramref name="after"/> holds (bit 0 All-Purpose Bait, bit 1 Hyoi
    /// Pear: what item_func_bird_esa_5 / item_func_animal_esa set when one is obtained). ORed in, never cleared.</summary>
    public static byte GetFlagsFor(BaitBagSlots after)
    {
        byte flags = 0;
        foreach (var item in after.Items)
        {
            if (item == BaitBagSlots.Bait) flags |= 1;
            else if (item == BaitBagSlots.Pear) flags |= 2;
        }
        return flags;
    }
}

/// <summary>Reads and writes the bait bag in game memory (addresses: <see cref="GameMemoryAddresses.Inventory.BaitItems"/>).</summary>
public static class BaitBagMemory
{
    private const uint Start = GameMemoryAddresses.Inventory.BaitItems;
    private const int NumsOffset = (int)(GameMemoryAddresses.Inventory.BaitNums - Start);
    private const int Length = NumsOffset + BaitCounts.SlotCount;

    /// <summary>The player has the bait bag (inventory slot 11 holds dItemNo_BAIT_BAG_e).</summary>
    public static bool OwnsBag(IDolphinService dolphin) =>
        dolphin.Read(GameMemoryAddresses.Inventory.BaitBag) == ItemIDs.MainItems.BaitBag;

    /// <summary>Both arrays in one read (so a pickup can't land between them), or null.</summary>
    public static BaitBagSlots? Read(IDolphinService dolphin)
    {
        var b = dolphin.ReadMemory(Start, Length);
        if (b == null || b.Length < Length) return null;
        return new BaitBagSlots(b[..BaitCounts.SlotCount], b[NumsOffset..(NumsOffset + BaitCounts.SlotCount)]);
    }

    /// <summary>The save's X/Y/Z select slots, or null.</summary>
    public static byte[]? ReadSelectSlots(IDolphinService dolphin) =>
        dolphin.ReadMemory(GameMemoryAddresses.Player.SelectItemSlots, BaitBag.ButtonCount);

    /// <summary>
    /// Write <paramref name="next"/> over <paramref name="expected"/>, but only if the bag still is
    /// <paramref name="expected"/> right now (the game may have used or added bait since it was read).
    /// Only the changed bytes are written. Then the X/Y/Z buttons are fixed up (<see cref="BaitBag.FixButtons"/>)
    /// and the "ever obtained" flags set for any type that arrived.
    /// </summary>
    public static BagWriteResult Write(IDolphinService dolphin, BaitBagSlots expected, BaitBagSlots next)
    {
        if (Read(dolphin) is not { } now || !now.SameAs(expected)) return BagWriteResult.Raced;
        var saveSelect = ReadSelectSlots(dolphin);
        var playSelect = dolphin.ReadMemory(GameMemoryAddresses.Player.PlaySelectItems, BaitBag.ButtonCount);

        bool ok = BagMemory.WriteChanged(dolphin, GameMemoryAddresses.Inventory.BaitItems, expected.Items, next.Items) &&
                  BagMemory.WriteChanged(dolphin, GameMemoryAddresses.Inventory.BaitNums, expected.Nums, next.Nums);
        if (!ok) return BagWriteResult.Failed;

        if (saveSelect != null && playSelect != null)
        {
            var (save, play, changed) = BaitBag.FixButtons(expected, next, saveSelect, playSelect);
            if (changed)
            {
                dolphin.WriteMemory(GameMemoryAddresses.Player.SelectItemSlots, save);
                dolphin.WriteMemory(GameMemoryAddresses.Player.PlaySelectItems, play);
            }
        }

        byte flags = BaitBag.GetFlagsFor(next);
        if (flags != 0 && dolphin.ReadMemory(GameMemoryAddresses.Inventory.BaitGetFlags, 1) is [var got] && (got | flags) != got)
            dolphin.WriteMemory(GameMemoryAddresses.Inventory.BaitGetFlags, [(byte)(got | flags)]);
        return BagWriteResult.Written;
    }
}
