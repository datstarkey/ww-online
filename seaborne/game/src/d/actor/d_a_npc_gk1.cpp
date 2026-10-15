/**
 * d_a_npc_gk1.cpp
 * NPC - Mila's Father (poor)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_npc_gk1.h"
#include "m_Do/m_Do_lib.h"
#include "d/d_snap.h"

class daNpc_Gk1_HIO_c : public mDoHIO_entry_c {
public:
    struct hio_prm_c {
        /* 0x00 */ s16 mMaxHeadX;
        /* 0x02 */ s16 mMaxHeadY;
        /* 0x04 */ s16 mMinHeadX;
        /* 0x06 */ s16 mMinHeadY;
        /* 0x08 */ s16 mMaxBackboneX;
        /* 0x0A */ s16 mMaxBackboneY;
        /* 0x0C */ s16 mMinBackboneX;
        /* 0x0E */ s16 mMinBackboneY;
        /* 0x10 */ s16 mMaxTurnStep;
        /* 0x12 */ s16 field_0x12;
        /* 0x14 */ f32 field_0x14;
        /* 0x18 */ f32 field_0x18;
    };

    daNpc_Gk1_HIO_c();
    virtual ~daNpc_Gk1_HIO_c() {}

public:


public:
    /* 0x04 */ s8 mNo;
    /* 0x05 */ u8 field_0x5[0x8 - 0x5];
    /* 0x08 */ s32 field_0x8;
    /* 0x0C */ hio_prm_c mPrmTbl;
};

static daNpc_Gk1_HIO_c l_HIO;

#define L_CHECK_SZ 20
static fopAc_ac_c* l_check_inf[L_CHECK_SZ];
static s32 l_check_wrk;

/* 000000EC-00000150       .text __ct__15daNpc_Gk1_HIO_cFv */
daNpc_Gk1_HIO_c::daNpc_Gk1_HIO_c() {
    static hio_prm_c a_prm_tbl = {
        0x2000, 0x0FA0, 0xEE6C, 0xF060,
        0x0000, 0x0DAC, 0xFC18, 0xF254,
        0x0578, 0x03E8,
        200.0f, 0.0f,
    };
    memcpy(&mPrmTbl, &a_prm_tbl, 0x1c);
    mNo = -1;
    field_0x8 = -1;
}

