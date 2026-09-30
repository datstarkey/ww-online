# Derived max health (Pieces of Heart and Heart Containers)

With **Shared items** on, a player's max health is no longer max-merged between players: it is derived from the heart sources anyone in the room has taken, each counted once, in the spirit of the small keys (`docs/small-keys.md`). §0 is what is built, §1 the catalogue (tick the list off in game), §2 the boss containers, §3 the research notes.

Target: GZLE01 (the GameCube game). Source: `tww-decomp/` (paths below are relative to it unless they start with a repo folder).

## 0. What is built

### The bug

`RoomInventory.MaxHealth` was a MAX merge. When two players get the same piece independently (P1 gets it and the room goes 13 → 14, P2 adopts 14, then P2 also gets the piece because its flag hadn't synced yet, or the reward isn't flag-gated for them: 15), the room counts it twice, and everyone ends up with too many hearts.

### The rule

> **max health = 12 (the 3 starting hearts) + 4 × Heart Containers in the room's source set + 1 × Pieces of Heart in the room's source set**

in quarter hearts. The vanilla game has 44 pieces and 6 containers: 12 + 44 + 24 = 80 = 20 hearts (`RoomInventory.MaxHealthLimit`, and d_meter's own clamp at 0x50).

**The room's source set** is `RoomInventory.HeartSources`, a grow-only 50-bit set: bit N = catalogue source N (`HeartCatalog.SourceIds`: the 34 stage-file sources in `HeartCatalog.StageSources` order, then the 16 rewards; the IDs are the wire format, so they are never reordered, only appended). Every client computes which sources its **own** game's flags show taken (`HeartTable.SourceBits`: chests, items, bosses, event bits, registers, charts, ocean bits, Maggie) and sends them like any other gain; the server OR-merges them (`RoomInventory.MergeGainsFrom`) and pushes the room to everyone. A source counts when it is in the room's set or its flag is set in this game (`HeartTable.Tally(flags, roomSources)`), once either way. The set is keyed by source, so it is idempotent: a piece two players both take is one bit. The flags themselves still don't sync because of this (only the count uses the set), so the count doesn't depend on Shared world or Shared story. **Protocol 6** (`HubConstants.ProtocolVersion`: `RoomInventory` gained a field). The server rejects a set with bits above 49 (`RoomInventory.IsValid`) and `Normalize` strips them.

**The heart table** (`WWOnline.Patcher/WorldData/HeartTable.cs`, `HeartTableBuilder.cs`, `HeartCatalog.cs`) is built like the small-key table: chests, placed and dug-up items, salvage points and boss containers come from the player's own stage files (`StageDataReader` + main.dol's object-name table), at runtime (`HeartTableProvider`, a `StageTableProvider<T>` like `SmallKeyTableProvider`, about 0.3 s on a background thread). Nothing built from game files is written anywhere or committed. `HeartCatalog` is our own hand-written data: the 16 NPC / minigame / letter rewards and their flags, the names of the 34 stage-file sources (by flag), and the three unreachable stages the builder skips (§3.4).

**When it applies** (`SharedHeartService.RuleApplies`): connected, **Shared items** on, and the heart table built (`DerivedHeartsState.OwnsMaxHealth`). Nothing is written until the room's inventory is known (joined), so a reconnect, which clears it, never takes hearts away. Otherwise every game keeps its own max health.

All 50 sources are shared this way, whatever Shared world and Shared story say. Those rules still share the flags that some sources use (world flags: chests open empty for everyone; event bits: the NPC remembers), which keeps a second player from taking the same source; when they are off, the second player can take it again and the duplicate is taken back (below).

