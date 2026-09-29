using Serilog;
using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// Live world: hands the puppet REL the bits OTHER players set in the current stage, so the actors
/// that read their flag only when they are created catch up while you are in the room
/// (GameMod/src/puppet_link/puppet_liveworld.c): a chest another player opened opens (empty) instead
/// of giving its item again, bombable walls / breakable floors / ice blocks / barricades vanish,
/// crystal switches show on and small-key locks clear. Objects that poll their switch need nothing:
/// the world sync's write to the live save data is enough.
///
/// Bits live in one word array, <see cref="Words"/> long: word 0 = chests (dSv_memBit_c::mTbox),
/// words 1..8 = switch n in word <see cref="SwitchWord"/>(n), bit n &amp; 31 (memory 0x00-0x7F, dan
/// 0x80-0xBF, zone 0xC0-0xEF of the zone room). <see cref="WorldFlagSyncService"/> records the bits it
/// applies from the room (<see cref="AddRemote"/>) and reports the game's live bits (<see cref="SetLive"/>):
/// only bits read back from the live save data are published or waited for, so the REL never acts on a
/// bit the game doesn't have.
///
/// Protocol (puppet_shared.h LIVEWORLD_*): the REL's game-heap block (boot-stamped, see
/// <see cref="BootStampedBlock"/>) carries one batch of NEW bits at a time. It is written under a
/// seqlock (SEQ odd while writing) and only when the REL has acknowledged the previous one
/// (DONE_SEQ == SEQ), so each bit is handled once per stage visit. The REL only runs while a puppet
/// exists, so <see cref="PuppetSyncService"/> asks for a parked one while <see cref="IsNeeded"/>. The REL
/// acknowledges a batch with RETRY set when it left actors undone (not ready in time, too many, a refused
/// delete): the batch is published again, up to <see cref="MaxRetries"/> times.
/// Thread-safe: <see cref="Tick"/> runs on the world-sync timer, <see cref="IsNeeded"/> on the puppet one.
/// </summary>
public sealed class LiveWorldPoke
{
    private static readonly ILogger Logger = Log.ForContext<LiveWorldPoke>();

    public const int Words = PuppetLayout.LIVEWORLD_BIT_WORDS;
    public const int ZoneRoomNone = PuppetLayout.LIVEWORLD_ZONE_ROOM_NONE;

    /// <summary>Stop asking for a worker puppet when the REL makes no progress for this long (not counting time
    /// in an event or the pause menu, when the REL waits on purpose), until the next change or <see cref="ReArmAfter"/>.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(10);

    /// <summary>After giving up, ask again this much later.</summary>
    public static readonly TimeSpan ReArmAfter = TimeSpan.FromSeconds(30);

    /// <summary>How many times a batch the REL couldn't finish is published again.</summary>
    public const int MaxRetries = 3;

    /// <summary>The word holding switch <paramref name="switchNo"/> (0x00-0xEF).</summary>
    public static int SwitchWord(int switchNo) => 1 + (switchNo >> 5);

    /// <summary>A batch handed to the REL.</summary>
    /// <param name="Seq">SEQ once written (even); the REL sets DONE_SEQ to it when done.</param>
    public sealed record Batch(uint Seq, int Slot, int ZoneRoom, uint[] Bits)
    {
        public bool IsEmpty => Bits.All(w => w == 0);
        public override string ToString() =>
            $"slot {Slot} tbox={Bits[0]:X8} sw={string.Join(",", Bits.Skip(1).Select(w => w.ToString("X8")))}" +
            (ZoneRoom == ZoneRoomNone ? "" : $" zone room {ZoneRoom}");
    }

    /// <summary>What one <see cref="Tick"/> did: the batch the REL just acknowledged (with how many actors
    /// it updated in total so far), and the batch just published.</summary>
    public readonly record struct TickResult(Batch? Acknowledged, uint PokeCount, Batch? Published);

    private readonly object _lock = new();
    private readonly Func<DateTime> _clock;

    // Guarded by _lock.
    private int? _slot;
    private int _zoneRoom = ZoneRoomNone;
    private readonly uint[] _remote = new uint[Words];  // bits other players set in this stage visit
    private readonly uint[] _handled = new uint[Words]; // of those, the ones the REL acknowledged
    private readonly uint[] _live = new uint[Words];    // the game's live bits, as last reported
    private int _retries;                               // consecutive batches acknowledged with RETRY
    private Batch? _inFlight;
    private (uint Address, ulong Boot)? _block;         // the REL block _handled/_inFlight belong to
    private (uint Seq, uint Done, bool Block, int Fresh)? _progress;
    private DateTime _progressSince, _gaveUpAt;
    private bool _gaveUp, _requested;

    public LiveWorldPoke() : this(() => DateTime.UtcNow) { }

    public LiveWorldPoke(Func<DateTime> clock) => _clock = clock;

    /// <summary>The current stage's save slot (null = unknown). A new stage starts from nothing.</summary>
    public void SetStage(int? slot)
    {
        lock (_lock)
        {
            if (slot == _slot) return;
            _slot = slot;
            _zoneRoom = ZoneRoomNone;
            Array.Clear(_remote);
            Array.Clear(_handled);
            Array.Clear(_live);
            _retries = 0;
            _inFlight = null; // the REL won't act on a batch for another stage; the next Tick publishes one for this stage
            Changed();
        }
    }

