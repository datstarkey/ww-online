/**
 * d_a_obj_try.cpp
 * Object - Tower of the Gods - Glowing blue/pink statue/pillar
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_obj_try.h"
#include "d/d_bg_s_func.h"
#include "d/d_bg_s_wtr_chk.h"
#include "f_op/f_op_kankyo_mng.h"
#include "f_op/f_op_actor_iter.h"
#include "f_op/f_op_camera.h"
#include "d/d_kankyo.h"
#include "d/d_a_obj.h"
#include "d/d_com_inf_game.h"
#include "d/d_vibration.h"
#include "res/Object/Hseki.h"
#include "JSystem/J3DGraphAnimator/J3DModel.h"

namespace daObjTry {
namespace {
struct AttrBase_c {
    /* 0x00 */ f32 mBoundRate;
    /* 0x04 */ f32 mBoundMin;
    /* 0x08 */ f32 mBoundMax;
};

const AttrBase_c L_attrBase = {
    -0.6f,
    15.0f,
    15.0f,
};

inline const AttrBase_c& attr_base() { return L_attrBase; }
}; // namespace
}; // namespace daObjTry

const char daObjTry::Act_c::M_arcname[] = "Hseki";

const dCcD_SrcCyl daObjTry::Act_c::M_cyl_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ AT_TYPE_UNK8,
        /* SrcObjAt  Atp     */ 1,
        /* SrcObjAt  SPrm    */ cCcD_AtSPrm_Set_e | cCcD_AtSPrm_VsEnemy_e | cCcD_AtSPrm_VsOther_e,
        /* SrcObjTg  Type    */ 0xFF3DFEFF,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsOther_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ 0,
        /* SrcGObjAt Spl     */ 0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ 0,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ 0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ 0,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCylS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 0.0f,
        /* Height */ 0.0f,
    }},
};

const daObjTry::Attr_c daObjTry::Act_c::M_attr[13] = {
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0x1240,
        1.0f, 0.75f, 1.12f,
        false, false, false, false, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0x1240,
        1.0f, 0.75f, 1.12f,
        false, false, false, false, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, false, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, false, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, false, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, dRes_INDEX_HSEKI_BRK_HMON1_e,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0x1BE0,
        1.0f, 0.75f, 1.12f,
        true, true, false, true, true, true, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON2_e, dRes_INDEX_HSEKI_BRK_HMON2_e,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0x1BE0,
        1.0f, 0.75f, 1.12f,
        true, true, false, true, true, true, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, true, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, true, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, true, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, true, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, true, false, false, 0, 0,
    },
    {
        -6.0f, 70.0f, 150.0f, 200,
        27.0f, 36.0f, 0.0f,
        0.002f, 0.0002f, 4.0f, 0.2f, 0.02f, 30.0f,
        15, 15, 0.1f, 0.02f,
        0x96, 0x05, 0x64, 0x04, 0x7D, 0x05,
        /* mBdlIdx, mBrkIdx */ dRes_INDEX_HSEKI_BDL_HMON1_e, -1,
        72, 56, 150, 150,
        0x692B, 0x6929, 0x6918, 0x0D,
        /* mHeapSize */ 0xFFFF,
        1.0f, 0.75f, 1.12f,
        false, false, true, true, false, false, 0, 0,
    },
};

bool daObjTry::Act_c::M_bingo;
bool daObjTry::Act_c::M_restart;

/* 00000078-00000090       .text prm_set_swSave__Q28daObjTry5Act_cFi */
void daObjTry::Act_c::prm_set_swSave(int i_swSave) {
    u32 param = fopAcM_GetParam(this);
    param &= ~(0xFF << PRM_SWSAVE_S);
    param |= (i_swSave & 0xFF) << PRM_SWSAVE_S;
    fopAcM_SetParam(this, param);
}

/* 00000090-000000B4       .text solidHeapCB__Q28daObjTry5Act_cFP10fopAc_ac_c */
BOOL daObjTry::Act_c::solidHeapCB(fopAc_ac_c* i_this) {
    return ((Act_c*)i_this)->create_heap();
}

