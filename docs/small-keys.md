# Shared small keys

With **Shared world** on, a small key anyone in the room finds is everyone's, and a key door anyone unlocks uses it up for everyone. The Room page's **Dungeons** card shows each dungeon's keys, map, compass, big key and boss. §0 is what is built; §1-§4 are the research it rests on.

Target: GZLE01. Source: `tww-decomp/` (paths below are relative to it unless they start with a repo folder). **Verified** means checked in the vanilla GZLE01 `main.dol`; **(unverified)** needs a test session.

## 0. What is built: derived keys, not counted keys

The count is never sent anywhere. In a dungeon (a save slot with key doors), the keys a player holds are

> **keys = key sources whose flag is set - key doors whose switch is set**

and those flags are world flags that `WorldFlagSyncService` already OR-merges (chest `mTbox` bits, placed-item `mItem` bits, door memory switches). So every player derives the same count from the same flags. There is no server store and no hub method (**no protocol change**: `HubConstants.ProtocolVersion` stays), nothing to dedupe or refund, and a reconnect, a race or two players opening the same chest can't count a key twice.

**The key table** (`WWOnline.Patcher/WorldData/SmallKeyTableBuilder.cs`, `SmallKeyTable.cs`) lists each dungeon's key sources and key doors. It is built from the player's own stage files (`files/res/Stage/*/Stage.arc` + `Room*.arc`, read by `StageDataReader`, the same reader as the switch table) and main.dol's object-name table (`l_objectName`), and never committed. The client builds it on first use (`SmallKeyTableProvider`: about 0.3 s for all 155 stages, on a background thread) from the Settings page's patched game, else its vanilla game, else (dev) `GameMod/config.json`'s paths, and keeps it in memory. It isn't written at patch time: that would need a re-patch and a build-stamp change for data the client can read itself, and no patch edits a stage's actors.

| Source / door | Actor | Flag (tww-decomp) |
|---|---|---|
| Key chest | `daTbox_c` (proc 0x126), item `home.angle.z >> 8` = 0x15 | tbox bit `prm >> 7 & 0x1F` (`d_a_tbox.h:35-37`); function types 7/8 save to STAGE_SEA2 and are skipped |
| Placed key | `daItem_c` (proc 0x101), item `prm & 0xFF` = 0x15 | item bit `prm >> 8 & 0xFF` if < 0x20 (`d_a_item.h:141-142`; memory items, `d_save.cpp:1629`) |
| Key door | `daDoor10_c` (0x12E) type 4/5, `daDoor12_c` (0x12F) type 1 (`chkMakeKey() == 1`) | memory switch `prm & 0xFF` (`d_door.cpp:16-28`), set by `keyInit` when the key is used |

Boss doors (door10 type 1, door12 type 3) also set their switch but never spend a small key, so they are not in the table. Anything else that could give a key (a zone item bit, a chest that saves elsewhere, a key door on a non-memory switch) is listed as *untracked* and logged, not counted.

**Vanilla table** (checked by `SmallKeyTableTests.VanillaGame_EveryDungeonHasAsManyKeysAsKeyDoors`): every dungeon has exactly as many keys as key doors, and nothing but chests and placed items holds a small key (every other actor in the dungeon stages was searched for item 0x15 in its parameters and angles).

| Slot | Dungeon | Keys (flag) | Key doors (switch) |
|---|---|---|---|
| 3 | Dragon Roost Cavern (`M_NewD2`) | 4: chests tbox 0, 1, 3; item bit 3 (room 3) | 4: 0x36, 0x3C, 0x3D, 0x3E |
| 4 | Forbidden Woods (`kindan`) | 1: chest tbox 5 | 1: 0x05 |
| 5 | Tower of the Gods (`Siren`) | 2: chests tbox 1, 4 | 2: 0x0E, 0x1A |
| 6 | Earth Temple (`M_Dai`) | 3: chests tbox 1, 4; item bit 14 (room 8) | 3: 0x0F, 0x2F, 0x4B |
| 7 | Wind Temple (`kaze`; the same actors are in `Cave08`) | 2: chests tbox 4, 9 | 2: 0x13, 0x1B |

