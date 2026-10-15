using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The overlay API: names and markers last set, a local event queue to poll and a peer queue of
/// <see cref="PeerEventCapacity"/>.</summary>
internal sealed partial class FakeNativeGame : ITwwOverlayApi
{
    public string?[] PeerNames { get; } = new string?[TwwNative.MaxPeers];
    public TwwOverlaySeaMarker[] SeaMarkers { get; private set; } = [];
    public int SeaMarkerCalls;
    public bool EventSend;
    public Queue<TwwOverlayEvent> LocalEvents { get; } = new();
    public List<TwwOverlayEvent> PeerEvents { get; } = new();
    public int PeerEventCapacity = 16;

    public void SetPeerName(int slot, string? name) => PeerNames[slot] = name;

    public void SetSeaMarkers(ReadOnlySpan<TwwOverlaySeaMarker> markers)
    {
        SeaMarkers = markers.ToArray();
        Interlocked.Increment(ref SeaMarkerCalls); // after the store: tests wait on the count, then read the markers
    }

    public void SetEventSend(bool on) => EventSend = on;

    public bool PollEvent(out TwwOverlayEvent e) => LocalEvents.TryDequeue(out e);

    public bool PushPeerEvent(in TwwOverlayEvent e)
    {
        if (PeerEvents.Count >= PeerEventCapacity) return false;
        PeerEvents.Add(e);
        return true;
    }
}