/* 000000B4-0000026C       .text create_heap__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::create_heap() {
    bool ret = false;
    J3DModelData* mdl_data = static_cast<J3DModelData*>(dComIfG_getObjectRes(M_arcname, attr().mBdlIdx));
    JUT_ASSERT(0x3F1, mdl_data != NULL);

    mpModel = mDoExt_J3DModel__create(mdl_data, 0, 0x11020203);
    int brk_ret = 1;
    if (attr().mBrkIdx >= 0) {
        J3DAnmTevRegKey* brk = static_cast<J3DAnmTevRegKey*>(dComIfG_getObjectRes(M_arcname, attr().mBrkIdx));
        JUT_ASSERT(0x3FB, brk != NULL);

        brk_ret = mBrk.init(mdl_data, brk, TRUE, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, false, FALSE);
        if (prm_get_dummy() == 0) {
            mBrk.setFrame(mBrk.getEndFrame());
        }
    }

    if (mpModel != NULL && brk_ret) {
        ret = true;
    }
    return ret;
}

/* 0000026C-0000038C       .text init_cc__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::init_cc() {
    mStts.Init(attr().mWeight, 0xFF, this);
    mCyl.Set(M_cyl_src);
    mCyl.SetStts(&mStts);
    mCyl.SetR(attr().mCylR);
    mCyl.SetH(attr().mCylH);
    mCyl.SetAtVec(*(cXyz*)&cXyz::Zero);
    mCyl.SetTgVec(*(cXyz*)&cXyz::Zero);
    mCyl.OnTgShield();
}

/* 0000038C-00000428       .text search_sameType__Q28daObjTry5Act_cFPvPv */
void* daObjTry::Act_c::search_sameType(void* i_ac, void* i_this) {
    Act_c* self = (Act_c*)i_this;
    Act_c* ac = (Act_c*)i_ac;
    if (ac != NULL && fopAc_IsActor(ac) && fopAcM_GetName(ac) == fpcNm_Obj_Try_e && ac != self &&
        ac->prm_get_type() == self->prm_get_type() && ac->mAppear)
    {
        return ac;
    }
    return NULL;
}

/* 00000428-00000528       .text chk_appear__Q28daObjTry5Act_cCFv */
bool daObjTry::Act_c::chk_appear() const {
    bool ret = true;
    if (attr().mChkSwitch) {
        u8 dummy = prm_get_dummy() != 0;
        bool sw = fopAcM_isSwitch((Act_c*)this, prm_get_swSave()) != FALSE;
        if ((!sw && dummy) || (sw && !dummy)) {
            ret = false;
        }
    }
    if (attr().mUnique && fopAcIt_Judge(search_sameType, (void*)this) != NULL) {
        ret = false;
    }
    return ret;
}

/* 00000528-000008D8       .text _create__Q28daObjTry5Act_cFv */
cPhs_State daObjTry::Act_c::_create() {
    fopAcM_ct(this, Act_c);

    mType = prm_get_type();
    mAppear = chk_appear();

    cPhs_State ret = cPhs_ERROR_e;
    if (mAppear) {
        ret = dComIfG_resLoad(&mPhs, M_arcname);
        if (ret == cPhs_COMPLEATE_e) {
            if (fopAcM_entrySolidHeap(this, solidHeapCB, attr().mHeapSize)) {
                mAcchCir.SetWall(30.0f, attr().mCylR);
                mAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir,
                          fopAcM_GetSpeed_p(this), fopAcM_GetAngle_p(this), fopAcM_GetShapeAngle_p(this));
                mAcch.ClrWaterNone();
                mAcch.ClrRoofNone();
                mAcch.SetRoofCrrHeight(attr().mSinkDownDepth);
                init_cc();
                fopAcM_SetMtx(this, mpModel->getBaseTRMtx());
                cull_set_draw();
                fopAcM_SetGravity(this, attr().mGravity);
                if (mType == UNK_5 || mType == UNK_6) {
                    cLib_onBit<u32>(actor_status, fopAcStts_UNK2000000_e);
                }
                fopAcM_posMoveF(this, NULL);
                mAcch.CrrPos(*dComIfG_Bgsp());
                mAcch.ClrGroundLanding();
                mOnGround = true;
                mLandTimer = 20;
                cLib_onBit<u32>(attention_info.flags, fopAc_Attn_ACTION_CARRY_e);
                attention_info.distances[fopAc_Attn_TYPE_CARRY_e] = 0x17;
                attention_info.position.x = current.pos.x;
                attention_info.position.y = current.pos.y + attr().mAttnH;
                attention_info.position.z = current.pos.z;
                mForceExec = true;
                mDropTimer = 0;
                mOldSpeedY = 0.0f;
                mTimer = 0;
                mTargetAngle = 0;
                init_mtx();
                mBingoEff = false;
                mStayCorrect = false;
                mCorrect = false;
                field_0x64e = true;
                mRoomChanged = false;
                field_0x652 = false;
                mode_wait_init();
                fopAcM_SetModel(this, mpModel);
            } else {
                ret = cPhs_ERROR_e;
            }
        }
    }
    return ret;
}