    /// <summary>
    /// The room whose zone switches (0xC0-0xEF) are in words 7-8. A different room drops the zone bits:
    /// the game keeps zone switches per room.
    /// </summary>
    public void SetZoneRoom(int room)
    {
        lock (_lock)
        {
            if (room == _zoneRoom) return;
            _zoneRoom = room;
            int c0 = SwitchWord(0xC0), e0 = SwitchWord(0xE0);
            _remote[c0] = _remote[e0] = _handled[c0] = _handled[e0] = _live[c0] = _live[e0] = 0;
            Changed();
        }
    }

    /// <summary>Record bits applied from other players to <paramref name="slot"/>'s live save data (<see cref="Words"/> layout).</summary>
    public void AddRemote(int slot, IReadOnlyList<uint> words)
    {
        lock (_lock)
        {
            if (slot != _slot) return;
            bool added = false;
            for (int i = 0; i < Words && i < words.Count; i++)
            {
                added |= (words[i] & ~_remote[i]) != 0;
                _remote[i] |= words[i];
            }
            if (added) Changed();
        }
    }

    /// <summary>The game's live bits for words <paramref name="firstWord"/>.. (read back after the writes).</summary>
    public void SetLive(int firstWord, IReadOnlyList<uint> words)
    {
        lock (_lock)
        {
            for (int i = 0; i < words.Count && firstWord + i < Words; i++)
                _live[firstWord + i] = words[i];
        }
    }

    /// <summary>Chests and memory switches of a dSv_memBit_c as <see cref="Words"/> (dan and zone words empty).</summary>
    public static uint[] FromMemBit(uint tbox, IReadOnlyList<uint> memorySwitchWords)
    {
        var w = new uint[Words];
        w[0] = tbox;
        for (int i = 0; i < memorySwitchWords.Count && i < 4; i++)
            w[1 + i] = memorySwitchWords[i];
        return w;
    }

    /// <summary>
    /// Collect the REL's acknowledgement and publish the next batch: remote bits not handled yet that the
    /// game's live bits (<see cref="SetLive"/>) have. Also publishes an empty batch when the REL still holds
    /// one for another stage, so it can acknowledge. Null when there is no usable block yet (the REL
    /// allocates it when a puppet is created).
    /// </summary>
    public TickResult? Tick(IDolphinService dolphin)
    {
        lock (_lock)
        {
            if (Read(dolphin) is not { } b)
                return null;
            Adopt(b.Address, b.Boot);

            Batch? acknowledged = null;
            if (_inFlight is { } inFlight)
            {
                if (b.Done == inFlight.Seq)
                {
                    if (b.Retry != 0 && _retries < MaxRetries)
                    {
                        _retries++; // not handled: the same bits go out again below
                        Logger.Information("[world] live world: the REL left {N} actor(s) undone — publishing the batch again ({Try}/{Max})",
                            b.Retry, _retries, MaxRetries);
                    }
                    else
                    {
                        if (b.Retry != 0)
                            Logger.Warning("[world] live world: {N} actor(s) still undone after {Max} tries — leaving them to the next room load",
                                b.Retry, MaxRetries);
                        _retries = 0;
                        for (int i = 0; i < Words; i++) _handled[i] |= inFlight.Bits[i];
                    }
                    acknowledged = inFlight;
                    _inFlight = null;
                }
                else if (b.Seq == inFlight.Seq)
                    return new TickResult(null, b.PokeCount, null); // the REL hasn't got to it yet
                else
                    _inFlight = null; // overwritten under us: publish again
            }

            if (_slot is not int slot)
                return new TickResult(acknowledged, b.PokeCount, null);

            var fresh = new uint[Words];
            bool any = false;
            for (int i = 0; i < Words; i++)
            {
                fresh[i] = _remote[i] & ~_handled[i] & _live[i];
                any |= fresh[i] != 0;
            }
            bool relIdle = b.Seq == b.Done;
            if (!any && relIdle)
                return new TickResult(acknowledged, b.PokeCount, null);

            var published = Publish(dolphin, b.Address, b.Seq, slot, fresh);
            _inFlight = published;
            Changed();
            return new TickResult(acknowledged, b.PokeCount, published);
        }
    }

