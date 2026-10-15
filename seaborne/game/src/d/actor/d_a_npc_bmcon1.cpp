/**
 * d_a_npc_bmcon1.cpp
 * NPC - Willi & Obli (Flight Control Platform)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_npc_bmcon1.h"
#include "d/d_snap.h"

#include "d/d_com_inf_game.h"
#include "d/d_a_obj.h"
#include "d/d_com_lib_game.h"
#include "d/actor/d_a_player.h"

const char daNpcBmcon_c::m_arcname[] = "Bmcon1";

static const int l_bmd_ix_tbl[] = {
    0xC,
    0xC,
};

static const int l_head_bmd_ix_tbl[] = {
    0x15,
    0x14,
};

static const int l_arm_bmd_ix_tbl[] = {
    0xD,
    0xD,
};

static const int l_bck_ix_tbl[] = {
    0x9,
    0xA,
    0x6,
    0x7,
    0x8,
    0xB,
    0x12,
    0x13,
};

static const int l_arm_bck_ix_tbl[] = {
    0x3,
    0x4,
    0x0,
    0x1,
    0x2,
    0x5,
    0x10,
    0x11,
};

static const int l_btp_ix_tbl[] = {
    0x0,
};

static char* l_npc_staff_id[] = {
    "Bmcon1",
    "Bmcon2",
};

static const char* l_arcname_tbl[] = {
    "Bmcon1",
    "Bmcon1",
};

static sBmconAnmDat l_npc_anm_wait[] = { { 0, 8, -1 } };
static sBmconAnmDat l_npc_anm_wait2[] = { { 1, 8, -1 } };
static sBmconAnmDat l_npc_anm_talk[] = { { 2, 8, -1 } };
static sBmconAnmDat l_npc_anm_talk2[] = { { 3, 8, -1 } };
static sBmconAnmDat l_npc_anm_talk3[] = { { 4, 8, -1 } };
static sBmconAnmDat l_npc_anm_walk[] = { { 5, 8, -1 } };
static sBmconAnmDat l_npc_anm_con1[] = { { 6, 8, -1 } };
static sBmconAnmDat l_npc_anm_con2[] = { { 7, 8, -1 } };

static sBmconAnmDat* l_npc_anm_tbl[] = {
    l_npc_anm_wait,
    l_npc_anm_wait2,
    l_npc_anm_talk,
    l_npc_anm_talk2,
    l_npc_anm_talk3,
    l_npc_anm_walk,
    l_npc_anm_con1,
    l_npc_anm_con2,
};

struct NpcDatStruct {
    /* 0x00 */ s16 mMaxHeadX;
    /* 0x02 */ s16 mMaxHeadY;
    /* 0x04 */ s16 mMaxBackboneX;
    /* 0x06 */ s16 mMaxBackboneY;
    /* 0x08 */ s16 mMinHeadX;
    /* 0x0A */ s16 mMinHeadY;
    /* 0x0C */ s16 mMinBackboneX;
    /* 0x0E */ s16 mMinBackboneY;
    /* 0x10 */ s16 mMaxTurnStep;
    /* 0x12 */ u8 field_0x12[0x14 - 0x12];
    /* 0x14 */ f32 field_0x14;
    /* 0x18 */ f32 field_0x18;
    /* 0x1C */ f32 field_0x1c;
    /* 0x20 */ f32 field_0x20;
    /* 0x24 */ f32 field_0x24;
    /* 0x28 */ s16 field_0x28;
    /* 0x2A */ s16 field_0x2a;
    /* 0x2C */ s16 field_0x2c;
    /* 0x2E */ u8 field_0x2e[0x30 - 0x2E];
    /* 0x30 */ f32 field_0x30;
    /* 0x34 */ f32 field_0x34;
    /* 0x38 */ f32 field_0x38;
    /* 0x3C */ u8 field_0x3c;
    /* 0x3D */ u8 field_0x3d;
    /* 0x3E */ u8 field_0x3e;
    /* 0x3F */ u8 field_0x3f;
    /* 0x40 */ s16 field_0x40;
    /* 0x42 */ s16 field_0x42;
    /* 0x44 */ s16 field_0x44;
    /* 0x46 */ s16 field_0x46;
    /* 0x48 */ s16 field_0x48;
    /* 0x4A */ u8 field_0x4a;
    /* 0x4B */ u8 field_0x4b;
}; // Size: 0x4C

static NpcDatStruct l_npc_dat[2] = {
    { 0x2000, 0x1B58, 0, 0x1E78, -0x1000, -0x1B58, 0, -0x1E78, 0x5DC, {0},
      0.0f, 220.0f, 190.0f, 200.0f, 0.0f,
      20000, 1300, 400, {0},
      60.0f, 0.5f, 3.0f,
      0, 6, 4, 0,
      60, 90, 200, 300, 30, 0, 1 },
    { 0x2000, 0x1B58, 0, 0x1E78, -0x1000, -0x1B58, 0, -0x1E78, 0x5DC, {0},
      25.0f, 220.0f, 190.0f, 220.0f, 0.0f,
      32767, 1300, 400, {0},
      60.0f, 0.5f, 3.0f,
      0, 6, 4, 0,
      60, 90, 200, 300, 30, 0, 1 },
};

static u32 l_msg_bmcon1_1st_appear[] = { 0x2A96, 0x2A97, 0x2A98, 0x2A99, 1 };
static u32 l_msg_bmcon1_appear[] = { 0x2A9C, 0x2A9D, 3 };
static u32 l_msg_bmcon1_not_appear[] = { 0x2A9A, 0 };
static u32 l_msg_bmcon1_not_rupee[] = { 0x2A9B, 0 };
static u32 l_msg_bmcon1_2nd[] = { 0x2AAD, 0x2A9E, 2 };
static u32 l_msg_bmcon1_cleared[] = { 0x2AAE, 2 };
static u32 l_msg_bmcon1_appear2[] = { 0x2A9F, 3 };
static u32 l_msg_bmcon1_not_appear2[] = { 0x2AA0, 0 };
static u32 l_msg_bmcon1_talk[] = { 0x2A95, 0 };
static u32 l_msg_bmcon1_result[] = { 0x2AA1, 0x2AA2, 0x2AA3, 0x2AA4, 0x2AA5 };
static u32 l_msg_bmcon1_1st_goal[] = { 0x2AA7, 0x2AA8, 0x2AA9, 4, 0x2AAA, 0 };
static u32 l_msg_bmcon1_goal[] = { 0x2AAB, 0x2AAC, 5, 0x2AAA, 0 };
static u32 l_msg_bmcon2_2st_talk[] = { 0x2AF9, 0 };
static u32 l_msg_bmcon2_cleared[] = { 0x2AFE, 0 };
static u32 l_msg_bmcon2_appear[] = { 0x2AFD, 0x2AFB, 0x2AFC, 0 };
static u32 l_msg_bmcon2_talk[] = { 0x2AFA, 0 };

/* 000000EC-000002A4       .text __ct__12daNpcBmcon_cFv */
daNpcBmcon_c::daNpcBmcon_c() {
    mNpcNo = getPrmNpcNo();
    field_0x7b6 = 0;
    field_0x7b5 = 0;
    field_0x780 = 0.0f;
    field_0x796 = 0;
    field_0x784 = -1.0f;
    field_0x7be = 0;
    field_0x790 = 1;
    field_0x79c = home.angle.y;
    mAnmIdx = 1;
    field_0x7a0 = 0;
    field_0x7c4 = 1;
    field_0x7c3 = 0;
}

