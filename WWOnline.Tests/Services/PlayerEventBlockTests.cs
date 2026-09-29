using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The client side of the puppet REL's projectile events block (puppet_fx.c, puppet_shared.h
/// PUPPET_FX_*): finding a block of this boot, draining and acking the outbox, filling the inbox only
/// as far as the REL has consumed it, and the event encoding.
/// </summary>
public class PlayerEventBlockTests
{
    private const uint Block = 0x81234560;
    private static readonly byte[] Boot = [0x00, 0x00, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC];

    private static FakeDolphin WithBlock(uint block = Block, uint magic = PuppetLayout.PUPPET_FX_MAGIC, byte[]? stamp = null)
    {
        var d = new FakeDolphin();
        d.Set(GameMemoryAddresses.System.OSStartTime.Address, Boot);
        d.SetU32(PuppetLayout.PUPPET_FX_PTR_ADDR, block);
        d.SetU32(block + PuppetLayout.PUPPET_FX_OFF_MAGIC, magic);
        d.Set(block + PuppetLayout.PUPPET_FX_OFF_BOOT, stamp ?? Boot);
        return d;
    }

    private static PlayerEventBlock.Raw Raw(byte kind, uint id, byte slot = 0) =>
        new(kind, slot, 0, id, 1.5f, -2.25f, 3e5f, 19.5f, 14f, -2.9f, -0x100, 0x4000, 7, 131, 5);

    // Put a REL outbox event at seq and bump OUT_WRITE to it.
    private static void RelEmits(FakeDolphin d, uint seq, PlayerEventBlock.Raw r)
    {
        uint entry = Block + PuppetLayout.PUPPET_FX_OFF_OUT_RING +
                     seq % (uint)PuppetLayout.PUPPET_FX_OUT_COUNT * (uint)PuppetLayout.PUPPET_FX_EVENT_SIZE;
        d.Set(entry, PlayerEventBlock.Encode(r));
        d.SetU32(Block + PuppetLayout.PUPPET_FX_OFF_OUT_WRITE, seq);
    }

    [Fact]
    public void Layout_RingsFitTheBlock()
    {
        Assert.Equal(PuppetLayout.PUPPET_FX_OFF_OUT_RING,
            PuppetLayout.PUPPET_FX_OFF_IN_RING + PuppetLayout.PUPPET_FX_IN_COUNT * PuppetLayout.PUPPET_FX_EVENT_SIZE);
        Assert.Equal(PuppetLayout.PUPPET_FX_BLOCK_SIZE,
            PuppetLayout.PUPPET_FX_OFF_OUT_RING + PuppetLayout.PUPPET_FX_OUT_COUNT * PuppetLayout.PUPPET_FX_EVENT_SIZE);
        Assert.True(PuppetLayout.PUPPET_FX_EV_OFF_TIMER + 2 <= PuppetLayout.PUPPET_FX_EVENT_SIZE);
        // The pointer word is the one free scratch word between the names pointer and the boat blocks.
        Assert.Equal(PuppetLayout.PUPPET_NAMES_PTR_ADDR + 4, PuppetLayout.PUPPET_FX_PTR_ADDR);
        Assert.True(PuppetLayout.PUPPET_FX_PTR_ADDR + 4 <= PuppetLayout.PUPPET_BOAT_0);
    }

    [Fact]
    public void Locate_FindsABlockOfThisBoot()
    {
        Assert.Equal(Block, PlayerEventBlock.Locate(WithBlock()));
    }

