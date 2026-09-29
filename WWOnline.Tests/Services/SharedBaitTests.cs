using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Server;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

internal static class Bag
{
    public const byte B = BaitBagSlots.Bait;
    public const byte P = BaitBagSlots.Pear;
    public const byte E = BaitBagSlots.Empty;

    /// <summary>Slots from a short list: ("B", 3) = bait with 3 uses, ("P", 3) = pear, ("-", 0) = empty; padded to 8.</summary>
    public static BaitBagSlots Of(params (byte Item, byte Num)[] slots)
    {
        var items = Enumerable.Repeat(E, BaitCounts.SlotCount).ToArray();
        var nums = new byte[BaitCounts.SlotCount];
        for (int i = 0; i < slots.Length; i++) (items[i], nums[i]) = slots[i];
        return new BaitBagSlots(items, nums);
    }

    public static BaitCounts C(int bait, int pears) => new(bait, pears);

    /// <summary>A game in gameplay (Link, a settled stage, no stage change), idle, holding <paramref name="bag"/>.</summary>
    public static FakeDolphin Game(BaitBagSlots bag, bool ownsBag = true)
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.Player.LinkActorPointer.Address, 0x80A00000);
        d.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, "sea"u8.ToArray().Concat(new byte[5]).ToArray());
        d.Set(GameMemoryAddresses.Inventory.BaitBag.Address, ownsBag ? ItemIDs.MainItems.BaitBag : (byte)0xFF);
        d.Set(GameMemoryAddresses.Player.SelectItemSlots, 0xFF, 0xFF, 0xFF);
        d.Set(GameMemoryAddresses.Player.PlaySelectItems, 0xFF, 0xFF, 0xFF);
        Put(d, bag);
        return d;
    }

    public static void Put(FakeDolphin d, BaitBagSlots bag)
    {
        d.Set(GameMemoryAddresses.Inventory.BaitItems, bag.Items);
        d.Set(GameMemoryAddresses.Inventory.BaitNums, bag.Nums);
    }

    public static BaitBagSlots Read(FakeDolphin d) => BaitBagMemory.Read(d)!;
}

public class BaitCountsTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(3, 0, 1)]
    [InlineData(4, 0, 2)]
    [InlineData(24, 0, 8)]
    [InlineData(5, 2, 4)]
    public void SlotsNeeded_PacksBaitIntoThrees(int bait, int pears, int slots) =>
        Assert.Equal(slots, BaitCounts.SlotsNeeded(bait, pears));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(24, 0, true)]
    [InlineData(0, 8, true)]
    [InlineData(21, 1, true)]
    [InlineData(22, 1, false)] // 8 bait slots + 1 pear
    [InlineData(25, 0, false)]
    [InlineData(-1, 0, false)]
    [InlineData(0, 9, false)]
    [InlineData(int.MinValue, 0, false)]
    public void IsValidTotal(int bait, int pears, bool valid) =>
        Assert.Equal(valid, new BaitCounts(bait, pears).IsValidTotal());

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(-1, 0, true)]
    [InlineData(0, 1, true)]
    [InlineData(-24, -8, true)]
    [InlineData(25, 0, false)]
    [InlineData(0, -9, false)]
    [InlineData(int.MinValue, 0, false)]
    [InlineData(0, int.MaxValue, false)]
    public void IsValidDelta(int bait, int pears, bool valid) =>
        Assert.Equal(valid, new BaitCounts(bait, pears).IsValidDelta());

    [Fact]
    public void DeltaText_IsSigned() => Assert.Equal("-1 bait, +1 pear", new BaitCounts(-1, 1).DeltaText());
}

public class BaitStoreTests
{
    [Fact]
    public void Delta_IsRejected_WhileUnseeded()
    {
        var store = new BaitStore();
        Assert.Null(store.ApplyDelta(Bag.C(3, 0)));
        Assert.False(store.IsSeeded);

        var (total, seeded) = store.Join(Bag.C(5, 1));
        Assert.True(seeded);
        Assert.Equal(Bag.C(5, 1), total);
        Assert.Equal(Bag.C(4, 2), store.ApplyDelta(Bag.C(-1, 1)));
    }

