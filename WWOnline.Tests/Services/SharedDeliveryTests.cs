using Microsoft.AspNetCore.SignalR.Client;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

internal static class Delivery
{
    public const byte E = DeliveryBagSlots.Empty;
    public const int Town = 0, Sea = 1, Exotic = 2, Pinwheel = 6, Father = 12, NoteToMom = 13, Maggie = 14, Moblin = 15,
                     Cabana = 16, Coupon = 18;

    public static byte Item(int type) => DeliveryCounts.Types[type].ItemNo;
    public static uint Bit(params int[] types) => types.Aggregate(0u, (b, t) => b | 1u << t);

    /// <summary>Counts by type, e.g. C((Town, 2), (Moblin, 1)), with these obtained bits.</summary>
    public static DeliveryCounts C(uint obtained, params (int Type, int Count)[] counts)
    {
        var c = new int[DeliveryCounts.TypeCount];
        foreach (var (t, n) in counts) c[t] = n;
        return new DeliveryCounts(c, obtained);
    }

    /// <summary>A bag holding these items in slot order (the rest empty); obtained = every item held.</summary>
    public static DeliveryBagSlots Bag(params int[] types)
    {
        var items = Enumerable.Repeat(E, DeliveryCounts.SlotCount).ToArray();
        for (int i = 0; i < types.Length; i++) items[i] = types[i] < 0 ? E : Item(types[i]);
        return new DeliveryBagSlots(items, Bit(types.Where(t => t >= 0).ToArray()));
    }

    public static FakeDolphin Game(DeliveryBagSlots bag, bool ownsBag = true)
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.Player.LinkActorPointer.Address, 0x80A00000);
        d.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, "sea"u8.ToArray().Concat(new byte[5]).ToArray());
        d.Set(GameMemoryAddresses.Inventory.DeliveryBag.Address, ownsBag ? ItemIDs.MainItems.DeliveryBag : (byte)0xFF);
        d.Set(GameMemoryAddresses.Player.SelectItemSlots, 0xFF, 0xFF, 0xFF);
        d.Set(GameMemoryAddresses.Player.PlaySelectItems, 0xFF, 0xFF, 0xFF);
        Put(d, bag);
        return d;
    }

    public static void Put(FakeDolphin d, DeliveryBagSlots bag)
    {
        d.Set(GameMemoryAddresses.Inventory.DeliveryItems, bag.Items);
        d.SetU32(GameMemoryAddresses.Inventory.DeliveryGetFlags, bag.Flags);
    }

    public static DeliveryBagSlots Read(FakeDolphin d) => DeliveryBagMemory.Read(d)!;

    /// <summary>An NPC gives an item the way item_func_* does it: onReserve, then setReserveItem (first empty slot).</summary>
    public static void Receive(FakeDolphin d, int type)
    {
        d.SetU32(GameMemoryAddresses.Inventory.DeliveryGetFlags, d.GetU32(GameMemoryAddresses.Inventory.DeliveryGetFlags) | 1u << type);
        int free = Array.IndexOf(Read(d).Items, E);
        d.Set(GameMemoryAddresses.Inventory.DeliveryItems + (uint)free, Item(type));
    }

    /// <summary>A trader swaps the shown item in place (setReserveItemChange) and marks the new one obtained (npc_roten).</summary>
    public static void Trade(FakeDolphin d, int from, int to)
    {
        int slot = Array.IndexOf(Read(d).Items, Item(from));
        d.Set(GameMemoryAddresses.Inventory.DeliveryItems + (uint)slot, Item(to));
        d.SetU32(GameMemoryAddresses.Inventory.DeliveryGetFlags, d.GetU32(GameMemoryAddresses.Inventory.DeliveryGetFlags) | 1u << to);
    }

    /// <summary>An item handed over or posted (setReserveItemEmpty): its slot empties, the obtained bit stays.</summary>
    public static void HandOver(FakeDolphin d, int type)
    {
        int slot = Array.IndexOf(Read(d).Items, Item(type));
        d.Set(GameMemoryAddresses.Inventory.DeliveryItems + (uint)slot, E);
    }
}

public class DeliveryCountsTests
{
    private const int Town = Delivery.Town, Sea = Delivery.Sea, Moblin = Delivery.Moblin;

