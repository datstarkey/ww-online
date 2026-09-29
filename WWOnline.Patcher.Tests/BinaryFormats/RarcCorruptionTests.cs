using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.Tests.Icons;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

/// <summary>A corrupt RARC header must fail fast, never size an allocation from a bogus count.</summary>
public class RarcCorruptionTests
{
    private static byte[] Valid() => SyntheticIcons.Archive([("a.bti", SyntheticIcons.Ia4White())]);

    [Fact]
    public void Valid_Reads()
    {
        var arc = new RarcArchive();
        arc.Read(Valid());
        Assert.NotNull(arc.GetFileEntry("a.bti"));
    }

    [Theory]
    [InlineData(0x20)] // node count
    [InlineData(0x28)] // file entry count
    public void HugeCount_Throws(int countOffset)
    {
        var data = Valid();
        BigEndianIO.WriteU32(data, countOffset, 0x7FFFFFFF);
        Assert.Throws<InvalidDataException>(() => new RarcArchive().Read(data));
    }

    [Fact]
    public void FileDataPastTheEnd_Throws()
    {
        var data = Valid();
        var arc = new RarcArchive();
        arc.Read(data);
        var entry = arc.GetFileEntry("a.bti")!;
        BigEndianIO.WriteU32(data, entry.EntryOffset + 0x0C, 0x7FFFFFFF); // data size
        Assert.Throws<InvalidDataException>(() => new RarcArchive().Read(data));
    }
}
