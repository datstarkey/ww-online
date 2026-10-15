// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// Other players' bombs, boat cannonballs and arrows made real here, and the local Link's reported (the Dolphin
// client's REL: GameMod/src/puppet_link/puppet_fx.c, whose header comment gives the rules in full).
//
// SENDER (with tww_overlay_set_event_send on): seen from wwo_frame, before the frame's executes. A hand bomb (a
// daBomb_c in the local Link's grab, or a Bomb Flower's daBomb2::Act_c) is reported when it leaves his hands
// (BOMB_THROW), when he carries it again (BOMB_PICKUP), when it explodes, in his hands too (EXPLODE), or when it is
// gone without exploding (REMOVE); the boat's cannonball the frame the ship fires (CANNON), then EXPLODE / REMOVE;
// a nocked arrow once it left the bow with the shot's param 1 (ARROW).
// VIEWER (tww_overlay_push_peer_event): a thrown bomb is a lit daBomb_c with the sender's velocity and fuse, which
// holds its fuse until the sender's EXPLODE arrives and then explodes where theirs did, unless this world took it
// over (carried it, swallowed it, set its fuse); a cannonball is the ship's own STATE_4 bomb; an arrow is a
// daArrow_c in flight from the sender's launch point. Copies carry kCopyPrm (never reported back), hit nothing in
// the player group, are not the local Link's bombs, and live in the player's layer: they die with the stage.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_iter.h"
#include "f_op/f_op_actor_mng.h"
#include "f_pc/f_pc_layer.h"
#include "d/actor/d_a_arrow.h"
#include "d/actor/d_a_bomb.h"
#include "d/actor/d_a_bomb2.h"
#include "d/actor/d_a_player_main.h"
#include "d/actor/d_a_ship.h"
#include "d/d_com_inf_game.h"
#include "m_Do/m_Do_mtx.h"

#include "ww_online_internal.h"
#include "ww_overlay.h"

#include <cstddef>
#include <cstdio>

static_assert(sizeof(TwwOverlayEvent) == 40 && offsetof(TwwOverlayEvent, id) == 4 &&
              offsetof(TwwOverlayEvent, pos) == 8 && offsetof(TwwOverlayEvent, speed_f) == 20 &&
              offsetof(TwwOverlayEvent, gravity) == 28 && offsetof(TwwOverlayEvent, angle) == 32 &&
              offsetof(TwwOverlayEvent, timer) == 38, "TwwOverlayEvent: the C# binding's layout");

namespace {

const int kTrackMax = 6;      // the local Link's bombs and cannonballs out (at most 3 bombs)
const int kCopyMax = 8;
const int kEventsPerFrame = 4;
const u32 kQueueMax = 16;
const s16 kMinFuse = 3;       // a thrown copy's fuse at least kHoldFuse, else its first frame would look game-set
const s16 kHoldFuse = 3;      // a held copy's rest time: its execute counts it to 2; anything lower was the game
const s16 kMaxFuse = 600;
const s16 kExplodeGrace = 60; // frames past the sender's fuse a copy waits for their EXPLODE
const s16 kMaxNoGravity = 60;
const u16 kLeaveFrames = 30;  // REMOVE: the copy gets this long to sink here too
const u16 kDoneFrames = 300;  // a finished copy is remembered this long, so a late duplicate can't respawn it
const f32 kMaxCoord = 1.0e6f;
const f32 kMaxSpeed = 1000.0f;
const f32 kMaxGravity = 50.0f;
const u32 kCopyPrm = 0x01000000; // a free bit of daBomb_c's params
const u8 kArrowTypeMax = 3;      // light
const u32 kArrowShotParam = 1;   // fopAcM_SetParam(arrow, 1) at the shot (d_a_player_bow.inc)
const u32 kBombStateMask = 0xFF;

enum : u8 { TRACK_FREE, TRACK_CARRIED, TRACK_FLYING };
enum : u8 { COPY_FREE, COPY_LIVE, COPY_LEAVING, COPY_EXPLODED, COPY_GONE };

struct Track {
    fpc_ProcID pid;
    s16 name; // fpcNm_BOMB_e or fpcNm_Bomb2_e (a Bomb Flower's)
    u8 state;
    s8 room;  // when last seen, for REMOVE
};

struct Copy {
    u32 id;          // the sender's projectile
    fpc_ProcID pid;  // ours
    u16 age;         // frames in this state
    s16 hold;        // a LIVE hand bomb's fuse is held while age < hold
    u8 slot;
    u8 state;
    bool cannon;     // no fuse
    bool local;      // this world took it over: its own fuse ends it
};

struct Queue {
    TwwOverlayEvent e[kQueueMax];
    u32 read = 0;
    u32 write = 0;