**Writing it** (`SharedHeartService` at 4 Hz, `HeartReconciler`, `HeartMemory`). Only in a settled scene (`SceneStabilityGate`), and only while the game is idle: no max-life change queued (`play.mItemMaxLifeCount` = 0), no event (`mEvtCtrl.mMode` = 0: a get-item demo, a reward's talk), no pause menu, no minigame (`play.mMiniGameType` = 0). A rise waits 2 idle ticks (0.5 s), a fall 8 (2 s): a pickup adds its max life during its demo and a few rewards set their flag at the very end of it (a letter's READ state, the withered trees' flag), so the game always finishes its own change first.
- The change is queued the way a Piece of Heart does it: `play.mItemMaxLifeCount` += delta (`GameMemoryAddresses.Hearts.PendingMaxLife`, only while it is 0, re-checked just before the write). `dMeter_LifeMove` (`src/d/d_meter.cpp:1405-1433`) adds it to mMaxLife clamped to 0..80, animates the heart row, refills health on a gain (exactly what a picked-up piece does) and **clamps current health** on a loss. mMaxLife is never written directly: that would leave the HUD's heart count stale until the next stage load (the HUD's own count, `i_Meter->mMaxHP`, is loaded from mMaxLife only in `dMeter_heartInit`, `d_meter.cpp:1394`).
- One change at a time: after a write, nothing is written until it shows in mMaxLife (or 2 s pass).

