namespace WWOnline.Data;

/// <summary>
/// Comprehensive memory address mappings for Wind Waker (North American GameCube version)
/// Based on WW-Hacking-Docs by LagoLunatic
/// Using typed memory addresses for type-safe reading/writing
/// </summary>
public static class GameMemoryAddresses
{
    /// <summary>g_dComIfG_gameInfo (tww-decomp config/GZLE01/symbols.txt). dSv_info_c is at +0.</summary>
    public const uint GameInfo = 0x803C4C08;
    /// <summary>g_dComIfG_gameInfo.play (dComIfG_play_c, d_com_inf_game.h "/* 0x012A0 */").
    /// Write game-info addresses as GameInfo/Play + the decomp's offset comment — never hand-add
    /// hex (a hand-computed 0x803CA368 instead of Play+0x48C0 = 0x803CA768 broke the wallet).</summary>
    public const uint Play = GameInfo + 0x12A0;

    /// <summary>
    /// Player status and health related addresses
    /// </summary>
    public static class Player
    {
        public static readonly MemoryAddress<ushort> MaxHealth = new(0x803C4C08, "MaxHealth", "Max HP (in quarters of hearts)");
        public static readonly MemoryAddress<ushort> CurrentHealth = new(0x803C4C0A, "CurrentHealth", "Current HP (in quarters of hearts)");
        public static readonly MemoryAddress<ushort> RupeeCount = new(0x803C4C0C, "RupeeCount", "Current rupee count (save value — read-only for us; see PendingRupeeDelta)");
        // play.mItemRupeeCount (d_com_inf_game.h "/* 0x48C0 */ s32 mItemRupeeCount"). What
        // dComIfGp_setItemRupeeCount(n) adds to; d_meter applies it next frame (clamped to the
        // wallet), updates the save value AND animates the HUD counter. Writing RupeeCount
        // directly skips the HUD, which keeps showing the old number.
        public static readonly MemoryAddress<int> PendingRupeeDelta = new(Play + 0x48C0, "PendingRupeeDelta", "Rupee change queued for the HUD (dComIfGp_setItemRupeeCount)");
        public static readonly MemoryAddress<byte> MaxMagicMeter = new(0x803C4C1B, "MaxMagicMeter", "Max magic meter");
        public static readonly MemoryAddress<byte> CurrentMagicMeter = new(0x803C4C1C, "CurrentMagicMeter", "Current magic meter");
        public static readonly MemoryAddress<ushort> CurrentOxygen = new(0x803C4C1E, "CurrentOxygen", "Current oxygen meter");
        public static readonly MemoryAddress<byte> CurrentArrowCount = new(0x803C4C71, "CurrentArrowCount", "Current arrow count");
        public static readonly MemoryAddress<byte> CurrentBombCount = new(0x803C4C72, "CurrentBombCount", "Current bomb count");
        // play.mItemArrowNumCount / mItemBombNumCount (d_com_inf_game.h "/* 0x48E0 */ s16",
        // "/* 0x48E4 */ s16"; gameInfo+0x5B80 / +0x5B84, the pending counts the puppet guard also
        // saves). What dComIfGp_setItemArrowNumCount / setItemBombNumCount add to; d_meter applies
        // them (clamped to the max) and updates the save value AND the HUD counter. Writing the
        // save count directly leaves the HUD showing the old number until the next pickup.
        public static readonly MemoryAddress<short> PendingArrowDelta = new(Play + 0x48E0, "PendingArrowDelta", "Arrow change queued for the HUD (dComIfGp_setItemArrowNumCount)");
        public static readonly MemoryAddress<short> PendingBombDelta = new(Play + 0x48E4, "PendingBombDelta", "Bomb change queued for the HUD (dComIfGp_setItemBombNumCount)");
        
        // Equipment
        public static readonly MemoryAddress<byte> XButtonItem = new(0x803C4C11, "XButtonItem", "X button equipped item");
        public static readonly MemoryAddress<byte> YButtonItem = new(0x803C4C12, "YButtonItem", "Y button equipped item");
        public static readonly MemoryAddress<byte> ZButtonItem = new(0x803C4C13, "ZButtonItem", "Z button equipped item");
        /// <summary>dSv_player_status_a_c /* 0x09 */ mSelectItem[5] (dComIfGs_getSelectItem): the INVENTORY SLOT
        /// on X, Y, Z (bait bag slots are 36-43, dInvSlot_BaitFirst_e; 0xFF = none). Same bytes as X/Y/ZButtonItem.</summary>
        public const uint SelectItemSlots = GameInfo + 0x09;
        /// <summary>play.mSelectItem[4] (d_com_inf_game.h "/* 0x4933 */"; DOL setBaitItemChange 0x8005A0C0:
        /// stb 0x5BD3(gameInfo)): the ITEM NUMBER on X, Y, Z that the HUD and Link use, refreshed from the
        /// save's slot by dComIfGp_setSelectItem.</summary>
        public const uint PlaySelectItems = Play + 0x4933;
        public static readonly MemoryAddress<byte> CurrentSword = new(0x803C4C16, "CurrentSword", "Currently equipped sword ID");
        public static readonly MemoryAddress<byte> CurrentShield = new(0x803C4C17, "CurrentShield", "Currently equipped shield ID");
        public static readonly MemoryAddress<byte> PowerBracelets = new(0x803C4C18, "PowerBracelets", "Equipped bracelets item id (mSelectEquip[2]; 0x28 = Power Bracelets)");
        // dSv_player_status_a_c /* 0x12 */ mWalletSize: 0 = 200, 1 = 1000, 2+ = 5000 (getRupeeMax).
        public static readonly MemoryAddress<byte> CurrentWallet = new(0x803C4C1A, "CurrentWallet", "Wallet size (0 = 200, 1 = 1000, 2 = 5000 rupees)");
        // dSv_player_c /* 0x148 */ mInfo -> dSv_player_info_c /* 0x58 */ mClearCount (dComIfGs_getClearCount;
        // playerInit reads it at DOL 0x80125AEC: lbz r0,0x1A0(gameInfo)). Non-zero = second quest.
        public static readonly MemoryAddress<byte> ClearCount = new(GameInfo + 0x148 + 0x58, "ClearCount", "Times the game was cleared (second quest when > 0)");
        
        // Player entity and state
        public static readonly MemoryAddress<uint> LinkActorPointer = new(0x803CA410, "LinkActorPointer", "Pointer to Link's actor instance");
        public static readonly MemoryAddress<uint> SecondPlayerActorPointer = new(0x80FBECD4, "SecondPlayerActorPointer", "Pointer to second player's actor instance");
        public static readonly MemoryAddress<uint> PlayerStatusBitfield0 = new(0x803CA8D0, "PlayerStatusBitfield0", "Player status bitfield 0");
        public static readonly MemoryAddress<uint> PlayerStatusBitfield1 = new(0x803CA8D4, "PlayerStatusBitfield1", "Player status bitfield 1");
        
