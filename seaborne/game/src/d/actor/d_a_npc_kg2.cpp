/**
 * d_a_npc_kg2.cpp
 * NPC - Salvatore (Cannon Minigame)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/d_lib.h"
#include "d/actor/d_a_npc_kg2.h"
#include "d/d_com_inf_game.h"
#include "m_Do/m_Do_ext.h"
#include "d/d_cc_d.h"
#include "SSystem/SComponent/c_math.h"
#include "SSystem/SComponent/c_angle.h"
#include "d/d_s_play.h"

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
        /* Radius */ 30.0f,
        /* Height */ 80.0f,
    }},
};


class daNpc_Kg2_HIO_c : public JORReflexible {
public:
    daNpc_Kg2_HIO_c();
    virtual ~daNpc_Kg2_HIO_c() {}

public:
    /* 0x004 */ s8 mNo;
    /* 0x005 */ u8 field_0x5[0x8 - 0x5];
    /* 0x008 */ dNpc_HIO_c mHio;
    /* 0x030 */ u8 m030;
    /* 0x031 */ u8 field_0x31[0x34 - 0x31];
};

static daNpc_Kg2_HIO_c l_HIO;

/* 000000EC-00000194       .text __ct__15daNpc_Kg2_HIO_cFv */
daNpc_Kg2_HIO_c::daNpc_Kg2_HIO_c() {
    mHio.m04 = -20.0f;
    mHio.mMaxHeadX = 0x9c4;
    mHio.mMaxHeadY = 0x1b58;
    mHio.mMaxBackboneX = 0x7d0;
    mHio.mMaxBackboneY = 0x1f40;
    mHio.mMinHeadX = -0x9c4;
    mHio.mMinHeadY = -0x1b58;
    mHio.mMinBackboneX = -0x7d0;
    mHio.mMinBackboneY = -0x1f40;
    mHio.mMaxTurnStep = 0x1000;
    mHio.mMaxHeadTurnVel = 0x3e8;
    mHio.mAttnYOffset = 35.0f;
    mHio.mMaxAttnAngleY = 0x4000;
    mHio.m22 = 0;
    mHio.mMaxAttnDistXZ = 400.0f;
    m030 = 0;
    mNo = -1;
}

#include "res/Object/Kg.h"

static const s32 l_bck_ix_tbl[] = {
    dRes_INDEX_KG_BCK_KG_WAIT01_e,    dRes_INDEX_KG_BCK_KG_WAIT02_e,
    dRes_INDEX_KG_BCK_KG_TALK01_e,    dRes_INDEX_KG_BCK_KG_KAMAE_e,
    dRes_INDEX_KG_BCK_KG_KWAIT_e,     dRes_INDEX_KG_BCK_KG_CAPTAIN_e,
    dRes_INDEX_KG_BCK_KG_CAPTAIN02_e, dRes_INDEX_KG_BCK_KG_KIDS_e,
    dRes_INDEX_KG_BCK_KG_RADIO_e,     dRes_INDEX_KG_BCK_KG_SASIKAE_e,
    dRes_INDEX_KG_BCK_KG_GONZO_e,     dRes_INDEX_KG_BCK_KG_TETORA_e,
    dRes_INDEX_KG_BCK_KG_KAMAE_e,     dRes_INDEX_KG_BCK_KG_KWAIT_e,
    dRes_INDEX_KG_BCK_KG_TETORA02_e,
};

static const s32 l_btp_ix_tbl[] = {
    dRes_INDEX_KG_BTP_KG_e,    dRes_INDEX_KG_BTP_MOPEN_e, dRes_INDEX_KG_BTP_WARAI_e,
    dRes_INDEX_KG_BTP_RADIO_e, dRes_INDEX_KG_BTP_SLEEP_e,
};

/* 00000194-00000368       .text nodeCallBack__FP7J3DNodei */
static BOOL nodeCallBack(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Kg2_c* actor = (daNpc_Kg2_c*)model->getUserArea();
        if (actor != NULL) {
            u16 jntNo = ((J3DJoint*)i_node)->getJntNo();
            mDoMtx_stack_c::copy(model->getAnmMtx(jntNo));

            if (jntNo == actor->m_jnt.getHeadJntNum()) {
                cXyz eyeOffset(24.0f + REG10_F(0), 5.0f + REG10_F(1), REG10_F(2));
                static cXyz l_offsetEyePos(24.0f, -16.0f, 0.0f);

                mDoMtx_stack_c::multVec(&eyeOffset, &actor->field_0x714);
                mDoMtx_stack_c::XrotM(+actor->m_jnt.getHead_y());
                mDoMtx_stack_c::ZrotM(-actor->m_jnt.getHead_x());
                mDoMtx_stack_c::multVec(&l_offsetEyePos, &actor->field_0x708);
                mDoMtx_stack_c::multVec(&eyeOffset, &actor->attention_info.position);
                actor->attention_info.position.y += l_HIO.mHio.mAttnYOffset;
            } else if (jntNo == actor->m_jnt.getBackboneJntNum()) {
                mDoMtx_stack_c::XrotM(+actor->m_jnt.getBackbone_y());
                mDoMtx_stack_c::ZrotM(-actor->m_jnt.getBackbone_x());
            }

            cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
            model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
        }
    }

    return TRUE;
}

