/**
 * d_a_pw.cpp
 * Enemy - Poe
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_pw.h"
#include "d/actor/d_a_kantera.h"
#include "JSystem/JMath/JMATrigonometric.h"
#include "d/actor/d_a_player.h"
#include "d/d_bg_s_lin_chk.h"
#include "d/d_com_inf_game.h"
#include "res/Object/Pw.h"
#include "d/d_kankyo_rain.h"
#include "f_op/f_op_camera.h"
#include "f_pc/f_pc_manager.h"
#include "d/d_cc_d.h"
#include "d/d_s_play.h"
#if TARGET_PC
#include "pc/pc_qol.h"
#endif

enum daPW__BehaviorType {
    VisibleFromStart = 0x0,
    InvisibleAtStart = 0x1,
    OnlyLanternVisibleAtStart = 0x2,
    JalhallaChildA = 0x3,
    JalhallaChildB = 0x4,
    UseDefault = 0xFF
};

static bool TORITUKI_ON = false;

/* 00000078-00000158       .text nodeCallBack__FP7J3DNodei */
static BOOL nodeCallBack(J3DNode* node, int calcTiming) {
    if (calcTiming == J3DNodeCBCalcTiming_In) {
        J3DJoint* joint = (J3DJoint*)node;
        s32 jntNo = joint->getJntNo();
        J3DModel* model = j3dSys.getModel();
        pw_class* i_this = (pw_class*)model->getUserArea();
        if (i_this) {
            if (jntNo == 0x17) {
                MTXCopy(model->getAnmMtx(jntNo), *calc_mtx);
                i_this->m2E4 = i_this->m2CC;

                cXyz v;
                v.x = 0.0f;
                v.y = 0.0f;
                v.z = 0.0f;
                MtxPosition(&v, &i_this->m2CC);

                model->setAnmMtx(jntNo, *calc_mtx);
                cMtx_copy(*calc_mtx, J3DSys::mCurrentMtx);
            }
        }
    }
    return TRUE;
}

/* 00000158-00000230       .text draw_SUB__FP8pw_class */
static void draw_SUB(pw_class* i_this) {
    J3DModel* model = i_this->mpMorf->getModel();
    model->setBaseScale(i_this->scale);
    mDoMtx_stack_c::transS(i_this->current.pos.x, i_this->current.pos.y + i_this->m3A4, i_this->current.pos.z);
    mDoMtx_stack_c::YrotM(i_this->shape_angle.y);
    mDoMtx_stack_c::XrotM(i_this->shape_angle.x);
    mDoMtx_stack_c::ZrotM(i_this->shape_angle.z);
    model->setBaseTRMtx(mDoMtx_stack_c::get());

    i_this->mpMorf->calc();
    
    enemy_fire(&i_this->mEnemyFire);

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &i_this->current.pos, &i_this->tevStr);
}

#include "d/d_snap.h"
#include "d/d_material.h"

/* 00000230-000004D4       .text daPW_Draw__FP8pw_class */
static BOOL daPW_Draw(pw_class* i_this) {
    J3DModel* model = i_this->mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();

    g_env_light.setLightTevColorType(model, &i_this->tevStr);
    dSnap_RegistFig(DSNAP_TYPE_UNKB8, i_this, 1.0f, 1.0f, 1.0f);

    for (u16 i = 0; i < modelData->getMaterialNum(); i++) {
        modelData->getMaterialNodePointer(i)->getTevKColor(3)->mColor.a = i_this->m39A;
    }

    dComIfGd_setListMaskOff();

    if (i_this->mEnemyIce.mFreezeTimer > 20) {
        dMat_control_c::iceEntryDL(i_this->mpMorf, -1, &i_this->mInvisibleModel);
        dComIfGd_setList();
        return TRUE;
    }

    if (i_this->m33E) {
        i_this->m2C0->entry(model->getModelData());
    } else if (i_this->m33F) {
        i_this->m2C4->entry(model->getModelData());
    } else {
        i_this->m2C8->entry(model->getModelData());
    }

    i_this->m2BC->entry(model->getModelData());
    i_this->m2BC->setFrame(i_this->mColorIndex);

    i_this->mpMorf->entryDL();

    if (i_this->m33E) {
        i_this->m2C0->remove(model->getModelData());
    } else if (i_this->m33F) {
        i_this->m2C4->remove(model->getModelData());
    } else {
        i_this->m2C8->remove(model->getModelData());
    }
    i_this->m2BC->remove(model->getModelData());

    dComIfGd_setList();

    i_this->mInvisibleModel.entryMaskOff();

    if (i_this->mBehaviorType == 0 && i_this->mEnemyIce.mLightShrinkTimer == 0) {
        dComIfGd_setSimpleShadow2(&i_this->current.pos, i_this->mAcch.GetGroundH(), 50.0f, i_this->mAcch.m_gnd);
    }

    return TRUE;
}

/* 000004D4-00000600       .text anm_init__FP8pw_classifUcfi */
static void anm_init(pw_class* i_this, int bckFileIdx, f32 morf, u8 loopMode, f32 speed, int soundFileIdx) {
    i_this->mBckIdx = bckFileIdx;
    if (soundFileIdx >= 0) {
        void* soundAnm = dComIfG_getObjectRes("PW", soundFileIdx);
        J3DAnmTransform* bckAnm = (J3DAnmTransform*)dComIfG_getObjectRes("PW", bckFileIdx);
        i_this->mpMorf->setAnm(bckAnm, loopMode, morf, speed, 0.0f, -1.0f, soundAnm);
    } else {
        J3DAnmTransform* bckAnm = (J3DAnmTransform*)dComIfG_getObjectRes("PW", bckFileIdx);
        i_this->mpMorf->setAnm(bckAnm, loopMode, morf, speed, 0.0f, -1.0f, NULL);
    }
}


/* 00000600-000006F4       .text kantera_break__FP8pw_class */
static void kantera_break(pw_class* i_this) {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
    i_this->m346 = 0;
    if (i_this->mKanteraID != fpcM_ERROR_PROCESS_ID_e) {
        fopAc_ac_c* kantera;
        if (fopAcM_SearchByID(i_this->mKanteraID, &kantera) && kantera != NULL &&
            fopAcM_GetName(kantera) == fpcNm_KANTERA_e)
        {
            kantera_class* k = (kantera_class*)kantera;
            f32 dx = k->actor.current.pos.x - player->current.pos.x;
            f32 dz = k->actor.current.pos.z - player->current.pos.z;
            mDoMtx_YrotS(*calc_mtx, cM_atan2s(dx, dz));
            cXyz offset(0.0f, 0.0f, 20.0f);
            MtxPosition(&offset, &kantera->speed);
            kantera->speed.y = 10.0f;
            k->mState = 5;
            i_this->mKanteraID = fpcM_ERROR_PROCESS_ID_e;
        }
    }
}

#include "d/actor/d_a_bpw.h"

/* 000006F4-000007CC       .text Big_pow_gattai_check__FP8pw_class */
static BOOL Big_pow_gattai_check(pw_class* i_this) {
    if (i_this->mJalhallaID != fpcM_ERROR_PROCESS_ID_e) {
        fopAc_ac_c* jal;
        if (fopAcM_SearchByID(i_this->mJalhallaID, &jal) && jal != NULL && fopAcM_GetName(jal) == fpcNm_BPW_e) {
            bpw_class* bpw = (bpw_class*)jal;
            if (bpw->m3E9) {
                i_this->m340 = 0;
                i_this->mCyl.OffAtSetBit();
                i_this->mCyl.OffAtSetBit();
                i_this->mCyl.OffTgSetBit();
                i_this->mCyl.ClrTgHit();

                i_this->m38C = fopAcM_searchActorAngleY(i_this, jal);
                i_this->mAction = 5;
                i_this->mMode = 0xAA;
                return TRUE;
            }
        }
    }

    return FALSE;
}

/* 000007CC-000008B0       .text Big_pow_down_check__FP8pw_class */
static BOOL Big_pow_down_check(pw_class* i_this) {
#if TARGET_PC
    // QoL "Arreglar fallos del juego original" (BetterWW fix_vanilla_bugs.asm, poe_fix_light_arrows_bug):
    // a Poe dying to a Light Arrow (its health is not 0) also counts for Jalhalla
    const bool fixBugs = pc_qol(PC_QOL_FIX_BUGS) != 0;
    if (i_this->health > 0 && !(fixBugs && i_this->mEnemyIce.mLightShrinkTimer > 0)) {
        return FALSE;
    }
#else
    if (i_this->health > 0) {
        return FALSE;
    }
#endif

    if (i_this->m345) {
        return FALSE;
    }

    if (i_this->mJalhallaID != fpcM_ERROR_PROCESS_ID_e) {
        fopAc_ac_c* jal;
        if (fopAcM_SearchByID(i_this->mJalhallaID, &jal) && jal != NULL && fopAcM_GetName(jal) == fpcNm_BPW_e) {
            bpw_class* bpw = (bpw_class*)jal;
            if (bpw->mMode == 0x6F && bpw->mSomeCountdownTimers[0] > 3) {
                jal->health--;
                if (jal->health <= 0) {
                    i_this->m344 = 1;
                }
                i_this->m345 = 1;
            } else {
                i_this->health = 4;
#if TARGET_PC
                // (the same fix) a Poe that rejoins Jalhalla stops dying to the Light Arrow
                if (fixBugs) {
                    i_this->mEnemyIce.mLightShrinkTimer = 0;
                }
#endif
                return TRUE;
            }
        }
    }

    return FALSE;
}

