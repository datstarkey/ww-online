/**
 * d_a_gy.cpp
 * Enemy - Gyorg
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_gy.h"
#include "d/d_cc_d.h"
#include "d/actor/d_a_sea.h"
#include "d/d_a_obj.h"
#include "d/d_lib.h"
#include "d/actor/d_a_player.h"

#include "d/actor/d_a_gy_ctrl.h"

const u32 daGy_c::m_heapsize = 0x3FA0;
const char daGy_c::m_arc_name[] = "Gy";

class daGy_HIO_c : public mDoHIO_entry_c {
public:
    daGy_HIO_c();
    virtual ~daGy_HIO_c() {}
    /* 0x004 */ f32 m004;
    /* 0x008 */ f32 m008;
    /* 0x00C */ s16 m00C;
    /* 0x00E */ u8 field_0xe[0x10 - 0xE];
    /* 0x010 */ f32 m010;
    /* 0x014 */ f32 m014;
    /* 0x018 */ f32 m018;
    /* 0x01C */ f32 m01C;
    /* 0x020 */ f32 m020;
    /* 0x024 */ f32 m024;
    /* 0x028 */ f32 m028;
    /* 0x02C */ f32 m02C;
    /* 0x030 */ f32 m030;
    /* 0x034 */ f32 m034;
    /* 0x038 */ f32 m038;
    /* 0x03C */ f32 m03C;
    /* 0x040 */ f32 m040;
    /* 0x044 */ f32 m044;
    /* 0x048 */ f32 m048;
    /* 0x04C */ f32 m04C;
    /* 0x050 */ f32 m050;
    /* 0x054 */ f32 m054;
    /* 0x058 */ f32 m058;
    /* 0x05C */ f32 m05C;
    /* 0x060 */ f32 m060;
    /* 0x064 */ f32 m064;
    /* 0x068 */ f32 m068;
    /* 0x06C */ f32 m06C;
    /* 0x070 */ f32 m070;
    /* 0x074 */ f32 m074;
    /* 0x078 */ f32 m078;
    /* 0x07C */ f32 m07C;
    /* 0x080 */ f32 m080;
    /* 0x084 */ f32 m084;
    /* 0x088 */ f32 m088;
    /* 0x08C */ f32 m08C;
    /* 0x090 */ f32 m090;
    /* 0x094 */ u8 m094;
    /* 0x095 */ u8 m095;
    /* 0x096 */ u8 m096;
    /* 0x097 */ u8 m097;
    /* 0x098 */ u8 m098;
    /* 0x099 */ u8 m099;
    /* 0x09A */ s16 m09A;
    /* 0x09C */ f32 m09C;
    /* 0x0A0 */ f32 m0A0;
    /* 0x0A4 */ f32 m0A4;
    /* 0x0A8 */ f32 m0A8;
    /* 0x0AC */ f32 m0AC;
    /* 0x0B0 */ f32 m0B0;
    /* 0x0B4 */ f32 m0B4;
    /* 0x0B8 */ f32 m0B8;
    /* 0x0BC */ f32 m0BC;
    /* 0x0C0 */ f32 m0C0;
    /* 0x0C4 */ f32 m0C4;
    /* 0x0C8 */ f32 m0C8;
    /* 0x0CC */ f32 m0CC;
    /* 0x0D0 */ f32 m0D0;
    /* 0x0D4 */ cXyz m0D4;
    /* 0x0E0 */ f32 m0E0;
    /* 0x0E4 */ f32 m0E4;
    /* 0x0E8 */ f32 m0E8;
    /* 0x0EC */ f32 m0EC;
    /* 0x0F0 */ f32 m0F0;
    /* 0x0F4 */ f32 m0F4;
    /* 0x0F8 */ f32 m0F8;
    /* 0x0FC */ f32 m0FC;
    /* 0x100 */ f32 m100;
    /* 0x104 */ f32 m104;
    /* 0x108 */ f32 m108;
    /* 0x10C */ s16 m10C;
    /* 0x10E */ s16 m10E;
    /* 0x110 */ s16 m110;
    /* 0x112 */ s16 m112;
    /* 0x114 */ s16 m114;
    /* 0x116 */ s16 m116;
    /* 0x118 */ s16 m118;
    /* 0x11A */ u8 m11A[0x12C - 0x11A];
    /* 0x12C */ f32 m12C;
    /* 0x130 */ s16 m130;
    /* 0x132 */ s16 m132;
    /* 0x134 */ f32 m134;
    /* 0x138 */ f32 m138;
    /* 0x13C */ f32 m13C;
    /* 0x140 */ s16 m140;
    /* 0x142 */ s16 m142;
    /* 0x144 */ f32 m144;
    /* 0x148 */ f32 m148;
    /* 0x14C */ f32 m14C;
    /* 0x150 */ f32 m150;
    /* 0x154 */ f32 m154;
    /* 0x158 */ f32 m158;
    /* 0x15C */ f32 m15C;
    /* 0x160 */ f32 m160;
    /* 0x164 */ s16 m164;
    /* 0x166 */ u8 field_0x166[0x168 - 0x166];
    /* 0x168 */ f32 m168;
    /* 0x16C */ f32 m16C;
    /* 0x170 */ f32 m170;
    /* 0x174 */ f32 m174;
    /* 0x178 */ f32 m178;
    /* 0x17C */ f32 m17C;
    /* 0x180 */ s16 m180;
    /* 0x182 */ u8 field_0x182[0x184 - 0x182];
    /* 0x184 */ f32 m184;
    /* 0x188 */ f32 m188;
    /* 0x18C */ f32 m18C;
    /* 0x190 */ f32 m190;
    /* 0x194 */ f32 m194;
    /* 0x198 */ s16 m198;
    /* 0x19A */ u8 field_0x19a[0x19C - 0x19A];
};

static daGy_HIO_c l_HIO;

static dCcD_SrcSph l_sph_head_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ AT_TYPE_UNK800,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ cCcD_AtSPrm_GrpAll_e,
        /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsOther_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ dCcG_SE_UNK23,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoConHit_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGSphS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 180.0f,
    }},
};


static dCcD_SrcSph l_sph_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ cCcD_AtSPrm_GrpAll_e,
        /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsOther_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ dCcG_SE_UNK23,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoConHit_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGSphS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 180.0f,
    }},
};


static dCcD_SrcCps l_cps_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ cCcD_AtSPrm_GrpAll_e,
        /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsOther_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ dCcG_SE_UNK23,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoConHit_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCpsS
    {{
        /* Start  */ {0.0f, 0.0f, 0.0f},
        /* End    */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 60.0f,
    }},
};



/* 000000EC-000003CC       .text __ct__10daGy_HIO_cFv */
daGy_HIO_c::daGy_HIO_c() {
    m00C = 200;
    m004 = 1.0f;
    m008 = 0.9f;
    m010 = 200.0f;
    m014 = 300.0f;
    m018 = 2.0f;
    m01C = 2.0f;
    m024 = 15.0f;
    m020 = 0.0f;
    m028 = -80.0f;
    m02C = -50.0f;
    m030 = -150.0f;
    m034 = -40.0f;
    m038 = -100.0f;
    m03C = -350.0f;
    m048 = 10.0f;
    m04C = 20.0f;
    m050 = 30.0f;
    m054 = 40.0f;
    m058 = 15.0f;
    m05C = 5.0f;
    m060 = 40.0f;
    m064 = 20.0f;
    m068 = 55.0f;
    m06C = 10.0f;
    m070 = 20.0f;
    m198 = 1;
    m040 = 60.0f;
    m044 = 2500.0f;
    m074 = 70.0f;
    m078 = 70.0f;
    m07C = 80.0f;
    m080 = 70.0f;
    m084 = 70.0f;
    m088 = 110.0f;
    m08C = 100.0f;
    m090 = 1.0f;
    m09C = 5000.0f;
    m0A4 = 180.0f;
    m0A8 = -400.0f;
    m094 = 0;
    m095 = 0;
    m096 = 0;
    m097 = 0;
    m098 = 0;
    m099 = 0;
    m0A0 = -500.0f;
    m0AC = -80.0f;
    m0B4 = -25.0f;
    m0B0 = 20.0f;
    m0B8 = 0.0f;
    m0BC = 0.0f;
    m0C0 = 0.0f;
    m0C4 = 8.0f;
    m178 = 0.5f;
    m0C8 = 30.0f;
    m0D4.x = 80.0f;
    m0D4.y = 0.0f;
    m0D4.z = 0.0f;
    m0E0 = 0.0f;
    m0E4 = 0.0f;
    m0E8 = 0.0f;
    m0EC = 0.0f;
    m0F0 = 0.0f;
    m0F4 = 120.0f;
    m0F8 = 0.0f;
    m0FC = 0.0f;
    m100 = 300.0f;
    m0CC = 2.0f;
    m0D0 = 20.0f;
    m104 = -40.0f;
    m108 = 25.0f;
    m10C = 120;
    m10E = 60;
    m110 = 120;
    m112 = 180;
    m114 = 360;
    m116 = 90;
    m118 = 240;
    m188 = 0.2f;
    m12C = 10.0f;
    m144 = 400.0f;
    m148 = 350.0f;
    m150 = 60.0f;
    m154 = 60.0f;
    m158 = 40.0f;
    for (int i = 2; i < 11; i++) {
        m11A[i] = 0;
    }
    m130 = 100;
    m132 = 300;
    m134 = 45.0f;
    m138 = 45.0f;
    m13C = 30.0f;
    m15C = 45.0f;
    m160 = 10.0f;
    m140 = 0x1500;
    m142 = 0x800;
    m164 = 90;
    m168 = -1000.0f;
    m16C = -100.0f;
    m170 = 50.0f;
    m174 = 0.1f;
    m17C = 0.1f;
    m184 = 4000.0f;
    m180 = 0x2000;
    m18C = 3000.0f;
    m190 = 200.0f;
    m194 = 3000.0f;
}

