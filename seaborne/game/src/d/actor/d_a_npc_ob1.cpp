/**
 * d_a_npc_ob1.cpp
 * NPC - Rose
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_npc_ob1.h"
#include "d/d_com_inf_game.h"
#include "d/d_com_inf_game.h"
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_kb.h"
#include "f_pc/f_pc_executor.h"
#include "res/Object/Ob.h"

class daNpc_Ob1_HIO_c : public mDoHIO_entry_c {
public:
    daNpc_Ob1_HIO_c();
    virtual ~daNpc_Ob1_HIO_c() {}

public:

public:
    /* 0x004 */ s8 mNo;
    /* 0x005 */ u8 field_0x5[0x8 - 0x5];
    /* 0x008 */ s32 field_0x8;
    /* 0x00C */ s16 field_0xc;
    /* 0x00E */ s16 field_0xe;
    /* 0x010 */ s16 field_0x10;
    /* 0x012 */ s16 field_0x12;
    /* 0x014 */ s16 field_0x14;
    /* 0x016 */ s16 field_0x16;
    /* 0x018 */ s16 field_0x18;
    /* 0x01A */ s16 field_0x1a;
    /* 0x01C */ s16 field_0x1c;
    /* 0x01E */ s16 field_0x1e;
    /* 0x020 */ f32 field_0x20;
    /* 0x024 */ u8 field_0x24[0x28 - 0x24];
    /* 0x028 */ s16 m028;
    /* 0x02A */ u8 field_0x2a[0x2C - 0x2A];
    /* 0x02C */ f32 m02C;
    /* 0x030 */ f32 m030;
    /* 0x034 */ f32 m034;
    /* 0x038 */ f32 m038;
};

static daNpc_Ob1_HIO_c l_HIO;
static fopAc_ac_c* l_check_inf[20];
static int l_check_wrk;
static u8 l_check_flg;

/* 000000EC-00000150       .text __ct__15daNpc_Ob1_HIO_cFv */
daNpc_Ob1_HIO_c::daNpc_Ob1_HIO_c() {
    static u32 a_prm_tbl[12] = {
        0x20001388, 0xFC18EC78, 0x05DC1F40, 0xF830E0C0,
        0x04B004B0, 0x432A0000, 0x00000006, 0x04000000,
        0x3F000000, 0x40400000, 0x3DCCCCCD, 0x42200000,
    };

    memcpy(&field_0xc, a_prm_tbl, 0x30);
    mNo = -1;
    field_0x8 = -1;
}

/* 00000198-00000228       .text searchActor_Kb__FPvPv */
static void* searchActor_Kb(void* i_act, void*) {
    if (l_check_wrk < 20 && fopAc_IsActor(i_act) && fopAcM_GetName(i_act) == fpcNm_KB_e) {
        u8 flg = ((kb_class*)i_act)->m405;
        if (flg != 0) {
            l_check_flg |= flg;
        }
        l_check_inf[l_check_wrk] = (fopAc_ac_c*)i_act;
        l_check_wrk++;
    }
    return NULL;
}

