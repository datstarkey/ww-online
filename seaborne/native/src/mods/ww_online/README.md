# ww_online: the multiplayer mod (milestone M1)

The game side of the ww-online client on the native port (tww-native-notes `WW_ONLINE_EMBED.md`, sections
1.3, 4.2g and 4.3). Built with `COS_WITH_WWONLINE` (CMake option, on by default on macOS, where libtww is
built; off elsewhere). The game then sees `COS_WITH_WWONLINE=1`. The sources go into the static library
`cos_wwonline`, which everything linking `cos_pc` links (seaborne and libtww).

## Files

| File | What |
|---|---|
| `d_a_puppet.cpp` | `daPuppet_c : public daPy_lk_c`, profile `g_profile_PUPPET`, process name `fpcNm_PUPPET_e` |
| `ww_online.cpp` | `wwo_frame`: creates and deletes the puppets; the `COS_MP_TEST_PEER` feeder |
| `ww_api.cpp` | the multiplayer part of `native/include/pc/tww.h` (API version 2) and the peer table |
| `ww_inv.cpp` | the shared-inventory calls (`tww_inv_*`, tww.h "fan-out: inv") |
| `ww_world.cpp` | the world sync outside the save window (`tww_world_*`) and the REL's two live passes |
| `ww_puppet.cpp` | puppet fidelity: the peer's anims, face, equipment, item in hand and clothes; the local Link's to send |
| `ww_smooth.cpp` | the peers' timed states: the puppets' and boats' poses played back a little behind, with their anims and look |
| `ww_online.exports` | those functions, exported from libtww |

## Hook points in the game (all `TARGET_PC`)

- `game/src/m_Do/m_Do_main.cpp`, `main01_frame`: `wwo_frame()` after `mDoCPd_Read` / `mDoAud_Execute`, before
  `fapGm_Execute`.
- `game/include/f_pc/f_pc_name.h`: `fpcNm_PUPPET_e` (0x1F6) before `fpcNm_MAX_NUM_e`;
  `native/include/pc/game_hooks/f_pc_profile_lst.inc` and `game/include/f_pc/f_pc_profile_lst.h`: its profile
  (`NULL` without the mod).
- `game/include/d/actor/d_a_player_main.h` / `.cpp`: `friend class daPuppet_c`; the eye and brow z-compare
  packets behind `daPy_cupPackets` (one set per Link drawn: a packet entered twice makes the draw list
  cyclic); `daPy_swapDebugKeep` for the position the player's execute restores.

## The puppet

Created by `wwo_frame` with `fopAcM_create(fpcNm_PUPPET_e, slot << 24, ...)` from the player's layer while
the peer is active, its stage equals the local stage and the local game has been in play
(`TWW_LOC_IDLE`) for 30 frames. Deleted when any of that stops, when it is still creating after 600 frames
(no ground under the peer; the slot then waits 120 frames), or by the scene. One puppet per slot.

- Create: phase 1 constructs it, keeps the slot, sets its parameter to `0xFF000040` (no start event, start
  mode 0, the local restart room kept) and mounts its own copy of `Link.arc`, registered as `Lnk<slot>`.
  Phase 2 waits for the archive and the local Link, then runs the stock `playerInit`; phase 3 runs
  `makeBgWait`.
- Player code finds the puppet's archive through `l_arcName`, so no model data, material animator or
  texture header is shared with the local Link (the Dolphin REL had to snapshot those instead).
- Player code runs only inside a `PuppetWindow`: `l_arcName` is `Lnk<slot>`, the local player's status
  words are zero, and the start-demo request (`dEvent_manager_c::mException`) and kept position are put back
  afterwards.
- Execute: position and facing from the peer's timed states ("Smooth peers" below), `mAcch.CrrPos` for the ground under it (shadow, room light) with the position kept, then
  the peer's look and anims (below), or without anims `procMove_init` or `procWait_init` (moving: proc `MOVE`
  or speed > 0.5), `mNormalSpeed` = the peer's speed and `setBlendMoveAnime(-1)`: the stock wait / walk / run
  blend. Then the joint calc and `setItemModel` as the player's execute does them.
