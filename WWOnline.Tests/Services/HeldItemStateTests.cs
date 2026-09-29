using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// The local held-item state the puppet REL mirrors (puppet_held.c): the reported item during a draw /
/// put-away anim, the carried bomb, and the grab-kind range the server enforces.
/// </summary>
public class HeldItemStateTests
{
    private const uint Link = 0x80400000;
    private const uint Bomb = 0x80500000;

    [Theory]
    [InlineData(PuppetLayout.DAPY_UPPER_ANM_REST)]
    [InlineData(PuppetLayout.DAPY_UPPER_ANM_TAKE)]
    [InlineData(PuppetLayout.DAPY_UPPER_ANM_TAKEBOTH)]
    [InlineData(PuppetLayout.DAPY_UPPER_ANM_TAKEL)]
    [InlineData(PuppetLayout.DAPY_UPPER_ANM_TAKER)]
    public void DuringADrawOrPutAway_TheNextItemIsReported(int anim)
    {
        Assert.True(HeldItemState.IsEquipAnim((ushort)anim));
        Assert.Equal((ushort)0x27, HeldItemState.ReportedItem(0x100, 0x27, (ushort)anim));
    }

    [Theory]
    [InlineData(0xFFFF)] // no upper anim
    [InlineData(0x35)]   // BOOMWAIT
    [InlineData(0x95)]   // GRABWAIT
    [InlineData(0x107)]  // just past TAKER
    public void OtherwiseTheHeldItemIsReported(int anim)
    {
        Assert.False(HeldItemState.IsEquipAnim((ushort)anim));
        Assert.Equal((ushort)0x2D, HeldItemState.ReportedItem(0x2D, 0x100, (ushort)anim));
    }

    [Fact]
    public void ACarriedBomb_IsBombKind()
    {
        var game = new FakeDolphin();
        game.SetU32(Link + PuppetLayout.DAPY_OFF_GRAB_ACTOR, Bomb);
        game.Set(Bomb + PuppetLayout.FPC_OFF_PROC_NAME, 0x01, 0x28);
        Assert.Equal((byte)PuppetLayout.PUPPET_GRAB_KIND_BOMB, HeldItemState.ReadGrabKind(game, Link));
    }

    [Fact]
    public void ACarriedPot_OrNothing_IsNone()
    {
        var game = new FakeDolphin();
        Assert.Equal((byte)PuppetLayout.PUPPET_GRAB_KIND_NONE, HeldItemState.ReadGrabKind(game, Link));

        game.SetU32(Link + PuppetLayout.DAPY_OFF_GRAB_ACTOR, Bomb);
        game.Set(Bomb + PuppetLayout.FPC_OFF_PROC_NAME, 0x01, 0xCB); // TSUBO
        Assert.Equal((byte)PuppetLayout.PUPPET_GRAB_KIND_NONE, HeldItemState.ReadGrabKind(game, Link));
    }

    [Theory]
    [InlineData(0x00000128u)] // not a MEM1 pointer
    [InlineData(0x80500002u)] // misaligned
    [InlineData(0x90000000u)] // past MEM1
    public void AnImplausibleGrabPointer_IsNeverFollowed(uint pointer)
    {
        var game = new FakeDolphin();
        game.SetU32(Link + PuppetLayout.DAPY_OFF_GRAB_ACTOR, pointer);
        game.Set(pointer + PuppetLayout.FPC_OFF_PROC_NAME, 0x01, 0x28);
        Assert.Equal((byte)PuppetLayout.PUPPET_GRAB_KIND_NONE, HeldItemState.ReadGrabKind(game, Link));
    }

    [Fact]
    public void UnknownGrabKinds_AreClampedToNothing_NotRejected()
    {
        Assert.Equal((byte)PuppetLayout.PUPPET_GRAB_KIND_BOMB, HeldItemState.SanitizeGrabKind((byte)PuppetLayout.PUPPET_GRAB_KIND_BOMB));
        Assert.Equal((byte)PuppetLayout.PUPPET_GRAB_KIND_NONE, HeldItemState.SanitizeGrabKind(200));

        // A newer client's kind (e.g. a pot) keeps the update: only the kind is dropped.
        var puppet = new PuppetData { Equipment = new EquipmentState { GrabKind = (byte)(EquipmentState.MaxGrabKind + 1) } };
        Assert.True(puppet.IsValid());
        puppet.ClampUnknownValues();
        Assert.Equal((byte)PuppetLayout.PUPPET_GRAB_KIND_NONE, puppet.Equipment.GrabKind);

        puppet.Equipment.GrabKind = EquipmentState.MaxGrabKind;
        puppet.ClampUnknownValues();
        Assert.Equal(EquipmentState.MaxGrabKind, puppet.Equipment.GrabKind);
    }
}
