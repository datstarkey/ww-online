using Serilog;
using WWOnline.Shared;

namespace WWOnline.Services;

public enum UpdateStatus
{
    /// <summary>Not a Velopack install (dev build): no update checks at all.</summary>
    Disabled,
    /// <summary>Installed, not checked yet.</summary>
    Idle,
    Checking,
    UpToDate,
    /// <summary>A newer release exists (<see cref="UpdateState.Version"/>); not downloaded yet.</summary>
    Available,
    /// <summary>Downloading <see cref="UpdateState.Version"/>, <see cref="UpdateState.Progress"/> 0-100.</summary>
    Downloading,
    /// <summary>Downloaded; applied on restart (or automatically on the next launch).</summary>
    ReadyToRestart,
    /// <summary>The last check or download failed (<see cref="UpdateState.Error"/>). Checks keep running.</summary>
    Error,
}

/// <summary>An immutable snapshot of the updater, raised by <see cref="UpdateService.StateChanged"/>.</summary>
/// <param name="Version">The available / downloading / ready version (null otherwise).</param>
public sealed record UpdateState(
    UpdateStatus Status,
    string? Version = null,
    int Progress = 0,
    string? Error = null,
    DateTimeOffset? LastChecked = null);

/// <summary>
/// App auto-update (Velopack, GitHub Releases). Checks shortly after <see cref="Start"/> and then
/// every <see cref="UpdateServiceOptions.CheckInterval"/>, downloads in the background
/// (<see cref="UpdateServiceOptions.AutoDownload"/>), and leaves the restart to the player.
///
/// Never throws into callers: every failure becomes <see cref="UpdateStatus.Error"/> plus a log line.
/// A no-op (<see cref="UpdateStatus.Disabled"/>) when the app isn't a Velopack install, so dev runs
/// from bin/ and dev-test.ps1 never touch the network.
/// <see cref="StateChanged"/> fires on background threads; marshal to the UI thread (UpdateViewModel does).
/// </summary>
public sealed class UpdateService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<UpdateService>();

    private readonly IAppUpdater _updater;
    private readonly UpdateServiceOptions _options;
    private readonly SemaphoreSlim _operation = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly object _stateLock = new();
    private Timer? _timer;
    private int _started;
    private int _disposed;
    private UpdateState _state;
    private AvailableUpdate? _available;

    public UpdateService(IAppUpdater updater, UpdateServiceOptions options)
    {
        _updater = updater;
        _options = options;
        _state = new UpdateState(SafeIsInstalled() ? UpdateStatus.Idle : UpdateStatus.Disabled);
    }

    public UpdateState State
    {
        get { lock (_stateLock) return _state; }
    }

    /// <summary>Raised (on a background thread) whenever <see cref="State"/> changes.</summary>
    public event Action<UpdateState>? StateChanged;

    /// <summary>False for dev builds (not installed via Velopack).</summary>
    public bool IsEnabled => State.Status != UpdateStatus.Disabled;

    /// <summary>The running app's version.</summary>
    public string CurrentVersion => SafeCurrentVersion() ?? AppInfo.Version;

    /// <summary>Begin background checks (first after <see cref="UpdateServiceOptions.StartupDelay"/>). Idempotent; returns at once.</summary>
    public void Start()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _started, 1) != 0) return;

        if (State.Status == UpdateStatus.Disabled)
        {
            Logger.Information("[update] {AppName} {Version} is not a Velopack install (dev build); update checks are off",
                AppInfo.DisplayName, AppInfo.Version);
            return;
        }

        // Downloaded last session but not applied yet (Velopack also applies it on the next launch).
        if (SafePendingRestartVersion() is { } pending)
        {
            Logger.Information("[update] {Version} is downloaded and waiting for a restart", pending);
            SetState(new UpdateState(UpdateStatus.ReadyToRestart, pending));
        }

        Logger.Information("[update] {AppName} {Version}: checking {Source} for updates every {Hours:0.#}h{Pre}",
            AppInfo.DisplayName, CurrentVersion, _options.Source, _options.CheckInterval.TotalHours,
            _options.AllowPrerelease ? " (including pre-releases)" : "");
        _timer = new Timer(_ => OnTimer(), null, _options.StartupDelay, _options.CheckInterval);
    }

    private void OnTimer()
    {
        _ = Task.Run(async () =>
        {
            try { await CheckAndMaybeDownloadAsync(_cts.Token); }
            catch (Exception ex) { Logger.Warning(ex, "[update] scheduled check failed"); }
        });
    }

    /// <summary>What the timer runs: check, and (with AutoDownload) fetch what it finds. Skips while downloading or ready.</summary>
    public async Task CheckAndMaybeDownloadAsync(CancellationToken ct)
    {
        var status = State.Status;
        if (status is UpdateStatus.Disabled or UpdateStatus.Downloading or UpdateStatus.ReadyToRestart) return;
        if (await CheckForUpdatesAsync(ct) && _options.AutoDownload)
            await DownloadAsync(ct);
    }

    /// <summary>
    /// Check now. True if an update is available (state <see cref="UpdateStatus.Available"/>).
    /// False when up to date, disabled, busy (another check/download is running), or on error.
    /// </summary>
    public async Task<bool> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        if (State.Status is UpdateStatus.Disabled or UpdateStatus.ReadyToRestart) return false;
        if (!await _operation.WaitAsync(0, CancellationToken.None)) return false; // a check/download is running

        try
        {
            if (State.Status == UpdateStatus.ReadyToRestart) return false;
            SetState(State with { Status = UpdateStatus.Checking, Error = null });

            var update = await _updater.CheckAsync(ct);
            var now = DateTimeOffset.Now;
            if (update == null)
            {
                _available = null;
                Logger.Information("[update] up to date ({Version})", CurrentVersion);
                SetState(new UpdateState(UpdateStatus.UpToDate, LastChecked: now));
                return false;
            }

            bool isNew = _available?.Version != update.Version;
            _available = update;
            if (isNew)
                Logger.Information("[update] {AppName} {New} is available (running {Current})",
                    AppInfo.DisplayName, update.Version, CurrentVersion);
            SetState(new UpdateState(UpdateStatus.Available, update.Version, LastChecked: now));
            return true;
        }
        catch (Exception ex)
        {
            Fail("check for updates", ex);
            return false;
        }
        finally
        {
            _operation.Release();
        }
    }

    /// <summary>
    /// Download the available update. True when it is ready to apply (state
    /// <see cref="UpdateStatus.ReadyToRestart"/>). False if there is nothing to download, a
    /// check/download is already running, or it failed (state <see cref="UpdateStatus.Error"/>; call again to retry).
    /// </summary>
    public async Task<bool> DownloadAsync(CancellationToken ct = default)
    {
        if (State.Status == UpdateStatus.ReadyToRestart) return true;
        if (!await _operation.WaitAsync(0, CancellationToken.None)) return false;

        try
        {
            var update = _available;
            if (update == null || State.Status == UpdateStatus.Disabled) return false;

            Logger.Information("[update] downloading {Version}", update.Version);
            SetState(new UpdateState(UpdateStatus.Downloading, update.Version, 0, LastChecked: State.LastChecked));

            int lastProgress = 0;
            await _updater.DownloadAsync(update, p =>
            {
                p = Math.Clamp(p, 0, 100);
                if (Interlocked.Exchange(ref lastProgress, p) == p) return;
                SetState(State with { Status = UpdateStatus.Downloading, Progress = p });
            }, ct);

            Logger.Information("[update] {Version} downloaded; it applies when the app restarts", update.Version);
            SetState(new UpdateState(UpdateStatus.ReadyToRestart, update.Version, 100, LastChecked: State.LastChecked));
            return true;
        }
        catch (Exception ex)
        {
            Fail("download the update", ex);
            return false;
        }
        finally
        {
            _operation.Release();
        }
    }

    /// <summary>
    /// Apply the downloaded update once the app exits, and relaunch it. Returns true if the updater
    /// is armed: the caller must then shut the app down normally (e.g. the desktop lifetime's
    /// Shutdown()), so hosted servers and Dolphin attachments are cleaned up first. False when
    /// nothing is downloaded or arming failed (state <see cref="UpdateStatus.Error"/>).
    /// </summary>
    public bool ApplyOnExit()
    {
        if (State.Status != UpdateStatus.ReadyToRestart) return false;
        try
        {
            Logger.Information("[update] restarting to apply {Version}", State.Version);
            _updater.ApplyOnExit(_available);
            return true;
        }
        catch (Exception ex)
        {
            Fail("apply the update", ex);
            return false;
        }
    }

    /// <summary>
    /// Exit NOW, apply the downloaded update and relaunch (skips normal shutdown; prefer
    /// <see cref="ApplyOnExit"/>). Only returns if it failed (false, state <see cref="UpdateStatus.Error"/>).
    /// </summary>
    public bool ApplyAndRestart()
    {
        if (State.Status != UpdateStatus.ReadyToRestart) return false;
        try
        {
            Logger.Information("[update] exiting to apply {Version}", State.Version);
            _updater.ApplyAndRestart(_available);
            return true;
        }
        catch (Exception ex)
        {
            Fail("apply the update", ex);
            return false;
        }
    }

    private void Fail(string what, Exception ex)
    {
        if (ex is OperationCanceledException && _cts.IsCancellationRequested) return; // shutting down
        Logger.Warning("[update] could not {What}: {Reason}", what, ex.Message);
        SetState(State with { Status = UpdateStatus.Error, Error = $"Could not {what}: {ex.Message}", Progress = 0 });
    }

    private void SetState(UpdateState state)
    {
        lock (_stateLock)
        {
            if (_state == state) return;
            _state = state;
        }
        try { StateChanged?.Invoke(state); }
        catch (Exception ex) { Logger.Error(ex, "[update] a StateChanged handler failed"); }
    }

    private bool SafeIsInstalled()
    {
        try { return _updater.IsInstalled; }
        catch (Exception ex) { Logger.Warning(ex, "[update] could not tell whether the app is installed; updates are off"); return false; }
    }

    private string? SafeCurrentVersion()
    {
        try { return _updater.CurrentVersion; }
        catch { return null; }
    }

    private string? SafePendingRestartVersion()
    {
        try { return _updater.PendingRestartVersion; }
        catch (Exception ex) { Logger.Warning(ex, "[update] could not read the pending update"); return null; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _timer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
        // _operation is left undisposed on purpose: a running check may still release it.
    }
}
