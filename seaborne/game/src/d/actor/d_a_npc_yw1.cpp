/**
 * d_a_npc_yw1.cpp
 * NPC - Sue-Belle
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_npc_yw1.h"
#include "f_op/f_op_actor_mng.h"
#include "d/d_com_inf_game.h"
#include "d/d_kankyo_wether.h"
#include "m_Do/m_Do_mtx.h"
#include "d/actor/d_a_npc_bm1.h"

class daNpc_Yw1_childHIO_c : public mDoHIO_entry_c {
public:
    daNpc_Yw1_childHIO_c();
    virtual ~daNpc_Yw1_childHIO_c() {}

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
    /* 0x1D */ u8 field_0x1d[0x20 - 0x1D];
    /* 0x20 */ f32 field_0x20;
    /* 0x24 */ s16 field_0x24;
    /* 0x26 */ s16 field_0x26;
    /* 0x28 */ f32 field_0x28;
    /* 0x2C */ f32 field_0x2c;
    /* 0x30 */ f32 field_0x30;
    /* 0x34 */ u32 field_0x34;
}; // size = 0x38

class daNpc_Yw1_HIO_c : public mDoHIO_entry_c {
public:
    daNpc_Yw1_HIO_c();
    virtual ~daNpc_Yw1_HIO_c() {}

public:
    /* 0x04 */ s8 field_0x4;
    /* 0x05 */ u8 field_0x5[0x8 - 0x5];
    /* 0x08 */ u32 field_0x8;
    /* 0x0C */ daNpc_Yw1_childHIO_c children[1];
}; // size = 0x44

static daNpc_Yw1_HIO_c l_HIO;

#define L_CHECK_SZ 20
static fopAc_ac_c* l_check_inf[L_CHECK_SZ];
static s32 l_check_wrk;

/* 000000EC-00000108       .text __ct__20daNpc_Yw1_childHIO_cFv */
daNpc_Yw1_childHIO_c::daNpc_Yw1_childHIO_c() {
    /* Nonmatching */
}

/* 00000150-000001DC       .text __ct__15daNpc_Yw1_HIO_cFv */
daNpc_Yw1_HIO_c::daNpc_Yw1_HIO_c() {
    static u32 a_prm_tbl[12] = {
        0x20001E78, 0xE000E188, 0x000020D0, 0x0000DF30,
        0x064004B0, 0x432A0000, 0x00000000, 0x43960000,
        0x00060400, 0x3F733333, 0x3F99999A, 0x3F000000,
    };

    for (int i = 0; i < 1; i++) {
        children[i].field_0x34 = 0;
        memcpy(&children[i].field_0x4, a_prm_tbl, 0x30);
    }
    field_0x4 = -1;
    field_0x8 = -1;
}

/* 00000238-00000284       .text nodeCB_Hair__FP7J3DNodei */
static BOOL nodeCB_Hair(J3DNode* i_node, int i_param) {
    if (i_param == 0) {
        daNpc_Yw1_c* actor = (daNpc_Yw1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->_nodeCB_Hair(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 00000284-00000414       .text _nodeCB_Hair__11daNpc_Yw1_cFP7J3DNodeP8J3DModel */
void daNpc_Yw1_c::_nodeCB_Hair(J3DNode* i_node, J3DModel* i_pModel) {
    static cXyz a_eye_pos_off(18.0f, 20.0f, 0.0f); // Unused

    int jointIdx = ((J3DJoint*)(i_node))->getJntNo();
    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jointIdx));

    if (m_hair1 == jointIdx) {
        mDoMtx_stack_c::YrotM(field_0x786);
        mDoMtx_stack_c::ZrotM(field_0x7b6 + (field_0x784 + field_0x780));
    } else if (m_hair2 == jointIdx) {
        mDoMtx_stack_c::YrotM(field_0x7a6);
        mDoMtx_stack_c::ZrotM(field_0x7a4 + field_0x7b8);
    } else if (m_hair3 == jointIdx) {
        mDoMtx_stack_c::YrotM(field_0x7ae);
        mDoMtx_stack_c::ZrotM(field_0x7ac + field_0x7ba);
    }

    mDoMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jointIdx, mDoMtx_stack_c::get());
}

/* 00000450-0000049C       .text nodeCB_Head__FP7J3DNodei */
static BOOL nodeCB_Head(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Yw1_c* this_ = (daNpc_Yw1_c*)model->getUserArea();
        if (this_ != NULL) {
            this_->_nodeCB_Head(i_node, model);
        }
    }
    return TRUE;
}

/* 0000049C-000005BC       .text _nodeCB_Head__11daNpc_Yw1_cFP7J3DNodeP8J3DModel */
void daNpc_Yw1_c::_nodeCB_Head(J3DNode* i_node, J3DModel* i_pModel) {
    static cXyz a_eye_pos_off(18.0f, 20.0f, 0.0f);
    int jointIdx = ((J3DJoint*)i_node)->getJntNo();

    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jointIdx));
    mHeadPos.x = mDoMtx_stack_c::get()[0][3];
    mHeadPos.y = mDoMtx_stack_c::get()[1][3];
    mHeadPos.z = mDoMtx_stack_c::get()[2][3];

    mDoMtx_stack_c::XrotM(m_jnt.getHead_y());
    mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
    PSMTXMultVec(mDoMtx_stack_c::get(), &a_eye_pos_off, &mEyePos);

    mDoMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jointIdx, mDoMtx_stack_c::get());
}

/* 000005BC-00000608       .text nodeCB_BackBone__FP7J3DNodei */
static BOOL nodeCB_BackBone(J3DNode* i_node, int i_param2) {
    if (!i_param2) {
        if (j3dSys.getModel()->getUserArea()) {
            reinterpret_cast<daNpc_Yw1_c*>(j3dSys.getModel()->getUserArea())->_nodeCB_BackBone(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 00000608-000006A8       .text _nodeCB_BackBone__11daNpc_Yw1_cFP7J3DNodeP8J3DModel */
void daNpc_Yw1_c::_nodeCB_BackBone(J3DNode* i_node, J3DModel* i_pModel) {
    s32 jntNo = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jntNo));
    mDoMtx_stack_c::XrotM(m_jnt.getBackbone_y());
    mDoMtx_stack_c::ZrotM(-m_jnt.getBackbone_x());
    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 000006A8-000006C8       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Yw1_c*)i_this)->CreateHeap();
}

/* 000006C8-00000740       .text searchActor_Bm1__FPvPv */
static void* searchActor_Bm1(void* i_actor, void*) {
    fopAc_ac_c* actor = (fopAc_ac_c*)i_actor;

    if (l_check_wrk < L_CHECK_SZ && fopAc_IsActor(actor) &&
        fopAcM_GetName(actor) == fpcNm_NPC_BM1_e) {
        l_check_inf[l_check_wrk] = actor;
        l_check_wrk++;
    }
    return NULL;
}

/* 00000740-00000844       .text init_YW1_0__11daNpc_Yw1_cFv */
bool daNpc_Yw1_c::init_YW1_0() {
    dSv_event_c& event = g_dComIfG_gameInfo.save.getEvent();

    if (!event.isEventBit(0x520) && !event.isEventBit(0x1)) {
        if (!mPathRun.isPath()) {
            return false;
        }
        field_0x773 = 1;
        field_0x704 = fopAcM_create(fpcNm_TSUBO_e, 0x7F063F, &current.pos, current.roomNo);
        set_action(&daNpc_Yw1_c::wait_action1, NULL);
        set_pthPoint(0);
        return field_0x704 != fpcM_ERROR_PROCESS_ID_e;
    }
    return false;
}

