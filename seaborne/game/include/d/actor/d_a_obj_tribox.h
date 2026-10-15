#ifndef D_A_OBJ_TRIBOX_H
#define D_A_OBJ_TRIBOX_H

#include "f_op/f_op_actor.h"
#include "f_op/f_op_actor_mng.h"
#include "d/d_a_obj.h"
#include "d/d_bg_w.h"
#include "d/d_bg_s_gnd_chk.h"
#include "d/d_bg_s_lin_chk.h"
#include "d/d_com_inf_game.h"
#include "d/actor/d_a_player.h"
#include "m_Do/m_Do_mtx.h"
#include "m_Do/m_Do_ext.h"
#include "m_Do/m_Do_audio.h"
#include "SSystem/SComponent/c_math.h"
#include "JAZelAudio/JAZelAudio_SE.h"
#include <math.h>

namespace daObjTribox {
    class Act_c : public fopAc_ac_c {
    public:
        Act_c() : field_0x398(1, 1, 0, 0), field_0x3b8(1, 1, 0, 0), field_0x3d8(1, 1, 0, 0) {}

        void is_switch() const {}
        void on_switch() const {}
        s32 prm_get_swSave() const { return daObj::PrmAbstract<Prm_e>(this, PRM_SWSAVE_W, PRM_SWSAVE_S); }
        void prm_get_type() const {}

        enum Prm_e {
            PRM_SWSAVE_W = 0x08,
            PRM_SWSAVE_S = 0x08,
            PRM_CORRECT_W = 0x01,
            PRM_CORRECT_S = 0x10,
        };
        BOOL prm_get_correct() const { return daObj::PrmAbstract<Prm_e>(this, PRM_CORRECT_W, PRM_CORRECT_S); }
    
        void set_state();
        static BOOL solidHeapCB(fopAc_ac_c*);
        u8 create_heap();
        void block_init();
        u8 chk_light() const;
        void correct_before_init();
        void correct_after_init();
        void controll_set();
        void controll_clear();
        cPhs_State create_block_before();
        cPhs_State create_block_after();
        cPhs_State create_correct_before();
        cPhs_State create_correct_after();
        cPhs_State _create();
        bool _delete();
        void set_mtx();
        void init_mtx();
        static fopAc_ac_c* push_pullCB(fopAc_ac_c*, fopAc_ac_c*, short, dBgW::PushPullLabel);
        bool line_cross(const cXyz*, const cXyz*) const;
        u8 chk_wall(int) const;
        u8 chk_space() const;
        void eff_flash();
        void eff_smoke_start();
        void eff_smoke_pos();
        void eff_smoke_end();
        void eff_smoke_remove();
        void eff_sink_smoke_init();
        void eff_sink_smoke_start();
        void eff_sink_smoke_end();
        void eff_sink_smoke_remove();
        void vib_sink_init();
        void vib_sink_start();
        void vib_sink_end();
        static void* search_block(void*, void*);
        void sound_pos_init();
        void sound_pos_delete();
        u32 sound_get_mapinfo(const cXyz&);
        void sound_sink_down_block();
        void sound_sink_stop_block();
        void sound_flash_shine();
        void sound_flash_light();
        void mode_block_wait_init();
        void mode_block_wait();
        void mode_block_walk_init();
        void mode_block_walk();
        void mode_block_sink_init();
        void mode_block_sink();
        void mode_block_lower_init();
        void mode_block_lower();
        void mode_correct_off_init();
        void mode_correct_off();
        void mode_correct_on_init();
        void mode_correct_on();
        void mode_correct_demoreq_init();
        void mode_correct_demoreq();
        void mode_correct_demorun_init();
        void mode_correct_demorun();
        void mode_correct_end_init();
        void mode_correct_end();
        void mode_correct_dummy_init();
        void mode_correct_dummy();
        void mode_proc_call();
        void execute_block();
        void execute_correct();
        bool _execute();
        bool _draw();

        static void reset();

        static cXyz M_sound_pos;
        static int M_correct_cnt;
        static bool M_correct_flag;
        static int M_b_cont_cnt;
        static int M_c_cont_cnt;
        static u8 M_sink_start;
        static cXyz M_post[3];
        static dBgS_ObjLinChk M_lin;
        static const char M_arcname[];

    public:
        /* 0x290 */ request_of_phase_process_class mPhase;
#if TARGET_PC
        /* 0x298 */ uintptr_t field_0x298; // holds a pointer
#else
        /* 0x298 */ u32 field_0x298;
#endif
#if TARGET_PC
        /* 0x29C */ uintptr_t field_0x29c; // holds a pointer
#else
        /* 0x29C */ u32 field_0x29c;
#endif
        /* 0x2A0 */ mDoExt_brkAnm mBrkApp;
        /* 0x2B8 */ mDoExt_brkAnm mBrkCmn;
        /* 0x2D0 */ dBgW* mpBgW;
        /* 0x2D4 */ u8 field_0x2d4[0x304 - 0x2D4];
        /* 0x304 */ s32 field_0x304;
        /* 0x308 */ u32 field_0x308;
        /* 0x30C */ s16 mTimer;
        /* 0x30E */ u8 field_0x30e[0x310 - 0x30E];
        /* 0x310 */ u8 field_0x310;
        /* 0x311 */ u8 field_0x311[0x314 - 0x311];
        /* 0x314 */ s32 field_0x314;
        /* 0x318 */ s32 field_0x318;
        /* 0x31C */ s32 field_0x31c;
        /* 0x320 */ u8 field_0x320;
        /* 0x321 */ u8 field_0x321[0x324 - 0x321];
        /* 0x324 */ s32 field_0x324;
        /* 0x328 */ s32 field_0x328;
        /* 0x32C */ s32 field_0x32c;
        /* 0x330 */ u8 field_0x330;
        /* 0x331 */ u8 field_0x331[0x334 - 0x331];
        /* 0x334 */ u32 field_0x334;
        /* 0x338 */ u32 field_0x338;
        /* 0x33C */ u32 field_0x33c;
        /* 0x340 */ s16 field_0x340;
        /* 0x342 */ u8 field_0x342[0x344 - 0x342];
        /* 0x344 */ cXyz mBasePos;
        /* 0x350 */ s32 mPostNo;
        /* 0x354 */ int mPostIdx;
        /* 0x358 */ s16 mMoveDir;
        /* 0x35A */ u8 field_0x35a[0x35C - 0x35A];
        /* 0x35C */ dPa_smokeEcallBack field_0x35c;
        /* 0x37C */ cXyz mSmokePos;
            /* 0x388 */ u16 field_0x388;
        /* 0x38A */ s16 mSmokeAngle;
        /* 0x38C */ u16 field_0x38c;
        /* 0x38E */ u8 field_0x38e;
        /* 0x38F */ u8 field_0x38f[0x390 - 0x38F];
        /* 0x390 */ f32 field_0x390;
        /* 0x394 */ u8 field_0x394;
        /* 0x395 */ u8 field_0x395;
        /* 0x396 */ s16 field_0x396;
        /* 0x398 */ dPa_smokeEcallBack field_0x398;
        /* 0x3B8 */ dPa_smokeEcallBack field_0x3b8;
        /* 0x3D8 */ dPa_smokeEcallBack field_0x3d8;
        /* 0x3F8 */ u8 field_0x3f8;
        /* 0x3F9 */ u8 field_0x3f9;
        /* 0x3FA */ s16 field_0x3fa;
    };
};

#endif /* D_A_OBJ_TRIBOX_H */
