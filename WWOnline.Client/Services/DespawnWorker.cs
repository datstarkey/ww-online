using Serilog;
using WWOnline.Data;

namespace WWOnline.Services;

/// <summary>
/// Shared-world layer 2 (live despawn of placed items other players collected) runs in the puppet
/// REL's per-puppet tick, so it only runs while a puppet actor exists. When the REL still has work
/// to do and no peer's puppet is spawned, <see cref="PuppetSyncService"/> asks the hook for one
/// anyway with slot 0 inactive — the hook spawns it PARKED (invisible, no collision / targeting)
/// and it just runs the despawn scan.
///
/// <see cref="WorldFlagSyncService"/> calls <see cref="MarkChanged"/> whenever it rewrites the
/// tag / mask block, and PuppetSyncService on every stage / room change; PuppetSyncService asks
/// <see cref="IsNeeded"/> every tick. One instance is shared by both (DI singleton).
/// </summary>
public sealed class DespawnWorker
{
    private static readonly ILogger Logger = Log.ForContext<DespawnWorker>();

    /// <summary>Stop asking for a worker when the scan's result hasn't moved for this long (until the next change).</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(10);

    /// <summary>One consistent-ish read of the REL's inputs and its last scan result.</summary>
    /// <param name="Tag">WORLDSYNC_STAGE_TAG (C#).</param>
    /// <param name="Mask">WORLDSYNC_ITEM_MASK (C#).</param>
    /// <param name="LiveItem">Live mItem[0] of the current stage.</param>
    /// <param name="SeqBefore">WORLDSYNC_SCAN_SEQ read before the result fields.</param>
    /// <param name="Pending">WORLDSYNC_PENDING: matching placed items still alive after the scan.</param>
    /// <param name="ScanTag">WORLDSYNC_SCAN_TAG: the tag the scan used.</param>
    /// <param name="ScanCand">WORLDSYNC_SCAN_CAND: the candidates the scan used.</param>
    /// <param name="SeqAfter">WORLDSYNC_SCAN_SEQ read after the result fields.</param>
    public readonly record struct Scan(uint Tag, uint Mask, uint LiveItem, uint SeqBefore,
        uint Pending, uint ScanTag, uint ScanCand, uint SeqAfter);

    private readonly object _lock = new();
    private readonly Func<DateTime> _clock;

    // Guarded by _lock
    private uint? _seqAtLastChange;
    private bool _gaveUp;
    private (uint Pending, uint Cand)? _progress;
    private DateTime _progressSince;
    private bool _requested;

    public DespawnWorker() : this(() => DateTime.UtcNow) { }

    public DespawnWorker(Func<DateTime> clock) => _clock = clock;

    /// <summary>
    /// Does the REL still need a scan? No when there's nothing to despawn (no valid tag, empty mask,
    /// or none of the masked items is collected in the live flags); yes when the scan result is
    /// torn, stale (other tag / candidates) or hasn't run since our last change; else its verdict
    /// (items still alive).
    /// </summary>
    public static bool NeedsScan(in Scan s, uint? seqAtLastChange)
    {
        if ((s.Tag & 0xFFFFFF00u) != (uint)PuppetLayout.WORLDSYNC_TAG_MAGIC || s.Mask == 0) return false;
        uint cand = s.Mask & s.LiveItem;
        if (cand == 0) return false;
        if (s.SeqBefore != s.SeqAfter) return true;                     // torn read
        if (s.ScanTag != s.Tag || s.ScanCand != cand) return true;      // result is for other inputs
        if (seqAtLastChange == s.SeqBefore) return true;                // no scan since our last change
        return s.Pending != 0;
    }

    /// <summary>Record that the tag / mask or the scene just changed; <paramref name="scanSeq"/> is SCAN_SEQ now.</summary>
    public void MarkChanged(uint? scanSeq)
    {
        lock (_lock)
        {
            _seqAtLastChange = scanSeq;
            _gaveUp = false;
            _progress = null;
        }
    }

    /// <summary>
    /// <see cref="NeedsScan"/> plus the give-up rule: while needed, if pending / candidates don't
    /// change for <see cref="GiveUpAfter"/>, stop asking until the next <see cref="MarkChanged"/>.
    /// </summary>
    public bool Update(in Scan s)
    {
        lock (_lock)
        {
            bool need = NeedsScan(s, _seqAtLastChange);
            if (!need)
                _progress = null;
            else if (!_gaveUp)
            {
                var now = _clock();
                var progress = (s.Pending, s.Mask & s.LiveItem);
                if (_progress != progress)
                {
                    _progress = progress;
                    _progressSince = now;
                }
                else if (now - _progressSince >= GiveUpAfter)
                {
                    _gaveUp = true;
                    Logger.Warning("[world] despawn worker made no progress in {Seconds:F0}s (pending {Pending}, candidates 0x{Cand:X8}) — giving up until the next change",
                        GiveUpAfter.TotalSeconds, s.Pending, s.Mask & s.LiveItem);
                }
            }

            bool result = need && !_gaveUp;
            if (result != _requested)
            {
                _requested = result;
                Logger.Information(result
                    ? "[world] despawn worker puppet requested (candidates 0x{Cand:X8}, pending {Pending})"
                    : "[world] despawn worker puppet released (candidates 0x{Cand:X8}, pending {Pending})",
                    s.Mask & s.LiveItem, s.Pending);
            }
            return result;
        }
    }

    /// <summary>Read SCAN_SEQ and <see cref="MarkChanged(uint?)"/>. Call BEFORE rewriting the tag / mask.</summary>
    public void MarkChanged(IDolphinService dolphin) => MarkChanged(ReadU32(dolphin, PuppetLayout.WORLDSYNC_SCAN_SEQ_ADDR));

    /// <summary>Read the REL's inputs + last scan result and <see cref="Update"/>. False if unreadable.</summary>
    public bool IsNeeded(IDolphinService dolphin)
    {
        if (ReadU32(dolphin, PuppetLayout.WORLDSYNC_STAGE_TAG_ADDR) is not uint tag ||
            ReadU32(dolphin, PuppetLayout.WORLDSYNC_ITEM_MASK_ADDR) is not uint mask)
            return false;
        uint liveItem = 0;
        if ((tag & 0xFFFFFF00u) == (uint)PuppetLayout.WORLDSYNC_TAG_MAGIC && mask != 0)
        {
            if (ReadU32(dolphin, GameMemoryAddresses.WorldFlags.LiveMemory + GameMemoryAddresses.WorldFlags.OffItem) is not uint live)
                return false;
            liveItem = live;
        }

        // Sequence-bracketed read of the scan result (the REL bumps SCAN_SEQ last).
        if (ReadU32(dolphin, PuppetLayout.WORLDSYNC_SCAN_SEQ_ADDR) is not uint s1 ||
            ReadU32(dolphin, PuppetLayout.WORLDSYNC_PENDING_ADDR) is not uint pending ||
            ReadU32(dolphin, PuppetLayout.WORLDSYNC_SCAN_TAG_ADDR) is not uint scanTag ||
            ReadU32(dolphin, PuppetLayout.WORLDSYNC_SCAN_CAND_ADDR) is not uint scanCand ||
            ReadU32(dolphin, PuppetLayout.WORLDSYNC_SCAN_SEQ_ADDR) is not uint s2)
            return false;

        return Update(new Scan(tag, mask, liveItem, s1, pending, scanTag, scanCand, s2));
    }

    private static uint? ReadU32(IDolphinService dolphin, uint addr)
    {
        var b = dolphin.ReadMemory(addr, 4);
        if (b == null || b.Length < 4) return null;
        return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
    }
}