/* 00000450-0000049C       .text nodeControl_CB__FP7J3DNodei */
static BOOL nodeControl_CB(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        J3DModel* model = j3dSys.getModel();
        daGy_c* actor = (daGy_c*)model->getUserArea();
        if (actor != NULL) {
            actor->_nodeControl(i_node, model);
        }
    }
    return TRUE;
}

/* 0000049C-00000888       .text _nodeControl__6daGy_cFP7J3DNodeP8J3DModel */
void daGy_c::_nodeControl(J3DNode* i_node, J3DModel* i_model) {
    int jntNo = ((J3DJoint*)i_node)->getJntNo();
    if (jntNo == 2) {
        mDoMtx_stack_c::copy(i_model->getAnmMtx(jntNo));
        cXyz offset(0.0f, 0.0f, 0.0f);
        offset = l_HIO.m0D4;
        mDoMtx_stack_c::multVec(&offset, &mD08);
    }

    Mtx mtx;
    cMtx_copy(i_model->getAnmMtx(jntNo), mtx);
    cXyz trans(mtx[0][3], mtx[1][3], mtx[2][3]);
    mtx[0][3] = mtx[1][3] = mtx[2][3] = 0.0f;
    mDoMtx_stack_c::transS(trans.x, trans.y, trans.z);

    if (jntNo == 2) {
        fopAc_ac_c* player = dComIfGp_getPlayer(0);
        f32 dist = (current.pos - player->current.pos).absXZ();
        if (cLib_distanceAngleS((s16)shape_angle.y, fopAcM_searchActorAngleY(this, player)) < l_HIO.m180 &&
            dist < l_HIO.m184 && mD15 != 8 && mD15 != 9 && mD15 != 6 && mD15 != 11 && mD15 != 3)
        {
            mCEC = player->current.pos;
            if (m2B0 == 2) {
                f32 y = daSea_calcWave(mCEC.x, mCEC.z); mCEC.y = y + l_HIO.m0A8;
            }
            if (m2B0 == 3 && field_0x928 == 1) {
                mCEC.y += l_HIO.m0A4;
            }
            cXyz target = mCEC - mD08;
            cXyz base = mD08 - mD08;
            Quaternion q;
            daObj::quat_rotVec(&q, base, target);
            mDoMtx_quatSlerp(&field_0xcf8, &q, &field_0xcf8, l_HIO.m188);
        } else {
            mDoMtx_quatSlerp(&field_0xcf8, &ZeroQuat, &field_0xcf8, l_HIO.m188);
        }
        mDoMtx_stack_c::quatM(&field_0xcf8);
    }

    mDoMtx_stack_c::concat(mtx);

    if (jntNo == 2) {
        cXyz offset(0.0f, 0.0f, 0.0f);
        offset = l_HIO.m0D4;
        mDoMtx_stack_c::multVec(&offset, &mD08);
    }
    if (jntNo == 5) {
        cXyz offset(100.0f, -100.0f, 0.0f);
        mDoMtx_stack_c::multVec(&offset, &m89C);
    }

    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 00000888-000008A8       .text createHeap_CB__FP10fopAc_ac_c */
static BOOL createHeap_CB(fopAc_ac_c* i_this) {
    return ((daGy_c*)i_this)->_createHeap();
}

#include "d/d_jnt_hit.h"
#include "res/Object/gy.h"

/* 000008A8-00000AAC       .text _createHeap__6daGy_cFv */
BOOL daGy_c::_createHeap() {
    J3DModelData* modelData = static_cast<J3DModelData*>(dComIfG_getObjectRes(m_arc_name, dRes_INDEX_GY_BMD_GY_e));
    JUT_ASSERT(0x377, modelData != NULL);

    mpMorf = new mDoExt_McaMorf(
        modelData, NULL, NULL,
        static_cast<J3DAnmTransformKey*>(dComIfG_getObjectRes(m_arc_name, dRes_INDEX_GY_BCK_SWIM1_e)),
        J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, 1,
        NULL, 0, 0x11020203
    );

    if (mpMorf == NULL || mpMorf->getModel() == NULL) {
        return FALSE;
    }

#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    if (m2D8.create(mpMorf->getModel()) == 0) {
        return FALSE;
    }

    for (u16 i = 0; i < modelData->getJointNum(); i++) {
        switch (i) {
        case 2:
        case 5:
        case 6:
        case 7:
        case 9:
        case 10:
            mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeControl_CB);
            break;
        }
    }

    static Vec atama_cyl_offset[] = {
        {0.0f, 0.0f, 0.0f},
        {150.0f, 0.0f, 0.0f},
    };
    static Vec ago_cyl_offset[] = {
        {0.0f, 0.0f, 0.0f},
        {90.0f, 0.0f, 0.0f},
    };
    static Vec body1_cyl_offset[] = {
        {-40.0f, -20.0f, 0.0f},
        {40.0f, -20.0f, 0.0f},
    };
    static Vec body2_cyl_offset[] = {
        {-40.0f, -10.0f, 0.0f},
        {40.0f, -10.0f, 0.0f},
    };
    static Vec sebire1_cyl_offset[] = {
        {-40.0f, 60.0f, 0.0f},
        {150.0f, -100.0f, 0.0f},
    };
    static __jnt_hit_data_c search_data[] = {
        {JntHitType_CYL_e, 2, 30.0f, atama_cyl_offset},
        {JntHitType_CYL_e, 3, 30.0f, ago_cyl_offset},
        {JntHitType_CYL_e, 4, 60.0f, body1_cyl_offset},
        {JntHitType_CYL_e, 5, 20.0f, sebire1_cyl_offset},
        {JntHitType_CYL_e, 6, 60.0f, body1_cyl_offset},
        {JntHitType_CYL_e, 7, 50.0f, body1_cyl_offset},
        {JntHitType_CYL_e, 9, 30.0f, body1_cyl_offset},
        {JntHitType_CYL_e, 10, 30.0f, body2_cyl_offset},
    };

    mpJntHit = JntHit_create(mpMorf->getModel(), search_data, ARRAY_SIZE(search_data));
    if (mpJntHit != NULL) {
        jntHit = mpJntHit;
    } else {
        return FALSE;
    }
    return TRUE;
}

#include "m_Do/m_Do_mtx.h"

/* 00000AAC-00000C30       .text setMtx__6daGy_cFv */
void daGy_c::setMtx() {
    J3DModel* model = mpMorf->getModel();

    model->setBaseScale(scale);

    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::ZXYrotM(shape_angle);

    cXyz localPos(0.0f, 0.0f, 500.0f);
    mDoMtx_stack_c::multVec(&localPos, &field_0xe84);

    if (l_HIO.m094) {
        mDoMtx_stack_c::transM(l_HIO.m0B8, l_HIO.m0BC, l_HIO.m0C0);
    }

    mDoMtx_stack_c::transM(0.0f, field_0x4e4, 0.0f);

    model->setBaseTRMtx(mDoMtx_stack_c::now);

    cXyz addPos;
    if (field_0x4e4 <= 10.0f + l_HIO.m0AC) {
        addPos.x = l_HIO.m0EC;
        addPos.y = l_HIO.m0F0;
        addPos.z = l_HIO.m0F4;
    } else if (field_0x4e4 > 10.0f + l_HIO.m0AC) {
        addPos.x = l_HIO.m0F8;
        addPos.y = l_HIO.m0FC;
        addPos.z = l_HIO.m100;
    }
    mDoMtx_stack_c::transM(addPos);

    field_0xdfc = mDoMtx_stack_c::now[0][3];
    field_0xe00 = mDoMtx_stack_c::now[1][3];
    field_0xe04 = mDoMtx_stack_c::now[2][3];
}