/* 00000690-000007F0       .text daNpc_Bmcon_nodeCallBack__FP7J3DNodei */
static BOOL daNpc_Bmcon_nodeCallBack(J3DNode* i_param_1, int i_param_2) {
    if (i_param_2 == 0) {
        J3DModel* model = j3dSys.getModel();
        daNpcBmcon_c* actor = reinterpret_cast<daNpcBmcon_c*>(model->getUserArea());
        s32 uVar1 = ((J3DJoint*)i_param_1)->getJntNo();
        cMtx_copy(model->getAnmMtx(uVar1), *calc_mtx);
        if (uVar1 == actor->m_nec_jnt_num) {
            cMtx_XrotM(*calc_mtx, -(-actor->m_jnt.getHead_y()));
            cMtx_ZrotM(*calc_mtx, -actor->m_jnt.getHead_x());
        }
        if (uVar1 == actor->m_jnt.getBackboneJntNum()) {
            cMtx_XrotM(*calc_mtx, -(-actor->m_jnt.getBackbone_y()));
            cMtx_ZrotM(*calc_mtx, -actor->m_jnt.getBackbone_x());
        }
        if (uVar1 == actor->m_arm_L_jnt_num) {
            cMtx_copy(*calc_mtx, actor->mLeftArmMtx);
        }
        if (uVar1 == actor->m_arm_R_jnt_num) {
            cMtx_copy(*calc_mtx, actor->mRightArmMtx);
        }
        model->setAnmMtx(uVar1, *calc_mtx);
        cMtx_copy(*calc_mtx, j3dSys.mCurrentMtx);
    }
    return TRUE;
}

/* 000007F0-00000834       .text daNpc_Arm_nodeCallBack__FP7J3DNodei */
static BOOL daNpc_Arm_nodeCallBack(J3DNode* i_node, int i_param2) {
    if (i_param2 == 0) {
        J3DModel* model = j3dSys.getModel();
        ((daNpcBmcon_c*)model->getUserArea())->nodeArmControl(i_node, model);
    }
    return TRUE;
}

/* 00000834-000008E4       .text nodeArmControl__12daNpcBmcon_cFP7J3DNodeP8J3DModel */
void daNpcBmcon_c::nodeArmControl(J3DNode* i_node, J3DModel* i_model) {
    s32 jntNo = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_model->getAnmMtx(jntNo));

    if (jntNo == m_armL1_jnt_num) {
        cMtx_copy(mLeftArmMtx, J3DSys::mCurrentMtx);
        i_model->setAnmMtx(jntNo, mLeftArmMtx);
    }

    if (jntNo == m_armR1_jnt_num) {
        cMtx_copy(mRightArmMtx, J3DSys::mCurrentMtx);
        i_model->setAnmMtx(jntNo, mRightArmMtx);
    }
}

/* 000008E4-00000904       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpcBmcon_c*)i_this)->createHeap();
}

/* 00000904-00000994       .text phase_1__FP12daNpcBmcon_c */
static cPhs_State phase_1(daNpcBmcon_c* i_this) {
    fopAcM_ct(i_this, daNpcBmcon_c);

    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    if (player == NULL || fpcM_IsCreating(fopAcM_GetID(player))) {
        return cPhs_INIT_e;
    }

    i_this->field_0x7b6 = 1;
    return cPhs_NEXT_e;
}

/* 00000994-00000A14       .text phase_2__FP12daNpcBmcon_c */
static cPhs_State phase_2(daNpcBmcon_c* i_this) {
    cPhs_State phase = dComIfG_resLoad(&i_this->mPhase2, l_arcname_tbl[i_this->mNpcNo]);

    if (phase == cPhs_COMPLEATE_e) {
        if (fopAcM_entrySolidHeap(i_this, CheckCreateHeap, 0)) {
            return i_this->createInit();
        }

        i_this->mpMorf = NULL;
        return cPhs_ERROR_e;
    }

    return phase;
}

/* 00000A14-00000A44       .text _create__12daNpcBmcon_cFv */
cPhs_State daNpcBmcon_c::_create() {
    static cPhs__Handler l_method[] = {
        (cPhs__Handler)phase_1,
        (cPhs__Handler)phase_2,
        NULL,
    };

    return dComLbG_PhaseHandler(&mPhase, l_method, this);
}

/* 00000A44-0000105C       .text createHeap__12daNpcBmcon_cFv */

BOOL daNpcBmcon_c::createHeap() {
    J3DModelData* modelData = (J3DModelData*)dComIfG_getObjectIDRes(l_arcname_tbl[mNpcNo], l_bmd_ix_tbl[mNpcNo]);
    mpMorf = new mDoExt_McaMorf(
        modelData,
        NULL, NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes(l_arcname_tbl[mNpcNo], l_bck_ix_tbl[mAnmIdx]),
        J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, 1, NULL,
        0x80000, 0x11000002
    );

    J3DModelData* armModelData = (J3DModelData*)dComIfG_getObjectIDRes(l_arcname_tbl[mNpcNo], l_arm_bmd_ix_tbl[mNpcNo]);
    mpArmMorf = new mDoExt_McaMorf(
        armModelData,
        NULL, NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes(l_arcname_tbl[mNpcNo], l_arm_bck_ix_tbl[mAnmIdx]),
        J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, 1, NULL,
        0x80000, 0x11000002
    );

    if (mpArmMorf == NULL || mpArmMorf->getModel() == NULL) {
        return FALSE;
    }

    mpHeadModel = mDoExt_J3DModel__create((J3DModelData*)dComIfG_getObjectIDRes(l_arcname_tbl[mNpcNo], l_head_bmd_ix_tbl[mNpcNo]), 0x80000, 0x37441422);
    if (mpHeadModel == NULL) {
        return FALSE;
    }

    m_jnt.setHeadJntNum(modelData->getJointName()->getIndex("head"));
    JUT_ASSERT(1007, m_jnt.getHeadJntNum() >= 0);
    m_jnt.setBackboneJntNum(modelData->getJointName()->getIndex("backbone"));
    JUT_ASSERT(1011, m_jnt.getBackboneJntNum() >= 0);

    m_nec_jnt_num = modelData->getJointName()->getIndex("neck");
    JUT_ASSERT(1018, m_nec_jnt_num >= 0);
    m_arm_L_jnt_num = modelData->getJointName()->getIndex("armL");
    JUT_ASSERT(1022, m_arm_L_jnt_num >= 0);
    m_arm_R_jnt_num = modelData->getJointName()->getIndex("armR");
    JUT_ASSERT(1024, m_arm_R_jnt_num >= 0);

    m_armL1_jnt_num = armModelData->getJointName()->getIndex("armLloc");
    JUT_ASSERT(1028, m_armL1_jnt_num >= 0);
    m_armR1_jnt_num = armModelData->getJointName()->getIndex("armRloc");
    JUT_ASSERT(1030, m_armR1_jnt_num >= 0);

    for (u16 i = 0; i < modelData->getJointNum(); i++) {
        if (i == m_jnt.getHeadJntNum() || i == m_jnt.getBackboneJntNum() || i == m_nec_jnt_num ||
            i == m_arm_L_jnt_num || i == m_arm_R_jnt_num) {
            modelData->getJointNodePointer(i)->setCallBack(daNpc_Bmcon_nodeCallBack);
        }
    }

#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    for (u16 j = 0; j < armModelData->getJointNum(); j++) {
        if (j == m_armL1_jnt_num || j == m_armR1_jnt_num) {
            armModelData->getJointNodePointer(j)->setCallBack(daNpc_Arm_nodeCallBack);
        }
    }

