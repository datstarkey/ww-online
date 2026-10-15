/**
 * d_a_npc_bms1.cpp
 * NPC - Bomb-Master Cannon (Windfall Bomb Shop shopkeeper)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_npc_bms1.h"
#include "d/d_cc_d.h"
#include "res/Object/Bms.h"

class daNpc_Bms1_childHIO_c : public JORReflexible {
public:
    daNpc_Bms1_childHIO_c();
    virtual ~daNpc_Bms1_childHIO_c() {}

    void genMessage(JORMContext* ctx) { UNUSED(ctx); }

public:
    /* 0x04 */ dNpc_HIO_c mNpc;
    /* 0x2C */ f32 m2C;
    /* 0x30 */ f32 m30;
    /* 0x34 */ f32 m34;
    /* 0x38 */ f32 m38;
    /* 0x3C */ f32 m3C;
    /* 0x40 */ f32 m40;
    /* 0x44 */ f32 m44;
    /* 0x48 */ f32 m48;
    /* 0x4C */ f32 m4C;
}; // Size: 0x50

class daNpc_Bms1_HIO_c : public JORReflexible {
public:
    daNpc_Bms1_HIO_c();
    virtual ~daNpc_Bms1_HIO_c() {}

    void genMessage(JORMContext* ctx) { UNUSED(ctx); }

public:
    /* 0x04 */ s8 mNo;
    /* 0x08 */ s32 m08;
    /* 0x0C */ daNpc_Bms1_childHIO_c mChild[1];
}; // Size: 0x5C

static fpc_ProcID l_msgId;
static msg_class* l_msg;
static daNpc_Bms1_HIO_c l_HIO;


static dCcD_SrcCyl l_cyl_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ AT_TYPE_ALL,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsEnemy_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsPlayer_e | cCcD_CoSPrm_VsGrpAll_e,
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
    // cM3dGCylS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 30.0f,
        /* Height */ 80.0f,
    }},
};

char daNpc_Bms1_c::m_arcname[] = "Bms";



/* 000000EC-00000108       .text __ct__21daNpc_Bms1_childHIO_cFv */
daNpc_Bms1_childHIO_c::daNpc_Bms1_childHIO_c() {}

/* 00000108-00000148       .text daNpc_Bms1_shopMsgCheck__FUl */
static BOOL daNpc_Bms1_shopMsgCheck(unsigned long i_msgNo) {
    if ((i_msgNo >= 0x2788 && i_msgNo <= 0x2790) || (i_msgNo >= 0x277A && i_msgNo <= 0x2781) ||
        i_msgNo == 0x2783 || i_msgNo == 0x2776) {
        return TRUE;
    }
    return FALSE;
}

/* 00000148-00000188       .text daNpc_Bms1_shopStickMoveMsgCheck__FUl */
static BOOL daNpc_Bms1_shopStickMoveMsgCheck(unsigned long i_msgNo) {
    if ((i_msgNo >= 0x2788 && i_msgNo <= 0x278A) || (i_msgNo >= 0x277A && i_msgNo <= 0x277C) ||
        i_msgNo == 0x2783 || i_msgNo == 0x2776) {
        return TRUE;
    }
    return FALSE;
}

/* 00000188-000002A0       .text __ct__16daNpc_Bms1_HIO_cFv */
daNpc_Bms1_HIO_c::daNpc_Bms1_HIO_c() {
    mChild[0].mNpc.m04 = -50.0f;
    mChild[0].mNpc.mMaxHeadX = 0x1388;
    mChild[0].mNpc.mMaxHeadY = 0x2710;
    mChild[0].mNpc.mMaxBackboneX = 0;
    mChild[0].mNpc.mMaxBackboneY = 0x834;
    mChild[0].mNpc.mMinHeadX = -0x1FFE;
    mChild[0].mNpc.mMinHeadY = 0;
    mChild[0].mNpc.mMinBackboneX = 0;
    mChild[0].mNpc.mMinBackboneY = 0;
    mChild[0].mNpc.mMaxTurnStep = 0x1000;
    mChild[0].mNpc.mMaxHeadTurnVel = 0x800;
    mChild[0].mNpc.mAttnYOffset = 40.0f;
    mChild[0].mNpc.m22 = 0;
    mChild[0].mNpc.mMaxAttnDistXZ = 300.0f;
    mChild[0].m2C = 0.65f;
    mChild[0].m30 = 0.9f;
    mChild[0].m34 = 0.5f;
    mChild[0].m38 = 27.0f;
    mChild[0].m3C = 20.0f;
    mChild[0].m40 = 0.45f;
    mChild[0].m44 = 0.8f;
    mChild[0].m48 = 0.9f;
    mChild[0].m4C = 0.00005f;
    mNo = -1;
    m08 = -1;
}

static const int l_bck_ix_tbl[] = {
    dRes_INDEX_BMS_BCK_BY1_WAIT01_e,
    dRes_INDEX_BMS_BCK_BY1_WAIT02_e,
    dRes_INDEX_BMS_BCK_BY1_TALK01_e,
    dRes_INDEX_BMS_BCK_BY1_TALK02_e,
    dRes_INDEX_BMS_BCK_BY1_BOW_e,
    dRes_INDEX_BMS_BCK_BY1_TALK01_e,
    dRes_INDEX_BMS_BCK_BY1_TALK02_e,
};
static const int l_btp_ix_tbl[] = {
    dRes_INDEX_BMS_BTP_MABA01_e,
    dRes_INDEX_BMS_BTP_BY_T01_e,
};

#include "m_Do/m_Do_mtx.h"

/* 00000300-000004C4       .text nodeCallBack_Bms__FP7J3DNodei */
static BOOL nodeCallBack_Bms(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Bms1_c* actor = (daNpc_Bms1_c*)model->getUserArea();
        int jointIdx = ((J3DJoint*)i_node)->getJntNo();

        if (actor != NULL) {
            PSMTXCopy(model->getAnmMtx(jointIdx), *calc_mtx);

            if (jointIdx == actor->m_head_jnt_num) {
                cXyz offset(0.0f, 0.0f, 0.0f);
                mDoMtx_YrotM(*calc_mtx, -(actor->mJntCtrl.mAngles[0][1] + actor->mHeadAnm.field_0x00.y));
                mDoMtx_ZrotM(*calc_mtx, -(actor->mJntCtrl.mAngles[0][0] + actor->mHeadAnm.field_0x00.x));

                cXyz pos;
                MtxPosition(&offset, &pos);
                actor->setAttentionBasePos(pos);

                offset.set(28.0f, -20.0f, 0.0f);
                MtxPosition(&offset, &pos);
                actor->setEyePos(pos);
            } else if (jointIdx == actor->m_backbone_jnt_num) {
                mDoMtx_XrotM(*calc_mtx, actor->mJntCtrl.getBackbone_y());
                mDoMtx_ZrotM(*calc_mtx, actor->mJntCtrl.getBackbone_x());
            }

            PSMTXCopy(*calc_mtx, J3DSys::mCurrentMtx);
            PSMTXCopy(*calc_mtx, model->getAnmMtx(jointIdx));
        }
    }

    return TRUE;
}

/* 000004C4-00000A00       .text nodeCallBack_BmsHead__FP7J3DNodei */
#include "d/d_a_obj.h"