    [Fact]
    public void OnlyTheFirstJoinSeeds()
    {
        var store = new BaitStore();
        store.Join(Bag.C(6, 0));
        var (total, seeded) = store.Join(Bag.C(0, 3));
        Assert.False(seeded);
        Assert.Equal(Bag.C(6, 0), total);
    }

    [Fact]
    public void InvalidDeltas_AreRejected()
    {
        var store = new BaitStore();
        store.Join(Bag.C(5, 1));
        Assert.Null(store.ApplyDelta(Bag.C(0, 0)));
        Assert.Null(store.ApplyDelta(Bag.C(int.MinValue, 0)));
        Assert.Null(store.ApplyDelta(Bag.C(0, 9)));
        Assert.Equal(Bag.C(5, 1), store.Join(Bag.C(0, 0)).Total);
    }

    [Fact]
    public void Uses_ClampAtZero()
    {
        var store = new BaitStore();
        store.Join(Bag.C(2, 1));
        Assert.Equal(Bag.C(0, 0), store.ApplyDelta(Bag.C(-3, -2)));
    }

    [Fact]
    public void AGainThatOverfillsTheBag_IsCutToWhatFits()
    {
        // 7 bait slots (21 uses) + 1 pear = 8 slots: two players both buying the last slot.
        Assert.Equal(Bag.C(21, 1), BaitStore.Add(Bag.C(21, 0), Bag.C(0, 1)));
        Assert.Equal(Bag.C(21, 1), BaitStore.Add(Bag.C(21, 1), Bag.C(0, 1)));
        Assert.Equal(Bag.C(21, 1), BaitStore.Add(Bag.C(21, 1), Bag.C(3, 0)));
        // A use and a gain together: the use stands, the gain only takes what's free (20 uses still need 7 slots).
        Assert.Equal(Bag.C(20, 1), BaitStore.Add(Bag.C(21, 1), Bag.C(-1, 1)));
        Assert.Equal(Bag.C(24, 0), BaitStore.Add(Bag.C(20, 0), Bag.C(24, 0)));
    }

    [Fact]
    public void Reset_MakesTheNextJoinReseed()
    {
        var store = new BaitStore();
        store.Join(Bag.C(9, 0));
        store.Reset();
        Assert.False(store.IsSeeded);
        Assert.Null(store.ApplyDelta(Bag.C(-1, 0)));
        Assert.Equal((Bag.C(3, 2), true), store.Join(Bag.C(3, 2)));
    }
}

public class BaitBagLayoutTests
{
    private const byte B = Bag.B, P = Bag.P, E = Bag.E;

    [Fact]
    public void Count_SumsBaitUses_AndCountsPearSlots()
    {
        var bag = Bag.Of((B, 3), (P, 3), (B, 1), (E, 0), (P, 0), (0x99, 3));
        Assert.Equal(Bag.C(4, 2), bag.Count()); // an unknown item counts as neither
    }

    [Fact]
    public void FromEmpty_FillsTheFirstSlots_TheWayTheGameStacks()
    {
        var next = BaitBag.Apply(Bag.Of(), Bag.C(5, 1));
        Assert.Equal(new[] { B, B, P, E, E, E, E, E }, next.Items);
        Assert.Equal(new byte[] { 3, 2, 3, 0, 0, 0, 0, 0 }, next.Nums);
    }

    [Fact]
    public void UsesComeOffTheEmptiestSlot_AndASlotAtZeroEmpties()
    {
        var next = BaitBag.Apply(Bag.Of((B, 3), (B, 1), (B, 3)), Bag.C(5, 0));
        Assert.Equal(new[] { B, E, B, E, E, E, E, E }, next.Items);
        Assert.Equal(new byte[] { 3, 0, 2, 0, 0, 0, 0, 0 }, next.Nums);
    }

