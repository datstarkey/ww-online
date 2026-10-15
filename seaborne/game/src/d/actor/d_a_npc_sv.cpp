/**
 * d_a_npc_sv.cpp
 * NPC - Salvage Corp group members
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_npc_sv.h"
#include "m_Do/m_Do_ext.h"
#include "d/actor/d_a_obj_ikada.h"

#include "d/d_com_inf_game.h"
#include "res/Object/Sv.h"
#include "d/d_com_lib_game.h"

const char daNpcSv_c::m_arcname[] = "Sv";

static const int l_bmd_ix_tbl[] = {
    dRes_ID_SV_BDL_SV_e,
    dRes_ID_SV_BDL_SV_e,
    dRes_ID_SV_BDL_SV_e,
    dRes_ID_SV_BDL_SV_e,
};

static const int l_bck_ix_tbl[4][6] = {
    { dRes_ID_SV_BCK_SV_WAIT01_e, dRes_ID_SV_BCK_SV_TALK01_e, dRes_ID_SV_BCK_SV_TALK02_e,
      dRes_ID_SV_BCK_SV_TALK03_e, dRes_ID_SV_BCK_SV_YA_e,     dRes_ID_SV_BCK_SV_MAWASI_e },
    { dRes_ID_SV_BCK_SV_WAIT02_e, dRes_ID_SV_BCK_SV_KYORO_e,  dRes_ID_SV_BCK_SV_UNAZUKU_e,
      dRes_ID_SV_BCK_SV_HARAU_e,  dRes_ID_SV_BCK_SV_BYE_e,    dRes_ID_SV_BCK_SV_SHI_e },
    { dRes_ID_SV_BCK_SV_WAIT02_e, dRes_ID_SV_BCK_SV_KYORO_e,  dRes_ID_SV_BCK_SV_UNAZUKU_e,
      dRes_ID_SV_BCK_SV_HARAU_e,  dRes_ID_SV_BCK_SV_BYE_e,    dRes_ID_SV_BCK_SV_SHI_e },
    { dRes_ID_SV_BCK_SV_WAIT02_e, dRes_ID_SV_BCK_SV_KYORO_e,  dRes_ID_SV_BCK_SV_UNAZUKU_e,
      dRes_ID_SV_BCK_SV_HARAU_e,  dRes_ID_SV_BCK_SV_BYE_e,    dRes_ID_SV_BCK_SV_SHI_e },
};

static char* l_npc_staff_id[] = {
    "Sv0",
    "Sv1",
    "Sv2",
    "Sv3",
};

static const char* l_arcname_tbl[] = {
    "Sv",
    "Sv",
    "Sv",
    "Sv",
};

static sSvAnmDat l_npc_anm_wait[] = { { 0, 8, 0xFF } };
static sSvAnmDat l_npc_anm_talk[] = { { 1, 8, 0xFF } };
static sSvAnmDat l_npc_anm_talk2[] = { { 2, 8, 0xFF } };
static sSvAnmDat l_npc_anm_talk3[] = { { 3, 8, 0xFF } };
static sSvAnmDat l_npc_anm_ya1[] = { { 4, 8, 1 }, { 0, 8, 0xFF } };
static sSvAnmDat l_npc_anm_ya2[] = { { 4, 8, 1 }, { 1, 8, 0xFF } };
static sSvAnmDat l_npc_anm_mawasi[] = { { 5, 0, 0xFF } };
static sSvAnmDat l_npc_anm_kyoro[] = { { 1, 8, 0xFF } };
static sSvAnmDat l_npc_anm_unun[] = { { 2, 8, 1 }, { 0xFF, 8, 0 } };
static sSvAnmDat l_npc_anm_sisi[] = { { 3, 8, 1 }, { 0xFF, 8, 0 } };
static sSvAnmDat l_npc_anm_byebye[] = { { 4, 8, 1 }, { 0xFF, 8, 0 } };
static sSvAnmDat l_npc_anm_shi[] = { { 5, 8, 1 }, { 0xFF, 8, 0 } };

static sSvAnmDat* l_npc_anm_sv1_tbl[] = {
    l_npc_anm_wait,
    l_npc_anm_kyoro,
    l_npc_anm_unun,
    l_npc_anm_sisi,
    l_npc_anm_byebye,
    l_npc_anm_shi,
};

static u32 l_npc_se_sv1_tbl[] = {
    0,
    0,
    0x4927,
    0x4929,
    0x4928,
    0,
};

struct sSvNpcDat {
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
    /* 0x28 */ f32 field_0x28;
    /* 0x2C */ f32 field_0x2c;
    /* 0x30 */ f32 field_0x30;
    /* 0x34 */ s16 field_0x34;
    /* 0x36 */ s16 field_0x36;
    /* 0x38 */ f32 field_0x38;
    /* 0x3C */ f32 field_0x3c;
    /* 0x40 */ s16 field_0x40;
    /* 0x42 */ u8 field_0x42;
    /* 0x43 */ u8 field_0x43;
}; // Size: 0x44

static sSvNpcDat l_npc_dat[] = {
    { 4000, 6000, 0, 8000, 0, -6000, -3000, -8000, 1000, {0},
      0.0f, -5.0f, 0.0f, 0.0f, 185.0f, 150.0f, 600.0f, 1500.0f,
      0x7FFF, 1000, 60.0f, 150.0f, 30, 1, 1 },
    { 4096, 0, 0, 0, -4096, 0, 0, 0, 2048, {0},
      0.0f, 0.0f, 0.0f, 0.0f, 180.0f, 150.0f, 0.0f, 0.0f,
      0x7FFF, 0x800, 60.0f, 150.0f, 30, 0, 1 },
    { 4096, 0, 0, 0, -4096, 0, 0, 0, 2048, {0},
      0.0f, 0.0f, 0.0f, 0.0f, 180.0f, 150.0f, 0.0f, 0.0f,
      0x7FFF, 0x800, 60.0f, 150.0f, 30, 0, 1 },
    { 4096, 0, 0, 0, -4096, 0, 0, 0, 2048, {0},
      0.0f, 0.0f, 0.0f, 0.0f, 180.0f, 150.0f, 0.0f, 0.0f,
      0x4000, 0x800, 60.0f, 150.0f, 30, 0, 1 },
};

