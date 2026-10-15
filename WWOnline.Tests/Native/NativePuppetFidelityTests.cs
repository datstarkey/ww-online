using System.Runtime.CompilerServices;
using System.Text.Json;
using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>tww.h "Puppet fidelity": the structs' layout, PuppetData to and from them, and the native sync path.</summary>
public sealed class NativePuppetFidelityTests : IDisposable
{
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), "wwo-native-fidelity-" + Guid.NewGuid().ToString("N"));
    private readonly FakeNativeGame _game = new();
    private readonly PuppetSyncService _sut;

    public NativePuppetFidelityTests()
    {
        _sut = new PuppetSyncService(new NativeGameService(_game), new GameSettingsService(_settingsDir), new DespawnWorker(),
            new LiveWorldPoke(), _game);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_settingsDir, recursive: true); } catch (IOException) { }
    }

    private static int Offset<TStruct, TField>(ref TStruct s, ref TField field) =>
        (int)Unsafe.ByteOffset(ref Unsafe.As<TStruct, byte>(ref s), ref Unsafe.As<TField, byte>(ref field));

    [Fact]
    public void TwwPuppetTrack_Is24Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwPuppetTrack();
        Assert.Equal(24, Unsafe.SizeOf<TwwPuppetTrack>());
        Assert.Equal(0, Offset(ref s, ref s.Bck));
        Assert.Equal(2, Offset(ref s, ref s.Start));
        Assert.Equal(4, Offset(ref s, ref s.End));
        Assert.Equal(6, Offset(ref s, ref s.Loop));
        Assert.Equal(8, Offset(ref s, ref s.Attr));
        Assert.Equal(12, Offset(ref s, ref s.Frame));
        Assert.Equal(16, Offset(ref s, ref s.Rate));
        Assert.Equal(20, Offset(ref s, ref s.Ratio));
    }

    [Fact]
    public void TwwPuppetAnim_Is140Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwPuppetAnim();
        Assert.Equal(140, Unsafe.SizeOf<TwwPuppetAnim>());
        Assert.Equal(0, Offset(ref s, ref s.Valid));
        Assert.Equal(4, Offset(ref s, ref s.Track));
        Assert.Equal(124, Offset(ref s, ref s.FaceBtp));
        Assert.Equal(126, Offset(ref s, ref s.FaceBtk));
        Assert.Equal(128, Offset(ref s, ref s.FaceBtpFrame));
        Assert.Equal(130, Offset(ref s, ref s.FaceBtkFrame));
        Assert.Equal(132, Offset(ref s, ref s.HandL));
        Assert.Equal(133, Offset(ref s, ref s.HandR));
        Assert.Equal(134, Offset(ref s, ref s.BodyAngle));
    }

    [Fact]
    public void TwwPuppetLook_Is16Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwPuppetLook();
        Assert.Equal(16, Unsafe.SizeOf<TwwPuppetLook>());
        Assert.Equal(0, Offset(ref s, ref s.Valid));
        Assert.Equal(4, Offset(ref s, ref s.Sword));
        Assert.Equal(5, Offset(ref s, ref s.Shield));
        Assert.Equal(6, Offset(ref s, ref s.Bracelet));
        Assert.Equal(7, Offset(ref s, ref s.Clothes));
        Assert.Equal(8, Offset(ref s, ref s.Tunic));
        Assert.Equal(11, Offset(ref s, ref s.Flags));
        Assert.Equal(12, Offset(ref s, ref s.EquipItem));
        Assert.Equal(14, Offset(ref s, ref s.Grab));
        Assert.Equal(15, Offset(ref s, ref s.GrabFuse));
    }

    [Fact]
    public void TheTrackCounts_MatchTheDolphinMirror_PlusTheItemTrack()
    {
        Assert.Equal(AnimationState.TrackCount + 1, TwwNative.PuppetTracks);
        Assert.Equal(AnimTrack.None, TwwNative.PuppetAnmNone);
        Assert.Equal(AnimTrack.Same, TwwNative.PuppetAnmSame);
    }

    /// <summary>A front roll (proc 0x1E) with the sword out and the Iron Boots on, in hero clothes.</summary>
    private static NativePuppetSnapshot Rolling()
    {
        var anim = new TwwPuppetAnim { Valid = 1, FaceBtp = 0x1E0, FaceBtk = 0x160, FaceBtpFrame = 3, FaceBtkFrame = 4, HandL = 2, HandR = 5 };
        anim.Track[0] = new TwwPuppetTrack { Bck = 0x80, End = 20, Attr = 2, Frame = 7.5f, Rate = 1.25f, Ratio = 1f };
        anim.Track[1] = new TwwPuppetTrack { Bck = AnimTrack.None };
        anim.Track[2] = new TwwPuppetTrack { Bck = AnimTrack.Same };
        anim.Track[3] = new TwwPuppetTrack { Bck = AnimTrack.Same };
        anim.Track[4] = new TwwPuppetTrack { Bck = PuppetLayout.DAPY_UPPER_ANM_GUARD_0, Frame = 2f, Rate = 1f, Ratio = 1f };
        anim.BodyAngle[0] = 0x400;
        anim.BodyAngle[1] = -0x200;
        var look = new TwwPuppetLook
        {
            Valid = 1, Sword = 0x38, Shield = 0x3B, Bracelet = 0x28, Clothes = TwwPuppetClothes.Hero,
            Flags = TwwPuppetLookFlags.IronBoots, EquipItem = 0x103, Grab = 1, GrabFuse = 90,
        };
        return new NativePuppetSnapshot(1, anim, look);
    }

    [Fact]
    public void DamageFlash_RoundTrips()
    {
        var local = Rolling();
        var look = local.Look;
        look.Flags |= TwwPuppetLookFlags.DamageFlash;
        var data = new PuppetData { Action = new ActionState() };

        NativePuppetFidelity.AddLocal(data, local with { Look = look });

        Assert.True(data.Action.DamageFlash);
        Assert.Equal(TwwPuppetLookFlags.IronBoots | TwwPuppetLookFlags.DamageFlash, NativePuppetFidelity.ToLook(data)!.Value.Flags);
        data.Action.DamageFlash = false;
        Assert.Equal(TwwPuppetLookFlags.IronBoots, NativePuppetFidelity.ToLook(data)!.Value.Flags);
    }

    [Fact]
    public void AddLocal_CarriesTracksFaceHandsAimEquipmentAndBoots()
    {
        var data = new PuppetData { Action = new ActionState { CurProc = 0x1E } };

        NativePuppetFidelity.AddLocal(data, Rolling());

        Assert.Equal(AnimationState.TrackCount, data.Animation.Tracks!.Length);
        Assert.Equal(0x80, data.Animation.Tracks[0].Bck);
        Assert.Equal(7.5f, data.Animation.Tracks[0].Frame);
        Assert.Equal(1.25f, data.Animation.Tracks[0].Rate);
        Assert.Equal(AnimTrack.Same, data.Animation.Tracks[2].Bck);
        Assert.Equal(PuppetLayout.DAPY_UPPER_ANM_GUARD_0, data.Animation.UpperBodyAnimation);
        Assert.Equal(PuppetLayout.PUPPET_ACTION_FLAG_GUARD, data.Action.ActionFlags);
        Assert.Equal(0x1E0, data.Animation.FaceBtp);
        Assert.Equal(4, data.Animation.FaceBtkFrame);
        Assert.Equal(5, data.Animation.HandRight);
        Assert.Equal(0x400, data.Action.BodyAngleX);
        Assert.Equal(-0x200, data.Action.BodyAngleY);
        Assert.Equal(0x38, data.Equipment.SwordId);
        Assert.Equal(0x3B, data.Equipment.ShieldId);
        Assert.Equal(0x103, data.Equipment.ItemInHand);
        Assert.Equal(1, data.Equipment.GrabKind);
        Assert.Equal(NativePuppetFidelity.HeavyBootsFlag, data.Action.NoResetFlg0);
    }

    [Theory]
    [InlineData(PuppetLayout.APPEARANCE_CLOTHES_DEFAULT, TwwPuppetClothes.Casual, 1)]
    [InlineData(PuppetLayout.APPEARANCE_CLOTHES_HERO, TwwPuppetClothes.Casual, 0)]
    [InlineData(PuppetLayout.APPEARANCE_CLOTHES_CASUAL, TwwPuppetClothes.Hero, 1)]
    public void AddLocal_ReplacesOnlyTheSavesDefault_WithWhatLinkWears(int chosen, TwwPuppetClothes worn, int sent)
    {
        var data = new PuppetData { Appearance = new AppearanceState { ClothesType = (byte)chosen } };
        var local = Rolling();
        var look = local.Look;
        look.Clothes = worn;

        NativePuppetFidelity.AddLocal(data, local with { Look = look });

        Assert.Equal(sent, data.Appearance.ClothesType);
    }

    [Fact]
    public void ToAnim_RoundTripsTheBodyAndItemTracks()
    {
        var data = new PuppetData();
        NativePuppetFidelity.AddLocal(data, Rolling());

        var anim = NativePuppetFidelity.ToAnim(data)!.Value;

        var sent = Rolling().Anim;
        for (int t = 0; t < AnimationState.TrackCount; t++)
        {
            Assert.Equal(sent.Track[t].Bck, anim.Track[t].Bck);
            Assert.Equal(sent.Track[t].Frame, anim.Track[t].Frame);
            Assert.Equal(sent.Track[t].Ratio, anim.Track[t].Ratio);
        }
        Assert.Equal(sent.Track[4].Bck, anim.Track[4].Bck);
        Assert.Equal(sent.Track[4].Frame, anim.Track[4].Frame);
        Assert.Equal(sent.FaceBtp, anim.FaceBtp);
        Assert.Equal(sent.HandL, anim.HandL);
        Assert.Equal(sent.BodyAngle[0], anim.BodyAngle[0]);
    }

    [Fact]
    public void TheItemTrackAndBracelet_GoThroughPuppetData_WithTheItemTracksFrame()
    {
        var data = new PuppetData();
        NativePuppetFidelity.AddLocal(data, Rolling());

        var back = JsonSerializer.Deserialize<PuppetData>(JsonSerializer.Serialize(data))!;
        var anim = NativePuppetFidelity.ToAnim(back)!.Value;
        var look = NativePuppetFidelity.ToLook(back)!.Value;

        Assert.Equal((byte)0x28, back.Equipment.BraceletId);
        Assert.Equal(0x28, look.Bracelet);
        Assert.Equal(PuppetLayout.DAPY_UPPER_ANM_GUARD_0, anim.Track[4].Bck);
        Assert.Equal(2f, anim.Track[4].Frame);
        Assert.Equal(1f, anim.Track[4].Rate);
    }

    [Fact]
    public void OldJson_WithoutTheItemTrackOrBracelet_StillDeserialises_AndFallsBack()
    {
        const string old = """
            {"playerId":"p","position":{"x":1,"y":2,"z":3},"rotation":0,
             "animation":{"upperBodyAnim":54,"animationSpeed":1,"tracks":[{"bck":128,"frame":3,"rate":1,"ratio":1}]},
             "equipment":{"swordId":56,"shieldId":59,"itemInHand":256,"grabKind":0},
             "action":{"curProc":4},"appearance":{"clothesType":0},"stageName":"sea","roomNumber":44}
            """;

        var data = JsonSerializer.Deserialize<PuppetData>(old)!;

        Assert.True(data.IsValid());
        Assert.Null(data.Animation.ItemTrack);
        Assert.Null(data.Equipment.BraceletId);
        var anim = NativePuppetFidelity.ToAnim(data)!.Value;
        Assert.Equal(54, anim.Track[4].Bck);
        Assert.True(anim.Track[4].Frame < 0); // unknown: plays on from its start
        Assert.Equal(0xFF, NativePuppetFidelity.ToLook(data)!.Value.Bracelet);
    }

    [Fact]
    public void AnInvalidItemTrack_MakesThePuppetDataInvalid()
    {
        var data = new PuppetData { Animation = new AnimationState { ItemTrack = new AnimTrack { Frame = float.NaN } } };
        Assert.False(data.IsValid());
    }

    [Fact]
    public void ToAnim_NoTracks_IsNull_AndShortTracksAreFilled()
    {
        Assert.Null(NativePuppetFidelity.ToAnim(new PuppetData()));

        var data = new PuppetData { Animation = new AnimationState { Tracks = [new AnimTrack { Bck = 0x10 }] } };
        var anim = NativePuppetFidelity.ToAnim(data)!.Value;

        Assert.Equal(0x10, anim.Track[0].Bck);
        Assert.Equal(AnimTrack.None, anim.Track[1].Bck);
        Assert.Equal(AnimTrack.Same, anim.Track[2].Bck);
        Assert.Equal(AnimTrack.Same, anim.Track[3].Bck);
    }

    [Fact]
    public void ToLook_CarriesEquipmentClothesColourAndBoots()
    {
        var data = new PuppetData
        {
            Equipment = new EquipmentState { SwordId = 0x39, ShieldId = 0x3C, ItemInHand = 0x2D, GrabKind = 1, GrabFuse = 40 },
            Appearance = new AppearanceState { ClothesType = 1, ColorR = 200, ColorG = 10, ColorB = 20 },
            Action = new ActionState { NoResetFlg0 = NativePuppetFidelity.HeavyBootsFlag | 0x1 },
        };

        var look = NativePuppetFidelity.ToLook(data)!.Value;

        Assert.Equal(1u, look.Valid);
        Assert.Equal(0x39, look.Sword);
        Assert.Equal(0x3C, look.Shield);
        Assert.Equal(0xFF, look.Bracelet);
        Assert.Equal(TwwPuppetClothes.Casual, look.Clothes);
        Assert.Equal(200, look.Tunic[0]);
        Assert.Equal(20, look.Tunic[2]);
        Assert.Equal(TwwPuppetLookFlags.IronBoots, look.Flags);
        Assert.Equal(0x2D, look.EquipItem);
        Assert.Equal(1, look.Grab);
        Assert.Equal(40, look.GrabFuse);
    }

    [Fact]
    public void ReadLocalPlayerState_AddsTheLocalAnimsAndLook()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 44, 0, 0, 0, proc: 0x1E));
        _game.PuppetSnapshot = Rolling();

        var data = _sut.ReadLocalPlayerState()!;

        Assert.Equal(0x80, data.Animation.Tracks![0].Bck);
        Assert.Equal(0x38, data.Equipment.SwordId);
        Assert.Equal(NativePuppetFidelity.HeavyBootsFlag, data.Action.NoResetFlg0);
    }

    [Fact]
    public void ReadLocalPlayerState_CountsAnUnderFrameJumpingBack_AsAProcRestart()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 44, 0, 0, 0, proc: 0x1E));
        _game.PuppetSnapshot = Rolling();
        byte first = _sut.ReadLocalPlayerState()!.Action.ProcSeq;
        var restarted = Rolling();
        var anim = restarted.Anim;
        anim.Track[0].Frame = 1f;
        _game.PuppetSnapshot = restarted with { Anim = anim };

        byte next = _sut.ReadLocalPlayerState()!.Action.ProcSeq;

        Assert.Equal((byte)(first + 1), next);
    }

    [Fact]
    public void VisiblePeers_GetTheirAnimsAndLook_OthersNone_AndTheLocalOutfitIsSet()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 44, 0, 0, 0));
        var here = new PuppetData { Position = new Vector3(), StageName = "sea", RoomNumber = 44, Action = new ActionState() };
        NativePuppetFidelity.AddLocal(here, Rolling());
        _sut.UpdateRemotePuppet("here", here);
        _sut.UpdateRemotePuppet("elsewhere", new PuppetData { Position = new Vector3(), StageName = "M_NewD2", RoomNumber = 1 });
        _sut.TryGetSlot("here", out int slot);
        _sut.TryGetSlot("elsewhere", out int other);
        _sut.LocalAppearance = new AppearanceState { ClothesType = 0, ColorR = 1, ColorG = 2, ColorB = 3 };

        _sut.Start();
        int calls = Volatile.Read(ref _game.SetPeerCalls);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref _game.SetPeerCalls) >= calls + TwwNative.MaxPeers * 2,
            TimeSpan.FromSeconds(5)), "no puppet ticks");

        Assert.Equal(0x80, _game.Anims[slot]!.Value.Track[0].Bck);
        Assert.Equal(0x38, _game.Looks[slot]!.Value.Sword);
        Assert.Null(_game.Anims[other]);
        Assert.Null(_game.Looks[other]);
        Assert.Equal((TwwPuppetClothes.Hero, (byte)1, (byte)2, (byte)3), _game.LocalOutfit);
    }

    [Fact]
    public void ReleaseSlot_ClearsThatPeersAnimsAndLook()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 0, 0, 0, 0));
        _sut.UpdateRemotePuppet("a", new PuppetData { Position = new Vector3(), StageName = "sea" });
        _sut.TryGetSlot("a", out int slot);
        _game.Anims[slot] = Rolling().Anim;
        _game.Looks[slot] = Rolling().Look;

        _sut.ReleaseSlot("a");

        Assert.Null(_game.Anims[slot]);
        Assert.Null(_game.Looks[slot]);
    }
}
