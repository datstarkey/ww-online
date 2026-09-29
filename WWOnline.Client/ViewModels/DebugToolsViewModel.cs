using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Data;
using WWOnline.Services;

namespace WWOnline.ViewModels;

/// <summary>A warp target. <see cref="Group"/> is the heading it's listed under on the Warp page.</summary>
public record WarpDestinationInfo(string Name, string StageName, byte Room, byte Spawn, string Group = WarpGroups.Sea)
{
    /// <summary>Mono code shown next to the name: the stage folder, plus the room for sea islands.</summary>
    public string Code => StageName == "sea" ? $"sea · {Room}" : StageName;
}

public static class WarpGroups
{
    public const string Sea = "SEA AND ISLANDS";
    public const string Dungeons = "DUNGEONS";
}

/// <summary>A selectable stage row on the Warp page.</summary>
public partial class WarpStageItem : ObservableObject
{
    public WarpDestinationInfo Info { get; }
    [ObservableProperty] private bool _isSelected;
    public WarpStageItem(WarpDestinationInfo info) => Info = info;
}

/// <summary>A heading plus its (search-filtered) stage rows.</summary>
public class WarpGroupViewModel
{
    public string Title { get; }
    public ObservableCollection<WarpStageItem> Items { get; } = new();
    public WarpGroupViewModel(string title) => Title = title;
}

/// <summary>Which Tools tab is shown. Item editing lives on the Room items page and event flags on the
/// Room story flags page, not here.</summary>
public enum ToolPage
{
    Warp,
    Stats,
    Memory
}

