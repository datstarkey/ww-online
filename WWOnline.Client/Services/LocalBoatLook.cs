using System.Text;
using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The local King of Red Lions in the local player's colours, the way the other players see it: the hull in
/// the boat colour, the sail in the tunic colour (GameMod puppet_boatlook.c, BOATLOOK_* in puppet_shared.h).
/// The REL recolours the "Ship" archive's palettes in place, and the recolour stays after the REL unloads.
/// The client writes the wanted colours into the REL's block, and <see cref="PuppetSyncService"/> keeps a
/// parked puppet while the local boat doesn't show them yet (<see cref="NeedsRel"/>).
/// </summary>
public static class LocalBoatLook
{
    /// <summary>One part's state in the block (BOATLOOK_P_OFF_*), and what the game shows now: whether the
    /// archive the REL wrote is still mounted there (<paramref name="ResLive"/>) and the live palette's checksum.</summary>
    public readonly record struct Part(uint Key, uint Sum, bool ResLive, uint LiveSum);

    /// <summary>The sail colour wanted: the tunic colour, 0 (vanilla cloth) for the default.</summary>
    public static uint SailRgb(AppearanceState a) =>
        LocalAppearance.IsDefaultColor(a.ColorR, a.ColorG, a.ColorB) ? 0 : (uint)(a.ColorR << 16 | a.ColorG << 8 | a.ColorB);

    /// <summary>BOATLOOK_P_OFF_KEY for a wanted colour: 0 = vanilla.</summary>
    public static uint Key(uint want) => want == 0 ? 0 : want | PuppetLayout.BOATLOOK_KEY_SET;

    /// <summary>BOATLOOK_SUM over big-endian u16 entries.</summary>
    public static uint Sum(ReadOnlySpan<byte> palette)
    {
        uint s = 0;
        for (int i = 0; i + 1 < palette.Length; i += 2)
            s = unchecked(s * PuppetLayout.BOATLOOK_SUM_MUL + (uint)(palette[i] << 8 | palette[i + 1]));
        return s;
    }

    /// <summary>Does the live palette show <paramref name="want"/>? A vanilla wish is met while the REL has
    /// written nothing; a colour once its key is in and the archive and palette are still what it left.</summary>
    public static bool Shows(uint want, in Part p) =>
        p.Key == Key(want) && (p.Key == 0 || (p.ResLive && p.LiveSum == p.Sum));

    /// <summary>
    /// Is the REL needed to make the local boat show <paramref name="hull"/> / <paramref name="sail"/>?
    /// Only while the local boat exists; with no block yet, only for a colour (vanilla needs nothing).
    /// </summary>
    public static bool NeedsRel(bool localBoat, ushort hull, uint sail, Part? hullPart, Part? sailPart)
    {
        if (!localBoat)
            return false;
        if (hullPart is not { } h || sailPart is not { } s)
            return hull != 0 || sail != 0;
        return !Shows(hull, h) || !Shows(sail, s);
    }

    /// <summary>
    /// Write the wanted colours into the REL's block (only where they differ) and say whether the REL is
    /// needed (<see cref="NeedsRel"/>).
    /// </summary>
    public static bool Publish(IDolphinService dolphin, bool localBoat, ushort hull, uint sail)
    {
        if (BootStampedBlock.Read(dolphin, PuppetLayout.BOATLOOK_PTR_ADDR, PuppetLayout.BOATLOOK_MAGIC,
                PuppetLayout.BOATLOOK_BLOCK_SIZE, PuppetLayout.BOATLOOK_HEADER_SIZE) is not { } block)
            return NeedsRel(localBoat, hull, sail, null, null);

        var b = block.Contents;
        if ((b[PuppetLayout.BOATLOOK_OFF_WANT_HULL] << 8 | b[PuppetLayout.BOATLOOK_OFF_WANT_HULL + 1]) != hull)
            dolphin.WriteMemory(block.Address + PuppetLayout.BOATLOOK_OFF_WANT_HULL, [(byte)(hull >> 8), (byte)hull]);
        if (BootStampedBlock.ReadU32(b, PuppetLayout.BOATLOOK_OFF_WANT_SAIL) != sail)
            dolphin.WriteMemory(block.Address + PuppetLayout.BOATLOOK_OFF_WANT_SAIL, BootStampedBlock.U32(sail));
        if (!localBoat)
            return false;

        uint resTable = ShipResTable(dolphin, BootStampedBlock.ReadU32(b, PuppetLayout.BOATLOOK_OFF_INFO));
        return NeedsRel(localBoat, hull, sail,
            ReadPart(dolphin, b, PuppetLayout.BOATLOOK_OFF_HULL, resTable, PuppetLayout.BOATLOOK_RES_HULL),
            ReadPart(dolphin, b, PuppetLayout.BOATLOOK_OFF_SAIL, resTable, PuppetLayout.BOATLOOK_RES_SAIL));
    }

