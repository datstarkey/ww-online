using Serilog;
using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>What a warp does (<see cref="WarpPlanner"/>).</summary>
public enum WarpAction
{
    /// <summary>Nothing: <see cref="WarpPlan.Message"/> says why.</summary>
    Refuse,
    /// <summary>Same stage and room: move Link to the other player's position, no reload.</summary>
    MoveInRoom,
    /// <summary>Another stage or room: load the other player's stage at the spawn point they entered it through.</summary>
    LoadEntrance,
}

/// <param name="AcrossRooms">A move to another room of the same stage (the Great Sea's squares): the room number
/// changes once the game notices where Link is, so only a stage change or a void-out stops it.</param>
public sealed record WarpPlan(WarpAction Action, string Message, StageEntry? Entrance = null, Vector3? Position = null, short AngleY = 0,
    bool AcrossRooms = false)
{
    public static WarpPlan Refuse(string message) => new(WarpAction.Refuse, message);
}

/// <summary>The local game as a warp sees it (<see cref="WarpService.ReadLocal"/>).</summary>
public readonly record struct LocalWarpSnapshot(
    string Stage, byte Room, uint Link, WarpBusy Busy, bool MenuOpen, uint ModeFlg, bool RidingShip);

/// <summary>The outcome of a warp request: whether it happened, and the line the Room page shows.</summary>
public readonly record struct WarpResult(bool Ok, string Message);

/// <summary>
/// Decides what "Warp to" does (docs/softlocks.md). Pure: the local game, the target's last puppet data and
/// the room rule in, a <see cref="WarpPlan"/> out.
/// <list type="bullet">
/// <item>Same stage and same room, or both on the Great Sea (its squares are rooms): move Link to the target's
/// position and facing, no reload.</item>
/// <item>Anywhere else: load the target's stage at the spawn point / room / layer they entered it through
/// (<see cref="WarpInfo"/>). Link appears at that spawn point, not next to them.</item>
/// </list>
/// </summary>
public static class WarpPlanner
{
    /// <summary>Puppet data older than this is not a location to warp to (PuppetSyncService drops such puppets too).</summary>
    public static readonly TimeSpan MaxTargetAge = TimeSpan.FromSeconds(3);

