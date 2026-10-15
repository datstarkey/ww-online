/**
 * d_a_npc_kp1.cpp
 * NPC - Maggie (rich)
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_npc_kp1.h"
#include "SSystem/SComponent/c_phase.h"
#include "d/d_com_inf_game.h"
#include "f_op/f_op_actor.h"
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_ext.h"
#include "d/d_snap.h"
#include "res/Object/Kp.h"

class daNpc_Kp1_HIO_c : public mDoHIO_entry_c {
public:
    struct hio_prm_c {
        s16 field_0;
        s16 field_2;
        s16 field_4;
        s16 field_6;
        s16 field_8;
        s16 field_A;
        s16 field_C;
        s16 field_E;
        s16 field_10;
        s16 field_12;
        f32 mAttentionArrowYOffset;
        f32 field_18;
    }; // Size: 0x1C

    daNpc_Kp1_HIO_c();
    virtual ~daNpc_Kp1_HIO_c() {}

    void genMessage(JORMContext* ctx) {}

public:
    /* 0x04 */ s8 mNo;
    /* 0x05 */ s8 field_0x5;
    /* 0x06 */ s8 field_0x6;
    /* 0x07 */ s8 field_0x7;
    /* 0x08 */ int field_0x8;
    /* 0x0C */ hio_prm_c mPrmTbl;
};

static daNpc_Kp1_HIO_c l_HIO;

/* 000000EC-00000150       .text __ct__15daNpc_Kp1_HIO_cFv */
daNpc_Kp1_HIO_c::daNpc_Kp1_HIO_c() {
    static hio_prm_c a_prm_tbl = {
        0x1FFE, 0x38E0, 0xF448, 0xC720, 0x0000, 0x0000, 0x0000, 0x0000, 0x05DC, 0x0400, 170.0f, 0.0f,
    };
    memcpy(&mPrmTbl, &a_prm_tbl, sizeof(hio_prm_c));
    mNo = -1;
    field_0x8 = -1;
}

/* 00000198-0000035C       .text nodeCallBack_Kp__FP7J3DNodei */
static BOOL nodeCallBack_Kp(J3DNode* i_node, int i_calcTiming) {
    if (i_calcTiming == J3DNodeCBCalcTiming_In) {
        J3DModel* model = j3dSys.getModel();
        daNpc_Kp1_c* kp1Actor = (daNpc_Kp1_c*)(model->getUserArea());
        if (kp1Actor) {
            static cXyz a_att_pos_offst(0.0f, 0.0f, 0.0f);
            static cXyz a_eye_pos_offst(20.0f, -20.0f, 0.0f);
            u16 jointIdx = ((J3DJoint*)(i_node))->getJntNo();
            mDoMtx_stack_c::copy(model->getAnmMtx(jointIdx));
            if (jointIdx == kp1Actor->getHeadJntNum()) {
                MTXMultVec(mDoMtx_stack_c::get(), &a_att_pos_offst, kp1Actor->getAttPos());
                mDoMtx_stack_c::YrotM(-kp1Actor->getHead_y());
                mDoMtx_stack_c::ZrotM(-kp1Actor->getHead_x());
                MTXMultVec(mDoMtx_stack_c::get(), &a_eye_pos_offst, kp1Actor->getEyePos());
            } else if (jointIdx == kp1Actor->getBackboneJntNum()) {
                mDoMtx_stack_c::XrotM(kp1Actor->getBackbone_y());
                mDoMtx_stack_c::ZrotM(-kp1Actor->getBackbone_x());
            }
            cMtx_copy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
            model->setAnmMtx(jointIdx, mDoMtx_stack_c::get());
        }
    }
    return TRUE;
}

/* 00000398-000004D0       .text createInit__11daNpc_Kp1_cFv */
u8 daNpc_Kp1_c::createInit() {
    if (g_dComIfG_gameInfo.save.mSavedata.mEvent.isEventBit(0x2D01) == 0) {
        return 0;
    }

    mEventCut.setActorInfo2("Kp1", this);
    attention_info.flags = fopAc_Attn_LOCKON_TALK_e | fopAc_Attn_ACTION_SPEAK_e;
    attention_info.distances[1] = 0xab;
    attention_info.distances[3] = 0xab;
    gravity = -4.0f;
    field_0x7BE = 0xFF;

    field_0x7A0.x = current.pos.x;
    field_0x7A0.y = current.pos.y;
    field_0x7A0.z = current.pos.z;

    set_action((int (daNpc_Kp1_c::*)(void*))&daNpc_Kp1_c::wait_action1, NULL);

    shape_angle.x = current.angle.x;
    shape_angle.y = current.angle.y;
    shape_angle.z = current.angle.z;

    mStts.Init(0xFF, 0xFF, this);
    mCyl.SetStts(&mStts);
    mCyl.Set(dNpc_cyl_src);
    mpMorf->setMorf(0.0f);

    field_0x7C4 = 1;
    setMtx();

    return 1;
}

