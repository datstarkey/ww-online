using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// What another player needs to warp to this one ("Warp to" on the Room page), sent in
/// <see cref="PuppetData.Warp"/>. In the same stage and room the warper is simply moved to
/// <see cref="PuppetData.Position"/>; anywhere else they load <see cref="PuppetData.StageName"/> at the
/// spawn point this player entered it through (play.mCurStage, dStage_startStage_c, d_stage.h).
/// </summary>
public class WarpInfo
{
    /// <summary>The spawn point (a PLYR entry's id, 0..255) this player last entered <see cref="PuppetData.StageName"/>
    /// through; -1 = unknown (they respawned there after a void-out or a game over, or the app started there).</summary>
    [JsonPropertyName("entryPoint")]
    public short EntryPoint { get; set; } = -1;

    /// <summary>The room that spawn point is in (0..63); -1 = unknown.</summary>
    [JsonPropertyName("entryRoom")]
    public sbyte EntryRoom { get; set; } = -1;

    /// <summary>The layer the stage was entered with (-1 = the game picks it from the story and time of day).</summary>
    [JsonPropertyName("layer")]
    public sbyte Layer { get; set; } = -1;

    /// <summary>Why nobody should warp to this player right now, or <see cref="WarpBusy.None"/>.</summary>
    [JsonPropertyName("busy")]
    public WarpBusy Busy { get; set; }

    /// <summary>PLYR ids are a u8 (dStage_playerInit compares (u8)angle.z).</summary>
    public const short MaxEntryPoint = 255;
    /// <summary>dStage_roomControl_c::mStatus[64].</summary>
    public const sbyte MaxRoom = 63;
    /// <summary>Stage layers are 0..11 (ACT0..ACTb); -1 = default.</summary>
    public const sbyte MaxLayer = 15;

    /// <summary>The entrance is known: a warper can load the stage there.</summary>
    [JsonIgnore]
    public bool HasEntry => EntryPoint >= 0 && EntryRoom >= 0;

    public bool IsValid() =>
        EntryPoint >= -1 && EntryPoint <= MaxEntryPoint &&
        EntryRoom >= -1 && EntryRoom <= MaxRoom &&
        Layer >= -1 && Layer <= MaxLayer &&
        Enum.IsDefined(Busy);
}

/// <summary>Why a player can't be warped to right now (<see cref="WarpInfo.Busy"/>).</summary>
public enum WarpBusy : byte
{
    None = 0,
    /// <summary>A cutscene, a conversation or another event runs.</summary>
    Event = 1,
    /// <summary>Between areas: a stage change is pending, or the scene isn't loaded.</summary>
    Loading = 2,
    /// <summary>Link is down (no hearts left).</summary>
    Dead = 3,
    /// <summary>A minigame runs.</summary>
    Minigame = 4,
    /// <summary>They control another character (Medli, Makar, a seagull...).</summary>
    OtherCharacter = 5,
}
