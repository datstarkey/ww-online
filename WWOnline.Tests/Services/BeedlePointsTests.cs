using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Beedle's point card (event register 0x86FF, +1 per purchase, never lowered: d_a_npc_bs1.cpp:939-941) rides in
/// the room story as a MAX: the room keeps the highest card and every player's is raised to it.
/// </summary>
public class BeedlePointsTests
{
    private static readonly uint Addr = EventFlagCatalog.EventBitfieldAddress + StoryFlags.BeedlePointsRegisterByte;

    [Fact]
    public void FromEventBytes_ReadsTheCard()
    {
        var raw = new byte[EventFlagCatalog.EventBitfieldSize];
        raw[0x86] = 42;
        Assert.Equal(42, StoryFlags.FromEventBytes(raw).BeedlePoints);
        Assert.Equal(EventRegisterPolicy.Max, EventFlagCatalog.Registers.Single(r => r.Id == 0x86FF).Policy);
    }

    [Fact]
    public void MergesByMax_AndOnlyAHigherCardIsMissing()
    {
        var room = new StoryFlags { BeedlePoints = 12 };
        Assert.False(room.MergeFrom(new StoryFlags { BeedlePoints = 5 }));
        Assert.Equal(12, room.BeedlePoints);
        Assert.True(room.MergeFrom(new StoryFlags { BeedlePoints = 20 }));
        Assert.Equal(20, room.BeedlePoints);

        Assert.Equal(20, room.Except(new StoryFlags { BeedlePoints = 7 }).BeedlePoints);
        Assert.Equal(0, room.Except(new StoryFlags { BeedlePoints = 25 }).BeedlePoints);
        Assert.Equal(20, room.FlagsOnly().BeedlePoints);
        Assert.False(room.IsEmpty);
    }

    [Fact]
    public void ApplyBits_RaisesTheCard_NeverLowersIt()
    {
        var d = new FakeDolphin();
        d.Set(Addr, 10);
        StorySyncService.ApplyBits(d, new StoryFlags { BeedlePoints = 30 });
        Assert.Equal(30, d.Get(Addr));
        StorySyncService.ApplyBits(d, new StoryFlags { BeedlePoints = 12 });
        Assert.Equal(30, d.Get(Addr));
    }

    [Fact]
    public void TheStoryFlagsPage_MarksTheCardShared()
    {
        var row = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.Id == 0x86FF));
        Assert.True(row.IsSharedBeedlePoints);
        Assert.Contains("Beedle", row.PolicyText);
    }
}