        // Position offsets (to be used with Link's actor pointer)
        public static readonly MemoryAddress<float> LinkPositionXOffset = new(0x1F8, "PositionX", "X position offset from actor base");
        public static readonly MemoryAddress<float> LinkPositionYOffset = new(0x1FC, "PositionY", "Y position offset from actor base");
        public static readonly MemoryAddress<float> LinkPositionZOffset = new(0x200, "PositionZ", "Z position offset from actor base");
        public static readonly MemoryAddress<ushort> LinkRotationYOffset = new(0x20E, "RotationY", "Facing (fopAc_ac_c::shape_angle.y) offset from actor base — 0x206 is current.angle.y, the movement direction");
        
        // Velocity offsets
        public static readonly MemoryAddress<float> LinkVelocityXOffset = new(0x220, "VelocityX", "X velocity offset from actor base");
        public static readonly MemoryAddress<float> LinkVelocityYOffset = new(0x224, "VelocityY", "Y velocity offset from actor base");
        public static readonly MemoryAddress<float> LinkVelocityZOffset = new(0x228, "VelocityZ", "Z velocity offset from actor base");
    }
    
    /// <summary>
    /// Inventory related addresses
    /// </summary>
    public static class Inventory
    {
        /// <summary>dSv_player_c /* 0x0C4 */ mMap: dSv_player_map_c (d_save.h:410-437, 0x84 bytes).</summary>
        public const uint SeaMapBase = GameInfo + 0xC4;
        /// <summary>field_0x0[1..3] (charts owned / opened / completed, u32[4] each) then mFmapBits[49]:
        /// +0x10..+0x70, the first 97 bytes of <see cref="RoomInventory.SeaMap"/>.</summary>
        public const uint SeaMapCharts = SeaMapBase + 0x10;
        public const int SeaMapChartsAndSquaresLength = 0x61;
        /// <summary>field_0x81: the Triforce charts deciphered (onTriforce), <see cref="RoomInventory.SeaMap"/>[97].</summary>
        public const uint SeaMapTriforce = SeaMapBase + 0x81;

        // Item slots as byte array
        public static readonly ByteArrayMemoryAddress ItemSlots = new(0x803C4C44, 21, "ItemSlots", "All 21 inventory item slots");
        
        // Individual item slots
        public static readonly MemoryAddress<byte> Telescope = new(0x803C4C44, "Telescope", "Telescope slot");
        public static readonly MemoryAddress<byte> Sail = new(0x803C4C45, "Sail", "Sail slot");
        public static readonly MemoryAddress<byte> WindWaker = new(0x803C4C46, "WindWaker", "Wind Waker slot");
        public static readonly MemoryAddress<byte> GrapplingHook = new(0x803C4C47, "GrapplingHook", "Grappling Hook slot");
        public static readonly MemoryAddress<byte> SpoilsBag = new(0x803C4C48, "SpoilsBag", "Spoils Bag slot");
        public static readonly MemoryAddress<byte> Boomerang = new(0x803C4C49, "Boomerang", "Boomerang slot");
        public static readonly MemoryAddress<byte> DekuLeaf = new(0x803C4C4A, "DekuLeaf", "Deku Leaf slot");
        public static readonly MemoryAddress<byte> TingleTuner = new(0x803C4C4B, "TingleTuner", "Tingle Tuner slot");
        public static readonly MemoryAddress<byte> PictoBox = new(0x803C4C4C, "PictoBox", "Picto Box slot");
        public static readonly MemoryAddress<byte> IronBoots = new(0x803C4C4D, "IronBoots", "Iron Boots slot");
        public static readonly MemoryAddress<byte> MagicArmor = new(0x803C4C4E, "MagicArmor", "Magic Armor slot");
        public static readonly MemoryAddress<byte> BaitBag = new(0x803C4C4F, "BaitBag", "Bait Bag slot");
        public static readonly MemoryAddress<byte> Bow = new(0x803C4C50, "Bow", "Bow slot");
        public static readonly MemoryAddress<byte> Bombs = new(0x803C4C51, "Bombs", "Bombs slot");
        public static readonly MemoryAddress<byte> Bottle1 = new(0x803C4C52, "Bottle1", "First bottle contents");
        public static readonly MemoryAddress<byte> Bottle2 = new(0x803C4C53, "Bottle2", "Second bottle contents");
        public static readonly MemoryAddress<byte> Bottle3 = new(0x803C4C54, "Bottle3", "Third bottle contents");
        public static readonly MemoryAddress<byte> Bottle4 = new(0x803C4C55, "Bottle4", "Fourth bottle contents");
        public static readonly MemoryAddress<byte> DeliveryBag = new(0x803C4C56, "DeliveryBag", "Delivery Bag slot");
        public static readonly MemoryAddress<byte> Hookshot = new(0x803C4C57, "Hookshot", "Hookshot slot");
        public static readonly MemoryAddress<byte> SkullHammer = new(0x803C4C58, "SkullHammer", "Skull Hammer slot");
        
        // dSv_player_get_item_c::mItemFlags (dSv_player_c /* 0x051 */ mGetItem): per-slot
        // "ever obtained" bits, e.g. Bow slot bit 0 = Bow, bit 1 = Fire & Ice Arrows, bit 2 = Light Arrows.
        public static readonly ByteArrayMemoryAddress ItemOwnership = new(GameInfo + 0x51, 21, "ItemOwnership", "Per-slot item get flags (dSv_player_get_item_c)");

        // dSv_player_collect_c (dSv_player_c /* 0x0B4 */ mCollect, d_save.h). mCollect[i] is what
        // dComIfGs_onCollect(i, bit) sets (d_item.cpp item_func_*). The old 0x803C4C5C/5D/6C-6F
        // values here pointed into mGetItem / mItemRecord's timer — triforce and pearl writes
        // were landing on the item-record timer.
        public const uint CollectBase = GameInfo + 0xB4;
        public static readonly MemoryAddress<byte> SwordsBitfield = new(CollectBase + 0x0, "SwordsBitfield", "Owned swords (mCollect[0]: Hero's, Master x3)");
        public static readonly MemoryAddress<byte> ShieldsBitfield = new(CollectBase + 0x1, "ShieldsBitfield", "Owned shields (mCollect[1]: Hero's, Mirror)");
        public static readonly MemoryAddress<byte> PowerBraceletsBitfield = new(CollectBase + 0x2, "PowerBraceletsBitfield", "Owned Power Bracelets (mCollect[2])");
        public static readonly MemoryAddress<byte> PiratesCharmBitfield = new(CollectBase + 0x3, "PiratesCharmBitfield", "Owned Pirate's Charm (mCollect[3])");
        public static readonly MemoryAddress<byte> HerosCharmBitfield = new(CollectBase + 0x4, "HerosCharmBitfield", "Hero's Charm (mCollect[4]: bit 0 owned, bit 1 worn)");
        public static readonly MemoryAddress<byte> SongsBitfield = new(CollectBase + 0x9, "SongsBitfield", "Owned songs (mTact)");
        public static readonly MemoryAddress<byte> TriforceShards = new(CollectBase + 0xA, "TriforceShards", "Triforce shards (mTriforce)");
        public static readonly MemoryAddress<byte> PearlsBitfield = new(CollectBase + 0xB, "PearlsBitfield", "Pearls (mSymbol)");
        /// <summary>sizeof(dSv_player_collect_c) = 0xD.</summary>
        public const uint CollectEnd = CollectBase + 0xD;