- Draw: the stock `daPy_lk_c::draw` with the puppet's own packets, then the held boomerang and a carried
  bomb (their real ones are actors bound to the local Link), from the puppet's archive in a solid heap of
  its own.

Inside the window a `WwoPuppetScope` also puts the peer's equipment (sword, shield, bracelet) into the save's
equip slots, keeps the local player's audio state (sword / shield / boots types, the sword-using BGM flag)
from the puppet's setters, and clears the local camera's first-person bits (`SUBJECT` made
`checkPlayerNoDraw` hide the whole puppet, `0x20` its head).

### Anims (the anim mirror, as the Dolphin REL's `puppet_anmmirror.c` does for procs it has no init for)

The puppet stays in proc `WAIT` and plays the bcks the peer's Link plays: under[0..1] and upper[0..1]
(frame, rate, start / end / loop, attribute, blend ratio) loaded into its anm heaps as `getAnimeResource`
does (only where the tracks before left the shared 0xB400 buffer's region free, capped at the region's end,
only a whole `J3D1bck1`), and the item track upper[2]. A track's frame snaps to the peer's on a load or
once it drifted 4 frames; in between it plays at the peer's rate. Also the face btp / eye btk and their
frames, the hand shapes and `mBodyAngle` (aim). So every proc shows: rolls, jumps, swimming, ledges,
climbing, attacks, item use, damage. No proc code runs for the puppet, so it plays no sounds and changes
no local state; the stock procs are not driven.

### Look

The item in hand (`mEquipItem`) is built in the puppet's own item heaps by the stock model setters
(`setSwordModel`, `makeItemType` for the model-only items, `setBottleModel`, `setTactModel`, the hookshot's
body, the leaf's swing and glide models); never `makeItemType` for the boomerang, grappling hook or bomb
bag, which spawn actors for the local Link. Iron Boots: `daPyFlg0_EQUIP_HEAVY_BOOTS`. Clothes: the hero /
casual headers swapped as `playerInit` does (plus `resTIMGChanged`); a tunic colour points the hero header
at a recoloured copy of the 160x96 CMPR image (every green endpoint scaled to the colour, as the REL's
`puppet_appearance.c` does) in a game-heap buffer, which `resTIMGChanged` gives a new texture object, so
Aurora's texture cache (keyed by texture object and data version) uploads it. The local Link gets the
same through `tww_puppet_set_local_outfit`.

Not yet: name tags, the boat, projectiles; the carried bomb's fuse flash (drawn unlit); sounds
from the puppet (positional footsteps would need `setAnimSound` with a guard on Link-voice SEs).

## The API (tww.h, game thread, between `tww_run_frame` calls)

- `tww_get_location`: 1 while the Link actor runs and the stage is not the title (`sea_T`) or the name
  screen. `room` is `dStage_roomControl_c::mStayNo`, `layer` and `point` the start stage's (layer -1 after a
  debug boot); `TWW_LOC_IDLE` while no scene change is pending, `TWW_LOC_EVENT` while an event runs.
- `tww_get_local_state`: the Link actor's `current.pos`, `shape_angle`, `speedF` and `mCurProc`.
- `tww_set_peer`: copies the state; `NULL` makes the slot inactive (the puppet is hidden at once and
  deleted the next frame). `room` is not used yet.
- `tww_puppet_set_anim` / `tww_puppet_set_look`: a slot's anims and look, copied; `NULL` (or `valid` 0)
  drops them (the blend and the local save's equipment again). They outlive the puppet: the client clears
  them with the peer.
- `tww_puppet_get_local`: the local Link's, as a puppet takes them (bck `TWW_PUPPET_ANM_NONE` for a
  cutscene's anim; the tunic colour is the one set with `tww_puppet_set_local_outfit`).
- `tww_save_size` / `tww_save_read` / `tww_save_write`: `dSv_info_c`, 0x12A0 bytes at the GameCube offsets.
  Natively `dSv_save_c` and `dSv_memory_c` (0x000-0x79B) are big-endian in memory (BE(T)); the fields after
  them (`mDan`, `mZone`, `mRestart`, `mTmp`, `mTurnRestart`, the trailing bytes and `mMemCardCheckID`) are
  host order, so the API swaps those to big-endian: the window is the GameCube's bytes throughout. A write
  goes through a big-endian image, so a partial write of a field lands on the right bytes.

- `tww_inv_get`: play's changes queued for the HUD (`TWW_INV_*`: rupees, keys, max life, max magic, arrows,
  bombs, the 8 spoils), `mSelectItem`, and the current stage's save slot and key-HUD flag (only while
  `tww_get_location` says in a stage). `tww_inv_add` adds to a counter as `dComIfGp_setItem*Count` does (d_meter
  applies it next frame); `tww_inv_set_select_item` sets a button's item. `tww_smoke ... inv` queues one of each
  and reads the save back.

