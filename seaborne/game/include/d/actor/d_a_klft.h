#ifndef D_A_KLFT_H
#define D_A_KLFT_H

#include "f_op/f_op_actor.h"
#include "d/d_bg_w.h"

#include "d/d_cc_d.h"
#include "d/d_cc_uty.h"
#include "d/d_com_inf_game.h"
#include "d/d_s_play.h"
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_audio.h"
#include "m_Do/m_Do_ext.h"
#include "m_Do/m_Do_mtx.h"
#include "JAZelAudio/JAZelAudio_SE.h"
#include "SSystem/SComponent/c_lib.h"
#include "SSystem/SComponent/c_math.h"

class klft_class : public fopAc_ac_c {
public:
    /* 0x290 */ request_of_phase_process_class mPhs;
    /* 0x298 */ s16 m298;
    /* 0x29A */ u8 m29A[0x29C - 0x29A];
    /* 0x29C */ J3DModel* mpModel;
    /* 0x2A0 */ mDoExt_McaMorf* mpMorf[2];
    /* 0x2A8 */ cXyz m2A8[2];
    /* 0x2C0 */ s16 m2C0;
    /* 0x2C2 */ u8 m2C2;
    /* 0x2C3 */ u8 m2C3;
    /* 0x2C4 */ cXyz m2C4;
    /* 0x2D0 */ cXyz m2D0;
    /* 0x2DC */ cXyz m2DC;
    /* 0x2E8 */ cXyz m2E8;
    /* 0x2F4 */ cXyz m2F4;
    /* 0x300 */ csXyz m300;
    /* 0x306 */ u8 m306[0x30C - 0x306];
    /* 0x30C */ Mtx m30C;
    /* 0x33C */ dBgW* pm_bgw;
    /* 0x340 */ mDoExt_3DlineMat1_c m340;
    /* 0x37C */ cXyz m37C[2];
    /* 0x394 */ cXyz m394;
    /* 0x3A0 */ cXyz m3A0;
    /* 0x3AC */ s16 m3AC;
    /* 0x3AE */ s16 m3AE;
    /* 0x3B0 */ s16 m3B0;
    /* 0x3B2 */ s16 m3B2;
    /* 0x3B4 */ f32 m3B4;
    /* 0x3B8 */ f32 m3B8;
    /* 0x3BC */ f32 m3BC;
    /* 0x3C0 */ f32 m3C0;
    /* 0x3C4 */ f32 m3C4;
    /* 0x3C8 */ f32 m3C8;
    /* 0x3CC */ dCcD_Stts mStts;
    /* 0x408 */ dCcD_Sph mSph;
    /* 0x534 */ dCcD_Cyl mCyl[2];
    /* 0x794 */ dCcD_Sph mSph2[2];
    /* 0x9EC */ s16 m9EC;
    /* 0x9EE */ u8 m9EE[0x9F0 - 0x9EE];
};

#endif /* D_A_KLFT_H */
