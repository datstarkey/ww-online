using System.Buffers.Binary;

namespace WWOnline.Patcher.BinaryFormats.Elf;

/// <summary>
/// Represents an ELF section header and its associated data.
/// Ported from Python ELFSection class.
/// </summary>
public class ElfSection
{
    /// <summary>
    /// Size of a single section header entry in the ELF section header table.
    /// </summary>
    public const int EntrySize = 0x28;

    /// <summary>Offset into the section header string table for this section's name.</summary>
    public uint NameOffset { get; set; }

    /// <summary>Section type (SHT_PROGBITS, SHT_SYMTAB, etc.).</summary>
    public ElfSectionType Type { get; set; }

    /// <summary>Section flags (SHF_WRITE, SHF_ALLOC, etc.).</summary>
    public uint Flags { get; set; }

    /// <summary>Virtual address of the section in memory.</summary>
    public uint Address { get; set; }

    /// <summary>File offset to the start of section data.</summary>
    public uint SectionOffset { get; set; }

    /// <summary>Size of section data in bytes.</summary>
    public uint Size { get; set; }

    /// <summary>Section header table index link (interpretation depends on section type).</summary>
    public uint Link { get; set; }

    /// <summary>Extra info (interpretation depends on section type).</summary>
    public uint Info { get; set; }

    /// <summary>Address alignment constraint.</summary>
    public uint AddrAlign { get; set; }

    /// <summary>Size of each entry if the section holds a table of fixed-size entries.</summary>
    public uint EntSize { get; set; }

    /// <summary>Resolved section name (read from the section header string table).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Raw section data bytes.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Read a section header from the ELF file data at the given header offset.
    /// Also copies the section data from the ELF data.
    /// </summary>
    /// <param name="elfData">The full ELF file bytes.</param>
    /// <param name="headerOffset">Byte offset to the start of this section header entry.</param>
    public void Read(byte[] elfData, int headerOffset)
    {
        var span = elfData.AsSpan();

        NameOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x00));
        Type = (ElfSectionType)BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x04));
        Flags = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x08));
        Address = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x0C));
        SectionOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x10));
        Size = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x14));
        Link = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x18));
        Info = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x1C));
        AddrAlign = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x20));
        EntSize = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(headerOffset + 0x24));

        // Copy section data from the ELF file
        if (Size > 0 && SectionOffset > 0)
        {
            Data = elfData.AsSpan((int)SectionOffset, (int)Size).ToArray();
        }
        else
        {
            Data = Array.Empty<byte>();
        }
    }
}
