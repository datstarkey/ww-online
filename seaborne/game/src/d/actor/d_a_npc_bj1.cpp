/**
 * d_a_npc_bj1.cpp
 * NPC - Generic Koroks
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/d_kankyo_wether.h"
#include "d/actor/d_a_npc_bj1.h"
#include "d/d_snap.h"

class daNpc_Bj1_childHIO_c : public mDoHIO_entry_c {
public:
    daNpc_Bj1_childHIO_c();

public:
    /* 0x04 */ s16 field_0x4;
    /* 0x06 */ s16 field_0x6;
    /* 0x08 */ s16 field_0x8;
    /* 0x0A */ s16 field_0xa;
    /* 0x0C */ s16 field_0xc;
    /* 0x0E */ s16 field_0xe;
    /* 0x10 */ s16 field_0x10;
    /* 0x12 */ s16 field_0x12;
    /* 0x14 */ s16 field_0x14;
    /* 0x16 */ s16 field_0x16;
    /* 0x18 */ f32 field_0x18;
    /* 0x1C */ u8 field_0x1c;
    /* 0x20 */ f32 field_0x20;
    /* 0x24 */ s16 field_0x24;
    /* 0x26 */ s16 field_0x26;
    /* 0x28 */ s16 field_0x28;
    /* 0x2A */ s16 field_0x2a;
    /* 0x2C */ f32 field_0x2c;
    /* 0x30 */ f32 field_0x30;
    /* 0x34 */ f32 field_0x34;
    /* 0x38 */ f32 field_0x38;
    /* 0x3C */ f32 field_0x3c;
    /* 0x40 */ f32 field_0x40;
    /* 0x44 */ s16 field_0x44;
    /* 0x46 */ s16 field_0x46;
    /* 0x48 */ s16 field_0x48;
    /* 0x4A */ s16 field_0x4a;
    /* 0x4C */ f32 field_0x4c;
    /* 0x50 */ s16 field_0x50;
    /* 0x52 */ s16 field_0x52;
    /* 0x54 */ s16 field_0x54;
    /* 0x56 */ s16 field_0x56;
    /* 0x58 */ f32 field_0x58;
    /* 0x5C */ f32 field_0x5c;
    /* 0x60 */ f32 field_0x60;
    /* 0x64 */ f32 field_0x64;
    /* 0x68 */ s32 field_0x68;
}; // Size: 0x6C

class daNpc_Bj1_HIO_c : public mDoHIO_entry_c {
public:
    daNpc_Bj1_HIO_c();
    virtual ~daNpc_Bj1_HIO_c() {}

public:
    /* 0x04 */ s8 mNo;
    /* 0x05 */ u8 field_0x5[0x8 - 0x5];
    /* 0x08 */ s32 field_0x8;
    /* 0x0C */ daNpc_Bj1_childHIO_c mChild[9];
}; // Size: 0x3D8

/* 000000EC-00000108       .text __ct__20daNpc_Bj1_childHIO_cFv */
daNpc_Bj1_childHIO_c::daNpc_Bj1_childHIO_c() {}

// Initial values for daNpc_Bj1_childHIO_c, copied over its 0x04..0x68 block.
struct daNpc_Bj1_prm_c {
    /* 0x00 */ s16 field_0x0;
    /* 0x02 */ s16 field_0x2;
    /* 0x04 */ s16 field_0x4;
    /* 0x06 */ s16 field_0x6;
    /* 0x08 */ s16 field_0x8;
    /* 0x0A */ s16 field_0xa;
    /* 0x0C */ s16 field_0xc;
    /* 0x0E */ s16 field_0xe;
    /* 0x10 */ s16 field_0x10;
    /* 0x12 */ s16 field_0x12;
    /* 0x14 */ f32 field_0x14;
    /* 0x18 */ u8 field_0x18;
    /* 0x1C */ f32 field_0x1c;
    /* 0x20 */ s16 field_0x20;
    /* 0x22 */ s16 field_0x22;
    /* 0x24 */ s16 field_0x24;
    /* 0x26 */ s16 field_0x26;
    /* 0x28 */ f32 field_0x28;
    /* 0x2C */ f32 field_0x2c;
    /* 0x30 */ f32 field_0x30;
    /* 0x34 */ f32 field_0x34;
    /* 0x38 */ f32 field_0x38;
    /* 0x3C */ f32 field_0x3c;
    /* 0x40 */ s16 field_0x40;
    /* 0x42 */ s16 field_0x42;
    /* 0x44 */ s16 field_0x44;
    /* 0x46 */ s16 field_0x46;
    /* 0x48 */ f32 field_0x48;
    /* 0x4C */ s16 field_0x4c;
    /* 0x4E */ s16 field_0x4e;
    /* 0x50 */ s16 field_0x50;
    /* 0x52 */ s16 field_0x52;
    /* 0x54 */ f32 field_0x54;
    /* 0x58 */ f32 field_0x58;
    /* 0x5C */ f32 field_0x5c;
    /* 0x60 */ f32 field_0x60;
}; // Size: 0x64

/* 00000150-0000020C       .text __ct__15daNpc_Bj1_HIO_cFv */
daNpc_Bj1_HIO_c::daNpc_Bj1_HIO_c() {
    static daNpc_Bj1_prm_c a_prm_tbl[] = {
        {5000, 5000, -8192, -5000, 0, 7280, 0, -7280, 1024, 1024, 130.0f, 0.0f, 300.0f},
        {5000, 5000, -700, -5000, 0, 7280, 0, -7280, 1024, 1024, 100.0f, 0.0f, 300.0f},
        {5000, 5000, -8192, -5000, 0, 7280, 0, -7280, 1024, 1024, 150.0f, 0.0f, 300.0f},
        {5000, 7000, -200, -2000, 0, 0, 0, 0, 1024, 1024, 100.0f, 0.0f, 300.0f, 90, 6, 1024, 0,
         4.0f, 5.0f, 0.2f, 4.0f, 0.2f, 1.0f, 9000, 200, 4096, 5000, 250.0f, 32767},
        {5000, 3000, 100, -3000, 0, 5000, 0, -5000, 1024, 1024, 110.0f, 0.0f, 300.0f},
        {5000, 5000, -100, -5000, 0, 2000, 0, -2000, 1024, 1024, 130.0f, 0.0f, 300.0f},
        {5000, 5000, -8192, -5000, 0, 7280, 0, -7280, 1024, 1024, 125.0f, 0.0f, 300.0f, 0, 0, 0, 0,
         0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0, 0, 0, 0, 0.0f, 0, 6, 1024, 0, 0.4f, 5.0f, 0.2f,
         40.0f},
        {5000, 4000, -2000, -4000, 0, 7280, 0, -7280, 1024, 1024, 120.0f, 0.0f, 300.0f},
        {5000, 5000, -3500, -5000, 0, 7280, 0, -7280, 1024, 1024, 130.0f, 0.0f, 300.0f},
    };

    for (int i = 0; i < 9; i++) {
        mChild[i].field_0x68 = i;
        memcpy(&mChild[i].field_0x4, &a_prm_tbl[i], sizeof(daNpc_Bj1_prm_c));
    }

    mNo = -1;
    field_0x8 = -1;
}

#include "d/actor/d_a_jbo.h"

static daNpc_Bj1_HIO_c l_HIO;
static fopAc_ac_c* l_check_inf[20];
static s32 l_check_wrk = 0;

/* 00000268-000002EC       .text searchActor_Jb__FPvPv */
static void* searchActor_Jb(void* i_actor, void* i_data) {
    if (l_check_wrk < 20) {
        if (fopAc_IsActor(i_actor)) {
            fopAc_ac_c* actor = (fopAc_ac_c*)i_actor;

            if (fopAcM_GetName(actor) == fpcNm_JBO_e) {
                if (((jbo_class*)actor)->mType != 0) {
                    l_check_inf[l_check_wrk] = actor;
                    l_check_wrk++;
                }
            }
        }
    }

    return NULL;
}

