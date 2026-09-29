using Microsoft.AspNetCore.SignalR.Client;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

internal static class Spoils
{
    public const byte E = SpoilsBagSlots.Empty;
    public const int Skull = 0, Seed = 1, Feather = 2, Crest = 3, Red = 4, Green = 5, Blue = 6, Pendant = 7;

    public static byte Item(int type) => SpoilsCounts.Types[type].ItemNo;

    /// <summary>Counts by type, e.g. C((Pendant, 5), (Crest, 2)).</summary>
    public static SpoilsCounts C(params (int Type, int Count)[] counts)
    {
        var c = new int[SpoilsCounts.TypeCount];
        foreach (var (t, n) in counts) c[t] = n;
        return new SpoilsCounts(c);
    }

    /// <summary>A bag holding these types in this slot order, with these counts (nums are per type).</summary>
    public static SpoilsBagSlots Bag(params (int Type, byte Count)[] slots)
    {
        var items = Enumerable.Repeat(E, SpoilsCounts.TypeCount).ToArray();
        var nums = new byte[SpoilsCounts.TypeCount];
        for (int i = 0; i < slots.Length; i++)
        {
            items[i] = Item(slots[i].Type);
            nums[slots[i].Type] = slots[i].Count;
        }
        return new SpoilsBagSlots(items, nums);
    }

    public static FakeDolphin Game(SpoilsBagSlots bag, bool ownsBag = true)
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.Player.LinkActorPointer.Address, 0x80A00000);
        d.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, "sea"u8.ToArray().Concat(new byte[5]).ToArray());
        d.Set(GameMemoryAddresses.Inventory.SpoilsBag.Address, ownsBag ? ItemIDs.MainItems.SpoilsBag : (byte)0xFF);
        Put(d, bag);
        return d;
    }

    public static void Put(FakeDolphin d, SpoilsBagSlots bag)
    {
        d.Set(GameMemoryAddresses.Inventory.SpoilsItems, bag.Items);
        d.Set(GameMemoryAddresses.Inventory.SpoilsNums, bag.Nums);
    }

    public static SpoilsBagSlots Read(FakeDolphin d) => SpoilsBagMemory.Read(d)!;

    public static short Pending(FakeDolphin d, int type) =>
        (short)(d.Get(GameMemoryAddresses.Inventory.PendingSpoilsDeltas + (uint)(2 * type)) << 8 |
                d.Get(GameMemoryAddresses.Inventory.PendingSpoilsDeltas + (uint)(2 * type) + 1));

    /// <summary>What d_meter does each frame with the pending spoils counts (d_meter.cpp, dBeastIdx loop).</summary>
    public static void Meter(FakeDolphin d)
    {
        for (int t = 0; t < SpoilsCounts.TypeCount; t++)
        {
            short p = Pending(d, t);
            if (p == 0) continue;
            uint numAddr = GameMemoryAddresses.Inventory.SpoilsNums + (uint)t;
            int n = d.Get(numAddr) + p;
            d.Set(GameMemoryAddresses.Inventory.PendingSpoilsDeltas + (uint)(2 * t), 0, 0);
            if (n <= 0)
            {
                d.Set(numAddr, 0);
                for (uint s = 0; s < SpoilsCounts.TypeCount; s++) // setBeastItemEmpty
                    if (d.Get(GameMemoryAddresses.Inventory.SpoilsItems + s) == Item(t))
                        d.Set(GameMemoryAddresses.Inventory.SpoilsItems + s, E);
            }
            else d.Set(numAddr, (byte)Math.Min(n, 99));
        }
    }

    /// <summary>A pickup the way item_func_* does it: setBeastItem, then +1 pending.</summary>
    public static void PickUp(FakeDolphin d, int type)
    {
        var bag = Read(d);
        if (bag.SlotOf(type) < 0)
            d.Set(GameMemoryAddresses.Inventory.SpoilsItems + (uint)Array.IndexOf(bag.Items, E), Item(type));
        short p = (short)(Pending(d, type) + 1);
        d.Set(GameMemoryAddresses.Inventory.PendingSpoilsDeltas + (uint)(2 * type), (byte)(p >> 8), (byte)p);
    }
}

