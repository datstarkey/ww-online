using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The pvp API: the slot mask last set, a queue of the local Link's hits to poll, and the peers' hits applied.</summary>
internal sealed partial class FakeNativeGame : ITwwPvpApi
{
    public uint PvpSlots;
    public Queue<TwwPvpHit> PvpLocalHits { get; } = new();
    public List<TwwPvpHit> PvpApplied { get; } = new();

    public void SetSlots(uint slotMask) => PvpSlots = slotMask;

    public bool PollHit(out TwwPvpHit hit) => PvpLocalHits.TryDequeue(out hit);

    public bool ApplyHit(in TwwPvpHit hit)
    {
        if ((PvpSlots & (1u << hit.Slot)) == 0) return false;
        PvpApplied.Add(hit);
        return true;
    }
}
