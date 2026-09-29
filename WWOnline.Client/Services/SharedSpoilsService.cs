using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared spoils bag (room rule <see cref="RoomSettings.SharedSpoils"/>; the sync itself is
/// <see cref="SharedBagService{T}"/>). The server holds one count per spoil type (<see cref="SpoilsCounts"/>).
/// A pushed total goes into this bag the way a pickup, sale or trade does (<see cref="SpoilsBag.PlanFor"/>): an
/// arriving type takes the first free slot, and each count change is queued on the game's pending spoils
/// counters for d_meter to apply (it empties a type's slot and X/Y/Z button at 0). The write is
/// compare-and-swap against a fresh read with nothing pending. While the game still has a count pending
/// (ours or its own) the bag isn't read; once it's applied the sync point is what was planned
/// (<see cref="SpoilsBag.SettledBaseline"/>), so a pickup during the apply is still sent as a local change.
/// </summary>
public class SharedSpoilsService : SharedBagService<SpoilsCounts>
{
    private SpoilsBagSlots? _bag;          // the bag as last read (what ApplyTarget writes over)
    private SpoilsCounts? _awaitExpected;   // a plan we queued, not yet applied by the game
    private SpoilsCounts? _awaitBefore;     // the counts when we queued it
    private bool _adoptNext;                // a write failed part-way: take what the bag settles at as the sync point

    public SharedSpoilsService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
        : base(dolphin, signalR, room) { }

    protected override string Tag => "spoils";
    protected override string What => "shared spoils bag";
    protected override bool RuleOn(RoomSettings rules) => rules.SharedSpoils;
    protected override bool OwnsBag() => SpoilsBagMemory.OwnsBag(Dolphin);
    protected override void Subscribe(Action<SpoilsCounts> onTotal) => SignalR.SpoilsTotalReceived += onTotal;
    protected override void Unsubscribe(Action<SpoilsCounts> onTotal) => SignalR.SpoilsTotalReceived -= onTotal;
    protected override Task<SpoilsCounts?> JoinAsync(SpoilsCounts current) => SignalR.JoinSpoilsAsync(current);
    protected override Task<bool> SendDeltaAsync(SpoilsCounts delta) => SignalR.SendSpoilsDeltaAsync(delta);

    protected override void OnReset()
    {
        _bag = null;
        _awaitExpected = null;
        _awaitBefore = null;
        _adoptNext = false;
    }

    protected override SpoilsCounts? ReadCounts()
    {
        // A count change still queued for d_meter (a pickup this frame, or our own write): wait for it to land.
        if (SpoilsBagMemory.ReadPending(Dolphin) is not { } pending || pending.Any(p => p != 0)) return null;
        _bag = SpoilsBagMemory.Read(Dolphin);
        return _bag?.Count();
    }

    protected override SpoilsCounts? Settle(SpoilsCounts current)
    {
        if (_adoptNext)
        {
            // Our own half-write must not be sent back as a local change.
            _adoptNext = false;
            return current;
        }
        if (_awaitExpected is not { } expected || _awaitBefore is not { } before) return null;
        _awaitExpected = _awaitBefore = null;
        var baseline = SpoilsBag.SettledBaseline(expected, before, current, out bool writeLost);
        if (writeLost)
            Logger.Warning("[spoils] queued {Expected} but the bag still shows {Current} — pending spoils write not taking effect?",
                expected, current);
        return baseline;
    }

    protected override (bool Applied, SpoilsCounts? Baseline) ApplyTarget(SpoilsCounts current, SpoilsCounts target)
    {
        if (_bag is not { } bag) return (false, null);
        var plan = SpoilsBag.PlanFor(bag, target);
        switch (SpoilsBagMemory.Write(Dolphin, bag, plan))
        {
            case BagWriteResult.Written:
                if (plan.IsEmpty)
                {
                    // Only slots changed (a type whose count was already right): nothing for d_meter to apply.
                    return (true, plan.Expected);
                }
                _awaitExpected = plan.Expected;
                _awaitBefore = current;
                if (plan.Expected.Equals(target))
                    Logger.Information("[spoils] shared bag {Total} → applying ({Delta})", target, target.Minus(current).DeltaText());
                else
                    Logger.Warning("[spoils] shared bag {Total}: only {Applied} fits this bag", target, plan.Expected);
                return (true, null); // the sync point moves once the game has applied it (Settle)
            case BagWriteResult.Failed:
                Logger.Warning("[spoils] writing the bag failed; retrying");
                _adoptNext = true;
                return (false, null);
            default:
                // Raced: the game changed the bag since we read it; the next tick sends that change, then applies.
                return (false, null);
        }
    }
}