    /// <summary>The mRes table of the dRes_info_c at <paramref name="info"/> while it is the mounted "Ship"
    /// archive, else 0.</summary>
    private static uint ShipResTable(IDolphinService dolphin, uint info)
    {
        var res = GameMemoryAddresses.Res.ObjectInfo;
        if (info < res || info >= res + GameMemoryAddresses.Res.ObjectInfoCount * GameMemoryAddresses.Res.InfoSize ||
            (info - res) % GameMemoryAddresses.Res.InfoSize != 0 ||
            dolphin.ReadMemory(info, GameMemoryAddresses.Res.InfoSize) is not { Length: GameMemoryAddresses.Res.InfoSize } e)
            return 0;
        int nul = Array.IndexOf(e, (byte)0, GameMemoryAddresses.Res.InfoOffsetName);
        bool ship = nul > 0 && Encoding.ASCII.GetString(e, 0, nul) == "Ship";
        bool mounted = (e[GameMemoryAddresses.Res.InfoOffsetCount] | e[GameMemoryAddresses.Res.InfoOffsetCount + 1]) != 0 &&
                       BootStampedBlock.ReadU32(e, GameMemoryAddresses.Res.InfoOffsetArchive) != 0;
        return ship && mounted ? BootStampedBlock.ReadU32(e, GameMemoryAddresses.Res.InfoOffsetRes) : 0;
    }

    private static Part ReadPart(IDolphinService dolphin, byte[] block, int offset, uint resTable, int resIndex)
    {
        uint palette = BootStampedBlock.ReadU32(block, offset + PuppetLayout.BOATLOOK_P_OFF_PAL);
        uint key = BootStampedBlock.ReadU32(block, offset + PuppetLayout.BOATLOOK_P_OFF_KEY);
        uint sum = BootStampedBlock.ReadU32(block, offset + PuppetLayout.BOATLOOK_P_OFF_SUM);
        uint count = BootStampedBlock.ReadU32(block, offset + PuppetLayout.BOATLOOK_P_OFF_COUNT);
        uint res = BootStampedBlock.ReadU32(block, offset + PuppetLayout.BOATLOOK_P_OFF_RES);
        if (key == 0)
            return new Part(key, sum, false, 0);

        bool resLive = resTable != 0 && BootStampedBlock.IsValidAddress(resTable + (uint)resIndex * 4, 4) &&
                       dolphin.ReadMemory(resTable + (uint)resIndex * 4, 4) is { Length: 4 } r && res != 0 &&
                       BootStampedBlock.ReadU32(r, 0) == res;
        uint liveSum = 0;
        if (resLive && count is > 0 and <= PuppetLayout.BOATLOOK_PAL_ENTRIES &&
            BootStampedBlock.IsValidAddress(palette, (int)count * 2) &&
            dolphin.ReadMemory(palette, (int)count * 2) is { } live && live.Length == count * 2)
            liveSum = Sum(live);
        return new Part(key, sum, resLive, liveSum);
    }
}
