# Held items & projectiles on puppets: research

Question: "How can we show a player holding something (the boomerang, a bomb, etc.) and make it real in the other players' world too? Is that possible?"

Short answer: **yes, in stages.** Holding and using items is cheap and fairly safe. Much of it needs no protocol change, because `mEquipItem` already reaches the slot. Real projectiles are possible for **bombs**, which the game already spawns for a second player: the Tingle Tuner bomb, `d_a_agb.cpp:956`. They are hard for the **boomerang, arrows and hookshot**, because those actors are hard-wired to the *local* Link.

Citations are to `tww-decomp/` (the `ww-online` checkout is populated) unless they name a GameMod file. **(?)** marks something unverified.

---

## 1. How daPy_lk_c shows held and used items

### Two kinds of item
`mEquipItem` (u16 @ `0x3560`, `d_a_player_main.h:2239`) holds either a `daPyItem_*` pseudo-id (`NONE 0x100`, `SWORD 0x103`, bottles `0x105/0x106`, ... `:931-942`) or a real `dItemNo`. The item comes out through `checkItemAction` (`d_a_player_main.cpp:3893`). At the TAKE/REST frame it runs `deleteEquipItem` and sets `mEquipItem = m3562`, then calls `makeItemType()` (`:3681-3760`), which splits the items in two:

| Item (dItemNo) | What `makeItemType` builds | Owned by | Notes |
|---|---|---|---|
| Bow `0x27/0x35/0x36` | `setBowModel` (`d_a_player_bow.inc:216`) | **player model** (`mpEquipItemModel` in `mpItemHeaps`) | also `mSwordAnim.init` (bow string bck) + joint CBs with `userArea = this` |
| Telescope `0x20` | `setScopeModel` (`:3740`) | player model | |
| Picto Box `0x23/0x26` | `setPhotoBoxModel` (`:3747`) | player model | |
| Tingle Tuner `0x21` | `setTinkleCeiverModel` (`d_a_player_dproc.inc:44`) | player model | |
| Deku Leaf `0x34` | `setSmallFanModel` (`d_a_player_fan.inc:132`) | player model | the glide uses `mpParachuteFanMorf` (FANB) |
| Wind Waker `0x22` | `setTactModel` (`d_a_player_tact.inc:34`) | player model | **brk registered on SHARED model data** (`entryTevRegAnimator`, :48) |
| Skull Hammer `0x33` | `setHammerModel` (`d_a_player_hammer.inc:20`) | player model | |
| Empty bottle `0x50`+ | `setBottleModel` (`d_a_player_bottle.inc:29`) | player model + `mpBottleContentsModel`/`mpBottleCapModel` | |
| Hookshot `0x2F` | `fopAcM_fastCreate(HOOKSHOT)` **+** `setHookshotModel` (`d_a_player_hook.inc:45`) | model + **separate actor** `daHookshot` | the actor is the tip and chain |
| Boomerang `0x2D` | `fopAcM_fastCreate(BOOMERANG)` → `mActorKeepEquip` | **separate actor** `daBoomerang_c` | no player model; the actor draws itself in the hand |
| Grappling hook `0x25` | `fopAcM_fastCreate(HIMO2)` → `mActorKeepEquip` | **separate actor** `d_a_himo2` | |
| Bomb bag `0x31` | `fopAcM_fastCreate(BOMB, prm_make(STATE_3))` → **`mActorKeepGrab`**, `setCarryNow`, GRABWAIT upper anim, `dComIfGp_setItemBombNumCount(-1)` | **separate actor** `daBomb_c` carried like a pot | `mEquipItem` goes back to `NONE`. A held bomb *is a grab* |
| Arrows | `makeArrow` (`d_a_player_bow.inc:69`) → `fopAcM_fastCreate(ARROW)` → `mActorKeepEquip` | **separate actor** `daArrow_c` | fired in `checkNextActionBowReady` (`:139-150`): `fopAcM_SetParam(arrow,1)` |
| Pots, rocks, barrels | existing world actor → `mActorKeepGrab` (GRAB procs) | **world actor** (`daTsubo` etc.) | `setGrabItemPos` moves it every frame (`d_a_player_grab.inc:68`) |

Every model comes from the resident **"Link"** archive: `initModel` → `dComIfG_getObjectRes("Link", idx)` (`d_a_player_main.cpp:12074`). The boomerang (`d_a_boomerang.cpp:746`), arrow (`d_a_arrow.cpp:20,94`) and bomb (`d_a_bomb3.inc:1342`) actors also use "Link". So **no archive loading is needed in any stage**.

### Drawing
`daPy_lk_c::draw` draws `mpEquipItemModel`, bottle contents and cap, and patches the hookshot tip joint from the actor (`d_a_player_main.cpp:1966-1995`). `setItemModel` attaches the model to a hand every frame: left hand for leaf/sword/tact/hammer/hookshot/bottle/camera, right hand for the bow, a custom transform for the telescope (`:1100-1200`). **`puppet_draw.c:817-893` already mirrors this whole path**, and `puppet_execute.c:1373` already calls `setItemModel`. Model-only items therefore need no new draw code.

### Player procs per item (`d_a_player_main.h:414-632`, per-frame funcs in `d_a_player_main_data.inc:223-389`)
- Boomerang: `0x80 BOOMERANG_SUBJECT`, `0x81 BOOMERANG_MOVE`, `0x82 BOOMERANG_CATCH`. The throw itself is an **upper-body anim** (`BOOMTHROW`), and `checkItemAction` calls `throwBoomerang()` at a HIO frame (`d_a_player_main.cpp:3925`).
- Hookshot: `0x83/0x84/0x85` (SUBJECT/MOVE/FLY). Bow: `0x94/0x95`. The shot happens inside per-frame `checkNextActionBowReady`.
- Bomb and pot: `0x6E-0x75` (GRAB_READY/UP/MISS/THROW/PUT/WAIT/HEAVY_WAIT/REBOUND). The release happens in per-frame `procGrabThrow` (`d_a_player_grab.inc:549-575`: `speedF/speed.y` from HIO, `freeGrabItem`, camera `ForceLockOn`).
- Deku Leaf: `0x92 FAN_SWING`, `0x93 FAN_GLIDE`. Hammer: `0x51-0x54`. Wind Waker: `0x9A-0x9D`. Bottles: `0xA3-0xA6`. Telescope/Picto: `0x00 SCOPE`. Ship variants: `0x8A-0x8E` (`puppet_execute.c` already seats these).
- Many "holding" poses are just the **UPPER_MOVE2 anim** layered on normal locomotion: BOOMWAIT, ARROWSHOOT/ARROWRELORD, HOOKSHOTWAIT, GRABWAIT, TAKE*/REST.

