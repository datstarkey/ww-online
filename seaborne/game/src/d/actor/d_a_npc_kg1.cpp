/**
 * d_a_npc_kg1.cpp
 * NPC - Salvatore (Squid-Hunt/"Sploosh Kaboom" Minigame)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_npc_kg1.h"
#include "d/d_lib.h"
#include "d/d_cc_d.h"
#include "d/actor/d_a_player.h"
#include "d/actor/d_a_mgameboard.h"
#include "d/d_event_manager.h"
#include "m_Do/m_Do_controller_pad.h"
#include "JAZelAudio/JAZelAudio_SE.h"

static dCcD_SrcCyl l_cyl_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ AT_TYPE_ALL,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsEnemy_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsPlayer_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ 0,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoHitMark_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCylS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 40.0f,
        /* Height */ 160.0f,
    }},
};


class daNpc_Kg1_HIO_c : public JORReflexible {
public:
    daNpc_Kg1_HIO_c();
    virtual ~daNpc_Kg1_HIO_c() {}

public:
    /* 0x04 */ s8 mNo;
    /* 0x05 */ u8 field_0x5[0x8 - 0x5];
    /* 0x08 */ s32 field_0x8;
    /* 0x0C */ u8 field_0xc;
    /* 0x0D */ u8 field_0xd[0x10 - 0xD];
    /* 0x10 */ dNpc_HIO_c mHio[1];
    /* 0x38 */ u8 field_0x38;
    /* 0x39 */ u8 field_0x39;
    /* 0x3A */ u8 field_0x3a[0x3C - 0x3A];
};

static daNpc_Kg1_HIO_c l_HIO;

/* 000000EC-000001E0       .text __ct__15daNpc_Kg1_HIO_cFv */
daNpc_Kg1_HIO_c::daNpc_Kg1_HIO_c() {
    mNo = -1;
    field_0x8 = -1;
    mHio[0].m04 = 0.0f;
    mHio[0].mMaxHeadX = 0x9c4;
    mHio[0].mMaxHeadY = 0x1b58;
    mHio[0].mMaxBackboneX = 0x7d0;
    mHio[0].mMaxBackboneY = 0x1f40;
    mHio[0].mMinHeadX = -0x9c4;
    mHio[0].mMinHeadY = -0x1b58;
    mHio[0].mMinBackboneX = -0x7d0;
    mHio[0].mMinBackboneY = -0x1f40;
    mHio[0].mMaxTurnStep = 0x3e8;
    mHio[0].mMaxHeadTurnVel = 0x7d0;
    mHio[0].mAttnYOffset = 35.0f;
    mHio[0].mMaxAttnAngleY = 0x4000;
    mHio[0].m22 = 0;
    mHio[0].mMaxAttnDistXZ = 400.0f;
    field_0xc = 0;
    field_0x38 = 0;
    field_0x39 = 0;
}

const char daNpc_Kg1_c::m_arcname[] = "Kg";
const f32 daNpc_Kg1_c::m_camera_fovy = 40.0f;
cXyz daNpc_Kg1_c::m_camera_ctr(-200.0f, 140.0f, 85.0f);
cXyz daNpc_Kg1_c::m_camera_eye(-117.0f, 92.0f, 344.0f);

#include "res/Object/Kg.h"

static const int l_btp_ix_tbl[] = {
    dRes_INDEX_KG_BTP_KG_e,
    dRes_INDEX_KG_BTP_MOPEN_e,
    dRes_INDEX_KG_BTP_WARAI_e,
    dRes_INDEX_KG_BTP_RADIO_e,
};