/* 000003A4-0000048C       .text set_mtx__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::set_mtx() {
    J3DModel* model = mpMorf->getModel();

    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    model->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();

    if (field_0x736) {
        mDoMtx_stack_c::copy(model->getAnmMtx(m_handL_num));
        mDoMtx_stack_c::transM(23.46f, -22.26f, -47.05f);
        mDoMtx_stack_c::XYZrotM(0x1f4b, -0x4f00, 0x1f4b);
        mpPlateModel->setBaseTRMtx(mDoMtx_stack_c::get());
    }
}

/* 0000048C-000005A0       .text initTexPatternAnm__11daNpc_Kg2_cFb */
BOOL daNpc_Kg2_c::initTexPatternAnm(bool i_modify) {
    J3DModelData* modelData = mpMorf->getModel()->getModelData();

    m_btp = (J3DAnmTexPattern*)dComIfG_getObjectRes("Kg", l_btp_ix_tbl[field_0x748]);
    JUT_ASSERT(0x12b, m_btp != 0);

    if (!mBtpAnm2.init(modelData, m_btp, 1, 2, 1.0f, 0, -1, i_modify, 0)) {
        return FALSE;
    }

    field_0x704 = 0;
    field_0x706 = 0;

    return TRUE;
}

/* 000005A0-000006EC       .text playTexPatternAnm__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::playTexPatternAnm() {
    if (field_0x738 == 1 && field_0x748 == 0 && field_0x704 == 2) {
        field_0x704 = 0;
        field_0x748 = 4;
        initTexPatternAnm(true);
        return;
    }

    if (cLib_calcTimer(&field_0x706) == 0) {
        if (field_0x748 == 4) {
            if (field_0x704 == 0) {
                field_0x704 = 1;
                field_0x706 = cM_rndF(150.0f) + 150.0f;
            } else {
                field_0x704 = 0;
                field_0x706 = (s16)(cM_rndF(150.0f) + 150.0f) * 2;
            }
        } else {
            if (field_0x704 >= m_btp->getFrameMax()) {
                field_0x704 -= m_btp->getFrameMax();
                field_0x706 = cM_rndF(100.0f) + 30.0f;
            } else {
                field_0x704++;
            }
        }
    }
}

/* 000006EC-00000838       .text setAnm__11daNpc_Kg2_cFScf */
void daNpc_Kg2_c::setAnm(s8 i_animIdx, f32 i_morf) {
    static u32 a_play_mode_tbl[] = {
        J3DFrameCtrl::EMode_LOOP, J3DFrameCtrl::EMode_LOOP, J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_NONE, J3DFrameCtrl::EMode_LOOP, J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_LOOP, J3DFrameCtrl::EMode_LOOP, J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_NONE, J3DFrameCtrl::EMode_LOOP, J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_NONE, J3DFrameCtrl::EMode_NONE, J3DFrameCtrl::EMode_LOOP,
    };
    static f32 a_morf_frame_tbl[] = {
        8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f
    };
    static f32 a_play_speed_tbl[] = {
        1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, -1.0f, 1.0f, 1.0f
    };

    f32 morf = i_morf;
    if (morf < 0.0f) {
        morf = a_morf_frame_tbl[i_animIdx];
    }

    if (i_animIdx != field_0x749 && field_0x749 != -1) {
        field_0x749 = i_animIdx;
        dNpc_setAnm(mpMorf, a_play_mode_tbl[field_0x749], morf, a_play_speed_tbl[field_0x749],
                    l_bck_ix_tbl[field_0x749], -1, "Kg");
        if (field_0x749 == 12) {
            mpMorf->setFrame(mpMorf->getEndFrame() - 1.0f);
            mpMorf->setPlaySpeed(-1.0f);
        }
    }

    if (field_0x749 == 2) {
        m_jnt.offHeadLock();
        m_jnt.onBackBoneLock();
    } else {
        m_jnt.onHeadLock();
        m_jnt.onBackBoneLock();
    }
}

