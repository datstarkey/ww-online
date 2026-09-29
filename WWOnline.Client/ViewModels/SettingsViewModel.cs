using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WWOnline.Services;

namespace WWOnline.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly GameSettingsService _gameSettingsService;
    private readonly GamePatcherService _gamePatcherService;

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

    public SettingsViewModel(GameSettingsService gameSettingsService, GamePatcherService gamePatcherService,
        PatchOptionsViewModel patchOptions, UpdateViewModel updates)
    {
        _gameSettingsService = gameSettingsService;
        _gamePatcherService = gamePatcherService;
        PatchOptions = patchOptions;
        Updates = updates;

        ReloadFromSettings();
    }

    /// <summary>Show the saved paths again (after the setup wizard changed them).</summary>
    public void ReloadFromSettings()
    {
        var settings = _gameSettingsService.Load();
        DolphinPath = settings.DolphinPath;
        GamePath = settings.GamePath;
        VanillaGamePath = settings.VanillaGamePath;
        AutoLaunchDolphin = settings.AutoLaunchDolphin;
        ValidationError = null;
        IsSaved = false;
    }

    [RelayCommand]
    private void RunSetup() => SetupRequested?.Invoke();

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

    private bool CanPatchGame() => !IsPatching
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
}