    [Fact]
    public void Totals_FitTheEightSlots_AndOnlyKnownBits()
    {
        Assert.True(new DeliveryCounts().IsValidTotal());
        Assert.True(Delivery.C(0, (Town, 3), (Sea, 5)).IsValidTotal());
        Assert.False(Delivery.C(0, (Town, 3), (Sea, 6)).IsValidTotal()); // 9 items, 8 slots
        Assert.False(Delivery.C(0, (Town, -1)).IsValidTotal());
        Assert.False(Delivery.C(1u << 19).IsValidTotal()); // the unused salvage items' bits
        Assert.True(Delivery.C(DeliveryCounts.KnownObtainedMask).IsValidTotal());
        Assert.False(new DeliveryCounts { Counts = null! }.IsValidTotal());
        Assert.False(new DeliveryCounts { Counts = new int[18] }.IsValidTotal());
    }

    [Fact]
    public void Deltas_ChangeSomething_AndStayWithinABag()
    {
        Assert.False(new DeliveryCounts().IsValidDelta());
        Assert.True(Delivery.C(0, (Town, -1), (Sea, 1)).IsValidDelta());
        Assert.True(Delivery.C(Delivery.Bit(Moblin)).IsValidDelta()); // only a new obtained bit
        Assert.False(Delivery.C(0, (Town, 9)).IsValidDelta());
        Assert.False(Delivery.C(0, (Town, int.MinValue)).IsValidDelta());
        Assert.False(Delivery.C(1u << 31, (Town, 1)).IsValidDelta());
        Assert.False(new DeliveryCounts { Counts = null! }.IsValidDelta());
    }

    [Fact]
    public void Minus_IsPerType_AndTheNewlyObtainedBits()
    {
        var now = Delivery.C(Delivery.Bit(Town, Sea), (Sea, 1));
        var before = Delivery.C(Delivery.Bit(Town), (Town, 1));
        Assert.Equal(Delivery.C(Delivery.Bit(Sea), (Town, -1), (Sea, 1)), now.Minus(before));
        Assert.Equal(0u, before.Minus(now).Obtained); // bits never go away
    }

    [Fact]
    public void Text_NamesTheItems()
    {
        Assert.Equal("-1 Town Flower, +1 Sea Flower, first obtained: Sea Flower",
                     Delivery.C(Delivery.Bit(Sea), (Town, -1), (Sea, 1)).DeltaText());
        Assert.Equal("2 Town Flower, Moblin's Letter", Delivery.C(0, (Town, 2), (Moblin, 1)).ToString());
        Assert.Equal("empty", new DeliveryCounts().ToString());
        Assert.Equal("(malformed)", new DeliveryCounts { Counts = null! }.ToString());
    }

    [Fact]
    public void TypeTable_MatchesTheGame()
    {
        // d_item_data.h 0x8C (dItemNo_TOWN_FLOWER_e) .. 0x9E (dItemNo_FILL_UP_COUPON_e); item_func_flower_1 ..
        // item_func_kaisen_present2 call onGetItemReserve(itemNo - 0x8C).
        Assert.Equal(19, DeliveryCounts.TypeCount);
        for (int t = 0; t < DeliveryCounts.TypeCount; t++)
        {
            Assert.Equal(0x8C + t, DeliveryCounts.Types[t].ItemNo);
            Assert.Equal(t, DeliveryCounts.TypeOf(DeliveryCounts.Types[t].ItemNo));
        }
        Assert.Equal(ItemIDs.Delivery.TownFlower, DeliveryCounts.Types[0].ItemNo);
        Assert.Equal(ItemIDs.Delivery.FillUpCoupon, DeliveryCounts.Types[^1].ItemNo);
        Assert.Equal(-1, DeliveryCounts.TypeOf(0x9F));
        Assert.Equal(-1, DeliveryCounts.TypeOf(0xFF));
        Assert.Equal(-1, DeliveryCounts.TypeOf(0x8B));
    }
}

public class DeliveryStoreTests
{
    private const int Town = Delivery.Town, Sea = Delivery.Sea, Exotic = Delivery.Exotic, Moblin = Delivery.Moblin, Coupon = Delivery.Coupon;