/* 00000844-000008F8       .text init_YW1_1__11daNpc_Yw1_cFv */
bool daNpc_Yw1_c::init_YW1_1() {
    if (!dComIfGs_isEventBit(0x520)) {
        if (!dComIfGs_isEventBit(0x1)) {
            fopAcM_ClearStatusMap(this);
        }

        set_action(&daNpc_Yw1_c::wait_action2, NULL);
        field_0x777 = 1;
        return true;
    } else {
        return false;
    }
}

/* 000008F8-000009A4       .text init_YW1_2__11daNpc_Yw1_cFv */
bool daNpc_Yw1_c::init_YW1_2() {
    dSv_event_c& event = g_dComIfG_gameInfo.save.getEvent();

    if (event.isEventBit(0x520)) {
        if (dKy_daynight_check() == 1 || !event.isEventBit(0x2A20)) {
            set_action(&daNpc_Yw1_c::wait_action2, NULL);
            return true;
        }
    }
    return false;
}

/* 000009A4-00000AB4       .text init_YW1_3__11daNpc_Yw1_cFv */
bool daNpc_Yw1_c::init_YW1_3() {
    dSv_event_c& event = g_dComIfG_gameInfo.save.getEvent();

    if (event.isEventBit(0x520) && dKy_daynight_check() == 0) {
        if (!mPathRun.isPath()) {
            return false;
        }
        if (event.isEventBit(0x2A20)) {
            field_0x773 = 1;
            field_0x704 = fopAcM_create(fpcNm_TSUBO_e, 0x7F063F, &current.pos, current.roomNo);
            set_action(&daNpc_Yw1_c::wait_action1, NULL);
            set_pthPoint(0);
            return field_0x704 != fpcM_ERROR_PROCESS_ID_e;
        }
    }
    return false;
}

/* 00000AB4-00000C9C       .text createInit__11daNpc_Yw1_cFv */
bool daNpc_Yw1_c::createInit() {
    mEventCut.setActorInfo2("Yw1", this);

    attention_info.flags = 0xa;
    attention_info.distances[1] = 0xab;
    attention_info.distances[3] = 0xab;

    int iVar3 = 0xff;
    u8 pathIdx = fopAcM_GetParamBit(fopAcM_GetParam(this), 0x10, 0x8);
    if (pathIdx != 0xff) {
        mPathRun.setInf(pathIdx, fopAcM_GetRoomNo(this), true);
        if (mPathRun.isPath()) {
            fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
            iVar3 = 0xf0;
        } else {
            return false;
        }
    }

    field_0x7c0 = 7;

    bool ret;
    switch (field_0x7c6) {
    case 0:
        ret = init_YW1_0();
        break;
    case 1:
        ret = init_YW1_1();
        break;
    case 2:
        ret = init_YW1_2();
        break;
    case 3:
        ret = init_YW1_3();
        break;
    default:
        ret = 0;
    }

    if (ret == 0) {
        return 0;
    }

    field_0x726 = current.angle;
    shape_angle = field_0x726;

    gravity = -4.5f;

    mStts.Init(iVar3, 0xff, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);

    play_animation();

    mObjAcch.CrrPos(*dComIfG_Bgsp());
    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);

    mpMorf->setMorf(0.0f);

    setMtx(true);

    return 1;
}


/* 00000C9C-00000D44       .text play_animation__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::play_animation() {
    u32 mtrlSndId = 0;

    play_texPttrnAnm();

    if (mObjAcch.ChkGroundHit()) {
        mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(mObjAcch.m_gnd);
    }

    field_0x770 = mpMorf->play(&eyePos, mtrlSndId, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));

    if (mpMorf->getFrame() < field_0x750) {
        field_0x770 = 1;
    }

    field_0x750 = mpMorf->getFrame();
}

static cXyz l_hed_front(0.0f, 1.0f, 0.0f);

/* 00000D44-00000E4C       .text upLift__11daNpc_Yw1_cFv */
u8 daNpc_Yw1_c::upLift() {
    int ret;
    fopAc_ac_c* tsubo = searchByID(field_0x704, &ret);

    field_0x775 = ret == 1;
    if (tsubo != NULL) {
        if (!(tsubo->actor_status & fopAcStts_CARRY_e)) {
            fopAcM_setCarryNow(tsubo, FALSE);
        }

        cXyz front;
        cXyz offset(34.0f, -4.0f, 0.0f);

        cMtx_multVecSR(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num), &l_hed_front, &front);
        tsubo->shape_angle.y = cM_atan2s(front.x, front.z);
        mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));
        mDoMtx_stack_c::multVec(&offset, &tsubo->current.pos);
    }
    return field_0x775;
}

/* 00000E4C-00000F48       .text setMtx__11daNpc_Yw1_cFb */
void daNpc_Yw1_c::setMtx(bool i_setAttention) {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::ZXYrotM(field_0x726);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();
    setHairAngle();
    field_0x6d8->setBaseTRMtx(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num));
    field_0x6d8->calc();
    upLift();
    setAttention(i_setAttention);
}

/* 00000F48-00000F5C       .text bckResID__11daNpc_Yw1_cFi */
int daNpc_Yw1_c::bckResID(int i_no) {
    static const int a_res_id_tbl[7] = {
        0x00000000, 0x00000003, 0x00000004, 0x00000002,
        0x00000001, 0x00000005, 0x00000009,
    };
    return a_res_id_tbl[i_no];
}

/* 00000F5C-00000F70       .text btpResID__11daNpc_Yw1_cFi */
int daNpc_Yw1_c::btpResID(int i_no) {
    static const int a_res_id_tbl[1] = {
        0x00000008,
    };
    return a_res_id_tbl[i_no];
}

/* 00000F70-00001078       .text init_texPttrnAnm__11daNpc_Yw1_cFScb */
bool daNpc_Yw1_c::init_texPttrnAnm(signed char i_no, bool i_modify) {
    J3DModel* model = field_0x6d8;

    if (i_no < 0) {
        return false;
    }

    J3DAnmTexPattern* a_btp = (J3DAnmTexPattern*)dComIfG_getObjectIDRes("Yw", btpResID(i_no));
    JUT_ASSERT(DEMO_SELECT(0x286, 0x28a), a_btp != 0);

    field_0x7bf = i_no;
    field_0x6f0 = 0;
    field_0x6f2 = 0;
    return mBtpAnm.init(model->getModelData(), a_btp, TRUE, 0, 1.0f, 0, -1, i_modify, FALSE) != 0;
}

/* 00001078-00001108       .text play_texPttrnAnm__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::play_texPttrnAnm() {
    if (field_0x7bf != 0 || cLib_calcTimer(&field_0x6f2) == 0) {
        field_0x6f0++;
        if (field_0x6f0 >= mBtpAnm.getBtpAnm()->getFrameMax()) {
            if (field_0x7bf != 0) {
                field_0x6f0 = mBtpAnm.getBtpAnm()->getFrameMax();
            } else {
                field_0x6f2 = cLib_getRndValue(0x3c, 0x5a);
                field_0x6f0 = 0;
            }
        }
    }
}

/* 00001108-000011AC       .text setAnm_anm__11daNpc_Yw1_cFPQ211daNpc_Yw1_c9anm_prm_c */
void daNpc_Yw1_c::setAnm_anm(daNpc_Yw1_c::anm_prm_c* i_anmPrm) {
    if (i_anmPrm->mAnmNum < 0 || field_0x7c0 == i_anmPrm->mAnmNum) {
        return;
    }

    dNpc_setAnmIDRes(
        mpMorf,
        i_anmPrm->mLoopMode,
        i_anmPrm->mMorf,
        i_anmPrm->mSpeed,
        bckResID(i_anmPrm->mAnmNum),
        -1,
        "Yw"
    );

    field_0x7c0 = i_anmPrm->mAnmNum;
    field_0x770 = 0;
    field_0x771 = 0;
    field_0x750 = 0.0f;
}

