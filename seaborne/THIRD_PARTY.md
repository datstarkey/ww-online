# Third-party code and licensing

Seaborne is derived from two projects:

- **SwitchWaker** ([github.com/centollOS/SwitchWaker](https://github.com/centollOS/SwitchWaker)), MIT,
  "Copyright (c) 2026 Pulpparty and depende3000, and the SwitchWaker contributors": the native port
  (the SDK layer, the run harness, the tools and the Switch platform layer). Its license notice is kept
  in [LICENSE](LICENSE).
- **The zeldaret/tww decompilation** ([github.com/zeldaret/tww](https://github.com/zeldaret/tww)),
  CC0-1.0: almost all of `game/`.

Seaborne contains, builds against, or fetches at build time the components below. Nothing in this
list is game data: the repository contains no asset, executable or data file of the game, and every
build reads them from the player's own disc. Every license below was checked against the license file
of the pinned source on 2026-10-15 ("Verified" column).

## Licensing in short

| Part | License |
|---|---|
| `game/` (the decompilation) | CC0-1.0 ([game/LICENSE](game/LICENSE)), except the few third-party files listed below |
| The port's own code (everything else not listed below), derived from SwitchWaker | MIT ([LICENSE](LICENSE)) |
| `native/src/mods/ww_online/`, `native/include/mods/ww_online.h`, `native/include/pc/tww.h` | MIT, Copyright (c) 2026 datstarkey (SPDX header in each file) |
| `native/dsp_hle/` (audio, adapted from a GPL-3.0-or-later project over Dolphin's DSP HLE) | **GPL-3.0-or-later** ([native/dsp_hle/LICENSE](native/dsp_hle/LICENSE)) |
| A built `libtww` / `seaborne` executable, as a whole | **GPL-3.0-or-later** (see "Binaries") |

## In this repository

| Path | Origin | License |
|---|---|---|
| `game/` | The game's decompilation, imported from a fork ([github.com/snrubrm/tww](https://github.com/snrubrm/tww)) at `b09eebc` of the community decompilation project ([github.com/zeldaret/tww](https://github.com/zeldaret/tww)), changed by later commits of this repository under `TARGET_PC` (see `game/README.md`) | CC0-1.0 (`game/LICENSE`) |
| `native/include/helpers/{endian.h,endian_gx.hpp,endian_ssystem.h,offset_ptr.h}`, `native/src/helpers/offset_ptr.cpp` | [Dusklight](https://github.com/TwilitRealm/dusklight) (TwilitRealm) at `40457c6`; the SDK-over-Aurora layer follows Dusklight's model | CC0-1.0 |
| `native/dsp_hle/` | Adapted from elliotttate's recompilation project ([github.com/elliotttate/Wind-Waker-Recomp](https://github.com/elliotttate/Wind-Waker-Recomp)) (`runtime/host/src/dsp_hle_backend.cpp`, `apple/ios/src/dsp_common_shim.cpp`); compiles Dolphin's DSPHLE (below) | GPL-3.0-or-later (`native/dsp_hle/LICENSE`, SPDX headers in each file): the recompilation project is GPL-3.0-or-later (its `LICENSE` and `RIGHTS_AND_LICENSES.md`), and `dsp_common_shim.cpp` is copied from it unchanged |
| `native/patches/aurora/`, `switch/native/aurora/patches/` | Patches to Aurora | same as Aurora (MIT) |
| `switch/dawn/patches/` | Patches to Dawn and Tint (Horizon) | same as Dawn (BSD-3-Clause) |
| `switch/uam/` | [uam](https://github.com/devkitPro/uam) 1.1.0 at `5a5afc2` (fincs, devkitPro), the deko3d shader compiler, as vendored by [SwitchWakerHD](https://github.com/centollOS/SwitchWakerHD) (`runtime/third_party/uam/` at `df8fbde`) with its patches 1-7, CMake build and `uam_api`, plus Seaborne's patch 8 (`switch/uam/PATCHES.md`) | zlib for uam's own files, MIT for `mesa-imported/` (from Mesa) (`switch/uam/LICENSE`); SwitchWakerHD lists its additions there under the same terms (SwitchWakerHD itself is MPL-2.0) |
| `native/tools/dksh_cache/dksh_cache.cpp`, `switch/deko/dksh_file.{h,cpp}`, `switch/deko/{dk.h,device.cpp,memory.cpp,descriptors.cpp,overlay.cpp,pattern.cpp}`, `switch/deko/shaders/{imgui,text}_{vsh,fsh}.glsl` | Adapted from [SwitchWakerHD](https://github.com/centollOS/SwitchWakerHD) at `df8fbde` (`tools/switch/dksh_cache/dksh_cache.cpp`, `runtime/src/gfx/deko/{shader_files.{h,cpp},dk.h,backend.cpp,memory.cpp,descriptors.cpp,overlay_dk.cpp}`, `runtime/src/gfx/deko/shaders/`) | MPL-2.0 (header in each file; [mozilla.org/MPL/2.0](https://mozilla.org/MPL/2.0/)) |
| `native/tools/dksh_cache/include/tracy/Tracy.hpp` | A header of [Tracy](https://github.com/wolfpld/tracy) (Switch tool) | BSD-3-Clause |
| `switch/native/icon/icon.jpg` | Original art, drawn by `scripts/switch/make_icon.py` | MIT (the port's own) |
| `switch/forwarder/nx-hbloader-forwarder.patch` | Patch to nx-hbloader | same as nx-hbloader (ISC) |

The quality-of-life options (`docs/QOL_PLAN.md`, `native/src/pc/features/pc_qol.cpp`, the options
menu's "Calidad de vida" tab in `native/src/pc/features/pc_menu.cpp` and the hunks in `game/` the plan
lists) are derived from the patches and tweaks of [BetterWW](https://github.com/WideBoner/betterww)
(WideBoner, at `4501481`) and [wwrando](https://github.com/LagoLunatic/wwrando) (LagoLunatic, at
`9775811`). Their assembly patches cannot run here (the game is compiled from its decompilation), so
each patch was rewritten in the decompiled function it changes, reproducing its behaviour and its
constants (speeds, frame counts, message numbers, event bits) closely. No asset of theirs (textures,
models, icons) is in this repository. BetterWW credits LagoLunatic (BetterWW modifies his code),
brainfubar (his wwrando fork was used to make BetterWW), MelonSpeedruns (the original design of the
Brisk Sail) and JarheadHME (releases); wwrando credits MelonSpeedruns (game and graphic design),
Hypatia (textures), SageOfMirrors and LordNed (file format documentation) and CryZe (event flag
documentation). Both projects are under this license:

```
The MIT License (MIT)

Copyright (c) 2018 LagoLunatic

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Design credit, no code taken: the 60/120 fps presentation (`docs/FPS60_PLAN.md`) follows the approach of
[WindowWaker](https://github.com/MaskeZenY/WindowWaker) (MaskeZenY, MIT), a native port of the same game.

Everything else is the port's own code under the MIT license ([LICENSE](LICENSE)): the SwitchWaker
code (copyright Pulpparty and depende3000, and the SwitchWaker contributors) and Seaborne's changes.

### Third-party notices inside `game/`

`game/` is CC0-1.0, but a few files the decompilation reproduces from the original SDK's libraries
keep their own notices: the double-precision math functions in
`game/src/PowerPC_EABI_Support/MSL/MSL_C/MSL_Common_Embedded/Math/` (fdlibm, Sun Microsystems'
permissive notice; `e_pow.c`, `k_tan.c` and two others under eCos's GPL-2.0-or-later with the eCos
linking exception). The native build compiles none of them (the host's libm is used); they come from
zeldaret/tww as they are.

## Fetched at build time (not in the repository)

| Component | Pin | Used for | License | Verified |
|---|---|---|---|---|
| [Aurora](https://github.com/encounter/aurora) | `3227d76` | GameCube SDK over WebGPU (GX, VI, PAD, DVD, CARD, THP) | MIT | `LICENSE` of the fetched source |
| [Dawn](https://dawn.googlesource.com/dawn) | Mac and Linux: Aurora's prebuilt package; Switch: [encounter/dawn](https://github.com/encounter/dawn) `266c1cf` | WebGPU implementation | BSD-3-Clause (its `LICENSE` lists Apache-2.0 for a few non-library directories) | encounter/dawn `LICENSE` (the prebuilt package carries no license file) |
| [abseil-cpp](https://github.com/abseil/abseil-cpp) | 20240722.0 (Aurora's pin) | Dawn dependency, linked statically | Apache-2.0 | `LICENSE` of the fetched source |
| [SDL 3](https://github.com/libsdl-org/SDL) | Aurora's pin, or the system's (Homebrew 3.4); 3.4.10 headers (Switch, with this repository's libnx shim) | window, input, audio | Zlib | `LICENSE.txt` |
| [Dear ImGui](https://github.com/ocornut/imgui) | v1.91.9b-docking (Aurora's pin) | menus and overlays | MIT | `LICENSE.txt` of the fetched source |
| [fmt](https://github.com/fmtlib/fmt) | 12.1.0 (Aurora's pin, or the system's) | formatting | MIT | `LICENSE` |
| [xxHash](https://github.com/Cyan4973/xxHash) | v0.8.3 (Aurora's pin, or the system's) | hashing | BSD-2-Clause (the library) | `LICENSE` |
| [zlib](https://zlib.net) / [zlib-ng](https://github.com/zlib-ng/zlib-ng) | the system's, else zlib-ng 2.3.3 (Aurora's pin) | compression | Zlib | — (standard) |
| [libpng](https://github.com/pnggroup/libpng) | v1.6.58 (Aurora's pin, or the system's) | PNG screenshots and textures | PNG Reference Library License v2 | `LICENSE` |
| [FreeType](https://freetype.org) | 2.14.3 (Aurora's pin, or the system's) | fonts | FreeType License (FTL) or GPL-2.0-or-later, at the user's choice | `LICENSE.TXT` |
| [zstd](https://github.com/facebook/zstd) | v1.5.7 (Aurora's pin, or the system's) | compression | BSD-3-Clause OR GPL-2.0 | `LICENSE`, `COPYING` |
| [Tracy](https://github.com/wolfpld/tracy) | v0.14.1 (Aurora's pin) | profiler hooks (off) | BSD-3-Clause (its libbacktrace: BSD-3-Clause-style) | `LICENSE` of the fetched source |
| [SQLite](https://sqlite.org) | 3.51.3 amalgamation (Aurora's pin), or the system's | pipeline and texture caches | Public domain | — (sqlite.org) |
| [nod](https://github.com/encounter/nod) | Aurora's prebuilt package (Mac and Linux; the Switch uses `switch/native/nod`) | disc reader | MIT OR Apache-2.0 | encounter/nod `LICENSE-MIT`, `LICENSE-APACHE`, README (the prebuilt package carries no license file) |
| [RecompCore](https://github.com/elliotttate/RecompCore) (Dolphin) | `8ab24da` | Dolphin's DSP HLE sources (`native/tools/fetch_recompcore.sh`); the build reads 97 of its files (`Source/Core/Core/HW/DSPHLE`, `Core/DSP/DSPAccelerator.cpp` and the headers they include) | GPL-2.0-or-later (81 files) and CC0-1.0 (2), by their SPDX headers; the repository as a whole: "compatible with the GPLv3" (`COPYING`) | SPDX header of every file the build reads, `COPYING`, `LICENSES/` |
| [deko3d](https://github.com/devkitPro/deko3d) | 0.5.0-1 in the pinned devkitPro image (`libdeko3d.a`, `libdeko3dd.a`; uam 1.1.0-1 compiles the deko3d NRO's own GLSL) | the deko3d NRO's GPU API (`switch/deko`) | Zlib | not re-checked (Switch only) |
| [libnx](https://github.com/switchbrew/libnx), [devkitA64](https://devkitpro.org) | pinned devkitPro image digest | Switch runtime and toolchain | ISC (libnx); toolchain runtime libraries under their own licenses | not re-checked (Switch only) |
| [nx-hbloader](https://github.com/switchbrew/nx-hbloader) | v2.4.5 (`82b9512`) | HOME-menu forwarder (optional) | ISC | not re-checked (Switch only) |
| [hacBrewPack](https://github.com/TooTallNate/hacBrewPack) | v3.05 (`745b16e`) | packs the forwarder NSP (build tool only, not shipped) | GPL-2.0 | not re-checked (Switch only) |
| [hactool](https://github.com/SciresM/hactool) | in the devkitPro image | verifies the forwarder NSP (build tool only) | ISC | not re-checked (Switch only) |
| [bc7enc_rdo](https://github.com/richgel999/bc7enc_rdo) | `b943862` | HD texture pack converter (`native/tools/hd_pack`) | MIT or Unlicense (bc7e.ispc: Apache-2.0, not built) | `LICENSE` (alternatives A and B) |
| [decomp-toolkit](https://github.com/encounter/decomp-toolkit) (`dtk`) | the decomp's pin | asset header generation from the player's disc (`native/tools/gen_assets.sh`); run, not linked | MIT OR Apache-2.0 | encounter/decomp-toolkit `LICENSE-MIT`, `LICENSE-APACHE` |

The Switch-only components were not part of this check: they are in `switch/` and `scripts/switch/`,
which the desktop build does not read.

## Binaries

Every build links `native/dsp_hle` (the game's audio needs a DSP; there is no build without it yet),
which is GPL-3.0-or-later and compiles Dolphin's GPL-2.0-or-later DSP HLE from RecompCore. A built
`libtww` or `seaborne` executable is therefore a combined work under **GPL-3.0-or-later** as a whole.
(GPL-2.0-or-later would not do in any case: the build also links Apache-2.0 code, abseil, which is
compatible with GPLv3 but not with GPLv2.)

Seaborne is distributed **as source only**. Players build `libtww` on their own machine, from their
own disc; the binary is never distributed, so the GPL's obligations for distributed binaries (the
corresponding source, the license text) never arise. Anyone who does distribute a binary must do so
under GPL-3.0-or-later with its complete corresponding source; it would also contain code generated
from the disc, which must never be shared.

### Compatibility of the parts under GPL-3.0-or-later

Every license in this file may be combined into a GPL-3.0-or-later work:

- **CC0-1.0, public domain (SQLite), MIT, ISC, zlib, BSD-2-Clause, BSD-3-Clause, the libpng license,
  the FreeType License**: permissive, GPL-compatible.
- **Apache-2.0** (abseil; nod and decomp-toolkit are MIT OR Apache-2.0):
  compatible with GPLv3 (not with GPLv2-only).
- **GPL-2.0-or-later** (Dolphin via RecompCore): "or later" allows GPLv3.
- **MPL-2.0** (the SwitchWakerHD-derived files): compatible through MPL-2.0 section 3.3 (Secondary
  Licenses). No file in this repository carries the "Incompatible With Secondary Licenses" notice
  (checked with `git grep`). These files are all Switch-only; the desktop `libtww` compiles none of them.
- **zstd** (BSD-3-Clause OR GPL-2.0) and **FreeType** (FTL OR GPL-2.0-or-later): dual-licensed; the
  permissive option applies.
- **GPL-2.0 (only)**: hacBrewPack, a Switch build tool that is run, never linked or shipped. Not part
  of any combined work.

No incompatibility was found.
