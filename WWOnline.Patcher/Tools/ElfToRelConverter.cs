using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Elf;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.BinaryFormats.Rel;

namespace WWOnline.Patcher.Tools;

/// <summary>
/// Converts a linked ELF to a REL module and optionally inserts it into RELS.arc.
/// Ports elf2rel.py.
/// </summary>
public static class ElfToRelConverter
{
    public static readonly string[] AllowedSections =
    [
        ".text", ".ctors", ".dtors", ".rodata", ".data", ".bss",
        ".rodata.str1.4", ".rodata.cst4", ".rodata.cst8",
        ".sdata", ".sbss",
    ];

    /// <summary>
    /// Convert an ELF file to a REL module.
    /// </summary>
    public static RelFile ConvertElfToRel(ElfFile elf, uint relId, string actorProfileName)
    {
        var rel = new RelFile { Id = relId };

        // Map ELF sections to REL sections
        var sectionNameToRelSection = new Dictionary<string, RelSection>();
        var elfSectionIndexToRelSection = new Dictionary<int, RelSection>();

        for (var i = 0; i < elf.Sections.Count; i++)
        {
            var elfSection = elf.Sections[i];
            if (!AllowedSections.Contains(elfSection.Name) && elfSection.Type != ElfSectionType.SHT_NULL)
                continue;

            var relSection = new RelSection
            {
                Data = GetSectionContents(elfSection),
                IsExecutable = ((uint)elfSection.Flags & (uint)ElfSectionFlags.SHF_EXECINSTR) != 0,
                IsUninitialized = elfSection.Type == ElfSectionType.SHT_NULL,
                IsBss = false,
            };

            rel.Sections.Add(relSection);
            sectionNameToRelSection[elfSection.Name] = relSection;
            elfSectionIndexToRelSection[i] = relSection;
        }

        // Generate relocations
        foreach (var elfSection in elf.Sections)
        {
            if (elfSection.Type != ElfSectionType.SHT_RELA) continue;
            if (!elfSection.Name.StartsWith(".rela")) continue;

            var relocatedSectionName = elfSection.Name[".rela".Length..];
            if (!sectionNameToRelSection.TryGetValue(relocatedSectionName, out var relSection))
                continue;

            var sectionIndex = rel.Sections.IndexOf(relSection);

            foreach (var elfRelocation in elf.Relocations[elfSection.Name])
            {
                var elfSymbol = elf.Symbols[".symtab"][(int)elfRelocation.SymbolIndex];
                var relRelocation = new RelRelocation
                {
                    RelocationType = (RelRelocationType)(byte)elfRelocation.Type,
                };

                if (elfSymbol.SectionIndex == 0)
                    throw new InvalidOperationException($"Unresolved external symbol: {elfSymbol.Name}");

                uint moduleNum;
                if (elfSymbol.SectionIndex == (ushort)ElfSymbolSpecialSection.SHN_ABS)
                {
                    // Symbol is in main.dol
                    moduleNum = 0;
                    relRelocation.SectionNumToRelocateAgainst = 0;
                }
                else if (elfSymbol.SectionIndex >= 0xFF00)
                {
                    throw new InvalidOperationException(
                        $"Special section number not implemented: {elfSymbol.SectionIndex:X4}");
                }
                else
                {
                    // Symbol is in the current REL
                    moduleNum = rel.Id;

                    var sectionNameToRelocateAgainst = elf.Sections[(int)elfSymbol.SectionIndex].Name;
                    if (!sectionNameToRelSection.TryGetValue(sectionNameToRelocateAgainst, out var relSectionAgainst))
                        throw new InvalidOperationException(
                            $"Section name \"{sectionNameToRelocateAgainst}\" could not be found for symbol \"{elfSymbol.Name}\"");

                    relRelocation.SectionNumToRelocateAgainst =
                        (byte)rel.Sections.IndexOf(relSectionAgainst);
                }

                relRelocation.SymbolAddress = elfSymbol.Address + elfRelocation.Addend;
                relRelocation.RelocationOffset = elfRelocation.RelocationOffset;
                relRelocation.CurrSectionNum = sectionIndex;

                if (!rel.RelocationEntriesForModule.ContainsKey(moduleNum))
                    rel.RelocationEntriesForModule[moduleNum] = new List<RelRelocation>();
                rel.RelocationEntriesForModule[moduleNum].Add(relRelocation);
            }
        }

        // Set prolog/epilog/unresolved from ELF symbols
        SetSpecialSymbol(elf, elfSectionIndexToRelSection, rel, "_prolog",
            out var prologSection, out var prologOffset);
        rel.PrologSection = prologSection;
        rel.PrologOffset = prologOffset;

        SetSpecialSymbol(elf, elfSectionIndexToRelSection, rel, "_epilog",
            out var epilogSection, out var epilogOffset);
        rel.EpilogSection = epilogSection;
        rel.EpilogOffset = epilogOffset;

        SetSpecialSymbol(elf, elfSectionIndexToRelSection, rel, "_unresolved",
            out var unresolvedSection, out var unresolvedOffset);
        rel.UnresolvedSection = unresolvedSection;
        rel.UnresolvedOffset = unresolvedOffset;

        return rel;
    }

    /// <summary>
    /// Returns the bytes to place in the REL for an ELF section.
    /// SHT_NOBITS sections (.bss/.sbss) occupy no file space: their sh_offset points at whatever
    /// section follows (typically .debug_info), so reading sh_size bytes from there yields garbage.
    /// They are emitted as zero-filled initialized data instead of a true REL bss section, because
    /// OSLink only allocates a single bss section and we may have both .bss and .sbss. Keeping them
    /// as ordinary data sections leaves section indices and relocations unchanged (BssSize stays 0).
    /// </summary>
    public static byte[] GetSectionContents(ElfSection elfSection)
    {
        if (elfSection.Type == ElfSectionType.SHT_NOBITS)
            return new byte[elfSection.Size];

        return (byte[])elfSection.Data.Clone();
    }

