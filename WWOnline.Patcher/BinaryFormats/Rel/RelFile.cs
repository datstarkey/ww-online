using System.Buffers.Binary;
using WWOnline.Patcher.BinaryFormats.Yaz0;

namespace WWOnline.Patcher.BinaryFormats.Rel;

/// <summary>
/// Reads, modifies, and writes Nintendo REL (relocatable module) files.
/// Ported from the Python rel.py implementation (LagoLunatic / wwrando).
/// </summary>
public class RelFile
{
    private MemoryStream _data = new();

    /// <summary>Module ID.</summary>
    public uint Id { get; set; }

    /// <summary>All sections in the REL.</summary>
    public List<RelSection> Sections { get; set; } = new();

    /// <summary>Offset of the module name string (usually 0).</summary>
    public uint NameOffset { get; set; }

    /// <summary>Length of the module name string (usually 0).</summary>
    public uint NameLength { get; set; }

    /// <summary>REL format version (always 3 for GameCube/Wii).</summary>
    public uint RelFormatVersion { get; set; } = 3;

    /// <summary>Size of the BSS section in bytes.</summary>
    public uint BssSize { get; set; }

    /// <summary>
    /// Relocation entries grouped by module number.
    /// Key = target module ID (0 = main DOL, self.Id = self-relocations).
    /// Insertion order matters: during save, self-module and module 0 are moved to the end.
    /// </summary>
    public OrderedDictionary<uint, List<RelRelocation>> RelocationEntriesForModule { get; set; } = new();

    /// <summary>Section index containing the _prolog function.</summary>
    public byte PrologSection { get; set; }

    /// <summary>Section index containing the _epilog function.</summary>
    public byte EpilogSection { get; set; }

    /// <summary>Section index containing the _unresolved function.</summary>
    public byte UnresolvedSection { get; set; }

    /// <summary>Offset of _prolog within its section.</summary>
    public uint PrologOffset { get; set; }

    /// <summary>Offset of _epilog within its section.</summary>
    public uint EpilogOffset { get; set; }

    /// <summary>Offset of _unresolved within its section.</summary>
    public uint UnresolvedOffset { get; set; }

    /// <summary>Section data alignment requirement.</summary>
    public uint Alignment { get; set; } = 8;

    /// <summary>BSS data alignment requirement.</summary>
    public uint BssAlignment { get; set; } = 1;

    /// <summary>
    /// End of the "fixed" portion of the REL file.
    /// Data after this offset can be reused at runtime (e.g., for BSS).
    /// </summary>
    public uint FixSize { get; set; }

    /// <summary>
    /// Computed BSS offset: next 0x20-byte aligned boundary after FixSize.
    /// </summary>
    public uint BssOffset => (FixSize + 0x1F) & ~0x1Fu;

    /// <summary>Index of the BSS section, or null if none exists.</summary>
    public int? BssSectionIndex { get; private set; }

    // ----------------------------------------------------------------
    //  Read
    // ----------------------------------------------------------------

    /// <summary>
    /// Reads a REL file from disk. Handles Yaz0-compressed files automatically.
    /// </summary>
    public void ReadFromFile(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        Read(fileData);
    }

