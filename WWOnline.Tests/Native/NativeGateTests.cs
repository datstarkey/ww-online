using System.Runtime.CompilerServices;
using WWOnline.Data;
using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using WWOnline.Tests.Services;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>The scene gate on both backends: tww_gate_get_state natively, play memory on Dolphin.</summary>
public class NativeGateTests
{
    private readonly FakeNativeGame _game = new();
    private readonly NativeGameService _sut;

    public NativeGateTests() => _sut = new NativeGameService(_game);

    private static int Offset<TStruct, TField>(ref TStruct s, ref TField field) =>
        (int)Unsafe.ByteOffset(ref Unsafe.As<TStruct, byte>(ref s), ref Unsafe.As<TField, byte>(ref field));

    [Fact]
    public void TwwGateState_Is28Bytes_WithTheHeadersOffsets()
    {
        var s = new TwwGateState();
        Assert.Equal(28, Unsafe.SizeOf<TwwGateState>());
        Assert.Equal(0, Offset(ref s, ref s.Stage));
        Assert.Equal(8, Offset(ref s, ref s.StartPoint));
        Assert.Equal(10, Offset(ref s, ref s.StartRoom));
        Assert.Equal(11, Offset(ref s, ref s.StartLayer));
        Assert.Equal(12, Offset(ref s, ref s.Room));
        Assert.Equal(13, Offset(ref s, ref s.Link));
        Assert.Equal(14, Offset(ref s, ref s.NextStage));
        Assert.Equal(15, Offset(ref s, ref s.EventMode));
        Assert.Equal(16, Offset(ref s, ref s.MenuPaused));
        Assert.Equal(17, Offset(ref s, ref s.MiniGame));
        Assert.Equal(18, Offset(ref s, ref s.OtherCharacter));
        Assert.Equal(20, Offset(ref s, ref s.PlayerStatus0));
        Assert.Equal(24, Offset(ref s, ref s.LinkModeFlg));
    }

    [Fact]
    public void ReadScene_Native_MapsTheGateState()
    {
        var gate = FakeNativeGame.InPlay("M_NewD2", 3);
        gate.StartPoint = 5;
        gate.StartRoom = 2;
        gate.StartLayer = 1;
        gate.MiniGame = 4;
        gate.OtherCharacter = 1;
        gate.PlayerStatus0 = 0x00010000;
        gate.LinkModeFlg = 0x20;
        _game.Gate = gate;
        _game.SetLocal(FakeNativeGame.Local("M_NewD2", 3, 1, 2, 3, proc: 0x86));

        var scene = _sut.ReadScene()!;

        Assert.True(scene.LinkPresent);
        Assert.Equal(new StageEntry("M_NewD2", 5, 2, 1), scene.StartEntry);
        Assert.Equal("M_NewD2", scene.Stage);
        Assert.Equal(3, scene.Room);
        Assert.Equal(4, scene.MiniGameType);
        Assert.True(scene.OtherCharacter);
        Assert.Equal(0x00010000u, scene.PlayerStatus0);
        Assert.Equal(0x20u, scene.LinkModeFlg);
        Assert.Equal(0x86u, scene.LinkProc);
        Assert.True(scene.IsIdle);
    }

    [Fact]
    public void Gate_Native_PassesAfterTheStageHoldsStill_WithoutTouchingTheSave()
    {
        _game.Gate = FakeNativeGame.InPlay("sea", 44);
        var gate = new SceneStabilityGate(2);

        Assert.False(gate.Check(_sut)); // first sight of the stage
        Assert.False(gate.Check(_sut));
        Assert.True(gate.Check(_sut));
        Assert.Equal("sea", gate.Stage);
        Assert.Equal(0, _game.SaveCalls);
    }

