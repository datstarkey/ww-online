using WWOnline.Patcher.BinaryFormats;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

public class BigEndianIOTests
{
    [Fact]
    public void ReadU32_BigEndian()
    {
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x80, 0x10, 0x82, 0x04 };
        Assert.Equal(0x00010203u, BigEndianIO.ReadU32(data, 0));
        Assert.Equal(0x80108204u, BigEndianIO.ReadU32(data, 4));
    }

    [Fact]
    public void WriteU32_BigEndian()
    {
        var data = new byte[8];
        BigEndianIO.WriteU32(data, 0, 0xDEADBEEF);
        Assert.Equal(0xDE, data[0]);
        Assert.Equal(0xAD, data[1]);
        Assert.Equal(0xBE, data[2]);
        Assert.Equal(0xEF, data[3]);
    }

    [Fact]
    public void ReadU16_BigEndian()
    {
        var data = new byte[] { 0x12, 0x34, 0xAB, 0xCD };
        Assert.Equal((ushort)0x1234, BigEndianIO.ReadU16(data, 0));
        Assert.Equal((ushort)0xABCD, BigEndianIO.ReadU16(data, 2));
    }

    [Fact]
    public void WriteU16_BigEndian()
    {
        var data = new byte[4];
        BigEndianIO.WriteU16(data, 0, 0xCAFE);
        Assert.Equal(0xCA, data[0]);
        Assert.Equal(0xFE, data[1]);
    }

    [Fact]
    public void ReadU8()
    {
        var data = new byte[] { 0xFF, 0x42 };
        Assert.Equal((byte)0xFF, BigEndianIO.ReadU8(data, 0));
        Assert.Equal((byte)0x42, BigEndianIO.ReadU8(data, 1));
    }

    [Fact]
    public void PadOffsetToNearest()
    {
        Assert.Equal(0, BigEndianIO.PadOffsetToNearest(0, 4));
        Assert.Equal(4, BigEndianIO.PadOffsetToNearest(1, 4));
        Assert.Equal(4, BigEndianIO.PadOffsetToNearest(4, 4));
        Assert.Equal(0x20, BigEndianIO.PadOffsetToNearest(0x1F, 0x20));
        Assert.Equal(0x20, BigEndianIO.PadOffsetToNearest(0x20, 0x20));
    }

    [Fact]
    public void PaddingBytes_HasCorrectContent()
    {
        var expected = System.Text.Encoding.ASCII.GetBytes("This is padding data to alignme");
        Assert.Equal(31, BigEndianIO.PADDING_BYTES.Length);
        Assert.Equal(expected, BigEndianIO.PADDING_BYTES);
    }

    [Fact]
    public void AlignDataToNearest_PadsCorrectly()
    {
        var stream = new MemoryStream();
        stream.Write(new byte[5]); // 5 bytes written

        BigEndianIO.AlignDataToNearest(stream, 8);

        Assert.Equal(8, stream.Length); // Padded to 8
    }

    [Fact]
    public void ReadS16_BigEndian()
    {
        var data = new byte[] { 0xFF, 0xFE }; // -2 in big-endian
        Assert.Equal((short)-2, BigEndianIO.ReadS16(data, 0));
    }

    [Fact]
    public void ReadFloat_BigEndian()
    {
        // 42C80000 = 100.0f in IEEE 754
        var data = new byte[] { 0x42, 0xC8, 0x00, 0x00 };
        Assert.Equal(100.0f, BigEndianIO.ReadFloat(data, 0));
    }
}
