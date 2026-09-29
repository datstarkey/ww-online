using System.Collections.Concurrent;
using Serilog;
using WWOnline.Data;
using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Other players' projectiles (docs/held-items.md stages 2-3): the client half of the puppet REL's
/// events block (<see cref="PlayerEventBlock"/>, GameMod puppet_fx.c).
///
/// OUT: every tick (20Hz) it drains the REL's outbox — the local Link's bomb thrown / picked up /
/// exploded / gone, the boat cannon fired, an arrow shot — stamps each event with our stage, the projectile's own room (the REL's, else ours), our origin and a sequence
/// number, and sends it (<see cref="SignalRClientService.SendPlayerEventAsync"/>). An event is only
/// dropped from the queue once it went out, or when it's too old to matter.
/// IN: a peer's event (relayed by the server to the players where it happened) is checked
/// (<see cref="PlayerEvent.IsValid"/>, the sender's origin + sequence, that it happened in our stage and
/// room — at sea within sight of us — and that they have a puppet slot) and written into the inbox for
/// their slot; the REL spawns / explodes the copy.
/// Both only while the room's <see cref="RoomSettings.SharedProjectiles"/> rule is on: the REL's SEND
/// flag follows the rule, and received events are dropped while it is off (the server drops them too).
/// </summary>
public sealed class PlayerEventService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<PlayerEventService>();

    /// <summary>An event not sent / not delivered to the game within this is dropped: a bomb's fuse is 5 s.</summary>
    public static readonly TimeSpan MaxEventAge = TimeSpan.FromSeconds(2);

    /// <summary>Queued events kept at most (a stuck send / a game that isn't running the REL).</summary>
    public const int MaxQueued = 32;

    private readonly IDolphinService _dolphin;
    private readonly SignalRClientService _signalR;
    private readonly PuppetSyncService _puppets;
    private readonly RoomSettingsService _room;
    private readonly PlayerEventDedup _dedup = new();
    private readonly MinigameGate _minigame = new();
    private bool _minigameActive; // the gate's verdict this tick (it sees every tick's type and stage)

    /// <summary>This app run's <see cref="PlayerEvent.Origin"/>; its seq keeps counting across reconnects.</summary>
    private readonly string _origin = Guid.NewGuid().ToString("N");

    private readonly ConcurrentQueue<(PlayerEvent Event, DateTime At)> _incoming = new();
    private readonly List<(PlayerEvent Event, DateTime At)> _outgoing = new();
    private readonly List<(PlayerEvent Event, DateTime At)> _pendingIn = new();
    // Guards _outgoing and _pendingIn: the tick (a pool thread) and Stop (any thread) both touch them.
    private readonly object _outLock = new();

    private System.Timers.Timer? _timer;
    private int _ticking;
    private uint _seq;
    private uint _block;
    private uint _outCursor;
    private bool? _sendFlag;
    private (uint Spawned, uint Rejected, uint Dropped) _lastCounters;
    private bool _disposed;

    public PlayerEventService(IDolphinService dolphin, SignalRClientService signalR, PuppetSyncService puppets,
                              RoomSettingsService room)
    {
        _dolphin = dolphin;
        _signalR = signalR;
        _puppets = puppets;
        _room = room;
    }

    public void Start()
    {
        if (_timer != null)
            return;
        _block = 0;
        _sendFlag = null;
        _signalR.PlayerEventReceived += OnPlayerEventReceived;
        _timer = new System.Timers.Timer(50);
        _timer.Elapsed += (_, _) =>
        {
            if (Interlocked.CompareExchange(ref _ticking, 1, 0) != 0) return;
            _ = Task.Run(async () =>
            {
                try { await TickAsync(); }
                catch (Exception ex) { Logger.Warning(ex, "[fx] tick failed"); }
                finally { Interlocked.Exchange(ref _ticking, 0); }
            });
        };
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Stop()
    {
        _signalR.PlayerEventReceived -= OnPlayerEventReceived;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _incoming.Clear();
        lock (_outLock)
        {
            _outgoing.Clear();
            _pendingIn.Clear();
        }
    }

    private void OnPlayerEventReceived(PlayerEvent evt)
    {
        if (evt == null || string.IsNullOrEmpty(evt.PlayerId) || !evt.IsValid())
        {
            Logger.Warning("[fx] ignored a malformed event from {Player}: {Event}", evt?.PlayerId ?? "?", evt?.ToString() ?? "null");
            return;
        }
        if (!_room.Current.SharedProjectiles)
            return;
        if (!_dedup.Accept(evt))
        {
            Logger.Information("[fx] ignored a repeated event #{Seq} from {Player}: {Event}", evt.Seq, Short(evt.PlayerId), evt);
            return;
        }
        if (_incoming.Count >= MaxQueued)
            return; // not draining (game detached, tick stuck): the old ones will be dropped as stale anyway
        _incoming.Enqueue((evt, DateTime.UtcNow));
    }

    private async Task TickAsync()
    {
        if (!_dolphin.IsConnected)
        {
            DropStale(DateTime.UtcNow);
            return;
        }
        // Every tick, events or not, so the gate knows which stage a minigame started in.
        var mg = _dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.MiniGameType, 1);
        _minigameActive = _minigame.Update(mg is { Length: 1 } ? mg[0] : (byte)0, _puppets.LocalLocation.Stage);
        bool rule = _room.Current.SharedProjectiles && _signalR.IsConnected;

        var block = PlayerEventBlock.Locate(_dolphin);
        if (block is not { } b)
        {
            if (_block != 0)
                Logger.Information("[fx] events block gone (was 0x{Block:X8})", _block);
            _block = 0;
            _sendFlag = null;
            DropStale(DateTime.UtcNow);
            return;
        }
        if (b != _block)
        {
            // A new block (first puppet since boot): skip whatever the outbox held before we looked.
            _block = b;
            _sendFlag = null;
            _outCursor = PlayerEventBlock.ReadWord(_dolphin, b, PuppetLayout.PUPPET_FX_OFF_OUT_WRITE);
            PlayerEventBlock.WriteWord(_dolphin, b, PuppetLayout.PUPPET_FX_OFF_OUT_READ, _outCursor);
            Logger.Information("[fx] events block at 0x{Block:X8}", b);
        }
        if (_sendFlag != rule)
        {
            PlayerEventBlock.SetSend(_dolphin, b, rule);
            _sendFlag = rule;
            Logger.Information("[fx] report our projectiles: {On}", rule);
        }

        CollectOutbox(b, rule);
        await SendOutgoingAsync();
        DeliverIncoming(b, rule);
        LogCounters(b);
    }

    private void CollectOutbox(uint block, bool rule)
    {
        var raw = PlayerEventBlock.ReadOutbox(_dolphin, block, ref _outCursor);
        if (raw.Count == 0 || !rule)
            return;
        var (stage, room, _, _) = _puppets.LocalLocation;
        var now = DateTime.UtcNow;
        lock (_outLock)
        {
            foreach (var r in raw)
            {
                var evt = PlayerEventBlock.ToEvent(r, room);
                evt.Origin = _origin;
                evt.Seq = ++_seq;
                evt.StageName = stage;
                if (!evt.IsValid())
                {
                    Logger.Warning("[fx] not sending an out-of-range event: {Event}", evt);
                    continue;
                }
                if (_outgoing.Count >= MaxQueued)
                    _outgoing.RemoveAt(0);
                _outgoing.Add((evt, now));
            }
        }
    }

    private async Task SendOutgoingAsync()
    {
        while (true)
        {
            (PlayerEvent Event, DateTime At) next;
            lock (_outLock)
            {
                _outgoing.RemoveAll(o => DateTime.UtcNow - o.At > MaxEventAge);
                if (_outgoing.Count == 0)
                    return;
                next = _outgoing[0];
            }
            bool sent;
            try { sent = await _signalR.SendPlayerEventAsync(next.Event); }
            catch (Exception ex)
            {
                Logger.Debug(ex, "[fx] send failed, retrying next tick");
                sent = false;
            }
            if (!sent)
                return; // offline: keep it (until MaxEventAge)
            lock (_outLock)
                _outgoing.Remove(next);
            Logger.Information("[fx] sent #{Seq} {Event}", next.Event.Seq, next.Event);
        }
    }

    private void DeliverIncoming(uint block, bool rule)
    {
        lock (_outLock)
            DeliverIncomingLocked(block, rule);
    }

    private void DeliverIncomingLocked(uint block, bool rule)
    {
        var now = DateTime.UtcNow;
        while (_incoming.TryDequeue(out var item))
            _pendingIn.Add(item);
        if (_pendingIn.Count == 0)
            return;

        var (stage, room, x, z) = _puppets.LocalLocation;
        bool minigame = _minigameActive;
        var batch = new List<PlayerEventBlock.Raw>();
        var batchEvents = new List<(PlayerEvent Event, DateTime At)>();
        foreach (var item in _pendingIn)
        {
            var evt = item.Event;
            int slot = -1;
            string why = !rule ? "rule off"
                : now - item.At > MaxEventAge ? "too old"
                : minigame ? "a minigame is running here"
                : !PuppetVisibility.IsSameLocation(stage, room, x, z, evt.StageName, evt.RoomNumber, evt.Position, wasVisible: true)
                    ? $"we're in {stage}:{room}"
                : !_puppets.TryGetSlot(evt.PlayerId, out slot) ? "they have no puppet slot here"
                : "";
            if (why.Length > 0)
            {
                Logger.Information("[fx] dropped {Event} from {Player}: {Why}", evt, Short(evt.PlayerId), why);
                continue;
            }
            batch.Add(PlayerEventBlock.FromEvent(evt, slot));
            batchEvents.Add(item);
        }
        _pendingIn.Clear();

        int written = PlayerEventBlock.WriteInbox(_dolphin, block, batch);
        for (int i = 0; i < batchEvents.Count; i++)
        {
            var (evt, at) = batchEvents[i];
            if (i < written)
                Logger.Information("[fx] in {Event} from {Player} -> slot {Slot}", evt, Short(evt.PlayerId), batch[i].Slot);
            else
                _pendingIn.Add((evt, at)); // inbox full: next tick (the REL empties it every frame)
        }
    }

    private void DropStale(DateTime now)
    {
        lock (_outLock)
        {
            while (_incoming.TryDequeue(out var item))
                _pendingIn.Add(item);
            _pendingIn.RemoveAll(p => now - p.At > MaxEventAge);
            _outgoing.RemoveAll(o => now - o.At > MaxEventAge);
        }
    }

    /// <summary>One line whenever the REL's counters move: copies spawned, events it refused, outbox drops.</summary>
    private void LogCounters(uint block)
    {
        var counters = (PlayerEventBlock.ReadWord(_dolphin, block, PuppetLayout.PUPPET_FX_OFF_SPAWNED),
                        PlayerEventBlock.ReadWord(_dolphin, block, PuppetLayout.PUPPET_FX_OFF_IN_REJECTED),
                        PlayerEventBlock.ReadWord(_dolphin, block, PuppetLayout.PUPPET_FX_OFF_OUT_DROPPED));
        if (counters == _lastCounters)
            return;
        _lastCounters = counters;
        Logger.Information("[fx] REL: spawned {Spawned}, refused {Rejected}, outbox drops {Dropped}",
            counters.Item1, counters.Item2, counters.Item3);
    }

    private static string Short(string id) => id.Length > 8 ? id[..8] : id;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}

