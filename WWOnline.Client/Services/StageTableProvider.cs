using Serilog;
using WWOnline.Patcher.Config;

namespace WWOnline.Services;

/// <summary>
/// A table built from the player's own stage files on first use (on a background thread) and kept in memory,
/// never written anywhere: the small-key table (<see cref="SmallKeyTableProvider"/>) and the heart table
/// (<see cref="HeartTableProvider"/>). Looked up in the Settings page's patched game, then its vanilla game,
/// then (dev) GameMod/config.json's game_path and vanilla_game_path: the stage files are the same in all of
/// them (no patch edits a stage's actors). Null until built, or when no game folder has stage data; retried
/// every 30 s.
/// </summary>
public abstract class StageTableProvider<T> where T : class
{
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(30);

    private readonly Func<IEnumerable<string>> _gamePaths;
    private readonly Func<string, T?> _build;
    private readonly object _lock = new();
    private T? _table;
    private Task? _building;
    private DateTime _nextAttempt = DateTime.MinValue;
    private bool _loggedMissing;

    protected StageTableProvider(ILogger logger, Func<IEnumerable<string>> gamePaths, Func<string, T?> build)
    {
        Logger = logger;
        _gamePaths = gamePaths;
        _build = build;
    }

    protected ILogger Logger { get; }

    /// <summary>The log tag, e.g. "[keys]".</summary>
    protected abstract string Tag { get; }

    /// <summary>A built table worth keeping (a folder whose stage data has nothing relevant is skipped).</summary>
    protected abstract bool IsUsable(T table);

    /// <summary>Log what was built (once, when it is).</summary>
    protected abstract void LogBuilt(string path, T table);

    /// <summary>Logged once when no game folder has stage data.</summary>
    protected abstract string MissingMessage { get; }

    /// <summary>The Settings page's game folders, then (dev) GameMod/config.json's.</summary>
    public static IEnumerable<string> DefaultGamePaths(GameSettingsService settings)
    {
        var s = settings.Load();
        if (!string.IsNullOrWhiteSpace(s.GamePath)) yield return s.GamePath;
        if (!string.IsNullOrWhiteSpace(s.VanillaGamePath)) yield return s.VanillaGamePath;
        var gameMod = GamePatcherService.FindGameModFolder();
        if (gameMod == null) yield break;
        PatcherConfig? config = null;
        try { config = PatcherConfig.LoadFromConfigJson(gameMod); }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { }
        if (config == null) yield break;
        if (!string.IsNullOrWhiteSpace(config.GamePath)) yield return config.GamePath;
        if (!string.IsNullOrWhiteSpace(config.ResolvedVanillaGamePath)) yield return config.ResolvedVanillaGamePath;
    }

    /// <summary>The table, or null while it is being built / when no game folder has stage data. Starts a build if needed.</summary>
    public T? Table
    {
        get
        {
            lock (_lock)
            {
                if (_table == null && _building == null && DateTime.UtcNow >= _nextAttempt)
                {
                    _nextAttempt = DateTime.UtcNow + RetryEvery;
                    _building = Task.Run(Build);
                }
                return _table;
            }
        }
    }

    /// <summary>Build now on the calling thread (tests, and the background build).</summary>
    public T? Build()
    {
        try
        {
            foreach (var path in _gamePaths().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                T? table;
                try { table = _build(path); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    Logger.Warning(ex, Tag + " couldn't read the stage data in {Path}", path);
                    continue;
                }
                if (table == null || !IsUsable(table)) continue;
                lock (_lock) _table = table;
                LogBuilt(path, table);
                return table;
            }
            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Logger.Warning(Tag + " " + MissingMessage);
            }
            return null;
        }
        finally
        {
            lock (_lock) _building = null;
        }
    }
}
