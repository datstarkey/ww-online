using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Bmg;
using WWOnline.Patcher.BinaryFormats.Dol;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.BinaryFormats.Rel;
using WWOnline.Patcher.BinaryFormats.Yaz0;
using WWOnline.Patcher.Tools;

namespace WWOnline.Patcher.Patches;

/// <summary>
/// Applies assembled patch diffs (required + selected optional) and the selected patches'
/// @edit file edits to a game folder. Shared by the pipeline (ApplyPatchesStep) and the client's
/// pre-built fallback so both behave identically.
///
/// Safety rules enforced here (GameMod/CLAUDE.md):
/// <list type="bullet">
/// <item>no patch writes .sbss2 or the scratch region; only required patches write the hook;</item>
/// <item>two patches never write the same bytes (a conflict fails the build instead of one
/// silently winning);</item>
/// <item>RELs inside RELS.arc are patched one entry at a time and re-compressed; other entries
/// are never decompressed, and RELS.arc must stay within the ARAM growth guard.</item>
/// </list>
/// </summary>
public sealed class GamePatchApplier
{
    public const string RelsArcGamePath = "files/RELS.arc";
    private const string MainDolGamePath = "sys/main.dol";
    private const string RelsDirPrefix = "files/rels/";

    /// <summary>Files bigger than this are compared by size only when deciding whether to restore them.</summary>
    private const long FullCompareLimit = 64L * 1024 * 1024;

    private readonly ProtectedRegions _regions;
    private readonly Action<string> _log;

    public GamePatchApplier(ProtectedRegions regions, Action<string>? log = null)
    {
        _regions = regions;
        _log = log ?? (_ => { });
    }

    /// <param name="GamePath">Output game folder (sys/, files/) — already reset to vanilla.</param>
    /// <param name="VanillaGamePath">Extracted vanilla game (for the RELS.arc growth guard).</param>
    /// <param name="FreeSpaceOffsetsPath">free_space_start_offsets.txt.</param>
    /// <param name="AssetsPath">GameMod/assets (or PatchData/assets) for replace-file edits.</param>
    public sealed record Targets(string GamePath, string VanillaGamePath, string FreeSpaceOffsetsPath, string AssetsPath);

    /// <summary>
    /// Check every chunk against the protected regions and against every other patch's chunks.
    /// Throws InvalidOperationException naming the offending patch(es).
    /// </summary>
    public static void Validate(IReadOnlyList<PatchDiffFile> diffs, ProtectedRegions regions)
    {
        var writes = new List<(string File, uint Start, uint End, string Patch)>();
        foreach (var diff in diffs)
        {
            foreach (var (file, chunks) in diff.Files)
            {
                foreach (var chunk in chunks)
                {
                    if (chunk.Data.Length == 0) continue;
                    if (file == MainDolGamePath)
                    {
                        var hit = regions.FindViolation(chunk.Address, chunk.End, diff.IsOptional);
                        if (hit != null)
                            throw new InvalidOperationException(
                                $"Patch {diff.PatchName} writes 0x{chunk.Address:X8}..0x{chunk.End - 1:X8}, inside the {hit}");
                    }
                    writes.Add((file, chunk.Address, chunk.End, diff.PatchName));
                }
            }
        }

        foreach (var group in writes.GroupBy(w => w.File))
        {
            var sorted = group.OrderBy(w => w.Start).ToList();
            for (int i = 1; i < sorted.Count; i++)
            {
                for (int j = i - 1; j >= 0 && sorted[j].End > sorted[i].Start; j--)
                {
                    if (sorted[j].Patch == sorted[i].Patch) continue;
                    throw new InvalidOperationException(
                        $"Patches {sorted[j].Patch} and {sorted[i].Patch} both write {group.Key} at 0x{Math.Max(sorted[i].Start, sorted[j].Start):X}. " +
                        "They can't be selected together.");
                }
            }
        }
    }

    public void Apply(IReadOnlyList<PatchDiffFile> diffs, IReadOnlyList<OptionalPatch> selectedPatches, Targets targets)
    {
        Validate(diffs, _regions);
        ApplyDol(diffs, targets);
        ApplyRels(diffs, targets);
        ApplyEdits(selectedPatches, targets);
    }

    private void ApplyDol(IReadOnlyList<PatchDiffFile> diffs, Targets targets)
    {
        var dolPath = Path.Combine(targets.GamePath, "sys", "main.dol");
        var patcher = new DolPatcher(dolPath);
        patcher.LoadFreeSpaceOffsets(targets.FreeSpaceOffsetsPath);
        foreach (var diff in diffs)
        {
            if (!diff.Files.TryGetValue(MainDolGamePath, out var chunks)) continue;
            foreach (var chunk in chunks)
            {
                if (chunk.Relocations.Count > 0)
                    throw new InvalidOperationException($"{diff.PatchName}: main.dol chunks can't carry REL relocations");
                patcher.ApplyPatch(chunk.Address, chunk.Data);
            }
            _log($"main.dol: applied {diff.PatchName} ({chunks.Count} chunk(s))");
        }
        patcher.SaveToFile(dolPath);
    }

