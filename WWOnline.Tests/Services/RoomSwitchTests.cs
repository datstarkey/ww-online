using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WWOnline.Hubs;
using WWOnline.Server;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

internal static class Switches
{
    /// <summary>A place with the given switch numbers (0x80-0xEF) set.</summary>
    public static RoomSwitches At(string stage, int slot, int room, params int[] switches)
    {
        var s = new RoomSwitches { Stage = stage, Slot = slot, Room = room };
        foreach (int n in switches)
        {
            if (n < 0xC0) s.Dan[(n - 0x80) >> 5] |= 1u << ((n - 0x80) & 31);
            else s.Zone[(n - 0xC0) >> 5] |= 1u << ((n - 0xC0) & 31);
        }
        return s;
    }

    public static int[] Numbers(RoomSwitches s)
    {
        var list = new List<int>();
        for (int n = 0x80; n < 0xF0; n++)
        {
            uint w = n < 0xC0 ? s.Dan[(n - 0x80) >> 5] : s.Zone[(n - 0xC0) >> 5];
            if ((w >> ((n < 0xC0 ? n - 0x80 : n - 0xC0) & 31) & 1) != 0) list.Add(n);
        }
        return list.ToArray();
    }
}

public class RoomSwitchesTests
{
    [Fact]
    public void IsValid_ChecksEveryField()
    {
        Assert.True(Switches.At("M_NewD2", 3, 12, 0x80, 0xE0).IsValid());
        Assert.False(Switches.At("", 3, 12).IsValid());
        Assert.False(Switches.At("TooLongName", 3, 12).IsValid());
        Assert.False(Switches.At("bad name", 3, 12).IsValid());
        Assert.False(Switches.At("M_NewD2", -1, 12).IsValid());
        Assert.False(Switches.At("M_NewD2", 16, 12).IsValid());
        Assert.False(Switches.At("M_NewD2", 3, -1).IsValid());
        Assert.False(Switches.At("M_NewD2", 3, 64).IsValid());
        Assert.False(new RoomSwitches { Stage = "M_NewD2", Dan = new uint[1] }.IsValid());
        Assert.False(new RoomSwitches { Stage = "M_NewD2", Zone = new uint[3] }.IsValid());
        Assert.False(new RoomSwitches { Stage = "M_NewD2", Dan = null! }.IsValid());
        Assert.False(new RoomSwitches { Stage = null! }.IsValid());
        var beyondZone = Switches.At("M_NewD2", 3, 12);
        beyondZone.Zone[1] = 0x10000; // switch 0xF0: not a zone switch
        Assert.False(beyondZone.IsValid());
    }

    [Fact]
    public void Masked_ExceptAndDanOnly_KeepThePlace()
    {
        var s = Switches.At("kaze", 5, 9, 0x81, 0xC1, 0xE2);
        var masked = s.Masked([0b10, 0], [0, 0b100]);
        Assert.Equal([0x81, 0xE2], Switches.Numbers(masked));
        Assert.True(masked.SamePlace(s));
        Assert.Equal([0xC1, 0xE2], Switches.Numbers(s.Except(Switches.At("kaze", 5, 9, 0x81))));
        Assert.Equal([0x81], Switches.Numbers(s.DanOnly()));
    }
}

public class RoomSwitchStoreTests
{
    [Fact]
    public void Join_MergesAndReturnsThePlaceState_AndWhoGetsTheNewBits()
    {
        var store = new RoomSwitchStore();
        var a = store.Join("a", Switches.At("kaze", 5, 9, 0x81, 0xE0));
        Assert.Equal([0x81, 0xE0], Switches.Numbers(a.Added));
        Assert.Empty(a.SameRoom);

        store.Join("elsewhere", Switches.At("kaze", 5, 2));     // same stage, other room
        store.Join("otherCave", Switches.At("TF_02", 5, 9));    // same slot, another stage: another visit
        store.Join("otherStage", Switches.At("sea", 1, 9));     // another slot entirely
        var b = store.Join("b", Switches.At("kaze", 5, 9, 0xE1));

        Assert.Equal([0x81, 0xE0, 0xE1], Switches.Numbers(b.State)); // what b should apply
        Assert.Equal([0xE1], Switches.Numbers(b.Added));
        Assert.Equal(["a"], b.SameRoom);
        Assert.Equal(["elsewhere"], b.SameStage);
    }

