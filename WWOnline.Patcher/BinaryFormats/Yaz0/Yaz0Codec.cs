using System.Buffers.Binary;
using System.Text;

namespace WWOnline.Patcher.BinaryFormats.Yaz0;

/// <summary>
/// Yaz0 compression and decompression codec.
/// Port of the Python yaz0.py implementation (LagoLunatic / wwrando).
/// </summary>
public class Yaz0Codec
{
    public const int MaxRunLength = 0xFF + 0x12; // 273
    public const int DefaultSearchDepth = 0x1000;

    /// <summary>Largest decompressed size <see cref="Decompress"/> accepts (64 MB).</summary>
    public const int MaxDecompressedSize = 64 * 1024 * 1024;

    private static readonly byte[] Yaz0Magic = "Yaz0"u8.ToArray();

    // Lookahead state for compression (reset each call to Compress).
    private int _nextNumBytes;
    private int _nextMatchPos;
    private bool _nextFlag;

    /// <summary>
    /// Checks whether the data starts with the "Yaz0" magic bytes.
    /// </summary>
    public static bool CheckIsCompressed(byte[] data)
    {
        if (data.Length < 4)
            return false;

        return data[0] == Yaz0Magic[0]
            && data[1] == Yaz0Magic[1]
            && data[2] == Yaz0Magic[2]
            && data[3] == Yaz0Magic[3];
    }

    /// <summary>
    /// Decompresses Yaz0-encoded data. Returns the original data unchanged if not Yaz0-compressed.
    /// </summary>
    public static byte[] Decompress(byte[] compData) => Decompress(compData, int.MaxValue);

    /// <summary>
    /// Decompresses only the first <paramref name="maxLength"/> bytes (or all, if the data is shorter):
    /// enough to read an archive's header and one early file without inflating the whole thing.
    /// Returns the original data unchanged if not Yaz0-compressed.
    /// </summary>
    public static byte[] Decompress(byte[] compData, int maxLength)
    {
        if (!CheckIsCompressed(compData))
            return compData;
        if (compData.Length < 0x10)
            throw new InvalidDataException("Yaz0: data is shorter than its 0x10-byte header");

        // The header's size decides the allocation: refuse a corrupt one before allocating it. Nothing
        // on the disc comes close (the whole GameCube has 24 MB of main RAM).
        uint declaredSize = BinaryPrimitives.ReadUInt32BigEndian(compData.AsSpan(4, 4));
        if (declaredSize > MaxDecompressedSize)
            throw new InvalidDataException($"Yaz0: declared size {declaredSize} bytes is over the {MaxDecompressedSize}-byte limit");
        int uncompSize = (int)Math.Min(declaredSize, (uint)Math.Max(maxLength, 0));

        var output = new byte[uncompSize];
        int outputLen = 0;
        int srcOffset = 0x10;
        int validBitCount = 0;
        int currCodeByte = 0;

        while (outputLen < uncompSize)
        {
            if (validBitCount == 0)
            {
                currCodeByte = compData[srcOffset];
                srcOffset++;
                validBitCount = 8;
            }

            if ((currCodeByte & 0x80) != 0)
            {
                // Literal byte
                output[outputLen] = compData[srcOffset];
                srcOffset++;
                outputLen++;
            }
            else
            {
                // Backreference
                byte byte1 = compData[srcOffset];
                byte byte2 = compData[srcOffset + 1];
                srcOffset += 2;

                int dist = ((byte1 & 0xF) << 8) | byte2;
                int copySrcOffset = outputLen - (dist + 1);
                int numBytes = byte1 >> 4;
                if (numBytes == 0)
                {
                    numBytes = compData[srcOffset] + 0x12;
                    srcOffset++;
                }
                else
                {
                    numBytes += 2;
                }

                for (int i = 0; i < numBytes && outputLen < uncompSize; i++)
                {
                    output[outputLen] = output[copySrcOffset];
                    outputLen++;
                    copySrcOffset++;
                }
            }

            currCodeByte <<= 1;
            validBitCount--;
        }

        return output;
    }

    /// <summary>
    /// Compresses data using Yaz0 encoding.
    /// </summary>
    /// <param name="uncompData">The uncompressed data to compress.</param>
    /// <param name="searchDepth">How far back to search for matches (max 0x1000).</param>
    /// <param name="shouldPadData">If true, pad output to 0x20-byte alignment with zeros.</param>
    /// <returns>The Yaz0-compressed data.</returns>
    public byte[] Compress(byte[] uncompData, int searchDepth = DefaultSearchDepth, bool shouldPadData = false)
    {
        using var compStream = new MemoryStream();

        // Write 0x10-byte header: "Yaz0" + uncompressed size (big-endian) + 8 zero bytes.
        compStream.Write(Yaz0Magic, 0, 4);
        Span<byte> sizeBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sizeBytes, (uint)uncompData.Length);
        compStream.Write(sizeBytes);
        compStream.Write(stackalloc byte[8]); // 8 zero bytes

