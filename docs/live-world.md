# Live world: another player's action happens in your world

When a player in the same room opens a chest, blows up a wall, hits a switch or clears a room so a ladder drops, the other players see the same thing live, and nobody can get a chest's item twice. This is the design doc: what is built (§0), then the research it rests on (§1-§8, written before the build: "today" there means before stages 1-2).

**Status (v0.1.0):** stage 1 (#12, key doors re-created #15) and stages 1b + 2 (#13, protocol 3) are merged. Chests and key doors were verified in game on 2026-09-29 (checklist A below). The room switches (checklist B) are merged but not explicitly verified in game: in particular the drop-down ladder (B1) hasn't been seen to drop on the other screen yet.

- Most puzzle objects **poll** their switch every frame. When a bit reaches your live save data, they react on their own, often with the same short camera cutscene you would get if you had pressed the switch yourself. That works for memory switches (0x00-0x7F), which `WorldFlagSyncService` syncs.
- About **half the in-room puzzle switches are not memory switches.** They are dungeon-visit (`dan`, 0x80-0xBF) or room-temporary (`zone`, 0xC0-0xEF) switches. That includes **16 of 18 drop-down ladders** (kill-all-enemies → room switch 0xE0), most torches and about half the crystal and floor switches. Stage 2 syncs the latching ones, chosen by a table built at patch time (stage 1b).
- A smaller set of objects reads its flag **only when created**: chests, bombable walls, ice blocks, barricades, crystal visuals, small-key locks, the boulders and light walls on the dungeon warp jars, and a normal warp jar's lid. Stage 1 (a small REL pass) brings them up to date. **The chest case was a duplication bug:** see 7.1.
- Not done, on purpose: shared enemy deaths (stage 4) and a no-cutscene mode (stage 3; cutscenes are handled by their flags). Some things change the world without a flag (pushing most blocks, switchless torches, grass): they stay local.

Citations are `tww-decomp/` paths unless they name a repo file. Offsets marked **verified** were checked in the vanilla GZLE01 `main.dol` or the actor's REL (the decomp has wrong offsets for some TWW structs). **(?)** marks an open question.

## 0. What is built

### Stage 1: REL catch-up for create-only actors (#12, #15)
- **C#** (`LiveWorldPoke`, fed by `WorldFlagSyncService`): the chest and memory-switch bits it applies **from other players** to the current stage's live copy are recorded per stage visit (the `_remoteItemMask` idea). Each tick it hands the REL the ones the live copy now has (read back, so the REL never acts on a bit the game lacks) and that the REL hasn't handled yet.
- **Block** (`puppet_shared.h` `LIVEWORLD_*`, pointer in scratch word `0x803FD14C`): a 0x4C-byte game-heap block the REL allocates once and never frees, boot-stamped like the names block (`BootStampedBlock` / `puppet_bootBlock`, shared). One **batch of new bits** at a time: C# writes it under a seqlock (SEQ odd, TAG = "LW" | saveTbl, ZONE_ROOM, BITS[9], SEQ even) only once the REL has acknowledged the previous one (`DONE_SEQ == SEQ`), so each bit is handled once. BITS: word 0 = chests, words 1..8 = switch *n* in word `1 + (n >> 5)` (memory, dan, and the zone switches of ZONE_ROOM).
- **REL** (`puppet_liveworld.c`, called first thing in every puppet's **draw**, parked ones too, once per frame, every 4th frame): one `fopAcIt_Judge` pass over a table `{proc, shift, mask, lock offsets}`. It runs in the draw, after every execute of the frame, because the player orders a chest's TREASURE event with the chest's pointer in its own execute (`d_a_player_main.cpp:4166`), which runs after the puppet's: from the execute we could delete a chest the player has just targeted, and the event would then use a freed actor. In the draw, `mOrderCount` holds every order of the frame.

| Actor (proc) | Key | Action | Result |
|---|---|---|---|
| Chest `TBOX` 0x126 | chest bit `(prm >> 7) & 0x1F`; funcs 7/8 (sea "extra save info") skipped | re-create | comes up open and empty (`checkOpen` at create) |
| Bombable wall `WALL` 0x1B1, breakable floor `FLOOR` 0x62, ice block `Obj_Ice` 0x1D2, FF barricade `MJDOOR` 0x47 | switch `prm & 0xFF` | re-create | create returns ERROR: gone |
| Wooden barricade `SAKU` 0x191 | switches `(prm >> 8)` and `(prm >> 16) & 0xFF` | re-create | the half comes up broken |
| Crystal switch `SWHIT0` 0x1C9 | switch `prm & 0xFF` | re-create | shows on; a timed one starts its own timer, as the peer's did |
| Small-key / boss doors `DOOR10` 0x12E / `DOOR12` 0x12F | memory switch `prm & 0xFF` (< 0x80) | re-create, only a door showing a lock, in Wait, with the local Link > 250 units away (0.1) | built unlocked, as on a reload: no second key spent. The lock byte is never written |
| Black boulder `Stone2` 0x1CD (`Ebrock`, `Ebrock2`, `Ekao`) | switch `(prm >> 8) & 0xFF` | re-create, only lying still (mode wait: not carried or thrown) | create returns ERROR: gone. The warp jar under it opens itself (0.2) |
| Light wall `Obj_Mkiek` 0x04E (`MkieK`) | switch `prm & 0xFF` | re-create | create stops: gone |
| Normal warp jar `OBJ_WARPT` 0x043, types other than 2-4 (`WarpD`, `GanonK`) | lid switch `home.angle.x & 0xFF` | re-create, only a jar with its lid on (`m2C6`), closed (mode 1, not burning), with the local Link > 120 units away | built open, as on a reload (0.2) |

  - **Re-create** = `fopAcM_delete` the old one, and only if the delete was accepted (`fpcDt_Delete` refuses an actor being created or deleted: two chests would give the item twice), `fopAcM_create(proc, params, &home.pos, home.roomNo, &home.angle, &scale, argument)` in the **actor's own layer** (`base_process_class::mLyTg.mpLayer` +0x2C, i.e. the room scene's). The deleted actor's fields stay readable until next frame's deletor. The current layer is not the room's (`f_pc_base.cpp:47`), and an actor created there would outlive the room. This replaces the `mStatus[room].mProcID` layer lookup the research proposed. None of these actors rewrites its params, `home` or `scale` at create (checked in each `.cpp`), except the boulder, which zeroes `home.angle.z` (`prmZ_init`); it is only re-created with its switch set, so its create returns ERROR anyway. Scale round-trips exactly (u8 × 0.1 × 10).
  - **Gates:** no stage change in flight, valid stage info, TAG is this stage's; **no event running and none queued** (`mOrderCount`, gameInfo+0x5298: an event order holds actor pointers, `d_event.cpp:45`); the actor is fully created, not being deleted, and (for re-creates) its room is loaded, not loading/unloading and not hidden (`mStatus[room].mFlags & 0xF == 1`: a hidden room's layer is deleted without it). An actor that isn't ready (still being created: its create may have read the flag before the bit landed; its room busy or hidden; a door not in Wait or with the local Link at it; a warp jar with its lid burning or the local Link on it; a boulder being carried or thrown; more than 16 matches) holds the batch back up to ~2 s; then the rest is done and the batch is acknowledged with **RETRY**, and C# publishes it again (up to 3 times; an actor already done is just re-created once more). Zone bits only for actors in ZONE_ROOM. It never starts an event, grants an item or touches the local Link.
