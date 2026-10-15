/**
 * d_a_npc_gp1.cpp
 * NPC - Maggie's Father (rich)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_npc_gp1.h"

class daNpc_Gp1_HIO_c : public mDoHIO_entry_c {
public:
    daNpc_Gp1_HIO_c();
    virtual ~daNpc_Gp1_HIO_c() {}

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
    /* 0x01E */ s16 field_0x1e;
    /* 0x020 */ f32 field_0x20;
    /* 0x024 */ u8 field_0x24;
    /* 0x025 */ u8 field_0x25[0x028 - 0x025];
    /* 0x028 */ f32 m028;
    /* 0x02C */ s16 m02C;
    /* 0x02E */ s16 m02E;
    /* 0x030 */ f32 m030;
    /* 0x034 */ f32 m034;
    /* 0x038 */ f32 m038;
    /* 0x03C */ f32 m03C;
};

static daNpc_Gp1_HIO_c l_HIO;
static fopAc_ac_c* l_check_inf[20];
static s32 l_check_wrk;

/* 000000EC-00000150       .text __ct__15daNpc_Gp1_HIO_cFv */
daNpc_Gp1_HIO_c::daNpc_Gp1_HIO_c() {
    static u32 a_prm_tbl[13] = {
        0x20002A00, 0xF254D600, 0x00001C00, 0x0000E400,
        0x04000400, 0x43690000, 0x00000000, 0x43960000,
        0x00060400, 0x3ECCCCCD, 0x40400000, 0x3E4CCCCD,
        0x41A00000,
    };

    memcpy(&mMaxHeadX, a_prm_tbl, 0x34);
    mNo = -1;
    field_0x8 = -1;
}

/* 00000198-00000210       .text searchActor_Bm__FPvPv */
static void* searchActor_Bm(void* i_actor, void*) {
    if (l_check_wrk < 0x14 && fopAcM_IsActor(i_actor) && fopAcM_GetName(i_actor) == fpcNm_NPC_BM4_e) {
        l_check_inf[l_check_wrk] = (fopAc_ac_c*)i_actor;
        l_check_wrk++;
    }
    return NULL;
}

