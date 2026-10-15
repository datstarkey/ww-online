using System.Runtime.CompilerServices;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>The boat structs match tww.h's layout, BoatState converts to and from them, and PuppetSyncService's
/// native path sends the peers' boats and the local boat's colours and reads the local boat.</summary>
public sealed class NativeBoatTests : IDisposable
{
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), "wwo-native-boats-" + Guid.NewGuid().ToString("N"));
    private readonly FakeNativeGame _game = new();
    private readonly PuppetSyncService _sut;

    public NativeBoatTests()
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
    public void TwwBoatState_Is52Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwBoatState();
        Assert.Equal(52, Unsafe.SizeOf<TwwBoatState>());
        Assert.Equal(0, Offset(ref s, ref s.Pos));
        Assert.Equal(12, Offset(ref s, ref s.SpeedF));
        Assert.Equal(16, Offset(ref s, ref s.AngleY));
        Assert.Equal(18, Offset(ref s, ref s.SailAngle));
        Assert.Equal(20, Offset(ref s, ref s.Tiller));
        Assert.Equal(22, Offset(ref s, ref s.HeadX));
        Assert.Equal(24, Offset(ref s, ref s.HeadY));
        Assert.Equal(26, Offset(ref s, ref s.CannonYaw));
        Assert.Equal(28, Offset(ref s, ref s.CannonPitch));
        Assert.Equal(30, Offset(ref s, ref s.CraneAngle));
        Assert.Equal(32, Offset(ref s, ref s.Flags));
        Assert.Equal(36, Offset(ref s, ref s.Part));
        Assert.Equal(37, Offset(ref s, ref s.MastFrame));
        Assert.Equal(38, Offset(ref s, ref s.HeadBck));
        Assert.Equal(39, Offset(ref s, ref s.HeadFrame));
        Assert.Equal(40, Offset(ref s, ref s.RopeLength));
        Assert.Equal(41, Offset(ref s, ref s.Room));
        Assert.Equal(42, Offset(ref s, ref s.Pad0));
        Assert.Equal(44, Offset(ref s, ref s.Stage));
    }

    [Fact]
    public void TwwBoatLook_Is8Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwBoatLook();
        Assert.Equal(8, Unsafe.SizeOf<TwwBoatLook>());
        Assert.Equal(0, Offset(ref s, ref s.HullRgb565));
        Assert.Equal(2, Offset(ref s, ref s.Pad0));
        Assert.Equal(4, Offset(ref s, ref s.SailRgb));
    }

    private static TwwBoatState Local(string stage, float x, float z, TwwBoatFlags flags = TwwBoatFlags.MastRaised,
        byte part = BoatState.PartSteer, sbyte room = 3)
    {
        var s = new TwwBoatState
        {
            SpeedF = 30f,
            AngleY = 0x4000,
            SailAngle = 0x1000,
            Tiller = -0x800,
            HeadX = 0x100,
            HeadY = -0x200,
            CannonYaw = 0x1234,
            CannonPitch = 0x2000,
            CraneAngle = 0x1800,
            Flags = flags,
            Part = part,
            MastFrame = 12,
            HeadBck = 0x07,
            HeadFrame = 4,
            RopeLength = 40,
            Room = room,
            Stage = TwwStageName.From(stage),
        };
        s.Pos[0] = x;
        s.Pos[1] = 5f;
        s.Pos[2] = z;
        return s;
    }

    [Fact]
    public void FromLocal_CarriesThePose_OnlyThePartsAnglesThatAreOut()
    {
        var boat = NativeBoats.FromLocal(Local("sea", 100000f, 9740f, TwwBoatFlags.MastRaised | TwwBoatFlags.MastHidden,
            BoatState.PartCrane))!;

        Assert.Equal(new Vector3(100000f, 5f, 9740f), boat.Position);
        Assert.Equal(0x4000, boat.Rotation);
        Assert.Equal(30f, boat.SpeedF);
        Assert.True(boat.MastRaised);
        Assert.True(boat.MastHidden);
        Assert.False(boat.Parked);
        Assert.Equal(26, boat.RoomNumber); // the sea square, not current.roomNo
        Assert.Equal(0x1000, boat.SailAngle);
        Assert.Equal(-0x800, boat.Tiller);
        Assert.Equal(0x07, boat.HeadBck);
        Assert.Equal(BoatState.PartCrane, boat.Part);
        Assert.Equal(0x1800, boat.CraneAngle);
        Assert.Equal(40, boat.RopeLength);
        Assert.Equal(0, boat.CannonYaw);
    }

    [Fact]
    public void FromLocal_OffTheSea_KeepsTheRoom_ClampsTheAngles_DropsAMastBckAsHeadBck()
    {
        var s = Local("ADMumi", 0, 0);
        s.SailAngle = short.MaxValue;
        s.HeadBck = 0x0B; // FN_MAST_ON2

        var boat = NativeBoats.FromLocal(s)!;

        Assert.Equal(3, boat.RoomNumber);
        Assert.Equal(BoatState.MaxSailAngle, boat.SailAngle);
        Assert.Equal(0, boat.HeadBck);
    }

    [Fact]
    public void ToPeer_RoundTripsThroughFromLocal()
    {
        var boat = NativeBoats.FromLocal(Local("ADMumi", 1, 2, TwwBoatFlags.Parked, BoatState.PartCannon))!;

        var s = NativeBoats.ToPeer(boat, "ADMumi");

        Assert.Equal("ADMumi", s.Stage.ToString());
        Assert.Equal(TwwBoatFlags.Parked, s.Flags);
        Assert.Equal(BoatState.PartCannon, s.Part);
        Assert.Equal(0x1234, s.CannonYaw);
        Assert.Equal(0x2000, s.CannonPitch);
        Assert.Equal(3, s.Room);
        Assert.Equal(1f, s.Pos[0]);
        Assert.Equal(boat.MastFrame, s.MastFrame);
    }

    [Fact]
    public void Look_IsTheBoatColourAndTheTunicColour_VanillaForTheDefaults()
    {
        var look = NativeBoats.Look(new AppearanceState { ColorR = 200, ColorG = 10, ColorB = 20, BoatR = 0, BoatG = 0, BoatB = 255 });
        Assert.Equal(0x001F, look.HullRgb565);
        Assert.Equal(0xC80A14u, look.SailRgb);

        var vanilla = NativeBoats.Look(new AppearanceState());
        Assert.Equal(0, vanilla.HullRgb565);
        Assert.Equal(0u, vanilla.SailRgb);
    }

    private static PuppetData Peer(string stage, byte room, BoatState? boat, float x = 0, float z = 0) => new()
    {
        Position = new Vector3(x, 0, z),
        Action = new ActionState { CurProc = 0x88 },
        StageName = stage,
        RoomNumber = room,
        Boat = boat,
        Appearance = new AppearanceState { BoatR = 0, BoatG = 0, BoatB = 255 },
    };

    private void WaitForTick()
    {
        int calls = Volatile.Read(ref _game.SetPeerCalls);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref _game.SetPeerCalls) >= calls + TwwNative.MaxPeers * 2,
            TimeSpan.FromSeconds(5)), "no puppet ticks");
    }

    [Fact]
    public void ARiddenBoat_GoesWithItsVisiblePuppet_InThePeersColours()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 11, 0, 0, 0));
        var boat = NativeBoats.FromLocal(Local("sea", 50, 0))!;
        _sut.UpdateRemotePuppet("sailing", Peer("sea", 11, boat, x: 50));
        _sut.UpdateRemotePuppet("away", Peer("M_NewD2", 2, boat));
        _sut.TryGetSlot("sailing", out int sailing);
        _sut.TryGetSlot("away", out int away);

        _sut.Start();
        WaitForTick();

        var sent = _game.PeerBoats[sailing]!.Value;
        Assert.Equal(50f, sent.Pos[0]);
        Assert.Equal("sea", sent.Stage.ToString());
        Assert.Equal(0x001F, _game.PeerBoatLooks[sailing]!.Value.HullRgb565);
        Assert.Null(_game.PeerBoats[away]);
    }

    [Fact]
    public void AParkedBoat_IsShownWhereItIs_WithoutItsPuppet()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 11, 0, 0, 0));
        var parked = NativeBoats.FromLocal(Local("sea", 1000, 1000, TwwBoatFlags.Parked))!;
        _sut.UpdateRemotePuppet("ashore", Peer("sea", 44, parked, x: 200000)); // on an island out of sight
        _sut.TryGetSlot("ashore", out int slot);

        _sut.Start();
        WaitForTick();

        Assert.Null(_game.Peers[slot]);
        Assert.Equal(TwwBoatFlags.Parked, _game.PeerBoats[slot]!.Value.Flags);
    }

    [Fact]
    public void TheLocalBoat_IsReadIntoTheLocalState_AndTheLocalLookIsSentOnce()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 11, 0, 0, 0));
        _game.LocalBoat = Local("sea", 10, 20);
        _sut.LocalAppearance = new AppearanceState { BoatR = 255, BoatG = 0, BoatB = 0 };

        _sut.Start();
        WaitForTick();
        WaitForTick();

        var data = _sut.ReadLocalPlayerState()!;
        Assert.NotNull(data.Boat);
        Assert.Equal(10f, data.Boat!.Position.X);
        Assert.Equal(0xF800, _game.LocalBoatLook!.Value.HullRgb565);
        Assert.Equal(1, _game.SetLocalBoatLookCalls);
    }

    [Fact]
    public void ReleaseSlot_AndWipeAll_ClearTheBoats()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 0, 0, 0, 0));
        _sut.UpdateRemotePuppet("a", Peer("sea", 0, null));
        _sut.TryGetSlot("a", out int slot);
        _game.PeerBoats[slot] = new TwwBoatState();
        _game.PeerBoats[(slot + 1) % TwwNative.MaxPeers] = new TwwBoatState();

        _sut.ReleaseSlot("a");
        Assert.Null(_game.PeerBoats[slot]);
        Assert.NotNull(_game.PeerBoats[(slot + 1) % TwwNative.MaxPeers]);

        _sut.WipeAll();
        Assert.All(_game.PeerBoats, b => Assert.Null(b));
    }
}
