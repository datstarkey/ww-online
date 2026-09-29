using Serilog;

namespace WWOnline.Services;

/// <summary>
/// Whether to show the first-run setup (SetupWizardViewModel) when the app starts.
/// <list type="bullet">
/// <item><see cref="GameSettings.SetupCompleted"/>: finished or skipped before, so no.</item>
/// <item>A settings file from before the setup existed that already holds a working setup (the paths
/// check out and the patched folder has a patched game): marked completed and saved, so no. If that
/// game is out of date the app's "patch your game" warning says so; the player isn't sent through
/// setup again.</item>
/// <item>Started by a script (--auto-connect, --host-server, --auto-attach: dev-test.ps1): no, and
/// nothing is saved.</item>
/// <item>Anything else (a new install, or settings that never got as far as a patched game): yes.</item>
/// </list>
/// </summary>
public static class FirstRun
{
    private static readonly ILogger Logger = Log.ForContext(typeof(FirstRun));

    public enum Decision
    {
        /// <summary>Show the setup.</summary>
        ShowSetup,
        /// <summary>Setup was finished or skipped before.</summary>
        AlreadyCompleted,
        /// <summary>Existing settings with a patched game: marked completed now.</summary>
        MigratedAsCompleted,
        /// <summary>A scripted start (dev-test.ps1): no setup, nothing saved.</summary>
        Scripted,
    }

    /// <summary>Decide, and save the migration when it applies.</summary>
    public static Decision Resolve(GameSettingsService settingsService, StartupOptions options)
    {
        var settings = settingsService.Load();
        if (settings.SetupCompleted) return Decision.AlreadyCompleted;

        if (IsWorkingSetup(settingsService, settings))
        {
            settings.SetupCompleted = true;
            settingsService.Save(settings);
            Logger.Information("[setup] existing settings with a patched game: first-run setup marked as done");
            return Decision.MigratedAsCompleted;
        }

        if (options.AutoConnect || options.HostServer || options.AutoAttach)
            return Decision.Scripted;

        return Decision.ShowSetup;
    }

    /// <summary>Paths all set and valid, and the patched folder holds a patched game.</summary>
    public static bool IsWorkingSetup(GameSettingsService settingsService, GameSettings settings) =>
        settingsService.IsConfigured(settings) &&
        File.Exists(Path.Combine(settings.GamePath, "sys", "main.dol"));

    /// <summary>Record that the setup was finished or skipped.</summary>
    public static void MarkCompleted(GameSettingsService settingsService)
    {
        var settings = settingsService.Load();
        if (settings.SetupCompleted) return;
        settings.SetupCompleted = true;
        settingsService.Save(settings);
    }
}
