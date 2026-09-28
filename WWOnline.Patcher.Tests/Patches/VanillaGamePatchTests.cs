using WWOnline.Patcher.BinaryFormats;
using WWOnline.Patcher.BinaryFormats.Bmg;
using WWOnline.Patcher.BinaryFormats.Rarc;
using WWOnline.Patcher.BinaryFormats.Rel;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Tools;
using Xunit;

namespace WWOnline.Patcher.Tests.Patches;

/// <summary>
/// Optional patches against the real vanilla files (skipped when no vanilla game is configured —
/// Nintendo's files are never in the repo). Only temp copies are written.
/// </summary>
public class VanillaGamePatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-vanilla-patch-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private const uint PuppetModuleId = 0x58;

    /// <summary>
    /// Selecting every optional patch must keep RELS.arc inside the ARAM guard. Uses the assembled
    /// diffs when a build has produced them, otherwise a 4-byte write at every literal .org of each
    /// RELS.arc REL a patch targets (the edited entry is re-compressed either way, which is what
    /// costs bytes). The puppet REL from GameMod/build is inserted too when present.
    /// </summary>
    [VanillaGameFact]
    public void EveryOptionalPatch_KeepsRelsArcWithinTheAramGuard()
    {
        var vanillaPath = Path.Combine(TestRepo.VanillaGamePath()!, "files", "RELS.arc");
        var vanillaSize = new FileInfo(vanillaPath).Length;
        var arc = RarcArchive.FromFile(vanillaPath);

        var puppet = Path.Combine(TestRepo.GameMod, "build", "d_a_puppet.rel");
        if (File.Exists(puppet)) Assert.True(arc.ReplaceRelById(PuppetModuleId, File.ReadAllBytes(puppet)));

        var config = TestRepo.Config();
        var catalog = config.LoadOptionalPatchCatalog();
        var chunksByRel = new Dictionary<string, List<PatchChunk>>();
        var chosen = new List<string>();
        foreach (var patch in catalog.Patches)
        {
            if (chosen.Any(c => catalog.ConflictsOf(patch.Id).Contains(c))) continue; // one of each conflicting group
            chosen.Add(patch.Id);
            var diffPath = PatchPlan.OptionalDiffPath(config.PatchDiffsPath, patch.Id);
            IEnumerable<(string File, PatchChunk Chunk)> chunks = File.Exists(diffPath)
                ? PatchDiffFile.Load(diffPath, true).Files.SelectMany(f => f.Value.Select(c => (f.Key, c)))
                : patch.Orgs.Where(o => o.Address != null && o.File.EndsWith(".rel"))
                    .Select(o => (o.File, new PatchChunk(o.Address!.Value, [0x60, 0, 0, 0], [])));
            foreach (var (file, chunk) in chunks.Where(c => c.File.StartsWith("files/rels/")))
            {
                var name = file["files/rels/".Length..];
                if (arc.GetFileEntry(name) == null) continue; // loose REL: not in ARAM
                if (!chunksByRel.TryGetValue(name, out var list)) chunksByRel[name] = list = [];
                list.Add(chunk with { Relocations = [] }); // relocation re-targeting doesn't change sizes
            }
        }

        Assert.NotEmpty(chunksByRel);
        foreach (var (name, chunks) in chunksByRel)
        {
            var entry = arc.GetFileEntry(name)!;
            entry.Data = RelBytePatcher.Apply(entry.Data!, chunks).Data;
        }
        var growth = arc.SaveChanges().Length - vanillaSize;
        Assert.True(growth <= ElfToRelConverter.MaxRelsArcGrowthBytes,
            $"RELS.arc grows by {growth} bytes with every optional patch (limit {ElfToRelConverter.MaxRelsArcGrowthBytes})");
    }

    /// <summary>The @edit kinds applied to temp copies of the real files, then undone by the reset.</summary>
    [VanillaGameFact]
    public void FileEdits_ApplyToTheRealFiles_AndResetRestoresVanilla()
    {
        var vanilla = TestRepo.VanillaGamePath()!;
        var game = Path.Combine(_root, "game");
        string[] files = ["sys/main.dol", "files/res/Msg/bmgres.arc", "files/res/Stage/sea/Room1.arc"];
        foreach (var f in files)
        {
            var dst = Path.Combine(game, f);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(Path.Combine(vanilla, f), dst);
        }

        OptionalPatch Edit(string id, string edit) => OptionalPatchCatalog.Parse(id,
            $"; @name {id}\n; @description d\n; @category c\n; @default off\n; @match no\n; @multiplayer m\n; @credit MIT\n; @edit {edit}\n", id + ".asm");
        var patches = new[]
        {
            Edit("instant", "bmg-instant-text"),
            Edit("rename", "bmg-message 463 Swift Sail"),
            Edit("door", "dzb-face-property files/res/Stage/sea/Room1.arc room.dzb 0x1493 0x11"),
        };

        var config = TestRepo.Config();
        new GamePatchApplier(ProtectedRegions.FromPuppetSharedHeader(config.PuppetSharedHeaderPath))
            .Apply([], patches, new GamePatchApplier.Targets(game, vanilla, config.FreeSpaceOffsetsPath, config.AssetsPath));

        var bmg = new BmgFile(RarcArchive.FromFile(Path.Combine(game, "files/res/Msg/bmgres.arc")).GetFileEntry(PatchEdit.BmgEntry)!.Data!);
        Assert.Equal("Swift Sail"u8.ToArray(), bmg.FindById(463)!.Text);
        Assert.All(bmg.Messages, m => Assert.Equal(1, m.InitialDrawType));
        Assert.DoesNotContain(bmg.Messages, m => m.Text.AsSpan().IndexOf(new byte[] { 0x1A, 0x07, 0x00, 0x00, 0x07 }) >= 0);

        var room = RarcArchive.FromFile(Path.Combine(game, "files/res/Stage/sea/Room1.arc"));
        var dzb = room.GetFileEntry("room.dzb")!.Data!;
        Assert.Equal(0x11, BigEndianIO.ReadU16(dzb, (int)BigEndianIO.ReadU32(dzb, 0x0C) + 0x1493 * 0xA + 6));
        var vanillaRoom = RarcArchive.FromFile(Path.Combine(vanilla, "files/res/Stage/sea/Room1.arc"));
        foreach (var entry in vanillaRoom.FileEntries.Where(e => !e.IsDir && e.Name != "room.dzb"))
            Assert.Equal(entry.Data, room.GetFileEntry(entry.Name)!.Data);

        var restored = GamePatchApplier.RestoreTouchedFiles(new OptionalPatchCatalog(patches), game, vanilla);
        Assert.Equal(2, restored.Count);
        foreach (var f in files.Skip(1))
            Assert.Equal(File.ReadAllBytes(Path.Combine(vanilla, f)), File.ReadAllBytes(Path.Combine(game, f)));
        Assert.Empty(GamePatchApplier.RestoreTouchedFiles(new OptionalPatchCatalog(patches), game, vanilla)); // already vanilla
    }
}
