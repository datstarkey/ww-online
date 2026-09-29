using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Bti;
using Xunit;

namespace WWOnline.Patcher.Tests.BinaryFormats;

/// <summary>BTI decoding on small synthetic textures (never game data): one test per GX format.</summary>
public class BtiTextureTests
{
    /// <summary>A BTI: 0x20-byte header, the image at 0x20, then the palette (if any).</summary>
    private static byte[] Bti(GxTextureFormat format, int width, int height, byte[] image,
        GxPaletteFormat paletteFormat = GxPaletteFormat.RGB565, ushort[]? palette = null)
    {
        int paletteOffset = 0x20 + image.Length;
        var data = new byte[paletteOffset + (palette?.Length ?? 0) * 2];
        data[0x00] = (byte)format;
        BigEndianIO.WriteU16(data, 0x02, (ushort)width);
        BigEndianIO.WriteU16(data, 0x04, (ushort)height);
        if (palette != null)
        {
            data[0x08] = 1;
            data[0x09] = (byte)paletteFormat;
            BigEndianIO.WriteU16(data, 0x0A, (ushort)palette.Length);
            BigEndianIO.WriteU32(data, 0x0C, (uint)paletteOffset);
            for (int i = 0; i < palette.Length; i++) BigEndianIO.WriteU16(data, paletteOffset + i * 2, palette[i]);
        }
        BigEndianIO.WriteU32(data, 0x1C, 0x20);
        image.CopyTo(data, 0x20);
        return data;
    }

