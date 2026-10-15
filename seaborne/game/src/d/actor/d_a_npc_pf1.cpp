/**
 * d_a_npc_pf1.cpp
 * NPC - Maggie's Father (poor)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_npc_pf1.h"
#include "res/Object/Pf.h"

class daNpc_Pf1_HIO_c : public mDoHIO_entry_c {
public:
    daNpc_Pf1_HIO_c();
    virtual ~daNpc_Pf1_HIO_c() {}

public:
    struct hio_prm_c {
        /* 0x00 */ s16 mMaxHeadX;
        /* 0x02 */ s16 mMaxHeadY;
        /* 0x04 */ s16 mMinHeadX;
        /* 0x06 */ s16 mMinHeadY;
        /* 0x08 */ s16 mMaxBackBoneX;
        /* 0x0A */ s16 mMaxBackBoneY;
        /* 0x0C */ s16 mMinBackBoneX;
        /* 0x0E */ s16 mMinBackBoneY;
        /* 0x10 */ s16 mMaxTurnStep;
    };

public:
    /* 0x004 */ s8 mNo;
    /* 0x005 */ u8 field_0x5[0x8 - 0x5];
    /* 0x008 */ s32 field_0x8;
    /* 0x00C */ hio_prm_c mPrm;
    /* 0x01E */ s16 field_0x1e;
    /* 0x020 */ f32 m020;
    /* 0x024 */ u8 field_0x24[0x026 - 0x024];
    /* 0x026 */ s16 m026;
    /* 0x028 */ s16 m028;
    /* 0x02A */ u8 field_0x2a[0x2C - 0x2A];
    /* 0x02C */ f32 m02C;
    /* 0x030 */ f32 m030;
    /* 0x034 */ f32 m034;
    /* 0x038 */ f32 m038;
    /* 0x03C */ f32 m03C;
    /* 0x040 */ f32 m040;
    /* 0x044 */ f32 m044;
    /* 0x048 */ f32 m048;
};

static daNpc_Pf1_HIO_c l_HIO;
static u8 l_check_inf[0x50];
static u32 l_check_wrk;

/* 000000EC-00000150       .text __ct__15daNpc_Pf1_HIO_cFv */
daNpc_Pf1_HIO_c::daNpc_Pf1_HIO_c() {
    static u32 a_prm_tbl[16] = {
        0x200038E0, 0xEC78C720, 0x00001C70, 0x0000E390,
        0x04000400, 0x428C0000, 0x00000008, 0x04000000,
        0x3F800000, 0x3F666666, 0x3E99999A, 0x3E4CCCCD,
        0x41180000, 0x3F19999A, 0x44610000, 0x44834000,
    };

    memcpy(&mPrm, a_prm_tbl, 0x40);
    mNo = -1;
    field_0x8 = -1;
}

/* 00000198-000001E4       .text nodeCB_Head__FP7J3DNodei */
static BOOL nodeCB_Head(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Pf1_c* i_this = (daNpc_Pf1_c*)model->getUserArea();
        if (i_this) {
            i_this->_nodeCB_Head(i_node, model);
        }
    }

    return TRUE;
}

/* 000001E4-0000030C       .text _nodeCB_Head__11daNpc_Pf1_cFP7J3DNodeP8J3DModel */
void daNpc_Pf1_c::_nodeCB_Head(J3DNode* i_node, J3DModel* i_model) {
    static cXyz a_eye_pos_off(20.0f, -30.0f, 0.0f);
    u16 joint_number = ((J3DJoint*)(i_node))->getJntNo();
    mDoMtx_stack_c::copy(i_model->getAnmMtx(joint_number));
    mDoMtx_stack_c::multVecZero(&field_0x750);
    mDoMtx_stack_c::multVec(&a_eye_pos_off, &field_0x72c);
    mDoMtx_stack_c::YrotM(-m_jnt.getHead_y());
    mDoMtx_stack_c::ZrotM(-m_jnt.getHead_x());
    cMtx_copy(mDoMtx_stack_c::get(), j3dSys.mCurrentMtx);
    i_model->setAnmMtx(joint_number, mDoMtx_stack_c::now);
}

/* 00000348-00000394       .text nodeCB_BackBone__FP7J3DNodei */
static BOOL nodeCB_BackBone(J3DNode* i_node, int i_param) {
    if (i_param == 0) {
        daNpc_Pf1_c* actor = (daNpc_Pf1_c*)j3dSys.getModel()->getUserArea();
        if (actor != NULL) {
            actor->_nodeCB_BackBone(i_node, j3dSys.getModel());
        }
    }
    return TRUE;
}

/* 00000394-0000042C       .text _nodeCB_BackBone__11daNpc_Pf1_cFP7J3DNodeP8J3DModel */
void daNpc_Pf1_c::_nodeCB_BackBone(J3DNode* i_node, J3DModel* i_pModel) {
    s32 jntNo = ((J3DJoint*)i_node)->getJntNo();
    mDoMtx_stack_c::copy(i_pModel->getAnmMtx(jntNo));
    mDoMtx_stack_c::XrotM(m_jnt.getBackbone_y());
    mDoMtx_stack_c::ZrotM(m_jnt.getBackbone_x());
    cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    i_pModel->setAnmMtx(jntNo, mDoMtx_stack_c::get());
}

