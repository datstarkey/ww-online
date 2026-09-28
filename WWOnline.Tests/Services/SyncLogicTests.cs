using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

public class SwitchApplyGuardTests
{
    private const int Slot = 3;

    private static StageFlags Switches(uint word0) => new() { Slot = Slot, Switch = { [0] = word0 } };

    [Fact]
    public void RoomSwitch_IsAppliedOnce_ThenNeverReappliedAfterTheGameClearsIt()
    {
        var guard = new SwitchApplyGuard();
        var missing = Switches(0x10);
        guard.Filter(Slot, Switches(0), missing);
        Assert.Equal(0x10u, missing.Switch[0]); // first time: apply

        var again = Switches(0x10); // the game cleared it (timed switch) — the room still has it
        guard.Filter(Slot, Switches(0), again);
        Assert.Equal(0u, again.Switch[0]);
    }

    [Fact]
    public void SwitchThisGameSetItself_IsNeverAppliedFromTheRoom_AfterTheGameClearsIt()
    {
        var guard = new SwitchApplyGuard();
        guard.Filter(Slot, Switches(0x04), Switches(0)); // pressure plate down: we set it (and send it)

        // Plate released: the game cleared it; the merged slot broadcast brings it back.
        var missing = Switches(0x04 | 0x100);
        guard.Filter(Slot, Switches(0), missing);
        Assert.Equal(0x100u, missing.Switch[0]); // only the switch this game never had

        // Other slots are independent.
        var otherSlot = new StageFlags { Slot = Slot + 1, Switch = { [0] = 0x04 } };
        guard.Filter(Slot + 1, new StageFlags { Slot = Slot + 1 }, otherSlot);
        Assert.Equal(0x04u, otherSlot.Switch[0]);
    }

    [Fact]
    public void NonSwitchBits_AreLeftForEveryTickEnforcement()
    {
        var guard = new SwitchApplyGuard();
        var missing = new StageFlags { Slot = Slot, Tbox = 0x1, Item = 0x2 };
        guard.Filter(Slot, new StageFlags { Slot = Slot, Tbox = 0x1 }, missing);
        Assert.Equal(0x1u, missing.Tbox);
        Assert.Equal(0x2u, missing.Item);
    }
}

public class SharedWalletMathTests
{
    [Theory]
    [InlineData(0, 200)]
    [InlineData(1, 1000)]
    [InlineData(2, 5000)]
    [InlineData(7, 5000)] // getRupeeMax's default case
    public void WalletCapacity_MatchesTheGame(byte size, int capacity) =>
        Assert.Equal(capacity, SharedWalletService.WalletCapacity(size));

    [Fact]
    public void Baseline_IsTheAppliedTotal_SoAPickupDuringTheCountUpIsStillSent()
    {
        // Total 300 pushed while we had 100; we grabbed a 5 during the count-up.
        int baseline = SharedWalletService.SettledBaseline(appliedTarget: 300, beforeApply: 100, current: 305, capacity: 1000, out bool lost);
        Assert.False(lost);
        Assert.Equal(300, baseline);
        Assert.Equal(+5, 305 - baseline); // → sent as a local delta next tick
    }

    [Fact]
    public void Baseline_PurchaseDuringTheCountUp_IsStillSent()
    {
        int baseline = SharedWalletService.SettledBaseline(300, 100, current: 250, capacity: 1000, out _);
        Assert.Equal(-50, 250 - baseline);
    }

    [Fact]
    public void Baseline_IsClampedToThisWallet_SoTheClampIsNotSpending()
    {
        int baseline = SharedWalletService.SettledBaseline(appliedTarget: 1500, beforeApply: 900, current: 1000, capacity: 1000, out bool lost);
        Assert.False(lost);
        Assert.Equal(1000, baseline);
    }

    [Fact]
    public void Baseline_WhenTheWriteDidntLand_IsTheCurrentValue()
    {
        int baseline = SharedWalletService.SettledBaseline(appliedTarget: 300, beforeApply: 100, current: 100, capacity: 1000, out bool lost);
        Assert.True(lost);
        Assert.Equal(100, baseline); // never send the unapplied -200 back to the room
    }
}

public class EquipSwapGuardTests
{
    private readonly FakeDolphin _game = new();

    [Theory]
    [InlineData(4u, 4u, true)]
    [InlineData(5u, 5u, false)] // swap in progress
    [InlineData(4u, 6u, false)] // a whole swap happened during the read
    [InlineData(4u, 5u, false)]
    public void IsConsistent(uint before, uint after, bool ok) => Assert.Equal(ok, EquipSwapGuard.IsConsistent(before, after));

    [Fact]
    public void ReadEquipped_ReturnsNull_MidSwap()
    {
        _game.Set(GameMemoryAddresses.Player.CurrentSword.Address, 0x38, 0x3B);
        Assert.Equal(((byte)0x38, (byte)0x3B), EquipSwapGuard.ReadEquipped(_game));
        _game.SetU32(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 7);
        Assert.Null(EquipSwapGuard.ReadEquipped(_game));
    }

