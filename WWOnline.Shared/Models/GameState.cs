using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// Complete game state for Wind Waker synchronization
/// </summary>
public class GameState
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("player")]
    public PlayerState Player { get; set; } = new();

    [JsonPropertyName("inventory")]
    public InventoryState Inventory { get; set; } = new();

    [JsonPropertyName("charts")]
    public byte[] Charts { get; set; } = new byte[32]; // Chart data from 0x4CDC to 0x4CFC

    [JsonPropertyName("eventFlags")]
    public byte[] EventFlags { get; set; } = new byte[256]; // Full event bitfield from 0x803C522C

    [JsonPropertyName("stageInfo")]
    public byte[] StageInfo { get; set; } = new byte[576]; // 16 stages x 36 bytes from 0x803C4F88

    [JsonPropertyName("stageName")]
    public string StageName { get; set; } = "";

    /// <summary>
    /// True when the player is loaded into a save (not on the title/menu screens).
    /// Derived from MaxHealth being non-zero — an unloaded save has zeroed stats.
    /// </summary>
    [JsonIgnore]
    public bool IsInGame => Player.MaxHealth > 0;

    [JsonPropertyName("checksum")]
    public uint Checksum { get; set; }

    public uint CalculateChecksum()
    {
        uint sum = 0;

        // Add player state values
        sum += Player.MaxHealth;
        sum += Player.CurrentHealth;
        sum += Player.RupeeCount;
        sum += (uint)(Player.CurrentSword << 8);
        sum += (uint)(Player.CurrentShield << 16);

        // Use bit representation for deterministic hashing of any float value
        sum += BitConverter.SingleToUInt32Bits(Player.PositionX);
        sum += BitConverter.SingleToUInt32Bits(Player.PositionY);
        sum += BitConverter.SingleToUInt32Bits(Player.PositionZ);

        // Add status flags
        sum += Player.StatusFlags1;
        sum += Player.StatusFlags2;

        // Add inventory items
        for (int i = 0; i < Inventory.Items.Length; i++)
        {
            sum += (uint)(Inventory.Items[i] << (i % 4 * 8));
        }

        // Add collection bitfields
        sum += (uint)(Inventory.SongsBitfield << 24);
        sum += Inventory.TriforceShards;
        sum += (uint)(Inventory.PearlsBitfield << 8);

        // Add bag contents
        for (int i = 0; i < Inventory.BagContents.Length; i++)
            sum += (uint)(Inventory.BagContents[i] << (i % 4 * 8));

        if (Charts != null)
            sum += SumBytesAsUint32(Charts);

        if (EventFlags != null)
            sum += SumBytesAsUint32(EventFlags);

        if (StageInfo != null)
            sum += SumBytesAsUint32(StageInfo);

        return sum;
    }

    public bool ValidateChecksum()
    {
        return Checksum == CalculateChecksum();
    }

    private static uint SumBytesAsUint32(byte[] data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4)
        {
            uint chunk = 0;
            for (int j = 0; j < 4 && i + j < data.Length; j++)
                chunk |= (uint)(data[i + j] << (j * 8));
            sum += chunk;
        }
        return sum;
    }

    public GameState Clone()
    {
        return new GameState
        {
            PlayerId = PlayerId,
            Timestamp = Timestamp,
            Player = Player.Clone(),
            Inventory = Inventory.Clone(),
            Charts = (byte[])Charts.Clone(),
            EventFlags = (byte[])EventFlags.Clone(),
            StageInfo = (byte[])StageInfo.Clone(),
            StageName = StageName,
            Checksum = Checksum
        };
    }

}
