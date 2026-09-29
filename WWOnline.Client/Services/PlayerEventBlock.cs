using System.Buffers.Binary;
using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The puppet REL's projectile events block (GameMod puppet_fx.c; layout in puppet_shared.h
/// PUPPET_FX_*). The REL allocates it on the game heap the first time a puppet is created, publishes
/// its address at PUPPET_FX_PTR_ADDR and never frees it; like the names block
/// (<see cref="PuppetNameTags"/>) it may be used whenever the pointer is in MEM1, the magic matches and
/// its boot stamp is this boot's __OSStartTime.
///
/// Two rings of <see cref="PuppetLayout.PUPPET_FX_EVENT_SIZE"/>-byte events: the OUTBOX (the REL reports
/// the local Link's projectiles; the client reads the events after OUT_READ and acks by writing
/// OUT_READ) and the INBOX (the client writes peers' events after IN_WRITE while there is room, the REL
/// consumes them and advances IN_READ). Event i of a ring lives at entry i % count; sequence numbers
/// are u32 and wrap.
/// </summary>
public static class PlayerEventBlock
{
    private const uint InCount = PuppetLayout.PUPPET_FX_IN_COUNT;
    private const uint OutCount = PuppetLayout.PUPPET_FX_OUT_COUNT;
    private const uint EventSize = PuppetLayout.PUPPET_FX_EVENT_SIZE;
    private const uint SendFlag = PuppetLayout.PUPPET_FX_FLAG_SEND;

    /// <summary>One event as the REL stores it (no stage / room / sender: those are the client's).</summary>
    /// <remarks><see cref="Room"/>: outbox, the projectile's own room (current.roomNo) when it happened, -1 when
    /// unknown; inbox, 0.</remarks>
    public readonly record struct Raw(byte Kind, byte Slot, byte Variant, uint Id, float X, float Y, float Z,
                                      float SpeedF, float SpeedY, float Gravity, short AngleX, short AngleY, short AngleZ, short Timer,
                                      sbyte Room = 0);

    /// <summary>Is <paramref name="address"/> a plausible events block (whole block in MEM1, word aligned)?</summary>
    public static bool IsValidBlockAddress(uint address) =>
        BootStampedBlock.IsValidAddress(address, PuppetLayout.PUPPET_FX_BLOCK_SIZE);

    /// <summary>
    /// The events block's address, or null when there is no usable one: no puppet created since boot,
    /// a bad pointer or magic, or a block from before a soft reset (someone else's memory now).
    /// </summary>
    public static uint? Locate(IDolphinService dolphin) =>
        BootStampedBlock.Read(dolphin, PuppetLayout.PUPPET_FX_PTR_ADDR, PuppetLayout.PUPPET_FX_MAGIC,
                              PuppetLayout.PUPPET_FX_BLOCK_SIZE)?.Address;

    public static uint ReadWord(IDolphinService dolphin, uint block, int offset)
    {
        var b = dolphin.ReadMemory(block + (uint)offset, 4);
        return b is { Length: 4 } ? BinaryPrimitives.ReadUInt32BigEndian(b) : 0u;
    }