/* 00000838-000009E8       .text subAnm__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::subAnm() {
    if (field_0x748 == 4) {
        mDoAud_seStart(JA_SE_CV_KG_SLEEP, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    }

    if ((field_0x749 == 3 || field_0x749 == 13) && mpMorf->checkFrame(1.0f)) {
        field_0x736 = 1;
        if (field_0x749 == 3) {
            field_0x737 = 3;
        } else {
            field_0x737 = 4;
        }
    } else if (field_0x749 == 9 && mpMorf->checkFrame(30.0f)) {
        field_0x736 = 1;
        field_0x737 = 2;
    } else if (field_0x749 == 12 && mpMorf->checkFrame(1.0f)) {
        field_0x736 = 0;
        setAnm(1, -1.0f);
    } else if (field_0x749 == 13 && mpMorf->checkFrame(mpMorf->getEndFrame() - 1.0f)) {
        setAnm(4, -1.0f);
    }
}

/* 000009E8-00000B3C       .text chkAttention__11daNpc_Kg2_cF4cXyzs */
bool daNpc_Kg2_c::chkAttention(cXyz i_pos, s16 i_angle) {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    f32 maxAttnDistXZ = l_HIO.mHio.mMaxAttnDistXZ;
    int maxAttnAngleY = l_HIO.mHio.mMaxAttnAngleY;

    cXyz delta;
    delta.x = player->current.pos.x - i_pos.x;
    delta.z = player->current.pos.z - i_pos.z;
    f32 distXZ = std::sqrtf(delta.x * delta.x + delta.z * delta.z);
    s16 targetAngleY = cM_atan2s(delta.x, delta.z);
    if (field_0x72c) {
        maxAttnDistXZ += 40.0f;
        maxAttnAngleY += cAngle::d2s(10.0f);
    }
    targetAngleY -= i_angle;
    return maxAttnAngleY > abs(targetAngleY) && maxAttnDistXZ > distXZ;
}

/* 00000B3C-00000BC0       .text eventOrder__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::eventOrder() {
    s8 stt = field_0x750;
    if (stt == 1 || stt == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x750 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (stt == 3) {
        fopAcM_orderOtherEventId(this, mEventIdx[field_0x754], 0xff, 0xffff, 0, 1);
    }
}

/* 00000BC0-00000CC4       .text checkOrder__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::checkOrder() {
    if (eventInfo.checkCommandDemoAccrpt()) {
        setAction(&daNpc_Kg2_c::event_wait_action, NULL);
        field_0x750 = 0;
    } else if (eventInfo.checkCommandTalk()) {
        if (field_0x750 == 1 || field_0x750 == 2) {
            field_0x750 = 0;
            field_0x72d = 1;
        }
    }
}

/* 00000CC4-00000E38       .text getMsg__11daNpc_Kg2_cFv */
u32 daNpc_Kg2_c::getMsg() {
    if (field_0x74c != 0) {
        u8 reg = dComIfGs_getEventReg(0xb703);
        if (field_0x74c == 0x3145) {
            if (field_0x735 == 1) {
                field_0x74c = 0x314b;
            } else if (dComIfGs_isEventBit(0x2508)) {
                field_0x74c = 0x314c;
            }
        } else if (field_0x74c == 0x3150) {
            if (reg != 0) {
                field_0x74c = 0x3154;
            }
        } else if (field_0x74c == 0x3152) {
            if (reg == 1) {
                field_0x74c = 0x3156;
            } else if (reg >= 2) {
                field_0x74c = 0x3158;
            }
        }
        u32 ret = field_0x74c;
        field_0x74c = 0;
        return ret;
    } else {
        if (field_0x738 == 1) {
            return 0x315b;
        }
        if (dComIfGs_isEventBit(0x2540) && !dComIfGs_isEventBit(0x2520)) {
            dComIfGs_onEventBit(0x2520);
            return 0x3140;
        }
        if (field_0x734 == 0) {
            field_0x734 = 1;
            return 0x3139;
        }
        return 0x313a;
    }
}

