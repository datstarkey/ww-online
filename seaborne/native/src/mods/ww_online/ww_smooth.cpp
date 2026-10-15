// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// Peer motion from timed states (snapshot interpolation). The network brings a peer's state 20-30 times a second, at
// uneven times; drawing each as it comes moves the puppet in uneven steps. So every state is kept with the time it was
// sampled on the sender's clock, and each game frame the puppet and the boat are drawn at a time a little behind the
// newest (COS_MP_INTERP_MS, default 100 ms), between the two states around it.
//
// - The local clock is the game's: 1/30 s a game frame, so the drawn time moves the same amount every frame.
// - The sender's clock is mapped to it by the smallest (arrival - sent) of the last states: the quickest arrival, the
//   one the delay is counted from. When the arrivals spread wider than the delay (jitter), the spread is used instead,
//   so a slow network shows the peer later rather than in jerks. While that mapping moves (a quicker state came, the slowest went out of the window,
//   the clocks drift), the drawn time catches up at most 20% faster or slower than the game; past 0.5 s it jumps.
// - Past the newest state the motion carries on along the last two states' way for up to 150 ms, then holds.
// - The anims and look (tww_puppet_set_anim / _set_look) come with the states: they are kept with the same send times
//   and handed to the puppet when its drawn time reaches them, so it plays them where its body is.
#include "ww_smooth.h"
#include "ww_puppet.h"

#include "pc/pc_harness.h"

#include <algorithm>
#include <cmath>
#include <cstdlib>

WwoSmooth g_wwoSmoothPuppet[TWW_MAX_PEERS];
WwoSmooth g_wwoSmoothBoat[TWW_MAX_PEERS];

namespace {

const double kFrame = 1.0 / 30.0;
const double kMaxAhead = 0.15;   // carried on past the newest state this long at most
const double kMaxGap = 0.25;     // two states further apart than this give no way to carry on along
const double kResync = 0.5;      // the drawn time jumps when it is this far off
const double kCatchUp = 0.1;     // of the difference, a frame
const double kMaxCatchUp = 0.2;  // of a frame's time, a frame

double sSentAt[TWW_MAX_PEERS];

// A slot's anims or look by send time, oldest first.
template <typename T>
struct Late {
    struct Entry {
        double t;
        T v;
    };
    static const int kEntries = 32;
    Entry e[kEntries];
    int n = 0;

    void push(double t, const T& v) {
        if (n > 0 && t <= e[n - 1].t) {
            if (t == e[n - 1].t) {
                e[n - 1].v = v;
            }
            return;
        }
        if (n == kEntries) {
            std::copy(e + 1, e + kEntries, e);
            n--;
        }
        e[n++] = {t, v};
    }
    // the newest at or before t
    const T* at(double t) const {
        const T* v = nullptr;
        for (int i = 0; i < n && e[i].t <= t; i++) {
            v = &e[i].v;
        }
        return v;
    }
};
Late<TwwPuppetAnim> sAnims[TWW_MAX_PEERS];
Late<TwwPuppetLook> sLooks[TWW_MAX_PEERS];

double localNow() {
    return pc_frame_count() * kFrame;
}

double delay() {
    static double d = -1.0;
    if (d < 0.0) {
        const char* v = getenv("COS_MP_INTERP_MS");
        d = (v != nullptr ? std::clamp(atof(v), 0.0, 500.0) : 100.0) / 1000.0;
    }
    return d;
}

s16 lerpAngle(s16 a, s16 b, f32 f) {
    return (s16)(s32)(a + (s16)(b - a) * f);
}

} // namespace

double wwo_smoothSentAt(int slot) {
    return sSentAt[slot] != 0.0 ? sSentAt[slot] : localNow();
}

