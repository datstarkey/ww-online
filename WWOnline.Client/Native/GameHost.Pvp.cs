namespace WWOnline.Native;

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwPvpApi
    {
        public void SetSlots(uint slotMask) => TwwNative.tww_pvp_set_slots(slotMask);

        public bool PollHit(out TwwPvpHit hit)
        {
            hit = default;
            fixed (TwwPvpHit* p = &hit) return TwwNative.tww_pvp_poll_hit(p) == 1;
        }

        public bool ApplyHit(in TwwPvpHit hit)
        {
            var copy = hit;
            return TwwNative.tww_pvp_apply_hit(&copy) == 1;
        }
    }
}
