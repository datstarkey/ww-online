/**
 * d_a_obj_ftree.cpp
 * Object - Forest Trees
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_obj_ftree.h"
#include "d/d_cc_d.h"
#include "d/d_a_obj.h"
#include "d/d_com_inf_game.h"
#include "d/d_kankyo.h"
#include "d/actor/d_a_player.h"
#include "f_op/f_op_actor_mng.h"
#include "f_op/f_op_msg_mng.h"
#include "m_Do/m_Do_mtx.h"
#include "JSystem/JMath/JMATrigonometric.h"
#include "JAZelAudio/JAZelAudio_SE.h"
#include "res/Object/Vmr.h"

namespace daObjFtree {
namespace {
struct Attr_c {
    /* 0x00 */ f32 m00;
    /* 0x04 */ f32 m04;
    /* 0x08 */ f32 m08;
    /* 0x0C */ f32 m0C;
    /* 0x10 */ f32 m10;
    /* 0x14 */ f32 m14;
    /* 0x18 */ f32 m18;
    /* 0x1C */ f32 m1C;
    /* 0x20 */ f32 m20;
    /* 0x24 */ s16 m24;
    /* 0x28 */ f32 m28;
    /* 0x2C */ f32 m2C;
    /* 0x30 */ f32 m30;
    /* 0x34 */ f32 m34;
    /* 0x38 */ f32 m38;
    /* 0x3C */ s16 m3C;
    /* 0x40 */ f32 m40;
    /* 0x44 */ f32 m44;
    /* 0x48 */ f32 m48;
    /* 0x4C */ s16 m4C;
    /* 0x50 */ f32 m50;
    /* 0x54 */ f32 m54;
    /* 0x58 */ s16 m58;
    /* 0x5A */ s16 m5A;
    /* 0x5C */ s16 m5C;
};

const Attr_c L_attr = {
    61.0f,
    96.0f,
    25.0f,
    52.0f,
    132.0f,
    950.0f,
    72.0f,
    37.0f,
    -37.0f,
    20,
    0.2f,
    0.2f,
    0.02f,
    50.0f,
    20.0f,
    140,
    130.0f,
    1.75f,
    30.0f,
    0x2328,
    860.0f,
    -2.1f,
    0x2F,
    0,
    0x1E,
};
} // namespace

static const dCcD_SrcCyl M_cyl_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsEnemy_e | cCcD_TgSPrm_IsPlayer_e | cCcD_TgSPrm_IsOther_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ 0,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_Shield_e | dCcG_TgSPrm_NoConHit_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCylS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 100.0f,
        /* Height */ 200.0f,
    }},
};

static const dCcD_SrcCyl M_cyl_srcW = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ AT_TYPE_WATER,
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsEnemy_e | cCcD_TgSPrm_IsPlayer_e | cCcD_TgSPrm_IsOther_e,
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
        /* SrcGObjTg SPrm    */ 0,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCylS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 100.0f,
        /* Height */ 200.0f,
    }},
};

u32 message_table[13] = {
    0, 0x149F, 0x14A0, 0x14A1, 0, 0, 0, 0, 0, 0, 0x149F, 0x14A0, 0x14A1,
};

}; // namespace daObjFtree

/* 00000078-000000DC       .text search_heart_part__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::search_heart_part() {
    if (mSpawnedHeartPieceProcessId != fpcM_ERROR_PROCESS_ID_e) {
        fopAc_ac_c* heart_piece;
        if (!fopAcM_SearchByID(mSpawnedHeartPieceProcessId, &heart_piece)) {
            dComIfGs_onEventBit(dSv_event_flag_c::UNK_2E20);
            mSpawnedHeartPieceProcessId = fpcM_ERROR_PROCESS_ID_e;
        }
    }
}

/* 000000DC-000001F0       .text launch_heart_part__Q210daObjFtree5Act_cFv */
BOOL daObjFtree::Act_c::launch_heart_part() {
    csXyz angle = shape_angle;
    cXyz scale(1.0f, 1.0f, 1.0f);
    cXyz pos = current.pos;
    angle.y += 0x2328;
    pos.y += 860.0f;

    void* item = fopAcM_fastCreateItem(&pos, dItemNo_HEART_PIECE_e, current.roomNo, &angle, &scale,
                                        1.75f, 30.0f, -2.0999999046325684f, -1, NULL);
    if (item != NULL) {
        mSpawnedHeartPieceProcessId = fpcM_GetID(item);
        dComIfGs_setEventReg(0x9b07, param_get_tree_idx());

        ((fopAc_ac_c*)item)->actor_status = fopAcStts_UNK40_e | fopAcStts_UNK4000_e;
        field_0x648 = 1;

        return TRUE;
    }

    return FALSE;
}

/* 000001F0-00000394       .text place_heart_part__Q210daObjFtree5Act_cFv */
s16 daObjFtree::Act_c::place_heart_part() {
    if (m356 == -1 && dComIfGs_isEventBit(0x102) == TRUE && !dComIfGs_isEventBit(0x2e20) &&
        param_get_tree_idx() == dComIfGs_getEventReg(0x9b07))
    {
        cXyz local_pos(0.0f, 1.0f, 310.0f);
        s16 y_angle = shape_angle.y + 0x2328;

        mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
        mDoMtx_stack_c::YrotM(y_angle);

        Mtx local_mtx;
        MTXCopy(mDoMtx_stack_c::get(), local_mtx);

        cXyz world_pos;
        PSMTXMultVec(local_mtx, &local_pos, &world_pos);

        cXyz scale(1.0f, 1.0f, 1.0f);
        csXyz angle(0, 0, 0);

        void* item = fopAcM_fastCreateItem(&world_pos, dItemNo_HEART_PIECE_e, current.roomNo, &angle, &scale,
                                            0.0f, 0.0f, -2.0999999046325684f, -1, NULL);
        if (item != NULL) {
            mSpawnedHeartPieceProcessId = fpcM_GetID(item);
            dComIfGs_setEventReg(0x9b07, param_get_tree_idx());
            return 1;
        }
    }

    return 0;
}

/* 00000394-00000454       .text Ftree_get_water_pos__10daObjFtreeFP4cXyzP4cXyz */
void daObjFtree::Ftree_get_water_pos(cXyz* o_pos, cXyz* i_pos) {
    cXyz local_pos(0.0f, 0.0f, 50.0f);

    fopAc_ac_c* player = dComIfGp_getPlayer(0);

    f32 dx = i_pos->x - player->current.pos.x;
    f32 dz = i_pos->z - player->current.pos.z;
    s16 angle = cM_atan2s(dx, dz);

    mDoMtx_stack_c::transS(player->current.pos.x, player->current.pos.y, player->current.pos.z);
    mDoMtx_stack_c::YrotM(angle);

    Mtx local_mtx;
    MTXCopy(mDoMtx_stack_c::get(), local_mtx);

    PSMTXMultVec(local_mtx, &local_pos, o_pos);
}