/* 000008B0-0000121C       .text body_atari_check__FP8pw_class */
static BOOL body_atari_check(pw_class* i_this) {
    daPy_py_c* player = daPy_getPlayerActorClass();

    i_this->mStts.Move();
    i_this->mHitByWhat = 0;

    if (i_this->health <= 0) {
        return FALSE;
    }

    if (i_this->mAction == 5) {
        return FALSE;
    }

    if (dComIfGp_getDetect().chk_light(&i_this->current.pos)) {
        i_this->mHitByWhat = 9;
        if (i_this->m39A != 255 && i_this->m382 == 0 && i_this->m346 != -1 &&
            i_this->mMode != 0x37 && i_this->mMode != 0x3C && i_this->mMode != 0x3D &&
            i_this->mJalhallaID == fpcM_ERROR_PROCESS_ID_e)
        {
            i_this->mAction = 2;
            i_this->mMode = 0x34;
            return TRUE;
        }
    }

    if (i_this->mCyl.ChkTgHit()) {
        cCcD_Obj* hitObj = i_this->mCyl.GetTgHitObj();
        if (hitObj == NULL) {
            return FALSE;
        }

        CcAtInfo atInfo;
        cXyz hitPos;

        atInfo.pParticlePos = NULL;
        hitPos = *i_this->mCyl.GetTgHitPosP();
        atInfo.mpObj = i_this->mCyl.GetTgHitObj();
        u8 noDamage = 0;

        if (i_this->m33E == 0) {
            if (hitObj->ChkAtType(AT_TYPE_LIGHT)) {
                i_this->mHitByWhat = 9;
            } else if (hitObj->ChkAtType(AT_TYPE_LIGHT_ARROW)) {
                i_this->mHitByWhat = 10;
                i_this->m39A = 255;
                i_this->mEnemyIce.mLightShrinkTimer = 1;
                i_this->mEnemyIce.mParticleScale = 1.0f;
                i_this->mEnemyIce.mYOffset = 80.0f;
                i_this->attention_info.flags = 0;
                Big_pow_down_check(i_this);
                kantera_break(i_this);
            } else {
                noDamage = 1;
            }
        } else {
            switch (hitObj->GetAtType()) {
            case AT_TYPE_GRAPPLING_HOOK:
                if (i_this->stealItemLeft > 0) {
                    s8 oldHealth = i_this->health;
                    i_this->health = 10;
                    atInfo.mpObj = i_this->mCyl.GetTgHitObj();
                    cc_at_check(i_this, &atInfo);
                    i_this->health = oldHealth;
                }
                dComIfGp_particle_set(dPa_name::ID_IT_JN_PIYOHIT00, &i_this->attention_info.position);
                noDamage = 1;
                i_this->mHitByWhat = 12;
                fopAcM_seStart(i_this, JA_SE_LK_MS_WEP_HIT, 0x20);
                break;
            case AT_TYPE_SWORD:
            case AT_TYPE_MACHETE:
            case AT_TYPE_UNK800:
            case AT_TYPE_DARKNUT_SWORD:
            case AT_TYPE_MOBLIN_SPEAR:
                fopAcM_seStart(i_this, JA_SE_LK_SW_HIT_S, 0x20);
                if (player->getCutType() == daPy_py_c::CUT_TYPE_CUT_EA ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_CUT_EB ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_CUT_TURN ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_CUT_ROLL ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_SWORD ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_STICK ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_MACHETE ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_BT_JUMPCUT ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_BT_ROLLCUT ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_BT_VERTICALJUMPCUT ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_CLUB ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_DN_SWORD ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_SPEAR ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_CUT_EXA ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_CUT_EXB ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_CUT_EXMJ ||
                    player->getCutType() == daPy_py_c::CUT_TYPE_CUT_KESA)
                {
                    i_this->mHitByWhat = 1;
                }
                break;
            case AT_TYPE_HOOKSHOT:
                noDamage = 1;
                fopAcM_seStart(i_this, JA_SE_LK_MS_WEP_HIT, 0x20);
                i_this->mHitByWhat = 11;
                i_this->mMode = 0x1B;
                break;
            case AT_TYPE_WIND:
                i_this->mHitByWhat = 3;
                i_this->mAction = 2;
                i_this->mMode = 0x38;
                return TRUE;
            case AT_TYPE_LIGHT:
                noDamage = 1;
                break;
            case AT_TYPE_BOOMERANG:
                noDamage = 1;
                i_this->mHitByWhat = 4;
                dComIfGp_particle_set(dPa_name::ID_IT_JN_PIYOHIT00, &i_this->attention_info.position);
                // Fall-through
            case AT_TYPE_BOKO_STICK:
            case AT_TYPE_STALFOS_MACE:
                fopAcM_seStart(i_this, JA_SE_LK_W_WEP_HIT, 0x20);
                break;
            case AT_TYPE_SKULL_HAMMER:
                fopAcM_seStart(i_this, JA_SE_LK_HAMMER_HIT, 0x20);
                i_this->mHitByWhat = 7;
                if (player->getCutType() == daPy_py_c::CUT_TYPE_HAMMER_SIDESWING) {
                    i_this->mHitByWhat = 8;
                }
                break;
            case AT_TYPE_BOMB:
                i_this->mHitByWhat = 6;
                break;
            case AT_TYPE_ICE_ARROW:
                noDamage = 1;
                i_this->mEnemyIce.mFreezeDuration = 200;
                enemy_fire_remove(&i_this->mEnemyFire);
                i_this->mHitByWhat = 5;
                fopAcM_seStart(i_this, JA_SE_LK_MS_WEP_HIT, 0x20);
                i_this->health = 0;
                i_this->attention_info.flags = 0;
                if (!Big_pow_down_check(i_this)) {
                    i_this->mAction = 2;
                    i_this->mMode = 0x3C;
                    return TRUE;
                }
                break;
            case AT_TYPE_LIGHT_ARROW:
                noDamage = 1;
                i_this->mEnemyIce.mLightShrinkTimer = 1;
                i_this->mEnemyIce.mParticleScale = 1.0f;
                i_this->mEnemyIce.mYOffset = 80.0f;
                i_this->attention_info.flags = 0;
                i_this->mHitByWhat = 5;
                fopAcM_seStart(i_this, JA_SE_LK_MS_WEP_HIT, 0x20);
                Big_pow_down_check(i_this);
                break;
            case AT_TYPE_FIRE:
            case AT_TYPE_FIRE_ARROW:
                i_this->mEnemyFire.mFireDuration = 100;
                // Fall-through
            case AT_TYPE_NORMAL_ARROW:
                i_this->mHitByWhat = 5;
                // Fall-through
            default:
                i_this->mHitByWhat = 0;
                fopAcM_seStart(i_this, JA_SE_LK_MS_WEP_HIT, 0x20);
                break;
            }
        }

        if (!noDamage) {
            if (i_this->m33E == 0) {
                if (i_this->mMode != 0x35) {
                    i_this->mAction = 2;
                    i_this->mMode = 0x34;
                }
                return TRUE;
            }

            cc_at_check(i_this, &atInfo);
            if (i_this->mHitByWhat == 1 || i_this->mHitByWhat == 7 || i_this->mHitByWhat == 8 ||
                i_this->health <= 0)
            {
                if (Big_pow_down_check(i_this)) {
                    return FALSE;
                }
                dComIfGp_particle_set(dPa_name::ID_AK_JN_CRITICALHITFLASH, &hitPos);
                cXyz scale(2.0f, 2.0f, 2.0f);
                dComIfGp_particle_set(dPa_name::ID_AK_JN_CRITICALHIT, &hitPos, &player->shape_angle,
                                      &scale);
                if (i_this->mHitByWhat == 7) {
                    i_this->mAction = 2;
                    i_this->mMode = 0x3E;
                    i_this->speedF = 0.0f;
                    i_this->gravity = -3.0f;
                    anm_init(i_this, dRes_INDEX_PW_BCK_PRESS1_e, 3.0f, J3DFrameCtrl::EMode_NONE,
                             1.0f, -1);
                    return TRUE;
                }
            } else {
                dComIfGp_particle_set(dPa_name::ID_AK_JN_OK, &hitPos, &player->shape_angle);
            }

            if (i_this->mMode != 0x37) {
                i_this->mAction = 2;
                i_this->mMode = 0x36;
            }
            return TRUE;
        } else {
            if (i_this->mHitByWhat == 4 || i_this->mHitByWhat == 12) {
                i_this->mAction = 2;
                i_this->mMode = 0x3A;
                return TRUE;
            }
        }
    }

    return FALSE;
}

/* 00001258-000016FC       .text kantera_atari_check__FP8pw_class */
static BOOL kantera_atari_check(pw_class* i_this) {
    daPy_py_c* player = daPy_getPlayerActorClass();

    if (i_this->m346 <= 0) {
        return FALSE;
    }

    if (i_this->mKanteraID != fpcM_ERROR_PROCESS_ID_e) {
        fopAc_ac_c* kantera;
        if (!fopAcM_SearchByID(i_this->mKanteraID, &kantera)) {
            return FALSE;
        }
        if (kantera == NULL) {
            return FALSE;
        }
        if (fopAcM_GetName(kantera) == fpcNm_KANTERA_e) {
            s16 oldHp = i_this->m398;
            i_this->mStts.Move();
            i_this->m33D = 0;

            if (i_this->mSph.ChkTgHit()) {
                if (i_this->m342) {
                    return FALSE;
                }
                cCcD_Obj* hitObj = i_this->mSph.GetTgHitObj();
                if (hitObj == NULL) {
                    return FALSE;
                }

                CcAtInfo atInfo;
                atInfo.pParticlePos = NULL;
                i_this->m342 = 1;

                switch (hitObj->GetAtType()) {
                case AT_TYPE_SWORD:
                    fopAcM_seStart(i_this, JA_SE_LK_SW_HIT_S, 0x40);
                    i_this->m398--;
                    if (player->getCutType() == daPy_py_c::CUT_TYPE_CUT_EA ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_CUT_EB ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_CUT_TURN ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_CUT_ROLL ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_SWORD ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_STICK ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_MACHETE ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_BT_ROLLCUT ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_BT_VERTICALJUMPCUT ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_CLUB ||
                        player->getCutType() == daPy_py_c::CUT_TYPE_JUMPCUT_DN_SWORD)
                    {
                        i_this->m33D = 1;
                        i_this->m398--;
                    }
                    break;
                case AT_TYPE_WIND:
                    i_this->m33D = 3;
                    break;
                case AT_TYPE_BOOMERANG:
                    i_this->m33D = 4;
                    // Fall-through
                case AT_TYPE_BOKO_STICK:
                    i_this->m398--;
                    fopAcM_seStart(i_this, JA_SE_LK_W_WEP_HIT, 0x40);
                    break;
                case AT_TYPE_SKULL_HAMMER:
                    fopAcM_seStart(i_this, JA_SE_LK_HAMMER_HIT, 0x40);
                    i_this->m33D = 7;
                    if (player->getCutType() == daPy_py_c::CUT_TYPE_HAMMER_SIDESWING) {
                        i_this->m33D = 8;
                    }
                    i_this->m398 = 0;
                    break;
                case AT_TYPE_BOMB:
                    i_this->m33D = 6;
                    i_this->m398 -= 2;
                    break;
                case AT_TYPE_FIRE_ARROW:
                case AT_TYPE_ICE_ARROW:
                case AT_TYPE_LIGHT_ARROW:
                    i_this->m398 -= 2;
                    // Fall-through
                case AT_TYPE_NORMAL_ARROW:
                    i_this->m33D = 5;
                    // Fall-through
                default:
                    i_this->m398--;
                    i_this->m33D = 0;
                    fopAcM_seStart(i_this, JA_SE_LK_MS_WEP_HIT, 0x40);
                    break;
                }
            } else {
                i_this->m342 = 0;
            }

            if (oldHp != i_this->m398) {
                if (i_this->m398 <= 0) {
                    cXyz hitPos = *i_this->mSph.GetTgHitPosP();
                    i_this->m398 = 0;
                    dComIfGp_particle_set(dPa_name::ID_AK_JN_CRITICALHITFLASH, &hitPos);
                    cXyz scale(1.0f, 1.0f, 1.0f);
                    dComIfGp_particle_set(dPa_name::ID_AK_JN_CRITICALHIT, &hitPos, &player->shape_angle, &scale);
                }
                i_this->mAction = 2;
                i_this->mMode = 0x32;
                return TRUE;
            }
        }
    }

    return FALSE;
}

/* 000016FC-000018FC       .text kantera_calc__FP8pw_class */
static void kantera_calc(pw_class* i_this) {
    if (i_this->m346 > 0 && i_this->m382 == 0 && i_this->mKanteraID != fpcM_ERROR_PROCESS_ID_e) {
        fopAc_ac_c* kantera;
        if (fopAcM_SearchByID(i_this->mKanteraID, &kantera) && kantera != NULL &&
            fopAcM_GetName(kantera) == fpcNm_KANTERA_e)
        {
            kantera_class* k = (kantera_class*)kantera;
            k->actor.current.pos = i_this->m2CC;
            kantera->current.angle.y = i_this->shape_angle.y;

            switch (i_this->m341) {
            case 0:
                cLib_addCalcAngleS2(&i_this->m396, 3000, 1, 100);
                cLib_addCalc2(&i_this->m3B0, 5000.0f, 1.0f, 1000.0f);
                cLib_addCalc2(&i_this->m3B8, -1000.0f, 1.0f, 1000.0f);
                break;
            case 1:
                cLib_addCalcAngleS2(&i_this->m396, 4000, 1, 100);
                cLib_addCalc2(&i_this->m3B0, 10000.0f, 1.0f, 1000.0f);
                cLib_addCalc2(&i_this->m3B8, -10000.0f, 1.0f, 1000.0f);
                break;
            case 2:
                break;
            }

            if (i_this->m341 != 2) {
                i_this->m394 += i_this->m396;
                k->mJointRot[1].x = i_this->m3B0 * JMASSin(i_this->m394);
                k->mJointRot[1].z = i_this->m3B8 * JMASCos(i_this->m394);
                kantera->attention_info.flags = 0;
            }

            i_this->m2D8 = k->mBonPos;
        }
    }
}

