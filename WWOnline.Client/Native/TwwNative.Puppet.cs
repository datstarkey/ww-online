using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WWOnline.Native;

// tww.h "Puppet fidelity": a peer's anims, face and equipment, and the local Link's to send.
public static unsafe partial class TwwNative
{
    public const int PuppetTracks = 5;
    public const ushort PuppetAnmNone = 0xFFFF;
    public const ushort PuppetAnmSame = 0xFFFE;

    [LibraryImport(LibraryName)] public static partial void tww_puppet_set_anim(int slot, TwwPuppetAnim* anim);
    [LibraryImport(LibraryName)] public static partial void tww_puppet_set_look(int slot, TwwPuppetLook* look);
    [LibraryImport(LibraryName)] public static partial int tww_puppet_get_local(TwwPuppetAnim* anim, TwwPuppetLook* look);
    [LibraryImport(LibraryName)] public static partial void tww_puppet_set_local_outfit(byte clothes, byte r, byte g, byte b);
}

/// <summary>TWW_PUPPET_CLOTHES_* (TwwPuppetLook.Clothes); the same values as AppearanceState.ClothesType.</summary>
public enum TwwPuppetClothes : byte
{
    Hero = 0,
    Casual = 1,
    /// <summary>As the game dresses Link from the save.</summary>
    Default = 2,
}

/// <summary>TWW_PUPPET_LOOK_* (TwwPuppetLook.Flags).</summary>
[Flags]
public enum TwwPuppetLookFlags : byte
{
    None = 0,
    IronBoots = 1 << 0,
    /// <summary>TWW_PUPPET_LOOK_DAMAGE_FLASH: hurt, flashing red.</summary>
    DamageFlash = 1 << 1,
}

/// <summary>TwwPuppetTrack (24 bytes): one body anim track, as J3DFrameCtrl and the blend ratio have it.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwPuppetTrack
{
    /// <summary>LkAnm bck index, <see cref="TwwNative.PuppetAnmNone"/> or <see cref="TwwNative.PuppetAnmSame"/>.</summary>
    public ushort Bck;
    public short Start;
    public short End;
    public short Loop;
    public byte Attr;
    public TwwPad3 Pad0;
    /// <summary>&lt; 0: unknown (the track plays on from its start).</summary>
    public float Frame;
    public float Rate;
    public float Ratio;
}

[InlineArray(TwwNative.PuppetTracks)]
public struct TwwPuppetTracks
{
    private TwwPuppetTrack _element;
}

/// <summary>TwwPuppetAnim (140 bytes): under[0..1], upper[0..1], upper[2] (the item track), face, hands, aim.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwPuppetAnim
{
    public uint Valid;
    public TwwPuppetTracks Track;
    public ushort FaceBtp;
    public ushort FaceBtk;
    public ushort FaceBtpFrame;
    public ushort FaceBtkFrame;
    public byte HandL;
    public byte HandR;
    /// <summary>mBodyAngle: the aim's pitch and yaw.</summary>
    public TwwShort3 BodyAngle;
}

/// <summary>TwwPuppetLook (16 bytes): equipment, item in hand, clothes.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwPuppetLook
{
    public uint Valid;
    /// <summary>The save's equip slots (dItemNo, 0xFF none).</summary>
    public byte Sword;
    public byte Shield;
    public byte Bracelet;
    public TwwPuppetClothes Clothes;
    public TwwPad3 Tunic;
    public TwwPuppetLookFlags Flags;
    /// <summary>mEquipItem: dItemNo, or 0x100 none, 0x103 the sword.</summary>
    public ushort EquipItem;
    /// <summary>TWW_PUPPET_GRAB_*: 1 = a carried bomb.</summary>
    public byte Grab;
    public byte GrabFuse;
}

/// <summary>The local Link's anims and look after the last frame (tww_puppet_get_local).</summary>
public sealed record NativePuppetSnapshot(uint Frame, TwwPuppetAnim Anim, TwwPuppetLook Look);

/// <summary>The puppet part of tww.h. Game thread only, like <see cref="ITwwApi"/>.</summary>
public interface ITwwPuppetApi
{
    /// <param name="anim">Null: none (the puppet plays the wait / walk / run blend).</param>
    void SetPuppetAnim(int slot, TwwPuppetAnim? anim);
    /// <param name="look">Null: none (the puppet shows the local save's equipment).</param>
    void SetPuppetLook(int slot, TwwPuppetLook? look);
    bool GetLocalPuppet(out TwwPuppetAnim anim, out TwwPuppetLook look);
    void SetLocalOutfit(TwwPuppetClothes clothes, byte r, byte g, byte b);
}

/// <summary>The embedded game's puppet state, published with <see cref="INativeGame.Snapshot"/>.</summary>
public interface INativePuppetGame
{
    /// <summary>The local Link's anims and look after the last frame, or null (no Link actor).</summary>
    NativePuppetSnapshot? PuppetSnapshot { get; }
}