/* 000004D0-00000658       .text setMtx__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::setMtx() {
    if ((u8)field_0x7CF == 0) {
        playTexPatternAnm();
        field_0x7BC = mpMorf->play(&eyePos, 0, 0);
        if (mpMorf->getFrame() < field_0x7AC) {
            field_0x7BC = 1;
        }
        field_0x7AC = mpMorf->getFrame();
        mObjAcch.CrrPos(*dComIfG_Bgsp());
    }

    tevStr.mRoomNo = dComIfG_Bgsp()->GetRoomId(mObjAcch.m_gnd);
    tevStr.mEnvrIdxOverride = dComIfG_Bgsp()->GetPolyColor(mObjAcch.m_gnd);
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::YrotM(current.angle.y);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    mpMorf->calc();
    mDoMtx_stack_c::copy(mpMorf->getModel()->getAnmMtx(m_handL_jnt_num));
    field_0x6D0->setBaseTRMtx(mDoMtx_stack_c::get());
    field_0x6D0->calc();
    field_0x6D4->setBaseTRMtx(mDoMtx_stack_c::get());
    field_0x6D4->calc();
    setAttention();
}

/* 00000658-00000760       .text anmResID__11daNpc_Kp1_cFiPiPi */
bool daNpc_Kp1_c::anmResID(int i_num, int* o_bck_num, int* o_bas_num) {
    static const int a_anm_idx_tbl[6][2] = {
        {5, -1}, {1, -1}, {0, -1}, {3, -1}, {2, -1}, {4, -1},
    };
    JUT_ASSERT(298, 0 <= i_num && i_num < ANM_END);
    JUT_ASSERT(299, o_bck_num && o_bas_num);
    *o_bck_num = a_anm_idx_tbl[i_num][0];
    *o_bas_num = a_anm_idx_tbl[i_num][1];
    return true;
}

/* 00000760-000007FC       .text BtpNum2ResID__11daNpc_Kp1_cFiPi */
void daNpc_Kp1_c::BtpNum2ResID(int i_num, int* o_btp_num) {
    static const int a_btp_arc_ix_tbl[2] = {9, 9};
    JUT_ASSERT(321, 0 <= i_num && i_num < TEXPATTERN_END);
    *o_btp_num = a_btp_arc_ix_tbl[i_num];
}

/* 000007FC-00000840       .text setAnm_tex__11daNpc_Kp1_cFSc */
void daNpc_Kp1_c::setAnm_tex(signed char param_1) {
    if (param_1 >= 0 && param_1 != field_0x7D5) {
        field_0x7D5 = param_1;
        initTexPatternAnm(true);
    }
}

/* 00000840-00000970       .text init_btp__11daNpc_Kp1_cFbi */
bool daNpc_Kp1_c::init_btp(bool param_1, int param_2) {
    J3DModelData* modelData = mpMorf->getModel()->getModelData();
    if (param_2 >= 0) {
        int btpId;
        BtpNum2ResID(param_2, &btpId);
        m_head_tex_pattern = (J3DAnmTexPattern*)dComIfG_getObjectIDRes("Kp", btpId);
        JUT_ASSERT(354, m_head_tex_pattern != 0);
        if (field_0x6E4.init(modelData, m_head_tex_pattern, TRUE, 2, 1.0f, 0, -1, param_1, FALSE) == 0) {
            return false;
        }
        field_0x6F8 = 0;
        field_0x6FA = 0;
        if (field_0x7D5 == 1) {
            field_0x6F8 = 1;
        }
    }
    return true;
}

/* 00000970-000009B4       .text initTexPatternAnm__11daNpc_Kp1_cFb */
bool daNpc_Kp1_c::initTexPatternAnm(bool param_1) {
    bool var_31 = false;
    if (init_btp(param_1, field_0x7D5)) {
        var_31 = true;
    }
    return var_31;
}

/* 000009B4-00000A80       .text playTexPatternAnm__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::playTexPatternAnm() {
    bool timer_ended = true;

    if (field_0x7D5 != 1) {
        if (field_0x7D5 == 0) {
            timer_ended = (cLib_calcTimer(&field_0x6FA) == 0) ? true : false;
        }

        if (timer_ended) {
            s16 anm_duration = m_head_tex_pattern->getFrameMax();

            if (!(++field_0x6F8 < anm_duration)) {
                if (field_0x7D5 != 0) {
                    field_0x6F8 = m_head_tex_pattern->getFrameMax();
                } else {
                    field_0x6F8 = 0;
                    field_0x6FA = cM_rndF(60.0f) + 30.0f;
                }
            }
        }
    }
}

/* 00000A80-00000B4C       .text setAnm_anm__11daNpc_Kp1_cFPQ211daNpc_Kp1_c9anm_prm_c */
s32 daNpc_Kp1_c::setAnm_anm(daNpc_Kp1_c::anm_prm_c* i_anm_ptr) {
    s32 result = 0;
    if (i_anm_ptr->field_0x0 >= 0 && field_0x7D6 != i_anm_ptr->field_0x0) {
        field_0x7D6 = i_anm_ptr->field_0x0;
        if (mpMorf != NULL) {
            int bck_num;
            int bas_num;
            anmResID(field_0x7D6, &bck_num, &bas_num);
            if (bck_num >= 0) {
                dNpc_setAnmIDRes(mpMorf, i_anm_ptr->field_0xC, i_anm_ptr->field_0x4, i_anm_ptr->field_0x8, bck_num, bas_num, "Kp");
            }
            result = 1;
        }
        field_0x7AC = 0.0f;
        field_0x7BD = 0;
        field_0x7BC = 0;
    }
    return result;
}

