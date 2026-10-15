#ifndef D_A_OBJ_FTREE_H
#define D_A_OBJ_FTREE_H

#include "f_op/f_op_actor.h"
#include "d/d_cc_d.h"
#include "d/d_bg_s_gnd_chk.h"
#include "m_Do/m_Do_ext.h"
#include "SSystem/SComponent/c_phase.h"

namespace daObjFtree {
    struct _ftree_seach_info_ {
        /* 0x0 */ s32 field_0x0;
        /* 0x4 */ s32 field_0x4;
    };

    static void Ftree_get_water_pos(cXyz*, cXyz*);
    static s16 Ftree_checkXyEventCallBack(void*, int);
    static s16 Ftree_XyEventCallBack(void*, int);
    static BOOL is_broughtID(int);
    static BOOL Ftree_NodeCallBack_Effect(J3DNode*, int);
    static BOOL Ftree_NodeCallBack_M(J3DNode*, int);
    static BOOL Ftree_NodeCallBack_L(J3DNode*, int);

    class Act_c : public fopAc_ac_c {
    public:
        static char M_arcname[];

        Act_c();
        virtual ~Act_c() {}
        f32 GetRate(f32 i_value, f32 i_min, f32 i_max) {
            if (i_value <= i_min) {
                return 0.0f;
            } else if (i_value >= i_max) {
                return 1.0f;
            } else {
                return (i_value - i_min) / (i_max - i_min);
            }
        }
        void brought() {}
        void check_heart() {}
        void check_water() {}
        void init_water() {}
        inline s32 param_get_tree_idx() const;
        void set_heart(short) {}
        void set_water(short) {}
        void tree_brouht(int) {}
    
        void search_heart_part();
        BOOL launch_heart_part();
        s16 place_heart_part();
        BOOL estimate_water();
        s16 XyEventCallBack(int);
        void XyEvent_init();
        BOOL XyEvent_set(short);
        void XyEvent_exe();
        BOOL SetJointAnimation(int, float, float, int);
        BOOL PlayStopJointAnimation();
        BOOL PlayStopColorAnimation();
        void set_first_stat();
        void set_collision();
        void talk_ct();
        void talk_main();
        void get_tev_material0_color(J3DModelData*, unsigned long, short*, short*, short*);
        void set_tev_color(J3DModelData*, unsigned long, short, short, short);
        BOOL is_brought();
        void set_broughtID(int);
        void set_brought();
        void unset_broughtID(int);
        void unset_brought();
        void get_ftree_info(_ftree_seach_info_*);
        BOOL iam_last();
        BOOL action_none_init(short);
        void action_none_main();
        BOOL action_waitS_init(short);
        void action_waitS_main();
        BOOL action_waitM_init(short);
        void action_waitM_main();
        BOOL action_waitL_init(short);
        void action_waitL_main();
        BOOL action_pikupikuS_init(short);
        void action_pikupikuS_main();
        BOOL action_pikupikuM_init(short);
        void action_pikupikuM_main();
        BOOL action_pikupikuL_init(short);
        void action_pikupikuL_main();
        BOOL action_changeSL_init(short);
        void action_changeSL_main();
        BOOL action_changeSL2_init(short);
        void action_changeSL2_main();
        BOOL action_changeLS_init(short);
        void action_changeLS_main();
        BOOL action_changeLS2_init(short);
        void action_changeLS2_main();
        BOOL action_changeSM_init(short);
        void action_changeSM_main();
        BOOL action_changeMS_init(short);
        void action_changeMS_main();
        BOOL process_init(int, short);
        void process_main();
        static BOOL solidHeapCB(fopAc_ac_c*);
        BOOL NodeCallBack_Effect(J3DNode*, int);
        bool create_heap();
        cPhs_State _create();
        bool _delete();
        void set_mtx();
        bool _execute();
        bool _draw();
    
    public:
    /* 0x0294 */ s16 m294;
    /* 0x0296 */ s16 m296;
    /* 0x0298 */ s16 m298;
    /* 0x029A */ s16 m29A;
    /* 0x029C */ request_of_phase_process_class mPhs;
    /* 0x02A4 */ s16 field_0x2a4;
    /* 0x02A6 */ u8 field_0x2a6;
    /* 0x02A7 */ u8 field_0x2a7;
    /* 0x02A8 */ Mtx m2A8;
    /* 0x02D8 */ mDoExt_McaMorf* mpMorf;
    /* 0x02DC */ J3DModel* mpModel;
    /* 0x02E0 */ mDoExt_brkAnm mBrkAnm;
    /* 0x02F8 */ dBgS_ObjGndChk mGndChk;
    /* 0x034C */ f32 m34C;
    /* 0x0350 */ s16 mEventCam0Idx;
    /* 0x0352 */ s16 mEventCam1Idx;
    /* 0x0354 */ s16 mEventCam2Idx;
    /* 0x0356 */ s16 m356;
    /* 0x0358 */ s16 m358;
    /* 0x035A */ u8 field_0x35a[0x35C - 0x35A];
    /* 0x035C */ dCcD_Stts m35C;
    /* 0x0398 */ dCcD_Cyl m398;
    /* 0x04C8 */ f32 m4C8;
    /* 0x04CC */ dCcD_Stts m4CC;
    /* 0x0508 */ dCcD_Cyl m508;
    /* 0x0638 */ s32 field_0x638;
    /* 0x063C */ f32 field_0x63c;
    /* 0x0640 */ f32 field_0x640;
    /* 0x0644 */ s32 field_0x644;
    /* 0x0648 */ s16 field_0x648;
    /* 0x064A */ u8 field_0x64a[0x64C - 0x64A];
    /* 0x064C */ fpc_ProcID mSpawnedHeartPieceProcessId;
    /* 0x0650 */ s16 m650;
    /* 0x0652 */ s16 m652;
    /* 0x0654 */ GXColorS10 m654;
    /* 0x065C */ s16 field_0x65c;
    /* 0x065E */ s16 field_0x65e;
    /* 0x0660 */ s16 field_0x660;
    /* 0x0662 */ s16 field_0x662;
    /* 0x0664 */ GXColorS10 m664;
    /* 0x066C */ GXColorS10 m66C;
    /* 0x0674 */ GXColorS10 m674;
    /* 0x067C */ s32 field_0x67c;
    /* 0x0680 */ s32 mMode;
    /* 0x0684 */ s16 field_0x684;
    /* 0x0686 */ u8 field_0x686[0x688 - 0x686];
    /* 0x0688 */ u32 field_0x688;
    /* 0x068C */ u32 field_0x68c;
    /* 0x0690 */ s16 field_0x690;
    /* 0x0692 */ s16 field_0x692;
    /* 0x0694 */ s16 field_0x694;
    /* 0x0696 */ s16 m696;
    /* 0x0698 */ s16 m698;
    /* 0x069A */ s16 field_0x69a;
    /* 0x069C */ s16 field_0x69c;
    /* 0x069E */ s16 field_0x69e;
    /* 0x06A0 */ s16 m6A0;
    /* 0x06A2 */ s16 m6A2;
    /* 0x06A4 */ u32 field_0x6a4;
#if TARGET_PC
    /* 0x06A8 */ uintptr_t field_0x6a8; // holds a pointer
#else
    /* 0x06A8 */ u32 field_0x6a8;
#endif
    /* 0x06AC */ u8 field_0x6ac[0x6B0 - 0x6AC];
    };
};

#endif /* D_A_OBJ_FTREE_H */
