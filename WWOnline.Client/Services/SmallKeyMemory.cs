using WWOnline.Data;
using WWOnline.Native;
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

        var pending = dolphin.Play.ReadPending()?[(int)TwwInvCounter.Key];
        var scene = dolphin.ReadScene();
        bool idle = pending == 0 && scene is { IsIdle: true };

        return new SmallKeyObservation(slots, live != null ? slot : null, keyHud, idle, scene?.Stage ?? "", scene?.Room ?? -1);
    }

    /// <summary>The current stage's save slot (dStage_stagInfo_GetSaveTbl) and key-HUD flag (dStage_stagInfo_ChkKeyDisp).</summary>
    public static (int? Slot, bool KeyHud) ReadStage(IDolphinService dolphin) => dolphin.Play.ReadStage();

    /// <summary>Make one write. A pending change is written only while none is queued (the game's own += would race it).</summary>
    public static bool Apply(IDolphinService dolphin, KeyWrite w)
    {
        switch (w.Kind)
        {
            case KeyWriteKind.Pending:
                if (dolphin.Play.ReadPending()?[(int)TwwInvCounter.Key] != 0) return false;
                return dolphin.Play.Add(TwwInvCounter.Key, (short)w.Value);
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
