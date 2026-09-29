namespace WWOnline.Patcher.BinaryFormats.Bti;

/// <summary>GX texture formats (GXTexFmt). The values are what a BTI header's first byte holds.</summary>
public enum GxTextureFormat : byte
{
    I4 = 0x0,
    I8 = 0x1,
    IA4 = 0x2,
    IA8 = 0x3,
    RGB565 = 0x4,
    RGB5A3 = 0x5,
    RGBA8 = 0x6,
    C4 = 0x8,
    C8 = 0x9,
    C14X2 = 0xA,
    CMPR = 0xE,
}

/// <summary>GX palette (TLUT) formats (GXTlutFmt).</summary>
public enum GxPaletteFormat : byte
{
    IA8 = 0,
    RGB565 = 1,
    RGB5A3 = 2,
}

/// <summary>
/// A J3D / J2D BTI texture (<c>ResTIMG</c> in tww-decomp: <c>JSystem/JUtility/JUTTexture.h</c>): a 0x20-byte
/// header followed by GX-tiled image data and an optional palette. <see cref="DecodeRgba"/> turns
/// the top mip level into straight (non-premultiplied) RGBA8, row-major, top row first.
/// Only used to read textures from the player's own game files (the item icons); nothing here
/// writes a BTI.
/// </summary>
public sealed class BtiTexture
{
    public const int HeaderSize = 0x20;

    public GxTextureFormat Format { get; }
    public int Width { get; }
    public int Height { get; }
    public bool HasPalette { get; }
    public GxPaletteFormat PaletteFormat { get; }
    public int PaletteCount { get; }

    private readonly byte[] _data;
    private readonly int _imageOffset;
    private readonly int _paletteOffset;

    /// <summary>Parse the header at <paramref name="offset"/> (image/palette offsets are relative to it).</summary>
    public BtiTexture(byte[] data, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (offset < 0 || data.Length - offset < HeaderSize)
            throw new InvalidDataException("BTI: data is shorter than its 0x20-byte header");
        _data = data;

        Format = (GxTextureFormat)data[offset + 0x00];
        Width = BigEndianIO.ReadU16(data, offset + 0x02);
        Height = BigEndianIO.ReadU16(data, offset + 0x04);
        HasPalette = data[offset + 0x08] != 0;
        PaletteFormat = (GxPaletteFormat)data[offset + 0x09];
        PaletteCount = BigEndianIO.ReadU16(data, offset + 0x0A);
        _paletteOffset = offset + (int)BigEndianIO.ReadU32(data, offset + 0x0C);
        _imageOffset = offset + (int)BigEndianIO.ReadU32(data, offset + 0x1C);

        if (!Enum.IsDefined(Format))
            throw new NotSupportedException($"BTI: unknown texture format 0x{(byte)Format:X}");
        if (Width == 0 || Height == 0 || Width > 1024 || Height > 1024)
            throw new InvalidDataException($"BTI: bad size {Width}x{Height}");
        int need = ImageDataSize(Format, Width, Height);
        if (_imageOffset < offset || _imageOffset > data.Length - need)
            throw new InvalidDataException("BTI: image data runs past the end of the file");
        if (IsIndexed(Format))
        {
            if (!Enum.IsDefined(PaletteFormat))
                throw new NotSupportedException($"BTI: unknown palette format {(byte)PaletteFormat}");
            if (_paletteOffset < offset || _paletteOffset > data.Length - PaletteCount * 2)
                throw new InvalidDataException("BTI: palette runs past the end of the file");
        }
    }

    public static bool IsIndexed(GxTextureFormat f) => f is GxTextureFormat.C4 or GxTextureFormat.C8 or GxTextureFormat.C14X2;

    /// <summary>Block width, height (texels) and bytes per block of a GX format.</summary>
    public static (int W, int H, int Bytes) BlockInfo(GxTextureFormat f) => f switch
    {
        GxTextureFormat.I4 => (8, 8, 32),
        GxTextureFormat.I8 => (8, 4, 32),
        GxTextureFormat.IA4 => (8, 4, 32),
        GxTextureFormat.IA8 => (4, 4, 32),
        GxTextureFormat.RGB565 => (4, 4, 32),
        GxTextureFormat.RGB5A3 => (4, 4, 32),
        GxTextureFormat.RGBA8 => (4, 4, 64),
        GxTextureFormat.C4 => (8, 8, 32),
        GxTextureFormat.C8 => (8, 4, 32),
        GxTextureFormat.C14X2 => (4, 4, 32),
        GxTextureFormat.CMPR => (8, 8, 32),
        _ => throw new NotSupportedException($"GX format 0x{(byte)f:X}"),
    };

