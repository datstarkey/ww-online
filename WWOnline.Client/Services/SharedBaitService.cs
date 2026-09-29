using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared bait bag (room rule <see cref="RoomSettings.SharedBait"/>; the sync itself is
/// <see cref="SharedBagService{T}"/>). The server holds one All-Purpose Bait count and one Hyoi Pear count
/// (<see cref="BaitCounts"/>). A pushed total is written into this bag with the fewest slot changes, the way
/// the game stacks them (<see cref="BaitBag.Apply"/>). The write is compare-and-swap: the bag is re-read just
/// before writing and nothing is written if the game changed it since (the local change is then sent first,
/// and the server's next total applied after). The sync point after a write is what was written, so a use the
/// game makes right after is still a local change.
/// </summary>
public class SharedBaitService : SharedBagService<BaitCounts>
{
    private BaitBagSlots? _bag; // the bag as last read (what ApplyTarget writes over)

    public SharedBaitService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
        : base(dolphin, signalR, room) { }

    protected override string Tag => "bait";
    protected override string What => "shared bait bag";
    protected override bool RuleOn(RoomSettings rules) => rules.SharedBait;
    protected override bool OwnsBag() => BaitBagMemory.OwnsBag(Dolphin);
    protected override void Subscribe(Action<BaitCounts> onTotal) => SignalR.BaitTotalReceived += onTotal;
    protected override void Unsubscribe(Action<BaitCounts> onTotal) => SignalR.BaitTotalReceived -= onTotal;
    protected override Task<BaitCounts?> JoinAsync(BaitCounts current) => SignalR.JoinBaitAsync(current);
    protected override Task<bool> SendDeltaAsync(BaitCounts delta) => SignalR.SendBaitDeltaAsync(delta);
    protected override void OnReset() => _bag = null;

    protected override BaitCounts? ReadCounts()
    {
        _bag = BaitBagMemory.Read(Dolphin);
        return _bag?.Count();
    }

    protected override (bool Applied, BaitCounts? Baseline) ApplyTarget(BaitCounts current, BaitCounts target)
    {
        if (_bag is not { } bag) return (false, null);
        int equipped = BaitBag.EquippedMask(BaitBagMemory.ReadSelectSlots(Dolphin) ?? []);
        var next = BaitBag.Apply(bag, target, equipped);
        switch (BaitBagMemory.Write(Dolphin, bag, next))
        {
            case BagWriteResult.Written:
                var written = next.Count();
                if (written.Equals(target))
                    Logger.Information("[bait] shared bag {Total} → applied ({Delta}): {Slots}", target, target.Minus(current).DeltaText(), next);
                else
                    Logger.Warning("[bait] shared bag {Total}: only {Applied} fits this bag ({Slots})", target, written, next);
                return (true, written);
            case BagWriteResult.Failed:
                // Partly written, maybe: adopt what the bag now holds as the sync point (so our own
                // half-write isn't sent as a local change) and keep the target to retry.
                Logger.Warning("[bait] writing the bag failed; retrying");
                return (false, BaitBagMemory.Read(Dolphin)?.Count());
            default:
                // Raced: the game changed the bag since we read it; the next tick sends that change, then applies.
                return (false, null);
        }
    }
}