    [Fact]
    public void Delta_IsRejected_WhileUnseeded_ThenApplied()
    {
        var store = new DeliveryStore();
        Assert.Null(store.ApplyDelta(Delivery.C(Delivery.Bit(Town), (Town, 1))));
        var (total, seeded) = store.Join(Delivery.C(Delivery.Bit(Moblin), (Moblin, 1)));
        Assert.True(seeded);
        Assert.Equal(Delivery.C(Delivery.Bit(Moblin), (Moblin, 1)), total);
        Assert.Equal(Delivery.C(Delivery.Bit(Moblin, Town), (Moblin, 1), (Town, 1)),
                     store.ApplyDelta(Delivery.C(Delivery.Bit(Town), (Town, 1))));
        Assert.False(store.Join(new DeliveryCounts()).Seeded); // joiners adopt
    }

    [Fact]
    public void TwoPlayersTradingTheSameItem_OnlyTheFirstTradeCounts()
    {
        var store = new DeliveryStore();
        store.Join(Delivery.C(Delivery.Bit(Town), (Town, 1)));
        var trade = Delivery.C(Delivery.Bit(Sea), (Town, -1), (Sea, 1));

        var first = store.Apply(trade)!.Value;
        Assert.Null(first.Refused);
        Assert.Equal(Delivery.C(Delivery.Bit(Town, Sea), (Sea, 1)), first.Total);

        // The second player traded the same Town Flower before the first trade reached them: no second Sea Flower.
        var second = store.Apply(trade)!.Value;
        Assert.Contains("Town Flower", second.Refused);
        Assert.Equal(Delivery.C(Delivery.Bit(Town, Sea), (Sea, 1)), second.Total);

        // Two hand-overs of the same letter: it's gone once.
        store.Join(new DeliveryCounts());
        Assert.Equal(Delivery.C(Delivery.Bit(Town, Sea, Moblin), (Sea, 1), (Moblin, 1)),
                     store.ApplyDelta(Delivery.C(Delivery.Bit(Moblin), (Moblin, 1))));
        Assert.Equal(Delivery.C(Delivery.Bit(Town, Sea, Moblin), (Sea, 1)), store.ApplyDelta(Delivery.C(0, (Moblin, -1))));
        Assert.Equal(Delivery.C(Delivery.Bit(Town, Sea, Moblin), (Sea, 1)), store.ApplyDelta(Delivery.C(0, (Moblin, -1))));
    }

    [Fact]
    public void ARefusedTrade_StillMergesItsObtainedBits()
    {
        var total = DeliveryStore.Add(Delivery.C(0, (Sea, 1)), Delivery.C(Delivery.Bit(Exotic), (Town, -1), (Exotic, 1)));
        Assert.Equal(Delivery.C(Delivery.Bit(Exotic), (Sea, 1)), total);
    }

    [Fact]
    public void AnOverfullBag_CutsTheGains_NeverTheLosses()
    {
        var full = Delivery.C(0, (Town, 3), (Sea, 3), (Moblin, 1));
        // Two gains race for the last slot: the later item number is cut.
        Assert.Equal(Delivery.C(0, (Town, 3), (Sea, 3), (Moblin, 1), (Exotic, 1)),
                     DeliveryStore.Add(full, Delivery.C(0, (Exotic, 1), (Coupon, 1))));
        // A swap that frees a slot is fine.
        Assert.Equal(Delivery.C(0, (Town, 2), (Sea, 3), (Moblin, 1), (Exotic, 1), (Coupon, 1)),
                     DeliveryStore.Add(full, Delivery.C(0, (Town, -1), (Exotic, 1), (Coupon, 1))));
        Assert.Equal(1, DeliveryStore.Add(full, Delivery.C(0, (Exotic, 8)))[Exotic]);
    }

