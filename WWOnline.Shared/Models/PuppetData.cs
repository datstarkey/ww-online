using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// Data structure for puppet player synchronization
/// Contains all necessary information to render remote players
/// </summary>
public class PuppetData
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; } = "";

    [JsonPropertyName("position")]
    public Vector3 Position { get; set; } = new();

    [JsonPropertyName("rotation")]
    public float Rotation { get; set; }

    [JsonPropertyName("animation")]
    public AnimationState Animation { get; set; } = new();

    [JsonPropertyName("equipment")]
    public EquipmentState Equipment { get; set; } = new();

    [JsonPropertyName("action")]
    public ActionState Action { get; set; } = new();

    [JsonPropertyName("appearance")]
    public AppearanceState Appearance { get; set; } = new();

    [JsonPropertyName("stageName")]
    public string StageName { get; set; } = "";

    [JsonPropertyName("roomNumber")]
    public byte RoomNumber { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>The player's King of Red Lions while they ride it; null otherwise.</summary>
    [JsonPropertyName("boat")]
    public BoatState? Boat { get; set; }

    public bool IsValid()
    {
        if (Position == null) return false;
        if (Boat != null && !Boat.IsValid()) return false;
        if (Equipment != null && Equipment.GrabKind > EquipmentState.MaxGrabKind) return false;
        return Position.IsFinite() && float.IsFinite(Rotation);
    }
}

/// <summary>
/// A player's King of Red Lions (daShip_c, d_a_ship.h). Receivers draw their own copy of the
/// boat here and seat the player's puppet in it.
/// </summary>
public class BoatState
{
    /// <summary>current.pos. Y only matters while <see cref="Flying"/>; otherwise the receiver's sea sets it.</summary>
    [JsonPropertyName("position")]
    public Vector3 Position { get; set; } = new();

    /// <summary>shape_angle.y.</summary>
    [JsonPropertyName("rotation")]
    public short Rotation { get; set; }

    /// <summary>speedF: forward speed, used to predict between updates.</summary>
    [JsonPropertyName("speedF")]
    public float SpeedF { get; set; }

    /// <summary>mStateFlag daSFLG_FLY_e: airborne (a ramp or cannon jump).</summary>
    [JsonPropertyName("flying")]
    public bool Flying { get; set; }

    // The pose, as daShip_c's body/head joint callbacks and its two morfs use it (d_a_ship.cpp:110-231).

    /// <summary>m0392 == FN_MAST_ON2: the mast bck is the raise (else FN_MAST_OFF2, the lower).</summary>
    [JsonPropertyName("mastRaised")]
    public bool MastRaised { get; set; }

    /// <summary>m03E8 == 0.001: the mast is scaled away because the cannon or crane is out.</summary>
    [JsonPropertyName("mastHidden")]
    public bool MastHidden { get; set; }

    /// <summary>mpBodyAnm's frame in the mast bck (whole frames).</summary>
    [JsonPropertyName("mastFrame")]
    public byte MastFrame { get; set; }

    /// <summary>mSailAngle: the sail yard's turn toward the wind.</summary>
    [JsonPropertyName("sailAngle")]
    public short SailAngle { get; set; }

    /// <summary>m0366: the tiller / rudder angle.</summary>
    [JsonPropertyName("tiller")]
    public short Tiller { get; set; }

    /// <summary>m03A0 / m03A2: the head's look pitch / yaw.</summary>
    [JsonPropertyName("headX")]
    public short HeadX { get; set; }

    [JsonPropertyName("headY")]
    public short HeadY { get; set; }

    /// <summary>m03B4: the head's bck (a "Ship" archive index); 0 = unknown, the receiver keeps its own.</summary>
    [JsonPropertyName("headBck")]
    public byte HeadBck { get; set; }

    /// <summary>mpHeadAnm's frame in the head bck (whole frames).</summary>
    [JsonPropertyName("headFrame")]
    public byte HeadFrame { get; set; }

    /// <summary>Largest |coordinate| / |speedF| accepted: the REL drops anything outside these
    /// anyway (puppet_boat.c BOAT_MAX_COORD / BOAT_MAX_SPEED), so the server rejects it too.</summary>
    public const float MaxCoordinate = 1.0e6f;
    public const float MaxSpeed = 1000f;

