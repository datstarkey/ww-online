using System.Runtime.InteropServices;

namespace WWOnline.Native;

// tww.h "gate (fan-out: gate)": the scene facts and the warp. Game thread only, between tww_run_frame calls.
public static unsafe partial class TwwNative
{
    [LibraryImport(LibraryName)] public static partial void tww_gate_get_state(TwwGateState* state);
    [LibraryImport(LibraryName)] public static partial int tww_gate_warp_local(float* pos, short angleY);
    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tww_gate_change_stage(string stage, short point, sbyte room, sbyte layer);
}

/// <summary>The gate half of tww.h: reach it through <see cref="INativeGame.Invoke{T}"/> (the API object implements it).</summary>
public interface ITwwGateApi
{
    TwwGateState GetGateState();
    /// <summary>Move the local Link within his room. False: no Link, or a stage change is pending.</summary>
    bool WarpLocal(float x, float y, float z, short angleY);
    /// <summary>dComIfGp_setNextStage. False: no Link, a change is already pending, or not a stage name.</summary>
    bool ChangeStage(string stage, short point, sbyte room, sbyte layer);
}

/// <summary>TwwGateState (28 bytes): the scene facts the sync services gate on.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwGateState
{
    /// <summary>play.mCurStage's name: the current stage, "" before the first.</summary>
    public TwwStageName Stage;
    /// <summary>play.mCurStage: the point, room and layer the stage was entered with.</summary>
    public short StartPoint;
    public sbyte StartRoom;
    public sbyte StartLayer;
    /// <summary>dStage_roomControl_c::mStayNo, -1 if none.</summary>
    public sbyte Room;
    /// <summary>1: the Link actor exists and runs.</summary>
    public byte Link;
    /// <summary>play.mNextStage.mEnable: a stage change is pending.</summary>
    public byte NextStage;
    /// <summary>play.mEvtCtrl.mMode: nonzero while an event runs.</summary>
    public byte EventMode;
    /// <summary>dMenu_flag(): the pause / item menu is open.</summary>
    public byte MenuPaused;
    /// <summary>play.mMiniGameType</summary>
    public byte MiniGame;
    /// <summary>1: the player controls another actor than Link.</summary>
    public byte OtherCharacter;
    public byte Pad0;
    /// <summary>play.mPlayerStatus[0][0]</summary>
    public uint PlayerStatus0;
    /// <summary>daPy_lk_c::mModeFlg, 0 without Link.</summary>
    public uint LinkModeFlg;
}
