// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The gate part of libtww's C API (pc/tww.h, "gate"): the scene facts the client's sync services wait on
// before touching the save, and the warp. Game thread only, between frames.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_player_main.h"
#include "d/d_camera.h"
#include "d/d_com_inf_game.h"
#include "d/d_meter.h"

#include "pc/tww.h"

#include <cstddef>
#include <cstring>

static_assert(sizeof(TwwGateState) == 28 && offsetof(TwwGateState, start_point) == 8 &&
              offsetof(TwwGateState, room) == 12 && offsetof(TwwGateState, other_character) == 18 &&
              offsetof(TwwGateState, player_status0) == 20 && offsetof(TwwGateState, link_mode_flg) == 24,
              "TwwGateState: the client's layout");

namespace {

// The Link actor once it is created and running (as ww_api.cpp's linkPlayer).
daPy_lk_c* gateLink() {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    if (player == NULL || fopAcM_SearchByName(fpcNm_PLAYER_e) != player) {
        return NULL;
    }
    return static_cast<daPy_lk_c*>(player);
}

bool isStageName(const char* name) {
    size_t n = 0;
    for (; name[n] != '\0'; n++) {
        const char c = name[n];
        if (n >= 7 || !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')) {
            return false;
        }
    }
    return n > 0;
}

} // namespace

extern "C" {

void tww_gate_get_state(TwwGateState* out) {
    memset(out, 0, sizeof(*out));
    dComIfG_play_c& play = g_dComIfG_gameInfo.play;
    strncpy(out->stage, play.getStartStageName(), sizeof(out->stage) - 1);
    out->start_point = play.getStartStagePoint();
    out->start_room = play.getStartStageRoomNo();
    out->start_layer = play.getStartStageLayer();
    out->room = (int8_t)dComIfGp_roomControl_getStayNo();
    out->next_stage = play.isEnableNextStage() ? 1 : 0;
    out->event_mode = play.getEvent()->getMode();
    out->menu_paused = dMenu_flag() != 0 ? 1 : 0;
    out->mini_game = play.getMiniGameType();
    out->player_status0 = play.checkPlayerStatus(0, 0, 0xFFFFFFFF);
    daPy_lk_c* link = gateLink();
    if (link != NULL) {
        out->link = 1;
        out->link_mode_flg = link->checkModeFlg(0xFFFFFFFF);
        fopAc_ac_c* controlled = dComIfGp_getPlayer(0);
        out->other_character = controlled != NULL && controlled != link ? 1 : 0;
    }
}

int tww_gate_warp_local(const float pos[3], int16_t angle_y) {
    daPy_lk_c* link = gateLink();
    if (link == NULL || dComIfGp_isEnableNextStage()) {
        return 0;
    }
    // Link's execute starts from the kept position and angles (l_debug_keep_pos, ...): set those too, or the
    // move is undone before anything sees it. old.pos as well, so his Acch sees no move to push back.
    cXyz to(pos[0], pos[1], pos[2]);
    csXyz shape = link->shape_angle;
    csXyz angle = link->current.angle;
    shape.y = angle_y;
    angle.y = angle_y;
    cXyz keepPos = to;
    csXyz keepShape = shape;
    csXyz keepAngle = angle;
    daPy_swapDebugKeep(&keepPos, &keepShape, &keepAngle);
    // The camera moves with him, or it stays where he was, looking at the back of his head from up close.
    if (dCamera_c* camera = dCam_getBody()) {
        const cXyz delta = to - link->current.pos;
        camera->Reset(camera->Center() + delta, camera->Eye() + delta);
    }
    link->current.pos = to;
    link->old.pos = to;
    link->shape_angle = shape;
    link->current.angle = angle;
    link->speed = cXyz::Zero;
    link->speedF = 0.0f;
    return 1;
}

int tww_gate_change_stage(const char* stage, int16_t point, int8_t room, int8_t layer) {
    if (stage == NULL || !isStageName(stage) || gateLink() == NULL || dComIfGp_isEnableNextStage()) {
        return 0;
    }
    dComIfGp_setNextStage(stage, point, room, layer);
    return 1;
}

} // extern "C"
