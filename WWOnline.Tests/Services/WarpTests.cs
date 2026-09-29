using System.Buffers.Binary;
using System.Text;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// "Warp to player" (docs/softlocks.md): the entrance each player broadcasts, the planner's two paths (same
/// stage and room: move Link; anywhere else: load the target's entrance) and refusals, and the game-memory
/// writes behind both, on a FakeDolphin.
/// </summary>
public class WarpTests
{
    private const uint Link = 0x80A00000;
    private static readonly uint NextStage = GameMemoryAddresses.Warp.NextStage;
    private static readonly uint Restart = GameMemoryAddresses.Warp.Restart;

    // ── A fake game ──────────────────────────────────────────────────────────

    private static FakeDolphin Game(string stage = "sea", short point = 0, sbyte entryRoom = 44, sbyte layer = -1,
                                    byte room = 44, ushort health = 12)
    {
        var d = new FakeDolphin();
        SetStartStage(d, stage, point, entryRoom, layer);
        d.Set(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, room);
        d.SetU32(GameMemoryAddresses.Warp.LinkActorPtr, Link);
        d.SetU32(GameMemoryAddresses.Warp.ControlledActorPtr, Link);
        d.Set(GameMemoryAddresses.Player.CurrentHealth.Address, (byte)(health >> 8), (byte)health);
        SetPos(d, Link, new Vector3(10f, 20f, 30f));
        return d;
    }

    private static void SetStartStage(FakeDolphin d, string stage, short point, sbyte room, sbyte layer)
    {
        var name = new byte[8];
        Encoding.ASCII.GetBytes(stage, name);
        d.Set(GameMemoryAddresses.Warp.StartStage, name);
        d.Set(GameMemoryAddresses.Warp.StartStage + 8, (byte)(point >> 8), (byte)point, (byte)room, (byte)layer);
    }

    private static void SetPos(FakeDolphin d, uint actor, Vector3 p)
    {
        var b = new byte[12];
        BinaryPrimitives.WriteSingleBigEndian(b.AsSpan(0), p.X);
        BinaryPrimitives.WriteSingleBigEndian(b.AsSpan(4), p.Y);
        BinaryPrimitives.WriteSingleBigEndian(b.AsSpan(8), p.Z);
        d.Set(actor + GameMemoryAddresses.Warp.ActorOffPos, b);
    }

    private static float F32(FakeDolphin d, uint addr) =>
        BinaryPrimitives.ReadSingleBigEndian(d.ReadMemory(addr, 4));

    private static short S16(FakeDolphin d, uint addr) =>
        BinaryPrimitives.ReadInt16BigEndian(d.ReadMemory(addr, 2));

    private static string NextStageName(FakeDolphin d) => WarpMemory.StageName(d.ReadMemory(NextStage, 8));

    // ── WarpInfo on the wire ─────────────────────────────────────────────────

    [Theory]
    [InlineData(-1, -1, -1, true)]
    [InlineData(0, 0, 0, true)]
    [InlineData(255, 63, 11, true)]
    [InlineData(256, 0, -1, false)]
    [InlineData(-2, 0, -1, false)]
    [InlineData(0, 64, -1, false)]
    [InlineData(0, -2, -1, false)]
    [InlineData(0, 0, 16, false)]
    [InlineData(0, 0, -2, false)]
    public void WarpInfo_Ranges(int point, int room, int layer, bool valid)
    {
        var w = new WarpInfo { EntryPoint = (short)point, EntryRoom = (sbyte)room, Layer = (sbyte)layer };
        Assert.Equal(valid, w.IsValid());
        var puppet = new PuppetData { Position = new Vector3(), Warp = w };
        Assert.Equal(valid, puppet.IsValid()); // the server drops the whole update, like a bad boat
    }

