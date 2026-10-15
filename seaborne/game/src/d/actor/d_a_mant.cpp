/**
 * d_a_mant.cpp
 * Object - Phantom Ganon's cape/Darknut cape
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_mant.h"
#include "d/d_cc_d.h"
#include "d/d_cc_uty.h"
#include "d/d_bg_s_gnd_chk.h"
#include "d/d_kankyo.h"
#include "dolphin/gf/GFGeometry.h"
#include "dolphin/gf/GFTev.h"
#include "dolphin/gf/GFTransform.h"
#include "JSystem/J3DGraphBase/J3DShape.h"

#include "assets/l_pg_mantle1TEX__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_pg_mantle1_palettePAL__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_boro_b1TEX__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_boro_b1_palettePAL__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_boro_k1TEX__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_boro_k1_palettePAL__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_boro1TEX__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_boro1_palettePAL__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_mantle1TEX__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_tn_mantle1_palettePAL__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_pos__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_texCoord__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_DL__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_matDL__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_matDL2__d_a_mant.h" // native/tools/extract_data_assets.py

#include "assets/l_matDL_LIGHTOFF__d_a_mant.h" // native/tools/extract_data_assets.py


static u8* tex_d[] = {
    l_tn_mantle1TEX, l_tn_boro1TEX, l_tn_boro_b1TEX, NULL,
    l_tn_boro_k1TEX, NULL, l_pg_mantle1TEX,
};

static u8* pal_d[] = {
    l_tn_mantle1_palettePAL, l_tn_boro1_palettePAL, l_tn_boro_b1_palettePAL, NULL,
    l_tn_boro_k1_palettePAL, NULL, l_pg_mantle1_palettePAL,
};


static cXyz* v_pos;
static int v_count;
static int mesh_cc_ct;
static cXyz non_pos(-20000.0f, -200000.0f, -100000.0f);

/* 000000EC-000003EC       .text draw__15daMant_packet_cFv */
void daMant_packet_c::draw() {
    static GXVtxDescList l_vtxDescList[] = {
        {GX_VA_POS, GX_INDEX8},
        {GX_VA_NRM, GX_INDEX8},
        {GX_VA_TEX0, GX_INDEX8},
        {GX_VA_NULL, GX_NONE},
    };
    static GXVtxAttrFmtList l_vtxAttrFmtList[] = {
        {GX_VA_POS, GX_POS_XYZ, GX_F32, 0},
        {GX_VA_NRM, GX_NRM_XYZ, GX_F32, 0},
        {GX_VA_TEX0, GX_TEX_ST, GX_F32, 0},
        {GX_VA_NULL, GX_POS_XYZ, GX_F32, 0},
    };
    GXTlutObj tlutObj;
    GXTexObj texObj;

    u8* tex = tex_d[mTexNo];
    u8* pal = pal_d[mTexNo];

    j3dSys.reinitGX();
    GXSetNumIndStages(0);
    dKy_GxFog_tevstr_set(mpTevStr);
    dKy_setLight_mine(mpTevStr);
    GFSetVtxDescv(l_vtxDescList);
    GFSetVtxAttrFmtv(GX_VTXFMT0, l_vtxAttrFmtList);
#if TARGET_PC
    // Aurora needs each array's byte size and byte order: the current buffer of the
    // position/normal arrays (written by the CPU, host-endian) and the static texcoord table
    // (big-endian float bytes here, unlike the snrubrm/tww fork's f32 table).
    GFSetArraySized(GX_VA_POS, mPos[mCurArr], sizeof(mPos[mCurArr]), sizeof(cXyz), true);
    GFSetArraySized(GX_VA_NRM, mNrm[mCurArr], sizeof(mNrm[mCurArr]), sizeof(cXyz), true);
    GFSetArraySized(GX_VA_TEX0, l_texCoord, sizeof(l_texCoord), 8, false);
#else
    GFSetArray(GX_VA_POS, mPos[mCurArr], sizeof(cXyz));
    GFSetArray(GX_VA_NRM, mNrm[mCurArr], sizeof(cXyz));
    GFSetArray(GX_VA_TEX0, l_texCoord, 8);
#endif
#if TARGET_PC
    // Bug B15: the palettes are 16 colours (32 bytes, the C4 texture's), but the TLUT is loaded
    // with 256 entries. The console's GXLoadTlut copied the 480 bytes after the palette into TMEM
    // unused; Aurora hashes the whole TLUT and read past the global array (ASan, actor sweep).
    GXInitTlutObj(&tlutObj, pal, GX_TL_RGB5A3, 16);
#else
    GXInitTlutObj(&tlutObj, pal, GX_TL_RGB5A3, 0x100);
#endif
    GXInitTexObjCI(&texObj, tex, 0x80, 0x80, GX_TF_C4, GX_CLAMP, GX_CLAMP, GX_FALSE, 0);
    GXInitTexObjLOD(&texObj, GX_LINEAR, GX_LINEAR, 0.0f, 0.0f, 0.0f, GX_FALSE, GX_FALSE, GX_ANISO_1);
    GXLoadTlut(&tlutObj, 0);
    GXLoadTexObj(&texObj, GX_TEXMAP0);

    GXColor k_color = {0xFF, 0xFF, 0xFF, 0xFF};
    if (mArg0 == 1) {
        GXColorS10 color = {0xFF, 0xFF, 0xFF, 0xFF};
        GFSetTevColorS10(GX_TEVREG0, color);
        GFSetTevColor(GX_TEVREG1, k_color);
        GXCallDisplayList(l_matDL_LIGHTOFF, 0x80);
        GFLoadPosMtxImm(mMtx, GX_PNMTX0);
        Mtx nrm;
        mDoMtx_inverseTranspose(mMtx, nrm);
        GFLoadNrmMtxImm(nrm, GX_PNMTX0);
        GXCallDisplayList(l_DL, 0x1E0);
    } else {
        GXColorS10 color = {55, 55, 55, 0xFF};
        GFSetTevColorS10(GX_TEVREG0, color);
        GFSetTevColor(GX_TEVREG1, k_color);
        GXCallDisplayList(l_matDL, 0xA0);
        GFLoadPosMtxImm(mMtx, GX_PNMTX0);
        Mtx nrm;
        mDoMtx_inverseTranspose(mMtx, nrm);
        GFLoadNrmMtxImm(nrm, GX_PNMTX0);
        GXCallDisplayList(l_DL, 0x1E0);

        color.r = 30;
        color.g = 30;
        color.b = 30;
        k_color.r = 0;
        k_color.g = 0;
        k_color.b = 0;
        GFSetTevColorS10(GX_TEVREG0, color);
        GFSetTevColor(GX_TEVREG1, k_color);
        GXCallDisplayList(l_matDL2, 0x40);
        GFLoadPosMtxImm(mMtx2, GX_PNMTX0);
        mDoMtx_inverseTranspose(mMtx2, nrm);
        GFLoadNrmMtxImm(nrm, GX_PNMTX0);
        GXCallDisplayList(l_DL, 0x1E0);
    }

    mCurArr ^= 1;
    J3DShape::resetVcdVatCache();
}

