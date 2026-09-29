using System.Diagnostics;
using Serilog;
using WWOnline.Patcher.Pipeline;

namespace WWOnline.Services;

/// <summary>Where starting the game has got to (<see cref="GameLaunchService.Status"/>).</summary>
public enum GameLaunchState
{
    /// <summary>Nothing happening (or never started).</summary>
    Idle,
    /// <summary>Checking the patched game / starting Dolphin.</summary>
    Starting,
    /// <summary>Dolphin is running; waiting for the game to boot so the app can attach.</summary>
    WaitingForGame,
    /// <summary>Attached to Dolphin.</summary>
    Attached,
    /// <summary>Refused: the game isn't patched, is out of date, or setup is missing a piece.</summary>
    Blocked,
    /// <summary>Tried and failed (Dolphin didn't start, closed, or the game never booted).</summary>
    Failed,
}

/// <param name="State">What's happening.</param>
/// <param name="Message">One line for the UI and the log.</param>
public sealed record GameLaunchStatus(GameLaunchState State, string Message)
{
    public static GameLaunchStatus Idle { get; } = new(GameLaunchState.Idle, "");

    /// <summary>Something the player should read (a refusal or a failure).</summary>
    public bool IsProblem => State is GameLaunchState.Blocked or GameLaunchState.Failed;

    /// <summary>Starting Dolphin or waiting for the game.</summary>
    public bool IsBusy => State is GameLaunchState.Starting or GameLaunchState.WaitingForGame;
}

/// <summary>Whether the app may start Dolphin / attach to it automatically, and if not, why.</summary>
public sealed record LaunchReadiness(bool CanLaunch, string? Problem)
{
    public static LaunchReadiness Ready { get; } = new(true, null);

    /// <summary>Shown whenever the patched game isn't up to date: the app never starts or attaches to it.</summary>
    public const string NotPatchedMessage =
        "Your game needs patching, so WW-Online won't start Dolphin or attach to it. Patch the game (Settings → Patch game) and try again.";

    /// <summary>
    /// The one rule for the automatic paths (auto-start after host/join, the Dolphin page's Start game):
    /// Dolphin must be set up and the patched game must be up to date. Manual attach stays the player's call.
    /// </summary>
    /// <param name="settings">The saved settings.</param>
    /// <param name="build">The build check of <see cref="GameSettings.GamePath"/> (null = the check failed).</param>
    public static LaunchReadiness Evaluate(GameSettings settings, OptionalPatchBuildStatus? build)
    {
        if (DolphinLocator.Validate(settings.DolphinPath) is { } dolphinProblem)
            return new(false, $"Dolphin isn't set up: {dolphinProblem} Choose it in Settings, or start the game yourself and attach on the Dolphin page.");
        if (string.IsNullOrWhiteSpace(settings.GamePath))
            return new(false, "No patched game folder is set. Run the setup (Settings → Run setup) to choose one.");
        if (build == null)
            return new(false, "Couldn't check your patched game, so WW-Online won't start Dolphin or attach to it. Patch the game (Settings → Patch game) and try again.");
        if (build.NeedsRepatch)
            return new(false, NotPatchedMessage);
        if (!File.Exists(Path.Combine(settings.GamePath, "sys", "main.dol")))
            return new(false, NotPatchedMessage);
        return Ready;
    }

    /// <summary>
    /// The CLI's --auto-attach (dev-test.ps1): only onto a game whose build check says fresh, unless
    /// --allow-stale (dev-test -AllowStale) says otherwise.
    /// </summary>
    public static LaunchReadiness EvaluateAutoAttach(BuildStamp.CheckResult? build, bool allowStale)
    {
        if (build?.Status == BuildStamp.Status.Fresh || allowStale) return Ready;
        return new(false, build == null
            ? "couldn't check the game build"
            : "the game build isn't up to date: " + build.Describe());
    }
}

/// <summary>Starts Dolphin (a fake in tests).</summary>
public interface IDolphinStarter
{
    /// <summary>Start <paramref name="dolphinExe"/> booting <paramref name="mainDol"/>.</summary>
    /// <returns>The new process id, or null (with <paramref name="error"/>).</returns>
    int? Start(string dolphinExe, string mainDol, out string? error);

    /// <summary>Has the process started by <see cref="Start"/> exited?</summary>
    bool HasExited(int processId);

