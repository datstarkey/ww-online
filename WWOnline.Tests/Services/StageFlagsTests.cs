using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

public class StageFlagsTests
{
    private static StageFlags Flags(uint tbox = 0, uint item = 0, uint sw0 = 0, byte dungeon = 0) => new()
    {
        Slot = 3, Tbox = tbox, Item = item, Switch = [sw0, 0, 0, 0], DungeonItem = dungeon,
    };

    [Fact]
    public void MergeFrom_OrsBits_AndReportsChange()
    {
        var a = Flags(tbox: 0b01, item: 0x10);
        Assert.True(a.MergeFrom(Flags(tbox: 0b10, sw0: 0x4)));
        Assert.Equal(0b11u, a.Tbox);
        Assert.Equal(0x10u, a.Item);
        Assert.Equal(0x4u, a.Switch[0]);
    }

    [Fact]
    public void MergeFrom_SubsetIsNotAChange()
    {
        var a = Flags(tbox: 0b11, dungeon: 0x5);
        Assert.False(a.MergeFrom(Flags(tbox: 0b01, dungeon: 0x1)));
    }

    [Fact]
    public void Except_ReturnsOnlyMissingBits()
    {
        var d = Flags(tbox: 0b111, item: 0x3).Except(Flags(tbox: 0b010, item: 0x1));
        Assert.Equal(0b101u, d.Tbox);
        Assert.Equal(0x2u, d.Item);
        Assert.Equal(3, d.BitCount);
        Assert.True(Flags().Except(Flags(tbox: 1)).IsEmpty);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(StageFlags.SlotCount)]
    public void IsValid_RejectsOutOfRangeSlot(int slot)
    {
        Assert.False(new StageFlags { Slot = slot }.IsValid());
    }

    [Fact]
    public void IsValid_RejectsWrongArrayLengths()
    {
        Assert.False(new StageFlags { Slot = 0, Switch = new uint[3] }.IsValid());
        Assert.False(new StageFlags { Slot = 0, VisitedRoom = new uint[1] }.IsValid());
        Assert.True(new StageFlags { Slot = 0 }.IsValid());
    }
}
