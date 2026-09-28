using System.Buffers.Binary;
using System.Text;

namespace WWOnline.Patcher.BinaryFormats;

/// <summary>
/// Static helpers for reading and writing big-endian binary data.
/// Ported from the Python fs_helpers.py used by the original Python build scripts.
/// </summary>
public static class BigEndianIO
{
    /// <summary>
    /// Padding string used by align operations. Exactly 31 characters, repeated as needed.
    /// </summary>
    public static readonly byte[] PADDING_BYTES =
        Encoding.ASCII.GetBytes("This is padding data to alignme");

    private static readonly Encoding ShiftJis;

    static BigEndianIO()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJis = Encoding.GetEncoding("shift_jis");
    }

    // ----------------------------------------------------------------
    //  Read primitives from byte[]
    // ----------------------------------------------------------------

    public static byte ReadU8(byte[] data, int offset) => data[offset];

    public static ushort ReadU16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    public static uint ReadU32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    public static sbyte ReadS8(byte[] data, int offset) => (sbyte)data[offset];

    public static short ReadS16(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));

    public static int ReadS32(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));

    public static float ReadFloat(byte[] data, int offset) =>
        BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(offset, 4));

    // ----------------------------------------------------------------
    //  Read primitives from Stream
    // ----------------------------------------------------------------

    public static byte ReadU8(Stream stream, int offset)
    {
        stream.Position = offset;
        int b = stream.ReadByte();
        if (b < 0) throw new EndOfStreamException();
        return (byte)b;
    }

    public static ushort ReadU16(Stream stream, int offset)
    {
        Span<byte> buf = stackalloc byte[2];
        stream.Position = offset;
        stream.ReadExactly(buf);
        return BinaryPrimitives.ReadUInt16BigEndian(buf);
    }

    public static uint ReadU32(Stream stream, int offset)
    {
        Span<byte> buf = stackalloc byte[4];
        stream.Position = offset;
        stream.ReadExactly(buf);
        return BinaryPrimitives.ReadUInt32BigEndian(buf);
    }

    public static sbyte ReadS8(Stream stream, int offset) => (sbyte)ReadU8(stream, offset);

    public static short ReadS16(Stream stream, int offset)
    {
        Span<byte> buf = stackalloc byte[2];
        stream.Position = offset;
        stream.ReadExactly(buf);
        return BinaryPrimitives.ReadInt16BigEndian(buf);
    }

    public static int ReadS32(Stream stream, int offset)
    {
        Span<byte> buf = stackalloc byte[4];
        stream.Position = offset;
        stream.ReadExactly(buf);
        return BinaryPrimitives.ReadInt32BigEndian(buf);
    }

    public static float ReadFloat(Stream stream, int offset)
    {
        Span<byte> buf = stackalloc byte[4];
        stream.Position = offset;
        stream.ReadExactly(buf);
        return BinaryPrimitives.ReadSingleBigEndian(buf);
    }

    // ----------------------------------------------------------------
    //  Write primitives to byte[]
    // ----------------------------------------------------------------

    public static void WriteU8(byte[] data, int offset, byte value) =>
        data[offset] = value;

    public static void WriteU16(byte[] data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, 2), value);

    public static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);

    public static void WriteS8(byte[] data, int offset, sbyte value) =>
        data[offset] = (byte)value;

    public static void WriteS16(byte[] data, int offset, short value) =>
        BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(offset, 2), value);

    public static void WriteS32(byte[] data, int offset, int value) =>
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset, 4), value);

    public static void WriteFloat(byte[] data, int offset, float value) =>
        BinaryPrimitives.WriteSingleBigEndian(data.AsSpan(offset, 4), value);

    // ----------------------------------------------------------------
    //  Write primitives to Stream
    // ----------------------------------------------------------------

    public static void WriteU8(Stream stream, int offset, byte value)
    {
        stream.Position = offset;
        stream.WriteByte(value);
    }

    public static void WriteU16(Stream stream, int offset, ushort value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf, value);
        stream.Position = offset;
        stream.Write(buf);
    }

    public static void WriteU32(Stream stream, int offset, uint value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        stream.Position = offset;
        stream.Write(buf);
    }

    public static void WriteS8(Stream stream, int offset, sbyte value)
    {
        stream.Position = offset;
        stream.WriteByte((byte)value);
    }

    public static void WriteS16(Stream stream, int offset, short value)
    {
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buf, value);
        stream.Position = offset;
        stream.Write(buf);
    }

    public static void WriteS32(Stream stream, int offset, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);
        stream.Position = offset;
        stream.Write(buf);
    }

    public static void WriteFloat(Stream stream, int offset, float value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buf, value);
        stream.Position = offset;
        stream.Write(buf);
    }

    // ----------------------------------------------------------------
    //  Raw byte read/write on Stream
    // ----------------------------------------------------------------

    /// <summary>
    /// Reads <paramref name="length"/> bytes from the stream at the given offset.
    /// </summary>
    public static byte[] ReadBytes(Stream stream, int offset, int length)
    {
        stream.Position = offset;
        var buf = new byte[length];
        stream.ReadExactly(buf);
        return buf;
    }

    /// <summary>
    /// Writes raw bytes into the stream at the given offset.
    /// </summary>
    public static void WriteBytes(Stream stream, int offset, byte[] bytes)
    {
        stream.Position = offset;
        stream.Write(bytes);
    }

    /// <summary>
    /// Writes a portion of a byte array into the stream at the given offset.
    /// </summary>
    public static void WriteBytes(Stream stream, int offset, byte[] bytes, int srcOffset, int count)
    {
        stream.Position = offset;
        stream.Write(bytes, srcOffset, count);
    }

    /// <summary>
    /// Returns the total length of the stream.
    /// </summary>
    public static long DataLen(Stream stream) => stream.Length;

    /// <summary>
    /// Reads all bytes from a stream, resetting position to 0 first.
    /// </summary>
    public static byte[] ReadAllBytes(Stream stream)
    {
        stream.Position = 0;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    // ----------------------------------------------------------------
    //  String read/write (Shift-JIS encoding)
    // ----------------------------------------------------------------

    /// <summary>
    /// Reads a fixed-length Shift-JIS string from the stream, stripping trailing null bytes.
    /// </summary>
    public static string ReadStr(Stream stream, int offset, int length)
    {
        long dataLength = stream.Length;
        if (offset + length > dataLength)
            throw new InvalidOperationException(
                $"Offset 0x{offset:X}, length 0x{length:X} is past the end of the data (length 0x{dataLength:X}).");

        stream.Position = offset;
        var buf = new byte[length];
        stream.ReadExactly(buf);
        string s = ShiftJis.GetString(buf);
        return s.TrimEnd('\0');
    }

    /// <summary>
    /// Reads a fixed-length Shift-JIS string from a byte array, stripping trailing null bytes.
    /// </summary>
    public static string ReadStr(byte[] data, int offset, int length)
    {
        if (offset + length > data.Length)
            throw new InvalidOperationException(
                $"Offset 0x{offset:X}, length 0x{length:X} is past the end of the data (length 0x{data.Length:X}).");

        string s = ShiftJis.GetString(data, offset, length);
        return s.TrimEnd('\0');
    }

    /// <summary>
    /// Attempts to read a fixed-length Shift-JIS string. Returns null on decode error or out-of-bounds.
    /// </summary>
    public static string? TryReadStr(Stream stream, int offset, int length)
    {
        try
        {
            return ReadStr(stream, offset, length);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a null-terminated Shift-JIS string from the stream starting at the given offset.
    /// </summary>
    public static string ReadStrUntilNull(Stream stream, int offset)
    {
        long dataLength = stream.Length;
        if (offset > dataLength)
            throw new InvalidOperationException(
                $"Offset 0x{offset:X} is past the end of the data (length 0x{dataLength:X}).");

        int tempOffset = offset;
        int strLength = 0;
        while (tempOffset < dataLength)
        {
            stream.Position = tempOffset;
            int b = stream.ReadByte();
            if (b is 0 or -1)
                break;
            strLength++;
            tempOffset++;
        }

        var buf = new byte[strLength];
        stream.Position = offset;
        stream.ReadExactly(buf);
        return ShiftJis.GetString(buf);
    }

    /// <summary>
    /// Reads a null-terminated Shift-JIS string from a byte array starting at the given offset.
    /// </summary>
    public static string ReadStrUntilNull(byte[] data, int offset)
    {
        if (offset > data.Length)
            throw new InvalidOperationException(
                $"Offset 0x{offset:X} is past the end of the data (length 0x{data.Length:X}).");

        int strLength = 0;
        while (offset + strLength < data.Length && data[offset + strLength] != 0)
            strLength++;

        return ShiftJis.GetString(data, offset, strLength);
    }

    /// <summary>
    /// Writes a fixed-length null-terminated Shift-JIS string to a stream.
    /// The string must be shorter than <paramref name="maxLength"/> to leave room for the null terminator.
    /// </summary>
    public static void WriteStr(Stream stream, int offset, string value, int maxLength)
    {
        byte[] encoded = ShiftJis.GetBytes(value);
        if (encoded.Length >= maxLength)
            throw new ArgumentException(
                $"String \"{value}\" is too long (max length including null byte: 0x{maxLength:X})");

        int paddingLength = maxLength - encoded.Length;
        var buf = new byte[encoded.Length + paddingLength];
        encoded.CopyTo(buf, 0);
        // Remaining bytes are already zero-initialized

        stream.Position = offset;
        stream.Write(buf);
    }

    /// <summary>
    /// Writes a fixed-length Shift-JIS string that does not require a null terminator.
    /// Used for magic file format identifiers (e.g. "RARC", "Yaz0").
    /// </summary>
    public static void WriteMagicStr(Stream stream, int offset, string value, int maxLength)
    {
        byte[] encoded = ShiftJis.GetBytes(value);
        if (encoded.Length > maxLength)
            throw new ArgumentException(
                $"String \"{value}\" is too long (max length 0x{maxLength:X})");

        int paddingLength = maxLength - encoded.Length;
        var buf = new byte[encoded.Length + paddingLength];
        encoded.CopyTo(buf, 0);
        // Remaining bytes are already zero-initialized

        stream.Position = offset;
        stream.Write(buf);
    }

    /// <summary>
    /// Writes a variable-length Shift-JIS string followed by a single null byte.
    /// </summary>
    public static void WriteStrWithNullByte(Stream stream, int offset, string value)
    {
        byte[] encoded = ShiftJis.GetBytes(value);
        WriteStr(stream, offset, value, encoded.Length + 1);
    }

    // ----------------------------------------------------------------
    //  Alignment helpers
    // ----------------------------------------------------------------

    /// <summary>
    /// Pads the stream's data to the next multiple of <paramref name="size"/> using the
    /// <paramref name="paddingBytes"/> pattern (defaults to <see cref="PADDING_BYTES"/>).
    /// </summary>
    public static void AlignDataToNearest(Stream stream, int size, byte[]? paddingBytes = null)
    {
        paddingBytes ??= PADDING_BYTES;

        long currentEnd = stream.Length;
        long nextOffset = currentEnd + (size - currentEnd % size) % size;
        long paddingNeeded = nextOffset - currentEnd;

        if (paddingNeeded == 0)
            return;

        stream.Position = currentEnd;

        // Write full repetitions of the padding pattern
        long fullCopies = paddingNeeded / paddingBytes.Length;
        for (long i = 0; i < fullCopies; i++)
            stream.Write(paddingBytes);

        // Write the remaining partial copy
        int remainder = (int)(paddingNeeded % paddingBytes.Length);
        if (remainder > 0)
            stream.Write(paddingBytes, 0, remainder);
    }

    /// <summary>
    /// Returns the next offset aligned to the given <paramref name="size"/>.
    /// </summary>
    public static int PadOffsetToNearest(int offset, int size)
    {
        return offset + (size - offset % size) % size;
    }
}
