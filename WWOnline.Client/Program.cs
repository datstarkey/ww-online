using Avalonia;
using Serilog;
using Velopack;
using WWOnline.Services;
using WWOnline.Shared;

namespace WWOnline;

internal class Program
{
    /// <summary>
    /// Parsed CLI options. Populated in Main() before Avalonia boots so
    /// App.OnFrameworkInitializationCompleted can register them in DI.
    /// </summary>
    public static StartupOptions StartupOptions { get; private set; } = new(null, null, null, false, false, null);

    private const string LogTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    [STAThread]
    public static int Main(string[] args)
    {
        // Parse is side-effect free, so it may run before Velopack.
        StartupOptions = StartupOptions.Parse(args);

        // Velopack's startup hook must run before anything else: on install/update/uninstall the
        // updater launches this exe with hook arguments, and Run() handles them and exits. It also
        // applies an update downloaded last session (and relaunches). Headless modes (--check-build,
        // --patch, --build-patchdata, used by scripts and CI) skip it so they never restart or update mid-run. When the app
        // isn't a Velopack install (dev runs from bin/, dev-test.ps1) it does nothing.
        if (!StartupOptions.IsHeadless)
            VelopackApp.Build().Run();

        ConfigureLogging(StartupOptions);
        Log.Information("{AppName} {Version} ({Commit}) starting", AppInfo.DisplayName, AppInfo.Version, AppInfo.Commit ?? "no commit");
        Log.Information("Startup options: {Options}", StartupOptions);

        // Before anything reads settings: bring them over from the pre-rename folder, once.
        // (--build-patchdata is a build tool: it reads no settings.)
        if (StartupOptions.BuildPatchData == null)
            LegacyAppDataMigration.Run();

        try
        {
            if (StartupOptions.IsHeadless)
                return HeadlessCommands.Run(StartupOptions);

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// One log file per client instance. With --log-file (used by dev-test.ps1) the log goes to
    /// exactly that path; otherwise to a daily-rolling file under <see cref="AppPaths.LogsDirectory"/>
    /// (logs/ for dev builds; %LocalAppData% for an installed app, whose install folder is replaced
    /// on every update). shared:true lets several instances without --log-file coexist without the
    /// second one silently rolling to _001.
    /// </summary>
    private static void ConfigureLogging(StartupOptions opts)
    {
        var config = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        config = string.IsNullOrWhiteSpace(opts.LogFile)
            ? config.WriteTo.File(Path.Combine(AppPaths.LogsDirectory, "ww-online-.txt"), rollingInterval: RollingInterval.Day,
                shared: true, outputTemplate: LogTemplate)
            : config.WriteTo.File(opts.LogFile, shared: true, outputTemplate: LogTemplate,
                flushToDiskInterval: TimeSpan.FromSeconds(1));

        Log.Logger = config.CreateLogger();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
