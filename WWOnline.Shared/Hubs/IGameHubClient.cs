using WWOnline.Shared.Models;

namespace WWOnline.Shared.Hubs;

/// <summary>
/// Strongly-typed SignalR client callback interface.
/// Both server and client reference this contract.
/// </summary>
public interface IGameHubClient
{
    Task PlayerJoined(string connectionId, string playerName);
    Task PlayerLeft(string connectionId);
    Task UpdatePlayerCount(int count);
    Task ReceivePlayerGameState(string senderConnectionId, GameState gameState);
    Task ReceivePuppetData(PuppetData puppetData);

    /// <summary>A stage slot's merged world flags changed (someone set new bits).</summary>
    Task ReceiveStageFlags(StageFlags flags);

    /// <summary>The shared wallet's new total (someone gained or spent rupees).</summary>
    Task ReceiveRupeeTotal(int total);

    /// <summary>The shared bait bag's new counts (someone used, bought or picked up bait or a Hyoi Pear).</summary>
    Task ReceiveBaitTotal(BaitCounts total);

    /// <summary>The shared spoils bag's new counts (someone picked up, sold or traded spoils).</summary>
    Task ReceiveSpoilsTotal(SpoilsCounts total);

    /// <summary>The shared delivery bag's new contents (someone received, handed over or traded a quest item).</summary>
    Task ReceiveDeliveryTotal(DeliveryCounts total);

    /// <summary>Room rules or the room owner changed.</summary>
    Task ReceiveRoomSettings(RoomSettings settings);

    /// <summary>The room inventory changed (someone gained something, or the room owner edited it).</summary>
    Task ReceiveRoomInventory(RoomInventory room);

    /// <summary>The room's shared story flags gained bits (someone progressed, or a joiner brought progress).</summary>
    Task ReceiveStoryFlags(StoryFlags room);

    /// <summary>Dungeon / room switches another player in the same save slot (dan) or room (zone) just set.</summary>
    Task ReceiveRoomSwitches(RoomSwitches switches);
    /// <summary>A player who can see us threw / exploded a bomb or fired their boat cannon (PlayerEvent.PlayerId = who).</summary>
    Task ReceivePlayerEvent(PlayerEvent evt);
}