/* 00000210-0000025C       .text nodeCallBack_Gp1__FP7J3DNodei */
static BOOL nodeCallBack_Gp1(J3DNode* i_node, int i_param) {
    if (i_param == 0) {
        daNpc_Gp1_c* actor = (daNpc_Gp1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->nodeGp1Control(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 0000025C-000003AC       .text nodeGp1Control__11daNpc_Gp1_cFP7J3DNodeP8J3DModel */
void daNpc_Gp1_c::nodeGp1Control(J3DNode* i_node, J3DModel* i_model) {
    static cXyz a_eye_pos_off(24.0f, -24.0f, 0.0f);
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

static const char* l_evn_tbl[3] = {
    "Kaere_Kaere", "Gp1_Get_Itm", "Giv_Rupee",
};

/* 000003E8-00000468       .text init_GP1_0__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::init_GP1_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_2D01)) {
        set_action(&daNpc_Gp1_c::wait_action1, NULL);
        return TRUE;
    }

    return FALSE;
}

#include "f_op/f_op_actor_mng.h"
#include "d/d_com_inf_game.h"

/* 00000468-00000638       .text createInit__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::createInit() {
    int weight;
    for (int i = 0; i < 3; i++) {
        mEventIdx[i] = dComIfGp_evmng_getEventIdx(l_evn_tbl[i], 0xFF);
    }

    attention_info.flags = 0xa;
    attention_info.distances[1] = 0xab;
    attention_info.distances[3] = 0xab;
    gravity = -4.5f;

    field_0x79c = current.pos.x;
    field_0x7a0 = current.pos.y;
    field_0x7a4 = current.pos.z;

    weight = 0xff;
    u8 pathIdx = fopAcM_GetParamBit(fopAcM_GetParam(this), 16, 8);
    if (pathIdx != 0xff) {
        mPathRun.setInf(pathIdx, current.roomNo, true);
        if (mPathRun.isPath()) {
            fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
            weight = 0xd9;
        } else {
            return false;
        }
    }

    mDemoEventCut.setActorInfo2("Gp1", this);

    field_0x812 = 8;

    bool ret;
    switch (field_0x818) {
    case 0:
        ret = init_GP1_0();
        break;
    default:
        ret = false;
        break;
    }

    if (ret) {
        field_0x78a = current.angle.x;
        field_0x78c = current.angle.y;
        field_0x78e = current.angle.z;
        shape_angle.x = field_0x78a;
        shape_angle.y = field_0x78c;
        shape_angle.z = field_0x78e;
    } else {
        return false;
    }

    mStts.Init(weight, 0xff, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);
    mpMorf->setMorf(0.0f);
    setMtx(true);
    return 1;
}

/* 00000638-00000758       .text setMtx__11daNpc_Gp1_cFb */
void daNpc_Gp1_c::setMtx(bool i_setEyePos) {
    if (!field_0x80b) {
        plyTexPttrnAnm();
        field_0x7f8 = mpMorf->play(&eyePos, 0, 0);
        if (mpMorf->getFrame() < field_0x7c0) {
            field_0x7f8 = true;
        }
        field_0x7c0 = mpMorf->getFrame();
        ctrl_WAITanm();
        mObjAcch.CrrPos(*dComIfG_Bgsp());
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);

    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::YrotM(field_0x78c);

    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();

    setAttention(i_setEyePos);
}

/* 00000758-0000076C       .text anmNum_toResID__11daNpc_Gp1_cFi */
int daNpc_Gp1_c::anmNum_toResID(int i_no) {
    static const int a_bck_resID_tbl[8] = {
        0x00000005, 0x00000005, 0x00000003, 0x00000004,
        0x00000002, 0x00000001, 0x00000000, 0x00000008,
    };
    return a_bck_resID_tbl[i_no];
}

/* 0000076C-00000780       .text btpNum_toResID__11daNpc_Gp1_cFi */
s32 daNpc_Gp1_c::btpNum_toResID(int i_btp_num) {
    static const s32 a_btp_resID_tbl[] = {
        0x00000007,
    };

    return a_btp_resID_tbl[i_btp_num];
}

#include "d/d_com_inf_game.h"
#include "JSystem/JUtility/JUTAssert.h"

/* 00000780-00000890       .text setBtp__11daNpc_Gp1_cFbi */
bool daNpc_Gp1_c::setBtp(bool i_modify, int i_btpNum) {
    J3DModelData* model_data = mpMorf->getModel()->getModelData();
    int res_id = btpNum_toResID(i_btpNum);
    m_hed_tex_pttrn = (J3DAnmTexPattern*)dComIfG_getObjectIDRes("Gp", (u16)res_id);
    JUT_ASSERT(0x1b3, m_hed_tex_pttrn != 0);

    bool ret = mBtpAnm.init(model_data, m_hed_tex_pttrn, 1, 2, 1.0f, 0, -1, i_modify, 0) == TRUE;
    if (ret) {
        field_0x6f0 = 0;
        field_0x6f2 = 0;
    }
    return ret;
}

/* 00000890-000008B8       .text iniTexPttrnAnm__11daNpc_Gp1_cFb */
bool daNpc_Gp1_c::iniTexPttrnAnm(bool i_modify) {
    return setBtp(i_modify, field_0x811);
}

/* 000008B8-00000964       .text plyTexPttrnAnm__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::plyTexPttrnAnm() {
    if (field_0x811 != 0 || !cLib_calcTimer(&field_0x6f2)) {
        field_0x6f0 += 1;
        if (field_0x6f0 >= m_hed_tex_pttrn->getFrameMax()) {
            if (field_0x811 != 0) {
                field_0x6f0 = m_hed_tex_pttrn->getFrameMax();
            } else {
                field_0x6f2 = cM_rndF(60.0f) + 30.0f;
                field_0x6f0 = 0;
            }
        }
    }
}

/* 00000964-000009A0       .text setAnm_tex__11daNpc_Gp1_cFSc */
void daNpc_Gp1_c::setAnm_tex(signed char i_btpNum) {
    if (field_0x811 != i_btpNum) {
        field_0x811 = i_btpNum;
        iniTexPttrnAnm(true);
    }
}

/* 000009A0-00000A78       .text setAnm_anm__11daNpc_Gp1_cFPQ211daNpc_Gp1_c9anm_prm_c */
BOOL daNpc_Gp1_c::setAnm_anm(daNpc_Gp1_c::anm_prm_c* i_prm) {
    if (field_0x812 == i_prm->anmNum) {
        return TRUE;
    }

    field_0x812 = i_prm->anmNum;
    dNpc_setAnmIDRes(mpMorf, i_prm->loopMode, i_prm->morf, i_prm->speed, anmNum_toResID(field_0x812), -1, "Gp");
    if (field_0x812 == 0) {
        field_0x7f6 = 180.0f + cM_rndF(120.0f);
    }
    field_0x7c0 = 0.0f;
    field_0x7f9 = 0;
    field_0x7f8 = 0;
    return TRUE;
}

/* 00000A78-00000AE4       .text setAnm_NUM__11daNpc_Gp1_cFii */
void daNpc_Gp1_c::setAnm_NUM(int i_idx, int i_tex) {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 8.0f, 1.0f, 2},
        {1, 0, 8.0f, 1.0f, 2},
        {2, 0, 8.0f, 1.0f, 2},
        {3, 0, 8.0f, 1.0f, 2},
        {4, 0, 8.0f, 1.0f, 0},
        {5, 0, 8.0f, 1.0f, 2},
        {6, 0, 8.0f, 1.0f, 0},
        {0, 0, 0.0f, 0.0f, 0},
        {0, 0, 0.0f, 0.0f, 0},
    };

    if (i_tex) {
        setAnm_tex(a_anm_prm_tbl[i_idx].btpNum);
    }
    setAnm_anm(&a_anm_prm_tbl[i_idx]);
}

/* 00000AE4-00000B64       .text setAnm__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[] = {
        {-1, -1, 0.0f, 0.0f, -1},
        {0, 0, 8.0f, 1.0f, 2},
        {-1, -1, 0.0f, 0.0f, -1},
        {0, 0, 8.0f, 1.0f, 2},
        {7, 0, 8.0f, 1.0f, 2},
        {4, 0, 8.0f, 1.0f, 0},
    };

    if (a_anm_prm_tbl[field_0x814].btpNum >= 0) {
        setAnm_tex(a_anm_prm_tbl[field_0x814].btpNum);
    }
    if (a_anm_prm_tbl[field_0x814].anmNum >= 0) {
        setAnm_anm(&a_anm_prm_tbl[field_0x814]);
    }
    return true;
}

/* 00000B64-00000B68       .text chg_anmTag__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::chg_anmTag() {
}

/* 00000B68-00000B6C       .text control_anmTag__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::control_anmTag() {
}

/* 00000B6C-00000BB0       .text chg_anmAtr__11daNpc_Gp1_cFUc */
void daNpc_Gp1_c::chg_anmAtr(unsigned char i_param_1) {
    if (i_param_1 >= 7 || i_param_1 == field_0x80f) {
        return;
    }

    field_0x80f = i_param_1;
    setAnm_ATR(1);
}

/* 00000BB0-00000D00       .text control_anmAtr__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::control_anmAtr() {
    switch (field_0x80f) {
    case 1:
        if (field_0x7f8 != 0) {
            field_0x80f = 0;
            setAnm_NUM(0, 1);
        }
        break;
    case 5:
        if (field_0x7f8 == 0) {
            if (mpMorf->checkFrame(80.0f)) {
                create_rupee();
                mDoAud_seStart(JA_SE_OBJ_LUPY_OUT, NULL, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                field_0x7f4 = 0;
            }
        } else {
            field_0x80f = 0;
            setAnm_NUM(0, 1);
        }
        break;
    case 6:
        if (field_0x7f8 != 0) {
            if (++field_0x7f9 >= 2) {
                field_0x80f = 0;
                setAnm_NUM(0, 1);
            }
        }
        break;
    case 0:
        break;
    }
}

/* 00000D00-00000D68       .text setAnm_ATR__11daNpc_Gp1_cFi */
void daNpc_Gp1_c::setAnm_ATR(int i_tex) {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 8.0f, 1.0f, 2},
        {5, 0, 8.0f, 1.0f, 2},
        {2, 0, 8.0f, 1.0f, 2},
        {3, 0, 8.0f, 1.0f, 2},
        {5, 0, 8.0f, 1.0f, 2},
        {6, 0, 8.0f, 1.0f, 0},
        {2, 0, 8.0f, 1.0f, 2},
    };

    if (i_tex) {
        setAnm_tex(a_anm_prm_tbl[field_0x80f].btpNum);
    }
    setAnm_anm(&a_anm_prm_tbl[field_0x80f]);
}

/* 00000D68-00000E2C       .text anmAtr__11daNpc_Gp1_cFUs */
void daNpc_Gp1_c::anmAtr(unsigned short i_status) {
    u32 mesgAnimeTagInfo;

    switch (i_status) {
    case 6:
        if (field_0x81a == 0) {
            field_0x80f = 0xFF;
            chg_anmAtr(dComIfGp_getMesgAnimeAttrInfo());
            field_0x81a++;
        }

        mesgAnimeTagInfo = dComIfGp_getMesgAnimeTagInfo();
        dComIfGp_clearMesgAnimeTagInfo();

        if (mesgAnimeTagInfo != 0xFF && field_0x810 != mesgAnimeTagInfo) {
            field_0x810 = mesgAnimeTagInfo;
            chg_anmTag();
        }
        break;
    case 0xE:
        field_0x81a = 0;
        break;
    default:
        break;
    }

    control_anmTag();
    control_anmAtr();
}

/* 00000E2C-00000EC4       .text eventOrder__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::eventOrder() {
    if (field_0x813 == 1 || field_0x813 == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        eventInfo.onCondition(dEvtCnd_CANTALKITEM_e);
        if (field_0x813 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (field_0x813 >= 3) {
        field_0x7e4 = field_0x813 - 3;
        fopAcM_orderOtherEventId(this, mEventIdx[field_0x7e4]);
    }
}

/* 00000EC4-00000F5C       .text checkOrder__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::checkOrder() {
    if (eventInfo.checkCommandDemoAccrpt()) {
        if (dComIfGp_evmng_startCheck(mEventIdx[field_0x7e4])) {
            field_0x813 = 0;
        }
    } else if (eventInfo.checkCommandTalk() && (field_0x813 == 1 || field_0x813 == 2)) {
        field_0x813 = 0;
        field_0x809 = 1;
    }
}

/* 00000F5C-00001008       .text chk_talk__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::chk_talk() {
    bool ret = true;
    field_0x7fb = 0xFF;

    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x7fb = dComIfGp_event_getPreItemNo();
        } else {
            ret = false;
        }

        field_0x7fb = dComIfGp_event_getPreItemNo();
    }

    return ret;
}

// Unreferenced; stripped from the REL, but its DEG2S literal remains in .rodata
bool daNpc_Gp1_c::chk_drct(f32 i_deg) {
    s16 target = cLib_targetAngleY(&current.pos, &dComIfGp_getPlayer(0)->current.pos) - current.angle.y;
    int cmp = abs(target);
    return cmp < cM_deg2s(i_deg);
}

/* 00001008-00001048       .text chk_partsNotMove__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::chk_partsNotMove() {
    bool ret = false;
    if (field_0x7d8 == m_jnt.getHead_y() && field_0x7da == m_jnt.getBackbone_y() && field_0x7dc == current.angle.y) {
        ret = true;
    }
    return ret;
}

/* 00001048-00001168       .text chk_forceTlkArea__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::chk_forceTlkArea() {
    f32 dist = (dComIfGp_getPlayer(0)->current.pos - current.pos).absXZ();
    f32 dy = dComIfGp_getPlayer(0)->current.pos.y - current.pos.y;
    bool ret = false;
    if (-1.0f < dy && dy <= 100.0f && dist < l_HIO.m028) {
        ret = true;
    }
    return ret;
}

/* 00001168-00001308       .text lookBack__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::lookBack() {
    cXyz dst_pos;
    cXyz src_pos;
    cXyz* dst_pos_p;
    s16 desired_y;
    bool head_only;

    field_0x7d8 = m_jnt.getHead_y();
    field_0x7da = m_jnt.getBackbone_y();
    field_0x7dc = current.angle.y;
    src_pos = current.pos;
    src_pos.y = eyePos.y;
    dst_pos.set(0.0f, 0.0f, 0.0f);
    dst_pos_p = NULL;
    desired_y = current.angle.y;
    head_only = field_0x80a;

    switch (field_0x816) {
    case 1:
        dst_pos = dNpc_playerEyePos(-20.0f);
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 2:
        dst_pos.x = field_0x79c;
        dst_pos.y = field_0x7a0;
        dst_pos.z = field_0x7a4;
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 3:
        desired_y = field_0x7f0;
        break;
    }

    cLib_addCalcAngleS2(&field_0x7ee, l_HIO.field_0x1e, 4, 0x800);
    if (!m_jnt.trnChk()) {
        field_0x7ee = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dst_pos_p, src_pos, desired_y, field_0x7ee, head_only);
}

/* 00001308-00001504       .text next_msgStatus__11daNpc_Gp1_cFPUl */
u16 daNpc_Gp1_c::next_msgStatus(u32* pMsgNo) {
    u16 ret = 0xf;
    s16 num = dComIfGs_getBeastNum(dBeastIdx_SKULL_NECKLACE_e);

    switch (*pMsgNo) {
    case 0x1e15:
        *pMsgNo = 0x1e16;
        break;
    case 0x1e16:
        *pMsgNo = 0x1e17;
        break;
    case 0x1e17:
        *pMsgNo = 0x1e18;
        break;
    case 0x1e19:
        *pMsgNo = 0x1e1a;
        break;
    case 0x1e1a:
        *pMsgNo = 0x1e1b;
        break;
    case 0x1e1b:
        *pMsgNo = 0x1e1c;
        break;
    case 0x1e29:
        *pMsgNo = 0x1e2a;
        field_0x7f2 = num;
        field_0x801 = 1;
        break;
    case 0x1e2a:
        if (field_0x7f2 >= 0x10) {
            *pMsgNo = 0x1e2d;
            field_0x7f4 = 3;
        } else if (field_0x7f2 >= 6) {
            *pMsgNo = 0x1e2c;
            field_0x7f4 = 2;
        } else {
            *pMsgNo = 0x1e2b;
            field_0x7f4 = 1;
        }
        break;
    case 0x1e20:
        *pMsgNo = 0x1e21;
        break;
    case 0x1e30:
        *pMsgNo = 0x1e23;
        break;
    case 0x1e22:
    case 0x1e28:
        *pMsgNo = 0x1e24;
        field_0x801 = 1;
        field_0x7f2 = 0x14;
        break;
    case 0x1e24:
        *pMsgNo = 0x1e25;
        break;
    case 0x1e26:
        *pMsgNo = 0x1e27;
        break;
    case 0x1e21:
        if (num >= 0x14) {
            *pMsgNo = 0x1e22;
        } else {
            *pMsgNo = 0x1e30;
        }
        break;
    case 0x1ee6:
        *pMsgNo = 0x1ee7;
        break;
    default:
        ret = 0x10;
        break;
    }

    return ret;
}

/* 00001504-0000166C       .text getMsg_GP1_0__11daNpc_Gp1_cFv */
u32 daNpc_Gp1_c::getMsg_GP1_0() {
    if (field_0x7fb == 0x45) {
        if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_1420)) {
            return 0x1e29;
        }

        if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_1804)) {
            return (s16)dComIfGs_getBeastNum(dBeastIdx_SKULL_NECKLACE_e) >= 0x14 ? 0x1e28 : 0x1e30;
        }

        return 0x1e20;
    }

    if (field_0x7fb != 0xff) {
        return 0x1e2f;
    }

    if (field_0x7fa != 0) {
        field_0x7fa = 0;
        return 0x1e26;
    }

    if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_1501)) {
        return 0x1e15;
    }

    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_1808)) {
        if (dComIfGs_checkReserveItem(dItemNo_MOBLINS_LETTER_e)) {
            return 0x1e1d;
        }

        return (dComIfGs_getEventReg(dSv_event_flag_c::UNK_C5FF) >= 10) ? 0x1e1e : 0x1e1f;
    }

    return dComIfGs_isEventBit(dSv_event_flag_c::UNK_1920) ? 0x1e1b : 0x1e19;
}