void WwoSmooth::push(double sentAt, const f32 pos[3], const s16 angle[3], f32 snapDist, bool snap) {
    if (mCount > 0) {
        const State& last = mState[mCount - 1];
        if (sentAt <= last.t) {
            return; // the same state again, or an older one
        }
        const f32 dx = pos[0] - last.pos[0], dy = pos[1] - last.pos[1], dz = pos[2] - last.pos[2];
        if (snap || dx * dx + dy * dy + dz * dz > snapDist * snapDist) {
            mCount = 0;
        }
    }
    if (mCount == kStates) {
        std::copy(mState + 1, mState + kStates, mState);
        mCount--;
    }
    State& s = mState[mCount++];
    s.t = sentAt;
    std::copy(pos, pos + 3, s.pos);
    std::copy(angle, angle + 3, s.angle);
    mLag[mLagNext] = localNow() - sentAt;
    mLagNext = (mLagNext + 1) % kLags;
    mLagCount = std::min(mLagCount + 1, kLags);
}

bool WwoSmooth::pose(f32 pos[3], s16 angle[3]) {
    if (mCount == 0) {
        return false;
    }
    const unsigned int frame = pc_frame_count();
    // behind the quickest arrival by the delay, or by the arrivals' spread if that is more (a slow network's jitter)
    const auto [quickest, slowest] = std::minmax_element(mLag, mLag + mLagCount);
    const double target = frame * kFrame - *quickest - std::max(delay(), *slowest - *quickest + kFrame);
    if (!mPlaying || std::fabs(target - mPlay) > kResync) {
        mPlay = target;
    } else {
        const double dt = (frame - mPlayFrame) * kFrame;
        mPlay += dt + std::clamp((target - mPlay - dt) * kCatchUp, -kMaxCatchUp * dt, kMaxCatchUp * dt);
    }
    mPlayFrame = frame;
    mPlaying = true;

    const State* a = &mState[0];
    const State* b = a;
    f32 f = 0.0f;
    if (mPlay >= mState[mCount - 1].t) {
        // past the newest: carried on along the last two states' way, briefly
        b = &mState[mCount - 1];
        a = mCount > 1 ? &mState[mCount - 2] : b;
        if (a != b && b->t - a->t <= kMaxGap) {
            f = (f32)(1.0 + std::min(mPlay - b->t, kMaxAhead) / (b->t - a->t));
        } else {
            a = b;
        }
    } else if (mPlay > mState[0].t) {
        int i = 1;
        while (mState[i].t <= mPlay) {
            i++;
        }
        a = &mState[i - 1];
        b = &mState[i];
        f = (f32)((mPlay - a->t) / (b->t - a->t));
    }
    for (int k = 0; k < 3; k++) {
        pos[k] = a->pos[k] + (b->pos[k] - a->pos[k]) * f;
        angle[k] = lerpAngle(a->angle[k], b->angle[k], f);
    }
    return true;
}

void WwoSmooth::clear() {
    mCount = 0;
    mLagCount = 0;
    mLagNext = 0;
    mPlaying = false;
}

void wwo_smoothHandLate(int slot) {
    const double t = g_wwoSmoothPuppet[slot].playTime();
    if (const TwwPuppetAnim* a = sAnims[slot].at(t)) {
        wwo_puppetSetAnim(slot, a);
    }
    if (const TwwPuppetLook* l = sLooks[slot].at(t)) {
        wwo_puppetSetLook(slot, l);
    }
}

extern "C" {

void tww_smooth_set_time(int slot, double sent_at) {
    if (slot >= 0 && slot < TWW_MAX_PEERS) {
        sSentAt[slot] = sent_at;
    }
}

// None (NULL, valid 0) goes on at once.
void tww_puppet_set_anim(int slot, const TwwPuppetAnim* anim) {
    if (slot < 0 || slot >= TWW_MAX_PEERS) {
        return;
    }
    if (anim == NULL || anim->valid == 0) {
        sAnims[slot].n = 0;
        wwo_puppetSetAnim(slot, NULL);
    } else {
        sAnims[slot].push(wwo_smoothSentAt(slot), *anim);
    }
}

void tww_puppet_set_look(int slot, const TwwPuppetLook* look) {
    if (slot < 0 || slot >= TWW_MAX_PEERS) {
        return;
    }
    if (look == NULL || look->valid == 0) {
        sLooks[slot].n = 0;
        wwo_puppetSetLook(slot, NULL);
    } else {
        sLooks[slot].push(wwo_smoothSentAt(slot), *look);
    }
}

} // extern "C"
