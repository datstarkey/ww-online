/**
 * d_a_npc_aj1.cpp
 * NPC - Sturgeon
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_npc_aj1.h"

class daNpc_Aj1_HIO_c : public mDoHIO_entry_c {
public:
    daNpc_Aj1_HIO_c();
    virtual ~daNpc_Aj1_HIO_c() {}

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
    /* 0x025 */ u8 field_0x25[0x28 - 0x25];
    /* 0x028 */ f32 m028;
    /* 0x02C */ s16 m02C;
    /* 0x02E */ u8 field_0x2e[0x30 - 0x2E];
    /* 0x030 */ f32 m030;
    /* 0x034 */ s16 m034;
    /* 0x036 */ s16 m036;
    /* 0x038 */ s16 m038;
    /* 0x03A */ u8 field_0x3a[0x3C - 0x3A];
};

static daNpc_Aj1_HIO_c l_HIO;

/* 000000EC-00000150       .text __ct__15daNpc_Aj1_HIO_cFv */
daNpc_Aj1_HIO_c::daNpc_Aj1_HIO_c() {
    static u32 a_prm_tbl[12] = {
        0x20001F40, 0x01F4CD38, 0x00001C70, 0xFA24FF6A,
        0x04000400, 0x43160000, 0x00000000, 0x44BB8000,
        0x52080000, 0x44098000, 0x61A8001E, 0x00140000,
    };

    memcpy(&mMaxHeadX, a_prm_tbl, 0x30);
    mNo = -1;
    field_0x8 = -1;
}

static char* l_evn_tbl[] = { "angry" };

/* 00000198-000001E4       .text nodeCB_Head__FP7J3DNodei */
static BOOL nodeCB_Head(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Aj1_c* i_this = (daNpc_Aj1_c*)model->getUserArea();
        if (i_this) {
            i_this->_nodeCB_Head(i_node, model);
        }
    }

    return TRUE;
}

/* 000001E4-0000030C       .text _nodeCB_Head__11daNpc_Aj1_cFP7J3DNodeP8J3DModel */
void daNpc_Aj1_c::_nodeCB_Head(J3DNode* i_node, J3DModel* i_pModel) {
    static cXyz a_eye_pos_off(24.0f, -16.0f, 0.0f);

    s32 jointIdx = ((J3DJoint*)(i_node))->getJntNo();
    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jointIdx));

    mDoMtx_stack_c::multVecZero(&field_0x72c);
    mDoMtx_stack_c::YrotM(-m_jnt.getHead_y());
    mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
    mDoMtx_stack_c::multVec(&a_eye_pos_off, &field_0x714);

    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jointIdx, mDoMtx_stack_c::get());
}

/* 00000348-00000394       .text nodeCB_BackBone__FP7J3DNodei */
static BOOL nodeCB_BackBone(J3DNode* i_node, int i_param2) {

    if (i_param2 == 0) {
        J3DModel* model = j3dSys.getModel();
        if (model->getUserArea() != NULL) {
            ((daNpc_Aj1_c*)model->getUserArea())->_nodeCB_BackBone(i_node, model);
        }
    }
    return TRUE;

}

/* 00000394-0000042C       .text _nodeCB_BackBone__11daNpc_Aj1_cFP7J3DNodeP8J3DModel */
void daNpc_Aj1_c::_nodeCB_BackBone(J3DNode* i_node, J3DModel* i_pModel) {
    s32 jntNo = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jntNo));
    mDoMtx_stack_c::XrotM(m_jnt.getBackbone_y());
    mDoMtx_stack_c::ZrotM(m_jnt.getBackbone_x());
    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 0000042C-0000044C       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Aj1_c*)i_this)->CreateHeap();
}

/* 0000044C-0000046C       .text daNpc_Aj1_XyCheck_CB__FPvi */
static bool daNpc_Aj1_XyCheck_CB(void* i_this, int i_itemBtn) {
    return ((daNpc_Aj1_c*)i_this)->_XyCheckCB(i_itemBtn);
}

/* 0000046C-0000048C       .text _XyCheckCB__11daNpc_Aj1_cFi */
bool daNpc_Aj1_c::_XyCheckCB(int i_itemBtn) {
    if (dComIfGp_getSelectItem(i_itemBtn) == dItemNo_KNIGHTS_CREST_e) {
        return true;
    }
    return false;
}

#include "d/d_com_inf_game.h"

