# GameMod: injected C/ASM rules

This folder holds the game-side half of the mod. It is compiled by devkitPPC via `WWOnline.Patcher`, and **only when the user asks** (see the root CLAUDE.md).

## Key rules
1. **`tww-decomp/` is the authority.** Look up every offset, proc id, flag and behaviour there, and cite it in a comment (`d_a_player_main.cpp:1234`, `d_save.h`). Never guess.
2. **Use offset macros, never struct fields.** `include/ww_structs.h` offsets are wrong (GCC vs Metrowerks padding). Use or add a macro in `include/ww_inlines.h` (`DAPY_LK_MCURPROC(link)`, `GAMEINFO_PLAYERSTATUS(&g_dComIfG_gameInfo)`, ...) with the decomp field in a comment. This applies to `g_dComIfG_gameInfo` too: raw offsets via `GAMEINFO_*` macros only.
3. **Put scratch/sync addresses in `src/puppet_link/puppet_shared.h`, never inline.** It is the memory map shared with C#: the client generates `PuppetLayout.g.cs` from its integer `#define`s. Put one integer literal in each `#define` and keep derived values as macros. Any change there needs a re-patch, which the build stamp enforces.
4. **Keep the hook tiny and put new logic in the REL.** See the table below.
5. **Guard global state.** `daPy_lk_c` code assumes `this` is the local player. Wrap `playerInit`/`playerDelete` (`puppet_saveGlobals`/`puppet_restoreGlobals` in puppet.c) and every proc init/execute (`puppet_guardBegin`/`puppet_guardEnd` in puppet_execute.c) so a puppet never changes the local player's status bits, event flow, hearts, magic, rumble, BGM, HUD or Z-target. Any new call into player code goes inside a guard.
6. **Watch the ARAM budget.** RELS.arc is loaded into ARAM, which is nearly full in vanilla. Only the puppet REL may be replaced (the others stay Yaz0-compressed; `RelsArcReplaceTests`). Keep the REL lean: growth can make the first play scene's ARAM mount fail (`d_s_play.cpp:3439`).

## The two pieces
| | Puppet REL (`puppet.c` + includes) | Draw hook (`link_draw_hook.c`) |
|---|---|---|
| Built by | `BuildRelStep`: C -> ELF -> REL module 0x58 -> RELS.arc | `HookAsmGenerator`: C -> linked at 0x80286000 -> `link_draw_hook.asm` (generated, do not edit) |
| Runs | as actor proc 0xB5 (profile slot redirected to `g_profile_PUPPET`) | from `daPy_Draw` (branch at 0x80108204) |
| Size | limited by the ARAM budget | **0x4C8 bytes max**: live code starts at 0x802864C8 (`HookAsmGenerator.CodeLimit`) |
| `.rodata` | fine: string/float literals, `OSReport` | **none**: no string or float literals (floats via IEEE-754 ints in a scratch word from puppet_shared.h) |
| Also | new sections go in `ElfToRelConverter.AllowedSections` | register allocation is fragile, so add nothing per-frame here |

Code organisation: `puppet.c` holds the profile, create/delete and a thin dispatch. `puppet_execute.c` holds network state, the proc/animation state machine, collision and the global guard. `puppet_draw.c` holds rendering. `puppet_worldsync.c` holds the REL side of world sync (item despawn). `puppet_appearance.c` holds per-Link outfit + tunic colour. `puppet_boat.c` draws the peer's King of Red Lions (hull + head from the "Ship" archive in the puppet's own heap, posed on the local sea) and seats the puppet in it. Never spawn a second `daShip_c`: it takes over the global ship pointer and writes the local player's sail state. `puppet_nametag.c` draws the peer's name above the puppet (see below).

## Memory map
- `0x80286000..0x802864C7` is hook code only, not free space.
- `0x803FCF20..0x803FCFA7` is the DOL's `.sbss2`, which holds live game constants. **Never write here.**
- `0x803FCFA8..0x803FD1FF` (`SCRATCH_REGION_START/END`) is our scratch/sync region. It is mapped word by word in `puppet_shared.h`, which also documents the layout. In a vanilla layout it is the never-reached bottom of the boot-thread stack. When a selected optional patch uses free space, it is the zero-filled start of DolPatcher's Text2 section, and the boot stack moves above the custom code, which starts at `0x803FD200`.
- Game globals (stage, room, save, event flags) are reached via `g_dComIfG_gameInfo` + `GAMEINFO_*` macros in C, and via `GameMemoryAddresses` (GameInfo/Play + decomp offset) in C#.