/* 000002EC-00000338       .text nodeCallBack_Bj1__FP7J3DNodei */
static BOOL nodeCallBack_Bj1(J3DNode* i_node, int i_param) {
    if (i_param == 0) {
        daNpc_Bj1_c* actor = (daNpc_Bj1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->nodeBj1Control(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 00000338-000004C4       .text nodeBj1Control__11daNpc_Bj1_cFP7J3DNodeP8J3DModel */
void daNpc_Bj1_c::nodeBj1Control(J3DNode* i_node, J3DModel* i_model) {
    static cXyz a_eye_pos_off(26.0f, 26.0f, 0.0f); // Never used.

    int jointIdx = ((J3DJoint*)i_node)->getJntNo();

    mDoMtx_stack_c::copy(i_model->getAnmMtx(jointIdx));

    if (jointIdx == (s8)m_hed_jnt_num) {
        mDoMtx_stack_c::XrotM(m_jnt.getHead_y());
        mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
        mDoMtx_stack_c::scaleM(field_0x7f0);
        mDoMtx_stack_c::multVec(&field_0x7e4, &field_0x7c0);
    }

    if (jointIdx == (s8)m_bbone_jnt_num) {
        mDoMtx_stack_c::XrotM(m_jnt.getBackbone_y());
        mDoMtx_stack_c::ZrotM(-m_jnt.getBackbone_x());
        mDoMtx_stack_c::scaleM(field_0x7fc);
    }

    if (jointIdx == (s8)m_armR2_jnt_num) {
        cMtx_copy(mDoMtx_stack_c::get(), field_0x704);
    }

    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_model->setAnmMtx(jointIdx, mDoMtx_stack_c::get());
}

/* 00000500-0000054C       .text nodeCallBack_Prp__FP7J3DNodei */
static BOOL nodeCallBack_Prp(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        daNpc_Bj1_c* actor = (daNpc_Bj1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->nodePrpControl(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 0000054C-00000664       .text nodePrpControl__11daNpc_Bj1_cFP7J3DNodeP8J3DModel */
void daNpc_Bj1_c::nodePrpControl(J3DNode* i_node, J3DModel* i_model) {
    int jointIdx = ((J3DJoint*)i_node)->getJntNo();

    if (jointIdx == m_stalk_jnt_num) {
        mDoMtx_stack_c::copy(field_0x704);
        mDoMtx_stack_c::transM(-1.5f, 4.5f, -4.2f);
        mDoMtx_stack_c::XYZrotM(-0x4000, -0x5555, 0);
        cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
        i_model->setAnmMtx(jointIdx, mDoMtx_stack_c::get());
    }

    if (jointIdx == m_cen_jnt_num) {
        mDoMtx_stack_c::YrotS(field_0x6f0);
        PSMTXConcat(i_model->getAnmMtx(jointIdx), mDoMtx_stack_c::get(), mDoMtx_stack_c::get());
        cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
        i_model->setAnmMtx(jointIdx, mDoMtx_stack_c::get());
    }
}

static char* l_evn_tbl[3] = {
    "Mak_Drg",
    "Get_Blu_Btl",
    "Calling",
};

/* 00000664-00000710       .text init_BJ4_0__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::init_BJ4_0() {
    u32 isSym = dComIfGs_isSymbol(2);
    u32 ret = isSym ? 0 : 1;
    if ((u8)ret) {
        u32 isEvt = dComIfGs_isEventBit(0x1801);
        ret = isEvt ? 1 : 0;
        if ((u8)ret) {
            set_action(&daNpc_Bj1_c::wait_action2, NULL);
        }
    }
    return ret;
}

/* 00000710-000007D4       .text init_BJ6_0__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::init_BJ6_0() {
    u32 isSym = dComIfGs_isSymbol(2);
    u32 ret = isSym ? 0 : 1;
    if ((u8)ret) {
        u32 isEvt = dComIfGs_isEventBit(0x1801);
        ret = isEvt ? 1 : 0;
        if ((u8)ret) {
            actor_status &= ~fopAcStts_NOCULLEXEC_e;
            actor_status |= fopAcStts_UNK4000_e;
            set_action(&daNpc_Bj1_c::wait_action4, NULL);
        }
    }
    return ret;
}

/* 000007D4-00000864       .text init_BJ7_0__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::init_BJ7_0() {
    u32 isEvt = dComIfGs_isEventBit(0x1801);
    u32 ret = isEvt ? 1 : 0;
    if ((u8)ret) {
        setPrtcl_drugPot_1();
        set_action(&daNpc_Bj1_c::wait_action3, NULL);
    }
    return ret;
}

/* 00000864-00000910       .text init_BJX_0__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::init_BJX_0() {
    u32 isSym = dComIfGs_isSymbol(2);
    u32 ret = isSym ? 0 : 1;
    if ((u8)ret) {
        u32 isEvt = dComIfGs_isEventBit(0x1801);
        ret = isEvt ? 1 : 0;
        if ((u8)ret) {
            set_action(&daNpc_Bj1_c::wait_action1, NULL);
        }
    }
    return ret;
}

/* 00000910-00000998       .text init_BJX_1__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::init_BJX_1() {
    u32 isSym = dComIfGs_isSymbol(2);
    u32 ret = isSym ? 1 : 0;
    if ((u8)ret) {
        set_action(&daNpc_Bj1_c::wait_action1, NULL);
    }
    return ret;
}

/* 00000998-00000C90       .text createInit__11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::createInit() {
    static Vec l_bj1_prm_tbl[9][3] = {
        {{1.0f, 1.0f, 1.0f}, {1.1f, 1.0f, 1.0f}, {40.0f, 10.0f, 0.0f}},
        {{0.9f, 1.0f, 1.0f}, {0.9f, 1.0f, 1.0f}, {30.0f, 10.0f, 0.0f}},
        {{1.1f, 1.0f, 1.0f}, {1.25f, 1.1f, 1.1f}, {40.0f, 10.0f, 0.0f}},
        {{0.8f, 1.0f, 1.0f}, {0.9f, 1.0f, 1.0f}, {30.0f, 10.0f, 0.0f}},
        {{0.8f, 1.0f, 1.0f}, {0.8f, 1.0f, 1.0f}, {30.0f, 10.0f, 0.0f}},
        {{1.0f, 1.0f, 1.0f}, {1.15f, 1.0f, 1.05f}, {40.0f, 10.0f, 0.0f}},
        {{1.0f, 1.0f, 1.0f}, {1.0f, 1.0f, 1.0f}, {40.0f, 10.0f, 0.0f}},
        {{1.0f, 1.0f, 1.0f}, {1.1f, 1.2f, 1.05f}, {40.0f, 10.0f, 0.0f}},
        {{1.1f, 1.0f, 1.0f}, {1.0f, 1.0f, 1.0f}, {40.0f, 10.0f, 0.0f}},
    };
    static char* a_staff_tbl[17] = {
        "Bj1", "Bj1", "Bj2", "Bj2", "Bj3", "Bj3", "Bj4", "Bj4", "Bj5",
        "Bj5", "Bj6", "Bj6", "Bj7", "Bj8", "Bj8", "Bj9", "Bj9",
    };
    for (int i = 0; i < 3; i++) {
        mEventIdx[i] = dComIfGp_evmng_getEventIdx(l_evn_tbl[i], 0xFF);
    }

    attention_info.flags = fopAc_Attn_LOCKON_TALK_e | fopAc_Attn_ACTION_SPEAK_e;

    switch (field_0x8b4) {
    case 4:
        attention_info.distances[1] = 0xa7;
        attention_info.distances[3] = 0xab;
        break;
    case 0:
    case 1:
    case 7:
        attention_info.distances[1] = 0xab;
        attention_info.distances[3] = 0xab;
        break;
    case 2:
    case 3:
    case 5:
    case 8:
        attention_info.distances[1] = 0xaa;
        attention_info.distances[3] = 0xaa;
        break;
    case 6:
        attention_info.distances[1] = 0x5a;
        attention_info.distances[3] = 0x5a;
        break;
    default:
        return false;
    }

    field_0x7f0 = l_bj1_prm_tbl[field_0x8b4][0];
    field_0x7fc = l_bj1_prm_tbl[field_0x8b4][1];
    field_0x7e4 = l_bj1_prm_tbl[field_0x8b4][2];

    gravity = -4.5f;
    field_0x7cc = current.pos;

#if TARGET_PC
    // fopAc_ac_c inherits the process base on the host (see f_op_actor.h).
    u8 params = mParameters >> 0x10;
#else
    u8 params = base.base.mParameters >> 0x10;
#endif
    field_0x83f[0] = params;
    if (params != 0xff) {
        mPathRun.setInf(params, current.roomNo, 1);
        if (mPathRun.getPath() != NULL) {
            fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
        } else {
            return false;
        }
    }

    mDemoEventCut.setActorInfo2(a_staff_tbl[field_0x8b5], this);
    field_0x8af = 0xb;

    BOOL control;
    switch (field_0x8b5) {
    case 6:
        control = init_BJ4_0();
        break;
    case 10:
        control = init_BJ6_0();
        break;
    case 0:
    case 2:
    case 4:
    case 8:
    case 13:
    case 15:
        control = init_BJX_0();
        break;
    case 1:
    case 3:
    case 5:
    case 7:
    case 9:
    case 11:
    case 14:
    case 16:
        control = init_BJX_1();
        break;
    case 12:
        control = init_BJ7_0();
        break;
    default:
        control = FALSE;
        break;
    }

    if ((u8)control) {
        shape_angle = current.angle;
    } else {
        return false;
    }

    mStts.Init(0xff, 0xff, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);
    mpMorf->setMorf(0.0f);
    setMtx(true);
    return true;
}

/* 00000C90-00000E00       .text setMtx_anmProc__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::setMtx_anmProc() {
    switch (field_0x8af) {
    case 4:
        if (field_0x8a9) {
            if (speed.y < 0.0f) {
                if (mpMorf->getFrame() < 7.0f) {
                    mpMorf->setFrame(mpMorf->getEndFrame());
                }
            } else {
                if (mpMorf->getFrame() >= 7.0f) {
                    mpMorf->setFrame(7.0f);
                }
            }
        }
        break;
    case 7:
        if (field_0x83c) {
            if (field_0x888 == NULL) {
                if (!cLib_calcTimer(&field_0x894)) {
                    JAIZelBasic::zel_basic->seStart(JA_SE_OBJ_CRG_DRUG_MADE, &current.pos, 0,
                                                     dComIfGp_getReverb(fopAcM_GetRoomNo(this)), 1.0f,
                                                     1.0f, -1.0f, -1.0f, 0);
                    setAnm_NUM(5);
                    setPrtcl_drugPot_2();
                }
            }
        }
        break;
    }
}

/* 00000E00-00001098       .text setMtx__11daNpc_Bj1_cFb */
void daNpc_Bj1_c::setMtx(bool i_setAttn) {
    u32 mtrlSndId;

    if (!field_0x857) {
        mtrlSndId = 0;
        if (mObjAcch.ChkGroundHit()) {
            mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(mObjAcch.m_gnd);
        }

        field_0x83c = mpMorf->play(&eyePos, mtrlSndId, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
        if (mpMorf->getFrame() < field_0x808) {
            field_0x83c = 1;
        }

        field_0x808 = mpMorf->getFrame();
        setMtx_anmProc();
        field_0x6e4->play(&eyePos, 0, 0);

        if (field_0x84d) {
            setPrtcl_peraProOpen();
            field_0x84d = false;
        }

        mObjAcch.CrrPos(*dComIfG_Bgsp());
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);

    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();
    field_0x6e4->calc();

    if (field_0x6d4) {
        mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_armL2_jnt_num));
        field_0x6d4->setBaseTRMtx(mDoMtx_stack_c::get());
        field_0x6d4->calc();
    }

    if (field_0x6d8) {
        mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_armR2_jnt_num));
        field_0x6d8->setBaseTRMtx(mDoMtx_stack_c::get());
        field_0x6d8->calc();
    }

    mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));
    field_0x6d0->setBaseTRMtx(mDoMtx_stack_c::get());
    field_0x6d0->calc();

    flwPrtcl_danceLR();
    setAttention(i_setAttn);
}

#include "res/Object/Bj.h"

/* 00001098-000010AC       .text anmNum_toResID__11daNpc_Bj1_cFi */
s32 daNpc_Bj1_c::anmNum_toResID(int i_num) {
    static const s32 a_bck_resID_tbl[] = {
        dRes_ID_BJ_BCK_WAIT01_e, dRes_ID_BJ_BCK_FLY01_e,  dRes_ID_BJ_BCK_TALK01_e,
        dRes_ID_BJ_BCK_TALK02_e, dRes_ID_BJ_BCK_HAPPY_e,  dRes_ID_BJ_BCK_SHOCK_e,
        dRes_ID_BJ_BCK_BOW_e,    dRes_ID_BJ_BCK_DANCE_e,  dRes_ID_BJ_BCK_WALK_e,
        dRes_ID_BJ_BCK_THROW_e,  dRes_ID_BJ_BCK_FLY02_e,
    };

    return a_bck_resID_tbl[i_num];
}

/* 000010AC-00001188       .text setAnm_anm__11daNpc_Bj1_cFPQ211daNpc_Bj1_c9anm_prm_c */
s32 daNpc_Bj1_c::setAnm_anm(daNpc_Bj1_c::anm_prm_c* i_prm) {
    if (field_0x8af == i_prm->field0) {
        return 1;
    }

    field_0x8af = i_prm->field0;
    s32 anm_res_id = anmNum_toResID(field_0x8af);
    dNpc_setAnmIDRes(mpMorf, i_prm->fieldC, i_prm->field4, i_prm->field8, anm_res_id, -1, "Bj");

    switch (field_0x8af) {
    case 7:
        setPrtcl_danceLR();
        field_0x888 = 0;
        field_0x894 = 30;
        break;
    default:
        delPrtcl_danceLR();
        break;
    }

    field_0x808 = 0.0f;
    field_0x83d = 0;
    field_0x83c = 0;
    return 1;
}

/* 00001188-000011B8       .text setAnm_NUM__11daNpc_Bj1_cFi */
void daNpc_Bj1_c::setAnm_NUM(int i_num) {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 8.0f, 1.0f, 2},
        {1, 0, 8.0f, 1.0f, 0},
        {2, 0, 8.0f, 1.0f, 2},
        {3, 0, 8.0f, 1.0f, 2},
        {4, 0, 8.0f, 1.0f, 2},
        {5, 0, 8.0f, 1.0f, 0},
        {6, 0, 8.0f, 1.0f, 0},
        {7, 0, 8.0f, 1.0f, 0},
        {8, 0, 8.0f, 1.0f, 2},
        {9, 0, 8.0f, 1.0f, 0},
        {10, 0, 8.0f, 1.0f, 2},
        {0, 0, 0.0f, 0.0f, 0},
    };

    setAnm_anm(&a_anm_prm_tbl[i_num]);
}

/* 000011B8-000011F4       .text setAnm__11daNpc_Bj1_cFv */
s32 daNpc_Bj1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[] = {
        {-1, 0, 0.0f, 0.0f, -1},
        {0, 0, 8.0f, 1.0f, 2},
        {-1, 0, 0.0f, 0.0f, -1},
        {1, 0, 8.0f, 1.0f, 0},
        {10, 0, 8.0f, 1.0f, 2},
        {0, 0, 8.0f, 1.0f, 2},
        {8, 0, 8.0f, 1.0f, 2},
        {0, 0, 8.0f, 1.0f, 2},
        {0, 0, 8.0f, 1.0f, 2},
    };

    setAnm_anm(&a_anm_prm_tbl[(s8)field_0x8b1]);
    return 1;
}


/* 000011F4-00001378       .text setAnm_prp__11daNpc_Bj1_cFSc */
void daNpc_Bj1_c::setAnm_prp(signed char i_type) {
    switch (i_type) {
    case 1: {
        J3DAnmTransform* anm = static_cast<J3DAnmTransform*>(dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BCK_OPEN_e));
        field_0x6e4->setAnm(anm, 0, 0.0f, 1.0f, 0.0f, -1.0f, NULL);
        fopAcM_seStartCurrent(this, 0x58c0, 0);
        field_0x84d = 1;
        break;
    }
    case 2: {
        J3DAnmTransform* anm = static_cast<J3DAnmTransform*>(dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BCK_OPEN_e));
        field_0x6e4->setAnm(anm, 2, 0.0f, -1.0f, 0.0f, -1.0f, NULL);
        field_0x6e4->setFrame(7.0f);
        fopAcM_seStartCurrent(this, 0x58c1, 0);
        break;
    }
    default:
        break;
    }
}

/* 00001378-0000137C       .text chg_anmTag__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::chg_anmTag() {
    /* Nonmatching */
}

/* 0000137C-00001380       .text control_anmTag__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::control_anmTag() {
    /* Nonmatching */
}

/* 00001380-000013C0       .text chg_anmAtr__11daNpc_Bj1_cFUc */
void daNpc_Bj1_c::chg_anmAtr(unsigned char i_param_1) {
    if (i_param_1 >= 0xb || i_param_1 == field_0x8ad) {
        return;
    }
    field_0x8ad = i_param_1;
    setAnm_ATR();
}

/* 000013C0-0000143C       .text control_anmAtr__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::control_anmAtr() {
    switch (field_0x8ad) {
    case 0:
    case 1:
    case 2:
    case 3:
    case 4:
    case 5:
        break;
    case 6:
    case 7:
        if (field_0x83c) {
            field_0x8ad = 0;
            setAnm_NUM(0);
        }
        break;
    case 10:
        if (field_0x83c) {
            field_0x8ad = 2;
            setAnm_NUM(3);
        }
        break;
    }
}

/* 0000143C-00001470       .text setAnm_ATR__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::setAnm_ATR() {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 8.0f, 1.0f, 2},
        {2, 0, 8.0f, 1.0f, 2},
        {3, 0, 8.0f, 1.0f, 2},
        {4, 0, 8.0f, 1.0f, 2},
        {5, 0, 8.0f, 1.0f, 0},
        {6, 0, 8.0f, 1.0f, 0},
        {6, 0, 8.0f, 1.0f, 0},
        {4, 0, 8.0f, 1.0f, 2},
        {7, 0, 8.0f, 1.0f, 0},
        {8, 0, 8.0f, 1.0f, 2},
        {5, 0, 8.0f, 1.0f, 0},
    };

    setAnm_anm(&a_anm_prm_tbl[field_0x8ad]);
}

/* 00001470-00001534       .text anmAtr__11daNpc_Bj1_cFUs */
void daNpc_Bj1_c::anmAtr(unsigned short i_msgStatus) {
    switch (i_msgStatus) {
    case fopMsgStts_MSG_TYPING_e:
        if (field_0x8b7 == 0) {
            field_0x8ad = 0xff;
            chg_anmAtr(dComIfGp_getMesgAnimeAttrInfo());
            field_0x8b7++;
        }
        {
            u8 tagInfo = dComIfGp_getMesgAnimeTagInfo();
            dComIfGp_clearMesgAnimeTagInfo();
            if (tagInfo != 0xff && field_0x8ae != tagInfo) {
                field_0x8ae = tagInfo;
                chg_anmTag();
            }
        }
        break;
    case fopMsgStts_MSG_DISPLAYED_e:
        field_0x8b7 = 0;
        break;
    default:
        break;
    }
    control_anmTag();
    control_anmAtr();
}