/* 00000C88-00000D5C       .text _delete__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::_delete() {
    if (mAppear) {
        mFollowCb.remove();
        if (mpBingoEmitter != NULL) {
            mpBingoEmitter->becomeInvalidEmitter();
        }
        if (mType == UNK_5) {
            dComIfGs_offTmpBit(dSv_event_tmp_flag_c::UNK_0108);
        } else if (mType == UNK_6) {
            dComIfGs_offTmpBit(dSv_event_tmp_flag_c::UNK_0110);
        }
        fopAcM_SetModel(this, NULL);
        dComIfG_deleteObjectRes(M_arcname);
    }
    return true;
}

/* 00000D5C-00000EA8       .text mode_restart_init__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_restart_init() {
    mCyl.OffAtSPrmBit(cCcD_AtSPrm_Set_e);
    mCyl.OffTgSPrmBit(cCcD_TgSPrm_Set_e);
    mCyl.OnCoSPrmBit(cCcD_CoSPrm_Set_e);
    mAcch.SetRoofNone();
    mAcch.SetWallNone();
    mAcch.ClrGrndNone();
    mAcch.SetWaterNone();
    mAcch.OffLineCheck();
    speedF = 0.0f;
    mTimer = 110;
    current.pos.set(home.pos.x, home.pos.y + (-10.0f - attr().mSinkDownDepth), home.pos.z);
    shape_angle.set(home.angle.x, home.angle.y - 0x8000, home.angle.z);
    current.angle = shape_angle;
    old.pos = current.pos;
    old.angle = current.angle;
    cLib_offBit<u32>(attention_info.flags, fopAc_Attn_ACTION_CARRY_e);
    mMode = MODE_RESTART;
}

/* 00000EA8-00000FFC       .text mode_restart__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_restart() {
    mTimer--;
    if (mTimer < 80) {
        M_restart = true;
    }
    mLandTimer = 20;
    mOnGround = true;

    if (mTimer == 0) {
        shape_angle.y = home.angle.y;
        current.angle.y = shape_angle.y;
        current.pos.y = home.pos.y;
        mode_wait_init();
    } else if (mTimer < 50) {
        s16 ang = mTimer * 655.36f;
        f32 rate = 0.5f * (1.0f - cM_scos(ang));
        current.pos.y = home.pos.y + rate * (-10.0f - attr().mSinkDownDepth);
        shape_angle.y = (s16)(-32768.0f * rate) + home.angle.y;
        current.angle.y = shape_angle.y;
    }
}

/* 00000FFC-00001074       .text mode_wait_init__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_wait_init() {
    mCyl.OffAtSPrmBit(cCcD_AtSPrm_Set_e);
    mCyl.OnTgSPrmBit(cCcD_TgSPrm_Set_e);
    mCyl.OnCoSPrmBit(cCcD_CoSPrm_Set_e);
    mAcch.ClrRoofNone();
    mAcch.ClrWallNone();
    mAcch.ClrGrndNone();
    mAcch.ClrWaterNone();
    mAcch.OffLineCheck();
    speedF = 0.0f;
    mMode = MODE_WAIT;
}

/* 00001074-000012C4       .text mode_wait__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_wait() {
    bool stay = false;
    if (attr().mStayCorrect && mStayCorrect) {
        stay = true;
    }

    if (attr().mCorrectPos && mCorrect) {
        f32 pos_y = current.pos.y;
        cLib_chasePos(&current.pos, mCorrectPos, 10.0f);
        current.pos.y = pos_y;
        if (mCorrectPos.abs2XZ(current.pos) < 25.0f) {
            current.pos.x = mCorrectPos.x;
            current.pos.z = mCorrectPos.z;
            if (mCorrectPos.abs2(current.pos) < 1.0f) {
                M_bingo = true;
            }
        }
        s16 diff = shape_angle.y - mCorrectAngle;
        mTargetAngle = (diff + 0x2000 & 0xC000) + mCorrectAngle;
        cLib_addCalcAngleS(&shape_angle.y, mTargetAngle, 3, 0x1800, 0x800);
    } else if (!stay) {
        bound();
    }

    u32 gnd_hit = mAcch.ChkGroundHit();
    if (gnd_hit && !stay) {
        cLib_onBit<u32>(attention_info.flags, fopAc_Attn_ACTION_CARRY_e);
    } else {
        cLib_offBit<u32>(attention_info.flags, fopAc_Attn_ACTION_CARRY_e);
    }

    if (stay) {
        field_0x64e = true;
    }

    bool stop = gnd_hit && (stay || mCorrect);

    const cXyz* move = stop ? &cXyz::Zero : mStts.GetCCMoveP();

    if (mAcch.ChkGroundHit()) {
        fopAcM_SetGravity(this, attr().mGravity);
        fopAcM_posMoveF(this, move);
    } else {
        f32 gravity, viscous_resist, inert_resist;
        calc_drop_param(&gravity, &viscous_resist, &inert_resist);
        fopAcM_SetGravity(this, gravity);
        daObj::posMoveF_stream(this, move, &cXyz::Zero, viscous_resist, inert_resist);
    }
}

/* 000012C4-00001344       .text mode_carry_init__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_carry_init() {
    mCyl.OffAtSPrmBit(cCcD_AtSPrm_Set_e);
    mCyl.OnTgSPrmBit(cCcD_TgSPrm_Set_e);
    mCyl.ClrCoSet();
    mAcch.ClrRoofNone();
    mAcch.ClrWallNone();
    mAcch.ClrGrndNone();
    mAcch.ClrWaterNone();
    mAcch.OffLineCheck();
    cLib_offBit<u32>(attention_info.flags, fopAc_Attn_ACTION_CARRY_e);
    mTimer = 8;
    mMode = MODE_CARRY;
}

/* 00001344-000013D4       .text mode_carry__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_carry() {
    if (mTimer > 0) {
        mTimer--;
    }

    speed.y = 0.0f;
    if (!fopAcM_checkCarryNow(this)) {
        if (speedF > 0.0f) {
            mode_drop_init();
            mode_drop();
        } else {
            daObj::SetCurrentRoomNo(this, &mAcch.m_gnd);
            mDropTimer = 2;
            mode_wait_init();
        }
    }
}

/* 000013D4-000014A8       .text mode_drop_init__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_drop_init() {
    f32 speed_f = attr().mDropSpeedF + dComIfGp_getPlayer(0)->speedF * attr().mDropSpeedFRate;
    mCyl.OnAtSPrmBit(cCcD_AtSPrm_Set_e);
    mCyl.OnTgSPrmBit(cCcD_TgSPrm_Set_e);
    mCyl.OnCoSPrmBit(cCcD_CoSPrm_Set_e);
    mAcch.ClrRoofNone();
    mAcch.ClrWallNone();
    mAcch.ClrGrndNone();
    mAcch.ClrWaterNone();
    mAcch.OnLineCheck();
    cLib_offBit<u32>(attention_info.flags, fopAc_Attn_ACTION_CARRY_e);
    speed.y = attr().mDropSpeedY;
    speedF = speed_f;
    fopAcM_SetGravity(this, attr().mGravity);
    mMode = MODE_DROP;
}

/* 000014A8-00001518       .text mode_drop__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_drop() {
    daObj::SetCurrentRoomNo(this, &mAcch.m_gnd);
    bound();

    f32 gravity, viscous_resist, inert_resist;
    calc_drop_param(&gravity, &viscous_resist, &inert_resist);
    fopAcM_SetGravity(this, gravity);
    daObj::posMoveF_stream(this, mStts.GetCCMoveP(), &cXyz::Zero, viscous_resist, inert_resist);
}

/* 00001518-000016A0       .text mode_sink_init__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_sink_init() {
    mCyl.OffAtSPrmBit(cCcD_AtSPrm_Set_e);
    mCyl.OnTgSPrmBit(cCcD_TgSPrm_Set_e);
    mCyl.OnCoSPrmBit(cCcD_CoSPrm_Set_e);
    mAcch.SetRoofNone();
    mAcch.ClrWallNone();
    mAcch.ClrGrndNone();
    mAcch.ClrWaterNone();
    mAcch.OnLineCheck();
    fopAcM_SetGravity(this, attr().mGravity + attr().mSinkGravity);

    f32 spd = std::sqrtf(speed.y * speed.y + speedF * speedF);
    if (spd > attr().mSinkMaxSpeed) {
        f32 rate = attr().mSinkMaxSpeed / spd;
        speed *= rate;
        speedF *= rate;
    }

    cLib_offBit<u32>(attention_info.flags, fopAc_Attn_ACTION_CARRY_e);
    mMode = MODE_SINK;
}

/* 000016A0-0000177C       .text mode_sink__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::mode_sink() {
    f32 water_h = get_water_h();
    f32 ground_h = mAcch.GetGroundH();
    daObj::SetCurrentRoomNo(this, &mAcch.m_gnd);

    if (speed.y != 0.0f && current.pos.y + attr().mSinkH > water_h && (water_h - attr().mSinkH) > ground_h) {
        current.pos.y = water_h - attr().mSinkH;
    }

    daObj::posMoveF_stream(this, mStts.GetCCMoveP(), &cXyz::Zero, attr().mSinkViscousResist, attr().mSinkInertResist);
}

/* 0000177C-00001B58       .text mode_proc_call__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::mode_proc_call() {
    typedef void (Act_c::*ModeFunc)();
    static ModeFunc mode_proc[] = {
        &Act_c::mode_restart,
        &Act_c::mode_wait,
        &Act_c::mode_carry,
        &Act_c::mode_drop,
        &Act_c::mode_sink,
    };

    if (fopAcM_checkCarryNow(this) && mMode != MODE_CARRY) {
        mode_carry_init();
    }

    M_bingo = false;
    M_restart = false;
    (this->*mode_proc[mMode])();

    if (attr().mBrkIdx >= 0) {
        if (M_bingo || M_restart) {
            eff_set_bingo(M_bingo, M_restart);
        } else {
            eff_clr_bingo();
        }

        if (M_bingo || M_restart || mMode == MODE_CARRY) {
            mBrk.setPlaySpeed(1.0f);
            mBrk.setPlayMode(J3DFrameCtrl::EMode_LOOP);
            fopAcM_seStart(this, JA_SE_OBJ_RES_ST_BLINK, 0);
        } else {
            mBrk.setPlayMode(J3DFrameCtrl::EMode_NONE);
        }

        if (mType == UNK_5) {
            if (mMode == MODE_CARRY) {
                dComIfGs_onTmpBit(dSv_event_tmp_flag_c::UNK_0108);
            } else {
                dComIfGs_offTmpBit(dSv_event_tmp_flag_c::UNK_0108);
            }
        } else if (mType == UNK_6) {
            if (mMode == MODE_CARRY) {
                dComIfGs_onTmpBit(dSv_event_tmp_flag_c::UNK_0110);
            } else {
                dComIfGs_offTmpBit(dSv_event_tmp_flag_c::UNK_0110);
            }
        }

        mBrk.play();
    }

    mOldSpeedY = speed.y;
    cXyz pos = current.pos;
    cXyz spd = speed;
    mAcch.CrrPos(*dComIfG_Bgsp());
    if (dComIfG_Bgsp()->ChkMoveBG(mAcch.m_gnd)) {
        mForceExec = true;
    }
    if (mMode == MODE_RESTART || mMode == MODE_CARRY) {
        current.pos = pos;
        speed = spd;
    }

    damage_bg_proc_directly();

    if (mMode != MODE_CARRY) {
        tevStr.mRoomNo = current.roomNo;
        tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mAcch.m_gnd);
    }

    if (current.roomNo != home.roomNo) {
        mRoomChanged = true;
        if (attr().mClrSwSave && prm_get_swSave() != 0xFF) {
            prm_set_swSave(0xFF);
        }
    }

    if (dComIfGp_event_runCheck()) {
        field_0x64e = true;
    }
    return true;
}

/* 00001B58-00001B90       .text cull_set_draw__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::cull_set_draw() {
    fopAcM_setCullSizeSphere(this, 0.0f, 65.0f, 0.0f, 100.0f);
}

/* 00001B90-00001BC8       .text cull_set_move__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::cull_set_move() {
    fopAcM_setCullSizeSphere(this, 0.0f, 65.0f, 0.0f, 300.0f);
}

/* 00001BC8-00001BE8       .text damaged__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::damaged() {
    fopAcM_cancelCarryNow(this);
}

/* 00001BE8-00001CD8       .text damage_cc_proc__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::damage_cc_proc() {
    if (mCyl.ChkAtHit()) {
        mCyl.ClrAtHit();
        speedF *= 0.3f;
    } else if (mCyl.ChkTgHit()) {
        mCyl.GetTgHitObj();
        daObj::HitSeStart(&eyePos, current.roomNo, &mCyl, attr().mSeHit);
        set_senv(attr().mSeHitP1, attr().mSeHitP2);
        daObj::HitEff_hibana(this, &mCyl);
        mCyl.ClrTgHit();
    }
    return false;
}

/* 00001CD8-00001E98       .text damage_bg_proc__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::damage_bg_proc() {
    int home_room;
    bool ret;
    u8 broken;
    bool sunk;
    u32 gnd_hit;
    int stay_no;
    home_room = home.roomNo;
    stay_no = dStage_roomControl_c::getStayNo();
    gnd_hit = mAcch.ChkGroundHit();
    sunk = chk_sink_water();
    ret = false;
    broken = 0;
    if (mMode == MODE_WAIT) {
        if (sunk) {
            se_fall_water();
            if (!(speed.y == 0.0f)) {
                eff_hit_water_splash();
                mSunk = true;
            } else {
                mSunk = false;
            }
            mode_sink_init();
        }
    } else if (mMode == MODE_DROP) {
        if (gnd_hit) {
            mode_wait_init();
        } else if (sunk) {
            se_fall_water();
            eff_hit_water_splash();
            mSunk = true;
            mode_sink_init();
        }
    } else if (mMode == MODE_SINK) {
        if (chk_sinkdown_water()) {
            broken = 1;
            if (!mSunk) {
                eff_hit_water_splash();
            }
        } else if (!sunk) {
            mode_wait_init();
        }
    }

    if (gnd_hit && dComIfG_Bgsp()->GetGroundCode(mAcch.m_gnd) == 4) {
        broken = 1;
    }

    if (broken) {
        if (home_room == stay_no && (attr().mUnique || !mRoomChanged)) {
            mode_restart_init();
        } else {
            damaged();
            ret = true;
        }
    }

    return ret;
}

/* 00001E98-00001FEC       .text damage_bg_proc_directly__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::damage_bg_proc_directly() {
    bool gnd_hit = mAcch.ChkGroundHit();
    if (mMode == MODE_DROP && (mAcch.ChkRoofHit() || chk_sink_water() || gnd_hit)) {
        cam_lockoff();
    }

    if (mLandTimer > 0) {
        mLandTimer--;
    } else if (gnd_hit) {
        if (!mOnGround && (mMode == MODE_WAIT || mMode == MODE_DROP)) {
            fopAcM_seStart(this, attr().mSeLand, dComIfG_Bgsp()->GetMtrlSndId(mAcch.m_gnd));
            if (mPrevMode != MODE_SINK) {
                eff_land_smoke();
            }
            mOnGround = true;
            mLandTimer = 20;
            make_vib();
        }
    } else {
        mOnGround = false;
    }
}

/* 00001FEC-00002034       .text chk_sink_water__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::chk_sink_water() {
    bool ret = false;
    if (mAcch.m_flags & dBgS_Acch::WATER_HIT) {
        if (mAcch.m_wtr.GetHeight() > current.pos.y + attr().mSinkH) {
            ret = true;
        }
    }
    return ret;
}

/* 00002034-000020AC       .text chk_sinkdown_water__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::chk_sinkdown_water() {
    bool ret = false;
    if (mAcch.ChkWaterHit()) {
        if (mAcch.m_wtr.GetHeight() > current.pos.y + attr().mSinkDownDepth + 50.0f) {
            ret = true;
        }
    }
    return ret;
}

/* 000020AC-00002218       .text calc_drop_param__Q28daObjTry5Act_cCFPfPfPf */
void daObjTry::Act_c::calc_drop_param(float* o_gravity, float* o_viscous_resist, float* o_inert_resist) const {
    bool water_hit = ((Act_c*)this)->mAcch.ChkWaterHit();
    if (water_hit) {
        f32 water_h = ((Act_c*)this)->mAcch.m_wtr.GetHeight();
        f32 diff = current.pos.y - water_h;
        f32 rate;
        if (diff >= 0.0f) {
            rate = 0.0f;
        } else if (diff <= -attr().mSinkDownDepth) {
            rate = 0.5f;
        } else {
            rate = -diff * (0.5f / attr().mSinkDownDepth);
        }

        f32 t = 1.0f - rate;
        *o_viscous_resist = rate * attr().mSinkViscousResist + t * attr().mViscousResist;
        *o_inert_resist = rate * attr().mSinkInertResist + t * attr().mInertResist;
        *o_gravity = rate * attr().mSinkGravity + attr().mGravity;
    } else {
        *o_viscous_resist = attr().mViscousResist;
        *o_inert_resist = attr().mInertResist;
        *o_gravity = attr().mGravity;
    }
}

