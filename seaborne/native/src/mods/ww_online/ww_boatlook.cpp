// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The boats' colours (the Dolphin client's REL: GameMod/src/puppet_link/puppet_boat.c "Hull colour" / "Sail" and
// puppet_boatlook.c). The hull's red is the fn_main1 palette of fn_body.bdl's TEX1, the head's the first palette of
// fn_head_h.bdl's (the Dolphin mod left the head red), the sail's canvas the new_ho1.bti palette, all C8 / RGB565.
// A recolour moves each palette entry toward the wanted colour by how much of the red (or the canvas) it is,
// keeping its shading; the wood, rope, trim, gold, eyes and the sail's emblem stay.
//
// The local boat is recoloured in place, so the local ship and its grid show it; the vanilla palettes are kept
// for the peers' boats (d_a_wwo_boat.cpp). Aurora caches a J3D TLUT by its object, so a red palette changed in place
// gets new texture objects (J3DTexture::resTIMGChanged); the grid's draw makes its texture objects each frame,
// which Aurora resolves by content.
#include "d/dolzel.h" // IWYU pragma: keep
#include "d/d_com_inf_game.h"
#include "pc/tww.h"
#include "res/Object/Ship.h"

#include "ww_boatlook.h"

#include <algorithm>
#include <cstring>