    [Fact]
    public void TwoPlayersGivenTheSameLetter_TheRoomKeepsOne_ButTradeGoodsCanDouble()
    {
        var store = new DeliveryStore();
        store.Join(new DeliveryCounts());
        var gotLetter = Delivery.C(Delivery.Bit(Moblin), (Moblin, 1));
        Assert.Equal(Delivery.C(Delivery.Bit(Moblin), (Moblin, 1)), store.ApplyDelta(gotLetter));
        var second = store.Apply(gotLetter)!.Value;
        Assert.Equal(Delivery.C(Delivery.Bit(Moblin), (Moblin, 1)), second.Total);
        Assert.Contains("already has Moblin's Letter", second.Refused);

        // Zunari sells Town Flowers until three trade goods are in the bag: two of them are two.
        var bought = Delivery.C(Delivery.Bit(Town), (Town, 1));
        store.ApplyDelta(bought);
        Assert.Equal(2, store.ApplyDelta(bought)![Town]);
    }

    [Fact]
    public void Reset_MakesTheNextJoinReseed()
    {
        var store = new DeliveryStore();
        store.Join(Delivery.C(0, (Town, 1)));
        store.Reset();
        Assert.False(store.IsSeeded);
        Assert.True(store.Join(Delivery.C(0, (Sea, 1))).Seeded);
    }
}

public class DeliveryBagTests
{
    private const int Town = Delivery.Town, Sea = Delivery.Sea, Pinwheel = Delivery.Pinwheel, Father = Delivery.Father,
                      Moblin = Delivery.Moblin, Cabana = Delivery.Cabana;
    private const byte E = Delivery.E;

    [Fact]
    public void Count_IsSlotsPerItem_AndUnknownItemsCountAsNone()
    {
        var bag = new DeliveryBagSlots([0x8C, 0x8C, 0x9F, E, 0x9B, E, E, E], Delivery.Bit(Town, Moblin) | 1u << 20);
        Assert.Equal(Delivery.C(Delivery.Bit(Town, Moblin), (Town, 2), (Moblin, 1)), bag.Count());
    }

    [Fact]
    public void ArrivingItems_TakeTheFirstEmptySlot_AndTheirObtainedBit()
    {
        var next = DeliveryBag.Apply(Delivery.Bag(Town, -1, Moblin), Delivery.C(0, (Town, 1), (Moblin, 1), (Cabana, 1), (Father, 1)));
        Assert.Equal(new byte[] { 0x8C, 0x98, 0x9B, 0x9C, E, E, E, E }, next.Items);
        Assert.Equal(Delivery.Bit(Town, Moblin, Cabana, Father), next.Flags);
    }

    [Fact]
    public void LeavingItems_EmptyTheHighestSlot_AndOnesOnAButtonLast()
    {
        var bag = Delivery.Bag(Town, Town, Sea, Town);
        Assert.Equal(new byte[] { 0x8C, 0x8C, 0x8D, E, E, E, E, E },
                     DeliveryBag.Apply(bag, Delivery.C(0, (Town, 2), (Sea, 1))).Items);
        // Slot 3 is on a button: slot 1 goes instead.
        Assert.Equal(new byte[] { 0x8C, E, 0x8D, 0x8C, E, E, E, E },
                     DeliveryBag.Apply(bag, Delivery.C(0, (Town, 2), (Sea, 1)), equippedMask: 1 << 3).Items);
        // Obtained bits stay.
        Assert.Equal(Delivery.Bit(Town, Sea), DeliveryBag.Apply(bag, new DeliveryCounts()).Flags);
    }

    [Fact]
    public void ATrade_LandsInTheSlotTheTradedItemLeft()
    {
        var next = DeliveryBag.Apply(Delivery.Bag(Moblin, Town, -1, Cabana), Delivery.C(0, (Moblin, 1), (Sea, 1), (Cabana, 1)));
        Assert.Equal(new byte[] { 0x9B, 0x8D, E, 0x9C, E, E, E, E }, next.Items);
    }

    [Fact]
    public void AFullBag_LeavesTheRest_AndCountSaysSo()
    {
        var unknown = new DeliveryBagSlots(Enumerable.Repeat((byte)0xA0, DeliveryCounts.SlotCount).ToArray());
        var next = DeliveryBag.Apply(unknown, Delivery.C(0, (Pinwheel, 1)));
        Assert.Equal(unknown.Items, next.Items);
        Assert.Equal(0, next.Count()[Pinwheel]);
    }

