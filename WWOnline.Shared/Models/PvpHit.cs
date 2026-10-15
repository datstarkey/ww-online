using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// A hit the sender's Link landed on another player's puppet (PVP, the room's <see cref="RoomSettings.Pvp"/> rule).
/// Victim-authoritative: the attacker's game reports it (native tww_pvp_poll_hit), the server relays it to the player
/// who was hit (GameHub.SendPvpHit, PvpHitGate) and their game applies it to their own Link as the same attack from
/// the attacker's puppet (tww_pvp_apply_hit). Only between clients with <see cref="ClientCapabilities.PvpHits"/>.
/// </summary>
public class PvpHit
{
    /// <summary>The connection id of the player who was hit.</summary>
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";

    /// <summary>Set by the server: the attacker's connection id.</summary>
    [JsonPropertyName("attackerId")]
    public string AttackerId { get; set; } = "";

    /// <summary>The attacker's stage (the target must be there too).</summary>
    [JsonPropertyName("stageName")]
    public string StageName { get; set; } = "";

    /// <summary>Quarter hearts, 1..<see cref="MaxDamage"/>.</summary>
    [JsonPropertyName("damage")]
    public byte Damage { get; set; }

    /// <summary>dCcG_At_Spl: the attack's special reaction, 0..<see cref="MaxSpl"/>.</summary>
    [JsonPropertyName("spl")]
    public byte Spl { get; set; }

    /// <summary>The attack's AT_TYPE_* bits: nonzero, only Link's weapons (<see cref="LinkAtTypes"/>).</summary>
    [JsonPropertyName("atType")]
    public uint AtType { get; set; }

    /// <summary>Where it hit, in the attacker's world.</summary>
    [JsonPropertyName("position")]
    public Vector3 Position { get; set; } = new();

    /// <summary>The attacker's Link: the knockback points away from it.</summary>
    [JsonPropertyName("attackerPosition")]
    public Vector3 AttackerPosition { get; set; } = new();

    /// <summary>tww.h TWW_PVP_MAX_DAMAGE: 20 hearts.</summary>
    public const byte MaxDamage = 80;

    /// <summary>tww.h TWW_PVP_SPL_MAX.</summary>
    public const byte MaxSpl = 0x0B;

    /// <summary>Sword, bomb, boomerang, arrow, hookshot, Skull Hammer, fire / ice / light arrow (d_cc_d.h AT_TYPE_*).</summary>
    public const uint LinkAtTypes = 0x2 | 0x20 | 0x40 | 0x4000 | 0x8000 | 0x10000 | 0x40000 | 0x80000 | 0x100000;

    /// <summary>Largest |coordinate| accepted (as <see cref="PlayerEvent.MaxCoordinate"/>).</summary>
    public const float MaxCoordinate = PlayerEvent.MaxCoordinate;

    /// <summary>Longest <see cref="TargetId"/> (SignalR connection ids are 22 characters).</summary>
    public const int MaxIdLength = 64;

    /// <summary>Every field in range: a target id, a stage name, damage, special and attack type within the game's
    /// values, finite positions inside the world. <see cref="AttackerId"/> is the server's and not checked here.</summary>
    public bool IsValid()
    {
        if (!IsPrintable(TargetId, MaxIdLength) || !IsPrintable(StageName, PlayerEvent.MaxStageNameLength)) return false;
        if (Damage < 1 || Damage > MaxDamage || Spl > MaxSpl) return false;
        if (AtType == 0 || (AtType & ~LinkAtTypes) != 0) return false;
        return InWorld(Position) && InWorld(AttackerPosition);
    }

    private static bool IsPrintable(string? s, int maxLength)
    {
        if (string.IsNullOrEmpty(s) || s.Length > maxLength) return false;
        foreach (char c in s)
        {
            if (c < '!' || c > '~') return false;
        }
        return true;
    }

    private static bool InWorld(Vector3? v) =>
        v != null && InRange(v.X) && InRange(v.Y) && InRange(v.Z);

    /// <summary>Finite and strictly inside ±<see cref="MaxCoordinate"/> (false for NaN / Infinity).</summary>
    private static bool InRange(float value) => value > -MaxCoordinate && value < MaxCoordinate;

    /// <summary>For logs, safe on an unvalidated hit: no null dereference, ids and the stage cut short and printable.</summary>
    public override string ToString()
    {
        static string Safe(string? s, int max) =>
            new((s ?? "").Take(max).Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
        var at = Position == null ? "no position" : $"{Position.X:F0},{Position.Y:F0},{Position.Z:F0}";
        return $"{Damage}/4 hearts, at type 0x{AtType:X}, spl {Spl}, at {Safe(StageName, PlayerEvent.MaxStageNameLength)} ({at}) on {Safe(TargetId, 8)}";
    }
}
