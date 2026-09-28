using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>PuppetData.Boat is relayed by the server, so it's validated like the position.</summary>
public class BoatStateTests
{
    private static PuppetData Puppet(BoatState? boat) => new()
    {
        Position = new Vector3(1, 2, 3),
        Boat = boat,
    };

    [Fact]
    public void NoBoat_IsValid()
    {
        Assert.True(Puppet(null).IsValid());
    }

    [Fact]
    public void FiniteBoat_IsValid()
    {
        Assert.True(Puppet(new BoatState { Position = new Vector3(100, 0, -200), SpeedF = 30, Rotation = -0x4000 }).IsValid());
    }

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(0f, float.NaN)]
    public void NonFiniteBoat_RejectsThePuppet(float x, float speed)
    {
        Assert.False(Puppet(new BoatState { Position = new Vector3(x, 0, 0), SpeedF = speed }).IsValid());
    }

    [Theory]
    [InlineData(2.0e6f, 0f, 0f)]
    [InlineData(0f, -2.0e6f, 0f)]
    [InlineData(0f, 0f, 5000f)]
    [InlineData(0f, 0f, -5000f)]
    public void OutOfRangeBoat_RejectsThePuppet(float x, float y, float speed)
    {
        Assert.False(Puppet(new BoatState { Position = new Vector3(x, y, 0), SpeedF = speed }).IsValid());
    }

    [Fact]
    public void NullBoatPosition_RejectsThePuppet()
    {
        Assert.False(Puppet(new BoatState { Position = null! }).IsValid());
    }

    [Fact]
    public void PoseAtTheGamesLimits_IsValid()
    {
        Assert.True(Puppet(new BoatState
        {
            SailAngle = BoatState.MaxSailAngle, Tiller = -BoatState.MaxTiller,
            HeadX = BoatState.MinHeadX, HeadY = BoatState.MaxHeadY,
            HeadBck = 0x07, HeadFrame = 255, MastFrame = 12, MastRaised = true, MastHidden = true,
        }).IsValid());
        Assert.True(Puppet(new BoatState { HeadX = BoatState.MaxHeadX, HeadY = -BoatState.MaxHeadY, HeadBck = 0 }).IsValid());
    }

    [Fact]
    public void HeadLookLimits_AreTheDecompClampsOverSix()
    {
        // d_a_ship.cpp:4171-4189: pitch -0x3000..0x2000, yaw +-0x7800, both / 6.
        Assert.Equal(-0x800, BoatState.MinHeadX);
        Assert.Equal(0x555, BoatState.MaxHeadX);
        Assert.Equal(0x1400, BoatState.MaxHeadY);
    }

    [Theory]
    [InlineData(short.MinValue, 0, 0, 0)]
    [InlineData(BoatState.MaxSailAngle + 1, 0, 0, 0)]
    [InlineData(0, short.MinValue, 0, 0)]
    [InlineData(0, BoatState.MaxTiller + 1, 0, 0)]
    [InlineData(0, 0, BoatState.MinHeadX - 1, 0)]
    [InlineData(0, 0, BoatState.MaxHeadX + 1, 0)]
    [InlineData(0, 0, 0, short.MinValue)]
    [InlineData(0, 0, 0, BoatState.MaxHeadY + 1)]
    public void OutOfRangePose_RejectsThePuppet(int sail, int tiller, int headX, int headY)
    {
        Assert.False(Puppet(new BoatState { SailAngle = (short)sail, Tiller = (short)tiller, HeadX = (short)headX, HeadY = (short)headY }).IsValid());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0x04)]
    [InlineData(0x0A)] // FN_MAST_OFF2: the mast's bck, never the head's
    [InlineData(0x0B)] // FN_MAST_ON2
    [InlineData(0x0F)]
    [InlineData(255)]
    public void NonHeadBck_RejectsThePuppet(int bck)
    {
        Assert.False(Puppet(new BoatState { HeadBck = (byte)bck }).IsValid());
    }

    [Fact]
    public void Pose_SurvivesJson()
    {
        var boat = new BoatState
        {
            Position = new Vector3(1, 2, 3), SailAngle = -0x1000, Tiller = 0x800, HeadX = -0x100, HeadY = 0x200,
            HeadBck = 0x0E, HeadFrame = 9, MastFrame = 7, MastRaised = true, MastHidden = true,
        };
        var copy = System.Text.Json.JsonSerializer.Deserialize<BoatState>(System.Text.Json.JsonSerializer.Serialize(boat))!;
        Assert.Equal(boat.SailAngle, copy.SailAngle);
        Assert.Equal(boat.Tiller, copy.Tiller);
        Assert.Equal(boat.HeadX, copy.HeadX);
        Assert.Equal(boat.HeadY, copy.HeadY);
        Assert.Equal(boat.HeadBck, copy.HeadBck);
        Assert.Equal(boat.HeadFrame, copy.HeadFrame);
        Assert.Equal(boat.MastFrame, copy.MastFrame);
        Assert.True(copy.MastRaised);
        Assert.True(copy.MastHidden);
    }
}
