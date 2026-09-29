using System.Diagnostics;

namespace WWOnline.Services;

/// <summary>
/// Finds Dolphin.exe for the setup wizard: a running Dolphin, the installer's Program Files folder,
/// Scoop, and portable builds unpacked into the usual places (the Desktop, Documents, Downloads or a
/// drive root, as "Dolphin-x64" or any folder starting with "Dolphin"). Cheap: a handful of
/// File.Exists calls and one level of folder listing.
/// </summary>
public static class DolphinLocator
{
    /// <summary>The emulator's process and file name. DolphinService attaches to processes named "Dolphin".</summary>
    public const string ExeName = "Dolphin.exe";

    /// <summary>Existing Dolphin.exe files, most likely first, without duplicates.</summary>
    public static IReadOnlyList<string> FindCandidates() => FindCandidates(RunningDolphinPaths(), CandidatePaths());

    /// <summary>As above with given inputs (tests): running processes' exe paths, then likely install paths.</summary>
    public static IReadOnlyList<string> FindCandidates(IEnumerable<string> runningExePaths, IEnumerable<string> likelyPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        foreach (var path in runningExePaths.Concat(likelyPaths))
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { continue; }
            if (File.Exists(full) && seen.Add(full)) found.Add(full);
        }
        return found;
    }

    /// <summary>Why <paramref name="path"/> can't be used as Dolphin (null = fine).</summary>
    public static string? Validate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Choose Dolphin.exe.";
        var trimmed = path.Trim();
        if (Directory.Exists(trimmed))
            return File.Exists(Path.Combine(trimmed, ExeName))
                ? $"That's Dolphin's folder. Choose {ExeName} inside it."
                : $"That's a folder. Choose {ExeName}.";
        if (!File.Exists(trimmed))
            return $"File not found: {trimmed}";
        var name = Path.GetFileName(trimmed);
        if (name.StartsWith("DolphinTool", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("DolphinUpdater", StringComparison.OrdinalIgnoreCase))
            return $"That's {name}, one of Dolphin's helper programs. Choose {ExeName} next to it.";
        if (!string.Equals(name, ExeName, StringComparison.OrdinalIgnoreCase))
            return $"That isn't {ExeName}. WW-Online finds the game by looking for a program called Dolphin, so choose {ExeName}.";
        return null;
    }

    private static IEnumerable<string> RunningDolphinPaths()
    {
        Process[] processes;
        try { processes = Process.GetProcessesByName("Dolphin"); }
        catch (InvalidOperationException) { yield break; }

        foreach (var p in processes)
        {
            string? path = null;
            try { path = p.MainModule?.FileName; }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            finally { p.Dispose(); }
            if (path != null) yield return path;
        }
    }

    private static IEnumerable<string> CandidatePaths()
    {
        var folders = new List<string>();
        void Add(Environment.SpecialFolder f)
        {
            var dir = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(dir)) folders.Add(dir);
        }

        // The installer's folders (x64 and the older names).
        foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var dir = Environment.GetFolderPath(root);
            if (string.IsNullOrEmpty(dir)) continue;
            yield return Path.Combine(dir, "Dolphin", ExeName);
            yield return Path.Combine(dir, "Dolphin-x64", ExeName);
            yield return Path.Combine(dir, "Dolphin Emulator", ExeName);
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
        {
            yield return Path.Combine(profile, "scoop", "apps", "dolphin", "current", ExeName);
            yield return Path.Combine(profile, "scoop", "apps", "dolphin-dev", "current", ExeName);
            folders.Add(Path.Combine(profile, "Downloads"));
        }

        // Portable builds: <folder>\Dolphin*\Dolphin.exe (and one level deeper, e.g. Dolphin-x64\Dolphin-x64).
        Add(Environment.SpecialFolder.Desktop);
        Add(Environment.SpecialFolder.MyDocuments);
        Add(Environment.SpecialFolder.LocalApplicationData);
        foreach (var drive in SafeFixedDriveRoots()) folders.Add(drive);

        foreach (var folder in folders)
        {
            foreach (var dir in SafeDolphinDirs(folder))
            {
                yield return Path.Combine(dir, ExeName);
                foreach (var inner in SafeDolphinDirs(dir))
                    yield return Path.Combine(inner, ExeName);
            }
        }
    }

    private static IEnumerable<string> SafeDolphinDirs(string folder)
    {
        try
        {
            return Directory.Exists(folder)
                ? Directory.EnumerateDirectories(folder, "Dolphin*").Take(20).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeFixedDriveRoots()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
