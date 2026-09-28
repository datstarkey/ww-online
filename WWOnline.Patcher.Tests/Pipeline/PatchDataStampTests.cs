using WWOnline.Patcher.Config;
using WWOnline.Patcher.Pipeline;
using Xunit;

namespace WWOnline.Patcher.Tests.Pipeline;

/// <summary>
/// The PatchData stamp reproduces the live pipeline's source hash for any selection, and an installed
/// app (no GameMod) can tell from it whether its patched game is stale after an update.
/// </summary>
public class PatchDataStampTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-pdstamp-" + Guid.NewGuid().ToString("N"));
    private readonly PatcherConfig _config;
    private string GamePath => _config.GamePath;

    public PatchDataStampTests()
    {
        _config = FakeGameMod.Create(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    public static TheoryData<string[]> Selections => new()
    {
        Array.Empty<string>(),
        new[] { "skip_intro" },
        new[] { "instant_text", "skip_intro" },
        new[] { "videos" },
        new[] { "instant_text", "skip_intro", "videos" },
    };

    [Theory]
    [MemberData(nameof(Selections))]
    public void ComputeSourceHash_MatchesTheLivePipeline(string[] selection)
    {
        var stamp = PatchDataStamp.FromSources(_config);
        Assert.Equal(BuildStamp.ComputeSourceHash(_config, selection), stamp.ComputeSourceHash(selection));
    }

    [Fact]
    public void Stamp_RecordsEveryPatch_AndTheDefaults()
    {
        var stamp = PatchDataStamp.FromSources(_config);
        Assert.Equal(["instant_text", "skip_intro", "videos"], stamp.OptionalPatchSources.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["skip_intro"], stamp.DefaultOptionalPatches);
        Assert.Equal(BuildStamp.ComputeSourceHash(_config, ["skip_intro"]), stamp.DefaultSourceHash);
        Assert.Contains(stamp.OptionalPatchSources["videos"], e => e.Path == "assets/blank.thp");
        Assert.Contains(stamp.RequiredSources, e => e.Path == "src/puppet_link/puppet.c");
        Assert.DoesNotContain(stamp.RequiredSources, e => e.Path.StartsWith("src/patches/optional/", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceHashes_IgnoreLineEndings()
    {
        // CI builds PatchData from an LF checkout (Linux); a Windows dev checkout has CRLF (autocrlf).
        // Their stamps must agree, or every dev-patched game reads as stale against a release.
        var lf = PatchDataStamp.FromSources(_config);
        foreach (var file in BuildStamp.GetSourceFiles(_config, ["instant_text", "skip_intro", "videos"]).Where(f => !f.EndsWith(".thp")))
            File.WriteAllText(file, File.ReadAllText(file).Replace("\n", "\r\n"));
        var crlf = PatchDataStamp.FromSources(_config);
        Assert.True(lf.SameSources(crlf));
        Assert.Equal(lf.DefaultSourceHash, BuildStamp.ComputeSourceHash(_config, ["skip_intro"]));
    }

    [Fact]
    public void Stamp_RoundTripsThroughJson()
    {
        var path = Path.Combine(_root, PatchDataFolder.StampFileName);
        var stamp = PatchDataStamp.FromSources(_config);
        stamp.Write(path);
        var read = PatchDataStamp.Read(path)!;
        Assert.True(stamp.SameSources(read));
        Assert.Equal(stamp.ComputeSourceHash(["videos", "skip_intro"]), read.ComputeSourceHash(["videos", "skip_intro"]));
    }

    [Fact]
    public void Read_MissingIsNull_BrokenThrows()
    {
        var path = Path.Combine(_root, PatchDataFolder.StampFileName);
        Assert.Null(PatchDataStamp.Read(path));
        File.WriteAllText(path, "{ not json");
        Assert.Throws<InvalidDataException>(() => PatchDataStamp.Read(path));
        File.WriteAllText(path, """{"Format":99,"RequiredSources":[],"OptionalPatchSources":{},"DefaultOptionalPatches":[],"DefaultSourceHash":"","Files":[]}""");
        Assert.Throws<InvalidDataException>(() => PatchDataStamp.Read(path));
    }

    [Fact]
    public void GameStamp_FromPatchData_IsFresh()
    {
        var patchData = PatchDataStamp.FromSources(_config);
        Assert.Equal(BuildStamp.Status.Missing, BuildStamp.CheckAgainstPatchData(GamePath, patchData, ["skip_intro"]).Status);

        var game = patchData.CreateGameStamp(["skip_intro", "instant_text"]);
        Assert.Equal("prebuilt", game.Origin);
        Assert.Equal(["instant_text", "skip_intro"], game.OptionalPatches!);
        BuildStamp.Write(GamePath, game);

        var result = BuildStamp.CheckAgainstPatchData(GamePath, patchData, ["instant_text", "skip_intro"]);
        Assert.Equal(BuildStamp.Status.Fresh, result.Status);
        Assert.True(result.AgainstPatchData);
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.CheckAgainstPatchData(GamePath, patchData).Status); // selection not compared
    }

    [Fact]
    public void UpdateWithNewGameCode_IsStale()
    {
        BuildStamp.Write(GamePath, PatchDataStamp.FromSources(_config).CreateGameStamp(["skip_intro"]));

        // The next release changed the puppet code: its PatchData stamp differs.
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int y;\n");
        var updated = PatchDataStamp.FromSources(_config);

        var result = BuildStamp.CheckAgainstPatchData(GamePath, updated, ["skip_intro"]);
        Assert.Equal(BuildStamp.Status.Stale, result.Status);
        Assert.False(result.SelectionChanged);
        Assert.Contains("Patch the game again", result.Describe());
    }

    [Fact]
    public void UpdateThatOnlyChangedAnUnselectedPatch_StaysFresh_ASelectedOneIsStale()
    {
        BuildStamp.Write(GamePath, PatchDataStamp.FromSources(_config).CreateGameStamp(["skip_intro"]));

        FakeGameMod.WritePatch(_config, "instant_text", defaultOn: false, name: "renamed");
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.CheckAgainstPatchData(GamePath, PatchDataStamp.FromSources(_config), ["skip_intro"]).Status);

        FakeGameMod.WritePatch(_config, "skip_intro", defaultOn: true, name: "renamed");
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.CheckAgainstPatchData(GamePath, PatchDataStamp.FromSources(_config), ["skip_intro"]).Status);
    }

    [Fact]
    public void UpdateThatRemovedASelectedPatch_IsStale()
    {
        BuildStamp.Write(GamePath, PatchDataStamp.FromSources(_config).CreateGameStamp(["instant_text", "skip_intro"]));
        File.Delete(Path.Combine(_config.OptionalPatchesSrcPath, "instant_text.asm"));
        Assert.Equal(BuildStamp.Status.Stale,
            BuildStamp.CheckAgainstPatchData(GamePath, PatchDataStamp.FromSources(_config), ["instant_text", "skip_intro"]).Status);
    }

    [Fact]
    public void ChangedSelection_IsStale()
    {
        var patchData = PatchDataStamp.FromSources(_config);
        BuildStamp.Write(GamePath, patchData.CreateGameStamp(["skip_intro"]));
        var result = BuildStamp.CheckAgainstPatchData(GamePath, patchData, ["skip_intro", "videos"]);
        Assert.Equal(BuildStamp.Status.Stale, result.Status);
        Assert.True(result.SelectionChanged);
    }

    [Fact]
    public void LivePipelineStamp_AndPatchData_AgreeBothWays()
    {
        // A dev's pipeline-built game reads as fresh against PatchData built from the same sources...
        _config.OptionalPatches = ["skip_intro", "videos"];
        BuildStamp.Write(_config);
        Assert.Equal(BuildStamp.Status.Fresh,
            BuildStamp.CheckAgainstPatchData(GamePath, PatchDataStamp.FromSources(_config), ["skip_intro", "videos"]).Status);

        // ...and a pre-built game reads as fresh against those sources.
        BuildStamp.Write(GamePath, PatchDataStamp.FromSources(_config).CreateGameStamp(["skip_intro", "videos"]));
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.Check(_config, ["skip_intro", "videos"]).Status);
    }
}