**A heart no source explains** (the same piece given twice, or a source the catalogue doesn't know, e.g. a randomizer): the game's max rises with no source added (by a flag here or the room's set) in the last 10 s. It is logged loudly (`[hearts] max health rose 13 → 14 in stage sea room 11 with no heart source added ...`, a Warning) and **not kept**: after 2 idle seconds max health goes back to the derived value. Small keys keep an unknown-source key for the session; hearts can't, because the bug this fixes (a reward that isn't flag-gated for the second player) looks exactly like an unknown source.

**Correcting existing saves.** On the first tick with the rule on, the save is reported (`[hearts] this save has max health 15 (3 3/4 hearts); its flags and the room's heart sources derive 14 (...) — correcting it down to 14 once the game is idle`), then written like any other change (`[hearts] max health 15 → 14 (...), current health clamped by the game`). A save that got its hearts legitimately has every flag set, so it derives at least the same value (plus the room's other sources).

**Shared items** (`RoomInventorySyncService`): each tick's local state carries `IMaxHealthOwner.LocalHeartSources` (written by `SharedHeartService` into `DerivedHeartsState`), so new sources go out as gains and a join sends them all. While `OwnsMaxHealth`, the room's `MaxHealth` is never applied (the game keeps its own, which `SharedHeartService` sets), a rise is never sent as a gain, and a join sends the derived value, so the room's field stays a real max health (the room summary).

**Room items page** (`RoomItemsViewModel`, "Hearts and magic" card): the max hearts shown are the derived value while it applies, the − / + buttons are disabled (there is nothing to edit: the flags are the state), and a line reads **"Hearts: X of 6 containers, Y of 44 pieces"** (the room's count while it applies, else this game's own flags).

**Magic** follows the same idea, without a catalogue: max magic is 0, 16 with the Deku Leaf, 32 once the Great Fairy who doubles it has (event 0x3180, set when her event ends: daBigelf type 6's `getEventFlag`) (`RoomInventory.MagicFromFlags`). `RoomInventoryMemory.Read` reports that instead of the game's byte, so it is what joins, gains and the max-merge see; `Apply` compares the room's value with the game's own byte. The game **adds** 16 on every Deku Leaf pickup (`item_func_deku_leaf`, `d_item.cpp:810-815`), so a player who picked one up after the room had shared the meter went to 32 (2026-09-29). `RoomInventorySyncService.ClampMaxMagic` puts the game's byte back to the larger of the flags' and the room's value every tick (idle, no event, no gain queued for the meter). A room that already holds 32 from that bug keeps it until the owner sets magic back to Normal on the Room items page.

**Log lines:** `[hearts]` (client log). The table's summary when it is built (`44 piece(s), 6 container(s): 12 Chest, 2 PlacedItem, 14 Salvage, 6 Boss, 16 Reward`), a warning if a table doesn't have 44 and 6, and every untracked heart.

## 1. The catalogue (vanilla GZLE01)

Checked by `HeartTableTests.VanillaGame_Has44PiecesAnd6Containers_AndEveryFlagDerives20Hearts` (skipped without a vanilla game): exactly 44 pieces and 6 containers, no two sources on one flag, no heart the save can't record, no heart flag shared with another chest or item, the stage-file part exactly the 34 named below, each source alone adding its own quarters, and every flag set deriving 80.

"Evidence": **D** = read in tww-decomp; **R** = read in the vanilla REL's code (the actor isn't decompiled; disassembled with its relocations resolved against `config/GZLE01/symbols.txt`); **S** = found in the vanilla stage files by the builder; **AP** = the same flag in the Archipelago TWW client's location table (`worlds/tww/Locations.py` / `TWWClient.py`, an autotracker that reads these flags from a running game). Every location is in the Wind Waker Randomizer's `logic/item_locations.txt` (its 44 "Piece of Heart" and 6 "Heart Container" entries are exactly these 50).

### Chests (12): `daTbox_c`, tbox bit of the stage's save slot

| # | Location | Flag | Stage | Evidence |
|---|---|---|---|---|
| 1 | Outset Island - Savage Labyrinth - Floor 50 | slot 13 tbox 12 | Cave10 room 20 | S, AP |
| 2 | Windfall Island - Transparent Chest | slot 0 tbox 10 | sea room 11 (layers 1, 3, 5) | S, AP |
| 3 | Greatfish Isle - Hidden Chest | slot 0 tbox 6 | sea room 23 | S, AP |
| 4 | Forsaken Fortress - Chest Inside Lower Jail Cell | slot 2 tbox 3 | majroom / ma2room / ma3room | S, AP |
| 5 | Needle Rock Isle - Chest | slot 0 tbox 3 | sea room 29 | S, AP |
| 6 | Angular Isles - Peak | slot 0 tbox 0 | sea room 47 | S, AP |
| 7 | Stone Watcher Island - Lookout Platform - Destroy the Cannons | slot 0 tbox 20 | sea room 31 | S, AP |
| 8 | Pawprint Isle - Chuchu Cave - Chest | slot 12 tbox 26 | TyuTyu | S, AP |
| 9 | Bomb Island - Cave | slot 12 tbox 5 | Cave01 | S, AP |
| 10 | Star Island - Cave | slot 12 tbox 6 | Cave02 | S, AP |
| 11 | Five-Star Isles - Submarine | slot 10 tbox 1 | Abship room 0 | S, AP |
| 12 | Six-Eye Reef - Submarine | slot 10 tbox 0 | Abship room 1 | S, AP |

### Placed and dug-up items (2): memory item bit

| # | Location | Flag | Actor | Evidence |
|---|---|---|---|---|
| 13 | Outset Island - Dig up Black Soil | slot 0 item bit 2 | `daTagKbItem_c` (TagKb, sea room 44): the item the pig digs up | S, D (`d_a_tag_kb_item.cpp:9-13`), AP |
| 14 | Headstone Island - Top of the Island | slot 0 item bit 8 | `daItem_c` (sea room 45) | S, AP |

### Salvage points (14): `daSalvage_c`

| # | Location | Flag | Actor | Evidence |
|---|---|---|---|---|
| 15 | Tingle Island - Big Octo | ocean grid 17 bit 0 | SwSlvg (kind 2, appears on the octo's switch 0x0E), sea room 17 | S, AP |
| 16 | Seven-Star Isles - Big Octo | ocean grid 6 bit 0 | SwSlvg (switch 0x0D), sea room 6 | S, AP |
| 17 | Rock Spire Isle - Southeast Gunboat | ocean grid 16 bit 0 | SwSlvg (switch 0x13), sea room 16 | S, AP |
| 18 | Crescent Moon Island - Sunken Treasure | chart 9 salvaged | Salvage kind 0, sea room 5 | S, AP |
| 19 | Pawprint Isle - Sunken Treasure | chart 11 salvaged | sea room 12 | S, AP |
| 20 | Rock Spire Isle - Sunken Treasure | chart 17 salvaged | sea room 16 | S, AP |
| 21 | Three-Eye Reef - Sunken Treasure | chart 18 salvaged | sea room 22 | S, AP |
| 22 | Thorned Fairy Island - Sunken Treasure | chart 13 salvaged | sea room 28 | S, AP |
| 23 | Bomb Island - Sunken Treasure | chart 12 salvaged | sea room 34 | S, AP |
| 24 | Diamond Steppe Island - Sunken Treasure | chart 14 salvaged | sea room 36 | S, AP |
| 25 | Southern Fairy Island - Sunken Treasure | chart 30 salvaged | sea room 39 | S, AP |
| 26 | Forest Haven - Sunken Treasure | chart 15 salvaged | sea room 41 | S, AP |
| 27 | Angular Isles - Sunken Treasure | chart 10 salvaged | sea room 47 | S, AP |
| 28 | Five-Star Isles - Sunken Treasure | chart 16 salvaged | sea room 49 | S, AP |

("chart N" is the salvage point's save number, `dComIfGs_isCompleteCollectMap(N)`; AP's bit is 32 + N - 1 in the same 8 bytes.)

### Boss Heart Containers (6): mDungeonItem STAGE_LIFE of the dungeon's slot (§2)

| # | Location | Flag | Evidence |
|---|---|---|---|
| 29 | Dragon Roost Cavern - Gohma Heart Container | slot 3 STAGE_LIFE | S (Bitem in M_DragB), D |
| 30 | Forbidden Woods - Kalle Demos Heart Container | slot 4 STAGE_LIFE | S (kinBOSS), D |
| 31 | Tower of the Gods - Gohdan Heart Container | slot 5 STAGE_LIFE | S (SirenB), D |
| 32 | Forsaken Fortress - Helmaroc King Heart Container | slot 2 STAGE_LIFE | S (M2tower, and sea room 1), D |
| 33 | Earth Temple - Jalhalla Heart Container | slot 6 STAGE_LIFE | S (M_DaiB), D |
| 34 | Wind Temple - Molgera Heart Container | slot 7 STAGE_LIFE | S (kazeB), D |

### NPC, minigame and letter rewards (16): `HeartCatalog.Rewards`

| # | Location | Flag | Where it is set | Evidence |
|---|---|---|---|---|
| 35 | Outset Island - Orca - Hit 500 Times | event 0x0F10 | `d_a_npc_ji1.cpp:1517-1519` createItem: the heart and the bit together | D |
| 36 | Windfall Island - Ivan - Catch Killer Bees | event 0x1340 | `d_a_npc_mk.cpp:819-825` | D, AP |
| 37 | Windfall Island - Maggie - Delivery Reward | Moblin's Letter delivered (§3.3) | `d_a_npc_kp1` GET_KAKERA_HRT | R, AP (the same workaround) |
| 38 | Windfall Island - Kreeb - Light Up Lighthouse | event 0x1B20 | `d_a_npc_people.cpp:7060-7062` (NPC_UM1, l_msg_um1_get_item) | D, AP |
| 39 | Windfall Island - 80 Rupee Auction | event 0x1020 | `d_a_auction.cpp:54, 1204-1210` | D, AP |
| 40 | Windfall Island - Sam - Decorate the Town | event 0x1B10 | `d_a_npc_people.cpp:6880-6882` (NPC_UO1, all flower stands) | D, AP |
| 41 | Windfall Island - Battlesquid - First Prize | register 0xFE07 >= 1 | `d_a_npc_kg1` next_msgStatus adds 1 (cap 3), then wait_action gives prize table[reg - 1] = {heart, chart, rupee} | R, AP (bit 0 of byte 0xFE) |
| 42 | Windfall Island - Linda and Anton | event 0x2280 | `d_a_npc_people.cpp:7032-7034` (NPC_UW2) | D, AP |
| 43 | Mailbox - Letter from Hoskit's Girlfriend | register 0xAE03 = 3 (READ) | `d_a_obj_toripost.cpp:50, 802` + `d_letter.cpp` | D, AP |
| 44 | Mailbox - Letter from Baito's Mother | register 0xAC03 = 3 (READ) | `d_a_obj_toripost.cpp:40, 802` | D, AP |
| 45 | Mailbox - Letter from Komali's Father | register 0xB503 = 3 (READ) | `d_a_obj_toripost.cpp:41, 802` | D, AP |
| 46 | The Great Sea - Goron Trading Reward | event 0x3E04 | `d_a_npc_roten.cpp:2499-2503` (the last trade) | D, AP |
| 47 | The Great Sea - Withered Trees | event 0x2E20 | `d_a_obj_ftree` search_heart: set once the heart it dropped (fastCreateItem, item 7) is gone | R, AP |
| 48 | Spectacle Island - Barrel Shooting - First Prize | register 0xB703 >= 1 | `d_a_npc_kg2` adds 1 at the end of KG2_CLEAR_DEMO, then KG2_GETDEMO's CREATEITEM gives {heart at 1, chart at 2, rupee at 3} | R, AP (bit 0 of byte 0xB7) |
| 49 | Rock Spire Isle - Beedle's Special Shop Ship - 950 Rupee Item | event 0x2010 | `d_a_npc_bs1.cpp:903-905` (bought) | D, AP |
| 50 | Flight Control Platform - Bird-Man Contest - First Prize | event 0x2B40 | `d_a_npc_bmcon1`: first prize and 0x2B40 not set → set it, prize message; set → the repeat prize | R, AP |

**Cross-check with a public list.** Thonky's list (<https://www.thonky.com/zelda-wind-waker/heart-pieces>) has 44 entries; 43 match the rows above (its "mail sorting" and "20 golden feathers" entries are rows 44 and 43, whose pieces arrive by mail). It lists "give Maggie's father 20 Skull Necklaces" and has no Rock Spire sunken treasure; in the GameCube data Maggie's father's event gives item 0xEE, a treasure chart (Orichh `Gp1_Get_Itm`, `011get_item prm0=EE`), and Rock Spire's salvage point holds item 0x07, so the list above stands. (zeldadungeon.net refused the fetch.)

## 2. Boss Heart Containers

A boss has two flags in its dungeon's `mDungeonItem`: bit 3 STAGE_BOSS_ENEMY (the boss is dead) and bit 4 STAGE_LIFE (`d_save.h:643-644`). **STAGE_LIFE is the one that counts**, because only `item_func_utuwa_heart` sets it (`src/d/d_item.cpp:587-603`), and that runs only when the Heart Container is picked up (execItemGet). The boss sets bit 3 when it dies and drops the container (`fopAcM_createDisappear(..., daDisItem_HEART_CONTAINER_e)` in `d_a_btd.cpp:1084`, `d_a_bmd.cpp:747`, `d_a_bpw.cpp:2856`, and the other bosses' equivalents).

**Beaten but not collected** (warped out, or the game was reset before the pickup): bit 3 set, bit 4 not. `daBossItem_c` (the `Bitem` actor in each boss room, parameter = the dungeon's slot) re-creates the container on the next visit while `isStageBossEnemy && !isStageLife` (`d_a_boss_item.cpp:25-40`). The derivation counts nothing until someone picks it up, so a peer who never collected it doesn't get the heart early, and once anyone does, its source ID joins the room's set and it counts for everyone. With Shared world on, STAGE_LIFE also syncs (`mDungeonItem` is OR-merged), so the other players' Bitem no longer spawns it, and a container already waiting on another player's screen is removed live by the REL (`puppet_worldsync.c`, [side-quests.md](side-quests.md) §3.5; a re-patch); without it, a second pickup is logged and taken back.

**Helmaroc King** is the one container that can be collected outside its dungeon: if it isn't taken in M2tower, `Bitem` also stands on the sea outside Forsaken Fortress (sea room 1), and `item_func_utuwa_heart` then sets STAGE_LIFE of STAGE_FF (slot 2) explicitly (`d_item.cpp:596-599`), the same flag. No container comes from anything but a boss in the vanilla game (the builder found no chest, item or salvage point with item 0x08).

## 3. Research notes

### 3.1 Where max health lives, and the game's own change

| What | Decomp | Address |
|---|---|---|
| mMaxLife, mLife (u16, quarter hearts) | `dSv_player_status_a_c` /* 0x0 */, /* 0x2 */ | 0x803C4C08, 0x803C4C0A |
| Pending max-life change | `dComIfG_play_c /* 0x48D6 */ s16 mItemMaxLifeCount` | Play + 0x48D6 = 0x803CA77E. **Verified**: `lha / sth 0x5B76(gameInfo)` in item_func_kakera_heart (0x800C2F30) and item_func_utuwa_heart (0x800C2F54) |
| A piece / a container | `item_func_kakera_heart`: +1; `item_func_utuwa_heart`: +4, fill life, STAGE_LIFE (`d_item.cpp:582-603`) | |

dItemNo_HEART_PIECE_e 0x07, dItemNo_HEART_CONTAINER_e 0x08 and dItemNo_HEART_PIECE_ALT_e 0x3F (the same function; Hoskit's girlfriend's letter uses it for its message). All pinned by `GameInfoAddressTests.HeartAddresses_MatchTheVanillaDol`.

### 3.2 The save flags the catalogue reads (`GameMemoryAddresses.Hearts`, one read of gameInfo + 0..0x724)

| Flag | Where | Set by |
|---|---|---|
| tbox / item / STAGE_LIFE | `dSv_save_c::mMemory[slot]` (gameInfo + 0x380 + slot × 0x24), the current slot from the live copy (gameInfo + 0x778) | chests (`OpenInit_com`, `d_a_tbox.cpp:850-858`: function types 7/8 save to STAGE_SEA2), items (`dSv_info_c::onItem`, memory bits < 0x20), `item_func_utuwa_heart` |
| Chart salvaged | `dSv_player_map_c field_0x0[3]` (gameInfo + 0xC4 + 0x30; `isCompleteMap(no - 1)`, `d_save.cpp:894`) | `daSalvage_c::end_salvage` kind 0 (`d_a_salvage.cpp:510`) |
| Ocean bit | `dSv_ocean_c` u16[50] at gameInfo + 0x5C0 (`d_save.h:910`) | `end_salvage` kinds 2-4 (`:517`), grid = the salvage point's room |
| Event bit / register | `dSv_event_c` at gameInfo + 0x624; `getEventReg` = byte & mask (`d_save.cpp:1207-1210`) | the actors in §1 |
| Moblin's Letter | `dSv_player_get_bag_item_c::mReserveFlags` bit 15 (gameInfo + 0x90) and the delivery bag `mReserve[8]` (gameInfo + 0x86) | item get; Maggie takes the letter |

Salvage parameters (`d_salvage.cpp:12-19`): kind `prm >> 28`, save number `prm >> 20 & 0xFF`, item `prm >> 4 & 0xFF`, room `prm >> 12`; `angle.z & 3` picks one of a sunken treasure's four copies (only the one matching `mRandomSalvagePoint` is active), `angle.z & 0xFF` is a kind-2 point's switch.

### 3.3 Rewards whose flag isn't an obvious event bit

- **Letters** (`d_letter.cpp`): a letter's register goes NOSEND 0 → SEND 1 → STOCK 2 → READ 3. The mailbox gives the letter's item in its receive event and sets READ when that event ends (`d_a_obj_toripost.cpp:800-802`), so READ = the piece was received.
- **Maggie** (`d_a_npc_kp1`, not decompiled): she picks her dialogue from `isReserve(15)` (Moblin's Letter ever obtained) and `checkReserveItem(0x9B)` (still in the bag); showing her the letter runs GET_KAKERA_HRT (ActNo 1 → item 7), and the letter leaves the bag. Nothing else takes it. Register 0xCCFF is only reset to 0 at the end of that event, so it isn't a record. The Archipelago client uses the same "owned once and gone" check.
- **Battlesquid / barrel shooting** (`d_a_npc_kg1` / `kg2`): the registers count first prizes won (capped at 3) and index the prize table, so ">= 1" = the first prize (the heart) was given. Both increment just before the prize is created.
- **Withered trees** (`d_a_obj_ftree`): the last watered tree drops a field Piece of Heart (item bit -1); `search_heart` sets 0x2E20 once that item actor is gone (picked up). Leaving before picking it up leaves the flag clear and the tree drops it again next time.
- **Kreeb, Sam, Linda and Anton** (`d_a_npc_people`): the flag is set when the NPC picks the reward message; the item comes from the event's `ItemNo` (`l_get_item_no[0]`, `[3]`, `[4]`: the randomizer's rel offsets 0xC54B, 0xC557, 0xC55B).
- **Orca**: `createItem` gives the heart and sets 0x0F10 in the same branch; reaching 500 hits again after 0x0F10 synced from a peer (the level register 0xD003 isn't synced) can give a second piece, which the derivation takes back (§0).

### 3.4 Stages left out

The builder skips STAGE_TEST (slot 15: `Amos_T`, `I_TestM`, `K_Test*` hold heart chests and items) and three unreachable stages (`HeartCatalog.UnreachableStages`):
- `Cave06`: a heart chest (slot 12 tbox 18). Its only entrance is in `sea_E`, the unused E3-demo sea.
- `I_SubAN`: a heart chest (slot 12 tbox 7) in a developer sub-dungeon. Sea room 47 (Angular Isles) lists it as an exit, but Angular Isles' cave is `SubD43`, and no location list has it.
- `DmSpot0`: an unused copy of Outset in the sea's slot (no exit leads there). Its rupee uses slot 0 item bit 2, the black soil heart's flag; the builder reports any heart flag another reachable chest or item also sets, and in vanilla there is none once `DmSpot0` is left out.

### 3.5 Other things checked

- No event list gives a heart through Link's `011get_item prm0=07/08/3F` cut (every stage's `event_list.dat` was searched), so no reward is hidden in event data.
- Every tbox bit and item bit of a heart source is used by no other reachable chest or item in the same slot (the vanilla test asserts `Untracked` is empty).

## 4. Limitations and follow-ups

### 4.1 Per-player sources (resolved)

The first version derived the count from this game's flags only, so the 20 sources whose flags never sync (registers, charts, ocean bits, the delivery bag) counted only for the player who got them. The room's grow-only source set (§0, protocol 6) shares all 50.

### 4.2 In-game checklist (dev-test, Full sync) — **unverified**

| # | Do | Expect |
|---|---|---|
| H1 | P1 opens the Windfall transparent chest (or Headstone's piece) | P1: the piece lands in the demo, no `[hearts]` write. P2: `[hearts] max health N → N+1`, the heart row animates and health refills |
| H2 | P1 gets Ivan's piece; then P2 plays hide-and-seek too | P2 gets no second piece in the dialogue; if the game gives one anyway, `[hearts] max health rose ... given twice` (Warning), then back to the derived value |
| H3 | P1 beats Gohma and picks up the container | Both: +4 (P2 via `[hearts]`); P2 entering Gohma's room finds no container |
| H4 | P1 beats Gohma and warps out without the container | Nobody gets +4; the container is still there on the next visit |
| H5 | A save with a double-counted piece joins | `[hearts] this save has max health 15 ...; its flags derive 14 ... correcting it down`, then `max health 15 → 14`, current health clamped |
| H6 | The owner turns Shared items off, then on | `[hearts] derived max health off`, then on again with the save reported |
| H7 | Room items page | "Hearts: X of 6 containers, Y of 44 pieces"; max hearts − / + disabled while derived |
| H8 | P1 reads the Baito's mother letter (or salvages a sunken treasure) | Both: +1 (P1 from the flag, P2 from the room's set: `[items] ... heart sources`) |
| H9 | Shared world off, P1 and P2 open the same heart chest | One piece for the room: P2's second one is logged and taken back |