/* 00002218-000022F4       .text bound__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::bound() {
    bool ret = mAcch.ChkGroundHit();
    if (mAcch.ChkWallHit()) {
        speedF *= 0.3f;
        current.angle.y = (mAcchCir.GetWallAngleY() * 2) - (current.angle.y + 0x8000);
    }

    if (mAcch.ChkGroundLanding()) {
        f32 bound_speed = std::fabsf(mOldSpeedY * attr_base().mBoundRate);
        if (bound_speed > attr_base().mBoundMin) {
            if (bound_speed > attr_base().mBoundMax) {
                speed.y = attr_base().mBoundMax;
            } else {
                speed.y = bound_speed;
            }
            ret = false;
        }
    } else if (mAcch.ChkGroundHit()) {
        cLib_addCalc(&speedF, 0.0f, 0.5f, 5.0f, 1.0f);
    }
    return ret;
}

/* 000022F4-0000240C       .text se_fall_water__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::se_fall_water() {
    cBgS_PolyInfo* poly[2] = {
        &mAcch.m_wtr,
        &mAcch.m_gnd,
    };

    u32 mtrl_sound_id = 0x13;
    for (int i = 0; i < ARRAY_SIZE(poly); i++) {
        int bg_index = poly[i]->GetBgIndex();
        if (bg_index >= 0 && bg_index < 0x100) {
            mtrl_sound_id = dComIfG_Bgsp()->GetMtrlSndId(*poly[i]);
            break;
        }
    }

    fopAcM_seStart(this, attr().mSeFallWater, mtrl_sound_id);
    set_senv(attr().mSeFallWaterP1, attr().mSeFallWaterP2);
}

/* 0000240C-00002460       .text set_senv__Q28daObjTry5Act_cCFii */
void daObjTry::Act_c::set_senv(int i_a, int i_b) const {
    dKy_Sound_set(current.pos, i_a, fopAcM_GetID((Act_c*)this), i_b);
}

