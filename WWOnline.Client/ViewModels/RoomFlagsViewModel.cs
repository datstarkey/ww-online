using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.ViewModels;

/// <summary>
/// One event flag on the Story flags page: an <see cref="EventFlagCatalog"/> entry (the only source of flag
/// names, descriptions and classes) plus its live state.
/// </summary>
public partial class StoryFlagRow : ObservableObject
{
    public EventFlagInfo Info { get; }

    /// <summary>The decomp name (RODE_KORL, UNK_0F40): the row's primary name.</summary>
    public string Name => Info.Name;
    public string? Description => Info.Description;
    public bool HasDescription => Info.Description != null;

    /// <summary>The flag id in hex, as the decomp and docs write it ("0F80").</summary>
    public string Code { get; }

    /// <summary>Described, or a real decomp name. The rest (bare UNK_xxxx) sit behind "Show unknown flags".</summary>
    public bool IsNamed { get; }

    /// <summary>What the list is sorted by: the description, else the decomp name.</summary>
    public string SortKey { get; }

    /// <summary>In <see cref="StoryFlags.SyncMask"/>: with Shared story on it merges into the room and reaches every player.</summary>
    public bool IsShared { get; }
    public bool IsLocalOnly => !IsShared;
    public bool IsRisky => Info.Risky;
    public bool IsStoryClass => Info.Category == EventFlagCategory.Story;

    /// <summary>The catalog class ("Story", "Local only"...).</summary>
    public string ClassText { get; }
    public string ScopeText => IsShared ? "Shared" : "This game only";

    /// <summary>The catalog's known side effect of a risky flag (null if none recorded).</summary>
    public string? RiskText { get; }
    public string Tooltip { get; }

    [ObservableProperty] private bool _isSet;

    /// <summary>Clickable right now (edit mode, a game to write to, and not held by the room).</summary>
    [ObservableProperty] private bool _canToggle;

    /// <summary>Set, shared, and the room owns the story: the room keeps it, so it can't be unticked.</summary>
    [ObservableProperty] private bool _isLockedByRoom;

    /// <summary><see cref="IsLockedByRoom"/> while editing: show the lock next to the tick.</summary>
    [ObservableProperty] private bool _showLock;

    public StoryFlagRow(EventFlagInfo info)
    {
        Info = info;
        Code = info.Id.ToString("X4", CultureInfo.InvariantCulture);
        IsNamed = info.Description != null || !info.HasPlaceholderName;
        SortKey = info.Description ?? info.Name;
        IsShared = (StoryFlags.SyncMask[info.ByteIndex] & info.Mask) != 0;
        ClassText = ClassLabel(info.Category);
        RiskText = info.Risky ? EventFlagCatalog.RiskEffect(info.Id) ?? "setting it without its normal trigger can have side effects" : null;
        Tooltip = BuildTooltip();
    }

    public static string ClassLabel(EventFlagCategory c) => c switch
    {
        EventFlagCategory.Story => "Story",
        EventFlagCategory.CutsceneSeen => "Cutscene seen",
        EventFlagCategory.SideQuest => "Side quest",
        EventFlagCategory.Collectible => "Collectible",
        EventFlagCategory.NpcState => "NPC state",
        EventFlagCategory.TutorialHint => "Tutorial hint",
        EventFlagCategory.LocalOnly => "Local only",
        _ => "Unknown",
    };