/* 000001E0-00000428       .text daNpc_Kg1_nodeCallBack__FP7J3DNodei */
static BOOL daNpc_Kg1_nodeCallBack(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Kg1_c* actor = (daNpc_Kg1_c*)model->getUserArea();
        int jntNo = ((J3DJoint*)i_node)->getJntNo();

        mDoMtx_stack_c::copy(model->getAnmMtx(jntNo));

        if (jntNo == actor->m_jnt.getHeadJntNum()) {
            mDoMtx_stack_c::XrotM(-(-actor->m_jnt.getHead_y()));
            mDoMtx_stack_c::ZrotM(-actor->m_jnt.getHead_x());

            static cXyz l_offsetAttPos(24.0f, 5.0f, 0.0f);
            static cXyz l_offsetEyePos(24.0f, -16.0f, 0.0f);

            mDoMtx_stack_c::multVec(&l_offsetAttPos, &actor->m740);
            mDoMtx_stack_c::XrotM(-(-actor->m_jnt.getHead_y()));
            mDoMtx_stack_c::ZrotM(-actor->m_jnt.getHead_x());
            mDoMtx_stack_c::multVec(&l_offsetEyePos, &actor->m734);
            mDoMtx_stack_c::multVec(&l_offsetAttPos, &actor->attention_info.position);
            actor->attention_info.position.y += l_HIO.mHio[0].mAttnYOffset;
        }

        if (jntNo == actor->m_jnt.getBackboneJntNum()) {
            mDoMtx_stack_c::XrotM(-(-actor->m_jnt.getBackbone_y()));
            mDoMtx_stack_c::ZrotM(-actor->m_jnt.getBackbone_x());
        }

        model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
        cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);

        if (jntNo == 8) {
            mDoMtx_stack_c::transM(23.46f, -22.26f, -47.05f);
            mDoMtx_stack_c::XYZrotM(0x1f4b, -0x4f00, 0x1f4b);
            actor->m6C4->setBaseTRMtx(mDoMtx_stack_c::get());
        }
    }

    return TRUE;
}

/* 00000464-00000688       .text lookBack__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::lookBack() {
    f32 dist = (dComIfGp_getPlayer(0)->current.pos - current.pos).absXZ();

    cXyz* pTgtPos = NULL;
    cXyz tgtPos;

    if (mEventCut.getAttnFlag()) {
        m_jnt.setTrn();
        tgtPos = mEventCut.getAttnPos();
        pTgtPos = &tgtPos;
        m731 = 1;
    } else if (m74D == 8) {
        tgtPos = m_camera_eye;
        pTgtPos = &tgtPos;
    } else if (dist < l_HIO.mHio[0].mMaxAttnDistXZ) {
        tgtPos = dNpc_playerEyePos(l_HIO.mHio[0].m04);
        pTgtPos = &tgtPos;
        m731 = chkAttention();
    } else {
        m731 = 0;
    }

    if (m_jnt.trnChk()) {
        s16 turnVel = l_HIO.mHio[0].mMaxHeadTurnVel;
        if (mEventCut.getTurnSpeed() != 0) {
            turnVel = mEventCut.getTurnSpeed();
        }

        cLib_addCalcAngleS2(&m72E, turnVel, 4, 0x800);
    } else {
        m72E = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, pTgtPos, eyePos, current.angle.y, m72E, true);
}

/* 00000688-000007D8       .text chkAttention__11daNpc_Kg1_cFv */
BOOL daNpc_Kg1_c::chkAttention() {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    f32 dist = (player->current.pos - current.pos).absXZ();
    s16 maxAngle = l_HIO.mHio[0].mMaxAttnAngleY;

    if (dist < l_HIO.mHio[0].mMaxAttnDistXZ) {
        cXyz sp;
        sp.x = player->current.pos.x - current.pos.x;
        sp.z = player->current.pos.z - current.pos.z;
        s16 diff = cM_atan2s(sp.x, sp.z) -
                   (s16)(current.angle.y + m_jnt.getHead_y() + m_jnt.getBackbone_y());
        s16 absDiff = abs(diff);
        if (absDiff < maxAngle) {
            return TRUE;
        }
    }

    return FALSE;
}

#include "d/d_com_inf_game.h"
#include "JSystem/JUtility/JUTAssert.h"
#include "res/Object/Kg.h"

/* 000007D8-000008D8       .text initTexPatternAnm__11daNpc_Kg1_cFib */
BOOL daNpc_Kg1_c::initTexPatternAnm(int ib, bool i_modify) {
    J3DModelData* modelData = mpMorf->getModel()->getModelData();
    m_eye_tex_pattern = (J3DAnmTexPattern*)dComIfG_getObjectRes(m_arcname, l_btp_ix_tbl[ib]);
    JUT_ASSERT(0x1cc, m_eye_tex_pattern != 0);
    if (!m6F8.init(modelData, m_eye_tex_pattern, TRUE, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, i_modify, FALSE)) {
        return FALSE;
    }
    m720 = 0;
    m72C = 0;
    return TRUE;
}

