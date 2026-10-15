/**
 * d_a_obj_search.cpp
 * Object - Searchlights - Forsaken Fortress
 */

#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_obj_search.h"
#if TARGET_PC
#include "d/actor/d_a_bdk.h" // bdk_class's fields by name
#endif
#include "d/actor/d_a_bk.h"
#include "d/actor/d_a_player_main.h"
#include "d/d_cc_d.h"
#include "d/d_com_inf_game.h"
#include "d/d_event.h"
#include "d/d_lib.h"
#include "d/d_path.h"
#include "d/d_bg_s_movebg_actor.h"
#include "d/d_s_play.h"
#include "f_op/f_op_actor_mng.h"
#include "f_pc/f_pc_name.h"
#include "m_Do/m_Do_lib.h"
#include "m_Do/m_Do_mtx.h"
#include "JAZelAudio/JAIZelBasic.h"
#include "SSystem/SComponent/c_lib.h"
#include "SSystem/SComponent/c_math.h"
#include "res/Object/Search.h"

const char daObj_Search::Act_c::m_arc_name[] = "Search";

static dCcD_SrcCps cps_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ 0,
        /* SrcObjTg  SPrm    */ 0,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e | cCcD_CoSPrm_VsEnemy_e | cCcD_CoSPrm_NoCrr_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ 0,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoHitMark_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCpsS
    {{
        /* Start  */ {0.0f, 0.0f, 0.0f},
        /* End    */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 50.0f,
    }},
};