/* 00000454-000004B8       .text estimate_water__Q210daObjFtree5Act_cFv */
BOOL daObjFtree::Act_c::estimate_water() {
    cXyz water_pos;
    Ftree_get_water_pos(&water_pos, &current.pos);
    return PSVECSquareDistance(&water_pos, &current.pos) <= SQUARE(20.0f + m4C8);
}

/* 000004B8-000004DC       .text Ftree_checkXyEventCallBack__10daObjFtreeFPvi */
s16 daObjFtree::Ftree_checkXyEventCallBack(void*, int i_index) {
    return dComIfGp_getSelectItem(i_index) == dItemNo_FOREST_WATER_e ? TRUE : FALSE;
}

/* 000004DC-000004FC       .text Ftree_XyEventCallBack__10daObjFtreeFPvi */
s16 daObjFtree::Ftree_XyEventCallBack(void* i_this, int param_2) {
    return ((Act_c*)i_this)->XyEventCallBack(param_2);
}

/* 000004FC-000005E4       .text XyEventCallBack__Q210daObjFtree5Act_cFi */
s16 daObjFtree::Act_c::XyEventCallBack(int i_index) {
    if (dComIfGp_getSelectItem(i_index) == dItemNo_FOREST_WATER_e) {
        if (!is_brought() && estimate_water()) {
            if (!iam_last()) {
                if (XyEvent_set(mEventCam1Idx)) {
                    m652 = 0x8c;
                    return mEventCam1Idx;
                }
            } else {
                if (XyEvent_set(mEventCam2Idx)) {
                    dComIfGs_onEventBit(dSv_event_flag_c::UNK_0102);
                    m652 = 0x8c;
                    return mEventCam2Idx;
                }
            }
        }
    }

    if (XyEvent_set(mEventCam0Idx)) {
        return mEventCam0Idx;
    }

    return -1;
}

/* 000005E4-000005F8       .text XyEvent_init__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::XyEvent_init() {
    m356 = -1;
    m358 = 0;
}

/* 000005F8-0000062C       .text XyEvent_set__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::XyEvent_set(short i_param_1) {
    if (m358 == 0 && i_param_1 != -1) {
        m356 = i_param_1;
        m358 = 1;
        return TRUE;
    }
    return FALSE;
}

/* 0000062C-00000764       .text XyEvent_exe__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::XyEvent_exe() {
    switch (m358) {
    case 1:
        if (eventInfo.checkCommandTalk()) {
            m358 = 2;
            daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);
            cXyz local_pos(0.0f, 1.0f, 108.0f);
            s16 angle = shape_angle.y + 0x2328;
            mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
            mDoMtx_stack_c::YrotM(angle);
            Mtx mtx;
            MTXCopy(mDoMtx_stack_c::get(), mtx);
            cXyz pos;
            PSMTXMultVec(mtx, &local_pos, &pos);
            player->setPlayerPosAndAngle(&pos, (s16)(angle + 0x8000));
        }
        break;
    case 2:
        if (dComIfGp_evmng_endCheck(m356)) {
            dComIfGp_event_onEventFlag(8);
            XyEvent_init();
        }
        break;
    }
}

namespace daObjFtree {
    static const u8 ret_tree_no[] = {0x0F, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x0F, 0x06, 0x07};
}

/* 00000764-000007A8       .text param_get_tree_idx__Q210daObjFtree5Act_cCFv */
inline s32 daObjFtree::Act_c::param_get_tree_idx() const {
    static const u8 ret_num = ARRAY_SIZE(ret_tree_no);
    s32 idx = daObj::PrmAbstract(this, 4, 0);
    if (idx < ret_num) {
        return ret_tree_no[idx];
    }
    return 0x0F;
}

/* 000007A8-000008D8       .text SetJointAnimation__Q210daObjFtree5Act_cFiffi */
BOOL daObjFtree::Act_c::SetJointAnimation(int i_index, float i_speed, float i_morf, int i_frame) {
    J3DAnmTransform* pAnimRes = (J3DAnmTransform*)dComIfG_getObjectRes(M_arcname, i_index);
    s32 frame = i_frame > 0 ? 0 : i_frame;

    if (pAnimRes != NULL) {
        if (frame == 0) {
            mpMorf->setAnm(pAnimRes, 0, i_morf, i_speed, 0.0f, -1.0f, NULL);
        } else {
            mpMorf->setAnm(pAnimRes, 0, i_morf, i_speed, 0.0f, (f32)frame + (f32)pAnimRes->getFrameMax(), NULL);
        }
        return TRUE;
    }

    return FALSE;
}

/* 000008D8-00000910       .text PlayStopJointAnimation__Q210daObjFtree5Act_cFv */
BOOL daObjFtree::Act_c::PlayStopJointAnimation() {
    return mpMorf->play(NULL, 0, 0) ? TRUE : FALSE;
}

/* 00000910-0000093C       .text PlayStopColorAnimation__Q210daObjFtree5Act_cFv */
BOOL daObjFtree::Act_c::PlayStopColorAnimation() {
    return mBrkAnm.play() ? TRUE : FALSE;
}