/* 000008D8-00000944       .text playTexPatternAnm__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::playTexPatternAnm() {
    if (cLib_calcTimer(&m72C) == 0) {
        if (m720 >= m_eye_tex_pattern->getFrameMax()) {
            m720 -= m_eye_tex_pattern->getFrameMax();
            m72C = 0x78;
        } else {
            m720++;
        }
    }
}

/* 00000944-000009E8       .text set_mtx__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::set_mtx() {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    m6C4->setBaseScale(scale);
}

/* 000009E8-00000A08       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Kg1_c*)i_this)->CreateHeap();
}

#include "res/Object/Kg.h"
#include "d/d_com_inf_game.h"
#include "f_op/f_op_actor_mng.h"

/* 00000A08-00000D34       .text CreateHeap__11daNpc_Kg1_cFv */
BOOL daNpc_Kg1_c::CreateHeap() {
    J3DModelData* modelData = (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_KG_BDL_KG_e);

    mpMorf = new mDoExt_McaMorf(
        modelData, NULL, NULL,
        static_cast<J3DAnmTransform*>(dComIfG_getObjectRes(m_arcname, dRes_INDEX_KG_BCK_KG_WAIT01_e)),
        J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, 1, NULL, 0x80000, 0x11020022
    );
    if (mpMorf == NULL || mpMorf->getModel() == NULL) {
        return FALSE;
    }

    m_jnt.setHeadJntNum(modelData->getJointName()->getIndex("head"));
    JUT_ASSERT(547, m_jnt.getHeadJntNum() >= 0);
    m_jnt.setBackboneJntNum(modelData->getJointName()->getIndex("backbone2"));
    JUT_ASSERT(552, m_jnt.getBackboneJntNum() >= 0);

    if (!initTexPatternAnm(3, false)) {
        return FALSE;
    }
    initTexPatternAnm(0, true);

    m6C4 = mDoExt_J3DModel__create(
        static_cast<J3DModelData*>(dComIfG_getObjectRes(m_arcname, dRes_INDEX_KG_BDL_KG_PLATE_e)),
        0x80000, 0x11020022
    );
    if (m6C4 == NULL) {
        return FALSE;
    }

    if (!mBtpAnm.init(
            static_cast<J3DModelData*>(dComIfG_getObjectRes(m_arcname, dRes_INDEX_KG_BDL_KG_PLATE_e)),
            static_cast<J3DAnmTexPattern*>(dComIfG_getObjectRes(m_arcname, dRes_INDEX_KG_BTP_KG_PLATE_e)),
            TRUE, 2, 1.0f, 0, -1, false)) {
        return FALSE;
    }

    for (u16 i = 0; i < modelData->getJointNum(); i++) {
        if (i == m_jnt.getHeadJntNum() || i == m_jnt.getBackboneJntNum() || i == 8) {
            modelData->getJointNodePointer(i)->setCallBack(daNpc_Kg1_nodeCallBack);
        }
    }

#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif
    mAcchCir.SetWall(30.0f, 30.0f);
    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir,
                 fopAcM_GetSpeed_p(this), fopAcM_GetAngle_p(this), fopAcM_GetShapeAngle_p(this));

    return TRUE;
}

/* 00000D34-00000EF0       .text CreateInit__11daNpc_Kg1_cFv */
cPhs_State daNpc_Kg1_c::CreateInit() {
    mStts.Init(0xff, 0xff, this);
    mCyl.Set(l_cyl_src);
    mCyl.SetStts(&mStts);
    setCollision(60.0f, 150.0f);
    gravity = -9.0f;

    mEventCut.setActorInfo2("Auc0", this);
    set_mtx();

    m72E = 0;
    m730 = 0;
    m731 = 0;
    cullMtx = mpMorf->getModel()->getBaseTRMtx();

    wait_action_init();

    if (l_HIO.field_0x8 < 0) {
        l_HIO.mNo = mDoHIO_createChild("海戦ゲーム屋", &l_HIO);
    }

    l_HIO.field_0x8++;

    attention_info.flags = 10;

    dEvent_manager_c* evtMgr = dComIfGp_getPEvtManager();
    m784 = evtMgr->getEventIdx("MINIGAME_START", 0xff);
    m786 = evtMgr->getEventIdx("MINIGAME_END", 0xff);
    m788 = evtMgr->getEventIdx("KAISEN_GETITEM", 0xff);

    m74E = 0;
    m74D = 2;
    setAnm();

    m_jnt.onHeadLock();
    m_jnt.onBackBoneLock();
    m750 = 1;

    m734.x = current.pos.x;
    m734.y = current.pos.y;
    m734.z = current.pos.z;

    return cPhs_COMPLEATE_e;
}

#include "f_op/f_op_actor_mng.h"