/* 0000042C-0000044C       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Pf1_c*)i_this)->CreateHeap();
}


static char* l_evn_tbl[] = { "dummy" };

/* 0000044C-000004CC       .text init_PF1_0__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::init_PF1_0() {
    if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_2D01)) {
        set_action(&daNpc_Pf1_c::wait_action1, NULL);
        return true;
    } else {
        return false;
    }
}

/* 000004CC-000006B8       .text createInit__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::createInit() {
    mEventIdx[0] = dComIfGp_evmng_getEventIdx(l_evn_tbl[0], 0xff);
    mEventCut.setActorInfo2("Pf1", this);

    int atSe = 0xff;
    u8 pathNo = (fopAcM_GetParam(this) >> 16) & 0xff;
    if (pathNo != 0xff) {
        mPathRun.setInf(pathNo, fopAcM_GetRoomNo(this), 1);
        if (mPathRun.isPath()) {
            fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
            atSe = 0xd9;
            set_pthPoint(0);
        } else {
            return false;
        }
    }

    if (!mPathRun.isPath()) {
        return false;
    }

    attention_info.flags = 10;
    switch (field_0x7bc) {
    case 0:
        break;
    }

    attention_info.distances[fopAc_Attn_TYPE_TALK_e] = 0xa9;
    attention_info.distances[fopAc_Attn_TYPE_SPEAK_e] = 0xa9;
    gravity = -4.5f;
    field_0x7b6 = 8;

    bool ret;
    switch (field_0x7bc) {
    case 0:
        ret = init_PF1_0();
        break;
    default:
        ret = false;
        break;
    }

    if (!ret) {
        return false;
    }

    field_0x71e = current.angle.x;
    field_0x720 = current.angle.y;
    field_0x722 = current.angle.z;
    shape_angle.x = field_0x71e;
    shape_angle.y = field_0x720;
    shape_angle.z = field_0x722;

    mStts.Init(atSe, 0xff, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);

    mObjAcch.CrrPos(*dComIfG_Bgsp());
    play_animation();
    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    mpMorf->setMorf(0.0f);
    setMtx(true);

    return true;
}

/* 000006B8-00000760       .text play_animation__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::play_animation() {
    u32 mtrlSndId = 0;
    play_btp_anm();
    if (mObjAcch.ChkGroundHit()) {
        mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(mObjAcch.m_gnd);
    }

    field_0x7a0 = mpMorf->play(&eyePos, mtrlSndId, dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    if (mpMorf->getFrame() < field_0x774) {
        field_0x7a0 = 1;
    }

    field_0x774 = mpMorf->getFrame();
}

/* 00000760-00000810       .text setMtx__11daNpc_Pf1_cFb */
void daNpc_Pf1_c::setMtx(bool i_setAttn) {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::ZXYrotM(field_0x71e, field_0x720, field_0x722);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();
    setAttention(i_setAttn);
}

/* 00000810-00000824       .text bckResID__11daNpc_Pf1_cFi */
int daNpc_Pf1_c::bckResID(int i_no) {
    static const int a_resID_tbl[8] = {
        0x00000004, 0x00000005, 0x00000006, 0x00000000,
        0x00000001, 0x00000002, 0x00000007, 0x00000003,
    };
    return a_resID_tbl[i_no];
}

/* 00000824-00000838       .text btpResID__11daNpc_Pf1_cFi */
s32 daNpc_Pf1_c::btpResID(int i_no) {
    static const s32 a_resID_tbl[2] = { dRes_ID_PF_BTP_MABA01_e, dRes_ID_PF_BTP_MABA01_e };

    return a_resID_tbl[i_no];
}

/* 00000838-00000958       .text setBtp__11daNpc_Pf1_cFScb */
bool daNpc_Pf1_c::setBtp(signed char i_no, bool i_modify) {
    J3DModel* a_model = mpMorf->getModel();
    if (i_no < 0) {
        return false;
    }

    int res_id = btpResID(i_no);
    J3DAnmTexPattern* a_btp = (J3DAnmTexPattern*)dComIfG_getObjectIDRes(field_0x6ce, res_id);
    JUT_ASSERT(0x1CE, a_btp != NULL);

    field_0x7b5 = i_no;
    if (field_0x7b5 == 1) {
        field_0x6ec = 2;
    } else {
        field_0x6ec = 0;
    }
    field_0x6ee = 0;

    return mBtpAnm.init(a_model->getModelData(), a_btp, 1, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, i_modify, 0) ? true : false;
}

/* 00000958-00000978       .text init_texPttrnAnm__11daNpc_Pf1_cFScb */
bool daNpc_Pf1_c::init_texPttrnAnm(signed char i_param_1, bool i_param_2) {
    return setBtp(i_param_1, i_param_2);
}

/* 00000978-00000A1C       .text play_btp_anm__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::play_btp_anm() {
    u8 frame_max = mBtpAnm.getBtpAnm()->getFrameMax();
    if (field_0x7b5 == 1) {
        return;
    }
    if (field_0x7b5 != 0 || cLib_calcTimer(&field_0x6ee) == 0) {
        field_0x6ec++;
        if (field_0x6ec >= frame_max) {
            if (field_0x7b5 != 0) {
                field_0x6ec = frame_max;
            } else {
                field_0x6ee = cLib_getRndValue(0x3C, 0x5A);
                field_0x6ec = 0;
            }
        }
    }
}

/* 00000A1C-00000AB8       .text setAnm_anm__11daNpc_Pf1_cFPQ211daNpc_Pf1_c9anm_prm_c */
void daNpc_Pf1_c::setAnm_anm(daNpc_Pf1_c::anm_prm_c* i_anmPrm) {
    if (i_anmPrm->m00 < 0 || field_0x7b6 == i_anmPrm->m00) {
        return;
    }

    dNpc_setAnmIDRes(mpMorf, i_anmPrm->field_0xc, i_anmPrm->m04, i_anmPrm->field_0x8, bckResID(i_anmPrm->m00), -1, field_0x6ce);
    field_0x7b6 = i_anmPrm->m00;
    field_0x7a0 = 0;
    field_0x7a1 = 0;
    field_0x774 = 0.0f;
}

/* 00000AB8-00000B28       .text setAnm_NUM__11daNpc_Pf1_cFii */
void daNpc_Pf1_c::setAnm_NUM(int param_1, int param_2) {
    static anm_prm_c a_anm_prm_tbl[8] = {
        {0, 0, {0}, 8.0f, 1.0f, 2},
        {1, 0, {0}, 8.0f, 1.0f, 2},
        {2, 0, {0}, 8.0f, 1.0f, 2},
        {3, 1, {0}, 8.0f, 1.0f, 2},
        {4, 0, {0}, 8.0f, 1.0f, 0},
        {5, 0, {0}, 8.0f, 1.0f, 0},
        {6, 0, {0}, 8.0f, 1.0f, 2},
        {7, 0, {0}, 8.0f, 1.0f, 2},
    };

    if (param_2 != 0) {
        init_texPttrnAnm(a_anm_prm_tbl[param_1].field_0x1, true);
    }
    setAnm_anm(&a_anm_prm_tbl[param_1]);
}

