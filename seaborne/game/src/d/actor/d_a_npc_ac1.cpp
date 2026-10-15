/**
 * d_a_npc_ac1.cpp
 * NPC - Prince Komali (after Dragon Roost Cavern)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_npc_ac1.h"
#include "res/Object/Ac.h"

class daNpc_Ac1_HIO_c : public mDoHIO_entry_c {
public:
    daNpc_Ac1_HIO_c();
    virtual ~daNpc_Ac1_HIO_c() {}

public:
    /* 0x004 */ s8 mNo;
    /* 0x005 */ u8 field_0x5[0x8 - 0x5];
    /* 0x008 */ s32 field_0x8;
    /* 0x00C */ s16 mMaxHeadX;
    /* 0x00E */ s16 mMaxHeadY;
    /* 0x010 */ s16 mMinHeadX;
    /* 0x012 */ s16 mMinHeadY;
    /* 0x014 */ s16 mMaxBackboneX;
    /* 0x016 */ s16 mMaxBackboneY;
    /* 0x018 */ s16 mMinBackboneX;
    /* 0x01A */ s16 mMinBackboneY;
    /* 0x01C */ s16 mMaxTurnStep;
    /* 0x1E */ s16 field_0x1E;
    /* 0x20 */ f32 field_0x20;
    /* 0x24 */ u8 field_0x24[0x28 - 0x24];
    /* 0x28 */ u8 field_0x28[0x3C - 0x28];
};

static daNpc_Ac1_HIO_c l_HIO;
static fopAc_ac_c* l_check_inf[20];
static int l_check_wrk;

/* 000000EC-00000150       .text __ct__15daNpc_Ac1_HIO_cFv */
daNpc_Ac1_HIO_c::daNpc_Ac1_HIO_c() {
    static u32 a_prm_tbl[12] = {
        0x20002710, 0xF000D8F0, 0x00001770, 0x0000E890,
        0x07080578, 0x43160000, 0x00000006, 0x04000000,
        0x3F000000, 0x40400000, 0x3DCCCCCD, 0x42200000,
    };

    memcpy(&mMaxHeadX, a_prm_tbl, 0x30);
    mNo = -1;
    field_0x8 = -1;
}