/* 00000EF0-00000FE8       .text eventOrder__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::eventOrder() {
    if (m732 == 2 || m732 == 1) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (m732 == 2) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (m732 == 3) {
        fopAcM_orderOtherEventId(this, m784, 0xff, 0xffff, 0, 1);
        eventInfo.onCondition(dEvtCnd_UNK2_e);
    } else if (m732 == 4) {
        fopAcM_orderOtherEventId(this, m786, 0xff, 0xffff, 0, 1);
        eventInfo.onCondition(dEvtCnd_UNK2_e);
    } else if (m732 == 5) {
        fopAcM_orderOtherEventId(this, m788, 0xff, 0xffff, 0, 1);
        eventInfo.onCondition(dEvtCnd_UNK2_e);
    }
}

#include "d/d_com_inf_game.h"

/* 00000FE8-000010C4       .text checkOrder__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::checkOrder() {
    if (eventInfo.checkCommandDemoAccrpt()) {
        if (dComIfGp_evmng_startCheck(m784)) {
            if (m732 == 3) {
                m732 = 0;
            }
        }
        if (dComIfGp_evmng_endCheck(m784)) {
            m732 = 0;
        }
        if (dComIfGp_evmng_startCheck(m788)) {
            if (m732 == 5) {
                m732 = 0;
            }
        }
    } else if (eventInfo.checkCommandTalk()) {
        if (m732 == 2 || m732 == 1) {
            m730 = 1;
        }
    }
}

#include "f_op/f_op_camera.h"

/* 000010C4-00001188       .text kg1_talk_camera__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::kg1_talk_camera() {
    camera_process_class* camera = dComIfGp_getCamera(dComIfGp_getPlayerCameraID(0));

    if (m751 && camera != NULL) {
        camera->mCamera.Stay();
        camera->mCamera.Set(m_camera_ctr, m_camera_eye, m_camera_fovy, 0);
        camera->mCamera.Reset();
        camera->mCamera.SetTrimSize(1);
    }
}

/* 00001188-000011D4       .text wait_action_init__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::wait_action_init() {
    clr_seq_flag();
    setAction(&daNpc_Kg1_c::wait_action);
}

/* 000011D4-00001858       .text wait_action__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::wait_action() {
    int staffId = dComIfGp_evmng_getMyStaffId("Kg1");
    fopAc_ac_c* board = fopAcM_SearchByName(fpcNm_MGBOARD_e);

    switch (mWaitMode) {
    case 0:
        m751 = 0;
        m_jnt.onHeadLock();
        m_jnt.onBackBoneLock();
        if (board != NULL) {
            ((daMgBoard_c*)board)->clrGInfoDraw();
        }
        clr_seq_flag();
        if (m731 != 0) {
            m732 = 1;
        } else {
            m732 = 0;
        }
        if (m730 == 0) {
            break;
        }
        mWaitMode = 1;
        mBtpAnm.setFrame(0.0f);
        // fallthrough
    case 1: {
        u16 res = talk(1);
        if (mCurrMsgNo < 0x1d56 && m74D == 4) {
            m_jnt.offHeadLock();
        } else {
            m_jnt.onHeadLock();
        }
        if (mCurrMsgNo == 0x1d5b) {
            m750 = 0;
        } else if ((mCurrMsgNo == 0x1d5c && res == 6) || (mCurrMsgNo == 0x1d57 && res == 6)) {
            m750 = 1;
        }
        if (res == 0x12) {
            dComIfGp_event_reset();
            if (field_0x772 != 0) {
                m732 = 3;
                mWaitMode = 2;
                m751 = 0;
            } else {
                m74D = 2;
                mWaitMode = 0;
            }
            m730 = 0;
        }
        break;
    }
    case 2:
        m751 = 0;
        if (board != NULL) {
            ((daMgBoard_c*)board)->reqStartGame();
            ((daMgBoard_c*)board)->setGInfoDraw();
        }
        mWaitMode = 3;
        fopAcM_seStart(this, JA_SE_PLAYSPOT_START, 0);
        mEndGameTimer = 0x3c;
        break;
    case 3:
        if (m732 == 0 && board != NULL) {
            mbGameEnd = ((daMgBoard_c*)board)->checkEndGame() != 0;
            if (mbGameEnd != 0 && cLib_calcTimer(&mEndGameTimer) == 0) {
                mbDidClearGame = ((daMgBoard_c*)board)->checkClearGame();
                if (mbDidClearGame != 0) {
                    mGameBoardScore = ((daMgBoard_c*)board)->getScore();
                } else {
                    m74D = 4;
                }
                mEndGameTimer = 0x5a;
                m778 = 0;
                mWaitMode = 4;
                dComIfGp_event_offHindFlag(2);
                m779 = 1;
            }
        }
        break;
    case 4:
        if (CPad_CHECK_TRIG_A(PAD_1)) {
            m779 = 0;
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_2540);
            if (board != NULL) {
                ((daMgBoard_c*)board)->mbForceEnd = 1;
            }
            if (board != NULL) {
                ((daMgBoard_c*)board)->clrGInfoDraw();
            }
            mWaitMode = 5;
            m732 = 2;
            dComIfGp_evmng_cutEnd(staffId);
            dComIfGp_event_reset();
        }
        break;
    case 5:
        if (m730 == 1) {
            m751 = 1;
            if (talk(1) == 0x12) {
                m732 = 0;
                dComIfGp_event_reset();
                m730 = 0;
                if (mbDidClearGame != 0) {
                    m732 = 5;
                    mWaitMode = 6;
                } else if (field_0x772 != 0) {
                    m732 = 3;
                    mWaitMode = 2;
                } else {
                    m74D = 2;
                    mWaitMode = 0;
                }
            }
        }
        break;
    case 6: {
        if (m732 != 0) {
            break;
        }
        m751 = 0;
        u8 item_tbl[3] = { dItemNo_HEART_PIECE_e, dItemNo_COLLECT_MAP_51_e, dItemNo_PURPLE_RUPEE_e };
        u8 idx = dComIfGs_getEventReg(dSv_event_flag_c::UNK_FE07) - 1;
        idx &= 0xff;
        m77C = fopAcM_createItemForPresentDemo(
            &current.pos, item_tbl[idx], 0, -1,
            fopAcM_GetRoomNo(this), NULL, NULL);
        if (m77C != fpcM_ERROR_PROCESS_ID_e) {
            dComIfGp_event_setItemPartnerId(m77C);
        }
        mWaitMode = 7;
        break;
    }
    case 7:
        if (dComIfGp_evmng_endCheck(m788)) {
            dComIfGp_event_reset();
            field_0x775 = 1;
            m732 = 2;
            mWaitMode = 8;
            m74D = 0xb;
        }
        break;
    case 8:
        m_jnt.onBackBoneLock();
        m_jnt.onHeadLock();
        if (m730 == 1) {
            m751 = 1;
            if (talk(1) == 0x12) {
                dComIfGp_event_reset();
                m730 = 0;
                m732 = 0;
                if (field_0x777 != 0) {
                    m732 = 5;
                    mWaitMode = 9;
                } else {
                    m74D = 2;
                    mWaitMode = 0;
                }
            }
        }
        break;
    case 9: {
        if (m732 != 0) {
            break;
        }
        m751 = 0;
        u8 item_tbl[2] = { dItemNo_COLLECT_MAP_14_e, dItemNo_ORANGE_RUPEE_e };
        u8 idx = dComIfGs_getEventReg(dSv_event_flag_c::UNK_FF07) - 1;
        if (idx > 1) {
            idx = 1;
        }
        m77C = fopAcM_createItemForPresentDemo(&current.pos, item_tbl[idx], 0, -1,
                                               fopAcM_GetRoomNo(this), NULL, NULL);
        if (m77C != fpcM_ERROR_PROCESS_ID_e) {
            dComIfGp_event_setItemPartnerId(m77C);
        }
        mWaitMode = 0xa;
        break;
    }
    case 0xa:
        if (dComIfGp_evmng_endCheck(m788)) {
            dComIfGp_event_reset();
            field_0x776 = 1;
            m732 = 2;
            mWaitMode = 0xb;
        }
        break;
    case 0xb:
        if (m730 == 1) {
            m751 = 1;
            if (talk(1) == 0x12) {
                dComIfGp_event_reset();
                m732 = 0;
                m730 = 0;
                mWaitMode = 0;
                m74D = 2;
            }
        }
        break;
    }

    kg1_talk_camera();
}

/* 00001858-00001878       .text clr_seq_flag__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::clr_seq_flag() {
    field_0x772 = 0;
    mbDidClearGame = 0;
    mbGameEnd = 0;
    field_0x775 = 0;
    field_0x776 = 0;
    field_0x777 = 0;
}

/* 00001878-000019B4       .text getMsg__11daNpc_Kg1_cFv */
u32 daNpc_Kg1_c::getMsg() {
    u32 ret;

    if (mbGameEnd != 0) {
        if (mbDidClearGame != 0) {
            u8 f775 = field_0x775;
            if (f775 != 0 && field_0x776 == 0) {
                if (dComIfGs_getEventReg(dSv_event_flag_c::UNK_BEFF) > mGameBoardScore) {
                    dComIfGs_onEventBit(dSv_event_flag_c::UNK_0E04);
                    dComIfGs_setEventReg(dSv_event_flag_c::UNK_BEFF, mGameBoardScore);
                    u8 val = dComIfGs_getEventReg(dSv_event_flag_c::UNK_FF07);
                    if (val < 3) {
                        val++;
                        dComIfGs_setEventReg(dSv_event_flag_c::UNK_FF07, val);
                    }
                    field_0x777 = 1;
                    ret = 0x1d63;
                } else {
                    ret = 0x1d64;
                }
            } else if (f775 != 0 && field_0x776 != 0) {
                ret = 0x1d64;
            } else {
                ret = 0x1d5f;
            }
        } else {
            ret = 0x1d5d;
        }
    } else if (field_0x770 == 0) {
        ret = 0x1d4d;
    } else {
        ret = 0x1d4e;
    }

    return ret;
}