static BOOL nodeCallBack_BmsHead(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Bms1_c* actor = (daNpc_Bms1_c*)model->getUserArea();
        int jointIdx = ((J3DJoint*)i_node)->getJntNo();

        if (actor != NULL) {
            static cXyz zero = cXyz::Zero;
            static cXyz offset_top(40.0f, 0.0f, 0.0f);

            Mtx base;
            cXyz basePos;
            cXyz center;
            cXyz top;
            cXyz now_top;
            Quaternion rot;

            PSMTXCopy(model->getAnmMtx(jointIdx), mDoMtx_stack_c::get());

            if (jointIdx == actor->m_hairL_jnt_num) {
                cXyz scale;
                cXyz vel;

                scale.x = actor->field_0x2b4;
                scale.y = actor->field_0x2b8;
                scale.z = actor->field_0x2bc;
                scale.x = scale.x * actor->field_0x2cc;
                mDoMtx_stack_c::scaleM(scale.x, scale.y, scale.z);

                PSMTXCopy(mDoMtx_stack_c::get(), base);
                basePos.set(base[0][3], base[1][3], base[2][3]);
                base[0][3] = base[1][3] = base[2][3] = 0.0f;

                mDoMtx_stack_c::transS(basePos.x, basePos.y, basePos.z);
                mDoMtx_stack_c::quatM(&actor->field_0x304);
                PSMTXConcat(mDoMtx_stack_c::get(), base, mDoMtx_stack_c::get());
                mDoMtx_stack_c::multVec(&offset_top, &top);
                mDoMtx_stack_c::multVec(&zero, &center);
                PSMTXCopy(mDoMtx_stack_c::get(), model->getAnmMtx(jointIdx));

                if (actor->field_0x2d4.isZero()) {
                    actor->field_0x2d4.x = top.x;
                    actor->field_0x2d4.y = top.y;
                    actor->field_0x2d4.z = top.z;
                }

                vel = (top - actor->field_0x2d4) * l_HIO.mChild[0].m40;
                PSVECAdd(&actor->field_0x2ec, &vel, &actor->field_0x2ec);
                PSVECScale(&actor->field_0x2ec, &actor->field_0x2ec, l_HIO.mChild[0].m44);
                PSVECAdd(&actor->field_0x2d4, &actor->field_0x2ec, &actor->field_0x2d4);

                now_top.x = actor->field_0x2d4.x;
                now_top.y = actor->field_0x2d4.y;
                now_top.z = actor->field_0x2d4.z;
                PSVECSubtract(&top, &center, &top);
                PSVECSubtract(&now_top, &center, &now_top);
                daObj::quat_rotVec(&rot, top, now_top);
                C_QUATSlerp(&actor->field_0x304, &rot, &actor->field_0x304, l_HIO.mChild[0].m48);

                f32 dot = PSVECDotProduct(&now_top, &actor->field_0x2ec);
                actor->field_0x2cc = 1.0f - l_HIO.mChild[0].m4C * dot;

                actor->field_0x2cc = cLib_minMaxLimit(actor->field_0x2cc, 0.5f, 1.5f);
            } else if (jointIdx == actor->m_hairR_jnt_num) {
                cXyz scale;
                cXyz vel;

                scale.x = actor->field_0x2c0;
                scale.y = actor->field_0x2c4;
                scale.z = actor->field_0x2c8;
                scale.y = scale.y * actor->field_0x2d0;
                mDoMtx_stack_c::scaleM(scale.x, scale.y, scale.z);

                PSMTXCopy(mDoMtx_stack_c::get(), base);
                basePos.set(base[0][3], base[1][3], base[2][3]);
                base[0][3] = base[1][3] = base[2][3] = 0.0f;

                mDoMtx_stack_c::transS(basePos.x, basePos.y, basePos.z);
                mDoMtx_stack_c::quatM(&actor->field_0x314);
                PSMTXConcat(mDoMtx_stack_c::get(), base, mDoMtx_stack_c::get());
                mDoMtx_stack_c::multVec(&offset_top, &top);
                mDoMtx_stack_c::multVec(&zero, &center);
                PSMTXCopy(mDoMtx_stack_c::get(), model->getAnmMtx(jointIdx));

                if (actor->field_0x2e0.isZero()) {
                    actor->field_0x2e0.x = top.x;
                    actor->field_0x2e0.y = top.y;
                    actor->field_0x2e0.z = top.z;
                }

                vel = (top - actor->field_0x2e0) * l_HIO.mChild[0].m40;
                PSVECAdd(&actor->field_0x2f8, &vel, &actor->field_0x2f8);
                PSVECScale(&actor->field_0x2f8, &actor->field_0x2f8, l_HIO.mChild[0].m44);
                PSVECAdd(&actor->field_0x2e0, &actor->field_0x2f8, &actor->field_0x2e0);

                now_top.x = actor->field_0x2e0.x;
                now_top.y = actor->field_0x2e0.y;
                now_top.z = actor->field_0x2e0.z;
                PSVECSubtract(&top, &center, &top);
                PSVECSubtract(&now_top, &center, &now_top);
                daObj::quat_rotVec(&rot, top, now_top);
                C_QUATSlerp(&actor->field_0x314, &rot, &actor->field_0x314, l_HIO.mChild[0].m48);

                f32 dot = PSVECDotProduct(&now_top, &actor->field_0x2f8);
                actor->field_0x2d0 = 1.0f - l_HIO.mChild[0].m4C * dot;

                actor->field_0x2d0 = cLib_minMaxLimit(actor->field_0x2d0, 0.5f, 1.5f);
            }
        }
    }

    return TRUE;
}

#include "m_Do/m_Do_mtx.h"
#include "d/d_com_inf_game.h"

/* 00000A00-00000C4C       .text set_mtx__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::set_mtx() {
    J3DModel* pModel = mpMorf->getModel();

    if (m89E == 0) {
        cXyz offset(0.0f, 100.0f, 35.0f);
        mDoMtx_stack_c::transS(current.pos);
        mDoMtx_stack_c::YrotM(current.angle.y);
        mDoMtx_stack_c::transM(offset.x, offset.y, offset.z);
    } else {
        mDoMtx_stack_c::transS(current.pos);
        mDoMtx_stack_c::YrotM(current.angle.y);
    }
    pModel->setBaseTRMtx(mDoMtx_stack_c::get());

    mDoMtx_stack_c::copy(pModel->getAnmMtx(m_head_jnt_num));
    mpHeadModel->setBaseTRMtx(mDoMtx_stack_c::get());

    if (m89E == 1) {
        mDoMtx_stack_c::copy(pModel->getAnmMtx(m_head_jnt_num));
        mpEyeModel->setBaseTRMtx(mDoMtx_stack_c::get());

        mDoMtx_stack_c::copy(pModel->getAnmMtx(m_head_jnt_num));
        mpMaskModel->setBaseTRMtx(mDoMtx_stack_c::get());
    } else if (dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT)) {
        mDoMtx_stack_c::copy(pModel->getAnmMtx(m_head_jnt_num));
        mpEyeModel->setBaseTRMtx(mDoMtx_stack_c::get());
    }

    if (mpLowModel) {
        mDoMtx_stack_c::copy(pModel->getAnmMtx(m_leg_jnt_num));
        mpLowModel->setBaseTRMtx(mDoMtx_stack_c::get());
    }

    if (mpTaruModel) {
        mDoMtx_stack_c::transS(current.pos);
        mpTaruModel->setBaseTRMtx(mDoMtx_stack_c::get());
    }
}

#include "res/Object/Bms.h"
#include "JSystem/JUtility/JUTAssert.h"
#include "d/d_com_inf_game.h"
#include "d/d_item.h"

/* 00000C4C-00000D54       .text initTexPatternAnm__12daNpc_Bms1_cFb */
BOOL daNpc_Bms1_c::initTexPatternAnm(bool i_modify) {
    J3DModelData* modelData = mpHeadModel->getModelData();
    m_head_tex_pattern = (J3DAnmTexPattern*)dComIfG_getObjectRes(m_arcname, l_btp_ix_tbl[m898]);
    JUT_ASSERT(574, m_head_tex_pattern != 0);
    if (!mBtp.init(modelData, m_head_tex_pattern, TRUE, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, i_modify, FALSE)) {
        return FALSE;
    }
    mBlinkFrame = 0;
    field_0x34e = 0;
    return TRUE;
}