/* 00001534-000015D8       .text eventOrder__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::eventOrder() {
    if (field_0x8b0 == 1 || field_0x8b0 == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x8b5 == 0xc) {
            eventInfo.onCondition(dEvtCnd_CANTALKITEM_e);
        }
        if (field_0x8b0 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (field_0x8b0 >= 3) {
        field_0x82c = field_0x8b0 - 3;
        fopAcM_orderOtherEventId(this, mEventIdx[field_0x82c]);
    }
}

/* 000015D8-00001690       .text checkOrder__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::checkOrder() {
    if (eventInfo.checkCommandDemoAccrpt()) {
        if (dComIfGp_evmng_startCheck(mEventIdx[field_0x82c])) {
            switch (field_0x82c) {
            case 1:
                break;
            case 2:
                actor_status &= ~fopAcStts_UNK4000_e;
                break;
            }
            field_0x8b0 = 0;
        }
    } else if (eventInfo.checkCommandTalk() && (field_0x8b0 == 1 || field_0x8b0 == 2)) {
        field_0x8b0 = 0;
        field_0x855 = 1;
    }
}

/* 00001690-0000172C       .text chk_talk__11daNpc_Bj1_cFv */
u8 daNpc_Bj1_c::chk_talk() {
    u8 ret = 1;
    field_0x83e = 0xFF;
    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x83e = dComIfGp_event_getPreItemNo();
        } else {
            ret = 0;
        }
    }
    return ret;
}

/* 0000172C-000017C0       .text chk_drct__11daNpc_Bj1_cFf */
bool daNpc_Bj1_c::chk_drct(f32 i_param_1) {
    s16 target = cLib_targetAngleY(
        &current.pos,
        &dComIfGp_getPlayer(0)->current.pos
    ) - current.angle.y;
    int cmp = abs(target);
    return cmp < cM_deg2s(i_param_1);
}

/* 000017C0-000017F0       .text chk_partsNotMove__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::chk_partsNotMove() {
    BOOL ret = FALSE;

    if (field_0x822 != m_jnt.getBackbone_y() || field_0x820 != m_jnt.getHead_y()) {
        ret = TRUE;
    }

    return ret;
}

/* 000017F0-000019A0       .text lookBack__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::lookBack() {
    cXyz dst_pos;
    cXyz src_pos;
    cXyz* dst_pos_p;
    s16 desired_y;
    bool head_only;

    field_0x820 = m_jnt.getHead_y();
    field_0x822 = m_jnt.getBackbone_y();
    field_0x824 = current.angle.y;
    src_pos = current.pos;
    src_pos.y = eyePos.y;
    dst_pos.set(0.0f, 0.0f, 0.0f);
    dst_pos_p = NULL;
    desired_y = current.angle.y;
    head_only = field_0x856;

    switch (field_0x8b3) {
    case 1:
        dst_pos = dNpc_playerEyePos(-20.0f);
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 2:
        dst_pos = field_0x7cc;
        dst_pos_p = &dst_pos;
        src_pos = current.pos;
        src_pos.y = eyePos.y;
        break;
    case 3:
        desired_y = field_0x83a;
        break;
    }

    cLib_addCalcAngleS2(&field_0x838, l_HIO.mChild[field_0x8b4].field_0x16, 4, 0x800);
    if (!m_jnt.trnChk()) {
        field_0x838 = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dst_pos_p, src_pos, desired_y, field_0x838, head_only);
}

/* 000019A0-00001A44       .text getMaskInf__11daNpc_Bj1_cFPUc */
bool daNpc_Bj1_c::getMaskInf(unsigned char* o_mask) {
    bool ret = o_mask != NULL;
    if (ret) {
        switch (field_0x8b5) {
        case 1:
            *o_mask = 1;
            break;
        case 3:
            *o_mask = 2;
            break;
        case 5:
            *o_mask = 4;
            break;
        case 7:
            *o_mask = 8;
            break;
        case 9:
            *o_mask = 0x10;
            break;
        case 11:
            *o_mask = 0x20;
            break;
        case 14:
            *o_mask = 0x40;
            break;
        case 16:
            *o_mask = 0x80;
            break;
        default:
            ret = 0;
            break;
        }
    }
    return ret;
}

/* 00001A44-00001AA8       .text chkReg__11daNpc_Bj1_cFUs */
bool daNpc_Bj1_c::chkReg(u16 i_reg) {
    u8 mask;
    bool ret = getMaskInf(&mask);

    if (ret) {
        u8 reg = dComIfGs_getEventReg(i_reg);
        ret = reg & mask;
    }

    return ret;
}

/* 00001AA8-00001B14       .text setReg__11daNpc_Bj1_cFUs */
void daNpc_Bj1_c::setReg(u16 i_reg) {
    u8 mask;

    if (getMaskInf(&mask)) {
        dSv_event_c& event = g_dComIfG_gameInfo.save.getEvent();
        u8 reg = event.getEventReg(i_reg);
        reg |= mask;
        event.setEventReg(i_reg, reg);
    }
}

/* 00001B14-00001E84       .text next_msgStatus__11daNpc_Bj1_cFPUl */
u16 daNpc_Bj1_c::next_msgStatus(u32* pMsgNo) {
    u16 msgStatus = fopMsgStts_MSG_CONTINUES_e;
    switch (*pMsgNo) {
    case 0x13ED:
        *pMsgNo = 0x13EE;
        break;
    case 0x13EE:
        *pMsgNo = 0x13EF;
        break;
    case 0x13F0:
        *pMsgNo = 0x13F1;
        break;
    case 0x13F2:
        *pMsgNo = 0x13F3;
        break;
    case 0x13F5:
        *pMsgNo = 0x13F6;
        break;
    case 0x13F8:
        *pMsgNo = 0x13F9;
        break;
    case 0x13FB:
        *pMsgNo = 0x13FC;
        break;
    case 0x13FD:
        *pMsgNo = 0x13FE;
        break;
    case 0x13FF:
        *pMsgNo = 0x1400;
        break;
    case 0x1400:
        *pMsgNo = 0x1401;
        break;
    case 0x1402:
        *pMsgNo = 0x1403;
        break;
    case 0x1403:
        *pMsgNo = 0x1404;
        break;
    case 0x1406:
        *pMsgNo = 0x1407;
        break;
    case 0x1407:
        *pMsgNo = 0x1408;
        break;
    case 0x140B:
        *pMsgNo = 0x140C;
        break;
    case 0x140E:
        *pMsgNo = 0x140F;
        break;
    case 0x1412:
        *pMsgNo = 0x1413;
        break;
    case 0x1414:
        *pMsgNo = 0x1415;
        break;
    case 0x1416:
        *pMsgNo = 0x1417;
        break;
    case 0x1417:
        *pMsgNo = 0x1418;
        break;
    case 0x1418:
        *pMsgNo = 0x1419;
        break;
    case 0x1419:
        *pMsgNo = 0x141A;
        break;
    case 0x141A:
        *pMsgNo = 0x141B;
        break;
    case 0x141B:
        *pMsgNo = 0x141C;
        break;
    case 0x141C:
        *pMsgNo = 0x141D;
        break;
    case 0x141F:
        *pMsgNo = 0x1420;
        break;
    case 0x142C:
        *pMsgNo = 0x142D;
        break;
    case 0x1431:
        *pMsgNo = 0x1432;
        break;
    case 0x1433:
        *pMsgNo = 0x1417;
        break;
    case 0x1422:
        *pMsgNo = 0x1423;
        break;
    case 0x1423:
        dComIfGs_onEventBit(0x1B80);
        // fallthrough
    case 0x1424:
        if (dComIfGs_getBeastNum(1) < 4) {
            if (field_0x83f[1]) {
                *pMsgNo = 0x1427;
            } else {
                *pMsgNo = 0x1425;
            }
        } else {
            if (dComIfGs_checkEmptyBottle()) {
                *pMsgNo = 0x1428;
            } else {
                if (field_0x83f[2]) {
                    *pMsgNo = 0x1430;
                } else {
                    *pMsgNo = 0x142E;
                }
            }
        }
        break;
    case 0x1425:
        *pMsgNo = 0x1426;
        break;
    case 0x1428:
        *pMsgNo = 0x1429;
        break;
    case 0x142E:
        *pMsgNo = 0x142F;
        break;
    case 0x1487:
        *pMsgNo = 0x1488;
        break;
    case 0x1488:
        *pMsgNo = 0x1489;
        break;
    case 0x1482:
        *pMsgNo = 0x1483;
        break;
    case 0x148A:
        *pMsgNo = 0x148B;
        break;
    case 0x148B:
        *pMsgNo = 0x148C;
        break;
    case 0x148C:
        *pMsgNo = 0x148D;
        break;
    case 0x148D:
        *pMsgNo = 0x148E;
        break;
    case 0x148E:
        *pMsgNo = 0x148F;
        break;
    case 0x148F:
        *pMsgNo = 0x1490;
        break;
    case 0x1490:
        *pMsgNo = 0x1491;
        break;
    case 0x1491:
        *pMsgNo = 0x1492;
        break;
    case 0x1492:
        *pMsgNo = 0x1493;
        break;
    case 0x1497:
        *pMsgNo = 0x1498;
        break;
    case 0x1498:
        *pMsgNo = 0x1499;
        break;
    case 0x1499:
        *pMsgNo = 0x149A;
        break;
    case 0x149A:
        *pMsgNo = 0x149B;
        break;
    case 0x149B:
        *pMsgNo = 0x149C;
        break;
    case 0x149C:
        *pMsgNo = 0x149D;
        break;
    case 0x1494:
        *pMsgNo = 0x1495;
        break;
    case 0x1495:
        *pMsgNo = 0x1496;
        break;
    default:
        msgStatus = fopMsgStts_MSG_ENDS_e;
        break;
    }
    return msgStatus;
}