/* 0000093C-00000A1C       .text set_first_stat__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::set_first_stat() {
    if (dComIfGs_isSymbol(dSymbol_FARORE_e)) {
        if (dComIfGs_isEventBit(dSv_event_flag_c::UNK_0102)) {
            process_init(3, 0);
            field_0x63c = 1.0f;
        } else if (!is_brought()) {
            process_init(1, 0);
            field_0x63c = 0.2f;
        } else {
            process_init(2, 0);
            field_0x63c = 0.2f;
        }
    } else {
        process_init(0, 0);
    }
}

/* 00000A1C-00000C1C       .text set_collision__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::set_collision() {
    if (field_0x2a6 == 1 || field_0x2a7 == 1) {
        if (m508.ChkTgHit()) {
            if (field_0x2a6 == 1) {
                daObj::HitSeStart(&current.pos, current.roomNo, &m508, 8);
            } else {
                daObj::HitSeStart(&current.pos, current.roomNo, &m508, 7);
                daObj::HitEff_kikuzu(this, &m508);
            }
            dKy_Sound_set(current.pos, 4, fopAcM_GetID(this), 100);
            m508.ClrTgHit();
        } else {
            if (field_0x2a6 == 1) {
                cXyz pos = current.pos;
                pos.y -= 50.0f;
                m398.SetR(61.0f);
                m398.SetH(96.0f);
                m398.SetC(pos);
                dComIfG_Ccsp()->Set(&m398);
                m4C8 = 61.0f;
                m508.SetR(61.0f);
                m508.SetH(96.0f);
                m508.SetC(current.pos);
                dComIfG_Ccsp()->Set(&m508);
            } else if (field_0x2a7 == 1) {
                m4C8 = 132.0f * field_0x63c;
                m508.SetR(m4C8);
                m508.SetH(950.0f * field_0x63c);
                m508.SetC(current.pos);
                dComIfG_Ccsp()->Set(&m508);
            }
        }
    }
}

/* 00000C1C-00000D50       .text talk_ct__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::talk_ct() {
    field_0x6a4 = fpcM_ERROR_PROCESS_ID_e;
    field_0x6a8 = 0;
    cXyz attn_pos = current.pos;
    cXyz eye_pos = current.pos;
    if (mMode >= 0 && mMode < 13) {
        switch (message_table[mMode]) {
        case 0x149F:
            eye_pos.y += 80.0f;
            attn_pos.y += 105.0f;
            attention_info.flags = fopAc_Attn_TALKFLAG_LOOK_e | fopAc_Attn_ACTION_SPEAK_e | fopAc_Attn_LOCKON_TALK_e;
            break;
        case 0x14A0:
            eye_pos.y += 90.0f;
            attn_pos.y += 195.0f;
            attention_info.flags = fopAc_Attn_TALKFLAG_LOOK_e | fopAc_Attn_ACTION_SPEAK_e | fopAc_Attn_LOCKON_TALK_e;
            break;
        case 0x14A1:
            eye_pos.y += 250.0f;
            attn_pos.y += 200.0f;
            attention_info.flags = fopAc_Attn_TALKFLAG_LOOK_e | fopAc_Attn_ACTION_SPEAK_e;
            break;
        }
    }
    attention_info.position = attn_pos;
    eyePos = eye_pos;
}

/* 00000D50-00000F14       .text talk_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::talk_main() {
    if (m356 == -1 && eventInfo.checkCommandTalk()) {
        if (field_0x6a4 == fpcM_ERROR_PROCESS_ID_e) {
            field_0x6a4 = fopMsgM_messageSet(message_table[mMode], this);
            field_0x6a8 = 0;
        } else if (field_0x6a8 == 0) {
#if TARGET_PC
            // field_0x6a8 holds a msg_class pointer; needs the full 64-bit width on the host.
            field_0x6a8 = (uintptr_t)fopMsgM_SearchByID(field_0x6a4);
#else
            field_0x6a8 = (u32)fopMsgM_SearchByID(field_0x6a4);
#endif
        } else {
            msg_class* msg = (msg_class*)field_0x6a8;
            switch (msg->mStatus) {
            case fopMsgStts_MSG_DISPLAYED_e:
                msg->mStatus = fopMsgStts_MSG_ENDS_e;
                break;
            case fopMsgStts_BOX_CLOSED_e:
                msg->mStatus = fopMsgStts_MSG_DESTROYED_e;
                dComIfGp_event_onEventFlag(8);
                talk_ct();
                break;
            }
        }
    } else {
        s32 mode = mMode;
        if (mode >= 0 && mode < 13 && message_table[mode] != 0) {
            fopAc_ac_c* player = dComIfGp_getPlayer(0);
            if (PSVECSquareDistance(&current.pos, &player->current.pos) <= SQUARE(130.0f + m4C8)) {
                talk_ct();
                player = dComIfGp_getPlayer(0);
                if (player != NULL) {
                    cXyz diff = current.pos - player->current.pos;
                    s16 angle = cM_atan2s(diff.x, diff.z);
                    s16 diffAngle = angle - player->shape_angle.y;
                    if ((diffAngle <= 0 ? -diffAngle : diffAngle) <= 0x4000) {
                        eventInfo.onCondition(dEvtCnd_CANTALK_e);
                    }
                }
                eventInfo.onCondition(dEvtCnd_CANTALKITEM_e);
            }
        }
    }
}

/* 00000F14-00000FC4       .text get_tev_material0_color__Q210daObjFtree5Act_cFP12J3DModelDataUlPsPsPs */
void daObjFtree::Act_c::get_tev_material0_color(J3DModelData* i_modelData, unsigned long i_no, short* o_r, short* o_g, short* o_b) {
    J3DMaterial* mat = i_modelData->getJointNodePointer(0)->getMesh();
    if (mat != NULL) {
        *o_r = mat->getTevBlock()->getTevColor(i_no)->mColor.r;
        *o_g = mat->getTevBlock()->getTevColor(i_no)->mColor.g;
        *o_b = mat->getTevBlock()->getTevColor(i_no)->mColor.b;
    }
}

/* 00000FC4-00001070       .text set_tev_color__Q210daObjFtree5Act_cFP12J3DModelDataUlsss */
void daObjFtree::Act_c::set_tev_color(J3DModelData* i_modelData, unsigned long i_no, short i_r, short i_g, short i_b) {
    for (J3DMaterial* mat = i_modelData->getJointNodePointer(0)->getMesh(); mat != NULL; mat = mat->getNext()) {
        mat->getTevBlock()->getTevColor(i_no)->mColor.r = i_r;
        mat->getTevBlock()->getTevColor(i_no)->mColor.g = i_g;
        mat->getTevBlock()->getTevColor(i_no)->mColor.b = i_b;
    }
}

/* 00001070-000010F0       .text is_broughtID__10daObjFtreeFi */
BOOL daObjFtree::is_broughtID(int i_id) {
    return ((dComIfGs_getEventReg(dSv_event_flag_c::UNK_9EFF) >> (i_id & 7)) & 1) || dComIfGs_isEventBit(dSv_event_flag_c::UNK_0102);
}

/* 000010F0-00001138       .text is_brought__Q210daObjFtree5Act_cFv */
BOOL daObjFtree::Act_c::is_brought() {
    return is_broughtID(param_get_tree_idx());
}