/* 00000B4C-00000BB8       .text setAnm_NUM__11daNpc_Kp1_cFii */
void daNpc_Kp1_c::setAnm_NUM(int param_1, int param_2) {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 0, 8.0f, 1.0f, 2},
        {1, 0, 0, 8.0f, 1.0f, 2},
        {2, 0, 0, 8.0f, 1.0f, 2},
        {3, 0, 0, 8.0f, 1.0f, 2},
        {4, 0, 0, 8.0f, 1.0f, 2},
        {5, 0, 0, 8.0f, 1.0f, 2},
    };
    if (param_2 != 0) {
        setAnm_tex(a_anm_prm_tbl[param_1].field_0x1);
    }
    setAnm_anm(&a_anm_prm_tbl[param_1]);
}

/* 00000BB8-00000C20       .text setAnm__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::setAnm() {
    static anm_prm_c a_anm_prm_tbl[] = {
        {-1, -1, 0, 0.0f, 0.0f, -1},
        {0, 0, 0, 8.0f, 1.0f, 2},
        {-1, -1, 0, 0.0f, 0.0f, -1},
    };
    setAnm_tex(a_anm_prm_tbl[field_0x7D8].field_0x1);
    setAnm_anm(&a_anm_prm_tbl[field_0x7D8]);
}

/* 00000C20-00000C2C       .text chngAnmTag__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::chngAnmTag() {
    switch (field_0x7D4) {
        case 0:
            break;
    }
}

/* 00000C2C-00000C38       .text ctrlAnmTag__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::ctrlAnmTag() {
    switch (field_0x7D3) {
        case 0:
            break;
    }
}

/* 00000C38-00000CA4       .text chngAnmAtr__11daNpc_Kp1_cFUc */
void daNpc_Kp1_c::chngAnmAtr(u8 param_1) {
    if (param_1 >= 7) {
        return;
    }
    if (param_1 == field_0x7D3) {
        return;
    }

    field_0x6D8 = 0;
    if (param_1 == 2) {
        field_0x6D8 = 2;
    } else if (param_1 == 3) {
        field_0x6D8 = 1;
    }

    field_0x7D3 = param_1;
    setAnm_ATR(1);
}

/* 00000CA4-00000D54       .text ctrlAnmAtr__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::ctrlAnmAtr() {
    switch ((u8)field_0x7D3) {
        case 0:
            break;
        case 1:
            if ((s8)field_0x7BC != 0) {
                if ((s8)(field_0x7BD += 1) >= 2) {
                    field_0x7D3 = 0;
                    setAnm_NUM(0, 1);
                }
            }
            break;
        case 6:
            if ((s8)field_0x7BC != 0) {
                if ((s8)(field_0x7BD += 1) >= 1) {
                    field_0x7D3 = 0;
                    setAnm_NUM(0, 1);
                }
            }
            break;
    }
}

/* 00000D54-00000DBC       .text setAnm_ATR__11daNpc_Kp1_cFi */
void daNpc_Kp1_c::setAnm_ATR(int param_1) {
    static anm_prm_c a_anm_prm_tbl[] = {
        {0, 0, 0, 8.0f, 1.0f, 2},
        {1, 0, 0, 8.0f, 1.0f, 2},
        {2, 0, 0, 8.0f, 1.0f, 2},
        {3, 0, 0, 8.0f, 1.0f, 2},
        {4, 0, 0, 8.0f, 1.0f, 2},
        {5, 0, 0, 8.0f, 1.0f, 2},
        {1, 1, 0, 8.0f, 1.0f, 2},
    };
    if (param_1 != 0) {
        setAnm_tex(a_anm_prm_tbl[field_0x7D3].field_0x1);
    }
    setAnm_anm(&a_anm_prm_tbl[field_0x7D3]);
}

/* 00000DBC-00000E78       .text anmAtr__11daNpc_Kp1_cFUs */
void daNpc_Kp1_c::anmAtr(u16 i_msgStatus) {
    u8 tag;

    if (i_msgStatus == fopMsgStts_MSG_TYPING_e) {
        if (field_0x7DE == 0) {
            field_0x7D4 = 0xff;
            chngAnmAtr(g_dComIfG_gameInfo.play.mMesgAnime);
            field_0x7DE++;
        }
        tag = g_dComIfG_gameInfo.play.mMesgAnimeTagInfo;
        if ((g_dComIfG_gameInfo.play.mMesgAnimeTagInfo != 0xff) && (g_dComIfG_gameInfo.play.mMesgAnimeTagInfo != field_0x7D4)) {
            g_dComIfG_gameInfo.play.mMesgAnimeTagInfo = 0xff;
            field_0x7D4 = tag;
            chngAnmTag();
        }
    } else if (i_msgStatus == fopMsgStts_MSG_DISPLAYED_e) {
        field_0x7DE = 0;
    }
    ctrlAnmAtr();
    ctrlAnmTag();
}