/* 0000166C-000016A8       .text getMsg__11daNpc_Gp1_cFv */
u32 daNpc_Gp1_c::getMsg() {
    u32 msgNo = 0;

    switch (field_0x818) {
        case 0:
            msgNo = getMsg_GP1_0();
            break;
    }

    return msgNo;
}

/* 000016A8-00001728       .text chkAttention__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::chkAttention() {
    dAttention_c& attention = dComIfGp_getAttention();
    if (attention.LockonTruth()) {
        return this == attention.LockonTarget(0);
    } else {
        return this == attention.ActionTarget(0);
    }
}

/* 00001728-00001790       .text setAttention__11daNpc_Gp1_cFb */
void daNpc_Gp1_c::setAttention(bool i_setEyePos) {
    attention_info.position.set(current.pos.x, current.pos.y + l_HIO.field_0x20, current.pos.z);

    if (field_0x804 == 0 && !i_setEyePos) {
        return;
    }

    eyePos.set(mEyePos.x, mEyePos.y, mEyePos.z);
    eyePos.y += field_0x7c4;
}

/* 00001790-000017C4       .text searchByID__11daNpc_Gp1_cFUi */
fopAc_ac_c* daNpc_Gp1_c::searchByID(fpc_ProcID i_procID) {
    fopAc_ac_c* actor = NULL;
    fopAcM_SearchByID(i_procID, &actor);
    return actor;
}

