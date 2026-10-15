/**
 * d_a_pz.cpp
 * NPC - Princess Zelda
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_pz.h"
#include "d/d_cc_d.h"
#include "d/actor/d_a_item.h"
#include "d/actor/d_a_arrow.h"
#include "d/actor/d_a_gnd.h"
#include "d/actor/d_a_player.h"
#include "d/d_cc_uty.h"
#include "d/d_kankyo.h"
#include "d/d_debug_viewer.h"

const u32 daPz_c::m_heapsize = 0xA740;
const char daPz_c::m_arc_name[] = "PZ";
const f32 daPz_c::m_smoke_ef = 1.25f;
const f32 daPz_c::m_grass_ef = 1.5f;
const f32 daPz_c::m_splash_ef = 1.0f;

const dCcD_SrcCyl daPz_c::m_cyl_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsOther_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ dCcG_SE_UNK23,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoConHit_e | dCcG_TgSPrm_NoHitMark_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCylS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 0.0f,
        /* Height */ 0.0f,
    }},
};



/* 000000EC-00000310       .text __ct__10daPz_HIO_cFv */
class daPz_HIO_c : public mDoHIO_entry_c {
public:
    daPz_HIO_c();
    virtual ~daPz_HIO_c() {}

public:
    /* 0x004 */ dNpc_HIO_c mNpcHIO;
    /* 0x02C */ u8 m02C;
    /* 0x02D */ u8 m02D;
    /* 0x02E */ u8 m02E;
    /* 0x02F */ u8 m02F;
    /* 0x030 */ u8 m030;
    /* 0x031 */ u8 m031;
    /* 0x032 */ u8 m032;
    /* 0x033 */ u8 m033;
    /* 0x034 */ u8 m034[10];
    /* 0x040 */ f32 m040;
    /* 0x044 */ f32 m044;
    /* 0x048 */ f32 m048;
    /* 0x04C */ f32 m04C;
    /* 0x050 */ f32 m050;
    /* 0x054 */ s16 m054;
    /* 0x058 */ f32 m058;
    /* 0x05C */ f32 m05C[3];
    /* 0x068 */ f32 m068[3];
    /* 0x074 */ f32 m074[3];
    /* 0x080 */ s16 m080[3];
    /* 0x086 */ s16 m086[3];
    /* 0x08C */ s16 m08C[3];
    /* 0x092 */ s16 m092[3];
    /* 0x098 */ s16 m098[3];
    /* 0x0A0 */ f32 m0A0[3];
    /* 0x0AC */ f32 m0AC;
    /* 0x0B0 */ f32 m0B0;
    /* 0x0B4 */ f32 m0B4;
    /* 0x0B8 */ f32 m0B8;
    /* 0x0BC */ f32 m0BC;
    /* 0x0C0 */ f32 m0C0;
    /* 0x0C4 */ f32 m0C4;
    /* 0x0C8 */ f32 m0C8;
    /* 0x0CC */ s16 m0CC;
    /* 0x0D0 */ f32 m0D0;
    /* 0x0D4 */ f32 m0D4;
    /* 0x0D8 */ f32 m0D8;
    /* 0x0DC */ s16 m0DC;
    /* 0x0DE */ s16 m0DE;
    /* 0x0E0 */ s16 m0E0;
    /* 0x0E2 */ s16 m0E2;
    /* 0x0E4 */ s16 m0E4;
    /* 0x0E6 */ s16 m0E6;
    /* 0x0E8 */ s16 m0E8;
    /* 0x0EC */ f32 m0EC;
    /* 0x0F0 */ f32 m0F0;
    /* 0x0F4 */ f32 m0F4;
    /* 0x0F8 */ s16 m0F8[3];
    /* 0x100 */ f32 m100[3];
};

daPz_HIO_c::daPz_HIO_c() {
    m02C = 0;
    m02D = 1;
    m032 = 0;
    m033 = 0;

    for (int i = 0; i < 10; i++) {
        m034[i] = 0;
    }

    mNpcHIO.m04 = -20.0f;
    mNpcHIO.mMaxHeadX = 0x1FFE;
    mNpcHIO.mMaxHeadY = 0x2710;
    mNpcHIO.mMaxBackboneX = 0x1B58;
    mNpcHIO.mMaxBackboneY = 0x32C8;
    mNpcHIO.mMinHeadX = -0x9C4;
    mNpcHIO.mMinHeadY = -0x2710;
    mNpcHIO.mMinBackboneX = -0x1B58;
    mNpcHIO.mMinBackboneY = -0x32C8;
    mNpcHIO.mMaxTurnStep = 0x1000;
    mNpcHIO.mMaxHeadTurnVel = 0x800;
    mNpcHIO.mAttnYOffset = 50.0f;
    mNpcHIO.mMaxAttnAngleY = 0x32C8;
    mNpcHIO.m22 = 0;
    mNpcHIO.mMaxAttnDistXZ = 400.0f;
    m040 = 60.0f;
    m02F = 0;
    m054 = 0x2000;
    m058 = 200.0f;
    m044 = 1.2f;
    m04C = 0.9f;
    m048 = 2.0f;
    m050 = 10.0f;
    m031 = 0;
    m05C[0] = 100.0f;
    m068[0] = 0.0f;
    m074[0] = 100.0f;
    m080[0] = 30;
    m086[0] = 300;
    m08C[0] = 0;
    m092[0] = 30;
    m098[0] = 0;
    m0A0[0] = 1000.0f;
    m0F8[0] = 60;
    m100[0] = 3000.0f;
    m05C[2] = 100.0f;
    m068[2] = 100.0f;
    m074[2] = 80.0f;
    m080[2] = 30;
    m086[2] = 100;
    m08C[2] = 100;
    m092[2] = 30;
    m098[2] = 60;
    m0A0[2] = 1000.0f;
    m0F8[2] = 600;
    m100[2] = 800.0f;
    m0AC = 100.0f;
    m0E2 = 4;
    m0E4 = 6;
    m0E6 = 4;
    m0E8 = 6;
    m0B0 = -2.5f;
    m0B4 = 20.0f;
    m0B8 = 20.0f;
    m0BC = 20.0f;
    m0C0 = 20.0f;
    m0C4 = 4.0f;
    m0C8 = 15.0f;
    m0CC = 14;
    m0D8 = 30.0f;
    m0DC = 40;
    m0DE = 40;
    m0E0 = 5;
    m0D0 = 600.0f;
    m0EC = 800.0f;
    m0D4 = 15.0f;
    m0F0 = 2.0f;
    m0F4 = 1.0f;
}

static daPz_HIO_c l_HIO;

/* 00000310-00000340       .text stealItem_CB__FPv */
static BOOL stealItem_CB(void* actor) {
    if (actor) {
        daItem_c* item = (daItem_c*)actor;
        item->scale.setall(1.0f);
        item->setFlag(daItem_c::FLAG_HOOK);
    }
    return TRUE;
}

/* 00000340-0000038C       .text nodeHeadControl_CB__FP7J3DNodei */
static BOOL nodeHeadControl_CB(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        J3DModel* model = j3dSys.getModel();
        daPz_c* actor = (daPz_c*)model->getUserArea();
        if (actor != NULL) {
            actor->_nodeHeadControl(i_node, model);
        }
    }
    return TRUE;
}

#include "m_Do/m_Do_mtx.h"
#include "d/d_s_play.h"

/* 0000038C-000004FC       .text _nodeHeadControl__6daPz_cFP7J3DNodeP8J3DModel */
void daPz_c::_nodeHeadControl(J3DNode* node, J3DModel* model) {
    J3DJoint* joint = static_cast<J3DJoint*>(node);
    int jntNo = joint->getJntNo();

    mDoMtx_stack_c::copy(model->getAnmMtx(jntNo));

    static cXyz l_offsetAttPos(0.0f, 0.0f, 0.0f);
    static cXyz l_offsetEyePos(24.0f, -16.0f, 0.0f);
    mDoMtx_stack_c::multVec(&l_offsetAttPos, &field_0x8b8);
    mDoMtx_stack_c::YrotM(-m_jnt.getHead_y());
    mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
    mDoMtx_stack_c::multVec(&l_offsetEyePos, &mEyePos);

    field_0x72c.x = mDoMtx_stack_c::get()[0][3];
    field_0x72c.y = mDoMtx_stack_c::get()[1][3];
    field_0x72c.z = mDoMtx_stack_c::get()[2][3];

    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);

    model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 00000538-00000584       .text nodeWaistControl_CB__FP7J3DNodei */