#include "d/d_lib.h"

/* 00000C30-00000C7C       .text setAnm__6daGy_cFv */
void daGy_c::setAnm() {
    static const dLib_anm_idx_c a_anm_idx_tbl[] = {
        { 11, -1 },
        { 12, -1 },
        { 6, -1 },
        { 10, -1 },
        { 7, -1 },
        { 8, -1 },
        { 9, -1 },
        { 4, -1 },
        { 5, -1 }
    };

    static const dLib_anm_prm_c a_anm_prm_tbl[] = {
        { 0, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_NONE },
        { 0, -1, 0, 8.0f, 2.0f, J3DFrameCtrl::EMode_LOOP },
        { 1, -1, 0, 8.0f, 2.0f, J3DFrameCtrl::EMode_LOOP },
        { 0, -1, 0, 8.0f, 2.5f, J3DFrameCtrl::EMode_LOOP },
        { 2, 1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_NONE },
        { 3, -1, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_NONE },
        { 4, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_NONE },
        { 4, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_LOOP },
        { 5, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_NONE },
        { 6, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_NONE },
        { 7, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_NONE },
        { 8, -1, 0, 2.0f, 1.0f, J3DFrameCtrl::EMode_NONE }
    };

    dLib_setAnm(m_arc_name, mpMorf, &field_0xd14, &mD15, &field_0xd16, a_anm_idx_tbl, a_anm_prm_tbl, false);
}

/* 00000C7C-00000E74       .text setAtCollision__6daGy_cFv */
void daGy_c::setAtCollision() {
    m50C.SetR(l_HIO.m088 * l_HIO.m090);
    m50C.SetC(mD08);

    if (m2B0 == 3) {
        s32 frame = (s32)mpMorf->getFrame();
        if (mD15 == 11 && frame < 0x12) {
            m50C.OnAtSetBit();
            m50C.OnAtHitBit();
            m50C.SetAtAtp(2);
            m50C.SetAtSpl(dCcG_At_Spl_UNK1);
            m50C.SetAtSe(6);
            m50C.OffCoSetBit();
        } else {
            m50C.OffAtSetBit();
            m50C.OffAtHitBit();
            m50C.SetAtAtp(0);
            m50C.OnCoSetBit();
        }
    } else if (m2B0 == 2 || m2B0 == 5) {
        if (field_0x2b8->m320 == 1) {
            if (m50C.ChkCoHit()) {
                fopAc_ac_c* hitAc = m50C.GetCoHitAc();
                if (fpcM_GetName(hitAc) == fpcNm_SHIP_e) {
                    m50C.OnAtSetBit();
                    m50C.OnAtHitBit();
                    m50C.SetAtAtp(2);
                    m50C.SetAtSpl(dCcG_At_Spl_UNK1);
                    m50C.SetAtSe(6);
                }
            } else {
                m50C.OffAtSetBit();
                m50C.OffAtHitBit();
                m50C.SetAtAtp(0);
            }
            m50C.OnCoSetBit();
        }
    } else {
        m50C.OffAtSetBit();
        m50C.OffAtHitBit();
        m50C.SetAtAtp(0);
        m50C.OnCoSetBit();

        if (m50C.ChkCoHit() && m2B0 != 9 && m2B0 != 8) {
            modeDiveInit();
        }
    }

    dComIfG_Ccsp()->Set(&m50C);
}

/* 00000E74-00000F3C       .text setCollision__6daGy_cFv */
void daGy_c::setCollision() {
#if TARGET_PC
    m638.SetR(140.0f * l_HIO.m090); // 0x90 holds on the GameCube only (the vtable pointer is 8 bytes here)
#else
    m638.SetR(140.0f * *(f32*)((u8*)&l_HIO + 0x90));
#endif
    m638.SetC(current.pos);
    dComIfG_Ccsp()->Set(&m638);

    m764.SetStartEnd(m89C, m89C);
#if TARGET_PC
    m764.SetR(l_HIO.m08C * l_HIO.m090);
#else
    m764.SetR(*(f32*)((u8*)&l_HIO + 0x8c) * *(f32*)((u8*)&l_HIO + 0x90));
#endif
    dComIfG_Ccsp()->Set(&m764);
}

/* 00000F3C-00000FC4       .text setAimSpeedF__6daGy_cFv */
void daGy_c::setAimSpeedF() {
    if (field_0x2b8->m320 == 0) {
        field_0x4f0 = l_HIO.m048;
        if (mpMorf->getFrame() > l_HIO.m0CC && mpMorf->getFrame() < l_HIO.m0D0) {
            field_0x4f0 = l_HIO.m04C;
        }
    } else {
        field_0x4f0 = l_HIO.m050;
        if (mpMorf->getFrame() > l_HIO.m0CC && mpMorf->getFrame() < l_HIO.m0D0) {
            field_0x4f0 = l_HIO.m054;
        }
    }
}

/* 00000FC4-00000FDC       .text modeDiveInit__6daGy_cFv */
void daGy_c::modeDiveInit() {
    m2B0 = 0;
    field_0x508 = 0.0f;
}

#include "d/d_com_inf_game.h"
#include "d/actor/d_a_ship.h"
#include "f_op/f_op_actor_mng.h"
#include "SSystem/SComponent/c_lib.h"

/* 00000FDC-000012DC       .text modeDive__6daGy_cFv */
void daGy_c::modeDive() {
    if (mD15 == 5) {
        return;
    }
    mD15 = 1;

    daGy_Ctrl_c* ctrl = field_0x2b8;
    if (ctrl->m324 == 3 || field_0xe80 != 0) {
        cLib_addCalcAngleS2(&current.angle.y, cLib_targetAngleY(&current.pos, &m2BC), 8, 0x400);
        field_0x4f0 = 0.0f;
        field_0x4e8 = 2.0f * l_HIO.m0A0;
        field_0x4ec = l_HIO.m0C8;
        actor_status &= ~0x20;
        attention_info.flags &= ~fopAc_Attn_LOCKON_BATTLE_e;
        return;
    }

    actor_status |= 0x20;
    attention_info.flags |= fopAc_Attn_LOCKON_BATTLE_e;
    field_0x4e8 = l_HIO.m0A0;
    field_0x4ec = l_HIO.m0C8;

    f32 dist = fopAcM_searchActorDistance(this, dComIfGp_getPlayer(0));

    if (ctrl->m312[field_0x2ac] == 1) {
        if (field_0x4e4 < 10.0f + l_HIO.m0A0) {
            if (dist > l_HIO.m148) {
                modeCircleInit();
                return;
            }
        }
    }

    fopAc_ac_c* ship = dComIfGp_getShipActor();

    dist = (current.pos - m2BC).absXZ();

    if (ship == NULL) {
        return;
    }

    if (dist > l_HIO.m144 || ship->speedF > l_HIO.m06C) {
        setAimSpeedF();
        cLib_addCalcAngleS2(&current.angle.y, cLib_targetAngleY(&current.pos, &m2BC), 8, 0x400);
        field_0x508 = 0.0f;
    } else {
        field_0x4f0 = 0.0f;
        cXyz diff = m2BC - current.pos;
        cLib_addCalc2(&field_0x508, 1.0f, 0.01f, 0.05f);
        current.pos = current.pos + diff * field_0x508;
        cLib_addCalcAngleS2(&current.angle.y, ctrl->m308[field_0x2ac] + 0x8000, 4, 0x400);
    }
}

/* 000012DC-0000141C       .text modeCircleInit__6daGy_cFv */
void daGy_c::modeCircleInit() {
    m2B0 = 1;

    s32 mode = field_0x2b8->m320;
    if (mode == 0) {
        field_0x914 = l_HIO.m116 + cM_rndF(l_HIO.m118 - l_HIO.m116);
    } else if (mode == 1) {
        field_0x914 = l_HIO.m112 + cM_rndF(l_HIO.m114 - l_HIO.m112);
    }

    field_0x508 = 0.0f;
}