    [Fact]
    public void RemovalAvoidsTheSlotsOnAButton()
    {
        // Slot 1 (the partial one) is on X: take from slot 2 instead.
        var next = BaitBag.Apply(Bag.Of((B, 3), (B, 1), (B, 3)), Bag.C(6, 0), equippedMask: 1 << 1);
        Assert.Equal(new byte[] { 3, 1, 2, 0, 0, 0, 0, 0 }, next.Nums);

        // Pears: the highest slot not on a button goes first.
        var pears = BaitBag.Apply(Bag.Of((P, 3), (P, 3), (P, 3)), Bag.C(0, 2), equippedMask: 1 << 2);
        Assert.Equal(new[] { P, E, P, E, E, E, E, E }, pears.Items);
    }

    [Fact]
    public void AddedBait_TopsUpPartialSlotsFirst()
    {
        // +4 uses: 2 top up slot 0, the other 2 go into the first empty slot.
        var next = BaitBag.Apply(Bag.Of((B, 1), (P, 3)), Bag.C(5, 1));
        Assert.Equal(new[] { B, P, B, E, E, E, E, E }, next.Items);
        Assert.Equal(new byte[] { 3, 3, 2, 0, 0, 0, 0, 0 }, next.Nums);
    }

    [Fact]
    public void WhenItDoesNotFit_TheBaitIsRepacked()
    {
        // 6 partial slots (6 uses) + 2 pears = 8 slots; the room has 6 uses and 4 pears (2 + 4 slots).
        var bag = Bag.Of((B, 1), (B, 1), (B, 1), (B, 1), (B, 1), (B, 1), (P, 3), (P, 3));
        var next = BaitBag.Apply(bag, Bag.C(6, 4));
        Assert.Equal(new[] { B, B, P, P, E, E, P, P }, next.Items);
        Assert.Equal(new byte[] { 3, 3, 3, 3, 0, 0, 3, 3 }, next.Nums);
    }

    [Fact]
    public void UnknownItems_AreLeftAlone_AndOnlyWhatFitsIsWritten()
    {
        var bag = Bag.Of((0x99, 1), (0x99, 1), (0x99, 1), (0x99, 1), (0x99, 1), (0x99, 1), (0x99, 1));
        var next = BaitBag.Apply(bag, Bag.C(6, 1));
        Assert.Equal(Bag.C(3, 0), next.Count());
        Assert.All(next.Items.Take(7), i => Assert.Equal(0x99, i));
    }

    [Fact]
    public void SameCounts_ChangeNothing()
    {
        var bag = Bag.Of((B, 2), (P, 3), (B, 3));
        Assert.True(BaitBag.Apply(bag, bag.Count()).SameAs(bag));
    }

    [Fact]
    public void EquippedMask_ReadsTheBaitSlotsOnXYZ()
    {
        Assert.Equal(1 << 0 | 1 << 7, BaitBag.EquippedMask([36, 43, 0x05]));
        Assert.Equal(0, BaitBag.EquippedMask([0xFF, 35, 44]));
    }

    [Fact]
    public void FixButtons_ClearsAButtonWhoseSlotEmptied_AndShowsANewItem()
    {
        var before = Bag.Of((P, 3), (B, 3), (E, 0));
        var after = Bag.Of((E, 0), (B, 2), (B, 3));
        // X on slot 0 (pear, now empty), Y on slot 1 (bait, still bait), Z on the Deku Leaf.
        var (save, play, changed) = BaitBag.FixButtons(before, after, [36, 37, 6], [P, B, 0x34]);
        Assert.True(changed);
        Assert.Equal(new byte[] { 0xFF, 37, 6 }, save);
        Assert.Equal(new byte[] { 0xFF, B, 0x34 }, play);

        var (_, play2, changed2) = BaitBag.FixButtons(Bag.Of((E, 0)), Bag.Of((B, 3)), [36, 0xFF, 0xFF], [0xFF, 0xFF, 0xFF]);
        Assert.True(changed2);
        Assert.Equal(B, play2[0]);
    }
}

public class BaitBagMemoryTests
{
    private const byte B = Bag.B, P = Bag.P, E = Bag.E;