        // Bag contents
        public static readonly ByteArrayMemoryAddress BagContents = new(0x803C4C7E, 24, "BagContents", "Spoils, Bait, and Delivery bag contents");

        // The bait bag (d_save.h). dSv_player_c /* 0x076 */ mBagItem -> dSv_player_bag_item_c /* 0x08 */
        // mBait[8] (item numbers, 0xFF = empty); /* 0x090 */ mGetBagItem -> dSv_player_get_bag_item_c /* 0x5 */
        // mBaitFlags (bit 0 All-Purpose Bait, bit 1 Hyoi Pear ever obtained: item_func_bird_esa_5 /
        // item_func_animal_esa); /* 0x09C */ mBagItemRecord -> dSv_player_bag_item_record_c /* 0x08 */
        // mBaitNum[8] (DOL setBaitItem 0x8005A294: gameInfo + 0x9C, stb 3 at +8+i).
        public const uint BaitItems = GameInfo + 0x76 + 0x08;
        public const uint BaitGetFlags = GameInfo + 0x90 + 0x5;
        public const uint BaitNums = GameInfo + 0x9C + 0x08;

        // The spoils bag, in the same structs: mBagItem /* 0x00 */ mBeast[8] (item numbers in menu order, 0xFF =
        // empty; setBeastItem puts a new type in the first free slot, DOL item_func_skull_necklace 0x800C3D14:
        // gameInfo + 0x76), mGetBagItem /* 0x4 */ mBeastFlags (bit = dBeastIndex_e, onGetItemBeast) and
        // mBagItemRecord /* 0x00 */ mBeastNum[8] (the count per TYPE, indexed by dBeastIndex_e, 0-99).
        public const uint SpoilsItems = GameInfo + 0x76 + 0x00;
        public const uint SpoilsGetFlags = GameInfo + 0x90 + 0x4;
        public const uint SpoilsNums = GameInfo + 0x9C + 0x00;

        // The delivery bag, in the same structs: mBagItem /* 0x10 */ mReserve[8] (item numbers 0x8C-0x9E, 0xFF =
        // empty; no counts: an item that arrives takes the first empty slot, setReserveItem) and mGetBagItem /* 0x0 */
        // u32 mReserveFlags (bit item - 0x8C: ever obtained, onReserve). Verified in main.dol: item_func_flower_1
        // (0x800C468C) calls onReserve(gameInfo + 0x90, 0) then setReserveItem(gameInfo + 0x76, 0x8C), and
        // setReserveItem (0x8005A7E4) stores at +0x10 + i; onReserve (0x8005AB24) is lwz / or / stw at +0.
        public const uint DeliveryItems = GameInfo + 0x76 + 0x10;
        public const uint DeliveryGetFlags = GameInfo + 0x90 + 0x0;

        // play.mItemBeastNumCounts[8] (d_com_inf_game.h "/* 0x48E8 */ s16", indexed by dBeastIndex_e;
        // DOL item_func_skull_necklace 0x800C3D3C: lha/sth 0x5B88(gameInfo)). What dComIfGp_setItemBeastNumCount
        // adds to, for every pickup, sale and trade; d_meter applies it next frame: count clamped to 0-99, and a
        // type at 0 leaves its slot (setBeastItemEmpty, which also clears X/Y/Z).
        public const uint PendingSpoilsDeltas = Play + 0x48E8;

        // Capacities (dSv_player_c /* 0x06E */ mItemMax: +1 arrows, +2 bombs) — the COUNTS are
        // Player.CurrentArrowCount / CurrentBombCount (mItemRecord).
        public static readonly MemoryAddress<byte> MaxArrows = new(GameInfo + 0x6E + 0x1, "MaxArrows", "Quiver capacity");
        public static readonly MemoryAddress<byte> MaxBombs = new(GameInfo + 0x6E + 0x2, "MaxBombs", "Bomb bag capacity");
    }
    
    /// <summary>
    /// Stage and world state addresses
    /// </summary>
    public static class Stage
    {
        public static readonly StringMemoryAddress CurrentStageName = new(0x803C9D3C, 8, "CurrentStageName", "Current stage name");
        public static readonly MemoryAddress<short> CurrentSpawnId = new(0x803C9D44, "CurrentSpawnId", "Most recent spawn ID (dStage_startStage_c::mPoint, s16)");
        public static readonly MemoryAddress<byte> CurrentRoomNumber = new(0x803F6A78, "CurrentRoomNumber", "Current room number");
        public static readonly StringMemoryAddress NextStageName = new(0x803C9D48, 8, "NextStageName", "Next stage name to load");
        public static readonly MemoryAddress<byte> NextRoomNumber = new(0x803C9D52, "NextRoomNumber", "Next room number");
        public static readonly MemoryAddress<short> NextSpawnId = new(0x803C9D50, "NextSpawnId", "Next spawn ID (dStage_startStage_c::mPoint, s16)");
        public static readonly MemoryAddress<byte> NextLayer = new(0x803C9D53, "NextLayer", "Next layer (dStage_startStage_c::mLayer, s8; 0xFF = default)");
        
        // Stage info
        public static readonly ByteArrayMemoryAddress StageInfoList = new(0x803C4F88, 0x240, "StageInfoList", "Stage information list");
        public static readonly ByteArrayMemoryAddress CurrentStageInfo = new(0x803C5380, 0x24, "CurrentStageInfo", "Currently loaded stage info");
        
        // Stage control
        public static readonly MemoryAddress<uint> StageInfoPointer = new(0x803C52A8, "StageInfoPointer", "Pointer to stage info");
        public static readonly MemoryAddress<uint> RoomControlPointer = new(0x803F6A5C, "RoomControlPointer", "Pointer to room control");
    }
    
    /// <summary>
    /// Event and save data addresses
    /// </summary>
    public static class Events
    {
        /// <summary>play.mEvtCtrl.mMode (d_com_inf_game.h:718 mEvtCtrl at play + 0x3F38, d_event.h:154 mMode +0xC2):
        /// dComIfGp_event_runCheck() is mMode != 0. The REL's GAMEINFO_EVT_MODE.</summary>
        public const uint EventMode = Play + 0x3F38 + 0xC2;
        /// <summary>dMenu_pause (d_meter.cpp:52, ww_linker.ld d_meter__dMenu_pause; dMenu_flag() reads it): nonzero while
        /// the pause / item menu is open (fopAc_Draw then draws no actor).</summary>
        public const uint MenuPause = 0x803F7097;
        public static readonly ByteArrayMemoryAddress EventBitfield = new(0x803C522C, 256, "EventBitfield", "Event bit flags");
        // dSv_save_c /* 0x624 */ mEvent; isEventBit(no) = mFlags[no >> 8] & (no & 0xFF) (d_save.cpp:1197).
        // playerInit tests EVENT_BIT_HERO_CLOTHES there (DOL 0x80125AD0: addi r3,r3,0x624; li r4,0x2A80).
        public const uint EventBits = GameInfo + 0x624;
        public static readonly MemoryAddress<byte> DoubleMagicEventByte = new(EventBits + (WWOnline.Shared.Models.RoomInventory.DoubleMagicEventFlag >> 8), "DoubleMagicEventByte", "Event byte holding the double-magic Great Fairy's bit (0x3180)");
        public static readonly MemoryAddress<byte> HeroClothesEventByte = new(EventBits + (PuppetLayout.EVENT_BIT_HERO_CLOTHES >> 8), "HeroClothesEventByte", "Event byte holding the hero's-clothes bit (EVENT_BIT_HERO_CLOTHES)");
        public static readonly MemoryAddress<uint> EventControlStructure = new(0x803C9DE0, "EventControlStructure", "Event control structure");
        public static readonly ByteArrayMemoryAddress EventFlagBitfield = new(0x803C9F10, 1280, "EventFlagBitfield", "Extended event flag bitfield");
        
