using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// A one-shot event about one of a player's projectiles (their bomb, boat-cannon shot or arrow),
/// sent once, reliably, to the players who can see them (GameHub.SendPlayerEvent). The receiver's
/// puppet REL makes it real in their world: it spawns the projectile at the sender's state, and an
/// explosion snaps it to where the sender's own exploded (GameMod puppet_fx.c; the in-game event
/// layout is puppet_shared.h PUPPET_FX_*, which <see cref="Kind"/> values mirror).
/// Allowed only while the room's <see cref="RoomSettings.SharedProjectiles"/> rule is on.
/// </summary>
public class PlayerEvent
{
    /// <summary>Set by the server: the sender's connection id.</summary>
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    /// <summary>The sending app's id for its events: random per app run, the same across reconnects (a
    /// new connection id). With <see cref="Seq"/> it identifies an event, so one re-sent after a
    /// reconnect is still recognised as a repeat. 1-<see cref="MaxOriginLength"/> ASCII letters / digits.</summary>
    [JsonPropertyName("origin")]
    public string Origin { get; set; } = "";

    /// <summary>The sender's event counter (+1 per event, per <see cref="Origin"/>): receivers drop an event
    /// whose seq they already had from that origin, so a repeat can't replay a throw or an explosion.</summary>
    [JsonPropertyName("seq")]
    public uint Seq { get; set; }

    /// <summary>What happened (<see cref="PlayerEventKind"/>).</summary>
    [JsonPropertyName("kind")]
    public byte Kind { get; set; }

    /// <summary>The sender's projectile: its actor's process id in the sender's game. With the sender
    /// it keys the receiver's copy, so a throw, an explosion and a duplicate all find the same one.</summary>
    [JsonPropertyName("id")]
    public uint Id { get; set; }

    /// <summary>Arrows: the arrow type (<see cref="ArrowNormal"/> .. <see cref="ArrowLight"/>); 0 for bombs and cannonballs.</summary>
    [JsonPropertyName("variant")]
    public byte Variant { get; set; }

    /// <summary>The sender's stage and room when it happened.</summary>
    [JsonPropertyName("stageName")]
    public string StageName { get; set; } = "";

    [JsonPropertyName("roomNumber")]
    public byte RoomNumber { get; set; }

    /// <summary>The projectile's position (current.pos): where it flies from, or where it exploded.</summary>
    [JsonPropertyName("position")]
    public Vector3 Position { get; set; } = new();

    /// <summary>speedF / speed.y / gravity of the projectile (fopAc_ac_c), for a throw or a shot.</summary>
    [JsonPropertyName("speedF")]
    public float SpeedF { get; set; }

    [JsonPropertyName("speedY")]
    public float SpeedY { get; set; }

    [JsonPropertyName("gravity")]
    public float Gravity { get; set; }

    /// <summary>current.angle (x = pitch, y = heading) and the shape's roll.</summary>
    [JsonPropertyName("angleX")]
    public short AngleX { get; set; }

    [JsonPropertyName("angleY")]
    public short AngleY { get; set; }

    [JsonPropertyName("angleZ")]
    public short AngleZ { get; set; }

    /// <summary>A bomb's fuse (frames left, daBomb_c::mRestTime), or a cannonball's frames without gravity.</summary>
    [JsonPropertyName("timer")]
    public short Timer { get; set; }

    /// <summary>Largest |coordinate| accepted (as <see cref="BoatState.MaxCoordinate"/>).</summary>
    public const float MaxCoordinate = 1.0e6f;

    /// <summary>Largest |speed| accepted: arrows fly at 200 a frame (d_a_arrow.cpp arrowShooting), cannonballs at 110.</summary>
    public const float MaxSpeed = 1000f;

    /// <summary>Largest |gravity| accepted: bombs fall at -2.9, cannonballs at -2.5 (d_a_bomb3.inc:1452, d_a_ship.cpp:95).</summary>
    public const float MaxGravity = 50f;

    /// <summary>Largest timer accepted: a new bomb's fuse is 150 frames (d_a_bomb3.inc:1454); enemies set up to 200.</summary>
    public const short MaxTimer = 600;

