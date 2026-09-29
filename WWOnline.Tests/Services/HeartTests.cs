using WWOnline.Data;
using WWOnline.Patcher.WorldData;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Derived max health (<see cref="HeartReconciler"/>): 12 + 4 per Heart Container + 1 per Piece of Heart whose flag is
/// set, written into the game only when it is idle; a heart no flag explains is logged and not kept.
/// </summary>
public class HeartReconcilerTests
{
    private static readonly HeartFlag ChestPiece = HeartFlag.Chest(0, 10), ItemPiece = HeartFlag.Item(0, 8),
        RewardPiece = HeartFlag.Event(0x1340), Gohma = HeartFlag.StageLife(3);

    private static readonly HeartTable Table = new()
    {
        StageCount = 2,
        Sources =
        [
            new(ChestPiece, 1, HeartSourceType.Chest, "Windfall Island - Transparent Chest", "sea room 11"),
            new(ItemPiece, 1, HeartSourceType.PlacedItem, "Headstone Island - Top of the Island", "sea room 45"),
            new(RewardPiece, 1, HeartSourceType.Reward, "Windfall Island - Ivan - Catch Killer Bees", "d_a_npc_mk"),
            new(Gohma, 4, HeartSourceType.Boss, "Dragon Roost Cavern - Gohma Heart Container", "M_DragB"),
        ],
    };

    private static HeartObservation Obs(int maxLife, bool idle = true, params HeartFlag[] set)
    {
        var f = new HeartFlagState();
        foreach (var s in set) f.Set(s);
        return new HeartObservation(f, maxLife, maxLife, idle, "sea", 11);
    }

    private sealed class Harness
    {
        public readonly HeartReconciler Reconciler = new();
        public readonly List<HeartLogLine> Log = [];
        public long Tick;
        public HeartWrite? Step(HeartObservation obs) => Reconciler.Step(Table, obs, ++Tick, Log);

        /// <summary>Step the same observation until it writes (or give up after <paramref name="ticks"/>).</summary>
        public HeartWrite? Settle(HeartObservation obs, int ticks = HeartReconciler.IdleTicksDown)
        {
            for (int i = 0; i < ticks; i++)
                if (Step(obs) is { } w) return w;
            return null;
        }
    }

    [Fact]
    public void AnotherPlayersPiece_RaisesMaxHealth_OnceIdle()
    {
        var h = new Harness();
        Assert.Null(h.Settle(Obs(12)));
        // The room's flags bring a piece this game didn't pick up: +1 (the game has long been idle).
        Assert.Equal(new HeartWrite(+1, 12, 13), h.Step(Obs(12, set: ChestPiece)));
        // The HUD applied it.
        Assert.Null(h.Settle(Obs(13, set: ChestPiece)));
    }

    [Fact]
    public void ARise_WaitsForTwoIdleTicks_AfterAnEvent()
    {
        var h = new Harness();
        Assert.Null(h.Step(Obs(12, idle: false, set: ChestPiece)));
        Assert.Null(h.Step(Obs(12, set: ChestPiece)));
        Assert.Equal(new HeartWrite(+1, 12, 13), h.Step(Obs(12, set: ChestPiece)));
    }

    [Fact]
    public void ABossContainer_CountsFourQuarters_AndBeatingTheBossAloneCountsNothing()
    {
        var h = new Harness();
        var beaten = new HeartFlagState();
        beaten.DungeonItem[3] = 0b1000; // STAGE_BOSS_ENEMY without STAGE_LIFE: the container is still lying there
        Assert.Null(h.Settle(new HeartObservation(beaten, 12, 12, true)));
        Assert.Equal(new HeartWrite(+4, 12, 16), h.Settle(Obs(12, set: Gohma)));
    }

    [Fact]
    public void ThisPlayersOwnPickup_NeedsNoWrite_AndIsNotLogged()
    {
        var h = new Harness();
        Assert.Null(h.Settle(Obs(12)));
        h.Log.Clear();
        // The chest's flag is set at the open, the piece lands during the get-item demo (not idle).
        for (int i = 0; i < 10; i++) Assert.Null(h.Step(Obs(12, idle: false, set: ChestPiece)));
        Assert.Null(h.Step(Obs(13, idle: false, set: ChestPiece)));
        Assert.Null(h.Settle(Obs(13, set: ChestPiece)));
        Assert.Empty(h.Log);
    }

    [Fact]
    public void APieceGivenTwice_IsLoggedLoudly_AndTakenBack()
    {
        var h = new Harness();
        Assert.Null(h.Settle(Obs(13, set: RewardPiece)));
        h.Tick += HeartReconciler.RecentFlagTicks + 1; // long after the flag arrived
        // The reward isn't flag-gated for this player: the game adds a second piece for the same flag.
        Assert.Null(h.Step(Obs(14, idle: false, set: RewardPiece)));
        var warn = Assert.Single(h.Log, l => l.Warn);
        Assert.Contains("given twice", warn.Text);
        Assert.Contains("stage sea room 11", warn.Text);
        // A fall waits for a longer idle stretch, then goes back to the derived value.
        for (int i = 1; i < HeartReconciler.IdleTicksDown; i++) Assert.Null(h.Step(Obs(14, set: RewardPiece)));
        Assert.Equal(new HeartWrite(-1, 14, 13), h.Step(Obs(14, set: RewardPiece)));
    }