/* 00000B28-00000B94       .text setAnm__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[8] = {
        {-1, -1, {0}, 0.0f, 0.0f, -1},
        {0, 0, {0}, 8.0f, 1.0f, 2},
        {-1, -1, {0}, 0.0f, 0.0f, -1},
        {1, 0, {0}, 8.0f, 1.0f, 2},
        {4, 0, {0}, 8.0f, 1.0f, 0},
        {0, 0, {0}, 4.0f, 0.0f, 2},
        {0, 0, {0}, 8.0f, 1.0f, 2},
        {0, 0, {0}, 8.0f, 1.0f, 2},
    };
    init_texPttrnAnm(a_anm_prm_tbl[field_0x7b8].field_0x1, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7b8]);
}

/* 00000B94-00000B98       .text chngAnmTag__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::chngAnmTag() {
}

/* 00000B98-00000B9C       .text ctrlAnmTag__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::ctrlAnmTag() {
}

/* 00000B9C-00000BDC       .text chngAnmAtr__11daNpc_Pf1_cFUc */
void daNpc_Pf1_c::chngAnmAtr(unsigned char i_atr) {
    if (i_atr == field_0x7b3 || i_atr > 6) {
        return;
    }
    field_0x7b3 = i_atr;
    setAnm_ATR();
}

/* 00000BDC-00000C48       .text ctrlAnmAtr__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::ctrlAnmAtr() {
    switch (field_0x7b3) {
    case 3:
        break;
    case 4:
        if (field_0x7a0 != 0) {
            if (++field_0x7a1 > 2) {
                field_0x7b3 = 6;
                setAnm_NUM(0, 1);
            }
        }
        break;
    case 5:
        break;
    }
}

/* 00000C48-00000CAC       .text setAnm_ATR__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::setAnm_ATR() {
    static anm_prm_c a_anm_prm_tbl[6] = {
        {0, 0, {0}, 8.0f, 1.0f, 2},
        {3, 1, {0}, 8.0f, 1.0f, 2},
        {5, 0, {0}, 8.0f, 1.0f, 0},
        {6, 0, {0}, 8.0f, 1.0f, 2},
        {6, 0, {0}, 8.0f, 1.0f, 2},
        {7, 0, {0}, 8.0f, 1.0f, 2},
    };

    init_texPttrnAnm(a_anm_prm_tbl[field_0x7b3].field_0x1, true);
    setAnm_anm(&a_anm_prm_tbl[field_0x7b3]);
}

/* 00000CAC-00000D68       .text anmAtr__11daNpc_Pf1_cFUs */
void daNpc_Pf1_c::anmAtr(unsigned short i_param) {
    switch (i_param) {
    case 6:
        if (field_0x7be == 0) {
            chngAnmAtr(dComIfGp_getMesgAnimeAttrInfo());
            field_0x7be++;
        }

        {
            u8 tag = dComIfGp_getMesgAnimeTagInfo();
            if (tag != 0xFF && tag != field_0x7b4) {
                dComIfGp_clearMesgAnimeTagInfo();
                field_0x7b4 = tag;
                chngAnmTag();
            }
        }
        break;
    case 14:
        field_0x7be = 0;
        break;
    }

    ctrlAnmAtr();
    ctrlAnmTag();
}

/* 00000D68-00000E60       .text next_msgStatus__11daNpc_Pf1_cFPUl */
u16 daNpc_Pf1_c::next_msgStatus(u32* pMsgNo) {
    u16 status = 0xf;

    switch (*pMsgNo) {
    case 0x1b59:
        switch (mpCurrMsg->mSelectNum) {
        case 0:
            *pMsgNo = 0x1b5a;
            break;
        case 1:
            *pMsgNo = 0x1b5d;
            break;
        }

        field_0x7a5 = 1;
        break;
    case 0x1b5a:
        *pMsgNo = 0x1b61;
        break;
    case 0x1b61:
        *pMsgNo = 0x1b5b;
        break;
    case 0x1b5b:
        *pMsgNo = 0x1b62;
        break;
    case 0x1b62:
        dComIfGs_onEventBit(dSv_event_flag_c::UNK_0B04);
        status = 0x10;
        break;
    case 0x1b60:
        field_0x7a4 = 1;
        status = 0x10;
        break;
    case 0x1b5c:
        *pMsgNo = 0x1b5f;
        break;
    case 0x1b5e:
        *pMsgNo = 0x1b60;
        break;
    default:
        status = 0x10;
        break;
    }

    return status;
}

/* 00000E60-00000EC0       .text getMsg_PF1_0__11daNpc_Pf1_cFv */
u32 daNpc_Pf1_c::getMsg_PF1_0() {
    if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0B04)) {
        return 0x1B5C;
    }

    if (field_0x7a5) {
        return 0x1B5E;
    }

    return 0x1B59;
}

/* 00000EC0-00000EFC       .text getMsg__11daNpc_Pf1_cFv */
u32 daNpc_Pf1_c::getMsg() {
    u32 msgNo = 0;
    switch (field_0x7bc) {
        case 0:
            msgNo = getMsg_PF1_0();
            break;
    }
    return msgNo;
}

