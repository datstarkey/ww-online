using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.ViewModels;

/// <summary>One of the 21 main item slots. <see cref="ItemId"/> is what "give" puts there.</summary>
public record InventorySlotInfo(string Name, int Slot, byte ItemId)
{
    public uint Address => GameMemoryAddresses.Inventory.ItemSlots.Address + (uint)Slot;
}

/// <summary>An item-menu tile on the Room items page.</summary>
public partial class ItemTileViewModel : ObservableObject
{
    public InventorySlotInfo Slot { get; }
    public string Code { get; }
    public string Name => Slot.Name;

    [ObservableProperty] private bool _isOwned;

    public ItemTileViewModel(InventorySlotInfo slot, string code)
    {
        Slot = slot;
        Code = code;
    }
}

/// <summary>A chip on the Room page's items summary; <see cref="IsExtra"/> ones are outlined ("+ 2 songs").</summary>
public record ItemChip(string Text, bool IsExtra = false);

/// <summary>A pick-one option (sword, magic meter, wallet size...). <see cref="Value"/> is the model value.</summary>
public partial class ChoiceOption : ObservableObject
{
    public string Label { get; }
    public int Value { get; }
    /// <summary>The existing SetSword / SetShield commands take the option's name.</summary>
    public string Key { get; }

    [ObservableProperty] private bool _isSelected;

    public ChoiceOption(string label, int value, string? key = null)
    {
        Label = label;
        Value = value;
        Key = key ?? label;
    }
}

/// <summary>An on/off bit (a song, a pearl, a triforce shard, a charm).</summary>
public partial class FlagOption : ObservableObject
{
    public string Label { get; }
    public int Index { get; }
    public IBrush? Dot { get; }

    [ObservableProperty] private bool _isOn;

    public FlagOption(string label, int index, IBrush? dot = null)
    {
        Label = label;
        Index = index;
        Dot = dot;
    }
}