/* 0000141C-000016E4       .text modeCircle__6daGy_cFv */
void daGy_c::modeCircle() {
    if (mD15 == 5) {
        return;
    }

    mD15 = 1;
    field_0x4e8 = l_HIO.m0AC;
    field_0x4ec = l_HIO.m0C4;

    daGy_Ctrl_c* ctrl = field_0x2b8;
    if (ctrl->m312[field_0x2ac] == 0) {
        modeDiveInit();
        return;
    }

    fopAc_ac_c* ship = dComIfGp_getShipActor();
    f32 dist = (current.pos - m2BC).absXZ();
    if (ship == NULL) {
        return;
    }

    if (dist > l_HIO.m144 || ship->speedF > l_HIO.m06C) {
        setAimSpeedF();
        if (fopAcM_searchActorDistance(this, dComIfGp_getPlayer(0)) < l_HIO.m148) {
            modeDiveInit();
        } else if (ship->speedF > l_HIO.m070) {
            modeWithCircleInit();
        } else {
            cLib_addCalcAngleS2(&current.angle.y, cLib_targetAngleY(&current.pos, &m2BC), 8, 0x400);
            field_0x508 = 0.0f;
        }
    } else {
        field_0x4f0 = 0.0f;
        cXyz diff = m2BC - current.pos;
        cLib_addCalc2(&field_0x508, 1.0f, 0.01f, 0.05f);
        current.pos = current.pos + diff * field_0x508;
        cLib_addCalcAngleS2(&current.angle.y, ctrl->m308[field_0x2ac] + 0x8000, 4, 0x400);
        if (cLib_calcTimer(&field_0x914) == 0 && !dComIfGp_checkPlayerStatus0(0, daPyStts0_UNK1000000_e)) {
            if (ctrl->m320 == 0) {
                modeAttackPlayerInit();
            } else if (ctrl->m320 == 1) {
                modeAttackInit();
            }
        }
    }
}

/* 000016E4-00001788       .text modeWithCircleInit__6daGy_cFv */
void daGy_c::modeWithCircleInit() {
    m2B0 = 6;

    field_0x4f8 = cM_rndF(l_HIO.m132);
    if (field_0x4f8 <= l_HIO.m130) {
        field_0x4f8 = l_HIO.m130;
    }

    field_0x914 = l_HIO.m110 + field_0x4f8;
}

#include "d/d_com_inf_game.h"
#include "d/actor/d_a_ship.h"
#include "d/d_s_play.h"
#include "f_op/f_op_actor_mng.h"
#include "SSystem/SComponent/c_lib.h"
#include "JAZelAudio/JAZelAudio_SE.h"

/* 00001788-000019AC       .text modeWithCircle__6daGy_cFv */
void daGy_c::modeWithCircle() {
    if (mD15 == 5) {
        return;
    }

    field_0x4e8 = l_HIO.m0AC;
    field_0x4ec = l_HIO.m0C4;

    if (field_0x2b8->m312[field_0x2ac] == 0) {
        modeDiveInit();
        return;
    }

    fopAc_ac_c* ship = dComIfGp_getShipActor();
    if (ship == NULL) {
        return;
    }

    if (ship->speedF < l_HIO.m050) {
        modeCircleInit();
    }

    cLib_addCalcAngleS2(&current.angle.y, ship->shape_angle.y, 8, 0x400);
    s32 angleDiff = cLib_distanceAngleS(ship->shape_angle.y, fopAcM_searchActorAngleY(ship, this));
    f32 dist = fopAcM_searchActorDistance(this, dComIfGp_getPlayer(0));

    if (angleDiff < l_HIO.m142 && dist > l_HIO.m09C) {
        field_0x4f0 = ship->speedF - 5.0f;
    } else {
        field_0x4f0 = ship->speedF + l_HIO.m040;
    }

    if (angleDiff < l_HIO.m140 && angleDiff > l_HIO.m142 && dist > l_HIO.m09C) {
        speed.y = l_HIO.m138;
        fopAcM_seStart(this, JA_SE_CM_GY_JUMP_L, 0);
        mD15 = 5;

        f32 s = 2.0f + REG10_F(0);
        cXyz scale(s, s + REG10_F(1), s);
        dComIfGp_particle_set(dPa_name::ID_IT_JN_MIZUSHIBUKI_A, &current.pos, NULL, &scale);

        modeWithAttackInit();
    }
}

/* 000019AC-00001A14       .text modeAttackInit__6daGy_cFv */
void daGy_c::modeAttackInit() {
    m2B0 = 2;
    mD15 = 3;

    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    field_0x8f8 = player->current.pos.x;
    field_0x8fc = player->current.pos.y;
    field_0x900 = player->current.pos.z;

    field_0x914 = l_HIO.m10C;

    m904.x = m2BC.x;
    m904.y = m2BC.y;
    m904.z = m2BC.z;

    field_0x928 = 0;
}

/* 00001A14-00001A74       .text modeWithAttackInit__6daGy_cFv */
void daGy_c::modeWithAttackInit() {
    m2B0 = 5;
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    field_0x8f8 = player->current.pos.x;
    field_0x8fc = player->current.pos.y;
    field_0x900 = player->current.pos.z;
    field_0x914 = l_HIO.m10C;
    m904.x = m2BC.x;
    m904.y = m2BC.y;
    m904.z = m2BC.z;
    field_0x928 = 0;
}

/* 00001A74-00001D60       .text modeWithAttack__6daGy_cFv */
void daGy_c::modeWithAttack() {
    if (mD15 != 5) {
        mD15 = 3;
    }

    if (field_0x2b8->m320 == 0) {
        modeCircleInit();
        return;
    }

    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    s16 angleY = cLib_targetAngleY(&current.pos, &player->current.pos);
    f32 dist = fopAcM_searchActorDistance(this, dComIfGp_getPlayer(0));

    switch (field_0x928) {
    case 0:
        field_0x4e8 = l_HIO.m0AC;
        field_0x4ec = l_HIO.m0C4;
        cLib_addCalcAngleS2(&current.angle.y, angleY, 8, 0x400);
        field_0x4f0 = l_HIO.m068;

        if (dist < l_HIO.m044) {
            field_0x928++;
            field_0x914 = l_HIO.m10E;
        }

        if (cLib_calcTimer(&field_0x914) == 0) {
            if (l_HIO.m099 == 0) {
                modeWithCircleInit();
            } else {
                field_0x928++;
            }
        }
        break;

    case 1:
        field_0x4e8 = l_HIO.m0B0;
        field_0x4ec = l_HIO.m0C4;
        field_0x4f0 = l_HIO.m068;

        if (m50C.ChkCoHit() && fpcM_GetName(m50C.GetCoHitAc()) == fpcNm_SHIP_e) {
            dComIfGp_getVibration().StartShock(
                7, -0x21,
                cXyz(0.0f,
                     1.0f,
                     0.0f));

            fopAcM_seStart(this, JA_SE_CM_GY_SHIP_CRASH, 0);
            fopAcM_monsSeStart(this, JA_SE_CV_GY_ATTACK, 0);
            modeAttackBackInit();
        } else {
            if (cLib_calcTimer(&field_0x914) == 0) {
                if (l_HIO.m099 == 0) {
                    modeWithCircleInit();
                } else {
                    field_0x928++;
                }
            }
        }
        break;

    case 2:
        field_0x4e8 = l_HIO.m0A0;
        field_0x4ec = l_HIO.m0C4;

        if (std::fabsf(field_0x4e4 - field_0x4e8) < 10.0f) {
            fopAcM_delete(this);
        }
        break;
    }
}

#include "f_op/f_op_actor_mng.h"
#include "JAZelAudio/JAZelAudio_SE.h"

/* 00001D60-00001F20       .text modeAttack__6daGy_cFv */
void daGy_c::modeAttack() {
    if (field_0x2b8->m320 == 0) {
        modeCircleInit();
        return;
    }

    field_0x4e8 = l_HIO.m0B0;
    field_0x4ec = l_HIO.m0C4;

    if (cLib_calcTimer(&field_0x914) == 0) {
        modeCircleInit();
    }

    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    if (fopAcM_searchActorDistance(this, player) < l_HIO.m148) {
        field_0x4f0 = 0.0f;
    } else {
        field_0x4f0 = l_HIO.m064;
        cLib_addCalcAngleS2(&current.angle.y, cLib_targetAngleY(&current.pos, &player->current.pos), 8, 0x400);
    }

    if (m50C.ChkCoHit()) {
        fopAc_ac_c* hitAc = m50C.GetCoHitAc();
        if (fpcM_GetName(hitAc) == fpcNm_SHIP_e) {
            dComIfGp_getVibration().StartShock(7, -0x21, cXyz(0.0f, 1.0f, 0.0f));
            fopAcM_seStart(this, JA_SE_CM_GY_SHIP_CRASH, 0);
            fopAcM_monsSeStart(this, JA_SE_CV_GY_ATTACK, 0);
            modeAttackBackInit();
        }
    }
}

/* 00001F20-00001F40       .text modeAttackPlayerInit__6daGy_cFv */
void daGy_c::modeAttackPlayerInit() {
    m2B0 = 3;
    mD15 = 2;
    field_0x928 = 0;
    field_0x924 = 0;
}

