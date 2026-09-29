using System.Text;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Png;
using WWOnline.Patcher.Tests.Icons;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

public class PngWriterTests
{
    private static byte[] Gradient(int w, int h)
    {
        var rgba = new byte[w * h * 4];
        for (int i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 7);
        return rgba;
    }

    [Fact]
    public void Encode_RoundTripsThePixels()
    {
        var rgba = Gradient(5, 3);
        var png = PngWriter.Encode(rgba, 5, 3);
        var (w, h, back) = SyntheticIcons.ReadPng(png);
        Assert.Equal((5, 3), (w, h));
        Assert.Equal(rgba, back);
    }

    [Fact]
    public void Encode_ChunkCrcsAreValid()
    {
        var png = PngWriter.Encode(Gradient(4, 4), 4, 4);
        int pos = 8, chunks = 0;
        while (pos < png.Length)
        {
            int length = (int)BigEndianIO.ReadU32(png, pos);
            uint stored = BigEndianIO.ReadU32(png, pos + 8 + length);
            Assert.Equal(Crc32(png.AsSpan(pos + 4, 4 + length)), stored);
            pos += 12 + length;
            chunks++;
        }
        Assert.Equal(png.Length, pos);
        Assert.Equal(4, chunks); // IHDR, tEXt, IDAT, IEND
    }

    [Fact]
    public void Encode_CarriesTheGameAssetMarker_WhereTheGuardLooks()
    {
        var png = PngWriter.Encode(Gradient(2, 2), 2, 2);
        Assert.True(PngWriter.HasGameAssetMarker(png));
        Assert.Equal("tEXt" + PngWriter.GameAssetKeyword,
            Encoding.ASCII.GetString(png, PngWriter.MarkerOffset, 4 + PngWriter.GameAssetKeyword.Length));
        // scripts/check-no-nintendo-files.ps1 reads the first 0x40 bytes.
        Assert.True(PngWriter.MarkerOffset + 4 + PngWriter.GameAssetKeyword.Length <= 0x40);
    }

    [Fact]
    public void HasGameAssetMarker_FalseForOtherData()
    {
        Assert.False(PngWriter.HasGameAssetMarker(new byte[64]));
        var png = PngWriter.Encode(Gradient(2, 2), 2, 2);
        png[PngWriter.MarkerOffset + 4] ^= 0xFF;
        Assert.False(PngWriter.HasGameAssetMarker(png));
    }

    [Fact]
    public void Encode_RejectsMismatchedSizes() =>
        Assert.Throws<ArgumentException>(() => PngWriter.Encode(new byte[10], 2, 2));

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }
}