/* 00000228-00000274       .text nodeCallBack_Ob1__FP7J3DNodei */
static BOOL nodeCallBack_Ob1(J3DNode* i_node, int i_param) {
    if (i_param == 0) {
        daNpc_Ob1_c* actor = (daNpc_Ob1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->nodeOb1Control(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 00000274-000003C4       .text nodeOb1Control__11daNpc_Ob1_cFP7J3DNodeP8J3DModel */
void daNpc_Ob1_c::nodeOb1Control(J3DNode* i_node, J3DModel* i_model) {
    static cXyz a_eye_pos_off(20.0f, -20.0f, 0.0f);
    s32 jnt_no = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_model->getAnmMtx(jnt_no));
    if (jnt_no == m_hed_jnt_num) {
        mDoMtx_stack_c::YrotM(-m_jnt.getHead_y());
        mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
        mDoMtx_stack_c::multVec(&a_eye_pos_off, &mEyePos);
    }
    if (jnt_no == m_bbone_jnt_num) {
        mDoMtx_stack_c::XrotM(m_jnt.getBackbone_y());
        mDoMtx_stack_c::ZrotM(m_jnt.getBackbone_x());
    }
    cMtx_copy(mDoMtx_stack_c::get(), j3dSys.mCurrentMtx);
    i_model->setAnmMtx(jnt_no, mDoMtx_stack_c::get());
}

static char* l_evn_tbl[] = {"Get_Rupee"};

/* 00000400-00000480       .text init_OB1_0__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::init_OB1_0() {
    if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_0520)) {
        set_action(&daNpc_Ob1_c::wait_action1, NULL);
        return TRUE;
    } else {
        return FALSE;
    }
}

/* 00000480-00000518       .text init_OB1_1__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::init_OB1_1() {
    if (dComIfGs_isEventBit(0x520) && !dKy_daynight_check()) {
        fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
        set_action(&daNpc_Ob1_c::wait_action2, NULL);
        return TRUE;
    }
    return FALSE;
}

/* 00000518-000005A4       .text init_OB1_2__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::init_OB1_2() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0520) && dKy_daynight_check() == 1) {
        set_action(&daNpc_Ob1_c::wait_action2, NULL);
        return TRUE;
    }

    return FALSE;
}

/* 000005A4-000007AC       .text createInit__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::createInit() {
    for (int i = 0; i < 1; i++) {
        mEventIdx[i] = dComIfGp_evmng_getEventIdx(l_evn_tbl[i]);
    }

    attention_info.flags = fopAc_Attn_LOCKON_TALK_e | fopAc_Attn_ACTION_SPEAK_e;
    attention_info.distances[fopAc_Attn_TYPE_TALK_e] = 0xAC;
    attention_info.distances[fopAc_Attn_TYPE_SPEAK_e] = 0xAC;

    gravity = -4.5f;

    field_0x784 = current.angle.x;
    field_0x786 = current.angle.y;
    field_0x788 = current.angle.z;

    field_0x778 = current.pos.x;
    field_0x77c = current.pos.y;
    field_0x780 = current.pos.z;

    mAttPos.x = field_0x778;
    mAttPos.y = field_0x77c;
    mAttPos.z = field_0x780;

    s32 weight = 0xFF;
    u8 pathIdx = fopAcM_GetParamBit(fopAcM_GetParam(this), 0x10, 8);
    if (pathIdx != 0xFF) {
        mPathRun.setInf(pathIdx, current.roomNo, 1);
        if (!(mPathRun.isPath())) {
            return FALSE;
        } else {
            actor_status &= ~fopAcStts_NOCULLEXEC_e;
            weight = 0xD9;
        }
    }

    mEventCut.setActorInfo2("Ob1", this);

    field_0x803 = 8;

    BOOL ok;
    switch (field_0x809) {
    case 0:
        ok = init_OB1_0();
        break;
    case 1:
        ok = init_OB1_1();
        break;
    case 2:
        ok = init_OB1_2();
        break;
    default:
        ok = 0;
        break;
    }

    if (ok & 0xFF) {
        field_0x78a = current.angle.x;
        field_0x78c = current.angle.y;
        field_0x78e = current.angle.z;
        shape_angle.x = field_0x78a;
        shape_angle.y = field_0x78c;
        shape_angle.z = field_0x78e;
    } else {
        return FALSE;
    }

    mStts.Init(weight, 0xFF, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);
    mpMorf->setMorf(0.0f);
    setMtx(true);

    return TRUE;
}

/* 000007AC-00000900       .text setMtx__11daNpc_Ob1_cFb */
void daNpc_Ob1_c::setMtx(bool i_setEyePos) {
    if (!field_0x7fb) {
        plyTexPttrnAnm();
        field_0x7e4 = mpMorf->play(&eyePos, 0, 0);
        if (mpMorf->getFrame() < field_0x7b4) {
            field_0x7e4 = true;
        }
        field_0x7b4 = mpMorf->getFrame();
        mObjAcch.CrrPos(*dComIfG_Bgsp());
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(field_0x78c);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();
    field_0x6d4->setBaseTRMtx(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));
    field_0x6d4->calc();
    setAttention(i_setEyePos);
}

/* 00000900-00000914       .text anmNum_toResID__11daNpc_Ob1_cFi */
int daNpc_Ob1_c::anmNum_toResID(int i_no) {
    static const int a_bck_resID_tbl[8] = {
        dRes_ID_OB_BCK_WAIT_e,
        dRes_ID_OB_BCK_TALK_e,
        dRes_ID_OB_BCK_THANK_e,
        dRes_ID_OB_BCK_WALK_e,
        dRes_ID_OB_BCK_THANK02_e,
        dRes_ID_OB_BCK_LAUGH_e,
        dRes_ID_OB_BCK_SHUTUP01_e,
        dRes_ID_OB_BCK_SHUTUP02_e,
    };
    return a_bck_resID_tbl[i_no];
}

/* 00000914-00000928       .text btpNum_toResID__11daNpc_Ob1_cFi */
int daNpc_Ob1_c::btpNum_toResID(int i_no) {
    static const int a_btp_resID_tbl[2] = {dRes_ID_OB_BTP_MABA_A_e, dRes_ID_OB_BTP_MABA_A_e};
    return a_btp_resID_tbl[i_no];
}

#include "JSystem/JUtility/JUTAssert.h"

/* 00000928-00000A34       .text setBtp__11daNpc_Ob1_cFbi */
bool daNpc_Ob1_c::setBtp(bool i_modify, int i_btpNum) {
    J3DModelData* model_data = field_0x6d4->getModelData();
    int res_id = btpNum_toResID(i_btpNum);
    m_hed_tex_pttrn = (J3DAnmTexPattern*)dComIfG_getObjectIDRes("Ob", (u16)res_id);
    JUT_ASSERT(511, m_hed_tex_pttrn != 0);

    if (mBtpAnm.init(model_data, m_hed_tex_pttrn, 1, 2, 1.0f, 0, -1, i_modify, 0) != 0) {
        field_0x6f0 = 0;
        field_0x6f2 = 0;
        return true;
    }
    return false;
}

/* 00000A34-00000A5C       .text iniTexPttrnAnm__11daNpc_Ob1_cFb */
bool daNpc_Ob1_c::iniTexPttrnAnm(bool i_modify) {
    return setBtp(i_modify, field_0x802);
}

/* 00000A5C-00000B08       .text plyTexPttrnAnm__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::plyTexPttrnAnm() {
    if (field_0x802 != 0 || cLib_calcTimer(&field_0x6f2) == 0) {
        field_0x6f0++;

        if (field_0x6f0 >= m_hed_tex_pttrn->getFrameMax()) {
            if (field_0x802 != 0) {
                field_0x6f0 = m_hed_tex_pttrn->getFrameMax();
            } else {
                field_0x6f2 = 30.0f + cM_rndF(60.0f);
                field_0x6f0 = 0;
            }
        }
    }
}

/* 00000B08-00000B44       .text setAnm_tex__11daNpc_Ob1_cFSc */
void daNpc_Ob1_c::setAnm_tex(signed char i_texNo) {
    if (field_0x802 != i_texNo) {
        field_0x802 = i_texNo;
        iniTexPttrnAnm(true);
    }
}

/* 00000B44-00000C04       .text setAnm_anm__11daNpc_Ob1_cFPQ211daNpc_Ob1_c9anm_prm_c */
s32 daNpc_Ob1_c::setAnm_anm(daNpc_Ob1_c::anm_prm_c* i_prm) {
    if (field_0x803 == i_prm->mAnmNo) {
        return 1;
    }

    field_0x803 = i_prm->mAnmNo;
    dNpc_setAnmIDRes(mpMorf, i_prm->mLoopMode, i_prm->mMorf, i_prm->mPlaySpeed, anmNum_toResID(field_0x803), -1, "Ob");
    field_0x7b4 = field_0x7e5 = field_0x7e4 = 0;
    return 1;
}

/* 00000C04-00000C70       .text setAnm_NUM__11daNpc_Ob1_cFii */
void daNpc_Ob1_c::setAnm_NUM(int i_no, int i_setTex) {
    static anm_prm_c a_anm_prm_tbl[8] = {
        {0, 0, 8.0f, 1.0f, 2, 0},
        {1, 0, 8.0f, 1.0f, 2, 0},
        {2, 0, 8.0f, 1.0f, 0, 0},
        {3, 0, 8.0f, 1.0f, 2, 0},
        {4, 0, 8.0f, 1.0f, 2, 0},
        {5, 0, 8.0f, 1.0f, 2, 0},
        {6, 0, 8.0f, 1.0f, 0, 0},
        {7, 0, 8.0f, 1.0f, 2, 0},
    };

    if (i_setTex != 0) {
        setAnm_tex(a_anm_prm_tbl[i_no].mTexNo);
    }
    setAnm_anm(&a_anm_prm_tbl[i_no]);
}

/* 00000C70-00000CF0       .text setAnm__11daNpc_Ob1_cFv */
bool daNpc_Ob1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[7] = {
        {-1, -1, 0.0f, 0.0f, -1, 0},
        {0, 0, 8.0f, 1.0f, 2, 0},
        {-1, -1, 0.0f, 0.0f, -1, 0},
        {3, 0, 8.0f, 1.0f, 2, 0},
        {0, 0, 8.0f, 1.0f, 2, 0},
        {1, 0, 8.0f, 1.0f, 2, 0},
        {-1, -1, 0.0f, 0.0f, -1, 0},
    };

    if (a_anm_prm_tbl[field_0x805].mTexNo >= 0) {
        setAnm_tex(a_anm_prm_tbl[field_0x805].mTexNo);
    }

    if (a_anm_prm_tbl[field_0x805].mAnmNo >= 0) {
        setAnm_anm(&a_anm_prm_tbl[field_0x805]);
    }

    return true;
}

/* 00000CF0-00000CFC       .text chg_anmTag__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::chg_anmTag() {
    switch (field_0x801) {
        case 0xFF:
            break;
    }
}

/* 00000CFC-00000D08       .text control_anmTag__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::control_anmTag() {
    switch (field_0x801) {
        case 0xFF:
            break;
    }
}

/* 00000D08-00000E00       .text chg_anmAtr__11daNpc_Ob1_cFUc */
void daNpc_Ob1_c::chg_anmAtr(unsigned char i_atr) {
    if (field_0x7cc != 0xFFFFFFFF) {
    switch (field_0x7cc) {
        case 0xafc:
        case 0xafd:
        case 0xaff:
        case 0xb02:
        case 0xb05:
        case 0xb06:
        case 0xb09:
        case 0xb0c:
        case 0xb0d:
        case 0xb5d:
        case 0xb61:
        case 0xb64:
        case 0xb65:
            field_0x807 = 1;
            field_0x7fa = 0;
            m_jnt.setTrn();
            break;
    }
    }

    if (i_atr >= 7 || i_atr == field_0x800) {
        return;
    }
    field_0x800 = i_atr;
    setAnm_ATR(1);
}

/* 00000E00-00000E8C       .text control_anmAtr__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::control_anmAtr() {
    switch (field_0x800) {
    case 3:
        if (field_0x7e4) {
            setAnm_NUM(4, 1);
            field_0x800 = 7;
        }
        break;
    case 6:
        if (field_0x7e4) {
            setAnm_NUM(7, 1);
            field_0x800 = 7;
        }
        break;
    case 2:
        break;
    }
}

/* 00000E8C-00000EF4       .text setAnm_ATR__11daNpc_Ob1_cFi */
void daNpc_Ob1_c::setAnm_ATR(int i_setTex) {
    static anm_prm_c a_anm_prm_tbl[7] = {
        {0, 0, 8.0f, 1.0f, 2, 0},
        {1, 0, 8.0f, 1.0f, 2, 0},
        {2, 1, 8.0f, 1.0f, 0, 0},
        {2, 0, 8.0f, 1.0f, 0, 0},
        {4, 0, 8.0f, 1.0f, 2, 0},
        {5, 0, 8.0f, 1.0f, 2, 0},
        {6, 0, 8.0f, 1.0f, 0, 0},
    };

    if (i_setTex != 0) {
        setAnm_tex(a_anm_prm_tbl[field_0x800].mTexNo);
    }
    setAnm_anm(&a_anm_prm_tbl[field_0x800]);
}

/* 00000EF4-00000FFC       .text anmAtr__11daNpc_Ob1_cFUs */
void daNpc_Ob1_c::anmAtr(unsigned short i_param_1) {
    dComIfG_MesgCamInfo_c* cam_info = dComIfGp_getMesgCameraInfo();
    if (field_0x6bc[0] == 2 && (fopAc_ac_c*)this != cam_info->mActor[cam_info->mBasicID - 1]) {
        control_anmTag();
        control_anmAtr();
    } else {
        switch (i_param_1) {
        case 6: {
            if (field_0x80b == 0) {
                field_0x800 = 0xFF;
                chg_anmAtr(dComIfGp_getMesgAnimeAttrInfo());
                field_0x80b += 1;
            }
            u8 uVar1 = dComIfGp_getMesgAnimeTagInfo();
            dComIfGp_clearMesgAnimeTagInfo();
            if (uVar1 != 0xFF && field_0x801 != uVar1) {
                field_0x801 = uVar1;
                chg_anmTag();
            }
            break;
        }
        case 0xE:
            field_0x80b = 0;
            break;
        }
        control_anmTag();
        control_anmAtr();
    }
}

/* 00000FFC-00001088       .text eventOrder__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::eventOrder() {
    if (field_0x804 == 1 || field_0x804 == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x804 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (field_0x804 >= 3) {
        field_0x7d2 = field_0x804 - 3;
        fopAcM_orderOtherEventId(this, mEventIdx[field_0x7d2]);
    }
}

/* 00001088-00001120       .text checkOrder__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::checkOrder() {
    if (eventInfo.checkCommandDemoAccrpt()) {
        if (dComIfGp_evmng_startCheck(mEventIdx[field_0x7d2])) {
            field_0x804 = 0;
        }
    } else if (eventInfo.checkCommandTalk()) {
        if (field_0x804 == 1 || field_0x804 == 2) {
            field_0x804 = 0;
            field_0x7f9 = 1;
        }
    }
}

/* 00001120-000011C0       .text chk_talk__11daNpc_Ob1_cFv */
bool daNpc_Ob1_c::chk_talk() {
    bool ret = false;
    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x7eb = dComIfGp_event_getPreItemNo();
            ret = true;
        }
    } else {
        field_0x7eb = 0xFF;
        ret = true;
    }
    return ret;
}

