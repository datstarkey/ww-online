using System.Buffers.Binary;

namespace WWOnline.Patcher.BinaryFormats.Dol;

/// <summary>
/// Handles patching the main.dol file. Ports apply_patches.py's DOLPatcher class.
/// </summary>
public class DolPatcher
{
    /// <summary>End of the vanilla DOL's bss (= end of .sbss2). DOL layout constant, not a scratch address.</summary>
    public const uint OriginalFreeSpaceRamAddress = 0x803FCFA8;

    /// <summary>Start of the vanilla DOL's .sbss2 (live game constants up to OriginalFreeSpaceRamAddress).</summary>
    public const uint Sbss2RamAddress = 0x803FCF20;

    /// <summary>
    /// Vanilla __ArenaLo (OSInit, 0x8030181C: lis/addi 0x8040EFC0). OSInit uses it as the arena start
    /// when BootInfo's arena_lo is 0 and the BI2 debug-flag path isn't taken. Otherwise the arena starts
    /// at the stack top rounded up to 32 bytes (_db_stack_end, which FinalizeHeaders moves). The moved
    /// boot stack must stay below this address, or the heap would start inside it.
    /// </summary>
    public const uint VanillaArenaLoAddress = 0x8040EFC0;

    /// <summary>Size of the boot-thread stack (vanilla 0x803FCFA8..0x8040CFA8).</summary>
    public const uint BootStackSize = 0x10000;

    private const int DolSectionCount = 18;
    public const int OriginalDolSize = 0x3A52C0;

    private byte[] _data;
    private int _text2SectionSize;
    private readonly Dictionary<string, uint> _freeSpaceStartOffsets;

    public DolPatcher(byte[] dolData)
    {
        _data = (byte[])dolData.Clone();
        _text2SectionSize = 0;
        _freeSpaceStartOffsets = new Dictionary<string, uint>();
    }

    public DolPatcher(string dolPath)
        : this(File.ReadAllBytes(dolPath))
    {
    }

    /// <summary>
    /// Load free space start offsets from a file (free_space_start_offsets.txt format).
    /// </summary>
    public void LoadFreeSpaceOffsets(string filePath)
    {
        foreach (var (path, offset) in Config.PatcherConfig.LoadFreeSpaceOffsets(filePath))
            _freeSpaceStartOffsets[path] = offset;
    }

