using System.Text.Json;
using Serilog;

namespace WWOnline.Services;

/// <summary>
/// Generic JSON file-backed settings store.
/// Handles load/save with error handling and app data directory creation.
/// </summary>
public class JsonSettingsStore<T> where T : new()
{
    private static readonly ILogger Logger = Log.ForContext(typeof(JsonSettingsStore<T>));
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

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
    }

    public T Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<T>(json);
                if (settings != null)
                    return settings;
            }
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
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to save settings to {Path}", SettingsPath);
        }
    }
}