    /// <summary>Bytes of the top mip level (whole blocks: the image is padded up to the block size).</summary>
    public static int ImageDataSize(GxTextureFormat f, int width, int height)
    {
        var (bw, bh, bytes) = BlockInfo(f);
        return ((width + bw - 1) / bw) * ((height + bh - 1) / bh) * bytes;
    }

    /// <summary>The top mip level as RGBA8 (4 bytes per pixel, <see cref="Width"/> × <see cref="Height"/>).</summary>
    public byte[] DecodeRgba()
    {
        var rgba = new byte[Width * Height * 4];
        var palette = IsIndexed(Format) ? DecodePalette() : null;
        var (bw, bh, blockBytes) = BlockInfo(Format);
        int blocksX = (Width + bw - 1) / bw;
        int blocksY = (Height + bh - 1) / bh;
        int src = _imageOffset;

        for (int by = 0; by < blocksY; by++)
        for (int bx = 0; bx < blocksX; bx++)
        {
            DecodeBlock(src, bx * bw, by * bh, rgba, palette);
            src += blockBytes;
        }
        return rgba;
    }

    private void DecodeBlock(int src, int x0, int y0, byte[] rgba, uint[]? palette)
    {
        var d = _data;
        switch (Format)
        {
            case GxTextureFormat.I4:
                for (int i = 0; i < 64; i++)
                {
                    int nib = (d[src + i / 2] >> (i % 2 == 0 ? 4 : 0)) & 0xF;
                    byte v = (byte)(nib * 0x11);
                    Put(rgba, x0 + i % 8, y0 + i / 8, v, v, v, v);
                }
                break;
            case GxTextureFormat.I8:
                for (int i = 0; i < 32; i++)
                {
                    byte v = d[src + i];
                    Put(rgba, x0 + i % 8, y0 + i / 8, v, v, v, v);
                }
                break;
            case GxTextureFormat.IA4:
                for (int i = 0; i < 32; i++)
                {
                    byte b = d[src + i];
                    byte a = (byte)((b >> 4) * 0x11), v = (byte)((b & 0xF) * 0x11);
                    Put(rgba, x0 + i % 8, y0 + i / 8, v, v, v, a);
                }
                break;
            case GxTextureFormat.IA8:
                for (int i = 0; i < 16; i++)
                {
                    byte a = d[src + i * 2], v = d[src + i * 2 + 1];
                    Put(rgba, x0 + i % 4, y0 + i / 4, v, v, v, a);
                }
                break;
            case GxTextureFormat.RGB565:
                for (int i = 0; i < 16; i++)
                    Put(rgba, x0 + i % 4, y0 + i / 4, Rgb565(BigEndianIO.ReadU16(d, src + i * 2)));
                break;
            case GxTextureFormat.RGB5A3:
                for (int i = 0; i < 16; i++)
                    Put(rgba, x0 + i % 4, y0 + i / 4, Rgb5A3(BigEndianIO.ReadU16(d, src + i * 2)));
                break;
            case GxTextureFormat.RGBA8:
                // Two 32-byte halves: AR pairs, then GB pairs.
                for (int i = 0; i < 16; i++)
                {
                    byte a = d[src + i * 2], r = d[src + i * 2 + 1];
                    byte g = d[src + 32 + i * 2], b = d[src + 32 + i * 2 + 1];
                    Put(rgba, x0 + i % 4, y0 + i / 4, r, g, b, a);
                }
                break;
            case GxTextureFormat.C4:
                for (int i = 0; i < 64; i++)
                {
                    int idx = (d[src + i / 2] >> (i % 2 == 0 ? 4 : 0)) & 0xF;
                    Put(rgba, x0 + i % 8, y0 + i / 8, PaletteColor(palette!, idx));
                }
                break;
            case GxTextureFormat.C8:
                for (int i = 0; i < 32; i++)
                    Put(rgba, x0 + i % 8, y0 + i / 8, PaletteColor(palette!, d[src + i]));
                break;
            case GxTextureFormat.C14X2:
                for (int i = 0; i < 16; i++)
                    Put(rgba, x0 + i % 4, y0 + i / 4, PaletteColor(palette!, BigEndianIO.ReadU16(d, src + i * 2) & 0x3FFF));
                break;
            case GxTextureFormat.CMPR:
                // 2×2 sub-blocks of 4×4 texels, 8 bytes each (DXT1 with big-endian colours and 2-bit
                // indices packed most significant first).
                for (int sub = 0; sub < 4; sub++)
                    DecodeCmprSubBlock(src + sub * 8, x0 + (sub % 2) * 4, y0 + (sub / 2) * 4, rgba);
                break;
        }
    }