/* 00001138-000011FC       .text set_broughtID__Q210daObjFtree5Act_cFi */
void daObjFtree::Act_c::set_broughtID(int i_id) {
    u8 reg = dComIfGs_getEventReg(dSv_event_flag_c::UNK_9EFF);
    int bit = 1 << (i_id & 7);
    reg |= bit;
    dComIfGs_setEventReg(dSv_event_flag_c::UNK_9EFF, reg);
    reg = dComIfGs_getEventReg(dSv_event_flag_c::UNK_9AFF);
    reg |= bit;
    dComIfGs_setEventReg(dSv_event_flag_c::UNK_9AFF, reg);

    _ftree_seach_info_ info;
    get_ftree_info(&info);
    if (info.field_0x4 == info.field_0x0) {
        dComIfGs_onEventBit(dSv_event_flag_c::UNK_0102);
    }
}

/* 000011FC-00001260       .text set_brought__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::set_brought() {
    set_broughtID(param_get_tree_idx());
    field_0x638 = 1;
}

/* 00001260-000012D0       .text unset_broughtID__Q210daObjFtree5Act_cFi */
void daObjFtree::Act_c::unset_broughtID(int param_1) {
    u8 reg = dComIfGs_getEventReg(dSv_event_flag_c::UNK_9EFF);
    reg &= ~(1 << (param_1 & 7));
    dComIfGs_setEventReg(dSv_event_flag_c::UNK_9EFF, reg);
}

/* 000012D0-00001334       .text unset_brought__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::unset_brought() {
    unset_broughtID(param_get_tree_idx());
    field_0x638 = 0;
}

/* 00001334-000013A0       .text get_ftree_info__Q210daObjFtree5Act_cFPQ210daObjFtree18_ftree_seach_info_ */
void daObjFtree::Act_c::get_ftree_info(daObjFtree::_ftree_seach_info_* o_info) {
    o_info->field_0x0 = 8;
    o_info->field_0x4 = 0;
    for (u32 i = 0; i < 8; i++) {
        if (is_broughtID(i)) {
            o_info->field_0x4++;
        }
    }
}

/* 000013A0-00001400       .text iam_last__Q210daObjFtree5Act_cFv */
BOOL daObjFtree::Act_c::iam_last() {
    _ftree_seach_info_ info;
    get_ftree_info(&info);
    if (!is_brought() && info.field_0x4 >= info.field_0x0 - 1) {
        return TRUE;
    }
    return FALSE;
}

