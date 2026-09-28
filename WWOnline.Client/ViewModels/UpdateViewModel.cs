using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WWOnline.Services;
using WWOnline.Shared;

namespace WWOnline.ViewModels;

/// <summary>
/// Bindable view of <see cref="UpdateService"/> for an "update available / restart to update"
/// banner (MainWindow sidebar) and the Settings "About" row with "check for updates".
///
/// Banner: visible when <see cref="ShowBanner"/>; title from <see cref="AvailableVersion"/>;
/// buttons Download (<see cref="DownloadCommand"/>, when <see cref="IsUpdateAvailable"/>),
/// a progress bar (<see cref="DownloadProgress"/>, when <see cref="IsDownloading"/>) and
/// Restart (<see cref="RestartToUpdateCommand"/>, when <see cref="IsReadyToRestart"/>).
/// </summary>
public partial class UpdateViewModel : ViewModelBase, IDisposable
{
    private readonly UpdateService _updates;
    private readonly Action<Action> _post;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled), nameof(IsChecking), nameof(IsUpdateAvailable), nameof(IsDownloading),
        nameof(IsReadyToRestart), nameof(HasError), nameof(ShowBanner), nameof(StatusText))]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand), nameof(DownloadCommand), nameof(RestartToUpdateCommand))]
    private UpdateStatus _status;

    /// <summary>The available / downloading / downloaded version, e.g. "0.3.0".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _availableVersion;

    /// <summary>0-100 while <see cref="IsDownloading"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _downloadProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _errorMessage;

    [ObservableProperty]
    private DateTimeOffset? _lastChecked;

    public UpdateViewModel(UpdateService updates)
        : this(updates, action => Dispatcher.UIThread.Post(action))
    {
    }

    /// <param name="post">Runs an action on the UI thread (tests pass <c>a =&gt; a()</c>).</param>
    public UpdateViewModel(UpdateService updates, Action<Action> post)
    {
        _updates = updates;
        _post = post;
        // Subscribe first: a change raised in between is then posted after this snapshot, not lost.
        _updates.StateChanged += OnStateChanged;
        Apply(updates.State);
    }

    /// <summary>The running version, e.g. "0.2.1".</summary>
    public string CurrentVersion => _updates.CurrentVersion;

    /// <summary>"WW-Online 0.2.1" for an About/Settings line.</summary>
    public string CurrentVersionText => $"{AppInfo.DisplayName} {CurrentVersion}";

    /// <summary>False for dev builds (not installed via Velopack): hide update UI.</summary>
    public bool IsEnabled => Status != UpdateStatus.Disabled;
    public bool IsChecking => Status == UpdateStatus.Checking;
    public bool IsUpdateAvailable => Status == UpdateStatus.Available;
    public bool IsDownloading => Status == UpdateStatus.Downloading;
    public bool IsReadyToRestart => Status == UpdateStatus.ReadyToRestart;
    public bool HasError => Status == UpdateStatus.Error;

    /// <summary>Show the app-wide update banner (something for the player to act on or watch).</summary>
    public bool ShowBanner => Status is UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.ReadyToRestart;

    public string StatusText => Status switch
    {
        UpdateStatus.Disabled => "Updates are off (development build).",
        // Shown beside CurrentVersionText (Settings → About), so these don't repeat the version.
        UpdateStatus.Idle => "Not checked for updates yet.",
        UpdateStatus.Checking => "Checking for updates…",
        UpdateStatus.UpToDate => "Up to date.",
        UpdateStatus.Available => $"{AppInfo.DisplayName} {AvailableVersion} is available.",
        UpdateStatus.Downloading => $"Downloading {AppInfo.DisplayName} {AvailableVersion}… {DownloadProgress}%",
        UpdateStatus.ReadyToRestart => $"{AppInfo.DisplayName} {AvailableVersion} is ready. Restart to update.",
        UpdateStatus.Error => ErrorMessage ?? "Update failed.",
        _ => "",
    };

    private bool CanCheck() => Status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Available or UpdateStatus.Error;
    private bool CanDownload() => Status == UpdateStatus.Available || (Status == UpdateStatus.Error && AvailableVersion != null);
    private bool CanRestart() => Status == UpdateStatus.ReadyToRestart;

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private Task CheckForUpdates() => _updates.CheckForUpdatesAsync();

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private Task Download() => _updates.DownloadAsync();

    /// <summary>Arm the updater, then shut the app down normally; the updater relaunches it on the new version.</summary>
    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void RestartToUpdate()
    {
        if (!_updates.ApplyOnExit()) return;
        // TryShutdown, not Shutdown: only TryShutdown raises ShutdownRequested, where App stops the
        // hosted server and the sync services. Shutdown() is a forced exit that skips that cleanup,
        // leaving the server child process running while the updater replaces the install folder.
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!desktop.TryShutdown()) desktop.Shutdown();
        }
        else
            _updates.ApplyAndRestart();
    }

    private void OnStateChanged(UpdateState state) => _post(() => Apply(state));

    private void Apply(UpdateState state)
    {
        // Keep the last known version through Checking/Error so "Download" can be retried.
        if (state.Version != null) AvailableVersion = state.Version;
        else if (state.Status is UpdateStatus.UpToDate or UpdateStatus.Disabled or UpdateStatus.Idle) AvailableVersion = null;
        DownloadProgress = state.Progress;
        ErrorMessage = state.Error;
        LastChecked = state.LastChecked ?? LastChecked;
        Status = state.Status;
    }

    public void Dispose()
    {
        _updates.StateChanged -= OnStateChanged;
        GC.SuppressFinalize(this);
    }
}
