using WWOnline.Data;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

public class GameActionsTests
{
    private static readonly MemoryAddress<byte> Arrows = GameMemoryAddresses.Player.CurrentArrowCount;
    private static readonly MemoryAddress<short> ArrowDelta = GameMemoryAddresses.Player.PendingArrowDelta;
    private static readonly MemoryAddress<byte> Bombs = GameMemoryAddresses.Player.CurrentBombCount;
    private static readonly MemoryAddress<short> BombDelta = GameMemoryAddresses.Player.PendingBombDelta;

    [Fact]
    public void SetArrows_QueuesTheDifferenceForTheHud_AndLeavesTheSaveCountToTheGame()
    {
        var dolphin = new FakeDolphin();
        dolphin.Write(Arrows, (byte)10);

        Assert.True(GameActions.SetArrows(dolphin, 30));

        Assert.Equal((short)20, dolphin.Read(ArrowDelta));
        Assert.Equal((byte)10, dolphin.Read(Arrows)); // d_meter applies it next frame
    }

    [Fact]
    public void SetBombs_CountsAnAlreadyPendingChange_AndCanGoDown()
    {
        var dolphin = new FakeDolphin();
        dolphin.Write(Bombs, (byte)20);
        dolphin.Write(BombDelta, (short)5); // a pickup the HUD hasn't applied yet

        Assert.True(GameActions.SetBombs(dolphin, 3));

        Assert.Equal((short)-17, dolphin.Read(BombDelta)); // 20 + (-17) = 3
    }

    [Fact]
    public void SetArrows_ClampsTheTargetTo0To99()
    {
        var dolphin = new FakeDolphin();
        GameActions.SetArrows(dolphin, 250);
        Assert.Equal((short)99, dolphin.Read(ArrowDelta));
    }

    [Fact]
    public void AmmoDeltas_AreThePendingCountsNextToTheRupeeOne()
    {
        // play.mItemRupeeCount 0x48C0, mItemArrowNumCount 0x48E0, mItemBombNumCount 0x48E4
        uint rupee = GameMemoryAddresses.Player.PendingRupeeDelta.Address;
        Assert.Equal(rupee + 0x20, ArrowDelta.Address);
        Assert.Equal(rupee + 0x24, BombDelta.Address);
    }
}