/* 000017C4-00001874       .text partner_srch_sub__11daNpc_Gp1_cFPFPvPv_Pv */
bool daNpc_Gp1_c::partner_srch_sub(void* (*i_searchFunc)(void*, void*)) {
    bool ret = false;
    field_0x774 = fpcM_ERROR_PROCESS_ID_e;
    l_check_wrk = 0;
    for (int i = 0; i < 20; i++) {
        l_check_inf[i] = NULL;
    }
    fpcM_Search(i_searchFunc, this);
    if (l_check_wrk != 0) {
        field_0x774 = fopAcM_GetID(l_check_inf[0]);
        ret = true;
    }
    return ret;
}

/* 00001874-00001918       .text partner_srch__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::partner_srch() {
    bool found = false;
    if (field_0x819 == 1) {
        switch (field_0x814) {
        case 3:
            found = partner_srch_sub(searchActor_Bm);
            if (found) {
                fopAc_ac_c* actor = searchByID(field_0x774);
                if (actor != NULL) {
                    current.angle.y = cLib_targetAngleY(&current.pos, &actor->current.pos);
                }
            }
            break;
        default:
            found = true;
            break;
        }
        if (found) {
            field_0x819++;
        }
    }
}

/* 00001918-000019AC       .text ctrl_WAITanm__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::ctrl_WAITanm() {
    switch (field_0x812) {
    case 0:
        if (!cLib_calcTimer(&field_0x7f6)) {
            setAnm_NUM(4, 1);
        }
        break;
    case 4:
        if (field_0x7f8) {
            setAnm_NUM(0, 1);
            mpMorf->setMorf(0.0f);
        }
        break;
    }
}

/* 000019AC-00001B1C       .text gp_movPass__11daNpc_Gp1_cFv */
u32 daNpc_Gp1_c::gp_movPass() {
    u32 ret = 0;
    if (mPathRun.isPath() && dPath_ChkClose(mPathRun.getPath())) {
        if (mPathRun.chkPointPass(current.pos, mPathRun.getDir())) {
            mPathRun.nextIdxAuto();
            ret = 1;
        }
        return ret;
    }
    f32 dist = (field_0x7a8 - current.pos).absXZ();
    if (dist <= field_0x7d4) {
        ret = 1;
        if (mPathRun.isPath() && mPathRun.nextIdxAuto() == 0) {
            ret = 2;
        }
    }
    return ret;
}