namespace boatlook {

namespace {

TwwBoatLook sLocalLook;

u16 palGet(const u8* pal, u32 i) {
    return (u16)(pal[i * 2] << 8 | pal[i * 2 + 1]);
}

void palSet(u8* pal, u32 i, u16 c) {
    pal[i * 2] = (u8)(c >> 8);
    pal[i * 2 + 1] = (u8)c;
}

u32 palSum(const u8* pal, u32 count) {
    u32 s = 0;
    for (u32 i = 0; i < count; i++) {
        s = s * 31 + palGet(pal, i);
    }
    return s;
}

// 0..256 as v goes 0..span
int ramp(int v, int span) {
    return v <= 0 ? 0 : v >= span ? 256 : v * 256 / span;
}

void unpack(u16 c, int ch[3]) {
    ch[0] = (c >> 11) & 0x1F;
    ch[1] = (c >> 5) & 0x3F;
    ch[2] = c & 0x1F;
    ch[0] = (ch[0] << 3) | (ch[0] >> 2);
    ch[1] = (ch[1] << 2) | (ch[1] >> 4);
    ch[2] = (ch[2] << 3) | (ch[2] >> 2);
}

// Each entry moved toward want (scaled by the entry's luminance over refLum) by its weight (0..256).
template <typename Weight>
void recolor(u8* dst, const u8* src, u32 count, const int want[3], int refLum, Weight weight) {
    for (u32 i = 0; i < count; i++) {
        int ch[3];
        unpack(palGet(src, i), ch);
        const int w = weight(ch);
        const int lum = (77 * ch[0] + 150 * ch[1] + 29 * ch[2]) >> 8;
        for (int k = 0; k < 3; k++) {
            const int t = std::min(want[k] * lum / refLum, 255);
            ch[k] += (t - ch[k]) * w / 256;
        }
        palSet(dst, i, (u16)((ch[0] >> 3) << 11 | (ch[1] >> 2) << 5 | ch[2] >> 3));
    }
}

J3DModelData* shipRes(int index) {
    if (dComIfG_getObjectResInfo("Ship") == nullptr) {
        return nullptr;
    }
    return (J3DModelData*)dComIfG_getObjectRes("Ship", index);
}

// One part's live palette and the vanilla copy of it; key = the colour written into it (0 = vanilla), sum = the
// live palette's after that write. A live palette that is not what we left there is new from disc: vanilla.
struct LookPart {
    const u8* live;
    u32 count;
    u32 key;
    u32 sum;
    u8 vanilla[kPalBytes];
};
LookPart sLook[3];

// Keeps pals[0..n) (the live palette and its identical copies, count entries) in want; true if written.
bool apply(Part part, u8* const* pals, u32 n, u32 count, u32 want) {
    LookPart& p = sLook[part];
    const u8* live = pals[0];
    if (p.live != live || p.count != count || palSum(live, count) != (p.key != 0 ? p.sum : palSum(p.vanilla, count))) {
        memset(p.vanilla, 0, sizeof(p.vanilla));
        memcpy(p.vanilla, live, count * 2);
        p.live = live;
        p.count = count;
        p.key = 0;
    }
    const u32 key = want != 0 ? (want | kKeySet) : 0;
    if (p.key == key) {
        return false;
    }
    for (u32 j = 0; j < n; j++) {
        if (want == 0) {
            memcpy(pals[j], p.vanilla, count * 2);
        } else if (part != kSail) {
            redRecolor(pals[j], p.vanilla, count, (u16)want);
        } else {
            sailRecolor(pals[j], p.vanilla, count, want);
        }
    }
    p.key = key;
    p.sum = palSum(live, count);
    return true;
}

} // namespace

bool isPalette(const ResTIMG* t) {
    return t->indexTexture && t->format == GX_TF_C8 && t->colorFormat == GX_TL_RGB565 && t->numColors != 0 &&
           t->numColors <= kPalEntries;
}

const u8* palOf(const ResTIMG* t) {
    return (const u8*)t + (s32)(u32)t->paletteOffset;
}

int redModel(Part part) {
    return part == kHead ? dRes_INDEX_SHIP_BDL_FN_HEAD_H_e : dRes_INDEX_SHIP_BDL_FN_BODY_e;
}

const ResTIMG* redRef(J3DTexture* tex) {
    for (u16 i = 0; i < std::min<u32>(tex->getNum(), kMaxRedHeaders); i++) {
        if (isPalette(tex->getResTIMG(i))) {
            return tex->getResTIMG(i);
        }
    }
    return nullptr;
}

bool redSame(const ResTIMG* t, const ResTIMG* ref) {
    return isPalette(t) && t->numColors == ref->numColors && memcmp(palOf(t), palOf(ref), ref->numColors * 2) == 0;
}

// The red (the hull's and head's main 197,60,24, luminance 97; g/r at most 0.62) takes c; wood, rope, trim and gold
// (g/r 0.72 and up) stay.
void redRecolor(u8* dst, const u8* src, u32 count, u16 c) {
    const int want[3] = {((c >> 11) & 0x1F) << 3, ((c >> 5) & 0x3F) << 2, (c & 0x1F) << 3};
    recolor(dst, src, count, want, 97, [](const int ch[3]) { return ramp(72 * ch[0] - 100 * ch[1], 10 * ch[0]); });
}

// new_ho1: the cream canvas (231,215,189, luminance 216) takes rgb; the teal emblem (canvas R-G >= 0, teal
// <= -70) and the orange corners (R-B: canvas <= 64, orange >= 96) stay.
void sailRecolor(u8* dst, const u8* src, u32 count, u32 rgb) {
    const int want[3] = {(int)(rgb >> 16 & 0xFF), (int)(rgb >> 8 & 0xFF), (int)(rgb & 0xFF)};
    recolor(dst, src, count, want, 216, [](const int ch[3]) {
        return ramp(ch[0] - ch[1] + 70, 70) * ramp(96 - (ch[0] - ch[2]), 32) / 256;
    });
}

const u8* source(Part part, const u8* live) {
    return sLook[part].live == live && sLook[part].key != 0 ? sLook[part].vanilla : live;
}

void tick() {
    for (Part part : kRedParts) {
        J3DModelData* data = shipRes(redModel(part));
        const ResTIMG* ref = data != nullptr ? redRef(data->getTexture()) : nullptr;
        if (ref == nullptr) {
            continue;
        }
        J3DTexture* tex = data->getTexture();
        const u16 n = std::min<u32>(tex->getNum(), kMaxRedHeaders);
        u8* pals[kMaxRedHeaders];
        u32 count = 1;
        pals[0] = (u8*)palOf(ref); // first: the vanilla copy is of it
        for (u16 i = 0; i < n; i++) {
            u8* pal = (u8*)palOf(tex->getResTIMG(i));
            if (redSame(tex->getResTIMG(i), ref) && std::find(pals, pals + count, pal) == pals + count) {
                pals[count++] = pal;
            }
        }
        if (apply(part, pals, count, ref->numColors, sLocalLook.hull_rgb565)) {
            for (u16 i = 0; i < n; i++) {
                if (std::find(pals, pals + count, palOf(tex->getResTIMG(i))) != pals + count) {
                    tex->resTIMGChanged(tex->getResTIMG(i));
                }
            }
        }
    }
    ResTIMG* timg = (ResTIMG*)shipRes(dRes_ID_SHIP_BTI_NEW_HO1_e);
    if (timg != nullptr && isPalette(timg)) {
        u8* pal = (u8*)palOf(timg);
        apply(kSail, &pal, 1, timg->numColors, sLocalLook.sail_rgb & 0xFFFFFF);
    }
}

} // namespace boatlook

extern "C" void tww_boat_set_local_look(const TwwBoatLook* look) {
    boatlook::sLocalLook = look != nullptr ? *look : TwwBoatLook{};
}
