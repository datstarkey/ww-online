# Shared small keys: research and plan

Goal (approved): one small-key **count** per dungeon save slot, synced wallet-style (+1 / -1 deltas, server-held count), under the **Shared world** rule. Show it on the Room page next to boss key / map / compass. When a player unlocks a key door, it must open **live** for another player in the same room.

Target: GZLE01. Source: `tww-decomp/` (paths below are relative to it unless they start with a repo folder). Items marked **(unverified)** need checking in the DOL/REL or in a test session.

## 1. Where the count lives

### Layout

| What | Decomp | Address |
|---|---|---|
| `dSv_memBit_c::mKeyNum` (u8) | `include/d/d_save.h:688`, `/* 0x20 */`. `mDungeonItem` is at 0x21 (`:689`) | offset 0x20 in every `dSv_memory_c` (size 0x24, `d_save.h:692,738`) |
| Saved copy per stage slot | `dSv_save_c /* 0x380 */ mMemory[STAGE_MAX]` (`d_save.h:909`) | `GameInfo + 0x380 + slot*0x24 + 0x20` |
| **Live** copy (current stage) | `dSv_info_c /* 0x0778 */ mMemory` (`d_save.h:992`) | `GameInfo + 0x778 + 0x20` = **0x803C53A0** (memBit base 0x803C5380) |
| Pending HUD delta | `dComIfG_play_c /* 0x48D4 */ s16 mItemKeyNumCount` (`include/d/d_com_inf_game.h:789`) | `Play + 0x48D4` = **0x803CA77C** |
| Key-HUD stage flag | `stage_stag_info_class::mProp` bit 0 (`include/d/d_stage.h:70,1023-1025`, `dStage_stagInfo_ChkKeyDisp`) | `*(StagInfoPtr) + 0x09`, bit 0 |

The game reads and writes the live copy only. `dComIfGs_getKeyNum` and `dComIfGs_setKeyNum` go through `g_dComIfG_gameInfo.save.getMemory()` (`d_com_inf_game.h:2096-2102`).

### Live and saved copies

- **Stage enter:** `dStage_stagInfoInit` runs `dComIfGs_getSave(saveTbl)` (`src/d/d_stage.cpp:1674-1675`). That is `mMemory = mSavedata.mMemory[slot]` (`src/d/d_save.cpp:1506-1509`).
- **Stage exit:** `dStage_Delete` runs `dComIfGs_putSave(saveTbl)` (`d_stage.cpp:2255-2256`). That is `mSavedata.mMemory[slot] = mMemory` (`d_save.cpp:1512-1515`). The card-save menu also runs a `putSave` (`src/d/d_menu_save.cpp:935`).
- While you are in slot X, `mSavedata.mMemory[X]` is stale. The next `putSave` overwrites it, so **never write the saved copy of the current slot.** This is the same rule `WorldFlagSyncService` follows (`WWOnline.Client/Services/WorldFlagSyncService.cs:182-196`).
- `dSv_memBit_c::init` zeroes `mKeyNum` (`d_save.cpp:1067`).

### Stage save slots

`dSv_save_c::SaveStageTbl` (`d_save.h:888-906`). Slot = `(mProp >> 1) & 0x7F` (`d_stage.h:1027-1029`). The C# side already reads it: `WorldFlagSyncService.ReadCurrentSlot`.

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

The "Small keys" column comes from vanilla game knowledge, not the decomp; stage data isn't in the decomp. **(unverified)** that each dungeon's boss and miniboss stages use the same slot. Log the slot at runtime to check. The sync should accept any slot from 0 to 15 (randomizers put keys anywhere).

## 2. Every code path that changes the count

**The only writer of `mKeyNum`** is the HUD: `dMeter_keyMove` (`src/d/d_meter.cpp:5220-5236`). Once per frame, if the stage shows keys (`ChkKeyDisp`), it sets `mKeyNum = clamp(mKeyNum + mItemKeyNumCount, 0, 99)`, zeroes the pending value and animates the HUD digit. The displayed digit `field_0x301d` is loaded only in `dMeter_keyInit` (`:5209`) and then changes only by the animated delta, so **a direct write to `mKeyNum` leaves the HUD wrong until the next stage load**. `mItemKeyNumCount` is zeroed only by `dComIfG_play_c::itemInit` (`src/d/d_com_inf_game.cpp:80`), which runs only from file select (`src/d/d_s_name.cpp:1016,1137`). **A pending delta written in a stage without the key HUD lingers**, and it is later added to the next key-HUD dungeon's count.

