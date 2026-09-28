using System.Timers;
using Serilog;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

public class GameMemoryMonitorService : IDisposable
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<GameMemoryMonitorService>();
    private readonly IDolphinService _dolphinService;
    private readonly IGameStateService _gameStateService;
    private readonly GameEventDetector _eventDetector;
    private System.Timers.Timer? _timer;
    private bool _isReading = false;
    private readonly object _readLock = new();
    private GameState? _previousState = null;
    
    public bool IsMonitoring { get; private set; }
    
    public GameMemoryMonitorService(IDolphinService dolphinService, IGameStateService gameStateService)
    {
        _dolphinService = dolphinService;
        _gameStateService = gameStateService;
        _eventDetector = new GameEventDetector();
        
        // Subscribe to game events
        RegisterEventHandlers();
    }
    
    private void RegisterEventHandlers()
    {
        _eventDetector.OnRupeeCollected += (prev, curr, change) =>
        {
            Logger.Information("🪙 EVENT: Rupee collection detected! {Prev} -> {Curr} (+{Change})", prev, curr, change);
            // TODO: Sync this event in multiplayer
            return Task.CompletedTask;
        };

        _eventDetector.OnHealthChanged += (prev, curr, change) =>
        {
            if (change < 0)
                Logger.Information("💔 EVENT: Damage taken! Health: {Prev} -> {Curr} ({Change})", prev, curr, change);
            else
                Logger.Information("❤️ EVENT: Health restored! Health: {Prev} -> {Curr} (+{Change})", prev, curr, change);
            return Task.CompletedTask;
        };

        _eventDetector.OnItemCollected += (itemType, amount) =>
        {
            Logger.Information("📦 EVENT: Collected {Amount} {Item}", amount, itemType);
            return Task.CompletedTask;
        };

        _eventDetector.OnItemUsed += (itemType, prev, curr) =>
        {
            Logger.Debug("Used {Item}: {Prev} -> {Curr}", itemType, prev, curr);
            return Task.CompletedTask;
        };
    }
    
    public void Start()
    {
        if (IsMonitoring)
        {
            Logger.Information("Memory monitor already running");
            return;
        }
        
        
        if (!_dolphinService.IsConnected)
        {
            Logger.Warning("Cannot start memory monitor - not connected to Dolphin");
            return;
        }
        
        Logger.Information("Starting game memory monitor (20Hz refresh rate)");
        
        _timer = new System.Timers.Timer(50); // 20Hz (50ms interval)
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
        
        IsMonitoring = true;
    }
    
    public void Stop()
    {
        if (!IsMonitoring)
        {
            return;
        }
        
        Logger.Information("Stopping game memory monitor");
        
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        
        IsMonitoring = false;
    }
    
    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // Prevent overlapping reads
        lock (_readLock)
        {
            if (_isReading)
            {
                return;
            }
            _isReading = true;
        }

        try
        {
            if (!_dolphinService.IsConnected)
            {
                Logger.Warning("Dolphin disconnected, stopping monitor");
                Stop();
                return;
            }

            // Read the current game state
            var gameState = _dolphinService.ReadGameStateAsync().GetAwaiter().GetResult();

            if (gameState != null)
            {
                // Detect events by comparing with previous state
                if (_previousState != null)
                {
                    _eventDetector.DetectEvents(gameState, _previousState).GetAwaiter().GetResult();
                }

                // Update the local state in the game state service
                _gameStateService.UpdateLocalState(gameState);

                // Store for next comparison
                _previousState = gameState;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error reading game memory");
        }
        finally
        {
            lock (_readLock)
            {
                _isReading = false;
            }
        }
    }
    
    public void Dispose()
    {
        Stop();
        _timer?.Dispose();
    }
}