/* 000019B4-00001C7C       .text next_msgStatus__11daNpc_Kg1_cFPUl */
u16 daNpc_Kg1_c::next_msgStatus(u32* pMsgNo) {
    u16 ret = fopMsgStts_MSG_CONTINUES_e;
    daPy_py_c* player = daPy_getPlayerActorClass();

    switch (*pMsgNo) {
    case 0x1d4d:
        if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_0E04)) {
            *pMsgNo = 0x1d52;
        } else if ((int)dLib_getIplDaysFromSaveTime() < 4) {
            *pMsgNo = 0x1d4f;
        } else if (dComIfGs_isTmpBit(dSv_event_flag_c::UNK_0101)) {
            *pMsgNo = 0x1d50;
        } else {
            dComIfGs_onTmpBit(dSv_event_flag_c::UNK_0101);
            *pMsgNo = 0x1d51;
        }
        field_0x770 = 1;
        break;
    case 0x1d4e:
    case 0x1d4f:
    case 0x1d50:
    case 0x1d51:
        *pMsgNo = 0x1d52;
        break;
    case 0x1d52:
    case 0x1d5e:
        clr_seq_flag();
        switch (mpCurrMsg->mSelectNum) {
        case 0:
            if ((u32)dComIfGs_getRupee() < 10) {
                *pMsgNo = 0x1d54;
            } else {
                m74D = 1;
                dComIfGp_setItemRupeeCount(-10);
                m751 = 1;
                m_jnt.onHeadLock();
                cXyz pos(0.0f, 0.0f, 250.0f);
                player->setPlayerPosAndAngle(&pos, 0x2000);
                *pMsgNo = 0x1d55;
            }
            break;
        case 1:
            *pMsgNo = 0x1d53;
            break;
        }
        break;
    case 0x1d55:
        if (m771 == 0) {
            *pMsgNo = 0x1d56;
        } else {
            *pMsgNo = 0x1d5c;
        }
        field_0x772 = 1;
        break;
    case 0x1d56:
    case 0x1d57:
    case 0x1d58:
    case 0x1d59:
    case 0x1d5d:
        (*pMsgNo)++;
        break;
    case 0x1d5a:
        switch (mpCurrMsg->mSelectNum) {
        case 0:
            *pMsgNo = 0x1d5c;
            break;
        case 1:
            *pMsgNo = 0x1d5b;
            break;
        }
        break;
    case 0x1d5b:
        *pMsgNo = 0x1d57;
        break;
    case 0x1d5f: {
        u8 cnt = dComIfGs_getEventReg(dSv_event_flag_c::UNK_FE07);
        switch (cnt) {
        case 0:
            *pMsgNo = 0x1d60;
            break;
        case 1:
            *pMsgNo = 0x1d61;
            break;
        default:
            *pMsgNo = 0x1d62;
            break;
        }
        if (cnt < 3) {
            cnt++;
        }
        dComIfGs_setEventReg(dSv_event_flag_c::UNK_FE07, cnt);
        break;
    }
    case 0x1d5c:
        m771 = 1;
        ret = fopMsgStts_MSG_ENDS_e;
        break;
    default:
        ret = fopMsgStts_MSG_ENDS_e;
        break;
    }

    return ret;
}

