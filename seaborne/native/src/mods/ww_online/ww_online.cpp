// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The puppets' lifetimes (the Dolphin client's link_draw_hook.c state machine): once per frame, before
// fapGm_Execute, each peer slot gets a puppet while the peer is in the local stage and the local game is
// in play, and loses it otherwise. COS_MP_TEST_PEER=1 (dev only) feeds slot 0 from the local player, 150
// units to the right, so one process shows two Links.
#include "d/d_com_inf_game.h"
#include "f_op/f_op_actor_mng.h"
#include "f_pc/f_pc_create_iter.h"
#include "f_pc/f_pc_create_req.h"
#include "f_pc/f_pc_layer.h"
#include "f_pc/f_pc_manager.h"
#include "mods/ww_online.h"
#include "pc/pc_harness.h"

#include "ww_boat.h"
#include "ww_online_internal.h"
#include "ww_overlay.h"
#include "ww_puppet.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <functional>

namespace {

const unsigned int kSettleFrames = 30;     // the local player in play this long before puppets appear
const unsigned int kCreateFrames = 600;    // a puppet still creating after this (no ground) is dropped
const unsigned int kRetryFrames = 120;     // and its slot waits this long before the next attempt
const unsigned int kDeleteRetryFrames = 16;

struct Slot {
    fpc_ProcID id = fpcM_ERROR_PROCESS_ID_e;
    char stage[8] = {};
    unsigned int frames = 0; // in the current state
    bool deleting = false;
    unsigned int wait = 0;
};

Slot sSlots[TWW_MAX_PEERS];
unsigned int sInPlayFrames = 0;
int sTestPeer = -1;

void* judgeCreatingById(void* req, void* id) {
    return static_cast<create_request*>(req)->mBsPcId == *static_cast<fpc_ProcID*>(id) ? req : nullptr;
}

// COS_MP_TEST_RATE=<hz>[:<jitter ms>[:<latency ms>]]: the test peer's states (puppet and boat) are sent at that rate
// instead of every frame, each arriving after the latency plus up to the jitter, in order (the hub's connection is TCP),
// with its send time (tww_smooth_set_time).
struct TestSend {
    double arrives;
    std::function<void()> deliver;
};
std::deque<TestSend> sTestSends;

void deliverTestSends() {
    const double now = pc_frame_count() / 30.0;
    while (!sTestSends.empty() && sTestSends.front().arrives <= now) {
        sTestSends.front().deliver();
        sTestSends.pop_front();
    }
}

// COS_MP_TEST_STEPS=1 (dev, with or without the test peer): how far the local Link and slot 0's puppet moved (XZ) in
// each frame.
void logTestSteps() {
    static int on = -1;
    static cXyz lastLocal, lastPuppet;
    static bool have = false;
    if (on < 0) {
        const char* v = getenv("COS_MP_TEST_STEPS");
        on = v != nullptr && strcmp(v, "1") == 0;
    }
    fopAc_ac_c* puppet = wwo_puppetActor(0);
    TwwLocalState local;
    if (!on || puppet == nullptr || !tww_get_local_state(&local)) {
        have = false;
        return;
    }
    const cXyz l(local.pos[0], local.pos[1], local.pos[2]);
    if (have) {
        fprintf(stderr, "[wwo] step frame %u: local %.2f puppet %.2f speed %.2f\n", pc_frame_count(), (l - lastLocal).absXZ(),
                (puppet->current.pos - lastPuppet).absXZ(), local.speed_f);
    }
    lastLocal = l;
    lastPuppet = puppet->current.pos;
    have = true;
}

void feedTestPeer() {
    deliverTestSends();
    TwwLocalState local;
    if (!tww_get_local_state(&local)) {
        sTestSends.clear();
        tww_set_peer(0, nullptr);
        return;
    }
    TwwPeerState peer = {};
    const float yaw = local.angle[1] * (float)M_PI / 32768.0f;
    // COS_MP_TEST_OFFSET=<right>:<ahead> moves it (first person needs it ahead).
    float right = 150.0f, ahead = 0.0f;
    if (const char* v = getenv("COS_MP_TEST_OFFSET")) {
        sscanf(v, "%f:%f", &right, &ahead);
    }
    peer.pos[0] = local.pos[0] - right * cosf(yaw) + ahead * sinf(yaw);
    peer.pos[1] = local.pos[1];
    peer.pos[2] = local.pos[2] + right * sinf(yaw) + ahead * cosf(yaw);
    // COS_MP_TEST_FIXED=1: it stays where it was first placed (to walk into it).
    static float fixedPos[3];
    static bool placed = false;
    if (const char* v = getenv("COS_MP_TEST_FIXED"); v != nullptr && strcmp(v, "1") == 0) {
        if (!placed && (local.loc.flags & TWW_LOC_IDLE) != 0) {
            memcpy(fixedPos, peer.pos, sizeof(fixedPos));
            placed = true;
        }
        if (placed) {
            memcpy(peer.pos, fixedPos, sizeof(fixedPos));
            const float dist = hypotf(local.pos[0] - fixedPos[0], local.pos[2] - fixedPos[2]);
            if (dist < 250.0f && pc_frame_count() % 5 == 0) {
                fprintf(stderr, "[wwo] frame %u: the local Link %.1f units from the fixed test peer (speed %.2f)\n",
                        pc_frame_count(), dist, local.speed_f);
            }
        }
    }
    memcpy(peer.angle, local.angle, sizeof(peer.angle));
    peer.speed_f = local.speed_f;
    peer.proc = local.proc;
    memcpy(peer.stage, local.loc.stage, sizeof(peer.stage));
    peer.room = local.loc.room;
    wwo_testSend([peer] { tww_set_peer(0, &peer); });
    wwo_overlayTestFeed(local, peer);
    TwwPuppetAnim anim;
    TwwPuppetLook look;
    tww_puppet_get_local(&anim, &look);
    // COS_MP_TEST_LOOK=<clothes>:<rrggbb>[:<sword>:<shield>] dresses the test peer differently.
    if (const char* v = getenv("COS_MP_TEST_LOOK")) {
        unsigned int clothes = look.clothes, rgb = 0, sword = look.sword, shield = look.shield;
        sscanf(v, "%u:%x:%x:%x", &clothes, &rgb, &sword, &shield);
        look.clothes = (uint8_t)clothes;
        look.tunic[0] = (uint8_t)(rgb >> 16);
        look.tunic[1] = (uint8_t)(rgb >> 8);
        look.tunic[2] = (uint8_t)rgb;
        look.sword = (uint8_t)sword;
        look.shield = (uint8_t)shield;
    }
    wwo_testSend([anim, look] {
        tww_puppet_set_anim(0, &anim);
        tww_puppet_set_look(0, &look);
    });
    if (pc_frame_count() % 300 == 0) {
        fprintf(stderr, "[wwo] frame %u: local stage %s room %d point %d flags %u pos (%.1f %.1f %.1f) angle %d proc %u "
                        "speed %.2f\n",
                pc_frame_count(), local.loc.stage, local.loc.room, local.loc.point, local.loc.flags, local.pos[0],
                local.pos[1], local.pos[2], local.angle[1], local.proc, local.speed_f);
        cXyz head;
        if (wwo_puppetHeadPos(0, &head)) {
            fprintf(stderr, "[wwo] slot 0: puppet head top (%.1f %.1f %.1f)\n", head.x, head.y, head.z);
        }
    }
}

// COS_MP_SPAWN_OFFSET=<right>:<ahead> (dev only): moves the local Link once, when he first comes into play, so two
// clients booted to the same start point see each other's puppet beside them instead of inside them.
void applySpawnOffset() {
    static bool done = false;
    const char* v = getenv("COS_MP_SPAWN_OFFSET");
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    if (done || v == nullptr || player == nullptr) {
        return;
    }
    done = true;
    float right = 0.0f, ahead = 0.0f;
    sscanf(v, "%f:%f", &right, &ahead);
    const float yaw = player->shape_angle.y * (float)M_PI / 32768.0f;
    const float to[3] = {player->current.pos.x - right * cosf(yaw) + ahead * sinf(yaw), player->current.pos.y,
                         player->current.pos.z + right * sinf(yaw) + ahead * cosf(yaw)};
    const int ok = tww_gate_warp_local(to, player->shape_angle.y);
    fprintf(stderr, "[wwo] spawn offset %s: the player %s (%.1f %.1f %.1f)\n", v, ok ? "moved to" : "could not move to", to[0],
            to[1], to[2]);
}

void createPuppet(int i, const TwwPeerState& peer) {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    cXyz pos(peer.pos[0], peer.pos[1], peer.pos[2]);
    csXyz angle(0, peer.angle[1], 0);
    // From the player's layer, as an actor the player spawned would be.
    layer_class* saved = fpcLy_CurrentLayer();
    fpcLy_SetCurrentLayer(static_cast<base_process_class*>(player)->mLyTg.mpLayer);
    sSlots[i].id = fopAcM_create(fpcNm_PUPPET_e, (u32)i << 24, &pos, fopAcM_GetRoomNo(player), &angle);
    fpcLy_SetCurrentLayer(saved);
    sSlots[i].frames = 0;
    sSlots[i].deleting = false;
    memcpy(sSlots[i].stage, peer.stage, sizeof(sSlots[i].stage));
    fprintf(stderr, "[wwo] slot %d: puppet requested in %s (process %u)\n", i, peer.stage, (unsigned)sSlots[i].id);
}

void updateSlot(int i, const TwwLocation& loc, bool inPlay) {
    Slot& slot = sSlots[i];
    const WwoPeer& peer = g_wwoPeers[i];
    const bool wanted = inPlay && peer.active && strncmp(peer.state.stage, loc.stage, sizeof(loc.stage)) == 0;
    slot.frames++;

    if (slot.id != fpcM_ERROR_PROCESS_ID_e) {
        base_process_class* proc = wwo_findProcess(slot.id);
        if (proc == nullptr) {
            fprintf(stderr, "[wwo] slot %d: puppet gone\n", i);
            slot.id = fpcM_ERROR_PROCESS_ID_e;
            slot.frames = 0;
        } else {
            const bool creating = fopAcM_SearchByID(slot.id) == nullptr;
            const bool stuck = creating && slot.frames > kCreateFrames;
            const bool unwanted = !wanted || strncmp(slot.stage, loc.stage, sizeof(loc.stage)) != 0;
            if ((unwanted || stuck) && (!slot.deleting || slot.frames % kDeleteRetryFrames == 0)) {
                if (!slot.deleting) {
                    fprintf(stderr, "[wwo] slot %d: deleting the puppet (%s)\n", i, stuck ? "stuck creating" : "not shown");
                    slot.wait = stuck ? kRetryFrames : 0;
                    slot.deleting = true;
                    slot.frames = 0;
                }
                fpcM_Delete(proc);
            }
        }
        return;
    }
    if (slot.wait > 0) {
        slot.wait--;
        return;
    }
    if (wanted) {
        createPuppet(i, peer.state);
    }
}

} // namespace

