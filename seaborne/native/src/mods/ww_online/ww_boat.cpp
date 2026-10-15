// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// ww_online's boats, the frame and API side (pc/tww.h, "boat"): the local King of Red Lions' state, the peers'
// boats (tww_boat_set_peer) and their actors' lifetimes (d_a_wwo_boat.cpp), the colours (ww_boatlook.cpp), and
// the COS_MP_TEST_PEER boat. Like the puppets (ww_online.cpp): a peer's boat is created while the local game is in
// play and the boat is in the local stage, and deleted when either stops or by the scene.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_player_main.h"
#include "d/actor/d_a_ship.h"
#include "d/d_com_inf_game.h"
#include "f_pc/f_pc_layer.h"
#include "f_pc/f_pc_manager.h"
#include "pc/pc_harness.h"
#include "res/Object/Ship.h"

#include "ww_boat.h"
#include "ww_boatlook.h"
#include "ww_online_internal.h"
#include "ww_smooth.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>

static_assert(sizeof(TwwBoatState) == 52 && offsetof(TwwBoatState, flags) == 32 && offsetof(TwwBoatState, part) == 36 &&
              offsetof(TwwBoatState, room) == 41 && offsetof(TwwBoatState, stage) == 44, "TwwBoatState layout");
static_assert(sizeof(TwwBoatLook) == 8 && offsetof(TwwBoatLook, sail_rgb) == 4, "TwwBoatLook layout");

WwoBoatPeer g_wwoBoats[TWW_MAX_PEERS];

