# Optional game patches

Players choose which quality-of-life patches go into their patched game. The patches are ported from
[Better Wind Waker](https://github.com/WideBoner/betterww) (WideBoner), which builds on
[wwrando](https://github.com/LagoLunatic/wwrando) (LagoLunatic). Both are MIT, credited in each
patch file and in `THIRD_PARTY_NOTICES.md`.

Required patches (`use_extra_memory`, the generated `link_draw_hook`) are always applied and are not
in this list.

## How it works

**Catalogue: one file per patch.** `GameMod/src/patches/optional/<id>.asm` is a normal
WW_Hacking_API-style patch (`.open` / `.org` / `.close`). Its header comment is the catalogue entry:

```
; @name        Swift Sail
; @description The wind always blows behind you while the sail is out, and ...
; @category    Sailing
; @default     off            ; on/off: selected when the player never chose
; @match       no             ; yes = every player in a room should pick the same
; @multiplayer Local only: ...   (shown to players; may repeat)
; @conflicts   brisk_sail, sail_controls_wind
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
; @edit        bmg-message 463 Swift Sail     (non-ASM edit; may repeat)
```

The id is the file name. `OptionalPatchCatalog` (WWOnline.Patcher/Patches) parses the
headers. Unknown tags, missing tags and bad `@edit` lines fail loudly. A patch can be ASM only,
`@edit` only (`instant_text`, `remove_title_and_ending_videos`), or both.

`@edit` kinds, all implemented in C# (`GamePatchApplier`):

| kind | what it does |
|---|---|
| `bmg-instant-text` | every message in `files/res/Msg/bmgres.arc` → `zel_00.bmg` draws instantly and loses its wait and wait-then-dismiss control codes (`BmgFile`, ported from wwlib/bmg.py) |
| `bmg-message <id> <text>` | replaces one message's text (ASCII) |
| `replace-file <game path> <asset>` | copies `GameMod/assets/<asset>` over a game file |
| `dzb-face-property <arc> <dzb> <face> <property>` | sets one collision triangle's property index in a stage archive |

**Assembling.** `AssemblePatchesStep` assembles the required patches into
`patch_diffs/<name>_diff.yaml`, and **every** optional patch into
`patch_diffs/optional/<id>_diff.yaml`, whatever is selected. So free-space addresses never depend on
the selection, and the pre-built `PatchData/` can apply any selection.

REL targets (`.open "files/rels/x.rel"`) are not linked. `AsmParser` takes the assembled `.text` and
resolves branch labels and absolute main.dol references itself. A `bl` into main.dol stays as a
relocation in the diff.

**Applying.** `ApplyPatchesStep`, and the client's pre-built fallback, call `GamePatchApplier`:

1. `Validate`: no patch writes `.sbss2` or the scratch region (bounds come from `puppet_shared.h`).
   Only required patches may write the draw hook (code and branch site). Two patches never write the
   same bytes.
2. main.dol goes through `DolPatcher`. It now maps any DOL-header section, so `.data` and `.sdata2`
   constants can be patched too.
3. RELs go through `RelBytePatcher`. It works in place, so nothing the patch doesn't touch changes:
   - It writes the bytes.
   - Any vanilla relocation under the patched bytes becomes `R_DOLPHIN_NOP`. Otherwise OSLink would
     overwrite them.
   - A chunk's `bl` into main.dol re-targets the vanilla module-0 relocation of the same type at the
     same offset. **Adding new REL relocations is not supported**, and the build fails if a patch
     needs one.
   - A REL inside `RELS.arc` has only its own entry decompressed, patched and Yaz0-re-compressed.
     The other entries are never touched.
   - RELS.arc must stay within `ElfToRelConverter.MaxRelsArcGrowthBytes` of vanilla. With every
     optional patch, the edited RELs re-compress to within about ±15 bytes of vanilla.
   - Loose RELs (`files/rels/*.rel`) are patched in place.
4. The selected patches' `@edit` lines run. Archives keep their Yaz0 state, and only the edited
   entry changes.

**Reset.** `ResetGameFilesStep` copies vanilla main.dol, RELS.arc and bi2.bin, as before. It also
restores every other file any catalogue patch could touch (loose RELs, `bmgres.arc`, `Room1.arc`,
the THP videos), so turning a patch off really undoes it. The client fallback does the same.

**Free space.** `free_space_start_offsets.txt` now starts main.dol free space at `0x803FD200`
(`SCRATCH_REGION_END`), so `@NextFreeSpace` code lands after our scratch block instead of on top of
it. When a selected patch uses free space, `DolPatcher` adds the Text2 section from `0x803FCFA8`,
which covers the scratch block (loaded as zeros), and moves the boot-thread stack above the code,
the way wwrando does. `AssemblePatchesStep` and a test both enforce the start address.

The resulting layout (verified against the vanilla DOL; `puppet_shared.h` documents it too):

| range | what |
|---|---|
| `0x803FCF20..0x803FCFA7` | `.sbss2`, live game constants (not in Text2, never written) |
| `0x803FCFA8..0x803FD1FF` | our scratch block: start of Text2, loaded as zeros |
| `0x803FD200..` code end | free-space code of every assembled patch, in id order; unselected patches' slots are zeros |
| code end (8-aligned) + 0x10000 | boot-thread stack. `_stack_end` (the `0xDEADBABE` magic word) is just past the code, and `_stack_addr` (initial r1) is 0x10000 above it |
| stack top rounded up to 32 | arena start (`_db_stack_end` in `OSInit`). The vanilla fallback `__ArenaLo` 0x8040EFC0 stays above the stack; `DolPatcher` refuses a layout that would pass it |

With the defaults, the code ends at `0x803FD444`, so the stack is `0x803FD448..0x8040D448` and the
arena starts at `0x8040D460` (vanilla: `0x8040CFC0`). With every free-space patch, the stack moves
to `0x803FD5F8..0x8040D5F8`. The patched instructions are `__init_registers` (r1), `__OSThreadInit`
(stack base/end), `OSInit` (`_db_stack_end`) and `InitMetroTRK` (unused). A scan of the vanilla DOL
found no other reference to the old stack bounds.

**Freshness.** `BuildStamp` records the selected ids (`OptionalPatches`). The source hash covers the
required sources, `free_space_start_offsets.txt`, and the selected patches plus their assets. So:
- editing an unselected patch doesn't make the game stale;
- editing a selected one does;
- `Check(config, desired)` reports **Stale / SelectionChanged** when the player's selection differs
  from the build.

The pre-built fallback writes a stamp too (`Origin: "prebuilt"`). Its hash comes from
`PatchData/patchdata-stamp.json`, which records the source hashes of the required sources and of each
optional patch separately, so it equals what the pipeline computes for the same sources and selection.
An installed app (no GameMod) checks its game against that PatchData stamp (`GameBuildCheck`,
`BuildStamp.CheckAgainstPatchData`): an update that changed a selected patch is stale, one that changed
only unselected patches is not. Without a PatchData stamp (a dev build's copy), only the selection is
compared (`CheckSelection`). See `docs/releasing.md` (PatchData).

**Where the selection comes from:**
- Client: `GameSettings.OptionalPatches`. `null` means never chosen, which uses each patch's
  `@default`. Unknown ids are dropped.
- Headless: `--patches a,b | none | default`. `--patch` without it applies the defaults.
  `--check-build` without it doesn't compare the selection.
- dev-test: `.\dev-test.ps1 -Patch -Patches skip_intro,instant_text`. Without `-Patches` it uses the
  defaults.

**Client API** (for the Settings page): `PatchOptionsViewModel`. `GamePatcherService.PatchGameAsync`
applies the saved selection automatically.

## betterww options

Categories: **(a)** main.dol only · **(b)** needs REL edits · **(c)** needs archive, BMG, stage or
other file edits.

| betterww option | id here | what it does | source | cat. | status | match | multiplayer |
|---|---|---|---|---|---|---|---|
| Remove intro video (skipintro part) | `skip_intro` | skips the intro movie | skipintro.asm | a | ported, **default on** (was always on) | no | local |
| Remove title and ending videos | `remove_title_and_ending_videos` | blank title-loop and ending THPs | tweaks.remove_title_and_ending_videos, assets/blank.thp | c | ported | no | local |
| Instant text boxes | `instant_text` | instant draw, no scripted waits | tweaks.make_all_text_instant | c (BMG) | ported | no | local |
| B skips text | `b_button_skips_text` | hold B to fast-forward dialogue | b_button_skips_text.asm | a | ported | no | local |
| No song replays | `song_no_replay` | skip the conducting replay | song_no_replay.asm | a | ported | no | local |
| Faster ballad of gales | `faster_ballad` | no cyclone flight cutscene | ballad.asm (d_a_ship.rel) | b | ported (disables the warp-music `bl` relocation) | no | local |
| Faster rolling / movement speeds | `faster_movement` | 2x crawl, fast roll start | tweaks.increase_player_movement_speeds | a (.data) | ported | no | local |
| Faster grapple | `faster_grapple` | HD-like grappling hook | tweaks.increase_grapple_animation_speed | a (.text + .sdata2) | ported | no | local |
| Faster crawl/climb/NPC chat zoom | `faster_misc_animations` | climb, ladder, sidle, talk zoom | tweaks.increase_misc_animations | a (.data) | ported | no | local |
| Faster block moving | `faster_block_moving` | 1.4x push/pull, 12-frame moves | tweaks.increase_block_moving_animation | a + b (d_a_obj_movebox.rel) + c (sea/Room1.arc dzb) | ported, **with wwrando's FF door softlock fix** (betterww lacks it) | no | local |
| Turn while swinging/grappling | `swing_turn` | steer on ropes | swing_turn.asm + custom_funcs | a (free space) | ported | no | local (puppets face where the network says) |
| Invert camera | `invert_camera_x_axis` | invert C-stick X | invert_camera_x_axis.asm + custom_funcs | a (free space) | ported | no | local |
| Swift sail (+ "for HD texture pack") | `swift_sail` | wind behind you, 2x speed, renamed "Swift Sail" | swift_sail.asm + tweaks + custom_funcs | a + b + c (BMG) | ported **without the sail textures** (= betterww `swift_sail2`) | no | local |
| Brisk sail (+ HD variant) | `brisk_sail` | swift sail with harder braking | brisk_sail.asm + tweaks | a + b + c | ported without textures (= `brisk_sail2`) | no | local |
| Normal sail controls wind | `sail_controls_wind` | wind behind you, normal speed | normal_sail_wind.asm | a + b | ported (betterww calls a missing `tweaks.normal_sail_wind`; this is the intended asm) | no | local |
| Unrestricted boat | `korl_unrestricted` | KoRL doesn't turn you back; board before the DRI intro | KORL_control.asm (d_a_ship.rel) | b | ported | **yes** | changes where you can go before the story allows it |
| Reveal full sea chart | `reveal_sea_chart` | new saves start with the full chart | reveal_sea_chart.asm | a | ported | no | local; new saves only |
| Tingle chests without tuner | `tingle_chests_without_tuner` | bombs reveal Tingle chests; map icons | tingle_chests_without_tuner.asm (+ d_a_agbsw0.rel) | a + b | ported | **yes** | chest behaviour differs per player |
| Random enemy colours | — | random enemy palettes | randomizers/palettes.py | c | **not yet** | — | would be local (cosmetic) |
| Title logo / memory card logo | — | cosmetic branding | tweaks | c | skipped (by request) | — | — |

Swift and Brisk Sail and Sail controls wind all hook `d_a_ship.rel` 0xB9FC, so they declare
`@conflicts`. The UI unticks the others, and the pipeline rejects the combination.

Faithfulness notes:
- At 0x2E1C, betterww and wwrando both comment "read 2.0 from 0xDB24" but actually load 10.0 from
  0xDB08. The shipped code is kept.
- The Swift/Brisk speed comments in betterww also disagree with the constants (0xDB44 is 30.0). The
  values are unchanged.

## betterww always-on fixes (apply_necessary_tweaks / fix_vanilla_bugs)

The crash and softlock fixes that matter in an unrandomized game are **default-on options** in the
"Bug fixes" category, not required patches. They are untested in this mod, and a player can turn one
off if it misbehaves.

| fix | id | cat. | status |
|---|---|---|---|
| Grandma soup crash (message 2041 doesn't exist) | `fix_grandma_soup_crash` | b (loose d_a_npc_ba1.rel) | ported, default on |
| Hookshot sight crash on Helmaroc's mask shards | `fix_hookshot_mask_crash` | a (free space) | ported, default on |
| Beedle buys Blue Chu Jelly (Blue Potion softlock) | `fix_beedle_blue_chu_jelly` | b (loose d_a_npc_bs1.rel) | ported, default on |
| Morths falling out of bounds never die | `fix_morth_out_of_bounds` | b (RELS.arc d_a_ks.rel) | ported, default on |
| Stalfos lower body unkillable after Light Arrows | `fix_stalfos_light_arrows` | a + b (re-targets a `bl`) | ported, default on (wwrando's fix, not necessary_fixes' "Stalfos immune to Light Arrows") |
| Miniblins killed by Light Arrows don't set their switch | `fix_miniblin_light_arrows` | a + b | ported, default on |
| Jalhalla's Poes killed by Light Arrows don't count | `fix_jalhalla_poes_light_arrows` | a + b | ported, default on |
| Respawning Magtails + Light Arrows | `fix_magtail_light_arrows` | a + b | ported, default on |
| Island LoD heap fragmentation on the sea + JKRHeap warnings | `fix_sea_heap_fragmentation` | b (RELS.arc d_a_lod_bg.rel) | ported, default on |
| Keep the Forest Haven → Forbidden Woods void zone | `keep_forest_haven_void` | b (loose d_a_tag_ret.rel) | ported, default **off** (a change, not a fix; `@match yes`) |
| Cannons die in one boomerang hit | `cannons_one_boomerang_hit` | b | ported, default off |
| Rainbow Tingle Statue rupee | `rainbow_rupee` | a (free space + item resource byte) | ported, default off (cosmetic) |
| Orca crashes when challenged with no sword | — | b | not ported: you can't be swordless at Orca in vanilla |
| Phantom Ganon 1 trigger ignores height (FF2) | — | b | not ported: only reachable out of order (randomizer), and it needs REL free space (a new REL section) |
| Auto-equip Deluxe Picto Box | — | a | not ported: only needed when items are randomized |
| Arrow field-model pointers, custom text commands, new-game save init, progressive items | — | a | not ported: randomizer-only (the save init is a no-op with 0 starting shards) |
| Fast treasure chests | — | b | not ported: commented out in betterww |

**Multiplayer impact of the defaults.** The default selection is skip_intro plus the bug fixes. It
now places a few hundred bytes of helper code in main.dol free space. That adds the Text2 section
and moves the boot-thread stack (and the start of the heap arena) up by 0x4A0 bytes, or 0x650 with
every free-space patch (see Free space above). The scratch region stays at its fixed addresses and
is no longer part of any stack. Before the first multiplayer session on a new build, do one test run
(`dev-test.ps1 -Patch`) to confirm puppets still spawn. If it misbehaves, try
`-Patches skip_intro`, which uses no free space and matches the old build.

## Not yet

- **Sail textures** (betterww's non-"HD pack" Swift/Brisk variants). These need:
  - a PNG decoder and a GameCube texture encoder (a port of wwlib/texture_utils.py and bti.py);
  - J3D `TEX1` editing for `Vho.arc/vho.bdl`;
  - replacing `new_ho1.bti` in `Ship.arc` and `sail_00.bti` in `itemicon.arc`.

  The behaviour is ported. The HD texture pack (or no texture change) is what `*_sail2` did anyway.
- **Random enemy colours.** This needs ports of randomizers/palettes.py, wwlib bti/j3d/jpc and
  texture_utils (about 3,000 lines of Python plus an image library). It edits textures, materials and
  particles across dozens of stage and object archives, and needs misc_rando_features' ChuChu float
  shuffle in d_a_cc.rel. It is cosmetic and local only; left for later.
- **New REL relocations / REL free space.** Not supported by design. Patches must hook a call site
  that already calls into main.dol, and put their code in main.dol free space.
