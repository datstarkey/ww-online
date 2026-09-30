# Side quests in a shared room

Every side quest in the GameCube game, where its progress is kept, whether a Full sync room shares it, and whether another player sees the change **live** or only after a room or area reload. §3 lists the gaps and what would fix them.

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
| Event registers 0x79-0xFF | Only the figurines, warp jars, Beedle's points and the postbox letters (Shared story) | **Every other register stays with the player who changed it**: counters, levels and prize tiers (§3) |
| `mTmp` (session bits), Picto Box photos | Never | Never |

So a quest works across players when its progress is event bits, items, bags or world flags, and its reward is an item, rupees, a heart or a chart. It splits when a **register** carries its progress.

## 2. The quests

**Live** = the other players see it without a reload; **Reload** = on their next room or area load; **Per player** = not shared at all. "Traced" rows were read in the decomp; "by mechanism" rows follow from where the guide says the reward comes from and weren't traced call by call.

### Outset Island

| Quest | Progress | Reward | Shared? | Live? | Notes |
|---|---|---|---|---|---|
| Orca's sword lessons (traced, `d_a_npc_ji1`) | Level register **D003**, records 0F20 (1000 hits), 0B20 (Hurricane Spin), 0F10 (piece given) | Knight's Crest trade, Hurricane Spin, Piece of Heart at 500 hits | Bits yes, **level no** | Bits at talk | **Gap 3.3**: another player starts at level 0 and can earn the 500-hit piece again (taken back by the heart derivation) |
| Rose's pigs (traced, `d_a_kb`) | Pen register **BFFF** (a bitmask of pigs penned), 3402 | Rupees from Rose, then the big pig digs up a Piece of Heart | Heart yes (black soil item flag, Shared world); **pen no** | Heart live (world flag) | **Gap 3.4**: each player can pen the pigs and be paid again (rupees land in the shared wallet) |
| Grandma's soup (traced, `d_a_npc_ba1`) | GRANDMA_HEALED 2A20; daily refill counter A60F | Elixir Soup refills | 2A20 yes; refills per player (by design: bottles aren't shared) | At talk | Fine |

### Windfall Island

