using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WWOnline.Hubs;
using WWOnline.Services;
using WWOnline.ViewModels;
using WWOnline.Views;

namespace WWOnline;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Remove Avalonia data validators to avoid duplicates with CommunityToolkit
        BindingPlugins.DataValidators.RemoveAt(0);

        // Serilog is configured in Program.Main (per-instance log file via --log-file).
        Log.Information("Starting WW-Online application");

        var services = new ServiceCollection();

        // Services
        services.AddSingleton(Program.StartupOptions);
        services.AddSingleton<WindowSettingsService>();
        services.AddSingleton<SignalRClientService>();
        services.AddSingleton<IGameStateService, GameStateService>();
        services.AddSingleton<IDolphinService, DolphinService>();
        services.AddSingleton<GameMemoryMonitorService>();
        services.AddSingleton<DespawnWorker>();
        services.AddSingleton<PuppetSyncService>();
        services.AddSingleton<RoomInventorySyncService>();
        services.AddSingleton<WorldFlagSyncService>();
        services.AddSingleton<SharedWalletService>();
        services.AddSingleton<StorySyncService>();
        services.AddSingleton<RoomSettingsService>();
        services.AddSingleton<GameSyncService>();
        services.AddSingleton<GameLogService>();
        services.AddSingleton<GameSettingsService>();
        services.AddSingleton<OptionalPatchCatalogService>();
        services.AddSingleton<GamePatcherService>();
        services.AddSingleton<GameLaunchService>();
        services.AddSingleton(UpdateServiceOptions.FromEnvironment());
        services.AddSingleton<IAppUpdater, VelopackAppUpdater>();
        services.AddSingleton<UpdateService>();

        // ViewModels (singleton — held by MainViewModel for lifetime of app)
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ServerViewModel>();
        services.AddSingleton<GameStateViewModel>();
        services.AddSingleton<GameLogViewModel>();
        services.AddSingleton<DebugToolsViewModel>();
        services.AddSingleton<RoomItemsViewModel>();
        services.AddSingleton<RoomFlagsViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<PatchOptionsViewModel>();
        services.AddSingleton<AppearanceViewModel>();
        services.AddSingleton<UpdateViewModel>();
        services.AddSingleton<SetupWizardViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Background update checks (a no-op for dev builds that Velopack didn't install).
        _serviceProvider.GetRequiredService<UpdateService>().Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var windowSettings = _serviceProvider.GetRequiredService<WindowSettingsService>().Load();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>(),
                Width = windowSettings.Width,
                Height = windowSettings.Height,
            };

            if (windowSettings.X != 0 || windowSettings.Y != 0)
            {
                desktop.MainWindow.Position = new Avalonia.PixelPoint(windowSettings.X, windowSettings.Y);
            }
            else
            {
                desktop.MainWindow.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
            }

            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();

        // Fire startup-driven auto-actions on a background thread so the UI finishes
        // initialising first. --auto-connect / --auto-attach flags make the dev-test
        // script a one-click setup.
        _ = System.Threading.Tasks.Task.Run(() => RunStartupActions(_serviceProvider!));
    }

    private static async System.Threading.Tasks.Task RunStartupActions(IServiceProvider services)
    {
        try
        {
            // Loudly flag a game build that doesn't match the C sources — the #1 cause of
            // "puppets don't work" (C# and in-game code disagreeing about the memory layout).
            var build = HeadlessCommands.TryCheckBuild();
            if (build != null)
            {
                if (build.Status == Patcher.Pipeline.BuildStamp.Status.Fresh)
                    Log.Information("[build-check] {Result}", build.Describe());
                else
                    Log.Error("[build-check] {Result}", build.Describe());
            }

            var opts = services.GetRequiredService<Services.StartupOptions>();
            var serverVm = services.GetRequiredService<ViewModels.ServerViewModel>();

            // Populate name/host/port from CLI before connecting so the form reflects
            // the actual target and the "connect" button's handler uses the right values.
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(opts.PlayerName))
                    serverVm.PlayerName = opts.PlayerName!;
                if (!string.IsNullOrEmpty(opts.Host))
                    serverVm.ServerHost = opts.Host!;
                if (opts.Port is int p)
                    serverVm.ServerPort = p.ToString();
            });

            // Give the SignalR service a beat to finish initial DI wiring first.
            if (opts.HostServer)
            {
                Log.Information("[startup] --host-server: hosting a server and joining it...");
                await System.Threading.Tasks.Task.Delay(300);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    serverVm.HostCommand.ExecuteAsync(null));
            }
            else if (opts.AutoConnect)
            {
                // Retry: under dev-test.ps1 the host client may still be starting its server.
                Log.Information("[startup] --auto-connect: connecting to server...");
                await System.Threading.Tasks.Task.Delay(300);
                for (int attempt = 1; attempt <= 30 && !serverVm.IsConnected; attempt++)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        serverVm.ConnectCommand.ExecuteAsync(null));
                    if (serverVm.IsConnected) break;
                    Log.Information("[startup] --auto-connect: attempt {Attempt} failed, retrying...", attempt);
                    await System.Threading.Tasks.Task.Delay(1000);
                }
            }

            if (opts.AutoAttach)
            {
                // Never onto a stale or unpatched game (the C# and in-game code would disagree about
                // the memory layout) unless dev-test.ps1 -AllowStale passed --allow-stale.
                var readiness = LaunchReadiness.EvaluateAutoAttach(build, opts.AllowStale);
                if (!readiness.CanLaunch)
                {
                    Log.Error("[startup] --auto-attach: not attaching: {Reason}. Patch the game (or pass --allow-stale).", readiness.Problem);
                }
                else
                {
                    if (build?.Status != Patcher.Pipeline.BuildStamp.Status.Fresh)
                        Log.Warning("[startup] --auto-attach: the game build isn't up to date; attaching anyway (--allow-stale)");

                    // Dolphin's window appears well before the game has booted and mapped its emulated
                    // memory, so keep trying until the attach actually succeeds: up to ~3 minutes.
                    Log.Information("[startup] --auto-attach: waiting for Dolphin{Pid} to boot the game...",
                        opts.DolphinPid is int p ? $" (PID {p})" : "");
                    var launch = services.GetRequiredService<GameLaunchService>();
                    if (await launch.WaitAndAttachAsync(opts.DolphinPid))
                        Log.Information("[startup] --auto-attach: attached");
                    else
                        Log.Warning("[startup] --auto-attach: gave up; attach from the Dolphin page");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during startup auto-actions");
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        Log.Information("Application closing, cleaning up resources");

        if (_serviceProvider == null) return;

        try
        {
            // Save window settings
            if (sender is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is { } window)
            {
                var settingsService = _serviceProvider.GetRequiredService<WindowSettingsService>();
                settingsService.Save(new WindowSettings
                {
                    X = window.Position.X,
                    Y = window.Position.Y,
                    Width = (int)window.Width,
                    Height = (int)window.Height,
                    IsMaximized = window.WindowState == Avalonia.Controls.WindowState.Maximized
                });
            }

            // Stop services
            var gameSyncService = _serviceProvider.GetRequiredService<GameSyncService>();
            gameSyncService.Stop();
            gameSyncService.Dispose();

            var memoryMonitor = _serviceProvider.GetRequiredService<GameMemoryMonitorService>();
            memoryMonitor.Stop();
            memoryMonitor.Dispose();

            var gameLog = _serviceProvider.GetRequiredService<GameLogService>();
            gameLog.Dispose();

            var signalRClient = _serviceProvider.GetRequiredService<SignalRClientService>();
            signalRClient.DisposeAsync().AsTask().GetAwaiter().GetResult();

            _serviceProvider.GetRequiredService<MainViewModel>().Dispose();
            _serviceProvider.GetRequiredService<RoomItemsViewModel>().Dispose();
            _serviceProvider.GetRequiredService<RoomFlagsViewModel>().Dispose();
            _serviceProvider.GetRequiredService<PatchOptionsViewModel>().Dispose();
            _serviceProvider.GetRequiredService<UpdateViewModel>().Dispose();
            _serviceProvider.GetRequiredService<UpdateService>().Dispose();
            _serviceProvider.GetRequiredService<SetupWizardViewModel>().Dispose();
            _serviceProvider.GetRequiredService<GameLaunchService>().Dispose();

            // Stops the hosted server process if this client was hosting.
            _serviceProvider.GetRequiredService<ServerViewModel>().Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during shutdown cleanup");
        }
        finally
        {
            Log.Information("Application shutdown complete");
        }
    }
}
