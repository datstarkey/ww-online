using Serilog;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>
/// The small-key table (<see cref="SmallKeyTable"/>): every dungeon's key sources and key doors, built from
/// the player's own stage files on first use (about 0.3 s for the whole game, on a background thread)
/// and kept in memory, never written anywhere. Looked up in the Settings page's patched game, then its
/// vanilla game, then (dev) GameMod/config.json's game_path and vanilla_game_path: the stage files are
/// the same in all of them (no patch edits a stage's actors). Null until built, or when no game folder
/// has stage data (then small keys don't sync); retried every 30 s.
/// </summary>
public sealed class SmallKeyTableProvider
{
    private static readonly ILogger Logger = Log.ForContext<SmallKeyTableProvider>();
    private static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(30);

    private readonly Func<IEnumerable<string>> _gamePaths;
    private readonly Func<string, SmallKeyTable?> _build;
    private readonly object _lock = new();
    private SmallKeyTable? _table;
    private Task? _building;
    private DateTime _nextAttempt = DateTime.MinValue;
    private bool _loggedMissing;

    public SmallKeyTableProvider(GameSettingsService settings) : this(() => DefaultGamePaths(settings), BuildFrom) { }

    public SmallKeyTableProvider(Func<IEnumerable<string>> gamePaths, Func<string, SmallKeyTable?> build)
    {
        _gamePaths = gamePaths;
        _build = build;
    }

    private static IEnumerable<string> DefaultGamePaths(GameSettingsService settings)
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

    private static SmallKeyTable? BuildFrom(string gamePath)
    {
        var skipped = new List<string>();
        var table = SmallKeyTableBuilder.BuildFromGame(gamePath, skipped.Add);
        if (skipped.Count > 0)
            Logger.Warning("[keys] skipped {Count} stage(s) of {Path} that couldn't be read: {Stages}", skipped.Count, gamePath, string.Join("; ", skipped));
        return table;
    }

    /// <summary>The table, or null while it is being built / when no game folder has stage data. Starts a build if needed.</summary>
    public SmallKeyTable? Table
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
    public SmallKeyTable? Build()
    {
        try
        {
            foreach (var path in _gamePaths().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                SmallKeyTable? table;
                try { table = _build(path); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                {
                    Logger.Warning(ex, "[keys] couldn't read the stage data in {Path}", path);
                    continue;
                }
                if (table == null || table.Dungeons.Count == 0) continue;
                lock (_lock) _table = table;
                Logger.Information("[keys] small-key table from {Path} ({Stages} stages): {Summary}", path, table.StageCount, table.Summary());
                foreach (var d in table.Dungeons.Values.Where(d => d.Doors.Count > 0 && d.Sources.Count != d.Doors.Count))
                    Logger.Warning("[keys] slot {Slot} has {Keys} key(s) for {Doors} key door(s)", d.Slot, d.Sources.Count, d.Doors.Count);
                foreach (var d in table.Dungeons.Values.Where(d => d.Untracked.Count > 0))
                    Logger.Information("[keys] slot {Slot}: key source(s) the save can't show, not counted: {What}", d.Slot, string.Join("; ", d.Untracked));
                return table;
            }
            if (!_loggedMissing)
            {
                _loggedMissing = true;
                Logger.Warning("[keys] no game folder with stage data (files/res/Stage + sys/main.dol): small keys won't be shared until one is set on the Settings page");
            }
            return null;
        }
        finally
        {
            lock (_lock) _building = null;
        }
    }
}
