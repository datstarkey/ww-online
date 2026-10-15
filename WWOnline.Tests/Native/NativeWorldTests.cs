using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Patcher.WorldData;
using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.Tests.Services;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>tww.h "world" bound exactly, and <see cref="NativeWorldGame"/>: the world sync's reads and writes outside the save window.</summary>
public class NativeWorldTests
{
    private readonly FakeNativeGame _game = new();
    private readonly NativeWorldGame _sut;

    public NativeWorldTests() => _sut = new NativeWorldGame(_game);

    private static int Offset<TStruct, TField>(ref TStruct s, ref TField field) =>
        (int)Unsafe.ByteOffset(ref Unsafe.As<TStruct, byte>(ref s), ref Unsafe.As<TField, byte>(ref field));

    [Fact]
    public void TheWorldStructs_MatchTheHeader()
    {
        Assert.Equal(4, Unsafe.SizeOf<TwwWorldSalvagePoint>());
        var batch = new TwwWorldLiveBatch();
        Assert.Equal(48, Unsafe.SizeOf<TwwWorldLiveBatch>());
        Assert.Equal(4, Offset(ref batch, ref batch.Slot));
        Assert.Equal(8, Offset(ref batch, ref batch.ZoneRoom));
        Assert.Equal(12, Offset(ref batch, ref batch.Bits));
        var status = new TwwWorldLiveStatus();
        Assert.Equal(16, Unsafe.SizeOf<TwwWorldLiveStatus>());
        Assert.Equal(12, Offset(ref status, ref status.Retry));
        Assert.Equal(8, Unsafe.SizeOf<TwwWorldStats>());
        Assert.Equal(LiveWorldPoke.Words, TwwNative.WorldLiveWords);
        Assert.Equal(GameMemoryAddresses.Salvage.InfoCount, TwwNative.WorldSalvageMax);
    }

    [Fact]
    public void OnlyTheNativeBackend_ServesIWorldGame()
    {
        var native = new ServiceCollection();
        App.AddGameBackend(native, GameBackend.Native);
        using (var provider = native.BuildServiceProvider())
            Assert.IsType<NativeWorldGame>(provider.GetRequiredService<IWorldGame>());

        var dolphin = new ServiceCollection();
        App.AddGameBackend(dolphin, GameBackend.Dolphin);
        using (var provider = dolphin.BuildServiceProvider())
            Assert.Null(provider.GetService<IWorldGame>()); // the services fall back to DolphinWorldGame
    }

    [Fact]
    public void ThePlace_IsTheLocationWithTheSaveSlot()
    {
        _game.SetLocal(FakeNativeGame.Local("M_NewD2", 2, 0, 0, 0));
        Assert.Null(_sut.ReadPlace()); // no slot: no stage info or a stage change pending
        Assert.Null(_sut.CurrentSlot());

        _game.SaveSlot = 3;
        var place = _sut.ReadPlace()!;
        Assert.Equal(("M_NewD2", 3, 2), (place.Stage, place.Slot, place.Room));
        Assert.True(place.IsEmpty);
        Assert.Equal(3, _sut.CurrentSlot());

        _game.SetLocal(FakeNativeGame.Local("M_NewD2", -1, 0, 0, 0)); // no stay room
        Assert.Null(_sut.ReadPlace());
    }

    [Fact]
    public void RoomZones_ComeFromTheRoomStatus()
    {
        _game.Zones[5] = 7;
        Assert.Equal(7, _sut.RoomZoneNo(5));
        Assert.Equal(-1, _sut.RoomZoneNo(6));
    }

    [Fact]
    public void NothingIsRead_WhileTheGameIsNotRunning()
    {
        _game.SaveSlot = 3;
        _game.SetLocal(FakeNativeGame.Local("sea", 44, 0, 0, 0));
        _game.IsRunning = false;
        Assert.Null(_sut.CurrentSlot());
        Assert.Null(_sut.ReadPlace());
        Assert.Null(_sut.RoomZoneNo(44));
        Assert.Null(_sut.ReadSalvagePoints());
        Assert.Null(_sut.ReadDespawns());
        Assert.Null(_sut.ReadLiveWorld());
    }