/* 00000E78-00000EE0       .text setStt__11daNpc_Kp1_cFSc */
void daNpc_Kp1_c::setStt(signed char param_1) {
    s8 uVar1 = field_0x7D8;
    field_0x6D8 = 0;
    field_0x7D8 = param_1;
    switch ((s8)field_0x7D8) {
        case 2:
            field_0x7DA = 1;
            m_jnt.setTrn();
            field_0x7D3 = 0xFF;
            field_0x7D9 = uVar1;
            field_0x7C2 = 0;
            break;
        default:
        case 1:
            setAnm();
            break;
    }
}

/* 00000EE0-0000102C       .text next_msgStatus__11daNpc_Kp1_cFPUl */
u16 daNpc_Kp1_c::next_msgStatus(u32* i_msgNo) {
    u16 result = fopMsgStts_MSG_CONTINUES_e;
    switch (*i_msgNo) {
        case 0x1E84:
            *i_msgNo = 0x1E85;
            break;
        case 0x1E85:
            *i_msgNo = 0x1E86;
            break;
        case 0x1E88:
            *i_msgNo = 0x1E89;
            break;
        case 0x1E8A:
            *i_msgNo = 0x1E8B;
            break;
        case 0x1E8B:
            *i_msgNo = 0x1E8C;
            break;
        case 0x1E8C:
            *i_msgNo = 0x1E8D;
            break;
        case 0x1E8D:
            *i_msgNo = 0x1E8E;
            break;
        case 0x1E8E:
            *i_msgNo = 0x1E8F;
            break;
        case 0x1E8F:
            switch (mpCurrMsg->mSelectNum) {
                case 0:
                    *i_msgNo = 0x1E91;
                    break;
                case 1:
                    *i_msgNo = 0x1E90;
                    break;
            }
            break;
        case 0x1E96:
            *i_msgNo = 0x1E97;
            field_0x7C2 = 1;
            break;
        case 0x1E97:
            *i_msgNo = 0x1E98;
            break;
        case 0x1E98:
            *i_msgNo = 0x1E99;
            break;
        case 0x1E99:
            *i_msgNo = 0x1E9A;
            break;
        case 0x1E9A:
            *i_msgNo = 0x1E9B;
            break;
        case 0x1E9B:
            *i_msgNo = 0x1E9C;
            break;
        case 0x1E9C:
            *i_msgNo = 0x1E9D;
            break;
        case 0x1E91:
            field_0x7D7 = 3;
            goto default_msg;
        case 0x1E9D:
            field_0x7D7 = 4;
            goto default_msg;
        case 0x1E90:
            field_0x7C1 = 1;
        default:
        default_msg:
            result = fopMsgStts_MSG_ENDS_e;
            break;
    }
    return result;
}

/* 0000102C-00001178       .text getMsg__11daNpc_Kp1_cFv */
u32 daNpc_Kp1_c::getMsg() {
    u8 msg = field_0x7BE;

    if (msg != 0xFF) {
        if (msg == 0x9B) {
            return 0x1E96;
        }
        return (msg == 0x45) ? 0x1EA0 : 0x1E9F;
    }

    if (field_0x7BF != 0) {
        field_0x7BF = 0;
        return 0x1E92;
    }
    if (field_0x7C0 != 0) {
        field_0x7C0 = 0;
        return 0x1E9E;
    }

    dSv_player_get_bag_item_c* getBagItem = &g_dComIfG_gameInfo.save.mSavedata.mPlayer.mGetBagItem;
    if (getBagItem->isReserve(0xF) && g_dComIfG_gameInfo.save.mSavedata.mPlayer.mBagItem.checkReserveItem(0x9B) == 0) {
        return (g_dComIfG_gameInfo.save.mSavedata.mEvent.getEventReg(0xCCFF) >= 1) ? 0x1E84 : 0x1E83;
    }
    if (getBagItem->isReserve(0xE)) {
        return getBagItem->isBeast(0) ? 0x1E87 : 0x1E88;
    }
    if (field_0x7C1 != 0) {
        return 0x1E93;
    }
    return 0x1E8A;
}