/* 00001C7C-00001CB8       .text anmAtr__11daNpc_Kg1_cFUs */
void daNpc_Kg1_c::anmAtr(unsigned short i_param) {
    static const u8 anm_atr[] = { 1, 2, 4, 5, 6, 7, 8, 9, 10 };

    if (i_param != 6) {
        return;
    }

    u8 atr = dComIfGp_getMesgAnimeAttrInfo();
    if (atr >= 9) {
        return;
    }

    m74D = anm_atr[atr];
    dComIfGp_clearMesgAnimeAttrInfo();
}

/* 00001CB8-00001E60       .text daNpc_Kg1_bcks_setAnm__FPCcP14mDoExt_McaMorfPScPScPScPCiPC14dLib_anm_prm_c */
static void daNpc_Kg1_bcks_setAnm(const char* arcName, mDoExt_McaMorf* morf, s8* pBckIdx, s8* pPrmIdx,
                                   s8* pOldPrmIdx, const int* bcksTbl, const dLib_anm_prm_c* anmPrmTbl) {
    if (*pOldPrmIdx != *pPrmIdx && anmPrmTbl[*pPrmIdx].mAnmIdx != -1) {
        *pBckIdx = anmPrmTbl[*pPrmIdx].mAnmIdx;
        J3DAnmTransform* bck = (J3DAnmTransform*)dComIfG_getObjectRes(arcName, bcksTbl[*pBckIdx]);
        morf->setAnm(bck, anmPrmTbl[*pPrmIdx].mLoopMode, anmPrmTbl[*pPrmIdx].mMorf, anmPrmTbl[*pPrmIdx].mPlaySpeed, 0.0f, -1.0f, NULL);
        if (anmPrmTbl[*pPrmIdx].mPlaySpeed < 0.0f) {
            morf->setFrame(morf->getEndFrame());
        }
    }

    *pOldPrmIdx = *pPrmIdx;

    if (morf->isStop()) {
        if (anmPrmTbl[*pPrmIdx].mNextPrmIdx != -1 && anmPrmTbl[*pPrmIdx].mLoopMode == J3DFrameCtrl::EMode_NONE) {
            *pPrmIdx = anmPrmTbl[*pPrmIdx].mNextPrmIdx;
        }
    }
}

