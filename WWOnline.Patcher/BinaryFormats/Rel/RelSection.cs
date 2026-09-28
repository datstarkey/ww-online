using System.Buffers.Binary;

namespace WWOnline.Patcher.BinaryFormats.Rel;

/// <summary>
/// Represents a single section within a REL module file.
/// Each section has an 8-byte info entry in the section info table.
/// </summary>
public class RelSection
{
    public const int ENTRY_SIZE = 8;

    /// <summary>
    /// The file offset where this section's data begins (masked to even alignment).
    /// Zero means the section is uninitialized.
    /// </summary>
    public uint Offset { get; set; }

    /// <summary>
    /// Whether this section contains executable code (bit 0 of the offset/flags field).
    /// </summary>
    public bool IsExecutable { get; set; }

    /// <summary>
    /// The length of the section data in bytes.
    /// For BSS sections this equals the BSS size from the REL header.
    /// </summary>
    public uint Length { get; set; }

    /// <summary>
    /// True if the section has no file data (offset == 0).
    /// </summary>
    public bool IsUninitialized { get; set; } = true;

    /// <summary>
    /// True if this is the BSS section (uninitialized, not executable, non-zero length).
    /// </summary>
    public bool IsBss { get; set; }

    /// <summary>
    /// The raw section data. Null or empty for uninitialized/BSS sections.
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Reads section info from the REL data at the given info table offset.
    /// </summary>
    public void Read(byte[] relData, int infoOffset)
    {
        uint multVals = BinaryPrimitives.ReadUInt32BigEndian(relData.AsSpan(infoOffset));
        Offset = multVals & 0xFFFFFFFE;
        IsExecutable = (multVals & 1) != 0;
        Length = BinaryPrimitives.ReadUInt32BigEndian(relData.AsSpan(infoOffset + 4));

        IsUninitialized = Offset == 0;

        IsBss = IsUninitialized && !IsExecutable && Length != 0;

        if (!IsBss && Length != 0 && !IsUninitialized)
        {
            Data = new byte[Length];
            Array.Copy(relData, (int)Offset, Data, 0, (int)Length);
        }
    }

    /// <summary>
    /// Writes this section's info entry and data into the REL output stream.
    /// </summary>
    /// <param name="relData">The output stream to write to.</param>
    /// <param name="infoOffset">Offset in the stream for this section's 8-byte info entry.</param>
    /// <param name="nextSectionDataOffset">File offset where this section's data should be written.</param>
    /// <param name="bssSize">The BSS size from the REL header (used for BSS section length).</param>
    public void Save(MemoryStream relData, int infoOffset, int nextSectionDataOffset, uint bssSize)
    {
        if (IsUninitialized)
            Offset = 0;
        else
            Offset = (uint)nextSectionDataOffset & 0xFFFFFFFE;

        uint multVals = Offset;
        if (IsExecutable)
            multVals |= 1;

        BigEndianIO.WriteU32(relData, infoOffset, multVals);

        if (IsBss)
            Length = bssSize;
        else
            Length = (uint)Data.Length;

        BigEndianIO.WriteU32(relData, infoOffset + 4, Length);

        if (!IsBss && Length != 0 && !IsUninitialized)
        {
            BigEndianIO.WriteBytes(relData, (int)Offset, Data);
        }
    }
}