    [Fact]
    public void ZoneBitsStayInTheirRoom_DanBitsInTheirStage()
    {
        var store = new RoomSwitchStore();
        store.Join("a", Switches.At("kaze", 5, 9, 0x81, 0xE0));
        var other = store.Join("b", Switches.At("kaze", 5, 2));
        Assert.Equal([0x81], Switches.Numbers(other.State)); // dan of the stage, not room 9's zone
        var cave = store.Join("c", Switches.At("TF_02", 5, 9));
        Assert.True(cave.State.IsEmpty);                     // same slot, another cave: nothing leaks
    }

    [Fact]
    public void Add_OnlyAtTheJoinedPlace()
    {
        var store = new RoomSwitchStore();
        Assert.Null(store.Add("a", Switches.At("kaze", 5, 9, 0xE0))); // never joined
        store.Join("a", Switches.At("kaze", 5, 9));
        Assert.Null(store.Add("a", Switches.At("kaze", 5, 8, 0xE0))); // another room: must rejoin
        var r = store.Add("a", Switches.At("kaze", 5, 9, 0xE0))!.Value;
        Assert.Equal([0xE0], Switches.Numbers(r.Added));
        Assert.True(store.Add("a", Switches.At("kaze", 5, 9, 0xE0))!.Value.Added.IsEmpty); // nothing new
    }

    [Fact]
    public void TheLastPlayerLeaving_DropsTheRoomAndThenTheStage()
    {
        var store = new RoomSwitchStore();
        store.Join("a", Switches.At("kaze", 5, 9, 0x81, 0xE0));
        store.Join("b", Switches.At("kaze", 5, 2));

        store.Join("a", Switches.At("kaze", 5, 3)); // a leaves room 9: nobody there any more
        Assert.Equal(0u, store.ZoneOf("kaze", 9)[1]);
        Assert.Equal(0b10u, store.DanOf(5, "kaze")[0]); // but the stage is still visited

        store.Leave("a");
        store.Leave("b");
        Assert.Equal(0u, store.DanOf(5, "kaze")[0]);    // a fresh visit starts clean
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var store = new RoomSwitchStore();
        store.Join("a", Switches.At("kaze", 5, 9, 0x81, 0xE0));
        store.Clear();
        Assert.Equal(0u, store.DanOf(5, "kaze")[0]);
        Assert.Null(store.Add("a", Switches.At("kaze", 5, 9, 0xE1)));
    }
}

/// <summary>The hub end to end: validation, relaying to the right players only.</summary>
[Collection("GameHub static state")]
public class RoomSwitchHubTests
{
    private static async Task<(WebApplication App, int Port)> StartAsync()
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

    private static async Task<RoomSwitches?> Next(TaskCompletionSource<RoomSwitches> tcs) =>
        await Task.WhenAny(tcs.Task, Task.Delay(3000)) == tcs.Task ? tcs.Task.Result : null;

    [Fact]
    public async Task Relays_ToTheSameRoom_DanOnlyToTheSlot_NothingElsewhere()
    {
        var (app, port) = await StartAsync();
        await using var _ = app;
        string stage = "T" + Random.Shared.Next(100000, 999999); // GameHub's store is static: a stage of our own

        await using var p1 = new SignalRClientService();
        await using var p2 = new SignalRClientService();
        await using var p3 = new SignalRClientService();
        await using var p4 = new SignalRClientService();
        foreach (var (c, n) in new[] { (p1, "P1"), (p2, "P2"), (p3, "P3"), (p4, "P4") })
            Assert.True((await c.ConnectAsync("127.0.0.1", port, n)).success);

        var got2 = new TaskCompletionSource<RoomSwitches>();
        var got3 = new TaskCompletionSource<RoomSwitches>();
        var got4 = new TaskCompletionSource<RoomSwitches>();
        p2.RoomSwitchesReceived += s => got2.TrySetResult(s);
        p3.RoomSwitchesReceived += s => got3.TrySetResult(s);
        p4.RoomSwitchesReceived += s => got4.TrySetResult(s);

        Assert.NotNull(await p1.JoinRoomSwitchesAsync(Switches.At(stage, 7, 4)));
        var p2State = await p2.JoinRoomSwitchesAsync(Switches.At(stage, 7, 4));
        Assert.NotNull(p2State);
        Assert.NotNull(await p3.JoinRoomSwitchesAsync(Switches.At(stage, 7, 5)));   // same slot, other room
        Assert.NotNull(await p4.JoinRoomSwitchesAsync(Switches.At(stage, 8, 4)));   // other slot

        Assert.True(await p1.SendRoomSwitchesAsync(Switches.At(stage, 7, 4, 0x82, 0xE0)));

        var r2 = await Next(got2);
        Assert.NotNull(r2);
        Assert.Equal([0x82, 0xE0], Switches.Numbers(r2!));
        var r3 = await Next(got3);
        Assert.NotNull(r3);
        Assert.Equal([0x82], Switches.Numbers(r3!));
        Assert.Null(await Next(got4));

        // A late joiner gets what the room holds.
        await using var p5 = new SignalRClientService();
        Assert.True((await p5.ConnectAsync("127.0.0.1", port, "P5")).success);
        var late = await p5.JoinRoomSwitchesAsync(Switches.At(stage, 7, 4));
        Assert.Equal([0x82, 0xE0], Switches.Numbers(late!));
    }