/* 00001400-00001414       .text action_none_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_none_init(short) {
    field_0x2a6 = 0;
    field_0x2a7 = 0;
    return TRUE;
}

/* 00001414-00001418       .text action_none_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_none_main() {
}

/* 00001418-000014E8       .text action_waitS_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_waitS_init(short) {
    field_0x2a6 = 1;
    field_0x2a7 = 0;
    unset_brought();

    J3DAnmTevRegKey* pRes = (J3DAnmTevRegKey*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BRK_VMRMZ_e);
    if (pRes != NULL && mBrkAnm.init(mpMorf->getModel()->getModelData(), pRes, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, 0)) {
        return SetJointAnimation(dRes_INDEX_VMR_BCK_VMRTY_e, 1.0f, 0.0f, 0);
    }
    return FALSE;
}

/* 000014E8-00001608       .text action_waitS_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_waitS_main() {
    if (m652 == 1) {
        BOOL ret;
        if (m356 == mEventCam2Idx) {
            ret = process_init(4, 0);
        } else {
            ret = process_init(8, 0);
        }
        if (ret) {
            m652 = 0;
            return;
        }
    }

    s32 hit = m508.ChkCoHit();
    if (field_0x67c == 0 && hit == 1 && process_init(10, 1)) {
        hit = 0;
    } else if ((s32)cM_rndF(100.0f) == 0 && mMode == 1) {
        process_init(10, (s16)(1.0f + 4.0f * cM_rndF(1.0f)));
    }
    field_0x67c = hit;
}

/* 00001608-0000173C       .text action_waitM_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_waitM_init(short) {
    set_brought();
    field_0x2a6 = 1;
    field_0x2a7 = 0;

    J3DAnmTevRegKey* pRes = (J3DAnmTevRegKey*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BRK_VMRMZ_e);
    if (pRes != NULL && mBrkAnm.init(mpMorf->getModel()->getModelData(), pRes, TRUE, J3DFrameCtrl::EMode_NONE, -1.0f, 0, -1, true, 0) &&
        SetJointAnimation(dRes_INDEX_VMR_BCK_VMRMZ_e, -1.0f, 0.0f, 0))
    {
        mpMorf->setFrame(mpMorf->getEndFrame() - 1.0f);
        return TRUE;
    }
    return FALSE;
}

/* 0000173C-00001878       .text action_waitM_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_waitM_main() {
    if (dComIfGs_getpItemRecord()->getTimer() == 0) {
        _ftree_seach_info_ info;
        get_ftree_info(&info);
        if (info.field_0x0 != info.field_0x4) {
            process_init(9, 0);
            return;
        }
    }

    if (mMode == 2) {
        s32 hit = m508.ChkCoHit();
        if (dKy_rain_check()) {
            process_init(0xb, -1);
        } else if (field_0x67c == 0 && hit == 1 && process_init(0xb, 0x78)) {
            hit = 0;
        } else if ((s32)cM_rndF(90.0f) == 0) {
            process_init(0xb, (s16)(30.0f + 90.0f * cM_rndF(1.0f)));
        }
        field_0x67c = hit;
    }
}

/* 00001878-000018AC       .text action_waitL_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_waitL_init(short) {
    field_0x2a7 = 1;
    field_0x2a6 = 0;
    set_brought();
    return TRUE;
}

/* 000018AC-000019BC       .text action_waitL_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_waitL_main() {
    if (mMode == 3) {
        if (dKy_rain_check()) {
            process_init(0xc, -1);
        } else if ((s32)cM_rndF(30.0f) == 0) {
            process_init(0xc, (s16)(100.0f + 80.0f * cM_rndF(1.0f)));
        } else if (m356 != -1) {
            process_init(0xc, -1);
        }
    } else if (mMode == 0xc) {
        if (!dKy_rain_check() && field_0x69a == -1 && m356 == -1) {
            process_init(3, 0);
        }
    }
}

/* 000019BC-00001A4C       .text action_pikupikuS_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_pikupikuS_init(short i_num) {
    i_num = i_num > 0 ? i_num : 1;
    field_0x2a6 = 1;
    field_0x2a7 = 0;
    field_0x688 = i_num;
    field_0x68c = 0;
    return SetJointAnimation(dRes_INDEX_VMR_BCK_VMRTY_e, 0.7f + (1.2f - 0.7f) * cM_rndF(1.0f), 10.0f, 0);
}

/* 00001A4C-00001AF4       .text action_pikupikuS_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_pikupikuS_main() {
    if (PlayStopJointAnimation()) {
        if ((s32)field_0x68c < (s32)field_0x688) {
            SetJointAnimation(dRes_INDEX_VMR_BCK_VMRTY_e, 0.7f + (1.0f - 0.7f) * cM_rndF(1.0f), 10.0f, 0);
        } else {
            process_init(1, 0);
        }
        field_0x68c++;
    }
    action_waitS_main();
}

/* 00001AF4-00001B4C       .text action_pikupikuM_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_pikupikuM_init(short i_num) {
    set_brought();
    field_0x690 = i_num;
    field_0x692 = 0;
    field_0x694 = 0;
    field_0x2a6 = 1;
    field_0x2a7 = 0;
    return TRUE;
}

/* 00001B4C-00001C44       .text action_pikupikuM_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_pikupikuM_main() {
    if (field_0x692 < field_0x690 || field_0x690 == -1) {
        m696 = (s16)(100.0f * JMASSin(field_0x694));
        m698 = (s16)(300.0f * JMASSin(field_0x694));
        field_0x694 += 0xfa0;
    } else if (field_0x692 >= field_0x690) {
        process_init(2, 0);
    }

    field_0x692++;
    action_waitM_main();
}

/* 00001C44-00001C9C       .text action_pikupikuL_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_pikupikuL_init(short param_1) {
    set_brought();
    field_0x69a = param_1;
    field_0x69c = 0;
    field_0x69e = 0;
    field_0x2a6 = 0;
    field_0x2a7 = 1;
    return TRUE;
}

/* 00001C9C-00001D94       .text action_pikupikuL_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_pikupikuL_main() {
    if (field_0x69c < field_0x69a || field_0x69a == -1) {
        m6A0 = (s16)(30.0f * JMASSin(field_0x69e));
        m6A2 = (s16)(36.0f * JMASSin(field_0x69e));
        field_0x69e += 3000;
    } else if (field_0x69c >= field_0x69a) {
        process_init(3, 0);
    }

    field_0x69c++;
    action_waitL_main();
}

/* 00001D94-00001EAC       .text action_changeSL_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_changeSL_init(short) {
    field_0x2a6 = 1;
    field_0x2a7 = 0;

    J3DAnmTevRegKey* pRes = static_cast<J3DAnmTevRegKey*>(dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BRK_VMRMZ_e));
    if (pRes != NULL && mBrkAnm.init(mpMorf->getModel()->getModelData(), pRes, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, true, 0) &&
        SetJointAnimation(dRes_INDEX_VMR_BCK_VMRMZ_e, 1.0f, 10.0f, 0))
    {
        set_brought();
        mDoAud_seStart(JA_SE_OBJ_DK_TREE_RECOVER, &current.pos);
        return TRUE;
    }
    return FALSE;
}

/* 00001EAC-00001F0C       .text action_changeSL_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_changeSL_main() {
    BOOL joint_end = PlayStopJointAnimation();
    BOOL color_end = PlayStopColorAnimation();
    if (joint_end == TRUE && color_end == TRUE) {
        process_init(5, 0);
    }
}

/* 00001F0C-00001FB4       .text action_changeSL2_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_changeSL2_init(short) {
    field_0x2a6 = 1;
    field_0x2a7 = 0;
    field_0x2a4 = 1;
    field_0x640 = 0.0f;
    field_0x684 = 30;
    field_0x63c = 0.2f;
    get_tev_material0_color(mpMorf->getModel()->getModelData(), 2, &field_0x65c, &field_0x65e, &field_0x660);
    m66C.r = field_0x65c;
    m66C.g = field_0x65e;
    m66C.b = field_0x660;
    m66C.a = field_0x662;
    field_0x69e = 0;
    return TRUE;
}

/* 00001FB4-00002300       .text action_changeSL2_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_changeSL2_main() {
    if (field_0x2a6 != 0) {
        cLib_addCalc(&field_0x640, 1.0f, 0.2f, 1.0f, 0.007f);

        m66C.r = (s16)(field_0x65c + (255.0f - field_0x65c) * field_0x640);
        m66C.g = (s16)(field_0x65e + (255.0f - field_0x65e) * field_0x640);
        m66C.b = (s16)(field_0x660 + (255.0f - field_0x660) * field_0x640);

        if (1.0f == field_0x640) {
            if (field_0x684 > 0) {
                field_0x684--;
            }

            if (field_0x684 == 0) {
                field_0x2a6 = 0;
                field_0x2a7 = 1;
            }
        }
    }

    if (field_0x2a7 != 0) {
        f32 ratio = GetRate(field_0x63c, 0.2f, 1.0f);

        cLib_addCalc(&field_0x63c, 1.0f, 0.02f, 1.0f, 0.005f);

        m674.r = (s16)(255.0f + (m664.r - 255.0f) * ratio);
        m674.g = (s16)(255.0f + (m664.g - 255.0f) * ratio);
        m674.b = (s16)(255.0f + (m664.b - 255.0f) * ratio);

        if (1.0f == field_0x63c) {
            if (process_init(3, 0)) {
                m650 = 0x2f;
            }
        }

        m6A0 = (s16)(30.0f * JMASSin(field_0x69e));
        m6A2 = (s16)(36.0f * JMASSin(field_0x69e));
        field_0x69e += 3000;
    }
}

/* 00002300-00002344       .text action_changeLS_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_changeLS_init(short) {
    field_0x2a6 = 0;
    field_0x2a7 = 1;
    field_0x640 = 1.0f;
    field_0x63c = 1.0f;
    unset_brought();
    return TRUE;
}

/* 00002344-000025DC       .text action_changeLS_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_changeLS_main() {
    if (field_0x2a7 != 0) {
        f32 ratio = GetRate(field_0x63c, 0.2f, 1.0f);

        cLib_addCalc(&field_0x63c, 0.2f, 0.02f, 1.0f, 0.005f);

        m674.r = (s16)(255.0f + (m664.r - 255.0f) * ratio);
        m674.g = (s16)(255.0f + (m664.g - 255.0f) * ratio);
        m674.b = (s16)(255.0f + (m664.b - 255.0f) * ratio);

        if (field_0x63c == 0.2f) {
            field_0x2a6 = 1;
            field_0x2a7 = 0;
        }
    }

    if (field_0x2a6 != 0) {
        cLib_addCalc(&field_0x640, 0.0f, 0.2f, 1.0f, 0.007f);

        m66C.r = (s16)(field_0x65c + (255.0f - field_0x65c) * field_0x640);
        m66C.g = (s16)(field_0x65e + (255.0f - field_0x65e) * field_0x640);
        m66C.b = (s16)(field_0x660 + (255.0f - field_0x660) * field_0x640);

        if (field_0x640 == 0.0f) {
            process_init(7, 0);
        }
    }
}

/* 000025DC-000026AC       .text action_changeLS2_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_changeLS2_init(short) {
    field_0x2a6 = 1;
    field_0x2a7 = 0;

    J3DAnmTevRegKey* pRes = (J3DAnmTevRegKey*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BRK_VMRMZ_e);
    if (pRes != NULL && mBrkAnm.init(mpMorf->getModel()->getModelData(), pRes, TRUE, J3DFrameCtrl::EMode_NONE, -1.0f, 0, -1, true, 0)) {
        return SetJointAnimation(dRes_INDEX_VMR_BCK_VMRMZ_e, -0.7f, 10.0f, -20);
    }
    return FALSE;
}

/* 000026AC-0000270C       .text action_changeLS2_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_changeLS2_main() {
    BOOL joint_end = PlayStopJointAnimation();
    BOOL color_end = PlayStopColorAnimation();
    if (joint_end == TRUE && color_end == TRUE) {
        process_init(1, 0);
    }
}

/* 0000270C-00002794       .text action_changeSM_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_changeSM_init(short) {
    set_brought();
    if (action_changeSL_init(0) == TRUE) {
        mDoAud_seStart(JA_SE_OBJ_DK_TREE_RECOVER, &current.pos);
        return TRUE;
    } else {
        return FALSE;
    }
}

/* 00002794-000027F4       .text action_changeSM_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_changeSM_main() {
    BOOL joint_end = PlayStopJointAnimation();
    BOOL color_end = PlayStopColorAnimation();
    if (joint_end == TRUE && color_end == TRUE) {
        process_init(2, 0);
    }
}

/* 000027F4-00002840       .text action_changeMS_init__Q210daObjFtree5Act_cFs */
BOOL daObjFtree::Act_c::action_changeMS_init(short) {
    if (action_changeLS2_init(0)) {
        unset_brought();
        return TRUE;
    } else {
        return FALSE;
    }
}

