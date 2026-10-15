# Vendored Seaborne

This folder is a snapshot of the Seaborne repository, made by `scripts/vendor-seaborne.sh`. It
holds no history of that repository. Do not edit it here: change Seaborne upstream and vendor again.

- Commit: `9357bf4dd56662ac707933ced09c59230ff82409`
- Commit date: 2026-10-15T16:32:24+01:00
- Commit subject: Regression fixes: the link census allows ww_online's peer boat's daShip_c references (expected_unresolved_phase2.txt takes '#' comments, regress.sh and the README diff without them); menu-controls goes down one more row for COS_PAD_BACKGROUND, which sits above the bindings on the Controls tab

Left out (the desktop libtww reads none of them; links to them in Seaborne's own docs are dangling here):

- `switch`: the Switch build (platform layer, deko3d, uam, the NRO and its forwarder)
- `scripts/switch`: the Switch build and deploy scripts
- `scripts/release`: Seaborne's own source and Switch release packaging
- `scripts/docker`: the container builds (they source scripts/switch)
- `native/tools/dksh_cache`: the Switch shader cache tool
- `poc/AvaloniaHost`: the proof-of-concept host; WWOnline.Client is the real one
- `.github`: Seaborne's own CI; ww-online's .github/workflows/ci.yml builds seaborne/
- `AGENTS.md`: agent notes for the Switch console and Seaborne's local build folders
- `INSTALL.md`: the Switch install guide
- `docs/SWITCH_BUILD.md`: Switch only
- `docs/SWITCH_PERF_STUDY.md`: Switch only
- `docs/DEKO3D_MIGRATION_PLAN.md`: Switch only
- `docs/DEBUG_SERVER.md`: the Switch debug server

Never part of the snapshot: `ref/` (sources fetched at build time) and `build/` (build output),
which are not in Seaborne's git.
