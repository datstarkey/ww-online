using System.Numerics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Server.Hubs;
using WWOnline.Services;
using WWOnline.Shared.Hubs;
using WWOnline.Shared.Models;
using WWOnline.ViewModels;
using Xunit;

namespace WWOnline.Tests.Services;

internal static class Figurines
{
    // Figurine numbers (daNpcMt_c::isFigureGet) and where they live: register FigurineRegisterBytes[n / 8], bit n % 8.
    public const int Fig12 = 0x12;     // register 0x93, bit 2
    public const int Carlov = 0x40;    // register 0x8D, bit 0 (npc_mt phase_1 sets it when the gallery is complete)
    public const int Fig55 = 0x55;     // register 0xB1, bit 5
    public const int Last = 0x85;      // register 0x80, bit 5

    public static uint Reg(int eventByte) => EventFlagCatalog.EventBitfieldAddress + (uint)eventByte;

    public static StoryFlags Made(params int[] figures)
    {
        var f = new StoryFlags();
        foreach (var n in figures) f.WithFigurine(n);
        return f;
    }

    public static StoryFlags Read(FakeDolphin d) =>
        StoryFlags.FromEventBytes(d.ReadMemory(EventFlagCatalog.EventBitfieldAddress, EventFlagCatalog.EventBitfieldSize)!);
}

/// <summary>The figurine half of <see cref="StoryFlags"/>: where the bits live, masking, merging, the wire.</summary>
public class FigurineFlagsTests
{
    [Fact]
    public void FigurineRegisters_AreTheCatalogsFigurineBitfields()
    {
        var regs = StoryFlags.FigurineRegisterBytes.ToArray();
        Assert.Equal(StoryFlags.FigurineByteCount, regs.Length);
        Assert.Equal(regs.Length, regs.Distinct().Count());

        // Every whole-byte OR-safe register in the catalog is a figurine bitfield, and nothing else is
        // (the warp pots are the only other BitwiseOr registers, 3 bits each).
        var bitfields = EventFlagCatalog.Registers
            .Where(r => r.Policy == EventRegisterPolicy.BitwiseOr && r.Mask == 0xFF)
            .Select(r => (byte)r.ByteIndex).OrderBy(b => b);
        Assert.Equal(bitfields, regs.OrderBy(b => b));
    }

    [Fact]
    public void FigurineMask_CoversTheGamesFigurines_AndNothingPast0x85()
    {
        var mask = StoryFlags.FigurineMask.ToArray();
        Assert.Equal(StoryFlags.TotalFigurines, mask.Sum(b => BitOperations.PopCount(b)));
        Assert.All(mask[..16], b => Assert.Equal(0xFF, b));
        Assert.Equal(0x3F, mask[16]); // figurines 0x80-0x85; bits 6-7 of register 0x80 are unused
    }

    [Fact]
    public void CarlovsInProgressFigurine_IsNeverShared_ButTheGalleryLatchesAre()
    {
        // 2F01 in progress, 3080 ready, 3F01 gallery state, 4080 handed over, 4040 room state (d_a_npc_mt.cpp)
        foreach (ushort id in new ushort[] { 0x2F01, 0x3080, 0x3F01, 0x4080, 0x4040 })
            Assert.Equal(0, StoryFlags.SyncMask[id >> 8] & (id & 0xFF));
        // 3A01 "a figurine was made" (setFigure) and 3D08 "gallery complete" (isComp) travel with the figurines.
        Assert.NotEqual(0, StoryFlags.SyncMask[0x3A] & 0x01);
        Assert.NotEqual(0, StoryFlags.SyncMask[0x3D] & 0x08);
        // Register A9 (the figurine being made) and 89 (picture count) are not figurine registers.
        Assert.DoesNotContain((byte)0xA9, StoryFlags.FigurineRegisterBytes.ToArray());
        Assert.DoesNotContain((byte)0x89, StoryFlags.FigurineRegisterBytes.ToArray());
    }

    [Fact]
    public void FromEventBytes_ReadsFigurinesFromTheirRegisters()
    {
        var raw = new byte[EventFlagCatalog.EventBitfieldSize];
        raw[0x8D] = 0x01;         // 0x40, the completion figurine
        raw[0x80] = 0xFF;         // 0x80-0x85, plus the two unused bits
        raw[0xA9] = Figurines.Fig12; // Carlov is making 0x12: not a made figurine
        raw[0x89] = 5;            // picture count
        var f = StoryFlags.FromEventBytes(raw);

        Assert.True(f.HasFigurine(Figurines.Carlov));
        Assert.True(f.HasFigurine(Figurines.Last));
        Assert.False(f.HasFigurine(Figurines.Fig12));
        Assert.Equal(7, f.FigurineCount);
        Assert.Equal(0x3F, f.Figurines[16]);
        Assert.Equal(0, f.BitCount);
    }

