// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// ww_online's puppet Link: a peer's player shown in the local game (tww-native-notes WW_ONLINE_EMBED.md,
// sections 1.3 and 4.2g; the Dolphin client's REL is GameMod/src/puppet_link/puppet.c). A daPy_lk_c
// built by the stock playerInit and makeBgWait, then driven from its peer slot (tww_set_peer) instead of
// the pad: position, facing, and the wait / walk / run blend from the peer's proc and speed.
//
// Player code writes its own animator objects into its model data and treats several globals as its
// own. So each puppet loads its own copy of Link.arc (registered as "Lnk<slot>"; player code finds it
// through l_arcName), and runs player code only inside a PuppetWindow, which hides the local player's
// status words, start-demo request and kept position from it and puts them back afterwards.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_audio.h"
#include "d/actor/d_a_player_main.h"
#include "d/actor/d_a_player_main_data.h"
#include "d/d_com_inf_game.h"
#include "d/d_com_lib_game.h"
#include "SSystem/SComponent/c_m3d.h"

#include "ww_boat.h"
#include "ww_online_internal.h"
#include "ww_puppet.h"
#include "ww_smooth.h"

#include <cstdio>
#include <cstring>

class daPuppet_c : public daPy_lk_c {
public:
    static cPhs_State phase1(daPuppet_c* i_this);
    static cPhs_State phase2(daPuppet_c* i_this);
    static cPhs_State phase3(daPuppet_c* i_this);
    cPhs_State create();
    BOOL execute();
    BOOL draw();
    BOOL remove();
    bool mountLinkCopy();
    static int procOf(const daPy_lk_c* player) { return player->mCurProc; }

    int mSlot;
    bool mArcMounted;
    bool mPlayerInited;
    bool mSeated; // in the peer's boat (ww_boat.cpp)
    char mArcName[sizeof("Link")];
    daPy_cupPackets_c mCupPackets;
};

int wwo_playerProc(const daPy_lk_c* player) {
    return daPuppet_c::procOf(player);
}

namespace {

// No start event (0xFF), start mode 0, and bit 0x40: makeBgWait keeps the local restart room.
const u32 kPuppetParam = 0xFF000040;

class PuppetWindow {
public:
    explicit PuppetWindow(daPuppet_c* puppet)
        : mKeepPos(puppet->current.pos), mKeepShape(puppet->shape_angle), mKeepAngle(puppet->current.angle),
          mScope(puppet, puppet->mSlot) {
        memcpy(mArcName, l_arcName, sizeof(mArcName));
        memcpy(l_arcName, puppet->mArcName, sizeof(mArcName));
        u32* status = g_dComIfG_gameInfo.play.mPlayerStatus[0];
        memcpy(mStatus, status, sizeof(mStatus));
        memset(status, 0, sizeof(mStatus));
        mException = g_dComIfG_gameInfo.play.getEvtManager().mException;
        daPy_swapDebugKeep(&mKeepPos, &mKeepShape, &mKeepAngle);
    }
    ~PuppetWindow() {
        daPy_swapDebugKeep(&mKeepPos, &mKeepShape, &mKeepAngle);
        g_dComIfG_gameInfo.play.getEvtManager().mException = mException;
        memcpy(g_dComIfG_gameInfo.play.mPlayerStatus[0], mStatus, sizeof(mStatus));
        memcpy(l_arcName, mArcName, sizeof(mArcName));
    }

private:
    char mArcName[sizeof("Link")];
    u32 mStatus[4];
    dEvent_exception_c mException;
    cXyz mKeepPos;
    csXyz mKeepShape;
    csXyz mKeepAngle;
    WwoPuppetScope mScope;
};

} // namespace

