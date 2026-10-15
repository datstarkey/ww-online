#ifndef D_A_OBJ_TRY_H
#define D_A_OBJ_TRY_H

#include "d/d_a_obj.h"
#include "f_op/f_op_actor.h"
#include "SSystem/SComponent/c_bg_s_poly_info.h"
#include "d/d_bg_s_acch.h"
#include "d/d_cc_d.h"
#include "d/d_particle.h"
#include "m_Do/m_Do_ext.h"

namespace daObjTry {
    enum Type_e {
        UNK_2 = 2,
        UNK_3 = 3,
        UNK_4 = 4,
        UNK_5 = 5,
        UNK_6 = 6,
        UNK_7 = 7,
        UNK_8 = 8,
        UNK_9 = 9,
        UNK_10 = 10,
        UNK_11 = 11,
        UNK_12 = 12,
    };

    struct Attr_c {
        /* 0x00 */ f32 mGravity;
        /* 0x04 */ f32 mSinkH;
        /* 0x08 */ f32 mAttnH;
        /* 0x0C */ u8 mWeight;
        /* 0x10 */ f32 mDropSpeedY;
        /* 0x14 */ f32 mDropSpeedF;
        /* 0x18 */ f32 mDropSpeedFRate;
        /* 0x1C */ f32 mViscousResist;
        /* 0x20 */ f32 mInertResist;
        /* 0x24 */ f32 mSinkGravity;
        /* 0x28 */ f32 mSinkViscousResist;
        /* 0x2C */ f32 mSinkInertResist;
        /* 0x30 */ f32 mSinkMaxSpeed;
        /* 0x34 */ s16 field_0x34;
        /* 0x36 */ s16 field_0x36;
        /* 0x38 */ f32 field_0x38;
        /* 0x3C */ f32 field_0x3c;
        /* 0x40 */ u8 field_0x40;
        /* 0x41 */ u8 field_0x41;
        /* 0x42 */ u8 mSeHitP1;
        /* 0x43 */ u8 mSeHitP2;
        /* 0x44 */ u8 mSeFallWaterP1;
        /* 0x45 */ u8 mSeFallWaterP2;
        /* 0x46 */ s16 mBdlIdx;
        /* 0x48 */ s16 mBrkIdx;
        /* 0x4A */ u8 mShadowSize;
        /* 0x4B */ u8 mCylR;
        /* 0x4C */ u8 mCylH;
        /* 0x4D */ u8 mSinkDownDepth;
        /* 0x50 */ u32 field_0x50;
        /* 0x54 */ u32 mSeLand;
        /* 0x58 */ u32 mSeFallWater;
        /* 0x5C */ u32 mSeHit;
        /* 0x60 */ u32 mHeapSize;
        /* 0x64 */ f32 field_0x64;
        /* 0x68 */ f32 field_0x68;
        /* 0x6C */ f32 field_0x6c;
        /* 0x70 */ bool mChkSwitch;
        /* 0x71 */ bool mUnique;
        /* 0x72 */ bool mClrSwSave;
        /* 0x73 */ bool mCorrectPos;
        /* 0x74 */ bool mStayCorrect;
        /* 0x75 */ bool mNoCull;
        /* 0x76 */ u8 field_0x76;
        /* 0x77 */ u8 field_0x77;
    };  // Size: 0x78

    class Act_c : public fopAc_ac_c {
    public:
        enum Prm_e {
            PRM_SWSAVE_W = 8,
            PRM_SWSAVE_S = 8,

            PRM_TYPE_W = 4,
            PRM_TYPE_S = 0,

            PRM_DUMMY_W = 1,
            PRM_DUMMY_S = 0x1F,
        };

        enum Mode_e {
            MODE_RESTART = 0,
            MODE_WAIT = 1,
            MODE_CARRY = 2,
            MODE_DROP = 3,
            MODE_SINK = 4,
        };