    [Fact]
    public async Task InvalidPayloads_AreRejected()
    {
        var (app, port) = await StartAsync();
        await using var _ = app;
        await using var p1 = new SignalRClientService();
        Assert.True((await p1.ConnectAsync("127.0.0.1", port, "P1")).success);

        Assert.Null(await p1.JoinRoomSwitchesAsync(Switches.At("bad name", 7, 4)));
        Assert.Null(await p1.JoinRoomSwitchesAsync(Switches.At("Tstage", 99, 4)));
        Assert.Null(await p1.JoinRoomSwitchesAsync(Switches.At("Tstage", 7, 64)));
        Assert.Null(await p1.Connection!.InvokeAsync<RoomSwitches?>(HubConstants.JoinRoomSwitches, (RoomSwitches?)null));
        Assert.Null(await p1.JoinRoomSwitchesAsync(new RoomSwitches { Stage = "Tstage", Dan = new uint[5] }));
    }
}

public class RoomSwitchTrackerTests
{
    [Fact]
    public void EchoGuard_RoomBitsAreNeverSentBack()
    {
        var t = new RoomSwitchTracker();
        var place = Switches.At("kaze", 5, 9);
        Assert.True(t.MoveTo(place));
        Assert.True(t.OnJoined(Switches.At("kaze", 5, 9), Switches.At("kaze", 5, 9, 0xE0)));

        var apply = t.ToApply(place);                  // the game doesn't have 0xE0 yet
        Assert.Equal([0xE0], Switches.Numbers(apply));
        t.MarkApplied(apply);

        var local = Switches.At("kaze", 5, 9, 0xE0);   // next tick: the game has it (we wrote it)
        t.Observe(local);
        Assert.True(t.Fresh(local).IsEmpty);           // not reported: it came from the room
    }

    [Fact]
    public void LocalBits_AreSentOnce()
    {
        var t = new RoomSwitchTracker();
        t.MoveTo(Switches.At("kaze", 5, 9));
        t.OnJoined(Switches.At("kaze", 5, 9), Switches.At("kaze", 5, 9));
        var local = Switches.At("kaze", 5, 9, 0xC3);
        var fresh = t.Fresh(local);
        Assert.Equal([0xC3], Switches.Numbers(fresh));
        t.MarkSent(fresh);
        Assert.True(t.Fresh(local).IsEmpty);

        t.Unsent(fresh);                               // the send failed: again next tick
        Assert.Equal([0xC3], Switches.Numbers(t.Fresh(local)));
    }

    [Fact]
    public void ARoomBit_IsAppliedOnce_AndNeverAfterTheGameClearedIt()
    {
        var t = new RoomSwitchTracker();
        var empty = Switches.At("kaze", 5, 9);
        t.MoveTo(empty);
        t.OnJoined(empty, Switches.At("kaze", 5, 9, 0x90));
        Assert.Equal([0x90], Switches.Numbers(t.ToApply(empty)));
        Assert.Equal([0x90], Switches.Numbers(t.ToApply(empty))); // the write didn't land: still to apply
        t.MarkApplied(t.ToApply(empty));
        Assert.True(t.ToApply(empty).IsEmpty);         // the game turned it off (a torch blown out): leave it

        // A bit this game set and cleared itself is not re-applied from the room either.
        t.Observe(Switches.At("kaze", 5, 9, 0x91));
        t.OnReceived(Switches.At("kaze", 5, 9, 0x91));
        Assert.True(t.ToApply(empty).IsEmpty);
    }

