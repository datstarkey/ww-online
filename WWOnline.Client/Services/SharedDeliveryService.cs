using WWOnline.Hubs;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Shared delivery bag (room rule <see cref="RoomSettings.SharedDelivery"/>; the sync itself is
/// <see cref="SharedBagService{T}"/>). The server holds the room's bag: how many of each quest item, and which
/// were ever obtained (<see cref="DeliveryCounts"/>). A pushed bag is written into this one the way the game adds
/// and takes items (<see cref="DeliveryBag.Apply"/>): leaving items empty their slot, arriving items take the
/// first empty slot and set their obtained bit, and X/Y/Z buttons on a changed slot are fixed. The write is
/// compare-and-swap against a fresh read, only while no event runs and the menu is closed. Windfall's pedestals
/// (<see cref="DeliveryCounts.Pedestals"/>) ride along: read with the bag, so setting an item down or taking it
/// back is one change, and written after it (docs/side-quests.md §3.2). Why a presence store and not flags:
/// docs/delivery-bag.md.
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
        if (_bag?.Count() is not { } counts || DeliveryBagMemory.ReadPedestals(Dolphin) is not { } pedestals) return null;
        counts.Pedestals = pedestals;
        return counts;
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
                switch (DeliveryBagMemory.WritePedestals(Dolphin, current.Pedestals, target.Pedestals))
                {
                    case BagWriteResult.Written:
                        written.Pedestals = (byte[])target.Pedestals.Clone();
                        break;
                    default:
                        // The bag is written; the pedestals raced or failed: take what the game holds now as the sync
                        // point and keep the target to retry.
                        Logger.Warning("[delivery] writing the pedestals failed; retrying");
                        return (false, ReadCounts());
                }
                if (written.Counts.AsSpan().SequenceEqual(target.Counts))
                    Logger.Information("[delivery] shared bag {Total} → applied ({Delta}): {Slots}", target, target.Minus(current).DeltaText(), next);
                else
                    Logger.Warning("[delivery] shared bag {Total}: only {Applied} fits this bag ({Slots})", target, written, next);
                // The sync point's obtained bits are the room's: any this game has beyond them (its own, or an
                // arriving item's) are sent next tick, so the room's bits become everyone's.
                return (true, new DeliveryCounts(written.Counts, target.Obtained, written.Pedestals));
            case BagWriteResult.Failed:
                // Partly written, maybe: adopt what the bag now holds as the sync point (so our own half-write
                // isn't sent as a local change) and keep the target to retry.
                Logger.Warning("[delivery] writing the bag failed; retrying");
                return (false, ReadCounts());
            default:
                // Raced: the game changed the bag since we read it; the next tick sends that change, then applies.
                return (false, null);
        }
    }
}