## Dev test

`COS_MP_TEST_PEER=1` (environment or the settings file's `[dev]` section): slot 0 is fed from the local
player every frame, 150 units to his right, same proc and speed, and a `[wwo]` line logs the local state
every 300 frames. `poc/tww_smoke` does the same through libtww's API with its `peer` argument.

`COS_MP_SPAWN_OFFSET=<right>:<ahead>` (dev only, off by default) warps the local Link that far from where he first comes
into play (`tww_gate_warp_local`), so two clients booted to the same `COS_BOOT_STAGE` see each other's puppet.

## The world (`tww_world_*`)

The client's world sync (chests, switches, room switches, salvage) writes the save window like any other save data;
what it needs outside that window is here. `tww_world_save_slot` is the current stage's `dStage_stagInfo_GetSaveTbl`
(-1 without Link or while a stage change is pending: the stage info is freed then), `tww_world_room_zone` is
`dStage_roomControl_c::getZoneNo`, and `tww_world_salvage_read` / `_set_flag` reach `daSalvage_c::mTagData_p` only while a
salvage actor exists (the pointer outlives it).

`wwo_world_frame` (from `wwo_frame`, every 4th frame, Link in play, no stage change, menu closed) runs the Dolphin REL's
two passes (GameMod `puppet_worldsync.c`, `puppet_liveworld.c`):
- **Despawn:** placed items whose item bit is in `tww_world_set_remote_items`'s mask and set in the live save data, and
  waiting Heart Containers once the stage's STAGE_LIFE bit is set, are deleted (idle ones only).
- **Live world:** a `tww_world_live_post` batch for this stage is handled while no event runs or is ordered: each chest,
  bombable wall, crystal switch, key door, warp jar... whose create-time flag is in it is deleted and created again from
  its own params in its own layer, so it comes up in the state the save data says. `tww_world_live_status` acknowledges
  it (`done_seq`, `retry` for actors that were not ready in ~2 s).

`poc/tww_smoke/tww_world_smoke.mm` drives it headless: `probe` lists every create-only actor in the loaded rooms,
`items` despawns every placed item with a bit, a chest mask and memory switches set the bits and post them.

## Overlay (name tags, sea chart, projectiles)

`ww_overlay.cpp` (names, the chart, the dev feeder's part) and `ww_overlay_fx.cpp` (projectiles); API in `tww.h`'s
`tww_overlay_*` block.

- Name tags: `wwo_draw` (`game/src/f_ap/f_ap_game.cpp`, `fapGm_After`, after the draw pass) puts each shown puppet's
  name in the 2D opaque list; drawn in `mDoExt_font0` above the puppet, hidden whenever the HUD is (events, menus,
  telescope, Picto Box).
- Sea chart: `dDlst_FMAP_c::draw` calls `g_dDlst_FMAP_afterDraw` (`game/src/d/d_menu_fmap.cpp`, `TARGET_PC`), which
  redraws Link's marker once per `tww_overlay_set_sea_markers` entry on the world map and on any zoomed square.
  `dMenu_Fmap_c` and `daBomb_c` have `friend struct WwoOverlay` for their private members.
- Projectiles: `wwo_overlayFrame` (end of `wwo_frame`) applies up to 4 queued peer events a frame (bombs, cannonballs,
  arrows made with `fopAcM_fastCreate` in the player's layer) and, with `tww_overlay_set_event_send(1)`, queues the
  local Link's for `tww_overlay_poll_event` (16 each way).
- `COS_MP_TEST_PEER=1` also names slot 0 "Test Peer", puts its sea chart marker 20000 / 10000 units off, throws a bomb
  from it every 300 frames (frame % 300 == 150) and echoes the local Link's projectiles back as its own.
player every frame, 150 units to his right, same proc, speed, anims and look, and a `[wwo]` line logs the
local state every 300 frames. `COS_MP_TEST_OFFSET=<right>:<ahead>` moves it (first person needs it ahead);
`COS_MP_TEST_LOOK=<clothes>:<rrggbb>[:<sword>:<shield>]` dresses it differently. `poc/tww_smoke` does the
same through libtww's API with its `peer` argument, and its next argument `<clothes>:<rrggbb>` sets the local
outfit.
## Smooth peers (fan-out: smooth)

`ww_smooth.cpp`; API in `tww.h`'s `tww_smooth_*` block. `tww_set_peer` and `tww_boat_set_peer` keep each slot's states
with their send times (`tww_smooth_set_time`, seconds on the sender's clock; without it a state is stamped as it
arrives). Each game frame the puppet and the boat are drawn at a time `COS_MP_INTERP_MS` (default 100) behind the
quickest arrival of the last 64 states (or behind by their arrivals' spread, when that is more), between the two states
around it (shortest arc for the angles). The local clock is the game's (1/30 s a frame), so the drawn time moves the same
amount every frame; when the mapping between the clocks moves, it catches up at most 20% faster or slower, and jumps past
0.5 s. Past the newest state the motion carries on for up to 150 ms, then holds. A state 300 units (the boat: 1000) from
the last, or in another stage, is drawn at once. The anims and look (`tww_puppet_set_anim` / `_set_look`) are kept by the
same times and handed to the puppet (`wwo_puppetSetAnim` / `_SetLook`) when its drawn time reaches them, so the body
plays them where it is. The drawn pose then takes part in the 60 fps interpolation like any actor (the puppet's and
boat's J3D models are blended between game frames by `pc_fps60_models.cpp`).

Dev test: `COS_MP_TEST_RATE=<hz>[:<jitter ms>[:<latency ms>]]` sends the `COS_MP_TEST_PEER` states (puppet, anims,
look, boat) at that rate, each arriving after the latency plus up to the jitter, in order, with its send time;
`COS_MP_TEST_STEPS=1` (with or without the test peer) logs how far the local Link and slot 0's puppet moved in each frame. Proof:
`run.sh run --preset pvp --input native/check/input/mp-smooth-run.txt --env COS_MP_TEST_PEER=1 --env COS_MP_TEST_STEPS=1
--env COS_MP_TEST_RATE=20:40:20` (Link runs straight from frame 700): the puppet's step equals Link's (16.46 +- 0.07 a
frame) where the old half-way easing gave 5-29.

## Boats (fan-out: boat)

| File | What |
|---|---|
| `ww_boat.cpp` | `wwo_boatFrame` (called at the end of `wwo_frame`): the peers' boat actors' lifetimes, the colours' tick, the `COS_MP_TEST_PEER` boat; `tww_boat_get_local` / `tww_boat_set_peer` |
| `d_a_wwo_boat.cpp` | `daWwoBoat_c : public daShip_c`, profile `g_profile_PUPPET_BOAT`, process name `fpcNm_PUPPET_BOAT_e` (0x1F7); `wwo_boatSeat` |
| `d_a_wwo_boat_move.cpp` | `daWwoBoat_c`'s execute: the pose from the peer's `TwwBoatState`, the colliders, wake and waterline |
| `ww_boatlook.cpp` | the hull / sail palettes and their recolours; the local boat's colours (`tww_boat_set_local_look`) |

A peer's boat is its own actor, created by `wwo_boatFrame` with `fopAcM_create(fpcNm_PUPPET_BOAT_e, slot << 24, ...)` from
the player's layer and no room while the local game is in play and the boat's `stage` is the local one, deleted when
that stops (`tww_boat_set_peer(slot, NULL, ...)`) or by the scene. It shows without the slot's puppet, so a parked boat
(`TWW_BOAT_PARKED`) is drawn where it lies, empty. It is a `daShip_c` built by the stock `createHeap`: the stock joint
callbacks pose it from its fields, which its execute sets from the peer's `TwwBoatState` (list 4, before the
puppets' list 5): position and heading from its timed states ("Smooth peers" below), height and tilt from the local water
(`getWaterY`, `setWaveAngle`), mast and head bcks at the peer's frames, sail, tiller, head look, the cannon or crane (the rope hangs
straight down). The sail is an imitation `daGrid_c` run by `d_a_grid.cpp`'s own `_create` / `_execute` / `_draw` with
the boat as its `l_ship`.

Colours: a peer's hull and head each draw through a `J3DTexture` of their own (fn_body's / fn_head_h's TEX1 headers
copied, the red ones on a recoloured palette; their material packets bind it), its sail through `daHo_pcSetPalette`
around its packet's draw. The head takes the hull colour (the Dolphin mod left it red). The local boat is recoloured in
place; Aurora caches a J3D TLUT by its object, so the hull's and head's texture objects are rebuilt
(`J3DTexture::resTIMGChanged`); the grid makes its texture objects each draw and Aurora resolves those by content.
A sail near the camera is paler: `daGrid_c::_execute` fades it with the camera distance (vanilla), so the clouds show
through the local sail.

Hook points (all `TARGET_PC`):
- `game/src/d/actor/d_a_grid.cpp` / `.h`: `daGrid_pcSetShip` (the ship a sail's code uses as `l_ship`; `_create` binds
  to it) and `daHo_pcSetPalette` (the palette `daHo_packet_c::draw` shows new_ho1 with).
- `f_pc_name.h`, `f_pc_profile_lst.h`, `f_pc_profile_lst.inc`: `fpcNm_PUPPET_BOAT_e` and its profile after the puppet's.
- `d_a_puppet.cpp`: while `wwo_boatSeat` gives a seat (the peer's proc a ship proc but getting in or out, their boat
  shown and ridden), the puppet sits there, tilted with the hull, in the ship procs' sitting pose (`initShipBaseAnime`).

Dev test: `COS_MP_TEST_PEER=1` also mirrors the local boat into slot 0 (800 units ahead, 700 to the right);
`COS_MP_TEST_BOAT` = a comma-separated list of `parked`, `cannon`, `crane`, `look` (peer colours), `locallook` (local
colours), `turned` (facing back, the head toward the camera), `near` (450 ahead, 250 right), `fixed` (below), `blink` (the boat deleted and created again every 200 frames). Proof runs: `--preset sailing --input
native/check/input/mp-boat-sail.txt` (the sail up at frame 400).

Collision: the boat has `daShip_c`'s colliders (three cylinders and the head sphere, Co and Tg), immovable (weight 0xFF,
its CC moves dropped: the position stays the peer's), so the local ship and Link on foot are pushed off it and
projectiles hit it. It also has `daShip_c`'s Acch (ground and water only: height on water that isn't the sea, room
light, shadow), wake (`setEffectData`) and waterline texture. `COS_MP_TEST_BOAT=fixed` places a parked boat across the
way ahead (on the sea, or on land in front of Link once he walks) and logs the distance to it.

Not yet: standing on a peer's boat (the "Ship" archive has no collision mesh: the local Link rides his own boat
through `setShipRidePos`, not BG; it would take a deck mesh of our own as a moving `dBgW`, the Co cylinders kept off
Link while he is aboard, and a way aboard from the water), the rope's sway, the talk mouth (a second head animation the API doesn't carry).