static u32 l_msg_sv_p1_1st_talk1[] = { 0x34BD, 0x34BE, 0x34BF, 0x34C0, 2, 0x34C1, 0x34C2, 0x34C3, 1, 0x34C4, 0x34E8, 2, 0x34C5, 4, 0 };
static u32 l_msg_sv_p1_1st_talk2[] = { 0x34BD, 0x34BE, 0x34BF, 0x34C0, 2, 0x34C1, 0x34C2, 0x34C3, 1, 0x34C6, 0x34E9, 2, 0x34C7, 4, 0 };
static u32 l_msg_sv_p1_2nd_talk1[] = { 0x34C8, 0x34C9, 0x34EA, 2, 0x34CA, 4, 0 };
static u32 l_msg_sv_p1_2nd_talk2[] = { 0x34C8, 0x34CB, 2, 0x34CC, 4, 0 };
static u32 l_msg_sv_p1_3rd_talk1[] = { 0x34CD, 0x34D0, 2, 0x34D1, 3, 0 };
static u32 l_msg_sv_p1_3rd_talk2[] = { 0x34CD, 0x34CE, 2, 0x34CF, 3, 0 };
static u32 l_msg_sv_p2_1st_talk[] = { 0x34D9, 0x34DA, 0x34DB, 2, 0x34DC, 4, 0 };
static u32 l_msg_sv_p2_2nd_talk[] = { 0x34DD, 0x34DE, 2, 0x34DF, 3, 0 };
static u32 l_msg_sv_p3_1st_talk[] = { 0x34D2, 0x34D3, 0x34D4, 2, 0x34D5, 4, 0 };
static u32 l_msg_sv_p3_2nd_talk[] = { 0x34D6, 0x34D7, 2, 0x34D8, 3, 0 };
static u32 l_msg_sv_p4_1st_talk[] = { 0x34E0, 0x34E1, 0x34E2, 0x34E3, 0x34E4, 5, 0 };
static u32 l_msg_sv_p4_2nd_talk[] = { 0x34E5, 0x34E6, 0x34E7, 5, 0 };

static u32* l_msg_sv_talk_tbl[] = {
    l_msg_sv_p1_1st_talk1,
    l_msg_sv_p1_1st_talk2,
    l_msg_sv_p1_2nd_talk1,
    l_msg_sv_p1_2nd_talk2,
    l_msg_sv_p1_3rd_talk1,
    l_msg_sv_p1_3rd_talk2,
    l_msg_sv_p2_1st_talk,
    l_msg_sv_p2_2nd_talk,
    l_msg_sv_p3_1st_talk,
    l_msg_sv_p3_2nd_talk,
    l_msg_sv_p4_1st_talk,
    l_msg_sv_p4_2nd_talk,
};

static u16 l_msg_sv_flag_tbl[] = {
    1, 1, 2, 2, 0, 0, 4, 0, 8, 0, 0x10, 0,
};

static s16 l_event_id_tbl[] = {
    0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2,
};

static u32 l_get_item_no[] = { 0xE5 };

/* 00000078-0000021C       .text __ct__9daNpcSv_cFv */
daNpcSv_c::daNpcSv_c() {
    field_0x73b = getPrmNpcNo();
    field_0x73a = 0;
    field_0x739 = 0;
    field_0x70c = -1.0f;
    field_0x741 = 0;
    field_0x71c = 1;
    field_0x742 = 1;
    field_0x728 = home.angle.y;
    field_0x73d = 0;
    field_0x734 = 0;
    field_0x718 = 0;

    if (dComIfGs_checkGetItem(dItemNo_COLLECT_MAP_26_e)) {
        field_0x734 |= 0x20;
    }
}

/* 00000528-00000640       .text daNpc_People_nodeCallBack__FP7J3DNodei */
static BOOL daNpc_People_nodeCallBack(J3DNode* node, int calcTiming) {
    if(calcTiming == J3DNodeCBCalcTiming_In) {
        J3DJoint* joint = (J3DJoint*)node;
        J3DModel* model = j3dSys.getModel();
        daNpcSv_c* i_this = (daNpcSv_c*)model->getUserArea();

        s32 jntNo = joint->getJntNo();
        MTXCopy(model->getAnmMtx(jntNo), *calc_mtx);

        if(jntNo == i_this->m_jnt.getHeadJntNum()) {
            cMtx_XrotM(*calc_mtx, (s16)i_this->m_jnt.getHead_y());
            cMtx_ZrotM(*calc_mtx, (s16)-i_this->m_jnt.getHead_x());
        }
        if(jntNo == i_this->m_jnt.getBackboneJntNum()) {
            cMtx_XrotM(*calc_mtx, (s16)i_this->m_jnt.getBackbone_y());
            cMtx_ZrotM(*calc_mtx, (s16)-i_this->m_jnt.getBackbone_x());
        }

        model->setAnmMtx(jntNo, *calc_mtx);
        MTXCopy(*calc_mtx, j3dSys.mCurrentMtx);
    }

    return true;
}

/* 00000640-00000660       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpcSv_c*)i_this)->createHeap();
}

/* 00000660-000006B8       .text phase_1__FP9daNpcSv_c */
static cPhs_State phase_1(daNpcSv_c* i_this) {
    fopAcM_ct_Retail(i_this, daNpcSv_c);

    i_this->field_0x73a = 1;
    return cPhs_NEXT_e;
}

