using System.Diagnostics;
using Serilog;

namespace WWOnline.Services;

public class GameLaunchService
{
    private static readonly ILogger Logger = Log.ForContext<GameLaunchService>();
    private readonly GameSettingsService _gameSettingsService;
    private Process? _dolphinProcess;

    public GameLaunchService(GameSettingsService gameSettingsService)
    {
        _gameSettingsService = gameSettingsService;
    }

    public bool IsRunning =>
        _dolphinProcess is { HasExited: false } ||
        Process.GetProcessesByName("Dolphin").Length > 0;

    public Task<bool> LaunchGameAsync()
    {
        var settings = _gameSettingsService.Load();

        if (string.IsNullOrWhiteSpace(settings.DolphinPath) || string.IsNullOrWhiteSpace(settings.GamePath))
        {
            Logger.Warning("Cannot launch game: paths not configured");
            return Task.FromResult(false);
        }

        var mainDol = Path.Combine(settings.GamePath, "sys", "main.dol");
        if (!File.Exists(mainDol))
        {
            Logger.Warning("Cannot launch game: main.dol not found at {Path}", mainDol);
            return Task.FromResult(false);
        }

        try
        {
            Logger.Information("Launching Dolphin: {Dolphin} --exec={Game}", settings.DolphinPath, mainDol);

            _dolphinProcess = Process.Start(new ProcessStartInfo
            {
                FileName = settings.DolphinPath,
                Arguments = $"--exec=\"{mainDol}\"",
                UseShellExecute = false
            });

            if (_dolphinProcess != null)
            {
                Logger.Information("Dolphin launched with PID {PID}", _dolphinProcess.Id);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to launch Dolphin");
        }

        return Task.FromResult(false);
    }
}
