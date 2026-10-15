/**
 * d_a_klft.cpp
 * Object - Forbidden Woods - Lift (moving wooden platform)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_klft.h"
#include "m_Do/m_Do_controller_pad.h"
#include "m_Do/m_Do_ext.h"
#include "d/d_cc_d.h"
#include "d/d_kankyo_wether.h"
#include "d/d_bg_s_movebg_actor.h"
#include "res/Object/Klft.h"

/* 00000078-00000374       .text ride_call_back__FP4dBgWP10fopAc_ac_cP10fopAc_ac_c */
static void ride_call_back(dBgW*, fopAc_ac_c* arg1, fopAc_ac_c* arg2) {
    klft_class* i_this = (klft_class*)arg1;

    mDoMtx_YrotS(*calc_mtx, -i_this->current.angle.y);

    cXyz sp_src = arg2->current.pos - i_this->m3A0;
    cXyz sp_dst1;
    MtxPosition(&sp_src, &sp_dst1);

    sp_src = arg2->old.pos - i_this->m3A0;
    cXyz sp_dst2;
    MtxPosition(&sp_src, &sp_dst2);

    if (fopAcM_GetName(arg2) == fpcNm_PLAYER_e) {
        i_this->m9EC = 10;
    }

    i_this->m3BC = -50.0f;

    f32 dist = std::sqrtf(sp_dst1.x * sp_dst1.x + sp_dst1.z * sp_dst1.z);

    cLib_addCalcAngleS2(&i_this->m3AE, (s16)(dist * ((30.0f + REG0_F(0)) / i_this->scale.z)), 10, 0x800);
    s16 angle = cM_atan2s(sp_dst1.x, sp_dst1.z);
    cLib_addCalcAngleS2(&i_this->m3AC, angle, 2, 0x2000);

    f32 stickX = g_mDoCPd_cpadInfo[0].mMainStickPosX;
    f32 stickY = g_mDoCPd_cpadInfo[0].mMainStickPosY;
    if (std::fabsf(stickX) + std::fabsf(stickY) > 0.1f ||
        std::fabsf(i_this->m3C4) > 0.0001f) {
        f32 val;

        val = (100.0f + REG0_F(4)) * std::fabsf(sp_dst1.z - sp_dst2.z);
        if (val > 200.0f + REG0_F(6)) {
            i_this->m2E8.x = val;
        }

        val = (100.0f + REG0_F(4)) * std::fabsf(sp_dst1.x - sp_dst2.x);
        if (val > 200.0f + REG0_F(6)) {
            i_this->m2E8.z = val;
        }

        val = (2.0f + REG0_F(8)) * std::fabsf(sp_dst1.x - sp_dst2.x);
        if (val > 10.0f && i_this->m2D0.x < val) {
            i_this->m2DC.x = val;
        }

        val = (2.0f + REG0_F(8)) * std::fabsf(sp_dst1.z - sp_dst2.z);
        if (val > 10.0f && i_this->m2D0.z < val) {
            i_this->m2DC.z = val;
        }
    }
}

/* 000003B0-0000046C       .text nodeCallBack_main__FP7J3DNodei */
static BOOL nodeCallBack_main(J3DNode* node, int calcTiming) {
    if (calcTiming == J3DNodeCBCalcTiming_In) {
        J3DJoint* joint = (J3DJoint*)node;
        int jntNo = joint->getJntNo();
        J3DModel* model = j3dSys.getModel();
        klft_class* i_this = (klft_class*)model->getUserArea();
        if (i_this != NULL) {
            if (jntNo == 1) {
                MTXCopy(model->getAnmMtx(jntNo), *calc_mtx);
                mDoMtx_XrotM(*calc_mtx, i_this->m3B0);
                model->setAnmMtx(jntNo, *calc_mtx);
                MTXCopy(*calc_mtx, J3DSys::mCurrentMtx);
            }
        }
    }
    return TRUE;
}

/* 0000046C-00000520       .text nodeCallBack__FP7J3DNodei */
static BOOL nodeCallBack(J3DNode* node, int calcTiming) {
    if (calcTiming == J3DNodeCBCalcTiming_In) {
        J3DJoint* joint = (J3DJoint*)node;
        s32 jntNo = joint->getJntNo();
        J3DModel* model = j3dSys.getModel();
        klft_class* i_this = (klft_class*)model->getUserArea();

        if (i_this != NULL) {
            PSMTXCopy(model->getAnmMtx(jntNo), *calc_mtx);
            mDoMtx_YrotM(*calc_mtx, i_this->m2C0);
            PSMTXCopy(*calc_mtx, model->getAnmMtx(jntNo));
            PSMTXCopy(*calc_mtx, J3DSys::mCurrentMtx);
        }
    }

    return TRUE;
}

