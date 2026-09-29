using Serilog;
using WWOnline.Patcher.Config;
using WWOnline.Patcher.WorldData;

namespace WWOnline.Services;

/// <summary>
/// The switch table the patcher wrote next to the patched game (<see cref="SwitchTable"/>): which dungeon
/// and room switches may sync, and which memory switches must never merge. Looked up in the Settings
/// page's patched-game folder, then (dev) GameMod/config.json's game_path, and re-read when the file
/// changes (the player patched again). Null while there is none: the game was patched by an older app
/// (patch again), and then only memory switches sync, as before.
/// </summary>
public sealed class SwitchTableProvider
{
    private static readonly ILogger Logger = Log.ForContext<SwitchTableProvider>();
    private static readonly TimeSpan RecheckEvery = TimeSpan.FromSeconds(10);

    private readonly Func<IEnumerable<string>> _gamePaths;
    private readonly object _lock = new();
    private SwitchTable? _table;
    private string? _path;
    private DateTime _stamp;
    private DateTime _nextCheck = DateTime.MinValue;
    private bool _loggedMissing;

    public SwitchTableProvider(GameSettingsService settings) : this(() => DefaultGamePaths(settings)) { }

    public SwitchTableProvider(Func<IEnumerable<string>> gamePaths) => _gamePaths = gamePaths;

    private static IEnumerable<string> DefaultGamePaths(GameSettingsService settings)
    {
        var fromSettings = settings.Load().GamePath;
        if (!string.IsNullOrWhiteSpace(fromSettings)) yield return fromSettings;
        var gameMod = GamePatcherService.FindGameModFolder();
        if (gameMod == null) yield break;
        string? devPath = null;
        try { devPath = PatcherConfig.LoadFromConfigJson(gameMod).GamePath; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException) { }
        if (!string.IsNullOrWhiteSpace(devPath)) yield return devPath;
    }

    /// <summary>The current table, or null when no patched game has one.</summary>
    public SwitchTable? Table
    {
        get
        {
            lock (_lock)
            {
                if (DateTime.UtcNow >= _nextCheck)
                {
                    _nextCheck = DateTime.UtcNow + RecheckEvery;
                    Refresh();
                }
                return _table;
            }
        }
    }

    private void Refresh()
    {
        foreach (var gamePath in _gamePaths())
        {
            var file = Path.Combine(gamePath, SwitchTable.FileName);
            DateTime stamp;
            try { stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue; }
            catch (IOException) { continue; }
            if (stamp == DateTime.MinValue) continue;
            if (file == _path && stamp == _stamp && _table != null) return;

            var table = SwitchTable.Load(gamePath);
            if (table == null) continue;
            if (table.Rules != SwitchTableBuilder.RulesVersion)
            {
                if (!_loggedMissing)
                    Logger.Warning("[switches] {File} was built with rules {Rules}, this app uses {Current}: patch the game again",
                        file, table.Rules, SwitchTableBuilder.RulesVersion);
                _loggedMissing = true;
                continue;
            }
            _table = table;
            _path = file;
            _stamp = stamp;
            _loggedMissing = false;
            Logger.Information("[switches] switch table {File}: {Stages} stages, {Dan} dan / {Zone} room switch(es) syncable, {Excluded} push-block switch(es) excluded",
                file, table.StageCount, table.Dan.Sum(d => d.Value.Length), table.Zone.Sum(z => z.Value.Sum(r => r.Value.Length)),
                table.MemoryExcluded.Sum(m => m.Value.Length));
            return;
        }
        if (_table == null && !_loggedMissing)
        {
            _loggedMissing = true;
            Logger.Warning("[switches] no {File} next to the patched game: dungeon and room switches won't sync until the game is patched again",
                SwitchTable.FileName);
        }
    }
}