/* 00000D54-00000DE0       .text playTexPatternAnm__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::playTexPatternAnm() {
    if (cLib_calcTimer(&field_0x34e) == 0) {
        if (mBlinkFrame >= m_head_tex_pattern->getFrameMax()) {
            mBlinkFrame -= m_head_tex_pattern->getFrameMax();
            field_0x34e = cM_rndF(100.0f) + 30.0f;
        } else {
            mBlinkFrame++;
        }
    }
}

/* 00000DE0-00000E78       .text setAnm__12daNpc_Bms1_cFScf */
void daNpc_Bms1_c::setAnm(s8 i_idx, f32 i_morf) {
    static int play_mode_tbl[] = {
        J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_LOOP,
        J3DFrameCtrl::EMode_LOOP,
    };
    static f32 morf_frame_tbl[] = {8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f, 8.0f};
    static f32 play_speed_tbl[] = {1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f};

    if (i_morf < 0.0f) {
        i_morf = morf_frame_tbl[i_idx];
    }
    if (i_idx != m899 || m899 == -1) {
        m899 = i_idx;
        dNpc_setAnm_2(mpMorf, play_mode_tbl[i_idx], i_morf, play_speed_tbl[i_idx], l_bck_ix_tbl[i_idx], 0, m_arcname);
    }
}

/* 00000E78-00000EBC       .text setTexAnm__12daNpc_Bms1_cFSc */
void daNpc_Bms1_c::setTexAnm(signed char i_idx) {
    if (m898 != i_idx || m898 == -1) {
        m898 = i_idx;
        initTexPatternAnm(true);
    }
}

/* 00000EBC-00001100       .text setAnmFromMsgTag__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::setAnmFromMsgTag() {
    if (m89E == 1) {
        return;
    }

    switch (dComIfGp_getMesgAnimeAttrInfo()) {
    case 0:
        setAnm(0, -1.0f);
        break;
    case 1:
        setAnm(1, -1.0f);
        break;
    case 2:
        setAnm(2, -1.0f);
        break;
    case 3:
        setAnm(3, -1.0f);
        break;
    case 4:
        setAnm(4, -1.0f);
        m89A = 1;
        break;
    case 5:
        setAnm(5, -1.0f);
        m89A = 3;
        break;
    case 6:
        setAnm(6, -1.0f);
        m89A = 3;
        break;
    }

    if (m899 == 5) {
        if (mpMorf->checkFrame(mpMorf->getEndFrame() - 1.0f) && --m89A <= 0) {
            setAnm(0, 13.0f);
        }
    } else if (m899 == 6) {
        if (mpMorf->checkFrame(mpMorf->getEndFrame() - 1.0f) && --m89A <= 0) {
            setAnm(1, -1.0f);
        }
    } else if (m899 == 4) {
        if (mpMorf->checkFrame(mpMorf->getEndFrame() - 1.0f) && --m89A <= 0) {
            setAnm(1, -1.0f);
        }
    }

    dComIfGp_clearMesgAnimeAttrInfo();
}

/* 00001100-000011D8       .text chkAttention__12daNpc_Bms1_cF4cXyzs */
#include "d/actor/d_a_player.h"
BOOL daNpc_Bms1_c::chkAttention(cXyz i_pos, short) {
    fopAc_ac_c* player = dComIfGp_getPlayer(0);
    f32 range = l_HIO.mChild[0].mNpc.mMaxAttnDistXZ;
    cXyz diff = player->current.pos - i_pos;
    cXyz dir(cM_ssin(current.angle.y), 0.0f, cM_scos(current.angle.y));
    return range > PSVECDotProduct(&dir, &diff);
}