/* 00001F40-000022F8       .text modeAttackPlayer__6daGy_cFv */
void daGy_c::modeAttackPlayer() {
    if (field_0x2b8->m320 == 1) {
        modeCircleInit();
        return;
    }

    fopAc_ac_c* player = dComIfGp_getPlayer(0);

    switch (field_0x928) {
    case 0: {
        field_0x4e8 = l_HIO.m0B4;
        field_0x4ec = l_HIO.m0C4;
        field_0x4f0 = l_HIO.m058;

        cLib_addCalcAngleS2(&current.angle.y,
                             cLib_targetAngleY(&current.pos, &player->current.pos), 8, 0x400);

        f32 dist = (mD08 - player->current.pos).absXZ();
        if (dist < l_HIO.m148) {
            mD15 = 10;
            field_0x928++;
        }
        break;
    }
    case 1:
        field_0x4e8 = l_HIO.m0B4;
        field_0x4ec = l_HIO.m0C4;
        field_0x4f0 = l_HIO.m05C;

        if (mD15 == 10 && mpMorf->isStop()) {
            mD15 = 2;
            setAnm();
            mD15 = 10;
            field_0x924++;
        }

        if (field_0x924 > l_HIO.m198) {
            mD15 = 11;
            field_0x928++;
        }

        cLib_addCalcAngleS2(&current.angle.y,
                             cLib_targetAngleY(&current.pos, &player->current.pos), 8, 0x400);
        break;
    case 2: {
        if (m50C.ChkCoHit()) {
            fopAc_ac_c* hitAc = m50C.GetCoHitAc();
            if (fpcM_GetName(hitAc) == fpcNm_PLAYER_e && field_0x504 == 0) {
                fopAcM_monsSeStart(this, JA_SE_CV_GY_ATTACK, 0);
                dComIfGp_getVibration().StartShock(4, -0x21, cXyz(0.0f, 1.0f, 0.0f));
                field_0x504 = 1;
            }
        }

        if (mpMorf->getFrame() > 10.0f + REG12_F(10)) {
            field_0x4e8 = l_HIO.m0A0;
            field_0x4ec = l_HIO.m0C4;
        } else {
            field_0x4e8 = l_HIO.m0B4;
            field_0x4ec = l_HIO.m0C4;
        }
        field_0x4f0 = l_HIO.m060;

        if (mD15 == 11 && mpMorf->isStop()) {
            mD15 = 1;
            field_0x504 = 0;
            modeDiveInit();
        }
        break;
    }
    }
}

#include "d/d_s_play.h"

/* 000022F8-00002400       .text modeAttackBackInit__6daGy_cFv */
void daGy_c::modeAttackBackInit() {
    m2B0 = 4;
    mD15 = 4;
    speedF = l_HIO.m104;
    speed.y = l_HIO.m108;

    fopAcM_seStart(this, JA_SE_CM_GY_JUMP_S, 0);

    f32 s = 2.0f + REG10_F(0);
    cXyz scale(s, s + REG10_F(1), s);
    dComIfGp_particle_set(dPa_name::ID_IT_JN_MIZUSHIBUKI_A, &current.pos, NULL, &scale);
}

#include "SSystem/SComponent/c_lib.h"

/* 00002400-00002560       .text modeAttackBack__6daGy_cFv */
void daGy_c::modeAttackBack() {
    field_0x4e8 = l_HIO.m0A0;
    field_0x4f0 = 0.0f;
    cLib_addCalcAngleS2(&m8F4, 0, 4, 0x800);

    if ((current.pos - m904).absXZ() > l_HIO.m144) {
        setAimSpeedF();
        if (mD15 == 1) {
            modeWithCircleInit();
        }
        cLib_addCalcAngleS2(&current.angle.y, cLib_targetAngleY(&current.pos, &m904), 8, 0x400);
    } else {
        if (mD15 == 1) {
            modeCircleInit();
        }
    }
}

#include "f_op/f_op_actor_mng.h"

/* 00002560-000025F0       .text modeDamageInit__6daGy_cFv */
void daGy_c::modeDamageInit() {
    m2B0 = 7;
    mD15 = 6;
    speed.y = 30.0f;
    fopAcM_seStart(this, JA_SE_CM_GY_JUMP_S, 0);
}

#include "SSystem/SComponent/c_lib.h"
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_mtx.h"

/* 000025F0-00002754       .text modeDamage__6daGy_cFv */
void daGy_c::modeDamage() {
    field_0x4f0 = 0.0f;

    if (m920 > 0.01f) {
        fopAc_ac_c* player = dComIfGp_getPlayer(0);
        cXyz local_vec(0.0f, 0.0f, -m920);
        cXyz world_vec(0.0f, 0.0f, 0.0f);

        mDoMtx_YrotS(mDoMtx_stack_c::get(), fopAcM_searchActorAngleY(this, player));
        PSMTXMultVec(mDoMtx_stack_c::get(), &local_vec, &world_vec);

        current.pos.x += world_vec.x;
        current.pos.y += world_vec.y;
        current.pos.z += world_vec.z;

        cLib_addCalc0(&m920, 1.0f, 2.0f);
    }

    if (mD15 == 6 && mpMorf->isStop()) {
        mD15 = 1;

        switch (field_0xd16) {
        case 6:
            modeWithCircleInit();
            break;
        case 0:
            modeDiveInit();
            break;
        default:
            modeCircleInit();
            break;
        }
    }
}

/* 00002754-000028B8       .text modeDeleteInit__6daGy_cFv */
void daGy_c::modeDeleteInit() {
    actor_status &= ~0x20;
    attention_info.flags &= ~fopAc_Attn_LOCKON_BATTLE_e;

    int killCount = dComIfGs_getEventReg(dSv_event_flag_c::UNK_7EFF) + 1;
    u8 newCount;
    dComIfGs_setEventReg(dSv_event_flag_c::UNK_7EFF, newCount = cLib_maxLimit(killCount, 0xff));

    m2B0 = 8;
    mD15 = 8;
    field_0x4f0 = 0.0f;
    speedF = 0.0f;
    speed.y = l_HIO.m15C;

    fopAcM_seStart(this, JA_SE_CM_GY_JUMP_L, 0);

    f32 s = 2.0f + REG10_F(0);
    cXyz scale(s, s + REG10_F(1), s);
    dComIfGp_particle_set(dPa_name::ID_IT_JN_MIZUSHIBUKI_A, &current.pos, NULL, &scale);
}

/* 000028B8-00002998       .text modeDelete__6daGy_cFv */
void daGy_c::modeDelete() {
    if (m500 != -1) {
        if (cLib_calcTimer(&m500) != 0) {
            return;
        }
        mEnemyIce.mLightShrinkTimer = 1;
        return;
    }

    if (m4FC != -1) {
        if (cLib_calcTimer(&m4FC) != 0) {
            return;
        }
        mEnemyIce.mFreezeDuration = l_HIO.m00C;
        return;
    }

    if (mD15 != 8) {
        return;
    }

    if (mpMorf->isStop()) {
        fopAcM_createDisappear(this, &current.pos, 10, daDisItem_IBALL_e, 0xff);
        fopAcM_delete(this);
    }
}

/* 00002998-00002A70       .text modeDeleteBombInit__6daGy_cFv */
void daGy_c::modeDeleteBombInit() {
    fopAcM_OffStatus(this, fopAcStts_SHOWMAP_e);
    cLib_offBit<u32>(attention_info.flags, fopAc_Attn_LOCKON_BATTLE_e);

    m2B0 = 9;
    mD15 = 7;

    field_0x4f0 = 0.0f;
    speedF = 0.0f;
    field_0x4e4 = 0.0f;

    speed.y = l_HIO.m15C;

    fopAcM_seStart(this, JA_SE_CM_GY_JUMP_L, 0);

    field_0x4ec = 10.0f * l_HIO.m0C8;
}

/* 00002A70-00002CBC       .text modeDeleteBomb__6daGy_cFv */
void daGy_c::modeDeleteBomb() {
    if (m920 > 0.01f) {
        fopAc_ac_c* player = dComIfGp_getPlayer(0);
        cXyz local_vec(0.0f, 0.0f, -m920);
        cXyz world_vec(0.0f, 0.0f, 0.0f);

        mDoMtx_YrotS(mDoMtx_stack_c::get(), fopAcM_searchActorAngleY(this, player));
        PSMTXMultVec(mDoMtx_stack_c::get(), &local_vec, &world_vec);

        current.pos.x += world_vec.x;
        current.pos.y += world_vec.y;
        current.pos.z += world_vec.z;

        cLib_addCalc0(&m920, 1.0f, 2.0f);
    }

    if (mD15 == 7) {
        field_0x4e8 = l_HIO.m168;
        if (field_0x4e4 <= 10.0f + l_HIO.m168) {
            mD15 = 9;
            field_0x4ec = l_HIO.m0C4;
        }
    }

    if (mD15 == 9) {
        if (field_0x4e4 >= l_HIO.m16C - 10.0f) {
            if (field_0x8ec == -1.0f) {
                field_0x8ec = l_HIO.m164;
            }
            field_0x4ec = l_HIO.m178;
            field_0x918 = l_HIO.m17C;
        }

        if (field_0x4e4 <= 10.0f + (l_HIO.m16C - l_HIO.m170)) {
            field_0x4e8 = l_HIO.m16C + l_HIO.m170;
        } else if (field_0x4e4 >= (l_HIO.m16C + l_HIO.m170) - 10.0f) {
            field_0x4e8 = l_HIO.m16C - l_HIO.m170;
        }

        if (field_0x8ec != -1.0f) {
            if (cLib_calcTimer(&field_0x8ec) == 0) {
                fopAcM_createDisappear(this, &current.pos, 10, daDisItem_IBALL_e, 0xff);
                fopAcM_delete(this);
            }
        }
    }
}

