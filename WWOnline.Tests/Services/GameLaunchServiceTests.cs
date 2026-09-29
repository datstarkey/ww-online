using Moq;
using WWOnline.Patcher.Pipeline;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The automatic start/attach paths refuse an unpatched or stale game (never start Dolphin, never
/// attach), and otherwise start Dolphin (or reuse a running one) and attach.
/// </summary>
public sealed class GameLaunchServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-launch-" + Guid.NewGuid().ToString("N"));
    private readonly GameSettingsService _settings;
    private readonly Mock<IDolphinService> _dolphin = new();
    private readonly FakeStarter _starter = new();
    private List<DolphinProcessInfo> _running = [];
    private int _attachedCount;
    private bool _connected;
    private OptionalPatchBuildStatus _build = new(false, "up to date", []);

    public GameLaunchServiceTests()
    {
        _settings = new GameSettingsService(Path.Combine(_dir, "settings"));
        _dolphin.SetupGet(d => d.IsConnected).Returns(() => _connected);
        _dolphin.SetupGet(d => d.ConnectedProcessId).Returns(() => _connected ? 7 : null);
        _dolphin.Setup(d => d.EnumerateProcesses()).Returns(() => _running);
        _dolphin.Setup(d => d.ConnectAsync(It.IsAny<int>())).ReturnsAsync(() =>
        {
            _connected = true;
            return true;
        });
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private sealed class FakeStarter : IDolphinStarter
    {
        public int Starts;
        public int? NextPid = 7;
        public bool Exited;

        public int? Start(string dolphinExe, string mainDol, out string? error)
        {
            Starts++;
            error = NextPid == null ? "nope" : null;
            return NextPid;
        }

        public bool HasExited(int processId) => Exited;
    }

    private GameLaunchService Service() =>
        new(_settings, _ => _build, _dolphin.Object, () => Interlocked.Increment(ref _attachedCount), _starter,
            TimeSpan.FromMilliseconds(1), maxAttachAttempts: 3);

    private void SaveSetup(bool withDolphin = true, bool withPatchedGame = true)
    {
        var game = Path.Combine(_dir, "patched");
        if (withPatchedGame) FakeGameFolder.Create(game);
        var dolphin = Path.Combine(_dir, "Dolphin.exe");
        if (withDolphin) File.WriteAllBytes(dolphin, [0]);
        var s = _settings.Load();
        s.GamePath = game;
        s.DolphinPath = dolphin;
        _settings.Save(s);
    }

    [Fact]
    public async Task StaleGame_IsRefused_WithoutStartingOrAttaching()
    {
        SaveSetup();
        _build = new OptionalPatchBuildStatus(true, "game build is STALE", []);
        using var launch = Service();
        var seen = new List<GameLaunchStatus>();
        launch.StatusChanged += seen.Add;

        var result = await launch.StartGameAsync();

        Assert.Equal(GameLaunchState.Blocked, result.State);
        Assert.Contains("won't start Dolphin", result.Message);
        Assert.Equal(0, _starter.Starts);
        _dolphin.Verify(d => d.ConnectAsync(It.IsAny<int>()), Times.Never);
        Assert.Equal(0, _attachedCount);
        Assert.Equal(GameLaunchState.Blocked, seen[^1].State);
    }

    [Fact]
    public async Task StaleGame_IsRefused_EvenWithDolphinAlreadyRunning()
    {
        SaveSetup();
        _running = [new DolphinProcessInfo(99, "Dolphin")];
        _build = new OptionalPatchBuildStatus(true, "no build stamp", []);
        using var launch = Service();

        Assert.Equal(GameLaunchState.Blocked, (await launch.StartGameAsync()).State);
        _dolphin.Verify(d => d.ConnectAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task FailedBuildCheck_IsRefused()
    {
        SaveSetup();
        using var launch = new GameLaunchService(_settings, _ => throw new IOException("locked"), _dolphin.Object,
            () => { }, _starter, TimeSpan.FromMilliseconds(1), 3);
        Assert.Equal(GameLaunchState.Blocked, (await launch.StartGameAsync()).State);
        Assert.Equal(0, _starter.Starts);
    }

    [Fact]
    public async Task NoDolphin_IsRefused_WithWhatToDo()
    {
        SaveSetup(withDolphin: false);
        using var launch = Service();
        var result = await launch.StartGameAsync();
        Assert.Equal(GameLaunchState.Blocked, result.State);
        Assert.Contains("Dolphin isn't set up", result.Message);
        Assert.Equal(0, _starter.Starts);
    }

    [Fact]
    public async Task FreshGame_StartsDolphin_AndAttaches()
    {
        SaveSetup();
        using var launch = Service();

        var result = await launch.StartGameAsync();

        Assert.Equal(GameLaunchState.Attached, result.State);
        Assert.Equal(1, _starter.Starts);
        _dolphin.Verify(d => d.ConnectAsync(7), Times.Once);
        Assert.Equal(1, _attachedCount);
    }

    [Fact]
    public async Task FreshGame_ReusesARunningDolphin()
    {
        SaveSetup();
        _running = [new DolphinProcessInfo(99, "Dolphin")];
        using var launch = Service();

        Assert.Equal(GameLaunchState.Attached, (await launch.StartGameAsync()).State);
        Assert.Equal(0, _starter.Starts);
        _dolphin.Verify(d => d.ConnectAsync(99), Times.Once);
    }

    [Fact]
    public async Task SeveralRunningDolphins_AreNotGuessedBetween()
    {
        SaveSetup();
        _running = [new DolphinProcessInfo(1, "a"), new DolphinProcessInfo(2, "b")];
        using var launch = Service();
        Assert.Equal(GameLaunchState.Blocked, (await launch.StartGameAsync()).State);
        _dolphin.Verify(d => d.ConnectAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DolphinClosing_BeforeTheGameBoots_IsAFailure()
    {
        SaveSetup();
        _dolphin.Setup(d => d.ConnectAsync(It.IsAny<int>())).ReturnsAsync(false);
        _starter.Exited = true;
        using var launch = Service();
        var result = await launch.StartGameAsync();
        Assert.Equal(GameLaunchState.Failed, result.State);
        Assert.Contains("closed", result.Message);
    }

    [Fact]
    public void CliAutoAttach_OnlyOntoAFreshBuild_UnlessAllowStale()
    {
        var fresh = new BuildStamp.CheckResult(BuildStamp.Status.Fresh, "abc", null);
        var stale = new BuildStamp.CheckResult(BuildStamp.Status.Stale, "abc", new BuildStamp.Stamp("def", DateTime.UtcNow, 1));
        var missing = new BuildStamp.CheckResult(BuildStamp.Status.Missing, "", null);

        Assert.True(LaunchReadiness.EvaluateAutoAttach(fresh, allowStale: false).CanLaunch);
        Assert.False(LaunchReadiness.EvaluateAutoAttach(stale, allowStale: false).CanLaunch);
        Assert.False(LaunchReadiness.EvaluateAutoAttach(missing, allowStale: false).CanLaunch);
        Assert.False(LaunchReadiness.EvaluateAutoAttach(null, allowStale: false).CanLaunch);
        Assert.True(LaunchReadiness.EvaluateAutoAttach(stale, allowStale: true).CanLaunch);
    }

    [Fact]
    public void StartupOptions_ParseAllowStale()
    {
        Assert.True(StartupOptions.Parse(["--auto-attach", "--allow-stale"]).AllowStale);
        Assert.False(StartupOptions.Parse(["--auto-attach"]).AllowStale);
    }
}