    [Fact]
    public void TryWrite_OnlyWritesWhileNoSwapIsInProgress()
    {
        _game.SetU32(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 1);
        Assert.False(EquipSwapGuard.TryWrite(_game, GameMemoryAddresses.Player.CurrentShield, 0x3B));
        Assert.Equal(0, _game.Get(GameMemoryAddresses.Player.CurrentShield.Address));

        _game.SetU32(PuppetLayout.PUPPET_EQUIP_SWAP_SEQ_ADDR, 2);
        Assert.True(EquipSwapGuard.TryWrite(_game, GameMemoryAddresses.Player.CurrentShield, 0x3B));
        Assert.Equal(0x3B, _game.Get(GameMemoryAddresses.Player.CurrentShield.Address));
    }
}

public class DespawnWorkerTests
{
    private static readonly uint Tag = (uint)PuppetLayout.WORLDSYNC_TAG_MAGIC | 5;

    /// <summary>A scan that finished with <paramref name="pending"/> items left, for mask 0b11 with both collected.</summary>
    private static DespawnWorker.Scan Done(uint seq, uint pending) =>
        new(Tag, Mask: 0b11, LiveItem: 0b111, SeqBefore: seq, Pending: pending, ScanTag: Tag, ScanCand: 0b11, SeqAfter: seq);

    [Fact]
    public void NotNeeded_WithoutAValidTag_MaskOrCollectedCandidates()
    {
        Assert.False(DespawnWorker.NeedsScan(Done(9, 1) with { Tag = 0 }, null));
        Assert.False(DespawnWorker.NeedsScan(Done(9, 1) with { Mask = 0 }, null));
        Assert.False(DespawnWorker.NeedsScan(Done(9, 1) with { LiveItem = 0b100 }, null)); // mask bits not in live mItem
    }

    [Fact]
    public void Needed_WhenTheResultIsTornStaleOrOlderThanOurChange()
    {
        Assert.True(DespawnWorker.NeedsScan(Done(9, 0) with { SeqAfter = 10 }, null));   // torn
        Assert.True(DespawnWorker.NeedsScan(Done(9, 0) with { ScanTag = Tag + 1 }, null)); // other stage
        Assert.True(DespawnWorker.NeedsScan(Done(9, 0) with { ScanCand = 0b01 }, null));   // other candidates
        Assert.True(DespawnWorker.NeedsScan(Done(9, 0), seqAtLastChange: 9));               // no scan since our change
    }

    [Fact]
    public void AFreshResult_DecidesByPending()
    {
        Assert.True(DespawnWorker.NeedsScan(Done(10, 1), seqAtLastChange: 9));
        Assert.False(DespawnWorker.NeedsScan(Done(10, 0), seqAtLastChange: 9));
    }

    [Fact]
    public void GivesUp_AfterNoProgress_UntilTheNextChange()
    {
        var now = DateTime.UtcNow;
        var worker = new DespawnWorker(() => now);
        worker.MarkChanged(scanSeq: 9);

        Assert.True(worker.Update(Done(10, 2)));
        now += TimeSpan.FromSeconds(5);
        Assert.True(worker.Update(Done(11, 1))); // progress: pending dropped — the clock restarts
        now += TimeSpan.FromSeconds(9);
        Assert.True(worker.Update(Done(30, 1)));
        now += TimeSpan.FromSeconds(1);
        Assert.False(worker.Update(Done(31, 1))); // 10s stuck at the same result
        Assert.False(worker.Update(Done(32, 1)));

        worker.MarkChanged(scanSeq: 32);
        Assert.True(worker.Update(Done(32, 1)));
    }

    [Fact]
    public void IsNeeded_ReadsTheRelBlock()
    {
        var game = new FakeDolphin();
        var worker = new DespawnWorker();
        Assert.False(worker.IsNeeded(game)); // nothing published

        game.SetU32(PuppetLayout.WORLDSYNC_SCAN_SEQ_ADDR, 4);
        worker.MarkChanged(game); // C# publishes a mask
        game.SetU32(PuppetLayout.WORLDSYNC_ITEM_MASK_ADDR, 0b1);
        game.SetU32(PuppetLayout.WORLDSYNC_STAGE_TAG_ADDR, Tag);
        game.SetU32(GameMemoryAddresses.WorldFlags.LiveMemory + GameMemoryAddresses.WorldFlags.OffItem, 0b1);
        Assert.True(worker.IsNeeded(game)); // no scan yet

        // The REL scanned and despawned everything.
        game.SetU32(PuppetLayout.WORLDSYNC_PENDING_ADDR, 0);
        game.SetU32(PuppetLayout.WORLDSYNC_SCAN_TAG_ADDR, Tag);
        game.SetU32(PuppetLayout.WORLDSYNC_SCAN_CAND_ADDR, 0b1);
        game.SetU32(PuppetLayout.WORLDSYNC_SCAN_SEQ_ADDR, 5);
        Assert.False(worker.IsNeeded(game));
    }
}