/* 00000198-000001E4       .text nodeCallBack_Wng__FP7J3DNodei */
static BOOL nodeCallBack_Wng(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        daNpc_Ac1_c* actor = (daNpc_Ac1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->nodeWngControl(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 000001E4-00000294       .text nodeWngControl__11daNpc_Ac1_cFP7J3DNodeP8J3DModel */
void daNpc_Ac1_c::nodeWngControl(J3DNode* i_node, J3DModel* i_model) {
    s32 jnt_no = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_model->getAnmMtx(jnt_no));
    if (jnt_no == m_wngL1_jnt_num) {
        cMtx_copy(mLeftArmMtx, j3dSys.mCurrentMtx);
        i_model->setAnmMtx(jnt_no, mLeftArmMtx);
    }
    if (jnt_no == m_wngR1_jnt_num) {
        cMtx_copy(mRightArmMtx, j3dSys.mCurrentMtx);
        i_model->setAnmMtx(jnt_no, mRightArmMtx);
    }
}

/* 00000294-000002E0       .text nodeCallBack_Arm__FP7J3DNodei */
static BOOL nodeCallBack_Arm(J3DNode* i_node, int i_param) {
    if (i_param == 0) {
        daNpc_Ac1_c* actor = (daNpc_Ac1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->nodeArmControl(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 000002E0-00000390       .text nodeArmControl__11daNpc_Ac1_cFP7J3DNodeP8J3DModel */
void daNpc_Ac1_c::nodeArmControl(J3DNode* i_node, J3DModel* i_model) {
    s32 jnt_no = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_model->getAnmMtx(jnt_no));
    if (jnt_no == m_armL1_jnt_num) {
        cMtx_copy(mLeftArmMtx, j3dSys.mCurrentMtx);
        i_model->setAnmMtx(jnt_no, mLeftArmMtx);
    }
    if (jnt_no == m_armR1_jnt_num) {
        cMtx_copy(mRightArmMtx, j3dSys.mCurrentMtx);
        i_model->setAnmMtx(jnt_no, mRightArmMtx);
    }
}

/* 00000390-000003DC       .text nodeCallBack_Ac1__FP7J3DNodei */
static BOOL nodeCallBack_Ac1(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        daNpc_Ac1_c* actor = (daNpc_Ac1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->nodeAc1Control(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 000003DC-0000056C       .text nodeAc1Control__11daNpc_Ac1_cFP7J3DNodeP8J3DModel */
void daNpc_Ac1_c::nodeAc1Control(J3DNode* i_node, J3DModel* i_model) {
    static cXyz a_eye_pos_off(20.0f, 18.0f, 0.0f);
    s32 jnt_no = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_model->getAnmMtx(jnt_no));
    if (jnt_no == m_hed_jnt_num) {
        mDoMtx_stack_c::XrotM(m_jnt.getHead_y());
        mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
        mDoMtx_stack_c::multVec(&a_eye_pos_off, &mEyePos);
    }
    if (jnt_no == m_bbone_jnt_num) {
        mDoMtx_stack_c::XrotM(m_jnt.getBackbone_y());
        mDoMtx_stack_c::ZrotM(-m_jnt.getBackbone_x());
    }
    if (jnt_no == m_arm_L_jnt_num) {
        cMtx_copy(mDoMtx_stack_c::get(), mLeftArmMtx);
    }
    if (jnt_no == m_arm_R_jnt_num) {
        cMtx_copy(mDoMtx_stack_c::get(), mRightArmMtx);
    }
    cMtx_copy(mDoMtx_stack_c::get(), j3dSys.mCurrentMtx);
    i_model->setAnmMtx(jnt_no, mDoMtx_stack_c::get());
}

/* 000005A8-00000640       .text init_AC1_0__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::init_AC1_0() {
    bool ret = dComIfGs_isEventBit(0x2E04) != 0;

    if (ret) {
        dComIfGs_isEventBit(0x1580);
        set_action(&daNpc_Ac1_c::wait_action1, NULL);
    }

    return ret;
}

/* 00000640-000007C8       .text createInit__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::createInit() {
    attention_info.flags = fopAc_Attn_LOCKON_TALK_e | fopAc_Attn_ACTION_SPEAK_e;
    attention_info.distances[fopAc_Attn_TYPE_TALK_e] = 0xA9;
    attention_info.distances[fopAc_Attn_TYPE_SPEAK_e] = 0xA9;
    gravity = -4.5f;
    m814 = current.pos;

    if (((fopAcM_GetParam(this) >> 16) & 0xFF) != 0xFF) {
        mPathRun.setInf((fopAcM_GetParam(this) >> 16) & 0xFF, fopAcM_GetRoomNo(this), true);
        if (mPathRun.isPath()) {
            fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
        } else {
            return false;
        }
    }

    mEventCut.setActorInfo2("Ac1", this);
    m871 = 4;

    bool init_success;
    switch (m879) {
    case 0:
        init_success = init_AC1_0();
        break;
    default:
        init_success = false;
        break;
    }

    if (init_success) {
        shape_angle = current.angle;
    } else {
        return false;
    }

    mStts.Init(0xFF, 0xFF, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);
    mpMorf->setMorf(0.0f);
    if (m85E) {
        m708->setMorf(0.0f);
    } else {
        m6FC->setMorf(0.0f);
    }
    setMtx(true);
    return true;
}

/* 000007C8-00000988       .text setMtx__11daNpc_Ac1_cFb */
void daNpc_Ac1_c::setMtx(bool i_setEyePos) {
    if (!m86B) {
        plyTexPttrnAnm();
        m858 = mpMorf->play(&eyePos, 0, 0);
        if (mpMorf->getFrame() < m82C) {
            m858 = true;
        }
        m82C = mpMorf->getFrame();
        if (m85E) {
            m708->play(&eyePos, 0, 0);
        } else {
            m6FC->play(&eyePos, 0, 0);
        }
        mObjAcch.CrrPos(*dComIfG_Bgsp());
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);

    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::YrotM(current.angle.y);

    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();

    if (!m85E) {
        m6FC->calc();
    } else {
        m708->calc();
    }

    if (m6D4 != NULL && m85E) {
        m6D4->setBaseTRMtx(m708->getModel()->getAnmMtx(m_hnd_R_jnt_num));
        m6D4->calc();
    }

    setAttention(i_setEyePos);
}

/* 00000988-0000099C       .text anmNum_toResID__11daNpc_Ac1_cFi */
s32 daNpc_Ac1_c::anmNum_toResID(int i_param_1) {
    static const s32 a_bck_resID_tbl[4] = {
        dRes_ID_AC_BCK_AC_WAIT01_e, dRes_ID_AC_BCK_AC_TALK01_e, dRes_ID_AC_BCK_AC_TALK02_e, dRes_ID_AC_BCK_AC_WAIT02_e,
    };
    return a_bck_resID_tbl[i_param_1];
}

/* 0000099C-000009B0       .text wingAnmNum_toResID__11daNpc_Ac1_cFi */
s32 daNpc_Ac1_c::wingAnmNum_toResID(int i_param_1) {
    static const s32 a_bck_resID_tbl[4] = {
        dRes_ID_AC_BCK_ACARM_WAIT01_e, dRes_ID_AC_BCK_ACARM_TALK01_e, dRes_ID_AC_BCK_ACARM_TALK02_e, dRes_ID_AC_BCK_ACARM_WAIT02_e,
    };
    return a_bck_resID_tbl[i_param_1];
}

/* 000009B0-000009C4       .text btpNum_toResID__11daNpc_Ac1_cFi */
s32 daNpc_Ac1_c::btpNum_toResID(int i_btpNum) {
    static const s32 a_btp_resID_tbl[3] = {dRes_ID_AC_BTP_MABA_e, dRes_ID_AC_BTP_KOMARI_e, dRes_ID_AC_BTP_WARAI_e};
    return a_btp_resID_tbl[i_btpNum];
}

/* 000009C4-00000AD4       .text setBtp__11daNpc_Ac1_cFbi */
bool daNpc_Ac1_c::setBtp(bool i_bBtpPlay, int i_btpNum) {
    J3DModelData* model_data = mpMorf->getModel()->getModelData();
    m_hed_tex_pttrn = (J3DAnmTexPattern*)dComIfG_getObjectIDRes("Ac", (int)btpNum_toResID(i_btpNum));
    JUT_ASSERT(0x216, m_hed_tex_pttrn != 0);
    bool ret = mBtpAnim.init(model_data, m_hed_tex_pttrn, 1, 2, 1.0f, 0, -1, i_bBtpPlay, 0) == 1;
    if (ret) {
        m6F4 = 0;
        field_0x6f6 = 0;
    }
    return ret;
}

/* 00000AD4-00000AFC       .text iniTexPttrnAnm__11daNpc_Ac1_cFb */
bool daNpc_Ac1_c::iniTexPttrnAnm(bool i_param_1) {
    return setBtp(i_param_1, m870);
}

/* 00000AFC-00000BA8       .text plyTexPttrnAnm__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::plyTexPttrnAnm() {
    if (m870 != 0 || cLib_calcTimer(&field_0x6f6) == 0) {
        m6F4++;

        if (m6F4 >= m_hed_tex_pttrn->getFrameMax()) {
            if (m870 != 0) {
                m6F4 = m_hed_tex_pttrn->getFrameMax();
            } else {
                field_0x6f6 = 30.0f + cM_rndF(60.0f);
                m6F4 = 0;
            }
        }
    }
}

/* 00000BA8-00000BE4       .text setAnm_tex__11daNpc_Ac1_cFSc */
void daNpc_Ac1_c::setAnm_tex(signed char i_param_1) {
    if (m870 != i_param_1) {
        m870 = i_param_1;
        iniTexPttrnAnm(true);
    }
}

/* 00000BE4-00000D0C       .text setAnm_anm__11daNpc_Ac1_cFPQ211daNpc_Ac1_c9anm_prm_c */
s32 daNpc_Ac1_c::setAnm_anm(daNpc_Ac1_c::anm_prm_c* i_prm) {
    if (m871 == i_prm->field0) {
        return 1;
    }

    m871 = i_prm->field0;
    s32 anm_res_id = anmNum_toResID(m871);
    dNpc_setAnmIDRes(mpMorf, i_prm->fieldC, i_prm->field4, i_prm->field8, anm_res_id, -1, "Ac");
    s32 wing_res_id = wingAnmNum_toResID(m871);
    m85E = (i_prm->field10 == 1);
    if (m85E) {
        dNpc_setAnmIDRes(m708, i_prm->fieldC, i_prm->field4, i_prm->field8, wing_res_id, -1, "Ac");
    } else {
        dNpc_setAnmIDRes(m6FC, i_prm->fieldC, i_prm->field4, i_prm->field8, wing_res_id, -1, "Ac");
    }
    m82C = 0.0f;
    field_0x859 = 0;
    m858 = 0;
    return 1;
}

/* 00000D0C-00000D8C       .text setAnm__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[3] = {
        {-1, -1, 0.0f, 0.0f, -1, -1},
        {0, 0, 8.0f, 1.0f, 2, 1},
        {-1, -1, 0.0f, 0.0f, -1, -1},
    };

    if (a_anm_prm_tbl[m875].field1 >= 0) {
        setAnm_tex(a_anm_prm_tbl[m875].field1);
    }

    if (a_anm_prm_tbl[m875].field0 >= 0) {
        setAnm_anm(&a_anm_prm_tbl[m875]);
    }

    return true;
}

/* 00000D8C-00000D90       .text chg_anmTag__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::chg_anmTag() {
    /* Nonmatching */
}

/* 00000D90-00000D94       .text control_anmTag__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::control_anmTag() {
    /* Nonmatching */
}

/* 00000D94-00000DD8       .text chg_anmAtr__11daNpc_Ac1_cFUc */
void daNpc_Ac1_c::chg_anmAtr(unsigned char i_param_1) {
    if (i_param_1 >= 6 || i_param_1 == m86E) {
        return;
    }
    m86E = i_param_1;
    setAnm_ATR(1);
}

/* 00000DD8-00000DE4       .text control_anmAtr__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::control_anmAtr() {
    switch (m86E) {
        case 6:
            break;
    }
}

/* 00000DE4-00000E4C       .text setAnm_ATR__11daNpc_Ac1_cFi */
void daNpc_Ac1_c::setAnm_ATR(int param_1) {
    static anm_prm_c a_anm_prm_tbl[6] = {
        {0, 0, 8.0f, 1.0f, 2, 1},
        {1, 0, 8.0f, 1.0f, 2, 1},
        {2, 1, 8.0f, 1.0f, 2, 1},
        {3, 0, 8.0f, 1.0f, 2, 1},
        {1, 2, 8.0f, 1.0f, 2, 1},
        {1, 1, 8.0f, 1.0f, 2, 1},
    };

    if (param_1 != 0) {
        setAnm_tex(a_anm_prm_tbl[m86E].field1);
    }
    setAnm_anm(&a_anm_prm_tbl[m86E]);
}

/* 00000E4C-00000F10       .text anmAtr__11daNpc_Ac1_cFUs */
void daNpc_Ac1_c::anmAtr(unsigned short i_status) {
    u32 mesgAnimeTagInfo;

    switch (i_status) {
    case 6:
        if (field_0x87b == 0) {
            m86E = 0xFF;
            chg_anmAtr(dComIfGp_getMesgAnimeAttrInfo());
            field_0x87b++;
        }

        mesgAnimeTagInfo = dComIfGp_getMesgAnimeTagInfo();
        dComIfGp_clearMesgAnimeTagInfo();

        if (mesgAnimeTagInfo != 0xFF && field_0x86f != mesgAnimeTagInfo) {
            field_0x86f = mesgAnimeTagInfo;
            chg_anmTag();
        }
        break;
    case 0xE:
        field_0x87b = 0;
        break;
    default:
        break;
    }

    control_anmTag();
    control_anmAtr();
}

/* 00000F10-00000F60       .text eventOrder__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::eventOrder() {
    if ((m874 == 1) || (m874 == 2)) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (m874 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    }
}

/* 00000F60-00000FA0       .text checkOrder__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::checkOrder() {
    if (eventInfo.checkCommandDemoAccrpt()) {
        return;
    }

    if (!eventInfo.checkCommandTalk()) {
        return;
    }

    if (m874 != 1 && m874 != 2) {
        return;
    }

    m874 = 0;
    m869 = 1;
}

/* 00000FA0-0000103C       .text chk_talk__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::chk_talk() {
    bool ret = true;
    field_0x85b = 0xFF;

    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x85b = dComIfGp_event_getPreItemNo();
        } else {
            ret = false;
        }
    }

    return ret;
}

/* 0000103C-0000107C       .text chk_partsNotMove__11daNpc_Ac1_cFv */
BOOL daNpc_Ac1_c::chk_partsNotMove() {
    BOOL ret = FALSE;

    if (field_0x844 == m_jnt.getHead_y() && field_0x846 == m_jnt.getBackbone_y() && field_0x848 == current.angle.y) {
        ret = TRUE;
    }

    return ret;
}

/* 0000107C-0000121C       .text lookBack__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::lookBack() {
    cXyz dst_pos;
    cXyz src_pos;
    cXyz* dst_pos_p;
    s16 desired_y;
    bool head_only;

    field_0x844 = m_jnt.getHead_y();
    field_0x846 = m_jnt.getBackbone_y();
    field_0x848 = current.angle.y;
    src_pos = current.pos;
    src_pos.y = eyePos.y;
    dst_pos.set(0.0f, 0.0f, 0.0f);
    dst_pos_p = NULL;
    desired_y = current.angle.y;
    head_only = m86A;

    switch (m877) {
    case 1:
        dst_pos = dNpc_playerEyePos(-20.0f);
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 2:
        dst_pos = m814;
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 3:
        desired_y = m854;
        break;
    }

    cLib_addCalcAngleS2(&m852, l_HIO.field_0x1E, 4, 0x800);
    if (!m_jnt.trnChk()) {
        m852 = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dst_pos_p, src_pos, desired_y, m852, head_only);
}

/* 0000121C-0000128C       .text next_msgStatus__11daNpc_Ac1_cFPUl */
u16 daNpc_Ac1_c::next_msgStatus(u32* pMsgNo) {
    u16 ret = fopMsgStts_MSG_CONTINUES_e;

    switch (*pMsgNo) {
    case 0x184C:
        *pMsgNo = 0x184D;
        break;
    case 0x184D:
        *pMsgNo = 0x184E;
        break;
    case 0x1850:
        *pMsgNo = 0x1851;
        break;
    case 0x1851:
        *pMsgNo = 0x1852;
        break;
    default:
        ret = fopMsgStts_MSG_ENDS_e;
        break;
    }

    return ret;
}

/* 0000128C-000012AC       .text getBitMask__11daNpc_Ac1_cFv */
s8 daNpc_Ac1_c::getBitMask() {
    s8 o_retval = 0;

    switch (m879) {
    case 0:
        o_retval = 0x10;
        break;
    }

    return o_retval;
}

/* 000012AC-00001380       .text getMsg_AC1_0__11daNpc_Ac1_cFv */
u32 daNpc_Ac1_c::getMsg_AC1_0() {
    s8 reg = dComIfGs_getEventReg(0xB8FF);
    s8 mask = getBitMask();

    if (dComIfGs_isEventBit(0x1580)) {
        if (mask & reg) {
            return 0x1853;
        }

        reg = reg | mask;
        dComIfGs_setEventReg(0xB8FF, reg);
        return 0x1850;
    }

    if (mask & reg) {
        return 0x184F;
    }

    reg = reg | mask;
    dComIfGs_setEventReg(0xB8FF, reg);
    return 0x184C;
}

/* 00001380-000013BC       .text getMsg__11daNpc_Ac1_cFv */
u32 daNpc_Ac1_c::getMsg() {
    u32 msgNo = 0;
    switch (m879) {
        case 0:
            msgNo = getMsg_AC1_0();
            break;
    }
    return msgNo;
}

/* 000013BC-0000143C       .text chkAttention__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::chkAttention() {
    dAttention_c& attention = dComIfGp_getAttention();

    if (attention.LockonTruth()) {
        return this == attention.LockonTarget(0);
    } else {
        return this == attention.ActionTarget(0);
    }
}

/* 0000143C-00001494       .text setAttention__11daNpc_Ac1_cFb */
void daNpc_Ac1_c::setAttention(bool i_param_1) {
    attention_info.position.set(current.pos.x, current.pos.y + l_HIO.field_0x20, current.pos.z);

    if (field_0x864 == 0 && !i_param_1) {
        return;
    }

    eyePos.set(mEyePos.x, mEyePos.y, mEyePos.z);
}

/* 00001494-000014B0       .text charDecide__11daNpc_Ac1_cFi */
bool daNpc_Ac1_c::charDecide(int) {
    field_0x878 = 0;
    m879 = -1;
    m879 = 0;
    return true;
}

/* 000014B0-00001508       .text event_actionInit__11daNpc_Ac1_cFi */
void daNpc_Ac1_c::event_actionInit(int i_staffIdx) {
    int* act_no_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "ActNo");

    if (act_no_p != NULL) {
        field_0x86d = *act_no_p;
    }
}

/* 00001508-00001528       .text event_action__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::event_action() {
    switch (field_0x86d) {
    case 0:
        return true;
    }

    return true;
}

/* 00001528-00001614       .text privateCut__11daNpc_Ac1_cFi */
void daNpc_Ac1_c::privateCut(int i_staffIdx) {
    static char* a_cut_tbl[] = {"ACTION"};

    if (i_staffIdx != -1) {
        field_0x86c = dComIfGp_evmng_getMyActIdx(i_staffIdx, a_cut_tbl, ARRAY_SIZE(a_cut_tbl), TRUE, 0);
        if (field_0x86c == -1) {
            dComIfGp_evmng_cutEnd(i_staffIdx);
        } else {
            if (dComIfGp_evmng_getIsAddvance(i_staffIdx)) {
                switch (field_0x86c) {
                    case 0:
                        event_actionInit(i_staffIdx);
                        break;
                }
            }

            bool temp;
            switch (field_0x86c) {
                case 0:
                    temp = event_action();
                    break;
                default:
                    temp = true;
                    break;
            }

            if (temp) {
                dComIfGp_evmng_cutEnd(i_staffIdx);
            }
        }
    }
}

/* 00001614-00001634       .text endEvent__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::endEvent() {
    dComIfGp_event_reset();
    m86E = 0xFF;
}

/* 00001634-0000166C       .text isEventEntry__11daNpc_Ac1_cFv */
int daNpc_Ac1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mEventCut.getActorName());
}

/* 0000166C-000016C4       .text event_proc__11daNpc_Ac1_cFi */
void daNpc_Ac1_c::event_proc(int i_param_1) {
    if (!mEventCut.cutProc()) {
        privateCut(i_param_1);
    }
    lookBack();
}

/* 000016C4-00001770       .text set_action__11daNpc_Ac1_cFM11daNpc_Ac1_cFPCvPvPv_iPv */
BOOL daNpc_Ac1_c::set_action(ActionFunc i_action, void* i_param) {
    if (mCurrActionFunc != i_action) {
        if (mCurrActionFunc) {
            m87A = 9;
            (this->*mCurrActionFunc)(i_param);
        }

        mCurrActionFunc = i_action;
        m87A = 0;
        (this->*mCurrActionFunc)(i_param);
    }

    return TRUE;
}

/* 00001770-000017D4       .text setStt__11daNpc_Ac1_cFSc */
void daNpc_Ac1_c::setStt(signed char i_status) {
    s8 prev_status = m875;
    m84C = 0;
    m875 = i_status;

    switch (m875) {
    case 0:
    case 1:
        break;
    case 2:
        m86E = 0xFF;
        m877 = 1;
        m876 = prev_status;
        m_jnt.setTrn();
        return;
    }

    setAnm();
}

/* 000017D4-0000189C       .text wait_1__11daNpc_Ac1_cFv */
BOOL daNpc_Ac1_c::wait_1() {
    if (m874 == 1 || m874 >= 3) {
        return TRUE;
    }

    if (m869 != 0) {
        if (chk_talk()) {
            setStt(2);
        }

        return TRUE;
    }

    m874 = 2;

    if (field_0x868 != 0) {
        m84C = 60;
    }

    if (cLib_calcTimer(&m84C) != 0) {
        m877 = 1;
    } else {
        m877 = 3;
        m854 = m800.y;
        m_jnt.setTrn();
    }

    return TRUE;
}

/* 0000189C-00001978       .text talk_1__11daNpc_Ac1_cFv */
BOOL daNpc_Ac1_c::talk_1() {
    u8 ret = chk_partsNotMove();

    if (mpCurrMsg != NULL) {
        field_0x856 = mpCurrMsg->mStatus;
    } else {
        field_0x856 = 0;
    }

    talk(1);

    if (mpCurrMsg != NULL) {
        switch (mpCurrMsg->mStatus) {
        case fopMsgStts_BOX_OPENING_e:
        case fopMsgStts_MSG_TYPING_e:
            break;
        case fopMsgStts_MSG_DESTROYED_e:
            dComIfGs_onEventBit(0x3F02);
            field_0x85b = 0xFF;
            m869 = 0;
            setStt(m876);
            m84C = 60;
            endEvent();
            break;
        }
    }

    return ret;
}

/* 00001978-00001A34       .text wait_action1__11daNpc_Ac1_cFPv */
BOOL daNpc_Ac1_c::wait_action1(void*) {
    switch (m87A) {
    case 0:
        setStt(1);
        m87A++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x868 = chkAttention();

        switch (m875) {
        case 1:
            field_0x864 = wait_1();
            break;
        case 2:
            field_0x864 = talk_1();
            break;
        }

        lookBack();
        break;
    case 9:
        break;
    }

    return TRUE;
}

/* 00001A34-00001B74       .text demo__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::demo() {
    if (demoActorID == 0) {
        if (m86B != 0) {
            m86B = 0;
        }
    } else {
        m86B = 1;
        dDemo_actor_c* demo_actor = dComIfGp_demo_getActor(demoActorID);
        if (m_hed_tex_pttrn) {
            m6F4 += 1;
            if (m6F4 >= m_hed_tex_pttrn->getFrameMax()) {
                m6F4 = m_hed_tex_pttrn->getFrameMax();
            }
        }
        J3DAnmTexPattern* demopattern = demo_actor->getP_BtpData("Ac");
        if (demopattern) {
            m_hed_tex_pttrn = demopattern;
            if (mBtpAnim.init(mpMorf->getModel()->getModelData(), m_hed_tex_pttrn, 1, 2, 1.0f, 0, -1, true, FALSE)) {
                m870 = 3;
                m6F4 = 0;
            }
        }
        dDemo_setDemoData(this, 0x6a, mpMorf, "Ac");
    }
    return m86B;
}

/* 00001B74-00001C70       .text shadowDraw__11daNpc_Ac1_cFv */
void daNpc_Ac1_c::shadowDraw() {
    cXyz shadow_pos(current.pos.x, current.pos.y + 150.0f, current.pos.z);

    mShadowId = dComIfGd_setShadow(
        mShadowId, 1, mpMorf->getModel(), &shadow_pos, 800.0f, 40.0f,
        current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr,
        0, 1.0f, dDlst_shadowControl_c::getSimpleTex()
    );
    if (mShadowId) {
        if (m85E) {
            dComIfGd_addRealShadow(mShadowId, m708->getModel());
        } else {
            dComIfGd_addRealShadow(mShadowId, m6FC->getModel());
        }
        if (m6D4) {
            dComIfGd_addRealShadow(mShadowId, m6D4);
        }
    }
}

#include "d/d_snap.h"

/* 00001C70-00001DC4       .text _draw__11daNpc_Ac1_cFv */
BOOL daNpc_Ac1_c::_draw() {
    GXColor unused_color0 = {0xFF, 0x00, 0x00, 0x80};
    GXColor unused_color1 = {0x00, 0x00, 0xFF, 0x80};
    J3DModel* model = mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();

    if (m85D || m861) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);

    mBtpAnim.entry(modelData, m6F4);
    mpMorf->entryDL();
    modelData->getMaterialTable().removeTexNoAnimator(mBtpAnim.getBtpAnm());

    if (m85E) {
        g_env_light.setLightTevColorType(m708->getModel(), &tevStr);
        m708->entryDL();
    } else {
        g_env_light.setLightTevColorType(m6FC->getModel(), &tevStr);
        m6FC->entryDL();
    }

    if (m6D4 && m85E) {
        g_env_light.setLightTevColorType(m6D4, &tevStr);
        mDoExt_modelEntryDL(m6D4);
    }

    shadowDraw();
    dSnap_RegistFig(DSNAP_TYPE_UNK8B, this, 1.0f, 1.0f, 1.0f);

    return TRUE;
}

/* 00001DC4-00001F80       .text _execute__11daNpc_Ac1_cFv */
BOOL daNpc_Ac1_c::_execute() {
    if (!m863) {
        m7F4 = current.pos;
        m800 = current.angle;
        m863 = true;
    }
    m_jnt.setParam(
        l_HIO.mMaxBackboneX, l_HIO.mMaxBackboneY, l_HIO.mMinBackboneX, l_HIO.mMinBackboneY,
        l_HIO.mMaxHeadX, l_HIO.mMaxHeadY, l_HIO.mMinHeadX, l_HIO.mMinHeadY,
        l_HIO.mMaxTurnStep
    );
    if (m85D && demoActorID == 0) {
        return TRUE;
    }
    m860 = 0;
    m85D = 0;
    checkOrder();
    if (!demo()) {
        s32 evt = -1;
        if (dComIfGp_event_runCheck() && eventInfo.checkCommandTalk() == 0) {
            evt = isEventEntry();
        }
        if (evt >= 0) {
            event_proc(evt);
        } else {
            (this->*mCurrActionFunc)(NULL);
        }
        if (!m860) {
            fopAcM_posMoveF(this, mStts.GetCCMoveP());
        }
        if (!m85F) {
            shape_angle = current.angle;
        }
    }
    eventOrder();
    setMtx(false);
    if (!m86B) {
        setCollision(50.0f, 140.0f);
    }
    return TRUE;
}

/* 00001F80-00001FFC       .text _delete__11daNpc_Ac1_cFv */
BOOL daNpc_Ac1_c::_delete() {
    dComIfG_resDelete((request_of_phase_process_class*)&m6C4, "Ac");

    if (heap != NULL) {
        if (mpMorf != NULL) {
            mpMorf->stopZelAnime();
        }
        if (m6FC != NULL) {
            m6FC->stopZelAnime();
        }
        if (m708 != NULL) {
            m708->stopZelAnime();
        }
    }

    return TRUE;
}

/* 00001FFC-0000201C       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Ac1_c*)i_this)->CreateHeap();
}

/* 0000201C-0000213C       .text _create__11daNpc_Ac1_cFv */
cPhs_State daNpc_Ac1_c::_create() {
    fopAcM_ct(this, daNpc_Ac1_c);

    cPhs_State resLoadResult = dComIfG_resLoad((request_of_phase_process_class*)&m6C4, "Ac");
    if (resLoadResult != cPhs_COMPLEATE_e) {
        return resLoadResult;
    }

    if (!charDecide(fopAcM_GetParam(this) & 0xFF)) {
        return cPhs_ERROR_e;
    }

    static u32 a_size_tbl[] = { 0x272E0 };

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_size_tbl[field_0x878])) {
        return cPhs_ERROR_e;
    }

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -50.0f, -20.0f, -50.0f, 50.0f, 140.0f, 50.0f);

    if (!createInit()) {
        return cPhs_ERROR_e;
    }

    return resLoadResult;
}

