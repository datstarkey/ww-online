using WWOnline.Patcher.BinaryFormats.Yaz0;
using WWOnline.Patcher.Patches;

namespace WWOnline.Patcher.BinaryFormats.Rel;

/// <summary>
/// Applies assembled patch chunks to a REL *in place*: the bytes are written at their REL file
/// offsets and the relocation table is edited without re-serialising the REL, so everything
/// the patch doesn't touch stays byte-identical to vanilla.
///
/// Relocations matter because the REL loader (OSLink) rewrites every relocated field when the
/// module is linked — a vanilla relocation left under patched bytes would overwrite them (e.g.
/// nop-ing a <c>bl</c> into main.dol gives a garbage instruction). So:
/// <list type="bullet">
/// <item>vanilla relocations whose field overlaps a chunk become R_DOLPHIN_NOP (the entry still
/// advances the offset chain, so later entries are unaffected — tww-decomp OSLink.c);</item>
/// <item>a chunk relocation (a <c>bl</c> from REL code into main.dol) re-targets the vanilla
/// module-0 relocation of the same type at the same offset. Adding new relocations would need
/// the table rebuilt; that isn't supported and fails loudly.</item>
/// </list>
/// Adapted from the REL patching in wwrando (LagoLunatic) / betterww (WideBoner), MIT.
/// </summary>
public static class RelBytePatcher
{
    public sealed record Result(byte[] Data, int RelocationsDisabled, int RelocationsRetargeted);

    private sealed record RelocEntry(int EntryOffset, uint Module, RelRelocationType Type, uint Position);

    /// <param name="relData">The REL as stored (Yaz0-compressed or not).</param>
    /// <param name="chunks">Chunks keyed by uncompressed REL file offset.</param>
    /// <returns>The patched REL, re-compressed with Yaz0 when the input was compressed.</returns>
    public static Result Apply(byte[] relData, IEnumerable<PatchChunk> chunks)
    {
        var wasCompressed = Yaz0Codec.CheckIsCompressed(relData);
        var data = wasCompressed ? Yaz0Codec.Decompress(relData) : (byte[])relData.Clone();
        var (disabled, retargeted) = ApplyUncompressed(data, chunks.ToList());
        var output = wasCompressed ? new Yaz0Codec().Compress(data) : data;
        return new Result(output, disabled, retargeted);
    }

    /// <summary>Patch an uncompressed REL buffer in place. Returns (relocations disabled, retargeted).</summary>
    public static (int Disabled, int Retargeted) ApplyUncompressed(byte[] data, IReadOnlyList<PatchChunk> chunks)
    {
        var sections = ReadSections(data);
        foreach (var chunk in chunks)
        {
            var inSection = sections.Any(s => s.Offset != 0 && chunk.Address >= s.Offset && chunk.End <= s.Offset + s.Length);
            if (!inSection)
                throw new InvalidOperationException(
                    $"REL patch at 0x{chunk.Address:X} (+{chunk.Data.Length} bytes) is not inside an initialised REL section " +
                    "(REL free space / new sections aren't supported)");
            chunk.Data.CopyTo(data, (int)chunk.Address);
        }

        var entries = ReadRelocations(data, sections);
        var handled = new HashSet<int>();
        int retargeted = 0;
        foreach (var chunk in chunks)
        {
            foreach (var reloc in chunk.Relocations)
            {
                var position = chunk.Address + reloc.Offset;
                var type = Enum.Parse<RelRelocationType>(reloc.Type);
                var existing = entries.FirstOrDefault(e => e.Module == 0 && e.Position == position && e.Type == type);
                if (existing == null)
                    throw new InvalidOperationException(
                        $"REL patch needs a {reloc.Type} relocation to {reloc.Symbol} at 0x{position:X}, but vanilla has no main.dol " +
                        $"{reloc.Type} relocation there to re-target. Adding REL relocations isn't supported — hook a call site that " +
                        "already calls into main.dol.");
                BigEndianIO.WriteU32(data, existing.EntryOffset + 4, reloc.Address);
                handled.Add(existing.EntryOffset);
                retargeted++;
            }
        }

        int disabled = 0;
        foreach (var entry in entries)
        {
            if (handled.Contains(entry.EntryOffset)) continue;
            var start = entry.Position;
            var end = start + FieldSize(entry.Type);
            if (!chunks.Any(c => start < c.End && end > c.Address)) continue;
            data[entry.EntryOffset + 2] = (byte)RelRelocationType.R_DOLPHIN_NOP;
            disabled++;
        }
        return (disabled, retargeted);
    }

    private static uint FieldSize(RelRelocationType type) => type switch
    {
        RelRelocationType.R_PPC_ADDR16 or RelRelocationType.R_PPC_ADDR16_LO or
        RelRelocationType.R_PPC_ADDR16_HI or RelRelocationType.R_PPC_ADDR16_HA => 2,
        _ => 4,
    };

    private sealed record SectionInfo(uint Offset, uint Length);

    private static List<SectionInfo> ReadSections(byte[] data)
    {
        var count = BigEndianIO.ReadU32(data, 0x0C);
        var table = BigEndianIO.ReadU32(data, 0x10);
        var list = new List<SectionInfo>();
        for (int i = 0; i < count; i++)
        {
            var offset = BigEndianIO.ReadU32(data, (int)table + i * 8) & ~1u;
            var length = BigEndianIO.ReadU32(data, (int)table + i * 8 + 4);
            list.Add(new SectionInfo(offset, length));
        }
        return list;
    }

    /// <summary>Walk every module's relocation list the way OSLink does (tww-decomp src/dolphin/os/OSLink.c).</summary>
    private static List<RelocEntry> ReadRelocations(byte[] data, List<SectionInfo> sections)
    {
        var result = new List<RelocEntry>();
        var impOffset = BigEndianIO.ReadU32(data, 0x28);
        var impSize = BigEndianIO.ReadU32(data, 0x2C);
        for (var imp = impOffset; imp < impOffset + impSize; imp += 8)
        {
            var module = BigEndianIO.ReadU32(data, (int)imp);
            var entryOffset = (int)BigEndianIO.ReadU32(data, (int)imp + 4);
            uint sectionBase = 0, position = 0;
            while (true)
            {
                var delta = BigEndianIO.ReadU16(data, entryOffset);
                var type = (RelRelocationType)data[entryOffset + 2];
                if (type == RelRelocationType.R_DOLPHIN_END) break;
                if (type == RelRelocationType.R_DOLPHIN_SECTION)
                {
                    sectionBase = sections[data[entryOffset + 3]].Offset;
                    position = 0;
                }
                else
                {
                    position += delta;
                    if (type != RelRelocationType.R_DOLPHIN_NOP)
                        result.Add(new RelocEntry(entryOffset, module, type, sectionBase + position));
                }
                entryOffset += RelRelocation.ENTRY_SIZE;
            }
        }
        return result;
    }
}
