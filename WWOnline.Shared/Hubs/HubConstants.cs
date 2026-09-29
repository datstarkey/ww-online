namespace WWOnline.Shared.Hubs;

public static class HubConstants
{
    public const string HubPath = "/gamehub";
    public const int DefaultPort = 6969;

    /// <summary>
    /// The client/server wire protocol. A client and a room must have the same number to play
    /// together; the server rejects a <see cref="Join"/> with a different one (see
    /// <see cref="ProtocolCheck"/>). BUMP IT whenever a hub method, a callback or a DTO that
    /// crosses the wire changes shape or meaning (PuppetData layout, RoomInventory fields, flag
    /// masks...). Pure app changes that don't touch the wire keep it.
    /// 2: held items + boat parts. 3: live-world room switches. 4: player events / projectiles
    /// (SendPlayerEvent, ReceivePlayerEvent, RoomSettings.SharedProjectiles). 5 (v0.2.0): body / face
    /// anim mirror (AnimationState.Tracks and face / hand fields), the shared bait bag (JoinBait,
    /// SendBaitDelta, ReceiveBaitTotal, BaitCounts, RoomSettings.SharedBait), the shared spoils bag
    /// (JoinSpoils, SendSpoilsDelta, ReceiveSpoilsTotal, SpoilsCounts, RoomSettings.SharedSpoils) and warp
    /// to player (PuppetData.Warp, RoomSettings.AllowWarping). 6: Nintendo Gallery figurines in the room
    /// story (StoryFlags.Figurines), and the dungeon warp jars' open bits (StoryFlags.WarpJars).
    /// </summary>
    public const int ProtocolVersion = 6; // 2: held items (#6), boat parts (#7). 3: room switches (JoinRoomSwitches / SendRoomSwitches / ReceiveRoomSwitches). 4: player events / projectiles (SendPlayerEvent / ReceivePlayerEvent, SharedProjectiles). 5: anim mirror (AnimationState.Tracks, face, hands), shared bait + spoils bags (JoinBait / SendBaitDelta / ReceiveBaitTotal, SharedBait; JoinSpoils / SendSpoilsDelta / ReceiveSpoilsTotal, SharedSpoils), warp to player (PuppetData.Warp, AllowWarping). 6: figurines in the room story (StoryFlags.Figurines)

    // Hub method names (server-side methods invoked by clients)

    /// <summary>First call on every (re)connection: version check + player name. Returns a <see cref="Models.JoinResult"/>.</summary>
    public const string Join = "Join";

    /// <summary>Pre-protocol clients' join call. The server now only uses it to turn them away.</summary>
    public const string SetPlayerName = "SetPlayerName";
    public const string SendPuppetData = "SendPuppetData";
    public const string SendPlayerGameState = "SendPlayerGameState";
    public const string GetPlayers = "GetPlayers";
    public const string SendStageFlags = "SendStageFlags";
    public const string GetWorldFlags = "GetWorldFlags";
    public const string JoinWallet = "JoinWallet";
    public const string SendRupeeDelta = "SendRupeeDelta";

    /// <summary>Join the shared bait bag with this game's <see cref="Models.BaitCounts"/>; returns the room's
    /// counts to adopt (null: rule off, or waiting for the room owner to seed it).</summary>
    public const string JoinBait = "JoinBait";

    /// <summary>A signed <see cref="Models.BaitCounts"/> change (bait used / bought / picked up), while SharedBait is on.</summary>
    public const string SendBaitDelta = "SendBaitDelta";

    /// <summary>Join the shared spoils bag with this game's <see cref="Models.SpoilsCounts"/>; returns the room's
    /// counts to adopt (null: rule off, or waiting for the room owner to seed it).</summary>
    public const string JoinSpoils = "JoinSpoils";

    /// <summary>A signed <see cref="Models.SpoilsCounts"/> change (spoils picked up / sold / traded), while SharedSpoils is on.</summary>
    public const string SendSpoilsDelta = "SendSpoilsDelta";
    public const string GetRoomSettings = "GetRoomSettings";
    public const string SetRoomSettings = "SetRoomSettings";
    public const string ClaimRoomOwner = "ClaimRoomOwner";
    public const string JoinRoomInventory = "JoinRoomInventory";
    public const string SendInventoryGains = "SendInventoryGains";
    public const string SetRoomInventory = "SetRoomInventory";
    public const string GetRoomInventory = "GetRoomInventory";
    public const string JoinRoomStory = "JoinRoomStory";
    public const string SendStoryFlags = "SendStoryFlags";
    public const string JoinRoomSwitches = "JoinRoomSwitches";
    public const string SendRoomSwitches = "SendRoomSwitches";

    /// <summary>One of the caller's projectiles (a <see cref="Models.PlayerEvent"/>: bomb thrown / exploded,
    /// cannon fired, arrow shot). Relayed only to the players who can see the caller, while SharedProjectiles is on.</summary>
    public const string SendPlayerEvent = "SendPlayerEvent";

    // Client callback names (server → client)
    public const string ReceiveStageFlags = "ReceiveStageFlags";
    public const string ReceiveRupeeTotal = "ReceiveRupeeTotal";
    public const string ReceiveBaitTotal = "ReceiveBaitTotal";
    public const string ReceiveSpoilsTotal = "ReceiveSpoilsTotal";
    public const string ReceiveRoomSettings = "ReceiveRoomSettings";
    public const string ReceiveRoomInventory = "ReceiveRoomInventory";
    public const string ReceiveStoryFlags = "ReceiveStoryFlags";
    public const string ReceiveRoomSwitches = "ReceiveRoomSwitches";
    public const string ReceivePlayerEvent = "ReceivePlayerEvent";

    /// <summary>Largest wallet in the game (dSv_player_status_a_c wallet size 2).</summary>
    public const int MaxRupees = 5000;
}
