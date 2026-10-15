using WWOnline.Native;

namespace WWOnline.Tests.Native;

/// <summary>The shared-inventory calls: <see cref="Inv"/> is play's state; adds and button writes land in it.</summary>
internal sealed partial class FakeNativeGame : ITwwInvApi
{
    public TwwInvPlay Inv;
    public int InvCalls;

    public TwwInvPlay InvGet()
    {
        InvCalls++;
        return Inv;
    }

    public void InvAdd(TwwInvCounter counter, int delta)
    {
        InvCalls++;
        Inv.Pending[(int)counter] += counter == TwwInvCounter.Rupee ? delta : (short)delta;
    }

    public void InvSetSelectItem(int button, byte itemNo)
    {
        InvCalls++;
        Inv.SelectItem[button] = itemNo;
    }
}
