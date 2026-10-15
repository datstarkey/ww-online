#!/usr/bin/env python3
"""Writes the data-array asset headers our decompilation keeps inline in its source.

The decompilation (game/, from our 100%-linked tree) extracted some disc data straight into its
source: textures, palettes, display lists and vertex tables (d_a_mant.cpp,
d_a_obj_buoyflag_data.inc), the JAudio DSP microcode (dsptask.c), the system font
(JUTFontData_Ascfont_fix12.cpp), the debug print font (JUTDirectPrint.cpp) and three SDK blobs
(OSAudioSystem.c, CARDUnlock.c, GBAJoyBoot.c). Those bytes come from the disc, so this repository's
copies of those files #include "assets/<name>__<unit>.h" instead, and this script writes the headers
from the decompilation's split objects (the dtk split of the player's disc that
native/tools/gen_assets.sh runs), with the same declaration the source had. ENTRIES is also what
gen_asset_stubs.py turns into stubs and loader rows for a COS_RUNTIME_ASSETS build.

  extract_data_assets.py --decomp build/decomp --version GZLE01 --out <assets dir>/include/assets
"""
import argparse
import os
import re
import struct
import sys

# (split object under build/<version>/, symbol, nth symbol of that name in address order,
#  declaration, header name)
ENTRIES = [
    ("d_a_mant/obj/d/actor/d_a_mant.o", s, 0, d, s + "__d_a_mant")
    for s, d in [
        ("l_pg_mantle1TEX", "static u8 l_pg_mantle1TEX[] ATTRIBUTE_ALIGN(32)"),
        ("l_pg_mantle1_palettePAL", "static u8 l_pg_mantle1_palettePAL[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_boro_b1TEX", "static u8 l_tn_boro_b1TEX[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_boro_b1_palettePAL", "static u8 l_tn_boro_b1_palettePAL[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_boro_k1TEX", "static u8 l_tn_boro_k1TEX[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_boro_k1_palettePAL", "static u8 l_tn_boro_k1_palettePAL[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_boro1TEX", "static u8 l_tn_boro1TEX[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_boro1_palettePAL", "static u8 l_tn_boro1_palettePAL[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_mantle1TEX", "static u8 l_tn_mantle1TEX[] ATTRIBUTE_ALIGN(32)"),
        ("l_tn_mantle1_palettePAL", "static u8 l_tn_mantle1_palettePAL[] ATTRIBUTE_ALIGN(32)"),
        ("l_pos", "static u8 l_pos[]"),
        ("l_texCoord", "static u8 l_texCoord[]"),
        ("l_DL", "static u8 l_DL[] ATTRIBUTE_ALIGN(32)"),
        ("l_matDL", "static u8 l_matDL[] ATTRIBUTE_ALIGN(32)"),
        ("l_matDL2", "static u8 l_matDL2[] ATTRIBUTE_ALIGN(32)"),
        ("l_matDL_LIGHTOFF", "static u8 l_matDL_LIGHTOFF[] ATTRIBUTE_ALIGN(32)"),
    ]
] + [
    ("d_a_obj_buoyflag/obj/d/actor/d_a_obj_buoyflag.o", s, n, d, h + "__d_a_obj_buoyflag")
    for s, n, d, h in [
        ("l_k_taru02TEX", 0, "static u8 l_k_taru02TEX[] ATTRIBUTE_ALIGN(32)", "l_k_taru02TEX"),
        ("l_pos", 0, "static u32 l_pos[]", "l_pos_Khasi"),
        ("l_texCoord", 0, "static u32 l_texCoord[]", "l_texCoord_Khasi"),
        ("l_Khasi_00DL", 0, "static u8 l_Khasi_00DL[] ATTRIBUTE_ALIGN(32)", "l_Khasi_00DL"),
        ("l_k_hata01TEX", 0, "static u8 l_k_hata01TEX[] ATTRIBUTE_ALIGN(32)", "l_k_hata01TEX"),
        ("l_pos", 1, "static u32 l_pos[]", "l_pos_Khata"),
        ("l_texCoord", 1, "static u32 l_texCoord[]", "l_texCoord_Khata"),
        ("l_Khata_00DL", 0, "static u8 l_Khata_00DL[] ATTRIBUTE_ALIGN(32)", "l_Khata_00DL"),
        ("l_matDL", 0, "static u8 l_matDL[] ATTRIBUTE_ALIGN(32)", "l_matDL"),
    ]
] + [
    ("obj/" + o + ".o", s, 0, d, s + "__" + o.rsplit("/", 1)[1])
    for o, s, d in [
        ("JSystem/JAudio/dsptask", "jdsp", "static u8 jdsp[]"),
        # GX reads the glyph page in place, as a texture: 32-byte aligned as on the GameCube
        ("JSystem/JUtility/JUTFontData_Ascfont_fix12", "JUTResFONT_Ascfont_fix12",
         "u8 JUTResFONT_Ascfont_fix12[] ATTRIBUTE_ALIGN(32)"),
        ("JSystem/JUtility/JUTDirectPrint", "sFontData", "u32 JUTDirectPrint::sFontData[]"),
        ("JSystem/JUtility/JUTDirectPrint", "sFontData2", "u32 JUTDirectPrint::sFontData2[]"),
        # not compiled on PC (cos_sdk replaces the SDK): only the decompilation's own build reads these
        ("dolphin/os/OSAudioSystem", "DSPInitCode", "static u8 DSPInitCode[]"),
        ("dolphin/card/CARDUnlock", "CardData", "static u8 CardData[] ATTRIBUTE_ALIGN(32)"),
        ("dolphin/gba/GBAJoyBoot", "D54", "static volatile u8 D54[]"),
    ]
]