/* 00001178-00001204       .text eventOrder__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::eventOrder() {
    static char* a_demo_name_tbl[] = {"GET_MAGYS_LTTR", "GET_KAKERA_HRT"};

    if (field_0x7D7 == 1 || field_0x7D7 == 2) {
        eventInfo.onCondition(dEvtCnd_CANTALK_e);
        eventInfo.onCondition(dEvtCnd_CANTALKITEM_e);
        if (field_0x7D7 == 1) {
            fopAcM_orderSpeakEvent(this);
        }
    } else if (field_0x7D7 >= 3) {
        fopAcM_orderOtherEvent2(this, a_demo_name_tbl[field_0x7D7 - 3], dEvtFlag_NOPARTNER_e);
    }
}

/* 00001204-000012E4       .text checkOrder__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::checkOrder() {
    if (eventInfo.getCommand() == dEvtCmd_INDEMO_e) {
        if (dComIfGp_evmng_startCheck("GET_MAGYS_LTTR") && field_0x7D7 == 3) {
            field_0x7D7 = 0;
        } else if (dComIfGp_evmng_startCheck("GET_KAKERA_HRT") && field_0x7D7 == 4) {
            field_0x7D7 = 0;
        }
    } else if (eventInfo.getCommand() == dEvtCmd_INTALK_e) {
        if (field_0x7D7 == 1 || field_0x7D7 == 2) {
            field_0x7D7 = 0;
            field_0x7CD = 1;
        }
    }
}

/* 000012E4-00001474       .text lookBack__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::lookBack() {
    cXyz* dstPos;
    s16 desiredYRot;
    bool headOnlyFollow;
    cXyz targetPos;
    cXyz srcPos;

    srcPos = current.pos;
    srcPos.y = eyePos.y;
    targetPos.x = 0.0f;
    targetPos.y = 0.0f;
    targetPos.z = 0.0f;
    dstPos = NULL;
    desiredYRot = current.angle.y;
    headOnlyFollow = field_0x7CE;

    switch (field_0x7DA) {
        case 1:
            targetPos = dNpc_playerEyePos(-20.0f);
            dstPos = &targetPos;
            srcPos = current.pos;
            srcPos.y = eyePos.y;
            break;
        case 2:
            targetPos = field_0x7A0;
            dstPos = &targetPos;
            srcPos = current.pos;
            srcPos.y = eyePos.y;
            break;
        case 3:
            desiredYRot = field_0x7BA;
            break;
        case 0:
            break;
    }

    if (m_jnt.trnChk()) {
        cLib_addCalcAngleS2(&field_0x7B8, l_HIO.mPrmTbl.field_12, 4, 0x800);
    } else {
        field_0x7B8 = 0;
    }

    m_jnt.lookAtTarget(&current.angle.y, dstPos, srcPos, desiredYRot, field_0x7B8, headOnlyFollow);
}

/* 00001474-000014F4       .text chkAttention__11daNpc_Kp1_cFv */
bool daNpc_Kp1_c::chkAttention() {
    dAttention_c& attention = dComIfGp_getAttention();
    if (attention.LockonTruth()) {
        return this == attention.LockonTarget(0);
    }
    return this == attention.ActionTarget(0);
}

/* 000014F4-00001558       .text setAttention__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::setAttention() {
    f32 f1 = current.pos.z;
    f32 f2 = current.pos.y + l_HIO.mPrmTbl.mAttentionArrowYOffset;
    attention_info.position.set(current.pos.x, f2, f1);
    if (!field_0x7C4 && !field_0x7C8) {
        return;
    }
    f2 = getEyePos()->z;
    f1 = getEyePos()->y;
    eyePos.set(getEyePos()->x, f1, f2);
    field_0x7C4 = 0;
}

/* 00001558-000015F4       .text chk_talk__11daNpc_Kp1_cFv */
bool daNpc_Kp1_c::chk_talk() {
    bool ret = true;
    field_0x7BE = 0xff;
    if (dComIfGp_event_chkTalkXY()) {
        if (dComIfGp_evmng_ChkPresentEnd()) {
            field_0x7BE = dComIfGp_event_getPreItemNo();
        } else {
            ret = false;
        }
    }
    return ret;
}

/* 000015F4-00001620       .text decideType__11daNpc_Kp1_cFi */
bool daNpc_Kp1_c::decideType(int) {
    field_0x7DB = 0xFF;
    switch (fopAcM_GetName(this)) {
        case fpcNm_NPC_KP1_e:
            field_0x7DB = 0;
            field_0x7DC = 0;
            break;
    }

    return true;
}

/* 00001620-000016A8       .text event_actionInit__11daNpc_Kp1_cFi */
void daNpc_Kp1_c::event_actionInit(int i_staffId) {
    int* pActNo = dComIfGp_evmng_getMyIntegerP(i_staffId, "ActNo");
    dComIfGp_evmng_getMyIntegerP(i_staffId, "Timer");
    if (pActNo != NULL) {
        field_0x7D2 = *pActNo;
    }
}

/* 000016A8-00001778       .text event_action__11daNpc_Kp1_cFv */
u8 daNpc_Kp1_c::event_action() {
    u8 ret = FALSE;
    switch (field_0x7D2) {
        case 0: {
            fpc_ProcID id = fopAcM_createItemForPresentDemo(&current.pos, dItemNo_MAGGIES_LETTER_e);
            if (id != fpcM_ERROR_PROCESS_ID_e) {
                dComIfGp_event_setItemPartnerId(id);
                ret = TRUE;
            }
            break;
        }
        case 1: {
            fpc_ProcID id = fopAcM_createItemForPresentDemo(&current.pos, dItemNo_HEART_PIECE_e);
            if (id != fpcM_ERROR_PROCESS_ID_e) {
                dComIfGp_event_setItemPartnerId(id);
                ret = TRUE;
            }
            break;
        }
        default:
            ret = TRUE;
            break;
    }
    return ret;
}