/* 000011C0-00001200       .text chk_partsNotMove__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::chk_partsNotMove() {
    BOOL ret = FALSE;
    if (field_0x7c4 == m_jnt.getHead_y() && field_0x7c6 == m_jnt.getBackbone_y() && field_0x7c8 == current.angle.y) {
        ret = TRUE;
    }
    return ret;
}

/* 00001200-000013A0       .text lookBack__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::lookBack() {
    cXyz dst_pos;
    cXyz src_pos;
    cXyz* dst_pos_p;
    s16 desired_y;
    bool head_only;

    field_0x7c4 = m_jnt.getHead_y();
    field_0x7c6 = m_jnt.getBackbone_y();
    field_0x7c8 = current.angle.y;
    src_pos = current.pos;
    src_pos.y = eyePos.y;
    dst_pos.set(0.0f, 0.0f, 0.0f);
    dst_pos_p = NULL;
    desired_y = current.angle.y;
    head_only = field_0x7fa;

    switch (field_0x807) {
    case 1:
        dst_pos = dNpc_playerEyePos(-20.0f);
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 2:
        dst_pos = mAttPos;
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 3:
        desired_y = field_0x7e0;
        break;
    }

    cLib_addCalcAngleS2(&field_0x7de, l_HIO.field_0x1e, 4, 0x800);
    if (!m_jnt.trnChk()) {
        field_0x7de = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dst_pos_p, src_pos, desired_y, field_0x7de, head_only);
}

/* 000013A0-000014CC       .text next_msgStatus__11daNpc_Ob1_cFPUl */
u16 daNpc_Ob1_c::next_msgStatus(u32* pMsgNo) {
    u16 uVar2 = 0xf;
    switch (*pMsgNo) {
        case 0xa8d:
            *pMsgNo = 0xa8e;
            break;
        case 0xa90:
            *pMsgNo = 0xa91;
            break;
        case 0xa93:
            *pMsgNo = 0xa94;
            break;
        case 0xa97:
            *pMsgNo = 0xa98;
            break;
        case 0xa92:
        case 0xa96:
        case 0xa9b:
        case 0xa9d:
        case 0xaa0:
        case 0xaa2:
        case 0xaa3:
        case 0xaa4:
        case 0xaa5:
        case 0xaa6:
            if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_0E20)) {
                goto end;
            }
            if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0308)) {
                goto end;
            }
            *pMsgNo = 0xaa7;
            break;
        case 0xaa8:
            if (dKy_daynight_check() == dKy_TIME_DAY_e) {
                *pMsgNo = 0xaa9;
            } else {
                *pMsgNo = 0xaaa;
            }
            break;
        case 0xaad:
        case 0xaae:
            if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0304)) {
                *pMsgNo = 0xa8f;
            } else {
                *pMsgNo = 0xa8d;
            }
            break;
        default:
        end:
            uVar2 = 0x10;
            break;
    }
    return uVar2;
}