    [Fact]
    public void FromEventBytes_AllSet_IsEveryFigurine()
    {
        var raw = new byte[EventFlagCatalog.EventBitfieldSize];
        Array.Fill(raw, (byte)0xFF);
        var f = StoryFlags.FromEventBytes(raw);
        Assert.Equal(StoryFlags.TotalFigurines, f.FigurineCount);
        Assert.Equal(StoryFlags.FigurineMask.ToArray(), f.Figurines);
        Assert.Equal(Enumerable.Range(0, StoryFlags.TotalFigurines), f.MadeFigurines());
    }

    [Fact]
    public void FromEventBytes_NeedsTheWholeEventArray()
    {
        Assert.Throws<ArgumentException>(() => StoryFlags.FromEventBytes(new byte[StoryFlags.ByteCount]));
    }

    [Fact]
    public void Merge_Except_Normalize()
    {
        var a = Figurines.Made(Figurines.Fig12);
        Assert.True(a.MergeFrom(Figurines.Made(Figurines.Carlov)));
        Assert.False(a.MergeFrom(Figurines.Made(Figurines.Carlov)));
        Assert.Equal(2, a.FigurineCount);

        var d = a.Except(Figurines.Made(Figurines.Carlov));
        Assert.Equal(new[] { Figurines.Fig12 }, d.MadeFigurines());

        var bad = new StoryFlags();
        bad.Figurines[16] = 0xC0; // not figurines
        Assert.False(a.MergeFrom(bad));
        Assert.Equal(0, bad.Normalize().Figurines[16]);
    }

    [Fact]
    public void FlagsOnly_And_FigurinesOnly_SplitTheTwoHalves()
    {
        var f = Figurines.Made(Figurines.Fig55);
        f.Bits[0x3A] |= 0x01;
        Assert.Equal(0, f.FlagsOnly().FigurineCount);
        Assert.Equal(1, f.FlagsOnly().BitCount);
        Assert.Equal(0, f.FigurinesOnly().BitCount);
        Assert.Equal(1, f.FigurinesOnly().FigurineCount);
        Assert.False(Figurines.Made(Figurines.Fig55).IsEmpty);
    }

    [Fact]
    public void IsValid_NeedsTheFigurineArray()
    {
        Assert.False(new StoryFlags { Figurines = new byte[3] }.IsValid());
        Assert.False(new StoryFlags { Figurines = null! }.IsValid());
        Assert.True(Figurines.Made(Figurines.Last).IsValid());
    }

    [Fact]
    public void WithFigurine_RejectsNumbersPastTheLast()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StoryFlags().WithFigurine(StoryFlags.TotalFigurines));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StoryFlags().WithFigurine(-1));
        Assert.False(new StoryFlags().HasFigurine(StoryFlags.TotalFigurines));
    }

    [Fact]
    public void Describe_ListsTheFigurines()
    {
        var f = Figurines.Made(Figurines.Carlov, Figurines.Fig12);
        Assert.Equal("figurines 0x12, 0x40", f.Describe());
        Assert.Equal("0 flag(s) + 2 figurine(s)", f.CountText());
        f.Bits[0x0F] |= 0x80; // MET_KORL
        Assert.Equal("MET_KORL, figurines 0x12, 0x40", f.Describe());
        Assert.Equal("1 flag(s)", new StoryFlags { Bits = f.Bits }.CountText());
    }

    [Fact]
    public void Json_CarriesTheFigurines_AndNoComputedProperties()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); // SignalR's JSON protocol
        var f = Figurines.Made(Figurines.Carlov, Figurines.Last);
        var json = JsonSerializer.Serialize(f, options);
        Assert.DoesNotContain("isEmpty", json);
        Assert.DoesNotContain("bitCount", json);
        Assert.DoesNotContain("figurineCount", json);

        var back = JsonSerializer.Deserialize<StoryFlags>(json, options)!;
        Assert.True(back.IsValid());
        Assert.Equal(f.Figurines, back.Figurines);

        // A malformed payload still serializes (the hub is the one that refuses it).
        JsonSerializer.Serialize(new StoryFlags { Figurines = null! }, options);
    }
}