    [Fact]
    public void Addresses_AreTheSaveFields()
    {
        // d_save.h: mBagItem 0x76 (+8 mBait), mGetBagItem 0x90 (+5 mBaitFlags), mBagItemRecord 0x9C (+8 mBaitNum).
        Assert.Equal(0x803C4C86u, GameMemoryAddresses.Inventory.BaitItems);
        Assert.Equal(0x803C4C9Du, GameMemoryAddresses.Inventory.BaitGetFlags);
        Assert.Equal(0x803C4CACu, GameMemoryAddresses.Inventory.BaitNums);
        Assert.Equal(GameMemoryAddresses.Player.SelectItemSlots, GameMemoryAddresses.Player.XButtonItem.Address);
        Assert.Equal(0x803CA7DBu, GameMemoryAddresses.Player.PlaySelectItems); // stb 0x5BD3(gameInfo)
    }

    [Fact]
    public void Write_ChangesTheBag_TheButtons_AndTheGetFlags()
    {
        var bag = Bag.Of((P, 3), (B, 3));
        var d = Bag.Game(bag);
        d.Set(GameMemoryAddresses.Player.SelectItemSlots, 36, 37, 0xFF);
        d.Set(GameMemoryAddresses.Player.PlaySelectItems, P, B, 0xFF);

        var next = BaitBag.Apply(bag, Bag.C(3, 0), BaitBag.EquippedMask([36, 37, 0xFF]));
        Assert.Equal(BaitBagMemory.WriteResult.Written, BaitBagMemory.Write(d, bag, next));

        Assert.True(Bag.Read(d).SameAs(Bag.Of((E, 0), (B, 3))));
        Assert.Equal(0xFF, d.Get(GameMemoryAddresses.Player.SelectItemSlots));      // X was the pear: cleared
        Assert.Equal(0xFF, d.Get(GameMemoryAddresses.Player.PlaySelectItems));
        Assert.Equal(37, d.Get(GameMemoryAddresses.Player.SelectItemSlots + 1));    // Y keeps its bait
        Assert.Equal(B, d.Get(GameMemoryAddresses.Player.PlaySelectItems + 1));
        Assert.Equal(1, d.Get(GameMemoryAddresses.Inventory.BaitGetFlags));         // bait "ever obtained"
    }

    [Fact]
    public void Write_DoesNothing_WhenTheGameChangedTheBagSinceItWasRead()
    {
        var read = Bag.Of((B, 3), (B, 3));
        var d = Bag.Game(Bag.Of((B, 3), (B, 2))); // a use landed after our read
        var next = BaitBag.Apply(read, Bag.C(9, 0));

        Assert.Equal(BaitBagMemory.WriteResult.Raced, BaitBagMemory.Write(d, read, next));
        Assert.True(Bag.Read(d).SameAs(Bag.Of((B, 3), (B, 2))));
    }
}