        // Save data
        public static readonly ByteArrayMemoryAddress SaveData = new(0x803C4C08, 0xD80, "SaveData", "Complete save data");
        public static readonly MemoryAddress<byte> CurrentSaveSlot = new(0x803C9D70, "CurrentSaveSlot", "Current save slot");
        public static readonly MemoryAddress<byte> NewGameFlag = new(0x803C53A4, "NewGameFlag", "New game flag");
    }
    
    /// <summary>
    /// Input and controller addresses
    /// </summary>
    public static class Input
    {
        public static readonly MemoryAddress<uint> ControllerState = new(0x803A4DF0, "ControllerState", "Controller input state");
        public static readonly MemoryAddress<uint> ButtonPressBitfield = new(0x803ED818, "ButtonPressBitfield", "Button press bitfield");
        public static readonly MemoryAddress<uint> ButtonPressBitfieldAlt = new(0x803ED848, "ButtonPressBitfieldAlt", "Alternate button press bitfield");
        public static readonly MemoryAddress<byte> AnalogStickX = new(0x803A4DF8, "AnalogStickX", "Analog stick X position");
        public static readonly MemoryAddress<byte> AnalogStickY = new(0x803A4DFA, "AnalogStickY", "Analog stick Y position");
        public static readonly MemoryAddress<byte> CStickX = new(0x803A4DFC, "CStickX", "C-stick X position");
        public static readonly MemoryAddress<byte> CStickY = new(0x803A4DFE, "CStickY", "C-stick Y position");
        public static readonly MemoryAddress<byte> LTriggerAnalog = new(0x803A4E00, "LTriggerAnalog", "L trigger analog value");
        public static readonly MemoryAddress<byte> RTriggerAnalog = new(0x803A4E01, "RTriggerAnalog", "R trigger analog value");
    }
    
    /// <summary>
    /// Game state and system addresses
    /// </summary>
    public static class System
    {
        public static readonly MemoryAddress<uint> FrameCounter = new(0x803A68E8, "FrameCounter", "Global frame counter");
        public static readonly MemoryAddress<float> TimeOfDay = new(0x803A2D14, "TimeOfDay", "Current time of day");
        public static readonly MemoryAddress<byte> GamePaused = new(0x803A5780, "GamePaused", "Game paused flag");
        public static readonly MemoryAddress<byte> DebugModeEnabled = new(0x803F7678, "DebugModeEnabled", "Debug mode enabled flag");
        public static readonly MemoryAddress<uint> RandomSeed = new(0x803D7970, "RandomSeed", "Random number generator seed");
        public static readonly MemoryAddress<byte> Language = new(0x803F9F48, "Language", "Game language setting");

        /// <summary>
        /// __OSStartTime (u64, dolphin/os/OS.c:39; symbols.txt .sbss 0x803F79B8, ww_linker.ld os____OSStartTime):
        /// OSInit stamps it once per boot (OS.c:230), so it tells this boot from the one before a soft reset.
        /// </summary>
        public static readonly ByteArrayMemoryAddress OSStartTime = new(0x803F79B8, 8, "OSStartTime", "OS boot time (changes on every boot / soft reset)");
    }
    
    /// <summary>
    /// Camera addresses
    /// </summary>
    public static class Camera
    {
        public static readonly MemoryAddress<uint> CameraMatrixPointer = new(0x803D43A8, "CameraMatrixPointer", "Pointer to camera matrix");
        public static readonly MemoryAddress<float> CameraPositionX = new(0x803ED838, "CameraPositionX", "Camera X position");
        public static readonly MemoryAddress<float> CameraPositionY = new(0x803ED83C, "CameraPositionY", "Camera Y position");
        public static readonly MemoryAddress<float> CameraPositionZ = new(0x803ED840, "CameraPositionZ", "Camera Z position");
        public static readonly MemoryAddress<float> CameraTargetX = new(0x803ED850, "CameraTargetX", "Camera target X");
        public static readonly MemoryAddress<float> CameraTargetY = new(0x803ED854, "CameraTargetY", "Camera target Y");
        public static readonly MemoryAddress<float> CameraTargetZ = new(0x803ED858, "CameraTargetZ", "Camera target Z");
    }
    
    /// <summary>
    /// Puppet synchronization shared memory — read by the puppet C code running inside Dolphin.
    /// Every value here is an alias of <see cref="PuppetLayout"/>, which is generated at build
    /// time from GameMod/src/puppet_link/puppet_shared.h. Never put a literal in this class:
    /// change the header instead so the C and C# sides cannot drift apart.
    /// </summary>
    public static class PuppetSync
    {
        // Header
        public const uint BaseAddress = PuppetLayout.PUPPET_SYNC_BASE;
        public const uint Magic = PuppetLayout.PUPPET_SYNC_MAGIC; // "PUPP"
        public const int HeaderSize = PuppetLayout.PUPPET_HDR_SIZE;
        public static readonly MemoryAddress<uint> MagicMarker = new(BaseAddress + PuppetLayout.PUPPET_HDR_OFF_MAGIC, "PuppetSyncMagic", "Validation marker (PUPP)");
        public static readonly MemoryAddress<uint> NumActive = new(BaseAddress + PuppetLayout.PUPPET_HDR_OFF_NUM_ACTIVE, "PuppetSyncNumActive", "Active puppet count (0-3)");
        public static readonly MemoryAddress<uint> WriteCounter = new(BaseAddress + PuppetLayout.PUPPET_HDR_OFF_WRITE_COUNTER, "PuppetSyncWriteCounter", "Incremented each write cycle");

        // Per-slot constants
        public const int MaxSlots = PuppetLayout.PUPPET_MAX_SLOTS;
        public const int SlotSize = PuppetLayout.PUPPET_SLOT_SIZE;
        public const uint Slot0Base = PuppetLayout.PUPPET_SLOT_0;
        public const uint SlotRegionEnd = Slot0Base + SlotSize * MaxSlots;