/* 000006B8-00000738       .text phase_2__FP9daNpcSv_c */
static cPhs_State phase_2(daNpcSv_c* i_this) {
    cPhs_State phase = dComIfG_resLoad(&i_this->mPhase2, l_arcname_tbl[i_this->field_0x73b]);

    if (phase == cPhs_COMPLEATE_e) {
        if (fopAcM_entrySolidHeap(i_this, CheckCreateHeap, 0x3800)) {
            return i_this->createInit();
        }

        i_this->mpMorf = NULL;
        return cPhs_ERROR_e;
    }

    return phase;
}

/* 00000738-00000768       .text _create__9daNpcSv_cFv */
cPhs_State daNpcSv_c::_create() {
    static cPhs__Handler l_method[] = {
        (cPhs__Handler)phase_1,
        (cPhs__Handler)phase_2,
        NULL,
    };

    return dComLbG_PhaseHandler(&mPhase, l_method, this);
}

/* 00000768-00000A04       .text createHeap__9daNpcSv_cFv */
BOOL daNpcSv_c::createHeap() {
    J3DModelData* modelData = (J3DModelData*)dComIfG_getObjectIDRes(l_arcname_tbl[field_0x73b],
                                                                    l_bmd_ix_tbl[field_0x73b]);

    mpMorf = new mDoExt_McaMorf(
        modelData, NULL, NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes(l_arcname_tbl[field_0x73b],
                                                 l_bck_ix_tbl[field_0x73b][field_0x73d]),
        2, 1.0f, 0, -1, 1, NULL, 0x80000, 0x37441422);

    if (mpMorf == NULL || mpMorf->getModel() == NULL) {
        return FALSE;
    }

    m_jnt.setHeadJntNum(modelData->getJointName()->getIndex("head"));
    JUT_ASSERT(946, m_jnt.getHeadJntNum() >= 0);
    m_jnt.setBackboneJntNum(modelData->getJointName()->getIndex("backbone"));
    JUT_ASSERT(950, m_jnt.getBackboneJntNum() >= 0);

    for (u16 i = 0; i < modelData->getJointNum(); i++) {
        if (i == m_jnt.getHeadJntNum() || i == m_jnt.getBackboneJntNum()) {
            modelData->getJointNodePointer(i)->setCallBack(daNpc_People_nodeCallBack);
        }
    }

#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif
    mAcchCir.SetWall(30.0f, 30.0f);
    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir,
                 fopAcM_GetSpeed_p(this), fopAcM_GetAngle_p(this), fopAcM_GetShapeAngle_p(this));

    return TRUE;
}

/* 00000A04-00000C74       .text createInit__9daNpcSv_cFv */
cPhs_State daNpcSv_c::createInit() {
    gravity = -9.0f;
    setAnmTbl(l_npc_anm_wait);

    field_0x71e[0] = dComIfGp_evmng_getEventIdx("SV_TALK_P1_1ST");
    field_0x71e[1] = dComIfGp_evmng_getEventIdx("SV_TALK_P1_2ND");
    field_0x71e[2] = dComIfGp_evmng_getEventIdx("SV_TALK_P4_1ST");

    eventInfo.setEventId(field_0x71e[l_event_id_tbl[getTalkNo()]]);
    mEventCut.setActorInfo2(l_npc_staff_id[field_0x73b], this);

    field_0x730 = 0;
    field_0x736 = 0;
    field_0x737 = 0;
    field_0x745 = 0;

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -70.0f, 0.0f, -70.0f, 70.0f, 200.0f, 70.0f);

    attention_info.distances[1] = 0xb1;
    attention_info.distances[3] = 0xb1;
    attention_info.flags = 0xa;

    m_jnt.setParam(l_npc_dat[field_0x73b].mMaxBackboneX, l_npc_dat[field_0x73b].mMaxBackboneY,
                   l_npc_dat[field_0x73b].mMinBackboneX, l_npc_dat[field_0x73b].mMinBackboneY,
                   l_npc_dat[field_0x73b].mMaxHeadX, l_npc_dat[field_0x73b].mMaxHeadY,
                   l_npc_dat[field_0x73b].mMinHeadX, l_npc_dat[field_0x73b].mMinHeadY,
                   l_npc_dat[field_0x73b].mMaxTurnStep);

    field_0x743 = l_npc_dat[field_0x73b].field_0x42;
    field_0x744 = l_npc_dat[field_0x73b].field_0x43;
    field_0x710 = l_npc_dat[field_0x73b].field_0x2c;
    field_0x726 = l_npc_dat[field_0x73b].field_0x34;

    setMtx();
    mpMorf->getModel()->calc();

    mStts.Init(0xff, 0xff, this);
    mCyl.Set(dNpc_cyl_src);
    mCyl.SetStts(&mStts);
    setCollision(&mCyl, current.pos, l_npc_dat[field_0x73b].field_0x38,
                 l_npc_dat[field_0x73b].field_0x3c);

    return cPhs_COMPLEATE_e;
}

/* 00000C74-00000CD8       .text _delete__9daNpcSv_cFv */
bool daNpcSv_c::_delete() {
    dComIfG_resDelete(&mPhase2, l_arcname_tbl[field_0x73b]);

    if (heap != NULL && mpMorf != NULL) {
        mpMorf->stopZelAnime();
    }

    return true;
}

/* 00000CD8-00000DAC       .text _draw__9daNpcSv_cFv */
bool daNpcSv_c::_draw() {
    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(mpMorf->getModel(), &tevStr);
    mpMorf->updateDL();

    cXyz shadowPos(current.pos.x, current.pos.y + 150.0f, current.pos.z);
    field_0x6d4 = dComIfGd_setShadow(
        field_0x6d4, 1, mpMorf->getModel(), &shadowPos, 800.0f, 20.0f,
        current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr
    );

    return true;
}