    [Fact]
    public void WarpInfo_UnknownBusyReason_IsInvalid()
    {
        Assert.False(new WarpInfo { Busy = (WarpBusy)99 }.IsValid());
        Assert.True(new WarpInfo { Busy = WarpBusy.Minigame }.IsValid());
        Assert.True(new PuppetData { Position = new Vector3() }.IsValid()); // no Warp is fine
    }

    [Fact]
    public void WarpInfo_HasEntry_NeedsPointAndRoom()
    {
        Assert.True(new WarpInfo { EntryPoint = 0, EntryRoom = 0 }.HasEntry);
        Assert.False(new WarpInfo { EntryPoint = -1, EntryRoom = 3 }.HasEntry);
        Assert.False(new WarpInfo { EntryPoint = 2, EntryRoom = -1 }.HasEntry);
    }

    // ── The entrance each player broadcasts ─────────────────────────────────

    [Fact]
    public void EntryTracker_KeepsTheLastRealEntrance_AcrossRestartsInTheSameStage()
    {
        var t = new WarpEntryTracker();
        var door = new StageEntry("M_NewD2", 4, 2, -1);
        Assert.Equal(door, t.Update(door));
        // A void-out restarts the stage at point -1 (dStage_restartRoom): the door is still a way in.
        Assert.Equal(door, t.Update(new StageEntry("M_NewD2", -1, 5, -1)));
        // Song of Passing (-3) too.
        Assert.Equal(door, t.Update(new StageEntry("M_NewD2", -3, 5, -1)));
        // A new stage through a spawn point replaces it.
        var sea = new StageEntry("sea", 1, 11, -1);
        Assert.Equal(sea, t.Update(sea));
    }

    [Fact]
    public void EntryTracker_ANewStageEnteredByARestart_HasNoEntrance()
    {
        var t = new WarpEntryTracker();
        t.Update(new StageEntry("Obshop", 0, 0, -1));
        // A ship interior's exit puts you on the sea at point -2: no spawn point to share.
        Assert.Null(t.Update(new StageEntry("sea", -2, 30, -1)));
        Assert.Null(t.Update(new StageEntry("sea", -1, 30, -1)));
        Assert.Equal(new StageEntry("sea", 2, 30, -1), t.Update(new StageEntry("sea", 2, 30, -1)));
    }

    [Fact]
    public void LocalWarpReader_BroadcastsTheEntranceAndBusy()
    {
        var d = Game(stage: "M_NewD2", point: 4, entryRoom: 2, layer: 3, room: 6);
        var reader = new LocalWarpReader();

        var info = reader.Read(d, "M_NewD2");
        Assert.NotNull(info);
        Assert.Equal(4, info!.EntryPoint);
        Assert.Equal(2, info.EntryRoom);
        Assert.Equal(3, info.Layer);
        Assert.Equal(WarpBusy.None, info.Busy);

        // A void-out: mCurStage point -1, the entrance stays.
        SetStartStage(d, "M_NewD2", -1, 6, -1);
        d.Set(GameMemoryAddresses.Events.EventMode, 1);
        info = reader.Read(d, "M_NewD2");
        Assert.Equal(4, info!.EntryPoint);
        Assert.Equal(WarpBusy.Event, info.Busy);
    }

    [Fact]
    public void LocalWarpReader_ReportsAMinigame()
    {
        var d = Game();
        d.Set(GameMemoryAddresses.WorldFlags.MiniGameType, 3);
        Assert.Equal(WarpBusy.Minigame, new LocalWarpReader().Read(d, "sea")!.Busy);
    }