/// <summary>
/// Drops a repeated or out-of-order event per sending app: each app run numbers its events (+1 per
/// event, across reconnects) under its own <see cref="PlayerEvent.Origin"/>, and the server keeps their
/// order, so anything at or below the last seq seen from that origin is a repeat — also one re-sent
/// under a new connection id after a reconnect. Remembers at most <see cref="MaxOrigins"/> origins.
/// </summary>
public sealed class PlayerEventDedup
{
    public const int MaxOrigins = 64;

    private readonly Dictionary<string, uint> _lastSeq = new();
    private readonly Queue<string> _order = new();
    private readonly object _lock = new();

    public bool Accept(PlayerEvent evt)
    {
        lock (_lock)
        {
            if (_lastSeq.TryGetValue(evt.Origin, out uint last))
            {
                if (evt.Seq <= last)
                    return false;
                _lastSeq[evt.Origin] = evt.Seq;
                return true;
            }
            if (_order.Count >= MaxOrigins)
                _lastSeq.Remove(_order.Dequeue());
            _lastSeq[evt.Origin] = evt.Seq;
            _order.Enqueue(evt.Origin);
            return true;
        }
    }
}

/// <summary>
/// Is one of the viewer's minigames running (the sailing race, Spectacle Island's cannons, the auction,
/// mail sorting, the bow game...)? Peers' projectiles are dropped then, so they can't score or break
/// anything in it. play.mMiniGameType says so, but only endMiniGame clears it: a minigame left without
/// ending it (a game over, back to the title without a reset) would leave it set and switch peers'
/// projectiles off for good. No minigame spans a stage change, so a type only counts in the stage where
/// it was first seen (fed every tick); a new stage with it still set means it is stale. A new type (a
/// new minigame: startMiniGame overwrites a stale type without a 0 between) starts afresh in the stage
/// it is seen in. (A reset re-zeroes it: the play struct is in the DOL's .bss.)
/// </summary>
public sealed class MinigameGate
{
    private byte _type;
    private string? _stage;
    private bool _stale;

    /// <summary>Feed the current type and stage every tick; true while a minigame counts as running.</summary>
    public bool Update(byte type, string stage)
    {
        if (type != _type)
        {
            // Cleared, or a (new) minigame started: count it from here.
            _type = type;
            _stage = type == 0 ? null : stage;
            _stale = false;
        }
        if (type == 0)
            return false;
        if (_stage != stage)
            _stale = true; // left its stage without endMiniGame: stale until the game clears or replaces it
        return !_stale;
    }
}
