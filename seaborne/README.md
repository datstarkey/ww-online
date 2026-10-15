# Seaborne

## What it is

Seaborne is an unofficial native port of a 2002 GameCube adventure game (disc ID `GZLE01`, USA,
revision 0, the reference; and `GZLP01`, Europe, revision 0, with its five languages) to the Mac,
Linux and the Switch (homebrew). The game is compiled from its decompilation
(the [`game/`](game/README.md) folder) and drawn with [Aurora](https://github.com/encounter/aurora);
there is no CPU emulation.

**Seaborne is based on SwitchWaker by Pulpparty and depende3000**
([github.com/centollOS/SwitchWaker](https://github.com/centollOS/SwitchWaker), MIT, with the
SwitchWaker contributors): the native port it continues, renamed for WW-Online. The game code is the
**[zeldaret/tww](https://github.com/zeldaret/tww)** decompilation (CC0-1.0), in `game/`. Everyone
SwitchWaker credits is credited below, in "Standing on the shoulders of others". It is the game engine of the WW-Online multiplayer client, and also builds and runs on its own.

**You must own the game.** Seaborne contains no game content; it reads everything from your own,
legally obtained disc image. Not affiliated with or endorsed by Nintendo (see "Disclaimer").

## Standing on the shoulders of others

Seaborne would not exist without these projects. The hard part, understanding the game, is
their work; this repository adds the layer that runs it on new hardware.

- **[SwitchWaker](https://github.com/centollOS/SwitchWaker)** (Pulpparty, depende3000 and the
  SwitchWaker contributors): the native port Seaborne is derived from: the SDK layer over Aurora, the
  run harness, the tools and the Switch platform layer.
- **[zeldaret/tww](https://github.com/zeldaret/tww)**: the community decompilation of the game.
  The careful, byte-for-byte matching work of its contributors produced almost all of the code in
  [`game/`](game/README.md). Without it there is nothing to port.
- **[snrubrm/tww](https://github.com/snrubrm/tww)**: a personal fork of zeldaret/tww that completes
  the functions still missing, with AI-assisted decompilation (in its author's words). `game/` was
  imported from it; units that zeldaret has since matched are taken from zeldaret again.
- **[encounter/aurora](https://github.com/encounter/aurora)**: the GameCube SDK reimplemented over
  WebGPU (Dawn), which draws the game and reads the controllers on every platform, the Switch
  included (with encounter's Dawn fork for it).
- **[Dusklight](https://github.com/TwilitRealm/dusklight)** (TwilitRealm): the native port of a
  sibling game on Aurora; the model for this project's SDK layer, and CC0 helper code.
- **[BetterWW](https://github.com/WideBoner/betterww)** (WideBoner) and
  **[wwrando](https://github.com/LagoLunatic/wwrando)** (LagoLunatic; with brainfubar, MelonSpeedruns
  and JarheadHME): the options menu's "Calidad de vida" (quality of life) options are derived from
  their patches ([docs/QOL_PLAN.md](docs/QOL_PLAN.md)).
- **[SwitchWakerHD](https://github.com/centollOS/SwitchWakerHD)** (centollOS): the HD remaster's
  recompilation for the Switch with a deko3d renderer; this port's deko3d renderer files, its uam
  shader-compiler build, the DKSH shader cache tool and the Switch debug server client are adapted
  from it (MPL-2.0, see [THIRD_PARTY.md](THIRD_PARTY.md)).
- **[WindowWaker](https://github.com/MaskeZenY/WindowWaker)** (MaskeZenY): a native port of the same
  game; the 60/120 fps presentation follows its approach ([docs/FPS60_PLAN.md](docs/FPS60_PLAN.md)).
- **[Dolphin](https://dolphin-emu.org)**: its DSP HLE plays the game's audio (`native/dsp_hle/`, GPL).
- **[elliotttate/Wind-Waker-Recomp](https://github.com/elliotttate/Wind-Waker-Recomp)** and
  **[RecompCore](https://github.com/elliotttate/RecompCore)**: the static recompilation this work
  started from, and the DSP adapter.
- **[devkitPro](https://devkitpro.org)**, **[libnx](https://github.com/switchbrew/libnx)** and
  **[deko3d](https://github.com/devkitPro/deko3d)** with uam (whose shader compiler comes from
  **[Mesa](https://mesa3d.org)**): the Switch toolchain and its GPU API.
- **[nx-hbloader](https://github.com/switchbrew/nx-hbloader)**,
  **[hacBrewPack](https://github.com/TooTallNate/hacBrewPack)** and
  **[hactool](https://github.com/SciresM/hactool)**: the HOME-menu forwarder.

If you enjoy this port, the decompilation projects above are where the credit belongs. Every
component and its license: [THIRD_PARTY.md](THIRD_PARTY.md).

## How it was made: AI use

Be aware of this before you use or build on this repository:

- **This project's own code was written with AI.** Nearly all of the code outside `game/` (the SDK
  layer, the run harness, the Switch platform layer, patches, tools and scripts), the documentation
  and the commit messages were written by an AI coding agent, Anthropic's Claude through Claude
  Code. The human authors chose what to build, set the priorities, reviewed the code line by line,
  played and tested it on the Mac and on a real Switch, reported the bugs and decided what went in.
  Commits written with the agent say so in a `Co-Authored-By: Claude` line.
- **Part of the decompilation is AI-assisted too**: the functions snrubrm/tww completed (see
  above). The rest of `game/` is zeldaret's human work. If you want only human-written
  decompilation, use [zeldaret/tww](https://github.com/zeldaret/tww).
- **How it is checked**: human review of the code, line by line, plus builds on every platform, a
  regression suite of boot milestones and smoke tests, automated sweeps over every stage, room,
  actor, event, item and boss (also under AddressSanitizer), and play on hardware
  ([native/README.md](native/README.md)). Bugs can still slip through; reports are welcome.

## What you need

- **Your own legally obtained copy of the disc**: `GZLE01` (USA) or `GZLP01` (Europe) revision 0, as
  an uncompressed `.iso`. The build generates what it needs from it. The version is fixed at build
  time: the European disc needs the European build (below).
- **This repository contains no game assets, executables or data.** The binaries you build contain
  code generated from your disc: they are for your own use only; do not share them.
- **No release binaries are provided** for now, precisely because they carry headers generated
  from the disc compiled in.

## Build on the Mac (Apple silicon)

Requirements: Xcode command line tools, `brew install cmake ninja python` (Python 3.10 or newer)
and the network the first time.

```sh
cd seaborne                                  # the root of the Seaborne source
export COS_DISC=/path/to/GZLE01.iso          # every script and the game read the disc from here
native/tools/fetch_recompcore.sh             # ref/recompcore: Dolphin's DSP HLE (audio)
cmake -S native -B build/native-mac -G Ninja
ninja -C build/native-mac seaborne          # build/native-mac/seaborne
build/native-mac/seaborne                   # a 960x720 window
```

Details, run options and tests: [native/README.md](native/README.md). Full regression:
`native/tools/regress.sh`.

**The European disc** (`GZLP01`) is a separate build directory with `-DCOS_VERSION=3`
([docs/PAL_DISC_PLAN.md](docs/PAL_DISC_PLAN.md)); its language follows the system's (English, German,
French, Spanish or Italian), `COS_GAME_LANGUAGE=en|de|fr|es|it` or the options menu fixes one:

```sh
export COS_DISC=/path/to/GZLP01.iso
cmake -S native -B build/native-mac-eur -G Ninja -DCOS_VERSION=3
ninja -C build/native-mac-eur seaborne
COS_BUILD_DIR=build/native-mac-eur native/tools/regress.sh   # the tools take the build directory
```

## Build on Linux (x86_64 or aarch64)

Requirements: clang (GCC is untested), CMake 3.28 or newer, Ninja, Python 3.10 or newer, git, and the
development headers SDL 3 needs for its video and audio backends; at run time a Vulkan driver
(any GPU's, or Mesa's). On Debian 13 / Ubuntu 24.04:

```sh
sudo apt install clang lld cmake ninja-build pkg-config python3 git zlib1g-dev \
    libx11-dev libxext-dev libxrandr-dev libxcursor-dev libxfixes-dev libxi-dev libxss-dev \
    libxtst-dev libxkbcommon-dev libwayland-dev wayland-protocols libdecor-0-dev \
    libegl-dev libgl-dev libgles-dev libvulkan-dev libdrm-dev libgbm-dev \
    libasound2-dev libpulse-dev libpipewire-0.3-dev libudev-dev libdbus-1-dev libibus-1.0-dev \
    mesa-vulkan-drivers
```

Then, as on the Mac but in `build/native-linux`:

```sh
cd seaborne                                  # the root of the Seaborne source
export COS_DISC=/path/to/GZLE01.iso
native/tools/fetch_recompcore.sh
CC=clang CXX=clang++ cmake -S native -B build/native-linux -G Ninja
ninja -C build/native-linux seaborne        # build/native-linux/seaborne
build/native-linux/seaborne
```

Aurora draws with Vulkan, then OpenGL ES, then nothing (null) if neither works; `COS_BACKEND=opengl`
(or `vulkan`, `opengles`, `null`) asks for one first. Dawn and nod come as Aurora's prebuilt
packages, SDL 3 is built from source. The regression runs with
`COS_BUILD_DIR=build/native-linux native/tools/regress.sh` (the link census and the duplicate
symbol census are Mac-only and skipped). Headless (no display, no GPU, e.g. in a container):
`COS_ALLOW_CPU_ADAPTER=1 xvfb-run -a native/tools/run.sh title` uses Mesa's software Vulkan.

Fullscreen (Linux and the Mac): `COS_FULLSCREEN=1` starts fullscreen, and **F11** or **Alt+Enter**
switches between fullscreen and the window while the game runs.

## Build with Docker (any OS)

`scripts/docker/build.sh` builds the Linux binary and/or the Switch NRO entirely in containers, on
Linux, macOS, or Windows (in a WSL 2 shell, with Docker Desktop's WSL integration or Podman). The
host needs only bash, git and Docker or Podman; the disc is bind-mounted read-only and never
copied into an image.

```sh
scripts/docker/build.sh linux  --disc /path/to/GZLE01.iso   # build/native-linux/seaborne
scripts/docker/build.sh switch --disc /path/to/GZLE01.iso   # build/switch-native/seaborne.nro
scripts/docker/build.sh switch --disc /path/to/GZLP01.iso   # build/switch-native-eur/seaborne-eur.nro
scripts/docker/build.sh all    --disc /path/to/GZLE01.iso --test   # both, plus headless checks
```

The Linux binary is built for the container's architecture, which is the host's (x86_64 on a PC,
aarch64 on Apple silicon); `--platform linux/amd64` builds the other one under emulation. The disc
decides the version (GZLP01: `build/native-linux-eur`, `build/switch-native-eur`). The asset
headers are generated once into `build/assets/GZLE01` (`build/assets/GZLP01`). Downloads stay between runs in the
repository's ignored `build/` and `ref/` folders (Aurora and its packages in
`build/native-linux/_deps`, the decompilation and its tools in `build/decomp-docker`, RecompCore in
`ref/recompcore`, Dawn's sources in `build/switch-native/_deps`)
and in the engine's image cache; the first Switch build takes an hour or more, later ones minutes.
`scripts/docker/build.sh --help` lists every option. On Windows, keep the clone inside the WSL file
system (for example `~/seaborne`), not under `/mnt/c`: builds there are much slower.

## Windows (status)

There is no native Windows build yet; it is low priority. Windows users can build the Linux binary
(and run it under WSLg) or the Switch NRO with Docker, as above. What a native port would need, and
an estimate: [docs/WINDOWS.md](docs/WINDOWS.md).

## Continuous integration

`.github/workflows/ci.yml` checks what can be checked without a disc: scripts lint, Aurora, the SDK
layer and the run harness built and unit-tested on Linux x86_64 and aarch64, and the Switch build
images cached. It runs only when started by hand (Actions tab, "Run workflow"), to keep the
private repository's Actions minutes. The game itself builds without the disc in the runtime-assets
mode ([docs/RUNTIME_ASSETS.md](docs/RUNTIME_ASSETS.md): the stub headers committed in `native/assets/`;
the data arrays are read from the player's disc at start-up), the default build, which the CI job also
builds.

## Build for the Switch

**Players do not need to build**: the release downloads hold the NRO (built without the disc) and its
caches, one download per disc: `Seaborne-<version>-switch.zip` (USA, `seaborne.nro`) and
`Seaborne-<version>-switch-eur.zip` (Europe), both installing `switch/seaborne/seaborne.nro`
(one version per SD card); players add their own disc ([INSTALL.md](INSTALL.md)). To build it yourself,
`scripts/switch/make_sd.sh --disc /path/to/GZLE01.iso` checks the disc, builds the NRO in containers and
lays out `build/sd/switch/` (NRO, caches, a copy of the disc) for the SD card.

On top of the above: Docker Desktop or Podman. `scripts/switch/build_native.sh` produces
`build/switch-native/seaborne.nro` (the deko3d renderer; Dawn, Aurora and the shader cache are built in
a devkitPro container; `--runtime-assets` builds it without the disc; `--eur` builds
`build/switch-native-eur/seaborne-eur.nro`, the European version).
Full guide: [docs/SWITCH_BUILD.md](docs/SWITCH_BUILD.md).

### Debug server (developers)

For development there is a debug server: from the computer, over the local network, you can
deploy a new build and restart the game without touching the console, follow the log as it is
written, take screenshots, press buttons and warp. It is **off by default**: players never need it
and the game opens no network port unless it is turned on in the options menu (Depuración >
"Servidor de depuración", at the next start). It has no password, so use it only on your own network.

```sh
echo <console ip> > build/switch_host.txt                                  # once; the log prints the address
scripts/switch/build_native.sh && scripts/switch/seaborne_debug.py deploy # upload, check, restart
scripts/switch/seaborne_debug.py log                                      # the log, live
scripts/switch/seaborne_debug.py shot                                     # PNG of the next frame
scripts/switch/seaborne_debug.py press A                                  # also hold, release, stick, warp
```

`deploy` restarts the game, so start it from the HOME-screen icon (forwarder). `deploy --eur` uploads the
European NRO as `seaborne.nro` too; `deploy` refuses to replace the running game with the other region's
build unless `--force`. Every command and the
protocol: [docs/DEBUG_SERVER.md](docs/DEBUG_SERVER.md). It is the same server as SwitchWakerHD's.

## Install on the Switch

### What you need

- A Switch running custom firmware (Atmosphère) with the Homebrew Menu.
- The release download for your disc (`Seaborne-<version>-switch.zip` for USA: `seaborne.nro`,
  `initial_pipeline_cache.db`, `initial_dksh_cache.bin`; `Seaborne-<version>-switch-eur.zip` for
  Europe: the European `seaborne.nro` and the same two caches), or the same files from your own build
  (`build/switch-native/`, or `build/switch-native-eur/` whose `seaborne-eur.nro` goes on the card as
  `seaborne.nro`).
- Your disc as an **uncompressed** `.iso` (not RVZ, GCZ, CISO or NKit), renamed to `GZLE01.iso` (USA) or
  `GZLP01.iso` (Europe).
- About 1.6 GB free on the SD card (the disc is 1.4 GB; the shader caches grow to a few dozen MB).

### Copy the files

The SD card must end up like this (folder and file names exactly as shown):

```
sdmc:/switch/seaborne/
├── seaborne.nro                the game, the USA or the European version (one per card)
├── GZLE01.iso                     your disc (GZLP01.iso: the European one)
├── initial_pipeline_cache.db      the list of graphics pipelines the game uses
├── initial_dksh_cache.bin         their shaders, compiled for the Switch's GPU
└── native/                        created by the game: logs, saves, settings, caches
```

Either way works:

- **By hand**: put the SD card in the computer (or mount it over USB with hekate's
  *Tools → USB Tools → SD Card* or similar) and copy the four files into `switch/seaborne/`.
- **Over USB (MTP)**, from the repository: `scripts/switch/push.sh --disc /path/to/GZLE01.iso`
  once (named after the disc's ID), then `scripts/switch/push.sh native` (the NRO and its caches;
  `native-eur` for the European build, pushed as `seaborne.nro`) after every build.
  Every file is read back and checked. Needs `brew install libmtp` and USB file transfer enabled on
  the console (DBI, haze or similar). Details: [docs/SWITCH_BUILD.md](docs/SWITCH_BUILD.md).

Without `initial_dksh_cache.bin` the game still runs, but every shader is then compiled on the console
the first time it is needed (its draws skipped meanwhile), and the first hours show missing effects.

### Start the game

- Open the Homebrew Menu **in title mode**: hold **R** while starting any installed game, then pick
  **Seaborne** (**Seaborne EUR**: the European version). Opened from the album (applet mode)
  the game has far too little memory and will not run properly.
- Or install the HOME-screen icon (forwarder), which always starts it in title mode:
  build it with `scripts/switch/build_forwarder.sh` and install `seaborne_forwarder.nsp` with
  DBI ([switch/forwarder/INSTALL.md](switch/forwarder/INSTALL.md)). It starts `seaborne.nro`, whichever
  version it is.

### The first start: shaders

The Switch renderer is deko3d (since phase 6 of [docs/DEKO3D_MIGRATION_PLAN.md](docs/DEKO3D_MIGRATION_PLAN.md)):
the compiled shaders of every bundled pipeline come next to the NRO in `initial_dksh_cache.bin`, so
**there is no shader preparation**: the game starts in seconds, the first time too. A shader the file
lacks (a new effect, a mod) is compiled once in the background and kept in
`native/user/cache/dksh_local.bin`; its draw is skipped for a moment meanwhile.

The previous renderer, OpenGL ES on Mesa (`seaborne_gl.nro`, "Seaborne (GL)"), was removed on
2026-10-09; [INSTALL.md](INSTALL.md), "Updating", lists the files it left on the SD card.

### Saves, settings and updates

- Saves (the memory card): `sdmc:/switch/seaborne/native/user/USA/Card A`. Back up this folder.
- Settings from the options menu: `native/user/settings.ini`.
- To update, replace `seaborne.nro`, `initial_pipeline_cache.db` and `initial_dksh_cache.bin`. Saves,
  settings and caches stay.
- Logs, if something goes wrong: `native/logs/`, one file per session named by its date and time
  (`seaborne_<date>_<time>.log`); the newest is this run. Only the 10 most recent sessions are kept.
- If you used this port's earlier SD folder (`sdmc:/switch/centollos/`), rename it to
  `seaborne` and rename `centollos.nro` to `seaborne.nro`; the old path is no longer read.

## Options

- In-game options menu: **Minus (−)** on the Switch, **F1** (or L+R+Z) on the Mac and Linux. Saved to
  `native/user/settings.ini`.
- Language: the options menu and the shader loading screen are in Spanish or English, following the
  console's language (on the Mac and Linux, `LANG`); `COS_LANG=es` or `COS_LANG=en` forces one.
- `COS_*` variables (environment on the Mac and Linux; on the Switch the options menu, and for
  developer variables the `[dev]` section of `native/user/settings.ini`):
  [switch/native/settings-dev.example.ini](switch/native/settings-dev.example.ini) and
  [native/README.md](native/README.md).
- HD textures (optional, a Dolphin-format pack you supply): [docs/HD_TEXTURES.md](docs/HD_TEXTURES.md).
- 16:9 widescreen: [docs/WIDESCREEN.md](docs/WIDESCREEN.md).

## Status

Boots and plays on the Mac and the Switch, with sound, controllers, memory-card saves and an
options menu. Played by hand mostly on the opening island and a few other areas; automated sweeps
run on the Mac over every stage and room, every actor, particle effect, model and screen, every event
of every stage, every item through its item-get demo, saves loaded back, every enemy fought and
every boss room, also under AddressSanitizer ([native/README.md](native/README.md), "Sweeps"). The Switch
does not yet hold a stable 30 fps everywhere. The Linux build boots to the same milestones and
passes the regression in a container with software Vulkan (aarch64); it has not been played on a
Linux desktop with a GPU yet.
Known issues: [docs/NATIVE_PORT_PLAN.md](docs/NATIVE_PORT_PLAN.md), "Known bugs".

## License

- `game/` (the decompilation, from zeldaret/tww): **CC0-1.0** ([game/LICENSE](game/LICENSE)).
- The port's own code, derived from SwitchWaker: **MIT** ([LICENSE](LICENSE), which keeps
  SwitchWaker's notice). The multiplayer mod (`native/src/mods/ww_online/`) and the embedding API
  (`native/include/pc/tww.h`): MIT, Copyright (c) 2026 datstarkey.
- `native/dsp_hle/` (audio, adapted from a recompilation project over Dolphin's DSP HLE):
  **GPL-3.0-or-later** ([native/dsp_hle/LICENSE](native/dsp_hle/LICENSE)).
- Every build links `native/dsp_hle`, so **a built `libtww` or `seaborne` is GPL-3.0-or-later as a
  whole**. Seaborne is distributed as source only: players build it on their own machine, and the
  binaries are never distributed (they also contain code generated from the player's disc).
- Every component, its license and why they combine: [THIRD_PARTY.md](THIRD_PARTY.md).
  Contributing: [CONTRIBUTING.md](CONTRIBUTING.md).

## Disclaimer

Seaborne is an independent fan project, **not affiliated with or endorsed by Nintendo**. *The
Legend of Zelda*, *The Wind Waker*, GameCube and Nintendo Switch are trademarks of Nintendo; they are
named only to say what the port runs and where. **You must own the game**: Seaborne needs your own,
legally obtained disc image, and it includes and distributes no game content (no disc image,
assets, textures, audio or executables of the game).

---

## Authors

Seaborne: datstarkey. Derived from SwitchWaker by Pulpparty, depende3000, and the SwitchWaker
contributors. Both were written with an AI coding agent (see "How it was made: AI use"). Thanks to everyone behind the projects in "Standing on the shoulders of others".