public partial class DebugToolsViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private static readonly ILogger Logger = Log.ForContext<DebugToolsViewModel>();

    private readonly IDolphinService _dolphinService;

    // ═══ Left sub-nav selection ═══
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToolStats))]
    [NotifyPropertyChangedFor(nameof(IsToolWarp))]
    [NotifyPropertyChangedFor(nameof(IsToolMemory))]
    private ToolPage _selectedTool = ToolPage.Warp;

    public bool IsToolStats => SelectedTool == ToolPage.Stats;
    public bool IsToolWarp => SelectedTool == ToolPage.Warp;
    public bool IsToolMemory => SelectedTool == ToolPage.Memory;

    [RelayCommand] private void PickStats() => SelectedTool = ToolPage.Stats;
    [RelayCommand] private void PickWarp() => SelectedTool = ToolPage.Warp;
    [RelayCommand] private void PickMemory() => SelectedTool = ToolPage.Memory;

    // ═══ Memory Read/Write ═══
    [ObservableProperty]
    private string _memoryAddress = "803C525C";

    [ObservableProperty]
    private string _memoryValue = "";

    [ObservableProperty]
    private string _memoryReadResult = "";

    // ═══ Status ═══
    [ObservableProperty]
    private string? _statusMessage;

    // ═══ Room Warp ═══
    // Stage names must be folders under <game>/files/res/Stage/ — WarpToDestination refuses any
    // that aren't (a missing Stage.arc hard-halts the game: "res info set error" +
    // d_s_play.cpp assert). Islands are rooms of the "sea" stage. Spawn points are best-known
    // values; a stage/room/spawn that doesn't exist may still misbehave, so prefer these.
    public List<WarpDestinationInfo> WarpDestinations { get; } = new()
    {
        new("Outset Island", "sea", 44, 0),
        new("Windfall Island", "sea", 11, 0),
        new("Dragon Roost Island", "sea", 13, 0),
        new("Link's House", "LinkRM", 0, 0),
        new("Forest Haven (interior)", "Omori", 0, 0),
        new("Forsaken Fortress (exterior)", "MajyuE", 0, 0),
        new("Hyrule Castle", "Hyrule", 0, 0),
        new("Dragon Roost Cavern", "M_NewD2", 0, 0, WarpGroups.Dungeons),
        new("Forbidden Woods", "kindan", 0, 0, WarpGroups.Dungeons),
        new("Tower of the Gods", "Siren", 0, 0, WarpGroups.Dungeons),
        new("Earth Temple", "M_Dai", 0, 0, WarpGroups.Dungeons),
        new("Wind Temple", "kaze", 15, 15, WarpGroups.Dungeons),
        new("Ganon's Tower (GanonA)", "GanonA", 0, 0, WarpGroups.Dungeons),
    };

    private readonly List<WarpStageItem> _warpItems = new();

    /// <summary>The Warp page's stage list, grouped and filtered by <see cref="WarpSearch"/>.</summary>
    public ObservableCollection<WarpGroupViewModel> WarpStageGroups { get; } = new();

    [ObservableProperty]
    private string _warpSearch = "";

    partial void OnWarpSearchChanged(string value) => RebuildWarpGroups();

    private void RebuildWarpGroups()
    {
        var q = WarpSearch?.Trim() ?? "";
        WarpStageGroups.Clear();
        foreach (var title in new[] { WarpGroups.Sea, WarpGroups.Dungeons })
        {
            var group = new WarpGroupViewModel(title);
            foreach (var item in _warpItems)
            {
                if (item.Info.Group != title) continue;
                if (q.Length > 0 &&
                    !item.Info.Name.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                    !item.Info.StageName.Contains(q, StringComparison.OrdinalIgnoreCase))
                    continue;
                group.Items.Add(item);
            }
            if (group.Items.Count > 0) WarpStageGroups.Add(group);
        }
    }

    [RelayCommand]
    private void SelectWarp(WarpStageItem? item)
    {
        if (item != null) SelectedWarp = item.Info;
    }

    partial void OnSelectedWarpChanged(WarpDestinationInfo? value)
    {
        foreach (var item in _warpItems) item.IsSelected = item.Info == value;
        // The room/spawn boxes are what WarpToDestination writes — keep them in sync with the
        // chosen destination (they used to stay at 0/0 regardless of the entry).
        if (value == null) return;
        WarpRoom = value.Room.ToString();
        WarpSpawn = value.Spawn.ToString();
    }

    /// <summary>
    /// The game's own files are the source of truth for stage names: true if
    /// files/res/Stage/&lt;name&gt;/Stage.arc exists in the configured patched or vanilla game,
    /// null if no game folder is configured (can't check).
    /// </summary>
    private bool? StageExists(string stageName)
    {
        var settings = _settingsService.Load();
        bool checkedAny = false;
        foreach (var root in new[] { settings.GamePath, settings.VanillaGamePath })
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var stageDir = Path.Combine(root, "files", "res", "Stage");
            if (!Directory.Exists(stageDir)) continue;
            checkedAny = true;
            if (File.Exists(Path.Combine(stageDir, stageName, "Stage.arc"))) return true;
        }
        return checkedAny ? false : null;
    }

    [ObservableProperty]
    private WarpDestinationInfo? _selectedWarp;

    [ObservableProperty]
    private string _warpRoom = "0";

    [ObservableProperty]
    private string _warpSpawn = "0";

    // ═══ Stat Editing ═══
    [ObservableProperty]
    private string _editHealth = "12";

    [ObservableProperty]
    private string _editMaxHealth = "12";

    [ObservableProperty]
    private string _editRupees = "0";

    [ObservableProperty]
    private string _editMagic = "0";

    [ObservableProperty]
    private string _editMaxMagic = "16";

    [ObservableProperty]
    private string _editArrows = "30";

    [ObservableProperty]
    private string _editBombs = "30";

    private readonly GameSettingsService _settingsService;

    public DebugToolsViewModel(
        IDolphinService dolphinService,
        GameSettingsService settingsService)
    {
        _dolphinService = dolphinService;
        _settingsService = settingsService;
        foreach (var d in WarpDestinations) _warpItems.Add(new WarpStageItem(d));
        RebuildWarpGroups();
        SelectedWarp = WarpDestinations[0];
    }

    // ═══ Memory Read/Write Commands ═══

    [RelayCommand]
    private void ReadMemory()
    {
        try
        {
            if (uint.TryParse(MemoryAddress, System.Globalization.NumberStyles.HexNumber, null, out var address))
            {
                var bytes = _dolphinService.ReadMemory(address, 4);
                if (bytes != null && bytes.Length >= 4)
                {
                    var value = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
                    MemoryReadResult = $"0x{value:X8}";
                }
                else
                {
                    StatusMessage = "Read returned no data";
                }
            }
            else
            {
                StatusMessage = "Invalid address format";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Read failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void WriteMemory()
    {
        try
        {
            if (uint.TryParse(MemoryAddress, System.Globalization.NumberStyles.HexNumber, null, out var address) &&
                uint.TryParse(MemoryValue, System.Globalization.NumberStyles.HexNumber, null, out var value))
            {
                var bytes = new byte[]
                {
                    (byte)(value >> 24), (byte)(value >> 16),
                    (byte)(value >> 8), (byte)value
                };
                _dolphinService.WriteMemory(address, bytes);
                StatusMessage = $"Wrote 0x{value:X8} to 0x{address:X8}";
            }
            else
            {
                StatusMessage = "Invalid address or value format";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Write failed: {ex.Message}";
        }
    }

    // ═══ Room Warp Commands ═══

    [RelayCommand]
    private void WarpToDestination()
    {
        if (!_dolphinService.IsConnected)
        {
            StatusMessage = "Dolphin not connected";
            return;
        }

        if (SelectedWarp == null)
        {
            StatusMessage = "Select a destination first";
            return;
        }

        switch (StageExists(SelectedWarp.StageName))
        {
            case false:
                StatusMessage = $"Stage '{SelectedWarp.StageName}' isn't in the game files — warp refused (it would crash the game)";
                Logger.Warning("[warp] refused: stage {Stage} has no Stage.arc in the configured game folders", SelectedWarp.StageName);
                return;
            case null:
                Logger.Warning("[warp] no game folder configured in Settings — can't verify stage {Stage}", SelectedWarp.StageName);
                break;
        }

        try
        {
            byte room = 0;
            byte spawn = 0;
            byte.TryParse(WarpRoom, out room);
            byte.TryParse(WarpSpawn, out spawn);

            // Write stage name (8 bytes, null padded)
            var nameBytes = new byte[8];
            var stageBytes = System.Text.Encoding.ASCII.GetBytes(SelectedWarp.StageName);
            Array.Copy(stageBytes, nameBytes, Math.Min(stageBytes.Length, 8));

            // Write to NextStage fields to trigger transition
            _dolphinService.WriteMemory(GameMemoryAddresses.Stage.NextStageName.Address, nameBytes);
            _dolphinService.WriteMemory(GameMemoryAddresses.Stage.NextRoomNumber.Address, new byte[] { room });
            _dolphinService.Write(GameMemoryAddresses.Stage.NextSpawnId, (short)spawn);

            // Write layer override (0xFF = default)
            _dolphinService.Write(GameMemoryAddresses.Stage.NextLayer, (byte)0xFF);

            // Trigger the transition by writing fade type
            _dolphinService.WriteMemory(0x803C9D54, new byte[] { 0x01 });

            StatusMessage = $"Warping to {SelectedWarp.Name} (Room {room}, Spawn {spawn})";
            Logger.Information("Warp to {Stage} room={Room} spawn={Spawn}", SelectedWarp.StageName, room, spawn);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Warp failed: {ex.Message}";
        }
    }

    // ═══ Stat Editing Helpers ═══

    /// <summary>
    /// Parses <paramref name="input"/> as type <typeparamref name="T"/>, writes to the given
    /// <paramref name="address"/>, and updates StatusMessage. Returns the parsed value on success.
    /// </summary>
    private T? WriteStatValue<T>(string input, MemoryAddress<T> address, string label) where T : struct
    {
        if (!_dolphinService.IsConnected) return null;
        try
        {
            if (typeof(T) == typeof(ushort) && ushort.TryParse(input, out var us))
            {
                _dolphinService.Write(address, (T)(object)us);
                StatusMessage = $"Set {label} to {us}";
                return (T)(object)us;
            }

            if (typeof(T) == typeof(byte) && byte.TryParse(input, out var b))
            {
                _dolphinService.Write(address, (T)(object)b);
                StatusMessage = $"Set {label} to {b}";
                return (T)(object)b;
            }
        }
        catch (Exception ex) { StatusMessage = $"Failed to set {label}: {ex.Message}"; }
        return null;
    }

    // ═══ Stat Editing Commands ═══

    [RelayCommand]
    private void SetHealth() => WriteStatValue<ushort>(EditHealth, GameMemoryAddresses.Player.CurrentHealth, "health");

    [RelayCommand]
    private void SetMaxHealth() => WriteStatValue<ushort>(EditMaxHealth, GameMemoryAddresses.Player.MaxHealth, "max health");

    [RelayCommand]
    private void SetRupees()
    {
        if (!_dolphinService.IsConnected) return;
        if (!ushort.TryParse(EditRupees, out var target))
        {
            StatusMessage = $"Invalid rupees value: {EditRupees}";
            return;
        }
        // Through the HUD's pending-rupee counter so the on-screen count updates (and the game
        // clamps to the wallet size) — writing the save value directly leaves the HUD stale.
        GameActions.SetRupees(_dolphinService, target);
        StatusMessage = $"Set rupees to {target} (clamped to wallet size)";
    }

    [RelayCommand]
    private void SetMagic() => WriteStatValue<byte>(EditMagic, GameMemoryAddresses.Player.CurrentMagicMeter, "magic");

    [RelayCommand]
    private void SetMaxMagic() => WriteStatValue<byte>(EditMaxMagic, GameMemoryAddresses.Player.MaxMagicMeter, "max magic");

    [RelayCommand]
    private void SetArrows() => SetAmmo(EditArrows, GameMemoryAddresses.Inventory.MaxArrows, GameActions.SetArrows, "arrows");

    [RelayCommand]
    private void SetBombs() => SetAmmo(EditBombs, GameMemoryAddresses.Inventory.MaxBombs, GameActions.SetBombs, "bombs");

    // The max first (d_meter clamps to it), then the count through the HUD's pending counter so
    // the on-screen number updates — writing the save count directly left the HUD stale.
    private void SetAmmo(string input, MemoryAddress<byte> max, Func<IDolphinService, int, bool> set, string label)
    {
        if (!_dolphinService.IsConnected) return;
        if (!byte.TryParse(input, out var value) || value > 99)
        {
            StatusMessage = $"Invalid {label} value (0-99)";
            return;
        }
        try
        {
            _dolphinService.Write(max, value);
            set(_dolphinService, value);
            StatusMessage = $"Set {label} to {value}";
        }
        catch (Exception ex) { StatusMessage = $"Failed to set {label}: {ex.Message}"; }
    }

    [RelayCommand]
    private void MaxStats()
    {
        if (!_dolphinService.IsConnected)
        {
            StatusMessage = "Dolphin not connected";
            return;
        }

        try
        {
            _dolphinService.Write(GameMemoryAddresses.Player.MaxHealth, (ushort)80); // 20 hearts
            _dolphinService.Write(GameMemoryAddresses.Player.CurrentHealth, (ushort)80);
            GameActions.SetRupees(_dolphinService, 5000); // HUD path; game clamps to wallet size
            _dolphinService.Write(GameMemoryAddresses.Player.MaxMagicMeter, (byte)32);
            _dolphinService.Write(GameMemoryAddresses.Player.CurrentMagicMeter, (byte)32);
            _dolphinService.Write(GameMemoryAddresses.Inventory.MaxArrows, (byte)99);
            _dolphinService.Write(GameMemoryAddresses.Inventory.MaxBombs, (byte)99);
            GameActions.SetArrows(_dolphinService, 99); // HUD path, after the max
            GameActions.SetBombs(_dolphinService, 99);
            _dolphinService.Write(GameMemoryAddresses.Player.CurrentWallet, (byte)2); // Biggest wallet
            StatusMessage = "Maxed all stats";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Max stats failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