    [Fact]
    public void APeersSalvage_MarksTheNativePointsEmptied()
    {
        Assert.Null(_sut.ReadSalvagePoints()); // no salvage actor (no sea since boot)
        _game.Salvage =
        [
            new() { Room = 11, SaveNo = 3, Kind = 2 },
            new() { Room = 12, SaveNo = 3, Kind = 2 },
            new() { Room = -1, SaveNo = 3, Kind = 2 },
        ];
        var bits = new StageFlags { Slot = StageFlags.SeaSlot };
        bits.Ocean[11] = 1 << 3;

        var points = _sut.ReadSalvagePoints()!;
        Assert.Equal(new SalvagePoint(11, 3, 2, 0), points[0]);
        foreach (int i in WorldFlagSyncService.SalvagedPoints(points, bits))
            Assert.True(_sut.OrSalvageFlag(i, GameMemoryAddresses.Salvage.FlagDone));

        Assert.Equal(new byte[] { 1, 0, 0 }, _game.Salvage.Select(p => p.Flag));
    }

    [Fact]
    public void RemoteItems_AndDespawns_GoThroughTheApi()
    {
        _sut.SetRemoteItems(4, 0x30);
        Assert.Equal((4, 0x30u), _game.RemoteItems);
        _game.Stats = new TwwWorldStats { DespawnCount = 2, LastDespawn = 0x00040105 };
        Assert.Equal((2u, 0x00040105u), _sut.ReadDespawns());
    }

    [Fact]
    public void LiveWorld_HandsTheGameBitsOthersSet_AndCollectsItsAcknowledgement()
    {
        var poke = new LiveWorldPoke();
        poke.SetStage(3);
        poke.AddRemote(3, LiveWorldPoke.FromMemBit(1u << 5, [0, 0x10, 0, 0]));
        poke.SetLive(0, LiveWorldPoke.FromMemBit(1u << 5, [0, 0x10, 0, 0]));

        var first = poke.Tick(_sut)!.Value;
        Assert.NotNull(first.Published);
        var batch = _game.LiveBatch!.Value;
        Assert.Equal((first.Published!.Seq, 3, LiveWorldPoke.ZoneRoomNone), (batch.Seq, batch.Slot, batch.ZoneRoom));
        Assert.Equal(1u << 5, batch.Bits[0]);
        Assert.Equal(0x10u, batch.Bits[2]);

        Assert.Null(poke.Tick(_sut)!.Value.Published); // the game hasn't got to it yet

        _game.LiveHandled(pokes: 2);
        var done = poke.Tick(_sut)!.Value;
        Assert.Equal(first.Published, done.Acknowledged);
        Assert.Equal(2u, done.PokeCount);
        Assert.Null(done.Published); // nothing new: each bit is handled once
    }

    [Fact]
    public void LiveWorld_PostsTheBatchAgain_WhenTheGameLeftActorsUndone()
    {
        var poke = new LiveWorldPoke();
        poke.SetStage(3);
        poke.AddRemote(3, LiveWorldPoke.FromMemBit(1, []));
        poke.SetLive(0, LiveWorldPoke.FromMemBit(1, []));
        var first = poke.Tick(_sut)!.Value.Published!;

        _game.LiveHandled(pokes: 0, retry: 1);
        var again = poke.Tick(_sut)!.Value;
        Assert.Equal(first, again.Acknowledged);
        Assert.NotNull(again.Published);
        Assert.True(again.Published!.Seq > first.Seq);
        Assert.Equal(1u, _game.LiveBatch!.Value.Bits[0]);
    }

