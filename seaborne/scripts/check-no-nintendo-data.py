#!/usr/bin/env python3
"""Fails if the repository holds Nintendo game data: disc files, binary files, or data blobs pasted
into source.

    scripts/check-no-nintendo-data.py            the git working tree (tracked files, and untracked
                                                 files that are not ignored)
    scripts/check-no-nintendo-data.py PATH...    those files and folders instead

The game reads its data from the player's own disc: the arrays the decompilation cuts out of
main.dol and the RELs come from native/tools/gen_assets.sh at build time or from the disc at start-up
(docs/RUNTIME_ASSETS.md), never from this repository. Checked:
- file names and extensions of Nintendo formats (main.dol, *.rel, *.arc, *.bmd, *.bfn, disc images...);
- leading magic numbers of those formats (RARC, Yaz0, J3D, BMG, BFN, THP, a GameCube disc header),
  so a renamed file is caught too;
- any other binary file (a NUL byte, or not UTF-8), unless ALLOWED_BINARY lists it;
- blobs in text files: a run of lines holding nothing but hex literals of BLOB_BYTES bytes or more
  (textures, display lists, microcode, fonts, as the decompilation's extractors write them), long
  base64 runs, and long runs of \\x string escapes; ALLOWED_BLOBS lists the reviewed tables that
  are code (math and audio coefficient tables, character maps);
- the committed stubs (native/assets/<ID>, gen_assets.sh --stubs) line by line: stub headers that
  declare arrays without contents, loader table rows, the row index, and resource index enums, and
  nothing else, so a regeneration cannot slip disc data in.
The spirit of ww-online's scripts/check-no-nintendo-files.ps1. Exit 0 = clean, 1 = found.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

FORBIDDEN_EXTENSIONS = {
    ".dol", ".rel", ".arc", ".szs", ".rarc", ".bdl", ".bmd", ".bti", ".bck", ".bca", ".bpk", ".brk", ".btk",
    ".btp", ".bas", ".bmg", ".bfn", ".blo", ".bms", ".aw", ".afc", ".ast", ".thp", ".dzs", ".dzr", ".dzb",
    ".jpc", ".gci", ".iso", ".gcm", ".rvz", ".nkit", ".wbfs", ".gcz", ".wia", ".ciso", ".tgc", ".dsp", ".ucode",
}
FORBIDDEN_NAMES = {"main.dol", "rels.arc", "bi2.bin", "boot.bin", "apploader.img", "fst.bin", "opening.bnr"}
MAGICS = [
    ("RARC archive", 0, b"RARC"),
    ("Yaz0-compressed data", 0, b"Yaz0"),
    ("J3D model or animation", 0, b"J3D"),
    ("BMG message text", 0, b"MESGbmg"),
    ("BFN font", 0, b"FONTbfn"),
    ("THP movie", 0, b"THP\0"),
    ("GameCube disc image", 0x1C, b"\xC2\x33\x9F\x3D"),
]

# Binary files that are ours, each reviewed: path -> why it holds no Nintendo data.
ALLOWED_BINARY = {
    "switch/native/icon/icon.jpg": "the NRO icon, original art (commit 7e7833cd0)",
    "native/data/initial_pipeline_cache.db": "GX pipeline keys only (docs/RUNTIME_ASSETS.md); no textures or models",
}

BLOB_BYTES = 128
# Reviewed hex tables in source that are code, not assets: (path, array name) -> why.
ALLOWED_BLOBS = {
    ("game/src/SSystem/SComponent/c_math.cpp", "atntable"): "arctangent lookup table (math)",
    ("game/src/JSystem/JAudio/JASDriverTables.cpp", "DSPRES_FILTER"): "resampling filter coefficients (math)",
    ("game/src/JSystem/JAudio/JASPlayer_impl.cpp", "CUTOFF_TO_IIR_TABLE"): "IIR filter coefficients (math)",
    ("game/src/d/actor/d_a_agb.cpp", "l_sjis2chrNo"): "Shift-JIS to GBA character map",
    ("game/src/d/actor/d_a_agb.cpp", "l_sjis1chrNo"): "Shift-JIS to GBA character map",
    ("game/src/f_op/f_op_msg_mng.cpp", "zfont"): "half-width to full-width Shift-JIS map",
    ("game/src/JSystem/JUtility/JUTDirectPrint.cpp", "sAsciiTable"): "ASCII to glyph index map",
    ("game/src/d/d_message.cpp", "dMsg2_tex_i4_color"): "per-character fade curve (240 values)",
    ("game/src/d/d_message_paper.cpp", "dMsg3_tex_i4_color"): "per-character fade curve (240 values)",
    ("game/src/JSystem/JUtility/JUTResFont.cpp", "halftofull"): "half-width to full-width character map",
    ("game/src/d/d_menu_collect.cpp", "item"): "collection screen item numbers",
    ("game/src/JAZelAudio/JAIZelParam.cpp", "m_bgm_mute_state"): "BGM mute flags per scene (game parameters)",
    ("game/src/JSystem/JAudio/JASSeqParser.cpp", "Arglist"): "sequence command argument formats",
    ("game/src/d/actor/d_a_movie_player.cpp", "daMP_VolumeTable"): "volume curve (math)",
    ("game/src/d/actor/d_a_kanban.cpp", "cut_parts_arg_data"): "per-piece angles (game parameters)",
    ("game/src/d/actor/d_a_npc_md.cpp", "anmTypeData_Talk"): "animation numbers (game parameters)",
    ("game/src/d/actor/d_a_wz.cpp", "enemy_arg_dt"): "enemy parameters per wave (game parameters)",
    ("game/src/d/actor/d_a_wz.cpp", "birth_dt"): "spawn parameters per wave (game parameters)",
    # third-party library sources
    ("game/src/PowerPC_EABI_Support/MSL/MSL_C/MSL_Common/Src/ctype.c", "__lower_map"): "Metrowerks MSL case map",
    ("game/src/PowerPC_EABI_Support/MSL/MSL_C/MSL_Common/Src/ctype.c", "__upper_map"): "Metrowerks MSL case map",
    ("game/src/PowerPC_EABI_Support/MSL/MSL_C/MSL_Common_Embedded/Math/Double_precision/e_rem_pio2.c",
     "two_over_pi"): "fdlibm constant (2/pi bits)",
    ("game/src/PowerPC_EABI_Support/MSL/MSL_C/MSL_Common_Embedded/Math/Double_precision/e_rem_pio2.c",
     "npio2_hw"): "fdlibm constant table",
}

TEXT_EXTENSIONS = {".c", ".cc", ".cpp", ".cp", ".h", ".hpp", ".inc", ".cs", ".py", ".js", ".ts", ".swift",
                   ".m", ".mm", ".glsl", ".wgsl", ".s", ".json", ".txt", ".md", ".sh", ".ps1", ".cmake", ".ini", ".yml"}
HEX_LINE = re.compile(r"^[\s{},]*(0[xX][0-9A-Fa-f]{1,8}[uUlL]*[\s,]*)+[\s{},;]*$")
HEX = re.compile(r"0[xX]([0-9A-Fa-f]{1,8})")
BASE64 = re.compile(r"[A-Za-z0-9+/]{256,}={0,2}")
ESCAPES = re.compile(r"(?:\\x[0-9A-Fa-f]{2}){128,}")
NAME = re.compile(r"([A-Za-z_]\w*)\s*(?:\[[^\]]*\]\s*)+(?:ATTRIBUTE_ALIGN\(\d+\)\s*)?=")
COMMENT = re.compile(r"//.*|/\*.*?\*/")

NUMBER = re.compile(r"\b\d+\b|0x[0-9A-Fa-f]+")
STUB_TREE = re.compile(r"^native/assets/[A-Z0-9]{6}/(include/assets/[^/]+|res/(?:[^/]+/)*[^/]+)$")
STUB_MARK = "// Generated by native/tools/gen_asset_stubs.py"
STUB_ROW = re.compile(r'^COS_ASSET_ROW\("\w+", "[\w/.]*", "[\w/.]*", \d+, 0x[0-9a-f]+, 0x[0-9a-f]+, '
                      r'COS_ASSET_(RAW|F32), "[0-9a-f]{40}"\)$')
RES_LINE = re.compile(r"^(\s*|#ifndef \w+|#define \w+|#endif.*|//.*|enum \w+ \{|\s*/\*.*\*/\s*"
                      r"|\s*\w+\s*=\s*(0x[0-9A-Fa-f]+|\d+),?\s*(//.*)?|\};)$")


def stub_tree(rel, text):
    """(line, what) for anything in native/assets/<ID> that is not the shape gen_assets.sh --stubs writes."""
    name = rel.rsplit("/", 1)[-1]
    lines = text.split("\n")
    if "/res/" in rel:
        if not name.endswith(".h"):
            return [(1, "not a resource index header")]
        return [(i + 1, "not a resource index enum line") for i, l in enumerate(lines) if not RES_LINE.match(l)]
    if name == "cos_asset_index.json":
        try:
            rows = json.loads(text)
            ok = all(isinstance(v, (int, str)) and len(str(v)) < 128 for r in rows for v in r.values())
        except (ValueError, AttributeError, TypeError):
            ok = False
        return [] if ok else [(1, "not a row index (a list of short names and numbers)")]
    if name == "cos_asset_table.inc":
        return [(i + 1, "not a COS_ASSET_ROW") for i, l in enumerate(lines)
                if l and not l.startswith("//") and not STUB_ROW.match(l)]
    if not name.endswith(".h") or not text.startswith(STUB_MARK):
        return [(1, "not a stub header of native/tools/gen_asset_stubs.py")]
    # the display-list macros (#define U32_AS_U8 ...) hold up to 8 numbers; a data line holds more
    return [(i + 1, "contents in a stub header") for i, l in enumerate(lines)
            if "{" in l or (not l.lstrip().startswith("//")
                            and len(NUMBER.findall(l)) > (8 if l.startswith("#define") else 3))]


def files_to_check(paths):
    if not paths:
        out = subprocess.run(["git", "-c", "core.quotepath=off", "ls-files", "--cached", "--others", "--exclude-standard"],
                             cwd=ROOT, capture_output=True, text=True, check=True).stdout
        return [(ROOT / p, p) for p in out.splitlines() if p]
    found = []
    for p in map(Path, paths):
        if p.is_dir():
            found += [(f, f.relative_to(p).as_posix()) for f in sorted(p.rglob("*"))
                      if f.is_file() and ".git" not in f.relative_to(p).parts]
        else:
            found.append((p, p.name))
    return found


def blobs(rel, text):
    """(line, what) for each hex run of BLOB_BYTES or more, base64 run or \\x escape run."""
    lines = text.split("\n")
    i = 0
    while i < len(lines):
        size, j = 0, i
        while j < len(lines):
            code = COMMENT.sub("", lines[j])
            if not HEX_LINE.match(code):
                break
            size += sum((len(h) + 1) // 2 for h in HEX.findall(code))
            j += 1
        if size >= BLOB_BYTES:
            # the array holding the run: the nearest declaration above it
            decl = next((m.group(1) for k in range(i, max(i - 200, -1), -1) for m in [NAME.search(lines[k])] if m), "?")
            if (rel, decl) not in ALLOWED_BLOBS:
                yield i + 1, f"{size} bytes of hex literals ({decl})"
        i = max(j, i + 1)
    for pattern, what in ((BASE64, "base64"), (ESCAPES, "\\x escapes")):
        for m in pattern.finditer(text):
            if pattern is BASE64 and not re.search(r"[0-9+/]", m.group()):
                continue  # a long identifier, not data
            yield text.count("\n", 0, m.start()) + 1, f"{len(m.group())} characters of {what}"


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("paths", nargs="*", help="files or folders (default: the git working tree)")
    args = ap.parse_args()
    problems = []
    checked = 0
    for path, rel in files_to_check(args.paths):
        if not path.is_file():
            continue  # a submodule, or a tracked file deleted in the working tree
        checked += 1
        name = path.name.lower()
        if name in FORBIDDEN_NAMES or path.suffix.lower() in FORBIDDEN_EXTENSIONS:
            problems.append(f"{rel}: a Nintendo file name or type")
        data = path.read_bytes()
        for what, offset, magic in MAGICS:
            if data[offset:offset + len(magic)] == magic:
                problems.append(f"{rel}: looks like a {what} (file magic)")
        try:
            text = None if b"\0" in data[:8192] else data.decode("utf-8")
        except UnicodeDecodeError:
            text = None
        if text is None:
            if rel not in ALLOWED_BINARY:
                problems.append(f"{rel}: a binary file (allow-list ours in ALLOWED_BINARY, with the reason)")
            continue
        if path.suffix.lower() in TEXT_EXTENSIONS or path.suffix == "":
            problems += [f"{rel}:{line}: {what}" for line, what in blobs(rel, text)]
        if rel.startswith("native/assets/"):
            if not STUB_TREE.match(rel):
                problems.append(f"{rel}: not part of the stubs gen_assets.sh --stubs writes")
            else:
                problems += [f"{rel}:{line}: {what}" for line, what in stub_tree(rel, text)]
    if problems:
        print(f"check-no-nintendo-data: {len(problems)} problem(s):")
        for p in problems:
            print("  " + p)
        print("Game data comes from the player's disc (docs/RUNTIME_ASSETS.md); never commit it.")
        return 1
    print(f"check-no-nintendo-data: {checked} files checked, no Nintendo data")
    return 0


if __name__ == "__main__":
    sys.exit(main())
