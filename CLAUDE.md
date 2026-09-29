# CLAUDE.md

WW-Online: online multiplayer for The Legend of Zelda: The Wind Waker (GameCube, in Dolphin).
Other players appear as puppet Links, and a room can share world, items, story and wallet.
It has two halves: C# apps (client, relay server, patcher) and the game-side mod (`GameMod/`, C + ASM injected into the game).

## Key rules
- **Never compile or patch the game unless the user asks.** That means `scripts\dev-test.ps1 -Patch`, `--patch`, the UI's Patch Game button and `PipelineRunner.RunAsync`. Say when the C code is ready, then wait. Building and testing the C# code is fine.
- **When debugging a test session, read `logs/latest/` first** (see below), before theorising.
- **`GameMod/src/puppet_link/puppet_shared.h` is the only place emulator scratch/sync addresses live.** Never hardcode a `0x803F....` address in `.c` or `.cs`.
- **Game addresses in C# are `GameInfo`/`Play` base + the decomp's offset,** with the tww-decomp field in a comment (`GameMemoryAddresses`). `tww-decomp/` is the authority for offsets and behaviour.
- **Never commit Nintendo files** (`main.dol`, `RELS.arc`, `bi2.bin`) or build output. They are gitignored.
- Edit files in place. Never create `_fixed` / `_v2` copies.
- C/ASM rules for the injected code are in `GameMod/CLAUDE.md`. Read it before touching `GameMod/`.

