using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Tools;
using Xunit;

namespace WWOnline.Patcher.Tests.Patches;

public class PatchPlanTests : IDisposable
{
    private readonly string _diffs = Path.Combine(Path.GetTempPath(), "wwo-plan-" + Guid.NewGuid().ToString("N"));
    private readonly OptionalPatchCatalog _catalog;

    public PatchPlanTests()
    {
        Directory.CreateDirectory(PatchPlan.OptionalDiffsDirectory(_diffs));
        File.WriteAllText(Path.Combine(_diffs, "use_extra_memory_diff.yaml"), "");
        File.WriteAllText(Path.Combine(_diffs, "link_draw_hook_diff.yaml"), "");
        foreach (var id in new[] { "skip_intro", "swift_sail", "brisk_sail" })
            File.WriteAllText(PatchPlan.OptionalDiffPath(_diffs, id), "");

        _catalog = new OptionalPatchCatalog([
            Patch("skip_intro", defaultOn: true),
            Patch("swift_sail", extra: "; @conflicts brisk_sail"),
            Patch("brisk_sail"),
            Patch("instant_text", body: "", extra: "; @edit bmg-instant-text"),
            Patch("never_assembled"),
        ]);
    }

    public void Dispose()
    {
        try { Directory.Delete(_diffs, recursive: true); } catch (IOException) { }
    }

    private static OptionalPatch Patch(string id, bool defaultOn = false, string extra = "", string body = ".open \"sys/main.dol\"\n.org 0x80000000\nnop\n.close") =>
        OptionalPatchCatalog.Parse(id, $"; @name {id}\n; @description d\n; @category c\n; @default {(defaultOn ? "on" : "off")}\n; @match no\n; @credit MIT\n{extra}\n{body}\n", id + ".asm");

    private IReadOnlyList<string> Names(IEnumerable<string> selection) =>
        PatchPlan.GetDiffFiles(_diffs, _catalog, selection).Select(f => PatchDiffFile.PatchNameFromPath(f.Path) + (f.IsOptional ? "*" : "")).ToList();

    [Fact]
    public void RequiredDiffsAlwaysApply_OptionalOnlyWhenSelected()
    {
        Assert.Equal(["link_draw_hook", "use_extra_memory"], Names([]));
        Assert.Equal(["link_draw_hook", "use_extra_memory", "skip_intro*"], Names(["skip_intro"]));
        Assert.Equal(["link_draw_hook", "use_extra_memory", "skip_intro*", "swift_sail*"], Names(["swift_sail", "skip_intro"]));
    }

    [Fact]
    public void EditOnlyPatch_HasNoDiff()
    {
        Assert.Equal(["link_draw_hook", "use_extra_memory"], Names(["instant_text"]));
    }

    [Fact]
    public void LeftoverTopLevelDiffOfAnOptionalPatch_IsNotRequired()
    {
        // e.g. a skip_intro_diff.yaml from before skip_intro became optional, left in bin/PatchData
        File.WriteAllText(Path.Combine(_diffs, "skip_intro_diff.yaml"), "");
        Assert.Equal(["link_draw_hook", "use_extra_memory"], Names([]));
        Assert.Equal(["link_draw_hook", "use_extra_memory", "skip_intro*"], Names(["skip_intro"]));
    }

    [Fact]
    public void SelectedPatchWithoutDiff_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => Names(["never_assembled"]));
    }

    [Fact]
    public void UnknownOrConflictingSelection_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PatchPlan.ResolveStrict(_catalog, ["nope"]));
        Assert.Throws<InvalidOperationException>(() => PatchPlan.ResolveStrict(_catalog, ["swift_sail", "brisk_sail"]));
        Assert.Equal(["skip_intro"], PatchPlan.ResolveStrict(_catalog, null).Ids);
    }

    [Fact]
    public void DiffFile_RoundTripsChunksAndRelocations()
    {
        var path = Path.Combine(_diffs, "roundtrip.yaml");
        PatchDiffFile.Write(path, new Dictionary<string, IReadOnlyList<PatchChunk>>
        {
            ["sys/main.dol"] = [new PatchChunk(0x80232C78, [0x60, 0, 0, 0], [])],
            ["files/rels/d_a_ship.rel"] = [new PatchChunk(0xB9FC, [0x48, 0, 0, 1], [new PatchRelocation(0, "R_PPC_REL24", "wind", 0x803FD200)])],
        });
        var diff = PatchDiffFile.Load(path, isOptional: true);
        var rel = Assert.Single(diff.Files["files/rels/d_a_ship.rel"]);
        Assert.Equal(0xB9FCu, rel.Address);
        Assert.Equal(new PatchRelocation(0, "R_PPC_REL24", "wind", 0x803FD200), Assert.Single(rel.Relocations));
        Assert.Equal(new byte[] { 0x60, 0, 0, 0 }, diff.Files["sys/main.dol"][0].Data);
    }
}

public class GamePatchApplierValidateTests
{
    private static readonly ProtectedRegions Regions = new(0x803FCFA8, 0x803FD200);

    private static PatchDiffFile Diff(string name, bool optional, string file, uint address, int length = 4) =>
        new(name, optional, new Dictionary<string, IReadOnlyList<PatchChunk>> { [file] = [new PatchChunk(address, new byte[length], [])] });

    [Fact]
    public void OptionalPatch_CannotWriteTheHook_RequiredCan()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GamePatchApplier.Validate([Diff("x", true, "sys/main.dol", HookAsmGenerator.CodeAddress + 0x10)], Regions));
        GamePatchApplier.Validate([Diff("link_draw_hook", false, "sys/main.dol", HookAsmGenerator.CodeAddress)], Regions);
    }

    [Theory]
    [InlineData(0x803FCF20u)] // .sbss2
    [InlineData(0x803FD1FCu)] // last scratch word
    public void NoPatch_WritesSbss2OrScratch(uint address)
    {
        Assert.Throws<InvalidOperationException>(() => GamePatchApplier.Validate([Diff("req", false, "sys/main.dol", address)], Regions));
    }

    [Fact]
    public void FreeSpacePastScratch_IsAllowed()
    {
        GamePatchApplier.Validate([Diff("x", true, "sys/main.dol", 0x803FD200, 0x40)], Regions);
    }

    [Fact]
    public void TwoPatchesWritingTheSameBytes_Throw()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => GamePatchApplier.Validate(
            [Diff("swift_sail", true, "files/rels/d_a_ship.rel", 0xB9FC), Diff("sail_controls_wind", true, "files/rels/d_a_ship.rel", 0xB9FE, 2)], Regions));
        Assert.Contains("swift_sail", ex.Message);
        Assert.Contains("sail_controls_wind", ex.Message);

        // Same address in different files is fine.
        GamePatchApplier.Validate([Diff("a", true, "files/rels/d_a_ship.rel", 0x100), Diff("b", true, "files/rels/d_a_st.rel", 0x100)], Regions);
    }
}
