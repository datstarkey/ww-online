namespace WWOnline.Patcher.Icons;

/// <summary>
/// Which of the game's item icons (BTI textures in <see cref="ArchiveRelativePath"/>) belongs to which
/// item number (dItemNo). Mirrors tww-decomp <c>dItem_data::item_resource[].mTexture</c>
/// (src/d/d_item_data.cpp, USA version) — the icons the pause menu and item-get use — minus the
/// entries whose texture is only the table's <c>bottle_00</c> filler (heart drops, magic jars...).
///
/// This file holds texture NAMES only. The images themselves are decoded from the player's own game
/// files on their machine (<see cref="ItemIconExtractor"/>); none is ever committed or shipped.
/// </summary>
public static class ItemIconCatalog
{
    /// <summary>The item icon archive, relative to an extracted game folder (dComIfGp_getItemIconArchive,
    /// mounted from /res/Msg/itemicon.arc in d_s_logo.cpp).</summary>
    public static readonly string ArchiveRelativePath = Path.Combine("files", "res", "Msg", "itemicon.arc");

    /// <summary>Bump when the extracted output changes (new tint, new files): older caches re-extract.</summary>
    public const int Version = 1;

    /// <summary>
    /// Item numbers (tww-decomp include/d/d_item_data.h, dItemNo_*_e) that the client shows as icons
    /// outside the 21 item-menu slots: quest status, capacities, dungeon items, wallet.
    /// </summary>
    public static class ItemNo
    {
        public const byte GreenRupee = 0x01;
        public const byte HeartPiece = 0x07;
        public const byte HeartContainer = 0x08;
        public const byte SmallKey = 0x15;
        public const byte PowerBracelets = 0x28;
        public const byte PiratesCharm = 0x42;
        public const byte HerosCharm = 0x43;
        public const byte DungeonMap = 0x4C;
        public const byte Compass = 0x4D;
        public const byte BossKey = 0x4E;
        public const byte EmptyBottle = 0x50;
        /// <summary>TRIFORCE1..8 are 0x61..0x68: shard N (0-based) is <c>Triforce1 + N</c>.</summary>
        public const byte Triforce1 = 0x61;
        public const byte PearlNayru = 0x69;
        public const byte PearlDin = 0x6A;
        public const byte PearlFarore = 0x6B;
        /// <summary>Songs are 0x6D..0x72 (Wind's Requiem .. Song of Passing), all the baton icon.</summary>
        public const byte WindsRequiem = 0x6D;
        public const byte BigWallet = 0xAB;      // MAX_RUPEE_UP1 (1000)
        public const byte GiantWallet = 0xAC;    // MAX_RUPEE_UP2 (5000)
        public const byte BombBag60 = 0xAD;      // MAX_BOMB_UP1
        public const byte BombBag99 = 0xAE;      // MAX_BOMB_UP2
        public const byte Quiver60 = 0xAF;       // MAX_ARROW_UP1
        public const byte Quiver99 = 0xB0;       // MAX_ARROW_UP2
    }

    /// <summary>
    /// Textures stored as IA4 (grey + alpha) that the game colours through the 2D pane that draws them.
    /// The extractor multiplies them by these colours (0xRRGGBB) so they read right on their own.
    /// Every other texture is extracted as-is.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, uint> Tints = new Dictionary<string, uint>
    {
        ["baton"] = 0xF4E3A6,     // the Wind Waker / songs: warm ivory
        ["get_rupy"] = 0x3FCB5E,  // rupee: green
    };

    /// <summary>The texture (file name without .bti) for an item number, or null if it has no icon.</summary>
    public static string? TextureForItem(byte itemNo) => TextureByItem.GetValueOrDefault(itemNo);

    /// <summary>Every texture the table refers to (each once).</summary>
    public static IEnumerable<string> AllTextures => TextureByItem.Values.Distinct();

