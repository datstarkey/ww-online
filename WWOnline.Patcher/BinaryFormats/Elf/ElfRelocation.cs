using System.Buffers.Binary;

namespace WWOnline.Patcher.BinaryFormats.Elf;

/// <summary>
/// Represents an ELF RELA relocation entry.
/// Ported from Python ELFRelocation class.
/// </summary>
public class ElfRelocation
{
    /// <summary>
    /// Size of a single RELA relocation entry (3 x uint32 = 12 bytes).
    /// </summary>
    public const int EntrySize = 0xC;

    /// <summary>Offset in the section where the relocation applies.</summary>
    public uint RelocationOffset { get; set; }

    /// <summary>Relocation type (lower 8 bits of the info field).</summary>
    public ElfRelocationType Type { get; set; }

    /// <summary>Index into the symbol table (upper 24 bits of the info field).</summary>
    public uint SymbolIndex { get; set; }

    /// <summary>Addend value for the relocation.</summary>
    public uint Addend { get; set; }

    /// <summary>
    /// Read a RELA relocation entry from the ELF data at the given offset.
    /// </summary>
    /// <param name="elfData">The full ELF file bytes.</param>
    /// <param name="offset">Byte offset to the start of this relocation entry.</param>
    public void Read(byte[] elfData, int offset)
    {
        var span = elfData.AsSpan();

        RelocationOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset + 0x00));
        uint info = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset + 0x04));
        Type = (ElfRelocationType)(info & 0xFF);
        SymbolIndex = (info >> 8);
        Addend = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset + 0x08));
    }
}