    public uint ReadU32(uint memAddress)
    {
        var offset = MemToFile(memAddress);
        return BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset, 4));
    }

    public void WriteU32(uint memAddress, uint value)
    {
        var offset = MemToFile(memAddress);
        BinaryPrimitives.WriteUInt32BigEndian(_data.AsSpan(offset, 4), value);
    }

    /// <summary>
    /// Convert memory address to file offset.
    /// </summary>
    public int MemToFile(uint memAddress)
    {
        // Check Text0 section
        if (memAddress >= 0x80003100 && memAddress < 0x80005620)
            return 0x000100 + (int)(memAddress - 0x80003100);

        // Check Text1 section
        if (memAddress >= 0x800056E0 && memAddress < 0x80338680)
            return 0x002620 + (int)(memAddress - 0x800056E0);

        // Any other section the DOL header describes (.data, .rodata, .sdata2, ...)
        if (memAddress < OriginalFreeSpaceRamAddress && TryMapThroughHeader(memAddress, out var headerOffset))
            return headerOffset;

        // Check free space section
        if (memAddress >= OriginalFreeSpaceRamAddress)
            return OriginalDolSize + (int)(memAddress - OriginalFreeSpaceRamAddress);

        throw new InvalidOperationException($"Memory address 0x{memAddress:X8} not in any known DOL section");
    }

    /// <summary>Maps an address through the DOL header's text/data section table (skipping Text2, our free space).</summary>
    private bool TryMapThroughHeader(uint memAddress, out int fileOffset)
    {
        fileOffset = 0;
        if (_data.Length < 0x90 + DolSectionCount * 4) return false;
        for (int i = 0; i < DolSectionCount; i++)
        {
            if (i == 2) continue; // Text2 is the free-space section DolPatcher adds
            var offset = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(0x00 + i * 4, 4));
            var address = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(0x48 + i * 4, 4));
            var size = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(0x90 + i * 4, 4));
            if (size == 0 || memAddress < address || memAddress >= address + size) continue;
            fileOffset = (int)(offset + (memAddress - address));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Write bytes at a memory address, extending file if needed.
    /// </summary>
    public void WriteBytes(uint memAddress, byte[] data)
    {
        var fileOffset = MemToFile(memAddress);
        var endOffset = fileOffset + data.Length;

        const int MaxDolSize = 0x800000; // 8MB - more than enough for Wind Waker's DOL
        if (endOffset > MaxDolSize)
            throw new InvalidOperationException($"Patch would exceed maximum DOL size (0x{endOffset:X} > 0x{MaxDolSize:X}). Likely a bad address.");
        if (endOffset > _data.Length)
        {
            Array.Resize(ref _data, endOffset);
        }

        data.CopyTo(_data.AsSpan(fileOffset));
    }

    /// <summary>
    /// Apply patch data at a memory address.
    /// </summary>
    public void ApplyPatch(uint memAddress, byte[] data)
    {
        var freeSpaceStart = _freeSpaceStartOffsets.GetValueOrDefault("sys/main.dol", OriginalFreeSpaceRamAddress);

        if (memAddress >= freeSpaceStart)
        {
            AddToFreeSpace(memAddress, data);
        }
        else
        {
            WriteBytes(memAddress, data);
        }
    }

    private void AddToFreeSpace(uint memAddress, byte[] data)
    {
        if (data.Length == 0) return;

        var newTotalSectionSize = (int)(memAddress - OriginalFreeSpaceRamAddress) + data.Length;

        if (newTotalSectionSize > _text2SectionSize)
            _text2SectionSize = newTotalSectionSize;

        WriteBytes(memAddress, data);
    }

    private void FinalizeHeaders()
    {
        if (_text2SectionSize == 0) return;

        // Update DOL header for Text2 section (section index 2)
        const int sectionIdx = 2;

        // Set file offset for Text2 (at end of original DOL)
        BinaryPrimitives.WriteUInt32BigEndian(_data.AsSpan(0x00 + sectionIdx * 4, 4), (uint)OriginalDolSize);

        // Set memory address for Text2
        BinaryPrimitives.WriteUInt32BigEndian(_data.AsSpan(0x48 + sectionIdx * 4, 4), OriginalFreeSpaceRamAddress);

        // Set size for Text2
        BinaryPrimitives.WriteUInt32BigEndian(_data.AsSpan(0x90 + sectionIdx * 4, 4), (uint)_text2SectionSize);

        // Move the boot-thread stack above our code so the game doesn't overwrite it. The stack grows
        // down from _stack_addr to _stack_end, and __OSThreadInit writes the 0xDEADBABE stack magic at
        // _stack_end, so _stack_end must be past the last code byte. Pad to 8 bytes (wwrando pads to 4)
        // so the initial r1 keeps the 8-byte alignment the PPC EABI expects, like vanilla's 0x8040CFA8.
        var paddedSectionSize = (_text2SectionSize + 7) & ~7;
        var newStartPointer = OriginalFreeSpaceRamAddress + (uint)paddedSectionSize;

        // _stack_addr (initial r1, the stack top) = _stack_end + 0x10000. It is also _db_stack_end, which
        // OSInit rounds up to 32 for the arena start. It must stay below vanilla __ArenaLo, the other
        // arena start OSInit may use.
        var newEndPointer = newStartPointer + BootStackSize;
        if (newEndPointer > VanillaArenaLoAddress)
            throw new InvalidOperationException(
                $"main.dol free space is too big: the boot stack would end at 0x{newEndPointer:X8}, past the vanilla arena start " +
                $"0x{VanillaArenaLoAddress:X8}. Deselect some optional patches that use free space.");

        var (high, low) = SplitPointerForHardcoding(newStartPointer);

        // _stack_end in __OSThreadInit (DefaultThread.stackEnd, where the stack magic is written)
        WriteU32(0x80307954, 0x3C600000 | high);  // lis r3, high
        WriteU32(0x8030795C, 0x38030000 | low);   // addi r0, r3, low

        // _stack_addr in __OSThreadInit (DefaultThread.stackBase) and _db_stack_end in OSInit (arena start)
        (high, low) = SplitPointerForHardcoding(newEndPointer);

        WriteU32(0x8030794C, 0x3C600000 | high);
        WriteU32(0x80307950, 0x38030000 | low);
        WriteU32(0x80301854, 0x3C600000 | high);
        WriteU32(0x80301858, 0x38630000 | low);

        // These use different register/no HA adjustment
        high = (newEndPointer & 0xFFFF0000) >> 16;
        low = newEndPointer & 0xFFFF;
        WriteU32(0x80003278, 0x3C200000 | high);  // lis r1, high
        WriteU32(0x8000327C, 0x60210000 | low);   // ori r1, r1, low

        // Update Metro TRK pointer (0x12000 after thread start)
        var newMetroPointer = newStartPointer + 0x12000;
        high = (newMetroPointer & 0xFFFF0000) >> 16;
        low = newMetroPointer & 0xFFFF;
        WriteU32(0x803370A8, 0x3C200000 | high);
        WriteU32(0x803370AC, 0x60210000 | low);
    }

    /// <summary>
    /// Finalize and return the patched DOL data.
    /// </summary>
    public byte[] Save()
    {
        FinalizeHeaders();
        return (byte[])_data.Clone();
    }

    /// <summary>
    /// Finalize and save the patched DOL to a file.
    /// </summary>
    public void SaveToFile(string dolPath)
    {
        FinalizeHeaders();
        File.WriteAllBytes(dolPath, _data);
    }

    /// <summary>
    /// Split a 32-bit pointer into high and low halfwords for PPC instructions.
    /// </summary>
    public static (uint High, uint Low) SplitPointerForHardcoding(uint pointer)
    {
        var high = (pointer & 0xFFFF0000) >> 16;
        var low = pointer & 0xFFFF;

        // If low halfword has highest bit set, it's treated as negative by addi
        // So we add 1 to high halfword to compensate (HA adjustment)
        if (low >= 0x8000)
            high += 1;

        return (high, low);
    }
}
