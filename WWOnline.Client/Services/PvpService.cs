using System.Collections.Concurrent;
using Serilog;
using WWOnline.Hubs;
using WWOnline.Native;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// PVP (the room's <see cref="RoomSettings.Pvp"/> rule), native backend only (tww.h's pvp block, <see cref="ITwwPvpApi"/>).
/// Every tick (20Hz) it tells the game which puppet slots take part (players whose game takes hits too,
/// <see cref="ClientCapabilities.PvpHits"/>; none while the rule is off or we're offline), sends each hit the local Link
/// landed on one of them to that player (<see cref="SignalRClientService.SendPvpHitAsync"/>), and applies the hits peers
/// landed on our puppet over there to the local Link (the game does the damage, guard, knockback and death).
/// Hits are momentary: one not sent or applied promptly is dropped, never retried.
/// </summary>
public sealed class PvpService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<PvpService>();

    /// <summary>A received hit not applied within this is dropped.</summary>
    public static readonly TimeSpan MaxHitAge = TimeSpan.FromSeconds(1);

    /// <summary>Received hits kept at most (a stuck tick / a detached game).</summary>
    public const int MaxQueued = 16;

    /// <summary>Local hits taken off the game's queue per tick at most.</summary>
    private const int MaxPolledPerTick = 32;

    private readonly SignalRClientService _signalR;
    private readonly PuppetSyncService _puppets;
    private readonly RoomSettingsService _room;
    private readonly INativeGame? _native;
    private readonly ConcurrentQueue<(PvpHit Hit, DateTime At)> _incoming = new();

    private System.Timers.Timer? _timer;
    private int _ticking;
    private uint _mask;
    private bool _disposed;

    public PvpService(SignalRClientService signalR, PuppetSyncService puppets, RoomSettingsService room,
                      INativeGame? native = null)
    {
        _signalR = signalR;
        _puppets = puppets;
        _room = room;
        _native = native;
    }

    public void Start()
    {
        if (_native == null || _timer != null)
            return;
        _signalR.PvpHitReceived += OnPvpHitReceived;
        _timer = new System.Timers.Timer(50);
        _timer.Elapsed += (_, _) =>
        {
            if (Interlocked.CompareExchange(ref _ticking, 1, 0) != 0) return;
            _ = Task.Run(async () =>
            {
                try { await TickAsync(); }
                catch (Exception ex) { Logger.Warning(ex, "[pvp] tick failed"); }
                finally { Interlocked.Exchange(ref _ticking, 0); }
            });
        };
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Stop()
    {
        _signalR.PvpHitReceived -= OnPvpHitReceived;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _incoming.Clear();
    }

    private void OnPvpHitReceived(PvpHit hit)
    {
        if (hit == null || string.IsNullOrEmpty(hit.AttackerId) || !hit.IsValid())
        {
            Logger.Warning("[pvp] ignored a malformed hit: {Hit}", hit?.ToString() ?? "null");
            return;
        }
        if (!_room.Current.Pvp || _incoming.Count >= MaxQueued)
            return;
        _incoming.Enqueue((hit, DateTime.UtcNow));
    }

    /// <summary>One tick (the timer's; tests call it directly).</summary>
    public async Task TickAsync()
    {
        if (_native is not { IsRunning: true } native)
        {
            _incoming.Clear();
            return;
        }
        bool rule = _room.Current.Pvp && _signalR.IsConnected;
        uint mask = rule ? SlotMask() : 0;
        if (mask != _mask)
        {
            _mask = mask;
            Logger.Information("[pvp] taking part: slots 0x{Mask:X}", mask);
        }

        var toApply = new List<(PvpHit Hit, TwwPvpHit Native)>();
        var now = DateTime.UtcNow;
        while (_incoming.TryDequeue(out var item))
        {
            var (hit, at) = item;
            int slot = -1;
            string why = !rule ? "rule off"
                : now - at > MaxHitAge ? "too old"
                : !_puppets.TryGetSlot(hit.AttackerId, out slot) ? "they have no puppet slot here"
                : (mask & (1u << slot)) == 0 ? "their slot doesn't take part"
                : "";
            if (why.Length > 0)
            {
                Logger.Information("[pvp] dropped a hit from {Player}: {Why}", Name(hit.AttackerId), why);
                continue;
            }
            toApply.Add((hit, ToNative(hit, slot)));
        }

        var (polled, applied) = native.Invoke(api =>
        {
            var pvp = (ITwwPvpApi)api;
            pvp.SetSlots(mask);
            var hits = new List<TwwPvpHit>();
            while (hits.Count < MaxPolledPerTick && pvp.PollHit(out var h))
                hits.Add(h);
            var ok = toApply.Select(a => pvp.ApplyHit(a.Native)).ToList();
            return (hits, ok);
        });

        for (int i = 0; i < toApply.Count; i++)
        {
            var hit = toApply[i].Hit;
            Logger.Information("[pvp] took a hit from {Player}: {Hit}{Refused}", Name(hit.AttackerId), hit,
                applied[i] ? "" : " (the game refused it)");
        }

        if (!rule)
            return;
        foreach (var h in polled)
            await SendAsync(h);
    }

    /// <summary>Bit i: slot i's player is here and their game takes PVP hits.</summary>
    private uint SlotMask()
    {
        uint mask = 0;
        for (int slot = 0; slot < TwwNative.MaxPeers; slot++)
        {
            if (_puppets.GetPlayerInSlot(slot) is { } id &&
                _signalR.PlayerCapabilities.TryGetValue(id, out var caps) && caps.HasFlag(ClientCapabilities.PvpHits))
                mask |= 1u << slot;
        }
        return mask;
    }

    private async Task SendAsync(TwwPvpHit h)
    {
        if (_puppets.GetPlayerInSlot(h.Slot) is not { } target)
        {
            Logger.Information("[pvp] dropped our hit on slot {Slot}: nobody is in it", h.Slot);
            return;
        }
        var hit = new PvpHit
        {
            TargetId = target,
            StageName = _puppets.LocalLocation.Stage,
            Damage = h.Damage,
            Spl = h.Spl,
            AtType = h.AtType,
            Position = new Vector3(h.Pos[0], h.Pos[1], h.Pos[2]),
            AttackerPosition = new Vector3(h.AttackerPos[0], h.AttackerPos[1], h.AttackerPos[2]),
        };
        if (!hit.IsValid())
        {
            Logger.Warning("[pvp] not sending an out-of-range hit: {Hit}", hit);
            return;
        }
        bool sent;
        try { sent = await _signalR.SendPvpHitAsync(hit); }
        catch (Exception ex)
        {
            Logger.Debug(ex, "[pvp] send failed");
            sent = false;
        }
        Logger.Information("[pvp] hit {Player}: {Hit}{Sent}", Name(target), hit, sent ? "" : " (not sent: offline)");
    }

    private static TwwPvpHit ToNative(PvpHit hit, int slot)
    {
        var h = new TwwPvpHit { Slot = (byte)slot, Damage = hit.Damage, Spl = hit.Spl, AtType = hit.AtType };
        (h.Pos[0], h.Pos[1], h.Pos[2]) = (hit.Position.X, hit.Position.Y, hit.Position.Z);
        (h.AttackerPos[0], h.AttackerPos[1], h.AttackerPos[2]) = (hit.AttackerPosition.X, hit.AttackerPosition.Y, hit.AttackerPosition.Z);
        return h;
    }

    private string Name(string id) =>
        _signalR.Players.TryGetValue(id, out var name) ? name : id.Length > 8 ? id[..8] : id;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
    }
}