/* 00000198-000001E4       .text nodeCB_Head__FP7J3DNodei */
static BOOL nodeCB_Head(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        daNpc_Gk1_c* actor = (daNpc_Gk1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->_nodeCB_Head(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 000001E4-000002D8       .text _nodeCB_Head__11daNpc_Gk1_cFP7J3DNodeP8J3DModel */
void daNpc_Gk1_c::_nodeCB_Head(J3DNode* i_node, J3DModel* i_pModel) {
    static cXyz a_eye_pos_off(30.0f, 30.0f, 0.0f);
    s32 jntNo = ((J3DJoint*)i_node)->getJntNo();

    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jntNo));
    field_0x758.x = mDoMtx_stack_c::now[0][3];
    field_0x758.y = mDoMtx_stack_c::now[1][3];
    field_0x758.z = mDoMtx_stack_c::now[2][3];
    PSMTXMultVec(mDoMtx_stack_c::get(), &a_eye_pos_off, (Vec*)&field_0x734);
    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 00000314-00000360       .text nodeCB_Neck__FP7J3DNodei */
static BOOL nodeCB_Neck(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        daNpc_Gk1_c* actor = (daNpc_Gk1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->_nodeCB_Neck(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 00000360-00000400       .text _nodeCB_Neck__11daNpc_Gk1_cFP7J3DNodeP8J3DModel */
void daNpc_Gk1_c::_nodeCB_Neck(J3DNode* i_node, J3DModel* i_pModel) {
    J3DJoint* joint = (J3DJoint*)i_node;
    s32 jntNo = joint->getJntNo();

    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jntNo));
    mDoMtx_stack_c::XrotM(m_jnt.getHead_y());
    mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 00000400-0000044C       .text nodeCB_BackBone__FP7J3DNodei */
static BOOL nodeCB_BackBone(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        daNpc_Gk1_c* actor = (daNpc_Gk1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->_nodeCB_BackBone(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 0000044C-000004EC       .text _nodeCB_BackBone__11daNpc_Gk1_cFP7J3DNodeP8J3DModel */
void daNpc_Gk1_c::_nodeCB_BackBone(J3DNode* i_node, J3DModel* i_pModel) {
    J3DJoint* joint = (J3DJoint*)i_node;
    s32 jntNo = joint->getJntNo();

    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jntNo));
    mDoMtx_stack_c::XrotM(m_jnt.getBackbone_y());
    mDoMtx_stack_c::ZrotM(-m_jnt.getBackbone_x());
    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 000004EC-0000050C       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Gk1_c*)i_this)->CreateHeap();
}

static char* l_evn_tbl[] = {
    "dummy",
};

/* 0000050C-0000058C       .text init_GK1_0__11daNpc_Gk1_cFv */
bool daNpc_Gk1_c::init_GK1_0() {
    if (dComIfGs_isEventBit(0x2D01)) {
        set_action(&daNpc_Gk1_c::wait_action1, NULL);
        return true;
    }
    return false;
}

/* 0000058C-00000720       .text createInit__11daNpc_Gk1_cFv */
bool daNpc_Gk1_c::createInit() {
    mEventIdx[0] = dComIfGp_evmng_getEventIdx(l_evn_tbl[0]);
    mEventCut.setActorInfo2("Gk1", this);
    attention_info.flags = 0xA;
    cullSizeFar = 12000.0f / mDoLib_clipper::getFar();
    switch (field_0x7b3) {
    case 0:
        break;
    }

    attention_info.distances[1] = 0xA9;
    attention_info.distances[3] = 0xA9;
    gravity = -4.5f;
    field_0x7ad = 5;

    bool ok;
    switch (field_0x7b3) {
    case 0:
        ok = init_GK1_0();
        break;
    default:
        ok = false;
        break;
    }

    if (!ok) {
        return false;
    }

    field_0x726 = current.angle.x;
    field_0x728 = current.angle.y;
    field_0x72a = current.angle.z;
    shape_angle.x = field_0x726;
    shape_angle.y = field_0x728;
    shape_angle.z = field_0x72a;

    mStts.Init(0xFF, 0xFF, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);
    mObjAcch.CrrPos(*dComIfG_Bgsp());
    play_animation();
    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    mpMorf->setMorf(0.0f);
    setMtx(true);

    return true;
}

/* 00000720-000007C8       .text play_animation__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::play_animation() {
    u32 mtrlSndId = 0;
    play_btp_anm();
    if (mObjAcch.ChkGroundHit()) {
        mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(mObjAcch.m_gnd);
    }

    field_0x79c = mpMorf->play(&eyePos, mtrlSndId, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    if (mpMorf->getFrame() < field_0x770) {
        field_0x79c = 1;
    }

    field_0x770 = mpMorf->getFrame();
}

/* 000007C8-00000930       .text setMtx__11daNpc_Gk1_cFb */
void daNpc_Gk1_c::setMtx(bool i_force) {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::ZXYrotM(field_0x726, field_0x728, field_0x72a);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();

    if (field_0x6d0 != 0) {
        mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));
        ((J3DModel*)field_0x6d0)->setBaseTRMtx(mDoMtx_stack_c::get());
        ((J3DModel*)field_0x6d0)->calc();
    }

    if (field_0x6d4 != 0) {
        mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));
        ((J3DModel*)field_0x6d4)->setBaseTRMtx(mDoMtx_stack_c::get());
        ((J3DModel*)field_0x6d4)->calc();
    }

    setAttention(i_force);
}

/* 00000930-00000944       .text bckResID__11daNpc_Gk1_cFi */
int daNpc_Gk1_c::bckResID(int i_no) {
    static const int a_resID_tbl[5] = {
        0x00000003, 0x00000004, 0x00000000, 0x00000001,
        0x00000002,
    };
    return a_resID_tbl[i_no];
}

/* 00000944-00000958       .text btpResID__11daNpc_Gk1_cFi */
int daNpc_Gk1_c::btpResID(int i_no) {
    static const int a_resID_tbl[1] = {
        0x00000008,
    };
    return a_resID_tbl[i_no];
}

/* 00000958-00000A5C       .text setBtp__11daNpc_Gk1_cFScb */
bool daNpc_Gk1_c::setBtp(signed char i_idx, bool i_param) {
    J3DModel* model = mpMorf->getModel();

    if (i_idx < 0) {
        return false;
    }

    J3DAnmTexPattern* a_btp =
        (J3DAnmTexPattern*)dComIfG_getObjectIDRes(mArcName, btpResID(i_idx));
    JUT_ASSERT(0x1C3, a_btp != 0);

    field_0x7ac = i_idx;
    field_0x6f4 = 0;
    field_0x6f6 = 0;

    return mBtpAnm.init(model->getModelData(), a_btp, 1, 0, 1.0f, 0, -1, i_param, 0) != 0;
}

/* 00000A5C-00000A7C       .text init_texPttrnAnm__11daNpc_Gk1_cFScb */
bool daNpc_Gk1_c::init_texPttrnAnm(signed char i_no, bool param_2) {
    return setBtp(i_no, param_2);
}