/* 00001E84-00001F14       .text getMsg_BJ1_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ1_0() {
    if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_0604)) {
        return 0x140E;
    }

    if (dKyw_get_windsdir() == 1) {
        if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0C80)) {
            return 0x1414;
        } else {
            return 0x1412;
        }
    }

    return dComIfGs_isEventBit(dSv_event_flag_c::UNK_0904) ? 0x1411 : 0x1410;
}

/* 00001F14-00001F54       .text getMsg_BJ2_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ2_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0920)) {
        return 0x13f4;
    }

    return 0x13f2;
}

/* 00001F54-00001FA8       .text getMsg_BJ3_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ3_0() {
    if (field_0x84b) {
        return 0x13FB;
    }

    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0C20)) {
        return 0x13FA;
    } else {
        return 0x13F8;
    }
}

/* 00001FA8-00001FE8       .text getMsg_BJ4_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ4_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0C08)) {
        return 0x13FF;
    }

    return 0x13FD;
}

/* 00001FE8-00002028       .text getMsg_BJ5_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ5_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0C04)) {
        return 0x13f7;
    }

    return 0x13f5;
}

/* 00002028-00002068       .text getMsg_BJ6_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ6_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0C02)) {
        return 0x1405;
    }

    return 0x1402;
}

/* 00002068-000021C4       .text getMsg_BJ7_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ7_0() {
    if (field_0x842) {
        field_0x842 = FALSE;
        return 0x142b;
    }
    if (field_0x843) {
        field_0x843 = FALSE;
        return 0x142c;
    }
    if (field_0x83e == 0x46) {
        return dComIfGs_isEventBit(0x1b80) ? 0x1424 : 0x1422;
    }
    if (field_0x83e != 0xff) {
        return 0x1421;
    }
    if (dComIfGs_isSymbol(2)) {
        if (!dComIfGs_isEventBit(0xd08)) {
            return 0x1433;
        }
        if (dComIfGs_isEventBit(0x1c80)) {
            return dComIfGs_isEventBit(0x1b80) ? 0x141e : 0x141f;
        }
        return 0x1431;
    }
    if (dComIfGs_isEventBit(0xd08)) {
        return dComIfGs_isEventBit(0x1b80) ? 0x141e : 0x141f;
    }
    return 0x1416;
}

#include "d/d_kankyo_wether.h"

/* 000021C4-00002254       .text getMsg_BJ8_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ8_0() {
    if (!dComIfGs_isEventBit(0xD40)) {
        return 0x1406;
    }

    if (dKyw_get_windsdir() == 7) {
        if (dComIfGs_isEventBit(0xD10)) {
            return 0x140D;
        } else {
            return 0x140B;
        }
    }

    return dComIfGs_isEventBit(0xD20) ? 0x140A : 0x1409;
}

/* 00002254-00002294       .text getMsg_BJ9_0__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_BJ9_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0C10)) {
        return 0x13f0;
    }

    return 0x13ed;
}

/* 00002294-0000242C       .text getMsg_Corog__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg_Corog() {
    if (chkReg(0x9AFF)) {
        if (dComIfGs_isEventBit(0x102)) {
            return 0x1485 + (chkReg(0x99FF) ? 1 : 0);
        }
        if (!chkReg(0x9EFF)) {
            if (chkReg(0x98FF)) {
                return 0x149e;
            }
            return 0x1487;
        }
        if (chkReg(0x96FF)) {
            return 0x1484;
        }
        u8 reg = dComIfGs_getEventReg(0x9EFF);
        s16 count = 0;
        for (int i = 0; i < 8; i++) {
            if (reg & 1) {
                count++;
            }
            reg = reg >> 1;
        }
        dComIfGp_setMessageCountNumber(8 - count);
        return 0x1482;
    }
    if (dComIfGs_isEventBit(0x2e10)) {
        if (chkReg(0x97FF)) {
            return 0x1494;
        }
        return 0x1497;
    }
    if (chkReg(0x97FF)) {
        return 0x1494;
    }
    return 0x148a;
}

/* 0000242C-000024EC       .text getMsg__11daNpc_Bj1_cFv */
u32 daNpc_Bj1_c::getMsg() {
    u32 msgNo = 0;
    switch (field_0x8b5) {
        case 0:
            msgNo = getMsg_BJ1_0();
            break;
        case 2:
            msgNo = getMsg_BJ2_0();
            break;
        case 4:
            msgNo = getMsg_BJ3_0();
            break;
        case 6:
            msgNo = getMsg_BJ4_0();
            break;
        case 8:
            msgNo = getMsg_BJ5_0();
            break;
        case 10:
            msgNo = getMsg_BJ6_0();
            break;
        case 12:
            msgNo = getMsg_BJ7_0();
            break;
        case 13:
            msgNo = getMsg_BJ8_0();
            break;
        case 15:
            msgNo = getMsg_BJ9_0();
            break;
        case 1:
        case 3:
        case 5:
        case 7:
        case 9:
        case 11:
        case 14:
        case 16:
            msgNo = getMsg_Corog();
            break;
    }
    return msgNo;
}

/* 000024EC-0000256C       .text chkAttention__11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::chkAttention() {
    dAttention_c& attention = dComIfGp_getAttention();
    if (attention.LockonTruth()) {
        return this == attention.LockonTarget(0);
    } else {
        return this == attention.ActionTarget(0);
    }
}


/* 0000256C-000025D4       .text setAttention__11daNpc_Bj1_cFb */
void daNpc_Bj1_c::setAttention(bool i_flag) {
    attention_info.position.set(current.pos.x,
                                current.pos.y + l_HIO.mChild[field_0x8b4].field_0x18,
                                current.pos.z);

    if (field_0x850 != 0 || i_flag) {
        eyePos.set(field_0x7c0.x, field_0x7c0.y, field_0x7c0.z);
    }
}

/* 000025D4-00002608       .text searchByID__11daNpc_Bj1_cFUi */
fopAc_ac_c* daNpc_Bj1_c::searchByID(fpc_ProcID i_id) {
    fopAc_ac_c* ret = NULL;
    fopAcM_SearchByID(i_id, &ret);
    return ret;
}

/* 00002608-000026B8       .text partner_srch_sub__11daNpc_Bj1_cFPFPvPv_Pv */
bool daNpc_Bj1_c::partner_srch_sub(fpcLyIt_JudgeFunc i_searchFunc) {
    bool o_retval = false;
    field_0x7a8 = -1;
    l_check_wrk = 0;
    for (int i = 0; i < 20; i++) {
        l_check_inf[i] = 0;
    }
    fpcM_Search(i_searchFunc, this);
    if (l_check_wrk != 0) {
        field_0x7a8 = fopAcM_GetID(l_check_inf[0]);
        o_retval = true;
    }
    return o_retval;
}

/* 000026B8-00002724       .text partner_srch__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::partner_srch() {
    bool found = false;

    if (field_0x8b6 == 1) {
        switch (field_0x8b5) {
        case 4:
            found = partner_srch_sub(searchActor_Jb);
            break;
        }

        if (found) {
            field_0x8b6++;
        }
    }
}

/* 00002724-000027B0       .text setCollision_SP___11daNpc_Bj1_cFv */
void daNpc_Bj1_c::setCollision_SP_() {
    static struct {
        f32 field_0x0;
        f32 field_0x4;
    } l_chk_tbl[9] = {
        { 130.0f, 40.0f },
        { 100.0f, 40.0f },
        { 150.0f, 40.0f },
        { 100.0f, 40.0f },
        { 100.0f, 40.0f },
        { 130.0f, 40.0f },
        { 130.0f, 40.0f },
        { 130.0f, 40.0f },
        { 130.0f, 40.0f },
    };

    mCyl.SetC(current.pos);
    mCyl.SetR(l_chk_tbl[field_0x8b4].field_0x4);
    mCyl.SetH(l_chk_tbl[field_0x8b4].field_0x0);
    dComIfG_Ccsp()->Set(&mCyl);
}

/* 000027B0-00002840       .text set_pthPoint__11daNpc_Bj1_cFUc */
void daNpc_Bj1_c::set_pthPoint(unsigned char i_idx) {
    if (mPathRun.isPath()) {
        u8 idx = cLib_maxLimit(i_idx, mPathRun.maxPoint());
        mPathRun.setIdx(idx);
        current.pos = mPathRun.getPoint(idx);
        mPathRun.nextIdxAuto();
    }
}

/* 00002840-00002988       .text bj_clcFlySpd__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::bj_clcFlySpd() {
    switch (field_0x8a8) {
    case 3:
        break;
    case 1: {
        s16 angleY = cLib_targetAngleY(&current.pos, &field_0x7d8);
        cLib_addCalcAngleS(&current.angle.y, angleY, l_HIO.mChild[field_0x8b4].field_0x26, l_HIO.mChild[field_0x8b4].field_0x28, 0);
        field_0x848 = field_0x7d8.y - current.pos.y > l_HIO.mChild[field_0x8b4].field_0x2c;
        f32 spd = l_HIO.mChild[field_0x8b4].field_0x30;
        if (!field_0x848) {
            spd *= -1.0f;
        }
        cLib_chaseF(&speed.y, spd, l_HIO.mChild[field_0x8b4].field_0x34);
        break;
    }
    case 2:
        cLib_chaseF(&speed.y, -l_HIO.mChild[field_0x8b4].field_0x30, -l_HIO.mChild[field_0x8b4].field_0x34);
        break;
    }
    cLib_chaseF(&speedF, field_0x80c, field_0x814);
}

/* 00002988-00002B80       .text bj_movPass__11daNpc_Bj1_cFb */
u32 daNpc_Bj1_c::bj_movPass(bool i_bMove) {
    u8 idx;
    u32 ret = 0;
    dPath* path = mPathRun.getPath();
    if (path != NULL) {
        idx = mPathRun.getIdx();
        if (dPath_ChkClose(path)) {
            if (mPathRun.chkPointPass(current.pos, mPathRun.getDir())) {
                mPathRun.nextIdxAuto();
                ret = 1;
                if (i_bMove) {
                    goto getPointBlock;
                }
            }
            return ret;
        }
    }

#if TARGET_PC
    // The goto above jumps past this declaration; clang rejects jumping past an initializer.
    f32 dist;
    dist = (field_0x7d8 - current.pos).absXZ();
#else
    f32 dist = (field_0x7d8 - current.pos).absXZ();
#endif
    if (dist <= field_0x81c) {
        ret = 1;
        if (mPathRun.getPath() != NULL) {
            if (!mPathRun.nextIdxAuto()) {
                ret = 2;
            }
        }
        if (i_bMove) {
            goto getPointBlock;
        }
    }
    return ret;

getPointBlock:
    u8 nowIdx = mPathRun.getIdx();
    cXyz p2;
    cXyz p1 = mPathRun.getPoint(nowIdx);
    p2 = mPathRun.getPoint(idx);
    field_0x848 = p2.y <= p1.y;
    return ret;
}

/* 00002B80-00002EDC       .text bj_flyMove__11daNpc_Bj1_cFv */
u8 daNpc_Bj1_c::bj_flyMove() {
    if (!field_0x8a8) {
        return FALSE;
    }

    if (field_0x849) {
        field_0x847 = 1;
        return TRUE;
    }

    if (mPathRun.isPath()) {
        field_0x7d8 = mPathRun.getPoint(mPathRun.getIdx());
    }

    if (field_0x8a8 == 1 || field_0x8a8 == 2 || field_0x8a8 == 7) {
        s16 limit = l_HIO.mChild[field_0x8b4].field_0x44;
        s16 val = field_0x6f2 + l_HIO.mChild[field_0x8b4].field_0x46;
        if (val > limit) {
            val = limit;
        }
        field_0x6f2 = val;
    }

    switch (field_0x8a8) {
    case 1:
        bj_clcFlySpd();
        switch (bj_movPass(true)) {
        case 1:
        case 2:
            field_0x849 = 1;
            break;
        }
        break;
    case 2: {
        bj_clcFlySpd();
        f32 diff = current.pos.y - mObjAcch.GetGroundH();
        field_0x849 = diff < l_HIO.mChild[field_0x8b4].field_0x40;
        if (field_0x849) {
            setAnm_NUM(1);
            speed.y = 0.0f;
            field_0x8a8 = 3;
        }
        break;
    }
    case 6:
        field_0x849 = field_0x6e4->isStop() != FALSE;
        if (field_0x849) {
            field_0x8a8 = 7;
        }
        break;
    case 7:
        field_0x849 = field_0x6f2 > l_HIO.mChild[field_0x8b4].field_0x4a;
        if (field_0x849) {
            setAnm_NUM(10);
            field_0x82e = l_HIO.mChild[field_0x8b4].field_0x24;
            field_0x8a8 = 1;
        }
        break;
    case 3: {
        s16 diff = field_0x6f2 - l_HIO.mChild[field_0x8b4].field_0x46;
        field_0x6f2 = diff < 0 ? 0 : diff;
        field_0x849 = field_0x6f2 == 0;
        if (field_0x849) {
            setAnm_prp(2);
            field_0x8a8 = 4;
        }
        break;
    }
    case 4:
        field_0x849 = field_0x6e4->checkFrame(0.0f) != FALSE;
        if (field_0x849) {
            field_0x84c = 0;
            gravity = -4.5f;
            field_0x8a8 = 5;
        }
        break;
    case 5:
        field_0x849 = mObjAcch.ChkGroundHit();
        if (field_0x849) {
            field_0x8a8 = 0;
        }
        break;
    }

    return TRUE;
}

