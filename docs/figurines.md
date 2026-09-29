# Nintendo Gallery figurines

With **Shared story** on, the room shares which Nintendo Gallery figurines Carlov has made. Players can split the Picto Box work: a figurine anyone gets made shows on its pedestal in everyone's gallery, and Carlov tells everyone "I've already made that one" for it.

The photos themselves are not shared. They are stored in ARAM and on the memory card, so each player gives Carlov their own photos.

Code: `StoryFlags.Figurines` (Shared), `StorySyncService` (client), `StoryFlagStore` / `GameHub.JoinRoomStory` / `SendStoryFlags` (server). Log lines: `[story]`, with the figurines listed as `figurines 0x12, 0x40`.

## Where the collection lives

The collection is a bitfield of 134 bits (`TOTAL_FIGURE_COUNT` = 0x86, `d_a_npc_mt.cpp:14`, `d_a_obj_figure.cpp:23`), spread over 17 **event registers** in `dSv_event_c` (`g_dComIfG_gameInfo` + 0x624 = 0x803C522C).

- Figurine `n` is bit `n % 8` of register `l_figure_comp[n / 8]`.
- `l_figure_comp` is 95, 94, 93, 92, 91, 90, 8F, 8E, 8D, 8C, B1, 9C, 84, 83, 82, 81, 80 (each used as `0xXXFF`, the whole byte).
- The last register (0x80) holds figurines 0x80-0x85 in bits 0-5. Bits 6-7 are unused, and sync never touches them.
- Figurine numbers are Picto Box photo types minus 0x49 (`dSnap_PhotoIndex2TableIndex`, `d_snap.cpp:1443`). Two have special roles: 0x40 is the one Carlov adds himself when the gallery is complete, and 0x32 is the optional one (see below).

Who reads and writes it:

| Code | What |
|---|---|
| `daNpcMt_c::setFigure` (`d_a_npc_mt.cpp:1366`) | `getEventReg`, OR the bit, `setEventReg`, then sets event flag 3A01 ("a figurine has been made"). The only writer. |
| `daNpcMt_c::getMsg` (`d_a_npc_mt.cpp:915`) | A photo of a figurine that is already made gets "already made" (`l_msg_mt_maked`). When the finished figurine is handed over, `setFigure` runs for it (plus the figurines that come with some photos: 0x00 with 0x01, 0x2A-0x2F with 0x29, 0x44 with 0x43, 0x3E with 0x3F). |
| `daNpcMt_c::isComp` (`d_a_npc_mt.cpp:1390`) | Complete at 0x84 figurines, or 0x85 if the optional figurine 0x32 is made. `phase_1` then sets 3D08 ("gallery complete") and figurine 0x40, and Carlov no longer appears. |
| `daObjFigure_c::isFigureGet` (`d_a_obj_figure.cpp:999`) | Whether a pedestal shows its figurine (read once, when the gallery room loads). |
| `daNpcMn_c::getPosNo` (`d_a_npc_mn.cpp:1633`) | Manny picks a gallery room that has figurines. |
| `dSv_info_c::reinit` (`d_save.cpp:1402`, `l_holdEventReg`) | New Game+ keeps exactly these 17 registers. |

**Checked against the game files:** the 17-entry table (as big-endian `u16`s `95FF 94FF ... 80FF`) is at 0x80375DC8 in main.dol (`l_holdEventReg`, matching `symbols.txt`), and appears once each in `files/rels/d_a_npc_mt.rel`, `d_a_npc_mn.rel` and `d_a_obj_figure.rel` (after Yaz0), the three `l_figure_comp` copies. The event catalogue already classed these registers as `BitwiseOr` (docs/event-flags.md), and they are its only whole-byte `BitwiseOr` registers.

## What syncs, and why under Shared story

The figurines are save event bits that only ever gain bits (`setFigure` ORs), so they merge the way the story flags do: OR, grow-only, owner seeds, joiners merge up.

They ride in the room story (`StoryFlags.Figurines`, beside the flag `Bits`) rather than a new rule, for two reasons:

