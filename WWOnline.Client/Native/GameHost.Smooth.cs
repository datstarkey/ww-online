namespace WWOnline.Native;

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwSmoothApi
    {
        public void SetPeerTime(int slot, double sentAt) => TwwNative.tww_smooth_set_time(slot, sentAt);
    }
}