/* 0000048C-00000574       .text init_AJ1_0__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::init_AJ1_0() {
    if (!dComIfGs_isEventBit(0xe20) && !dComIfGs_isEventBit(0x502)) {
        attention_info.distances[fopAc_Attn_TYPE_TALK_e] = 0x1a;
        set_action(&daNpc_Aj1_c::wait_action1, NULL);
        return true;
    }

    if (field_0x75b != 0xff) {
        if (!dComIfGs_isSwitch(field_0x75b, current.roomNo)) {
            dComIfGs_onSwitch(field_0x75b, current.roomNo);
        }
    }

    return false;
}

/* 00000574-00000624       .text init_AJ1_1__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::init_AJ1_1() {
    if (!dComIfGs_isEventBit(0x520)) {
        dComIfGs_onEventBit(0x502);
        set_action(&daNpc_Aj1_c::wait_action2, NULL);
        fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
        fopAcM_OnStatus(this, fopAcStts_UNK4000_e);
        return true;
    }

    return false;
}

/* 00000624-000006B0       .text init_AJ1_2__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::init_AJ1_2() {
    if (dComIfGs_isEventBit(0x520)) {
        eventInfo.setXyCheckCB((dEvt_info_c::CallbackFunc)daNpc_Aj1_XyCheck_CB);
        set_action(&daNpc_Aj1_c::wait_action2, NULL);
        return true;
    }

    return false;
}

/* 000006B0-00000868       .text createInit__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::createInit() {
    field_0x75b = (fopAcM_GetParam(this) >> 8) & 0xff;
    mEventIdx[0] = dComIfGp_evmng_getEventIdx(l_evn_tbl[0], 0xff);
    mEventCut.setActorInfo2("Aj1", this);

    attention_info.flags = 10;
    attention_info.distances[fopAc_Attn_TYPE_TALK_e] = 0xad;
    attention_info.distances[fopAc_Attn_TYPE_SPEAK_e] = 0xad;
    field_0x7b9 = 9;

    bool ret;
    switch (field_0x7bf) {
    case 0:
        ret = init_AJ1_0();
        break;
    case 1:
        ret = init_AJ1_1();
        break;
    case 2:
        ret = init_AJ1_2();
        break;
    default:
        ret = false;
        break;
    }

    if (!ret) {
        return false;
    }

    field_0x70e = current.angle.x;
    field_0x710 = current.angle.y;
    field_0x712 = current.angle.z;
    shape_angle.x = field_0x70e;
    shape_angle.y = field_0x710;
    shape_angle.z = field_0x712;
    gravity = -4.5f;

    mStts.Init(0xff, 0xff, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);

    play_animation();
    mObjAcch.CrrPos(*dComIfG_Bgsp());
    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    mpMorf->setMorf(0.0f);
    setMtx(true);

    return true;
}

/* 00000868-00000910       .text play_animation__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::play_animation() {
    u32 mtrlSndId = 0;
    play_texPttrnAnm();
    if (mObjAcch.ChkGroundHit()) {
        mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(mObjAcch.m_gnd);
    }

    field_0x758 = mpMorf->play(&eyePos, mtrlSndId, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    if (mpMorf->getFrame() < field_0x740) {
        field_0x758 = 1;
    }

    field_0x740 = mpMorf->getFrame();
}

/* 00000910-000009C0       .text ctrl_WAITanm__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::ctrl_WAITanm() {
    switch (field_0x7b9) {
    case 0:
        if (!cLib_calcTimer(&field_0x748) && field_0x758 != 0 && chk_parts_notMov()) {
            setAnm_NUM(1, 1);
        }
        break;
    case 1:
        if (field_0x758 != 0) {
            setAnm_NUM(0, 1);
            field_0x748 = cLib_getRndValue(0x5a, 0xb4);
        }
        break;
    }
}

/* 000009C0-00000A3C       .text ctrl_TIREanm__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::ctrl_TIREanm() {
    if (field_0x7b9 == 6) {
        if (field_0x758 != 0 && !cLib_calcTimer(&field_0x75c)) {
            setAnm_NUM(0, 1);
            mpMorf->setMorf(20.0f);
        }
    } else {
        field_0x75c = 0;
    }
}

/* 00000A3C-00000B30       .text setMtx__11daNpc_Aj1_cFb */
void daNpc_Aj1_c::setMtx(bool i_param) {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::ZXYrotM(field_0x70e, field_0x710, field_0x712);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();

    if (field_0x6d0 != NULL) {
        field_0x6d0->setBaseTRMtx(mpMorf->getModel()->getAnmMtx(m_hnd_L_jnt_num));
        field_0x6d0->calc();
    }

    setAttention(i_param);
}

#include "res/Object/Aj.h"

/* 00000B30-00000B44       .text bckResID__11daNpc_Aj1_cFi */
s32 daNpc_Aj1_c::bckResID(int i_idx) {
    static const s32 a_res_id_tbl[9] = {
        dRes_ID_AJ_BCK_WAIT01_e, dRes_ID_AJ_BCK_WAIT02_e, dRes_ID_AJ_BCK_ANGRY_e,
        dRes_ID_AJ_BCK_JAMP_e,   dRes_ID_AJ_BCK_SHOCK_e,  dRes_ID_AJ_BCK_LOOK_e,
        dRes_ID_AJ_BCK_TIRE_e,   dRes_ID_AJ_BCK_CALL_e,   dRes_ID_AJ_BCK_NOBI_e,
    };

    return a_res_id_tbl[i_idx];
}

#include "JSystem/JUtility/JUTAssert.h"

/* 00000B44-00000B58       .text btpResID__11daNpc_Aj1_cFi */
s32 daNpc_Aj1_c::btpResID(int i_idx) {
    static const s32 a_res_id_tbl[1] = { dRes_ID_AJ_BTP_MABA_e };

    return a_res_id_tbl[i_idx];
}

/* 00000B58-00000C64       .text init_texPttrnAnm__11daNpc_Aj1_cFScb */
bool daNpc_Aj1_c::init_texPttrnAnm(signed char i_type, bool i_modify) {
    J3DModel* model = mpMorf->getModel();

    if (i_type < 0) {
        return false;
    }

    s32 res_id = btpResID(i_type);
    J3DAnmTexPattern* a_btp = (J3DAnmTexPattern*)dComIfG_getObjectIDRes("Aj", (u16)res_id);
    JUT_ASSERT(0x213, a_btp != NULL);

    field_0x7b8 = i_type;
    field_0x6ec = 0;
    field_0x6ee = 0;

    return mBtpAnm.init(model->getModelData(), a_btp, TRUE, 0, 1.0f, 0, -1, i_modify, 0) != 0;
}

/* 00000C64-00000CF4       .text play_texPttrnAnm__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::play_texPttrnAnm() {
    if (field_0x7b8 != 0 || !cLib_calcTimer(&field_0x6ee)) {
        field_0x6ec++;
        s16 frameMax = mBtpAnm.getBtpAnm()->getFrameMax();
        if (field_0x6ec >= frameMax) {
            if (field_0x7b8 != 0) {
                field_0x6ec = frameMax;
            } else {
                field_0x6ee = cLib_getRndValue(0x3c, 0x5a);
                field_0x6ec = 0;
            }
        }
    }
}

#include "JSystem/JParticle/JPAEmitter.h"

/* 00000CF4-00000DFC       .text setAnm_anm__11daNpc_Aj1_cFPQ211daNpc_Aj1_c9anm_prm_c */
void daNpc_Aj1_c::setAnm_anm(daNpc_Aj1_c::anm_prm_c* i_prm) {
    if (i_prm->field0 < 0 || field_0x7b9 == i_prm->field0) {
        return;
    }

    dNpc_setAnmIDRes(mpMorf, i_prm->fieldC, i_prm->field4, i_prm->field8, bckResID(i_prm->field0), -1,
                      "Aj");

    field_0x7b9 = i_prm->field0;
    field_0x758 = 0;
    field_0x759 = 0;
    field_0x740 = 0.0f;

    switch (field_0x7b9) {
    case 2:
        set_pa_pun();
        set_pa_aka();
        set_pa_don();
        return;
    }

    if (field_0x794 != NULL) {
        field_0x794->stopCreateParticle();
        field_0x7b4 = 1;
    }

    del_pa(&field_0x790);
    del_pa(&field_0x798);
}

/* 00000DFC-00000E6C       .text setAnm_NUM__11daNpc_Aj1_cFii */
void daNpc_Aj1_c::setAnm_NUM(int i_param_1, int param_2) {
    static anm_prm_c a_anm_prm_tbl[9] = {
        {0, 0, 0.0f, 1.0f, 2},
        {1, 0, 0.0f, 1.0f, 2},
        {2, 0, 0.0f, 1.0f, 2},
        {3, 0, 8.0f, 1.0f, 0},
        {4, 0, 0.0f, 1.0f, 0},
        {5, 0, 5.0f, 1.0f, 0},
        {6, 0, 0.0f, 1.0f, 2},
        {7, 0, 8.0f, 1.0f, 2},
        {8, 0, 8.0f, 1.0f, 0},
    };

    if (param_2 != 0) {
        init_texPttrnAnm(a_anm_prm_tbl[i_param_1].field1, true);
    }
    setAnm_anm(&a_anm_prm_tbl[i_param_1]);
}