Exceptions: slot 15 (STAGE_TEST, `K_Test5` / `K_Testc`) has a key chest and a flagless key item but no key door, so it is not a dungeon and is never touched. No other slot has key doors, so keys are never written outside the five dungeons.

**Writing it** (`SharedSmallKeyService` at 4 Hz, `SmallKeyReconciler`, `SmallKeyMemory`). Only in a room with Shared world on (offline or with the rule off, every game keeps its own counts), and only in a settled scene (`SceneStabilityGate`). For each dungeon, target = clamp(derived + unknown-source keys, 0, 99).
- **Current dungeon** (flags and count from the live copy): written only after the game has been idle for 2 ticks: no key change queued for the HUD (`play.mItemKeyNumCount` = 0), no event (`play.mEvtCtrl.mMode` = 0: a chest's or field key's get-item demo, a door's unlock event) and no pause menu. A pickup sets its flag at once but adds its key at the end of its demo, and a door queues its -1 in its unlock event, so the game always finishes its own change first. With the key HUD (`ChkKeyDisp`) the difference goes into `mItemKeyNumCount`, so the HUD animates it (never while a change is already queued); without it the live `mKeyNum` is written (a pending change would linger into the next dungeon, §2).
- **Other dungeons:** their saved `mKeyNum` is written at once (it is loaded into the live copy on entry). The saved copy of the current slot is never written.
- **A key spent on an already-open door** (another player's unlock arrived but live world hasn't re-created the lock yet): that door's switch was already counted, so the game's count drops below the derived one and is written back up: the key is handed back. Two players opening the same door at once spend one key between them.
- **Below zero** (two players used the same last key on two different doors at once): clamped to 0 and logged.
- **A key from a source the table doesn't know** (a randomizer, modded stage data): the game's count rises past the derived one with no key flag set in the last 10 s. It is kept for this player for the session (added to the derived count, and used up by its door like any other key) and logged with the stage and room, so the table can learn the source. Other players don't get it. This is the least surprising choice: taking away a key the player just picked up would look like a bug, and the others can't be given a key no flag records. A save reloaded later loses it (the next derivation writes the count down); that only happens with non-vanilla data.
- **Joining** with keys the room's flags don't explain (your own save got further than the room): the count is set to the derived one, like everyone's.

**Room page:** the Dungeons card (`DungeonsCardViewModel`, `DashboardView.axaml`) lists the five key dungeons: keys held (the derived count, or the game's own when not shared), "n of m keys found · n of m doors unlocked", and map / compass / big key / boss pips from `mDungeonItem` bits 0-3 (world flags, shared with Shared world). Read-only for everyone. There is no owner edit: a derived count has nothing to edit, the flags are the state.

**Not used:** the scratch range 0x803FD1D0..0x803FD1DF stays free. There is no REL change: the game mod is untouched and nothing needs a re-patch.

### In-game checklist (dev-test, Shared world on)

| # | Do | Expect |
|---|---|---|
| K1 | P1 opens DRC's first key chest | P1: the key lands at the end of the demo and P1 logs no `[keys]` write. P2: `[keys] slot 3: small keys 0 → 1`; in DRC P2's HUD counts up, elsewhere the Dungeons card shows 1 |
| K2 | P1 unlocks a key door, P2 in DRC | Both go to 0 (P2 via `[keys] slot 3: small keys 1 → 0`); the door comes back unlocked for P2 (`[PUPPET] live world: re-created`) |
| K3 | P2 stands at a door P1 just unlocked (lock still showing) and opens it with a key | P2's HUD -1 at the unlock, then +1 about half a second after the door event (`[keys] slot 3: small keys 0 → 1`) |
| K4 | Both pick up the same field key at once | The count rises by 1, not 2 |
| K5 | P2 disconnects and reconnects in DRC | No `[keys]` write; counts unchanged |
| K6 | The owner turns Shared world off, then on | `[keys] shared small keys off`: counts change only by each player's own play. Back on: every count is derived again |
| K7 | P2 enters Forbidden Woods after P1 took its key | P2's HUD shows 1 on entry (the saved copy was written) **(unverified: `dMeter_keyInit` reads the copied value)** |
| K8 | Room page | Dungeons card: keys, "n of m keys found", map / compass / big key / boss pips; "Off: per player" with the rule off |

## 1. Where the count lives

### Layout

| What | Decomp | Address |
|---|---|---|
| `dSv_memBit_c::mKeyNum` (u8) | `include/d/d_save.h:688`, `/* 0x20 */`. `mDungeonItem` is at 0x21 (`:689`) | offset 0x20 in every `dSv_memory_c` (size 0x24, `d_save.h:692,738`) |
| Saved copy per stage slot | `dSv_save_c /* 0x380 */ mMemory[STAGE_MAX]` (`d_save.h:909`) | `GameInfo + 0x380 + slot*0x24 + 0x20` |
| **Live** copy (current stage) | `dSv_info_c /* 0x0778 */ mMemory` (`d_save.h:992`) | `GameInfo + 0x778 + 0x20` = **0x803C53A0** (memBit base 0x803C5380). **Verified**: `dMeter_keyMove` does `lbz`/`stb 0x798(gameInfo)` (DOL 0x801FCF78, 0x801FCFB4) |
| Pending HUD delta | `dComIfG_play_c /* 0x48D4 */ s16 mItemKeyNumCount` (`include/d/d_com_inf_game.h:789`) | `Play + 0x48D4` = **0x803CA77C**. **Verified**: `lha 0x5B74(gameInfo)` in `dMeter_keyMove` (0x801FCF6C), `item_func_small_key` (0x800C31B8, +1) and `keyInit` (0x8006C52C, -1) |
| Key-HUD stage flag | `stage_stag_info_class::mProp` bit 0 (`include/d/d_stage.h:70,1023-1025`, `dStage_stagInfo_ChkKeyDisp`) | `*(StagInfoPtr) + 0x09`, bit 0. **Verified**: `lbz r0,9(r3); clrlwi. r0,r0,31` after `getStagInfo` (0x801FCF58) |

All three are in `GameMemoryAddresses.WorldFlags` (`LiveKeyNum`, `PendingKeyDelta`, `StagKeyDispMask`) and pinned by `GameInfoAddressTests.SmallKeyAddresses_MatchTheVanillaDol`.

The game reads and writes the live copy only. `dComIfGs_getKeyNum` and `dComIfGs_setKeyNum` go through `g_dComIfG_gameInfo.save.getMemory()` (`d_com_inf_game.h:2096-2102`).

### Live and saved copies

- **Stage enter:** `dStage_stagInfoInit` runs `dComIfGs_getSave(saveTbl)` (`src/d/d_stage.cpp:1674-1675`). That is `mMemory = mSavedata.mMemory[slot]` (`src/d/d_save.cpp:1506-1509`).
- **Stage exit:** `dStage_Delete` runs `dComIfGs_putSave(saveTbl)` (`d_stage.cpp:2255-2256`). That is `mSavedata.mMemory[slot] = mMemory` (`d_save.cpp:1512-1515`). The card-save menu also runs a `putSave` (`src/d/d_menu_save.cpp:935`).
- While you are in slot X, `mSavedata.mMemory[X]` is stale. The next `putSave` overwrites it, so **never write the saved copy of the current slot.** This is the same rule `WorldFlagSyncService` follows (`WWOnline.Client/Services/WorldFlagSyncService.cs:182-196`).
- `dSv_memBit_c::init` zeroes `mKeyNum` (`d_save.cpp:1067`).

### Stage save slots

`dSv_save_c::SaveStageTbl` (`d_save.h:888-906`). Slot = `(mProp >> 1) & 0x7F` (`d_stage.h:1027-1029`). The C# side reads it in `SmallKeyMemory.ReadStage` (with the key-HUD bit) and `WorldFlagSyncService.ReadCurrentSlot`.

| Slot | Enum | Dungeon | mKeyNum address | Small keys (vanilla) |
|---|---|---|---|---|
| 0x0 | STAGE_SEA | Great Sea | 0x803C4FA8 | no |
| 0x1 | STAGE_SEA2 | (sea 2) | 0x803C4FCC | no |
| 0x2 | STAGE_FF | Forsaken Fortress | 0x803C4FF0 | no |
| **0x3** | STAGE_DRC | Dragon Roost Cavern (`M_NewD2`, boss `M_DragB`) | **0x803C5014** | yes |
| **0x4** | STAGE_FW | Forbidden Woods (`kindan`, `kinBOSS`) | **0x803C5038** | yes |
| **0x5** | STAGE_TOTG | Tower of the Gods (`Siren*`) | **0x803C505C** | yes |
| **0x6** | STAGE_ET | Earth Temple (`M_Dai*`) | **0x803C5080** | yes |
| **0x7** | STAGE_WT | Wind Temple (`kaze*`) | **0x803C50A4** | yes |
| 0x8 | STAGE_GT | Ganon's Tower | 0x803C50C8 | no |
| 0x9 | STAGE_HYRULE | Hyrule | 0x803C50EC | no |
| 0xA | STAGE_SHIP | Ghost ship | 0x803C5110 | no |
| 0xB | STAGE_MISC | interiors | 0x803C5134 | no |
| 0xC / 0xD | SUBDUNGEON / _NEW | caves, Savage Labyrinth... | 0x803C5158 / 0x803C517C | no |
| 0xE | STAGE_BLUE_CHU_JELLY | (item-bit store) | 0x803C51A0 | no |
| 0xF | STAGE_TEST | test | 0x803C51C4 | no |

The "Small keys" column is confirmed by the key table (§0). The stage data also confirms each dungeon's boss and miniboss stages use its slot: `M_DragB` / `M_Dra09` 3, `kinBOSS` / `kinMB` 4, `SirenB` / `SirenMB` 5, `M_DaiB` / `M_DaiMB` 6, `kazeB` / `kazeMB` / `Cave08` 7.

## 2. Every code path that changes the count

**The only writer of `mKeyNum`** is the HUD: `dMeter_keyMove` (`src/d/d_meter.cpp:5220-5236`). Once per frame, if the stage shows keys (`ChkKeyDisp`), it sets `mKeyNum = clamp(mKeyNum + mItemKeyNumCount, 0, 99)`, zeroes the pending value and animates the HUD digit. The displayed digit `field_0x301d` is loaded only in `dMeter_keyInit` (`:5209`) and then changes only by the animated delta, so **a direct write to `mKeyNum` leaves the HUD wrong until the next stage load**. `mItemKeyNumCount` is zeroed only by `dComIfG_play_c::itemInit` (`src/d/d_com_inf_game.cpp:80`), which runs only from file select (`src/d/d_s_name.cpp:1016,1137`). **A pending delta written in a stage without the key HUD lingers**, and it is later added to the next key-HUD dungeon's count.

Writers of the pending delta (the whole list: a grep for `setItemKeyNumCount` / `setKeyNum` finds only these):

| Change | Where | Notes |
|---|---|---|
| +1 | `item_func_small_key` (`src/d/d_item.cpp:664-667`), called by `execItemGet(dItemNo_SMALL_KEY_e = 0x15)` | Every source goes through this. A field key (`daItem_c`) sets `STATUS_INIT_GET_DEMO` (`src/d/actor/d_a_item.cpp:647-649`) and sets its item bit right away (`:768` `fopAcM_onItemForIb`). The +1 lands when the get-demo item is deleted (`src/d/actor/d_a_demo_item.cpp:438-441`), at the end of the get-item event (as long as the player keeps its message open). Chest keys: `dComIfGs_onTbox` at the open (`src/d/actor/d_a_tbox.cpp:858`), then the same demo-item path. |
| -1 | `dDoor_key2_c::keyInit` (`src/d/d_door.cpp:434-448`) | Only if the lock is enabled, has a model and the player is on the **front** side (`!mFrontCheck`). It sets the door switch first, `dComIfGs_onSwitch(getSwbit(), -1)` when `swbit < 0x80` (`:436-437`, which goes to the live memBit via `d_save.cpp:1548-1549`), then queues -1 unless the door is a boss door (`:438-439`). |

`keyInit` runs on the door event's `"UNLOCK"` cut (action index 8, `d_door.cpp:332`). It is called from `daDoor10_c::demoProc` (`src/d/actor/d_a_door10.cpp:569-571`) and `daDoor12_c::demoProc` (`src/d/actor/d_a_door12.cpp:619-621`). **No other actor consumes keys.** Knob doors, shutters and the other `*lock*` actors never call it.

| Actor | Proc (`include/f_pc/f_pc_name.h:314-315`) | Small-key lock | Boss lock | Lock object | Action byte |
|---|---|---|---|---|---|
| `daDoor10_c` (stone/shutter doors) | `fpcNm_DOOR10_e` 0x12E | type 4 or 5 (`d_a_door10.cpp:13-25`) | type 1 | `mKeyLock` +0x308 (`include/d/actor/d_a_door10.h:46`) | `m354` +0x354 (`:51`) |
| `daDoor12_c` | `fpcNm_DOOR12_e` 0x12F | type 1 (`d_a_door12.cpp:39-49`) | type 3 | `mKeyLock` +0x2E4 (`include/d/actor/d_a_door12.h:51`) | `m314` +0x314 (`:53`) |

Door params (`d_door.cpp:16-28`): `swbit = prm & 0xFF`, `type = (prm >> 8) & 0xF`. `dDoor_key2_c` (`include/d/d_door.h:88-96`): `mbEnabled` at +0x00, `mpModel` at +0x04, `m20` (anim running) at +0x20, `mbIsBossDoor` at +0x21.

**What keeps a door open:** its memory switch `swbit` (`dSv_memBit_c::mSwitch`, which the world sync already covers). `setKey` shows the lock only while `!isSwitch(swbit)` (`d_a_door10.cpp:28-51`, `d_a_door12.cpp:52-73`). No separate "unlocked" bit exists. **(inferred)** Small-key doors must use a memory switch (< 0x80), because `keyInit` only saves those.

Other readers, which do not change the count:
- `setEventPrm` refuses to offer the door when `mbEnabled && getKeyNum()==0` (`d_a_door10.cpp:359-373`, `d_a_door12.cpp:407-421`).
- The Tingle Tuner's `d_a_agbsw0.cpp:1949` condition.

The boss key is `mDungeonItem` bit 2 (`d_save.h:639-646`, `item_func_boss_key` at `d_item.cpp:963-965`). It is never cleared (there are no `offDungeonItemBossKey` callers), so boss doors don't consume anything. Map is bit 0 and compass bit 1 (`d_item.cpp:953-960`). All three are already OR-synced as `StageFlags.DungeonItem`.

## 3. Live behaviour of a locked door

The lock state is re-evaluated **only in `actionInit`**. `daDoor10_actionInit` (`d_a_door10.cpp:748-759`) and `daDoor12_actionInit` (`d_a_door12.cpp:765-773`) call `setKey()`, then switch to Wait (action 1). The action is reset to Init (0) only when `checkExecute()` returns 0 (`d_a_door10.cpp:804-813`, `d_a_door12.cpp:823-832`). `dDoor_info_c::checkExecute` (`d_door.cpp:156-171`) returns 0 only on the frame the player's stay room changes (`mRoomNo2 != stayNo`), or when the player is in neither adjoining room. It returns 2 every frame the player stays in an adjoining room.

So if the other player's unlock sets the switch in your live memBit while you stand in either room of that door, **your lock stays** (`mbEnabled` stays true, drawn at `d_a_door10.cpp:774-776`) until you change rooms. Meanwhile:
- If the shared count is now 0, you cannot open it (`setEventPrm`). If you reached this room one-way, that is a **soft-lock**.
- If the count is > 0, opening it runs `keyInit` again, which **spends a second key** on an already-unlocked door (`mbEnabled` is still true).

**Superseded:** live world now re-creates the door instead (`docs/live-world.md` 0.1), and a key spent on such a door is handed back by the derivation (§0). The first plan, kept for reference:

**Minimal REL fix.** In the puppet REL, scan door actors every few frames and clear stale locks. Put it in `puppet_worldsync_tick` as a second `fopAcIt_Judge` pass, the same pattern as `ws_judge` (`GameMod/src/puppet_link/puppet_worldsync.c:58-103`). A door qualifies when all of these hold:

1. `BASE_PROC_NAME` is 0x12E or 0x12F, create is done, and it is not being deleted.
2. The type is a small-key lock (door10: type 4 or 5; door12: type 1), and `mbIsBossDoor == 0`.
3. Its action byte == 1 (Wait). Never touch Init (0) or Demo (3): an unlock animation in progress has `mbEnabled` and the switch both set legitimately, on the local player's own unlock.
4. `mKeyLock.mbEnabled != 0`, `swbit < 0x80`, and `MEMBIT_SWITCH(live, swbit>>5) & (1 << (swbit&31))`.

Action: write `mbEnabled = 0`. That is exactly `dDoor_key2_c::keyOff` (`d_door.cpp:522-524`), the state `setKey` would produce. The lock stops drawing. `setEventPrm` stops requiring a key. If the door's event runs, `keyInit` skips the decrement because `mbEnabled` is false (`d_door.cpp:435,445-446`). In vanilla, Wait + `mbEnabled` + switch set never happens (a local unlock ends in `keyOff` via `keyProc`, `:450-463`), so the rule needs no C# mask.

An equally small alternative: write the action byte to 0, and the door's own `actionInit`/`setKey` runs next frame. That also re-runs `setStop` (the stop bars), which is harmless but more than needed.

Caveats:
- The REL tick runs only while a puppet actor exists (`puppet_worldsync.c:36-37`). A player who just unlocked a door in your stage is normally a puppet there. **(unverified)** that this covers someone who unlocks and leaves the stage at once. If needed, publish a `DOOR_STALE_PENDING` word the way `WORLDSYNC_PENDING_ADDR` works, so C# keeps a parked puppet alive.
- Verify the offsets 0x308/0x354 (door10) and 0x2E4/0x314 (door12) in the door RELs **(unverified)**. For example, find `addi r3,r31,0x308` before the `keyOn`/`keyOff` calls.

## 4. Races and edge cases

The first plan synced a +1 / -1 count wallet-style. It had to dedupe pickups (two players opening the same chest before the flag syncs), refund a key spent on an already-unlocked door, max-merge on join and survive reconnects without counting again. Deriving the count from the flags (§0) makes those moot: the flags are OR-merged and idempotent, so the result is the same however often and in whatever order they arrive. What remains:

| Case | Handling (§0) |
|---|---|
| A pickup's flag is set before its key lands (get-item demo) | The current dungeon is written only after 2 idle ticks with no event |
| A door's -1 is queued in its unlock event | Same idle rule. The switch and the -1 land in the same frame (`keyInit`, verified at DOL 0x8006C4A8-0x8006C534) |
| C# writing `mItemKeyNumCount` while the game changes it | Written only while it is 0, re-checked just before the write. A lost write shows up as a mismatch and is written again |
| A pending change written in a stage without the key HUD lingers | The live count is written there instead |
| Stage change (putSave / getSave) | Only in a settled scene; the current slot's saved copy is never written |
| Two players use the same last key on different doors | Both doors open; the count is clamped at 0 and logged |
| A key from a source the table doesn't know | Kept for that player, logged with stage and room |

The first plan's REL fix (clear a stale lock by writing `mbEnabled`) froze the game and was replaced by live world's door re-create (`docs/live-world.md` 0.1).