    private static byte[] BigEndian16(params ushort[] values)
    {
        var bytes = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++) BigEndianIO.WriteU16(bytes, i * 2, values[i]);
        return bytes;
    }

    private static (int R, int G, int B, int A) Pixel(byte[] rgba, int width, int x, int y)
    {
        int o = (y * width + x) * 4;
        return (rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]);
    }

    [Fact]
    public void Header_IsParsed()
    {
        var tex = new BtiTexture(Bti(GxTextureFormat.C8, 8, 4, new byte[32], GxPaletteFormat.RGB5A3, [0x8000, 0xFFFF]));
        Assert.Equal(GxTextureFormat.C8, tex.Format);
        Assert.Equal(8, tex.Width);
        Assert.Equal(4, tex.Height);
        Assert.True(tex.HasPalette);
        Assert.Equal(GxPaletteFormat.RGB5A3, tex.PaletteFormat);
        Assert.Equal(2, tex.PaletteCount);
    }

    [Fact]
    public void I4_TwoTexelsPerByte_HighNibbleFirst()
    {
        var image = new byte[32];
        image[0] = 0x0F; // texel 0 = 0, texel 1 = F
        image[31] = 0x80; // texel 62 = 8
        var rgba = new BtiTexture(Bti(GxTextureFormat.I4, 8, 8, image)).DecodeRgba();
        Assert.Equal((0, 0, 0, 0), Pixel(rgba, 8, 0, 0));
        Assert.Equal((255, 255, 255, 255), Pixel(rgba, 8, 1, 0));
        Assert.Equal((0x88, 0x88, 0x88, 0x88), Pixel(rgba, 8, 6, 7));
    }

    [Fact]
    public void I8_BlocksAre8x4_AndTileLeftToRight()
    {
        // 16x4: two 8x4 blocks side by side.
        var image = new byte[64];
        for (int i = 0; i < 64; i++) image[i] = (byte)i;
        var rgba = new BtiTexture(Bti(GxTextureFormat.I8, 16, 4, image)).DecodeRgba();
        Assert.Equal((11, 11, 11, 11), Pixel(rgba, 16, 3, 1));    // block 0, row 1, col 3
        Assert.Equal((32, 32, 32, 32), Pixel(rgba, 16, 8, 0));    // block 1 starts at x = 8
        Assert.Equal((63, 63, 63, 63), Pixel(rgba, 16, 15, 3));
    }

    [Fact]
    public void I8_PartialBlock_IsClippedToTheImage()
    {
        var image = new byte[32];
        for (int i = 0; i < 32; i++) image[i] = (byte)(i + 1);
        var rgba = new BtiTexture(Bti(GxTextureFormat.I8, 5, 3, image)).DecodeRgba();
        Assert.Equal(5 * 3 * 4, rgba.Length);
        Assert.Equal(8 * 2 + 4 + 1, Pixel(rgba, 5, 4, 2).R); // texel (4,2) is block index 20
    }

    [Fact]
    public void IA4_HighNibbleIsAlpha()
    {
        var image = new byte[32];
        image[0] = 0xA5;
        var rgba = new BtiTexture(Bti(GxTextureFormat.IA4, 8, 4, image)).DecodeRgba();
        Assert.Equal((0x55, 0x55, 0x55, 0xAA), Pixel(rgba, 8, 0, 0));
    }

    [Fact]
    public void IA8_AlphaThenIntensity()
    {
        var image = new byte[32];
        image[2] = 0x40; // texel 1: alpha
        image[3] = 0xC0; //          intensity
        var rgba = new BtiTexture(Bti(GxTextureFormat.IA8, 4, 4, image)).DecodeRgba();
        Assert.Equal((0xC0, 0xC0, 0xC0, 0x40), Pixel(rgba, 4, 1, 0));
    }

    [Fact]
    public void RGB565_ExpandsToEightBits()
    {
        var image = new byte[32];
        BigEndian16(0xF800, 0x07E0, 0x001F, 0x0000).CopyTo(image, 0);
        var rgba = new BtiTexture(Bti(GxTextureFormat.RGB565, 4, 4, image)).DecodeRgba();
        Assert.Equal((255, 0, 0, 255), Pixel(rgba, 4, 0, 0));
        Assert.Equal((0, 255, 0, 255), Pixel(rgba, 4, 1, 0));
        Assert.Equal((0, 0, 255, 255), Pixel(rgba, 4, 2, 0));
        Assert.Equal((0, 0, 0, 255), Pixel(rgba, 4, 3, 0));
    }

    [Fact]
    public void RGB5A3_OpaqueAndTranslucentModes()
    {
        var image = new byte[32];
        // 0xFC00: top bit set, RGB555 red. 0x3F00: alpha 3 (of 7), RGB444 red. 0x0000: fully transparent.
        BigEndian16(0xFC00, 0x3F00, 0x0000, 0x7FFF).CopyTo(image, 0);
        var rgba = new BtiTexture(Bti(GxTextureFormat.RGB5A3, 4, 4, image)).DecodeRgba();
        Assert.Equal((255, 0, 0, 255), Pixel(rgba, 4, 0, 0));
        Assert.Equal((255, 0, 0, 109), Pixel(rgba, 4, 1, 0)); // 3 → (3<<5)|(3<<2)|(3>>1)
        Assert.Equal((0, 0, 0, 0), Pixel(rgba, 4, 2, 0));
        Assert.Equal((255, 255, 255, 255), Pixel(rgba, 4, 3, 0));
    }

    [Fact]
    public void RGBA8_ArThenGbHalves()
    {
        var image = new byte[64];
        image[2] = 0x11; image[3] = 0x22;   // texel 1: A, R
        image[34] = 0x33; image[35] = 0x44; // texel 1: G, B
        var rgba = new BtiTexture(Bti(GxTextureFormat.RGBA8, 4, 4, image)).DecodeRgba();
        Assert.Equal((0x22, 0x33, 0x44, 0x11), Pixel(rgba, 4, 1, 0));
    }

    [Fact]
    public void C4_LooksUpThePalette()
    {
        var image = new byte[32];
        image[0] = 0x01; // texel 0 → entry 0, texel 1 → entry 1
        var rgba = new BtiTexture(Bti(GxTextureFormat.C4, 8, 8, image, GxPaletteFormat.RGB565, [0x001F, 0xF800])).DecodeRgba();
        Assert.Equal((0, 0, 255, 255), Pixel(rgba, 8, 0, 0));
        Assert.Equal((255, 0, 0, 255), Pixel(rgba, 8, 1, 0));
    }

    [Theory]
    [InlineData(GxPaletteFormat.RGB5A3, (ushort)0x3F00, 255, 0, 0, 109)]
    [InlineData(GxPaletteFormat.RGB565, (ushort)0x07E0, 0, 255, 0, 255)]
    [InlineData(GxPaletteFormat.IA8, (ushort)0x80C0, 0xC0, 0xC0, 0xC0, 0x80)] // alpha high byte, intensity low
    public void C8_PaletteFormats(GxPaletteFormat paletteFormat, ushort entry, int r, int g, int b, int a)
    {
        var image = new byte[32];
        image[5] = 1;
        var rgba = new BtiTexture(Bti(GxTextureFormat.C8, 8, 4, image, paletteFormat, [0, entry])).DecodeRgba();
        Assert.Equal((r, g, b, a), Pixel(rgba, 8, 5, 0));
    }

    [Fact]
    public void C8_IndexPastThePalette_IsTransparent()
    {
        var image = new byte[32];
        image[0] = 200;
        var rgba = new BtiTexture(Bti(GxTextureFormat.C8, 8, 4, image, GxPaletteFormat.RGB565, [0xFFFF])).DecodeRgba();
        Assert.Equal((0, 0, 0, 0), Pixel(rgba, 8, 0, 0));
    }

    [Fact]
    public void C14X2_UsesTheLow14Bits()
    {
        var image = new byte[32];
        BigEndian16(0xC001).CopyTo(image, 0);
        var rgba = new BtiTexture(Bti(GxTextureFormat.C14X2, 4, 4, image, GxPaletteFormat.RGB565, [0x0000, 0xF800])).DecodeRgba();
        Assert.Equal((255, 0, 0, 255), Pixel(rgba, 4, 0, 0));
    }

    [Fact]
    public void CMPR_FourColourMode()
    {
        // One 8x8 block = four 4x4 sub-blocks; only the first is filled. c0 (red) > c1 (blue).
        var image = new byte[32];
        BigEndian16(0xF800, 0x001F).CopyTo(image, 0);
        image[4] = 0b00_01_10_11; // row 0: c0, c1, 2/3 c0 + 1/3 c1, 1/3 c0 + 2/3 c1
        var rgba = new BtiTexture(Bti(GxTextureFormat.CMPR, 8, 8, image)).DecodeRgba();
        Assert.Equal((255, 0, 0, 255), Pixel(rgba, 8, 0, 0));
        Assert.Equal((0, 0, 255, 255), Pixel(rgba, 8, 1, 0));
        Assert.Equal((170, 0, 85, 255), Pixel(rgba, 8, 2, 0));
        Assert.Equal((85, 0, 170, 255), Pixel(rgba, 8, 3, 0));
    }

    [Fact]
    public void CMPR_ThreeColourMode_HasTransparentIndex3_AndSubBlocksTile2x2()
    {
        var image = new byte[32];
        // Sub-block 3 (bottom right, texels 4..7 x 4..7): c0 (blue) <= c1 (red).
        BigEndian16(0x001F, 0xF800).CopyTo(image, 24);
        image[28] = 0b10_11_00_01; // row 0 of the sub-block: half/half, transparent, c0, c1
        var rgba = new BtiTexture(Bti(GxTextureFormat.CMPR, 8, 8, image)).DecodeRgba();
        Assert.Equal((127, 0, 127, 255), Pixel(rgba, 8, 4, 4));
        Assert.Equal((0, 0, 0, 0), Pixel(rgba, 8, 5, 4));
        Assert.Equal((0, 0, 255, 255), Pixel(rgba, 8, 6, 4));
        Assert.Equal((255, 0, 0, 255), Pixel(rgba, 8, 7, 4));
    }

    [Fact]
    public void Truncated_Throws()
    {
        var data = Bti(GxTextureFormat.I8, 8, 4, new byte[32]);
        Assert.Throws<InvalidDataException>(() => new BtiTexture(data[..0x30]));
        Assert.Throws<InvalidDataException>(() => new BtiTexture(new byte[0x10]));
    }

    [Fact]
    public void UnknownFormat_Throws()
    {
        var data = Bti(GxTextureFormat.I8, 8, 4, new byte[32]);
        data[0] = 0x07;
        Assert.Throws<NotSupportedException>(() => new BtiTexture(data));
    }

    [Theory]
    [InlineData(GxTextureFormat.I4, 48, 48, 1152)]
    [InlineData(GxTextureFormat.C8, 48, 48, 2304)]
    [InlineData(GxTextureFormat.C8, 48, 51, 2496)] // boss_key-sized: 51 rows round up to 13 blocks of 4 (6 x 13 x 32)
    [InlineData(GxTextureFormat.CMPR, 10, 10, 128)]
    [InlineData(GxTextureFormat.RGBA8, 4, 4, 64)]
    public void ImageDataSize_RoundsUpToWholeBlocks(GxTextureFormat format, int w, int h, int expected) =>
        Assert.Equal(expected, BtiTexture.ImageDataSize(format, w, h));
}