    public static WarpPlan Plan(bool allowWarping, LocalWarpSnapshot? local, string name, PuppetData? target, TimeSpan targetAge)
    {
        if (!allowWarping)
            return WarpPlan.Refuse("Warping is off in this room. The room owner can turn on Allow warping.");

        // You
        if (local is not { } me || me.Link == 0)
            return WarpPlan.Refuse("Your game isn't running (or the app isn't attached to Dolphin).");
        if (me.Stage.Length == 0 || StageIDs.IsNonGameplayStage(me.Stage))
            return WarpPlan.Refuse("Load your save first: you're on the title screen or file select.");
        if (me.Busy != WarpBusy.None)
            return WarpPlan.Refuse(me.Busy switch
            {
                WarpBusy.Event => "You're in a cutscene or a conversation. Try again when it ends.",
                WarpBusy.Loading => "You're between areas. Try again in a moment.",
                WarpBusy.Dead => "Link is down. Try again once you're back up.",
                WarpBusy.Minigame => "Finish or leave the minigame first.",
                WarpBusy.OtherCharacter => "You're controlling another character. Switch back to Link first.",
                _ => "You can't warp right now.",
            });
        if (me.MenuOpen)
            return WarpPlan.Refuse("Close the pause menu first.");

        // Them
        if (target == null || targetAge > MaxTargetAge || !target.IsValid())
            return WarpPlan.Refuse($"{name} isn't sending a position. Are they in the game?");
        if (string.IsNullOrEmpty(target.StageName) || StageIDs.IsNonGameplayStage(target.StageName))
            return WarpPlan.Refuse($"{name} is on the title screen or file select.");
        if (target.Warp is not { } warp)
            return WarpPlan.Refuse($"{name}'s location isn't shared. Is Allow warping on?");
        if (warp.Busy != WarpBusy.None)
            return WarpPlan.Refuse(warp.Busy switch
            {
                WarpBusy.Event => $"{name} is in a cutscene or a conversation. Try again when it ends.",
                WarpBusy.Loading => $"{name} is between areas. Try again in a moment.",
                WarpBusy.Dead => $"{name} is down. Try again once they're back up.",
                WarpBusy.Minigame => $"{name} is in a minigame. Try again when it's over.",
                WarpBusy.OtherCharacter => $"{name} is controlling another character. Try again when they're back to Link.",
                _ => $"{name} can't be warped to right now.",
            });

        // The Great Sea's rooms are its 7x7 grid squares (PuppetVisibility), loaded by where Link is: on the
        // sea every warp is a move. Its "entrance" is wherever the target last came onto the sea (an island's
        // door), which can be squares away from them by now.
        bool atSea = target.StageName == PuppetVisibility.SeaStage && me.Stage == PuppetVisibility.SeaStage;
        if (target.StageName == me.Stage && (target.RoomNumber == me.Room || atSea))
        {
            if (me.RidingShip)
                return WarpPlan.Refuse($"You're on your boat. Get off it first, or sail over to {name}.");
            if ((me.ModeFlg & GameMemoryAddresses.Warp.ModeFlgAttached) != 0)
                return WarpPlan.Refuse("Let go first: Link is holding on to something (a ledge, a ladder, a rope, a block...).");
            short angle = unchecked((short)(ushort)Math.Clamp(target.Rotation, 0f, ushort.MaxValue));
            return new WarpPlan(WarpAction.MoveInRoom, $"Moving you to {name}.",
                Position: new Vector3(target.Position.X, target.Position.Y, target.Position.Z), AngleY: angle,
                AcrossRooms: atSea && target.RoomNumber != me.Room);
        }

        string where = StageIDs.GetStageName(target.StageName);
        if (!warp.HasEntry)
            return WarpPlan.Refuse($"{name}'s way into {where} isn't known yet (they respawned there). " +
                                   "It will be once they go through a door or a loading zone.");
        if (!WarpMemory.IsValidStageName(target.StageName))
            return WarpPlan.Refuse($"{name}'s stage '{target.StageName}' isn't a stage name.");
        var entrance = new StageEntry(target.StageName, warp.EntryPoint, warp.EntryRoom, warp.Layer);
        return new WarpPlan(WarpAction.LoadEntrance,
            $"Warping to {name}: {where}, at the entrance they came in through.", Entrance: entrance);
    }
}

/// <summary>Carries out a <see cref="WarpPlan"/> on the game (<see cref="WarpMemory"/>).</summary>
public static class WarpExecutor
{
    private static readonly ILogger Logger = Log.ForContext(typeof(WarpExecutor));

    /// <summary>How close Link must end up to count as moved.</summary>
    public const float ArrivedDistance = 100f;
    public const int MoveAttempts = 3;
    /// <summary>A few frames: long enough for Link's execute to run on the new position (and undo it, if it will).</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(150);

