namespace WWOnline.Native;

public sealed unsafe partial class GameHost
{
    private sealed partial class TwwApi : ITwwInvApi
    {
        public TwwInvPlay InvGet()
        {
            TwwInvPlay play;
            TwwNative.tww_inv_get(&play);
            return play;
        }

        public void InvAdd(TwwInvCounter counter, int delta) => TwwNative.tww_inv_add(counter, delta);
        public void InvSetSelectItem(int button, byte itemNo) => TwwNative.tww_inv_set_select_item((uint)button, itemNo);
    }
}