    /// <summary>
    /// Parses a REL from raw (possibly Yaz0-compressed) byte data.
    /// </summary>
    public void Read(byte[] data)
    {
        if (Yaz0Codec.CheckIsCompressed(data))
            data = Yaz0Codec.Decompress(data);

        _data = new MemoryStream(data);

        Id = BigEndianIO.ReadU32(data, 0x00);

        // Parse section info table
        Sections = new List<RelSection>();
        uint numSections = BigEndianIO.ReadU32(data, 0x0C);
        uint sectionInfoTableOffset = BigEndianIO.ReadU32(data, 0x10);
        for (int i = 0; i < (int)numSections; i++)
        {
            int sectionInfoOffset = (int)sectionInfoTableOffset + i * RelSection.ENTRY_SIZE;
            var section = new RelSection();
            section.Read(data, sectionInfoOffset);
            Sections.Add(section);
        }

        NameOffset = BigEndianIO.ReadU32(data, 0x14);
        NameLength = BigEndianIO.ReadU32(data, 0x18);
        RelFormatVersion = BigEndianIO.ReadU32(data, 0x1C);

        BssSize = BigEndianIO.ReadU32(data, 0x20);

        // Parse import table to get relocation data offsets per module
        var relocationDataOffsetForModule = new OrderedDictionary<uint, uint>();
        uint impTableOffset = BigEndianIO.ReadU32(data, 0x28);
        uint impTableLength = BigEndianIO.ReadU32(data, 0x2C);
        uint offset = impTableOffset;
        while (offset < impTableOffset + impTableLength)
        {
            uint moduleNum = BigEndianIO.ReadU32(data, (int)offset);
            uint relocationDataOffset = BigEndianIO.ReadU32(data, (int)offset + 4);
            relocationDataOffsetForModule[moduleNum] = relocationDataOffset;
            offset += 8;
        }

        // Parse relocation entries for each module
        RelocationEntriesForModule = new OrderedDictionary<uint, List<RelRelocation>>();
        int? currSectionNum = null;
        foreach (var (moduleNum, relocationDataOffset) in relocationDataOffsetForModule)
        {
            RelocationEntriesForModule[moduleNum] = new List<RelRelocation>();

            int entryOffset = (int)relocationDataOffset;
            uint prevRelocationOffset = 0;
            while (true)
            {
                var relocationType = (RelRelocationType)data[entryOffset + 2];
                if (relocationType == RelRelocationType.R_DOLPHIN_END)
                    break;

                var entry = new RelRelocation();
                entry.Read(data, entryOffset, prevRelocationOffset, currSectionNum);
                prevRelocationOffset = entry.RelocationOffset;

                if (entry.RelocationType == RelRelocationType.R_DOLPHIN_SECTION)
                {
                    currSectionNum = entry.SectionNumToRelocateAgainst;
                    prevRelocationOffset = 0;
                }
                else
                {
                    RelocationEntriesForModule[moduleNum].Add(entry);
                }

                entryOffset += RelRelocation.ENTRY_SIZE;
            }
        }

        PrologSection = BigEndianIO.ReadU8(data, 0x30);
        EpilogSection = BigEndianIO.ReadU8(data, 0x31);
        UnresolvedSection = BigEndianIO.ReadU8(data, 0x32);
        PrologOffset = BigEndianIO.ReadU32(data, 0x34);
        EpilogOffset = BigEndianIO.ReadU32(data, 0x38);
        UnresolvedOffset = BigEndianIO.ReadU32(data, 0x3C);

        Alignment = BigEndianIO.ReadU32(data, 0x40);
        BssAlignment = BigEndianIO.ReadU32(data, 0x44);

        // Space after this fix_size offset can be reused for other purposes.
        FixSize = BigEndianIO.ReadU32(data, 0x48);

        // Find the BSS section and set its offset
        BssSectionIndex = null;
        for (int i = 0; i < Sections.Count; i++)
        {
            if (Sections[i].IsBss)
            {
                BssSectionIndex = i;
                Sections[i].Offset = BssOffset;
                break;
            }
        }
    }

    // ----------------------------------------------------------------
    //  Offset conversion helpers
    // ----------------------------------------------------------------

    /// <summary>
    /// Converts a REL file offset to a (sectionIndex, relativeOffset) tuple.
    /// </summary>
    public (int sectionIndex, uint relativeOffset) ConvertRelOffsetToSectionIndexAndRelativeOffset(uint relOffset)
    {
        for (int i = 0; i < Sections.Count; i++)
        {
            var section = Sections[i];
            if (section.IsUninitialized)
                continue;

            if (relOffset >= section.Offset && relOffset < section.Offset + (uint)section.Data.Length)
                return (i, relOffset - section.Offset);
        }

        throw new InvalidOperationException(
            $"Offset 0x{relOffset:X4} is not in the data for any of the REL sections");
    }

    /// <summary>
    /// Converts a REL file offset to a (sectionData, relativeOffset) tuple.
    /// Returns (null, 0) if the offset is not within any section.
    /// </summary>
    public (byte[]? data, uint relativeOffset) ConvertRelOffsetToSectionDataAndRelativeOffset(uint relOffset)
    {
        foreach (var section in Sections)
        {
            if (section.IsUninitialized)
                continue;

            if (relOffset >= section.Offset && relOffset < section.Offset + (uint)section.Data.Length)
                return (section.Data, relOffset - section.Offset);
        }

        return (null, 0);
    }

    // ----------------------------------------------------------------
    //  Save
    // ----------------------------------------------------------------