/* 00002840-00002860       .text action_changeMS_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::action_changeMS_main() {
    action_changeLS2_main();
}

/* 00002860-00002A4C       .text process_init__Q210daObjFtree5Act_cFis */
BOOL daObjFtree::Act_c::process_init(int type, short arg2) {
    static BOOL (Act_c::*init_table[13])(short) = {
        &Act_c::action_none_init,
        &Act_c::action_waitS_init,
        &Act_c::action_waitM_init,
        &Act_c::action_waitL_init,
        &Act_c::action_changeSL_init,
        &Act_c::action_changeSL2_init,
        &Act_c::action_changeLS_init,
        &Act_c::action_changeLS2_init,
        &Act_c::action_changeSM_init,
        &Act_c::action_changeMS_init,
        &Act_c::action_pikupikuS_init,
        &Act_c::action_pikupikuM_init,
        &Act_c::action_pikupikuL_init,
    };
    if (type >= 0 && type < 13) {
        if ((this->*init_table[type])(arg2)) {
            m696 = 0;
            m698 = 0;
            m6A0 = 0;
            m6A2 = 0;
            mMode = type;
            return TRUE;
        }
    }
    return FALSE;
}

/* 00002A4C-00002BF0       .text process_main__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::process_main() {
    static void (Act_c::*main_table[13])() = {
        &Act_c::action_none_main,
        &Act_c::action_waitS_main,
        &Act_c::action_waitM_main,
        &Act_c::action_waitL_main,
        &Act_c::action_changeSL_main,
        &Act_c::action_changeSL2_main,
        &Act_c::action_changeLS_main,
        &Act_c::action_changeLS2_main,
        &Act_c::action_changeSM_main,
        &Act_c::action_changeMS_main,
        &Act_c::action_pikupikuS_main,
        &Act_c::action_pikupikuM_main,
        &Act_c::action_pikupikuL_main,
    };
    if (mMode >= 0 && mMode < 13) {
        (this->*main_table[mMode])();
    }
}

/* 00002BF0-00002C14       .text solidHeapCB__Q210daObjFtree5Act_cFP10fopAc_ac_c */
BOOL daObjFtree::Act_c::solidHeapCB(fopAc_ac_c* i_this) {
    return ((Act_c*)i_this)->create_heap();
}

/* 00002C14-00002D94       .text NodeCallBack_Effect__Q210daObjFtree5Act_cFP7J3DNodei */
BOOL daObjFtree::Act_c::NodeCallBack_Effect(J3DNode* i_node, int i_timing) {
    if (field_0x2a4 == 1 && i_timing == J3DNodeCBCalcTiming_In) {
        cXyz offset(0.0f, 0.0f, 0.0f);
        MTXCopy(j3dSys.getModel()->getAnmMtx(((J3DJoint*)i_node)->getJntNo()), mDoMtx_stack_c::get());
        Mtx mtx;
        MTXCopy(mDoMtx_stack_c::get(), mtx);
        cXyz pos;
        PSMTXMultVec(mtx, &offset, &pos);
        mDoAud_seStart(JA_SE_OBJ_DK_TREE_GROW_UP, &current.pos);
        JPABaseEmitter* emitter0 = dComIfGp_particle_set(dPa_name::ID_AK_SN_HANASAKAFLASH00, &pos);
        JPABaseEmitter* emitter1 = dComIfGp_particle_set(dPa_name::ID_AK_SN_HANASAKAFLASH01, &pos);
        if (emitter0 != NULL && emitter1 != NULL) {
            field_0x2a4 = 0;
        }
    }
    return TRUE;
}

/* 00002D94-00002DDC       .text Ftree_NodeCallBack_Effect__10daObjFtreeFP7J3DNodei */
BOOL daObjFtree::Ftree_NodeCallBack_Effect(J3DNode* i_node, int i_timing) {
    if (i_timing == J3DNodeCBCalcTiming_In) {
        Act_c* i_this = (Act_c*)j3dSys.getModel()->getUserArea();
        return i_this->NodeCallBack_Effect(i_node, i_timing);
    }
    return TRUE;
}