    /// <summary>
    /// Does the REL have work, so a (parked) puppet must exist for it to run? Yes while it holds a batch
    /// it hasn't acknowledged, or there are bits (still live) to hand it (and, with no block yet, so it
    /// creates one). Gives up after <see cref="GiveUpAfter"/> without progress, not counting time in an event
    /// or the pause menu (the REL waits for those on purpose), and asks again <see cref="ReArmAfter"/> later.
    /// </summary>
    public bool IsNeeded(IDolphinService dolphin)
    {
        lock (_lock)
        {
            bool need = false;
            var progress = default((uint, uint, bool, int));
            if (_slot != null)
            {
                int fresh = 0;
                for (int i = 0; i < Words; i++)
                    fresh += System.Numerics.BitOperations.PopCount(_remote[i] & ~_handled[i] & _live[i]);
                var b = Read(dolphin);
                need = b is { } blk ? blk.Seq != blk.Done || fresh > 0 : fresh > 0;
                progress = (b?.Seq ?? 0, b?.Done ?? 0, b != null, fresh);
            }

            var now = _clock();
            if (_gaveUp && now - _gaveUpAt >= ReArmAfter)
            {
                _gaveUp = false;
                _progress = null;
            }
            if (!need)
                _progress = null;
            else if (!_gaveUp)
            {
                if (_progress != progress || IsWaitingOnPurpose(dolphin))
                {
                    _progress = progress;
                    _progressSince = now;
                }
                else if (now - _progressSince >= GiveUpAfter)
                {
                    _gaveUp = true;
                    _gaveUpAt = now;
                    Logger.Warning("[world] live world: the REL made no progress in {Seconds:F0}s (seq {Seq}, done {Done}, block {Block}, {Fresh} bit(s) waiting) — giving up for {ReArm:F0}s or until the next change",
                        GiveUpAfter.TotalSeconds, progress.Item1, progress.Item2, progress.Item3, progress.Item4, ReArmAfter.TotalSeconds);
                }
            }

            bool result = need && !_gaveUp;
            if (result != _requested)
            {
                _requested = result;
                Logger.Information(result
                    ? "[world] live world: worker puppet requested (the REL has a batch to handle)"
                    : "[world] live world: worker puppet released");
            }
            return result;
        }
    }

    /// <summary>An event runs or the pause menu is open: the REL holds its batch on purpose (puppet_liveworld.c).</summary>
    private static bool IsWaitingOnPurpose(IDolphinService dolphin)
    {
        var evt = dolphin.ReadMemory(GameMemoryAddresses.Events.EventMode, 1);
        var menu = dolphin.ReadMemory(GameMemoryAddresses.Events.MenuPause, 1);
        return evt is [not 0] || menu is [not 0];
    }

    /// <summary>The REL block's state: address, boot stamp, SEQ, DONE_SEQ, POKE_COUNT and RETRY.</summary>
    public readonly record struct BlockState(uint Address, ulong Boot, uint Seq, uint Done, uint PokeCount, uint Retry = 0);

    /// <summary>Read the live-world block, or null when there is no usable one.</summary>
    public static BlockState? Read(IDolphinService dolphin)
    {
        if (BootStampedBlock.Read(dolphin, PuppetLayout.LIVEWORLD_PTR_ADDR, PuppetLayout.LIVEWORLD_MAGIC,
                PuppetLayout.LIVEWORLD_BLOCK_SIZE) is not { } found)
            return null;
        var (address, c) = found;
        ulong boot = (ulong)BootStampedBlock.ReadU32(c, PuppetLayout.LIVEWORLD_OFF_BOOT) << 32 |
                     BootStampedBlock.ReadU32(c, PuppetLayout.LIVEWORLD_OFF_BOOT + 4);
        return new BlockState(address, boot,
            BootStampedBlock.ReadU32(c, PuppetLayout.LIVEWORLD_OFF_SEQ),
            BootStampedBlock.ReadU32(c, PuppetLayout.LIVEWORLD_OFF_DONE_SEQ),
            BootStampedBlock.ReadU32(c, PuppetLayout.LIVEWORLD_OFF_POKE_COUNT),
            BootStampedBlock.ReadU32(c, PuppetLayout.LIVEWORLD_OFF_RETRY));
    }

    // A different block (first sight, or a new boot's): nothing it acknowledged is ours.
    private void Adopt(uint address, ulong boot)
    {
        if (_block == (address, boot)) return;
        _block = (address, boot);
        _inFlight = null;
        Array.Clear(_handled);
    }

    /// <summary>Write one batch under the seqlock: SEQ odd, TAG / ZONE_ROOM / BITS, SEQ even.</summary>
    private Batch Publish(IDolphinService dolphin, uint block, uint seq, int slot, uint[] bits)
    {
        uint writing = (seq + 1) | 1;
        uint done = writing + 1;
        dolphin.WriteMemory(block + PuppetLayout.LIVEWORLD_OFF_SEQ, BootStampedBlock.U32(writing));
        var payload = new byte[8 + Words * 4];
        BootStampedBlock.U32((uint)PuppetLayout.LIVEWORLD_TAG_MAGIC | (uint)slot).CopyTo(payload, 0);
        BootStampedBlock.U32((uint)_zoneRoom).CopyTo(payload, 4);
        for (int i = 0; i < Words; i++)
            BootStampedBlock.U32(bits[i]).CopyTo(payload, 8 + i * 4);
        dolphin.WriteMemory(block + PuppetLayout.LIVEWORLD_OFF_TAG, payload);
        dolphin.WriteMemory(block + PuppetLayout.LIVEWORLD_OFF_SEQ, BootStampedBlock.U32(done));
        return new Batch(done, slot, _zoneRoom, bits);
    }

    private void Changed()
    {
        _gaveUp = false;
        _progress = null;
    }
}