/* 000003EC-000004E8       .text daMant_Draw__FP10mant_class */
static BOOL daMant_Draw(mant_class* i_this) {
    fopAc_ac_c* actor = i_this;
    if (actor->scale.y <= 0.01f) {
        return TRUE;
    }
    g_env_light.settingTevStruct(TEV_TYPE_BG0_FULL, &actor->current.pos, &actor->tevStr);
    MtxTrans(0.0f, 0.0f, 0.0f, 0);
    cMtx_concat(j3dSys.getViewMtx(), *calc_mtx, i_this->mPacket.getMtx());
    if (i_this->mType != 1) {
        MtxTrans(0.0f, -3.0f, 0.0f, 0);
        cMtx_concat(j3dSys.getViewMtx(), *calc_mtx, i_this->mPacket.getMtx2());
    }
    i_this->mPacket.setTevStr(&actor->tevStr);
    j3dSys.getDrawBuffer(0)->entryImm(&i_this->mPacket, 0);
    return TRUE;
}

/* 000004E8-00000FC0       .text joint_control__FP10mant_classP8mant_j_si */
static void joint_control(mant_class* i_this, mant_j_s* i_joint, int i_idx) {
    static f32 d_p[8] = {0.6f, 0.4f, 0.3f, 0.3f, 0.2f, 0.2f, 0.2f, 0.1f};
    static f32 d_p2[8] = {5.0f, 2.0f, 1.0f, 0.5f, 0.0f, -0.3f, -0.3f, 0.0f};
    fopAc_ac_c* actor = i_this;
    int i;
    cXyz* pos = i_joint->mPos;
    cXyz* speed = i_joint->mSpeed;
    cXyz base_offset;
    cXyz mtx_result;
    cXyz rot_offset;
    cXyz scaled_offset;
    cXyz damp_offset;
    cXyz wave_offset;
    cXyz wave_result;
    cXyz wind_pos;
    cXyz swing_offset;
    dBgS_GndChk gnd_chk;
    f32 dy;
    f32 x;
    f32 ground_y;
    f32 damp_scale;

    f32 chk_y = pos[0].y;
    gnd_chk.m_pos.set(pos[0].x, chk_y += 50.0f, pos[0].z);

    ground_y = 1.5f + dComIfG_Bgsp()->GroundCross(&gnd_chk);
    if (ground_y - pos[0].y > 50.0f) {
        ground_y = pos[0].y;
    }

    damp_offset.x = 0.0f;
    damp_offset.y = 0.0f;
    damp_offset.z = 0.0f;
    wave_offset.x = 0.0f;
    wave_offset.y = 0.0f;
    wave_offset.z = 0.0f;
    wave_result.x = 0.0f;
    wave_result.y = 0.0f;
    wave_result.z = 0.0f;
    mDoMtx_YrotS(*calc_mtx, i_joint->mAngle.y);
    base_offset.x = 0.0f;
    base_offset.y = 0.0f;
    base_offset.z = -5.0f;
    MtxPosition(&base_offset, &rot_offset);

    swing_offset.x = 0.0f;
    swing_offset.y = 0.0f;
    swing_offset.z = 0.0f;

    s16 angle_diff = i_this->m1C0C + (s16)((i_idx - 4) * 3000);
    mDoMtx_YrotS(*calc_mtx, angle_diff);

    base_offset.x = 0.0f;
    base_offset.y = 0.0f;
    base_offset.z = i_this->m1C00;

    s16 abs_diff = angle_diff - i_joint->mAngle.y;
    if (abs_diff < 0) {
        abs_diff *= -1;
    }

    if ((u16)abs_diff < 0x4000) {
        base_offset.z *= 0.05f;
        damp_scale = 0.0f;
    } else {
        damp_scale = 1.0f;
    }

    MtxPosition(&base_offset, &wind_pos);

    base_offset.x = 0.0f;
    base_offset.y = 0.0f;
    base_offset.z = i_this->m1BF8 + cM_ssin(i_idx * 0x59D8);
    base_offset.z *= actor->scale.y;

    for (i = 0; i < 9; i++, pos++, speed++) {
        if (i > 0) {
            scaled_offset = rot_offset * d_p[i - 1];

            if (std::fabsf(i_this->m1C00) > 0.1f) {
                swing_offset = wind_pos;
                swing_offset.y = 0.5f * i_this->m1C00 * cM_ssin((i_this->mCounter << 12) + i * 10000 + i_idx * 10000);
            }

            if (i_this->m1C04 > 0.01f) {
                damp_offset = rot_offset * d_p2[i - 1] * i_this->m1C04 * damp_scale;
            }

            if (i_this->mType == 1) {
                wave_offset.z = 2.0f * cM_ssin((i_this->mCounter << 11) + i * 10000 + i_idx * 10000);
                mDoMtx_YrotS(*calc_mtx, i_joint->mAngle.y);
                MtxPosition(&wave_offset, &wave_result);
            }

            x = wave_result.x + (damp_offset.x + (swing_offset.x + (scaled_offset.x + (speed->x + (pos->x - pos[-1].x)))));
            f32 z = wave_result.z + (damp_offset.z + (swing_offset.z + (scaled_offset.z + (speed->z + (pos->z - pos[-1].z)))));
            f32 y = swing_offset.y + (i_this->m1C08 + (pos->y + speed->y));
            if (y < ground_y) {
                y = ground_y;
            }

            dy = y - pos[-1].y;
            s16 x_angle = -cM_atan2s(dy, z);
            f32 len = std::sqrtf(dy * dy + z * z);
            s16 y_angle = (s16)cM_atan2s(x, len);

            mDoMtx_XrotS(*calc_mtx, x_angle);
            mDoMtx_YrotM(*calc_mtx, y_angle);
            MtxPosition(&base_offset, &mtx_result);

            speed->x = pos->x;
            speed->y = pos->y;
            speed->z = pos->z;
            pos->x = pos[-1].x + mtx_result.x;
            pos->y = pos[-1].y + mtx_result.y;
            pos->z = pos[-1].z + mtx_result.z;
            speed->x = i_this->m1BFC * (pos->x - speed->x);
            speed->y = i_this->m1BFC * (pos->y - speed->y);
            speed->z = i_this->m1BFC * (pos->z - speed->z);
        }

        *v_pos = *pos;
        v_pos++;
        v_count++;

        if (i_this->mType == 0 && (i & 1) == 0 && (i_idx & 1) == 0 && i != 0 && i != 8 && i_idx != 0 && i_idx != 8) {
            if (i_this->m2834 != 0) {
                i_this->mMeshSph[mesh_cc_ct].SetR(30.0f);
                i_this->mMeshSph[mesh_cc_ct].SetC(*pos);
            } else {
                i_this->mMeshSph[mesh_cc_ct].SetR(-200.0f);
                i_this->mMeshSph[mesh_cc_ct].SetC(non_pos);
            }
            dComIfG_Ccsp()->Set(&i_this->mMeshSph[mesh_cc_ct]);

            if (i_this->m2836 == 0 && i_this->m2838 == 0 && i_this->mMeshSph[mesh_cc_ct].ChkTgHit()) {
                i_this->m2836 = 10;
                CcAtInfo atInfo;
                atInfo.mpObj = i_this->mMeshSph[mesh_cc_ct].GetTgHitObj();
                at_power_check(&atInfo);
                fopAc_ac_c* player = (fopAc_ac_c*)dComIfGp_getPlayer(0);

                s8 hit = 0;
                if (atInfo.mResultingAttackType == 1 || atInfo.mResultingAttackType == 2) {
                    if (atInfo.mResultingAttackType == 2) {
                        if (i_this->mPacket.getTexNo() == 0) {
                            i_this->mPacket.setTexNo(2);
                            i_this->m2834 = 0;
                            hit = 1;
                        }
                    } else if (atInfo.mResultingAttackType == 1) {
                        if (i_this->mPacket.getTexNo() == 0) {
                            i_this->mPacket.setTexNo(4);
                            i_this->m2834 = 0;
                            hit = 1;
                        }
                    }
                }

                if (hit != 0) {
                    csXyz angle = actor->current.angle;
                    angle.y += 0x8000;
                    dComIfGp_particle_set(dPa_name::ID_IT_SN_TN_MANTOKIRE00, pos, &angle, NULL, 0xFF, NULL, fopAcM_GetRoomNo(actor), &actor->tevStr.mColorK0, &actor->tevStr.mColorK0);
                    fopAcM_seStartCurrent(actor, JA_SE_CM_TN_BREAK_MANTLE, 0);
                }

                i_this->m1C0E = 2;
                i_this->m1C0C = player->shape_angle.y;
                i_this->m1C00 = 10.0f;

                if (atInfo.mResultingAttackType == 5) {
                    i_this->m2838 = 26;
                    i_this->m1C0E = 6;
                    return;
                }
            }
            mesh_cc_ct++;

            if (i == 4 && i_idx == 4) {
                i_this->mWindSph.SetR(200.0f);
                i_this->mWindSph.SetC(*pos);
                dComIfG_Ccsp()->Set(&i_this->mWindSph);
            }
        }
    }
}

