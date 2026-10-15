// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// d_a_wwo_boat.cpp / d_a_wwo_boat_move.cpp: a peer's King of Red Lions (see d_a_wwo_boat.cpp).
#ifndef D_A_WWO_BOAT_H
#define D_A_WWO_BOAT_H

#include "d/actor/d_a_ship.h"

#include "ww_boat.h"
#include "ww_boatlook.h"

class daGrid_c;

class daWwoBoat_c : public daShip_c {
public:
    cPhs_State create();
    BOOL createBoatHeap();
    void createCollision();
    BOOL execute();
    BOOL draw();
    BOOL remove();
    s16 pose(const TwwBoatState& s);
    void animate(const TwwBoatState& s);
    void calcRope();
    void placeSail();
    void setCollision();
    void setEffects(s16 roll);
    void color(const TwwBoatLook& look);
    J3DModel* redModel(boatlook::Part part) { return (part == boatlook::kHead ? mpHeadAnm : mpBodyAnm)->getModel(); }

    // A red model's own texture table (its TEX1 headers copied), the red headers on a palette of ours.
    struct RedLook {
        J3DTexture* tex;
        u32 red; // bit i: header i is the red's
        u8* pal; // two palettes: the GPU may still be loading the other
        u8 cur;
        u32 key;
    };
    bool createRed(boatlook::Part part);
    void colorRed(boatlook::Part part, u16 c);

    int mSlot;
    request_of_phase_process_class mClothPhs;
    bool mHasPose;
    bool mVisible;
    bool mParked;
    daGrid_c* mpSail;
    bool mSailCreated;
    RedLook mRed[2];        // kHull, kHead
    u8* mpSailPal;
    u8 mSailCur;
    u32 mSailKey;
};

#endif