/* 00000520-000005A0       .text himo_Draw__FP10klft_class */
static void himo_Draw(klft_class* i_this) {
    i_this->m340.update(0x14, (GXColor){ 150, 150, 150, 255 }, &i_this->tevStr);
    dComIfGd_set3DlineMat(&i_this->m340);
}

/* 000005A0-00000688       .text daKlft_Draw__FP10klft_class */
static BOOL daKlft_Draw(klft_class* i_this) {
    g_env_light.settingTevStruct(TEV_TYPE_BG0, &i_this->current.pos, &i_this->tevStr);
    g_env_light.setLightTevColorType(i_this->mpModel, &i_this->tevStr);

    dComIfGd_setListBG();
    mDoExt_modelUpdateDL(i_this->mpModel);
    dComIfGd_setList();

    himo_Draw(i_this);

    for (int i = 0; i < 2; i++) {
        g_env_light.setLightTevColorType(i_this->mpMorf[i]->getModel(), &i_this->tevStr);
        i_this->mpMorf[i]->updateDL();
    }

    return TRUE;
}

/* 00000688-00000F24       .text klft_move__FP10klft_class */
static void klft_move(klft_class* i_this) {
    fopAc_ac_c* a_this = i_this;
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    cXyz sp_src;
    cXyz sp_dst;
    s16 angle;
    f32 mul;
    f32 rate;
    f32 sw;
    f32 target;

    i_this->m298++;

    if (i_this->m9EC != 0) {
        angle = player->shape_angle.y;
        mul = 1.0f;
    } else {
        angle = player->shape_angle.y + 0x8000;
        mul = 0.5f;
    }

    rate = 0.01f;

    if (i_this->mSph.ChkTgHit() || i_this->mSph2[0].ChkTgHit() || i_this->mSph2[1].ChkTgHit()) {
        if (i_this->mSph.ChkTgHit()) {
            mDoMtx_YrotS(*calc_mtx, angle - i_this->current.angle.y);
            sp_src.z = (0.5f + REG0_F(13)) * mul;
        } else {
            if (i_this->mSph2[0].ChkTgHit()) {
                mDoMtx_YrotS(*calc_mtx, -0x8000);
            } else {
                mDoMtx_YrotS(*calc_mtx, 0);
            }

            sp_src.z = (0.85f + REG0_F(16)) * mul;
            rate = 0.1f;
        }

        sp_src.x = 0.0f;
        sp_src.y = 0.0f;
        MtxPosition(&sp_src, &sp_dst);
        i_this->m3C8 = sp_dst.z;

        sp_src.x = (500.0f + REG0_F(15)) * mul;
        sp_src.y = 0.0f;
        sp_src.z = 0.0f;
        MtxPosition(&sp_src, &i_this->m2E8);

        i_this->m3B2 = REG0_S(5) + 60;

        mDoMtx_YrotS(*calc_mtx, angle);
        sp_src.x = 0.0f;
        sp_src.y = 0.0f;
        sp_src.z = (20.0f + REG0_F(16)) * mul;
        MtxPosition(&sp_src, &i_this->m2DC);
    }

    if (i_this->m3B2 == 40) {
        fopAcM_seStartCurrent(a_this, JA_SE_OBJ_KASSHA_LIFT_SWING, 0);
    }

    cLib_addCalc2(&i_this->m3C4, i_this->m3C8, 1.0f, rate);
    cLib_addCalc0(&i_this->m3C8, 1.0f, 0.001f + REG0_F(14));

    i_this->m3C0 += i_this->m3C4;

    if (i_this->m3C0 > 80.0f) {
        i_this->m3C0 = 80.0f;
        i_this->m3C4 = 0.0f;
    } else if (i_this->m3C0 < 20.0f) {
        i_this->m3C0 = 20.0f;
        i_this->m3C4 = 0.0f;
    }

    i_this->m3B0 += (s16)(-2500.0f * i_this->m3C4);

    if (std::fabsf(i_this->m3C4) > 0.01f) {
        u32 vol = (500.0f + REG0_F(0)) * std::fabsf(i_this->m3C4);

        if (vol > 100) {
            vol = 100;
        }

        for (int i = 0; i < 2; i++) {
            mDoAud_seStart(JA_SE_OBJ_KM_WINDMILL, &i_this->m2A8[i], vol,
                           dComIfGp_getReverb(fopAcM_GetRoomNo(a_this)));
        }

        i_this->m3B4 += std::fabsf(i_this->m3C4);

        if (i_this->m3B4 > 3.0f + REG0_F(1)) {
            i_this->m3B4 -= 3.0f + REG0_F(1);
            fopAcM_seStartCurrent(a_this, JA_SE_OBJ_KASSHA_LIFT_MOVE, vol);
        }
    }

    for (int i = 0; i < 2; i++) {
        if (i_this->mCyl[i].ChkTgHit()) {
            def_se_set(a_this, i_this->mCyl[i].GetTgHitObj(), 11);
        }
    }

    cLib_addCalcAngleS2(&i_this->m3AE, 0, 10, 0x200);
    cLib_addCalcAngleS2(&i_this->current.angle.x, 0, 10, 0x200);
    cLib_addCalcAngleS2(&i_this->current.angle.z, 0, 10, 0x200);

    i_this->m300.x = i_this->m2F4.x * cM_ssin(i_this->m298 * 1500);
    i_this->m300.z = i_this->m2F4.z * cM_ssin(i_this->m298 * 1300);

    if (i_this->m3B2 != 0) {
        target = 10.0f;
    } else if (std::fabsf(i_this->m2E8.x) > 100.0f + REG0_F(2)) {
        target = 10.0f + REG0_F(4);
    } else {
        target = 2.0f + REG0_F(3);
    }

    cLib_addCalc2(&i_this->m2F4.x, i_this->m2E8.x, 1.0f, target);

    if (i_this->m3B2 != 0) {
        target = 40.0f;
    } else if (std::fabsf(i_this->m2E8.z) > 100.0f + REG0_F(2)) {
        target = 10.0f + REG0_F(4);
    } else {
        target = 2.0f + REG0_F(3);
    }

    cLib_addCalc2(&i_this->m2F4.z, i_this->m2E8.z, 1.0f, target);

    if (i_this->m3B2 == 0) {
        i_this->m2E8.x = i_this->m2E8.z = 0.0f;
    }

    sw = cM_fsin(3.14159274f * (0.01f * i_this->m3C0));
    i_this->m2C4.x = sw * (i_this->m2D0.x * cM_ssin(i_this->m298 * 750));
    i_this->m2C4.z = sw * (i_this->m2D0.z * cM_ssin(i_this->m298 * 900));
    i_this->m2C4.y = sw * ((1.0f + REG0_F(1)) *
                           ((i_this->m2D0.x + i_this->m2D0.z) * cM_ssin(i_this->m298 * 1500)));

    if (i_this->m3B2 != 0) {
        target = 10.0f;
    } else if (std::fabsf(i_this->m2DC.x) > 10.0f) {
        target = 1.2f;
    } else {
        target = 0.25f;
    }

    cLib_addCalc2(&i_this->m2D0.x, i_this->m2DC.x, 1.0f, target);

    if (i_this->m3B2 != 0) {
        target = 10.0f;
    } else if (std::fabsf(i_this->m2DC.z) > 10.0f) {
        target = 1.2f;
    } else {
        target = 0.25f;
    }

    cLib_addCalc2(&i_this->m2D0.z, i_this->m2DC.z, 1.0f, target);

    if (i_this->m3B2 == 0) {
        i_this->m2DC.x = 0.0f;
        i_this->m2DC.z = 0.0f;
    }

    i_this->shape_angle = i_this->current.angle + i_this->m300;
    i_this->home.pos = i_this->m37C[0] + i_this->m394 * i_this->m3C0 * 0.01f;
    i_this->home.pos.y += (-100.0f + REG0_F(2)) + sw * (-100.0f + i_this->m3B8 + REG0_F(0));

    cLib_addCalc2(&i_this->m3B8, i_this->m3BC, 0.1f, 5.0f);
    i_this->m3BC = 0.0f;
    i_this->current.pos = i_this->home.pos + i_this->m2C4;
}