/* 0000137C-000015F0       .text mant_v_calc__FP10mant_class */
static void mant_v_calc(mant_class* i_this) {
    csXyz angle(0, 0, 0);
    cXyz offset;
    cXyz mtx_pos;
    cXyz diff = i_this->m1BE0[0] - i_this->m1BE0[1];
    angle.y = cM_atan2s(diff.x, diff.z) + 0x4000;

    mant_j_s* joint = i_this->mJoint;
    offset.x = 0.0f;
    for (int i = 0; i < 9; i++, joint++) {
        i_this->mJoint[i].mPos[0].x = i_this->m1BE0[1].x + (diff.x / 8.0f) * i;
        i_this->mJoint[i].mPos[0].y = i_this->m1BE0[1].y + (diff.y / 8.0f) * i;
        i_this->mJoint[i].mPos[0].z = i_this->m1BE0[1].z + (diff.z / 8.0f) * i;

        mDoMtx_YrotS(*calc_mtx, angle.y);
        f32 s = cM_ssin(cM_rad2s(0.3926991f * i));
        offset.y = -10.0f * s;
        offset.z = -20.0f * s;
        MtxPosition(&offset, &mtx_pos);
        i_this->mJoint[i].mPos[0] += mtx_pos;

        i_this->mJoint[i].mAngle = angle;
        i_this->mJoint[i].mAngle.y += (s16)((i - 4) * 3000);

        joint_control(i_this, joint, i);
    }
}

