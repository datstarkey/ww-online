using WWOnline.Services;

namespace WWOnline.Native;

/// <summary>
/// The overlay services' data to and from libtww's overlay API (<see cref="ITwwOverlayApi"/>): name tags and sea
/// chart markers from <see cref="PuppetSyncService"/>, projectile events for <see cref="PlayerEventService"/> as the
/// same <see cref="PlayerEventBlock.Raw"/> records the REL's events block holds. Game thread (inside Post / Invoke).
/// </summary>
public static class NativeOverlay
{
    /// <summary>Each slot's name tag ("" = none) and the other players on the sea chart.</summary>
    public static void Publish(ITwwApi api, IReadOnlyList<string> names, IReadOnlyList<SeaChartMarkers.Marker> markers)
    {
        if (api is not ITwwOverlayApi overlay) return;
        for (int i = 0; i < TwwNative.MaxPeers; i++)
            overlay.SetPeerName(i, i < names.Count ? names[i] : null);
        Span<TwwOverlaySeaMarker> native = stackalloc TwwOverlaySeaMarker[Math.Min(markers.Count, TwwNative.OverlaySeaMarkers)];
        for (int i = 0; i < native.Length; i++)
        {
            var m = markers[i];
            native[i] = new TwwOverlaySeaMarker { X = m.X, Z = m.Z, AngleY = m.Angle, R = m.R, G = m.G, B = m.B };
        }
        overlay.SetSeaMarkers(native);
    }

    /// <summary>Report the local Link's projectiles or not, then take the reported ones.</summary>
    public static List<PlayerEventBlock.Raw> ReadEvents(ITwwApi api, bool send)
    {
        var local = new List<PlayerEventBlock.Raw>();
        if (api is not ITwwOverlayApi overlay) return local;
        overlay.SetEventSend(send);
        while (overlay.PollEvent(out var e))
            local.Add(ToRaw(e));
        return local;
    }

    /// <summary>Queue peers' events in the game, in order, until it refuses one. Returns how many it took.</summary>
    public static int WriteEvents(ITwwApi api, IReadOnlyList<PlayerEventBlock.Raw> events)
    {
        if (api is not ITwwOverlayApi overlay) return 0;
        int n = 0;
        while (n < events.Count && overlay.PushPeerEvent(FromRaw(events[n])))
            n++;
        return n;
    }

    public static PlayerEventBlock.Raw ToRaw(in TwwOverlayEvent e) => new(
        e.Kind, e.Slot, e.Variant, e.Id, e.Pos[0], e.Pos[1], e.Pos[2], e.SpeedF, e.SpeedY, e.Gravity,
        e.Angle[0], e.Angle[1], e.Angle[2], e.Timer, e.Room);

    public static TwwOverlayEvent FromRaw(PlayerEventBlock.Raw r)
    {
        var e = new TwwOverlayEvent
        {
            Kind = r.Kind,
            Slot = r.Slot,
            Variant = r.Variant,
            Room = r.Room,
            Id = r.Id,
            SpeedF = r.SpeedF,
            SpeedY = r.SpeedY,
            Gravity = r.Gravity,
            Timer = r.Timer,
        };
        e.Pos[0] = r.X;
        e.Pos[1] = r.Y;
        e.Pos[2] = r.Z;
        e.Angle[0] = r.AngleX;
        e.Angle[1] = r.AngleY;
        e.Angle[2] = r.AngleZ;
        return e;
    }
}