    private string BuildTooltip()
    {
        var lines = new List<string> { $"{Name} · {Code}" };
        if (Description != null) lines.Add(Description);
        lines.Add(IsShared
            ? $"Class: {ClassText}. Shared: with Shared story on it merges into the room and reaches every player."
            : $"Class: {ClassText}{(IsRisky ? " (risky)" : "")}. Never synced: it stays in this game and never reaches other players.");
        if (RiskText != null) lines.Add($"Risky: {RiskText}.");
        lines.Add($"Byte 0x{Info.ByteIndex:X2}, bit mask 0x{Info.Mask:X2}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Search: the decomp name (underscores or spaces), the description, the hex code (with or without 0x),
    /// the class or the scope. <paramref name="q"/> is already trimmed.
    /// </summary>
    public bool Matches(string q)
    {
        if (q.Length == 0) return true;
        var code = q.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? q[2..] : q;
        if (code.Length > 0 && Code.Contains(code, StringComparison.OrdinalIgnoreCase)) return true;
        var spaced = q.Replace('_', ' ');
        return Name.Replace('_', ' ').Contains(spaced, StringComparison.OrdinalIgnoreCase)
            || (Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
            || ClassText.Contains(q, StringComparison.OrdinalIgnoreCase)
            || ScopeText.Contains(q, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// An 8-bit event register (bytes 0x79-0xFF), edited in this game only. Never synced, except the 17
/// Nintendo Gallery figurine bitfields (<see cref="IsSharedFigurines"/>) and the 6 warp jar registers
/// (<see cref="IsSharedWarpJars"/>), which Shared story merges.
/// </summary>
public partial class EventRegisterRow : ObservableObject
{
    public EventRegisterInfo Info { get; }
    public string Name => Info.Name;
    public string Code { get; }
    public string PolicyText { get; }
    public string Tooltip { get; }

    /// <summary>One of the figurine bitfields (<see cref="StoryFlags.FigurineRegisterBytes"/>): shared with Shared story on.</summary>
    public bool IsSharedFigurines { get; }

    /// <summary>One of the warp jar registers (<see cref="StoryFlags.WarpJarRegisterBytes"/>): shared with Shared story on.</summary>
    public bool IsSharedWarpJars { get; }

    /// <summary>Current value (byte &amp; mask), null while no game is loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    private int? _value;

    public string ValueText => Value?.ToString(CultureInfo.InvariantCulture) ?? "—";

    /// <summary>The value typed into the Set box (decimal, or hex with 0x).</summary>
    [ObservableProperty] private string _editText = "";

    public EventRegisterRow(EventRegisterInfo info)
    {
        Info = info;
        Code = info.Id.ToString("X4", CultureInfo.InvariantCulture);
        IsSharedFigurines = StoryFlags.FigurineRegisterBytes.Contains((byte)info.ByteIndex);
        IsSharedWarpJars = StoryFlags.WarpJarRegisterBytes.Contains((byte)info.ByteIndex) && info.Mask == StoryFlags.WarpJarMask;
        PolicyText = IsSharedFigurines ? "Figurines (Shared story)" : IsSharedWarpJars ? "Warp jars (Shared story)" : info.Policy switch
        {
            EventRegisterPolicy.BitwiseOr => "Bitfield",
            EventRegisterPolicy.Max => "Progress state",
            EventRegisterPolicy.LocalOnly => "Local only",
            _ => "Unknown",
        };
        Tooltip = $"{Name} · {Code}\nByte 0x{info.ByteIndex:X2}, value mask 0x{info.Mask:X2} (0 to {info.Mask})\n" +
                  (IsSharedFigurines
                      ? "Nintendo Gallery figurines Carlov has made, one bit each. With Shared story on, a figurine set here merges into the room and every player gets it."
                      : IsSharedWarpJars
                          ? "A dungeon's warp jars that are open, one bit each. With Shared story on, a jar opened here opens for every player."
                          : "Registers are never synced: this changes your game only.");
    }
}

/// <summary>
/// The Story flags page (a sub-page of Room): every save event flag in <see cref="EventFlagCatalog"/>, one
/// flat list, searchable, with its class and whether it reaches other players.
/// <para>
/// With the SharedStory rule on (connected) it shows the room's story: shared flags set in the room or
/// in this game. Only the room owner may edit, and an owner edit sets the flag in the owner's own game;
/// <see cref="StorySyncService"/> then merges it up and applies it to everyone. The room only grows,
/// so shared flags can't be unticked there. With the rule off (or offline) it is "Your game only" and
/// anyone may edit their own game. Every write is gated by <see cref="SceneStabilityGate"/>.
/// Read-only until Edit flags (<see cref="Edit"/>). Event registers are in an Advanced section, this
/// game only (never synced, except the figurine bitfields, which Shared story merges).
/// </para>
/// </summary>
public partial class RoomFlagsViewModel : ViewModelBase, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RoomFlagsViewModel>();

    public const string RoomKeepsFlagsNote = "The room keeps story progress; flags can't be removed from a shared room yet.";

    private readonly IDolphinService _dolphin;
    private readonly IStoryRoom _room;
    private readonly Action<Action> _ui;
    private System.Timers.Timer? _timer;
    private bool _disposed;

    // Reads and writes touch save data: only while the game is in play. Poll checks the gate
    // (serialized by _pollLock); edits check _sceneStable.
    private readonly SceneStabilityGate _scene = new(2);
    private readonly object _pollLock = new();
    private volatile bool _sceneStable;

    private bool? _lastRoomMode;
    private bool? _lastConnected;

    // UI thread only
    private byte[]? _local;            // this game's 256-byte event array, null = no save loaded / no game
    private StoryFlags? _roomFlags;    // the room's flags (room mode only)

    private readonly List<StoryFlagRow> _sortedRows;

    /// <summary>Every catalog flag, named ones first (A-Z by description), then unknown by code.</summary>
    public IReadOnlyList<StoryFlagRow> AllRows => _sortedRows;

    /// <summary>The list as shown (search and the unknown toggle applied).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleCountText))]
    [NotifyPropertyChangedFor(nameof(HasNoMatches))]
    private IReadOnlyList<StoryFlagRow> _visibleRows = [];

    public IReadOnlyList<EventRegisterRow> Registers { get; }

    /// <summary>Read-only by default; Edit flags → edit mode, Done → locked.</summary>
    public EditMode Edit { get; } = new();
    public bool IsEditing => Edit.IsEditing;
    public bool ShowEditButton => Edit.CanEdit && !Edit.IsEditing;

    /// <summary>Raised when the page's "← Room" link is pressed.</summary>
    public event Action? BackRequested;

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private bool _showUnknown;
    [ObservableProperty] private bool _isRoomMode;
    [ObservableProperty] private bool _isOwner;
    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private bool _hasLocalGame;
    [ObservableProperty] private int _setCount;
    [ObservableProperty] private string? _statusMessage;

    public int TotalCount => _sortedRows.Count;
    public int UnknownCount { get; }
    public string ShowUnknownText => $"Show unknown flags ({UnknownCount})";
    public string SetCountText => HasData ? $"{SetCount} of {TotalCount} set" : $"— of {TotalCount} set";
    public double SetPercent => HasData && TotalCount > 0 ? 100.0 * SetCount / TotalCount : 0;
    public string VisibleCountText => VisibleRows.Count == TotalCount ? $"All {TotalCount} flags" : $"Showing {VisibleRows.Count} of {TotalCount}";
    public bool HasNoMatches => VisibleRows.Count == 0;

    /// <summary>Room mode, not the owner: explain why there's no Edit button.</summary>
    public bool ShowViewOnly => IsRoomMode && !IsOwner;

    public string PageTitle => IsRoomMode ? "Story flags" : "Your story flags";
    public string PageSubtitle => IsRoomMode
        ? "The room's shared story. Flags any player's game sets merge into the room. The room owner can add flags in Edit mode."
        : "Shared story is off, so each player keeps their own story. Edits here change your own game only.";
    public string EditBannerTitle => IsRoomMode ? "Editing the room's story." : "Editing your story flags.";
    public string EditBannerText => IsRoomMode
        ? "A shared flag you tick is set in your game and reaches every player. This-game-only flags stay in your game."
        : "Shared story is off, so this only changes your own game.";
    public string SyncedText => IsRoomMode ? "Shared with the room" : "Your game only";
    public string WaitingText => IsRoomMode
        ? "Waiting for the story flags. They appear once your game has a save loaded or the room's story arrives."
        : "Attach to Dolphin on the Dolphin page and load a save to see your story flags.";
    public string CardNote => IsRoomMode
        ? "Shared story is on: the room keeps everyone's story progress."
        : "Shared story is off: these are your own game's flags.";

    /// <summary>DI: the live room (rules, connection, story sync) and the UI dispatcher.</summary>
    public RoomFlagsViewModel(
        IDolphinService dolphin,
        RoomSettingsService roomSettings,
        SignalRClientService signalR,
        StorySyncService story)
        : this(dolphin, new StoryRoomState(roomSettings, signalR, story), a => Dispatcher.UIThread.Post(a), startPolling: true)
    {
    }

    /// <param name="ui">Runs an action on the UI thread (tests pass <c>a => a()</c>).</param>
    /// <param name="startPolling">Read the game every second (tests call <see cref="Poll"/> instead).</param>
    public RoomFlagsViewModel(IDolphinService dolphin, IStoryRoom room, Action<Action> ui, bool startPolling)
    {
        _dolphin = dolphin;
        _room = room;
        _ui = ui;

        var rows = EventFlagCatalog.Flags.Select(f => new StoryFlagRow(f)).ToList();
        _sortedRows = rows.Where(r => r.IsNamed)
            .OrderBy(r => r.SortKey, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Info.Id)
            .Concat(rows.Where(r => !r.IsNamed).OrderBy(r => r.Info.Id))
            .ToList();
        UnknownCount = rows.Count(r => !r.IsNamed);
        Registers = EventFlagCatalog.Registers.Select(r => new EventRegisterRow(r)).ToList();

        Edit.PropertyChanged += OnEditPropertyChanged;
        _room.Changed += OnRoomChanged;
        _dolphin.ConnectionChanged += OnDolphinConnectionChanged;
        RefreshMode();
        RebuildVisible();
        ApplyState();

        if (startPolling)
        {
            _timer = new System.Timers.Timer(1000) { AutoReset = true };
            _timer.Elapsed += (_, _) => _ = Task.Run(Poll);
            _timer.Start();
        }
    }

    // ═══ Mode / edit lock ═══

    private void OnRoomChanged() => _ui(() =>
    {
        if (_disposed) return;
        RefreshMode();
        ApplyState();
    });

    private void OnDolphinConnectionChanged(object? sender, bool connected)
    {
        if (!connected) _ui(() => { _local = null; ApplyState(); });
    }

    /// <summary>Room vs own game, owner, who may edit. Edit mode never survives a mode or connection change.</summary>
    private void RefreshMode()
    {
        bool connected = _room.IsConnected;
        bool roomMode = connected && _room.SharedStory;
        IsOwner = connected && _room.IsOwner;
        IsRoomMode = roomMode;
        if (!roomMode) _roomFlags = null;
        Edit.CanEdit = !roomMode || IsOwner;
        if (_lastRoomMode != roomMode || _lastConnected != connected)
        {
            _lastRoomMode = roomMode;
            _lastConnected = connected;
            Edit.End();
        }
    }

    private void OnEditPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditMode.IsEditing))
        {
            OnPropertyChanged(nameof(IsEditing));
            UpdateEditability();
        }
        OnPropertyChanged(nameof(ShowEditButton));
    }

    partial void OnIsRoomModeChanged(bool value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(EditBannerTitle));
        OnPropertyChanged(nameof(EditBannerText));
        OnPropertyChanged(nameof(SyncedText));
        OnPropertyChanged(nameof(WaitingText));
        OnPropertyChanged(nameof(CardNote));
        OnPropertyChanged(nameof(ShowViewOnly));
    }

    partial void OnIsOwnerChanged(bool value) => OnPropertyChanged(nameof(ShowViewOnly));

    partial void OnHasDataChanged(bool value)
    {
        OnPropertyChanged(nameof(SetCountText));
        OnPropertyChanged(nameof(SetPercent));
    }

    partial void OnSetCountChanged(int value)
    {
        OnPropertyChanged(nameof(SetCountText));
        OnPropertyChanged(nameof(SetPercent));
    }

    partial void OnSearchChanged(string value) => RebuildVisible();
    partial void OnShowUnknownChanged(bool value) => RebuildVisible();

    /// <summary>
    /// Browsing shows named flags, plus unknown ones when the toggle is on. A search looks through
    /// every flag (so a decomp name or code is always found), named matches first.
    /// </summary>
    private void RebuildVisible()
    {
        var q = Search?.Trim() ?? "";
        VisibleRows = q.Length == 0
            ? _sortedRows.Where(r => r.IsNamed || ShowUnknown).ToList()
            : _sortedRows.Where(r => r.Matches(q)).ToList();
    }

    [RelayCommand]
    private void ClearSearch() => Search = "";

    [RelayCommand]
    private void StartEdit()
    {
        if (!Edit.Begin())
            StatusMessage = IsRoomMode ? "Only the room owner can change the room's story flags" : null;
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

    // ═══ Reading the game ═══

    /// <summary>Read this game's event flags (and the room's copy). Runs on the 1 Hz timer; tests call it directly.</summary>
    public void Poll()
    {
        if (!Monitor.TryEnter(_pollLock)) return; // a poll is already running
        try
        {
            byte[]? bytes = null;
            bool clear;
            if (_dolphin.IsConnected)
            {
                _sceneStable = _scene.Check(_dolphin);
                if (_sceneStable)
                    bytes = _dolphin.ReadMemory(EventFlagCatalog.EventBitfieldAddress, EventFlagCatalog.EventBitfieldSize);
                // Title screen / file select run on an empty default save: show nothing for this game.
                clear = !_sceneStable && StageIDs.IsNonGameplayStage(_scene.Stage);
            }
            else
            {
                _sceneStable = false;
                _scene.Reset();
                clear = true;
            }
            var room = _room.RoomFlags;
            _ui(() => OnPolled(bytes, clear, room));
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "[story] flag page read failed");
        }
        finally
        {
            Monitor.Exit(_pollLock);
        }
    }

    private void OnPolled(byte[]? bytes, bool clear, StoryFlags? room)
    {
        if (_disposed) return;
        if (bytes is { Length: >= EventFlagCatalog.EventBitfieldSize }) _local = bytes;
        else if (clear) _local = null;
        RefreshMode();
        _roomFlags = IsRoomMode ? room : null;
        ApplyState();
    }

    /// <summary>Refresh every row from this game's bytes and (room mode) the room's flags.</summary>
    private void ApplyState()
    {
        int count = 0;
        foreach (var row in _sortedRows)
        {
            bool local = _local != null && (_local[row.Info.ByteIndex] & row.Info.Mask) != 0;
            bool room = _roomFlags != null && row.IsShared && _roomFlags.Has(row.Info.Id);
            row.IsSet = local || room;
            if (row.IsSet) count++;
        }
        foreach (var reg in Registers)
            reg.Value = _local == null ? null : _local[reg.Info.ByteIndex] & reg.Info.Mask;
        SetCount = count;
        HasLocalGame = _local != null;
        HasData = _local != null || _roomFlags != null;
        UpdateEditability();
    }

    private void UpdateEditability()
    {
        bool editing = Edit.IsEditing;
        foreach (var row in _sortedRows)
        {
            row.IsLockedByRoom = IsRoomMode && row.IsShared && row.IsSet;
            row.ShowLock = editing && row.IsLockedByRoom;
            row.CanToggle = editing && _local != null && !row.IsLockedByRoom;
        }
    }

    // ═══ Editing (always this game's save; shared story carries owner edits to the room) ═══

    /// <summary>Edits write save data: Dolphin attached and the game in play (not title / loading).</summary>
    private bool LocalGameReady()
    {
        if (!_dolphin.IsConnected)
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

    /// <summary>
    /// Shared story is on right now. Read live from the room, not the cached <see cref="IsRoomMode"/>:
    /// the rule or the connection can change between a room event and the UI-thread refresh.
    /// </summary>
    private bool RoomModeNow => _room.IsConnected && _room.SharedStory;

    /// <summary>Common checks for any edit: edit mode, and in room mode only the owner.</summary>
    private bool MayEdit()
    {
        if (!Edit.IsEditing)
        {
            StatusMessage = Edit.CanEdit ? "Press Edit flags to change flags" : "Only the room owner can change the room's story flags";
            return false;
        }
        if ((IsRoomMode || RoomModeNow) && !_room.IsOwner)
        {
            StatusMessage = "Only the room owner can change the room's story flags";
            return false;
        }
        return true;
    }

    [RelayCommand]
    private void ToggleFlag(StoryFlagRow? row)
    {
        if (row == null || !MayEdit()) return;
        bool set = !row.IsSet;
        if (!set && (IsRoomMode || RoomModeNow) && row.IsShared)
        {
            StatusMessage = RoomKeepsFlagsNote;
            return;
        }
        if (!LocalGameReady()) return;

        try
        {
            uint addr = EventFlagCatalog.EventBitfieldAddress + (uint)row.Info.ByteIndex;
            var current = _dolphin.ReadMemory(addr, 1);
            if (current == null)
            {
                StatusMessage = "Couldn't read your game's flags right now — try again";
                return;
            }
            byte value = set ? (byte)(current[0] | row.Info.Mask) : (byte)(current[0] & ~row.Info.Mask);
            if (!_dolphin.WriteMemory(addr, [value]))
            {
                StatusMessage = $"Couldn't write {row.Name} — try again";
                return;
            }
            if (_local != null) _local[row.Info.ByteIndex] = value;
            ApplyState();

            string where = !IsRoomMode ? ""
                : row.IsShared ? " in your game. Shared story sends it to the room"
                : " in your game only. It never reaches other players";
            StatusMessage = $"{(set ? "Set" : "Cleared")} {row.Name}{where}";
            if (set && row.RiskText != null) StatusMessage += $". Warning: {row.RiskText}";
            Logger.Information("[story] flag editor: {Action} {Flag} ({Code}, {Class}, {Scope}) in this game{Room}",
                set ? "set" : "cleared", row.Name, row.Code, row.ClassText, row.ScopeText,
                IsRoomMode ? " as room owner (shared story on)" : "");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[story] flag editor: toggling {Flag} failed", row.Name);
            StatusMessage = $"Toggle failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SetRegister(EventRegisterRow? reg)
    {
        if (reg == null || !MayEdit()) return;
        var text = reg.EditText?.Trim() ?? "";
        bool parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)
            : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        if (!parsed || v < 0 || v > reg.Info.Mask || (v & ~reg.Info.Mask) != 0)
        {
            StatusMessage = $"{reg.Name}: enter a value from 0 to {reg.Info.Mask}";
            return;
        }
        if (!LocalGameReady()) return;

        try
        {
            uint addr = EventFlagCatalog.EventBitfieldAddress + (uint)reg.Info.ByteIndex;
            var current = _dolphin.ReadMemory(addr, 1);
            if (current == null)
            {
                StatusMessage = "Couldn't read your game's registers right now — try again";
                return;
            }
            // dSv_event_c::setEventReg: clear the register's bits, then OR the value in.
            byte value = (byte)((current[0] & ~reg.Info.Mask) | v);
            if (!_dolphin.WriteMemory(addr, [value]))
            {
                StatusMessage = $"Couldn't write {reg.Name} — try again";
                return;
            }
            if (_local != null) _local[reg.Info.ByteIndex] = value;
            ApplyState();
            StatusMessage = $"Set register {reg.Name} to {v} in your game only";
            Logger.Information("[story] flag editor: register {Register} ({Code}) = {Value} in this game", reg.Name, reg.Code, v);
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[story] flag editor: setting register {Register} failed", reg.Name);
            StatusMessage = $"Set failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Edit.PropertyChanged -= OnEditPropertyChanged;
        _room.Changed -= OnRoomChanged;
        _dolphin.ConnectionChanged -= OnDolphinConnectionChanged;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        (_room as StoryRoomState)?.Dispose();
    }
}