    bool push(const TwwOverlayEvent& ev) {
        if (write - read >= kQueueMax) {
            return false;
        }
        e[write++ % kQueueMax] = ev;
        return true;
    }
    bool pop(TwwOverlayEvent* out) {
        if (read == write) {
            return false;
        }
        *out = e[read++ % kQueueMax];
        return true;
    }
};

Track sTrack[kTrackMax];
Copy sCopy[kCopyMax];
Queue sIn;
Queue sOut;
fpc_ProcID sArrowId = fpcM_ERROR_PROCESS_ID_e; // the local Link's nocked arrow
bool sSend;

daPy_lk_c* linkPlayer() {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    return player != NULL && fopAcM_SearchByName(fpcNm_PLAYER_e) == player ? static_cast<daPy_lk_c*>(player) : NULL;
}

// A live actor of that kind, or NULL (gone, being deleted, still being created, another kind).
fopAc_ac_c* find(fpc_ProcID pid, s16 name) {
    if (pid == fpcM_ERROR_PROCESS_ID_e) {
        return NULL;
    }
    fopAc_ac_c* a = fopAcM_SearchByID(pid);
    return a != NULL && static_cast<base_process_class*>(a)->mInitState != 3 && fopAcM_GetName(a) == name ? a : NULL;
}

daBomb_c* findBomb(fpc_ProcID pid) {
    return static_cast<daBomb_c*>(find(pid, fpcNm_BOMB_e));
}

// Frames left on a tracked bomb's fuse, 0 once it explodes (either kind).
s16 fuseOf(fopAc_ac_c* a) {
    if (fopAcM_GetName(a) != fpcNm_Bomb2_e) {
        return static_cast<daBomb_c*>(a)->getBombRestTime();
    }
    daBomb2::Act_c* flower = static_cast<daBomb2::Act_c*>(a);
    const int time = flower->get_time();
    return flower->chk_explode() || time <= 0 ? 0 : (s16)(time > 0x7FFF ? 0x7FFF : time);
}

void emit(u8 kind, fpc_ProcID id, fopAc_ac_c* ac, const cXyz& at, u8 variant, s8 room) {
    TwwOverlayEvent e = {};
    e.kind = kind;
    e.variant = variant;
    e.id = id;
    const cXyz& pos = ac != NULL ? ac->current.pos : at;
    e.pos[0] = pos.x;
    e.pos[1] = pos.y;
    e.pos[2] = pos.z;
    e.room = ac != NULL ? fopAcM_GetRoomNo(ac) : room;
    if (ac != NULL) {
        e.speed_f = ac->speedF;
        e.speed_y = ac->speed.y;
        e.gravity = ac->gravity;
        e.angle[0] = ac->current.angle.x;
        e.angle[1] = ac->current.angle.y;
        e.angle[2] = ac->shape_angle.z;
        if (kind == TWW_OVERLAY_EVENT_CANNON) {
            e.timer = WwoOverlay::noGravityTime(static_cast<daBomb_c*>(ac));
        } else if (kind != TWW_OVERLAY_EVENT_ARROW) {
            e.timer = fuseOf(ac);
        }
    }
    if (!sOut.push(e)) {
        fprintf(stderr, "[wwo] fx: local event %u dropped (not polled)\n", kind);
    }
}

Track* track(fpc_ProcID pid) {
    for (Track& t : sTrack) {
        if (t.state != TRACK_FREE && t.pid == pid) {
            return &t;
        }
    }
    return NULL;
}

Track* trackAdd(fpc_ProcID pid, s16 name, u8 state) {
    for (Track& t : sTrack) {
        if (t.state == TRACK_FREE) {
            t = Track{pid, name, state, -1};
            return &t;
        }
    }
    return NULL; // full: this one isn't shared
}

// A lit hand bomb of ours: STATE_1..3, not a copy.
bool isHandBomb(daBomb_c* b) {
    const u32 prm = fopAcM_GetParam(b);
    const u32 state = prm & kBombStateMask;
    return (prm & kCopyPrm) == 0 && state >= daBomb_c::STATE_1 && state <= daBomb_c::STATE_3 && b->getBombRestTime() > 0;
}

// fopAcIt_Judge: the boat's new cannonball (the ship's exact params: never a copy or an enemy cannon's).
void* judgeCannon(void* proc, void* prm) {
    fopAc_ac_c* a = static_cast<fopAc_ac_c*>(proc);
    if (fopAcM_GetName(a) != fpcNm_BOMB_e || fopAcM_GetParam(a) != *static_cast<u32*>(prm) ||
        static_cast<base_process_class*>(a)->mInitState == 3 || static_cast<daBomb_c*>(a)->getBombRestTime() == 0 ||
        track(fopAcM_GetID(a)) != NULL) {
        return NULL;
    }
    return a;
}

void trackLocal(daPy_lk_c* link) {
    const fpc_ProcID grabId = link->getGrabActorID();
    if (grabId != fpcM_ERROR_PROCESS_ID_e && track(grabId) == NULL) {
        daBomb_c* bomb = findBomb(grabId);
        if (bomb != NULL && isHandBomb(bomb)) {
            trackAdd(grabId, fpcNm_BOMB_e, TRACK_CARRIED);
        }
        // A Bomb Flower's bomb, lit when picked: the viewers' copy is a plain bomb.
        fopAc_ac_c* flower = bomb == NULL ? find(grabId, fpcNm_Bomb2_e) : NULL;
        if (flower != NULL && fuseOf(flower) > 0) {
            trackAdd(grabId, fpcNm_Bomb2_e, TRACK_CARRIED);
        }
    }

    // The nocked arrow is no longer in the bow: shot (param 1), or put away.
    const fpc_ProcID equipId = link->getItemID();
    fopAc_ac_c* nocked = find(equipId, fpcNm_ARROW_e);
    if (sArrowId != fpcM_ERROR_PROCESS_ID_e && (nocked == NULL || equipId != sArrowId)) {
        daArrow_c* shot = static_cast<daArrow_c*>(find(sArrowId, fpcNm_ARROW_e));
        if (shot != NULL && fopAcM_GetParam(shot) == kArrowShotParam && shot->mArrowType <= kArrowTypeMax) {
            emit(TWW_OVERLAY_EVENT_ARROW, sArrowId, shot, shot->current.pos, shot->mArrowType, 0);
        }
    }
    sArrowId = nocked != NULL ? equipId : fpcM_ERROR_PROCESS_ID_e;

    // The ship sets SHOOT_CANNON the frame it fires and clears it at its next execute.
    daShip_c* ship = static_cast<daShip_c*>(fopAcM_SearchByName(fpcNm_SHIP_e));
    if (ship != NULL && ship->checkShootCannon()) {
        u32 prm = daBomb_c::prm_make(daBomb_c::STATE_4, false, true);
        fopAc_ac_c* ball = static_cast<fopAc_ac_c*>(fopAcIt_Judge(judgeCannon, &prm));
        if (ball != NULL && trackAdd(fopAcM_GetID(ball), fpcNm_BOMB_e, TRACK_FLYING) != NULL) {
            emit(TWW_OVERLAY_EVENT_CANNON, fopAcM_GetID(ball), ball, ball->current.pos, 0, 0);
        }
    }

    for (Track& t : sTrack) {
        if (t.state == TRACK_FREE) {
            continue;
        }
        fopAc_ac_c* a = find(t.pid, t.name);
        if (a == NULL) {
            // Gone without exploding (sank, the stage ended). A carried one had no copy.
            if (t.state == TRACK_FLYING) {
                emit(TWW_OVERLAY_EVENT_REMOVE, t.pid, NULL, link->current.pos, 0, t.room);
            }
            t.state = TRACK_FREE;
            continue;
        }
        t.room = fopAcM_GetRoomNo(a);
        if (fuseOf(a) == 0) {
            emit(TWW_OVERLAY_EVENT_EXPLODE, t.pid, a, a->current.pos, 0, 0);
            t.state = TRACK_FREE;
            continue;
        }
        const bool carried = grabId == t.pid;
        if (t.state == TRACK_CARRIED && !carried) {
            emit(TWW_OVERLAY_EVENT_BOMB_THROW, t.pid, a, a->current.pos, 0, 0);
            t.state = TRACK_FLYING;
        } else if (t.state == TRACK_FLYING && carried) {
            emit(TWW_OVERLAY_EVENT_BOMB_PICKUP, t.pid, a, a->current.pos, 0, 0);
            t.state = TRACK_CARRIED;
        }
    }
}

Copy* copyOf(u8 slot, u32 id) {
    for (Copy& c : sCopy) {
        if (c.state != COPY_FREE && c.slot == slot && c.id == id) {
            return &c;
        }
    }
    return NULL;
}

// A free entry, else the oldest finished one; NULL while every entry is a copy still in the air.
Copy* copyAlloc(u8 slot, u32 id) {
    Copy* best = NULL;
    for (Copy& c : sCopy) {
        if (c.state == COPY_FREE) {
            best = &c;
            break;
        }
        if ((c.state == COPY_EXPLODED || c.state == COPY_GONE) && (best == NULL || c.age > best->age)) {
            best = &c;
        }
    }
    if (best != NULL) {
        *best = Copy{id, fpcM_ERROR_PROCESS_ID_e, 0, 0, slot, COPY_GONE, false, false};
    }
    return best;
}

// From the player's layer, as the player's own bombs and arrows are: the copy dies with the stage.
void* create(daPy_lk_c* link, s16 name, u32 prm, cXyz* pos, csXyz* angle) {
    layer_class* saved = fpcLy_CurrentLayer();
    fpcLy_SetCurrentLayer(static_cast<base_process_class*>(link)->mLyTg.mpLayer);
    void* a = fopAcM_fastCreate(name, prm, pos, -1, angle);
    fpcLy_SetCurrentLayer(saved);
    return a;
}

daBomb_c* spawnBomb(daPy_lk_c* link, u32 prm, cXyz* pos, csXyz* angle) {
    daBomb_c* b = static_cast<daBomb_c*>(create(link, fpcNm_BOMB_e, prm | kCopyPrm, pos, angle));
    if (b != NULL) {
        WwoOverlay::makeCopy(b);
    }
    return b;
}

// Where the sender's projectile is, moving as it moves.
void place(fopAc_ac_c* a, const TwwOverlayEvent& e) {
    a->current.pos.set(e.pos[0], e.pos[1], e.pos[2]);
    a->old.pos = a->current.pos;
    a->speedF = e.speed_f;
    a->speed.y = e.speed_y;
    a->gravity = e.gravity;
    a->current.angle.set(e.angle[0], e.angle[1], e.angle[2]);
    a->shape_angle.y = e.angle[1];
}

// A real arrow in flight, detached from the local Link: no parent, the peer's type in m_keep_type and the local
// magic raised around the create only (a light arrow needs 2 to be made one); its CO is off (a stuck arrow would
// give the local Link +1 arrow); procMove and arrowShooting as procWait does at the shot. Its param stays 0, so
// procWait (which spends magic) never runs.
bool spawnArrow(daPy_lk_c* link, const TwwOverlayEvent& e) {
    const u8 keepType = daArrow_c::m_keep_type;
    const u8 magic = dComIfGs_getMagic();
    cXyz pos(e.pos[0], e.pos[1], e.pos[2]);
    csXyz aim(e.angle[0], e.angle[1], e.angle[2]);
    daArrow_c::m_keep_type = e.variant;
    if (magic < 2) {
        dComIfGs_setMagic(2);
    }
    daArrow_c* a = static_cast<daArrow_c*>(create(link, fpcNm_ARROW_e, 0, &pos, &aim));
    dComIfGs_setMagic(magic);
    daArrow_c::m_keep_type = keepType;
    if (a == NULL || a->mpModel == NULL) {
        return false;
    }
    a->current.pos = pos;
    a->old.pos = pos;
    a->current.angle.set(e.angle[0], e.angle[1], 0); // up is +x, as setKeepMatrix sets it
    a->shape_angle.set(-e.angle[0], e.angle[1], 0);
    a->mCoSph.OffCoSPrmBit(cCcD_CoSPrm_Set_e);
    a->mCurrProcFunc = &daArrow_c::procMove;
    a->arrowShooting();
    // The model is still in the local Link's hand until its first procMove: draw it at the launch point.
    MtxP m = a->mpModel->getBaseTRMtx();
    MTXTrans(m, pos.x, pos.y, pos.z);
    mDoMtx_ZXYrotM(m, -e.angle[0], e.angle[1], 0);
    return true;
}

bool inRange(f32 v, f32 limit) {
    return v > -limit && v < limit; // false for NaN and infinity
}

bool valid(const TwwOverlayEvent& e) {
    return e.kind >= TWW_OVERLAY_EVENT_BOMB_THROW && e.kind <= TWW_OVERLAY_EVENT_ARROW && e.slot < TWW_MAX_PEERS &&
           e.variant <= (e.kind == TWW_OVERLAY_EVENT_ARROW ? kArrowTypeMax : 0) && inRange(e.pos[0], kMaxCoord) &&
           inRange(e.pos[1], kMaxCoord) && inRange(e.pos[2], kMaxCoord) && inRange(e.speed_f, kMaxSpeed) &&
           inRange(e.speed_y, kMaxSpeed) && inRange(e.gravity, kMaxGravity);
}

s16 clamp(s16 v, s16 lo, s16 hi) {
    return v < lo ? lo : (v > hi ? hi : v);
}

void refused(const TwwOverlayEvent& e) {
    fprintf(stderr, "[wwo] fx: slot %u event %u #%u refused (no room for a copy, or the create failed)\n", e.slot,
            e.kind, e.id);
}

void apply(daPy_lk_c* link, const TwwOverlayEvent& e) {
    Copy* c = copyOf(e.slot, e.id);
    daBomb_c* a = c != NULL ? findBomb(c->pid) : NULL;
    cXyz pos(e.pos[0], e.pos[1], e.pos[2]);
    csXyz angle(e.angle[0], e.angle[1], e.angle[2]);
    switch (e.kind) {
    case TWW_OVERLAY_EVENT_BOMB_THROW:
        if (c != NULL && (c->state == COPY_EXPLODED || c->state == COPY_LEAVING || (a != NULL && c->local))) {
            return; // late, or this world's bomb now
        }
        if (a == NULL) {
            c = c != NULL ? c : copyAlloc(e.slot, e.id);
            a = c != NULL ? spawnBomb(link, daBomb_c::prm_make(daBomb_c::STATE_1, false, false), &pos, &angle) : NULL;
            if (a == NULL) {
                if (c != NULL) {
                    c->state = COPY_FREE;
                }
                refused(e);
                return;
            }
            c->pid = fopAcM_GetID(a);
            c->cannon = false;
            c->local = false;
        }
        place(a, e);
        a->setBombRestTime(clamp(e.timer, kMinFuse, kMaxFuse));
        c->hold = a->getBombRestTime() + kExplodeGrace;
        c->state = COPY_LIVE;
        c->age = 0;
        fprintf(stderr, "[wwo] fx: slot %u bomb #%u thrown at (%.0f %.0f %.0f), fuse %d (process %u)\n", e.slot, e.id,
                e.pos[0], e.pos[1], e.pos[2], a->getBombRestTime(), (unsigned)c->pid);
        return;

    case TWW_OVERLAY_EVENT_CANNON:
        if (c != NULL) {
            return; // a repeat
        }
        c = copyAlloc(e.slot, e.id);
        a = c != NULL ? spawnBomb(link, daBomb_c::prm_make(daBomb_c::STATE_4, false, true), &pos, &angle) : NULL;
        if (a == NULL) {
            if (c != NULL) {
                c->state = COPY_FREE;
            }
            refused(e);
            return;
        }
        place(a, e);
        a->setNoGravityTime(clamp(e.timer, 0, kMaxNoGravity));
        c->pid = fopAcM_GetID(a);
        c->cannon = true;
        c->state = COPY_LIVE;
        c->age = 0;
        fprintf(stderr, "[wwo] fx: slot %u cannonball #%u fired from (%.0f %.0f %.0f) (process %u)\n", e.slot, e.id,
                e.pos[0], e.pos[1], e.pos[2], (unsigned)c->pid);
        return;

    case TWW_OVERLAY_EVENT_BOMB_PICKUP:
        // The puppet carries it again.
        if (c != NULL && a != NULL && c->state == COPY_LIVE && !c->local && !fopAcM_checkCarryNow(a) &&
            a->getBombRestTime() != 0) {
            fopAcM_delete(a);
            c->state = COPY_FREE;
        }
        return;

    case TWW_OVERLAY_EVENT_EXPLODE:
        if ((c != NULL && c->state == COPY_EXPLODED) || (a != NULL && c->local)) {
            return; // done, or this world's bomb now
        }
        if (a != NULL && a->getBombRestTime() != 0) {
            a->current.pos = pos;
            a->old.pos = pos;
            a->procExplode_init();
        } else if (a == NULL) {
            // Nothing of ours to explode (it blew up in the sender's hands, or ours sank): an explosion there.
            c = c != NULL ? c : copyAlloc(e.slot, e.id);
            a = spawnBomb(link, daBomb_c::prm_make(daBomb_c::STATE_0, false, false), &pos, NULL);
            if (c != NULL) {
                c->pid = a != NULL ? fopAcM_GetID(a) : fpcM_ERROR_PROCESS_ID_e;
            }
        }
        if (c != NULL) {
            c->state = COPY_EXPLODED;
            c->age = 0;
        }
        return;

    case TWW_OVERLAY_EVENT_REMOVE:
        if (c != NULL && c->state == COPY_LIVE && !c->local) {
            c->state = COPY_LEAVING;
            c->age = 0;
        }
        return;

    case TWW_OVERLAY_EVENT_ARROW:
        if (c != NULL) {
            return; // a repeat
        }
        c = copyAlloc(e.slot, e.id);
        if (c == NULL || !spawnArrow(link, e)) {
            if (c != NULL) {
                c->state = COPY_FREE;
            }
            refused(e);
            return;
        }
        c->state = COPY_EXPLODED; // fire and forget: kept only so a repeat does nothing
        c->age = 0;
        fprintf(stderr, "[wwo] fx: slot %u arrow #%u (type %u) shot from (%.0f %.0f %.0f)\n", e.slot, e.id, e.variant,
                e.pos[0], e.pos[1], e.pos[2]);
        return;
    }
}

void updateCopies() {
    for (Copy& c : sCopy) {
        if (c.state == COPY_FREE) {
            continue;
        }
        if (c.age < 0xFFFF) {
            c.age++;
        }
        if (c.state == COPY_EXPLODED || c.state == COPY_GONE) {
            if (c.age > kDoneFrames) {
                c.state = COPY_FREE;
            }
            continue;
        }
        daBomb_c* a = findBomb(c.pid);
        if (a == NULL || a->getBombRestTime() == 0) {
            c.state = a == NULL ? COPY_GONE : COPY_EXPLODED; // sank / exploded here
            c.age = 0;
            fprintf(stderr, "[wwo] fx: slot %u copy #%u %s here\n", c.slot, c.id, a == NULL ? "gone" : "exploded");
            continue;
        }
        if (c.state == COPY_LEAVING && c.age > kLeaveFrames && !fopAcM_checkCarryNow(a)) {
            fopAcM_delete(a);
            c.state = COPY_GONE;
            c.age = 0;
            continue;
        }
        if (c.cannon || c.local) {
            continue;
        }
        // Taken over here: carried, swallowed or caught (its state changed), or its fuse set below our hold.
        const s16 rest = a->getBombRestTime();
        if (fopAcM_checkCarryNow(a) || (fopAcM_GetParam(a) & kBombStateMask) != daBomb_c::STATE_1 || rest < kHoldFuse - 1) {
            c.local = true;
            fprintf(stderr, "[wwo] fx: slot %u copy #%u taken over here (fuse %d)\n", c.slot, c.id, rest);
            continue;
        }
        // Wait for the sender's explosion (this runs before the bomb's execute).
        if ((s32)c.age < c.hold && rest < kHoldFuse) {
            a->setBombRestTime(kHoldFuse);
        }
    }
}

} // namespace