#if TARGET_PC
    mpArmMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpArmMorf->getModel()->setUserArea((u32)this);
#endif
    mAcchCir.SetWall(30.0f, 30.0f);
    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir, fopAcM_GetSpeed_p(this), fopAcM_GetAngle_p(this), fopAcM_GetShapeAngle_p(this));
    return TRUE;
}

/* 0000105C-0000107C       .text daNpcBmcon_XyCheckCB__FPvi */
static s16 daNpcBmcon_XyCheckCB(void* i_this, int i_itemBtn) {
    return ((daNpcBmcon_c*)i_this)->XyCheckCB(i_itemBtn);
}

/* 0000107C-00001468       .text createInit__12daNpcBmcon_cFv */
cPhs_State daNpcBmcon_c::createInit() {
    int cc_no = 0xff;

    u8 railID = getPrmRailID();
    if (railID != 0xff) {
        mPathRun.setInf(railID, current.roomNo, 1);

        if (mPathRun.getPath() == NULL) {
            return cPhs_ERROR_e;
        }

        dPath_GetNextRoomPath(mPathRun.getPath(), -1);
        fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);

        cXyz point = mPathRun.getPoint(mPathRun.getIdx());
        old.pos = point;
        current.pos = old.pos;

        mPathRun.incIdxLoop();
        field_0x796 = 1;
        cc_no = 0xfe;
    }

    gravity = -9.0f;
    setAnmTbl(l_npc_anm_tbl[mAnmIdx]);

    switch (mNpcNo) {
    case 0:
        field_0x792 = dComIfGp_evmng_getEventIdx("BMCON_RESULT");
        field_0x794 = dComIfGp_evmng_getEventIdx("BMCON_GET_ITEM");

        if (dComIfGs_isTmpBit(0x210)) {
            dComIfGs_offTmpBit(0x210);

            daPy_py_c* player = (daPy_py_c*)dComIfGp_getLinkPlayer();
            fopAcM_orderChangeEventId(player, this, field_0x792, 0, 0xffff);
            player->onNoResetFlg1(daPy_py_c::daPyFlg1_WATER_DROP);
        }
        break;
    case 1:
        field_0x792 = dComIfGp_evmng_getEventIdx("BMCON_END");
        field_0x794 = dComIfGp_evmng_getEventIdx("BMCON_END2");
        break;
    }

    eventInfo.setXyCheckCB(daNpcBmcon_XyCheckCB);
    mEventCut.setActorInfo2(l_npc_staff_id[mNpcNo], this);

    field_0x7a8 = 0;
    field_0x7ac = 0;
    field_0x7ad = 0;
    field_0x7c1 = 0;

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -70.0f, 0.0f, -70.0f, 70.0f, 200.0f, 70.0f);

    attention_info.distances[1] = 0xa9;
    attention_info.distances[3] = 0xa9;
    attention_info.flags = 0xa;

    m_jnt.setParam(l_npc_dat[mNpcNo].mMaxBackboneX, l_npc_dat[mNpcNo].mMaxBackboneY,
                   l_npc_dat[mNpcNo].mMinBackboneX, l_npc_dat[mNpcNo].mMinBackboneY,
                   l_npc_dat[mNpcNo].mMaxHeadX, l_npc_dat[mNpcNo].mMaxHeadY,
                   l_npc_dat[mNpcNo].mMinHeadX, l_npc_dat[mNpcNo].mMinHeadY,
                   l_npc_dat[mNpcNo].mMaxTurnStep);

    field_0x7bf = l_npc_dat[mNpcNo].field_0x4a;
    field_0x7c0 = l_npc_dat[mNpcNo].field_0x4b;
    field_0x788 = l_npc_dat[mNpcNo].field_0x20;
    field_0x79a = l_npc_dat[mNpcNo].field_0x28;

    mObjAcch.CrrPos(*dComIfG_Bgsp());

    if (-1000000000.0f != mObjAcch.GetGroundH()) {
        f32 groundH = mObjAcch.GetGroundH();
        home.pos.y = groundH;
        current.pos.y = groundH;
    }

    setMtx();
    mpMorf->getModel()->calc();

    mStts.Init(cc_no, 0xff, this);
    mCyl.Set(dNpc_cyl_src);
    mCyl.SetStts(&mStts);
    setCollision(&mCyl, current.pos, l_npc_dat[mNpcNo].field_0x30, 150.0f);

    return cPhs_COMPLEATE_e;
}

/* 00001468-000014D8       .text _delete__12daNpcBmcon_cFv */
bool daNpcBmcon_c::_delete() {
    if (field_0x7b6 != 0) {
        dComIfG_resDelete(&mPhase2, l_arcname_tbl[mNpcNo]);
    }

    if (heap != NULL && mpMorf != NULL) {
        mpMorf->stopZelAnime();
    }

    return true;
}

/* 000014D8-00001694       .text _draw__12daNpcBmcon_cFv */
bool daNpcBmcon_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModel* armModel = mpArmMorf->getModel();
    J3DModel* headModel = mpHeadModel;

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);
    g_env_light.setLightTevColorType(armModel, &tevStr);
    g_env_light.setLightTevColorType(headModel, &tevStr);

    mpMorf->updateDL();
    mpArmMorf->updateDL();

    headModel->setBaseTRMtx(model->getAnmMtx(m_jnt.getHeadJntNum()));
    mDoExt_modelUpdateDL(mpHeadModel);

    cXyz shadowPos(current.pos.x, 150.0f + current.pos.y, current.pos.z);
    mShadowId = dComIfGd_setShadow(mShadowId, true, mpMorf->getModel(), &shadowPos, 800.0f, 20.0f,
                                    current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);
    if (mShadowId) {
        dComIfGd_addRealShadow(mShadowId, headModel);
        dComIfGd_addRealShadow(mShadowId, armModel);
    }

    switch (mNpcNo) {
    case 0:
        dSnap_RegistFig(DSNAP_TYPE_UNK95, this, 1.0f, 1.0f, 1.0f);
        break;
    case 1:
        dSnap_RegistFig(DSNAP_TYPE_UNK94, this, 1.0f, 1.0f, 1.0f);
        break;
    }

    return true;
}


static cXyz l_bmcon_pos_tbl[2] = {
    cXyz(297080.0f, 1100.0f, -202920.0f),
    cXyz(278900.0f, 1100.0f, -221100.0f),
};

typedef u8 (daNpcBmcon_c::*ExecuteInitProc)();
static ExecuteInitProc l_execute_init[] = {
    &daNpcBmcon_c::executeWaitInit,
    &daNpcBmcon_c::executeTalkInit,
    &daNpcBmcon_c::executeWalkInit,
    &daNpcBmcon_c::executeTurnInit,
};

typedef void (daNpcBmcon_c::*MoveProc_t)();
static MoveProc_t moveProc[] = {
    &daNpcBmcon_c::executeWait,
    &daNpcBmcon_c::executeTalk,
    &daNpcBmcon_c::executeWalk,
    &daNpcBmcon_c::executeTurn,
};