| Quest | Progress | Reward | Shared? | Live? | Notes |
|---|---|---|---|---|---|
| Killer Bees hide-and-seek (traced, `d_a_npc_mk`, `d_a_npc_ho`) | 1E02, 1E04, 2D80, 1340 | Piece of Heart (Ivan, 1340), Mrs. Marie's thanks | Yes | At talk; the kids' positions on reload | Heart via the room's sources |
| **Mrs. Marie's Joy Pendants** (traced, `d_a_npc_ho`) | Pendants-given register **C0FF**; 1C08 (Cabana Deed given), 1C04 (40-pendant reward) | 20 → Cabana Deed, 40 → Hero's Charm | Flags and rewards yes; **the count no** | At talk | **Gap 3.1** (real bug): with the count at 0 another player gets her first-time talk again and hands over 20 pendants from the shared spoils bag for a Cabana Deed the room already has |
| Lenzo's pictographs (by mechanism) | 1601, 1701; Lenzo state register C407 (daily) | Deluxe Picto Box (item) | Item and bits yes; C407 no | Item live | Photos never sync: the player who does it takes the photos. After one player finishes, the others already own the Deluxe Picto Box |
| Linda and Anton (by mechanism, `d_a_npc_people`) | 2280 | Piece of Heart | Yes | At talk | Needs your own photo (never shared) |
| Kreeb: light the lighthouse (by mechanism, `d_a_npc_people`) | 1B20 | Piece of Heart | Yes | At talk | Heart via the room's sources |
| **Town Flower pedestals** / Sam decorates the town (traced, `d_a_dai`, `d_a_npc_people:8280`) | What stands on each pedestal: registers **D1FF-F8FF** (an item number each); 1B10 (reward given) | Piece of Heart | Reward yes; **the pedestals no** | — | **Gap 3.2**: a flower you set on a pedestal leaves the shared delivery bag but appears only in your game. Two players decorating half each never finish; the other player can't take your flower back |
| Zunari's trading and the travelling merchants (traced, `d_a_npc_roten`) | The delivery bag; each merchant's trade count (C6-CB) and daily trade flags (1301-1320, local by design); 3E04 (last Goron trade) | Magic Armor (Zunari), trade goods, Piece of Heart (3E04) | Goods and rewards yes; counts per player | Bag live | The daily "traded today" flags must stay local (latching them would block trading for good) |
| The auction (by mechanism, `d_a_auction`) | Items bought: 0F01, 1004-1080 (Collectible bits) | Joy Pendant, charts, Piece of Heart, statues | Yes | At the next auction | Two players bidding in two separate auctions at the same time could both win the same item before either bit syncs |
| Sploosh Kaboom / Battlesquid (hearts.md §3.3) | Prizes-won register **FE07** (indexes {heart, chart, rupees}) | Piece of Heart, then Treasure Chart | Heart and chart yes; **prize tier no** | — | **Gap 3.3**: another player's first win pays the heart again (taken back) instead of the chart |
| Maggie and Moblin's Letter (hearts.md §3.3) | Delivery bag (Moblin's Letter) | Piece of Heart | Yes | Bag live | Only the first player to show her the letter gets it (it leaves the shared bag) |
| Maggie's father and the Skull Necklaces (by mechanism) | Spoils bag; the event gives a Treasure Chart (item 0xEE) | Treasure Chart | Yes | Bag live | Not traced: whether he gates on a flag or on your count is in a REL that isn't decompiled |
| Pirate ship password and Niko's ropes (traced, `d_a_knob00`, `d_a_tag_event`) | 3B20 + password register BA0F (local by design), 1910 (password solved) | Entry to the ship; bombs; Niko's challenge | 1910 yes | At the door | Each player overhears their own random password; once anyone gets in, everyone's door skips it |
| Windmill / lighthouse (traced, `d_a_obj_ferris`) | 2104 | (Kreeb's heart above) | Yes | Reload (the wheel reads its state as it runs) | |
| Tott's dance (by mechanism) | Song of Passing (item) | Song | Yes | Live | |
| Windfall residents' rupee gifts (catalogue) | 2402-2410 | Rupees | Yes | At talk | Paid once per room |

### Dragon Roost Island

| Quest | Progress | Reward | Shared? | Live? | Notes |
|---|---|---|---|---|---|
| Koboli's mail sorting (traced, `d_a_npc_bmsw`) | Level register **C203** (1-3), 1A01, 1A02; record 8AFF (local) | Rupees per round; the Note to Mom route | Bits yes; **level no** | At talk | **Gap 3.3**: every player starts at level 1 |
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
| **Ghost Ship** (traced, `d_a_tag_ghostship`, `d_a_ghostship.cpp:275`, `d_menu_fmap.cpp:2537`) | Register **8803** (0-3; 3 = cleared); its chest (Shared world) | Triforce Chart | Chart and chest yes; **cleared no** | Chest live | **Gap 3.3**: the others still see the ship and its sea chart icon after it's cleared; its chest is empty for them, so nothing duplicates |
| Big Octos, sunken treasure, treasure charts | Salvage bits, chart bits (Shared world / items) | Rupees, hearts, charts | Yes | **Live** (live-world.md §0.4) | |
| Savage Labyrinth, caves, submarines, platforms | Chests and switches (Shared world) | Items, hearts | Yes | Live (chests) | Enemies are never shared: each player clears their own |
| Bird-Man Contest (hearts.md) | 2B40 (first prize); record BEFF (local) | Piece of Heart | Yes | At talk | High score per player |
| Barrel shooting, Spectacle Island (hearts.md) | Prizes-won register **B703** | Piece of Heart, then Treasure Chart | Heart and chart yes; **tier no** | — | **Gap 3.3**, like the Battlesquid |
| Private Oasis / Cabana (by mechanism) | Cabana Deed (delivery bag), maze chest (Shared world) | Entry; the maze's prize | Yes | Bag live | |
| Nintendo Gallery ([figurines.md](figurines.md)) | Figurine registers (Shared story) | Figurines | Yes | Pedestals on the next gallery room load | Photos per player |
| Mail ([event-flags.md](event-flags.md)) | Letter registers (Shared story) | Charts, hearts, tickets, rupees | Yes | Mail on your next postbox visit | Two players reading the same letter at once both get it |

## 3. Gaps and fixes

### 3.1 Mrs. Marie's pendant count (real bug)

`daNpc_Ho_c` keeps the pendants handed over in register C0FF (`receivePendant`, `d_a_npc_ho.cpp:70-82`) and chooses her talk from it (`:405-420`): at 0 she runs the first-time talk, takes 20 pendants and gives the Cabana Deed, whatever 1C08 says. With Shared spoils bag on:
- P1 hands over 20: P1's C0FF = 20, 1C08 and the Cabana Deed reach everyone.
- P2 talks to her: P2's C0FF is still 0, so she takes **20 more from the shared bag** and gives a Cabana Deed the room already holds (the delivery bag keeps one).
- The Hero's Charm (40 given) needs 40 from one player alone.

**Fix:** sync C0FF under Shared spoils bag (or Shared story), merged by MAX, applied while idle like Beedle's points. MAX is right when one player hands in at a time (the bag is one stock, so that is the normal case); two players handing in at the same moment would count only the larger total. A protocol bump.

### 3.2 The Town Flower pedestals

Each of Windfall's pedestals keeps the item on it in its own register (`daDai_c::m_savelabel`, D1FF-F8FF, `d_a_dai.cpp:201, 320`), local by design (an item number, not a bitfield). The flower comes out of the shared delivery bag, but the pedestal shows it only in the game of the player who put it there.

**Fix options:** a pedestal store under Shared delivery bag (one item per pedestal, set and take-back as deltas, like the bag), applied while idle and re-created by the REL if a pedestal should update live; or, cheaper, a note in the docs: one player decorates the whole town.

### 3.3 Levels and prize tiers kept in registers

| Register | Quest | What goes wrong |
|---|---|---|
| D003 | Orca's lessons | The next player starts at level 0; his 500-hit piece is paid again, then taken back |
| C203 | Koboli's mail sorting | Every player starts at level 1 |
| FE07 | Sploosh Kaboom | The next player's first win pays the heart again (taken back) instead of the chart |
| B703 | Barrel shooting | Same |
| 8803 | Ghost Ship | Still there for the others after it's cleared |

The catalogue already marks D003, C203 and 8803 **Max**; FE07 and B703 are counters that only go up (hearts.md §3.3). **Fix:** carry them in the room story merged by MAX (value masks 0x03 / 0x07), like `StoryFlags.BeedlePoints`, applied while idle. A protocol bump; no game code.

### 3.4 Rose's pig pen

BFFF is a bitmask of the pigs in the pen (`d_a_kb.cpp:2170-2175`, OR'd in). **Fix:** carry it OR-merged with the others in 3.3. Without it, each player can pen the pigs and be paid (into the shared wallet).

### 3.5 Unverified

- Tingle's statue payments: whether each statue pays once per room, or once per player (a shared wallet would then receive it once for each player).
- The withered trees: whether a tree one player watered stays watered for the others.
- Maggie's father, Hoskit and Doc Bandam gate on counts or flags inside RELs that aren't decompiled.

### 3.6 In-game checklist

| # | Do | Expect |
|---|---|---|
| Q1 | P1 gives Mrs. Marie 20 pendants; P2 talks to her with pendants in the room's bag | Today: she takes 20 more (bug 3.1). After the fix: she asks for more pendants towards the charm |
| Q2 | P1 wins Sploosh Kaboom once; P2 wins once | Today: P2 is shown a heart that is then taken away. After the fix: P2 wins the chart |
| Q3 | P1 clears the Ghost Ship; P2 opens the sea chart | Today: the icon is still there. After the fix: gone |
| Q4 | P1 frees Tingle; P2 is on Tingle Island | P2 sees him freed after leaving and coming back |
| Q5 | P1 collects a Tingle Statue; P2 talks to Tingle on Tingle Island after a reload | The statue is there for P2; note what the wallet does (3.5) |
| Q6 | P1 sets a Town Flower on a pedestal | Gone from both bags; only P1's pedestal shows it (3.2) |