namespace {

int sTestBoat = -1; // COS_MP_TEST_PEER

const unsigned int kRetryFrames = 120;      // after a boat that failed to create, before the next attempt
const unsigned int kDeleteRetryFrames = 16;

struct Slot {
    fpc_ProcID id = fpcM_ERROR_PROCESS_ID_e;
    char stage[8] = {};
    bool ran = false;      // the boat got past its create
    unsigned int deleting = 0; // frames since its delete was requested, 0 = none
    unsigned int wait = 0;
};
Slot sSlots[TWW_MAX_PEERS];

void createBoat(int i, const TwwBoatState& boat) {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    cXyz pos(boat.pos[0], boat.pos[1], boat.pos[2]);
    csXyz angle(0, boat.angle_y, 0);
    // From the player's layer (deleted with the scene); no room, so a sea square change keeps it.
    layer_class* saved = fpcLy_CurrentLayer();
    fpcLy_SetCurrentLayer(static_cast<base_process_class*>(player)->mLyTg.mpLayer);
    sSlots[i].id = fopAcM_create(fpcNm_PUPPET_BOAT_e, (u32)i << 24, &pos, -1, &angle);
    fpcLy_SetCurrentLayer(saved);
    memcpy(sSlots[i].stage, boat.stage, sizeof(sSlots[i].stage));
    sSlots[i].ran = false;
    sSlots[i].deleting = 0;
    fprintf(stderr, "[wwo] slot %d: boat requested in %s (process %u)\n", i, boat.stage, (unsigned)sSlots[i].id);
}

void updateSlot(int i, const TwwLocation& loc, bool inPlay) {
    Slot& slot = sSlots[i];
    const WwoBoatPeer& peer = g_wwoBoats[i];
    const bool wanted = inPlay && peer.active && strncmp(peer.state.stage, loc.stage, sizeof(loc.stage)) == 0;
    if (slot.id != fpcM_ERROR_PROCESS_ID_e) {
        base_process_class* proc = wwo_findProcess(slot.id);
        if (proc == nullptr) {
            fprintf(stderr, "[wwo] slot %d: boat gone%s\n", i, slot.ran ? "" : " (its create failed)");
            slot.id = fpcM_ERROR_PROCESS_ID_e;
            slot.wait = slot.ran ? 0 : kRetryFrames;
            return;
        }
        slot.ran = slot.ran || fopAcM_SearchByID(slot.id) != nullptr;
        if ((!wanted || strncmp(slot.stage, loc.stage, sizeof(loc.stage)) != 0) && slot.deleting++ % kDeleteRetryFrames == 0) {
            fpcM_Delete(proc);
        }
        return;
    }
    if (slot.wait > 0) {
        slot.wait--;
    } else if (wanted) {
        createBoat(i, peer.state);
    }
}

// COS_MP_TEST_BOAT=fixed: a parked boat placed once, broadside across the way ahead (1500 units ahead of the local
// boat once Link rides it, or 400 ahead of Link on foot, at his height, once he walks), to run into; the distance to
// it is logged every 10 frames.
void feedFixedBoat(const TwwBoatLook* look) {
    static TwwBoatState sBoat;
    static bool sPlaced;
    TwwLocalState link;
    TwwBoatState local;
    const bool boat = tww_boat_get_local(&local);
    const bool sailing = boat && !(local.flags & TWW_BOAT_PARKED);
    if (!tww_get_local_state(&link) || (!sPlaced && (boat ? !sailing : link.speed_f < 5.0f))) {
        return; // placed once Link rides the boat, or walks
    }
    if (!sPlaced) {
        sBoat = sailing ? local : TwwBoatState{};
        const f32 ahead = sailing ? 1500.0f : 400.0f;
        const s16 angle = sailing ? local.angle_y : link.angle[1];
        const f32* from = sailing ? local.pos : link.pos;
        const f32 yaw = angle * (f32)M_PI / 32768.0f;
        sBoat.pos[0] = from[0] + ahead * sinf(yaw) - 100.0f * cosf(yaw); // a little to the right: seen past the
        sBoat.pos[1] = from[1];                                             // local boat's head
        sBoat.pos[2] = from[2] + ahead * cosf(yaw) + 100.0f * sinf(yaw);
        sBoat.angle_y = angle + 0x4000;
        sBoat.flags = TWW_BOAT_PARKED | (sailing ? 0 : TWW_BOAT_FLYING);
        sBoat.speed_f = 0.0f;
        sBoat.part = 0;
        sBoat.mast_frame = 0xFF;
        sBoat.room = link.loc.room;
        memcpy(sBoat.stage, link.loc.stage, sizeof(sBoat.stage));
        sPlaced = true;
    }
    tww_boat_set_peer(0, &sBoat, look);
    if (pc_frame_count() % 10 == 0) {
        const f32* p = sailing ? local.pos : link.pos;
        fprintf(stderr, "[wwo] frame %u: fixed boat: %s at (%.1f %.1f %.1f), %.1f from the boat\n", pc_frame_count(),
                sailing ? "local boat" : "Link", p[0], p[1], p[2], hypotf(p[0] - sBoat.pos[0], p[2] - sBoat.pos[2]));
    }
}

// Is name one of the comma-separated words in list?
bool hasWord(const char* list, const char* name) {
    const size_t n = strlen(name);
    for (const char* p = list; (p = strstr(p, name)) != nullptr; p += n) {
        if ((p == list || p[-1] == ',') && (p[n] == ',' || p[n] == '\0')) {
            return true;
        }
    }
    return false;
}

// COS_MP_TEST_PEER=1 (dev only): slot 0's boat mirrors the local one 800 units ahead and 700 to its right, with
// slot 0's puppet in it. COS_MP_TEST_BOAT, a comma-separated list of: parked (nobody aboard, mast down), cannon or
// crane (that part out, posed), look (the peer's boat in blue and magenta), locallook (the local boat in green and
// yellow), turned (facing the other way: its head toward the camera), near (450 ahead, 250 right), fixed (see
// feedFixedBoat), blink (the boat gone 60 frames in every 200:
// its actor deleted and created again).
void feedTestBoat() {
    const char* modes = getenv("COS_MP_TEST_BOAT");
    if (modes == nullptr) {
        modes = "";
    }
    const TwwBoatLook look = {0x2A9B, {}, 0xE63CA0};
    const TwwBoatLook localLook = {0x2D07, {}, 0xFAC828};
    tww_boat_set_local_look(hasWord(modes, "locallook") ? &localLook : nullptr);
    if (hasWord(modes, "fixed")) {
        feedFixedBoat(hasWord(modes, "look") ? &look : nullptr);
        return;
    }
    TwwBoatState boat;
    if (!tww_boat_get_local(&boat) || (hasWord(modes, "blink") && pc_frame_count() % 200 >= 140)) {
        tww_boat_set_peer(0, nullptr, nullptr);
        return;
    }
    const f32 yaw = boat.angle_y * (f32)M_PI / 32768.0f;
    const f32 ahead = hasWord(modes, "near") ? 450.0f : 800.0f;
    const f32 right = hasWord(modes, "near") ? 250.0f : 700.0f;
    boat.pos[0] += ahead * sinf(yaw) - right * cosf(yaw);
    boat.pos[2] += ahead * cosf(yaw) + right * sinf(yaw);
    if (hasWord(modes, "turned")) {
        boat.angle_y += (s16)0x8000;
    }
    if (hasWord(modes, "parked")) {
        boat.flags = (boat.flags & ~TWW_BOAT_MAST_RAISED) | TWW_BOAT_PARKED; // as left: mast down
        boat.speed_f = 0.0f;
        boat.mast_frame = 0xFF;
    }
    if (hasWord(modes, "cannon") || hasWord(modes, "crane")) {
        boat.flags |= TWW_BOAT_MAST_RAISED | TWW_BOAT_MAST_HIDDEN;
        boat.mast_frame = 0xFF;
        boat.part = hasWord(modes, "cannon") ? daShip_c::PART_CANNON_e : daShip_c::PART_CRANE_e;
        boat.cannon_yaw = 0x1800;
        boat.cannon_pitch = 0x2800;
        boat.crane_angle = 0x2000;
        boat.rope_length = 30;
    }
    const bool withLook = hasWord(modes, "look");
    wwo_testSend([boat, look, withLook] { tww_boat_set_peer(0, &boat, withLook ? &look : nullptr); });
}

} // namespace