/* 00001694-00001878       .text _execute__12daNpcBmcon_cFv */
bool daNpcBmcon_c::_execute() {
    chkAttention();
    checkOrder();

    if (!dComIfGp_event_runCheck() || eventInfo.checkCommandTalk()) {
        (this->*moveProc[field_0x7b5])();
    } else {
        eventMove();
    }

    eventOrder();
    playAnm();

    if (mAnmIdx == 5) {
        cLib_chaseF(&speedF, field_0x780, 0.3f);

        f32 morfSpeed = speedF * l_npc_dat[mNpcNo].field_0x34;
        if (morfSpeed < 0.5f) {
            morfSpeed = 0.5f;
        }

        mpMorf->setPlaySpeed(morfSpeed);
        mpArmMorf->setPlaySpeed(morfSpeed);
    } else {
        cLib_chaseF(&speedF, field_0x780, 0.1f);
    }

    fopAcM_posMoveF(this, mStts.GetCCMoveP());
    mObjAcch.CrrPos(*dComIfG_Bgsp());
    setCollision(&mCyl, current.pos, l_npc_dat[mNpcNo].field_0x30, 150.0f);

    attention_info.position.set(current.pos.x, current.pos.y + l_npc_dat[mNpcNo].field_0x18, current.pos.z);
    eyePos.set(current.pos.x, current.pos.y + l_npc_dat[mNpcNo].field_0x1c, current.pos.z);

    lookBack();
    setMtx();
    return false;
}

/* 00001878-00001BC8       .text executeCommon__12daNpcBmcon_cFv */
u8 daNpcBmcon_c::executeCommon() {
    if (field_0x7ad != 0 && field_0x7c3 == 0) {
        field_0x7ae = 1;
    } else {
        field_0x7ae = 0;
    }

    if (field_0x7ac == 1 && field_0x7b5 != 1) {
        executeSetMode(1);
    }

    switch (mNpcNo) {
    case 0:
        break;
    case 1:
        s8 layer = dComIfGp_getStartStageLayer();
        if (layer == 2 || layer == 3) {
            cXyz flyDist = calcFlyDist();

            if ((field_0x7c5 & 1) == 0 && flyDist.x > 10.0f) {
                field_0x7c5 |= 1;
                JAIZelBasic::zel_basic->bgmStart(JA_BGM_BIRDMAN, 0, 0);
                JAIZelBasic::zel_basic->seStart(JA_SE_BIRDMAN_TAKE_OFF);
            }

            if (field_0x7c4 == 1) {
                if (flyDist.x > 25710.0f && flyDist.z < 1131.0f && flyDist.z > -1131.0f) {
                    field_0x7c4 = 0;
                    dComIfGp_setItemMagicCount(-dComIfGs_getMaxMagic());
                } else if (flyDist.x < -1948.0f || flyDist.x > 25710.0f || flyDist.z < -3534.0f || flyDist.z > 3534.0f) {
                    field_0x7c4 = 2;
                    dComIfGp_setItemMagicCount(-dComIfGs_getMaxMagic());
                }
            }

            if (dComIfGp_checkPlayerStatus0(0, daPyStts0_SWIM_e)) {
                field_0x7a2 = flyDist.x / 100.0f;
                if (field_0x7a2 < 0) {
                    field_0x7c4 = 3;
                }

                dComIfGs_setTmpReg(dSv_event_tmp_flag_c::UNK_F903, field_0x7c4);

                if (field_0x7c4 == 0 || field_0x7c4 == 1) {
                    field_0x7ae = 3;
                } else {
                    field_0x7ae = 4;
                }

                setFlyDistNow(field_0x7a2);

                if (field_0x7c4 != 2 && field_0x7a2 > getFlyDistMax()) {
                    setFlyDistMax(field_0x7a2);
                }

                old.pos = mPathRun.getPoint(mPathRun.getIdx());
                current.pos = old.pos;
                field_0x79c = -0x6000;
                current.angle.y = -0x6000;

                if ((field_0x7c5 & 2) == 0) {
                    field_0x7c5 |= 2;
                    JAIZelBasic::zel_basic->bgmStop(90, 0);
                }
            }
        }
        break;
    }

    return field_0x7ac;
}

/* 00001BC8-00001C1C       .text executeSetMode__12daNpcBmcon_cFUc */
void daNpcBmcon_c::executeSetMode(u8 i_param_1) {
    field_0x780 = 0.0f;
    field_0x7b5 = (this->*l_execute_init[i_param_1])();
}

/* 00001C1C-00001CC8       .text executeWaitInit__12daNpcBmcon_cFv */
u8 daNpcBmcon_c::executeWaitInit() {
    speedF = 0.0f;

    if (mNpcNo != 0) {
        setAnmTbl(l_npc_anm_wait2);
    } else {
        setAnmTbl(l_npc_anm_wait);
    }

    m_jnt.setParam(l_npc_dat[mNpcNo].mMaxBackboneX, l_npc_dat[mNpcNo].mMaxBackboneY,
                   l_npc_dat[mNpcNo].mMinBackboneX, l_npc_dat[mNpcNo].mMinBackboneY,
                   l_npc_dat[mNpcNo].mMaxHeadX, l_npc_dat[mNpcNo].mMaxHeadY,
                   l_npc_dat[mNpcNo].mMinHeadX, l_npc_dat[mNpcNo].mMinHeadY,
                   l_npc_dat[mNpcNo].mMaxTurnStep);

    return 0;
}

/* 00001CC8-00001D38       .text executeWait__12daNpcBmcon_cFv */
void daNpcBmcon_c::executeWait() {
    if(!executeCommon() && mNpcNo == 1 && field_0x7ad != 0 && !dComIfGs_isEventBit(dSv_event_flag_c::UNK_2A40)) {
        field_0x7ae = 2;
    }
}

/* 00001D38-00001D48       .text executeTalkInit__12daNpcBmcon_cFv */
u8 daNpcBmcon_c::executeTalkInit() {
    field_0x7bf = 1;
    return 1;
}

/* 00001D48-00001E40       .text executeTalk__12daNpcBmcon_cFv */
void daNpcBmcon_c::executeTalk() {
    executeCommon();

    if (talk2(1) == 0x12) {
        field_0x7ac = 0;
        executeSetMode(0);
        field_0x7bf = l_npc_dat[mNpcNo].field_0x4a;
        field_0x7c0 = l_npc_dat[mNpcNo].field_0x4b;

        if (field_0x7c2 != 0) {
            field_0x7c3 = 1;
            field_0x7ae = 0;
            dComIfGp_setNextStage("sea", 1, 14, 2);
        } else {
            g_dComIfG_gameInfo.play.getEvent()->onEventFlag(8);
        }
    } else {
        setAnmFromMsgTag();
    }
}

/* 00001E40-00001E6C       .text executeWalkInit__12daNpcBmcon_cFv */
u8 daNpcBmcon_c::executeWalkInit() {
    setAnmTbl(l_npc_anm_walk);
    return 2;
}

