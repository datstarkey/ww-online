# CLAUDE.md

WW-Online: online multiplayer for The Legend of Zelda: The Wind Waker (GameCube, in Dolphin).
Other players appear as puppet Links, and a room can share world, items, story, wallet, bait, spoils and delivery bags, and projectiles.
It has two halves: C# apps (client, relay server, patcher) and the game-side mod (`GameMod/`, C + ASM injected into the game).

## Key rules
- **Never compile or patch the game unless the user asks.** That means `scripts\dev-test.ps1 -Patch`, `--patch`, the UI's Patch Game button and `PipelineRunner.RunAsync`. Say when the C code is ready, then wait. Building and testing the C# code is fine.
- **When debugging a test session, read `logs/latest/` first** (see below), before theorising.
- **`GameMod/src/puppet_link/puppet_shared.h` is the only place emulator scratch/sync addresses live.** Never hardcode a `0x803F....` address in `.c` or `.cs`.
- **Game addresses in C# are `GameInfo`/`Play` base + the decomp's offset,** with the tww-decomp field in a comment (`GameMemoryAddresses`). `tww-decomp/` is the authority for offsets and behaviour.
- **Never commit Nintendo files** (`main.dol`, `RELS.arc`, `bi2.bin`) or build output. They are gitignored.
- **Item icons come from the player's own game, never the repo.** `ItemIconService` decodes `files/res/Msg/itemicon.arc` (BTI → PNG, `WWOnline.Patcher/Icons`, `BinaryFormats/Bti`) into `AppPaths.IconCacheDirectory` (`%LocalAppData%\WWOnline\GameIcons`, or under `WWO_SETTINGS_DIR`): at startup and after a successful patch when missing or out of date, and on demand from Settings → Item icons. A named mutex per folder serialises extraction across clients. Views bind `ItemIcons` and fall back to their placeholders while it's null. Never download icon rips or put icons in the repo, PatchData or a release: the guard rejects `GameIcons/` folders and our marked PNGs. The item → texture table is `ItemIconCatalog` (from tww-decomp `dItem_data::item_resource`).
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
- **Protocol:** bump `HubConstants.ProtocolVersion` whenever a hub method, callback or wire DTO changes. `GameHub.Join` refuses a client on a different protocol, and `SignalRClientService` surfaces the reason (`JoinRejected`, and the `ConnectAsync` failure message). History: 1 first build; 2 held items + boat parts (`PuppetData` fields); 3 room switches; 4 player events / projectiles + `RoomSettings.SharedProjectiles`; 5 anim mirror, shared bait and spoils bags (`RoomSettings.SharedBait` / `SharedSpoils`) and warp to player (`PuppetData.Warp`, `RoomSettings.AllowWarping`); 6 figurines and warp jars in the room story (`StoryFlags.Figurines` / `WarpJars`), the shared delivery bag (`RoomSettings.SharedDelivery`) and the room's heart sources (`RoomInventory.HeartSources`). v0.1.0 ships protocol 4, v0.2.0 protocol 5.
- **Hub** (`HubConstants`, `IGameHubClient`): `Join` first on every (re)connect (`SetPlayerName` only turns pre-protocol clients away); `SendPuppetData` / `SendPlayerGameState` / `GetPlayers`; world `SendStageFlags` / `GetWorldFlags`; wallet `JoinWallet` / `SendRupeeDelta`; rules `GetRoomSettings` / `SetRoomSettings` / `ClaimRoomOwner`; items `JoinRoomInventory` / `SendInventoryGains` / `SetRoomInventory` / `GetRoomInventory`; story `JoinRoomStory` / `SendStoryFlags`; switches `JoinRoomSwitches` / `SendRoomSwitches`; projectiles `SendPlayerEvent`. Callbacks: `PlayerJoined` / `PlayerLeft` / `UpdatePlayerCount`, `ReceivePuppetData`, `ReceivePlayerGameState` and `Receive{StageFlags,RupeeTotal,RoomSettings,RoomInventory,StoryFlags,RoomSwitches,PlayerEvent}`.
- **Releases:** push a `v*` tag. `.github/workflows/release.yml` builds PatchData (`--build-patchdata` in devkitPro's Linux container, tag pinned to the local devkitPPC r47.1), packs the client with Velopack (with `docs/release-notes/v<version>.md` as the release notes when it exists) and publishes a GitHub Release. Installed apps update from it through `UpdateService`, which is a no-op in dev builds. It then pushes the server's Docker image (amd64 + arm64) to `ghcr.io/<owner>/ww-online-server`. CI (`ci.yml`) runs the Nintendo-files guard, a `-warnaserror` build, both test projects and an amd64 image build + smoke test. See `docs/releasing.md`.
- **Server config:** `ServerOptions` (CLI flags over `WWO_*` env vars over defaults), endpoints in `ServerApp` (hub + `GET /health`). Self-hosting, Docker and the owner key: `docs/self-hosting.md`.
- **Nintendo-files guard:** run `./scripts/check-no-nintendo-files.ps1` before any commit that adds binaries. With `-Path <dirs>` it checks build output and packages.

## Multiplayer test cycle: `scripts\dev-test.ps1`
- `.\scripts\dev-test.ps1` stops the old run, builds the C#, checks the game build stamp, then launches 2 Dolphins and 2 auto-connecting clients (Player1 hosts the server).
- `-Patch` also recompiles the C code and re-patches the game. This is a compile, so only use it when asked. `-Stop` / `-Collect` stop everything / copy the live Dolphin logs. `-AllowStale` launches against a stale build (it passes `--allow-stale`: otherwise a client's `--auto-attach` refuses a stale game). `-DedicatedServer` runs the server as a separate process.
- **Logs** are in `logs/latest/`. Older runs are in `logs/sessions/<time>/`.
  - `session.txt`: git rev, PIDs, build-check result.
  - `client-Player<N>.log`: `[diag]` every 2s, plus `[puppet]`, `[held]`, `[names]`, `[world]` (incl. `live world:`), `[switches]`, `[keys]`, `[hearts]`, `[items]`, `[story]`, `[wallet]`, `[fx]` (projectile events sent / received / dropped, REL counters), `[launch]`, `[icons]` and `[build-check]` lines.
  - `server.log`: joins and leaves, `[room]` rules/owner, sync store activity, `[stats]` relay rates every 10s.
  - `dolphin-<N>.log`: OSReport output (`[PUPPET]` lines from the REL) and MMU invalid read/write errors.
  - `build.log`, `patch.log`, `build-check.log`.
- The client's headless modes are `--check-build` (exits 0 fresh / 2 stale / 3 no stamp), `--patch`, `--build-patchdata <dir>` and `--log-file <path>`.
- **A game crash or freeze** (JUTException screen, `ISI exception` / invalid read in `dolphin-<N>.log`): read the logs, then `scripts/dolphin-crash-context.py` (registers + symbolised stack) and `scripts/dolphin-link-anm-check.py` (foreign animator pointers in Link's shared model data) against the live Dolphin. See GameMod/CLAUDE.md, "Debugging a crash".

## Layout
```
WWOnline.Shared/           DTOs (PuppetData, RoomSettings, RoomInventory, StageFlags, StoryFlags,
                           RoomSwitches, EventFlagCatalog), IGameHubClient, HubConstants
WWOnline.Server/           ASP.NET SignalR relay: Hubs/GameHub (relay + room rules/owner) and the
                           room stores (WorldFlag, Wallet, RoomInventory, StoryFlag, RoomSwitch, OwnerSeedGate)
                           and PlayerEventRelay (who gets a player's projectile events)
WWOnline.Client/           Avalonia app (MVVM, CommunityToolkit.Mvvm, all-singleton DI in App.axaml.cs)
  Services/                Dolphin memory, puppet + room sync services, patcher UI, HeadlessCommands,
                           UpdateService (Velopack auto-update), AppPaths
  Data/                    GameMemoryAddresses, PuppetLayout.g.cs (generated, do not edit)
  build/PuppetLayout.targets  generates PuppetLayout.g.cs from puppet_shared.h on every build
WWOnline.Patcher/          C# build pipeline (devkitPPC -> REL + DOL patches); PatcherConfig;
                           WorldData/ (stage data reader, switch / small-key / heart tables, built from the player's game)
WWOnline.Tests/            client + server tests (xUnit, FakeDolphin)
WWOnline.Patcher.Tests/
GameMod/                   game-side mod (see GameMod/CLAUDE.md)
  src/puppet_link/         puppet REL (puppet*.c) + draw hook (link_draw_hook.c) + puppet_shared.h
  src/patches/             hand-written ASM patches (+ generated link_draw_hook.asm, patch_diffs/)
  include/                 WW headers, ww_inlines.h offset macros, ww_linker.ld symbol addresses
  build/                   compiled output (gitignored)
  config.json              machine paths (gitignored; copy config.example.json)
tww-decomp/                Wind Waker decompilation (submodule), the reference for offsets
scripts/                   dev-test.ps1 (local multiplayer test harness), check-no-nintendo-files.ps1
                           (CI + release guard), dolphin-crash-context.py + dolphin-link-anm-check.py
                           (read-only crash debugging against a live Dolphin)
docs/                      design notes (held-items, live-world, small-keys, hearts, event-flags,
                           optional-patches), self-hosting, releasing, release-notes/<tag>.md
Directory.Build.props      app version + GitHub repo URL (all projects)
.github/workflows/         ci.yml (every push/PR), release.yml (v* tags; docs/releasing.md)
deploy/docker-compose.yml  dedicated server example (image built from WWOnline.Server/Dockerfile)
```

## Patcher and config
`GameMod/config.json` is machine-specific. Copy it from `config.example.json`:
- `game_path` is the output folder, which each patch overwrites.
- `vanilla_game_path` is an extracted vanilla game (`sys/main.dol`, `sys/bi2.bin`, `files/RELS.arc`). If it is unset, `GameMod/vanilla/` is used.
- `devkitppc_path` defaults to `C:\devkitPro\devkitPPC`.

The client's Patch Game button (and the setup's Patch step, the same `GamePatcherService.PatchGameAsync`) uses the Settings page's vanilla and game paths instead of the config's. It first copies any game files the patched folder lacks (all of them into a new folder), since only a complete extracted disc boots.

Pipeline (`PipelineRunner`, Full = all seven steps):
1. Copy the vanilla files.
2. Compile `puppet.c`, turn it into a REL and put it in RELS.arc.
3. Compile `link_draw_hook.c` into `link_draw_hook.asm`.
4. Assemble the ASM into YAML diffs.
5. Apply the diffs to main.dol.
6. Set bi2.bin to 48MB.
7. Build the switch table (`wwo-switch-table.json`) from the vanilla stage data into the game folder (`SwitchTableBuilder`; the pre-built patch path does it too). Nintendo-derived: never in the repo. Its `RulesVersion` is part of the stamp hash (bump it when the classification changes), and a stamped game without its table is stale.

A Full build writes `<game_path>/wwo-build-stamp.json`, a hash of the GameMod C/ASM/include sources and the selected optional patches. The client and dev-test refuse a stale build.

Optional patches (`GameMod/src/patches/optional/*.asm`, betterww QoL + vanilla bug fixes) are chosen per player: `GameSettings.OptionalPatches` (null = each patch's `@default`), `--patches a,b|none|default` headless, `dev-test.ps1 -Patches a,b`. See `docs/optional-patches.md`. Without devkitPPC, the client falls back to the pre-built `PatchData/`.

**PatchData** is what an installed app patches from (no devkitPPC, no GameMod). `--build-patchdata <dir>` builds it from the GameMod sources and devkitPPC only (no config.json, no game files, Linux or Windows): steps 2-4 into a temp folder (`PipelineRunner.BuildMode.PatchData`, `PatchDataBuilder`), plus the optional patch catalogue, assets and `patchdata-stamp.json`. The stamp holds per-file source hashes (required, and per optional patch), so it reproduces `BuildStamp`'s source hash for any selection: a pre-built patch writes a game stamp comparable with both PatchData and the sources. `GameBuildCheck` is the one build check (Settings page, startup, `--check-build`): against the sources in dev, against `PatchData/patchdata-stamp.json` when installed, so an update with new game code asks the player to patch again. Dev builds' `bin/PatchData` is copied from `GameMod/build` + `patch_diffs` (no stamp: selection-only check). See `docs/releasing.md`.

## First run and starting the game
- **Setup wizard** (`SetupWizardViewModel`, Settings → Run setup again): Welcome → Dolphin → Game (extracts an ISO/RVZ with DolphinTool when its `extract --help` shows `-i`/`-o`, else Dolphin's Extract Entire Disc steps; `GameFolderCheck` wants GZLE01) → Patches (the shared `PatchOptionsView`) → You → Patch → Play. `FirstRun` shows it once (`GameSettings.SetupCompleted`); old settings with a patched game are marked done, and scripted starts (dev-test) skip it.
- **Gating:** `GameLaunchService` is the one start/attach path (after Host/Join when `AutoLaunchDolphin`, the Dolphin page's Start game, CLI `--auto-attach`). It never starts or attaches to a game whose build check isn't fresh (`LaunchReadiness`), and before the sync starts it checks the running game (`PatchedGameCheck`: 48 MB MEM1, and the DOL draw hook's `STATUS_ADDR` FourCC), so a vanilla/PAL game or a Dolphin without the memory override is refused. Manual Attach still attaches, with a warning. Attaches are serialised (one `ConnectAsync` at a time). It starts Dolphin with `-C` overrides for the 48 MB MEM1.
- `WWO_SETTINGS_DIR=<dir>` runs the client on a throwaway settings folder (screenshots, trying the setup) without touching %AppData%\WWOnline.

## Room sync model
- The **room owner** is the player who started the server, otherwise the earliest-joined player still connected. The owner can change the rules at runtime.
- The **rules** (`RoomSettings`) are shared wallet, shared world, shared items, shared story, shared bait bag (`SharedBait`), shared spoils bag (`SharedSpoils`), shared delivery bag (`SharedDelivery`), other players' projectiles (`SharedProjectiles`: peers' bombs, cannon shots and arrows are real in your world) and warping (`AllowWarping`). The Full sync preset turns them all on. Co-op turns the seven progress rules and warping off but keeps projectiles on, so players see each other and fight together with their own progress. The server ignores messages for a rule that is off.
- The owner's game **seeds** the items, story, wallet, bait, spoils and delivery stores. If the owner hasn't seeded them within 30s of the first joiner (`OwnerSeedGate`), a joiner seeds them.
- **World and story merge in one direction only.** World is per-stage save flags OR-merged. Story is event flags 0x00-0x41, masked by `StoryFlags.SyncMask`, plus the Nintendo Gallery figurines Carlov has made (`StoryFlags.Figurines`: the 17 figurine bitfield registers, written only while idle; Carlov's in-progress figurine never syncs; `docs/figurines.md`), and which dungeon warp jars are open (`StoryFlags.WarpJars`: registers 0x9F-0xA4, value mask 0x07, `daObj_Warpt_c::m_event_reg`; `docs/live-world.md` §0.2), and Beedle's membership points (`StoryFlags.BeedlePoints`, register 0x86FF, merged by MAX). **Items** only grow (OR / MAX), and only the owner can remove an item. The sea chart menu's save data rides along (`RoomInventory.SeaMap`: charts owned / opened / completed, sea squares, Triforce charts; OR-merged). Max health leaves that MAX merge (derived hearts, below).
- **Small keys** (part of Shared world, `docs/small-keys.md`) are not synced but derived: a dungeon's count = its key flags taken (chest tbox / item bits) - its key doors opened (door switches), from a key table built from the player's own stage files (`SmallKeyTableBuilder`, at runtime by `SmallKeyTableProvider`). `SharedSmallKeyService` writes it into mKeyNum (the HUD's pending count in the current stage, only while idle; the saved copy elsewhere). No hub method or server state. The Room page's Dungeons card shows it. Log lines: `[keys]`.
- **Max health** (Shared items, `docs/hearts.md`) is derived, not merged: 12 + 4 per Heart Container + 1 per Piece of Heart that anyone in the room has taken. The heart table lists every source and its flag, built from the player's own stage files (chests, placed / dug-up items, salvage points, boss containers: `HeartTableBuilder`, at runtime by `HeartTableProvider`) plus our catalogue of the 16 NPC / minigame / letter rewards (`HeartCatalog`, which also fixes each source's ID); vanilla has exactly 44 pieces + 6 containers = 80. Each client adds the sources its own flags show to the grow-only `RoomInventory.HeartSources` (a 50-bit set, OR-merged; the server rejects unknown bits), and a source in the set counts once, so nothing is counted twice and the flags themselves never need to sync. `SharedHeartService` queues the difference on play.mItemMaxLifeCount (the HUD applies it and clamps health) only while idle, logs a heart no source explains and takes it back, and corrects saves with too many hearts; `RoomInventorySyncService` never applies or sends MaxHealth then (`IMaxHealthOwner` / `DerivedHeartsState`). The Room items page shows "Hearts: X of 6 containers, Y of 44 pieces". Log lines: `[hearts]`.
- **Bait bag** (SharedBait rule): All-Purpose Bait uses and Hyoi Pears (`BaitCounts`) are one room total, like the wallet: `JoinBait` (owner seeds, joiners adopt), then signed per-type `SendBaitDelta` → `ReceiveBaitTotal`. The server (`BaitStore`) keeps each type in range and both within the bag's 8 slots (an over-full gain is cut). `SharedBaitService` joins only once the game has the bait bag, and writes a total into mBait/mBaitNum with the fewest slot changes (`BaitBag.Apply`: uses off the emptiest slot, pears off the highest, slots on X/Y/Z last; gains top up partial slots, then fill empty ones), only while no event runs and the menu is closed, compare-and-swap against a fresh read, then fixes X/Y/Z buttons whose slot emptied. Log lines: `[bait]`.
- **Spoils bag** (SharedSpoils rule): one count per spoil type (`SpoilsCounts`, dBeastIndex_e order, 0-99), same hub shape (`JoinSpoils` / `SendSpoilsDelta` → `ReceiveSpoilsTotal`, `SpoilsStore`). mBeastNum is per TYPE; mBeast[8] only orders the menu (a type takes the first free slot). `SharedSpoilsService` writes the way a pickup or trade does: an arriving type's slot, then the count change on play.mItemBeastNumCounts for d_meter to apply (it empties a type's slot and button at 0); it waits while any count is pending. Both bag services share `SharedBagService<T>` (join/seed/delta/reset/idle gate) and the server's `BagCountsStore<T>`. Log lines: `[spoils]`.
- **Delivery bag** (SharedDelivery rule, `docs/delivery-bag.md`): a presence store, not derived from flags (the trade goods are rebought, traded, set on Zunari's stand and thrown away with no flag, and the game itself reads "Father's / Moblin's Letter handed over" as "obtained and no longer in the bag"). `DeliveryCounts` = how many slots hold each of the 19 items 0x8C-0x9E, plus mReserveFlags ("ever obtained", OR-merged). Same hub shape (`JoinDelivery` / `SendDeliveryDelta` → `ReceiveDeliveryTotal`, `DeliveryStore`); a delta that takes an item the room no longer has is refused whole (two players trading the same item at once: only the first trade counts, nothing duplicates). `SharedDeliveryService` writes the way the game does (leaving items empty their slot, highest / off-button first; arriving ones take the first empty slot and set their obtained bit; X/Y/Z fixed like dComIfGp_setSelectItem), only while idle, compare-and-swap. Log lines: `[delivery]`.
- **These never sync:** health, magic, arrow and bomb counts, bottle contents (the bait, spoils and delivery bags only under their rules), LocalOnly or risky event flags, event registers other than the figurine bitfields and the warp jar registers and the warp jar registers, mTmp, Picto Box photos.
- Every sync service resets and rejoins on `SignalRClientService.Connected`. Nothing is marked "sent" until the send succeeds. Puppets are position/animation/equipment, the held item + aim + carried bomb (`HeldItemState`), appearance, plus the boat (and its cannon / crane) while riding it, only, and never include yourself.
- **Name tags:** each peer's name (from `PlayerJoined`, sanitised to printable ASCII by `PuppetNameTags`) is drawn above their puppet by the REL. `PuppetSyncService` writes the REL's names block (pointer at `PUPPET_NAMES_PTR_ADDR`) only where it differs; "Show player names" (Appearance page, `GameSettings.ShowPlayerNames`) is the block's SHOW flag, local only. Log lines: `[names]`.
- **Live world** (`docs/live-world.md`): chest and switch bits applied from other players to the current stage are handed to the REL (`LiveWorldPoke` → the REL's boot-stamped block at `LIVEWORLD_PTR_ADDR`, one acknowledged batch at a time), which re-creates the actors that read their flag only at create (chests open empty, walls/floors/ice/barricades vanish, crystals show on, locked doors come back unlocked; a lock byte is never written, docs/live-world.md 0.1). Log lines: `[world] live world:` (client) and `[PUPPET] live world:` per actor (Dolphin).
- **Live room switches:** latching dungeon-visit (dan) and room (zone) switches sync between players in the same stage / room, on-edges only, apply-once, never echoed (`RoomSwitchSyncService`, `RoomSwitchStore`, hub `JoinRoomSwitches`/`SendRoomSwitches`). Which switches are latching comes from the patch-time switch table, which also keeps push-block (path-encoded) memory switches out of the world sync. Log lines: `[switches]`.
- **Warp to player** (`AllowWarping`; docs/softlocks.md): one-way story/world merges can leave a player who is behind with no way forward (a peer left Outset with Tetra), so the Room page's Players list has **Warp to**. Client-only, no hub method: each client's `PuppetData.Warp` (`WarpInfo`, built by `LocalWarpReader` every puppet tick) carries the entrance it last really entered its stage through (play.mCurStage's point / room / layer, kept across point -1/-2/-3 restarts in the same stage by `WarpEntryTracker`) and why nobody should warp to it now (`WarpBusy`: event, loading, dead, minigame, other character). The server validates it with the rest of `PuppetData` and strips it while the rule is off. `WarpPlanner` decides: same stage and room, `WarpMemory.MoveActor` moves the local Link (current.pos then old.pos, both angles, speed zeroed; retried up to 3 times if his proc or Acch pulls him back); anywhere else, `WarpMemory.RequestStageChange` loads the target's stage at that entrance the way `dComIfGp_setNextStage` does (mRestart last speed/mode/start code, then play.mNextStage with mEnable last), with no reposition afterwards. It refuses (a status line under the Players list) on the title screen / file select, in an event, between areas, down, in a minigame, in the pause menu, controlling another character, holding on / on the boat for a same-room move, for a target that is busy or silent for 3 s, and for an unknown entrance. Addresses: `GameMemoryAddresses.Warp`. Log lines: `[warp]`.
- **Visibility:** a puppet shows when the other player is in the same stage and room. On the Great Sea (`sea`, whose grid squares and islands are rooms) it's by distance instead, and crossing a square doesn't despawn puppets (`PuppetVisibility`, in Shared: the server uses it too).
- **Projectiles** (SharedProjectiles rule; docs/held-items.md): one-shot `PlayerEvent`s (bomb thrown / picked up / exploded / gone, boat cannon fired, arrow shot) go through the hub method `SendPlayerEvent` → `ReceivePlayerEvent`, not the 20Hz puppet data. The REL reports the local Link's projectiles in a game-heap events block (`PlayerEventBlock`, puppet_shared.h `PUPPET_FX_*`); `PlayerEventService` sends them with an origin (per app run) + seq, and writes peers' events back for the REL to spawn real bombs, cannonballs and arrows (they hit the receiver's world, never the receiver). The server (`PlayerEventRelay`) validates every field, rate-limits (burst 20, 10/s), checks the event is where its sender is, forgets locations after 3 s without puppet data, and relays it to the players in the event's own stage and room (the projectile's room, from the REL; sea: by distance from the event); an origin belongs to one connection until it leaves. Receivers drop repeats (origin + seq), events not in their stage and room, and everything during one of their minigames (`MinigameGate`).

## C# conventions
- ViewModels implement `IDisposable`: unsubscribe events and dispose timers/CTS.
- No `async void`. Timer handlers use `Task.Run` with try/catch. Use `Interlocked` for counters shared across threads.
- `SignalRClientService` guards `_hubConnection` with a `SemaphoreSlim`. Its send methods return false when offline.
- `GameHub` validates every input: null checks, lengths, ranges (never `Math.Abs` on client ints), NaN/Infinity.
- `DolphinService` is sync-only P/Invoke. Before touching save data, check `SceneStabilityGate`.
- Avalonia: `SystemAccentColor` must be a `Color`, not a brush. Grid has no `ColumnSpacing`, so use a margin on each item. For conditional styles use `Classes.x="{Binding Bool}"`; there is no `BoolConverters.ToSelector`. Theme tokens and `ww-*` classes are in `Styles/WindWakerTheme.axaml` (dark theme; Room pages use `EditMode`: owner-only, read-only until Edit).