    /// <summary>dItemNo → texture (tww-decomp dItem_data::item_resource[n].mTexture, USA).</summary>
    public static readonly IReadOnlyDictionary<byte, string> TextureByItem = new Dictionary<byte, string>
    {
        [0x01] = "get_rupy", // GREEN_RUPEE
        [0x02] = "get_rupy", // BLUE_RUPEE
        [0x03] = "get_rupy", // YELLOW_RUPEE
        [0x04] = "get_rupy", // RED_RUPEE
        [0x05] = "get_rupy", // PURPLE_RUPEE
        [0x06] = "get_rupy", // ORANGE_RUPEE
        [0x07] = "heart_up_02", // HEART_PIECE
        [0x08] = "heart_up_01", // HEART_CONTAINER
        [0x0B] = "bomb_00", // BOMB_5
        [0x0C] = "bomb_00", // BOMB_10
        [0x0D] = "bomb_00", // BOMB_20
        [0x0E] = "bomb_00", // BOMB_30
        [0x0F] = "get_rupy", // SILVER_RUPEE
        [0x10] = "bow_01", // ARROW_10
        [0x11] = "bow_01", // ARROW_20
        [0x12] = "bow_01", // ARROW_30
        [0x15] = "get_key", // SMALL_KEY
        [0x17] = "heart_up_02", // NOENTRY_23
        [0x18] = "heart_up_02", // NOENTRY_24
        [0x19] = "heart_up_02", // NOENTRY_25
        [0x1A] = "get_rupy", // SUB_DUN_RUPEE
        [0x1F] = "beast_08", // JOY_PENDANT
        [0x20] = "telescope", // TELESCOPE
        [0x21] = "whistle", // TINGLE_TUNER
        [0x22] = "baton", // WIND_WAKER
        [0x23] = "camera", // PICTO_BOX
        [0x24] = "coverofbeast", // SPOILS_BAG
        [0x25] = "rope", // GRAPPLING_HOOK
        [0x26] = "camera_2", // DELUXE_PICTO_BOX
        [0x27] = "bow_01", // BOW
        [0x28] = "gloves_00", // POWER_BRACELETS
        [0x29] = "boots_00", // IRON_BOOTS
        [0x2A] = "shield_02", // MAGIC_ARMOR
        [0x2B] = "boots_01", // WATER_BOOTS
        [0x2C] = "coverofbait", // BAIT_BAG
        [0x2D] = "boomerang", // BOOMERANG
        [0x2E] = "gloves_00", // BARE_HAND
        [0x2F] = "hookshot", // HOOKSHOT
        [0x30] = "delivery", // DELIVERY_BAG
        [0x31] = "bomb_00", // BOMB_BAG
        [0x32] = "clothes", // FUKU
        [0x33] = "hammer_01", // SKULL_HAMMER
        [0x34] = "fan", // DEKU_LEAF
        [0x35] = "arrow_power_01", // MAGIC_ARROW
        [0x36] = "arrow_power_02", // LIGHT_ARROW
        [0x37] = "clothes", // NEW_FUKU
        [0x38] = "sword_00", // SWORD
        [0x39] = "sword_01", // MASTER_SWORD_1
        [0x3A] = "sword_02", // MASTER_SWORD_2
        [0x3B] = "shield_00", // SHIELD
        [0x3C] = "shield_01", // MIRROR_SHIELD
        [0x3D] = "sword_00", // DROPPED_SWORD
        [0x3E] = "sword_03", // MASTER_SWORD_3
        [0x3F] = "heart_up_02", // HEART_PIECE_ALT
        [0x42] = "amulet_00", // PIRATES_CHARM
        [0x43] = "amulet_01", // HEROS_CHARM
        [0x45] = "beast_01", // SKULL_NECKLACE
        [0x46] = "beast_02", // BOKOBABA_SEED
        [0x47] = "beast_03", // GOLDEN_FEATHER
        [0x48] = "beast_04", // KNIGHTS_CREST
        [0x49] = "beast_05", // RED_JELLY
        [0x4A] = "beast_06", // GREEN_JELLY
        [0x4B] = "beast_07", // BLUE_JELLY
        [0x4C] = "dungeon_map", // MAP
        [0x4D] = "compass", // COMPASS
        [0x4E] = "boss_key", // BOSS_KEY
        [0x50] = "bottle_00", // EMPTY_BOTTLE
        [0x51] = "bottle_01", // RED_POTION
        [0x52] = "bottle_02", // GREEN_POTION
        [0x53] = "bottle_03", // BLUE_POTION
        [0x54] = "bottle_09", // HALF_SOUP_BOTTLE
        [0x55] = "bottle_04", // SOUP_BOTTLE
        [0x56] = "bottle_05", // WATER_BOTTLE
        [0x57] = "bottle_06", // FAIRY_BOTTLE
        [0x58] = "bottle_07", // FIREFLY_BOTTLE
        [0x59] = "bottle_08", // FOREST_WATER
        [0x61] = "triforce_00", // TRIFORCE1
        [0x62] = "triforce_01", // TRIFORCE2
        [0x63] = "triforce_02", // TRIFORCE3
        [0x64] = "triforce_03", // TRIFORCE4
        [0x65] = "triforce_04", // TRIFORCE5
        [0x66] = "triforce_05", // TRIFORCE6
        [0x67] = "triforce_06", // TRIFORCE7
        [0x68] = "triforce_07", // TRIFORCE8
        [0x69] = "god_symbol_02", // PEARL_NAYRU
        [0x6A] = "god_symbol_00", // PEARL_DIN
        [0x6B] = "god_symbol_01", // PEARL_FARORE
        [0x6C] = "amulet_00", // KNOWLEDGE_TF
        [0x6D] = "baton", // WINDS_REQUIEM
        [0x6E] = "baton", // BALLAD_OF_GALES
        [0x6F] = "baton", // COMMAND_MELODY
        [0x70] = "baton", // EARTH_GODS_LYRIC
        [0x71] = "baton", // WIND_GODS_ARIA
        [0x72] = "baton", // SONG_OF_PASSING
        [0x78] = "sail_00", // SAIL
        [0x79] = "cmap_tri2", // TRIFORCE_MAP_1
        [0x7A] = "cmap_tri2", // TRIFORCE_MAP_2
        [0x7B] = "cmap_tri2", // TRIFORCE_MAP_3
        [0x7C] = "cmap_tri2", // TRIFORCE_MAP_4
        [0x7D] = "cmap_tri2", // TRIFORCE_MAP_5
        [0x7E] = "cmap_tri2", // TRIFORCE_MAP_6
        [0x7F] = "cmap_tri2", // TRIFORCE_MAP_7
        [0x80] = "cmap_tri2", // TRIFORCE_MAP_8
        [0x82] = "bait_01", // BIRD_BAIT_5
        [0x83] = "bait_02", // HYOI_PEAR
        [0x8C] = "delivery_01", // TOWN_FLOWER
        [0x8D] = "delivery_02", // SEA_FLOWER
        [0x8E] = "delivery_03", // EXOTIC_FLOWER
        [0x8F] = "delivery_04", // HEROS_FLAG
        [0x90] = "delivery_05", // BIG_CATCH_FLAG
        [0x91] = "delivery_06", // BIG_SALE_FLAG
        [0x92] = "delivery_07", // PINWHEEL
        [0x93] = "delivery_08", // SICKLE_MOON_FLAG
        [0x94] = "delivery_09", // SKULL_TOWER_IDOL
        [0x95] = "delivery_10", // FOUNTAIN_IDOL
        [0x96] = "delivery_11", // POSTMAN_STATUE
        [0x97] = "delivery_12", // SHOP_GURU_STATUE
        [0x98] = "delivery_13", // FATHER_LETTER
        [0x99] = "delivery_14", // NOTE_TO_MOM
        [0x9A] = "delivery_15", // MAGGIES_LETTER
        [0x9B] = "delivery_16", // MOBLINS_LETTER
        [0x9C] = "delivery_17", // CABANA_DEED
        [0x9D] = "delivery_18", // COMPLIMENTARY_ID
        [0x9E] = "delivery_19", // FILL_UP_COUPON
        [0x9F] = "camera", // LEGENDARY_PICTOGRAPH
        [0xA0] = "amulet_00", // SALVAGE_ITEM_2
        [0xA1] = "amulet_00", // SALVAGE_ITEM_3
        [0xA2] = "amulet_00", // XXX_039
        [0xA3] = "tingle_figure", // TINGLE_STATUE_1
        [0xA4] = "tingle_figure", // TINGLE_STATUE_2
        [0xA5] = "tingle_figure", // TINGLE_STATUE_3
        [0xA6] = "tingle_figure", // TINGLE_STATUE_4
        [0xA7] = "tingle_figure", // TINGLE_STATUE_5
        [0xAB] = "big_purse", // MAX_RUPEE_UP1
        [0xAC] = "max_purse", // MAX_RUPEE_UP2
        [0xAD] = "bombpouch_1", // MAX_BOMB_UP1
        [0xAE] = "bombpouch_2", // MAX_BOMB_UP2
        [0xAF] = "arrowcase_1", // MAX_ARROW_UP1
        [0xB0] = "arrowcase_2", // MAX_ARROW_UP2
        [0xB3] = "get_rupy", // TINGLE_RUPEE_1
        [0xB4] = "get_rupy", // TINGLE_RUPEE_2
        [0xB5] = "get_rupy", // TINGLE_RUPEE_3
        [0xB6] = "get_rupy", // TINGLE_RUPEE_4
        [0xB7] = "get_rupy", // TINGLE_RUPEE_5
        [0xB8] = "get_rupy", // TINGLE_RUPEE_6
        [0xB9] = "get_rupy", // LITHOGRAPH_1
        [0xBA] = "get_rupy", // LITHOGRAPH_2
        [0xBB] = "get_rupy", // LITHOGRAPH_3
        [0xBC] = "get_rupy", // LITHOGRAPH_4
        [0xBD] = "get_rupy", // LITHOGRAPH_5
        [0xBE] = "get_rupy", // LITHOGRAPH_6
        [0xBF] = "get_rupy", // COLLECT_MAP_64
        [0xC0] = "get_rupy", // COLLECT_MAP_63
        [0xC1] = "get_rupy", // COLLECT_MAP_62
        [0xC2] = "cmap_hint2", // COLLECT_MAP_61
        [0xC3] = "cmap_hint2", // COLLECT_MAP_60
        [0xC4] = "cmap_hint2", // COLLECT_MAP_59
        [0xC5] = "cmap_hint2", // COLLECT_MAP_58
        [0xC6] = "cmap_hint2", // COLLECT_MAP_57
        [0xC7] = "cmap_hint2", // COLLECT_MAP_56
        [0xC8] = "cmap_hint2", // COLLECT_MAP_55
        [0xC9] = "cmap_hint2", // COLLECT_MAP_54
        [0xCA] = "cmap_hint2", // COLLECT_MAP_53
        [0xCB] = "cmap_tingle2", // COLLECT_MAP_52
        [0xCC] = "cmap_treasure2", // COLLECT_MAP_51
        [0xCD] = "cmap_treasure2", // COLLECT_MAP_50
        [0xCE] = "cmap_treasure2", // COLLECT_MAP_49
        [0xCF] = "cmap_treasure2", // COLLECT_MAP_48
        [0xD0] = "cmap_treasure2", // COLLECT_MAP_47
        [0xD1] = "cmap_treasure2", // COLLECT_MAP_46
        [0xD2] = "cmap_treasure2", // COLLECT_MAP_45
        [0xD3] = "cmap_treasure2", // COLLECT_MAP_44
        [0xD4] = "cmap_treasure2", // COLLECT_MAP_43
        [0xD5] = "cmap_treasure2", // COLLECT_MAP_42
        [0xD6] = "cmap_treasure2", // COLLECT_MAP_41
        [0xD7] = "cmap_treasure2", // COLLECT_MAP_40
        [0xD8] = "cmap_treasure2", // COLLECT_MAP_39
        [0xD9] = "cmap_treasure2", // COLLECT_MAP_38
        [0xDA] = "cmap_treasure2", // COLLECT_MAP_37
        [0xDB] = "cmap_phantomship2", // COLLECT_MAP_36
        [0xDC] = "cmap_tingle2", // COLLECT_MAP_35
        [0xDD] = "cmap_treasure2", // COLLECT_MAP_34
        [0xDE] = "cmap_treasure2", // COLLECT_MAP_33
        [0xDF] = "cmap_treasure2", // COLLECT_MAP_32
        [0xE0] = "cmap_treasure2", // COLLECT_MAP_31
        [0xE1] = "cmap_treasure2", // COLLECT_MAP_30
        [0xE2] = "cmap_treasure2", // COLLECT_MAP_29
        [0xE3] = "cmap_treasure2", // COLLECT_MAP_28
        [0xE4] = "cmap_treasure2", // COLLECT_MAP_27
        [0xE5] = "cmap_treasure2", // COLLECT_MAP_26
        [0xE6] = "cmap_treasure2", // COLLECT_MAP_25
        [0xE7] = "cmap_treasure2", // COLLECT_MAP_24
        [0xE8] = "cmap_treasure2", // COLLECT_MAP_23
        [0xE9] = "cmap_treasure2", // COLLECT_MAP_22
        [0xEA] = "cmap_treasure2", // COLLECT_MAP_21
        [0xEB] = "cmap_treasure2", // COLLECT_MAP_20
        [0xEC] = "cmap_treasure2", // COLLECT_MAP_19
        [0xED] = "cmap_treasure2", // COLLECT_MAP_18
        [0xEE] = "cmap_treasure2", // COLLECT_MAP_17
        [0xEF] = "cmap_treasure2", // COLLECT_MAP_16
        [0xF0] = "cmap_treasure2", // COLLECT_MAP_15
        [0xF1] = "cmap_treasure2", // COLLECT_MAP_14
        [0xF2] = "cmap_treasure2", // COLLECT_MAP_13
        [0xF3] = "cmap_treasure2", // COLLECT_MAP_12
        [0xF4] = "cmap_treasure2", // COLLECT_MAP_11
        [0xF5] = "cmap_treasure2", // COLLECT_MAP_10
        [0xF6] = "cmap_treasure2", // COLLECT_MAP_09
        [0xF7] = "cmap_tri2", // COLLECT_MAP_08
        [0xF8] = "cmap_tri2", // COLLECT_MAP_07
        [0xF9] = "cmap_tri2", // COLLECT_MAP_06
        [0xFA] = "cmap_tri2", // COLLECT_MAP_05
        [0xFB] = "cmap_tri2", // COLLECT_MAP_04
        [0xFC] = "cmap_tri2", // COLLECT_MAP_03
        [0xFD] = "cmap_tri2", // COLLECT_MAP_02
        [0xFE] = "cmap_tri2", // COLLECT_MAP_01
    };
}