/* 000011D8-00001278       .text eventOrder__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::eventOrder() {
    if (field_0x89b == 4) {
        fopAcM_orderOtherEvent2(this, "BMS_LAND_DEMO", 1, 0xFFFF);
    } else if (field_0x89b == 3) {
        fopAcM_orderOtherEvent2(this, "BMS_GET_DEMO", 1, 0xFFFF);
    } else if (field_0x89b == 1 || field_0x89b == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x89b == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    }
}

/* 00001278-000014AC       .text checkOrder__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::checkOrder() {
    if (eventInfo.getCommand() == dEvtCmd_INDEMO_e) {
        if (field_0x89b == 4) {
            field_0x89b = 0;
            mShopCam.Reset();
            setAction(&daNpc_Bms1_c::event_action, NULL);
        } else if (field_0x89b == 3) {
            field_0x89b = 0;
            mShopCam.Reset();
            setAction(&daNpc_Bms1_c::getdemo_action, NULL);
        }
    } else if (eventInfo.getCommand() == dEvtCmd_INTALK_e) {
        if (field_0x89b == 1 || field_0x89b == 2) {
            field_0x89b = 0;
            field_0x7cd = 1;
            talkInit();
            mShopCam.shop_cam_action_init();
            cXyz playerPos(-70.0f, 0.0f, 150.0f);
            daPy_getPlayerActorClass()->setPlayerPosAndAngle(&playerPos, (s16)-0x7000);
        }
    } else {
        mShopCam.Save();
    }
}

/* 000014AC-000017FC       .text next_msgStatus__12daNpc_Bms1_cFPUl */
#include "m_Do/m_Do_controller_pad.h"
#include "d/d_shop.h"
#include "JAZelAudio/JAZelAudio_SE.h"
#if TARGET_PC
// u32 is unsigned int on the host, not unsigned long; same type as the original on MWCC.
u16 daNpc_Bms1_c::next_msgStatus(u32* pMsgNo) {
#else
u16 daNpc_Bms1_c::next_msgStatus(unsigned long* pMsgNo) {
#endif
    u16 status = fopMsgStts_MSG_CONTINUES_e;
    u32 msg = *pMsgNo;

    switch (msg) {
    case 0x2775:
    case 0x2777:
    case 0x2780:
    case 0x2782:
        (*pMsgNo)++;
        break;
    case 0x2784:
        *pMsgNo = 0x2786;
        break;
    case 0x2776:
        if (CPad_CHECK_TRIG_B(0)) {
            *pMsgNo = 0x2779;
            break;
        }
        dComIfGp_setDoStatusForce(dActStts_CHOOSE_e);
        dComIfGp_setAStatusForce(dActStts_CANCEL_e);
        status = fopMsgStts_MSG_DISPLAYED_e;
        break;
    case 0x2783:
        if (CPad_CHECK_TRIG_B(0)) {
            *pMsgNo = 0x2787;
            break;
        }
        dComIfGp_setDoStatusForce(dActStts_CHOOSE_e);
        dComIfGp_setAStatusForce(dActStts_CANCEL_e);
        status = fopMsgStts_MSG_DISPLAYED_e;
        break;
    case 0x277A:
    case 0x277B:
    case 0x277C:
        if (CPad_CHECK_TRIG_B(0)) {
            *pMsgNo = 0x2779;
            break;
        }
        dComIfGp_setDoStatusForce(dActStts_CHOOSE_e);
        dComIfGp_setAStatusForce(dActStts_CANCEL_e);
        *pMsgNo += 3;
        break;
    case 0x2788:
    case 0x2789:
    case 0x278A:
        if (CPad_CHECK_TRIG_B(0)) {
            *pMsgNo = 0x2787;
            break;
        }
        dComIfGp_setDoStatusForce(dActStts_CHOOSE_e);
        dComIfGp_setAStatusForce(dActStts_CANCEL_e);
        *pMsgNo += 3;
        break;
    case 0x2778:
    case 0x2781:
        *pMsgNo = 0x2776;
        break;
    case 0x2786:
    case 0x278E:
    case 0x278F:
        *pMsgNo = 0x2783;
        break;
    case 0x277D:
    case 0x277E:
    case 0x277F:
        if (dComIfGp_checkMesgCancelButton()) {
            *pMsgNo = msg - 3;
            break;
        }
        if (l_msg->mSelectNum == 0) {
            *pMsgNo = 0x2780;
            break;
        }
        *pMsgNo -= 3;
        break;
    case 0x278B:
    case 0x278C:
    case 0x278D:
        if (dComIfGp_checkMesgCancelButton()) {
            *pMsgNo = msg - 3;
            break;
        }
        if (l_msg->mSelectNum == 0) {
            int rupee = dComIfGp_getMessageRupee();
            u8 boughtStatus = dShop_BoughtErrorStatus(&mShopItems, 0, rupee);

            if (boughtStatus & 0x20) {
                *pMsgNo = 0x278E;
                break;
            }
            if (boughtStatus & 4) {
                *pMsgNo = 0x278F;
                break;
            }

            fopAcM_seStart(this, JA_SE_SHOP_BOUGHT, 0);
            mShopItems.hideSelectItem();
            field_0x7e8 = mShopItems.getSelectItemNo();
            dComIfGp_setItemRupeeCount(-rupee);
            int itemNo = mShopItems.getSelectItemNo();
            if (!checkItemGet(itemNo, 1)) {
                field_0x89b = 3;
                status = fopMsgStts_MSG_ENDS_e;
                break;
            }
            itemNo = mShopItems.getSelectItemNo();
            execItemGet(itemNo);
            *pMsgNo = 0x2790;
            break;
        }
        *pMsgNo -= 3;
        break;
    case 0x2790:
        *pMsgNo = 0x2783;
        break;
    default:
        status = fopMsgStts_MSG_ENDS_e;
        break;
    }
    return status;
}

/* 000017FC-00001860       .text getMsg__12daNpc_Bms1_cFv */
u32 daNpc_Bms1_c::getMsg() {
    u32 msg;
    if (field_0x7e4 != 0) {
        msg = field_0x7e4;
        field_0x7e4 = 0;
    } else if (!dComIfGs_isEventBit(0xA02)) {
        msg = 0x2775;
    } else {
        msg = 0x2782;
    }
    return msg;
}

/* 00001860-0000191C       .text setCollision__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::setCollision() {
    cXyz offset(0.0f, 0.0f, 0.0f);
    cXyz center;

    offset.z = -16.0f;
    MtxTrans(current.pos.x, current.pos.y, current.pos.z, 0);
    mDoMtx_YrotM(*calc_mtx, field_0x7c2.y);
    MtxPosition(&offset, &center);
    mCyl.SetC(center);
    mCyl.SetR(46.0f);
    mCyl.SetH(130.0f);
    dComIfG_Ccsp()->Set(&mCyl);
}

/* 0000191C-00001928       .text talkInit__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::talkInit() {
    field_0x8a1 = 0x0;
}

/* 00001928-00001A34       .text normal_talk__12daNpc_Bms1_cFv */
u16 daNpc_Bms1_c::normal_talk() {
    u16 status = l_msg->mStatus;
    if (status == fopMsgStts_MSG_DISPLAYED_e) {
        l_msg->mStatus = next_msgStatus(&field_0x7d8);
        if (l_msg->mStatus == fopMsgStts_MSG_CONTINUES_e) {
            fopMsgM_messageSet(field_0x7d8);
        }
    } else if (status == fopMsgStts_BOX_CLOSED_e) {
        l_msg->mStatus = fopMsgStts_MSG_DESTROYED_e;
    } else if (status == fopMsgStts_MSG_PREPARING_e) {
        fopMsgM_demoMsgFlagOn();
    }

    cXyz zoomPos = mShopCam.getItemZoomPos(100.0f);
    mShopItems.Item_ZoomUp(zoomPos);
    mpShopCursor->hide();
    if (dComIfGp_checkMesgSendButton()) {
        field_0x7d8 = l_msg->mMsgNo;
    }
    return status;
}

/* 00001A34-00001B88       .text shop_talk__12daNpc_Bms1_cFv */
u16 daNpc_Bms1_c::shop_talk() {
    mpShopCursor->show();
    if (dShop_now_triggercheck(l_msg, &mStickControl, &mShopItems, &field_0x7d8, NULL, NULL)) {
        field_0x7a4 = 1;
        field_0x7dc = 0;
        if (mShopItems.mSelectedItemIdx >= 0 && !checkItemGet(dItemNo_PEARL_NAYRU_e, TRUE)) {
            mHeadAnm.swing_vertical_init(1, 0x1800, 0x1000, 1);
        }
    }

    u16 status = l_msg->mStatus;
    if (status == fopMsgStts_MSG_DISPLAYED_e || status == fopMsgStts_MSG_CONTINUES_e) {
        if (field_0x7a4 != 0) {
            field_0x7a4 = 0;
        } else {
            field_0x7dc = field_0x7d8;
            l_msg->mStatus = next_msgStatus(&field_0x7dc);
            if (l_msg->mStatus == fopMsgStts_MSG_CONTINUES_e) {
                fopMsgM_messageSet(field_0x7dc);
            }
        }
    } else if (status == fopMsgStts_BOX_CLOSED_e) {
        l_msg->mStatus = fopMsgStts_MSG_DESTROYED_e;
        mShopItems.mSelectedItemIdx = -1;
    } else if (status == fopMsgStts_MSG_PREPARING_e) {
        fopMsgM_demoMsgFlagOn();
    }

    return status;
}

/* 00001B88-00001D68       .text talk__12daNpc_Bms1_cFv */
u16 daNpc_Bms1_c::talk() {
    u16 status = 0xFF;

    if (field_0x8a1 == 0) {
        l_msgId = fpcM_ERROR_PROCESS_ID_e;
        l_msg = NULL;
        field_0x7d8 = getMsg();
        field_0x7dc = 0;
        field_0x8a1 = 1;
        field_0x7e8 = 0xFF;
    } else if (field_0x8a1 != -1) {
        if (l_msgId == fpcM_ERROR_PROCESS_ID_e) {
            l_msgId = fopMsgM_messageSet(field_0x7d8, this);
        } else if (l_msg == NULL) {
            l_msg = fopMsgM_SearchByID(l_msgId);
            if (l_msg) {
                if (!daNpc_Bms1_shopStickMoveMsgCheck(field_0x7d8)) {
                    field_0x8a1 = 2;
                } else {
                    field_0x8a1 = 3;
                }
            }
        } else {
            setAnmFromMsgTag();
            switch (field_0x8a1) {
            case 2:
                status = normal_talk();
                break;
            case 3:
                status = shop_talk();
                break;
            }
            if (dComIfGp_checkMesgSendButton()) {
                field_0x7d8 = l_msg->mMsgNo;
                if (!daNpc_Bms1_shopStickMoveMsgCheck(field_0x7d8)) {
                    if (!daNpc_Bms1_shopMsgCheck(field_0x7d8)) {
                        mShopItems.mSelectedItemIdx = -1;
                        mShopItems.showItem();
                    }
                    field_0x8a1 = 2;
                } else {
                    if (field_0x7d8 == 0x2776 || field_0x7d8 == 0x2783) {
                        mShopItems.mSelectedItemIdx = -1;
                        mShopItems.showItem();
                    }
                    field_0x8a1 = 3;
                }
            }
        }
    }

    mShopCam.SetSelectIdx(mShopItems.mSelectedItemIdx);
    return status;
}

#include "d/d_lib.h"

/* 00001D68-00002104       .text CreateInit__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::CreateInit() {
    cXyz dummy(0.0f, 0.0f, 0.0f);

    field_0x7c2.x = current.angle.x;
    field_0x7c2.y = current.angle.y;
    field_0x7c2.z = current.angle.z;

    attention_info.flags = fopAc_Attn_LOCKON_TALK_e | fopAc_Attn_ACTION_SPEAK_e;
    attention_info.distances[fopAc_Attn_TYPE_TALK_e] = 0xAD;
    attention_info.distances[fopAc_Attn_TYPE_SPEAK_e] = 0xAD;

    gravity = -30.0f;

    switch (m89E) {
    case 0:
        setAction(&daNpc_Bms1_c::wait_action, NULL);
        break;
    case 1:
        setAction(&daNpc_Bms1_c::wait_action, NULL);
        break;
    }

    field_0x7b4 = current.pos.x;
    field_0x7b8 = current.pos.y;
    field_0x7bc = current.pos.z;

    mStts.Init(0xFF, 0xFF, this);
    mCyl.Set(l_cyl_src);
    mCyl.SetStts(&mStts);

    field_0x8a2[0] = 0;
    field_0x7a4 = 0;
    field_0x7e4 = 0;
    field_0x7e8 = 0xFF;

    int shopIdx = checkItemGet(dItemNo_PEARL_NAYRU_e, TRUE) ? 2 : 1;

    if (m89E == 1) {
        field_0x2b4 = 1.2f;
        field_0x2b8 = 0.9f;
        field_0x2bc = 0.9f;
        field_0x2c0 = 0.7f;
        field_0x2c4 = 0.6f;
        field_0x2c8 = 0.6f;
    } else if (checkItemGet(dItemNo_PEARL_NAYRU_e, TRUE)) {
        field_0x2b4 = 0.4f;
        field_0x2b8 = 0.4f;
        field_0x2bc = 0.4f;
        field_0x2c0 = 0.4f;
        field_0x2c4 = 0.4f;
        field_0x2c8 = 0.4f;
    } else {
        field_0x2b4 = 1.2f;
        field_0x2b8 = 0.9f;
        field_0x2bc = 0.9f;
        field_0x2c0 = 0.7f;
        field_0x2c4 = 0.6f;
        field_0x2c8 = 0.6f;
    }

    field_0x2cc = 1.0f;
    field_0x2d0 = 1.0f;

    field_0x304 = ZeroQuat;
    field_0x314 = ZeroQuat;

    mShopCam.setCamDataIdx(shopIdx);
    mShopItems.setItemDataIdx(shopIdx);
    mShopCam.setCamAction(NULL);

    mShopItems.setItemSetDataList();
    mShopItems.createItem(3, fopAcM_GetRoomNo(this));

    field_0x7e9 = 0;
    field_0x7d4 = current.pos.y;

    mEventCut.setActorInfo("Bms1", this);
    mEventCut.setJntCtrlPtr(&mJntCtrl);

    mpMorf->calc();
    set_mtx();

    return TRUE;
}

/* 00002104-00002144       .text setAttention__12daNpc_Bms1_cFb */
void daNpc_Bms1_c::setAttention(bool i_force) {
    if (!i_force && field_0x7cb >= 2) {
        return;
    }
    attention_info.position.set(field_0x7b4, field_0x7b8 + l_HIO.mChild[0].mNpc.mAttnYOffset, field_0x7bc);
}

/* 00002144-000021F4       .text checkPlayerLanding__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::checkPlayerLanding() {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
    f32 player_y = player->current.pos.y;
    BOOL ret = FALSE;
    if (field_0x7d4 - player_y > 200.0f && !player->checkPlayerFly()) {
        ret = TRUE;
    }
    if (!player->checkPlayerFly()) {
        field_0x7d4 = player_y;
    }
    return ret;
}

/* 000021F4-00002424       .text lookBack__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::lookBack() {
    cXyz target;
    cXyz center(0.0f, 0.0f, 0.0f);
    cXyz* pTarget = NULL;
    s16 angleY = current.angle.y;

    switch (field_0x89c) {
    case 1:
        if (field_0x7cc) {
            target = dNpc_playerEyePos(l_HIO.mChild[0].mNpc.m04);
            pTarget = &target;
            center = current.pos;
            center.y = eyePos.y;
        } else {
            pTarget = NULL;
        }
        break;
    case 2:
        if (mShopItems.mSelectedItemIdx == -1) {
            target = mShopCam.getItemZoomPos(100.0f);
        } else {
            cXyz basePos = mShopItems.getSelectItemBasePos();
            target = mShopItems.getSelectItemPos();
            mpShopCursor->setPos(basePos);
            mpShopCursor->setScale(l_HIO.mChild[0].m2C, l_HIO.mChild[0].m30, l_HIO.mChild[0].m34, l_HIO.mChild[0].m38, l_HIO.mChild[0].m3C);
            mpShopCursor->anm_play();
        }
        pTarget = &target;
        center = current.pos;
        center.y = eyePos.y;
        break;
    }

    if (mJntCtrl.mbTrn) {
        cLib_addCalcAngleS2(&field_0x7c0, l_HIO.mChild[0].mNpc.mMaxHeadTurnVel, 4, 0x800);
    } else {
        field_0x7c0 = 0;
    }
    mJntCtrl.lookAtTarget(&current.angle.y, pTarget, center, angleY, field_0x7c0, true);
}

/* 00002424-0000249C       .text wait01__12daNpc_Bms1_cFv */
bool daNpc_Bms1_c::wait01() {
    if (field_0x7cd) {
        field_0x89d = field_0x89c;
        field_0x89c = 2;
    } else if (field_0x7cc) {
        if (m89E == 1) {
            field_0x89b = 0;
        } else {
            field_0x89b = 2;
        }
    }
    return mpMorf->isMorf();
}

/* 0000249C-00002590       .text talk01__12daNpc_Bms1_cFv */
bool daNpc_Bms1_c::talk01() {
    u16 status = talk();
    if (status == fopMsgStts_BOX_CLOSED_e) {
        daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
        field_0x89c = field_0x89d;
        dComIfGp_event_reset();
        mShopCam.Reset();
        player->offNoResetFlg0(daPy_py_c::daPyFlg0_NO_DRAW);
        field_0x7cd = 0;
    } else if (daNpc_Bms1_shopMsgCheck(field_0x7d8) && status == 8 && field_0x7d8 == mShopItems.getSelectItemBuyMsg()) {
        dComIfGp_setDoStatusForce(dActStts_CHOOSE_e);
        dComIfGp_setAStatusForce(dActStts_CANCEL_e);
    }
    return mpMorf->isMorf();
}

#include "d/actor/d_a_player.h"
/* 00002590-00002764       .text getdemo_action__12daNpc_Bms1_cFPv */
BOOL daNpc_Bms1_c::getdemo_action(void*) {
    int staffIdx = dComIfGp_evmng_getMyStaffId("Bms1");

    if (mActionStatus == ACTION_STARTING) {
        daPy_getPlayerActorClass()->offPlayerNoDraw();
        field_0x89c = field_0x89d;
        mShopCam.Reset();
        fpc_ProcID itemPtId = fopAcM_createItemForPresentDemo(&current.pos, mShopItems.getSelectItemNo(), 0, -1, DEMO_SELECT(-1, current.roomNo));
        if (itemPtId != fpcM_ERROR_PROCESS_ID_e) {
            dComIfGp_event_setItemPartnerId(itemPtId);
        }
        dComIfGp_evmng_cutEnd(staffIdx);
        mActionStatus++;
    } else if (mActionStatus != ACTION_ENDING) {
        fopMsgM_demoMsgFlagOn();
        dComIfGp_evmng_cutEnd(staffIdx);
        if (dComIfGp_evmng_endCheck("BMS_GET_DEMO")) {
            field_0x89b = 1;
            dComIfGp_event_reset();
            field_0x7e4 = 0x2790;
            setAction(&daNpc_Bms1_c::wait_action, NULL);
        }
    }

    return TRUE;
}

/* 00002764-00002918       .text wait_action__12daNpc_Bms1_cFPv */
int daNpc_Bms1_c::wait_action(void*) {
    if (mActionStatus == ACTION_STARTING) {
        field_0x89c = 1;
        mActionStatus++;
    } else if (mActionStatus != ACTION_ENDING) {
        s16 angle = current.angle.y + mJntCtrl.getHead_y() + mJntCtrl.getBackbone_y();
        field_0x7cc = chkAttention(current.pos, angle);

        if (m89E == 1) {
            if (mpMorf->checkFrame(mpMorf->getEndFrame() - 1.0f)) {
                if (!cLib_calcTimer(&m89A)) {
                    m89A = 3;
                    JAIZelBasic::zel_basic->seStart(JA_SE_CV_BO_STRUGGLE, &eyePos, 0,
                                                     dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
                }
            }
        }

        switch (field_0x89c) {
        case 1:
            wait01();
            break;
        case 2:
            talk01();
            break;
        }

        lookBack();

        if (checkPlayerLanding() && m89E != 1) {
            field_0x89b = 4;
        }

        setAttention(true);
    }
    return 1;
}

/* 00002918-00002A98       .text event_action__12daNpc_Bms1_cFPv */
int daNpc_Bms1_c::event_action(void*) {
    if (mActionStatus == ACTION_STARTING) {
        if (m89E != 1) {
            if (dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT)) {
                setAnm(1, -1.0f);
            } else {
                setAnm(0, -1.0f);
            }
        }
        mActionStatus++;
    } else if (mActionStatus != ACTION_ENDING) {
        privateCut();
        if (dComIfGp_evmng_endCheck("BMS_LAND_DEMO")) {
            field_0x89b = 0;
            dComIfGp_event_reset();
            setAction(&daNpc_Bms1_c::wait_action, NULL);
        }
    }
    return 1;
}

/* 00002A98-00002B94       .text evn_talk_init__12daNpc_Bms1_cFi */
BOOL daNpc_Bms1_c::evn_talk_init(int i_staffIdx) {
    u32* msg_no_p = (u32*)dComIfGp_evmng_getMyIntegerP(i_staffIdx, "MsgNo");
    u32* end_msg_no_p = (u32*)dComIfGp_evmng_getMyIntegerP(i_staffIdx, "EndMsgNo");

    l_msgId = fpcM_ERROR_PROCESS_ID_e;
    l_msg = NULL;

    if (msg_no_p) {
        field_0x7d8 = *msg_no_p;
        if (field_0x7d8 == 0x2791 && dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT)) {
            field_0x7d8 = 0x2792;
        }
    } else {
        field_0x7d8 = 0;
    }

    if (end_msg_no_p) {
        field_0x7e0 = *end_msg_no_p;
    } else {
        field_0x7e0 = 0;
    }
    return TRUE;
}