/* 00000A7C-00000B18       .text play_btp_anm__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::play_btp_anm() {
    u8 frameMax = mBtpAnm.getBtpAnm()->getFrameMax();

    if (field_0x7ac != 0 || cLib_calcTimer(&field_0x6f6) == 0) {
        field_0x6f4++;

        if (field_0x6f4 >= frameMax) {
            if (field_0x7ac != 0) {
                field_0x6f4 = frameMax;
            } else {
                field_0x6f6 = cLib_getRndValue(0x3C, 0x5A);
                field_0x6f4 = 0;
            }
        }
    }
}

/* 00000B18-00000BB4       .text setAnm_anm__11daNpc_Gk1_cFPQ211daNpc_Gk1_c9anm_prm_c */
void daNpc_Gk1_c::setAnm_anm(daNpc_Gk1_c::anm_prm_c* i_prm) {
    if (i_prm->mBckIdx < 0 || field_0x7ad == i_prm->mBckIdx) {
        return;
    }

    dNpc_setAnmIDRes(mpMorf, i_prm->mMode, i_prm->mMorf, i_prm->mSpeed, bckResID(i_prm->mBckIdx), -1,
                     mArcName);
    field_0x7ad = i_prm->mBckIdx;
    field_0x79c = 0;
    field_0x79d = 0;
    field_0x770 = 0.0f;
}

/* 00000BB4-00000C20       .text setAnm__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[] = {
        { -1, -1, {0}, 0.0f, 0.0f, -1 },
        {  0,  0, {0}, 8.0f, 1.0f,  2 },
        { -1, -1, {0}, 0.0f, 0.0f, -1 },
    };
    init_texPttrnAnm(a_anm_prm_tbl[field_0x7af].mBtpIdx, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7af]);
}

/* 00000C20-00000C24       .text chngAnmTag__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::chngAnmTag() {
}

/* 00000C24-00000C28       .text ctrlAnmTag__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::ctrlAnmTag() {
}

/* 00000C28-00000C68       .text chngAnmAtr__11daNpc_Gk1_cFUc */
void daNpc_Gk1_c::chngAnmAtr(unsigned char i_param_1) {
    if (i_param_1 == field_0x7aa || i_param_1 > 5) {
        return;
    }

    field_0x7aa = i_param_1;
    setAnm_ATR();
}

/* 00000C68-00000C74       .text ctrlAnmAtr__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::ctrlAnmAtr() {
    switch(field_0x7aa) {
        case 5:
            break;
    }
}

/* 00000C74-00000CD8       .text setAnm_ATR__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::setAnm_ATR() {
    static anm_prm_c a_anm_prm_tbl[] = {
        { 0, 0, {0}, 8.0f, 1.0f, 2 },
        { 1, 0, {0}, 8.0f, 1.0f, 2 },
        { 2, 0, {0}, 8.0f, 1.0f, 2 },
        { 3, 0, {0}, 8.0f, 1.0f, 2 },
        { 4, 0, {0}, 8.0f, 1.0f, 2 },
    };
    init_texPttrnAnm(a_anm_prm_tbl[field_0x7aa].mBtpIdx, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7aa]);
}

/* 00000CD8-00000D94       .text anmAtr__11daNpc_Gk1_cFUs */
void daNpc_Gk1_c::anmAtr(unsigned short i_param) {
    switch (i_param) {
    case 6:
        if (field_0x7b5 == 0) {
            chngAnmAtr(dComIfGp_getMesgAnimeAttrInfo());
            field_0x7b5++;
        }

        {
            u8 tag = dComIfGp_getMesgAnimeTagInfo();
            if (tag != 0xFF && tag != field_0x7ab) {
                dComIfGp_clearMesgAnimeTagInfo();
                field_0x7ab = tag;
                chngAnmTag();
            }
        }
        break;
    case 14:
        field_0x7b5 = 0;
        break;
    }

    ctrlAnmAtr();
    ctrlAnmTag();
}

/* 00000D94-00000EAC       .text next_msgStatus__11daNpc_Gk1_cFPUl */
u16 daNpc_Gk1_c::next_msgStatus(u32* pMsgNo) {

    u16 ret = fopMsgStts_MSG_CONTINUES_e;
    switch (*pMsgNo) {
        case 0x28a1:
            *pMsgNo = 0x28a2;
            break;
        case 0x28a2:
            *pMsgNo = 0x28a3;
            break;
        case 0x28a3:
            *pMsgNo = 0x28a4;
            break;
        case 0x28a4:
            *pMsgNo = 0x28a5;
            break;
        case 0x28a6:
            *pMsgNo = 0x28a7;
            break;
        case 0x28a7:
            *pMsgNo = 0x28a8;
            break;
        case 0x28a8:
            if (dComIfGs_isEventBit(0x1640)) {
                ret = fopMsgStts_MSG_ENDS_e;
            } else {
                *pMsgNo = 0x28a9;
            }
            break;
        case 0x28a9:
            *pMsgNo = 0x28aa;
            break;
        case 0x28ab:
            *pMsgNo = 0x28ac;
            break;
        case 0x28ac:
            *pMsgNo = 0x28ad;
            break;
        case 0x28ad:
            *pMsgNo = 0x28ae;
            break;
        case 0x28af:
            *pMsgNo = 0x28b0;
            break;
        default:
            ret = fopMsgStts_MSG_ENDS_e;
            break;
    }
    return ret;

}

