// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The multiplayer part of libtww's C API (pc/tww.h, API version 2). Game thread only, between frames.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_audio.h"
#include "d/actor/d_a_player_main.h"
#include "d/d_com_inf_game.h"
#include "d/d_save.h"

#include "ww_online_internal.h"
#include "ww_smooth.h"

#include <algorithm>
#include <cstddef>
#include <cstring>

static_assert(__BYTE_ORDER__ == __ORDER_LITTLE_ENDIAN__, "the save window swaps host-order fields to big-endian");
// The GameCube offsets past dSv_save_c (d_save.h's comments; layout_headers.txt checks up to dSv_save_c).
static_assert(offsetof(dSv_info_c, mMemory) == 0x778 && offsetof(dSv_info_c, mDan) == 0x79C &&
              offsetof(dSv_info_c, mZone) == 0x7A8 && offsetof(dSv_info_c, mRestart) == 0x1128 &&
              offsetof(dSv_info_c, mTmp) == 0x1158 && offsetof(dSv_info_c, mTurnRestart) == 0x1258 &&
              offsetof(dSv_info_c, mDataNum) == 0x1290 && offsetof(dSv_info_c, mMemCardCheckID) == 0x1298 &&
              sizeof(dSv_info_c) == 0x12A0, "dSv_info_c: GameCube layout");
static_assert(sizeof(dSv_danBit_c) == 0xC && sizeof(dSv_zone_c) == 0x4C && sizeof(dSv_restart_c) == 0x30 &&
              sizeof(dSv_turnRestart_c) == 0x38 && offsetof(dSv_restart_c, mRestartPos) == 0x18 &&
              offsetof(dSv_turnRestart_c, mShipPos) == 0x24 && offsetof(dSv_turnRestart_c, mHasShip) == 0x34,
              "dSv_info_c's tail: GameCube layout");

WwoPeer g_wwoPeers[TWW_MAX_PEERS];

namespace {

// The Link actor once it is created and running; never a pointer left over from a deleted play scene.
daPy_lk_c* linkPlayer() {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    if (player == NULL || fopAcM_SearchByName(fpcNm_PLAYER_e) != player) {
        return NULL;
    }
    return static_cast<daPy_lk_c*>(player);
}

// dSv_info_c keeps the GameCube offsets natively (STATIC_ASSERT in d_save.h). dSv_save_c and dSv_memory_c
// are big-endian there (BE(T), they go to the memory card as they are); the fields after them are in host
// order. These are those fields, swapped to make the window big-endian throughout.
struct HostRun {
    size_t offset;
    size_t width;
    size_t count;
};

template <typename F>
void forEachHostRun(F f) {
    f(HostRun{offsetof(dSv_info_c, mDan) + offsetof(dSv_danBit_c, mSwitch), 4, 2});
    for (size_t i = 0; i < ARRAY_SIZE(dSv_info_c::mZone); i++) {
        const size_t zone = offsetof(dSv_info_c, mZone) + i * sizeof(dSv_zone_c);
        f(HostRun{zone + offsetof(dSv_zone_c, mZoneBit), 2, 4});   // mSwitch[3], mItem
        f(HostRun{zone + offsetof(dSv_zone_c, mZoneActor), 4, 16}); // mActorFlags
    }
    const size_t restart = offsetof(dSv_info_c, mRestart);
    f(HostRun{restart + offsetof(dSv_restart_c, mOptionPoint), 2, 2}); // mOptionPoint, mOptionRoomAngleY
    f(HostRun{restart + offsetof(dSv_restart_c, mOptionRoomPos), 4, 3});
    f(HostRun{restart + offsetof(dSv_restart_c, mStartCode), 2, 2});   // mStartCode, mRestartAngle
    f(HostRun{restart + offsetof(dSv_restart_c, mRestartPos), 4, 6});  // + mRestartParam, mLastSpeedF, mLastMode
    const size_t turn = offsetof(dSv_info_c, mTurnRestart);
    f(HostRun{turn + offsetof(dSv_turnRestart_c, mPosition), 4, 4});   // + mParam
    f(HostRun{turn + offsetof(dSv_turnRestart_c, mAngleY), 2, 1});
    f(HostRun{turn + offsetof(dSv_turnRestart_c, mShipPos), 4, 3});
    f(HostRun{turn + offsetof(dSv_turnRestart_c, mShipAngleY), 2, 1});
    f(HostRun{turn + offsetof(dSv_turnRestart_c, mHasShip), 4, 1});
    f(HostRun{offsetof(dSv_info_c, mMemCardCheckID), 8, 1});
}

// Host order <-> big-endian, in place (its own inverse).
void swapHostRuns(uint8_t* image) {
    forEachHostRun([image](const HostRun& run) {
        for (size_t i = 0; i < run.count; i++) {
            std::reverse(image + run.offset + i * run.width, image + run.offset + (i + 1) * run.width);
        }
    });
}

} // namespace

