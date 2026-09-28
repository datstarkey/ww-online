using System.Buffers.Binary;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Patches;
using WWOnline.Patcher.Pipeline;
using WWOnline.Patcher.Pipeline.Steps;
using Xunit;

namespace WWOnline.Patcher.Tests.Pipeline;

/// <summary>
/// --build-patchdata's output: the layout the client's pre-built path reads, with a stamp listing
/// every file. <see cref="PatchDataBuilder.Package"/> is tested on a fake build; the full build
/// (real GameMod, devkitPPC) only runs where devkitPPC is installed.
/// </summary>
public class PatchDataBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-pdbuild-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    /// <summary>A fake GameMod plus the output a PatchData-mode pipeline run would leave in BuildOutputPath.</summary>
    private PatcherConfig FakeBuild()
    {
        var config = FakeGameMod.Create(_root);
        config.BuildOutputPath = Path.Combine(_root, "intermediate");
        Directory.CreateDirectory(config.BuildPath);
        Directory.CreateDirectory(config.OptionalPatchDiffsPath);
        File.WriteAllBytes(Path.Combine(config.BuildPath, BuildRelStep.RelFileName), [0, 0, 0, 0x58, 1, 2, 3, 4]);
        foreach (var name in new[] { "link_draw_hook", "use_extra_memory" })
            File.WriteAllText(Path.Combine(config.PatchDiffsPath, name + PatchDiffFile.Suffix), "sys/main.dol: {}\n");
        foreach (var id in new[] { "instant_text", "skip_intro" }) // "videos" is @edit-only: no diff
            File.WriteAllText(PatchPlan.OptionalDiffPath(config.PatchDiffsPath, id), "sys/main.dol: {}\n");
        return config;
    }

    [Fact]
    public void Package_WritesTheLayoutThePrebuiltPathReads()
    {
        var config = FakeBuild();
        var outDir = Path.Combine(_root, "out", "PatchData");
        var stamp = PatchDataBuilder.Package(config, outDir, PatchDataStamp.FromSources(config));
        var folder = new PatchDataFolder(outDir);

        Assert.Equal(
            [
                "assets/blank.thp",
                "d_a_puppet.rel",
                "free_space_start_offsets.txt",
                "optional/instant_text.asm",
                "optional/skip_intro.asm",
                "optional/videos.asm",
                "patch_diffs/link_draw_hook_diff.yaml",
                "patch_diffs/optional/instant_text_diff.yaml",
                "patch_diffs/optional/skip_intro_diff.yaml",
                "patch_diffs/use_extra_memory_diff.yaml",
            ],
            stamp.Files.Select(f => f.Path));
        Assert.True(File.Exists(folder.StampPath));
        Assert.Empty(folder.Verify(folder.ReadStamp()!));

        // Everything the client's pre-built path needs resolves from the folder alone.
        var catalog = folder.LoadCatalog();
        Assert.Equal(3, catalog.Patches.Count);
        var diffs = PatchPlan.GetDiffFiles(folder.PatchDiffsPath, catalog, ["instant_text", "skip_intro", "videos"]);
        Assert.Equal(4, diffs.Count);
        Assert.Equal(stamp.ComputeSourceHash(["skip_intro"]), BuildStamp.ComputeSourceHash(config, ["skip_intro"]));
    }

    [Fact]
    public void Verify_ReportsMissingAndChangedFiles()
    {
        var config = FakeBuild();
        var outDir = Path.Combine(_root, "PatchData");
        var stamp = PatchDataBuilder.Package(config, outDir, PatchDataStamp.FromSources(config));
        var folder = new PatchDataFolder(outDir);

        File.Delete(Path.Combine(outDir, "optional", "skip_intro.asm"));
        File.WriteAllText(Path.Combine(outDir, "free_space_start_offsets.txt"), "sys/main.dol: 0x80000000\n");
        var problems = folder.Verify(stamp);
        Assert.Contains("optional/skip_intro.asm is missing", problems);
        Assert.Contains("free_space_start_offsets.txt differs", problems);
    }

    [Fact]
    public void Verify_ReportsFilesTheStampDoesNotList()
    {
        var config = FakeBuild();
        var outDir = Path.Combine(_root, "PatchData");
        var stamp = PatchDataBuilder.Package(config, outDir, PatchDataStamp.FromSources(config));
        var folder = new PatchDataFolder(outDir);
        Assert.Empty(folder.Verify(stamp));

        // A stray top-level diff would be applied as a required patch: the folder is not intact.
        File.WriteAllText(Path.Combine(outDir, "patch_diffs", "stray" + PatchDiffFile.Suffix), "sys/main.dol: {}\n");
        Assert.Equal(["patch_diffs/stray" + PatchDiffFile.Suffix + " is not in the stamp"], folder.Verify(stamp));
    }

    [Fact]
    public void Package_FailsWhenAnOptionalPatchWasNotAssembled()
    {
        var config = FakeBuild();
        File.Delete(PatchPlan.OptionalDiffPath(config.PatchDiffsPath, "skip_intro"));
        Assert.Throws<FileNotFoundException>(() =>
            PatchDataBuilder.Package(config, Path.Combine(_root, "PatchData"), PatchDataStamp.FromSources(config)));
    }

    [Fact]
    public void OutputDirectory_MustBeNewEmptyOrAPreviousPatchData()
    {
        var gameMod = Path.Combine(_root, "GameMod");
        Directory.CreateDirectory(gameMod);
        var outDir = Path.Combine(_root, "PatchData");

        PatchDataBuilder.ValidateOutputDirectory(outDir, gameMod); // new
        Directory.CreateDirectory(outDir);
        PatchDataBuilder.ValidateOutputDirectory(outDir, gameMod); // empty

        File.WriteAllText(Path.Combine(outDir, "precious.txt"), "not ours");
        Assert.Throws<InvalidOperationException>(() => PatchDataBuilder.ValidateOutputDirectory(outDir, gameMod));
        File.WriteAllText(Path.Combine(outDir, PatchDataFolder.StampFileName), "{}");
        PatchDataBuilder.ValidateOutputDirectory(outDir, gameMod); // a previous PatchData

        Assert.Throws<InvalidOperationException>(() => PatchDataBuilder.ValidateOutputDirectory(gameMod, gameMod));
        Assert.Throws<InvalidOperationException>(() => PatchDataBuilder.ValidateOutputDirectory(Path.Combine(gameMod, "out"), gameMod));
        PatchDataBuilder.ValidateOutputDirectory(Path.Combine(_root, "GameModOut"), gameMod); // a sibling is fine

        // A folder that contains GameMod (the repo root, say) is refused even if it has a stamp.
        File.WriteAllText(Path.Combine(_root, PatchDataFolder.StampFileName), "{}");
        Assert.Throws<InvalidOperationException>(() => PatchDataBuilder.ValidateOutputDirectory(_root, gameMod));
        Assert.Throws<InvalidOperationException>(() => PatchDataBuilder.ValidateOutputDirectory(Path.GetPathRoot(_root)!, gameMod));
    }

    [Fact]
    public async Task Build_RefusesAFolderThatIsNotOurs_BeforeCompiling()
    {
        var outDir = Path.Combine(_root, "somewhere");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "keep.txt"), "x");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PatchDataBuilder.BuildAsync(TestRepo.GameMod, Path.Combine(_root, "no-devkitppc"), outDir));
        Assert.True(File.Exists(Path.Combine(outDir, "keep.txt")));
    }

    /// <summary>The real thing: GameMod sources + devkitPPC → PatchData, without writing the source tree.</summary>
    [DevKitPpcFact]
    public async Task Build_RealSources_ProducesCompletePatchData()
    {
        var gameMod = TestRepo.GameMod;
        var inTree = new[]
        {
            Path.Combine(gameMod, "build", BuildRelStep.RelFileName),
            Path.Combine(gameMod, "src", "patches", PatcherConfig.HookAsmFileName),
            Path.Combine(gameMod, "src", "patches", "patch_diffs", "use_extra_memory_diff.yaml"),
        };
        var before = inTree.Select(f => File.Exists(f) ? File.GetLastWriteTimeUtc(f) : DateTime.MinValue).ToList();

        var outDir = Path.Combine(_root, "PatchData");
        var stamp = await PatchDataBuilder.BuildAsync(gameMod, PatcherConfig.DefaultDevKitPpcPath(), outDir);
        var folder = new PatchDataFolder(outDir);

        var rel = File.ReadAllBytes(folder.RelPath);
        Assert.Equal(0x58u, BinaryPrimitives.ReadUInt32BigEndian(rel));
        Assert.True(File.Exists(Path.Combine(folder.PatchDiffsPath, "link_draw_hook_diff.yaml")));
        Assert.True(File.Exists(Path.Combine(folder.PatchDiffsPath, "use_extra_memory_diff.yaml")));
        var catalog = folder.LoadCatalog();
        Assert.Equal(TestRepo.Config().LoadOptionalPatchCatalog().Patches.Count, catalog.Patches.Count);
        foreach (var patch in catalog.Patches.Where(p => p.HasAsm))
            Assert.True(File.Exists(PatchPlan.OptionalDiffPath(folder.PatchDiffsPath, patch.Id)), patch.Id);
        foreach (var asset in catalog.Patches.SelectMany(p => p.Edits.OfType<ReplaceFileEdit>()))
            Assert.True(File.Exists(Path.Combine(folder.AssetsPath, asset.AssetName)), asset.AssetName);
        Assert.Empty(folder.Verify(folder.ReadStamp()!));
        Assert.Equal(stamp.DefaultSourceHash, BuildStamp.ComputeSourceHash(TestRepo.Config(), catalog.DefaultIds));

        var after = inTree.Select(f => File.Exists(f) ? File.GetLastWriteTimeUtc(f) : DateTime.MinValue).ToList();
        Assert.Equal(before, after); // the build output went to a temp folder, not GameMod/
    }
}

/// <summary>A [Fact] reported as skipped when devkitPPC isn't installed (DEVKITPPC or the default path).</summary>
internal sealed class DevKitPpcFactAttribute : FactAttribute
{
    public DevKitPpcFactAttribute()
    {
        var config = PatcherConfig.Create("", PatcherConfig.DefaultDevKitPpcPath(), TestRepo.GameMod);
        if (!File.Exists(config.GetToolPath("powerpc-eabi-gcc")))
            Skip = "devkitPPC not found (set DEVKITPPC or install it at the default path)";
    }
}