/* 000011AC-0000121C       .text setAnm_NUM__11daNpc_Yw1_cFii */
void daNpc_Yw1_c::setAnm_NUM(int i_anmIdx, int i_btpIdx) {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {1, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {2, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {3, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {4, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {5, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {6, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
    };

    if (i_btpIdx != 0) {
        init_texPttrnAnm(a_anm_prm_tbl[i_anmIdx].mBtpNum, true);
    }
    setAnm_anm(&a_anm_prm_tbl[i_anmIdx]);
}

/* 0000121C-00001288       .text setAnm__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[] = {
        {-1, -1, 0, 0.0f, 0.0f, -1},
        {5, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {-1, -1, 0, 0.0f, 0.0f, -1},
        {2, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {5, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {2, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {5, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
    };
    init_texPttrnAnm(a_anm_prm_tbl[field_0x7c2].mBtpNum, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7c2]);
}

/* 00001288-0000128C       .text chngAnmTag__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::chngAnmTag() {
    /* Nonmatching */
}

/* 0000128C-00001290       .text ctrlAnmTag__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::ctrlAnmTag() {
    /* Nonmatching */
}

/* 00001290-000012D0       .text chngAnmAtr__11daNpc_Yw1_cFUc */
void daNpc_Yw1_c::chngAnmAtr(unsigned char i_param_1) {
    if (i_param_1 == field_0x7bd || i_param_1 > 7) {
        return;
    }

    field_0x7bd = i_param_1;
    setAnm_ATR();
}

/* 000012D0-000012D4       .text ctrlAnmAtr__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::ctrlAnmAtr() {
    /* Nonmatching */
}

/* 000012D4-00001338       .text setAnm_ATR__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::setAnm_ATR() {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {1, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {2, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {3, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {4, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {5, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
        {6, 0, 0, 8.0f, 1.0f, J3DFrameCtrl::EMode_LOOP},
    };

    init_texPttrnAnm(a_anm_prm_tbl[field_0x7bd].mBtpNum, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7bd]);
}

/* 00001338-000013F4       .text anmAtr__11daNpc_Yw1_cFUs */
void daNpc_Yw1_c::anmAtr(u16 i_atr) {
    switch (i_atr) {
    case 6:
        if (field_0x7c8 == 0) {
            chngAnmAtr(dComIfGp_getMesgAnimeAttrInfo());
            field_0x7c8++;
        }
        {
            u8 tag = dComIfGp_getMesgAnimeTagInfo();
            if (tag != 0xFF && tag != field_0x7be) {
                dComIfGp_clearMesgAnimeTagInfo();
                field_0x7be = tag;
                chngAnmTag();
            }
        }
        break;
    case 0xE:
        field_0x7c8 = 0;
        break;
    }
    ctrlAnmAtr();
    ctrlAnmTag();
}

/* 000013F4-0000148C       .text next_msgStatus__11daNpc_Yw1_cFPUl */
u16 daNpc_Yw1_c::next_msgStatus(u32* pMsgNo) {
    u16 status = 0xF;

    switch (*pMsgNo) {
    case 0x8A3:
        if (!g_dComIfG_gameInfo.save.getEvent().isEventBit(0x2A20)) {
            *pMsgNo = 0x8A4;
        } else if (dKy_daynight_check() == 0) {
            *pMsgNo = 0x8A6;
        } else {
            *pMsgNo = 0x8A5;
        }
        break;
    default:
        status = 0x10;
        break;
    }
    return status;
}

/* 0000148C-00001588       .text getMsg_YW1_0__11daNpc_Yw1_cFv */
u32 daNpc_Yw1_c::getMsg_YW1_0() {
    if (field_0x774) {
        return 0x8A0;
    }
    if (field_0x775) {
        return 0x89F;
    }

    dSv_event_c& event = g_dComIfG_gameInfo.save.getEvent();
    if (event.isEventBit(0x1)) {
        return event.isEventBit(0x140) ? 0x89C : 0x89B;
    }
    if (mPathRun.isPath() && field_0x776 == 1) {
        return mPathRun.getDir() == 1 ? 0x8A2 : 0x8A1;
    }
    if (!event.isEventBit(0x180)) {
        return event.isEventBit(0x2A80) ? 0x8AA : 0x899;
    }
    return 0x89A;
}

/* 00001588-00001600       .text getMsg_YW1_1__11daNpc_Yw1_cFv */
u32 daNpc_Yw1_c::getMsg_YW1_1() {
    dSv_event_c& event = g_dComIfG_gameInfo.save.getEvent();

    if (event.isEventBit(0xE20)) {
        return event.isEventBit(0x120) ? 0x89E : 0x89D;
    }
    return event.isEventBit(0x140) ? 0x89C : 0x89B;
}

/* 00001600-00001678       .text getMsg_YW1_2__11daNpc_Yw1_cFv */
u32 daNpc_Yw1_c::getMsg_YW1_2() {
    dSv_event_c& event = g_dComIfG_gameInfo.save.getEvent();

    if (!event.isEventBit(0x3A40)) {
        return 0x8A3;
    }
    if (!event.isEventBit(0x2A20)) {
        return 0x8A7;
    }
    return dKy_daynight_check() == 0 ? 0x8A9 : 0x8A8;
}

/* 00001678-000016F0       .text getMsg_YW1_3__11daNpc_Yw1_cFv */
u32 daNpc_Yw1_c::getMsg_YW1_3() {
    if (field_0x774) {
        return 0x8A0;
    }
    if (field_0x775) {
        return 0x89F;
    }
    if (mPathRun.isPath() && field_0x776 == 1) {
        return mPathRun.getDir() == 1 ? 0x8A2 : 0x8A1;
    }
    return getMsg_YW1_2();
}

/* 000016F0-00001770       .text getMsg__11daNpc_Yw1_cFv */
u32 daNpc_Yw1_c::getMsg() {
    u32 msgNo = 0;
    switch (field_0x7c6) {
        case 0:
            msgNo = getMsg_YW1_0();
            break;
        case 1:
            msgNo = getMsg_YW1_1();
            break;
        case 2:
            msgNo = getMsg_YW1_2();
            break;
        case 3:
            msgNo = getMsg_YW1_3();
            break;
    }
    return msgNo;
}

/* 00001770-000017C0       .text eventOrder__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::eventOrder() {
    char event_state = field_0x7c1;
    if (event_state == 1 || event_state == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x7c1 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    }
}

/* 000017C0-00001800       .text checkOrder__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::checkOrder() {
    u16 cmd = eventInfo.getCommand();
    if (cmd == dEvtCmd_INDEMO_e) {
        return;
    }
    if (cmd != dEvtCmd_INTALK_e) {
        return;
    }

    if (field_0x7c1 == 1 || field_0x7c1 == 2) {
        field_0x7c1 = 0;
        field_0x77c = 1;
    }
}

/* 00001800-00001898       .text chk_talk__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::chk_talk() {
    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x772 = dComIfGp_event_getPreItemNo();
            return TRUE;
        } else {
            return FALSE;
        }
    } else {
        field_0x772 = 0xFF;
        return TRUE;
    }
}

/* 00001898-000018D8       .text chk_parts_notMov__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::chk_parts_notMov() {
    BOOL ret = FALSE;
    if (field_0x75a == m_jnt.getHead_y() && field_0x75c == m_jnt.getBackbone_y() && field_0x758 == current.angle.y) {
        ret = TRUE;
    }
    return ret;
}

/* 000018D8-00001938       .text searchByID__11daNpc_Yw1_cFUiPi */
fopAc_ac_c* daNpc_Yw1_c::searchByID(fpc_ProcID i_id, int* o_ret) {
    fopAc_ac_c* actor = NULL;
    if (o_ret != NULL) {
        *o_ret = 0;
    }
    if (!fopAcM_SearchByID(i_id, &actor) && o_ret != NULL) {
        *o_ret = 1;
    }
    return actor;
}

/* 00001938-000019E8       .text partner_search_sub__11daNpc_Yw1_cFPFPvPv_Pv */
bool daNpc_Yw1_c::partner_search_sub(void* (*i_searchFunc)(void*, void*)) {
    bool ret = false;

    field_0x700 = -1;
    l_check_wrk = 0;
    for (int i = 0; i < L_CHECK_SZ; i++) {
        l_check_inf[i] = NULL;
    }
    fpcEx_Search(*i_searchFunc, this);
    if (l_check_wrk != 0) {
        field_0x700 = fopAcM_GetID(l_check_inf[0]);
        ret = true;
    }
    return ret;
}

/* 000019E8-00001A54       .text partner_search__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::partner_search() {
    if (field_0x7c7 == 1) {
        bool temp;
        switch (field_0x7c6) {
            case 0:
                temp = partner_search_sub(searchActor_Bm1);
                break;
            default:
                temp = true;
                break;
        }

        if (temp) {
            field_0x7c7++;
        }
    }
}

/* 00001A54-00001C2C       .text lookBack__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::lookBack() {
    f32 unused_tbl[] = {182.0389f};
    field_0x75a = m_jnt.mAngles[0][1];
    field_0x75c = m_jnt.mAngles[1][1];
    field_0x758 = current.angle.y;

    cXyz target;
    cXyz eye_pos;

    eye_pos = current.pos;
    eye_pos.y = eyePos.y;
    target.set(0.0f, 0.0f, 0.0f);

    cXyz* targetP = NULL;
    s16 angle = current.angle.y;
    bool trn = field_0x77d;

    switch (field_0x7c4) {
    case 1:
        target = dNpc_playerEyePos(-20.0f);
        targetP = &target;
        break;
    case 2:
        target.x = field_0x738.x;
        target.y = field_0x738.y;
        target.z = field_0x738.z;
        targetP = &target;
        break;
    case 3:
        angle = field_0x76e;
        break;
    case 4: {
        fopAc_ac_c* actor = searchByID(field_0x708, NULL);
        if (actor != NULL) {
            field_0x738.x = actor->current.pos.x;
            field_0x738.y = actor->current.pos.y;
            field_0x738.z = actor->current.pos.z;
            field_0x738.y = actor->eyePos.y;
            target.x = field_0x738.x;
            target.y = field_0x738.y;
            target.z = field_0x738.z;
            targetP = &target;
        }
        break;
    }
    }

    cLib_addCalcAngleS2(&field_0x76c, l_HIO.children[mType].field_0x16, 4, 0x800);
    if (!m_jnt.mbTrn) {
        field_0x76c = 0;
    }
    m_jnt.lookAtTarget(&current.angle.y, targetP, eye_pos, angle, field_0x76c, trn);
}

/* 00001C2C-00001CAC       .text chkAttention__11daNpc_Yw1_cFv */
bool daNpc_Yw1_c::chkAttention() {
    dAttention_c& attention = dComIfGp_getAttention();

    if (attention.LockonTruth()) {
        return this == attention.LockonTarget(0);
    }
    return this == attention.ActionTarget(0);
}

/* 00001CAC-00001D28       .text setAttention__11daNpc_Yw1_cFb */
void daNpc_Yw1_c::setAttention(bool i_force) {
    f32 y_offset = l_HIO.children[mType].field_0x18;
    cXyz pos(current.pos);

    attention_info.position.set(pos.x, pos.y + y_offset, pos.z);
    if (field_0x760 != 0 || i_force) {
        eyePos.set(mEyePos.x, mEyePos.y, mEyePos.z);
    }
}

/* 00001D28-00001DC4       .text decideType__11daNpc_Yw1_cFi */
bool daNpc_Yw1_c::decideType(int i_type) {
    mType = 0;
    field_0x7c6 = -1;

    switch (i_type) {
    case 0:
        field_0x7c6 = 0;
        break;
    case 1:
        field_0x7c6 = 1;
        break;
    case 2:
        field_0x7c6 = 2;
        break;
    case 3:
        field_0x7c6 = 3;
        break;
    default:
        return false;
    }

    bool ret = false;
    if (mType != -1 && field_0x7c6 != -1) {
        ret = true;
    }
    return ret;
}

/* 00001DC4-00001E64       .text privateCut__11daNpc_Yw1_cFi */
void daNpc_Yw1_c::privateCut(int i_staffIdx) {
    static char* a_cut_tbl[] = {"DUMMY"};

    if (i_staffIdx != -1) {
        field_0x7bc = dComIfGp_evmng_getMyActIdx(i_staffIdx, a_cut_tbl, ARRAY_SIZE(a_cut_tbl), TRUE, 0);
        if (field_0x7bc == -1) {
            dComIfGp_evmng_cutEnd(i_staffIdx);
        } else {
            dComIfGp_evmng_getIsAddvance(i_staffIdx);
            dComIfGp_evmng_cutEnd(i_staffIdx);
        }
    }
}

/* 00001E64-00001E88       .text endEvent__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x7bd = 0xff;
    field_0x7be = 0xff;
}

/* 00001E88-00001EC0       .text isEventEntry__11daNpc_Yw1_cFv */
s32 daNpc_Yw1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mEventCut.getActorName());
}