## PVP (fan-out: pvp)

`ww_pvp.cpp` (`wwo_pvpFrame`, from `wwo_frame` after the puppets' lifetimes); API in `tww.h`'s `tww_pvp_*` block.

- Body (always, PVP or not, as the Dolphin REL's setCollision of the puppet's mCyl): every puppet has the player's body
  cylinder with Co (the enemy group against all) on a `dCcD_Stts` of weight 0xFF, so the CC pass pushes only the local
  Link (and anything else) off it; its CC move is dropped, the peer's position is the network's. Proof: `run.sh run
  --preset pvp --input native/check/input/mp-body-walk.txt --env COS_MP_TEST_PEER=1 --env COS_MP_TEST_FIXED=1 --env
  COS_MP_TEST_OFFSET=0:300` (`COS_MP_TEST_FIXED=1`: the test puppet stays where it was placed and the distance to it is
  logged): Link stops about 60 units from it, then slides round it.

For PVP only the
slots in `tww_pvp_set_slots`' mask take part (the client sets the peers whose game can take hits, while the room's PVP rule
is on); the others' puppets are no targets and have no hurtbox.

- Targeting: a taking-part puppet has `fopAc_Attn_LOCKON_BATTLE_e` (battle distance 3, as enemies' default), its attention
  position over its head and its `eyePos` at the head, so `dAttention_c` offers it to L like an enemy (cursor, strafing,
  lock-on camera). Without PVP the puppets stay out of the attention list: no "look at" type, L stays the camera reset.
- Hurtbox: a `dCcD_Cyl` (the player's body cylinder, tg in the enemy group, the Bokoblin's hit mark, no continuous hits)
  on the puppet's own `dCcD_Stts`, registered every frame. Its hit callback (during the CC pass, while the attacker still
  exists) keeps a hit by the local Link's own attack: Link himself (sword, spin, hammer), his shot arrow (param 1), his
  bombs and Bomb Flowers (never a peer's copy, `WwoOverlay::isCopy`), boomerang and hookshot; enemies' hits on a puppet
  only spark. It plays the weapon's hit sound (`at_se_get`); the CC pass draws the spark. One report per puppet per 10
  frames, queued for `tww_pvp_poll_hit` (16).
- Victim side: `tww_pvp_apply_hit` queues it; once the attacker's puppet and the local Link are there (up to 60 frames,
  not while a menu is open) the puppet registers a one-frame AT sphere over the local Link with the hit's type, power and
  special (and a vector away from the attacker). The CC pass and `daPy_lk_c::changeDamageProc` then do what an enemy's
  hit does: guard (against the puppet's `eyePos`), damage from `mStts`, `procDamage` / `procLargeDamage`, invincibility
  frames (a hit during them is lost, as in the game), and at 0 hearts the stock death: a fairy revives, else the game-over
  screen and its continue. Hits during an event are lost too.

- Damage flash: the stock draw flickers Link red while `daPy_lk_c::mDamageWaitTimer > 0`. The local Link's state goes out
  as `TWW_PUPPET_LOOK_DAMAGE_FLASH` (`tww_puppet_get_local`), and a puppet's timer is 1 while its peer's look has it, or
  for 20 frames after the local Link hit it (`wwo_pvpFlashing`: at once, before the victim's own flash comes back about
  12 frames later), so the puppet flickers as the victim does. The victim's hurt anims (`procDamage`'s DAMF/B/L/R) come
  through the anim mirror like any other.

Dev test: `COS_MP_TEST_PVP=1` with `COS_MP_TEST_PEER=1` makes slot 0 take part and deals each hit the local Link lands on it
back to him 30 frames later. Proof runs: `run.sh run --preset pvp --input native/check/input/mp-pvp-target.txt --env
COS_MP_TEST_PEER=1 --env COS_MP_TEST_PVP=1 --env COS_MP_TEST_OFFSET=0:110`, and `tww_smoke ... pvp` (the API: polls the hits,
deals `TWW_SMOKE_PVP_DAMAGE` at every shot after the first). `COS_BOOT_PRESET=pvp` (pc_preset.cpp) boots Outset's grass by
the shore (sea:44:8) with sword, shield, bow, boomerang, bombs, hookshot and 8 full hearts.

Not yet: hits on a puppet in the peer's boat while sailing (the hurtbox follows the puppet, but the victim's sphere may also
touch their own ship's player-group colliders); a hit flash on the puppet's model (only the spark and sound); damage scaling
(PVP uses the attack's stock power: a sword slash is a quarter heart, a spin attack knocks down).
