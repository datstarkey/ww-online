using static WWOnline.Shared.Models.EventFlagCategory;
using P = WWOnline.Shared.Models.EventRegisterPolicy;

namespace WWOnline.Shared.Models;

/// <summary>How a single event bit behaves when players share story progress.</summary>
public enum EventFlagCategory
{
    /// <summary>
    /// Main-quest progression: prologue steps, companions joining, pearls placed, dungeon/boss milestones,
    /// Hyrule state, island landing gates, stage-layer selectors. The point of "Full sync".
    /// </summary>
    Story,

    /// <summary>
    /// "This demo/cutscene has already played" latches (usually set by the event system's EventFlag or an
    /// <c>if (!flag) { on(flag); playDemo(); }</c> pattern). Sharing them just skips the cutscene.
    /// </summary>
    CutsceneSeen,

    /// <summary>Optional quest progress (Tingle, Beedle, Lenzo, Gallery, minigames, ferris wheel...).</summary>
    SideQuest,

    /// <summary>
    /// "Got it" flags for things that are NOT in the inventory arrays: auction purchases, lithograph item-get
    /// checks, NPC reward-given latches, taught sword techniques.
    /// </summary>
    Collectible,

    /// <summary>"Already talked to X" dialogue selectors. Harmless to share; only changes which line an NPC says.</summary>
    NpcState,

    /// <summary>King of Red Lions hint messages, first-use hints (grappling hook, first sail, first door...).</summary>
    TutorialHint,

    /// <summary>
    /// Must never be synced: the game clears it again during normal play (daily/weekly resets, toggles,
    /// recomputed-on-load state) or it describes the local player's own clock/ship/position/session.
    /// OR-merging these latches them on and breaks the owning actor.
    /// </summary>
    LocalOnly,

    /// <summary>
    /// Named in the decomp enum but purpose not established from src (most have no src reference at all and
    /// are set/tested only by stage, event-list or message data). Synced unless marked risky.
    /// </summary>
    Unknown,
}

/// <summary>How an 8-bit event REGISTER (bytes 0x79-0xFF of the same array) may be merged. Never OR-merge blindly.</summary>
public enum EventRegisterPolicy
{
    /// <summary>Counter, item number, score, random value or daily-reset state. Do not sync.</summary>
    LocalOnly,

    /// <summary>Register is itself a bitfield that only ever gains bits (figurines, warp pots). OR within the register mask is safe.</summary>
    BitwiseOr,

    /// <summary>Monotonic state machine (letter 0..3, ghost ship 0..3, lesson level). Merge with max() inside the register mask.</summary>
    Max,

    /// <summary>No src reference; semantics unknown. Do not sync.</summary>
    Unknown,
}

/// <param name="Id">Decomp encoding: <c>(byteIndex &lt;&lt; 8) | bitMask</c> (dSv_event_c::onEventBit).</param>
/// <param name="Risky">
/// For Unknown: excluded from Full sync. For Story: still synced (that is the feature) but setting it without
/// its normal trigger has a known side effect; see docs/event-flags.md.
/// </param>
/// <param name="Description">
/// Readable description for the UI (e.g. RODE_KORL: "Entered KoRL for the first time"), or null when nothing is
/// known. <see cref="Name"/> stays the decomp name; this is the only other name a flag has.
/// </param>
public sealed record EventFlagInfo(ushort Id, string Name, EventFlagCategory Category, bool Risky = false, string? Description = null)
{
    public int ByteIndex => Id >> 8;
    public byte Mask => (byte)(Id & 0xFF);

    /// <summary>True for the decomp's placeholder names (UNK_xxxx).</summary>
    public bool HasPlaceholderName => Name.StartsWith("UNK_", StringComparison.Ordinal);

    public bool SyncableUnderFullSync =>
        Category != EventFlagCategory.LocalOnly && !(Category == EventFlagCategory.Unknown && Risky);
}

/// <param name="Id">Decomp encoding: <c>(byteIndex &lt;&lt; 8) | valueMask</c> (dSv_event_c::setEventReg).</param>
public sealed record EventRegisterInfo(ushort Id, string Name, EventRegisterPolicy Policy)
{
    public int ByteIndex => Id >> 8;
    public byte Mask => (byte)(Id & 0xFF);
}

/// <summary>
/// Classification of every named Wind Waker (GZLE01) save event flag, derived from the zeldaret/tww decomp
/// (include/d/d_save_event_flag.inc + every dComIfGs_on/off/is EventBit and set/get EventReg call in src/).
/// See docs/event-flags.md for method and evidence.
/// <para>
/// Storage: dSv_save_c::mEvent (dSv_event_c, u8 mFlags[0x100]) = g_dComIfG_gameInfo (0x803C4C08) + 0x624.
/// Bytes 0x00-0x41 hold single-bit flags; 0x42-0x78 have no named flags; 0x79-0xFF are multi-bit registers.
/// dSv_info_c::mTmp (+0x1158) is a second dSv_event_c of per-session temp bits: never sync it.
/// </para>
/// </summary>
public static class EventFlagCatalog
{
    public const uint EventBitfieldAddress = 0x803C4C08 + 0x624; // 0x803C522C
    public const uint TmpEventBitfieldAddress = 0x803C4C08 + 0x1158; // 0x803C5D60, dSv_info_c::mTmp (not saved)
    public const int EventBitfieldSize = 0x100;

    /// <summary>First byte used by event registers. Everything from here to 0xFF is multi-bit and not OR-safe.</summary>
    public const int FirstRegisterByte = 0x79;

    /// <summary>Bytes 0x00-0x41 hold every named single-bit flag (0x42-0x78 are unused).</summary>
    public const int BitFlagBytes = 0x42;

    private static EventFlagInfo F(ushort id, string name, EventFlagCategory category, bool risky = false, string? desc = null) =>
        new(id, name, category, risky, desc);

    private static EventRegisterInfo R(ushort id, string name, EventRegisterPolicy policy) => new(id, name, policy);

