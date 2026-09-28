namespace WWOnline.Patcher.BinaryFormats.Bmg;

/// <summary>
/// Minimal BMG message file (zel_00.bmg in files/res/Msg/bmgres.arc): reads the INF1 message
/// table and DAT1 string pool, lets callers change a message's initial draw type and raw text,
/// and rebuilds INF1/DAT1. Other sections are copied unchanged.
///
/// Text is kept as raw bytes: one byte per character, with control codes introduced by 0x1A
/// followed by the code's total length (e.g. 1A 07 00 00 07 xx xx = wait N frames).
///
/// Ported from wwlib/bmg.py in wwrando (LagoLunatic) / betterww (WideBoner), MIT.
/// </summary>
public sealed class BmgFile
{
    public const byte ControlCodeStart = 0x1A;

    private const int HeaderSize = 0x20;
    private const int Inf1EntriesOffset = 0x10;

    private readonly byte[] _header;
    private readonly List<(string Magic, byte[] Data)> _sections = [];
    private readonly int _inf1Index;
    private readonly int _dat1Index;
    private readonly int _entrySize;

    public List<BmgMessage> Messages { get; } = [];

    public BmgFile(byte[] data)
    {
        if (data.Length < HeaderSize || BigEndianIO.ReadStr(data, 0, 8) != "MESGbmg1")
            throw new InvalidDataException("Not a BMG file (missing MESGbmg1 magic)");
        _header = data[..HeaderSize];

        var count = BigEndianIO.ReadU32(data, 0x0C);
        int offset = HeaderSize;
        _inf1Index = _dat1Index = -1;
        for (int i = 0; i < count; i++)
        {
            var magic = BigEndianIO.ReadStr(data, offset, 4);
            var size = (int)BigEndianIO.ReadU32(data, offset + 4);
            _sections.Add((magic, data[offset..(offset + size)]));
            if (magic == "INF1") _inf1Index = i;
            if (magic == "DAT1") _dat1Index = i;
            offset += size;
        }
        if (_inf1Index < 0 || _dat1Index < 0)
            throw new InvalidDataException("BMG has no INF1/DAT1 section");

        var inf1 = _sections[_inf1Index].Data;
        var dat1 = _sections[_dat1Index].Data;
        var messageCount = BigEndianIO.ReadU16(inf1, 0x08);
        _entrySize = BigEndianIO.ReadU16(inf1, 0x0A);
        for (int i = 0; i < messageCount; i++)
        {
            var entry = inf1[(Inf1EntriesOffset + i * _entrySize)..(Inf1EntriesOffset + (i + 1) * _entrySize)];
            var stringOffset = (int)BigEndianIO.ReadU32(entry, 0);
            Messages.Add(new BmgMessage(entry, ReadString(dat1, 8 + stringOffset)));
        }
    }

    public BmgMessage? FindById(ushort messageId) => Messages.FirstOrDefault(m => m.MessageId == messageId);

    private static byte[] ReadString(byte[] dat1, int start)
    {
        int p = start;
        while (dat1[p] != 0)
            p += dat1[p] == ControlCodeStart ? dat1[p + 1] : 1;
        return dat1[start..p];
    }

    public byte[] Save()
    {
        // String pool: a leading NUL (offset 0 = empty string), then each message + NUL.
        using var pool = new MemoryStream();
        pool.WriteByte(0);
        var inf1 = _sections[_inf1Index].Data;
        var newInf1 = new byte[Inf1EntriesOffset + Messages.Count * _entrySize];
        Array.Copy(inf1, newInf1, Inf1EntriesOffset);
        BigEndianIO.WriteU16(newInf1, 0x08, (ushort)Messages.Count);
        for (int i = 0; i < Messages.Count; i++)
        {
            var m = Messages[i];
            BigEndianIO.WriteU32(m.Entry, 0, (uint)pool.Position);
            pool.Write(m.Text);
            pool.WriteByte(0);
            m.Entry.CopyTo(newInf1, Inf1EntriesOffset + i * _entrySize);
        }
        var newDat1 = new byte[8 + pool.Length];
        Array.Copy(_sections[_dat1Index].Data, newDat1, 8);
        pool.ToArray().CopyTo(newDat1, 8);

        using var output = new MemoryStream();
        output.Write(_header);
        for (int i = 0; i < _sections.Count; i++)
        {
            var (magic, data) = _sections[i];
            var body = i == _inf1Index ? newInf1 : i == _dat1Index ? newDat1 : data;
            var padded = new byte[(body.Length + 0x1F) & ~0x1F];
            body.CopyTo(padded, 0);
            BigEndianIO.WriteU32(padded, 4, (uint)padded.Length);
            output.Write(padded);
        }
        var result = output.ToArray();
        // 0x08 is the file size in 32-byte blocks in WW's BMGs; keep it true if it was.
        var oldBlocks = BigEndianIO.ReadU32(_header, 0x08);
        var oldSize = HeaderSize + _sections.Sum(s => s.Data.Length);
        if (oldBlocks == (uint)(oldSize / 0x20))
            BigEndianIO.WriteU32(result, 0x08, (uint)(result.Length / 0x20));
        else if (oldBlocks == (uint)oldSize)
            BigEndianIO.WriteU32(result, 0x08, (uint)result.Length);
        return result;
    }
}

/// <summary>One INF1 entry (raw, so unknown fields round-trip) plus its text bytes.</summary>
public sealed class BmgMessage
{
    internal byte[] Entry { get; }
    public byte[] Text { get; set; }

    internal BmgMessage(byte[] entry, byte[] text)
    {
        Entry = entry;
        Text = text;
    }

    public ushort MessageId => BigEndianIO.ReadU16(Entry, 0x04);

    /// <summary>0 = characters appear one by one, 1 = the whole box draws instantly.</summary>
    public byte InitialDrawType
    {
        get => Entry[0x0D];
        set => Entry[0x0D] = value;
    }

    /// <summary>Remove every control code whose bytes start with <paramref name="prefix"/> (prefix includes 0x1A and the length).</summary>
    public int RemoveControlCodes(ReadOnlySpan<byte> prefix)
    {
        using var output = new MemoryStream();
        int removed = 0, p = 0;
        while (p < Text.Length)
        {
            if (Text[p] == BmgFile.ControlCodeStart)
            {
                int len = Text[p + 1];
                if (Text.AsSpan(p).StartsWith(prefix) && len >= prefix.Length) { removed++; p += len; continue; }
                output.Write(Text, p, len);
                p += len;
            }
            else
            {
                output.WriteByte(Text[p]);
                p++;
            }
        }
        Text = output.ToArray();
        return removed;
    }
}
