using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// A small game-heap block the puppet REL allocates once per boot, never frees and publishes at a
/// scratch word (puppet_nametag.c <c>puppet_bootBlock</c>): the names block and the live-world block.
/// Magic at +0, the boot's <c>__OSStartTime</c> at +<see cref="PuppetLayout.PUPPET_NAMES_OFF_BOOT"/>.
/// The client may use it whenever the pointer is in MEM1, the magic matches AND the boot stamp is this
/// boot's: a soft reset leaves the pointer and the old block's bytes (magic included) in RAM, but the
/// game heap is recreated, so a stale block is someone else's memory and must never be written.
/// </summary>
public static class BootStampedBlock
{
    /// <summary>MEM1 ends here with use_extra_memory.asm (48MB); a block must lie below it.</summary>
    public const uint Mem1End = 0x83000000;

    /// <summary>Is <paramref name="address"/> a plausible block of <paramref name="size"/> bytes (all in MEM1, word aligned)?</summary>
    public static bool IsValidAddress(uint address, int size) =>
        address >= 0x80000000 && address <= Mem1End - (uint)size && address % 4 == 0;

    /// <summary>
    /// The block published at <paramref name="pointerAddress"/> and its current bytes, or null when there
    /// is no usable block: no REL since boot, a bad pointer or magic, or a block from an earlier boot.
    /// </summary>
    public static (uint Address, byte[] Contents)? Read(IDolphinService dolphin, uint pointerAddress, uint magic, int size)
    {
        var ptr = dolphin.ReadMemory(pointerAddress, 4);
        if (ptr is not { Length: 4 })
            return null;
        uint block = ReadU32(ptr, 0);
        if (!IsValidAddress(block, size))
            return null;

        var contents = dolphin.ReadMemory(block, size);
        if (contents == null || contents.Length != size || ReadU32(contents, 0) != magic)
            return null;

        var bootTime = GameMemoryAddresses.System.OSStartTime;
        var boot = dolphin.ReadMemory(bootTime.Address, bootTime.Length);
        if (boot is not { Length: 8 } || boot.All(b => b == 0) ||
            !contents.AsSpan(PuppetLayout.PUPPET_NAMES_OFF_BOOT, 8).SequenceEqual(boot))
            return null;

        return (block, contents);
    }

    public static uint ReadU32(byte[] b, int offset) =>
        (uint)(b[offset] << 24 | b[offset + 1] << 16 | b[offset + 2] << 8 | b[offset + 3]);

    public static byte[] U32(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
}