/* 00001B1C-00001B80       .text gp_clcMovSpd__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::gp_clcMovSpd() {
    s16 target = cLib_targetAngleY(&current.pos, &field_0x7a8);
    cLib_addCalcAngleS(&current.angle.y, target, l_HIO.m02C, l_HIO.m02E, 0);
    cLib_chaseF(&speedF, field_0x7cc, field_0x7d0);
}

/* 00001B80-00001C40       .text gp_nMove__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::gp_nMove() {
    if (field_0x800) {
        field_0x7fe = 1;
    } else {
        switch(field_0x80c) {
            case 1:
                gp_clcMovSpd();
                f32 temp = speedF * l_HIO.m030;
                if (temp < 0.5f) {
                    temp = 0.5f;
                }
                mpMorf->setPlaySpeed(temp);
                int temp2 = gp_movPass();
                switch(temp2) {
                    case 1:
                        field_0x800 = 1;
                        break;
                    case 2:
                        field_0x800 = 1;
                        field_0x80c = 0;
                        break;
                }
        }
    }
}

#include "SSystem/SComponent/c_counter.h"

/* 00001C40-00001ED8       .text create_rupee__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::create_rupee() {
    cXyz scale(0.2f, 0.2f, 0.2f);
    csXyz ang(0, 0, 0);

    MTXCopy(mpMorf->getModel()->getAnmMtx(m_hnd_L_jnt_num), mDoMtx_stack_c::now);
    cXyz pos;
    pos.x = mDoMtx_stack_c::now[0][3];
    pos.y = mDoMtx_stack_c::now[1][3];
    pos.z = mDoMtx_stack_c::now[2][3];

    int i;
    s32 n = g_Counter.mCounter0;
    for (i = 0; i < field_0x7f4; i++, n++) {
        f32 dirTbl[3] = { -30.0f, 0.0f, 30.0f };
        s16 deg = dirTbl[n % 3] + (cM_rndF(30.0f) - 15.0f);
        deg = deg * 182.04444885253906f;
        ang.y = current.angle.y + deg;

        fopAc_ac_c* item = (fopAc_ac_c*)fopAcM_fastCreateItem(&pos, dItemNo_RED_RUPEE_e, fopAcM_GetRoomNo(this), NULL, NULL,
                                                                cM_rndFX(3.0f) + 10.0f, cM_rndFX(6.0f) + 33.0f, -2.0f);
        if (item == NULL) {
            break;
        }
        item->actor_status |= fopAcStts_UNK4000_e;
        item->scale = scale;
        item->current.angle = ang;
        item->shape_angle = ang;
    }
    return true;
}

/* 00001ED8-00001EF4       .text charDecide__11daNpc_Gp1_cFi */
bool daNpc_Gp1_c::charDecide(int) {
    field_0x817 = 0x0;
    field_0x818 = -1;
    field_0x818 = 0x0;
    return true;
}

/* 00001EF4-00001F40       .text eInit_INI_KAERE_KAERE___11daNpc_Gp1_cFv */
void daNpc_Gp1_c::eInit_INI_KAERE_KAERE_() {
    fopAc_ac_c* actor = searchByID(field_0x774);
    if (actor != NULL) {
        dComIfGp_event_setItemPartner(actor);
    }
}

/* 00001F40-00001F4C       .text eInit_END_KAERE_KAERE___11daNpc_Gp1_cFv */
void daNpc_Gp1_c::eInit_END_KAERE_KAERE_() {
    current.angle.y = field_0x786;
}

/* 00001F4C-00001FE0       .text event_actionInit__11daNpc_Gp1_cFi */
void daNpc_Gp1_c::event_actionInit(int i_staffIdx) {
    int* act_no_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "ActNo");

    if (act_no_p != NULL) {
        field_0x80e = *act_no_p;
        field_0x804 = 0;
        switch (field_0x80e) {
        case 0:
            eInit_INI_KAERE_KAERE_();
            break;
        case 1:
            eInit_END_KAERE_KAERE_();
            break;
        }
    }
}

/* 00001FE0-00001FF4       .text event_action__11daNpc_Gp1_cFv */
bool daNpc_Gp1_c::event_action() {
    switch (field_0x80e) {
    case 0:
    case 1:
        break;
    }

    return true;
}

/* 00001FF4-000020E0       .text privateCut__11daNpc_Gp1_cFi */
void daNpc_Gp1_c::privateCut(int i_staffIdx) {
    static char* a_cut_tbl[] = {"ACTION"};

    if (i_staffIdx != -1) {
        field_0x80d = dComIfGp_evmng_getMyActIdx(i_staffIdx, a_cut_tbl, ARRAY_SIZE(a_cut_tbl), TRUE, 0);
        if (field_0x80d == -1) {
            dComIfGp_evmng_cutEnd(i_staffIdx);
        } else {
            if (dComIfGp_evmng_getIsAddvance(i_staffIdx)) {
                switch (field_0x80d) {
                case 0:
                    event_actionInit(i_staffIdx);
                    break;
                }
            }

            bool temp;
            switch (field_0x80d) {
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

/* 000020E0-00002100       .text endEvent__11daNpc_Gp1_cFv */
void daNpc_Gp1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x80f = 0xff;
}

/* 00002100-00002138       .text isEventEntry__11daNpc_Gp1_cFv */
int daNpc_Gp1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mDemoEventCut.getActorName());
}

/* 00002138-00002268       .text event_proc__11daNpc_Gp1_cFi */
void daNpc_Gp1_c::event_proc(int i_staffIdx) {
    if (dComIfGp_evmng_endCheck(mEventIdx[field_0x7e4])) {
        switch (field_0x7e4) {
        case 0:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_1808);
            fopAcM_OnStatus(this, fopAcStts_NOCULLEXEC_e);
            setStt(1);
            field_0x7ea = 0;
            field_0x7ec = (g_Counter.mCounter0 & 3) + 2;
            break;
        case 1:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_1420);
            field_0x813 = 1;
            field_0x7fa = 1;
            break;
        case 2:
            setStt(1);
            break;
        }
        endEvent();
    } else {
        if (!mDemoEventCut.cutProc()) {
            privateCut(i_staffIdx);
        }
        lookBack();
    }
}