    /// <summary>setSailAngle eases mSailAngle toward ±0x1555 (d_a_ship.cpp:767-783).</summary>
    public const short MaxSailAngle = 0x1555;
    /// <summary>Every m0366 target is within ±0x2000 (d_a_ship.cpp:1399, 1625, 2084-2093, 3686).</summary>
    public const short MaxTiller = 0x2000;
    /// <summary>m03A0 eases toward a target clamped to -0x3000..0x2000, then / 6 (d_a_ship.cpp:4171-4197).</summary>
    public const short MinHeadX = -0x3000 / 6;
    public const short MaxHeadX = 0x2000 / 6;
    /// <summary>m03A2: target clamped to ±0x7800, then / 6 (d_a_ship.cpp:4180-4201).</summary>
    public const short MaxHeadY = 0x7800 / 6;

    /// <summary>"Ship" archive bck indices (Ship.h dRes_INDEX_SHIP_BCK_*; SHIP_BCK_* in puppet_shared.h).
    /// The head plays any bck but the mast's two.</summary>
    public const byte FirstShipBck = 0x05;
    public const byte LastShipBck = 0x0E;
    public const byte MastOffBck = 0x0A;
    public const byte MastOnBck = 0x0B;

    public static bool IsHeadBck(int bck) =>
        bck is >= FirstShipBck and <= LastShipBck && bck != MastOffBck && bck != MastOnBck;

    public bool IsValid() =>
        Position != null && Position.IsFinite() && float.IsFinite(SpeedF) &&
        Math.Abs(Position.X) < MaxCoordinate && Math.Abs(Position.Y) < MaxCoordinate &&
        Math.Abs(Position.Z) < MaxCoordinate && Math.Abs(SpeedF) < MaxSpeed &&
        // Ranges, not Math.Abs: Math.Abs(short.MinValue) throws.
        SailAngle >= -MaxSailAngle && SailAngle <= MaxSailAngle &&
        Tiller >= -MaxTiller && Tiller <= MaxTiller &&
        HeadX >= MinHeadX && HeadX <= MaxHeadX &&
        HeadY >= -MaxHeadY && HeadY <= MaxHeadY &&
        (HeadBck == 0 || IsHeadBck(HeadBck));
}

public class Vector3
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("z")]
    public float Z { get; set; }

    public Vector3() { }

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public bool IsFinite() => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

    public override bool Equals(object? obj) => obj is Vector3 other && X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public static bool operator ==(Vector3? a, Vector3? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(Vector3? a, Vector3? b) => !(a == b);
}

/// <summary>
/// Animation state for puppet rendering
/// </summary>
public class AnimationState
{
    [JsonPropertyName("upperBodyAnim")]
    public ushort UpperBodyAnimation { get; set; }

    [JsonPropertyName("lowerBodyAnim")]
    public ushort LowerBodyAnimation { get; set; }

    [JsonPropertyName("eyeAnim")]
    public ushort EyeAnimation { get; set; }

    [JsonPropertyName("animationSpeed")]
    public float AnimationSpeed { get; set; } = 1.0f;
}

/// <summary>
/// Equipment visibility state
/// </summary>
public class EquipmentState
{
    [JsonPropertyName("swordId")]
    public byte SwordId { get; set; }

    [JsonPropertyName("shieldId")]
    public byte ShieldId { get; set; }

    [JsonPropertyName("itemInHand")]
    public ushort ItemInHand { get; set; }

    [JsonPropertyName("leftHandItem")]
    public uint LeftHandItem { get; set; }

    [JsonPropertyName("bottleContents")]
    public uint BottleContents { get; set; }

    /// <summary>
    /// What the player carries (mActorKeepGrab): 0 = nothing the puppet draws, 1 = a bomb
    /// (puppet_shared.h PUPPET_GRAB_KIND_*). Receivers draw a bomb between the puppet's hands.
    /// </summary>
    [JsonPropertyName("grabKind")]
    public byte GrabKind { get; set; }

    /// <summary>Highest <see cref="GrabKind"/> (puppet_shared.h PUPPET_GRAB_KIND_MAX; a test keeps them equal).</summary>
    public const byte MaxGrabKind = 1;
}

/// <summary>
/// Visual appearance state for puppet customization
/// </summary>
public class AppearanceState
{
    [JsonPropertyName("clothesType")]
    public byte ClothesType { get; set; } // 0 = hero tunic, 1 = casual pajamas, 2 = follow that player's save