/* 00000E6C-00000ED8       .text setAnm__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[5] = {
        {-1, -1, 0.0f, 0.0f, -1},
        {0, 0, 0.0f, 1.0f, 2},
        {7, 0, 8.0f, 1.0f, 2},
        {-1, -1, 0.0f, 0.0f, -1},
        {0, 0, 8.0f, 1.0f, 2},
    };

    init_texPttrnAnm(a_anm_prm_tbl[field_0x7bb].field1, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7bb]);
}

/* 00000ED8-00000EDC       .text chngAnmTag__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::chngAnmTag() {
}

/* 00000EDC-00000EE0       .text ctrlAnmTag__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::ctrlAnmTag() {
}

/* 00000EE0-00000F44       .text chngAnmAtr__11daNpc_Aj1_cFUc */
void daNpc_Aj1_c::chngAnmAtr(unsigned char i_param_1) {
    if (i_param_1 == field_0x7b6 || i_param_1 > 9) {
        return;
    }

    field_0x7b6 = i_param_1;
    setAnm_ATR();
    switch (field_0x7b6) {
    case 8:
        field_0x75c = 3;
        break;
    }
}

/* 00000F44-00000FC0       .text ctrlAnmAtr__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::ctrlAnmAtr() {
    switch (field_0x7b6) {
    case 0:
        break;
    case 1:
        break;
    case 2:
        ctrl_WAITanm();
        break;
    case 3:
        break;
    case 4:
        break;
    case 5:
        if (field_0x758 != 0) {
            field_0x7b6 = 0;
            setAnm_NUM(0, 1);
        }
        break;
    case 6:
        break;
    case 7:
        break;
    case 8:
        break;
    case 9:
        break;
    }

    ctrl_TIREanm();
}

/* 00000FC0-00001024       .text setAnm_ATR__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::setAnm_ATR() {
    static anm_prm_c a_anm_prm_tbl[9] = {
        {0, 0, 0.0f, 1.0f, 2},
        {1, 0, 0.0f, 1.0f, 2},
        {0, 0, 8.0f, 1.0f, 2},
        {2, 0, 0.0f, 1.0f, 2},
        {3, 0, 8.0f, 1.0f, 0},
        {1, 0, 8.0f, 1.0f, 2},
        {4, 0, 0.0f, 1.0f, 0},
        {5, 0, 5.0f, 1.0f, 0},
        {6, 0, 0.0f, 1.0f, 2},
    };

    init_texPttrnAnm(a_anm_prm_tbl[field_0x7b6].field1, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7b6]);
}

/* 00001024-000010E0       .text anmAtr__11daNpc_Aj1_cFUs */
void daNpc_Aj1_c::anmAtr(unsigned short i_param) {
    switch (i_param) {
    case 6:
        if (field_0x7c1 == 0) {
            chngAnmAtr(dComIfGp_getMesgAnimeAttrInfo());
            field_0x7c1++;
        }

        {
            u8 tag = dComIfGp_getMesgAnimeTagInfo();
            if (tag != 0xFF && tag != field_0x7b7) {
                dComIfGp_clearMesgAnimeTagInfo();
                field_0x7b7 = tag;
                chngAnmTag();
            }
        }
        break;
    case 14:
        field_0x7c1 = 0;
        break;
    }

    ctrlAnmAtr();
    ctrlAnmTag();
}

#include "d/d_kankyo.h"

/* 000010E0-000012B4       .text next_msgStatus__11daNpc_Aj1_cFPUl */
u16 daNpc_Aj1_c::next_msgStatus(u32* pMsgNo) {
    u16 status = 0xf;

    switch (*pMsgNo) {
    case 0x9c6:
        *pMsgNo = 0x9c7;
        break;
    case 0x9c7:
        *pMsgNo = 0x9c8;
        break;
    case 0x9c9:
        *pMsgNo = 0x9ca;
        break;
    case 0x9c8:
    case 0x9ca:
        if (dComIfGs_isEventBit(1) && !dComIfGs_isEventBit(0x3704)) {
            *pMsgNo = 0x9da;
        } else {
            status = 0x10;
        }
        break;
    case 0x9db:
        if (!dComIfGs_isEventBit(0x504)) {
            if (dComIfGs_isEventBit(0x2a80)) {
                *pMsgNo = 0x9dc;
            } else {
                *pMsgNo = 0x9cb;
            }
        } else {
            *pMsgNo = 0x9cf;
        }
        break;
    case 0x9cb:
    case 0x9dc:
        *pMsgNo = 0x9cc;
        break;
    case 0x9cd:
        *pMsgNo = 0x9ce;
        break;
    case 0x9cf:
        if (dComIfGs_isEventBit(1) && !dComIfGs_isEventBit(0x3704)) {
            *pMsgNo = 0x9dd;
        } else {
            status = 0x10;
        }
        break;
    case 0x9d0:
        if (!dComIfGs_isEventBit(0x2a20)) {
            *pMsgNo = 0x9d1;
        } else if (!dKy_daynight_check()) {
            *pMsgNo = 0x9d2;
        } else {
            *pMsgNo = 0x9d3;
        }
        break;
    default:
        status = 0x10;
        break;
    }

    return status;
}

/* 000012B4-000012F4       .text getMsg_AJ1_0__11daNpc_Aj1_cFv */
u32 daNpc_Aj1_c::getMsg_AJ1_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0510)) {
        return 0x9C9;
    } else {
        return 0x9C6;
    }
}

/* 000012F4-00001398       .text getMsg_AJ1_1__11daNpc_Aj1_cFv */
u32 daNpc_Aj1_c::getMsg_AJ1_1() {
    if (dComIfGs_isEventBit(0xe20) && !dComIfGs_isEventBit(0x3702)) {
        dComIfGs_onEventBit(0x3702);
        return 0x9DB;
    }

    if (dComIfGs_isEventBit(0x504)) {
        return 0x9CF;
    }

    if (dComIfGs_isEventBit(0x2a80)) {
        return 0x9DC;
    }

    return 0x9CB;
}