typedef u8 (daNpcSv_c::*ExecuteInitProc)();
static ExecuteInitProc l_execute_init[] = {
    &daNpcSv_c::executeWaitInit,
    &daNpcSv_c::executeTalkInit,
};

typedef void (daNpcSv_c::*MoveProc_t)();
static MoveProc_t moveProc[] = {
    &daNpcSv_c::executeWait,
    &daNpcSv_c::executeTalk,
};

/* 00000DAC-00000F90       .text _execute__9daNpcSv_cFv */
bool daNpcSv_c::_execute() {
    daObj_Ikada_c* target;
    fopAcM_SearchByID(parentActorID, (fopAc_ac_c**)&target);

    chkAttention();
    checkOrder();

    if (!dComIfGp_event_runCheck()) {
        (this->*moveProc[field_0x739])();
    } else {
        eventMove();
    }

    eventOrder();

    if (field_0x73b == 0 && field_0x73d == 5) {
        mpMorf->setFrame((f32)field_0x718);
    } else {
        playAnm();
    }

    if (target != NULL) {
        field_0x718 = target->m1154;
    }

    mObjAcch.CrrPos(*dComIfG_Bgsp());
    setCollision(&mCyl, current.pos, l_npc_dat[field_0x73b].field_0x38, l_npc_dat[field_0x73b].field_0x3c);

    attention_info.position.set(current.pos.x, current.pos.y + l_npc_dat[field_0x73b].field_0x24, current.pos.z);
    eyePos.set(current.pos.x, current.pos.y + l_npc_dat[field_0x73b].field_0x28, current.pos.z);

    lookBack();
    setMtx();
    return false;
}

/* 00000F90-00000FB8       .text executeCommon__9daNpcSv_cFv */
u8 daNpcSv_c::executeCommon() {
    if (field_0x737 != 0) {
        field_0x738 = 1;
    } else {
        field_0x738 = 0;
    }

    return 0;
}

/* 00000FB8-00001000       .text executeSetMode__9daNpcSv_cFUc */
void daNpcSv_c::executeSetMode(u8 param_1) {
    field_0x739 = (this->*l_execute_init[param_1])();
}

/* 00001000-00001080       .text executeWaitInit__9daNpcSv_cFv */
u8 daNpcSv_c::executeWaitInit() {
    setAnmTbl(l_npc_anm_wait);

    m_jnt.setParam(l_npc_dat[field_0x73b].mMaxBackboneX, l_npc_dat[field_0x73b].mMaxBackboneY,
                   l_npc_dat[field_0x73b].mMinBackboneX, l_npc_dat[field_0x73b].mMinBackboneY,
                   l_npc_dat[field_0x73b].mMaxHeadX, l_npc_dat[field_0x73b].mMaxHeadY,
                   l_npc_dat[field_0x73b].mMinHeadX, l_npc_dat[field_0x73b].mMinHeadY,
                   l_npc_dat[field_0x73b].mMaxTurnStep);

    return 0;
}


/* 00001080-0000131C       .text executeWait__9daNpcSv_cFv */
void daNpcSv_c::executeWait() {
    daObj_Ikada_c* svActor;
    fopAcM_SearchByID(parentActorID, (fopAc_ac_c**)&svActor);
    if (svActor == NULL) {
        return;
    }

    if (field_0x73b == 0) {
        if (field_0x745 != 0) {
            field_0x738 = 1;
            if (field_0x745 != field_0x74a) {
                field_0x749 = 1;
            }
            svActor->m1166 = 1;
        } else {
            field_0x738 = 0;
            field_0x749 = 0;
            svActor->m1166 = 0;
        }

        if (field_0x749 != 0) {
            setAnmTbl(l_npc_anm_wait);
        } else {
            field_0x732 = svActor->shape_angle.y;
            current.angle.x = svActor->shape_angle.x;
            current.angle.z = svActor->shape_angle.z;
            field_0x741 = 2;
            field_0x71c = 0;
            m_jnt.setTrn();
            setAnmTbl(l_npc_anm_mawasi);
        }

        field_0x74a = field_0x745;
        return;
    }

    field_0x743 = 1;

    if (svActor->mCurMode == 8 || svActor->mCurMode == 9 || svActor->mCurMode == 5) {
        field_0x732 = svActor->shape_angle.y;
        field_0x741 = 2;
        field_0x71c = 0;
        m_jnt.setTrn();
    } else if (svActor->mCurMode == 4 || svActor->mCurMode == 6) {
        if (svActor->m1168 == 0) {
            field_0x732 = svActor->shape_angle.y + 0x4000;
        } else {
            field_0x732 = svActor->shape_angle.y - 0x4000;
        }
        field_0x741 = 2;
        field_0x71c = 0;
        m_jnt.setTrn();
    } else {
        s16 angle;
        if (svActor->m1168 == 0) {
            angle = svActor->shape_angle.y + 0x4000;
        } else {
            angle = svActor->shape_angle.y - 0x4000;
        }

        if (current.angle.y == angle) {
            field_0x6ec = svActor->getCranePos();
            field_0x741 = 1;
            field_0x71c = 1;
            field_0x743 = 0;
        } else {
            field_0x732 = angle;
            field_0x741 = 2;
            field_0x71c = 0;
            m_jnt.setTrn();
        }
    }

    if (svActor->mCurMode == 7) {
        if (field_0x748 == 0) {
            field_0x748 = 1;
            setAnmTbl(l_npc_anm_sv1_tbl[1]);
        }
    } else {
        if (field_0x748 == 1) {
            field_0x748 = 0;
            setAnmTbl(l_npc_anm_sv1_tbl[0]);
        }
    }
}