/* 00002460-00002498       .text cam_lockoff__Q28daObjTry5Act_cCFv */
void daObjTry::Act_c::cam_lockoff() const {
#if TARGET_PC
    dComIfGp_getCamera(0)->mCamera.ForceLockOff(mBsPcId);
#else
    dComIfGp_getCamera(0)->mCamera.ForceLockOff(base.base.mBsPcId);
#endif
}

/* 00002498-00002504       .text set_mtx__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::set_mtx() {
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::ZXYrotM(shape_angle);
    mpModel->setBaseTRMtx(mDoMtx_stack_c::get());
}

/* 00002504-00002540       .text init_mtx__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::init_mtx() {
    mpModel->setBaseScale(scale);
    set_mtx();
}

/* 00002540-00002718       .text eff_set_bingo__Q28daObjTry5Act_cFbb */
void daObjTry::Act_c::eff_set_bingo(bool i_playSe, bool i_useHome) {
    static GXColor prm0[2] = { { 0x0C, 0x18, 0x48, 0x00 }, { 0x48, 0x0C, 0x18, 0x00 } };
    static GXColor env0[2] = { { 0x42, 0x49, 0xCA, 0x00 }, { 0x67, 0x3E, 0xCA, 0x00 } };
    static GXColor prm1[2] = { { 0x02, 0x0C, 0x38, 0x00 }, { 0x38, 0x02, 0x0C, 0x00 } };
    static GXColor env1[2] = { { 0x08, 0x3E, 0x1B, 0x00 }, { 0x2A, 0x3E, 0x62, 0x00 } };

    if (!mBingoEff) {
        int idx = mType != UNK_5 ? 1 : 0;

        g_dComIfG_gameInfo.play.getParticle()->setNormal(
            dPa_name::ID_AK_SN_SIRENKEY00, &current.pos, &shape_angle, NULL, 0xff,
            &mFollowCb, -1, &prm0[idx], &env0[idx], NULL);

        cXyz* pos2 = i_useHome ? &home.pos : &current.pos;
        csXyz angleTmp(0, mTargetAngle, 0);

        mpBingoEmitter = g_dComIfG_gameInfo.play.getParticle()->setNormal(
            dPa_name::ID_AK_SN_SIRENKEY01, pos2, &angleTmp, NULL, 0xff, NULL, -1, &prm1[idx],
            &env1[idx], NULL);

        if (i_playSe) {
            s8 reverb = dComIfGp_getReverb(current.roomNo);
            JAIZelBasic::zel_basic->seStart(JA_SE_OBJ_KEY_STATUE_SET, &eyePos, 0, reverb, 1.0f,
                                             1.0f, -1.0f, -1.0f, 0);
        }

        mBingoEff = true;
    } else {
        s8 reverb = dComIfGp_getReverb(current.roomNo);
        JAIZelBasic::zel_basic->seStart(JA_SE_OBJ_KEY_ST_LIGHT, &eyePos, 0, reverb, 1.0f, 1.0f,
                                         -1.0f, -1.0f, 0);
    }
}

