using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.ViewModels;

public enum ConnectionState
{
    Disconnected,
    Connected,
    Connecting,
    Hosting
}

public partial class ServerViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ServerViewModel>();
    private bool _disposed;

    private readonly SignalRClientService _signalRClient;
    private readonly IDolphinService _dolphinService;
    private readonly IGameStateService _gameStateService;
    private readonly GameMemoryMonitorService _memoryMonitor;
    private readonly GameSyncService _gameSyncService;
    private readonly GameSettingsService _gameSettingsService;
    private readonly GameLaunchService _gameLaunchService;
    private readonly PuppetSyncService _puppetSyncService;
    private Process? _serverProcess;
    private System.Timers.Timer? _diagnosticsTimer;

    [ObservableProperty]
    private string _playerName = "";

    [ObservableProperty]
    private string _serverHost = "localhost";

    [ObservableProperty]
    private string _serverPort = "6969";

    /// <summary>
    /// Optional, for joining a dedicated server that has an owner key (WWO_OWNER_KEY): entering it
    /// makes this player the room owner. Not saved to disk, and cleared when the address changes or
    /// the player leaves, so one server's key is never sent to another. (An automatic reconnect to
    /// the same server re-sends it: SignalRClientService keeps its own copy for that.)
    /// </summary>
    [ObservableProperty]
    private string _ownerKey = "";

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private bool _dolphinConnected;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsConnected => ConnectionState == ConnectionState.Connected || ConnectionState == ConnectionState.Hosting;
    public bool IsDisconnected => ConnectionState == ConnectionState.Disconnected;
    public bool IsConnecting => ConnectionState == ConnectionState.Connecting;
    public bool IsHosting => ConnectionState == ConnectionState.Hosting;

    public string ConnectionInfoText => ConnectionState switch
    {
        ConnectionState.Hosting => $"Hosting on port {ServerPort}",
        ConnectionState.Connected => $"Connected to {ServerAddress}",
        ConnectionState.Connecting => $"Connecting to {ServerAddress}...",
        _ => "Not connected"
    };

    /// <summary>"host:port", or the URL as typed when the host field holds a full URL (reverse proxy).</summary>
    public string ServerAddress => ServerHost.Contains("://", StringComparison.Ordinal) ? ServerHost.Trim() : $"{ServerHost}:{ServerPort}";

    public string DolphinStatusText => DolphinConnected ? "Connected" : "Not connected";

    /// <summary>Sidebar status line under the local player's name.</summary>
    public string DolphinLinkText => DolphinConnected ? "Dolphin linked" : "Dolphin not linked";

    /// <summary>The local player's tunic colour (Appearance page), for avatars.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocalAvatarForeground))]
    private IBrush _localTunicBrush = TunicColors.DefaultBrush;

    public IBrush LocalAvatarForeground => TunicColors.ForegroundFor(LocalTunicBrush);

    public string LocalInitials => TunicColors.InitialsOf(PlayerName);

    partial void OnPlayerNameChanged(string value) => OnPropertyChanged(nameof(LocalInitials));

    partial void OnServerHostChanged(string value) => OwnerKey = "";
    partial void OnServerPortChanged(string value) => OwnerKey = "";

    /// <summary>Called when the tunic colour is picked on the Appearance page; recolours the local avatar and row.</summary>
    public void SetLocalTunicColor(string hex)
    {
        LocalTunicBrush = TunicColors.FromHex(hex);
        foreach (var row in Players)
            if (row.IsLocal) row.TunicBrush = LocalTunicBrush;
    }

    public event Action? Connected;
    public event Action? Disconnected;

    partial void OnConnectionStateChanged(ConnectionState value)
    {
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDisconnected));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(IsHosting));
        OnPropertyChanged(nameof(ConnectionInfoText));
        if (value == ConnectionState.Connected || value == ConnectionState.Hosting)
            Connected?.Invoke();
        else if (value == ConnectionState.Disconnected)
            Disconnected?.Invoke();
    }

    partial void OnDolphinConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(DolphinStatusText));
        OnPropertyChanged(nameof(DolphinLinkText));
    }

    public ServerViewModel(
        SignalRClientService signalRClient,
        IDolphinService dolphinService,
        IGameStateService gameStateService,
        GameMemoryMonitorService memoryMonitor,
        GameSyncService gameSyncService,
        GameSettingsService gameSettingsService,
        GameLaunchService gameLaunchService,
        PuppetSyncService puppetSyncService)
    {
        _signalRClient = signalRClient;
        _dolphinService = dolphinService;
        _gameStateService = gameStateService;
        _memoryMonitor = memoryMonitor;
        _gameSyncService = gameSyncService;
        _gameSettingsService = gameSettingsService;
        _gameLaunchService = gameLaunchService;
        _puppetSyncService = puppetSyncService;

        _dolphinService.ConnectionChanged += OnDolphinConnectionChanged;
        _signalRClient.PlayerJoined += OnPlayerJoined;
        _signalRClient.PlayerLeft += OnPlayerLeft;
        _signalRClient.PlayerGameStateReceived += OnRemoteGameState;
        _signalRClient.PuppetDataReceived += OnPuppetDataReceivedCount;
        // Player rows are per connection: dropped when it's lost, rebuilt by the (re)connect's
        // PlayerJoined announcements plus our own row on Connected (our connection id changes).
        _signalRClient.ConnectionLost += ClearPlayers;
        _signalRClient.Connected += AddLocalPlayerRow;
        _signalRClient.JoinRejected += OnJoinRejected;
        _gameStateService.StateChanged += OnLocalGameState;

        _diagnosticsTimer = new System.Timers.Timer(500); // 2Hz UI refresh
        _diagnosticsTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(RefreshPuppetDiagnostics);
        _diagnosticsTimer.AutoReset = true;
        _diagnosticsTimer.Start();

        // Load saved player name
        var settings = _gameSettingsService.Load();
        _playerName = settings.PlayerName;
    }

    // === Puppet sync diagnostics ===

    public ObservableCollection<PuppetDiagnosticRow> PuppetDiagnostics { get; } = new();

    [ObservableProperty] private int _puppetMessagesReceived;
    [ObservableProperty] private string _spawnCountersText = "(awaiting patched Dolphin)";

    private void OnPuppetDataReceivedCount(WWOnline.Shared.Models.PuppetData data)
    {
        Interlocked.Increment(ref _puppetMessagesReceivedInterlocked);
        TrackAppearance(data);
    }

    // Peer tunic colours for the Players card avatars, from their puppet data (UI only).
    private readonly ConcurrentDictionary<string, uint> _peerTunicRgb = new();

    private void TrackAppearance(WWOnline.Shared.Models.PuppetData data)
    {
        if (data == null || string.IsNullOrEmpty(data.PlayerId) || data.Appearance == null) return;
        var a = data.Appearance;
        uint rgb = ((uint)a.ColorR << 16) | ((uint)a.ColorG << 8) | a.ColorB;
        if (_peerTunicRgb.TryGetValue(data.PlayerId, out var known) && known == rgb) return;
        _peerTunicRgb[data.PlayerId] = rgb;
        var id = data.PlayerId;
        Dispatcher.UIThread.Post(() =>
        {
            var row = FindRow(id);
            if (row != null && !row.IsLocal) row.TunicBrush = TunicColors.FromRgb(a.ColorR, a.ColorG, a.ColorB);
        });
    }

    private PlayerRowViewModel NewRow(string connectionId, string playerName, bool isLocal)
    {
        var row = new PlayerRowViewModel(connectionId, playerName, isLocal);
        if (isLocal)
            row.TunicBrush = LocalTunicBrush;
        else if (_peerTunicRgb.TryGetValue(connectionId, out var rgb))
            row.TunicBrush = TunicColors.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return row;
    }
    private int _puppetMessagesReceivedInterlocked;

    private void RefreshPuppetDiagnostics()
    {
        // Pull current counter
        PuppetMessagesReceived = _puppetMessagesReceivedInterlocked;

        var snap = _puppetSyncService.GetDiagnostics();
        // Simplest approach: replace contents in place so the UI just rebinds.
        PuppetDiagnostics.Clear();
        foreach (var row in snap)
            PuppetDiagnostics.Add(row);

        var counters = _puppetSyncService.ReadSpawnCounters();
        SpawnCountersText = counters?.Summary ?? "(Dolphin not attached)";
    }

    // === Connected players list ===

    public ObservableCollection<PlayerRowViewModel> Players { get; } = new();

    private void OnPlayerJoined(string connectionId, string playerName)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var row = FindRow(connectionId);
            if (row == null)
            {
                var selfId = _signalRClient.Connection?.ConnectionId;
                Players.Add(NewRow(connectionId, playerName, isLocal: connectionId == selfId));
            }
            else
            {
                row.PlayerName = playerName;
            }
        });
    }

    private void OnPlayerLeft(string connectionId)
    {
        _peerTunicRgb.TryRemove(connectionId, out _);
        Dispatcher.UIThread.Post(() =>
        {
            var row = FindRow(connectionId);
            if (row != null) Players.Remove(row);
        });
    }

    private void OnRemoteGameState(string senderId, GameState state)
    {
        Dispatcher.UIThread.Post(() => ApplyGameState(senderId, state));
    }

    private void OnLocalGameState(object? sender, GameStateChangedEventArgs e)
    {
        var selfId = _signalRClient.Connection?.ConnectionId;
        if (string.IsNullOrEmpty(selfId)) return;
        // GameStateService fires on background thread
        var snapshot = e.NewState;
        Dispatcher.UIThread.Post(() => ApplyGameState(selfId, snapshot));
    }

    private void ApplyGameState(string connectionId, GameState state)
    {
        var row = FindRow(connectionId);
        if (row == null)
        {
            // Row driven by data arrival — peer is "present" as long as we keep hearing from them.
            // PlayerJoined will fill in the friendly name if/when it arrives.
            var selfId = _signalRClient.Connection?.ConnectionId;
            var isLocal = connectionId == selfId;
            var fallbackName = isLocal
                ? (string.IsNullOrWhiteSpace(PlayerName) ? "You" : PlayerName)
                : $"Player({connectionId[..Math.Min(8, connectionId.Length)]})";
            row = NewRow(connectionId, fallbackName, isLocal);
            Players.Add(row);
        }
        var p = state.Player;
        row.HasGameState = true;
        row.CurrentHealth = p.CurrentHealth;
        row.MaxHealth = p.MaxHealth;
        row.Rupees = p.RupeeCount;
        row.Sector = p.CurrentSector;
        row.StageName = state.StageName;
        row.IsInGame = state.IsInGame;
        row.LastUpdate = DateTime.UtcNow;
    }

    private PlayerRowViewModel? FindRow(string connectionId)
    {
        for (int i = 0; i < Players.Count; i++)
            if (Players[i].ConnectionId == connectionId)
                return Players[i];
        return null;
    }

    private void AddLocalPlayerRow()
    {
        var selfId = _signalRClient.Connection?.ConnectionId;
        if (string.IsNullOrEmpty(selfId)) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (FindRow(selfId) == null)
                Players.Add(NewRow(selfId, PlayerName, isLocal: true));
        });
    }

    /// <summary>
    /// The room refused this client (different protocol). On the first connect Connect/Host show
    /// ConnectAsync's message; this also covers an automatic reconnect to a server that was updated
    /// meanwhile, which SignalRClientService drops without any command running.
    /// </summary>
    private void OnJoinRejected(string reason) => Dispatcher.UIThread.Post(() =>
    {
        ErrorMessage = reason;
        if (ConnectionState == ConnectionState.Connected) ConnectionState = ConnectionState.Disconnected;
    });

    private void ClearPlayers()
    {
        _peerTunicRgb.Clear();
        Dispatcher.UIThread.Post(Players.Clear);
    }

    private void OnDolphinConnectionChanged(object? sender, bool isConnected)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => DolphinConnected = isConnected);
    }

    private void SavePlayerName()
    {
        var settings = _gameSettingsService.Load();
        settings.PlayerName = PlayerName;
        _gameSettingsService.Save(settings);
    }

    [RelayCommand]
    private async Task Host()
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            if (!int.TryParse(ServerPort, out var port))
            {
                ErrorMessage = "Invalid port number";
                return;
            }

            SavePlayerName();
            ConnectionState = ConnectionState.Connecting;

            // Find the server: the copy bundled with an installed/published app (server/ next to
            // the client, see release.yml), else the dev build (repo/WWOnline.Server/bin/Debug/net9.0/).
            var solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var serverBin = Path.Combine(solutionDir, "WWOnline.Server", "bin", "Debug", "net9.0");
            var serverExe = AppPaths.BundledServerExe ?? Path.Combine(serverBin, "WWOnline.Server.exe");
            var serverDll = Path.ChangeExtension(serverExe, ".dll");

            if (AppPaths.BundledServerExe == null && !File.Exists(serverDll))
            {
                // Try building it first
                Logger.Information("Server DLL not found, attempting to build...");
                var buildProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{Path.Combine(solutionDir, "WWOnline.Server", "WWOnline.Server.csproj")}\" -c Debug",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (buildProcess != null)
                    await buildProcess.WaitForExitAsync();

                if (!File.Exists(serverDll))
                {
                    ErrorMessage = "Could not find or build the server. Build WWOnline.Server first.";
                    ConnectionState = ConnectionState.Disconnected;
                    return;
                }
            }

            // The hosted server logs to a file next to this client's log (logs/latest/server.log
            // under dev-test.ps1). Its console is NOT redirected: an unread redirected pipe fills
            // up after a few KB of log lines and blocks the server.
            var clientLog = Program.StartupOptions.LogFile;
            var serverLog = !string.IsNullOrWhiteSpace(clientLog)
                ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(clientLog)) ?? ".", "server.log")
                : Path.Combine(AppPaths.LogsDirectory, "server.log");
            // One-time token so THIS client becomes host even if another client connects first.
            var hostToken = Guid.NewGuid().ToString("N");
            var serverArgs = $"{port} --log-file \"{serverLog}\" --host-token {hostToken}";

            _serverProcess = new Process
            {
                StartInfo = File.Exists(serverExe)
                    ? new ProcessStartInfo { FileName = serverExe, Arguments = serverArgs }
                    : new ProcessStartInfo { FileName = "dotnet", Arguments = $"\"{serverDll}\" {serverArgs}" },
                EnableRaisingEvents = true
            };
            _serverProcess.StartInfo.UseShellExecute = false;
            _serverProcess.StartInfo.CreateNoWindow = true;

            _serverProcess.Exited += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (ConnectionState == ConnectionState.Hosting)
                    {
                        Logger.Warning("Server process exited unexpectedly");
                        _ = Disconnect();
                    }
                });
            };

            _serverProcess.Start();
            Logger.Information("Started server process (PID {Pid}) on port {Port}, log {Log}", _serverProcess.Id, port, serverLog);

            // Wait until it's actually listening (not a fixed delay)
            if (!await WaitForPortAsync(port, TimeSpan.FromSeconds(15)))
            {
                StopServerProcess();
                ConnectionState = ConnectionState.Disconnected;
                ErrorMessage = $"Server didn't start listening on port {port} — see {serverLog}";
                return;
            }

            // Connect to it and claim the room (re-claimed automatically after a reconnect)
            var (success, message) = await _signalRClient.ConnectAsync("localhost", port, PlayerName, hostToken);
            if (success)
            {
                ConnectionState = ConnectionState.Hosting;
                // Dolphin attach is manual via GameState picker — see GameStateViewModel.
            }
            else
            {
                StopServerProcess();
                ConnectionState = ConnectionState.Disconnected;
                ErrorMessage = $"Server started but failed to connect: {message}";
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to host server");
            StopServerProcess();
            ConnectionState = ConnectionState.Disconnected;
            ErrorMessage = $"Failed to host: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task<bool> WaitForPortAsync(int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                await tcp.ConnectAsync("127.0.0.1", port);
                return true;
            }
            catch (System.Net.Sockets.SocketException)
            {
                await Task.Delay(200);
            }
        }
        return false;
    }

    private void StopServerProcess()
    {
        if (_serverProcess is { HasExited: false })
        {
            try
            {
                _serverProcess.Kill(entireProcessTree: true);
                Logger.Information("Stopped server process");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error stopping server process");
            }
        }
        _serverProcess?.Dispose();
        _serverProcess = null;
    }

    [RelayCommand]
    private async Task Connect()
    {
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            // A URL in the host field (reverse proxy) carries its own port: the Port field is ignored.
            int port = 0;
            if (!SignalRClientService.IsUrl(ServerHost) && !int.TryParse(ServerPort, out port))
            {
                ErrorMessage = "Invalid port number";
                return;
            }

            SavePlayerName();
            ConnectionState = ConnectionState.Connecting;

            var (success, message) = await _signalRClient.ConnectAsync(ServerHost, port, PlayerName, OwnerKey);
            if (success)
            {
                ConnectionState = ConnectionState.Connected;
                // Dolphin attach is manual via GameState picker — see GameStateViewModel.
            }
            else
            {
                ConnectionState = ConnectionState.Disconnected;
                ErrorMessage = message;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to connect to server");
            ConnectionState = ConnectionState.Disconnected;
            ErrorMessage = $"Failed to connect: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        try
        {
            await _signalRClient.DisconnectAsync();
            StopServerProcess();
            OwnerKey = ""; // the next server gets no key unless the player enters one

            ConnectionState = ConnectionState.Disconnected;
            ClearPlayers();
            Logger.Information("Disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error during disconnect");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _dolphinService.ConnectionChanged -= OnDolphinConnectionChanged;
        _signalRClient.PlayerJoined -= OnPlayerJoined;
        _signalRClient.PlayerLeft -= OnPlayerLeft;
        _signalRClient.PlayerGameStateReceived -= OnRemoteGameState;
        _signalRClient.PuppetDataReceived -= OnPuppetDataReceivedCount;
        _signalRClient.ConnectionLost -= ClearPlayers;
        _signalRClient.Connected -= AddLocalPlayerRow;
        _signalRClient.JoinRejected -= OnJoinRejected;
        _gameStateService.StateChanged -= OnLocalGameState;
        _diagnosticsTimer?.Stop();
        _diagnosticsTimer?.Dispose();
        _diagnosticsTimer = null;
        StopServerProcess();
    }
}