/* 000018FC-00001990       .text BG_check__FP8pw_class */
static void BG_check(pw_class* i_this) {
    i_this->mAcchCir.SetWall(70.0f, 140.0f);

    i_this->current.pos.y -= i_this->mCorrectionOffsetY;
    i_this->old.pos.y -= i_this->mCorrectionOffsetY;
    i_this->mAcch.CrrPos(*dComIfG_Bgsp());
    i_this->current.pos.y += i_this->mCorrectionOffsetY;
    i_this->old.pos.y += i_this->mCorrectionOffsetY;
}

/* 00001990-00001D60       .text Line_check__FP8pw_class4cXyzUc */
static BOOL Line_check(pw_class* i_this, cXyz destPos, u8 r22) {
    fopAc_ac_c* actor = i_this;
    dBgS_LinChk linChk;
    i_this->m314 = destPos;
    if (!r22) {
        cMtx_YrotS(*calc_mtx, i_this->current.angle.y);
        cXyz sp14(0.0f, 0.0f, 200.0f);
        MtxPosition(&sp14, &destPos);
        i_this->m314 = destPos + i_this->current.pos;
    }
    i_this->m314.y += 80.0f;
    destPos = i_this->m314;
    i_this->m308 = i_this->current.pos;
    i_this->m308.y += 80.0f;
    linChk.Set(&i_this->m308, &destPos, actor);
    if (!dComIfG_Bgsp()->LineCross(&linChk)) {
        return FALSE;
    }
    i_this->m38C = i_this->current.angle.y + 0x8000;
    return TRUE;
}

/* 00002198-00002254       .text alpha_anime__FP8pw_class */
static void alpha_anime(pw_class* i_this) {
    if (i_this->m33E != 0) {
        return;
    }

    i_this->m38E += 0x400;
    cLib_addCalcAngleS2(&i_this->m39C, 100, 1, 10);

    f32 s = JMASSin(i_this->m38E);
    i_this->m39A = i_this->m39C + 30.0f * s;
}

/* 00002254-00002400       .text fuwafuwa_calc__FP8pw_class */
static void fuwafuwa_calc(pw_class* i_this) {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
    f32 target = i_this->m2F0.y;

    i_this->m390 += 0x2bc;

    if (i_this->mAcch.GetGroundH() != -G_CM3D_F_INF && dComIfG_Bgsp()->ChkPolySafe(i_this->mAcch.m_gnd) &&
        dComIfG_Bgsp()->GetGroundCode(i_this->mAcch.m_gnd) == 4)
    {
        if (i_this->mAction == 1) {
            target = player->current.pos.y;
        }
        i_this->current.pos.y = 30.0f + target;
        return;
    }

    if (!i_this->mHoversAtInitialYPos) {
        target = i_this->mAcch.GetGroundH();
    }

    if (i_this->mMode == 0x21 || i_this->mMode == 0x28) {
        target = player->current.pos.y;
    }

    f32 sinY = JMASSin(i_this->m390);
    cLib_addCalc2(&i_this->current.pos.y, 30.0f + target + 30.0f * sinY, 1.0f, 3.0f);

    if (i_this->mMode == 0x26) {
        i_this->m392 += 0x3e8;
        f32 targetZ = 50.0f + i_this->m320.z + 50.0f * JMASSin(i_this->m392);
        cLib_addCalc2(&i_this->current.pos.x, 50.0f + i_this->m320.x + 50.0f * JMASSin(i_this->m392), 1.0f, 5.0f);
        cLib_addCalc2(&i_this->current.pos.z, targetZ, 1.0f, 5.0f);
    }
}

/* 00002400-00002560       .text kougen_hani_check__FP8pw_classUc */
static BOOL kougen_hani_check(pw_class* i_this, u8 i_type) {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    cXyz sp14;
    cXyz pos = i_this->current.pos;
    if (!i_type) {
        f32 dx = 0.33f * (i_this->current.pos.x - player->current.pos.x);
        f32 dz = 0.33f * (i_this->current.pos.z - player->current.pos.z);
        for (int i = 0; i < 3; i++) {
            pos.x += dx;
            pos.z += dz;
            if (dComIfGp_getDetect().chk_light(&pos)) {
                return TRUE;
            }
        }
    } else {
        cMtx_YrotS(*calc_mtx, i_this->current.angle.y);
        sp14.x = 0.0f;
        sp14.y = 0.0f;
        sp14.z = 200.0f;
        MtxPosition(&sp14, &pos);
        pos += i_this->current.pos;
        if (dComIfGp_getDetect().chk_light(&pos)) {
            return TRUE;
        }
    }
    return FALSE;
}

/* 00002560-00002714       .text kyori_sub__FP8pw_class */
static void kyori_sub(pw_class* i_this) {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    fopAc_ac_c* actor = i_this;
    if (fopAcM_searchActorDistance(actor, player) > 300.0f) {
        return;
    }

    cMtx_YrotS(*calc_mtx, fopAcM_searchPlayerAngleY(actor));

    cXyz vec;
    cXyz pos2;
    cXyz pos1;
    vec.x = 0.0f;
    vec.y = 0.0f;
    vec.z = -300.0f;
    MtxPosition(&vec, &pos1);
    PSVECAdd(&pos1, &player->current.pos, &pos1);
    if (dComIfGp_getDetect().chk_light(&pos1)) {
        return;
    }

    vec.x = 0.0f;
    vec.y = 0.0f;
    vec.z = -400.0f;
    MtxPosition(&vec, &pos2);
    PSVECAdd(&pos2, &player->current.pos, &pos2);
    if (dComIfGp_getDetect().chk_light(&pos2)) {
        return;
    }

    f32 speedAbs = std::fabsf(player->speedF);
    u32 volume = (u32)(4.0f * speedAbs);
    if (volume > 100) {
        volume = 100;
    }

    fopAcM_seStart(actor, JA_SE_CHR_PW_MOVE, volume);

    speedAbs *= 1.5f;
    cLib_addCalc2(&actor->current.pos.x, pos1.x, 1.0f, speedAbs);
    cLib_addCalc2(&actor->current.pos.z, pos1.z, 1.0f, speedAbs);
}

/* 00002714-0000289C       .text hani_check__FP8pw_class */
static BOOL hani_check(pw_class* i_this) {
    if (i_this->mJalhallaID != fpcM_ERROR_PROCESS_ID_e) {
        return FALSE;
    }

    f32 dx, dz, dist_threshold;
    if (i_this->mPathIndex != 0xFF && i_this->mpPath != NULL) {
        dist_threshold = 500.0f;
        dx = i_this->m2FC.x - i_this->current.pos.x;
        dz = i_this->m2FC.z - i_this->current.pos.z;
    } else {
        dist_threshold = 1000.0f;
        dx = i_this->m2F0.x - i_this->current.pos.x;
        dz = i_this->m2F0.z - i_this->current.pos.z;
    }

    f32 dist = std::sqrtf(dx * dx + dz * dz);
    if (dist > dist_threshold) {
        i_this->m38C = cM_atan2s(dx, dz);
        return TRUE;
    }

    if (i_this->mMode != 0x5B && kougen_hani_check(i_this, 1)) {
        i_this->m38C = cM_atan2s(dx, dz);
        return TRUE;
    }

    return FALSE;
}

/* 0000289C-000029C8       .text next_dousa_check__FP8pw_class */
static void next_dousa_check(pw_class* a_this) {
    pw_class* i_this = (pw_class*)a_this;
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);

    i_this->mCyl.SetTgType(AT_TYPE_LIGHT_ARROW | AT_TYPE_LIGHT);
    i_this->mCyl.OffTgNoConHit();

    if (!Line_check(i_this, player->current.pos, 1) && !hani_check(i_this) &&
        fopAcM_searchPlayerDistance(a_this) < 500.0f &&
        std::fabsf(i_this->current.pos.y - player->current.pos.y) < 100.0f)
    {
        i_this->mAction = 1;
        i_this->mMode = 0x20;
    } else {
        i_this->m37C = cM_rndF(70.0f) + 70.0f;
        if (i_this->mPathIndex == 0xFF) {
            i_this->m2F0.x = i_this->current.pos.x;
            i_this->m2F0.z = i_this->current.pos.z;
        }
        i_this->mAction = 0;
        i_this->mMode = 0xD;
    }
}

/* 000029C8-00002A54       .text move_sound__FP8pw_class */
static void move_sound(pw_class* i_this) {
    fopAcM_seStart(i_this, JA_SE_CHR_PW_MOVE, 4.0f * i_this->speedF);
}

/* 00002A54-00002A90       .text first_mode_change__FP8pw_class */
static void first_mode_change(pw_class* i_this) {
    i_this->m340 = 0;
    i_this->m39A = 100;
    i_this->m39C = 100;
    i_this->attention_info.flags = fopAc_Attn_LOCKON_BATTLE_e;
    fopAcM_OnStatus(i_this, fopAcStts_SHOWMAP_e);
    i_this->m38C = i_this->shape_angle.y;
    i_this->current.angle.y = i_this->m38C;
}