/* 00002CBC-00002E24       .text modeProcCall__6daGy_cFv */
void daGy_c::modeProcCall() {
    if (l_HIO.m095) {
        m2B0 = l_HIO.m09A;
    }

    typedef void (daGy_c::*ModeFunc)();
    static const ModeFunc mode_proc[] = {
        &daGy_c::modeDive,
        &daGy_c::modeCircle,
        &daGy_c::modeAttack,
        &daGy_c::modeAttackPlayer,
        &daGy_c::modeAttackBack,
        &daGy_c::modeWithAttack,
        &daGy_c::modeWithCircle,
        &daGy_c::modeDamage,
        &daGy_c::modeDelete,
        &daGy_c::modeDeleteBomb,
    };

    (this->*mode_proc[m2B0])();
}

/* 00002E24-00003004       .text createWave__6daGy_cFv */
void daGy_c::createWave() {
    static JGeometry::TVec3<f32> wave_l_direction(0.5f, 1.0f, -0.3f);
    static JGeometry::TVec3<f32> wave_r_direction(-0.5f, 1.0f, -0.3f);

    if (mD18.getEmitter() == NULL) {
        dComIfGp_particle_set(dPa_name::ID_AK_JN_SHIPWAVE00, (cXyz*)&field_0xdfc,
                              (csXyz*)&field_0xe08, NULL, 0xff, &mD18);
        if (mD18.getEmitter() != NULL) {
            mD18.getEmitter()->setDirection(wave_l_direction);
        }
    }

    if (mD7C.getEmitter() == NULL) {
        dComIfGp_particle_set(dPa_name::ID_AK_JN_SHIPWAVE00, (cXyz*)&field_0xdfc,
                              (csXyz*)&field_0xe08, NULL, 0xff, &mD7C);
        if (mD7C.getEmitter() != NULL) {
            mD7C.getEmitter()->setDirection(wave_r_direction);
        }
    }

    if (mDE0.getEmitter() == NULL) {
        dComIfGp_particle_set(dPa_name::ID_AK_JN_SHIPSPLASH00, (cXyz*)&field_0xdfc,
                              (csXyz*)&field_0xe08, NULL, 0xff, &mDE0);
    }
}

#include "d/actor/d_a_sea.h"

/* 00003004-00003268       .text setWave__6daGy_cFv */
void daGy_c::setWave() {
    daGy_HIO_c* hio = &l_HIO;
    f32 vel;
    f32 max_vel = l_HIO.m024;
    f32 vel_target;
    f32 rate = 1.0f;

    if (mD15 == 5 || mD15 == 8 || mD15 == 9 || mD15 == 6) {
        vel_target = vel = 0.0f;
    } else {
        vel = l_HIO.m018 * rate;
        vel_target = l_HIO.m010;
    }

    switch (m2B0) {
    case 1:
        max_vel = hio->m024 * (0.6f + REG12_F(13));
        vel = vel * (0.7f + REG12_F(13));
        break;
    case 0:
        vel_target = vel = 0.0f;
        break;
    case 8:
        vel_target = vel = 0.0f;
        break;
    }

    switch (mD15) {
    case 10:
        vel_target = vel = 0.0f;
        break;
    }

    field_0xe00 = daSea_calcWave(field_0xdfc, field_0xe04);
    field_0xe0a = current.angle.y;

    mD7C.setSpeed(vel);
    mD18.setSpeed(vel);
    mD7C.setMaxSpeed(max_vel);
    mD18.setMaxSpeed(max_vel);

    mD7C.setPitch(1.0f + l_HIO.m020);
    mD18.setPitch(1.0f - l_HIO.m020);

    cXyz p1(l_HIO.m028, l_HIO.m02C, l_HIO.m030);
    cXyz p2(l_HIO.m034, l_HIO.m038, l_HIO.m03C);
    cXyz p3 = p1;
    cXyz p4 = p2;
    mD7C.setAnchor(&p1, &p2);
    p3.x *= -1.0f;
    p4.x *= -1.0f;
    mD18.setAnchor(&p3, &p4);

    mD7C.setMaxDisSpeed(l_HIO.m01C);
    mD18.setMaxDisSpeed(l_HIO.m01C);

    cLib_addCalc2(&field_0xe10, vel_target, 0.1f, 10.0f);

    mDE0.setSpeed(field_0xe10);
    mDE0.setMaxSpeed(l_HIO.m014);
}

#include "d/d_com_inf_game.h"

/* 00003268-000032E4       .text lineCheck__6daGy_cFP4cXyzP4cXyz */
void daGy_c::lineCheck(cXyz* i_1, cXyz* o_2) {
    mE14.Set(i_1, o_2, this);
    if (dComIfG_Bgsp()->LineCross(&mE14)) {
        *o_2 = mE14.GetCross();
        field_0xe80 = true;
    }
}

#include "f_op/f_op_actor_mng.h"

/* 000032E4-000038EC       .text checkTgHit__6daGy_cFv */
void daGy_c::checkTgHit() {
    cXyz hit_pos;

    m8A8.Move();

    cCcD_Obj* hit_obj = m764.GetTgHitObj();
    hit_pos = *m764.GetTgHitPosP();

    if (hit_obj == NULL) {
        hit_obj = m50C.GetTgHitObj();
        hit_pos = *m50C.GetTgHitPosP();
    }

    if (hit_obj == NULL) {
        hit_obj = m638.GetTgHitObj();
        hit_pos = *m638.GetTgHitPosP();
    }

    if (hit_obj != NULL) {
        u8 damage_flag = 0;
        u32 at_type = hit_obj->GetAtType();

        if (at_type & AT_TYPE_BOMB) {
            health = 0;
            m920 = l_HIO.m14C;
            damage_flag = 1;
        } else if (at_type & AT_TYPE_NORMAL_ARROW) {
            fopAcM_seStart(this, JA_SE_LK_MS_WEP_HIT, 0x20);
            health -= 2;
            m920 = l_HIO.m150;
            damage_flag = 1;
        } else if (at_type & AT_TYPE_ICE_ARROW) {
            mCE8 = 9;
            fopAcM_seStart(this, JA_SE_LK_MS_WEP_HIT, 0x20);
            m4FC = 10;
            mDE0.remove();
            mD18.remove();
            mD7C.remove();
            damage_flag = 0;
        } else if (at_type & AT_TYPE_FIRE_ARROW) {
            fopAcM_seStart(this, JA_SE_LK_MS_WEP_HIT, 0x20);
            health -= 2;
            m920 = l_HIO.m150;
            damage_flag = 1;
        } else if (at_type & AT_TYPE_LIGHT_ARROW) {
            mCE8 = 0xB;
            fopAcM_seStart(this, JA_SE_LK_MS_WEP_HIT, 0x20);
            m500 = 10;
            mDE0.remove();
            mD18.remove();
            mD7C.remove();
            damage_flag = 0;
        } else if (at_type & AT_TYPE_HOOKSHOT) {
            fopAcM_seStart(this, JA_SE_LK_MS_WEP_HIT, 0x20);
            health -= 2;
            m920 = l_HIO.m154;
            damage_flag = 1;
        } else if (at_type & AT_TYPE_BOOMERANG) {
            fopAcM_seStart(this, JA_SE_LK_W_WEP_HIT, 0x20);
            health -= 2;
            m920 = l_HIO.m158;
            damage_flag = 1;
        }

        if (damage_flag == 1) {
            mDoAud_onEnemyDamage();

            daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
            dComIfGp_particle_set(dPa_name::ID_AK_JN_CRITICALHITFLASH, &hit_pos);

            if (mCE8 == 0xB) {
                cXyz scale(2.0f, 2.0f, 2.0f);
                dComIfGp_particle_set(dPa_name::ID_AK_JN_CRITICALHIT, &hit_pos, &player->shape_angle,
                                      &scale);
                fopAcM_seStart(this, JA_SE_LK_LAST_HIT, 0);
            } else if (health <= 0) {
                cXyz scale(2.0f, 2.0f, 2.0f);
                dComIfGp_particle_set(dPa_name::ID_AK_JN_CRITICALHIT, &hit_pos, &player->shape_angle,
                                      &scale);
                fopAcM_seStart(this, JA_SE_LK_LAST_HIT, 0);
                health = 0;
                fopAcM_monsSeStart(this, JA_SE_CV_GY_DIE, 0);

                if (m920 == l_HIO.m14C) {
                    modeDeleteBombInit();
                } else {
                    modeDeleteInit();
                }
            } else {
                m2B4 = m2B0;
                fopAcM_monsSeStart(this, JA_SE_CV_GY_DAMAGE, 0);
                modeDamageInit();
            }
        } else if (mCE8 == 9 || mCE8 == 0xB) {
            modeDeleteInit();
        }
    }
}