/* 00001398-00001460       .text getMsg_AJ1_2__11daNpc_Aj1_cFv */
u32 daNpc_Aj1_c::getMsg_AJ1_2() {
    if (field_0x75a == 0x48) {
        if (!dComIfGs_isEventBit(0x3701)) {
            return 0x9D7;
        }

        return dComIfGs_isEventBit(0xb20) ? 0x9D8 : 0x9D9;
    }

    if (!dComIfGs_isEventBit(0x3708)) {
        return 0x9D0;
    }

    if (!dComIfGs_isEventBit(0x2a20)) {
        return 0x9D4;
    }

    return dKy_daynight_check() == 0 ? 0x9D5 : 0x9D6;
}

/* 00001460-000014D0       .text getMsg__11daNpc_Aj1_cFv */
u32 daNpc_Aj1_c::getMsg() {
    u32 msgNo = 0;

    switch (field_0x7bf) {
    case 0:
        msgNo = getMsg_AJ1_0();
        break;
    case 1:
        msgNo = getMsg_AJ1_1();
        break;
    case 2:
        msgNo = getMsg_AJ1_2();
        break;
    }

    return msgNo;
}

/* 000014D0-00001574       .text eventOrder__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::eventOrder() {
    s8 stt = field_0x7ba;
    if (stt == 1 || stt == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x7bf == 2) {
            eventInfo.onCondition(dEvtCnd_CANTALKITEM_e);
        }

        if (field_0x7ba == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (stt >= 3) {
        field_0x746 = stt - 3;
        fopAcM_orderOtherEventId(this, mEventIdx[field_0x746], 0xff, 0xffff, 0, 1);
    }
}

/* 00001574-00001628       .text checkOrder__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::checkOrder() {
    if (eventInfo.getCommand() == dEvtCmd_INDEMO_e) {
        if (dComIfGp_evmng_startCheck(mEventIdx[field_0x746])) {
            switch (field_0x746) {
            case 0:
                fopAcM_OffStatus(this, fopAcStts_UNK4000_e);
                break;
            }

            field_0x7ba = 0;
        }
    } else if (eventInfo.getCommand() == dEvtCmd_INTALK_e) {
        if (field_0x7ba == 1 || field_0x7ba == 2) {
            field_0x7ba = 0;
            field_0x769 = 1;
        }
    }
}

/* 00001628-000016C0       .text chk_talk__11daNpc_Aj1_cFv */
u8 daNpc_Aj1_c::chk_talk() {
    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x75a = dComIfGp_event_getPreItemNo();
            return 1;
        } else {
            return 0;
        }
    } else {
        field_0x75a = 0xFF;
        return 1;
    }
}

/* 000016C0-00001700       .text chk_parts_notMov__11daNpc_Aj1_cFv */
u8 daNpc_Aj1_c::chk_parts_notMov() {
    u8 ret = 0;

    if (field_0x73a == m_jnt.getHead_y() && field_0x73c == m_jnt.getBackbone_y() &&
        field_0x738 == current.angle.y) {
        ret = 1;
    }

    return ret;
}

/* 00001700-00001870       .text lookBack__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::lookBack() {
    field_0x73a = m_jnt.getHead_y();
    field_0x73c = m_jnt.getBackbone_y();
    field_0x738 = current.angle.y;

    cXyz dstPos;
    cXyz srcPos = current.pos;
    srcPos.y = eyePos.y;
    dstPos.set(0.0f, 0.0f, 0.0f);
    cXyz* pDstPos = NULL;
    s16 defaultY = current.angle.y;
    bool param6 = field_0x76a;

    switch ((s8)field_0x7bd) {
    case 1:
        dstPos = dNpc_playerEyePos(-20.0f);
        pDstPos = &dstPos;
        break;
    case 2:
        dstPos = field_0x720;
        pDstPos = &dstPos;
        break;
    case 3:
        defaultY = field_0x756;
        break;
    }

    cLib_addCalcAngleS2(&field_0x754, l_HIO.field_0x1e, 4, 0x800);
    if (!m_jnt.trnChk()) {
        field_0x754 = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, pDstPos, srcPos, defaultY, field_0x754, param6);
}

/* 00001870-000018F0       .text chkAttention__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::chkAttention() {
    if (dComIfGp_getAttention().LockonTruth()) {
        return this == dComIfGp_getAttention().LockonTarget(0);
    }

    return this == dComIfGp_getAttention().ActionTarget(0);
}

/* 000018F0-00001948       .text setAttention__11daNpc_Aj1_cFb */
void daNpc_Aj1_c::setAttention(bool i_param_1) {
    attention_info.position.set(current.pos.x, current.pos.y + l_HIO.field_0x20, current.pos.z);

    if (field_0x764 == 0 && !i_param_1) {
        return;
    }
    eyePos.set(field_0x714.x, field_0x714.y, field_0x714.z);
}

/* 00001948-000019C8       .text decideType__11daNpc_Aj1_cFi */
bool daNpc_Aj1_c::decideType(int i_type) {
    field_0x7be = 0;
    field_0x7bf = -1;

    switch (i_type) {
    case 0:
        field_0x7bf = 0;
        break;
    case 1:
        field_0x7bf = 1;
        break;
    case 2:
        field_0x7bf = 2;
        break;
    }

    bool ret = false;
    if (field_0x7be != -1 && field_0x7bf != -1) {
        ret = true;
    }

    return ret;
}