/* 00000EFC-00000F88       .text eventOrder__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::eventOrder() {
    s8 stt = field_0x7b7;
    if (stt == 1 || stt == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        if (field_0x7b7 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (stt >= 3) {
        field_0x78a = stt - 3;
        fopAcM_orderOtherEventId(this, mEventIdx[field_0x78a], 0xff, 0xffff, 0, 1);
    }
}

/* 00000F88-00001044       .text checkOrder__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::checkOrder() {
    if (eventInfo.getCommand() == dEvtCmd_INDEMO_e) {
        if (dComIfGp_evmng_startCheck(mEventIdx[field_0x78a]) && field_0x7b7 >= 3) {
            switch (field_0x78a) {
            case 0:
                // Fakematch: a body that only folds away after inlining keeps the cmpwi
                cLib_onBit<u32>(attention_info.flags, 0);
                break;
            }

            field_0x7b7 = 0;
            field_0x7b3 = 0xff;
            field_0x7b4 = 0xff;
        }
    } else if (eventInfo.getCommand() == dEvtCmd_INTALK_e) {
        if (field_0x7b7 == 1 || field_0x7b7 == 2) {
            field_0x7b7 = 0;
            field_0x7ae = 1;
        }
    }
}

/* 00001044-000010DC       .text chk_talk__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::chk_talk() {
    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x7a2 = dComIfGp_event_getPreItemNo();
            return true;
        } else {
            return false;
        }
    } else {
        field_0x7a2 = 0xFF;
        return true;
    }
}

/* 000010DC-0000111C       .text chk_parts_notMov__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::chk_parts_notMov() {
    return field_0x77e != m_jnt.getHead_y() ||
        field_0x780 != m_jnt.getBackbone_y() ||
        field_0x77c != current.angle.y;
}

#include "f_op/f_op_actor_mng.h"

/* 0000111C-00001170       .text searchByID__11daNpc_Pf1_cFUiPi */
fopAc_ac_c* daNpc_Pf1_c::searchByID(fpc_ProcID id, int* result) {
    fopAc_ac_c* actor = NULL;
    *result = 0;
    if (!fopAcM_SearchByID(id, &actor)) {
        *result = 1;
    }
    return actor;
}

/* 00001170-00001334       .text lookBack__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::lookBack() {
    field_0x77e = m_jnt.getHead_y();
    field_0x780 = m_jnt.getBackbone_y();
    field_0x77c = current.angle.y;
    cXyz lookPos;
    cXyz pos = current.pos;
    pos.y = eyePos.y;
    lookPos.setall(0.0f);

    int result;
    fopAc_ac_c* actor;
    cXyz* pLookPos = NULL;
    s16 targetY = current.angle.y;
    bool someBool = field_0x7af;
    s8 state = field_0x7ba;
    switch (state) {
    case 1:
        field_0x738 = dNpc_playerEyePos(-20.0f);
        lookPos = field_0x738;
        pLookPos = &lookPos;
        break;
    case 2:
        lookPos = field_0x738;
        pLookPos = &lookPos;
        break;
    case 3:
        targetY = field_0x79e;
        break;
    case 4:
        actor = searchByID(field_0x700, &result);
        if (actor != NULL && result == 0) {
            field_0x738 = actor->current.pos;
            field_0x738.y = actor->eyePos.y;
            lookPos = field_0x738;
            pLookPos = &lookPos;
        }
        break;
    }
    m_jnt.lookAtTarget_2(&current.angle.y, pLookPos, pos, targetY, l_HIO.field_0x1e, someBool);
}

/* 00001334-000013B4       .text chkAttention__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::chkAttention() {
    if (dComIfGp_getAttention().LockonTruth()) {
        return this == dComIfGp_getAttention().LockonTarget(0);
    }

    return this == dComIfGp_getAttention().ActionTarget(0);
}

/* 000013B4-0000140C       .text setAttention__11daNpc_Pf1_cFb */
void daNpc_Pf1_c::setAttention(bool i_force) {
    attention_info.position.set(field_0x750.x, field_0x750.y + l_HIO.m020, field_0x750.z);

    if (field_0x784 == 0 && !i_force) {
        return;
    }

    eyePos.set(field_0x72c.x, field_0x72c.y, field_0x72c.z);
}

/* 0000140C-00001494       .text decideType__11daNpc_Pf1_cFi */
bool daNpc_Pf1_c::decideType(int i_type) {
    if (field_0x7bb > 0) {
        return true;
    }

    field_0x7bb = 1;
    field_0x7bc = 0;
    strcpy(field_0x6ce, "Pf");

    bool ret = false;
    if (field_0x7bb != -1 && field_0x7bc != -1) {
        ret = true;
    }

    return ret;
}

/* 00001494-00001534       .text privateCut__11daNpc_Pf1_cFi */
void daNpc_Pf1_c::privateCut(int i_staffIdx) {
    static char* a_cut_tbl[] = { "DUMMY" };

    if (i_staffIdx == -1) {
        return;
    }

    field_0x7b2 = dComIfGp_evmng_getMyActIdx(i_staffIdx, a_cut_tbl, ARRAY_SIZE(a_cut_tbl), TRUE, 0);

    if (field_0x7b2 == -1) {
        dComIfGp_evmng_cutEnd(i_staffIdx);
    } else {
        dComIfGp_evmng_getIsAddvance(i_staffIdx);
        dComIfGp_evmng_cutEnd(i_staffIdx);
    }
}

/* 00001534-00001558       .text endEvent__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x7b3 = 0xff;
    field_0x7b4 = 0xff;
}

/* 00001558-00001590       .text isEventEntry__11daNpc_Pf1_cFv */
int daNpc_Pf1_c::isEventEntry() {
    return dComIfGp_evmng_getMyStaffId(mEventCut.getActorName());
}

/* 00001590-0000161C       .text event_proc__11daNpc_Pf1_cFi */
void daNpc_Pf1_c::event_proc(int i_staffIdx) {
    if (dComIfGp_evmng_endCheck(mEventIdx[field_0x78a])) {
        switch (field_0x78a) {
        case 0:
            break;
        }

        endEvent();
    } else {
        if (!mEventCut.cutProc()) {
            privateCut(i_staffIdx);
        }
    }
}

/* 0000161C-000016C8       .text set_action__11daNpc_Pf1_cFM11daNpc_Pf1_cFPCvPvPv_iPv */
BOOL daNpc_Pf1_c::set_action(int (daNpc_Pf1_c::*i_func)(void*), void* i_arg) {
    if (field_0x6f0 != i_func) {
        if (field_0x6f0) {
            field_0x7bd = 9;
            (this->*field_0x6f0)(i_arg);
        }

        field_0x6f0 = i_func;
        field_0x7bd = 0;
        (this->*field_0x6f0)(i_arg);
    }

    return TRUE;
}

