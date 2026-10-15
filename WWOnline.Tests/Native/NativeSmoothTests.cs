using WWOnline.Native;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Native;

/// <summary>Timed peer states: the local puppet stamped with its frame's time, the peers' times passed to the game.</summary>
public sealed class NativeSmoothTests : IDisposable
{
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), "wwo-native-smooth-" + Guid.NewGuid().ToString("N"));
    private readonly FakeNativeGame _game = new();
    private readonly PuppetSyncService _sut;

    public NativeSmoothTests()
    {
        _sut = new PuppetSyncService(new NativeGameService(_game), new GameSettingsService(_settingsDir), new DespawnWorker(),
            new LiveWorldPoke(), _game);
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_settingsDir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void FrameClock_StepsAThirtiethOfASecondAFrame()
    {
        var start = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var t = GameHost.FrameClock(start, 30, start.AddSeconds(1));

        Assert.Equal(start.AddSeconds(1), t);
    }

    [Fact]
    public void FrameClock_EasesTowardTheWallClock_AndJumpsWhenFarOff()
    {
        var start = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        var late = start.AddSeconds(1).AddMilliseconds(10);

        Assert.Equal(start.AddSeconds(1).AddMilliseconds(0.1), GameHost.FrameClock(start, 30, late));
        Assert.Equal(start.AddSeconds(3), GameHost.FrameClock(start, 1, start.AddSeconds(3)));
        Assert.Equal(start, GameHost.FrameClock(default, 1, start));
    }

    [Fact]
    public void SentAt_IsSecondsSinceTheUnixEpoch_ZeroWhenUnset()
    {
        Assert.Equal(1.5, TwwNative.SentAt(DateTime.UnixEpoch.AddSeconds(1.5)));
        Assert.Equal(0, TwwNative.SentAt(default));
    }

    [Fact]
    public void ReadLocalPlayerState_IsStampedWithTheFramesTime()
    {
        var time = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        _game.Snapshot = new NativeSnapshot(7, FakeNativeGame.Local("sea", 0, 0, 0, 0), default, true) { Time = time };

        Assert.Equal(time, _sut.ReadLocalPlayerState()!.Timestamp);
    }

    [Fact]
    public void EachPeersSendTime_GoesToTheGameBeforeItsState()
    {
        _game.SetLocal(FakeNativeGame.Local("sea", 0, 0, 0, 0));
        var sent = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        _sut.UpdateRemotePuppet("a", new PuppetData
        {
            Position = new Vector3(10, 0, 0),
            StageName = "sea",
            Timestamp = sent,
        });
        _sut.TryGetSlot("a", out int slot);

        _sut.Start();
        Assert.True(SpinWait.SpinUntil(() => _game.PeerTimes[slot] != 0, TimeSpan.FromSeconds(5)), "no send time");

        Assert.Equal(TwwNative.SentAt(sent), _game.PeerTimes[slot]);
        Assert.Equal(0, _game.PeerTimes[(slot + 1) % TwwNative.MaxPeers]);
    }
}
