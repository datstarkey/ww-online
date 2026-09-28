using WWOnline.Patcher.Config;
using WWOnline.Patcher.Pipeline;
using Xunit;

namespace WWOnline.Patcher.Tests.Pipeline;

/// <summary>The optional patch selection is part of what makes a patched game fresh or stale.</summary>
public class BuildStampSelectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-stamp-sel-" + Guid.NewGuid().ToString("N"));
    private readonly PatcherConfig _config;

    public BuildStampSelectionTests()
    {
        _config = new PatcherConfig
        {
            GameModPath = Path.Combine(_root, "GameMod"),
            GamePath = Path.Combine(_root, "game"),
        };
        Directory.CreateDirectory(_config.PuppetLinkSrcPath);
        Directory.CreateDirectory(_config.OptionalPatchesSrcPath);
        Directory.CreateDirectory(_config.IncludePath);
        Directory.CreateDirectory(_config.AssetsPath);
        Directory.CreateDirectory(_config.GamePath);
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int x;\n");
        File.WriteAllText(Path.Combine(_config.PatchesSrcPath, "use_extra_memory.asm"), "nop\n");
        File.WriteAllText(Path.Combine(_config.IncludePath, "ww_linker.ld"), "SECTIONS {}\n");
        WritePatch("skip_intro", defaultOn: true);
        WritePatch("instant_text", defaultOn: false);
        WritePatch("videos", defaultOn: false, "; @edit replace-file files/thpdemo/title_loop.thp blank.thp", body: "");
        File.WriteAllText(Path.Combine(_config.AssetsPath, "blank.thp"), "THP0");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private void WritePatch(string id, bool defaultOn, string extra = "", string body = ".open \"sys/main.dol\"\n.org 0x80000000\nnop\n.close", string name = "n") =>
        File.WriteAllText(Path.Combine(_config.OptionalPatchesSrcPath, id + ".asm"),
            $"; @name {name}\n; @description d\n; @category c\n; @default {(defaultOn ? "on" : "off")}\n; @match no\n; @credit MIT\n{extra}\n{body}\n");

    [Fact]
    public void Write_RecordsTheSelection_DefaultsWhenUnset()
    {
        BuildStamp.Write(_config);
        Assert.Equal(["skip_intro"], BuildStamp.Read(_config)!.OptionalPatches!);

        _config.OptionalPatches = ["instant_text", "skip_intro"];
        BuildStamp.Write(_config);
        Assert.Equal(["instant_text", "skip_intro"], BuildStamp.Read(_config)!.OptionalPatches!);
    }

    [Fact]
    public void Hash_ChangesWithTheSelection()
    {
        var defaults = BuildStamp.ComputeSourceHash(_config, ["skip_intro"]);
        Assert.NotEqual(defaults, BuildStamp.ComputeSourceHash(_config, ["skip_intro", "instant_text"]));
        Assert.NotEqual(defaults, BuildStamp.ComputeSourceHash(_config, []));
        Assert.Equal(defaults, BuildStamp.ComputeSourceHash(_config)); // null selection = defaults
    }

    [Fact]
    public void Check_DifferentDesiredSelection_IsStale()
    {
        BuildStamp.Write(_config);
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.Check(_config, ["skip_intro"]).Status);

        var result = BuildStamp.Check(_config, ["skip_intro", "instant_text"]);
        Assert.Equal(BuildStamp.Status.Stale, result.Status);
        Assert.True(result.SelectionChanged);
        Assert.Contains("instant_text", result.Describe());

        // No desired selection = only the sources of the recorded selection are checked.
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.Check(_config).Status);
    }

    [Fact]
    public void EditingAnUnselectedPatch_KeepsTheBuildFresh_EditingASelectedOneDoesNot()
    {
        BuildStamp.Write(_config); // defaults: skip_intro only

        WritePatch("instant_text", defaultOn: false, name: "renamed");
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.Check(_config).Status);

        WritePatch("skip_intro", defaultOn: true, name: "renamed");
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.Check(_config).Status);
    }

    [Fact]
    public void SelectedPatchAssets_AreHashed()
    {
        _config.OptionalPatches = ["videos"];
        BuildStamp.Write(_config);
        File.WriteAllText(Path.Combine(_config.AssetsPath, "blank.thp"), "THP1");
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.Check(_config).Status);
    }

    [Fact]
    public void CheckSelection_WorksWithoutSources()
    {
        Assert.Equal(BuildStamp.Status.Missing, BuildStamp.CheckSelection(_config.GamePath, ["skip_intro"]).Status);
        BuildStamp.Write(_config.GamePath, new BuildStamp.Stamp("ab" + new string('0', 62), DateTime.UtcNow, 3, ["skip_intro"], "prebuilt"));
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.CheckSelection(_config.GamePath, ["skip_intro"]).Status);
        Assert.True(BuildStamp.CheckSelection(_config.GamePath, []).SelectionChanged);
    }

    [Fact]
    public void LegacyStampWithoutSelection_IsStale()
    {
        File.WriteAllText(BuildStamp.GetStampPath(_config),
            $$"""{"SourceHash":"{{BuildStamp.ComputeSourceHash(_config, [])}}","BuiltAtUtc":"2026-01-01T00:00:00Z","FileCount":3}""");
        // Old stamps predate optional patches: they read as "built with none", so the defaults differ.
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.Check(_config, ["skip_intro"]).Status);
    }
}
