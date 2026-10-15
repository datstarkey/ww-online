using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The puppet API (tww.h "Puppet fidelity"): the anims and looks last set, and a settable local snapshot.</summary>
internal sealed partial class FakeNativeGame : ITwwPuppetApi, INativePuppetGame
{
    public TwwPuppetAnim?[] Anims { get; } = new TwwPuppetAnim?[TwwNative.MaxPeers];
    public TwwPuppetLook?[] Looks { get; } = new TwwPuppetLook?[TwwNative.MaxPeers];
    public (TwwPuppetClothes Clothes, byte R, byte G, byte B)? LocalOutfit { get; private set; }
    public NativePuppetSnapshot? PuppetSnapshot { get; set; }

    public void SetPuppetAnim(int slot, TwwPuppetAnim? anim) => Anims[slot] = anim;

    public void SetPuppetLook(int slot, TwwPuppetLook? look) => Looks[slot] = look;

    public bool GetLocalPuppet(out TwwPuppetAnim anim, out TwwPuppetLook look)
    {
        anim = PuppetSnapshot?.Anim ?? default;
        look = PuppetSnapshot?.Look ?? default;
        return PuppetSnapshot != null;
    }

    public void SetLocalOutfit(TwwPuppetClothes clothes, byte r, byte g, byte b) => LocalOutfit = (clothes, r, g, b);
}
