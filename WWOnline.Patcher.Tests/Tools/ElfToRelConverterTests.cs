using WWOnline.Patcher.BinaryFormats.Elf;
using WWOnline.Patcher.Tools;
using Xunit;

namespace WWOnline.Patcher.Tests.Tools;

public class ElfToRelConverterTests
{
    [Fact]
    public void GetSectionContents_NoBits_IsZeroFilledWithSectionSize()
    {
        // NOBITS sections have no file data; ElfSection.Read slices sh_size bytes from sh_offset,
        // which for .bss/.sbss is the start of the next section (e.g. .debug_info).
        var bss = new ElfSection
        {
            Name = ".bss",
            Type = ElfSectionType.SHT_NOBITS,
            Size = 8,
            Data = new byte[] { 0x00, 0x01, 0x6D, 0x09, 0x00, 0x05, 0x01, 0x04 },
        };

        var contents = ElfToRelConverter.GetSectionContents(bss);

        Assert.Equal(8, contents.Length);
        Assert.All(contents, b => Assert.Equal(0, b));
    }

    [Fact]
    public void GetSectionContents_ProgBits_IsCopiedNotAliased()
    {
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var text = new ElfSection
        {
            Name = ".data",
            Type = ElfSectionType.SHT_PROGBITS,
            Size = 4,
            Data = data,
        };

        var contents = ElfToRelConverter.GetSectionContents(text);

        Assert.Equal(data, contents);
        Assert.NotSame(data, contents);
    }
}