/* 00002268-00002314       .text set_action__11daNpc_Gp1_cFM11daNpc_Gp1_cFPCvPvPv_iPv */
BOOL daNpc_Gp1_c::set_action(int (daNpc_Gp1_c::*i_action)(void*), void* i_param) {
    if (field_0x6f4 != i_action) {
        if (field_0x6f4) {
            field_0x819 = 9;
            (this->*field_0x6f4)(i_param);
        }

        field_0x6f4 = i_action;
        field_0x819 = 0;
        (this->*field_0x6f4)(i_param);
    }

    return TRUE;
}

/* 00002314-0000240C       .text setStt__11daNpc_Gp1_cFSc */
void daNpc_Gp1_c::setStt(signed char stt) {
    s8 old_stt = field_0x814;
    field_0x7e8 = 0;
    field_0x814 = stt;

    switch ((s8)field_0x814) {
    case 1:
        field_0x7ea = 90;
        break;
    case 2:
        field_0x80f = 0xff;
        field_0x816 = 1;
        m_jnt.mbTrn = true;
        field_0x815 = old_stt;
        return;
    case 3:
        break;
    case 4:
        field_0x816 = 0;
        field_0x80c = 1;
        field_0x7ea = (u16)(180.0f + cM_rndF(120.0f));
        field_0x7cc = l_HIO.m034;
        field_0x7d0 = l_HIO.m038;
        field_0x7d4 = l_HIO.m03C;
        break;
    case 5:
        field_0x816 = 0;
        break;
    default:
        break;
    }

    setAnm();
}

/* 0000240C-0000255C       .text wait_1__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::wait_1() {
    field_0x7f6 = 180.0f + cM_rndF(120.0f);

    if (field_0x809 != 0 && field_0x812 != 4) {
        if (chk_talk()) {
            setStt(2);
            field_0x80a = 0;
        }
        return TRUE;
    }

    if (field_0x813 != 1 && field_0x813 < 3) {
        field_0x813 = 2;
    }

    if (chk_forceTlkArea() || field_0x808 != 0) {
        field_0x7e8 = 0x3c;
    }

    if (cLib_calcTimer(&field_0x7e8)) {
        field_0x816 = 1;
        return TRUE;
    }

    if (!cLib_calcTimer(&field_0x7ea)) {
        setStt(4);
        return TRUE;
    }

    field_0x816 = 2;
    f32 y = eyePos.y;
    field_0x79c = -2.0f;
    field_0x7a0 = y;
    field_0x7a4 = 940.0f;
    m_jnt.setTrn();
    return TRUE;
}

/* 0000255C-00002724       .text talk_1__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::talk_1() {
    u8 ret = chk_partsNotMove();
    talk(1);
    if (mpCurrMsg != NULL) {
        switch (mpCurrMsg->mStatus) {
            case fopMsgStts_MSG_DESTROYED_e:
                switch (mCurrMsgNo) {
                    case 0x1e18:
                        dComIfGs_onEventBit(dSv_event_flag_c::UNK_1501);
                        break;
                    case 0x1e1c:
                        dComIfGs_onEventBit(dSv_event_flag_c::UNK_1920);
                        break;
                    case 0x1e23:
                        dComIfGs_onEventBit(dSv_event_flag_c::UNK_1804);
                        break;
                    case 0x1e25:
                        field_0x813 = 4;
                        break;
                    case 0x1e2b:
                    case 0x1e2c:
                    case 0x1e2d:
                        field_0x813 = 5;
                        break;
                }
                field_0x7fb = 0xff;
                field_0x809 = 0;
                setStt(field_0x815);
                field_0x7e8 = 0x3c;
                endEvent();
                break;
            case fopMsgStts_MSG_TYPING_e:
            case fopMsgStts_BOX_OPENING_e:
                if (field_0x801 != 0) {
                    dComIfGp_evmng_CancelPresent();
                    if (field_0x7f2 != 0) {
                        int reg = dComIfGs_getEventReg(dSv_event_flag_c::UNK_C5FF);
                        dComIfGp_setItemBeastNumCount(dBeastIdx_SKULL_NECKLACE_e, -field_0x7f2);
                        dComIfGs_setEventReg(dSv_event_flag_c::UNK_C5FF, reg + field_0x7f2 > 0x7f ? 0x7f : reg + field_0x7f2);
                    }
                    field_0x801 = 0;
                }
                break;
        }
    }
    return ret;
}

/* 00002724-0000288C       .text walk_1__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::walk_1() {
    if (field_0x809 != 0 || chk_forceTlkArea() || !cLib_calcTimer(&field_0x7ea)) {
        field_0x7cc = 0.0f;
    }

    if (field_0x813 != 1 && field_0x813 < 3) {
        field_0x813 = 2;
    }

    if (field_0x800 != 0) {
        field_0x800 = 0;
        if (field_0x80c == 0) {
            mPathRun.clrIdx();
            field_0x80c = 1;
        }
    }

    u8 idx = mPathRun.getIdx();
    field_0x7a8 = mPathRun.getPoint(idx);

    if ((int)field_0x7cc == 0 && (int)speedF == 0) {
        if (field_0x809 != 0 || cLib_calcTimer(&field_0x7ec)) {
            setStt(1);
        } else {
            setStt(5);
            field_0x7ec = (g_Counter.mCounter0 & 3) + 2;
        }

        speedF = 0.0f;
        field_0x7d0 = 0.0f;
        field_0x80c = 0;
    }

    return TRUE;
}

/* 0000288C-00002930       .text hair_1__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::hair_1() {
    if (field_0x7f8 != 0) {
        if (field_0x809 || chk_forceTlkArea()) {
            setStt(1);
            return TRUE;
        } else {
            setStt(4);
            return TRUE;
        }
    }

    if (!field_0x809 && field_0x813 != 1 && field_0x813 < 3) {
        field_0x813 = 2;
    }

    return TRUE;
}

/* 00002930-00002988       .text wait_2__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::wait_2() {
    if (field_0x813 != 1 && field_0x813 < 3) {
        f32 temp = dComIfGp_getPlayer(0)->current.pos.y - current.pos.y;
        field_0x813 = 0;
        if (temp > -1.0f) {
            field_0x813 = 3;
        }
    }

    return TRUE;
}

#include "SSystem/SComponent/c_counter.h"

/* 00002988-00002B2C       .text wait_action1__11daNpc_Gp1_cFPv */
BOOL daNpc_Gp1_c::wait_action1(void*) {
    switch (field_0x819) {
        case 0:
            if (dNpc_chkArasoi()) {
                actor_status &= ~fopAcStts_NOCULLEXEC_e;
                setStt(3);
            } else {
                actor_status |= fopAcStts_NOCULLEXEC_e;
                setStt(1);
                field_0x7ec = (g_Counter.mCounter0 & 3) + 2;
            }
            field_0x819++;
            break;
        case 1:
        case 2:
        case 3:
            field_0x808 = chkAttention();
            switch (field_0x814) {
                case 1:
                    field_0x804 = wait_1();
                    break;
                case 2:
                    field_0x804 = talk_1();
                    break;
                case 4:
                    field_0x804 = walk_1();
                    break;
                case 5:
                    field_0x804 = hair_1();
                    break;
                case 3:
                    field_0x804 = wait_2();
                    break;
            }

            if (field_0x813 != 1 && field_0x813 < 3) {
                if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_1501) && field_0x809 == 0 && chk_forceTlkArea()) {
                    field_0x813 = 1;
                }
            }
            lookBack();
            break;
        default:
        case 9:
            break;
    }
    return TRUE;
}

