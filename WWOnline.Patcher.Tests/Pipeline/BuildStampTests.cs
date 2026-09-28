using WWOnline.Patcher.Config;
using WWOnline.Patcher.Pipeline;
using Xunit;

namespace WWOnline.Patcher.Tests.Pipeline;

public class BuildStampTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wwo-stamp-" + Guid.NewGuid().ToString("N"));
    private readonly PatcherConfig _config;

    public BuildStampTests()
    {
        _config = new PatcherConfig
        {
            GameModPath = Path.Combine(_root, "GameMod"),
            GamePath = Path.Combine(_root, "game"),
        };
        Directory.CreateDirectory(_config.PuppetLinkSrcPath);
        Directory.CreateDirectory(_config.PatchesSrcPath);
        Directory.CreateDirectory(_config.IncludePath);
        Directory.CreateDirectory(_config.GamePath);

        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet_shared.h"), "#define PUPPET_SLOT_SIZE 0x48\n");
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int x;\n");
        File.WriteAllText(Path.Combine(_config.PatchesSrcPath, "skip_intro.asm"), "nop\n");
        File.WriteAllText(Path.Combine(_config.IncludePath, "ww_linker.ld"), "SECTIONS {}\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Check_WithoutStamp_ReportsMissing()
    {
        Assert.Equal(BuildStamp.Status.Missing, BuildStamp.Check(_config).Status);
    }

    [Fact]
    public void Check_AfterWrite_ReportsFresh()
    {
        BuildStamp.Write(_config);
        Assert.Equal(BuildStamp.Status.Fresh, BuildStamp.Check(_config).Status);
    }

    [Fact]
    public void Check_AfterHeaderChange_ReportsStale()
    {
        BuildStamp.Write(_config);
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet_shared.h"), "#define PUPPET_SLOT_SIZE 0x50\n");
        Assert.Equal(BuildStamp.Status.Stale, BuildStamp.Check(_config).Status);
    }

    [Fact]
    public void Hash_IgnoresLineEndings()
    {
        var before = BuildStamp.ComputeSourceHash(_config);
        File.WriteAllText(Path.Combine(_config.PuppetLinkSrcPath, "puppet.c"), "int x;\r\n");
        Assert.Equal(before, BuildStamp.ComputeSourceHash(_config));
    }

    [Fact]
    public void Hash_IgnoresPipelineGeneratedHookAsm()
    {
        var before = BuildStamp.ComputeSourceHash(_config);
        File.WriteAllText(Path.Combine(_config.PatchesSrcPath, "link_draw_hook.asm"), "generated\n");
        Assert.Equal(before, BuildStamp.ComputeSourceHash(_config));
    }

    [Fact]
    public void Hash_ChangesWhenAsmPatchAdded()
    {
        var before = BuildStamp.ComputeSourceHash(_config);
        File.WriteAllText(Path.Combine(_config.PatchesSrcPath, "new_patch.asm"), "nop\n");
        Assert.NotEqual(before, BuildStamp.ComputeSourceHash(_config));
    }
}