        // Slot field offsets (from slot base)
        public const int SlotOffset_Active = PuppetLayout.PUPPET_SLOT_OFF_ACTIVE;
        public const int SlotOffset_PosX = PuppetLayout.PUPPET_SLOT_OFF_POSX;
        public const int SlotOffset_PosY = PuppetLayout.PUPPET_SLOT_OFF_POSY;
        public const int SlotOffset_PosZ = PuppetLayout.PUPPET_SLOT_OFF_POSZ;
        public const int SlotOffset_RotY = PuppetLayout.PUPPET_SLOT_OFF_ROTY;
        public const int SlotOffset_AnimId = PuppetLayout.PUPPET_SLOT_OFF_ANIM_ID;
        public const int SlotOffset_AnimSpeed = PuppetLayout.PUPPET_SLOT_OFF_ANIM_SPEED;
        public const int SlotOffset_StateFlags = PuppetLayout.PUPPET_SLOT_OFF_STATE_FLAGS;
        public const int SlotOffset_CurProc = PuppetLayout.PUPPET_SLOT_OFF_CUR_PROC;
        public const int SlotOffset_EquipSword = PuppetLayout.PUPPET_SLOT_OFF_EQUIP_SWORD;
        public const int SlotOffset_EquipShield = PuppetLayout.PUPPET_SLOT_OFF_EQUIP_SHIELD;
        public const int SlotOffset_VelocityF = PuppetLayout.PUPPET_SLOT_OFF_VELOCITY_F;
        public const int SlotOffset_StickAngle = PuppetLayout.PUPPET_SLOT_OFF_STICK_ANGLE;
        public const int SlotOffset_SeqNum = PuppetLayout.PUPPET_SLOT_OFF_SEQ_NUM;
        public const int SlotOffset_ClothesType = PuppetLayout.PUPPET_SLOT_OFF_CLOTHES_TYPE;
        public const int SlotOffset_ColorR = PuppetLayout.PUPPET_SLOT_OFF_COLOR_R;
        public const int SlotOffset_ColorG = PuppetLayout.PUPPET_SLOT_OFF_COLOR_G;
        public const int SlotOffset_ColorB = PuppetLayout.PUPPET_SLOT_OFF_COLOR_B;
        public const int SlotOffset_EquipItem = PuppetLayout.PUPPET_SLOT_OFF_EQUIP_ITEM;
        public const int SlotOffset_ActionFlags = PuppetLayout.PUPPET_SLOT_OFF_ACTION_FLAGS;
        public const int SlotOffset_ProcSeq = PuppetLayout.PUPPET_SLOT_OFF_PROC_SEQ;
        public const int SlotOffset_SpeedF = PuppetLayout.PUPPET_SLOT_OFF_SPEED_F;
        public const int SlotOffset_MaxNormalSpeed = PuppetLayout.PUPPET_SLOT_OFF_MAX_NORMAL_SPEED;
        public const int SlotOffset_StickDistance = PuppetLayout.PUPPET_SLOT_OFF_STICK_DISTANCE;
        public const int SlotOffset_ModeFlg = PuppetLayout.PUPPET_SLOT_OFF_MODE_FLG;
        public const int SlotOffset_NoResetFlg0 = PuppetLayout.PUPPET_SLOT_OFF_NO_RESET_FLG0;
        public const int SlotOffset_NoResetFlg1 = PuppetLayout.PUPPET_SLOT_OFF_NO_RESET_FLG1;
        public const int SlotOffset_BodyAngleX = PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_X;   // daPy_py_c::mBodyAngle.x (+0x2B4, d_a_player.h:491)
        public const int SlotOffset_BodyAngleY = PuppetLayout.PUPPET_SLOT_OFF_BODY_ANGLE_Y;   // mBodyAngle.y (+0x2B6)
        public const int SlotOffset_GrabKind = PuppetLayout.PUPPET_SLOT_OFF_GRAB_KIND;        // mActorKeepGrab (+0x318C, d_a_player_main.h:2088) -> PUPPET_GRAB_KIND_*
        public const int SlotOffset_GrabFuse = PuppetLayout.PUPPET_SLOT_OFF_GRAB_FUSE;        // the carried bomb's daBomb_c::mRestTime (+0x6FC), capped at 255

        // Hook tracking arrays
        public const uint ProcIdsAddr = PuppetLayout.PUPPET_PROC_IDS_ADDR;
        public const uint ActorPtrsAddr = PuppetLayout.PUPPET_ACTOR_PTRS_ADDR;
        public const uint SpawnStateAddr = PuppetLayout.PUPPET_SPAWN_STATE_ADDR;
        public const uint SpawnFrameCounterAddr = PuppetLayout.SPAWN_FRAME_COUNTER_ADDR;

        // Hook debug counters
        public const uint DbgMagicDetectedAddr = PuppetLayout.DBG_MAGIC_DETECTED_ADDR;
        public const uint DbgCreateAttemptsAddr = PuppetLayout.DBG_CREATE_ATTEMPTS_ADDR;
        public const uint DbgCreateSuccessesAddr = PuppetLayout.DBG_CREATE_SUCCESSES_ADDR;
        public const uint DbgCreateFailuresAddr = PuppetLayout.DBG_CREATE_FAILURES_ADDR;
        public const uint DbgLastDesiredAddr = PuppetLayout.DBG_LAST_DESIRED_ADDR;
        public const uint DbgLastPidAddr = PuppetLayout.DBG_LAST_PID_ADDR;
        public const uint DbgDeleteIssuedAddr = PuppetLayout.DBG_DELETE_ISSUED_ADDR;

        // Client-written scratch (heartbeat + diagnostics mirrored for the UI/logs)
        public const uint ClientHeartbeatAddr = PuppetLayout.CLIENT_HEARTBEAT_ADDR;
        public const uint ClientDbgSlot0GatesAddr = PuppetLayout.CLIENT_DBG_SLOT0_GATES;
        public const uint ClientDbgUnstableHitsAddr = PuppetLayout.CLIENT_DBG_UNSTABLE_HITS;
        public const uint ClientDbgStabilityBitsAddr = PuppetLayout.CLIENT_DBG_STABILITY_BITS;
        public const uint ClientDbgLastActiveAddr = PuppetLayout.CLIENT_DBG_LAST_ACTIVE;
        public const uint ClientDbgWriteCounterAddr = PuppetLayout.CLIENT_DBG_WRITE_COUNTER;

        public static uint GetSlotBase(int slotIndex) => Slot0Base + (uint)(slotIndex * SlotSize);

        // Per-slot boat block (the peer's King of Red Lions)
        public const int BoatSize = PuppetLayout.PUPPET_BOAT_SIZE;
        public const uint Boat0Base = PuppetLayout.PUPPET_BOAT_0;
        public static uint GetBoatBase(int slotIndex) => Boat0Base + (uint)(slotIndex * BoatSize);

        // Per-slot cannon / crane words (the peer's boat parts; two scratch gaps, see puppet_shared.h)
        public const int BoatCannonSize = PuppetLayout.PUPPET_BOAT_CANNON_SIZE;
        public const uint BoatCannon0Base = PuppetLayout.PUPPET_BOAT_CANNON_0;
        public static uint GetBoatCannonBase(int slotIndex) => BoatCannon0Base + (uint)(slotIndex * BoatCannonSize);
        public const int BoatCraneSize = PuppetLayout.PUPPET_BOAT_CRANE_SIZE;
        public const uint BoatCrane0Base = PuppetLayout.PUPPET_BOAT_CRANE_0;
        public static uint GetBoatCraneBase(int slotIndex) => BoatCrane0Base + (uint)(slotIndex * BoatCraneSize);
    }