/* 00002B2C-00002C6C       .text demo__11daNpc_Gp1_cFv */
u8 daNpc_Gp1_c::demo() {
    if (demoActorID == 0) {
        if (field_0x80b) {
            field_0x80b = 0;
        }
    } else {
        field_0x80b = 1;
        dDemo_actor_c* demo_actor = dComIfGp_demo_getActor(demoActorID);
        if (m_hed_tex_pttrn) {
            field_0x6f0 += 1;
            if (field_0x6f0 >= m_hed_tex_pttrn->getFrameMax()) {
                field_0x6f0 = m_hed_tex_pttrn->getFrameMax();
            }
        }
        J3DAnmTexPattern* demo_pattern = demo_actor->getP_BtpData("Gp");
        if (demo_pattern) {
            m_hed_tex_pttrn = demo_pattern;
            if (mBtpAnm.init(mpMorf->getModel()->getModelData(), m_hed_tex_pttrn, 1, 2, 1.0f, 0, -1, true, FALSE)) {
                field_0x811 = 1;
                field_0x6f0 = 0;
            }
        }
        dDemo_setDemoData(this, 0x6a, mpMorf, "Gp");
    }
    return field_0x80b;
}

#include "d/d_com_inf_game.h"
#include "d/d_snap.h"

/* 00002C6C-00002E24       .text _draw__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* model_data = model->getModelData();

    if (field_0x7fc || field_0x7ff) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);
    mBtpAnm.entry(model_data, field_0x6f0);
    mpMorf->entryDL();
    mBtpAnm.remove(model_data);
    dNpc_setShadowModel(field_0x6d4, model_data, model);

    cXyz pos(current.pos.x, current.pos.y + 150.0f, current.pos.z);
    field_0x6d0 = dComIfGd_setRealShadow(field_0x6d0, 1, field_0x6d4, &pos, 800.0f,
                                          current.pos.y - mObjAcch.GetGroundH(), NULL);
    if (field_0x6d0 == 0) {
        dComIfGd_setSimpleShadow(&current.pos, mObjAcch.GetGroundH(), 40.0f,
                                  dComIfG_Bgsp()->GetTriPla(mObjAcch.m_gnd)->GetNP());
    }

    if (l_HIO.field_0x24) {
        GXColor color0 = {0xFF, 0x00, 0x00, 0x80};
        GXColor color1 = {0x00, 0x00, 0xFF, 0x80};
        GXColor color2 = {0x00, 0xFF, 0x00, 0x80};
        GXColor color3 = {0x00, 0xFF, 0x00, 0x80};
        cXyz srcPos;
        srcPos.x = current.pos.x;
        srcPos.y = current.pos.y;
        srcPos.z = current.pos.z;
        srcPos.y = eyePos.y;
    }

    dSnap_RegistFig(DSNAP_TYPE_UNK5B, this, 1.0f, 1.0f, 1.0f);
    return TRUE;
}

#include "f_op/f_op_actor_mng.h"

/* 00002E24-00003008       .text _execute__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::_execute() {
    if (field_0x802 == 0) {
        field_0x778 = current.pos.x;
        field_0x77c = current.pos.y;
        field_0x780 = current.pos.z;
        field_0x784 = current.angle.x;
        field_0x786 = current.angle.y;
        field_0x788 = current.angle.z;
        field_0x802 = 1;
    }

    m_jnt.setParam(l_HIO.mMaxBackboneX, l_HIO.mMaxBackboneY, l_HIO.mMinBackboneX, l_HIO.mMinBackboneY,
                   l_HIO.mMaxHeadX, l_HIO.mMaxHeadY, l_HIO.mMinHeadX, l_HIO.mMinHeadY,
                   l_HIO.mMaxTurnStep);

    if (field_0x7fc && demoActorID == 0) {
        return TRUE;
    }

    field_0x7fe = 0;
    field_0x7fc = 0;
    partner_srch();
    checkOrder();
    if (!demo()) {
        int event_id = -1;
        if (dComIfGp_event_runCheck() && !eventInfo.checkCommandTalk() != FALSE) {
            event_id = isEventEntry();
        }
        if (event_id >= 0) {
            event_proc(event_id);
        } else {
            (this->*field_0x6f4)(0);
        }
        if (field_0x7fe == 0) {
            gp_nMove();
            fopAcM_posMoveF(this, mStts.GetCCMoveP());
        }
        if (field_0x7fd == 0) {
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
    if (field_0x80b == 0) {
        setCollision(70.0f, 230.0f);
    }
    return TRUE;
}

/* 00003008-00003064       .text _delete__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::_delete() {
    dComIfG_resDelete((request_of_phase_process_class*)&field_0x6c4, "Gp");

    if (heap != NULL && mpMorf != NULL) {
        mpMorf->stopZelAnime();
    }

    return TRUE;
}

/* 00003064-00003084       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Gp1_c*)i_this)->CreateHeap();
}

/* 00003084-000031A4       .text _create__11daNpc_Gp1_cFv */
cPhs_State daNpc_Gp1_c::_create() {
    fopAcM_ct(this, daNpc_Gp1_c);

    cPhs_State resLoadResult = dComIfG_resLoad((request_of_phase_process_class*)&field_0x6c4, "Gp");
    if (resLoadResult != cPhs_COMPLEATE_e) {
        return resLoadResult;
    }

    if (!charDecide(fopAcM_GetParam(this) & 0xFF)) {
        return cPhs_ERROR_e;
    }

    static u32 a_size_tbl[] = { 0x272E0 };

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_size_tbl[field_0x817])) {
        return cPhs_ERROR_e;
    }

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -70.0f, -20.0f, -70.0f, 50.0f, 240.0f, 50.0f);

    if (!createInit()) {
        return cPhs_ERROR_e;
    }

    return resLoadResult;
}