/* 0000131C-00001338       .text executeTalkInit__9daNpcSv_cFv */
u8 daNpcSv_c::executeTalkInit() {
    if (field_0x742 != 0) {
        return 1;
    }

    return field_0x739;
}

/* 00001338-000013B8       .text executeTalk__9daNpcSv_cFv */
void daNpcSv_c::executeTalk() {
    executeCommon();
    if(talk2(1, this) == fopMsgStts_BOX_CLOSED_e) {
        field_0x736 = 0;
        field_0x749 = 0;
        executeSetMode(0);
        dComIfGp_event_reset();
    } else {
        setAnmFromMsgTag();
    }
}

/* 000013B8-000013EC       .text checkOrder__9daNpcSv_cFv */
void daNpcSv_c::checkOrder() {
    if(eventInfo.checkCommandDemoAccrpt()) {
        return;
    }

    if(!eventInfo.checkCommandTalk()) {
        return;
    }

    if(field_0x738 != 2 && field_0x738 != 1) {
        return;
    }

    field_0x736 = 1;
}

/* 000013EC-00001458       .text eventOrder__9daNpcSv_cFv */
void daNpcSv_c::eventOrder() {
    if ((field_0x738 == 2 || field_0x738 == 1) && isTalkOK()) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x738 == 2) {
            fopAcM_orderSpeakEvent(this);
        }
    }
}

/* 00001458-00001570       .text eventMove__9daNpcSv_cFv */
void daNpcSv_c::eventMove() {
    if (dComIfGp_evmng_endCheck(field_0x71e[0]) || dComIfGp_evmng_endCheck(field_0x71e[1])
        || dComIfGp_evmng_endCheck(field_0x71e[2])) {
        eventInfo.setEventId(field_0x71e[l_event_id_tbl[getTalkNo()]]);
        dComIfGp_event_reset();
        field_0x73c &= ~2;
        field_0x736 = 0;
        field_0x749 = 0;
        executeSetMode(0);
    } else {
        bool attnFlag = mEventCut.getAttnFlag();
        if (mEventCut.cutProc()) {
            if (!mEventCut.getAttnFlag()) {
                mEventCut.setAttnFlag(attnFlag);
            }
        } else {
            privateCut();
            if (!field_0x73b) {
                setAnmFromMsgTag();
            }
        }
    }
}

#include "d/d_com_inf_game.h"


/* 00001570-00001744       .text privateCut__9daNpcSv_cFv */
void daNpcSv_c::privateCut() {
    static char* cut_name_tbl[] = {
        "MES_SET",
        "GET_ITEM",
        "SET_ANGLE",
        "ATTENTION",
        "TURN_OK",
    };

    int staffIdx = dComIfGp_evmng_getMyStaffId(l_npc_staff_id[field_0x73b]);
    if (staffIdx != -1) {
        field_0x740 = dComIfGp_evmng_getMyActIdx(staffIdx, cut_name_tbl, ARRAY_SIZE(cut_name_tbl), TRUE, 0);
        if (field_0x740 == -1) {
            dComIfGp_evmng_cutEnd(staffIdx);
        } else {
            if (dComIfGp_evmng_getIsAddvance(staffIdx)) {
                switch (field_0x740) {
                    case 0:
                        eventMesSetInit(staffIdx);
                        break;
                    case 1:
                        eventGetItemInit(staffIdx);
                        break;
                    case 2:
                        eventSetAngleInit();
                        break;
                    case 3:
                        eventAttentionInit(staffIdx);
                        break;
                    case 4:
                        eventTurnOkInit();
                        break;
                }
            }

            bool end;
            switch (field_0x740) {
                case 0:
                    end = eventMesSet();
                    break;
                case 1:
                    end = eventGetItem();
                    break;
                case 3:
                    end = eventAttention();
                    break;
                default:
                    end = true;
                    break;
            }

            if (end) {
                dComIfGp_evmng_cutEnd(staffIdx);
            }
        }
    }

    if (field_0x73c & 2) {
        field_0x732 = field_0x72c;
        field_0x741 = 2;
        field_0x71c = 0;
        m_jnt.setTrn();
    }
}

/* 00001744-00001814       .text eventMesSetInit__9daNpcSv_cFi */
void daNpcSv_c::eventMesSetInit(int i_staffIdx) {
    int* msg_no_p = dComIfGp_evmng_getMyIntegerP(i_staffIdx, "MsgNo");

    if (msg_no_p != NULL) {
        mpMsgNo = NULL;

        switch (*msg_no_p) {
        case 0:
            setMessage(getMsg());
            break;
        default:
            setMessage(*msg_no_p);
            break;
        }
    } else {
        mpMsgNo++;
        setMessage(*mpMsgNo);
    }

    g_dComIfG_gameInfo.play.getEvent()->setPtT(this);
}

/* 00001814-0000184C       .text eventMesSet__9daNpcSv_cFv */
bool daNpcSv_c::eventMesSet() {
    return talk2(0, this) == fopMsgStts_BOX_CLOSED_e;
}

/* 0000184C-000018F0       .text eventGetItemInit__9daNpcSv_cFi */
void daNpcSv_c::eventGetItemInit(int staffIdx) {
    int* pItemNo = dComIfGp_evmng_getMyIntegerP(staffIdx, "ItemNo");

    u32 itemNo;
    if (pItemNo != NULL) {
        itemNo = l_get_item_no[*pItemNo];
    } else {
        itemNo = field_0x714;
    }

    fpc_ProcID itemProcId = fopAcM_createItemForPresentDemo(
        &current.pos, itemNo, 0, -1, fopAcM_GetRoomNo(this)
    );

    if (itemProcId != fpcM_ERROR_PROCESS_ID_e) {
        dComIfGp_event_setItemPartnerId(itemProcId);
    }
}

