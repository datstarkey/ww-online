using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Data;
using WWOnline.Services;

namespace WWOnline.ViewModels;

public partial class GameStateViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<GameStateViewModel>();

    private readonly IGameStateService _gameStateService;
    private readonly IDolphinService _dolphinService;
    private readonly GameMemoryMonitorService _memoryMonitor;
    private readonly GameSyncService _gameSyncService;
    private bool _disposed;

    [ObservableProperty]
    private int _currentHealth;

    [ObservableProperty]
    private int _maxHealth;

    [ObservableProperty]
    private int _rupees;

    [ObservableProperty]
    private int _currentMagic;

    [ObservableProperty]
    private int _maxMagic;

    [ObservableProperty]
    private int _currentSword;

    [ObservableProperty]
    private int _currentShield;

    [ObservableProperty]
    private float _positionX;

    [ObservableProperty]
    private float _positionY;

    [ObservableProperty]
    private float _positionZ;

    [ObservableProperty]
    private int _currentArrows;

    [ObservableProperty]
    private int _currentBombs;

    [ObservableProperty]
    private int _sector;

    [ObservableProperty]
    private string _stageName = "";

    [ObservableProperty]
    private string _swordName = "None";

    [ObservableProperty]
    private string _shieldName = "None";

    [ObservableProperty]
    private bool _hasData;

    // === Heart rendering ===
    // WW health is stored in quarter-hearts (4 = one heart). The row shows a full red heart
    // per completed heart and a slate heart for the rest; the precise half is shown in HealthText.
    private static readonly IBrush HeartFullBrush = new SolidColorBrush(Color.Parse("#FF6B6B"));
    private static readonly IBrush HeartEmptyBrush = new SolidColorBrush(Color.Parse("#2A3A46"));

    public ObservableCollection<IBrush> Hearts { get; } = new();

    public string HealthText
    {
        get
        {
            int full = CurrentHealth / 4;
            bool half = (CurrentHealth % 4) >= 2;
            int maxHearts = (MaxHealth + 3) / 4; // round up so a partial heart container still counts
            return $"{full}{(half ? "½" : "")} / {maxHearts}";
        }
    }

    private void RebuildHearts()
    {
        int maxHearts = Math.Max(0, (MaxHealth + 3) / 4);
        int full = CurrentHealth / 4;
        Hearts.Clear();
        for (int i = 0; i < maxHearts; i++)
            Hearts.Add(i < full ? HeartFullBrush : HeartEmptyBrush);
        OnPropertyChanged(nameof(HealthText));
    }

    // Keep Hearts/HealthText in sync no matter who changes health (live sync or Debug stat editor).
    partial void OnCurrentHealthChanged(int value) => RebuildHearts();
    partial void OnMaxHealthChanged(int value) => RebuildHearts();

    // === Dolphin picker state ===

    public ObservableCollection<DolphinProcessInfo> AvailableDolphins { get; } = new();

    [ObservableProperty]
    private DolphinProcessInfo? _selectedDolphin;

    [ObservableProperty]
    private bool _isDolphinConnected;

    [ObservableProperty]
    private int? _connectedProcessId;

    [ObservableProperty]
    private string? _dolphinError;

    [ObservableProperty]
    private bool _isBusy;

    public string DolphinStatusText => IsDolphinConnected
        ? $"Attached to PID {ConnectedProcessId}"
        : "Not attached";

    partial void OnIsDolphinConnectedChanged(bool value) => OnPropertyChanged(nameof(DolphinStatusText));
    partial void OnConnectedProcessIdChanged(int? value) => OnPropertyChanged(nameof(DolphinStatusText));

    public GameStateViewModel(
        IGameStateService gameStateService,
        IDolphinService dolphinService,
        GameMemoryMonitorService memoryMonitor,
        GameSyncService gameSyncService)
    {
        _gameStateService = gameStateService;
        _dolphinService = dolphinService;
        _memoryMonitor = memoryMonitor;
        _gameSyncService = gameSyncService;

        _gameStateService.StateChanged += OnStateChanged;
        _dolphinService.ConnectionChanged += OnDolphinConnectionChanged;

        SyncConnectionState();
        RefreshDolphins();
    }

    private void OnDolphinConnectionChanged(object? sender, bool isConnected)
    {
        Dispatcher.UIThread.Post(SyncConnectionState);
    }

    private void SyncConnectionState()
    {
        IsDolphinConnected = _dolphinService.IsConnected;
        ConnectedProcessId = _dolphinService.ConnectedProcessId;
        if (!IsDolphinConnected) HasData = false;
    }

    [RelayCommand]
    private void RefreshDolphins()
    {
        var current = SelectedDolphin?.ProcessId;
        AvailableDolphins.Clear();
        foreach (var p in _dolphinService.EnumerateProcesses())
            AvailableDolphins.Add(p);

        // Preserve selection if still present, otherwise prefer the currently-connected PID, else first entry.
        DolphinProcessInfo? restore = null;
        if (current is int pid)
            restore = AvailableDolphins.FirstOrDefault(d => d.ProcessId == pid);
        if (restore == null && ConnectedProcessId is int attachedPid)
            restore = AvailableDolphins.FirstOrDefault(d => d.ProcessId == attachedPid);
        SelectedDolphin = restore ?? AvailableDolphins.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ConnectDolphin()
    {
        if (SelectedDolphin is null)
        {
            DolphinError = "Pick a Dolphin process first.";
            return;
        }

        DolphinError = null;
        IsBusy = true;
        try
        {
            var ok = await _dolphinService.ConnectAsync(SelectedDolphin.ProcessId);
            if (ok)
            {
                _memoryMonitor.Start();
                _gameSyncService.Start();
                Logger.Information("GameState attached to Dolphin PID {Pid}", SelectedDolphin.ProcessId);
            }
            else
            {
                DolphinError = $"Failed to attach to PID {SelectedDolphin.ProcessId}. Is the game booted?";
            }
            SyncConnectionState();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error attaching to Dolphin");
            DolphinError = $"Attach failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void DisconnectDolphin()
    {
        DolphinError = null;
        try
        {
            _gameSyncService.Stop();
            _memoryMonitor.Stop();
            _dolphinService.Disconnect();
            Logger.Information("GameState detached from Dolphin");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error detaching from Dolphin");
            DolphinError = $"Detach failed: {ex.Message}";
        }
        finally
        {
            SyncConnectionState();
        }
    }

    private void OnStateChanged(object? sender, GameStateChangedEventArgs e)
    {
        // Read stage name on background thread before dispatching
        string? rawStageName = null;
        try
        {
            rawStageName = _dolphinService.ReadString(GameMemoryAddresses.Stage.CurrentStageName);
        }
        catch { /* Dolphin read may fail */ }

        // StateChanged fires from background thread, dispatch to UI
        Dispatcher.UIThread.Post(() =>
        {
            HasData = true;
            var p = e.NewState.Player;
            CurrentHealth = p.CurrentHealth;
            MaxHealth = p.MaxHealth;
            Rupees = p.RupeeCount;
            CurrentMagic = p.CurrentMagic;
            MaxMagic = p.MaxMagic;
            CurrentSword = p.CurrentSword;
            CurrentShield = p.CurrentShield;
            PositionX = p.PositionX;
            PositionY = p.PositionY;
            PositionZ = p.PositionZ;
            CurrentArrows = p.CurrentArrows;
            CurrentBombs = p.CurrentBombs;
            Sector = p.CurrentSector;

            SwordName = ItemIDs.GetItemName((byte)p.CurrentSword);
            ShieldName = ItemIDs.GetItemName((byte)p.CurrentShield);

            if (rawStageName != null)
                StageName = StageIDs.GetStageName(rawStageName.TrimEnd('\0'));
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gameStateService.StateChanged -= OnStateChanged;
        _dolphinService.ConnectionChanged -= OnDolphinConnectionChanged;
    }
}
