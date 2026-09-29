namespace WWOnline.Patcher.WorldData;

/// <summary>
/// Our own hand-written heart data (docs/hearts.md), not Nintendo's: the NPC / minigame / letter rewards whose flag
/// is set by an actor's code rather than placed in a stage file, the names of the vanilla sources the stage files
/// give (<see cref="HeartTableBuilder"/> finds those by their actors and flags), and the stages nothing reaches.
/// Every flag was mapped in tww-decomp (or, where the actor isn't decompiled, in the vanilla REL's code) and
/// cross-checked against the randomizer's location list; docs/hearts.md has the evidence for each.
/// </summary>
public static class HeartCatalog
{
    /// <summary>The 16 vanilla Pieces of Heart that NPCs, minigames and letters give, and the flag each sets.</summary>
    public static IReadOnlyList<HeartSource> Rewards { get; } =
    [
        Reward("Outset Island - Orca - Hit 500 Times", HeartFlag.Event(0x0F10), "d_a_npc_ji1 createItem (onEventBit with the heart)"),
        Reward("Windfall Island - Ivan - Catch Killer Bees", HeartFlag.Event(0x1340), "d_a_npc_mk (demo cut 13: heart + onEventBit)"),
        Reward("Windfall Island - Maggie - Delivery Reward", HeartFlag.MoblinsLetter, "d_a_npc_kp1 GET_KAKERA_HRT (takes Moblin's Letter)"),
        Reward("Windfall Island - Kreeb - Light Up Lighthouse", HeartFlag.Event(0x1B20), "d_a_npc_people NPC_UM1 l_msg_um1_get_item"),
        Reward("Windfall Island - 80 Rupee Auction", HeartFlag.Event(0x1020), "d_a_auction l_item_dat[3]"),
        Reward("Windfall Island - Sam - Decorate the Town", HeartFlag.Event(0x1B10), "d_a_npc_people NPC_UO1 l_msg_uo1_1st_talk_fdai"),
        Reward("Windfall Island - Battlesquid - First Prize", HeartFlag.Register(0xFE07, 1), "d_a_npc_kg1 (first-prize count, +1 before the prize; heart at 1)"),
        Reward("Windfall Island - Linda and Anton", HeartFlag.Event(0x2280), "d_a_npc_people NPC_UW2 l_msg_uw2_1st_talk2"),
        Reward("Mailbox - Letter from Hoskit's Girlfriend", HeartFlag.Register(0xAE03, 3), "d_a_obj_toripost (letter read: dLetter READ_e)"),
        Reward("Mailbox - Letter from Baito's Mother", HeartFlag.Register(0xAC03, 3), "d_a_obj_toripost (letter read: dLetter READ_e)"),
        Reward("Mailbox - Letter from Komali's Father", HeartFlag.Register(0xB503, 3), "d_a_obj_toripost (letter read: dLetter READ_e)"),
        Reward("The Great Sea - Goron Trading Reward", HeartFlag.Event(0x3E04), "d_a_npc_roten (last trade)"),
        Reward("The Great Sea - Withered Trees", HeartFlag.Event(0x2E20), "d_a_obj_ftree (the heart it drops was picked up)"),
        Reward("Spectacle Island - Barrel Shooting - First Prize", HeartFlag.Register(0xB703, 1), "d_a_npc_kg2 (first-prize count, +1 before KG2_GETDEMO; heart at 1)"),
        Reward("Rock Spire Isle - Beedle's Special Shop Ship - 950 Rupee Item", HeartFlag.Event(0x2010), "d_a_npc_bs1 (bought)"),
        Reward("Flight Control Platform - Bird-Man Contest - First Prize", HeartFlag.Event(0x2B40), "d_a_npc_bmcon1 (first first prize)"),
    ];

