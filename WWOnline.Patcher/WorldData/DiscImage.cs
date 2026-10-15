using WWOnline.Patcher.BinaryFormats;

namespace WWOnline.Patcher.WorldData;

/// <summary>
/// A GameCube disc image (a plain GCM / ISO, as the native backend reads it), read-only: main.dol and the files in
/// its FST by path ("res/Stage/sea/Stage.arc", the extracted game's files/ folder). For data the patcher reads from
/// an extracted game, when there is only the disc.
/// </summary>
public sealed class DiscImage : IDisposable
{
    private const uint GameCubeMagic = 0xC2339F3D; // disc header +0x1C
    private const int HeaderSize = 0x440;
    private const int DolHeaderSize = 0x100;

    private readonly FileStream _stream;
    private readonly uint _dolOffset;
    private readonly Dictionary<string, (uint Offset, uint Size)> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { "" };

    private DiscImage(FileStream stream, byte[] header)
    {
        _stream = stream;
        _dolOffset = BigEndianIO.ReadU32(header, 0x420);
        var fst = Read(BigEndianIO.ReadU32(header, 0x424), BigEndianIO.ReadU32(header, 0x428));
        ReadDirectory(fst, 0, "");
    }

    /// <summary>The disc at <paramref name="path"/>, or null when it isn't a plain GameCube disc image.</summary>
    public static DiscImage? Open(string path)
    {
        FileStream? stream = null;
        try
        {
            stream = File.OpenRead(path);
            var header = new byte[HeaderSize];
            if (stream.Read(header) != header.Length || BigEndianIO.ReadU32(header, 0x1C) != GameCubeMagic)
            {
                stream.Dispose();
                return null;
            }
            return new DiscImage(stream, header);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or
                                       ArgumentException or IndexOutOfRangeException)
        {
            stream?.Dispose();
            return null;
        }
    }

    /// <summary>The directories directly in <paramref name="directory"/> ("" = the root), as full paths.</summary>
    public IEnumerable<string> Directories(string directory) => _directories.Where(d => d.Length > 0 && Parent(d) == directory);

    /// <summary>The files directly in <paramref name="directory"/>, as full paths.</summary>
    public IEnumerable<string> Files(string directory) => _files.Keys.Where(f => Parent(f) == directory);

    /// <summary>A file's bytes, or null when the disc has no such file.</summary>
    public byte[]? ReadFile(string path) => _files.TryGetValue(path, out var f) ? Read(f.Offset, f.Size) : null;

    /// <summary>main.dol, as long as its header says.</summary>
    public byte[] ReadDol()
    {
        var header = Read(_dolOffset, DolHeaderSize);
        uint end = DolHeaderSize;
        for (int i = 0; i < 18; i++) // 7 text and 11 data sections: offsets at 0x00, sizes at 0x90
            end = Math.Max(end, BigEndianIO.ReadU32(header, i * 4) + BigEndianIO.ReadU32(header, 0x90 + i * 4));
        return Read(_dolOffset, end);
    }

    public void Dispose() => _stream.Dispose();

    private static string Parent(string path) => path.LastIndexOf('/') is var slash and >= 0 ? path[..slash] : "";

    // FST entries are 12 bytes: flags (1 = directory) and a 24-bit name offset into the string table that follows
    // them; a file's disc offset and size, or a directory's parent and the index just past its last entry.
    private void ReadDirectory(byte[] fst, int dir, string path)
    {
        int end = (int)BigEndianIO.ReadU32(fst, dir * 12 + 8);
        int strings = (int)BigEndianIO.ReadU32(fst, 8) * 12; // the root's end = the entry count
        for (int i = dir + 1; i < end;)
        {
            int e = i * 12;
            string name = BigEndianIO.ReadStrUntilNull(fst, strings + (int)(BigEndianIO.ReadU32(fst, e) & 0xFFFFFF));
            string full = path.Length == 0 ? name : path + "/" + name;
            if (fst[e] != 0)
            {
                _directories.Add(full);
                ReadDirectory(fst, i, full);
                i = (int)BigEndianIO.ReadU32(fst, e + 8);
            }
            else
            {
                _files[full] = (BigEndianIO.ReadU32(fst, e + 4), BigEndianIO.ReadU32(fst, e + 8));
                i++;
            }
        }
    }

    private byte[] Read(uint offset, uint size)
    {
        if (offset + (long)size > _stream.Length)
            throw new InvalidDataException($"disc read past its end (0x{offset:X} + 0x{size:X})");
        var data = new byte[size];
        _stream.Position = offset;
        _stream.ReadExactly(data);
        return data;
    }
}