    [Theory]
    [InlineData("sea", 0, 0)]     // no Link
    [InlineData("sea", 1, 1)]     // a stage change is pending
    [InlineData("sea_T", 1, 0)]   // the title demo
    [InlineData("", 1, 0)]
    public void Gate_Native_HoldsOutsidePlay(string stage, byte link, byte nextStage)
    {
        var state = FakeNativeGame.InPlay(stage, 1);
        state.Link = link;
        state.NextStage = nextStage;
        _game.Gate = state;
        var gate = new SceneStabilityGate(0);

        Assert.False(gate.Check(_sut));
        Assert.False(gate.Check(_sut));
    }

    [Fact]
    public void Gate_Native_IsClosedWhileTheGameIsntRunning()
    {
        _game.Gate = FakeNativeGame.InPlay("sea", 44);
        _game.IsRunning = false;

        Assert.Null(_sut.ReadScene());
        Assert.False(new SceneStabilityGate(0).Check(_sut));
        Assert.False(SceneStabilityGate.IsIdle(_sut));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(2, 0, false)] // an event
    [InlineData(0, 1, false)] // the pause menu
    public void IsIdle_Native_IsNoEventAndNoMenu(byte eventMode, byte menu, bool idle)
    {
        var state = FakeNativeGame.InPlay("sea", 44);
        state.EventMode = eventMode;
        state.MenuPaused = menu;
        _game.Gate = state;

        Assert.Equal(idle, SceneStabilityGate.IsIdle(_sut));
    }

    [Fact]
    public async Task ReadGameState_Native_TakesTheSceneFromTheGate()
    {
        var gate = FakeNativeGame.InPlay("sea", 11);
        gate.PlayerStatus0 = 0xABCD1234;
        _game.Gate = gate;
        _game.SetLocal(FakeNativeGame.Local("sea", 11, 10.5f, 20f, -30f));
        _game.Save[3] = 12; // life

        var state = (await _sut.ReadGameStateAsync())!;

        Assert.Equal("sea", state.StageName);
        Assert.Equal((10.5f, 20f, -30f), (state.Player.PositionX, state.Player.PositionY, state.Player.PositionZ));
        Assert.Equal(0xABu, state.Player.StatusFlags1);
        Assert.Equal(0xCDu, state.Player.StatusFlags2);
        Assert.Equal(11, state.Player.CurrentSector);
        Assert.Equal(12, state.Player.CurrentHealth);
    }

    [Fact]
    public void ReadScene_Dolphin_ReadsPlayMemory()
    {
        var d = new FakeDolphin();
        d.SetU32(GameMemoryAddresses.Player.LinkActorPointer.Address, 0x80400000);
        d.Set(GameMemoryAddresses.Warp.StartStage, "kaze"u8.ToArray());
        d.Set(GameMemoryAddresses.Warp.StartStage + 8, 0x00, 0x0F, 0x0F, 0x02); // point 15, room 15, layer 2
        d.Set(GameMemoryAddresses.Warp.NextStage + GameMemoryAddresses.Warp.NextStageOffEnable, 1);
        d.SetU32(GameMemoryAddresses.Warp.ControlledActorPtr, 0x80500000);
        d.SetU32(GameMemoryAddresses.Warp.LinkActorPtr, 0x80400000);
        d.SetU32(0x80400000 + PuppetLayout.DAPY_OFF_MODE_FLG, 0x200);
        d.SetU32(0x80400000 + PuppetLayout.DAPY_OFF_CUR_PROC, 0x86);
        d.Set(GameMemoryAddresses.Events.EventMode, 3);
        d.Set(GameMemoryAddresses.Events.MenuPause, 1);
        d.Set(GameMemoryAddresses.WorldFlags.MiniGameType, 2);
        d.Set(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, 7);
        d.SetU32(GameMemoryAddresses.Player.PlayerStatusBitfield0.Address, 0x00010000);

        var scene = d.ReadScene()!;

        Assert.Equal(new GameScene(true, 0x80400000, new StageEntry("kaze", 15, 15, 2), 7, true, 3, true, 2, true,
            0x00010000, 0x200, 0x86), scene);
        Assert.False(scene.IsIdle);
    }
}
