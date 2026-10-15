using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared;

namespace WWOnline.ViewModels;

/// <summary>The Settings page's tabs: the app's own, then the native game's (<see cref="SettingsViewModel.Game"/>).</summary>
public enum SettingsTab
{
    GameFiles,
    /// <summary>The optional game patches (Dolphin only).</summary>
    Patches,
    General,
    /// <summary>One of the native game's tabs (<see cref="GameSettingsViewModel.SelectedSection"/>).</summary>
    Game
}

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly GameSettingsService _gameSettingsService;
    private readonly GamePatcherService _gamePatcherService;
    private readonly ItemIconService _iconService;

    [ObservableProperty]
    private string _dolphinPath = "";

    [ObservableProperty]
    private string _gamePath = "";

    [ObservableProperty]
    private string _vanillaGamePath = "";

    [ObservableProperty]
    private bool _autoLaunchDolphin = true;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private bool _isSaved;

    [ObservableProperty]
    private bool _isPatching;

    [ObservableProperty]
    private string _patchProgress = "";

    [ObservableProperty]
    private bool _isPatched;

    [ObservableProperty]
    private string? _patchError;

    /// <summary>The optional game patch checklist (Settings → Game patches).</summary>
    public PatchOptionsViewModel PatchOptions { get; }

    /// <summary>App version and "check for updates" (Settings → About).</summary>
    public UpdateViewModel Updates { get; }

    /// <summary>The native backend's disc and build (shown while <see cref="IsNative"/>).</summary>
    public NativeSetupViewModel? Native { get; }

    /// <summary>The native game's own options, as further tabs (native backend only).</summary>
    public GameSettingsViewModel? Game { get; }

    public bool HasGame => Game != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameFilesTab), nameof(IsPatchesTab), nameof(IsGeneralTab), nameof(IsGameTab))]
    private SettingsTab _tab;

    public bool IsGameFilesTab => Tab == SettingsTab.GameFiles;
    public bool IsPatchesTab => Tab == SettingsTab.Patches;
    public bool IsGeneralTab => Tab == SettingsTab.General;
    public bool IsGameTab => Tab == SettingsTab.Game;

    /// <summary>The backend this run uses: a change applies after a restart.</summary>
    private readonly GameBackend _runningBackend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNative), nameof(IsDolphin), nameof(BackendRestartNeeded))]
    private GameBackend _backend;

    /// <summary>The embedded native game (radio button; <see cref="IsDolphin"/> is the other one).</summary>
    public bool IsNative
    {
        get => Backend == GameBackend.Native;
        set { if (value) Backend = GameBackend.Native; }
    }

    public bool IsDolphin
    {
        get => Backend == GameBackend.Dolphin;
        set { if (value) Backend = GameBackend.Dolphin; }
    }

    public bool BackendRestartNeeded => Backend != _runningBackend;

    public event Action? SettingsSaved;

    /// <summary>"Run setup again": MainViewModel opens the first-run setup.</summary>
    public event Action? SetupRequested;

    /// <summary>Both game folders are filled in, so Patch game can run.</summary>
    public bool HasGamePaths => !string.IsNullOrWhiteSpace(VanillaGamePath) && !string.IsNullOrWhiteSpace(GamePath);

    /// <summary>
    /// Set by the View to provide file/folder browse capability.
    /// </summary>
    public Func<Task<string?>>? BrowseForFileAsync { get; set; }
    public Func<Task<string?>>? BrowseForFolderAsync { get; set; }
    public Func<Task<string?>>? BrowseForVanillaFolderAsync { get; set; }

    /// <summary>The game's item icons (Settings → Item icons shows a few and their status).</summary>
    public ItemIcons Icons { get; }

    /// <summary>Where the item icons stand ("116 item icons, read from your game files", or why not).</summary>
    [ObservableProperty]
    private string _iconStatusText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExtractIconsCommand))]
    private bool _isExtractingIcons;

    /// <summary>A few icons shown beside the status once they exist (empty until then).</summary>
    public IReadOnlyList<IImage> IconPreview => PreviewItems
        .Select(Icons.Get).OfType<IImage>().ToList();

    // Telescope, Wind Waker, Hero's Bow, Hookshot, Compass, Boss Key
    private static readonly byte[] PreviewItems = [0x20, 0x22, 0x27, 0x2F, 0x4D, 0x4E];

    public SettingsViewModel(GameSettingsService gameSettingsService, GamePatcherService gamePatcherService,
        PatchOptionsViewModel patchOptions, UpdateViewModel updates, ItemIconService iconService, ItemIcons icons,
        NativeSetupViewModel? native = null, GameSettingsViewModel? game = null)
    {
        Native = native;
        Game = game;
        _runningBackend = gameSettingsService.Load().Backend;
        _gameSettingsService = gameSettingsService;
        _gamePatcherService = gamePatcherService;
        _iconService = iconService;
        PatchOptions = patchOptions;
        Updates = updates;
        Icons = icons;

        ReloadFromSettings();
        _gamePatcherService.IsPatchingChanged += OnPatcherBusyChanged;
        _iconService.Changed += OnIconServiceChanged;
        Icons.Refreshed += OnIconsRefreshed;
        OnIconServiceChanged();
    }

    private void OnIconServiceChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        IconStatusText = _iconService.IsExtracting ? "Reading the item icons from your game files..." : _iconService.StatusText;
        IsExtractingIcons = _iconService.IsExtracting;
    });

    private void OnIconsRefreshed() => OnPropertyChanged(nameof(IconPreview));

    /// <summary>Read the icons again from the vanilla game folder (else the patched one) in Settings.</summary>
    [RelayCommand(CanExecute = nameof(CanExtractIcons))]
    private async Task ExtractIcons()
    {
        // The folders on screen, saved or not: vanilla first, as at startup.
        string? folder = new[] { VanillaGamePath, GamePath }
            .FirstOrDefault(f => Patcher.Icons.ItemIconExtractor.FindArchive(f) != null);
        await _iconService.ExtractAsync(folder);
    }

    private bool CanExtractIcons() => !IsExtractingIcons;

    [RelayCommand]
    private void ShowTab(SettingsTab tab)
    {
        if (tab == SettingsTab.Game && Game == null) return;
        Tab = tab;
    }

    /// <summary>Shows one of the game's tabs.</summary>
    [RelayCommand]
    private void ShowGameSection(GameSettingsSection section)
    {
        if (Game == null) return;
        Game.SelectedSection = section;
        Tab = SettingsTab.Game;
    }

    partial void OnTabChanged(SettingsTab value)
    {
        if (Game != null) Game.IsShown = value == SettingsTab.Game;
    }

    // The patches tab goes with Dolphin.
    partial void OnBackendChanged(GameBackend value)
    {
        if (value != GameBackend.Dolphin && Tab == SettingsTab.Patches) Tab = SettingsTab.GameFiles;
    }

    /// <summary>Show the saved paths again (after the setup wizard changed them).</summary>
    public void ReloadFromSettings()
    {
        var settings = _gameSettingsService.Load();
        DolphinPath = settings.DolphinPath;
        GamePath = settings.GamePath;
        VanillaGamePath = settings.VanillaGamePath;
        AutoLaunchDolphin = settings.AutoLaunchDolphin;
        Backend = settings.Backend;
        Native?.Load();
        ValidationError = null;
        IsSaved = false;
    }

    /// <summary>A patch is running anywhere (this page, the banner or the setup): no second one, no setup.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunSetupCommand), nameof(PatchGameCommand))]
    private bool _isAnyPatchRunning;

    [RelayCommand(CanExecute = nameof(CanRunSetup))]
    private void RunSetup() => SetupRequested?.Invoke();

    private bool CanRunSetup() => !IsAnyPatchRunning;

    private void OnPatcherBusyChanged(bool busy) => Avalonia.Threading.Dispatcher.UIThread.Post(() => IsAnyPatchRunning = busy);

    [RelayCommand]
    private async Task BrowseDolphinPath()
    {
        if (BrowseForFileAsync != null)
        {
            var path = await BrowseForFileAsync();
            if (path != null)
                DolphinPath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseGamePath()
    {
        if (BrowseForFolderAsync != null)
        {
            var path = await BrowseForFolderAsync();
            if (path != null)
                GamePath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseVanillaGamePath()
    {
        if (BrowseForVanillaFolderAsync != null)
        {
            var path = await BrowseForVanillaFolderAsync();
            if (path != null)
                VanillaGamePath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPatchGame))]
    private async Task PatchGame()
    {
        IsPatching = true;
        IsPatched = false;
        PatchError = null;
        PatchProgress = "";

        var progress = new Progress<string>(msg => PatchProgress = msg);

        var error = await _gamePatcherService.PatchGameAsync(VanillaGamePath, GamePath, progress);

        IsPatching = false;

        if (error != null)
        {
            PatchError = error;
        }
        else
        {
            IsPatched = true;
        }
    }

    private bool CanPatchGame() => !IsPatching && !IsAnyPatchRunning
        && !string.IsNullOrWhiteSpace(VanillaGamePath)
        && !string.IsNullOrWhiteSpace(GamePath);

    partial void OnIsPatchingChanged(bool value) => PatchGameCommand.NotifyCanExecuteChanged();
    partial void OnVanillaGamePathChanged(string value)
    {
        PatchGameCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasGamePaths));
    }

    partial void OnGamePathChanged(string value)
    {
        PatchGameCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasGamePaths));
        // The build status beside Patch game follows the folder being edited, saved or not.
        PatchOptions.SetGamePath(value);
    }

    [RelayCommand]
    private void SaveSettings()
    {
        ValidationError = null;
        IsSaved = false;

        // Appearance and the optional patch selection save themselves; this saves the paths only.
        var settings = _gameSettingsService.Load();
        settings.DolphinPath = DolphinPath;
        settings.GamePath = GamePath;
        settings.VanillaGamePath = VanillaGamePath;
        settings.AutoLaunchDolphin = AutoLaunchDolphin;
        settings.Backend = Backend;

        var error = _gameSettingsService.ValidatePaths(settings);
        if (error != null)
        {
            ValidationError = error;
            return;
        }

        _gameSettingsService.Save(settings);
        IsSaved = true;
        SettingsSaved?.Invoke();
    }

    /// <summary>Where the last bug report went, or why it couldn't be made (Settings → Bug report).</summary>
    [ObservableProperty]
    private string _bugReportStatus = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveBugReportCommand))]
    private bool _isSavingBugReport;

    private bool CanSaveBugReport() => !IsSavingBugReport;

    /// <summary>Zip the recent logs and a summary for an issue (<see cref="BugReport"/>), then show it in Explorer.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveBugReport))]
    private async Task SaveBugReport()
    {
        IsSavingBugReport = true;
        BugReportStatus = "Saving...";
        try
        {
            var settings = _gameSettingsService.Load();
            var buildCheck = PatchOptions.BuildStatusText is { Length: > 0 } s ? s : "not checked";
            var logFile = Program.StartupOptions.LogFile;
            var path = await Task.Run(() =>
            {
                var now = DateTime.Now;
                var files = BugReport.CollectFiles(AppPaths.LogsDirectory, logFile, settings.DolphinPath, now);
                var about = BugReport.About(settings, buildCheck, now);
                return BugReport.Write(BugReport.ReportsDirectory, files, about,
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), now);
            });
            Serilog.Log.Information("[bug-report] saved {Path}", path);
            BugReportStatus = $"Saved {Path.GetFileName(path)}. Attach it to your issue on GitHub.";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "[bug-report] couldn't open Explorer"); }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[bug-report] couldn't save a bug report");
            BugReportStatus = $"Couldn't save a bug report: {ex.Message}";
        }
        finally
        {
            IsSavingBugReport = false;
        }
    }

    /// <summary>Open the GitHub issues page (Settings → Bug report).</summary>
    [RelayCommand]
    private void OpenIssues()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppInfo.RepositoryUrl + "/issues/new") { UseShellExecute = true });
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "[bug-report] couldn't open the issues page"); }
    }

    public void Dispose()
    {
        _gamePatcherService.IsPatchingChanged -= OnPatcherBusyChanged;
        _iconService.Changed -= OnIconServiceChanged;
        Icons.Refreshed -= OnIconsRefreshed;
    }
}
