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
    /// </summary>
    public const int ProtocolVersion = 2;

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
    public const string GetRoomSettings = "GetRoomSettings";
    public const string SetRoomSettings = "SetRoomSettings";
    public const string ClaimRoomOwner = "ClaimRoomOwner";
    public const string JoinRoomInventory = "JoinRoomInventory";
    public const string SendInventoryGains = "SendInventoryGains";
    public const string SetRoomInventory = "SetRoomInventory";
    public const string GetRoomInventory = "GetRoomInventory";
    public const string JoinRoomStory = "JoinRoomStory";
    public const string SendStoryFlags = "SendStoryFlags";

    // Client callback names (server → client)
    public const string ReceiveStageFlags = "ReceiveStageFlags";
    public const string ReceiveRupeeTotal = "ReceiveRupeeTotal";
    public const string ReceiveRoomSettings = "ReceiveRoomSettings";
    public const string ReceiveRoomInventory = "ReceiveRoomInventory";
    public const string ReceiveStoryFlags = "ReceiveStoryFlags";

    /// <summary>Largest wallet in the game (dSv_player_status_a_c wallet size 2).</summary>
    public const int MaxRupees = 5000;
}