public class SpoilsCountsTests
{
    [Fact]
    public void Totals_AreZeroTo99_PerType_AndWellFormed()
    {
        Assert.True(new SpoilsCounts().IsValidTotal());
        Assert.True(Spoils.C((Spoils.Pendant, 99)).IsValidTotal());
        Assert.False(Spoils.C((Spoils.Pendant, 100)).IsValidTotal());
        Assert.False(Spoils.C((Spoils.Red, -1)).IsValidTotal());
        Assert.False(new SpoilsCounts { Counts = null! }.IsValidTotal());
        Assert.False(new SpoilsCounts { Counts = new int[7] }.IsValidTotal());
        Assert.False(new SpoilsCounts { Counts = new int[9] }.IsValidTotal());
    }

    [Fact]
    public void Deltas_ChangeSomething_AndStayWithinABag()
    {
        Assert.False(new SpoilsCounts().IsValidDelta());
        Assert.True(Spoils.C((Spoils.Crest, -3)).IsValidDelta());
        Assert.True(Spoils.C((Spoils.Skull, 99), (Spoils.Seed, -99)).IsValidDelta());
        Assert.False(Spoils.C((Spoils.Skull, 100)).IsValidDelta());
        Assert.False(Spoils.C((Spoils.Skull, int.MinValue)).IsValidDelta());
        Assert.False(new SpoilsCounts { Counts = null! }.IsValidDelta());
    }

    [Fact]
    public void Text_NamesTheTypes()
    {
        Assert.Equal("-3 Knight's Crest, +1 Joy Pendant", Spoils.C((Spoils.Crest, -3), (Spoils.Pendant, 1)).DeltaText());
        Assert.Equal("2 Red Chu Jelly", Spoils.C((Spoils.Red, 2)).ToString());
        Assert.Equal("(malformed)", new SpoilsCounts { Counts = null! }.ToString());
    }

    [Fact]
    public void TypeTable_MatchesTheGame()
    {
        // fopMsgM_itemNumIdx(24 + dBeastIdx) (f_op_msg_mng.cpp itemicon) and d_item_data.h.
        Assert.Equal(new byte[] { 0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x1F },
                     SpoilsCounts.Types.Select(t => t.ItemNo).ToArray());
    }
}

public class SpoilsStoreTests
{
    [Fact]
    public void Delta_IsRejected_WhileUnseeded_ThenApplied()
    {
        var store = new SpoilsStore();
        Assert.Null(store.ApplyDelta(Spoils.C((Spoils.Pendant, 1))));
        var (total, seeded) = store.Join(Spoils.C((Spoils.Pendant, 5)));
        Assert.True(seeded);
        Assert.Equal(Spoils.C((Spoils.Pendant, 5)), total);
        Assert.Equal(Spoils.C((Spoils.Pendant, 4), (Spoils.Red, 2)), store.ApplyDelta(Spoils.C((Spoils.Pendant, -1), (Spoils.Red, 2))));
    }

    [Fact]
    public void OnlyTheFirstJoinSeeds_AndCountsClampTo0And99()
    {
        var store = new SpoilsStore();
        store.Join(Spoils.C((Spoils.Crest, 98)));
        Assert.Equal(Spoils.C((Spoils.Crest, 98)), store.Join(Spoils.C((Spoils.Skull, 3))).Total);
        Assert.Equal(Spoils.C((Spoils.Crest, 99)), store.ApplyDelta(Spoils.C((Spoils.Crest, 5))));
        Assert.Equal(Spoils.C(), store.ApplyDelta(Spoils.C((Spoils.Crest, -99))));
        Assert.Null(store.ApplyDelta(new SpoilsCounts()));
        Assert.Null(store.ApplyDelta(new SpoilsCounts { Counts = new int[3] }));
    }

