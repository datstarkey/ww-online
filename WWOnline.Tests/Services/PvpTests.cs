using WWOnline.Server;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>PVP's wire and rules: <see cref="PvpHit.IsValid"/>, the server's <see cref="PvpHitGate"/>, and the Pvp rule's
/// options and presets.</summary>
public sealed class PvpTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PvpHit Hit(string target = "target") => new()
    {
        TargetId = target,
        StageName = "sea",
        Damage = 4,
        Spl = 0,
        AtType = 0x2,
        Position = new Vector3(110, 0, 0),
        AttackerPosition = new Vector3(0, 0, 0),
    };

    private static PuppetData At(string stage, float x, float z = 0) =>
        new() { StageName = stage, Position = new Vector3(x, 0, z) };

    /// <summary>A sender at 0,0 and a target at 100,0 on the sea, both native.</summary>
    private static (PlayerEventRelay Locations, PvpHitGate Gate) Room()
    {
        var locations = new PlayerEventRelay();
        var gate = new PvpHitGate(locations);
        locations.UpdateLocation("sender", At("sea", 0), T0);
        locations.UpdateLocation("target", At("sea", 100), T0);
        gate.Join("sender", ClientCapabilities.PvpHits);
        gate.Join("target", ClientCapabilities.PvpHits);
        return (locations, gate);
    }

    [Fact]
    public void Hit_IsValid_InTheGamesRanges()
    {
        Assert.True(Hit().IsValid());
        Assert.True(new PvpHit { TargetId = "t", StageName = "M_NewD2", Damage = PvpHit.MaxDamage, Spl = PvpHit.MaxSpl,
                                 AtType = PvpHit.LinkAtTypes }.IsValid());

        Assert.False(Hit("").IsValid());
        Assert.False(Hit(new string('a', PvpHit.MaxIdLength + 1)).IsValid());
        Assert.False(Hit("bad id").IsValid());
    }

    [Theory]
    [InlineData(0, 0, 0x2u, "sea")]           // no damage
    [InlineData(81, 0, 0x2u, "sea")]          // over 20 hearts
    [InlineData(4, 0x0C, 0x2u, "sea")]        // special past dCcG_At_Spl
    [InlineData(4, 0, 0u, "sea")]             // no attack type
    [InlineData(4, 0, 0x1u, "sea")]           // an enemy's attack type, not Link's
    [InlineData(4, 0, 0x2u | 0x80000000u, "sea")]
    [InlineData(4, 0, 0x2u, "")]              // no stage
    [InlineData(4, 0, 0x2u, "TooLongName")]
    [InlineData(4, 0, 0x2u, "se\na")]
    public void Hit_IsInvalid_OutOfRange(int damage, int spl, uint atType, string stage)
    {
        var hit = Hit();
        hit.Damage = (byte)damage;
        hit.Spl = (byte)spl;
        hit.AtType = atType;
        hit.StageName = stage;
        Assert.False(hit.IsValid());
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(2.0e6f)]
    public void Hit_IsInvalid_OffTheWorld(float x)
    {
        var hit = Hit();
        hit.Position = new Vector3(x, 0, 0);
        Assert.False(hit.IsValid());
        hit = Hit();
        hit.AttackerPosition = new Vector3(0, x, 0);
        Assert.False(hit.IsValid());
        hit = Hit();
        hit.Position = null!;
        Assert.False(hit.IsValid());
    }

    [Fact]
    public void Hit_ToString_IsSafeOnGarbage()
    {
        var hit = new PvpHit { TargetId = null!, StageName = "a\nb" + new string('x', 100), Position = null! };
        var text = hit.ToString();
        Assert.DoesNotContain("\n", text);
        Assert.True(text.Length < 120);
    }

    [Fact]
    public void Gate_RelaysAHitBetweenTwoNativePlayersWhereTheyAre()
    {
        var (_, gate) = Room();
        Assert.Equal(PvpHitGate.Verdict.Relay, gate.Check("sender", Hit(), ruleOn: true, T0));
    }

    [Fact]
    public void Gate_DropsEverythingWhileTheRuleIsOff_AndInvalidHits()
    {
        var (_, gate) = Room();
        Assert.Equal(PvpHitGate.Verdict.RuleOff, gate.Check("sender", Hit(), ruleOn: false, T0));
        Assert.Equal(PvpHitGate.Verdict.Invalid, gate.Check("sender", null, ruleOn: true, T0));
        var broken = Hit();
        broken.Damage = 0;
        Assert.Equal(PvpHitGate.Verdict.Invalid, gate.Check("sender", broken, ruleOn: true, T0));
    }

    [Fact]
    public void Gate_NeverHitsADolphinPlayer_NorTakesTheirHits()
    {
        var (locations, gate) = Room();
        locations.UpdateLocation("dolphin", At("sea", 50), T0);
        gate.Join("dolphin", ClientCapabilities.None);

        Assert.Equal(PvpHitGate.Verdict.TargetCannotPvp, gate.Check("sender", Hit("dolphin"), ruleOn: true, T0));
        Assert.Equal(PvpHitGate.Verdict.SenderCannotPvp, gate.Check("dolphin", Hit(), ruleOn: true, T0));
        Assert.Equal(PvpHitGate.Verdict.SenderCannotPvp, gate.Check("never-joined", Hit(), ruleOn: true, T0));
    }

    [Fact]
    public void Gate_DropsAHitOnYourself_OrOnSomeoneNotHere()
    {
        var (_, gate) = Room();
        Assert.Equal(PvpHitGate.Verdict.BadTarget, gate.Check("sender", Hit("sender"), ruleOn: true, T0));
        Assert.Equal(PvpHitGate.Verdict.BadTarget, gate.Check("sender", Hit("nobody"), ruleOn: true, T0));

        gate.Forget("target");
        Assert.Equal(PvpHitGate.Verdict.BadTarget, gate.Check("sender", Hit(), ruleOn: true, T0));
    }

    [Fact]
    public void Gate_NeedsBothFreshLocations_InTheHitsStage()
    {
        var (locations, gate) = Room();
        var later = T0 + PlayerEventRelay.LocationTtl + TimeSpan.FromSeconds(1);
        locations.UpdateLocation("sender", At("sea", 0), later);
        Assert.Equal(PvpHitGate.Verdict.NoLocation, gate.Check("sender", Hit(), ruleOn: true, later)); // target stale

        (locations, gate) = Room();
        var otherStage = Hit();
        otherStage.StageName = "M_NewD2";
        Assert.Equal(PvpHitGate.Verdict.WrongStage, gate.Check("sender", otherStage, ruleOn: true, T0));

        locations.UpdateLocation("target", At("M_NewD2", 100), T0);
        Assert.Equal(PvpHitGate.Verdict.WrongStage, gate.Check("sender", Hit(), ruleOn: true, T0)); // target elsewhere
    }

    [Fact]
    public void Gate_DropsAHitFarFromTheSenderOrTheTarget()
    {
        var (_, gate) = Room();
        var farAttacker = Hit();
        farAttacker.AttackerPosition = new Vector3(0, 0, PvpHitGate.MaxDistance + 1);
        Assert.Equal(PvpHitGate.Verdict.TooFar, gate.Check("sender", farAttacker, ruleOn: true, T0));

        var farSpot = Hit();
        farSpot.Position = new Vector3(100 + PvpHitGate.MaxDistance + 1, 0, 0);
        Assert.Equal(PvpHitGate.Verdict.TooFar, gate.Check("sender", farSpot, ruleOn: true, T0));

        var high = Hit(); // height doesn't count: the target may stand on a ledge
        high.Position = new Vector3(100, 5000, 0);
        Assert.Equal(PvpHitGate.Verdict.Relay, gate.Check("sender", high, ruleOn: true, T0));
    }

    [Fact]
    public void Gate_RateLimitsEachSender_AndRejectedHitsCostNothing()
    {
        var (_, gate) = Room();
        var broken = Hit();
        broken.AtType = 0;
        for (int i = 0; i < 50; i++)
            gate.Check("sender", broken, ruleOn: true, T0);

        for (int i = 0; i < PvpHitGate.BurstHits; i++)
            Assert.Equal(PvpHitGate.Verdict.Relay, gate.Check("sender", Hit(), ruleOn: true, T0));
        Assert.Equal(PvpHitGate.Verdict.RateLimited, gate.Check("sender", Hit(), ruleOn: true, T0));
        Assert.Equal(PvpHitGate.Verdict.Relay, gate.Check("sender", Hit(), ruleOn: true, T0 + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Rule_IsOffByDefault_AndNoPresetTouchesIt()
    {
        Assert.False(new RoomSettings().Pvp);
        var s = new RoomSettings { Pvp = true };
        Assert.Equal(RoomPreset.FullSync, s.MatchingPreset());
        Assert.True(s.ApplyPreset(RoomPreset.Coop).Pvp);
        Assert.False(new RoomSettings().ApplyPreset(RoomPreset.FullSync).Pvp);
        Assert.Contains("PVP ON", s.RulesSummary());
        Assert.True(s.Clone().Pvp);
    }

    [Fact]
    public void ServerOptions_TakePvp_FromTheFlagOrTheVariable()
    {
        Assert.False(ServerOptions.Parse([], _ => null).Options.Pvp);
        Assert.True(ServerOptions.Parse(["--pvp"], _ => null).Options.Pvp);
        Assert.True(ServerOptions.Parse([], v => v == ServerOptions.PvpVar ? "true" : null).Options.Pvp);
    }

    [Fact]
    public void HostedRoom_PassesPvp_ToTheServerItStarts()
    {
        Assert.Equal(" --pvp", HostedRoomRules.ServerArgs(new RoomSettings { Pvp = true }));
        Assert.False(HostedRoomRules.SameRules(new RoomSettings { Pvp = true }, new RoomSettings()));
    }

    [Fact]
    public void PvpArena_IsCoopWithPvp()
    {
        var arena = HostedRoomRules.PvpArena();
        Assert.Equal(RoomPreset.Coop, arena.MatchingPreset());
        Assert.True(arena.Pvp);
        Assert.Equal(" --no-shared-wallet --no-shared-world --no-shared-items --no-shared-story --no-shared-bait" +
                     " --no-shared-spoils --no-shared-delivery --no-warping --pvp", HostedRoomRules.ServerArgs(arena));
    }

    [Fact]
    public void ClientFlag_Pvp()
    {
        Assert.True(StartupOptions.Parse(["--player", "Player1", "--pvp"]).Pvp);
        Assert.False(StartupOptions.Parse(["--player", "Player1"]).Pvp);
    }
}