    /// <summary>All 487 named event bits, in array order (byte ascending, bit descending).</summary>
    public static IReadOnlyList<EventFlagInfo> Flags { get; } =
    [
        // ---- byte 0x00 ----
        F(0x0080, "UNK_0080", Unknown),
        F(0x0020, "UNK_0020", Unknown),
        F(0x0010, "UNK_0010", Unknown),
        F(0x0008, "UNK_0008", Unknown),
        F(0x0004, "UNK_0004", CutsceneSeen, desc: "Bokoblins drop into Outset's forest (cutscene)"), // FF1 Bokoblin fly-down demo (tag_event/bk carry_drop)
        F(0x0002, "UNK_0002", NpcState, desc: "Orca dialogue"), // Orca dialogue (ji1)
        F(0x0001, "UNK_0001", Story, desc: "Prologue: Aryll's telescope, Helmaroc arrives on Outset"), // Prologue: Aryll/telescope step (ls1 event_proc); gates Outset NPCs, pirate ship, audio
        // ---- byte 0x01 ----
        F(0x0180, "UNK_0180", Unknown),
        F(0x0140, "UNK_0140", Unknown),
        F(0x0120, "UNK_0120", Unknown),
        F(0x0108, "UNK_0108", NpcState, desc: "Orca: talked after the Helmaroc"), // Orca dialogue (ji1)
        F(0x0104, "UNK_0104", Unknown),
        F(0x0102, "UNK_0102", Unknown, desc: "Withered trees quest (sea chart)"), // read by sea chart menu only (d_menu_fmap)
        F(0x0101, "UNK_0101", Story, desc: "Prologue: Tetra rescued from the forest"), // Prologue: Tetra-falls demo (tag_event); Outset layer 9
        // ---- byte 0x02 ----
        F(0x0280, "UNK_0280", CutsceneSeen, desc: "Look-at-Tetra cutscene started"), // 'look_tetra' event started (d_event_manager exceptionProc)
        F(0x0240, "UNK_0240", Unknown),
        F(0x0220, "UNK_0220", Unknown),
        F(0x0210, "UNK_0210", Unknown),
        F(0x0208, "UNK_0208", Unknown),
        F(0x0201, "UNK_0201", Unknown),
        // ---- byte 0x03 ----
        F(0x0380, "UNK_0380", Unknown),
        F(0x0340, "UNK_0340", Unknown, desc: "Caught by a Forsaken Fortress searchlight"),
        F(0x0310, "UNK_0310", Story, desc: "Prologue: used the telescope, pirate ship at Outset"), // Prologue: used telescope (ls1); gates Helmaroc/pirate actors
        F(0x0308, "UNK_0308", Unknown),
        F(0x0304, "UNK_0304", Unknown),
        F(0x0302, "UNK_0302", Unknown),
        F(0x0301, "UNK_0301", TutorialHint, desc: "A Bokoblin defeated (first)"), // Set when a Bokoblin dies (d_a_bk.cpp fail(), type != 4); read by andsw0 (FF1)
        // ---- byte 0x04 ----
        F(0x0480, "UNK_0480", TutorialHint, desc: "A second Bokoblin defeated"), // Set when a Bokoblin dies with 0301 already set (d_a_bk.cpp fail()); read by andsw0 (FF1)
        F(0x0420, "UNK_0420", TutorialHint, desc: "Grappling hook first-use hint"), // Grappling hook first-use (himo2)
        F(0x0404, "UNK_0404", TutorialHint, desc: "Forsaken Fortress: thrown in the jail cell"), // tag_hint shown
        F(0x0402, "UNK_0402", TutorialHint, desc: "Rope swing hint"), // Rope swing first-use (himo3)
        F(0x0401, "UNK_0401", TutorialHint, desc: "Forsaken Fortress: barrel carry hint"), // Barrel carry hint (obj_barrel / tag_hint)
        // ---- byte 0x05 ----
        F(0x0580, "UNK_0580", TutorialHint, desc: "Grappling hook first-use hint (3)"), // Grappling hook first-use (himo2)
        F(0x0540, "UNK_0540", TutorialHint, desc: "Grappling hook first-use hint (2)"), // Grappling hook first-use (himo2)
        F(0x0520, "UNK_0520", Story, desc: "Outset gossip stone cutscene (Outset layer)"), // Outset layer 4 / Asoko layer 2 (d_com_inf_game); set by stage data
        F(0x0510, "UNK_0510", Unknown),
        F(0x0508, "UNK_0508", Unknown),
        F(0x0504, "UNK_0504", Unknown),
        F(0x0502, "UNK_0502", Unknown, desc: "Message tag state (Outset)"), // read by tag_msg only
        F(0x0501, "UNK_0501", TutorialHint, desc: "Orca spin-attack lesson hint"), // Orca spin-attack lesson (ji1 kaitenAction)
        // ---- byte 0x06 ----
        F(0x0640, "UNK_0640", NpcState, desc: "Orca dialogue (2)"), // Orca dialogue (ji1)
        F(0x0608, "UNK_0608", NpcState, desc: "Grandma dialogue (unused)"), // Grandma unused dialogue
        F(0x0604, "UNK_0604", Unknown),
        F(0x0602, "UNK_0602", NpcState, desc: "Grandma dialogue after getting the sword"), // Grandma dialogue after sword obtained
        F(0x0601, "UNK_0601", NpcState, desc: "Grandma dialogue after the Helmaroc cutscene"), // Grandma dialogue after Helmaroc demo
        // ---- byte 0x07 ----
        F(0x0780, "UNK_0780", Unknown, desc: "Grandma dialogue state"), // read by Grandma (ba1) only
        F(0x0740, "UNK_0740", NpcState, desc: "Grandma dialogue after Aryll is kidnapped"), // Grandma dialogue after Aryll kidnapped
        F(0x0720, "UNK_0720", Unknown, desc: "Niko's rope game intro"),
        F(0x0710, "UNK_0710", Unknown, desc: "Rope game 1 finished"),
        F(0x0704, "UNK_0704", Unknown),
        F(0x0702, "UNK_0702", Unknown),
        F(0x0701, "UNK_0701", Unknown),
        // ---- byte 0x08 ----
        F(0x0880, "UNK_0880", NpcState, desc: "Pirate dialogue (3)"), // Pirates (p1) talk
        F(0x0840, "UNK_0840", NpcState, desc: "Pirate dialogue (2)"), // Pirates (p1) talk
        F(0x0820, "UNK_0820", NpcState, desc: "Pirate dialogue"), // Pirates (p1) talk
        F(0x0810, "UNK_0810", NpcState, desc: "Tetra dialogue"), // Tetra talk
        F(0x0808, "UNK_0808", Story, risky: true, desc: "Forsaken Fortress arrival (pirate ship step)"), // FF1/pirate-ship step; LOAD SPAWN: dComIfGs_setGameStartStage -> MajyuE start 18
        F(0x0804, "UNK_0804", Story, desc: "Tetra: climb the mast"), // Tetra event (zl1 event_proc)
        F(0x0802, "UNK_0802", Story, desc: "Tetra event on the pirate ship"), // Tetra event (zl1 event_proc)
        F(0x0801, "UNK_0801", Story, risky: true, desc: "Barrel launch to the Forsaken Fortress"), // Tetra event; LOAD SPAWN: setGameStartStage -> MajyuE (FF1)
        // ---- byte 0x09 ----
        F(0x0940, "UNK_0940", Unknown),
        F(0x0920, "UNK_0920", Unknown),
        F(0x0910, "UNK_0910", NpcState, desc: "Pirate dialogue (4)"), // Pirates (p1) talk
        F(0x0908, "UNK_0908", Story, desc: "Sea chart unlocked (KoRL's sail talk)"), // Sea chart unlocked: enables map button + chart menu (d_meter, d_menu_collect)
        F(0x0904, "UNK_0904", Unknown),
        F(0x0902, "UNK_0902", Story, desc: "Dragon Roost arrival (Wind Waker cutscene)"), // Landing-event gate for Dragon Roost (d_com_inf_game l_landingEvent)
        F(0x0901, "UNK_0901", NpcState, desc: "Fishman first talk"), // Fishman first disappear (npc_so)
        // ---- byte 0x0A ----
        F(0x0A80, "UNK_0A80", TutorialHint, desc: "KoRL hint: Din's Pearl"), // KoRL hint message seen (ship setNextMessage)
        F(0x0A40, "UNK_0A40", NpcState, desc: "Windfall resident dialogue"), // Windfall resident talk (npc_people)
        F(0x0A20, "UNK_0A20", Story, desc: "Forest Haven arrival cutscene"), // Landing-event gate for Forest Haven
        F(0x0A10, "UNK_0A10", TutorialHint, desc: "KoRL: first talk"), // KoRL hint message seen
        F(0x0A08, "UNK_0A08", TutorialHint, desc: "KoRL hint: Farore's Pearl"), // KoRL hint message seen
        F(0x0A04, "UNK_0A04", TutorialHint, desc: "KoRL hint: after Farore's Pearl"), // KoRL hint message seen
        F(0x0A02, "ENDLESS_NIGHT", LocalOnly, desc: "Endless night (clock frozen until Nayru's Pearl)"), // ENDLESS_NIGHT: freezes clock at night until Nayru's Pearl (d_kankyo dKy_checkEventNightStop); time-of-day is per player
        F(0x0A01, "UNK_0A01", TutorialHint, desc: "KoRL hint: endless night"), // KoRL hint message seen
        // ---- byte 0x0B ----
        F(0x0B80, "UNK_0B80", SideQuest, desc: "Tingle rescued from the jail"), // Tingle rescued from jail (npc_tc statusDemoRescue); gates Tingle letter
        F(0x0B40, "UNK_0B40", NpcState, desc: "Tingle: talked in the jail"), // Tingle jail state (npc_tc)
        F(0x0B20, "UNK_0B20", Collectible, desc: "Hurricane Spin learned from Orca"), // Orca taught Hurricane Spin; unlocks move in d_a_player_sword (ability, not in inventory)
        F(0x0B08, "UNK_0B08", NpcState, desc: "Tott dialogue"), // Tott talk
        F(0x0B04, "UNK_0B04", Unknown),
        F(0x0B02, "UNK_0B02", Unknown),
        F(0x0B01, "UNK_0B01", Unknown),
        // ---- byte 0x0C ----
        F(0x0C80, "UNK_0C80", Unknown),
        F(0x0C40, "UNK_0C40", CutsceneSeen, desc: "Song of Passing cutscene (Tott)"), // Tott Song-of-Passing demo (tt demoProcTact1)
        F(0x0C20, "UNK_0C20", Unknown),
        F(0x0C10, "UNK_0C10", Unknown),
        F(0x0C08, "UNK_0C08", Unknown),
        F(0x0C04, "UNK_0C04", Unknown),
        F(0x0C02, "UNK_0C02", Unknown),
        // ---- byte 0x0D ----
        F(0x0D80, "UNK_0D80", NpcState, desc: "Orca: Knight's Crests handed in"), // Orca dialogue (ji1); read by kmon
        F(0x0D40, "UNK_0D40", Unknown),
        F(0x0D20, "UNK_0D20", Unknown),
        F(0x0D10, "UNK_0D10", Unknown),
        F(0x0D08, "UNK_0D08", Unknown),
        F(0x0D04, "UNK_0D04", Unknown, desc: "Shop state (2)"), // read by d_shop only
        F(0x0D02, "UNK_0D02", Unknown, desc: "Shop state"), // read by d_shop only
        // ---- byte 0x0E ----
        F(0x0E80, "UNK_0E80", Unknown),
        F(0x0E40, "UNK_0E40", Unknown),
        F(0x0E20, "UNK_0E20", Story, desc: "Aryll kidnapped cutscene"), // Prologue: Aryll kidnapped (tag_event); Outset layer 2
        F(0x0E10, "UNK_0E10", Unknown),
        F(0x0E08, "UNK_0E08", NpcState, desc: "Zunari: Mila's bottle"), // Zunari (rsh1)
        F(0x0E04, "UNK_0E04", Unknown),
        F(0x0E02, "MEDLI_GAVE_FATHERS_LETTER", SideQuest, desc: "Medli gave Komali's father's letter"), // Medli gave Komali's father's letter
        F(0x0E01, "UNK_0E01", Story, desc: "Tower of the Gods light bridge appeared"), // TotG light bridge appeared (lbridge)
        // ---- byte 0x0F ----
        F(0x0F80, "MET_KORL", Story, risky: true, desc: "Met KoRL (save now loads at Windfall)"), // MET_KORL: LOAD SPAWN -> Windfall; A_mori layer; enables KoRL/ship (setGameStartStage)
        F(0x0F40, "UNK_0F40", Story, desc: "Tower of the Gods light bridge vanish cutscene"), // TotG light bridge disappear demo (lbridge)
        F(0x0F20, "UNK_0F20", SideQuest, desc: "Orca: 1000 hits"), // Orca clear record
        F(0x0F10, "UNK_0F10", Collectible, desc: "Orca: Piece of Heart given"), // Orca reward item given (ji1 createItem)
        F(0x0F08, "UNK_0F08", Unknown),
        F(0x0F04, "UNK_0F04", Unknown),
        F(0x0F02, "UNK_0F02", Unknown),
        F(0x0F01, "UNK_0F01", Collectible, desc: "Auction: Joy Pendant bought"), // Auction: Joy Pendant bought (auction l_item_dat)
        // ---- byte 0x10 ----
        F(0x1080, "UNK_1080", Collectible, desc: "Auction: Treasure Chart 27 bought"), // Auction: Treasure Chart 27 bought
        F(0x1040, "UNK_1040", Collectible, desc: "Auction: Treasure Chart 18 bought"), // Auction: Treasure Chart 18 bought
        F(0x1020, "UNK_1020", Collectible, desc: "Auction: Piece of Heart bought"), // Auction: Piece of Heart bought
        F(0x1010, "UNK_1010", CutsceneSeen, desc: "Tower of the Gods: Beamos lasers off"), // Beamos event demo (obj_bemos event_move)
        F(0x1008, "UNK_1008", Collectible, desc: "Auction: Postman statue bought"), // Auction: Postman statue bought
        F(0x1004, "UNK_1004", Collectible, desc: "Auction: Shop Guru statue bought"), // Auction: Shop Guru statue bought
        F(0x1002, "UNK_1002", Unknown),
        F(0x1001, "UNK_1001", CutsceneSeen, desc: "Fire and Ice Arrows fairy cutscene"), // fairy.stb (sea)
        // ---- byte 0x11 ----
        F(0x1140, "UNK_1140", LocalOnly, desc: "Dragon Roost Cavern door state (miniboss)"), // Cleared by d_a_mdoor.cpp CreateInit when 1101 unset (transient door-demo state)
        F(0x1120, "UNK_1120", Unknown),
        F(0x1110, "UNK_1110", NpcState, desc: "Zunari dialogue"), // Zunari
        F(0x1108, "UNK_1108", NpcState, desc: "Zunari: Town Flower given"), // Zunari; read by traveling merchants
        F(0x1104, "UNK_1104", NpcState, desc: "Medli dialogue (2)"), // Medli dialogue
        F(0x1102, "UNK_1102", NpcState, desc: "Medli dialogue"), // Medli dialogue; read by Rito (bm1)
        F(0x1101, "UNK_1101", Story, desc: "Dragon Roost: grappling hook obtained (Medli step)"), // Medli/DRC progress; read by mdoor, Kargaroc, Moblin, tag_event
        // ---- byte 0x12 ----
        F(0x1280, "UNK_1280", NpcState, desc: "Medli dialogue (3)"), // Medli dialogue
        F(0x1240, "UNK_1240", Unknown, desc: "Tingle dialogue state"), // read by Tingle (tc_msg_red)
        F(0x1220, "UNK_1220", SideQuest, desc: "Rito postbox letter delivered"), // Rito postbox letter delivered (toripost)
        F(0x1210, "UNK_1210", NpcState, desc: "Ivan dialogue"), // Ivan talk
        F(0x1208, "UNK_1208", NpcState, desc: "Lenzo: first talk"), // Lenzo first talk (npc_photo l_save_dat.0); read by Ivan
        F(0x1204, "UNK_1204", Unknown),
        F(0x1202, "UNK_1202", Unknown),
        F(0x1201, "UNK_1201", Unknown),
        // ---- byte 0x13 ----
        F(0x1380, "UNK_1380", NpcState, desc: "Mrs. Marie: hide and seek started"), // Mrs. Marie
        F(0x1340, "UNK_1340", NpcState, desc: "Killer Bees hide and seek won"), // Ivan demo
        F(0x1320, "UNK_1320", NpcState, desc: "Traveling merchant 1: first talk"), // Traveling merchant 1 first talk (roten l_save_dat)
        F(0x1310, "UNK_1310", NpcState, desc: "Traveling merchant 2: first talk"), // Traveling merchant 2 first talk
        F(0x1308, "UNK_1308", NpcState, desc: "Traveling merchant 3: first talk"), // Traveling merchant 3 first talk
        F(0x1304, "UNK_1304", LocalOnly, desc: "Traveling merchant 1 traded today (daily)"), // DAILY reset: merchant 1 traded today (d_kankyo_dayproc.inc:35)
        F(0x1302, "UNK_1302", LocalOnly, desc: "Traveling merchant 2 traded today (daily)"), // DAILY reset: merchant 2 traded today (dayproc:36)
        F(0x1301, "UNK_1301", LocalOnly, desc: "Traveling merchant 3 traded today (daily)"), // DAILY reset: merchant 3 traded today (dayproc:37)
        // ---- byte 0x14 ----
        F(0x1480, "PLACED_DINS_PEARL", Story, desc: "Din's Pearl placed"), // Din's Pearl placed (obj_doguu)
        F(0x1440, "PLACED_FARORES_PEARL", Story, desc: "Farore's Pearl placed"), // Farore's Pearl placed
        F(0x1420, "UNK_1420", Unknown, desc: "20 Skull Necklaces handed in"),
        F(0x1410, "PLACED_NAYRUS_PEARL", Story, desc: "Nayru's Pearl placed"), // Nayru's Pearl placed
        F(0x1408, "UNK_1408", Unknown),
        F(0x1404, "UNK_1404", Unknown),
        F(0x1402, "UNK_1402", NpcState, desc: "Medli dialogue (4)"), // Medli dialogue
        F(0x1401, "UNK_1401", NpcState, desc: "Rito dialogue"), // Rito (bm1) dialogue
        // ---- byte 0x15 ----
        F(0x1580, "UNK_1580", Story, desc: "Dragon Roost state after the Rito event"), // Dragon Roost room load after 3F02 (d_s_room phase_4)
        F(0x1540, "UNK_1540", CutsceneSeen, desc: "Wind Temple fan elevator cutscene (4)"), // Wind Temple fan-elevator demo seen (hbrf1)
        F(0x1520, "UNK_1520", CutsceneSeen, desc: "Wind Temple fan elevator cutscene (3)"), // Wind Temple fan-elevator demo seen (hbrf1)
        F(0x1510, "UNK_1510", CutsceneSeen, desc: "Wind Temple fan elevator cutscene (2)"), // Wind Temple fan-elevator demo seen (hbrf1)
        F(0x1508, "UNK_1508", CutsceneSeen, desc: "Wind Temple fan elevator cutscene"), // Wind Temple fan-elevator demo seen (hbrf1)
        F(0x1504, "UNK_1504", NpcState, desc: "Medli dialogue (5)"), // Medli dialogue
        F(0x1502, "UNK_1502", Unknown),
        F(0x1501, "UNK_1501", Unknown),
        // ---- byte 0x16 ----
        F(0x1680, "UNK_1680", Unknown),
        F(0x1640, "UNK_1640", Unknown),
        F(0x1620, "UNK_1620", Story, desc: "Medli joined as companion"), // Medli joins as companion (npc_md create/initial events); read by ship, player_main, Rito
        F(0x1610, "UNK_1610", Story, desc: "Makar joined as companion"), // Makar joins as companion (npc_cb1 evInitEnd); read by ship, mmusic
        F(0x1608, "UNK_1608", Story, desc: "Medli companion event done"), // Medli companion event done; read by tag_island
        F(0x1604, "UNK_1604", Story, desc: "Makar companion event done"), // Makar companion event done; read by tag_island
        F(0x1602, "UNK_1602", NpcState, desc: "Ivan visit dialogue"), // Ivan visit talk
        F(0x1601, "UNK_1601", SideQuest, desc: "Lenzo photo quest step"), // Lenzo photo quest step (tag_photo / npc_photo l_save_dat.4)
        // ---- byte 0x17 ----
        F(0x1780, "UNK_1780", Story, desc: "Tower of the Gods companion statue woken (3)"), // TotG companion statue woken (npc_os)
        F(0x1740, "UNK_1740", Story, desc: "Tower of the Gods companion statue woken (2)"), // TotG companion statue woken
        F(0x1720, "UNK_1720", Story, desc: "Tower of the Gods companion statue woken"), // TotG companion statue woken
        F(0x1710, "UNK_1710", Story, desc: "Tower of the Gods statue puzzle finished (2)"), // TotG statue puzzle finished; read by d_door, pedestal
        F(0x1708, "UNK_1708", SideQuest, desc: "Tingle Tuner flags received"), // Tingle Tuner flags received (d_a_agb)
        F(0x1704, "UNK_1704", Story, desc: "Tower of the Gods statue puzzle finished"), // TotG statue puzzle finished; read by d_door, pedestal
        F(0x1701, "UNK_1701", SideQuest, desc: "Lenzo photo quest step (2)"), // Lenzo photo quest step (npc_photo l_save_dat.2); read by knob00
        // ---- byte 0x18 ----
        F(0x1880, "UNK_1880", NpcState, desc: "Makar dialogue (2)"), // Makar dialogue
        F(0x1840, "UNK_1840", NpcState, desc: "Makar dialogue"), // Makar dialogue
        F(0x1820, "UNK_1820", Story, desc: "After Zelda awakened (Forsaken Fortress layer)"), // Post-Zelda-awakened (warpdm20 CreateInit); FF layer 3; dayproc auto-stocks Aryll/Tingle letters
        F(0x1810, "UNK_1810", Unknown),
        F(0x1808, "UNK_1808", Unknown, desc: "Rito and Windfall dialogue state"), // read by Rito, Windfall people, d_npc
        F(0x1804, "UNK_1804", Unknown),
        F(0x1802, "UNK_1802", Unknown),
        F(0x1801, "UNK_1801", Unknown, desc: "Deku Tree cutscene"), // read by Deku Leaf item, Baba Bud, tag_hint
        // ---- byte 0x19 ----
        F(0x1980, "UNK_1980", TutorialHint, desc: "Sailed KoRL after the Din's Pearl hint"), // Set in ship procPaddleMove_init when 0A80 is set
        F(0x1940, "UNK_1940", TutorialHint, desc: "First whirlpool hint"), // First whirlpool (ship procWhirlDown)
        F(0x1920, "UNK_1920", Unknown),
        F(0x1910, "UNK_1910", SideQuest, desc: "Windfall door password solved"), // Door password solved (knob00 actionPassward2)
        F(0x1908, "UNK_1908", NpcState, desc: "Rito dialogue (2)"), // Rito (bm1) dialogue
        F(0x1904, "UNK_1904", NpcState, desc: "Makar dialogue (3)"), // Makar dialogue
        F(0x1902, "UNK_1902", SideQuest, desc: "Volcano island timer cleared (2)"), // Volcano isle timer cleared, type 1 (tag_volcano)
        F(0x1901, "UNK_1901", SideQuest, desc: "Volcano island timer cleared"), // Volcano isle timer cleared, type 0 (tag_volcano)
        // ---- byte 0x1A ----
        F(0x1A80, "UNK_1A80", Story, desc: "Left the room holding Din's Pearl"), // Room left while holding Din's Pearl (d_s_room delete); Rito/door state
        F(0x1A20, "UNK_1A20", SideQuest, desc: "Tingle Tuner activated"), // Tingle Tuner upload message (agb)
        F(0x1A10, "UNLOCK_TINGLE_BALLOON_DISCOUNT", SideQuest, desc: "Tingle Tuner: balloon discount"), // Tingle Tuner balloon discount unlocked
        F(0x1A08, "UNLOCK_TING_DISCOUNT", SideQuest, desc: "Tingle Tuner: Ting discount"), // Tingle Tuner Ting discount unlocked
        F(0x1A04, "UNK_1A04", Unknown),
        F(0x1A02, "UNK_1A02", SideQuest, desc: "Koboli mail-sorting minigame (2)"), // Koboli mail-sorting minigame (bmsw)
        F(0x1A01, "UNK_1A01", SideQuest, desc: "Koboli mail-sorting minigame"), // Koboli mail-sorting minigame (bmsw)
        // ---- byte 0x1B ----
        F(0x1B80, "UNK_1B80", Unknown),
        F(0x1B40, "UNK_1B40", NpcState, desc: "Windfall resident dialogue (5)"), // Windfall resident talk
        F(0x1B20, "UNK_1B20", NpcState, desc: "Windfall resident dialogue (4)"), // Windfall resident talk
        F(0x1B10, "UNK_1B10", NpcState, desc: "Windfall resident dialogue (3)"), // Windfall resident talk
        F(0x1B04, "UNK_1B04", NpcState, desc: "Windfall resident dialogue (2)"), // Windfall resident talk
        F(0x1B01, "UNK_1B01", Story, desc: "Tower of the Gods statue puzzle finished (3)"), // TotG statue puzzle finished (npc_os setFinish)
        // ---- byte 0x1C ----
        F(0x1C80, "UNK_1C80", Unknown),
        F(0x1C40, "UNK_1C40", Story, desc: "Left Forest Haven holding Farore's Pearl"), // Forest Haven room left holding Farore's Pearl (d_s_room)
        F(0x1C20, "UNK_1C20", Unknown, desc: "Deku Tree: talked after Farore's Pearl"),
        F(0x1C08, "UNK_1C08", SideQuest, desc: "Mrs. Marie: Joy Pendant reward (Cabana)"), // Mrs. Marie Joy Pendant reward; read by people
        F(0x1C04, "UNK_1C04", Collectible, desc: "Mrs. Marie: 40 Joy Pendants reward"), // Mrs. Marie 40-pendant reward given (npc_ho, gated on reg C0FF)
        F(0x1C02, "UNK_1C02", Unknown, desc: "Windfall lighthouse lit"), // read by Windfall people + lighthouse light angle (d_com_static)
        F(0x1C01, "UNK_1C01", Unknown),
        // ---- byte 0x1D ----
        F(0x1D80, "UNK_1D80", Unknown),
        F(0x1D40, "UNK_1D40", Unknown),
        F(0x1D20, "UNK_1D20", NpcState, desc: "Windfall resident dialogue (7)"), // Windfall resident talk
        F(0x1D10, "UNK_1D10", NpcState, desc: "Windfall resident dialogue (6)"), // Windfall resident talk
        F(0x1D08, "UNK_1D08", Unknown, desc: "Tingle dialogue state (5)"), // read by Tingle (tc_msg_red)
        F(0x1D04, "UNK_1D04", Unknown, desc: "Tingle dialogue state (4)"), // read by Tingle (tc_msg_red)
        F(0x1D02, "UNK_1D02", Unknown, desc: "Tingle dialogue state (3)"), // read by Tingle (tc_msg_red)
        F(0x1D01, "UNK_1D01", Unknown, desc: "Tingle dialogue state (2)"), // read by Tingle (tc_msg_red)
        // ---- byte 0x1E ----
        F(0x1E80, "UNK_1E80", Story, desc: "Endless night: Rito quill cutscene"), // Rito event (bm1 event_proc); read by Tingle Tuner, sea chart
        F(0x1E40, "UNK_1E40", Story, desc: "Tower of the Gods raised"), // All 3 pearls placed -> Tower of the Gods raised (obj_doguu); read by tower, ship, lod_bg
        F(0x1E20, "UNK_1E20", NpcState, desc: "Windfall resident dialogue (10)"), // Windfall resident talk
        F(0x1E10, "UNK_1E10", NpcState, desc: "Windfall resident dialogue (9)"), // Windfall resident talk
        F(0x1E08, "UNK_1E08", NpcState, desc: "Windfall resident dialogue (8)"), // Windfall resident talk
        F(0x1E04, "UNK_1E04", NpcState, desc: "Ivan visit"), // Ivan visit; read by tag_mk
        F(0x1E02, "UNK_1E02", SideQuest, desc: "Killer Bees hide and seek"), // Killer Bees hide-and-seek (tag_mk)
        F(0x1E01, "UNK_1E01", NpcState, desc: "Mrs. Marie: first talk"), // Mrs. Marie
        // ---- byte 0x1F ----
        F(0x1F80, "UNK_1F80", NpcState, desc: "Mrs. Marie dialogue"), // Mrs. Marie
        F(0x1F40, "UNK_1F40", NpcState, desc: "Rito event"), // Rito (bm1) event
        F(0x1F20, "UNK_1F20", NpcState, desc: "Beedle dialogue"), // Beedle
        F(0x1F10, "UNK_1F10", SideQuest, desc: "Beedle: membership started"), // Beedle membership start; dayproc advances local 7-day counter BB07
        F(0x1F08, "UNK_1F08", SideQuest, desc: "Beedle: 7-day membership reached"), // Beedle: set by dayproc when BB07 reaches 7 (monotonic)
        F(0x1F04, "UNK_1F04", Unknown, desc: "KoRL message selection"), // read by ship only: KoRL message selection (setter is data)
        F(0x1F02, "UNK_1F02", TutorialHint, desc: "KoRL hint message (2)"), // KoRL hint message seen
        F(0x1F01, "UNK_1F01", TutorialHint, desc: "KoRL hint message"), // KoRL hint message seen
        // ---- byte 0x20 ----
        F(0x2080, "UNK_2080", LocalOnly, desc: "Moonlight salvage point 0 (weekly)"), // WEEKLY reset: moonlight salvage point 0 (daSalvage m_savelabel; dayproc.inc:84)
        F(0x2040, "UNK_2040", NpcState, desc: "Beedle dialogue (2)"), // Beedle
        F(0x2020, "UNK_2020", NpcState, desc: "Beedle: bottle bought"), // Beedle
        F(0x2010, "UNK_2010", NpcState, desc: "Beedle: Piece of Heart bought"), // Beedle
        F(0x2008, "UNK_2008", NpcState, desc: "Beedle: Treasure Chart bought"), // Beedle
        F(0x2004, "UNK_2004", LocalOnly, desc: "Moonlight salvage point 1 (weekly)"), // WEEKLY reset: salvage point 1 (dayproc:85)
        F(0x2002, "UNK_2002", LocalOnly, desc: "Moonlight salvage point 2 (weekly)"), // WEEKLY reset: salvage point 2 (dayproc:86)
        F(0x2001, "UNK_2001", NpcState, desc: "Rito dialogue (3)"), // Rito (bm1) dialogue
        // ---- byte 0x21 ----
        F(0x2180, "UNK_2180", NpcState, desc: "Rito: 20 Golden Feathers"), // Rito (bm1) event
        F(0x2140, "UNK_2140", NpcState, desc: "Rito dialogue (5)"), // Rito (bm1) dialogue
        F(0x2120, "UNK_2120", NpcState, desc: "Rito dialogue (4)"), // Rito (bm1) dialogue
        F(0x2110, "UNK_2110", Story, desc: "Bomb shop cutscene (endless night ends)"), // bombshop.stb: ends Windfall endless-night sequence (tag_event demoEndProc)
        F(0x2108, "UNK_2108", NpcState, desc: "Beedle dialogue (3)"), // Beedle
        F(0x2104, "UNK_2104", SideQuest, desc: "Windfall windmill running"), // Windfall ferris wheel (obj_ferris); read by people
        F(0x2102, "UNK_2102", NpcState, desc: "Windfall resident dialogue (12)"), // Windfall resident talk
        F(0x2101, "UNK_2101", NpcState, desc: "Windfall resident dialogue (11)"), // Windfall resident talk
        // ---- byte 0x22 ----
        F(0x2280, "UNK_2280", NpcState, desc: "Windfall resident dialogue (18)"), // Windfall resident talk
        F(0x2240, "UNK_2240", NpcState, desc: "Windfall resident dialogue (17)"), // Windfall resident talk
        F(0x2220, "UNK_2220", NpcState, desc: "Windfall resident dialogue (16)"), // Windfall resident talk
        F(0x2210, "UNK_2210", NpcState, desc: "Windfall resident dialogue (15)"), // Windfall resident talk
        F(0x2208, "UNK_2208", NpcState, desc: "Windfall resident dialogue (14)"), // Windfall resident talk
        F(0x2204, "UNK_2204", NpcState, desc: "Windfall resident dialogue (13)"), // Windfall resident talk
        F(0x2202, "UNK_2202", NpcState, desc: "Rito dialogue (6)"), // Rito (bm1) dialogue
        F(0x2201, "UNK_2201", NpcState, desc: "Ivan dialogue (2)"), // Ivan talk; read by Mrs. Marie
        // ---- byte 0x23 ----
        F(0x2340, "UNK_2340", NpcState, desc: "Windfall resident dialogue (25)"), // Windfall resident talk
        F(0x2320, "UNK_2320", NpcState, desc: "Windfall resident dialogue (24)"), // Windfall resident talk
        F(0x2310, "UNK_2310", NpcState, desc: "Windfall resident dialogue (23)"), // Windfall resident talk
        F(0x2308, "UNK_2308", NpcState, desc: "Windfall resident dialogue (22)"), // Windfall resident (executeWait)
        F(0x2304, "UNK_2304", NpcState, desc: "Windfall resident dialogue (21)"), // Windfall resident talk
        F(0x2302, "UNK_2302", NpcState, desc: "Windfall resident dialogue (20)"), // Windfall resident talk
        F(0x2301, "UNK_2301", NpcState, desc: "Windfall resident dialogue (19)"), // Windfall resident talk
        // ---- byte 0x24 ----
        F(0x2480, "UNK_2480", NpcState, desc: "Windfall resident dialogue (27)"), // Windfall resident talk
        F(0x2440, "UNK_2440", NpcState, desc: "Windfall resident dialogue (26)"), // Windfall resident talk
        F(0x2420, "UNK_2420", NpcState, desc: "Zunari dialogue (2)"), // Zunari
        F(0x2410, "UNK_2410", SideQuest, desc: "Windfall resident rupee reward (4)"), // Windfall NPC rupee reward given (people l_item_chk_sa3)
        F(0x2408, "UNK_2408", SideQuest, desc: "Windfall resident rupee reward (3)"), // Windfall NPC rupee reward given
        F(0x2404, "UNK_2404", SideQuest, desc: "Windfall resident rupee reward (2)"), // Windfall NPC rupee reward given
        F(0x2402, "UNK_2402", SideQuest, desc: "Windfall resident rupee reward"), // Windfall NPC rupee reward given
        F(0x2401, "UNK_2401", Story, risky: true, desc: "Left Outset (departure cutscene)"), // departure_DEMO.stb; LOAD SPAWN: setGameStartStage -> A_umikz 204
        // ---- byte 0x25 ----
        F(0x2580, "UNK_2580", CutsceneSeen, desc: "Forsaken Fortress: find Aryll cutscene"), // FIND_SISTER.stb (Mjtower)
        F(0x2540, "UNK_2540", Unknown),
        F(0x2520, "UNK_2520", Unknown),
        F(0x2510, "UNK_2510", Story, desc: "Command Melody learned (Tower of the Gods statue)"), // TotG statue step (npc_os); read by hsehi1
        F(0x2508, "UNK_2508", Unknown),
        F(0x2504, "UNK_2504", NpcState, desc: "Windfall resident dialogue (29)"), // Windfall resident talk
        F(0x2502, "UNK_2502", CutsceneSeen, desc: "Delivery Bag cutscene"), // tag_event demo end (type 8)
        F(0x2501, "UNK_2501", NpcState, desc: "Windfall resident dialogue (28)"), // Windfall resident talk
        // ---- byte 0x26 ----
        F(0x2680, "UNK_2680", LocalOnly, desc: "Windfall resident state (daily)"), // DAILY reset (dayproc:73) and also cleared by npc_people getMsg (d_a_npc_people.cpp:7266)
        F(0x2640, "UNK_2640", NpcState, desc: "Windfall resident dialogue (31)"), // Windfall resident talk
        F(0x2620, "UNK_2620", NpcState, desc: "Windfall resident dialogue (30)"), // Windfall resident talk
        F(0x2608, "UNK_2608", Story, desc: "Tower of the Gods statue event (2)"), // TotG statue event (npc_os)
        F(0x2604, "UNK_2604", Story, desc: "Tower of the Gods statue event"), // TotG statue event (npc_os)
        F(0x2602, "UNK_2602", TutorialHint, desc: "First door hint (2)"), // First-door message (d_door onFirst)
        F(0x2601, "UNK_2601", TutorialHint, desc: "First door hint"), // First-door message (d_door onFirst)
        // ---- byte 0x27 ----
        F(0x2780, "UNK_2780", Unknown),
        F(0x2740, "UNK_2740", CutsceneSeen, desc: "Special event cutscene"), // tag_event special event seen
        F(0x2710, "UNK_2710", Story, desc: "Cyclos defeated"), // Cyclos demo (npc_hr); read by Ballad-of-Gales tornado
        F(0x2708, "UNK_2708", CutsceneSeen, desc: "Ballad of Gales cutscene (Cyclos)"), // Cyclos teaches Ballad of Gales demo
        F(0x2704, "UNK_2704", NpcState, desc: "Baito dialogue (2)"), // Baito
        F(0x2702, "UNK_2702", NpcState, desc: "Baito dialogue"), // Baito
        F(0x2701, "UNK_2701", NpcState, desc: "Baito cutscene"), // Baito demo; read by Koboli
        // ---- byte 0x28 ----
        F(0x2880, "UNK_2880", NpcState, desc: "Windfall resident dialogue (32)"), // Windfall resident talk
        F(0x2840, "UNK_2840", Unknown),
        F(0x2820, "UNK_2820", Unknown),
        F(0x2810, "UNK_2810", Unknown),
        F(0x2808, "UNK_2808", Unknown),
        F(0x2804, "UNK_2804", LocalOnly, desc: "Moonlight salvage point 3 (weekly)"), // WEEKLY reset: salvage point 3 (dayproc:87)
        F(0x2802, "UNK_2802", LocalOnly, desc: "Moonlight salvage point 4 (weekly)"), // WEEKLY reset: salvage point 4 (dayproc:88)
        F(0x2801, "UNK_2801", LocalOnly, desc: "Moonlight salvage point 5 (weekly)"), // WEEKLY reset: salvage point 5 (dayproc:89)
        // ---- byte 0x29 ----
        F(0x2980, "UNK_2980", LocalOnly, desc: "Moonlight salvage point 6 (weekly)"), // WEEKLY reset: salvage point 6 (dayproc:90)
        F(0x2940, "UNK_2940", LocalOnly, desc: "Moonlight salvage point 7 (weekly)"), // WEEKLY reset: salvage point 7 (dayproc:91)
        F(0x2920, "UNK_2920", Story, desc: "Earth God's Lyric stone (unlocks Medli)"), // Earth God's Lyric learned at stone (obj_mknjd); gates Medli
        F(0x2910, "UNK_2910", Story, desc: "Wind God's Aria stone (unlocks Makar)"), // Wind God's Aria learned at stone (obj_mknjd); gates Makar
        F(0x2908, "UNK_2908", NpcState, desc: "Tetra dialogue (2)"), // Tetra talk
        F(0x2904, "UNK_2904", Unknown),
        F(0x2902, "UNK_2902", Unknown),
        F(0x2901, "UNK_2901", Unknown),
        // ---- byte 0x2A ----
        F(0x2A80, "UNK_2A80", CutsceneSeen, desc: "Hero's clothes (prologue)"), // Prologue demo (demo00 l_eventBit[1]); read by Grandma, Aryll, player_main
        F(0x2A40, "UNK_2A40", Unknown),
        F(0x2A20, "GRANDMA_HEALED", SideQuest, desc: "Grandma healed (soup quest)"), // Grandma healed (soup quest); enables daily soup counter A60F
        F(0x2A10, "UNK_2A10", TutorialHint, desc: "Dragon Roost Cavern flame lift hint"), // DRC flame-lift hint (mflft); read by tag_hint
        F(0x2A08, "RODE_KORL", LocalOnly, desc: "Entered KoRL for the first time (ship spawn and save start)"), // RODE_KORL: switches ship spawn logic (d_stage.cpp:1933, player_main:10869) and load spawn to last position (setGameStartStage)
        F(0x2A04, "UNK_2A04", NpcState, desc: "Windfall resident dialogue (pig game)"), // Windfall resident talk
        F(0x2A02, "UNK_2A02", TutorialHint, desc: "Sailed KoRL after the Farore's Pearl hint"), // Set in ship procPaddleMove_init when 0A08 is set
        F(0x2A01, "UNK_2A01", TutorialHint, desc: "Sailed KoRL during the endless night"), // Set in ship procPaddleMove_init when ENDLESS_NIGHT is set
        // ---- byte 0x2B ----
        F(0x2B80, "UNK_2B80", TutorialHint, desc: "KoRL hint: Forest Haven"), // KoRL hint message seen
        F(0x2B40, "UNK_2B40", Unknown, desc: "Flight Control Platform Piece of Heart"),
        F(0x2B20, "UNK_2B20", Unknown),
        F(0x2B10, "UNK_2B10", CutsceneSeen, desc: "Command Melody monument revealed"), // TotG Command Melody monument revealed (hsehi1)
        F(0x2B08, "UNK_2B08", NpcState, desc: "Windfall resident dialogue (34)"), // Windfall resident talk
        F(0x2B04, "UNK_2B04", NpcState, desc: "Windfall resident dialogue (33)"), // Windfall resident talk
        // ---- byte 0x2C ----
        F(0x2C80, "UNK_2C80", Unknown),
        F(0x2C40, "UNK_2C40", Unknown),
        F(0x2C20, "UNK_2C20", Unknown),
        F(0x2C08, "UNK_2C08", NpcState, desc: "Medli dialogue (6)"), // Medli (XyCheckCB)
        F(0x2C04, "UNK_2C04", Unknown),
        F(0x2C02, "BARRIER_BREAK", Story, desc: "Hyrule barrier broken"), // Hyrule barrier broken (obj_barrier)
        F(0x2C01, "UNK_2C01", Story, desc: "Hyrule: Mighty Darknuts defeated"), // Endgame Hyrule state; kenroom/Hyroom layers, statue (d_com_inf_game)
        // ---- byte 0x2D ----
        F(0x2D80, "UNK_2D80", SideQuest, desc: "Killer Bees quest"), // Ivan/Killer Bees (tag_mk); read by knob00, sea chart, d_message
        F(0x2D40, "UNK_2D40", Story, desc: "Medli cutscene at the Earth Temple entrance"), // dance_zola.stb (stage Edaichi, the Earth Temple entrance)
        F(0x2D20, "UNK_2D20", Story, desc: "Makar cutscene at the Wind Temple entrance"), // dance_kokiri.stb (stage Ekaze, the Wind Temple entrance)
        F(0x2D10, "UNK_2D10", Story, risky: true, desc: "First descent into Hyrule (KoRL needs the Master Sword)"), // warp_in.stb (first Hyrule descent); ship boarding DISABLED until Master Sword equipped (d_a_ship.cpp:4225)
        F(0x2D08, "UNK_2D08", Story, desc: "Hyrule warp opened"), // warphole.stb (Hyrule warp opened); read by ship, warps
        F(0x2D04, "MASTER_SWORD_CUTSCENE", Story, desc: "Master Sword cutscene"), // MASTER_SWORD_CUTSCENE (master_sword.stb)
        F(0x2D02, "ZELDA_AWAKENED", Story, desc: "Zelda awakened"), // ZELDA_AWAKENED (awake_zelda.stb)
        F(0x2D01, "UNK_2D01", Story, desc: "Aryll rescued (Forsaken Fortress 2)"), // rescue.stb: Aryll rescued (FF2); Windfall layer 4, M2tower layer
        // ---- byte 0x2E ----
        F(0x2E80, "UNK_2E80", CutsceneSeen, desc: "Tower of the Gods rising cutscene"), // towerd/f/n.stb (ADMumi)
        F(0x2E40, "UNK_2E40", NpcState, desc: "Medli dialogue (7)"), // Medli dialogue
        F(0x2E20, "UNK_2E20", Unknown),
        F(0x2E10, "UNK_2E10", Unknown),
        F(0x2E08, "UNK_2E08", SideQuest, desc: "Tingle Tuner state"), // Tingle Tuner (agbsw0)
        F(0x2E04, "UNK_2E04", Story, desc: "Headstone Island arrival with Medli"), // Landing-event gate for Headstone Island
        F(0x2E02, "UNK_2E02", Story, desc: "Gale Isle arrival with Makar"), // Landing-event gate for Gale Isle
        F(0x2E01, "UNK_2E01", CutsceneSeen, desc: "Meeting KoRL cutscene"), // MEETSHISHIOH.stb (meeting KoRL)
        // ---- byte 0x2F ----
        F(0x2F80, "UNK_2F80", Unknown),
        F(0x2F40, "UNK_2F40", NpcState, desc: "Orca: training done"), // Orca bow/greeting (ji1 reiAction)
        F(0x2F20, "UNK_2F20", TutorialHint, desc: "KoRL hint message (3)"), // KoRL hint message seen
        F(0x2F10, "UNK_2F10", Story, desc: "Orca sword lesson (Hero's Sword)"), // Orca sword lesson (ji1 teachAction) - prologue
        F(0x2F08, "UNK_2F08", NpcState, desc: "Manny dialogue (2)"), // Manny; re-set on New Game+ (d_save reinit)
        F(0x2F04, "UNK_2F04", NpcState, desc: "Manny dialogue"), // Manny; re-set on New Game+
        F(0x2F02, "UNK_2F02", NpcState, desc: "Carlov: first talk"), // Carlov first talk; re-set on New Game+
        F(0x2F01, "UNK_2F01", LocalOnly, desc: "Carlov: figurine in progress"), // Carlov figurine in progress; cleared by npc_mt (d_a_npc_mt.cpp:399,568,1002)
        // ---- byte 0x30 ----
        F(0x3080, "UNK_3080", LocalOnly, desc: "Carlov: figurine ready"), // Carlov figurine ready; set by dayproc, cleared by npc_mt (d_a_npc_mt.cpp:398,567,961,1013)
        F(0x3040, "UNK_3040", Story, desc: "Forsaken Fortress arrival with KoRL"), // FF landing gate (tag_event type B; l_landingEvent)
        F(0x3020, "UNK_3020", Unknown, desc: "Wallet upgrade (Northern Fairy)"), // read by Lenzo (npc_photo) and Tingle
        F(0x3010, "UNK_3010", Unknown, desc: "Wallet upgrade (Outset fairy)"), // read by Lenzo (npc_photo)
        F(0x3008, "UNK_3008", Unknown, desc: "Bomb bag upgrade (Eastern Fairy)"), // read by Lenzo
        F(0x3004, "UNK_3004", Unknown, desc: "Bomb bag upgrade (Southern Fairy)"), // read by Lenzo
        F(0x3002, "UNK_3002", Unknown, desc: "Quiver upgrade (Western Fairy)"), // read by Lenzo
        F(0x3001, "UNK_3001", Unknown, desc: "Quiver upgrade (Thorned Fairy)"), // read by Lenzo
        // ---- byte 0x31 ----
        F(0x3180, "UNK_3180", Unknown, desc: "Double magic"), // read by Lenzo
        F(0x3140, "UNK_3140", Unknown),
        F(0x3120, "UNK_3120", NpcState, desc: "Manny dialogue (3)"), // Manny
        F(0x3104, "UNK_3104", NpcState, desc: "Baito dialogue (4)"), // Baito
        F(0x3102, "UNK_3102", NpcState, desc: "Baito dialogue (3)"), // Baito (btsw2)
        F(0x3101, "UNK_3101", Unknown),
        // ---- byte 0x32 ----
        F(0x3280, "UNK_3280", Story, desc: "Valoo cutscene after the Forsaken Fortress"), // runaway_majuto.stb; Hyrule/Hyroom/kenroom layers
        F(0x3240, "GOHMA_TRIALS_CLEAR", Story, desc: "Gohma refight cleared"), // Gohma refight cleared (Ganon's Tower, d_a_btd)
        F(0x3220, "KALLE_DEMOS_TRIALS_CLEAR", Story, desc: "Kalle Demos refight cleared"), // Kalle Demos refight cleared (d_a_bmd)
        F(0x3210, "JALHALLA_TRIALS_CLEAR", Story, desc: "Jalhalla refight cleared"), // Jalhalla refight cleared (d_a_bpw)
        F(0x3208, "MOLGERA_TRIALS_CLEAR", Story, desc: "Molgera refight cleared"), // Molgera refight cleared (d_a_bwd)
        F(0x3204, "UNK_3204", Story, desc: "Ganon's Tower trials door destroyed"), // All 4 trials finished (obj_vgnfd on_fin)
        F(0x3202, "UNK_3202", CutsceneSeen, desc: "Grandma: shield missing cutscene"), // tag_event demo end (type A); read by Grandma
        F(0x3201, "UNK_3201", TutorialHint, desc: "KoRL hint message (4)"), // KoRL hint message seen
        // ---- byte 0x33 ----
        F(0x3380, "UNK_3380", TutorialHint, desc: "KoRL hint message (5)"), // KoRL hint message seen; read by tag_mk
        F(0x3340, "UNK_3340", Unknown),
        F(0x3320, "UNK_3320", CutsceneSeen, desc: "Companion cutscene (6)"), // Companion cutscene (tag_md_cb)
        F(0x3310, "UNK_3310", CutsceneSeen, desc: "Companion cutscene (5)"), // Companion cutscene (tag_md_cb / npc_md)
        F(0x3308, "UNK_3308", CutsceneSeen, desc: "Companion cutscene (4)"), // Companion cutscene (tag_md_cb)
        F(0x3304, "UNK_3304", CutsceneSeen, desc: "Companion cutscene (3)"), // Companion cutscene (tag_md_cb)
        F(0x3302, "UNK_3302", CutsceneSeen, desc: "Companion cutscene (2)"), // Companion cutscene (tag_md_cb)
        F(0x3301, "UNK_3301", CutsceneSeen, desc: "Companion cutscene"), // Companion cutscene (tag_md_cb)
        // ---- byte 0x34 ----
        F(0x3480, "UNK_3480", CutsceneSeen, desc: "Companion cutscene (8)"), // Companion cutscene (tag_md_cb)
        F(0x3440, "UNK_3440", CutsceneSeen, desc: "Companion cutscene (7)"), // Companion cutscene (tag_md_cb)
        F(0x3420, "UNK_3420", CutsceneSeen, desc: "Companion cutscene (Makar tree)"), // Companion cutscene (tag_md_cb / Makar tree)
        F(0x3410, "UNK_3410", LocalOnly, desc: "Companion trigger (re-armed every visit)"), // Cleared by d_a_tag_md_cb.cpp:61 on create (arg 11)
        F(0x3408, "UNK_3408", CutsceneSeen, desc: "Makar grabbed by a Floormaster"), // Floormaster grabbed companion demo (fm)
        F(0x3404, "UNK_3404", CutsceneSeen, desc: "Medli grabbed by a Floormaster"), // Floormaster grabbed companion demo (fm)
        F(0x3402, "UNK_3402", TutorialHint, desc: "Pig carry hint"), // Pig carried (kb)
        F(0x3401, "UNK_3401", NpcState, desc: "Carlov dialogue"), // Carlov; re-set on New Game+; read by knob00
        // ---- byte 0x35 ----
        F(0x3580, "UNK_3580", Unknown),
        F(0x3540, "UNK_3540", Unknown),
        F(0x3520, "UNK_3520", Unknown),
        F(0x3510, "UNK_3510", CutsceneSeen, desc: "Opening place-name title shown"), // Opening place-name title shown (d_menu_window); read by audio, file select
        F(0x3508, "LITHOGRAPH_1", Collectible, desc: "Lithograph 1"), // LITHOGRAPH_1 item-get flag (d_item)
        F(0x3504, "LITHOGRAPH_2", Collectible, desc: "Lithograph 2"), // LITHOGRAPH_2 item-get flag
        F(0x3502, "LITHOGRAPH_3", Collectible, desc: "Lithograph 3"), // LITHOGRAPH_3 item-get flag
        F(0x3501, "LITHOGRAPH_4", Collectible, desc: "Lithograph 4"), // LITHOGRAPH_4 item-get flag
        // ---- byte 0x36 ----
        F(0x3680, "LITHOGRAPH_5", Collectible, desc: "Lithograph 5"), // LITHOGRAPH_5 item-get flag
        F(0x3640, "LITHOGRAPH_6", Collectible, desc: "Lithograph 6"), // LITHOGRAPH_6 item-get flag
        F(0x3620, "LITHOGRAPH_7", Collectible, desc: "Lithograph 7"), // LITHOGRAPH_7 (collectmap64) item-get flag
        F(0x3610, "LITHOGRAPH_8", Collectible, desc: "Lithograph 8"), // LITHOGRAPH_8 (collectmap63) item-get flag
        F(0x3608, "LITHOGRAPH_9", Collectible, desc: "Lithograph 9"), // LITHOGRAPH_9 (collectmap62) item-get flag
        F(0x3604, "LITHOGRAPH_10", Collectible, desc: "Lithograph 10"), // LITHOGRAPH_10 item-get check
        F(0x3602, "UNK_3602", Unknown),
        F(0x3601, "UNK_3601", Unknown),
        // ---- byte 0x37 ----
        F(0x3780, "UNK_3780", Unknown),
        F(0x3740, "UNK_3740", Unknown),
        F(0x3720, "UNK_3720", Unknown),
        F(0x3710, "UNK_3710", Unknown),
        F(0x3708, "UNK_3708", Unknown),
        F(0x3704, "UNK_3704", Unknown),
        F(0x3702, "UNK_3702", Unknown),
        F(0x3701, "UNK_3701", Unknown),
        // ---- byte 0x38 ----
        F(0x3880, "UNK_3880", Unknown, desc: "Wind Temple face stone broken"), // set by Wind Temple face-stone collision (obj_homen)
        F(0x3840, "UNK_3840", TutorialHint, desc: "KoRL hint message (6)"), // KoRL hint message seen
        F(0x3820, "MOVED_HYRULE_STATUE", LocalOnly, desc: "Hyrule statue moved (recomputed every load)"), // MOVED_HYRULE_STATUE: recomputed on/off every load by d_a_obj_YLzou.cpp:143 set_start_type
        F(0x3810, "UNK_3810", CutsceneSeen, desc: "Left Hyrule (first warp)"), // First Hyrule warp used (warphr); read by ship
        F(0x3808, "UNK_3808", NpcState, desc: "Lenzo dialogue"), // Lenzo
        F(0x3804, "HYRULE_COURTYARD_CUTSCENE", Story, risky: true, desc: "Hyrule courtyard cutscene (KoRL needs Zelda awakened)"), // HYRULE_COURTYARD_CUTSCENE; ship boarding DISABLED until ZELDA_AWAKENED (d_a_ship.cpp:4226)
        F(0x3802, "COLORS_IN_HYRULE", Story, desc: "Colours in Hyrule"), // COLORS_IN_HYRULE (Hyrule layers, enemies)
        F(0x3801, "UNK_3801", Unknown),
        // ---- byte 0x39 ----
        F(0x3980, "UNK_3980", CutsceneSeen, desc: "Hyrule statue stairs cutscene"), // Hyrule statue stairs demo (YLzou); read by barrier
        F(0x3940, "UNK_3940", Unknown, desc: "Sea chart state"), // read by sea chart menu only
        F(0x3920, "UNK_3920", Story, desc: "Nayru's Pearl from Jabun"), // getperl_jab.stb (Nayru's Pearl from Jabun)
        F(0x3910, "UNK_3910", Story, desc: "Ganon attack cutscene"), // attack_ganon.stb
        F(0x3908, "UNK_3908", CutsceneSeen, desc: "Dragon Roost clouds lift (Din's Pearl)"), // DRI clouds lift near Valoo (obj_rcloud)
        F(0x3904, "TRIALS_DOOR_LIGHT_GOHMA", Story, desc: "Trials door light: Gohma"), // Trials door light: Gohma (set via obj_vgnfd M_door_ev_table)
        F(0x3902, "TRIALS_DOOR_LIGHT_KALLE_DEMOS", Story, desc: "Trials door light: Kalle Demos"), // Trials door light: Kalle Demos
        F(0x3901, "TRIALS_DOOR_LIGHT_JALHALLA", Story, desc: "Trials door light: Jalhalla"), // Trials door light: Jalhalla
        // ---- byte 0x3A ----
        F(0x3A80, "TRIALS_DOOR_LIGHT_MOLGERA", Story, desc: "Trials door light: Molgera"), // Trials door light: Molgera
        F(0x3A40, "UNK_3A40", Unknown),
        F(0x3A20, "UNK_3A20", NpcState, desc: "Fishman Triforce dialogue"), // Fishman Triforce talk (npc_so)
        F(0x3A10, "UNK_3A10", NpcState, desc: "Fishman bow dialogue"), // Fishman bow talk (npc_so)
        F(0x3A08, "UNK_3A08", Story, desc: "Phantom Ganon door opened"), // Phantom Ganon door opened (obj_vfan)
        F(0x3A04, "MASTER_SWORD_SWINGING_CUTSCENE", Story, desc: "Master Sword swinging cutscene"), // MASTER_SWORD_SWINGING_CUTSCENE (swing_sword.stb)
        F(0x3A02, "UNK_3A02", Story, desc: "Earth Temple cleared (Medli's prayer)"), // pray_zola.stb (Earth Temple prayer)
        F(0x3A01, "UNK_3A01", SideQuest, desc: "Nintendo Gallery figurine made"), // Nintendo Gallery figurine made (npc_mt setFigure); re-set on New Game+
        // ---- byte 0x3B ----
        F(0x3B80, "UNK_3B80", NpcState, desc: "Medli dialogue (8)"), // Medli dialogue
        F(0x3B40, "UNK_3B40", Story, desc: "Hyrule: all enemies defeated"), // Hyroom layer 6 (d_com_inf_game)
        F(0x3B20, "UNK_3B20", LocalOnly, desc: "Random door password generated"), // Random door password generated; paired with LOCAL random register BA0F (tag_event:112, knob00)
        F(0x3B10, "UNK_3B10", Story, desc: "Medli awakened as a sage"), // awake_zola.stb
        F(0x3B08, "UNK_3B08", Story, desc: "Hyrule seal cutscene"), // seal.stb (Hyrule)
        F(0x3B04, "UNK_3B04", NpcState, desc: "Beedle dialogue (4)"), // Beedle
        F(0x3B02, "UNK_3B02", Story, desc: "Puppet Ganon intro"), // kugutu_ganon.stb (Puppet Ganon intro); GanonK layer
        F(0x3B01, "UNK_3B01", LocalOnly, desc: "Moonlight salvage point 8 (weekly)"), // WEEKLY reset: salvage point 8 (dayproc:92)
        // ---- byte 0x3C ----
        F(0x3C80, "UNK_3C80", LocalOnly, desc: "Moonlight salvage point 9 (weekly)"), // WEEKLY reset: salvage point 9 (dayproc:93)
        F(0x3C40, "UNK_3C40", LocalOnly, desc: "Moonlight salvage point 10 (weekly)"), // WEEKLY reset: salvage point 10 (dayproc:94)
        F(0x3C20, "UNK_3C20", LocalOnly, desc: "Moonlight salvage point 11 (weekly)"), // WEEKLY reset: salvage point 11 (dayproc:95)
        F(0x3C10, "UNK_3C10", LocalOnly, desc: "Moonlight salvage point 12 (weekly)"), // WEEKLY reset: salvage point 12 (dayproc:96)
        F(0x3C08, "UNK_3C08", LocalOnly, desc: "Moonlight salvage point 13 (weekly)"), // WEEKLY reset: salvage point 13 (dayproc:97)
        F(0x3C04, "UNK_3C04", LocalOnly, desc: "Moonlight salvage point 14 (weekly)"), // WEEKLY reset: salvage point 14 (dayproc:98)
        F(0x3C02, "UNK_3C02", LocalOnly, desc: "Moonlight salvage point 15 (weekly)"), // WEEKLY reset: salvage point 15 (dayproc:99)
        F(0x3C01, "UNK_3C01", Story, desc: "Helmaroc King defeated"), // Helmaroc King defeated (d_a_bdk); read by tag_hint
        // ---- byte 0x3D ----
        F(0x3D80, "UNK_3D80", LocalOnly, desc: "Gohma rematch snapshot taken"), // Boss-rematch snapshot taken for DRC (d_meter.cpp:1054 copyPlayerRecollectionData); gates Xboss0 restore
        F(0x3D40, "UNK_3D40", LocalOnly, desc: "Kalle Demos rematch snapshot taken"), // Boss-rematch snapshot taken for FW (d_meter.cpp:1057); gates Xboss1 restore
        F(0x3D20, "UNK_3D20", LocalOnly, desc: "Jalhalla rematch snapshot taken"), // Boss-rematch snapshot taken for ET (d_meter); gates Xboss2 restore
        F(0x3D10, "UNK_3D10", LocalOnly, desc: "Molgera rematch snapshot taken"), // Boss-rematch snapshot taken for WT (d_meter); gates Xboss3 restore
        F(0x3D08, "UNK_3D08", SideQuest, desc: "Nintendo Gallery complete"), // Nintendo Gallery complete (npc_mt isComp)
        F(0x3D04, "UNK_3D04", TutorialHint, desc: "KoRL hint: Triforce"), // KoRL hint message seen
        F(0x3D02, "UNK_3D02", Story, desc: "Ganon's Tower portal opened"), // Ganon's Tower warp appeared (warpgn); read by ship, d_stage
        F(0x3D01, "UNK_3D01", NpcState, desc: "Zephos and Cyclos hidden"), // Zephos/Cyclos hide (npc_hr)
        // ---- byte 0x3E ----
        F(0x3E80, "UNK_3E80", SideQuest, desc: "Warship destroyed"), // Warship destroyed (oship modeDelete); read by agbsw0
        F(0x3E40, "UNK_3E40", TutorialHint, desc: "First tornado hint"), // First tornado (ship procTornadoUp_init)
        F(0x3E20, "UNK_3E20", TutorialHint, desc: "KoRL hint message (7)"), // KoRL hint message seen
        F(0x3E10, "UNK_3E10", Unknown, risky: true, desc: "Hyrule state that blocks boarding KoRL"), // Set by data; with !3F80 DISABLES ship boarding (d_a_ship.cpp:4227) and triggers 3E01 on scene change
        F(0x3E04, "UNK_3E04", NpcState, desc: "Traveling merchant dialogue"), // Traveling merchant
        F(0x3E02, "UNK_3E02", SideQuest, desc: "Salvage done"), // Salvage done (d_a_salvage end_salvage); read by agbsw0
        F(0x3E01, "UNK_3E01", Unknown, risky: true, desc: "KoRL message after a scene change"), // Set by player_main on scene change/fall when 3E10 set; drives KoRL messages
        // ---- byte 0x3F ----
        F(0x3F80, "UNK_3F80", TutorialHint, desc: "KoRL message state"), // Set with 3E01 (player_main makeBgWait) / KoRL message
        F(0x3F40, "UNK_3F40", Story, desc: "Ganondorf defeated"), // endhr.stb (GTower); read by player_sword
        F(0x3F20, "UNK_3F20", Story, desc: "Phantom Ganon defeated"), // Phantom Ganon defeated (d_a_fganon)
        F(0x3F10, "UNK_3F10", Story, desc: "Puppet Ganon defeated"), // Puppet Ganon defeated (d_a_bgn)
        F(0x3F02, "UNK_3F02", Unknown, desc: "Dragon Roost room trigger"), // read by d_s_room (triggers 1580 on DRI load)
        F(0x3F01, "UNK_3F01", LocalOnly, desc: "Carlov: gallery state"), // Carlov gallery state; set on npc_mt delete, cleared by npc_mt (d_a_npc_mt.cpp:401,570,963,1015)
        // ---- byte 0x40 ----
        F(0x4080, "UNK_4080", LocalOnly, desc: "Carlov: figurine handed over"), // Carlov figurine handed over; cleared by npc_mt (d_a_npc_mt.cpp:400,569,962,1014)
        F(0x4040, "UNK_4040", LocalOnly, desc: "Carlov: room state"), // Carlov room-in state; cleared by npc_mt (d_a_npc_mt.cpp:402,571,964,1016)
        F(0x4020, "UNK_4020", LocalOnly, desc: "Grab-miss hint"), // Grab-miss hint (player_grab); cleared on tag_hint delete (d_a_tag_hint.cpp:733)
        F(0x4008, "UNK_4008", LocalOnly, desc: "Last auction won or lost"), // Last auction won/lost; toggled by d_a_auction.cpp:1428/1432 each auction
        F(0x4004, "UNK_4004", Story, desc: "Wind Temple cleared (Makar's prayer)"), // pray_kokiri.stb (Wind Temple prayer)
        F(0x4002, "UNK_4002", Story, desc: "Ganondorf cutscene (Ganon's Tower)"), // g2before.stb; GTower layer
        F(0x4001, "UNK_4001", CutsceneSeen, desc: "Companion cutscene (9)"), // Companion cutscene (tag_md_cb)
        // ---- byte 0x41 ----
        F(0x4180, "UNK_4180", CutsceneSeen, desc: "Companion cutscene (10)"), // Companion cutscene (npc_md carry / tag_md_cb)
    ];

