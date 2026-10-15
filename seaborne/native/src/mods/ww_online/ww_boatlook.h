// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// ww_boatlook.cpp: the boats' palettes (big-endian RGB565, as in the "Ship" archive) and the local boat's colours.
#ifndef WW_BOATLOOK_H
#define WW_BOATLOOK_H

#include "dolphin/types.h"

struct ResTIMG;
class J3DTexture;

namespace boatlook {

const u32 kPalEntries = 0x100;  // the grid's draw loads 256 entries; the tail past numColors stays 0
const u32 kPalBytes = kPalEntries * 2;
const u32 kMaxRedHeaders = 8;   // a red model's TEX1 headers looked at
const u32 kKeySet = 0x01000000; // a look key: colour | kKeySet, 0 = vanilla

// The red parts (the hull, fn_body.bdl, and the head, fn_head_h.bdl: the hull colour) and the sail (new_ho1.bti:
// the tunic colour).
enum Part { kHull, kHead, kSail };
const Part kRedParts[] = {kHull, kHead};
int redModel(Part part); // the "Ship" BDL index of a red part

bool isPalette(const ResTIMG* t); // C8 with an RGB565 palette of at most kPalEntries
const u8* palOf(const ResTIMG* t);
// A red model's TEX1: the first C8 / RGB565 header (the red's texture), or NULL.
const ResTIMG* redRef(J3DTexture* tex);
// The red texture is listed twice in each model: every header with the reference's colours is the red's.
bool redSame(const ResTIMG* t, const ResTIMG* ref);
void redRecolor(u8* dst, const u8* src, u32 count, u16 c);
void sailRecolor(u8* dst, const u8* src, u32 count, u32 rgb);
// The palette to recolour a peer's part from: the vanilla copy while live shows the local boat's colour.
const u8* source(Part part, const u8* live);
// Once a frame: the live "Ship" palettes in the local boat's colours (tww_boat_set_local_look).
void tick();

} // namespace boatlook

#endif