    private void DecodeCmprSubBlock(int src, int x0, int y0, byte[] rgba)
    {
        ushort c0 = BigEndianIO.ReadU16(_data, src), c1 = BigEndianIO.ReadU16(_data, src + 2);
        var colors = new uint[4];
        colors[0] = Rgb565(c0);
        colors[1] = Rgb565(c1);
        if (c0 > c1)
        {
            colors[2] = Mix(colors[0], colors[1], 2, 1, 3);
            colors[3] = Mix(colors[0], colors[1], 1, 2, 3);
        }
        else
        {
            colors[2] = Mix(colors[0], colors[1], 1, 1, 2);
            colors[3] = 0; // transparent black
        }
        for (int row = 0; row < 4; row++)
        {
            byte bits = _data[src + 4 + row];
            for (int col = 0; col < 4; col++)
                Put(rgba, x0 + col, y0 + row, colors[(bits >> (6 - col * 2)) & 3]);
        }
    }

    private uint[] DecodePalette()
    {
        var pal = new uint[PaletteCount];
        for (int i = 0; i < PaletteCount; i++)
        {
            ushort v = BigEndianIO.ReadU16(_data, _paletteOffset + i * 2);
            pal[i] = PaletteFormat switch
            {
                GxPaletteFormat.IA8 => Pack((byte)(v & 0xFF), (byte)(v & 0xFF), (byte)(v & 0xFF), (byte)(v >> 8)),
                GxPaletteFormat.RGB565 => Rgb565(v),
                _ => Rgb5A3(v),
            };
        }
        return pal;
    }

    private static uint PaletteColor(uint[] palette, int index) =>
        index < palette.Length ? palette[index] : 0;

    // ── Colour helpers: a colour is packed as 0xAABBGGRR (r in the low byte). ──

    private static uint Pack(byte r, byte g, byte b, byte a) => r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);

    private static byte Expand5(int v) => (byte)((v << 3) | (v >> 2));
    private static byte Expand6(int v) => (byte)((v << 2) | (v >> 4));
    private static byte Expand4(int v) => (byte)(v * 0x11);
    private static byte Expand3(int v) => (byte)((v << 5) | (v << 2) | (v >> 1));

    public static uint Rgb565(ushort v) =>
        Pack(Expand5((v >> 11) & 0x1F), Expand6((v >> 5) & 0x3F), Expand5(v & 0x1F), 0xFF);

    /// <summary>Top bit set: RGB555, opaque. Clear: 3-bit alpha + RGB444.</summary>
    public static uint Rgb5A3(ushort v) => (v & 0x8000) != 0
        ? Pack(Expand5((v >> 10) & 0x1F), Expand5((v >> 5) & 0x1F), Expand5(v & 0x1F), 0xFF)
        : Pack(Expand4((v >> 8) & 0xF), Expand4((v >> 4) & 0xF), Expand4(v & 0xF), Expand3((v >> 12) & 0x7));

    private static uint Mix(uint a, uint b, int wa, int wb, int div)
    {
        uint result = 0xFF000000;
        for (int shift = 0; shift < 24; shift += 8)
        {
            int ca = (int)((a >> shift) & 0xFF), cb = (int)((b >> shift) & 0xFF);
            result |= (uint)((ca * wa + cb * wb) / div) << shift;
        }
        return result;
    }

    private void Put(byte[] rgba, int x, int y, uint c) =>
        Put(rgba, x, y, (byte)c, (byte)(c >> 8), (byte)(c >> 16), (byte)(c >> 24));

    private void Put(byte[] rgba, int x, int y, byte r, byte g, byte b, byte a)
    {
        if (x >= Width || y >= Height) return; // padding texels of a partial block
        int o = (y * Width + x) * 4;
        rgba[o] = r;
        rgba[o + 1] = g;
        rgba[o + 2] = b;
        rgba[o + 3] = a;
    }
}