    /// <summary>All 131 named event registers (bytes 0x79-0xFF).</summary>
    public static IReadOnlyList<EventRegisterInfo> Registers { get; } =
    [
        R(0x790F, "UNK_790F", P.LocalOnly),                  // Auction: model of last won item (d_a_auction)
        R(0x7A03, "LETTER_ROCK_SPIRE_SHOP_AD", P.Max),       // Letter state 0 none/1 sent/2 stocked/3 read (d_letter); mail bag is per-player
        R(0x7B03, "LETTER_ORCA", P.Max),                     // Letter state (d_letter)
        R(0x7C03, "LETTER_BAITO", P.Max),                    // Letter state (d_letter)
        R(0x7D03, "LETTER_BOMBS_AD", P.Max),                 // Letter state (d_letter)
        R(0x7EFF, "UNK_7EFF", P.LocalOnly),                  // Initialised to 0x0E by setInitEventBit (d_save_init.cpp:14)
        R(0x7F0F, "UNK_7F0F", P.LocalOnly),                  // Beedle counter < 10 (npc_bs1)
        R(0x80FF, "UNK_80FF", P.BitwiseOr),                  // Nintendo Gallery figurine bits (npc_mn/mt l_figure_comp); kept on New Game+
        R(0x81FF, "UNK_81FF", P.BitwiseOr),                  // Figurine bits
        R(0x82FF, "UNK_82FF", P.BitwiseOr),                  // Figurine bits
        R(0x83FF, "UNK_83FF", P.BitwiseOr),                  // Figurine bits
        R(0x84FF, "UNK_84FF", P.BitwiseOr),                  // Figurine bits
        R(0x85FF, "UNK_85FF", P.LocalOnly),                  // Ghost Ship spawn start code (d_a_ghostship)
        R(0x86FF, "UNK_86FF", P.Max),                        // Beedle points card (npc_bs1 counter, +1 per purchase): Shared story, MAX (StoryFlags.BeedlePoints)
        R(0x870F, "UNK_870F", P.LocalOnly),                  // Manny transient counter, zeroed on create/delete (npc_mn)
        R(0x8803, "GHOST_SHIP", P.Max),                      // GHOST_SHIP progress 0..3 (tag_ghostship)
        R(0x89FF, "UNK_89FF", P.LocalOnly),                  // Picto Box picture count (d_picture_box; preserved by reinit)
        R(0x8AFF, "UNK_8AFF", P.LocalOnly),                  // Koboli/Baito minigame record (npc_bmsw/btsw)
        R(0x8B03, "LETTER_ARYLL", P.Max),                    // LETTER_ARYLL state (auto-stocked by dayproc when 1820)
        R(0x8CFF, "UNK_8CFF", P.BitwiseOr),                  // Figurine bits
        R(0x8DFF, "UNK_8DFF", P.BitwiseOr),                  // Figurine bits
        R(0x8EFF, "UNK_8EFF", P.BitwiseOr),                  // Figurine bits
        R(0x8FFF, "UNK_8FFF", P.BitwiseOr),                  // Figurine bits
        R(0x90FF, "UNK_90FF", P.BitwiseOr),                  // Figurine bits
        R(0x91FF, "UNK_91FF", P.BitwiseOr),                  // Figurine bits
        R(0x92FF, "UNK_92FF", P.BitwiseOr),                  // Figurine bits
        R(0x93FF, "UNK_93FF", P.BitwiseOr),                  // Figurine bits
        R(0x94FF, "UNK_94FF", P.BitwiseOr),                  // Figurine bits
        R(0x95FF, "UNK_95FF", P.BitwiseOr),                  // Figurine bits
        R(0x96FF, "UNK_96FF", P.Unknown),
        R(0x97FF, "UNK_97FF", P.Unknown),
        R(0x98FF, "UNK_98FF", P.Unknown),
        R(0x99FF, "UNK_99FF", P.Unknown),
        R(0x9AFF, "UNK_9AFF", P.Unknown),
        R(0x9B07, "UNK_9B07", P.Unknown),
        R(0x9CFF, "UNK_9CFF", P.BitwiseOr),                  // Figurine bits
        R(0x9D03, "LETTER_GRANDMA", P.Max),                  // LETTER_GRANDMA state
        R(0x9EFF, "UNK_9EFF", P.LocalOnly),                  // Zeroed by player_main execute; read by sea chart
        R(0x9F07, "UNK_9F07", P.BitwiseOr),                  // Warp pot activation bits (obj_warpt onWarpBit)
        R(0xA007, "UNK_A007", P.BitwiseOr),                  // Warp pot activation bits
        R(0xA107, "UNK_A107", P.BitwiseOr),                  // Warp pot activation bits
        R(0xA207, "UNK_A207", P.BitwiseOr),                  // Warp pot activation bits
        R(0xA307, "UNK_A307", P.BitwiseOr),                  // Warp pot activation bits
        R(0xA407, "UNK_A407", P.BitwiseOr),                  // Warp pot activation bits
        R(0xA507, "UNK_A507", P.LocalOnly),                  // DRC flame-lift counter (mflft)
        R(0xA60F, "UNK_A60F", P.LocalOnly),                  // Grandma soup refill counter, DAILY increment (dayproc:79)
        R(0xA7FF, "UNK_A7FF", P.Unknown),
        R(0xA8FF, "UNK_A8FF", P.Unknown),
        R(0xA9FF, "UNK_A9FF", P.LocalOnly),                  // Carlov: figurine currently being made (npc_mt)
        R(0xAAFF, "UNK_AAFF", P.Unknown),                    // Race time-limit modifier (goal_flag); setter is data
        R(0xAB03, "UNK_AB03", P.LocalOnly),                  // Baito counter, DAILY increment (dayproc:23)
        R(0xAC03, "LETTER_BAITOS_MOM", P.Max),               // LETTER_BAITOS_MOM state (dayproc delivery)
        R(0xADFF, "UNK_ADFF", P.LocalOnly),                  // Salvage treasure counter (salvage_tbox)
        R(0xAE03, "LETTER_HOSKITS_GIRLFRIEND", P.Max),       // LETTER_HOSKITS_GIRLFRIEND state
        R(0xAF03, "LETTER_GOLD_MEMBERSHIP", P.Max),          // LETTER_GOLD_MEMBERSHIP state
        R(0xB003, "LETTER_SILVER_MEMBERSHIP", P.Max),        // LETTER_SILVER_MEMBERSHIP state
        R(0xB1FF, "UNK_B1FF", P.BitwiseOr),                  // Figurine bits
        R(0xB203, "LETTER_TINGLE", P.Max),                   // LETTER_TINGLE state
        R(0xB503, "LETTER_KOMALIS_FATHER", P.Max),           // LETTER_KOMALIS_FATHER state
        R(0xB6FF, "UNK_B6FF", P.Unknown),
        R(0xB703, "UNK_B703", P.Unknown),
        R(0xB8FF, "UNK_B8FF", P.LocalOnly),                  // Rito dialogue selector; zeroed on DRI room load (d_s_room.cpp:277)
        R(0xB907, "UNK_B907", P.LocalOnly),                  // Windfall NPC state with DAILY increment (dayproc:71)
        R(0xBA0F, "UNK_BA0F", P.LocalOnly),                  // Random door password (tag_event cM_rndF); pair of flag 3B20
        R(0xBB07, "UNK_BB07", P.LocalOnly),                  // Beedle 7-day counter, DAILY increment (dayproc:62)
        R(0xBCFF, "UNK_BCFF", P.LocalOnly),                  // DAILY zeroed (dayproc:31)
        R(0xBEFF, "UNK_BEFF", P.LocalOnly),                  // Flight-platform high score, initialised from daNpc_Kg1_c::m_highscore
        R(0xBFFF, "UNK_BFFF", P.LocalOnly),                  // Pig counter (d_a_kb)
        R(0xC0FF, "UNK_C0FF", P.LocalOnly),                  // Joy Pendants given to Mrs. Marie (counter, npc_ho)
        R(0xC103, "UNK_C103", P.LocalOnly),                  // Windfall NPC 1->2 on day change (dayproc:53)
        R(0xC203, "UNK_C203", P.Max),                        // Koboli mail-sorting progress 1..3 (npc_bmsw)
        R(0xC3FF, "UNK_C3FF", P.LocalOnly),                  // Ghost Ship spawn room (d_a_ghostship)
        R(0xC407, "UNK_C407", P.LocalOnly),                  // Lenzo quest state; DAILY 6->7 (dayproc:66), forced 7 on New Game+
        R(0xC5FF, "UNK_C5FF", P.Unknown),
        R(0xC603, "UNK_C603", P.Unknown),                    // Traveling merchant 3 (unused per decomp)
        R(0xC703, "UNK_C703", P.Unknown),                    // Traveling merchant 2 (unused per decomp)
        R(0xC803, "UNK_C803", P.Unknown),                    // Traveling merchant 1 (unused per decomp)
        R(0xC903, "UNK_C903", P.LocalOnly),                  // Merchant 3 trade count, DAILY zeroed (dayproc:34)
        R(0xCA03, "UNK_CA03", P.LocalOnly),                  // Merchant 2 trade count, DAILY zeroed (dayproc:33)
        R(0xCB03, "UNK_CB03", P.LocalOnly),                  // Merchant 1 trade count, DAILY zeroed (dayproc:32)
        R(0xCCFF, "UNK_CCFF", P.LocalOnly),                  // Moblin's-letter day counter, DAILY increment (dayproc:27)
        R(0xCD03, "UNK_CD03", P.LocalOnly),                  // Auction: index of last won item
        R(0xCF03, "UNK_CF03", P.LocalOnly),                  // Orca counter, DAILY increment, reset by ji1
        R(0xD003, "UNK_D003", P.Max),                        // Orca lesson level (npc_ji1 setClearRecord)
        R(0xD1FF, "UNK_D1FF", P.LocalOnly),                  // Item placed on pedestal (daDai_c m_savelabel): item number, not a bitfield
        R(0xD2FF, "UNK_D2FF", P.LocalOnly),                  // Pedestal item number
        R(0xD3FF, "UNK_D3FF", P.LocalOnly),                  // Pedestal item number
        R(0xD4FF, "UNK_D4FF", P.LocalOnly),                  // Pedestal item number
        R(0xD5FF, "UNK_D5FF", P.LocalOnly),                  // Pedestal item number
        R(0xD6FF, "UNK_D6FF", P.LocalOnly),                  // Pedestal item number
        R(0xD7FF, "UNK_D7FF", P.LocalOnly),                  // Pedestal item number
        R(0xD8FF, "UNK_D8FF", P.LocalOnly),                  // Pedestal item number
        R(0xD9FF, "UNK_D9FF", P.LocalOnly),                  // Pedestal item number
        R(0xDAFF, "UNK_DAFF", P.LocalOnly),                  // Pedestal item number
        R(0xDBFF, "UNK_DBFF", P.LocalOnly),                  // Pedestal item number
        R(0xDCFF, "UNK_DCFF", P.LocalOnly),                  // Pedestal item number
        R(0xDDFF, "UNK_DDFF", P.LocalOnly),                  // Pedestal item number
        R(0xDEFF, "UNK_DEFF", P.LocalOnly),                  // Pedestal item number
        R(0xDFFF, "UNK_DFFF", P.LocalOnly),                  // Pedestal item number
        R(0xE0FF, "UNK_E0FF", P.LocalOnly),                  // Pedestal item number
        R(0xE1FF, "UNK_E1FF", P.LocalOnly),                  // Pedestal item number
        R(0xE2FF, "UNK_E2FF", P.LocalOnly),                  // Pedestal item number
        R(0xE3FF, "UNK_E3FF", P.LocalOnly),                  // Pedestal item number
        R(0xE4FF, "UNK_E4FF", P.LocalOnly),                  // Pedestal item number
        R(0xE5FF, "UNK_E5FF", P.LocalOnly),                  // Pedestal item number
        R(0xE6FF, "UNK_E6FF", P.LocalOnly),                  // Pedestal item number
        R(0xE7FF, "UNK_E7FF", P.LocalOnly),                  // Pedestal item number
        R(0xE8FF, "UNK_E8FF", P.LocalOnly),                  // Pedestal item number
        R(0xE9FF, "UNK_E9FF", P.LocalOnly),                  // Pedestal item number
        R(0xEAFF, "UNK_EAFF", P.LocalOnly),                  // Pedestal item number
        R(0xEBFF, "UNK_EBFF", P.LocalOnly),                  // Pedestal item number
        R(0xECFF, "UNK_ECFF", P.LocalOnly),                  // Pedestal item number
        R(0xEDFF, "UNK_EDFF", P.LocalOnly),                  // Pedestal item number
        R(0xEEFF, "UNK_EEFF", P.LocalOnly),                  // Pedestal item number
        R(0xEFFF, "UNK_EFFF", P.LocalOnly),                  // Pedestal item number
        R(0xF0FF, "UNK_F0FF", P.LocalOnly),                  // Pedestal item number
        R(0xF1FF, "UNK_F1FF", P.LocalOnly),                  // Pedestal item number
        R(0xF2FF, "UNK_F2FF", P.LocalOnly),                  // Pedestal item number
        R(0xF3FF, "UNK_F3FF", P.LocalOnly),                  // Pedestal item number (also read by npc_people l_daiza_no_tbl)
        R(0xF4FF, "UNK_F4FF", P.LocalOnly),                  // Pedestal item number
        R(0xF5FF, "UNK_F5FF", P.LocalOnly),                  // Pedestal item number
        R(0xF6FF, "UNK_F6FF", P.LocalOnly),                  // Pedestal item number
        R(0xF7FF, "UNK_F7FF", P.LocalOnly),                  // Pedestal item number
        R(0xF8FF, "UNK_F8FF", P.LocalOnly),                  // Pedestal item number
        R(0xF903, "UNK_F903", P.Unknown),
        R(0xFAFF, "UNK_FAFF", P.Unknown),
        R(0xFBFF, "UNK_FBFF", P.Unknown),
        R(0xFC03, "UNK_FC03", P.Unknown),
        R(0xFD07, "UNK_FD07", P.Unknown),
        R(0xFE07, "UNK_FE07", P.Unknown),
        R(0xFF07, "UNK_FF07", P.Unknown),
    ];

