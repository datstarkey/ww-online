using WWOnline.Patcher.BinaryFormats.Yaz0;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

public class Yaz0CodecTests
{
    [Fact]
    public void CheckIsCompressed_ReturnsTrueForYaz0Data()
    {
        var data = new byte[] { 0x59, 0x61, 0x7A, 0x30, 0x00, 0x00, 0x00, 0x10 }; // "Yaz0" + size
        Assert.True(Yaz0Codec.CheckIsCompressed(data));
    }

    [Fact]
    public void CheckIsCompressed_ReturnsFalseForNonYaz0Data()
    {
        var data = new byte[] { 0x52, 0x41, 0x52, 0x43 }; // "RARC"
        Assert.False(Yaz0Codec.CheckIsCompressed(data));
    }

    [Fact]
    public void CheckIsCompressed_ReturnsFalseForShortData()
    {
        var data = new byte[] { 0x59, 0x61 }; // Too short
        Assert.False(Yaz0Codec.CheckIsCompressed(data));
    }

    [Fact]
    public void Decompress_PassesThroughUncompressedData()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var result = Yaz0Codec.Decompress(data);
        Assert.Equal(data, result);
    }

    [Fact]
    public void CompressDecompress_RoundTrip()
    {
        // Create some test data with repetition (good for compression)
        var original = new byte[256];
        for (var i = 0; i < original.Length; i++)
            original[i] = (byte)(i % 16);

        var codec = new Yaz0Codec();
        var compressed = codec.Compress(original);

        Assert.True(Yaz0Codec.CheckIsCompressed(compressed));

        var decompressed = Yaz0Codec.Decompress(compressed);
        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void CompressDecompress_AllZeros()
    {
        var original = new byte[1024];

        var codec = new Yaz0Codec();
        var compressed = codec.Compress(original);
        var decompressed = Yaz0Codec.Decompress(compressed);

        Assert.Equal(original, decompressed);
        Assert.True(compressed.Length < original.Length); // Should compress well
    }

    [Fact]
    public void CompressDecompress_SmallData()
    {
        var original = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        var codec = new Yaz0Codec();
        var compressed = codec.Compress(original);
        var decompressed = Yaz0Codec.Decompress(compressed);

        Assert.Equal(original, decompressed);
    }

    [Fact]
    public void Compress_WithPadding()
    {
        var original = new byte[] { 0x01, 0x02, 0x03 };
        var codec = new Yaz0Codec();
        var compressed = codec.Compress(original, shouldPadData: true);

        // Should be padded to 0x20 alignment
        Assert.Equal(0, compressed.Length % 0x20);

        var decompressed = Yaz0Codec.Decompress(compressed);
        Assert.Equal(original, decompressed);
    }
}