/* 00001E6C-00002050       .text executeWalk__12daNpcBmcon_cFv */
void daNpcBmcon_c::executeWalk() {
    if (executeCommon()) {
        return;
    }

    bool end = false;
    if (mPathRun.chkPointPass(current.pos, mPathRun.getDir()) && !mPathRun.nextIdxAuto()) {
        end = true;
    }

    if (!end) {
        cXyz point = mPathRun.getPoint(mPathRun.getIdx());

        s16 angle;
        dNpc_calc_DisXZ_AngY(current.pos, point, NULL, &angle);

        field_0x7aa = field_0x79c = angle;
        field_0x790 = 0;
        field_0x7a6 = l_npc_dat[mNpcNo].field_0x2c;
        field_0x7be = 2;
        m_jnt.setTrn();
        field_0x780 = l_npc_dat[mNpcNo].field_0x38;
    } else {
        mPathRun.turnDir();

        cXyz point = mPathRun.getPoint(mPathRun.getIdx());

        s16 angle;
        dNpc_calc_DisXZ_AngY(current.pos, point, NULL, &angle);

        field_0x79c = angle;
        mPathRun.setInf(0xff, current.roomNo, 1);
        executeSetMode(0);
    }
}

/* 00002050-00002194       .text executeTurnInit__12daNpcBmcon_cFv */
u8 daNpcBmcon_c::executeTurnInit() {
    cXyz point = mPathRun.getPoint(mPathRun.getIdx());

    s16 angle;
    dNpc_calc_DisXZ_AngY(current.pos, point, NULL, &angle);

    if (angle == current.angle.y) {
        setAnmTbl(l_npc_anm_walk);
        field_0x796 = l_npc_dat[mNpcNo].field_0x44 +
                      cM_rndF(l_npc_dat[mNpcNo].field_0x46 - l_npc_dat[mNpcNo].field_0x44);
        return 2;
    }

    return 3;
}

/* 00002194-00002264       .text executeTurn__12daNpcBmcon_cFv */
void daNpcBmcon_c::executeTurn() {
    if (executeCommon()) {
        return;
    }

    cXyz point = mPathRun.getPoint(mPathRun.getIdx());
    s16 angle;
    dNpc_calc_DisXZ_AngY(current.pos, point, NULL, &angle);
    field_0x7aa = angle;
    field_0x790 = 0;
    field_0x7be = 2;
    m_jnt.setTrn();

    if (current.angle.y == angle) {
        executeSetMode(2);
    }
}

/* 00002264-00002360       .text checkOrder__12daNpcBmcon_cFv */
void daNpcBmcon_c::checkOrder() {
    u16 command = eventInfo.getCommand();
    if (command == 2) {
        if (dComIfGp_evmng_startCheck(field_0x792) && field_0x7ae == 3) {
            field_0x7ae = 0;
        } else if (dComIfGp_evmng_startCheck(field_0x794) && field_0x7ae == 4) {
            field_0x7ae = 0;
        } else if (dComIfGp_evmng_startCheck(field_0x794) && field_0x7ae == 6) {
            field_0x7ae = 0;
        }
    } else if (command == 1) {
        if (field_0x7ae == 2 || field_0x7ae == 1) {
            field_0x7ac = 1;
            executeSetMode(1);
        }
    }
}

/* 00002360-00002444       .text eventOrder__12daNpcBmcon_cFv */
void daNpcBmcon_c::eventOrder() {
    if (field_0x7ae == 2 || field_0x7ae == 1) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        eventInfo.onCondition(dEvtCnd_CANTALKITEM_e);
        if (field_0x7ae == 2) {
            fopAcM_orderSpeakEvent(this);
        }
    }
    else if (field_0x7ae == 3) {
        fopAcM_orderChangeEventId(dComIfGp_getPlayer(0), this, field_0x792, 0, 0xffff);
    }
    else if (field_0x7ae == 4) {
        fopAcM_orderChangeEventId(dComIfGp_getPlayer(0), this, field_0x794, 0, 0xffff);
    }
    else if (field_0x7ae == 6) {
        fopAcM_orderChangeEventId(dComIfGp_getPlayer(0), this, field_0x794, 0, 0xffff);
    }
}

/* 00002444-000024C8       .text eventMove__12daNpcBmcon_cFv */
void daNpcBmcon_c::eventMove() {
    if(!chkEndEvent()) {
        bool temp = mEventCut.getAttnFlag();
        if(mEventCut.cutProc()){
            if(!mEventCut.getAttnFlag()){
                mEventCut.setAttnFlag(temp);
            }
        } else {
            privateCut();
            setAnmFromMsgTag();
        }
    } else {
        executeSetMode(0);
    }
}

/* 000024C8-000025F8       .text privateCut__12daNpcBmcon_cFv */
void daNpcBmcon_c::privateCut() {
    static char* cut_name_tbl[] = {
        "MES_SET",
        "GET_ITEM",
    };

    int staffIdx = dComIfGp_evmng_getMyStaffId(l_npc_staff_id[mNpcNo]);
    if (staffIdx != -1) {
        field_0x7bc = dComIfGp_evmng_getMyActIdx(staffIdx, cut_name_tbl, ARRAY_SIZE(cut_name_tbl), TRUE, 0);
        if (field_0x7bc == -1) {
            dComIfGp_evmng_cutEnd(staffIdx);
            return;
        }

        if (dComIfGp_evmng_getIsAddvance(staffIdx)) {
            switch (field_0x7bc) {
            case 0:
                eventMesSetInit(staffIdx);
                break;
            case 1:
                eventGetItemInit();
                break;
            }
        }

        bool temp;
        switch (field_0x7bc) {
        case 0:
            temp = eventMesSet();
            break;
        default:
            temp = true;
        }

        if (temp) {
            dComIfGp_evmng_cutEnd(staffIdx);
        }
    }
}




/* 000025F8-00002A48       .text eventMesSetInit__12daNpcBmcon_cFi */
void daNpcBmcon_c::eventMesSetInit(int i_staffIdx) {
    int* msg_no_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "MsgNo");
    if (msg_no_p != NULL) {
        mpMsgNo = NULL;
        int msg_no = *msg_no_p;
        switch (msg_no) {
        case 0:
            setMessage(getMsg());
            break;
        case 1:
            switch (field_0x7c4) {
            case 0:
                dComIfGp_setMessageCountNumber(field_0x7a2);
                setMessage(0x2ab2);
                dComIfGp_getVibration().StartShock(8, 1, cXyz(0.0f, 1.0f, 0.0f));
                dComIfGp_getVibration().StartShock(4, 0x1e, cXyz(0.0f, 1.0f, 0.0f));
                JAIZelBasic::zel_basic->seStart(JA_SE_BIRDMAN_GOAL_END);
                break;
            case 1:
                dComIfGp_setMessageCountNumber(field_0x7a2);
                setMessage(0x2ab3);
                dComIfGp_getVibration().StartShock(8, 1, cXyz(0.0f, 1.0f, 0.0f));
                dComIfGp_getVibration().StartShock(4, 0x1e, cXyz(0.0f, 1.0f, 0.0f));
                JAIZelBasic::zel_basic->seStart(JA_SE_BIRDMAN_FALL_END);
                break;
            case 2:
                setMessage(0x2ab0);
                break;
            case 3:
                setMessage(0x2ab5);
                break;
            }
            dComIfGs_onEventBit(0x2901);
            dComIfGs_onTmpBit(0x210);
            break;
        case 2:
            switch (field_0x7c4) {
            case 0:
                setMessage(0x2ab4);
                break;
            case 1:
                dComIfGp_setMessageCountNumber(field_0x7a2);
                setMessage(0x2aa6);
                break;
            case 2:
                setMessage(0x2ab0);
                break;
            case 3:
                setMessage(0x2ab5);
                break;
            }
            break;
        case 10:
            switch (dComIfGs_getTmpReg(0xf903)) {
            case 0:
                if (dComIfGs_isEventBit(0x2b40)) {
                    mpMsgNo = l_msg_bmcon1_goal;
                } else {
                    dComIfGs_onEventBit(0x2b40);
                    mpMsgNo = l_msg_bmcon1_1st_goal;
                }
                break;
            case 1:
                if (dComIfGs_isEventBit(0x2b40)) {
                    setMessage(0x2aaf);
                } else {
                    int idx = getFlyDistNow() * 6 / 256;
                    if (idx > 4) {
                        idx = 4;
                    }
                    setMessage(l_msg_bmcon1_result[idx]);
                }
                break;
            case 2:
                setMessage(0x2ab1);
                break;
            case 3:
                setMessage(0x2ab6);
                break;
            }
            break;
        default:
            setMessage(msg_no);
            break;
        }
        if (mpMsgNo != NULL) {
            setMessage(*mpMsgNo);
        }
    } else {
        mpMsgNo++;
        setMessage(*mpMsgNo);
    }
}

