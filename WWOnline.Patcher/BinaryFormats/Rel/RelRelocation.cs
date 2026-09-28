using System.Buffers.Binary;

namespace WWOnline.Patcher.BinaryFormats.Rel;

/// <summary>
/// Represents a single relocation entry within a REL module file.
/// Each entry is 8 bytes and describes how to patch a location when the module is linked.
/// </summary>
public class RelRelocation
{
    public const int ENTRY_SIZE = 8;

    /// <summary>
    /// The byte offset from the previous relocation to this one (within the same section).
    /// </summary>
    public ushort OffsetOfCurrRelocationFromPrev { get; set; }

    /// <summary>
    /// The type of relocation to apply.
    /// </summary>
    public RelRelocationType RelocationType { get; set; }

    /// <summary>
    /// The section index that the symbol address is relative to.
    /// </summary>
    public byte SectionNumToRelocateAgainst { get; set; }

    /// <summary>
    /// The address/offset of the symbol within the target section.
    /// </summary>
    public uint SymbolAddress { get; set; }

    /// <summary>
    /// The absolute offset within the current section where this relocation applies.
    /// Computed as the running sum of OffsetOfCurrRelocationFromPrev values.
    /// </summary>
    public uint RelocationOffset { get; set; }

    /// <summary>
    /// The section number that this relocation is applied to.
    /// Set by R_DOLPHIN_SECTION entries during parsing.
    /// </summary>
    public int? CurrSectionNum { get; set; }

    /// <summary>
    /// Reads a relocation entry from REL data at the given offset.
    /// </summary>
    /// <param name="relData">The raw REL file data.</param>
    /// <param name="offset">File offset of this 8-byte relocation entry.</param>
    /// <param name="prevRelocationOffset">The cumulative relocation offset from previous entries.</param>
    /// <param name="currSectionNum">The current section number (set by prior R_DOLPHIN_SECTION).</param>
    public void Read(byte[] relData, int offset, uint prevRelocationOffset, int? currSectionNum)
    {
        OffsetOfCurrRelocationFromPrev = BinaryPrimitives.ReadUInt16BigEndian(relData.AsSpan(offset));
        RelocationType = (RelRelocationType)relData[offset + 2];
        SectionNumToRelocateAgainst = relData[offset + 3];
        SymbolAddress = BinaryPrimitives.ReadUInt32BigEndian(relData.AsSpan(offset + 4));

        RelocationOffset = OffsetOfCurrRelocationFromPrev + prevRelocationOffset;
        CurrSectionNum = currSectionNum;
    }

    /// <summary>
    /// Writes this relocation entry to the output stream at the given offset.
    /// Handles special Dolphin relocation types by zeroing out irrelevant fields.
    /// </summary>
    public void Save(MemoryStream data, int offset)
    {
        if (RelocationType == RelRelocationType.R_DOLPHIN_SECTION)
        {
            OffsetOfCurrRelocationFromPrev = 0;
            SymbolAddress = 0;
        }
        else if (RelocationType == RelRelocationType.R_DOLPHIN_END)
        {
            OffsetOfCurrRelocationFromPrev = 0;
            SectionNumToRelocateAgainst = 0;
            SymbolAddress = 0;
        }
        else if (RelocationType == RelRelocationType.R_DOLPHIN_NOP)
        {
            SectionNumToRelocateAgainst = 0;
            SymbolAddress = 0;
        }

        // Ensure the stream is large enough
        if (data.Length < offset + ENTRY_SIZE)
        {
            data.Position = data.Length;
            data.Write(new byte[offset + ENTRY_SIZE - data.Length]);
        }

        BigEndianIO.WriteU16(data, offset, OffsetOfCurrRelocationFromPrev);
        BigEndianIO.WriteU8(data, offset + 2, (byte)RelocationType);
        BigEndianIO.WriteU8(data, offset + 3, SectionNumToRelocateAgainst);
        BigEndianIO.WriteU32(data, offset + 4, SymbolAddress);
    }
}