    /// <summary>
    /// Per-stage persistent world flags (dSv_memBit_c inside dSv_memory_c), used by shared-world
    /// sync. g_dComIfG_gameInfo = 0x803C4C08; dSv_info_c starts at +0 (tww-decomp d_save.h).
    /// </summary>
    public static class WorldFlags
    {
        /// <summary>dSv_save_c::mMemory[16] — saved flags per stage slot (gameInfo + 0x380).</summary>
        public const uint SavedMemoryBase = GameInfo + 0x380; // dSv_save_c /* 0x380 */ mMemory
        /// <summary>dSv_info_c::mMemory — LIVE flags for the current stage (gameInfo + 0x778).
        /// Copied from/to SavedMemoryBase[slot] on stage load/leave.</summary>
        public const uint LiveMemory = GameInfo + 0x778;      // dSv_info_c /* 0x0778 */ mMemory
        public const int MemorySize = 0x24; // sizeof(dSv_memory_c)

        // dSv_memBit_c field offsets within a dSv_memory_c
        public const int OffTbox = 0x00;        // u32
        public const int OffSwitch = 0x04;      // u32[4]
        public const int OffItem = 0x14;        // u32
        public const int OffVisitedRoom = 0x18; // u32[2]
        public const int OffKeyNum = 0x20;      // u8 mKeyNum — not a flag: derived from the flags (SmallKeyReconciler)
        public const int OffDungeonItem = 0x21; // u8

        public static uint SavedSlot(int slot) => SavedMemoryBase + (uint)(slot * MemorySize);

        /// <summary>play.mStageData.mpStagInfo (gameInfo + 0x12A0 + 0x3EB0 + 0x48) — pointer to the
        /// current stage's STAG info. Slot = (byte at +StagSaveTblOffset >> 1) &amp; 0x7F
        /// (dStage_stagInfo_GetSaveTbl; DOL dComIfGs_onStageTbox 0x800538B0).</summary>
        public const uint StagInfoPtr = Play + 0x3EB0 + 0x48;  // play mStageData + mpStagInfo
        public const int StagSaveTblOffset = 0x09;
        /// <summary>stage_stag_info_class::mProp bit 0 (the same byte as <see cref="StagSaveTblOffset"/>):
        /// dStage_stagInfo_ChkKeyDisp, the stage shows the small-key HUD. dMeter_keyMove applies
        /// <see cref="PendingKeyDelta"/> only then (DOL 0x801FCF58: lbz r0,9(stagInfo); clrlwi. r0,r0,31).</summary>
        public const int StagKeyDispMask = 0x01;

        /// <summary>The live mKeyNum of the current stage (dSv_info_c /* 0x0778 */ mMemory + dSv_memBit_c /* 0x20 */
        /// mKeyNum) = 0x803C53A0. DOL dMeter_keyMove 0x801FCF78 / 0x801FCFB4: lbz / stb 0x798(gameInfo).</summary>
        public const uint LiveKeyNum = LiveMemory + OffKeyNum;

        /// <summary>play.mItemKeyNumCount (d_com_inf_game.h "/* 0x48D4 */ s16") = 0x803CA77C: the small-key change
        /// queued for the HUD. item_func_small_key adds 1 (DOL 0x800C31B8: lha 0x5B74(gameInfo)), dDoor_key2_c::keyInit
        /// subtracts 1 (0x8006C52C); dMeter_keyMove adds it to mKeyNum clamped 0..99, zeroes it and animates the HUD
        /// digit (0x801FCF6C-0x801FCFBC) — only in a stage with <see cref="StagKeyDispMask"/>.</summary>
        public static readonly MemoryAddress<short> PendingKeyDelta = new(Play + 0x48D4, "PendingKeyDelta", "Small-key change queued for the HUD (dComIfGp_setItemKeyNumCount)");
        /// <summary>play.mNextStage.mEnable (s8) — nonzero while a stage change is pending; the
        /// live copy and stag pointer are in flux then (putSave → getSave).</summary>
        public const uint NextStageEnable = Play + 0x3EA0 + 0x0C; // play mNextStage + mEnable

        /// <summary>dSv_info_c::mDan (dSv_danBit_c, gameInfo + 0x79C; DOL isSwitch 0x8005DEEC: addi r3,0x79C).
        /// The dungeon-visit switches 0x80-0xBF of the save slot in <see cref="DanOffStageNo"/>.</summary>
        public const uint LiveDan = GameInfo + 0x79C;      // dSv_info_c /* 0x079C */ mDan
        public const int DanOffStageNo = 0x00;             // s8 mStageNo = the slot (dSv_danBit_c::init, DOL 0x8005CBF0)
        public const int DanOffSwitch = 0x04;              // u32 mSwitch[2] (init clears +4, +8)

        /// <summary>dSv_info_c::mZone[32] (dSv_zone_c, 0x4C each, gameInfo + 0x7A8; DOL createZone 0x8005DAD8
        /// addi r3,0x7A8 / addi 0x4C, isSwitch 0x8005DFB4 mulli 0x4C, addi 0x7AA).</summary>
        public const uint ZoneBase = GameInfo + 0x7A8;     // dSv_info_c /* 0x07A8 */ mZone
        public const int ZoneSize = 0x4C;
        public const int ZoneCount = 32;                   // dSv_info_c::ZONE_MAX
        public const int ZoneOffRoomNo = 0x00;             // s8 mRoomNo (< 0 = unused)
        public const int ZoneOffSwitch = 0x02;             // u16 mSwitch[3]: 0xC0-0xCF, 0xD0-0xDF, 0xE0-0xEF

        public static uint Zone(int zoneNo) => ZoneBase + (uint)(zoneNo * ZoneSize);

        /// <summary>dStage_roomControl_c::mStatus[64] (static, ww_linker.ld dStage_roomControl_c__mStatus;
        /// dStage_roomStatus_c 0x114 each, d_stage.h:855-869). Not part of gameInfo.</summary>
        public const uint RoomStatusBase = 0x803BDC88;
        public const int RoomStatusSize = 0x114;
        public const int RoomStatusOffZoneNo = 0x107;      // s8 mZoneNo (DOL getZoneNo 0x8005DCE0: lbz r3,0x107)

        public static uint RoomZoneNo(int room) => RoomStatusBase + (uint)(room * RoomStatusSize) + RoomStatusOffZoneNo;
        /// <summary>play.mMiniGameType (d_com_inf_game.h "/* 0x4A3A */ u8 mMiniGameType"; daArrow_c::_create reads
        /// it at 0x800D79C8): nonzero while a minigame runs (dComIfGp_startMiniGame: 1 sailing race, 2 / 6 Orca's
        /// training, 3 Spectacle Island cannons, 5 auction, 7 mail sorting (d_a_npc_bmsw / btsw), 8 the bow game).
        /// Only endMiniGame clears it (and a reset, which re-zeroes the DOL's .bss).</summary>
        public const uint MiniGameType = Play + 0x4A3A;
    }

