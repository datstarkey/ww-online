#ifndef D_A_SS_H
#define D_A_SS_H

#include "f_op/f_op_actor.h"

#include "d/d_bg_s_gnd_chk.h"
#include "d/d_cc_d.h"
#include "d/d_com_inf_game.h"
#include "d/d_s_play.h"
#include "d/d_particle_name.h"
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_ext.h"
#include "m_Do/m_Do_mtx.h"
#include "JSystem/JMath/JMATrigonometric.h"
#include "JAZelAudio/JAZelAudio_SE.h"
#include "SSystem/SComponent/c_lib.h"
#include "SSystem/SComponent/c_math.h"

struct ss_s_s {
    /* 0x00 */ cXyz mPos;
    /* 0x0C */ u8 field_0xc;
};  // Size: 0x10

struct ss_s {
    /* 0x000 */ u8 field_0x0;
    /* 0x001 */ u8 field_0x1[0x004 - 0x001];
    /* 0x004 */ cXyz mPos;
    /* 0x010 */ s16 field_0x10;
    /* 0x012 */ s16 field_0x12;
    /* 0x014 */ s16 field_0x14;
    /* 0x016 */ u8 field_0x16[0x018 - 0x016];
    /* 0x018 */ cXyz mSpeed;
    /* 0x024 */ f32 field_0x24;
    /* 0x028 */ f32 field_0x28;
    /* 0x02C */ f32 field_0x2c;
    /* 0x030 */ s16 field_0x30;
    /* 0x032 */ s16 field_0x32;
    /* 0x034 */ s16 field_0x34;
    /* 0x036 */ s16 field_0x36;
    /* 0x038 */ s16 field_0x38;
    /* 0x03A */ s8 field_0x3a;
    /* 0x03B */ s8 field_0x3b;
    /* 0x03C */ dCcD_Sph mSph[4];
    /* 0x4EC */ ss_s_s mHand[20];
};  // Size: 0x62C

class ss_class : public fopAc_ac_c {
public:
    /* 0x290 */ u8 field_0x290[0x2AC - 0x290];
    /* 0x2AC */ request_of_phase_process_class mPhs;
    /* 0x2B4 */ s8 field_0x2b4;
    /* 0x2B8 */ mDoExt_McaMorf* mpMorf;
    /* 0x2BC */ u8 field_0x2bc;
    /* 0x2BD */ u8 field_0x2bd;
    /* 0x2BE */ u8 field_0x2be;
    /* 0x2C0 */ s16 field_0x2c0;
    /* 0x2C2 */ u8 field_0x2c2[0x2C4 - 0x2C2];
    /* 0x2C4 */ s16 field_0x2c4;
    /* 0x2C6 */ s16 field_0x2c6;
    /* 0x2C8 */ s16 field_0x2c8[4];
    /* 0x2D0 */ s16 field_0x2d0;
    /* 0x2D2 */ s16 field_0x2d2;
    /* 0x2D4 */ s16 field_0x2d4;
    /* 0x2D6 */ u8 field_0x2d6[0x2D8 - 0x2D6];
    /* 0x2D8 */ s8 field_0x2d8;
    /* 0x2DC */ ss_s mSs[10];
    /* 0x4094 */ mDoExt_3DlineMat0_c mLineMat;
    /* 0x40B0 */ dCcD_Stts mStts;
    /* 0x40EC */ dCcD_Sph mSph;
};  // Size: 0x4218

#endif /* D_A_SS_H */
