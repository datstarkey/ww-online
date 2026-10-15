// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// Peer motion from timed states (ww_smooth.cpp): each slot's puppet and boat are drawn a little behind the newest
// state the network brought, between the two states around that time, so they move evenly whatever the send rate and
// the arrival jitter.
#ifndef WW_SMOOTH_H
#define WW_SMOOTH_H

#include "dolphin/types.h"
#include "pc/tww.h"

class WwoSmooth {
public:
    // A state sent at sentAt (seconds, the sender's clock). Only a newer one than the last is kept; one further than
    // snapDist from it, or with snap set, drops the older ones (a teleport: it is drawn there at once).
    void push(double sentAt, const f32 pos[3], const s16 angle[3], f32 snapDist, bool snap);
    // The pose to draw this frame (call once a frame); false while there is no state.
    bool pose(f32 pos[3], s16 angle[3]);
    void clear();
    // The sender's time pose drew last.
    double playTime() const { return mPlay; }

private:
    struct State {
        double t;
        f32 pos[3];
        s16 angle[3];
    };
    static const int kStates = 32;
    static const int kLags = 64;

    State mState[kStates]; // oldest first
    int mCount = 0;
    double mLag[kLags]; // arrival time - sent time of the last kLags states
    int mLagCount = 0;
    int mLagNext = 0;
    double mPlay = 0.0; // the sender's time drawn
    unsigned int mPlayFrame = 0;
    bool mPlaying = false;
};

// slot i's puppet and boat (tww_set_peer, tww_boat_set_peer)
extern WwoSmooth g_wwoSmoothPuppet[TWW_MAX_PEERS];
extern WwoSmooth g_wwoSmoothBoat[TWW_MAX_PEERS];

// The puppet's execute, after its pose: hands the slot's anims and look at its drawn time to the puppet.
void wwo_smoothHandLate(int slot);

// The send time tww_smooth_set_time gave slot, else the local clock (the state stamped on arrival).
double wwo_smoothSentAt(int slot);

#endif