daNpc_Ac1_c::daNpc_Ac1_c() {}

/* 0000256C-00002838       .text create_Anm__11daNpc_Ac1_cFv */
J3DModelData* daNpc_Ac1_c::create_Anm() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BDL_AC_e);
    JUT_ASSERT(0x6BE, a_mdl_dat != NULL);
    mpMorf = new mDoExt_McaMorf(
        a_mdl_dat,
        NULL,
        NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BCK_AC_WAIT01_e),
        J3DFrameCtrl::EMode_LOOP,
        1.0f,
        0,
        -1,
        1,
        NULL,
        0x80000,
        0x11020022
    );
    if (mpMorf == NULL) {
        return NULL;
    } else if (mpMorf->getModel() == NULL) {
        mpMorf = NULL;
        return NULL;
    }

    m_hed_jnt_num = a_mdl_dat->getJointName()->getIndex("head");
    JUT_ASSERT(0x6D2, m_hed_jnt_num >= 0);
    m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone");
    JUT_ASSERT(0x6D5, m_bbone_jnt_num >= 0);
    m_arm_L_jnt_num = a_mdl_dat->getJointName()->getIndex("armL");
    JUT_ASSERT(0x6D8, m_arm_L_jnt_num >= 0);
    m_arm_R_jnt_num = a_mdl_dat->getJointName()->getIndex("armR");
    JUT_ASSERT(0x6DB, m_arm_R_jnt_num >= 0);
    return a_mdl_dat;
}