/* 000014CC-000017F0       .text getMsg_OB1_0__11daNpc_Ob1_cFv */
u32 daNpc_Ob1_c::getMsg_OB1_0() {
    u32 msg = 0;

    if (field_0x7e8 != 0 || field_0x7e7 != 0) {
        if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0302)) {
            if (field_0x7e9 != 0) {
                if (field_0x7e7 == 2) {
                    if (field_0x7ea != 0) {
                        msg = 0xaa2;
                        field_0x7ea = 0;
                    } else {
                        msg = 0xaa1;
                        field_0x7ea = 1;
                    }
                } else {
                    switch (field_0x7e9) {
                    case 1:
                        if (field_0x7ea == 1) {
                            msg = 0xa9d;
                            field_0x7ea = 0;
                        } else {
                            msg = 0xa9c;
                            field_0x7ea = 1;
                        }
                        break;
                    case 2:
                        if (field_0x7ea == 2) {
                            msg = 0xa9f;
                            field_0x7ea = 1;
                        } else if (field_0x7ea == 1) {
                            msg = 0xaa0;
                            field_0x7ea = 0;
                        } else {
                            msg = 0xa9e;
                            field_0x7ea = 2;
                        }
                        break;
                    default:
                        break;
                    }
                }
            } else {
                if (!(field_0x7e7 <= field_0x7e8)) {
                    msg = 0xaa3;
                } else {
                    switch (field_0x7e8) {
                    case 1:
                        msg = 0xaa4;
                        break;
                    case 2:
                        msg = 0xaa5;
                        break;
                    default:
                        msg = 0xaa6;
                        break;
                    }
                }
            }
        } else {
            switch (field_0x7e9) {
            case 1:
                if (field_0x7ea == 1) {
                    msg = 0xa92;
                    field_0x7ea = 0;
                } else {
                    msg = 0xa90;
                    field_0x7ea = 1;
                }
                break;
            case 2:
                if (field_0x7ea == 2) {
                    msg = 0xa95;
                    field_0x7ea = 1;
                } else if (field_0x7ea == 1) {
                    msg = 0xa96;
                    field_0x7ea = 0;
                } else {
                    msg = 0xa93;
                    field_0x7ea = 2;
                }
                break;
            case 3:
                if (field_0x7ea == 3) {
                    msg = 0xa99;
                    field_0x7ea = 2;
                } else if (field_0x7ea == 2) {
                    msg = 0xa9a;
                    field_0x7ea = 1;
                } else if (field_0x7ea == 1) {
                    msg = 0xa9b;
                    field_0x7ea = 0;
                } else {
                    msg = 0xa97;
                    field_0x7ea = 3;
                }
                break;
            default:
                break;
            }
            field_0x7ec = 1;
        }
        return msg;
    }

    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_2A80)) {
        if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_2C40)) {
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_2C40);
            return 0xaad;
        }
    } else {
        if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_2C80)) {
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_2C80);
            return 0xaae;
        }
    }
    return dComIfGs_isEventBit(dSv_event_flag_c::UNK_0304) ? 0xa8f : 0xa8d;
}

/* 000017F0-0000185C       .text getMsg_OB1_1__11daNpc_Ob1_cFv */
u32 daNpc_Ob1_c::getMsg_OB1_1() {
    if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_2C20)) {
        return 0xAA8;
    }
    return dComIfGs_getEventReg(dSv_event_flag_c::UNK_B6FF) != 0 ? 0xAAB : 0xAAC;
}

/* 0000185C-0000187C       .text getMsg_OB1_2__11daNpc_Ob1_cFv */
u32 daNpc_Ob1_c::getMsg_OB1_2() {
    return getMsg_OB1_1();
}

/* 0000187C-000018EC       .text getMsg__11daNpc_Ob1_cFv */
u32 daNpc_Ob1_c::getMsg() {
    u32 msgNo = 0;
    switch (field_0x809) {
    case 0:
        msgNo = getMsg_OB1_0();
        break;
    case 1:
        msgNo = getMsg_OB1_1();
        break;
    case 2:
        msgNo = getMsg_OB1_2();
        break;
    }
    return msgNo;
}

/* 000018EC-0000196C       .text chkAttention__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::chkAttention() {
    dAttention_c& attention = dComIfGp_getAttention();

    if (attention.LockonTruth()) {
        return this == attention.LockonTarget(0) ? TRUE : FALSE;
    } else {
        return this == attention.ActionTarget(0) ? TRUE : FALSE;
    }
}

/* 0000196C-000019C4       .text setAttention__11daNpc_Ob1_cFb */
void daNpc_Ob1_c::setAttention(bool i_setEyePos) {
    attention_info.position.set(current.pos.x, current.pos.y + l_HIO.field_0x20, current.pos.z);

    if (field_0x7f4 == 0 && !i_setEyePos) {
        return;
    }

    eyePos.set(mEyePos.x, mEyePos.y, mEyePos.z);
}

/* 000019C4-000019F8       .text searchByID__11daNpc_Ob1_cFUi */
fopAc_ac_c* daNpc_Ob1_c::searchByID(fpc_ProcID i_id) {
    fopAc_ac_c* ret = NULL;
    fopAcM_SearchByID(i_id, &ret);
    return ret;
}