def is_words(decl):
    """A u32 array: written as numbers, so host-endian once compiled (the loader swaps it)."""
    return re.search(r"\bu32\b", decl.split("[")[0]) is not None


def elf_symbols(path):
    """{name: [(section index, offset, bytes)...]} in address order, for a 32-bit big-endian ELF.
    A label (no size: JUTResFONT_Ascfont_fix12) runs to the next symbol or its section's end."""
    data = open(path, "rb").read()
    if data[:4] != b"\x7fELF" or data[4] != 1 or data[5] != 2:
        sys.exit("extract_data_assets: %s is not a 32-bit big-endian ELF" % path)
    shoff, = struct.unpack_from(">I", data, 0x20)
    shentsize, shnum, shstrndx = struct.unpack_from(">HHH", data, 0x2E)
    secs = [struct.unpack_from(">IIIIIIIIII", data, shoff + i * shentsize) for i in range(shnum)]
    syms = []
    for sh in secs:
        if sh[1] != 2:  # SHT_SYMTAB
            continue
        strtab = secs[sh[6]]
        for off in range(sh[4], sh[4] + sh[5], 16):
            name_o, value, size, info, other, shndx = struct.unpack_from(">IIIBBH", data, off)
            # STT_OBJECT, or a STT_NOTYPE label, in a section with contents (SHT_PROGBITS)
            if shndx == 0 or shndx >= len(secs) or (info & 0xF) not in (0, 1) or secs[shndx][1] != 1:
                continue
            n = data[strtab[4] + name_o:data.index(b"\0", strtab[4] + name_o)].decode()
            if n:
                syms.append((n, shndx, value, size))
    found = {}
    for n, shndx, value, size in syms:
        sec = secs[shndx]
        if size == 0:
            size = min([v for _, i, v, _ in syms if i == shndx and v > value] + [sec[5]]) - value
        found.setdefault(n, []).append((shndx, value, data[sec[4] + value:sec[4] + value + size]))
    for v in found.values():
        v.sort(key=lambda e: (e[0], e[1]))
    return found


def find(symbols, sym, nth):
    """The nth symbol named sym (or sym__<mangled class or namespace>) in address order, or None."""
    hits = sorted((e for n, v in symbols.items() if n == sym or n.startswith(sym + "__") for e in v),
                  key=lambda e: (e[0], e[1]))
    return hits[nth] if nth < len(hits) else None


def render(decl, raw):
    if is_words(decl):
        words = struct.unpack(">%dI" % (len(raw) // 4), raw)
        items = ["0x%08X" % w for w in words]
        per = 8
    else:
        items = ["0x%02X" % b for b in raw]
        per = 16
    lines = ["    " + ", ".join(items[i:i + per]) + "," for i in range(0, len(items), per)]
    return "%s = {\n%s\n};\n" % (decl, "\n".join(lines))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--decomp", required=True)
    ap.add_argument("--version", default="GZLE01")
    ap.add_argument("--out", required=True)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    cache = {}
    for obj, sym, nth, decl, name in ENTRIES:
        path = os.path.join(a.decomp, "build", a.version, obj)
        if path not in cache:
            if not os.path.isfile(path):
                sys.exit("extract_data_assets: %s: missing (run the decompilation's split first)" % path)
            cache[path] = elf_symbols(path)
        # Symbols inside namespaces keep their mangled suffix (l_pos__Q213daObjBuoyflag...).
        hit = find(cache[path], sym, nth)
        if hit is None:
            sys.exit("extract_data_assets: %s: no symbol %s #%d" % (obj, sym, nth))
        raw = hit[2]
        with open(os.path.join(a.out, name + ".h"), "w") as f:
            f.write("// Generated from the player's disc by native/tools/extract_data_assets.py; not in git.\n")
            f.write(render(decl, raw))
    print("extract_data_assets: %d headers in %s" % (len(ENTRIES), a.out))


if __name__ == "__main__":
    main()
