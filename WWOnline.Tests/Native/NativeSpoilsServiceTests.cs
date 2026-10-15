using Microsoft.AspNetCore.SignalR.Client;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Native;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using WWOnline.Tests.Services;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>A Dolphin game and a native game (its scene gate open through tww_gate_get_state) sharing a spoils bag through the real hub.</summary>
[Collection("GameHub static state")]
public class NativeSpoilsServiceTests
{
    private const int Crest = Spoils.Crest, Pendant = Spoils.Pendant, Skull = Spoils.Skull;

    private static uint Off(uint address) => address - GameMemoryAddresses.GameInfo;

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

    private static SpoilsBagSlots Read(FakeNativeGame g) => new(
        g.Save.AsSpan((int)Off(GameMemoryAddresses.Inventory.SpoilsItems), SpoilsCounts.TypeCount).ToArray(),
        g.Save.AsSpan((int)Off(GameMemoryAddresses.Inventory.SpoilsNums), SpoilsCounts.TypeCount).ToArray());

    /// <summary>d_meter's step on the native game: each pending count into mBeastNum, a type at 0 leaves its slot.</summary>
    private static void Meter(FakeNativeGame g)
    {
        for (int t = 0; t < SpoilsCounts.TypeCount; t++)
        {
            ref int p = ref g.Inv.Pending[(int)TwwInvCounter.Beast + t];
            if (p == 0) continue;
            int numOff = (int)Off(GameMemoryAddresses.Inventory.SpoilsNums) + t;
            int n = g.Save[numOff] + p;
            p = 0;
            g.Save[numOff] = (byte)Math.Clamp(n, 0, 99);
            if (n > 0) continue;
            int slot = Array.IndexOf(Read(g).Items, Spoils.Item(t));
            if (slot >= 0) g.Save[Off(GameMemoryAddresses.Inventory.SpoilsItems) + slot] = Spoils.E;
        }
    }

    [Fact]
    public async Task ANativeGame_FollowsTheRoomBag_AndSendsItsPickups()
    {
        GameHub.ConfigureHostToken("spoils-native-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var hubA = new SignalRClientService();
            await using var hubB = new SignalRClientService();
            using var roomA = new RoomSettingsService(hubA);
            using var roomB = new RoomSettingsService(hubB);
            Assert.True((await hubA.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Owner"), "spoils-native-key")).success);
            Assert.True((await hubB.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Player"))).success);
            await Until(() => roomA.Current.SharedSpoils && roomB.Current.SharedSpoils, () => { });

            var gameA = Spoils.Game(Spoils.Bag((Pendant, 5), (Crest, 2)));
            var nativeB = new FakeNativeGame();
            nativeB.Save[Off(GameMemoryAddresses.Inventory.SpoilsBag.Address)] = ItemIDs.MainItems.SpoilsBag;
            Spoils.Bag((Skull, 1)).Items.CopyTo(nativeB.Save, Off(GameMemoryAddresses.Inventory.SpoilsItems));
            nativeB.Save[Off(GameMemoryAddresses.Inventory.SpoilsNums) + Skull] = 1;
            nativeB.Gate = FakeNativeGame.InPlay("sea", 1);
            using var gameB = new NativeGameService(nativeB);
            using var a = new SharedSpoilsService(gameA, hubA, roomA);
            using var b = new SharedSpoilsService(gameB, hubB, roomB);
            a.Attach();
            b.Attach();
            void Both() { a.Tick(); Spoils.Meter(gameA); b.Tick(); Meter(nativeB); }

            // The owner's bag seeds the room; the native bag becomes it (the Skull Necklace leaves its slot).
            await Until(() => Equals(a.Total, Spoils.C((Pendant, 5), (Crest, 2))), a.Tick);
            await Until(() => Read(nativeB).Count().Equals(Spoils.C((Pendant, 5), (Crest, 2))), () => { b.Tick(); Meter(nativeB); });
            Assert.Equal(-1, Read(nativeB).SlotOf(Skull));

            // The native player picks up a Joy Pendant the way item_func does (+1 pending): the owner's count goes up.
            nativeB.Inv.Pending[(int)TwwInvCounter.Beast + Pendant]++;
            await Until(() => Spoils.Read(gameA).Count()[Pendant] == 6, Both);

            // The owner trades the crests away: the native bag loses them and the slot.
            gameA.Set(GameMemoryAddresses.Inventory.PendingSpoilsDeltas + 2 * Crest, 0xFF, 0xFE);
            await Until(() => Read(nativeB).Count()[Crest] == 0 && Spoils.Read(gameA).Count()[Crest] == 0, Both);
            Assert.Equal(-1, Read(nativeB).SlotOf(Crest));
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        }
    }
}
