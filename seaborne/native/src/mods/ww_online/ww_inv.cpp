// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The shared-inventory part of libtww's C API (pc/tww.h, "fan-out: inv"). Game thread only, between frames.
#include "d/dolzel.h" // IWYU pragma: keep
#include "d/d_com_inf_game.h"
#include "d/d_stage.h"

#include "pc/tww.h"

#include <cstddef>
#include <cstring>

static_assert(sizeof(TwwInvPlay) == 64 && offsetof(TwwInvPlay, select_item) == 56 &&
              offsetof(TwwInvPlay, save_tbl) == 60 && offsetof(TwwInvPlay, key_disp) == 61, "TwwInvPlay layout");

namespace {

s16* pending16(dComIfG_play_c& play, uint32_t counter) {
    switch (counter) {
    case TWW_INV_KEY: return &play.mItemKeyNumCount;
    case TWW_INV_MAX_LIFE: return &play.mItemMaxLifeCount;
    case TWW_INV_MAX_MAGIC: return &play.mItemMaxMagicCount;
    case TWW_INV_ARROW: return &play.mItemArrowNumCount;
    case TWW_INV_BOMB: return &play.mItemBombNumCount;
    default:
        return counter >= TWW_INV_BEAST && counter < TWW_INV_COUNTERS ? &play.mItemBeastNumCounts[counter - TWW_INV_BEAST]
                                                                      : NULL;
    }
}

} // namespace

extern "C" {

void tww_inv_get(TwwInvPlay* out) {
    memset(out, 0, sizeof(*out));
    dComIfG_play_c& play = g_dComIfG_gameInfo.play;
    out->pending[TWW_INV_RUPEE] = play.mItemRupeeCount;
    for (uint32_t i = TWW_INV_KEY; i < TWW_INV_COUNTERS; i++) {
        out->pending[i] = *pending16(play, i);
    }
    memcpy(out->select_item, play.mSelectItem, sizeof(out->select_item));
    // The stage data is only settled while the stage runs (tww_get_location's test); between scenes the
    // STAG pointer may be the unloaded stage's.
    TwwLocation loc;
    stage_stag_info_class* stag = tww_get_location(&loc) ? dComIfGp_getStageStagInfo() : NULL;
    out->save_tbl = stag != NULL ? (int8_t)dStage_stagInfo_GetSaveTbl(stag) : -1;
    out->key_disp = stag != NULL && dStage_stagInfo_ChkKeyDisp(stag) ? 1 : 0;
}

void tww_inv_add(uint32_t counter, int32_t delta) {
    dComIfG_play_c& play = g_dComIfG_gameInfo.play;
    if (counter == TWW_INV_RUPEE) {
        play.setItemRupeeCount(delta);
    } else if (s16* p = pending16(play, counter)) {
        *p += (s16)delta;
    }
}

void tww_inv_set_select_item(uint32_t button, uint8_t item_no) {
    if (button < ARRAY_SIZE(g_dComIfG_gameInfo.play.mSelectItem)) {
        g_dComIfG_gameInfo.play.setSelectItem(button, item_no);
    }
}

} // extern "C"
