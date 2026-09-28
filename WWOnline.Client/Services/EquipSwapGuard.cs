using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// The puppet REL temporarily swaps a peer's sword / shield ids into the local save's equip bytes
/// (<see cref="GameMemoryAddresses.Player.CurrentSword"/> / CurrentShield) while it runs that
/// peer's puppet, bracketed by PUPPET_EQUIP_SWAP_SEQ (++ at swap start and end: odd = in progress).
/// Every C# reader / writer of those two bytes goes through here so it never sees or clobbers the
/// peer's values.
/// </summary>
public static class EquipSwapGuard
{
    /// <summary>A read bracketed by these two sequence values saw the real (unswapped) bytes.</summary>
    public static bool IsConsistent(uint seqBefore, uint seqAfter) => seqBefore == seqAfter && (seqBefore & 1) == 0;

    /// <summary>
    /// The local player's equipped sword / shield ids, or null when a swap was in progress or
    /// raced the read (the caller keeps its last good value or skips this tick).
    /// </summary>
    public static (byte Sword, byte Shield)? ReadEquipped(IDolphinService dolphin)
    {
        if (ReadSeq(dolphin) is not uint before || (before & 1) != 0) return null;
        var bytes = dolphin.ReadMemory(GameMemoryAddresses.Player.CurrentSword.Address, 2);
        if (bytes == null || bytes.Length < 2) return null;
        if (ReadSeq(dolphin) is not uint after || !IsConsistent(before, after)) return null;
        return (bytes[0], bytes[1]);
    }

    /// <summary>
    /// Write one of the equip bytes, only while no swap is in progress. False if skipped (or the
    /// write failed) — the caller retries. A swap starting right after the check can still undo
    /// the write when it restores the byte, so callers re-read (seq-checked) and re-write once.
    /// </summary>
    public static bool TryWrite(IDolphinService dolphin, MemoryAddress<byte> equipByte, byte value)
    {
        if (ReadSeq(dolphin) is not uint seq || (seq & 1) != 0) return false;
        return dolphin.Write(equipByte, value);
    }

    private static uint? ReadSeq(IDolphinService dolphin)
    {
        var b = dolphin.ReadMemory(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 4);
        if (b == null || b.Length < 4) return null;
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }
}
