using WWOnline.Services;
using WWOnline.Shared.Models;

namespace WWOnline.Native;

/// <summary>
/// <see cref="BoatState"/> to and from libtww's boat structs, and a player's boat colours (the Dolphin path's
/// PuppetSyncService.ReadLocalBoat / WriteBoat and LocalBoatLook).
/// </summary>
public static class NativeBoats
{
    /// <summary>The local boat as BoatState, or null if it isn't valid. Angles are clamped into the ranges the game
    /// keeps them in (as ParseShipPose), so a surprise value never gets the whole puppet rejected.</summary>
    public static BoatState? FromLocal(TwwBoatState s)
    {
        var flags = s.Flags;
        var boat = new BoatState
        {
            Position = new Vector3(s.Pos[0], s.Pos[1], s.Pos[2]),
            Rotation = s.AngleY,
            SpeedF = s.SpeedF,
            Flying = flags.HasFlag(TwwBoatFlags.Flying),
            Parked = flags.HasFlag(TwwBoatFlags.Parked),
            MastRaised = flags.HasFlag(TwwBoatFlags.MastRaised),
            MastHidden = flags.HasFlag(TwwBoatFlags.MastHidden),
            MastFrame = s.MastFrame,
            SailAngle = Math.Clamp(s.SailAngle, (short)-BoatState.MaxSailAngle, BoatState.MaxSailAngle),
            Tiller = Math.Clamp(s.Tiller, (short)-BoatState.MaxTiller, BoatState.MaxTiller),
            HeadX = Math.Clamp(s.HeadX, BoatState.MinHeadX, BoatState.MaxHeadX),
            HeadY = Math.Clamp(s.HeadY, (short)-BoatState.MaxHeadY, BoatState.MaxHeadY),
            HeadBck = BoatState.IsHeadBck(s.HeadBck) ? s.HeadBck : (byte)0,
            HeadFrame = s.HeadFrame,
            Part = s.Part <= BoatState.PartCrane ? s.Part : BoatState.PartWait,
        };
        // On the sea, the square it's in (daShip_c keeps its player's stay room on open water).
        string stage = s.Stage.ToString();
        byte room = PuppetVisibility.IsSea(stage) ? PuppetVisibility.SeaRoom(s.Pos[0], s.Pos[2]) : unchecked((byte)s.Room);
        boat.RoomNumber = room <= BoatState.MaxRoom ? room : (byte)0;
        if (boat.Part == BoatState.PartCannon)
        {
            boat.CannonYaw = s.CannonYaw;
            boat.CannonPitch = Math.Clamp(s.CannonPitch, (short)0, BoatState.MaxCannonPitch);
        }
        else if (boat.Part == BoatState.PartCrane)
        {
            boat.CraneAngle = Math.Clamp(s.CraneAngle, (short)-BoatState.MaxCraneAngle, (short)BoatState.MaxCraneAngle);
            boat.RopeLength = Math.Min(s.RopeLength, BoatState.MaxRopeLength);
        }
        return boat.IsValid() ? boat : null;
    }

    /// <summary>A peer's boat for tww_boat_set_peer, in <paramref name="stage"/> (the peer's).</summary>
    public static TwwBoatState ToPeer(BoatState boat, string? stage)
    {
        var s = new TwwBoatState
        {
            SpeedF = boat.SpeedF,
            AngleY = boat.Rotation,
            SailAngle = boat.SailAngle,
            Tiller = boat.Tiller,
            HeadX = boat.HeadX,
            HeadY = boat.HeadY,
            CannonYaw = boat.CannonYaw,
            CannonPitch = boat.CannonPitch,
            CraneAngle = boat.CraneAngle,
            Flags = (boat.Parked ? TwwBoatFlags.Parked : 0) | (boat.Flying ? TwwBoatFlags.Flying : 0) |
                    (boat.MastRaised ? TwwBoatFlags.MastRaised : 0) | (boat.MastHidden ? TwwBoatFlags.MastHidden : 0),
            Part = boat.Part,
            MastFrame = boat.MastFrame,
            HeadBck = boat.HeadBck,
            HeadFrame = boat.HeadFrame,
            RopeLength = boat.RopeLength,
            Room = unchecked((sbyte)boat.RoomNumber),
            Stage = TwwStageName.From(stage),
        };
        s.Pos[0] = boat.Position.X;
        s.Pos[1] = boat.Position.Y;
        s.Pos[2] = boat.Position.Z;
        return s;
    }

    /// <summary>A player's boat colours: the hull in their boat colour, the sail in their tunic colour.</summary>
    public static TwwBoatLook Look(AppearanceState a) => new()
    {
        HullRgb565 = PuppetSyncService.BoatColor565(a),
        SailRgb = LocalBoatLook.SailRgb(a),
    };

    /// <summary>
    /// The boat to show for a peer: their ridden boat with their puppet, a parked one where it is
    /// (<paramref name="parkedInSight"/>, PuppetSyncService.ParkedBoatInSight), else none.
    /// </summary>
    public static BoatState? Shown(PuppetData puppet, bool puppetVisible, bool parkedInSight) =>
        puppet.Boat is not { } boat || !boat.IsValid() ? null
        : boat.Parked ? (parkedInSight ? boat : null)
        : puppetVisible ? boat : null;
}