/* 000019F8-00001A04       .text partner_srch__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::partner_srch() {
    switch (field_0x80a) {
        case 1:
            break;
    }
}

/* 00001A04-00001A34       .text bitCount__11daNpc_Ob1_cFUc */
s8 daNpc_Ob1_c::bitCount(unsigned char i_bits) {
    u8 bits = i_bits;
    s8 count = 0;
    for (int i = 0; i < 8; ++i) {
        if (bits & 1)
            count++;
        bits = bits >> 1;
    }
    return count;
}

/* 00001A34-00001B14       .text set_pigCnt__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::set_pigCnt() {
    field_0x7e7 = bitCount(dComIfGs_getEventReg(dSv_event_flag_c::UNK_B6FF));
    l_check_wrk = 0;
    l_check_flg = 0;
    for (int i = 0; i < 20; i++) {
        l_check_inf[i] = NULL;
    }
    fpcEx_Search(searchActor_Kb, this);
    field_0x7e8 = bitCount(l_check_flg);
    field_0x7e9 = bitCount(l_check_flg & ~dComIfGs_getEventReg(dSv_event_flag_c::UNK_B6FF));
    field_0x7ea = 0;
}

/* 00001B14-00001BB0       .text ob_setPthPos__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::ob_setPthPos() {
    if (mPathRun.isPath()) {
        current.pos = mPathRun.getPoint(mPathRun.getIdx());
        mPathRun.nextIdxAuto();
        cXyz point2 = mPathRun.getPoint(mPathRun.getIdx());
        current.angle.y = cLib_targetAngleY(&current.pos, &point2);
    }
}

/* 00001BB0-00001C50       .text get_attPos__11daNpc_Ob1_cFv */
cXyz daNpc_Ob1_c::get_attPos() {
    cXyz pos;
    dPath* path = mPathRun.nextPath(current.roomNo);
    if (path != NULL) {
        u8 idx = mPathRun.getIdx();
        if (idx == 0) {
            idx = mPathRun.maxPoint();
        }
        pos.set(path->m_points[(u8)(idx - 1)].m_position.x, path->m_points[(u8)(idx - 1)].m_position.y, path->m_points[(u8)(idx - 1)].m_position.z);
    }
    return pos;
}

/* 00001C50-00001DC0       .text ob_movPass__11daNpc_Ob1_cFv */
u32 daNpc_Ob1_c::ob_movPass() {
    u32 uVar3 = 0;
    if (mPathRun.isPath() && dPath_ChkClose(mPathRun.getPath())) {
        if (mPathRun.chkPointPass(current.pos, mPathRun.getDir())) {
            mPathRun.nextIdxAuto();
            uVar3 = 1;
        }
        return uVar3;
    }
    f32 fVar5 = (field_0x7a8 - current.pos).absXZ();
    if (fVar5 <= field_0x7c0) {
        uVar3 = 1;
        if (mPathRun.isPath() && mPathRun.nextIdxAuto() == 0) {
            uVar3 = 2;
        }
    }
    return uVar3;
}

/* 00001DC0-00001E50       .text ob_clcMovSpd__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::ob_clcMovSpd() {
    (field_0x7a8 - current.pos).abs2XZ();
    cLib_chaseAngleS(&current.angle.y, cLib_targetAngleY(&current.pos, &field_0x7a8), l_HIO.m028);
    cLib_chaseF(&speedF, field_0x7b8, field_0x7bc);
}

/* 00001E50-00001F28       .text ob_nMove__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::ob_nMove() {
    switch (field_0x7fc) {
    case 1:
        ob_clcMovSpd();

        f32 speed;
        if ((int)field_0x7fc == 1) {
            speed = speedF * l_HIO.m02C;
        }

        if (speed < 0.5f) {
            speed = 0.5f;
        }

        mpMorf->setPlaySpeed(speed);

        switch (ob_movPass()) {
        case 1:
            field_0x7f2 = 1;
            break;
        case 2:
            field_0x7f2 = 1;
            field_0x7fc = 0;
            break;
        }
        break;
    }

    if (field_0x7f2) {
        field_0x7f0 = 1;
    }
}

/* 00001F28-00001F78       .text charDecide__11daNpc_Ob1_cFi */
BOOL daNpc_Ob1_c::charDecide(int i_param_1) {
    field_0x808 = 0;
    field_0x809 = -1;

    switch (i_param_1) {
        case 1:
            field_0x809 = 1;
            break;
        case 2:
            field_0x809 = 2;
            break;
        default:
            field_0x809 = 0;
            break;
    }

    return TRUE;
}

/* 00001F78-00001FD0       .text event_actionInit__11daNpc_Ob1_cFi */
void daNpc_Ob1_c::event_actionInit(int i_staffIdx) {
    int* pActionNo = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "ActNo");
    if (pActionNo != NULL) {
        field_0x7ff = *pActionNo;
    }
}

/* 00001FD0-00001FE0       .text event_action__11daNpc_Ob1_cFv */
bool daNpc_Ob1_c::event_action() {
    switch (field_0x7ff) {
    case 0:
        break;
    }
    return true;
}

/* 00001FE0-000020CC       .text privateCut__11daNpc_Ob1_cFi */
void daNpc_Ob1_c::privateCut(int i_staffIdx) {

    bool cVar3;
    static char* a_cut_tbl[1] = {"ACTION"};
    if (i_staffIdx != -1) {
        field_0x7fe = dComIfGp_evmng_getMyActIdx(i_staffIdx, a_cut_tbl, 1, 1, 0);
        if (field_0x7fe == -1) {
            dComIfGp_evmng_cutEnd(i_staffIdx);
        } else {
            if (dComIfGp_evmng_getIsAddvance(i_staffIdx)) {
                switch (field_0x7fe) {
                case 0:
                    event_actionInit(i_staffIdx);
                    break;
                }
            }
            switch (field_0x7fe) {
            case 0:
                cVar3 = event_action();
                break;
            default:
                cVar3 = 1;
                break;
            }

            if (cVar3 != 0) {
                dComIfGp_evmng_cutEnd(i_staffIdx);
            }
        }
    }
}

/* 000020CC-000020EC       .text endEvent__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x800 = 0xff;
}

/* 000020EC-00002124       .text isEventEntry__11daNpc_Ob1_cFv */
int daNpc_Ob1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mEventCut.getActorName(), NULL, 0);
}

/* 00002124-000021B8       .text event_proc__11daNpc_Ob1_cFi */
void daNpc_Ob1_c::event_proc(int i_staffIdx) {
    if (dComIfGp_evmng_endCheck(mEventIdx[field_0x7d2])) {
        field_0x804 = 1;
        endEvent();
    } else {
        if (!mEventCut.cutProc()) {
            privateCut(i_staffIdx);
        }
        lookBack();
    }
}

