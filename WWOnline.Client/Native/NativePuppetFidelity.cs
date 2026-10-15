using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Native;

/// <summary>
/// <see cref="PuppetData"/>'s anims, equipment and look to and from libtww's puppet structs (tww.h "Puppet
/// fidelity"): the same fields the Dolphin REL's anim mirror and held-item code take (AnimMirror, HeldItemState).
/// </summary>
public static class NativePuppetFidelity
{
    /// <summary>daPyFlg0_EQUIP_HEAVY_BOOTS (d_a_player.h): ActionState.NoResetFlg0's Iron Boots bit, as the REL reads it.</summary>
    public const uint HeavyBootsFlag = 0x02000000;

    /// <summary>The Dolphin item track carries no frame: it plays on from its start.</summary>
    private const float UnknownFrame = -1f;

    /// <summary>The local Link's anims, equipment and actual clothes into <paramref name="data"/>.</summary>
    public static void AddLocal(PuppetData data, NativePuppetSnapshot local)
    {
        var anim = local.Anim;
        var look = local.Look;
        if (anim.Valid != 0)
        {
            var tracks = new AnimTrack[AnimationState.TrackCount];
            for (int t = 0; t < tracks.Length; t++)
                tracks[t] = ToTrack(anim.Track[t]);
            var item = anim.Track[AnimationState.TrackCount];
            ushort upper = item.Bck;
            data.Animation.Tracks = tracks;
            data.Animation.UpperBodyAnimation = upper;
            data.Animation.ItemTrack = ToTrack(item);
            data.Animation.FaceBtp = anim.FaceBtp;
            data.Animation.FaceBtk = anim.FaceBtk;
            data.Animation.FaceBtpFrame = anim.FaceBtpFrame;
            data.Animation.FaceBtkFrame = anim.FaceBtkFrame;
            data.Animation.HandLeft = anim.HandL;
            data.Animation.HandRight = anim.HandR;
            data.Action.BodyAngleX = anim.BodyAngle[0];
            data.Action.BodyAngleY = anim.BodyAngle[1];
            byte proc = data.Action.CurProc;
            bool guarding = proc is PuppetLayout.DAPY_PROC_GUARD_0 or PuppetLayout.DAPY_PROC_GUARD_1 or PuppetLayout.DAPY_PROC_GUARD_2
                            || upper is PuppetLayout.DAPY_UPPER_ANM_GUARD_0 or PuppetLayout.DAPY_UPPER_ANM_GUARD_1;
            data.Action.ActionFlags = guarding ? (byte)PuppetLayout.PUPPET_ACTION_FLAG_GUARD : (byte)0;
        }
        if (look.Valid != 0)
        {
            data.Equipment = new EquipmentState
            {
                SwordId = look.Sword,
                ShieldId = look.Shield,
                BraceletId = look.Bracelet,
                ItemInHand = look.EquipItem,
                GrabKind = look.Grab <= EquipmentState.MaxGrabKind ? look.Grab : (byte)0,
                GrabFuse = look.GrabFuse,
            };
            data.Action.NoResetFlg0 = look.Flags.HasFlag(TwwPuppetLookFlags.IronBoots) ? HeavyBootsFlag : 0;
            data.Action.DamageFlash = look.Flags.HasFlag(TwwPuppetLookFlags.DamageFlash);
            // What Link wears, rather than "as the save says": the receiver can't read our save.
            if (data.Appearance.ClothesType == PuppetLayout.APPEARANCE_CLOTHES_DEFAULT)
                data.Appearance.ClothesType = (byte)look.Clothes;
        }
    }

    /// <summary>A peer's anims for tww_puppet_set_anim; null when they sent none.</summary>
    public static TwwPuppetAnim? ToAnim(PuppetData puppet)
    {
        if (puppet.Animation?.Tracks is not { Length: > 0 } tracks)
            return null;
        var anim = new TwwPuppetAnim { Valid = 1 };
        for (int t = 0; t < AnimationState.TrackCount; t++)
        {
            AnimTrack? tr = t < tracks.Length ? tracks[t] : null;
            anim.Track[t] = tr == null ? new TwwPuppetTrack { Bck = t < 2 ? AnimTrack.None : AnimTrack.Same } : ToTrack(tr);
        }
        // Without the item track (an older client, Dolphin) only its bck is known.
        anim.Track[AnimationState.TrackCount] = puppet.Animation.ItemTrack is { } item
            ? ToTrack(item)
            : new TwwPuppetTrack
            {
                Bck = puppet.Animation.UpperBodyAnimation,
                Attr = 0xFF,
                Frame = UnknownFrame,
                Rate = 1f,
                Ratio = 1f,
            };
        anim.FaceBtp = puppet.Animation.FaceBtp;
        anim.FaceBtk = puppet.Animation.FaceBtk;
        anim.FaceBtpFrame = puppet.Animation.FaceBtpFrame;
        anim.FaceBtkFrame = puppet.Animation.FaceBtkFrame;
        anim.HandL = puppet.Animation.HandLeft;
        anim.HandR = puppet.Animation.HandRight;
        if (puppet.Action != null)
        {
            anim.BodyAngle[0] = puppet.Action.BodyAngleX;
            anim.BodyAngle[1] = puppet.Action.BodyAngleY;
        }
        return anim;
    }

    /// <summary>A peer's look for tww_puppet_set_look; null when they sent no equipment.</summary>
    public static TwwPuppetLook? ToLook(PuppetData puppet)
    {
        if (puppet.Equipment is not { } eq)
            return null;
        var look = new TwwPuppetLook
        {
            Valid = 1,
            Sword = eq.SwordId,
            Shield = eq.ShieldId,
            Bracelet = eq.BraceletId ?? 0xFF,
            Clothes = (TwwPuppetClothes)(puppet.Appearance?.ClothesType ?? PuppetLayout.APPEARANCE_CLOTHES_DEFAULT),
            Flags = (((puppet.Action?.NoResetFlg0 ?? 0) & HeavyBootsFlag) != 0 ? TwwPuppetLookFlags.IronBoots : TwwPuppetLookFlags.None)
                    | (puppet.Action?.DamageFlash == true ? TwwPuppetLookFlags.DamageFlash : TwwPuppetLookFlags.None),
            EquipItem = eq.ItemInHand,
            Grab = eq.GrabKind <= EquipmentState.MaxGrabKind ? eq.GrabKind : (byte)0,
            GrabFuse = eq.GrabFuse,
        };
        if (puppet.Appearance is { } a)
        {
            look.Tunic[0] = a.ColorR;
            look.Tunic[1] = a.ColorG;
            look.Tunic[2] = a.ColorB;
        }
        return look;
    }

    private static AnimTrack ToTrack(TwwPuppetTrack tr) => new()
    {
        Bck = tr.Bck,
        Start = tr.Start,
        End = tr.End,
        Loop = tr.Loop,
        Attribute = tr.Attr,
        Frame = Finite(tr.Frame),
        Rate = Finite(tr.Rate),
        Ratio = Finite(tr.Ratio),
    };

    private static TwwPuppetTrack ToTrack(AnimTrack tr) => new()
    {
        Bck = tr.Bck,
        Start = tr.Start,
        End = tr.End,
        Loop = tr.Loop,
        Attr = tr.Attribute,
        Frame = tr.Frame,
        Rate = tr.Rate,
        Ratio = tr.Ratio,
    };

    private static float Finite(float f) => float.IsFinite(f) ? f : 0f;
}
