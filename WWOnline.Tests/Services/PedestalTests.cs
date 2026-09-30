using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;
using static WWOnline.Tests.Services.Delivery;

namespace WWOnline.Tests.Services;

/// <summary>
/// Windfall's pedestals ride in the shared delivery bag (<see cref="DeliveryCounts.Pedestals"/>, event registers
/// D1FF-F8FF, docs/side-quests.md §3.2): setting a trade good down or taking it back is one change with its bag
/// slot, and the server refuses the whole change when the pedestal no longer holds what the change says it held.
/// </summary>
public class PedestalTests
{
    private const int P = 0x22; // pedestal F3FF, one of Sam's six (d_a_npc_people l_daiza_no_tbl)

    private static uint Reg(int pedestal) =>
        EventFlagCatalog.EventBitfieldAddress + DeliveryCounts.FirstPedestalRegister + (uint)pedestal;

    private static DeliveryCounts WithPedestal(DeliveryCounts c, int pedestal, int type)
    {
        c.Pedestals[pedestal] = type < 0 ? (byte)0 : Item(type);
        return c;
    }

    [Fact]
    public void ThePedestals_AreRegistersD1ToF8_AndTakeOnlyTradeGoods()
    {
        Assert.Equal(0xF8, DeliveryCounts.FirstPedestalRegister + DeliveryCounts.PedestalCount - 1);
        Assert.Equal("F3FF", DeliveryCounts.PedestalName(P));
        for (int t = 0; t < DeliveryCounts.TypeCount; t++)
            Assert.Equal(t < DeliveryCounts.FirstUniqueType, DeliveryCounts.IsPedestalItem(Item(t))); // isDaizaItem: 0x8C-0x97
        Assert.False(DeliveryCounts.IsPedestalItem(0));
    }

    [Fact]
    public void ATotal_HoldsOnlyTradeGoodsOnPedestals()
    {
        Assert.True(WithPedestal(C(0), P, Town).IsValidTotal());
        Assert.False(WithPedestal(C(0), P, Moblin).IsValidTotal()); // a letter can't stand on a pedestal
        var withBefore = C(0);
        withBefore.PedestalsBefore = new byte[DeliveryCounts.PedestalCount];
        Assert.False(withBefore.IsValidTotal()); // "before" is for deltas only
    }

    [Fact]
    public void SettingAFlowerDown_IsOneDelta_WithItsBagSlot()
    {
        var before = C(Bit(Town), (Town, 1));
        var after = WithPedestal(C(Bit(Town)), P, Town);
        var delta = after.Minus(before);
        Assert.True(delta.IsValidDelta());
        Assert.Equal(-1, delta[Town]);
        Assert.Equal([P], delta.ChangedPedestals());
        Assert.Equal(0, delta.PedestalsBefore![P]);
        Assert.Equal(Item(Town), delta.Pedestals[P]);
        Assert.Contains("pedestal F3FF: empty → Town Flower", delta.DeltaText());
        // Only a pedestal changing still makes a delta.
        Assert.True(WithPedestal(C(0), P, Town).Minus(C(0)).IsValidDelta());
        Assert.False(C(0).Minus(C(0)).IsValidDelta());
    }

    [Fact]
    public void UndoingAnUnsentDelta_PutsThePedestalBack()
    {
        var before = C(Bit(Town), (Town, 1));
        var after = WithPedestal(C(Bit(Town)), P, Town);
        var delta = after.Minus(before);
        var undone = after.Minus(delta); // SharedBagService: _baseline = b.Minus(delta) when a send fails
        Assert.True(undone.IsValidTotal());
        Assert.Equal(before, undone);
    }

    [Fact]
    public void TheStore_SetsThePedestal_AndTakesTheFlowerOutOfTheBag()
    {
        var store = new DeliveryStore();
        store.Join(C(Bit(Town), (Town, 2)));
        var total = store.Apply(WithPedestal(C(Bit(Town), (Town, 1)), P, Town).Minus(C(Bit(Town), (Town, 2))));
        Assert.NotNull(total);
        Assert.Null(total!.Value.Refused);
        Assert.Equal(1, total.Value.Total[Town]);
        Assert.Equal(Item(Town), total.Value.Total.Pedestals[P]);
    }

    [Fact]
    public void TwoPlayersUsingTheSamePedestal_OnlyTheFirstCounts_AndNoFlowerIsLost()
    {
        var store = new DeliveryStore();
        store.Join(C(Bit(Town, Sea), (Town, 1), (Sea, 1)));
        // Both see the pedestal empty; P1 sets the Town Flower down, P2 the Sea Flower, in the same moment.
        var p1 = WithPedestal(C(Bit(Town, Sea), (Sea, 1)), P, Town).Minus(C(Bit(Town, Sea), (Town, 1), (Sea, 1)));
        var p2 = WithPedestal(C(Bit(Town, Sea), (Town, 1)), P, Sea).Minus(C(Bit(Town, Sea), (Town, 1), (Sea, 1)));
        Assert.Null(store.Apply(p1)!.Value.Refused);
        var second = store.Apply(p2)!.Value;
        Assert.Contains("pedestal F3FF", second.Refused);
        // The room: Town Flower on the pedestal, the Sea Flower still in the bag (P2 gets it back).
        Assert.Equal(Item(Town), second.Total.Pedestals[P]);
        Assert.Equal(1, second.Total[Sea]);
        Assert.Equal(0, second.Total[Town]);
    }

    [Fact]
    public void TakingAFlowerBack_PutsItInTheBag()
    {
        var store = new DeliveryStore();
        store.Join(WithPedestal(C(Bit(Town)), P, Town));
        var delta = C(Bit(Town), (Town, 1)).Minus(WithPedestal(C(Bit(Town)), P, Town));
        var total = store.Apply(delta)!.Value.Total;
        Assert.Equal(1, total[Town]);
        Assert.Equal(0, total.Pedestals[P]);
        // Taking it back again (a second player at the same pedestal) is refused whole: no second flower.
        var again = store.Apply(delta)!.Value;
        Assert.NotNull(again.Refused);
        Assert.Equal(1, again.Total[Town]);
    }

    [Fact]
    public void Memory_ReadsThePedestalRegisters_AndWritesOnlyWhatStillMatches()
    {
        var d = Game(Bag());
        d.Set(Reg(P), Item(Town));
        d.Set(Reg(0), 0x50); // not a pedestal item: read as empty, never written over
        var read = DeliveryBagMemory.ReadPedestals(d)!;
        Assert.Equal(Item(Town), read[P]);
        Assert.Equal(0, read[0]);

        var next = (byte[])read.Clone();
        next[P] = 0;
        next[1] = Item(Sea);
        Assert.Equal(BagWriteResult.Written, DeliveryBagMemory.WritePedestals(d, read, next));
        Assert.Equal(0, d.Get(Reg(P)));
        Assert.Equal(Item(Sea), d.Get(Reg(1)));
        Assert.Equal(0x50, d.Get(Reg(0)));

        // The game changed pedestal 1 since: nothing is written.
        d.Set(Reg(1), Item(Exotic));
        var again = (byte[])next.Clone();
        again[1] = 0;
        Assert.Equal(BagWriteResult.Raced, DeliveryBagMemory.WritePedestals(d, next, again));
        Assert.Equal(Item(Exotic), d.Get(Reg(1)));
    }
}