/* 00002B94-00002BFC       .text evn_continue_talk_init__12daNpc_Bms1_cFi */
BOOL daNpc_Bms1_c::evn_continue_talk_init(int i_staffIdx) {
    u32* end_msg_no_p = (u32*)dComIfGp_evmng_getMyIntegerP(i_staffIdx, "EndMsgNo");

    if (end_msg_no_p) {
        field_0x7e0 = *end_msg_no_p;
    } else {
        field_0x7e0 = 0;
    }
    return TRUE;
}

/* 00002BFC-00002DD8       .text evn_talk__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::evn_talk() {
    if (l_msgId == fpcM_ERROR_PROCESS_ID_e) {
        l_msgId = fopMsgM_messageSet(field_0x7d8, &eyePos);
    } else if (l_msg == NULL) {
        l_msg = fopMsgM_SearchByID(l_msgId);
        fopMsgM_demoMsgFlagOn();
    } else {
        setAnmFromMsgTag();
        u16 msgStatus = l_msg->mStatus;
        if (msgStatus == fopMsgStts_MSG_DISPLAYED_e) {
            l_msg->mStatus = next_msgStatus(&field_0x7d8);
            if (l_msg->mStatus == fopMsgStts_MSG_CONTINUES_e) {
                fopMsgM_messageSet(field_0x7d8);
            }
        } else {
            if (msgStatus == fopMsgStts_BOX_CLOSED_e) {
                if (field_0x7d8 == 0x1DCA) {
                    int idx;
                    s16 delta;
                    if (field_0x8a3 == 0x49) {
                        idx = dBeastIdx_RED_JELLY_e;
                        delta = -10;
                    } else if (field_0x8a3 == 0x4A) {
                        idx = dBeastIdx_GREEN_JELLY_e;
                        delta = -20;
                    } else {
                        idx = dBeastIdx_BLUE_JELLY_e;
                        delta = -30;
                    }
                    dComIfGp_setItemBeastNumCount(idx, delta);
                } else if (field_0x7d8 == 0x1DC4) {
                    int idx;
                    if (field_0x8a3 == 0x49) {
                        idx = dBeastIdx_RED_JELLY_e;
                    } else if (field_0x8a3 == 0x4A) {
                        idx = dBeastIdx_GREEN_JELLY_e;
                    } else {
                        idx = dBeastIdx_BLUE_JELLY_e;
                    }
                    dComIfGp_setItemBeastNumCount(idx, -10);
                }
                l_msg->mStatus = fopMsgStts_MSG_DESTROYED_e;
                l_msg = NULL;
                l_msgId = fpcM_ERROR_PROCESS_ID_e;
                return TRUE;
            }
            if ((msgStatus == fopMsgStts_BOX_OPENING_e || msgStatus == fopMsgStts_MSG_TYPING_e) && (field_0x7d8 == field_0x7e0)) {
                field_0x7e0 = 0;
                return TRUE;
            }
        }
    }
    return FALSE;
}

/* 00002DD8-00002E30       .text evn_viblation_init__12daNpc_Bms1_cFi */
BOOL daNpc_Bms1_c::evn_viblation_init(int) {
    dComIfGp_getVibration().StartShock(5, -17, cXyz(0.0f, 1.0f, 0.0f));
    return TRUE;
}

