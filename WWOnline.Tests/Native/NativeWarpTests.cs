using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>"Warp to player" on the native backend: tww_gate_warp_local / tww_gate_change_stage and the gate state.</summary>
public class NativeWarpTests
{
    private readonly FakeNativeGame _game = new();
    private readonly NativeGameService _sut;

    public NativeWarpTests()
    {
        _sut = new NativeGameService(_game);
        var gate = FakeNativeGame.InPlay("M_NewD2", 6);
        gate.StartPoint = 4;
        gate.StartRoom = 2;
        gate.StartLayer = 3;
        _game.Gate = gate;
        _game.SetLocal(FakeNativeGame.Local("M_NewD2", 6, 10, 20, 30));
        _game.Save[3] = 12; // life
    }

    private static Task NoDelay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

    private static LocalWarpSnapshot Local(string stage = "M_NewD2", byte room = 6) =>
        new(stage, room, 1, WarpBusy.None, false, 0, false);

    [Fact]
    public void ReadStartStage_IsTheGatesEntry()
    {
        Assert.Equal(new StageEntry("M_NewD2", 4, 2, 3), WarpMemory.ReadStartStage(_sut));
        Assert.False(WarpMemory.IsStageChangePending(_sut));
    }

    [Fact]
    public void LocalWarpReader_BroadcastsTheEntranceAndBusy()
    {
        var info = new LocalWarpReader().Read(_sut, "M_NewD2")!;

        Assert.Equal((4, 2, 3, WarpBusy.None), (info.EntryPoint, (int)info.EntryRoom, (int)info.Layer, info.Busy));
    }

    [Fact]
    public void ReadBusy_ChecksLoadingEventDeathAndControl()
    {
        Assert.Equal(WarpBusy.None, WarpMemory.ReadBusy(_sut, minigame: false));
        Assert.Equal(WarpBusy.Minigame, WarpMemory.ReadBusy(_sut, minigame: true));
        _game.Gate.OtherCharacter = 1;
        Assert.Equal(WarpBusy.OtherCharacter, WarpMemory.ReadBusy(_sut, false));
        _game.Save[3] = 0;
        Assert.Equal(WarpBusy.Dead, WarpMemory.ReadBusy(_sut, false));
        _game.Gate.EventMode = 1;
        Assert.Equal(WarpBusy.Event, WarpMemory.ReadBusy(_sut, false));
        _game.Gate.NextStage = 1;
        Assert.Equal(WarpBusy.Loading, WarpMemory.ReadBusy(_sut, false));
        _game.Gate.Link = 0;
        Assert.Equal(WarpBusy.Loading, WarpMemory.ReadBusy(_sut, false));
    }

    [Fact]
    public void RequestStageChange_CallsChangeStage_OnceUntilTheChangeHappens()
    {
        Assert.True(WarpMemory.RequestStageChange(_sut, new StageEntry("sea", 1, 11, -1)));
        Assert.False(WarpMemory.RequestStageChange(_sut, new StageEntry("LinkRM", 0, 0, -1)));

        Assert.Equal([("sea", (short)1, (sbyte)11, (sbyte)-1)], _game.StageChanges);
        Assert.Equal(0, _game.SaveCalls); // no mRestart / mNextStage pokes: the game's own setNextStage does it
    }

    [Fact]
    public async Task Execute_LoadEntrance_ChangesStage()
    {
        var plan = new WarpPlan(WarpAction.LoadEntrance, "go", Entrance: new StageEntry("Omori", 0, 0, -1));

        var result = await WarpExecutor.ExecuteAsync(_sut, plan, Local(), _ => null, NoDelay);

        Assert.True(result.Ok);
        Assert.Equal("Omori", _game.StageChanges.Single().Stage);
    }

    [Fact]
    public async Task Execute_MoveInRoom_MovesLinkThroughTheGame()
    {
        var plan = new WarpPlan(WarpAction.MoveInRoom, "moved", Position: new Vector3(100, 200, 300), AngleY: 0x4000);

        var result = await WarpExecutor.ExecuteAsync(_sut, plan, Local(), _ => true, NoDelay);

        Assert.True(result.Ok);
        Assert.Equal([(100f, 200f, 300f, (short)0x4000)], _game.Warps);
    }

    [Fact]
    public async Task Execute_MoveInRoom_StopsWhenLinkChangedRoom()
    {
        var plan = new WarpPlan(WarpAction.MoveInRoom, "moved", Position: new Vector3(100, 200, 300));

        var result = await WarpExecutor.ExecuteAsync(_sut, plan, Local(room: 5), _ => true, NoDelay);

        Assert.False(result.Ok);
        Assert.Empty(_game.Warps);
    }

    [Fact]
    public void IsRidingShip_FromStatusOrProc()
    {
        Assert.True(WarpService.IsRidingShip(GameMemoryAddresses.Sea.PlayerStatus0ShipRide, 0));
        Assert.True(WarpService.IsRidingShip(0, GameMemoryAddresses.Sea.ProcShipFirst));
        Assert.False(WarpService.IsRidingShip(0, 4));
    }
}