/* 00000F24-00001140       .text himo_move__FP10klft_class */
static void himo_move(klft_class* i_this) {
    cXyz* pos = i_this->m340.getPos(0);
    u8* size = i_this->m340.getSize(0);

    cXyz vecA = i_this->current.pos - i_this->m37C[0];
    cXyz vecB = i_this->current.pos - i_this->m37C[1];
    vecA.y -= 25.0f;
    vecB.y -= 25.0f;

    for (int i = 0; i < 20; i++, pos++, size++) {
        f32 t;
        if (i < 10) {
            t = (f32)i / 9.0f;
            *pos = i_this->m37C[0] + vecA * t;
        } else {
            t = (f32)(19 - i) / 9.0f;
            *pos = i_this->m37C[1] + vecB * t;
        }

        pos->y += (-20.0f + REG0_F(7)) * cM_fsin(3.14159274f * t);
        *size = 8;
    }
}

static cXyz* wind_vec;
static s16 wy;
static f32* wp;

/* 00001140-00001444       .text daKlft_Execute__FP10klft_class */
static BOOL daKlft_Execute(klft_class* i_this) {
    if (i_this->m3B2 != 0) {
        i_this->m3B2--;
    }

    if (i_this->m9EC != 0) {
        i_this->m9EC--;
    }

    wind_vec = dKyw_get_wind_vec();
    wy = cM_atan2s(wind_vec->x, wind_vec->z);
    wp = dKyw_get_wind_power();

    klft_move(i_this);
    himo_move(i_this);

    MtxTrans(i_this->current.pos.x, i_this->current.pos.y, i_this->current.pos.z, 0);
    mDoMtx_YrotM(*calc_mtx, i_this->shape_angle.y);
    mDoMtx_YrotM(*calc_mtx, i_this->m3AC);
    mDoMtx_XrotM(*calc_mtx, i_this->m3AE);
    mDoMtx_YrotM(*calc_mtx, -i_this->m3AC);
    mDoMtx_XrotM(*calc_mtx, i_this->shape_angle.x);
    mDoMtx_ZrotM(*calc_mtx, i_this->shape_angle.z);
    i_this->mpModel->setBaseTRMtx(*calc_mtx);
    PSMTXCopy(*calc_mtx, i_this->m30C);

    MtxTrans(0.0f, REG0_F(7) - 400.0f, 0.0f, 1);

    cXyz pos;
    pos.x = 0.0f;
    pos.y = 0.0f;
    pos.z = 0.0f;
    MtxPosition(&pos, &i_this->m3A0);

    i_this->mStts.Move();
    i_this->mSph.SetC(i_this->m3A0);
    dComIfG_Ccsp()->Set(&i_this->mSph);

    for (int i = 0; i < 2; i++) {
        pos.x = i_this->m2A8[i].x;
        pos.y = i_this->m2A8[i].y;
        pos.z = i_this->m2A8[i].z;
        pos.y += 35.0f + REG0_F(11);

        i_this->mSph2[i].SetC(pos);
        dComIfG_Ccsp()->Set(&i_this->mSph2[i]);

        i_this->mCyl[i].SetC(pos);
        dComIfG_Ccsp()->Set(&i_this->mCyl[i]);
    }

    i_this->pm_bgw->Move();

    i_this->m2C0 += (s32)(i_this->m3C4 * (8000.0f + REG0_F(8)));

    for (int i = 0; i < 2; i++) {
        MtxTrans(i_this->m2A8[i].x, i_this->m2A8[i].y, i_this->m2A8[i].z, 0);
        i_this->mpMorf[i]->getModel()->setBaseTRMtx(*calc_mtx);
    }

    return TRUE;
}