static BOOL nodeWaistControl_CB(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        daPz_c* actor = (daPz_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->_nodeWaistControl(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

#include "m_Do/m_Do_mtx.h"
#include "d/d_s_play.h"

/* 00000584-00000710       .text _nodeWaistControl__6daPz_cFP7J3DNodeP8J3DModel */
void daPz_c::_nodeWaistControl(J3DNode* node, J3DModel* model) {
    J3DJoint* joint = static_cast<J3DJoint*>(node);
    int jntNo = joint->getJntNo();

    static s16 tmp_angle;
    tmp_angle += 0x1000;

    mDoMtx_stack_c::copy(model->getAnmMtx(jntNo));

    if (m06D3 == 4 || m06D3 == 5 || m06D3 == 6) {
        mDoMtx_stack_c::ZrotM(0xdac);
        mDoMtx_stack_c::YrotM(0x5dc);
    }

    if (REG12_S(6) != 0) {
        mDoMtx_stack_c::XrotM(tmp_angle);
    } else {
        mDoMtx_XrotM(mDoMtx_stack_c::get(), m_jnt.getBackbone_y());
        mDoMtx_stack_c::ZrotM(-m_jnt.getBackbone_x());
    }

    if (m06D3 == 4 || m06D3 == 5 || m06D3 == 6) {
        mDoMtx_stack_c::YrotM(-0x5dc);
        mDoMtx_stack_c::ZrotM(-0xdac);
    }

    cMtx_copy(mDoMtx_stack_c::get(), mWaistMtx);

    cXyz& pos = mWaistPos;
    pos.x = mDoMtx_stack_c::get()[0][3];
    pos.y = mDoMtx_stack_c::get()[1][3];
    pos.z = mDoMtx_stack_c::get()[2][3];

    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);

    model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 00000710-0000075C       .text nodeWaist2Control_CB__FP7J3DNodei */
static BOOL nodeWaist2Control_CB(J3DNode* i_node, int i_param) {
    if (i_param == 0) {
        daPz_c* actor = (daPz_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->_nodeWaist2Control(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 0000075C-000007F4       .text _nodeWaist2Control__6daPz_cFP7J3DNodeP8J3DModel */
void daPz_c::_nodeWaist2Control(J3DNode* node, J3DModel* model) {
    J3DJoint* joint = static_cast<J3DJoint*>(node);
    int jntNo = joint->getJntNo();

    mDoMtx_stack_c::copy(model->getAnmMtx(jntNo));

    mDoMtx_stack_c::YrotM(field_0xf7a);
    mDoMtx_stack_c::ZrotM(field_0xf78);

    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);

    model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 000007F4-00000840       .text nodeSkirtControl_CB__FP7J3DNodei */
static BOOL nodeSkirtControl_CB(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        J3DModel* model = j3dSys.getModel();
        daPz_c* actor = (daPz_c*)model->getUserArea();
        if (actor != NULL) {
            actor->_nodeSkirtControl(i_node, model);
        }
    }
    return TRUE;
}

#include "d/d_s_play.h"
#include "m_Do/m_Do_mtx.h"

/* 00000840-00000920       .text _nodeSkirtControl__6daPz_cFP7J3DNodeP8J3DModel */
void daPz_c::_nodeSkirtControl(J3DNode* node, J3DModel* model) {
    int jntNo = static_cast<J3DJoint*>(node)->getJntNo();
    MTXCopy(model->getAnmMtx(jntNo), mDoMtx_stack_c::get());

    s16 angle = field_0xf78;
    if (angle > field_0xf7a) {
        angle = field_0xf7a;
    }

    mDoMtx_stack_c::XrotM(REG12_S(2));
    mDoMtx_stack_c::YrotM(REG12_S(3));
    mDoMtx_stack_c::ZrotM(-(angle + REG12_S(4)));

    MTXCopy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    MTXCopy(mDoMtx_stack_c::get(), model->getAnmMtx(jntNo));
}

/* 00000920-00000940       .text createHeap_CB__FP10fopAc_ac_c */
static BOOL createHeap_CB(fopAc_ac_c* i_this) {
    return ((daPz_c*)i_this)->_createHeap();
}

/* 00000940-00000D54       .text bodyCreateHeap__6daPz_cFv */
BOOL daPz_c::bodyCreateHeap() {
    J3DAnmTexPattern* btp;
    J3DAnmTextureSRTKey* btk;
    J3DAnmTevRegKey* brk;
    J3DModelData* modelData = static_cast<J3DModelData*>(dComIfG_getObjectRes(m_arc_name, dRes_INDEX_PZ_BDL_PZ_e));
    JUT_ASSERT(0x23e, modelData != 0);

    for (u16 i = 0; i < modelData->getMaterialNum(); i++) {
        J3DMaterial* mat = modelData->getMaterialNodePointer(i);
        mat->setMaterialAnm(new daPz_matAnm_c());
    }

    mpMorf = new mDoExt_McaMorf(modelData, NULL, NULL, NULL, J3DFrameCtrl::EMode_NULL, 1.0f, 0, -1, 1, NULL, 0x80000, 0x11020222);
    if (mpMorf == NULL || mpMorf->getModel() == NULL) {
        return FALSE;
    }

#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    if (!mInvisibleModel.create(mpMorf->getModel())) {
        return FALSE;
    }

    m_jnt.setHeadJntNum(PZ_JNT_HEAD_e);
    modelData->getJointNodePointer(PZ_JNT_HEAD_e)->setCallBack(nodeHeadControl_CB);
    m_jnt.setBackboneJntNum(PZ_JNT_STOMACH_e);
    modelData->getJointNodePointer(PZ_JNT_STOMACH_e)->setCallBack(nodeWaistControl_CB);
    modelData->getJointNodePointer(PZ_JNT_WAIST_e)->setCallBack(nodeWaist2Control_CB);
    modelData->getJointNodePointer(PZ_JNT_SKIRT_2_e)->setCallBack(nodeSkirtControl_CB);

    btp = (J3DAnmTexPattern*)dComIfG_getObjectRes(m_arc_name, dRes_INDEX_PZ_BTP_MABA_A_e);
    JUT_ASSERT(0x277, btp != 0);
    if (!mBtpAnm.init(modelData, btp, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, false, FALSE)) {
        return FALSE;
    }

    btk = (J3DAnmTextureSRTKey*)dComIfG_getObjectRes(m_arc_name, dRes_INDEX_PZ_BTK_MABA_A_e);
    JUT_ASSERT(0x27d, btk != 0);
    if (!mBtkAnm.init(modelData, btk, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, false, FALSE)) {
        return FALSE;
    }

    u16 num = mBtkAnm.getBtkAnm()->getUpdateMaterialNum();
    for (u16 i = 0; i < num; i++) {
        m08A8[i] = (daPz_matAnm_c*)modelData->getMaterialNodePointer(mBtkAnm.getBtkAnm()->getUpdateMaterialID(i))->getMaterialAnm();
    }

    brk = (J3DAnmTevRegKey*)dComIfG_getObjectRes(m_arc_name, dRes_INDEX_PZ_BRK_TRI_TEST_e);
    JUT_ASSERT(0x28e, brk != 0);
    if (!mBrkAnm.init(modelData, brk, TRUE, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, false, FALSE)) {
        return FALSE;
    }

    return TRUE;
}

#include "d/d_com_inf_game.h"
/* 00000D54-00000E74       .text bowCreateHeap__6daPz_cFv */
BOOL daPz_c::bowCreateHeap() {
    J3DModelData* modelData = (J3DModelData*)dComIfG_getObjectRes(m_arc_name, dRes_INDEX_PZ_BDL_BOW_e);
    JUT_ASSERT(0x29f, modelData != 0);

    mpBowMcaMorf = new mDoExt_McaMorf(modelData, NULL, NULL, NULL, J3DFrameCtrl::EMode_NULL, 1.0f, 0, -1, 1, NULL, 0x80000, 0x11000022);

    if (mpBowMcaMorf == NULL || mpBowMcaMorf->getModel() == NULL) {
        return FALSE;
    }

#if TARGET_PC
    mpBowMcaMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpBowMcaMorf->getModel()->setUserArea((u32)this);
#endif
    return TRUE;
}

/* 00000E74-00000EC0       .text _createHeap__6daPz_cFv */
BOOL daPz_c::_createHeap() {
    if (bodyCreateHeap() == 0) {
        return FALSE;
    }

    return bowCreateHeap() ? TRUE : FALSE;
}

/* 00000EC0-00000F20       .text __ct__13daPz_matAnm_cFv */
daPz_matAnm_c::daPz_matAnm_c() {
    clrMoveFlag();
    mOffset.x = 0.0f;
    mOffset.y = 0.0f;
}

#include "JSystem/J3DGraphBase/J3DMaterial.h"

/* 00000F20-00000FB4       .text calc__13daPz_matAnm_cCFP11J3DMaterial */
void daPz_matAnm_c::calc(J3DMaterial* material) const {
    J3DMaterialAnm::calc(material);

    for (u32 i = 0; i < 8; i++) {
        if (getTexMtxAnm(i) != NULL) {
            J3DTexMtx* texMtx = material->getTexMtx(i);
            if (field_0x7C) {
                texMtx->mSRT.mTranslationX = mOffset.x;
                texMtx->mSRT.mTranslationY = mOffset.y;
            }
        }
    }
}

/* 00000FB4-00001038       .text getGndPos__6daPz_cFv */
void daPz_c::getGndPos() {
    mbHasGanondorf = false;

    fopAc_ac_c* target;
    if (fopAcM_SearchByName(fpcNm_GND_e, &target)) {
        mGanondorfPos4 = target->eyePos;
        mGanondorfPosCurrent = target->current.pos;
        mbHasGanondorf = true;
    }
}

#include "SSystem/SComponent/c_lib.h"


/* 00001038-0000114C       .text checkEyeArea__6daPz_cFR4cXyz */
bool daPz_c::checkEyeArea(cXyz& i_pos) {
    s32 angleDiff = cLib_distanceAngleS(shape_angle.y, cLib_targetAngleY(&current.pos, &i_pos));
    f32 dist = (current.pos - i_pos).absXZ();

    if (angleDiff < l_HIO.m054 && dist < l_HIO.m058) {
        return true;
    }
    return false;
}

/* 0000114C-00001194       .text getMsg__6daPz_cFv */
u32 daPz_c::getMsg() {
    u32 msgNo = 0x3562;
    if (m08B0 == 0) {
        msgNo = 0x3562;
    } else if (m08B0 == 2) {
        if (m0F65 == 0) {
            m0F65 = 1;
            msgNo = 0x3563;
        } else {
            msgNo = 0x3565;
        }
    }

    return msgNo;
}

/* 00001194-00001208       .text next_msgStatus__6daPz_cFPUl */
u16 daPz_c::next_msgStatus(u32* i_msgNo) {
    u16 msgStatus = 15;

    switch (*i_msgNo) {
    case 0x3563:
        *i_msgNo = 0x3564;
        break;
    default:
        if (*i_msgNo == 0x3564) {
            fopAc_ac_c* actor;
            if (fopAcM_SearchByName(0xf8, &actor)) {
                if (actor->stealItemBitNo == 0) {
                    actor->stealItemBitNo = 0x23;
                }
            }
        }

        msgStatus = 16;
        break;
    }

    return msgStatus;
}

/* 00001208-00001288       .text anmAtr__6daPz_cFUs */
void daPz_c::anmAtr(u16 i_msgStatus) {
    static const s8 anm_atr[] = { 12, 13 };

    switch (i_msgStatus) {
        case fopMsgStts_MSG_TYPING_e:
            if (field_0x6cc == 0) {
                u8 idx = dComIfGp_getMesgAnimeAttrInfo();
                field_0x6cc = 1;
                setAnm(anm_atr[idx], false, 15);
            }
            break;
        case fopMsgStts_MSG_DISPLAYED_e:
            field_0x6cc = 0;
            break;
    }
}

/* 00001288-000012D4       .text eventOrder__6daPz_cFv */
void daPz_c::eventOrder() {
    if (m0F82 == 1 || m0F82 == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (m0F82 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    }
}

/* 000012D4-00001338       .text checkOrder__6daPz_cFv */
void daPz_c::checkOrder() {
    if(eventInfo.checkCommandDemoAccrpt()) {
        m0F82 = 0;
    } else if (eventInfo.checkCommandTalk()) {
        if(m0F82 == 1 || m0F82 == 2) {
            m0F82 = 0;
            modeProc(PROC_INIT_e, 9);
        }
    }
}

/* 00001338-0000151C       .text setFallSplash__6daPz_cFv */
void daPz_c::setFallSplash() {
    g_env_light.settingTevStruct(TEV_TYPE_BG1, &current.pos, &mTevstr);

    mFallSplashPos = mEyePos;
    mFallSplashPos.y += l_HIO.m0D4;

    if (mObjAcch.ChkGroundHit()) {
        if (dComIfG_Bgsp()->GetAttributeCode(mObjAcch.m_gnd) == dBgS_Attr_WATERFALL_e) {
            if (mFallSplashCb.getEmitter() == NULL) {
                dComIfGp_particle_set(dPa_name::ID_AK_JN_ELEMENTSHIBUKI00, &mFallSplashPos, &shape_angle, NULL, 0xFF, &mFallSplashCb);
                if (mFallSplashCb.getEmitter() != NULL) {
                    mFallSplashCb.getEmitter()->setRate(4.0f);
                    mFallSplashCb.getEmitter()->setSpread(1.0f);
                    mFallSplashCb.getEmitter()->setGlobalPrmColor(mTevstr.mColorC0.r, mTevstr.mColorC0.g, mTevstr.mColorC0.b);
                }
            }
        } else {
            mFallSplashCb.remove();
        }

        if (cLib_calcTimer(&mFallRippleTimer) == 0) {
            static Vec fall_ripple_scale = {0.75f, 0.75f, 0.75f};
            MtxP mtx = mpMorf->getModel()->getAnmMtx(0);
            cXyz pos;
            pos.x = mtx[0][3];
            pos.y = mtx[1][3];
            pos.z = mtx[2][3];
            dComIfGp_particle_setSingleRipple(dPa_name::ID_IT_JN_WP_HAMON03, &pos, NULL, (cXyz*)&fall_ripple_scale);
            mFallRippleTimer = 15;
        }
    } else {
        mFallSplashCb.remove();
    }
}

/* 0000151C-000015F4       .text setHeadSplash__6daPz_cFv */
void daPz_c::setHeadSplash() {
    g_env_light.settingTevStruct(TEV_TYPE_BG1, &current.pos, &mTevstr);

    if (mHeadSplashCb.getEmitter() == NULL) {
        dComIfGp_particle_set(dPa_name::ID_AK_SN_PZSHAKEHEADSPLASH00, &field_0x72c, NULL, NULL,
                               0xFF, &mHeadSplashCb);
    }

    if (mHeadSplashCb.getEmitter() != NULL) {
        mHeadSplashCb.getEmitter()->setGlobalPrmColor(mTevstr.mColorC0.r, mTevstr.mColorC0.g,
                                                       mTevstr.mColorC0.b);
        mHeadSplashCb.getEmitter()->setGlobalRTMatrix(mpMorf->getModel()->getAnmMtx(PZ_JNT_HEAD_e));
    }
}

/* 000015F4-00001704       .text setRipple__6daPz_cFv */
void daPz_c::setRipple() {
    if (mObjAcch.ChkGroundHit()) {
        if (dComIfG_Bgsp()->GetAttributeCode(mObjAcch.m_gnd) == dBgS_Attr_WATER_e) {
            if (mRippleCb.getEmitter() == NULL) {
                static cXyz ripple_scale(0.8f, 0.8f, 0.8f);
                dComIfGp_particle_setShipTail(dPa_name::ID_AK_JN_HAMON00, &current.pos, NULL, &ripple_scale, 0xFF, &mRippleCb);
                if (mRippleCb.getEmitter() != NULL) {
                    mRippleCb.setRate(0.0f);
                }
            }
        } else {
            mRippleCb.end();
        }
    }
}

/* 00001704-0000175C       .text setJntStatus__6daPz_cFv */
void daPz_c::setJntStatus() {
    m_jnt.setParam(l_HIO.mNpcHIO.mMaxBackboneX, l_HIO.mNpcHIO.mMaxBackboneY,
                   l_HIO.mNpcHIO.mMinBackboneX, l_HIO.mNpcHIO.mMinBackboneY,
                   l_HIO.mNpcHIO.mMaxHeadX, l_HIO.mNpcHIO.mMaxHeadY, l_HIO.mNpcHIO.mMinHeadX,
                   l_HIO.mNpcHIO.mMinHeadY, l_HIO.mNpcHIO.mMaxTurnStep);
}

/* 0000175C-00001954       .text demo__6daPz_cFv */
u8 daPz_c::demo() {
    if (demoActorID == 0) {
        if (mbDemo != 0) {
            mbDemo = 0;
        }
    } else {
        mbDemo = 1;

        dDemo_actor_c* demo_actor = dComIfGp_demo_getActor(demoActorID);

        for (int i = 0; i < 2; i++) {
            if (m08A8[i] != NULL) {
                m08A8[i]->clrMoveFlag();
            }
        }

        J3DAnmTexPattern* btp = mBtpAnm.getBtpAnm();
        if (btp != NULL) {
            s16 frame = btp->getFrameMax() & 0xff;
            mBtpFrame++;
            if ((u16)mBtpFrame >= (u16)frame) {
                mBtpFrame = frame;
            }
        }

        btp = demo_actor->getP_BtpData(m_arc_name);
        if (btp != NULL) {
            mBtpAnm.init(mpMorf->getModel()->getModelData(), btp, 1, 0, 1.0f, 0, -1, true, 0);
            mBtpFrame = 0;
        }

        J3DAnmTextureSRTKey* btk = demo_actor->getP_BtkData(m_arc_name);
        if (btk != NULL) {
            mBtkAnm.init(mpMorf->getModel()->getModelData(), btk, 1, 0, 1.0f, 0, -1, true, 0);
        }

        J3DAnmTevRegKey* brk = (J3DAnmTevRegKey*)demo_actor->getP_BrkData(m_arc_name);
        if (brk != NULL) {
            mBrkAnm.init(mpMorf->getModel()->getModelData(), brk, 1, 2, 1.0f, 0, -1, true, 0);
        }

        dDemo_setDemoData(this, dDemo_actor_c::ENABLE_TRANS_e | dDemo_actor_c::ENABLE_ROTATE_e | dDemo_actor_c::ENABLE_ANM_e | dDemo_actor_c::ENABLE_ANM_FRAME_e, mpMorf, m_arc_name);
    }
    return mbDemo;
}

/* 00001954-00001EEC       .text checkTgHit__6daPz_cFv */
bool daPz_c::checkTgHit() {
    daPy_py_c* player = daPy_getPlayerActorClass();

    mStts.Move();

    if (calcTgHitTimer() == 0 && mCyl.ChkTgHit()) {
        bool hit = true;
        cCcD_Obj* hitObj = mCyl.GetTgHitObj();
        mTgHitTimer = l_HIO.m0E0;
        if (hitObj == NULL) {
            return false;
        }

        switch (hitObj->GetAtType()) {
        case AT_TYPE_SWORD:
        case AT_TYPE_MACHETE:
        case AT_TYPE_UNK800:
        case AT_TYPE_DARKNUT_SWORD:
        case AT_TYPE_MOBLIN_SPEAR:
            switch (player->getCutType()) {
            case daPy_py_c::CUT_TYPE_BT_JUMPCUT:
            case daPy_py_c::CUT_TYPE_CUT_EA:
            case daPy_py_c::CUT_TYPE_CUT_EB:
            case daPy_py_c::CUT_TYPE_CUT_TURN:
            case daPy_py_c::CUT_TYPE_CUT_ROLL:
            case daPy_py_c::CUT_TYPE_JUMPCUT_SWORD:
            case daPy_py_c::CUT_TYPE_JUMPCUT_STICK:
            case daPy_py_c::CUT_TYPE_JUMPCUT_MACHETE:
            case daPy_py_c::CUT_TYPE_BT_ROLLCUT:
            case daPy_py_c::CUT_TYPE_BT_VERTICALJUMPCUT:
            case daPy_py_c::CUT_TYPE_JUMPCUT_CLUB:
            case daPy_py_c::CUT_TYPE_JUMPCUT_DN_SWORD:
            case daPy_py_c::CUT_TYPE_JUMPCUT_SPEAR:
            case daPy_py_c::CUT_TYPE_CUT_EXA:
            case daPy_py_c::CUT_TYPE_CUT_EXB:
                mHitType = 1;
                break;
            default:
                mHitType = 0;
                break;
            }
            break;
        case AT_TYPE_WIND:
            hit = false;
            mHitType = 3;
            break;
        case AT_TYPE_BOOMERANG:
        case AT_TYPE_BOKO_STICK:
            mHitType = 4;
            break;
        case AT_TYPE_HOOKSHOT:
            mHitType = 12;
            break;
        case AT_TYPE_SKULL_HAMMER:
        case AT_TYPE_STALFOS_MACE:
            mHitType = 7;
            if (player->getCutType() == daPy_py_c::CUT_TYPE_HAMMER_SIDESWING) {
                mHitType = 8;
            }
            break;
        case AT_TYPE_BOMB:
            mHitType = 6;
            break;
        case AT_TYPE_NORMAL_ARROW:
        case AT_TYPE_FIRE_ARROW:
        case AT_TYPE_ICE_ARROW:
        case AT_TYPE_LIGHT_ARROW:
            mHitType = 5;
            break;
        case AT_TYPE_GRAPPLING_HOOK:
            mHitType = 14;
            hit = false;
            break;
        }

        fopAc_ac_c* hitActor = mCyl.GetTgHitAc();
        if (fopAcM_GetName(hitActor) == fpcNm_ARROW_e) {
            if (((daArrow_c*)hitActor)->isLinkReflect()) {
                mHitType = 5;
            } else if (((daArrow_c*)hitActor)->isSetByZelda()) {
                return false;
            }
        }

        if (hit) {
            cXyz* hitPos = mCyl.GetTgHitPosP();
            m0F6C = fopAcM_searchActorAngleY(this, hitActor);
            CcAtInfo atInfo;
            atInfo.mpObj = mCyl.GetTgHitObj();
            def_se_set(this, mCyl.GetTgHitObj(), 0x41);
            dKy_SordFlush_set(*hitPos, 0);

            if (mHitType == 1 || mHitType == 7 || mHitType == 8) {
                m0F68 = 60.0f;
            } else {
                m0F68 = 40.0f;
            }

            dComIfGp_particle_set(dPa_name::ID_AK_JN_NG, hitPos, &player->shape_angle);
            modeProc(PROC_INIT_e, 4);

            if (fopAcM_GetName(hitActor) == fpcNm_GND_e) {
                m0744 = 0;
                m0F68 *= 1.5;
                m0754 = 0;
                m075C = 0;
                m0760++;
                if (m0760 > l_HIO.m0E2) {
                    m0760 = 0;
                    m0764 = l_HIO.m0E4;
                    m0768 = 1;
                }
                if (cM_rndF(100.0f) < 60.0f) {
                    fopAcM_monsSeStart(this, JA_SE_CV_ZL_GN_DAMAGE, 0);
                }
            } else {
                m0744 = 1;
                m0760 = 0;
                m0768 = 0;
                m0754++;
                if (m0754 > l_HIO.m0E6) {
                    m0754 = 0;
                    m0758 = l_HIO.m0E8;
                    m075C = 1;
                }
                if (cM_rndF(100.0f) < 30.0f) {
                    fopAcM_monsSeStart(this, JA_SE_CV_ZL_GN_DAMAGE, 0);
                }
            }

            mHitPos = *hitPos;
        } else if (mHitType == 14) {
            fopAcM_monsSeStart(this, JA_SE_CV_ZL_GN_DAMAGE, 0);
            int itemNo = 0;
            if (dComIfGs_getLife() <= 12) {
                itemNo = 30;
            }
            fopAcM_fastCreateItem(&current.pos, itemNo, fopAcM_GetRoomNo(this), &shape_angle, NULL, 0.0f, 0.0f, -6.0f, -1, stealItem_CB);
            modeProc(PROC_INIT_e, 4);
        }
        return true;
    }

    return false;
}

/* 00001EEC-00001F10       .text getArg__6daPz_cFv */
void daPz_c::getArg() {
    mArg = fopAcM_GetParam(this) >> 8 & 0xFF;
    if (mArg == 0xff) {
        mArg = 0;
    }
}

/* 00001F10-000020B8       .text setAttention__6daPz_cFv */
void daPz_c::setAttention() {
    attention_info.position = field_0x8b8;
    attention_info.position.y += l_HIO.m040;

    eyePos = mEyePos;

    f32 dist = (mEyePos - m08C4).absXZ();
    cLib_distanceAngleS(shape_angle.y, cLib_targetAngleY(&mEyePos, &m08C4));

    s16 target;
    if (m_jnt.trnChk()) {
        if (mEventCut.getTurnSpeed() != 0) {
            target = mEventCut.getTurnSpeed();
        } else {
            target = l_HIO.mNpcHIO.mMaxHeadTurnVel;
        }
    } else {
        target = 0;
    }
    cLib_addCalcAngleS2(&mTurnSpeed, target, 4, 0x800);

    m_jnt.lookAtTarget(&shape_angle.y, &m08C4, mEyePos, shape_angle.y,
                        mTurnSpeed, m08EA);
}

#include "d/d_lib.h"
/* 000020B8-00002114       .text setBowAnm__6daPz_cFScb */
void daPz_c::setBowAnm(s8 arg1, bool arg2) {
    if (arg1 != 5) {
        field_0x6de = arg1;
    }

    static const int a_anm_bcks_tbl[] = {
        dRes_INDEX_PZ_BCK_ARROW_RELORD_e,
        dRes_INDEX_PZ_BCK_ARROW_WAIT03_e,
        dRes_INDEX_PZ_BCK_ARROW_SHOOT_e,
    };
    static const dLib_anm_prm_c a_anm_prm_tbl[] = {
        {2, -1, 0, 0.0f, 0.0f, J3DFrameCtrl::EMode_NONE},
        {2, -1, 0, 0.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {0, -1, 0, 0.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {1, -1, 0, 0.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {2, -1, 0, 0.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
    };

    dLib_bcks_setAnm(m_arc_name, mpBowMcaMorf, &m06DD, &field_0x6de, &field_0x6df,
                      a_anm_bcks_tbl, a_anm_prm_tbl, arg2);
}

/* 00002114-00002184       .text setBowString__6daPz_cFb */
void daPz_c::setBowString(bool i_draw) {
    J3DMaterial* mat = mpBowMcaMorf->getModel()->getModelData()->getJointNodePointer(0)->getMesh()->getNext();
    J3DMaterial* mat2 = mat->getNext();
    if (i_draw) {
        mat->getShape()->show();
        mat2->getShape()->hide();
    } else {
        mat->getShape()->hide();
        mat2->getShape()->show();
    }
}

#include "d/d_lib.h"
#include "f_op/f_op_actor_mng.h"
#include "JAZelAudio/JAZelAudio_SE.h"

/* 00002184-0000246C       .text setAnm__6daPz_cFScbi */
void daPz_c::setAnm(s8 arg1, bool arg2, int arg3) {
    if (arg1 != 15) {
        m06D3 = arg1;
    }

    static const int a_anm_bcks_tbl[] = {
        dRes_INDEX_PZ_BCK_WAIT01_e, dRes_INDEX_PZ_BCK_WAIT02_e, dRes_INDEX_PZ_BCK_RUN01_e,
        dRes_INDEX_PZ_BCK_RELORD_e, dRes_INDEX_PZ_BCK_WAIT03_e, dRes_INDEX_PZ_BCK_SHOOT_e,
        dRes_INDEX_PZ_BCK_DAM01_e,  dRes_INDEX_PZ_BCK_DAM02_e,  dRes_INDEX_PZ_BCK_STAND_e,
        dRes_INDEX_PZ_BCK_DEFEND_e, dRes_INDEX_PZ_BCK_WAIT04_e,
    };
    dLib_anm_prm_c a_anm_prm_tbl[] = {
        {0, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {0, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {1, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {2, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {3, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {4, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {5, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {6, -1, 0, 4.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {7, -1, 0, 4.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {8, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {9, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_NONE},
        {10, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {1, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {1, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {4, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
    };
    static const s8 eye_anm[] = {0, 0, 7, 7, 1, 1, 1, 2, 3, 4, 5, 6, 8, 9, 8};
    a_anm_prm_tbl[7].mMorf = l_HIO.m0F0;
    a_anm_prm_tbl[8].mMorf = l_HIO.m0F4;

    if ((s8)m06D4 != (s8)m06D3) {
        if ((s8)m06D3 == 4) {
            fopAcM_monsSeStart(this, JA_SE_CV_ZL_DRAW_BOW, 0);
            setBowAnm(2, false);
            m08B4 = fopAcM_createChild(fpcNm_ARROW_e, fopAcM_GetID(this), 0, &current.pos,
                                        fopAcM_GetRoomNo(this), NULL);
        }

        if (m06D3 == 5) {
            setBowAnm(3, true);
        }

        if (m06D3 == 6) {
            fopAcM_monsSeStart(this, JA_SE_CV_ZL_SHOOT_BOW, 0);
            setBowAnm(4, true);

            fopAc_ac_c* arrow;
            if (fopAcM_SearchByID(m08B4, &arrow)) {
                fopAcM_SetParam(arrow, 1);
            }
        }

        if ((s8)m06D3 != 4 && (s8)m06D3 != 5) {
            fopAc_ac_c* arrow;
            if (fopAcM_SearchByID(m08B4, &arrow)) {
                fopAcM_SetParam(arrow, 1);
            }
        }

        if (m06D3 == 9) {
            setHeadSplash();
        } else {
            mHeadSplashCb.remove();
        }

        if ((s8)m06D3 == 4 || (s8)m06D3 == 5 || (s8)m06D3 == 6) {
            setBowString(true);
        } else {
            setBowAnm(1, true);
            setBowString(false);
        }

        if (arg3 == 15) {
            setEyeAnm(eye_anm[(s8)m06D3]);
        } else {
            setEyeAnm(eye_anm[arg3]);
        }
    }

    dLib_bcks_setAnm(m_arc_name, mpMorf, &m06D2, &m06D3, &m06D4, a_anm_bcks_tbl, a_anm_prm_tbl,
                      arg2);
}

/* 0000246C-00002684       .text setAnmRunSpeed__6daPz_cFv */
void daPz_c::setAnmRunSpeed() {
    if (m06D3 == 3) {
        f32 speed = (current.pos - old.pos).abs();

        speed = speed / (10.0f + REG12_F(6));
        if (speed <= 0.0f) {
            speed = 0.0f;
        } else if (speed >= 1.0f) {
            speed = 1.0f;
        }

        speed *= l_HIO.m044;
        speed = cLib_minLimit(speed, l_HIO.m04C);
        speed = cLib_maxLimit(speed, l_HIO.m048);

        mpMorf->setPlaySpeed(speed);

        int frame = mpMorf->getFrame();
        if (frame == 7 || frame == 14) {
            static JGeometry::TVec3<f32> run_splash_scale(
                0.6f,
                0.6f,
                0.6f);

            int temp;
            JPABaseEmitter* emitter = dComIfGp_particle_setSimpleLand(
                mObjAcch.m_gnd, &current.pos, &shape_angle,
                1.25f,
                1.5f,
                1.0f, &tevStr, &temp, 7);
            if (emitter != NULL) {
                emitter->setRate(18.0f);
                emitter->setSpread(1.0f);
                emitter->setGlobalScale(run_splash_scale);
            }
        }
    }
}

/* 00002684-0000274C       .text setEyeBtp__6daPz_cFi */
void daPz_c::setEyeBtp(int i_resIdx) {
    J3DModel* model = mpMorf->getModel();
    J3DAnmTexPattern* btp = (J3DAnmTexPattern*)dComIfG_getObjectRes(m_arc_name, i_resIdx);
    JUT_ASSERT(0x683, btp != 0);

    mBtpFrame = 0;
    mBtpAnm.init(model->getModelData(), btp, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true);
}

/* 0000274C-00002810       .text setEyeBtk__6daPz_cFi */
void daPz_c::setEyeBtk(int i) {
    J3DModel* model = mpMorf->getModel();
    J3DAnmTextureSRTKey* btk = (J3DAnmTextureSRTKey*)dComIfG_getObjectRes(m_arc_name, i);
    JUT_ASSERT(0x691, btk != 0);

    mBtkAnm.init(model->getModelData(), btk, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, FALSE);
}

/* 00002810-00002888       .text setEyeAnm__6daPz_cFSc */
void daPz_c::setEyeAnm(s8 i_anm) {
    static const int a_eye_tbl[][2] = {
        {dRes_INDEX_PZ_BTP_MABA_A_e, dRes_INDEX_PZ_BTK_MABA_A_e},
        {dRes_INDEX_PZ_BTP_BOW_WAIT_e, dRes_INDEX_PZ_BTK_BOW_WAIT_e},
        {dRes_INDEX_PZ_BTP_DAM01_e, -1},
        {dRes_INDEX_PZ_BTP_DAM02_e, -1},
        {dRes_INDEX_PZ_BTP_STAND_e, dRes_INDEX_PZ_BTK_STAND_e},
        {dRes_INDEX_PZ_BTP_DEFEND_e, -1},
        {dRes_INDEX_PZ_BTP_WAIT04_e, -1},
        {dRes_INDEX_PZ_BTP_MABA_B_e, dRes_INDEX_PZ_BTK_MABA_A_e},
        {dRes_INDEX_PZ_BTP_TALK01_e, -1},
        {dRes_INDEX_PZ_BTP_TALK02_e, -1},
    };

    if (a_eye_tbl[i_anm][0] != -1) {
        setEyeBtp(a_eye_tbl[i_anm][0]);
    }
    
    if (a_eye_tbl[i_anm][1] != -1) {
        setEyeBtk(a_eye_tbl[i_anm][1]);
    }

    mCurEye = i_anm;
}

/* 00002888-00002AE0       .text ctrlEye__6daPz_cFv */
void daPz_c::ctrlEye() {
    mEyeTargetPos = m08C4;

    s16 angleX = cLib_targetAngleX(&mEyePos, &mEyeTargetPos);
    s16 angleY = cLib_targetAngleY(&mEyePos, &mEyeTargetPos);
    angleY -= (s16)(shape_angle.y + m_jnt.getHead_y() + m_jnt.getBackbone_y());
    angleX -= (s16)(m_jnt.getHead_x() + m_jnt.getBackbone_x());
    f32 valX = angleX / 8192.0f;
    f32 valY = angleY / 8192.0f;
    valX = 0.1f * valX;
    valY = 0.1f * valY;

    field_0x6dc = (u8)cLib_checkMinMaxLimit(valX, -0.1f, 0.1f);
    field_0x6dc = (u8)cLib_checkMinMaxLimit(valY, -0.1f, 0.1f);

    valX = cLib_minMaxLimit(valX, -0.1f, 0.1f);
    valY = cLib_minMaxLimit(valY, -0.1f, 0.1f);

    if (mMode == 3) {
        valY = 0.1f;
    }

    if (m08A8[0] != NULL) {
        cLib_addCalc(m08A8[0]->getNowOffsetX(), valY, 0.5f, 0.1f, 0.03f);
        cLib_addCalc(m08A8[0]->getNowOffsetY(), valX, 0.5f, 0.1f, 0.03f);
    }

    valY *= -1.0f;

    if (m08A8[1] != NULL) {
        cLib_addCalc(m08A8[1]->getNowOffsetX(), valY, 0.5f, 0.1f, 0.03f);
        cLib_addCalc(m08A8[1]->getNowOffsetY(), valX, 0.5f, 0.1f, 0.03f);
    }
}

/* 00002AE0-00002D38       .text playEyeAnm__6daPz_cFv */
void daPz_c::playEyeAnm() {
    bool doCtrlEye = true;

    if (mCurEye == 0 || mCurEye == 7) {
        if (cLib_calcTimer(&field_0x6d8) == 0) {
            mBtpFrame++;
            if ((f32)mBtpFrame > (f32)mBtpAnm.getFrameCtrl()->getEnd()) {
                field_0x6d8 = (s16)(100.0f + cM_rndF(100.0f));
                mBtpFrame = 0;
            }
        }
    } else if (mCurEye == 1 || mCurEye == 2 || mCurEye == 5 || mCurEye == 6) {
        mBtpFrame = 1;
    } else {
        mBtpFrame++;
        if ((f32)mBtpFrame > (f32)mBtpAnm.getFrameCtrl()->getEnd()) {
            field_0x6d8 = (s16)(100.0f + cM_rndF(100.0f));
            mBtpFrame = (f32)mBtpAnm.getFrameCtrl()->getEnd();
        }
    }

    if (mCurEye == 4) {
        doCtrlEye = false;
    }

    if (doCtrlEye) {
        for (int i = 0; i < 2; i++) {
            if (m08A8[i] != NULL) {
                m08A8[i]->setMoveFlag();
            }
        }
        ctrlEye();
    } else {
        for (int i = 0; i < 2; i++) {
            if (m08A8[i] != NULL) {
                m08A8[i]->clrMoveFlag();
            }
        }
        mBtkAnm.play();
    }
}

/* 00002D38-00002DC8       .text setMtx__6daPz_cFv */
void daPz_c::setMtx() {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::ZXYrotM(shape_angle);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
}

/* 00002DC8-00002E2C       .text modeWaitInit__6daPz_cFv */
void daPz_c::modeWaitInit() {
    m0924 = 0.0f;
    setAnm(1, false, 15);
    m_jnt.mbTrn = false;
    m08EA = 1;
    m_jnt.mbHeadLock = true;
    m_jnt.mbBackBoneLock = true;
    mTimer1 = 30;
}

#include "SSystem/SComponent/c_lib.h"

/* 00002E2C-00002FE8       .text modeWait__6daPz_cFv */
void daPz_c::modeWait() {
    m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);

    dEvent_manager_c* evtMgr = dComIfGp_getPEvtManager();

    if (evtMgr->getEventData(evtMgr->getEventIdx("btl_of_swroom", 0xff)) != NULL) {
        int staffId = evtMgr->getMyStaffId("p_zelda", NULL, 0);

        if (staffId != -1) {
            if (strcmp(evtMgr->getMyNowCutName(staffId), "Turn") == 0) {
                m08EA = 0;
                m_jnt.mbTrn = true;
                m_jnt.mbHeadLock = false;
                m_jnt.mbBackBoneLock = false;

                if (cLib_calcTimer(&mTimer1) == 0) {
                    evtMgr->cutEnd(staffId);
                }
            } else if (strcmp(evtMgr->getMyNowCutName(staffId), "Retire") == 0) {
                field_0x6d8 = 0;

                if (mBtpFrame >= REG8_S(0) + 4) {
                    mBtpFrame = REG8_S(0) + 4;
                    mEventIce.mLightShrinkTimer = 1;
                    field_0x6d8 = 1000;
                    evtMgr->cutEnd(staffId);
                }
            } else {
                if (mEventIce.mLightShrinkTimer == 0) {
                    field_0x6d8 = 1000;
                }
                m_jnt.mbTrn = false;
            }
        }
    } else {
        m_jnt.mbTrn = false;
        m08EA = 1;
        m_jnt.mbHeadLock = true;
        m_jnt.mbBackBoneLock = true;
    }
}

#include "SSystem/SComponent/c_lib.h"
#include "SSystem/SComponent/c_math.h"
#include "d/d_lib.h"

/* 00002FE8-000031E8       .text modeMoveInit__6daPz_cFv */
void daPz_c::modeMoveInit() {
    cLib_onBit<u32>(attention_info.flags, fopAc_Attn_ACTION_SPEAK_e);
    cLib_onBit<u32>(attention_info.flags, fopAc_Attn_LOCKON_TALK_e);

    f32 rnd = cM_rndF(l_HIO.m08C[m08B0]);
    mTimer1 = l_HIO.m086[m08B0] + rnd;
    mTimer2 = 10;
    mTimer3 = 0x78;

    setAnm(3, false, 15);

    if (cM_rndF(100.0f) < l_HIO.m0D8) {
        if (m0920 == 1) {
            m0920 = -1;
        } else {
            m0920 = 1;
        }
    }

    if (l_HIO.m030 == 0) {
        if (mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    } else {
        m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
    }

    mCirclePath.mRadius = l_HIO.m0A0[m08B0];
    mCirclePath.mWobbleAmplitude = l_HIO.m0AC;
    mCirclePath.mAngleSpeed = (REG12_S(0) + 0x150) * m0920;

    mCirclePath.mTranslation = m08C4;
    dLib_setCirclePath(&mCirclePath);

    m08EA = 0;
    m_jnt.clrTrn();
    m_jnt.offHeadLock();
    m_jnt.offBackBoneLock();
}

/* 000031E8-0000395C       .text modeMove__6daPz_cFv */
void daPz_c::modeMove() {
    if (l_HIO.m032 != 0) {
        l_HIO.m032 = 0;
        modeProc(PROC_INIT_e, 7);
        return;
    }
    if (l_HIO.m034[0] != 0) {
        l_HIO.m034[0] = 0;
        modeProc(PROC_INIT_e, 8);
        return;
    }

    bool wallHit = false;
    mLinChk.Set(&current.pos, &mCirclePath.mPos, this);
    if (dComIfG_Bgsp()->LineCross(&mLinChk)) {
        wallHit = true;
    }
    if (mObjAcch.ChkWallHit()) {
        wallHit = true;
    }

    if (cLib_calcTimer(&m08F8) == 0 && wallHit) {
        m08F8 = 60;
        if (m0920 == 1) {
            m0920 = -1;
        } else {
            m0920 = 1;
        }
        mCirclePath.mAngleSpeed = (REG12_S(0) + 0x4000) * m0920;
        if (cLib_calcTimer(&mTimer3) == 0) {
            dLib_setCirclePath(&mCirclePath);
            modeProc(PROC_INIT_e, 7);
        }
    }

    if (checkTgHit()) {
        return;
    }

    if (l_HIO.m030 == 0) {
        if (mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    } else {
        m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
    }

    m_jnt.clrTrn();
    cLib_addCalc2(&mCirclePath.mRadius, l_HIO.m0A0[m08B0], 0.1f, 10.0f);

    f32 dist = (current.pos - mCirclePath.mPos).absXZ();
    f32 dist2 = (current.pos - m08C4).absXZ();
    f32 dist3 = (m08C4 - mCirclePath.mPos).absXZ();

    if (dist > 200.0f + REG12_F(7) || !mbEyesFollowGanondorf) {
        m0924 = l_HIO.m050;
    } else {
        m0924 = 0.0f;
    }

    if (wallHit || dist <= 200.0f || dist3 >= 2.0f * l_HIO.m0A0[m08B0] || mbEyesFollowGanondorf) {
        mCirclePath.mWobbleAmplitude = l_HIO.m0AC;
        mCirclePath.mAngleSpeed = (REG12_S(0) + 0x150) * m0920;
        mCirclePath.mTranslation = m08C4;
        dLib_setCirclePath(&mCirclePath);
    }

    if (cLib_calcTimer(&mTimer2) == 0) {
        f32 ganonDist = 100000.0f;
        f32 targetDist = (current.pos - m08C4).absXZ();
        if (mbHasGanondorf) {
            ganonDist = (current.pos - mGanondorfPosCurrent).absXZ();
        }
        if (mbEyesFollowGanondorf) {
            if (targetDist < l_HIO.m0D0 || ganonDist < l_HIO.m0D0) {
                field_0x6c8 = mMode;
                modeProc(PROC_INIT_e, 8);
                return;
            }
        }
    }

    cLib_addCalcAngleS2(&shape_angle.y, cLib_targetAngleY(&current.pos, &mCirclePath.mPos), 8, 0x400);

    if (speedF < 0.5f) {
        setAnm(2, false, 15);
    } else {
        setAnm(3, false, 15);
    }

    if (m08B0 == 2) {
        fopAc_ac_c* player = dComIfGp_getLinkPlayer();
        m0F81 = dLib_checkActorInFan(player->current.pos, this, player->shape_angle.y, 0x2500, 30000.0f, 1000.0f);
    }

    if (l_HIO.m034[1] == 0 && cLib_calcTimer(&mTimer1) == 0) {
        if (m08B0 == 0) {
            modeProc(PROC_INIT_e, 2);
        } else if (m08B0 == 2) {
            if (m0F65 == 0) {
                m0F81 = 1;
            }
            if (m0F81) {
                modeProc(PROC_INIT_e, 2);
            }
        }
    }

    m0F82 = 2;
}

/* 0000395C-000039C0       .text modeAttackWaitInit__6daPz_cFv */
void daPz_c::modeAttackWaitInit() {
    m0924 = 0.0f;
    mTimer1 = 20;
    mTimer2 = 10;
    setAnm(2, false, 15);
    m08EA = 0;
    m_jnt.mbHeadLock = false;
    m_jnt.mbBackBoneLock = false;
}

#include "f_op/f_op_actor_mng.h"

/* 000039C0-00003D88       .text modeAttackWait__6daPz_cFv */
void daPz_c::modeAttackWait() {
    if (checkTgHit()) {
        return;
    }

    if (l_HIO.m030 == 0) {
        if (mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    } else {
        m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
    }

    if (cLib_calcTimer(&mTimer2) == 0) {
        f32 dist = (current.pos - m08C4).absXZ();
        f32 ganonDist = 100000.0f;
        if (mbHasGanondorf) {
            ganonDist = (current.pos - mGanondorfPosCurrent).absXZ();
        }

        if (mbEyesFollowGanondorf) {
            if (dist < l_HIO.m0D0 || ganonDist < l_HIO.m0D0) {
                field_0x6c8 = mMode;
                if (cM_rndF(100.0f) < 50.0f) {
                    modeProc(PROC_INIT_e, 8);
                } else {
                    modeProc(PROC_INIT_e, 7);
                }
                return;
            }
        }
    }

    if (m08B0 == 0) {
        fopAc_ac_c* gnd;
        if (fopAcM_SearchByName(fpcNm_GND_e, &gnd) && gnd->health > 60 && dComIfGs_getLife() >= 12 && m075C) {
            m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
        }
        if (m0768 && mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    } else {
        if (m075C) {
            m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
        }
        if (m0768 && mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    }

    if (l_HIO.m02D != 0) {
        if (cLib_calcTimer(&mTimer1) == 0) {
            modeProc((Proc_e)0, 3);
        }
    }

    m_jnt.mbTrn = true;
}

#include "SSystem/SComponent/c_math.h"

/* 00003D88-00003F14       .text modeAttackInit__6daPz_cFv */
void daPz_c::modeAttackInit() {
    mTimer3 = 30;

    s32 idx = m08B0;
    if (m0F65 == 0) {
        idx = 0;
    }

    if ((s8)m06D3 != 4 && (s8)m06D3 != 5) {
        mTimer1 = l_HIO.m092[idx] + cM_rndF(l_HIO.m098[idx]);
        mTimer2 = l_HIO.m080[idx];
        setAnm(4, false, 15);
    }

    if (cM_rndF(100.0f) < l_HIO.m068[idx]) {
        m0F48 = 1;
    } else {
        m0F48 = 0;

        if (cM_rndF(100.0f) < l_HIO.m074[idx]) {
            m0F44 = 0.0f;
        } else {
            m0F44 = cM_rndF(l_HIO.m05C[idx]);
        }
    }

    m08EA = 0;
    m_jnt.onHeadLock();
    m_jnt.offBackBoneLock();
}

/* 00003F14-0000445C       .text modeAttack__6daPz_cFv */
void daPz_c::modeAttack() {
    if (checkTgHit()) {
        return;
    }

    cXyz scale(m0F44, m0F44, m0F44);

    if (m0F48 == 0) {
        if (mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    } else {
        m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
    }

    if (m08B0 == 0) {
        fopAc_ac_c* gnd;
        if (fopAcM_SearchByName(fpcNm_GND_e, &gnd) && gnd->health > 60 && dComIfGs_getLife() >= 12 && m075C) {
            m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
        }
        if (m0768 && mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    } else {
        if (m075C) {
            m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
        }
        if (m0768 && mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    }

    if ((m06D3 == 4 || m06D3 == 5) && cLib_calcTimer(&mTimer3) == 0) {
        f32 dist = (current.pos - m08C4).absXZ();
        f32 ganonDist = 100000.0f;
        if (mbHasGanondorf) {
            ganonDist = (current.pos - mGanondorfPosCurrent).absXZ();
        }
        if (mbEyesFollowGanondorf) {
            if (dist < l_HIO.m0D0 || ganonDist < l_HIO.m0D0) {
                field_0x6c8 = mMode;
                if (cM_rndF(100.0f) < 50.0f) {
                    modeProc(PROC_INIT_e, 8);
                } else {
                    modeProc(PROC_INIT_e, 7);
                }
                return;
            }
        }
    }

    if (m06D3 == 4 && mpMorf->isStop()) {
        m_jnt.setTrn();
        setAnm(5, false, 15);
    } else if (m06D3 == 5) {
        m_jnt.setTrn();
        if (cLib_calcTimer(&mTimer1) == 0) {
            setAnm(6, false, 15);
        }
    } else if (m06D3 == 6 && mpMorf->isStop()) {
        m_jnt.clrTrn();
        if (m075C) {
            m0758--;
            if (cLib_calcTimer(&m0758)) {
                modeProc(PROC_INIT_e, 2);
            } else {
                m075C = 0;
            }
        } else if (m0768) {
            m0764--;
            if (cLib_calcTimer(&m0764)) {
                modeProc(PROC_INIT_e, 2);
            } else {
                m0768 = 0;
            }
        } else if (cLib_calcTimer(&mTimer2) == 0) {
            modeProc(PROC_INIT_e, 1);
        } else {
            m08EA = 1;
        }
    }
}

/* 0000445C-000044B4       .text modeDefendInit__6daPz_cFv */
void daPz_c::modeDefendInit() {
    setAnm(10, true, 15);
    mTimer1 = 30;
    m_jnt.mbTrn = false;
    m_jnt.mbHeadLock = true;
    m_jnt.mbBackBoneLock = true;
    m08EA = 1;
}

#include "SSystem/SComponent/c_lib.h"
#include "m_Do/m_Do_mtx.h"
#include "d/d_com_inf_game.h"

/* 000044B4-00004630       .text modeDefend__6daPz_cFv */
void daPz_c::modeDefend() {
    m0924 = 0.0f;
    m_jnt.mbTrn = false;

    s16 target_angle = cLib_targetAngleY(&current.pos, &mHitPos);
    cLib_addCalcAngleS2(&shape_angle.y, target_angle, 4, 0x1000);
    cLib_distanceAngleS(shape_angle.y, target_angle);

    if (m0F68 > 0.01f) {
        cXyz offset(0.0f, 0.0f, -(m0F68));
        cXyz move(0.0f, 0.0f, 0.0f);

        mDoMtx_stack_c::YrotS(m0F6C);
        mDoMtx_stack_c::multVec(&offset, &move);
        current.pos += move;

        cLib_addCalc0(&m0F68, 1.0f, 7.0f);

        int temp;
        dComIfGp_particle_setSimpleLand(mObjAcch.m_gnd, &current.pos, &shape_angle, 1.25f, 1.5f,
                                        1.0f, &tevStr, &temp, 7);
    } else {
        if (m075C || m0768) {
            modeProc((Proc_e)0, 2);
        } else if (cLib_calcTimer(&mTimer1) == 0) {
            modeProc((Proc_e)0, 1);
        }
    }

    if (checkTgHit()) {
        return;
    }
}

#include "f_op/f_op_actor_mng.h"
#include "JAZelAudio/JAZelAudio_SE.h"

/* 00004630-0000470C       .text modeDownInit__6daPz_cFv */
void daPz_c::modeDownInit() {
    cLib_offBit<u32>(attention_info.flags, fopAc_Attn_ACTION_SPEAK_e);
    cLib_offBit<u32>(attention_info.flags, fopAc_Attn_LOCKON_TALK_e);
    setAnm(7, true, 15);
    fopAcM_monsSeStart(this, JA_SE_CV_ZL_GN_DAMAGE, 0);
    m08EA = 1;
    m0924 = 0.0f;
    speedF = 0.0f;
    m08EA = 0;
    m_jnt.clrTrn();
    m_jnt.onHeadLock();
    m_jnt.onBackBoneLock();
    speed.y = l_HIO.m0C4;
    m0F70 = l_HIO.m0C8;
    mTimer1 = l_HIO.m0CC;
}

#include "d/d_com_inf_game.h"
#include "d/d_s_play.h"
#include "m_Do/m_Do_mtx.h"
#include "SSystem/SComponent/c_lib.h"
#include "JSystem/JParticle/JPAEmitter.h"

/* 0000470C-00004C20       .text modeDown__6daPz_cFv */
void daPz_c::modeDown() {
    int temp;
    m0924 = 0.0f;
    speedF = 0.0f;
    m_jnt.mbTrn = false;

    if (m06D3 == 7 && mpMorf->getMorfRatio() == 1.0f && mObjAcch.ChkGroundHit()) {
        setAnm(8, false, 15);
    }

    if (cLib_calcTimer(&mTimer1) != 0) {
        cXyz offset(m0F70, 0.0f, 0.0f);
        cXyz move(0.0f, 0.0f, 0.0f);

        mDoMtx_stack_c::YrotS(shape_angle.y);
        mDoMtx_stack_c::multVec(&offset, &move);
        current.pos += move;

        if (m06D3 == 8 && mpMorf->isStop()) {
            dComIfGp_particle_setSimpleLand(mObjAcch.m_gnd, &current.pos, &shape_angle, 1.25f,
                                            1.5f, 1.0f, &tevStr, &temp, 7);
        }
    }

    int frame = mpMorf->getFrame();

    if (m06D3 == 8) {
        if (frame == 1.0f + REG12_F(10)) {
            static JGeometry::TVec3<f32> run_splash_scale(0.6f, 0.6f, 0.6f);
            JPABaseEmitter* emitter = dComIfGp_particle_setSimpleLand(
                mObjAcch.m_gnd, &current.pos, &shape_angle, 2.5f, 3.0f, 2.0f, &tevStr, &temp, 7);
            if (emitter != NULL) {
                emitter->setRate(18.0f);
                emitter->setSpread(1.0f);
                emitter->setGlobalScale(run_splash_scale);
            }
        }

        if (frame == 2.0f + REG12_F(10)) {
            static JGeometry::TVec3<f32> run_splash_scale(0.6f, 0.6f, 0.6f);
            JPABaseEmitter* emitter = dComIfGp_particle_setSimpleLand(
                mObjAcch.m_gnd, &mWaistPos, &shape_angle, 2.5f, 3.0f, 2.0f, &tevStr,
                &temp, 7);
            if (emitter != NULL) {
                emitter->setRate(18.0f);
                emitter->setSpread(1.0f);
                emitter->setGlobalScale(run_splash_scale);
            }
        }

        if (frame == 3.0f + REG12_F(10)) {
            static JGeometry::TVec3<f32> run_splash_scale(0.6f, 0.6f, 0.6f);
            JPABaseEmitter* emitter = dComIfGp_particle_setSimpleLand(
                mObjAcch.m_gnd, &mEyePos, &shape_angle, 2.5f, 3.0f, 2.0f, &tevStr,
                &temp, 7);
            if (emitter != NULL) {
                emitter->setRate(18.0f);
                emitter->setSpread(1.0f);
                emitter->setGlobalScale(run_splash_scale);
            }
        }
    }

    if (m06D3 == 8 && mpMorf->isStop()) {
        m08EA = 1;
    }

    if (m08B0 != 1) {
        if (m06D3 == 9) {
            if (mpMorf->isStop()) {
                m08EA = 0;
                setAnm(2, false, 15);
            }
        } else if (m0740) {
            m0740 = 0;
            m0F7C = l_HIO.m0F8[2];
            m0F80 = 0;
            m075C = 0;
            m0768 = 0;
            modeProc((Proc_e)0, 1);
        } else {
            setAnm(9, false, 15);
        }
    }
}

/* 00004C20-00004C78       .text modeAfraidInit__6daPz_cFv */
void daPz_c::modeAfraidInit() {
    setAnm(11, true, 15);
    m0924 = 0.0f;
    speedF = 0.0f;
    m08EA = 0;
    m_jnt.mbHeadLock = false;
    m_jnt.mbBackBoneLock = false;
}

/* 00004C78-00004E8C       .text modeAfraid__6daPz_cFv */
void daPz_c::modeAfraid() {
    m0924 = 0.0f;
    speedF = 0.0f;
    m08EA = 0;
    m_jnt.mbTrn = false;
    m073F = 0;
    m_jnt.mbHeadLock = false;
    m_jnt.mbBackBoneLock = false;

    fopAc_ac_c* target;
    if (fopAcM_SearchByName(fpcNm_GND_e, &target)) {
        if (REG12_S(9) == 1) {
            m08C4 = target->current.pos;
            m08C4.y += 1200.0f + REG12_F(10);
        } else if (REG12_S(9) == 2) {
            m08C4 = target->eyePos;
            m08C4.y += 800.0f + REG12_F(10);
        } else {
            m08C4 = target->current.pos;

            f32 dist = (current.pos - target->current.pos).absXZ();
            f32 minY = 50.0f + target->current.pos.y;
            m08C4.y = 8.0f * (target->eyePos.y - dist / 2.0f);
            if (m08C4.y <= minY) {
                m08C4.y = minY;
            }
        }

        s16 angle = cLib_targetAngleY(&current.pos, &target->current.pos);
        cLib_addCalcAngleS2(&shape_angle.y, angle, 4, 0x400);
    }
}

/* 00004E8C-00004F08       .text modeSideStepInit__6daPz_cFv */
void daPz_c::modeSideStepInit() {
    if (field_0x6c8 != 2 && field_0x6c8 != 3) {
        setAnm(11, true, 15);
    }

    m0924 = 0.0f;
    speedF = 0.0f;
    m08EA = 0;

    speed.y = l_HIO.m0BC;
    m0F70 = l_HIO.m0C0;
}

/* 00004F08-00005098       .text modeSideStep__6daPz_cFv */
void daPz_c::modeSideStep() {
    if (checkTgHit()) {
        field_0xf7a = 0;
        return;
    }

    m0924 = 0.0f;
    speedF = 0.0f;
    m08EA = 0;
    m_jnt.mbTrn = true;
    m073F = 0;

    cLib_addCalcAngleS2(&field_0xf7a, m0920 * 0x1194, 4, 0x800);

    s16 angle = cLib_targetAngleY(&mEyePos, &m08C4);
    cXyz offset(m0F70 * (f32)m0920, 0.0f, 0.0f);
    cXyz move(0.0f, 0.0f, 0.0f);

    mDoMtx_stack_c::YrotS(angle);
    mDoMtx_stack_c::multVec(&offset, &move);
    current.pos += move;

    if (mObjAcch.ChkGroundHit()) {
        field_0xf7a = 0;

        int temp;
        dComIfGp_particle_setSimpleLand(mObjAcch.m_gnd, &current.pos, &shape_angle, 1.25f, 1.5f,
                                        1.0f, &tevStr, &temp, 7);

        if (field_0x6c8 == 2 || field_0x6c8 == 3) {
            modeProc((Proc_e)0, 3);
        } else {
            modeProc((Proc_e)0, 1);
        }
    }
}

/* 00005098-00005114       .text modeBackStepInit__6daPz_cFv */
void daPz_c::modeBackStepInit() {
    if (field_0x6c8 != 2 && field_0x6c8 != 3) {
        setAnm(11, true, 15);
    }

    m0924 = 0.0f;
    speedF = 0.0f;
    m08EA = 0;

    speed.y = l_HIO.m0B4;
    m0F70 = l_HIO.m0B8;
}

/* 00005114-0000527C       .text modeBackStep__6daPz_cFv */
void daPz_c::modeBackStep() {
    if (checkTgHit()) {
        field_0xf78 = 0;
        return;
    }

    m0924 = 0.0f;
    speedF = 0.0f;
    m08EA = 0;
    m_jnt.mbTrn = true;
    m073F = 0;

    cLib_addCalcAngleS2(&field_0xf78, -0x1194, 4, 0x800);

    s16 angle = cLib_targetAngleY(&current.pos, &m08C4);

    cXyz offset(0.0f,
                0.0f, -m0F70);
    cXyz move(0.0f,
              0.0f,
              0.0f);

    mDoMtx_stack_c::YrotS(angle);
    mDoMtx_stack_c::multVec(&offset, &move);
    current.pos += move;

    if (mObjAcch.ChkGroundHit()) {
        field_0xf78 = 0;

        int temp;
        dComIfGp_particle_setSimpleLand(mObjAcch.m_gnd, &current.pos, &shape_angle,
                                        1.25f,
                                        1.5f,
                                        1.0f, &tevStr,
                                        &temp, 7);

        if (field_0x6c8 == 2 || field_0x6c8 == 3) {
            modeProc((Proc_e)0, 3);
        } else {
            modeProc((Proc_e)0, 1);
        }
    }
}

/* 0000527C-00005304       .text modeTalkInit__6daPz_cFv */
void daPz_c::modeTalkInit() {
    setAnm(2, true, 15);
    speedF = 0.0f;
    m0924 = 0.0f;

    m08EA = 0;
    m_jnt.offHeadLock();
    m_jnt.offBackBoneLock();

    if (m08B0 == 0) {
        m0F80 = 1;
    } else {
        m0F7C = l_HIO.m0F8[m08B0];
    }
}

/* 00005304-0000539C       .text modeTalk__6daPz_cFv */
void daPz_c::modeTalk() {
    m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
    m_jnt.setTrn();

    if (talk(1) == 0x12) {
        dComIfGp_event_onEventFlag(8);
        modeProc((Proc_e)0, 2);
    }
}

/* 0000539C-0000549C       .text modeFollowInit__6daPz_cFv */
void daPz_c::modeFollowInit() {
    cLib_onBit<u32>(attention_info.flags, fopAc_Attn_ACTION_SPEAK_e);
    cLib_onBit<u32>(attention_info.flags, fopAc_Attn_LOCKON_TALK_e);

    f32 rnd = cM_rndF(l_HIO.m08C[m08B0]);
    mTimer1 = l_HIO.m086[m08B0] + rnd;
    mTimer2 = 10;
    mTimer3 = 0x78;

    setAnm(3, false, 15);

    m08EA = 0;
    m_jnt.clrTrn();
    m_jnt.offHeadLock();
    m_jnt.offBackBoneLock();
}

/* 0000549C-000059B8       .text modeFollow__6daPz_cFv */
void daPz_c::modeFollow() {
    bool wallHit = false;
    mLinChk.Set(&current.pos, &m0830, this);
    if (dComIfG_Bgsp()->LineCross(&mLinChk)) {
        wallHit = true;
    }
    if (mObjAcch.ChkWallHit()) {
        wallHit = true;
    }

    if (checkTgHit()) {
        return;
    }

    if (l_HIO.m030 == 0) {
        if (mbHasGanondorf) {
            m08C4 = mGanondorfPos4;
        }
    } else {
        m08C4 = dNpc_playerEyePos(l_HIO.mNpcHIO.m04);
    }

    m_jnt.clrTrn();

    f32 dist = (current.pos - m0830).absXZ();
    if (dist > 200.0f && !mbEyesFollowGanondorf && !wallHit) {
        m0924 = l_HIO.m050;
    } else if (mbEyesFollowGanondorf) {
        m0924 = 0.0f;
    } else if (wallHit) {
        m0924 = 2.0f;
    } else {
        m0924 = 1.0f;
    }

    f32 radius = l_HIO.m0EC;
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    m0830 = mGanondorfPosCurrent;
    s16 angle = cLib_targetAngleY(&player->current.pos, &mGanondorfPosCurrent);
    cLib_distanceAngleS(angle, shape_angle.y);
    m0830.x += radius * cM_ssin(REG12_S(0) + (angle + 0x4000));
    m0830.z += radius * cM_scos(REG12_S(0) + (angle + 0x4000));
    m0830.y += 200.0f;

    if (cLib_calcTimer(&mTimer2) == 0) {
        f32 ganonDist = 100000.0f;
        f32 targetDist = (current.pos - m08C4).absXZ();
        if (mbHasGanondorf) {
            ganonDist = (current.pos - mGanondorfPosCurrent).absXZ();
        }
        if (mbEyesFollowGanondorf) {
            if (targetDist < l_HIO.m0D0 || ganonDist < l_HIO.m0D0) {
                field_0x6c8 = mMode;
                modeProc(PROC_INIT_e, 8);
                return;
            }
        }
    }

    cLib_addCalcAngleS2(&shape_angle.y, cLib_targetAngleY(&current.pos, &m0830), 8, 0x400);

    if (speedF < 0.5f) {
        setAnm(2, false, 15);
    } else {
        setAnm(3, false, 15);
    }

    if (l_HIO.m034[1] == 0 && cLib_calcTimer(&mTimer1) == 0) {
        modeProc(PROC_INIT_e, 2);
    }

    m0F82 = 2;
}

/* 000059B8-00005C58       .text modeProc__6daPz_cFQ26daPz_c6Proc_ei */
void daPz_c::modeProc(daPz_c::Proc_e proc, int newMode) {
    typedef void (daPz_c::*ModeFunc)();
    struct ModeEntry {
        ModeFunc init;
        ModeFunc run;
        const char* name;
    };

    static ModeEntry mode_tbl[] = {
        {
            &daPz_c::modeWaitInit,
            &daPz_c::modeWait,
            "WAIT",
        },
        {
            &daPz_c::modeMoveInit,
            &daPz_c::modeMove,
            "MOVE",
        },
        {
            &daPz_c::modeAttackWaitInit,
            &daPz_c::modeAttackWait,
            "ATTACKWAIT",
        },
        {
            &daPz_c::modeAttackInit,
            &daPz_c::modeAttack,
            "ATTACK",
        },
        {
            &daPz_c::modeDefendInit,
            &daPz_c::modeDefend,
            "DEFEND",
        },
        {
            &daPz_c::modeDownInit,
            &daPz_c::modeDown,
            "DOWN",
        },
        {
            &daPz_c::modeAfraidInit,
            &daPz_c::modeAfraid,
            "AFRAID",
        },
        {
            &daPz_c::modeSideStepInit,
            &daPz_c::modeSideStep,
            "SIDE_STEP",
        },
        {
            &daPz_c::modeBackStepInit,
            &daPz_c::modeBackStep,
            "BACK_STEP",
        },
        {
            &daPz_c::modeTalkInit,
            &daPz_c::modeTalk,
            "TALK",
        },
        {
            &daPz_c::modeFollowInit,
            &daPz_c::modeFollow,
            "FOLLOW",
        },
    };

    if (proc == daPz_c::PROC_INIT_e) {
        mMode = newMode;
        (this->*mode_tbl[mMode].init)();
    } else if (proc == daPz_c::PROC_EXEC_e) {
        (this->*mode_tbl[mMode].run)();
    }
}

/* 00005C58-000060D8       .text _execute__6daPz_cFv */
bool daPz_c::_execute() {
    if (l_HIO.m033 != 0) {
        modeProc(PROC_INIT_e, 0);
        fopAc_ac_c* player = dComIfGp_getLinkPlayer();
        current.pos = player->current.pos;
        current.pos.y += 50.0f;
        current.pos.x -= 200.0f * cM_ssin(player->shape_angle.y);
        current.pos.z -= 200.0f * cM_scos(player->shape_angle.y);
    }

    setFallSplash();
    getGndPos();

    mbEyesFollowGanondorf = false;
    if (mbHasGanondorf) {
        mbEyesFollowGanondorf = checkEyeArea(mGanondorfPosCurrent);
    }

    gravity = l_HIO.m0B0;

    if (mMode != 6 && mMode != 5 && m073F) {
        modeProc(PROC_INIT_e, 6);
    }

    setRipple();

    if (mMode != 5 && m08B0 == 1) {
        modeProc(PROC_INIT_e, 5);
    }

    setJntStatus();

    if (demo()) {
        mBtkAnm.play();
        mBrkAnm.play();
        setMtx();
        g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
        return true;
    }

    current.angle = shape_angle;
    mpMorf->calc();
    enemy_fire(&mEnemyFire);
    if (enemy_ice(&mEventIce)) {
        mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
        return true;
    }

    if (m06D3 == 9) {
        setHeadSplash();
    }

    if (mArg == 0 && (m08B0 == 0 || m08B0 == 2) && m0F80 == 0 && mMode != 2 && mMode != 3 && mMode != 5 && mMode != 9 &&
        cLib_calcTimer(&m0F7C) == 0)
    {
        fopAc_ac_c* actor;
        if (fopAcM_SearchByName(fpcNm_GND_e, &actor)) {
            gnd_class* gnd = (gnd_class*)actor;
            if (fopAcM_searchActorDistanceXZ(this, dComIfGp_getPlayer(0)) < l_HIO.m100[m08B0] && gnd->mAction == 0) {
                m0F82 = 1;
                modeProc(PROC_INIT_e, 9);
            }
        }
    }

    checkOrder();
    modeProc(PROC_EXEC_e, 11);
    eventOrder();
    setAttention();

    cLib_addCalc2(&speedF, m0924, 0.3f, 4.0f);

    mpMorf->play(&eyePos, mObjAcch.ChkGroundHit() ? dComIfG_Bgsp()->GetMtrlSndId(mObjAcch.m_gnd) : 0,
                 dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    mpBowMcaMorf->play(NULL, 0, 0);
    mBrkAnm.play();
    playEyeAnm();

    if (l_HIO.m02F != 0) {
        setAnm(l_HIO.m02F, false, 15);
    }

    fopAcM_posMoveF(this, NULL);
    mObjAcch.CrrPos(*dComIfG_Bgsp());

    if (!dComIfGp_event_runCheck()) {
        setCollision(30.0f, 130.0f);
    }

    setAnmRunSpeed();
    current.angle = shape_angle;
    setMtx();
    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    return true;
}

static void daPz_debugDraw(daPz_c* i_this) {
    static const GXColor color_ok = {0x00, 0xFF, 0x00, 0x80};
    static const GXColor color_ng = {0xFF, 0x00, 0x00, 0x80};

    cXyz pos = i_this->current.pos;
    dDbVw_drawCircleXlu(pos, l_HIO.m058, i_this->mbEyesFollowGanondorf ? color_ok : color_ng, 1, 1);

    GXColor color0 = {0xFF, 0xFF, 0x00, 0x80};
    dDbVw_drawCircleXlu(pos, l_HIO.m0D0, color0, 1, 1);
    GXColor color1 = {0xFF, 0x00, 0x00, 0x80};
    dDbVw_drawCircleXlu(pos, l_HIO.m0EC, color1, 1, 1);
    GXColor color2 = {0xFF, 0xFF, 0x00, 0x80};
    dDbVw_drawCircleXlu(i_this->m0830, l_HIO.m0AC, color2, 1, 1);
    GXColor color3 = {0x00, 0x00, 0xFF, 0x80};
    dDbVw_drawCircleXlu(i_this->mCirclePath.mPos, i_this->mCirclePath.mRadius, color3, 1, 1);
    GXColor color4 = {0xFF, 0x00, 0xFF, 0x80};
    dDbVw_drawCircleXlu(i_this->m08C4, 10000.0f, color4, 1, 1);
}

/* 000060D8-00006154       .text bowDraw__6daPz_cFv */
void daPz_c::bowDraw() {
    g_env_light.setLightTevColorType(mpBowMcaMorf->getModel(), &tevStr);
    mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(PZ_JNT_HAND_L1_e));
    mpBowMcaMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpBowMcaMorf->updateDL();
}

/* 00006154-00006600       .text bodyDraw__6daPz_cFv */
void daPz_c::bodyDraw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();

    g_env_light.setLightTevColorType(model, &tevStr);
    mpMorf->calc();
    model->unlock();

    if (mArg != 0) {
        mBrkAnm.entry(modelData);
    }

    dComIfGd_setListP0();
    mBtpAnm.entry(modelData, mBtpFrame);
    mOnCupOffAupPacket2.entryOpa();

    for (u16 i = 0; i < modelData->getMaterialNum(); i++) {
        J3DMaterial* mat = modelData->getMaterialNodePointer(i);
        if (i != 3 && i != 6 && i != 13 && i != 16) {
            mat->getShape()->hide();
        } else {
            mat->getShape()->show();
        }
    }
    field_0xfd0->entryIn();

    mOffCupOnAupPacket2.entryOpa();
    mBtkAnm.entry(modelData);

    field_0x1014[0]->hide();
    field_0x1014[3]->hide();
    field_0x1014[2]->show();
    field_0x1014[5]->show();
    field_0x102c[0]->hide();
    field_0x102c[3]->hide();
    field_0x102c[2]->show();
    field_0x102c[5]->show();
    field_0xfd0->entryIn();
    mBtkAnm.remove(modelData);

    field_0x1014[2]->hide();
    field_0x1014[5]->hide();
    field_0x102c[2]->hide();
    field_0x102c[5]->hide();
    for (int i = 0; i < 4; i++) {
        field_0x1044[i]->show();
    }
    field_0xfd0->entryIn();

    mOnCupOffAupPacket1.entryOpa();
    for (int i = 0; i < 4; i++) {
        field_0x1044[i]->hide();
    }
    field_0x1014[1]->show();
    field_0x1014[4]->show();
    field_0x102c[1]->show();
    field_0x102c[4]->show();
    field_0xfd0->entryIn();

    mOffCupOnAupPacket1.entryOpa();
    field_0x1014[1]->hide();
    field_0x1014[4]->hide();
    field_0x102c[1]->hide();
    field_0x102c[4]->hide();

    dComIfGd_setListP1();
    for (u16 i = 0; i < modelData->getMaterialNum(); i++) {
        if (i != 3 && i != 2 && i != 1 && i != 6 && i != 5 && i != 4 && i != 13 && i != 12 && i != 11 && i != 16 &&
            i != 15 && i != 14 && i != 18 && i != 19 && i != 8 && i != 7)
        {
            modelData->getMaterialNodePointer(i)->getShape()->show();
        }
    }
    mpMorf->entryDL();

    dComIfGd_setList();
    for (u16 i = 0; i < modelData->getMaterialNum(); i++) {
        modelData->getMaterialNodePointer(i)->getShape()->show();
    }

    mBtpAnm.remove(modelData);
    if (mArg != 0) {
        mBrkAnm.remove(modelData);
    }
}

/* 00006600-000066D8       .text drawShadow__6daPz_cFv */
void daPz_c::drawShadow() {
    cXyz shadow_pos(current.pos.x,
                     current.pos.y + 100.0f + REG8_F(18),
                     current.pos.z);

    m0774 = dComIfGd_setShadow(
        m0774, 1, mpMorf->getModel(), &shadow_pos,
        250.0f + REG8_F(19),
        40.0f, current.pos.y, mObjAcch.GetGroundH(),
        mObjAcch.m_gnd, &tevStr, 0, 1.0f,
        dDlst_shadowControl_c::getSimpleTex());

    if (m0F88 != 0 && m0774 != 0) {
        dComIfGd_addRealShadow(m0774, mpBowMcaMorf->getModel());
    }
}

#include "d/d_snap.h"
#include "d/d_material.h"

/* 000066D8-0000676C       .text _draw__6daPz_cFv */
bool daPz_c::_draw() {
    if (mEventIce.mFreezeTimer > 20) {
        dMat_control_c::iceEntryDL(mpMorf, -1, &mInvisibleModel);
    } else {
        bodyDraw();
    }

    if (m0F88 != 0) {
        bowDraw();
    }

    drawShadow();

    dSnap_RegistFig(DSNAP_TYPE_UNK87, this, 1.0f, 1.0f, 1.0f);

    return true;
}

/* 0000676C-00006974       .text bodyCreateInit__6daPz_cFv */
void daPz_c::bodyCreateInit() {
    static u8 l_eyeMatNo[] = { 3, 2, 1, 6, 5, 4 };
    static u8 l_eyeblowMatNo[] = { 13, 12, 11, 16, 15, 14 };
    static u8 l_faceMatNo[] = { 18, 19, 8, 7 };

    if (strcmp(dComIfGp_getStartStageName(), "GTower") == 0) {
        eventInfo.setEventId(dComIfGp_evmng_getEventIdx("PZ_TALK"));
    }

    J3DModelData* modelData = mpMorf->getModel()->getModelData();
    field_0xfd0 = modelData->getJointNodePointer(0);

    for (int i = 0; i < 6; i++) {
        field_0xfd4[i] = modelData->getMaterialNodePointer(l_eyeMatNo[i]);
        field_0x1014[i] = field_0xfd4[i]->getShape();
    }

    for (int i = 0; i < 6; i++) {
        field_0xfec[i] = modelData->getMaterialNodePointer(l_eyeblowMatNo[i]);
        field_0x102c[i] = field_0xfec[i]->getShape();
    }

    for (int i = 0; i < 4; i++) {
        field_0x1004[i] = modelData->getMaterialNodePointer(l_faceMatNo[i]);
        field_0x1044[i] = field_0x1004[i]->getShape();
    }

    J3DMaterialAnm* anm = field_0xfd4[2]->getMaterialAnm();
    field_0xfd4[1]->setMaterialAnm(anm);
    field_0xfd4[0]->setMaterialAnm(anm);

    anm = field_0xfd4[5]->getMaterialAnm();
    field_0xfd4[4]->setMaterialAnm(anm);
    field_0xfd4[3]->setMaterialAnm(anm);

    anm = field_0xfec[2]->getMaterialAnm();
    field_0xfec[1]->setMaterialAnm(anm);
    field_0xfec[0]->setMaterialAnm(anm);

    anm = field_0xfec[5]->getMaterialAnm();
    field_0xfec[4]->setMaterialAnm(anm);
    field_0xfec[3]->setMaterialAnm(anm);
}

/* 00006974-00006BAC       .text createInit__6daPz_cFv */
void daPz_c::createInit() {
    max_health = 30;
    health = max_health;
    stealItemLeft = 10;

    mEnemyFire.mpMcaMorf = mpMorf;
    mEnemyFire.mpActor = this;

    static u8 fire_j[ARRAY_SIZE(mEnemyFire.mFlameJntIdxs)] = {
        PZ_JNT_HAIR_2_e, PZ_JNT_CHEST_e,  PZ_JNT_ARM_L1_e, PZ_JNT_ARM_L2_e,  PZ_JNT_ARM_R1_e,
        PZ_JNT_ARM_R2_e, PZ_JNT_LEG_1_e,  PZ_JNT_LEG_2_e,  PZ_JNT_SKIRT_2_e, PZ_JNT_SKIRT_3_e,
    };
    static f32 fire_sc[ARRAY_SIZE(mEnemyFire.mParticleScale)] = {
        2.0f, 2.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f,
    };

    for (int i = 0; i < ARRAY_SIZE(mEnemyFire.mFlameJntIdxs); i++) {
        mEnemyFire.mFlameJntIdxs[i] = fire_j[i];
        mEnemyFire.mParticleScale[i] = fire_sc[i];
    }

    mEventIce.mpActor = this;
    mEventIce.m00C = 1;
    mEventIce.mWallRadius = 50.0f;
    mEventIce.mCylHeight = 250.0f;

    m08B0 = 0;
    m0F7C = l_HIO.m0F8[0];
    m0F80 = 0;

    bodyCreateInit();

    if (mArg == 0) {
        m0F88 = 1;
        modeProc((Proc_e)0, 1);
        attention_info.distances[fopAc_Attn_TYPE_TALK_e] = 3;
        attention_info.distances[fopAc_Attn_TYPE_SPEAK_e] = 3;
        attention_info.flags |= fopAc_Attn_ACTION_SPEAK_e;
        attention_info.flags |= fopAc_Attn_LOCKON_TALK_e;
    } else {
        attention_info.flags = 0xa;
        modeProc((Proc_e)0, 0);
    }

    m0920 = 1;
    cullMtx = mpMorf->getModel()->getBaseTRMtx();

    fopAcM_setCullSizeBox(this, -60.0f,
                           -50.0f,
                           -60.0f,
                           60.0f,
                           1800.0f,
                           60.0f);

    mAcchCir.SetWall(80.0f,
                      60.0f);

    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir,
                 fopAcM_GetSpeed_p(this), NULL, NULL);

    mObjAcch.SetRoofNone();

    gravity = l_HIO.m0B0;

    mStts.Init(0xFF, 0xFF, this);
    mCyl.Set(m_cyl_src);
    mCyl.SetStts(&mStts);

    setCollision(30.0f,
                 130.0f);

    dKy_tevstr_init(&mTevstr, home.roomNo, 0xFF);

    g_env_light.settingTevStruct(0, &current.pos, &tevStr);
}

/* 00006BAC-00006CB0       .text _create__6daPz_cFv */
cPhs_State daPz_c::_create() {
    cPhs_State resLoad = dComIfG_resLoad(&mPhs, m_arc_name);

    fopAcM_ct(this, daPz_c);

    if (resLoad == cPhs_COMPLEATE_e) {
        getArg();

        if (mArg != 0 && strcmp(dComIfGp_getStartStageName(), "kenroom") == 0 &&
            dComIfGs_isEventBit(dSv_event_flag_c::UNK_3520) == 1) {
            return cPhs_ERROR_e;
        }

        if (!fopAcM_entrySolidHeap(this, createHeap_CB, m_heapsize)) {
            return cPhs_ERROR_e;
        }

        createInit();
    }

    return resLoad;
}

/* 00007DA0-00007E20       .text _delete__6daPz_cFv */
bool daPz_c::_delete() {
    dComIfG_resDelete(&mPhs, m_arc_name);
    mRippleCb.end();
    mFallSplashCb.end();
    mHeadSplashCb.end();

    if (heap != NULL) {
        mpMorf->stopZelAnime();
    }

    return true;
}

/* 00007E20-00007E40       .text daPzCreate__FPv */
static cPhs_State daPzCreate(void* i_this) {
    return ((daPz_c*)i_this)->_create();
}

/* 00007E40-00007E64       .text daPzDelete__FPv */
static BOOL daPzDelete(void* i_this) {
    return ((daPz_c*)i_this)->_delete();
}

/* 00007E64-00007E88       .text daPzExecute__FPv */
static BOOL daPzExecute(void* i_this) {
    return ((daPz_c*)i_this)->_execute();
}

/* 00007E88-00007EAC       .text daPzDraw__FPv */
static BOOL daPzDraw(void* i_this) {
    return ((daPz_c*)i_this)->_draw();
}

/* 00007EAC-00007EB4       .text daPzIsDelete__FPv */
static BOOL daPzIsDelete(void*) {
    return TRUE;
}

static actor_method_class daPzMethodTable = {
    (process_method_func)daPzCreate,
    (process_method_func)daPzDelete,
    (process_method_func)daPzExecute,
    (process_method_func)daPzIsDelete,
    (process_method_func)daPzDraw,
};

actor_process_profile_definition g_profile_PZ = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_PZ_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daPz_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_PZ_e,
    /* Actor SubMtd */ &daPzMethodTable,
    /* Status       */ 0x08 | fopAcStts_SHOWMAP_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
