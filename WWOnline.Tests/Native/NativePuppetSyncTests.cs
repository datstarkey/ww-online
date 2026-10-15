using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>PuppetSyncService on the native backend: the local Link from the snapshot, peers through tww_set_peer.</summary>
public sealed class NativePuppetSyncTests : IDisposable
{
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), "wwo-native-puppets-" + Guid.NewGuid().ToString("N"));
    private readonly FakeNativeGame _game = new();
    private readonly PuppetSyncService _sut;

    public NativePuppetSyncTests()
    {
        _sut = new PuppetSyncService(new NativeGameService(_game), new GameSettingsService(_settingsDir), new DespawnWorker(),
            new LiveWorldPoke(), _game);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_settingsDir, recursive: true); } catch (IOException) { }
    }

    private static PuppetData Peer(string stage, byte room, float x = 0, float z = 0) => new()
    {
        Position = new Vector3(x, 0, z),
        Rotation = 0x1000,
        Action = new ActionState { SpeedF = 4f, CurProc = 0x06 },
        StageName = stage,
        RoomNumber = room,
    };

    private void WaitForTick()
    {
        int calls = Volatile.Read(ref _game.SetPeerCalls);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref _game.SetPeerCalls) >= calls + TwwNative.MaxPeers * 2,
            TimeSpan.FromSeconds(5)), "no puppet ticks");
    }

    [Fact]
    public void ReadLocalPlayerState_ComesFromTheSnapshot_WithTheLocalLook()
    {
        _game.SetLocal(FakeNativeGame.Local("M_NewD2", 2, 100, 0, -50, angleY: 0x2000, speed: 9f, proc: 0x06));
        _sut.LocalAppearance = new AppearanceState { ClothesType = 1, ColorR = 10, ColorG = 20, ColorB = 30 };

        var data = _sut.ReadLocalPlayerState()!;

        Assert.Equal(new Vector3(100, 0, -50), data.Position);
        Assert.Equal(0x2000, data.Rotation);
        Assert.Equal("M_NewD2", data.StageName);
        Assert.Equal(2, data.RoomNumber);
        Assert.Equal(0x06, data.Action.CurProc);
        Assert.Equal(1, data.Appearance.ClothesType);
        Assert.Equal(30, data.Appearance.ColorB);
    }

    [Fact]
    public void ReadLocalPlayerState_CarriesTheWarpEntrance()
    {
        var gate = FakeNativeGame.InPlay("A_mori", 0);
        gate.StartPoint = 3;
        _game.Gate = gate;
        _game.Save[3] = 12; // life
        _game.SetLocal(FakeNativeGame.Local("A_mori", 0, 0, 0, 0));

        var warp = _sut.ReadLocalPlayerState()!.Warp!;

        Assert.Equal((3, 0, -1, WarpBusy.None), (warp.EntryPoint, (int)warp.EntryRoom, (int)warp.Layer, warp.Busy));
        Assert.Same(warp, _sut.LocalWarp);
    }

    [Fact]
    public void ReadLocalPlayerState_CountsProcChanges()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 0, 0, 0, 0, proc: 0x04));
        byte first = _sut.ReadLocalPlayerState()!.Action.ProcSeq;
        byte same = _sut.ReadLocalPlayerState()!.Action.ProcSeq;
        _game.SetLocal(FakeNativeGame.Local("sea", 0, 0, 0, 0, proc: 0x06));
        byte next = _sut.ReadLocalPlayerState()!.Action.ProcSeq;

        Assert.Equal(first, same);
        Assert.Equal((byte)(first + 1), next);
    }

    [Fact]
    public void ReadLocalPlayerState_NoPlayer_IsNull()
    {
        _game.SetLocal(default);
        Assert.Null(_sut.ReadLocalPlayerState());
    }

    [Fact]
    public void PeersInTheSameStageAndRoom_AreSet_OthersAreNull()
    {
        _game.SetLocal(FakeNativeGame.Local("M_NewD2", 2, 0, 0, 0));
        _sut.UpdateRemotePuppet("here", Peer("M_NewD2", 2, x: 50));
        _sut.UpdateRemotePuppet("elsewhere", Peer("M_NewD2", 5));
        _sut.TryGetSlot("here", out int here);
        _sut.TryGetSlot("elsewhere", out int elsewhere);

        _sut.Start();
        WaitForTick();

        var peer = _game.Peers[here]!.Value;
        Assert.Equal(50f, peer.Pos[0]);
        Assert.Equal("M_NewD2", peer.Stage.ToString());
        Assert.Equal(0x06u, peer.Proc);
        Assert.Null(_game.Peers[elsewhere]);
    }

    [Fact]
    public void WhileTheLocalGameIsNotIdle_NoPeerIsShown()
    {
        _game.SetLocal(FakeNativeGame.Local("M_NewD2", 2, 0, 0, 0, flags: TwwLocationFlags.None));
        _sut.UpdateRemotePuppet("here", Peer("M_NewD2", 2));

        _sut.Start();
        WaitForTick();

        Assert.All(_game.Peers, p => Assert.Null(p));
    }

    [Fact]
    public void ReleaseSlot_ClearsThatPeer_WipeAll_ClearsEveryPeer()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 0, 0, 0, 0));
        _sut.UpdateRemotePuppet("a", Peer("sea", 0));
        _sut.TryGetSlot("a", out int slot);
        _game.Peers[slot] = NativePuppets.ToPeer(Peer("sea", 0));
        _game.Peers[(slot + 1) % TwwNative.MaxPeers] = NativePuppets.ToPeer(Peer("sea", 0));

        _sut.ReleaseSlot("a");
        Assert.Null(_game.Peers[slot]);
        Assert.NotNull(_game.Peers[(slot + 1) % TwwNative.MaxPeers]);

        _sut.WipeAll();
        Assert.All(_game.Peers, p => Assert.Null(p));
    }
}
