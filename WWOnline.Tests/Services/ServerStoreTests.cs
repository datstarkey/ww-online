using WWOnline.Server.Hubs;
using WWOnline.Shared.Hubs;
using Xunit;

namespace WWOnline.Tests.Services;

public class WalletStoreTests
{
    [Fact]
    public void Delta_IsRejected_WhileUnseeded()
    {
        var wallet = new WalletStore();
        Assert.Null(wallet.ApplyDelta(50)); // a stale client must not seed the wallet with its delta
        Assert.False(wallet.IsSeeded);

        var (total, seeded) = wallet.Join(120);
        Assert.True(seeded);
        Assert.Equal(120, total);
        Assert.Equal(170, wallet.ApplyDelta(50));
    }

    [Fact]
    public void Join_OnlyTheFirstSeeds_AndIsClamped()
    {
        var wallet = new WalletStore();
        Assert.Equal((HubConstants.MaxRupees, true), wallet.Join(99999));
        Assert.Equal((HubConstants.MaxRupees, false), wallet.Join(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MinValue)] // Math.Abs(int.MinValue) used to throw
    [InlineData(int.MaxValue)]
    [InlineData(HubConstants.MaxRupees + 1)]
    [InlineData(-HubConstants.MaxRupees - 1)]
    public void InvalidDeltas_AreRejected(int delta)
    {
        var wallet = new WalletStore();
        wallet.Join(100);
        Assert.False(WalletStore.IsValidDelta(delta));
        Assert.Null(wallet.ApplyDelta(delta));
    }

    [Fact]
    public void Deltas_ClampToTheWalletRange()
    {
        var wallet = new WalletStore();
        wallet.Join(100);
        Assert.Equal(0, wallet.ApplyDelta(-HubConstants.MaxRupees));
        Assert.Equal(HubConstants.MaxRupees, wallet.ApplyDelta(HubConstants.MaxRupees));
        Assert.Equal(HubConstants.MaxRupees, wallet.ApplyDelta(1));
    }

    [Fact]
    public void Reset_MakesTheNextJoinReseed()
    {
        var wallet = new WalletStore();
        wallet.Join(500);
        wallet.Reset();
        Assert.False(wallet.IsSeeded);
        Assert.Null(wallet.ApplyDelta(10));
        Assert.Equal((42, true), wallet.Join(42));
    }
}

public class OwnerSeedGateTests
{
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly OwnerSeedGate _gate;

    public OwnerSeedGateTests() => _gate = new OwnerSeedGate(TimeSpan.FromSeconds(30), () => _now);

    [Fact]
    public void Proceeds_WhenSeeded_NoClaimedOwner_OrCallerIsOwner()
    {
        Assert.Equal(OwnerSeedGate.Decision.Proceed, _gate.Check(isSeeded: true, "owner", "joiner", out _));
        Assert.Equal(OwnerSeedGate.Decision.Proceed, _gate.Check(isSeeded: false, null, "joiner", out _));
        Assert.Equal(OwnerSeedGate.Decision.Proceed, _gate.Check(isSeeded: false, "owner", "owner", out _));
    }

    [Fact]
    public void Joiner_Waits_ThenSeeds_OnceTheOwnerHasntSeededWithinTheTimeout()
    {
        Assert.Equal(OwnerSeedGate.Decision.Wait, _gate.Check(false, "owner", "a", out bool first));
        Assert.True(first);
        _now += TimeSpan.FromSeconds(20);
        Assert.Equal(OwnerSeedGate.Decision.Wait, _gate.Check(false, "owner", "a", out first));
        Assert.False(first); // logged once per joiner
        Assert.Equal(OwnerSeedGate.Decision.Wait, _gate.Check(false, "owner", "b", out first));
        Assert.True(first);

        _now += TimeSpan.FromSeconds(10); // 30s after the FIRST wait, not b's
        Assert.Equal(OwnerSeedGate.Decision.OwnerTimedOut, _gate.Check(false, "owner", "b", out _));
        // The owner is never held back.
        Assert.Equal(OwnerSeedGate.Decision.Proceed, _gate.Check(false, "owner", "owner", out _));
    }

    [Fact]
    public void Reset_RestartsTheClock()
    {
        _gate.Check(false, "owner", "a", out _);
        _now += TimeSpan.FromSeconds(29);
        _gate.Reset(); // seeded, then e.g. the rule was cleared
        _now += TimeSpan.FromSeconds(5);
        Assert.Equal(OwnerSeedGate.Decision.Wait, _gate.Check(false, "owner", "a", out bool first));
        Assert.True(first);
    }
}
