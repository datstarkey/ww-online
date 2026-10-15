using Serilog;

namespace WWOnline.Native;

/// <summary>
/// The native game's own settings for the Game settings page. While the game runs, through tww.h's settings API
/// (applied live, saved by the game). Otherwise from settings.ini in its user folder (<see cref="NativeSettingsFile"/>),
/// described by the catalogue the game gave at its last run (cached next to it): the game reads the file when it
/// starts. Any thread; the game's calls go through <see cref="INativeGame.Invoke{T}"/>.
/// </summary>
public sealed class NativeGameSettings
{
    private static readonly ILogger Logger = Log.ForContext<NativeGameSettings>();
    public const string FileName = "settings.ini";
    public const string CatalogFileName = "settings-catalogue.json";

    private readonly INativeGame _game;
    private readonly string _userDirectory;
    private readonly Func<string, string?> _environment;

    public NativeGameSettings(INativeGame game)
        : this(game, NativePaths.UserDirectory, Environment.GetEnvironmentVariable)
    {
    }

    /// <param name="game">The embedded game.</param>
    /// <param name="userDirectory">Its COS_USER_DIR (<see cref="NativePaths.UserDirectory"/>).</param>
    /// <param name="environment">This process's environment, which the game inherits: a COS_* variable set there
    /// locks its setting.</param>
    public NativeGameSettings(INativeGame game, string userDirectory, Func<string, string?> environment)
    {
        _game = game;
        _userDirectory = userDirectory;
        _environment = environment;
    }

    public INativeGame Game => _game;

    public string SettingsPath => Path.Combine(_userDirectory, FileName);
    private string CatalogPath => Path.Combine(_userDirectory, CatalogFileName);

    /// <summary>The settings now: the game's while it runs, else the file's. Null before the game's first run.</summary>
    public GameSettingsCatalog? Load()
    {
        if (CallGame(api => api.SettingsCatalogue()) is { } json && GameSettingsCatalog.Parse(json) is { } live)
        {
            SaveCatalog(live);
            return live;
        }
        if (!File.Exists(CatalogPath) || GameSettingsCatalog.Parse(File.ReadAllText(CatalogPath)) is not { } cached)
            return null;
        var file = NativeSettingsFile.Load(SettingsPath);
        return cached with
        {
            Settings = cached.Settings.Select(s => s with
            {
                Value = _environment(s.Key) ?? file.Get(s.Key, s.PerMode, cached.Mode) ?? s.Default,
                Locked = _environment(s.Key) != null,
                RestartPending = false,
            }).ToList(),
        };
    }

    /// <summary>
    /// Sets one setting: through the game while it runs, else in the file. False: locked, not one of its choices,
    /// or the game refused it.
    /// </summary>
    public bool Set(GameSettingsCatalog catalog, GameSettingInfo setting, string value)
    {
        if (setting.Locked || setting.Choices.All(c => c.Value != value)) return false;
        if (_game.IsRunning)
        {
            try
            {
                return _game.Invoke(api => api is ITwwSettingsApi settings && settings.SetSetting(setting.Key, value));
            }
            catch (InvalidOperationException)
            {
                // It stopped meanwhile: the file is the settings again.
            }
            catch (TimeoutException ex)
            {
                Logger.Warning(ex, "[settings] the game didn't take {Key}={Value}", setting.Key, value);
                return false;
            }
        }
        var file = NativeSettingsFile.Load(SettingsPath);
        file.Set(setting.Key, setting.PerMode, catalog.Mode, value);
        file.Save();
        Logger.Information("[settings] {Key}={Value} saved to {Path} for the next start", setting.Key, value, SettingsPath);
        return true;
    }

    private string? CallGame(Func<ITwwSettingsApi, string> call)
    {
        if (!_game.IsRunning) return null;
        try
        {
            return _game.Invoke(api => api is ITwwSettingsApi settings ? call(settings) : null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            Logger.Warning(ex, "[settings] couldn't read the game's settings");
            return null;
        }
    }

    private void SaveCatalog(GameSettingsCatalog catalog)
    {
        try
        {
            Directory.CreateDirectory(_userDirectory);
            File.WriteAllText(CatalogPath, catalog.ToJson());
        }
        catch (IOException ex)
        {
            Logger.Warning(ex, "[settings] couldn't cache the settings catalogue at {Path}", CatalogPath);
        }
    }
}
