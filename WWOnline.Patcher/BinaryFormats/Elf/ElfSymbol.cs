using System.Buffers.Binary;

namespace WWOnline.Patcher.BinaryFormats.Elf;

/// <summary>
/// Represents an ELF symbol table entry.
/// Ported from Python ELFSymbol class.
/// </summary>
public class ElfSymbol
{
    /// <summary>
    /// Size of a single symbol table entry (0x10 = 16 bytes).
    /// </summary>
    public const int EntrySize = 0x10;

    /// <summary>Offset into the string table for this symbol's name.</summary>
    public uint NameOffset { get; set; }

    /// <summary>Symbol value (typically an address).</summary>
    public uint Address { get; set; }

    /// <summary>Size of the object the symbol represents (0 if unknown or not applicable).</summary>
    public uint Size { get; set; }

    /// <summary>Symbol type (lower 4 bits of st_info).</summary>
    public ElfSymbolType Type { get; set; }

    /// <summary>Symbol binding (upper 4 bits of st_info).</summary>
    public ElfSymbolBinding Binding { get; set; }

    /// <summary>Symbol visibility / other info (st_other field).</summary>
    public byte Other { get; set; }

    /// <summary>Section index this symbol is associated with.</summary>
    public ushort SectionIndex { get; set; }

    /// <summary>Resolved symbol name (read from the string table).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Read a symbol table entry from the ELF data at the given offset.
    /// </summary>
    /// <param name="elfData">The full ELF file bytes.</param>
    /// <param name="offset">Byte offset to the start of this symbol entry.</param>
    public void Read(byte[] elfData, int offset)
    {
        var span = elfData.AsSpan();

        NameOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset + 0x00));
        Address = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset + 0x04));
        Size = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(offset + 0x08));

        byte info = elfData[offset + 0x0C];
        Type = (ElfSymbolType)(info & 0x0F);
        Binding = (ElfSymbolBinding)((info >> 4) & 0x0F);

        Other = elfData[offset + 0x0D];
        SectionIndex = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(offset + 0x0E));
    }
}