/* 00000E38-00001028       .text next_msgStatus__11daNpc_Kg2_cFPUl */
u16 daNpc_Kg2_c::next_msgStatus(u32* pMsgNo) {
    u16 msgStatus = fopMsgStts_MSG_CONTINUES_e;
    switch (*pMsgNo) {
        case 0x3140:
        case 0x3145:
        case 0x3146:
        case 0x3147:
        case 0x3148:
        case 0x314C:
        case 0x314E:
        case 0x3150:
        case 0x3152:
        case 0x3154:
        case 0x3156:
        case 0x3158:
            *pMsgNo += 1;
            break;
        case 0x314D:
            *pMsgNo = 0x3147;
            break;
        case 0x3139:
            if (dComIfGs_getEventReg(dSv_event_flag_c::UNK_B703) == 0) {
                *pMsgNo = 0x313E;
            } else if ((int)dLib_getIplDaysFromSaveTime() < 4) {
                *pMsgNo = 0x313B;
            } else if (dComIfGs_isTmpBit(dSv_event_tmp_flag_c::UNK_0102)) {
                *pMsgNo = 0x313C;
            } else {
                dComIfGs_onTmpBit(dSv_event_tmp_flag_c::UNK_0102);
                *pMsgNo = 0x313D;
            }
            break;
        case 0x3149:
            if (mpCurrMsg->mSelectNum == 0) {
                dComIfGs_onEventBit(dSv_event_flag_c::UNK_2508);
                field_0x735 = 1;
                *pMsgNo = 0x314B;
            } else {
                *pMsgNo = 0x314A;
            }
            break;
        case 0x314A:
            *pMsgNo = 0x3148;
            break;
        case 0x313A:
        case 0x313B:
        case 0x313C:
        case 0x313D:
        case 0x313E:
            *pMsgNo = 0x313F;
            break;
        case 0x313F:
        case 0x314F:
            if (mpCurrMsg->mSelectNum == 0) {
                if (dComIfGs_getRupee() < 50) {
                    *pMsgNo = 0x3143;
                } else {
                    dComIfGp_setItemRupeeCount(-50);
                    dComIfGp_setAStatusForce(0x19);
                    *pMsgNo = 0x3144;
                }
            } else {
                *pMsgNo = 0x3142;
            }
            break;
        case 0x3141:
            if (field_0x734 == 0) {
                field_0x734 = 1;
                *pMsgNo = 0x3139;
            } else {
                *pMsgNo = 0x313A;
            }
            break;
        default:
            msgStatus = fopMsgStts_MSG_ENDS_e;
            break;
    }
    return msgStatus;
}

/* 00001028-000012EC       .text anmAtr__11daNpc_Kg2_cFUs */
void daNpc_Kg2_c::anmAtr(unsigned short) {
    u8 mesg_anime = g_dComIfG_gameInfo.play.mMesgAnime;

    if (this->field_0x738 == 1) {
        return;
    }

    switch (mesg_anime) {
    case 0:
        this->setAnm(0, -1.0f);
        this->field_0x748 = 0;
        this->initTexPatternAnm(true);
        break;
    case 1:
        this->setAnm(1, -1.0f);
        this->field_0x748 = 0;
        this->initTexPatternAnm(true);
        break;
    case 2:
        this->setAnm(2, -1.0f);
        this->field_0x748 = 1;
        this->initTexPatternAnm(true);
        break;
    case 3:
        this->setAnm(3, -1.0f);
        this->field_0x748 = 0;
        this->initTexPatternAnm(true);
        break;
    case 4:
        this->setAnm(5, -1.0f);
        this->field_0x748 = 1;
        this->initTexPatternAnm(true);
        break;
    case 5:
        this->setAnm(6, -1.0f);
        this->field_0x748 = 1;
        this->initTexPatternAnm(true);
        break;
    case 6:
        this->setAnm(7, -1.0f);
        this->field_0x748 = 2;
        this->initTexPatternAnm(true);
        break;
    case 7:
        this->setAnm(8, -1.0f);
        this->field_0x748 = 3;
        this->initTexPatternAnm(true);
        break;
    case 8:
        this->setAnm(9, -1.0f);
        this->field_0x748 = 0;
        this->initTexPatternAnm(true);
        break;
    case 9:
        this->setAnm(0xa, -1.0f);
        this->field_0x748 = 1;
        this->initTexPatternAnm(true);
        break;
    case 10:
        this->setAnm(0xb, -1.0f);
        this->field_0x748 = 2;
        this->initTexPatternAnm(true);
        break;
    case 11:
        this->setAnm(0xe, -1.0f);
        this->field_0x748 = 2;
        this->initTexPatternAnm(true);
        break;
    }

    if (this->field_0x749 == 3) {
        if (this->mpMorf->checkFrame(this->mpMorf->getEndFrame() - 1.0f)) {
            this->setAnm(4, -1.0f);
            this->field_0x748 = 0;
            this->initTexPatternAnm(true);
        }
    }

    g_dComIfG_gameInfo.play.mMesgAnime = 0xff;
}

/* 000012EC-00001308       .text setAttention__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::setAttention() {
    eyePos.set(field_0x708.x, field_0x708.y, field_0x708.z);
}

/* 00001308-00001448       .text lookBack__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::lookBack() {
    cXyz tgtPos;
    cXyz pos(0.0f, 0.0f, 0.0f);
    cXyz* pTgtPos = NULL;
    s16 angleY = current.angle.y;

    switch (field_0x760) {
    case 1:
    case 2:
        if (field_0x760 == 2) {
            m_jnt.setTrn();
        }

        if (field_0x72c) {
            tgtPos = dNpc_playerEyePos(l_HIO.mHio.m04);
            pTgtPos = &tgtPos;
            pos = current.pos;
            pos.y = eyePos.y;
        }
        break;
    }

    if (m_jnt.trnChk()) {
        cLib_addCalcAngleS2(&field_0x720, l_HIO.mHio.mMaxHeadTurnVel, 4, 0x800);
    } else {
        field_0x720 = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, pTgtPos, pos, angleY, field_0x720, true);
}

/* 00001448-00001468       .text CallbackCreateHeap__FP10fopAc_ac_c */
static BOOL CallbackCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Kg2_c*)i_this)->CreateHeap();
}