/* 00002E30-00002E68       .text evn_head_swing_init__12daNpc_Bms1_cFi */
BOOL daNpc_Bms1_c::evn_head_swing_init(int) {
    mHeadAnm.swing_vertical_init(1, 0x1800, 0x1000, 1);
    return TRUE;
}

/* 00002E68-00002FBC       .text privateCut__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::privateCut() {
    static char* cut_name_tbl[] = {
        "TALKMSG",
        "CONTINUE_TALK",
        "VIBLATE",
        "HEADSWING",
    };

    int staffIdx = dComIfGp_evmng_getMyStaffId(mEventCut.getActorName());
    if (staffIdx == -1) {
        return FALSE;
    }
    int actIdx = dComIfGp_evmng_getMyActIdx(staffIdx, cut_name_tbl, ARRAY_SIZE(cut_name_tbl), true, 0);
    if (actIdx == -1) {
        dComIfGp_evmng_cutEnd(staffIdx);
    } else {
        if (dComIfGp_evmng_getIsAddvance(staffIdx)) {
            switch (actIdx) {
            case 0:
                evn_talk_init(staffIdx);
                break;
            case 1:
                evn_continue_talk_init(staffIdx);
                break;
            case 2:
                evn_viblation_init(staffIdx);
                break;
            case 3:
                evn_head_swing_init(staffIdx);
                break;
            }
        }

        BOOL end;
        switch (actIdx) {
        case 0:
        case 1:
            end = evn_talk();
            break;
        default:
            end = TRUE;
            break;
        }
        if (end) {
            dComIfGp_evmng_cutEnd(staffIdx);
        }
    }

    return TRUE;
}

/* 00002FBC-000030A4       .text demo_move__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::demo_move() {
    dDemo_actor_c* demo_actor = dComIfGp_demo_getActor(demoActorID);
    if (demo_actor == NULL) {
        if (field_0x7e9 == 1) {
            demo_end_init();
        }
        return FALSE;
    }

    field_0x7e9 = 1;
    J3DAnmTexPattern* btp = demo_actor->getP_BtpData("Bms");
    if (btp) {
        mBtp.init(mpHeadModel->getModelData(), btp, TRUE, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, true, FALSE);
    }
    dDemo_setDemoData(this, 0x6A, mpMorf, "Bms");
    return TRUE;
}

/* 000030A4-000030B0       .text demo_end_init__12daNpc_Bms1_cFv */
void daNpc_Bms1_c::demo_end_init() {
    field_0x7e9 = 0x0;
}

