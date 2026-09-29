using Moq;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Tests for PuppetSyncService — slot management, lifecycle methods, and thread safety.
/// Uses a disconnected Dolphin stub so no actual memory writes occur.
/// </summary>
public class PuppetSyncServiceTests : IDisposable
{
    private readonly Mock<IDolphinService> _dolphin;
    private readonly GameSettingsService _settings;
    private readonly PuppetSyncService _sut;
    // A temp folder, never the real %AppData%\WWOnline settings.
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), "wwo-puppetsync-" + Guid.NewGuid().ToString("N"));

    public PuppetSyncServiceTests()
    {
        _dolphin = new Mock<IDolphinService>();
        _dolphin.Setup(d => d.IsConnected).Returns(false);
        _dolphin.Setup(d => d.WriteMemory(It.IsAny<uint>(), It.IsAny<byte[]>())).Returns(true);
        _settings = new GameSettingsService(_settingsDir);
        _sut = new PuppetSyncService(_dolphin.Object, _settings, new DespawnWorker());
    }

    public void Dispose()
    {
        _sut.Dispose();
        try { Directory.Delete(_settingsDir, recursive: true); } catch (IOException) { }
    }

    // ── Slot assignment ────────────────────────────────────────────────────────

    [Fact]
    public void AssignSlot_FirstPlayer_ReturnsSlotZero()
    {
        int slot = _sut.AssignSlot("player-1");
        Assert.Equal(0, slot);
    }

    [Fact]
    public void AssignSlot_SamePlayerTwice_ReturnsSameSlot()
    {
        int first = _sut.AssignSlot("player-1");
        int second = _sut.AssignSlot("player-1");
        Assert.Equal(first, second);
    }

    [Fact]
    public void AssignSlot_MultiplePlayers_AssignsDistinctSlots()
    {
        int s0 = _sut.AssignSlot("player-A");
        int s1 = _sut.AssignSlot("player-B");
        int s2 = _sut.AssignSlot("player-C");

        // All within valid range
        Assert.InRange(s0, 0, GameMemoryAddresses.PuppetSync.MaxSlots - 1);
        Assert.InRange(s1, 0, GameMemoryAddresses.PuppetSync.MaxSlots - 1);
        Assert.InRange(s2, 0, GameMemoryAddresses.PuppetSync.MaxSlots - 1);

        // All distinct
        Assert.NotEqual(s0, s1);
        Assert.NotEqual(s1, s2);
        Assert.NotEqual(s0, s2);
    }

    [Fact]
    public void AssignSlot_WhenAllSlotsFull_ReturnsNegativeOne()
    {
        // Fill all slots (MaxSlots = 3)
        for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
            _sut.AssignSlot($"player-{i}");

        int overflow = _sut.AssignSlot("player-overflow");

        Assert.Equal(-1, overflow);
    }

    [Fact]
    public void ActiveSlotCount_ReflectsAssignments()
    {
        Assert.Equal(0, _sut.ActiveSlotCount);
        _sut.AssignSlot("p1");
        Assert.Equal(1, _sut.ActiveSlotCount);
        _sut.AssignSlot("p2");
        Assert.Equal(2, _sut.ActiveSlotCount);
    }

    // ── Slot release ───────────────────────────────────────────────────────────

    [Fact]
    public void ReleaseSlot_KnownPlayer_DecrementsCount()
    {
        _sut.AssignSlot("player-1");
        _sut.ReleaseSlot("player-1");

        Assert.Equal(0, _sut.ActiveSlotCount);
    }

    [Fact]
    public void ReleaseSlot_FreesSlotForReuse()
    {
        int s0 = _sut.AssignSlot("player-A");
        _sut.AssignSlot("player-B");
        _sut.AssignSlot("player-C");

        _sut.ReleaseSlot("player-A");

        int reused = _sut.AssignSlot("player-new");
        Assert.Equal(s0, reused);
    }

    [Fact]
    public void ReleaseSlot_UnknownPlayer_DoesNotThrow()
    {
        var ex = Record.Exception(() => _sut.ReleaseSlot("ghost-player"));
        Assert.Null(ex);
    }

    [Fact]
    public void ReleaseSlot_WhenDolphinConnected_WritesZeroToSlotMemory()
    {
        _dolphin.Setup(d => d.IsConnected).Returns(true);
        int slot = _sut.AssignSlot("player-1");

        _sut.ReleaseSlot("player-1");

        // Zeroes the slot body (active = 0 deactivates it), the slot's boat block and its cannon / crane words
        _dolphin.Verify(
            d => d.WriteMemory(GameMemoryAddresses.PuppetSync.GetSlotBase(slot), It.Is<byte[]>(b => b.All(x => x == 0))),
            Times.Once);
        _dolphin.Verify(
            d => d.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatBase(slot), It.Is<byte[]>(b => b.All(x => x == 0))),
            Times.Once);
        _dolphin.Verify(
            d => d.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatCannonBase(slot),
                It.Is<byte[]>(b => b.Length == GameMemoryAddresses.PuppetSync.BoatCannonSize && b.All(x => x == 0))),
            Times.Once);
        _dolphin.Verify(
            d => d.WriteMemory(GameMemoryAddresses.PuppetSync.GetBoatCraneBase(slot),
                It.Is<byte[]>(b => b.Length == GameMemoryAddresses.PuppetSync.BoatCraneSize && b.All(x => x == 0))),
            Times.Once);
    }

    // ── UpdateRemotePuppet ─────────────────────────────────────────────────────

    [Fact]
    public void UpdateRemotePuppet_AutoAssignsSlot()
    {
        _sut.UpdateRemotePuppet("player-1", MakePuppet());

        Assert.Equal(1, _sut.ActiveSlotCount);
    }

    [Fact]
    public void UpdateRemotePuppet_WhenSlotsFull_DropsExtraPlayer()
    {
        for (int i = 0; i < GameMemoryAddresses.PuppetSync.MaxSlots; i++)
            _sut.UpdateRemotePuppet($"player-{i}", MakePuppet());

        // 4th player — no slot available, should be silently dropped
        _sut.UpdateRemotePuppet("overflow-player", MakePuppet());

        Assert.Equal(GameMemoryAddresses.PuppetSync.MaxSlots, _sut.ActiveSlotCount);
    }

    // ── Subscription lifecycle ─────────────────────────────────────────────────

    [Fact]
    public void TrackSubscription_StopBroadcast_DisposesAllSubscriptions()
    {
        var sub1 = new Mock<IDisposable>();
        var sub2 = new Mock<IDisposable>();

        _sut.TrackSubscription(sub1.Object);
        _sut.TrackSubscription(sub2.Object);
        _sut.StopBroadcast();

        sub1.Verify(s => s.Dispose(), Times.Once);
        sub2.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public void TrackSubscription_StopBroadcastTwice_DisposesOnce()
    {
        var sub = new Mock<IDisposable>();
        _sut.TrackSubscription(sub.Object);

        _sut.StopBroadcast(); // first — disposes
        _sut.StopBroadcast(); // second — list already cleared

        sub.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public void StartBroadcast_ReturnsUnexpiredToken()
    {
        var token = _sut.StartBroadcast();
        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public void StartBroadcast_CalledTwice_CancelsPreviousToken()
    {
        var first = _sut.StartBroadcast();
        _sut.StartBroadcast(); // re-start cancels the first

        Assert.True(first.IsCancellationRequested);
    }

    [Fact]
    public void StopBroadcast_CancelsBroadcastToken()
    {
        var token = _sut.StartBroadcast();
        _sut.StopBroadcast();

        Assert.True(token.IsCancellationRequested);
    }

    // ── Start / Stop ───────────────────────────────────────────────────────────

    [Fact]
    public void IsActive_BeforeStart_IsFalse()
    {
        Assert.False(_sut.IsActive);
    }

    [Fact]
    public void IsActive_AfterStart_IsTrue()
    {
        _sut.Start();
        Assert.True(_sut.IsActive);
        _sut.Stop();
    }

    [Fact]
    public void IsActive_AfterStop_IsFalse()
    {
        _sut.Start();
        _sut.Stop();
        Assert.False(_sut.IsActive);
    }

    [Fact]
    public void Stop_ClearsAllSlotAssignments()
    {
        _sut.AssignSlot("player-1");
        _sut.AssignSlot("player-2");

        _sut.Stop();

        Assert.Equal(0, _sut.ActiveSlotCount);
    }

    // ── Thread safety ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AssignAndRelease_ConcurrentAccess_DoesNotCorruptState()
    {
        const int threadCount = 20;
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
        {
            try
            {
                string id = $"player-{i}";
                _sut.AssignSlot(id);
                _sut.UpdateRemotePuppet(id, MakePuppet());
                _sut.ReleaseSlot(id);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        await Task.WhenAll(tasks);

        Assert.Empty(exceptions);
        // All slots should be released after all threads complete
        Assert.Equal(0, _sut.ActiveSlotCount);
    }

    [Fact]
    public async Task TrackSubscription_ConcurrentAdd_AllDisposedOnStop()
    {
        const int count = 50;
        var subs = Enumerable.Range(0, count)
            .Select(_ => new Mock<IDisposable>())
            .ToList();

        // Add all subscriptions concurrently
        await Task.WhenAll(subs.Select(s => Task.Run(() => _sut.TrackSubscription(s.Object))));

        _sut.StopBroadcast();

        foreach (var sub in subs)
            sub.Verify(s => s.Dispose(), Times.Once);
    }

    // ── Player names (name tags) ───────────────────────────────────────────────

    [Fact]
    public void GetSlotNames_FollowsSlots_WithSanitisedJoinNames()
    {
        _sut.SetPlayerName("player-A", "Jösé");
        _sut.SetPlayerName("player-B", "ゼルダ");
        _sut.AssignSlot("player-A");
        _sut.AssignSlot("player-B");

        Assert.Equal(new[] { "Jose", "Player 2", "" }, _sut.GetSlotNames());

        _sut.ReleaseSlot("player-A");
        Assert.Equal(new[] { "", "Player 2", "" }, _sut.GetSlotNames());

        // The name goes with the player: a rejoin under the same id has to announce it again.
        _sut.AssignSlot("player-A");
        Assert.Equal(new[] { "Player 1", "Player 2", "" }, _sut.GetSlotNames());
    }

    [Fact]
    public void GetSlotNames_FallsBackToThePuppetDataName()
    {
        var puppet = MakePuppet();
        puppet.PlayerName = "Tetra";
        _sut.UpdateRemotePuppet("player-A", puppet);
        Assert.Equal("Tetra", _sut.GetSlotNames()[0]);

        _sut.SetPlayerName("player-A", "Link");
        Assert.Equal("Link", _sut.GetSlotNames()[0]);
    }

    [Fact]
    public void ShowPlayerNames_DefaultsFromSettings()
    {
        Assert.Equal(_settings.Load().ShowPlayerNames, _sut.ShowPlayerNames);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static PuppetData MakePuppet(float x = 0, float y = 0, float z = 0) => new()
    {
        PlayerId = "test",
        Position = new Vector3(x, y, z),
        Rotation = 0,
        Animation = new AnimationState { AnimationSpeed = 1.0f },
        Action = new ActionState(),
        Equipment = new EquipmentState(),
        Timestamp = DateTime.UtcNow
    };
}
