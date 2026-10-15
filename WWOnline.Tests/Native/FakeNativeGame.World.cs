using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>tww.h "world" without libtww: settable slot, zones and salvage points; the live batch and remote items as set.</summary>
internal sealed partial class FakeNativeGame : ITwwWorldApi
{
    public int SaveSlot { get; set; } = -1;
    public Dictionary<int, int> Zones { get; } = new();
    public TwwWorldSalvagePoint[] Salvage { get; set; } = [];
    public (int Slot, uint Mask) RemoteItems { get; private set; }
    public TwwWorldStats Stats { get; set; }
    public TwwWorldLiveBatch? LiveBatch { get; private set; }
    public TwwWorldLiveStatus LiveStatus;

    public int WorldSaveSlot() => SaveSlot;

    public int WorldRoomZone(int room) => Zones.GetValueOrDefault(room, -1);

    public int WorldSalvageRead(Span<TwwWorldSalvagePoint> points)
    {
        int n = Math.Min(points.Length, Salvage.Length);
        Salvage.AsSpan(0, n).CopyTo(points);
        return n;
    }

    public bool WorldSalvageSetFlag(int index, byte bits)
    {
        if (index >= Salvage.Length) return false;
        Salvage[index].Flag |= bits;
        return true;
    }

    public void WorldSetRemoteItems(int slot, uint mask) => RemoteItems = (slot, mask);

    public TwwWorldStats WorldStats() => Stats;

    public void WorldLivePost(in TwwWorldLiveBatch batch)
    {
        LiveBatch = batch;
        LiveStatus.Seq = batch.Seq;
    }

    public TwwWorldLiveStatus WorldLiveStatus() => LiveStatus;

    /// <summary>What the game's live pass does once it has handled the batch.</summary>
    public void LiveHandled(uint pokes, uint retry = 0)
    {
        LiveStatus.DoneSeq = LiveStatus.Seq;
        LiveStatus.PokeCount += pokes;
        LiveStatus.Retry = retry;
    }
}
