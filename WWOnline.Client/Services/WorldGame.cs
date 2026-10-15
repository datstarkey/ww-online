using WWOnline.Data;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>A registered salvage point (dSalvage_control_c::mInfo[i]): the keys end_salvage writes.</summary>
public readonly record struct SalvagePoint(sbyte Room, byte SaveNo, byte Kind, byte Flag);

/// <summary>
/// The game state the world sync needs outside the save window: the current stage's save slot, a room's zone,
/// the salvage points, and the live passes' inputs (the remote item mask, the live-world batch). On Dolphin that
/// is GameCube memory and the puppet REL's scratch words (<see cref="DolphinWorldGame"/>); natively, libtww's
/// tww_world_* calls (<see cref="Native.NativeWorldGame"/>). Null / false: unreadable now.
/// </summary>
public interface IWorldGame
{
    /// <summary>The current stage's save slot (dStage_stagInfo_GetSaveTbl).</summary>
    int? CurrentSlot();

    /// <summary>Stage name, save slot and stay room (no bits), or null if unreadable or not valid.</summary>
    RoomSwitches? ReadPlace();

    /// <summary>dStage_roomControl_c::mStatus[room].mZoneNo, unchecked.</summary>
    int? RoomZoneNo(int room);

    /// <summary>All salvage points (registered or not), or null before there is a salvage control.</summary>
    IReadOnlyList<SalvagePoint>? ReadSalvagePoints();

    /// <summary>OR <paramref name="bits"/> into point <paramref name="index"/>'s mFlag.</summary>
    bool OrSalvageFlag(int index, byte bits);

    /// <summary>The item bits other players collected in <paramref name="slot"/>, for the live despawn (0: none).</summary>
    void SetRemoteItems(int slot, uint mask);

    /// <summary>The live despawns so far and the last one ((slot &lt;&lt; 16) | (item &lt;&lt; 8) | bit).</summary>
    (uint Count, uint Last)? ReadDespawns();

    /// <summary>The live-world block's state (<see cref="LiveWorldPoke.Read"/>), or null when there is none.</summary>
    LiveWorldPoke.BlockState? ReadLiveWorld();

    /// <summary>Hand the live pass a batch; <paramref name="seq"/> becomes DONE_SEQ once it is handled.</summary>
    void PublishLiveWorld(LiveWorldPoke.BlockState block, uint seq, int slot, int zoneRoom, uint[] bits);
}