/* 00002838-00002A4C       .text create_wng_Anm__11daNpc_Ac1_cFv */
J3DModelData* daNpc_Ac1_c::create_wng_Anm() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BDL_ACWING_e);
    JUT_ASSERT(0x6EA, a_mdl_dat != NULL);
    m6FC = new mDoExt_McaMorf(
        a_mdl_dat,
        NULL,
        NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BCK_ACARM_WAIT01_e),
        J3DFrameCtrl::EMode_LOOP,
        1.0f,
        0,
        -1,
        1,
        NULL,
        0x80000,
        0x11000002
    );
    if (m6FC == NULL) {
        return NULL;
    } else if (m6FC->getModel() == NULL) {
        m6FC = NULL;
        return NULL;
    }

    m_wngL1_jnt_num = a_mdl_dat->getJointName()->getIndex("wingL_loc");
    JUT_ASSERT(0x6FF, m_wngL1_jnt_num >= 0);
    m_wngR1_jnt_num = a_mdl_dat->getJointName()->getIndex("wingR_loc");
    JUT_ASSERT(0x702, m_wngR1_jnt_num >= 0);
    return a_mdl_dat;
}

/* 00002A4C-00002CBC       .text create_arm_Anm__11daNpc_Ac1_cFv */
J3DModelData* daNpc_Ac1_c::create_arm_Anm() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BDL_ACARM_e);
    JUT_ASSERT(0x711, a_mdl_dat != NULL);
    m708 = new mDoExt_McaMorf(
        a_mdl_dat,
        NULL,
        NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BCK_ACARM_WAIT01_e),
        J3DFrameCtrl::EMode_LOOP,
        1.0f,
        0,
        -1,
        1,
        NULL,
        0x80000,
        0x11000002
    );
    if (m708 == NULL) {
        return NULL;
    } else if (m708->getModel() == NULL) {
        m708 = NULL;
        return NULL;
    }

    m_hnd_R_jnt_num = a_mdl_dat->getJointName()->getIndex("handR");
    JUT_ASSERT(0x726, m_hnd_R_jnt_num >= 0);
    m_armL1_jnt_num = a_mdl_dat->getJointName()->getIndex("armL_loc");
    JUT_ASSERT(0x729, m_armL1_jnt_num >= 0);
    m_armR1_jnt_num = a_mdl_dat->getJointName()->getIndex("armR_loc");
    JUT_ASSERT(0x72C, m_armR1_jnt_num >= 0);
    return a_mdl_dat;
}

