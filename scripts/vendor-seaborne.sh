#!/usr/bin/env bash
# Vendor Seaborne, the native game engine, into seaborne/: a snapshot of one commit of the Seaborne
# repository, without its history, minus what the desktop libtww never reads.
#
#   scripts/vendor-seaborne.sh <seaborne repo> [<commit>]      (default: the repository's HEAD)
#
# git archive exports only the commit's tracked files: no history, no untracked or ignored files, so
# nothing from a local build, a fetched source (ref/) or a disc comes along. seaborne/ is replaced as a
# whole, seaborne/VENDORED.md records where it came from, and Seaborne's own data scanner must pass on
# the result. Re-run it to update; never hand-edit seaborne/ (CLAUDE.md). Then commit:
#   git add seaborne && git commit
set -euo pipefail

src=${1:?usage: scripts/vendor-seaborne.sh <seaborne repo> [<commit>]}
ref=${2:-HEAD}
root=$(cd "$(dirname "$0")/.." && pwd)
dest=$root/seaborne

# path|why: dropped from the snapshot (the desktop build reads none of them)
excluded=(
    "switch|the Switch build (platform layer, deko3d, uam, the NRO and its forwarder)"
    "scripts/switch|the Switch build and deploy scripts"
    "scripts/release|Seaborne's own source and Switch release packaging"
    "scripts/docker|the container builds (they source scripts/switch)"
    "native/tools/dksh_cache|the Switch shader cache tool"
    "poc/AvaloniaHost|the proof-of-concept host; WWOnline.Client is the real one"
    ".github|Seaborne's own CI; ww-online's .github/workflows/ci.yml builds seaborne/"
    "AGENTS.md|agent notes for the Switch console and Seaborne's local build folders"
    "INSTALL.md|the Switch install guide"
    "docs/SWITCH_BUILD.md|Switch only"
    "docs/SWITCH_PERF_STUDY.md|Switch only"
    "docs/DEKO3D_MIGRATION_PLAN.md|Switch only"
    "docs/DEBUG_SERVER.md|the Switch debug server"
)

commit=$(git -C "$src" rev-parse --verify "$ref^{commit}")
date=$(git -C "$src" show -s --format=%cI "$commit")
subject=$(git -C "$src" show -s --format=%s "$commit")

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir "$tmp/seaborne"
git -C "$src" archive --format=tar "$commit" | tar -x -C "$tmp/seaborne"

for entry in "${excluded[@]}"; do
    path=${entry%%|*}
    # a path the commit no longer has: the list is stale, fix it rather than carry it
    [[ -e $tmp/seaborne/$path ]] || { echo "vendor-seaborne: $path is not in $commit; update the exclusion list" >&2; exit 1; }
    rm -rf "${tmp:?}/seaborne/$path"
done

{
    echo "# Vendored Seaborne"
    echo
    echo "This folder is a snapshot of the Seaborne repository, made by \`scripts/vendor-seaborne.sh\`. It"
    echo "holds no history of that repository. Do not edit it here: change Seaborne upstream and vendor again."
    echo
    echo "- Commit: \`$commit\`"
    echo "- Commit date: $date"
    echo "- Commit subject: $subject"
    echo
    echo "Left out (the desktop libtww reads none of them; links to them in Seaborne's own docs are dangling here):"
    echo
    for entry in "${excluded[@]}"; do
        echo "- \`${entry%%|*}\`: ${entry#*|}"
    done
    echo
    echo "Never part of the snapshot: \`ref/\` (sources fetched at build time) and \`build/\` (build output),"
    echo "which are not in Seaborne's git."
} > "$tmp/seaborne/VENDORED.md"

python3 "$tmp/seaborne/scripts/check-no-nintendo-data.py" "$tmp/seaborne"

rm -rf "$dest"
mv "$tmp/seaborne" "$dest"
files=$(find "$dest" -type f | wc -l | tr -d ' ')
size=$(du -sh "$dest" | cut -f1)
echo "vendor-seaborne: seaborne/ is ${commit:0:9} ($date): $files files, $size"