daNpc_Gp1_c::daNpc_Gp1_c() {}

#include "res/Object/Gp.h"

/* 000035D4-00003844       .text create_Anm__11daNpc_Gp1_cFv */
J3DModelData* daNpc_Gp1_c::create_Anm() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Gp", dRes_ID_GP_BDL_GP_e);
    JUT_ASSERT(2159, a_mdl_dat != NULL);
    mpMorf = new mDoExt_McaMorf(
        a_mdl_dat,
        NULL,
        NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes("Gp", dRes_ID_GP_BCK_WAIT01_e),
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
    }
    if (mpMorf->getModel() == NULL) {
        mpMorf = NULL;
        return NULL;
    }

    m_hed_jnt_num = a_mdl_dat->getJointName()->getIndex("head");
    JUT_ASSERT(2179, m_hed_jnt_num >= 0);
    m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone");
    JUT_ASSERT(2182, m_bbone_jnt_num >= 0);
    m_hnd_L_jnt_num = a_mdl_dat->getJointName()->getIndex("handL");
    JUT_ASSERT(2185, m_hnd_L_jnt_num >= 0);

    return a_mdl_dat;
}

/* 00003844-000039A0       .text CreateHeap__11daNpc_Gp1_cFv */
BOOL daNpc_Gp1_c::CreateHeap() {
    J3DModelData* model_data = create_Anm();
    if (model_data == NULL) {
        return FALSE;
    }

    field_0x811 = 0;
    bool bVar2 = iniTexPttrnAnm(false);
    if (!bVar2) {
        mpMorf = NULL;
        return FALSE;
    }

    field_0x6d4 = mDoExt_J3DModel__create(model_data, 0x20000, 0x11020203);
    if (field_0x6d4 != NULL) {
        for (u16 i = 0; i < model_data->getJointNum(); i++) {
            if (i == m_hed_jnt_num || i == m_bbone_jnt_num) {
                mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Gp1);
            }
        }

#if TARGET_PC
        mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
        mpMorf->getModel()->setUserArea((u32)this);
#endif
        mAcchCir.SetWall(30.0f, 70.0f);
        mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir, fopAcM_GetSpeed_p(this));
        return TRUE;
    } else {
        mpMorf = NULL;
        return FALSE;
    }
}

/* 000039A0-000039C0       .text daNpc_Gp1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Gp1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Gp1_c*)i_this)->_create();
}

/* 000039C0-000039E0       .text daNpc_Gp1_Delete__FP11daNpc_Gp1_c */
static BOOL daNpc_Gp1_Delete(daNpc_Gp1_c* i_this) {
    return ((daNpc_Gp1_c*)i_this)->_delete();
}

/* 000039E0-00003A00       .text daNpc_Gp1_Execute__FP11daNpc_Gp1_c */
static BOOL daNpc_Gp1_Execute(daNpc_Gp1_c* i_this) {
    return ((daNpc_Gp1_c*)i_this)->_execute();
}

/* 00003A00-00003A20       .text daNpc_Gp1_Draw__FP11daNpc_Gp1_c */
static BOOL daNpc_Gp1_Draw(daNpc_Gp1_c* i_this) {
    return ((daNpc_Gp1_c*)i_this)->_draw();
}

/* 00003A20-00003A28       .text daNpc_Gp1_IsDelete__FP11daNpc_Gp1_c */
static BOOL daNpc_Gp1_IsDelete(daNpc_Gp1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Gp1_Method = {
    (process_method_func)daNpc_Gp1_Create,
    (process_method_func)daNpc_Gp1_Delete,
    (process_method_func)daNpc_Gp1_Execute,
    (process_method_func)daNpc_Gp1_IsDelete,
    (process_method_func)daNpc_Gp1_Draw,
};

actor_process_profile_definition g_profile_NPC_GP1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_GP1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Gp1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_GP1_e,
    /* Actor SubMtd */ &l_daNpc_Gp1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
