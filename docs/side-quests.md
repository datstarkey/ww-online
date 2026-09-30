# Side quests in a shared room

Every side quest in the GameCube game, where its progress is kept, whether a Full sync room shares it, and whether another player sees the change **live** or only after a room or area reload. §3 lists the gaps this audit found and how each was fixed (protocol 11).

Target: GZLE01. Sources: `tww-decomp/` (paths relative to `src/d/actor/` unless they say otherwise), the event flag catalogue (`EventFlagCatalog`, [event-flags.md](event-flags.md)), the heart catalogue ([hearts.md](hearts.md)), and the quest lists in the public guides (Game8's side-quest and 100% walkthrough pages; IGN, GameFAQs, StrategyWiki, Zelda Wiki and Zelda Dungeon refuse automated fetches). **Nothing here has been played through in a room yet**: it is read from the code, so every row is a prediction until a session confirms it.

## 1. How a quest's progress travels

A side quest keeps its state in one or more of these, and each has its own rule and timing:

| Where the state lives | Shared by | When another player's game sees it |
|---|---|---|
| Items, equipment, capacities, songs, charts, sea chart | Shared items | Live (written while idle) |
| Max hearts | Shared items (derived from the room's heart sources) | Live; a heart given twice is taken back ([hearts.md](hearts.md) §0) |
| Rupees | Shared wallet | Live |
| Spoils, bait, delivery bag | Their Shared bag rules | Live (while idle) |
| Chests, stage switches, picked-up items, salvage | Shared world | Live in the same room for the objects the REL re-creates or that poll ([live-world.md](live-world.md) §0); otherwise on the next room load |
| Event bits 0x00-0x41 | Shared story | Written at once. An NPC that reads the bit **when you talk** reacts straight away; one that reads it **when it is created** (who stands where, what they hold) changes on the next room or area load, the same as in the vanilla game after its own cutscene |
| Event registers 0x79-0xFF | The figurines, warp jars, Beedle's points, the postbox letters and the side-quest registers of §3 (Shared story); the pedestals (Shared delivery bag) | Written while idle. Every other register stays with the player who changed it |
| `mTmp` (session bits), Picto Box photos | Never | Never |

So a quest works across players when its progress is event bits, items, bags or world flags, and its reward is an item, rupees, a heart or a chart. It split when a **register** carried its progress; §3 lists the registers that did, which the room now shares.

## 2. The quests

**Live** = the other players see it without a reload; **Reload** = on their next room or area load; **Per player** = not shared at all. "Traced" rows were read in the decomp; "by mechanism" rows follow from where the guide says the reward comes from and weren't traced call by call.

### Outset Island

| Quest | Progress | Reward | Shared? | Live? | Notes |
|---|---|---|---|---|---|
| Orca's sword lessons (traced, `d_a_npc_ji1`) | Level register D003, records 0F20 (1000 hits), 0B20 (Hurricane Spin), 0F10 (piece given) | Knight's Crest trade, Hurricane Spin, Piece of Heart at 500 hits | Yes (level: §3.3) | At the next lesson | |
| Rose's pigs (traced, `d_a_kb`) | Pen register BFFF (a bitmask of pigs penned), 3402 | Rupees from Rose, then the big pig digs up a Piece of Heart | Yes (pen: §3.4) | Pigs in the pen on the next load | |
| Grandma's soup (traced, `d_a_npc_ba1`) | GRANDMA_HEALED 2A20; daily refill counter A60F | Elixir Soup refills | 2A20 yes; refills per player (by design: bottles aren't shared) | At talk | Fine |

### Windfall Island

| Quest | Progress | Reward | Shared? | Live? | Notes |
|---|---|---|---|---|---|
| Killer Bees hide-and-seek (traced, `d_a_npc_mk`, `d_a_npc_ho`) | 1E02, 1E04, 2D80, 1340 | Piece of Heart (Ivan, 1340), Mrs. Marie's thanks | Yes | At talk; the kids' positions on reload | Heart via the room's sources |
| Mrs. Marie's Joy Pendants (traced, `d_a_npc_ho`) | Pendants-given register C0FF; 1C08 (Cabana Deed given), 1C04 (40-pendant reward) | 20 → Cabana Deed, 40 → Hero's Charm | Yes (count: §3.1) | At talk | Hand pendants in one player at a time |
| Lenzo's pictographs (by mechanism) | 1601, 1701; Lenzo state register C407 (daily) | Deluxe Picto Box (item) | Item and bits yes; C407 no | Item live | Photos never sync: the player who does it takes the photos. After one player finishes, the others already own the Deluxe Picto Box |
| Linda and Anton (by mechanism, `d_a_npc_people`) | 2280 | Piece of Heart | Yes | At talk | Needs your own photo (never shared) |
| Kreeb: light the lighthouse (by mechanism, `d_a_npc_people`) | 1B20 | Piece of Heart | Yes | At talk | Heart via the room's sources |
| Town Flower pedestals / decorating the town (traced, `d_a_dai`, `d_a_npc_people:8280`) | What stands on each pedestal: registers D1FF-F8FF (an item number each); 1B10 (reward given) | Piece of Heart | Yes, with Shared delivery bag (§3.2) | **Reload**: a pedestal on screen shows another player's change on your next visit | |
| Zunari's trading and the travelling merchants (traced, `d_a_npc_roten`) | The delivery bag; each merchant's trade count (C6-CB) and daily trade flags (1301-1320, local by design); 3E04 (last Goron trade) | Magic Armor (Zunari), trade goods, Piece of Heart (3E04) | Goods and rewards yes; counts per player | Bag live | The daily "traded today" flags must stay local (latching them would block trading for good) |
| The auction (by mechanism, `d_a_auction`) | Items bought: 0F01, 1004-1080 (Collectible bits) | Joy Pendant, charts, Piece of Heart, statues | Yes | At the next auction | Two players bidding in two separate auctions at the same time could both win the same item before either bit syncs |
| Sploosh Kaboom / Battlesquid (hearts.md §3.3) | Prizes-won register FE07 (indexes {heart, chart, rupees}) | Piece of Heart, then Treasure Chart | Yes (tier: §3.3) | At the next game | |
| Maggie and Moblin's Letter (hearts.md §3.3) | Delivery bag (Moblin's Letter) | Piece of Heart | Yes | Bag live | Only the first player to show her the letter gets it (it leaves the shared bag) |
| Maggie's father and the Skull Necklaces (by mechanism) | Spoils bag; the event gives a Treasure Chart (item 0xEE) | Treasure Chart | Yes | Bag live | Not traced: whether he gates on a flag or on your count is in a REL that isn't decompiled |
| Pirate ship password and Niko's ropes (traced, `d_a_knob00`, `d_a_tag_event`) | 3B20 + password register BA0F (local by design), 1910 (password solved) | Entry to the ship; bombs; Niko's challenge | 1910 yes | At the door | Each player overhears their own random password; once anyone gets in, everyone's door skips it |
| Windmill / lighthouse (traced, `d_a_obj_ferris`) | 2104 | (Kreeb's heart above) | Yes | Reload (the wheel reads its state as it runs) | |
| Tott's dance (by mechanism) | Song of Passing (item) | Song | Yes | Live | |
| Windfall residents' rupee gifts (catalogue) | 2402-2410 | Rupees | Yes | At talk | Paid once per room |

### Dragon Roost Island

| Quest | Progress | Reward | Shared? | Live? | Notes |
|---|---|---|---|---|---|
| Koboli's mail sorting (traced, `d_a_npc_bmsw`) | Level register C203 (1-3), 1A01, 1A02; record 8AFF (local) | Rupees per round; the Note to Mom route | Yes (level: §3.3) | At talk | Best time per player |
| Note to Mom / Baito's mother (hearts.md) | Delivery bag, then letter register AC03 | Piece of Heart by mail | Yes (bag, letters) | Mail on your next postbox visit | |
| Hoskit's Golden Feathers (by mechanism) | Spoils bag, then letter AE03 | Piece of Heart by mail | Yes | Bag live | Not traced |
| Komali's father's letter | 0E02, letter B503 | Piece of Heart by mail | Yes | Mail | |
| Rito postbox / Maggie's letter | 1220, delivery bag | — | Yes | Bag live | |

### Forest Haven and the Great Sea

| Quest | Progress | Reward | Shared? | Live? | Notes |
|---|---|---|---|---|---|
| Forest Water / withered trees (hearts.md §3.3) | 2E20 (heart taken); each tree's state isn't in the decomp (`d_a_obj_ftree` is a stub) | Piece of Heart | Heart yes | — | The run is a timer in one player's bottle: it can't be split. Whether a watered tree stays watered for others is unverified |
| Doc Bandam's potions (by mechanism) | Spoils bag (Chu Jellies); potions in bottles | Green / Blue Potion | Jellies yes; potions per player (bottle contents) | Bag live | |
| Tingle: rescue and the Tingle Statues (traced, `d_a_npc_tc`) | 0B80 (rescued), 0B40; a statue = its dungeon's chest 0xF (Shared world) | Rupees per statue, the Tingle Island reward | Yes | **Reload**: Tingle reads the statues when he is created (`d_a_npc_tc.cpp:83-87`) and his jail state too | Whether a statue's rupee payment is flagged once per room is **unverified**: if it isn't, each player is paid for each statue (into the shared wallet) |
| Beedle's point card and shop ship (traced, `d_a_npc_bs1`) | Points register 86FF (Shared story, MAX), 1F08-2040 | Membership letters, the 950-rupee Piece of Heart (2010) | Yes | At talk | Two players buying at the same moment can lose a point |
| Goron merchants' trades (traced, `d_a_npc_roten`) | As Zunari's above | Piece of Heart (3E04) | Yes | Bag live | |
| Fishmen (by mechanism) | Sea chart squares (Shared items) | The square's map | Yes | Live | |
| Great Fairies (by mechanism) | Capacities and magic (Shared items) | Bigger wallet, quiver, bomb bag, double magic | Yes | Live | Magic follows the Great Fairy's flag ([hearts.md](hearts.md) §0) |
| Ghost Ship (traced, `d_a_tag_ghostship`, `d_a_ghostship.cpp:275`, `d_menu_fmap.cpp:2537`) | Register 8803 (0-3; 3 = cleared); its chest (Shared world) | Triforce Chart | Yes (§3.3) | The sea chart icon at once; the ship is gone on the next load | |
| Big Octos, sunken treasure, treasure charts | Salvage bits, chart bits (Shared world / items) | Rupees, hearts, charts | Yes | **Live** (live-world.md §0.4) | |
| Savage Labyrinth, caves, submarines, platforms | Chests and switches (Shared world) | Items, hearts | Yes | Live (chests) | Enemies are never shared: each player clears their own |
| Boss Heart Containers ([hearts.md](hearts.md) §2) | The dungeon's STAGE_LIFE bit (Shared world) | Heart Container | Yes | **Live** (§3.5) | |
| Bird-Man Contest (hearts.md) | 2B40 (first prize); record BEFF (local) | Piece of Heart | Yes | At talk | High score per player |
| Barrel shooting, Spectacle Island (hearts.md) | Prizes-won register B703 | Piece of Heart, then Treasure Chart | Yes (tier: §3.3) | At the next game | |
| Private Oasis / Cabana (by mechanism) | Cabana Deed (delivery bag), maze chest (Shared world) | Entry; the maze's prize | Yes | Bag live | |
| Nintendo Gallery ([figurines.md](figurines.md)) | Figurine registers (Shared story) | Figurines | Yes | Pedestals on the next gallery room load | Photos per player |
| Mail ([event-flags.md](event-flags.md)) | Letter registers (Shared story) | Charts, hearts, tickets, rupees | Yes | Mail on your next postbox visit | Two players reading the same letter at once both get it |

## 3. Gaps and fixes

The audit found these; each is fixed as described (protocol 11 for the registers and the pedestals; the Heart Container fix is REL code, so it needs a re-patch).

### 3.1 Mrs. Marie's pendant count (was a real bug)

`daNpc_Ho_c` keeps the pendants handed over in register C0FF (`receivePendant`, `d_a_npc_ho.cpp:70-82`) and chooses her talk from it (`:405-420`): at 0 she runs the first-time talk, takes 20 pendants and gives the Cabana Deed, whatever 1C08 says. With the count per player and Shared spoils bag on, a second player's first talk took **20 more pendants from the shared bag** for a Cabana Deed the room already held (the delivery bag keeps one), and the Hero's Charm (40 given) needed 40 from one player alone.

**Fixed:** C0FF rides in the room story (`StoryFlags.QuestRegisters`), merged by MAX and written while idle, like Beedle's points. MAX is right when one player hands in at a time (the bag is one stock, so that is the normal case); two players handing in at the very same moment count only the larger total.

### 3.2 The Town Flower pedestals

Each of Windfall's pedestals keeps the item on it in its own register (`daDai_c::m_savelabel`, D1FF-F8FF, `d_a_dai.cpp:201, 320`): only the trade goods 0x8C-0x97 (`isDaizaItem`, `d_item.cpp:2749`). Setting one down empties its bag slot in the same frame (`:201-202`), and taking it back puts it in the first empty slot (`:320-323`). With the registers per player, a flower left the shared bag but stood only in one game.

**Fixed:** the pedestals ride in the shared delivery bag (`DeliveryCounts.Pedestals`, Shared delivery bag rule). A change carries each pedestal before and after with its bag change, and the server refuses the whole change if a pedestal no longer holds what the change says it held: two players using the same pedestal at once never lose or copy an item (the second gets the room's bag back). The client writes the registers while idle, compare-and-swap. A pedestal reads its register only when it is created (`d_a_dai.cpp:101`), so one already on screen shows another player's change on the next visit; until then talking to it acts on the room's state (taking an item back works, and it won't take a second item).

### 3.3 Levels and prize tiers kept in registers

| Register | Quest | What went wrong |
|---|---|---|
| D003 | Orca's lessons | The next player started at level 0; his 500-hit piece was paid again, then taken back |
| C203 | Koboli's mail sorting | Every player started at level 1 |
| FE07 | Sploosh Kaboom | The next player's first win paid the heart again (taken back) instead of the chart |
| B703 | Barrel shooting | Same |
| 8803 | Ghost Ship | Still there for the others after it was cleared |

**Fixed:** all five ride in the room story merged by MAX (value masks 0x07, 0x03, 0x07, 0x03, 0x03; Orca's is carried as 0x07 because `setClearRecord` writes level 4 past the catalogue's 0x03 mask, `d_a_npc_ji1.cpp:409-422`), written while idle.

### 3.4 Rose's pig pen

BFFF is a bitmask of the pigs in the pen (`d_a_kb.cpp:2170-2175`, OR'd in). **Fixed:** it rides in the room story OR-merged (mask 0x0F), so a pig penned by one player is penned for all, and Rose doesn't pay twice.

### 3.5 Boss Heart Containers stayed after another player took theirs

A boss's Heart Container has no item bit: picking it up sets the stage's STAGE_LIFE dungeon bit (`item_func_utuwa_heart`, `d_item.cpp:587-603`), which Shared world ORs into the live copy. The live item despawn skipped it (it waits in status 0xA/0xB, `execWaitMainFromBoss`), so a container stayed on a second player's screen; picking it up was undone by the derived hearts. **Fixed (REL, `puppet_worldsync.c`):** while a puppet is in the room, a Heart Container still waiting in a stage whose live STAGE_LIFE bit is set is deleted (never one the local player is collecting), and counted in the client's `[world] REL despawned` line (item 0x08). Not covered: the Helmaroc King container on the sea outside Forsaken Fortress (its STAGE_LIFE belongs to the Forsaken Fortress slot, not the sea's).

### 3.6 Unverified

- Tingle's statue payments: whether each statue pays once per room, or once per player (a shared wallet would then receive it once for each player).
- The withered trees: whether a tree one player watered stays watered for the others.
- Maggie's father, Hoskit and Doc Bandam gate on counts or flags inside RELs that aren't decompiled.

### 3.7 In-game checklist (Full sync; patch again first for Q7)

| # | Do | Expect |
|---|---|---|
| Q1 | P1 gives Mrs. Marie 20 pendants; P2 talks to her with pendants in the room's bag | She asks P2 for pendants towards the charm (not the first-time 20); the Cabana Deed isn't given twice. `[story]` applies "Joy Pendants given to Mrs. Marie 20" on P2 |
| Q2 | P1 wins Sploosh Kaboom once; P2 wins once | P2's win gives the Treasure Chart, not a heart |
| Q3 | P1 clears the Ghost Ship; P2 opens the sea chart | The ghost ship icon is gone; the ship isn't there on P2's next load |
| Q4 | P1 frees Tingle; P2 is on Tingle Island | P2 sees him freed after leaving and coming back |
| Q5 | P1 collects a Tingle Statue; P2 talks to Tingle on Tingle Island after a reload | The statue is there for P2; note what the wallet does (3.6) |
| Q6 | P1 sets a Town Flower on a pedestal; P2 goes to Windfall | Gone from both bags (`[delivery]` "pedestal …: empty → Town Flower"); P2 sees it on the pedestal on arrival, and can take it back |
| Q6b | P1 and P2 set a flower on the same empty pedestal at the same moment | One counts; the other player's flower comes back to their bag (`server.log`: "refused: pedestal … changed in the room first") |
| Q7 | P1 and P2 each beat Gohma; P1 picks up the container while P2 is in the room with theirs | P2's container vanishes (`dolphin-2.log`: `[PUPPET] world sync: Heart Container taken by another player`); both +4 hearts once |
| Q8 | P1 pens a pig; P2 visits Rose | The pig is in P2's pen after a load; Rose doesn't pay P2 for it |