#include "d/d_bg_s_func.h"

/* 000038EC-000039AC       .text getWaterY__6daGy_cFv */
f32 daGy_c::getWaterY() {
    if (mAcch.ChkWaterHit()) {
        f32 waterY = mAcch.m_wtr.GetHeight();
        if (current.pos.y > waterY) {
            gravity = -2.5f;
            return current.pos.y;
        } else {
            gravity = 0.0f;
            return waterY;
        }
    } else {
        cXyz pos = current.pos;
        pos.y += 1000.0f;
        f32 waterY = dBgS_GetWaterHeight(pos);
        if (current.pos.y > waterY) {
            gravity = -2.5f;
            return current.pos.y;
        } else {
            gravity = 0.0f;
            return waterY;
        }
    }
}

#include "c/c_damagereaction.h"

/* 000039AC-00004264       .text _execute__6daGy_cFv */
bool daGy_c::_execute() {
    field_0x2b8 = NULL;

    fopAc_ac_c* ac;
    if (parentActorID != fpcM_ERROR_PROCESS_ID_e) {
        if (fopAcM_SearchByID(parentActorID, &ac)) {
            if (ac != NULL) {
                if (fopAcM_IsActor(ac) &&
                    (fopAcM_GetName(ac) == fpcNm_GY_CTRL_e || fopAcM_GetName(ac) == fpcNm_GY_CTRLB_e))
                {
                    field_0x2b8 = (daGy_Ctrl_c*)ac;
                } else {
                    if (cLib_calcTimer(&field_0x8f0) == 0) {
                        fopAcM_delete(this);
                    }
                    return true;
                }
            } else {
                fopAcM_delete(this);
                return true;
            }
        } else {
            fopAcM_delete(this);
            return true;
        }
    } else {
        fopAcM_delete(this);
        return true;
    }

    daGy_Ctrl_c* ctrl = field_0x2b8;
    shape_angle = current.angle;

    if (enemy_ice(&mEnemyIce)) {
        mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::now);
        mpMorf->calc();
        return true;
    }

    lineCheck(&current.pos, &field_0xe84);

    if (m2B0 != 0 && field_0xe80 != 0) {
        modeDiveInit();
    }

    if (m2B0 != 0 && ctrl->m324 == 3) {
        modeDiveInit();
    }

    cXyz* wavePos = &ctrl->m290[field_0x2ac];
    m2BC.x = wavePos->x;
    m2BC.y = wavePos->y;
    m2BC.z = wavePos->z;

    mpMorf->play(NULL, 0, 0);
    mpMorf->calc();

    if (m2B0 != 0) {
        setAtCollision();
        setCollision();
        checkTgHit();
    }

    if (gravity == 0.0f) {
        cLib_addCalc2(&field_0x4e4, field_0x4e8, field_0x918, field_0x4ec);
    }

    cLib_addCalc2(&speedF, field_0x4f0, 0.3f,
                  4.0f);

    setAnm();

    if (l_HIO.m097) {
        cXyz pos = current.pos;
        pos.y += 1000.0f;
        current.pos.y = dBgS_GetWaterHeight(pos);
        setMtx();
        setWave();
        return true;
    }

    modeProcCall();
    fopAcM_posMoveF(this, m8A8.GetCCMoveP());
    current.pos.y = getWaterY();

    s8 mode = mD15;
    if (mode == 5) {
        if (speed.y < -5.0f) {
            cLib_addCalcAngleS2(&current.angle.x, REG12_S(1) + 0x2000, 8, 0x400);
        } else if (speed.y > 5.0f) {
            cLib_addCalcAngleS2(&current.angle.x, REG12_S(2) - 0x1000, 8, 0x400);
        } else {
            cLib_addCalcAngleS2(&current.angle.x, 0, 8, 0x800);
        }

        if (gravity == 0.0f && field_0x91c < 0.0f) {
            mD15 = 1;

            f32 s = 2.0f + REG10_F(0);
            cXyz scale(s, s + REG10_F(1), s);
            dComIfGp_particle_set(dPa_name::ID_IT_JT_MIZUBASHIRA, &current.pos, NULL, &scale);
            dComIfGp_particle_set(dPa_name::ID_IT_JN_MIZUSHIBUKI_A, &current.pos, NULL, &scale);

            fopAcM_seStart(this, JA_SE_CM_GY_LANDING_L, 0);
        }
    } else if (mode == 6 || mode == 4) {
        if (m2B0 == 4) {
            cLib_addCalcAngleS2(&current.angle.x, 0, 8, 0x800);
        }

        daSea_calcWave(current.pos.x, current.pos.z);

        if (gravity == 0.0f && field_0x91c < 0.0f) {
            f32 s = 1.0f + REG10_F(0);
            cXyz scale(s, s + REG10_F(1), s);
            dComIfGp_particle_set(dPa_name::ID_IT_JN_MIZUSHIBUKI_A, &current.pos, NULL, &scale);

            fopAcM_seStart(this, JA_SE_CM_GY_LANDING_S, 0);
        }
    } else if (mode == 7 || mode == 9) {
        daSea_calcWave(current.pos.x, current.pos.z);

        if (gravity == 0.0f && field_0x91c < 0.0f) {
            f32 s = 2.0f + REG10_F(0);
            cXyz scale(s, s + REG10_F(1), s);
            dComIfGp_particle_set(dPa_name::ID_IT_JT_MIZUBASHIRA, &current.pos, NULL, &scale);
            dComIfGp_particle_set(dPa_name::ID_IT_JN_MIZUSHIBUKI_A, &current.pos, NULL, &scale);

            fopAcM_seStart(this, JA_SE_CM_GY_LANDING_L, 0);
        }
    } else {
        cLib_addCalcAngleS2(&current.angle.x, 0, 8, 0x800);
    }

    if (m2B0 != 0) {
        mAcch.CrrPos(*dComIfG_Bgsp());
    }

    tevStr.mRoomNo = current.roomNo;
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mAcch.m_gnd);

    setMtx();
    setWave();

    attention_info.position = current.pos;
    attention_info.position.y += 160.0f;

    eyePos = current.pos;
    eyePos.y += 60.0f;

    field_0x91c = gravity;

    if (mD15 != 5 && m2B0 != 0) {
        f32 dist = (current.pos - old.pos).abs();

        f32 ratio = dist / 30.0f;
        if (ratio <= 0.0f) {
            ratio = 0.0f;
        } else if (ratio >= 1.0f) {
            ratio = 1.0f;
        }

        fopAcM_seStart(this, JA_SE_CM_GY_CRUISING, (s8)(ratio * 100.0f));

        if (mD15 == 1) {
            f32 vol = ratio * l_HIO.m004;
            mpMorf->setPlaySpeed(vol < l_HIO.m008 ? l_HIO.m008 : vol);
        }
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    return true;
}

