using System.Buffers.Binary;
using System.Text;
using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>A stage the game was entered with or is asked to load (dStage_startStage_c, d_stage.h:949-952).</summary>
public readonly record struct StageEntry(string Name, short Point, sbyte Room, sbyte Layer);

/// <summary>
/// The game memory behind "Warp to player" (docs/softlocks.md): reading where the local stage was
/// entered, asking the game for a stage change the way dComIfGp_setNextStage does, and moving the
/// local Link within his room. Addresses: <see cref="GameMemoryAddresses.Warp"/>.
/// </summary>
public static class WarpMemory
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext(typeof(WarpMemory));

    /// <summary>Longest stage name the game's 8-byte mName holds with its NUL.</summary>
    public const int MaxStageNameLength = GameMemoryAddresses.Warp.StageNameSize - 1;

    /// <summary>play.mCurStage: the stage, spawn point, room and layer the current stage was entered with.</summary>
    public static StageEntry? ReadStartStage(IDolphinService dolphin)
    {
        var b = dolphin.ReadMemory(GameMemoryAddresses.Warp.StartStage, GameMemoryAddresses.Warp.NextStageOffEnable);
        if (b == null || b.Length < GameMemoryAddresses.Warp.NextStageOffEnable)
            return null;
        return new StageEntry(
            StageName(b.AsSpan(GameMemoryAddresses.Warp.StageOffName, GameMemoryAddresses.Warp.StageNameSize)),
            BinaryPrimitives.ReadInt16BigEndian(b.AsSpan(GameMemoryAddresses.Warp.StageOffPoint)),
            (sbyte)b[GameMemoryAddresses.Warp.StageOffRoom],
            (sbyte)b[GameMemoryAddresses.Warp.StageOffLayer]);
    }

    /// <summary>An 8-byte, NUL-padded stage name.</summary>
    public static string StageName(ReadOnlySpan<byte> bytes)
    {
        int nul = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(nul >= 0 ? bytes[..nul] : bytes);
    }

    /// <summary>A name the game can take in mName: 1..7 letters, digits or '_' (stage folder names).</summary>
    public static bool IsValidStageName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaxStageNameLength &&
        name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    /// <summary>True while a stage change is pending (play.mNextStage.mEnable).</summary>
    public static bool IsStageChangePending(IDolphinService dolphin) =>
        dolphin.ReadMemory(GameMemoryAddresses.Warp.NextStage + GameMemoryAddresses.Warp.NextStageOffEnable, 1) is not [0];

    /// <summary>
    /// Ask the game to load <paramref name="entry"/>, as dComIfGp_setNextStage(name, point, room, layer, 0, mode,
    /// TRUE, 0) does (d_com_inf_game.cpp:667-691, DOL 0x800537C8): mRestart's last speed / mode (Link arrives
    /// standing; the Magic Armor, a timed shield and the soup's power-up carry over) and start code, then
    /// play.mNextStage with mEnable written last, so the game never sees a half-written request. dScnPly_Draw
    /// starts the change on its next frame. False (and nothing written) if a change is already pending.
    /// </summary>
    public static bool RequestStageChange(IDolphinService dolphin, StageEntry entry)
    {
        if (!IsValidStageName(entry.Name))
            throw new ArgumentException($"not a stage name: '{entry.Name}'", nameof(entry));
        if (IsStageChangePending(dolphin))
            return false;

        uint restart = GameMemoryAddresses.Warp.Restart;
        var u32 = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(u32, 0); // 0.0f
        dolphin.WriteMemory(restart + GameMemoryAddresses.Warp.RestartOffLastSpeedF, u32);
        BinaryPrimitives.WriteUInt32BigEndian(u32, LastSceneMode(dolphin));
        dolphin.WriteMemory(restart + GameMemoryAddresses.Warp.RestartOffLastMode, u32);
        var s16 = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(s16, entry.Point);
        dolphin.WriteMemory(restart + GameMemoryAddresses.Warp.RestartOffStartCode, s16);

        // name, point, room, layer in one write; then the wipe; then mEnable.
        var stage = new byte[GameMemoryAddresses.Warp.NextStageOffEnable];
        Encoding.ASCII.GetBytes(entry.Name, stage.AsSpan(GameMemoryAddresses.Warp.StageOffName));
        BinaryPrimitives.WriteInt16BigEndian(stage.AsSpan(GameMemoryAddresses.Warp.StageOffPoint), entry.Point);
        stage[GameMemoryAddresses.Warp.StageOffRoom] = (byte)entry.Room;
        stage[GameMemoryAddresses.Warp.StageOffLayer] = (byte)entry.Layer;
        uint next = GameMemoryAddresses.Warp.NextStage;
        dolphin.WriteMemory(next, stage);
        dolphin.WriteMemory(next + GameMemoryAddresses.Warp.NextStageOffWipe, [0]);
        return dolphin.WriteMemory(next + GameMemoryAddresses.Warp.NextStageOffEnable, [1]);
    }

    /// <summary>
    /// mLastMode as dComIfGp_setNextStage builds it for a plain exit (mode 0): 0x8000 while the Magic Armor
    /// is on, the timed shield's timer in the high half, 0x4000 with the soup's power-up (DOL 0x8005382C-0x80053850).
    /// </summary>
    public static uint LastSceneMode(IDolphinService dolphin)
    {
        uint link = ReadU32(dolphin, GameMemoryAddresses.Warp.LinkActorPtr);
        if (link == 0)
            return 0;
        uint flg1 = ReadU32(dolphin, link + PuppetLayout.DAPY_OFF_NO_RESET_FLG1);
        var timer = dolphin.ReadMemory(link + GameMemoryAddresses.Warp.LinkOffTinkleShieldTimer, 2);
        short tinkle = timer is { Length: 2 } ? BinaryPrimitives.ReadInt16BigEndian(timer) : (short)0;

        uint mode = 0;
        if ((flg1 & GameMemoryAddresses.Warp.NoResetFlg1DragonShield) != 0)
            mode |= GameMemoryAddresses.Warp.LastModeDragonShield;
        mode |= (uint)tinkle << 16; // slwi of the sign-extended s16, as the game does
        if ((flg1 & GameMemoryAddresses.Warp.NoResetFlg1SoupPowerUp) != 0)
            mode |= GameMemoryAddresses.Warp.LastModeSoupPowerUp;
        return mode;
    }

    /// <summary>
    /// Move the local Link to <paramref name="pos"/> facing <paramref name="angleY"/>: current.pos first, then
    /// old.pos (so his Acch sees no move to push back across a wall: it checks old -> current), both angles,
    /// and his speed zeroed so he doesn't carry momentum. The room must be the one he is in.
    /// </summary>
    public static bool MoveActor(IDolphinService dolphin, uint actor, Vector3 pos, short angleY)
    {
        var p = new byte[12];
        BinaryPrimitives.WriteSingleBigEndian(p.AsSpan(0), pos.X);
        BinaryPrimitives.WriteSingleBigEndian(p.AsSpan(4), pos.Y);
        BinaryPrimitives.WriteSingleBigEndian(p.AsSpan(8), pos.Z);
        bool ok = dolphin.WriteMemory(actor + GameMemoryAddresses.Warp.ActorOffPos, p);
        ok &= dolphin.WriteMemory(actor + GameMemoryAddresses.Warp.ActorOffOldPos, p);

        var a = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(a, angleY);
        ok &= dolphin.WriteMemory(actor + GameMemoryAddresses.Warp.ActorOffShapeAngleY, a);
        ok &= dolphin.WriteMemory(actor + GameMemoryAddresses.Warp.ActorOffAngleY, a);

        ok &= dolphin.WriteMemory(actor + GameMemoryAddresses.Warp.ActorOffSpeed, new byte[12]);
        ok &= dolphin.WriteMemory(actor + GameMemoryAddresses.Warp.ActorOffSpeedF, new byte[4]);
        return ok;
    }

    /// <summary>An actor's current.pos, or null if unreadable / not finite.</summary>
    public static Vector3? ReadActorPos(IDolphinService dolphin, uint actor)
    {
        var b = dolphin.ReadMemory(actor + GameMemoryAddresses.Warp.ActorOffPos, 12);
        if (b is not { Length: 12 })
            return null;
        var v = new Vector3(BinaryPrimitives.ReadSingleBigEndian(b.AsSpan(0)),
                            BinaryPrimitives.ReadSingleBigEndian(b.AsSpan(4)),
                            BinaryPrimitives.ReadSingleBigEndian(b.AsSpan(8)));
        return v.IsFinite() ? v : null;
    }

    public static float Distance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// Why the local player can't be warped to right now (or be warped anywhere), from game memory; see
    /// <see cref="WarpBusy"/>. <paramref name="minigame"/> comes from a <see cref="MinigameGate"/>.
    /// </summary>
    public static WarpBusy ReadBusy(IDolphinService dolphin, bool minigame)
    {
        uint link = ReadU32(dolphin, GameMemoryAddresses.Warp.LinkActorPtr);
        if (link == 0 || IsStageChangePending(dolphin))
            return WarpBusy.Loading;
        if (dolphin.ReadMemory(GameMemoryAddresses.Events.EventMode, 1) is not [0])
            return WarpBusy.Event;
        if (dolphin.Read(GameMemoryAddresses.Player.CurrentHealth) is 0)
            return WarpBusy.Dead;
        if (ReadU32(dolphin, GameMemoryAddresses.Warp.ControlledActorPtr) is var controlled && controlled != 0 && controlled != link)
            return WarpBusy.OtherCharacter;
        if (minigame)
            return WarpBusy.Minigame;
        return WarpBusy.None;
    }

    public static uint ReadU32(IDolphinService dolphin, uint address) =>
        dolphin.ReadMemory(address, 4) is { Length: 4 } b ? BinaryPrimitives.ReadUInt32BigEndian(b) : 0u;
}

