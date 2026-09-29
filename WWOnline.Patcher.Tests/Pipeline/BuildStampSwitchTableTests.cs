using System.Security.Cryptography;
using System.Text;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.Pipeline;
using WWOnline.Patcher.WorldData;
using Xunit;

namespace WWOnline.Patcher.Tests.Pipeline;

/// <summary>
/// The switch table is part of the build: a game patched before it existed, or with other rules, or whose
/// table has gone, reads as stale in every check (sources, PatchData, selection only).
/// </summary>
public class BuildStampSwitchTableTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-stamp-table-" + Guid.NewGuid().ToString("N"));
    private readonly PatcherConfig _config;

    public BuildStampSwitchTableTests()
    {
        _config = new PatcherConfig { GameModPath = Path.Combine(_root, "GameMod"), GamePath = Path.Combine(_root, "game") };
        Directory.CreateDirectory(_config.PuppetLinkSrcPath);
        Directory.CreateDirectory(_config.PatchesSrcPath);
        Directory.CreateDirectory(_config.IncludePath);
        Directory.CreateDirectory(_config.GamePath);
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int x;\n");
        File.WriteAllText(Path.Combine(_config.IncludePath, "ww_linker.ld"), "SECTIONS {}\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private void WriteTable(int rules) => new SwitchTable { Rules = rules }.Write(_config.GamePath);

    [Fact]
    public void AStampThatRecordsATable_IsStaleWithoutIt()
    {
        BuildStamp.Write(_config, SwitchTableBuilder.RulesVersion);
        var missing = BuildStamp.Check(_config);
        Assert.Equal(BuildStamp.Status.Stale, missing.Status);
        Assert.Contains("switch table", missing.Describe());
        Assert.Contains("Patch the game again", missing.Describe());

        WriteTable(SwitchTableBuilder.RulesVersion);
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.Check(_config).Status);

        WriteTable(SwitchTableBuilder.RulesVersion + 1); // a table from other rules
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.Check(_config).Status);
    }

    [Fact]
    public void TheOtherChecks_AlsoLookAtTheTable()
    {
        var patchData = PatchDataStamp.FromSources(_config);
        BuildStamp.Write(_config.GamePath, patchData.CreateGameStamp([]) with { SwitchTableRules = SwitchTableBuilder.RulesVersion });
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.CheckAgainstPatchData(_config.GamePath, patchData, []).Status);
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.CheckSelection(_config.GamePath, []).Status);

        WriteTable(SwitchTableBuilder.RulesVersion);
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.CheckAgainstPatchData(_config.GamePath, patchData, []).Status);
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.CheckSelection(_config.GamePath, []).Status);
    }

    [Fact]
    public void TheRulesVersion_IsPartOfTheSourceHash()
    {
        // The hash as it was defined before the switch table: games patched then are stale now.
        var entries = BuildStamp.HashSourceFiles(_config, BuildStamp.GetRequiredSourceFiles(_config));
        var manifest = new StringBuilder();
        foreach (var e in entries.OrderBy(e => e.Path, StringComparer.Ordinal))
            manifest.Append(e.Path).Append('\n').Append(e.Sha256).Append('\n');
        manifest.Append("optional:");
        var before = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString()))).ToLowerInvariant();

        Assert.NotEqual(before, BuildStamp.ComputeSourceHash(entries, []));
        manifest.Append("\nswitch-table-rules:").Append(SwitchTableBuilder.RulesVersion);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString()))).ToLowerInvariant(),
            BuildStamp.ComputeSourceHash(entries, []));
    }

    [Fact]
    public void BuildAndWrite_WithoutStageData_FailsAndLeavesNoTable()
    {
        WriteTable(SwitchTableBuilder.RulesVersion); // an old one
        Assert.ThrowsAny<IOException>(() => SwitchTableBuilder.BuildAndWrite(Path.Combine(_root, "no-vanilla"), _config.GamePath));
        Assert.Null(SwitchTable.Load(_config.GamePath));
    }
}