/* 00001444-0000144C       .text daKlft_IsDelete__FP10klft_class */
static BOOL daKlft_IsDelete(klft_class*) {
    return TRUE;
}

/* 0000144C-00001520       .text daKlft_Delete__FP10klft_class */
static BOOL daKlft_Delete(klft_class* i_this) {
    dComIfG_resDeleteDemo(&i_this->mPhs, "Klft");

#if VERSION > VERSION_DEMO
    if (i_this->heap != NULL)
#endif
    {
        dComIfG_Bgsp()->Release(i_this->pm_bgw);
    }

    if (i_this->m2C3 != 0) {
        if (i_this->m3C0 >= 50.0f) {
            dComIfGs_onSwitch(i_this->m2C3, i_this->current.roomNo);
        } else {
            dComIfGs_offSwitch(i_this->m2C3, i_this->current.roomNo);
        }
    }

    mDoAud_seDeleteObject(&i_this->m2A8[0]);
    mDoAud_seDeleteObject(&i_this->m2A8[1]);

    return TRUE;
}

/* 00001520-000017C4       .text CallbackCreateHeap__FP10fopAc_ac_c */
static BOOL CallbackCreateHeap(fopAc_ac_c* a_this) {
    klft_class* actor = (klft_class*)a_this;

    J3DModelData* modelData = (J3DModelData*)dComIfG_getObjectRes("Klft", dRes_INDEX_KLFT_BDL_LIFT_00_e);
    actor->mpModel = mDoExt_J3DModel__create(modelData, 0, 0x11020203);
    if (actor->mpModel == NULL) {
        return FALSE;
    }

#if TARGET_PC
    actor->mpModel->setUserArea((uintptr_t)actor);
#else
    actor->mpModel->setUserArea((u32)actor);
#endif

    for (u16 i = 0; i < modelData->getJointNum(); i++) {
        if (i == 1) {
            modelData->getJointNodePointer(i)->setCallBack(nodeCallBack_main);
        }
    }

    actor->pm_bgw = new dBgW();
    JUT_ASSERT(0x343, actor->pm_bgw != NULL);

    actor->pm_bgw->Set((cBgD_t*)dComIfG_getObjectRes("Klft", dRes_INDEX_KLFT_DZB_LIFT_00_e), dBgW::MOVE_BG_e,
                      &actor->m30C);
    actor->pm_bgw->SetCrrFunc(dBgS_MoveBGProc_Typical);
    actor->pm_bgw->SetRideCallback(ride_call_back);

    if (!actor->m340.init(1, 0x14, (ResTIMG*)dComIfG_getObjectRes("Klft", dRes_INDEX_KLFT_BTI_K_ROPE_01_e), 1)) {
        return FALSE;
    }

    for (int j = 0; j < 2; j++) {
        actor->mpMorf[j] = new mDoExt_McaMorf(
            (J3DModelData*)dComIfG_getObjectRes("Klft", dRes_INDEX_KLFT_BDL_VPBOT_00_e),
            NULL, NULL, NULL, 2, 1.0f, 0, -1, 0, NULL, 0, 0x11020203
        );

        J3DModel* model = actor->mpMorf[j]->getModel();
#if TARGET_PC
        model->setUserArea((uintptr_t)actor);
#else
        model->setUserArea((u32)actor);
#endif

        for (u16 k = 0; k < model->getModelData()->getJointNum(); k++) {
            if (k == 3) {
                model->getModelData()->getJointNodePointer(k)->setCallBack(nodeCallBack);
            }
        }
    }

    return TRUE;
}