/* 000021B8-00002264       .text set_action__11daNpc_Ob1_cFM11daNpc_Ob1_cFPCvPvPv_iPv */
BOOL daNpc_Ob1_c::set_action(ActionFunc i_action, void* i_param) {
    if (field_0x6f4 != i_action) {
        if (field_0x6f4) {
            field_0x80a = 0;
            (this->*field_0x6f4)(i_param);
        }

        field_0x6f4 = i_action;
        field_0x80a = 0;
        (this->*field_0x6f4)(i_param);
    }

    return TRUE;
}

/* 00002264-00002288       .text clrSpd__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::clrSpd() {
    field_0x7b8 = 0.0f;
    field_0x7bc = 0.0f;
    speedF = 0.0f;
    gravity = -4.5f;
}

/* 00002288-00002488       .text setStt__11daNpc_Ob1_cFSc */
void daNpc_Ob1_c::setStt(signed char i_stt) {
    searchByID(field_0x774);
    s8 old_stt = field_0x805;
    field_0x7d6 = 0;
    field_0x805 = i_stt;

    switch (field_0x805) {
    case 0:
        break;
    case 4:
        field_0x7da = cLib_getRndValue<int>(90, 180);
        // fallthrough
    case 1:
    case 5:
        if (old_stt != 2) {
            switch (field_0x805) {
            case 1:
                field_0x807 = 3;
                field_0x7e0 = field_0x786;
                break;
            case 4: {
                field_0x807 = 2;
                mAttPos = get_attPos();
                break;
            }
            case 5:
                field_0x807 = 1;
                break;
            }
            m_jnt.setTrn();
            field_0x7fa = 0;
        }
        field_0x804 = 0;
        field_0x7fc = 0;
        clrSpd();
        break;
    case 2:
        field_0x807 = 1;
        m_jnt.setTrn();
        field_0x7fa = 0;
        field_0x804 = 0;
        field_0x7fc = 0;
        clrSpd();
        field_0x800 = 0xFF;
        if (old_stt != 5) {
            field_0x806 = old_stt;
        }
        set_pigCnt();
        break;
    case 3:
        field_0x7a8 = mPathRun.getPoint(mPathRun.getIdx());
        field_0x807 = 0;
        field_0x7fa = 1;
        field_0x804 = 0;
        field_0x7fc = 1;
        field_0x7f2 = 0;
        gravity = -4.5f;
        field_0x7b8 = l_HIO.m030;
        field_0x7bc = l_HIO.m034;
        field_0x7c0 = l_HIO.m038;
        break;
    case 6:
        field_0x800 = 0xFF;
        field_0x806 = old_stt;
        return;
    }

    setAnm();
}

/* 00002488-00002564       .text wait_1__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::wait_1() {
    if (field_0x7f9) {
        if (chk_talk()) {
            setStt(2);
        }
        return TRUE;
    }

    if (field_0x6bc[0] == 1) {
        field_0x6bc[0] = 2;
        setStt(6);
        field_0x807 = 3;
        field_0x7e0 = field_0x786;
        return TRUE;
    }

    field_0x804 = 2;
    if (field_0x7f8) {
        field_0x7d6 = 0x3c;
    }

    if (cLib_calcTimer(&field_0x7d6)) {
        field_0x807 = 1;
    } else {
        field_0x807 = 3;
        field_0x7e0 = field_0x786;
        m_jnt.setTrn();
    }

    return TRUE;
}

/* 00002564-0000264C       .text wait_2__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::wait_2() {
    if (field_0x7f9) {
        if (chk_talk()) {
            setStt(2);
        }
        return TRUE;
    }

    field_0x804 = 2;
    if (field_0x7f8) {
        field_0x7d6 = 0x3c;
    }

    if (cLib_calcTimer(&field_0x7d6)) {
        field_0x807 = 1;
    } else {
        if (!cLib_calcTimer(&field_0x7da)) {
            setStt(3);
            return TRUE;
        }
        field_0x807 = 2;
        mAttPos = get_attPos();
        m_jnt.setTrn();
    }

    return TRUE;
}

/* 0000264C-000026F0       .text wait_3__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::wait_3() {
    if (field_0x7f9) {
        if (chk_talk()) {
            setStt(2);
        }
        return TRUE;
    }

    if (field_0x7e4) {
        field_0x7e5++;
        if (field_0x7e5 > 3) {
            setStt(field_0x806);
            return TRUE;
        }
    }

    field_0x804 = 2;
    field_0x807 = 1;
    m_jnt.setTrn();
    return TRUE;
}

#include "SSystem/SComponent/c_lib.h"

/* 000026F0-00002808       .text walk_1__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::walk_1() {
    if (field_0x7f9) {
        setAnm_NUM(0, 1);
        speedF = 0.0f;
        if (chk_talk()) {
            setStt(2);
        }
        return TRUE;
    }

    field_0x804 = 2;
    if (field_0x7f8) {
        field_0x7d6 = 0x14;
    }

    if (cLib_calcTimer(&field_0x7d6)) {
        field_0x807 = 1;
    } else {
        field_0x807 = 0;
    }

    if (field_0x7f2) {
        if (!mPathRun.getIdx()) {
            mPathRun.nextIdxAuto();
        }
        setStt(4);
        return TRUE;
    }

    field_0x7f2 = 0;
    field_0x7a8 = mPathRun.getPoint(mPathRun.getIdx());
    return TRUE;
}

/* 00002808-00002A58       .text talk_1__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::talk_1() {
    u8 partsNotMove = chk_partsNotMove();

    if (field_0x804 == 1 || field_0x804 >= 3) {
        return TRUE;
    }

    if (mpCurrMsg != NULL) {
        field_0x7e2 = mpCurrMsg->mStatus;
    } else {
        field_0x7e2 = 0;
    }

    if (field_0x7ed[0] && mCurrMsgBsPcId == fpcM_ERROR_PROCESS_ID_e) {
        mCurrMsgNo = getMsg();
        mCurrMsgBsPcId = fopMsgM_messageSet(mCurrMsgNo, dComIfGp_getMesgCameraInfo()->mActor[0]);
        mpCurrMsg = NULL;
    } else {
        field_0x7cc = -1;
        talk(1);
    }

    if (mpCurrMsg != NULL) {
        switch (mpCurrMsg->mStatus) {
        case fopMsgStts_MSG_TYPING_e:
        case fopMsgStts_BOX_OPENING_e:
            break;
        case fopMsgStts_MSG_DESTROYED_e:
            if (field_0x7ea) {
                field_0x804 = 3;
                setAnm_NUM(0, 1);
                endEvent();
                return partsNotMove;
            }

            l_check_flg |= dComIfGs_getEventReg(dSv_event_flag_c::UNK_B6FF);
            dComIfGs_setEventReg(dSv_event_flag_c::UNK_B6FF, l_check_flg);

            switch (mCurrMsgNo) {
            case 0xA8E:
                dComIfGs_onEventBit(dSv_event_flag_c::UNK_0304);
                break;
            case 0xAA7:
                dComIfGs_onEventBit(dSv_event_flag_c::UNK_0308);
                break;
            case 0xAA9:
            case 0xAAA:
            case 0xAAB:
            case 0xAAC:
                dComIfGs_onEventBit(dSv_event_flag_c::UNK_2C20);
                break;
            }

            if (field_0x7ec) {
                dComIfGs_onEventBit(dSv_event_flag_c::UNK_0302);
            }

            field_0x7eb = 0xFF;
            field_0x7f9 = 0;

            switch (field_0x809) {
            case 0:
                setStt(5);
                break;
            default:
                setStt(field_0x806);
                field_0x7d6 = 0x3C;
                break;
            }

            endEvent();
            break;
        }
    }

    return partsNotMove;
}

/* 00002A58-00002B60       .text manzai__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::manzai() {
    dComIfG_MesgCamInfo_c* temp_r30 = dComIfGp_getMesgCameraInfo();
    fopNpc_npc_c* temp_r4;
    switch (field_0x6bc[0]) {
    case 2:
        temp_r4 = (fopNpc_npc_c*)searchByID(field_0x6b4);
        if (this != temp_r30->mActor[temp_r30->mBasicID - 1]) {
            if (field_0x800 != 0xFF) {
                field_0x807 = 3;
                field_0x7e0 = field_0x786;
                m_jnt.mbTrn = true;
                field_0x805 = field_0x806;
                setAnm();
                field_0x805 = 6;
                field_0x800 = 0xFF;
            }
        } else {
            field_0x7cc = temp_r4->mCurrMsgNo;
            anmAtr(temp_r4->field_0x6b8);
        }
        break;
    case 3:
        fopAcM_OffStatus(this, fopAcStts_UNK4000_e);
        setStt(field_0x806);
        field_0x6bc[0] = 0;
        break;
    }
    return 1;
}

/* 00002B60-00002C54       .text wait_action1__11daNpc_Ob1_cFPv */
BOOL daNpc_Ob1_c::wait_action1(void*) {
    switch (field_0x80a) {
    case 0:
        ob_setPthPos();
        setStt(3);
        field_0x80a++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x7f8 = chkAttention();
        switch (field_0x805) {
        case 4:
            field_0x7f4 = wait_2();
            break;
        case 5:
            field_0x7f4 = wait_3();
            break;
        case 3:
            field_0x7f4 = walk_1();
            break;
        case 2:
            field_0x7f4 = talk_1();
            break;
        }
        lookBack();
        break;
    case 9:
        break;
    }

    return TRUE;
}