## C pitfalls
- **Inline functions have no body.** Calling a WW inline jumps to a fake address. Do what it does by offset instead, e.g. `(MTX34*)((u8*)model + 0x24)` for `getBaseTRMtx`.
- **Calling game functions:** functions are named `Class__method` in `include/ww_functions.h`, with addresses in `include/ww_linker.ld`. Globals are in `ww_variables.h`. Only call what is in the linker script.
- **Shared model data:** the puppet is built by the stock `playerInit` from Link's shared archive model data. Anything swapped into shared data (eye/mouth texture, texMtx animator, linktex ResTIMG) must be set right before the puppet's `mDoExt_modelEntryDL` and restored right after (see puppet_draw.c). That works because texture headers are read when the entry builds the model's differed display list (`J3DModel::diff` -> `loadTexNo`), not when the packets execute.
- **Appearance:** outfit and tunic colour both live in the linktexS3TC header (colour = a recoloured copy of the texture; the body TEV never uses the material colour). The REL owns that header for the local Link while it is loaded (`puppet_appearance_tick`), and each puppet swaps its own in around its entry. The C# client only publishes `LOCAL_APPEARANCE_WORD`; never write Link model data from C#.
- **Display packets:** each puppet owns its packets (`PUPPET_class.packets`). Entering one `J3DPacket` twice makes the draw list cyclic, which hangs the game.
- **2D / name tags:** `puppet_nametag.c` projects a point above the puppet (`mDoLib_project`) in its draw and enters its own `dDlst_base_c` (vtable in the REL: `{0, 0, dtor, draw}`) in the 2D opaque list, like the HUD. Its draw runs in the painter's 2D pass: `J2DOrthoGraph::setPort` on the current graf port, then the message font `mDoExt_font0` (held for good since boot, d_s_logo.cpp:576; read it, never `mDoExt_getMesgFont`, which counts a reference) through its vtable, and `setPort` again after. Safe because the painter runs at the start of the next frame, before deletes. Names come from a game-heap block the REL allocates once and never frees (`PUPPET_NAMES_*`), so C# can write it without racing a free. Hidden when the game hides its HUD (`m2dShow`, events, pause menu, telescope, Picto Box).
- **Peer equipment** is presented by swapping the save's select-equip bytes inside the guard. The swap is seqlocked for C# (`PUPPET_EQUIP_SWAP_SEQ_ADDR`), and End restores a byte only if it still holds the peer value.
- **Delete path:** `daPuppet_Delete()` must call `daPy_lk_c__playerDelete()` or room transitions crash. Bounds-check slot indices against `PUPPET_MAX_SLOTS`. NULL-check `polyInfo` / `mpJoints`.
- **Globals** like `l_puppetFollowState` are safe only because GameCube actors run single-threaded. Save and restore them per puppet.
- Compile flags (`DevKitPpcToolchain`): `-mcpu=750 -std=c99 -fno-inline -Wall -Og -g -fshort-enums -fno-jump-tables`. `-fno-jump-tables` avoids R_PPC_REL32 relocations.

## ASM patches (`src/patches/`)
WW_Hacking_API style (`.open "sys/main.dol"`, `.org <addr>`, `.close`). Every `.asm` file directly in `src/patches/` is a **required** patch picked up by `AssemblePatchesStep`: `use_extra_memory.asm` (48MB, which also needs bi2.bin) and the generated `link_draw_hook.asm`.

`src/patches/optional/<id>.asm` are **player-selectable** patches (skip intro, betterww QoL, vanilla bug fixes). Each file's `; @name / @description / @category / @default / @match / @multiplayer / @credit` header is its catalogue entry (single source of truth); `@conflicts` and `@edit` (BMG / file / dzb edits in C#) are optional. All of them are assembled every build; only the selected ones are applied. See `docs/optional-patches.md`. Rules for them:
- Never write `.sbss2`, the scratch region or the hook (enforced by `GamePatchApplier.Validate` and tests).
- Custom code goes in main.dol `@NextFreeSpace`, which starts at `SCRATCH_REGION_END` (`free_space_start_offsets.txt`, enforced). Prefix global labels with the patch id: every optional patch is assembled in one symbol table.
- REL patches (`.open "files/rels/x.rel"`) may not use `@NextFreeSpace` or need new relocations: a `bl` into main.dol must replace a call that already had a main.dol REL24 relocation (it gets re-targeted). Put the main.dol block before the REL block so its symbols exist.
- Two patches writing the same bytes must declare `@conflicts`.

`free_space_start_offsets.txt` sets where free space begins in each file.