void WwoOverlay::makeCopy(daBomb_c* bomb) {
    bomb->field_0x6F0 = 0; // not the local Link's: explode / delete leave his bomb count alone
    bomb->mSph.OffAtVsPlayerBit();
}

s16 WwoOverlay::noGravityTime(const daBomb_c* bomb) {
    return bomb->mNoGravityTime;
}

bool WwoOverlay::isCopy(const daBomb_c* bomb) {
    return (fopAcM_GetParam(const_cast<daBomb_c*>(bomb)) & kCopyPrm) != 0;
}

void wwo_overlayFrame() {
    daPy_lk_c* link = linkPlayer();
    if (link == NULL || dComIfGp_isEnableNextStage()) {
        sIn.read = sIn.write; // no spawning across a stage change: what arrived belongs to the old stage
        return;
    }
    updateCopies();
    TwwOverlayEvent e;
    for (int n = 0; n < kEventsPerFrame && sIn.pop(&e); n++) {
        apply(link, e);
    }
    if (sSend) {
        trackLocal(link);
    } else {
        for (Track& t : sTrack) {
            t.state = TRACK_FREE;
        }
    }
}

extern "C" {

void tww_overlay_set_event_send(int on) {
    sSend = on != 0;
}

int tww_overlay_poll_event(TwwOverlayEvent* out) {
    return out != NULL && sOut.pop(out) ? 1 : 0;
}

int tww_overlay_push_peer_event(const TwwOverlayEvent* e) {
    return e != NULL && valid(*e) && sIn.push(*e) ? 1 : 0;
}

} // extern "C"