    [Fact]
    public void ReadBusy_ChecksLoadingEventDeathAndControl()
    {
        var d = Game();
        Assert.Equal(WarpBusy.None, WarpMemory.ReadBusy(d, minigame: false));
        Assert.Equal(WarpBusy.Minigame, WarpMemory.ReadBusy(d, minigame: true));

        d.SetU32(GameMemoryAddresses.Warp.ControlledActorPtr, 0x80B00000); // Medli
        Assert.Equal(WarpBusy.OtherCharacter, WarpMemory.ReadBusy(d, false));
        d.SetU32(GameMemoryAddresses.Warp.ControlledActorPtr, Link);

        d.Set(GameMemoryAddresses.Player.CurrentHealth.Address, 0, 0);
        Assert.Equal(WarpBusy.Dead, WarpMemory.ReadBusy(d, false));
        d.Set(GameMemoryAddresses.Player.CurrentHealth.Address, 0, 4);

        d.Set(GameMemoryAddresses.Events.EventMode, 2);
        Assert.Equal(WarpBusy.Event, WarpMemory.ReadBusy(d, false));

        d.Set(NextStage + GameMemoryAddresses.Warp.NextStageOffEnable, 1);
        Assert.Equal(WarpBusy.Loading, WarpMemory.ReadBusy(d, false));

        var noLink = Game();
        noLink.SetU32(GameMemoryAddresses.Warp.LinkActorPtr, 0);
        Assert.Equal(WarpBusy.Loading, WarpMemory.ReadBusy(noLink, false));
    }

    // ── The stage change (different stage or room) ───────────────────────────

    [Fact]
    public void RequestStageChange_WritesNextStageLikeSetNextStage_EnableLast()
    {
        var d = Game();
        var writes = new List<uint>();
        d.BeforeWrite = a => writes.Add(a);

        Assert.True(WarpMemory.RequestStageChange(d, new StageEntry("M_NewD2", 5, 2, -1)));

        Assert.Equal("M_NewD2", NextStageName(d));
        Assert.Equal(0, d.Get(NextStage + 7)); // NUL-terminated
        Assert.Equal(5, S16(d, NextStage + GameMemoryAddresses.Warp.StageOffPoint));
        Assert.Equal(2, d.Get(NextStage + GameMemoryAddresses.Warp.StageOffRoom));
        Assert.Equal(0xFF, d.Get(NextStage + GameMemoryAddresses.Warp.StageOffLayer));
        Assert.Equal(0, d.Get(NextStage + GameMemoryAddresses.Warp.NextStageOffWipe));
        Assert.Equal(1, d.Get(NextStage + GameMemoryAddresses.Warp.NextStageOffEnable));
        // The game must never see mEnable before the rest of the request.
        Assert.Equal(NextStage + GameMemoryAddresses.Warp.NextStageOffEnable, writes[^1]);
        Assert.Single(writes, a => a == NextStage + GameMemoryAddresses.Warp.NextStageOffEnable);

        // mRestart: arrive standing, start code = the point (dComIfGp_setNextStage with i_setPoint).
        Assert.Equal(0f, F32(d, Restart + GameMemoryAddresses.Warp.RestartOffLastSpeedF));
        Assert.Equal(0u, d.GetU32(Restart + GameMemoryAddresses.Warp.RestartOffLastMode));
        Assert.Equal(5, S16(d, Restart + GameMemoryAddresses.Warp.RestartOffStartCode));
    }

    [Fact]
    public void RequestStageChange_ShortNameOverALongerOne_IsPaddedWithNuls()
    {
        var d = Game();
        d.Set(NextStage, Encoding.ASCII.GetBytes("Mjtower"));
        Assert.True(WarpMemory.RequestStageChange(d, new StageEntry("sea", 1, 11, -1)));
        Assert.Equal("sea", NextStageName(d));
        Assert.Equal(new byte[5], d.ReadMemory(NextStage + 3, 5));
    }