/* 00002A90-00003B08       .text action_dousa__FP8pw_class */
static void action_dousa(pw_class* i_this) {
    /* Nonmatching */
    daPy_py_c* player = daPy_getPlayerActorClass();
    camera_class* camera = dComIfGp_getCamera(0);
    cXyz camfwd;
    switch (i_this->mMode) {
    case 0:
        anm_init(i_this, dRes_INDEX_PW_BCK_WAIT1_e, 7.0, J3DFrameCtrl::EMode_LOOP, 1.0, -1);
        i_this->m346 = 0;
        i_this->mMode += 1;
        break;
    case 1:
        if (fopAcM_searchPlayerDistance(i_this) < 500.0f) {
            anm_init(i_this, dRes_INDEX_PW_BCK_DERUA1_e, 3.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
            i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
            i_this->current.angle.y = i_this->m38C;
            i_this->shape_angle.y = i_this->m38C;
            i_this->m3A4 = -80.0f;
            i_this->m340 = 1;
            i_this->mMode += 1;
        }
        break;
    case 2:
        i_this->shape_angle.y += 0x1000;
        cLib_addCalc0(&i_this->m3A4, 1.0f, 3.0f);
        i_this->m39A += 7;
        if (i_this->m39A > 100) {
            i_this->m39A = 100;
        }
        if (i_this->mpMorf->checkFrame(25.0f)) {
            i_this->m3A4 = 0.0f;
            first_mode_change(i_this);
            i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
            JPABaseEmitter* particle = dComIfGp_particle_set(dPa_name::ID_AK_SN_POUGETLAMP00, &i_this->m2CC, &i_this->shape_angle);
            if (particle != NULL) {
                particle->setGlobalRTMatrix(i_this->mpMorf->getModel()->getAnmMtx(PW_JNT_J_PW_ITEM_R1_e));
            }
            i_this->attention_info.flags = 0;
            i_this->mMode = 0x0A;
        }
        break;
    case 9:
        i_this->m39A = 0;
        i_this->m39C = 0;
        i_this->mMode += 1;
        // Fall-through
    case 10:
        if (i_this->mBehaviorType == InvisibleAtStart) {
            i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
        }
        i_this->mKanteraID = fopAcM_create(fpcNm_KANTERA_e, 0xFF000001, &i_this->m2CC, i_this->current.roomNo);
        if (i_this->mKanteraID != fpcM_ERROR_PROCESS_ID_e) {
            i_this->m382 = 5;
            switch (i_this->mBehaviorType) {
            case InvisibleAtStart:
                i_this->mMode = 0xB;
                break;
            case OnlyLanternVisibleAtStart:
                i_this->m341 = 2;
                i_this->mMode = 8;
                break;
            default:
                i_this->m346 = 1;
                i_this->mMode = 0xD;
                break;
            }
        }
        break;
    case 11:
        dKyr_get_vectle_calc(&i_this->current.pos, &camera->view.mLookat.mEye, &camfwd);
        i_this->mActorPlace.pos.x = i_this->m2CC.x + camfwd.x * 150.0f;
        i_this->mActorPlace.pos.y = i_this->m2CC.y;
        i_this->mActorPlace.pos.z = i_this->m2CC.z + camfwd.z * 150.0f;
        fopAc_ac_c* kantera;
        if (i_this->mKanteraID != fpcM_ERROR_PROCESS_ID_e &&
            fopAcM_SearchByID(i_this->mKanteraID, &kantera) && kantera != NULL &&
            fopAcM_GetName(kantera) == fpcNm_KANTERA_e)
        {
            kantera->current.pos = i_this->m2CC;
            kantera->current.angle.y = i_this->shape_angle.y;
            kantera->scale.setall(1.0f);
            if (i_this->mpMorf->isStop()) {
                i_this->m341 = 0;
                i_this->mBehaviorType = VisibleFromStart;
                first_mode_change(i_this);
                i_this->m382 = 3;
                i_this->m346 = 1;
                i_this->mMode = 0xD;
            }
        }
        break;
    case 6:
        i_this->m39A += 10;
        if (i_this->m39A > 100) {
            i_this->m39A = 100;
        }
        if (i_this->mpMorf->isStop()) {
            i_this->mBehaviorType = VisibleFromStart;
            i_this->m346 = 1;
            first_mode_change(i_this);
            i_this->mMode = 0xD;
        }
        break;
    case 7:
        if (fopAcM_searchPlayerDistance(i_this) < i_this->mNoticeRange) {
            i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
            i_this->current.angle.y = i_this->m38C;
            i_this->shape_angle.y = i_this->m38C;
            anm_init(i_this, dRes_INDEX_PW_BCK_DERUB2_e, 3.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
            i_this->mMode = 6;
        }
        break;
    case 8:
        i_this->m380 = 2;
        i_this->m346 = 1;
        if (fopAcM_searchPlayerDistance(i_this) < i_this->m3AC) {
            anm_init(i_this, dRes_INDEX_PW_BCK_DERUB1_e, 3.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            i_this->mMode = 7;
        }
        break;
    case 13:
        for (int i = 0; i < 4; i++) {
            i_this->m384[i] = 0;
        }
        i_this->m340 = 0;
        i_this->m341 = 0;
        i_this->m378 = cM_rndF(60.0f) + 60.0f;
        if (i_this->m346 == 1) {
            if (i_this->mBckIdx != dRes_INDEX_PW_BCK_WAIT1_e) {
                anm_init(i_this, dRes_INDEX_PW_BCK_WAIT1_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            }
        } else {
            if (i_this->mBckIdx != dRes_INDEX_PW_BCK_WAIT2_e) {
                anm_init(i_this, dRes_INDEX_PW_BCK_WAIT2_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            }
        }
        i_this->mMode += 1;
        // Fall-through
    case 14:
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        if (i_this->m378) {
            break;
        }
        i_this->mMode += 1;
        // Fall-through
    case 15:
        i_this->m378 = cM_rndF(120.0f) + 120.0f;
        if (i_this->m346 == 1) {
            if (i_this->mBckIdx != dRes_INDEX_PW_BCK_IDOU1_e) {
                anm_init(i_this, dRes_INDEX_PW_BCK_IDOU1_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            }
        } else {
            if (i_this->mBckIdx != dRes_INDEX_PW_BCK_IDOU2_e) {
                anm_init(i_this, dRes_INDEX_PW_BCK_IDOU2_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            }
        }
        i_this->m38C += (s16)cM_rndFX(0x4000);
        if (i_this->mPathIndex != 0xFF && i_this->mpPath != NULL) {
            i_this->mMode = 0x14;
        } else {
            i_this->mMode = 0x10;
        }
        break;
    case 16:
        i_this->speedF = 5.0f;
        move_sound(i_this);
        if (i_this->m37A == 0) {
            if (Line_check(i_this, i_this->current.pos, 0) || hani_check(i_this)) {
                i_this->m37A = 10;
            } else if (i_this->m378 == 0) {
                i_this->mMode = 0xF;
            }
        }
        break;
    case 20:
        i_this->m2FC = i_this->current.pos;
        if (i_this->mPathIndex != 0xFF && i_this->mpPath != NULL) {
            i_this->speedF = 5.0f;
            move_sound(i_this);
            dPnt* pnt = &i_this->mpPath->m_points[i_this->mPathPntIdx];
            f32 delta_x = pnt->m_position.x - i_this->current.pos.x;
            f32 delta_z = pnt->m_position.z - i_this->current.pos.z;
            i_this->m38C = cM_atan2s(delta_x, delta_z);
            f32 dist_xz = std::sqrtf(delta_x*delta_x + delta_z*delta_z);
            if (dist_xz < 80.0f) {
                i_this->mPathPntIdx++;
                if (i_this->mPathPntIdx >= i_this->mpPath->m_num) {
                    i_this->mPathPntIdx = 0;
                }
            }
        }
        break;
    case 25:
        i_this->m38C = fopAcM_searchActorAngleY(i_this, player);
        anm_init(i_this, dRes_INDEX_PW_BCK_DAMAGE_K1_e, 9.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        i_this->speedF = -20.0f;
        i_this->mMode += 1;
        // Fall-through
    case 26:
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        if (std::fabsf(i_this->speedF) < 0.2f) {
            i_this->mAction = 1;
            i_this->mMode = 0x25;
        }
        break;
    case 27:
        anm_init(i_this, dRes_INDEX_PW_BCK_DOKI1_e, 3.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
        i_this->speedF = 0.0f;
        i_this->speed.setall(0.0f);
        i_this->mMode += 1;
        // Fall-through
    case 28:
        if (!fopAcM_CheckStatus(i_this, fopAcStts_HOOK_CARRY_e)) {
            i_this->mMode = 90;
        }
        break;
    case 100:
        anm_init(i_this, dRes_INDEX_PW_BCK_JITTAIKA1_e, 6.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        i_this->mMode += 1;
        break;
    case 101:
        if (!i_this->mpMorf->isStop()) {
            break;
        }
        i_this->mMode = 90;
        // Fall-through
    case 90:
        i_this->speedF = 12.0f;
        i_this->m378 = 360;
        if (i_this->mJalhallaID != fpcM_ERROR_PROCESS_ID_e) {
            i_this->speedF = 9.0f + REG0_F(1);
        }
        if (i_this->mBckIdx != dRes_INDEX_PW_BCK_NIGERU1_e) {
            anm_init(i_this, dRes_INDEX_PW_BCK_NIGERU1_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, dRes_INDEX_PW_BAS_NIGERU1_e);
        }
        i_this->attention_info.flags = fopAc_Attn_LOCKON_BATTLE_e;
        i_this->m38C = fopAcM_searchPlayerAngleY(i_this) + 0x8000;
        i_this->m340 = 0;
        i_this->mMode += 1;
        // Fall-through
    case 91:
        if (i_this->mpMorf->checkFrame(0.0f)) {
            fopAcM_monsSeStart(i_this, JA_SE_CV_PW_RUN, 0);
        }
        if (i_this->m37E == 0) {
            i_this->m38C += (int)cM_rndFX(16384.0f);
            i_this->m37E = 15.0f + cM_rndF(15.0f);
        }
        if (i_this->m37A == 0) {
            if (Line_check(i_this, i_this->current.pos, 0) || hani_check(i_this)) {
                i_this->m37A = 20.0f + cM_rndF(20.0f);
                i_this->m37E = 20.0f + cM_rndF(20.0f);
            }
        }
        if (i_this->m378 == 0 && i_this->mJalhallaID == fpcM_ERROR_PROCESS_ID_e &&
            !dComIfGp_getDetect().chk_light(&i_this->current.pos))
        {
            i_this->mCyl.OffTgNoConHit();
            anm_init(i_this, dRes_INDEX_PW_BCK_ANSIN1_e, 3.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
            i_this->m33E = 0;
            i_this->mMode += 1;
            if (i_this->m33F != 0) {
                J3DModel* model = i_this->mpMorf->getModel();
                i_this->m33F = 0;
                i_this->m2C8->init(model->getModelData(),
                                   (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_DEFAULT_e),
                                   true, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, 0);
            }
        }
        Big_pow_gattai_check(i_this);
        break;
    case 92:
        fopAcM_seStart(i_this, JA_SE_CM_PW_BECOME_CLEAR, 0);
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        i_this->m39A -= 3;
        if (i_this->m39A < 100) {
            if (i_this->mpMorf->isStop()) {
                i_this->m38E = 0;
                i_this->m39C = i_this->m39A = 100;
                next_dousa_check(i_this);
            }
        }
        break;
    case 110: {
        i_this->mActorPlace.pos = i_this->current.pos;
        i_this->mActorPlace.angle = i_this->shape_angle;
        dComIfGp_particle_set(dPa_name::ID_AK_SN_BPWBUNRETUSMOKE01, &i_this->mActorPlace.pos,
                              &i_this->mActorPlace.angle, NULL, 0xFF, &i_this->m5C4);
        anm_init(i_this, dRes_INDEX_PW_BCK_ATTACK1_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
        i_this->speed.y = 45.0f + REG0_F(2);
        i_this->speed.y += cM_rndF(15.0f + REG0_F(3));
        i_this->gravity = -2.0f + REG0_F(4);
        i_this->speedF = 10.0f + REG0_F(5);
        i_this->speedF += cM_rndF(10.0f + REG0_F(6));
        fopAc_ac_c* jal;
        if (i_this->mJalhallaID != fpcM_ERROR_PROCESS_ID_e && fopAcM_SearchByID(i_this->mJalhallaID, &jal) &&
            jal != NULL && fopAcM_GetName(jal) == fpcNm_BPW_e && ((bpw_class*)jal)->m3F6 == 2)
        {
            i_this->gravity = -2.0f + REG0_F(4);
            i_this->speedF = 8.0f + REG0_F(5);
            i_this->speedF += cM_rndF(5.0f + REG0_F(6));
        }
        i_this->m340 = 1;
        i_this->mMode += 1;
        break;
    }
    case 111:
        if (i_this->m5C4.getEmitter() != NULL) {
            i_this->mActorPlace.pos = i_this->current.pos;
            i_this->mActorPlace.angle = i_this->shape_angle;
        }
        if (!i_this->mAcch.ChkGroundHit())
            break;
        i_this->m5C4.remove();
        i_this->speedF = 0.0f;
        i_this->speed.setall(0.0f);
        i_this->gravity = 0.0f;
        anm_init(i_this, dRes_INDEX_PW_BCK_SIRIMOTI1_e, 0.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        i_this->mMode += 1;
    case 112:
        // Fall-through
        if (i_this->mpMorf->isStop()) {
            i_this->mMode = 90;
        }
        break;
    }
    
    if (i_this->mMode <= 11) {
        return;
    }
    if (i_this->m37C == 0 && i_this->mMode < 90) {
        if (i_this->mMode == 14 || i_this->mMode == 16 || i_this->mMode == 20) {
            if (!hani_check(i_this) && fopAcM_searchPlayerDistance(i_this) < 500.0f && std::fabsf(i_this->current.pos.y - player->current.pos.y) < 100.0f) {
                if (!Line_check(i_this, player->current.pos, 1) && (i_this->m346 == 1 || !TORITUKI_ON)) {
                    i_this->mAction = 1;
                    i_this->mMode = 30;
                }
            }
        }
    }
    
    if (i_this->mMode >= 10 && i_this->mMode < 90) {
        alpha_anime(i_this);
    }
    
    if (i_this->mJalhallaID == fpcM_ERROR_PROCESS_ID_e) {
        fuwafuwa_calc(i_this);
    }
}

/* 00003B08-000042B8       .text action_kougeki__FP8pw_class */
static void action_kougeki(pw_class* i_this) {
    daPy_py_c* player = daPy_getPlayerActorClass();

    switch (i_this->mMode) {
    case 30:
        if (i_this->m346 == 1) {
            anm_init(i_this, dRes_INDEX_PW_BCK_KIDUKU1_e, 6.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        } else {
            anm_init(i_this, dRes_INDEX_PW_BCK_KIDUKU2_e, 6.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        }
        i_this->m340 = 0;
        i_this->mMode += 1;
        break;
    case 31:
        kyori_sub(i_this);
        i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
        cLib_addCalc0(&i_this->speedF, 1.0f, 0.3f);
        if (!i_this->mpMorf->isStop()) {
            break;
        }
        i_this->mMode += 1;
        // Fall-through
    case 32:
        kyori_sub(i_this);
        for (int i = 0; i < 4; i++) {
            i_this->m384[i] = 0;
        }
        i_this->m340 = 0;
        if (i_this->m346 == 1) {
            anm_init(i_this, dRes_INDEX_PW_BCK_ATTACK1_e, 6.0f, J3DFrameCtrl::EMode_NONE, 1.0f, dRes_INDEX_PW_BAS_ATTACK1_e);
            fopAcM_monsSeStart(i_this, JA_SE_CV_PW_ATTACK, 0);
            i_this->m341 = 1;
            i_this->mMode = 0x21;
        } else {
            anm_init(i_this, dRes_INDEX_PW_BCK_TORITUKI1_e, 3.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            i_this->m378 = 100;
            i_this->mMode = 0x28;
        }
        break;
    case 33:
        kyori_sub(i_this);
        cLib_addCalc0(&i_this->speedF, 1.0f, 0.3f);
        i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
        if (i_this->mpMorf->checkFrame(23.0f)) {
            i_this->speedF = 24.0f;
            i_this->mMode += 1;
        }
        break;
    case 34:
        move_sound(i_this);
        if (kougen_hani_check(i_this, 0)) {
            i_this->mMode = 0x25;
            break;
        }
        if (kougen_hani_check(i_this, 1)) {
            i_this->mMode = 0x25;
            break;
        }
        if (i_this->mpMorf->checkFrame(28.0f)) {
            i_this->mSph.OnAtSetBit();
            i_this->mSph.OnAtHitBit();
        }
        if (i_this->mpMorf->getFrame() > 28.0f && i_this->mSph.ChkAtShieldHit()) {
            i_this->mSph.OffAtSetBit();
            i_this->mSph.OffAtSetBit();
            i_this->mAction = 0;
            i_this->mMode = 0x19;
            return;
        } else if (i_this->mpMorf->checkFrame(40.0f)) {
            i_this->mSph.OffAtSetBit();
            i_this->mSph.OffAtSetBit();
            i_this->mMode += 1;
        }
        break;
    case 35:
        cLib_addCalc0(&i_this->speedF, 1.0f, 5.0f);
        if (std::fabsf(i_this->speedF) < 0.2f) {
            i_this->mMode = 0x25;
        }
        break;
    case 40:
        cLib_addCalc0(&i_this->speedF, 1.0f, 0.3f);
        i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
        if (i_this->m378 == 0) {
            i_this->m378 = 300;
            anm_init(i_this, dRes_INDEX_PW_BCK_TORITUKI2_e, 4.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            fopAcM_monsSeStart(i_this, JA_SE_CV_PW_GO_CURSE, 0);
            i_this->mMode += 1;
        }
        break;
    case 41:
        i_this->m38C = fopAcM_searchActorAngleY(i_this, player);
        if (kougen_hani_check(i_this, 0)) {
            i_this->mMode = 0x25;
            break;
        }
        if (kougen_hani_check(i_this, 1)) {
            i_this->mMode = 0x25;
            break;
        }
        cLib_addCalc2(&i_this->speedF, 15.0f, 1.0f, 3.0f);
        move_sound(i_this);
        if (i_this->m378 == 0) {
            anm_init(i_this, dRes_INDEX_PW_BCK_IKIGIRE1_e, 6.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
            i_this->mMode = 0x24;
            break;
        }
        if (Line_check(i_this, player->current.pos, 1)) {
            i_this->m37C = cM_rndF(70.0f) + 70.0f;
            if (i_this->mPathIndex == 0xFF) {
                i_this->m2F0.x = i_this->current.pos.x;
                i_this->m2F0.z = i_this->current.pos.z;
            }
            i_this->mAction = 0;
            i_this->mMode = 0xD;
        }
        break;
    case 36:
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        if (i_this->mpMorf->isStop()) {
            i_this->mMode = 0x25;
        }
        break;
    case 37:
        for (int i = 0; i < 4; i++) {
            i_this->m384[i] = 0;
        }
        i_this->speedF = 0.0f;
        if (i_this->m346 == 1) {
            if (i_this->mBckIdx != dRes_INDEX_PW_BCK_WAIT1_e) {
                anm_init(i_this, dRes_INDEX_PW_BCK_WAIT1_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            }
        } else {
            if (i_this->mBckIdx != dRes_INDEX_PW_BCK_WAIT2_e) {
                anm_init(i_this, dRes_INDEX_PW_BCK_WAIT2_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            }
        }
        i_this->m37C = cM_rndF(70.0f) + 70.0f;
        i_this->m320 = i_this->current.pos;
        i_this->m392 = 0;
        i_this->m340 = 0;
        i_this->m341 = 0;
        i_this->mMode += 1;
        // Fall-through
    case 38:
        kyori_sub(i_this);
        i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        if (i_this->m37C == 0) {
            i_this->speedF = 0.0f;
            next_dousa_check(i_this);
        }
        break;
    }

    if (i_this->m346 == 0) {
        if (TORITUKI_ON == 1) {
            i_this->m37C = cM_rndF(70.0f) + 70.0f;
            if (i_this->mPathIndex == 0xFF) {
                i_this->m2F0.x = i_this->current.pos.x;
                i_this->m2F0.z = i_this->current.pos.z;
            }
            i_this->mAction = 0;
            i_this->mMode = 0xD;
        } else if (i_this->mMode == 0x28 || i_this->mMode == 0x29 || i_this->mMode == 0x26) {
            if (i_this->mCyl.ChkAtHit()) {
                fopAc_ac_c* hitAc = i_this->mCyl.GetAtHitAc();
                if (hitAc != NULL && hitAc == player) {
                    i_this->speedF = 0.0f;
                    i_this->mCyl.OffAtSetBit();
                    i_this->mCyl.OffAtSetBit();
                    i_this->mCyl.SetTgType(AT_TYPE_LIGHT);
                    TORITUKI_ON = true;
                    i_this->m343 = 1;
                    i_this->mAction = 3;
                    i_this->mMode = 0x46;
                }
            }
        }
    }

    alpha_anime(i_this);
    fuwafuwa_calc(i_this);
}


/* 000042B8-00004C50       .text action_itai__FP8pw_class */
static void action_itai(pw_class* i_this) {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
    J3DModel* model = i_this->mpMorf->getModel();

    switch (i_this->mMode) {
    case 50:
        i_this->mBehaviorType = VisibleFromStart;
        i_this->m340 = 1;
        i_this->m341 = 1;
        i_this->attention_info.flags = 4;
        fopAcM_OnStatus(i_this, fopAcStts_SHOWMAP_e);
        i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
        i_this->current.angle.y = i_this->m38C;
        i_this->shape_angle.y = i_this->m38C;
        i_this->m38E = 0;
        i_this->m39A = 200;
        i_this->m39C = 200;
        i_this->m37E = 3;
        if (i_this->m398 == 0) {
            kantera_break(i_this);
        }
        i_this->speedF = -20.0f;
        anm_init(i_this, dRes_INDEX_PW_BCK_DAMAGE_K1_e, 3.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        i_this->mMode += 1;
        break;
    case 51:
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        if (i_this->mpMorf->isStop()) {
            i_this->speedF = 0.0f;
            next_dousa_check(i_this);
        }
        break;
    case 52:
        kantera_break(i_this);
        i_this->m340 = 1;
        i_this->m38C = i_this->current.angle.y;
        i_this->shape_angle.y = i_this->m38C;
        for (int i = 0; i < 4; i++) {
            i_this->m384[i] = 0;
        }
        if (i_this->mBckIdx != dRes_INDEX_PW_BCK_DOKI1_e) {
            anm_init(i_this, dRes_INDEX_PW_BCK_DOKI1_e, 3.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
        }
        i_this->m378 = 30;
        i_this->speedF = 0.0f;
        i_this->mMode += 1;
        // Fall-through
    case 53:
        if (i_this->mHitByWhat == 9 || i_this->mHitByWhat == 10) {
            i_this->m378 = 10;
            i_this->m39A += 5;
            if (i_this->m33F == 0) {
                i_this->m33F = 1;
                i_this->m2C4->init(model->getModelData(),
                                   (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_HIT_e),
                                   true, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, true, 0);
            }
            if (i_this->mHitByWhat == 10) {
                i_this->m39A = 255;
            }
            if (i_this->m39A >= 255) {
                i_this->m39A = 255;
                i_this->m38E = 0;
                mDoAud_seStart(JA_SE_CM_PW_SOLID_END, &i_this->eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(i_this)));
                fopAcM_monsSeStart(i_this, JA_SE_CV_PW_APPEAR, 0);
                i_this->mCyl.OnTgNoConHit();
                i_this->m33E = 1;
                i_this->m2C0->init(model->getModelData(),
                                   (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_JITTAIKA1_e),
                                   true, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, 0);
                i_this->mCyl.SetTgType(AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT);
                i_this->mAction = 0;
                i_this->mMode = 100;
                return;
            }
            fopAcM_seStart(i_this, JA_SE_CM_PW_BECOME_SOLID, 0);
        } else {
            if (i_this->m33F != 0) {
                i_this->m33F = 0;
                i_this->m2C8->init(model->getModelData(),
                                   (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_DEFAULT_e),
                                   true, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, 0);
            }
            i_this->m39C = 0;
            i_this->m39A = 0;
            i_this->m38E = 0;
            next_dousa_check(i_this);
        }
        break;
    case 54:
        i_this->m340 = 1;
        i_this->m380 = 0;
        i_this->m5C4.remove();
        i_this->m38C = i_this->shape_angle.y;
        i_this->current.angle.y = fopAcM_searchPlayerAngleY(i_this);
        i_this->shape_angle.y = i_this->m38C;
        i_this->speedF = -24.0f;
        if (i_this->health <= 0) {
            i_this->mCyl.OffAtSetBit();
            i_this->mCyl.OffAtSetBit();
            i_this->mCyl.OffTgSetBit();
            i_this->mCyl.ClrTgHit();
            i_this->speedF *= 1.5f;
        }
        fopAcM_monsSeStart(i_this, JA_SE_CV_PW_DAMAGE, 0);
        anm_init(i_this, dRes_INDEX_PW_BCK_DAMAGE1_e, 3.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        i_this->mMode += 1;
        break;
    case 55:
        if (Big_pow_gattai_check(i_this)) {
            break;
        }
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        if (i_this->mpMorf->isStop()) {
            i_this->speedF = 0.0f;
            if (i_this->health > 0) {
                i_this->mAction = 0;
                i_this->mMode = 90;
            } else {
                i_this->mMode = 60;
            }
        }
        break;
    case 56:
        i_this->current.angle.y = player->shape_angle.y;
        i_this->m340 = 1;
        i_this->speedF = 24.0f;
        i_this->mpMorf->setPlaySpeed(0.25f);
        i_this->mMode += 1;
        // Fall-through
    case 57:
        if (Big_pow_gattai_check(i_this)) {
            break;
        }
        cLib_addCalc0(&i_this->speedF, 1.0f, 1.0f);
        if (i_this->speedF < 0.2f) {
            i_this->speedF = 0.0f;
            i_this->current.angle.y = i_this->shape_angle.y;
            i_this->m340 = 0;
            i_this->mpMorf->setPlaySpeed(1.0f);
            i_this->mAction = 0;
            i_this->mMode = 90;
        }
        break;
    case 58:
        i_this->m340 = 1;
        i_this->speedF = 0.0f;
        if (i_this->mHitByWhat == 4) {
            enemy_piyo_set(i_this);
            fopAcM_seStart(i_this, JA_SE_CM_MD_PIYO, 0);
            i_this->m378 = 75 + REG0_S(7);
        }
        anm_init(i_this, dRes_INDEX_PW_BCK_KIZETU1_e, 3.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        i_this->mMode += 1;
        // Fall-through
    case 59:
        if (Big_pow_gattai_check(i_this)) {
            break;
        }
        if (i_this->m378 == 0) {
            i_this->mAction = 0;
            i_this->mMode = 90;
        }
        break;
    case 60:
        anm_init(i_this, dRes_INDEX_PW_BCK_SYOUTEN1_e, 2.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
        i_this->m33E = 0;
        i_this->attention_info.flags = 0;
        i_this->m5C4.remove();
        if (i_this->m384[3] == 0) {
            mDoAud_seStart(JA_SE_CM_PW_GO_HEAVEN, &i_this->eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(i_this)));
            fopAcM_monsSeStart(i_this, JA_SE_CV_PW_DIE, 0);
        }
        if (i_this->m33F != 0) {
            i_this->m33F = 0;
            i_this->m2C8->init(model->getModelData(),
                               (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_DEFAULT_e),
                               true, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, 0);
        }
        i_this->gravity = 0.6f;
        i_this->mMode += 1;
        // Fall-through
    case 61:
        i_this->m39A -= 8;
        if (i_this->m39A < 0) {
            i_this->m38E = 0;
            i_this->m39A = 0;
            if (i_this->m384[3] == 0) {
                cXyz pos = i_this->current.pos;
                pos.y += 60.0f;
                fopAcM_createDisappear(i_this, &pos, 5, 0, 0xFF);
            }
            fopAcM_delete(i_this);
            fopAcM_onActor(i_this);
        }
        break;
    case 62:
        if (i_this->mpMorf->isStop()) {
            i_this->mMode = 60;
        }
        break;
    }

    if (i_this->mMode <= 51) {
        if (i_this->m37E == 0) {
            alpha_anime(i_this);
        }
        fuwafuwa_calc(i_this);
    }
}

/* 00004C50-00004FFC       .text action_demo__FP8pw_class */
static void action_demo(pw_class* i_this) {
    daPy_py_c* player = daPy_getPlayerActorClass();
    camera_process_class* camera = dComIfGp_getCamera(dComIfGp_getPlayerCameraID(0));
    cXyz pos = player->current.pos;

    switch (i_this->mMode) {
    case 70:
        fopAcM_OnStatus(i_this, fopAcStts_UNK4000_e);
        i_this->mMode += 1;
        // Fall-through
    case 71:
        if (!i_this->eventInfo.checkCommandDemoAccrpt()) {
            dComIfGp_event_onEventFlag(1);
            fopAcM_orderPotentialEvent(i_this, dEvtFlag_STAFF_ALL_e, 0xFFFF, 0);
            i_this->eventInfo.onCondition(dEvtCnd_UNK2_e);
            break;
        }
        player->changeOriginalDemo();
        player->changeDemoMode(daPy_demo_c::DEMO_N_WAIT_e);
        camera->mCamera.Stop();
        camera->mCamera.SetTrimSize(2);
        i_this->m38C = fopAcM_searchActorAngleY(i_this, dComIfGp_getPlayer(0));
        fopAcM_monsSeStart(i_this, JA_SE_CV_PW_CURSE_IN, 0);
        mDoAud_seStart(JA_SE_CM_PW_CURSE_START, &i_this->eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(i_this)));
        anm_init(i_this, dRes_INDEX_PW_BCK_TORITUKI3_e, 4.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
        i_this->mMode += 1;
        break;
    case 72:
        cLib_addCalc2(&i_this->current.pos.x, player->current.pos.x, 1.0f, 5.0f);
        cLib_addCalc2(&i_this->current.pos.z, player->current.pos.z, 1.0f, 5.0f);
        if (i_this->mpMorf->isStop()) {
            anm_init(i_this, dRes_INDEX_PW_BCK_TORITUKI_WAIT1_e, 4.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            pos.y += 100.0f;
            i_this->m340 = 1;
            i_this->current.pos = pos;
            i_this->attention_info.flags = 0;
            i_this->shape_angle.setall(0);
            i_this->shape_angle.y = player->shape_angle.y;
            i_this->m378 = 30;
            i_this->mMode += 1;
        }
        break;
    case 73:
        if (i_this->m378 != 0) {
            break;
        }
        fopAc_ac_c* linkPlayer = dComIfGp_getLinkPlayer();
        dComIfGp_getVibration().StopQuake(0x20);
        camera->mCamera.Start();
        camera->mCamera.SetTrimSize(0);
        player->cancelOriginalDemo();
        dComIfGp_event_onEventFlag(8);
        fopAcM_OffStatus(i_this, fopAcStts_UNK4000_e);
        i_this->mAction = 4;
        i_this->mMode = 0x50;
        if (player == linkPlayer) {
            break;
        }
        if (i_this->m343 != 0) {
            i_this->m343 = 0;
            TORITUKI_ON = false;
            fopAcM_seStart(i_this, JA_SE_CM_PW_CURSE_END, 0);
        }
        i_this->m384[3] = 1;
        i_this->m38E = 0;
        i_this->m39A = 255;
        i_this->mAction = 2;
        i_this->mMode = 0x3C;
        break;
    }
}

/* 00004FFC-000052B8       .text action_torituku__FP8pw_class */
static void action_torituku(pw_class* i_this) {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
    fopAc_ac_c* partner = dComIfGp_getPlayer(1);
    cXyz pos = player->current.pos;
    u8 endCurse = 0;

    switch (i_this->mMode) {
    case 0x50:
        for (int i = 0; i < 4; i++) {
            i_this->m384[i] = 0;
        }
        i_this->m378 = 300;
        i_this->m33E = 1;
        i_this->actor_status |= fopAcStts_UNK4000_e;
        player->onConfuse();
        i_this->mMode += 1;
        // Fall-through
    case 0x51:
        JAIZelBasic::zel_basic->seStart(JA_SE_LK_NOW_CURSE_PW, &i_this->eyePos, 0,
                                         dComIfGp_getReverb(i_this->current.roomNo));
        i_this->m384[0] += 700;
        pos.y = 100.0f + player->current.pos.y + 10.0f * JMASSin(i_this->m384[0]);
        i_this->current.pos = pos;
        i_this->shape_angle.y += 1000;
        if (i_this->m378 == 0 || dComIfGp_getDetect().chk_light(&i_this->current.pos) ||
            dComIfGp_checkPlayerStatus1(0, daPyStts1_UNK2000_e) ||
            player->getDamageWaitTimer() != 0 || player != partner || player->checkFairyUse()) {
            endCurse = 1;
        }
        if (i_this->mCyl.ChkTgHit()) {
            cCcD_Obj* hitObj = i_this->mCyl.GetTgHitObj();
            if (hitObj != NULL && hitObj->ChkAtType(AT_TYPE_LIGHT)) {
                endCurse = 1;
            }
        }
        if (endCurse) {
            if (i_this->m343 != 0) {
                i_this->m343 = 0;
                TORITUKI_ON = false;
                JAIZelBasic::zel_basic->seStart(JA_SE_CM_PW_CURSE_END, &i_this->eyePos, 0,
                                                 dComIfGp_getReverb(i_this->current.roomNo));
            }
            i_this->m384[3] = 1;
            i_this->m38E = 0;
            i_this->m39A = 255;
            player->offConfuse();
            i_this->mAction = 2;
            i_this->mMode = 60;
        }
        break;
    }

    cLib_addCalcAngleS2(&i_this->m39A, 0xff, 1, 10);
}


/* 000052B8-00005CA4       .text action_big_demo__FP8pw_class */
static void action_big_demo(pw_class* i_this) {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    fopAc_ac_c* jal = fopAcM_SearchByID(i_this->mJalhallaID);
    cXyz player_pos = player->current.pos;

    if (!fopAcM_SearchByID(i_this->mJalhallaID, &jal) ||
        (jal != NULL && fopAcM_GetName(jal) == fpcNm_BPW_e)) {
        bpw_class* bpw = (bpw_class*)jal;

        switch (i_this->mMode) {
        case 150:
            for (int i = 0; i < 4; i++) {
                i_this->m384[i] = 0;
            }
            i_this->m39A = 255;
            i_this->m38E = 0;
            i_this->speedF = 17.0f + REG0_F(1);
            anm_init(i_this, dRes_INDEX_PW_BCK_NIGERU1_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, dRes_INDEX_PW_BAS_NIGERU1_e);
            i_this->mMode += 1;
            // Fall-through
        case 151:
            if (i_this->mpMorf->checkFrame(0.0f)) {
                fopAcM_monsSeStart(i_this, JA_SE_CV_PW_RUN, 0);
            }
            if (i_this->m37E == 0) {
                i_this->m38C += (int)cM_rndFX(16384.0f);
                i_this->m37E = 15.0f + cM_rndF(15.0f);
            }
            if (i_this->m37A == 0) {
                if (Line_check(i_this, i_this->current.pos, 0)) {
                    i_this->m37A = 20.0f + cM_rndF(20.0f);
                    i_this->m37E = 20.0f + cM_rndF(20.0f);
                } else {
                    f32 dx = i_this->m2F0.x - i_this->current.pos.x;
                    f32 dz = i_this->m2F0.z - i_this->current.pos.z;
                    f32 dist = std::sqrtf(dx * dx + dz * dz);
                    if (dist > 1000.0f + REG9_F(6)) {
                        i_this->m38C = cM_atan2s(dx, dz);
                        i_this->m37A = 20.0f + cM_rndF(20.0f);
                        i_this->m37E = 20.0f + cM_rndF(20.0f);
                    }
                }
            }
            if (bpw->m3EA != 0) {
                anm_init(i_this, dRes_INDEX_PW_BCK_WAIT2_e, 2.0f + cM_rndF(7.0f), J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
                i_this->mMode += 1;
            }
            break;
        case 152:
            cLib_addCalc0(&i_this->speedF, 1.0f, 2.0f);
            if (bpw->m3EA == 2) {
                i_this->m38C = fopAcM_searchPlayerAngleY(i_this);
                i_this->mMode += 1;
            }
            break;
        case 153:
            if (bpw->m3EA == 3) {
                anm_init(i_this, dRes_INDEX_PW_BCK_KIDUKU2_e, 2.0f + cM_rndF(7.0f), J3DFrameCtrl::EMode_NONE, 1.0f, -1);
                i_this->mMode += 1;
            }
            break;
        case 154:
            if (bpw->m3EA == 4) {
                anm_init(i_this, dRes_INDEX_PW_BCK_NIGERU1_e, 7.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, dRes_INDEX_PW_BAS_NIGERU1_e);
                i_this->mMode += 1;
            }
            break;
        case 155:
            if (i_this->mpMorf->checkFrame(0.0f)) {
                fopAcM_monsSeStart(i_this, JA_SE_CV_PW_RUN, 0);
            }
            i_this->speedF = 10.0f + REG9_F(7);
            i_this->m39A -= 3;
            if (i_this->m39A < 100) {
                i_this->m39A = 100;
            }
            if (bpw->m3EA == 4) {
                cLib_addCalcAngleS2(&i_this->m39E, 2000.0f + REG9_F(8), 1, 100.0f + REG9_F(9));
                i_this->speedF = 40.0f + REG9_F(10);
                i_this->m38C = fopAcM_searchActorAngleY(i_this, jal);
            } else if (bpw->m3EA == 5) {
                i_this->m39E = 1000.0f + REG9_F(11);
                i_this->m38C = fopAcM_searchActorAngleY(i_this, jal);
                i_this->m38C -= 0x8000;
                if (i_this->m384[0] == 0) {
                    i_this->m384[0] = cM_rndF(40.0f + REG9_F(13));
                }
            } else if (bpw->m3EA == 6) {
                if (i_this->m384[0] > 0) {
                    i_this->m384[0]--;
                } else {
                    anm_init(i_this, dRes_INDEX_PW_BCK_TORITUKI2_e, 1.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
                    fopAcM_monsSeStart(i_this, JA_SE_CV_PW_GO_CURSE, 0);
                    mDoAud_seStart(JA_SE_CM_PW_BECOME_CLEAR, &i_this->eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(i_this)));
                    i_this->m39E = 2000.0f + REG9_F(12);
                    i_this->m38C = fopAcM_searchActorAngleY(i_this, jal);
                    i_this->mMode = 171;
                }
            }
            break;
        case 170:
            anm_init(i_this, dRes_INDEX_PW_BCK_TORITUKI2_e, 4.0f, J3DFrameCtrl::EMode_LOOP, 1.0f, -1);
            fopAcM_monsSeStart(i_this, JA_SE_CV_PW_GO_CURSE, 0);
            mDoAud_seStart(JA_SE_CM_PW_BECOME_CLEAR, &i_this->eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(i_this)));
            i_this->m33E = 0;
            i_this->speedF = 20.0f + REG0_F(7);
            i_this->mMode += 1;
            // Fall-through
        case 171:
            i_this->m39A -= 3;
            if (i_this->m39A < 100) {
                i_this->m39A = 100;
            }
            if (jal != NULL) {
                i_this->m38C = fopAcM_searchActorAngleY(i_this, jal);
                f32 dx = jal->current.pos.x - i_this->current.pos.x;
                f32 dz = jal->current.pos.z - i_this->current.pos.z;
                f32 dist = std::sqrtf(dx * dx + dz * dz);
                if (dist < 200.0f + REG0_F(9)) {
                    i_this->speedF = 0.0f;
                    fopAcM_monsSeStart(i_this, JA_SE_CV_PW_CURSE_IN, 0);
                    mDoAud_seStart(JA_SE_CM_PW_CURSE_START, &i_this->eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(i_this)));
                    anm_init(i_this, dRes_INDEX_PW_BCK_TORITUKI3_e, 0.0f, J3DFrameCtrl::EMode_NONE, 1.0f, -1);
                    i_this->mMode += 1;
                }
            }
            break;
        case 172:
            if (bpw != NULL) {
                cLib_addCalc2(&i_this->current.pos.y, 200.0f + bpw->actor.current.pos.y + REG0_F(8), 1.0f, 10.0f);
                cLib_addCalc2(&i_this->current.pos.x, jal->current.pos.x, 1.0f, 10.0f);
                cLib_addCalc2(&i_this->current.pos.z, jal->current.pos.z, 1.0f, 10.0f);
            }
            if (i_this->mpMorf->isStop()) {
                bpw_class* target = (bpw_class*)jal;
                if (target != NULL) {
                    target->m464 = 1;
                    target->m462++;
                }
                fopAcM_delete(i_this);
            }
            break;
        }
    }
}

/* 00005CA4-000061FC       .text daPW_Execute__FP8pw_class */
static BOOL daPW_Execute(pw_class* i_this) {
    fopAcM_setGbaName(i_this, dItemNo_MIRROR_SHIELD_e, 0xD, 0x2D);

    if (enemy_ice(&i_this->mEnemyIce)) {
        i_this->mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::now);
        i_this->mpMorf->calc();
        return TRUE;
    }

    for (int i = 0; i < 6; i++) {
        if ((&i_this->m378)[i] != 0) {
            (&i_this->m378)[i]--;
        }
    }

    switch (i_this->mAction) {
    case 0:
        action_dousa(i_this);
        break;
    case 1:
        action_kougeki(i_this);
        break;
    case 2:
        action_itai(i_this);
        break;
    case 3:
        action_demo(i_this);
        break;
    case 4:
        action_torituku(i_this);
        break;
    case 5:
        action_big_demo(i_this);
        break;
    }

    if (!i_this->m340) {
        s16 step = 0x1000;
        if (i_this->mMode == 0xF || i_this->mMode == 0x10 || i_this->mMode == 0x14) {
            step = 500;
        }
        if (i_this->mAction == 5) {
            step = i_this->m39E;
        }
        cLib_addCalcAngleS2(&i_this->current.angle.y, i_this->m38C, 1, step);
        cLib_addCalcAngleS2(&i_this->shape_angle.y, i_this->current.angle.y, 1, step);
    }

    if (i_this->m33E) {
        i_this->m2C0->play();
    } else if (i_this->m33F) {
        i_this->m2C4->play();
    } else {
        i_this->m2C8->play();
    }

    mDoMtx_YrotS(*calc_mtx, i_this->current.angle.y);
    mDoMtx_XrotM(*calc_mtx, i_this->current.angle.x);
    cXyz vec;
    vec.x = 0.0f;
    vec.y = 0.0f;
    vec.z = i_this->speedF;
    cXyz out;
    MtxPosition(&vec, &out);
    i_this->speed.x = out.x;
    i_this->speed.z = out.z;
    i_this->speed.y += i_this->gravity;
    if (i_this->speed.y < -100.0f) {
        i_this->speed.y = -100.0f;
    }

    if (i_this->m346 == 1) {
        fopAc_ac_c* kantera;
        if (i_this->mKanteraID != fpcM_ERROR_PROCESS_ID_e && fopAcM_SearchByID(i_this->mKanteraID, &kantera) &&
            kantera != NULL && fopAcM_GetName(kantera) == fpcNm_KANTERA_e)
        {
            i_this->attention_info.position = i_this->m2D8;
            i_this->attention_info.position.y += 40.0f;
            i_this->eyePos = i_this->m2D8;
        }
    } else {
        i_this->attention_info.position = i_this->current.pos;
        i_this->attention_info.position.y += 200.0f;
        i_this->eyePos = i_this->current.pos;
        i_this->eyePos.y += 100.0f;
    }

    cXyz pos = i_this->current.pos;
    i_this->mCyl.SetC(pos);
    if (i_this->mAction == 4) {
        i_this->mCyl.SetH(100.0f);
        i_this->mCyl.SetR(40.0f + REG8_F(14));
    } else {
        i_this->mCyl.SetH(200.0f);
        i_this->mCyl.SetR(80.0f);
    }
    dComIfG_Ccsp()->Set(&i_this->mCyl);

    if (i_this->mAction != 3 && i_this->mAction != 5 && i_this->mAction != 4 && i_this->mMode != 0x3D &&
        i_this->mMode != 0x6E && i_this->mMode != 0x6F)
    {
        body_atari_check(i_this);
    }

    if (i_this->m346 == 1) {
        fopAcM_posMove(i_this, i_this->mStts.GetCCMoveP());
    } else {
        fopAcM_posMove(i_this, NULL);
    }

    if (i_this->mMode != 0x51 && i_this->mMode != 0x3D && i_this->mMode != 8) {
        BG_check(i_this);
    }

    if (i_this->mMode >= 2 && i_this->m380 == 0) {
        if (i_this->mAcch.GetGroundH() != -G_CM3D_F_INF) {
            s8 roomNo = fopAcM_GetRoomNo(i_this);
            u32 mtrlSndId = i_this->mAcch.ChkGroundHit() ? dComIfG_Bgsp()->GetMtrlSndId(i_this->mAcch.m_gnd) : 0;
            i_this->mpMorf->play(&i_this->eyePos, mtrlSndId, dComIfGp_getReverb(roomNo));
        } else {
            i_this->mpMorf->play(NULL, 0, 0);
        }
    }

    draw_SUB(i_this);

    if (i_this->m346 == 1) {
        kantera_calc(i_this);
        if (i_this->m382 != 0) {
            return TRUE;
        }
        kantera_atari_check(i_this);
        fopAc_ac_c* kantera;
        if (i_this->mKanteraID != fpcM_ERROR_PROCESS_ID_e && fopAcM_SearchByID(i_this->mKanteraID, &kantera) &&
            kantera != NULL && fopAcM_GetName(kantera) == fpcNm_KANTERA_e)
        {
            i_this->mSph.SetC(i_this->m2D8);
            i_this->mSph.SetR(40.0f);
            dComIfG_Ccsp()->Set(&i_this->mSph);
        }
    }

    return TRUE;
}

/* 000061FC-00006204       .text daPW_IsDelete__FP8pw_class */
static BOOL daPW_IsDelete(pw_class*) {
    return TRUE;
}

/* 00006204-000062B0       .text daPW_Delete__FP8pw_class */
static BOOL daPW_Delete(pw_class* i_this) {
    dComIfG_resDelete(&i_this->mPhase, "PW");
    if (i_this->heap != NULL) {
        i_this->mpMorf->stopZelAnime();
    }
    if (i_this->m343) {
        i_this->m343 = 0;
        TORITUKI_ON = false;
    }
    i_this->m5C4.remove();
    enemy_fire_remove(&i_this->mEnemyFire);
    if (i_this->mMode == 0x51) {
        daPy_getPlayerActorClass()->offConfuse();
    }
    return TRUE;
}

/* 000062B0-000066D8       .text useHeapInit__FP10fopAc_ac_c */
static BOOL useHeapInit(fopAc_ac_c* a_this) {
    pw_class* i_this = (pw_class*)a_this;

    i_this->mpMorf = new mDoExt_McaMorf(
        (J3DModelData*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BDL_PW_e),
        NULL,
        NULL,
        (J3DAnmTransformKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BCK_WAIT1_e),
        J3DFrameCtrl::EMode_LOOP,
        1.0f,
        0,
        -1,
        1,
        NULL,
        0x80000,
        0x37441422
    );
    if (i_this->mpMorf == NULL || i_this->mpMorf->getModel() == NULL) {
        return FALSE;
    }
    i_this->mpMorf->getModel()->setUserArea((uintptr_t)i_this);
    for (u16 i = 0; i < i_this->mpMorf->getModel()->getModelData()->getJointNum(); i++) {
        i_this->mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack);
    }
    J3DModel* model = i_this->mpMorf->getModel();

    i_this->m2BC = new mDoExt_btpAnm();
    if (i_this->m2BC == NULL) {
        return FALSE;
    }
    if (!i_this->m2BC->init(i_this->mpMorf->getModel()->getModelData(),
                            (J3DAnmTexPattern*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BTP_IROGAE1_e), TRUE,
                            J3DFrameCtrl::EMode_RESET))
    {
        return FALSE;
    }

    i_this->m2C0 = new mDoExt_brkAnm();
    if (i_this->m2C0 == NULL) {
        return FALSE;
    }
    if (!i_this->m2C0->init(model->getModelData(),
                            (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_JITTAIKA1_e), TRUE,
                            J3DFrameCtrl::EMode_NONE))
    {
        return FALSE;
    }

    i_this->m2C4 = new mDoExt_brkAnm();
    if (i_this->m2C4 == NULL) {
        return FALSE;
    }
    if (!i_this->m2C4->init(model->getModelData(),
                            (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_HIT_e), TRUE,
                            J3DFrameCtrl::EMode_LOOP))
    {
        return FALSE;
    }

    i_this->m2C8 = new mDoExt_brkAnm();
    if (i_this->m2C8 == NULL) {
        return FALSE;
    }
    if (!i_this->m2C8->init(model->getModelData(),
                            (J3DAnmTevRegKey*)dComIfG_getObjectRes("PW", dRes_INDEX_PW_BRK_DEFAULT_e), TRUE,
                            J3DFrameCtrl::EMode_LOOP))
    {
        return FALSE;
    }

    if (!i_this->mInvisibleModel.create(i_this->mpMorf->getModel())) {
        return FALSE;
    }
    return TRUE;
}

/* 00006720-00006BDC       .text daPW_Create__FP10fopAc_ac_c */
static cPhs_State daPW_Create(fopAc_ac_c* i_actor) {
    static dCcD_SrcCyl body_co_cyl_src = {
        // dCcD_SrcGObjInf
        {
            /* Flags             */ 0,
            /* SrcObjAt  Type    */ AT_TYPE_SPIKE,
            /* SrcObjAt  Atp     */ 0,
            /* SrcObjAt  SPrm    */ cCcD_AtSPrm_Set_e | cCcD_AtSPrm_VsPlayer_e | cCcD_AtSPrm_NoTgHitInfSet_e,
            /* SrcObjTg  Type    */ AT_TYPE_LIGHT_ARROW | AT_TYPE_LIGHT,
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
            /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoConHit_e | dCcG_TgSPrm_NoHitMark_e,
            /* SrcGObjCo SPrm    */ 0,
        },
        // cM3dGCylS
        {{
            /* Center */ {0.0f, 0.0f, 0.0f},
            /* Radius */ 15.0f,
            /* Height */ 0.0f,
        }},
    };
    static dCcD_SrcSph kantera_co_sph_src = {
        // dCcD_SrcGObjInf
        {
            /* Flags             */ 0,
            /* SrcObjAt  Type    */ AT_TYPE_SPIKE,
            /* SrcObjAt  Atp     */ 2,
            /* SrcObjAt  SPrm    */ cCcD_AtSPrm_Set_e | cCcD_AtSPrm_VsPlayer_e,
            /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT,
            /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_GrpAll_e,
            /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_VsGrpAll_e,
            /* SrcGObjAt Se      */ dCcG_SE_METAL,
            /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
            /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
            /* SrcGObjAt Mtrl    */ 0,
            /* SrcGObjAt SPrm    */ 0,
            /* SrcGObjTg Se      */ 0,
            /* SrcGObjTg HitMark */ dCcg_TgHitMark_Purple_e,
            /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
            /* SrcGObjTg Mtrl    */ 0,
            /* SrcGObjTg SPrm    */ dCcG_TgSPrm_Shield_e | dCcG_TgSPrm_NoConHit_e,
            /* SrcGObjCo SPrm    */ 0,
        },
        // cM3dGSphS
        {{
            /* Center */ {0.0f, 0.0f, 0.0f},
            /* Radius */ 15.0f,
        }},
    };

    pw_class* i_this = (pw_class*)i_actor;

    fopAcM_ct(i_actor, pw_class);

    cPhs_State phase = dComIfG_resLoad(&i_this->mPhase, "PW");

    if (phase == cPhs_COMPLEATE_e) {
        i_this->mBehaviorType = fopAcM_GetParam(i_this);
        i_this->mHoversAtInitialYPos = fopAcM_GetParamBit(fopAcM_GetParam(i_this), 8, 1);
        i_this->mNoticeRangeParam = fopAcM_GetParamBit(fopAcM_GetParam(i_this), 16, 8);
        i_this->mColorIndex = fopAcM_GetParamBit(fopAcM_GetParam(i_this), 9, 7);
        i_this->mPathIndex = fopAcM_GetParam(i_this) >> 24;

        if (i_this->mBehaviorType == UseDefault) {
            i_this->mBehaviorType = VisibleFromStart;
        }

        if (i_this->mNoticeRangeParam == 0xFF) {
            i_this->mNoticeRange = 1000.0f;
        } else {
            i_this->mNoticeRange = 10.0f * i_this->mNoticeRangeParam;
        }

        if (i_this->mColorIndex == 0x7F) {
            i_this->mColorIndex = 0;
        }

        if (!fopAcM_entrySolidHeap(i_actor, useHeapInit, 0x2540)) {
            return cPhs_ERROR_e;
        }

        i_this->gbaName = 13;
        i_this->m3AC = 5000.0f + i_this->mNoticeRange;

        if (REG8_S(9) != 0) {
            i_this->mBehaviorType = REG8_S(9) - 1;
        }

        if (i_this->mPathIndex != 0xFF) {
            i_this->mpPath = dPath_GetRoomPath(i_this->mPathIndex, fopAcM_GetRoomNo(i_this));
        }

        if (i_this->mColorIndex > 5) {
            i_this->mColorIndex = 0;
        }

        i_this->m2CC = i_this->current.pos;
        i_this->itemTableIdx = dComIfGp_CharTbl()->GetNameIndex("pow", 0);
        i_this->max_health = 4;
        i_this->health = 4;
        i_this->m398 = 4;
        i_this->m2F0 = i_this->current.pos;
        i_this->m39E = 0x1000;
        fopAcM_SetMtx(i_this, i_this->mpMorf->getModel()->getBaseTRMtx());
        fopAcM_setCullSizeBox(i_this, -100.0f, -50.0f, -50.0f, 100.0f, 200.0f, 100.0f);
        i_this->attention_info.flags = 4;
        i_this->mAcch.Set(&i_this->current.pos, &i_this->old.pos, i_this, 1, &i_this->mAcchCir, &i_this->speed, NULL, NULL);
        i_this->mStts.Init(0, 1, i_this);
        i_this->mEnemyIce.mpActor = i_this;
        i_this->mEnemyIce.mWallRadius = 50.0f;
        i_this->mEnemyIce.mCylHeight = 200.0f;
        i_this->mEnemyFire.mpMcaMorf = i_this->mpMorf;
        i_this->mEnemyFire.mpActor = i_this;

        static u8 fire_j[ARRAY_SIZE(i_this->mEnemyFire.mFlameJntIdxs)] = {
            PW_JNT_PW_SKLROOT_e,
            PW_JNT_J_PW_KOSI1_e,
            PW_JNT_J_PW_ASI_L1_e,
            PW_JNT_J_PW_ASI_R1_e,
            PW_JNT_J_PW_MUNE1_e,
            PW_JNT_J_PW_MUNE2_e,
            PW_JNT_J_PW_ATAMA1_e,
            PW_JNT_J_PW_KAMEN2_e,
            PW_JNT_J_PW_UDE_L2_e,
            PW_JNT_J_PW_UDE_R2_e
        };
        static f32 fire_sc[ARRAY_SIZE(i_this->mEnemyFire.mParticleScale)] = {
            1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f
        };

        for (int i = 0; i < ARRAY_SIZE(i_this->mEnemyFire.mFlameJntIdxs); i++) {
            i_this->mEnemyFire.mFlameJntIdxs[i] = fire_j[i];
            i_this->mEnemyFire.mParticleScale[i] = fire_sc[i];
        }

        i_this->mCyl.Set(body_co_cyl_src);
        i_this->mCyl.SetStts(&i_this->mStts);
        i_this->mSph.Set(kantera_co_sph_src);
        i_this->mSph.SetStts(&i_this->mStts);
        i_this->mSph.OffAtSetBit();
        i_this->mSph.OffAtSetBit();
        i_this->m38C = i_this->current.angle.y;
        i_this->stealItemLeft = 1;
        i_this->mAction = 0;
        i_this->mMode = 10;
        i_this->mJalhallaID = fpcM_ERROR_PROCESS_ID_e;
        i_this->mKanteraID = fpcM_ERROR_PROCESS_ID_e;
        i_this->mCyl.OffTgNoConHit();
        i_this->m2D8 = i_this->current.pos;
        i_this->m346 = -1;

        switch (i_this->mBehaviorType) {
        case InvisibleAtStart:
            i_this->attention_info.flags = 0;
            fopAcM_OffStatus(i_this, fopAcStts_SHOWMAP_e);
            i_this->mMode = 0;
            break;
        case OnlyLanternVisibleAtStart:
            i_this->attention_info.flags = 0;
            fopAcM_OffStatus(i_this, fopAcStts_SHOWMAP_e);
            i_this->mMode = 9;
            break;
        case JalhallaChildA:
        case JalhallaChildB:
            i_this->mJalhallaID = fopAcM_GetLinkId(i_this);
            if (i_this->mJalhallaID == fpcM_ERROR_PROCESS_ID_e) {
                return cPhs_ERROR_e;
            }
            i_this->mCyl.OnTgNoConHit();
            i_this->mCyl.SetTgType(AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT);
            i_this->m39A = 255;
            i_this->m38E = 0;
            i_this->max_health = 4;
            i_this->health = 4;
            i_this->m346 = 0;
            if (i_this->mBehaviorType == JalhallaChildA) {
                i_this->m33E = 1;
                i_this->mAction = 0;
                i_this->mMode = 110;
            } else {
                fopAcM_OnStatus(i_this, fopAcStts_UNK4000_e);
                i_this->m390 = cM_rndFX(16384.0f);
                i_this->current.pos.x += cM_rndFX(150.0f);
                i_this->current.pos.z += cM_rndFX(150.0f);
                i_this->mAction = 5;
                i_this->mMode = 150;
            }
            break;
        }

        BG_check(i_this);
        draw_SUB(i_this);
    }

    return phase;
}

static actor_method_class l_daPW_Method = {
    (process_method_func)daPW_Create,
    (process_method_func)daPW_Delete,
    (process_method_func)daPW_Execute,
    (process_method_func)daPW_IsDelete,
    (process_method_func)daPW_Draw,
};

actor_process_profile_definition g_profile_PW = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_PW_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(pw_class),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_PW_e,
    /* Actor SubMtd */ &l_daPW_Method,
    /* Status       */ fopAcStts_SHOWMAP_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e | fopAcStts_UNK80000_e,
    /* Group        */ fopAc_ENEMY_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
