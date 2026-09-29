namespace WWOnline.Data;

/// <summary>
/// Wind Waker Item IDs for inventory items
/// These are the IDs used in inventory slots (803C4C44-803C4C58)
/// </summary>
public static class ItemIDs
{
    /// <summary>
    /// Main items
    /// </summary>
    public static class MainItems
    {
        public const byte Empty = 0xFF;              // Empty slot
        public const byte Telescope = 0x20;          // Telescope
        public const byte Sail = 0x78;               // Sail
        public const byte WindWaker = 0x22;          // Wind Waker baton
        public const byte GrapplingHook = 0x25;      // Grappling Hook
        public const byte SpoilsBag = 0x24;          // Spoils Bag
        public const byte Boomerang = 0x2D;          // Boomerang
        public const byte DekuLeaf = 0x34;           // Deku Leaf
        public const byte TingleTuner = 0x21;        // Tingle Tuner/GBA
        public const byte IronBoots = 0x29;          // Iron Boots
        public const byte MagicArmor = 0x2A;         // Magic Armor
        public const byte BaitBag = 0x2C;            // Bait Bag
        public const byte Bow = 0x27;                // Hero's Bow
        public const byte Bombs = 0x31;              // Bombs
        public const byte DeliveryBag = 0x30;        // Delivery Bag
        public const byte Hookshot = 0x2F;           // Hookshot
        public const byte SkullHammer = 0x33;        // Skull Hammer
    }
    
    /// <summary>
    /// Bottle contents
    /// </summary>
    public static class BottleContents
    {
        public const byte EmptyBottle = 0x50;        // Empty bottle
        public const byte RedPotion = 0x51;          // Red potion (health)
        public const byte GreenPotion = 0x52;        // Green potion (magic)
        public const byte BluePotion = 0x53;         // Blue potion (health + magic)
        public const byte HalfElixirSoup = 0x54;     // Elixir soup (half)
        public const byte FullElixirSoup = 0x55;     // Elixir soup (full)
        public const byte Water = 0x56;              // Water
        public const byte FairyInBottle = 0x57;      // Fairy
        public const byte ForestWater = 0x58;        // Forest water
        public const byte ForestFirefly = 0x59;      // Forest firefly
    }
    
    /// <summary>
    /// Picto Box variants
    /// </summary>
    public static class PictoBox
    {
        public const byte StandardPictoBox = 0x23;   // Standard Picto Box
        public const byte DeluxePictoBox = 0x26;     // Deluxe Picto Box (color)
    }
    
    /// <summary>
    /// Sword IDs
    /// </summary>
    public static class Swords
    {
        public const byte NoSword = 0xFF;            // No sword equipped
        public const byte HerosSword = 0x38;         // Hero's Sword (starting sword)
        public const byte MasterSword = 0x39;        // Master Sword
        public const byte MasterSwordHalf = 0x3A;    // Master Sword (half power)
        public const byte MasterSwordFull = 0x3E;    // Master Sword (full power)
    }
    
    /// <summary>
    /// Shield IDs
    /// </summary>
    public static class Shields
    {
        public const byte NoShield = 0xFF;           // No shield equipped
        public const byte HerosShield = 0x3B;        // Hero's Shield
        public const byte MirrorShield = 0x3C;       // Mirror Shield
    }
    
    /// <summary>
    /// Power Bracelets
    /// </summary>
    public static class Bracelets
    {
        public const byte NoBracelet = 0xFF;         // No bracelet
        public const byte PowerBracelets = 0x28;     // Power Bracelets
    }
    
    /// <summary>
    /// Spoils Bag items
    /// </summary>
    public static class Spoils
    {
        public const byte JoyPendant = 0x1F;         // Joy Pendant
        public const byte SkullNecklace = 0x20;      // Skull Necklace
        public const byte BokoBabaSeed = 0x21;       // Boko Baba Seed
        public const byte GoldenFeather = 0x22;      // Golden Feather
        public const byte KnightsCrest = 0x23;       // Knight's Crest
        public const byte RedChuJelly = 0x24;        // Red Chu Jelly
        public const byte GreenChuJelly = 0x25;      // Green Chu Jelly
        public const byte BlueChuJelly = 0x26;       // Blue Chu Jelly
    }
    
    /// <summary>
    /// Bait Bag items
    /// </summary>
    public static class Bait
    {
        public const byte AllPurposeBait = 0x82;     // All-Purpose Bait (d_item_data.h dItemNo_BIRD_BAIT_5_e)
        public const byte HyoiPear = 0x83;           // Hyoi Pear, control seagulls (dItemNo_HYOI_PEAR_e)
    }
    