/* 00002CBC-00002DC8       .text create_itm_Mdl__11daNpc_Ac1_cFv */
bool daNpc_Ac1_c::create_itm_Mdl() {
    J3DModelData* a_mdl_dat;

    m6D4 = NULL;
    if (dComIfGs_isEventBit(0x1580)) {
        a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BDL_AC_FLOWER02_e);
    } else {
        a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Ac", dRes_ID_AC_BDL_AC_FLOWER_e);
    }
    JUT_ASSERT(0x744, a_mdl_dat != NULL);
    m6D4 = mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
    return true;
}

/* 00002DC8-00003044       .text CreateHeap__11daNpc_Ac1_cFv */
BOOL daNpc_Ac1_c::CreateHeap() {
    J3DModelData* anm_model = create_Anm();
    if (!anm_model) {
        return FALSE;
    }

    m870 = 0;
    if (!iniTexPttrnAnm(false)) {
        mpMorf = NULL;
        return FALSE;
    }

    J3DModelData* modeldat = create_wng_Anm();
    if (!modeldat) {
        mpMorf = NULL;
        return FALSE;
    }

    J3DModelData* arm_anmdata = create_arm_Anm();
    if (arm_anmdata != NULL && create_itm_Mdl()) {
        for (u16 i = 0; i < modeldat->getJointNum(); i++) {
            if (i == m_wngL1_jnt_num || i == m_wngR1_jnt_num) {
                m6FC->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Wng);
            }
        }
#if TARGET_PC
        m6FC->getModel()->setUserArea((uintptr_t)this);
#else
        m6FC->getModel()->setUserArea((u32)this);
#endif

        for (u16 i = 0; i < arm_anmdata->getJointNum(); i++) {
            if (i == m_armL1_jnt_num || i == m_armR1_jnt_num) {
                m708->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Arm);
            }
        }
