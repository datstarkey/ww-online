#ifndef D_A_SITEM_H
#define D_A_SITEM_H

#include "f_op/f_op_actor.h"
#include "d/d_bg_s_acch.h"
#include "d/d_cc_d.h"
#include "d/d_particle.h"
#include "m_Do/m_Do_ext.h"

struct sitem_s {
    /* 0x00 */ cXyz pos;
    /* 0x0C */ u8 field_0xc[0x18 - 0xC];
    /* 0x18 */ f32 field_0x18;
}; // Size: 0x1C

class sitem_class : public fopAc_ac_c {
public:
    /* 0x0290 */ u8 field_0x290[0x2AC - 0x290];
    /* 0x02AC */ request_of_phase_process_class mPhase;
    /* 0x02B4 */ J3DModel* field_0x2b4;
    /* 0x02B8 */ u8 field_0x2b8;
    /* 0x02B9 */ u8 field_0x2b9;
    /* 0x02BA */ u8 field_0x2ba;
    /* 0x02BB */ u8 field_0x2bb;
    /* 0x02BC */ s16 field_0x2bc;
    /* 0x02BE */ u8 field_0x2be[0x2C0 - 0x2BE];
    /* 0x02C0 */ s16 field_0x2c0;
    /* 0x02C2 */ s16 field_0x2c2[2];
    /* 0x02C6 */ s16 field_0x2c6;
    /* 0x02C8 */ cXyz field_0x2c8;
    /* 0x02D4 */ cXyz field_0x2d4;
    /* 0x02E0 */ cXyz field_0x2e0;
    /* 0x02EC */ s16 field_0x2ec;
    /* 0x02EE */ s16 field_0x2ee;
    /* 0x02F0 */ u8 field_0x2f0[0x2F4 - 0x2F0];
    /* 0x02F4 */ f32 field_0x2f4;
    /* 0x02F8 */ u8 field_0x2f8[0x2FC - 0x2F8];
    /* 0x02FC */ f32 field_0x2fc;
    /* 0x0300 */ f32 field_0x300;
    /* 0x0304 */ f32 field_0x304;
    /* 0x0308 */ f32 field_0x308;
    /* 0x030C */ mDoExt_3DlineMat0_c field_0x30c;
    /* 0x0328 */ sitem_s field_0x328[10];
    /* 0x0440 */ mDoExt_3DlineMat0_c field_0x440;
    /* 0x045C */ sitem_s field_0x45c[5];
    /* 0x04E8 */ dCcD_Stts mStts;
    /* 0x0524 */ dCcD_Sph mSph[4];
    /* 0x09D4 */ dCcD_Sph mBmSph;
    /* 0x0B00 */ cXyz field_0xb00;
    /* 0x0B0C */ f32 field_0xb0c;
    /* 0x0B10 */ s16 field_0xb10;
    /* 0x0B14 */ dBgS_AcchCir mAcchCir;
    /* 0x0B54 */ dBgS_ObjAcch mAcch;
    /* 0x0D18 */ dPa_followEcallBack field_0xd18[2];
    /* 0x0D40 */ csXyz field_0xd40[2];
    /* 0x0D4C */ s8 field_0xd4c;
    /* 0x0D4D */ u8 field_0xd4d[0xD50 - 0xD4D];
}; // Size: 0xD50

STATIC_ASSERT(sizeof(sitem_class) == 0xD50);

#endif /* D_A_SITEM_H */