    /// <summary>
    /// Serializes the REL to the internal data stream, recomputing all offsets and tables.
    /// </summary>
    public void SaveChanges(bool preserveSectionDataOffsets = false)
    {
        _data = new MemoryStream();
        var data = _data;

        // Write header fields
        BigEndianIO.WriteU32(data, 0x00, Id);
        BigEndianIO.WriteU32(data, 0x04, 0);
        BigEndianIO.WriteU32(data, 0x08, 0);

        int numSections = Sections.Count;
        BigEndianIO.WriteU32(data, 0x0C, (uint)numSections);
        BigEndianIO.WriteU32(data, 0x14, NameOffset);
        BigEndianIO.WriteU32(data, 0x18, NameLength);
        BigEndianIO.WriteU32(data, 0x1C, RelFormatVersion);
        BigEndianIO.WriteU32(data, 0x20, BssSize);

        // Section info table starts at 0x4C
        uint sectionInfoTableOffset = 0x4C;
        BigEndianIO.WriteU32(data, 0x10, sectionInfoTableOffset);

        int nextSectionInfoOffset = (int)sectionInfoTableOffset;
        int nextSectionDataOffset = (int)sectionInfoTableOffset + numSections * RelSection.ENTRY_SIZE;
        nextSectionDataOffset = BigEndianIO.PadOffsetToNearest(nextSectionDataOffset, 4);

        foreach (var section in Sections)
        {
            if (preserveSectionDataOffsets)
            {
                if (!section.IsUninitialized)
                {
                    if (section.Offset < (uint)nextSectionDataOffset)
                        throw new InvalidOperationException(
                            $"Section offset 0x{section.Offset:X} is before expected 0x{nextSectionDataOffset:X}");
                    nextSectionDataOffset = (int)section.Offset;
                }
            }

            section.Save(data, nextSectionInfoOffset, nextSectionDataOffset, BssSize);
            nextSectionInfoOffset += RelSection.ENTRY_SIZE;
            if (!section.IsBss)
                nextSectionDataOffset += (int)section.Length;

            nextSectionDataOffset = BigEndianIO.PadOffsetToNearest(nextSectionDataOffset, 4);
        }

        // Reorder relocations: move self-module and module 0 (main DOL) to END
        if (RelocationEntriesForModule.ContainsKey(Id))
        {
            var selfRelocs = RelocationEntriesForModule[Id];
            RelocationEntriesForModule.Remove(Id);
            RelocationEntriesForModule[Id] = selfRelocs;
        }
        if (RelocationEntriesForModule.ContainsKey(0))
        {
            var mainRelocs = RelocationEntriesForModule[0];
            RelocationEntriesForModule.Remove(0);
            RelocationEntriesForModule[0] = mainRelocs;
        }

        // Write imp table and relocation data
        int impTableOffset = (int)BigEndianIO.DataLen(data);
        int impTableSize = RelocationEntriesForModule.Count * 8;
        int impTableEnd = impTableOffset + impTableSize;
        int relocationTableOffset = impTableEnd;
        FixSize = (uint)relocationTableOffset;

        BigEndianIO.WriteU32(data, 0x24, (uint)relocationTableOffset);
        BigEndianIO.WriteU32(data, 0x28, (uint)impTableOffset);
        BigEndianIO.WriteU32(data, 0x2C, (uint)impTableSize);

        int nextImpOffset = impTableOffset;
        int nextRelocationEntryOffset = relocationTableOffset;

        foreach (var (moduleNum, relocationEntries) in RelocationEntriesForModule)
        {
            BigEndianIO.WriteU32(data, nextImpOffset, moduleNum);
            BigEndianIO.WriteU32(data, nextImpOffset + 4, (uint)nextRelocationEntryOffset);
            nextImpOffset += 8;

            // Sort relocations by (curr_section_num, relocation_offset)
            relocationEntries.Sort((a, b) =>
            {
                int cmpSection = (a.CurrSectionNum ?? 0).CompareTo(b.CurrSectionNum ?? 0);
                return cmpSection != 0 ? cmpSection : a.RelocationOffset.CompareTo(b.RelocationOffset);
            });

            int? currSectionNum = null;
            uint prevRelocationOffset = 0;

            foreach (var entry in relocationEntries)
            {
                // Emit R_DOLPHIN_SECTION when the section changes
                if (entry.CurrSectionNum != currSectionNum)
                {
                    currSectionNum = entry.CurrSectionNum;
                    prevRelocationOffset = 0;

                    var sectionStartEntry = new RelRelocation
                    {
                        RelocationType = RelRelocationType.R_DOLPHIN_SECTION,
                        SectionNumToRelocateAgainst = (byte)(currSectionNum ?? 0),
                    };
                    sectionStartEntry.Save(data, nextRelocationEntryOffset);
                    nextRelocationEntryOffset += RelRelocation.ENTRY_SIZE;
                }

                // Insert R_DOLPHIN_NOP entries to bridge gaps larger than 0xFFFF
                long offsetDiff = (long)entry.RelocationOffset - prevRelocationOffset;
                while (offsetDiff > 0xFFFF)
                {
                    var nopEntry = new RelRelocation
                    {
                        RelocationType = RelRelocationType.R_DOLPHIN_NOP,
                        OffsetOfCurrRelocationFromPrev = 0xFFFF,
                    };
                    nopEntry.Save(data, nextRelocationEntryOffset);
                    prevRelocationOffset += 0xFFFF;
                    nextRelocationEntryOffset += RelRelocation.ENTRY_SIZE;
                    offsetDiff -= 0xFFFF;
                }

                if (offsetDiff < 0)
                    throw new InvalidOperationException(
                        "Negative offset difference between relocations. Relocations not properly sorted.");

                entry.OffsetOfCurrRelocationFromPrev = (ushort)offsetDiff;
                entry.Save(data, nextRelocationEntryOffset);
                prevRelocationOffset = entry.RelocationOffset;
                nextRelocationEntryOffset += RelRelocation.ENTRY_SIZE;
            }

            // Write R_DOLPHIN_END to terminate this module's relocation list
            var endEntry = new RelRelocation
            {
                RelocationType = RelRelocationType.R_DOLPHIN_END,
                SectionNumToRelocateAgainst = (byte)(currSectionNum ?? 0),
            };
            endEntry.Save(data, nextRelocationEntryOffset);
            nextRelocationEntryOffset += RelRelocation.ENTRY_SIZE;

            // fix_size tracks the end of relocation data for non-self, non-main modules
            if (moduleNum != 0 && moduleNum != Id)
                FixSize = (uint)nextRelocationEntryOffset;
        }

        // Write prolog/epilog/unresolved
        BigEndianIO.WriteU8(data, 0x30, PrologSection);
        BigEndianIO.WriteU8(data, 0x31, EpilogSection);
        BigEndianIO.WriteU8(data, 0x32, UnresolvedSection);
        BigEndianIO.WriteU32(data, 0x34, PrologOffset);
        BigEndianIO.WriteU32(data, 0x38, EpilogOffset);
        BigEndianIO.WriteU32(data, 0x3C, UnresolvedOffset);

        BigEndianIO.WriteU32(data, 0x40, Alignment);
        BigEndianIO.WriteU32(data, 0x44, BssAlignment);

        BigEndianIO.WriteU32(data, 0x48, FixSize);
    }

