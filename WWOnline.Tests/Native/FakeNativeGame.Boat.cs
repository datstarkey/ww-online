using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The boat half: a settable local boat, and the peers' boats and the local look last set.</summary>
internal sealed partial class FakeNativeGame : ITwwBoatApi
{
    public TwwBoatState? LocalBoat { get; set; }
    public TwwBoatState?[] PeerBoats { get; } = new TwwBoatState?[TwwNative.MaxPeers];
    public TwwBoatLook?[] PeerBoatLooks { get; } = new TwwBoatLook?[TwwNative.MaxPeers];
    public TwwBoatLook? LocalBoatLook;
    public int SetLocalBoatLookCalls;

    public bool GetLocalBoat(out TwwBoatState state)
    {
        state = LocalBoat ?? default;
        return LocalBoat != null;
    }

    public void SetPeerBoat(int slot, TwwBoatState? boat, TwwBoatLook? look)
    {
        PeerBoats[slot] = boat;
        PeerBoatLooks[slot] = look;
    }

    public void SetLocalBoatLook(TwwBoatLook? look)
    {
        SetLocalBoatLookCalls++;
        LocalBoatLook = look;
    }
}