#include "d/d_path.h"

/* 000017C4-00001C78       .text daKlft_Create__FP10fopAc_ac_c */
static cPhs_State daKlft_Create(fopAc_ac_c* a_this) {
    static dCcD_SrcSph utiwa_sph_src = {
        // dCcD_SrcGObjInf
        {
            /* Flags             */ 0,
            /* SrcObjAt  Type    */ 0,
            /* SrcObjAt  Atp     */ 0,
            /* SrcObjAt  SPrm    */ cCcD_AtSPrm_Set_e | cCcD_AtSPrm_VsPlayer_e,
            /* SrcObjTg  Type    */ AT_TYPE_WIND,
            /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsEnemy_e,
            /* SrcObjCo  SPrm    */ 0,
            /* SrcGObjAt Se      */ 0,
            /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
            /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
            /* SrcGObjAt Mtrl    */ 0,
            /* SrcGObjAt SPrm    */ 0,
            /* SrcGObjTg Se      */ 0,
            /* SrcGObjTg HitMark */ 0,
            /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
            /* SrcGObjTg Mtrl    */ 0,
            /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoConHit_e,
            /* SrcGObjCo SPrm    */ 0,
        },
        // cM3dGSphS
        {{
            /* Center */ {0.0f, 0.0f, 0.0f},
            /* Radius */ 250.0f,
        }},
    };
    static dCcD_SrcCyl p_co_cyl_src = {
        // dCcD_SrcGObjInf
        {
            /* Flags             */ 0,
            /* SrcObjAt  Type    */ 0,
            /* SrcObjAt  Atp     */ 0,
            /* SrcObjAt  SPrm    */ 0,
            /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_BOOMERANG & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT,
            /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_GrpAll_e,
            /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e | cCcD_CoSPrm_VsGrpAll_e,
            /* SrcGObjAt Se      */ 0,
            /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
            /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
            /* SrcGObjAt Mtrl    */ 0,
            /* SrcGObjAt SPrm    */ 0,
            /* SrcGObjTg Se      */ dCcG_SE_UNK6,
            /* SrcGObjTg HitMark */ 0,
            /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
            /* SrcGObjTg Mtrl    */ 0,
            /* SrcGObjTg SPrm    */ dCcG_TgSPrm_Shield_e,
            /* SrcGObjCo SPrm    */ 0,
        },
        // cM3dGCylS
        {{
            /* Center */ {0.0f, 0.0f, 0.0f},
            /* Radius */ 40.0f,
            /* Height */ 60.0f,
        }},
    };

    klft_class* i_this = (klft_class*)a_this;
    fopAcM_ct(a_this, klft_class);

    cPhs_State phase = dComIfG_resLoad(&i_this->mPhs, "Klft");

    if (phase == cPhs_COMPLEATE_e) {
        i_this->m2C2 = fopAcM_GetParam(i_this);
        u8 path_no = fopAcM_GetParamBit(fopAcM_GetParam(i_this), 0x10, 8);

        i_this->m2C3 = i_this->current.angle.z;
        i_this->current.angle.z = 0;

        if (i_this->m2C3 == 0xff) {
            i_this->m2C3 = 0;
        }

        if (i_this->m2C3 != 0) {
            if (dComIfGs_isSwitch(i_this->m2C3, fopAcM_GetRoomNo(a_this))) {
                i_this->m3C0 = 80.0f;
            } else {
                i_this->m3C0 = 20.0f;
            }
        }

        if (i_this->m2C2 == 0xff) {
            i_this->m2C2 = 0;
        }

        if (!fopAcM_entrySolidHeap(a_this, CallbackCreateHeap, 0x10000)) {
            return cPhs_ERROR_e;
        }

        if (i_this->pm_bgw != NULL && dComIfG_Bgsp()->Regist(i_this->pm_bgw, a_this)) {
            return cPhs_ERROR_e;
        }

        if (path_no == 0xff) {
            return cPhs_ERROR_e;
        }

        dPath* path = dPath_GetRoomPath(path_no, fopAcM_GetRoomNo(a_this));

        if (path != NULL) {
            dPnt* pnt = path->m_points;

            i_this->m37C[0] = pnt[0].m_position;
            i_this->m37C[1] = pnt[1].m_position;

            cXyz dir = i_this->m37C[1] - i_this->m37C[0];

            i_this->home.angle.y = cM_atan2s(dir.x, dir.z) + 0x8000;
            i_this->current.angle.y = cM_atan2s(dir.x, dir.z) + 0x8000;

            i_this->m394 = dir;

            i_this->m2A8[0] = pnt[2].m_position;
            i_this->m2A8[1] = pnt[3].m_position;
        } else {
            return cPhs_ERROR_e;
        }

        switch (i_this->m2C2) {
        case 1:
            i_this->scale.z = i_this->scale.x = 0.9f;
            break;
        case 2:
            i_this->scale.z = i_this->scale.x = 0.8f;
            break;
        case 3:
            i_this->scale.z = i_this->scale.x = 0.7f;
            break;
        default:
            i_this->scale.x = i_this->scale.z = 1.0f;
            break;
        }

        i_this->scale.y = 1.0f;
        i_this->mpModel->setBaseScale(i_this->scale);

        i_this->mStts.Init(0xff, 0xff, a_this);
        i_this->mSph.Set(utiwa_sph_src);
        i_this->mSph.SetStts(&i_this->mStts);

        for (int i = 0; i < 2; i++) {
            i_this->mSph2[i].Set(utiwa_sph_src);
            i_this->mSph2[i].SetStts(&i_this->mStts);
            i_this->mSph2[i].SetR(130.0f + REG0_F(11));
            i_this->mCyl[i].Set(p_co_cyl_src);
            i_this->mCyl[i].SetStts(&i_this->mStts);
        }

        daKlft_Execute(i_this);
    }

    return phase;
}

static actor_method_class l_daKlft_Method = {
    (process_method_func)daKlft_Create,
    (process_method_func)daKlft_Delete,
    (process_method_func)daKlft_Execute,
    (process_method_func)daKlft_IsDelete,
    (process_method_func)daKlft_Draw,
};

actor_process_profile_definition g_profile_KLFT = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0003,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_KLFT_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(klft_class),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_KLFT_e,
    /* Actor SubMtd */ &l_daKlft_Method,
    /* Status       */ fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