    public static void WriteWord(IDolphinService dolphin, uint block, int offset, uint value)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, value);
        dolphin.WriteMemory(block + (uint)offset, b);
    }

    /// <summary>Set or clear PUPPET_FX_FLAG_SEND (the REL reports the local Link's projectiles only while it is set).</summary>
    public static void SetSend(IDolphinService dolphin, uint block, bool send)
    {
        uint flags = ReadWord(dolphin, block, PuppetLayout.PUPPET_FX_OFF_FLAGS);
        uint want = send ? flags | SendFlag : flags & ~SendFlag;
        if (want != flags)
            WriteWord(dolphin, block, PuppetLayout.PUPPET_FX_OFF_FLAGS, want);
    }

    /// <summary>
    /// Read the outbox events after <paramref name="cursor"/> (the last one read) and ack them (OUT_READ).
    /// A cursor more than a ring behind (a stale client) skips to the ring's oldest event.
    /// </summary>
    public static List<Raw> ReadOutbox(IDolphinService dolphin, uint block, ref uint cursor)
    {
        var result = new List<Raw>();
        uint write = ReadWord(dolphin, block, PuppetLayout.PUPPET_FX_OFF_OUT_WRITE);
        if (write == cursor)
            return result;
        if (write - cursor > OutCount)
            cursor = write - OutCount;
        var ring = dolphin.ReadMemory(block + PuppetLayout.PUPPET_FX_OFF_OUT_RING, (int)(OutCount * EventSize));
        if (ring == null || ring.Length != OutCount * EventSize)
            return result;
        while (cursor != write)
        {
            cursor++;
            int entry = (int)(cursor % OutCount * EventSize);
            result.Add(Decode(ring.AsSpan(entry, (int)EventSize)));
        }
        WriteWord(dolphin, block, PuppetLayout.PUPPET_FX_OFF_OUT_READ, cursor);
        return result;
    }

    /// <summary>
    /// Write as many of <paramref name="events"/> into the inbox as it has room for (the REL empties it
    /// every frame), each entry before IN_WRITE moves past it. Returns how many were written.
    /// </summary>
    public static int WriteInbox(IDolphinService dolphin, uint block, IReadOnlyList<Raw> events)
    {
        if (events.Count == 0)
            return 0;
        uint write = ReadWord(dolphin, block, PuppetLayout.PUPPET_FX_OFF_IN_WRITE);
        uint read = ReadWord(dolphin, block, PuppetLayout.PUPPET_FX_OFF_IN_READ);
        uint used = write - read;
        int room = used >= InCount ? 0 : (int)(InCount - used);
        int n = Math.Min(room, events.Count);
        for (int i = 0; i < n; i++)
        {
            uint seq = write + 1 + (uint)i;
            uint entry = block + PuppetLayout.PUPPET_FX_OFF_IN_RING + seq % InCount * EventSize;
            dolphin.WriteMemory(entry, Encode(events[i]));
        }
        if (n > 0)
            WriteWord(dolphin, block, PuppetLayout.PUPPET_FX_OFF_IN_WRITE, write + (uint)n);
        return n;
    }

    public static Raw Decode(ReadOnlySpan<byte> e) => new(
        e[PuppetLayout.PUPPET_FX_EV_OFF_KIND],
        e[PuppetLayout.PUPPET_FX_EV_OFF_SLOT],
        e[PuppetLayout.PUPPET_FX_EV_OFF_VARIANT],
        BinaryPrimitives.ReadUInt32BigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_ID..]),
        BinaryPrimitives.ReadSingleBigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_POSX..]),
        BinaryPrimitives.ReadSingleBigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_POSY..]),
        BinaryPrimitives.ReadSingleBigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_POSZ..]),
        BinaryPrimitives.ReadSingleBigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_SPEED_F..]),
        BinaryPrimitives.ReadSingleBigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_SPEED_Y..]),
        BinaryPrimitives.ReadSingleBigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_GRAVITY..]),
        BinaryPrimitives.ReadInt16BigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_ANGLE_X..]),
        BinaryPrimitives.ReadInt16BigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_ANGLE_Y..]),
        BinaryPrimitives.ReadInt16BigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_ANGLE_Z..]),
        BinaryPrimitives.ReadInt16BigEndian(e[PuppetLayout.PUPPET_FX_EV_OFF_TIMER..]),
        (sbyte)e[PuppetLayout.PUPPET_FX_EV_OFF_ROOM]);

    public static byte[] Encode(Raw r)
    {
        var e = new byte[PuppetLayout.PUPPET_FX_EVENT_SIZE];
        e[PuppetLayout.PUPPET_FX_EV_OFF_KIND] = r.Kind;
        e[PuppetLayout.PUPPET_FX_EV_OFF_SLOT] = r.Slot;
        e[PuppetLayout.PUPPET_FX_EV_OFF_VARIANT] = r.Variant;
        e[PuppetLayout.PUPPET_FX_EV_OFF_ROOM] = (byte)r.Room;
        BinaryPrimitives.WriteUInt32BigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_ID), r.Id);
        BinaryPrimitives.WriteSingleBigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_POSX), r.X);
        BinaryPrimitives.WriteSingleBigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_POSY), r.Y);
        BinaryPrimitives.WriteSingleBigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_POSZ), r.Z);
        BinaryPrimitives.WriteSingleBigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_SPEED_F), r.SpeedF);
        BinaryPrimitives.WriteSingleBigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_SPEED_Y), r.SpeedY);
        BinaryPrimitives.WriteSingleBigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_GRAVITY), r.Gravity);
        BinaryPrimitives.WriteInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_ANGLE_X), r.AngleX);
        BinaryPrimitives.WriteInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_ANGLE_Y), r.AngleY);
        BinaryPrimitives.WriteInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_ANGLE_Z), r.AngleZ);
        BinaryPrimitives.WriteInt16BigEndian(e.AsSpan(PuppetLayout.PUPPET_FX_EV_OFF_TIMER), r.Timer);
        return e;
    }

    /// <summary>A local outbox event as a wire event (the caller adds origin, seq and stage). Its room is the
    /// projectile's own (a bomb explodes in the room it was thrown into, wherever its thrower is by the time
    /// the event is sent), or <paramref name="localRoom"/> when the REL didn't know it.</summary>
    public static PlayerEvent ToEvent(Raw r, byte localRoom) => new()
    {
        RoomNumber = r.Room >= 0 ? (byte)r.Room : localRoom,
        Kind = r.Kind,
        Id = r.Id,
        Variant = r.Variant,
        Position = new Vector3(r.X, r.Y, r.Z),
        SpeedF = r.SpeedF,
        SpeedY = r.SpeedY,
        Gravity = r.Gravity,
        AngleX = r.AngleX,
        AngleY = r.AngleY,
        AngleZ = r.AngleZ,
        Timer = r.Timer,
    };

    /// <summary>A peer's (validated) event for the inbox, addressed to their puppet slot.</summary>
    public static Raw FromEvent(PlayerEvent e, int slot) => new(
        e.Kind, (byte)slot, e.Variant, e.Id, e.Position.X, e.Position.Y, e.Position.Z,
        e.SpeedF, e.SpeedY, e.Gravity, e.AngleX, e.AngleY, e.AngleZ, e.Timer, 0);
}