/* 000018F0-000018F8       .text eventGetItem__9daNpcSv_cFv */
bool daNpcSv_c::eventGetItem() {
    return true;
}

/* 000018F8-000019E0       .text eventSetAngleInit__9daNpcSv_cFv */
void daNpcSv_c::eventSetAngleInit() {
    fopAc_ac_c* actor;
    fopAcM_SearchByID(parentActorID, &actor);

    if (actor != NULL) {
        fopAc_ac_c* player = dComIfGp_getLinkPlayer();

        s16 angle;
        dNpc_calc_DisXZ_AngY(actor->current.pos, player->current.pos, NULL, &angle);

        angle -= actor->shape_angle.y;
        if (angle > 0) {
            field_0x72c = actor->shape_angle.y + 0x4000;
        } else {
            field_0x72c = actor->shape_angle.y - 0x4000;
        }

        field_0x728 = field_0x72c;
    } else {
        field_0x72c = field_0x728;
    }

    field_0x73c |= 2;
}

/* 000019E0-00001A74       .text eventAttentionInit__9daNpcSv_cFi */
void daNpcSv_c::eventAttentionInit(int staffIdx) {
    int* pTimer = dComIfGp_evmng_getMyIntegerP(staffIdx, "Timer");

    if (pTimer != NULL) {
        field_0x72a = *pTimer;
    } else {
        field_0x72a = 12;
    }

    field_0x748 = 0;

    if (field_0x73b == 2) {
        dComIfGp_event_setTalkPartner(this);
    }
}

/* 00001A74-00001BC8       .text eventAttention__9daNpcSv_cFv */
bool daNpcSv_c::eventAttention() {
    if (field_0x72a != 0) {
        field_0x72a--;

        if (field_0x72a == 0) {
            daNpcSv_c* sv = (daNpcSv_c*)fopAcM_searchFromName(l_npc_staff_id[0], 0, 0);

            if (sv != NULL) {
                setAnmTbl(l_npc_anm_sv1_tbl[sv->field_0x747]);

                if (field_0x73b == 1) {
                    JAIZelBasic::zel_basic->seStart(l_npc_se_sv1_tbl[sv->field_0x747], NULL, 0, 0,
                                                    1.0f, 1.0f, -1.0f, -1.0f, 0);
                }
            } else {
                setAnmTbl(l_npc_anm_sv1_tbl[2]);
                JAIZelBasic::zel_basic->seStart(l_npc_se_sv1_tbl[2], NULL, 0, 0, 1.0f, 1.0f, -1.0f,
                                                -1.0f, 0);
            }
        }
    } else if (field_0x73e & 1) {
        setAnmTbl(l_npc_anm_sv1_tbl[0]);
        return true;
    }

    return false;
}

/* 00001BC8-00001BD4       .text eventTurnOkInit__9daNpcSv_cFv */
void daNpcSv_c::eventTurnOkInit() {
    field_0x743 = 0x1;
}

/* 00001BD4-00001CF0       .text talk2__9daNpcSv_cFiP10fopAc_ac_c */
u16 daNpcSv_c::talk2(int param_1, fopAc_ac_c* param_2) {
    u16 status = 0xFF;

    if (mCurrMsgBsPcId == fpcM_ERROR_PROCESS_ID_e) {
        if (param_1 == 1) {
            mCurrMsgNo = getMsg();
        }

        mCurrMsgBsPcId = fopMsgM_messageSet(mCurrMsgNo, param_2);
        mpCurrMsg = NULL;
    } else if (mpCurrMsg) {
        status = mpCurrMsg->mStatus;

        switch (status) {
        case fopMsgStts_MSG_DISPLAYED_e:
            mpCurrMsg->mStatus = next_msgStatus(&mCurrMsgNo);
            if (mpCurrMsg->mStatus == fopMsgStts_MSG_CONTINUES_e) {
                fopMsgM_messageSet(mCurrMsgNo);
            }
            break;
        case fopMsgStts_BOX_CLOSED_e:
            mpCurrMsg->mStatus = fopMsgStts_MSG_DESTROYED_e;
            mCurrMsgBsPcId = fpcM_ERROR_PROCESS_ID_e;
            break;
        }

        anmAtr(status);
    } else {
        mpCurrMsg = fopMsgM_SearchByID(mCurrMsgBsPcId);
    }

    return status;
}

