using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace WWOnline.Native;

/// <summary>
/// libtww's C API (tww-native <c>native/include/pc/tww.h</c>, <see cref="ApiVersion"/>), bound exactly.
/// <see cref="GameHost"/> loads the library on its game thread; the resolver it installs hands that handle
/// to every import here. Threads are as tww.h says: view calls on the UI thread, init / frames / the
/// multiplayer calls on the game thread, the rest anywhere.
/// </summary>
public static unsafe partial class TwwNative
{
    public const string LibraryName = "tww";
    public const uint ApiVersion = 2;
    public const int MaxPeers = 3;

    [LibraryImport(LibraryName)] public static partial uint tww_api_version();

    [LibraryImport(LibraryName)] public static partial IntPtr tww_view_create();
    [LibraryImport(LibraryName)] public static partial void tww_view_destroy(IntPtr view);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tww_init(IntPtr view, string disc, string? config);
    [LibraryImport(LibraryName)] public static partial int tww_run_frame();
    [LibraryImport(LibraryName)] public static partial void tww_shutdown();

    [LibraryImport(LibraryName)] public static partial void tww_set_size(uint width, uint height, float scale);
    [LibraryImport(LibraryName)] public static partial void tww_set_keys(TwwKeys keys);
    [LibraryImport(LibraryName)] public static partial void tww_set_fatal_callback(delegate* unmanaged<int, void> fn);
    [LibraryImport(LibraryName)] public static partial uint tww_frame_count();
    [LibraryImport(LibraryName)] public static partial ulong tww_present_count();
    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tww_save_surface_png(string path);
    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int tww_save_frame_png(string path);

    // API version 2: multiplayer. Game thread only, between tww_run_frame calls.
    [LibraryImport(LibraryName)] public static partial int tww_get_local_state(TwwLocalState* state);
    [LibraryImport(LibraryName)] public static partial int tww_get_location(TwwLocation* location);
    [LibraryImport(LibraryName)] public static partial void tww_set_peer(int slot, TwwPeerState* peer);
    [LibraryImport(LibraryName)] public static partial uint tww_save_size();
    [LibraryImport(LibraryName)] public static partial uint tww_save_read(uint offset, void* dst, uint n);
    [LibraryImport(LibraryName)] public static partial uint tww_save_write(uint offset, void* src, uint n);

    /// <summary>Leave the process without running libtww's static destructors (they assume a game that never ends).</summary>
    [LibraryImport("libc", EntryPoint = "_exit")] public static partial void ExitProcess(int code);
}

/// <summary>Keyboard pad bits (TWW_KEY_*, tww_set_keys).</summary>
[Flags]
public enum TwwKeys : uint
{
    None = 0,
    StickUp = 1u << 0,
    StickDown = 1u << 1,
    StickLeft = 1u << 2,
    StickRight = 1u << 3,
    CUp = 1u << 4,
    CDown = 1u << 5,
    CLeft = 1u << 6,
    CRight = 1u << 7,
    A = 1u << 8,
    B = 1u << 9,
    X = 1u << 10,
    Y = 1u << 11,
    Z = 1u << 12,
    L = 1u << 13,
    R = 1u << 14,
    Start = 1u << 15,
    DpadUp = 1u << 16,
    DpadDown = 1u << 17,
    DpadLeft = 1u << 18,
    DpadRight = 1u << 19,
}

/// <summary>TWW_LOC_* (TwwLocation.flags).</summary>
[Flags]
public enum TwwLocationFlags : uint
{
    None = 0,
    /// <summary>In play, no scene change pending: safe to sync and to show peers.</summary>
    Idle = 1u << 0,
    /// <summary>An event or cutscene is running.</summary>
    Event = 1u << 1,
}

[InlineArray(3)]
public struct TwwFloat3
{
    private float _element;
}

[InlineArray(3)]
public struct TwwShort3
{
    private short _element;
}

/// <summary>A NUL-terminated stage name (char[8]).</summary>
[InlineArray(8)]
public struct TwwStageName
{
    private byte _element;

    public override string ToString()
    {
        ReadOnlySpan<byte> bytes = this;
        int end = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end >= 0 ? bytes[..end] : bytes);
    }

    /// <summary>At most 7 ASCII characters (longer names are cut), NUL-padded.</summary>
    public static TwwStageName From(string? name)
    {
        var result = new TwwStageName();
        Span<byte> bytes = result;
        if (!string.IsNullOrEmpty(name))
            Encoding.ASCII.GetBytes(name.AsSpan(0, Math.Min(name.Length, bytes.Length - 1)), bytes);
        return result;
    }
}

[InlineArray(3)]
public struct TwwPad3
{
    private byte _element;
}

/// <summary>TwwLocation (16 bytes).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwLocation
{
    /// <summary>The current stage ("sea", "M_NewD2", ...); "" on the title and name screens.</summary>
    public TwwStageName Stage;
    /// <summary>dStage_roomControl_c::mStayNo, -1 if none.</summary>
    public sbyte Room;
    /// <summary>The stage layer, -1 if unknown.</summary>
    public sbyte Layer;
    /// <summary>The start point the stage was entered from.</summary>
    public short Point;
    public TwwLocationFlags Flags;
}

/// <summary>TwwLocalState (48 bytes): the local Link.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwLocalState
{
    /// <summary>0: no player actor (title, loading); every other field is then zero.</summary>
    public uint Valid;
    /// <summary>current.pos</summary>
    public TwwFloat3 Pos;
    /// <summary>shape_angle (s16 binary angles)</summary>
    public TwwShort3 Angle;
    public short Pad0;
    /// <summary>speedF</summary>
    public float SpeedF;
    /// <summary>mCurProc (daPy_lk_c::daPy_PROC)</summary>
    public uint Proc;
    public TwwLocation Loc;
}

/// <summary>TwwPeerState (40 bytes): one other player's puppet.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwPeerState
{
    public TwwFloat3 Pos;
    public TwwShort3 Angle;
    public short Pad0;
    public float SpeedF;
    public uint Proc;
    /// <summary>The puppet shows only while this equals the local stage.</summary>
    public TwwStageName Stage;
    public sbyte Room;
    public TwwPad3 Pad1;
}
