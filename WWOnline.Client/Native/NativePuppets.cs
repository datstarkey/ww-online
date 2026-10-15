using WWOnline.Shared.Models;

namespace WWOnline.Native;

/// <summary>
/// <see cref="PuppetData"/> to and from libtww's puppet structs: position, shape angle, speed, proc and place. Anims,
/// equipment and look go through <see cref="NativePuppetFidelity"/>; the boat has no native field yet.
/// </summary>
public static class NativePuppets
{
    /// <summary>The local Link as PuppetData, or null without a player actor.</summary>
    public static PuppetData? FromLocal(TwwLocalState state)
    {
        if (state.Valid == 0) return null;
        bool moving = state.SpeedF > 0.5f;
        return new PuppetData
        {
            Position = new Vector3(state.Pos[0], state.Pos[1], state.Pos[2]),
            Rotation = state.Angle[1],
            // The same derivations as PuppetSyncService.ReadLocalPlayerState's for Dolphin.
            Animation = new AnimationState { AnimationSpeed = moving ? 1.0f : 0.0f, UpperBodyAnimation = AnimTrack.None },
            Action = new ActionState
            {
                StateFlags = moving ? 1u : 0u,
                // daPy_PROC has ~220 values: a byte, as PuppetData carries it.
                CurProc = (byte)state.Proc,
                SpeedF = state.SpeedF,
            },
            StageName = state.Loc.Stage.ToString(),
            RoomNumber = unchecked((byte)state.Loc.Room),
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>A peer's puppet for tww_set_peer.</summary>
    public static TwwPeerState ToPeer(PuppetData puppet)
    {
        var peer = new TwwPeerState
        {
            SpeedF = puppet.Action.SpeedF,
            Proc = puppet.Action.CurProc,
            Stage = TwwStageName.From(puppet.StageName),
            Room = unchecked((sbyte)puppet.RoomNumber),
        };
        peer.Pos[0] = puppet.Position.X;
        peer.Pos[1] = puppet.Position.Y;
        peer.Pos[2] = puppet.Position.Z;
        // PuppetData's rotation is shape_angle.y, an s16 carried as a float.
        peer.Angle[1] = unchecked((short)(int)puppet.Rotation);
        return peer;
    }
}