    [Fact]
    public void Reset_MakesTheNextJoinReseed()
    {
        var store = new SpoilsStore();
        store.Join(Spoils.C((Spoils.Blue, 7)));
        store.Reset();
        Assert.False(store.IsSeeded);
        Assert.True(store.Join(Spoils.C((Spoils.Green, 1))).Seeded);
    }
}

public class SpoilsBagTests
{
    private const int Skull = Spoils.Skull, Crest = Spoils.Crest, Red = Spoils.Red, Pendant = Spoils.Pendant, Feather = Spoils.Feather;

    [Fact]
    public void Count_IsPerType_AndATypeWithoutASlotIsZero()
    {
        var bag = Spoils.Bag((Pendant, 5), (Crest, 2));
        bag.Nums[Red] = 7; // a count left behind with no slot: the menu shows no Red Chu Jelly
        Assert.Equal(Spoils.C((Pendant, 5), (Crest, 2)), bag.Count());
    }

    [Fact]
    public void AnArrivingType_TakesTheFirstFreeSlot_AndItsCountIsQueued()
    {
        var plan = SpoilsBag.PlanFor(Spoils.Bag((Pendant, 5)), Spoils.C((Pendant, 5), (Crest, 3)));
        Assert.Equal(Spoils.Item(Crest), plan.Items[1]);
        Assert.Equal(new short[] { 0, 0, 0, 3, 0, 0, 0, 0 }, plan.Pending);
        Assert.Equal(Spoils.C((Pendant, 5), (Crest, 3)), plan.Expected);
    }

    [Fact]
    public void CountsChange_ThroughThePendingCounter_AndZeroLeavesTheSlotToTheGame()
    {
        var plan = SpoilsBag.PlanFor(Spoils.Bag((Pendant, 5), (Skull, 4)), Spoils.C((Pendant, 2)));
        Assert.Equal(new short[] { -4, 0, 0, 0, 0, 0, 0, -3 }, plan.Pending);
        Assert.Equal(Spoils.Bag((Pendant, 5), (Skull, 4)).Items, plan.Items); // d_meter empties the Skull slot itself

        // A slot showing a type at 0 still gets a -1, so d_meter runs its "reached 0" branch and frees the slot.
        var zombie = SpoilsBag.PlanFor(Spoils.Bag((Feather, 0)), new SpoilsCounts());
        Assert.Equal(-1, zombie.Pending[Feather]);
    }

    [Fact]
    public void NoFreeSlot_LeavesTheType_AndExpectedSaysSo()
    {
        var items = Enumerable.Repeat((byte)0x99, SpoilsCounts.TypeCount).ToArray();
        var bag = new SpoilsBagSlots(items, new byte[SpoilsCounts.TypeCount]);
        var plan = SpoilsBag.PlanFor(bag, Spoils.C((Red, 2)));
        Assert.True(plan.IsEmpty);
        Assert.Equal(new SpoilsCounts(), plan.Expected);
    }

    [Fact]
    public void SameCounts_PlanNothing()
    {
        var bag = Spoils.Bag((Pendant, 5), (Crest, 2));
        var plan = SpoilsBag.PlanFor(bag, bag.Count());
        Assert.True(plan.IsEmpty);
        Assert.Equal(bag.Items, plan.Items);
    }

    [Fact]
    public void SettledBaseline_IsWhatWasPlanned_SoAPickupMeanwhileIsStillSent()
    {
        var expected = Spoils.C((Pendant, 5));
        var before = Spoils.C((Pendant, 2));
        // The game applied +3 and a pickup of another +1 landed in the same frames: 6 now.
        var baseline = SpoilsBag.SettledBaseline(expected, before, Spoils.C((Pendant, 6)), out bool lost);
        Assert.False(lost);
        Assert.Equal(expected, baseline);

        // Nothing happened at all: the write didn't take, don't send -3 back.
        var unchanged = SpoilsBag.SettledBaseline(expected, before, before, out bool lost2);
        Assert.True(lost2);
        Assert.Equal(before, unchanged);
    }

