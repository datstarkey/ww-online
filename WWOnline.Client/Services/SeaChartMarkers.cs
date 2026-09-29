using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The other players on the sea chart (puppet_seachart.c, SEACHART_* in puppet_shared.h): every player on the
/// Great Sea stage, visible as a puppet or not, goes into the REL's game-heap block, and the REL draws Link's
/// chart marker once per entry in their tunic colour. While sailing, their boat's position and heading.
/// </summary>
public static class SeaChartMarkers
{
    /// <summary>The entry region (SEACHART_OFF_ENTRY0..SEACHART_BLOCK_SIZE) for these players: the first
    /// SEACHART_MAX_ENTRIES of them on the sea, the rest zero (not shown).</summary>
    public static byte[] BuildEntries(IEnumerable<PuppetData> players)
    {
        var entries = new byte[PuppetLayout.SEACHART_MAX_ENTRIES * PuppetLayout.SEACHART_ENTRY_SIZE];
        int n = 0;
        foreach (var p in players)
        {
            if (n >= PuppetLayout.SEACHART_MAX_ENTRIES) break;
            if (p.StageName != GameMemoryAddresses.Sea.SeaStageName) continue;
            var pos = p.Boat?.Position ?? p.Position;
            if (pos == null || !pos.IsFinite()) continue;
            short angle = p.Boat?.Rotation ?? (short)p.Rotation;
            int o = n * PuppetLayout.SEACHART_ENTRY_SIZE;
            BigEndian.WriteF32(entries, o + PuppetLayout.SEACHART_E_OFF_X, pos.X);
            BigEndian.WriteF32(entries, o + PuppetLayout.SEACHART_E_OFF_Z, pos.Z);
            BigEndian.WriteS16(entries, o + PuppetLayout.SEACHART_E_OFF_ANGLE, angle);
            entries[o + PuppetLayout.SEACHART_E_OFF_FLAGS] = PuppetLayout.SEACHART_FLAG_SHOW;
            var a = p.Appearance ?? new AppearanceState();
            bool vanilla = a.ColorR == 0 && a.ColorG == 0 && a.ColorB == 0;
            entries[o + PuppetLayout.SEACHART_E_OFF_R] = vanilla ? (byte)PuppetLayout.TUNIC_COLOR_DEFAULT_R : a.ColorR;
            entries[o + PuppetLayout.SEACHART_E_OFF_G] = vanilla ? (byte)PuppetLayout.TUNIC_COLOR_DEFAULT_G : a.ColorG;
            entries[o + PuppetLayout.SEACHART_E_OFF_B] = vanilla ? (byte)PuppetLayout.TUNIC_COLOR_DEFAULT_B : a.ColorB;
            n++;
        }
        return entries;
    }

    /// <summary>
    /// Write <paramref name="players"/> into the REL's block (only when it differs). Returns true when the REL is
    /// needed for the chart: this game is on the sea and another player is too (the caller then keeps a puppet,
    /// parked if need be, so the REL is loaded whenever the menu opens).
    /// </summary>
    public static bool Publish(IDolphinService dolphin, string localStage, IReadOnlyList<PuppetData> players)
    {
        var entries = BuildEntries(players);
        bool anyone = entries.Where((_, i) => i % PuppetLayout.SEACHART_ENTRY_SIZE == PuppetLayout.SEACHART_E_OFF_FLAGS)
            .Any(f => (f & PuppetLayout.SEACHART_FLAG_SHOW) != 0);
        if (BootStampedBlock.Read(dolphin, PuppetLayout.SEACHART_PTR_ADDR, PuppetLayout.SEACHART_MAGIC,
                PuppetLayout.SEACHART_BLOCK_SIZE) is { } block &&
            !block.Contents.AsSpan(PuppetLayout.SEACHART_OFF_ENTRY0).SequenceEqual(entries))
            dolphin.WriteMemory(block.Address + PuppetLayout.SEACHART_OFF_ENTRY0, entries);
        return anyone && localStage == GameMemoryAddresses.Sea.SeaStageName;
    }
}

/// <summary>Big-endian writes into a byte buffer (the game's byte order).</summary>
internal static class BigEndian
{
    public static void WriteF32(byte[] b, int o, float v) => WriteU32(b, o, BitConverter.SingleToUInt32Bits(v));

    public static void WriteS16(byte[] b, int o, short v)
    {
        b[o] = (byte)(v >> 8);
        b[o + 1] = (byte)v;
    }

    public static void WriteU32(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }
}
