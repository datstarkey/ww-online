using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Room flags held back from a game that isn't ready for them (<see cref="StorySyncService.HeldFlags"/>,
/// docs/softlocks.md): each would stop a player who is behind boarding the King of Red Lions (d_a_ship.cpp:4224-4229).
/// </summary>
public class HeldStoryFlagTests
{
    private const ushort FirstDescent = 0x2D10, Courtyard = 0x3804, ZeldaAwakened = 0x2D02;

    private static StoryFlags With(params ushort[] ids)
    {
        var f = new StoryFlags();
        foreach (var id in ids) f.Bits[id >> 8] |= (byte)(id & 0xFF);
        return f.Normalize();
    }

    [Fact]
    public void TheHeldFlags_AreSyncedStoryFlags()
    {
        foreach (var held in StorySyncService.HeldFlags)
            Assert.True(With(held.Id).Has(held.Id), $"0x{held.Id:X4} must be in the sync mask, or holding it means nothing");
    }

    [Fact]
    public void TheFirstDescent_WaitsForTheMasterSword()
    {
        var room = With(FirstDescent);
        var noSword = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => true,
            new StorySyncService.HoldState(new StoryFlags(), MasterSwordEquipped: false));
        Assert.False(noSword.Has(FirstDescent));
        var sword = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => true,
            new StorySyncService.HoldState(new StoryFlags(), MasterSwordEquipped: true));
        Assert.True(sword.Has(FirstDescent));
    }

    [Fact]
    public void TheCourtyardScene_WaitsForZeldaAwakened_WhichItselfIsntHeld()
    {
        var room = With(Courtyard, ZeldaAwakened);
        var behind = new StorySyncService.HoldState(new StoryFlags(), MasterSwordEquipped: true);
        var first = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => true, behind);
        Assert.True(first.Has(ZeldaAwakened));
        Assert.False(first.Has(Courtyard));

        // Next tick, the game has ZELDA_AWAKENED: the courtyard flag goes too.
        var local = With(ZeldaAwakened);
        var next = StorySyncService.ToApply(room, local, local, () => true, new StorySyncService.HoldState(local, true));
        Assert.True(next.Has(Courtyard));
    }

    [Fact]
    public void WithoutAHoldState_NothingIsHeld()
    {
        var room = With(FirstDescent, Courtyard);
        var all = StorySyncService.ToApply(room, new StoryFlags(), new StoryFlags(), () => true);
        Assert.True(all.Has(FirstDescent));
        Assert.True(all.Has(Courtyard));
    }

    [Theory]
    [InlineData(ItemIDs.Swords.MasterSword, true)]
    [InlineData(ItemIDs.Swords.MasterSwordHalf, true)]
    [InlineData(ItemIDs.Swords.MasterSwordFull, true)]
    [InlineData(0x38, false)] // Hero's Sword
    [InlineData(0xFF, false)] // none
    public void MasterSwordEquipped_ReadsTheSelectedSword(byte sword, bool expected)
    {
        var d = new FakeDolphin();
        d.Set(GameMemoryAddresses.Player.CurrentSword.Address, sword);
        Assert.Equal(expected, StorySyncService.IsMasterSwordEquipped(d));
    }
}