extern "C" {

int tww_get_location(TwwLocation* out) {
    memset(out, 0, sizeof(*out));
    out->room = -1;
    out->layer = -1;
    const char* stage = dComIfGp_getStartStageName();
    if (linkPlayer() == NULL || stage[0] == '\0' || strcmp(stage, "sea_T") == 0 || strcmp(stage, "Name") == 0) {
        return 0;
    }
    strncpy(out->stage, stage, sizeof(out->stage) - 1);
    out->room = (int8_t)dComIfGp_roomControl_getStayNo();
    out->layer = dComIfGp_getStartStageLayer();
    out->point = dComIfGp_getStartStagePoint();
    if (!dComIfGp_isEnableNextStage()) {
        out->flags |= TWW_LOC_IDLE;
    }
    if (dComIfGp_event_runCheck()) {
        out->flags |= TWW_LOC_EVENT;
    }
    return 1;
}

int tww_get_local_state(TwwLocalState* out) {
    memset(out, 0, sizeof(*out));
    daPy_lk_c* player = linkPlayer();
    if (player == NULL) {
        return 0;
    }
    out->valid = 1;
    out->pos[0] = player->current.pos.x;
    out->pos[1] = player->current.pos.y;
    out->pos[2] = player->current.pos.z;
    out->angle[0] = player->shape_angle.x;
    out->angle[1] = player->shape_angle.y;
    out->angle[2] = player->shape_angle.z;
    out->speed_f = player->speedF;
    out->proc = (uint32_t)wwo_playerProc(player);
    tww_get_location(&out->loc);
    return 1;
}

void tww_set_peer(int slot, const TwwPeerState* peer) {
    if (slot < 0 || slot >= TWW_MAX_PEERS) {
        return;
    }
    if (peer == NULL) {
        g_wwoSmoothPuppet[slot].clear();
    } else {
        const bool newStage =
            !g_wwoPeers[slot].active || strncmp(peer->stage, g_wwoPeers[slot].state.stage, sizeof(peer->stage)) != 0;
        g_wwoSmoothPuppet[slot].push(wwo_smoothSentAt(slot), peer->pos, peer->angle, 300.0f, newStage);
        g_wwoPeers[slot].state = *peer;
        g_wwoPeers[slot].state.stage[sizeof(peer->stage) - 1] = '\0';
    }
    g_wwoPeers[slot].active = peer != NULL;
}

uint32_t tww_save_size(void) {
    return sizeof(dSv_info_c);
}

uint32_t tww_save_read(uint32_t offset, void* dst, uint32_t n) {
    if (offset >= sizeof(dSv_info_c)) {
        return 0;
    }
    n = std::min<uint32_t>(n, sizeof(dSv_info_c) - offset);
    uint8_t image[sizeof(dSv_info_c)];
    memcpy(image, &g_dComIfG_gameInfo.save, sizeof(image));
    swapHostRuns(image);
    memcpy(dst, image + offset, n);
    return n;
}

uint32_t tww_save_write(uint32_t offset, const void* src, uint32_t n) {
    if (offset >= sizeof(dSv_info_c)) {
        return 0;
    }
    n = std::min<uint32_t>(n, sizeof(dSv_info_c) - offset);
    // Through a big-endian image, so a write to part of a host-order field lands on the right bytes.
    uint8_t image[sizeof(dSv_info_c)];
    memcpy(image, &g_dComIfG_gameInfo.save, sizeof(image));
    swapHostRuns(image);
    memcpy(image + offset, src, n);
    swapHostRuns(image);
    memcpy(&g_dComIfG_gameInfo.save, image, sizeof(image));
    return n;
}

} // extern "C"