    /// <summary>
    /// Derived max health (SharedHeartService, docs/hearts.md): the save data the heart flags live in, read as one
    /// block from gameInfo (dSv_save_c: dSv_player_c /* 0x000 */ .. dSv_event_c /* 0x624 */, d_save.h:908-911),
    /// and the game's own pending max-life change.
    /// </summary>
    public static class Hearts
    {
        /// <summary>dSv_player_status_a_c /* 0x0 */ mMaxLife, /* 0x2 */ mLife (u16, quarter hearts).</summary>
        public const int OffMaxLife = 0x0, OffLife = 0x2;
        /// <summary>dSv_player_c /* 0x076 */ mBagItem → dSv_player_bag_item_c /* 0x10 */ mReserve[8]: the delivery bag.</summary>
        public const int OffDeliveryBag = 0x76 + 0x10;
        /// <summary>dSv_player_c /* 0x090 */ mGetBagItem → dSv_player_get_bag_item_c /* 0x0 */ u32 mReserveFlags.</summary>
        public const int OffGetBagReserve = 0x90;
        /// <summary>dSv_player_c /* 0x0C4 */ mMap → dSv_player_map_c field_0x0[3][4] (+0x30): isCompleteMap (d_save.cpp:894-897).</summary>
        public const int OffCompleteMaps = 0xC4 + 0x30;
        /// <summary>dSv_save_c /* 0x380 */ mMemory[16] (the saved memBits; <see cref="WorldFlags.SavedMemoryBase"/>).</summary>
        public const int OffSavedMemory = 0x380;
        /// <summary>dSv_save_c /* 0x5C0 */ mOcean: dSv_ocean_c u16 field_0x0[50] (isOceanSvBit, d_save.cpp:1171-1175).</summary>
        public const int OffOcean = 0x5C0;
        /// <summary>dSv_save_c /* 0x624 */ mEvent (<see cref="Events.EventBits"/>).</summary>
        public const int OffEvents = 0x624;
        /// <summary>gameInfo .. the end of mEvent (0x624 + 0x100).</summary>
        public const int SaveBlockLength = OffEvents + 0x100;

        /// <summary>play.mItemMaxLifeCount (d_com_inf_game.h "/* 0x48D6 */ s16"): the max-life change queued for the HUD.
        /// item_func_kakera_heart adds 1 and item_func_utuwa_heart 4 (DOL 0x800C2F30 / 0x800C2F54: lha / sth
        /// 0x5B76(gameInfo)); dMeter_LifeMove adds it to mMaxLife clamped 0..80, zeroes it, refills life on a gain and
        /// clamps it on a loss, and animates the heart row (d_meter.cpp:1405-1433).</summary>
        public static readonly MemoryAddress<short> PendingMaxLife = new(Play + 0x48D6, "PendingMaxLife", "Max-life change queued for the HUD (dComIfGp_setItemMaxLifeCount)");
        /// <summary>dComIfG_play_c::mItemMaxMagicCount (d_com_inf_game.h 0x48DC, beside mItemMaxLifeCount): the max-magic
        /// gain the Deku Leaf / Great Fairy queued, which the magic meter adds to mMaxMagic (capped at 32) and zeroes
        /// (d_meter.cpp:4045-4052).</summary>
        public static readonly MemoryAddress<short> PendingMaxMagic = new(Play + 0x48DC, "PendingMaxMagic", "Max-magic change queued for the HUD (dComIfGp_setItemMaxMagicCount)");
    }

    /// <summary>
    /// Warp to player (WarpService, docs/softlocks.md): where the current stage was entered, the
    /// stage-change request, and the local Link's position.
    /// </summary>
    public static class Warp
    {
        /// <summary>play.mCurStage (d_com_inf_game.h "/* 0x3E94 */ dStage_startStage_c mCurStage"): the stage,
        /// spawn point, room and layer the current stage was entered with. dStage_playerInit rewrites its room
        /// with the spawn's own room (d_stage.cpp:1477). Point -1 / -2 / -3 = a restart (void-out, a ship
        /// interior's exit, Song of Passing), not a spawn point.</summary>
        public const uint StartStage = Play + 0x3E94;

        /// <summary>play.mNextStage (d_com_inf_game.h "/* 0x3EA0 */ dStage_nextStage_c mNextStage"). dScnPly_Draw
        /// (d_s_play.cpp:291) starts the scene change while mEnable != 0; the next play scene's phase_1 copies it into
        /// mCurStage and clears mEnable (d_s_play.cpp:1277-1278).</summary>
        public const uint NextStage = Play + 0x3EA0;

        // dStage_startStage_c / dStage_nextStage_c fields (d_stage.h:949-952, 966-967). DOL
        // set__19dStage_startStage_c (0x80040BA8): strcpy name, stb room 0xA, sth point 0x8, stb layer 0xB;
        // set__18dStage_nextStage_c (0x80040900): lbz/stb enable 0xC, stb wipe 0xD.
        public const int StageOffName = 0x0;       // char mName[8]
        public const int StageNameSize = 8;
        public const int StageOffPoint = 0x8;      // s16 mPoint
        public const int StageOffRoom = 0xA;       // s8 mRoomNo
        public const int StageOffLayer = 0xB;      // s8 mLayer
        public const int NextStageOffEnable = 0xC; // s8 mEnable
        public const int NextStageOffWipe = 0xD;   // s8 mWipe (an index into dScnPly_Draw's l_wipeType[12]; 0 = the usual fade)

        /// <summary>dSv_info_c /* 0x1128 */ mRestart (dSv_restart_c, d_save.h:826-837). dComIfGp_setNextStage
        /// (DOL 0x800537C8) writes mLastSpeedF (stfs 0x1150(gameInfo)), mLastMode (stw 0x1154) and, with
        /// i_setPoint, mStartCode (sth 0x113C); playerInit reads the last two to pick how Link arrives.</summary>
        public const uint Restart = GameInfo + 0x1128;
        public const int RestartOffStartCode = 0x14;  // s16 mStartCode
        public const int RestartOffLastSpeedF = 0x28; // f32 mLastSpeedF
        public const int RestartOffLastMode = 0x2C;   // u32 mLastMode

        /// <summary>play.mPlayerInfo[0].mpPlayer (d_com_inf_game.h "/* 0x48A4 */", dComIfGp_getPlayer(0)): the
        /// character the player controls, an NPC while they control Medli, Makar, a seagull or Hyoi.</summary>
        public const uint ControlledActorPtr = Play + 0x48A4;
        /// <summary>play.mpPlayerPtr[0] (d_com_inf_game.h "/* 0x48AC */"): the local Link
        /// (daPy_getPlayerLinkActorClass; DOL 0x80053820: lwz r3,0x5B4C(gameInfo)).</summary>
        public const uint LinkActorPtr = Play + 0x48AC;

        // fopAc_ac_c (f_op_actor.h:288-302). fopAc_Execute copies current into old before each execute
        // (f_op_actor.cpp:196); Link's Acch checks the move from old.pos to current.pos (d_a_player_main.cpp:12182).
        public const uint ActorOffOldPos = 0x1E4;          // old.pos (cXyz)
        public const uint ActorOffPos = 0x1F8;             // current.pos (cXyz)
        public const uint ActorOffAngleY = 0x206;          // current.angle.y (s16, the movement direction)
        public const uint ActorOffRoomNo = 0x20A;          // current.roomNo (s8)
        public const uint ActorOffShapeAngleY = 0x20E;     // shape_angle.y (s16, the facing)
        public const uint ActorOffSpeed = 0x220;           // speed (cXyz)
        public const uint ActorOffSpeedF = 0x254;          // speedF (f32)