Writers of the pending delta (the whole list: a grep for `setItemKeyNumCount` / `setKeyNum` finds only these):

| Change | Where | Notes |
|---|---|---|
| +1 | `item_func_small_key` (`src/d/d_item.cpp:664-667`), called by `execItemGet(dItemNo_SMALL_KEY_e = 0x15)` | Every source goes through this. A field key (`daItem_c`) sets `STATUS_INIT_GET_DEMO` (`src/d/actor/d_a_item.cpp:647-649`) and sets its item bit right away (`:768` `fopAcM_onItemForIb`). The +1 lands when the get-demo item is deleted (`src/d/actor/d_a_demo_item.cpp:438-441`), about 1-3 s later. Chest keys: `dComIfGs_onTbox` (`src/d/actor/d_a_tbox.cpp:858`), then the same demo-item path. |
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

| Case | What happens | Handling |
|---|---|---|
| Two players pick up the same **field key** | Keys are `daItem_c` with an item bit (`d_a_item.cpp:226-236` at create; `:768` at pickup). World sync ORs the bit, and layer 2 despawns an idle key live (`puppet_worldsync.c`). The only window is sync latency (about 0.25-1 s). | Both +1 reach the server, so the count is one too high. Harmless: it can't soft-lock. Optional dedupe is below. |
| Two players open the same **chest** | Chests check `mTbox` only at create (`puppet_worldsync.c:15`), so a remote open doesn't close or open it live. The second player can open it again. | Count one too high, harmless. Same optional dedupe. |
| Count = 1, both use it on **different** doors | Each game spends its local 1. The server gets -1 twice and clamps at 0. Both doors are open (switch bits sync). | Players got a free door. No soft-lock, because the dungeon now has a spare key. Accept it. |
| Both open the **same** door at once | Each game runs `keyInit`, so -2 for one door. **That can soft-lock later** (one key short). | The REL fix closes most of the window: once the switch bit arrives, the second game's lock is gone. Recommended dedupe: tag a -1 with the door `swbit`, and have the server refund a second -1 for the same (slot, swbit) (see §5). |
| Entering a dungeon never visited | That save's `mMemory[slot].mKeyNum` is 0. | The client writes the server count into the **saved** slot of every non-current slot. `getSave` then copies it to the live copy on entry. **(unverified)** that `dMeter_keyInit` runs after `dStage_stagInfoInit` in the play scene, so the HUD shows the copied value. Check in a test. |
| Remote change for the **current** slot | The live copy is authoritative. | If `ChkKeyDisp`, write the pending s16 (HUD animates, the game clamps it). Otherwise write live `mKeyNum` directly, because a pending delta would linger (see §2). |
| Stage change | `putSave` copies live to saved[old], then `getSave` copies saved[new] to live (§1). The stag pointer is stale in between. | Only tick behind `SceneStabilityGate` (Link present, `NextStageEnable == 0`, same stage for 4 ticks: `WWOnline.Client/Services/SceneStabilityGate.cs`). A key used just before leaving shows up in saved[old] on the next stable tick, and the per-slot baseline catches it. |
| Game writes pending while C# does | C# read-modify-write of `mItemKeyNumCount` can race the same-frame `+=` in `item_func_small_key` or `keyInit`. The wallet has the same window with `PendingRupeeDelta`. | Write only when pending == 0, as one store. The wallet's `SettledBaseline` logic spots a lost write. Hardening option: C# publishes a target and the REL applies `pending = target - (mKeyNum + pending)` inside the frame. |
| Bounds | u8 field. The HUD clamps to 0..99 (`d_meter.cpp:5228-5232`). The HUD shows 2 digits (`:5210-5211`). | Server: slot 0..15, counts 0..99, delta non-zero with \|delta\| ≤ 99. Clamp on apply. |
| Joiner's counts differ from the room | A joiner may hold a key whose pickup bit is already in the room. Adopting a lower room count would erase a key they need. | On join, **max-merge** each slot (joiners merge up, per the room design), then use deltas. Over-count is harmless. Under-count soft-locks. |
| Rule toggled | | SharedWorld off: the server resets the key store (like `WalletStore.Reset`, `GameHub.cs:261-268`) and everyone keeps their own counts. Back on: rejoin with max-merge. |

## 5. Implementation plan

