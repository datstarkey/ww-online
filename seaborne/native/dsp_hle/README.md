# native/dsp_hle: Dolphin's DSP HLE (isolated, GPL-3.0-or-later)

This folder is the only part of Seaborne under the GNU General Public License: its files are
GPL-3.0-or-later ([LICENSE](LICENSE), SPDX headers in each file). They adapt the DSP HLE backend
of an earlier recompilation project by elliotttate, Wind-Waker-Recomp, which is GPL-3.0-or-later
(see [THIRD_PARTY.md](../../THIRD_PARTY.md)); `dsp_common_shim.cpp` is copied from it unchanged. They
compile Dolphin's DSP HLE sources from RecompCore (`native/cmake/dsp_hle.cmake`,
`native/tools/fetch_recompcore.sh`), which are GPL-2.0-or-later.

**Binaries built with it are GPL-3.0-or-later.** The build links this component (today every build
does: there is no build option without it yet, since the game's audio code needs a DSP) into
`libtww`, the executable (`build/native-mac/seaborne`) and the Switch NRO (`seaborne.nro`). Seaborne
is distributed as source only and players build these binaries for themselves; whoever does
distribute one must do so under GPL-3.0-or-later terms, with its corresponding source. The rest of
the repository stays under its own licenses (MIT for the port's code, CC0-1.0 for `game/`); the GPL
applies to this folder and to the combined binaries.

The goal is to replace it with a DSP implementation of the project's own, after which no build
needs it.