/* 00001EC0-00001F18       .text event_proc__11daNpc_Yw1_cFi */
void daNpc_Yw1_c::event_proc(int param) {
    if (!mEventCut.cutProc()) {
        privateCut(param);
    }
    lookBack();
}

/* 00001F18-00001FC4       .text set_action__11daNpc_Yw1_cFM11daNpc_Yw1_cFPCvPvPv_iPv */
s32 daNpc_Yw1_c::set_action(ActionFunc i_actionFunc, void* i_param) {
    if (mActionFunc != i_actionFunc) {
        if (mActionFunc != NULL) {
            field_0x7c7 = 9;
            (this->*mActionFunc)(i_param);
        }
        mActionFunc = i_actionFunc;
        field_0x7c7 = 0;
        (this->*mActionFunc)(i_param);
    }
    return 1;
}

/* 00001FC4-0000205C       .text setStt__11daNpc_Yw1_cFSc */
void daNpc_Yw1_c::setStt(s8 i_stt) {
    s8 old_stt = field_0x7c2;

    field_0x7c2 = i_stt;
    switch (field_0x7c2) {
    case 2:
        field_0x7bd = 0xFF;
        field_0x7be = 0xFF;
        field_0x7c8 = 0;
        field_0x7c3 = old_stt;
        break;
    case 3:
    case 5:
        field_0x77d = 1;
        break;
    case 4:
    case 6:
        break;
    }
    setAnm();
    chngTsuboAnm();
}

