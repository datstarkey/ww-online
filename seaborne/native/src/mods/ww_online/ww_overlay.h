// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The overlay's files (ww_overlay.cpp: name tags, the sea chart; ww_overlay_fx.cpp: projectiles) and what the
// rest of the mod calls in them.
#ifndef WW_OVERLAY_H
#define WW_OVERLAY_H

#include "dolphin/types.h"
#include "pc/tww.h"

class daBomb_c;
class dDlst_FMAP_c;
class J2DOrthoGraph;

// A friend of dMenu_Fmap_c and daBomb_c (TARGET_PC): their private members the overlay needs.
struct WwoOverlay {
    // g_dDlst_FMAP_afterDraw: the other players on the chart, after its screen has drawn.
    static void drawSeaChart(dDlst_FMAP_c* dl, J2DOrthoGraph* graf);
    // A peer's bomb copy: it never hits the player group and is not the local Link's.
    static void makeCopy(daBomb_c* bomb);
    static s16 noGravityTime(const daBomb_c* bomb);
    // ww_pvp.cpp: a peer's copy is never the local Link's attack.
    static bool isCopy(const daBomb_c* bomb);
};

// From wwo_frame, before fapGm_Execute: the peers' projectiles in, the local Link's out.
void wwo_overlayFrame();
// COS_MP_TEST_PEER: slot 0's name and sea chart marker, a bomb thrown by it every 300 frames, and the local
// Link's projectiles echoed back as its own, from its place.
void wwo_overlayTestFeed(const TwwLocalState& local, const TwwPeerState& peer);

#endif