/* 00002A48-00002ADC       .text eventMesSet__12daNpcBmcon_cFv */
bool daNpcBmcon_c::eventMesSet() {
    u16 temp = talk2(0);
    if(temp == fopMsgStts_BOX_CLOSED_e) {
        if(field_0x7b8 & 1) {
            field_0x7b8 &= ~1;
            field_0x78c = 7;
            field_0x7ae = 6;
        } else if (field_0x7b8 & 2) {
            field_0x7b8 &= ~2;
            field_0x78c = 5;
            field_0x7ae = 6;
        }
    }

    return temp == fopMsgStts_BOX_CLOSED_e;
}

/* 00002ADC-00002B38       .text eventGetItemInit__12daNpcBmcon_cFv */
void daNpcBmcon_c::eventGetItemInit() {
    fpc_ProcID item_id = fopAcM_createItemForPresentDemo(&current.pos, field_0x78c, 0, -1, fopAcM_GetRoomNo(this));
    if(item_id != fpcM_ERROR_PROCESS_ID_e) {
        dComIfGp_event_setItemPartnerId(item_id);
    }
}

/* 00002B38-00002C8C       .text talk2__12daNpcBmcon_cFi */
u16 daNpcBmcon_c::talk2(int i_param) {
    u16 status = 0xFF;

    if (mCurrMsgBsPcId == fpcM_ERROR_PROCESS_ID_e) {
        if (i_param == 1) {
            mCurrMsgNo = getMsg();
        }

        mCurrMsgBsPcId = fopMsgM_messageSet(mCurrMsgNo, this);
        mpCurrMsg = NULL;
        field_0x79e = 0xFFFF;
    } else if (mpCurrMsg != NULL) {
        status = mpCurrMsg->mStatus;

        switch (status) {
        case fopMsgStts_MSG_DISPLAYED_e:
            mpCurrMsg->mStatus = next_msgStatus(&mCurrMsgNo);
            if (mpCurrMsg->mStatus == fopMsgStts_MSG_CONTINUES_e) {
                fopMsgM_messageSet(mCurrMsgNo);
            }
            break;
        case 6:
            if (field_0x79e == 0xF || field_0x79e == 2) {
                chkMsg();
            }
            break;
        case 0x12:
            mpCurrMsg->mStatus = 0x13;
            mCurrMsgBsPcId = fpcM_ERROR_PROCESS_ID_e;
            break;
        }

        field_0x79e = status;
        anmAtr(status);
    } else {
        mpCurrMsg = fopMsgM_SearchByID(mCurrMsgBsPcId);
    }

    return status;
}

/* 00002C8C-00002E58       .text next_msgStatus__12daNpcBmcon_cFPUl */
u16 daNpcBmcon_c::next_msgStatus(u32* pMsgNo) {
    u16 ret = fopMsgStts_MSG_CONTINUES_e;

    switch (*pMsgNo) {
    case 0x2ab4:
        *pMsgNo = 0x2aff;
        break;
    default:
        if (mpMsgNo != NULL) {
            mpMsgNo++;

            switch (*mpMsgNo) {
            case 0:
                mpMsgNo = NULL;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 1:
                if (mpCurrMsg->mSelectNum == 0) {
                    if (dComIfGs_getRupee() < dComIfGp_getMessageRupee()) {
                        mpMsgNo = l_msg_bmcon1_not_rupee;
                    } else {
                        mpMsgNo = l_msg_bmcon1_appear;
                        dComIfGp_setItemRupeeCount(-dComIfGp_getMessageRupee());
                    }
                } else {
                    mpMsgNo = l_msg_bmcon1_not_appear;
                }
                *pMsgNo = *mpMsgNo;
                break;
            case 2:
                if (mpCurrMsg->mSelectNum == 0) {
                    if (dComIfGs_getRupee() < dComIfGp_getMessageRupee()) {
                        mpMsgNo = l_msg_bmcon1_not_rupee;
                    } else {
                        mpMsgNo = l_msg_bmcon1_appear2;
                        dComIfGp_setItemRupeeCount(-dComIfGp_getMessageRupee());
                    }
                } else {
                    mpMsgNo = l_msg_bmcon1_not_appear2;
                }
                *pMsgNo = *mpMsgNo;
                break;
            case 3:
                dComIfGp_setItemMagicCount(dComIfGs_getMaxMagic());
                field_0x7c2 = 1;
                mpMsgNo = NULL;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 4:
                field_0x7b8 |= 1;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 5:
                field_0x7b8 |= 2;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            default:
                *pMsgNo = *mpMsgNo;
                break;
            }
        } else {
            ret = fopMsgStts_MSG_ENDS_e;
        }
        break;
    }

    return ret;
}


/* 00002E58-0000301C       .text getMsg__12daNpcBmcon_cFv */
u32 daNpcBmcon_c::getMsg() {
    u32 ret = 0;
    mpMsgNo = 0;
    if (!dComIfGp_event_chkTalkXY()) {
        switch (mNpcNo) {
        case 0:
            if (field_0x7c2 != 0) {
                mpMsgNo = l_msg_bmcon1_talk;
            } else if (!dComIfGs_isEventBit(0x2901)) {
                mpMsgNo = l_msg_bmcon1_1st_appear;
            } else if (isClear()) {
                mpMsgNo = l_msg_bmcon1_cleared;
                dComIfGp_setMessageCountNumber(getFlyDistMax());
            } else {
                dComIfGp_setMessageCountNumber(getFlyDistMax());
                mpMsgNo = l_msg_bmcon1_2nd;
            }
            break;
        case 1:
            if (!dComIfGs_isEventBit(0x2a40)) {
                mpMsgNo = l_msg_bmcon2_2st_talk;
                dComIfGs_onEventBit(0x2a40);
            } else if (isClear()) {
                mpMsgNo = l_msg_bmcon2_cleared;
            } else if (dComIfGs_isEventBit(0x2901)) {
                dComIfGp_setMessageCountNumber(getFlyDistMax());
                mpMsgNo = l_msg_bmcon2_appear;
            } else {
                mpMsgNo = l_msg_bmcon2_talk;
            }
            break;
        }
    }
    if (mpMsgNo != 0) {
        ret = *mpMsgNo;
    }
    return ret;
}