/* 0000205C-0000212C       .text chngTsuboAnm__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::chngTsuboAnm() {
    if (field_0x773) {
        if (!field_0x775) {
            switch (field_0x7c0) {
            case 4:
                setAnm_NUM(3, 1);
                break;
            case 5:
                setAnm_NUM(1, 1);
                break;
            }
        } else {
            switch (field_0x7c0) {
            case 1:
                setAnm_NUM(5, 1);
                break;
            case 2:
                break;
            case 3:
                setAnm_NUM(4, 1);
                break;
            }
            field_0x773 = 0;
        }
    }
}

/* 0000212C-000022B8       .text chk_areaIN__11daNpc_Yw1_cFffs4cXyz */
bool daNpc_Yw1_c::chk_areaIN(f32 param_1, f32 param_2, s16 param_3, cXyz param_4) {
    f32 abs1 = (dComIfGp_getLinkPlayer()->current.pos - param_4).absXZ();
    f32 abs2 = dComIfGp_getLinkPlayer()->current.pos.y - param_4.y;
    int angleY = cLib_targetAngleY(&current.pos, &dComIfGp_getLinkPlayer()->current.pos);
    s16 angle = (angleY - current.angle.y);

    if (abs1 < param_1 && std::fabsf(abs2) < param_2 && abs(angle) < param_3) {
        return true;
    }
    return false;
}

/* 000022B8-00002360       .text set_pthPoint__11daNpc_Yw1_cFUc */
void daNpc_Yw1_c::set_pthPoint(unsigned char i_point) {
    if (mPathRun.isPath()) {
        mPathRun.setIdx(i_point);
        current.pos = mPathRun.getPoint(mPathRun.getIdx());
        if (mPathRun.nextIdx()) {
            cXyz next_pos = mPathRun.getPoint(mPathRun.getIdx());
            current.angle.y = cLib_targetAngleY(&current.pos, &next_pos);
        }
    }
}

static const Vec l_eye_offset = {15.0f, 10.0f, 0.0f};
static const Vec l_head_center_offset = {15.0f, 0.0f, 0.0f};
static const Vec l_neck_front = {0.0f, 1.0f, 0.0f};
static const Vec l_neck_top = {1.0f, 0.0f, 0.0f};

/* 00002360-00002D94       .text setHairAngle__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::setHairAngle() {
    cXyz neck_front;
    cXyz neck_top;
    cXyz head_center;
    cXyz eye;
    cXyz head_dir;
    cXyz hair_pos;
    cXyz diff;
    cXyz wind_vec;
    f32 wind_pow;

    MtxP head_mtx = mpMorf->getModel()->getAnmMtx(m_hed_jnt_num);
    cMtx_multVec(head_mtx, &l_head_center_offset, &head_center);
    cMtx_multVec(head_mtx, &l_eye_offset, &eye);
    head_dir = eye - head_center;

    s16 angle_y = cM_atan2s(head_dir.x, head_dir.z);
    f32 sin_y = cM_ssin(angle_y);
    f32 cos_y = cM_scos(angle_y);

    MtxP hair_mtx = mpMorf->getModel()->getAnmMtx(m_hed_jnt_num);
    hair_pos.x = hair_mtx[0][3];
    hair_pos.y = hair_mtx[1][3];
    hair_pos.z = hair_mtx[2][3];

    dKyw_get_AllWind_vec(&hair_pos, &wind_vec, &wind_pow);
    f32 wind_pow2 = wind_pow * wind_pow;

    cMtx_multVecSR(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num), &l_neck_front, &neck_front);
    cMtx_multVecSR(mpMorf->getModel()->getAnmMtx(m_hed_jnt_num), &l_neck_top, &neck_top);

    s16 old780 = field_0x780;
    s16 old782 = field_0x782;

    if (neck_top.y < 0.0f) {
        field_0x780 = cM_atan2s(neck_front.y, -neck_front.absXZ());
        field_0x782 = cM_atan2s(neck_front.x, neck_front.z) + 0x8000;
    } else {
        field_0x780 = cM_atan2s(neck_front.y, neck_front.absXZ());
        field_0x782 = cM_atan2s(neck_front.x, neck_front.z);
    }

    if (std::fabsf(neck_front.y) > 0.7f) {
        field_0x782 = old782;
    }

    s16 half780 = (s16)(field_0x780 - old780) / 2;
    s16 half782 = (s16)(field_0x782 - old782) / 2;

    if (half780 > 0x200) {
        half780 = 0x200;
    } else if (half780 < -0x200) {
        half780 = -0x200;
    }

    if (half782 > 0x800) {
        half782 = 0x800;
    } else if (half782 < -0x800) {
        half782 = -0x800;
    }

    diff.x = field_0x794.x - hair_pos.x;
    diff.y = field_0x794.y - hair_pos.y - 7.5f;
    diff.z = field_0x794.z - hair_pos.z;

    diff += wind_vec * wind_pow2;

    if (std::fabsf(diff.x) < 0.01f) {
        diff.x = 0.0f;
    }
    if (std::fabsf(diff.z) < 0.01f) {
        diff.z = 0.0f;
    }

    s16 prev_x = field_0x784;
    s16 prev_y = field_0x786;

    f32 proj = diff.z * cos_y + diff.x * sin_y;
    s16 pitch = cM_atan2s(-proj, -diff.y);
    if (field_0x784 < 0) {
        pitch = 0;
    } else if (pitch < 0 && pitch > -0x7800) {
        pitch = 0;
    } else if (pitch > 0x7800 || pitch <= -0x7800) {
        pitch = 0x7800;
    }

    cLib_addCalcAngleS2(&field_0x784, pitch, 5, 0x400);
    field_0x784 += field_0x7a0 + half780;

    s16 yaw = cM_atan2s(-(diff.x * cos_y - diff.z * sin_y), std::sqrtf(proj * proj + diff.y * diff.y));
    if (yaw > 0x3800) {
        yaw = 0x3800;
    } else if (yaw < -0x3800) {
        yaw = -0x3800;
    }

    cLib_addCalcAngleS2(&field_0x786, yaw, 5, 0x400);
    field_0x786 += field_0x7a2 - half782;

    field_0x7a0 = 0.2f * (s16)(field_0x784 - prev_x);
    field_0x7a2 = 0.2f * (s16)(field_0x786 - prev_y);
    prev_x = field_0x784 - prev_x;
    prev_y = field_0x786 - prev_y;
    field_0x7a4 -= prev_x;
    field_0x7a6 -= prev_y;

    prev_x = field_0x7a4;
    prev_y = field_0x7a6;
    cLib_addCalcAngleS2(&field_0x7a4, 0, 5, 0x400);
    cLib_addCalcAngleS2(&field_0x7a6, 0, 5, 0x400);
    field_0x7a4 += field_0x7a8 + half780;
    field_0x7a6 += field_0x7aa - half782;

    field_0x7a8 = 0.2f * (s16)(field_0x7a4 - prev_x);
    field_0x7aa = 0.2f * (s16)(field_0x7a6 - prev_y);
    prev_x = field_0x7a4 - prev_x;
    prev_y = field_0x7a6 - prev_y;
    field_0x7ac -= prev_x;
    field_0x7ae -= prev_y;

    prev_x = field_0x7ac;
    prev_y = field_0x7ae;
    cLib_addCalcAngleS2(&field_0x7ac, 0, 5, 0x400);
    cLib_addCalcAngleS2(&field_0x7ae, 0, 5, 0x400);
    field_0x7ac += field_0x7b0 + half780;
    field_0x7ae += field_0x7b2 - half782;

    field_0x7b0 = 0.2f * (s16)(field_0x7ac - prev_x);
    field_0x7b2 = 0.2f * (s16)(field_0x7ae - prev_y);

    f32 wind = (wind_pow2 + 0.65f * field_0x794.abs(hair_pos)) / 4.0f;
    if (wind > 1.0f) {
        wind = 1.0f;
    }

    s16 delta = 1500.0f + 4096.0f * wind;
    field_0x7b4 += delta;
    field_0x7b6 = 2280.0f * wind * cM_scos(field_0x7b4);
    field_0x7b8 = 3908.0f * wind * cM_scos(field_0x7b4 - 3.0f * delta);
    field_0x7ba = 7568.0f * wind * cM_scos(field_0x7b4 - 6.0f * delta);

    field_0x794 = hair_pos;
}

/* 00002D94-00002DEC       .text chk_brkTsubo__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::chk_brkTsubo() {
    if (field_0x775) {
        setStt(4);
        speedF = 0.0f;
        field_0x7c4 = 0;
        field_0x77d = 0;
    }
    return field_0x775;
}

/* 00002DEC-00002E28       .text chk_bm1Odoroki__11daNpc_Yw1_cFv */
u8 daNpc_Yw1_c::chk_bm1Odoroki() {
    fopAc_ac_c* bm1 = searchByID(field_0x700, NULL);

    if (bm1 != NULL) {
        return ((daNpc_Bm1_c*)bm1)->getOdoroki();
    }
    return 0;
}