    [Fact]
    public void ASaveWithTooManyHearts_IsCorrectedDown_AndReported()
    {
        var h = new Harness();
        var w = h.Settle(Obs(15, set: [ChestPiece, ItemPiece]));
        Assert.Equal(new HeartWrite(-1, 15, 14), w);
        Assert.Contains(h.Log, l => !l.Warn && l.Text.Contains("this save has max health 15") && l.Text.Contains("derive 14"));
        Assert.Contains(h.Log, l => l.Text.Contains("max health 15 → 14"));
    }

    [Fact]
    public void DuringAnEvent_NothingIsWritten()
    {
        var h = new Harness();
        for (int i = 0; i < 20; i++) Assert.Null(h.Step(Obs(12, idle: false, set: ChestPiece)));
    }

    [Fact]
    public void OneWriteAtATime_UntilItLands()
    {
        var h = new Harness();
        Assert.Equal(new HeartWrite(+2, 12, 14), h.Settle(Obs(12, set: [ChestPiece, ItemPiece])));
        // Not applied yet (the HUD takes a frame): no second write while it is awaited.
        for (int i = 0; i < HeartReconciler.WriteLandTicks; i++) Assert.Null(h.Step(Obs(12, set: [ChestPiece, ItemPiece])));
        // It never landed: written again.
        Assert.Equal(new HeartWrite(+2, 12, 14), h.Settle(Obs(12, set: [ChestPiece, ItemPiece])));
    }

    [Fact]
    public void MaxHealthIsClampedTo20Hearts()
    {
        var sources = Enumerable.Range(0, 20).Select(i => new HeartSource(HeartFlag.StageLife(i % 16) with { B = i }, 4, HeartSourceType.Boss, $"b{i}", ""));
        var t = new HeartTable { Sources = sources.ToList() };
        var f = new HeartFlagState();
        for (int i = 0; i < 16; i++) f.Set(HeartFlag.StageLife(i));
        Assert.Equal(HeartTable.MaxQuarters, t.Tally(f).MaxLife);
    }
}

/// <summary>Reading the heart flags and max health from the game, and the write (<see cref="HeartMemory"/>).</summary>
public class HeartMemoryTests
{
    private const uint StagInfo = 0x80A00000;

    /// <summary>A game in Dragon Roost Cavern (slot 3), idle.</summary>
    private static FakeDolphin InDrc()
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.WorldFlags.StagInfoPtr, StagInfo);
        d.Set(StagInfo + (uint)GameMemoryAddresses.WorldFlags.StagSaveTblOffset, 3 << 1);
        d.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, (byte)'M', (byte)'_', (byte)'D', 0);
        d.Set(GameMemoryAddresses.Player.MaxHealth.Address, 0, 13);
        return d;
    }

    [Fact]
    public void Read_ParsesEveryFlagKind_TheCurrentSlotFromTheLiveCopy()
    {
        var d = InDrc();
        const uint gi = GameMemoryAddresses.GameInfo;
        d.Set(GameMemoryAddresses.WorldFlags.LiveMemory + GameMemoryAddresses.WorldFlags.OffDungeonItem, HeartFlagState.StageLifeMask);
        d.Set(GameMemoryAddresses.WorldFlags.SavedSlot(3) + GameMemoryAddresses.WorldFlags.OffDungeonItem, 0); // stale saved copy
        d.SetU32(GameMemoryAddresses.WorldFlags.SavedSlot(0) + GameMemoryAddresses.WorldFlags.OffTbox, 1u << 10);
        d.SetU32(GameMemoryAddresses.WorldFlags.SavedSlot(0) + GameMemoryAddresses.WorldFlags.OffItem, 1u << 8);
        d.Set(GameMemoryAddresses.Events.EventBits + 0x13, 0x40);
        d.Set(GameMemoryAddresses.Events.EventBits + 0xAC, 0x03);
        d.SetU32(gi + GameMemoryAddresses.Hearts.OffCompleteMaps, 1u << 8);       // chart 9
        d.Set(gi + GameMemoryAddresses.Hearts.OffOcean + 17 * 2 + 1, 0x01);        // grid 17 bit 0
        d.SetU32(gi + GameMemoryAddresses.Hearts.OffGetBagReserve, 1u << HeartFlagState.MoblinsLetterReserveBit);
        for (int i = 0; i < 8; i++) d.Set(gi + GameMemoryAddresses.Hearts.OffDeliveryBag + (uint)i, 0xFF);

        var obs = HeartMemory.Read(d)!;
        Assert.Equal(13, obs.MaxLife);
        Assert.True(obs.Idle);
        Assert.Equal("M_D", obs.Stage);
        var f = obs.Flags;
        Assert.True(f.IsSet(HeartFlag.StageLife(3)));
        Assert.True(f.IsSet(HeartFlag.Chest(0, 10)));
        Assert.True(f.IsSet(HeartFlag.Item(0, 8)));
        Assert.True(f.IsSet(HeartFlag.Event(0x1340)));
        Assert.True(f.IsSet(HeartFlag.Register(0xAC03, 3)));
        Assert.True(f.IsSet(HeartFlag.Chart(9)));
        Assert.True(f.IsSet(HeartFlag.Ocean(17, 0)));
        Assert.True(f.IsSet(HeartFlag.MoblinsLetter));
        Assert.False(f.IsSet(HeartFlag.Chest(0, 11)));
    }

    [Fact]
    public void Read_IsNotIdle_WhileAMaxLifeChangeIsQueued_OrAnEventMenuOrMinigameRuns()
    {
        var d = InDrc();
        d.Write(GameMemoryAddresses.Hearts.PendingMaxLife, (short)1);
        Assert.False(HeartMemory.Read(d)!.Idle);
        d.Write(GameMemoryAddresses.Hearts.PendingMaxLife, (short)0);
        d.Set(GameMemoryAddresses.Events.EventMode, 1);
        Assert.False(HeartMemory.Read(d)!.Idle);
        d.Set(GameMemoryAddresses.Events.EventMode, 0);
        d.Set(GameMemoryAddresses.Events.MenuPause, 1);
        Assert.False(HeartMemory.Read(d)!.Idle);
        d.Set(GameMemoryAddresses.Events.MenuPause, 0);
        d.Set(GameMemoryAddresses.WorldFlags.MiniGameType, 3);
        Assert.False(HeartMemory.Read(d)!.Idle);
        d.Set(GameMemoryAddresses.WorldFlags.MiniGameType, 0);
        Assert.True(HeartMemory.Read(d)!.Idle);
    }

    [Fact]
    public void ApplyDelta_QueuesTheChange_OnlyWhenNoneIsQueued()
    {
        var d = InDrc();
        Assert.True(HeartMemory.ApplyDelta(d, -1));
        Assert.Equal((short)-1, d.Read(GameMemoryAddresses.Hearts.PendingMaxLife));
        Assert.False(HeartMemory.ApplyDelta(d, +2)); // the first is still queued
        Assert.Equal((short)-1, d.Read(GameMemoryAddresses.Hearts.PendingMaxLife));
        // mMaxLife itself is never written: the HUD applies the queued change.
        Assert.Equal(13, d.Get(GameMemoryAddresses.Player.MaxHealth.Address + 1));
    }
}

