using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.ViewModels;

/// <summary>
/// The Room page (nav "Room"; class name kept from the old Dashboard). Composite: it surfaces the
/// existing Server / GameState / GameLog / DebugTools view-models plus the Room items and Story
/// flags sub-pages (<see cref="Items"/>, <see cref="Flags"/>), and owns the room rules card. Disconnected it shows the connect / host
/// form; connected it shows the room: rules, players, wallet, room items, and for non-owners a
/// "What's happening" feed.
///
/// The room owner sees everything read-only until Edit room (<see cref="Edit"/>); rule toggles and
/// presets only send while editing. Edit mode drops when ownership moves or the connection goes.
/// Navigation to other pages is raised through <see cref="NavigateRequested"/>, which
/// <see cref="MainViewModel"/> subscribes to.
/// </summary>
public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    public ServerViewModel Server { get; }
    public GameStateViewModel Live { get; }
    public GameLogViewModel Activity { get; }
    public DebugToolsViewModel Tools { get; }
    public RoomItemsViewModel Items { get; }
    public RoomFlagsViewModel Flags { get; }

    private readonly SharedWalletService _wallet;
    private readonly RoomSettingsService _room;
    private bool _disposed;

    /// <summary>Owner-only edit lock for the room rules.</summary>
    public EditMode Edit { get; } = new();
    public bool IsEditing => Edit.IsEditing;
    public bool IsViewing => Edit.IsViewing;
    public bool CanEdit => Edit.CanEdit;

    /// <summary>Owner, locked: offer Edit room (and show the "Locked" hint).</summary>
    public bool ShowEditButton => Edit.CanEdit && !Edit.IsEditing;

    /// <summary>The room's shared rupee total ("—" until joined, "OFF" when the rule is off).</summary>
    [ObservableProperty]
    private string _sharedWalletText = "—";

    // Room rules — editable only by the room owner (in edit mode); everyone else sees them read-only.
    [ObservableProperty] private bool _isOwner;
    [ObservableProperty] private bool _sharedWalletOn = true;
    [ObservableProperty] private bool _sharedWorldOn = true;
    [ObservableProperty] private bool _sharedItemsOn = true;
    [ObservableProperty] private bool _sharedStoryOn = true;
    [ObservableProperty] private string _roomOwnerText = "";
    [ObservableProperty] private string _ownerName = "";

    // Which preset the current rules match (the preset buttons are the room owner's shortcut).
    [ObservableProperty] private bool _isFullSyncPreset = true;
    [ObservableProperty] private bool _isCoopPreset;
    [ObservableProperty] private bool _isCustomPreset;
    [ObservableProperty] private string _presetText = "Full sync";
    [ObservableProperty] private string _presetNote = "Everything is shared: wallet, world, items and story.";
    private bool _applyingServerRules;

    /// <summary>Show the Room items sub-page instead of the room overview.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOverview))]
    private bool _showItemsPage;

    /// <summary>Show the Story flags sub-page instead of the room overview.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOverview))]
    private bool _showFlagsPage;

    /// <summary>The room overview (or the connect form): no sub-page open.</summary>
    public bool ShowOverview => !ShowItemsPage && !ShowFlagsPage;

    // Header
    [ObservableProperty] private string _roomAddress = "";
    [ObservableProperty] private string _roomLabel = "ROOM";
    [ObservableProperty] private string _roomTitle = "Room";
    [ObservableProperty] private string _copyInviteText = "Copy invite";

    // Wallet card
    [ObservableProperty] private string _walletAmountText = "—";
    [ObservableProperty] private string _walletMaxText = "";
    [ObservableProperty] private double _walletPercent;
    [ObservableProperty] private string _walletStateText = "Shared";
    [ObservableProperty] private string _walletNote = "";

    public string OnlineText => $"{Server.Players.Count} online";
    public bool IsWaitingForPlayers => Server.Players.Count <= 1;
    public string LeaveText => Server.IsHosting ? "Close room" : "Leave room";
    public string ItemsLinkText => IsOwner && SharedItemsOn ? (IsEditing ? "Edit items" : "View items") : "View items";
    public string ItemsCardTitle => SharedItemsOn ? "Room items" : "Your items";
    public string FlagsLinkText => Flags.Edit.CanEdit ? "Edit flags" : "View flags";
    public string FlagsCardTitle => Flags.IsRoomMode ? "Story flags" : "Your story flags";
    public string OwnerBannerName => string.IsNullOrEmpty(OwnerName) ? "The room owner" : OwnerName;

    /// <summary>Raised when the page wants the shell to switch screens.</summary>
    public event Action<ViewName>? NavigateRequested;

    /// <summary>Raised with the invite address; the view puts it on the clipboard.</summary>
    public event Action<string>? CopyRequested;

    public DashboardViewModel(
        ServerViewModel server,
        GameStateViewModel live,
        GameLogViewModel activity,
        DebugToolsViewModel tools,
        RoomItemsViewModel items,
        RoomFlagsViewModel flags,
        SharedWalletService wallet,
        RoomSettingsService room)
    {
        Server = server;
        Live = live;
        Activity = activity;
        Tools = tools;
        Items = items;
        Flags = flags;
        _wallet = wallet;
        _room = room;
        _wallet.TotalChanged += OnWalletTotalChanged;
        _room.Changed += OnRoomRulesChanged;
        Edit.PropertyChanged += OnEditPropertyChanged;
        Server.PropertyChanged += OnServerPropertyChanged;
        Server.Players.CollectionChanged += OnPlayersChanged;
        Server.Disconnected += OnDisconnected;
        Items.PropertyChanged += OnItemsPropertyChanged;
        Items.BackRequested += OnItemsBack;
        Flags.PropertyChanged += OnFlagsPropertyChanged;
        Flags.Edit.PropertyChanged += OnFlagsEditPropertyChanged;
        Flags.BackRequested += OnFlagsBack;
        Live.PropertyChanged += OnLivePropertyChanged;
        OnWalletTotalChanged(_wallet.Total);
        OnRoomRulesChanged(_room.Current);
        UpdateAddress();
    }

    private void OnEditPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditMode.IsEditing):
                OnPropertyChanged(nameof(IsEditing));
                OnPropertyChanged(nameof(IsViewing));
                OnPropertyChanged(nameof(ItemsLinkText));
                OnPropertyChanged(nameof(ShowEditButton));
                break;
            case nameof(EditMode.CanEdit):
                OnPropertyChanged(nameof(CanEdit));
                OnPropertyChanged(nameof(ShowEditButton));
                break;
        }
    }

    private void OnServerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ServerViewModel.ConnectionState):
                // Ownership is per connection: don't carry the last room's IsOwner into a new
                // connection (it would offer Edit room until that room's rules arrive).
                IsOwner = _room.IsOwner;
                UpdateCanEdit();
                UpdateAddress();
                OnPropertyChanged(nameof(LeaveText));
                break;
            case nameof(ServerViewModel.ServerHost):
            case nameof(ServerViewModel.ServerPort):
                UpdateAddress();
                break;
        }
    }

    private void OnPlayersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(OnlineText));
        OnPropertyChanged(nameof(IsWaitingForPlayers));
        UpdateOwnerRows();
    }

    private void OnDisconnected()
    {
        Edit.End();
        Items.Lock();
        Flags.Lock();
        ShowItemsPage = false;
        ShowFlagsPage = false;
    }

    private void OnItemsBack() => ShowItemsPage = false;

    private void OnFlagsBack() => ShowFlagsPage = false;

    private void OnFlagsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomFlagsViewModel.IsRoomMode))
            OnPropertyChanged(nameof(FlagsCardTitle));
    }

    private void OnFlagsEditPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditMode.CanEdit))
            OnPropertyChanged(nameof(FlagsLinkText));
    }

    private void OnItemsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RoomItemsViewModel.WalletSize) or nameof(RoomItemsViewModel.HasSnapshot))
            UpdateWallet();
    }

    private void OnLivePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!SharedWalletOn && e.PropertyName is nameof(GameStateViewModel.Rupees) or nameof(GameStateViewModel.HasData))
            UpdateWallet();
    }

    partial void OnIsOwnerChanged(bool value)
    {
        UpdateCanEdit();
        OnPropertyChanged(nameof(ItemsLinkText));
    }

    partial void OnOwnerNameChanged(string value) => OnPropertyChanged(nameof(OwnerBannerName));

    private void UpdateCanEdit() => Edit.CanEdit = IsOwner && Server.IsConnected;

    private void OnWalletTotalChanged(int? total) => Dispatcher.UIThread.Post(UpdateWalletText);

    private void UpdateWalletText()
    {
        SharedWalletText = !_room.Current.SharedWallet ? "OFF" : _wallet.Total?.ToString() ?? "—";
        UpdateWallet();
    }

    private void UpdateWallet()
    {
        bool shared = _room.Current.SharedWallet;
        int? amount = shared ? _wallet.Total : (Live.HasData ? Live.Rupees : null);
        WalletAmountText = amount?.ToString("N0") ?? "—";
        int? capacity = Items.HasSnapshot ? SharedWalletService.WalletCapacity(Items.WalletSize) : null;
        WalletMaxText = capacity is int cap ? $"/ {cap:N0}" : "";
        WalletPercent = amount is int a && capacity is int c && c > 0 ? Math.Clamp(100.0 * a / c, 0, 100) : 0;
        WalletStateText = shared ? "Shared" : "Off: per player";
        WalletNote = shared
            ? "Anyone's rupees count for everyone. Every in-game counter matches this."
            : "Shared wallet is off, so this is your own purse.";
    }

    private void OnRoomRulesChanged(RoomSettings rules) => Dispatcher.UIThread.Post(() =>
    {
        _applyingServerRules = true;
        SharedWalletOn = rules.SharedWallet;
        SharedWorldOn = rules.SharedWorld;
        SharedItemsOn = rules.SharedItems;
        SharedStoryOn = rules.SharedStory;
        UpdatePreset(rules.MatchingPreset());
        IsOwner = _room.IsOwner;
        OwnerName = rules.OwnerName ?? "";
        RoomOwnerText = string.IsNullOrEmpty(rules.OwnerName) ? "" : IsOwner ? "You own this room" : $"Room owner: {rules.OwnerName}";
        RoomTitle = IsOwner ? "Your room" : string.IsNullOrEmpty(rules.OwnerName) ? "Room" : $"{rules.OwnerName}'s room";
        _applyingServerRules = false;
        UpdateOwnerRows();
        UpdateCanEdit();
        UpdateWalletText();
        OnPropertyChanged(nameof(ItemsLinkText));
        OnPropertyChanged(nameof(ItemsCardTitle));
    });

    private void UpdateOwnerRows()
    {
        var ownerId = _room.Current.OwnerConnectionId;
        foreach (var row in Server.Players)
            row.IsOwner = !string.IsNullOrEmpty(ownerId) && row.ConnectionId == ownerId;
    }

    private void UpdateAddress()
    {
        RoomAddress = Server.IsHosting ? $"{LanAddress() ?? "localhost"}:{Server.ServerPort}" : Server.ServerAddress;
        RoomLabel = $"ROOM · {RoomAddress.ToUpperInvariant()}";
    }

    private static string? _lanAddress;

    /// <summary>This machine's first LAN IPv4 address (what a friend connects to when we host).</summary>
    private static string? LanAddress()
    {
        if (_lanAddress != null) return _lanAddress;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;
                if (ni.GetIPProperties().GatewayAddresses.Count == 0) continue; // virtual switches etc.
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                        return _lanAddress = ua.Address.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[room] couldn't list network interfaces for the invite address");
        }
        return null;
    }

    // A toggle flipped by the room owner in edit mode → ask the server; it pushes the result back to everyone.
    partial void OnSharedWalletOnChanged(bool value) => PushRules();
    partial void OnSharedWorldOnChanged(bool value) => PushRules();
    partial void OnSharedItemsOnChanged(bool value) { PushRules(); OnPropertyChanged(nameof(ItemsCardTitle)); OnPropertyChanged(nameof(ItemsLinkText)); }
    partial void OnSharedStoryOnChanged(bool value) => PushRules();

    private void PushRules()
    {
        if (_applyingServerRules || !_room.IsOwner || !Edit.IsEditing) return;
        var wallet = SharedWalletOn;
        var world = SharedWorldOn;
        var items = SharedItemsOn;
        var story = SharedStoryOn;
        _ = Task.Run(async () =>
        {
            try { await _room.SetAsync(wallet, world, items, story); }
            catch (Exception ex) { Serilog.Log.Warning(ex, "[room] failed to update room rules"); }
        });
    }

    private void UpdatePreset(RoomPreset preset)
    {
        IsFullSyncPreset = preset == RoomPreset.FullSync;
        IsCoopPreset = preset == RoomPreset.Coop;
        IsCustomPreset = !IsFullSyncPreset && !IsCoopPreset;
        PresetText = preset switch
        {
            RoomPreset.FullSync => "Full sync",
            RoomPreset.Coop => "Co-op",
            _ => "Custom",
        };
        PresetNote = preset switch
        {
            RoomPreset.FullSync => "Everything is shared: wallet, world, items and story.",
            RoomPreset.Coop => "Players see each other, but progress stays on each save.",
            _ => "A custom mix. Pick a preset to reset.",
        };
    }

    /// <summary>Room owner: wallet, world, items and story all shared.</summary>
    [RelayCommand]
    private void ApplyFullSyncPreset() => PushPreset(RoomPreset.FullSync);

    /// <summary>Room owner: nothing shared — players just see each other.</summary>
    [RelayCommand]
    private void ApplyCoopPreset() => PushPreset(RoomPreset.Coop);

    private void PushPreset(RoomPreset preset)
    {
        if (!_room.IsOwner || !Edit.IsEditing) return;
        _ = Task.Run(async () =>
        {
            try { await _room.SetPresetAsync(preset); }
            catch (Exception ex) { Serilog.Log.Warning(ex, "[room] failed to apply room preset {Preset}", preset); }
        });
    }

    /// <summary>Owner: unlock the rules.</summary>
    [RelayCommand]
    private void StartEdit() => Edit.Begin();

    /// <summary>Done: lock the rules again, showing the room's actual rules (a toggle whose send
    /// failed or was rejected would otherwise keep showing the local flip).</summary>
    [RelayCommand]
    private void StopEdit()
    {
        Edit.End();
        OnRoomRulesChanged(_room.Current);
    }

    [RelayCommand]
    private void OpenItems()
    {
        Flags.Lock();
        ShowFlagsPage = false;
        Items.Lock();
        ShowItemsPage = true;
    }

    [RelayCommand]
    private void OpenFlags()
    {
        Items.Lock();
        ShowItemsPage = false;
        Flags.Lock();
        ShowFlagsPage = true;
    }

    /// <summary>Back to the room overview (the nav's Room item, or a sub-page's back link).</summary>
    public void ShowRoom()
    {
        if (ShowOverview) return;
        Items.Lock();
        Flags.Lock();
        ShowItemsPage = false;
        ShowFlagsPage = false;
    }

    [RelayCommand]
    private async Task CopyInvite()
    {
        CopyRequested?.Invoke(RoomAddress);
        CopyInviteText = "Copied";
        await Task.Delay(1500);
        CopyInviteText = "Copy invite";
    }

    [RelayCommand]
    private void GoLiveState() => NavigateRequested?.Invoke(ViewName.LiveState);

    [RelayCommand]
    private void GoActivity() => NavigateRequested?.Invoke(ViewName.Activity);

    [RelayCommand]
    private void GoToolsWarp()
    {
        Tools.SelectedTool = ToolPage.Warp;
        NavigateRequested?.Invoke(ViewName.Tools);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wallet.TotalChanged -= OnWalletTotalChanged;
        _room.Changed -= OnRoomRulesChanged;
        Edit.PropertyChanged -= OnEditPropertyChanged;
        Server.PropertyChanged -= OnServerPropertyChanged;
        Server.Players.CollectionChanged -= OnPlayersChanged;
        Server.Disconnected -= OnDisconnected;
        Items.PropertyChanged -= OnItemsPropertyChanged;
        Items.BackRequested -= OnItemsBack;
        Flags.PropertyChanged -= OnFlagsPropertyChanged;
        Flags.Edit.PropertyChanged -= OnFlagsEditPropertyChanged;
        Flags.BackRequested -= OnFlagsBack;
        Live.PropertyChanged -= OnLivePropertyChanged;
    }
}