/* 00002E28-00002F90       .text wait_1__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::wait_1() {
    if ((u8)chk_brkTsubo()) {
        return TRUE;
    }

    if (field_0x77c) {
        if ((u8)chk_talk()) {
            setStt(2);
            field_0x7c4 = 1;
            field_0x77d = 0;
            m_jnt.mbTrn = 1;
        }
        return TRUE;
    }

    if (chk_bm1Odoroki()) {
        field_0x7c4 = 4;
        field_0x708 = field_0x700;
        return TRUE;
    }

    field_0x7c1 = 2;
    chngTsuboAnm();
    if (chk_areaIN(100.0f + l_HIO.children[mType].field_0x20, 100.0f, 0x4400, current.pos) == false) {
        if (cLib_calcTimer(&field_0x766) == 0) {
            setStt(3);
            field_0x7c4 = 0;
            field_0x77d = 1;
            return TRUE;
        }
    } else {
        field_0x766 = cLib_getRndValue(10, 20);
    }
    field_0x7c4 = 1;
    return TRUE;
}

/* 00002F90-0000309C       .text wait_2__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::wait_2() {
    if (field_0x77c) {
        if ((u8)chk_talk()) {
            setStt(2);
            field_0x7c4 = 1;
            field_0x77d = 0;
            m_jnt.mbTrn = 1;
        }
        return TRUE;
    }

    if (chk_bm1Odoroki()) {
        field_0x7c4 = 4;
        field_0x708 = field_0x700;
        field_0x77d = 0;
        m_jnt.mbTrn = 1;
        return TRUE;
    }

    field_0x7c1 = 2;
    field_0x77d = 1;
    cLib_addCalcAngleS(&current.angle.y, field_0x722, 4, 0x800, 0x80);
    if (field_0x77b) {
        field_0x766 = cLib_getRndValue(10, 20);
    }
    if (cLib_calcTimer(&field_0x766) != 0) {
        field_0x7c4 = 1;
        return TRUE;
    }
    field_0x7c4 = 0;
    return TRUE;
}

/* 0000309C-00003174       .text wait_3__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::wait_3() {
    if (field_0x77c) {
        if ((u8)chk_talk()) {
            setStt(2);
            field_0x7c4 = 1;
            field_0x77d = 0;
            m_jnt.mbTrn = 1;
        }
        return TRUE;
    }

    field_0x7c1 = 2;
    field_0x77d = 1;
    cLib_addCalcAngleS(&current.angle.y, field_0x722, 4, 0x800, 0x80);
    if (field_0x77b) {
        field_0x766 = cLib_getRndValue(10, 20);
    }
    if (cLib_calcTimer(&field_0x766) != 0) {
        field_0x7c4 = 1;
        return TRUE;
    }
    field_0x7c4 = 0;
    return TRUE;
}

/* 00003174-0000347C       .text walk_1__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::walk_1() {
    if ((u8)chk_brkTsubo()) {
        return TRUE;
    }

    if (!field_0x776) {
        if (mPathRun.chkPointPass(current.pos, mPathRun.getDir())) {
            field_0x776 = !mPathRun.nextIdx();
        }
    }

    f32 speed;
    if (!field_0x776 &&
        chk_areaIN(l_HIO.children[mType].field_0x20, 100.0f, 0x4000, current.pos) == false &&
        !field_0x77c && !chk_bm1Odoroki()) {
        cXyz point = mPathRun.getPoint(mPathRun.getIdx());
        int target_angle = cLib_targetAngleY(&current.pos, &point);
        cLib_addCalcAngleS(&current.angle.y, target_angle, l_HIO.children[mType].field_0x24,
                           l_HIO.children[mType].field_0x26, 0);
        speed = l_HIO.children[mType].field_0x2c;
    } else {
        speed = 0.0f;
    }

    cLib_chaseF(&speedF, speed, l_HIO.children[mType].field_0x30);

    f32 play_speed = speedF * l_HIO.children[mType].field_0x28;
    mpMorf->setPlaySpeed(cLib_minLimit(play_speed, 0.5f));

    if ((int)speed == 0 && (int)speedF == 0) {
        speedF = 0.0f;
        if (field_0x77c) {
            if ((u8)chk_talk()) {
                setStt(2);
                field_0x7c4 = 1;
                field_0x77d = 0;
                m_jnt.mbTrn = 1;
            }
            return TRUE;
        }
        if (field_0x776) {
            setStt(5);
            field_0x7c4 = 0;
            return TRUE;
        }
        setStt(1);
        field_0x7c4 = 1;
        field_0x77d = 0;
        m_jnt.mbTrn = 1;
        return TRUE;
    }
    field_0x7c1 = 2;
    field_0x7c4 = 0;
    return TRUE;
}

/* 0000347C-00003594       .text turn_1__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::turn_1() {
    if ((u8)chk_brkTsubo()) {
        return TRUE;
    }

    if (field_0x77c != 0) {
        if ((u8)chk_talk()) {
            setStt(2);
        }
        return TRUE;
    }

    field_0x7c1 = 2;
    cXyz point = mPathRun.getPoint(mPathRun.getIdx());
    int targetAngleY = cLib_targetAngleY(&current.pos, &point);
    cLib_chaseAngleS(&current.angle.y, targetAngleY, l_HIO.children[mType].field_0x26);

    if (current.angle.y == targetAngleY) {
        setStt(3);
        field_0x7c4 = 0;
        mPathRun.turnDir();
        field_0x776 = 0;
        return TRUE;
    }

    return TRUE;
}

/* 00003594-000036F0       .text talk_1__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::talk_1() {
    u8 ret = chk_parts_notMov();
    talk(1);
    if (mpCurrMsg == NULL) {
        return ret;
    }
    {
        switch (mpCurrMsg->mStatus) {
        case fopMsgStts_BOX_OPENING_e:
        case fopMsgStts_MSG_TYPING_e:
            break;
        case fopMsgStts_MSG_DESTROYED_e:
            switch (mCurrMsgNo) {
            case 0x899:
            case 0x8AA:
                dComIfGs_onEventBit(0x180);
                break;
            case 0x89B:
                dComIfGs_onEventBit(0x140);
                break;
            case 0x89D:
                dComIfGs_onEventBit(0x120);
                break;
            case 0x89F:
                dComIfGp_setItemRupeeCount(-10);
                field_0x774 = 1;
                break;
            case 0x8A4:
            case 0x8A5:
            case 0x8A6:
                dComIfGs_onEventBit(0x3a40);
                break;
            }
            field_0x772 = 0xFF;
            field_0x77c = 0;
            setStt(field_0x7c3);
            field_0x766 = cLib_getRndValue(10, 20);
            endEvent();
            break;
        }
    }
    return ret;
}

/* 000036F0-00003848       .text wait_action1__11daNpc_Yw1_cFPv */
int daNpc_Yw1_c::wait_action1(void*) {
    switch (field_0x7c7) {
    case 0:
        setStt(3);
        field_0x7c7++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x77b = chkAttention();
        switch (field_0x7c2) {
        case 1:
            field_0x760 = wait_1();
            break;
        case 4:
            field_0x760 = wait_2();
            break;
        case 3:
            field_0x760 = walk_1();
            break;
        case 5:
            field_0x760 = turn_1();
            break;
        case 2:
            field_0x760 = talk_1();
            break;
        }
        lookBack();
        break;
    case 9:
        break;
    }

    if (g_dComIfG_gameInfo.save.getEvent().isEventBit(0x1) && field_0x7c6 == 0) {
        int ret;
        fopAc_ac_c* tsubo = searchByID(field_0x704, &ret);
        if (ret == 0 && tsubo != NULL) {
            fopAcM_delete(tsubo);
        }
        fopAcM_delete(this);
    }
    return 1;
}