    [Fact]
    public void NoDisc_NoSwitchTable()
    {
        Assert.Null(new SwitchTableProvider(() => [], () => null).Table);
        Assert.Null(new SwitchTableProvider(() => [], () => "/nonexistent/GZLE01.iso").Table);
    }

    [DiscFact]
    public void TheNativeBackends_SwitchTable_IsBuiltFromTheDisc()
    {
        var table = new SwitchTableProvider(() => [], () => Environment.GetEnvironmentVariable(NativePaths.DiscVariable)).Table;
        Assert.NotNull(table);
        Assert.True(table.StageCount > 100);
        Assert.NotEmpty(table.Dan);
        Assert.NotEmpty(table.Zone);
    }

    [DiscFact]
    public void TheNativeBackends_HeartAndKeyTables_AreBuiltFromTheDisc()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wwo-tables-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settings = new GameSettingsService(dir);
            var disc = Environment.GetEnvironmentVariable(NativePaths.DiscVariable)!;
            settings.Save(new GameSettings { Backend = GameBackend.Native, DiscImagePath = disc });
            Assert.Equal(disc, StageTableProvider<HeartTable>.DefaultGamePaths(settings).First());

            var hearts = new HeartTableProvider(settings).Build();
            Assert.Equal((HeartTable.VanillaPieces, HeartTable.VanillaContainers), (hearts!.PiecesTotal, hearts.ContainersTotal));
            var keys = new SmallKeyTableProvider(settings).Build();
            Assert.Equal(4, keys!.For(3)!.Doors.Count); // Dragon Roost Cavern
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    // Story: the event bits are in the save window; only the scene gate is outside it (mp-gate).

    [Fact]
    public void StoryBits_LandInTheNativeSave_WithTheGateOpen()
    {
        using var dolphin = new GateOpenNativeService(_game, "sea");
        var bits = new StoryFlags();
        bits.Bits[0x0F] = 0x80; // MET_KORL (0x0F80)
        StorySyncService.ApplyBits(dolphin, bits);

        Assert.Equal(0x80, _game.Save[0x624 + 0x0F]);
        Assert.Equal(GameMemoryAddresses.GameInfo + 0x624, EventFlagCatalog.EventBitfieldAddress);
        Assert.True(0x624 + EventFlagCatalog.EventBitfieldSize <= FakeNativeGame.SaveSizeBytes);
    }

    private sealed class Room : IStoryRoom
    {
        public bool IsConnected => true;
        public bool SharedStory => true;
        public bool IsOwner => true;
        public StoryFlags? RoomFlags => null;
        public event Action? Changed { add { } remove { } }
    }

    [Fact]
    public void TheRoomFlagsPage_ReadsAndEditsTheNativeSave_WithTheGateOpen()
    {
        using var dolphin = new GateOpenNativeService(_game, "sea");
        _game.Save[0x624 + 0x2A] = 0x08; // RODE_KORL, set by the game
        using var vm = new RoomFlagsViewModel(dolphin, new Room(), a => a(), startPolling: false);
        for (int i = 0; i < 3; i++) vm.Poll(); // the gate's stable ticks

        Assert.True(vm.AllRows.Single(r => r.Info.Id == 0x2A08).IsSet);
        vm.StartEditCommand.Execute(null);
        vm.ToggleFlagCommand.Execute(vm.AllRows.Single(r => r.Info.Id == 0x0F80));
        Assert.Equal(0x80, _game.Save[0x624 + 0x0F]);
    }
}

/// <summary>A [Fact] reported as skipped unless WWO_DISC names the disc image (Nintendo files aren't in the repo).</summary>
internal sealed class DiscFactAttribute : FactAttribute
{
    public DiscFactAttribute()
    {
        if (!File.Exists(Environment.GetEnvironmentVariable(NativePaths.DiscVariable)))
            Skip = $"No disc image: set {NativePaths.DiscVariable}";
    }
}