const daObj_Search::Act_c::Attr_c daObj_Search::Act_c::m_attr = {
    /* mSkipExecute */ 0,
    /* mSkipDraw    */ 0,
    /* m02          */ 0x14,
    /* mBeamLength  */ 16000.0f,
    /* m08          */ 0,
    /* m0A          */ {0, 0, 0, 0, 0, 0},
    /* m10          */ 4000.0f,
    /* m14          */ 0.05f,
    /* m18          */ -0.0987f,
    /* m1C          */ 0.1017f,
    /* m20          */ 4.0f,
    /* m24          */ 1,
    /* m25          */ {0, 0, 0},
    /* mCpsR        */ 460.0f,
    /* m2C          */ {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
    /* m38          */ 1,
    /* m39          */ 0,
    /* m3A          */ 0,
    /* m3B          */ 0,
    /* m3C          */ 0x14,
    /* m3E          */ 0,
    /* m3F          */ {0, 0, 0},
    /* m42          */ 0,
    /* m43          */ 0,
    /* m44          */ 0,
    /* m45          */ 0,
    /* m46          */ 0,
    /* m47          */ 0,
    /* m48          */ {0, 0, 0, 0},
    /* m4C          */ 2000.0f,
    /* m50          */ 0x2D,
    /* m52          */ {0, 0},
    /* m54          */ 600.0f,
    /* m58          */ 1500.0f,
    /* m5C          */ 2000,
    /* m5E          */ 14,
};

/* 800FDAFC-800FDB1C       .text createHeap_CB__FP10fopAc_ac_c */
static BOOL createHeap_CB(fopAc_ac_c* i_this) {
    return static_cast<daObj_Search::Act_c*>(i_this)->_createHeap();
}

/* 800FDB1C-800FDB8C       .text _createHeap__Q212daObj_Search5Act_cFv */
BOOL daObj_Search::Act_c::_createHeap() {
    if (!searchCreateHeap()) {
        return FALSE;
    }

    if (!beamCreateHeap(0)) {
        return FALSE;
    }

    if (!beamCreateHeap(1)) {
        return FALSE;
    }

    return TRUE;
}

/* 800FDB8C-800FDCAC       .text searchCreateHeap__Q212daObj_Search5Act_cFv */
bool daObj_Search::Act_c::searchCreateHeap() {
    J3DModelData* modelData = static_cast<J3DModelData*>(dComIfG_getObjectRes(m_arc_name, dRes_INDEX_SEARCH_BDL_S_SEARCH_e));
    JUT_ASSERT(57, modelData != NULL);

    J3DModel* pModel = mDoExt_J3DModel__create(modelData, 0x80000, 0x11000022);
    mpModel = pModel;

    if (mpModel == NULL) {
        return false;
    }

    dBgW* bgw = new dBgW();
    mpBgW = bgw;
    if (mpBgW == NULL) {
        return false;
    }

    cBgD_t* pDat = (cBgD_t*)dComIfG_getObjectRes(m_arc_name, dRes_INDEX_SEARCH_DZB_S_BASE_e);
    if (mpBgW->Set(pDat, cBgW::MOVE_BG_e, &mBgMtx) == true) {
        return false;
    }

    return true;
}

/* 800FDCAC-800FDDBC       .text beamCreateHeap__Q212daObj_Search5Act_cFi */
bool daObj_Search::Act_c::beamCreateHeap(int i_no) {
    static int dzb[] = {
        dRes_INDEX_SEARCH_DZB_S_SEARCH_LIGHTA_e,
        dRes_INDEX_SEARCH_DZB_S_SEARCH_LIGHTB_e,
    };

    J3DModelData* modelData = (J3DModelData*)dComIfG_getObjectRes(m_arc_name, dRes_INDEX_SEARCH_BDL_S_BEAM_e);
    if (modelData == NULL) {
        return false;
    }

    mpBeamModel[i_no] = mDoExt_J3DModel__create(modelData, 0x80000, 0x11000022);
    if (mpBeamModel[i_no] == NULL) {
        return false;
    }

    mpBeamBgW[i_no] = new dBgW();
    if (mpBeamBgW[i_no] == NULL) {
        return false;
    }

    cBgD_t* pDat = (cBgD_t*)dComIfG_getObjectRes(m_arc_name, dzb[i_no]);
    if (mpBeamBgW[i_no]->Set(pDat, cBgW::MOVE_BG_e, &mBeamBgMtx[i_no]) == true) {
        return false;
    }

    return true;
}

/* 800FDDBC-800FDE08       .text nodeControl_CB__FP7J3DNodei */
static BOOL nodeControl_CB(J3DNode* node, int calcTiming) {
    if (calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daObj_Search::Act_c* i_this = reinterpret_cast<daObj_Search::Act_c*>(model->getUserArea());
        if (i_this != NULL) {
            i_this->_nodeControl(node, model);
        }
    }
    return TRUE;
}

/* 800FDE08-800FE10C       .text _nodeControl__Q212daObj_Search5Act_cFP7J3DNodeP8J3DModel */
void daObj_Search::Act_c::_nodeControl(J3DNode* node, J3DModel* model) {
    J3DJoint* joint = (J3DJoint*)node;
    int jntNo = joint->getJntNo();
    mDoMtx_stack_c::copy(model->getAnmMtx(jntNo));

    s16 diff = mAngle[0].y - field_0x7b0;
    s16 scaled = diff * (1.0f + attr().m20);
    mShaftAngle = 0x2700;

    switch (jntNo) {
    case S_SEARCH_JNT_LIGHTA_e:
        mDoMtx_stack_c::XrotM(mAngle[0].y);
        mDoMtx_stack_c::YrotM(mAngle[0].x);
        break;
    case S_SEARCH_JNT_LIGHTB_e:
        mDoMtx_stack_c::XrotM(mAngle[1].y);
        mDoMtx_stack_c::YrotM(mAngle[1].x);
        break;
    case S_SEARCH_JNT_SHAFT_e:
        mDoMtx_stack_c::YrotM(mShaftAngle);
        break;
    case S_SEARCH_JNT_HANDLE_e:
        mDoMtx_stack_c::YrotM(mShaftAngle);
        mDoMtx_stack_c::XrotM(mGearAngleS);
        break;
    }

    if (mMode != MODE_STOP_e) {
        switch (jntNo) {
        case S_SEARCH_JNT_GEAR_S_e:
            mGearAngleS = mGearAngleS + scaled * (2.0f + attr().m1C);
            mDoMtx_stack_c::XrotM(mGearAngleS);
            break;
        case S_SEARCH_JNT_GEAR_M_e:
            mGearAngleM = mGearAngleM - scaled * (1.5f + attr().m18);
            mDoMtx_stack_c::XrotM(mGearAngleM);
            break;
        case S_SEARCH_JNT_GEAR_L_e:
            mGearAngleL = mGearAngleL + scaled * (1.0f + attr().m14);
            mDoMtx_stack_c::XrotM(mGearAngleL);
            break;
        }
    }

    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 800FE10C-800FE110       .text modeSearchRndInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeSearchRndInit() {}

/* 800FE110-800FE200       .text modeSearchRnd__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeSearchRnd() {
    if (field_0x834 != 0 && attr().m38 != 0 && player_check()) {
        modeProcInit(MODE_FIND_e);
    }

    field_0x7b2 = mAngle[0].x;
    field_0x7b4 = mAngle[1].x;
    field_0x7b0 = mAngle[0].y;

    mAngle[0].x = attr().m08;

    f32 speed = 16.0f * (1.0f + attr().m20);
    mAngle[0].y = (s16)(mAngle[0].y + speed);

    mAngle[1].x = -mAngle[0].x;
    mAngle[1].y = mAngle[0].y;
}

/* 800FE200-800FE244       .text modeSearchPathInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeSearchPathInit() {
    if (is_path_info() == false) {
        modeProcInit(MODE_SEARCH_RND_e);
    }
}

/* 800FE244-800FEA6C       .text modeSearchPath__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeSearchPath() {
    dLib_pathMove(&mPathPos, &mPathPntNo, mpPath, attr().m02, NULL, NULL);

    if (field_0x834 != 0) {
        fopAc_ac_c* actor = fopAcM_SearchByName(fpcNm_FGANON_e);

        if (field_0x85c != 0xff && actor != NULL) {
            bk_class* bk = (bk_class*)fopAcM_SearchByID(mChildId);
            if (bk != NULL) {
                bk->m1224 = mGearAngleS;
            }

            if (attr().m39 != 0 || field_0x8d0 == 4) {
                modeProcInit(MODE_TO_STOP_e);
                return;
            }

            if (attr().m3A != 0) {
                fopAc_ac_c* player = dComIfGp_getPlayer(0);
                static cXyz pos = cXyz(0.0f, 100.0f, 0.0f);

                cXyz vec = pos;
                vec = vec + player->current.pos - mLightPos[0];
                field_0x7b0 = mAngle[0].y;

                s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
                s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));

                cLib_addCalcAngleS2(&mAngle[mLightNo].y, angle_y, 10, 0x400);
                cLib_addCalcAngleS2(&mAngle[mLightNo].x, angle_x, 10, 0x400);
                return;
            } else if (field_0x8d0 == 2) {
                fopAc_ac_c* player = dComIfGp_getPlayer(0);
                static cXyz pos = cXyz(0.0f, 100.0f, 0.0f);

                cXyz vec = pos;
                vec = vec + player->current.pos - mLightPos[0];
                field_0x7b0 = mAngle[0].y;

                s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
                s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));

                cLib_addCalcAngleS2(&mAngle[mLightNo].y, angle_y, 10, 0x400);
                cLib_addCalcAngleS2(&mAngle[mLightNo].x, angle_x, 10, 0x400);
                return;
            } else if (field_0x8d0 == 3) {
                static cXyz pos = cXyz(0.0f, 100.0f, 0.0f);

                cXyz vec = pos;
                vec = vec + actor->current.pos - mLightPos[0];
                field_0x7b0 = mAngle[0].y;

                s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
                s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));

                cLib_addCalcAngleS2(&mAngle[mLightNo].y, angle_y, 10, 0x400);
                cLib_addCalcAngleS2(&mAngle[mLightNo].x, angle_x, 10, 0x400);
                return;
            }
        } else if (attr().m38 != 0) {
            if (player_check()) {
                if (dComIfGp_demo_mode() != 1) {
                    mFindTimer++;

                    if (mFindTimer > attr().m5E) {
                        modeProcInit(MODE_FIND_e);
                        mFindTimer = 0;
                    }
                }
            } else {
                mFindTimer = 0;
            }
        }
    }

    mTargetPos[0] = mPathPos;

    cXyz vec = mTargetPos[0] - mLightPos[0];
    field_0x7b0 = mAngle[0].y;
    field_0x7b2 = mAngle[0].x;
    field_0x7b4 = mAngle[1].x;

    s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
    s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));

    if (attr().m3E != 0) {
        cLib_addCalcAngleS2(&mAngle[0].y, angle_y, 30, 0x400);
        cLib_addCalcAngleS2(&mAngle[0].x, angle_x, 30, 0x400);
        mAngle[1].y = mAngle[0].y;
    } else if (REG12_S(4) == 0) {
        cLib_addCalcAngleS2(&mAngle[0].y, angle_y, 30, 0x400);
        cLib_addCalcAngleS2(&mAngle[0].x, angle_x, 30, 0x400);
        mAngle[1].y = mAngle[0].y;
    } else {
        mAngle[0].y = angle_y;
        mAngle[0].x = angle_x;
        mAngle[1].y = mAngle[0].y;
    }

    mAngle[1].x = cM_deg2s(attr().m3C);
}

/* 800FEA6C-800FEA80       .text modeStopInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeStopInit() {
    mAngle[0].x = 0x2300;
    mAngle[1].x = -0x2300;
}

/* 800FEA80-800FEA84       .text modeStop__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeStop() {}

/* 800FEA84-800FEA88       .text modeToSearchInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeToSearchInit() {}

/* 800FEA88-800FEB30       .text modeToSearch__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeToSearch() {
    bool done0 = false;
    bool done1 = false;
    s16 diff0 = cLib_addCalcAngleS(&mAngle[0].x, field_0x7b2, 10, 0x200, 0x100);
    if (diff0 < 0x100) {
        done0 = true;
    }
    s16 diff1 = cLib_addCalcAngleS(&mAngle[1].x, field_0x7b4, 10, 0x200, 0x100);
    if (diff1 < 0x100) {
        done1 = true;
    }
    if (done0 && done1) {
        modeProcInit(MODE_SEARCH_PATH_e);
    }
}

/* 800FEB30-800FEBB4       .text modeToStopInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeToStopInit() {
    field_0x7b0 = mAngle[0].y;

    if (dComIfGs_isEventBit(0x201)) {
        fopAcM_seStart(this, JA_SE_OBJ_SEARCH_LIGHT_UP, 0);
    }
}

/* 800FEBB4-800FEECC       .text modeToStop__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeToStop() {
    bool done0 = false;
    bool done1 = false;

    if ((!dComIfGs_isEventBit(0x201) || attr().m42 != 0) && field_0x85c == 0xFF) {
        if (eventInfo.checkCommandDemoAccrpt()) {
            mBeamAlpha = 0xFF;
            int staffIdx = dComIfGp_evmng_getMyStaffId("Search");

            if (dComIfGp_evmng_endCheck("Search_Light_Up")) {
                dComIfGp_event_onEventFlag(8);
                dComIfGs_onEventBit(0x201);
                modeProcInit(MODE_STOP_e);
            } else {
                bk_class* bk = (bk_class*)fopAcM_SearchByID(mChildId);
                if (bk != NULL && bk->m0300[1] < 5) {
                    bk->m0300[1] = 5;
                }

                char* cutName = dComIfGp_getPEvtManager()->getMyNowCutName(staffIdx);
                if (strcmp(cutName, "BK_FIND") == 0) {
                    dComIfGp_evmng_cutEnd(staffIdx);
                }
                if (strcmp(cutName, "LIGHT_UP_SOUND") == 0) {
                    mDoAud_seStart(JA_SE_OBJ_SEARCH_LIGHT_UP, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                    dComIfGp_evmng_cutEnd(staffIdx);
                }
                if (strcmp(cutName, "LIGHT_UP") == 0) {
                    if (abs(cLib_addCalcAngleS(&mAngle[0].x, 0x2300, 30, 0x300, 0x10)) < 0x100) {
                        done0 = true;
                    }
                    if (abs(cLib_addCalcAngleS(&mAngle[1].x, -0x2300, 30, 0x300, 0x10)) < 0x100) {
                        done1 = true;
                    }
                    if (done0 && done1) {
                        smoke_set(5.0f, 10);
                        dComIfGp_evmng_cutEnd(staffIdx);
                    }
                }
            }
        } else {
            fopAcM_orderOtherEvent2(this, "Search_Light_Up", dEvtFlag_NOPARTNER_e);
        }
    } else {
        if (abs(cLib_addCalcAngleS(&mAngle[0].x, 0x2300, 20, 0x200, 0x10)) < 0x100) {
            done0 = true;
        }
        if (abs(cLib_addCalcAngleS(&mAngle[1].x, -0x2300, 20, 0x200, 0x10)) < 0x100) {
            done1 = true;
        }
        if (done0 && done1) {
            smoke_set(5.0f, 10);
            modeProcInit(MODE_STOP_e);
        }
    }
}

/* 800FEECC-800FEF80       .text modeFindInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeFindInit() {
    if (field_0x85c != 0xff) {
        modeProcInit(MODE_FIND_2ND_e);
    } else {
        if (field_0x8d0 != 0) {
            dComIfGs_onEventBit(0x340);
        }

        if (!eventInfo.checkCommandDemoAccrpt()) {
            mDoAud_seStart(JA_SE_MAJUTOU_ALERM);
        }

        field_0x7b2 = mAngle[0].x;
        field_0x7b4 = mAngle[1].x;
        field_0x7b0 = mAngle[0].y;
    }
}

/* 800FEF80-800FF44C       .text modeFind__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeFind() {
    dComIfGp_evmng_getEventIdx("Search_Light_Find");
    dComIfGp_evmng_getEventIdx("Search_Light_Find_With_Barrel");
    dComIfGp_evmng_getEventIdx("Search_Light_Find_Wall");

    daPy_py_c* player = daPy_getPlayerActorClass();
    player_check();
    mLightInfluence.mColor.r = attr().m5C;
    mLightInfluence.mColor.g = attr().m5C;

    static cXyz pos = cXyz(0.0f, 100.0f, 0.0f);

    cXyz vec = pos;
    vec = vec + player->current.pos - mLightPos[mLightNo];
    field_0x7b0 = mAngle[0].y;

    s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
    s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));

    if (mLightNo == 0) {
        mAngle[1].y = angle_y;
    } else {
        angle_y += 0x8000;
        angle_x = -angle_x;
        mAngle[0].y = (s16)angle_y;
    }

    cLib_addCalcAngleS2(&mAngle[mLightNo].y, angle_y, 10, 0x400);
    cLib_addCalcAngleS2(&mAngle[mLightNo].x, angle_x, 10, 0x400);

    if (eventInfo.getCommand() == dEvtCmd_INDEMO_e) {
        int staff_id = dComIfGp_evmng_getMyStaffId("Search");

        if (dComIfGp_evmng_endCheck("Search_Light_Find") ||
            dComIfGp_evmng_endCheck("Search_Light_Find_With_Barrel") ||
            dComIfGp_evmng_endCheck("Search_Light_Find_Wall")) {
            JAIZelBasic::getInterface()->seStop(JA_SE_MAJUTOU_ALERM, 20);

            if (attr().m43 != 0) {
                dComIfGp_event_onEventFlag(8);
                modeProcInit(MODE_SEARCH_PATH_e);
            } else {
                dComIfGp_setNextStage("majroom", 0, 0);
            }
        } else {
            if (!dComIfGp_checkPlayerStatus0(0, daPyStts0_UNK1_e) &&
                !dComIfGp_checkPlayerStatus0(0, daPyStts0_HANG_e)) {
                player->changeOriginalDemo();

                s16 target_angle = mAngle[mLightNo].y + current.angle.y;
                if (mLightNo == 0) {
                    target_angle += 0x8000;
                }

                cLib_addCalcAngleS2(&field_0x7e0, target_angle, 4, 0x400);
                player->setPlayerPosAndAngle(&player->current.pos, field_0x7e0);

                if (cLib_distanceAngleS(target_angle, field_0x7e0) < 0x500) {
                    player->cancelOriginalDemo();
                }
            }

            dComIfGp_evmng_cutEnd(staff_id);
        }
    } else if (!player->checkPlayerFly()) {
        if (player->field_0x2b0 < 0.0f) {
            fopAcM_orderOtherEvent2(this, (char*)"Search_Light_Find_With_Barrel", dEvtFlag_NOPARTNER_e);
        } else if (dComIfGp_checkPlayerStatus0(0, daPyStts0_UNK1_e)) {
            fopAcM_orderOtherEvent2(this, (char*)"Search_Light_Find_Wall", dEvtFlag_NOPARTNER_e);
        } else {
            fopAcM_orderOtherEvent2(this, (char*)"Search_Light_Find", dEvtFlag_NOPARTNER_e);
        }
    }
}

/* 800FF44C-800FF49C       .text modeFind2ndInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeFind2ndInit() {
    field_0x7b2 = mAngle[0].x;
    field_0x7b4 = mAngle[1].x;
    field_0x7b0 = mAngle[0].y;
    dComIfGs_onSwitch(field_0x85c, dComIfGp_roomControl_getStayNo());
}

/* 800FF49C-800FF7A4       .text modeFind2nd__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeFind2nd() {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
    player_check();

    static cXyz pos = cXyz(0.0f, 100.0f, 0.0f);

    cXyz vec = pos;
    vec = vec + player->current.pos - mLightPos[mLightNo];
    field_0x7b0 = mAngle[0].y;

    s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
    s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));

    bool clamped = false;
    BOOL is_lo;
    s16 max;
    s16 min;
    BOOL is_hi;
    max = REG12_S(0) + 0x6590;
    min = REG12_S(1) - 0x2710;
    is_lo = angle_x <= min;
    is_hi = angle_x >= max;

    if (is_hi) {
        angle_x = cLib_minLimit<s16>(angle_x, max);
    }
    if (is_lo) {
        angle_x = cLib_maxLimit<s16>(angle_x, min);
    }
    if (is_hi || is_lo) {
        clamped = true;
    }

    cXyz headTop = player->getHeadTopPos();
    mLinChk.Set(&mLightPos[mLightNo], &headTop, this);

    if (dComIfG_Bgsp()->LineCross(&mLinChk)) {
        clamped = true;
    }

    if (clamped) {
        modeProcInit(MODE_SEARCH_PATH_e);
    }

    if (mLightNo == 0) {
        mAngle[1].y = angle_y;
    } else {
        angle_y += 0x8000;
        angle_x = -angle_x;
        mAngle[0].y = (s16)angle_y;
    }

    cLib_addCalcAngleS2(&mAngle[mLightNo].y, angle_y, 10, 0x400);
    cLib_addCalcAngleS2(&mAngle[mLightNo].x, angle_x, 10, 0x400);
}

/* 800FF7A4-800FF7A8       .text modeSearchBdkInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeSearchBdkInit() {}

/* 800FF7A8-800FFE78       .text modeSearchBdk__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::modeSearchBdk() {
    fopAc_ac_c* actor;

    if (fopAcM_SearchByName(fpcNm_BDK_e, &actor)) {
        fopAc_ac_c* bdk = actor;

        if (bdk != NULL) {
            f32 speed = REG12_F(0);
            cXyz pos = bdk->current.pos;
            pos.y += REG12_F(2);
            bool ok = true;

            mFanCenter.x = 3600.0f;
            mFanCenter.y = 10000.0f;
            mFanCenter.z = -3800.0f;
            mCircleRadius = 4000.0f + REG8_F(0);
            mFanRadius = 15000.0f + REG8_F(5);
            mFanSpread = REG8_S(0) + 0x3500;
            mFanAngle = REG8_S(1);

            f32 dist = (mFanCenter - actor->current.pos).absXZ();

            mInFan = dLib_checkActorInFan(mFanCenter, actor, mFanAngle, mFanSpread, mFanRadius, 3000.0f);

            if (dLib_checkActorInCircle(mFanCenter, actor, mCircleRadius, 10000.0f)) {
                mInFan = false;
            }

#if TARGET_PC
            switch (((bdk_class*)bdk)->mAction) { // 0x2C6 on the GameCube only
#else
            switch (*(s16*)((u8*)bdk + 0x2C6)) {
#endif
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 8:
            case 9:
                if (mInFan) {
                    speed += 3000.0f;
                    pos.y += 1000.0f;
                } else {
                    ok = false;
                }
                break;
            case 15:
                ok = false;
                break;
            case 0:
            case 1:
            case 7:
                if (mInFan) {
                    speed += 500.0f;
                    pos.y += 100.0f;
                } else {
                    ok = false;
                }
                break;
            case 10: {
#if TARGET_PC
                s16 sub = ((bdk_class*)bdk)->mState;
#else
                s16 sub = *(s16*)((u8*)bdk + 0x2C8);
#endif

                if (sub == 8) {
                    if (cLib_calcTimer(&mWaitTimer) == 0) {
                        ok = false;
                    } else {
                        pos = mSavePos;
                    }
                } else if (sub == 7) {
                    mWaitTimer = (int)(REG8_S(4) + (300.0f + cM_rndF(100.0f)));
                    pos = mSavePos;
                } else if (sub >= 6) {
                    mSavePos = pos;
                } else if (sub >= 5) {
                    speed += 100.0f;
                    pos.y += 100.0f;
                } else {
                    ok = false;
                }
                break;
            }
            default:
                ok = false;
                break;
            }

            fpc_ProcID id = fopAcM_GetID(this);
            int sign = 1;
            if ((id & 1) == 1) {
                sign = -1;
            }

            if (ok) {
                cLib_addCalc2(&mCirclePath.mRadius, speed, 0.1f, 100.0f);
                mCirclePath.mWobbleAmplitude = 200.0f + REG12_F(1);
                mCirclePath.mAngleSpeed = (REG12_S(0) + 0x150) * sign;
                mCirclePath.mTranslation = pos;
                dLib_setCirclePath(&mCirclePath);

                static cXyz pos = cXyz(0.0f, 100.0f, 0.0f);

                cXyz vec = pos;
                vec = vec + mCirclePath.mPos - mLightPos[0];
                field_0x7b0 = mAngle[0].y;

                s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
                s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));

                cLib_addCalcAngleS2(&mAngle[0].y, angle_y, 10, 0x100);
                cLib_addCalcAngleS2(&mAngle[0].x, angle_x, 10, 0x100);
                mAngle[1].y = mAngle[0].y;
                mAngle[1].x = -mAngle[0].x;
            } else {
                static cXyz pos = cXyz(0.0f, 100.0f, 0.0f);

                cXyz vec = pos;
                s16 target;

                if (mSwingDir == 1) {
                    target = REG8_S(7) + 0x3400;
                } else {
                    target = REG8_S(8) + 0x2600;
                }

                if (cLib_calcTimer(&mSwingTimer) == 0) {
                    mSwingTimer = (int)((REG8_S(2) + 0x3C) + cM_rndF(60.0f + REG8_F(5)));

                    if (mSwingDir == 1) {
                        mSwingDir = -1;
                    } else {
                        mSwingDir = 1;
                    }
                }

                field_0x7b0 = mAngle[0].y;
                cLib_addCalcAngleS2(&mAngle[0].x, target, 20, 0x20);
                mAngle[1].x = -mAngle[0].x;
            }
        }
    }
}

/* 800FFE78-80100080       .text modeProc__Q212daObj_Search5Act_cFQ312daObj_Search5Act_c6Proc_ei */
void daObj_Search::Act_c::modeProc(Proc_e proc, int mode) {
    static ModeEntry mode_tbl[] = {
        { &Act_c::modeSearchRndInit,  &Act_c::modeSearchRnd,  "MODE_SEARCH_RND" },
        { &Act_c::modeSearchPathInit, &Act_c::modeSearchPath, "MODE_SEARCH_PATH" },
        { &Act_c::modeStopInit,       &Act_c::modeStop,       "MODE_STOP" },
        { &Act_c::modeToSearchInit,   &Act_c::modeToSearch,   "MODE_TO_SEARCH" },
        { &Act_c::modeToStopInit,     &Act_c::modeToStop,     "MODE_TO_STOP" },
        { &Act_c::modeFindInit,       &Act_c::modeFind,       "MODE_FIND" },
        { &Act_c::modeFind2ndInit,    &Act_c::modeFind2nd,    "MODE_FIND_2ND" },
        { &Act_c::modeSearchBdkInit,  &Act_c::modeSearchBdk,  "MODE_SEARCH_BDK" },
    };

    if (proc == PROC_INIT_e) {
        mMode = mode;
        (this->*mode_tbl[mMode].mInitFunc)();
    } else if (proc == PROC_EXEC_e) {
        (this->*mode_tbl[mMode].mUpdFunc)();
    }
}

/* 80100080-801001C4       .text __ct__Q212daObj_Search5Bgc_cFv */
daObj_Search::Bgc_c::Bgc_c() {}

/* 801001C4-801002D4       .text wall_pos__Q212daObj_Search5Bgc_cFPCQ212daObj_Search5Act_cP4cXyzP4cXyzPbPf */
void daObj_Search::Bgc_c::wall_pos(const daObj_Search::Act_c* actor, cXyz* start, cXyz* end, bool* hit, float* dot) {
    mLinChk.Set(start, end, const_cast<Act_c*>(actor));

    if (dComIfG_Bgsp()->LineCross(&mLinChk)) {
        cXyz* normal = dComIfG_Bgsp()->GetTriPla(mLinChk.GetBgIndex(), mLinChk.GetPolyIndex())->GetNP();
        cXyz dir = (*end - *start).normZP();

        *dot = dir.inprod(*normal);
        mCrossPos = mLinChk.mLin.GetEnd();
        *end = mCrossPos;
        *hit = true;
    } else {
        *hit = false;
    }
}

bool daObj_Search::Act_c::m_find_flag;

/* 801002D4-801002E0       .text attr__Q212daObj_Search5Act_cCFv */
const daObj_Search::Act_c::Attr_c& daObj_Search::Act_c::attr() const {
    return m_attr;
}

/* 801002E0-8010035C       .text SetArgData__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::SetArgData() {
    u32 param = fopAcM_GetParam(this);

    field_0x7e2 = (param >> 24) & 0xFF;
    mPathId = (param >> 16) & 0xFF;
    field_0x836 = param & 0xFF;
    field_0x8d0 = (param >> 8) & 0xFF;

    if (field_0x8d0 == 0xFF) {
        field_0x8d0 = 0;
    }

    if (field_0x836 == 0xFF) {
        scale.setall(1.0f);
    } else if (field_0x836 == 1) {
        scale.setall(1.27f);
    }

    field_0x85c = (home.angle.x >> 8) & 0xFF;
}

/* 8010035C-8010071C       .text CreateInit__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::CreateInit() {
    set_path_info();

    mBeamCulled[0] = false;
    mBeamCulled[1] = false;
    mCulled = false;

    fopAcM_SetMtx(this, mpBeamModel[0]->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -1000.0f, 0.0f, -1000.0f, 1000.0f, 1500.0f, 1000.0f);

    mpBeamBgW[0]->SetCrrFunc(dBgS_MoveBGProc_Typical);
    mpBeamBgW[1]->SetCrrFunc(dBgS_MoveBGProc_Typical);
    mpBgW->SetCrrFunc(dBgS_MoveBGProc_Typical);

    dComIfG_Bgsp()->Regist(mpBeamBgW[0], this);
    dComIfG_Bgsp()->Regist(mpBeamBgW[1], this);
    dComIfG_Bgsp()->Regist(mpBgW, this);

    mStts[0].Init(0xFF, 0xFF, this);
    mCps[0].Set(cps_src);
    mCps[0].SetStts(&mStts[0]);
    mCps[0].SetStartEnd(current.pos, current.pos);
    mCps[0].SetR(m_attr.mCpsR);

    mStts[1].Init(0xFF, 0xFF, this);
    mCps[1].Set(cps_src);
    mCps[1].SetStts(&mStts[1]);
    mCps[1].SetStartEnd(current.pos, current.pos);
    mCps[1].SetR(m_attr.mCpsR);

    set_mtx_base();
    set_moveBG_mtx_base();

#if TARGET_PC
    mpModel->setUserArea((uintptr_t)this);
#else
    mpModel->setUserArea((u32)this);
#endif
    J3DModelData* modelData = mpModel->getModelData();
    for (u16 i = 0; i < modelData->getJointNum(); i++) {
        switch (i) {
        case S_SEARCH_JNT_GEAR_L_e:
        case S_SEARCH_JNT_GEAR_M_e:
        case S_SEARCH_JNT_GEAR_S_e:
        case S_SEARCH_JNT_HANDLE_e:
        case S_SEARCH_JNT_LIGHTA_e:
        case S_SEARCH_JNT_LIGHTB_e:
        case S_SEARCH_JNT_SHAFT_e:
            modelData->getJointNodePointer(i)->setCallBack(nodeControl_CB);
            break;
        }
    }
    mpModel->calc();

    if (is_path_info()) {
        mpPath = dPath_GetRoomPath(mPathId, fopAcM_GetRoomNo(this));
        dLib_pathMove(&mPathPos, &mPathPntNo, mpPath, m_attr.m02, NULL, NULL);
        mTargetPos[0] = mPathPos;

        cXyz vec = mTargetPos[0] - mLightPos[0];
        field_0x7b0 = mAngle[0].y;
        field_0x7b2 = mAngle[0].x;
        field_0x7b4 = mAngle[1].x;

        s16 angle_y = cM_atan2s(vec.x, vec.z) - current.angle.y;
        s16 angle_x = (s16)cM_atan2s(vec.y, std::sqrtf(vec.x * vec.x + vec.z * vec.z));
        mAngle[0].y = angle_y;
        mAngle[0].x = angle_x;
        mAngle[1].y = mAngle[0].y;
    }

    if (field_0x8d0 == 6) {
        mAngle[0].x = 0x4000;
        mAngle[1].x = -mAngle[0].x;
    }

    set_mtx_light_A();
    set_mtx_light_B();
    set_moveBG_mtx_light_A();
    set_moveBG_mtx_light_B();
    mFindTimer = 0;
}

/* 8010071C-80100B08       .text _create__Q212daObj_Search5Act_cFv */
cPhs_State daObj_Search::Act_c::_create() {
    fopAcM_ct(this, daObj_Search::Act_c);

    cPhs_State phase = dComIfG_resLoad(&mPhase, m_arc_name);

    if (phase == cPhs_COMPLEATE_e) {
        SetArgData();

        if (!fopAcM_entrySolidHeap(this, createHeap_CB, 0x4620)) {
            return cPhs_ERROR_e;
        }

        CreateInit();

        mLightPos[0] = current.pos;
        mLightPos[1] = current.pos;

        if (field_0x8d0 == 6) {
            modeProcInit(MODE_SEARCH_BDK_e);
        } else if (field_0x8d0 == 5) {
            field_0x860 = 1;
            modeProcInit(MODE_STOP_e);
        } else if (is_path_info()) {
            cXyz offset(1.0f, 1.0f, 1.0f);

            mTargetPos[0] = mPathPos;
            mTargetPos[1] = mLightPos[1] + offset;

            if (dComIfGs_isSwitch(field_0x7e2, dComIfGp_roomControl_getStayNo())) {
                mBkControl = false;
                modeProcInit(MODE_STOP_e);
            } else {
                mBkControl = true;
                modeProcInit(MODE_SEARCH_PATH_e);
                set_mtx_light_A();
                set_mtx_light_B();

                cXyz dist = mTargetPos[0] - mLightPos[0];
                s16 angleY = cM_atan2s(dist.x, dist.z) - current.angle.y;
                s16 angleX = cM_atan2s(dist.y, std::sqrtf(dist.x * dist.x + dist.z * dist.z));
                mAngle[0].y = angleY;
                mAngle[0].x = angleX;
                mAngle[1].y = mAngle[0].y;
            }
        } else {
            cXyz offset(1.0f, 1.0f, 1.0f);

            mTargetPos[0] = mLightPos[0] + offset;
            mTargetPos[1] = mLightPos[1] + offset;
            modeProcInit(MODE_STOP_e);
        }

        mBeamEndPos[0] = mTargetPos[0];
        mBeamEndPos[1] = mTargetPos[1];

        dKy_plight_set(&mLightInfluence);

        mLightInfluence.mPos = current.pos;
        mLightInfluence.mColor.r = 0;
        mLightInfluence.mColor.g = 0;
        mLightInfluence.mColor.b = 0;
        mLightInfluence.mPower = 0.0f;
        mLightInfluence.mFluctuation = 0.0f;

        if (field_0x8d0 == 5) {
            mBeamAlpha = 0;
        } else if (dComIfGs_getTime() < 240.0f && dComIfGs_getTime() > 60.0f) {
            mBeamAlpha = 0;
        } else {
            mBeamAlpha = 0xff;
        }

        field_0x82c = 0xeb;
        field_0x82d = 0x7d;

        _execute();
        _execute();
    }

    return phase;
}

/* 80100F9C-801010C4       .text smoke_set__Q212daObj_Search5Act_cFfi */
void daObj_Search::Act_c::smoke_set(float rate, int timer) {
    mSmokeEmitPos.x = mSmokePos.x;
    mSmokeEmitPos.y = mSmokePos.y;
    mSmokeEmitPos.z = mSmokePos.z;

    mSmokeEmitAngle.x = 0;
    mSmokeEmitAngle.y = 0;
    mSmokeEmitAngle.z = 0;

    if (mSmokeCB.getEmitter() == NULL) {
        dComIfGp_particle_setToon(0x2022, &mSmokeEmitPos, &mSmokeEmitAngle, NULL, 0xB9, &mSmokeCB,
                                  fopAcM_GetRoomNo(this));
    }

    if (mSmokeCB.getEmitter() != NULL) {
        mSmokeCB.getEmitter()->setRate(rate);
        mSmokeCB.getEmitter()->setSpread(1.0f);

        f32 dynScale = 2.5f + REG12_F(10);
        JPABaseEmitter* emitter = mSmokeCB.getEmitter();
        emitter->mGlobalDynamicsScale.set(dynScale, dynScale, dynScale);
        emitter->mGlobalParticleScale.set(dynScale, dynScale, dynScale);

        f32 ptclScale = 3.5f + REG12_F(11);
        mSmokeCB.getEmitter()->mGlobalParticleScale.set(ptclScale, ptclScale, ptclScale);
    }

    mSmokeTimer = timer;
}

/* 801010C4-801013AC       .text _execute__Q212daObj_Search5Act_cFv */
bool daObj_Search::Act_c::_execute() {
    if (m_attr.mSkipExecute) {
        return true;
    }

    if (cLib_calcTimer(&mSmokeTimer) == 0) {
        mSmokeCB.remove();
    }

    if (field_0x8d0 != 6 && field_0x8d0 != 5) {
        bk_class* bk = (bk_class*)fopAcM_SearchByID(mChildId);
        if (bk != NULL) {
            field_0x850 = bk->m02B7;
        } else if (field_0x850 != 0xFF && !dComIfGs_isSwitch(field_0x850, dComIfGp_roomControl_getStayNo())) {
            dComIfGs_onSwitch(field_0x850, dComIfGp_roomControl_getStayNo());
        }
    }

    eyePos = current.pos;

    if ((s16)abs(mAngle[0].y - field_0x7b0) != 0 && mMode != MODE_STOP_e) {
        mDoAud_seStart(JA_SE_OBJ_SEARCH_LIGHT, &eyePos, 0, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    }

    mpModel->calc();

    cXyz offset;
    cXyz unused;
    unused.set(0.0f, 0.0f, 0.0f);
    offset.set(0.0f, 0.0f, 0.0f);
    cMtx_copy(mpModel->getAnmMtx(5), mDoMtx_stack_c::now);
    mDoMtx_stack_c::multVec(&offset, &mSmokePos);

    set_mtx_light_A();
    set_mtx_light_B();

    field_0x7a4++;
    if (field_0x7a4 >= 4) {
        field_0x7a4 = 0;
    }

    if (dComIfGs_getTime() < 240.0f && dComIfGs_getTime() > 60.0f) {
        field_0x834 = false;
    } else if (field_0x860) {
        field_0x834 = false;
    } else {
        field_0x834 = true;
    }

    if (field_0x834) {
        cLib_chaseS(&mBeamAlpha, 0xFF, REG12_S(0) + 0x14);
    } else {
        cLib_chaseS(&mBeamAlpha, 0, REG12_S(0) + 0x14);
        mLightInfluence.mPower = 0.0f;
    }

    fopAc_ac_c* ganon = fopAcM_SearchByName(fpcNm_FGANON_e);
    if ((field_0x85c == 0xFF || ganon == NULL) && field_0x8d0 != 5) {
        check_bk_control();
    }

    modeProc(PROC_EXEC_e, 8);
    bg_check();
    return true;
}

/* 801013AC-80101464       .text check_bk_control__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::check_bk_control() {
    bk_class* bk = (bk_class*)fopAcM_SearchByID(mChildId);

    if (bk != NULL) {
        field_0x860 = 0;
        bk->m1224 = mGearAngleS;

        if (mBkControl == 1 && (mMode == 4 || mMode == 2)) {
            modeProcInit(MODE_TO_SEARCH_e);
        } else if (mBkControl == 0 && mMode != 4 && mMode != 2) {
            modeProcInit(MODE_TO_STOP_e);
        }
    }
}

/* 80101464-801014E4       .text set_mtx_base__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::set_mtx_base() {
    mpModel->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    mpModel->setBaseTRMtx(mDoMtx_stack_c::get());
}

/* 801014E4-801018A8       .text set_mtx_light_A__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::set_mtx_light_A() {
    cXyz src(0.0f, 0.0f, 0.0f);
    cXyz tgt(0.0f, 0.0f, m_attr.mBeamLength);

    cMtx_copy(mpModel->getAnmMtx(5), mDoMtx_stack_c::now);
    mDoMtx_stack_c::transM(1004.0f, 376.0f, 1111.0f);
    mDoMtx_stack_c::XYZrotM(0, -0xC8, 0);
    mDoMtx_stack_c::multVec(&src, &mLightPos[0]);

    if (field_0x7a4 == setID % 4 || m_attr.m24 != 0) {
        f32 wall_dist = 0.0f;

        mDoMtx_stack_c::multVec(&tgt, &mBeamEndPos[0]);

        if (m_attr.m3E != 0 || mMode != 5) {
            mBgc.wall_pos(this, &mLightPos[0], &mBeamEndPos[0], &mWallHit[0], &wall_dist);
        }

        f32 length = mLightPos[0].abs(mBeamEndPos[0]);

        if (setID == 0) {
            if (mWallHit[0]) {
                f32 add = 1800.0f * (1.02f + wall_dist);

                if (field_0x8d0 == 6) {
                    add = -(1.02f + wall_dist) * ((500.0f + m_attr.m10) - REG12_F(0));
                }

                length += add;

                cXyz dir = (mBeamEndPos[0] - mLightPos[0]).normZP();
                mBeamEndPos[0] = mBeamEndPos[0] + dir * add;
            }
        } else if (mWallHit[0]) {
            f32 add = (1.02f + wall_dist) * m_attr.m10;

            if (field_0x8d0 == 6) {
                add = -(1.02f + wall_dist) * (REG12_F(0) + (6500.0f + m_attr.m10));
            }

            length += add;

            cXyz dir = (mBeamEndPos[0] - mLightPos[0]).normZP();
            mBeamEndPos[0] = mBeamEndPos[0] + dir * add;
        }

        field_0x65c = length;
        field_0x654 = field_0x65c;
    }

    mDoMtx_stack_c::XYZrotM(0, 0, 0xC80);
    mDoMtx_stack_c::scaleM(scale.x, scale.y, 0.0001043f * field_0x654);
    mpBeamModel[0]->setBaseTRMtx(mDoMtx_stack_c::get());
    cMtx_copy(mDoMtx_stack_c::get(), mBeamMtx[0]);
}

/* 801018A8-80101BAC       .text set_mtx_light_B__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::set_mtx_light_B() {
    cXyz src;
    cXyz tgt;
    src = cXyz(0.0f, 0.0f, 0.0f);
    tgt = cXyz(0.0f, 0.0f, m_attr.mBeamLength);

    cMtx_copy(mpModel->getAnmMtx(6), mDoMtx_stack_c::now);
    mDoMtx_stack_c::transM(742.0f, -402.0f, -1010.0f);
    mDoMtx_stack_c::XYZrotM(-0x8000, 0xC8, 0);
    mDoMtx_stack_c::multVec(&src, &mLightPos[1]);

    if (field_0x7a4 == setID % 4 || m_attr.m24 != 0) {
        f32 wall_dist = 0.0f;

        mDoMtx_stack_c::multVec(&tgt, &mBeamEndPos[1]);

        if (m_attr.m3E != 0 || mMode != 5) {
            mBgc.wall_pos(this, &mLightPos[1], &mBeamEndPos[1], &mWallHit[1], &wall_dist);
        }

        f32 length = mLightPos[1].abs(mBeamEndPos[1]);

        if (mWallHit[1]) {
            f32 add = (1.02f + wall_dist) * m_attr.m10;

            if (field_0x8d0 == 6) {
                add = -(1.02f + wall_dist) * (REG12_F(0) + (6500.0f + m_attr.m10));
            }

            length += add;

            cXyz dir = (mBeamEndPos[1] - mLightPos[1]).normZP();
            mBeamEndPos[1] = mBeamEndPos[1] + dir * add;
        }

        field_0x660 = length;
        field_0x658 = field_0x660;
    }

    mDoMtx_stack_c::XYZrotM(0, 0, 0xC80);
    mDoMtx_stack_c::scaleM(scale.x, scale.y, 0.0001043f * field_0x658);
    mpBeamModel[1]->setBaseTRMtx(mDoMtx_stack_c::get());
    cMtx_copy(mDoMtx_stack_c::get(), mBeamMtx[1]);
}

/* 80101BAC-80101C30       .text set_moveBG_mtx_base__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::set_moveBG_mtx_base() {
    mpModel->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    cMtx_copy(mDoMtx_stack_c::get(), mBgMtx);
    mpBgW->Move();
}

/* 80101C30-80101CB0       .text set_moveBG_mtx_light_A__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::set_moveBG_mtx_light_A() {
    cMtx_copy(mpModel->getAnmMtx(5), mDoMtx_stack_c::now);
    mDoMtx_stack_c::XYZrotM(0, 0, -0x4000);
    mDoMtx_stack_c::transM(40.0f, -270.0f, 30.0f);
    cMtx_copy(mDoMtx_stack_c::now, mBeamBgMtx[0]);
    mpBeamBgW[0]->Move();
}

/* 80101CB0-80101D30       .text set_moveBG_mtx_light_B__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::set_moveBG_mtx_light_B() {
    cMtx_copy(mpModel->getAnmMtx(6), mDoMtx_stack_c::now);
    mDoMtx_stack_c::XYZrotM(0, 0, -0x4000);
    mDoMtx_stack_c::transM(45.0f, -270.0f, 35.0f);
    cMtx_copy(mDoMtx_stack_c::now, mBeamBgMtx[1]);
    mpBeamBgW[1]->Move();
}

/* 80101D30-80101D94       .text bg_check__Q212daObj_Search5Act_cFv */
void daObj_Search::Act_c::bg_check() {
    if (fopAcM_searchPlayerDistance(this) > 2000.0f || mCulled != false) {
        return;
    }

    set_moveBG_mtx_light_A();
    set_moveBG_mtx_light_B();
}

/* 80101D94-8010234C       .text player_check__Q212daObj_Search5Act_cFv */
bool daObj_Search::Act_c::player_check() {
    f32 len;
    daPy_py_c* player = daPy_getPlayerActorClass();

    cXyz vec = player->current.pos - mLightPos[0];
    f32 dist_a = vec.abs();
    vec = player->current.pos - mLightPos[1];
    f32 dist_b = vec.abs();

    mCps[0].SetStartEnd(mLightPos[0], mBeamEndPos[0]);
    mCps[0].SetR(m_attr.mCpsR);
    dComIfG_Ccsp()->Set(&mCps[0]);

    if (mCps[0].ChkCoHit()) {
        fopAc_ac_c* actor = mCps[0].GetCoHitAc();
        cXyz to_actor = actor->current.pos - mLightPos[0];
        cXyz light_dir;
        cXyz light_vec = mBeamEndPos[0] - mLightPos[0];

        light_dir = light_vec.normZP();
        len = light_dir.getDotProduct(to_actor) - 250.0f;
        mLightInfluence.mPos = mLightPos[0] + light_dir * len;
        mLightInfluence.mColor.g = mLightInfluence.mColor.r = m_attr.m50;
        mLightInfluence.mPower = m_attr.m54;

        if (fopAcM_GetName(actor) == fpcNm_PLAYER_e) {
            if (dist_a < 2000.0f || dist_b < 2000.0f) {
                return false;
            }

            if (!(std::fabsf(player->speedF) < 0.1f && player->checkGrabWear())) {
                return true;
            }
        }
    }

    mCps[1].SetStartEnd(mLightPos[1], mBeamEndPos[1]);
    mCps[1].SetR(m_attr.mCpsR);
    dComIfG_Ccsp()->Set(&mCps[1]);

    if (mCps[1].ChkCoHit()) {
        fopAc_ac_c* actor = mCps[1].GetCoHitAc();
        cXyz to_actor = actor->current.pos - mLightPos[1];
        cXyz light_dir;
        cXyz light_vec = mBeamEndPos[1] - mLightPos[1];

        light_dir = light_vec.normZP();
        len = light_dir.getDotProduct(to_actor) - 250.0f;
        mLightInfluence.mPos = mLightPos[1] + light_dir * len;
        mLightInfluence.mColor.g = mLightInfluence.mColor.r = m_attr.m50;
        mLightInfluence.mPower = m_attr.m54;

        if (dist_a < 2000.0f || dist_b < 2000.0f) {
            return false;
        }

        if (!(std::fabsf(player->speedF) < 0.1f && player->checkGrabWear())) {
            return true;
        }
    }

    if (!mCps[0].ChkCoHit() && !mCps[1].ChkCoHit()) {
        cXyz to_path = mTargetPos[0] - mLightPos[0];
        cXyz light_dir;
        cXyz light_vec = mBeamEndPos[0] - mLightPos[0];

        light_dir = light_vec.normZP();
        len = light_dir.getDotProduct(to_path) - 250.0f;
        mLightInfluence.mPos = mLightPos[0] + light_dir * len;
        mLightInfluence.mColor.g = mLightInfluence.mColor.r = m_attr.m50;
        mLightInfluence.mPower = m_attr.m54;
    }

    return false;
}

/* 8010234C-80102384       .text set_path_info__Q212daObj_Search5Act_cFv */
bool daObj_Search::Act_c::set_path_info() {
    if (mPathId != 0xff) {
        return dLib_pathInfo(&mpPath, mPathId);
    }
    return false;
}

/* 80102384-80102398       .text is_path_info__Q212daObj_Search5Act_cFv */
bool daObj_Search::Act_c::is_path_info() {
    return mPathId != 0xff;
}

/* 80102398-801026F8       .text _draw__Q212daObj_Search5Act_cFv */
bool daObj_Search::Act_c::_draw() {
    if (m_attr.mSkipDraw) {
        return true;
    }

    if (mMode != MODE_TO_STOP_e || mMode != MODE_FIND_e) {
        mDoLib_clipper::changeFar(1000000.0f);
        if (m_attr.m44 == 0) {
            mCulled = fopAcM_checkCullingBox(mpModel->getBaseTRMtx(), -1000.0f, 0.0f, -1000.0f, 1000.0f, 1500.0f, 1000.0f);
        }
        if (m_attr.m45 == 0) {
            mBeamCulled[0] = fopAcM_checkCullingBox(mpBeamModel[0]->getBaseTRMtx(), -600.0f, -600.0f, -100.0f, 600.0f, 600.0f, 9585.62f);
        }
        if (m_attr.m46 == 0) {
            mBeamCulled[1] = fopAcM_checkCullingBox(mpBeamModel[1]->getBaseTRMtx(), -600.0f, -600.0f, -100.0f, 600.0f, 600.0f, 9585.62f);
        }
        mDoLib_clipper::resetFar();
    }

    if (mCulled == 0) {
        if (field_0x836 == 1) {
            g_env_light.settingTevStruct(TEV_TYPE_BG0, &current.pos, &tevStr);
            tevStr.mColorC0 = g_env_light.mActorC0;
            tevStr.mColorK0.r = g_env_light.mActorK0.r;
            tevStr.mColorK0.g = g_env_light.mActorK0.g;
            tevStr.mColorK0.b = g_env_light.mActorK0.b;
        } else {
            g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
        }
        g_env_light.setLightTevColorType(mpModel, &tevStr);
        mDoExt_modelEntryDL(mpModel);
    }

    if (mBeamCulled[0] == 0) {
        mDoExt_modelUpdateDL(mpBeamModel[0]);
        if (mBeamAlpha > 0x80) {
            dComIfGd_setAlphaModel(dDlst_alphaModel_c::TYPE_SEARCHLIGHT, mBeamMtx[0], 0x20);
#if TARGET_PC
            // dComIfGd_setAlphaModelColor takes a non-const reference, which cannot bind a temporary.
            GXColor color = {field_0x82c, field_0x82d};
            dComIfGd_setAlphaModelColor(color);
#else
            dComIfGd_setAlphaModelColor((GXColor){field_0x82c, field_0x82d});
#endif
        }
    }

    if (mBeamCulled[1] == 0) {
        mDoExt_modelUpdateDL(mpBeamModel[1]);
        if (mBeamAlpha > 0x80) {
            dComIfGd_setAlphaModel(dDlst_alphaModel_c::TYPE_SEARCHLIGHT, mBeamMtx[1], 0x20);
#if TARGET_PC
            // dComIfGd_setAlphaModelColor takes a non-const reference, which cannot bind a temporary.
            GXColor color = {field_0x82c, field_0x82d};
            dComIfGd_setAlphaModelColor(color);
#else
            dComIfGd_setAlphaModelColor((GXColor){field_0x82c, field_0x82d});
#endif
        }
    }

    J3DGXColor* kcolor = mpBeamModel[0]->getModelData()->getMaterialNodePointer(0)->getTevBlock()->getTevKColor(0);
    kcolor->mColor.a = mBeamAlpha;

    if (m_attr.m47 != 0) {
        static const GXColor color_ok = {0x00, 0xFF, 0x00, 0x80};
        static const GXColor color_ng = {0xFF, 0x00, 0x00, 0x80};
        GXColor color = color_ng;
        if (mInFan != 0) {
            color = color_ok;
        }
        dLib_debugDrawFan(mFanCenter, mFanAngle, mFanSpread, mFanRadius, color);
    }

    return true;
}

/* 801026F8-8010283C       .text _delete__Q212daObj_Search5Act_cFv */
bool daObj_Search::Act_c::_delete() {
    dKy_plight_cut(&mLightInfluence);

    if (mpBeamBgW[0] != NULL && mpBeamBgW[0]->ChkUsed()) {
        dComIfG_Bgsp()->Release(mpBeamBgW[0]);
    }
    if (mpBeamBgW[1] != NULL && mpBeamBgW[1]->ChkUsed()) {
        dComIfG_Bgsp()->Release(mpBeamBgW[1]);
    }
    if (mpBgW != NULL && mpBgW->ChkUsed()) {
        dComIfG_Bgsp()->Release(mpBgW);
    }

    dComIfG_resDelete(&mPhase, m_arc_name);
    mSmokeCB.remove();

    if (JAIZelBasic::getInterface()->checkSePlaying(JA_SE_MAJUTOU_ALERM)) {
        JAIZelBasic::getInterface()->seStop(JA_SE_MAJUTOU_ALERM, 0);
    }

    return true;
}

/* 8010283C-80102844       .text _isdelete__Q212daObj_Search5Act_cFv */
BOOL daObj_Search::Act_c::_isdelete() {
    return TRUE;
}

/* 80102844-80102864       .text Create__Q212daObj_Search4MthdFPv */
cPhs_State daObj_Search::Mthd::Create(void* impl) {
    return ((daObj_Search::Act_c*)impl)->_create();
}

/* 80102864-80102884       .text Delete__Q212daObj_Search4MthdFPv */
bool daObj_Search::Mthd::Delete(void* impl) {
    return static_cast<daObj_Search::Act_c*>(impl)->_delete();
}

/* 80102884-801028A4       .text Execute__Q212daObj_Search4MthdFPv */
bool daObj_Search::Mthd::Execute(void* impl) {
    return static_cast<daObj_Search::Act_c*>(impl)->_execute();
}

/* 801028A4-801028C4       .text Draw__Q212daObj_Search4MthdFPv */
bool daObj_Search::Mthd::Draw(void* impl) {
    return static_cast<daObj_Search::Act_c*>(impl)->_draw();
}

/* 801028C4-801028E4       .text IsDelete__Q212daObj_Search4MthdFPv */
BOOL daObj_Search::Mthd::IsDelete(void* impl) {
    return static_cast<daObj_Search::Act_c*>(impl)->_isdelete();
}

actor_method_class daObj_Search::Mthd::Table = {
    (process_method_func)daObj_Search::Mthd::Create,
    (process_method_func)daObj_Search::Mthd::Delete,
    (process_method_func)daObj_Search::Mthd::Execute,
    (process_method_func)daObj_Search::Mthd::IsDelete,
    (process_method_func)daObj_Search::Mthd::Draw,
};

actor_process_profile_definition g_profile_OBJ_SEARCH = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0003,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_OBJ_SEARCH_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daObj_Search::Act_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_OBJ_SEARCH_e,
    /* Actor SubMtd */ &daObj_Search::Mthd::Table,
    /* Status       */ fopAcStts_UNK4000_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