/* 000019C8-000019E0       .text cut_init_AJ1_TLK__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_AJ1_TLK() {
    field_0x7b6 = 0xff;
    field_0x7b7 = 0xff;
    field_0x7c1 = 0x0;
}

/* 000019E0-00001A3C       .text cut_move_AJ1_TLK__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_AJ1_TLK() {
    if (talk(1) == fopMsgStts_BOX_CLOSED_e) {
        field_0x7b6 = 0xff;
        field_0x7b7 = 0xff;
        field_0x7c1 = 0;
        return true;
    }
    return false;
}

/* 00001A3C-00001A80       .text cut_init_INI_ANGRY__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_INI_ANGRY() {
    setAnm_NUM(0, 1);
    mpMorf->setMorf(8.0f);
}

/* 00001A80-00001A88       .text cut_move_INI_ANGRY__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_INI_ANGRY() {
    return true;
}

/* 00001A88-00001A8C       .text cut_init_VIVRATE__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_VIVRATE() {
}

/* 00001A8C-00001B5C       .text cut_move_VIVRATE__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_VIVRATE() {
    if (field_0x758 != 0) {
        mDoAud_seStart(JA_SE_CM_JI_BODY_ATTACK, NULL, 0,
                       dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
        dComIfGp_getVibration().StartShock(5, -0x11, cXyz(0.0f, 1.0f, 0.0f));
        setAnm_NUM(3, 1);
        return true;
    }

    return false;
}

/* 00001B5C-00001B60       .text cut_init_JMP__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_JMP() {
}

/* 00001B60-00001BBC       .text cut_move_JMP__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_JMP() {
    if (field_0x758) {
        dComIfGp_getDetect().set_quake(0);
        field_0x74c = 0x14;
        return true;
    }

    return false;
}

/* 00001BBC-00001BC0       .text cut_init_SPPRISE__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_SPPRISE() {
}

/* 00001BC0-00001C24       .text cut_move_SPPRISE__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_SPPRISE() {
    if (cLib_calcTimer(&field_0x74c) == 0) {
        setAnm_NUM(4, 1);
        field_0x74c = l_HIO.m036;
        return true;
    }
    return false;
}

/* 00001C24-00001C28       .text cut_init_LOK__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_LOK() {
}

/* 00001C28-00001CE0       .text cut_move_LOK__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_LOK() {
    if (field_0x758 != 0 && !cLib_calcTimer(&field_0x74c)) {
        setAnm_NUM(5, 1);
        mDoAud_seStart(JA_SE_CV_AJ_ANGRY, &current.pos, 0,
                       dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
        field_0x74c = l_HIO.m038;
        return true;
    }

    return false;
}

/* 00001CE0-00001CE4       .text cut_init_DAN__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_DAN() {
}

/* 00001CE4-00001D44       .text cut_move_DAN__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_DAN() {
    if (field_0x758 != 0 && cLib_calcTimer(&field_0x74c) == 0) {
        setAnm_NUM(2, 1);
        return true;
    }
    return false;
}

/* 00001D44-00001DB4       .text cut_init_INVIT__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::cut_init_INVIT() {
    fopAcM_seStartCurrent(this, JA_SE_CV_AJ_INVITE, 0);
}

/* 00001DB4-00001DBC       .text cut_move_INVIT__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::cut_move_INVIT() {
    return true;
}

/* 00001DBC-00001F7C       .text privateCut__11daNpc_Aj1_cFi */
void daNpc_Aj1_c::privateCut(int i_staffIdx) {
    static char* a_cut_tbl[] = {
        "AJ1_TLK", "INI_ANGRY", "VIVRATE", "JMP", "SPPRISE", "LOK", "DAN", "INVITE"
    };

    if (i_staffIdx == -1) {
        return;
    }

    field_0x7b5 = dComIfGp_evmng_getMyActIdx(i_staffIdx, a_cut_tbl, ARRAY_SIZE(a_cut_tbl), TRUE, 0);

    if (field_0x7b5 == -1) {
        dComIfGp_evmng_cutEnd(i_staffIdx);
    } else {
        if (dComIfGp_evmng_getIsAddvance(i_staffIdx)) {
            switch (field_0x7b5) {
                case 0:
                    cut_init_AJ1_TLK();
                    break;
                case 1:
                    cut_init_INI_ANGRY();
                    break;
                case 2:
                    cut_init_VIVRATE();
                    break;
                case 3:
                    cut_init_JMP();
                    break;
                case 4:
                    cut_init_SPPRISE();
                    break;
                case 5:
                    cut_init_LOK();
                    break;
                case 6:
                    cut_init_DAN();
                    break;
                case 7:
                    cut_init_INVIT();
                    break;
            }
        }

        bool bVar;
        switch (field_0x7b5) {
            case 0:
                bVar = cut_move_AJ1_TLK();
                break;
            case 1:
                bVar = cut_move_INI_ANGRY();
                break;
            case 2:
                bVar = cut_move_VIVRATE();
                break;
            case 3:
                bVar = cut_move_JMP();
                break;
            case 4:
                bVar = cut_move_SPPRISE();
                break;
            case 5:
                bVar = cut_move_LOK();
                break;
            case 6:
                bVar = cut_move_DAN();
                break;
            case 7:
                bVar = cut_move_INVIT();
                break;
            default:
                bVar = true;
                break;
        }

        if (bVar) {
            dComIfGp_evmng_cutEnd(i_staffIdx);
        }
    }
}

/* 00001F7C-00001FA0       .text endEvent__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x7b6 = 0xFF;
    field_0x7b7 = 0xFF;
}

/* 00001FA0-00001FD8       .text isEventEntry__11daNpc_Aj1_cFv */
s32 daNpc_Aj1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mEventCut.getActorName(), NULL, 0);
}

/* 00001FD8-00002098       .text event_proc__11daNpc_Aj1_cFi */
void daNpc_Aj1_c::event_proc(int i_staffIdx) {
    if (dComIfGp_evmng_endCheck(mEventIdx[field_0x746])) {
        switch (field_0x746) {
        case 0:
            dComIfGs_onEventBit(0x508);
            dComIfGs_onEventBit(0x504);
            break;
        }

        endEvent();
    } else {
        if (!mEventCut.cutProc()) {
            privateCut(i_staffIdx);
        }

        lookBack();
    }
}

