# Releasing WW-Online

Releases are GitHub Releases in this repo. Players install the app once (`WWOnline-win-Setup.exe`), and from then on it updates itself from those releases through [Velopack](https://velopack.io).

## Cut a release

1. Make sure `main` is green in CI.
2. Decide the version (SemVer). Bump `HubConstants.ProtocolVersion` (in `WWOnline.Shared/Hubs/HubConstants.cs`) if anything that crosses the wire changed: a hub method, a callback, or a DTO's shape or meaning. Players on different protocols can't join each other's rooms. They get a clear "update the app" message instead of subtle desyncs.
3. Tag and push:
   ```
   git tag v0.3.0            # or v0.4.0-beta.1 for a pre-release
   git push origin v0.3.0
   ```
   That's all. You don't edit a version file: the tag is the version. `Directory.Build.props` only holds the version that dev builds report.

To rehearse without releasing, run the **Release** workflow by hand (Actions → Release → Run workflow) with a version like `0.0.1-dryrun`. It builds and packs everything and keeps the packages as a workflow artifact, but doesn't create a release. It also builds the server image for both architectures without pushing it.

## What the release workflow does

`.github/workflows/release.yml`, on a `v*` tag:

1. **version**: `v1.2.3` becomes `1.2.3`. A `-suffix` makes it a pre-release.
2. **patchdata** (Linux): runs in devkitPro's `devkitpro/devkitppc` container, pinned to the devkitPPC release the game code is tested with (see [PatchData](#patchdata)). It installs .NET 9 and runs the client's headless `--build-patchdata` mode, which builds the complete `PatchData/` from `GameMod/` **without any game files**. The output is checked by the Nintendo-files guard and uploaded as an artifact.
3. **windows**:
   - Runs the guard on the repo, then a 0-warning build and both test projects.
   - Publishes the client self-contained for `win-x64` with `-p:Version=<tag>` and `-p:WwoRepositoryUrl=<this repo>`, so the app always updates from the repo it was released from.
   - Publishes the server into the client's `server/` folder, which the Host button runs.
   - Runs the guard on the CI-built `PatchData/`, then puts it in the client in place of whatever the publish copied, and checks every file against its stamp. Players patch from it; they have no devkitPPC.
   - Zips the server twice: `win-x64`, which is self-contained, and `portable`, which needs the ASP.NET Core 9 runtime and runs as `dotnet WWOnline.Server.dll [port]` on a Linux VPS.
   - Runs the guard on the published app and the server zips.
   - Runs `vpk download github` to fetch the previous release, so Velopack can build a small delta package.
   - Runs `vpk pack`: packId `WWOnline`, title `WW-Online`, main exe `WWOnline.Client.exe`, icon `assets/branding/app.ico`.
   - Runs the guard on the packages (it opens the `.nupkg` and `Portable.zip`).
   - Runs `vpk upload github --publish` to create the release. `gh release upload` then attaches the server zips.
4. **docker** (Linux, after **windows**, so an image only exists for a version that was released): builds the dedicated server's image from `WWOnline.Server/Dockerfile` for `linux/amd64` and `linux/arm64` (QEMU + Buildx) and pushes it to `ghcr.io/<owner>/ww-online-server` with the tags `<version>`, `<major>.<minor>` and `latest`. A pre-release only gets its exact version tag. The image reports the tag's version and commit in `/health`. A dry run builds both architectures and pushes nothing. Players and admins use it as described in [self-hosting.md](self-hosting.md).

The release's assets are:
- `WWOnline-win-Setup.exe`, the installer players download.
- `WWOnline-win-Portable.zip`.
- `WWOnline-<v>-full.nupkg` and `-delta.nupkg`.
- `releases.win.json` and `RELEASES`, the update feed.
- `WW-Online-Server-<v>-*.zip`.

The server image `ghcr.io/<owner>/ww-online-server:<v>` is pushed next to the release (step 4).

The windows job uses `GITHUB_TOKEN` (`contents: write`); the patchdata job, which runs repo code in a third-party image, only gets `contents: read`; the docker job gets `contents: read` and `packages: write`. No secrets need setting up.

CI (`ci.yml`) also builds the image for amd64 on every push and PR and smoke-tests it (`/health`, SignalR negotiate, the `HEALTHCHECK` turns healthy, a clean stop), so a broken Dockerfile shows up before a release.

### The GHCR package: make it public once

The first release push creates the `ww-online-server` package under the repo owner's account. GitHub links it to this repo by itself (the workflow pushes with this repo's `GITHUB_TOKEN`, and the image's `org.opencontainers.image.source` label names the repo), so it shows on the repo page and this repo's workflows can push to it. But a linked package inherits the repo's *access permissions*, not its *visibility*: **a new package is private**, and `docker pull` fails for everyone else until you change it. Once, after the first release:

1. Open the package: the repo page → **Packages** → `ww-online-server` (or `https://github.com/users/<owner>/packages/container/package/ww-online-server`).
2. **Package settings** → **Danger Zone** → **Change visibility** → **Public**. This can't be undone.

Later pushes keep the visibility.

## PatchData

An installed app has no devkitPPC and no `GameMod/` sources. It patches the player's game from the `PatchData/` folder next to the exe, which the release workflow builds with:

```
WWOnline.Client --build-patchdata <outDir> [--gamemod <GameMod dir>] [--log-file <path>]
```

- It needs only the `GameMod/` sources (found above the working directory or the app, or `--gamemod`) and devkitPPC (`DEVKITPPC` if that folder exists, else `C:\devkitPro\devkitPPC` / `/opt/devkitpro/devkitPPC`). It reads no `config.json` and no game files, and runs on Linux and Windows.
- It runs the pipeline's own steps (`PipelineRunner.BuildMode.PatchData`, `PatchDataBuilder`): build the puppet REL (not inserted anywhere), generate the draw hook ASM, and assemble the required patches plus **every** optional patch. The intermediate output goes to a temp folder, so it never writes the source tree.
- `<outDir>` must be new, empty or a previous PatchData (it has a stamp), and neither inside nor above `GameMod/`. It is replaced once the build has succeeded (a failed build leaves it alone).
- Exit code 0 on success, 1 on failure. The log lists every file written. (The client is a GUI-subsystem exe on Windows, so PowerShell doesn't wait for it when you run the exe directly: use `dotnet run`, or `Start-Process -Wait` as dev-test does.)

The layout (`PatchDataFolder`):

| path | what |
|---|---|
| `d_a_puppet.rel` | the puppet REL (module 0x58), inserted into RELS.arc when patching |
| `patch_diffs/<name>_diff.yaml` | required patches (`use_extra_memory`, `link_draw_hook`) |
| `patch_diffs/optional/<id>_diff.yaml` | every optional patch with ASM, so any selection can be applied |
| `optional/<id>.asm` | the optional patch catalogue (the Settings checklist reads the headers) |
| `assets/<file>` | files `replace-file` edits copy into the game |
| `free_space_start_offsets.txt` | where free space starts |
| `patchdata-stamp.json` | what it was built from, and every file's size and SHA-256 |

**The stamp and staleness.** `patchdata-stamp.json` (`PatchDataStamp`) records the per-file source hashes of the required sources and, separately, of each optional patch (its `.asm` and assets). From those it can recompute the source hash of any optional patch selection without the sources, and gets exactly the hash the live pipeline computes (`BuildStamp.ComputeSourceHash`; there is one definition). So:
- After a pre-built patch, the client writes the game's `wwo-build-stamp.json` with that hash for the chosen selection (`Origin: "prebuilt"`). Before patching, it checks every PatchData file against the stamp (missing, changed, or not listed) and refuses a damaged folder.
- The installed app's build check (`GameBuildCheck`, used by the Settings page, the startup log line and `--check-build`) compares the game's stamp with the hash its own PatchData gives for the selection the game was built with, plus the saved selection. An update whose game code differs (REL, hook, a required patch, or a selected optional patch) reads as **stale**: the Settings page says "Patch again" and the sidebar shows a "Patch the game again" prompt. An update that changed nothing the game was built from, or only an optional patch the player didn't pick, stays **up to date**. The app never patches by itself.
- In dev (a `GameMod/config.json` is found), the same check runs against the sources instead, as before. Pipeline and pre-built stamps are interchangeable: a pre-built game is fresh against matching sources, and a pipeline-built one is fresh against matching PatchData.

**Toolchain.** The container tag is pinned (`devkitpro/devkitppc:20250727`, devkitPPC r47.1 / gcc 15.1), the toolchain the game code is developed and tested with. With it, the CI-built REL and patch diffs should match a local Windows build's, so a pre-built patch matches a live full build. (The whole folder isn't byte-identical: the stamp has a build time, and the copied `.asm`/`.txt` files keep the checkout's line endings. The stamp's source hashes ignore line endings, so a stamp built from CI's LF checkout agrees with a Windows CRLF checkout.) `latest` moves to new gcc versions, which change the REL. Bump the tag deliberately, together with your local devkitPPC, and re-test in game (REL size and ARAM budget, hook size).

Rehearse it locally:
```
dotnet run --project WWOnline.Client/WWOnline.Client.csproj -- --build-patchdata out/PatchData --log-file out/patchdata.log
./scripts/check-no-nintendo-files.ps1 -Path out
```
The guard needs the folder to be called `PatchData`, because the REL is only allowed at a `PatchData/` path.

## How updates reach players

- The `UpdateService` in the client checks GitHub Releases 20 s after start-up and every 4 hours after that. It downloads a newer version in the background. The UI then offers "restart to update". If the player doesn't restart, Velopack applies the update on the next launch.
- A stable build only sees stable releases. A pre-release build, or one run with `WWO_UPDATE_PRERELEASE=1`, also sees pre-releases.
- An update that changed the game code ships new PatchData, so the patched game is out of date. The build check spots it by comparing the game's stamp with the new `PatchData/patchdata-stamp.json` (see [PatchData](#patchdata)): the Settings page and a sidebar prompt ask the player to press **Patch game** again. An update that didn't change the game code leaves the game up to date.
- Dev builds (`dotnet run`, `bin/`, `dev-test.ps1`) are not Velopack installs. Update checks are off there and never touch the network.
- An installed app writes its logs to `%LocalAppData%\WWOnline\logs`. The install folder is replaced on every update, so logs can't live there.
- Settings (`*.json`) live in `%AppData%\WWOnline` (roaming), outside Velopack's install root, so they survive an uninstall/reinstall. Before the rename to WW-Online they lived in `%LocalAppData%\WindwakerOnline`. On first start the client copies them (and that folder's `logs\` into the logs folder above) across once (`LegacyAppDataMigration`), and leaves the old folder alone.

## Test the updater locally

You need the vpk CLI: `dotnet tool install -g vpk --version 1.2.158`. Keep that version in step with the `Velopack` package in the client csproj and `VPK_VERSION` in release.yml.

1. Build and pack version A:
   ```
   dotnet publish WWOnline.Client/WWOnline.Client.csproj -c Release -r win-x64 --self-contained -p:Version=0.0.1 -o publish/a
   dotnet publish WWOnline.Server/WWOnline.Server.csproj -c Release -r win-x64 --self-contained -p:Version=0.0.1 -o publish/a/server
   Remove-Item publish/a/PatchData -Recurse
   dotnet run --project WWOnline.Client/WWOnline.Client.csproj -- --build-patchdata publish/a/PatchData
   vpk pack -u WWOnline -v 0.0.1 -p publish/a -e WWOnline.Client.exe --packTitle WW-Online -i assets/branding/app.ico -o C:\wwo-feed
   ```
   The `--build-patchdata` line swaps the dev PatchData the publish copied from `GameMod/` for a release one with a stamp, as the release workflow does (it needs devkitPPC). Without it, the installed app can still patch, but its build check only compares the selection.
2. Install it: run `C:\wwo-feed\WWOnline-win-Setup.exe`.
3. Build and pack version B (`0.0.2`) the same way, into the same `C:\wwo-feed`.
4. Point the installed app at the local feed and start it:
   ```
   $env:WWO_UPDATE_SOURCE = 'C:\wwo-feed'
   & "$env:LOCALAPPDATA\WWOnline\current\WWOnline.Client.exe"
   ```
   About 20 s after start-up, its log (`%LocalAppData%\WWOnline\logs`) shows `[update] WW-Online 0.0.2 is available` and then `downloaded`. `UpdateViewModel.RestartToUpdateCommand`, or the next launch, applies the update.
5. Uninstall through Windows Settings → Apps → WW-Online.

`WWO_UPDATE_SOURCE` also accepts another GitHub repo URL (a fork's releases) or an http(s) Velopack feed.

## The Nintendo-files guard

`scripts/check-no-nintendo-files.ps1` fails when a file looks like a Nintendo file. It checks:
- file names: `main.dol`, `RELS.arc`, `bi2.bin`, `boot.bin`, `apploader.img`, `fst.bin` and `opening.bnr`;
- extensions: `.dol .rel .arc .szs .bdl .bmd .bti .bck .bmg .bfn .blo .iso .gcm .rvz .nkit .wbfs .gcz .wia .ciso .tgc`;
- file magic: RARC, Yaz0, J3D, BMG (message text) and the GameCube disc header.

The only file it allows is our own `d_a_puppet.rel`, and only at these exact paths: `PatchData/`, `lib/app/PatchData/` in a nupkg and `current/PatchData/` in `Portable.zip`. The file must also carry our REL module id, `0x58`. Everything else in PatchData (`.yaml` diffs, `.asm` catalogue, `blank.thp`, `.txt`, `.json`) passes the normal checks.

```
./scripts/check-no-nintendo-files.ps1                           # tracked + untracked (not ignored) files
./scripts/check-no-nintendo-files.ps1 -Path publish/client, Releases   # folders; .zip/.nupkg are opened
```

CI runs it on every push. The release workflow runs it on PatchData, the published app, the server zips and the packages before anything is uploaded. Run it before making the repo public, and before any commit that adds binaries.

## Renaming or moving the repo

The GitHub owner/repo lives in one place: `WwoRepositoryUrl` in `Directory.Build.props`. It's the default update source for local and dev builds. Release builds use the repo the workflow runs in. The `README.md` badges and links name the repo too, and so do the image name in `docs/self-hosting.md`, `deploy/docker-compose.yml` and the OCI labels in `WWOnline.Server/Dockerfile`, so update them by hand. (The release workflow names the image after the repo owner and sets the image's labels from the repo it runs in, so released images are right either way.)
