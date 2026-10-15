using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Native;
using WWOnline.Patcher.WorldData;
using WWOnline.Services;
using WWOnline.Tests.Services;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>
/// The world services between a Dolphin game and a native one through a real server, the native game's scene gate
/// faked open (<see cref="GateOpenNativeService"/>): chests and memory switches both ways, the native game's live
/// world batch, and live room switches (dan and zone) into the native save window.
/// </summary>
public class NativeWorldSyncTests
{
    private const string Stage = "M_NewD2";
    private const int Slot = 3, Room = 2, Zone = 1;
    private const uint StagInfo = 0x80500000;

    private static FakeDolphin DolphinGame()
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.Player.LinkActorPointer.Address, 0x80400000);
        d.Set(GameMemoryAddresses.Stage.CurrentStageName.Address, [.. System.Text.Encoding.ASCII.GetBytes(Stage), 0]);
        d.Set(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, Room);
        d.SetU32(GameMemoryAddresses.WorldFlags.StagInfoPtr, StagInfo);
        d.Set(StagInfo + GameMemoryAddresses.WorldFlags.StagSaveTblOffset, Slot << 1);
        d.Set(GameMemoryAddresses.WorldFlags.LiveDan + GameMemoryAddresses.WorldFlags.DanOffStageNo, Slot);
        d.Set(GameMemoryAddresses.WorldFlags.RoomZoneNo(Room), Zone);
        d.Set(GameMemoryAddresses.WorldFlags.Zone(Zone) + GameMemoryAddresses.WorldFlags.ZoneOffRoomNo, Room);
        return d;
    }

    private static FakeNativeGame NativeGame()
    {
        var game = new FakeNativeGame { SaveSlot = Slot };
        game.SetLocal(FakeNativeGame.Local(Stage, Room, 0, 0, 0));
        game.Zones[Room] = Zone;
        game.Save[0x79C] = Slot;              // mDan.mStageNo
        game.Save[0x7A8 + Zone * 0x4C] = Room; // mZone[Zone].mRoomNo
        return game;
    }

    private static async Task Until(Func<bool> done, int ms = 15000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(ms);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out");
            await Task.Delay(50);
        }
    }

    private static async Task<(SignalRClientService A, SignalRClientService B)> Connect(int port)
    {
        var a = new SignalRClientService();
        var b = new SignalRClientService();
        Assert.True((await a.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Dolphin"))).success);
        Assert.True((await b.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Native"))).success);
        return (a, b);
    }

    [Fact]
    public async Task Chests_AndSwitches_SyncBothWays_AndTheNativeGameGetsALiveBatch()
    {
        var (app, port) = await SharedBaitHubTests.StartAsync();
        await using var _ = app;
        var (hubA, hubB) = await Connect(port);
        await using var __ = hubA;
        await using var ___ = hubB;
        using var roomA = new RoomSettingsService(hubA);
        using var roomB = new RoomSettingsService(hubB);
        var tables = new SwitchTableProvider(() => []);

        var dolphin = DolphinGame();
        var game = NativeGame();
        using var native = new GateOpenNativeService(game, Stage);
        using var a = new WorldFlagSyncService(dolphin, hubA, roomA, new DespawnWorker(), new LiveWorldPoke(), tables);
        using var b = new WorldFlagSyncService(native, hubB, roomB, new DespawnWorker(), new LiveWorldPoke(), tables,
            new NativeWorldGame(game));
        a.Start();
        b.Start();

        // The Dolphin player opens chest 5: the native game's live copy gets the bit, then a live batch for it.
        dolphin.Set(GameMemoryAddresses.WorldFlags.LiveMemory + 3, 0x20);
        await Until(() => (game.Save[0x778 + 3] & 0x20) != 0);
        await Until(() => game.LiveBatch is { } batch && (batch.Bits[0] & 0x20) != 0);
        Assert.Equal(Slot, game.LiveBatch!.Value.Slot);

        // The native player sets memory switch 0x10: it reaches the Dolphin game's live copy.
        game.Save[0x778 + 4 + 1] |= 0x01; // mSwitch[0] (big-endian in the window), bit 16
        await Until(() => (dolphin.Get(GameMemoryAddresses.WorldFlags.LiveMemory + 4 + 1) & 0x01) != 0);
    }

    [Fact]
    public async Task RoomSwitches_ReachTheNativeGamesDanAndZone()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wwo-switches-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            new SwitchTable
            {
                Rules = SwitchTableBuilder.RulesVersion,
                Dan = { [Stage] = [0x81] },
                Zone = { [Stage] = new() { [Room] = [0xC3] } },
            }.Write(dir);
            var tables = new SwitchTableProvider(() => [dir]);

            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            var (hubA, hubB) = await Connect(port);
            await using var __ = hubA;
            await using var ___ = hubB;
            using var roomA = new RoomSettingsService(hubA);
            using var roomB = new RoomSettingsService(hubB);

            var dolphin = DolphinGame();
            var game = NativeGame();
            using var native = new GateOpenNativeService(game, Stage);
            using var a = new RoomSwitchSyncService(dolphin, hubA, roomA, tables, new LiveWorldPoke());
            using var b = new RoomSwitchSyncService(native, hubB, roomB, tables, new LiveWorldPoke(), new NativeWorldGame(game));
            a.Start();
            b.Start();

            // Dan switch 0x81 (mDan.mSwitch[0] bit 1) and zone switch 0xC3 (mZone[Zone].mSwitch[0] bit 3), both big-endian.
            dolphin.Set(GameMemoryAddresses.WorldFlags.LiveDan + GameMemoryAddresses.WorldFlags.DanOffSwitch + 3, 0x02);
            dolphin.Set(GameMemoryAddresses.WorldFlags.Zone(Zone) + GameMemoryAddresses.WorldFlags.ZoneOffSwitch + 1, 0x08);
            await Until(() => (game.Save[0x79C + 4 + 3] & 0x02) != 0 && (game.Save[0x7A8 + Zone * 0x4C + 2 + 1] & 0x08) != 0);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