/* 00002EDC-00002F5C       .text bj_clcMovSpd__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::bj_clcMovSpd() {
    if (field_0x8a9 == 0) {
        s16 target = cLib_targetAngleY(&current.pos, &field_0x7d8);
        cLib_addCalcAngleS(&current.angle.y, target, l_HIO.mChild[field_0x8b4].field_0x52,
                           l_HIO.mChild[field_0x8b4].field_0x54, 0);
    }
    cLib_chaseF(&speedF, field_0x80c, field_0x814);
}

/* 00002F5C-000030A4       .text bj_nMove__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::bj_nMove() {
    if (field_0x84a) {
        field_0x847 = 1;
        return;
    }

    switch (field_0x8a9) {
    case 1:
        field_0x8a9 = 2;
        break;
    case 2:
        if (mObjAcch.ChkGroundHit()) {
            setAnm_NUM(8);
            speedF = 0.0f;
            gravity = -4.5f;
            field_0x8a9 = 0;
        }
        break;
    default:
        switch (field_0x8aa) {
        case 1:
            if (field_0x8a9 == 0) {
                bj_clcMovSpd();
                f32 spd = speedF * l_HIO.mChild[field_0x8b4].field_0x58;
                if (spd < 0.5f) {
                    spd = 0.5f;
                }
                mpMorf->setPlaySpeed(spd);

                switch (bj_movPass(false)) {
                case 1:
                    field_0x84a = 1;
                    break;
                case 2:
                    field_0x84a = 1;
                    field_0x8aa = 0;
                    break;
                }
            }
            break;
        }
        break;
    }
}

/* 000030A4-000031F4       .text setPrtcl_drugPot_1__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::setPrtcl_drugPot_1() {
    f32 transX, transZ;
    s16 angleY;

    if (dComIfGp_getMapTrans(current.roomNo, &transX, &transZ, &angleY)) {
        field_0x858.set(0, angleY, 0);
        field_0x860.set(transX, 0.0f, transZ);

        field_0x87c = dComIfGp_particle_set(dPa_name::ID_AK_SN_DRUGPOT00, &field_0x860, &field_0x858, NULL, 0xFF, NULL, fopAcM_GetRoomNo(this));
        field_0x880 = dComIfGp_particle_set(dPa_name::ID_AK_SN_DRUGPOT01, &field_0x860, &field_0x858, NULL, 0xFF, NULL, fopAcM_GetRoomNo(this));
        field_0x884 = dComIfGp_particle_set(dPa_name::ID_AK_SN_DRUGPOT02, &field_0x860, &field_0x858, NULL, 0xFF, NULL, fopAcM_GetRoomNo(this));
    }
}

/* 000031F4-000032B4       .text setPrtcl_drugPot_2__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::setPrtcl_drugPot_2() {
    f32 transX, transZ;
    s16 angleY;

    if (dComIfGp_getMapTrans(current.roomNo, &transX, &transZ, &angleY)) {
        field_0x858.set(0, angleY, 0);
        field_0x860.set(transX, 0.0f, transZ);

        field_0x888 = dComIfGp_particle_set(0x822D, &field_0x860, &field_0x858, NULL, 0xFF, NULL, fopAcM_GetRoomNo(this));
    }
}

/* 000032B4-00003330       .text delPrtcl_drugPot__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::delPrtcl_drugPot() {
    if (field_0x87c != NULL) {
        field_0x87c->becomeInvalidEmitter();
        field_0x87c = NULL;
    }
    if (field_0x880 != NULL) {
        field_0x880->becomeInvalidEmitter();
        field_0x880 = NULL;
    }
    if (field_0x884 != NULL) {
        field_0x884->becomeInvalidEmitter();
        field_0x884 = NULL;
    }
}

/* 00003330-000033F4       .text setPrtcl_danceLR__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::setPrtcl_danceLR() {
    delPrtcl_danceLR();
    field_0x88c = dComIfGp_particle_set(dPa_name::ID_AK_SN_NIYATODANCEL00, &current.pos, NULL, NULL, 0xFF, NULL, fopAcM_GetRoomNo(this));
    field_0x890 = dComIfGp_particle_set(dPa_name::ID_AK_SN_NIYATODANCER00, &current.pos, NULL, NULL, 0xFF, NULL, fopAcM_GetRoomNo(this));
}

/* 000033F4-00003484       .text flwPrtcl_danceLR__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::flwPrtcl_danceLR() {
    if (field_0x88c != NULL) {
        field_0x88c->setGlobalRTMatrix(mpMorf->getModel()->getAnmMtx(m_armL2_jnt_num));
    }
    if (field_0x890 != NULL) {
        field_0x890->setGlobalRTMatrix(mpMorf->getModel()->getAnmMtx(m_armR2_jnt_num));
    }
}

/* 00003484-000034D8       .text delPrtcl_danceLR__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::delPrtcl_danceLR() {
    if (field_0x88c != NULL) {
        field_0x88c->becomeInvalidEmitter();
        field_0x88c = NULL;
    }

    if (field_0x890 != NULL) {
        field_0x890->becomeInvalidEmitter();
        field_0x890 = NULL;
    }
}

/* 000034D8-00003594       .text setPrtcl_peraProOpen__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::setPrtcl_peraProOpen() {
    MTXCopy(field_0x6e4->getModel()->getAnmMtx(m_cen_jnt_num), mDoMtx_stack_c::now);

    field_0x86c.x = mDoMtx_stack_c::now[0][3];
    field_0x86c.y = mDoMtx_stack_c::now[1][3];
    field_0x86c.z = mDoMtx_stack_c::now[2][3];

    field_0x878 = dComIfGp_particle_set(0x830B, &field_0x86c, &current.angle, NULL, 0xFF, NULL, fopAcM_GetRoomNo(this));
}

/* 00003594-00003774       .text createSeed__11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::createSeed() {
    fopAc_ac_c* itemActor[4];
    s32 i;
    for (i = 0; i < 4; i++) {
        field_0x898[i] = -1;
        itemActor[i] = NULL;
    }

    MTXCopy(mpMorf->getModel()->getAnmMtx(m_armR2_jnt_num), mDoMtx_stack_c::now);
    cXyz pos;
    pos.x = mDoMtx_stack_c::now[0][3];
    pos.y = mDoMtx_stack_c::now[1][3];
    pos.z = mDoMtx_stack_c::now[2][3];

    for (i = 0; i < 4; i++) {
        s16 angleOffsetTbl[4] = { -0x1000, -0xA00, 0xA00, 0x1000 };
        csXyz normal(0, 0, 0);
        cXyz scale(0.2f, 0.2f, 0.2f);
        normal.y = current.angle.y + angleOffsetTbl[i];
        itemActor[i] = fopAcM_createItemForKP2(&pos, 0x46, fopAcM_GetRoomNo(this), &normal, &scale, 10.0f + cM_rndFX(1.0f),
                                                14.0f + cM_rndFX(4.0f), -2.0f, 1);
        if (itemActor[i] == NULL) {
            break;
        }
        itemActor[i]->actor_status |= fopAcStts_UNK4000_e;
        field_0x898[i] = fopAcM_GetID(itemActor[i]);
    }
    return i == 4;
}

/* 00003774-000037E4       .text deleteSeed__11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::deleteSeed() {
    for (int i = 0; i < 4; i++) {
        fopAc_ac_c* seed = searchByID(field_0x898[i]);

        if (seed != NULL) {
            field_0x898[i] = fpcM_ERROR_PROCESS_ID_e;
            fopAcM_delete(seed);
        }
    }

    return true;
}

/* 000037E4-00003998       .text charDecide__11daNpc_Bj1_cFi */
bool daNpc_Bj1_c::charDecide(int i_param_1) {
    field_0x8b4 = -1;
    field_0x8b5 = -1;

    switch (fopAcM_GetName(this)) {
        case fpcNm_NPC_BJ1_e:
            field_0x8b4 = 0;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 1;
                    break;
                default:
                    field_0x8b5 = 0;
                    break;
            }
            break;
        case fpcNm_NPC_BJ2_e:
            field_0x8b4 = 1;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 3;
                    break;
                default:
                    field_0x8b5 = 2;
                    break;
            }
            break;
        case fpcNm_NPC_BJ3_e:
            field_0x8b4 = 2;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 5;
                    break;
                default:
                    field_0x8b5 = 4;
                    break;
            }
            break;
        case fpcNm_NPC_BJ4_e:
            field_0x8b4 = 3;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 7;
                    break;
                default:
                    field_0x8b5 = 6;
                    break;
            }
            break;
        case fpcNm_NPC_BJ5_e:
            field_0x8b4 = 4;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 9;
                    break;
                default:
                    field_0x8b5 = 8;
                    break;
            }
            break;
        case fpcNm_NPC_BJ6_e:
            field_0x8b4 = 5;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 0xB;
                    break;
                default:
                    field_0x8b5 = 0xA;
                    break;
            }
            break;
        case fpcNm_NPC_BJ7_e:
            field_0x8b4 = 6;
            field_0x8b5 = 0xC;
            break;
        case fpcNm_NPC_BJ8_e:
            field_0x8b4 = 7;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 0xE;
                    break;
                default:
                    field_0x8b5 = 0xD;
                    break;
            }
            break;
        case fpcNm_NPC_BJ9_e:
            field_0x8b4 = 8;
            switch (i_param_1) {
                case 1:
                    field_0x8b5 = 0x10;
                    break;
                default:
                    field_0x8b5 = 0xF;
                    break;
            }
            break;
        default:
            return false;
    }

    return true;
}

/* 00003998-000039DC       .text eInit_setLocFlag__11daNpc_Bj1_cFPi */
void daNpc_Bj1_c::eInit_setLocFlag(int* i_param_1) {
    field_0x856 = 0;
    if (i_param_1 == NULL) {
        return;
    }
    switch (*i_param_1) {
    case 2:
        m_jnt.setTrn();
        break;
    case 1:
        field_0x856 = 1;
        break;
    }
}

/* 000039DC-00003A14       .text eInit_setShapeAngleY__11daNpc_Bj1_cFPis */
void daNpc_Bj1_c::eInit_setShapeAngleY(int* i_prm, s16 i_angleY) {
    field_0x846 = 0;

    if (i_prm == NULL) {
        return;
    }

    field_0x846 = *i_prm == 1;

    if (field_0x846 != 0) {
        shape_angle.y = i_angleY;
    }
}

/* 00003A14-00003A30       .text eInit_setEvTimer__11daNpc_Bj1_cFPi */
void daNpc_Bj1_c::eInit_setEvTimer(int* i_param_1) {
    field_0x830 = 0;
    if (i_param_1 != NULL) {
        field_0x830 = *i_param_1;
    }
}

/* 00003A30-00003B10       .text eInit_calcRelativPos__11daNpc_Bj1_cFP4cXyzPi */
cXyz daNpc_Bj1_c::eInit_calcRelativPos(cXyz* i_param_2, int* arg2) {
    s16 sVar1;
    if (arg2) {
        sVar1 = shape_angle.y + (s16)*arg2;
    } else {
        sVar1 = shape_angle.y;
    }
    cXyz local_1c;
    cXyz local_28;
    if (i_param_2) {
        local_1c.set(i_param_2->x, i_param_2->y, i_param_2->z);
    } else {
        local_1c.set(0.0f, 0.0f, 0.0f);
    }
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(sVar1);
    mDoMtx_stack_c::multVec(&local_1c, &local_28);
    return local_28;
}

/* 00003B10-00003B20       .text eInit_prmFloat__11daNpc_Bj1_cFPff */
float daNpc_Bj1_c::eInit_prmFloat(float* i_param_1, float i_param_2) {
    if (i_param_1 == NULL) {
        return i_param_2;
    }
    return *i_param_1;
}

/* 00003B20-00003D3C       .text eInit_ATTENTION___11daNpc_Bj1_cFPiPiPiP4cXyzPiPiPi */
void daNpc_Bj1_c::eInit_ATTENTION_(int* i_prm_0_p, int* i_prm_1_p, int* i_prm_2_p, cXyz* i_offset_p, int* i_angle_p, int* i_index_p, int* i_timer_p) {
    if (!i_prm_0_p) {
        return;
    }
    s16 unaff_r27;
    switch (*i_prm_0_p) {
        case 0:
            break;
        case 1:
            field_0x8b3 = 1;
            unaff_r27 = cLib_targetAngleY(&current.pos, &dComIfGp_getPlayer(0)->current.pos);
            break;
        case 2:
            if (!i_offset_p) {
                return;
            }
            field_0x8b3 = 2;
            unaff_r27 = cLib_targetAngleY(&current.pos, i_offset_p);
            break;
        case 3:
            field_0x8b3 = 2;
            field_0x7cc = eInit_calcRelativPos(i_offset_p, i_angle_p);
            unaff_r27 = cLib_targetAngleY(&current.pos, &field_0x7cc);
            break;
        case 4:
            if (i_angle_p == NULL) {
                return;
            }
            field_0x8b3 = 3;
            unaff_r27 = *i_angle_p;
            break;
        case 5: {
            if (mPathRun.isPath() == false) {
                return;
            }
            u8 pointIndex = mPathRun.getIdx();
            if (i_index_p) {
                pointIndex = *i_index_p;
            }
            field_0x7cc = mPathRun.getPoint(pointIndex);
            field_0x8b3 = 2;
            break;
        }
        case 6: {
            fopAc_ac_c* iVar1 = searchByID(field_0x7a8);
            if (iVar1 == NULL) {
                return;
            }
            field_0x8b3 = 2;
            field_0x7cc = iVar1->eyePos;
            unaff_r27 = cLib_targetAngleY(&current.pos, &field_0x7cc);
            break;
        }
        case 7:
            field_0x8b3 = 2;
            field_0x7cc = field_0x860;
            field_0x7cc.y = eyePos.y;
            unaff_r27 = cLib_targetAngleY(&current.pos, &field_0x7cc);
            break;
        default:
            field_0x8b3 = 0;
            break;
    }

    eInit_setLocFlag(i_prm_1_p);
    // !@bug Possible use of uninitialized variable
    eInit_setShapeAngleY(i_prm_2_p, unaff_r27);
    eInit_setEvTimer(i_timer_p);
}

/* 00003D3C-00003E24       .text eInit_PLYER_MOV_1___11daNpc_Bj1_cFv */
void daNpc_Bj1_c::eInit_PLYER_MOV_1_() {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    s16 targetAngle = cLib_targetAngleY(&field_0x7ac, &player->current.pos);
    s16 diff = targetAngle - field_0x7ba;
    cXyz local;
    cXyz goalPos;

    if (abs(diff) > 0x1000) {
        mDoMtx_stack_c::transS(current.pos);
        mDoMtx_stack_c::YrotM(field_0x7ba);

        local.set(0.0f, 0.0f, 120.0f);
        mDoMtx_stack_c::multVec(&local, &goalPos);
    } else {
        goalPos.set(player->current.pos.x, player->current.pos.y, player->current.pos.z);
    }

    dComIfGp_evmng_setGoal(&goalPos);
}

