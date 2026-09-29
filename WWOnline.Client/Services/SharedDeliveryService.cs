using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared delivery bag (room rule <see cref="RoomSettings.SharedDelivery"/>; the sync itself is
/// <see cref="SharedBagService{T}"/>). The server holds the room's bag: how many of each quest item, and which
/// were ever obtained (<see cref="DeliveryCounts"/>). A pushed bag is written into this one the way the game adds
/// and takes items (<see cref="DeliveryBag.Apply"/>): leaving items empty their slot, arriving items take the
/// first empty slot and set their obtained bit, and X/Y/Z buttons on a changed slot are fixed. The write is
/// compare-and-swap against a fresh read, only while no event runs and the menu is closed. Why a presence store
/// and not flags: docs/delivery-bag.md.
/// </summary>
public class SharedDeliveryService : SharedBagService<DeliveryCounts>
{
    private DeliveryBagSlots? _bag; // the bag as last read (what ApplyTarget writes over)

    public SharedDeliveryService(IDolphinService dolphin, SignalRClientService signalR, RoomSettingsService room)
        : base(dolphin, signalR, room) { }

    protected override string Tag => "delivery";
    protected override string What => "shared delivery bag";
    protected override bool RuleOn(RoomSettings rules) => rules.SharedDelivery;
    protected override bool OwnsBag() => DeliveryBagMemory.OwnsBag(Dolphin);
    protected override void Subscribe(Action<DeliveryCounts> onTotal) => SignalR.DeliveryTotalReceived += onTotal;
    protected override void Unsubscribe(Action<DeliveryCounts> onTotal) => SignalR.DeliveryTotalReceived -= onTotal;
    protected override Task<DeliveryCounts?> JoinAsync(DeliveryCounts current) => SignalR.JoinDeliveryAsync(current);
    protected override Task<bool> SendDeltaAsync(DeliveryCounts delta) => SignalR.SendDeliveryDeltaAsync(delta);
    protected override void OnReset() => _bag = null;

    protected override DeliveryCounts? ReadCounts()
    {
        _bag = DeliveryBagMemory.Read(Dolphin);
        return _bag?.Count();
    }

    protected override (bool Applied, DeliveryCounts? Baseline) ApplyTarget(DeliveryCounts current, DeliveryCounts target)
    {
        if (_bag is not { } bag) return (false, null);
        int equipped = DeliveryBag.EquippedMask(DeliveryBagMemory.ReadSelectSlots(Dolphin) ?? []);
        var next = DeliveryBag.Apply(bag, target, equipped);
        switch (DeliveryBagMemory.Write(Dolphin, bag, next))
        {
            case BagWriteResult.Written:
                var written = next.Count();
                if (written.Counts.AsSpan().SequenceEqual(target.Counts))
                    Logger.Information("[delivery] shared bag {Total} → applied ({Delta}): {Slots}", target, target.Minus(current).DeltaText(), next);
                else
                    Logger.Warning("[delivery] shared bag {Total}: only {Applied} fits this bag ({Slots})", target, written, next);
                // The sync point's obtained bits are the room's: any this game has beyond them (its own, or an
                // arriving item's) are sent next tick, so the room's bits become everyone's.
                return (true, new DeliveryCounts(written.Counts, target.Obtained));
            case BagWriteResult.Failed:
                // Partly written, maybe: adopt what the bag now holds as the sync point (so our own half-write
                // isn't sent as a local change) and keep the target to retry.
                Logger.Warning("[delivery] writing the bag failed; retrying");
                return (false, DeliveryBagMemory.Read(Dolphin)?.Count());
            default:
                // Raced: the game changed the bag since we read it; the next tick sends that change, then applies.
                return (false, null);
        }
    }
}