#if TARGET_PC
        m708->getModel()->setUserArea((uintptr_t)this);
#else
        m708->getModel()->setUserArea((u32)this);
#endif

        for (u16 i = 0; i < anm_model->getJointNum(); i++) {
            if (i == m_hed_jnt_num || i == m_bbone_jnt_num || i == m_arm_L_jnt_num || i == m_arm_R_jnt_num) {
                mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Ac1);
            }
        }
#if TARGET_PC
        mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
        mpMorf->getModel()->setUserArea((u32)this);
#endif

        mAcchCir.SetWall(30.0f, 50.0f);
        mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir, fopAcM_GetSpeed_p(this), NULL, NULL);

        return TRUE;
    } else {
        mpMorf = NULL;
        m6FC = NULL;
        return FALSE;
    }
}

/* 00003044-00003064       .text daNpc_Ac1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Ac1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Ac1_c*)i_this)->_create();
}

/* 00003064-00003084       .text daNpc_Ac1_Delete__FP11daNpc_Ac1_c */
static BOOL daNpc_Ac1_Delete(daNpc_Ac1_c* i_this) {
    return ((daNpc_Ac1_c*)i_this)->_delete();
}

/* 00003084-000030A4       .text daNpc_Ac1_Execute__FP11daNpc_Ac1_c */
static BOOL daNpc_Ac1_Execute(daNpc_Ac1_c* i_this) {
    return ((daNpc_Ac1_c*)i_this)->_execute();
}

/* 000030A4-000030C4       .text daNpc_Ac1_Draw__FP11daNpc_Ac1_c */
static BOOL daNpc_Ac1_Draw(daNpc_Ac1_c* i_this) {
    return ((daNpc_Ac1_c*)i_this)->_draw();
}

/* 000030C4-000030CC       .text daNpc_Ac1_IsDelete__FP11daNpc_Ac1_c */
static BOOL daNpc_Ac1_IsDelete(daNpc_Ac1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Ac1_Method = {
    (process_method_func)daNpc_Ac1_Create,
    (process_method_func)daNpc_Ac1_Delete,
    (process_method_func)daNpc_Ac1_Execute,
    (process_method_func)daNpc_Ac1_IsDelete,
    (process_method_func)daNpc_Ac1_Draw,
};

actor_process_profile_definition g_profile_NPC_AC1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_AC1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Ac1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_AC1_e,
    /* Actor SubMtd */ &l_daNpc_Ac1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
