using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The delivery bag as the game stores it (tww-decomp d_save.h): the item in each of the 8 menu slots
/// (dSv_player_bag_item_c::mReserve[8], <see cref="Empty"/> = 0xFF; there are no counts) and the "ever obtained"
/// bits (dSv_player_get_bag_item_c::mReserveFlags, bit = item - 0x8C). See <see cref="DeliveryCounts"/>.
/// </summary>
public sealed class DeliveryBagSlots
{
    public const byte Empty = 0xFF;

    public byte[] Items { get; }
    public uint Flags { get; }

    public DeliveryBagSlots(byte[] items, uint flags = 0)
    {
        if (items.Length != DeliveryCounts.SlotCount)
            throw new ArgumentException($"a delivery bag has {DeliveryCounts.SlotCount} slots");
        Items = (byte[])items.Clone();
        Flags = flags;
    }

    public bool SameItemsAs(DeliveryBagSlots other) => Items.AsSpan().SequenceEqual(other.Items);

    /// <summary>
    /// How many slots hold each item this rule knows, and the known obtained bits. A slot holding anything else
    /// (the unused salvage items 0x9F-0xA2) counts as none and is left alone.
    /// </summary>
    public DeliveryCounts Count()
    {
        var counts = new int[DeliveryCounts.TypeCount];
        foreach (var item in Items)
            if (DeliveryCounts.TypeOf(item) is var t and >= 0) counts[t]++;
        return new DeliveryCounts(counts, Flags & DeliveryCounts.KnownObtainedMask);
    }

    public override string ToString() =>
        string.Join(" ", Items.Select(i => i == Empty ? "--" : $"{i:X2}")) + $" | got {Flags:X8}";
}

/// <summary>
/// How the room's bag goes into this game's delivery bag: the way the game itself adds and takes items, changing
/// as few slots as possible so the menu and the X/Y/Z buttons stay put:
///   - An item that leaves empties a slot holding it (setReserveItemEmpty), the highest slot first and a slot on a
///     button last.
///   - An item that arrives takes the first empty slot (setReserveItem). Removals go first, so a trade (one item
///     out, another in) lands in the slot the traded item left when that is the first empty one, as
///     setReserveItemChange does.
///   - A button whose slot changed is fixed the way dComIfGp_setSelectItem does it (<see cref="BagMemory.FixButtons"/>).
///   - The obtained bits are ORed in: the room's, and every item's that arrives (item_func_* sets it with the item).
/// Pure: <see cref="DeliveryBagMemory"/> reads and writes.
/// </summary>
public static class DeliveryBag
{
    /// <summary>dInvSlot_ReserveFirst_e (d_com_inf_game.h): the inventory slot number of bag slot 0, as the save's
    /// X/Y/Z select items store it.</summary>
    public const int FirstInvSlot = 48;

    /// <summary>Bit i set = bag slot i is on X, Y or Z.</summary>
    public static int EquippedMask(ReadOnlySpan<byte> selectSlots) =>
        BagMemory.EquippedMask(FirstInvSlot, DeliveryCounts.SlotCount, selectSlots);

    /// <summary>
    /// <paramref name="current"/> changed to hold <paramref name="target"/>. If the bag can't hold it all (slots
    /// taken by unknown items) the result holds what fits: check its <see cref="DeliveryBagSlots.Count"/>.
    /// </summary>
    public static DeliveryBagSlots Apply(DeliveryBagSlots current, DeliveryCounts target, int equippedMask = 0)
    {
        var items = (byte[])current.Items.Clone();
        var have = current.Count();
        bool Equipped(int i) => (equippedMask >> i & 1) != 0;
        uint flags = current.Flags | (target.Obtained & DeliveryCounts.KnownObtainedMask);

        for (int t = 0; t < DeliveryCounts.TypeCount; t++)
        {
            int off = have[t] - Math.Max(target[t], 0);
            if (off <= 0) continue;
            byte itemNo = DeliveryCounts.Types[t].ItemNo;
            foreach (int i in Enumerable.Range(0, DeliveryCounts.SlotCount).Where(i => items[i] == itemNo)
                                        .OrderBy(i => Equipped(i) ? 1 : 0).ThenByDescending(i => i).Take(off).ToList())
                items[i] = DeliveryBagSlots.Empty;
        }

        for (int t = 0; t < DeliveryCounts.TypeCount; t++)
        {
            int on = Math.Max(target[t], 0) - have[t];
            for (; on > 0; on--)
            {
                int free = Array.IndexOf(items, DeliveryBagSlots.Empty);
                if (free < 0) break; // no room (a slot holds something unknown): leave the rest
                items[free] = DeliveryCounts.Types[t].ItemNo;
                flags |= 1u << t;
            }
        }
        return new DeliveryBagSlots(items, flags);
    }
}