/// <summary>The hub's bait methods: gated by the rule, seeded by the owner, every input validated.</summary>
[Collection("GameHub static state")]
public class SharedBaitHubTests
{
    internal static async Task<(WebApplication App, int Port)> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        ServerApp.AddServices(builder.Services);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        ServerApp.MapEndpoints(app);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return (app, new Uri(address).Port);
    }

    internal static string Name(string role) => $"{role}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>Owner: set the bait rule (turning it off clears the room's bait, so tests start from an unseeded bag).</summary>
    internal static async Task SetBaitRule(SignalRClientService owner, bool on)
    {
        var s = await owner.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
        s.SharedBait = on;
        await owner.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
    }

    /// <summary>The next total <paramref name="client"/> receives after <paramref name="send"/>, within <paramref name="ms"/>; else null.</summary>
    private static async Task<BaitCounts?> NextTotal(SignalRClientService client, Func<Task> send, int ms = 1500)
    {
        var got = new TaskCompletionSource<BaitCounts>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(BaitCounts t) => got.TrySetResult(t);
        client.BaitTotalReceived += Handler;
        try
        {
            await send();
            return await Task.WhenAny(got.Task, Task.Delay(ms)) == got.Task ? got.Task.Result : null;
        }
        finally
        {
            client.BaitTotalReceived -= Handler;
        }
    }

    [Fact]
    public async Task OwnerSeeds_JoinersAdopt_DeltasAreValidated_AndTheRuleGatesEverything()
    {
        GameHub.ConfigureHostToken("bait-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true);
        try
        {
            var (app, port) = await StartAsync();
            await using var _ = app;
            await using var owner = new SignalRClientService();
            Assert.True((await owner.ConnectAsync("127.0.0.1", port, Name("Owner"), "bait-key")).success);
            await using var player = new SignalRClientService();
            Assert.True((await player.ConnectAsync("127.0.0.1", port, Name("Player"))).success);
            await SetBaitRule(owner, false);
            await SetBaitRule(owner, true);

            // The player can't seed while the owner hasn't; a bag the game can't make is refused.
            Assert.Null(await player.JoinBaitAsync(Bag.C(0, 0)));
            Assert.Null(await owner.JoinBaitAsync(Bag.C(25, 0)));
            Assert.Null(await owner.Connection!.InvokeAsync<BaitCounts?>(HubConstants.JoinBait, (object?)null));

            Assert.Equal(Bag.C(5, 1), await owner.JoinBaitAsync(Bag.C(5, 1)));
            Assert.Equal(Bag.C(5, 1), await player.JoinBaitAsync(Bag.C(0, 3))); // joiners adopt the room's

            // Invalid deltas: dropped, nobody hears anything.
            Assert.Null(await NextTotal(owner, () => player.SendBaitDeltaAsync(Bag.C(0, 0)), 400));
            Assert.Null(await NextTotal(owner, () => player.SendBaitDeltaAsync(Bag.C(int.MinValue, 0)), 400));
            Assert.Null(await NextTotal(owner, () => player.SendBaitDeltaAsync(Bag.C(0, 9)), 400));
            Assert.Null(await NextTotal(owner, () => player.Connection!.InvokeAsync(HubConstants.SendBaitDelta, (object?)null), 400));

            // A use reaches everyone; an over-full gain is cut to what fits.
            Assert.Equal(Bag.C(4, 1), await NextTotal(owner, () => player.SendBaitDeltaAsync(Bag.C(-1, 0))));
            Assert.Equal(Bag.C(21, 1), await NextTotal(player, () => owner.SendBaitDeltaAsync(Bag.C(24, 0))));

            // Rule off: joins return null and deltas are ignored. Back on: the bag re-seeds from the owner.
            await SetBaitRule(owner, false);
            Assert.Null(await owner.JoinBaitAsync(Bag.C(1, 0)));
            Assert.Null(await NextTotal(owner, () => player.SendBaitDeltaAsync(Bag.C(-1, 0)), 400));
            await SetBaitRule(owner, true);
            Assert.Null(await NextTotal(owner, () => player.SendBaitDeltaAsync(Bag.C(-1, 0)), 400)); // unseeded: must rejoin
            Assert.Equal(Bag.C(2, 0), await owner.JoinBaitAsync(Bag.C(2, 0)));
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true);
        }
    }

    [Fact]
    public void RulesSummary_NamesTheBaitRule()
    {
        Assert.Contains("shared bait bag OFF", new RoomSettings { SharedBait = false }.RulesSummary());
        Assert.Contains("shared bait bag ON", new RoomSettings().RulesSummary());
    }
}