void wwo_boatFrame(const TwwLocation& loc, bool inPlay) {
    if (sTestBoat < 0) {
        const char* v = getenv("COS_MP_TEST_PEER");
        sTestBoat = v != nullptr && strcmp(v, "1") == 0 ? 1 : 0;
    }
    if (sTestBoat == 1) {
        feedTestBoat();
    }
    boatlook::tick();
    for (int i = 0; i < TWW_MAX_PEERS; i++) {
        updateSlot(i, loc, inPlay);
    }
}

extern "C" {

int tww_boat_get_local(TwwBoatState* out) {
    memset(out, 0, sizeof(*out));
    out->room = -1;
    fopAc_ac_c* link = dComIfGp_getLinkPlayer();
    daShip_c* ship = (daShip_c*)fopAcM_SearchByName(fpcNm_SHIP_e);
    if (link == nullptr || fopAcM_SearchByName(fpcNm_PLAYER_e) != link || ship == nullptr) {
        return 0;
    }
    const int proc = wwo_playerProc(static_cast<daPy_lk_c*>(link));
    const bool riding = dComIfGp_checkPlayerStatus0(0, daPyStts0_SHIP_RIDE_e) ||
                        (proc >= daPy_lk_c::daPyProc_SHIP_READY_e && proc <= daPy_lk_c::daPyProc_SHIP_RESTART_e) ||
                        proc == daPy_lk_c::daPyProc_DEMO_SHIP_SIT_e;
    if (!riding && dComIfGp_isEnableNextStage()) {
        return 0; // a parked boat may be freed with the stage
    }
    out->pos[0] = ship->current.pos.x;
    out->pos[1] = ship->current.pos.y;
    out->pos[2] = ship->current.pos.z;
    out->speed_f = riding ? ship->speedF : 0.0f;
    out->angle_y = ship->shape_angle.y;
    out->sail_angle = ship->mSailAngle;
    out->tiller = ship->m0366;
    out->head_x = ship->m03A0;
    out->head_y = ship->m03A2;
    out->part = ship->mPart;
    if (ship->mPart == daShip_c::PART_CANNON_e) {
        out->cannon_yaw = ship->m0394;
        out->cannon_pitch = ship->m0396;
    } else if (ship->mPart == daShip_c::PART_CRANE_e) {
        out->crane_angle = (s16)(ship->m0398 + ship->m039C);
        out->rope_length = (u8)std::clamp<s16>(ship->mRopeCnt, 0, kWwoMaxRope);
    }
    out->flags = (riding ? 0 : TWW_BOAT_PARKED) | (riding && ship->checkStateFlg(daShip_c::daSFLG_FLY_e) ? TWW_BOAT_FLYING : 0) |
                 (ship->m0392 == dRes_INDEX_SHIP_BCK_FN_MAST_ON2_e ? TWW_BOAT_MAST_RAISED : 0) |
                 (ship->m03E8 < 0.5f ? TWW_BOAT_MAST_HIDDEN : 0);
    // Rounded: a finished bck sits at end - 0.001 (EMode_NONE), which must arrive as the end frame.
    out->mast_frame = (u8)std::clamp(std::lround(ship->mpBodyAnm->getFrame()), 0L, 255L);
    out->head_frame = (u8)std::clamp(std::lround(ship->mpHeadAnm->getFrame()), 0L, 255L);
    out->head_bck = ship->m03B4 >= dRes_INDEX_SHIP_BCK_AKIBI1_e && ship->m03B4 <= dRes_INDEX_SHIP_BCK_KYAKKAN1_e &&
                            ship->m03B4 != dRes_INDEX_SHIP_BCK_FN_MAST_OFF2_e &&
                            ship->m03B4 != dRes_INDEX_SHIP_BCK_FN_MAST_ON2_e
                        ? (u8)ship->m03B4
                        : 0;
    out->room = fopAcM_GetRoomNo(ship);
    strncpy(out->stage, dComIfGp_getStartStageName(), sizeof(out->stage) - 1);
    return 1;
}

void tww_boat_set_peer(int slot, const TwwBoatState* boat, const TwwBoatLook* look) {
    if (slot < 0 || slot >= TWW_MAX_PEERS) {
        return;
    }
    if (boat == nullptr) {
        g_wwoSmoothBoat[slot].clear();
    } else {
        const bool newStage =
            !g_wwoBoats[slot].active || strncmp(boat->stage, g_wwoBoats[slot].state.stage, sizeof(boat->stage)) != 0;
        const s16 angle[3] = {0, boat->angle_y, 0};
        g_wwoSmoothBoat[slot].push(wwo_smoothSentAt(slot), boat->pos, angle, 1000.0f, newStage);
    }
    g_wwoBoats[slot].active = boat != nullptr;
    if (boat != nullptr) {
        g_wwoBoats[slot].state = *boat;
        g_wwoBoats[slot].state.stage[sizeof(boat->stage) - 1] = '\0';
    }
    g_wwoBoats[slot].look = look != nullptr ? *look : TwwBoatLook{};
}

} // extern "C"