/* 000016C8-0000179C       .text setStt__11daNpc_Pf1_cFSc */
void daNpc_Pf1_c::setStt(s8 i_stt) {
    u8 prev_stt = field_0x7b8;
    field_0x7b8 = i_stt;
    switch (field_0x7b8) {
    case 0:
        break;
    case 1:
    case 4:
    case 6:
    case 7:
        field_0x7b7 = 0;
        speedF = 0.0f;
        break;
    case 2:
        field_0x7b7 = 0;
        speedF = 0.0f;
        field_0x7b3 = 0xff;
        field_0x7b4 = 0xff;
        field_0x7be = 0;
        field_0x7b9 = prev_stt;
        break;
    case 3:
        field_0x7b7 = 0;
        field_0x798 = cLib_getRndValue(0xb4, 0x5a);
        break;
    case 5:
        field_0x7b7 = 0;
        field_0x796 = 0x1e;
        field_0x798 = 0;
        break;
    }

    setAnm();
}

#include "d/actor/d_a_tama.h"
#include "SSystem/SComponent/c_counter.h"

/* 0000179C-00001958       .text createTama__11daNpc_Pf1_cFf */
void daNpc_Pf1_c::createTama(float i_dis) {
    cXyz plrEyePos;
    csXyz local_5c(0.0f, 0.0f, 0.0f);

    cXyz local_30 = eyePos;
    local_30.y = 15.0f + eyePos.y;
    plrEyePos = dNpc_playerEyePos(-20.0f);

    f32 a = (plrEyePos - eyePos).absXZ();
    local_5c.y = cLib_targetAngleY(&local_30, &plrEyePos);
    local_5c.x = cLib_targetAngleX(&local_30, &plrEyePos);
    daTama_c* tama = (daTama_c*)fopAcM_fastCreate(0x1D6, 0, &eyePos, fopAcM_GetRoomNo(this), &local_5c, NULL, 0xFF, NULL, NULL);
    if (tama != NULL) {
        fpc_ProcID procID;
        if (this != NULL) {
#if TARGET_PC
            procID = mBsPcId;
#else
            procID = base.base.mBsPcId;
#endif
        } else {
            procID = -1;
        }
        tama->setPartnerID(procID);
        tama->setDis(i_dis);
        tama->setSpd(50.0f);
    }
    return;
}

/* 00001958-00001A80       .text chk_areaIN__11daNpc_Pf1_cFf4cXyz */
bool daNpc_Pf1_c::chk_areaIN(f32 i_dist, cXyz i_pos) {
    bool ret = (dComIfGp_getLinkPlayer()->current.pos - i_pos).absXZ() < i_dist;

    if (ret && g_Counter.mCounter0 % 3 == 0) {
        createTama(i_dist);
    }

    return ret;
}


/* 00001A80-00001ACC       .text endEvent_check__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::endEvent_check() {
    if (field_0x7a5) {
        return !dComIfGs_isEventBit(dSv_event_flag_c::UNK_0B04);
    }
    return false;
}

/* 00001ACC-00001BEC       .text startEvent_check__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::startEvent_check() {
    cXyz pos(field_0x70c.x, 100.0f + field_0x70c.y, field_0x70c.z);

    if (chk_areaIN(l_HIO.m044, pos)) {
        f32 sqDist = PSVECSquareDistance(&current.pos, &dComIfGp_getLinkPlayer()->current.pos);
        f32 dist = std::sqrtf(sqDist);

        if (dist < 210.0f || field_0x6ba != 0) {
            return true;
        }
    }

    return false;
}

/* 00001BEC-00001C94       .text set_pthPoint__11daNpc_Pf1_cFUc */
void daNpc_Pf1_c::set_pthPoint(unsigned char i_point) {
    if (mPathRun.isPath()) {
        mPathRun.setIdx(i_point);
        current.pos = mPathRun.getPoint(mPathRun.getIdx());
        if (mPathRun.nextIdx()) {
            cXyz point = mPathRun.getPoint(mPathRun.getIdx());
            current.angle.y = cLib_targetAngleY(&current.pos, &point);
        }
    }
}

/* 00001C94-00001EAC       .text chk_attn__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::chk_attn() {
    f32 dist = std::sqrtf((current.pos - dComIfGp_getLinkPlayer()->current.pos).abs2XZ());
    f32 heightDiff = current.pos.y - dComIfGp_getLinkPlayer()->current.pos.y;
    s16 angleDiff =
        cLib_targetAngleY(&current.pos, &dComIfGp_getLinkPlayer()->current.pos) - current.angle.y;

    if (field_0x7ba == 1) {
        return dist < 200.0f && abs(angleDiff) / DEG2S_CONSTANT < 90.0f &&
               std::fabsf(heightDiff) < 300.0f;
    } else {
        return dist < 200.0f && abs(angleDiff) / DEG2S_CONSTANT < 60.0f &&
               std::fabsf(heightDiff) < 300.0f;
    }
}

/* 00001EAC-00001F68       .text setBikon__11daNpc_Pf1_cF4cXyz */
void daNpc_Pf1_c::setBikon(cXyz i_param_1) {
    delBikon();
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::YrotM(current.angle.y);
    cXyz local_18;
    mDoMtx_stack_c::multVec(&i_param_1, &local_18);
    dComIfGp_particle_set(dPa_name::ID_IT_SN_PF_BIKON00, &local_18);
}

/* 00001F68-00001F6C       .text delBikon__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::delBikon() {
}

/* 00001F6C-00001F74       .text wait_1__11daNpc_Pf1_cFv */
int daNpc_Pf1_c::wait_1() {
    return 0x1;
}