- **Worker puppet:** the REL only runs while a puppet exists, so `PuppetSyncService` asks for a parked one while `LiveWorldPoke.IsNeeded` (bits waiting, no block yet, or an unacknowledged batch; gives up after 10 s without progress, not counting time in an event or the pause menu, when the REL waits on purpose, and asks again 30 s later or on the next change), like the despawn worker. It only counts bits the live copy still has (a remote switch the game cleared again doesn't hold a puppet).
- **Logs:** `client-PlayerN.log` `[world] live world: handing the REL slot … tbox=… sw=…` and `REL handled … (N actor(s) updated so far)`. `dolphin-N.log` has one line per actor the pass handles: `[PUPPET] live world: re-created proc 0x126 prm 0x… room N` (or `NOT re-created` when the delete was refused; the batch then retries). `POKE_COUNT` in the block counts actors updated.
- **Size:** puppet REL +1680 bytes over main (50304 → 51984, measured with `ElfToRelConverter` on a locally linked ELF; +208 for the review fixes: the draw-time pass, RETRY, delete-first; the door re-create and per-actor OSReport: 53336 → 53736, +400). The file is built with `#pragma GCC optimize("no-shrink-wrap")` (-Og otherwise copies the epilogue into every early return). The warp jars (0.2) add 172 more (99652 → 99824 raw, 41259 → 41351 Yaz0, `--build-patchdata`). No protocol change; needs a re-patch (build stamp).
- **Not done:** the genocide chest's *appear* switch (func 2 reads it only at create). If the peer *opens* it, the chest bit re-creates yours open; if they only made it appear, yours waits for your own enemies. Shutters (`Htobi`), stop bars, ladders: they poll (A+ev), so no REL work.

**0.1 Small-key doors: re-created, never written (2026-09-29 freeze).** The first build cleared a door's `dDoor_key2_c::mbEnabled` (door10 +0x308, door12 +0x2E4) while the door was in Wait. In a DRC test (`M_NewD2`, Stage.arc `keyshut` prm `0x0FFFF43C`: door10 type 4, switch 0x3C, rooms 0/2, no stop bars) P1 unlocked the door; P2, in room 0, got the bit and the REL cleared the lock (the batch's one actor; nothing else in the table reads 0x3C). 40 s later P2 walked up to the door and Dolphin logged `ISI exception at 0x00000000`; the game froze. No REL code was running then (no puppet, no batch). With the pass not touching doors (84075ba) nothing froze, but P2 (no key) was stuck behind the lock; after a stage reload the unlocked door worked. So a door **created** with its switch set is fine, and changing the lock of a live door is not (or something it meets on the way is). The decomp shows no difference between the two states (`keyOff` is only `mbEnabled = false`; nothing that reads the lock calls through a pointer), and Dolphin gave no PC/LR, so the crashing call is still unidentified.

So a locked door is now **re-created** like a chest: its create builds it exactly as a reload does. Checked in the decomp:
- **Create = reload.** A Stage.arc door (`TGDR`, `dStage_tgscInfoInit`) is created with room -1 in the stage's layer, and the door sets `current.roomNo` to its front room itself (`d_a_door10.cpp:535`). The re-create passes the old `home.roomNo` (-1) and uses the old layer: the same. `CreateHeap` builds the key model for every key door, `CreateInit` starts in Init, and the first `actionInit` runs `setKey`: keyOff with the switch set. Stop bars (`swbit2`, none on a key door), `Hkyo` and `DoorBs` are built the same way. door12 arg1 8 clears temp bit 0x0440 at create (`d_a_door12.cpp:581`): those are left alone.
- **No raw pointers to a door outlive it.** The event system holds process IDs (`mPt1`/`mPt2`, `d_event.cpp:118`), attention holds IDs (`dAttList_c::mActorID`), the player's door proc (`dProcDoorOpen`) keeps no door, the event manager looks the door up (`specialCast_Shutter`) only when an event starts, and event orders (raw pointers) are covered by the no-event/no-order gate.
- **Rooms.** A door doesn't load rooms: room loading follows the stay room and the room table. It only changes the hidden flag (0x08) in its open/close cuts (`openInitCom`/`closeEndCom`), which run only in an event. Its collision (`dBgW`) is released at delete and registered again at create, so there is a gap of a frame or more (longer if the `Key` archive has to reload).
- **Gates** (on top of the common ones): the door shows a lock and is in **Wait** (Init re-runs `setKey` on its own; any other action waits), it's on a memory switch (< 0x80), and the local Link is **more than 250 units** (XZ, the door's own reach) from it, so nobody walks through the gap. A door in Init is skipped: the player isn't in its front or back room, and `actionInit` will clear the lock when they get there.
- **If you stand at the door** when the bit arrives, the batch waits (up to ~2 s per publish, RETRY, 3 publishes). Step back and it goes; otherwise it's cleared the next time you change rooms (execute sets Init on a stay-room change, `d_a_door10.cpp:794-831`).
- **Key spent twice:** can't happen once the door is re-created (no lock). While a door is still waiting for its re-create, a player with a small key could spend it on the door. With shared small keys that key is handed back on the next idle tick: the count is derived from the flags, and the door's switch was already counted (`docs/small-keys.md` §0).

