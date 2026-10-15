using System.Runtime.InteropServices;

namespace WWOnline.Native;

// tww.h "boat (fan-out: boat)": the King of Red Lions. Game thread only, between tww_run_frame calls.
public static unsafe partial class TwwNative
{
    [LibraryImport(LibraryName)] public static partial int tww_boat_get_local(TwwBoatState* state);
    [LibraryImport(LibraryName)] public static partial void tww_boat_set_peer(int slot, TwwBoatState* boat, TwwBoatLook* look);
    [LibraryImport(LibraryName)] public static partial void tww_boat_set_local_look(TwwBoatLook* look);
}

/// <summary>The boat half of tww.h, implemented by the same API object as <see cref="ITwwApi"/>.</summary>
public interface ITwwBoatApi
{
    /// <returns>The local boat is in this stage, ridden or parked.</returns>
    bool GetLocalBoat(out TwwBoatState state);
    /// <param name="boat">Null: the slot has no boat (it is deleted).</param>
    /// <param name="look">Null: the vanilla colours.</param>
    void SetPeerBoat(int slot, TwwBoatState? boat, TwwBoatLook? look);
    /// <param name="look">Null: the vanilla colours.</param>
    void SetLocalBoatLook(TwwBoatLook? look);
}

/// <summary>TWW_BOAT_* (TwwBoatState.flags).</summary>
[Flags]
public enum TwwBoatFlags : uint
{
    None = 0,
    /// <summary>Nobody aboard: drawn empty where it is, speed 0.</summary>
    Parked = 1u << 0,
    /// <summary>daSFLG_FLY_e: airborne, pos[1] is used.</summary>
    Flying = 1u << 1,
    /// <summary>The mast bck is FN_MAST_ON2 (raised / rising), else FN_MAST_OFF2.</summary>
    MastRaised = 1u << 2,
    /// <summary>m03E8 0.001: the mast scaled away, the cannon or crane out.</summary>
    MastHidden = 1u << 3,
}

/// <summary>TwwBoatState (52 bytes): a King of Red Lions (daShip_c).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwBoatState
{
    /// <summary>current.pos</summary>
    public TwwFloat3 Pos;
    /// <summary>speedF (0 while parked)</summary>
    public float SpeedF;
    /// <summary>shape_angle.y</summary>
    public short AngleY;
    /// <summary>mSailAngle</summary>
    public short SailAngle;
    /// <summary>m0366</summary>
    public short Tiller;
    /// <summary>m03A0: head look pitch</summary>
    public short HeadX;
    /// <summary>m03A2: head look yaw</summary>
    public short HeadY;
    /// <summary>m0394</summary>
    public short CannonYaw;
    /// <summary>m0396</summary>
    public short CannonPitch;
    /// <summary>m0398 + m039C</summary>
    public short CraneAngle;
    public TwwBoatFlags Flags;
    /// <summary>mPart (daShip_c::Part_e)</summary>
    public byte Part;
    /// <summary>mpBodyAnm's frame in the mast bck, whole frames</summary>
    public byte MastFrame;
    /// <summary>m03B4, the head's "Ship" bck index; 0 = unknown (the receiver keeps its own)</summary>
    public byte HeadBck;
    /// <summary>mpHeadAnm's frame, whole frames</summary>
    public byte HeadFrame;
    /// <summary>mRopeCnt, 10-unit segments</summary>
    public byte RopeLength;
    /// <summary>current.roomNo, -1 if none</summary>
    public sbyte Room;
    /// <summary>uint8_t pad0[2]</summary>
    public ushort Pad0;
    /// <summary>A peer's boat shows only while this equals the local stage.</summary>
    public TwwStageName Stage;
}

/// <summary>TwwBoatLook (8 bytes): a boat's colours.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwBoatLook
{
    /// <summary>The hull's red, RGB565; 0 = the classic red.</summary>
    public ushort HullRgb565;
    /// <summary>uint8_t pad0[2]</summary>
    public ushort Pad0;
    /// <summary>0xRRGGBB on the sail's canvas (the tunic colour); 0 = the vanilla sail.</summary>
    public uint SailRgb;
}

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwBoatApi
    {
        public bool GetLocalBoat(out TwwBoatState state)
        {
            state = default;
            fixed (TwwBoatState* p = &state) return TwwNative.tww_boat_get_local(p) == 1;
        }

        public void SetPeerBoat(int slot, TwwBoatState? boat, TwwBoatLook? look)
        {
            TwwBoatState b = boat.GetValueOrDefault();
            TwwBoatLook l = look.GetValueOrDefault();
            TwwNative.tww_boat_set_peer(slot, boat.HasValue ? &b : null, look.HasValue ? &l : null);
        }

        public void SetLocalBoatLook(TwwBoatLook? look)
        {
            TwwBoatLook l = look.GetValueOrDefault();
            TwwNative.tww_boat_set_local_look(look.HasValue ? &l : null);
        }
    }
}
