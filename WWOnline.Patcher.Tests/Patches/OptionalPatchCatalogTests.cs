using WWOnline.Patcher.BinaryFormats.Dol;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Tools;
using Xunit;

namespace WWOnline.Patcher.Tests.Patches;

public class OptionalPatchCatalogTests
{
    private const string Header = """
        ; @name        Test patch
        ; @description First line.
        ; @description Second line.
        ; @category    Speed
        ; @default     on
        ; @match       yes
        ; @multiplayer Everyone should pick it.
        ; @conflicts   other_patch
        ; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
        ;
        ; Free comment, not a tag.

        .open "sys/main.dol"
        .org 0x80232C78 ; comment
          nop
        .org @NextFreeSpace
          nop
        .close
        .open "files/rels/d_a_ship.rel"
        .org 0x29EC
          b 0x2A50
        .close
        """;

    private static OptionalPatch Make(string id, bool defaultOn = false, string extra = "", string body = ".open \"sys/main.dol\"\n.org 0x80000000\nnop\n.close\n") =>
        OptionalPatchCatalog.Parse(id, $"""
            ; @name        {id}
            ; @description d
            ; @category    c
            ; @default     {(defaultOn ? "on" : "off")}
            ; @match       no
            ; @credit      MIT
            {extra}
            {body}
            """, id + ".asm");

    [Fact]
    public void Parse_ReadsEveryHeaderTag()
    {
        var p = OptionalPatchCatalog.Parse("test_patch", Header, "test_patch.asm");

        Assert.Equal("test_patch", p.Id);
        Assert.Equal("Test patch", p.Name);
        Assert.Equal("First line. Second line.", p.Description);
        Assert.Equal("Speed", p.Category);
        Assert.True(p.DefaultEnabled);
        Assert.True(p.AllPlayersShouldMatch);
        Assert.Equal("Everyone should pick it.", p.MultiplayerNote);
        Assert.Equal(["other_patch"], p.Conflicts);
        Assert.Contains("MIT", p.Credit);
        Assert.Equal(["sys/main.dol", "files/rels/d_a_ship.rel"], p.AsmTargetFiles);
        Assert.Equal([new PatchOrg("sys/main.dol", 0x80232C78), new PatchOrg("sys/main.dol", null), new PatchOrg("files/rels/d_a_ship.rel", 0x29EC)], p.Orgs);
        Assert.True(p.HasAsm);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("category")]
    [InlineData("default")]
    [InlineData("match")]
    [InlineData("credit")]
    public void Parse_MissingRequiredTag_Throws(string tag)
    {
        var text = string.Join('\n', Header.Split('\n').Where(l => !l.StartsWith($"; @{tag} ")));
        Assert.Throws<InvalidDataException>(() => OptionalPatchCatalog.Parse("test_patch", text, "x.asm"));
    }

    [Theory]
    [InlineData("; @nmae typo")]
    [InlineData("; @default maybe")]
    [InlineData("; @edit teleport-link")]
    public void Parse_BadHeader_Throws(string line)
    {
        Assert.Throws<InvalidDataException>(() => OptionalPatchCatalog.Parse("test_patch", line + "\n" + Header, "x.asm"));
    }

    [Fact]
    public void Parse_IdMustBeSnakeCase()
    {
        Assert.Throws<InvalidDataException>(() => OptionalPatchCatalog.Parse("Bad-Id", Header, "Bad-Id.asm"));
    }

    [Fact]
    public void Parse_EditOnlyPatch_HasNoAsm()
    {
        var p = Make("videos", body: "", extra: "; @edit replace-file files/thpdemo/title_loop.thp blank.thp\n; @edit bmg-message 463 Swift Sail");
        Assert.False(p.HasAsm);
        Assert.Equal(new ReplaceFileEdit("files/thpdemo/title_loop.thp", "blank.thp"), p.Edits[0]);
        Assert.Equal(new BmgMessageTextEdit(463, "Swift Sail"), p.Edits[1]);
        Assert.Equal(["files/thpdemo/title_loop.thp", PatchEdit.BmgArchive], p.TouchedFiles);
    }

    [Fact]
    public void Parse_NeitherAsmNorEdit_Throws()
    {
        Assert.Throws<InvalidDataException>(() => Make("empty", body: ""));
    }

    [Fact]
    public void Resolve_NothingSaved_UsesDefaults()
    {
        var catalog = new OptionalPatchCatalog([Make("a", defaultOn: true), Make("b"), Make("c", defaultOn: true)]);
        Assert.Equal(["a", "c"], catalog.Resolve(null).Ids);
        Assert.Equal(["a", "c"], catalog.DefaultIds);
    }

    [Fact]
    public void Resolve_KeepsKnownIdsSortedAndReportsUnknown()
    {
        var catalog = new OptionalPatchCatalog([Make("a"), Make("b")]);
        var selection = catalog.Resolve(["b", "zzz", "a", "b", " "]);
        Assert.Equal(["a", "b"], selection.Ids);
        Assert.Equal(["zzz"], selection.UnknownIds);
        Assert.Empty(catalog.Resolve([]).Ids); // an explicit empty selection is "none", not defaults
    }

    [Fact]
    public void Conflicts_AreSymmetric()
    {
        var catalog = new OptionalPatchCatalog([Make("a", extra: "; @conflicts b"), Make("b"), Make("c")]);
        Assert.Equal([("a", "b")], catalog.FindConflicts(["b", "a", "c"]));
        Assert.Equal(["a"], catalog.ConflictsOf("b"));
        Assert.Throws<InvalidOperationException>(() => PatchPlan.ResolveStrict(catalog, ["a", "b"]));
    }