/* 00003E24-00003F04       .text eInit_MOV___11daNpc_Bj1_cFPfPfPfPi */
void daNpc_Bj1_c::eInit_MOV_(float* i_speed1_p, float* i_speed2_p, float* i_speed3_p, int* i_evTimer_p) {
    u8 pointIdx = mPathRun.getIdx();

    setAnm_NUM(8);
    field_0x8b3 = 0;

    field_0x81c = eInit_prmFloat(i_speed1_p, 0.0f);
    field_0x80c = eInit_prmFloat(i_speed2_p, 0.0f);
    field_0x814 = eInit_prmFloat(i_speed3_p, 0.0f);

    field_0x7d8 = mPathRun.getPoint(pointIdx);

    eInit_setEvTimer(i_evTimer_p);

    field_0x84a = 0;
    field_0x8aa = 1;
}

/* 00003F04-00003F74       .text eInit_JMP___11daNpc_Bj1_cFPfPf */
void daNpc_Bj1_c::eInit_JMP_(float* i_param_1, float* i_param_2) {
    field_0x8a9 = 1;
    speed.y = eInit_prmFloat(i_param_1, 0.0f);
    gravity = eInit_prmFloat(i_param_2, 0.0f);
    setAnm_NUM(4);
}

/* 00003F74-0000406C       .text eInit_CHG_PTH___11daNpc_Bj1_cFPiPi */
void daNpc_Bj1_c::eInit_CHG_PTH_(int* i_cnt_p, int* i_idx_p) {
    u8 idx;
    mPathRun.setInf(field_0x83f[0], fopAcM_GetRoomNo(this), true);

    int cnt;
    if (mPathRun.isPath() && i_cnt_p != NULL && (cnt = *i_cnt_p) > 0) {
        dPath* path = mPathRun.nextPath(fopAcM_GetRoomNo(this));
        for (int i = 0; i < cnt - 1; i++) {
            if (path == NULL) {
                break;
            }
            path = dPath_GetNextRoomPath(path, fopAcM_GetRoomNo(this));
        }
        if (path != NULL) {
            mPathRun.setInfDrct(path);
        }
    }

    if (i_idx_p != NULL) {
        idx = *i_idx_p;
        if (idx >= mPathRun.maxPoint()) {
            idx = mPathRun.maxPoint() - 1;
        }
        mPathRun.setIdx(idx);
    }
}

/* 0000406C-000040BC       .text eInit_END_MOV___11daNpc_Bj1_cFv */
void daNpc_Bj1_c::eInit_END_MOV_() {
    setAnm_NUM(0);
    field_0x8aa = 0;
    field_0x80c = 0.0f;
    speedF = field_0x80c;
    field_0x814 = 0.0f;
}

/* 000040BC-000040F4       .text eInit_SET_TNE___11daNpc_Bj1_cFv */
void daNpc_Bj1_c::eInit_SET_TNE_() {
    setAnm_NUM(9);
    field_0x8b3 = 0;
}

/* 000040F4-00004114       .text eInit_DEL_TNE___11daNpc_Bj1_cFv */
void daNpc_Bj1_c::eInit_DEL_TNE_() {
    deleteSeed();
}

/* 00004114-0000417C       .text eInit_SET_ANM___11daNpc_Bj1_cFPiPf */
void daNpc_Bj1_c::eInit_SET_ANM_(int* i_anm_num, float* i_morf) {
    if (i_anm_num != NULL) {
        s8 temp = *i_anm_num;
        if ((temp >= 0) && (temp < 0xb)) {
            setAnm_NUM(temp);
            if (i_morf != NULL) {
                mpMorf->setMorf(*i_morf);
            }
        }
    }
}

/* 0000417C-000044E4       .text event_actionInit__11daNpc_Bj1_cFi */
void daNpc_Bj1_c::event_actionInit(int i_staffIdx) {
    int* act_no_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "ActNo");
    if (act_no_p != NULL) {
        field_0x8ac = *act_no_p;
        field_0x850 = 0;

        switch (field_0x8ac) {
        case 0: {
            int* prm_0_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "prm_0");
            int* prm_1_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "prm_1");
            int* prm_2_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "prm_2");
            cXyz* offst_p = dComIfGp_evmng_getMyXyzP(i_staffIdx, "Offst");
            int* angle_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "Angle");
            int* index_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "Index");
            int* timer_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "Timer");
            field_0x850 = 1;
            eInit_ATTENTION_(prm_0_p, prm_1_p, prm_2_p, offst_p, angle_p, index_p, timer_p);
            break;
        }
        case 1:
            eInit_PLYER_MOV_1_();
            break;
        case 2:
        case 8: {
            f32* goalR_p = dComIfGp_evmng_getMyFloatP(i_staffIdx, "GoalR");
            f32* speed_p = dComIfGp_evmng_getMyFloatP(i_staffIdx, "Speed");
            f32* accel_p = dComIfGp_evmng_getMyFloatP(i_staffIdx, "Accel");
            int* timer_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "Timer");
            field_0x850 = 1;
            eInit_MOV_(goalR_p, speed_p, accel_p, timer_p);
            break;
        }
        case 3: {
            f32* speed_p = dComIfGp_evmng_getMyFloatP(i_staffIdx, "Speed");
            f32* grvty_p = dComIfGp_evmng_getMyFloatP(i_staffIdx, "Grvty");
            field_0x850 = 1;
            eInit_JMP_(speed_p, grvty_p);
            break;
        }
        case 4: {
            int* pthNo_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "PthNo");
            int* index_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "Index");
            eInit_CHG_PTH_(pthNo_p, index_p);
            break;
        }
        case 5:
            eInit_END_MOV_();
            break;
        case 6:
            eInit_SET_TNE_();
            break;
        case 7:
            eInit_DEL_TNE_();
            break;
        case 9: {
            int* anmNo_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "AnmNo");
            f32* morfc_p = dComIfGp_evmng_getMyFloatP(i_staffIdx, "Morfc");
            eInit_SET_ANM_(anmNo_p, morfc_p);
            break;
        }
        }
    }
}