    [Fact]
    public void Locate_RefusesAStaleOrBrokenBlock()
    {
        Assert.Null(PlayerEventBlock.Locate(WithBlock(magic: 0x4E414D45)));                     // not our magic
        Assert.Null(PlayerEventBlock.Locate(WithBlock(stamp: [0, 0, 0, 0, 0, 0, 0, 1])));      // an earlier boot's
        Assert.Null(PlayerEventBlock.Locate(WithBlock(block: 0x7FFF0000)));                     // not MEM1
        Assert.Null(PlayerEventBlock.Locate(WithBlock(block: PuppetNameTags.Mem1End - 0x10)));  // runs off MEM1
        Assert.Null(PlayerEventBlock.Locate(WithBlock(block: Block + 2)));                      // unaligned
        Assert.Null(PlayerEventBlock.Locate(new FakeDolphin()));                                // none yet
    }

    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        var r = Raw((byte)PlayerEventKind.Cannon, 0xDEADBEEF, slot: 2);
        var bytes = PlayerEventBlock.Encode(r);
        Assert.Equal(PuppetLayout.PUPPET_FX_EVENT_SIZE, bytes.Length);
        Assert.Equal(r, PlayerEventBlock.Decode(bytes));
        // Big-endian, at the REL's offsets.
        Assert.Equal(0xDE, bytes[PuppetLayout.PUPPET_FX_EV_OFF_ID]);
        Assert.Equal(0x40, bytes[PuppetLayout.PUPPET_FX_EV_OFF_ANGLE_Y]);
        Assert.Equal(2, bytes[PuppetLayout.PUPPET_FX_EV_OFF_SLOT]);
        Assert.Equal(5, bytes[PuppetLayout.PUPPET_FX_EV_OFF_ROOM]);
        Assert.Equal((sbyte)-1, PlayerEventBlock.Decode(PlayerEventBlock.Encode(r with { Room = -1 })).Room);
    }

    [Fact]
    public void ReadOutbox_ReadsNewEventsInOrder_AndAcks()
    {
        var d = WithBlock();
        RelEmits(d, 1, Raw(1, 100));
        RelEmits(d, 2, Raw(3, 100));
        uint cursor = 0;

        var events = PlayerEventBlock.ReadOutbox(d, Block, ref cursor);

        Assert.Equal([1, 3], events.Select(e => (int)e.Kind));
        Assert.Equal(2u, cursor);
        Assert.Equal(2u, d.GetU32(Block + PuppetLayout.PUPPET_FX_OFF_OUT_READ));
        Assert.Empty(PlayerEventBlock.ReadOutbox(d, Block, ref cursor)); // nothing new
    }

    [Fact]
    public void ReadOutbox_WrapsTheRingAndTheSequence()
    {
        var d = WithBlock();
        uint cursor = uint.MaxValue - 1;
        RelEmits(d, uint.MaxValue, Raw(1, 7));
        RelEmits(d, 0, Raw(3, 7));      // seq wraps to 0
        var events = PlayerEventBlock.ReadOutbox(d, Block, ref cursor);
        Assert.Equal([1, 3], events.Select(e => (int)e.Kind));
        Assert.Equal(0u, cursor);
    }

    [Fact]
    public void ReadOutbox_ACursorMoreThanARingBehind_SkipsToTheOldestKept()
    {
        var d = WithBlock();
        uint count = (uint)PuppetLayout.PUPPET_FX_OUT_COUNT;
        for (uint seq = 1; seq <= count + 3; seq++)
            RelEmits(d, seq, Raw(1, seq));
        uint cursor = 0;
        var events = PlayerEventBlock.ReadOutbox(d, Block, ref cursor);
        Assert.Equal((int)count, events.Count);
        Assert.Equal(4u, events[0].Id);
        Assert.Equal(count + 3, cursor);
    }

    [Fact]
    public void WriteInbox_WritesEntriesThenTheSeq_OnlyAsFarAsTheRelHasRead()
    {
        var d = WithBlock();
        uint count = (uint)PuppetLayout.PUPPET_FX_IN_COUNT;
        // The REL has consumed up to 5; we wrote up to 5 + count - 2 → room for 2.
        d.SetU32(Block + PuppetLayout.PUPPET_FX_OFF_IN_READ, 5);
        d.SetU32(Block + PuppetLayout.PUPPET_FX_OFF_IN_WRITE, 5 + count - 2);
        var order = new List<uint>();
        d.BeforeWrite = a => order.Add(a);

        int written = PlayerEventBlock.WriteInbox(d, Block, [Raw(1, 1, 0), Raw(1, 2, 1), Raw(1, 3, 2)]);

        Assert.Equal(2, written);
        uint seqWrite = Block + PuppetLayout.PUPPET_FX_OFF_IN_WRITE;
        Assert.Equal(5 + count, d.GetU32(seqWrite));
        Assert.Equal(seqWrite, order[^1]); // the seq moves last
        uint first = 5 + count - 1;
        var entry = d.ReadMemory(Block + PuppetLayout.PUPPET_FX_OFF_IN_RING +
                                 first % count * (uint)PuppetLayout.PUPPET_FX_EVENT_SIZE, PuppetLayout.PUPPET_FX_EVENT_SIZE)!;
        Assert.Equal(Raw(1, 1, 0), PlayerEventBlock.Decode(entry));

        // Full now: nothing more until the REL reads.
        Assert.Equal(0, PlayerEventBlock.WriteInbox(d, Block, [Raw(1, 3, 2)]));
    }

    [Fact]
    public void SetSend_TogglesOnlyTheSendFlag()
    {
        var d = WithBlock();
        d.SetU32(Block + PuppetLayout.PUPPET_FX_OFF_FLAGS, 0x80);
        PlayerEventBlock.SetSend(d, Block, true);
        Assert.Equal(0x80u | PuppetLayout.PUPPET_FX_FLAG_SEND, d.GetU32(Block + PuppetLayout.PUPPET_FX_OFF_FLAGS));
        PlayerEventBlock.SetSend(d, Block, false);
        Assert.Equal(0x80u, d.GetU32(Block + PuppetLayout.PUPPET_FX_OFF_FLAGS));
    }

    [Fact]
    public void EventConversions_KeepEveryField()
    {
        var raw = Raw((byte)PlayerEventKind.BombThrow, 99);
        var evt = PlayerEventBlock.ToEvent(raw, localRoom: 9);
        evt.StageName = "M_NewD2";
        evt.Origin = "abc123";
        Assert.True(evt.IsValid());
        Assert.Equal(5, evt.RoomNumber); // the projectile's own room, not ours
        Assert.Equal(raw with { Slot = 1, Room = 0 }, PlayerEventBlock.FromEvent(evt, 1));

        // The REL didn't know the room (-1): ours.
        Assert.Equal(9, PlayerEventBlock.ToEvent(raw with { Room = -1 }, localRoom: 9).RoomNumber);
    }
}