/* 00002098-00002120       .text set_pa_pun__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::set_pa_pun() {
    field_0x790 = dComIfGp_particle_set(dPa_name::ID_IT_SN_AOJI_PUNPUN00, &current.pos, NULL, NULL, 0xff,
                                        NULL, fopAcM_GetRoomNo(this));
    if (field_0x790 != NULL) {
        field_0x74a = 0;
    }
}

/* 00002120-000021B4       .text set_pa_aka__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::set_pa_aka() {
    JPABaseEmitter* emitter = field_0x794;
    if (emitter != NULL) {
        emitter->setMaxFrame(-1);
        emitter->stopCreateParticle();
    }

    field_0x794 = dComIfGp_particle_set(dPa_name::ID_IT_SN_AOJI_AKAAKA00, &current.pos, NULL, NULL, 0xff,
                                        NULL, fopAcM_GetRoomNo(this));
}

/* 000021B4-00002284       .text set_pa_don__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::set_pa_don() {
    cXyz offset(37.3f, 0.0f, 13.8f);

    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::YrotM(current.angle.y);
    mDoMtx_stack_c::multVec(&offset, &field_0x7a8);

    field_0x798 = dComIfGp_particle_set(dPa_name::ID_IT_SN_AOJI_DONDON00, &field_0x7a8, NULL, NULL, 0xff,
                                        NULL, fopAcM_GetRoomNo(this));
}

/* 00002284-000023FC       .text set_pa_smk__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::set_pa_smk() {
    GXColor color = {0xa0, 0xa0, 0x80, 0xc8};

    mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_fot_L_jnt_num));
    mDoMtx_stack_c::multVecZero(&field_0x79c);
    mSmokeCB.remove();

    field_0x78c = dComIfGp_particle_setToon(dPa_name::ID_AK_JT_ELEMENTSMOKE01, &field_0x79c,
                                            &current.angle, NULL, 0xc8, &mSmokeCB,
                                            fopAcM_GetRoomNo(this));
    if (field_0x78c != NULL) {
        field_0x78c->setGlobalParticleScale(0.3f, 0.3f);
        field_0x78c->setGlobalDynamicsScale(JGeometry::TVec3<f32>(0.1f, 0.1f, 0.1f));
        field_0x78c->setLifeTime(0x28);
        field_0x78c->setRate(3.0f);
        field_0x78c->setMaxFrame(1);
        field_0x78c->setDirectionalSpeed(120.0f);
        field_0x78c->setEmitterScale(JGeometry::TVec3<f32>(1.0f, 0.1f, 1.0f));
        mSmokeCB.setColor(color);
    }
}

/* 000023FC-000024B8       .text flw_pa_pun__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::flw_pa_pun() {
    if (field_0x790 != NULL) {
        field_0x790->setGlobalRTMatrix(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));

        if (!cLib_calcTimer(&field_0x74a)) {
            mDoAud_seStart(JA_SE_CM_AJ_ANGRY_STEAM, &current.pos, 0,
                           dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
            field_0x74a = 5;
        }
    }
}

/* 000024B8-00002508       .text flw_pa_aka__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::flw_pa_aka() {
    if (field_0x794 != NULL) {
        field_0x794->setGlobalRTMatrix(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));
    }
}

/* 00002508-00002560       .text del_pa_aka__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::del_pa_aka() {
    if (field_0x7b4 != 0) {
        if (field_0x794 != NULL) {
            if (field_0x794->getParticleNumber() == 0) {
                field_0x794->becomeInvalidEmitter();
                field_0x7b4 = 0;
                field_0x794 = NULL;
            }
        } else {
            field_0x7b4 = 0;
        }
    }
}

/* 00002560-0000258C       .text del_pa__11daNpc_Aj1_cFPP14JPABaseEmitter */
void daNpc_Aj1_c::del_pa(JPABaseEmitter** pEmitter) {
    if (*pEmitter == NULL) {
        return;
    }
    (*pEmitter)->becomeInvalidEmitter();
    *pEmitter = NULL;
}

#include "f_op/f_op_actor_mng.h"

/* 0000258C-00002678       .text setSmoke__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::setSmoke() {
    if (field_0x7b9 == 2) {
        if (mpMorf->checkFrame(0.0f) || mpMorf->checkFrame(9.0f)) {
            set_pa_smk();
        }

        if (mpMorf->checkFrame(0.0f) || mpMorf->checkFrame(10.0f)) {
            fopAcM_seStartCurrent(this, JA_SE_CM_AJ_STICK, 0);
        }
    }
}

/* 00002678-00002724       .text set_action__11daNpc_Aj1_cFM11daNpc_Aj1_cFPCvPvPv_iPv */
BOOL daNpc_Aj1_c::set_action(int (daNpc_Aj1_c::*i_func)(void*), void* i_arg) {
    if (mpAction != i_func) {
        if (mpAction) {
            field_0x7c0 = 9;
            (this->*mpAction)(i_arg);
        }

        mpAction = i_func;
        field_0x7c0 = 0;
        (this->*mpAction)(i_arg);
    }

    return TRUE;
}

/* 00002724-0000280C       .text setStt__11daNpc_Aj1_cFSc */
void daNpc_Aj1_c::setStt(s8 i_stt) {
    u8 prev_stt = field_0x7bb;
    field_0x7bb = i_stt;
    switch (field_0x7bb) {
    case 0:
        break;
    case 1:
        field_0x7ba = 0;
        field_0x750 = cLib_getRndValue(0x5a, 0x78);
        break;
    case 2:
        field_0x7ba = 0;
        break;
    case 3:
        field_0x7ba = 0;
        field_0x7b6 = 0xff;
        field_0x7b7 = 0xff;
        field_0x7c1 = 0;
        field_0x7bc = prev_stt;
        break;
    case 4:
        field_0x7ba = 0;
        field_0x748 = cLib_getRndValue(0x5a, 0xb4);
        if (field_0x7b9 == 1 || field_0x7b9 == 6) {
            return;
        }
        break;
    }

    setAnm();
}

/* 0000280C-00002988       .text chk_areaIN__11daNpc_Aj1_cFfs4cXyz */
bool daNpc_Aj1_c::chk_areaIN(f32 param_1, s16 param_2, cXyz param_3) {
    f32 abs1 = (dComIfGp_getPlayer(0)->current.pos - param_3).absXZ();
    f32 abs2 = dComIfGp_getPlayer(0)->current.pos.y - param_3.y;
    int angleY = cLib_targetAngleY(&current.pos, &dComIfGp_getPlayer(0)->current.pos);
    s16 angle = (angleY - field_0x70a);

    if (abs1 < param_1 && std::fabsf(abs2) < 500.0f && abs(angle) < param_2) {
        return true;
    }
    return false;
}

/* 00002988-00002A84       .text FARwai__11daNpc_Aj1_cFv */
int daNpc_Aj1_c::FARwai() {
    if (chk_areaIN(l_HIO.m028, l_HIO.m02C, current.pos)) {
        setStt(2);
        field_0x7bd = 1;
        field_0x76a = 0;
        m_jnt.mbTrn = true;
        return 1;
    }

    if (field_0x7b9 == 8) {
        if (field_0x758 != 0) {
            setAnm_NUM(0, 1);
            mpMorf->setMorf(8.0f);
        }
        return 1;
    }

    if (cLib_calcTimer(&field_0x750) == 0) {
        setAnm_NUM(8, 1);
        field_0x750 = cLib_getRndValue(0x5a, 0x78);
    }
    return 1;
}