## Build, run, test
```
dotnet build "WW-Online.sln"                          # everything (should be 0 warnings)
dotnet test "WWOnline.Tests/WWOnline.Tests.csproj"    # client + server
dotnet test "WWOnline.Patcher.Tests/WWOnline.Patcher.Tests.csproj"
dotnet run --project WWOnline.Client/WWOnline.Client.csproj
dotnet run --project WWOnline.Server/WWOnline.Server.csproj -- [port]   # default 6969; --help for flags/WWO_* env vars
docker build -f WWOnline.Server/Dockerfile -t ww-online-server .          # server image (context = repo root)
dotnet run --project WWOnline.Client/WWOnline.Client.csproj -- --build-patchdata out/PatchData   # release PatchData
```
- If a build fails with "file is locked", a running client or server (e.g. from dev-test) holds `bin/`. Run `.\scripts\dev-test.ps1 -Stop`, or build with `--artifacts-path <tmp>`.
- HotAvalonia reloads `.axaml` in Debug builds. `dotnet watch` handles C# changes.
- **Version:** the release version comes from the git tag (`v1.2.3` means `-p:Version=1.2.3`). `Directory.Build.props` holds the dev version and `WwoRepositoryUrl`, the one place the GitHub owner/repo lives. Read them at runtime through `AppInfo` (Shared), never from a hardcoded string.
- **Protocol:** bump `HubConstants.ProtocolVersion` whenever a hub method, callback or wire DTO changes. `GameHub.Join` refuses a client on a different protocol, and `SignalRClientService` surfaces the reason (`JoinRejected`, and the `ConnectAsync` failure message).
- **Releases:** push a `v*` tag. `.github/workflows/release.yml` builds PatchData (`--build-patchdata` in devkitPro's Linux container, tag pinned to the local devkitPPC r47.1), packs the client with Velopack and publishes a GitHub Release. Installed apps update from it through `UpdateService`, which is a no-op in dev builds. It then pushes the server's Docker image (amd64 + arm64) to `ghcr.io/<owner>/ww-online-server`. CI (`ci.yml`) runs the Nintendo-files guard, a `-warnaserror` build, both test projects and an amd64 image build + smoke test. See `docs/releasing.md`.
- **Server config:** `ServerOptions` (CLI flags over `WWO_*` env vars over defaults), endpoints in `ServerApp` (hub + `GET /health`). Self-hosting, Docker and the owner key: `docs/self-hosting.md`.
- **Nintendo-files guard:** run `./scripts/check-no-nintendo-files.ps1` before any commit that adds binaries. With `-Path <dirs>` it checks build output and packages.

## Multiplayer test cycle: `scripts\dev-test.ps1`
- `.\scripts\dev-test.ps1` stops the old run, builds the C#, checks the game build stamp, then launches 2 Dolphins and 2 auto-connecting clients (Player1 hosts the server).
- `-Patch` also recompiles the C code and re-patches the game. This is a compile, so only use it when asked. `-Stop` / `-Collect` stop everything / copy the live Dolphin logs. `-AllowStale` launches against a stale build (it passes `--allow-stale`: otherwise a client's `--auto-attach` refuses a stale game). `-DedicatedServer` runs the server as a separate process.
- **Logs** are in `logs/latest/`. Older runs are in `logs/sessions/<time>/`.
  - `session.txt`: git rev, PIDs, build-check result.
  - `client-Player<N>.log`: `[diag]` every 2s, plus `[puppet]`, `[world]`, `[items]`, `[story]`, `[wallet]` and `[build-check]` lines.
  - `server.log`: joins and leaves, `[room]` rules/owner, sync store activity, `[stats]` relay rates every 10s.
  - `dolphin-<N>.log`: OSReport output (`[PUPPET]` lines from the REL) and MMU invalid read/write errors.
  - `build.log`, `patch.log`, `build-check.log`.
- The client's headless modes are `--check-build` (exits 0 fresh / 2 stale / 3 no stamp), `--patch`, `--build-patchdata <dir>` and `--log-file <path>`.

## Layout
```
WWOnline.Shared/           DTOs (PuppetData, RoomSettings, RoomInventory, StageFlags, StoryFlags,
                           EventFlagCatalog), IGameHubClient, HubConstants
WWOnline.Server/           ASP.NET SignalR relay: Hubs/GameHub (relay + room rules/owner) and the
                           room stores (WorldFlag, Wallet, RoomInventory, StoryFlag, OwnerSeedGate)
WWOnline.Client/           Avalonia app (MVVM, CommunityToolkit.Mvvm, all-singleton DI in App.axaml.cs)
  Services/                Dolphin memory, puppet + room sync services, patcher UI, HeadlessCommands,
                           UpdateService (Velopack auto-update), AppPaths
  Data/                    GameMemoryAddresses, PuppetLayout.g.cs (generated, do not edit)
  build/PuppetLayout.targets  generates PuppetLayout.g.cs from puppet_shared.h on every build
WWOnline.Patcher/          C# build pipeline (devkitPPC -> REL + DOL patches); PatcherConfig
WWOnline.Tests/            client + server tests (xUnit, FakeDolphin)
WWOnline.Patcher.Tests/
GameMod/                   game-side mod (see GameMod/CLAUDE.md)
  src/puppet_link/         puppet REL (puppet*.c) + draw hook (link_draw_hook.c) + puppet_shared.h
  src/patches/             hand-written ASM patches (+ generated link_draw_hook.asm, patch_diffs/)
  include/                 WW headers, ww_inlines.h offset macros, ww_linker.ld symbol addresses
  build/                   compiled output (gitignored)
  config.json              machine paths (gitignored; copy config.example.json)
tww-decomp/                Wind Waker decompilation (submodule), the reference for offsets
scripts/dev-test.ps1       local multiplayer test harness
Directory.Build.props      app version + GitHub repo URL (all projects)
.github/workflows/         ci.yml (every push/PR), release.yml (v* tags; docs/releasing.md)
deploy/docker-compose.yml  dedicated server example (image built from WWOnline.Server/Dockerfile)
scripts/                   check-no-nintendo-files.ps1 (CI + release guard)
```

## Patcher and config
`GameMod/config.json` is machine-specific. Copy it from `config.example.json`:
- `game_path` is the output folder, which each patch overwrites.
- `vanilla_game_path` is an extracted vanilla game (`sys/main.dol`, `sys/bi2.bin`, `files/RELS.arc`). If it is unset, `GameMod/vanilla/` is used.
- `devkitppc_path` defaults to `C:\devkitPro\devkitPPC`.

The client's Patch Game button (and the setup's Patch step, the same `GamePatcherService.PatchGameAsync`) uses the Settings page's vanilla and game paths instead of the config's. It first copies any game files the patched folder lacks (all of them into a new folder), since only a complete extracted disc boots.

Pipeline (`PipelineRunner`, Full = all six steps):
1. Copy the vanilla files.
2. Compile `puppet.c`, turn it into a REL and put it in RELS.arc.
3. Compile `link_draw_hook.c` into `link_draw_hook.asm`.
4. Assemble the ASM into YAML diffs.
5. Apply the diffs to main.dol.
6. Set bi2.bin to 48MB.

A Full build writes `<game_path>/wwo-build-stamp.json`, a hash of the GameMod C/ASM/include sources and the selected optional patches. The client and dev-test refuse a stale build.

Optional patches (`GameMod/src/patches/optional/*.asm`, betterww QoL + vanilla bug fixes) are chosen per player: `GameSettings.OptionalPatches` (null = each patch's `@default`), `--patches a,b|none|default` headless, `dev-test.ps1 -Patches a,b`. See `docs/optional-patches.md`. Without devkitPPC, the client falls back to the pre-built `PatchData/`.

**PatchData** is what an installed app patches from (no devkitPPC, no GameMod). `--build-patchdata <dir>` builds it from the GameMod sources and devkitPPC only (no config.json, no game files, Linux or Windows): steps 2-4 into a temp folder (`PipelineRunner.BuildMode.PatchData`, `PatchDataBuilder`), plus the optional patch catalogue, assets and `patchdata-stamp.json`. The stamp holds per-file source hashes (required, and per optional patch), so it reproduces `BuildStamp`'s source hash for any selection: a pre-built patch writes a game stamp comparable with both PatchData and the sources. `GameBuildCheck` is the one build check (Settings page, startup, `--check-build`): against the sources in dev, against `PatchData/patchdata-stamp.json` when installed, so an update with new game code asks the player to patch again. Dev builds' `bin/PatchData` is copied from `GameMod/build` + `patch_diffs` (no stamp: selection-only check). See `docs/releasing.md`.

## First run and starting the game
- **Setup wizard** (`SetupWizardViewModel`, Settings → Run setup again): Welcome → Dolphin → Game (extracts an ISO/RVZ with DolphinTool when its `extract --help` shows `-i`/`-o`, else Dolphin's Extract Entire Disc steps; `GameFolderCheck` wants GZLE01) → Patches (the shared `PatchOptionsView`) → You → Patch → Play. `FirstRun` shows it once (`GameSettings.SetupCompleted`); old settings with a patched game are marked done, and scripted starts (dev-test) skip it.
- **Gating:** `GameLaunchService` is the one start/attach path (after Host/Join when `AutoLaunchDolphin`, the Dolphin page's Start game, CLI `--auto-attach`). It never starts or attaches to a game whose build check isn't fresh (`LaunchReadiness`), and before the sync starts it checks the running game (`PatchedGameCheck`: 48 MB MEM1, and the DOL draw hook's `STATUS_ADDR` FourCC), so a vanilla/PAL game or a Dolphin without the memory override is refused. Manual Attach still attaches, with a warning. Attaches are serialised (one `ConnectAsync` at a time). It starts Dolphin with `-C` overrides for the 48 MB MEM1.
- `WWO_SETTINGS_DIR=<dir>` runs the client on a throwaway settings folder (screenshots, trying the setup) without touching %AppData%\WWOnline.

## Room sync model
- The **room owner** is the player who started the server, otherwise the earliest-joined player still connected. The owner can change the rules at runtime.
- The **rules** (`RoomSettings`) are shared wallet, shared world, shared items and shared story. The Full sync preset turns them all on. Co-op turns them all off, so players only see each other. The server ignores messages for a rule that is off.
- The owner's game **seeds** the items, story and wallet stores. If the owner hasn't seeded them within 30s of the first joiner (`OwnerSeedGate`), a joiner seeds them.
- **World and story merge in one direction only.** World is per-stage save flags OR-merged. Story is event flags 0x00-0x41, masked by `StoryFlags.SyncMask`. **Items** only grow (OR / MAX), and only the owner can remove an item.
- **These never sync:** health, magic, arrow and bomb counts, bottle and bag contents, small keys, LocalOnly or risky event flags, event registers, mTmp.
- Every sync service resets and rejoins on `SignalRClientService.Connected`. Nothing is marked "sent" until the send succeeds. Puppets are position/animation/equipment (plus the boat while riding it) only, and never include yourself.
- **Name tags:** each peer's name (from `PlayerJoined`, sanitised to printable ASCII by `PuppetNameTags`) is drawn above their puppet by the REL. `PuppetSyncService` writes the REL's names block (pointer at `PUPPET_NAMES_PTR_ADDR`) only where it differs; "Show player names" (Appearance page, `GameSettings.ShowPlayerNames`) is the block's SHOW flag, local only. Log lines: `[names]`.
- **Live world** (`docs/live-world.md`): chest and switch bits applied from other players to the current stage are handed to the REL (`LiveWorldPoke` → the REL's boot-stamped block at `LIVEWORLD_PTR_ADDR`, one acknowledged batch at a time), which re-creates the actors that read their flag only at create (chests open empty, walls/floors/ice/barricades vanish, crystals show on) and clears stale small-key locks. Log lines: `[world] live world:`.
- **Visibility:** a puppet shows when the other player is in the same stage and room. On the Great Sea (`sea`, whose grid squares and islands are rooms) it's by distance instead, and crossing a square doesn't despawn puppets (`PuppetVisibility`).

## C# conventions
- ViewModels implement `IDisposable`: unsubscribe events and dispose timers/CTS.
- No `async void`. Timer handlers use `Task.Run` with try/catch. Use `Interlocked` for counters shared across threads.
- `SignalRClientService` guards `_hubConnection` with a `SemaphoreSlim`. Its send methods return false when offline.
- `GameHub` validates every input: null checks, lengths, ranges (never `Math.Abs` on client ints), NaN/Infinity.
- `DolphinService` is sync-only P/Invoke. Before touching save data, check `SceneStabilityGate`.
- Avalonia: `SystemAccentColor` must be a `Color`, not a brush. Grid has no `ColumnSpacing`, so use a margin on each item. For conditional styles use `Classes.x="{Binding Bool}"`; there is no `BoolConverters.ToSelector`. Theme tokens and `ww-*` classes are in `Styles/WindWakerTheme.axaml` (dark theme; Room pages use `EditMode`: owner-only, read-only until Edit).
