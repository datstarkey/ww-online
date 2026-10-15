namespace WWOnline.Native;

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwOverlayApi
    {
        public void SetPeerName(int slot, string? name) => TwwNative.tww_overlay_set_peer_name(slot, name);

        public void SetSeaMarkers(ReadOnlySpan<TwwOverlaySeaMarker> markers)
        {
            fixed (TwwOverlaySeaMarker* p = markers) TwwNative.tww_overlay_set_sea_markers(p, markers.Length);
        }

        public void SetEventSend(bool on) => TwwNative.tww_overlay_set_event_send(on ? 1 : 0);

        public bool PollEvent(out TwwOverlayEvent e)
        {
            e = default;
            fixed (TwwOverlayEvent* p = &e) return TwwNative.tww_overlay_poll_event(p) == 1;
        }

        public bool PushPeerEvent(in TwwOverlayEvent e)
        {
            var copy = e;
            return TwwNative.tww_overlay_push_peer_event(&copy) == 1;
        }
    }
}
