namespace WWOnline.Shared.Models;

/// <summary>
/// Which remote players get a puppet, and which local moves count as a scene change. Shared by the
/// client (puppets) and the server (it relays a player's projectile events only to the players who
/// can see them).
///
/// The Great Sea is one stage ("sea") whose 7x7 grid squares are its rooms, islands included.
/// A room check there hides a player one square over, and treating a room change as a scene
/// change despawns every puppet at each border while sailing. So at sea a remote player is
/// shown by distance, and moving between squares keeps the puppets. Everywhere else a room
/// is a separate space (dungeon rooms, interiors), so the room check stays.
/// </summary>
public static class PuppetVisibility
{
    /// <summary>Stage name of the Great Sea (StageIDs: islands are rooms of it).</summary>
    public const string SeaStage = "sea";

    /// <summary>Sea grid square size: d_a_sea.cpp:70 (100000 units per square).</summary>
    public const float SeaSquareSize = 100000f;

    /// <summary>Horizontal distance within which a player in another sea square is shown.</summary>
    public const float SeaVisibleDistance = 30000f;

    /// <summary>A shown player is only hidden again past this distance. Each respawn is a full
    /// puppet (playerInit + heaps + boat), so a player hovering at the edge mustn't flicker.</summary>
    public const float SeaHideDistance = 35000f;

    public static bool IsSea(string stage) => stage == SeaStage;

    /// <summary>
    /// True when the local player's move needs the puppets despawned (stage change, or a room
    /// change outside the sea). A room change at sea is not a scene change.
    /// </summary>
    public static bool IsSceneChange(string prevStage, byte prevRoom, string stage, byte room) =>
        stage != prevStage || (room != prevRoom && !IsSea(stage));

    /// <summary>
    /// True when the remote player is where the local player can see them: same stage, and
    /// same room or (at sea) within <see cref="SeaVisibleDistance"/> horizontally
    /// (<see cref="SeaHideDistance"/> if they were already shown).
    /// </summary>
    public static bool IsSameLocation(string localStage, byte localRoom, float localX, float localZ, PuppetData puppet,
                                      bool wasVisible = false) =>
        IsSameLocation(localStage, localRoom, localX, localZ, puppet.StageName, puppet.RoomNumber, puppet.Position, wasVisible);

    /// <summary>The same check for a remote player known only by stage, room and position.</summary>
    public static bool IsSameLocation(string localStage, byte localRoom, float localX, float localZ,
                                      string? remoteStage, byte remoteRoom, Vector3? remotePosition, bool wasVisible = false)
    {
        if (string.IsNullOrEmpty(remoteStage) || remoteStage != localStage)
            return false;
        if (remoteRoom == localRoom)
            return true;
        if (!IsSea(localStage) || remotePosition == null || !remotePosition.IsFinite())
            return false;

        float dx = remotePosition.X - localX;
        float dz = remotePosition.Z - localZ;
        float range = wasVisible ? SeaHideDistance : SeaVisibleDistance;
        return dx * dx + dz * dz <= range * range;
    }
}
