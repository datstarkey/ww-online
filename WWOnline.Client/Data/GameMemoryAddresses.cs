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
        
        // Equipment
        public static readonly MemoryAddress<byte> XButtonItem = new(0x803C4C11, "XButtonItem", "X button equipped item");
        public static readonly MemoryAddress<byte> YButtonItem = new(0x803C4C12, "YButtonItem", "Y button equipped item");
        public static readonly MemoryAddress<byte> ZButtonItem = new(0x803C4C13, "ZButtonItem", "Z button equipped item");
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
        public static readonly ByteArrayMemoryAddress EventBitfield = new(0x803C522C, 256, "EventBitfield", "Event bit flags");
        // dSv_save_c /* 0x624 */ mEvent; isEventBit(no) = mFlags[no >> 8] & (no & 0xFF) (d_save.cpp:1197).
        // playerInit tests EVENT_BIT_HERO_CLOTHES there (DOL 0x80125AD0: addi r3,r3,0x624; li r4,0x2A80).
        public const uint EventBits = GameInfo + 0x624;
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
        public const int OffKeyNum = 0x20;      // u8 — NOT synced (a count)
        public const int OffDungeonItem = 0x21; // u8

        public static uint SavedSlot(int slot) => SavedMemoryBase + (uint)(slot * MemorySize);

        /// <summary>play.mStageData.mpStagInfo (gameInfo + 0x12A0 + 0x3EB0 + 0x48) — pointer to the
        /// current stage's STAG info. Slot = (byte at +StagSaveTblOffset >> 1) &amp; 0x7F
        /// (dStage_stagInfo_GetSaveTbl; DOL dComIfGs_onStageTbox 0x800538B0).</summary>
        public const uint StagInfoPtr = Play + 0x3EB0 + 0x48;  // play mStageData + mpStagInfo
        public const int StagSaveTblOffset = 0x09;
        /// <summary>play.mNextStage.mEnable (s8) — nonzero while a stage change is pending; the
        /// live copy and stag pointer are in flux then (putSave → getSave).</summary>
        public const uint NextStageEnable = Play + 0x3EA0 + 0x0C; // play mNextStage + mEnable
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