/* 00000EAC-00000F50       .text getMsg_GK1_0__11daNpc_Gk1_cFv */
u32 daNpc_Gk1_c::getMsg_GK1_0() {
    if (dComIfGs_isEventBit(0xB02) && !dComIfGs_isEventBit(0x1680)) {
        return 0x28A1;
    }

    if (!dKy_daynight_check()) {
        return 0x28A6 + (dComIfGs_isEventBit(0x1640) ? 1 : 0);
    }

    return dComIfGs_isEventBit(0xE08) ? 0x28AF : 0x28AB;
}

/* 00000F50-00000F8C       .text getMsg__11daNpc_Gk1_cFv */
u32 daNpc_Gk1_c::getMsg() {
    u32 msgNo = 0;
    switch (field_0x7b3) {
    case 0:
        msgNo = getMsg_GK1_0();
        break;
    }
    return msgNo;
}

/* 00000F8C-00001018       .text eventOrder__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::eventOrder() {
    s8 stt = field_0x7ae;
    if (stt == 1 || stt == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x7ae == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (stt >= 3) {
        field_0x786 = stt - 3;
        fopAcM_orderOtherEventId(this, mEventIdx[field_0x786], 0xff, 0xffff, 0, 1);
    }
}

/* 00001018-000010CC       .text checkOrder__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::checkOrder() {
    if (eventInfo.getCommand() == dEvtCmd_INDEMO_e) {
        if (dComIfGp_evmng_startCheck(mEventIdx[field_0x786]) && field_0x7ae >= 3) {
            field_0x7ae = 0;
            field_0x7aa = 0xFF;
            field_0x7ab = 0xFF;
        }
    } else if (eventInfo.getCommand() == dEvtCmd_INTALK_e) {
        if (field_0x7ae == 1 || field_0x7ae == 2) {
            field_0x7ae = 0;
            field_0x7a5 = 1;
        }
    }
}

/* 000010CC-00001164       .text chk_talk__11daNpc_Gk1_cFv */
bool daNpc_Gk1_c::chk_talk() {
    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x79e = dComIfGp_event_getPreItemNo();
            return TRUE;
        }

        return FALSE;
    }

    field_0x79e = 0xFF;

    return TRUE;
}

/* 00001164-000011A4       .text chk_parts_notMov__11daNpc_Gk1_cFv */
bool daNpc_Gk1_c::chk_parts_notMov() {
    return field_0x77a != m_jnt.getHead_y() || field_0x77c != m_jnt.getBackbone_y() || field_0x778 != current.angle.y;
}

/* 000011A4-000011F8       .text searchByID__11daNpc_Gk1_cFUiPi */
fopAc_ac_c* daNpc_Gk1_c::searchByID(fpc_ProcID i_id, int* o_notFound) {
    fopAc_ac_c* actor = NULL;
    *o_notFound = 0;
    if (!fopAcM_SearchByID(i_id, &actor)) {
        *o_notFound = 1;
    }
    return actor;
}

/* 000011F8-000013BC       .text lookBack__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::lookBack() {
    field_0x77a = m_jnt.getHead_y();
    field_0x77c = m_jnt.getBackbone_y();
    field_0x778 = current.angle.y;

    cXyz dstPos;
    cXyz srcPos = current.pos;
    srcPos.y = eyePos.y;

    dstPos.set(0.0f, 0.0f, 0.0f);
    cXyz* dstPos_p = NULL;
    s16 targetY = current.angle.y;
    bool headOnlyFollow = field_0x7a6;

    switch ((s8)field_0x7b1) {
    case 1:
        field_0x740 = dNpc_playerEyePos(-20.0f);
        dstPos = field_0x740;
        dstPos_p = &dstPos;
        break;
    case 2:
        dstPos = field_0x740;
        dstPos_p = &dstPos;
        break;
    case 3:
        targetY = field_0x79a;
        break;
    case 4: {
        int notFound;
        fopAc_ac_c* actor = searchByID(field_0x708, &notFound);
        if (actor != NULL && notFound == 0) {
            field_0x740 = actor->current.pos;
            field_0x740.y = actor->eyePos.y;
            dstPos = field_0x740;
            dstPos_p = &dstPos;
        }
        break;
    }
    }

    m_jnt.lookAtTarget_2(&current.angle.y, dstPos_p, srcPos, targetY, l_HIO.mPrmTbl.field_0x12,
                         headOnlyFollow);
}

/* 000013BC-0000143C       .text chkAttention__11daNpc_Gk1_cFv */
bool daNpc_Gk1_c::chkAttention() {
    dAttention_c* attention = &dComIfGp_getAttention();
    if (attention->LockonTruth()) {
        return this == attention->LockonTarget(0);
    }
    return this == attention->ActionTarget(0);
}

