using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The play side of the shared inventory (dComIfG_play_c and the stage, outside the save window): the changes
/// queued for the HUD, the buttons' items and the stage's save slot (the scene itself: <see cref="GameScene"/>). <see cref="IDolphinService.Play"/>:
/// game memory on Dolphin (<see cref="DolphinPlayInventory"/>), typed calls on the native game.
/// </summary>
public interface IPlayInventory
{
    /// <summary>The changes queued for the HUD, indexed by <see cref="TwwInvCounter"/> (<see cref="TwwInvPlay.CounterCount"/>),
    /// or null. Non-zero: d_meter hasn't applied it yet.</summary>
    int[]? ReadPending();

    /// <summary>Queue a change the way the game's own pickups do (dComIfGp_setItem*Count, +=): d_meter applies it next
    /// frame, clamped, with the HUD animation, then zeroes it.</summary>
    bool Add(TwwInvCounter counter, int delta);

    /// <summary>play.mSelectItem for X, Y, Z (<see cref="BagMemory.ButtonCount"/> bytes): the item each button shows, or null.</summary>
    byte[]? ReadSelectItems();

    /// <summary>Write play.mSelectItem for X, Y, Z.</summary>
    bool WriteSelectItems(byte[] items);

    /// <summary>The current stage's save slot (dStage_stagInfo_GetSaveTbl) and key-HUD flag (dStage_stagInfo_ChkKeyDisp).</summary>
    (int? Slot, bool KeyHud) ReadStage();

    /// <summary>The puppets' equip swap sequence (<see cref="EquipSwapGuard"/>: odd = a peer's sword / shield are in
    /// the save's equip bytes), or null.</summary>
    uint? ReadEquipSwapSeq();
}

/// <summary><see cref="IPlayInventory"/> in GameCube memory: play at <see cref="GameMemoryAddresses.Play"/>.</summary>
public sealed class DolphinPlayInventory(IDolphinService dolphin) : IPlayInventory
{
    // play.mItemRupeeCount (s32) .. mItemBeastNumCounts[8] (s16): each counter's offset from Play + 0x48C0.
    private const uint PendingBase = GameMemoryAddresses.Play + 0x48C0;
    private const int PendingLength = 0x38;
    private static readonly int[] PendingOffsets = [0x00, 0x14, 0x16, 0x1C, 0x20, 0x24, 0x28, 0x2A, 0x2C, 0x2E, 0x30, 0x32, 0x34, 0x36];

    public int[]? ReadPending()
    {
        var b = dolphin.ReadMemory(PendingBase, PendingLength);
        if (b == null || b.Length < PendingLength) return null;
        var p = new int[TwwInvPlay.CounterCount];
        for (int i = 0; i < p.Length; i++)
            p[i] = i == (int)TwwInvCounter.Rupee ? b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3] : (short)(b[PendingOffsets[i]] << 8 | b[PendingOffsets[i] + 1]);
        return p;
    }

    public bool Add(TwwInvCounter counter, int delta)
    {
        if (counter == TwwInvCounter.Rupee)
        {
            int pending = dolphin.Read(GameMemoryAddresses.Player.PendingRupeeDelta) ?? 0;
            return dolphin.Write(GameMemoryAddresses.Player.PendingRupeeDelta, pending + delta);
        }
        uint address = PendingBase + (uint)PendingOffsets[(int)counter];
        if (dolphin.ReadMemory(address, 2) is not [var hi, var lo]) return false;
        short next = (short)((short)(hi << 8 | lo) + delta);
        return dolphin.WriteMemory(address, [(byte)(next >> 8), (byte)next]);
    }

    public byte[]? ReadSelectItems() => dolphin.ReadMemory(GameMemoryAddresses.Player.PlaySelectItems, BagMemory.ButtonCount);

    public bool WriteSelectItems(byte[] items) => dolphin.WriteMemory(GameMemoryAddresses.Player.PlaySelectItems, items);

    public (int? Slot, bool KeyHud) ReadStage()
    {
        var ptrBytes = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.StagInfoPtr, 4);
        if (ptrBytes == null) return (null, false);
        uint p = (uint)(ptrBytes[0] << 24 | ptrBytes[1] << 16 | ptrBytes[2] << 8 | ptrBytes[3]);
        if (p < 0x80000000 || p >= 0x84000000) return (null, false);
        var b = dolphin.ReadMemory(p + GameMemoryAddresses.WorldFlags.StagSaveTblOffset, 1);
        if (b == null) return (null, false);
        int slot = (b[0] >> 1) & 0x7F;
        return (slot < StageFlags.SlotCount ? slot : null, (b[0] & GameMemoryAddresses.WorldFlags.StagKeyDispMask) != 0);
    }

    public uint? ReadEquipSwapSeq()
    {
        var b = dolphin.ReadMemory(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 4);
        if (b == null || b.Length < 4) return null;
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }
}