/* 00002C54-00002D2C       .text wait_action2__11daNpc_Ob1_cFPv */
BOOL daNpc_Ob1_c::wait_action2(void*) {
    switch (field_0x80a) {
    case 0:
        setStt(1);
        field_0x80a++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x7f8 = chkAttention();
        switch (field_0x805) {
        case 1:
            field_0x7f4 = wait_1();
            break;
        case 2:
            field_0x7f4 = talk_1();
            break;
        case 6:
            field_0x7f4 = manzai();
            break;
        }
        lookBack();
        break;
    case 9:
        break;
    }

    return TRUE;
}

/* 00002D2C-00002E68       .text demo__11daNpc_Ob1_cFv */
u8 daNpc_Ob1_c::demo() {
    if (demoActorID == 0) {
        if (field_0x7fb != 0) {
            field_0x7fb = 0;
        }
    } else {
        field_0x7fb = 1;
        dDemo_actor_c* a_actor = dComIfGp_demo_getActor(demoActorID);
        if (m_hed_tex_pttrn != NULL) {
            field_0x6f0 += 1;
            if (field_0x6f0 >= m_hed_tex_pttrn->getFrameMax()) {
                field_0x6f0 = m_hed_tex_pttrn->getFrameMax();
            }
        }
        J3DAnmTexPattern* pJVar2 = (J3DAnmTexPattern*)a_actor->getP_BtpData("Ob");
        if (pJVar2 != NULL) {
            m_hed_tex_pttrn = pJVar2;
            if (mBtpAnm.init(field_0x6d4->getModelData(), m_hed_tex_pttrn, 1, 2, 1.0f, 0, -1, true, 0)) {
                field_0x802 = 2;
                field_0x6f0 = 0;
            }
        }
        dDemo_setDemoData(this, 0x6A, mpMorf, "Ob");
    }
    return field_0x7fb;
}

/* 00002E68-00002F18       .text shadowDraw__11daNpc_Ob1_cFv */
void daNpc_Ob1_c::shadowDraw() {
    cXyz shadow_pos(current.pos.x, current.pos.y + 150.0f, current.pos.z);

    field_0x6d0 = dComIfGd_setShadow(
        field_0x6d0, 1, mpMorf->getModel(), &shadow_pos, 800.0f, 40.0f,
        current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr,
        0, 1.0f, dDlst_shadowControl_c::getSimpleTex()
    );
    if (field_0x6d0 != 0) {
        dComIfGd_addRealShadow(field_0x6d0, field_0x6d4);
    }
}

#include "d/d_snap.h"