/// <summary>
/// <see cref="IWorldGame"/> on Dolphin: the stage info and room status in GameCube memory, the salvage control on
/// the game heap, and the puppet REL's WORLDSYNC scratch words and live-world block.
/// </summary>
public sealed class DolphinWorldGame(IDolphinService dolphin, DespawnWorker? despawnWorker = null) : IWorldGame
{
    public int? CurrentSlot()
    {
        var ptrBytes = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.StagInfoPtr, 4);
        if (ptrBytes == null) return null;
        uint p = ReadU32(ptrBytes, 0);
        if (p < 0x80000000 || p >= 0x84000000) return null;
        var b = dolphin.ReadMemory(p + GameMemoryAddresses.WorldFlags.StagSaveTblOffset, 1);
        if (b == null) return null;
        int slot = (b[0] >> 1) & 0x7F;
        return slot < StageFlags.SlotCount ? slot : null;
    }

    public RoomSwitches? ReadPlace()
    {
        var name = dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentStageName.Address, 8);
        var room = dolphin.ReadMemory(GameMemoryAddresses.Stage.CurrentRoomNumber.Address, 1);
        var ptr = dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.StagInfoPtr, 4);
        if (name == null || room == null || ptr is not { Length: 4 }) return null;
        uint stag = ReadU32(ptr, 0);
        if (stag < 0x80000000 || stag >= 0x84000000) return null;
        var prop = dolphin.ReadMemory(stag + GameMemoryAddresses.WorldFlags.StagSaveTblOffset, 1);
        if (prop == null) return null;

        int nul = Array.IndexOf(name, (byte)0);
        var place = new RoomSwitches
        {
            Stage = System.Text.Encoding.ASCII.GetString(name, 0, nul >= 0 ? nul : name.Length),
            Slot = (prop[0] >> 1) & 0x7F,
            Room = room[0],
        };
        return place.IsValid() ? place : null;
    }

    public int? RoomZoneNo(int room) =>
        dolphin.ReadMemory(GameMemoryAddresses.WorldFlags.RoomZoneNo(room), 1) is [byte b] ? (sbyte)b : null;

    public IReadOnlyList<SalvagePoint>? ReadSalvagePoints()
    {
        if (InfoBase() is not uint infos) return null;
        var buf = dolphin.ReadMemory(infos, GameMemoryAddresses.Salvage.InfoCount * GameMemoryAddresses.Salvage.InfoSize);
        return buf == null ? null : ParseSalvagePoints(buf);
    }

    public bool OrSalvageFlag(int index, byte bits)
    {
        if (InfoBase() is not uint infos) return false;
        uint addr = infos + (uint)(index * GameMemoryAddresses.Salvage.InfoSize + GameMemoryAddresses.Salvage.OffFlag);
        return dolphin.ReadMemory(addr, 1) is [byte flag] && dolphin.WriteMemory(addr, [(byte)(flag | bits)]);
    }

    /// <summary>dSalvage_control_c::mInfo as the game lays it out (0x38 bytes each).</summary>
    public static SalvagePoint[] ParseSalvagePoints(byte[] infos)
    {
        var points = new SalvagePoint[infos.Length / GameMemoryAddresses.Salvage.InfoSize];
        for (int i = 0; i < points.Length; i++)
        {
            int o = i * GameMemoryAddresses.Salvage.InfoSize;
            points[i] = new SalvagePoint((sbyte)infos[o + GameMemoryAddresses.Salvage.OffRoomNo],
                infos[o + GameMemoryAddresses.Salvage.OffSaveNo], infos[o + GameMemoryAddresses.Salvage.OffKind],
                infos[o + GameMemoryAddresses.Salvage.OffFlag]);
        }
        return points;
    }

    /// <summary>
    /// Publish the mask for the REL's live despawn (puppet_worldsync.c). The tag is cleared first and set last so the
    /// REL never pairs a new mask with an old stage (it re-checks the tag after reading the mask).
    /// </summary>
    public void SetRemoteItems(int slot, uint mask)
    {
        despawnWorker?.MarkChanged(dolphin); // the REL must rescan before its result counts again
        WriteU32(PuppetLayout.WORLDSYNC_STAGE_TAG_ADDR, 0);
        WriteU32(PuppetLayout.WORLDSYNC_ITEM_MASK_ADDR, mask);
        if (mask != 0)
            WriteU32(PuppetLayout.WORLDSYNC_STAGE_TAG_ADDR, (uint)PuppetLayout.WORLDSYNC_TAG_MAGIC | (uint)slot);
    }

    public (uint Count, uint Last)? ReadDespawns() =>
        dolphin.ReadMemory(PuppetLayout.WORLDSYNC_DESPAWN_COUNT_ADDR, 8) is { Length: 8 } c ? (ReadU32(c, 0), ReadU32(c, 4)) : null;

    public LiveWorldPoke.BlockState? ReadLiveWorld() => LiveWorldPoke.Read(dolphin);

    public void PublishLiveWorld(LiveWorldPoke.BlockState block, uint seq, int slot, int zoneRoom, uint[] bits) =>
        LiveWorldPoke.Write(dolphin, block.Address, seq, slot, zoneRoom, bits);

    // daSalvage_c::mTagData_p, the dSalvage_control_c: not built yet before the first sea since boot.
    private uint? InfoBase()
    {
        var ptr = dolphin.ReadMemory(GameMemoryAddresses.Salvage.TagDataPtr, 4);
        if (ptr is not { Length: 4 }) return null;
        uint ctl = ReadU32(ptr, 0);
        return ctl is >= 0x80000000 and < 0x81800000 ? ctl + GameMemoryAddresses.Salvage.InfoBase : null;
    }

    private void WriteU32(uint addr, uint v) =>
        dolphin.WriteMemory(addr, [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v]);

    private static uint ReadU32(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
}