        // Reset lookahead state.
        _nextNumBytes = 0;
        _nextMatchPos = 0;
        _nextFlag = false;

        int uncompOffset = 0;
        int uncompSize = uncompData.Length;

        var dst = new List<byte>();
        int validBitCount = 0;
        int currCodeByte = 0;

        while (uncompOffset < uncompSize)
        {
            var (numBytes, matchPos) = GetNumBytesAndMatchPos(uncompData, uncompOffset, searchDepth);

            if (numBytes < 3)
            {
                // Copy the byte directly (literal).
                dst.Add(uncompData[uncompOffset]);
                uncompOffset++;

                currCodeByte |= (0x80 >> validBitCount);
            }
            else
            {
                // Backreference.
                int dist = uncompOffset - matchPos - 1;

                if (numBytes >= 0x12)
                {
                    dst.Add((byte)((dist & 0xFF00) >> 8));
                    dst.Add((byte)(dist & 0x00FF));

                    if (numBytes > MaxRunLength)
                        numBytes = MaxRunLength;
                    dst.Add((byte)(numBytes - 0x12));
                }
                else
                {
                    byte b = (byte)(((numBytes - 2) << 4) | ((dist >> 8) & 0x0F));
                    dst.Add(b);
                    dst.Add((byte)(dist & 0xFF));
                }

                uncompOffset += numBytes;
            }

            validBitCount++;

            if (validBitCount == 8)
            {
                // Finished 8 codes, write this block.
                compStream.WriteByte((byte)currCodeByte);
                foreach (byte b in dst)
                    compStream.WriteByte(b);

                currCodeByte = 0;
                validBitCount = 0;
                dst.Clear();
            }
        }

        if (validBitCount > 0)
        {
            // Still some codes leftover that weren't written yet, so write them now.
            compStream.WriteByte((byte)currCodeByte);
            foreach (byte b in dst)
                compStream.WriteByte(b);
        }
        else
        {
            // If there are no codes leftover to be written, we instead write a single zero at the end.
            // Not strictly necessary, but matches the original algorithm for maximum accuracy.
            compStream.WriteByte(0);
        }

        if (shouldPadData)
        {
            long currentLength = compStream.Length;
            long aligned = currentLength + (0x20 - currentLength % 0x20) % 0x20;
            long paddingNeeded = aligned - currentLength;
            if (paddingNeeded > 0)
            {
                compStream.Write(new byte[paddingNeeded]);
            }
        }

        return compStream.ToArray();
    }

    /// <summary>
    /// Returns the best (numBytes, matchPos) for the current position, with lookahead optimization.
    /// If the next position would yield a match that is >= current+2, defers the current position
    /// to a literal byte and reserves the next match.
    /// </summary>
    private (int numBytes, int matchPos) GetNumBytesAndMatchPos(byte[] uncomp, int uncompOffset, int searchDepth)
    {
        if (_nextFlag)
        {
            _nextFlag = false;
            return (_nextNumBytes, _nextMatchPos);
        }

        _nextFlag = false;
        var (numBytes, matchPos) = SimpleRleEncode(uncomp, uncompOffset, searchDepth);

        if (numBytes >= 3)
        {
            // Check if the next byte has a match that would compress better.
            var (nextNumBytes, nextMatchPos) = SimpleRleEncode(uncomp, uncompOffset + 1, searchDepth);

            if (nextNumBytes >= numBytes + 2)
            {
                // Defer: only copy one literal byte now, reserve the next match.
                numBytes = 1;
                matchPos = 0; // Not used when numBytes < 3.
                _nextNumBytes = nextNumBytes;
                _nextMatchPos = nextMatchPos;
                _nextFlag = true;
            }
        }

        return (numBytes, matchPos);
    }

    /// <summary>
    /// Simple RLE encoder: searches backward up to searchDepth bytes for the longest match
    /// at the given offset.
    /// </summary>
    private static (int numBytes, int matchPos) SimpleRleEncode(byte[] uncomp, int uncompOffset, int searchDepth)
    {
        int startOffset = uncompOffset - searchDepth;
        if (startOffset < 0)
            startOffset = 0;

        int numBytes = 0;
        int matchPos = 0;
        int maxNumBytesToCheck = uncomp.Length - uncompOffset;
        if (maxNumBytesToCheck > MaxRunLength)
            maxNumBytesToCheck = MaxRunLength;

        for (int possibleMatchPos = startOffset; possibleMatchPos < uncompOffset; possibleMatchPos++)
        {
            for (int indexInMatch = 0; indexInMatch < maxNumBytesToCheck; indexInMatch++)
            {
                if (uncomp[possibleMatchPos + indexInMatch] != uncomp[uncompOffset + indexInMatch])
                    break;

                int numBytesMatched = indexInMatch + 1;
                if (numBytesMatched > numBytes)
                {
                    numBytes = numBytesMatched;
                    matchPos = possibleMatchPos;
                }
            }
        }

        return (numBytes, matchPos);
    }
}
