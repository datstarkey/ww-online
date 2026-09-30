using System.IO.Compression;
using System.Text;
using WWOnline.Shared;
using WWOnline.Shared.Hubs;

namespace WWOnline.Services;

/// <summary>
/// Settings → Bug report: one zip a player can attach to an issue, holding what we'd ask them for.
///   - the client's recent logs (<see cref="AppPaths.LogsDirectory"/>, or the --log-file one) and a hosted room's
///     server.log;
///   - Dolphin's own log (dolphin.log in its user folder, portable or Documents) when it's recent;
///   - about.txt: versions, the game build check, the selected patches and the last room rules.
/// Text files have the Windows user folder replaced by %USERPROFILE%, so the player's name isn't in a public issue.
/// Nothing is sent anywhere: the zip is written to <see cref="ReportsDirectory"/> for the player to attach.
/// </summary>
public static class BugReport
{
    /// <summary>Logs changed longer ago than this are left out (the daily client logs pile up).</summary>
    public static readonly TimeSpan MaxLogAge = TimeSpan.FromDays(2);

    /// <summary>At most this many client log files (the newest).</summary>
    public const int MaxClientLogs = 3;

    /// <summary>Where reports are written: next to the logs.</summary>
    public static string ReportsDirectory => Path.Combine(Path.GetDirectoryName(AppPaths.LogsDirectory.TrimEnd('\\', '/')) ?? ".", "bug-reports");

    /// <summary>One file that goes into the report: its name in the zip and where it is.</summary>
    public readonly record struct Entry(string Name, string Path);

    /// <summary>The files to include, newest client logs first; missing or stale files are skipped.</summary>
    public static List<Entry> CollectFiles(string logsDirectory, string? explicitLogFile, string? dolphinExe, DateTime now)
    {
        var entries = new List<Entry>();
        bool Recent(FileInfo f) => f.Exists && now - f.LastWriteTime <= MaxLogAge;

        if (!string.IsNullOrWhiteSpace(explicitLogFile))
        {
            var f = new FileInfo(explicitLogFile);
            if (Recent(f)) entries.Add(new("client/" + f.Name, f.FullName));
        }
        else if (Directory.Exists(logsDirectory))
        {
            foreach (var f in new DirectoryInfo(logsDirectory).GetFiles("ww-online-*.txt")
                         .Where(Recent).OrderByDescending(f => f.LastWriteTime).Take(MaxClientLogs))
                entries.Add(new("client/" + f.Name, f.FullName));
        }

        // A hosted room's server logs next to the client's log.
        var serverLog = new FileInfo(Path.Combine(
            string.IsNullOrWhiteSpace(explicitLogFile) ? logsDirectory : Path.GetDirectoryName(Path.GetFullPath(explicitLogFile)) ?? ".",
            "server.log"));
        if (Recent(serverLog)) entries.Add(new("server/server.log", serverLog.FullName));

        foreach (var candidate in DolphinLogCandidates(dolphinExe))
        {
            var f = new FileInfo(candidate);
            if (!Recent(f)) continue;
            entries.Add(new("dolphin/dolphin.log", f.FullName));
            break;
        }
        return entries;
    }

    /// <summary>dolphin.log: a portable Dolphin's User folder next to the exe first, then Documents\Dolphin Emulator.</summary>
    public static IEnumerable<string> DolphinLogCandidates(string? dolphinExe)
    {
        if (!string.IsNullOrWhiteSpace(dolphinExe) && Path.GetDirectoryName(dolphinExe) is { } dir)
            yield return Path.Combine(dir, "User", "Logs", "dolphin.log");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Dolphin Emulator", "Logs", "dolphin.log");
    }

    /// <summary>Replace the Windows user folder in <paramref name="text"/> with %USERPROFILE% (any case, either slash).</summary>
    public static string Redact(string text, string userProfile)
    {
        if (string.IsNullOrEmpty(userProfile)) return text;
        text = text.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return text.Replace(userProfile.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>about.txt: what the report is from.</summary>
    public static string About(GameSettings settings, string buildCheck, DateTime now)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{AppInfo.DisplayName} {AppInfo.Version} (commit {AppInfo.Commit}), protocol {HubConstants.ProtocolVersion}");
        sb.AppendLine($"Report made {now:yyyy-MM-dd HH:mm:ss} (local time)");
        sb.AppendLine($"OS: {Environment.OSVersion}, {(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit");
        sb.AppendLine($"Game build check: {buildCheck}");
        sb.AppendLine("Optional patches: " + (settings.OptionalPatches == null ? "the defaults" : settings.OptionalPatches.Count == 0 ? "none" : string.Join(", ", settings.OptionalPatches)));
        sb.AppendLine("Start Dolphin on host/join: " + (settings.AutoLaunchDolphin ? "yes" : "no"));
        sb.AppendLine("Last hosted room rules: " + (settings.HostedRoomRules?.RulesSummary() ?? "never hosted"));
        return sb.ToString();
    }

    /// <summary>Write the zip to <paramref name="directory"/> and return its path. Text is redacted; a file that
    /// can't be read (locked, deleted meanwhile) is noted in about.txt instead.</summary>
    public static string Write(string directory, IReadOnlyList<Entry> files, string about, string userProfile, DateTime now)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"WW-Online-bug-report-{now:yyyyMMdd-HHmmss}.zip");
        var skipped = new List<string>();
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                string text;
                try
                {
                    // The logs are open for writing (Serilog shared: true, Dolphin): read with sharing.
                    using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    text = reader.ReadToEnd();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped.Add($"{file.Name}: {ex.Message}");
                    continue;
                }
                using var writer = new StreamWriter(zip.CreateEntry(file.Name).Open());
                writer.Write(Redact(text, userProfile));
            }
            if (skipped.Count > 0) about += "Couldn't include:" + Environment.NewLine + string.Join(Environment.NewLine, skipped) + Environment.NewLine;
            using var aboutWriter = new StreamWriter(zip.CreateEntry("about.txt").Open());
            aboutWriter.Write(Redact(about, userProfile));
        }
        return path;
    }
}