- They live in the same `dSv_event_c` array as the story flags. The gallery's own flags were already under Shared story: 3A01 (a figurine was made, `SideQuest`) and 3D08 (gallery complete, `SideQuest`).
- Sharing 3D08 without the figurines was inconsistent. A player who received "gallery complete" lost Carlov (his `phase_1` stops on 3D08) without having the figurines. Now the two travel together.

Shared items was the other candidate. It holds `dSv_player_*` inventory arrays, not event data, so it is not a fit.

How the client applies them (`StorySyncService`):

- It reads the whole 256-byte event array each tick. A figurine this game has made that the room hasn't seen is sent. If the send fails, the client rejoins the room story, which merges it up again.
- Room figurines this save is missing are ORed into their registers one byte at a time (`ApplyBits`), exactly like `setFigure` but without its 3A01. 3A01 comes through the story flags from whoever made the figurine.
- Figurines are written **only while the game is idle** (no event running, pause menu closed; `SceneStabilityGate.IsIdle`). The game changes them only in Carlov's talk event, so a write never races `setFigure`. While not idle they wait, and flags are still applied.
- Like the flags, a figurine is written at most once per loaded save and never re-forced.

Carlov's first-talk flag (2F02, `NpcState`) was already shared with the story flags. It only changes what he says.

## What does not sync

| What | Where | Why |
|---|---|---|
| Carlov's figurine in progress (one at a time, ready after a day) | flags 2F01 (in progress), 3080 (ready, set by `dKankyo_DayProc`), 4080 (handed over), 3F01 / 4040 (gallery room visit), register A9 (which figurine) | All `LocalOnly`: Carlov clears them himself. Sharing them would wedge his cycle, and each player gives him their own photo. |
| The photos | ARAM, memory card | Not in the save RAM we sync. |
| Picture count | register 89 | `LocalOnly` (each player's Picto Box). |
| Colour photos | the photo's texture format (`GX_TF_I4/I8` = black and white, `getMsg`) | Depends on the box held when the photo was taken. The Deluxe Picto Box itself comes through **Shared items** (the Picto Box slot upgrades), and Lenzo's quest flags through Shared story. |
| Gallery rooms | stages `figureA`-`figureG` | Nothing to share beyond the figurine bits: each pedestal (`daObjFigure_c`) reads its bit once, when its room loads. A figurine that arrives while you stand in the gallery appears the next time the room loads. Carlov's own room-visit state (3F01 / 4040) is in the first row. |

Neither the **Joy Pendants** nor the **Tingle Statues** are part of the figurine bitfield. Joy Pendants are spoils (Shared spoils bag). A Tingle Statue's item-get function writes nothing (`item_func_tincle_statue01`, `d_item.cpp:1334`), and the chest it came from is a stage flag (Shared world). The Tingle-family figurines are ordinary figurine bits. The optional one, 0x32, is photo type 0x7B (`DSNAP_TYPE_NPC_TC_BLUE`); `isComp` counts it only once someone has it.

## Carlov's in-progress figurine is safe

When a figurine arrives from the room, the receiving player's own job with Carlov keeps working:

- **The same figurine was in progress:** his flags and register A9 are untouched. When it is ready, his `setFigure(A9)` ORs a bit that is already set, and he plays his normal "it's done" lines.
- **A new photo of a figurine the room already has:** he says "already made" and takes no job, which is the reason for sharing.
- **Mid-conversation:** figurines are not written during an event, so `getMsg` sees one consistent collection for the whole talk.
- **The collection completes through the room:** on the next visit Carlov's `phase_1` finds `isComp` true, sets 3D08 and figurine 0x40, and leaves, as in the unmodified game. A job still in progress is dropped, but its figurine is already made.

## Only a game session can check

- Carlov's dialogue with a room figurine: "already made" for a synced figurine, and a job for the same figurine finishing normally.
- A synced figurine appearing on its pedestal after re-entering the gallery room.
- The whole collection arriving at once (joining a room with a full gallery): 3D08, Carlov leaving, Manny's completion lines.
