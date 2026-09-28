using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// PuppetVisibility: rooms separate players everywhere except the Great Sea, where the rooms
/// are grid squares and distance decides instead.
/// </summary>
public class PuppetVisibilityTests
{
    private static PuppetData At(string stage, byte room, float x = 0, float z = 0) => new()
    {
        StageName = stage,
        RoomNumber = room,
        Position = new Vector3(x, 0, z),
    };

    [Fact]
    public void SameStageSameRoom_IsVisible()
    {
        Assert.True(PuppetVisibility.IsSameLocation("M_NewD2", 3, 0, 0, At("M_NewD2", 3)));
    }

    [Fact]
    public void DungeonOtherRoom_IsHidden_EvenWhenClose()
    {
        Assert.False(PuppetVisibility.IsSameLocation("M_NewD2", 3, 0, 0, At("M_NewD2", 4, 10, 10)));
    }

    [Fact]
    public void OtherStage_IsHidden()
    {
        Assert.False(PuppetVisibility.IsSameLocation("sea", 44, 0, 0, At("M_NewD2", 44)));
    }

    [Fact]
    public void EmptyStage_IsHidden()
    {
        Assert.False(PuppetVisibility.IsSameLocation("", 0, 0, 0, At("", 0)));
    }

    [Fact]
    public void SeaAdjacentSquare_WithinSight_IsVisible()
    {
        // Either side of a square border, 2000 units apart.
        Assert.True(PuppetVisibility.IsSameLocation("sea", 11, 49000, 0, At("sea", 12, 51000, 0)));
    }

    [Fact]
    public void SeaOtherSquare_OutOfSight_IsHidden()
    {
        float far = PuppetVisibility.SeaVisibleDistance + 1;
        Assert.False(PuppetVisibility.IsSameLocation("sea", 11, 0, 0, At("sea", 12, far, 0)));
    }

    [Fact]
    public void SeaHysteresis_ShownPlayerStaysUntilHideDistance()
    {
        float between = (PuppetVisibility.SeaVisibleDistance + PuppetVisibility.SeaHideDistance) / 2;
        var puppet = At("sea", 12, between, 0);
        Assert.False(PuppetVisibility.IsSameLocation("sea", 11, 0, 0, puppet, wasVisible: false));
        Assert.True(PuppetVisibility.IsSameLocation("sea", 11, 0, 0, puppet, wasVisible: true));

        var gone = At("sea", 12, PuppetVisibility.SeaHideDistance + 1, 0);
        Assert.False(PuppetVisibility.IsSameLocation("sea", 11, 0, 0, gone, wasVisible: true));
    }

    [Fact]
    public void SeaDistance_IgnoresHeight()
    {
        var puppet = At("sea", 12, 1000, 0);
        puppet.Position.Y = 50000;
        Assert.True(PuppetVisibility.IsSameLocation("sea", 11, 0, 0, puppet));
    }

    [Fact]
    public void SeaNonFinitePosition_IsHidden()
    {
        Assert.False(PuppetVisibility.IsSameLocation("sea", 11, 0, 0, At("sea", 12, float.NaN, 0)));
    }

    [Theory]
    [InlineData("sea", 11, "sea", 12, false)]        // sailing across a border
    [InlineData("sea", 11, "sea", 11, false)]
    [InlineData("M_NewD2", 3, "M_NewD2", 4, true)]   // dungeon room change
    [InlineData("sea", 44, "LinkRM", 0, true)]       // entering a house
    [InlineData("LinkRM", 0, "sea", 0, true)]        // stage change even with the same room number
    public void IsSceneChange(string prevStage, byte prevRoom, string stage, byte room, bool expected)
    {
        Assert.Equal(expected, PuppetVisibility.IsSceneChange(prevStage, prevRoom, stage, room));
    }
}
