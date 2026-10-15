using System.Runtime.InteropServices;

namespace WWOnline.Native;

/// <summary>
/// tww.h's overlay block (tww_overlay_*): the peers' name tags, the other players on the sea chart, and projectiles
/// (a peer's bombs, cannonballs and arrows made real in the game; the local Link's reported). Game thread only, like
/// <see cref="ITwwApi"/>: reach it through <see cref="INativeGame.Invoke{T}"/> and cast the api.
/// </summary>
public interface ITwwOverlayApi
{
    /// <param name="name">Null or "": no name tag. Printable ASCII is drawn, other characters dropped.</param>
    void SetPeerName(int slot, string? name);
    /// <summary>Replaces the markers; at most <see cref="TwwNative.OverlaySeaMarkers"/> are shown.</summary>
    void SetSeaMarkers(ReadOnlySpan<TwwOverlaySeaMarker> markers);
    /// <summary>Report the local Link's projectiles (off until set).</summary>
    void SetEventSend(bool on);
    /// <summary>The oldest unread local event, taken off the game's queue.</summary>
    bool PollEvent(out TwwOverlayEvent e);
    /// <returns>False: the game's queue is full or a value is out of range.</returns>
    bool PushPeerEvent(in TwwOverlayEvent e);
}

public static unsafe partial class TwwNative
{
    public const int OverlayNameBytes = 24;
    public const int OverlaySeaMarkers = 8;

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial void tww_overlay_set_peer_name(int slot, string? utf8);
    [LibraryImport(LibraryName)] public static partial void tww_overlay_set_sea_markers(TwwOverlaySeaMarker* markers, int count);
    [LibraryImport(LibraryName)] public static partial void tww_overlay_set_event_send(int on);
    [LibraryImport(LibraryName)] public static partial int tww_overlay_poll_event(TwwOverlayEvent* e);
    [LibraryImport(LibraryName)] public static partial int tww_overlay_push_peer_event(TwwOverlayEvent* e);
}

/// <summary>TwwOverlaySeaMarker (16 bytes): one other player on the sea chart.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwOverlaySeaMarker
{
    /// <summary>World position on the sea stage (the boat's while sailing).</summary>
    public float X;
    public float Z;
    /// <summary>Heading (s16 binary angle).</summary>
    public short AngleY;
    /// <summary>Marker colour (the tunic's).</summary>
    public byte R;
    public byte G;
    public byte B;
    public TwwPad3 Pad0;
}

/// <summary>TwwOverlayEvent (40 bytes): a projectile event, the same fields as the REL's events block.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TwwOverlayEvent
{
    /// <summary>TWW_OVERLAY_EVENT_*: the REL's PUPPET_FX_KIND_* values.</summary>
    public byte Kind;
    /// <summary>A peer's event: the sender's slot; a local event: 0.</summary>
    public byte Slot;
    /// <summary>An arrow's type (0 normal .. 3 light); else 0.</summary>
    public byte Variant;
    /// <summary>A local event: the projectile's room, -1 if unknown; a peer's: unused.</summary>
    public sbyte Room;
    /// <summary>The sender's projectile (its process id there).</summary>
    public uint Id;
    public TwwFloat3 Pos;
    public float SpeedF;
    public float SpeedY;
    public float Gravity;
    /// <summary>current.angle.x, current.angle.y, shape_angle.z.</summary>
    public TwwShort3 Angle;
    /// <summary>A bomb: fuse frames left; a cannonball: frames without gravity.</summary>
    public short Timer;
}
