using System.Collections.Concurrent;
using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared story ("room story"): the single-bit event flags in dSv_event_c bytes 0x00-0x41
/// (story milestones, cutscenes seen, side quests, collectible latches, NPC dialogue state,
/// tutorial hints) are OR-merged across players through the server, masked to
/// <see cref="StoryFlags.SyncMask"/> (the catalog's Full-sync mask: never LocalOnly flags, never
/// mTmp). So are the Nintendo Gallery figurines Carlov has made (<see cref="StoryFlags.Figurines"/>,
/// the 17 figurine bitfield registers; docs/figurines.md). No other register (0x79-0xFF) is touched.
///
///   - on attach this game JOINS: the room owner's game seeds the room; anyone else's flags merge
///     up into it, and the room's flags come back to apply;
///   - every tick (4Hz, only while the scene is stable), newly set local bits are sent, and room
///     bits this game is missing are ORed in with a per-byte read-modify-write.
/// Applying is edge-triggered per bit: a room bit is written at most once per loaded save, and a
/// bit this save has ever had (from the room or set by the game itself) is never re-forced. If the
/// game clears one later we leave it cleared
/// (the catalog excludes every flag the game is known to clear; this is the safety net).
/// Figurines are written only while the game is idle (<see cref="SceneStabilityGate.IsIdle"/>): the
/// game sets them itself only in Carlov's talk event, so a write never races daNpcMt_c::setFigure.
/// Carlov's in-progress figurine (flags 2F01 / 3080 / 3F01 / 4080 / 4040 and register A9FF) is
/// LocalOnly and never synced.
/// </summary>
public class StorySyncService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<StorySyncService>();

    private const int TickMs = 250;
    private const int StableTicksRequired = 4; // 1s of the same stage with Link present
    private static readonly TimeSpan JoinRetry = TimeSpan.FromSeconds(3);

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly RoomSettingsService _room;
    private readonly SceneStabilityGate _scene = new(StableTicksRequired);
    private readonly ConcurrentQueue<StoryFlags> _inbox = new();
    private readonly object _tickLock = new();
    private System.Timers.Timer? _timer;

    // Guarded by _tickLock
    private bool _joined;
    private DateTime _nextJoinAttempt;
    private bool _loggedWaiting;
    private StoryFlags _sent = new();         // bits we've reported to the room
    private StoryFlags _received = new();     // the room's bits as far as we know
    private StoryFlags _applied = new();      // room bits this loaded save has had (written by us, or already set)
    private StoryFlags _clearedLogged = new(); // applied bits the game cleared again (logged once)

    private int _joining;

    public StorySyncService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _room = room;
    }

    /// <summary>
    /// The room's story flags as far as this client knows them (a copy), or null while not joined
    /// (rule off, offline, not in play yet). Read-only view for the Room story flags page.
    /// </summary>
    public StoryFlags? RoomFlags
    {
        get
        {
            lock (_tickLock) return _joined ? _received.Clone() : null;
        }
    }

    public void Start()
    {
        if (_timer != null) return;
        _signalR.StoryFlagsReceived += OnStoryFlagsReceived;
        _signalR.ConnectionLost += OnConnectionReset;
        _signalR.Connected += OnConnectionReset;

        _timer = new System.Timers.Timer(TickMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            // Skip rather than overlap a slow tick.
            if (!Monitor.TryEnter(_tickLock)) return;
            try { Tick(); }
            catch (Exception ex) { Logger.Error(ex, "[story] tick failed"); }
            finally { Monitor.Exit(_tickLock); }
        };
        _timer.Start();
        Logger.Information("[story] shared story sync started ({Bits} syncable flag bits, {Figurines} figurines)",
            new StoryFlags { Bits = StoryFlags.SyncMask.ToArray() }.BitCount, StoryFlags.TotalFigurines);
    }

    public void Stop()
    {
        if (_timer == null) return;
        _signalR.StoryFlagsReceived -= OnStoryFlagsReceived;
        _signalR.ConnectionLost -= OnConnectionReset;
        _signalR.Connected -= OnConnectionReset;
        _timer.Stop();
        _timer.Dispose();
        _timer = null;
        lock (_tickLock) ResetSync(forgetApplied: true);
        _scene.Reset();
        Logger.Information("[story] shared story sync stopped");
    }

    /// <summary>
    /// Leave the room (we rejoin on the next stable tick, merging this game's flags up again).
    /// <paramref name="forgetApplied"/> also forgets which bits this save has had — only when a
    /// different save may be loaded (title screen / file select, or sync stopped).
    /// </summary>
    private void ResetSync(bool forgetApplied)
    {
        _joined = false;
        _nextJoinAttempt = DateTime.MinValue;
        _loggedWaiting = false;
        _sent = new StoryFlags();
        _received = new StoryFlags();
        _inbox.Clear();
        if (forgetApplied)
        {
            _applied = new StoryFlags();
            _clearedLogged = new StoryFlags();
        }
    }

    private void OnStoryFlagsReceived(StoryFlags flags)
    {
        if (flags == null || !flags.IsValid())
        {
            Logger.Warning("[story] ignored invalid story flags push");
            return;
        }
        _inbox.Enqueue(flags.Clone().Normalize());
        lock (_tickLock)
        {
            if (!_joined) _nextJoinAttempt = DateTime.MinValue; // room just got seeded? join now
        }
    }

    // Connection dropped, or a new one is up (the server may have restarted with an empty room): rejoin.
    private void OnConnectionReset()
    {
        lock (_tickLock) ResetSync(forgetApplied: false);
    }

    private void Tick()
    {
        if (!_dolphin.IsConnected) return;

        while (_inbox.TryDequeue(out var incoming))
            _received.MergeFrom(incoming);

        // Room rule off: neither share nor apply. We rejoin (merging up) when it's back on.
        if (!_room.Current.SharedStory)
        {
            if (_joined)
            {
                ResetSync(forgetApplied: false);
                Logger.Information("[story] shared story turned off by the room owner — keeping this game's story");
            }
            return;
        }

        if (!_scene.Check(_dolphin))
        {
            // Title / file select: whatever save loads next replaces the whole event array.
            if (StageIDs.IsNonGameplayStage(_scene.Stage) && (_joined || !_applied.IsEmpty))
            {
                ResetSync(forgetApplied: true);
                Logger.Information("[story] left gameplay ({Stage}) — will rejoin the room story when a save is loaded", _scene.Stage);
            }
            return;
        }

        // Offline: sync nothing (flags set meanwhile merge up when we rejoin on Connected).
        if (!_signalR.IsConnected) return;

        var raw = _dolphin.ReadMemory(EventFlagCatalog.EventBitfieldAddress, EventFlagCatalog.EventBitfieldSize);
        if (raw is not { Length: EventFlagCatalog.EventBitfieldSize }) return;
        var local = StoryFlags.FromEventBytes(raw);

        if (!_joined)
        {
            TryJoin(local);
            return;
        }

        // Every bit this save has had counts as applied — room bits it already has AND bits it set
        // itself (possibly before the room echoes them back): if the game clears one later we don't
        // re-apply it from the room.
        _applied.MergeFrom(local);
        var clearedByGame = _applied.Except(local).Except(_clearedLogged);
        if (!clearedByGame.IsEmpty)
        {
            _clearedLogged.MergeFrom(clearedByGame);
            Logger.Warning("[story] the game cleared {Count}: {Flags} — leaving them cleared",
                clearedByGame.CountText(), clearedByGame.Describe());
        }

        // Outbound: bits this game set that the room hasn't seen from us or anyone.
        var known = _sent.Clone();
        known.MergeFrom(_received);
        var fresh = local.Except(known);
        if (!fresh.IsEmpty)
        {
            Logger.Information("[story] local set {Count}: {Flags} → sending", fresh.CountText(), fresh.Describe());
            _sent.MergeFrom(local);
            var toSend = local.Clone();
            _ = Task.Run(async () =>
            {
                bool sent = false;
                try { sent = await _signalR.SendStoryFlagsAsync(toSend); }
                catch (Exception ex) { Logger.Warning(ex, "[story] sending flags failed"); }
                if (!sent)
                {
                    // Rejoin, which merges this game's flags up again.
                    Logger.Warning("[story] flags not delivered — will rejoin the room story");
                    lock (_tickLock) ResetSync(forgetApplied: false);
                }
            });
        }

        // Inbound: room bits this game is missing and has never had.
        var missing = ToApply(_received, local, _applied, () => SceneStabilityGate.IsIdle(_dolphin));
        if (missing.IsEmpty) return;
        ApplyBits(_dolphin, missing);
        _applied.MergeFrom(missing);
        Logger.Information("[story] applied room {Count}: {Flags}", missing.CountText(), missing.Describe());
        foreach (var f in missing.RiskyFlags())
            Logger.Warning("[story] WARNING applied {Flag}: {Effect}", f.Name, EventFlagCatalog.RiskEffect(f.Id));
    }

    /// <summary>
    /// What to write this tick: the room's bits this game is missing (<paramref name="local"/>) and has never
    /// had (<paramref name="applied"/>). Figurines wait for an idle game (not talking to Carlov, menu closed):
    /// without <paramref name="isIdle"/> they are left out, stay missing, and a later tick applies them.
    /// </summary>
    public static StoryFlags ToApply(StoryFlags room, StoryFlags local, StoryFlags applied, Func<bool> isIdle)
    {
        var missing = room.Except(local).Except(applied);
        return missing.FigurineCount > 0 && !isIdle() ? missing.FlagsOnly() : missing;
    }

    /// <summary>
    /// OR <paramref name="bits"/> into the game's event array, one byte at a time (read-modify-write per
    /// byte keeps the window where the game could race us tiny, and never touches other bits): the flags
    /// into bytes 0x00-0x41, the figurines into their registers (<see cref="StoryFlags.FigurineRegisterBytes"/>),
    /// as daNpcMt_c::setFigure does (getEventReg, OR the bit, setEventReg).
    /// </summary>
    public static void ApplyBits(IDolphinService dolphin, StoryFlags bits)
    {
        for (int i = 0; i < StoryFlags.ByteCount; i++)
            OrByte(dolphin, i, bits.Bits[i]);
        for (int i = 0; i < StoryFlags.FigurineByteCount; i++)
            OrByte(dolphin, StoryFlags.FigurineRegisterBytes[i], (byte)(bits.Figurines[i] & StoryFlags.FigurineMask[i]));
    }

    private static void OrByte(IDolphinService dolphin, int eventByte, byte add)
    {
        if (add == 0) return;
        uint addr = EventFlagCatalog.EventBitfieldAddress + (uint)eventByte;
        var b = dolphin.ReadMemory(addr, 1);
        if (b is not { Length: 1 } || (b[0] | add) == b[0]) return;
        dolphin.WriteMemory(addr, new[] { (byte)(b[0] | add) });
    }

    private void TryJoin(StoryFlags local)
    {
        if (DateTime.UtcNow < _nextJoinAttempt) return;
        if (Interlocked.Exchange(ref _joining, 1) == 1) return;
        _nextJoinAttempt = DateTime.UtcNow + JoinRetry;

        var snapshot = local.Clone();
        _ = Task.Run(async () =>
        {
            try
            {
                var room = await _signalR.JoinRoomStoryAsync(snapshot);
                if (room == null || !room.IsValid())
                {
                    bool waiting;
                    lock (_tickLock)
                    {
                        waiting = !_loggedWaiting && _signalR.IsConnected && _room.Current.SharedStory;
                        if (waiting) _loggedWaiting = true;
                    }
                    if (waiting)
                        Logger.Information("[story] room story not ready — waiting for room owner {Owner} to seed it (retrying)",
                            _room.Current.OwnerName);
                    return;
                }

                room = room.Clone().Normalize();
                int toApply;
                lock (_tickLock)
                {
                    _received.MergeFrom(room);
                    _sent.MergeFrom(snapshot);
                    _joined = true;
                    _loggedWaiting = false;
                    var fresh = room.Except(snapshot).Except(_applied);
                    toApply = fresh.BitCount + fresh.FigurineCount;
                }
                Logger.Information("[story] joined room story ({Room} in room, this game brought {Mine}); {N} to apply",
                    room.CountText(), snapshot.CountText(), toApply);
            }
            catch (Exception ex) { Logger.Warning(ex, "[story] join failed (retrying)"); }
            finally { Interlocked.Exchange(ref _joining, 0); }
        });
    }

    public void Dispose() => Stop();
}