    /// <param name="stageExists">Whether the stage is in the game files (null = can't tell): loading a missing stage crashes the game.</param>
    /// <param name="delay">Task.Delay; a no-op in tests.</param>
    public static async Task<WarpResult> ExecuteAsync(IDolphinService dolphin, WarpPlan plan, LocalWarpSnapshot local,
        Func<string, bool?> stageExists, Func<TimeSpan, CancellationToken, Task> delay, CancellationToken ct = default)
    {
        switch (plan.Action)
        {
            case WarpAction.LoadEntrance when plan.Entrance is { } entrance:
            {
                if (stageExists(entrance.Name) == false)
                {
                    Logger.Warning("[warp] refused: stage {Stage} has no Stage.arc in the configured game folders", entrance.Name);
                    return new(false, $"The stage '{entrance.Name}' isn't in your game files.");
                }
                if (!WarpMemory.RequestStageChange(dolphin, entrance))
                {
                    Logger.Information("[warp] not sent: a stage change is already pending");
                    return new(false, "You're already changing areas. Try again in a moment.");
                }
                Logger.Information("[warp] stage change requested: {Stage} point {Point} room {Room} layer {Layer} (from {From} room {FromRoom})",
                    entrance.Name, entrance.Point, entrance.Room, entrance.Layer, local.Stage, local.Room);
                return new(true, plan.Message);
            }

            case WarpAction.MoveInRoom when plan.Position is { } pos:
            {
                for (int attempt = 1; attempt <= MoveAttempts; attempt++)
                {
                    // Still the same Link in the same room? (A door or a void-out in between makes the move stale.)
                    if (WarpMemory.ReadU32(dolphin, GameMemoryAddresses.Warp.LinkActorPtr) != local.Link ||
                        (!plan.AcrossRooms && dolphin.Read(GameMemoryAddresses.Stage.CurrentRoomNumber) != local.Room) ||
                        WarpMemory.IsStageChangePending(dolphin))
                    {
                        Logger.Information("[warp] move stopped: Link changed room or stage (attempt {Attempt})", attempt);
                        return new(false, "You changed areas before the warp finished.");
                    }
                    var before = WarpMemory.ReadActorPos(dolphin, local.Link);
                    WarpMemory.MoveActor(dolphin, local.Link, pos, plan.AngleY);
                    await delay(SettleDelay, ct);
                    var after = WarpMemory.ReadActorPos(dolphin, local.Link);
                    float dist = after is { } a ? WarpMemory.Distance(a, pos) : float.NaN;
                    Logger.Information("[warp] move attempt {Attempt}: {Before} -> ({X:F0}, {Y:F0}, {Z:F0}) angle 0x{Angle:X4} in {Stage} room {Room}; now {After} ({Dist:F0} away)",
                        attempt, Fmt(before), pos.X, pos.Y, pos.Z, (ushort)plan.AngleY, local.Stage, local.Room, Fmt(after), dist);
                    if (dist <= ArrivedDistance)
                        return new(true, plan.Message);
                }
                Logger.Warning("[warp] move failed: Link didn't stay at the target after {Attempts} attempts", MoveAttempts);
                return new(false, "Link was pulled back. Stand still on the ground and try again.");
            }

            default:
                Logger.Information("[warp] refused: {Message}", plan.Message);
                return new(false, plan.Message);
        }
    }

    private static string Fmt(Vector3? v) => v is { } p ? $"({p.X:F0}, {p.Y:F0}, {p.Z:F0})" : "(?)";
}

/// <summary>
/// "Warp to" on the Room page: warps the local player to another player in the room, when the room's
/// <see cref="RoomSettings.AllowWarping"/> rule is on. Client-only: the target's location, room and
/// entrance come with their puppet data (<see cref="PuppetData.Warp"/>). Logs <c>[warp]</c> lines.
/// </summary>
public sealed class WarpService
{
    private static readonly ILogger Logger = Log.ForContext<WarpService>();

    private readonly IDolphinService _dolphin;
    private readonly PuppetSyncService _puppets;
    private readonly RoomSettingsService _room;
    private readonly GameSettingsService _settings;
    private int _busy;

    public WarpService(IDolphinService dolphin, PuppetSyncService puppets, RoomSettingsService room, GameSettingsService settings)
    {
        _dolphin = dolphin;
        _puppets = puppets;
        _room = room;
        _settings = settings;
    }

