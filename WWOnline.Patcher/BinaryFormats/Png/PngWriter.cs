using System.IO.Compression;
using System.Text;

namespace WWOnline.Patcher.BinaryFormats.Png;

/// <summary>
/// Minimal PNG encoder for RGBA8 images (the item icons decoded from the player's game files).
/// Every PNG it writes carries a <c>tEXt</c> chunk right after <c>IHDR</c> with the keyword
/// <see cref="GameAssetKeyword"/>, at a fixed offset (<see cref="MarkerOffset"/>), so
/// <c>scripts/check-no-nintendo-files.ps1</c> can spot an extracted game image that ends up in the
/// repo or a release under any name.
/// </summary>
public static class PngWriter
{
    /// <summary>The tEXt keyword that marks a PNG as derived from the game's files.</summary>
    public const string GameAssetKeyword = "WWOnline-GameAsset";

    /// <summary>File offset of the marker chunk's type ("tEXt"): 8 (signature) + 25 (IHDR) + 4 (length).</summary>
    public const int MarkerOffset = 8 + 25 + 4;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Encode <paramref name="rgba"/> (width × height × 4 bytes, top row first) as a PNG.</summary>
    public static byte[] Encode(byte[] rgba, int width, int height, string markerText = "extracted from the player's own game files")
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0 || rgba.Length != width * height * 4)
            throw new ArgumentException("PNG: pixel data does not match the size");

        using var png = new MemoryStream();
        png.Write(Signature);

        var ihdr = new byte[13];
        BigEndianIO.WriteU32(ihdr, 0, (uint)width);
        BigEndianIO.WriteU32(ihdr, 4, (uint)height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // colour type: RGBA
        // compression 0, filter 0, interlace 0
        WriteChunk(png, "IHDR", ihdr);

        WriteChunk(png, "tEXt", Encoding.Latin1.GetBytes(GameAssetKeyword + "\0" + markerText));

        // Scanlines, each with filter type 0 (none), zlib-compressed.
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            int stride = width * 4;
            for (int y = 0; y < height; y++)
            {
                z.WriteByte(0);
                z.Write(rgba, y * stride, stride);
            }
        }
        WriteChunk(png, "IDAT", raw.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    /// <summary>True if <paramref name="png"/> starts with the PNG signature and our game-asset marker.</summary>
    public static bool HasGameAssetMarker(ReadOnlySpan<byte> png)
    {
        var keyword = Encoding.ASCII.GetBytes(GameAssetKeyword);
        int end = MarkerOffset + 4 + keyword.Length;
        return png.Length >= end
               && png[..8].SequenceEqual(Signature)
               && png.Slice(MarkerOffset, 4).SequenceEqual("tEXt"u8)
               && png.Slice(MarkerOffset + 4, keyword.Length).SequenceEqual(keyword);
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var header = new byte[8];
        BigEndianIO.WriteU32(header, 0, (uint)data.Length);
        Encoding.ASCII.GetBytes(type, 0, 4, header, 4);
        s.Write(header);
        s.Write(data);
        uint crc = Crc32(header.AsSpan(4, 4), 0xFFFFFFFF);
        crc = Crc32(data, crc) ^ 0xFFFFFFFF;
        var crcBytes = new byte[4];
        BigEndianIO.WriteU32(crcBytes, 0, crc);
        s.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> data, uint crc)
    {
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