    /// <summary>Longest stage name (the game's stage names are at most 7 characters + NUL).</summary>
    public const int MaxStageNameLength = 8;

    /// <summary>Longest <see cref="Origin"/> (a GUID without dashes).</summary>
    public const int MaxOriginLength = 32;

    /// <summary>daArrow_c::mArrowType (d_a_arrow.h): normal, fire, ice, light (puppet_shared.h PUPPET_FX_ARROW_TYPE_MAX = light).</summary>
    public const byte ArrowNormal = 0;
    public const byte ArrowFire = 1;
    public const byte ArrowIce = 2;
    public const byte ArrowLight = 3;

    public static bool IsKnownKind(byte kind) => kind is >= (byte)PlayerEventKind.BombThrow and <= (byte)PlayerEventKind.Max;

    /// <summary>
    /// Every field in range: a known kind, a finite position inside the world, finite bounded
    /// speeds and gravity, a timer in 0..<see cref="MaxTimer"/>, an arrow type only on an arrow, and a
    /// stage name. Ranges only, never Math.Abs on the client's integers.
    /// </summary>
    public bool IsValid()
    {
        if (!IsKnownKind(Kind)) return false;
        if (Position == null || !Position.IsFinite()) return false;
        if (!InRange(Position.X, MaxCoordinate) || !InRange(Position.Y, MaxCoordinate) || !InRange(Position.Z, MaxCoordinate))
            return false;
        if (!InRange(SpeedF, MaxSpeed) || !InRange(SpeedY, MaxSpeed) || !InRange(Gravity, MaxGravity)) return false;
        if (Timer < 0 || Timer > MaxTimer) return false;
        if (Kind == (byte)PlayerEventKind.Arrow ? Variant > ArrowLight : Variant != 0) return false;
        if (string.IsNullOrEmpty(StageName) || StageName.Length > MaxStageNameLength) return false;
        foreach (char c in StageName)
        {
            if (c < '!' || c > '~') return false;
        }
        if (string.IsNullOrEmpty(Origin) || Origin.Length > MaxOriginLength) return false;
        foreach (char c in Origin)
        {
            if (!char.IsAsciiLetterOrDigit(c)) return false;
        }
        return true;
    }

    /// <summary>Finite and strictly inside ±<paramref name="limit"/> (false for NaN / Infinity).</summary>
    private static bool InRange(float value, float limit) => value > -limit && value < limit;

    /// <summary>For logs, safe on an unvalidated event: no null dereference, and the stage name is cut to
    /// <see cref="MaxStageNameLength"/> printable characters (a client can't put newlines or a long string in the log).</summary>
    public override string ToString()
    {
        var stage = new string((StageName ?? "").Take(MaxStageNameLength).Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
        var at = Position == null ? "no position" : $"{Position.X:F0},{Position.Y:F0},{Position.Z:F0}";
        return $"{(PlayerEventKind)Kind} #{Id} at {stage}:{RoomNumber} ({at})";
    }
}

/// <summary>
/// <see cref="PlayerEvent.Kind"/> values: the same numbers as puppet_shared.h PUPPET_FX_KIND_* (a test
/// keeps them equal).
/// </summary>
public enum PlayerEventKind : byte
{
    /// <summary>A bomb left the sender's hands (thrown or put down): the receiver spawns a lit bomb in flight.</summary>
    BombThrow = 1,

    /// <summary>The sender picked their bomb up again: the receiver's copy goes (their puppet shows it carried).</summary>
    BombPickup = 2,

    /// <summary>The sender's bomb or cannonball exploded here: the copy explodes there (or an explosion appears).</summary>
    Explode = 3,

    /// <summary>The sender's bomb or cannonball is gone without exploding (it sank): the copy goes too.</summary>
    Remove = 4,

    /// <summary>The sender's boat cannon fired: the receiver spawns the cannonball.</summary>
    Cannon = 5,

    /// <summary>The sender shot an arrow (<see cref="PlayerEvent.Variant"/> = its type): the receiver spawns it in flight.</summary>
    Arrow = 6,

    /// <summary>Highest known kind.</summary>
    Max = Arrow,
}