/* 00001CF0-00001DC8       .text next_msgStatus__9daNpcSv_cFPUl */
u16 daNpcSv_c::next_msgStatus(u32* pMsgNo) {
    u16 ret = fopMsgStts_MSG_CONTINUES_e;

    switch (*pMsgNo) {
    case 0:
        ret = fopMsgStts_MSG_ENDS_e;
        break;
    default:
        if (mpMsgNo != NULL) {
            mpMsgNo++;

            switch (*mpMsgNo) {
            case 1:
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 2:
                field_0x747 = 2;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 3:
                field_0x747 = 3;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 4:
                field_0x747 = 4;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 5:
                field_0x747 = 5;
                ret = fopMsgStts_MSG_ENDS_e;
                break;
            case 0:
                mpMsgNo = NULL;
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

/* 00001DC8-00001EB4       .text getMsg__9daNpcSv_cFv */
u32 daNpcSv_c::getMsg() {
    u32 ret = 0;
    mpMsgNo = NULL;

    if (!dComIfGp_event_chkTalkXY()) {
        mpMsgNo = l_msg_sv_talk_tbl[field_0x746];
        field_0x734 |= l_msg_sv_flag_tbl[field_0x746];

        if (field_0x734 & 0x10) {
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_2F80);
        }
    }

    if (mpMsgNo != NULL) {
        ret = *mpMsgNo;
    }

    if (ret == 0) {
        setAnmTbl(l_npc_anm_talk);
    }

    return ret;
}

/* 00001EB4-00001EBC       .text setMessage__9daNpcSv_cFUl */
void daNpcSv_c::setMessage(u32 i_msgNo) {
    mCurrMsgNo = i_msgNo;
}

/* 00001EBC-00001F74       .text setAnmFromMsgTag__9daNpcSv_cFv */
void daNpcSv_c::setAnmFromMsgTag() {
    switch (g_dComIfG_gameInfo.play.getBaseAnimeID()) {
    case 0:
        setAnmTbl(l_npc_anm_wait);
        break;
    case 1:
        setAnmTbl(l_npc_anm_ya1);
        break;
    case 2:
        setAnmTbl(l_npc_anm_talk);
        break;
    case 3:
        setAnmTbl(l_npc_anm_talk2);
        break;
    case 4:
        setAnmTbl(l_npc_anm_talk3);
        break;
    case 5:
        setAnmTbl(l_npc_anm_ya2);
        break;
    }

    g_dComIfG_gameInfo.play.clearBaseAnimeID();
}

/* 00001F74-00001F94       .text getPrmNpcNo__9daNpcSv_cFv */
u8 daNpcSv_c::getPrmNpcNo() {
    s8 no = argument;
    if (no >= 0 && no < 4) {
        return no;
    }
    return 0;
}

/* 00001F94-0000209C       .text setMtx__9daNpcSv_cFv */
void daNpcSv_c::setMtx() {
    mpMorf->getModel()->setBaseScale(scale);
    cXyz offset(l_npc_dat[field_0x73b].field_0x14, l_npc_dat[field_0x73b].field_0x18, l_npc_dat[field_0x73b].field_0x1c);

    mDoMtx_stack_c::YrotS(current.angle.y);
    mDoMtx_stack_c::multVec(&offset, &offset);
    mDoMtx_stack_c::transS(
        current.pos.x + offset.x,
        current.pos.y + offset.y,
        current.pos.z + offset.z
    );

    mDoMtx_stack_c::XYZrotM(current.angle.x, 0, current.angle.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
}

/* 0000209C-000023D4       .text chkAttention__9daNpcSv_cFv */
void daNpcSv_c::chkAttention() {
    field_0x745 = 0;

    if (mEventCut.getAttnFlag()) {
        field_0x6ec = mEventCut.getAttnPos();
        field_0x741 = 1;

        if (field_0x743 != 0) {
            field_0x71c = 0;
            m_jnt.setTrn();
        } else {
            field_0x71c = 1;
        }

        if (field_0x737 == 0) {
            field_0x737 = 1;
        }
    } else {
        fopAc_ac_c* player = dComIfGp_getLinkPlayer();
        f32 dist = field_0x710;
        int ang = field_0x726;

        f32 disXZ;
        s16 angY;
        dNpc_calc_DisXZ_AngY(current.pos, player->current.pos, &disXZ, &angY);

        if (field_0x737 != 0) {
            dist += 100.0f;
            ang += 0x71c;
        }

        angY -= shape_angle.y;

        if (dist > disXZ && ang > abs(angY)) {
            field_0x6ec = dNpc_playerEyePos(l_npc_dat[field_0x73b].field_0x20);
            field_0x741 = 1;

            if (field_0x743 != 0) {
                field_0x71c = 0;
            } else {
                field_0x71c = 1;
            }

            if (field_0x744 == 0) {
                field_0x732 = field_0x728;
                field_0x71c = 0;
                field_0x741 = 2;
                m_jnt.setTrn();
            }

            if (field_0x737 == 0) {
                field_0x737 = 1;
            }

            field_0x745 = 1;
        } else {
            if (field_0x737 == 1) {
                field_0x737 = 0;
                field_0x724 = l_npc_dat[field_0x73b].field_0x40;
            }

            if (l_npc_dat[field_0x73b].field_0x30 > disXZ) {
                field_0x6ec = dNpc_playerEyePos(l_npc_dat[field_0x73b].field_0x20);
                field_0x741 = 1;

                if (field_0x743 != 0) {
                    field_0x71c = 0;
                } else {
                    field_0x71c = 1;
                }

                if (field_0x744 == 0) {
                    field_0x732 = field_0x728;
                    field_0x71c = 0;
                    field_0x741 = 2;
                    m_jnt.setTrn();
                }

                field_0x745 = 1;
            } else {
                field_0x741 = 0;

                if (field_0x6d8 == 0) {
                    if (field_0x724 != 0) {
                        field_0x724--;
                    } else {
                        field_0x732 = field_0x728;
                        field_0x71c = 0;
                        field_0x741 = 2;
                        m_jnt.setTrn();
                    }
                }
            }
        }
    }

    field_0x72e = l_npc_dat[field_0x73b].field_0x36;
}

/* 000023D4-00002520       .text lookBack__9daNpcSv_cFv */
void daNpcSv_c::lookBack() {
    s16 headAngleY = field_0x72e;
    s16 targetY = current.angle.y;
    cXyz* dstPos_p;
    dstPos_p = NULL;
    cXyz dstPos;
    cXyz pos = eyePos;
    bool headOnlyFollow = field_0x71c;

    switch (field_0x741) {
    case 0:
        break;
    case 1:
        dstPos = field_0x6ec;
        dstPos_p = &dstPos;
        break;
    case 2:
        targetY = field_0x732;
        break;
    }

    if (field_0x736 != 0 && field_0x743 != 0) {
        headOnlyFollow = 0;
        m_jnt.setTrn();
    }

    if (m_jnt.trnChk()) {
        s16 turnSpeed = mEventCut.getTurnSpeed();
        if (turnSpeed != 0) {
            headAngleY = turnSpeed;
        }
        cLib_addCalcAngleS2(&field_0x730, headAngleY, 4, 0x800);
    } else {
        field_0x730 = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dstPos_p, pos, targetY, field_0x730, headOnlyFollow);

    shape_angle = current.angle;
}

/* 00002520-000025E8       .text playAnm__9daNpcSv_cFv */
void daNpcSv_c::playAnm() {
    field_0x73e &= ~1;

    if (mpMorf->play(NULL, 0, 0) && mpAnmDat != NULL && field_0x73f > 0) {
        field_0x73f--;

        if (field_0x73f == 0) {
            mpAnmDat++;

            if (setAnmTbl(mpAnmDat)) {
                field_0x73e |= 1;
            }
        } else {
            setAnm(mpAnmDat->field_0x0, 0, 0.0f);
        }
    }
}

/* 000025E8-000026C8       .text setAnm__9daNpcSv_cFUcif */
void daNpcSv_c::setAnm(u8 i_bckIdx, int i_mode, f32 i_speed) {
    if (field_0x70c >= 0.0f) {
        i_speed = field_0x70c;
        field_0x70c = -1.0f;
    }

    mpMorf->setAnm((J3DAnmTransform*)dComIfG_getObjectIDRes(
                       l_arcname_tbl[field_0x73b], l_bck_ix_tbl[field_0x73b][i_bckIdx]),
                   i_mode, i_speed, 1.0f, 0.0f, -1.0f, NULL);

    field_0x73d = i_bckIdx;
}

/* 000026C8-00002768       .text setAnmTbl__9daNpcSv_cFP9sSvAnmDat */
u8 daNpcSv_c::setAnmTbl(sSvAnmDat* i_dat) {
    if (i_dat->field_0x0 == 0xff) {
        mpAnmDat = NULL;
        return 1;
    }

    mpAnmDat = i_dat;
    field_0x73f = mpAnmDat->field_0x2;

    int mode = 2;
    if ((s8)field_0x73f > 0) {
        mode = 0;
    }

    if (field_0x73d != mpAnmDat->field_0x0 || mode == 0) {
        setAnm(mpAnmDat->field_0x0, mode, mpAnmDat->field_0x1);
    }

    return 0;
}

/* 00002768-000027E0       .text setCollision__9daNpcSv_cFP8dCcD_Cyl4cXyzff */
void daNpcSv_c::setCollision(dCcD_Cyl* cyl, cXyz center, float radius, float height) {
    cyl->SetC(center);
    cyl->SetR(radius);
    cyl->SetH(height);
    dComIfG_Ccsp()->Set(cyl);
}

#include "d/d_com_inf_game.h"

/* 000027E0-0000293C       .text getTalkNo__9daNpcSv_cFv */
u8 daNpcSv_c::getTalkNo() {
    BOOL hasItem;
    if (dComIfGs_checkGetItem(dItemNo_GRAPPLING_HOOK_e)) {
        hasItem = TRUE;
    } else {
        hasItem = FALSE;
    }

    field_0x746 = 0;

    if (!(field_0x734 & 0x20)) {
        if (!(field_0x734 & 0x1)) {
            if (hasItem) {
                field_0x746 = 1;
            } else {
                field_0x746 = 0;
            }
        } else if (!(field_0x734 & 0x2)) {
            if (hasItem) {
                field_0x746 = 3;
            } else {
                field_0x746 = 2;
            }
        } else {
            if (hasItem) {
                field_0x746 = 5;
            } else {
                field_0x746 = 4;
            }
        }
    } else {
        if (!hasItem) {
            if (!(field_0x734 & 0x4)) {
                field_0x746 = 6;
            } else {
                field_0x746 = 7;
            }
        } else if (!dComIfGs_isCollect(0, 1)) {
            if (!(field_0x734 & 0x8)) {
                field_0x746 = 8;
            } else {
                field_0x746 = 9;
            }
        } else {
            if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_2F80)) {
                field_0x746 = 0xa;
            } else {
                field_0x746 = 0xb;
            }
        }
    }

    return field_0x746;
}

/* 0000293C-00002958       .text isTalkOK__9daNpcSv_cFv */
BOOL daNpcSv_c::isTalkOK() {
    if (dComIfGp_checkPlayerStatus0(0, daPyStts0_SWIM_e)) {
        return FALSE;
    }
    return TRUE;
}

/* 00002958-00002978       .text daNpc_PeopleCreate__FPv */
static cPhs_State daNpc_PeopleCreate(void* i_this) {
    return ((daNpcSv_c*)i_this)->_create();
}

/* 00002978-0000299C       .text daNpc_PeopleDelete__FPv */
static BOOL daNpc_PeopleDelete(void* i_this) {
    return ((daNpcSv_c*)i_this)->_delete();
}

/* 0000299C-000029C0       .text daNpc_PeopleExecute__FPv */
static BOOL daNpc_PeopleExecute(void* i_this) {
    return ((daNpcSv_c*)i_this)->_execute();
}

/* 000029C0-000029E4       .text daNpc_PeopleDraw__FPv */
static BOOL daNpc_PeopleDraw(void* i_this) {
    return ((daNpcSv_c*)i_this)->_draw();
}

/* 000029E4-000029EC       .text daNpc_PeopleIsDelete__FPv */
static BOOL daNpc_PeopleIsDelete(void*) {
    return TRUE;
}

static actor_method_class daNpc_PeopleMethodTable = {
    (process_method_func)daNpc_PeopleCreate,
    (process_method_func)daNpc_PeopleDelete,
    (process_method_func)daNpc_PeopleExecute,
    (process_method_func)daNpc_PeopleIsDelete,
    (process_method_func)daNpc_PeopleDraw,
};

actor_process_profile_definition g_profile_NPC_SV = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_SV_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpcSv_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_SV_e,
    /* Actor SubMtd */ &daNpc_PeopleMethodTable,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
