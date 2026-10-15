using WWOnline.Data;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>The phase-1 shim: the save window reaches tww_save_read / write with the GameCube's bytes; nothing else does.</summary>
public class NativeGameServiceTests
{
    private readonly FakeNativeGame _game = new();
    private readonly NativeGameService _sut;

    public NativeGameServiceTests() => _sut = new NativeGameService(_game);

    [Fact]
    public void ReadMemory_InTheSaveWindow_ReadsTheSaveAtItsOffset()
    {
        _game.Save[0x10] = 0xAB;
        _game.Save[0x11] = 0xCD;

        Assert.Equal(new byte[] { 0xAB, 0xCD }, _sut.ReadMemory(FakeNativeGame.SaveAddress(0x10), 2));
    }

    [Fact]
    public void TypedReads_AreBigEndian_LikeOnTheGameCube()
    {
        // dSv_player_status_a_c: max life (u16) then current life (u16) at the GameInfo base.
        _game.Save[0] = 0x00;
        _game.Save[1] = 0x50;
        _game.Save[2] = 0x12;
        _game.Save[3] = 0x34;

        Assert.Equal((ushort)0x50, _sut.Read(new MemoryAddress<ushort>(GameMemoryAddresses.GameInfo, "max")));
        Assert.Equal(0x00501234u, _sut.Read(new MemoryAddress<uint>(GameMemoryAddresses.GameInfo, "both")));
    }

    [Fact]
    public void Writes_InTheSaveWindow_LandInTheSave()
    {
        Assert.True(_sut.Write(new MemoryAddress<ushort>(FakeNativeGame.SaveAddress(0x20), "x"), (ushort)0xBEEF));
        Assert.True(_sut.WriteMemory(FakeNativeGame.SaveAddress(0x30), [1, 2, 3]));

        Assert.Equal(0xBE, _game.Save[0x20]);
        Assert.Equal(0xEF, _game.Save[0x21]);
        Assert.Equal(new byte[] { 1, 2, 3 }, _game.Save[0x30..0x33]);
    }

    [Fact]
    public void TheLastBytesOfTheWindow_AreMapped()
    {
        _game.Save[^1] = 0x77;
        Assert.Equal(new byte[] { 0x77 }, _sut.ReadMemory(FakeNativeGame.SaveAddress(FakeNativeGame.SaveSizeBytes - 1), 1));
    }

    [Theory]
    [InlineData(GameMemoryAddresses.Play + 0x48C0, 2)] // play: pending rupees
    [InlineData(GameMemoryAddresses.GameInfo - 1, 1)] // just below the window
    [InlineData(GameMemoryAddresses.GameInfo + FakeNativeGame.SaveSizeBytes - 1, 2)] // straddles its end
    [InlineData(PuppetLayout.STATUS_ADDR, 4)] // the Dolphin REL's scratch region
    public void AddressesOutsideTheSaveWindow_FailWithoutReachingTheGame(uint address, int size)
    {
        int before = _game.SaveCalls;

        Assert.Null(_sut.ReadMemory(address, size));
        Assert.False(_sut.WriteMemory(address, new byte[size]));

        Assert.Equal(before, _game.SaveCalls);
    }

    [Fact]
    public void TypedReadsOutsideTheSaveWindow_AreNull()
    {
        Assert.Null(_sut.Read(new MemoryAddress<uint>(GameMemoryAddresses.Stage.CurrentStageName.Address, "stage")));
        Assert.Null(_sut.ReadString(GameMemoryAddresses.Stage.CurrentStageName));
    }

    [Fact]
    public void ShortAddresses_AreGameCubeAddresses()
    {
        _game.Save[4] = 9;
        Assert.Equal(new byte[] { 9 }, _sut.ReadMemory(FakeNativeGame.SaveAddress(4) - 0x80000000, 1));
    }

    [Fact]
    public void WhileTheGameIsNotRunning_NothingIsConnectedOrRead()
    {
        bool? changed = null;
        _sut.ConnectionChanged += (_, connected) => changed = connected;

        _game.IsRunning = false;

        Assert.False(changed);
        Assert.False(_sut.IsConnected);
        Assert.Null(_sut.ReadMemory(FakeNativeGame.SaveAddress(0), 1));
        Assert.False(_sut.WriteMemory(FakeNativeGame.SaveAddress(0), [1]));
        Assert.Empty(_sut.EnumerateProcesses());
    }

    [Fact]
    public async Task Running_IsConnected()
    {
        Assert.True(_sut.IsConnected);
        Assert.True(await _sut.ConnectAsync());
        Assert.Null(_sut.ConnectedProcessId);
    }
}