/// <summary>Reads and writes the delivery bag in game memory (<see cref="GameMemoryAddresses.Inventory.DeliveryItems"/>).</summary>
public static class DeliveryBagMemory
{
    private const uint Start = GameMemoryAddresses.Inventory.DeliveryItems;
    private const int FlagsOffset = (int)(GameMemoryAddresses.Inventory.DeliveryGetFlags - Start);
    private const int Length = FlagsOffset + 4;

    /// <summary>The player has the delivery bag (its inventory slot holds dItemNo_DELIVERY_BAG_e).</summary>
    public static bool OwnsBag(IDolphinService dolphin) =>
        dolphin.Read(GameMemoryAddresses.Inventory.DeliveryBag) == ItemIDs.MainItems.DeliveryBag;

    /// <summary>Slots and obtained bits in one read (they are 10 bytes apart), or null.</summary>
    public static DeliveryBagSlots? Read(IDolphinService dolphin)
    {
        var b = dolphin.ReadMemory(Start, Length);
        if (b == null || b.Length < Length) return null;
        uint flags = (uint)(b[FlagsOffset] << 24 | b[FlagsOffset + 1] << 16 | b[FlagsOffset + 2] << 8 | b[FlagsOffset + 3]);
        return new DeliveryBagSlots(b[..DeliveryCounts.SlotCount], flags);
    }

    /// <summary>The save's X/Y/Z select slots, or null.</summary>
    public static byte[]? ReadSelectSlots(IDolphinService dolphin) =>
        dolphin.ReadMemory(GameMemoryAddresses.Player.SelectItemSlots, BagMemory.ButtonCount);

    /// <summary>
    /// Write <paramref name="next"/> over <paramref name="expected"/>, but only if the bag's slots still are
    /// <paramref name="expected"/>'s right now (the game may have added or taken an item since it was read). Only
    /// the changed slots are written, then the X/Y/Z buttons are fixed up and the obtained bits ORed in (never
    /// cleared).
    /// </summary>
    public static BagWriteResult Write(IDolphinService dolphin, DeliveryBagSlots expected, DeliveryBagSlots next)
    {
        if (Read(dolphin) is not { } now || !now.SameItemsAs(expected)) return BagWriteResult.Raced;
        var saveSelect = ReadSelectSlots(dolphin);
        var playSelect = dolphin.ReadMemory(GameMemoryAddresses.Player.PlaySelectItems, BagMemory.ButtonCount);

        if (!BagMemory.WriteChanged(dolphin, Start, expected.Items, next.Items)) return BagWriteResult.Failed;

        if (saveSelect != null && playSelect != null)
        {
            var (save, play, changed) = BagMemory.FixButtons(DeliveryBag.FirstInvSlot, expected.Items, next.Items, saveSelect, playSelect);
            if (changed)
            {
                dolphin.WriteMemory(GameMemoryAddresses.Player.SelectItemSlots, save);
                dolphin.WriteMemory(GameMemoryAddresses.Player.PlaySelectItems, play);
            }
        }

        uint flags = now.Flags | next.Flags;
        if (flags != now.Flags &&
            !dolphin.WriteMemory(GameMemoryAddresses.Inventory.DeliveryGetFlags, [(byte)(flags >> 24), (byte)(flags >> 16), (byte)(flags >> 8), (byte)flags]))
            return BagWriteResult.Failed;
        return BagWriteResult.Written;
    }

    private static uint PedestalStart => EventFlagCatalog.EventBitfieldAddress + DeliveryCounts.FirstPedestalRegister;

    /// <summary>
    /// Windfall's pedestals (event registers D1FF-F8FF: the trade good on each, or 0), or null. A value that isn't a
    /// pedestal item (never in the vanilla game) reads as 0 and is never written over.
    /// </summary>
    public static byte[]? ReadPedestals(IDolphinService dolphin)
    {
        var b = dolphin.ReadMemory(PedestalStart, DeliveryCounts.PedestalCount);
        if (b is not { Length: DeliveryCounts.PedestalCount }) return null;
        for (int i = 0; i < b.Length; i++)
            if (!DeliveryCounts.IsPedestalItem(b[i])) b[i] = 0;
        return b;
    }

    /// <summary>
    /// Write each pedestal that differs between <paramref name="expected"/> and <paramref name="next"/>, only if it
    /// still holds <paramref name="expected"/>'s item right now (compare-and-swap per register). A pedestal reads its
    /// register when it is created (d_a_dai.cpp:101), so one already on screen shows the change on the next visit.
    /// </summary>
    public static BagWriteResult WritePedestals(IDolphinService dolphin, byte[] expected, byte[] next)
    {
        if (ReadPedestals(dolphin) is not { } now) return BagWriteResult.Failed;
        var changed = Enumerable.Range(0, DeliveryCounts.PedestalCount).Where(i => expected[i] != next[i]).ToList();
        if (changed.Any(i => now[i] != expected[i])) return BagWriteResult.Raced;
        foreach (int i in changed)
            if (!dolphin.WriteMemory(PedestalStart + (uint)i, [next[i]])) return BagWriteResult.Failed;
        return BagWriteResult.Written;
    }
}