    /// <summary>Ask Dolphin <paramref name="processId"/> to close (its window's close, never a kill) and wait for it.</summary>
    /// <returns>True once it has exited.</returns>
    bool Close(int processId, TimeSpan timeout);
}

/// <summary>Starts the real Dolphin.exe.</summary>
public sealed class DolphinProcessStarter : IDolphinStarter
{
    /// <summary>
    /// Dolphin's session-only config overrides (not saved to its settings): the patched game needs the
    /// 48 MB MEM1 (use_extra_memory.asm + bi2.bin), like dev-test.ps1's Dolphin.ini. With Dolphin's
    /// default 24 MB it hangs on a black screen.
    /// </summary>
    public static readonly string[] MemoryOverrideArgs =
        ["-C", "Dolphin.Core.RAMOverrideEnable=True", "-C", "Dolphin.Core.MEM1Size=0x03000000"];

    public int? Start(string dolphinExe, string mainDol, out string? error)
    {
        try
        {
            var psi = new ProcessStartInfo(dolphinExe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(dolphinExe) ?? "",
            };
            foreach (var arg in MemoryOverrideArgs) psi.ArgumentList.Add(arg);
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add(mainDol);

            using var process = Process.Start(psi);
            if (process == null)
            {
                error = "Dolphin didn't start.";
                return null;
            }
            error = null;
            return process.Id;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            error = ex.Message;
            return null;
        }
    }

