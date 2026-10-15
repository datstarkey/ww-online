/**
 * d_a_npc_uk.cpp
 * NPC - Jin, Jan, & Jun-Roberto
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_npc_uk.h"
#if TARGET_PC
#include "d/actor/d_a_npc_mk.h"
// The partner Mk's daNpc_Mk_c::mVisitMode, read at its GameCube offset 0x6A0 by the original.
#define UK_MK_VISIT_MODE(a) (static_cast<daNpc_Mk_c*>(a)->mVisitMode)
#else
#define UK_MK_VISIT_MODE(a) (*((u8*)(a) + 0x6a0))
#endif
#include "m_Do/m_Do_ext.h"

/* 000000EC-000000F8       .text getType__10daNpc_Uk_cFv */
int daNpc_Uk_c::getType() {
    return fopAcM_GetParamBit(fopAcM_GetParam(this), 0, 8);
}

/* 000000F8-00000104       .text getPath__10daNpc_Uk_cFv */
u32 daNpc_Uk_c::getPath() {
    return fopAcM_GetParamBit(fopAcM_GetParam(this), 8, 8);
}

/* 00000104-00000110       .text getShapeType__10daNpc_Uk_cFv */
int daNpc_Uk_c::getShapeType() {
    return fopAcM_GetParamBit(fopAcM_GetParam(this), 16, 4);
}

/* 00000110-0000015C       .text getCaughtFlag__10daNpc_Uk_cFv */
u16 daNpc_Uk_c::getCaughtFlag() {
    switch (getShapeType()) {
    case 1:
        return 8;
    case 2:
        return 4;
    default:
        return 0x10;
    }
}

/* 0000015C-000001A8       .text getFoundFlag__10daNpc_Uk_cFv */
u16 daNpc_Uk_c::getFoundFlag() {
    switch (getShapeType()) {
    case 1:
        return 0x180;
    case 2:
        return 0x140;
    default:
        return 1;
    }
}

/* 000001A8-000001F4       .text getFirstTalk__10daNpc_Uk_cFv */
u16 daNpc_Uk_c::getFirstTalk() {
    switch (getShapeType()) {
    case 0:
        return 0x1202;
    case 1:
        return 0x1204;
    default:
        return 0x1201;
    }
}

/* 000001F4-0000026C       .text chkGameStart__10daNpc_Uk_cFv */
BOOL daNpc_Uk_c::chkGameStart() {
    if (dComIfGs_isTmpBit(0x40) && !dComIfGs_isTmpBit(getCaughtFlag())) {
        return TRUE;
    }
    return FALSE;
}

/* 0000026C-00000420       .text chkPositioning__10daNpc_Uk_cFfffss */
BOOL daNpc_Uk_c::chkPositioning(float i_dist, float i_yMin, float i_yMax, short i_angleMin, short i_angleMax) {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    cXyz diff = player->current.pos - current.pos;
    f32 dist = diff.absXZ();
    if (dist > i_dist) {
        return FALSE;
    }

    if (diff.y < i_yMin || diff.y > i_yMax) {
        return FALSE;
    }

    short yaw = cLib_targetAngleY(&current.pos, &player->current.pos) - current.angle.y;
    if (i_angleMin > i_angleMax) {
        if (yaw < i_angleMin && yaw > i_angleMax) {
            return TRUE;
        }
    } else {
        if (yaw < i_angleMin || yaw > i_angleMax) {
            return TRUE;
        }
    }

    return FALSE;
}

/* 0000045C-000008B0       .text nextVisitMode__10daNpc_Uk_cFv */
unsigned char daNpc_Uk_c::nextVisitMode() {
    fopAc_ac_c* actor = fopAcM_SearchByID(field_0x68c);
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    u8 partnerMode;
    cXyz diff;

    if (!(actor != NULL)) {
        field_0x6bb = 0;
    } else {
        partnerMode = UK_MK_VISIT_MODE(actor);
    }

    if (field_0x6bb == 10) {
        if (field_0x6bc != 0) {
            field_0x6bc--;
            return 10;
        }
    }

    if (field_0x6bb == 0) {
        actor = fopAcM_searchFromName("Mk", 0xff, 1);
        if (actor != NULL) {
            field_0x68c = fopAcM_GetID(actor);
            partnerMode = UK_MK_VISIT_MODE(actor);
        } else {
            return 0;
        }
    }

    if (partnerMode == 2 || partnerMode == 9 || partnerMode == 8) {
        diff = actor->current.pos - current.pos;

        switch (field_0x6bb) {
        case 4:
            if (diff.abs2XZ() > 10000.0f) {
                return 3;
            }
            return 4;
        case 3:
            if (diff.abs2XZ() > 40000.0f) {
                return 2;
            }
            if (diff.abs2XZ() < 6400.0f) {
                return 4;
            }
            return 3;
        default:
            if (diff.abs2XZ() < 22500.0f) {
                return 3;
            }
            return 2;
        }
    }

    if (field_0x6bb == 9) {
        if (field_0x6bc != 0) {
            field_0x6bc--;
            return 9;
        }

        diff = player->current.pos - current.pos;
        if (diff.abs2XZ() < 22500.0f) {
            return 7;
        }

        if (partnerMode == 4 || partnerMode == 5) {
            return 5;
        }

        s16 angleDiff = fopAcM_searchActorAngleY(this, actor);
        angleDiff -= shape_angle.y;
        if (angleDiff < 0) {
            angleDiff = -angleDiff;
        }
        if (angleDiff < 0x3800) {
            return 2;
        }
        return 9;
    }

    if (partnerMode == 6) {
        switch (field_0x6bb) {
        case 8:
            if (field_0x6bc != 0) {
                field_0x6bc--;
                return 8;
            }
            return 9;
        }

        return 8;
    }

    if (partnerMode == 7) {
        return 10;
    }

    diff = player->current.pos - current.pos;

    switch (field_0x6bb) {
    case 6:
    case 7:
        if (diff.abs2XZ() > 32400.0f) {
            return 5;
        }
        if (field_0x6bb == 7) {
            if (field_0x6bd != 0) {
                field_0x6bd--;
            } else {
                return 6;
            }
        }
        return field_0x6bb;
    case 5:
        if (diff.abs2XZ() < 22500.0f) {
            return 7;
        }
        return 5;
    default:
        if (diff.abs2XZ() < 22500.0f) {
            return 7;
        }
        return 2;
    }
}

/* 000008B0-00000930       .text approachRun__10daNpc_Uk_cFP10fopAc_ac_c */
void daNpc_Uk_c::approachRun(fopAc_ac_c* i_actor) {
    s16 angle;
    dNpc_calc_DisXZ_AngY(current.pos, i_actor->current.pos, NULL, &angle);
    cLib_addCalcAngleS2(&current.angle.y, angle, 8, 0x800);
}

/* 00000930-00000964       .text aroundWalk__10daNpc_Uk_cFP10fopAc_ac_c */
void daNpc_Uk_c::aroundWalk(fopAc_ac_c* i_actor) {
    field_0x6f0.aroundWalk(this, i_actor, field_0x6bd);
}

#include "JSystem/JMath/JMATrigonometric.h"

/* 00000964-00000AD4       .text surrender__10daNpc_Uk_cFv */
void daNpc_Uk_c::surrender() {
    fopAc_ac_c* foundActor = fopAcM_SearchByID(field_0x68c);
    if (foundActor == NULL) {
        return;
    }

    if (foundActor->speedF <= 1.0f) {
        return;
    }

    cXyz diff = foundActor->current.pos - current.pos;
    if (diff.abs2XZ() > 7225.0f) {
        return;
    }

    s16 angleDiff = fopAcM_searchActorAngleY(foundActor, this);
    angleDiff -= foundActor->shape_angle.y;
    if (angleDiff > 0x2AAA || angleDiff < -0x2AAA) {
        return;
    }

    s16 targetAngle;
    if (angleDiff >= 0) {
        targetAngle = foundActor->shape_angle.y + 0x4000;
    } else {
        targetAngle = foundActor->shape_angle.y - 0x4000;
    }

    speed.x = speed.x + 0.5f * JMASSin(targetAngle);
    speed.z = speed.z + 0.5f * JMASCos(targetAngle);
    field_0x688 |= 8;
}