    /// <summary>
    /// Delivery Bag items (d_item_data.h 0x8C-0x9E; the room's bag is DeliveryCounts)
    /// </summary>
    public static class Delivery
    {
        public const byte TownFlower = 0x8C;         // Town Flower
        public const byte SeaFlower = 0x8D;          // Sea Flower
        public const byte ExoticFlower = 0x8E;       // Exotic Flower
        public const byte HeroFlag = 0x8F;           // Hero's Flag
        public const byte BigCatchFlag = 0x90;       // Big Catch Flag
        public const byte BigSaleFlag = 0x91;        // Big Sale Flag
        public const byte Pinwheel = 0x92;           // Pinwheel
        public const byte SickleMoonFlag = 0x93;     // Sickle Moon Flag
        public const byte SkullTowerIdol = 0x94;     // Skull Tower Idol
        public const byte FountainIdol = 0x95;       // Fountain Idol
        public const byte PostmanStatue = 0x96;      // Postman Statue
        public const byte ShopGuruStatue = 0x97;     // Shop Guru Statue
        public const byte FathersLetter = 0x98;      // Father's Letter
        public const byte NoteToMom = 0x99;          // Note to Mom
        public const byte MaggiesLetter = 0x9A;      // Maggie's Letter
        public const byte MoblinsLetter = 0x9B;      // Moblin's Letter
        public const byte CabanaDeed = 0x9C;         // Cabana Deed
        public const byte ComplimentaryId = 0x9D;    // Complimentary ID
        public const byte FillUpCoupon = 0x9E;       // Fill-Up Coupon
    }
    
    /// <summary>
    /// Charts
    /// </summary>
    public static class Charts
    {
        // Treasure Charts (IDs 0x61-0x89)
        public const byte TreasureChart1 = 0x61;
        public const byte TreasureChart2 = 0x62;
        public const byte TreasureChart3 = 0x63;
        public const byte TreasureChart4 = 0x64;
        public const byte TreasureChart5 = 0x65;
        public const byte TreasureChart6 = 0x66;
        public const byte TreasureChart7 = 0x67;
        public const byte TreasureChart8 = 0x68;
        public const byte TreasureChart9 = 0x69;
        public const byte TreasureChart10 = 0x6A;
        public const byte TreasureChart11 = 0x6B;
        public const byte TreasureChart12 = 0x6C;
        public const byte TreasureChart13 = 0x6D;
        public const byte TreasureChart14 = 0x6E;
        public const byte TreasureChart15 = 0x6F;
        public const byte TreasureChart16 = 0x70;
        public const byte TreasureChart17 = 0x71;
        public const byte TreasureChart18 = 0x72;
        public const byte TreasureChart19 = 0x73;
        public const byte TreasureChart20 = 0x74;
        public const byte TreasureChart21 = 0x75;
        public const byte TreasureChart22 = 0x76;
        public const byte TreasureChart23 = 0x77;
        public const byte TreasureChart24 = 0x78;
        public const byte TreasureChart25 = 0x79;
        public const byte TreasureChart26 = 0x7A;
        public const byte TreasureChart27 = 0x7B;
        public const byte TreasureChart28 = 0x7C;
        public const byte TreasureChart29 = 0x7D;
        public const byte TreasureChart30 = 0x7E;
        public const byte TreasureChart31 = 0x7F;
        public const byte TreasureChart32 = 0x80;
        public const byte TreasureChart33 = 0x81;
        public const byte TreasureChart34 = 0x82;
        public const byte TreasureChart35 = 0x83;
        public const byte TreasureChart36 = 0x84;
        public const byte TreasureChart37 = 0x85;
        public const byte TreasureChart38 = 0x86;
        public const byte TreasureChart39 = 0x87;
        public const byte TreasureChart40 = 0x88;
        public const byte TreasureChart41 = 0x89;
        
        // Triforce Charts (IDs 0xA3-0xAA)
        public const byte TriforceChart1 = 0xA3;
        public const byte TriforceChart2 = 0xA4;
        public const byte TriforceChart3 = 0xA5;
        public const byte TriforceChart4 = 0xA6;
        public const byte TriforceChart5 = 0xA7;
        public const byte TriforceChart6 = 0xA8;
        public const byte TriforceChart7 = 0xA9;
        public const byte TriforceChart8 = 0xAA;
        