/* 00001F74-00002030       .text regret__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::regret() {
    if (field_0x7ae) {
        if (chk_talk()) {
            setStt(2);
        }

        return TRUE;
    }

    if (!endEvent_check()) {
        field_0x7b7 = 0;
        if (startEvent_check()) {
            setStt(5);
        }
    } else {
        field_0x7b7 = 2;
    }

    field_0x7ba = 0;
    field_0x7af = 1;

    if (field_0x7a0 != 0) {
        setStt(3);
    }

    return TRUE;
}

/* 00002030-00002350       .text attk_1__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::attk_1() {
    f32 dist = (current.pos - dComIfGp_getLinkPlayer()->current.pos).absXZ();

    if (field_0x7ae) {
        if (chk_talk()) {
            setStt(6);
            setStt(2);
        }
        return TRUE;
    }

    field_0x7ba = 0;
    field_0x7af = 1;

    if (!(field_0x7b6)) {
        if (field_0x798 == 0) {
            s16 targetAngle = cLib_targetAngleY(&current.pos, &dComIfGp_getLinkPlayer()->current.pos);
            cLib_addCalcAngleS(&current.angle.y, targetAngle, 4, 0x1000, 0x80);

            if (abs((s16)(current.angle.y - targetAngle)) < 0x800) {
                cXyz vec(0.0f, -40.0f, 60.0f);
                JAIZelBasic::zel_basic->seStart(JA_SE_CM_CMN_NOTICE, &current.pos, 0, 0, 1.0f, 1.0f,
                                                 -1.0f, -1.0f, 0);
                setBikon(vec);
                field_0x798 = 0x14;
            }
        } else {
            if (!cLib_calcTimer(&field_0x798)) {
                setAnm_NUM(2, 1);
            }
        }
        return TRUE;
    }

    if (field_0x7b7 != 1 && dist < 210.0f) {
        field_0x7b7 = 1;
    }

    if (!cLib_calcTimer(&field_0x796)) {
        if (chk_areaIN(l_HIO.m048, field_0x70c) == false) {
            setStt(6);
            return TRUE;
        }
    }

    cLib_addCalcAngleS(&current.angle.y,
                       cLib_targetAngleY(&current.pos, &dComIfGp_getLinkPlayer()->current.pos),
                       l_HIO.m026, l_HIO.m028, 0x80);
    cLib_chaseF(&speedF, l_HIO.m03C, l_HIO.m040);

    f32 playSpeed;
    f32 rate;
    rate = speedF * l_HIO.m038;
    playSpeed = 0.5f;
    if (rate < playSpeed) {
        playSpeed = 0.5f;
    } else {
        playSpeed = rate;
    }
    mpMorf->setPlaySpeed(playSpeed);

    return TRUE;
}

/* 00002350-000025D0       .text walk_1__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::walk_1() {
    if (!mPathRun.isPath()) {
        return TRUE;
    }

    if (dPath_ChkClose(mPathRun.getPath())) {
        if (mPathRun.chkPointPass(current.pos, (bool)mPathRun.getDir())) {
            mPathRun.nextIdxAuto();
        }
    } else {
        return TRUE;
    }

    cXyz point = mPathRun.getPoint(mPathRun.getIdx());
    cLib_addCalcAngleS(&current.angle.y, cLib_targetAngleY(&current.pos, &point), l_HIO.m026,
                       l_HIO.m028, 0x80);

    f32 spd = l_HIO.m030;
    if (!cLib_calcTimer(&field_0x798) || field_0x7ae != 0) {
        spd = 0.0f;
    }

    if (!endEvent_check() && startEvent_check()) {
        speedF = 0.0f;
        field_0x798 = 0;
        setStt(5);
        field_0x7ba = 0;
        field_0x7af = 1;
        return TRUE;
    }

    cLib_chaseF(&speedF, spd, l_HIO.m034);

    f32 playSpeed;
    f32 rate;
    rate = speedF * l_HIO.m02C;
    playSpeed = 0.5f;
    if (rate < playSpeed) {
        playSpeed = 0.5f;
    } else {
        playSpeed = rate;
    }
    mpMorf->setPlaySpeed(playSpeed);

    if ((int)(0.5f + spd) == 0 && (int)speedF == 0) {
        if (field_0x7ae) {
            if (chk_talk()) {
                setStt(2);
            }

            return TRUE;
        }

        setStt(4);
        return TRUE;
    }

    field_0x7b7 = 0;
    if (endEvent_check()) {
        field_0x7b7 = 2;
    }

    field_0x7ba = 0;
    field_0x7af = 1;

    return TRUE;
}

#include "d/d_com_inf_game.h"

/* 000025D0-000026E4       .text wait_2__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::wait_2() {
    if (field_0x7ae) {
        if (chk_talk()) {
            setStt(2);
        }
        return TRUE;
    }

    if (!dComIfGs_isEventBit(dSv_event_flag_c::UNK_0B04) ||
        chk_areaIN(l_HIO.m048, field_0x70c) == false) {
        mPathRun.setNearPathIndx(&current.pos, 0.0f);
        setStt(3);
        return TRUE;
    }

    field_0x7b7 = 2;
    field_0x7af = 1;
    if (chk_attn()) {
        field_0x7ba = 1;
        return TRUE;
    }

    field_0x7ba = 0;
    return TRUE;
}

/* 000026E4-00002874       .text wait_3__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::wait_3() {
    f32 dist = (current.pos - dComIfGp_getLinkPlayer()->current.pos).absXZ();

    if (field_0x7ae) {
        if (chk_talk()) {
            setStt(2);
            field_0x7b0 = 0;
        }
        return TRUE;
    }

    field_0x7b0 = dist > 300.0f;
    if (field_0x7b0) {
        setStt(3);
        field_0x7ba = 0;
        field_0x7af = 1;
        return TRUE;
    }

    field_0x7b7 = 2;
    field_0x7af = 1;
    if (chk_attn()) {
        field_0x7ba = 1;
        return TRUE;
    }

    field_0x7ba = 0;
    return TRUE;
}

/* 00002874-00002980       .text talk_1__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::talk_1() {
    int ret = chk_parts_notMov();

    cLib_addCalcAngleS(&current.angle.y,
                       cLib_targetAngleY(&current.pos, &dComIfGp_getLinkPlayer()->current.pos), 4,
                       l_HIO.field_0x1e, 0x80);

    u16 status = talk(1);
    if (mpCurrMsg == NULL) {
        return TRUE;
    }

    if (status == fopMsgStts_CLOSE_WAIT_e && field_0x7a0 != 0 && mCurrMsgNo == 0x1b60) {
        fopMsgM_messageSendOn();
    }

    switch (mpCurrMsg->mStatus) {
    case fopMsgStts_BOX_OPENING_e:
    case fopMsgStts_MSG_TYPING_e:
        break;
    case fopMsgStts_MSG_DESTROYED_e:
        field_0x7a2 = 0xff;
        field_0x7ae = 0;
        setStt(field_0x7b9);
        endEvent();
        break;
    }

    return ret;
}

/* 00002980-00002A8C       .text wait_action1__11daNpc_Pf1_cFPv */
BOOL daNpc_Pf1_c::wait_action1(void*) {
    switch (field_0x7bd) {
        case 0:
            setStt(3);
            field_0x7bd += 1;
            break;
        case 1:
        case 2:
        case 3:
            field_0x7ad = chkAttention();
            switch (field_0x7b8) {
                case 1:
                    field_0x784 = wait_1();
                    break;
                case 2:
                    field_0x784 = talk_1();
                    break;
                case 3:
                    field_0x784 = walk_1();
                    break;
                case 4:
                    field_0x784 = regret();
                    break;
                case 5:
                    field_0x784 = attk_1();
                    break;
                case 6:
                    field_0x784 = wait_2();
                    break;
                case 7:
                    field_0x784 = wait_3();
                    break;
            }
            break;
        case 9:
            break;
    }

    return TRUE;
}

