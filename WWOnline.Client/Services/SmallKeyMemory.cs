using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Reads the game's small-key state (every stage slot's memBit flags and mKeyNum, the current slot, the
/// key HUD and whether the game is idle) and makes <see cref="SmallKeyReconciler"/>'s writes.
/// The current slot's flags and count come from the live copy (dSv_info_c::mMemory), the others from
/// the save (dSv_save_c::mMemory[slot]); the saved copy of the current slot is stale and never written
/// (putSave overwrites it from the live copy when the stage is left, d_stage.cpp:2255).
/// </summary>
public static class SmallKeyMemory
{
    private const int SlotCount = StageFlags.SlotCount;

    /// <summary>Read the whole state, or null when the save can't be read.</summary>
    public static SmallKeyObservation? Read(IDolphinService dolphin)
    {
        var saved = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.SavedMemoryBase, SlotCount * GameMemoryAddresses.WorldFlags.MemorySize);
        if (saved == null) return null;
        var (slot, keyHud) = ReadStage(dolphin);
        var live = slot.HasValue
            ? dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.LiveMemory, GameMemoryAddresses.WorldFlags.MemorySize)
            : null;

        var slots = new SlotKeyState[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            slots[i] = i == slot && live != null ? Parse(live, 0) : Parse(saved, i * GameMemoryAddresses.WorldFlags.MemorySize);

        var pending = dolphin.Read(GameMemoryAddresses.WorldFlags.PendingKeyDelta);
        var evt = dolphin.ReadMemory(GameMemoryAddresses.Events.EventMode, 1);
        var menu = dolphin.ReadMemory(GameMemoryAddresses.Events.MenuPause, 1);
        bool idle = pending == 0 && evt is [0] && menu is [0];

        var name = dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentStageName.Address, 8);
        string stage = name == null ? "" : System.Text.Encoding.ASCII.GetString(name, 0, Array.IndexOf(name, (byte)0) is int n and >= 0 ? n : name.Length);
        int room = dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, 1) is [var r] ? (sbyte)r : -1;

        return new SmallKeyObservation(slots, live != null ? slot : null, keyHud, idle, stage, room);
    }

    /// <summary>The current stage's save slot (dStage_stagInfo_GetSaveTbl) and key-HUD flag (dStage_stagInfo_ChkKeyDisp).</summary>
    public static (int? Slot, bool KeyHud) ReadStage(IDolphinService dolphin)
    {
        var ptrBytes = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.StagInfoPtr, 4);
        if (ptrBytes == null) return (null, false);
        uint p = ReadU32(ptrBytes, 0);
        if (p < 0x80000000 || p >= 0x84000000) return (null, false);
        var b = dolphin.ReadMemory(p + GameMemoryAddresses.WorldFlags.StagSaveTblOffset, 1);
        if (b == null) return (null, false);
        int slot = (b[0] >> 1) & 0x7F;
        return (slot < SlotCount ? slot : null, (b[0] & GameMemoryAddresses.WorldFlags.StagKeyDispMask) != 0);
    }

    /// <summary>Make one write. A pending change is written only while none is queued (the game's own += would race it).</summary>
    public static bool Apply(IDolphinService dolphin, KeyWrite w)
    {
        switch (w.Kind)
        {
            case KeyWriteKind.Pending:
                if (dolphin.Read(GameMemoryAddresses.WorldFlags.PendingKeyDelta) != 0) return false;
                return dolphin.Write(GameMemoryAddresses.WorldFlags.PendingKeyDelta, (short)w.Value);
            case KeyWriteKind.Live:
                return dolphin.WriteMemory(GameMemoryAddresses.WorldFlags.LiveKeyNum, [(byte)Math.Clamp(w.Value, 0, SmallKeyReconciler.MaxKeys)]);
            case KeyWriteKind.Saved:
                return dolphin.WriteMemory(GameMemoryAddresses.WorldFlags.SavedSlot(w.Slot) + GameMemoryAddresses.WorldFlags.OffKeyNum,
                    [(byte)Math.Clamp(w.Value, 0, SmallKeyReconciler.MaxKeys)]);
            default:
                return false;
        }
    }

    private static SlotKeyState Parse(byte[] b, int off)
    {
        var sw = new uint[StageFlags.SwitchWords];
        for (int w = 0; w < sw.Length; w++) sw[w] = ReadU32(b, off + GameMemoryAddresses.WorldFlags.OffSwitch + w * 4);
        return new SlotKeyState(
            ReadU32(b, off + GameMemoryAddresses.WorldFlags.OffTbox),
            ReadU32(b, off + GameMemoryAddresses.WorldFlags.OffItem),
            sw,
            b[off + GameMemoryAddresses.WorldFlags.OffKeyNum],
            b[off + GameMemoryAddresses.WorldFlags.OffDungeonItem]);
    }

    private static uint ReadU32(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
}