/* 0000143C-00001494       .text setAttention__11daNpc_Gk1_cFb */
void daNpc_Gk1_c::setAttention(bool i_force) {
    attention_info.position.set(current.pos.x, current.pos.y + l_HIO.mPrmTbl.field_0x14, current.pos.z);
    if (field_0x780 == 0 && !i_force) {
        return;
    }
    eyePos.set(field_0x734, field_0x738, field_0x73c);
}

/* 00001494-0000151C       .text decideType__11daNpc_Gk1_cFi */
bool daNpc_Gk1_c::decideType(int) {
    if (field_0x7b2 > 0) {
        return true;
    }

    field_0x7b2 = 1;
    field_0x7b3 = 0;
    strcpy(mArcName, "Gk");

    bool ret = false;
    if (field_0x7b2 != -1 && field_0x7b3 != -1) {
        ret = true;
    }

    return ret;
}

/* 0000151C-000015BC       .text privateCut__11daNpc_Gk1_cFi */
void daNpc_Gk1_c::privateCut(int i_idx) {
    static char* a_cut_tbl[] = {
        "dummy",
    };

    if (i_idx == -1) {
        return;
    }

    field_0x7a9 = dComIfGp_evmng_getMyActIdx(i_idx, a_cut_tbl, 1, TRUE, 0);

    if (field_0x7a9 == -1) {
        dComIfGp_evmng_cutEnd(i_idx);
        return;
    }

    dComIfGp_evmng_getIsAddvance(i_idx);
    dComIfGp_evmng_cutEnd(i_idx);
}

/* 000015BC-000015E0       .text endEvent__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x7aa = 0xff;
    field_0x7ab = 0xff;
}

/* 000015E0-00001618       .text isEventEntry__11daNpc_Gk1_cFv */
int daNpc_Gk1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mEventCut.getActorName(), NULL, 0);
}

/* 00001618-0000169C       .text event_proc__11daNpc_Gk1_cFi */
void daNpc_Gk1_c::event_proc(int i_param) {
    if (dComIfGp_evmng_endCheck(mEventIdx[field_0x786])) {
        endEvent();
    } else if (!mEventCut.cutProc()) {
        privateCut(i_param);
    }
}

/* 0000169C-00001748       .text set_action__11daNpc_Gk1_cFM11daNpc_Gk1_cFPCvPvPv_iPv */
BOOL daNpc_Gk1_c::set_action(int (daNpc_Gk1_c::*i_func)(void*), void* i_arg) {
    if (mCurrProcFunc != i_func) {
        if (mCurrProcFunc) {
            field_0x7b4 = 9;
            (this->*mCurrProcFunc)(i_arg);
        }

        mCurrProcFunc = i_func;
        field_0x7b4 = 0;
        (this->*mCurrProcFunc)(i_arg);
    }

    return TRUE;
}

/* 00001748-000017C8       .text setStt__11daNpc_Gk1_cFSc */
void daNpc_Gk1_c::setStt(s8 i_stt) {
    u8 prev_stt = field_0x7af;
    field_0x7af = i_stt;
    switch (field_0x7af) {
    case 0:
        break;
    case 1:
        field_0x7ae = 0;
        speedF = 0.0f;
        break;
    case 2:
        field_0x7ae = 0;
        field_0x7aa = 0xff;
        field_0x7ab = 0xff;
        field_0x7b5 = 0;
        field_0x7b0 = prev_stt;
        break;
    }
    setAnm();
}

/* 000017C8-000019E0       .text chk_attn__11daNpc_Gk1_cFv */
bool daNpc_Gk1_c::chk_attn() {
    f32 dist = std::sqrtf((current.pos - dComIfGp_getPlayer(0)->current.pos).abs2XZ());
    f32 heightDiff = current.pos.y - dComIfGp_getPlayer(0)->current.pos.y;
    s16 angleDiff =
        cLib_targetAngleY(&current.pos, &dComIfGp_getPlayer(0)->current.pos) - current.angle.y;

    if (field_0x7b1 == 1) {
        return dist < 200.0f && abs(angleDiff) / DEG2S_CONSTANT < 90.0f &&
               std::fabsf(heightDiff) < 300.0f;
    } else {
        return dist < 200.0f && abs(angleDiff) / DEG2S_CONSTANT < 60.0f &&
               std::fabsf(heightDiff) < 300.0f;
    }
}