/* 0000301C-00003098       .text chkMsg__12daNpcBmcon_cFv */
void daNpcBmcon_c::chkMsg() {
    switch (mCurrMsgNo) {
    case 0x2ab2:
        mDoAud_bgmStart(JA_BGM_BIRDMAN_GOAL);
        break;
    case 0x2ab3:
        mDoAud_bgmStart(JA_BGM_BIRDMAN_FAIL);
        break;
    }
}

/* 00003098-000030A0       .text setMessage__12daNpcBmcon_cFUl */
void daNpcBmcon_c::setMessage(u32 i_msgNo) {
    mCurrMsgNo = i_msgNo;
}

/* 000030A0-00003158       .text setAnmFromMsgTag__12daNpcBmcon_cFv */
void daNpcBmcon_c::setAnmFromMsgTag() {
    switch (g_dComIfG_gameInfo.play.getBaseAnimeID()) {
    case 0:
        setAnmTbl(l_npc_anm_wait);
        break;
    case 5:
        setAnmTbl(l_npc_anm_wait2);
        break;
    case 6:
        setAnmTbl(l_npc_anm_talk);
        break;
    case 9:
        setAnmTbl(l_npc_anm_talk2);
        break;
    case 14:
        field_0x7c0 = 0;
        setAnmTbl(l_npc_anm_talk3);
        break;
    case 18:
        setAnmTbl(l_npc_anm_con1);
        break;
    case 19:
        setAnmTbl(l_npc_anm_con2);
        break;
    }

    g_dComIfG_gameInfo.play.clearBaseAnimeID();
}

/* 00003158-00003178       .text getPrmNpcNo__12daNpcBmcon_cFv */
u8 daNpcBmcon_c::getPrmNpcNo() {
    s8 no = argument;
    if (no >= 0 && no < 2) {
        return no;
    }
    return 0;
}

/* 00003178-000031A4       .text getPrmRailID__12daNpcBmcon_cFv */
u8 daNpcBmcon_c::getPrmRailID() {
    return daObj::PrmAbstract<Prm_e>(this, PRM_RAILID_W, PRM_RAILID_S);
}

/* 000031A4-0000322C       .text setMtx__12daNpcBmcon_cFv */
void daNpcBmcon_c::setMtx() {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
}

/* 0000322C-0000356C       .text chkAttention__12daNpcBmcon_cFv */
void daNpcBmcon_c::chkAttention() {
    field_0x7c1 = 0;

    if (mEventCut.getAttnFlag()) {
        field_0x76c = mEventCut.getAttnPos();
        field_0x7be = 1;

        if (field_0x7bf != 0) {
            field_0x790 = 0;
            m_jnt.setTrn();
        } else {
            field_0x790 = 1;
        }

        if (field_0x7ad == 0) {
            field_0x7ad = 1;
        }
    } else {
        fopAc_ac_c* player = dComIfGp_getLinkPlayer();
        f32 dist = field_0x788;
        int ang = field_0x79a;

        f32 disXZ;
        s16 angY;
        dNpc_calc_DisXZ_AngY(current.pos, player->current.pos, &disXZ, &angY);

        if (field_0x7ad != 0) {
            dist += 40.0f;
            ang += 0x71c;
        }

        angY -= shape_angle.y;

        if (dist > disXZ && ang > abs(angY) && player->current.pos.y > current.pos.y - 200.0f) {
            field_0x76c = dNpc_playerEyePos(l_npc_dat[mNpcNo].field_0x14);
            field_0x7be = 1;

            if (field_0x7bf != 0) {
                field_0x790 = 0;
            } else {
                field_0x790 = 1;
            }

            if (field_0x7c0 == 0) {
                field_0x7aa = field_0x79c;
                field_0x790 = 0;
                field_0x7be = 2;
                m_jnt.setTrn();
            }

            if (field_0x7ad == 0) {
                field_0x7ad = 1;
            }
        } else {
            if (field_0x7ad == 1) {
                field_0x7ad = 0;
                field_0x798 = l_npc_dat[mNpcNo].field_0x48;
            }

            if (l_npc_dat[mNpcNo].field_0x24 > disXZ) {
                field_0x76c = dNpc_playerEyePos(l_npc_dat[mNpcNo].field_0x14);
                field_0x7be = 1;

                if (field_0x7bf != 0) {
                    field_0x790 = 0;
                } else {
                    field_0x790 = 1;
                }

                if (field_0x7c0 == 0) {
                    field_0x7aa = field_0x79c;
                    field_0x790 = 0;
                    field_0x7be = 2;
                    m_jnt.setTrn();
                }

                field_0x7c1 = 1;
            } else {
                field_0x7be = 0;

                if (field_0x798 != 0) {
                    field_0x798--;
                } else {
                    field_0x7aa = field_0x79c;
                    field_0x790 = 0;
                    field_0x7be = 2;
                    m_jnt.setTrn();
                }
            }
        }
    }

    field_0x7a6 = l_npc_dat[mNpcNo].field_0x2a;
}

/* 0000356C-000036B8       .text lookBack__12daNpcBmcon_cFv */
void daNpcBmcon_c::lookBack() {
    s16 headAngleY = field_0x7a6;
    s16 targetY = current.angle.y;
    cXyz* dstPos_p;
    dstPos_p = NULL;
    cXyz dstPos;
    cXyz pos = eyePos;
    bool headOnlyFollow = field_0x790;

    switch ((s8)field_0x7be) {
        case 0:
            break;
        case 1:
            dstPos = field_0x76c;
            dstPos_p = &dstPos;
            break;
        case 2:
            targetY = field_0x7aa;
            break;
    }

    if (field_0x7ac != 0 && field_0x7bf != 0) {
        headOnlyFollow = 0;
        m_jnt.setTrn();
    }

    if (m_jnt.trnChk()) {
        s16 turnSpeed = mEventCut.getTurnSpeed();
        if (turnSpeed != 0) {
            headAngleY = turnSpeed;
        }
        cLib_addCalcAngleS2(&field_0x7a8, headAngleY, 4, 0x800);
    } else {
        field_0x7a8 = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dstPos_p, pos, targetY, field_0x7a8, headOnlyFollow);

    shape_angle = current.angle;
}

/* 000036B8-00003794       .text playAnm__12daNpcBmcon_cFv */
void daNpcBmcon_c::playAnm() {
    field_0x7ba &= ~1;
    mpArmMorf->play(NULL, 0, 0);

    if (mpMorf->play(NULL, 0, 0)) {
        if (mpAnmDat != NULL) {
            if (field_0x7bb > 0) {
                field_0x7bb--;

                if (!(field_0x7bb != 0)) {
                    mpAnmDat++;

                    if (setAnmTbl(mpAnmDat)) {
                        field_0x7ba |= 1;
                    }
                } else {
                    setAnm(mpAnmDat->mAnmIdx, 0, 0.0f);
                }
            }
        }
    }
}

/* 00003794-000038C0       .text setAnm__12daNpcBmcon_cFUcif */
void daNpcBmcon_c::setAnm(u8 i_bckIdx, int i_mode, float i_speed) {
    if (field_0x784 >= 0.0f) {
        i_speed = field_0x784;
        field_0x784 = -1.0f;
    }

    mpMorf->setAnm((J3DAnmTransform*)dComIfG_getObjectIDRes(l_arcname_tbl[mNpcNo], l_bck_ix_tbl[i_bckIdx]), i_mode, i_speed, 1.0f, 0.0f, -1.0f, NULL);

    mpArmMorf->setAnm((J3DAnmTransform*)dComIfG_getObjectIDRes(l_arcname_tbl[mNpcNo], l_arm_bck_ix_tbl[i_bckIdx]), i_mode, i_speed, 1.0f, 0.0f, -1.0f, NULL);

    mAnmIdx = i_bckIdx;
}

