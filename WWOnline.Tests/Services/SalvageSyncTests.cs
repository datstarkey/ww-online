using WWOnline.Data;
using WWOnline.Services;
using WWOnline.Shared.Models;
using Xunit;

namespace WWOnline.Tests.Services;

/// <summary>
/// Sunken treasure salvaged rides in the sea slot's world flags (<see cref="StageFlags.Ocean"/>,
/// <see cref="StageFlags.SalvagedCharts"/>), and a peer's salvage marks the matching registered salvage point done
/// live (<see cref="WorldFlagSyncService.SalvagedPoints"/>), the way a peer's chest opens live.
/// </summary>
public class SalvageSyncTests
{
    [Fact]
    public void SalvageBits_MergeGrowOnly_OnTheSeaSlotOnly()
    {
        var room = new StageFlags { Slot = StageFlags.SeaSlot };
        var peer = new StageFlags { Slot = StageFlags.SeaSlot };
        peer.Ocean[11] = 0x0004;
        peer.SalvagedCharts[1] = 0x1;
        Assert.True(peer.IsValid());
        Assert.Equal(2, peer.SalvageCount);
        Assert.False(peer.IsEmpty);

        Assert.True(room.MergeFrom(peer));
        Assert.False(room.MergeFrom(peer));
        Assert.Equal(0, room.Except(peer).SalvageCount);
        Assert.Equal(2, room.Except(new StageFlags { Slot = StageFlags.SeaSlot }).SalvageCount);
        Assert.Equal(2, room.Clone().SalvageCount);

        var dungeon = new StageFlags { Slot = 3 };
        Assert.True(dungeon.IsValid());
        dungeon.Ocean[0] = 1;
        Assert.False(dungeon.IsValid()); // salvage bits only belong to the sea
        Assert.False(new StageFlags { Slot = 0, Ocean = new ushort[3] }.IsValid());
    }

    private static byte[] Infos(params (sbyte Room, byte Save, byte Kind, byte Flag)[] points)
    {
        var buf = new byte[GameMemoryAddresses.Salvage.InfoCount * GameMemoryAddresses.Salvage.InfoSize];
        for (int i = 0; i < GameMemoryAddresses.Salvage.InfoCount; i++)
            buf[i * GameMemoryAddresses.Salvage.InfoSize + GameMemoryAddresses.Salvage.OffRoomNo] = 0xFF; // not registered
        for (int i = 0; i < points.Length; i++)
        {
            int o = i * GameMemoryAddresses.Salvage.InfoSize;
            buf[o + GameMemoryAddresses.Salvage.OffRoomNo] = (byte)points[i].Room;
            buf[o + GameMemoryAddresses.Salvage.OffSaveNo] = points[i].Save;
            buf[o + GameMemoryAddresses.Salvage.OffKind] = points[i].Kind;
            buf[o + GameMemoryAddresses.Salvage.OffFlag] = points[i].Flag;
        }
        return buf;
    }

    [Fact]
    public void SalvagedPoints_MatchEndSalvagesKeys()
    {
        var bits = new StageFlags { Slot = StageFlags.SeaSlot };
        bits.SalvagedCharts[1] = 1u << 1;  // chart 34 (1-based: bit 33)
        bits.Ocean[11] = 1 << 3;           // square 11, point 3
        var infos = Infos(
            (5, 34, 0, 0),    // 0: chart 34's treasure: done
            (5, 35, 0, 0),    // 1: another chart: no
            (11, 3, 2, 0),    // 2: square 11 point 3 (light ring): done
            (11, 3, 4, 0),    // 3: same point, night light kind: done
            (12, 3, 3, 0),    // 4: another square: no
            (11, 3, 6, 0),    // 5: kind 6 (event bit): never
            (11, 3, 3, 1),    // 6: already done here: nothing to write
            (-1, 34, 0, 0));  // 7: not registered
        Assert.Equal(new[] { 0, 2, 3 }, WorldFlagSyncService.SalvagedPoints(infos, bits).ToArray());
    }

    [Fact]
    public void TheSaveAddresses_AreDSvSaveCs()
    {
        Assert.Equal(GameMemoryAddresses.GameInfo + 0x5C0, GameMemoryAddresses.WorldFlags.Ocean);
        Assert.Equal(GameMemoryAddresses.Inventory.SeaMapCharts + 0x20, GameMemoryAddresses.WorldFlags.SalvagedCharts);
        Assert.Equal(100, GameMemoryAddresses.WorldFlags.OceanLength);
    }
}
