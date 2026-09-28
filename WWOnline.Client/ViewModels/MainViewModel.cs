using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WWOnline.Services;
using WWOnline.Shared;

namespace WWOnline.ViewModels;

public enum ViewName
{
    Dashboard,
    Appearance,
    /// <summary>The Dolphin page (attach + live game state; GameStateViewModel).</summary>
    LiveState,
    Activity,
    Tools,
    Settings
}

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly GameSettingsService _gameSettingsService;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly AppearanceViewModel _appearanceViewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly ServerViewModel _serverViewModel;
    private readonly GameStateViewModel _gameStateViewModel;
    private readonly GameLogViewModel _gameLogViewModel;
    private readonly DebugToolsViewModel _debugToolsViewModel;
    private bool _disposed;

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPatchBanner))]
    private bool _settingsConfigured;

    [ObservableProperty]
    private string _title = AppInfo.DisplayName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboardActive))]
    [NotifyPropertyChangedFor(nameof(IsAppearanceActive))]
    [NotifyPropertyChangedFor(nameof(IsLiveStateActive))]
    [NotifyPropertyChangedFor(nameof(IsActivityActive))]
    [NotifyPropertyChangedFor(nameof(IsToolsActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    [NotifyPropertyChangedFor(nameof(ShowPatchBanner))]
    private ViewName _currentViewName = ViewName.Dashboard;

    public bool IsDashboardActive => CurrentViewName == ViewName.Dashboard;
    public bool IsAppearanceActive => CurrentViewName == ViewName.Appearance;
    public bool IsLiveStateActive => CurrentViewName == ViewName.LiveState;
    public bool IsActivityActive => CurrentViewName == ViewName.Activity;
    public bool IsToolsActive => CurrentViewName == ViewName.Tools;
    public bool IsSettingsActive => CurrentViewName == ViewName.Settings;

    /// <summary>Exposed so the sidebar can bind the local player card (name, tunic, Dolphin link).</summary>
    public ServerViewModel Server => _serverViewModel;

    /// <summary>Exposed so the sidebar's player card can show the owner ring.</summary>
    public DashboardViewModel Room => _dashboardViewModel;

    /// <summary>Exposed for the sidebar's update banner.</summary>
    public UpdateViewModel Updates { get; }

    /// <summary>The patched game's build status (the Settings page's check), for the sidebar's patch banner.</summary>
    public PatchOptionsViewModel PatchOptions => _settingsViewModel.PatchOptions;

    /// <summary>
    /// A gentle "patch the game again" prompt in the sidebar: the patched game doesn't match what this
    /// app would patch in (e.g. an update shipped new game code). Never shown on the Settings page,
    /// which has its own banner, and the app never patches by itself.
    /// </summary>
    public bool ShowPatchBanner => SettingsConfigured && PatchOptions.NeedsRepatch && !PatchOptions.IsCheckingBuild && !IsSettingsActive;

    public MainViewModel(
        GameSettingsService gameSettingsService,
        SettingsViewModel settingsViewModel,
        AppearanceViewModel appearanceViewModel,
        DashboardViewModel dashboardViewModel,
        ServerViewModel serverViewModel,
        GameStateViewModel gameStateViewModel,
        GameLogViewModel gameLogViewModel,
        DebugToolsViewModel debugToolsViewModel,
        UpdateViewModel updateViewModel)
    {
        _gameSettingsService = gameSettingsService;
        _settingsViewModel = settingsViewModel;
        _appearanceViewModel = appearanceViewModel;
        Updates = updateViewModel;
        _dashboardViewModel = dashboardViewModel;
        _serverViewModel = serverViewModel;
        _gameStateViewModel = gameStateViewModel;
        _gameLogViewModel = gameLogViewModel;
        _debugToolsViewModel = debugToolsViewModel;

        // Check if settings are configured, show appropriate view
        var settings = _gameSettingsService.Load();
        _settingsConfigured = _gameSettingsService.IsConfigured(settings);
        _currentView = _settingsConfigured ? _dashboardViewModel : _settingsViewModel;
        _currentViewName = _settingsConfigured ? ViewName.Dashboard : ViewName.Settings;

        // Listen for settings saved
        _settingsViewModel.SettingsSaved += OnSettingsSaved;

        // Navigation requested from the Room page
        _dashboardViewModel.NavigateRequested += OnDashboardNavigateRequested;

        // Local avatar follows the tunic colour picked on the Appearance page
        _appearanceViewModel.PropertyChanged += OnAppearancePropertyChanged;

        PatchOptions.PropertyChanged += OnPatchOptionsPropertyChanged;
        _serverViewModel.SetLocalTunicColor(_appearanceViewModel.SelectedColor.HexDisplay);
    }

    private void OnAppearancePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppearanceViewModel.SelectedColor))
            _serverViewModel.SetLocalTunicColor(_appearanceViewModel.SelectedColor.HexDisplay);
    }

    private void OnPatchOptionsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PatchOptionsViewModel.NeedsRepatch) or nameof(PatchOptionsViewModel.IsCheckingBuild))
            OnPropertyChanged(nameof(ShowPatchBanner));
    }

    private void OnDashboardNavigateRequested(ViewName target)
    {
        switch (target)
        {
            case ViewName.Appearance: ShowAppearance(); break;
            case ViewName.LiveState: ShowLiveState(); break;
            case ViewName.Activity: ShowActivity(); break;
            case ViewName.Tools: ShowTools(); break;
            case ViewName.Settings: ShowSettings(); break;
            default: ShowDashboard(); break;
        }
    }

    private void OnSettingsSaved()
    {
        var settings = _gameSettingsService.Load();
        SettingsConfigured = _gameSettingsService.IsConfigured(settings);
        if (SettingsConfigured)
        {
            CurrentView = _dashboardViewModel;
            CurrentViewName = ViewName.Dashboard;
        }
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        _dashboardViewModel.ShowRoom();
        CurrentView = _dashboardViewModel;
        CurrentViewName = ViewName.Dashboard;
    }

    [RelayCommand]
    private void ShowAppearance()
    {
        CurrentView = _appearanceViewModel;
        CurrentViewName = ViewName.Appearance;
    }

    [RelayCommand]
    private void ShowLiveState()
    {
        CurrentView = _gameStateViewModel;
        CurrentViewName = ViewName.LiveState;
    }

    [RelayCommand]
    private void ShowActivity()
    {
        CurrentView = _gameLogViewModel;
        CurrentViewName = ViewName.Activity;
    }

    [RelayCommand]
    private void ShowTools()
    {
        CurrentView = _debugToolsViewModel;
        CurrentViewName = ViewName.Tools;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        CurrentView = _settingsViewModel;
        CurrentViewName = ViewName.Settings;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settingsViewModel.SettingsSaved -= OnSettingsSaved;
        _dashboardViewModel.NavigateRequested -= OnDashboardNavigateRequested;
        _appearanceViewModel.PropertyChanged -= OnAppearancePropertyChanged;
        PatchOptions.PropertyChanged -= OnPatchOptionsPropertyChanged;
    }
}