/* 000015F0-000017EC       .text mant_n_calc__FP10mant_class */
static void mant_n_calc(mant_class* i_this) {
    cXyz base;
    cXyz rotated;
    cXyz diff;

    base.x = 0.0f;
    base.y = 0.0f;
    base.z = 1.0f;
    mDoMtx_YrotS(*calc_mtx, 0x4FA0);
    MtxPosition(&base, &rotated);

    cXyz* pos = i_this->mPacket.getPos();
    cXyz* nrm = i_this->mPacket.getNrm();
    for (int i = 0; i < 81; i++, pos++, nrm++) {
        if (i == 8 || i == 17 || i == 26 || i == 35 || i == 44 || i == 53 || i == 62 || i == 71) {
            nrm->x = nrm[-1].x;
            nrm->y = nrm[-1].y;
            nrm->z = nrm[-1].z;
        } else if (i >= 72) {
            nrm->x = nrm[-9].x;
            nrm->y = nrm[-9].y;
            nrm->z = nrm[-9].z;
        } else {
            diff = pos[10] - pos[0];
            mDoMtx_YrotS(*calc_mtx, cM_atan2s(diff.x, diff.z));
            mDoMtx_XrotM(*calc_mtx, -cM_atan2s(diff.y, std::sqrtf(diff.x * diff.x + diff.z * diff.z)));
            MtxPosition(&rotated, nrm);
        }
    }
}