/* 000019E0-00001B74       .text wait_1__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::wait_1() {
    field_0x7a7 = (current.pos - dComIfGp_getPlayer(0)->current.pos).absXZ() > 300.0f;
    if (field_0x7a7) {
        cLib_addCalcAngleS(&current.angle.y, field_0x722, 4, 0x800, 0x80);
    }

    if (field_0x7a5) {
        if (chk_talk()) {
            setStt(2);
            field_0x7b1 = 1;
            field_0x7a6 = 0;
            field_0x7a7 = 0;
            m_jnt.setTrn();
        }
        return TRUE;
    }

    field_0x7ae = 2;
    field_0x7a6 = 1;
    if (chk_attn()) {
        field_0x7b1 = 1;
        return TRUE;
    }

    field_0x7b1 = 0;
    return TRUE;
}

/* 00001B74-00001C74       .text talk_1__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::talk_1() {
    BOOL ret = chk_parts_notMov();
    talk(1);
    if (mpCurrMsg == NULL) {
        return TRUE;
    }
    switch (mpCurrMsg->mStatus) {
    case fopMsgStts_BOX_OPENING_e:
    case fopMsgStts_MSG_TYPING_e:
        break;
    case fopMsgStts_MSG_DESTROYED_e:
        switch (mCurrMsgNo) {
        case 0x28a5:
            dComIfGs_onEventBit(0x1680);
            break;
        case 0x28aa:
            dComIfGs_onEventBit(0x1640);
            break;
        }
        field_0x79e = 0xff;
        field_0x7a5 = 0;
        setStt(field_0x7b0);
        field_0x790 = cLib_getRndValue(0xf, 0x1e);
        endEvent();
        break;
    }
    return ret;
}

/* 00001C74-00001D30       .text wait_action1__11daNpc_Gk1_cFPv */
int daNpc_Gk1_c::wait_action1(void*) {
    switch (field_0x7b4) {
    case 0:
        setStt(1);
        field_0x7a7 = 1;
        field_0x7b4++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x7a4 = chkAttention();
        switch (field_0x7af) {
        case 1:
            field_0x780 = wait_1();
            break;
        case 2:
            field_0x780 = talk_1();
            break;
        }
        break;
    case 9:
        break;
    }
    return 1;
}

/* 00001D30-00001E70       .text demo__11daNpc_Gk1_cFv */
u8 daNpc_Gk1_c::demo() {
    if (demoActorID == 0) {
        if (field_0x7a8 != 0) {
            field_0x7a8 = 0;
        }
    } else {
        if (field_0x7a8 == 0) {
            field_0x7a8 = 1;
            field_0x7a1 = 0;
            m_jnt.setHead_y(0);
            m_jnt.setHead_x(0);
            m_jnt.setBackBone_y(0);
            m_jnt.setBackBone_x(0);
        }

        dDemo_actor_c* actor = dComIfGp_demo_getActor(demoActorID);

        if (mBtpAnm.getBtpAnm() != NULL) {
            u8 frameMax = mBtpAnm.getBtpAnm()->getFrameMax();
            field_0x6f4++;

            if (field_0x6f4 >= frameMax) {
                field_0x6f4 = frameMax;
            }
        }

        J3DAnmTexPattern* btp = actor->getP_BtpData(mArcName);

        if (btp != NULL) {
            mBtpAnm.init(mpMorf->getModel()->getModelData(), btp, 1, 0, 1.0f, 0, -1, true, 0);
            field_0x7ac = 1;
            field_0x6f4 = 0;
        }

        dDemo_setDemoData(this, 0x6A, mpMorf, mArcName, 0, NULL, NULL, 0);
    }

    return field_0x7a8;
}

/* 00001E70-00001F48       .text shadowDraw__11daNpc_Gk1_cFv */
void daNpc_Gk1_c::shadowDraw() {
    cXyz pos(current.pos.x, 150.0f + current.pos.y, current.pos.z);

    field_0x6dc = dComIfGd_setShadow(field_0x6dc, 1, mpMorf->getModel(), &pos, 800.0f, 40.0f,
                                     current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);

    if (field_0x6dc != 0) {
        if (field_0x6d0 != 0) {
            dComIfGd_addRealShadow(field_0x6dc, (J3DModel*)field_0x6d0);
        }

        if (field_0x6d4 != 0) {
            dComIfGd_addRealShadow(field_0x6dc, (J3DModel*)field_0x6d4);
        }
    }
}