### Addresses
- `GameMemoryAddresses.WorldFlags` (`WWOnline.Client/Data/GameMemoryAddresses.cs:298-325`): `OffKeyNum = 0x20` already exists (`:312`). Update its comment, which says it is not synced. Add:
  - `PendingKeyDelta = Play + 0x48D4`: `dComIfG_play_c /* 0x48D4 */ s16 mItemKeyNumCount`, 0x803CA77C, a `MemoryAddress<short>` next to `Player.PendingRupeeDelta`.
  - `StagPropKeyDispMask = 0x01`: `mProp` bit 0, `dStage_stagInfo_ChkKeyDisp`. It is read from the same byte as `StagSaveTblOffset`.
  - A `GameInfoAddressTests` case: live key = 0x803C53A0, slot 3 key = 0x803C5014, pending = 0x803CA77C.
- `ww_inlines.h` (REL, WORLD SYNC block): `PROC_NAME_DOOR10 0x12E`, `PROC_NAME_DOOR12 0x12F`, `DDOOR_PRM_SWBIT(prm)`, `DDOOR_PRM_TYPE(prm)`, `DOOR10_KEYLOCK(d) (+0x308)`, `DOOR10_ACTION(d) (+0x354)`, `DOOR12_KEYLOCK(d) (+0x2E4)`, `DOOR12_ACTION(d) (+0x314)`, `KEYLOCK_ENABLED(k) (+0x00)`, `KEYLOCK_IS_BOSS(k) (+0x21)`, `DOOR_ACTION_WAIT 1`. Cite the lines from §2 and §3 in comments. `MEMBIT_KEY_NUM` already exists (`ww_inlines.h:882`).
- `puppet_shared.h`: needed only for diagnostics. For example, `DOOR_UNLOCK_COUNT_ADDR 0x803FD1D0` (u32, ++ per stale lock cleared) and `DOOR_LAST_UNLOCK_ADDR 0x803FD1D4` (u32, `(saveTbl<<16) | ((procName & 0xFF)<<8) | swbit`). 0x803FD1D0..0x803FD1DF is free: the boats end at `PUPPET_BOAT_BASE(3)` = 0x803FD1D0 and world sync starts at 0x803FD1E0. Add a `typedef` range check and a `PuppetLayoutTests` case. Any change there needs a re-patch.

### REL (`puppet_worldsync.c`, small)
Add `ws_door_judge` plus a loop that runs in `puppet_worldsync_tick` on the same every-4th-frame gate and the same `NEXT_STAGE_ENABLE`/stag checks. It does **not** depend on the item-mask tag, so run it before the `mask == 0` early return. Clear at most N doors per scan, bump the counter, and `OSReport("[PUPPET] door lock cleared sw=%d")`. The code is tiny, so the ARAM impact is negligible, but it is still REL growth (see GameMod/CLAUDE.md rule 6). **Compile or patch only when the user asks.**

### Shared DTOs, hub, server
- `HubConstants`: `JoinSmallKeys`, `SendSmallKeyDelta`, `ReceiveSmallKeyCount`, `MaxSmallKeys = 99`. **Bump `ProtocolVersion`** (`WWOnline.Shared/Hubs/HubConstants.cs:15`).
- DTO `SmallKeyDelta { int Slot; int Delta; int DoorSwitch = -1 }`. `DoorSwitch` is set only on -1 when the client could attribute it. `IsValid()`: slot 0..15, delta ≠ 0 with |delta| ≤ 99, DoorSwitch -1 or 0..0x7F.
- `IGameHubClient.ReceiveSmallKeyCount(int slot, int count)`.
- `WWOnline.Server/Hubs/SmallKeyStore.cs`, shaped like `WalletStore`, thread-safe:
  - `int[]? _counts` (null = unseeded).
  - `Join(int[] local)` seeds or max-merges and returns `(int[] counts, bool seeded, List<int> raisedSlots)`.
  - `ApplyDelta(SmallKeyDelta)` returns the new count, or null if unseeded or invalid. It clamps to 0..99.
  - Optional `HashSet<(int slot,int sw)> _spentDoors`: a second -1 for the same door is refused and returns the unchanged count, so the caller is refunded.
  - `Reset()`.