/* 00004264-00004560       .text drawDebug__6daGy_cFv */
void daGy_c::drawDebug() {
    GXColor color0 = {0xFF, 0xFF, 0x00, 0x80};
    GXColor color1 = {0xFF, 0xFF, 0x00, 0x80};
    GXColor color2 = {0xFF, 0xFF, 0x00, 0x80};
    static const GXColor color_ok = {0x00, 0xFF, 0x00, 0x80};
    static const GXColor color_ng = {0xFF, 0x00, 0x00, 0x80};
    GXColor color3 = {0xFF, 0x00, 0x00, 0x80};
    GXColor color4 = {0xFF, 0x00, 0x00, 0x80};
    GXColor color5 = {0xFF, 0x00, 0x00, 0x80};
    GXColor color6 = {0x00, 0xFF, 0x00, 0x80};
    GXColor color7 = {0xFF, 0xFF, 0x00, 0x80};
    GXColor color8 = {0xFF, 0xFF, 0x00, 0x80};
    GXColor color9 = {0xFF, 0x00, 0x00, 0x80};
    GXColor color10 = {0xFF, 0x00, 0x00, 0x80};
    GXColor color11 = {0xFF, 0x00, 0x00, 0x80};
    GXColor color12 = {0xFF, 0x00, 0x00, 0x80};
    GXColor color13 = {0xFF, 0xFF, 0x00, 0x80};

    u32 uVar1 = l_HIO.m180;
    if (uVar1 - 0x70000 != 0xFFFF || l_HIO.m180 != 0) {
        cXyz sp68 = current.pos;
        sp68.y += 10.0f;

        s16 angle1 = shape_angle.y + l_HIO.m180;

        cXyz sp5c = sp68;

        sp5c.z += l_HIO.m184 * cM_scos(angle1);

        sp5c.x += l_HIO.m184 * cM_ssin(angle1);

        cXyz sp50 = sp68;

        s16 angle2 = shape_angle.y - l_HIO.m180;

        sp50.z += l_HIO.m184 * cM_scos(angle2);

        sp50.x += l_HIO.m184 * cM_ssin(angle2);
    }

    u32 uVar2 = l_HIO.m140;
    if (uVar2 - 0x70000 != 0xFFFF || l_HIO.m140 != 0) {
        fopAc_ac_c* player = dComIfGp_getShipActor();
        if (player != NULL) {
            cXyz sp44 = player->current.pos;
            sp44.y += 100.0f;

            s16 angle1 = player->shape_angle.y + l_HIO.m140;

            cXyz sp38 = sp44;

            sp38.z += 3000.0f * cM_scos(angle1);

            sp38.x += 3000.0f * cM_ssin(angle1);

            cXyz sp2c = sp44;

            s16 angle2 = player->shape_angle.y - l_HIO.m140;

            sp2c.z += 3000.0f * cM_scos(angle2);

            sp2c.x += 3000.0f * cM_ssin(angle2);
        }
    }

    u32 uVar3 = l_HIO.m142;
    if (uVar3 - 0x70000 != 0xFFFF || l_HIO.m142 != 0) {
        fopAc_ac_c* player = dComIfGp_getShipActor();
        if (player != NULL) {
            cXyz sp20 = player->current.pos;
            sp20.y += 100.0f;

            s16 angle1 = player->shape_angle.y + l_HIO.m142;

            cXyz sp14 = sp20;

            sp14.z += 3000.0f * cM_scos(angle1);

            sp14.x += 3000.0f * cM_ssin(angle1);

            cXyz sp08 = sp20;

            s16 angle2 = player->shape_angle.y - l_HIO.m142;

            sp08.z += 3000.0f * cM_scos(angle2);

            sp08.x += 3000.0f * cM_ssin(angle2);
        }
    }
}

#include "d/d_material.h"
#include "d/d_snap.h"

/* 00004560-000046C8       .text _draw__6daGy_cFv */
bool daGy_c::_draw() {
    if (l_HIO.m094) {
        drawDebug();
    }

    if (field_0x4e4 <= l_HIO.m0A0 && mD15 != 5) {
        return true;
    }

    if (l_HIO.m096) {
        return true;
    }

    g_env_light.setLightTevColorType(mpMorf->getModel(), &tevStr);

    if (mEnemyIce.mFreezeTimer > 20) {
        dMat_control_c::iceEntryDL(mpMorf, -1, &m2D8);
        return true;
    }

    mpMorf->entryDL();
    dSnap_RegistFig(DSNAP_TYPE_UNKBC, this, 1.0f, 1.0f, 1.0f);

    cXyz pos(current.pos.x, current.pos.y + 150.0f, current.pos.z);
    m8E4 = dComIfGd_setShadow(m8E4, 0, mpMorf->getModel(), &pos, 800.0f, 40.0f, current.pos.y,
                              mAcch.GetGroundH(), mAcch.m_gnd, &tevStr);

    return true;
}

/* 000046C8-00004920       .text createInit__6daGy_cFv */
void daGy_c::createInit() {
    field_0xcf8 = ZeroQuat;

    gbaName = 0x1D;

    scale.z = scale.y = scale.x = l_HIO.m090;

    m500 = m4FC = -1;

    mEnemyIce.mpActor = this;

    mEnemyIce.m00C = 1;

    mEnemyIce.mCylHeight = mEnemyIce.mWallRadius = 0.0f;

    itemTableIdx = dComIfGp_CharTbl()->GetNameIndex("GyCtrl", 0);

    attention_info.flags = fopAc_Attn_LOCKON_BATTLE_e;
    attention_info.distances[fopAc_Attn_TYPE_BATTLE_e] = 0x22;

    mD15 = 1;

    field_0x918 = l_HIO.m174;

    field_0x8ec = -1;
    field_0x8f0 = 0x78;
    field_0x4e4 = l_HIO.m0A0;

    modeDiveInit();

    m2BC.x = current.pos.x;
    m2BC.y = current.pos.y;
    m2BC.z = current.pos.z;

    for (int i = 0; i < mpMorf->getModel()->getModelData()->getJointNum(); i++)
        ;

    setMtx();

    mpMorf->calc();

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);

    mAcchCir.SetWall(30.0f, 30.0f);

    mAcch.Set(&current.pos, &old.pos, this, 1, &mAcchCir, &speed);
    mAcch.SetWallNone();
    mAcch.SetRoofNone();

    gravity = -2.5f;

    fopAcM_posMoveF(this, NULL);

    cullMtx = mpMorf->getModel()->getBaseTRMtx();

    m8A8.Init(100, 100, this);

    m638.Set(l_sph_src);
    m638.SetStts(&m8A8);
    m764.Set(l_cps_src);
    m764.SetStts(&m8A8);
    m50C.Set(l_sph_head_src);
    m50C.SetStts(&m8A8);

    max_health = 4;
    health = max_health;

    createWave();
}

#include "d/actor/d_a_gy_ctrl.h"

inline daGy_c::daGy_c() {}

/* 00004920-00004A80       .text _create__6daGy_cFv */
cPhs_State daGy_c::_create() {
    fopAcM_ct(this, daGy_c);

    cPhs_State state = dComIfG_resLoad(&mPhs, m_arc_name);
    if (state == cPhs_COMPLEATE_e) {
        if (!dComIfGs_checkGetItem(dItemNo_BOOMERANG_e)) {
            return cPhs_ERROR_e;
        }

        fopAc_ac_c* ac;
        if (parentActorID != fpcM_ERROR_PROCESS_ID_e) {
            if (fopAcM_SearchByID(parentActorID, &ac)) {
                if (ac != NULL) {
                    if (fopAcM_IsActor(ac) &&
                        (fopAcM_GetName(ac) == fpcNm_GY_CTRL_e || fopAcM_GetName(ac) == fpcNm_GY_CTRLB_e))
                    {
                        field_0x2b8 = (daGy_Ctrl_c*)ac;
                        daGy_Ctrl_c* ctrl = field_0x2b8;
                        field_0x2ac = ctrl->m31C;
                        ctrl->m31C++;
                        if (fopAcM_GetName(ac) == fpcNm_GY_CTRL_e) {
                            fopAcM_setStageLayer(this);
                        }
                    } else {
                        return cPhs_ERROR_e;
                    }
                } else {
                    return cPhs_ERROR_e;
                }
            } else {
                return cPhs_ERROR_e;
            }
        } else {
            return cPhs_ERROR_e;
        }

        if (!fopAcM_entrySolidHeap(this, createHeap_CB, 0x3fa0)) {
            return cPhs_ERROR_e;
        }

        createInit();
    }
    return state;
}

/* 00005A80-00005AD4       .text _delete__6daGy_cFv */
bool daGy_c::_delete() {
    dComIfG_resDelete(&mPhs, m_arc_name);
    mD7C.remove();
    mD18.remove();
    mDE0.remove();
    return true;
}

/* 00005AD4-00005AF4       .text daGyCreate__FPv */
static cPhs_State daGyCreate(void* i_this) {
    return ((daGy_c*)i_this)->_create();
}

/* 00005AF4-00005B18       .text daGyDelete__FPv */
static BOOL daGyDelete(void* i_this) {
    return ((daGy_c*)i_this)->_delete();
}

/* 00005B18-00005B3C       .text daGyExecute__FPv */
static BOOL daGyExecute(void* i_this) {
    return ((daGy_c*)i_this)->_execute();
}

/* 00005B3C-00005B60       .text daGyDraw__FPv */
static BOOL daGyDraw(void* i_this) {
    return ((daGy_c*)i_this)->_draw();
}

/* 00005B60-00005B68       .text daGyIsDelete__FPv */
static BOOL daGyIsDelete(void*) {
    return TRUE;
}

static actor_method_class daGyMethodTable = {
    (process_method_func)daGyCreate,
    (process_method_func)daGyDelete,
    (process_method_func)daGyExecute,
    (process_method_func)daGyIsDelete,
    (process_method_func)daGyDraw,
};

actor_process_profile_definition g_profile_GY = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_GY_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daGy_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_GY_e,
    /* Actor SubMtd */ &daGyMethodTable,
    /* Status       */ fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ENEMY_e,
    /* Cull Type    */ fopAc_CULLBOX_4_e,
};