    private void ApplyRels(IReadOnlyList<PatchDiffFile> diffs, Targets targets)
    {
        var relChunks = diffs
            .SelectMany(d => d.Files.Where(f => f.Key != MainDolGamePath).Select(f => (Patch: d.PatchName, File: f.Key, Chunks: f.Value)))
            .ToList();
        if (relChunks.Count == 0) return;

        var relsArcPath = Path.Combine(targets.GamePath, "files", "RELS.arc");
        RarcArchive? relsArc = null;
        foreach (var group in relChunks.GroupBy(r => r.File))
        {
            if (!group.Key.StartsWith(RelsDirPrefix, StringComparison.Ordinal) || !group.Key.EndsWith(".rel", StringComparison.Ordinal))
                throw new InvalidOperationException($"Patches can only target sys/main.dol and files/rels/*.rel, not {group.Key}");
            var relName = group.Key[RelsDirPrefix.Length..];
            var chunks = group.SelectMany(g => g.Chunks).ToList();
            var patchNames = string.Join(", ", group.Select(g => g.Patch).Distinct());

            if (relsArc == null && File.Exists(relsArcPath)) relsArc = RarcArchive.FromFile(relsArcPath);
            var entry = relsArc?.GetFileEntry(relName);
            if (entry != null)
            {
                // The game loads a REL from RELS.arc before looking for a loose file, so patch that copy.
                var result = RelBytePatcher.Apply(entry.Data!, chunks);
                _log($"RELS.arc/{relName}: {patchNames} ({entry.Data!.Length} -> {result.Data.Length} bytes, " +
                     $"{result.RelocationsDisabled} relocation(s) disabled, {result.RelocationsRetargeted} re-targeted)");
                entry.Data = result.Data;
                continue;
            }

            var loosePath = Path.Combine(targets.GamePath, "files", "rels", relName);
            if (!File.Exists(loosePath))
                throw new FileNotFoundException($"{relName} is neither in RELS.arc nor at {loosePath}");
            var loose = RelBytePatcher.Apply(File.ReadAllBytes(loosePath), chunks);
            File.WriteAllBytes(loosePath, loose.Data);
            _log($"files/rels/{relName}: {patchNames} ({loose.RelocationsDisabled} relocation(s) disabled, {loose.RelocationsRetargeted} re-targeted)");
        }

        if (relsArc == null) return;
        var saved = relsArc.SaveChanges();
        var vanillaArc = Path.Combine(targets.VanillaGamePath, "files", "RELS.arc");
        if (File.Exists(vanillaArc))
        {
            var growth = saved.Length - new FileInfo(vanillaArc).Length;
            if (growth > ElfToRelConverter.MaxRelsArcGrowthBytes)
                throw new InvalidOperationException(
                    $"RELS.arc would be {growth} bytes bigger than vanilla (limit {ElfToRelConverter.MaxRelsArcGrowthBytes}); " +
                    "ARAM can't take it. Deselect some REL-editing optional patches or shrink the puppet REL.");
        }
        File.WriteAllBytes(relsArcPath, saved);
    }

    private void ApplyEdits(IReadOnlyList<OptionalPatch> selectedPatches, Targets targets)
    {
        var edits = selectedPatches.SelectMany(p => p.Edits.Select(e => (Patch: p.Id, Edit: e))).ToList();
        foreach (var group in edits.GroupBy(e => e.Edit.TargetFile, StringComparer.Ordinal))
        {
            var gameFile = Path.Combine(targets.GamePath, group.Key.Replace('/', Path.DirectorySeparatorChar));
            var replaces = group.Where(g => g.Edit is ReplaceFileEdit).ToList();
            if (replaces.Count > 0)
            {
                if (replaces.Count != group.Count())
                    throw new InvalidOperationException($"{group.Key}: replace-file can't be combined with other edits of the same file");
                var (patch, edit) = replaces[^1];
                var asset = Path.Combine(targets.AssetsPath, ((ReplaceFileEdit)edit).AssetName);
                if (!File.Exists(asset)) throw new FileNotFoundException($"{patch}: asset not found: {asset}");
                Directory.CreateDirectory(Path.GetDirectoryName(gameFile)!);
                File.Copy(asset, gameFile, overwrite: true);
                _log($"{group.Key}: replaced with {Path.GetFileName(asset)} ({patch})");
                continue;
            }

            if (!File.Exists(gameFile)) throw new FileNotFoundException($"Game file for @edit not found: {gameFile}");
            var raw = File.ReadAllBytes(gameFile);
            var archiveCompressed = Yaz0Codec.CheckIsCompressed(raw);
            var arc = new RarcArchive();
            arc.Read(raw);
            foreach (var (patch, edit) in group)
            {
                ApplyArchiveEdit(arc, edit);
                _log($"{group.Key}: {edit.Describe()} ({patch})");
            }
            var saved = arc.SaveChanges();
            File.WriteAllBytes(gameFile, archiveCompressed ? new Yaz0Codec().Compress(saved) : saved);
        }
    }