// The puppet's Link.arc, mounted again (mDoDvdThd_mountArchive_c reads a fresh copy) and registered
// under mArcName, so its model data is not the local Link's.
bool daPuppet_c::mountLinkCopy() {
    dRes_control_c& control = g_dComIfG_gameInfo.mResControl;
    if (control.getObjectResInfo(mArcName) != NULL) {
        return false; // the slot has a puppet already
    }
    dRes_info_c* info = dRes_control_c::newResInfo(control.mObjectInfo, ARRAY_SIZE(control.mObjectInfo));
    if (info == NULL || !info->set("Link", "/res/Object/", JKRArchive::DEFAULT_MOUNT_DIRECTION, NULL)) {
        return false;
    }
    strncpy(info->getArchiveName(), mArcName, sizeof(mArcName));
    info->incCount();
    return true;
}

cPhs_State daPuppet_c::phase1(daPuppet_c* i_this) {
    fopAcM_ct(i_this, daPuppet_c);
    const u32 slot = fopAcM_GetParam(i_this) >> 24;
    i_this->mSlot = slot < TWW_MAX_PEERS ? (int)slot : 0;
    fopAcM_SetParam(i_this, kPuppetParam);
    snprintf(i_this->mArcName, sizeof(i_this->mArcName), "Lnk%d", i_this->mSlot);
    fopAcM_setStageLayer(i_this);
    i_this->attention_info.flags = 0;
    if (!i_this->mountLinkCopy()) {
        return cPhs_ERROR_e;
    }
    i_this->mArcMounted = true;
    wwo_puppetReset(i_this->mSlot);
    return cPhs_NEXT_e;
}

cPhs_State daPuppet_c::phase2(daPuppet_c* i_this) {
    const int loading = dComIfG_syncObjectRes(i_this->mArcName);
    if (loading < 0) {
        return cPhs_ERROR_e;
    }
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    if (loading > 0 || player == NULL || !fopAcM_IsExecuting(fopAcM_GetID(player))) {
        return cPhs_INIT_e;
    }
    PuppetWindow window(i_this);
    i_this->playerInit();
    i_this->mPlayerInited = true;
    return cPhs_NEXT_e;
}

cPhs_State daPuppet_c::phase3(daPuppet_c* i_this) {
    PuppetWindow window(i_this);
    return i_this->makeBgWait();
}

cPhs_State daPuppet_c::create() {
    static cPhs__Handler l_method[] = {
        (cPhs__Handler)phase1,
        (cPhs__Handler)phase2,
        (cPhs__Handler)phase3,
        (cPhs__Handler)NULL,
    };
    return dComLbG_PhaseHandler(getPhase(), l_method, this);
}

