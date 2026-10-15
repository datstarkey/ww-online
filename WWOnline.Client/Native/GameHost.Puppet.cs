namespace WWOnline.Native;

public sealed unsafe partial class GameHost : INativePuppetGame
{
    private volatile NativePuppetSnapshot? _puppetSnapshot;

    public NativePuppetSnapshot? PuppetSnapshot => _puppetSnapshot;

    private void PublishPuppet() =>
        _puppetSnapshot = _api.GetLocalPuppet(out var anim, out var look)
            ? new NativePuppetSnapshot(TwwNative.tww_frame_count(), anim, look)
            : null;

    private sealed partial class TwwApi : ITwwPuppetApi
    {
        public void SetPuppetAnim(int slot, TwwPuppetAnim? anim)
        {
            if (anim is not { } a)
            {
                TwwNative.tww_puppet_set_anim(slot, null);
                return;
            }
            TwwNative.tww_puppet_set_anim(slot, &a);
        }

        public void SetPuppetLook(int slot, TwwPuppetLook? look)
        {
            if (look is not { } l)
            {
                TwwNative.tww_puppet_set_look(slot, null);
                return;
            }
            TwwNative.tww_puppet_set_look(slot, &l);
        }

        public bool GetLocalPuppet(out TwwPuppetAnim anim, out TwwPuppetLook look)
        {
            anim = default;
            look = default;
            fixed (TwwPuppetAnim* a = &anim)
            fixed (TwwPuppetLook* l = &look)
                return TwwNative.tww_puppet_get_local(a, l) == 1;
        }

        public void SetLocalOutfit(TwwPuppetClothes clothes, byte r, byte g, byte b) =>
            TwwNative.tww_puppet_set_local_outfit((byte)clothes, r, g, b);
    }
}
