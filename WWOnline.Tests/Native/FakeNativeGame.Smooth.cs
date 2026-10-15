using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The smooth API: each slot's send time last set.</summary>
internal sealed partial class FakeNativeGame : ITwwSmoothApi
{
    public double[] PeerTimes { get; } = new double[TwwNative.MaxPeers];

    public void SetPeerTime(int slot, double sentAt) => PeerTimes[slot] = sentAt;
}