#include "d/d_snap.h"

/* 000030B0-00003314       .text _draw__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* head_model_data = mpHeadModel->getModelData();

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);
    mBtp.entry(head_model_data, mBlinkFrame);
    g_env_light.setLightTevColorType(mpHeadModel, &tevStr);
    mpMorf->entryDL();
    mDoExt_modelUpdateDL(mpHeadModel);

    if (m89E == 1) {
        g_env_light.setLightTevColorType(mpEyeModel, &tevStr);
        mDoExt_modelUpdateDL(mpEyeModel);
        g_env_light.setLightTevColorType(mpMaskModel, &tevStr);
        mDoExt_modelUpdateDL(mpMaskModel);
    } else if (dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT)) {
        g_env_light.setLightTevColorType(mpEyeModel, &tevStr);
        mDoExt_modelUpdateDL(mpEyeModel);
    }

    if (mpLowModel) {
        g_env_light.setLightTevColorType(mpLowModel, &tevStr);
        mDoExt_modelUpdateDL(mpLowModel);
    }

    if (mpTaruModel) {
        g_env_light.setLightTevColorType(mpTaruModel, &tevStr);
        mDoExt_modelUpdateDL(mpTaruModel);
    }

    mBtp.remove(head_model_data);

    cXyz shadowPos(current.pos.x, current.pos.y + 130.0f, current.pos.z);
    mShadowId = dComIfGd_setShadow(mShadowId, true, mpMorf->getModel(), &shadowPos, 800.0f, 20.0f, current.pos.y,
                                    mAcch.GetGroundH(), mAcch.m_gnd, &tevStr);
    if (mShadowId) {
        dComIfGd_addRealShadow(mShadowId, mpHeadModel);
    }

    if (mShopItems.mSelectedItemIdx >= 0) {
        mpShopCursor->draw();
    }

    cXyz snapPos = current.pos;
    if (m89E == 1) {
        snapPos.y -= 90.0f;
    }
    f32 snapScale = 1.0f;
    dSnap_RegistFig(DSNAP_TYPE_UNK5C, this, snapPos, current.angle.y, snapScale, snapScale, snapScale);

    return TRUE;
}

/* 00003314-00003474       .text _execute__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::_execute() {
    mJntCtrl.setParam(
        l_HIO.mChild[0].mNpc.mMaxBackboneX,
        l_HIO.mChild[0].mNpc.mMaxBackboneY,
        l_HIO.mChild[0].mNpc.mMinBackboneX,
        l_HIO.mChild[0].mNpc.mMinBackboneY,
        l_HIO.mChild[0].mNpc.mMaxHeadX,
        l_HIO.mChild[0].mNpc.mMaxHeadY,
        l_HIO.mChild[0].mNpc.mMinHeadX,
        l_HIO.mChild[0].mNpc.mMinHeadY,
        l_HIO.mChild[0].mNpc.mMaxTurnStep
    );

    playTexPatternAnm();
    field_0x7ca = mpMorf->play(&eyePos, 0, 0);
    mpMorf->calc();
    if (mpMorf->getFrame() < field_0x7d0) {
        field_0x7ca = 1;
    }
    field_0x7d0 = mpMorf->getFrame();

    if (!demo_move()) {
        checkOrder();
        (this->*mCurrActionFunc)(NULL);
        mShopCam.move();
        mShopItems.Item_Move();
        eventOrder();
    }

    mHeadAnm.move();
    fopAcM_posMoveF(this, mStts.GetCCMoveP());
    mAcch.CrrPos(*dComIfG_Bgsp());
    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mAcch.m_gnd);

    set_mtx();
    setCollision();
    return TRUE;
}

/* 00003474-00003514       .text _delete__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::_delete() {
    if (field_0x8a5 == 1) {
        return TRUE;
    }

    dComIfG_resDelete(&mPhs, m_arcname);
    if (heap && mpMorf) {
        mpMorf->stopZelAnime();
    }

    if (l_HIO.m08 >= 0) {
        l_HIO.m08--;
        if (l_HIO.m08 < 0) {
            mDoHIO_deleteChild(l_HIO.mNo);
        }
    }
    return TRUE;
}

/* 00003514-00003534       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Bms1_c*)i_this)->CreateHeap();
}

/* 00003534-00003718       .text _create__12daNpc_Bms1_cFv */
cPhs_State daNpc_Bms1_c::_create() {
    fopAcM_ct(this, daNpc_Bms1_c);

    m89E = fopAcM_GetParam(this) >> 24;
    if (m89E == 0) {
        if (dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT) && !checkItemGet(dItemNo_PEARL_NAYRU_e, TRUE)) {
            field_0x8a5 = 1;
            return cPhs_ERROR_e;
        }
        field_0x8a5 = 0;
    } else {
        if (dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT) && !checkItemGet(dItemNo_PEARL_NAYRU_e, TRUE)) {
            field_0x8a5 = 0;
        } else {
            field_0x8a5 = 1;
            return cPhs_ERROR_e;
        }
    }

    cPhs_State state = dComIfG_resLoad(&mPhs, m_arcname);
    if (state == cPhs_COMPLEATE_e) {
        switch (m89E) {
        case 0:
            m89E = 0;
            break;
        default:
            m89E = 1;
            break;
        }

        if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, 0x18000)) {
            return cPhs_ERROR_e;
        }
        cullMtx = mpMorf->getModel()->getBaseTRMtx();

        if (l_HIO.m08 < 0) {
            l_HIO.mNo = mDoHIO_createChild("ボム屋店員", &l_HIO); // "Bomb Shop Clerk"
        }
        l_HIO.m08++;

        if (!CreateInit()) {
            return cPhs_ERROR_e;
        }
    }
    return state;
}