**0.2 Warp jars (2026-09-29: "unlocking the teleports by exploding the top doesn't sync until a room reload").** `daObj_Warpt_c` (`d_a_obj_warpt.cpp`) comes in two kinds (type = `prm & 0xF`, `getArg` :705-764):
- **The three-way dungeon jars** (types 2-4, `Warpts1/2/3`: DRC `M_NewD2`, FW `kindan`, ET `M_Dai`, WT `kaze` + `Cave08`). Their "open" state is **not a switch**: it is bit 1/2/4 of an event register, one per dungeon (`m_event_reg[(prm >> 4) & 0xF]`: 0xA207 DRC, 0xA107 FW, 0x9F07 WT, 0xA307 ET; `onWarpBit`/`isWarpBit` :285-295). Warping reads the other jars' bits live (`spWarp`). `home.angle.x & 0xFF` is the switch of whatever **covers** the jar: DRC's jars in rooms 2 and 10 sit under black boulders (`Ebrock2` 0x11, `Ebrock` 0x1E), ET's under a boulder (0x5B) and a light wall (`MkieK` 0x26), FW's room 5 jar under 0x2F (an event). While that switch is off the jar has no lid of its own and **polls** the switch (`modeClose` :471), then opens itself (sets its register bit, the riddle jingle, no break effect). So in DRC the jar already opened live when a peer's switch arrived; what stayed until the reload was the **boulder** (`Stone2`: create returns ERROR once its switch is set, it never polls, `d_a_stone2.cpp:171-193`). Stage 1 now re-creates boulders and light walls. The jars whose own lid you break (FW room 16, ET room 2, all of WT) keep it in the register: see **not synced** below.
- **Normal jars** (types 0/1: `WarpD` = Diamond Steppe's warp maze on dan switches 0x81-0x92, `GanonK` on memory 0x07/0x08). The lid switch is `home.angle.x & 0xFF` (set by `openHuta` :396 when the lid breaks; type 1 sets it at create :718), the partner's is `angle.x >> 8`, read live when you warp (`normalWarp`). A jar with its lid on never re-reads it (`modeClose` only reacts to hits), so stage 1 re-creates it: the new one's `getArg` sees the switch and builds no lid (`modeOpenInit`: only the open-jar swirl every open jar shows on a room load; `breakHuta`'s break effect and sound run only on a hit). Skipped: a jar with its lid burning (mode 2, it opens itself), and the local Link within 120 units (on the lid: he would drop in and warp); then the batch waits and retries like a door. The switch table (rules 3) now knows these lids, the boulders and the light walls as latching setters, so `WarpD`'s dan switches sync too (83 dan / 163 room switches on the vanilla game, was 68 / 161).
- **Not synced (open question):** the three-way jars' event registers (0x9F-0xA4, mask 0x07). `EventFlagCatalog` marks them `BitwiseOr`, but no rule carries them (Shared story only has bytes 0x00-0x41 and the figurines). So a lid broken on FW room 16, ET room 2 or in WT never reaches the others, not even after a reload, and a jar a peer uncovered while you were in another room comes up **with a lid** on your next visit (its cover switch is set, its register bit isn't: `isRealHuta`). Fixing it needs the registers in a room store (a protocol bump, like the figurines #29) plus a REL rule keyed on the register for the jar with its own lid.

**The chest in the same session.** A Stage.arc chest logs `room -1` (for example `re-created proc 0x126 prm 0xff000001 room -1`: tbox 0, func 1). That's its real `home.roomNo`: stage actors are created with room -1 (`dStage_tgscInfoInit`/`dStage_actorInit`: `appen->room_no = i_stage->getRoomNo()`, -1 for the stage) in the stage's layer, and the chest finds its room itself (`searchRoomNo`: `home.angle.x & 0x3F`). The re-create does the same, so room -1 is correct and not a crash suspect by itself. The room gate is skipped for room -1 (the stage is always loaded). The OSReport now reads the actor's fields before the delete.

**In-game checklist (stage 1).** Chests and key doors verified in game on 2026-09-29. Two players, Shared world on, P2 standing in the room while P1 acts. Check `client-Player2.log` for `live world: handing the REL` then `REL handled`.

| # | Where | P1 does | Expected on P2 |
|---|---|---|---|
| A1 | DRC room 10, yellow-rupee chest | Opens it | P2's chest pops **open and empty** within about a second (after P1's get-item event ends on P1; after any event on P2 ends). P2 can't get the rupees (wallet +10 once) |
| A2 | Any chest, P2 standing at it | Opens it while P2 is about to open it | No crash. At worst both get it (latency race, accepted) |
| A3 | ToTG room 0, bombable walls 0x38-0x3A | Bombs one | P2's wall vanishes (no explosion effect); leave and return: still gone |
| A4 | DRC key door (`keyshut`, room 0/2), P2 in room 0 away from the door, **no key** | Unlocks it | `dolphin-2.log`: `live world: re-created proc 0x12e prm 0x0ffff43c room -1`. The door blinks and comes back unlocked. P2 opens it (door event, room change) and goes back through it: **no freeze**, key count unchanged |
| A4b | A4 with P2 standing at the door | Unlocks it | Nothing until P2 steps > 250 units back, then as A4 (within ~8 s; after that on P2's next room change) |
| A4c | A4, P2 in another room (not room 0 or 2) | Unlocks it | No re-create line for the door (Init); P2 enters room 0: the door is unlocked |
| A5 | FW room 13, timed crystals 0x08-0x0C | Hits one | P2's crystal shows on and turns off on its own timer |
| A6 | WT breakable floor / an ice block on a memory switch | Breaks / melts it | Gone on P2 |
| A7 | A1 with P2 in a cutscene, or mid-room-load | | Catches up when the event ends / the room has loaded; nothing duplicates on re-entering the room (layer check) |
| A8 | A1, then P1 leaves the stage | | P2's chest still opens (P2 keeps a parked puppet until the REL acknowledges) |
| A9 | DRC room 2 (or 10), warp jar under a black boulder, P2 in the room a few steps from it | Bombs the boulder (bomb flower) | `dolphin-2.log`: `re-created proc 0x1cd`. P2's boulder vanishes (no break effect); the jar opens with the jingle; P2 can warp. A boulder P2 is carrying is left alone |
| A10 | `WarpD` (Diamond Steppe), a lidded jar (`Warpt`), P2 in the room away from it | Bombs the lid | `[switches]` sends / applies the dan bit; `dolphin-2.log`: `re-created proc 0x043`. P2's jar comes back open, no lid, no break effect. P2 standing on the lid: nothing until P2 steps off |
| A11 | ET room 6, jar under the light wall (`MkieK` 0x26) | Dissolves the wall with the mirror shield | P2's wall vanishes; the jar opens |
| A12 | WT, any jar | Bombs its lid | **Nothing** on P2, even after a reload (register not synced, 0.2) |

### Stage 1b: the switch table (#13)
- **Built at patch time from the player's own game** (`WWOnline.Patcher/WorldData`): `StageDataReader` reads every `files/res/Stage/*/Stage.arc` and `Room*.arc` (only the archive header and the .dzs/.dzr are inflated; all layers), the STAG chunk gives each stage's save slot, and main.dol's `l_objectName` (0x80372818) maps object names to process names. `SwitchTableBuilder.SettersOf` says what each actor kind does to its switch (from the decomp, proc names read from each REL's profile). Pipeline step 7 (`BuildSwitchTableStep`, Full builds) and the pre-built patch path both write `<game>/wwo-switch-table.json` (a few KB, ~0.2 s). It is never in the repo; `SwitchTableProvider` finds it next to the patched game (Settings game folder, else the dev config's `game_path`) and re-reads it after a new patch. A patch without stage data fails rather than writing no table; a stage whose archives can't be read (damaged or modded) is skipped and named in the patch log.
- **Enforced like the rest of the build:** `SwitchTableBuilder.RulesVersion` (bump it whenever the classification changes) is part of the build stamp's source hash, so a game patched before the table existed or with other rules reads as **stale — patch again** (dev sources, installed PatchData and selection-only checks alike). The stamp records the rules of the table it wrote (`SwitchTableRules`), and a stamped game whose table is missing or from other rules is stale too. The client ignores a table from other rules.
- **Classification** (per save slot for memory switches, per **stage** for dan switches, per stage room for zone switches; a Stage.arc actor's zone room is its own room field, or "unknown"). Dan switches are per stage, not per slot: the game keeps them per slot (`dComIfGs_initDan` only resets on a new slot, `d_save.cpp:1225`), but the stages that share a slot are separate visits (the sea caves in slots 12/13: you always leave through the sea), so `TF_01`'s kill-all doors must not open `TF_02`'s:

| Setter | Latching (may sync) | Unsafe (never syncs) |
|---|---|---|
| Crystal `SW_HIT0` | no timer (`(prm >> 20) & 0xFF` is 0/0xFF) | timed (turns it off, `actionOnTimer`) |
| Torch `bonbori` (`EP`) | no timer (`(prm >> 8) & 0xFF` = 0xFF) | timed (`d_a_ep.cpp:286`) |
| Floor switch `Kbota*` (`Obj_Swpush`) | types 0, 3 (stay pressed, obey save) | 1 pressure plate, 2 "on is up" |
| Iron-boots switch (`Obj_Swheavy`) | type 3 | 0/1 pop back up, 2 toggles |
| `ALLdie`, chest open switch (`angle.z`) and func-2 appear switch, bombable wall / floor / ice / barricades, boulder `Stone2`, light wall `MkieK`, a normal warp jar's lid (`angle.x & 0xFF`, not types 2-4), kill-all bars (door type 2) | always | |
| AND switch `AND_SW0` | behaviours 0, 2, 3 (output), unless an input is unsafe | behaviour 1 output; behaviour 2's **inputs** (its timer turns them off); count 0xFF; any output fed (through any chain of AND switches) by an unsafe input |
| AND switch `AND_SW2` | type 0 (one-off), unless an input is unsafe | type 1 (continuous); outputs fed by unsafe inputs (e.g. `SubD45`/`PShip2`: an area switch 0xEB → 0xEC → … → 0xEF starts a Stalfos ambush) |
| Area switch `SW_C00` | | always: type 0 clears when your Link leaves, the others fire when your Link enters (a player position, e.g. arena bars) |
| Timer `ObjTime` | | always |
| Push block `osiBLK*` (`Obj_Movebox`) | | when it follows a path (pathId = `angle.z & 0xFF` != 0xFF, swSave1 != 0xFF, not a "dmy" box (prm bit 30), not `MkieBB`), swSave1 and `angle.z >> 8` are **path-encoded**: excluded from the memory sync too (§7.2). Otherwise its switch is just one it reads |

  A dan or zone switch syncs only if at least one latching setter uses it and no unsafe one does; a switch nobody is known to set (enemy death switches, events) is left alone. An unsafe setter with an unknown room excludes that switch in every room of the stage. On the vanilla game (rules 2) this gave 68 dan and 161 room switches (the 0xE0 ladder rooms `PShip/0-2`, `SubD45/0-2`, `SubD71/1-2`, `M_Dai/5`, ToTG, WT and FW rooms, the sea caves, the sea) and 27 excluded push-block memory switches. Rules 3 (boulders, light walls, normal warp jars; 0.2) give 83 dan (`WarpD`'s jars) and 163 room switches.

### Stage 2: live dan and room switches (#13, protocol 3)
- **What syncs:** the table's syncable dan switches between players in the same stage (and save slot), and zone switches between players in the same stage **and** room (at sea too: each square is a room with its own zone), while **Shared world** is on. On-edges only.
- **Client** (`RoomSwitchSyncService` + `RoomSwitchTracker`, 4 Hz, scene stable and the same room for 1 s): reads the place (stage name, STAG save slot, `mStayNo`), `mDan` (gameInfo+0x79C, only while its `mStageNo` is this slot) and the stay room's zone (`mStatus[room].mZoneNo`, only if `mZone[z].mRoomNo == room`). A new place **joins** the server's store for it (sending its own latching bits) and applies what the players there set. Then new local bits are sent, received bits are ORed in (u32 dan words, u16 zone words).
  - **Echo guard:** a bit received from the room is never sent back; nobody ever sends a clear.
  - **Apply once:** a room bit is written at most once per place, and never if this game has had it there (it may have turned it off itself, e.g. a torch blown out): the room's copy would re-latch it. A bit counts as applied only once the write landed. A new room keeps the stage's dan bookkeeping; a new stage starts afresh, and so does anything that drops the scene gate (a load, void-out, game over, soft reset, title screen): the reload clears the room's switches even at the same place, so the client leaves and joins again.
  - Applied dan/zone bits also go to `LiveWorldPoke` (words 5-8, ZONE_ROOM = the stay room), so the stage-1 REL pass catches up ice blocks and crystal visuals on room switches. **No REL change in stage 2.**
- **Server** (`RoomSwitchStore`, `GameHub.JoinRoomSwitches` / `SendRoomSwitches` → `ReceiveRoomSwitches`): every input is validated (`RoomSwitches.IsValid`: stage 1-8 printable ASCII, slot 0-15, room 0-63, exactly 2+2 words, no bits past 0xEF). Each connection is at one place; bits merge up per stage (dan) and per stage room (zone); new bits go to the others in the same room (dan + zone) and dan-only to the rest of the stage, nobody else. A stage's dan bits are dropped when its last player leaves, a room's zone bits when the last player leaves the room (a fresh visit starts clean, like the game). `SendRoomSwitches` for a place the connection hasn't joined returns false and the client joins again (the store was cleared, e.g. Shared world off and on between two ticks); clients also rejoin on every room-rules change. Turning Shared world off clears the store.
- **Protocol:** `HubConstants.ProtocolVersion` 2 → **3** (new hub methods, callback and DTO; 2 was held items #6 and boat parts #7).
- **Per object** (the §3 classes, confirmed): ladders `Mhsg` (A+ev: poll, drop with their event), torches (A), floor and iron-boots switches (A), shutters / bars / gates (A+ev), switch-appear chests (A+ev) react on their own. Crystal visuals and ice blocks on room switches are B: the stage-1 REL pass handles them. Kill-all bars and genocide chests still count your own enemies (local); a synced `ALLdie` switch opens what polls it.
- **Also:** push-block memory switches are no longer sent or applied by the world sync (`PushBlockSwitches`).
- **Logs:** `[switches]` in `client-PlayerN.log` (`at … syncable here`, `joined`, `local set … → sending`, `room set`, `applied`) and `server.log` (`is at …`, `set …`).

**In-game checklist (stage 2).** Not yet run as a whole: B1 (the ladder) in particular is unverified. Two players, Shared world on, both in the room. Patch the game again first (the table is written by the patch).

| # | Where | P1 does | Expected on P2 |
|---|---|---|---|
| B1 | PShip / SubD45 / SubD71 room with a ladder on 0xE0, or ET room 5 | Kills P1's enemies (ALLdie sets 0xE0) | P2's ladder **drops with its event** while P2's enemies are alive (T13) |
| B2 | ToTG room 5 (0xE0/0xE1) or WT room 9 (0xC0-0xC4) | Lights the torches / hits the switches | Same on P2, live |
| B3 | DRC room 12 pressure plate 0x41, room 16 plates 0xE0/0xE1 | Presses them | **Nothing** on P2 (unsafe: excluded) |
| B4 | A timed crystal or timed torch on a dan/zone switch | Hits / lights it | Nothing on P2 (excluded) |
| B5 | B1, then P2 leaves the room and comes back while P1 stays | | P2's ladder is down again (the room's store) |
| B6 | B1, then both leave the room and one comes back | | The store is gone with the room: vanilla behaviour |
| B7 | A room with a push block (DRC room 0 / 14) | Pushes it along its path | P2's block does **not** jump to a corrupted position on reload |
| B8 | Shared world turned off, then on | | No switch syncs while off; on again, both rejoin their room |
| B9 | Any B1-B2 with P2 mid-jump / swimming / on a ladder | | Note how P2's event starts (T14) |
| B10 | Sea caves: P1 in `TF_01`, P2 in `TF_02` (same save slot) | Clears the kill-all doors | **Nothing** on P2 (dan switches are per stage) |
| B11 | `SubD45` / `PShip2` room 0 | Walks in (area switch 0xEB) | P2's Stalfos ambush does **not** start (AND chain on an unsafe input) |
| B12 | B1, then P2 voids out / dies and continues in the same room | | P2's ladder drops again after the reload (rejoin) |

---

## 1. How the flags work

### Switch routing (`dSv_info_c::onSwitch/offSwitch/isSwitch/revSwitch`, `src/d/d_save.cpp:1540-1627`; constants `include/d/d_save.h:982-989`)

| Switch no. | Store | Lifetime | Synced today |
|---|---|---|---|
| `0x00-0x7F` | `mMemory.mMembit.mSwitch[4]` (`d_save.h:685`) | Per stage save slot. Saved to the card (`putSave`) | **Yes**, OR-merged, applied once per bit (`SwitchApplyGuard`); push-block (path-encoded) switches excluded since stage 1b |
| `0x80-0xBF` | `mDan.mSwitch[2]` (`d_save.h:754`, `dSv_danBit_c`) | The current save slot's visit. `dStage_stagInfoInit` → `dComIfGs_initDan(saveTbl)` (`src/d/d_stage.cpp:1669-1677`) clears it when the slot changes (`d_save.cpp:1225-1234`). A reload of the same dungeon keeps it. Never saved | Latching ones, since stage 2 (§0) |
| `0xC0-0xDF` | `mZone[z].mZoneBit.mSwitch[0..1]` (`d_save.h:770`) | Per room. The zone is created when the room loads (`src/d/d_s_room.cpp:193-197`). `zoneCountCheck` removes it after 2 room changes away (`d_stage.cpp:236, 250-263`). Never saved | Latching ones, since stage 2 |
| `0xE0-0xEF` | `mZone[z].mZoneBit.mSwitch[2]` | **Cleared on every room change** (`clearRoomSwitch`: `d_save.cpp:1274`, `d_stage.cpp:137, 254`, `d_s_room.cpp:82`). This is "this visit of this room" | Latching ones, since stage 2 |
| `0xFF` / `-1` | none | | |

- **Items** (`dSv_info_c::onItem/isItem`, `d_save.cpp:1629-1669`): `0x00-0x3F` go to `memBit.mItem`, and bits 32-63 alias `mVisitedRoom[0]` (`puppet_worldsync.c` already notes this). `0x40-0x4F` go to `zone.mItem`.
- **Chests**: `memBit.mTbox`, 32 per stage (`d_save.cpp:1072-1081`). `dComIfGs_isTbox` reads the **live** copy (`include/d/d_com_inf_game.h:1617-1627`). The sea's "extra save info" chests use `dComIfGs_isStageTbox(STAGE_SEA2, n)`, which reads the saved slot unless it is current (`src/d/d_com_inf_game.cpp:693-712`).
- **Killed enemies**: `dSv_zoneActor_c`, 512 set-IDs per zone (`d_save.cpp:1314-1322, 1671-1700`). Enemies call `fopAcM_onActor` on death (`d_a_bk.cpp:2648`, `d_a_mo2.cpp:2411`, `d_a_st.cpp:1239`, `d_a_tn.cpp:2073`, ...). The room loader skips them (`d_stage.cpp:1694`). Zone-scoped, never saved, not synced.

### Which copy the game reads during play
The **live** copies only: `dSv_info_c` `mMemory` at GameInfo+0x778, `mDan` at +0x79C and `mZone[32]` at +0x7A8 (`d_save.h:991-996`). `mSavedata.mMemory[slot]` (+0x380 + slot·0x24) is touched only by `getSave`/`putSave` on a stage change (`d_save.cpp:1506-1515`). The world sync already follows this rule.
- `dSv_zone_c` is 0x4C bytes: `mRoomNo` s8 +0x00, `mSwitch` u16[3] +0x02, `mItem` u16 +0x08, `mActorFlags` u32[16] +0x0C (`d_save.h:770-797`). The array ends exactly at `mRestart` +0x1128, which fits. **Verified**: `dSv_info_c::isSwitch` 0x8005DFB4 (`mulli 0x4C; addi 0x7AA`), `createZone` 0x8005DAD8 (`addi 0x7A8`, `lbz 0(r3)` = `mRoomNo`).
- Room to zone: `dStage_roomControl_c::mStatus[64]` (static, `ww_linker.ld` `0x803BDC88`), stride 0x114, `mZoneNo` s8 at +0x107 (`include/d/d_stage.h:853-866`). `mStayNo` is at `0x803F6A78`. **Verified**: `getZoneNo` 0x8005DCD0 (`0x803BDC88 + room * 0x114`, `lbz 0x107`); `mFlags` +0x104 (`checkRoomDisp` 0x80040E48). Dan: `mDan` +0x79C with `mSwitch[2]` at +4 (`isSwitch` `addi 0x79C`, `dSv_danBit_c::init` 0x8005CBD0 `stw 4/8`).
- Zone switch bits are u16 words (`i >> 4`), memory bits are u32 words (`i >> 5`).

---

## 2. What vanilla stage data uses (mined, not guessed)

I parsed every `Stage.arc`/`Room*.arc` in the vanilla `files/res/Stage` (156 stages, all layers), using each actor's param getters from the decomp. The research script is not in the repo (the data is Nintendo's). Counts exclude `*Test*` maps. They are approximate: an actor placed on several layers counts once per layer.

| Object (actor name → proc) | memory | dan | zone C0-DF | room E0-EF |
|---|---|---|---|---|
| Drop-down ladder `Mhsg*` (switched ones) | 2 | 0 | 0 | **16** |
| Torch `bonbori` → `d_a_ep` (628 of 771 placements on all maps have **no** switch) | 24 | **45** | 0 | **55** |
| Crystal switch `SW_HIT0` | 15 | 0 | 0 | **14** |
| Floor switches `Kbota_A/B/C` | 30 | 3 | 8 | **20** |
| Iron-boots switch `Hhbot1N` | 4 | 0 | 0 | 1 |
| Ice block `Ikori` | 1 | 0 | 0 | 6 |
| Bombable wall `Wall` | 7 | 0 | 0 | 0 |
| All-enemies-dead `ALLdie` | 7 | 4 | 0 | 10 |
| Door switch `door10/12` (type 0: stop bars) | 24 | 12 | 1 | 3 |
| Kill-all bars `door10` type 2 (`Zenshut`) | 7 | **17** | 3 | 0 |
| Key doors (door10 t4, door12 t1), boss doors | 18 | 0 | 0 | 0 |
| Chest appear switch (`takara*`, func 1/2/4) | 46 | 0 | 0 | 9 |
| Chest "open switch" (`angle.z`) | 42 | 1 | 0 | 1 |

**Takeaway:** the current memory-only sync reaches roughly half the in-room puzzle objects. The ladders in the question are almost all "kill every enemy → `ALLdie` sets room switch 0xE0 → ladder drops". The rooms are `Abship/0-7`, `PShip/0-2`, `SubD45/0-2` and `SubD71/1-2`, and in each of them `ALLdie` and the ladder share switch 0xE0.

---

## 3. How each object reacts to a bit set by someone else while you are in the room

Classes: **A** = polls and reacts live (works as soon as the bit is in your live memory). **A+ev** = polls, but the change plays as an event (camera cutscene) on your game. **B** = reads only at create (needs a reload or a REL poke). **C** = the change only happens inside an event or needs the player.

| Object | Actor | How it reads the flag | Class | A remote bit today | Minimal fix | Risk |
|---|---|---|---|---|---|---|
| **Chest, normal** | `d_a_tbox` | `checkOpen` (`isTbox`) only in `CreateInit`/`setDzb` (`d_a_tbox.cpp:454, 342`). `actionOpenWait` (`:954-996`) and `boxCheck` (`:553-563`) never re-check | **B** | Stays closed **and can be opened again, giving the item again** (7.1) | Re-create from its own params (create sees the bit → spawns open, `:454-465`). Or poke the fields (needs REL-internal PTMFs) | Low. Only when no event is running |
| Chest appears on switch (func 1/4 and TACT 6) | `d_a_tbox` | `actionSwOnWait` polls (`:1000-1016`) | **A+ev** | Plays `DEFAULT_TREASURE_APPEAR` on you | none (or snap, 4) | Event interrupt |
| Chest under vines (func 3) | `d_a_tbox` | `actionSwOnWait2` polls, `setDzb` (`:1018-1025`) | A | Vines clear live | none | - |
| Chest after kill-all (func 2) | `d_a_tbox` | `actionGenocide` counts **your** enemies only (`:1028-1049`). The switch is read only at create (`checkNormal`, `:401-420`) | B | Waits for your own enemies | Re-create | Low |
| Chest on a zone switch ≥ 0xC0 | `d_a_tbox` | `checkNormal` returns FALSE for `swNo >= 0xC0` (`:414`), so the appear always replays | - | - | - | - |
| **Drop-down ladder** | `d_a_obj_ladder` | Create (`:93-99`). `mode_wait` polls (`:149-154`), then `mode_demoreq` orders its event only if the stage has one (`:162-182`), then vibrates and drops | **A+ev** | Works **if** the switch syncs. 16/18 use room switch 0xE0, which doesn't | Zone sync (stage 2) | Event interrupt |
| **Small-key lock** | `d_a_door10/12` | `setKey` only in `actionInit` (door10 `:748-759`), which re-runs only on a stay-room change (`d_door.cpp:156-171`) | B-lite | Lock stays. **Using it spends a second key** (`d_door.cpp:434-448`) | `docs/small-keys.md` §3 wrote `mbEnabled=0`; that froze the game at the door (0.1). Stage 1 re-creates the door instead | Low |
| Stop bars (door type 0/3 with a switch) | `d_a_door10/12` | `actionWait` → `chkStopOpen` polls `isSwitch` (`d_a_door10.cpp:119-181, 663-704`) and orders the `SHUTTER_DOOR` event | A+ev | Bars open with a camera | none | Event |
| **Kill-all bars** (type 2 `Zenshut`) | `d_a_door10` | Opening checks **your** enemies (`fopAcM_myRoomSearchEnemy`, `:140`). The switch only stops them closing on the next entry (`chkStopClose`, `:185-222`) | local | Each player fights their own copies | none (4, stage 4) | - |
| Boss door | `d_a_door10` t1 | Boss key = dungeon-item bit, read live in `setEventPrm` (`:362`) | A | Works | - | - |
| Knob doors | `d_a_knob00` | Event bits only (`:172-330`) | (story) | - | - | - |
| Sliding shutter `Htobi*` | `d_a_shutter(2)` | Edge-detects every frame (`d_a_shutter.cpp:142, 233-266`). **It only moves inside its event** (`shutter_move`, `:146-229`) | A+ev | Opens with a camera | none. An event-less path needs a re-create (create reads the switch, `:79-86`) | Event |
| Wooden/metal bars | `d_a_mdoor` | `actionSwitch` polls and orders an event (`:322-329`). The genocide type uses your enemies (`:276-290`) | A+ev | | | |
| FF / Puppet Ganon double doors | `d_a_mbdoor` | `checkUnlock` polls (type 0) or uses your enemies (type 1) (`:432-450`), plus an event | A+ev | | | |
| ATdoor | `d_a_atdoor` | Polls on and off (`:115, 137`) | A | | | |
| **Crystal switch** | `d_a_swhit0` | State read only in `CreateInit` (`:166-173`). `actionOffWait` reacts to hits only (`:241-268`) | **B (visual)** | Whatever it controls reacts (those poll). The crystal still shows "off" | Re-create | Low |
| ↳ timed crystal | | The timer that turns the switch **off** starts only on a local hit (`actionOnWait`/`actionOnTimer`, `:328-365`) | - | **Latches on** (e.g. `kindan` room 13, 0x08-0x0C, memory, 3 s) | Re-create: the new one starts its own timer and turns it off, which matches the peer | Medium |
| **Floor switch** obey-save (types 0, 2) | `d_a_obj_swpush` | `mode_upper` polls `is_switch` when `FLAG_OBEY_SAVE` (`:481-529`). Event if the stage has one (`demo_reqSw_init`, `:679-692`) | A(+ev) | Goes down live | none | - |
| ↳ pressure plate (type 1 `Kbota_B`, `FLAG_UNK20`) | | Doesn't obey the save. Clears the switch on release **only if pressed locally** (`mode_l_u`, `:613-624`) | - | **Latches on** (DRC room 12 0x41, WT room 2 0x0D/0x0E) | Exclude or edge-sync (5) | Medium |
| Iron-boots switch | `d_a_obj_swheavy` | Type 3 obey-save polls (`:310`). Type 2 **toggles** (`rev_switch`, `:334`) | A / toggle | Toggles drift apart | Exclude toggles | Low |
| Hammer switch | `d_a_obj_swhammer` | `mode_upper` polls (`:348`) | A | | | |
| **Torch** | `d_a_ep` | Unlit: `ep_move` case 0 polls, lights (`SHOKUDAI_SWITCH` event if present) (`:230-245`). Lit: polls and goes out when cleared (`:290-297`) | **A** (both edges) | Lights live | none | - |
| ↳ timed torch | | The timer starts only on a local hit (`:224`) | - | **Latches lit** (ToTG `Siren` room 0, 0x03/0x04, memory) | Off-edges (5) | Low (makes the puzzle easier) |
| **Bombable wall** | `d_a_wall` | Create returns ERROR when the switch is set (`:141-145`). Breaking it ends in `fopAcM_delete` (`:216`) | **B** | Wall stays until reload | **Delete** (+ SE/particle) | Low |
| Breakable floor (WT) | `d_a_floor` | Create (`:60`). Break = delete (`:86-87`) | B | | Delete | Low |
| FF barricade | `d_a_obj_majyuu_door` | Create (`:147`) | B | | Delete | Low |
| Wooden barricade (2 halves) | `d_a_saku` | Create (`:714-722`) | B | | Re-create | Low |
| Ice block | `d_a_obj_ice` | `chk_appear` at first create only (`:105-116, 232`). 6/7 use room switches | B | | Delete (after zone sync) | Low |
| Black boulder (on DRC / ET warp jars) | `d_a_stone2` | `chk_appear` at create only (`:171-193`) | B | | Re-create (built, 0.2) | Low |
| **Push block** | `d_a_obj_movebox` | Its position is a **path index encoded in swSave1/swSave2** (`path_save`: `:1163-1206`, sets **and clears**), read only at create (`path_init`: `:1129-1160`) | B | See 7.4: OR-merging corrupts it | Exclude these switch pairs from sync | **High if merged** |
| Grapple post | `d_a_kui` | Execute polls (`:362-381`) | A | | | |
| ToTG light bridge/stairs | `d_a_lbridge`, `d_a_lstair` | Edge-detect every frame (`d_a_lbridge.cpp:136-165`) | A | | | |
| Warp jar | `d_a_obj_warpt` | Only a jar **without** a lid of its own polls (`modeClose :471`: the switch of the boulder on a three-way jar). A lidded normal jar reads its lid switch at create only; a three-way jar's lid is an event register (0.2) | B / A | | Re-create (built, 0.2); registers not synced | Low |
| Wind tag | `d_a_wind_tag` | Polls on and off (`:301-353`) | A | | | |
| Gates `Hami3/4` | `d_a_obj_hami3/4` | Poll, then event (`hami3 :121`, `hami4 :83`) | A+ev | | | |
| Area switch `SW_C00` | `d_a_swc00` | Type 0 sets while **your** Link is inside and clears when outside (`:13-30`) | local | A remote "on" is cleared at once (harmless). One-shot types latch | - | - |
| AND / all-dead controllers | `d_a_andsw0/2`, `d_a_alldie` | AND polls (A). All-dead counts **your** enemies (`d_a_alldie.cpp:28-47`) | A / local | | | |
| Timer | `d_a_obj_timer` | Turns its switch off (`:72-73`). Mostly zone | - | | | |
| Water level | `d_a_tag_waterlevel` | Changes inside an event (`:87`) | C | | | |
| Rope bridge, hookshot target | `d_a_bridge`, `d_a_obj_hfuck1` | No switch | local | | | |
| ToTG statues/gate, ET face statue, Hyrule triangles/knights | `d_a_obj_try`, `_htetu1`, `_Vds`, `_tribox`, `_zouK` | Decomp stubs ("Nonmatching") or `isCollect` | **(?)** | | Not assessed | |

### Why a REL "poke" is needed at all, and how
All these actors are **RELs** (only 26 actors are in main.dol: `config/GZLE01/splits.txt`), so their methods are not in `ww_linker.ld` and can't be called. The REL can use the DOL's `fopAcIt_Judge`, `fopAcM_delete`, `fopAcM_create`, `fopAcM_SearchByID` and `fpcLy_SetCurrentLayer`, plus writes at verified field offsets. That gives three actions:
1. **Delete** (walls, floors, ice, FF barricade), like `puppet_worldsync.c` does for items.
2. **Re-create**: `fopAcM_delete` the old instance, then `fopAcM_create(procName, BASE_PARAMETERS, &home.pos, roomNo, &home.angle, &scale, subtype)` (`src/f_op/f_op_actor_mng.cpp:154-160`). The new instance's create reads the current flags and comes up in the end state with no animation and no event. This is the generic fast-forward for chests, crystals, barricades and shutters. **Traps:**
   - **Layer.** During execute the current layer is the *executing* actor's (`src/f_pc/f_pc_base.cpp:47`), which is the puppet's play-scene layer. A chest created there would survive the room unload and duplicate on reload. **Built:** the old actor's own layer (`mLyTg.mpLayer` +0x2C, verified in `fpcBs_Execute` 0x8003C924) around the create; `fopAcM_create` creates in the current layer (0x80024568).
   - `setID` is lost. That doesn't matter for objects, but don't re-create enemies.
   - Only create when no event runs (`dComIfGp_event_runCheck`) and never re-create an actor mid-demo.
   - Params and angle must come from `home` **(verify per actor that create doesn't rewrite `home.angle`)**.
   - A deleted actor leaves the execute queue at once (`src/f_pc/f_pc_deletor.cpp:79-81`), so it can't order an event after the poke.
3. **Byte write**: the small-key lock (`mbEnabled`), a door's action byte (door10 `m354` +0x354, door12 +0x314, **verified** in each REL's `actionInit`), and the ladder `mEventIdx` +0x344 (`d_a_obj_ladder.h:78`, not needed and not verified).

---

## 4. The event problem

- **Mechanism.** A reacting actor calls `fopAcM_orderOtherEventId(this, idx)` → `dComIfGp_event_order(dEvtType_OTHER_e, …, this, player0)` (`f_op_actor_mng.cpp:729-740`). `dEvt_control_c::check` starts the first acceptable order whenever no event is running (`src/d/d_event.cpp:542-573`). `demoCheck` (`:297-329`) only needs the event manager to accept it: `beforeFlagProc` checks nothing but TALK (`:108-114`). **Nothing checks what the local Link is doing**, so a remote bit can start a camera cutscene while you jump, swim or fight. Vanilla already does this for timers and last-enemy kills, so the player code copes with it in general. **(?)** Test it specifically while climbing, hanging, sailing, and in the puppet/held-item paths.
- **Events involved:** `DEFAULT_TREASURE_APPEAR` (tbox), `SHUTTER_DOOR` (door bars, mdoor), `MBDOOR_STOP_OPEN`, `DEFAULT_SWITCH` (crystal types 1/3), each ladder's and floor switch's stage event (only if it exists: `dComIfGp_evmng_existence`), shutter open/close, `AMI4_OPEN`, and `SHOKUDAI_SWITCH` (torches, if the stage defines it).
- **Options:**
  1. **Let it play (recommended default).** It is exactly what vanilla shows when the switch changes, and it tells you what your friend did. It costs nothing.
  2. **Snap (no cutscene).** C# hands the room's new bits to the REL instead of writing them. In the same tick the REL writes the bits, deletes each actor that would react, and re-creates it (3). The old instance never sees the bit, so it never orders the event. The new one's create reads the end state (shutter `:79-86`, mdoor `CreateInit :149-158`, ladder `:93-96`, tbox `:454`). Use it when an event is already running, when the local Link is in a no-interrupt proc, or behind a room rule "Show other players' cutscenes".
  3. **Per-actor "no event exists".** Writing the ladder's `mEventIdx = -1` makes `mode_demoreq` skip straight to the drop (`:166-181`). It does **not** work generally: a shutter with no event index never moves (`d_a_shutter.cpp:258-266`). So prefer 2.
- **The same principle as the puppet REL.** `puppet_initBoatPose` (`GameMod/src/puppet_link/puppet_execute.c:194-204`) never runs the ship `*_init`s, which touch global state. It reproduces the end pose and relabels the proc. Here too: **reproduce the end state and never start a global event from a remote cause**, except in option 1, where the event is the vanilla reaction on *your* own game.
- **Never mirrored:** events that grant items or story (chest open, item get, talk). The remote player's *interaction* (opening a chest, pulling a lever) is not replayed on you. Only its *result* (the flag) is.

---

## 5. Zone and dan switches: sync them per room / per dungeon visit? (built as §0 stage 2)

**Why it matters:** section 2 shows ladders, most torches, about half the crystals and floor switches, and most kill-all bars use them.

### Scope
- **Room switches (0xC0-0xEF)**: key them by `(stage, room)`. They are live puzzle state for the current visit of that room. Share them only between players who are both in that room. `PuppetVisibility.IsSameLocation` already decides this. At sea each grid square is a room with its own zone, and puppets there are shown by distance, so use the room number, not visibility.
- **Dan switches (0x80-0xBF)**: key them by save slot. They live for the whole dungeon visit. Share them while both players are in stages with that slot. A server-side store per slot that is **cleared when the last player leaves the slot** mirrors vanilla semantics: a fresh visit starts clean.

### Game side: C# only for the reacting objects
The objects poll, so C# writes the bits and they react (A/A+ev):
- `zoneNo = *(s8*)(mStatus + room·0x114 + 0x107)`. Require `zoneNo >= 0` and `mZone[zoneNo].mRoomNo == room`.
- OR the u16 into `GameInfo + 0x7A8 + zoneNo·0x4C + 0x02 + (i>>4)·2`. Dan: `GameInfo + 0x79C + 4 + (i>>5)·4` **(verify both)**.
- The REL table (3) only matters for B-class objects on these switches, such as ice blocks.

### Protocol
A new hub method `SendRoomSwitches(RoomSwitchEdges { Stage, Room|Slot, OnBits, OffBits, Epoch })` and a callback. The server relays to others and keeps a per-slot dan store. It validates stage length, room 0..63 and slot 0..15. **Bump `HubConstants.ProtocolVersion`.** The payload is tiny: 6 bytes of zone bits, or 8 bytes of dan bits.

### Hazards
- **Switches the game turns off.** Timers (`d_a_obj_timer :72-73`), timed crystals and torches, pressure plates (swpush type 1), area switches (swc00 type 0 clears the bit every frame you are outside) and toggles. OR-state sync latches them on.
  - v1: sync **on-edges only**, for a **whitelist of latching setters**. Build the list at patch time from the player's own vanilla files: which actor writes switch N in room R (untimed crystal, untimed torch, obey-save floor switch, `ALLdie`, AND switches, `kui`). That keeps Nintendo data out of the repo.
  - v2: add off-edges for timed switches.
- **Echo loops.** A remote "on" makes the local game's actor react. A pressure plate or area switch then turns it off, which would send "off" back. Rules:
  - Never forward an edge on a bit within ~1 s of applying the opposite remote edge.
  - Never send the clears caused by leaving a room (`clearRoomSwitch`). Only send while the scene has been stable in the same room for N ticks, and reset per-room state on a room change.
- **Two players setting it at once:** idempotent for on-edges.
- **Joining a room late:** the room's current state should come from the players already there. Have each client re-send its room's latched bits once when a peer enters the same room. That is safe for whitelisted latching switches only.
- **Zone not alive yet:** the room is loading, so `mZoneNo < 0`. Queue the bits until it exists.
- **Room switches in rooms both players can't reach:** harmless. The bits are dropped when the room changes.

---

## 6. Changes that set no flag, or set one only at the end

| Action | Flag? | Notes |
|---|---|---|
| Pushing a block | Only for path boxes: a 2-bit path index in swSave1/2, written when a push ends (`d_a_obj_movebox.cpp:1163-1206`). Otherwise none | Pushing is purely local. The *effect* (block on a floor switch) syncs through that switch |
| Lighting a torch | 143 of 771 torch placements (all maps) have a switch. **628 have none** | The switchless ones are purely local |
| Cutting grass, breaking pots or barrels | none | Random drops per game. Local |
| Killing enemies | `onActor(setID)` zone bit, so no respawn while the zone lives. Some have a **death switch**: Bokoblin `m02B8` (`d_a_bk.cpp:2645, 2731`), Moblin (`d_a_mo2.cpp:2409`), Stalfos (`d_a_st.cpp:1237, 2218`), Darknut (`d_a_tn.cpp:2071`); plus `ALLdie` for "room cleared" | The death switch syncs if memory (or dan/zone after stage 2). Each game's enemies are separate, so "room cleared" rooms (`ALLdie`, kill-all bars, genocide chests) need your own kills, **except** that a synced `ALLdie` switch opens whatever polls it (e.g. `M_Dai` room 8: `ALLdie` 0x34 → ladder 0x34) |
| Picking up placed items | memBit item bits | Already synced, plus the REL live despawn |
| Zone items (item no. 0x40-0x4F) | zone `mItem` | Not synced. Could ride stage 2 |
| Deku Leaf leaf piles, bomb flowers | `d_a_obj_leaves :1`, `d_a_bflower :1` switch uses | Minor. Not assessed further |

---

## 7. Hazards

### 7.1 Double items from chests (a bug in the current code)
If the other player opens a chest while you are in the same room, your chest stays closed: `checkOpen` runs only at create. Opening it gives you the item again, because `actionOpenWait` and `boxCheck` never look at `mTbox` (`d_a_tbox.cpp:553-563, 954-996`), and `OpenInit_com` just sets the bit again (`:850-871`). The duplicate then spreads:
- **Shared wallet** is delta-based (`WWOnline.Server/Hubs/WalletStore.cs:5-17`), so a duplicated rupee chest pays everyone twice.
- **Shared items** used to MAX-merge heart quarters (`RoomInventoryStore`), so a re-opened heart-piece chest gave *everyone* an extra quarter. Max health is now derived from the room's heart sources (`docs/hearts.md`): the chest is one source, so the second piece is logged and taken back.
- **Small keys** are handled in `docs/small-keys.md` §4.

`puppet_worldsync.c:15` ("Chests ... need nothing here") is only true across a reload. Stage 1's chest re-create closes this. A residual race remains: both players open within about 0.4-0.9 s plus network latency (the REL's 4-frame cadence, the 4 Hz world-sync ticks, the read-back). Closing it needs **server-side chest-claim arbitration** (a future stage: a player's chest-open event asks the server first, and a second claim for the same chest gets an empty chest), not more client speed.

### 7.2 Other hazards
- **Items when Shared items is off:** the opener gets the item and your chest pops open empty. That is the world rule working as designed, but say so in the UI.
- **Latches** (5): timed torches and crystals, pressure plates and toggles stay on for the receiver. Today this makes puzzles easier and doesn't softlock. `SwitchApplyGuard` stops the latch coming back, but never undoes it.
- **Push-block position corruption.** Box at path point 1 in game A (`sw1=1, sw2=0`), point 2 in game B (`sw1=0, sw2=1`): the OR-merge gives point 3 on the next load. At worst the block sits somewhere unsolvable, or on a switch it shouldn't. Exclude the movebox `swSave1/swSave2` pairs from sync (patch-time table), or treat them as last-writer-wins.
- **Softlocks:**
  - A stale key lock with a count of 0 in a one-way room (fixed by the small-keys plan).
  - Area-switch one-shots firing arena bars for a player outside the arena. The bars reopen when the peer's boss/miniboss death switch syncs, but it is still worth a stage-0 check.
  - Zone off-edges without the echo guard.
- **Scene stability.** C# writes the live memBit only behind `SceneStabilityGate`. REL pokes must keep `puppet_worldsync_tick`'s gates: `NEXT_STAGE_ENABLE == 0`, stag pointer valid, stage tag matches. They must also add "no event running" and "target room is loaded (`checkRoomDisp`)". Zone writes need `mZoneNo >= 0` and must be for the local stay room.
- **Save data:** applied memBit bits are saved by your game, which is intended. Dan and zone bits are never saved, so stage 2 can't corrupt a save.
- **REL budget:** since the REL is stored Yaz0 (#11), RELS.arc grows by about 35 KB of its 64 KB ARAM guard (v0.1.0). Stage 1 costs 1680 bytes of REL.

---

## 8. Staged plan

### Stage 0: find out what already works (no code)
The expectations below are the behaviour **before** stages 1-2 (kept as the baseline they fixed). Two players, **Shared world on** (and Shared wallet on for T1). P1 acts while P2 stands in the same room. Check `logs/latest/client-Player2.log` for `[world] applied … (current stage, live)` and watch P2's screen. Room numbers are stage-file rooms (`M_NewD2` = Dragon Roost Cavern, save slot 3; `Siren` = Tower of the Gods; `kindan` = Forbidden Woods; `kaze` = Wind Temple; `M_Dai` = Earth Temple).

| # | Where | P1 does | Expected on P2 | Class |
|---|---|---|---|---|
| T1 | DRC room 10, yellow-rupee chest (tbox 10) | Opens it | Chest **stays closed**. P2 opens it and **gets 10 rupees again** (the wallet shows +20 total). Leave and re-enter: chest is open | B / bug 7.1 |
| T2 | DRC room 6 (torch 0x0E and a chest that appears on 0x0E) | Lights the torch | Torch lights. **The chest-appear cutscene plays on P2** | A + A+ev |
| T3 | DRC room 11, floor switch 0x01 (latching) | Steps on it | Switch goes down (+ event if the stage has one) | A |
| T4 | DRC room 12, pressure plate 0x41 (type 1) | Presses it, steps off | P2's target stays **on** (latched) | latch |
| T5 | DRC room 16, plates 0xE0/0xE1 (room switches) | Presses them | **Nothing** on P2 | not synced |
| T6 | DRC key door (`keyshut`, 0x36/0x3C/0x3D/0x3E) | Unlocks it | Lock **stays** until P2 changes room. If P2 uses a key on it, a key is spent | B-lite |
| T7 | DRC kill-all bars (`Zenshut` 0x3F) | Kills P1's enemies | P2's bars stay closed until P2 kills P2's enemies | local |
| T8 | ToTG room 0, bombable walls 0x38-0x3A | Bombs one | Wall **stays** until a reload | B |
| T9 | ToTG room 0, timed torches 0x03/0x04 | Lights one | Lights on P2 and **never goes out** (P1's does) | latch |
| T10 | ToTG room 1, shutter `Htobi1` 0x07 | Triggers it | Shutter opens **with a camera event** | A+ev |
| T11 | FW room 13, timed crystals 0x08-0x0C | Hits one | Crystal still looks off on P2, but its effect is on and latched | B + latch |
| T12 | WT room 2, iron-boots switch 0x09 / plates 0x0D, 0x0E | Uses them | Iron switch: live. Plates: latched | A / latch |
| T13 | ET room 8, `ALLdie` 0x34 + ladder 0x34 | Kills P1's enemies | **P2's ladder drops with its event** while P2's enemies are still alive | A+ev |
| T14 | Any A+ev case (T2, T10, T13) with P2 mid-jump, swimming, on a ladder or ledge, or holding an item | Triggers it | Note whether P2's event starts cleanly, waits, or misbehaves | event safety |

### Stage 1: REL poke for create-only objects on memory switches and chests — **built (§0)**
- **C#** (`WorldFlagSyncService`): track `remoteTbox` and `remoteSwitch[4]` for the current stage, i.e. bits applied from others. This is the same idea as `_remoteItemMask`.
- **Layout.** Publish them in a **REL-allocated game-heap block** like `PUPPET_NAMES_*` (magic, boot stamp, stage tag, masks, a "handled" mask the REL owns, a counter and the last action). Its pointer goes in the free scratch word **0x803FD14C** (`puppet_shared.h`; `0x803FD16C` became the projectile events block's pointer).
- **REL** (`puppet_worldsync.c`): one `fopAcIt_Judge` pass over a small table `{procName, param field/shift/width, bit space, action}`:
  - `TBOX` (tbox no. = `(prm>>7)&0x1F`, `include/d/actor/d_a_tbox.h:35`) → re-create.
  - `TBOX` func 2 (`swNo = (prm>>12)&0xFF`) → re-create.
  - `WALL`, `FLOOR`, `Obj_Ice`, `MjDoor` → delete.
  - `SWHIT0` (`prm&0xFF`), `SAKU` → re-create.
  - `DOOR10/12` small-key lock → the `docs/small-keys.md` §3 byte write. (Built as a re-create instead: the byte write froze the game, §0.1.)
- **Gates:** as in 7.2. Handle each bit once per stage tag.
- **No protocol change.** Needs a re-patch (build stamp). REL about +1-1.5 KB. Put the diagnostics in the block, not in scratch.
- **Effort** 2-3 days. **Risk** low-medium. The layer trap (3) is the one to get right. Fixes bug 7.1, the stale key locks, walls and crystal visuals.

### Stage 1b: switch semantics table (patch time) — **built (§0)**
- The patcher already reads the vanilla game. Build `(stage, room, switch) → setter kind` from the player's own files (`tmp/stage_mine.py` ported to C# on `RarcArchive`/`Yaz0Codec`). Kinds: latching, timed, toggle, pressure, area, path-encoded (movebox).
- Ship it in the game folder, not the repo.
- C# stops syncing path-encoded movebox bits (7.2) and can mark latch-risk bits.
- About 1-2 days. Low risk. **Needed before stage 2.**

### Stage 2: dan and room (zone) switches for players in the same dungeon or room — **built (§0)**
Section 5. C# writes the bits (the objects poll), plus a hub method, DTO, per-slot dan store and **ProtocolVersion bump**. Whitelisted latching setters only, on-edges only, with the echo guard. This is **what makes most ladders, torches, crystals and floor switches live.**
- 3-5 days. Medium risk (echo and latch rules, a zone address to verify). No REL change unless B-class objects on zone switches need the stage 1 table (ice blocks).

### Stage 3: event policy and off-edges — **not planned** (cutscenes are handled by their flags)
- "Snap" mode (4, option 2): C# hands bits to the REL, which applies them and re-creates the actors that would react in the same tick. Used as a fallback when an event is running or Link is busy, and behind a room rule "Show other players' cutscenes".
- Off-edges for timed torches and crystals (synced timers).
- 3-4 days. Medium-high risk (per-actor create quirks, event races).

### Stage 4: shared enemy deaths (research spike) — **not wanted**
Sync `dSv_zoneActor` set-ID bits per room, and in the REL delete (with a death effect) the local enemy whose `setID` (`fopAc_ac_c` +0x1BC, verified in `fopAc_Create` 0x80023E20) the peer killed. That would make kill-all rooms, genocide chests and `ALLdie` truly shared.
- 1 week+. High risk: enemies mid-attack, carried items, bosses (exclude them), `setID` 0xFFFF actors. Do it only if stage 0-2 feel right.

### Stage 6 (future): server-side chest-claim arbitration
Closes the chest race of 7.1: opening a chest asks the server for the claim before the item is granted; the loser's chest opens empty. Needs a REL hook on the chest-open path and a hub method.

### Stage 5 (optional): mirror the peer's interaction on the puppet
Chest-open, lever and door-push poses, as relabelled procs (the `puppet_initBoatPose` pattern). Visual only. Rides on the held-items work.

| Stage | Delivers | Needs | Effort | Risk |
|---|---|---|---|---|
| 0 | Checklist of what works now | nothing | ½ day of play | none |
| 1 | Chests open live (no duplicate items), walls/ice/barricades vanish, crystals show on, key doors come back unlocked | C# masks + REL heap block + judge table; 1 scratch word (0x803FD14C); re-patch; no protocol change | 2-3 d | low-med |
| 1b | Switch semantics table; no push-block corruption | patcher step, C# filter | 1-2 d | low |
| 2 | Ladders, most torches, crystals and floor switches live for players in the same room/dungeon | C# zone/dan read-write, hub method + DTO + dan store, **ProtocolVersion bump** | 3-5 d | med |
| 3 | No-cutscene mode, timed puzzles in sync | REL apply-and-recreate, C# rule | 3-4 d | med-high |
| 4 | Shared enemy kills | REL + zone actor sync | 1 wk+ | high |
| 5 | Puppet shows the interaction | REL pose relabels | 1-2 d | low |

**Recommendation:** run stage 0 first (an hour of two-player testing settles the event question, T14). Then **stage 1**: it fixes a real duplication bug and is small. Then 1b → 2, which is the big "the ladder drops for everyone" win. Stages 3-4 only if players want them.
