using System.Buffers.Binary;
using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The scene facts the sync services gate on and a warp needs, without addresses: read from Dolphin's memory
/// (<see cref="FromMemory"/>) or from the native game's typed call (<see cref="FromNative"/>). Get one with
/// <see cref="GameSceneExtensions.ReadScene"/>.
/// </summary>
/// <param name="LinkPresent">The Link actor exists (play.mAttention's player: <see cref="GameMemoryAddresses.Player.LinkActorPointer"/>).</param>
/// <param name="LinkActor">Which Link (play.mpPlayerPtr[0]; natively 1): a change means a new Link, 0 means none.</param>
/// <param name="StartEntry">play.mCurStage: the current stage and the point / room / layer it was entered with.</param>
/// <param name="Room">The room Link is in (dStage_roomControl_c::mStayNo).</param>
/// <param name="NextStagePending">play.mNextStage.mEnable: a stage change is pending.</param>
/// <param name="EventMode">play.mEvtCtrl.mMode: nonzero while an event runs (a talk, a shop, a cutscene).</param>
/// <param name="MenuPaused">The pause / item menu is open (dMenu_pause).</param>
/// <param name="MiniGameType">play.mMiniGameType</param>
/// <param name="OtherCharacter">The player controls another actor than Link (Medli, Makar, a seagull, Hyoi).</param>
/// <param name="PlayerStatus0">play.mPlayerStatus[0][0]</param>
/// <param name="LinkModeFlg">daPy_lk_c::mModeFlg, 0 without Link.</param>
/// <param name="LinkProc">daPy_lk_c::mCurProc, 0 without Link.</param>
public sealed record GameScene(
    bool LinkPresent, uint LinkActor, StageEntry StartEntry, sbyte Room, bool NextStagePending, byte EventMode,
    bool MenuPaused, byte MiniGameType, bool OtherCharacter, uint PlayerStatus0, uint LinkModeFlg, uint LinkProc)
{
    /// <summary>The current stage ("" before the first).</summary>
    public string Stage => StartEntry.Name;

    /// <summary>No event running and the pause menu closed: safe to rewrite save data the game itself only
    /// changes from an event (a bag, a figurine).</summary>
    public bool IsIdle => EventMode == 0 && !MenuPaused;

    /// <summary>The Dolphin game's memory; null if a read failed.</summary>
    public static GameScene? FromMemory(IDolphinService dolphin)
    {
        const uint start = GameMemoryAddresses.Warp.StartStage;
        const int stageBlock = (int)(GameMemoryAddresses.Warp.NextStage - start) + GameMemoryAddresses.Warp.NextStageOffEnable + 1;
        var link = dolphin.Read(GameMemoryAddresses.Player.LinkActorPointer);
        var stage = dolphin.ReadMemory(start, stageBlock);
        var players = dolphin.ReadMemory(GameMemoryAddresses.Warp.ControlledActorPtr, 12); // mPlayerInfo[0].mpPlayer .. mpPlayerPtr[0]
        var eventMode = dolphin.ReadMemory(GameMemoryAddresses.Events.EventMode, 1);
        var menu = dolphin.ReadMemory(GameMemoryAddresses.Events.MenuPause, 1);
        var miniGame = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.MiniGameType, 1);
        var room = dolphin.Read(GameMemoryAddresses.Stage.CurrentRoomNumber);
        var status = dolphin.ReadMemory(GameMemoryAddresses.Player.PlayerStatusBitfield0.Address, 4);
        if (link is null || stage is not { Length: stageBlock } || players is not { Length: 12 } || eventMode is not [var mode] ||
            menu is not [var paused] || miniGame is not [var mg] || room is null || status is not { Length: 4 })
            return null;

        uint controlled = BinaryPrimitives.ReadUInt32BigEndian(players);
        uint linkActor = BinaryPrimitives.ReadUInt32BigEndian(players.AsSpan(8));
        uint modeFlg = 0, proc = 0;
        if (linkActor != 0)
        {
            modeFlg = WarpMemory.ReadU32(dolphin, linkActor + PuppetLayout.DAPY_OFF_MODE_FLG);
            proc = WarpMemory.ReadU32(dolphin, linkActor + PuppetLayout.DAPY_OFF_CUR_PROC);
        }
        var entry = new StageEntry(
            WarpMemory.StageName(stage.AsSpan(GameMemoryAddresses.Warp.StageOffName, GameMemoryAddresses.Warp.StageNameSize)),
            BinaryPrimitives.ReadInt16BigEndian(stage.AsSpan(GameMemoryAddresses.Warp.StageOffPoint)),
            (sbyte)stage[GameMemoryAddresses.Warp.StageOffRoom],
            (sbyte)stage[GameMemoryAddresses.Warp.StageOffLayer]);
        return new GameScene(link.Value != 0, linkActor, entry, (sbyte)room.Value, stage[^1] != 0, mode, paused != 0, mg,
            controlled != 0 && controlled != linkActor, BinaryPrimitives.ReadUInt32BigEndian(status), modeFlg, proc);
    }

    /// <summary>The native game's tww_gate_get_state, and its local state for Link's proc.</summary>
    public static GameScene FromNative(in TwwGateState gate, in TwwLocalState local) => new(
        gate.Link != 0, gate.Link,
        new StageEntry(gate.Stage.ToString(), gate.StartPoint, gate.StartRoom, gate.StartLayer),
        gate.Room, gate.NextStage != 0, gate.EventMode, gate.MenuPaused != 0, gate.MiniGame, gate.OtherCharacter != 0,
        gate.PlayerStatus0, gate.LinkModeFlg, local.Valid != 0 ? local.Proc : 0);
}

/// <summary>A game backend that reads <see cref="GameScene"/> through a typed call instead of addresses.</summary>
public interface IGameSceneSource
{
    GameScene? ReadScene();
}

public static class GameSceneExtensions
{
    /// <summary>The scene now, from whichever backend <paramref name="dolphin"/> is; null if it can't be read.</summary>
    public static GameScene? ReadScene(this IDolphinService dolphin) =>
        dolphin is IGameSceneSource source ? source.ReadScene() : GameScene.FromMemory(dolphin);
}