**State a puppet needs:** `mEquipItem` (already in the slot), the peer's UPPER_MOVE2 anim id (C# already reads `0x301C` and writes it to `ANIM_ID` slot `+0x12`, which the REL ignores today), the proc (already sent), a grab kind (bomb or pot; new), and aim angles `mBodyAngle.x/y` (`d_a_player.h:491`, `+0x2B4`; new).

---

## 2. Tier A: visual only (show the item, play the use anims)

### What works as-is
The puppet has its **own item heaps**. `playerInit` → `createHeap` makes `mpItemHeaps[0..1]` (0xE600 each, adjusted), `mpItemAnimeHeap` and `m_item_bck_buffer` (`d_a_player_main.cpp:12035, 12255-12265`), and `setItemHeap` flips between them (`:569`). Calling `setBowModel` / `setScopeModel` / `setSmallFanModel` / `setHammerModel` / `setBottleModel` / `setPhotoBoxModel` / `setTinkleCeiverModel` / `setTactModel` / `setHookshotModel` on the puppet builds a real model in *its* heap. That is the same mechanism `puppet_setSwordOut` uses for the blade. All of these, plus `makeItemType`, `setAnimeUnequipItem`, `setActAnimeUpper`, `resetActAnimeUpper`, `setSingleMoveAnime`, `freeGrabItem`, `procGrab*_init`, `procBoomerangSubject_init`, `procBowSubject_init` and `procFanSwing_init`, are **already in `GameMod/include/ww_linker.ld`**.

### Recipe (REL only, no protocol change for step 1)
1. In `puppet_readNetworkState`, extend today's sword-only equip mirror (`puppet_execute.c:1777-1804`). For a model-only item `X`, set `m3562 = X` and call `setAnimeUnequipItem(X)`, which plays TAKEL/TAKER/TAKEBOTH (`d_a_player_main.cpp:3498`). `checkItemAction` (already called every frame, `puppet_execute.c:876`) then swaps it in at the take frame and calls `makeItemType`. That path is model-only for these items. Put it away with `setAnimeUnequip`. Keep the equip swap inside the guard.
2. **Never let `makeItemType` run for boomerang, hookshot, grappling hook or bomb** (see the owner problem in 3). For those, set `mEquipItem` yourself and either call `setHookshotModel` directly (the body without the actor; `puppet_draw.c:826` already NULL-checks the tip actor), or draw a REL-owned model: see 4.
3. Item procs: add cases to `puppet_applyProcInit` where the `_init` is harmless. The boomerang, hookshot and bow SUBJECT/MOVE inits only set anims plus `dComIfGp_setPlayerStatus0`, which the guard undoes.
   - `procBowSubject_init` → `setBowReadyAnime` → `mSwordAnim.changeBckOnly`. That asserts unless `setBowModel` ran first: the same `mAnm != NULL` trap as the sword (`m_Do_ext.cpp:464`).
   - **Must NOT call** `procBottleDrink_init` / `procBottleOpen_init` / `procTactWait_init(-1)`. They call `dComIfGp_event_compulsory(this)` and `StartEventCamera` (`d_a_player_bottle.inc:96,119,212,239`; `d_a_player_tact.inc:205`), which is a global event. Use the `puppet_initBoatPose` trick instead: `procWait_init`, then `setSingleMoveAnime`/`setActAnimeUpper` with the right anim, then relabel `mCurProc`.
   - `procScope_init` plays a *system* SE locally (`d_a_player_main.cpp:5907`). Skip that one or accept the sound.
4. Mirror the peer's **UPPER_MOVE2 anim** from `ANIM_ID` (already written by C#) with `setActAnimeUpper(idx, UPPER_MOVE2_e, ...)`, against a whitelist (BOOMWAIT, HOOKSHOTWAIT, ARROWSHOOT/RELORD, GRABWAIT, ...). This covers "aiming while walking" without new procs.
   - **Crash trap:** if the puppet ever plays **BOOMTHROW** with `mActorKeepEquip` empty, `checkItemAction` → `throwBoomerang` → `fopAcM_SetParam(NULL)` (`d_a_player_boomerang.inc:25-27`; `d_a_player_main.cpp:3920-3927`). Either never mirror BOOMTHROW/BOOMCATCH/ROPETHROW, or play them with the frame past the throw point.
5. Aim: send `mBodyAngle.x/y` and write it to the puppet's `mBodyAngle` so bow and boomerang pitch match.