    public bool Close(int processId, TimeSpan timeout)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            if (p.HasExited) return true;
            p.CloseMainWindow();
            return p.WaitForExit(timeout);
        }
        catch (ArgumentException)
        {
            return true; // already gone
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public bool HasExited(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            return p.HasExited;
        }
        catch (ArgumentException)
        {
            return true; // no such process any more
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}

/// <summary>
/// Starts the patched game in Dolphin and attaches to it: after hosting or joining a room (when
/// <see cref="GameSettings.AutoLaunchDolphin"/> is on), from the Dolphin page's Start game, and the
/// CLI's --auto-attach. Also the one "attach to this Dolphin" implementation (the Dolphin page's
/// Attach button uses <see cref="AttachAsync"/>).
///
/// The automatic paths refuse, with a message (<see cref="Status"/>), unless the patched game is up to
/// date (<see cref="LaunchReadiness.Evaluate"/>): they never start or attach to an unpatched game.
/// </summary>
public sealed class GameLaunchService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GameLaunchService>();

    private readonly GameSettingsService _settings;
    private readonly Func<string, OptionalPatchBuildStatus> _checkBuild;
    private readonly IDolphinService _dolphin;
    private readonly Action _onAttached;
    private readonly IDolphinStarter _starter;
    private readonly TimeSpan _attachInterval;
    private readonly int _maxAttachAttempts;
    private readonly Func<Action, Task> _runOnUi;
    private readonly SemaphoreSlim _attachLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private int _busy;
    private GameLaunchStatus _status = GameLaunchStatus.Idle;

    public GameLaunchService(GameSettingsService settings, OptionalPatchCatalogService patchCatalog, IDolphinService dolphin,
        GameMemoryMonitorService memoryMonitor, GameSyncService gameSync)
        : this(settings, patchCatalog.CheckGameBuild, dolphin, () => { memoryMonitor.Start(); gameSync.Start(); },
            new DolphinProcessStarter(), TimeSpan.FromSeconds(2), maxAttachAttempts: 90,
            a => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(a).GetTask())
    {
    }

    /// <param name="settings">Dolphin path and patched game folder.</param>
    /// <param name="checkBuild">The patched game's build check (OptionalPatchCatalogService.CheckGameBuild).</param>
    /// <param name="dolphin">Enumerates and attaches to Dolphin.</param>
    /// <param name="onAttached">Starts the memory monitor and game sync once attached.</param>
    /// <param name="starter">Starts Dolphin.</param>
    /// <param name="attachInterval">Wait between attach attempts while the game boots.</param>
    /// <param name="maxAttachAttempts">Attempts before giving up (90 × 2s in the app).</param>
    /// <param name="runOnUi">Runs <paramref name="onAttached"/> on the UI thread (tests run it inline; null = inline).</param>
    public GameLaunchService(GameSettingsService settings, Func<string, OptionalPatchBuildStatus> checkBuild, IDolphinService dolphin,
        Action onAttached, IDolphinStarter starter, TimeSpan attachInterval, int maxAttachAttempts, Func<Action, Task>? runOnUi = null)
    {
        _runOnUi = runOnUi ?? (a => { a(); return Task.CompletedTask; });
        _settings = settings;
        _checkBuild = checkBuild;
        _dolphin = dolphin;
        _onAttached = onAttached;
        _starter = starter;
        _attachInterval = attachInterval;
        _maxAttachAttempts = maxAttachAttempts;
    }

    /// <summary>The latest status. <see cref="StatusChanged"/> fires on any thread.</summary>
    public GameLaunchStatus Status => Volatile.Read(ref _status);

    public event Action<GameLaunchStatus>? StatusChanged;

    /// <summary>Forget a finished status (the UI's dismiss).</summary>
    public void ClearStatus()
    {
        if (!Status.IsBusy) SetStatus(GameLaunchStatus.Idle, log: false);
    }

    /// <summary>
    /// Can the app start / attach to the game by itself right now? Runs the build check (hashes the
    /// GameMod sources in dev), so call it off the UI thread.
    /// </summary>
    public LaunchReadiness CheckReadiness()
    {
        var settings = _settings.Load();
        OptionalPatchBuildStatus? build;
        try
        {
            build = _checkBuild(settings.GamePath);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[launch] build check failed");
            build = null;
        }
        return LaunchReadiness.Evaluate(settings, build);
    }

    /// <summary>
    /// Start the patched game and attach to it. Refuses (<see cref="GameLaunchState.Blocked"/>) when the
    /// game isn't patched and up to date. Reuses an already running Dolphin instead of starting a second.
    /// Returns the final status; a call while another is running returns at once with the current one.
    /// </summary>
    public async Task<GameLaunchStatus> StartGameAsync(CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return Status;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        try
        {
            if (_dolphin.IsConnected)
                return SetStatus(new(GameLaunchState.Attached, $"Already attached to Dolphin (PID {_dolphin.ConnectedProcessId})."));

            SetStatus(new(GameLaunchState.Starting, "Checking your patched game…"), log: false);
            var readiness = await Task.Run(CheckReadiness, linked.Token).ConfigureAwait(false);
            if (!readiness.CanLaunch)
                return SetStatus(new(GameLaunchState.Blocked, readiness.Problem!));

            var running = _dolphin.EnumerateProcesses();
            int pid;
            bool startedHere;
            if (running.Count > 1)
                return SetStatus(new(GameLaunchState.Blocked,
                    "Several Dolphins are running, so WW-Online won't guess which one to use. Attach on the Dolphin page."));
            if (running.Count == 1)
            {
                // A Dolphin with no game booted would run whatever the player then picks in its game list,
                // usually the original disc image: close it and start the patched game instead. One with a
                // game running is left alone (it may be the patched game, or unsaved progress).
                int open = running[0].ProcessId;
                var probe = await AttachAsync(open, requirePatchedGame: true).ConfigureAwait(false);
                if (probe == PatchedGameCheck.Result.Ok)
                    return SetStatus(new(GameLaunchState.Attached, $"Attached to Dolphin (PID {open})."));
                if (probe == PatchedGameCheck.Result.NotBooted)
                {
                    SetStatus(new(GameLaunchState.Starting, "Dolphin is open without a game: restarting it with your patched game…"));
                    if (!_starter.Close(open, CloseTimeout))
                        return SetStatus(new(GameLaunchState.Blocked,
                            "Dolphin is open without a game and didn't close. Close Dolphin, then press Start game: " +
                            "WW-Online starts it with your patched game (Dolphin's game list boots the original disc)."));
                    running = [];
                }
            }
            if (running.Count == 1)
            {
                pid = running[0].ProcessId;
                startedHere = false;
                SetStatus(new(GameLaunchState.WaitingForGame,
                    "Dolphin is already open: attaching once your patched game is running in it…"));
            }
            else
            {
                var settings = _settings.Load();
                var mainDol = Path.Combine(settings.GamePath, "sys", "main.dol");
                SetStatus(new(GameLaunchState.Starting, "Starting Dolphin…"), log: false);
                Logger.Information("[launch] starting {Dolphin} -e {MainDol}", settings.DolphinPath, mainDol);
                var started = _starter.Start(settings.DolphinPath, mainDol, out var error);
                if (started is not int newPid)
                    return SetStatus(new(GameLaunchState.Failed, $"Couldn't start Dolphin: {error}"));
                pid = newPid;
                startedHere = true;
                SetStatus(new(GameLaunchState.WaitingForGame, $"Dolphin started (PID {pid}). Waiting for the game to boot…"));
            }

            var attached = await WaitAndAttachAsync(pid, startedHere, linked.Token).ConfigureAwait(false);
            return attached switch
            {
                AttachOutcome.Attached => SetStatus(new(GameLaunchState.Attached, $"Attached to Dolphin (PID {pid}).")),
                AttachOutcome.SmallMemory or AttachOutcome.NotPatched =>
                    SetStatus(new(GameLaunchState.Blocked, OutcomeMessage(attached, startedHere))),
                _ => SetStatus(new(GameLaunchState.Failed, OutcomeMessage(attached, startedHere))),
            };
        }
        catch (OperationCanceledException)
        {
            return SetStatus(GameLaunchStatus.Idle, log: false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[launch] starting the game failed");
            return SetStatus(new(GameLaunchState.Failed, $"Starting the game failed: {ex.Message}"));
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    /// <summary>
    /// The CLI's --auto-attach: keep trying until the game has booted in Dolphin (<paramref name="processId"/>,
    /// else the first Dolphin found). The caller has already applied <see cref="LaunchReadiness.EvaluateAutoAttach"/>;
    /// the running game must still pass <see cref="PatchedGameCheck"/>.
    /// </summary>
    public async Task<bool> WaitAndAttachAsync(int? processId, CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        try
        {
            var outcome = await WaitAndAttachAsync(processId, startedHere: false, linked.Token).ConfigureAwait(false);
            if (outcome is AttachOutcome.SmallMemory or AttachOutcome.NotPatched)
                Logger.Error("[launch] {Message}", OutcomeMessage(outcome, startedHere: false));
            return outcome == AttachOutcome.Attached;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// The Dolphin page's Attach: attach to <paramref name="processId"/> even if the running game fails
    /// <see cref="PatchedGameCheck"/> (the player's call), but say so.
    /// </summary>
    /// <returns>Attached (with a warning when the game doesn't look like the patched one, else null), or not.</returns>
    public async Task<(bool Attached, string? Warning)> AttachManuallyAsync(int processId)
    {
        var result = await AttachAsync(processId, requirePatchedGame: false).ConfigureAwait(false);
        return result switch
        {
            PatchedGameCheck.Result.Ok or PatchedGameCheck.Result.NotInGame => (true, null),
            PatchedGameCheck.Result.SmallMemory or PatchedGameCheck.Result.NotPatched =>
                (true, "Attached anyway, but careful: " + OutcomeMessage(ToOutcome(result), startedHere: false)),
            _ => (false, null),
        };
    }

    /// <summary>
    /// Attach to one Dolphin and, once the game passes <see cref="PatchedGameCheck"/>, start the memory
    /// monitor and game sync (on the UI thread, like the Attach button always did). One attach at a
    /// time: a second caller waits, then finds the first one's connection. With
    /// <paramref name="requirePatchedGame"/>, a game that fails the check is detached again.
    /// </summary>
    /// <returns><see cref="PatchedGameCheck.Result.NotBooted"/> when Dolphin has no game (yet).</returns>
    private async Task<PatchedGameCheck.Result> AttachAsync(int processId, bool requirePatchedGame)
    {
        await _attachLock.WaitAsync(_shutdown.Token).ConfigureAwait(false);
        try
        {
            if (_dolphin.IsConnected)
                return _dolphin.ConnectedProcessId == processId ? PatchedGameCheck.Result.Ok : PatchedGameCheck.Result.NotBooted;
            if (!await _dolphin.ConnectAsync(processId).ConfigureAwait(false)) return PatchedGameCheck.Result.NotBooted;

            var check = PatchedGameCheck.Check(_dolphin);
            if (check != PatchedGameCheck.Result.Ok && requirePatchedGame)
            {
                _dolphin.Disconnect();
                Logger.Information("[launch] PID {Pid}: not attaching yet ({Check})", processId, check);
                return check;
            }
            await _runOnUi(_onAttached).ConfigureAwait(false);
            Logger.Information("[launch] attached to Dolphin PID {Pid} ({Check})", processId, check);
            return check;
        }
        finally
        {
            _attachLock.Release();
        }
    }

    /// <summary>How long an idle Dolphin gets to close before Start game gives up on replacing it.</summary>
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    /// <summary>How many 2s attempts a booted game may take to show the patch's draw hook before we give up on it.</summary>
    private const int NotPatchedGraceAttempts = 5;

    private enum AttachOutcome { Attached, TimedOut, ProcessExited, SmallMemory, NotPatched }

    private static AttachOutcome ToOutcome(PatchedGameCheck.Result r) => r switch
    {
        PatchedGameCheck.Result.SmallMemory => AttachOutcome.SmallMemory,
        PatchedGameCheck.Result.NotPatched => AttachOutcome.NotPatched,
        PatchedGameCheck.Result.Ok => AttachOutcome.Attached,
        _ => AttachOutcome.TimedOut,
    };

    private static string OutcomeMessage(AttachOutcome outcome, bool startedHere) => outcome switch
    {
        AttachOutcome.SmallMemory =>
            "Dolphin is running without the 48 MB memory setting the patched game needs, so WW-Online won't use it. " +
            "Close Dolphin and let WW-Online start it (it turns the setting on), or turn on Config → Advanced → Enable " +
            "Emulated Memory Size Override with MEM1 at 48 MB.",
        AttachOutcome.NotPatched =>
            "The game running in Dolphin isn't your patched WW-Online game, so WW-Online won't link up with it. " +
            "Close Dolphin and let WW-Online start it.",
        AttachOutcome.ProcessExited => "Dolphin closed before the game started.",
        _ => startedHere
            ? "The game didn't finish booting in time. Once it's running, attach on the Dolphin page."
            : "No game showed up in Dolphin in time. Boot your patched game in it (or close Dolphin and let WW-Online start it).",
    };

    private async Task<AttachOutcome> WaitAndAttachAsync(int? processId, bool startedHere, CancellationToken ct)
    {
        int notPatched = 0;
        bool saidLoadSave = false;
        for (int attempt = 1; attempt <= _maxAttachAttempts; attempt++)
        {
            await Task.Delay(_attachInterval, ct).ConfigureAwait(false);
            if (startedHere && processId is int started && _starter.HasExited(started)) return AttachOutcome.ProcessExited;

            var target = processId ?? _dolphin.EnumerateProcesses().FirstOrDefault()?.ProcessId;
            if (target is not int pid) continue;
            ct.ThrowIfCancellationRequested();
            var check = await AttachAsync(pid, requirePatchedGame: true).ConfigureAwait(false);
            switch (check)
            {
                case PatchedGameCheck.Result.Ok:
                    Logger.Information("[launch] attached on attempt {Attempt}", attempt);
                    return AttachOutcome.Attached;
                case PatchedGameCheck.Result.SmallMemory:
                    return AttachOutcome.SmallMemory;
                case PatchedGameCheck.Result.NotPatched:
                    // A save is loaded but Link hasn't been drawn by the patched hook yet: give it a few tries.
                    if (++notPatched >= NotPatchedGraceAttempts) return AttachOutcome.NotPatched;
                    break;
                case PatchedGameCheck.Result.NotInGame:
                    // Title screen or file select: nothing to judge until a save is loaded, however long
                    // the player takes (leaving the room or closing Dolphin still ends the wait).
                    notPatched = 0;
                    attempt--;
                    if (!saidLoadSave)
                    {
                        saidLoadSave = true;
                        SetStatus(new(GameLaunchState.WaitingForGame, "Your game is running: load your save and WW-Online links up."));
                    }
                    break;
            }
            if (attempt % 10 == 0)
                Logger.Information("[launch] still waiting for the game to boot (attempt {Attempt}/{Max})", attempt, _maxAttachAttempts);
        }
        Logger.Warning("[launch] gave up attaching after {Max} attempts", _maxAttachAttempts);
        return notPatched > 0 ? AttachOutcome.NotPatched : AttachOutcome.TimedOut;
    }


    private GameLaunchStatus SetStatus(GameLaunchStatus status, bool log = true)
    {
        Volatile.Write(ref _status, status);
        if (log && status.Message.Length > 0)
        {
            if (status.IsProblem) Logger.Warning("[launch] {Message}", status.Message);
            else Logger.Information("[launch] {Message}", status.Message);
        }
        try { StatusChanged?.Invoke(status); }
        catch (Exception ex) { Logger.Error(ex, "[launch] a StatusChanged handler failed"); }
        return status;
    }

    /// <summary>
    /// Stop any wait for the game (app shutdown). The token source is cancelled, not disposed: a
    /// start racing the shutdown may still link to its token, and it owns no timer or wait handle.
    /// </summary>
    public void Dispose() => _shutdown.Cancel();
}