/* 00002A84-00002C1C       .text call_1__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::call_1() {
    if (field_0x769) {
        if (chk_talk()) {
            setStt(3);
            field_0x7bd = 1;
            field_0x76a = 0;
            m_jnt.setTrn();
        }
        return TRUE;
    }

    if (chk_areaIN(l_HIO.m028, l_HIO.m02C, current.pos) == false) {
        setStt(1);
        mpMorf->setMorf(15.0f);
        field_0x7bd = 3;
        field_0x756 = field_0x70a;
        field_0x76a = 0;
        m_jnt.setTrn();
        return TRUE;
    }

    m_jnt.setTrn();
    if (chk_areaIN(l_HIO.m030, l_HIO.m034, current.pos)) {
        field_0x7ba = 2;
        if (field_0x768) {
            if (field_0x7b9) {
                setAnm_NUM(0, 1);
                mpMorf->setMorf(15.0f);
            }
            return TRUE;
        }

        if (field_0x7b9 != 7) {
            setAnm_NUM(7, 1);
        }
    }
    return TRUE;
}

/* 00002C1C-00002D14       .text wait_1__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::wait_1() {
    cLib_addCalcAngleS(&current.angle.y, field_0x70a, 4, 0x400, 0);
    ctrl_WAITanm();

    if (field_0x7ba == 1 || field_0x7ba >= 3) {
        return TRUE;
    }

    ctrl_TIREanm();
    if (field_0x769) {
        if (chk_talk()) {
            setStt(3);
            field_0x7bd = 1;
            field_0x76a = 0;
            m_jnt.setTrn();
        }
        return TRUE;
    }

    field_0x7ba = 2;
    if (field_0x7b9 != 1 && field_0x768) {
        field_0x7bd = 1;
        return TRUE;
    }

    field_0x7bd = 3;
    field_0x756 = field_0x70a;
    return TRUE;
}

/* 00002D14-00002E64       .text talk_1__11daNpc_Aj1_cFv */
int daNpc_Aj1_c::talk_1() {
    int ret = chk_parts_notMov();
    talk(1);
    if (mpCurrMsg == NULL) {
        return ret;
    }
    switch (mpCurrMsg->mStatus) {
    case fopMsgStts_BOX_OPENING_e:
    case fopMsgStts_MSG_TYPING_e:
        break;
    case fopMsgStts_MSG_DESTROYED_e:
        switch (mCurrMsgNo) {
        case 0x9da:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_3704);
        case 0x9c8:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_0510);
            break;
        case 0x9dd:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_3704);
            break;
        case 0x9d1:
        case 0x9d2:
        case 0x9d3:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_3708);
            break;
        case 0x9d7:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_3701);
            break;
        }
        field_0x75a = 0xff;
        field_0x769 = 0;
        setStt(field_0x7bc);
        field_0x74e = cLib_getRndValue(0xf, 0x1e);
        endEvent();
        break;
    }
    return ret;
}

/* 00002E64-00002F3C       .text wait_action1__11daNpc_Aj1_cFPv */
BOOL daNpc_Aj1_c::wait_action1(void*) {
    switch (field_0x7c0) {
    case 0:
        setStt(1);
        field_0x7c0++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x768 = chkAttention();
        switch (field_0x7bb) {
        case 1:
            field_0x764 = FARwai();
            break;
        case 2:
            field_0x764 = call_1();
            break;
        case 3:
            field_0x764 = talk_1();
            break;
        }
        lookBack();
        break;
    case 9:
        break;
    }

    return TRUE;
}

/* 00002F3C-0000304C       .text wait_action2__11daNpc_Aj1_cFPv */
BOOL daNpc_Aj1_c::wait_action2(void*) {
    switch (field_0x7c0) {
    case 0:
        setStt(4);
        if (strcmp(dComIfGp_getStartStageName(), "Ojhous2") == 0 && field_0x7bf == 1 &&
            !dComIfGs_isEventBit(0x508)) {
            field_0x7ba = 3;
        }
        field_0x7c0++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x768 = chkAttention();
        switch (field_0x7bb) {
        case 4:
            field_0x764 = wait_1();
            break;
        case 3:
            field_0x764 = talk_1();
            break;
        }
        lookBack();
        break;
    case 9:
        break;
    }

    return TRUE;
}

/* 0000304C-0000319C       .text demo__11daNpc_Aj1_cFv */
bool daNpc_Aj1_c::demo() {
    if (demoActorID == 0) {
        if (field_0x76b != 0) {
            field_0x76b = 0;
        }
    } else {
        if (field_0x76b == 0) {
            field_0x76b = 1;
            field_0x75f = 0;
            m_jnt.setHead_y(0);
            m_jnt.setHead_x(0);
            m_jnt.setBackBone_y(0);
            m_jnt.setBackBone_x(0);
        }
        dDemo_actor_c* demo_actor = dComIfGp_demo_getActor(demoActorID);
        if (mBtpAnm.getBtpAnm()) {
            field_0x6ec += 1;
            if (field_0x6ec >= mBtpAnm.getBtpAnm()->getFrameMax()) {
                field_0x6ec = mBtpAnm.getBtpAnm()->getFrameMax();
            }
        }
        J3DAnmTexPattern* demopattern = demo_actor->getP_BtpData("Aj");
        if (demopattern) {
            mBtpAnm.init(mpMorf->getModel()->getModelData(), demopattern, 1, 0, 1.0f, 0, -1, true, FALSE);
            field_0x7b8 = 1;
            field_0x6ec = 0;
        }
        dDemo_setDemoData(this, 0x6a, mpMorf, "Aj");
    }
    return field_0x76b;
}

/* 0000319C-00003254       .text shadowDraw__11daNpc_Aj1_cFv */
void daNpc_Aj1_c::shadowDraw() {
    cXyz pos(current.pos.x, 150.0f + current.pos.y, current.pos.z);

    field_0x6d4 = dComIfGd_setShadow(field_0x6d4, 1, mpMorf->getModel(), &pos, 800.0f, 40.0f,
                                     current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);

    if (field_0x6d4 != 0 && field_0x6d0 != NULL) {
        dComIfGd_addRealShadow(field_0x6d4, field_0x6d0);
    }
}