/* 000017EC-000019C0       .text mant_move__FP10mant_class */
static void mant_move(mant_class* i_this) {
    v_count = 0;
    v_pos = i_this->mPacket.getPos();
    mesh_cc_ct = 0;

    mant_v_calc(i_this);
    mant_n_calc(i_this);
    DCStoreRangeNoSync(i_this->mPacket.getPos(), sizeof(cXyz) * 81);

    if (i_this->m2838 != 0) {
        i_this->m2838--;
        cXyz* pos = i_this->mPacket.getPos();
        for (int i = 0; i < 81; i++, pos++) {
            if ((i & 3) == 0) {
                dComIfGp_particle_setSimple(dPa_name::ID_IT_SN_O_TN_MANTOFIRE00, pos);
            }
        }
        if (i_this->m2838 <= 2) {
            fopAcM_delete(i_this);
            return;
        }
    }

    cLib_addCalc2(&i_this->m1BF8, 30.0f, 0.1f, 1.0f);
    cLib_addCalc2(&i_this->m1BFC, 0.7f, 0.1f, 0.05f);

    if (i_this->mWindSph.ChkTgHit()) {
        i_this->m1C0E = 10;
        i_this->m1C0C = dComIfGp_getPlayer(0)->shape_angle.y;
    }

    if (i_this->m1C0E != 0) {
        i_this->m1C0E--;
        cLib_addCalc2(&i_this->m1C00, 50.0f, 0.2f, 1.5f);
    } else {
        cLib_addCalc0(&i_this->m1C00, 0.1f, 1.0f);
    }

    if (i_this->m1C0E > 4) {
        cLib_addCalc2(&i_this->m1C04, 1.0f, 1.0f, 1.0f);
    } else {
        cLib_addCalc0(&i_this->m1C04, 0.1f, 0.1f);
    }
}

