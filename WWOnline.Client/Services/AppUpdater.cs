using Serilog;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
using WWOnline.Shared;

namespace WWOnline.Services;

/// <summary>A release newer than the running app. <see cref="Handle"/> is the updater's own object.</summary>
public sealed record AvailableUpdate(string Version, object? Handle = null);

/// <summary>
/// The update operations <see cref="UpdateService"/> needs, so it can be tested without Velopack
/// (<see cref="VelopackAppUpdater"/> is the real one).
/// </summary>
public interface IAppUpdater
{
    /// <summary>False when the app wasn't installed by Velopack (dev runs from bin/, dev-test.ps1): updates are off.</summary>
    bool IsInstalled { get; }

    /// <summary>The installed version, or null when not installed.</summary>
    string? CurrentVersion { get; }

    /// <summary>A newer release, or null if up to date.</summary>
    Task<AvailableUpdate?> CheckAsync(CancellationToken ct);

    /// <summary>Download (and verify) the update; <paramref name="progress"/> gets 0-100.</summary>
    Task DownloadAsync(AvailableUpdate update, Action<int> progress, CancellationToken ct);

    /// <summary>The version already downloaded and waiting for a restart, if any.</summary>
    string? PendingRestartVersion { get; }

    /// <summary>
    /// Hand the downloaded update to the updater process, which waits for this app to exit, applies
    /// it and relaunches the app. The caller then shuts the app down normally (so hosted servers etc.
    /// are cleaned up).
    /// </summary>
    void ApplyOnExit(AvailableUpdate? update);

    /// <summary>Exit immediately, apply the update and relaunch (no normal shutdown).</summary>
    void ApplyAndRestart(AvailableUpdate? update);
}

/// <summary>Where and how <see cref="UpdateService"/> checks.</summary>
public sealed record UpdateServiceOptions
{
    /// <summary>First check this long after startup, so it never competes with app start-up.</summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>Then check again this often.</summary>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromHours(4);

    /// <summary>Also offer GitHub pre-releases.</summary>
    public bool AllowPrerelease { get; init; }

    /// <summary>Download a found update in the background, so the UI only has to offer "restart to update".</summary>
    public bool AutoDownload { get; init; } = true;

    /// <summary>
    /// A GitHub repo URL (releases), an http(s) URL of a Velopack release feed, or a local folder of
    /// <c>vpk pack</c> output (for testing the updater locally; see docs/releasing.md).
    /// </summary>
    public string Source { get; init; } = AppInfo.RepositoryUrl;

    /// <summary>
    /// Defaults, with overrides from the environment:
    /// WWO_UPDATE_SOURCE (repo URL / feed URL / local folder), WWO_UPDATE_PRERELEASE=1.
    /// A pre-release build follows pre-releases by default.
    /// </summary>
    public static UpdateServiceOptions FromEnvironment()
    {
        var source = Environment.GetEnvironmentVariable("WWO_UPDATE_SOURCE");
        var pre = Environment.GetEnvironmentVariable("WWO_UPDATE_PRERELEASE");
        return new UpdateServiceOptions
        {
            Source = string.IsNullOrWhiteSpace(source) ? AppInfo.RepositoryUrl : source.Trim(),
            AllowPrerelease = AppInfo.IsPrerelease || pre is "1" || string.Equals(pre, "true", StringComparison.OrdinalIgnoreCase),
        };
    }
}

/// <summary>Velopack-backed <see cref="IAppUpdater"/> (GitHub Releases by default).</summary>
public sealed class VelopackAppUpdater : IAppUpdater
{
    private static readonly ILogger Logger = Log.ForContext<VelopackAppUpdater>();
    private readonly UpdateManager? _manager;

    public VelopackAppUpdater(UpdateServiceOptions options)
    {
        try
        {
            _manager = new UpdateManager(CreateSource(options));
        }
        catch (Exception ex)
        {
            // Never let the updater take the app down: no manager = updates off.
            Logger.Warning(ex, "[update] could not create the update manager; updates are off");
        }
    }

    internal static IUpdateSource CreateSource(UpdateServiceOptions options)
    {
        var source = options.Source;
        if (Directory.Exists(source))
            return new SimpleFileSource(new DirectoryInfo(source));
        if (source.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            return new GithubSource(source.TrimEnd('/'), accessToken: null, prerelease: options.AllowPrerelease);
        return new SimpleWebSource(source);
    }

    /// <summary>True when this process runs from a Velopack install (not dev bin/). Safe to call any time after VelopackApp.Run().</summary>
    public static bool IsRunningInstalled
    {
        get
        {
            try { return VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion != null; }
            catch { return false; }
        }
    }

    public bool IsInstalled => _manager?.IsInstalled == true;

    public string? CurrentVersion => _manager?.CurrentVersion?.ToString();

    public async Task<AvailableUpdate?> CheckAsync(CancellationToken ct)
    {
        var manager = RequireManager();
        ct.ThrowIfCancellationRequested();
        var info = await manager.CheckForUpdatesAsync().WaitAsync(ct);
        return info == null ? null : new AvailableUpdate(info.TargetFullRelease.Version.ToString(), info);
    }

    public Task DownloadAsync(AvailableUpdate update, Action<int> progress, CancellationToken ct) =>
        RequireManager().DownloadUpdatesAsync(AsInfo(update), progress, ct);

    public string? PendingRestartVersion => _manager?.UpdatePendingRestart?.Version.ToString();

    public void ApplyOnExit(AvailableUpdate? update)
    {
        var manager = RequireManager();
        manager.WaitExitThenApplyUpdates(TargetAsset(manager, update), silent: false, restart: true, restartArgs: []);
    }

    public void ApplyAndRestart(AvailableUpdate? update)
    {
        var manager = RequireManager();
        manager.ApplyUpdatesAndRestart(TargetAsset(manager, update));
    }

    private UpdateManager RequireManager() =>
        _manager is { IsInstalled: true } m ? m : throw new InvalidOperationException("Not installed via Velopack; updates are off.");

    private static UpdateInfo AsInfo(AvailableUpdate update) =>
        update.Handle as UpdateInfo ?? throw new ArgumentException("Not a Velopack update.", nameof(update));

    private static VelopackAsset? TargetAsset(UpdateManager manager, AvailableUpdate? update) =>
        (update?.Handle as UpdateInfo)?.TargetFullRelease ?? manager.UpdatePendingRestart;
}
