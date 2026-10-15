using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WWOnline.Hubs;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.ViewModels;
using WWOnline.Views;

namespace WWOnline;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private PosixSignalRegistration? _sigterm;

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
        var backend = new GameSettingsService().Load().Backend;
        services.AddSingleton(_ => new SignalRClientService
        {
            Platform = NativePaths.PlatformName(backend),
            Capabilities = backend == GameBackend.Native ? Shared.Models.ClientCapabilities.PvpHits : Shared.Models.ClientCapabilities.None,
        });
        services.AddSingleton<IGameStateService, GameStateService>();
        AddGameBackend(services, backend);
        services.AddSingleton<GameMemoryMonitorService>();
        services.AddSingleton<DespawnWorker>();
        services.AddSingleton<LiveWorldPoke>();
        services.AddSingleton<SwitchTableProvider>();
        services.AddSingleton<RoomSwitchSyncService>();
        services.AddSingleton<PuppetSyncService>();
        services.AddSingleton<RoomInventorySyncService>();
        services.AddSingleton<WorldFlagSyncService>();
        services.AddSingleton<SharedWalletService>();
        services.AddSingleton<SharedBaitService>();
        services.AddSingleton<SharedSpoilsService>();
        services.AddSingleton<SharedDeliveryService>();
        services.AddSingleton<StorySyncService>();
        services.AddSingleton<SmallKeyTableProvider>();
        services.AddSingleton<SharedSmallKeyService>();
        services.AddSingleton<HeartTableProvider>();
        services.AddSingleton<DerivedHeartsState>();
        services.AddSingleton<IMaxHealthOwner>(sp => sp.GetRequiredService<DerivedHeartsState>());
        services.AddSingleton<SharedHeartService>();
        services.AddSingleton<IHeartsSource>(sp => sp.GetRequiredService<SharedHeartService>());
        services.AddSingleton<PlayerEventService>();
        services.AddSingleton<PvpService>();
        services.AddSingleton<RoomSettingsService>();
        services.AddSingleton<WarpService>();
        services.AddSingleton<GameSyncService>();
        services.AddSingleton<GameLogService>();
        services.AddSingleton<GameSettingsService>();
        services.AddSingleton<OptionalPatchCatalogService>();
        services.AddSingleton<GamePatcherService>();
        services.AddSingleton<GameLaunchService>();
        // Item icons decoded from the player's own game files into a local cache (never shipped).
        services.AddSingleton(sp => new ItemIconService(
            sp.GetRequiredService<GameSettingsService>(), sp.GetRequiredService<GamePatcherService>()));
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
        services.AddSingleton(sp => new NativeSetupViewModel(sp.GetRequiredService<GameSettingsService>()));
        services.AddSingleton<SetupWizardViewModel>();
        services.AddSingleton(sp => new ItemIcons(sp.GetRequiredService<ItemIconService>()));

        _serviceProvider = services.BuildServiceProvider();

        // Background update checks (a no-op for dev builds that Velopack didn't install).
        _serviceProvider.GetRequiredService<UpdateService>().Start();

        // Item icons: read them from the game folders in Settings if the cache has none yet (never throws).
        _ = _serviceProvider.GetRequiredService<ItemIconService>().EnsureExtractedAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Program.StartupOptions.Hidden)
            {
                RunWithoutWindow(_serviceProvider);
            }
            else
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
            }

            // Closing the main window quits, even while the game is popped out into its own window.
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();

        // Fire startup-driven auto-actions on a background thread so the UI finishes
        // initialising first. --auto-connect / --auto-attach flags make the dev-test
        // script a one-click setup.
        _ = System.Threading.Tasks.Task.Run(() => RunStartupActions(_serviceProvider!));

        if (Program.StartupOptions is { TestShots: not null } or { TestRupees: not null } or { TestWarp: not null }
            && _serviceProvider.GetService<GameHost>() is { } host)
            _ = System.Threading.Tasks.Task.Run(() => NativeTestRun.RunAsync(Program.StartupOptions, host,
                _serviceProvider.GetRequiredService<IDolphinService>(), _serviceProvider.GetRequiredService<SharedWalletService>(),
                _serviceProvider.GetRequiredService<DashboardViewModel>()));

        // scripts/dev-test-native.sh --stop sends SIGTERM: shut down as closing the window does (a hosted server stops too).
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            _sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => lifetime.TryShutdown());
            });
    }

    /// <summary>
    /// --hidden: no window at all, so nothing can show on the desktop. The view models and services run as usual, and
    /// the embedded game renders into its view outside any window, as tww_smoke runs it.
    /// </summary>
    private static void RunWithoutWindow(IServiceProvider services)
    {
        services.GetRequiredService<MainViewModel>();
        if (services.GetService<GameHost>() is not { } host) return;
        host.Started += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                host.CreateView();
                TwwNative.tww_set_size(1240, 760, 2f);
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex, "[native] no game view");
            }
        });
    }

    /// <summary>
    /// The game backend, chosen once per run (<see cref="GameSettings.Backend"/>): Dolphin's memory, or the embedded
    /// native game behind the same <see cref="IDolphinService"/> (<see cref="NativeGameService"/>). Only the native
    /// backend registers <see cref="GameHost"/> / <see cref="INativeGame"/>, which the services that take them
    /// optionally use for their native path.
    /// </summary>
    public static void AddGameBackend(IServiceCollection services, GameBackend backend)
    {
        Log.Information("Game backend: {Backend}", backend);
        if (backend == GameBackend.Native)
        {
            services.AddSingleton<GameHost>();
            services.AddSingleton<INativeGame>(sp => sp.GetRequiredService<GameHost>());
            services.AddSingleton<IDolphinService, NativeGameService>();
            services.AddSingleton<IWorldGame, NativeWorldGame>();
            services.AddSingleton(sp => new GameSettingsViewModel(new NativeGameSettings(sp.GetRequiredService<INativeGame>())));
        }
        else
        {
            services.AddSingleton<IDolphinService, DolphinService>();
        }
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
        _sigterm?.Dispose();

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
            // Off the UI thread: its awaits resume on the UI context, which this wait blocks.
            Task.Run(() => signalRClient.DisposeAsync().AsTask()).GetAwaiter().GetResult();

            _serviceProvider.GetRequiredService<MainViewModel>().Dispose();
            _serviceProvider.GetRequiredService<RoomItemsViewModel>().Dispose();
            _serviceProvider.GetRequiredService<RoomFlagsViewModel>().Dispose();
            _serviceProvider.GetRequiredService<PatchOptionsViewModel>().Dispose();
            _serviceProvider.GetRequiredService<UpdateViewModel>().Dispose();
            _serviceProvider.GetRequiredService<UpdateService>().Dispose();
            _serviceProvider.GetRequiredService<SetupWizardViewModel>().Dispose();
            _serviceProvider.GetRequiredService<SettingsViewModel>().Dispose();
            _serviceProvider.GetRequiredService<GameLaunchService>().Dispose();
            _serviceProvider.GetRequiredService<ItemIcons>().Dispose();
            _serviceProvider.GetRequiredService<ItemIconService>().Dispose();

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
            // libtww's static destructors assume a game that never ends: once it has loaded, leave without them.
            if (_serviceProvider.GetService<GameHost>() is { IsStarted: true } game)
            {
                game.Stop(TimeSpan.FromSeconds(2));
                Log.CloseAndFlush();
                TwwNative.ExitProcess(0);
            }
        }
    }
}