/// <summary>The server's room story keeps figurines like flags: the owner seeds, everyone merges up.</summary>
public class FigurineStoreTests
{
    [Fact]
    public void Figurines_OnlyEverMergeUp()
    {
        var store = new StoryFlagStore();
        var seed = store.Merge(Figurines.Made(Figurines.Fig12));
        Assert.True(seed.Seeded);
        Assert.Equal(new[] { Figurines.Fig12 }, seed.Added.MadeFigurines());

        var join = store.Merge(Figurines.Made(Figurines.Fig12, Figurines.Carlov));
        Assert.False(join.Seeded);
        Assert.Equal(new[] { Figurines.Carlov }, join.Added.MadeFigurines());
        Assert.Equal(new[] { Figurines.Fig12, Figurines.Carlov }, join.Room.MadeFigurines());

        var none = store.Merge(new StoryFlags()); // a player without them takes nothing away
        Assert.True(none.Added.IsEmpty);
        Assert.Equal(2, none.Room.FigurineCount);
    }
}

/// <summary>Writing room figurines into a game (FakeDolphin): the right registers, nothing else, only while idle.</summary>
public class FigurineApplyTests
{
    [Fact]
    public void ApplyBits_OrsFigurinesIntoTheirRegisters_AndLeavesCarlovsJobAlone()
    {
        var d = new FakeDolphin();
        d.Set(Figurines.Reg(0x93), 0x01);            // figurine 0x10 already made
        d.Set(Figurines.Reg(0x2F), 0x01);            // 2F01: Carlov is making one...
        d.Set(Figurines.Reg(0xA9), Figurines.Fig12); // ...figurine 0x12
        int writes = 0;
        d.BeforeWrite = _ => writes++;

        StorySyncService.ApplyBits(d, Figurines.Made(Figurines.Fig12, Figurines.Last));

        Assert.Equal(0x05, d.Get(Figurines.Reg(0x93)));
        Assert.Equal(0x20, d.Get(Figurines.Reg(0x80)));
        Assert.Equal(2, writes);
        Assert.Equal(0x01, d.Get(Figurines.Reg(0x2F)));            // the job is still his
        Assert.Equal(Figurines.Fig12, d.Get(Figurines.Reg(0xA9)));
        Assert.Equal(new[] { 0x10, Figurines.Fig12, Figurines.Last }, Figurines.Read(d).MadeFigurines());

        // When he finishes, setFigure(0x12) ORs a bit that is already there: nothing to lose.
        d.Set(Figurines.Reg(0x93), (byte)(d.Get(Figurines.Reg(0x93)) | 1 << (Figurines.Fig12 % 8)));
        Assert.Equal(3, Figurines.Read(d).FigurineCount);
    }

    [Fact]
    public void ApplyBits_WritesFlagsAndFigurines_AndSkipsWhatIsAlreadySet()
    {
        var d = new FakeDolphin();
        var room = Figurines.Made(Figurines.Carlov);
        room.Bits[0x3A] |= 0x01; // 3A01: a figurine was made
        StorySyncService.ApplyBits(d, room);
        Assert.Equal(0x01, d.Get(Figurines.Reg(0x3A)));
        Assert.Equal(0x01, d.Get(Figurines.Reg(0x8D)));

        int writes = 0;
        d.BeforeWrite = _ => writes++;
        StorySyncService.ApplyBits(d, room);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void ApplyBits_NeverWritesUnusedFigurineBits()
    {
        var d = new FakeDolphin();
        var bad = new StoryFlags();
        bad.Figurines[16] = 0xC0;
        StorySyncService.ApplyBits(d, bad);
        Assert.Equal(0, d.Get(Figurines.Reg(0x80)));
    }

    [Fact]
    public void ToApply_HoldsFigurinesUntilTheGameIsIdle()
    {
        var room = Figurines.Made(Figurines.Fig12, Figurines.Carlov);
        room.Bits[0x3A] |= 0x01;
        var local = Figurines.Made(Figurines.Carlov);
        var applied = new StoryFlags();

        var busy = StorySyncService.ToApply(room, local, applied, () => false);
        Assert.Equal(0, busy.FigurineCount);
        Assert.True(busy.Has(0x3A01)); // flags don't wait

        var idle = StorySyncService.ToApply(room, local, applied, () => true);
        Assert.Equal(new[] { Figurines.Fig12 }, idle.MadeFigurines());
        Assert.True(idle.Has(0x3A01));

        // Already applied once (the game may have cleared it since): never re-forced.
        Assert.True(StorySyncService.ToApply(room, local, room, () => true).IsEmpty);
    }

    [Fact]
    public void IsIdle_NeedsNoEventAndAClosedMenu()
    {
        var d = new FakeDolphin();
        Assert.True(SceneStabilityGate.IsIdle(d));
        d.Set(GameMemoryAddresses.Events.EventMode, 1); // talking to Carlov
        Assert.False(SceneStabilityGate.IsIdle(d));
        d.Set(GameMemoryAddresses.Events.EventMode, 0);
        d.Set(GameMemoryAddresses.Events.MenuPause, 1);
        Assert.False(SceneStabilityGate.IsIdle(d));
    }

    [Fact]
    public void StoryFlagsPage_MarksTheFigurineRegistersShared()
    {
        var fig = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.Id == 0x8DFF));
        Assert.True(fig.IsSharedFigurines);
        Assert.Contains("Shared story", fig.PolicyText);
        Assert.Contains("merges into the room", fig.Tooltip);

