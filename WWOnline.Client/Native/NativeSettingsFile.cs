namespace WWOnline.Native;

/// <summary>
/// The native game's settings.ini, read and edited the way tww-native's pc_settings.cpp reads it: NAME=value lines,
/// NAME@handheld=value / NAME@docked=value for a per-mode setting, '#' comments, and from a "[dev]" line to the end
/// developer variables that are not settings. A change edits its one line and leaves the rest of the file as it
/// was; the file is replaced whole (written next to it, then moved over it), as the game does.
/// </summary>
public sealed class NativeSettingsFile
{
    private const string DevSection = "[dev]";
    private readonly List<string> _lines;

    private NativeSettingsFile(string path, List<string> lines)
    {
        Path = path;
        _lines = lines;
    }

    public string Path { get; }

    /// <summary>The file's lines (none when it doesn't exist yet).</summary>
    public static NativeSettingsFile Load(string path) =>
        new(path, File.Exists(path) ? File.ReadAllLines(path).ToList() : []);

    /// <summary>
    /// The value the game would read: for a per-mode setting the mode's line, else the plain one; for another the
    /// plain line, else a per-mode line (handheld first). Null when the file has neither (the default applies).
    /// </summary>
    public string? Get(string key, bool perMode, string mode)
    {
        var values = Values();
        if (perMode && values.TryGetValue($"{key}@{mode}", out var own)) return own;
        if (values.TryGetValue(key, out var plain)) return plain;
        if (perMode) return null;
        return values.TryGetValue($"{key}@handheld", out var handheld) ? handheld
            : values.TryGetValue($"{key}@docked", out var docked) ? docked
            : null;
    }

    /// <summary>Sets the line the game would write for this change (the mode's for a per-mode setting).</summary>
    public void Set(string key, bool perMode, string mode, string value)
    {
        string name = perMode ? $"{key}@{mode}" : key;
        string line = $"{name}={value}";
        int end = MainEnd();
        int at = -1;
        for (int i = end - 1; i >= 0; i--)
        {
            if (NameOf(_lines[i]) != name) continue;
            if (at >= 0) _lines.RemoveAt(at); // the game keeps the last of several lines: one is enough
            at = i;
        }
        if (at >= 0)
        {
            _lines[at] = line;
            return;
        }
        // At the end of the settings, before the blank lines that lead into [dev].
        while (end > 0 && end < _lines.Count && _lines[end - 1].Trim().Length == 0) end--;
        _lines.Insert(end, line);
    }

    public void Save()
    {
        if (System.IO.Path.GetDirectoryName(Path) is { Length: > 0 } dir) Directory.CreateDirectory(dir);
        string tmp = Path + ".tmp";
        File.WriteAllLines(tmp, _lines);
        File.Move(tmp, Path, overwrite: true);
    }

    // The settings' NAME -> value, the last line of a name winning (the game reads them in order).
    private Dictionary<string, string> Values()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < MainEnd(); i++)
        {
            if (NameOf(_lines[i]) is { } name) values[name] = _lines[i][(_lines[i].IndexOf('=') + 1)..].Trim();
        }
        return values;
    }

    // The index of the [dev] line, or the end of the file.
    private int MainEnd()
    {
        int dev = _lines.FindIndex(l => l.Trim() == DevSection);
        return dev >= 0 ? dev : _lines.Count;
    }

    // A setting line's NAME (with its @mode), or null for a comment, a blank line or a line without one.
    private static string? NameOf(string line)
    {
        string trimmed = line.Trim();
        int eq = trimmed.IndexOf('=');
        if (trimmed.Length == 0 || trimmed[0] == '#' || eq <= 0) return null;
        string name = trimmed[..eq].Trim();
        return name.Length > 0 ? name : null;
    }
}
