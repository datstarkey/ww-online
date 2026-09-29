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
    private int? _connectedPid;
    private long? _mem1 = 0x3000000;
    private byte[]? _hookStatus = "HOOK"u8.ToArray();
    private int _connects;
    private OptionalPatchBuildStatus _build = new(false, "up to date", []);

    public GameLaunchServiceTests()
    {
        _settings = new GameSettingsService(Path.Combine(_dir, "settings"));
        _dolphin.SetupGet(d => d.IsConnected).Returns(() => _connected);
        _dolphin.SetupGet(d => d.ConnectedProcessId).Returns(() => _connected ? _connectedPid : null);
        _dolphin.SetupGet(d => d.EmulatedMemorySize).Returns(() => _connected ? _mem1 : null);
        _dolphin.Setup(d => d.ReadMemory(Data.PuppetLayout.STATUS_ADDR, 4)).Returns(() => _connected ? _hookStatus : null);
        _dolphin.Setup(d => d.EnumerateProcesses()).Returns(() => _running);
        _dolphin.Setup(d => d.ConnectAsync(It.IsAny<int>())).Returns((int pid) =>
        {
            Interlocked.Increment(ref _connects);
            _connected = true;
            _connectedPid = pid;
            return Task.FromResult(true);
        });
        _dolphin.Setup(d => d.Disconnect()).Callback(() => { _connected = false; _connectedPid = null; });
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

        public List<int> Closed = [];
        public bool CloseSucceeds = true;

        public bool Close(int processId, TimeSpan timeout)
        {
            Closed.Add(processId);
            return CloseSucceeds;
        }
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
    public async Task IdleDolphin_WithoutAGame_IsClosed_AndThePatchedGameStarted()
    {
        SaveSetup();
        _running = [new DolphinProcessInfo(99, "Dolphin")];
        _dolphin.Setup(d => d.ConnectAsync(99)).ReturnsAsync(false); // no game booted in it
        using var launch = Service();

        var result = await launch.StartGameAsync();

        Assert.Equal([99], _starter.Closed);
        Assert.Equal(1, _starter.Starts);
        Assert.Equal(GameLaunchState.Attached, result.State);
        _dolphin.Verify(d => d.ConnectAsync(7), Times.AtLeastOnce);
    }

    [Fact]
    public async Task IdleDolphin_ThatWontClose_IsRefused_WithWhatToDo()
    {
        SaveSetup();
        _running = [new DolphinProcessInfo(99, "Dolphin")];
        _dolphin.Setup(d => d.ConnectAsync(99)).ReturnsAsync(false);
        _starter.CloseSucceeds = false;
        using var launch = Service();

        var result = await launch.StartGameAsync();

        Assert.Equal(GameLaunchState.Blocked, result.State);
        Assert.Contains("Close Dolphin", result.Message);
        Assert.Equal(0, _starter.Starts);
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
    public async Task RunningDolphinWithout48MB_IsRefused_AndLeftAlone()
    {
        SaveSetup();
        _running = [new DolphinProcessInfo(99, "Dolphin")];
        _mem1 = 0x1800000; // opened by the player without the memory override
        using var launch = Service();

        var result = await launch.StartGameAsync();

        Assert.Equal(GameLaunchState.Blocked, result.State);
        Assert.Contains("48 MB", result.Message);
        Assert.Contains("let WW-Online start it", result.Message);
        Assert.Equal(0, _attachedCount); // no sync writing into it
        Assert.False(_connected);
    }

    [Fact]
    public async Task RunningUnpatchedGame_IsRefused_WithoutSyncing()
    {
        SaveSetup();
        _running = [new DolphinProcessInfo(99, "Dolphin")];
        _hookStatus = [0, 0, 0, 0]; // vanilla / PAL: the draw hook never writes its status word
        using var launch = new GameLaunchService(_settings, _ => _build, _dolphin.Object,
            () => Interlocked.Increment(ref _attachedCount), _starter, TimeSpan.FromMilliseconds(1), maxAttachAttempts: 20);

        var result = await launch.StartGameAsync();

        Assert.Equal(GameLaunchState.Blocked, result.State);
        Assert.Contains("isn't your patched", result.Message);
        Assert.Equal(0, _attachedCount);
        Assert.False(_connected);
        Assert.Equal(6, _connects); // Start game's first look, then a few seconds' grace for Link to be drawn, then no
    }

    [Fact]
    public async Task APatchedGameOnFileSelect_IsWaitedFor_NotRefused()
    {
        SaveSetup();
        _hookStatus = [0, 0, 0, 0];   // Link not drawn yet
        byte[] maxLife = [0, 0];      // no save loaded
        _dolphin.Setup(d => d.ReadMemory(Data.GameMemoryAddresses.Player.MaxHealth.Address, 2)).Returns(() => _connected ? maxLife : null);
        _dolphin.Setup(d => d.ConnectAsync(It.IsAny<int>())).Returns((int pid) =>
        {
            // Far longer on file select than the unpatched grace (5) or the whole budget (3), then a save loads.
            if (++_connects == 12) { maxLife = [0, 12]; _hookStatus = "HOOK"u8.ToArray(); }
            _connected = true;
            _connectedPid = pid;
            return Task.FromResult(true);
        });
        using var launch = Service();
        var seen = new List<GameLaunchStatus>();
        launch.StatusChanged += seen.Add;

        Assert.Equal(GameLaunchState.Attached, (await launch.StartGameAsync()).State);
        Assert.Equal(1, _attachedCount);
        Assert.Contains(seen, s => s.Message.Contains("load your save"));
    }

    [Fact]
    public async Task AnUnpatchedGameWithASaveLoaded_IsStillRefused()
    {
        SaveSetup();
        _hookStatus = [0, 0, 0, 0];
        _dolphin.Setup(d => d.ReadMemory(Data.GameMemoryAddresses.Player.MaxHealth.Address, 2)).Returns(() => _connected ? new byte[] { 0, 12 } : null);
        using var launch = new GameLaunchService(_settings, _ => _build, _dolphin.Object,
            () => Interlocked.Increment(ref _attachedCount), _starter, TimeSpan.FromMilliseconds(1), maxAttachAttempts: 20);

        var result = await launch.StartGameAsync();
        Assert.Equal(GameLaunchState.Blocked, result.State);
        Assert.Contains("isn't your patched", result.Message);
    }

    [Fact]
    public async Task PatchedHook_ShowingUpAfterBoot_IsAccepted()
    {
        SaveSetup();
        _hookStatus = null;
        _dolphin.Setup(d => d.ConnectAsync(It.IsAny<int>())).Returns((int pid) =>
        {
            if (++_connects == 3) _hookStatus = "TITL"u8.ToArray(); // the title screen draws Link
            _connected = true;
            _connectedPid = pid;
            return Task.FromResult(true);
        });
        using var launch = Service();

        Assert.Equal(GameLaunchState.Attached, (await launch.StartGameAsync()).State);
        Assert.Equal(1, _attachedCount);
    }

    [Fact]
    public async Task ManualAttach_ToAnUnpatchedGame_AttachesWithAWarning()
    {
        _hookStatus = "GZLP"u8.ToArray();
        using var launch = Service();

        var (attached, warning) = await launch.AttachManuallyAsync(42);

        Assert.True(attached);
        Assert.Contains("isn't your patched", warning);
        Assert.Equal(1, _attachedCount);

        var again = await launch.AttachManuallyAsync(42); // already attached: no second sync start
        Assert.True(again.Attached);
        Assert.Equal(1, _attachedCount);
    }

    [Fact]
    public async Task ConcurrentAttaches_ConnectAndStartSyncOnce()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dolphin.Setup(d => d.ConnectAsync(It.IsAny<int>())).Returns(async (int pid) =>
        {
            Interlocked.Increment(ref _connects);
            await gate.Task;
            _connected = true;
            _connectedPid = pid;
            return true;
        });
        using var launch = Service();

        var first = launch.AttachManuallyAsync(42);
        var second = launch.AttachManuallyAsync(42);
        await Task.Delay(50);
        gate.SetResult(true);
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.True(r.Attached));
        Assert.Equal(1, _connects);
        Assert.Equal(1, _attachedCount);
    }

    [Fact]
    public async Task OnAttached_RunsThroughTheUiMarshaller()
    {
        int marshalled = 0;
        using var launch = new GameLaunchService(_settings, _ => _build, _dolphin.Object,
            () => Interlocked.Increment(ref _attachedCount), _starter, TimeSpan.FromMilliseconds(1), 3,
            a => { marshalled++; a(); return Task.CompletedTask; });
        await launch.AttachManuallyAsync(42);
        Assert.Equal(1, marshalled);
        Assert.Equal(1, _attachedCount);
    }

    [Fact]
    public async Task LeavingTheRoom_CancelsTheWaitForTheGame()
    {
        SaveSetup();
        _dolphin.Setup(d => d.ConnectAsync(It.IsAny<int>())).ReturnsAsync(false); // still booting
        using var launch = new GameLaunchService(_settings, _ => _build, _dolphin.Object,
            () => Interlocked.Increment(ref _attachedCount), _starter, TimeSpan.FromMilliseconds(20), maxAttachAttempts: 10_000);
        using var cts = new CancellationTokenSource();

        var start = launch.StartGameAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        var result = await start.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(GameLaunchState.Idle, result.State);
        Assert.Equal(0, _attachedCount);
    }

    [Fact]
    public void HookStatusWords_AreRecognised()
    {
        foreach (var s in new[] { "HOOK", "TITL", "NAME", "DONE" }) // as the game stores them: ASCII, big-endian
            Assert.True(PatchedGameCheck.IsHookStatus(System.Text.Encoding.ASCII.GetBytes(s)), s);
        Assert.Equal(4, PatchedGameCheck.HookStatuses.Distinct().Count());
        Assert.False(PatchedGameCheck.IsHookStatus("KOOH"u8.ToArray())); // not little-endian
        Assert.False(PatchedGameCheck.IsHookStatus([0, 0, 0, 0]));
        Assert.False(PatchedGameCheck.IsHookStatus(null));
        Assert.False(PatchedGameCheck.IsHookStatus("HOO"u8.ToArray()));
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