/* 00002A8C-00002BCC       .text demo__11daNpc_Pf1_cFv */
bool daNpc_Pf1_c::demo() {
    if (demoActorID == 0) {
        if (field_0x7b1 != 0) {
            field_0x7b1 = 0;
        }
    } else {
        if (field_0x7b1 == 0) {
            field_0x7b1 = 1;
            field_0x7aa = 0;
            m_jnt.setHead_y(0);
            m_jnt.setHead_x(0);
            m_jnt.setBackBone_y(0);
            m_jnt.setBackBone_x(0);
        }

        dDemo_actor_c* demo_actor = dComIfGp_demo_getActor(demoActorID);
        if (mBtpAnm.getBtpAnm()) {
            u8 frame_max = mBtpAnm.getBtpAnm()->getFrameMax();
            field_0x6ec += 1;
            if (field_0x6ec >= frame_max) {
                field_0x6ec = frame_max;
            }
        }

        J3DAnmTexPattern* btp_data = demo_actor->getP_BtpData(field_0x6ce);
        if (btp_data != NULL) {
            mBtpAnm.init(mpMorf->getModel()->getModelData(), btp_data, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, FALSE);
            field_0x7b5 = 2;
            field_0x6ec = 0;
        }

        dDemo_setDemoData(this, 0x6A, mpMorf, field_0x6ce);
    }

    return field_0x7b1;
}

/* 00002BCC-00002C5C       .text shadowDraw__11daNpc_Pf1_cFv */
void daNpc_Pf1_c::shadowDraw() {
    cXyz pos(current.pos.x, current.pos.y + 150.0f, current.pos.z);
    field_0x6d4 = dComIfGd_setShadow(field_0x6d4, 1, mpMorf->getModel(), &pos, 800.0f, 40.0f, current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr, 0, 1.0, dDlst_shadowControl_c::getSimpleTex());
}

#include "d/d_snap.h"

/* 00002C5C-00002D30       .text _draw__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();

    if (field_0x7a8 || field_0x7ab) {
        return TRUE;
    }

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);

    mBtpAnm.entry(modelData, field_0x6ec);
    mpMorf->entryDL();
    modelData->getMaterialTable().removeTexNoAnimator(mBtpAnm.getBtpAnm());

    shadowDraw();

    GXColor color1 = {0x00, 0xff, 0x00, 0x80};
    GXColor color2 = {0xff, 0xff, 0x00, 0x80};
    GXColor color3 = {0xff, 0x00, 0x00, 0x80};
    GXColor color4 = {0x00, 0x00, 0xff, 0x80};

    dSnap_RegistFig(DSNAP_TYPE_UNK5B, this, 1.0f, 1.0f, 1.0f);

    return TRUE;
}

/* 00002D30-00002F94       .text _execute__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::_execute() {
    if (field_0x7ac == 0) {
        field_0x70c.x = current.pos.x;
        field_0x70c.y = current.pos.y;
        field_0x70c.z = current.pos.z;
        field_0x718 = current.angle.x;
        field_0x71a = current.angle.y;
        field_0x71c = current.angle.z;
        field_0x7ac = 1;
    }

    m_jnt.setParam(
        l_HIO.mPrm.mMaxBackBoneX,
        l_HIO.mPrm.mMaxBackBoneY,
        l_HIO.mPrm.mMinBackBoneX,
        l_HIO.mPrm.mMinBackBoneY,
        l_HIO.mPrm.mMaxHeadX,
        l_HIO.mPrm.mMaxHeadY,
        l_HIO.mPrm.mMinHeadX,
        l_HIO.mPrm.mMinHeadY,
        l_HIO.mPrm.mMaxTurnStep
    );

    if (field_0x7a8 && demoActorID == 0) {
        return TRUE;
    }

    checkOrder();
    if (!demo()) {
        int staff_id = -1;
        if (dComIfGp_event_runCheck() && eventInfo.checkCommandTalk() == false) {
            staff_id = isEventEntry();
        }

        if (staff_id >= 0) {
            event_proc(staff_id);
        } else {
            (this->*field_0x6f0)(0);
        }

        field_0x6ba = 0;
        lookBack();
        fopAcM_posMoveF(this, mStts.GetCCMoveP());
        mObjAcch.CrrPos(*dComIfG_Bgsp());

        if (mObjAcch.GetGroundH() != -G_CM3D_F_INF) {
            cM3dGPla* pla = dComIfG_Bgsp()->GetTriPla(mObjAcch.m_gnd);
            if (pla != NULL) {
                field_0x768 = pla->mNormal;
            }
        }

        play_animation();
    } else {
        field_0x7a8 = 0;
    }

    eventOrder();
    field_0x71e = current.angle.x;
    field_0x720 = current.angle.y;
    field_0x722 = current.angle.z;
    if (!field_0x7aa) {
        shape_angle = current.angle;
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    setMtx(false);

    if (!field_0x7b1) {
        setCollision(100.0f, 200.0f);
    }

    return TRUE;
}

/* 00002F94-00002FF8       .text _delete__11daNpc_Pf1_cFv */
#include "c/c_dylink.h"