/* 00002718-00002790       .text eff_clr_bingo__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::eff_clr_bingo() {
    if (mBingoEff) {
        mFollowCb.remove();
        if (mpBingoEmitter != NULL) {
            mpBingoEmitter->becomeInvalidEmitter();
            mpBingoEmitter = NULL;
        }
        mBingoEff = false;
    }
}

/* 00002790-000027BC       .text eff_land_smoke__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::eff_land_smoke() {
    daObj::make_land_effect(this, &mAcch.m_gnd, 1.0f);
}

/* 000027BC-0000280C       .text eff_hit_water_splash__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::eff_hit_water_splash() {
    cXyz pos(current.pos.x, mAcch.m_wtr.GetHeight(), current.pos.z);
    fopKyM_createWpillar(&pos, 1.0f, 0.75f, 0);
}

/* 0000280C-00002868       .text make_vib__Q28daObjTry5Act_cFv */
void daObjTry::Act_c::make_vib() {
    int n = check_circle() & 1;
    dComIfGp_getVibration().StartShock(n + 1, 1, cXyz(0.0f, 1.0f, 0.0f));
}

/* 00002868-00002960       .text check_circle__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::check_circle() {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    cXyz diff = player->current.pos - current.pos;
    f32 dist = diff.absXZ();
    bool ret = false;
    if (dist <= 190.0f) {
        ret = true;
    }
    return ret;
}

/* 00002960-00002A90       .text get_water_h__Q28daObjTry5Act_cFv */
f32 daObjTry::Act_c::get_water_h() {
    dBgS_WtrChk water_chk;
    f32 water_h = current.pos.y;

    if (dBgS_SplGrpChk_In_ObjGnd(current.pos, &water_chk, 100.0f) != false) {
        water_h = water_chk.GetHeight();
    }

    return water_h;
}

