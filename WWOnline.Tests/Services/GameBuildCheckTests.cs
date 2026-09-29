using WWOnline.Patcher.Config;
using WWOnline.Patcher.Pipeline;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The shared build check: dev (GameMod sources) and installed (PatchData stamp, no GameMod) paths,
/// plus the headless flags that drive it.
/// </summary>
public class GameBuildCheckTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-buildcheck-" + Guid.NewGuid().ToString("N"));
    private readonly PatcherConfig _config;
    private readonly string _patchData;
    private string GamePath => _config.GamePath;

    public GameBuildCheckTests()
    {
        _config = new PatcherConfig { GameModPath = Path.Combine(_root, "GameMod"), GamePath = Path.Combine(_root, "game") };
        Directory.CreateDirectory(_config.PuppetLinkSrcPath);
        Directory.CreateDirectory(_config.OptionalPatchesSrcPath);
        Directory.CreateDirectory(_config.IncludePath);
        Directory.CreateDirectory(GamePath);
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int x;\n");
        File.WriteAllText(Path.Combine(_config.IncludePath, "ww_linker.ld"), "SECTIONS {}\n");
        WritePatch("skip_intro", defaultOn: true);
        WritePatch("instant_text", defaultOn: false);

        _patchData = Path.Combine(_root, "app", "PatchData");
        WritePatchDataStamp();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private void WritePatch(string id, bool defaultOn, string name = "n") =>
        File.WriteAllText(Path.Combine(_config.OptionalPatchesSrcPath, id + ".asm"),
            $"; @name {name}\n; @description d\n; @category c\n; @default {(defaultOn ? "on" : "off")}\n; @match no\n; @credit MIT\n" +
            ".open \"sys/main.dol\"\n.org 0x80000000\nnop\n.close\n");

    /// <summary>What a release's --build-patchdata writes for the current fake sources.</summary>
    private PatchDataStamp WritePatchDataStamp()
    {
        Directory.CreateDirectory(_patchData);
        var stamp = PatchDataStamp.FromSources(_config);
        stamp.Write(new PatchDataFolder(_patchData).StampPath);
        return stamp;
    }

    private BuildStamp.CheckResult Installed(IReadOnlyCollection<string>? desired) =>
        GameBuildCheck.Check(GamePath, desired, gameModPath: null, _patchData);

    [Fact]
    public void Installed_NoGameStamp_IsMissing()
    {
        Assert.Equal(BuildStamp.Status.Missing, Installed(["skip_intro"]).Status);
    }

    [Fact]
    public void Installed_PatchedFromThisAppsPatchData_IsFresh()
    {
        BuildStamp.Write(GamePath, WritePatchDataStamp().CreateGameStamp(["skip_intro"]));
        var result = Installed(["skip_intro"]);
        Assert.Equal(BuildStamp.Status.Fresh, result.Status);
        Assert.True(result.AgainstPatchData);
    }

    [Fact]
    public void Installed_UpdateWithNewGameCode_IsStale_UpdateWithoutIsFresh()
    {
        BuildStamp.Write(GamePath, WritePatchDataStamp().CreateGameStamp(["skip_intro"]));

        // An update whose only game-side change is to a patch the player didn't pick.
        WritePatch("instant_text", defaultOn: false, name: "renamed");
        WritePatchDataStamp();
        Assert.Equal(BuildStamp.Status.Fresh, Installed(["skip_intro"]).Status);

        // An update that changed the puppet code.
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int changed;\n");
        WritePatchDataStamp();
        var result = Installed(["skip_intro"]);
        Assert.Equal(BuildStamp.Status.Stale, result.Status);
        Assert.False(result.SelectionChanged);
    }

    [Fact]
    public void Installed_SavedSelectionChanged_IsStale()
    {
        BuildStamp.Write(GamePath, WritePatchDataStamp().CreateGameStamp(["skip_intro"]));
        var result = Installed(["instant_text", "skip_intro"]);
        Assert.Equal(BuildStamp.Status.Stale, result.Status);
        Assert.True(result.SelectionChanged);
    }

    [Fact]
    public void WithoutAPatchDataStamp_OnlyTheSelectionIsCompared()
    {
        File.Delete(new PatchDataFolder(_patchData).StampPath);
        BuildStamp.Write(GamePath, new BuildStamp.Stamp("legacy", DateTime.UtcNow, 3, ["skip_intro"], "prebuilt"));
        // No hash to compare: the switch table must be there and current.
        Assert.Equal(BuildStamp.Status.Stale, Installed(["skip_intro"]).Status);
        Assert.Equal(BuildStamp.Status.Stale, Installed(null).Status);
        new WWOnline.Patcher.WorldData.SwitchTable { Rules = WWOnline.Patcher.WorldData.SwitchTableBuilder.RulesVersion }.Write(GamePath);
        Assert.Equal(BuildStamp.Status.Fresh, Installed(["skip_intro"]).Status);
        Assert.True(Installed([]).SelectionChanged);
        Assert.Equal(BuildStamp.Status.Fresh, Installed(null).Status);

        File.WriteAllText(new PatchDataFolder(_patchData).StampPath, "{ broken"); // unreadable: same as none
        Assert.Equal(BuildStamp.Status.Fresh, Installed(["skip_intro"]).Status);
    }

    [Fact]
    public void Dev_ChecksTheGameModSources()
    {
        // A pre-built game from matching PatchData is fresh against the sources too (one hash definition)...
        BuildStamp.Write(GamePath, WritePatchDataStamp().CreateGameStamp(["skip_intro"]));
        Assert.Equal(BuildStamp.Status.Fresh, GameBuildCheck.Check(GamePath, ["skip_intro"], _config.GameModPath, _patchData).Status);

        // ...and a source edit makes it stale in dev even though the (old) PatchData still matches.
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int edited;\n");
        var dev = GameBuildCheck.Check(GamePath, ["skip_intro"], _config.GameModPath, _patchData);
        Assert.Equal(BuildStamp.Status.Stale, dev.Status);
        Assert.False(dev.AgainstPatchData);
        Assert.Equal(BuildStamp.Status.Fresh, Installed(["skip_intro"]).Status);
    }

    [Fact]
    public void StartupOptions_BuildPatchData()
    {
        var opts = StartupOptions.Parse(["--build-patchdata", "out/PatchData", "--log-file", "x.log", "--gamemod", "../GameMod"]);
        Assert.Equal("out/PatchData", opts.BuildPatchData);
        Assert.Equal("../GameMod", opts.GameModPath);
        Assert.Equal("x.log", opts.LogFile);
        Assert.True(opts.IsHeadless);
        Assert.False(opts.Patch);
        Assert.False(opts.CheckBuild);
    }

    [Fact]
    public void StartupOptions_BuildPatchDataWithoutAFolder_StaysHeadless()
    {
        // Headless with "" so the mode reports the mistake instead of the UI starting (in CI there is no display).
        var last = StartupOptions.Parse(["--build-patchdata"]);
        Assert.Equal("", last.BuildPatchData);
        Assert.True(last.IsHeadless);

        var beforeFlag = StartupOptions.Parse(["--build-patchdata", "--log-file", "x.log"]);
        Assert.Equal("", beforeFlag.BuildPatchData);
        Assert.Equal("x.log", beforeFlag.LogFile);
        Assert.Equal(HeadlessCommands.ExitError, HeadlessCommands.Run(beforeFlag));
    }

    [Fact]
    public void StartupOptions_NoHeadlessFlags_IsNotHeadless()
    {
        var opts = StartupOptions.Parse(["--player", "Link", "--auto-connect"]);
        Assert.Null(opts.BuildPatchData);
        Assert.False(opts.IsHeadless);
    }
}
