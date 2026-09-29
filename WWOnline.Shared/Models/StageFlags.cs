namespace WWOnline.Shared.Models;

/// <summary>
/// One stage's persistent world flags — a mirror of the game's dSv_memBit_c (chests opened,
/// memory switches, placed items collected, rooms visited, dungeon items/boss state).
/// Shared-world sync treats these as a grow-only set: players' flags are OR-merged, so once
/// anyone opens a chest or collects a placed rupee, it's done for everyone.
/// Small-key counts are not synced (a count, not a flag): each client derives them from these flags
/// (keys taken - key doors opened, the client's SmallKeyReconciler).
/// </summary>
public class StageFlags
{
    /// <summary>dSv_save_c::mMemory has 16 stage slots (STAGE_SEA = 0 ... STAGE_TEST = 15).</summary>
    public const int SlotCount = 16;
    public const int SwitchWords = 4;
    public const int VisitedRoomWords = 2;

    /// <summary>Stage save slot (dStage_stagInfo_GetSaveTbl), 0..15.</summary>
    public int Slot { get; set; }

    public uint Tbox { get; set; }
    public uint[] Switch { get; set; } = new uint[SwitchWords];
    public uint Item { get; set; }
    public uint[] VisitedRoom { get; set; } = new uint[VisitedRoomWords];
    public byte DungeonItem { get; set; }

    public bool IsValid() =>
        Slot is >= 0 and < SlotCount &&
        Switch is { Length: SwitchWords } &&
        VisitedRoom is { Length: VisitedRoomWords };

    public bool IsEmpty =>
        Tbox == 0 && Item == 0 && DungeonItem == 0 &&
        Switch.All(w => w == 0) && VisitedRoom.All(w => w == 0);

    /// <summary>OR <paramref name="other"/> into this. Returns true if any bit was added.</summary>
    public bool MergeFrom(StageFlags other)
    {
        bool changed = false;
        Tbox = Or(Tbox, other.Tbox, ref changed);
        Item = Or(Item, other.Item, ref changed);
        DungeonItem = (byte)Or(DungeonItem, other.DungeonItem, ref changed);
        for (int i = 0; i < SwitchWords; i++) Switch[i] = Or(Switch[i], other.Switch[i], ref changed);
        for (int i = 0; i < VisitedRoomWords; i++) VisitedRoom[i] = Or(VisitedRoom[i], other.VisitedRoom[i], ref changed);
        return changed;
    }

    /// <summary>Bits set in this but not in <paramref name="other"/> (same slot).</summary>
    public StageFlags Except(StageFlags other)
    {
        var d = new StageFlags
        {
            Slot = Slot,
            Tbox = Tbox & ~other.Tbox,
            Item = Item & ~other.Item,
            DungeonItem = (byte)(DungeonItem & ~other.DungeonItem),
        };
        for (int i = 0; i < SwitchWords; i++) d.Switch[i] = Switch[i] & ~other.Switch[i];
        for (int i = 0; i < VisitedRoomWords; i++) d.VisitedRoom[i] = VisitedRoom[i] & ~other.VisitedRoom[i];
        return d;
    }

    public StageFlags Clone()
    {
        var c = new StageFlags { Slot = Slot, Tbox = Tbox, Item = Item, DungeonItem = DungeonItem };
        Array.Copy(Switch, c.Switch, SwitchWords);
        Array.Copy(VisitedRoom, c.VisitedRoom, VisitedRoomWords);
        return c;
    }

    public int BitCount =>
        System.Numerics.BitOperations.PopCount(Tbox) + System.Numerics.BitOperations.PopCount(Item) +
        System.Numerics.BitOperations.PopCount(DungeonItem) +
        Switch.Sum(w => System.Numerics.BitOperations.PopCount(w)) +
        VisitedRoom.Sum(w => System.Numerics.BitOperations.PopCount(w));

    public override string ToString() =>
        $"slot {Slot}: tbox={Tbox:X8} sw={string.Join(",", Switch.Select(w => w.ToString("X8")))} item={Item:X8} " +
        $"visited={string.Join(",", VisitedRoom.Select(w => w.ToString("X8")))} dungeon={DungeonItem:X2}";

    private static uint Or(uint a, uint b, ref bool changed)
    {
        uint r = a | b;
        if (r != a) changed = true;
        return r;
    }
}