/* 00001F48-00002064       .text _draw__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();

    if (field_0x7a0 != 0 || field_0x7a2 != 0) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);
    mBtpAnm.entry(modelData, field_0x6f4);
    mpMorf->entryDL();
    modelData->getMaterialTable().removeTexNoAnimator(mBtpAnm.getBtpAnm());

    if (field_0x6d0 != 0) {
        g_env_light.setLightTevColorType((J3DModel*)field_0x6d0, &tevStr);
        mDoExt_modelEntryDL((J3DModel*)field_0x6d0);
    }

    if (field_0x6d4 != 0) {
        g_env_light.setLightTevColorType((J3DModel*)field_0x6d4, &tevStr);
        mDoExt_modelEntryDL((J3DModel*)field_0x6d4);
    }

    shadowDraw();

    GXColor color1 = {0xff, 0x00, 0x00, 0x80};
    GXColor color2 = {0x00, 0x00, 0xff, 0x80};
    GXColor color3 = {0xff, 0xff, 0x00, 0x80};

    dSnap_RegistFig(0x59, this, 1.0f, 1.0f, 1.0f);

    return TRUE;
}

/* 00002064-00002280       .text _execute__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::_execute() {
    if (field_0x7a3 == 0) {
        field_0x714 = current.pos.x;
        field_0x718 = current.pos.y;
        field_0x71c = current.pos.z;
        field_0x720 = current.angle.x;
        field_0x722 = current.angle.y;
        field_0x724 = current.angle.z;
        field_0x7a3 = 1;
    }

    m_jnt.setParam(l_HIO.mPrmTbl.mMaxBackboneX, l_HIO.mPrmTbl.mMaxBackboneY,
        l_HIO.mPrmTbl.mMinBackboneX, l_HIO.mPrmTbl.mMinBackboneY,
        l_HIO.mPrmTbl.mMaxHeadX, l_HIO.mPrmTbl.mMaxHeadY,
        l_HIO.mPrmTbl.mMinHeadX, l_HIO.mPrmTbl.mMinHeadY,
        l_HIO.mPrmTbl.mMaxTurnStep);

    if (field_0x7a0 != 0 && demoActorID == 0) {
        return TRUE;
    }

    checkOrder();
    if (!demo()) {
        int cond = -1;
        if (dComIfGp_event_runCheck() && !eventInfo.checkCommandTalk()) {
            cond = isEventEntry();
        }
        if (cond >= 0) {
            event_proc(cond);
        } else {
            (this->*mCurrProcFunc)(0);
        }
        lookBack();
        fopAcM_posMoveF(this, mStts.GetCCMoveP());
        mObjAcch.CrrPos(*dComIfG_Bgsp());
        play_animation();
    } else {
        field_0x7a0 = 0;
    }

    eventOrder();
    field_0x726 = current.angle.x;
    field_0x728 = current.angle.y;
    field_0x72a = current.angle.z;
    if (field_0x7a1 == 0) {
        shape_angle.x = current.angle.x;
        shape_angle.y = current.angle.y;
        shape_angle.z = current.angle.z;
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);

    setMtx(false);
    if (field_0x7a8 == 0) {
        setCollision(90.0f, 200.0f);
    }
    return TRUE;
}

/* 00002280-000022D4       .text _delete__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::_delete() {
    dComIfG_resDelete(&mPhs, mArcName);

    if (heap != NULL) {
        if (mpMorf != NULL) {
            mpMorf->stopZelAnime();
        }
    }

    return TRUE;
}

/* 000022D4-00002520       .text _create__11daNpc_Gk1_cFv */
cPhs_State daNpc_Gk1_c::_create() {
    static int a_siz_tbl[] = {
        0,
        0,
    };

    cPhs_State state;

    fopAcM_ct_Retail(this, daNpc_Gk1_c);

    if (!decideType(fopAcM_GetParam(this) & 0xFF)) {
        return cPhs_ERROR_e;
    }

    state = dComIfG_resLoad(&mPhs, mArcName);
    field_0x79f = state == cPhs_COMPLEATE_e;
    if (!field_0x79f) {
        return state;
    }

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_siz_tbl[field_0x7b2])) {
        return cPhs_ERROR_e;
    }

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -90.0f, -20.0f, -80.0f, 90.0f, 200.0f, 80.0f);

    if (!createInit()) {
        return cPhs_ERROR_e;
    }

    return state;
}

