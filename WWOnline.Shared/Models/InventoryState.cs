using System.Numerics;
using System.Text.Json.Serialization;

namespace WWOnline.Shared.Models;

/// <summary>
/// Represents the player's inventory in Wind Waker
/// </summary>
public class InventoryState
{
    // Inventory slots (21 items)
    [JsonPropertyName("items")]
    public byte[] Items { get; set; } = new byte[21];

    // Item ownership flags
    [JsonPropertyName("itemOwnership")]
    public byte[] ItemOwnership { get; set; } = new byte[21];

    // Equipment progression bitfields
    [JsonPropertyName("swordBitfield")]
    public byte SwordBitfield { get; set; }

    [JsonPropertyName("shieldBitfield")]
    public byte ShieldBitfield { get; set; }

    [JsonPropertyName("powerBraceletsBitfield")]
    public byte PowerBraceletsBitfield { get; set; }

    [JsonPropertyName("piratesCharmBitfield")]
    public byte PiratesCharmBitfield { get; set; }

    [JsonPropertyName("herosCharmBitfield")]
    public byte HerosCharmBitfield { get; set; }

    // Bag contents (spoils, bait, delivery bag items)
    [JsonPropertyName("bagContents")]
    public byte[] BagContents { get; set; } = new byte[24];

    // Collection bitfields
    [JsonPropertyName("songsBitfield")]
    public ushort SongsBitfield { get; set; }

    [JsonPropertyName("triforceShards")]
    public byte TriforceShards { get; set; }

    [JsonPropertyName("pearlsBitfield")]
    public byte PearlsBitfield { get; set; }

    // Item slot indices
    public const int TELESCOPE = 0;
    public const int SAIL = 1;
    public const int WIND_WAKER = 2;
    public const int GRAPPLING_HOOK = 3;
    public const int SPOILS_BAG = 4;
    public const int BOOMERANG = 5;
    public const int DEKU_LEAF = 6;
    public const int TINGLE_TUNER = 7;
    public const int PICTO_BOX = 8;
    public const int IRON_BOOTS = 9;
    public const int MAGIC_ARMOR = 10;
    public const int BAIT_BAG = 11;
    public const int BOW = 12;
    public const int BOMBS = 13;
    public const int BOTTLE_1 = 14;
    public const int BOTTLE_2 = 15;
    public const int BOTTLE_3 = 16;
    public const int BOTTLE_4 = 17;
    public const int DELIVERY_BAG = 18;
    public const int HOOKSHOT = 19;
    public const int SKULL_HAMMER = 20;

    // Helper properties
    public bool HasItem(int index) => index >= 0 && index < Items.Length && index < ItemOwnership.Length && ItemOwnership[index] > 0;
    public byte GetItem(int index) => index >= 0 && index < Items.Length ? Items[index] : (byte)0;

    // Song checks
    public bool HasWindsRequiem => (SongsBitfield & 0x01) != 0;
    public bool HasBalladOfGales => (SongsBitfield & 0x02) != 0;
    public bool HasCommandMelody => (SongsBitfield & 0x04) != 0;
    public bool HasEarthGodsLyric => (SongsBitfield & 0x08) != 0;
    public bool HasWindGodsAria => (SongsBitfield & 0x10) != 0;
    public bool HasSongOfPassing => (SongsBitfield & 0x20) != 0;

    // Pearl checks
    public bool HasNayrusPearl => (PearlsBitfield & 0x01) != 0;
    public bool HasDinsPearl => (PearlsBitfield & 0x02) != 0;
    public bool HasFaroresPearl => (PearlsBitfield & 0x04) != 0;

    public int TriforceShardCount => BitOperations.PopCount(TriforceShards);

    public InventoryState Clone()
    {
        var clone = (InventoryState)MemberwiseClone();
        clone.Items = (byte[])Items.Clone();
        clone.ItemOwnership = (byte[])ItemOwnership.Clone();
        clone.BagContents = (byte[])BagContents.Clone();
        return clone;
    }
}
