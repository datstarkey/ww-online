# Event flags: what can be shared under "story sync"

Catalog: `WWOnline.Shared/Models/EventFlagCatalog.cs`. Target: GZLE01. Source: the zeldaret/tww decomp in `tww-decomp/`.

## Method

1. Parsed `include/d/d_save_event_flag.inc`, the only flag table in the decomp. It defines 487 named event bits and 131 named event registers (all names except about 20 are `UNK_xxxx`).
2. Extracted every call to `dComIfGs_onEventBit`, `offEventBit`, `isEventBit`, `setEventReg` and `getEventReg`, plus `JAIZelBasic::checkEventBit` (audio), across all of `src/`. That is 1,459 call sites.
   - 98 of those calls pass a table entry or a variable instead of a constant. These were traced by hand: `daSalvage_c::m_savelabel`, `daDai_c::m_savelabel`, `l_figure_comp`, `daObj_Warpt_c::m_event_reg`, the auction/Lenzo/merchant/`npc_people` save tables, `M_door_ev_table`, `daObjMknjD` m0430, `demo00 l_eventBit`, and `d_letter`.
   - The event system sets flags from data through `dEvDtStaff_c::specialProcPackage` (`d_event_data.cpp:800`, the demo's `EventFlag` integer). That is where the "Set by X.stb" comments in the enum come from.
3. Each flag was classified by who sets it, who clears it, and what reads it. `revEventBit` does not exist for save event bits. `revSwitch` is for stage switches only.
4. **Limitation:** 116 named bits have no reference anywhere in `src/`. They are set and tested only by stage, event-list or message data. 112 of them are `Unknown`, and 4 are classified from their `.stb` comment. None of them is ever cleared, because every `offEventBit` call is in `src/`. So OR-merging them is monotonic. Their only risk is a missing side effect.

## Storage (confirmed)

| What | Where | Notes |
|---|---|---|
| `dSv_save_c::mEvent` (`dSv_event_c`, `u8 mFlags[0x100]`) | `g_dComIfG_gameInfo` 0x803C4C08 + 0x624 = **0x803C522C**, 256 bytes | `STATIC_ASSERT(sizeof(dSv_event_c)==0x100)`. The memory-card copy is packed, at +0x618 inside the save. |
| Bit flags | bytes **0x00-0x41** | `id = byte<<8 \| mask`, `mFlags[id>>8] \|= id&0xFF` (`d_save.cpp:1187`) |
| No named flag | bytes 0x42-0x78 | Mask = 0. The enum appears to have been dumped from game data, so these bytes are most likely unused. |
| Event registers | bytes **0x79-0xFF** | `setEventReg`: `mFlags[b] &= ~mask; mFlags[b] \|= value` (`d_save.cpp:1202`). These hold multi-bit values and must **not** be OR-merged. |
| `dSv_info_c::mTmp` | +0x1158 = 0x803C5D60 | A second `dSv_event_c` holding per-session temporary bits and registers (`d_save_event_tmp_flag.inc`). It sits outside `mSavedata`, so it is never written to the card. It is re-initialised by `dSv_info_c::init` (`d_save.cpp:1394`), and many bits are cleared daily in `dKankyo_DayProc`. `npc_people` uses its registers for a minigame. **Do not sync it.** |

`setInitEventBit` (`d_save_init.cpp:12`) seeds registers 0xBE and 0x7E on a new file. `dSv_info_c::reinit` (New Game+, `d_save.cpp:1401`) keeps the figurine registers and forces bits 2F08/2F04/2F02/3A01/3401 on.

## Counts

| Category | Bits | Full sync |
|---|---|---|
| Story | 84 (6 marked risky) | yes |
| CutsceneSeen | 35 | yes |
| SideQuest | 31 | yes |
| Collectible | 19 | yes |
| NpcState | 110 | yes |
| TutorialHint | 33 | yes |
| Unknown | 138 (112 with no src reference; 2 risky) | yes, except the 2 risky ones |
| **LocalOnly** | **37** | **never** |

`GetFullSyncMask()` covers 448 bits across 66 bytes, all in the range 0x00-0x41. Every register byte is 0 in the mask.

## LocalOnly (37)

| Flag(s) | Evidence | Why OR-merge is wrong |
|---|---|---|
| 1304 1302 1301 | `d_kankyo_dayproc.inc:35-37` `offEventBit` every new day | "Traded with merchant today". Latching it blocks the trade permanently. |
| 2680 | `dayproc.inc:73` and `d_a_npc_people.cpp:7266` | Daily flag, also toggled in dialogue. |
| 2080 2004 2002 2804 2802 2801 2980 2940 3B01 3C80 3C40 3C20 3C10 3C08 3C04 3C02 | `dayproc.inc:84-99` (weekly, when day-of-week is 5) | Moonlight salvage points (`daSalvage_c::m_savelabel`, `d_com_static.cpp:115`). Latching them disables those salvages forever. |
| 2F01 3080 3F01 4080 4040 | `d_a_npc_mt.cpp:398-402, 567-571, 961-964, 1002, 1013-1016`; 3080 is also set by `dayproc.inc:104` | Carlov's "figurine in progress / ready / collected" cycle. Latching it wedges the gallery. |
| 4008 | `d_a_auction.cpp:1428/1432` | Toggled on or off after each auction. |
| 4020 | set `d_a_player_grab.inc:481`, cleared `d_a_tag_hint.cpp:733` | Transient hint state. |
| 3410 | cleared `d_a_tag_md_cb.cpp:61` on create | Companion trigger is re-armed every visit. |
| 1140 | cleared `d_a_mdoor.cpp:141` when 1101 is off | Door demo state. |
| 3820 MOVED_HYRULE_STATUE | `d_a_obj_YLzou.cpp:143` recomputes it on or off from story state every load | A latched "moved" value triggers the stairs demo out of order. |
| 0A02 ENDLESS_NIGHT | never cleared, but `dKy_checkEventNightStop` (`d_kankyo.cpp:3162`) freezes the clock at night until the Nayru's Pearl symbol is owned | Controls the local player's time of day. It also changes KoRL behaviour and switches the Windfall `tag_event` into hunt mode (`d_a_tag_event.cpp:297`). |
| 2A08 RODE_KORL | `d_stage.cpp:1933` and `d_a_player_main.cpp:10869` force the ship onto dock spawn points until it is set; it is also the first entry in `dComIfGs_setGameStartStage` | Controls the local ship and spawn position. A peer without it would use a stale ship position. |
| 3B20 | `d_a_tag_event.cpp:112` sets it and rolls a random door password into register BA0F | Its paired register is local-only, so the password would not match. |
| 3D80 3D40 3D20 3D10 | `d_meter.cpp:1054-1063` set these when they take `dComIfGs_copyPlayerRecollectionData()`; `d_com_inf_game.cpp:1446` reads them for the Xboss0-3 rematches | A peer who received the flag never takes its own snapshot. The Ganon's Tower rematch then restores empty data. |

### Every `offEventBit` call site (51 total)

- **Game logic, clears in normal play (all LocalOnly above):**
  - `d_kankyo_dayproc.inc`: 20 sites.
  - `d_a_npc_mt.cpp`: 18 sites.
  - `d_a_auction.cpp:1432`, `d_a_mdoor.cpp:141`, `d_a_npc_people.cpp:7266`, `d_a_obj_YLzou.cpp:143`, `d_a_tag_hint.cpp:733`, `d_a_tag_md_cb.cpp:61`.
  - With OR-merge, every one of these latches wrongly: the local actor clears the bit and the next sync sets it again. That is visible as NPCs stuck in one state or a flag flapping between players.
- **Debug or demo builds only, harmless:**
  - `d_s_menu.cpp:244`: map-select menu toggle for 2D01.
  - `d_a_npc_rsh1.cpp:1703-1715`: 0E08/1110/1108, inside `#if VERSION == VERSION_DEMO` HIO.
  - `d_a_obj_YLzou.cpp:482`: 3820, demo HIO.
  - These flags stay in their normal category.

## Story milestones that can break when set without their trigger

These are synced by design, but the host should know what happens.

1. **Load spawn table** (`dComIfGs_setGameStartStage`, `d_com_inf_game.cpp:1302`): RODE_KORL, then MET_KORL (Windfall), then 0801 (MajyuE), then 0808 (MajyuE start 18), then 2401 (A_umikz 204). A player still in the prologue who receives MET_KORL, 0801, 0808 or 2401 spawns at FF1, the pirate ship or Windfall on the next load, without the story state for that place. These four are marked `risky`. The recommendation is to sync them only after the local player has MET_KORL, or to accept the teleport.
2. **Boat lockout** (`d_a_ship.cpp:4224-4229` removes the ship-board attention flag):
   - 2D10 while the Master Sword is not equipped.
   - HYRULE_COURTYARD_CUTSCENE while ZELDA_AWAKENED is not set.
   - 3E10 while 3F80 is not set.
   - A peer standing on an island cannot board KoRL until the other player finishes that Hyrule section, or until the Master Sword arrives through room inventory. 2D10 and 3804 are marked `risky`. 3E10 and 3E01 are risky `Unknown` and are excluded from the mask.
3. **Stage layers** (`dComIfG_play_c::getLayerNo`, `d_com_inf_game.cpp:192-262`):
   - Flags: 0101/0E20/0520 (Outset), 2D01 (Windfall), 1820 (FF), MET_KORL (Forest), 3280/3B40/2C01/COLORS_IN_HYRULE (Hyrule, kenroom), 3B02/4002 (GanonK, GTower).
   - Receiving them swaps whole island layers: NPCs disappear, and FF switches to its post-rescue state.
   - Items those scenes would have given come through room inventory. Stage switches do not; `StageFlags` covers those.
4. **Landing gates** (`l_landingEvent`, `d_com_inf_game.cpp:1272`): 3040 FF, 2E02 Gale Isle, 0902 DRI, ENDLESS_NIGHT Greatfish, 0A20 Forest Haven, 2E04 Headstone. Syncing these is desirable, but ENDLESS_NIGHT is LocalOnly, so the Greatfish gate stays local.
5. **Companions:** 1620/1608 (Medli), 1610/1604 (Makar), 2920/2910 (Lyric/Aria stones), 2D40/2D20/3A02/4004 (sage demos). They assume the companion actor state that `tag_md_cb` and `npc_md`/`npc_cb1` build locally. A remote player can end up with a "joined" companion who is not following them. The songs themselves come through inventory.
6. **Dependent latches:**
   - 1820 makes `dKankyo_DayProc` auto-stock Aryll's and Tingle's letters into local registers. That is harmless.
   - 1E40 (Tower of the Gods raised) should travel with all three `PLACED_*_PEARL` flags.
   - The four `*_TRIALS_CLEAR` flags, the four `TRIALS_DOOR_LIGHT_*` flags and 3204 should travel together. OR-merge does this naturally.
7. **Dungeon clears are not stored in event flags.** Boss-defeated, map, compass and boss key live in `dSv_memBit_c::mDungeonItem` for each stage. `StageFlags` already syncs those. Event-flag sync only adds the overworld and story consequences.

## Event registers (bytes 0x79-0xFF), 131 total

`setEventReg` replaces the bits under the register mask, so values do not combine with OR. For example, OR-ing two letter states 1 and 2 gives 3 ("read"), and OR-ing ghost-ship states 1 and 2 gives 3 ("cleared"). Each register needs its own policy (`EventRegisterPolicy`):

| Policy | Count | Registers | Handling |
|---|---|---|---|
| BitwiseOr | 24 | Figurine bitfields 80-84, 8C-95, 9C, B1 (`l_figure_comp`); warp pots 9F-A4 (`daObj_Warpt_c::onWarpBit`, `d_a_obj_warpt.cpp:284`); Rose's pig pen BF (`d_a_kb`) | `local \|= remote & mask` is safe. |
| Max | 19 | Letter states (7A-7D, 8B, 9D, AC, AE, AF, B0, B2, B5; 0 none, 1 sent, 2 stocked, 3 read, `d_letter.cpp`), Beedle points 86, GHOST_SHIP 88 (0..3), Koboli C2, Orca level D0, pendants given C0, Sploosh Kaboom FE and barrel shooting B7 prizes won | `max()` within the mask. |
| LocalOnly | 68 | Pedestal item numbers D1-F8 (`daDai_c`: shared by the delivery bag instead, below), counters (soup A6, Beedle 7-day BB), daily resets (C9-CC, CF, B9, BC, AB, C1, C4), the random password BA, ghost-ship spawn room and start code C3/85, picture count 89, high scores 8A/BE, auction CD/79, and others | Never sync. |
| Unknown | 20 | 96-9B, A7, A8, AA, B6, C5-C8, F9-FD, FF | Never sync. |

Doing nothing is a safe default: the Full-sync mask is 0 for every register byte.

The one register group that does sync is the 17 figurine bitfields, under Shared story, as their own field (`StoryFlags.Figurines`, OR within the figurine mask, written only while idle). See [figurines.md](figurines.md). Carlov's in-progress figurine (register A9 and flags 2F01/3080/3F01/4080/4040) stays LocalOnly.

Also under Shared story, as their own fields: the side quests' counters, levels and prize tiers (`StoryFlags.QuestRegisters`, [side-quests.md](side-quests.md) §3: pendants given C0, Orca D0, Koboli C2, Sploosh Kaboom FE, barrel shooting B7 and the Ghost Ship 88 by MAX, Rose's pig pen BF by OR, written only while idle), the warp jar registers 9F-A4 (`StoryFlags.WarpJars`, OR), Beedle's points 86 (`StoryFlags.BeedlePoints`, MAX), and the 12 postbox letters (`StoryFlags.Letters`, MAX, written only while idle): 7A-7D, 8B, 9D, AC, AE, AF, B0, B2 and B5, value mask 03 (dLetter: 0 not sent, 1 sent, 2 in the postbox, 3 read; `d_letter.cpp`, `d_a_obj_toripost.cpp:39-52`). A letter anyone has read is read for everyone, so its reward (`m_letter`'s item) is given once and reaches the others through the other rules: the treasure charts (bomb ad, Tingle) through the shared sea map, the heart pieces (Baito's mother, Komali's father, Hoskit's girlfriend) through the derived hearts (the catalogue counts them at state 3), the Complimentary ID and Fill-Up Coupon through the shared delivery bag, the rupees through the shared wallet. Two players reading the same stocked letter in the same moment both get it (the rupees twice; the rest are sets).

## The old heuristic (`ProgressionSyncService`: skip bytes 0x00-0x3F, OR-merge 0x40-0xFF)

Removed: story sync is now `StorySyncService` + the server's `StoryFlagStore`, masked by `StoryFlags.SyncMask` (this catalogue's Full-sync mask, bytes 0x00-0x41), and no register is synced except the figurine bitfields ([figurines.md](figurines.md)), the warp jars, Beedle's points, the postbox letters and the side-quest registers (above); Windfall's pedestals D1-F8 ride in the shared delivery bag ([side-quests.md](side-quests.md) §3.2). Kept for the record: the old heuristic was close to inverted.

- **It excludes almost everything sharable.** Bytes 0x00-0x3F hold 479 of the 487 named bits, and 444 of those are OR-safe (including all Story and CutsceneSeen bits). Nothing in the story is shared.
- **It OR-merges what must not be merged:**
  - 4 of the 8 named bits in 0x40-0x41 are LocalOnly (4080, 4040, 4020, 4008).
  - All 131 registers in 0x79-0xFF are OR-merged. That corrupts pedestal item IDs and counters (Beedle points, Joy Pendant count), and forces letter and ghost-ship states to their final value. It also mixes the ghost-ship spawn room and start code, and it undoes daily resets.
  - The comments in that file ("0x40+: side quests, collection flags, NPC states") do not match the decomp.
- **Replacement:** OR-merge through `EventFlagCatalog.GetFullSyncMask()`: `local[i] |= remote[i] & mask[i]` for bytes 0x00-0x41 only. Handle registers per `EventRegisterPolicy`, or skip them.