#include "res/Object/Kg.h"
#include "d/d_com_inf_game.h"
#include "f_op/f_op_actor_mng.h"

/* 00001468-0000180C       .text CreateHeap__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::CreateHeap() {
    J3DModelData* modelData = static_cast<J3DModelData*>(dComIfG_getObjectRes("Kg", dRes_INDEX_KG_BDL_KG_e));
    JUT_ASSERT(913, modelData != 0);

    mpMorf = new mDoExt_McaMorf(
        modelData, NULL, NULL,
        static_cast<J3DAnmTransform*>(dComIfG_getObjectRes("Kg", dRes_INDEX_KG_BCK_KG_WAIT02_e)),
        J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, 1, NULL, 0, 0x11020203
    );
    if (mpMorf == NULL || mpMorf->getModel() == NULL) {
        return FALSE;
    }

    m_jnt.setHeadJntNum(modelData->getJointName()->getIndex("head"));
    JUT_ASSERT(929, m_jnt.getHeadJntNum() >= 0);
    m_jnt.setBackboneJntNum(modelData->getJointName()->getIndex("backbone2"));
    JUT_ASSERT(934, m_jnt.getBackboneJntNum() >= 0);
    m_handL_num = modelData->getJointName()->getIndex("handL");
    JUT_ASSERT(938, m_handL_num >= 0);

    field_0x748 = 0;
    if (!initTexPatternAnm(false)) {
        return FALSE;
    }

    modelData = static_cast<J3DModelData*>(dComIfG_getObjectRes("Kg", dRes_INDEX_KG_BDL_KG_PLATE_e));
    mpPlateModel = mDoExt_J3DModel__create(modelData, 0x80000, 0x11020002);
    if (mpPlateModel == NULL) {
        return FALSE;
    }

    if (!mBtpAnm.init(
            modelData,
            static_cast<J3DAnmTexPattern*>(dComIfG_getObjectRes("Kg", dRes_INDEX_KG_BTP_KG_PLATE_e)),
            TRUE, 2, 0.0f, 0, -1, false
        )) {
        return FALSE;
    }

    modelData = mpMorf->getModel()->getModelData();
    modelData->getJointNodePointer(m_jnt.getHeadJntNum())->setCallBack(nodeCallBack);
    modelData->getJointNodePointer(m_jnt.getBackboneJntNum())->setCallBack(nodeCallBack);
#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif
    mAcchCir.SetWall(30.0f, 0.0f);
    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir, fopAcM_GetSpeed_p(this));

    return TRUE;
}

/* 0000180C-00001A9C       .text CreateInit__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::CreateInit() {
    field_0x724 = current.angle;

    attention_info.flags = fopAc_Attn_LOCKON_TALK_e | fopAc_Attn_ACTION_SPEAK_e;
    attention_info.distances[1] = 0x6E;
    attention_info.distances[3] = 0x6E;
    gravity = -30.0f;

    setAction(&daNpc_Kg2_c::wait_action, NULL);

    field_0x708.x = current.pos.x;
    field_0x708.y = current.pos.y;
    field_0x708.z = current.pos.z;

    mStts.Init(0xFF, 0xFF, this);
    mCyl.Set(l_cyl_src);
    mCyl.SetStts(&mStts);
    setCollision(60.0f, 150.0f);
    mEventCut.setActorInfo2("Kg2", this);
    mEventCut.setJntCtrlPtr(&m_jnt);

    field_0x734 = 0;
    field_0x735 = 0;
    field_0x736 = 0;
    field_0x737 = 0;
    field_0x74c = 0;
    m_jnt.onHeadLock();
    m_jnt.onBackBoneLock();
    field_0x750 = 0;
    field_0x754 = 4;

    dEvent_manager_c* evtMgr = dComIfGp_getPEvtManager();
    mEventIdx[0] = evtMgr->getEventIdx("KG2_START_DEMO", 0xFF);
    mEventIdx[1] = evtMgr->getEventIdx("KG2_CLEAR_DEMO", 0xFF);
    mEventIdx[2] = evtMgr->getEventIdx("KG2_GETDEMO", 0xFF);
    mEventIdx[3] = evtMgr->getEventIdx("CANON_GAME", 0xFF);

    set_mtx();

    f32 time = dComIfGs_getTime();
    if (time >= 105.0f && time < 300.0f) {
        field_0x738 = 0;
        setAnm(1, -1.0f);
    } else {
        field_0x738 = 1;
        setAnm(0xD, 0.0f);
    }

    l_kg2_pointer = this;
    return TRUE;
}

/* 00001A9C-00001BDC       .text wait01__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::wait01() {
    f32 time = dComIfGs_getTime();
    if (time >= 105.0f && time < 300.0f) {
        if (field_0x738 == 1) {
            field_0x738 = 0;
            setAnm(12, -1.0f);
            field_0x748 = 0;
            initTexPatternAnm(true);
        }
    } else {
        if (field_0x738 == 0) {
            field_0x738 = 1;
            setAnm(13, -1.0f);
            field_0x748 = 0;
            initTexPatternAnm(true);
        }
    }

    if (field_0x72d) {
        field_0x760 = 2;
    } else {
        fopAc_ac_c* player = dComIfGp_getPlayer(0);
        f32 heightDiff = std::fabsf(current.pos.y - player->current.pos.y);
        if (field_0x72c && heightDiff < 50.0f && field_0x749 != 13 && field_0x749 != 12) {
            field_0x750 = 2;
        }
    }
}

/* 00001BDC-00001CA4       .text talk01__11daNpc_Kg2_cFv */
void daNpc_Kg2_c::talk01() {
    if (talk(1) == fopMsgStts_BOX_CLOSED_e) {
        field_0x760 = 1;
        dComIfGp_event_reset();
        field_0x72d = 0;

        if (mCurrMsgNo == 0x3144) {
            field_0x750 = 3;
            field_0x754 = 0;
        } else if (field_0x738 == 0) {
            setAnm(1, -1.0f);
            field_0x748 = 0;
            initTexPatternAnm(true);
        } else {
            setAnm(4, -1.0f);
        }
    }
}