    [Fact]
    public void RequestStageChange_CarriesTheMagicArmorShieldAndSoupIntoLastMode()
    {
        var d = Game();
        d.SetU32(Link + PuppetLayout.DAPY_OFF_NO_RESET_FLG1,
                 GameMemoryAddresses.Warp.NoResetFlg1DragonShield | GameMemoryAddresses.Warp.NoResetFlg1SoupPowerUp);
        d.Set(Link + GameMemoryAddresses.Warp.LinkOffTinkleShieldTimer, 0x01, 0x20);

        Assert.True(WarpMemory.RequestStageChange(d, new StageEntry("sea", 1, 11, -1)));
        Assert.Equal(0x8000u | 0x0120u << 16 | 0x4000u, d.GetU32(Restart + GameMemoryAddresses.Warp.RestartOffLastMode));
    }

    [Fact]
    public void RequestStageChange_WhileAChangeIsPending_WritesNothing()
    {
        var d = Game();
        d.Set(NextStage + GameMemoryAddresses.Warp.NextStageOffEnable, 1);
        int writes = 0;
        d.BeforeWrite = _ => writes++;
        Assert.False(WarpMemory.RequestStageChange(d, new StageEntry("sea", 1, 11, -1)));
        Assert.Equal(0, writes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TooLong8")]
    [InlineData("sea/..")]
    public void RequestStageChange_RefusesWhatIsntAStageName(string name)
    {
        Assert.False(WarpMemory.IsValidStageName(name));
        Assert.Throws<ArgumentException>(() => WarpMemory.RequestStageChange(Game(), new StageEntry(name, 0, 0, -1)));
    }

    [Fact]
    public void ReadStartStage_ReadsPlayCurStage()
    {
        var d = Game(stage: "kaze", point: 15, entryRoom: 15, layer: 2);
        Assert.Equal(new StageEntry("kaze", 15, 15, 2), WarpMemory.ReadStartStage(d));
    }

    // ── The move (same stage and room) ───────────────────────────────────────

    [Fact]
    public void MoveActor_WritesCurrentThenOldPos_BothAngles_AndStopsLink()
    {
        var d = Game();
        d.Set(Link + GameMemoryAddresses.Warp.ActorOffSpeedF, 0x41, 0x20, 0, 0); // 10.0f
        var writes = new List<uint>();
        d.BeforeWrite = a => writes.Add(a);

        Assert.True(WarpMemory.MoveActor(d, Link, new Vector3(-100.5f, 250f, 3000f), unchecked((short)0xC000)));

        foreach (var off in new[] { GameMemoryAddresses.Warp.ActorOffPos, GameMemoryAddresses.Warp.ActorOffOldPos })
        {
            Assert.Equal(-100.5f, F32(d, Link + off));
            Assert.Equal(250f, F32(d, Link + off + 4));
            Assert.Equal(3000f, F32(d, Link + off + 8));
        }
        Assert.True(writes.IndexOf(Link + GameMemoryAddresses.Warp.ActorOffPos) <
                    writes.IndexOf(Link + GameMemoryAddresses.Warp.ActorOffOldPos));
        Assert.Equal(unchecked((short)0xC000), S16(d, Link + GameMemoryAddresses.Warp.ActorOffShapeAngleY));
        Assert.Equal(unchecked((short)0xC000), S16(d, Link + GameMemoryAddresses.Warp.ActorOffAngleY));
        Assert.Equal(0f, F32(d, Link + GameMemoryAddresses.Warp.ActorOffSpeedF));
        Assert.Equal(new byte[12], d.ReadMemory(Link + GameMemoryAddresses.Warp.ActorOffSpeed, 12));

        // Link's execute restores current.pos / angles from these statics every frame, so they carry the move too.
        Assert.Equal(-100.5f, F32(d, GameMemoryAddresses.Warp.LinkKeepPos));
        Assert.Equal(250f, F32(d, GameMemoryAddresses.Warp.LinkKeepPos + 4));
        Assert.Equal(3000f, F32(d, GameMemoryAddresses.Warp.LinkKeepPos + 8));
        Assert.Equal(unchecked((short)0xC000), S16(d, GameMemoryAddresses.Warp.LinkKeepShapeAngle + 2));
        Assert.Equal(unchecked((short)0xC000), S16(d, GameMemoryAddresses.Warp.LinkKeepCurrentAngle + 2));
    }

    // ── The planner ──────────────────────────────────────────────────────────

    private static LocalWarpSnapshot Me(string stage = "sea", byte room = 44, WarpBusy busy = WarpBusy.None,
                                        bool menu = false, uint modeFlg = 0, bool ship = false) =>
        new(stage, room, Link, busy, menu, modeFlg, ship);

    private static PuppetData Them(string stage = "sea", byte room = 44, short point = 1, sbyte entryRoom = 11,
                                   sbyte layer = -1, WarpBusy busy = WarpBusy.None, float rotation = 0x4000) => new()
    {
        StageName = stage,
        RoomNumber = room,
        Position = new Vector3(100f, 200f, 300f),
        Rotation = rotation,
        Warp = new WarpInfo { EntryPoint = point, EntryRoom = entryRoom, Layer = layer, Busy = busy },
    };

    private static WarpPlan Plan(LocalWarpSnapshot? me, PuppetData? them, bool rule = true, double ageSeconds = 0.1) =>
        WarpPlanner.Plan(rule, me, "Bob", them, TimeSpan.FromSeconds(ageSeconds));

    [Fact]
    public void SameStageAndRoom_MovesLinkToThem_NoReload()
    {
        var plan = Plan(Me(), Them());
        Assert.Equal(WarpAction.MoveInRoom, plan.Action);
        Assert.Equal(new Vector3(100f, 200f, 300f), plan.Position);
        Assert.Equal((short)0x4000, plan.AngleY);
        Assert.Null(plan.Entrance);
    }

    [Fact]
    public void SameStageAndRoom_FacingPastHalfATurn_IsANegativeAngle()
    {
        Assert.Equal(unchecked((short)0xC000), Plan(Me(), Them(rotation: 0xC000)).AngleY);
    }

    [Fact]
    public void AnotherRoom_LoadsTheirEntrance()
    {
        var plan = Plan(Me(room: 44), Them(room: 30, point: 2, entryRoom: 11, layer: 1));
        Assert.Equal(WarpAction.LoadEntrance, plan.Action);
        Assert.Equal(new StageEntry("sea", 2, 11, 1), plan.Entrance);
    }

    [Fact]
    public void AnotherStage_LoadsTheirEntrance()
    {
        var plan = Plan(Me(stage: "sea"), Them(stage: "M_NewD2", room: 3, point: 0, entryRoom: 0));
        Assert.Equal(WarpAction.LoadEntrance, plan.Action);
        Assert.Equal(new StageEntry("M_NewD2", 0, 0, -1), plan.Entrance);
        Assert.Contains("Dragon Roost Cavern", plan.Message);
    }

    [Fact]
    public void AnotherStage_WithoutAKnownEntrance_IsRefused()
    {
        var plan = Plan(Me(), Them(stage: "M_NewD2", point: -1, entryRoom: -1));
        Assert.Equal(WarpAction.Refuse, plan.Action);
        Assert.Contains("isn't known yet", plan.Message);
    }

    [Fact]
    public void RuleOff_IsRefusedFirst()
    {
        var plan = Plan(null, null, rule: false);
        Assert.Equal(WarpAction.Refuse, plan.Action);
        Assert.Contains("Allow warping", plan.Message);
    }

    [Theory]
    [InlineData(WarpBusy.Event, "cutscene")]
    [InlineData(WarpBusy.Loading, "between areas")]
    [InlineData(WarpBusy.Dead, "down")]
    [InlineData(WarpBusy.Minigame, "minigame")]
    [InlineData(WarpBusy.OtherCharacter, "another character")]
    public void LocalBusy_IsRefused(WarpBusy busy, string why)
    {
        var plan = Plan(Me(busy: busy), Them());
        Assert.Equal(WarpAction.Refuse, plan.Action);
        Assert.Contains(why, plan.Message);
        Assert.DoesNotContain("Bob", plan.Message);
    }

    [Theory]
    [InlineData(WarpBusy.Event, "cutscene")]
    [InlineData(WarpBusy.Loading, "between areas")]
    [InlineData(WarpBusy.Dead, "down")]
    [InlineData(WarpBusy.Minigame, "minigame")]
    [InlineData(WarpBusy.OtherCharacter, "another character")]
    public void TargetBusy_IsRefused(WarpBusy busy, string why)
    {
        var plan = Plan(Me(), Them(busy: busy));
        Assert.Equal(WarpAction.Refuse, plan.Action);
        Assert.StartsWith("Bob", plan.Message);
        Assert.Contains(why, plan.Message);
    }

    [Theory]
    [InlineData("sea_T")]
    [InlineData("Name")]
    [InlineData("")]
    public void TitleScreenOrFileSelect_IsRefused_ForEitherSide(string stage)
    {
        Assert.Equal(WarpAction.Refuse, Plan(Me(stage: stage), Them()).Action);
        Assert.Equal(WarpAction.Refuse, Plan(Me(), Them(stage: stage)).Action);
    }

    [Fact]
    public void NoGame_NoPuppetData_StaleData_OrNoWarpInfo_AreRefused()
    {
        Assert.Contains("isn't running", Plan(null, Them()).Message);
        Assert.Contains("isn't running", Plan(Me() with { Link = 0 }, Them()).Message);
        Assert.Contains("isn't sending a position", Plan(Me(), null).Message);
        Assert.Contains("isn't sending a position", Plan(Me(), Them(), ageSeconds: 5).Message);
        var noWarp = Them();
        noWarp.Warp = null;
        Assert.Contains("isn't shared", Plan(Me(), noWarp).Message);
    }

    [Fact]
    public void PauseMenu_IsRefused()
    {
        Assert.Contains("pause menu", Plan(Me(menu: true), Them()).Message);
    }

    [Fact]
    public void SameRoom_WhileHoldingOnOrSailing_IsRefused()
    {
        Assert.Contains("Let go", Plan(Me(modeFlg: 0x00400000 /* LADDER */), Them()).Message);
        Assert.Contains("Let go", Plan(Me(modeFlg: 0x00000020 /* HANG */), Them()).Message);
        Assert.Contains("boat", Plan(Me(ship: true), Them()).Message);
        // Swimming or in mid-air is fine: Link just lands next to them.
        Assert.Equal(WarpAction.MoveInRoom, Plan(Me(modeFlg: 0x00040000 /* SWIM */ | 0x2 /* MIDAIR */), Them()).Action);
        // Another room: a stage load, which works from the boat.
        Assert.Equal(WarpAction.LoadEntrance, Plan(Me(ship: true, room: 1), Them()).Action);
    }

    // ── The executor ─────────────────────────────────────────────────────────

    private static Task NoDelay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

    [Fact]
    public async Task Execute_Move_PutsLinkThere()
    {
        var d = Game();
        var plan = Plan(Me(), Them());
        var result = await WarpExecutor.ExecuteAsync(d, plan, Me(), _ => true, NoDelay);
        Assert.True(result.Ok, result.Message);
        Assert.Equal(new Vector3(100f, 200f, 300f), WarpMemory.ReadActorPos(d, Link));
        Assert.Equal(0, d.Get(NextStage + GameMemoryAddresses.Warp.NextStageOffEnable)); // no reload
    }

    [Fact]
    public async Task Execute_Move_RetriesThenGivesUp_WhenLinkIsPulledBack()
    {
        var d = Game();
        int moves = 0;
        // The game undoes the move every time (a proc holding him, or the Acch snapping him back).
        Task PullBack(TimeSpan _, CancellationToken __)
        {
            moves++;
            SetPos(d, Link, new Vector3(10f, 20f, 30f));
            return Task.CompletedTask;
        }
        var result = await WarpExecutor.ExecuteAsync(d, Plan(Me(), Them()), Me(), _ => true, PullBack);
        Assert.False(result.Ok);
        Assert.Contains("pulled back", result.Message);
        Assert.Equal(WarpExecutor.MoveAttempts, moves);
    }

    [Fact]
    public async Task Execute_Move_SecondAttemptSticks()
    {
        var d = Game();
        int calls = 0;
        Task PullBackOnce(TimeSpan _, CancellationToken __)
        {
            if (calls++ == 0) SetPos(d, Link, new Vector3(10f, 20f, 30f));
            return Task.CompletedTask;
        }
        var result = await WarpExecutor.ExecuteAsync(d, Plan(Me(), Them()), Me(), _ => true, PullBackOnce);
        Assert.True(result.Ok);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Execute_Move_StopsIfLinkChangedRoom()
    {
        var d = Game(room: 45);
        var result = await WarpExecutor.ExecuteAsync(d, Plan(Me(room: 44), Them()), Me(room: 44), _ => true, NoDelay);
        Assert.False(result.Ok);
        Assert.Equal(new Vector3(10f, 20f, 30f), WarpMemory.ReadActorPos(d, Link)); // untouched
    }

    [Fact]
    public async Task Execute_Load_RequestsTheirEntrance()
    {
        var d = Game();
        var plan = Plan(Me(), Them(stage: "M_NewD2", room: 3, point: 0, entryRoom: 0));
        var result = await WarpExecutor.ExecuteAsync(d, plan, Me(), _ => true, NoDelay);
        Assert.True(result.Ok, result.Message);
        Assert.Equal("M_NewD2", NextStageName(d));
        Assert.Equal(1, d.Get(NextStage + GameMemoryAddresses.Warp.NextStageOffEnable));
        Assert.Equal(new Vector3(10f, 20f, 30f), WarpMemory.ReadActorPos(d, Link)); // no reposition
    }

    [Fact]
    public async Task Execute_Load_OfAStageMissingFromTheGameFiles_IsRefused()
    {
        var d = Game();
        var plan = Plan(Me(), Them(stage: "M_NewD2", room: 3, point: 0, entryRoom: 0));
        var result = await WarpExecutor.ExecuteAsync(d, plan, Me(), _ => false, NoDelay);
        Assert.False(result.Ok);
        Assert.Equal(0, d.Get(NextStage + GameMemoryAddresses.Warp.NextStageOffEnable));
    }

    [Fact]
    public async Task Execute_Load_UnverifiableStage_StillGoes()
    {
        var d = Game();
        var plan = Plan(Me(), Them(stage: "M_NewD2", room: 3, point: 0, entryRoom: 0));
        Assert.True((await WarpExecutor.ExecuteAsync(d, plan, Me(), _ => null, NoDelay)).Ok);
    }

    [Fact]
    public async Task Execute_Refusal_WritesNothing()
    {
        var d = Game();
        int writes = 0;
        d.BeforeWrite = _ => writes++;
        var result = await WarpExecutor.ExecuteAsync(d, WarpPlan.Refuse("no"), Me(), _ => true, NoDelay);
        Assert.False(result.Ok);
        Assert.Equal("no", result.Message);
        Assert.Equal(0, writes);
    }

    // ── Stage files ──────────────────────────────────────────────────────────

    [Fact]
    public void StageFiles_ChecksEachGameFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "wwo-warp-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "files", "res", "Stage", "sea"));
            File.WriteAllBytes(Path.Combine(root, "files", "res", "Stage", "sea", "Stage.arc"), [0]);
            Assert.True(StageFiles.Exists("sea", "", root));
            Assert.False(StageFiles.Exists("M_NewD2", root));
            Assert.Null(StageFiles.Exists("sea", "", null));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