/* 00000AD4-00000D88       .text runawayInit__10daNpc_Uk_cFv */
void daNpc_Uk_c::runawayInit() {
    switch (field_0x6f0.m0) {
    case 1:
    case 2:
    case 5:
        if ((int)(u8)field_0x6ad == 5) {
            switch (getShapeType()) {
            case 1:
                mDoAud_seStart(JA_SE_CV_TR_KO_C_FOUND, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                break;
            case 2:
                mDoAud_seStart(JA_SE_CV_TR_KO_D_FOUND, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                break;
            default:
                mDoAud_seStart(JA_SE_CV_TR_KO_B_FOUND, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                break;
            }
        }

        speedF = 0.0f;
        setAnm(4, 0);
        break;
    case 3:
        speedF = 0.0f;
        setAnm(5, 0);
        break;
    case 4:
        speed.y = 25.0f;
        gravity = -3.3f;
        speedF = 8.0f;
        field_0x688 |= 0x200;
        setAnm(8, 0);
        field_0x6b9 = 11;
        current.angle.y = field_0x6e8;

        switch (getShapeType()) {
        case 1:
            mDoAud_seStart(JA_SE_CV_TR_KO_C_CAUGHT, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
            break;
        case 2:
            mDoAud_seStart(JA_SE_CV_TR_KO_D_CAUGHT, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
            break;
        default:
            mDoAud_seStart(JA_SE_CV_TR_KO_B_CAUGHT, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
            break;
        }
        break;
    }
}

static const int l_bck_ix_tbl[] = {
    0x10, 0x0E, 0x0F, 0x11, 0x0D, 0x0B, 0x08, 0x09, 0x07, 0x0A, 0x0C,
};

static const int l_btp_ix_tbl[] = {
    0x20,
};

/* 00000D88-00000F48       .text nodeCallBack_Uk__FP7J3DNodei */
static BOOL nodeCallBack_Uk(J3DNode* node, int calcTiming) {
    if (calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        J3DJoint* joint = (J3DJoint*)node;
        daNpc_Uk_c* i_this = (daNpc_Uk_c*)model->getUserArea();
        s32 jntNo = joint->getJntNo();

        if (i_this != NULL) {
            MTXCopy(model->getAnmMtx(jntNo), *calc_mtx);
            if (jntNo == i_this->getHeadJntNum()) {
                cXyz offset(0.0f, 0.0f, 0.0f);
                cXyz pos;
                mDoMtx_XrotM(*calc_mtx, i_this->getHead_y());
                cMtx_ZrotM(*calc_mtx, -i_this->getHead_x());
                MtxPosition(&offset, &pos);
                i_this->setAttentionBasePos(pos);
                offset.set(20.0f, -20.0f, 0.0f);
                MtxPosition(&offset, &pos);
                i_this->setEyePos(pos);
                i_this->incAttnSetCount();
            } else if (jntNo == i_this->getBackboneJntNum()) {
                mDoMtx_XrotM(*calc_mtx, i_this->getBackbone_y());
                mDoMtx_ZrotM(*calc_mtx, i_this->getBackbone_x());
            }
            cMtx_copy(*calc_mtx, J3DSys::mCurrentMtx);
            model->setAnmMtx(jntNo, *calc_mtx);
        }
    }
    return TRUE;
}

/* 00000F48-00001070       .text initTexPatternAnm__10daNpc_Uk_cFb */
BOOL daNpc_Uk_c::initTexPatternAnm(bool i_param) {
    J3DModelData* modelData = mpHeadModel->getModelData();

    if (getShapeType() != 1) {
        return TRUE;
    }

    m_maba_tex_pattern = (J3DAnmTexPattern*)dComIfG_getObjectRes("Uk", l_btp_ix_tbl[field_0x6ac]);
    JUT_ASSERT(0x2A7, m_maba_tex_pattern != 0);

    if (!mBtpAnm.init(modelData, m_maba_tex_pattern, 1, 2, 1.0f, 0, -1, i_param, 0)) {
        return FALSE;
    }

    field_0x2bc = 0;
    field_0x2be = 0;

    return TRUE;
}

/* 00001070-00001108       .text playTexPatternAnm__10daNpc_Uk_cFv */
void daNpc_Uk_c::playTexPatternAnm() {
    if (getShapeType() == 1 && cLib_calcTimer(&field_0x2be) == 0) {
        if (field_0x2bc >= m_maba_tex_pattern->getFrameMax()) {
            field_0x2bc -= m_maba_tex_pattern->getFrameMax();
            field_0x2be = 30.0f + cM_rndF(100.0f);
        } else {
            field_0x2bc++;
        }
    }
}

/* 00001108-000011C0       .text setAnm__10daNpc_Uk_cFScUc */
void daNpc_Uk_c::setAnm(signed char i_anm, unsigned char i_param) {
    f32 morf = 8.0f;

    if (i_anm == 4 && (int)(u8)field_0x6ad == 10) {
        morf = 0.0f;
    }

    if (i_param & 1) {
        morf = 0.0f;
        field_0x6ad = 11;
    }

    if (i_anm != field_0x6ad && i_anm != -1) {
        field_0x6ad = i_anm;
        field_0x680 = 0.0f;
        dNpc_setAnm(mpMorf, -1, morf, 1.0f, l_bck_ix_tbl[field_0x6ad], -1, "Uk");
    }
}

/* 000011C0-00001274       .text chkAttentionLocal__10daNpc_Uk_cFv */
bool daNpc_Uk_c::chkAttentionLocal() {
    dAttention_c* attention = &dComIfGp_getAttention();

    if ((field_0x688 & 1) == 1) {
        return true;
    }

    if (field_0x68a != 0) {
        field_0x68a--;
        return true;
    }

    if (attention->LockonTruth()) {
        return this == attention->LockonTarget(0);
    }

    return this == attention->ActionTarget(0);
}

/* 00001274-00001310       .text chkAttention__10daNpc_Uk_cFv */
void daNpc_Uk_c::chkAttention() {
    BOOL attnOld = (field_0x688 & 4) == 4 ? TRUE : FALSE;

    if (chkAttentionLocal()) {
        field_0x688 |= 4;
    } else {
        field_0x688 &= ~4;
    }

    if ((u8)attnOld != (u8)((field_0x688 & 4) == 4) && (u8)attnOld == 1) {
        m_jnt.setTrn();
    }
}

/* 00001310-00001378       .text setAnmStatus__10daNpc_Uk_cFv */
void daNpc_Uk_c::setAnmStatus() {
    switch ((s8)field_0x6b9) {
    case 6:
    case 7:
    case 8:
        setAnm(5, 0);
        break;
    case 12:
    case 13:
        break;
    default:
        setAnm(0, 0);
        break;
    }
}

/* 00001378-000013BC       .text eventOrder__10daNpc_Uk_cFv */
void daNpc_Uk_c::eventOrder() {
    if ((field_0x6b8 & 3) != 0) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if ((field_0x6b8 & 2) != 0) {
            fopAcM_orderSpeakEvent(this);
        }
    }
}

/* 000013BC-00001420       .text checkOrder__10daNpc_Uk_cFv */
void daNpc_Uk_c::checkOrder() {
    if (!eventInfo.checkCommandDemoAccrpt() && eventInfo.checkCommandTalk() && (field_0x6b8 & 3)) {
        field_0x688 |= 1;
        talkInit();
    }

    field_0x6b8 &= ~0xFF;
}

/* 00001420-00001464       .text next_msgStatus__10daNpc_Uk_cFPUl */
u16 daNpc_Uk_c::next_msgStatus(u32* i_msgNo) {
    u16 status = fopMsgStts_MSG_CONTINUES_e;

    switch (*i_msgNo) {
    case 0x26BF:
    case 0x26C1:
    case 0x26C3:
    case 0x26D8:
        (*i_msgNo)++;
        break;
    default:
        status = fopMsgStts_MSG_ENDS_e;
        break;
    }

    return status;
}

/* 00001464-000015E0       .text getMsg__10daNpc_Uk_cFv */
u32 daNpc_Uk_c::getMsg() {
    u32 msg = 0;

    switch ((s8)field_0x6bf) {
    case 1:
        if ((s8)field_0x6b9 == 3 || (s8)field_0x6b9 == 4) {
            msg = field_0x6ec;
            break;
        }

        if (dComIfGs_isTmpBit(0x40)) {
            msg = getShapeType() + 0x26B6;
            break;
        }

        if (dComIfGs_isEventBit(0x1340)) {
            msg = getShapeType() + 0x26AE;
            break;
        }

        if (dComIfGs_isEventBit(getFirstTalk())) {
            msg = getShapeType() + 0x26BA;
            break;
        }

        msg = getShapeType() * 2 + 0x26BF;
        dComIfGs_onEventBit(getFirstTalk());
        break;
    case 2:
        if (dComIfGs_isTmpBit(getCaughtFlag())) {
            msg = getShapeType() + 0x26B6;
            break;
        }

        msg = getShapeType() + 0x26B2;
        dComIfGs_onTmpBit(getCaughtFlag());
        break;
    }

    return msg;
}

/* 000015E0-00001660       .text setCollision__10daNpc_Uk_cFv */
void daNpc_Uk_c::setCollision() {
    cXyz pos = current.pos;
    mCyl.SetC(pos);
    mCyl.SetR(40.0f);
    mCyl.SetH(80.0f);
    dComIfG_Ccsp()->Set(&mCyl);
}

/* 00001660-00001730       .text msgAnm__10daNpc_Uk_cFUc */
void daNpc_Uk_c::msgAnm(unsigned char param_1) {
    if (field_0x6c2 != param_1) {
        field_0x6c2 = param_1;
        switch (field_0x6c2) {
            case 0:
                setAnm(0, 0);
                break;
            case 1:
                setAnm(1, 0);
                break;
            case 2:
                setAnm(2, 0);
                break;
            case 3:
                setAnm(3, 0);
                break;
            case 4:
                setAnm(4, 0);
                break;
            case 5:
                setAnm(6, 0);
                break;
            case 6:
                setAnm(5, 0);
                break;
            case 7:
                setAnm(8, 0);
                break;
        }
    }
}

/* 00001730-00001744       .text talkInit__10daNpc_Uk_cFv */
void daNpc_Uk_c::talkInit() {
    field_0x6c1 = 0;
    field_0x6c2 = 0xff;
}

#include "f_op/f_op_msg_mng.h"

static fpc_ProcID l_msgId;
static msg_class* l_msg;

/* 00001744-000018F0       .text talk__10daNpc_Uk_cFv */
u16 daNpc_Uk_c::talk() {
    u16 ret = 0xff;
    if (field_0x6c1 == 0) {
        l_msgId = -1;
        field_0x684 = getMsg();
        field_0x6c1 = 1;
    } else if (field_0x6c1 != -1) {
        if (l_msgId == -1) {
            l_msgId = fopMsgM_messageSet(field_0x684, this);
        } else {
            if ((field_0x688 & 0x400) != 0x400) {
                msgAnm(dComIfGp_getMesgAnimeAttrInfo());
            }
            switch (field_0x6c1) {
            case 1:
                l_msg = fopMsgM_SearchByID(l_msgId);
                if (l_msg != NULL) {
                    field_0x6c1 = 2;
                    if ((field_0x688 & 0x80) == 0x80) {
                        g_dComIfG_gameInfo.play.getEvent()->setPtT(this);
                        field_0x688 &= ~0x80;
                    }
                }
                break;
            case 2:
                ret = l_msg->mStatus;
                if (ret == fopMsgStts_MSG_DISPLAYED_e) {
                    l_msg->mStatus = next_msgStatus(&field_0x684);
                    if (l_msg->mStatus == fopMsgStts_MSG_CONTINUES_e) {
                        fopMsgM_messageSet(field_0x684);
                    }
                } else if (ret == fopMsgStts_BOX_CLOSED_e) {
                    l_msg->mStatus = fopMsgStts_MSG_DESTROYED_e;
                    field_0x6c1 = -1;
                }
                break;
            }
        }
    }
    return ret;
}

#include "d/d_npc.h"
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_mtx.h"
#include "m_Do/m_Do_lib.h"

typedef BOOL (daNpc_Uk_c::*daNpc_Uk_ActFunc)(void*);

#if TARGET_PC
// The struct below overlays daNpc_Uk_c at GameCube offsets; on the host the overlay names are the
// class's own members (mCurrActionFunc is a host-only member of daNpc_Uk_c, d_a_npc_uk.h).
typedef daNpc_Uk_c daNpc_Uk_mem_c;
#define mType field_0x6bf
#define mFlags field_0x688
#define mActionStatus field_0x6c0
#define field_0x6C4 field_0x6c4
#define mReverb field_0x6c8
#else
struct daNpc_Uk_mem_c : public fopAc_ac_c {
    /* 0x290 */ u8 field_0x290[0x298 - 0x290];
    /* 0x298 */ mDoExt_McaMorf* mpMorf;
    /* 0x29C */ J3DModel* mpHeadModel;
    /* 0x2A0 */ u32 mShadowId;
    /* 0x2A4 */ u8 field_0x2A4[0x2A8 - 0x2A4];
    /* 0x2A8 */ mDoExt_btpAnm mBtpAnm;
    /* 0x2BC */ u8 field_0x2BC[0x2C0 - 0x2BC];
    /* 0x2C0 */ dBgS_Acch mObjAcch;
    /* 0x484 */ dBgS_AcchCir mAcchCir;
    /* 0x4C4 */ dCcD_Stts mStts;
    /* 0x500 */ dCcD_Cyl mCyl;
    /* 0x630 */ u8 field_0x630[0x638 - 0x630];
    /* 0x638 */ s8 field_0x638;
    /* 0x639 */ s8 field_0x639;
    /* 0x63A */ u8 field_0x63A[0x664 - 0x63A];
    /* 0x664 */ cXyz mEyePos;
    /* 0x670 */ cXyz mAttnBasePos;
    /* 0x67C */ u8 field_0x67C[0x688 - 0x67C];
    /* 0x688 */ u16 mFlags;
    /* 0x68A */ u8 field_0x68A[0x68C - 0x68A];
    /* 0x68C */ u32 field_0x68C;
    /* 0x690 */ u8 field_0x690[0x698 - 0x690];
    /* 0x698 */ dNpc_PathRun_c mPathRun;
    /* 0x6A0 */ daNpc_Uk_ActFunc mCurrActionFunc;
    /* 0x6AC */ u8 field_0x6AC;
    /* 0x6AD */ u8 field_0x6AD[0x6BF - 0x6AD];
    /* 0x6BF */ s8 mType;
    /* 0x6C0 */ s8 mActionStatus;
    /* 0x6C1 */ u8 field_0x6C1[0x6C4 - 0x6C1];
    /* 0x6C4 */ s32 field_0x6C4;
    /* 0x6C8 */ u8 mReverb;
};
#endif

#ifdef __MWERKS__
extern "C" u32 getPath__10daNpc_Uk_cFv(daNpc_Uk_c*);
extern "C" int chkGameStart__10daNpc_Uk_cFv(daNpc_Uk_c*);
#define UK_GET_PATH(p) getPath__10daNpc_Uk_cFv(p)
#define UK_CHK_GAME_START(p) chkGameStart__10daNpc_Uk_cFv(p)
#else
#define UK_GET_PATH(p) (p)->getPath()
#define UK_CHK_GAME_START(p) (p)->chkGameStart()
#endif

static inline void daNpc_Uk_setAction(daNpc_Uk_c* i_this, daNpc_Uk_ActFunc i_func, void* i_arg) {
    daNpc_Uk_mem_c* m = (daNpc_Uk_mem_c*)i_this;
    if (m->mCurrActionFunc != i_func) {
        if (m->mCurrActionFunc) {
            m->mActionStatus = -1;
            (i_this->*(m->mCurrActionFunc))(i_arg);
        }
        m->mCurrActionFunc = i_func;
        m->mActionStatus = 0;
        (i_this->*(m->mCurrActionFunc))(i_arg);
    }
}

/* 000018F0-00001DE4       .text init__10daNpc_Uk_cFv */
BOOL daNpc_Uk_c::init() {
    daNpc_Uk_mem_c* m = (daNpc_Uk_mem_c*)this;
    cXyz sp44(0.0f, 0.0f, 0.0f);

    attention_info.flags = fopAc_Attn_LOCKON_TALK_e | fopAc_Attn_ACTION_SPEAK_e;
    attention_info.distances[1] = 0xA9;
    attention_info.distances[3] = 0xA9;
    gravity = -30.0f;

    J3DModel* model = m->mpMorf->getModel();
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::YrotM(shape_angle.y);
    model->setBaseTRMtx(mDoMtx_stack_c::get());
    m->mpMorf->calc();

    m->mStts.Init(100, 0xFF, this);
    m->mCyl.Set(dNpc_cyl_src);
    m->mCyl.SetStts(&m->mStts);

    fopAcM_SetStatusMap(this, 0x07);
    fopAcM_OnStatus(this, fopAcStts_SHOWMAP_e);

    switch (m->mType) {
    case 0:
        daNpc_Uk_setAction(this, &daNpc_Uk_c::wait_action, NULL);
        break;
    case 1:
        if (UK_CHK_GAME_START(this)) {
            daNpc_Uk_setAction(this, &daNpc_Uk_c::hind_action, NULL);
            m->mFlags |= 0x10;
            fopAcM_OffStatus(this, fopAcStts_SHOWMAP_e);
        } else {
            daNpc_Uk_setAction(this, &daNpc_Uk_c::visit_action, NULL);
        }
        break;
    case 2:
        m->mStts.SetWeight(0xFE);
        m->mPathRun.setInf(UK_GET_PATH(this), fopAcM_GetRoomNo(this), 1);
        if (m->mPathRun.getPath() == NULL) {
            return FALSE;
        }
        if (UK_CHK_GAME_START(this)) {
            daNpc_Uk_setAction(this, &daNpc_Uk_c::seek_action, NULL);
        } else {
            m->mFlags |= 0x10;
            fopAcM_OffStatus(this, fopAcStts_SHOWMAP_e);
            daNpc_Uk_setAction(this, &daNpc_Uk_c::hind_action, NULL);
        }
        fopAcM_OnStatus(this, fopAcStts_UNK40_e);
        break;
    }

#if TARGET_PC
    // mAttnBasePos / mEyePos of the overlay: field_0x670..678 and field_0x664..66c.
    *(cXyz*)&field_0x670 = current.pos;
    *(cXyz*)&field_0x664 = current.pos;
#else
    m->mAttnBasePos = current.pos;
    m->mEyePos = current.pos;
#endif

    f32 far = mDoLib_clipper::getFar();
    if (far > 1.0f) {
        cullSizeFar = 5000.0f / far;
    }

    m->field_0x6C4 = 0;
    m->mReverb = dComIfGp_getReverb(fopAcM_GetRoomNo(this));

    return TRUE;
}

/* 00001DE4-00001E38       .text setAttention__10daNpc_Uk_cFb */
void daNpc_Uk_c::setAttention(bool i_set) {
    if (!i_set && field_0x67f >= 2) {
        return;
    }

    eyePos.set(field_0x664, field_0x668, field_0x66c);
    attention_info.position.set(field_0x670, 45.0f + field_0x674, field_0x678);
}

/* 00001E38-00001F5C       .text getLookBackMode__10daNpc_Uk_cFv */
u8 daNpc_Uk_c::getLookBackMode() {
    fopAc_ac_c* actor = fopAcM_SearchByID(field_0x68c);

    if (actor != NULL && (s8)field_0x6b9 == 2) {
        if (field_0x6bb == 8 || field_0x6bb == 4) {
            return 3;
        }

        u8 visiting = 0;
        u8 partnerMode = UK_MK_VISIT_MODE(actor);
        if (partnerMode == 8 || partnerMode == 9) {
            visiting = 1;
        }

        if (visiting) {
            return 2;
        }
    }

    if ((field_0x688 & 0x40) == 0x40) {
        if (fopAcM_getTalkEventPartner(dComIfGp_getLinkPlayer()) != this) {
            return 4;
        }

        return 2;
    }

    if ((field_0x688 & 0x20) == 0x20) {
        return 0;
    }

    if ((field_0x688 & 4) == 4) {
        return 1;
    }

    if ((s8)field_0x6b9 == 2 && (field_0x6bb == 6 || field_0x6bb == 7)) {
        return 1;
    }

    return 2;
}

/* 00001F5C-0000218C       .text lookBack__10daNpc_Uk_cFv */
void daNpc_Uk_c::lookBack() {
    fopAc_ac_c* foundActor = fopAcM_SearchByID(field_0x68c);

    cXyz dst;
    cXyz temp(0.0f, 0.0f, 0.0f);
    cXyz* dstPos = NULL;
    s16 defaultY = current.angle.y;
    bool followFlag = false;

    u8 mode = getLookBackMode();
    temp = current.pos;
    temp.y = eyePos.y;

    switch (mode) {
    case 0:
        m_jnt.setTrn();
        dst = dNpc_playerEyePos(-20.0f);
        dstPos = &dst;
        break;
    case 1:
        followFlag = true;
        dst = dNpc_playerEyePos(-20.0f);
        dstPos = &dst;
        break;
    case 2:
        defaultY = current.angle.y;
        followFlag = true;
        break;
    case 3:
        m_jnt.setTrn();
        dst = foundActor->current.pos;
        dst.y = foundActor->eyePos.y;
        dstPos = &dst;
        break;
    case 4: {
        fopAc_ac_c* partner = fopAcM_getTalkEventPartner(dComIfGp_getLinkPlayer());
        if (partner == NULL) {
            partner = dComIfGp_getLinkPlayer();
        }
        m_jnt.setTrn();
        dst = partner->eyePos;
        dstPos = &dst;
        temp = current.pos;
        temp.y = eyePos.y;
        break;
    }
    }

    if (m_jnt.trnChk()) {
        cLib_addCalcAngleS2(&field_0x67c, 0x5DC, 4, 0x800);
    } else {
        field_0x67c = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dstPos, temp, defaultY, field_0x67c, followFlag);
}

/* 0000218C-00002254       .text getStaffName__10daNpc_Uk_cFv */
char* daNpc_Uk_c::getStaffName() {
    if ((s8)field_0x6bf == 2) {
        switch (getShapeType()) {
        case 0:
            return "UkB2";
        case 1:
            return "UkC2";
        case 2:
            return "UkD2";
        }
    } else {
        switch (getShapeType()) {
        case 1:
            return "UkC";
        case 2:
            return "UkD";
        }
    }

    return "UkB";
}

/* 00002254-000022C4       .text checkDemoStart__10daNpc_Uk_cFv */
BOOL daNpc_Uk_c::checkDemoStart() {
    char* staffName = getStaffName();
    field_0x6b0 = dComIfGp_evmng_getMyStaffId(staffName, NULL, 0);

    if (field_0x6b0 != -1) {
        field_0x6b4 = getNowEventAction();
        return TRUE;
    } else {
        return FALSE;
    }
}

/* 000022C4-0000230C       .text getNowEventAction__10daNpc_Uk_cFv */
int daNpc_Uk_c::getNowEventAction() {
    static char* action_table[] = {
        "WAIT", "TALK", "TALK2", "HOME", "RUN", "RUN3", "HIND", "DISP",
        "SPEAK", "LOOK_P", "WARNING", "JUMP", "JUMP2", "TURN", "WARP", "TURN",
    };

    return dComIfGp_evmng_getMyActIdx(field_0x6b0, action_table, 15, FALSE, 0);
}

/* 0000230C-00002494       .text visitInit__10daNpc_Uk_cFUc */
void daNpc_Uk_c::visitInit(u8 i_nextVisitMode) {
    switch (i_nextVisitMode) {
        case 2:
            setAnm(4, 0);
            break;
        case 5:
            setAnm(4, 0);
            if (field_0x6bb == 9) {
                field_0x6be = 0x2d;
            } else {
                field_0x6be = 0;
            }
            break;
        case 3:
        case 6:
            setAnm(3, 0);
            break;
        case 7:
            if (field_0x6bb == 9 || field_0x6bb == 8 || field_0x6bb == 2) {
                field_0x6bd = 0x2d;
            } else {
                field_0x6bd = field_0x6be;
            }
            setAnm(4, 0);
            break;
        case 8:
            setAnm(0, 0);
            field_0x6bc = 5;
            speedF = 0.0f;
            break;
        case 9:
            setAnm(0, 0);
            field_0x6bc = 0xf;
            speedF = 0.0f;
            break;
        case 10:
            setAnm(0, 0);
            field_0x6bc = 0x1e;
            speedF = 0.0f;
            break;
        case 4:
            setAnm(0, 0);
            speedF = 0.0f;
            break;
        case 0:
        case 1:
        default:
            speedF = 0.0f;
            break;
    }
    field_0x6bb = i_nextVisitMode;
}

/* 00002494-000025A4       .text visitProc__10daNpc_Uk_cFv */
void daNpc_Uk_c::visitProc() {
    fopAc_ac_c* actor = fopAcM_SearchByID(field_0x68c);
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();

    switch (field_0x6bb) {
    case 2:
        approachRun(actor);
        break;
    case 5:
        if (field_0x6be < 0x2D) {
            field_0x6be++;
        }
        approachRun(player);
        break;
    case 3:
        approachRun(actor);
        break;
    case 6:
    case 7:
        aroundWalk(player);
        break;
    case 4:
        surrender();
        break;
    case 8:
        surrender();
        break;
    case 9:
        surrender();
        break;
    case 10:
        surrender();
        break;
    }

    switch (field_0x6bb) {
    case 9:
    case 10:
        field_0x688 |= 0x20;
        break;
    }
}

/* 000025A4-00002628       .text wait01__10daNpc_Uk_cFv */
bool daNpc_Uk_c::wait01() {
    if ((field_0x688 & 1) == 1) {
        field_0x6ba = field_0x6b9;
        field_0x6b9 = 1;
        setAnmStatus();
    } else {
        field_0x6b8 |= 1;
    }

    return mpMorf->isMorf();
}

/* 00002628-000026CC       .text talk01__10daNpc_Uk_cFv */
bool daNpc_Uk_c::talk01() {
    if (talk() == fopMsgStts_BOX_CLOSED_e) {
        field_0x6b9 = field_0x6ba;
        setAnmStatus();
        dComIfGp_event_onEventFlag(dEvtFlag_UNK8_e);
        field_0x688 &= ~1;
        field_0x68a = 5;
    }

    field_0x688 |= 0x20;
    return mpMorf->isMorf();
}

#include "JAZelAudio/JAIZelBasic.h"
#include "JAZelAudio/JAZelAudio_SE.h"

/* 000026CC-0000280C       .text talk02__10daNpc_Uk_cFv */
bool daNpc_Uk_c::talk02() {
    if (talk() == fopMsgStts_BOX_CLOSED_e) {
        if (!field_0x6f0.chkGameSet()) {
            field_0x6b9 = field_0x6ba;
            setAnmStatus();
            dComIfGp_event_reset();
        } else {
            field_0x6e6 = dComIfGp_evmng_getEventIdx("MK_GAMESET");
            fopAcM_orderChangeEventId(this, field_0x6e6, 0, 0xFFFF);
            JAIZelBasic::zel_basic->seStart(JA_SE_END_WHISTLE, NULL, 0, 0);
            field_0x6b9 = 4;
            field_0x6ba = 5;
        }
        field_0x688 &= ~1;
        field_0x68a = 5;
    }
    field_0x688 |= 0x20;
    return mpMorf->isMorf();
}

/* 0000280C-00002968       .text visitTalkInit__10daNpc_Uk_cFv */
unsigned char daNpc_Uk_c::visitTalkInit() {
    if (dComIfGs_isEventBit(0x1F80) && !dComIfGs_isEventBit(0x1E02)) {
        field_0x6e6 = eventInfo.getEventId();
        dComIfGp_event_setTalkPartner(fopAcM_SearchByID(field_0x68c));
        field_0x6b9 = 4;
        demo02();
        dComIfGs_onEventBit(0x1E04);
        return 4;
    }

    if (dComIfGs_isEventBit(0x1208) && !dComIfGs_checkGetItem(0x23) &&
        !dComIfGs_checkGetItem(0x26)) {
        field_0x6e6 = eventInfo.getEventId();
        dComIfGp_event_setTalkPartner(fopAcM_SearchByID(field_0x68c));
        field_0x6b9 = 4;
        demo02();
        dComIfGs_onEventBit(0x1602);
        return 4;
    }

    return 1;
}

/* 00002968-00002A44       .text visitSetEvent__10daNpc_Uk_cFv */
void daNpc_Uk_c::visitSetEvent() {
    if (dComIfGs_isEventBit(0x1F80) && !dComIfGs_isEventBit(0x1E02)) {
        eventInfo.setEventName("MK_TALK3");
    } else if (dComIfGs_isEventBit(0x1208) && !dComIfGs_checkGetItem(0x23) &&
               !dComIfGs_checkGetItem(0x26)) {
        eventInfo.setEventName("MK_TALK2");
    } else {
        eventInfo.setEventName("MK_TALK");
    }
}

/* 00002A44-00002C80       .text visit01__10daNpc_Uk_cFv */
bool daNpc_Uk_c::visit01() {
    if ((field_0x688 & 1) == 1) {
        field_0x6ba = field_0x6b9;
        field_0x6b9 = visitTalkInit();
        setAnmStatus();
        speedF = 0.0f;
        field_0x6bb = 1;
    } else if (checkDemoStart()) {
        field_0x6ba = field_0x6b9;
        field_0x6b9 = 3;
        setAnmStatus();
        speedF = 0.0f;
        field_0x6bb = 1;
        demo01();
    } else {

        unsigned char mode = nextVisitMode();
        if (mode != field_0x6bb) {
            visitInit(mode);
        }
        visitProc();

        f32 target;
        if (field_0x6ad == 3) {
            if (field_0x6bb == 6) {
                target = 2.5f;
            } else {
                target = field_0x690;
            }
        } else if (field_0x6ad == 4) {
            target = field_0x694;
            if (field_0x6bb == 7) {
                target -= 2.5f;
            }
        }

        if (speedF > target) {
            cLib_chaseF(&speedF, target, 2.799999952316284f);
        } else {
            cLib_chaseF(&speedF, target, 1.100000023841858f);
        }

        field_0x6b8 |= 1;

        if (field_0x6bb == 6 || field_0x6bb == 7) {
            fopAc_ac_c* player = dComIfGp_getLinkPlayer();
            s16 angleDiff = fopAcM_searchActorAngleY(player, this);
            angleDiff -= player->shape_angle.y;
            if (angleDiff < 0) {
                angleDiff = -angleDiff;
            }
            if (angleDiff > 0x1800) {
                field_0x6b8 &= ~1;
            }
        }

        fopAc_ac_c* target_actor = fopAcM_SearchByID(field_0x68c);
        if (target_actor != NULL) {
            u8 visiting = 0;
            u8 mode2 = UK_MK_VISIT_MODE(target_actor);
            if (mode2 == 8 || mode2 == 9) {
                visiting = 1;
            }

            if (visiting) {
                field_0x6b8 &= ~1;
            }
        }

        if (field_0x6b8 & 1) {
            visitSetEvent();
        }
    }

    return true;
}

/* 00002C80-00002D20       .text warningB__10daNpc_Uk_cFv */
bool daNpc_Uk_c::warningB() {
    if (chkPositioning(300.0f, -10.0f, 80.0f, 0x3A98, -0x3A98)) {
        field_0x6b9 = 9;
        field_0x6e6 = dComIfGp_evmng_getEventIdx("UkB_FOUND");
        fopAcM_orderOtherEventId(this, field_0x6e6, 0xFF, 0xFFFF, 0, 1);
    }

    return false;
}

/* 00002D20-00002DC0       .text warningC__10daNpc_Uk_cFv */
bool daNpc_Uk_c::warningC() {
    if (chkPositioning(180.0f, -10.0f, 80.0f, 0x3A98, -0x1388)) {
        field_0x6b9 = 9;
        field_0x6e6 = dComIfGp_evmng_getEventIdx("UkC_FOUND");
        fopAcM_orderOtherEventId(this, field_0x6e6, 0xFF, 0xFFFF, 0, 1);
    }

    return false;
}

/* 00002DC0-00002ED8       .text warningD__10daNpc_Uk_cFv */
bool daNpc_Uk_c::warningD() {
    if (chkPositioning(150.0f, -10.0f, 80.0f, 0x4650, 0)) {
        field_0x6b9 = 9;
        field_0x6e6 = dComIfGp_evmng_getEventIdx("UkD_FOUND");
        fopAcM_orderOtherEventId(this, field_0x6e6, 0xFF, 0xFFFF, 0, 1);
    }

    if (chkPositioning(150.0f, -10.0f, 80.0f, 0, -0x4650)) {
        field_0x6b9 = 9;
        field_0x6e6 = dComIfGp_evmng_getEventIdx("UkD_FOUND2");
        fopAcM_orderOtherEventId(this, field_0x6e6, 0xFF, 0xFFFF, 0, 1);
    }

    return false;
}

/* 00002ED8-00002F74       .text found__10daNpc_Uk_cFv */
bool daNpc_Uk_c::found() {
    if (eventInfo.checkCommandDemoAccrpt()) {
        field_0x6ba = 10;
        field_0x6b9 = 4;
        field_0x6f0.init(0x50, 0x12C);
        dComIfGs_onTmpBit(getFoundFlag());
        return demo02();
    }

    fopAcM_orderOtherEventId(this, field_0x6e6, 0xFF, 0xFFFF, 0, 1);
    return FALSE;
}

/* 00002F74-00003148       .text runaway__10daNpc_Uk_cFv */
bool daNpc_Uk_c::runaway() {
    u8 mode = field_0x6f0.runAwayProc(this, &mPathRun, &mCyl, &field_0x6e8);

    if (mode != field_0x6f0.m0) {
        if ((mode == 1 && field_0x6f0.m0 == 2) || (mode == 2 && field_0x6f0.m0 == 1)) {
            if (cM_rndF(1.0f) < 0.5f) {
                switch (getShapeType()) {
                case 1:
                    mDoAud_seStart(JA_SE_CV_TR_KO_C_TURN, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                    break;
                case 2:
                    mDoAud_seStart(JA_SE_CV_TR_KO_D_TURN, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                    break;
                default:
                    mDoAud_seStart(JA_SE_CV_TR_KO_B_TURN, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                    break;
                }
            }
        }

        field_0x6f0.m0 = mode;
        runawayInit();
    }

    if (mode == 3) {
        field_0x688 |= 0x20;
    }

    if ((int)(u8)field_0x6ad == 4) {
        cLib_chaseF(&speedF, field_0x6f0.getSpeedF(15.0f, 18.0f), 2.8f);
    }

    return false;
}

/* 00003148-000031E8       .text jump__10daNpc_Uk_cFv */
bool daNpc_Uk_c::jump() {
    if ((field_0x688 & 0x200) != 0x200) {
        setAnm(0, 0);
        gravity = -30.0f;
        speedF = 0.0f;
        if (mpMorf->isMorf()) {
            field_0x6b9 = 12;
            setAnm(6, 0);
            field_0x6b8 |= 2;
        }
    }

    return true;
}

/* 000031E8-00003228       .text jitanda01__10daNpc_Uk_cFv */
bool daNpc_Uk_c::jitanda01() {
    if ((field_0x688 & 1) == 1) {
        field_0x688 |= 0x400;
        field_0x6ba = 0xd;
        field_0x6b9 = 1;
    } else {
        field_0x6b8 |= 2;
    }

    return true;
}

/* 00003228-000032AC       .text jitanda02__10daNpc_Uk_cFv */
bool daNpc_Uk_c::jitanda02() {
    if ((field_0x688 & 1) == 1) {
        field_0x6ba = field_0x6b9;
        field_0x6b9 = 1;
    }

    if (checkDemoStart()) {
        field_0x6ba = field_0x6b9;
        field_0x6b9 = 3;
        demo01();
    } else {
        field_0x6b8 |= 1;
    }

    return true;
}

/* 000032AC-00003300       .text demo01__10daNpc_Uk_cFv */
bool daNpc_Uk_c::demo01() {
    if (!checkDemoStart()) {
        field_0x6b9 = field_0x6ba;
        setAnmStatus();
        return TRUE;
    }

    return demoProc();
}

/* 00003300-000033D4       .text demo02__10daNpc_Uk_cFv */
bool daNpc_Uk_c::demo02() {
    if (dComIfGp_evmng_endCheck(field_0x6e6)) {
        field_0x6b9 = field_0x6ba;
        setAnmStatus();
        dComIfGp_event_onEventFlag(dEvtFlag_UNK8_e);
        field_0x688 &= ~1;
        field_0x68a = 5;
        return TRUE;
    }

    if (!checkDemoStart()) {
        JUT_ASSERT(0x781, 0);
    }

    return demoProc();
}

#include "d/d_com_inf_game.h"

/* 000033D4-00003648       .text demoInitCom__10daNpc_Uk_cFv */
void daNpc_Uk_c::demoInitCom() {
    int* p = dComIfGp_evmng_getMyIntegerP(field_0x6b0, "SOUND");
    if (p != NULL) {
        switch (*p) {
        case 1:
            switch (getShapeType()) {
            case 1:
                fopAcM_seStart(this, JA_SE_CV_TR_KO_C_RUN_AWAY, 0);
                break;
            case 2:
                fopAcM_seStart(this, JA_SE_CV_TR_KO_D_RUN_AWAY, 0);
                break;
            default:
                fopAcM_seStart(this, JA_SE_CV_TR_KO_B_RUN_AWAY, 0);
                break;
            }
            break;
        case 2:
            switch (getShapeType()) {
            case 1:
                fopAcM_seStart(this, JA_SE_CV_TR_KO_C_FOUND, 0);
                break;
            case 2:
                fopAcM_seStart(this, JA_SE_CV_TR_KO_D_FOUND, 0);
                break;
            default:
                fopAcM_seStart(this, JA_SE_CV_TR_KO_B_FOUND, 0);
                break;
            }
            break;
        }
    }
}

#include "d/actor/d_a_npc_mk_static.h"
#include "d/d_particle.h"
#include "d/d_vibration.h"
#include "JAZelAudio/JAIZelBasic.h"
#include "JAZelAudio/JAZelAudio_SE.h"

/* 00003648-000040E0       .text demoProc__10daNpc_Uk_cFv */
bool daNpc_Uk_c::demoProc() {
    BOOL changed = FALSE;

    if (dComIfGp_evmng_getIsAddvance(field_0x6b0)) {
        demoInitCom();

        switch (field_0x6b4) {
        case 0:
            speedF = 0.0f;
            setAnm(0, 0);
            break;
        case 1:
        case 9:
            speedF = 0.0f;
            setAnm(0, 0);
            break;
        case 2:
            speedF = 0.0f;
            setAnm(2, 0);
            break;
        case 3:
            speedF = 0.0f;
            old.pos = home.pos;
            current.pos = home.pos;
            current.angle.y = home.angle.y;
            break;
        case 4:
        case 5: {
            if (field_0x6ad == 4) {
                speedF = field_0x694;
            } else {
                setAnm(4, 0);
            }

            cXyz* a_xyz = dComIfGp_evmng_getMyXyzP(field_0x6b0, "Pos");
            JUT_ASSERT(0x7df, a_xyz);
            field_0x6cc = current.pos;
            field_0x6d8 = *a_xyz;

            int* a_intP = dComIfGp_evmng_getMyIntegerP(field_0x6b0, "Timer");
            if (a_intP != NULL) {
                field_0x6e4 = *a_intP;
            } else {
                field_0x6e4 = -1;
            }
            break;
        }
        case 13: {
            cXyz* a_xyz = dComIfGp_evmng_getMyXyzP(field_0x6b0, "Pos");
            JUT_ASSERT(0x7eb, a_xyz);
            field_0x6cc = current.pos;
            field_0x6d8 = *a_xyz;
            field_0x6e8 = cLib_targetAngleY(&field_0x6cc, &field_0x6d8);
            setAnm(0, 0);
            break;
        }
        case 6:
            speedF = 0.0f;
            field_0x688 |= 0x10;
            fopAcM_OffStatus(this, fopAcStts_SHOWMAP_e);
            field_0x6ba = 5;
            break;
        case 7:
            ((daNpc_Uk_mem_c*)this)->mCurrActionFunc = NULL;
            daNpc_Uk_setAction(this, &daNpc_Uk_c::visit_action, NULL);
            field_0x6ba = field_0x6b9;
            field_0x6b9 = 3;
            field_0x688 &= ~0x10;
            fopAcM_OnStatus(this, fopAcStts_SHOWMAP_e);
            break;
        case 8: {
            speedF = 0.0f;
            talkInit();
            field_0x688 |= 0x80;

            int* a_intP = dComIfGp_evmng_getMyIntegerP(field_0x6b0, "MsgNo");
            JUT_ASSERT(0x806, a_intP);
            field_0x6ec = *a_intP;

            switch (getShapeType()) {
            case 0:
                setAnm(0, 0);
                break;
            case 1:
                setAnm(1, 0);
                break;
            default:
                setAnm(2, 0);
                break;
            }
            changed = TRUE;
            break;
        }
        case 10:
            speedF = 0.0f;
            field_0x688 |= 0x2;
            speedF = 0.0f;
            setAnm(5, 1);
            break;
        case 11:
        case 12:
            field_0x688 |= 0x2;
            gravity = -3.299999952316284f;
            speed.y = 28.0f;
            speedF = 12.0f;
            field_0x688 |= 0x200;
            setAnm(9, 0);

            switch (getShapeType()) {
            case 1:
                JAIZelBasic::zel_basic->seStart(JA_SE_CV_TR_KO_C_RUN_AWAY, &eyePos, 0, dComIfGp_getReverb(current.roomNo));
                break;
            case 2:
                JAIZelBasic::zel_basic->seStart(JA_SE_CV_TR_KO_D_RUN_AWAY, &eyePos, 0, dComIfGp_getReverb(current.roomNo));
                break;
            default:
                JAIZelBasic::zel_basic->seStart(JA_SE_CV_TR_KO_B_RUN_AWAY, &eyePos, 0, dComIfGp_getReverb(current.roomNo));
                break;
            }
            break;
        case 14: {
            speedF = 0.0f;

            cXyz* a_xyz = dComIfGp_evmng_getMyXyzP(field_0x6b0, "Pos");
            JUT_ASSERT(0x82f, a_xyz);
            current.pos = *a_xyz;
            old.pos = *a_xyz;

            int* a_intP = dComIfGp_evmng_getMyIntegerP(field_0x6b0, "Angle");
            JUT_ASSERT(0x835, a_intP);
            current.angle.y = *a_intP;
            shape_angle.y = *a_intP;
            changed = TRUE;
            break;
        }
        }
    }

    switch (field_0x6b4) {
    case 4:
    case 5: {
        f32 dist;
        s16 angle;
        dNpc_calc_DisXZ_AngY(current.pos, field_0x6d8, &dist, &angle);

        if (dist < field_0x694 || field_0x6f0.chkPointPass(&field_0x6cc, &field_0x6d8, &current.pos)) {
            dComIfGp_evmng_cutEnd(field_0x6b0);
            if (field_0x6b4 == 5) {
                current.pos.x = field_0x6d8.x;
                current.pos.z = field_0x6d8.z;
                speedF = 0.0f;
            }
        } else {
            cLib_addCalcAngleS(&current.angle.y, angle, 4, 0x2000, 0x400);
            cLib_chaseF(&speedF, field_0x694, 1.100000023841858f);
            if (field_0x6e4 > 0) {
                field_0x6e4--;
            }
            if (field_0x6e4 == 0) {
                dComIfGp_evmng_cutEnd(field_0x6b0);
            }
        }
        break;
    }
    case 13:
        cLib_addCalcAngleS(&current.angle.y, field_0x6e8, 1, 0x800, 0x800);
        if (current.angle.y == field_0x6e8) {
            dComIfGp_evmng_cutEnd(field_0x6b0);
        }
        break;
    case 1:
    case 2:
        field_0x688 |= 0x20;
        dComIfGp_evmng_cutEnd(field_0x6b0);
        break;
    case 9:
        field_0x688 |= 0x40;
        dComIfGp_evmng_cutEnd(field_0x6b0);
        break;
    case 8: {
        u16 ret = talk();
        if (ret == fopMsgStts_BOX_CLOSED_e || ret == 0xfe) {
            dComIfGp_evmng_cutEnd(field_0x6b0);
        }
        field_0x688 |= 0x20;

        if (changed) {
            return TRUE;
        }

        daNpc_Uk_mem_c* m;
        m = (daNpc_Uk_mem_c*)this;
        if (m->mpMorf->isMorf() != FALSE) {
            return TRUE;
        }
        return FALSE;
    }
    case 10:
        if ((field_0x688 & 0x2) == 0x2) {
            if (field_0x67e) {
                dComIfGp_evmng_cutEnd(field_0x6b0);
                field_0x688 &= ~0x2;
            }
        } else {
            dComIfGp_evmng_cutEnd(field_0x6b0);
        }
        break;
    case 11:
    case 12:
        if ((field_0x688 & 0x200) == 0x200) {
            break;
        }

        setAnm(0xa, 0);
        gravity = -30.0f;
        speedF = 0.0f;

        if ((field_0x688 & 0x2) == 0x2) {
            field_0x688 &= ~0x2;
            dComIfGp_getVibration().StartShock(4, -0x21, cXyz(0.0f, 1.0f, 0.0f));

            if (field_0x6b4 == 0xc) {
                static cXyz scale(0.6f, 0.6f, 0.6f);
                dPa_control_c* particle = g_dComIfG_gameInfo.play.getParticle();
                JPABaseEmitter* emtr = particle->set(0, 0x23, &current.pos, &current.angle, &scale, 0xff, NULL, -1, NULL, NULL, NULL);
                if (emtr != NULL) {
                    emtr->mRate = 18.0f;
                    emtr->mSpread = 1.0f;
                    emtr->setMaxFrame(1);
                }
            }
        } else if (field_0x67e) {
            dComIfGp_evmng_cutEnd(field_0x6b0);
        }
        break;
    default:
        dComIfGp_evmng_cutEnd(field_0x6b0);
        break;
    }

    return TRUE;
}

/* 000040E0-000041B0       .text wait_action__10daNpc_Uk_cFPv */
BOOL daNpc_Uk_c::wait_action(void*) {
    if (field_0x6c0 == 0) {
        field_0x6b9 = 0;
        setAnmStatus();
        field_0x6c0++;
    } else if (field_0x6c0 != -1) {
        bool attn;

        chkAttention();
        field_0x688 &= ~0x60;

        switch ((s8)field_0x6b9) {
        case 0:
            attn = wait01();
            break;
        case 1:
            attn = talk01();
            break;
        default:
            attn = false;
            break;
        }

        lookBack();
        setAttention(attn);
    }

    return TRUE;
}

/* 000041B0-0000440C       .text seek_action__10daNpc_Uk_cFPv */
BOOL daNpc_Uk_c::seek_action(void*) {
    if (field_0x6c0 == 0) {
        if (dComIfGs_isTmpBit(getFoundFlag())) {
            field_0x6b9 = 10;
            field_0x6f0.init(0x50, 0x12C);
            field_0x6f0.setRndPathPos(this, &mPathRun);
        } else {
            field_0x6b9 = getShapeType() + 6;
        }

        setAnmStatus();
        field_0x6c0++;
    } else if (field_0x6c0 != -1) {
        bool attn;

        chkAttention();
        field_0x688 &= ~0x60;

        switch ((s8)field_0x6b9) {
        case 6:
            attn = warningB();
            break;
        case 7:
            attn = warningC();
            break;
        case 8:
            attn = warningD();
            break;
        case 9:
            attn = found();
            break;
        case 10:
            attn = runaway();
            break;
        case 11:
            attn = jump();
            break;
        case 12:
            attn = jitanda01();
            break;
        case 13:
            attn = jitanda02();
            break;
        case 3:
            attn = demo01();
            break;
        case 4:
            attn = demo02();
            break;
        case 1:
            attn = talk02();
            break;
        case 5:
            attn = false;
            daNpc_Uk_setAction(this, &daNpc_Uk_c::hind_action, NULL);
            break;
        default:
            attn = false;
            break;
        }

        lookBack();
        setAttention(attn);
    }

    return TRUE;
}

/* 0000440C-0000457C       .text hind_action__10daNpc_Uk_cFPv */
BOOL daNpc_Uk_c::hind_action(void*) {
    daNpc_Uk_mem_c* m = (daNpc_Uk_mem_c*)this;

    if (m->mActionStatus == 0) {
        m->mActionStatus++;
    } else if (m->mActionStatus != -1) {
        m->mFlags &= ~0x60;

        if (m->mType == 2 && UK_CHK_GAME_START(this)) {
            daNpc_Uk_setAction(this, &daNpc_Uk_c::seek_action, NULL);
            m->mFlags &= ~0x10;
            fopAcM_OnStatus(this, fopAcStts_SHOWMAP_e);
        }

        if (m->mType == 1 || m->mType == 2) {
            if (checkDemoStart()) {
                bool isDemo = demoProc();
                lookBack();
                setAttention(isDemo);
            }
        }
    }

    return TRUE;
}

/* 0000457C-00004774       .text visit_action__10daNpc_Uk_cFPv */
BOOL daNpc_Uk_c::visit_action(void*) {
    if (field_0x6c0 == 0) {
        if (dComIfGs_isTmpBit(0x40)) {
            field_0x6b9 = 13;
            setAnm(6, 0);
            mStts.SetWeight(0xFE);
        } else {
            field_0x6b9 = 2;
            setAnmStatus();
            mStts.SetWeight(0x64);
        }

        field_0x6c0++;
        field_0x6bb = 0;
    } else if (field_0x6c0 != -1) {
        bool attn;

        chkAttention();
        field_0x688 &= ~0x60;

        switch ((s8)field_0x6b9) {
        case 3:
            attn = demo01();
            break;
        case 4:
            attn = demo02();
            break;
        case 2:
            attn = visit01();
            break;
        case 1:
            attn = talk01();
            break;
        case 13:
            attn = jitanda02();
            break;
        case 5:
            attn = false;
            daNpc_Uk_setAction(this, &daNpc_Uk_c::hind_action, NULL);
            break;
        default:
            attn = false;
            break;
        }

        lookBack();
        setAttention(attn);
    }

    return TRUE;
}

#include "d/d_kankyo.h"
#include "d/d_snap.h"
#include "res/Object/Uk.h"

/* 00004774-00004A20       .text _draw__10daNpc_Uk_cFv */
BOOL daNpc_Uk_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();
    J3DModelData* headModelData = mpHeadModel->getModelData();

    if ((field_0x688 & 0x10) == 0x10) {
        return TRUE;
    }

    g_env_light.settingTevStruct(0, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);
    g_env_light.setLightTevColorType(mpHeadModel, &tevStr);

    J3DTexture* savedTexture;
    JUTNameTab* savedTextureName;

    if (getShapeType() != 0) {
        static u32 table_bmt[3] = {
            -1,
            dRes_INDEX_UK_BMT_UK_C_e,
            dRes_INDEX_UK_BMT_UK_D_e,
        };
        savedTexture = modelData->getTexture();
        savedTextureName = modelData->getTextureName();
        modelData->setMaterialTable((J3DMaterialTable*)dComIfG_getObjectRes("Uk", table_bmt[getShapeType()]), J3DMatCopyFlag_Texture);
    }

    if (getShapeType() == 1) {
        mBtpAnm.entry(headModelData, field_0x2bc);
    }

    mpMorf->entryDL();

    mpHeadModel->setBaseTRMtx(model->getAnmMtx(m_jnt.getHeadJntNum()));
    mDoExt_modelUpdateDL(mpHeadModel);

    if (getShapeType() == 1) {
        mBtpAnm.remove(headModelData);
    }

    if (getShapeType() != 0) {
        modelData->setTexture(savedTexture);
        modelData->setTextureName(savedTextureName);
    }

    cXyz tempPos(current.pos.x, current.pos.y + 50.0f, current.pos.z);
    mShadowKey = dComIfGd_setShadow(mShadowKey, 1, model, &tempPos, 300.0f, 20.0f, current.pos.y,
                                       mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);
    if (mShadowKey) {
        dComIfGd_setShadow(mShadowKey, 1, mpHeadModel, &tempPos, 300.0f, 20.0f, current.pos.y,
                            mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);
    }

    switch (getShapeType()) {
    case 0:
        dSnap_RegistFig(DSNAP_TYPE_NPC_MK, this, 1.0f, 1.0f, 1.0f);
        break;
    case 1:
        dSnap_RegistFig(DSNAP_TYPE_NPC_MK, this, 1.0f, 1.0f, 1.0f);
        break;
    case 2:
        dSnap_RegistFig(DSNAP_TYPE_NPC_MK, this, 1.0f, 1.0f, 1.0f);
        break;
    }

    return TRUE;
}

/* 00004A20-00004CE4       .text _execute__10daNpc_Uk_cFv */
BOOL daNpc_Uk_c::_execute() {
    daNpc_Uk_mem_c* m = (daNpc_Uk_mem_c*)this;

    m_jnt.setParam(0, 0x1C84, 0, -0x1C84, 0x1F40, 0x2328, -0x7D0, -0x2328, 1000);
    playTexPatternAnm();

    field_0x67e = mpMorf->play(&eyePos, field_0x6c4, field_0x6c8);

    if (mpMorf->getFrame() < field_0x680) {
        field_0x67e = 1;
    }

    field_0x680 = mpMorf->getFrame();

    if (field_0x67e) {
        switch ((s8)field_0x6ad) {
        case 6:
            if (cM_rnd() < 0.4f) {
                setAnm(7, 0);
            }
            break;
        case 7:
            setAnm(6, 0);
            break;
        }
    }

    checkOrder();
    (this->*m->mCurrActionFunc)(NULL);
    eventOrder();

    if ((field_0x688 & 0x100) != 0x100) {
        shape_angle.y = current.angle.y;
    }

    field_0x688 &= ~0x100;
    field_0x6c4 = 0;

    if ((field_0x688 & 0x10) != 0x10) {
        if ((field_0x688 & 8) == 8) {
            speed.y += gravity;
            if (speed.y < maxFallSpeed) {
                speed.y = maxFallSpeed;
            }

            fopAcM_posMove(this, m->mStts.GetCCMoveP());
        } else {
            fopAcM_posMoveF(this, m->mStts.GetCCMoveP());
        }

        m->mObjAcch.CrrPos(*dComIfG_Bgsp());

        if (m->mObjAcch.ChkGroundHit()) {
            field_0x688 &= ~0x200;
            field_0x6c4 = dComIfG_Bgsp()->GetMtrlSndId(m->mObjAcch.m_gnd);
        } else {
            field_0x688 |= 0x200;
        }

        tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(m->mObjAcch.m_gnd);
        tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(m->mObjAcch.m_gnd);
    } else {
        field_0x688 |= 0x200;
    }

    field_0x688 &= ~8;

    J3DModel* model = mpMorf->getModel();
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::YrotM(shape_angle.y);
    model->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();

    if ((field_0x688 & 0x10) == 0x10) {
        mCyl.OffCoSPrmBit(cCcD_CoSPrm_Set_e);
    } else {
        mCyl.OnCoSPrmBit(cCcD_CoSPrm_Set_e);
        setCollision();
    }

    return TRUE;
}

/* 00004CE4-00004D34       .text _delete__10daNpc_Uk_cFv */
BOOL daNpc_Uk_c::_delete() {
    dComIfG_resDelete(&mPhs, "Uk");

    if (mpMorf != NULL) {
        mpMorf->stopZelAnime();
    }

    return TRUE;
}

/* 00004D34-00004D54       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Uk_c*)i_this)->CreateHeap();
}

/* 00004D54-00005028       .text _create__10daNpc_Uk_cFv */
cPhs_State daNpc_Uk_c::_create() {
    fopAcM_ct(this, daNpc_Uk_c);

    cPhs_State phase = (cPhs_State)dComIfG_resLoad(&mPhs, "Uk");

    if (phase == cPhs_COMPLEATE_e) {
        switch (fopAcM_GetName(this)) {
        case fpcNm_NPC_UK_e:
        switch (getType()) {
        case 1:
            field_0x6bf = 1;

            switch (getShapeType()) {
            case 0:
                field_0x690 = 2.5f;
                field_0x694 = 10.0f;
                break;
            case 1:
                field_0x690 = 2.3f;
                field_0x694 = 8.5f;
                break;
            case 2:
                field_0x690 = 2.0f;
                field_0x694 = 7.0f;
                break;
            }
            break;
        case 2:
            field_0x6bf = 2;
            argument = getShapeType() + 5;
            field_0x694 = 14.0f;
            break;
        default:
            field_0x6bf = 0;
            break;
        }
        break;
        default:
            return cPhs_ERROR_e;
        }

        if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, 0xB7B0)) {
            mpMorf = NULL;
            return cPhs_ERROR_e;
        }

        tevStr.mRoomNo = current.roomNo;
        cullMtx = mpMorf->getModel()->getBaseTRMtx();
        fopAcM_setCullSizeBox(this, -35.0f, -10.0f, -35.0f, 35.0f, 100.0f, 35.0f);

        if (!init()) {
            mpMorf = NULL;
            return cPhs_ERROR_e;
        }
    }

    return phase;
}

#include "d/d_com_inf_game.h"
#include "res/Object/Uk.h"

/* 000053D8-00005728       .text CreateHeap__10daNpc_Uk_cFv */
BOOL daNpc_Uk_c::CreateHeap() {
    static u32 head_bdl_table[3] = {
        dRes_INDEX_UK_BDL_UKHEAD_B_e,
        dRes_INDEX_UK_BDL_UKHEAD_C_e,
        dRes_INDEX_UK_BDL_UKHEAD_D_e,
    };

    J3DModelData* model_data_p = (J3DModelData*)dComIfG_getObjectRes("Uk", dRes_INDEX_UK_BDL_UK_e);

    mpMorf = new mDoExt_McaMorf(
        model_data_p,
        NULL,
        NULL,
        static_cast<J3DAnmTransformKey*>(dComIfG_getObjectRes("Uk", dRes_INDEX_UK_BCK_UK_WAIT_e)),
        J3DFrameCtrl::EMode_LOOP,
        1.0f,
        0,
        -1,
        TRUE,
        NULL,
        0x80000,
        0x15021222
    );
    if (!mpMorf || !mpMorf->getModel()) {
        return FALSE;
    }

    m_jnt.setHeadJntNum(model_data_p->getJointName()->getIndex("head"));
    JUT_ASSERT(0xAD4, m_jnt.getHeadJntNum() >= 0);

    m_jnt.setBackboneJntNum(model_data_p->getJointName()->getIndex("backbone"));
    JUT_ASSERT(0xAD9, m_jnt.getBackboneJntNum() >= 0);

    J3DModelData* headModelData = static_cast<J3DModelData*>(dComIfG_getObjectRes("Uk", head_bdl_table[getShapeType()]));
    JUT_ASSERT(0xAE6, headModelData);

    mpHeadModel = mDoExt_J3DModel__create(headModelData, 0x80000, 0x11020022);
    if (!mpHeadModel) {
        return FALSE;
    }

    field_0x6ac = 0;
    if (!initTexPatternAnm(FALSE)) {
        return FALSE;
    }

    for (u16 i = 0; i < model_data_p->getJointNum(); i += 1) {
        if (i == m_jnt.getHeadJntNum() || i == m_jnt.getBackboneJntNum()) {
            mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Uk);
        }
    }

#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif
    mAcchCir.SetWall(60.0f, 30.0f);
    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir, fopAcM_GetSpeed_p(this));
    mObjAcch.SetGroundCheckOffset(100.0f);
    maxFallSpeed = -90.0f;
    field_0x68c = -1;

    return TRUE;
}

/* 00005728-00005748       .text daNpc_Uk_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Uk_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Uk_c*)i_this)->_create();
}

/* 00005748-00005768       .text daNpc_Uk_Delete__FP10daNpc_Uk_c */
static BOOL daNpc_Uk_Delete(daNpc_Uk_c* i_this) {
    return ((daNpc_Uk_c*)i_this)->_delete();
}

/* 00005768-00005788       .text daNpc_Uk_Execute__FP10daNpc_Uk_c */
static BOOL daNpc_Uk_Execute(daNpc_Uk_c* i_this) {
    return ((daNpc_Uk_c*)i_this)->_execute();
}

/* 00005788-000057A8       .text daNpc_Uk_Draw__FP10daNpc_Uk_c */
static BOOL daNpc_Uk_Draw(daNpc_Uk_c* i_this) {
    return ((daNpc_Uk_c*)i_this)->_draw();
}

/* 000057A8-000057B0       .text daNpc_Uk_IsDelete__FP10daNpc_Uk_c */
static BOOL daNpc_Uk_IsDelete(daNpc_Uk_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Uk_Method = {
    (process_method_func)daNpc_Uk_Create,
    (process_method_func)daNpc_Uk_Delete,
    (process_method_func)daNpc_Uk_Execute,
    (process_method_func)daNpc_Uk_IsDelete,
    (process_method_func)daNpc_Uk_Draw,
};

actor_process_profile_definition g_profile_NPC_UK = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_UK_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Uk_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_UK_e,
    /* Actor SubMtd */ &l_daNpc_Uk_Method,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