/* 00003848-00003904       .text wait_action2__11daNpc_Yw1_cFPv */
int daNpc_Yw1_c::wait_action2(void*) {
    switch (field_0x7c7) {
    case 0:
        setStt(6);
        field_0x7c7++;
        break;
    case 1:
    case 2:
    case 3:
        field_0x77b = chkAttention();
        switch (field_0x7c2) {
        case 6:
            field_0x760 = wait_3();
            break;
        case 2:
            field_0x760 = talk_1();
            break;
        }
        lookBack();
        break;
    case 9:
        break;
    }
    return 1;
}

/* 00003904-00003A50       .text demo__11daNpc_Yw1_cFv */
u8 daNpc_Yw1_c::demo() {
    if (demoActorID == 0) {
        if (field_0x77e != 0) {
            field_0x77e = 0;
        }
    } else {
        if (field_0x77e == 0) {
            field_0x77e = 1;
            field_0x778 = 0;
            m_jnt.setHead_y(0);
            m_jnt.setHead_x(0);
            m_jnt.setBackBone_y(0);
            m_jnt.setBackBone_x(0);
        }

        dDemo_actor_c* demo_actor = dComIfGp_demo_getActor(demoActorID);
        if (mBtpAnm.getBtpAnm()) {
            field_0x6f0++;
            if (field_0x6f0 >= mBtpAnm.getBtpAnm()->getFrameMax()) {
                field_0x6f0 = mBtpAnm.getBtpAnm()->getFrameMax();
            }
        }

        J3DAnmTexPattern* btp_data = demo_actor->getP_BtpData("Yw");
        if (btp_data != NULL) {
            mBtpAnm.init(field_0x6d8->getModelData(), btp_data, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, FALSE);
            field_0x7bf = 1;
            field_0x6f0 = 0;
        }

        dDemo_setDemoData(this, 0x6A, mpMorf, "Yw");
    }

    return field_0x77e;
}

/* 00003A50-00003B4C       .text shadowDraw__11daNpc_Yw1_cFv */
void daNpc_Yw1_c::shadowDraw() {
    int ret;
    fopAc_ac_c* tsubo = searchByID(field_0x704, &ret);
    cXyz shadow_pos(current.pos.x, current.pos.y + 150.0f, current.pos.z);
    GXTexObj* tex = dDlst_shadowControl_c::getSimpleTex();

    field_0x6d4 = dComIfGd_setShadow(field_0x6d4, 1, mpMorf->getModel(), &shadow_pos, 800.0f, 40.0f,
                                     current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr,
                                     0, 1.0f, tex);
    if (field_0x6d4 != 0) {
        if (ret == 0 && tsubo != NULL && tsubo->model != NULL) {
            dComIfGd_addRealShadow(field_0x6d4, tsubo->model);
        }
        dComIfGd_addRealShadow(field_0x6d4, field_0x6d8);
    }
}

#include "d/d_snap.h"

/* 00003B4C-00003C84       .text _draw__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::_draw() {
    J3DModel* model = field_0x6d8;
    J3DModelData* model_data = model->getModelData();
    J3DModel* morf_model = mpMorf->getModel();

    if (field_0x777 != 0 || field_0x779 != 0) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(morf_model, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);

    mpMorf->entryDL();
    mBtpAnm.entry(model_data, field_0x6f0);
    mDoExt_modelEntryDL(model);
    mBtpAnm.remove(model_data);

    shadowDraw();

    dSnap_RegistFig(DSNAP_TYPE_UNK4B, this, 1.0f, 1.0f, 1.0f);

    if (l_HIO.children[mType].field_0x1c != 0) {
        cXyz pos = current.pos;
        pos.y = eyePos.y;
        GXColor unused_color0 = {0xFF, 0x00, 0x00, 0x80};
        GXColor unused_color1 = {0x00, 0x00, 0xFF, 0x80};
        GXColor unused_color2 = {0xFF, 0xFF, 0x00, 0x80};
    }

    return TRUE;
}

/* 00003C84-00003EDC       .text _execute__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::_execute() {
    if (!field_0x77a) {
        field_0x714 = current.pos.x;
        field_0x718 = current.pos.y;
        field_0x71c = current.pos.z;
        field_0x720 = current.angle.x;
        field_0x722 = current.angle.y;
        field_0x724 = current.angle.z;
        field_0x77a = 1;
    }

    m_jnt.setParam(l_HIO.children[mType].field_0xc, l_HIO.children[mType].field_0xe,
                   l_HIO.children[mType].field_0x10, l_HIO.children[mType].field_0x12,
                   l_HIO.children[mType].field_0x4, l_HIO.children[mType].field_0x6,
                   l_HIO.children[mType].field_0x8, l_HIO.children[mType].field_0xa,
                   l_HIO.children[mType].field_0x14);

    if (field_0x777 && !demoActorID) {
        if (g_dComIfG_gameInfo.save.getEvent().isEventBit(0x1)) {
            if (field_0x7c6 == 1) {
                actor_status = (actor_status & ~0x3F) | 0x28;
            }
            field_0x777 = 0;
        } else {
            return TRUE;
        }
    }

    partner_search();
    checkOrder();
    if (!demo()) {
        int event_idx = -1;
        if (dComIfGp_event_runCheck() && !eventInfo.checkCommandTalk()) {
            event_idx = isEventEntry();
        }
        if (event_idx >= 0) {
            event_proc(event_idx);
        } else {
            (this->*mActionFunc)(NULL);
        }
        fopAcM_posMoveF(this, mStts.GetCCMoveP());
        play_animation();
        mObjAcch.CrrPos(*dComIfG_Bgsp());
    }
    eventOrder();
    field_0x726 = current.angle;
    if (!field_0x778) {
        shape_angle = current.angle;
    }
    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    setMtx(false);
    setCollision(30.0f, 150.0f);
    return TRUE;
}

/* 00003EDC-00003F38       .text _delete__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::_delete() {
    dComIfG_resDelete(&mPhs, "Yw");
#if VERSION > VERSION_DEMO
    if (heap != NULL && mpMorf != NULL)
#else
    if (mpMorf != NULL)
#endif
    {
        mpMorf->stopZelAnime();
    }
    return TRUE;
}

/* 00003F38-0000417C       .text _create__11daNpc_Yw1_cFv */
cPhs_State daNpc_Yw1_c::_create() {
    fopAcM_ct(this, daNpc_Yw1_c);

    static u32 a_siz_tbl[] = {
        0,
    };

    cPhs_State state = dComIfG_resLoad(&mPhs, "Yw");
    if (state != cPhs_COMPLEATE_e) {
        return state;
    }

    if (!decideType(fopAcM_GetParam(this) & 0xFF)) {
        return cPhs_ERROR_e;
    }

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_siz_tbl[mType])) {
        return cPhs_ERROR_e;
    }

    fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
    fopAcM_setCullSizeBox(this, -40.0f, -20.0f, -40.0f, 40.0f, 210.0f, 40.0f);

    if (!createInit()) {
        return cPhs_ERROR_e;
    }

    return state;
}

