using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WWOnline.Native;

// tww.h "world": the shared world outside the save window. Game thread only, between frames.
public static unsafe partial class TwwNative
{
    public const int WorldLiveWords = 9;
    public const int WorldSalvageMax = 160;

    [LibraryImport(LibraryName)] public static partial int tww_world_save_slot();
    [LibraryImport(LibraryName)] public static partial int tww_world_room_zone(int room);
    [LibraryImport(LibraryName)] public static partial uint tww_world_salvage_read(TwwWorldSalvagePoint* points, uint max);
    [LibraryImport(LibraryName)] public static partial int tww_world_salvage_set_flag(uint index, byte bits);
    [LibraryImport(LibraryName)] public static partial void tww_world_set_remote_items(int slot, uint mask);
    [LibraryImport(LibraryName)] public static partial void tww_world_get_stats(TwwWorldStats* stats);
    [LibraryImport(LibraryName)] public static partial void tww_world_live_post(TwwWorldLiveBatch* batch);
    [LibraryImport(LibraryName)] public static partial void tww_world_live_status(TwwWorldLiveStatus* status);
}

/// <summary>The world half of tww.h. The real API class implements it beside <see cref="ITwwApi"/>.</summary>
public interface ITwwWorldApi
{
    /// <summary>The current stage's save slot (dStage_stagInfo_GetSaveTbl), -1 without Link or mid stage change.</summary>
    int WorldSaveSlot();
    /// <summary>dStage_roomControl_c::getZoneNo(room), -1 if none.</summary>
    int WorldRoomZone(int room);
    /// <returns>Points copied: 0 while there is no salvage actor.</returns>
    int WorldSalvageRead(Span<TwwWorldSalvagePoint> points);
    bool WorldSalvageSetFlag(int index, byte bits);
    void WorldSetRemoteItems(int slot, uint mask);
    TwwWorldStats WorldStats();
    void WorldLivePost(in TwwWorldLiveBatch batch);
    TwwWorldLiveStatus WorldLiveStatus();
}

/// <summary>TwwWorldSalvagePoint (4 bytes): dSalvage_control_c::mInfo[i].</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwWorldSalvagePoint
{
    /// <summary>mRoomNo, -1: not registered.</summary>
    public sbyte Room;
    public byte SaveNo;
    public byte Kind;
    /// <summary>mFlag (bit 0: salvaged).</summary>
    public byte Flag;
}

[InlineArray(TwwNative.WorldLiveWords)]
public struct TwwWorldLiveBits
{
    private uint _element;
}

/// <summary>TwwWorldLiveBatch (48 bytes): bits other players set here, for the actors that read them only at create.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwWorldLiveBatch
{
    /// <summary>Even and new; the status's DoneSeq becomes it once handled.</summary>
    public uint Seq;
    public int Slot;
    /// <summary>The room of the zone switches 0xC0-0xEF, or 0xFF.</summary>
    public int ZoneRoom;
    /// <summary>Word 0: chests, words 1-8: switches 0x00-0xFF.</summary>
    public TwwWorldLiveBits Bits;
}

/// <summary>TwwWorldLiveStatus (16 bytes).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwWorldLiveStatus
{
    public uint Seq;
    public uint DoneSeq;
    public uint PokeCount;
    public uint Retry;
}

/// <summary>TwwWorldStats (8 bytes): the placed-item despawns so far.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwWorldStats
{
    public uint DespawnCount;
    /// <summary>(slot &lt;&lt; 16) | (item no &lt;&lt; 8) | item bit.</summary>
    public uint LastDespawn;
}
