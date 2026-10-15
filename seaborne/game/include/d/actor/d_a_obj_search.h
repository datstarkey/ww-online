#ifndef D_A_OBJ_SEARCH_H
#define D_A_OBJ_SEARCH_H

#include "d/d_bg_s_lin_chk.h"
#include "d/d_bg_w.h"
#include "d/d_cc_d.h"
#include "d/d_kankyo.h"
#include "d/d_lib.h"
#include "d/d_particle.h"
#include "f_op/f_op_actor.h"

class J3DNode;
struct dPath;

namespace daObj_Search {
    class Act_c;

    class Bgc_c {
    public:
        Bgc_c();
        void wall_pos(const daObj_Search::Act_c*, cXyz*, cXyz*, bool*, float*);

        /* 0x00 */ dBgS_LinChk mLinChk;
        /* 0x6C */ cXyz mCrossPos;
    };  // Size: 0x78

    class Act_c : public fopAc_ac_c {
    public:
        enum Proc_e {
            PROC_INIT_e = 0,
            PROC_EXEC_e = 1,
        };

        enum Mode_e {
            MODE_SEARCH_RND_e = 0,
            MODE_SEARCH_PATH_e = 1,
            MODE_STOP_e = 2,
            MODE_TO_SEARCH_e = 3,
            MODE_TO_STOP_e = 4,
            MODE_FIND_e = 5,
            MODE_FIND_2ND_e = 6,
            MODE_SEARCH_BDK_e = 7,
        };

        struct Attr_c {
            /* 0x00 */ u8 mSkipExecute;
            /* 0x01 */ u8 mSkipDraw;
            /* 0x02 */ s16 m02;
            /* 0x04 */ f32 mBeamLength;
            /* 0x08 */ s16 m08;
            /* 0x0A */ u8 m0A[0x10 - 0x0A];
            /* 0x10 */ f32 m10;
            /* 0x14 */ f32 m14;
            /* 0x18 */ f32 m18;
            /* 0x1C */ f32 m1C;
            /* 0x20 */ f32 m20;
            /* 0x24 */ u8 m24;
            /* 0x25 */ u8 m25[0x28 - 0x25];
            /* 0x28 */ f32 mCpsR;
            /* 0x2C */ u8 m2C[0x38 - 0x2C];
            /* 0x38 */ u8 m38;
            /* 0x39 */ u8 m39;
            /* 0x3A */ u8 m3A;
            /* 0x3B */ u8 m3B;
            /* 0x3C */ s16 m3C;
            /* 0x3E */ u8 m3E;
            /* 0x3F */ u8 m3F[0x42 - 0x3F];
            /* 0x42 */ u8 m42;
            /* 0x43 */ u8 m43;
            /* 0x44 */ u8 m44;
            /* 0x45 */ u8 m45;
            /* 0x46 */ u8 m46;
            /* 0x47 */ u8 m47;
            /* 0x48 */ u8 m48[0x4C - 0x48];
            /* 0x4C */ f32 m4C;
            /* 0x50 */ s16 m50;
            /* 0x52 */ u8 m52[0x54 - 0x52];
            /* 0x54 */ f32 m54;
            /* 0x58 */ f32 m58;
            /* 0x5C */ s16 m5C;
            /* 0x5E */ s16 m5E;
        };  // Size: 0x60

        struct ModeEntry {
            typedef void (Act_c::*ModeProcFunc)(void);
            ModeProcFunc mInitFunc;
            ModeProcFunc mUpdFunc;
            const char* mName;
        };

        static bool getFindFlag() { return m_find_flag == true; }
        static void setFindFlag(bool flag) { m_find_flag = flag; }

        static bool m_find_flag;
        static const Attr_c m_attr;

        bool isSecond() { return field_0x860 != false; }
        void modeProcInit(int mode) { modeProc(PROC_INIT_e, mode); }
        void setBkControl(bool control) { mBkControl = control; }
        void setChildId(fpc_ProcID id) { mChildId = id; }

        BOOL _createHeap();
        bool searchCreateHeap();
        bool beamCreateHeap(int);
        void _nodeControl(J3DNode*, J3DModel*);
        void modeSearchRndInit();
        void modeSearchRnd();
        void modeSearchPathInit();
        void modeSearchPath();
        void modeStopInit();
        void modeStop();
        void modeToSearchInit();
        void modeToSearch();
        void modeToStopInit();
        void modeToStop();
        void modeFindInit();
        void modeFind();
        void modeFind2ndInit();
        void modeFind2nd();
        void modeSearchBdkInit();
        void modeSearchBdk();
        void modeProc(Proc_e, int);
        const Attr_c& attr() const;
        void SetArgData();
        void CreateInit();
        cPhs_State _create();
        void smoke_set(float, int);
        bool _execute();
        void check_bk_control();
        void set_mtx_base();
        void set_mtx_light_A();
        void set_mtx_light_B();
        void set_moveBG_mtx_base();
        void set_moveBG_mtx_light_A();
        void set_moveBG_mtx_light_B();
        void bg_check();
        bool player_check();
        bool set_path_info();
        bool is_path_info();
        bool _draw();
        bool _delete();
        BOOL _isdelete();