/* 000019C0-00001A3C       .text daMant_Execute__FP10mant_class */
static BOOL daMant_Execute(mant_class* i_this) {
    fopAc_ac_c* actor = i_this;
    i_this->mCounter++;
    if (i_this->m2836 != 0) {
        i_this->m2836--;
    }
    mant_move(i_this);
    MtxTrans(actor->current.pos.x, actor->current.pos.y, actor->current.pos.z, false);
    cMtx_copy(*calc_mtx, i_this->mCullMtx);
    return TRUE;
}

/* 00001A3C-00001A44       .text daMant_IsDelete__FP10mant_class */
static BOOL daMant_IsDelete(mant_class*) {
    return TRUE;
}

/* 00001A44-00001A4C       .text daMant_Delete__FP10mant_class */
static BOOL daMant_Delete(mant_class*) {
    return TRUE;
}

/* 00001A4C-00001D18       .text daMant_Create__FP10fopAc_ac_c */
static cPhs_State daMant_Create(fopAc_ac_c* i_this) {
    static dCcD_SrcSph wind_cc_sph_src = {
        // dCcD_SrcGObjInf
        {
            /* Flags             */ 0,
            /* SrcObjAt  Type    */ 0,
            /* SrcObjAt  Atp     */ 0,
            /* SrcObjAt  SPrm    */ 0,
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
            /* SrcGObjTg SPrm    */ 0,
            /* SrcGObjCo SPrm    */ 0,
        },
        // cM3dGSphS
        {{
            /* Center */ {0.0f, 0.0f, 0.0f},
            /* Radius */ 200.0f,
        }},
    };
    static dCcD_SrcSph mesh_cc_sph_src = {
        // dCcD_SrcGObjInf
        {
            /* Flags             */ 0,
            /* SrcObjAt  Type    */ 0,
            /* SrcObjAt  Atp     */ 0,
            /* SrcObjAt  SPrm    */ 0,
            /* SrcObjTg  Type    */ AT_TYPE_ALL & ~AT_TYPE_WATER & ~AT_TYPE_UNK20000 & ~AT_TYPE_WIND & ~AT_TYPE_UNK400000 & ~AT_TYPE_LIGHT,
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
            /* SrcGObjTg SPrm    */ 0,
            /* SrcGObjCo SPrm    */ 0,
        },
        // cM3dGSphS
        {{
            /* Center */ {0.0f, 0.0f, 0.0f},
            /* Radius */ 30.0f,
        }},
    };
    mant_class* a_this = (mant_class*)i_this;

    fopAcM_ct(a_this, mant_class);

    a_this->mType = fopAcM_GetParam(i_this);
    fopAcM_SetMin(i_this, -2000.0f, -2000.0f, -2000.0f);
    fopAcM_SetMax(i_this, 2000.0f, 2000.0f, 2000.0f);
    fopAcM_SetMtx(i_this, a_this->mCullMtx);
    a_this->mPacket.setarg0(a_this->mType);

    if (a_this->mType == 1) {
        a_this->m1BF8 = 40.0f;
        a_this->m1BFC = 0.8f;
        a_this->m1C08 = -5.0f;
        a_this->mPacket.setTexNo(6);
    } else {
        a_this->m1BF8 = 30.0f;
        a_this->m1BFC = 0.7f;
        a_this->m1C08 = -10.0f;
        a_this->mStts.Init(200, 0xFF, i_this);
        a_this->mWindSph.Set(wind_cc_sph_src);
        a_this->mWindSph.SetStts(&a_this->mStts);
        for (int i = 0; i < 9; i++) {
            a_this->mMeshSph[i].Set(mesh_cc_sph_src);
            a_this->mMeshSph[i].SetStts(&a_this->mStts);
        }
        a_this->m2834 = 10;
    }

    i_this->scale.x = 1.0f;
    i_this->scale.y = 1.0f;
    i_this->scale.z = 1.0f;

    for (int i = 0; i < 10; i++) {
        daMant_Execute(a_this);
    }

    return cPhs_COMPLEATE_e;
}

static actor_method_class l_daMant_Method = {
    (process_method_func)daMant_Create,
    (process_method_func)daMant_Delete,
    (process_method_func)daMant_Execute,
    (process_method_func)daMant_IsDelete,
    (process_method_func)daMant_Draw,
};

actor_process_profile_definition g_profile_MANT = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_MANT_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(mant_class),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_MANT_e,
    /* Actor SubMtd */ &l_daMant_Method,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK4000_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