    [Fact]
    public void GetFlags_AreOnePerTypeHeld()
    {
        Assert.Equal(1 << Pendant | 1 << Crest, SpoilsBag.GetFlagsFor(Spoils.Bag((Pendant, 1), (Crest, 1)).Items));
    }
}

public class SpoilsBagMemoryTests
{
    private const int Crest = Spoils.Crest, Pendant = Spoils.Pendant;

    [Fact]
    public void Addresses_AreTheSaveAndPlayFields()
    {
        // d_save.h: mBagItem 0x76 (+0 mBeast), mGetBagItem 0x90 (+4 mBeastFlags), mBagItemRecord 0x9C (+0 mBeastNum);
        // play.mItemBeastNumCounts = gameInfo + 0x5B88 (DOL item_func_skull_necklace: lha/sth 0x5B88).
        Assert.Equal(0x803C4C7Eu, GameMemoryAddresses.Inventory.SpoilsItems);
        Assert.Equal(0x803C4C9Cu, GameMemoryAddresses.Inventory.SpoilsGetFlags);
        Assert.Equal(0x803C4CA4u, GameMemoryAddresses.Inventory.SpoilsNums);
        Assert.Equal(0x803CA790u, GameMemoryAddresses.Inventory.PendingSpoilsDeltas);
        Assert.Equal(GameMemoryAddresses.GameInfo + 0x5B88, GameMemoryAddresses.Inventory.PendingSpoilsDeltas);
    }

    [Fact]
    public void Write_PlacesTheSlot_SetsTheFlag_AndQueuesTheCount()
    {
        var bag = Spoils.Bag((Pendant, 5));
        var d = Spoils.Game(bag);
        var plan = SpoilsBag.PlanFor(bag, Spoils.C((Pendant, 4), (Crest, 3)));

        Assert.Equal(BagWriteResult.Written, SpoilsBagMemory.Write(d, bag, plan));
        Assert.Equal(Spoils.Item(Crest), Spoils.Read(d).Items[1]);
        Assert.Equal(1 << Pendant | 1 << Crest, d.Get(GameMemoryAddresses.Inventory.SpoilsGetFlags));
        Assert.Equal(3, Spoils.Pending(d, Crest));
        Assert.Equal(-1, Spoils.Pending(d, Pendant));

        Spoils.Meter(d);
        Assert.Equal(Spoils.C((Pendant, 4), (Crest, 3)), Spoils.Read(d).Count());
    }

    [Fact]
    public void Write_DoesNothing_WhenTheBagChanged_OrACountIsStillPending()
    {
        var read = Spoils.Bag((Pendant, 5));
        var plan = SpoilsBag.PlanFor(read, Spoils.C((Pendant, 9)));

        var changed = Spoils.Game(Spoils.Bag((Pendant, 6)));
        Assert.Equal(BagWriteResult.Raced, SpoilsBagMemory.Write(changed, read, plan));
        Assert.Equal(0, Spoils.Pending(changed, Pendant));

        var pending = Spoils.Game(read);
        Spoils.PickUp(pending, Crest); // queued this frame, not applied yet
        Assert.Equal(BagWriteResult.Raced, SpoilsBagMemory.Write(pending, read, plan));
        Assert.Equal(0, Spoils.Pending(pending, Pendant));
    }
}

/// <summary>The hub's spoils methods: gated by the rule, seeded by the owner, every input validated.</summary>
[Collection("GameHub static state")]
public class SharedSpoilsHubTests
{
    private static async Task SetSpoilsRule(SignalRClientService owner, bool on)
    {
        var s = await owner.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
        s.SharedSpoils = on;
        await owner.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
    }

