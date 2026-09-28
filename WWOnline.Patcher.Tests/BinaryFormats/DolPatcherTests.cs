using WWOnline.Patcher.BinaryFormats.Dol;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

public class DolPatcherTests
{
    [Theory]
    [InlineData(0x80003100u, 0x000100)]
    [InlineData(0x800056E0u, 0x002620)]
    [InlineData(0x80108204u, 0x105144)] // daPy_Draw
    [InlineData(0x80286000u, 0x282F40)] // Our code injection point (0x002620 + 0x80286000 - 0x800056E0)
    public void MemToFile_CorrectMapping(uint memAddress, int expectedFileOffset)
    {
        var data = new byte[DolPatcher.OriginalDolSize + 0x10000];
        var patcher = new DolPatcher(data);
        Assert.Equal(expectedFileOffset, patcher.MemToFile(memAddress));
    }

    [Fact]
    public void MemToFile_FreeSpace_CorrectMapping()
    {
        var data = new byte[DolPatcher.OriginalDolSize + 0x10000];
        var patcher = new DolPatcher(data);

        // Free space starts at OriginalFreeSpaceRamAddress -> file end
        Assert.Equal(DolPatcher.OriginalDolSize,
            patcher.MemToFile(DolPatcher.OriginalFreeSpaceRamAddress));
    }

    [Fact]
    public void MemToFile_DataSectionsComeFromTheDolHeader()
    {
        var data = new byte[DolPatcher.OriginalDolSize];
        // Data section 4 (header index 11), like the vanilla .data at 0x80338840.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x00 + 11 * 4), 0x335840);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x48 + 11 * 4), 0x80338840);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(0x90 + 11 * 4), 0x38D40);
        var patcher = new DolPatcher(data);

        Assert.Equal(0x335840 + 0x2535C, patcher.MemToFile(0x8035DB9C));
        Assert.Throws<InvalidOperationException>(() => patcher.MemToFile(0x803F9D60)); // not described by this header
    }

    [Fact]
    public void SplitPointerForHardcoding_NoAdjustment()
    {
        var (high, low) = DolPatcher.SplitPointerForHardcoding(0x80400000);
        Assert.Equal(0x8040u, high);
        Assert.Equal(0x0000u, low);
    }

    [Fact]
    public void SplitPointerForHardcoding_WithHaAdjustment()
    {
        // When low >= 0x8000, addi treats it as negative, so high needs +1
        var (high, low) = DolPatcher.SplitPointerForHardcoding(0x8040FFFF);
        Assert.Equal(0x8041u, high); // +1 adjustment
        Assert.Equal(0xFFFFu, low);
    }

    [Fact]
    public void ApplyPatch_WritesToCorrectLocation()
    {
        var data = new byte[DolPatcher.OriginalDolSize];
        var patcher = new DolPatcher(data);
        var patchData = new byte[] { 0x48, 0x00, 0x00, 0x01 };

        // Write a branch instruction at daPy_Draw
        patcher.ApplyPatch(0x80108204, patchData);

        var result = patcher.Save();
        var fileOffset = patcher.MemToFile(0x80108204);

        Assert.Equal(0x48, result[fileOffset]);
        Assert.Equal(0x00, result[fileOffset + 1]);
        Assert.Equal(0x00, result[fileOffset + 2]);
        Assert.Equal(0x01, result[fileOffset + 3]);
    }

    [Fact]
    public void ApplyPatch_FreeSpace_ExtendsFile()
    {
        var data = new byte[DolPatcher.OriginalDolSize];
        var patcher = new DolPatcher(data);
        var patchData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        patcher.ApplyPatch(DolPatcher.OriginalFreeSpaceRamAddress, patchData);

        var result = patcher.Save();
        Assert.True(result.Length > DolPatcher.OriginalDolSize);
    }

    /// <summary>
    /// Free-space code after the scratch block (0x803FD200, SCRATCH_REGION_END) moves the boot-thread
    /// stack above the code. The stack magic word (_stack_end) sits past the last code byte, r1 stays
    /// 8-byte aligned, the arena start (_db_stack_end) is the stack top, and the scratch block is
    /// loaded as zeros.
    /// </summary>
    [Theory]
    [InlineData(4)]
    [InlineData(0x244)]  // the default bug fixes: code end not 8-aligned
    [InlineData(0x3F8)]  // every optional patch
    public void FreeSpace_MovesBootStackAboveTheCode(int codeLength)
    {
        const uint codeStart = 0x803FD200;
        var patcher = new DolPatcher(new byte[DolPatcher.OriginalDolSize]);
        patcher.ApplyPatch(codeStart, Enumerable.Repeat((byte)0x60, codeLength).ToArray());
        var result = new DolPatcher(patcher.Save());

        uint Pair(uint lisAddress, uint addiAddress) =>
            (result.ReadU32(lisAddress) << 16) + (uint)(short)(result.ReadU32(addiAddress) & 0xFFFF);

        var stackEnd = Pair(0x80307954, 0x8030795C);   // _stack_end: where 0xDEADBABE is written
        var stackTop = Pair(0x8030794C, 0x80307950);   // _stack_addr
        var dbStackEnd = Pair(0x80301854, 0x80301858); // OSInit arena start (rounded up to 32)
        var r1 = (result.ReadU32(0x80003278) << 16) | (result.ReadU32(0x8000327C) & 0xFFFF);

        Assert.True(stackEnd >= codeStart + (uint)codeLength, $"stack end 0x{stackEnd:X8} overlaps the code");
        Assert.Equal(0u, stackEnd % 8);
        Assert.Equal(stackEnd + DolPatcher.BootStackSize, stackTop);
        Assert.Equal(stackTop, r1);
        Assert.Equal(stackTop, dbStackEnd);
        Assert.True(stackTop <= DolPatcher.VanillaArenaLoAddress);

        // Text2 covers the scratch block, which is loaded as zeros.
        var bytes = patcher.Save();
        Assert.Equal(DolPatcher.OriginalFreeSpaceRamAddress, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0x48 + 2 * 4)));
        Assert.All(bytes[DolPatcher.OriginalDolSize..(DolPatcher.OriginalDolSize + (int)(codeStart - DolPatcher.OriginalFreeSpaceRamAddress))],
            b => Assert.Equal(0, b));
    }

    [Fact]
    public void FreeSpace_TooBigForTheArena_Throws()
    {
        var patcher = new DolPatcher(new byte[DolPatcher.OriginalDolSize]);
        patcher.ApplyPatch(0x803FD200, new byte[0x2000]);
        Assert.Throws<InvalidOperationException>(() => patcher.Save());
    }
}