#include "res/Object/Yw.h"

/* 00004448-0000469C       .text bodyCreateHeap__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::bodyCreateHeap() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Yw", dRes_ID_YW_BDL_YW_e);
    JUT_ASSERT(DEMO_SELECT(0x96B, 0x96F), a_mdl_dat != NULL);

    mpMorf = new mDoExt_McaMorf(a_mdl_dat, NULL, NULL, NULL, -1, 1.0f, 0, -1, 1, NULL, 0x80000, 0x11020022);
    if (!mpMorf) {
        return FALSE;
    }
    if (!mpMorf->getModel()) {
        mpMorf = NULL;
        return FALSE;
    }

    m_hed_jnt_num = a_mdl_dat->getJointName()->getIndex("head");
    JUT_ASSERT(DEMO_SELECT(0x979, 0x97D), m_hed_jnt_num >= 0);
    m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone");
    JUT_ASSERT(DEMO_SELECT(0x97B, 0x97F), m_bbone_jnt_num >= 0);

    mpMorf->getModel()->getModelData()->getJointNodePointer(m_hed_jnt_num)->setCallBack(nodeCB_Head);
    mpMorf->getModel()->getModelData()->getJointNodePointer(m_bbone_jnt_num)->setCallBack(nodeCB_BackBone);
#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    return TRUE;
}

/* 0000469C-00004930       .text headCreateHeap__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::headCreateHeap() {
    static u32 a_hed_bdl_resID_tbl[] = {
        dRes_ID_YW_BDL_YWHEAD01_e,
    };

    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes("Yw", (u16)a_hed_bdl_resID_tbl[mType]);
    JUT_ASSERT(DEMO_SELECT(0x998, 0x99c), a_mdl_dat != NULL);

    field_0x6d8 = mDoExt_J3DModel__create(a_mdl_dat, 0x80000, 0x15020022);
    if (field_0x6d8 == NULL) {
        return FALSE;
    }

    static s8 a_tex_pttrn_num_tbl[] = {0};
    if (!init_texPttrnAnm(a_tex_pttrn_num_tbl[mType], false)) {
        return FALSE;
    }

    m_hair1 = a_mdl_dat->getJointName()->getIndex("hair1");
    JUT_ASSERT(DEMO_SELECT(0x9a9, 0x9ad), m_hair1 >= 0);

    m_hair2 = a_mdl_dat->getJointName()->getIndex("hair2");
    JUT_ASSERT(DEMO_SELECT(0x9ab, 0x9af), m_hair2 >= 0);

    m_hair3 = a_mdl_dat->getJointName()->getIndex("hair3");
    JUT_ASSERT(DEMO_SELECT(0x9ad, 0x9b1), m_hair3 >= 0);

    field_0x6d8->getModelData()->getJointNodePointer(m_hair1)->setCallBack(nodeCB_Hair);
    field_0x6d8->getModelData()->getJointNodePointer(m_hair2)->setCallBack(nodeCB_Hair);
    field_0x6d8->getModelData()->getJointNodePointer(m_hair3)->setCallBack(nodeCB_Hair);
#if TARGET_PC
    field_0x6d8->setUserArea((uintptr_t)this);
#else
    field_0x6d8->setUserArea((u32)this);
#endif

    return TRUE;
}

/* 00004930-000049D0       .text CreateHeap__11daNpc_Yw1_cFv */
BOOL daNpc_Yw1_c::CreateHeap() {
    if (!bodyCreateHeap()) {
        return FALSE;
    }
    if (!headCreateHeap()) {
        mpMorf = NULL;
        return FALSE;
    }
    mAcchCir.SetWall(30.0f, 30.0f);
    mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir,
                 fopAcM_GetSpeed_p(this));
    return TRUE;
}

/* 000049D0-000049F0       .text daNpc_Yw1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Yw1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Yw1_c*)i_this)->_create();
}

/* 000049F0-00004A10       .text daNpc_Yw1_Delete__FP11daNpc_Yw1_c */
static BOOL daNpc_Yw1_Delete(daNpc_Yw1_c* i_this) {
    return ((daNpc_Yw1_c*)i_this)->_delete();
}

/* 00004A10-00004A30       .text daNpc_Yw1_Execute__FP11daNpc_Yw1_c */
static BOOL daNpc_Yw1_Execute(daNpc_Yw1_c* i_this) {
    return ((daNpc_Yw1_c*)i_this)->_execute();
}

/* 00004A30-00004A50       .text daNpc_Yw1_Draw__FP11daNpc_Yw1_c */
static BOOL daNpc_Yw1_Draw(daNpc_Yw1_c* i_this) {
    return ((daNpc_Yw1_c*)i_this)->_draw();
}

/* 00004A50-00004A58       .text daNpc_Yw1_IsDelete__FP11daNpc_Yw1_c */
static BOOL daNpc_Yw1_IsDelete(daNpc_Yw1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Yw1_Method = {
    (process_method_func)daNpc_Yw1_Create,
    (process_method_func)daNpc_Yw1_Delete,
    (process_method_func)daNpc_Yw1_Execute,
    (process_method_func)daNpc_Yw1_IsDelete,
    (process_method_func)daNpc_Yw1_Draw,
};

actor_process_profile_definition g_profile_NPC_YW1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_YW1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Yw1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_YW1_e,
    /* Actor SubMtd */ &l_daNpc_Yw1_Method,
    /* Status       */ 0x08 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