/* 00001778-00001880       .text privateCut__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::privateCut() {
    static char* cut_name_tbl[] = {
        "ACTION",
    };
    int staffIdx = dComIfGp_evmng_getMyStaffId("Kp1", NULL, 0);
    if (staffIdx != -1) {
        field_0x7D1 = dComIfGp_evmng_getMyActIdx(staffIdx, cut_name_tbl, ARRAY_SIZE(cut_name_tbl), TRUE, 0);
        if (field_0x7D1 == -1) {
            dComIfGp_evmng_cutEnd(staffIdx);
        } else {
            if (dComIfGp_evmng_getIsAddvance(staffIdx)) {
                switch ((u8)field_0x7D1) {
                    case 0:
                        event_actionInit(staffIdx);
                        break;
                }
            }
            u8 end;
            switch ((u8)field_0x7D1) {
                case 0:
                    end = event_action();
                    break;
                default:
                    end = TRUE;
                    break;
            }
            if (end) {
                dComIfGp_evmng_cutEnd(staffIdx);
            }
        }
    }
}

/* 00001880-000018A0       .text endEvent__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::endEvent() {
    dComIfGp_event_reset();
    field_0x7D3 = 0xFF;
}

/* 000018A0-00001998       .text event_proc__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::event_proc() {
    if (dComIfGp_evmng_endCheck("GET_MAGYS_LTTR")) {
        endEvent();
        field_0x7D7 = 1;
        field_0x7BF = 1;
    } else if (dComIfGp_evmng_endCheck("GET_KAKERA_HRT")) {
        dComIfGs_setEventReg(0xCCFF, 0);
        endEvent();
        field_0x7D7 = 1;
        field_0x7C0 = 1;
    } else {
        if (!mEventCut.cutProc()) {
            privateCut();
        }
        lookBack();
    }
    shape_angle.x = current.angle.x;
    shape_angle.y = current.angle.y;
    shape_angle.z = current.angle.z;
}

/* 00001998-00001A44       .text set_action__11daNpc_Kp1_cFM11daNpc_Kp1_cFPCvPvPv_iPv */
BOOL daNpc_Kp1_c::set_action(ActionFunc i_action, void* i_prm) {
    if (mCurrActionFunc != i_action) {
        if (mCurrActionFunc != 0) {
            field_0x7DD = -1;
            (this->*mCurrActionFunc)(i_prm);
        }
        mCurrActionFunc = i_action;
        field_0x7DD = 0;
        (this->*mCurrActionFunc)(i_prm);
    }
    return TRUE;
}

/* 00001A44-00001AFC       .text wait01__11daNpc_Kp1_cFv */
BOOL daNpc_Kp1_c::wait01() {
    if (field_0x7D7 == 3 || field_0x7D7 == 4 || field_0x7D7 == 1) {
        return TRUE;
    }
    if (field_0x7CD != 0) {
        if (chk_talk()) {
            setStt(2);
        }
    } else {
        field_0x7D7 = 2;
        if (field_0x7CC != 0) {
            field_0x7DA = 1;
            m_jnt.setTrn();
        } else {
            field_0x7DA = 3;
            field_0x7BA = mRot.y;
            m_jnt.setTrn();
        }
    }
    return TRUE;
}

/* 00001AFC-00001BD4       .text talk01__11daNpc_Kp1_cFv */
BOOL daNpc_Kp1_c::talk01() {
    talk(1);
    if (mpCurrMsg != NULL) {
        switch (mpCurrMsg->mStatus) {
            case fopMsgStts_MSG_DESTROYED_e:
                switch (mCurrMsgNo) {
                    case 0x1E9D:
                        dComIfGs_setReserveItemEmpty();
                        break;
                }
                field_0x7BE = 0xff;
                setStt(field_0x7D9);
                field_0x7D0 = 0;
                field_0x7CD = 0;
                endEvent();
                break;
            case fopMsgStts_BOX_OPENING_e:
            case fopMsgStts_MSG_TYPING_e:
                if (field_0x7C2 != 0) {
                    dComIfGp_evmng_CancelPresent();
                    field_0x7C2 = 0;
                }
                break;
        }
    }
    return TRUE;
}

/* 00001BD4-00001C88       .text wait_action1__11daNpc_Kp1_cFPv */
int daNpc_Kp1_c::wait_action1(void*) {
    if (field_0x7DD == 0) {
        setStt(1);
        field_0x7DD++;
    } else if (field_0x7DD != -1) {
        field_0x7CC = chkAttention();
        switch (field_0x7D8) {
            case 2:
                field_0x7C8 = talk01();
                break;
            case 1:
                field_0x7C8 = wait01();
                break;
            default:
                field_0x7C8 = 0;
                break;
        }
        lookBack();
    }
    return 1;
}

/* 00001C88-00001DB8       .text demo__11daNpc_Kp1_cFv */
u8 daNpc_Kp1_c::demo() {
    if (demoActorID == 0) {
        if ((u8)field_0x7CF != 0) {
            field_0x7CF = 0;
        }
    } else {
        field_0x7CF = 1;
        dDemo_actor_c* actor = dComIfGp_demo_getActor(demoActorID);
        if ((u8)(field_0x6F8 += 1) >= m_head_tex_pattern->getFrameMax()) {
            field_0x6F8 = m_head_tex_pattern->getFrameMax();
        }
        J3DAnmTexPattern* btp = actor->getP_BtpData("Kp");
        if (btp != NULL) {
            m_head_tex_pattern = btp;
            if (field_0x6E4.init(mpMorf->getModel()->getModelData(), m_head_tex_pattern, TRUE, 2, 1.0f, 0, -1, true, FALSE) != 0) {
                field_0x6F8 = 0;
            }
        }
        dDemo_setDemoData(
            this,
            dDemo_actor_c::ENABLE_TRANS_e | dDemo_actor_c::ENABLE_ROTATE_e | dDemo_actor_c::ENABLE_ANM_e | dDemo_actor_c::ENABLE_ANM_FRAME_e,
            mpMorf,
            "Kp",
            0,
            NULL,
            0,
            0
        );
    }
    return (u8)field_0x7CF;
}