/* 00001E60-00001F8C       .text setAnm__11daNpc_Kg1_cFv */
void daNpc_Kg1_c::setAnm() {
    static const int a_anm_bck_tbl[] = {
        dRes_INDEX_KG_BCK_KG_WAIT01_e,    dRes_INDEX_KG_BCK_KG_WAIT02_e,
        dRes_INDEX_KG_BCK_KG_KWAIT_e,     dRes_INDEX_KG_BCK_KG_TALK01_e,
        dRes_INDEX_KG_BCK_KG_KAMAE_e,     dRes_INDEX_KG_BCK_KG_CAPTAIN_e,
        dRes_INDEX_KG_BCK_KG_CAPTAIN02_e, dRes_INDEX_KG_BCK_KG_KIDS_e,
        dRes_INDEX_KG_BCK_KG_RADIO_e,     dRes_INDEX_KG_BCK_KG_SASIKAE_e,
    };
    static const s16 a_anm_btp_tbl[] = { 0, 0, 0, 0, 1, 0, 1, 1, 2, 3, 0, 0 };

    dLib_anm_prm_c a_anm_prm_tbl[] = {
        { -1, -1, 0, 0.0f, 0.0f, 0 },
        {  0, -1, 0, 8.0f, 1.0f, 2 },
        {  1, -1, 0, 8.0f, 1.0f, 2 },
        {  2, -1, 0, 8.0f, 1.0f, 2 },
        {  3, -1, 0, 8.0f, 1.0f, 2 },
        {  4,  3, 0, 8.0f, 1.0f, 0 },
        {  5, -1, 0, 8.0f, 1.0f, 2 },
        {  6, -1, 0, 8.0f, 1.0f, 2 },
        {  7, -1, 0, 6.0f, 1.0f, 2 },
        {  8, -1, 0, 6.0f, 1.0f, 2 },
        {  9,  8, 0, 8.0f, 1.0f, 0 },
        {  4,  3, 0, 8.0f, -1.0f, 0 },
    };

    if (m74D != m74E) {
        initTexPatternAnm(a_anm_btp_tbl[m74D], true);
    }

    if (m74C == 4 || m74C == 2 || m74C == 5 || m74C == 6 || m74C == 7 || m74C == 8 ||
        m74C == 9) {
        field_0x74f = 1;
        if (m74C == 9 && mpMorf->getFrame() == 31.0f) {
            mBtpAnm.setFrame(1.0f);
        }
    } else {
        field_0x74f = 0;
    }

    daNpc_Kg1_bcks_setAnm(m_arcname, mpMorf, &m74C, &m74D, &m74E, a_anm_bck_tbl, a_anm_prm_tbl);
}

/* 00001F8C-00001FAC       .text daNpc_Kg1Create__FPv */
static cPhs_State daNpc_Kg1Create(void* i_this) {
    return ((daNpc_Kg1_c*)i_this)->_create();
}