BOOL daPuppet_c::execute() {
    const WwoPeer& peer = g_wwoPeers[mSlot];
    if (!peer.active) {
        return TRUE; // wwo_frame deletes it
    }
    // Network-authoritative: the peer's states played back a little behind the newest, with the anims and look of that
    // time (ww_smooth.cpp).
    f32 played[3];
    s16 playedAngle[3];
    const bool posed = g_wwoSmoothPuppet[mSlot].pose(played, playedAngle);
    if (posed) {
        wwo_smoothHandLate(mSlot);
    }
    PuppetWindow window(this);
    if (posed) {
        current.pos.set(played[0], played[1], played[2]);
        shape_angle.set(playedAngle[0], playedAngle[1], playedAngle[2]);
    }
    current.angle.y = shape_angle.y;
    speedF = peer.state.speed_f;

    // In the peer's boat: at its seat, tilted with the hull.
    cXyz seat;
    csXyz seatAngle;
    const bool seated = wwo_boatSeat(mSlot, peer.state.proc, &seat, &seatAngle);
    if (seated) {
        current.pos = seat;
        shape_angle = seatAngle;
        current.angle.y = seatAngle.y;
    }

    // The collision pass only updates the ground under the puppet (its shadow and room light); the
    // position stays the peer's.
    const cXyz pos = current.pos;
    old.pos = pos;
    mAcch.CrrPos(*dComIfG_Bgsp());
    if (mAcch.GetGroundH() != -G_CM3D_F_INF) {
        setRoomInfo();
    }
    current.pos = pos;
    old.pos = pos;

    wwo_puppetLook(this, mSlot);
    // The peer's own anims; else the ship procs' sitting pose in a boat, or the walk / run blend from their
    // speed (setBlendMoveAnime, as procMove does every frame).
    if (!wwo_puppetMirror(this, mSlot)) {
        const bool moving = peer.state.proc == daPyProc_MOVE_e || peer.state.speed_f > 0.5f;
        if (seated) {
            if (!mSeated) {
                procWait_init();
                initShipBaseAnime();
            }
        } else {
            if (moving) {
                procMove_init();
            } else {
                procWait_init();
            }
            mNormalSpeed = moving ? peer.state.speed_f : 0.0f;
            setBlendMoveAnime(-1.0f);
        }
        mSeated = seated;
        animeUpdate();
    } else {
        mSeated = false; // the mirror plays the peer's own sitting pose
    }
    playTextureAnime();
    wwo_puppetFace(this, mSlot);

    setWorldMatrix();
    setWaistAngle();
    setHatAngle();
    mpCLModelData->getJointNodePointer(CL_JNT_LINK_ROOT_e)->setMtxCalc(m_pbCalc[PART_UNDER_e]);
    mpCLModelData->getJointNodePointer(CL_JNT_BODY_CHN_e)->setMtxCalc(m_pbCalc[PART_UPPER_e]);
    mpCLModelData->getJointNodePointer(CL_JNT_WAIST_CHN_e)->setMtxCalc(m_pbCalc[PART_UNDER_e]);
    checkOriginalHatAnimation();
    mpCLModel->calc();
    mpKatsuraModel->setBaseTRMtx(mpCLModel->getAnmMtx(CL_JNT_HEAD_JNT_e));
    mpKatsuraModel->calc();
    mpYamuModel->setBaseTRMtx(mpCLModel->getAnmMtx(CL_JNT_HEAD_JNT_e));
    mpYamuModel->calc();
    setItemModel();
    return TRUE;
}

BOOL daPuppet_c::draw() {
    if (!g_wwoPeers[mSlot].active) {
        return TRUE;
    }
    PuppetWindow window(this);
    daPy_cupPackets_c* packets = daPy_cupPackets;
    daPy_cupPackets = &mCupPackets;
    daPy_lk_c::draw();
    wwo_puppetDraw(this, mSlot);
    daPy_cupPackets = packets;
    return TRUE;
}

BOOL daPuppet_c::remove() {
    if (mPlayerInited) {
        PuppetWindow window(this);
        playerDelete();
    }
    if (mArcMounted) {
        dComIfG_deleteObjectRes(mArcName);
        wwo_puppetReset(mSlot);
    }
    return TRUE;
}

static cPhs_State daPuppet_Create(fopAc_ac_c* i_this) {
    return static_cast<daPuppet_c*>(i_this)->create();
}

static BOOL daPuppet_Delete(daPuppet_c* i_this) {
    return i_this->remove();
}

static BOOL daPuppet_Execute(daPuppet_c* i_this) {
    return i_this->execute();
}

static BOOL daPuppet_IsDelete(daPuppet_c*) {
    return TRUE;
}

static BOOL daPuppet_Draw(daPuppet_c* i_this) {
    return i_this->draw();
}

static actor_method_class l_daPuppet_Method = {
    (process_method_func)daPuppet_Create,
    (process_method_func)daPuppet_Delete,
    (process_method_func)daPuppet_Execute,
    (process_method_func)daPuppet_IsDelete,
    (process_method_func)daPuppet_Draw,
};

actor_process_profile_definition g_profile_PUPPET = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 5,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_PUPPET_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daPuppet_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_PLAYER_e,
    /* Actor SubMtd */ &l_daPuppet_Method,
    /* Status       */ fopAcStts_FREEZE_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_0_e,
};