/* 00001DB8-00001E9C       .text shadowDraw__11daNpc_Kp1_cFv */
void daNpc_Kp1_c::shadowDraw() {
    cXyz pos(current.pos.x, 150.0f + current.pos.y, current.pos.z);

    mShadowId = dComIfGd_setShadow(mShadowId, 1, mpMorf->getModel(), &pos, 800.0f, 40.0f,
                                   current.pos.y, mObjAcch.GetGroundH(), mObjAcch.m_gnd, &tevStr);

    if (mShadowId != 0) {
        switch (field_0x6D8) {
            case 1:
                dComIfGd_addRealShadow(mShadowId, field_0x6D0);
                break;
            case 2:
                dComIfGd_addRealShadow(mShadowId, field_0x6D4);
                break;
        }
    }
}

/* 00001E9C-00001FA4       .text _draw__11daNpc_Kp1_cFv */
BOOL daNpc_Kp1_c::_draw() {
    J3DModel* model = mpMorf->getModel();
    J3DModelData* modelData = model->getModelData();

    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(model, &tevStr);
    field_0x6E4.entry(modelData, field_0x6F8);
    mpMorf->entryDL();
    modelData->getMaterialTable().removeTexNoAnimator(field_0x6E4.getBtpAnm());

    J3DModel* itm_model;
    switch (field_0x6D8) {
        case 1:
            itm_model = field_0x6D0;
            break;
        case 2:
            itm_model = field_0x6D4;
            break;
        default:
            itm_model = NULL;
            break;
    }
    if (itm_model != NULL) {
        g_env_light.setLightTevColorType(itm_model, &tevStr);
        mDoExt_modelEntryDL(itm_model);
    }

    shadowDraw();
    dSnap_RegistFig(DSNAP_TYPE_PM1, this, 1.0f, 1.0f, 1.0f);

    // Color literals for debug drawing. Unused in non-debug builds.
    (GXColor){0xFF, 0x00, 0x00, 0x80};
    (GXColor){0x00, 0x00, 0xFF, 0x80};

    return TRUE;
}

/* 00001FA4-00002100       .text _execute__11daNpc_Kp1_cFv */
BOOL daNpc_Kp1_c::_execute() {
    if (field_0x7C3 == 0) {
        mPos.x = current.pos.x;
        mPos.y = current.pos.y;
        mPos.z = current.pos.z;
        mRot.x = current.angle.x;
        mRot.y = current.angle.y;
        mRot.z = current.angle.z;
        field_0x7C3 = 1;
    }
    m_jnt.setParam(
        l_HIO.mPrmTbl.field_8,
        l_HIO.mPrmTbl.field_A,
        l_HIO.mPrmTbl.field_C,
        l_HIO.mPrmTbl.field_E,
        l_HIO.mPrmTbl.field_0,
        l_HIO.mPrmTbl.field_2,
        l_HIO.mPrmTbl.field_4,
        l_HIO.mPrmTbl.field_6,
        l_HIO.mPrmTbl.field_10
    );
    checkOrder();
    if (demo() == 0) {
        if (dComIfGp_event_runCheck() && !eventInfo.checkCommandTalk()) {
            event_proc();
        } else {
            (this->*mCurrActionFunc)(NULL);
            shape_angle.x = current.angle.x;
            shape_angle.y = current.angle.y;
            shape_angle.z = current.angle.z;
        }
    }
    eventOrder();
    if ((u8)field_0x7CF == 0) {
        fopAcM_posMoveF(this, mStts.GetCCMoveP());
    }
    setMtx();
    setCollision(60.0f, 170.0f);
    return TRUE;
}