/* 00001FAC-0000203C       .text _create__11daNpc_Kg1_cFv */
cPhs_State daNpc_Kg1_c::_create() {
    fopAcM_ct(this, daNpc_Kg1_c);

    cPhs_State state = dComIfG_resLoad(&mPhs, m_arcname);
    if (state == cPhs_COMPLEATE_e) {
        if (fopAcM_entrySolidHeap(this, CheckCreateHeap, 0x10000)) {
            state = CreateInit();
        } else {
            return cPhs_ERROR_e;
        }
    }

    return state;
}


daNpc_Kg1_c::daNpc_Kg1_c() {}

/* 00002480-0000250C       .text daNpc_Kg1Delete__FPv */
static BOOL daNpc_Kg1Delete(void* i_this) {
    daNpc_Kg1_c* this_ = (daNpc_Kg1_c*)i_this;

    dComIfG_resDelete(&this_->mPhs, daNpc_Kg1_c::m_arcname);

    if (this_->heap != NULL && this_->mpMorf != NULL) {
        this_->mpMorf->stopZelAnime();
    }

    if (l_HIO.field_0x8 >= 0) {
        if (--l_HIO.field_0x8 < 0) {
            mDoHIO_deleteChild(l_HIO.mNo);
        }
    }

    return TRUE;
}

/* 0000250C-00002648       .text daNpc_Kg1Execute__FPv */
bool daNpc_Kg1_c::_execute() {
    m_jnt.setParam(l_HIO.mHio[0].mMaxBackboneX, l_HIO.mHio[0].mMaxBackboneY,
                          l_HIO.mHio[0].mMinBackboneX, l_HIO.mHio[0].mMinBackboneY,
                          l_HIO.mHio[0].mMaxHeadX, l_HIO.mHio[0].mMaxHeadY,
                          l_HIO.mHio[0].mMinHeadX, l_HIO.mHio[0].mMinHeadY,
                          l_HIO.mHio[0].mMaxTurnStep);

    checkOrder();
    (this->*mAction)();
    eventOrder();
    playTexPatternAnm();
    setAnm();

    if (m750) {
        mpMorf->play(NULL, 0, 0);
    }

    mObjAcch.CrrPos(*dComIfG_Bgsp());
    setCollision(60.0f, 150.0f);

    attention_info.position.set(current.pos.x, 190.0f + current.pos.y,
                                       current.pos.z);
    eyePos.set(current.pos.x, 150.0f + current.pos.y, current.pos.z);

    lookBack();
    set_mtx();

    return false;
}

static BOOL daNpc_Kg1Execute(void* i_this) {
    return static_cast<daNpc_Kg1_c*>(i_this)->_execute();
}

#include "d/d_snap.h"

/* 00002648-000027CC       .text daNpc_Kg1Draw__FPv */
bool daNpc_Kg1_c::_draw() {
    dSnap_RegistFig(DSNAP_TYPE_UNK81, this, 1.0f, 1.0f, 1.0f);

    J3DModelData* modelData = mpMorf->getModel()->getModelData();
    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(mpMorf->getModel(), &tevStr);
    g_env_light.setLightTevColorType(m6C4, &tevStr);
    m6F8.entry(modelData, m720);
    mpMorf->updateDL();
    m6F8.remove(modelData);

    if (field_0x74f) {
        mBtpAnm.entry(m6C4->getModelData());
        mDoExt_modelUpdateDL(m6C4);
    }

    cXyz shadowPos;
    shadowPos.set(current.pos.x, current.pos.y + 150.0f, current.pos.z);
    mShadowId = dComIfGd_setShadow(mShadowId, 1, mpMorf->getModel(), &shadowPos, 800.0f, 20.0f,
                                           current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd,
                                           &tevStr);

    if (m779) {
        dComIfGp_setDoStatusForce(dActStts_NEXT_e);
    }

    return true;
}

static BOOL daNpc_Kg1Draw(void* i_this) {
    return static_cast<daNpc_Kg1_c*>(i_this)->_draw();
}

/* 000027CC-000027D4       .text daNpc_Kg1IsDelete__FPv */
static BOOL daNpc_Kg1IsDelete(void*) {
    return TRUE;
}

static actor_method_class daNpc_Kg1MethodTable = {
    (process_method_func)daNpc_Kg1Create,
    (process_method_func)daNpc_Kg1Delete,
    (process_method_func)daNpc_Kg1Execute,
    (process_method_func)daNpc_Kg1IsDelete,
    (process_method_func)daNpc_Kg1Draw,
};

actor_process_profile_definition g_profile_NPC_KG1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_KG1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Kg1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_KG1_e,
    /* Actor SubMtd */ &daNpc_Kg1MethodTable,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_0_e,
};