/* 00002BB4-00002DA8       .text _execute__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::_execute() {
    cull_set_move();

    if (mForceExec || mMode != MODE_WAIT || !mAcch.ChkGroundHit() || mAcch.ChkGroundLanding() ||
        attr().mNoCull || !fopAcM_cullingCheck(this))
    {
        mForceExec = false;
        BOOL del = TRUE;
        if (!damage_cc_proc() && !damage_bg_proc()) {
            if (mDropTimer != 0) {
                mDropTimer--;
            }
            field_0x64e = false;
            if (mode_proc_call()) {
                del = FALSE;
                set_mtx();
                mStts.SetRoomId(current.roomNo);
                mCyl.MoveCAtTg(current.pos);
                dComIfG_Ccsp()->Set(&mCyl);
                if (mMode == MODE_DROP || mMode == MODE_SINK || mDropTimer != 0) {
                    dComIfG_Ccsp()->SetMass(&mCyl, 3);
                }
                attention_info.position.x = current.pos.x;
                attention_info.position.y = current.pos.y + attr().mAttnH;
                attention_info.position.z = current.pos.z;
                eyePos = attention_info.position;
                mPrevMode = mMode;
            }
        }
        if (del) {
            fopAcM_delete(this);
        }
    }

    if (!attr().mStayCorrect) {
        mStayCorrect = false;
    }
    mCorrect = false;
    cull_set_draw();
    return true;
}

/* 00002DA8-00002EA4       .text _draw__Q28daObjTry5Act_cFv */
bool daObjTry::Act_c::_draw() {
    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(mpModel, &tevStr);
    if (attr().mBrkIdx >= 0) {
        mBrk.entry(mpModel->getModelData());
    }
    mDoExt_modelUpdateDL(mpModel);
    if (!fopAcM_checkCarryNow(this)) {
        dComIfGd_setSimpleShadow2(&current.pos, mAcch.GetGroundH(), attr().mShadowSize, mAcch.m_gnd);
    }
    return true;
}