        Act_c() { mpBingoEmitter = NULL; }
        const Attr_c& attr() const { return M_attr[mType]; }
        bool prm_chk_type_tryStatue() const { return mType == UNK_5 || mType == UNK_6; }
        int prm_get_dummy() const {
            return daObj::PrmAbstract(this, PRM_DUMMY_W, PRM_DUMMY_S);
        }
        int prm_get_swSave() const {
            return daObj::PrmAbstract(this, PRM_SWSAVE_W, PRM_SWSAVE_S);
        }
        int prm_get_type() const {
            return daObj::PrmAbstract(this, PRM_TYPE_W, PRM_TYPE_S);
        }
        void to_correct_pos(const cXyz* i_pos, short i_angle, bool i_stay, bool i_correct) {
            mCorrectPos.set(*i_pos);
            mCorrectAngle = i_angle;
            mStayCorrect = i_stay;
            mCorrect = i_correct;
        }

        void prm_set_swSave(int);
        static BOOL solidHeapCB(fopAc_ac_c*);
        bool create_heap();
        void init_cc();
        static void* search_sameType(void*, void*);
        bool chk_appear() const;
        cPhs_State _create();
        bool _delete();
        void mode_restart_init();
        void mode_restart();
        void mode_wait_init();
        void mode_wait();
        void mode_carry_init();
        void mode_carry();
        void mode_drop_init();
        void mode_drop();
        void mode_sink_init();
        void mode_sink();
        bool mode_proc_call();
        void cull_set_draw();
        void cull_set_move();
        void damaged();
        bool damage_cc_proc();
        bool damage_bg_proc();
        void damage_bg_proc_directly();
        bool chk_sink_water();
        bool chk_sinkdown_water();
        void calc_drop_param(float*, float*, float*) const;
        bool bound();
        void se_fall_water();
        void set_senv(int, int) const;
        void cam_lockoff() const;
        void set_mtx();
        void init_mtx();
        void eff_set_bingo(bool, bool);
        void eff_clr_bingo();
        void eff_land_smoke();
        void eff_hit_water_splash();
        void make_vib();
        bool check_circle();
        f32 get_water_h();
        bool _execute();
        bool _draw();

        static const char M_arcname[];
        static const dCcD_SrcCyl M_cyl_src;
        static const Attr_c M_attr[];
        static bool M_bingo;
        static bool M_restart;

    public:
        /* 0x290 */ request_of_phase_process_class mPhs;
        /* 0x298 */ J3DModel* mpModel;
        /* 0x29C */ mDoExt_brkAnm mBrk;
        /* 0x2B4 */ dBgS_ObjAcch mAcch;
        /* 0x478 */ dBgS_AcchCir mAcchCir;
        /* 0x4B8 */ dCcD_Stts mStts;
        /* 0x4F4 */ dCcD_Cyl mCyl;
        /* 0x624 */ int mType;
        /* 0x628 */ int mMode;
        /* 0x62C */ int mPrevMode;
        /* 0x630 */ s16 mTimer;
        /* 0x632 */ s16 mLandTimer;
        /* 0x634 */ bool mOnGround;
        /* 0x635 */ bool mForceExec;
        /* 0x636 */ u8 mDropTimer;
        /* 0x638 */ f32 mOldSpeedY;
        /* 0x63C */ cXyz mCorrectPos;
        /* 0x648 */ s16 mCorrectAngle;
        /* 0x64A */ s16 mTargetAngle;
        /* 0x64C */ bool mStayCorrect;
        /* 0x64D */ bool mCorrect;
        /* 0x64E */ bool field_0x64e;
        /* 0x64F */ bool mRoomChanged;
        /* 0x650 */ bool mAppear;
        /* 0x651 */ bool mBingoEff;
        /* 0x652 */ bool field_0x652;
        /* 0x653 */ bool mSunk;
        /* 0x654 */ dPa_followEcallBack mFollowCb;
        /* 0x668 */ JPABaseEmitter* mpBingoEmitter;
    };  // Size: 0x66C

    STATIC_ASSERT(sizeof(Act_c) == 0x66C);
};

#endif /* D_A_OBJ_TRY_H */