- `GameHub`:
  - `JoinSmallKeys(int[] counts)`: null when `!RoomRules.SharedWorld`, when `CheckSeedGate(...)` says wait (a new `KeysSeedGate`), or when the payload is invalid (length 16, each 0..99). Broadcast `ReceiveSmallKeyCount` to others for each slot the join raised.
  - `SendSmallKeyDelta(SmallKeyDelta d)`: validate, gate on the rule, apply, log `[keys] {Player} slot {Slot} {Delta:+#;-#} → {Count}`, then `Clients.All.ReceiveSmallKeyCount`. Send a refused duplicate only to the caller.
  - Reset the store and gate when SharedWorld turns off (next to the wallet reset in `SetRoomSettings`).

### Client: `SharedSmallKeyService` (mirror `SharedWalletService`)
- 4 Hz timer, `_tickLock`, `SceneStabilityGate(4)`, `_connectionGeneration`, reset and rejoin on `SignalRClientService.Connected`, `Stop`/`Dispose`, no `async void`. Register it in `App.axaml.cs` and start it wherever the other sync services start.
- Each tick:
  1. Gates: Dolphin and hub connected, `SharedWorld` on (when off, leave and `SetCounts(null)`), scene stable.
  2. Read the current slot. Share `ReadCurrentSlot` with `WorldFlagSyncService` by moving it to a small helper that also returns `ChkKeyDisp`.
  3. Read `PendingKeyDelta`. If ≠ 0, return (the game is applying).
  4. `local[s]` = live `mKeyNum` for the current slot, else saved `mMemory[s].mKeyNum`. One `ReadMemory(SavedMemoryBase, 16*0x24)` is enough.
  5. Not joined: `TryJoin(local)` (retry every 3 s), then baseline = local and targets = the server counts.
  6. **Local changes first:** for each slot where `local[s] != baseline[s]`, send `local - baseline` and set baseline = local. On a failed send, restore the baseline (the wallet pattern). For a -1 in the current slot, attach `DoorSwitch` if exactly one live switch bit appeared in the last ~2 s of ticks. `keyInit` sets the switch in the same frame it queues the -1 (`d_door.cpp:436-439`).
  7. **Apply targets:** current slot with `ChkKeyDisp`: write the pending s16 `target - live`. Current slot without it: write live `mKeyNum`. Other slots: write saved `mKeyNum`. Set the baseline once the next tick sees the value landed; follow `SettledBaseline` so a lost write is never sent back as a spend.
- Expose `IReadOnlyList<int>? Counts` and a `CountsChanged` event for the UI. Log with a `[keys]` tag, one line per change.
- `WorldFlagSyncService` doesn't change (it never touches `mKeyNum`). Expose a per-slot `DungeonItem` view of local OR received for the Room page (for example `DungeonItemsChanged(int slot, byte bits)`).

### Room page (`DashboardViewModel` / `DashboardView.axaml`)
Add a "Dungeons" card with one row per key dungeon: DRC, FW, TotG, ET, WT. FF and GT rows could show only BK/map/compass. Each row shows:
- the key count (the room's count when the rule is on; "Off: per player" plus the local count when it is off),
- map, compass and boss-key pips from `DungeonItem` bits 0/1/2,
- optionally a "boss beaten" pip from bit 3 (`STAGE_BOSS_ENEMY`).

Unsubscribe in `Dispose`. Use `ww-*` classes, and margins rather than `ColumnSpacing`.

### Tests
- `ServerStoreTests`: `SmallKeyStore` seeding, join max-merge, clamp to 0..99, rejecting an unseeded delta, invalid slot/delta/array, reset, and same-door -1 refused (if implemented).
- Hub validation: bad payloads rejected, the rule-off gate.
- `SyncLogicTests` with `FakeDolphin` covering:
  - a pickup in the current slot is sent as +1,
  - a key used just before a stage change is caught from saved[old],
  - a remote count for a non-current slot is written to saved only,
  - the current slot gets the pending write only with `ChkKeyDisp` and pending == 0, otherwise the live write,
  - an applied value is never echoed back,
  - reconnect rejoins.
- `GameInfoAddressTests` and `PuppetLayoutTests` for the new addresses and scratch words.
- Manual (dev-test): P1 picks up a key and P2's HUD animates +1. P1 unlocks a door while P2 stands in the same room: P2's lock disappears and `[PUPPET] door lock cleared` shows in `dolphin-2.log`. P2 walks through with count 0.

### Docs to update when it ships
- `CLAUDE.md` "These never sync" (remove small keys) and the Room sync model.
- `README.md:58-60`.
- The `StageFlags` doc comment (`WWOnline.Shared/Models/StageFlags.cs:8`).
- The `OffKeyNum` comment.
