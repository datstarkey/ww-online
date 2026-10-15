using System.Runtime.InteropServices;

namespace WWOnline.Native;

/// <summary>
/// tww.h's smooth block (tww_smooth_*): the peers' puppets and boats are drawn from timed states, a little behind the
/// newest. Game thread only, like <see cref="ITwwApi"/>: reach it through <see cref="INativeGame.Post"/> and cast the api.
/// </summary>
public interface ITwwSmoothApi
{
    /// <summary>When the states the slot's next SetPeer / SetPeerBoat carry were sampled: seconds on the sender's clock
    /// (<see cref="TwwNative.SentAt"/>); 0 = unknown (stamped as they arrive).</summary>
    void SetPeerTime(int slot, double sentAt);
}

public static unsafe partial class TwwNative
{
    [LibraryImport(LibraryName)] public static partial void tww_smooth_set_time(int slot, double sentAt);

    /// <summary>A peer's <see cref="Shared.Models.PuppetData.Timestamp"/> as tww_smooth_set_time takes it: seconds since
    /// the Unix epoch; 0 for an unset one.</summary>
    public static double SentAt(DateTime timestamp) =>
        timestamp == default ? 0 : (timestamp - DateTime.UnixEpoch).TotalSeconds;
}
