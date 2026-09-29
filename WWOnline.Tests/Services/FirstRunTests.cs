using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

public sealed class FirstRunTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-firstrun-" + Guid.NewGuid().ToString("N"));
    private readonly GameSettingsService _settings;
    private static readonly StartupOptions Interactive = new(null, null, null, false, false, null);

    public FirstRunTests() => _settings = new GameSettingsService(Path.Combine(_dir, "settings"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>Saved settings with valid paths; the patched folder holds a game when <paramref name="patched"/>.</summary>
    private void SaveWorkingSetup(bool patched)
    {
        var vanilla = FakeGameFolder.Create(Path.Combine(_dir, "ww"));
        var game = Path.Combine(_dir, "ww-WWOnline");
        if (patched) FakeGameFolder.Create(game);
        var dolphin = Path.Combine(_dir, "Dolphin.exe");
        File.WriteAllBytes(dolphin, [0]);

        var s = _settings.Load();
        s.VanillaGamePath = vanilla;
        s.GamePath = game;
        s.DolphinPath = dolphin;
        _settings.Save(s);
    }

    [Fact]
    public void NewInstall_ShowsSetup_AndSavesNothing()
    {
        Assert.Equal(FirstRun.Decision.ShowSetup, FirstRun.Resolve(_settings, Interactive));
        Assert.False(_settings.Load().SetupCompleted);
    }

    [Fact]
    public void ExistingSettings_WithAPatchedGame_AreMigratedAsCompleted()
    {
        SaveWorkingSetup(patched: true);

        Assert.Equal(FirstRun.Decision.MigratedAsCompleted, FirstRun.Resolve(_settings, Interactive));
        Assert.True(_settings.Load().SetupCompleted);
        Assert.Equal(FirstRun.Decision.AlreadyCompleted, FirstRun.Resolve(_settings, Interactive));
    }

    [Fact]
    public void ExistingSettings_WithoutAPatchedGame_StillShowSetup()
    {
        SaveWorkingSetup(patched: false);
        Assert.Equal(FirstRun.Decision.ShowSetup, FirstRun.Resolve(_settings, Interactive));
        Assert.False(_settings.Load().SetupCompleted);
    }

    [Fact]
    public void Completed_IsNeverShownAgain_EvenWithBrokenPaths()
    {
        var s = _settings.Load();
        s.SetupCompleted = true;
        _settings.Save(s);
        Assert.Equal(FirstRun.Decision.AlreadyCompleted, FirstRun.Resolve(_settings, Interactive));
    }

    [Fact]
    public void ScriptedStarts_SkipSetup_WithoutMarkingItDone()
    {
        var devTest = new StartupOptions("Player2", "localhost", 6969, AutoConnect: true, AutoAttach: true, DolphinPid: 42);
        Assert.Equal(FirstRun.Decision.Scripted, FirstRun.Resolve(_settings, devTest));
        Assert.False(_settings.Load().SetupCompleted);
    }

    [Fact]
    public void OldSettingsFile_WithoutTheFlag_ReadsAsNotCompleted()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "settings"));
        File.WriteAllText(Path.Combine(_dir, "settings", "game-settings.json"), """{ "PlayerName": "Tetra" }""");
        var s = _settings.Load();
        Assert.False(s.SetupCompleted);
        Assert.True(s.AutoLaunchDolphin);
        Assert.Equal("Tetra", s.PlayerName);
    }
}
