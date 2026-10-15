using System.Runtime.InteropServices;

namespace WWOnline.Native;

/// <summary>
/// tww.h's pvp block (tww_pvp_*): peers' puppets as battle targets, the hits the local Link lands on them, and peers'
/// hits applied to the local Link. Game thread only, like <see cref="ITwwApi"/>: reach it through
/// <see cref="INativeGame.Invoke{T}"/> and cast the api.
/// </summary>
public interface ITwwPvpApi
{
    /// <summary>Bit i: slot i's player takes part (target, hittable, their hits applied). 0: PVP off.</summary>
    void SetSlots(uint slotMask);
    /// <summary>The oldest hit the local Link landed on a taking-part puppet, taken off the game's queue.</summary>
    bool PollHit(out TwwPvpHit hit);
    /// <returns>False: the slot doesn't take part, the queue is full or a value is out of range.</returns>
    bool ApplyHit(in TwwPvpHit hit);
}

public static unsafe partial class TwwNative
{
    public const byte PvpMaxDamage = 80;
    public const byte PvpSplMax = 0x0B;

    [LibraryImport(LibraryName)] public static partial void tww_pvp_set_slots(uint slotMask);
    [LibraryImport(LibraryName)] public static partial int tww_pvp_poll_hit(TwwPvpHit* hit);
    [LibraryImport(LibraryName)] public static partial int tww_pvp_apply_hit(TwwPvpHit* hit);
}

/// <summary>TwwPvpHit (32 bytes): a hit between Links.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwPvpHit
{
    /// <summary>Polled: the peer whose puppet was hit; applied: the peer who hit the local Link.</summary>
    public byte Slot;
    /// <summary>Quarter hearts, 1..<see cref="TwwNative.PvpMaxDamage"/>.</summary>
    public byte Damage;
    /// <summary>dCcG_At_Spl, 0..<see cref="TwwNative.PvpSplMax"/>.</summary>
    public byte Spl;
    public byte Pad0;
    /// <summary>AT_TYPE_* bits.</summary>
    public uint AtType;
    /// <summary>Where it hit, in the attacker's world.</summary>
    public TwwFloat3 Pos;
    /// <summary>The attacker's Link.</summary>
    public TwwFloat3 AttackerPos;
}