/// <summary>A tiny GameMod tree: required sources, three optional patches (one @edit-only with an asset).</summary>
internal static class FakeGameMod
{
    public static PatcherConfig Create(string root)
    {
        var config = new PatcherConfig
        {
            GameModPath = Path.Combine(root, "GameMod"),
            GamePath = Path.Combine(root, "game"),
        };
        Directory.CreateDirectory(config.PuppetLinkSrcPath);
        Directory.CreateDirectory(config.OptionalPatchesSrcPath);
        Directory.CreateDirectory(config.IncludePath);
        Directory.CreateDirectory(config.AssetsPath);
        Directory.CreateDirectory(config.GamePath);
        File.WriteAllText(Path.Combine(config.PuppetLinkSrcPath, "puppet.c"), "int x;\n");
        File.WriteAllText(Path.Combine(config.PuppetLinkSrcPath, "puppet_shared.h"), "#define SCRATCH_REGION_START 0x803FCFA8\n");
        File.WriteAllText(Path.Combine(config.PatchesSrcPath, "use_extra_memory.asm"), "nop\n");
        File.WriteAllText(config.FreeSpaceOffsetsPath, "sys/main.dol: 0x803FD200\n");
        File.WriteAllText(Path.Combine(config.IncludePath, "ww_linker.ld"), "SECTIONS {}\n");
        WritePatch(config, "skip_intro", defaultOn: true);
        WritePatch(config, "instant_text", defaultOn: false);
        WritePatch(config, "videos", defaultOn: false, "; @edit replace-file files/thpdemo/title_loop.thp blank.thp", body: "");
        File.WriteAllText(Path.Combine(config.AssetsPath, "blank.thp"), "THP0");
        return config;
    }

    public static void WritePatch(PatcherConfig config, string id, bool defaultOn, string extra = "",
        string body = ".open \"sys/main.dol\"\n.org 0x80000000\nnop\n.close", string name = "n") =>
        File.WriteAllText(Path.Combine(config.OptionalPatchesSrcPath, id + ".asm"),
            $"; @name {name}\n; @description d\n; @category c\n; @default {(defaultOn ? "on" : "off")}\n; @match no\n; @credit MIT\n{extra}\n{body}\n");
}