/* 00002100-0000215C       .text _delete__11daNpc_Kp1_cFv */
BOOL daNpc_Kp1_c::_delete() {
    dComIfG_resDelete(&mPhase, "Kp");
#if TARGET_PC
    if (heap != NULL && mpMorf != NULL) {
#else
    if ((u32)heap != 0U && mpMorf != NULL) {
#endif
        mpMorf->stopZelAnime();
    }
    return 1;
}

/* 0000215C-0000217C       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return ((daNpc_Kp1_c*)i_this)->CreateHeap();
}

/* 0000217C-000022A0       .text _create__11daNpc_Kp1_cFv */
cPhs_State daNpc_Kp1_c::_create() {
    static int a_heap_size_tbl[] = {0x272E0};

    fopAcM_ct_Retail(this, daNpc_Kp1_c);

    cPhs_State phase = dComIfG_resLoad(&mPhase, "Kp");
    if (phase != cPhs_COMPLEATE_e) {
        return phase;
    }
    if (!decideType(fopAcM_GetParam(this) & 0xFF)) {
        return cPhs_ERROR_e;
    }
    if (fopAcM_entrySolidHeap(this, CheckCreateHeap, a_heap_size_tbl[field_0x7DB])) {
        fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
        fopAcM_setCullSizeBox(this, -50.0f, -20.0f, -50.0f, 50.0f, 170.0f, 50.0f);
    } else {
        return cPhs_ERROR_e;
    }
    if (createInit() == 0) {
        phase = cPhs_ERROR_e;
    }
    return phase;
}

daNpc_Kp1_c::daNpc_Kp1_c() {}

/* 000026D0-00002B14       .text CreateHeap__11daNpc_Kp1_cFv */
BOOL daNpc_Kp1_c::CreateHeap() {
    static u8 a_tex_pattern_num_tbl[1] = {0};

    J3DModelData* a_mdl_data = (J3DModelData*)dComIfG_getObjectIDRes("Kp", dRes_ID_KP_BDL_KP_e);
    JUT_ASSERT(1580, a_mdl_data != NULL);
    mpMorf = new mDoExt_McaMorf(
        a_mdl_data,
        NULL,
        NULL,
        (J3DAnmTransform*)dComIfG_getObjectIDRes("Kp", dRes_ID_KP_BCK_WAIT01_e),
        J3DFrameCtrl::EMode_LOOP,
        1.0f,
        0,
        -1,
        1,
        NULL,
        0x80000,
        0x11020002
    );
    if (mpMorf) {
        if (mpMorf->getModel()) {
            m_head_jnt_num = a_mdl_data->getJointName()->getIndex("head");
            JUT_ASSERT(1599, m_head_jnt_num >= 0);
            m_backbone_jnt_num = a_mdl_data->getJointName()->getIndex("backbone");
            JUT_ASSERT(1601, m_backbone_jnt_num >= 0);
            m_handL_jnt_num = a_mdl_data->getJointName()->getIndex("handL");
            JUT_ASSERT(1603, m_handL_jnt_num >= 0);
            field_0x7D5 = a_tex_pattern_num_tbl[field_0x7DB];
            if (initTexPatternAnm(false)) {
                J3DModelData* a_itm_mdl_data = (J3DModelData*)dComIfG_getObjectIDRes("Kp", dRes_ID_KP_BDL_LT_01_e);
                JUT_ASSERT(1621, a_itm_mdl_data != NULL);
                field_0x6D0 = mDoExt_J3DModel__create(a_itm_mdl_data, 0x80000, 0x11000002);
                if (field_0x6D0 != NULL) {
                    a_itm_mdl_data = (J3DModelData*)dComIfG_getObjectIDRes("Kp", dRes_ID_KP_BDL_LT_02_e);
                    JUT_ASSERT(1631, a_itm_mdl_data != NULL);
                    field_0x6D4 = mDoExt_J3DModel__create(a_itm_mdl_data, 0x80000, 0x11000002);
                    if (field_0x6D4 != NULL) {
                        for (u16 i = 0; i < a_mdl_data->getJointNum(); i++) {
                            if (i == m_head_jnt_num || i == m_backbone_jnt_num) {
                                mpMorf->getModel()->getModelData()->getJointNodePointer(i)->setCallBack(nodeCallBack_Kp);
                            }
                        }
#if TARGET_PC
                        mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
                        mpMorf->getModel()->setUserArea((u32)this);
#endif
                        mAcchCir.SetWall(30.0f, 60.0f);
                        mObjAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 1, &mAcchCir, fopAcM_GetSpeed_p(this));
                        return TRUE;
                    }
                }
            }
        }
        mpMorf = NULL;
    }
    return FALSE;
}

/* 00002B14-00002B34       .text daNpc_Kp1_Create__FP10fopAc_ac_c */
static cPhs_State daNpc_Kp1_Create(fopAc_ac_c* i_this) {
    return ((daNpc_Kp1_c*)i_this)->_create();
}

/* 00002B34-00002B54       .text daNpc_Kp1_Delete__FP11daNpc_Kp1_c */
static BOOL daNpc_Kp1_Delete(daNpc_Kp1_c* i_this) {
    return ((daNpc_Kp1_c*)i_this)->_delete();
}

/* 00002B54-00002B74       .text daNpc_Kp1_Execute__FP11daNpc_Kp1_c */
static BOOL daNpc_Kp1_Execute(daNpc_Kp1_c* i_this) {
    return ((daNpc_Kp1_c*)i_this)->_execute();
}

/* 00002B74-00002B94       .text daNpc_Kp1_Draw__FP11daNpc_Kp1_c */
static BOOL daNpc_Kp1_Draw(daNpc_Kp1_c* i_this) {
    return ((daNpc_Kp1_c*)i_this)->_draw();
}

/* 00002B94-00002B9C       .text daNpc_Kp1_IsDelete__FP11daNpc_Kp1_c */
static BOOL daNpc_Kp1_IsDelete(daNpc_Kp1_c*) {
    return TRUE;
}

static actor_method_class l_daNpc_Kp1_Method = {
    (process_method_func)daNpc_Kp1_Create,
    (process_method_func)daNpc_Kp1_Delete,
    (process_method_func)daNpc_Kp1_Execute,
    (process_method_func)daNpc_Kp1_IsDelete,
    (process_method_func)daNpc_Kp1_Draw,
};

actor_process_profile_definition g_profile_NPC_KP1 = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_NPC_KP1_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daNpc_Kp1_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_NPC_KP1_e,
    /* Actor SubMtd */ &l_daNpc_Kp1_Method,
    /* Status       */ 0x08 | fopAcStts_SHOWMAP_e | fopAcStts_NOCULLEXEC_e | fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