    [JsonPropertyName("colorR")]
    public byte ColorR { get; set; } = 30;  // 30,100,35 (or 0,0,0) = the vanilla tunic (puppet_shared.h TUNIC_COLOR_DEFAULT_*)

    [JsonPropertyName("colorG")]
    public byte ColorG { get; set; } = 100;

    [JsonPropertyName("colorB")]
    public byte ColorB { get; set; } = 35;
}

/// <summary>
/// Current action state
/// </summary>
public class ActionState
{
    [JsonPropertyName("attackState")]
    public byte AttackState { get; set; }

    /// <summary>
    /// Raw mCurProc value from daPy_lk_c (Link's state-machine proc id).
    /// Values from daPyProc enum: 0x04=WAIT, 0x06=MOVE, 0x1E=FRONT_ROLL, 0x21=SIDE_ROLL,
    /// 0x22=BACK_JUMP, 0x24=AUTO_JUMP, 0x27=FALL, 0x36=SWIM_WAIT, 0x37=SWIM_MOVE, 0x66=DAMAGE, etc.
    /// The puppet dispatches on this to pick the matching proc*_init.
    /// </summary>
    [JsonPropertyName("curProc")]
    public byte CurProc { get; set; }

    [JsonPropertyName("stateFlags")]
    public uint StateFlags { get; set; }

    [JsonPropertyName("stateFlags2")]
    public uint StateFlags2 { get; set; }

    [JsonPropertyName("velocity")]
    public float Velocity { get; set; }

    /// <summary>speedF from Link's fopAc_ac_c (+0x254). Drives animation playback speed.</summary>
    [JsonPropertyName("speedF")]
    public float SpeedF { get; set; }

    /// <summary>mMaxNormalSpeed from Link's daPy_lk_c (+0x2A8). Ceiling for walk/run blend.</summary>
    [JsonPropertyName("maxNormalSpeed")]
    public float MaxNormalSpeed { get; set; }

    /// <summary>mStickDistance from Link's daPy_lk_c (+0x35B0). 0..1 blend input.</summary>
    [JsonPropertyName("stickDistance")]
    public float StickDistance { get; set; }

    /// <summary>mModeFlg (+0x3618): swim, climb, ride, ship, grappling, etc.</summary>
    [JsonPropertyName("modeFlg")]
    public uint ModeFlg { get; set; }

    /// <summary>mNoResetFlg0 (+0x29C): hover boots, heavy boots, push-pull-keep, no-draw, etc.</summary>
    [JsonPropertyName("noResetFlg0")]
    public uint NoResetFlg0 { get; set; }

    /// <summary>mNoResetFlg1 (+0x2A0): casual clothes, frozen, ship tact, etc.</summary>
    [JsonPropertyName("noResetFlg1")]
    public uint NoResetFlg1 { get; set; }

    /// <summary>PUPPET_ACTION_FLAG_* bits (puppet_shared.h), e.g. 0x01 = guarding.</summary>
    [JsonPropertyName("actionFlags")]
    public byte ActionFlags { get; set; }

    /// <summary>m34E8 (+0x34E8): stick direction incl. camera — drives Z-target (ATN_MOVE) strafing.</summary>
    [JsonPropertyName("stickAngle")]
    public short StickAngle { get; set; }

    /// <summary>Bumps whenever the peer's proc (re)starts — lets the puppet replay a combo swing
    /// that re-inits the same proc id.</summary>
    [JsonPropertyName("procSeq")]
    public byte ProcSeq { get; set; }

    /// <summary>mBodyAngle.x (+0x2B4): aim pitch while aiming (bow, boomerang, hookshot, telescope).</summary>
    [JsonPropertyName("bodyAngleX")]
    public short BodyAngleX { get; set; }

    /// <summary>mBodyAngle.y (+0x2B6): aim yaw, relative to the facing.</summary>
    [JsonPropertyName("bodyAngleY")]
    public short BodyAngleY { get; set; }

    [JsonPropertyName("pressedButtons")]
    public byte PressedButtons { get; set; }

    [JsonPropertyName("isSwimming")]
    public bool IsSwimming { get; set; }

    [JsonPropertyName("isClimbing")]
    public bool IsClimbing { get; set; }

    [JsonPropertyName("isHoldingItem")]
    public bool IsHoldingItem { get; set; }
}
