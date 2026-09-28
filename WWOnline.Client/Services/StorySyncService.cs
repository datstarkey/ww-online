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
/// registers 0x79-0xFF, never mTmp).
///
///   - on attach this game JOINS: the room owner's game seeds the room; anyone else's flags merge
///     up into it, and the room's flags come back to apply;
///   - every tick (4Hz, only while the scene is stable), newly set local bits are sent, and room
///     bits this game is missing are ORed in with a per-byte read-modify-write.
/// Applying is edge-triggered per bit: a room bit is written at most once per loaded save, and a
/// bit this save has ever had (from the room or set by the game itself) is never re-forced. If the
/// game clears one later we leave it cleared
/// (the catalog excludes every flag the game is known to clear; this is the safety net).
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
        Logger.Information("[story] shared story sync started ({Bits} syncable flag bits)",
            new StoryFlags { Bits = StoryFlags.SyncMask.ToArray() }.BitCount);
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

        var raw = _dolphin.ReadMemory(EventFlagCatalog.EventBitfieldAddress, StoryFlags.ByteCount);
        if (raw == null) return;
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
            Logger.Warning("[story] the game cleared {Count} synced flag(s): {Flags} — leaving them cleared",
                clearedByGame.BitCount, clearedByGame.Describe());
        }

        // Outbound: bits this game set that the room hasn't seen from us or anyone.
        var known = _sent.Clone();
        known.MergeFrom(_received);
        var fresh = local.Except(known);
        if (!fresh.IsEmpty)
        {
            Logger.Information("[story] local set {Count} flag(s): {Flags} → sending", fresh.BitCount, fresh.Describe());
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
        var missing = _received.Except(local).Except(_applied);
        if (missing.IsEmpty) return;
        ApplyBits(missing);
        _applied.MergeFrom(missing);
        Logger.Information("[story] applied {Count} room flag(s): {Flags}", missing.BitCount, missing.Describe());
        foreach (var f in missing.RiskyFlags())
            Logger.Warning("[story] WARNING applied {Flag}: {Effect}", f.Name, EventFlagCatalog.RiskEffect(f.Id));
    }

    /// <summary>OR <paramref name="bits"/> into the game's event array, one byte at a time (read-modify-write
    /// per byte keeps the window where the game could race us tiny, and never touches other bits).</summary>
    private void ApplyBits(StoryFlags bits)
    {
        for (int i = 0; i < StoryFlags.ByteCount; i++)
        {
            byte add = bits.Bits[i];
            if (add == 0) continue;
            uint addr = EventFlagCatalog.EventBitfieldAddress + (uint)i;
            var b = _dolphin.ReadMemory(addr, 1);
            if (b == null) continue;
            _dolphin.WriteMemory(addr, new[] { (byte)(b[0] | add) });
        }
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
                    toApply = room.Except(snapshot).Except(_applied).BitCount;
                }
                Logger.Information("[story] joined room story ({Room} flag(s) in room, this game brought {Mine}); {N} to apply",
                    room.BitCount, snapshot.BitCount, toApply);
            }
            catch (Exception ex) { Logger.Warning(ex, "[story] join failed (retrying)"); }
            finally { Interlocked.Exchange(ref _joining, 0); }
        });
    }

    public void Dispose() => Stop();
}