    /// <summary>
    /// Serializes the REL and writes it to disk.
    /// </summary>
    public void SaveToFile(string filePath, bool preserveSectionDataOffsets = false)
    {
        SaveChanges(preserveSectionDataOffsets);

        byte[] bytes = BigEndianIO.ReadAllBytes(_data);
        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>
    /// Returns the raw serialized data after a SaveChanges call.
    /// </summary>
    public byte[] GetData()
    {
        return BigEndianIO.ReadAllBytes(_data);
    }
}

/// <summary>
/// A dictionary that preserves insertion order and supports reordering by remove-and-re-add.
/// Used to replicate Python's OrderedDict behavior for relocation module ordering.
/// </summary>
public class OrderedDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    private readonly List<KeyValuePair<TKey, TValue>> _list = new();
    private readonly Dictionary<TKey, int> _index = new();

    public int Count => _list.Count;

    public TValue this[TKey key]
    {
        get => _list[_index[key]].Value;
        set
        {
            if (_index.TryGetValue(key, out int idx))
            {
                _list[idx] = new KeyValuePair<TKey, TValue>(key, value);
            }
            else
            {
                _index[key] = _list.Count;
                _list.Add(new KeyValuePair<TKey, TValue>(key, value));
            }
        }
    }

    public bool ContainsKey(TKey key) => _index.ContainsKey(key);

    public bool Remove(TKey key)
    {
        if (!_index.TryGetValue(key, out int idx))
            return false;

        _list.RemoveAt(idx);
        _index.Remove(key);

        // Rebuild indices for items after the removed one
        for (int i = idx; i < _list.Count; i++)
            _index[_list[i].Key] = i;

        return true;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _list.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