    [Fact]
    public void Buttons_OnAChangedSlot_FollowIt()
    {
        // X on bag slot 1 (inventory slot 49), which empties; Y on slot 0, which now holds a Sea Flower; Z elsewhere.
        var before = Delivery.Bag(Town, Moblin).Items;
        var after = Delivery.Bag(Sea).Items;
        var (save, play, changed) = BagMemory.FixButtons(DeliveryBag.FirstInvSlot, before, after,
            [49, 48, 11], [0x9B, 0x8C, 0x0B]);
        Assert.True(changed);
        Assert.Equal(new byte[] { 0xFF, 48, 11 }, save);
        Assert.Equal(new byte[] { 0xFF, 0x8D, 0x0B }, play);
        Assert.Equal(1 << 0 | 1 << 1, DeliveryBag.EquippedMask([49, 48, 11]));
    }
}

public class DeliveryBagMemoryTests
{
    private const int Town = Delivery.Town, Sea = Delivery.Sea, Moblin = Delivery.Moblin;

    [Fact]
    public void Addresses_AreTheSaveFields()
    {
        // d_save.h: mBagItem 0x76 (+0x10 mReserve), mGetBagItem 0x90 (+0 mReserveFlags); main.dol item_func_flower_1.
        Assert.Equal(0x803C4C8Eu, GameMemoryAddresses.Inventory.DeliveryItems);
        Assert.Equal(0x803C4C98u, GameMemoryAddresses.Inventory.DeliveryGetFlags);
        Assert.Equal(0x803C4C56u, GameMemoryAddresses.Inventory.DeliveryBag.Address); // dInvSlot_DELIVERY_BAG_e = 18
        Assert.Equal(0x30, ItemIDs.MainItems.DeliveryBag);                                  // dItemNo_DELIVERY_BAG_e
    }

    [Fact]
    public void Write_ChangesTheSlots_FixesTheButtons_AndOrsTheFlags()
    {
        var bag = Delivery.Bag(Town, Moblin);
        var d = Delivery.Game(bag);
        d.Set(GameMemoryAddresses.Player.SelectItemSlots, 49, 0xFF, 0xFF); // Moblin's Letter on X
        d.Set(GameMemoryAddresses.Player.PlaySelectItems, 0x9B, 0xFF, 0xFF);
        var next = DeliveryBag.Apply(bag, Delivery.C(Delivery.Bit(Delivery.Father), (Sea, 1)));

        Assert.Equal(BagWriteResult.Written, DeliveryBagMemory.Write(d, bag, next));
        Assert.Equal(new byte[] { 0x8D, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, Delivery.Read(d).Items);
        Assert.Equal(Delivery.Bit(Town, Moblin, Sea, Delivery.Father), d.GetU32(GameMemoryAddresses.Inventory.DeliveryGetFlags));
        Assert.Equal(0xFF, d.Get(GameMemoryAddresses.Player.SelectItemSlots));
        Assert.Equal(0xFF, d.Get(GameMemoryAddresses.Player.PlaySelectItems));
    }

    [Fact]
    public void Write_DoesNothing_WhenTheGameChangedTheBagSinceItWasRead()
    {
        var read = Delivery.Bag(Town);
        var d = Delivery.Game(read);
        Delivery.Trade(d, Town, Sea); // a trade landed after our read
        var next = DeliveryBag.Apply(read, Delivery.C(0, (Town, 1), (Moblin, 1)));

        Assert.Equal(BagWriteResult.Raced, DeliveryBagMemory.Write(d, read, next));
        Assert.Equal(Delivery.Bag(Sea).Items, Delivery.Read(d).Items);
    }
}

/// <summary>The hub's delivery methods: gated by the rule, seeded by the owner, every input validated.</summary>
[Collection("GameHub static state")]
public class SharedDeliveryHubTests
{
    private const int Town = Delivery.Town, Sea = Delivery.Sea, Moblin = Delivery.Moblin;

    private static async Task SetDeliveryRule(SignalRClientService owner, bool on)
    {
        var s = await owner.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
        s.SharedDelivery = on;
        await owner.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
    }

