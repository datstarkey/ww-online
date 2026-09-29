using System.Buffers.Binary;
using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>The other players on the sea chart: the entries C# hands the REL (SEACHART_* in puppet_shared.h).</summary>
public class SeaChartMarkersTests
{
    private static PuppetData Player(string stage, float x, float z, short rot, byte r = 180, byte g = 30, byte b = 30) => new()
    {
        StageName = stage,
        Position = new Vector3 { X = x, Y = 0, Z = z },
        Rotation = rot,
        Appearance = new AppearanceState { ColorR = r, ColorG = g, ColorB = b },
    };

    private static float F(byte[] e, int o) => BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32BigEndian(e.AsSpan(o)));

    [Fact]
    public void OnlyPlayersOnTheSea_InOrder_WithTheirColourAndHeading()
    {
        var e = SeaChartMarkers.BuildEntries([
            Player("sea", 1000, -2000, 0x4000),
            Player("M_NewD2", 5, 5, 0),             // in a dungeon: not on the chart
            Player("sea", -300000, 250000, -0x2000, 0, 0, 0),
        ]);
        Assert.Equal(PuppetLayout.SEACHART_BLOCK_SIZE - PuppetLayout.SEACHART_OFF_ENTRY0, e.Length);

        Assert.Equal(1000f, F(e, PuppetLayout.SEACHART_E_OFF_X));
        Assert.Equal(-2000f, F(e, PuppetLayout.SEACHART_E_OFF_Z));
        Assert.Equal(0x4000, BinaryPrimitives.ReadInt16BigEndian(e.AsSpan(PuppetLayout.SEACHART_E_OFF_ANGLE)));
        Assert.Equal(PuppetLayout.SEACHART_FLAG_SHOW, e[PuppetLayout.SEACHART_E_OFF_FLAGS]);
        Assert.Equal(180, e[PuppetLayout.SEACHART_E_OFF_R]);

        int o = PuppetLayout.SEACHART_ENTRY_SIZE;
        Assert.Equal(-300000f, F(e, o + PuppetLayout.SEACHART_E_OFF_X));
        Assert.Equal(PuppetLayout.TUNIC_COLOR_DEFAULT_G, e[o + PuppetLayout.SEACHART_E_OFF_G]); // 0,0,0 = the vanilla tunic

        Assert.Equal(0, e[2 * PuppetLayout.SEACHART_ENTRY_SIZE + PuppetLayout.SEACHART_E_OFF_FLAGS]); // nobody else
    }

    [Fact]
    public void SailingPlayers_ShowWhereTheirBoatIs()
    {
        var p = Player("sea", 0, 0, 0);
        p.Boat = new BoatState { Position = new Vector3 { X = 7000, Z = 8000 }, Rotation = 0x1234 };
        var e = SeaChartMarkers.BuildEntries([p]);
        Assert.Equal(7000f, F(e, PuppetLayout.SEACHART_E_OFF_X));
        Assert.Equal(0x1234, BinaryPrimitives.ReadInt16BigEndian(e.AsSpan(PuppetLayout.SEACHART_E_OFF_ANGLE)));
    }

    [Fact]
    public void AtMost_MaxEntries()
    {
        var many = Enumerable.Range(0, 12).Select(i => Player("sea", i, i, 0));
        var e = SeaChartMarkers.BuildEntries(many);
        for (int i = 0; i < PuppetLayout.SEACHART_MAX_ENTRIES; i++)
            Assert.Equal(PuppetLayout.SEACHART_FLAG_SHOW, e[i * PuppetLayout.SEACHART_ENTRY_SIZE + PuppetLayout.SEACHART_E_OFF_FLAGS]);
    }

    [Fact]
    public void Publish_WritesTheBlock_AndAsksForTheRel_OnlyWhenBothAreOnTheSea()
    {
        var d = new FakeDolphin();
        const uint block = 0x80500000;
        d.Set(PuppetLayout.SEACHART_PTR_ADDR, 0x80, 0x50, 0x00, 0x00);
        d.Set(block, 0x53, 0x43, 0x48, 0x54); // "SCHT"
        byte[] boot = [0, 0, 0, 1, 2, 3, 4, 5]; // this boot's __OSStartTime, stamped in the block
        d.Set(GameMemoryAddresses.System.OSStartTime.Address, boot);
        d.Set(block + PuppetLayout.SEACHART_OFF_BOOT, boot);
        var players = new[] { Player("sea", 1, 2, 3) };

        Assert.True(SeaChartMarkers.Publish(d, "sea", players));
        Assert.Equal(PuppetLayout.SEACHART_FLAG_SHOW, d.Get(block + PuppetLayout.SEACHART_OFF_ENTRY0 + PuppetLayout.SEACHART_E_OFF_FLAGS));
        Assert.False(SeaChartMarkers.Publish(d, "Omori", players)); // we're indoors: no chart to draw on
        Assert.False(SeaChartMarkers.Publish(d, "sea", [Player("Abesso", 0, 0, 0)]));
    }
}