base_process_class* wwo_findProcess(fpc_ProcID id) {
    if (fopAc_ac_c* actor = fopAcM_SearchByID(id)) {
        return actor;
    }
    create_request* req = static_cast<create_request*>(fpcCtIt_Judge(judgeCreatingById, &id));
    return req != nullptr ? req->mpRes : nullptr;
}

void wwo_testSend(std::function<void()> deliver) {
    static float rate = -1.0f, jitter = 0.0f, latency = 0.0f;
    static double next = 0.0, arrives = 0.0;
    static unsigned int frame = ~0u;
    static bool due = false;
    static uint32_t seed = 12345;
    if (rate < 0.0f) {
        rate = 0.0f;
        if (const char* v = getenv("COS_MP_TEST_RATE")) {
            sscanf(v, "%f:%f:%f", &rate, &jitter, &latency);
        }
    }
    if (rate <= 0.0f) {
        deliver();
        return;
    }
    const double now = pc_frame_count() / 30.0;
    if (frame != pc_frame_count()) {
        frame = pc_frame_count();
        due = now >= next;
        if (due) {
            next = now - next > 1.0 ? now + 1.0 / rate : next + 1.0 / rate;
            seed = seed * 1664525u + 1013904223u;
            arrives = std::max(arrives, now + (latency + jitter * (seed >> 8) / 16777216.0) / 1000.0);
        }
    }
    if (due) {
        sTestSends.push_back({arrives, [now, deliver] {
                                  tww_smooth_set_time(0, now);
                                  deliver();
                              }});
    }
}