### Held bomb (Tier A)
- Option (a), cheapest: the REL draws its own Link `BDL_BOMB` model between the puppet's hands, at `(mLeftHandPos+mRightHandPos)/2` like `setGrabItemPos` (`d_a_player_grab.inc:136`). Add GRAB procs with the GRABWAIT upper anim.
- Option (b): run the real `makeItemType` bomb path. That spawns a **live bomb with a 150-frame fuse** (`d_a_bomb3.inc:1454`) in our world. It is real (explodes in the puppet's hands), but it also decrements the **local** bomb count through `dComIfGp_setItemBombNumCount(-1)`. That is play `+0x48E4` = gameInfo `+0x5B84`, which **is not in `PuppetGlobalGuard` today** (the guard covers life and magic only, `puppet_execute.c:44-45`). Add it, and arrow count `+0x48E0` = `+0x5B80`.

### Risks
- **Shared model data** (same class as the outfit issue): item `J3DModelData` is shared with the local Link's copy.
  - `mSwordAnim.entry` writes joint 0's mtxCalc on shared data. The bow uses `mSwordAnim` as well, and `puppet_execute.c:1354-1379` already saves and restores joint 0 for `mpEquipItemModel`. It keys on the offset, so it covers the bow automatically.
  - The Wind Waker brk (`entryTevRegAnimator` on shared data) and the telescope/leaf btk: last writer wins, so there is a small glow mismatch when both Links hold the baton. Fix with a swap around the entry like `puppet_appearance`, or ignore.
  - Bow joint callbacks read `userArea` per model, so they are safe (`d_a_player_bow.inc:20-45`).
- **Heap:** `mpItemHeaps` are sized for the largest vanilla item, so no new game-heap cost. REL-owned extra models (boomerang, bomb, arrow) need a small solid heap per puppet, the same pattern as `puppet_boat.c:161-192` (a few KB).
- **REL/ARAM:** Tier A is mostly calls into existing DOL code, likely about 2-4 KB of REL. Keep OSReports out (`ElfToRelConverter.MaxRelsArcGrowthBytes` = 64 KB guard; the `d_s_play.cpp:3439` assert is the symptom).

---

## 3. Tier B: making it real in the other world

### The core blocker: projectile actors talk to player 0, not to their thrower
`daPy_getPlayerLinkActorClass()` / `daPy_getPlayerActorClass()` = `dComIfGp_getPlayer(0)` = `play.mpPlayerPtr[0]` (`d_com_inf_game.h:781,2331`; `d_a_player.h:658`). That is always the **local** Link.
- **Boomerang:** held pose `setKeepMatrix` copies the *local* Link's left hand (`d_a_boomerang.cpp:436`). The throw direction comes from the local Link's body angle (`:500`), the return target is the local `getBoomerangCatchPos` (`:447`), and the catch calls the local Link's `returnBoomerang()` (`:609`). A boomerang the puppet spawns would sit in *your* hand and come back to *you*.
- **Arrow:** `setKeepMatrix` uses the local Link's left hand unless the creator (`fopAcM_GetLinkId`) is Zelda (`d_a_arrow.cpp:139-147, 524-555`), and `arrowShooting` fires along that matrix (`:267-272`). So a puppet arrow would fly out of *your* bow. It also spends the local player's magic (`arrowUseMp`, `:307-316`).
- **Hookshot:** chain root = local `getHookshotRootPos` (`d_a_hookshot.cpp:53,111,126,...`). **Rope (`d_a_himo2`)** is the same (`:287,407,522`).
- **Bomb: mostly independent.**
  - Its draw reads the *brk* from the local Link (`d_a_bomb3.inc:128`), which is only a shared anim.
  - Shadow and carry checks look at player 0's grab id, which is harmless when it doesn't match (`:648`).
  - The only real coupling is `field_0x6F0` (set for `STATE_3`, `:1483-1485`). It makes explode/delete call **the local Link's** `decrementBombCnt()` (`:856,1293`), which lets the local player exceed 3 bombs. Fix it by writing `*(u8*)(bomb+0x6F0)=0` right after `fopAcM_fastCreate` (synchronous) or by using another state.
  - Explosion = `mSph` r=200 `AT_TYPE_BOMB` into `dComIfG_Ccsp` (`:861-871`), plus a rumble `StartShock` on the local pad (`:885`).
- **Pots:** `daTsubo::mode_carry` checks player 0's grab id and falls through gracefully if it isn't that id (`d_a_tsubo.cpp:1918-1932`). Throw distance uses the *local* Link's `speedF` (`:1974`).

### Per-item feasibility

| Item | "Real" approach | Feasibility |
|---|---|---|
| **Bomb** | On the peer's throw or place event, `fopAcM_fastCreate(fpcNm_BOMB_e (0x128), prm_make(STATE_3,...), &pos)`, clear `0x6F0`, set `speedF/speed.y/current.angle.y` from the event and set the fuse (`setBombRestTime`, `d_a_bomb.h:156`). Or make the puppet grab it (`mActorKeepGrab.setData` + `fopAcM_setCarryNow`) and replay `procGrabThrow`'s release by hand at the throw frame, **without** the camera `ForceLockOn`. For an **authoritative explosion**, on the peer's "exploded at P" event delete our copy and spawn `prm_make(STATE_0)`, which explodes at create: attr `A1` has bit 1 → `procExplode_init` (`d_a_bomb3.inc:40-50, 1494-1495`); vanilla does this for the cannon at `d_a_canon.cpp:191`. `STATE_8` is the Tingle-Tuner bomb (Atp 2, `d_a_agb.cpp:956`). | **Good.** Vanilla already drops bombs for a remote second player. |
| KoRL cannon | `prm_make(STATE_4,...)` like `d_a_ship.cpp:4021` / `d_a_obj_canon.cpp:242` | Good (same as bombs) |
| Boomerang | Real actor needs its "player" to be the puppet. Option: give the spawned instance a **per-instance `sub_method` copy** (`fopAc_ac_c::sub_method` @ `0xEC`, `f_op_actor.h:274`) whose execute/draw wrappers swap `play.mpPlayerPtr[0]` to the puppet and back, inside the guard. `returnBoomerang` would then run on the puppet and write its `mActorKeepEquip`. | **Hard / risky (?)**. Anything else inside that execute that reads player 0 (camera, attention, status) sees the puppet. Untested idea |
| Arrow | Same swap trick (spawn with param 1, one execute fires it), plus stop `arrowUseMp` (guard magic, already done) and `dComIfGp_setItemArrowNumCount` (add to the guard). Fire/Ice/Light spawn effect actors (`d_a_arrow.cpp:382-405`). | Hard (?) |
| Hookshot | Pulls the *owner* to the target; the peer's position already comes from the network. Only the visual chain is useful. | Visual only |
| Pots, rocks, barrels | No cross-game actor id. Match by `fpcNm` (e.g. `TSUBO 0x1CB`) + `home.pos` + room, then puppet `mActorKeepGrab.setData` + `fopAcM_setCarryNow` so it leaves its spot in our world. Throw the same way as bombs. Items dropped from a broken pot are random per game (fine). | Medium-hard; later |

### What breaks, and the decisions to make
- **Double effects are OK for enemies.** Enemies are not synced: each game has its own. A copied bomb or arrow damaging *our* enemies is the right result. Damage is then "attributed" to nobody (no player owner), which is fine for kills and drops. It is not fine for anything that credits "Link" (**(?)** some enemies check `fopAcM_GetLinkId` / at-hit actor type).
- **Friendly fire:** the bomb AT hits the local Link's Tg cylinder, as vanilla self-damage does. That needs a room rule ("shared combat / friendly fire"). Hurting the *peer* (PvP) would need a damage event back to them; out of scope.
- **Walls and switches:** a bombable wall sets a stage switch that shared-world OR-merges, so both games converge. Worst case, the two simulations disagree (the copy bounced differently) and only one game breaks it live; the switch then fixes the other on reload. **(?)** Whether a live-received switch removes an already-loaded wall is unverified; it probably needs a reload. The authoritative-explosion design (explode at the peer's position) avoids most of the disagreement.
- **Rumble:** the copy's explosion calls `StartShock` on the local pad (`d_a_bomb3.inc:885`). Spawn and advance outside the guard, or accept it; it is vanilla behaviour when a bomb explodes near you.
- **Latency:** a 150-frame fuse and 100-200 ms of lag put the copy about 6-12 frames behind. Use the explode event to snap.
- **Scene changes:** spawned actors belong to the room. Delete any pending copies on a scene or room change (the puppet delete path).

---

## 4. Protocol and memory impact

**Already on the wire:**
- `Equipment.ItemInHand` = `mEquipItem` → slot `EQUIP_ITEM +0x2C` (`PuppetSyncService.cs:876,1073`).
- `Animation.UpperBodyAnimation` → slot `ANIM_ID +0x12`, unused by the REL.
- `Action.CurProc` + `PROC_SEQ`.
- Also present but unused: `Equipment.LeftHandItem`, `BottleContents` (`PuppetData.cs:210-216`).

**Slot space.** The slot is 0x48, and its last field is at 0x44. There is room:
- **REL-unused slot fields** C# still writes: `ANIM_SPEED +0x14` (f32), `STATE_FLAGS +0x18` (u32), pad byte `+0x1F`, `SEQ_NUM +0x26`, `SPEED_F +0x30` (duplicate of `VELOCITY_F`, which is what the REL reads), `MODE_FLG +0x3C`, `NO_RESET_FLG0 +0x40` ("deliberately not synced", `puppet_execute.c:1682`). That is about 20 bytes per slot to repurpose, with a re-patch and a protocol bump.
- `PUPPET_SLOT_SIZE` can grow **0x48 → 0x50** with nothing moving: `0x803FD010 + 3*0x50 = 0x803FD100` = `PUPPET_PROC_IDS_ADDR`, so the existing `puppet_check_slots_before_tracking` still holds.
- New per-slot state, about 8 bytes: grab kind (u8: 0 none, 1 bomb, 2 pot...), bomb fuse (u8), `bodyAngleX/Y` (2 × s16), upper-anim frame (u8).

**Events (one-shot) should not go in the slot.**
- **Inbound:** use a game-heap block like the names block (`puppet_shared.h`: the REL allocates it, publishes the pointer, and stamps it with `__OSStartTime`). The last free scratch word, `0x803FD16C`, can be its pointer (`PUPPET_FX_PTR_ADDR`). Put a per-slot ring of about 4-8 events in it: `{seq, type, pos xyz, angleY, speedF, speedY, param}`. C# writes; the REL consumes by seq.
- **Outbound:** C# polls at 20 Hz and misses one-frame events such as the arrow release (`daPyRFlg0_ARROW_SHOOT` is a reset flag), the bomb release frame, the boomerang throw frame and an explosion. So the **REL** (which runs every frame while any puppet exists, parked or not) should watch the local Link and write an **outbox ring** in the same block for C# to drain. The local Link fields it needs:
  - `mActorKeepGrab @0x318C` → actor → `mProcName @+0x08` (`f_pc_base.h:16`)
  - `mActorKeepThrow @0x3184` (boomerang in flight) → actor `current.pos @+0x1F8`
  - `mActorKeepEquip @0x317C`
  - `mCurProc`
  - bomb `mRestTime`
- **Network:** add a reliable hub method `SendPlayerEvent` / `ReceivePlayerEvent(PlayerEvent)` (bump `HubConstants.ProtocolVersion`). `GameHub` validates it: type enum, finite position within `MaxCoordinate`, and a rate limit of about 10/s. Gate it on a new `RoomSettings` rule. Keep the stream-style data (boomerang or hookshot-tip position while in flight) in `PuppetData` at 20 Hz, interpolated by the REL.

---

## 5. Recommended staged plan

| Stage | What | Needs | Effort | Risk |
|---|---|---|---|---|
| **0. Held models** | Puppet shows bow, telescope, Picto Box, Tuner, Deku Leaf, Wind Waker, hammer, bottles and the hookshot body, with take-out/put-away anims | REL only. `mEquipItem` is already in the slot; `setXModel` / `makeItemType` for model-only items; exclude actor items | ~1 day | Low. Main traps: a bow proc before `setBowModel`, and never calling `makeItemType` for boomerang/hookshot/rope/bomb |
| **1. Use poses** | Mirror the UPPER_MOVE2 anim (whitelist) and item procs (boomerang/hookshot/bow SUBJECT+MOVE, grab procs, fan swing, hammer swings, relabelled bottle/tact/scope poses) plus aim angles. REL-drawn held **boomerang** (left hand, `d_a_boomerang.cpp:436-440` transform) and held **bomb** model (between hands) | Slot 0x48→0x50 (grab kind, body angles), C# reads `mBodyAngle` + grab actor name; add bomb and arrow counts to the guard | 2-3 days | Low-medium. BOOMTHROW crash trap; no event-starting inits |
| **2. Visual projectiles** (no damage) | Ghost boomerang (peer's thrown-actor pos streamed at 20 Hz + lerp, spinning model), ghost arrow (event pos+angle, straight flight at 200/frame with a BG line check to stop), ghost bomb (event + simple gravity/bounce, or a real bomb with collision off **(?)**), explosion effect only | Event channel (hub method, heap block + outbox in the REL, 1 scratch word) | 3-5 days | Medium: mostly plumbing. No world effects, so nothing to desync |
| **3. Real bombs** | Real `daBomb` copies: spawn on the throw/place event, clear `0x6F0`, set fuse and velocity; authoritative explosion (`STATE_0` at the peer's position, delete the copy). Damages our enemies, breaks our walls, hurts the local Link if the rule allows. Add the KoRL cannon (`STATE_4`) | Stage 2 channel + a room rule (friendly fire / shared combat) | 2-4 days | Medium: double effects, rumble, timing. World switches reconcile through shared world |
| **4. Real arrows/boomerang** (arrows done, section 8: detached, no swap needed) | Per-instance `sub_method` wrapper that swaps `mpPlayerPtr[0]` to the puppet around the actor's execute/draw, inside the guard | Stage 2 | 1-2 weeks, **research spike first** | High (?): anything reading player 0 inside those executes; untested |
| **5. Carried world objects** | Pots, barrels, rocks: identity match (name + `home.pos` + room), puppet grabs our copy, throw like a bomb | Stage 2-3 | 1 week | High: races with the local player picking the same pot, room reloads |

**Recommendation:** do 0 → 1 first. They are cheap and highly visible, and stage 0 is REL-only because `mEquipItem` already arrives. Then build the event channel once (stage 2), since every "real" feature rides on it. Make bombs the first real projectile (stage 3), because the game already supports a second player's bombs. Treat arrows and the boomerang as real only after a spike proves the player-pointer swap is safe; until then they stay visual.

---

## 6. Implemented: stages 0 and 1

Code: `GameMod/src/puppet_link/puppet_held.c` (REL), called from `puppet_execute.c` / `puppet_draw.c` / `puppet.c`; C# `HeldItemState` + `PuppetSyncService`.

**Correction to section 4:** `Animation.UpperBodyAnimation` was never filled in before this change (C# read `0x301C` but didn't send it), so `ANIM_ID` was always 0. It is sent now.

### Protocol and layout
- Slot `0x48 → 0x50` (3 slots now end exactly at `PUPPET_PROC_IDS_ADDR`): `BODY_ANGLE_X` s16 `+0x48`, `BODY_ANGLE_Y` s16 `+0x4A` (`mBodyAngle`, Link `+0x2B4`), `GRAB_KIND` u8 `+0x4C` (`PUPPET_GRAB_KIND_NONE/BOMB`: `mActorKeepGrab.mActor`'s `mProcName == fpcNm_BOMB_e`). Needs a re-patch (build stamp).
- `PuppetData`: `Action.BodyAngleX/Y`, `Equipment.GrabKind` (the server clamps a kind above `EquipmentState.MaxGrabKind` to 0 instead of dropping the update, so a newer client's puppet keeps moving), `Animation.UpperBodyAnimation` now set. `HubConstants.ProtocolVersion` 1 → 2: an old server would silently strip the new fields.
- `ItemInHand` is `m3562` during REST **and** TAKE/TAKEBOTH/TAKEL/TAKER, so both Links start the same anim together.

### Stage 0: held models
`puppet_heldMirror` (replaces the sword-only mirror). Items: bow (3 ids, one model), telescope, Picto Box (both), Tingle Tuner, Deku Leaf, Wind Waker, Skull Hammer, bottles `0x50-0x59` (liquid/fairy/firefly + cap via `setBottleModel`; Forest Water shown as water, no emitter), hookshot (body only: `setHookshotModel` without the actor), boomerang (REL-drawn). Built in the puppet's own item heaps.
- The peer in a draw/put-away anim → the same vanilla anim (`setAnimeUnequipItem` / `setAnimeUnequip`), with `m3562 = 0x1000 | item`: `checkItemAction`'s swap then hands `makeItemType` a value it ignores, and `puppet_heldFinishSwap` builds the item (never an actor). Otherwise (a throw, an item proc ending, an old client) the change is instant.
- The sword keeps its REST draw/sheathe.
- Shared model data: the item setters' btk/brk/bpk registrations (sword, bottle, Wind Waker) land in the "Link" archive's shared J3DMaterialAnm slots. `puppet_sharedanm.c` keeps them in the puppet's own swap list and writes them in only around the puppet's execute and draw, so no Link draws with another's (possibly freed) anm. The Picto Box flash shape (hidden for the regular box, shown for the Deluxe) is set only around each puppet's entry, and `setPhotoBoxModel`'s change is undone right after the build.

### Stage 1: use poses
- Real `_init`s (safe: anims, mode flags, guarded status/magic, AT bits cleared): BOOMERANG_SUBJECT/MOVE/CATCH, HOOKSHOT_SUBJECT, BOW_SUBJECT/MOVE (only once `setBowModel` has built `mSwordAnim`), FAN_SWING, FAN_GLIDE, the four hammer procs (only once `setHammerModel` has). Each item proc first puts its item in hand (`puppet_heldForProc`).
- Relabelled poses (WAIT + the init's `setSingleMoveAnime`, `mCurProc` = the peer's proc): GRAB_READY/UP/THROW/PUT/WAIT (the inits read the grabbed actor), HOOKSHOT_FLY (reads the hookshot), TACT_WAIT..TACT_PLAY_ORIGINAL (global event + camera), BOTTLE_DRINK/OPEN/SWING/GET (event + camera / catch target), DEMO_AGB_USE (Tuner; system SE). HOOKSHOT_MOVE = ATN_MOVE + HOOKSHOTWAIT, relabelled.
- BOOMERANG_MOVE / HOOKSHOT_MOVE / BOW_MOVE blend like ATN_MOVE from the peer's stick.
- Upper-body mirror from `ANIM_ID` (whitelist, with the peer's own `setActAnimeUpper` values): BOOMWAIT, BOOMTHROW, BOOMCATCH, HOOKSHOTWAIT, ARROWRELORD/ARROWSHOOT/BOWWAIT (+ the bow string bck), GRABWAIT. `checkItemAction` is skipped while one of these holds UPPER_MOVE2 (the BOOMTHROW trap). The mirror only resets anims it owns and never interrupts a draw/put-away.
- Item bck frame (`m35EC`) follows the body anim for fan/hammer and the upper anim for the bow, as their per-frame procs do.
- Aim: the peer's `mBodyAngle` x/y is applied after execute's decay, before the model calc, while the peer is in SCOPE, BOOMERANG/HOOKSHOT/BOW SUBJECT/MOVE or SHIP_SCOPE..SHIP_BOW.
- REL models: `puppet_held.c` builds `BDL_BOOMERANG` and `BDL_BOMB` from the resident "Link" archive in a 0x8000 solid heap per puppet (adjusted; built on first need, destroyed with the puppet). Boomerang in the left hand (`daBoomerang_c::setKeepMatrix`), bomb between the hands (`setGrabItemPos` / `daBomb_c::set_mtx`), bind pose (the shared bomb joint 0 calc is cleared and restored), fuse brk frame 0. Since the projectiles work the carried bomb's fuse animates too: see section 7, "The carried bomb's fuse".
- Global guard: pending arrow (`+0x5B80`) and bomb (`+0x5B84`) counts added (no puppet path should reach them).

### Size
Measured on origin/main 0f6baa1: REL 40,904 → 47,308 bytes (+6,404; `.text` +4.8 KB, `.rodata` +0.6 KB, ~120 relocations), RELS.arc growth 58,304 of the 65,536-byte guard. The review fixes (ship baton, Picto flash) add 372 bytes (REL 47,680), so about 58.7 KB with ~6.9 KB left. The boat cannon/crane PR adds ~2.6 KB more, so stage 2 needs space reclaimed first.

### Verify in game
1. Take out / put away each item on one Link; the other shows the same model and anim at the same time; swap item → item and sword ↔ item.
2. Bow: aim (standing and strafing), pitch up/down, shoot, reload; no assert. Magic/light arrows keep the bow.
3. Boomerang: aim, throw (the puppet's boomerang vanishes mid-throw), catch; **no crash on the throw** (the BOOMTHROW trap).
4. Hookshot: aim standing/strafing, fly pose; the body is in hand, no chain.
5. Hammer swings, Deku Leaf swing and glide, Wind Waker (stance only), bottle swing / drink / open; the local Link's bottle liquid and baton glow stay right while both hold one.
6. Carry a bomb: the puppet holds a bomb between its hands; throw → the pose; the local bomb count never changes. A pot shows the carry pose with nothing in the hands.
7. Telescope / Picto Box: the puppet holds it; the aim angles tilt the body.
8. Peer leaves/joins mid-use, room change while holding each item: no crash, no stray models.

### Left out (by design or risk)
- No projectiles or real effects in stages 0-1 (bombs and the boat cannon are real since stages 2-3, section 7): no flying boomerang, arrows, hookshot chain/tip; the held boomerang is a model only.
- Grappling hook (a rope actor) and carried pots/barrels/rocks: no model; carrying shows the pose only.
- Wind Waker conducting beats (per-frame UPPER_MOVE1/2 ratios) and song playing: stance only. Deku Leaf glide: static parachute pose (the morf isn't played). Bottle procs: the first anim of each only.
- GRAB_MISS, GRAB_HEAVY_WAIT, GRAB_REBOUND, ROPE_*, DEMO item procs other than DEMO_AGB_USE: WAIT.
- Forest Water's sparkle emitter (bound to the model; would outlive it).

---

## 7. Implemented: stages 2 and 3 (bombs and the boat cannon)

Code: `GameMod/src/puppet_link/puppet_fx.c` (REL), the events block in `puppet_shared.h` (`PUPPET_FX_*`); C# `PlayerEventBlock` + `PlayerEventService` (client), `PlayerEventRelay` + `GameHub.SendPlayerEvent` (server), `PlayerEvent` (Shared). Room rule: `RoomSettings.SharedProjectiles` ("Other players' projectiles", on in both presets). `HubConstants.ProtocolVersion` → 4.

Stage 2 (the event channel) and stage 3 (real bombs) were built together. The "ghost bomb" step was skipped: a real `daBomb_c` copy is as cheap as a ghost and does everything a ghost would.

### The channel
- **Outbox (REL → C#).** Once per frame (any puppet's execute, parked ones too), `fx_trackLocal` watches the local Link:
  - A bomb in `mActorKeepGrab` (`+0x318C` id) that is a `daBomb_c` in STATE_1..3 and not a copy is tracked as CARRIED.
  - The frame after it leaves his hands (`freeGrabItem` cleared the grab: a throw or a put-down), it reports **BOMB_THROW** with the bomb's state then: `current.pos`, `speedF`, `speed.y`, `gravity`, `current.angle`, `mRestTime` (fuse).
  - Carried again → **BOMB_PICKUP**. `mRestTime == 0` (`procExplode_init` zeroes it; a live bomb's is > 0) → **EXPLODE** at its position, in his hands or not. Gone without exploding (sank, or the stage ended) → **REMOVE**.
  - The boat cannon: the frame `daSFLG_SHOOT_CANNON` is set on the local ship (`play.mpPlayerPtr[2]`, `mStateFlag +0x358`; set at the shot, `d_a_ship.cpp:4030`, cleared at its next execute, `:3594`), `fopAcIt_Judge` finds the one BOMB whose params are exactly the ship's (`prm_make(STATE_4, FALSE, TRUE)` = `0x80020004`; enemy cannons and ships use cheapEff = TRUE). **CANNON** reports its launch state (the no-gravity frames in TIMER); then EXPLODE / REMOVE like a bomb.
- **C#** drains the outbox at 20 Hz, acks it, stamps each event with our stage, the projectile's own room (the REL writes its `current.roomNo`: a bomb thrown in room 1 still explodes in room 1 after its thrower walked on; our room only when the REL didn't know it), the app run's **origin** (a random id that survives reconnects) and a **seq**, and sends it (`SendPlayerEvent`). An event stays queued until it went out, or 2 s.
- **Server** (`PlayerEventRelay`):
  - `PlayerEvent.IsValid`: a known kind; a finite position within ±1e6; |speedF| and |speed.y| < 1000; |gravity| < 50; timer 0..600; variant 0; a stage name of 1-8 printable characters. Ranges only, never `Math.Abs` on ints.
  - The sender must have sent puppet data; the event must be on the sender's stage and within 20000 units of them; a token bucket per sender (burst 20, 10/s).
  - It relays to the players in the room where the event happened, by the event's own stage and room (a bomb thrown in room 1 explodes for room 1 even after its thrower walked into room 2), at sea to those within the sea hide distance of the event (`PuppetVisibility`, now in Shared); never back to the sender, and nothing while the rule is off.
  - A location with no puppet update for 3 s is forgotten: a player whose game is detached neither sends nor gets events.
  - An origin belongs to the connection that first used it (one origin per connection) until that connection leaves, so nobody can send under another player's origin with a huge seq to make receivers drop that player's events; a reconnecting client takes its origin back once its old connection is gone, or once that connection's location is stale (no puppet data for 3 s; after an unclean drop the server only notices the old connection at its 30 s client timeout).
- **Inbox (C# → REL).** The receiver drops a repeated or older seq from the same origin (`PlayerEventDedup`; also an event re-sent under a new connection id after a reconnect), events older than 2 s (at most 32 queued, and stale ones are dropped while the game is detached), events that didn't happen in our stage and room (at sea: within sight of us), and senders without a puppet slot; then it writes the event with the sender's puppet slot. The REL checks it again (kind, slot, finite ranges) and applies up to 4 events a frame. While a stage change is pending it discards the inbox (those events belong to the old stage).

### The copies (viewer)
Real `daBomb_c`s made with `fopAcM_fastCreate`, like the game's own second-player bombs (the Tingle Tuner's, `d_a_agb.cpp:956`), in the puppet's stage layer:
- **BOMB_THROW** → a STATE_1 bomb (lit: fuse sparks, smoke, hiss) at the sender's position, with their velocity, heading and fuse. A repeat for the same bomb re-places it.
- **CANNON** → the ship's own STATE_4 cannonball with the sender's launch state and no-gravity frames. It flies with the fly sound, then explodes on ground, walls or anything it touches, or splashes into the sea.
- **EXPLODE** → the copy is put where the sender's exploded and `procExplode_init` runs: the vanilla explosion (flash, smoke, debris, sound, light, wind, rumble, AI noise, AT sphere r=200). With no copy (it blew up in the sender's hands, or ours sank) a STATE_0 bomb appears there and explodes at create (as `d_a_canon.cpp:191`).
- **BOMB_PICKUP** → the copy goes (the puppet's carried-bomb model from stage 0 shows it). **REMOVE** → the copy gets 30 frames to sink here too, then goes.
- **Timing and de-dup:** each copy is keyed by (sender slot, the sender's bomb id = its process id there). Its fuse counts like the sender's, but is held at 3 frames for up to its fuse + 60 frames, so it explodes when the sender's EXPLODE arrives, at their position (not a latency early at a slightly different spot). If the EXPLODE never comes, its own fuse ends it. The hold only lasts while the copy is as spawned: once something here carries it (the local Link, an enemy), changes its state (an Armos swallowing it, `d_a_am.cpp:400`) or sets its fuse lower than the hold leaves it (`setBombRestTime(1)`, `d_a_am.cpp:942`), it is this world's bomb: its own fuse ends it where it is and the sender's EXPLODE is ignored. A copy that exploded here first (a sword, another explosion) ignores the sender's later EXPLODE. Finished entries are remembered for 300 frames, so late duplicates do nothing.

### The carried bomb's fuse
While the peer carries a lit bomb, the puppet's held bomb (stage 0's REL model) flashes and swells like the real one:
- The sender reads the carried bomb's `mRestTime` (`daBomb_c +0x6FC`) with the grab kind and sends it in `PuppetData.Equipment.GrabFuse` (a byte, capped at 255; 0 = none). It goes into the slot's spare byte after `GRAB_KIND` (`PUPPET_SLOT_OFF_GRAB_FUSE` `+0x4D`); the slot stays 0x50.
- The REL counts it down one per frame between the 20 Hz updates and snaps to each new value. The draw sets the shared bomb brk's frame to `end - fuse + 2` and plays the archive's `BCK_BOMB` at `end - fuse`, as `daBomb_c::draw_norm` does (`d_a_bomb3.inc:128-148`). The bck plays through the puppet's own `mDoExt_bckAnm` (its `J3DMtxCalcMayaAnm` lives in the held-model heap); joint 0's calc is put back right after the entry, so nothing of the puppet's stays in the shared bomb data, inside or outside a puppet's shared-animator window.
- If the fuse runs out in the peer's hands, their EXPLODE event makes the explosion. The fuse sparks and smoke particles aren't drawn on the carried bomb (they are on the thrown copy).

### Traps handled
- **`field_0x6F0` / bomb count:** only STATE_3 sets it, and it makes explode / delete call the LOCAL Link's `decrementBombCnt`. Copies are never STATE_3 and get it cleared anyway. Nothing on this path touches the pending bomb count (gameInfo `+0x5B84`, still in the puppet guard).
- **Echo:** every copy has `0x01000000` in its params. Bits 8-15 and 18-30 are unused (`prm_get_state`, cheapEff, angXZero and version read bits 0-7, 16, 17 and 31, and `change_state` keeps the rest), so the tracker never reports a copy, even after the REL was reloaded. The local Link may pick a copy up and throw it: it is then his, and explodes on its own fuse where it is (no snap back).
- **Player 0:** a copy reads the local Link only for the shared bomb brk (`draw_norm`) and his grab id (`set_real_shadow_flag`), both harmless. It is never in his `mActorKeepGrab` unless he picks it up himself.
- **Stage changes:** nothing is spawned or reported while `mNextStage` is pending, and the inbox is discarded; copies die with the stage layer.
- **The local Link** is `play.mpPlayerPtr[0]`, not `dComIfGp_getPlayer(0)`: that is the NPC the player controls while playing Medli, Makar, a seagull or Hyoi (`procExplode_init` reads the same pointer).
- **Minigames:** the client drops peers' events while `play.mMiniGameType` is set (the sailing race, Orca's training, Spectacle Island's cannon game, the auction, mail sorting, the bow game), so a peer's shot can't score in the viewer's minigame. Only `endMiniGame` clears that byte (a reset re-zeroes it: `play` is in the DOL's .bss), so a minigame left without ending it (a game over, back to the title) would stick; no minigame spans a stage change, so a type only counts in the stage where it was first seen (the gate reads it every tick), and a new type starts afresh, since `startMiniGame` can overwrite a stale type with no 0 between (`MinigameGate`).

### Player damage: none
A copy's AT has `cCcD_AtSPrm_VsPlayer_e` cleared (`+0x5A0`; `procExplode_init` only ORs the Set bit back). It breaks walls and boulders, hurts enemies and sets off other bombs (the enemy and other groups), but never hurts the local Link, his ship or puppets (the player Tg group; also Medli, Makar and a few objects with player-only Tg).
- The copy lands ~100-200 ms after the sender's and may bounce differently, so a hit would feel unfair.
- There is no friendly-fire rule yet, and a co-op room shouldn't let one player bomb another. A future "friendly fire" rule would just keep the bit.
- The explosion's rumble, light and sound stay (vanilla, as for any bomb going off near you).

**World state:** a bombable wall or boulder the copy breaks here is broken for real in this world. Its switch reaches the room through shared world, as the sender's own does (live world, #12, applies it without a reload). If the copy and the original disagree, the switch reconciles both.

### Size
Measured on origin/main d5e4dfb (REL stored Yaz0): REL 51,632 → 57,792 bytes (+6,160; `.text` +5.0 KB), RELS.arc growth 27,392 → 30,272 of the 65,536-byte guard (+2,880).

### Verify in game
1. Take out a bomb, walk, throw it. The other player sees the puppet carry it, then a lit bomb fly the same arc, bounce and explode when and where the thrower's does (effect, sound, light, rumble). The viewer's bomb count never changes, and neither Link is hurt by the other's bomb.
2. Put a bomb down, pick it up again, throw: the copy disappears while it is carried and comes back on the throw.
3. Let a bomb explode in hand: an explosion at the thrower's hands on the other screen.
4. Throw a bomb at a bombable wall, a boulder, an enemy: it breaks or takes damage in both worlds.
5. Throw a bomb into deep water: it sinks on both sides, no explosion.
6. The viewer picks up the peer's lying bomb and throws it: it explodes where the viewer threw it.
7. Boat cannon: fire at sea, at a target and at an island. The other player sees the cannonball leave the peer's cannon, fly, and explode or splash where the shooter's does.
8. The room owner turns "Other players' projectiles" off: nothing spawns (the carried bomb model and aim poses still show). On again: it does.
9. Throw a bomb and leave the room or change stage before it explodes; the peer leaves mid-flight: no crash, no stray bombs.
10. Three players: each sees the other two's bombs, and nothing spawns twice.

### What's left
- Arrows: section 8.
- Boomerang, hookshot chain and tip, grappling hook: visual only or not at all (they are bound to the local Link).
- Carried pots, barrels, rocks and bomb flowers (`daBomb2`): stage 5.
- A friendly-fire room rule (copies would keep VsPlayer), and PvP damage back to the thrower.

---

## 8. Implemented: stage 4 for arrows (normal, fire, ice, light)

The stage 4 spike question ("does a real arrow need a player-pointer swap?") has a simpler answer: a `daArrow_c` can be made **detached** from the local Link, with no swap. `d_a_arrow` is in main.dol; every offset below is verified there.

### What the arrow reads from player 0, and why none of it matters
- `checkCreater`: the parent actor, to spot Zelda's arrows. `fopAcM_fastCreate` has no parent, so `mbSetByZelda` stays false.
- `setTypeByPlayer` / `checkRestMp` (at create): the type is the static `m_keep_type` (`0x803F6B7C`), downgraded to normal when the local magic (gameInfo `+0x14`) is below its cost (`use_mp` 0/1/1/2). The light arrow's model (`BDL_ARROWGLITTER`) and heap (0x820) are chosen here, so the type can't be fixed after create.
- `createInit` → `setKeepMatrix`: puts the arrow in the local Link's left hand, and `procWait` does that every frame until the arrow's param is 1. `procWait` on param 1 then calls `arrowUseMp` (spends the local magic) and `arrowShooting`.
- `createInit` also reads the local Link's ice / light arrow btk (shared archive anims: a harmless read).
- `procMove`, `procStop_*`, `procReturn`, `procWater`, draw and delete read no player. The only other local write is `procStop_BG`'s pickup: a CO hit on a stuck normal arrow gives the local Link +1 arrow (`dComIfGp_setItemArrowNumCount(1)`, d_a_arrow.cpp:976).
- The fire / ice / light effect child (`d_a_arrow_lighteff`, a REL) sets and clears the local Link's `USE_ARROW_EFFECT` (it blocks his bow's reload), but only once the arrow's param has been 1 (`field_0x2EA`, lighteff.cpp:208-222, 315-327).

### Sender
`fx_trackLocal` remembers the arrow nocked in `mActorKeepEquip` (`+0x317C` id; name `0x1DE`). When the bow no longer holds that id and the arrow still exists with param 1 (`checkNextActionBowReady` sets it at the shot, d_a_player_bow.inc:136), it was shot: **ARROW** with its type (`mArrowType +0x601`) as VARIANT, its launch point (`current.pos`) and aim (`current.angle` x / y). The next frame the arrow hasn't moved yet (it executes after the puppets: lists 9 and 3). Switching the arrow type deletes the nocked arrow and nocks a new one, and a put-away arrow is deleted, so neither is reported. One event per arrow: the copy flies the same straight 200-per-frame line through the same stage.

### Viewer (`fx_spawnArrow`)
1. Save `m_keep_type` and the local magic; set the peer's type, and the magic to 2 if it is lower. `fopAcM_fastCreate(ARROW, param 0, launch point, -1, aim)`, synchronous. Put both back at once.
2. `createInit` put it in the local Link's hand: set `current.pos` / `old.pos` to the launch point, `current.angle` = (x, y, 0) (up is +x, as `setKeepMatrix` sets it) and `shape_angle` = (-x, y, 0).
3. Clear its CO sphere's Set bit (`mCoSph +0x4D4`, CO SPrm `+0x500`): no pickup.
4. `mCurrProcFunc` (`+0x68C`) = the `procMove` PTMF `{0, -1, 0x800D5B20}` (the constant `procWait` copies, `0x803898B4`), then `arrowShooting()`: speed 200 along the aim, the typed shoot sound, blur, the AT capsule.
5. Its model's base matrix = trans(launch point) · ZXYrot(shape x, y, 0), so its first draw isn't in the local Link's hand.
6. Param stays 0: `procWait` never runs (no magic spent) and the effect child never touches the local Link's flag. It shows the "nocked" glow size while flying, a small visual difference.

Keyed and de-duplicated like the bombs (a repeat does nothing). The pending arrow count (gameInfo `+0x5B80`) is never touched.

`arrowShooting` registers the AT capsule in dCcS; that is the arrow's only registration in its spawn frame, because an actor `fopAcM_fastCreate`d during another's execute first executes the next frame (it enters the execute queue in `fpcCt_Handler`, before `fpcEx_Handler`: f_pc_create_req.cpp:100, f_pc_manager.cpp:284). So point-blank shots hit on that frame, as a local arrow's do.

### What peer arrows hit: the viewer's world, never the viewer
The arrow's AT is Set | VsEnemy | VsOther (`m_at_cps_src`), so it hits the viewer's enemies (the light arrow's instant kill too, except in GanonK), eye switches, torches (fire lights them), ice (fire melts), water (ice makes a platform), and sticks in walls and enemies, all as a local arrow would. It never hits the local Link, his ship or puppets (no VsPlayer bit, as vanilla). The local player's own arrows share the 5-arrow `m_count` ring, so a peer's arrows can retire their stuck arrows sooner (cosmetic). In the bow minigame, peer arrows are normal arrows (vanilla override).

### Size
REL 57,792 → 58,876 bytes (+1,084); RELS.arc growth 30,272 → 30,880 of the 65,536-byte guard (+608). After the review fixes (both sections): REL 59,084 bytes, RELS.arc growth 30,976. Rebased on origin/main d0ac5b5 (live world): REL 53,336 → 60,460 bytes, RELS.arc growth 28,416 → 31,904 of 65,536 (+3,488 for sections 7-8).

### Verify in game
1. Shoot normal, fire, ice and light arrows (standing, strafing, up and down): the other player sees the arrow leave the puppet's bow along the same line, with the right tip, glow, trail and shoot sound.
2. Hit an enemy, a wall, water, lava: it hits, sticks, bounces, splashes, freezes, makes an ice platform or a magma rock, as a local arrow would.
3. Shoot an eye switch or light a torch with a fire arrow: it triggers in the viewer's world.
4. The viewer's magic and arrow count never change; touching a stuck peer arrow gives nothing; the viewer can reload their own bow normally while peer fire / ice / light arrows fly.
5. Swap arrow types and put the bow away without shooting: nothing is sent.
6. "Other players' projectiles" off: no arrows spawn (the bow poses still mirror).
