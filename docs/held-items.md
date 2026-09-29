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
| **4. Real arrows/boomerang** | Per-instance `sub_method` wrapper that swaps `mpPlayerPtr[0]` to the puppet around the actor's execute/draw, inside the guard | Stage 2 | 1-2 weeks, **research spike first** | High (?): anything reading player 0 inside those executes; untested |
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
- Shared model data: bottle btk/brk and the Wind Waker brk are re-registered around each puppet's entry and put back for the local Link (`puppet_heldEntryItemAnms`; the local Link's baton is also `daPyItem_UNK10A` while conducting on the ship), so no Link draws with another's (possibly freed) anm. The Picto Box flash shape (hidden for the regular box, shown for the Deluxe) is set only around each puppet's entry, and `setPhotoBoxModel`'s change is undone right after the build.

### Stage 1: use poses
- Real `_init`s (safe: anims, mode flags, guarded status/magic, AT bits cleared): BOOMERANG_SUBJECT/MOVE/CATCH, HOOKSHOT_SUBJECT, BOW_SUBJECT/MOVE (only once `setBowModel` has built `mSwordAnim`), FAN_SWING, FAN_GLIDE, the four hammer procs (only once `setHammerModel` has). Each item proc first puts its item in hand (`puppet_heldForProc`).
- Relabelled poses (WAIT + the init's `setSingleMoveAnime`, `mCurProc` = the peer's proc): GRAB_READY/UP/THROW/PUT/WAIT (the inits read the grabbed actor), HOOKSHOT_FLY (reads the hookshot), TACT_WAIT..TACT_PLAY_ORIGINAL (global event + camera), BOTTLE_DRINK/OPEN/SWING/GET (event + camera / catch target), DEMO_AGB_USE (Tuner; system SE). HOOKSHOT_MOVE = ATN_MOVE + HOOKSHOTWAIT, relabelled.
- BOOMERANG_MOVE / HOOKSHOT_MOVE / BOW_MOVE blend like ATN_MOVE from the peer's stick.
- Upper-body mirror from `ANIM_ID` (whitelist, with the peer's own `setActAnimeUpper` values): BOOMWAIT, BOOMTHROW, BOOMCATCH, HOOKSHOTWAIT, ARROWRELORD/ARROWSHOOT/BOWWAIT (+ the bow string bck), GRABWAIT. `checkItemAction` is skipped while one of these holds UPPER_MOVE2 (the BOOMTHROW trap). The mirror only resets anims it owns and never interrupts a draw/put-away.
- Item bck frame (`m35EC`) follows the body anim for fan/hammer and the upper anim for the bow, as their per-frame procs do.
- Aim: the peer's `mBodyAngle` x/y is applied after execute's decay, before the model calc, while the peer is in SCOPE, BOOMERANG/HOOKSHOT/BOW SUBJECT/MOVE or SHIP_SCOPE..SHIP_BOW.
- REL models: `puppet_held.c` builds `BDL_BOOMERANG` and `BDL_BOMB` from the resident "Link" archive in a 0x8000 solid heap per puppet (adjusted; built on first need, destroyed with the puppet). Boomerang in the left hand (`daBoomerang_c::setKeepMatrix`), bomb between the hands (`setGrabItemPos` / `daBomb_c::set_mtx`), bind pose (the shared bomb joint 0 calc is cleared and restored), fuse brk frame 0.
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
- No projectiles or real effects (stages 2-5): no flying boomerang, arrows, hookshot chain/tip, thrown bomb, explosions; the boomerang/bomb are models only.
- Grappling hook (a rope actor) and carried pots/barrels/rocks: no model; carrying shows the pose only.
- Wind Waker conducting beats (per-frame UPPER_MOVE1/2 ratios) and song playing: stance only. Deku Leaf glide: static parachute pose (the morf isn't played). Bottle procs: the first anim of each only.
- GRAB_MISS, GRAB_HEAVY_WAIT, GRAB_REBOUND, ROPE_*, DEMO item procs other than DEMO_AGB_USE: WAIT.
- Forest Water's sparkle emitter (bound to the model; would outlive it).