/// <summary>Shared items leave max health out of the merge while it is derived.</summary>
public class DerivedMaxHealthMergeTests
{
    [Fact]
    public void WithMaxHealth_ReplacesItOnlyWhileDerived()
    {
        var inv = new RoomInventory { MaxHealth = 15, MaxMagic = 16 };
        Assert.Same(inv, RoomInventorySyncService.WithMaxHealth(inv, derived: false, 0));
        var gains = RoomInventorySyncService.WithMaxHealth(inv, derived: true, 0);
        Assert.Equal(0, gains.MaxHealth);
        Assert.Equal(16, gains.MaxMagic);
        Assert.Equal(15, inv.MaxHealth); // the original is untouched

        // A room state to apply keeps the game's own max health: nothing to write for it.
        var room = new RoomInventory { MaxHealth = 20 };
        var local = new RoomInventory { MaxHealth = 13 };
        Assert.DoesNotContain(RoomInventory.Describe(local, RoomInventorySyncService.WithMaxHealth(room, derived: true, local.MaxHealth)),
            c => c.StartsWith("max health", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryRewardEventBit_IsSharedByStorySync()
    {
        // The NPC rewards' event bits are OR-merged by Shared story (none is LocalOnly or risky), so with it on a
        // reward anyone gets counts for everyone.
        var bits = HeartCatalog.Rewards.Where(r => r.Flag.Kind == HeartFlagKind.EventBit).Select(r => r.Flag.A).ToList();
        Assert.Equal(10, bits.Count);
        Assert.All(bits, id => Assert.NotEqual(0, StoryFlags.SyncMask[id >> 8] & (id & 0xFF)));
    }

    [Fact]
    public void TheRule_NeedsSharedItemsAndSharedWorld()
    {
        Assert.True(SharedHeartService.RuleApplies(new RoomSettings { SharedItems = true, SharedWorld = true }));
        Assert.False(SharedHeartService.RuleApplies(new RoomSettings { SharedItems = true, SharedWorld = false }));
        Assert.False(SharedHeartService.RuleApplies(new RoomSettings { SharedItems = false, SharedWorld = true }));
    }
}

/// <summary>Where the heart table comes from (<see cref="HeartTableProvider"/>).</summary>
public class HeartTableProviderTests
{
    [Fact]
    public void Build_SkipsAFolderWithoutStageSources()
    {
        var real = new HeartTable { Sources = [new(HeartFlag.Chest(0, 10), 1, HeartSourceType.Chest, "c", "")] };
        var rewardsOnly = new HeartTable { Sources = HeartCatalog.Rewards };
        var provider = new HeartTableProvider(() => ["empty", "game"], path => path == "empty" ? rewardsOnly : real);
        Assert.Same(real, provider.Build());
    }
}