    private static void ApplyArchiveEdit(RarcArchive arc, PatchEdit edit)
    {
        switch (edit)
        {
            case BmgInstantTextEdit:
                EditEntry(arc, PatchEdit.BmgEntry, data =>
                {
                    var bmg = new BmgFile(data);
                    foreach (var m in bmg.Messages)
                    {
                        m.InitialDrawType = 1; // draw the whole box at once
                        m.RemoveControlCodes([0x1A, 0x07, 0x00, 0x00, 0x07]); // wait N frames
                        m.RemoveControlCodes([0x1A, 0x07, 0x00, 0x00, 0x03]); // wait N frames then dismiss (prompt)
                    }
                    return bmg.Save();
                });
                break;
            case BmgMessageTextEdit rename:
                EditEntry(arc, PatchEdit.BmgEntry, data =>
                {
                    var bmg = new BmgFile(data);
                    var message = bmg.FindById(rename.MessageId)
                        ?? throw new InvalidOperationException($"BMG message {rename.MessageId} not found");
                    if (rename.Text.Any(c => c > 0x7E || c < 0x20))
                        throw new InvalidOperationException($"bmg-message text must be printable ASCII: \"{rename.Text}\"");
                    message.Text = System.Text.Encoding.ASCII.GetBytes(rename.Text);
                    return bmg.Save();
                });
                break;
            case DzbFacePropertyEdit dzb:
                EditEntry(arc, dzb.EntryName, data =>
                {
                    // DZB: 0x0C = offset of the face list; each face is 0xA bytes, +6 = property index
                    // (wwrando fix_forsaken_fortress_door_softlock).
                    var faceCount = BigEndianIO.ReadU32(data, 0x08);
                    if (dzb.FaceIndex >= faceCount)
                        throw new InvalidOperationException($"{dzb.EntryName}: face 0x{dzb.FaceIndex:X} out of range (0x{faceCount:X} faces)");
                    var faceOffset = (int)BigEndianIO.ReadU32(data, 0x0C) + dzb.FaceIndex * 0xA;
                    BigEndianIO.WriteU16(data, faceOffset + 6, dzb.PropertyIndex);
                    return data;
                });
                break;
            default:
                throw new InvalidOperationException($"Unsupported archive edit {edit.Describe()}");
        }
    }

    /// <summary>Edit one archive entry, keeping its Yaz0 compression state; other entries are untouched.</summary>
    private static void EditEntry(RarcArchive arc, string entryName, Func<byte[], byte[]> edit)
    {
        var entry = arc.GetFileEntry(entryName) ?? throw new InvalidOperationException($"Archive has no {entryName}");
        var compressed = Yaz0Codec.CheckIsCompressed(entry.Data!);
        var data = compressed ? Yaz0Codec.Decompress(entry.Data!) : (byte[])entry.Data!.Clone();
        var edited = edit(data);
        entry.Data = compressed ? new Yaz0Codec().Compress(edited) : edited;
    }

    /// <summary>
    /// Restore every game file an optional patch could have changed (loose RELs, archives, replaced
    /// files) from the vanilla game, so a patch that is no longer selected is really undone.
    /// main.dol / RELS.arc / bi2.bin are always reset separately. Returns the files copied.
    /// </summary>
    public static IReadOnlyList<string> RestoreTouchedFiles(OptionalPatchCatalog catalog, string gamePath, string vanillaGamePath, Action<string>? log = null)
    {
        log ??= _ => { };
        var restored = new List<string>();
        HashSet<string>? relsInArc = null;
        foreach (var file in catalog.TouchedGameFiles)
        {
            if (file == RelsArcGamePath || file == MainDolGamePath) continue;
            if (file.StartsWith(RelsDirPrefix, StringComparison.Ordinal))
            {
                relsInArc ??= ListRelsArcEntries(Path.Combine(vanillaGamePath, "files", "RELS.arc"));
                if (relsInArc.Contains(file[RelsDirPrefix.Length..])) continue; // reset with RELS.arc
            }

            var vanilla = Path.Combine(vanillaGamePath, file.Replace('/', Path.DirectorySeparatorChar));
            var target = Path.Combine(gamePath, file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(vanilla))
            {
                log($"{file}: no vanilla copy to restore from (only needed if a patch that edits it is selected)");
                continue;
            }
            if (FilesMatch(vanilla, target)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(vanilla, target, overwrite: true);
            restored.Add(file);
            log($"{file}: restored from vanilla");
        }
        return restored;
    }

    private static HashSet<string> ListRelsArcEntries(string relsArcPath)
    {
        if (!File.Exists(relsArcPath)) return [];
        return RarcArchive.FromFile(relsArcPath).FileEntries.Where(e => !e.IsDir).Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
    }

    private static bool FilesMatch(string a, string b)
    {
        if (!File.Exists(b)) return false;
        var la = new FileInfo(a).Length;
        if (la != new FileInfo(b).Length) return false;
        if (la > FullCompareLimit) return true;
        return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }
}
