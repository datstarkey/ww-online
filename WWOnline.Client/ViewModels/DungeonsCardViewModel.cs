using CommunityToolkit.Mvvm.ComponentModel;
using WWOnline.Services;

namespace WWOnline.ViewModels;

/// <summary>One dungeon on the Dungeons card: small keys held, map, compass, boss key, boss beaten.</summary>
public partial class DungeonRowViewModel : ObservableObject
{
    public int Slot { get; }
    public string Name { get; }

    [ObservableProperty] private string _keysText = "—";
    [ObservableProperty] private bool _hasKeys;
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty] private bool _hasMap;
    [ObservableProperty] private bool _hasCompass;
    [ObservableProperty] private bool _hasBossKey;
    [ObservableProperty] private bool _bossBeaten;

    public DungeonRowViewModel(int slot, string name)
    {
        Slot = slot;
        Name = name;
    }

    // dSv_memBit_c::mDungeonItem bits (d_save.h:639-646): MAP, COMPASS, BOSS_KEY, STAGE_BOSS_ENEMY.
    private const byte Map = 1 << 0, Compass = 1 << 1, BossKey = 1 << 2, BossEnemy = 1 << 3;

    public void Update(DungeonProgress? p)
    {
        KeysText = p?.Keys.ToString() ?? "—";
        HasKeys = p is { Keys: > 0 };
        DetailText = p is { KeysTaken: int taken, KeysTotal: int total, DoorsOpened: int opened, DoorsTotal: int doors }
            ? $"{taken} of {total} {(total == 1 ? "key" : "keys")} found · {opened} of {doors} {(doors == 1 ? "door" : "doors")} unlocked"
            : "";
        byte items = p?.DungeonItem ?? 0;
        HasMap = (items & Map) != 0;
        HasCompass = (items & Compass) != 0;
        HasBossKey = (items & BossKey) != 0;
        BossBeaten = (items & BossEnemy) != 0;
    }
}

/// <summary>
/// The Room page's Dungeons card: for each dungeon with small keys, the keys held (the room's derived
/// count while Shared world is on, else this game's own) and the map / compass / boss key / boss
/// beaten flags (world flags, shared with Shared world). Read-only for everyone.
/// </summary>
public partial class DungeonsCardViewModel : ObservableObject, IDisposable
{
    /// <summary>The dungeons with small keys, by save slot (dSv_save_c::SaveStageTbl, d_save.h:888-906).</summary>
    public static readonly IReadOnlyList<(int Slot, string Name)> KeyDungeons =
    [
        (3, "Dragon Roost Cavern"),
        (4, "Forbidden Woods"),
        (5, "Tower of the Gods"),
        (6, "Earth Temple"),
        (7, "Wind Temple"),
    ];

    private readonly IDungeonKeysSource _source;
    private readonly Action<Action> _post;
    private bool _disposed;

    public IReadOnlyList<DungeonRowViewModel> Rows { get; } = KeyDungeons.Select(d => new DungeonRowViewModel(d.Slot, d.Name)).ToList();

    [ObservableProperty] private bool _hasGame;
    [ObservableProperty] private bool _isShared;
    [ObservableProperty] private string _stateText = "No game";
    [ObservableProperty] private string _note = "";

    public DungeonsCardViewModel(IDungeonKeysSource source, Action<Action> post)
    {
        _source = source;
        _post = post;
        _source.SnapshotChanged += OnSnapshotChanged;
        Refresh();
    }

    private void OnSnapshotChanged() => _post(Refresh);

    /// <summary>Show the source's current snapshot (on the UI thread).</summary>
    public void Refresh()
    {
        if (_disposed) return;
        var s = _source.Snapshot;
        HasGame = s.HasGame;
        IsShared = s.HasGame && s.Shared;
        StateText = !s.HasGame ? "No game" : s.Shared ? "Shared" : "Off: per player";
        Note = !s.HasGame
            ? "Start the game to see each dungeon's keys and treasures."
            : s.Shared
                ? "Keys come from the shared world: a key anyone finds is everyone's, and a door anyone unlocks uses it up for everyone."
                : "Shared world is off (or you're not in a room), so these are your own keys.";
        foreach (var row in Rows)
            row.Update(s.HasGame ? s.Dungeons.GetValueOrDefault(row.Slot) : null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.SnapshotChanged -= OnSnapshotChanged;
    }
}