    private static async Task<DeliveryCounts?> NextTotal(SignalRClientService client, Func<Task> send, int ms = 1500)
    {
        var got = new TaskCompletionSource<DeliveryCounts>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(DeliveryCounts t) => got.TrySetResult(t);
        client.DeliveryTotalReceived += Handler;
        try
        {
            await send();
            return await Task.WhenAny(got.Task, Task.Delay(ms)) == got.Task ? got.Task.Result : null;
        }
        finally
        {
            client.DeliveryTotalReceived -= Handler;
        }
    }

    [Fact]
    public async Task OwnerSeeds_JoinersAdopt_TradesAreRefereed_AndTheRuleGatesEverything()
    {
        GameHub.ConfigureHostToken("delivery-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true, sharedDelivery: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var owner = new SignalRClientService();
            Assert.True((await owner.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Owner"), "delivery-key")).success);
            await using var player = new SignalRClientService();
            Assert.True((await player.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Player"))).success);
            await SetDeliveryRule(owner, false);
            await SetDeliveryRule(owner, true);

            Assert.Null(await player.JoinDeliveryAsync(new DeliveryCounts()));                       // waits for the owner
            Assert.Null(await owner.JoinDeliveryAsync(Delivery.C(0, (Town, 9))));                     // doesn't fit
            Assert.Null(await owner.JoinDeliveryAsync(new DeliveryCounts { Counts = new int[3] }));    // malformed
            Assert.Null(await owner.Connection!.InvokeAsync<DeliveryCounts?>(HubConstants.JoinDelivery, (object?)null));

            var seed = Delivery.C(Delivery.Bit(Town, Moblin), (Town, 1), (Moblin, 1));
            Assert.Equal(seed, await owner.JoinDeliveryAsync(seed));
            Assert.Equal(seed, await player.JoinDeliveryAsync(Delivery.C(0, (Sea, 2)))); // joiners adopt the room's

            Assert.Null(await NextTotal(owner, () => player.SendDeliveryDeltaAsync(new DeliveryCounts()), 400));
            Assert.Null(await NextTotal(owner, () => player.SendDeliveryDeltaAsync(Delivery.C(0, (Town, int.MinValue))), 400));
            Assert.Null(await NextTotal(owner, () => player.SendDeliveryDeltaAsync(new DeliveryCounts { Counts = null! }), 400));
            Assert.Null(await NextTotal(owner, () => player.Connection!.InvokeAsync(HubConstants.SendDeliveryDelta, (object?)null), 400));

            // A trade reaches everyone; the same trade again (raced) changes nothing but is still answered.
            var trade = Delivery.C(Delivery.Bit(Sea), (Town, -1), (Sea, 1));
            var traded = Delivery.C(Delivery.Bit(Town, Moblin, Sea), (Sea, 1), (Moblin, 1));
            Assert.Equal(traded, await NextTotal(owner, () => player.SendDeliveryDeltaAsync(trade)));
            Assert.Equal(traded, await NextTotal(player, () => owner.SendDeliveryDeltaAsync(trade)));

            await SetDeliveryRule(owner, false);
            Assert.Null(await owner.JoinDeliveryAsync(new DeliveryCounts()));
            Assert.Null(await NextTotal(owner, () => player.SendDeliveryDeltaAsync(Delivery.C(0, (Sea, 1))), 400));
            await SetDeliveryRule(owner, true);
            Assert.Null(await NextTotal(owner, () => player.SendDeliveryDeltaAsync(Delivery.C(0, (Sea, 1))), 400)); // unseeded
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true, sharedDelivery: true);
        }
    }

    [Fact]
    public void RulesSummary_NamesTheDeliveryRule()
    {
        Assert.Contains("shared delivery bag OFF", new RoomSettings { SharedDelivery = false }.RulesSummary());
        Assert.Contains("shared delivery bag ON", new RoomSettings().RulesSummary());
    }
}

/// <summary>Two games (FakeDolphin) through the real hub.</summary>
[Collection("GameHub static state")]
public class SharedDeliveryServiceTests
{
    private const int Town = Delivery.Town, Sea = Delivery.Sea, Exotic = Delivery.Exotic, Moblin = Delivery.Moblin,
                      Maggie = Delivery.Maggie, Cabana = Delivery.Cabana;

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
    public async Task BagsFollowTheRoom_TradesDontDuplicate_AndTheRuleOffStopsIt()
    {
        GameHub.ConfigureHostToken("delivery-svc-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true, sharedDelivery: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var hubA = new SignalRClientService();
            await using var hubB = new SignalRClientService();
            using var roomA = new RoomSettingsService(hubA);
            using var roomB = new RoomSettingsService(hubB);
            Assert.True((await hubA.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Owner"), "delivery-svc-key")).success);
            Assert.True((await hubB.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Player"))).success);
            var s = await hubA.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
            s.SharedDelivery = false;
            await hubA.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
            s.SharedDelivery = true;
            await hubA.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
            await Until(() => roomA.Current.SharedDelivery && roomB.Current.SharedDelivery, () => { });

            var gameA = Delivery.Game(Delivery.Bag(Town, Moblin));
            var gameB = Delivery.Game(Delivery.Bag(Cabana));
            using var a = new SharedDeliveryService(gameA, hubA, roomA);
            using var b = new SharedDeliveryService(gameB, hubB, roomB);
            a.Attach();
            b.Attach();
            void Both() { a.Tick(); b.Tick(); }
            DeliveryCounts CountOf(FakeDolphin g) => Delivery.Read(g).Count();

            // The owner's bag seeds the room; the player's bag becomes it, and its own obtained bit reaches the room.
            await Until(() => CountOf(gameB).Counts.SequenceEqual(Delivery.C(0, (Town, 1), (Moblin, 1)).Counts), Both);
            await Until(() => (Delivery.Read(gameA).Flags & Delivery.Bit(Cabana)) != 0, Both);
            Assert.Equal(Delivery.Bit(Town, Moblin, Cabana), Delivery.Read(gameB).Flags);

            // The player receives Maggie's Letter: the owner has it too, obtained.
            Delivery.Receive(gameB, Maggie);
            await Until(() => CountOf(gameA)[Maggie] == 1 && (Delivery.Read(gameA).Flags & Delivery.Bit(Maggie)) != 0, Both);

            // The owner hands Moblin's Letter over: gone from both, still obtained (the game reads that as "passed").
            Delivery.HandOver(gameA, Moblin);
            await Until(() => CountOf(gameB)[Moblin] == 0, Both);
            Assert.NotEqual(0u, Delivery.Read(gameB).Flags & Delivery.Bit(Moblin));

            // Both trade the same Town Flower before either hears of the other's trade (one to a Sea Flower, one to
            // an Exotic Flower): the room keeps the first trade only, and both bags end the same, with one flower.
            Delivery.Trade(gameA, Town, Sea);
            Delivery.Trade(gameB, Town, Exotic);
            a.Tick();
            await Task.Delay(200);
            b.Tick();
            await Until(() => CountOf(gameA).Equals(CountOf(gameB)) && a.Total != null && CountOf(gameA).Counts.SequenceEqual(a.Total.Counts), Both);
            Assert.Equal(1, CountOf(gameA)[Sea] + CountOf(gameA)[Exotic]);
            Assert.Equal(0, CountOf(gameA)[Town]);
            Assert.Equal(1, CountOf(gameA)[Maggie]);

            // While an event runs the player's bag isn't rewritten.
            gameB.Set(GameMemoryAddresses.Events.EventMode, 1);
            Delivery.Receive(gameA, Cabana);
            await Until(() => b.Total?[Cabana] == 1, Both);
            for (int i = 0; i < 5; i++) Both();
            Assert.Equal(0, CountOf(gameB)[Cabana]);
            gameB.Set(GameMemoryAddresses.Events.EventMode, 0);
            await Until(() => CountOf(gameB)[Cabana] == 1, Both);

            // Rule off: each bag is its own again.
            s.SharedDelivery = false;
            await hubA.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
            await Until(() => !roomA.Current.SharedDelivery && !roomB.Current.SharedDelivery, () => { });
            Both();
            Delivery.HandOver(gameB, Maggie);
            for (int i = 0; i < 10; i++) { Both(); await Task.Delay(40); }
            Assert.Equal(1, CountOf(gameA)[Maggie]);
            Assert.Null(a.Total);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true, sharedDelivery: true);
        }
    }
}