/* 000027EC-00002AE8       .text bodyCreateHeap__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::bodyCreateHeap() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes(mArcName, 7);
    JUT_ASSERT(0x611, a_mdl_dat != 0);

    mpMorf = new mDoExt_McaMorf(a_mdl_dat, NULL, NULL, NULL, -1, 1.0f, 0, -1, 1, NULL, 0x80000,
                                0x11020022);

    if (mpMorf == NULL) {
        return FALSE;
    }

    if (mpMorf->getModel() == NULL) {
        mpMorf = NULL;
        return FALSE;
    }

    if (!init_texPttrnAnm(0, false)) {
        mpMorf = NULL;
        return FALSE;
    }

    m_hed_jnt_num = a_mdl_dat->getJointName()->getIndex("head");
    JUT_ASSERT(0x625, m_hed_jnt_num >= 0);

    m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone");
    JUT_ASSERT(0x627, m_bbone_jnt_num >= 0);

    m_nck_jnt_num = a_mdl_dat->getJointName()->getIndex("neck");
    JUT_ASSERT(0x629, m_nck_jnt_num >= 0);

    mpMorf->getModel()->getModelData()->getJointNodePointer(m_hed_jnt_num)->setCallBack(nodeCB_Head);
    mpMorf->getModel()->getModelData()->getJointNodePointer(m_bbone_jnt_num)->setCallBack(nodeCB_BackBone);
    mpMorf->getModel()->getModelData()->getJointNodePointer(m_nck_jnt_num)->setCallBack(nodeCB_Neck);
#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    return TRUE;
}

/* 00002AE8-00002BA8       .text itemCreateHeap__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::itemCreateHeap() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes(mArcName, 5);
    JUT_ASSERT(0x63E, a_mdl_dat != 0);

#if TARGET_PC
    // The model pointer is kept in an integer member; uintptr_t on the host (see the header).
    field_0x6d0 = (uintptr_t)mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
#else
    field_0x6d0 = (u32)mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
#endif
    if (field_0x6d0 == NULL) {
        return FALSE;
    }

    return TRUE;
}

/* 00002BA8-00002C68       .text hat_CreateHeap__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::hat_CreateHeap() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes(mArcName, 6);
    JUT_ASSERT(0x64E, a_mdl_dat != 0);

#if TARGET_PC
    // The model pointer is kept in an integer member; uintptr_t on the host (see the header).
    field_0x6d4 = (uintptr_t)mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
#else
    field_0x6d4 = (u32)mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
#endif
    if (field_0x6d4 == NULL) {
        return FALSE;
    }

    return TRUE;
}

/* 00002C68-00002D2C       .text CreateHeap__11daNpc_Gk1_cFv */
BOOL daNpc_Gk1_c::CreateHeap() {
    if (!bodyCreateHeap()) {
        return FALSE;
    }

    if (!itemCreateHeap()) {
        mpMorf = NULL;
        return FALSE;
    }

    if (!hat_CreateHeap()) {
        mpMorf = NULL;
        return FALSE;
    }

    mAcchCir.SetWall(30.0f, 90.0f);
    mObjAcch.Set(&current.pos, &old.pos, this, 1, &mAcchCir, &speed, NULL, NULL);

    return TRUE;
}

/* 00002D2C-00002D4C       .text daNpc_Gk1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Gk1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Gk1_c*)i_this)->_create();
}

/* 00002D4C-00002D6C       .text daNpc_Gk1_Delete__FP11daNpc_Gk1_c */
static BOOL daNpc_Gk1_Delete(daNpc_Gk1_c* i_this) {
    return ((daNpc_Gk1_c*)i_this)->_delete();
}

/* 00002D6C-00002D8C       .text daNpc_Gk1_Execute__FP11daNpc_Gk1_c */
static BOOL daNpc_Gk1_Execute(daNpc_Gk1_c* i_this) {
    return ((daNpc_Gk1_c*)i_this)->_execute();
}

/* 00002D8C-00002DAC       .text daNpc_Gk1_Draw__FP11daNpc_Gk1_c */
static BOOL daNpc_Gk1_Draw(daNpc_Gk1_c* i_this) {
    return ((daNpc_Gk1_c*)i_this)->_draw();
}

/* 00002DAC-00002DB4       .text daNpc_Gk1_IsDelete__FP11daNpc_Gk1_c */
static BOOL daNpc_Gk1_IsDelete(daNpc_Gk1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Gk1_Method = {
    (process_method_func)daNpc_Gk1_Create,
    (process_method_func)daNpc_Gk1_Delete,
    (process_method_func)daNpc_Gk1_Execute,
    (process_method_func)daNpc_Gk1_IsDelete,
    (process_method_func)daNpc_Gk1_Draw,
};

actor_process_profile_definition g_profile_NPC_GK1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_GK1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Gk1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_GK1_e,
    /* Actor SubMtd */ &l_daNpc_Gk1_Method,
    /* Status       */ 0x08 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