namespace daObjTry {
namespace {
/* 00002EA4-00002EC4       .text Mthd_Create__Q28daObjTry25@unnamed@d_a_obj_try_cpp@FPv */
cPhs_State Mthd_Create(void* i_this) {
    return ((daObjTry::Act_c*)i_this)->_create();
}

/* 00002EC4-00002EE8       .text Mthd_Delete__Q28daObjTry25@unnamed@d_a_obj_try_cpp@FPv */
BOOL Mthd_Delete(void* i_this) {
    return ((daObjTry::Act_c*)i_this)->_delete();
}

/* 00002EE8-00002F0C       .text Mthd_Execute__Q28daObjTry25@unnamed@d_a_obj_try_cpp@FPv */
BOOL Mthd_Execute(void* i_this) {
    return ((daObjTry::Act_c*)i_this)->_execute();
}

/* 00002F0C-00002F30       .text Mthd_Draw__Q28daObjTry25@unnamed@d_a_obj_try_cpp@FPv */
BOOL Mthd_Draw(void* i_this) {
    return ((daObjTry::Act_c*)i_this)->_draw();
}

/* 00002F30-00002F38       .text Mthd_IsDelete__Q28daObjTry25@unnamed@d_a_obj_try_cpp@FPv */
BOOL Mthd_IsDelete(void*) {
    return TRUE;
}

static actor_method_class Mthd_Table = {
    (process_method_func)Mthd_Create,
    (process_method_func)Mthd_Delete,
    (process_method_func)Mthd_Execute,
    (process_method_func)Mthd_IsDelete,
    (process_method_func)Mthd_Draw,
};
}; // namespace
}; // namespace daObjTry

actor_process_profile_definition g_profile_Obj_Try = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0008,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_Obj_Try_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daObjTry::Act_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_Obj_Try_e,
    /* Actor SubMtd */ &daObjTry::Mthd_Table,
    /* Status       */ 0x04 | fopAcStts_SHOWMAP_e | fopAcStts_CULL_e | fopAcStts_FREEZE_e | fopAcStts_UNK4000_e | fopAcStts_UNK40000_e | fopAcStts_UNK8000000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLSPHERE_CUSTOM_e,
};