/* 00001CA4-00001D88       .text evn_setAnm_init__11daNpc_Kg2_cFi */
BOOL daNpc_Kg2_c::evn_setAnm_init(int i_staffIdx) {
    int* anmNo = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "AnmNo");
    int* count = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "count");
    f32* hokan = dComIfGp_evmng_getMyFloatP(i_staffIdx, "hokan");

    if (anmNo != NULL) {
        f32 morf = -1.0f;
        if (hokan != NULL) {
            morf = *hokan;
        }

        setAnm(*anmNo, morf);

        if (count != NULL) {
            field_0x739 = *count;
        } else {
            field_0x739 = 0;
        }
    }

    return TRUE;
}

/* 00001D88-00001E4C       .text evn_setAnm__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::evn_setAnm() {
    if (field_0x749 == 12) {
        if (mpMorf->checkFrame(1.0f)) {
            field_0x739--;
        }
    } else {
        if (mpMorf->checkFrame(mpMorf->getEndFrame() - 1.0f)) {
            field_0x739--;
        }
    }

    return field_0x739 <= 0 ? TRUE : FALSE;
}

/* 00001E4C-00001F14       .text evn_jnt_lock_init__11daNpc_Kg2_cFi */
BOOL daNpc_Kg2_c::evn_jnt_lock_init(int i_staffIdx) {
    int* prm = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "prm");
    int mode = 0;
    if (prm != NULL) {
        mode = *prm;
    }

    switch (mode) {
    case 0:
        m_jnt.offHeadLock();
        m_jnt.offBackBoneLock();
        break;
    case 1:
        m_jnt.onHeadLock();
        m_jnt.offBackBoneLock();
        break;
    case 2:
        m_jnt.offHeadLock();
        m_jnt.onBackBoneLock();
        break;
    case 3:
        m_jnt.onHeadLock();
        m_jnt.onBackBoneLock();
        break;
    }

    return TRUE;
}

/* 00001F14-00001F88       .text evn_talk_init__11daNpc_Kg2_cFi */
BOOL daNpc_Kg2_c::evn_talk_init(int i_staffIdx) {
    int* msgNo = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "msg_num");

    mCurrMsgBsPcId = -1;
    mpCurrMsg = NULL;

    if (msgNo != NULL) {
        field_0x74c = *msgNo;
    } else {
        field_0x74c = 0;
    }

    return TRUE;
}

/* 00001F88-00001FBC       .text evn_talk__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::evn_talk() {
    return talk(1) == fopMsgStts_BOX_CLOSED_e ? TRUE : FALSE;
}

/* 00001FBC-00002070       .text evn_createItem_init__11daNpc_Kg2_cFi */
BOOL daNpc_Kg2_c::evn_createItem_init(int) {
    u8 itemNo;
    switch (dComIfGs_getEventReg(dSv_event_flag_c::UNK_B703)) {
    case 0:
    case 1:
        itemNo = 7;
        break;
    case 2:
        itemNo = 0xd7;
        break;
    case 3:
    default:
        itemNo = 6;
        break;
    }

    fpc_ProcID id = fopAcM_createItemForPresentDemo(&current.pos, itemNo, 0, -1,
                                                    fopAcM_GetRoomNo(this), NULL, NULL);
    if (id != fpcM_ERROR_PROCESS_ID_e) {
        dComIfGp_event_setItemPartnerId(id);
    }

    return TRUE;
}