/* 000038C0-00003960       .text setAnmTbl__12daNpcBmcon_cFP12sBmconAnmDat */
u8 daNpcBmcon_c::setAnmTbl(sBmconAnmDat* i_dat) {
    if (i_dat->mAnmIdx == 0xff) {
        mpAnmDat = NULL;
        return 1;
    }

    mpAnmDat = i_dat;
    field_0x7bb = mpAnmDat->mMode;

    int mode = 2;
    if (field_0x7bb > 0) {
        mode = 0;
    }

    if (mAnmIdx != mpAnmDat->mAnmIdx || mode == 0) {
        setAnm(mpAnmDat->mAnmIdx, mode, mpAnmDat->mFrame);
    }

    return 0;
}

/* 00003960-00003968       .text XyCheckCB__12daNpcBmcon_cFi */
s16 daNpcBmcon_c::XyCheckCB(int) {
    return 0;
}

/* 00003968-000039E0       .text setCollision__12daNpcBmcon_cFP8dCcD_Cyl4cXyzff */
void daNpcBmcon_c::setCollision(dCcD_Cyl* cyl, cXyz center, float radius, float height) {
    cyl->SetC(center);
    cyl->SetR(radius);
    cyl->SetH(height);
    dComIfG_Ccsp()->Set(cyl);
}

/* 000039E0-00003A7C       .text calcFlyDist__12daNpcBmcon_cFv */
cXyz daNpcBmcon_c::calcFlyDist() {
    cXyz pos = dComIfGp_getLinkPlayer()->current.pos;
    pos -= l_bmcon_pos_tbl[0];

    mDoMtx_stack_c::YrotS(-0x6000);

    cXyz dist;
    mDoMtx_stack_c::multVec(&pos, &dist);

    return dist;
}

/* 00003A7C-00003AE8       .text getFlyDistMax__12daNpcBmcon_cFv */
s16 daNpcBmcon_c::getFlyDistMax() {
    u8 reg1 = dComIfGs_getEventReg(dSv_event_flag_c::UNK_A7FF);
    u8 reg2 = dComIfGs_getEventReg(dSv_event_flag_c::UNK_A8FF);

    return (reg1 + reg2 * 256);
}


/* 00003AE8-00003B6C       .text setFlyDistMax__12daNpcBmcon_cFs */
void daNpcBmcon_c::setFlyDistMax(s16 i_dist) {
    dComIfGs_setEventReg(dSv_event_flag_c::UNK_A7FF, i_dist % 256);
    dComIfGs_setEventReg(dSv_event_flag_c::UNK_A8FF, i_dist / 256);
}

/* 00003B6C-00003BD8       .text getFlyDistNow__12daNpcBmcon_cFv */
s16 daNpcBmcon_c::getFlyDistNow() {
    u8 lo = dComIfGs_getTmpReg(dSv_event_flag_c::UNK_FBFF);
    u8 hi = dComIfGs_getTmpReg(dSv_event_flag_c::UNK_FAFF);
    return lo + (hi << 8);
}

/* 00003BD8-00003C5C       .text setFlyDistNow__12daNpcBmcon_cFs */
void daNpcBmcon_c::setFlyDistNow(s16 i_dist) {
    dComIfGs_setTmpReg(dSv_event_flag_c::UNK_FBFF, i_dist % 256);
    dComIfGs_setTmpReg(dSv_event_flag_c::UNK_FAFF, i_dist / 256);
}

/* 00003C5C-00003DC4       .text chkEndEvent__12daNpcBmcon_cFv */
BOOL daNpcBmcon_c::chkEndEvent() {
    switch (mNpcNo) {
    case 0:
        if (dComIfGp_evmng_endCheck(field_0x792) &&
            dComIfGs_getTmpReg(dSv_event_flag_c::UNK_F903) != 0) {
            g_dComIfG_gameInfo.play.getEvent()->onEventFlag(8);
            field_0x7c0 = l_npc_dat[mNpcNo].field_0x4b;
            return TRUE;
        }
        if (dComIfGp_evmng_endCheck(field_0x794)) {
            g_dComIfG_gameInfo.play.getEvent()->onEventFlag(8);
            field_0x7c0 = l_npc_dat[mNpcNo].field_0x4b;
            return TRUE;
        }
        break;
    case 1:
        if (dComIfGp_evmng_endCheck(field_0x792) || dComIfGp_evmng_endCheck(field_0x794)) {
            g_dComIfG_gameInfo.play.getEvent()->onEventFlag(8);
            field_0x7c0 = l_npc_dat[mNpcNo].field_0x4b;
            return TRUE;
        }
        break;
    }
    return FALSE;
}

/* 00003DC4-00003DFC       .text isClear__12daNpcBmcon_cFv */
BOOL daNpcBmcon_c::isClear() {
    return dComIfGs_isEventBit(dSv_event_flag_c::UNK_2B40) ? TRUE : FALSE;
}

/* Unreferenced: the REL link strips it, but its assert string stays in @stringBase0 */
BOOL daNpcBmcon_c::initTexPatternAnm(bool i_modify) {
    J3DModelData* modelData = mpHeadModel->getModelData();
    m_head_tex_pattern = (J3DAnmTexPattern*)dComIfG_getObjectRes(m_arcname, l_btp_ix_tbl[0]);
    JUT_ASSERT(0, m_head_tex_pattern != 0);
    if (!mBtpAnm.init(modelData, m_head_tex_pattern, TRUE, J3DFrameCtrl::EMode_LOOP, 1.0f, 0, -1, i_modify, FALSE)) {
        return FALSE;
    }
    return TRUE;
}

/* 00003DFC-00003E1C       .text daNpc_BmconCreate__FPv */
static cPhs_State daNpc_BmconCreate(void* i_this) {
    return ((daNpcBmcon_c*)i_this)->_create();
}

/* 00003E1C-00003E40       .text daNpc_BmconDelete__FPv */
static BOOL daNpc_BmconDelete(void* i_this) {
    return ((daNpcBmcon_c*)i_this)->_delete();
}

/* 00003E40-00003E64       .text daNpc_BmconExecute__FPv */
static BOOL daNpc_BmconExecute(void* i_this) {
    return ((daNpcBmcon_c*)i_this)->_execute();
}

/* 00003E64-00003E88       .text daNpc_BmconDraw__FPv */
static BOOL daNpc_BmconDraw(void* i_this) {
    return ((daNpcBmcon_c*)i_this)->_draw();
}

/* 00003E88-00003E90       .text daNpc_BmconIsDelete__FPv */
static BOOL daNpc_BmconIsDelete(void*) {
    return TRUE;
}

static actor_method_class daNpc_BmconMethodTable = {
    (process_method_func)daNpc_BmconCreate,
    (process_method_func)daNpc_BmconDelete,
    (process_method_func)daNpc_BmconExecute,
    (process_method_func)daNpc_BmconIsDelete,
    (process_method_func)daNpc_BmconDraw,
};

actor_process_profile_definition g_profile_NPC_BMCON1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_BMCON1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpcBmcon_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_BMCON1_e,
    /* Actor SubMtd */ &daNpc_BmconMethodTable,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