    public async Task<WarpResult> WarpToAsync(string playerId, string playerName, CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            return new(false, "A warp is already under way.");
        try
        {
            var local = ReadLocal();
            bool known = _puppets.TryGetRemotePuppet(playerId, out var target, out var age);
            var plan = WarpPlanner.Plan(_room.Current.AllowWarping, local, playerName, known ? target : null, known ? age : TimeSpan.MaxValue);
            Logger.Information("[warp] to {Name} ({Id}): me {Me}; them {Them}; plan {Action}: {Message}",
                playerName, playerId, Describe(local), known ? Describe(target!, age) : "no puppet data", plan.Action, plan.Message);
            if (plan.Action == WarpAction.Refuse || local is not { } me)
                return new(false, plan.Message);
            return await WarpExecutor.ExecuteAsync(_dolphin, plan, me, StageExists, Task.Delay, ct);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[warp] to {Name} failed", playerName);
            return new(false, $"The warp failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    /// <summary>The local game as the planner needs it; null when Dolphin isn't attached.</summary>
    public LocalWarpSnapshot? ReadLocal()
    {
        if (!_dolphin.IsConnected)
            return null;
        string stage = WarpMemory.ReadStartStage(_dolphin)?.Name ?? "";
        byte room = _dolphin.Read(GameMemoryAddresses.Stage.CurrentRoomNumber) ?? 0;
        uint link = WarpMemory.ReadU32(_dolphin, GameMemoryAddresses.Warp.LinkActorPtr);
        // The minigame gate lives in the 20Hz puppet tick (it needs every tick to spot a stale type).
        bool minigame = _puppets.LocalWarp?.Busy == WarpBusy.Minigame;
        var busy = WarpMemory.ReadBusy(_dolphin, minigame);
        bool menu = _dolphin.ReadMemory(GameMemoryAddresses.Events.MenuPause, 1) is not [0];
        uint modeFlg = link == 0 ? 0 : WarpMemory.ReadU32(_dolphin, link + PuppetLayout.DAPY_OFF_MODE_FLG);
        return new LocalWarpSnapshot(stage, room, link, busy, menu, modeFlg, link != 0 && IsRidingShip(_dolphin, link));
    }

    /// <summary>Riding the King of Red Lions: the SHIP_RIDE status bit or a ship proc (as PuppetSyncService.ReadLocalBoat).</summary>
    public static bool IsRidingShip(IDolphinService dolphin, uint link)
    {
        uint status0 = WarpMemory.ReadU32(dolphin, GameMemoryAddresses.Player.PlayerStatusBitfield0.Address);
        byte proc = (byte)(WarpMemory.ReadU32(dolphin, link + PuppetLayout.DAPY_OFF_CUR_PROC) & 0xFF);
        return (status0 & GameMemoryAddresses.Sea.PlayerStatus0ShipRide) != 0
               || proc is >= GameMemoryAddresses.Sea.ProcShipFirst and <= GameMemoryAddresses.Sea.ProcShipLast
               || proc == GameMemoryAddresses.Sea.ProcDemoShipSit;
    }

    private bool? StageExists(string stage)
    {
        var s = _settings.Load();
        return StageFiles.Exists(stage, s.GamePath, s.VanillaGamePath);
    }

    private static string Describe(LocalWarpSnapshot? local) => local is { } l
        ? $"{l.Stage} room {l.Room} link 0x{l.Link:X8} busy {l.Busy} menu {l.MenuOpen} mode 0x{l.ModeFlg:X8} ship {l.RidingShip}"
        : "not attached";

    private static string Describe(PuppetData t, TimeSpan age) =>
        $"{t.StageName} room {t.RoomNumber} at ({t.Position.X:F0}, {t.Position.Y:F0}, {t.Position.Z:F0}) " +
        $"entrance {(t.Warp is { } w ? $"point {w.EntryPoint} room {w.EntryRoom} layer {w.Layer} busy {w.Busy}" : "none")}" +
        $"{(t.Boat is { Parked: false } ? " sailing" : "")}, {age.TotalMilliseconds:F0} ms old";
}

/// <summary>The game's own files are the source of truth for stage names.</summary>
public static class StageFiles
{
    /// <summary>
    /// True if files/res/Stage/&lt;name&gt;/Stage.arc exists in one of the game folders, false if none has it,
    /// null if no game folder is configured (can't check).
    /// </summary>
    public static bool? Exists(string stageName, params string?[] gameRoots)
    {
        bool checkedAny = false;
        foreach (var root in gameRoots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var stageDir = Path.Combine(root, "files", "res", "Stage");
            if (!Directory.Exists(stageDir)) continue;
            checkedAny = true;
            if (File.Exists(Path.Combine(stageDir, stageName, "Stage.arc"))) return true;
        }
        return checkedAny ? false : null;
    }
}
