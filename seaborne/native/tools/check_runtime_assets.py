#!/usr/bin/env python3
"""Check the runtime assets against the decompilation's extraction (docs/RUNTIME_ASSETS.md, step 3).

    native/tools/check_runtime_assets.py --stubs build/assets-stubs/GZLE01 \\
        --run-dir RUN --decomp build/decomp [--version GZLE01|GZLP01]

RUN/assets/ holds what COS_SMOKE=assets wrote: every array as the loader (pc_assets.cpp) filled it,
read from the disc through Aurora's DVD layer, Yaz0 and RARC readers of its own and the DOL/REL
section tables. The reference is independent: the decompilation's dtk split of the same disc
(build/decomp/build/<version>/bin/assets/<header>.bin, gen_assets.sh --version), the input its header converters
read; for the arrays our tree keeps out of its source (extract_data_assets.py ENTRIES), the symbol's
bytes in the same split's objects. Vec, cXy and u32 arrays are compared after swapping their
big-endian words, as the loader does and the converters did. Every byte must match. Reads
disc-derived files only locally; writes nothing.
"""
import argparse
import json
import struct
import sys
from pathlib import Path

from extract_data_assets import elf_symbols, find


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--stubs", required=True, type=Path, help="gen_assets.sh --stubs output")
    ap.add_argument("--run-dir", required=True, type=Path, help="COS_RUN_DIR of the assets smoke")
    ap.add_argument("--decomp", required=True, type=Path)
    ap.add_argument("--version", default=None, choices=("GZLE01", "GZLP01"),
                    help="the disc (default: the one the stubs were generated for)")
    args = ap.parse_args()
    index = json.loads((args.stubs / "include/assets/cos_asset_index.json").read_text())
    version = args.version
    if version is None:  # cos_assets.h names it (stubs older than GZLP01 support: GZLE01)
        text = (args.stubs / "include/assets/cos_assets.h").read_text()
        version = "GZLP01" if '#define COS_ASSETS_GAME_ID "GZLP01"' in text else "GZLE01"
    bins = args.decomp / f"build/{version}/bin/assets"
    bad = 0
    objects = {}
    for row in index:
        got_path = args.run_dir / "assets" / f"{row['id']:03d}_{row['name']}.bin"
        if "object" in row:
            want_path = args.decomp / f"build/{version}" / row["object"]
            if want_path.exists() and want_path not in objects:
                objects[want_path] = elf_symbols(want_path)
            hit = find(objects[want_path], row["symbol"], row["nth"]) if want_path.exists() else None
            want = hit[2] if hit else None
        else:
            want_path = bins / (Path(row["header"]).stem + ".bin")
            want = want_path.read_bytes() if want_path.exists() else None
        if not got_path.exists() or want is None:
            print(f"MISSING {row['header']}: {got_path if not got_path.exists() else want_path}")
            bad += 1
            continue
        got = got_path.read_bytes()
        if row["kind"] == "COS_ASSET_F32":
            n = len(want) // 4
            want = struct.pack(f"<{n}I", *struct.unpack(f">{n}I", want[: 4 * n])) + want[4 * n:]
        if got != want:
            diff = next((i for i, (a, b) in enumerate(zip(got, want)) if a != b), min(len(got), len(want)))
            print(f"DIFFER {row['header']}: {len(got)} vs {len(want)} bytes, first difference at {diff:#x}")
            bad += 1
    print(f"check_runtime_assets: {len(index) - bad} of {len(index)} arrays identical to the dtk extraction")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