/* 00002F18-0000300C       .text _draw__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::_draw() {
    GXColor unused_color0 = {0xFF, 0x00, 0x00, 0x80};
    GXColor unused_color1 = {0x00, 0x00, 0xFF, 0x80};
    GXColor unused_color2 = {0x00, 0xFF, 0x00, 0x80};
    J3DModel* pModel = field_0x6d4;
    J3DModelData* pModelData = pModel->getModelData();
    J3DModel* pMorfModel = mpMorf->getModel();

    if (field_0x7ee != 0 || field_0x7f1[0] != 0) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(pMorfModel, &tevStr);
    mpMorf->entryDL();
    mBtpAnm.entry(pModelData, field_0x6f0);
    mDoExt_modelEntryDL(pModel);
    mBtpAnm.remove(pModelData);
    g_env_light.setLightTevColorType(pModel, &tevStr);
    shadowDraw();
    dSnap_RegistFig(DSNAP_TYPE_UNK4F, this, 1.0f, 1.0f, 1.0f);

    return TRUE;
}

/* 0000300C-000031F0       .text _execute__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::_execute() {
    if (!field_0x7f3) {
        field_0x778 = current.pos.x;
        field_0x77c = current.pos.y;
        field_0x780 = current.pos.z;
        field_0x784 = current.angle.x;
        field_0x786 = current.angle.y;
        field_0x788 = current.angle.z;
        field_0x7f3 = 1;
    }

    m_jnt.setParam(l_HIO.field_0x14, l_HIO.field_0x16, l_HIO.field_0x18, l_HIO.field_0x1a,
                   l_HIO.field_0xc, l_HIO.field_0xe, l_HIO.field_0x10, l_HIO.field_0x12,
                   l_HIO.field_0x1c);

    if (field_0x7ee && !demoActorID) {
        return true;
    }

    field_0x7f0 = 0;
    field_0x7ee = 0;
    partner_srch();
    checkOrder();

    if (!demo()) {
        int mode = -1;
        if (dComIfGp_event_runCheck() && !eventInfo.checkCommandTalk()) {
            mode = isEventEntry();
        }

        if (mode >= 0) {
            event_proc(mode);
        } else {
            (this->*field_0x6f4)(NULL);
        }

        if (!field_0x7f0) {
            ob_nMove();
            fopAcM_posMoveF(this, mStts.GetCCMoveP());
        }

        if (!field_0x7ef) {
            field_0x78a = current.angle.x;
            field_0x78c = current.angle.y;
            field_0x78e = current.angle.z;
            shape_angle.x = field_0x78a;
            shape_angle.y = field_0x78c;
            shape_angle.z = field_0x78e;
        }
    }

    eventOrder();
    setMtx(false);

    if (!field_0x7fb) {
        setCollision(80.0f, 160.0f);
    }

    return true;
}

/* 000031F0-0000324C       .text _delete__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::_delete() {
    dComIfG_resDelete(&mPhs, "Ob");

    if (heap != NULL && mpMorf != NULL) {
        mpMorf->stopZelAnime();
    }

    return TRUE;
}

/* 0000324C-0000326C       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Ob1_c*)i_this)->CreateHeap();
}

/* 0000326C-0000338C       .text _create__11daNpc_Ob1_cFv */
cPhs_State daNpc_Ob1_c::_create() {
    fopAcM_ct_Retail(this, daNpc_Ob1_c);

    cPhs_State resLoad = dComIfG_resLoad(&mPhs, "Ob");
    if (resLoad != cPhs_COMPLEATE_e) {
        return resLoad;
    }

    if (!(charDecide(fopAcM_GetParam(this) & 0xFF) & 0xFF)) {
        return cPhs_ERROR_e;
    }

    enum { HEAP_SIZE = 0x272E0 };
    static int a_size_tbl[] = { HEAP_SIZE };

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_size_tbl[field_0x808])) {
        return cPhs_ERROR_e;
    }

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -60.0f, -20.0f, -60.0f, 60.0f, 170.0f, 60.0f);

    return (createInit() & 0xFF) ? resLoad : cPhs_ERROR_e;
}


daNpc_Ob1_c::daNpc_Ob1_c() {}

/* 000037BC-000039D0       .text create_Anm__11daNpc_Ob1_cFv */
J3DModelData* daNpc_Ob1_c::create_Anm() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Ob", dRes_ID_OB_BDL_OB_e);

    JUT_ASSERT(2423, a_mdl_dat != 0);

    mpMorf = new mDoExt_McaMorf(a_mdl_dat, NULL, NULL,
                                 (J3DAnmTransform*)dComIfG_getObjectIDRes("Ob", dRes_ID_OB_BCK_WAIT_e), 2, 1.0, 0,
                                 -1, 1, NULL, 0x80000, 0x11020022);

    if (mpMorf == NULL) {
        a_mdl_dat = NULL;
        return a_mdl_dat;
    } else if (mpMorf->getModel() == NULL) {
        mpMorf = NULL;
        a_mdl_dat = NULL;
        return a_mdl_dat;
    } else {
        m_hed_jnt_num = a_mdl_dat->getJointName()->getIndex("head");
        JUT_ASSERT(2443, m_hed_jnt_num >= 0);

        m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone");
        JUT_ASSERT(2446, m_bbone_jnt_num >= 0);
    }
    return a_mdl_dat;
}

/* 000039D0-00003A9C       .text create_hed_Mdl__11daNpc_Ob1_cFv */
J3DModelData* daNpc_Ob1_c::create_hed_Mdl() {
    static int a_hed_mdl_resID_tbl[] = {dRes_ID_OB_BDL_OBA_HEAD_e};
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Ob", a_hed_mdl_resID_tbl[field_0x808]);
    JUT_ASSERT(0x9A1, a_mdl_dat != 0);
    field_0x6d4 = mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11020022);
    return a_mdl_dat;
}

/* 00003A9C-00003BE8       .text CreateHeap__11daNpc_Ob1_cFv */
BOOL daNpc_Ob1_c::CreateHeap() {
    J3DModelData* anm = create_Anm();
    if (anm == NULL) {
        return FALSE;
    }

    if (!create_hed_Mdl()) {
        mpMorf = NULL;
        return FALSE;
    }

    u8 local_arr[] = { 0 };
    field_0x802 = local_arr[0];
    if (!iniTexPttrnAnm(false)) {
        mpMorf = NULL;
        return FALSE;
    }

    for (u16 i = 0; i < anm->getJointNum(); i++) {
        if (i == m_hed_jnt_num || i == m_bbone_jnt_num) {
            mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Ob1);
        }
    }
#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    mAcchCir.SetWall(30.0f, 80.0f);
    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir,
                 fopAcM_GetSpeed_p(this), NULL, NULL);
    return TRUE;
}

/* 00003BE8-00003C08       .text daNpc_Ob1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Ob1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Ob1_c*)i_this)->_create();
}

/* 00003C08-00003C28       .text daNpc_Ob1_Delete__FP11daNpc_Ob1_c */
static BOOL daNpc_Ob1_Delete(daNpc_Ob1_c* i_this) {
    return ((daNpc_Ob1_c*)i_this)->_delete();
}

/* 00003C28-00003C48       .text daNpc_Ob1_Execute__FP11daNpc_Ob1_c */
static BOOL daNpc_Ob1_Execute(daNpc_Ob1_c* i_this) {
    return ((daNpc_Ob1_c*)i_this)->_execute();
}

/* 00003C48-00003C68       .text daNpc_Ob1_Draw__FP11daNpc_Ob1_c */
static BOOL daNpc_Ob1_Draw(daNpc_Ob1_c* i_this) {
    return ((daNpc_Ob1_c*)i_this)->_draw();
}

/* 00003C68-00003C70       .text daNpc_Ob1_IsDelete__FP11daNpc_Ob1_c */
static BOOL daNpc_Ob1_IsDelete(daNpc_Ob1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Ob1_Method = {
    (process_method_func)daNpc_Ob1_Create,
    (process_method_func)daNpc_Ob1_Delete,
    (process_method_func)daNpc_Ob1_Execute,
    (process_method_func)daNpc_Ob1_IsDelete,
    (process_method_func)daNpc_Ob1_Draw,
};

actor_process_profile_definition g_profile_NPC_OB1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_OB1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Ob1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_OB1_e,
    /* Actor SubMtd */ &l_daNpc_Ob1_Method,
    /* Status       */ 0x08 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
