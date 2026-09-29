using WWOnline.Data;
using WWOnline.Services;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Live world (puppet_liveworld.c): which bits the client hands the REL, the batch / acknowledgement
/// protocol on the REL's game-heap block, and when a worker puppet is needed for it.
/// </summary>
public class LiveWorldPokeTests
{
    private const uint Block = 0x81234500;
    private const int Slot = 3;
    private static readonly byte[] BootTime = [0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC];
    private static readonly uint[] AllLive = Enumerable.Repeat(0xFFFFFFFFu, LiveWorldPoke.Words).ToArray();

    private static FakeDolphin GameWithBlock(uint block = Block, byte[]? boot = null)
    {
        var game = new FakeDolphin();
        game.Set(GameMemoryAddresses.System.OSStartTime.Address, BootTime);
        game.SetU32(PuppetLayout.LIVEWORLD_PTR_ADDR, block);
        game.SetU32(block + PuppetLayout.LIVEWORLD_OFF_MAGIC, (uint)PuppetLayout.LIVEWORLD_MAGIC);
        game.Set(block + PuppetLayout.LIVEWORLD_OFF_BOOT, boot ?? BootTime);
        return game;
    }

    private static uint Seq(FakeDolphin g, uint block = Block) => g.GetU32(block + PuppetLayout.LIVEWORLD_OFF_SEQ);
    private static uint Bits(FakeDolphin g, int word, uint block = Block) =>
        g.GetU32(block + (uint)(PuppetLayout.LIVEWORLD_OFF_BITS + word * 4));

    /// <summary>What the REL does after a pass: DONE_SEQ = SEQ.</summary>
    private static void RelAcknowledges(FakeDolphin g, uint pokes = 0, uint block = Block)
    {
        g.SetU32(block + PuppetLayout.LIVEWORLD_OFF_DONE_SEQ, Seq(g, block));
        g.SetU32(block + PuppetLayout.LIVEWORLD_OFF_POKE_COUNT, pokes);
    }

    private static LiveWorldPoke.TickResult? Tick(LiveWorldPoke poke, FakeDolphin game, uint[] live)
    {
        poke.SetLive(0, live);
        return poke.Tick(game);
    }

    private static uint[] Chest(int n) { var w = new uint[LiveWorldPoke.Words]; w[0] = 1u << n; return w; }

    private static uint[] Switch(int n)
    {
        var w = new uint[LiveWorldPoke.Words];
        w[LiveWorldPoke.SwitchWord(n)] = 1u << (n & 31);
        return w;
    }

    [Theory]
    [InlineData(0x00, 1)]
    [InlineData(0x7F, 4)]
    [InlineData(0x80, 5)]
    [InlineData(0xBF, 6)]
    [InlineData(0xC0, 7)]
    [InlineData(0xE0, 8)]
    [InlineData(0xEF, 8)]
    public void SwitchWord_MapsEverySwitchSpace(int switchNo, int word) =>
        Assert.Equal(word, LiveWorldPoke.SwitchWord(switchNo));

    [Fact]
    public void FromMemBit_PutsChestsInWordZero_AndMemorySwitchesAfter()
    {
        var w = LiveWorldPoke.FromMemBit(0x10, [1, 2, 3, 4]);
        Assert.Equal(new uint[] { 0x10, 1, 2, 3, 4, 0, 0, 0, 0 }, w);
    }

    [Fact]
    public void Publishes_OnlyRemoteBitsTheLiveCopyHas_UnderTheSeqlock()
    {
        var game = GameWithBlock();
        var poke = new LiveWorldPoke();
        poke.SetStage(Slot);
        var remote = Chest(4);
        remote[LiveWorldPoke.SwitchWord(0x21)] |= 1u << 1;
        poke.AddRemote(Slot, remote);

        var seqWrites = new List<uint>();
        game.BeforeWrite = a => { if (a == Block + PuppetLayout.LIVEWORLD_OFF_SEQ) seqWrites.Add(Seq(game)); };

        // The live copy only has the chest so far (the switch write hasn't been read back yet).
        var live = new uint[LiveWorldPoke.Words];
        live[0] = 1u << 4;
        var r = Tick(poke, game, live);

        Assert.NotNull(r!.Value.Published);
        Assert.Equal(1u << 4, Bits(game, 0));
        Assert.Equal(0u, Bits(game, LiveWorldPoke.SwitchWord(0x21)));
        Assert.Equal((uint)PuppetLayout.LIVEWORLD_TAG_MAGIC | Slot, game.GetU32(Block + PuppetLayout.LIVEWORLD_OFF_TAG));
        Assert.Equal((uint)PuppetLayout.LIVEWORLD_ZONE_ROOM_NONE, game.GetU32(Block + PuppetLayout.LIVEWORLD_OFF_ZONE_ROOM));
        // SEQ went odd before the batch and even after it.
        Assert.Equal(2u, Seq(game));
        Assert.Equal(2, seqWrites.Count);
        Assert.Equal(2u, r.Value.Published!.Seq);
        Assert.Equal((uint)PuppetLayout.LIVEWORLD_MAGIC, game.GetU32(Block)); // never touched
        Assert.Equal(BootTime, game.ReadMemory(Block + PuppetLayout.LIVEWORLD_OFF_BOOT, 8));
    }