void wwo_frame() {
    if (sTestPeer < 0) {
        const char* v = getenv("COS_MP_TEST_PEER");
        sTestPeer = v != nullptr && strcmp(v, "1") == 0 ? 1 : 0;
    }
    if (sTestPeer == 1) {
        feedTestPeer();
    }
    logTestSteps();
    wwo_puppetLocalOutfit();
    TwwLocation loc;
    const bool idle = tww_get_location(&loc) && (loc.flags & TWW_LOC_IDLE) != 0;
    sInPlayFrames = idle ? sInPlayFrames + 1 : 0;
    if (sInPlayFrames == 10) {
        applySpawnOffset();
    }
    for (int i = 0; i < TWW_MAX_PEERS; i++) {
        updateSlot(i, loc, sInPlayFrames >= kSettleFrames);
    }
    wwo_pvpFrame();
    wwo_world_frame();
    wwo_overlayFrame();
    wwo_boatFrame(loc, sInPlayFrames >= kSettleFrames);
}

fopAc_ac_c* wwo_puppetActor(int slot) {
    return slot >= 0 && slot < TWW_MAX_PEERS && sSlots[slot].id != fpcM_ERROR_PROCESS_ID_e
               ? fopAcM_SearchByID(sSlots[slot].id)
               : nullptr;
}