        // Special Charts
        public const byte TingleChart = 0x8A;        // Tingle's Chart
        public const byte GhostShipChart = 0x8B;     // Ghost Ship Chart
        public const byte IncredibleChart = 0x8C;    // Incredible Chart
        public const byte BeedlesChart = 0x8D;       // Beedle's Chart
        public const byte PlatformChart = 0x8E;      // Platform Chart
        public const byte LightRingChart = 0x8F;     // Light Ring Chart
        public const byte SecretCaveChart = 0x90;    // Secret Cave Chart
        public const byte SeaHeartsChart = 0x91;     // Sea Hearts Chart
        public const byte IslandHeartsChart = 0x92;  // Island Hearts Chart
        public const byte GreatFairyChart = 0x93;    // Great Fairy Chart
        public const byte OctoChart = 0x94;          // Octo Chart
        public const byte INcredibleChart = 0x95;    // IN-credible Chart
        public const byte SubmarineChart = 0x96;     // Submarine Chart
    }
    
    /// <summary>
    /// Get the name of an item by its ID
    /// </summary>
    public static string GetItemName(byte itemId)
    {
        return itemId switch
        {
            0xFF => "Empty",
            0x20 => "Telescope",
            0x78 => "Sail",
            0x22 => "Wind Waker",
            0x25 => "Grappling Hook",
            0x24 => "Spoils Bag",
            0x2D => "Boomerang",
            0x34 => "Deku Leaf",
            0x21 => "Tingle Tuner",
            0x29 => "Iron Boots",
            0x2A => "Magic Armor",
            0x2C => "Bait Bag",
            0x27 => "Hero's Bow",
            0x31 => "Bombs",
            0x30 => "Delivery Bag",
            0x2F => "Hookshot",
            0x33 => "Skull Hammer",
            0x23 => "Picto Box",
            0x26 => "Deluxe Picto Box",
            
            // Bottles
            0x50 => "Empty Bottle",
            0x51 => "Red Potion",
            0x52 => "Green Potion",
            0x53 => "Blue Potion",
            0x54 => "Elixir Soup (Half)",
            0x55 => "Elixir Soup",
            0x56 => "Water",
            0x57 => "Fairy",
            0x58 => "Forest Water",
            0x59 => "Forest Firefly",
            
            // Swords
            0x38 => "Hero's Sword",
            0x39 => "Master Sword",
            0x3A => "Master Sword (Half)",
            0x3E => "Master Sword (Full)",
            
            // Shields
            0x3B => "Hero's Shield",
            0x3C => "Mirror Shield",
            
            // Power
            0x28 => "Power Bracelets",
            
            // Charts
            >= 0x61 and <= 0x89 => $"Treasure Chart {itemId - 0x60}",
            >= 0xA3 and <= 0xAA => $"Triforce Chart {itemId - 0xA2}",
            0x8A => "Tingle's Chart",
            0x8B => "Ghost Ship Chart",
            0x8C => "Incredible Chart",
            0x8D => "Beedle's Chart",
            0x8E => "Platform Chart",
            0x8F => "Light Ring Chart",
            0x90 => "Secret Cave Chart",
            0x91 => "Sea Hearts Chart",
            0x92 => "Island Hearts Chart",
            0x93 => "Great Fairy Chart",
            0x94 => "Octo Chart",
            0x95 => "IN-credible Chart",
            0x96 => "Submarine Chart",
            
            _ => $"Item_{itemId:X2}"
        };
    }
    
    /// <summary>
    /// Check if an item is a bottle
    /// </summary>
    public static bool IsBottle(byte itemId)
    {
        return itemId >= 0x50 && itemId <= 0x59;
    }
    
    /// <summary>
    /// Check if an item is a chart
    /// </summary>
    public static bool IsChart(byte itemId)
    {
        return (itemId >= 0x61 && itemId <= 0x96) || (itemId >= 0xA3 && itemId <= 0xAA);
    }
    
    /// <summary>
    /// Check if an item is a sword
    /// </summary>
    public static bool IsSword(byte itemId)
    {
        return itemId == 0x38 || itemId == 0x39 || itemId == 0x3A || itemId == 0x3E;
    }
    
    /// <summary>
    /// Check if an item is a shield
    /// </summary>
    public static bool IsShield(byte itemId)
    {
        return itemId == 0x3B || itemId == 0x3C;
    }
    
    /// <summary>
    /// Check if an item is equippable to X/Y/Z buttons
    /// </summary>
    public static bool IsEquippable(byte itemId)
    {
        return itemId switch
        {
            0x20 or 0x22 or 0x25 or 0x2D or 0x34 or 0x21 or 0x29 or 0x2A or 
            0x27 or 0x31 or 0x2F or 0x33 or 0x23 or 0x26 => true,
            _ when IsBottle(itemId) => true,
            _ => false
        };
    }
}