/// <summary>Two games (FakeDolphin) through the real hub: the owner's bag seeds, uses and purchases reach the other bag.</summary>
[Collection("GameHub static state")]
public class SharedBaitServiceTests
{
    private const byte B = Bag.B, P = Bag.P, E = Bag.E;

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
        GameHub.ConfigureHostToken("bait-svc-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var hubA = new SignalRClientService();
            await using var hubB = new SignalRClientService();
            using var roomA = new RoomSettingsService(hubA);
            using var roomB = new RoomSettingsService(hubB);
            Assert.True((await hubA.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Owner"), "bait-svc-key")).success);
            Assert.True((await hubB.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Player"))).success);
            await SharedBaitHubTests.SetBaitRule(hubA, false);
            await SharedBaitHubTests.SetBaitRule(hubA, true);
            await Until(() => roomA.Current.SharedBait && roomB.Current.SharedBait, () => { });

            var gameA = Bag.Game(Bag.Of((B, 3), (B, 2), (P, 3)));
            var gameB = Bag.Game(Bag.Of());
            using var a = new SharedBaitService(gameA, hubA, roomA);
            using var b = new SharedBaitService(gameB, hubB, roomB);
            a.Attach();
            b.Attach();
            void Both() { a.Tick(); b.Tick(); }

            // The owner's bag seeds the room; the player's (empty) bag becomes the room's.
            await Until(() => Equals(a.Total, Bag.C(5, 1)), a.Tick);
            await Until(() => Bag.Read(gameB).Count().Equals(Bag.C(5, 1)), b.Tick);
            Assert.Equal(new[] { B, B, P, E, E, E, E, E }, Bag.Read(gameB).Items);
            Assert.Equal(new byte[] { 3, 2, 3, 0, 0, 0, 0, 0 }, Bag.Read(gameB).Nums);

            // The player feeds a bait to a fish: the owner's bag loses a use (off its partial slot).
            gameB.Set(GameMemoryAddresses.Inventory.BaitNums + 1, 1);
            await Until(() => Bag.Read(gameA).Count().Equals(Bag.C(4, 1)), Both);
            Assert.Equal(new byte[] { 3, 1, 3 }, Bag.Read(gameA).Nums[..3]);

            // The owner buys a Hyoi Pear: it lands in the player's first empty slot.
            gameA.Set(GameMemoryAddresses.Inventory.BaitItems + 3, P);
            gameA.Set(GameMemoryAddresses.Inventory.BaitNums + 3, 3);
            await Until(() => Bag.Read(gameB).Count().Equals(Bag.C(4, 2)), Both);
            Assert.Equal(P, Bag.Read(gameB).Items[3]);

            // While an event runs the player's bag isn't rewritten; the change lands once it ends.
            gameB.Set(GameMemoryAddresses.Events.EventMode, 1);
            gameA.Set(GameMemoryAddresses.Inventory.BaitItems + 2, E); // the owner's pear is eaten by a seagull
            await Until(() => Equals(b.Total, Bag.C(4, 1)), Both);
            for (int i = 0; i < 5; i++) Both();
            Assert.Equal(Bag.C(4, 2), Bag.Read(gameB).Count());
            gameB.Set(GameMemoryAddresses.Events.EventMode, 0);
            await Until(() => Bag.Read(gameB).Count().Equals(Bag.C(4, 1)), Both);

            // Rule off: each bag is its own again.
            await SharedBaitHubTests.SetBaitRule(hubA, false);
            await Until(() => !roomA.Current.SharedBait && !roomB.Current.SharedBait, () => { });
            Both();
            gameB.Set(GameMemoryAddresses.Inventory.BaitNums, 1); // uses 2 of slot 0
            for (int i = 0; i < 10; i++) { Both(); await Task.Delay(40); }
            Assert.Equal(Bag.C(4, 1), Bag.Read(gameA).Count());
            Assert.Null(a.Total);
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true);
        }
    }

    [Fact]
    public async Task AGameWithoutTheBag_NeverJoins()
    {
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true);
        var (app, port) = await SharedBaitHubTests.StartAsync();
        await using var _ = app;
        await using var hub = new SignalRClientService();
        using var room = new RoomSettingsService(hub);
        Assert.True((await hub.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("NoBag"))).success);

        var game = Bag.Game(Bag.Of(), ownsBag: false);
        using var svc = new SharedBaitService(game, hub, room);
        svc.Attach();
        for (int i = 0; i < 12; i++) { svc.Tick(); await Task.Delay(40); }
        Assert.Null(svc.Total);
        Assert.True(Bag.Read(game).SameAs(Bag.Of()));
    }
}
