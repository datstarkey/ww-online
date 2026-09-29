using WWOnline.Data;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Other players' projectiles on the wire: the event DTO's validation (the server's first gate), the
/// server's relay scoping and rate limit, and the receiver's per-sender de-duplication.
/// </summary>
public class PlayerEventTests
{
    private static PlayerEvent Valid(PlayerEventKind kind = PlayerEventKind.BombThrow) => new()
    {
        Kind = (byte)kind,
        Id = 42,
        Origin = "0123456789abcdef0123456789ABCDEF",
        Seq = 1,
        StageName = "M_NewD2",
        RoomNumber = 1,
        Position = new Vector3(-1200.5f, 300f, 4500f),
        SpeedF = 20f,
        SpeedY = 16f,
        Gravity = -2.9f,
        AngleY = -0x4000,
        Timer = 132,
    };

    [Fact]
    public void Kinds_MatchTheRelLayout()
    {
        Assert.Equal(PuppetLayout.PUPPET_FX_KIND_BOMB_THROW, (int)PlayerEventKind.BombThrow);
        Assert.Equal(PuppetLayout.PUPPET_FX_KIND_BOMB_PICKUP, (int)PlayerEventKind.BombPickup);
        Assert.Equal(PuppetLayout.PUPPET_FX_KIND_EXPLODE, (int)PlayerEventKind.Explode);
        Assert.Equal(PuppetLayout.PUPPET_FX_KIND_REMOVE, (int)PlayerEventKind.Remove);
        Assert.Equal(PuppetLayout.PUPPET_FX_KIND_CANNON, (int)PlayerEventKind.Cannon);
        Assert.Equal(PuppetLayout.PUPPET_FX_KIND_ARROW, (int)PlayerEventKind.Arrow);
        Assert.Equal(PuppetLayout.PUPPET_FX_ARROW_TYPE_MAX, PlayerEvent.ArrowLight);
        Assert.Equal(PuppetLayout.PUPPET_FX_KIND_MAX, (int)PlayerEventKind.Max);
    }

    [Theory]
    [InlineData(PlayerEventKind.BombThrow)]
    [InlineData(PlayerEventKind.BombPickup)]
    [InlineData(PlayerEventKind.Explode)]
    [InlineData(PlayerEventKind.Remove)]
    [InlineData(PlayerEventKind.Cannon)]
    [InlineData(PlayerEventKind.Arrow)]
    public void EveryKnownKind_IsValid(PlayerEventKind kind) => Assert.True(Valid(kind).IsValid());

    public static TheoryData<string, Action<PlayerEvent>> Broken => new()
    {
        { "kind 0", e => e.Kind = 0 },
        { "unknown kind", e => e.Kind = (byte)PlayerEventKind.Max + 1 },
        { "kind 255", e => e.Kind = 255 },
        { "NaN x", e => e.Position = new Vector3(float.NaN, 0, 0) },
        { "infinite y", e => e.Position = new Vector3(0, float.PositiveInfinity, 0) },
        { "z out of the world", e => e.Position = new Vector3(0, 0, -PlayerEvent.MaxCoordinate) },
        { "no position", e => e.Position = null! },
        { "NaN speedF", e => e.SpeedF = float.NaN },
        { "huge speedY", e => e.SpeedY = PlayerEvent.MaxSpeed },
        { "infinite gravity", e => e.Gravity = float.NegativeInfinity },
        { "huge gravity", e => e.Gravity = -PlayerEvent.MaxGravity },
        { "negative timer", e => e.Timer = -1 },
        { "short.MinValue timer", e => e.Timer = short.MinValue },
        { "long timer", e => e.Timer = PlayerEvent.MaxTimer + 1 },
        { "variant on a bomb", e => e.Variant = 1 },
        { "unknown arrow type", e => { e.Kind = (byte)PlayerEventKind.Arrow; e.Variant = PlayerEvent.ArrowLight + 1; } },
        { "no stage", e => e.StageName = "" },
        { "null stage", e => e.StageName = null! },
        { "long stage", e => e.StageName = "M_NewD2xx" },
        { "stage with a space", e => e.StageName = "sea 1" },
        { "stage with a control char", e => e.StageName = "sea\u0001" },
        { "no origin", e => e.Origin = "" },
        { "null origin", e => e.Origin = null! },
        { "long origin", e => e.Origin = new string('a', PlayerEvent.MaxOriginLength + 1) },
        { "origin with a dash", e => e.Origin = "abc-def" },
    };

    [Theory]
    [MemberData(nameof(Broken))]
    public void OutOfRangeFields_AreInvalid(string what, Action<PlayerEvent> breakIt)
    {
        var e = Valid();
        breakIt(e);
        Assert.False(e.IsValid(), what);
    }

    [Theory]
    [InlineData(PlayerEvent.ArrowNormal)]
    [InlineData(PlayerEvent.ArrowFire)]
    [InlineData(PlayerEvent.ArrowIce)]
    [InlineData(PlayerEvent.ArrowLight)]
    public void EveryArrowType_IsValid(byte type)
    {
        var e = Valid(PlayerEventKind.Arrow);
        e.Variant = type;
        e.SpeedF = 0;
        e.SpeedY = 200; // arrowShooting: 200 a frame along the aim
        Assert.True(e.IsValid());
    }

    [Fact]
    public void ExtremesJustInside_AreValid()
    {
        var e = Valid();
        e.Position = new Vector3(PlayerEvent.MaxCoordinate - 1000, -PlayerEvent.MaxCoordinate + 1000, 0);
        e.Timer = PlayerEvent.MaxTimer;
        e.AngleX = short.MinValue;
        e.AngleY = short.MaxValue;
        e.Id = uint.MaxValue;
        e.StageName = "sea";
        Assert.True(e.IsValid());
    }

    // ── Relay scoping (server) ──────────────────────────────────────────────

    private static PuppetData At(string stage, byte room, float x = 0, float z = 0) =>
        new() { StageName = stage, RoomNumber = room, Position = new Vector3(x, 0, z) };

    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static PlayerEvent In(string stage, byte room, float x = 0, float z = 0)
    {
        var e = Valid();
        e.StageName = stage;
        e.RoomNumber = room;
        e.Position = new Vector3(x, 0, z);
        return e;
    }

    [Fact]
    public void Relay_GoesToTheEventsRoom_NotOtherRoomsOrStages_NotBackToTheSender()
    {
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("sender", At("M_NewD2", 1), T0);
        relay.UpdateLocation("sameRoom", At("M_NewD2", 1, 5000, 5000), T0);
        relay.UpdateLocation("otherRoom", At("M_NewD2", 2), T0);
        relay.UpdateLocation("otherStage", At("sea", 1), T0);

        var evt = In("M_NewD2", 1);
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("sender", evt, T0));
        Assert.Equal(["sameRoom"], relay.Recipients("sender", evt, T0));
    }

    [Fact]
    public void Relay_ByTheEventsRoom_NotWhereTheSenderIsNow()
    {
        // A bomb thrown in room 1 explodes after its thrower walked into room 2: room 1 sees it, room 2 doesn't.
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("sender", At("M_NewD2", 2), T0);
        relay.UpdateLocation("room1", At("M_NewD2", 1), T0);
        relay.UpdateLocation("room2", At("M_NewD2", 2), T0);

        var explode = In("M_NewD2", 1);
        explode.Kind = (byte)PlayerEventKind.Explode;
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("sender", explode, T0));
        Assert.Equal(["room1"], relay.Recipients("sender", explode, T0));
    }

    [Fact]
    public void Relay_AtSea_GoesByDistanceFromTheEvent_AcrossSquares()
    {
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("sender", At("sea", 11, 0, 0), T0);
        relay.UpdateLocation("nearTheShot", At("sea", 12, PuppetVisibility.SeaHideDistance + 5000, 0), T0);
        relay.UpdateLocation("farAway", At("sea", 12, -PuppetVisibility.SeaHideDistance - 100, 0), T0);

        // A cannonball landing 10000 units out: whoever is within sight of it gets it.
        var evt = In("sea", 11, 10000, 0);
        Assert.Equal(["nearTheShot"], relay.Recipients("sender", evt, T0));
    }

    [Fact]
    public void Relay_NeedsTheSendersLocation_AndAnEventWhereTheyAre()
    {
        var relay = new PlayerEventRelay();
        Assert.Equal(PlayerEventRelay.Verdict.NoLocation, relay.Check("sender", Valid(), T0));

        relay.UpdateLocation("sender", At("M_NewD2", 1, -1200, 4500), T0);
        var otherStage = Valid();
        otherStage.StageName = "sea";
        Assert.Equal(PlayerEventRelay.Verdict.WrongPlace, relay.Check("sender", otherStage, T0));

        var farAway = Valid();
        farAway.Position = new Vector3(-1200 + PlayerEventRelay.MaxDistanceFromSender + 1, 0, 4500);
        Assert.Equal(PlayerEventRelay.Verdict.WrongPlace, relay.Check("sender", farAway, T0));

        Assert.Equal(PlayerEventRelay.Verdict.Invalid, relay.Check("sender", null, T0));
        var broken = Valid();
        broken.Kind = 0;
        Assert.Equal(PlayerEventRelay.Verdict.Invalid, relay.Check("sender", broken, T0));
    }

    [Fact]
    public void Relay_ForgetsStaleLocations_BothWays()
    {
        // A player whose game is detached stops sending puppet data: they neither send nor get events.
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("sender", At("M_NewD2", 1, -1200, 4500), T0);
        relay.UpdateLocation("viewer", At("M_NewD2", 1), T0);
        var later = T0 + PlayerEventRelay.LocationTtl + TimeSpan.FromMilliseconds(1);

        Assert.Equal(PlayerEventRelay.Verdict.NoLocation, relay.Check("sender", Valid(), later));
        relay.UpdateLocation("sender", At("M_NewD2", 1, -1200, 4500), later);
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("sender", Valid(), later));
        Assert.Empty(relay.Recipients("sender", Valid(), later)); // the viewer went quiet
    }

    [Fact]
    public void Relay_AnOriginBelongsToItsConnection_UntilItLeaves()
    {
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("victim", At("M_NewD2", 1, -1200, 4500), T0);
        relay.UpdateLocation("spoofer", At("M_NewD2", 1, -1200, 4500), T0);
        var victims = Valid();
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("victim", victims, T0));

        // Another connection can't send under the victim's origin (a huge seq would silence them).
        var stolen = Valid();
        stolen.Seq = uint.MaxValue;
        Assert.Equal(PlayerEventRelay.Verdict.ForeignOrigin, relay.Check("spoofer", stolen, T0));

        // One origin per connection.
        var mine = Valid();
        mine.Origin = "spoofer1";
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("spoofer", mine, T0));
        var another = Valid();
        another.Origin = "spoofer2";
        Assert.Equal(PlayerEventRelay.Verdict.ForeignOrigin, relay.Check("spoofer", another, T0));

        // The victim reconnects: once their old connection is gone, the new one takes the origin back.
        relay.UpdateLocation("victim-new", At("M_NewD2", 1, -1200, 4500), T0);
        Assert.Equal(PlayerEventRelay.Verdict.ForeignOrigin, relay.Check("victim-new", Valid(), T0));
        relay.Forget("victim");
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("victim-new", Valid(), T0));
    }

    [Fact]
    public void Relay_AStaleOwnersOrigin_CanBeTakenOver_ALiveOnesNever()
    {
        // An unclean drop: the old connection lingers (no OnDisconnected yet) but stops sending puppet data.
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("old", At("M_NewD2", 1, -1200, 4500), T0);
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("old", Valid(), T0));

        var soon = T0 + TimeSpan.FromSeconds(1);
        relay.UpdateLocation("new", At("M_NewD2", 1, -1200, 4500), soon);
        Assert.Equal(PlayerEventRelay.Verdict.ForeignOrigin, relay.Check("new", Valid(), soon)); // the owner is live

        var later = T0 + PlayerEventRelay.LocationTtl + TimeSpan.FromSeconds(1);
        relay.UpdateLocation("new", At("M_NewD2", 1, -1200, 4500), later);
        var resent = Valid();
        resent.Seq = 2;
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("new", resent, later)); // taken over

        // The old connection comes back to life: the origin is the new one's now, but it may take a new one.
        relay.UpdateLocation("old", At("M_NewD2", 1, -1200, 4500), later);
        Assert.Equal(PlayerEventRelay.Verdict.ForeignOrigin, relay.Check("old", Valid(), later));
        var fresh = Valid();
        fresh.Origin = "neworigin";
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("old", fresh, later));
    }

    [Fact]
    public void MinigameGate_CountsOnlyInTheStageWhereItStarted()
    {
        var gate = new MinigameGate();
        Assert.False(gate.Update(0, "Atorizk"));
        Assert.True(gate.Update(7, "Atorizk"));   // mail sorting starts
        Assert.True(gate.Update(7, "Atorizk"));
        Assert.False(gate.Update(0, "Atorizk"));  // ended
        Assert.True(gate.Update(5, "Obombh"));    // the auction
        Assert.False(gate.Update(5, "sea"));      // left without endMiniGame (game over, title): stale
        Assert.False(gate.Update(5, "Obombh"));   // still stale until it is cleared
        Assert.False(gate.Update(0, "sea"));
        Assert.True(gate.Update(3, "sea"));       // a new one counts again
    }

    [Fact]
    public void MinigameGate_ATypeLeftSetInOneStage_IsStaleInTheNext()
    {
        // The gate is fed every tick (no peer events needed), so it saw the minigame in Atorizk.
        var gate = new MinigameGate();
        Assert.True(gate.Update(7, "Atorizk"));
        Assert.False(gate.Update(7, "sea"));
    }

    [Fact]
    public void MinigameGate_ANewTypeReplacingAStaleOne_CountsAgain()
    {
        // A stale auction (5) left set, then Spectacle Island's cannon game (3) overwrites it with no 0 between.
        var gate = new MinigameGate();
        Assert.True(gate.Update(5, "Obombh"));
        Assert.False(gate.Update(5, "sea"));
        Assert.True(gate.Update(3, "sea"));
        Assert.True(gate.Update(3, "sea"));
        Assert.False(gate.Update(3, "Obombh")); // and that one goes stale too if it is left
    }

    [Fact]
    public void Relay_RateLimitsBursts_ThenRefills()
    {
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("sender", At("M_NewD2", 1, -1200, 4500), T0);
        int burst = (int)PlayerEventRelay.BurstEvents;
        for (int i = 0; i < burst; i++)
            Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("sender", Valid(), T0));
        Assert.Equal(PlayerEventRelay.Verdict.RateLimited, relay.Check("sender", Valid(), T0));

        // Time refills the bucket.
        var later = T0.AddSeconds(1.0 / PlayerEventRelay.EventsPerSecond * 2);
        relay.UpdateLocation("sender", At("M_NewD2", 1, -1200, 4500), later);
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("sender", Valid(), later));
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("sender", Valid(), later));
        Assert.Equal(PlayerEventRelay.Verdict.RateLimited, relay.Check("sender", Valid(), later));

        // Another player has their own bucket.
        relay.UpdateLocation("other", At("M_NewD2", 1, -1200, 4500), T0);
        var others = Valid();
        others.Origin = "other";
        Assert.Equal(PlayerEventRelay.Verdict.Relay, relay.Check("other", others, T0));
    }

    [Fact]
    public void Relay_ForgetsAPlayerWhoLeft()
    {
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("sender", At("M_NewD2", 1), T0);
        relay.UpdateLocation("viewer", At("M_NewD2", 1), T0);
        relay.Forget("viewer");
        Assert.Empty(relay.Recipients("sender", In("M_NewD2", 1), T0));
        relay.Forget("sender");
        Assert.Equal(PlayerEventRelay.Verdict.NoLocation, relay.Check("sender", Valid(), T0));
    }

    [Fact]
    public void Relay_IgnoresPuppetDataWithoutAFinitePosition()
    {
        var relay = new PlayerEventRelay();
        relay.UpdateLocation("sender", new PuppetData { StageName = "sea", Position = new Vector3(float.NaN, 0, 0) }, T0);
        Assert.Equal(PlayerEventRelay.Verdict.NoLocation, relay.Check("sender", Valid(), T0));
    }

    [Fact]
    public void ToString_IsSafeOnAnUnvalidatedEvent()
    {
        var e = new PlayerEvent { Position = null!, StageName = "line\nbreak" + new string('x', 30000) };
        string text = e.ToString();
        Assert.Contains("no position", text);
        Assert.DoesNotContain('\n', text);
        Assert.True(text.Length < 100);
        Assert.Contains("0 at", new PlayerEvent { StageName = null! }.ToString());
    }

    // ── De-duplication (receiver) ───────────────────────────────────────────

    private static PlayerEvent From(string origin, uint seq, PlayerEventKind kind = PlayerEventKind.BombThrow,
                                    string connection = "conn1")
    {
        var e = Valid(kind);
        e.PlayerId = connection;
        e.Origin = origin;
        e.Seq = seq;
        return e;
    }

    [Fact]
    public void Dedup_DropsRepeatsAndOlderEvents_PerOrigin()
    {
        var dedup = new PlayerEventDedup();
        Assert.True(dedup.Accept(From("a", 1)));
        Assert.False(dedup.Accept(From("a", 1)));                         // the same event again
        Assert.False(dedup.Accept(From("a", 1, PlayerEventKind.Explode))); // same seq, whatever it says
        Assert.True(dedup.Accept(From("a", 3)));
        Assert.False(dedup.Accept(From("a", 2)));                         // late, after a newer one
        Assert.True(dedup.Accept(From("b", 1)));                          // another app counts on its own
    }

    [Fact]
    public void Dedup_AnEventResentAfterAReconnect_IsStillARepeat()
    {
        // The send went out, the connection dropped before the ack, the client re-sent it on its new connection.
        var dedup = new PlayerEventDedup();
        Assert.True(dedup.Accept(From("a", 7, connection: "old")));
        Assert.False(dedup.Accept(From("a", 7, connection: "new")));
        Assert.True(dedup.Accept(From("a", 8, connection: "new")));
    }

    [Fact]
    public void Dedup_TheSameBombThrownTwice_IsTwoEvents()
    {
        // Throw, pick up, throw again: same projectile id and kind, new seqs.
        var dedup = new PlayerEventDedup();
        Assert.True(dedup.Accept(From("a", 1, PlayerEventKind.BombThrow)));
        Assert.True(dedup.Accept(From("a", 2, PlayerEventKind.BombPickup)));
        Assert.True(dedup.Accept(From("a", 3, PlayerEventKind.BombThrow)));
    }

    [Fact]
    public void Dedup_RemembersABoundedNumberOfOrigins()
    {
        var dedup = new PlayerEventDedup();
        Assert.True(dedup.Accept(From("first", 5)));
        for (int i = 0; i < PlayerEventDedup.MaxOrigins; i++)
            Assert.True(dedup.Accept(From($"o{i}", 1)));
        Assert.True(dedup.Accept(From("first", 5))); // forgotten (oldest), so accepted again
    }
}
