using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>The local boat in the local colours: what C# asks of the REL's block (BOATLOOK_* in puppet_shared.h).</summary>
public class LocalBoatLookTests
{
    private static LocalBoatLook.Part Part(uint key, uint sum = 7, uint liveSum = 7, bool resLive = true) =>
        new(Key: key, Sum: sum, ResLive: resLive, LiveSum: liveSum);

    [Fact]
    public void Sum_IsTheRelsChecksumOverBigEndianEntries()
    {
        // s = s * 31 + entry, from 0 (BOATLOOK_SUM_MUL)
        Assert.Equal((0x1234u * 31) + 0xABCD, LocalBoatLook.Sum([0x12, 0x34, 0xAB, 0xCD]));
        Assert.Equal(0u, LocalBoatLook.Sum([]));
    }

    [Fact]
    public void SailRgb_IsTheTunicColour_ZeroForTheDefault()
    {
        Assert.Equal(0x102030u, LocalBoatLook.SailRgb(new AppearanceState { ColorR = 0x10, ColorG = 0x20, ColorB = 0x30 }));
        Assert.Equal(0u, LocalBoatLook.SailRgb(new AppearanceState()));
        Assert.Equal(0u, LocalBoatLook.SailRgb(new AppearanceState
        {
            ColorR = (byte)PuppetLayout.TUNIC_COLOR_DEFAULT_R, ColorG = (byte)PuppetLayout.TUNIC_COLOR_DEFAULT_G,
            ColorB = (byte)PuppetLayout.TUNIC_COLOR_DEFAULT_B,
        }));
    }

    [Fact]
    public void NoLocalBoat_NeverNeedsTheRel() =>
        Assert.False(LocalBoatLook.NeedsRel(localBoat: false, 0x1234, 0x102030, null, null));

    [Fact]
    public void NoBlockYet_OnlyAColourNeedsTheRel()
    {
        Assert.False(LocalBoatLook.NeedsRel(true, 0, 0, null, null));
        Assert.True(LocalBoatLook.NeedsRel(true, 0x1234, 0, null, null));
        Assert.True(LocalBoatLook.NeedsRel(true, 0, 0x102030, null, null));
    }

    [Fact]
    public void AppliedColours_WithTheirChecksum_Match()
    {
        var hull = Part(LocalBoatLook.Key(0x1234));
        var sail = Part(LocalBoatLook.Key(0x102030));
        Assert.False(LocalBoatLook.NeedsRel(true, 0x1234, 0x102030, hull, sail));
    }

    [Fact]
    public void AnotherColour_OrAReloadedPalette_NeedsTheRel()
    {
        var sail = Part(0);
        Assert.True(LocalBoatLook.NeedsRel(true, 0x1234, 0, Part(LocalBoatLook.Key(0x4321)), sail));
        // The archive came back from disc (vanilla) where the REL had written the colour.
        Assert.True(LocalBoatLook.NeedsRel(true, 0x1234, 0, Part(LocalBoatLook.Key(0x1234), sum: 7, liveSum: 8), sail));
        // Unmounted and mounted elsewhere: the old bytes may still sum right, but the archive isn't the REL's.
        Assert.True(LocalBoatLook.NeedsRel(true, 0x1234, 0, Part(LocalBoatLook.Key(0x1234), resLive: false), sail));
    }

    [Fact]
    public void VanillaWanted_AfterAColour_NeedsTheRelToPutItBack()
    {
        Assert.True(LocalBoatLook.NeedsRel(true, 0, 0, Part(LocalBoatLook.Key(0x1234)), Part(0)));
        Assert.False(LocalBoatLook.NeedsRel(true, 0, 0, Part(0, sum: 1, liveSum: 2, resLive: false), Part(0)));
    }
}