/* 00002070-000021EC       .text privateCut__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::privateCut() {
    static char* cut_name_tbl[] = { "SETANM", "JNTLOCK", "SHOWITEM", "KGTALK", "CREATEITEM" };

    int staffIdx = dComIfGp_evmng_getMyStaffId(mEventCut.getActorName(), NULL, 0);
    if (staffIdx == -1) {
        return FALSE;
    }

    int actIdx = dComIfGp_evmng_getMyActIdx(staffIdx, cut_name_tbl, ARRAY_SIZE(cut_name_tbl), TRUE, 0);
    if (actIdx == -1) {
        dComIfGp_evmng_cutEnd(staffIdx);
    } else {
        if (dComIfGp_evmng_getIsAddvance(staffIdx)) {
            switch (actIdx) {
            case 0:
                evn_setAnm_init(staffIdx);
                break;
            case 1:
                evn_jnt_lock_init(staffIdx);
                break;
            case 2:
                break;
            case 3:
                evn_talk_init(staffIdx);
                break;
            case 4:
                evn_createItem_init(staffIdx);
                break;
            }
        }

        BOOL done;
        switch (actIdx) {
        case 0:
            done = evn_setAnm();
            break;
        case 2:
            done = TRUE;
            break;
        case 3:
            done = evn_talk();
            break;
        default:
            done = TRUE;
            break;
        }

        if (done) {
            dComIfGp_evmng_cutEnd(staffIdx);
        }
    }

    return TRUE;
}

/* 000021EC-00002250       .text processMove__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::processMove() {
    (this->*mCurrActionFunc)(NULL);

    if (mEventCut.cutProc() || privateCut()) {
        return TRUE;
    }

    return FALSE;
}

/* 00002250-00002334       .text wait_action__11daNpc_Kg2_cFPv */
BOOL daNpc_Kg2_c::wait_action(void*) {
    if (mActionStatus == ACTION_STARTING) {
        field_0x760 = 1;
        mActionStatus++;
    } else if (mActionStatus != ACTION_ENDING) {
        s16 ang = current.angle.y + m_jnt.getHead_y() + m_jnt.getBackbone_y();
        field_0x72c = chkAttention(current.pos, ang);
        field_0x750 = 0;

        switch (field_0x760) {
        case 1:
            wait01();
            break;
        case 2:
            talk01();
            break;
        }

        lookBack();
        setAttention();
    }

    return TRUE;
}

/* 00002334-00002590       .text event_wait_action__11daNpc_Kg2_cFPv */
BOOL daNpc_Kg2_c::event_wait_action(void*) {
    if (mActionStatus == ACTION_STARTING) {
        field_0x760 = 1;
        mActionStatus++;
    } else if (mActionStatus != ACTION_ENDING) {
        s16 ang = current.angle.y + m_jnt.getHead_y() + m_jnt.getBackbone_y();
        field_0x72c = chkAttention(current.pos, ang);
        lookBack();
        setAttention();

        if (dComIfGp_evmng_endCheck(mEventIdx[field_0x754])) {
            if (field_0x754 == 0) {
                field_0x750 = 3;
                field_0x754 = 3;
            } else if (field_0x754 == 3) {
                if ((u8)canon_game_result == 1) {
                    field_0x750 = 3;
                    field_0x754 = 1;
                } else {
                    field_0x750 = 1;
                    field_0x74c = 0x314e;
                }
            } else if (field_0x754 == 1) {
                field_0x750 = 3;
                field_0x754 = 2;

                u8 v = dComIfGs_getEventReg(dSv_event_flag_c::UNK_B703);
                dComIfGs_setEventReg(dSv_event_flag_c::UNK_B703, v + 1 > 3 ? 3 : v + 1);
            } else if (field_0x754 == 2) {
                field_0x750 = 1;
                field_0x74c = 0x315a;
            } else {
                field_0x754 = 4;
            }

            dComIfGp_event_reset();
            setAction(&daNpc_Kg2_c::wait_action, NULL);
        }
    }

    return TRUE;
}