/* 000044E4-00004530       .text eMove_ATTENTION___11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::eMove_ATTENTION_() {
    if (field_0x830 >= 0) {
        return cLib_calcTimer(&field_0x830) == 0;
    }

    return m_jnt.trnChk() == 0;
}

/* 00004530-00004580       .text eMove_MOV___11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::eMove_MOV_() {
    if (field_0x830 >= 0) {
        return cLib_calcTimer(&field_0x830) == 0;
    }

    return field_0x84a == 1;
}

/* 00004580-00004594       .text eMove_JMP___11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::eMove_JMP_() {
    return field_0x8a9 == 0;
}

/* 00004594-000045F0       .text eMove_SET_TNE___11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::eMove_SET_TNE_() {
    if (mpMorf->checkFrame(20.0f)) {
        createSeed();
    }

    return field_0x83c != 0;
}

/* 000045F0-00004678       .text eMove_PTH_MOV___11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::eMove_PTH_MOV_() {
    bool isEnd = field_0x8aa == 0;
    if (!isEnd) {
        if (field_0x84a != 0) {
            field_0x7d8 = mPathRun.getPoint(mPathRun.getIdx());
        }
        field_0x84a = 0;
    }
    return isEnd;
}

/* 00004678-000046E8       .text event_action__11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::event_action() {
    switch (field_0x8ac) {
    case 0:
        return eMove_ATTENTION_();
    case 2:
        return eMove_MOV_();
    case 3:
        return eMove_JMP_();
    case 6:
        return eMove_SET_TNE_();
    case 8:
        return eMove_PTH_MOV_();
    case 9:
        break;
    }

    return true;
}

/* 000046E8-000047D4       .text privateCut__11daNpc_Bj1_cFi */
void daNpc_Bj1_c::privateCut(int i_staffIdx) {
    static char* a_cut_tbl[1] = {"ACTION"};

    if (i_staffIdx != -1) {
        field_0x8ab = dComIfGp_evmng_getMyActIdx(i_staffIdx, a_cut_tbl, 1, 1, 0);
        if (field_0x8ab == -1) {
            dComIfGp_evmng_cutEnd(i_staffIdx);
        } else {
            if (dComIfGp_evmng_getIsAddvance(i_staffIdx)) {
                switch (field_0x8ab) {
                    case 0:
                        event_actionInit(i_staffIdx);
                        break;
                }
            }

            bool cVar3;
            switch (field_0x8ab) {
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

/* 000047D4-000047F4       .text endEvent__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x8ad = 0xff;
}

/* 000047F4-0000482C       .text isEventEntry__11daNpc_Bj1_cFv */
s32 daNpc_Bj1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mDemoEventCut.getActorName());
}

/* 0000482C-00004914       .text event_proc__11daNpc_Bj1_cFi */
void daNpc_Bj1_c::event_proc(int i_staffIdx) {
    s16 evtIdx = mEventIdx[field_0x82c];
    if (dComIfGp_evmng_endCheck(evtIdx)) {
        switch (field_0x82c) {
        case 0:
            field_0x8b0 = TRUE;
            field_0x842 = TRUE;
            break;
        case 1:
            field_0x8b0 = TRUE;
            field_0x843 = TRUE;
            break;
        case 2:
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_2902);
            break;
        default:
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

/* 00004914-000049C0       .text set_action__11daNpc_Bj1_cFM11daNpc_Bj1_cFPCvPvPv_iPv */
s32 daNpc_Bj1_c::set_action(ActionFunc i_param_1, void* i_param_2) {

    if (field_0x6f8 != i_param_1) {
        if (field_0x6f8 != NULL) {
            field_0x8b6 = 9;
            (this->*field_0x6f8)(i_param_2);
        }
        field_0x6f8 = i_param_1;
        field_0x8b6 = 0;
        (this->*field_0x6f8)(i_param_2);
    }
    return 1;
}

/* 000049C0-00004B60       .text setStt__11daNpc_Bj1_cFSc */
void daNpc_Bj1_c::setStt(signed char i_stt) {
    s8 old_stt = field_0x8b1;
    field_0x832 = 0;
    field_0x8b1 = i_stt;

    switch (field_0x8b1) {
    case 0:
    case 1:
        break;
    case 2:
        if (field_0x8b5 == 4) {
            fopAc_ac_c* obj = searchByID(field_0x7a8);
            if (obj != NULL) {
#if TARGET_PC
                field_0x84b = ((jbo_class*)obj)->m2BA != 0; // 0x2BA holds on the GameCube only
#else
                field_0x84b = *((u8*)obj + 0x2ba) != 0;
#endif
            }
        }
        field_0x8ad = 0xff;
        field_0x8b3 = 1;
        field_0x8b2 = old_stt;
        m_jnt.setTrn();
        return;
    case 3:
        field_0x8b3 = 0;
        field_0x80c = l_HIO.mChild[field_0x8b4].field_0x38;
        field_0x814 = l_HIO.mChild[field_0x8b4].field_0x3c;
        gravity = 0.0f;
        field_0x849 = 0;
        field_0x8a8 = 6;
        setAnm_prp(1);
        field_0x84c = 1;
        break;
    case 4:
        field_0x8b3 = 1;
        field_0x856 = 1;
        field_0x80c = 0.0f;
        break;
    case 5:
        field_0x834 = 0x5a;
        break;
    case 6:
        field_0x80c = l_HIO.mChild[field_0x8b4].field_0x5c;
        field_0x814 = l_HIO.mChild[field_0x8b4].field_0x60;
        field_0x81c = l_HIO.mChild[field_0x8b4].field_0x64;
        field_0x8aa = 1;
        break;
    case 7:
    case 8:
        break;
    }

    setAnm();
}

/* 00004B60-00004C30       .text wait_1__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::wait_1() {
    if (field_0x8b0 == 1 || field_0x8b0 >= 3) {
        return TRUE;
    }

    if (field_0x855) {
        u8 talked = chk_talk();
        if (talked) {
            setStt(2);
        }
        return TRUE;
    }

    field_0x8b0 = 2;
    if (field_0x854) {
        field_0x832 = 0x3c;
    }

    if (cLib_calcTimer(&field_0x832)) {
        field_0x8b3 = 1;
        field_0x856 = 0;
    } else {
        field_0x8b3 = 3;
        field_0x83a = field_0x7ba;
        m_jnt.setTrn();
    }

    return TRUE;
}

/* 00004C30-00004D10       .text wait_2__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::wait_2() {
    if (field_0x8b0 == 1 || field_0x8b0 >= 3) {
        return TRUE;
    }

    if (field_0x855) {
        u8 talked = chk_talk();
        if (talked) {
            setStt(2);
        }
        return TRUE;
    }

    field_0x8b0 = 2;
    if (field_0x854) {
        field_0x832 = 0x3c;
        field_0x834 = 0x5a;
    }

    if (cLib_calcTimer(&field_0x832)) {
        field_0x8b3 = 1;
        field_0x856 = 0;
        return TRUE;
    }

    if (!cLib_calcTimer(&field_0x834)) {
        setStt(3);
    }

    return TRUE;
}

/* 00004D10-00004E2C       .text wait_3__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::wait_3() {
    cLib_addCalcAngleS(&current.angle.y, field_0x7ba, l_HIO.mChild[field_0x8b4].field_0x52,
                       l_HIO.mChild[field_0x8b4].field_0x54, 0);

    if (field_0x8b0 == 1 || field_0x8b0 >= 3) {
        return TRUE;
    }

    if (field_0x855) {
        u8 talked = chk_talk();
        if (talked) {
            setStt(2);
        }
        return TRUE;
    }

    field_0x8b0 = 2;
    if (field_0x854) {
        field_0x832 = 0x3c;
    }

    if (cLib_calcTimer(&field_0x832) && chk_drct(46.0f)) {
        field_0x8b3 = 1;
        field_0x856 = 0;
    } else {
        field_0x8b3 = 3;
        field_0x83a = field_0x7ba;
        m_jnt.setTrn();
    }

    return TRUE;
}

/* 00004E2C-00004F48       .text wait_4__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::wait_4() {
    if (field_0x8b0 == 1 || field_0x8b0 >= 3) {
        return TRUE;
    }

    if (field_0x855) {
        u8 talked = chk_talk();
        if (talked) {
            setStt(2);
        }
        return TRUE;
    }

    if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_2902) && dComIfGs_isGetItem(6, 0)) {
        field_0x8b0 = 5;
        return TRUE;
    }

    field_0x8b0 = 2;
    if (field_0x854) {
        field_0x832 = 0x3c;
    }

    if (cLib_calcTimer(&field_0x832)) {
        field_0x8b3 = 1;
        field_0x856 = 0;
    } else {
        field_0x8b3 = 3;
        field_0x83a = field_0x7ba;
        m_jnt.setTrn();
    }

    return TRUE;
}

/* 00004F48-000050CC       .text flyMov__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::flyMov() {
    field_0x8b0 = 0;
    field_0x849 = 0;

    if (field_0x8a8 == 1) {
        if (cLib_calcTimer(&field_0x82e)) {
            return TRUE;
        }

        f32 dist = (current.pos - dComIfGp_getPlayer(0)->current.pos).absXZ();

        s16 targetAngle = cLib_targetAngleY(&current.pos, &dComIfGp_getPlayer(0)->current.pos);
        s16 angleDiff = targetAngle - current.angle.y;
        s16 angleDiffAbs = abs(angleDiff);

        if (dist < l_HIO.mChild[field_0x8b4].field_0x4c && angleDiffAbs < l_HIO.mChild[field_0x8b4].field_0x50) {
            setStt(4);
        }
    }

    return TRUE;
}

/* 000050CC-0000514C       .text fall01__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::fall01() {
    field_0x8b0 = 0;
    field_0x849 = 0;

    if (field_0x8a8 == 0) {
        setStt(5);
        return TRUE;
    }

    if (field_0x8a8 == 1) {
        if ((int)speedF == 0) {
            speedF = 0.0f;
            field_0x8a8 = 2;
        }
    }

    return TRUE;
}

/* 0000514C-00005510       .text talk_1__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::talk_1() {
    u8 ret = chk_partsNotMove();
    talk(1);

    if (mpCurrMsg != NULL) {
        switch (mpCurrMsg->mStatus) {
        case fopMsgStts_BOX_OPENING_e:
        case fopMsgStts_MSG_TYPING_e:
            break;
        case fopMsgStts_MSG_DESTROYED_e:
            switch (mCurrMsgNo) {
            case 0x140F:
                dComIfGs_onEventBit(0x604);
                break;
            case 0x1410:
                dComIfGs_onEventBit(0x904);
                break;
            case 0x1413:
                dComIfGs_onEventBit(0xC80);
                break;
            case 0x13F3:
                dComIfGs_onEventBit(0x920);
                break;
            case 0x13F9:
                dComIfGs_onEventBit(0xC20);
                break;
            case 0x13FE:
                dComIfGs_onEventBit(0xC08);
                break;
            case 0x13F6:
                dComIfGs_onEventBit(0xC04);
                break;
            case 0x1404:
                dComIfGs_onEventBit(0xC02);
                break;
            case 0x1408:
                dComIfGs_onEventBit(0xD40);
                break;
            case 0x141D:
                dComIfGs_onEventBit(0xD08);
                break;
            case 0x1426:
                field_0x83f[1] = 1;
                break;
            case 0x1429:
                dComIfGp_setItemBeastNumCount(1, -4);
                field_0x8b0 = 3;
                break;
            case 0x142B:
                field_0x8b0 = 4;
                break;
            case 0x142F:
                field_0x83f[2] = 1;
                break;
            case 0x1432:
                dComIfGs_onEventBit(0x1C80);
                break;
            case 0x140C:
                dComIfGs_onEventBit(0xD10);
                break;
            case 0x1409:
                dComIfGs_onEventBit(0xD20);
                break;
            case 0x13EF:
                dComIfGs_onEventBit(0xC10);
                break;
            case 0x1493:
            case 0x149D:
                dComIfGs_onEventBit(0x2E10);
                dComIfGs_onEventBit(0x1D40);
                setReg(0x97FF);
                break;
            case 0x1483:
                setReg(0x96FF);
                break;
            case 0x1485:
                setReg(0x99FF);
                break;
            case 0x1489:
                setReg(0x98FF);
                break;
            }

            field_0x83e = 0xFF;
            field_0x855 = 0;
            setStt(field_0x8b2);
            field_0x832 = 0x3C;
            endEvent();
            break;
        }
    }

    return ret;
}

/* 00005510-000055D8       .text walk_1__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::walk_1() {
    field_0x84a = 0;
    if (field_0x8aa == 0) {
        field_0x7ac = current.pos;
        field_0x7b8 = current.angle.x;
        field_0x7ba = current.angle.y;
        field_0x7bc = current.angle.z;
        field_0x814 = field_0x80c = 0.0f;
        speedF = field_0x80c;
        setStt(7);
        return TRUE;
    }

    field_0x7d8 = mPathRun.getPoint(mPathRun.getIdx());
    field_0x856 = 1;
    return TRUE;
}

/* 000055D8-00005694       .text wait_action1__11daNpc_Bj1_cFPv */
BOOL daNpc_Bj1_c::wait_action1(void*) {
    switch (field_0x8b6) {
        case 0:
            setStt(1);
            field_0x8b6++;
            break;
        case 1:
        case 2:
        case 3:
            field_0x854 = chkAttention();
            switch (field_0x8b1) {
                case 1:
                    field_0x850 = wait_1();
                    break;
                case 2:
                    field_0x850 = talk_1();
                    break;
            }
            lookBack();
            break;
        default:
        case 9:
            break;
    }
    return TRUE;
}

/* 00005694-0000577C       .text wait_action2__11daNpc_Bj1_cFPv */
BOOL daNpc_Bj1_c::wait_action2(void*) {
    switch (field_0x8b6) {
        case 0:
            setStt(5);
            field_0x8b6++;
            break;
        case 1:
        case 2:
        case 3:
            field_0x854 = chkAttention();
            switch (field_0x8b1) {
                case 5:
                    field_0x850 = wait_2();
                    break;
                case 3:
                    field_0x850 = flyMov();
                    break;
                case 4:
                    field_0x850 = fall01();
                    break;
                case 2:
                    field_0x850 = talk_1();
                    break;
            }
            lookBack();
            break;
        default:
        case 9:
            break;
    }
    return TRUE;
}

/* 0000577C-00005860       .text wait_action3__11daNpc_Bj1_cFPv */
BOOL daNpc_Bj1_c::wait_action3(void*) {
    switch (field_0x8b6) {
        case 0:
            set_pthPoint(0);
            setStt(6);
            field_0x8b6++;
            break;
        case 1:
        case 2:
        case 3:
            field_0x854 = chkAttention();
            switch (field_0x8b1) {
                case 6:
                    field_0x850 = walk_1();
                    break;
                case 7:
                    field_0x850 = wait_3();
                    break;
                case 2:
                    field_0x850 = talk_1();
                    break;
            }
            lookBack();
            break;
        default:
        case 9:
            break;
    }
    return TRUE;
}

/* 00005860-00005928       .text wait_action4__11daNpc_Bj1_cFPv */
BOOL daNpc_Bj1_c::wait_action4(void*) {
    switch (field_0x8b6) {
        case 0:
            set_pthPoint(0);
            setStt(8);
            field_0x8b6++;
            break;
        case 1:
        case 2:
        case 3:
            field_0x854 = chkAttention();
            switch (field_0x8b1) {
                case 8:
                    field_0x850 = wait_4();
                    break;
                case 2:
                    field_0x850 = talk_1();
                    break;
            }
            lookBack();
            break;
        default:
        case 9:
            break;
    }
    return TRUE;
}

/* 00005928-000059C4       .text demo__11daNpc_Bj1_cFv */
u8 daNpc_Bj1_c::demo() {
    if (demoActorID == 0) {
        if (field_0x857 != 0) {
            field_0x857 = 0;
        }
    } else {
        field_0x857 = 1;
        dComIfGp_demo_getActor(demoActorID);
        dDemo_setDemoData(this, 0x6a, mpMorf, "Bj");
    }
    return field_0x857;
}

/* 000059C4-00005AC4       .text shadowDraw__11daNpc_Bj1_cFv */
void daNpc_Bj1_c::shadowDraw() {
    cXyz pos(current.pos.x, current.pos.y + 150.0f, current.pos.z);

    field_0x6e0 = dComIfGd_setShadow(field_0x6e0, 1, mpMorf->getModel(), &pos, 800.0f, 40.0f,
                                      current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);

    if (field_0x6e0 != 0) {
        if (field_0x6d4 != NULL) {
            dComIfGd_addRealShadow(field_0x6e0, field_0x6d4);
        }
        if (field_0x6d8 != NULL) {
            dComIfGd_addRealShadow(field_0x6e0, field_0x6d8);
        }
        if (field_0x84c != 0) {
            dComIfGd_addRealShadow(field_0x6e0, field_0x6e4->getModel());
        }
    }
}

/* 00005AC4-00005E38       .text _draw__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::_draw() {
    J3DModel* model = mpMorf->getModel();

    if (field_0x844 || field_0x845) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);

    switch (field_0x8b4) {
    case 1:
    case 3:
    case 8:
        mpMorf->entryDL((J3DMaterialTable*)dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BMT_BJ2_e));
        break;
    case 2:
    case 6:
    case 7:
        mpMorf->entryDL((J3DMaterialTable*)dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BMT_BJ3_e));
        break;
    case 0:
    case 4:
    case 5:
    default:
        mpMorf->entryDL();
        break;
    }

    if (field_0x6d4 != NULL) {
        g_env_light.setLightTevColorType(field_0x6d4, &tevStr);
        mDoExt_modelEntryDL(field_0x6d4);
    }
    if (field_0x6d8 != NULL) {
        g_env_light.setLightTevColorType(field_0x6d8, &tevStr);
        mDoExt_modelEntryDL(field_0x6d8);
    }

    g_env_light.setLightTevColorType(field_0x6d0, &tevStr);
    mDoExt_modelEntryDL(field_0x6d0);

    if (field_0x84c != 0) {
        g_env_light.setLightTevColorType(field_0x6e4->getModel(), &tevStr);
        field_0x6e4->entryDL();
    }

    shadowDraw();

    switch (field_0x8b4) {
    case 0:
        dSnap_RegistFig(DSNAP_TYPE_UNK9D, this, 1.0f, 1.0f, 1.0f);
        break;
    case 1:
        dSnap_RegistFig(DSNAP_TYPE_UNK9E, this, 1.0f, 1.0f, 1.0f);
        break;
    case 2:
        dSnap_RegistFig(DSNAP_TYPE_UNK9F, this, 1.0f, 1.0f, 1.0f);
        break;
    case 3:
        dSnap_RegistFig(DSNAP_TYPE_UNKA0, this, 1.0f, 1.0f, 1.0f);
        break;
    case 4:
        dSnap_RegistFig(DSNAP_TYPE_UNKA1, this, 1.0f, 1.0f, 1.0f);
        break;
    case 5:
        dSnap_RegistFig(DSNAP_TYPE_UNKA2, this, 1.0f, 1.0f, 1.0f);
        break;
    case 6:
        dSnap_RegistFig(DSNAP_TYPE_UNKA3, this, 1.0f, 1.0f, 1.0f);
        break;
    case 7:
        dSnap_RegistFig(DSNAP_TYPE_UNKA4, this, 1.0f, 1.0f, 1.0f);
        break;
    case 8:
        dSnap_RegistFig(DSNAP_TYPE_UNKA5, this, 1.0f, 1.0f, 1.0f);
        break;
    }

    if (l_HIO.mChild[field_0x8b4].field_0x1c) {
        GXColor unused_color0 = {0xFF, 0x00, 0x00, 0x80};
        GXColor unused_color1 = {0x00, 0x00, 0xFF, 0x80};
        GXColor unused_color2 = {0x00, 0xFF, 0x00, 0x80};
        GXColor unused_color3 = {0x00, 0xFF, 0x00, 0x80};
        GXColor unused_color4 = {0xFF, 0xFF, 0x00, 0x80};
        cXyz pos;
        if (field_0x8b5 == 6) {
            pos = current.pos;
            pos.y = mObjAcch.GetGroundH();
        }
        pos = current.pos;
        pos.y = eyePos.y;
    }

    return TRUE;
}

/* 00005E38-00006128       .text _execute__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::_execute() {
    if (field_0x84e[0] == 0) {
        field_0x7ac = current.pos;
        field_0x7b8 = current.angle.x;
        field_0x7ba = current.angle.y;
        field_0x7bc = current.angle.z;
        field_0x84e[0] = 1;
    }

    m_jnt.setParam(l_HIO.mChild[field_0x8b4].field_0xc, l_HIO.mChild[field_0x8b4].field_0xe,
                   l_HIO.mChild[field_0x8b4].field_0x10, l_HIO.mChild[field_0x8b4].field_0x12,
                   l_HIO.mChild[field_0x8b4].field_0x4, l_HIO.mChild[field_0x8b4].field_0x6,
                   l_HIO.mChild[field_0x8b4].field_0x8, l_HIO.mChild[field_0x8b4].field_0xa,
                   l_HIO.mChild[field_0x8b4].field_0x14);

    if (field_0x844 != 0 && demoActorID == 0) {
        return true;
    }

    field_0x847 = 0;
    field_0x844 = 0;
    partner_srch();
    checkOrder();
    if (!demo()) {
        s32 staffIdx = -1;
        if (dComIfGp_event_runCheck() && !eventInfo.checkCommandTalk()) {
            staffIdx = isEventEntry();
        }
        if (staffIdx >= 0) {
            event_proc(staffIdx);
        } else {
            (this->*field_0x6f8)(0);
        }

        if (!bj_flyMove()) {
            bj_nMove();
        }

        if (field_0x847 == 0) {
            fopAcM_posMoveF(this, mStts.GetCCMoveP());
        }

        if (field_0x846 == 0) {
            shape_angle = current.angle;
        }
    }

    eventOrder();

    if (field_0x84c != 0) {
        field_0x6f0 += field_0x6f2;

        f32 vol = 0.0f;
        s16 limit = l_HIO.mChild[field_0x8b4].field_0x44;
        if (limit != 0) {
            vol = 100.0f * ((f32)field_0x6f2 / limit);
            vol = cLib_minLimit(cLib_maxLimit(vol, 100.0f), 0.0f);
        }

        JAIZelBasic::zel_basic->seStart(JA_SE_CM_PRAPELLO_ROLLING, &current.pos, (u32)vol,
                                         dComIfGp_getReverb(fopAcM_GetRoomNo(this)), 1.0f, 1.0f, -1.0f,
                                         -1.0f, 0);
    }

    setMtx(false);
    if (field_0x857 == 0) {
        setCollision_SP_();
    }
    return true;
}

/* 00006128-000061A4       .text _delete__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::_delete() {
    dComIfG_resDelete((request_of_phase_process_class*)&field_0x6c4, "Bj");

    if (heap != NULL) {
        if (mpMorf != NULL) {
            mpMorf->stopZelAnime();
        }
        if (field_0x6e4 != NULL) {
            field_0x6e4->stopZelAnime();
        }
    }

    delPrtcl_drugPot();
    delPrtcl_danceLR();

    return TRUE;
}

/* 000061A4-000061C4       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Bj1_c*)i_this)->CreateHeap();
}

/* 000061C4-000062E4       .text _create__11daNpc_Bj1_cFv */
cPhs_State daNpc_Bj1_c::_create() {
    fopAcM_ct(this, daNpc_Bj1_c);

    cPhs_State resLoadResult = dComIfG_resLoad((request_of_phase_process_class*)&field_0x6c4, "Bj");
    if (resLoadResult != cPhs_COMPLEATE_e) {
        return resLoadResult;
    }

    if (!charDecide(fopAcM_GetParam(this) & 0xFF)) {
        return cPhs_ERROR_e;
    }

    static u32 a_size_tbl[] = {
        0x272E0, 0x272E0, 0x272E0, 0x272E0, 0x272E0,
        0x272E0, 0x272E0, 0x272E0, 0x272E0
    };

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_size_tbl[field_0x8b4])) {
        return cPhs_ERROR_e;
    }

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -50.0f, -20.0f, -50.0f, 50.0f, 160.0f, 50.0f);

    if (!createInit()) {
        return cPhs_ERROR_e;
    }

    return resLoadResult;
}

daNpc_Bj1_c::daNpc_Bj1_c() {}

/* 00006734-00006A00       .text create_Anm__11daNpc_Bj1_cFv */
J3DModelData* daNpc_Bj1_c::create_Anm() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BDL_BJ_e);

    JUT_ASSERT(VERSION_SELECT(0, 0, 4298, 0), a_mdl_dat != 0);

    mpMorf = new mDoExt_McaMorf(a_mdl_dat, NULL, NULL,
                                 (J3DAnmTransform*)dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BCK_WAIT01_e), 2, 1.0, 0,
                                 -1, 1, NULL, 0x80000, 0x15021222);

    if (mpMorf == NULL) {
        a_mdl_dat = NULL;
        return a_mdl_dat;
    } else if (mpMorf->getModel() == NULL) {
        mpMorf = NULL;
        a_mdl_dat = NULL;
        return a_mdl_dat;
    } else {
        m_hed_jnt_num = a_mdl_dat->getJointName()->getIndex("head");
        JUT_ASSERT(VERSION_SELECT(0, 0, 4318, 0), m_hed_jnt_num >= 0);

        m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone");
        JUT_ASSERT(VERSION_SELECT(0, 0, 4321, 0), m_bbone_jnt_num >= 0);

        m_armL2_jnt_num = a_mdl_dat->getJointName()->getIndex("armL2");
        JUT_ASSERT(VERSION_SELECT(0, 0, 4324, 0), m_armL2_jnt_num >= 0);

        m_armR2_jnt_num = a_mdl_dat->getJointName()->getIndex("armR2");
        JUT_ASSERT(VERSION_SELECT(0, 0, 4327, 0), m_armR2_jnt_num >= 0);
    }
    return a_mdl_dat;
}