/// <summary>
/// Remembers how the local player last really entered the current stage. play.mCurStage says so after a
/// door or a loading zone, but a void-out or a game over (point -1), a ship interior's exit (-2) or the Song
/// of Passing (-3) restart the stage without a spawn point; those keep the last real entrance, which is
/// still a valid way into the same stage. A new stage entered by a restart has none (-1).
/// </summary>
public sealed class WarpEntryTracker
{
    private string _stage = "";
    private StageEntry? _entry;

    /// <summary>Feed play.mCurStage; returns the entrance to broadcast (null = unknown).</summary>
    public StageEntry? Update(StageEntry current)
    {
        if (current.Point >= 0 && current.Room >= 0)
        {
            _stage = current.Name;
            _entry = current;
        }
        else if (current.Name != _stage)
        {
            _stage = current.Name;
            _entry = null;
        }
        return _entry;
    }
}

/// <summary>
/// Builds the local <see cref="WarpInfo"/> every puppet tick (PuppetSyncService.ReadLocalPlayerState):
/// the entrance from <see cref="WarpEntryTracker"/> and why nobody should warp here right now.
/// </summary>
public sealed class LocalWarpReader
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<LocalWarpReader>();

    private readonly WarpEntryTracker _tracker = new();
    private readonly MinigameGate _minigame = new();
    private StageEntry? _loggedEntry;
    private WarpBusy _loggedBusy;

    public WarpInfo? Read(IDolphinService dolphin, string stage)
    {
        var mg = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.MiniGameType, 1);
        bool minigame = _minigame.Update(mg is { Length: 1 } ? mg[0] : (byte)0, stage);
        if (WarpMemory.ReadStartStage(dolphin) is not { } start)
            return null;

        var entry = _tracker.Update(start);
        var busy = WarpMemory.ReadBusy(dolphin, minigame);
        if (entry != _loggedEntry)
        {
            _loggedEntry = entry;
            Logger.Information("[warp] local entrance: {Entry} (mCurStage {Stage} point {Point} room {Room} layer {Layer})",
                entry is { } e ? $"{e.Name} point {e.Point} room {e.Room} layer {e.Layer}" : "unknown",
                start.Name, start.Point, start.Room, start.Layer);
        }
        if (busy != _loggedBusy)
        {
            _loggedBusy = busy;
            Logger.Debug("[warp] local busy: {Busy}", busy);
        }
        return new WarpInfo
        {
            EntryPoint = entry?.Point ?? -1,
            EntryRoom = entry?.Room ?? -1,
            Layer = entry?.Layer ?? -1,
            Busy = busy,
        };
    }
}
