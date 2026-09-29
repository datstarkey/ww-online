using System.IO.Compression;
using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Bti;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.Icons;

namespace WWOnline.Patcher.Tests.Icons;

/// <summary>Made-up textures and archives in the game's formats (tests never use game data).</summary>
internal static class SyntheticIcons
{
    /// <summary>An 8x4 C8 BTI whose texels all use palette entry 1 (<paramref name="rgb5a3"/>).</summary>
    public static byte[] C8(ushort rgb5a3)
    {
        var data = new byte[0x20 + 32 + 4];
        data[0x00] = (byte)GxTextureFormat.C8;
        BigEndianIO.WriteU16(data, 0x02, 8);
        BigEndianIO.WriteU16(data, 0x04, 4);
        data[0x08] = 1;
        data[0x09] = (byte)GxPaletteFormat.RGB5A3;
        BigEndianIO.WriteU16(data, 0x0A, 2);
        BigEndianIO.WriteU32(data, 0x0C, 0x40);
        BigEndianIO.WriteU32(data, 0x1C, 0x20);
        for (int i = 0; i < 32; i++) data[0x20 + i] = 1;
        BigEndianIO.WriteU16(data, 0x42, rgb5a3);
        return data;
    }

    /// <summary>An 8x4 IA4 BTI, white and opaque (what the tinted icons look like).</summary>
    public static byte[] Ia4White()
    {
        var data = new byte[0x20 + 32];
        data[0x00] = (byte)GxTextureFormat.IA4;
        BigEndianIO.WriteU16(data, 0x02, 8);
        BigEndianIO.WriteU16(data, 0x04, 4);
        BigEndianIO.WriteU32(data, 0x1C, 0x20);
        for (int i = 0; i < 32; i++) data[0x20 + i] = 0xFF;
        return data;
    }

    /// <summary>A RARC holding the given files in a "timg" folder, like itemicon.arc.</summary>
    public static byte[] Archive(IEnumerable<(string Name, byte[] Data)> files)
    {
        var arc = new RarcArchive();
        var root = new RarcNode { Type = "ROOT", Name = "itemicon" };
        arc.Nodes.Add(root);
        foreach (var (name, data) in files) arc.AddNewFile(name, data, root);
        return arc.SaveChanges();
    }

    /// <summary>A fake extracted game folder with an itemicon.arc made of <paramref name="files"/>.</summary>
    public static string GameFolder(string parent, IEnumerable<(string Name, byte[] Data)> files)
    {
        var path = Path.Combine(parent, ItemIconCatalog.ArchiveRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Archive(files));
        return parent;
    }

    /// <summary>Decode a PNG written by PngWriter (8-bit RGBA, filter 0) back to RGBA.</summary>
    public static (int Width, int Height, byte[] Rgba) ReadPng(byte[] png)
    {
        int pos = 8, width = 0, height = 0;
        using var idat = new MemoryStream();
        while (pos < png.Length)
        {
            int length = (int)BigEndianIO.ReadU32(png, pos);
            string type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            if (type == "IHDR")
            {
                width = (int)BigEndianIO.ReadU32(png, pos + 8);
                height = (int)BigEndianIO.ReadU32(png, pos + 12);
            }
            else if (type == "IDAT") idat.Write(png, pos + 8, length);
            pos += 12 + length;
        }
        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        z.CopyTo(raw);
        var rows = raw.ToArray();
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            if (rows[y * (width * 4 + 1)] != 0) throw new InvalidDataException("unexpected PNG filter");
            Array.Copy(rows, y * (width * 4 + 1) + 1, rgba, y * width * 4, width * 4);
        }
        return (width, height, rgba);
    }
}