/* 00002DDC-00002E9C       .text Ftree_NodeCallBack_M__10daObjFtreeFP7J3DNodei */
BOOL daObjFtree::Ftree_NodeCallBack_M(J3DNode* i_node, int i_timing) {
    if (i_timing == J3DNodeCBCalcTiming_In) {
        int jntNo = ((J3DJoint*)i_node)->getJntNo();
        J3DModel* model = j3dSys.getModel();
        Act_c* i_this = (Act_c*)model->getUserArea();

        if (i_this != NULL) {
            csXyz angle(i_this->m294, i_this->m296, 0);
            PSMTXCopy(model->getAnmMtx(jntNo), mDoMtx_stack_c::now);
            mDoMtx_stack_c::ZXYrotM(angle);
            PSMTXCopy(mDoMtx_stack_c::now, model->getAnmMtx(jntNo));
            PSMTXCopy(mDoMtx_stack_c::now, J3DSys::mCurrentMtx);
        }
    }

    return TRUE;
}

/* 00002E9C-00002F5C       .text Ftree_NodeCallBack_L__10daObjFtreeFP7J3DNodei */
BOOL daObjFtree::Ftree_NodeCallBack_L(J3DNode* i_node, int i_timing) {
    if (i_timing == 0) {
        int jntNo = ((J3DJoint*)i_node)->getJntNo();
        J3DModel* model = j3dSys.getModel();
        Act_c* actor = (Act_c*)model->getUserArea();

        if (actor != NULL) {
            csXyz angle(0, actor->m298, actor->m29A);
            PSMTXCopy(model->getAnmMtx(jntNo), mDoMtx_stack_c::now);
            mDoMtx_stack_c::ZXYrotM(angle);
            PSMTXCopy(mDoMtx_stack_c::now, model->getAnmMtx(jntNo));
            PSMTXCopy(mDoMtx_stack_c::now, J3DSys::mCurrentMtx);
        }
    }

    return TRUE;
}

/* 00002F5C-00003354       .text create_heap__Q210daObjFtree5Act_cFv */
bool daObjFtree::Act_c::create_heap() {
    J3DAnmTransform* tmp_bck_data = (J3DAnmTransform*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BCK_VMRTY_e);
    JUT_ASSERT(0x836, tmp_bck_data != 0);

    J3DModelData* mdl_dataS = (J3DModelData*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BDL_VMRTY_e);
    JUT_ASSERT(0x83C, mdl_dataS != 0);
    if (mdl_dataS == NULL) {
        return false;
    }

    mpMorf = new mDoExt_McaMorf(mdl_dataS, NULL, NULL, tmp_bck_data, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, 1, NULL, 0, 0x11020203);
    if (mpMorf == NULL) {
        return false;
    }

#if TARGET_PC
    mpMorf->getModel()->setUserArea((uintptr_t)this);
#else
    mpMorf->getModel()->setUserArea((u32)this);
#endif
    mdl_dataS->getJointNodePointer(VMRTY_JNT_SPIN_e)->setCallBack(Ftree_NodeCallBack_Effect);
    mdl_dataS->getJointNodePointer(VMRTY_JNT_ARML_e)->setCallBack(Ftree_NodeCallBack_M);
    mdl_dataS->getJointNodePointer(VMRTY_JNT_ARMR_e)->setCallBack(Ftree_NodeCallBack_M);
    mdl_dataS->getJointNodePointer(VMRTY_JNT_NECK_e)->setCallBack(Ftree_NodeCallBack_M);

    J3DModelData* mdl_dataL = (J3DModelData*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BDL_VMRTO_e);
    JUT_ASSERT(0x85B, mdl_dataL != 0);
    if (mdl_dataL == NULL) {
        return false;
    }

    mpModel = mDoExt_J3DModel__create(mdl_dataL, 0, 0x11020203);
    if (mpModel == NULL) {
        return false;
    }

#if TARGET_PC
    mpModel->setUserArea((uintptr_t)this);
#else
    mpModel->setUserArea((u32)this);
#endif
    mdl_dataL->getJointNodePointer(VMRTO_JNT_HEAD_e)->setCallBack(Ftree_NodeCallBack_L);

    J3DAnmTevRegKey* brk_data = (J3DAnmTevRegKey*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_VMR_BRK_VMRMZ_e);
    JUT_ASSERT(0x86B, brk_data != 0);
    if (brk_data == NULL) {
        return false;
    }

    BOOL brk_ret = mBrkAnm.init(mdl_dataS, brk_data, TRUE, J3DFrameCtrl::EMode_NONE, 1.0f, 0, -1, false, 0);

    get_tev_material0_color(mpMorf->getModel()->getModelData(), 2, &m654.r, &m654.g, &m654.b);
    m66C = m654;
    get_tev_material0_color(mpModel->getModelData(), 2, &m664.r, &m664.g, &m664.b);
    m674 = m664;

    return mdl_dataS != NULL && mpMorf != NULL && mpMorf->getModel() != NULL && mdl_dataL != NULL && brk_data != NULL && brk_ret;
}

/* 00003354-000035F4       .text _create__Q210daObjFtree5Act_cFv */
cPhs_State daObjFtree::Act_c::_create() {
    fopAcM_ct(this, daObjFtree::Act_c);

    cPhs_State ret = dComIfG_resLoad(&mPhs, M_arcname);
    if (ret == cPhs_COMPLEATE_e) {
        if (fopAcM_entrySolidHeap(this, solidHeapCB, 0)) {
            field_0x644 = param_get_tree_idx();
            field_0x648 = 0;
            mSpawnedHeartPieceProcessId = fpcM_ERROR_PROCESS_ID_e;
            set_first_stat();
            talk_ct();

            cXyz pos(current.pos.x, current.pos.y + 100.0f, current.pos.z);
            mGndChk.SetPos(&pos);
            mGndChk.SetActorPid(fopAcM_GetID(this));
            m34C = dComIfG_Bgsp()->GroundCross(&mGndChk);

            set_mtx();
            fopAcM_SetMtx(this, mpMorf->getModel()->getBaseTRMtx());
            fopAcM_setCullSizeBox(this, -300.0f, 0.0f, -300.0f, 300.0f, 1000.0f, 300.0f);

            m35C.Init(0xFF, 0xFF, this);
            m398.Set(M_cyl_srcW);
            m398.SetStts(&m35C);
            m398.SetTgVec((cXyz&)cXyz::Zero);
            m398.OnTgNoHitMark();

            m4CC.Init(0xFF, 0xFF, this);
            m508.Set(M_cyl_src);
            m508.SetStts(&m4CC);
            m508.SetTgVec((cXyz&)cXyz::Zero);
            m508.OnTgNoHitMark();

            XyEvent_init();
            eventInfo.setXyCheckCB(Ftree_checkXyEventCallBack);
            eventInfo.setXyEventCB(Ftree_XyEventCallBack);
            mEventCam0Idx = dComIfGp_evmng_getEventIdx("morinoki_camera0", 0xFF);
            mEventCam1Idx = dComIfGp_evmng_getEventIdx("morinoki_camera1", 0xFF);
            mEventCam2Idx = dComIfGp_evmng_getEventIdx("morinoki_camera2", 0xFF);
        } else {
            ret = cPhs_ERROR_e;
        }
    }

    return ret;
}