    [Fact]
    public void WaitsForTheAcknowledgement_ThenSendsOnlyNewBits()
    {
        var game = GameWithBlock();
        var poke = new LiveWorldPoke();
        poke.SetStage(Slot);
        poke.AddRemote(Slot, Chest(1));
        Tick(poke, game, AllLive);
        uint first = Seq(game);

        // More bits while the REL hasn't handled the first batch: nothing is written.
        poke.AddRemote(Slot, Chest(2));
        int writes = 0;
        game.BeforeWrite = _ => writes++;
        var waiting = Tick(poke, game, AllLive);
        Assert.Null(waiting!.Value.Published);
        Assert.Equal(0, writes);

        RelAcknowledges(game, pokes: 1);
        game.BeforeWrite = null;
        var next = Tick(poke, game, AllLive);
        Assert.Equal(first, next!.Value.Acknowledged!.Seq);
        Assert.Equal(1u, next.Value.PokeCount);
        Assert.Equal(1u << 2, Bits(game, 0)); // only the new chest: chest 1 was handled
        Assert.True(Seq(game) > first);

        // Nothing new: once acknowledged, the block is left alone.
        RelAcknowledges(game, pokes: 2);
        Tick(poke, game, AllLive);
        writes = 0;
        game.BeforeWrite = _ => writes++;
        Assert.Null(Tick(poke, game, AllLive)!.Value.Published);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void BitsOfAnotherStage_AreIgnored_AndANewStageStartsEmpty()
    {
        var game = GameWithBlock();
        var poke = new LiveWorldPoke();
        poke.SetStage(Slot);
        poke.AddRemote(Slot + 1, Chest(1)); // world sync applied it to a saved slot, not the current stage
        Assert.Null(Tick(poke, game, AllLive)!.Value.Published);

        poke.AddRemote(Slot, Chest(5));
        Tick(poke, game, AllLive); // published, not acknowledged yet
        poke.SetStage(Slot + 1);

        // The REL won't act on the old stage's batch: an empty batch for this stage lets it acknowledge.
        var r = Tick(poke, game, AllLive);
        Assert.NotNull(r!.Value.Published);
        Assert.True(r.Value.Published!.IsEmpty);
        Assert.Equal((uint)PuppetLayout.LIVEWORLD_TAG_MAGIC | (Slot + 1), game.GetU32(Block + PuppetLayout.LIVEWORLD_OFF_TAG));
        Assert.Equal(0u, Bits(game, 0));
    }

    [Fact]
    public void ANewBlock_GetsEveryBitAgain()
    {
        var game = GameWithBlock();
        var poke = new LiveWorldPoke();
        poke.SetStage(Slot);
        poke.AddRemote(Slot, Switch(0x10));
        Tick(poke, game, AllLive);
        RelAcknowledges(game);
        Tick(poke, game, AllLive);

        // Same stage, but a block from a new boot (soft reset) at another address.
        const uint other = Block + 0x100;
        byte[] newBoot = [0x00, 0x00, 0x12, 0x35, 0, 0, 0, 1];
        var game2 = GameWithBlock(other, newBoot);
        game2.Set(GameMemoryAddresses.System.OSStartTime.Address, newBoot);
        var r = Tick(poke, game2, AllLive);
        Assert.False(r!.Value.Published!.IsEmpty);
        Assert.Equal(1u << 0x10, Bits(game2, LiveWorldPoke.SwitchWord(0x10), other));
    }

    [Fact]
    public void NoUsableBlock_WritesNothing()
    {
        var poke = new LiveWorldPoke();
        poke.SetStage(Slot);
        poke.AddRemote(Slot, Chest(0));

        var none = new FakeDolphin();
        var stale = GameWithBlock();
        stale.Set(GameMemoryAddresses.System.OSStartTime.Address, [0, 0, 0x12, 0x35, 0, 0, 0, 1]); // block from an earlier boot
        int writes = 0;
        none.BeforeWrite = _ => writes++;
        stale.BeforeWrite = _ => writes++;

        Assert.Null(Tick(poke, none, AllLive));
        Assert.Null(Tick(poke, stale, AllLive));
        Assert.Equal(0, writes);
    }

    [Fact]
    public void ZoneRoomChange_DropsTheZoneBits_AndIsPublishedWithThem()
    {
        var game = GameWithBlock();
        var poke = new LiveWorldPoke();
        poke.SetStage(Slot);
        poke.SetZoneRoom(7);
        poke.AddRemote(Slot, Switch(0xE0));
        Tick(poke, game, AllLive);
        Assert.Equal(7u, game.GetU32(Block + PuppetLayout.LIVEWORLD_OFF_ZONE_ROOM));
        Assert.Equal(1u, Bits(game, 8));
        RelAcknowledges(game);
        Tick(poke, game, AllLive);

        poke.SetZoneRoom(8);
        poke.AddRemote(Slot, Switch(0x05));
        Tick(poke, game, AllLive);
        Assert.Equal(8u, game.GetU32(Block + PuppetLayout.LIVEWORLD_OFF_ZONE_ROOM));
        Assert.Equal(0u, Bits(game, 8));
        Assert.Equal(1u << 5, Bits(game, 1));
    }

    [Fact]
    public void IsNeeded_WhileBitsWaitOrTheRelHoldsABatch()
    {
        var game = GameWithBlock();
        var poke = new LiveWorldPoke();
        Assert.False(poke.IsNeeded(game)); // no stage
        poke.SetStage(Slot);
        Assert.False(poke.IsNeeded(game)); // nothing to do

        poke.AddRemote(Slot, Chest(3));
        Assert.False(poke.IsNeeded(game)); // not read back from the live copy yet (the game may have cleared it)
        poke.SetLive(0, AllLive);
        Assert.True(poke.IsNeeded(new FakeDolphin())); // no block yet: a puppet makes the REL create one
        Assert.True(poke.IsNeeded(game));

        Tick(poke, game, AllLive);
        Assert.True(poke.IsNeeded(game)); // published, not handled
        RelAcknowledges(game);
        Tick(poke, game, AllLive);
        Assert.False(poke.IsNeeded(game));
    }

    [Fact]
    public void IsNeeded_GivesUpWithoutProgress_UntilTheNextChange()
    {
        var now = new DateTime(2026, 1, 1);
        var game = GameWithBlock();
        var poke = new LiveWorldPoke(() => now);
        poke.SetStage(Slot);
        poke.AddRemote(Slot, Chest(3));
        Tick(poke, game, AllLive);

        Assert.True(poke.IsNeeded(game));
        now += LiveWorldPoke.GiveUpAfter;
        Assert.False(poke.IsNeeded(game)); // the REL never acknowledged: stop keeping a puppet for it

        poke.AddRemote(Slot, Chest(4)); // a change re-arms it
        Assert.True(poke.IsNeeded(game));
    }

    [Fact]
    public void IsNeeded_ReArmsAfterAWhile_AndDoesntCountTimeInAnEventOrTheMenu()
    {
        var now = new DateTime(2026, 1, 1);
        var game = GameWithBlock();
        var poke = new LiveWorldPoke(() => now);
        poke.SetStage(Slot);
        poke.AddRemote(Slot, Chest(3));
        Tick(poke, game, AllLive);
        Assert.True(poke.IsNeeded(game));

        // A long cutscene: the REL waits on purpose, the clock doesn't run.
        game.Set(GameMemoryAddresses.Events.EventMode, 1);
        now += LiveWorldPoke.GiveUpAfter * 3;
        Assert.True(poke.IsNeeded(game));
        game.Set(GameMemoryAddresses.Events.EventMode, 0);
        game.Set(GameMemoryAddresses.Events.MenuPause, 1);
        now += LiveWorldPoke.GiveUpAfter * 3;
        Assert.True(poke.IsNeeded(game));
        game.Set(GameMemoryAddresses.Events.MenuPause, 0);

        now += LiveWorldPoke.GiveUpAfter / 2;
        Assert.True(poke.IsNeeded(game));  // the clock starts again from the last wait
        now += LiveWorldPoke.GiveUpAfter;
        Assert.False(poke.IsNeeded(game)); // no progress while nothing forbade it
        now += LiveWorldPoke.ReArmAfter;
        Assert.True(poke.IsNeeded(game));  // asks again later
    }

    [Fact]
    public void ABatchTheRelLeftUndone_IsPublishedAgain_ThenGivenUp()
    {
        var game = GameWithBlock();
        var poke = new LiveWorldPoke();
        poke.SetStage(Slot);
        poke.AddRemote(Slot, Chest(6));
        Tick(poke, game, AllLive);

        for (int i = 0; i < LiveWorldPoke.MaxRetries; i++)
        {
            game.SetU32(Block + PuppetLayout.LIVEWORLD_OFF_RETRY, 1);
            uint before = Seq(game);
            RelAcknowledges(game);
            var again = Tick(poke, game, AllLive);
            Assert.Equal(1u << 6, again!.Value.Published!.Bits[0]); // the same bits again
            Assert.True(Seq(game) > before);
        }
        RelAcknowledges(game); // still RETRY: given up, handled
        Assert.Null(Tick(poke, game, AllLive)!.Value.Published);
        Assert.False(poke.IsNeeded(game));
    }
}