    private static async Task<SpoilsCounts?> NextTotal(SignalRClientService client, Func<Task> send, int ms = 1500)
    {
        var got = new TaskCompletionSource<SpoilsCounts>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(SpoilsCounts t) => got.TrySetResult(t);
        client.SpoilsTotalReceived += Handler;
        try
        {
            await send();
            return await Task.WhenAny(got.Task, Task.Delay(ms)) == got.Task ? got.Task.Result : null;
        }
        finally
        {
            client.SpoilsTotalReceived -= Handler;
        }
    }

    [Fact]
    public async Task OwnerSeeds_JoinersAdopt_DeltasAreValidated_AndTheRuleGatesEverything()
    {
        GameHub.ConfigureHostToken("spoils-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var owner = new SignalRClientService();
            Assert.True((await owner.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Owner"), "spoils-key")).success);
            await using var player = new SignalRClientService();
            Assert.True((await player.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Player"))).success);
            await SetSpoilsRule(owner, false);
            await SetSpoilsRule(owner, true);

            Assert.Null(await player.JoinSpoilsAsync(new SpoilsCounts()));                    // waits for the owner
            Assert.Null(await owner.JoinSpoilsAsync(Spoils.C((Spoils.Pendant, 100))));        // out of range
            Assert.Null(await owner.JoinSpoilsAsync(new SpoilsCounts { Counts = new int[3] })); // malformed
            Assert.Null(await owner.Connection!.InvokeAsync<SpoilsCounts?>(HubConstants.JoinSpoils, (object?)null));

            var seed = Spoils.C((Spoils.Pendant, 5), (Spoils.Crest, 2));
            Assert.Equal(seed, await owner.JoinSpoilsAsync(seed));
            Assert.Equal(seed, await player.JoinSpoilsAsync(Spoils.C((Spoils.Red, 9)))); // joiners adopt the room's

            Assert.Null(await NextTotal(owner, () => player.SendSpoilsDeltaAsync(new SpoilsCounts()), 400));
            Assert.Null(await NextTotal(owner, () => player.SendSpoilsDeltaAsync(Spoils.C((Spoils.Red, int.MinValue))), 400));
            Assert.Null(await NextTotal(owner, () => player.SendSpoilsDeltaAsync(new SpoilsCounts { Counts = null! }), 400));
            Assert.Null(await NextTotal(owner, () => player.Connection!.InvokeAsync(HubConstants.SendSpoilsDelta, (object?)null), 400));

            // The Knight's Crest trade (-2) reaches everyone; a count can't go past 99.
            Assert.Equal(Spoils.C((Spoils.Pendant, 5)), await NextTotal(owner, () => player.SendSpoilsDeltaAsync(Spoils.C((Spoils.Crest, -2)))));
            Assert.Equal(Spoils.C((Spoils.Pendant, 99)), await NextTotal(player, () => owner.SendSpoilsDeltaAsync(Spoils.C((Spoils.Pendant, 99)))));

            await SetSpoilsRule(owner, false);
            Assert.Null(await owner.JoinSpoilsAsync(new SpoilsCounts()));
            Assert.Null(await NextTotal(owner, () => player.SendSpoilsDeltaAsync(Spoils.C((Spoils.Red, 1))), 400));
            await SetSpoilsRule(owner, true);
            Assert.Null(await NextTotal(owner, () => player.SendSpoilsDeltaAsync(Spoils.C((Spoils.Red, 1))), 400)); // unseeded
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        }
    }

    [Fact]
    public void RulesSummary_NamesTheSpoilsRule()
    {
        Assert.Contains("shared spoils bag OFF", new RoomSettings { SharedSpoils = false }.RulesSummary());
        Assert.Contains("shared spoils bag ON", new RoomSettings().RulesSummary());
    }
}

/// <summary>Two games (FakeDolphin, with d_meter's pending-count step simulated) through the real hub.</summary>
[Collection("GameHub static state")]
public class SharedSpoilsServiceTests
{
    private const int Crest = Spoils.Crest, Pendant = Spoils.Pendant, Skull = Spoils.Skull;

    private static async Task Until(Func<bool> done, Action tick, int ms = 8000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ms);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out");
            tick();
            await Task.Delay(40);
        }
    }

    [Fact]
    public async Task BagsFollowTheRoom_BothWays_AndStopWhenTheRuleIsOff()
    {
        GameHub.ConfigureHostToken("spoils-svc-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var hubA = new SignalRClientService();
            await using var hubB = new SignalRClientService();
            using var roomA = new RoomSettingsService(hubA);
            using var roomB = new RoomSettingsService(hubB);
            Assert.True((await hubA.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Owner"), "spoils-svc-key")).success);
            Assert.True((await hubB.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Player"))).success);
            var s = await hubA.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            s.SharedSpoils = false;
            await hubA.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
            s.SharedSpoils = true;
            await hubA.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
            await Until(() => roomA.Current.SharedSpoils && roomB.Current.SharedSpoils, () => { });

            var gameA = Spoils.Game(Spoils.Bag((Pendant, 5), (Crest, 2)));
            var gameB = Spoils.Game(Spoils.Bag((Skull, 1)));
            using var a = new SharedSpoilsService(gameA, hubA, roomA);
            using var b = new SharedSpoilsService(gameB, hubB, roomB);
            a.Attach();
            b.Attach();
            void Both() { a.Tick(); Spoils.Meter(gameA); b.Tick(); Spoils.Meter(gameB); }

            // The owner's bag seeds the room; the player's bag becomes it (the Skull Necklace leaves its slot).
            await Until(() => Equals(a.Total, Spoils.C((Pendant, 5), (Crest, 2))), a.Tick);
            await Until(() => Spoils.Read(gameB).Count().Equals(Spoils.C((Pendant, 5), (Crest, 2))), () => { b.Tick(); Spoils.Meter(gameB); });
            Assert.Equal(-1, Array.IndexOf(Spoils.Read(gameB).Items, Spoils.Item(Skull)));

            // The player picks up a Joy Pendant: the owner's count goes up.
            Spoils.PickUp(gameB, Pendant);
            await Until(() => Spoils.Read(gameA).Count()[Pendant] == 6, Both);

            // The owner trades two Knight's Crests away: the player's go too, and the slot empties.
            gameA.Set(GameMemoryAddresses.Inventory.PendingSpoilsDeltas + 2 * Crest, 0xFF, 0xFE); // -2, as npc_ji1 does
            await Until(() => Spoils.Read(gameB).Count()[Crest] == 0 && Spoils.Read(gameA).Count()[Crest] == 0, Both);
            Assert.Equal(-1, Spoils.Read(gameB).SlotOf(Crest));

            // While an event runs the player's bag isn't rewritten.
            gameB.Set(GameMemoryAddresses.Events.EventMode, 1);
            Spoils.PickUp(gameA, Skull);
            await Until(() => Equals(b.Total, Spoils.C((Pendant, 6), (Skull, 1))), Both);
            for (int i = 0; i < 5; i++) Both();
            Assert.Equal(0, Spoils.Read(gameB).Count()[Skull]);
            gameB.Set(GameMemoryAddresses.Events.EventMode, 0);
            await Until(() => Spoils.Read(gameB).Count().Equals(Spoils.C((Pendant, 6), (Skull, 1))), Both);

            // Rule off: each bag is its own again.
            s.SharedSpoils = false;
            await hubA.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
            await Until(() => !roomA.Current.SharedSpoils && !roomB.Current.SharedSpoils, () => { });
            Both();
            Spoils.PickUp(gameB, Pendant);
            for (int i = 0; i < 10; i++) { Both(); await Task.Delay(40); }
            Assert.Equal(6, Spoils.Read(gameA).Count()[Pendant]);
            Assert.Null(a.Total);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        }
    }
}