BOOL daNpc_Pf1_c::_delete() {
    cDyl_Unlink(0x1D6);
    dComIfG_resDelete(&field_0x6c4, field_0x6ce);
    delBikon();

    if (heap != NULL) {
        if (mpMorf != NULL) {
            mpMorf->stopZelAnime();
        }
    }

    return TRUE;
}

#include "c/c_dylink.h"
#include "d/d_com_inf_game.h"
#include "f_op/f_op_actor_mng.h"

/* 00002FF8-00003258       .text _create__11daNpc_Pf1_cFv */
cPhs_State daNpc_Pf1_c::_create() {
    fopAcM_ct(this, daNpc_Pf1_c);

#if TARGET_PC
    if (!decideType(mParameters & 0xff)) {
#else
    if (!decideType(base.base.mParameters & 0xff)) {
#endif
        return cPhs_ERROR_e;
    }

    s32 resLoadResult = dComIfG_resLoad(&field_0x6c4, field_0x6ce);
    field_0x7a7 = resLoadResult == cPhs_COMPLEATE_e;

    if ((u8)field_0x7a7 == 0) {
        return resLoadResult;
    }

    s32 linkResult = cDyl_LinkASync(0x1d6);
    if (linkResult != cPhs_COMPLEATE_e) {
        return linkResult;
    }

    static u32 a_siz_tbl[2] = { 0x0, 0x0 };

    if (!fopAcM_entrySolidHeap(this, CheckCreateHeap, a_siz_tbl[field_0x7bb])) {
        return cPhs_ERROR_e;
    }

    cullMtx = (MtxP)mpMorf->getModel()->getBaseTRMtx();
    fopAcM_SetMtx(this, cullMtx);
    fopAcM_setCullSizeBox(this, -100.0f, -20.0f, -80.0f, 100.0f, 180.0f, 140.0f);

    if (!createInit()) {
        return cPhs_ERROR_e;
    }

    return resLoadResult;
}

/* 00003524-00003798       .text bodyCreateHeap__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::bodyCreateHeap() {
    J3DModelData* a_mdl_dat = (J3DModelData*)dComIfG_getObjectIDRes(field_0x6ce, dRes_ID_PF_BDL_PF_e);
    JUT_ASSERT(0x803, a_mdl_dat != NULL);

    mpMorf = new mDoExt_McaMorf(
        a_mdl_dat,
        NULL, NULL, NULL,
        J3DFrameCtrl::EMode_NULL, 1.0f, 0, -1, 1, NULL,
        0x80000, 0x11020022
    );

    if (!mpMorf) {
        return FALSE;
    } else if (mpMorf->getModel() == NULL) {
        mpMorf = NULL;
        return FALSE;
    }

    if (!init_texPttrnAnm(0, false)) {
        mpMorf = NULL;
        return FALSE;
    }
    m_hed_jnt_num = a_mdl_dat->getJointName()->getIndex("head");
    JUT_ASSERT(0x817, m_hed_jnt_num >= 0);
    m_bbone_jnt_num = a_mdl_dat->getJointName()->getIndex("backbone1");
    JUT_ASSERT(0x819, m_bbone_jnt_num >= 0);

    mpMorf->getModel()->getModelData()->getJointNodePointer(m_hed_jnt_num & 0xffff)->setCallBack(nodeCB_Head);
    mpMorf->getModel()->getModelData()->getJointNodePointer(m_bbone_jnt_num & 0xffff)->setCallBack(nodeCB_BackBone);
#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif

    return TRUE;
}

/* 00003798-0000381C       .text CreateHeap__11daNpc_Pf1_cFv */
BOOL daNpc_Pf1_c::CreateHeap() {
    if (!bodyCreateHeap()) {
        return FALSE;
    }

    mAcchCir.SetWall(30.0f, 100.0f);
    mObjAcch.Set(&current.pos, &old.pos, this, 1, &mAcchCir, &speed, NULL, NULL);

    return TRUE;
}

/* 0000381C-0000383C       .text daNpc_Pf1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Pf1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Pf1_c*)i_this)->_create();
}

/* 0000383C-0000385C       .text daNpc_Pf1_Delete__FP11daNpc_Pf1_c */
static BOOL daNpc_Pf1_Delete(daNpc_Pf1_c* i_this) {
    return ((daNpc_Pf1_c*)i_this)->_delete();
}

/* 0000385C-0000387C       .text daNpc_Pf1_Execute__FP11daNpc_Pf1_c */
static BOOL daNpc_Pf1_Execute(daNpc_Pf1_c* i_this) {
    return ((daNpc_Pf1_c*)i_this)->_execute();
}

/* 0000387C-0000389C       .text daNpc_Pf1_Draw__FP11daNpc_Pf1_c */
static BOOL daNpc_Pf1_Draw(daNpc_Pf1_c* i_this) {
    return ((daNpc_Pf1_c*)i_this)->_draw();
}

/* 0000389C-000038A4       .text daNpc_Pf1_IsDelete__FP11daNpc_Pf1_c */
static BOOL daNpc_Pf1_IsDelete(daNpc_Pf1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Pf1_Method = {
    (process_method_func)daNpc_Pf1_Create,
    (process_method_func)daNpc_Pf1_Delete,
    (process_method_func)daNpc_Pf1_Execute,
    (process_method_func)daNpc_Pf1_IsDelete,
    (process_method_func)daNpc_Pf1_Draw,
};

actor_process_profile_definition g_profile_NPC_PF1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_PF1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Pf1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_PF1_e,
    /* Actor SubMtd */ &l_daNpc_Pf1_Method,
    /* Status       */ 0x08 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