/* 00002590-000027BC       .text _create__11daNpc_Kg2_cFv */
cPhs_State daNpc_Kg2_c::_create() {
    fopAcM_ct_Retail(this, daNpc_Kg2_c);

    cPhs_State ret = dComIfG_resLoad(&mPhase, "Kg");
    if (ret == cPhs_COMPLEATE_e) {
        if (!fopAcM_entrySolidHeap(this, CallbackCreateHeap, 0x2D00)) {
            return cPhs_ERROR_e;
        }

        fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());

        if (l_HIO.mNo < 0) {
            l_HIO.mNo = mDoHIO_createChild("海戦ゲーム親父２", &l_HIO);
        }

        if (!CreateInit()) {
            return cPhs_ERROR_e;
        }
    }

    return ret;
}

/* 00002B6C-00002BFC       .text _delete__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::_delete() {
    dComIfG_resDelete(&mPhase, "Kg");

    if (heap != NULL && mpMorf != NULL) {
        mpMorf->stopZelAnime();
    }

    l_kg2_pointer = NULL;

    if (l_HIO.mNo >= 0) {
        mDoHIO_deleteChild(l_HIO.mNo);
        l_HIO.mNo = -1;
    }

    return TRUE;
}

/* 00002BFC-00002D14       .text _execute__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::_execute() {
    m_jnt.setParam(l_HIO.mHio.mMaxBackboneX, l_HIO.mHio.mMaxBackboneY, l_HIO.mHio.mMinBackboneX,
                   l_HIO.mHio.mMinBackboneY, l_HIO.mHio.mMaxHeadX, l_HIO.mHio.mMaxHeadY,
                   l_HIO.mHio.mMinHeadX, l_HIO.mHio.mMinHeadY, l_HIO.mHio.mMaxTurnStep);

    playTexPatternAnm();
    mpMorf->play(&eyePos, 0, 0);
    mpMorf->calc();

    checkOrder();
    processMove();
    eventOrder();
    subAnm();

    fopAcM_posMoveF(this, mStts.GetCCMoveP());
    mObjAcch.CrrPos(*dComIfG_Bgsp());
    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    set_mtx();
    setCollision(60.0f, 150.0f);

    return TRUE;
}

#include "d/d_snap.h"

/* 00002D14-00002E74       .text _draw__11daNpc_Kg2_cFv */
BOOL daNpc_Kg2_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);

    mBtpAnm2.entry(modelData, field_0x704);
    mpMorf->entryDL();
    modelData->getMaterialTable().removeTexNoAnimator(mBtpAnm2.getBtpAnm());

    if (field_0x736) {
        J3DModelData* plateData = mpPlateModel->getModelData();
        g_env_light.setLightTevColorType(mpPlateModel, &tevStr);
        mBtpAnm.entry(plateData, field_0x737);
        mDoExt_modelUpdateDL(mpPlateModel);
        plateData->getMaterialTable().removeTexNoAnimator(mBtpAnm.getBtpAnm());
    }

    cXyz pos(current.pos.x, 150.0f + current.pos.y, current.pos.z);
    field_0x6d0 = dComIfGd_setShadow(field_0x6d0, 1, mpMorf->getModel(), &pos, 800.0f, 20.0f,
                                     current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);

    dSnap_RegistFig(0x81, this, current.pos, current.angle.y, 1.0f, 1.0f, 1.0f);

    return TRUE;
}

/* 00002E74-00002E94       .text daNpc_Kg2_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Kg2_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Kg2_c*)i_this)->_create();
}

/* 00002E94-00002EB4       .text daNpc_Kg2_Delete__FP11daNpc_Kg2_c */
static BOOL daNpc_Kg2_Delete(daNpc_Kg2_c* i_this) {
    return ((daNpc_Kg2_c*)i_this)->_delete();
}

/* 00002EB4-00002ED4       .text daNpc_Kg2_Execute__FP11daNpc_Kg2_c */
static BOOL daNpc_Kg2_Execute(daNpc_Kg2_c* i_this) {
    return ((daNpc_Kg2_c*)i_this)->_execute();
}

/* 00002ED4-00002EF4       .text daNpc_Kg2_Draw__FP11daNpc_Kg2_c */
static BOOL daNpc_Kg2_Draw(daNpc_Kg2_c* i_this) {
    return ((daNpc_Kg2_c*)i_this)->_draw();
}

/* 00002EF4-00002EFC       .text daNpc_Kg2_IsDelete__FP11daNpc_Kg2_c */
static BOOL daNpc_Kg2_IsDelete(daNpc_Kg2_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Kg2_Method = {
    (process_method_func)daNpc_Kg2_Create,
    (process_method_func)daNpc_Kg2_Delete,
    (process_method_func)daNpc_Kg2_Execute,
    (process_method_func)daNpc_Kg2_IsDelete,
    (process_method_func)daNpc_Kg2_Draw,
};

actor_process_profile_definition g_profile_NPC_KG2 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_KG2_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Kg2_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_KG2_e,
    /* Actor SubMtd */ &l_daNpc_Kg2_Method,
    /* Status       */ fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_12_e,
};