        /// <summary>
        /// daPy_lk_c::execute starts with current.pos = l_debug_keep_pos, shape_angle = l_debug_shape_angle,
        /// current.angle = l_debug_current_angle (d_a_player_main.cpp:11251-11255, retail too) and saves them
        /// again at its end (:11716-11718): a position written only to the actor is undone the next frame.
        /// Addresses: tww-decomp config/GZLE01/symbols.txt (.bss / .sbss statics of d_a_player_main.cpp).
        /// </summary>
        public const uint LinkKeepPos = 0x803E440C;          // l_debug_keep_pos (cXyz)
        public const uint LinkKeepCurrentAngle = 0x803F6F10; // l_debug_current_angle (csXyz)
        public const uint LinkKeepShapeAngle = 0x803F6F18;   // l_debug_shape_angle (csXyz)

        /// <summary>daPy_lk_c mTinkleShieldTimer (d_a_player_main.h:2230 "/* 0x354E */ s16"). With mNoResetFlg1's
        /// EQUIP_DRAGON_SHIELD (0x1) and SOUP_POWER_UP (0x8000) bits (d_a_player.h:199, 213) it is what
        /// dComIfGp_setNextStage folds into mLastMode (DOL 0x8005382C-0x80053850: 0x8000, timer &lt;&lt; 16, 0x4000),
        /// so the Magic Armor, a timed shield and the soup's power-up survive the stage change.</summary>
        public const uint LinkOffTinkleShieldTimer = 0x354E;
        public const uint NoResetFlg1DragonShield = 0x00000001;
        public const uint NoResetFlg1SoupPowerUp = 0x00008000;
        public const uint LastModeDragonShield = 0x8000;
        public const uint LastModeSoupPowerUp = 0x4000;

        /// <summary>mModeFlg bits that tie Link to something (d_a_player_main.h:896-927: HANG, HOOKSHOT, ROPE,
        /// IN_SHIP, CLIMB, GRAB, PUSHPULL, LADDER, CRAWL, CAUGHT): his proc would pull him back after a move.</summary>
        public const uint ModeFlgAttached = 0x00000020 | 0x00000200 | 0x00000800 | 0x00002000 | 0x00010000 |
                                            0x00100000 | 0x00200000 | 0x00400000 | 0x01000000 | 0x10000000;
    }

    /// <summary>
    /// Sea and sailing addresses
    /// </summary>
    public static class Sea
    {
        public static readonly MemoryAddress<byte> CurrentSeaSector = new(0x803C9D5C, "CurrentSeaSector", "Current sea sector (quadrant)");
        public static readonly MemoryAddress<float> BoatSpeed = new(0x803F9F50, "BoatSpeed", "King of Red Lions speed");
        public static readonly MemoryAddress<float> BoatDirection = new(0x803F9F54, "BoatDirection", "King of Red Lions direction");
        public static readonly MemoryAddress<byte> SailState = new(0x803C4CC4, "SailState", "Sail state (up/down)");

        /// <summary>play.mpPlayerPtr[2] — the King of Red Lions (daShip_c*), or 0. d_com_inf_game.h
        /// "/* 0x48AC */ fopAc_ac_c* mpPlayerPtr[3]; // 0: Link, 1: Partner, 2: Ship" (dComIfGp_getShipActor).</summary>
        public const uint ShipActorPtr = Play + 0x48AC + 2 * 4;
        /// <summary>The Great Sea's stage name, where a boat you got off stays (and shows parked to others).</summary>
        public const string SeaStageName = "sea";
        /// <summary>base_process_class mProcName (s16 at +0x8, as the REL's BASE_PROC_NAME) and daShip_c's (g_profile_SHIP
        /// in the vanilla d_a_ship.rel).</summary>
        public const uint ActorOffsetProcName = 0x08;
        public const ushort ProcNameShip = 0x0A7;

        // daShip_c fields (d_a_ship.h); position/angle/speed are the fopAc_ac_c base fields.
        public const uint ShipOffsetPosX = 0x1F8;      // current.pos
        public const uint ShipOffsetRotY = 0x20E;      // shape_angle.y (s16)
        public const uint ShipOffsetSpeedF = 0x254;    // speedF
        public const uint ShipOffsetStateFlag = 0x358; // u32 mStateFlag
        public const uint ShipStateFly = 0x00000001;   // daSFLG_FLY_e

        // The boat's pose, as daShip_c's joint callbacks and morfs use it (d_a_ship.cpp:110-231, 3960-3976).
        public const uint ShipOffsetBodyAnm = 0x298;   // mDoExt_McaMorf* mpBodyAnm (FN_BODY + mast bck)
        public const uint ShipOffsetHeadAnm = 0x29C;   // mDoExt_McaMorf* mpHeadAnm (FN_HEAD_H + head bck)
        public const uint ShipOffsetSailAngle = 0x364; // s16 mSailAngle (J_FN_SAIL1 Z rotation)
        public const uint ShipOffsetTiller = 0x366;    // s16 m0366 (J_FN_STEER1 / J_FN_KAJI Z rotation)
        public const uint ShipOffsetMastBck = 0x392;   // u16 m0392 "file idx": FN_MAST_ON2 / FN_MAST_OFF2
        public const uint ShipOffsetHeadX = 0x3A0;     // s16 m03A0 head look pitch (headJointCallBack1)
        public const uint ShipOffsetHeadY = 0x3A2;     // s16 m03A2 head look yaw
        public const uint ShipOffsetHeadBck = 0x3B4;   // u16 m03B4 "file idx": the head's bck
        public const uint ShipOffsetMastScale = 0x3E8; // f32 m03E8: J_FN_MAST scale, 1.0 or 0.001

        // The cannon / crane (d_a_ship.cpp:119-179, 304-317). Checked against the vanilla d_a_ship.rel:
        // cannonJointCallBack reads 0x394 / 0x396, craneJointCallBack 0x398 + 0x39C, draw 0x34E / 0x39E.
        public const uint ShipOffsetPart = 0x34E;        // u8 mPart (daShip_c::Part_e): what's on the mast joint
        public const uint ShipOffsetCannonYaw = 0x394;   // s16 m0394: CANON1 X rotation
        public const uint ShipOffsetCannonPitch = 0x396; // s16 m0396: CANON2 -Y rotation
        public const uint ShipOffsetCraneAngle = 0x398;  // s16 m0398: the arm's angle
        public const uint ShipOffsetCraneSwing = 0x39C;  // s16 m039C: the hook's swing, added to m0398
        public const uint ShipOffsetRopeCnt = 0x39E;     // s16 mRopeCnt: rope segments (0..250)
        /// <summary>mDoExt_McaMorf mFrameCtrl (0x58, m_Do_ext.h) + J3DFrameCtrl mFrame (0x10, J3DAnimation.h):
        /// the morf's current bck frame (f32).</summary>
        public const uint McaMorfOffsetFrame = 0x58 + 0x10;

        /// <summary>daPyStts0_SHIP_RIDE_e in play.mPlayerStatus[0][0] (d_com_inf_game.h).</summary>
        public const uint PlayerStatus0ShipRide = 0x00010000;
        /// <summary>daPyProc_SHIP_READY_e .. daPyProc_SHIP_RESTART_e and daPyProc_DEMO_SHIP_SIT_e
        /// (d_a_player_main.h): Link's procs while on the boat.</summary>
        public const byte ProcShipFirst = 0x86;
        public const byte ProcShipLast = 0x91;
        public const byte ProcDemoShipSit = 0xD7;
    }
}