/// <summary>
/// The Room items page (a sub-page of Room): the item menu, equipment, hearts / magic, quest status
/// and capacities. With the SharedItems rule on it shows and edits the ROOM inventory (room owner
/// only, sent through <see cref="RoomInventorySyncService"/>; the server pushes it to everyone);
/// with it off it shows and edits this game. Read-only until Edit items (<see cref="Edit"/>).
/// The item / equipment commands are the Tools page's former Inventory editor, moved here unchanged.
/// </summary>
public partial class RoomItemsViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RoomItemsViewModel>();
    private bool _disposed;

    private readonly IDolphinService _dolphinService;
    private readonly RoomInventorySyncService _roomInventory;
    private readonly RoomSettingsService _roomSettings;
    private System.Timers.Timer? _localTimer;
    private bool? _lastRoomMode;

    // Local (shared items off) reads and writes touch save data: only while the game is in play.
    // PollLocal checks the gate each second (serialized by _pollLock); local edits check _sceneStable.
    private readonly SceneStabilityGate _scene = new(2);
    private readonly object _pollLock = new();
    private volatile bool _sceneStable;

    public ServerViewModel Server { get; }

    /// <summary>Read-only by default; Edit items → edit mode, Done → locked.</summary>
    public EditMode Edit { get; } = new();
    public bool IsEditing => Edit.IsEditing;

    /// <summary>Raised when the page's "← Room" link is pressed.</summary>
    public event Action? BackRequested;

    [ObservableProperty]
    private string? _statusMessage;

    // ═══ Inventory Management (moved from DebugToolsViewModel) ═══
    public List<InventorySlotInfo> InventorySlots { get; } = new()
    {
        new("Telescope", 0, ItemIDs.MainItems.Telescope),
        new("Sail", 1, ItemIDs.MainItems.Sail),
        new("Wind Waker", 2, ItemIDs.MainItems.WindWaker),
        new("Grappling Hook", 3, ItemIDs.MainItems.GrapplingHook),
        new("Spoils Bag", 4, ItemIDs.MainItems.SpoilsBag),
        new("Boomerang", 5, ItemIDs.MainItems.Boomerang),
        new("Deku Leaf", 6, ItemIDs.MainItems.DekuLeaf),
        new("Tingle Tuner", 7, ItemIDs.MainItems.TingleTuner),
        new("Picto Box", 8, ItemIDs.PictoBox.StandardPictoBox),
        new("Iron Boots", 9, ItemIDs.MainItems.IronBoots),
        new("Magic Armor", 10, ItemIDs.MainItems.MagicArmor),
        new("Bait Bag", 11, ItemIDs.MainItems.BaitBag),
        new("Bow", 12, ItemIDs.MainItems.Bow),
        new("Bombs", 13, ItemIDs.MainItems.Bombs),
        new("Bottle 1", 14, ItemIDs.BottleContents.EmptyBottle),
        new("Bottle 2", 15, ItemIDs.BottleContents.EmptyBottle),
        new("Bottle 3", 16, ItemIDs.BottleContents.EmptyBottle),
        new("Bottle 4", 17, ItemIDs.BottleContents.EmptyBottle),
        new("Delivery Bag", 18, ItemIDs.MainItems.DeliveryBag),
        new("Hookshot", 19, ItemIDs.MainItems.Hookshot),
        new("Skull Hammer", 20, ItemIDs.MainItems.SkullHammer),
    };

    private static readonly string[] TileCodes =
    [
        "TS", "SL", "WW", "GH", "SB", "BR", "DL", "TT", "PB", "IB", "MA", "BB", "BW", "BM",
        "B1", "B2", "B3", "B4", "DB", "HS", "SH",
    ];

    // ═══ Room items (SharedItems rule) ═══
    // With the rule on, the item editor edits the ROOM inventory (room owner only);
    // the server pushes the result to everyone's game. With it off it writes this game directly.

    /// <summary>False for non-owners while the room owns the items.</summary>
    [ObservableProperty]
    private bool _itemsEditable = true;

    /// <summary>Short note above the item tools ("" when editing this game only).</summary>
    [ObservableProperty]
    private string _roomItemsHint = "";

    /// <summary>True while connected with the SharedItems rule on: this page shows the room's items.</summary>
    [ObservableProperty]
    private bool _isRoomMode;

    // ═══ Display state ═══
    public ObservableCollection<ItemTileViewModel> Tiles { get; } = new();
    public ObservableCollection<ChoiceOption> SwordOptions { get; } = new();
    public ObservableCollection<ChoiceOption> ShieldOptions { get; } = new();
    public ObservableCollection<ChoiceOption> MagicOptions { get; } = new();
    public ObservableCollection<ChoiceOption> WalletOptions { get; } = new();
    public ObservableCollection<ChoiceOption> QuiverOptions { get; } = new();
    public ObservableCollection<ChoiceOption> BombBagOptions { get; } = new();
    public ObservableCollection<FlagOption> Songs { get; } = new();
    public ObservableCollection<FlagOption> Pearls { get; } = new();
    public ObservableCollection<FlagOption> Shards { get; } = new();
    public ObservableCollection<FlagOption> Upgrades { get; } = new();

    /// <summary>One entry per heart container, for the heart row.</summary>
    public ObservableCollection<int> HeartIcons { get; } = new();

    /// <summary>Owned-item chips for the Room page's summary card.</summary>
    public ObservableCollection<ItemChip> OwnedChips { get; } = new();

    [ObservableProperty] private bool _hasSnapshot;
    [ObservableProperty] private string _ownedCountText = "";
    [ObservableProperty] private string _swordName = "None";
    [ObservableProperty] private string _shieldName = "None";
    [ObservableProperty] private int _maxHearts;
    [ObservableProperty] private string _maxHeartsText = "—";
    [ObservableProperty] private string _heartsText = "";
    [ObservableProperty] private string _magicText = "";
    [ObservableProperty] private string _shardText = "0 of 8";
    [ObservableProperty] private bool _hasOwnedChips;
    [ObservableProperty] private byte _walletSize;

    private RoomInventory? _snapshot;

    public string PageTitle => IsRoomMode ? "Room items" : "Your items";
    public string PageSubtitle => IsRoomMode
        ? "Anything a player picks up is added for the whole room. The room owner can also add or remove items in Edit mode."
        : "Shared items is off, so each player keeps their own items. Edits here change your own game only.";
    public string EditBannerTitle => IsRoomMode ? "Editing room items." : "Editing your items.";
    public string EditBannerText => IsRoomMode
        ? "Removing an item takes it away from every player."
        : "Shared items is off, so this only changes your own game.";
    public string SyncedText => IsRoomMode
        ? $"Synced to {Server.Players.Count} player{(Server.Players.Count == 1 ? "" : "s")}"
        : "Your game only";
    public string WaitingText => IsRoomMode
        ? "Waiting for the room's items. They appear once the room owner's game is attached."
        : "Attach to Dolphin on the Dolphin page to see your items.";

    partial void OnIsRoomModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(EditBannerTitle));
        OnPropertyChanged(nameof(EditBannerText));
        OnPropertyChanged(nameof(SyncedText));
        OnPropertyChanged(nameof(WaitingText));
        OnPropertyChanged(nameof(ShowViewOnly));
        OnPropertyChanged(nameof(LegendText));
    }

    public string LegendText => IsRoomMode ? "Room has it" : "You have it";

    partial void OnItemsEditableChanged(bool value)
    {
        Edit.CanEdit = value;
        OnPropertyChanged(nameof(ShowViewOnly));
    }

    public RoomItemsViewModel(
        IDolphinService dolphinService,
        RoomInventorySyncService roomInventory,
        RoomSettingsService roomSettings,
        ServerViewModel server)
    {
        _dolphinService = dolphinService;
        _roomInventory = roomInventory;
        _roomSettings = roomSettings;
        Server = server;

        foreach (var slot in InventorySlots)
            Tiles.Add(new ItemTileViewModel(slot, TileCodes[slot.Slot]));
        SwordOptions.Add(new ChoiceOption("None", ItemIDs.Swords.NoSword));
        SwordOptions.Add(new ChoiceOption("Hero's Sword", ItemIDs.Swords.HerosSword));
        SwordOptions.Add(new ChoiceOption("Master Sword", ItemIDs.Swords.MasterSword));
        SwordOptions.Add(new ChoiceOption("Master Sword (half power)", ItemIDs.Swords.MasterSwordHalf, "Master Sword (Half)"));
        SwordOptions.Add(new ChoiceOption("Master Sword (full power)", ItemIDs.Swords.MasterSwordFull, "Master Sword (Full)"));
        ShieldOptions.Add(new ChoiceOption("None", ItemIDs.Shields.NoShield));
        ShieldOptions.Add(new ChoiceOption("Hero's Shield", ItemIDs.Shields.HerosShield));
        ShieldOptions.Add(new ChoiceOption("Mirror Shield", ItemIDs.Shields.MirrorShield));
        MagicOptions.Add(new ChoiceOption("None", 0));
        MagicOptions.Add(new ChoiceOption("Normal", 16));
        MagicOptions.Add(new ChoiceOption("Double", RoomInventory.MaxMagicLimit));
        WalletOptions.Add(new ChoiceOption("200", 0));
        WalletOptions.Add(new ChoiceOption("1000", 1));
        WalletOptions.Add(new ChoiceOption("5000", 2));
        foreach (var n in new[] { 30, 60, 99 })
        {
            QuiverOptions.Add(new ChoiceOption(n.ToString(), n));
            BombBagOptions.Add(new ChoiceOption(n.ToString(), n));
        }
        // mTact bits 0-5 (item_func_tact_song1..6), mSymbol bits 0-2 (dSymbol_NAYRU/DIN/FARORE_e)
        string[] songs = ["Wind's Requiem", "Ballad of Gales", "Command Melody", "Earth God's Lyric", "Wind God's Aria", "Song of Passing"];
        for (int i = 0; i < songs.Length; i++) Songs.Add(new FlagOption(songs[i], i));
        Pearls.Add(new FlagOption("Din", 1, Brush("#FF7A59")));
        Pearls.Add(new FlagOption("Farore", 2, Brush("#5BD17A")));
        Pearls.Add(new FlagOption("Nayru", 0, Brush("#5B9DFF")));
        for (int i = 0; i < 8; i++) Shards.Add(new FlagOption($"{i + 1} shards", i));
        Upgrades.Add(new FlagOption("Power Bracelets", 0));
        Upgrades.Add(new FlagOption("Pirate's Charm", 1));
        Upgrades.Add(new FlagOption("Hero's Charm", 2));

        Edit.CanEdit = ItemsEditable;
        Edit.PropertyChanged += OnEditPropertyChanged;
        Server.Players.CollectionChanged += OnPlayersChanged;
        _roomSettings.Changed += OnRoomSettingsChanged;
        _roomInventory.RoomChanged += OnRoomInventoryChanged;
        _dolphinService.ConnectionChanged += OnDolphinConnectionChanged;
        OnRoomItemsModeChanged();

        // With shared items off, the page mirrors this game's own items (1 Hz read, nothing written).
        _localTimer = new System.Timers.Timer(1000) { AutoReset = true };
        _localTimer.Elapsed += (_, _) => _ = Task.Run(PollLocal);
        _localTimer.Start();
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    /// <summary>"Edit items" is offered while locked, to whoever may edit (the owner, or anyone for their own game).</summary>
    public bool ShowEditButton => Edit.CanEdit && !Edit.IsEditing;

    /// <summary>Room mode, not the owner: explain why there's no Edit button.</summary>
    public bool ShowViewOnly => IsRoomMode && !ItemsEditable;

    private void OnEditPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditMode.IsEditing)) OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(ShowEditButton));
    }

    private void OnPlayersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(SyncedText));

    private void OnRoomItemsModeChanged() => Dispatcher.UIThread.Post(() =>
    {
        bool roomMode = _roomInventory.IsRoomMode;
        ItemsEditable = !roomMode || _roomSettings.IsOwner;
        RoomItemsHint = !roomMode ? ""
            : _roomSettings.IsOwner ? "Room items: changes apply to everyone in the room"
            : "Only the room owner can change room items";
        IsRoomMode = roomMode;
        // Room ↔ own game changes what an edit touches: never carry edit mode across.
        if (_lastRoomMode != roomMode)
        {
            _lastRoomMode = roomMode;
            Edit.End();
            SetSnapshot(roomMode ? _roomInventory.Room : null);
        }
        else if (roomMode)
        {
            SetSnapshot(_roomInventory.Room);
        }
    });

    private void OnRoomSettingsChanged(RoomSettings _) => OnRoomItemsModeChanged();
    private void OnRoomInventoryChanged(RoomInventory? _) => OnRoomItemsModeChanged();

    private void OnDolphinConnectionChanged(object? sender, bool connected)
    {
        if (!connected) Dispatcher.UIThread.Post(() => { if (!IsRoomMode) SetSnapshot(null); });
    }

    private void PollLocal()
    {
        if (!Monitor.TryEnter(_pollLock)) return; // a poll is already running
        try
        {
            if (_roomInventory.IsRoomMode || !_dolphinService.IsConnected)
            {
                _sceneStable = false;
                _scene.Reset();
                return;
            }
            // Title screen / file select run on an empty default save; loading is mid-change.
            _sceneStable = _scene.Check(_dolphinService);
            if (!_sceneStable) return;
            var local = RoomInventoryMemory.Read(_dolphinService);
            if (local == null) return;
            Dispatcher.UIThread.Post(() => { if (!IsRoomMode) SetSnapshot(local); });
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "[items] local item read failed");
        }
        finally
        {
            Monitor.Exit(_pollLock);
        }
    }

    /// <summary>Local edits write save data: Dolphin attached and the game in play (not title / loading).</summary>
    private bool LocalGameReady()
    {
        if (!_dolphinService.IsConnected)
        {
            StatusMessage = "Dolphin not connected";
            return false;
        }
        if (!_sceneStable)
        {
            StatusMessage = "Your game isn't in play (title screen or loading) — try again in a moment";
            return false;
        }
        return true;
    }

    /// <summary>Refresh every display property from <paramref name="inv"/> (null = nothing known yet).</summary>
    private void SetSnapshot(RoomInventory? inv)
    {
        if (inv != null && _snapshot != null && inv.ContentEquals(_snapshot)) return;
        _snapshot = inv?.Clone();
        bool known = inv != null;
        HasSnapshot = known;
        inv ??= new RoomInventory();

        int owned = 0;
        foreach (var tile in Tiles)
        {
            tile.IsOwned = inv.Items[tile.Slot.Slot] != RoomInventory.NoItem;
            if (tile.IsOwned) owned++;
        }
        OwnedCountText = $"{owned} of {RoomInventory.SlotCount}";

        // Nothing known yet: no option is shown as picked (not even "None").
        foreach (var o in SwordOptions) o.IsSelected = known && o.Value == inv.EquippedSword;
        foreach (var o in ShieldOptions) o.IsSelected = known && o.Value == inv.EquippedShield;
        SwordName = inv.EquippedSword == RoomInventory.NoItem ? "None" : ItemIDs.GetItemName(inv.EquippedSword);
        ShieldName = inv.EquippedShield == RoomInventory.NoItem ? "None" : ItemIDs.GetItemName(inv.EquippedShield);

        MaxHearts = inv.MaxHealth / 4;
        MaxHeartsText = known ? MaxHearts.ToString() : "—";
        int pieces = inv.MaxHealth % 4;
        HeartsText = !known ? "" : $"{MaxHearts} heart{(MaxHearts == 1 ? "" : "s")} max{(pieces > 0 ? $" + {pieces}/4" : "")}";
        while (HeartIcons.Count > Math.Min(MaxHearts, 20)) HeartIcons.RemoveAt(HeartIcons.Count - 1);
        while (HeartIcons.Count < Math.Min(MaxHearts, 20)) HeartIcons.Add(HeartIcons.Count);

        var magic = MagicOptions.Last(o => inv.MaxMagic >= o.Value); // options[0] is 0, so one always matches
        foreach (var o in MagicOptions) o.IsSelected = known && o == magic;
        MagicText = inv.MaxMagic == 0 ? "Magic: none" : inv.MaxMagic >= RoomInventory.MaxMagicLimit ? "Magic: double" : "Magic: normal";

        foreach (var o in WalletOptions) o.IsSelected = known && o.Value == inv.WalletSize;
        foreach (var o in QuiverOptions) o.IsSelected = o.Value == inv.MaxArrows;
        foreach (var o in BombBagOptions) o.IsSelected = o.Value == inv.MaxBombs;
        WalletSize = inv.WalletSize;

        foreach (var s in Songs) s.IsOn = (inv.Songs & (1 << s.Index)) != 0;
        foreach (var p in Pearls) p.IsOn = (inv.Pearls & (1 << p.Index)) != 0;
        int shards = BitOperations.PopCount(inv.TriforceShards);
        foreach (var t in Shards) t.IsOn = (inv.TriforceShards & (1 << t.Index)) != 0;
        ShardText = $"{shards} of 8";
        Upgrades[0].IsOn = (inv.PowerBracelets & RoomInventory.BraceletMask) != 0;
        Upgrades[1].IsOn = (inv.PiratesCharm & RoomInventory.CharmMask) != 0;
        Upgrades[2].IsOn = (inv.HerosCharm & RoomInventory.CharmMask) != 0;

        // Summary chips: owned items (bottles collapsed), then the song count.
        OwnedChips.Clear();
        int bottles = 0;
        foreach (var tile in Tiles)
        {
            if (!tile.IsOwned) continue;
            if (RoomInventory.IsBottleSlot(tile.Slot.Slot)) { bottles++; continue; }
            OwnedChips.Add(new ItemChip(tile.Name));
        }
        if (bottles > 0) OwnedChips.Add(new ItemChip(bottles == 1 ? "Bottle" : $"{bottles} Bottles"));
        int songCount = BitOperations.PopCount((uint)(inv.Songs & RoomInventory.SongMask));
        if (songCount > 0) OwnedChips.Add(new ItemChip($"+ {songCount} song{(songCount == 1 ? "" : "s")}", IsExtra: true));
        HasOwnedChips = OwnedChips.Count > 0;
    }

    [RelayCommand]
    private void StartEdit()
    {
        if (!Edit.Begin())
            StatusMessage = IsRoomMode ? "Only the room owner can change room items" : null;
    }

    [RelayCommand]
    private void StopEdit() => Edit.End();

    [RelayCommand]
    private void Back()
    {
        Edit.End();
        BackRequested?.Invoke();
    }

    /// <summary>Leaving the page (or the Room page) always locks it again.</summary>
    public void Lock() => Edit.End();

    /// <summary>
    /// If the room owns the items, apply <paramref name="edit"/> to a copy of the room state and
    /// send it (owner only) — returns true (handled). Returns false to fall through to a local write.
    /// </summary>
    private async Task<bool> TryEditRoomAsync(string what, Action<RoomInventory> edit)
    {
        if (!_roomInventory.IsRoomMode) return false;
        if (!_roomSettings.IsOwner)
        {
            StatusMessage = "Only the room owner can change room items";
            return true;
        }
        try
        {
            var room = await _roomInventory.GetEditableRoomAsync();
            edit(room);
            StatusMessage = await _roomInventory.SetRoomAsync(room, what)
                ? $"Room items: {what}"
                : $"Room items: '{what}' rejected (invalid)";
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[items] room edit '{What}' failed", what);
            StatusMessage = $"Room edit failed: {ex.Message}";
        }
        return true;
    }

    /// <summary>
    /// Quest status / hearts / magic / capacity edits: the room (owner) while items are shared,
    /// else this game — read, change the one field, and write back only what differs
    /// (<see cref="RoomInventoryMemory.Apply"/>, the same writer the sync uses).
    /// </summary>
    private async Task EditAsync(string what, Action<RoomInventory> edit)
    {
        if (!Edit.IsEditing) return;
        if (await TryEditRoomAsync(what, edit)) return;
        if (!LocalGameReady()) return;
        try
        {
            var local = RoomInventoryMemory.Read(_dolphinService);
            if (local == null)
            {
                StatusMessage = "Couldn't read your items right now — try again";
                return;
            }
            var edited = local.Clone();
            edit(edited);
            edited.Normalize();
            var changes = new List<string>();
            var after = RoomInventoryMemory.Apply(_dolphinService, edited, local, applySword: false, applyShield: false, changes);
            SetSnapshot(after);
            StatusMessage = changes.Count > 0 ? $"Your items: {what}" : $"Your items: {what} (no change)";
            if (changes.Count > 0)
                Logger.Information("[items] local edit '{What}': {Changes}", what, string.Join(", ", changes));
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[items] local edit '{What}' failed", what);
            StatusMessage = $"Edit failed: {ex.Message}";
        }
    }

    // ═══ Inventory Management Commands (moved from DebugToolsViewModel) ═══

    [RelayCommand]
    private async Task ToggleItem(InventorySlotInfo? slot)
    {
        if (slot == null || !Edit.IsEditing) return;
        if (await TryEditRoomAsync($"toggle {slot.Name}", room =>
            {
                bool had = room.Items[slot.Slot] != RoomInventory.NoItem;
                room.Items[slot.Slot] = had ? RoomInventory.NoItem : slot.ItemId;
                room.ItemGetFlags[slot.Slot] = had ? (byte)0 : (byte)(room.ItemGetFlags[slot.Slot] | 0x01);
            }))
            return;
        if (!LocalGameReady()) return;

        try
        {
            var current = _dolphinService.ReadMemory(slot.Address, 1);
            if (current != null && current[0] != 0xFF)
            {
                _dolphinService.WriteMemory(slot.Address, new byte[] { 0xFF });
                StatusMessage = $"Removed {slot.Name}";
            }
            else
            {
                _dolphinService.WriteMemory(slot.Address, new byte[] { slot.ItemId });
                StatusMessage = $"Gave {slot.Name}";
            }
            PollLocal();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Toggle failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GiveAllItems()
    {
        if (!Edit.IsEditing) return;
        // Room: items, swords, shields, bracelets and capacities — never ammo counts (per-player).
        if (await TryEditRoomAsync("give all", room =>
            {
                foreach (var slot in InventorySlots)
                {
                    if (RoomInventory.UpgradeRank(slot.Slot, slot.ItemId) > RoomInventory.UpgradeRank(slot.Slot, room.Items[slot.Slot]))
                        room.Items[slot.Slot] = slot.ItemId;
                    room.ItemGetFlags[slot.Slot] |= 0x01;
                }
                room.Swords |= RoomInventory.SwordMask;
                room.Shields |= RoomInventory.ShieldMask;
                room.PowerBracelets |= RoomInventory.BraceletMask;
                room.EquippedSword = RoomInventory.MasterSword3;
                room.EquippedShield = RoomInventory.MirrorShield;
                room.MaxArrows = Math.Max(room.MaxArrows, RoomInventory.MaxAmmoLimit);
                room.MaxBombs = Math.Max(room.MaxBombs, RoomInventory.MaxAmmoLimit);
            }))
            return;

        if (!LocalGameReady()) return;

        try
        {
            foreach (var slot in InventorySlots)
                _dolphinService.WriteMemory(slot.Address, new byte[] { slot.ItemId });

            // Also give sword and shield
            EquipSwapGuard.TryWrite(_dolphinService, GameMemoryAddresses.Player.CurrentSword, ItemIDs.Swords.MasterSwordFull);
            _dolphinService.Write(GameMemoryAddresses.Inventory.SwordsBitfield, RoomInventory.SwordMask);
            EquipSwapGuard.TryWrite(_dolphinService, GameMemoryAddresses.Player.CurrentShield, ItemIDs.Shields.MirrorShield);
            _dolphinService.Write(GameMemoryAddresses.Inventory.ShieldsBitfield, RoomInventory.ShieldMask);
            _dolphinService.WriteMemory(GameMemoryAddresses.Player.PowerBracelets.Address, new byte[] { ItemIDs.Bracelets.PowerBracelets });
            _dolphinService.Write(GameMemoryAddresses.Inventory.PowerBraceletsBitfield, RoomInventory.BraceletMask);

            // Max out ammo
            _dolphinService.WriteMemory(GameMemoryAddresses.Inventory.MaxArrows.Address, new byte[] { 99 });
            _dolphinService.WriteMemory(GameMemoryAddresses.Inventory.MaxBombs.Address, new byte[] { 99 });
            GameActions.SetArrows(_dolphinService, 99); // HUD path, after the max
            GameActions.SetBombs(_dolphinService, 99);

            StatusMessage = "Gave all items + equipment";
            PollLocal();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Give all failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearInventory()
    {
        if (!Edit.IsEditing) return;
        if (await TryEditRoomAsync("clear items", room =>
            {
                room.Items = RoomInventory.NewEmptySlots();
                room.ItemGetFlags = new byte[RoomInventory.SlotCount];
            }))
            return;

        if (!LocalGameReady()) return;

        try
        {
            foreach (var slot in InventorySlots)
                _dolphinService.WriteMemory(slot.Address, new byte[] { 0xFF });

            StatusMessage = "Cleared inventory";
            PollLocal();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clear failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SetSword(string? swordType)
    {
        if (swordType == null || !Edit.IsEditing) return;

        byte id = swordType switch
        {
            "None" => 0xFF,
            "Hero's Sword" => ItemIDs.Swords.HerosSword,
            "Master Sword" => ItemIDs.Swords.MasterSword,
            "Master Sword (Half)" => ItemIDs.Swords.MasterSwordHalf,
            "Master Sword (Full)" => ItemIDs.Swords.MasterSwordFull,
            _ => 0xFF
        };
        if (await TryEditRoomAsync($"equip sword: {swordType}", room =>
            {
                room.EquippedSword = id;
                room.Swords |= RoomInventory.SwordBit(id);
            }))
            return;
        if (!LocalGameReady()) return;

        StatusMessage = EquipSwapGuard.TryWrite(_dolphinService, GameMemoryAddresses.Player.CurrentSword, id)
            ? $"Set sword: {swordType}"
            : "Sword not set (a puppet's equipment swap was in progress) — try again";
        PollLocal();
    }

    [RelayCommand]
    private async Task SetShield(string? shieldType)
    {
        if (shieldType == null || !Edit.IsEditing) return;

        byte id = shieldType switch
        {
            "None" => ItemIDs.Shields.NoShield,
            "Hero's Shield" => ItemIDs.Shields.HerosShield,
            "Mirror Shield" => ItemIDs.Shields.MirrorShield,
            _ => ItemIDs.Shields.NoShield
        };
        if (await TryEditRoomAsync($"equip shield: {shieldType}", room =>
            {
                room.EquippedShield = id;
                room.Shields |= RoomInventory.ShieldBit(id);
            }))
            return;
        if (!LocalGameReady()) return;

        StatusMessage = EquipSwapGuard.TryWrite(_dolphinService, GameMemoryAddresses.Player.CurrentShield, id)
            ? $"Set shield: {shieldType}"
            : "Shield not set (a puppet's equipment swap was in progress) — try again";
        PollLocal();
    }

    // ═══ Quest status, hearts, magic, capacity (RoomInventory fields) ═══

    [RelayCommand]
    private Task ToggleSong(FlagOption? song) => song == null ? Task.CompletedTask
        : EditAsync($"{(song.IsOn ? "remove" : "add")} {song.Label}", inv => inv.Songs ^= (byte)(1 << song.Index));

    [RelayCommand]
    private Task TogglePearl(FlagOption? pearl) => pearl == null ? Task.CompletedTask
        : EditAsync($"{(pearl.IsOn ? "remove" : "add")} {pearl.Label}'s Pearl", inv => inv.Pearls ^= (byte)(1 << pearl.Index));

    /// <summary>Shards fill in order: picking shard N sets N shards (picking the last one set clears it).</summary>
    [RelayCommand]
    private Task PickShard(FlagOption? shard)
    {
        if (shard == null) return Task.CompletedTask;
        int current = BitOperations.PopCount(_snapshot?.TriforceShards ?? 0);
        int want = current == shard.Index + 1 ? shard.Index : shard.Index + 1;
        return EditAsync($"triforce shards: {want}", inv => inv.TriforceShards = (byte)((1 << want) - 1));
    }

    [RelayCommand]
    private Task ToggleUpgrade(FlagOption? up)
    {
        if (up == null) return Task.CompletedTask;
        string what = $"{(up.IsOn ? "remove" : "add")} {up.Label}";
        return up.Index switch
        {
            0 => EditAsync(what, inv => inv.PowerBracelets ^= RoomInventory.BraceletMask),
            1 => EditAsync(what, inv => inv.PiratesCharm ^= RoomInventory.CharmMask),
            _ => EditAsync(what, inv => inv.HerosCharm ^= RoomInventory.CharmMask),
        };
    }

    [RelayCommand]
    private Task HeartUp() => EditAsync("max hearts +1", inv =>
        inv.MaxHealth = (ushort)Math.Min(RoomInventory.MaxHealthLimit, inv.MaxHealth + 4));

    [RelayCommand]
    private Task HeartDown() => EditAsync("max hearts -1", inv =>
    {
        if (inv.MaxHealth > 12) inv.MaxHealth = (ushort)Math.Max(12, inv.MaxHealth - 4);
    });

    [RelayCommand]
    private Task SetMagic(ChoiceOption? o) => o == null ? Task.CompletedTask
        : EditAsync($"magic meter: {o.Label}", inv => inv.MaxMagic = (byte)o.Value);

    [RelayCommand]
    private Task SetWallet(ChoiceOption? o) => o == null ? Task.CompletedTask
        : EditAsync($"wallet: {o.Label}", inv => inv.WalletSize = (byte)o.Value);

    [RelayCommand]
    private Task SetQuiver(ChoiceOption? o) => o == null ? Task.CompletedTask
        : EditAsync($"quiver: {o.Label}", inv => inv.MaxArrows = (byte)o.Value);

    [RelayCommand]
    private Task SetBombBag(ChoiceOption? o) => o == null ? Task.CompletedTask
        : EditAsync($"bomb bag: {o.Label}", inv => inv.MaxBombs = (byte)o.Value);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Edit.PropertyChanged -= OnEditPropertyChanged;
        Server.Players.CollectionChanged -= OnPlayersChanged;
        _roomSettings.Changed -= OnRoomSettingsChanged;
        _roomInventory.RoomChanged -= OnRoomInventoryChanged;
        _dolphinService.ConnectionChanged -= OnDolphinConnectionChanged;
        _localTimer?.Stop();
        _localTimer?.Dispose();
        _localTimer = null;
    }
}