daObjFtree::Act_c::Act_c() {}

/* 00003FB8-00004004       .text _delete__Q210daObjFtree5Act_cFv */
bool daObjFtree::Act_c::_delete() {
    if (field_0x638 != 0) {
        set_brought();
    }
    dComIfG_resDelete(&mPhs, M_arcname);
    return true;
}

/* 00004004-00004144       .text set_mtx__Q210daObjFtree5Act_cFv */
void daObjFtree::Act_c::set_mtx() {
    mpMorf->getModel()->setBaseScale(scale);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::ZXYrotM(shape_angle);
    mpMorf->getModel()->setBaseTRMtx(mDoMtx_stack_c::get());
    cMtx_copy(mDoMtx_stack_c::get(), m2A8);

    cXyz sp08(scale.x * field_0x63c, scale.y * field_0x63c, scale.z * field_0x63c);
    mpModel->setBaseScale(sp08);
    mDoMtx_stack_c::transS(current.pos.x, current.pos.y, current.pos.z);
    mDoMtx_stack_c::ZXYrotM(shape_angle);
    mpModel->setBaseTRMtx(mDoMtx_stack_c::get());
    cMtx_copy(mDoMtx_stack_c::get(), m2A8);

    mpModel->calc();
}

/* 00004144-000042C4       .text _execute__Q210daObjFtree5Act_cFv */
bool daObjFtree::Act_c::_execute() {
    if (mMode == 0) {
        return true;
    }

    search_heart_part();
    talk_main();
    set_mtx();
    process_main();
    set_collision();
    fopAcM_rollPlayerCrash(this, m4C8, 7);

    s16 angle_step1 = (mMode == 0xb) ? 1 : 0x1e;
    cLib_addCalcAngleS2(&m294, m696, angle_step1, 0x1000);
    cLib_addCalcAngleS2(&m296, m698, angle_step1, 0x1000);

    s16 angle_step2 = (mMode == 0xc || mMode == 0x5) ? 1 : 0xa;
    cLib_addCalcAngleS2(&m298, m6A0, angle_step2, 0x1000);
    cLib_addCalcAngleS2(&m29A, m6A2, angle_step2, 0x1000);

    XyEvent_exe();

    if (field_0x648 == 0) {
        field_0x648 = place_heart_part();
    }

    if (m650 == 1) {
        if (launch_heart_part()) {
            m650 = 0;
        }
    }

    if (m652 > 1) {
        m652--;
    }

    if (m650 > 1) {
        m650--;
    }

    return true;
}

/* 000042C4-0000455C       .text _draw__Q210daObjFtree5Act_cFv */
bool daObjFtree::Act_c::_draw() {
    if (mMode == 0) {
        return true;
    }

    if (field_0x2a6 == 1) {
        g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
        g_env_light.setLightTevColorType(mpMorf->getModel(), &tevStr);
        dComIfGd_setListBG();

        if (mMode == 5 || mMode == 6) {
            mBrkAnm.remove(mpMorf->getModel()->getModelData());
            set_tev_color(mpMorf->getModel()->getModelData(), 2, m66C.r, m66C.g, m66C.b);
        } else {
            mBrkAnm.entry(mpMorf->getModel()->getModelData());
        }

        mpMorf->updateDL();
        dComIfGd_setList();

        cXyz pos(current.pos.x, current.pos.y, current.pos.z);
        dComIfGd_setSimpleShadow2(&pos, m34C, 75.0f, mGndChk, shape_angle.y);
    }

    if (field_0x2a7 == 1) {
        g_env_light.settingTevStruct(TEV_TYPE_BG0, &current.pos, &tevStr);
        g_env_light.setLightTevColorType(mpModel, &tevStr);
        dComIfGd_setListBG();

        set_tev_color(mpModel->getModelData(), 2, m674.r, m674.g, m674.b);
        mDoExt_modelUpdateDL(mpModel);
        dComIfGd_setList();

        f32 t = GetRate(field_0x63c, 0.2f, 1.0f);

        cXyz pos(current.pos.x, current.pos.y, current.pos.z);
        dComIfGd_setSimpleShadow2(&pos, m34C, 75.0f * (1.0f + 2.5f * t), mGndChk, shape_angle.y);
    }

    return true;
}

char daObjFtree::Act_c::M_arcname[] = "Vmr";

namespace daObjFtree {
namespace {
/* 0000455C-0000457C       .text Mthd_Create__Q210daObjFtree27@unnamed@d_a_obj_ftree_cpp@FPv */
cPhs_State Mthd_Create(void* i_this) {
    return ((daObjFtree::Act_c*)i_this)->_create();
}

/* 0000457C-000045A0       .text Mthd_Delete__Q210daObjFtree27@unnamed@d_a_obj_ftree_cpp@FPv */
BOOL Mthd_Delete(void* i_this) {
    return ((daObjFtree::Act_c*)i_this)->_delete();
}

/* 000045A0-000045C4       .text Mthd_Execute__Q210daObjFtree27@unnamed@d_a_obj_ftree_cpp@FPv */
BOOL Mthd_Execute(void* i_this) {
    return ((daObjFtree::Act_c*)i_this)->_execute();
}

/* 000045C4-000045E8       .text Mthd_Draw__Q210daObjFtree27@unnamed@d_a_obj_ftree_cpp@FPv */
BOOL Mthd_Draw(void* i_this) {
    return ((daObjFtree::Act_c*)i_this)->_draw();
}

/* 000045E8-000045F0       .text Mthd_IsDelete__Q210daObjFtree27@unnamed@d_a_obj_ftree_cpp@FPv */
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
}; // namespace daObjFtree

actor_process_profile_definition g_profile_Obj_Ftree = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0003,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_Obj_Ftree_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daObjFtree::Act_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_Obj_Ftree_e,
    /* Actor SubMtd */ &daObjFtree::Mthd_Table,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