        var warpPot = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.Id == 0x9F07));
        Assert.False(warpPot.IsSharedFigurines);
        Assert.Contains("never synced", warpPot.Tooltip);

        var making = new EventRegisterRow(EventFlagCatalog.Registers.Single(r => r.Id == 0xA9FF));
        Assert.False(making.IsSharedFigurines);
    }
}

/// <summary>The hub carries figurines in the room story: merged up, pushed to the others, bad payloads refused.</summary>
[Collection("GameHub static state")]
public class SharedFigurinesHubTests
{
    private static async Task<StoryFlags?> NextStory(SignalRClientService client, Func<Task> send, int ms = 1500)
    {
        var got = new TaskCompletionSource<StoryFlags>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(StoryFlags f) => got.TrySetResult(f);
        client.StoryFlagsReceived += Handler;
        try
        {
            await send();
            return await Task.WhenAny(got.Task, Task.Delay(ms)) == got.Task ? got.Task.Result : null;
        }
        finally
        {
            client.StoryFlagsReceived -= Handler;
        }
    }

    private static async Task SetStoryRule(SignalRClientService owner, bool on)
    {
        var s = await owner.Connection!.InvokeAsync<RoomSettings>(HubConstants.GetRoomSettings);
        s.SharedStory = on;
        await owner.Connection!.InvokeAsync(HubConstants.SetRoomSettings, s);
    }

    [Fact]
    public async Task Figurines_MergeUpThroughTheRoomStory()
    {
        GameHub.ConfigureHostToken("figurines-key");
        GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        try
        {
            var (app, port) = await SharedBaitHubTests.StartAsync();
            await using var _ = app;
            await using var owner = new SignalRClientService();
            Assert.True((await owner.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Owner"), "figurines-key")).success);
            await using var player = new SignalRClientService();
            Assert.True((await player.ConnectAsync("127.0.0.1", port, SharedBaitHubTests.Name("Player"))).success);

            Assert.Null(await owner.JoinRoomStoryAsync(new StoryFlags { Figurines = new byte[3] })); // malformed

            var ownerRoom = await owner.JoinRoomStoryAsync(Figurines.Made(Figurines.Fig12));
            Assert.NotNull(ownerRoom);
            Assert.True(ownerRoom!.HasFigurine(Figurines.Fig12));

            // A joiner brings figurine 0x40: it gets the room back, the owner gets it pushed.
            StoryFlags? playerRoom = null;
            var pushed = await NextStory(owner, async () => playerRoom = await player.JoinRoomStoryAsync(Figurines.Made(Figurines.Carlov)));
            Assert.NotNull(playerRoom);
            Assert.True(playerRoom!.HasFigurine(Figurines.Fig12) && playerRoom.HasFigurine(Figurines.Carlov));
            Assert.NotNull(pushed);
            Assert.True(pushed!.HasFigurine(Figurines.Carlov));

            // The player gets a new one made; the unused bits never reach the room.
            var made = Figurines.Made(Figurines.Fig55);
            made.Figurines[16] |= 0xC0;
            var got = await NextStory(owner, () => player.SendStoryFlagsAsync(made));
            Assert.NotNull(got);
            Assert.True(got!.HasFigurine(Figurines.Fig55));
            Assert.Equal(0, got.Figurines[16] & 0xC0);

            // Nothing new: no push.
            Assert.Null(await NextStory(owner, () => player.SendStoryFlagsAsync(Figurines.Made(Figurines.Fig55)), 400));

            // Rule off: the room story is closed.
            await SetStoryRule(owner, false);
            Assert.Null(await player.JoinRoomStoryAsync(Figurines.Made(Figurines.Last)));
            Assert.Null(await NextStory(owner, () => player.SendStoryFlagsAsync(Figurines.Made(Figurines.Last)), 400));
        }
        finally
        {
            GameHub.ConfigureHostToken(null);
            GameHub.ConfigureRoomDefaults(true, true, true, true, sharedProjectiles: true, sharedBait: true, sharedSpoils: true);
        }
    }
}
