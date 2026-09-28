namespace WWOnline.Services;

/// <summary>
/// Where the client keeps files, for dev runs and for a Velopack install. An installed app's own
/// folder (%LocalAppData%\WWOnline\current) is replaced on every update, so nothing that must
/// survive an update (logs, settings) may live next to the exe; settings also live outside the
/// install root so they survive an uninstall/reinstall.
/// </summary>
public static class AppPaths
{
    /// <summary>Name of the per-user folders (settings under %AppData%, logs under %LocalAppData%).
    /// It is also the Velopack packId, so an installed app lives in %LocalAppData%\WWOnline.</summary>
    public const string AppDataFolderName = "WWOnline";

    /// <summary>%LocalAppData%: Velopack's install root (%LocalAppData%\WWOnline) and the legacy data folder.</summary>
    public static string LocalAppDataRoot { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// Per-user settings folder: %AppData%\WWOnline (roaming). Deliberately NOT under
    /// %LocalAppData%\WWOnline: that is Velopack's install root, which an uninstall removes, and the
    /// player's settings (paths, look, patch choices) should survive a reinstall. Settings from before
    /// the WW-Online rename are copied in by <see cref="LegacyAppDataMigration"/>.
    /// </summary>
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);

    /// <summary>Where an installed app keeps its logs: %LocalAppData%\WWOnline\logs (next to, not
    /// inside, Velopack's replaced-on-update <c>current</c> folder; removed on uninstall, which is fine for logs).</summary>
    public static string InstalledLogsDirectory { get; } = Path.Combine(LocalAppDataRoot, AppDataFolderName, "logs");

    /// <summary>
    /// Log folder: <c>logs/</c> under the working directory for dev builds (unchanged behaviour),
    /// <see cref="InstalledLogsDirectory"/> for an installed app. Only valid after
    /// VelopackApp.Run() (Program.Main) has set up the install locator.
    /// </summary>
    public static string LogsDirectory => VelopackAppUpdater.IsRunningInstalled
        ? InstalledLogsDirectory
        : Path.GetFullPath("logs");

    /// <summary>
    /// The server the Host button starts: the copy bundled with an installed/published app
    /// (<c>server/WWOnline.Server.exe</c> next to the client, see release.yml), else null
    /// (dev layout: callers fall back to WWOnline.Server/bin/...).
    /// </summary>
    public static string? BundledServerExe
    {
        get
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "server", "WWOnline.Server.exe");
            return File.Exists(exe) ? exe : null;
        }
    }
}