    /// <summary>The 34 vanilla sources found in the stage files, by flag: names for the logs and docs, and the list
    /// the vanilla test checks the built table against (it must find exactly these).</summary>
    public static IReadOnlyDictionary<HeartFlag, string> StageSourceNames { get; } = new Dictionary<HeartFlag, string>
    {
        // Chests (daTbox_c): tbox bit of the stage's save slot
        [HeartFlag.Chest(13, 12)] = "Outset Island - Savage Labyrinth - Floor 50",
        [HeartFlag.Chest(0, 10)] = "Windfall Island - Transparent Chest",
        [HeartFlag.Chest(0, 6)] = "Greatfish Isle - Hidden Chest",
        [HeartFlag.Chest(2, 3)] = "Forsaken Fortress - Chest Inside Lower Jail Cell",
        [HeartFlag.Chest(0, 3)] = "Needle Rock Isle - Chest",
        [HeartFlag.Chest(0, 0)] = "Angular Isles - Peak",
        [HeartFlag.Chest(0, 20)] = "Stone Watcher Island - Lookout Platform - Destroy the Cannons",
        [HeartFlag.Chest(12, 26)] = "Pawprint Isle - Chuchu Cave - Chest",
        [HeartFlag.Chest(12, 5)] = "Bomb Island - Cave",
        [HeartFlag.Chest(12, 6)] = "Star Island - Cave",
        [HeartFlag.Chest(10, 1)] = "Five-Star Isles - Submarine",
        [HeartFlag.Chest(10, 0)] = "Six-Eye Reef - Submarine",
        // Placed (daItem_c) and dug-up (daTagKbItem_c) items: memory item bit
        [HeartFlag.Item(0, 2)] = "Outset Island - Dig up Black Soil",
        [HeartFlag.Item(0, 8)] = "Headstone Island - Top of the Island",
        // Salvage points (daSalvage_c): switch-gated ones save an ocean bit, sunken treasure the chart's bit
        [HeartFlag.Ocean(17, 0)] = "Tingle Island - Big Octo",
        [HeartFlag.Ocean(6, 0)] = "Seven-Star Isles - Big Octo",
        [HeartFlag.Ocean(16, 0)] = "Rock Spire Isle - Southeast Gunboat",
        [HeartFlag.Chart(9)] = "Crescent Moon Island - Sunken Treasure",
        [HeartFlag.Chart(11)] = "Pawprint Isle - Sunken Treasure",
        [HeartFlag.Chart(17)] = "Rock Spire Isle - Sunken Treasure",
        [HeartFlag.Chart(18)] = "Three-Eye Reef - Sunken Treasure",
        [HeartFlag.Chart(13)] = "Thorned Fairy Island - Sunken Treasure",
        [HeartFlag.Chart(12)] = "Bomb Island - Sunken Treasure",
        [HeartFlag.Chart(14)] = "Diamond Steppe Island - Sunken Treasure",
        [HeartFlag.Chart(30)] = "Southern Fairy Island - Sunken Treasure",
        [HeartFlag.Chart(15)] = "Forest Haven - Sunken Treasure",
        [HeartFlag.Chart(10)] = "Angular Isles - Sunken Treasure",
        [HeartFlag.Chart(16)] = "Five-Star Isles - Sunken Treasure",
        // Boss Heart Containers (daBossItem_c marks the slot; item_func_utuwa_heart sets STAGE_LIFE there)
        [HeartFlag.StageLife(3)] = "Dragon Roost Cavern - Gohma Heart Container",
        [HeartFlag.StageLife(4)] = "Forbidden Woods - Kalle Demos Heart Container",
        [HeartFlag.StageLife(5)] = "Tower of the Gods - Gohdan Heart Container",
        [HeartFlag.StageLife(2)] = "Forsaken Fortress - Helmaroc King Heart Container",
        [HeartFlag.StageLife(6)] = "Earth Temple - Jalhalla Heart Container",
        [HeartFlag.StageLife(7)] = "Wind Temple - Molgera Heart Container",
    };

    /// <summary>
    /// Stages no vanilla play reaches, whose hearts (or flags) would otherwise be counted: <c>Cave06</c> (a heart
    /// chest; its only entrance is in <c>sea_E</c>, the unused E3-demo sea), <c>I_SubAN</c> (a heart chest in a
    /// developer sub-dungeon that neither the randomizer's location list nor any exit a player takes leads to) and
    /// <c>DmSpot0</c> (an unused copy of Outset in the sea's save slot, no exit leads there; its rupee uses item
    /// bit 2, the black soil heart's). STAGE_TEST (save slot 15) stages are left out by slot.
    /// </summary>
    public static IReadOnlySet<string> UnreachableStages { get; } = new HashSet<string>(StringComparer.Ordinal) { "Cave06", "I_SubAN", "DmSpot0" };

    /// <summary>dSv_save_c::STAGE_TEST: the debug stages' save slot.</summary>
    public const int TestSlot = 15;

    private static HeartSource Reward(string name, HeartFlag flag, string where) =>
        new(flag, 1, HeartSourceType.Reward, name, where);
}
