using System.Runtime.CompilerServices;
using WWOnline.Native;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>The C# structs match tww.h's layout, and PuppetData converts to and from them.</summary>
public class TwwNativeTests
{
    private static int Offset<TStruct, TField>(ref TStruct s, ref TField field) =>
        (int)Unsafe.ByteOffset(ref Unsafe.As<TStruct, byte>(ref s), ref Unsafe.As<TField, byte>(ref field));

    [Fact]
    public void TwwLocation_Is16Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwLocation();
        Assert.Equal(16, Unsafe.SizeOf<TwwLocation>());
        Assert.Equal(0, Offset(ref s, ref s.Stage));
        Assert.Equal(8, Offset(ref s, ref s.Room));
        Assert.Equal(9, Offset(ref s, ref s.Layer));
        Assert.Equal(10, Offset(ref s, ref s.Point));
        Assert.Equal(12, Offset(ref s, ref s.Flags));
    }

    [Fact]
    public void TwwLocalState_Is48Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwLocalState();
        Assert.Equal(48, Unsafe.SizeOf<TwwLocalState>());
        Assert.Equal(0, Offset(ref s, ref s.Valid));
        Assert.Equal(4, Offset(ref s, ref s.Pos));
        Assert.Equal(16, Offset(ref s, ref s.Angle));
        Assert.Equal(22, Offset(ref s, ref s.Pad0));
        Assert.Equal(24, Offset(ref s, ref s.SpeedF));
        Assert.Equal(28, Offset(ref s, ref s.Proc));
        Assert.Equal(32, Offset(ref s, ref s.Loc));
    }

    [Fact]
    public void TwwPeerState_Is40Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwPeerState();
        Assert.Equal(40, Unsafe.SizeOf<TwwPeerState>());
        Assert.Equal(0, Offset(ref s, ref s.Pos));
        Assert.Equal(12, Offset(ref s, ref s.Angle));
        Assert.Equal(18, Offset(ref s, ref s.Pad0));
        Assert.Equal(20, Offset(ref s, ref s.SpeedF));
        Assert.Equal(24, Offset(ref s, ref s.Proc));
        Assert.Equal(28, Offset(ref s, ref s.Stage));
        Assert.Equal(36, Offset(ref s, ref s.Room));
        Assert.Equal(37, Offset(ref s, ref s.Pad1));
    }

    [Theory]
    [InlineData("sea", "sea")]
    [InlineData("M_NewD2", "M_NewD2")]
    [InlineData("TooLongName", "TooLong")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StageName_KeepsSevenCharactersAndANul(string? name, string expected)
    {
        var stage = TwwStageName.From(name);
        Assert.Equal(expected, stage.ToString());
        Assert.Equal(0, stage[7]);
    }

    [Fact]
    public void FromLocal_NoPlayer_IsNull()
    {
        Assert.Null(NativePuppets.FromLocal(default));
    }

    [Fact]
    public void FromLocal_CarriesPositionAngleSpeedProcAndPlace()
    {
        var local = FakeNativeGame.Local("M_NewD2", 3, 1.5f, -20f, 300.25f, angleY: -0x4000, speed: 12f, proc: 0xA3);

        var data = NativePuppets.FromLocal(local)!;

        Assert.Equal(new Vector3(1.5f, -20f, 300.25f), data.Position);
        Assert.Equal(-0x4000, data.Rotation);
        Assert.Equal(12f, data.Action.SpeedF);
        Assert.Equal(0xA3, data.Action.CurProc);
        Assert.Equal(1u, data.Action.StateFlags);
        Assert.Equal(1f, data.Animation.AnimationSpeed);
        Assert.Equal(AnimTrack.None, data.Animation.UpperBodyAnimation);
        Assert.Equal("M_NewD2", data.StageName);
        Assert.Equal(3, data.RoomNumber);
        Assert.True(data.IsValid());
    }

    [Fact]
    public void FromLocal_StandingStill_IsNotMoving()
    {
        var data = NativePuppets.FromLocal(FakeNativeGame.Local("sea", 0, 0, 0, 0, speed: 0.2f))!;
        Assert.Equal(0u, data.Action.StateFlags);
        Assert.Equal(0f, data.Animation.AnimationSpeed);
    }

    [Fact]
    public void ToPeer_CarriesPositionAngleSpeedProcAndPlace()
    {
        var puppet = new PuppetData
        {
            Position = new Vector3(10, 20, 30),
            Rotation = 0x7FFF,
            Action = new ActionState { SpeedF = 7.5f, CurProc = 0x21 },
            StageName = "sea",
            RoomNumber = 44,
        };

        var peer = NativePuppets.ToPeer(puppet);

        Assert.Equal(10f, peer.Pos[0]);
        Assert.Equal(20f, peer.Pos[1]);
        Assert.Equal(30f, peer.Pos[2]);
        Assert.Equal(0, peer.Angle[0]);
        Assert.Equal(0x7FFF, peer.Angle[1]);
        Assert.Equal(0, peer.Angle[2]);
        Assert.Equal(7.5f, peer.SpeedF);
        Assert.Equal(0x21u, peer.Proc);
        Assert.Equal("sea", peer.Stage.ToString());
        Assert.Equal(44, peer.Room);
    }

    [Fact]
    public void LocalToPeer_RoundTripsThroughPuppetData_IncludingNoRoom()
    {
        var local = FakeNativeGame.Local("Asoko", -1, -100f, 5f, 2500f, angleY: short.MinValue, speed: 3f, proc: 0xD9);

        var peer = NativePuppets.ToPeer(NativePuppets.FromLocal(local)!);

        Assert.Equal(local.Pos[0], peer.Pos[0]);
        Assert.Equal(local.Pos[1], peer.Pos[1]);
        Assert.Equal(local.Pos[2], peer.Pos[2]);
        Assert.Equal(local.Angle[1], peer.Angle[1]);
        Assert.Equal(local.SpeedF, peer.SpeedF);
        Assert.Equal(local.Proc, peer.Proc);
        Assert.Equal("Asoko", peer.Stage.ToString());
        Assert.Equal(-1, peer.Room);
    }
}