    private static readonly Dictionary<ushort, EventFlagInfo> ById = Flags.ToDictionary(f => f.Id);

    public static EventFlagInfo? Find(ushort id) => ById.GetValueOrDefault(id);

    /// <summary>Look up the flag at <paramref name="byteIndex"/> / single-bit <paramref name="bitMask"/>.</summary>
    public static EventFlagInfo? Find(int byteIndex, byte bitMask) =>
        byteIndex is >= 0 and < EventBitfieldSize ? Find((ushort)((byteIndex << 8) | bitMask)) : null;

    /// <summary>
    /// Per-byte mask of bits that may be OR-merged under "Full sync": every named flag except LocalOnly and
    /// risky Unknown. Unnamed bits and the whole register area (0x79-0xFF) are 0. Returns a fresh array.
    /// </summary>
    public static byte[] GetFullSyncMask() => BuildMask(f => f.SyncableUnderFullSync);

    /// <summary>
    /// What goes wrong when a <see cref="EventFlagInfo.Risky"/> flag is set on a game that did not reach it
    /// itself (decomp evidence in docs/event-flags.md). Every risky flag has an entry; story sync logs it as a
    /// WARNING when it applies one. The two risky Unknown flags are listed for completeness but are not synced.
    /// </summary>
    public static IReadOnlyDictionary<ushort, string> RiskEffects { get; } = new Dictionary<ushort, string>
    {
        // dComIfGs_setGameStartStage (d_com_inf_game.cpp:1304): first set flag picks where a loaded save starts
        [0x0F80] = "respawn point may change on next load (MET_KORL: save loads on the sea at Windfall)",
        [0x0801] = "respawn point may change on next load (save loads in the Forsaken Fortress, MajyuE)",
        [0x0808] = "respawn point may change on next load (save loads in the Forsaken Fortress, MajyuE start 18)",
        [0x2401] = "respawn point may change on next load (save loads at the Outset departure stage, A_umikz start 204)",
        // d_a_ship.cpp:4224-4229: removes the ship-board action
        [0x2D10] = "boarding King of Red Lions is disabled until the Master Sword is equipped",
        [0x3804] = "boarding King of Red Lions is disabled until ZELDA_AWAKENED is set",
        [0x3E10] = "boarding King of Red Lions is disabled until UNK_3F80 is set (not synced)",
        [0x3E01] = "drives KoRL messages after a scene change (not synced)",
    };

    /// <summary>The known side effect of setting a risky flag without its trigger, or null.</summary>
    public static string? RiskEffect(ushort id) => RiskEffects.GetValueOrDefault(id);

    /// <summary>Build a 256-byte mask from any predicate (e.g. Story + CutsceneSeen only for a lighter mode).</summary>
    public static byte[] BuildMask(Func<EventFlagInfo, bool> include)
    {
        var mask = new byte[EventBitfieldSize];
        foreach (var f in Flags)
        {
            if (include(f))
                mask[f.ByteIndex] |= f.Mask;
        }
        return mask;
    }
}
