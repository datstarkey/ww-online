using System.Collections.Concurrent;
using System.Text.Json;
using Serilog;

namespace WWOnline.Services;

/// <summary>
/// Generic JSON file-backed settings store.
/// Handles load/save with error handling and app data directory creation.
/// Loads and saves of the same file are serialised (every store instance for that path shares one
/// lock: background build checks load while the UI saves), and a save writes a temp file then moves
/// it over the old one, so a reader never sees a half-written file and falls back to defaults.
/// </summary>
public class JsonSettingsStore<T> where T : new()
{
    private static readonly ILogger Logger = Log.ForContext(typeof(JsonSettingsStore<T>));
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, object> FileLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _fileLock;

    public string SettingsPath { get; }

    public JsonSettingsStore(string fileName)
        : this(fileName, AppPaths.AppDataDirectory)
    {
    }

    /// <param name="fileName">Settings file name.</param>
    /// <param name="directory">Folder to keep it in (the app data folder by default; tests pass a temp folder).</param>
    public JsonSettingsStore(string fileName, string directory)
    {
        Directory.CreateDirectory(directory);
        SettingsPath = Path.Combine(directory, fileName);
        _fileLock = FileLocks.GetOrAdd(Path.GetFullPath(SettingsPath), _ => new object());
    }

    public T Load()
    {
        try
        {
            string? json = null;
            lock (_fileLock)
            {
                if (File.Exists(SettingsPath))
                    json = File.ReadAllText(SettingsPath);
            }
            if (json != null && JsonSerializer.Deserialize<T>(json) is { } settings)
                return settings;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to load settings from {Path}, using defaults", SettingsPath);
        }

        return new T();
    }

    public void Save(T settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, WriteOptions);
            var temp = SettingsPath + ".tmp";
            lock (_fileLock)
            {
                File.WriteAllText(temp, json);
                File.Move(temp, SettingsPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to save settings to {Path}", SettingsPath);
        }
    }
}