        static const char m_arc_name[];

    public:
        /* 0x290 */ int mMode;
        /* 0x294 */ Bgc_c mBgc;
        /* 0x30C */ dCcD_Cps mCps[2];
        /* 0x57C */ dCcD_Stts mStts[2];
        /* 0x5F4 */ J3DModel* mpModel;
        /* 0x5F8 */ J3DModel* mpBeamModel[2];
        /* 0x600 */ cXyz mSmokePos;
        /* 0x60C */ cXyz mTargetPos[2];
        /* 0x624 */ cXyz mLightPos[2];
        /* 0x63C */ cXyz mBeamEndPos[2];
        /* 0x654 */ f32 field_0x654;
        /* 0x658 */ f32 field_0x658;
        /* 0x65C */ f32 field_0x65c;
        /* 0x660 */ f32 field_0x660;
        /* 0x664 */ bool mBkControl;
        /* 0x668 */ dBgW* mpBeamBgW[2];
        /* 0x670 */ dBgW* mpBgW;
        /* 0x674 */ Mtx mBeamBgMtx[2];
        /* 0x6D4 */ Mtx mBgMtx;
        /* 0x704 */ u8 field_0x704[0x706 - 0x704];
        /* 0x706 */ csXyz mAngle[2];
        /* 0x712 */ u8 field_0x712[0x71C - 0x712];
        /* 0x71C */ Mtx mBeamMtx[2];
        /* 0x77C */ bool mWallHit[2];
        /* 0x77E */ s16 mBeamAlpha;
        /* 0x780 */ LIGHT_INFLUENCE mLightInfluence;
        /* 0x7A0 */ int mFindTimer;
        /* 0x7A4 */ int field_0x7a4;
        /* 0x7A8 */ s16 mGearAngleL;
        /* 0x7AA */ s16 mGearAngleM;
        /* 0x7AC */ s16 mGearAngleS;
        /* 0x7AE */ s16 mShaftAngle;
        /* 0x7B0 */ s16 field_0x7b0;
        /* 0x7B2 */ s16 field_0x7b2;
        /* 0x7B4 */ s16 field_0x7b4;
        /* 0x7B6 */ bool mBeamCulled[2];
        /* 0x7B8 */ bool mCulled;
        /* 0x7BC */ dLib_circle_path_c mCirclePath;
        /* 0x7E0 */ s16 field_0x7e0;
        /* 0x7E2 */ u8 field_0x7e2;
        /* 0x7E4 */ u8 field_0x7e4[0x7F4 - 0x7E4];
        /* 0x7F4 */ cXyz mSmokeEmitPos;
        /* 0x800 */ csXyz mSmokeEmitAngle;
        /* 0x808 */ dPa_smokeEcallBack mSmokeCB;
        /* 0x828 */ int mSmokeTimer;
        /* 0x82C */ u8 field_0x82c;
        /* 0x82D */ u8 field_0x82d;
        /* 0x830 */ int mLightNo;
        /* 0x834 */ bool field_0x834;
        /* 0x835 */ u8 field_0x835;
        /* 0x836 */ u8 field_0x836;
        /* 0x837 */ u8 mPathId;
        /* 0x838 */ cXyz mPathPos;
        /* 0x844 */ s8 mPathPntNo;
        /* 0x848 */ dPath* mpPath;
#if VERSION == VERSION_DEMO
        u8 pad84C;
#endif
        /* 0x84C */ fpc_ProcID mChildId;
        /* 0x850 */ int field_0x850;
        /* 0x854 */ request_of_phase_process_class mPhase;
        /* 0x85C */ int field_0x85c;
        /* 0x860 */ bool field_0x860;
        /* 0x864 */ dBgS_ObjLinChk mLinChk;
        /* 0x8D0 */ u8 field_0x8d0;
        /* 0x8D4 */ cXyz mFanCenter;
        /* 0x8E0 */ s16 mFanAngle;
        /* 0x8E4 */ f32 mFanRadius;
        /* 0x8E8 */ f32 mCircleRadius;
        /* 0x8EC */ s16 mFanSpread;
        /* 0x8EE */ bool mInFan;
        /* 0x8F0 */ int mSwingTimer;
        /* 0x8F4 */ cXyz mSavePos;
        /* 0x900 */ int mSwingDir;
        /* 0x904 */ int mWaitTimer;
    };  // Size: 0x908

    namespace Mthd {
        cPhs_State Create(void* impl);
        bool Delete(void* impl);
        bool Execute(void* impl);
        bool Draw(void* impl);
        BOOL IsDelete(void* impl);
        extern actor_method_class Table;
    };
};

#endif /* D_A_OBJ_SEARCH_H */