    private static void SetSpecialSymbol(ElfFile elf,
        Dictionary<int, RelSection> elfSectionIndexToRelSection,
        RelFile rel, string symbolName,
        out byte sectionIndex, out uint offset)
    {
        var symbol = elf.SymbolsByName[".symtab"][symbolName];
        var relSection = elfSectionIndexToRelSection[(int)symbol.SectionIndex];
        sectionIndex = (byte)rel.Sections.IndexOf(relSection);
        offset = symbol.Address;
    }

    /// <summary>
    /// Convert ELF to REL and insert into RELS.arc.
    /// </summary>
    public static void ConvertAndInsertIntoRelsArc(
        string elfPath, string relOutputPath, uint relId,
        string actorProfileName, string? relsArcPath)
    {
        var elf = new ElfFile();
        elf.ReadFromFile(elfPath);

        var rel = ConvertElfToRel(elf, relId, actorProfileName);
        rel.SaveToFile(relOutputPath);

        if (relsArcPath == null || !File.Exists(relsArcPath)) return;

        var arcData = File.ReadAllBytes(relsArcPath);
        var relsArc = new RarcArchive();
        relsArc.Read(arcData);

        // Update profile list to reference the profile in the custom REL
        var profileListEntry = relsArc.GetFileEntry("f_pc_profile_lst.rel")
            ?? throw new InvalidOperationException("f_pc_profile_lst.rel not found in RELS.arc");

        var profileList = new RelFile();
        profileList.Read(profileListEntry.Data!);

        if (!profileList.RelocationEntriesForModule.ContainsKey(rel.Id))
            throw new InvalidOperationException($"REL ID 0x{rel.Id:X2} not found in profile list");

        if (!elf.SymbolsByName[".symtab"].ContainsKey(actorProfileName))
            throw new InvalidOperationException(
                $"Actor profile symbol \"{actorProfileName}\" not found in ELF");

        var profileSymbol = elf.SymbolsByName[".symtab"][actorProfileName];
        profileList.RelocationEntriesForModule[rel.Id][0].SymbolAddress = profileSymbol.Address;

        // Find .rodata section index in the new REL
        if (elf.Sections.FindIndex(s => s.Name == ".rodata") is var elfRodataIdx and >= 0)
        {
            // Find the ELF .rodata section index, map it to REL section
            var elfRodataSection = elf.Sections[elfRodataIdx];
            // We need to find which REL section corresponds to .rodata
            // Rebuild the mapping
            int relSectionIdx = 0;
            for (int i = 0; i < elf.Sections.Count; i++)
            {
                var es = elf.Sections[i];
                if (AllowedSections.Contains(es.Name) || es.Type == ElfSectionType.SHT_NULL)
                {
                    if (es.Name == ".rodata")
                    {
                        profileList.RelocationEntriesForModule[rel.Id][0]
                            .SectionNumToRelocateAgainst = (byte)relSectionIdx;
                        break;
                    }
                    relSectionIdx++;
                }
            }
        }

        profileList.SaveChanges(preserveSectionDataOffsets: true);

        // Update the profile list entry (re-compressed like the vanilla one: ~5 KB instead of ~16 KB)
        profileListEntry.ReplaceContents(profileList.GetData());

        // Find and replace the existing REL with matching ID (stored Yaz0 like every vanilla REL;
        // other RELs stay compressed)
        if (!relsArc.ReplaceRelById(rel.Id, rel.GetData()))
            throw new InvalidOperationException($"Failed to find REL to replace with ID 0x{rel.Id:X3}");

        var savedArc = relsArc.SaveChanges();

        // All of RELS.arc is loaded into ARAM (see MaxRelsArcGrowthBytes). Too much growth makes
        // the first play scene's LkD00.arc ARAM mount fail ("Failed assertion d_s_play.cpp:3439
        // l_lkDemoAnmCommand->getArchive()") — fail the build instead.
        long growth = savedArc.Length - arcData.Length;
        if (growth > MaxRelsArcGrowthBytes)
            throw new InvalidOperationException(
                $"RELS.arc would grow by {growth} bytes ({arcData.Length} -> {savedArc.Length}); limit is " +
                $"{MaxRelsArcGrowthBytes}. ARAM can't take it — shrink the puppet REL " +
                "or check that the RELs kept their Yaz0 compression.");

        File.WriteAllBytes(relsArcPath, savedArc);
    }

    /// <summary>
    /// How much bigger than vanilla RELS.arc may get (compressed bytes: the puppet REL and the
    /// profile list are stored Yaz0). A deliberately conservative guard, not the real limit.
    /// RELS.arc is a JKRCompArchive whose whole file data (vanilla 0xE4A40 bytes) is the ARAM part
    /// (DynamicLink.cpp:185, JKRCompArchive.cpp:126), allocated for good from the 0x5CE000-byte
    /// JKRAramHeap (m_Do_machine.cpp:558, set at main.dol 0x8000C92C). The rest of that heap is
    /// the boot-time ARAM mounts (d_s_logo.cpp:865-923, 2,885,760 bytes), picture box + boss data
    /// (0x6200) and LkD00/LkD01.arc (1,208,992 / 1,129,728), which leaves about 1,030,000 bytes free
    /// in vanilla: the real limit, matching the 943KB -> 1.97MB incident that failed at ~1.03 MB.
    /// </summary>
    public const int MaxRelsArcGrowthBytes = 0x10000;
}
