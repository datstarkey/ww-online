using System.Buffers.Binary;

namespace WWOnline.Patcher.BinaryFormats.Elf;

/// <summary>
/// Parses a big-endian ELF file (PowerPC GameCube/Wii format).
/// Ported from Python ELF class in elf.py.
/// </summary>
public class ElfFile
{
    /// <summary>Raw ELF file bytes.</summary>
    public byte[] Data { get; private set; } = Array.Empty<byte>();

    /// <summary>All section headers in order.</summary>
    public List<ElfSection> Sections { get; private set; } = new();

    /// <summary>Sections indexed by name.</summary>
    public Dictionary<string, ElfSection> SectionsByName { get; private set; } = new();

    /// <summary>
    /// RELA relocations keyed by the relocation section name (e.g., ".rela.text").
    /// </summary>
    public Dictionary<string, List<ElfRelocation>> Relocations { get; private set; } = new();

    /// <summary>
    /// Symbol table entries keyed by the symbol table section name (e.g., ".symtab").
    /// </summary>
    public Dictionary<string, List<ElfSymbol>> Symbols { get; private set; } = new();

    /// <summary>
    /// Symbols indexed by [section name][symbol name] for fast lookup.
    /// </summary>
    public Dictionary<string, Dictionary<string, ElfSymbol>> SymbolsByName { get; private set; } = new();

    /// <summary>
    /// Read and parse an ELF file from disk.
    /// </summary>
    /// <param name="filePath">Path to the ELF file.</param>
    public void ReadFromFile(string filePath)
    {
        Data = File.ReadAllBytes(filePath);
        Parse();
    }

    /// <summary>
    /// Read and parse an ELF from an in-memory byte array.
    /// </summary>
    /// <param name="elfData">The raw ELF bytes.</param>
    public void ReadFromBytes(byte[] elfData)
    {
        Data = elfData;
        Parse();
    }

    private void Parse()
    {
        var span = Data.AsSpan();

        // ELF header fields (big-endian 32-bit ELF)
        uint sectionHeadersTableOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(0x20));
        ushort numSectionHeaders = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(0x30));
        ushort sectionHeaderStringTableIndex = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(0x32));

        // Read all section headers
        Sections = new List<ElfSection>(numSectionHeaders);
        for (int i = 0; i < numSectionHeaders; i++)
        {
            var section = new ElfSection();
            section.Read(Data, (int)(sectionHeadersTableOffset + (uint)(i * ElfSection.EntrySize)));
            Sections.Add(section);
        }

        // Resolve section names from the section header string table (.shstrtab)
        var shstrtab = Sections[sectionHeaderStringTableIndex];
        foreach (var section in Sections)
        {
            section.Name = BigEndianIO.ReadStrUntilNull(shstrtab.Data, (int)section.NameOffset);
        }

        // Build sections-by-name dictionary
        SectionsByName = new Dictionary<string, ElfSection>();
        foreach (var section in Sections)
        {
            SectionsByName[section.Name] = section;
        }

        // Parse RELA relocation sections
        Relocations = new Dictionary<string, List<ElfRelocation>>();
        foreach (var section in Sections)
        {
            if (section.Type == ElfSectionType.SHT_RELA)
            {
                var relocs = new List<ElfRelocation>();
                int entryCount = (int)(section.Size / ElfRelocation.EntrySize);
                for (int i = 0; i < entryCount; i++)
                {
                    var relocation = new ElfRelocation();
                    relocation.Read(Data, (int)(section.SectionOffset + (uint)(i * ElfRelocation.EntrySize)));
                    relocs.Add(relocation);
                }
                Relocations[section.Name] = relocs;
            }
        }

        // Parse symbol table sections and resolve symbol names from .strtab
        Symbols = new Dictionary<string, List<ElfSymbol>>();
        SymbolsByName = new Dictionary<string, Dictionary<string, ElfSymbol>>();
        foreach (var section in Sections)
        {
            if (section.Type == ElfSectionType.SHT_SYMTAB)
            {
                var syms = new List<ElfSymbol>();
                var symsByName = new Dictionary<string, ElfSymbol>();
                int entryCount = (int)(section.Size / ElfSymbol.EntrySize);
                for (int i = 0; i < entryCount; i++)
                {
                    var symbol = new ElfSymbol();
                    symbol.Read(Data, (int)(section.SectionOffset + (uint)(i * ElfSymbol.EntrySize)));
                    symbol.Name = ReadStringFromTable(symbol.NameOffset);
                    syms.Add(symbol);
                    symsByName[symbol.Name] = symbol;
                }
                Symbols[section.Name] = syms;
                SymbolsByName[section.Name] = symsByName;
            }
        }
    }

    /// <summary>
    /// Read a null-terminated string from the .strtab section at the given offset.
    /// Equivalent to Python's read_string_from_table().
    /// </summary>
    /// <param name="stringOffset">Offset within the .strtab section data.</param>
    /// <returns>The decoded string.</returns>
    public string ReadStringFromTable(uint stringOffset)
    {
        if (!SectionsByName.TryGetValue(".strtab", out var strtab))
            return string.Empty;

        return BigEndianIO.ReadStrUntilNull(strtab.Data, (int)stringOffset);
    }
}