/* 00006A00-00006C14       .text create_prp_Anm__11daNpc_Bj1_cFv */
J3DModelData* daNpc_Bj1_c::create_prp_Anm() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BDL_PP_e);
    JUT_ASSERT(0x10f6, a_mdl_dat != 0);

    mDoExt_McaMorf* morf = new mDoExt_McaMorf(
        a_mdl_dat, NULL, NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes("Bj", dRes_ID_BJ_BCK_OPEN_e),
        J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, 1, NULL,
        0x80000, 0x11000022
    );

    field_0x6e4 = morf;
    if (field_0x6e4 == NULL) {
        return NULL;
    }

    if (field_0x6e4->getModel() == NULL) {
        field_0x6e4 = NULL;
        return NULL;
    }

    m_cen_jnt_num = a_mdl_dat->getJointName()->getIndex("center");
    JUT_ASSERT(0x110a, m_cen_jnt_num >= 0);

    m_stalk_jnt_num = a_mdl_dat->getJointName()->getIndex("stalk");
    JUT_ASSERT(0x110d, m_stalk_jnt_num >= 0);

    return a_mdl_dat;
}

/* 00006C14-00006F54       .text create_itm_Mdl__11daNpc_Bj1_cFv */
bool daNpc_Bj1_c::create_itm_Mdl() {
    J3DModelData* a_mdl_dat;
    field_0x6d4 = NULL;
    field_0x6d8 = NULL;
    field_0x6d0 = NULL;

    static s32 l_arm_L_bmd_tbl[] = { -1, -1, -1, -1, -1, -1, dRes_ID_BJ_BDL_PL_AL_e, -1, -1 };

    if (l_arm_L_bmd_tbl[field_0x8b4] >= 0) {
        a_mdl_dat =
            (J3DModelData*)dComIfG_getObjectIDRes("Bj", (u16)l_arm_L_bmd_tbl[field_0x8b4]);
        JUT_ASSERT(0x1145, a_mdl_dat != 0);

        field_0x6d4 = mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
        bool ok = field_0x6d4 != NULL;
        if (!ok) {
            return ok;
        }

        if (l_arm_L_bmd_tbl[field_0x8b4] == dRes_ID_BJ_BDL_PL_AL_e) {
            m_plant_l_jnt_num = a_mdl_dat->getJointName()->getIndex("plant_al");
            JUT_ASSERT(0x114f, m_plant_l_jnt_num >= 0);
        }
    }

    static s32 l_arm_R_bmd_tbl[] = { -1, -1, -1, -1, -1, -1, dRes_ID_BJ_BDL_PL_AR_e, -1, -1 };

    if (l_arm_R_bmd_tbl[field_0x8b4] >= 0) {
        a_mdl_dat =
            (J3DModelData*)dComIfG_getObjectIDRes("Bj", (u16)l_arm_R_bmd_tbl[field_0x8b4]);
        JUT_ASSERT(0x1156, a_mdl_dat != 0);

        field_0x6d8 = mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
        bool ok = field_0x6d8 != NULL;
        if (!ok) {
            return ok;
        }

        if (l_arm_R_bmd_tbl[field_0x8b4] == dRes_ID_BJ_BDL_PL_AR_e) {
            m_plant_r_jnt_num = a_mdl_dat->getJointName()->getIndex("plant_ar");
            JUT_ASSERT(0x1160, m_plant_r_jnt_num >= 0);
        }
    }

    static s32 l_bmd_tbl[] = { dRes_ID_BJ_BDL_BJ1_FACE_e, dRes_ID_BJ_BDL_BJ2_FACE_e,
                                dRes_ID_BJ_BDL_BJ3_FACE_e, dRes_ID_BJ_BDL_BJ4_FACE_e,
                                dRes_ID_BJ_BDL_BJ5_FACE_e, dRes_ID_BJ_BDL_BJ6_FACE_e,
                                dRes_ID_BJ_BDL_BJ7_FACE_e, dRes_ID_BJ_BDL_BJ8_FACE_e,
                                dRes_ID_BJ_BDL_BJ9_FACE_e };

    a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Bj", (u16)l_bmd_tbl[field_0x8b4]);
    JUT_ASSERT(0x1166, a_mdl_dat != 0);

    field_0x6d0 = mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x11000022);
    return field_0x6d0 != NULL;
}

/* 00006F54-00007114       .text CreateHeap__11daNpc_Bj1_cFv */
BOOL daNpc_Bj1_c::CreateHeap() {
    J3DModelData* model_data = create_Anm();
    if (model_data == NULL) {
        return FALSE;
    }

    J3DModelData* prp_model_data = create_prp_Anm();
    if (prp_model_data == NULL) {
        mpMorf = NULL;
        return FALSE;
    }

    if (create_itm_Mdl()) {
        for (u16 i = 0; i < prp_model_data->getJointNum(); i++) {
            if (i == m_cen_jnt_num || i == m_stalk_jnt_num) {
                field_0x6e4->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Prp);
            }
        }

#if TARGET_PC
        field_0x6e4->getModel()->setUserArea((uintptr_t)this);
#else
        field_0x6e4->getModel()->setUserArea((u32)this);
#endif

        for (u16 i = 0; i < model_data->getJointNum(); i++) {
            if (i == m_hed_jnt_num || i == m_bbone_jnt_num || i == m_armR2_jnt_num) {
                mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Bj1);
            }
        }

#if TARGET_PC
        mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
        mpMorf->getModel()->setUserArea((u32)this);
#endif
        mAcchCir.SetWall(30.0f, 40.0f);
        mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir, fopAcM_GetSpeed_p(this));
        return TRUE;
    } else {
        mpMorf = NULL;
        field_0x6e4 = NULL;
        return FALSE;
    }
}

/* 00007114-00007134       .text daNpc_Bj1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Bj1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Bj1_c*)i_this)->_create();
}

/* 00007134-00007154       .text daNpc_Bj1_Delete__FP11daNpc_Bj1_c */
static BOOL daNpc_Bj1_Delete(daNpc_Bj1_c* i_this) {
    return ((daNpc_Bj1_c*)i_this)->_delete();
}

/* 00007154-00007174       .text daNpc_Bj1_Execute__FP11daNpc_Bj1_c */
static BOOL daNpc_Bj1_Execute(daNpc_Bj1_c* i_this) {
    return ((daNpc_Bj1_c*)i_this)->_execute();
}

/* 00007174-00007194       .text daNpc_Bj1_Draw__FP11daNpc_Bj1_c */
static BOOL daNpc_Bj1_Draw(daNpc_Bj1_c* i_this) {
    return ((daNpc_Bj1_c*)i_this)->_draw();
}

/* 00007194-0000719C       .text daNpc_Bj1_IsDelete__FP11daNpc_Bj1_c */
static BOOL daNpc_Bj1_IsDelete(daNpc_Bj1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Bj1_Method = {
    (process_method_func)daNpc_Bj1_Create,
    (process_method_func)daNpc_Bj1_Delete,
    (process_method_func)daNpc_Bj1_Execute,
    (process_method_func)daNpc_Bj1_IsDelete,
    (process_method_func)daNpc_Bj1_Draw,
};

actor_process_profile_definition g_profile_NPC_BJ1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ1_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ2 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ2_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ2_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ3 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ3_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ3_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ4 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ4_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ4_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ5 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ5_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ5_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ6 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ6_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ6_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ7 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ7_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ7_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ8 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ8_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ8_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};

actor_process_profile_definition g_profile_NPC_BJ9 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BJ9_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bj1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BJ9_e,
    /* Actor SubMtd */ &l_daNpc_Bj1_Method,
    /* Status       */ 0x07 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_NPC_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