/* 00003CE8-000043B8       .text CreateHeap__12daNpc_Bms1_cFv */
BOOL daNpc_Bms1_c::CreateHeap() {
    J3DModelData* modelData;
    J3DAnmTransform* anm;

    if (m89E == 0) {
        modelData = (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_BY1_e);
        if (!checkItemGet(dItemNo_PEARL_NAYRU_e, TRUE)) {
            anm = (J3DAnmTransform*)dComIfG_getObjectRes(m_arcname,
                                                        dRes_INDEX_BMS_BCK_BY1_WAIT01_e);
        } else {
            anm = (J3DAnmTransform*)dComIfG_getObjectRes(m_arcname,
                                                        dRes_INDEX_BMS_BCK_BY1_WAIT02_e);
        }
    } else {
        modelData = (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_BY2_e);
        anm = (J3DAnmTransform*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BCK_BY2_WAIT00_e);
        m89A = 3;
    }

    mpMorf = new mDoExt_McaMorf(modelData, NULL, NULL, anm, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1,
                                TRUE, NULL, 0, 0x11020203);
    if (mpMorf == NULL || mpMorf->getModel() == NULL) {
        mpMorf = NULL;
        return FALSE;
    }

    m_head_jnt_num = modelData->getJointName()->getIndex("head");
    JUT_ASSERT(2083, m_head_jnt_num >= 0);
    m_backbone_jnt_num = modelData->getJointName()->getIndex("backbone");
    JUT_ASSERT(2086, m_backbone_jnt_num >= 0);

    if (m89E == 0) {
        m_leg_jnt_num = modelData->getJointName()->getIndex("center");
        JUT_ASSERT(2091, m_leg_jnt_num >= 0);
    }

    J3DModelData* headModelData =
        (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_BY_HEAD_e);
    mpHeadModel = mDoExt_J3DModel__create(headModelData, 0x80000, 0x11020002);
    if (mpHeadModel == NULL) {
        return FALSE;
    }

    m_hairL_jnt_num = headModelData->getJointName()->getIndex("hairL");
    JUT_ASSERT(2110, m_hairL_jnt_num >= 0);
    m_hairR_jnt_num = headModelData->getJointName()->getIndex("hairR");
    JUT_ASSERT(2113, m_hairR_jnt_num >= 0);

    m898 = 1;
    if (!initTexPatternAnm(false)) {
        return FALSE;
    }

    switch (m89E) {
    case 0:
        if (dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT)) {
            setTexAnm(1);
        } else {
            setTexAnm(0);
        }
        break;
    case 1:
        setTexAnm(1);
        break;
    }

    mpEyeModel = mDoExt_J3DModel__create(
        (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_BY_EYE_e), 0,
        0x11020203);
    if (mpEyeModel == NULL) {
        return FALSE;
    }

    mpMaskModel = mDoExt_J3DModel__create(
        (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_BY_MASK_e), 0,
        0x11020203);

    if (m89E == 0) {
        mpLowModel = mDoExt_J3DModel__create(
            (J3DModelData*)(dComIfGs_isEventBit(dSv_event_flag_c::ENDLESS_NIGHT)
                                ? dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_BY_LOW2_e)
                                : dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_BY_LOW1_e)),
            0, 0x11020203);
        if (mpLowModel == NULL) {
            return FALSE;
        }

        mpTaruModel = mDoExt_J3DModel__create(
            (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BDL_PTARU_e), 0,
            0x11020203);
        if (mpTaruModel == NULL) {
            return FALSE;
        }
    } else {
        mpLowModel = NULL;
    }

    for (u16 i = 0; i < modelData->getJointNum(); i++) {
        if (i == m_head_jnt_num || i == m_backbone_jnt_num) {
            mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(
                nodeCallBack_Bms);
        }
    }
#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    for (u16 i = 0; i < mpHeadModel->getModelData()->getJointNum(); i++) {
        if (i == m_hairL_jnt_num || i == m_hairR_jnt_num) {
            mpHeadModel->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_BmsHead);
        }
    }
#if TARGET_PC
    mpHeadModel->setUserArea((uintptr_t)this);
#else
    mpHeadModel->setUserArea((u32)this);
#endif

    mAcchCir.SetWall(30.0f, 0.0f);
    mAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir,
              fopAcM_GetSpeed_p(this), NULL, NULL);

    J3DAnmTevRegKey* tevRegKey =
        (J3DAnmTevRegKey*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BRK_SHOP_CURSOR01_e);
    J3DModelData* cursorModelData =
        (J3DModelData*)dComIfG_getObjectRes(m_arcname, dRes_INDEX_BMS_BMD_SHOP_CURSOR01_e);
    mpShopCursor = ShopCursor_create(cursorModelData, tevRegKey, l_HIO.mChild[0].m2C);

    if (mpShopCursor != NULL) {
        return TRUE;
    }
    return FALSE;
}

/* 000043B8-000043D8       .text daNpc_Bms1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Bms1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Bms1_c*)i_this)->_create();
}

/* 000043D8-000043F8       .text daNpc_Bms1_Delete__FP12daNpc_Bms1_c */
static BOOL daNpc_Bms1_Delete(daNpc_Bms1_c* i_this) {
    return ((daNpc_Bms1_c*)i_this)->_delete();
}

/* 000043F8-00004418       .text daNpc_Bms1_Execute__FP12daNpc_Bms1_c */
static BOOL daNpc_Bms1_Execute(daNpc_Bms1_c* i_this) {
    return ((daNpc_Bms1_c*)i_this)->_execute();
}

/* 00004418-00004438       .text daNpc_Bms1_Draw__FP12daNpc_Bms1_c */
static BOOL daNpc_Bms1_Draw(daNpc_Bms1_c* i_this) {
    return ((daNpc_Bms1_c*)i_this)->_draw();
}

/* 00004438-00004440       .text daNpc_Bms1_IsDelete__FP12daNpc_Bms1_c */
static BOOL daNpc_Bms1_IsDelete(daNpc_Bms1_c*) {
    return TRUE;
}

// /* 000047AC-000047C8       .text setEyePos__12daNpc_Bms1_cF4cXyz */
// void daNpc_Bms1_c::setEyePos(cXyz) {
//     /* Nonmatching */
// }

// /* 000047C8-000047E4       .text setAttentionBasePos__12daNpc_Bms1_cF4cXyz */
// void daNpc_Bms1_c::setAttentionBasePos(cXyz) {
//     /* Nonmatching */
// }

// /* 00004820-00004900       .text getItemZoomPos__16ShopCam_action_cFf */
// void ShopCam_action_c::getItemZoomPos(float) {
//     /* Nonmatching */
// }

static actor_method_class l_daNpc_Bms1_Method = {
    (process_method_func)daNpc_Bms1_Create,
    (process_method_func)daNpc_Bms1_Delete,
    (process_method_func)daNpc_Bms1_Execute,
    (process_method_func)daNpc_Bms1_IsDelete,
    (process_method_func)daNpc_Bms1_Draw,
};

actor_process_profile_definition g_profile_NPC_BMS1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BMS1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Bms1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BMS1_e,
    /* Actor SubMtd */ &l_daNpc_Bms1_Method,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_12_e,
};