#include "d/d_snap.h"
/* 00003254-00003380       .text _draw__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::_draw() {

    J3DModel* model = mpMorf->getModel();
    J3DModelData* model_data = model->getModelData();

    if (field_0x75e || field_0x760) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);
    mBtpAnm.entry(model_data, field_0x6ec);
    mpMorf->entryDL();
    mBtpAnm.remove(model_data);

    if (field_0x6d0) {
        g_env_light.setLightTevColorType(field_0x6d0, &tevStr);
        mDoExt_modelEntryDL(field_0x6d0);
    }

    shadowDraw();

    if (l_HIO.field_0x24) {
        cXyz vec = current.pos;
        vec.y = eyePos.y;
        GXColor color1 = {0xff, 0x00, 0x00, 0x80};
        GXColor color2 = {0x00, 0x00, 0xff, 0x80};
        GXColor color3 = {0xff, 0xff, 0x00, 0x80};
    }

    dSnap_RegistFig(0x4c, this, 1.0f, 1.0f, 1.0f);
    return TRUE;
}

/* 00003380-000035A8       .text _execute__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::_execute() {
    if (field_0x761 == 0) {
        field_0x6fc = current.pos.x;
        field_0x700 = current.pos.y;
        field_0x704 = current.pos.z;
        field_0x708 = current.angle.x;
        field_0x70a = current.angle.y;
        field_0x70c = current.angle.z;
        field_0x761 = 1;
    }

    m_jnt.setParam(l_HIO.mMaxBackboneX, l_HIO.mMaxBackboneY, l_HIO.mMinBackboneX, l_HIO.mMinBackboneY,
        l_HIO.mMaxHeadX, l_HIO.mMaxHeadY, l_HIO.mMinHeadX, l_HIO.mMinHeadY, l_HIO.mMaxTurnStep);

    if (field_0x75e && demoActorID == 0) {
        return TRUE;
    }

    checkOrder();
    if (!demo()) {
        s32 cond = -1;
        if (dComIfGp_event_runCheck() && eventInfo.checkCommandTalk() == false) {
            cond = isEventEntry();
        }
        if (cond >= 0) {
            event_proc(cond);
        } else {
            (this->*mpAction)(NULL);
        }
        fopAcM_posMoveF(this, mStts.GetCCMoveP());
        play_animation();
        mObjAcch.CrrPos(*dComIfG_Bgsp());
    }

    eventOrder();
    field_0x70e = current.angle.x;
    field_0x710 = current.angle.y;
    field_0x712 = current.angle.z;
    if (field_0x75f == 0) {
        shape_angle.x = current.angle.x;
        shape_angle.y = current.angle.y;
        shape_angle.z = current.angle.z;
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);

    setMtx(false);
    flw_pa_pun();
    del_pa_aka();
    flw_pa_aka();
    setSmoke();

    if (field_0x76b == 0) {
        setCollision(60.0f, 140.0f);
    }

    return TRUE;
}

/* 000035A8-0000363C       .text _delete__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::_delete() {
    dComIfG_resDelete((request_of_phase_process_class*)&field_0x6c4, "Aj");

    if (heap != NULL) {
        if (mpMorf != NULL) {
            mpMorf->stopZelAnime();
        }
    }


    del_pa(&field_0x790);
    del_pa(&field_0x794);
    del_pa(&field_0x798);
    mSmokeCB.remove();

    return TRUE;
}

/* 0000363C-00003890       .text _create__11daNpc_Aj1_cFv */
cPhs_State daNpc_Aj1_c::_create() {
    fopAcM_ct(this, daNpc_Aj1_c);

    cPhs_State state = dComIfG_resLoad((request_of_phase_process_class*)&field_0x6c4, "Aj");
    if (state != cPhs_COMPLEATE_e) {
        return state;
    }

    if (!decideType(fopAcM_GetParam(this) & 0xFF)) {
        return cPhs_ERROR_e;
    }

    static u32 a_siz_tbl[] = { 0x0 };

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_siz_tbl[field_0x7be])) {
        return cPhs_ERROR_e;
    }

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -60.0f, -20.0f, -60.0f, 80.0f, 260.0f, 100.0f);

    if (!createInit()) {
        return cPhs_ERROR_e;
    }

    return state;
}

/* 00003B5C-00003E90       .text bodyCreateHeap__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::bodyCreateHeap() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Aj", dRes_ID_AJ_BDL_AJ_e);
    JUT_ASSERT(0x8be, a_mdl_dat != 0);

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
    JUT_ASSERT(0x8d3, m_hed_jnt_num >= 0);

    m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone");
    JUT_ASSERT(0x8d5, m_bbone_jnt_num >= 0);

    m_hnd_L_jnt_num = a_mdl_dat->getJointName()->getIndex("handL");
    JUT_ASSERT(0x8d7, m_hnd_L_jnt_num >= 0);

    m_fot_L_jnt_num = a_mdl_dat->getJointName()->getIndex("footL");
    JUT_ASSERT(0x8d9, m_fot_L_jnt_num >= 0);

    mpMorf->getModel()->getModelData()->getJointNodePointer(m_hed_jnt_num)->setCallBack(nodeCB_Head);
    mpMorf->getModel()->getModelData()->getJointNodePointer(m_bbone_jnt_num)->setCallBack(nodeCB_BackBone);
#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    return TRUE;
}

/* 00003E90-00003F58       .text itemCreateHeap__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::itemCreateHeap() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Aj", dRes_ID_AJ_BDL_AJ_STICK_e);
    JUT_ASSERT(0x8f3, a_mdl_dat != 0);

    field_0x6d0 = mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
    if (field_0x6d0 == NULL) {
        return FALSE;
    }

    return TRUE;
}

/* 00003F58-00003FFC       .text CreateHeap__11daNpc_Aj1_cFv */
BOOL daNpc_Aj1_c::CreateHeap() {
    if (!bodyCreateHeap()) {
        return FALSE;
    }

    if (!itemCreateHeap()) {
        mpMorf = NULL;
        return FALSE;
    }

    mAcchCir.SetWall(30.0f, 60.0f);
    mObjAcch.Set(&current.pos, &old.pos, this, 1, &mAcchCir, &speed, NULL, NULL);

    return TRUE;
}

/* 00003FFC-0000401C       .text daNpc_Aj1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Aj1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Aj1_c*)i_this)->_create();
}

/* 0000401C-0000403C       .text daNpc_Aj1_Delete__FP11daNpc_Aj1_c */
static BOOL daNpc_Aj1_Delete(daNpc_Aj1_c* i_this) {
    return ((daNpc_Aj1_c*)i_this)->_delete();
}

/* 0000403C-0000405C       .text daNpc_Aj1_Execute__FP11daNpc_Aj1_c */
static BOOL daNpc_Aj1_Execute(daNpc_Aj1_c* i_this) {
    return ((daNpc_Aj1_c*)i_this)->_execute();
}

/* 0000405C-0000407C       .text daNpc_Aj1_Draw__FP11daNpc_Aj1_c */
static BOOL daNpc_Aj1_Draw(daNpc_Aj1_c* i_this) {
    return ((daNpc_Aj1_c*)i_this)->_draw();
}

/* 0000407C-00004084       .text daNpc_Aj1_IsDelete__FP11daNpc_Aj1_c */
static BOOL daNpc_Aj1_IsDelete(daNpc_Aj1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Aj1_Method = {
    (process_method_func)daNpc_Aj1_Create,
    (process_method_func)daNpc_Aj1_Delete,
    (process_method_func)daNpc_Aj1_Execute,
    (process_method_func)daNpc_Aj1_IsDelete,
    (process_method_func)daNpc_Aj1_Draw,
};

actor_process_profile_definition g_profile_NPC_AJ1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_AJ1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Aj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_AJ1_e,
    /* Actor SubMtd */ &l_daNpc_Aj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