    [Fact]
    public void ANewRoom_KeepsTheSlotsDanBookkeeping_ANewSlotForgetsAll()
    {
        var t = new RoomSwitchTracker();
        var room9 = Switches.At("kaze", 5, 9);
        t.MoveTo(room9);
        t.OnJoined(room9, Switches.At("kaze", 5, 9, 0x90, 0xE0));
        t.MarkApplied(t.ToApply(room9));               // both applied here

        var room2 = Switches.At("kaze", 5, 2);
        Assert.True(t.MoveTo(room2));
        Assert.False(t.Joined);
        t.OnJoined(room2, Switches.At("kaze", 5, 2, 0x90, 0xE0)); // (zone 0xE0 of room 2, say)
        Assert.Equal([0xE0], Switches.Numbers(t.ToApply(room2))); // 0x90 was already applied in this slot

        var otherSlot = Switches.At("M_Dai", 6, 2);
        t.MoveTo(otherSlot);
        t.OnJoined(otherSlot, Switches.At("M_Dai", 6, 2, 0x90));
        Assert.Equal([0x90], Switches.Numbers(t.ToApply(otherSlot)));
    }

    [Fact]
    public void Received_ForAnotherPlace_OnlyDanOfTheSameSlotCounts()
    {
        var t = new RoomSwitchTracker();
        var here = Switches.At("kaze", 5, 9);
        t.MoveTo(here);
        t.OnJoined(here, here);
        Assert.False(t.OnReceived(Switches.At("sea", 1, 9, 0x90, 0xE0)));   // other slot
        Assert.False(t.OnReceived(Switches.At("TF_02", 5, 9, 0x91)));       // same slot, another stage
        Assert.True(t.OnReceived(Switches.At("kaze", 5, 2, 0x90, 0xE0)));   // same slot, other room: dan only
        Assert.Equal([0x90], Switches.Numbers(t.ToApply(here)));
    }

    [Fact]
    public void AJoinAnswer_ForAPlaceWeLeft_IsIgnored()
    {
        var t = new RoomSwitchTracker();
        t.MoveTo(Switches.At("kaze", 5, 9));
        t.MoveTo(Switches.At("kaze", 5, 2));
        Assert.False(t.OnJoined(Switches.At("kaze", 5, 9), Switches.At("kaze", 5, 9, 0xE0)));
        Assert.False(t.Joined);
    }
}

public class PushBlockSwitchesTests
{
    [Fact]
    public void Remove_ClearsOnlyTheExcludedSwitches()
    {
        var flags = new StageFlags { Slot = 3, Tbox = 1, Switch = [0xFFFFFFFF, 0xF, 0, 0] };
        PushBlockSwitches.Remove(flags, [0x80000001u, 0x2u, 0, 0]);
        Assert.Equal(0x7FFFFFFEu, flags.Switch[0]);
        Assert.Equal(0xDu, flags.Switch[1]);
        Assert.Equal(1u, flags.Tbox); // everything else untouched
    }
}

public class SwitchTableProviderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wwo-switch-table-" + Guid.NewGuid().ToString("N"));

    public SwitchTableProviderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void LoadsTheTableNextToTheFirstGameThatHasOne()
    {
        var missing = Path.Combine(_dir, "none");
        new Patcher.WorldData.SwitchTable
        {
            Rules = Patcher.WorldData.SwitchTableBuilder.RulesVersion, StageCount = 2, Dan = new() { ["kaze"] = [0x81] },
        }.Write(_dir);

        var provider = new SwitchTableProvider(() => [missing, _dir]);
        var table = provider.Table;
        Assert.NotNull(table);
        Assert.Equal(2, table!.StageCount);
        Assert.Equal(0b10u, table.DanWords("kaze")[0]);
    }

    [Fact]
    public void ATableFromOtherRules_IsNotUsed()
    {
        new Patcher.WorldData.SwitchTable { Rules = Patcher.WorldData.SwitchTableBuilder.RulesVersion + 1 }.Write(_dir);
        Assert.Null(new SwitchTableProvider(() => [_dir]).Table);
    }

    [Fact]
    public void NoTable_IsNull()
    {
        Assert.Null(new SwitchTableProvider(() => [_dir]).Table);
    }
}
