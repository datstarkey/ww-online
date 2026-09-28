using Serilog;

namespace WWOnline.Services;

/// <summary>
/// One-time copy of the player's data from the folder the app used before the WW-Online rename
/// (<c>%LocalAppData%\WindwakerOnline</c>): every <c>*.json</c> settings file into
/// <see cref="AppPaths.AppDataDirectory"/> (%AppData%\WWOnline), plus <c>logs\</c> into
/// <see cref="AppPaths.InstalledLogsDirectory"/>.
/// <list type="bullet">
/// <item>Copies, never moves or deletes: the old folder is left as it was.</item>
/// <item>Never overwrites a file that already exists in the new folder.</item>
/// <item>"Once" is recorded by <see cref="MarkerFileName"/> in the new settings folder, not by the
/// folder's existence. With the marker, a settings file the player deletes later is not copied back.</item>
/// <item>If any copy fails (e.g. a locked file), no marker is written, so the next start retries
/// the files still missing.</item>
/// </list>
/// Program.Main runs it after logging is set up and before anything reads settings.
/// </summary>
public static class LegacyAppDataMigration
{
    /// <summary>The %LocalAppData% folder name used before the rename.</summary>
    public const string LegacyFolderName = "WindwakerOnline";

    /// <summary>Written into the new folder once the copy has completed.</summary>
    public const string MarkerFileName = ".migrated-from-WindwakerOnline";

    private static readonly ILogger Logger = Log.ForContext(typeof(LegacyAppDataMigration));

    /// <param name="Ran">True when a copy was attempted (old folder present, no marker yet).</param>
    /// <param name="CopiedFiles">Paths (relative to the new folder) that were copied.</param>
    /// <param name="FailedFiles">Paths (relative to the old folder) that could not be copied.</param>
    public sealed record Result(bool Ran, IReadOnlyList<string> CopiedFiles, IReadOnlyList<string> FailedFiles)
    {
        public static Result Skipped { get; } = new(false, [], []);
    }

    /// <summary>Migrates %LocalAppData%\WindwakerOnline into the new settings and logs folders.</summary>
    public static Result Run() =>
        Run(Path.Combine(AppPaths.LocalAppDataRoot, LegacyFolderName), AppPaths.AppDataDirectory,
            AppPaths.InstalledLogsDirectory);

    /// <param name="legacyDir">The old data folder.</param>
    /// <param name="newDir">The new settings folder (created if missing); holds the marker.</param>
    /// <param name="newLogsDir">Where old logs go; defaults to <c>newDir\logs</c>.</param>
    public static Result Run(string legacyDir, string newDir, string? newLogsDir = null)
    {
        try
        {
            if (!Directory.Exists(legacyDir))
                return Result.Skipped;

            var marker = Path.Combine(newDir, MarkerFileName);
            if (File.Exists(marker))
                return Result.Skipped;

            Directory.CreateDirectory(newDir);
            var copied = new List<string>();
            var failed = new List<string>();

            foreach (var src in Directory.GetFiles(legacyDir, "*.json"))
                CopyIfMissing(src, newDir, Path.GetFileName(src), copied, failed);

            var legacyLogs = Path.Combine(legacyDir, "logs");
            if (Directory.Exists(legacyLogs))
            {
                var logsDir = newLogsDir ?? Path.Combine(newDir, "logs");
                Directory.CreateDirectory(logsDir);
                foreach (var src in Directory.GetFiles(legacyLogs))
                    CopyIfMissing(src, logsDir, Path.GetFileName(src), copied, failed);
            }

            if (failed.Count == 0)
                File.WriteAllText(marker, $"Copied from {legacyDir} at {DateTime.UtcNow:O}. The old folder was left in place.{Environment.NewLine}");

            if (copied.Count > 0 || failed.Count > 0)
                Logger.Information("[migrate] Copied {Count} file(s) from {Old} to {New}: {Files}{Failed}",
                    copied.Count, legacyDir, newDir, string.Join(", ", copied),
                    failed.Count > 0 ? $"; {failed.Count} failed, will retry next start: {string.Join(", ", failed)}" : "");
            else
                Logger.Information("[migrate] Nothing to copy from {Old} to {New}", legacyDir, newDir);

            return new Result(true, copied, failed);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[migrate] Could not copy settings from {Old} to {New}", legacyDir, newDir);
            return Result.Skipped;
        }
    }

    private static void CopyIfMissing(string src, string newDir, string relative, List<string> copied, List<string> failed)
    {
        var dest = Path.Combine(newDir, relative);
        if (File.Exists(dest))
            return;
        try
        {
            File.Copy(src, dest, overwrite: false);
            copied.Add(relative);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[migrate] Could not copy {Source} to {Dest}", src, dest);
            failed.Add(relative);
        }
    }
}
