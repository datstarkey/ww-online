using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// The client's bookkeeping for live room switches (<see cref="RoomSwitchSyncService"/>), without any I/O
/// so it can be tested: where this game is, what it has sent, what the room holds, and what it may apply.
/// <list type="bullet">
/// <item><b>Place.</b> Dan switches belong to the stage visit, zone switches to the room, so a new room of
/// the same stage keeps the dan bookkeeping and a new stage starts from nothing.</item>
/// <item><b>Echo guard.</b> A bit the room sent us is never sent back (<see cref="Fresh"/>), and only
/// on-edges exist: a switch the game turns off again is never reported.</item>
/// <item><b>Apply once.</b> A room bit is written at most once per place, and never if this game has had it
/// set at this place (it may have turned it off itself, e.g. a torch the wind blew out): the room's copy
/// would re-latch it (<see cref="ToApply"/>).</item>
/// </list>
/// All bits are the table's syncable ones only: the caller masks what it reads and what it receives.
/// </summary>
public sealed class RoomSwitchTracker
{
    /// <summary>Where this game is (no bits), or null before the first <see cref="MoveTo"/>.</summary>
    public RoomSwitches? Place { get; private set; }

    /// <summary>The room joined the current place (<see cref="OnJoined"/>).</summary>
    public bool Joined { get; private set; }

    private RoomSwitches _sent = new();
    private RoomSwitches _received = new();
    private RoomSwitches _had = new(); // bits this game has had at this place: set by itself or applied by us

    /// <summary>
    /// This game is at <paramref name="place"/>. Returns true when that is a new place (then the caller must
    /// join it): a new stage forgets everything, a new room of the same stage forgets the zone part.
    /// </summary>
    public bool MoveTo(RoomSwitches place)
    {
        if (Place != null && Place.SamePlace(place)) return false;
        bool sameSlot = Place != null && Place.Slot == place.Slot && Place.Stage == place.Stage;
        var p = place.EmptyCopy();
        _sent = Rebase(_sent, p, sameSlot);
        _received = Rebase(_received, p, sameSlot);
        _had = Rebase(_had, p, sameSlot);
        Place = p;
        Joined = false;
        return true;
    }

    /// <summary>Leave (rule off, disconnected, left gameplay): the next <see cref="MoveTo"/> starts afresh.</summary>
    public void Reset()
    {
        Place = null;
        Joined = false;
        _sent = new RoomSwitches();
        _received = new RoomSwitches();
        _had = new RoomSwitches();
    }

    /// <summary>
    /// Rejoin the current place (a new connection: the server may have restarted), keeping what this game
    /// has had here so nothing is re-applied.
    /// </summary>
    public void Rejoin()
    {
        if (Place == null) return;
        Joined = false;
        _sent = Place.EmptyCopy();
        _received = Place.EmptyCopy();
    }

    /// <summary>The join of <paramref name="sent"/>'s place answered with <paramref name="state"/>. False (ignored) if we moved meanwhile.</summary>
    public bool OnJoined(RoomSwitches sent, RoomSwitches state)
    {
        if (Place == null || !Place.SamePlace(sent) || !Place.SamePlace(state)) return false;
        _sent.MergeFrom(sent);
        _received.MergeFrom(state);
        Joined = true;
        return true;
    }

    /// <summary>Bits another player set. False (ignored) unless they are for this place.</summary>
    public bool OnReceived(RoomSwitches bits)
    {
        if (Place == null || bits.Slot != Place.Slot || bits.Stage != Place.Stage) return false;
        if (bits.SamePlace(Place))
        {
            _received.MergeFrom(bits);
            return true;
        }
        // Same stage, another room of it: only its dan bits concern us.
        var dan = Place.EmptyCopy();
        Array.Copy(bits.Dan, dan.Dan, RoomSwitches.DanWords);
        _received.MergeFrom(dan);
        return !dan.IsEmpty;
    }

    /// <summary>This game's bits now: remember them (they are this game's own if the room didn't send them).</summary>
    public void Observe(RoomSwitches local) => _had.MergeFrom(local);

    /// <summary>Bits to report: set by this game and neither sent yet nor received from the room.</summary>
    public RoomSwitches Fresh(RoomSwitches local) => local.Except(_sent).Except(_received);

    public void MarkSent(RoomSwitches bits) => _sent.MergeFrom(bits);

    /// <summary>Report failed: send those bits again.</summary>
    public void Unsent(RoomSwitches bits) => _sent = _sent.Except(bits);

    /// <summary>Room bits to write into the game now: the room has them, this game doesn't and never had.</summary>
    public RoomSwitches ToApply(RoomSwitches local) => _received.Except(local).Except(_had);

    /// <summary>Bits actually written into the game (never applied again at this place).</summary>
    public void MarkApplied(RoomSwitches written) => _had.MergeFrom(written);

    private static RoomSwitches Rebase(RoomSwitches bits, RoomSwitches place, bool keepDan)
    {
        var r = place.EmptyCopy();
        if (keepDan) Array.Copy(bits.Dan, r.Dan, RoomSwitches.DanWords);
        return r;
    }
}