    [Fact]
    public void Catalog_RejectsConflictWithUnknownPatch()
    {
        Assert.Throws<InvalidDataException>(() => new OptionalPatchCatalog([Make("a", extra: "; @conflicts nope")]));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("default", null)]
    [InlineData("none", "")]
    [InlineData("skip_intro, instant_text", "skip_intro|instant_text")]
    public void ParseIdList(string? text, string? expected)
    {
        var result = OptionalPatchCatalog.ParseIdList(text);
        if (expected == null) Assert.Null(result);
        else Assert.Equal(expected.Length == 0 ? [] : expected.Split('|'), result);
    }

    // ---- the real catalogue in GameMod/src/patches/optional ----

    private static OptionalPatchCatalog Repo => TestRepo.Config().LoadOptionalPatchCatalog();

    [Fact]
    public void RepoCatalogue_Loads_WithSkipIntroOnByDefault()
    {
        var catalog = Repo;
        Assert.True(catalog.Patches.Count >= 20);
        Assert.Contains("skip_intro", catalog.DefaultIds);
        Assert.All(catalog.Patches, p => Assert.Contains("MIT", p.Credit));
        Assert.All(catalog.Patches, p => Assert.False(string.IsNullOrWhiteSpace(p.MultiplayerNote), p.Id + " needs a @multiplayer note"));
        Assert.Empty(catalog.FindConflicts(catalog.DefaultIds));
    }

    [Fact]
    public void RepoCatalogue_ReplaceFileAssetsExist()
    {
        foreach (var edit in Repo.Patches.SelectMany(p => p.Edits).OfType<ReplaceFileEdit>())
            Assert.True(File.Exists(Path.Combine(TestRepo.Config().AssetsPath, edit.AssetName)), edit.AssetName);
    }

    [Fact]
    public void RepoCatalogue_RequiredPatchesAreNotOptional()
    {
        var ids = Repo.Patches.Select(p => p.Id).ToList();
        Assert.DoesNotContain("use_extra_memory", ids);
        Assert.DoesNotContain("link_draw_hook", ids);
        Assert.False(File.Exists(Path.Combine(TestRepo.Config().PatchesSrcPath, "skip_intro.asm")),
            "skip_intro is optional now; a copy in src/patches/ would make it required again");
    }

    [Fact]
    public void RepoCatalogue_NoOptionalPatchWritesProtectedRegions()
    {
        var config = TestRepo.Config();
        var regions = ProtectedRegions.FromPuppetSharedHeader(config.PuppetSharedHeaderPath);
        foreach (var patch in Repo.Patches)
        {
            foreach (var org in patch.Orgs.Where(o => o.File == "sys/main.dol" && o.Address != null))
            {
                // .org gives the start; 4 bytes covers the first instruction/word written there.
                var hit = regions.FindViolation(org.Address!.Value, org.Address.Value + 4, isOptionalPatch: true);
                Assert.True(hit == null, $"{patch.Id} writes 0x{org.Address:X8} inside the {hit}");
            }
        }
    }

    [Fact]
    public void ProtectedRegions_CoverHookSbss2AndScratch()
    {
        var regions = ProtectedRegions.FromPuppetSharedHeader(TestRepo.Config().PuppetSharedHeaderPath);
        var scratchEnd = PuppetSharedHeader.ReadDefine(TestRepo.Config().PuppetSharedHeaderPath, "SCRATCH_REGION_END");

        Assert.NotNull(regions.FindViolation(HookAsmGenerator.CodeAddress, HookAsmGenerator.CodeAddress + 4, isOptionalPatch: true));
        Assert.Null(regions.FindViolation(HookAsmGenerator.CodeAddress, HookAsmGenerator.CodeAddress + 4, isOptionalPatch: false));
        Assert.NotNull(regions.FindViolation(DolPatcher.Sbss2RamAddress, DolPatcher.Sbss2RamAddress + 4, isOptionalPatch: false));
        Assert.NotNull(regions.FindViolation(scratchEnd - 4, scratchEnd, isOptionalPatch: false));
        Assert.Null(regions.FindViolation(scratchEnd, scratchEnd + 4, isOptionalPatch: true));
    }

    [Fact]
    public void FreeSpaceStartsPastTheScratchRegion()
    {
        var config = TestRepo.Config();
        var regions = ProtectedRegions.FromPuppetSharedHeader(config.PuppetSharedHeaderPath);
        var start = PatcherConfig.LoadFreeSpaceOffsets(config.FreeSpaceOffsetsPath)["sys/main.dol"];
        Assert.True(start >= regions.FirstFreeSpaceAddress,
            $"main.dol free space 0x{start:X8} overlaps the scratch region (must be >= 0x{regions.FirstFreeSpaceAddress:X8})");
    }

    [Fact]
    public void RepoCatalogue_PatchesWritingTheSameAddressDeclareAConflict()
    {
        var catalog = Repo;
        var writes = catalog.Patches
            .SelectMany(p => p.Orgs.Where(o => o.Address != null).Select(o => (p.Id, o.File, Address: o.Address!.Value)))
            .ToList();
        foreach (var clash in writes.GroupBy(w => (w.File, w.Address)).Where(g => g.Select(w => w.Id).Distinct().Count() > 1))
        {
            var ids = clash.Select(w => w.Id).Distinct().ToList();
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                    Assert.True(catalog.FindConflicts([ids[i], ids[j]]).Count == 1,
                        $"{ids[i]} and {ids[j]} both write {clash.Key.File} 0x{clash.Key.Address:X}; declare @conflicts");
        }
    }
}
