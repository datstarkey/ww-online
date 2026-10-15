#ifndef D_A_NPC_AC1_H
#define D_A_NPC_AC1_H

#include "f_op/f_op_actor.h"
#include "d/d_npc.h"

class daNpc_Ac1_c : public fopNpc_npc_c {
public:
    daNpc_Ac1_c();
    typedef BOOL (daNpc_Ac1_c::*ActionFunc)(void*);

    struct anm_prm_c {
        s8 field0;
        s8 field1;
        f32 field4;
        f32 field8;
        s32 fieldC;
        s32 field10;
    };

    void nodeWngControl(J3DNode*, J3DModel*);
    void nodeArmControl(J3DNode*, J3DModel*);
    void nodeAc1Control(J3DNode*, J3DModel*);
    bool init_AC1_0();
    bool createInit();
    void setMtx(bool);
    s32 anmNum_toResID(int);
    s32 wingAnmNum_toResID(int);
    s32 btpNum_toResID(int);
    bool setBtp(bool, int);
    bool iniTexPttrnAnm(bool);
    void plyTexPttrnAnm();
    void setAnm_tex(signed char);
    s32 setAnm_anm(anm_prm_c*);
    bool setAnm();
    void chg_anmTag();
    void control_anmTag();
    void chg_anmAtr(unsigned char);
    void control_anmAtr();
    void setAnm_ATR(int);
    void anmAtr(unsigned short);
    void eventOrder();
    void checkOrder();
    bool chk_talk();
    BOOL chk_partsNotMove();
    void lookBack();
    u16 next_msgStatus(u32* pMsgNo);
    s8 getBitMask();
    u32 getMsg_AC1_0();
    u32 getMsg();
    bool chkAttention();
    void setAttention(bool);
    bool charDecide(int);
    void event_actionInit(int);
    bool event_action();
    void privateCut(int);
    void endEvent();
    int isEventEntry();
    void event_proc(int);
    BOOL set_action(ActionFunc, void*);
    void setStt(signed char);
    BOOL wait_1();
    BOOL talk_1();
    BOOL wait_action1(void*);
    bool demo();
    void shadowDraw();
    BOOL _draw();
    BOOL _execute();
    BOOL _delete();
    cPhs_State _create();
    J3DModelData* create_Anm();
    J3DModelData* create_wng_Anm();
    J3DModelData* create_arm_Anm();
    bool create_itm_Mdl();
    BOOL CreateHeap();

public:
    /* 0x6C4 */ u8 m6C4[0x6CC - 0x6C4];
    /* 0x6CC */ s8 m_hed_jnt_num;
    /* 0x6CD */ s8 m_bbone_jnt_num;
    /* 0x6CE */ s8 m_arm_L_jnt_num;
    /* 0x6CF */ s8 m_arm_R_jnt_num;
    /* 0x6D0 */ s8 m_hnd_R_jnt_num;
    /* 0x6D4 */ J3DModel* m6D4;
    /* 0x6D8 */ u32 mShadowId;
    /* 0x6DC */ J3DAnmTexPattern* m_hed_tex_pttrn;
    /* 0x6E0 */ mDoExt_btpAnm mBtpAnim;
    /* 0x6F4 */ u8 m6F4;
    /* 0x6F5 */ u8 field_0x6f5[0x6F6 - 0x6F5];
    /* 0x6F6 */ s16 field_0x6f6;
    /* 0x6F8 */ u8 field_0x6f8[0x6FC - 0x6F8];
    /* 0x6FC */ mDoExt_McaMorf* m6FC;
    /* 0x700 */ s8 m_wngL1_jnt_num;
    /* 0x701 */ s8 m_wngR1_jnt_num;
    /* 0x702 */ u8 m702[0x708 - 0x702];
    /* 0x708 */ mDoExt_McaMorf* m708;
    /* 0x70C */ s8 m_armL1_jnt_num;
    /* 0x70D */ s8 m_armR1_jnt_num;
    /* 0x70E */ u8 m70E[0x710 - 0x70E];
    /* 0x710 */ ActionFunc mCurrActionFunc;
    /* 0x71C */ Mtx mLeftArmMtx;
    /* 0x74C */ Mtx mRightArmMtx;
    /* 0x77C */ dNpc_PathRun_c mPathRun;
    /* 0x784 */ dNpc_EventCut_c mEventCut;
    /* 0x7F0 */ u8 field_0x7f0[0x7F4 - 0x7F0];
    /* 0x7F4 */ cXyz m7F4;
    /* 0x800 */ csXyz m800;
    /* 0x806 */ u8 field_0x806[0x808 - 0x806];
    /* 0x808 */ cXyz mEyePos;
    /* 0x814 */ cXyz m814;
    /* 0x820 */ u8 m820[0x82C - 0x820];
    /* 0x82C */ f32 m82C;
    /* 0x830 */ u8 field_0x830[0x844 - 0x830];
    /* 0x844 */ s16 field_0x844;
    /* 0x846 */ s16 field_0x846;
    /* 0x848 */ s16 field_0x848;
    /* 0x84A */ u8 field_0x84a[0x84C - 0x84A];
    /* 0x84C */ s16 m84C;
    /* 0x84E */ u8 m84E[0x852 - 0x84E];
    /* 0x852 */ s16 m852;
    /* 0x854 */ s16 m854;
    /* 0x856 */ u16 field_0x856;
    /* 0x858 */ u8 m858;
    /* 0x859 */ u8 field_0x859;
    /* 0x85A */ u8 field_0x85a[0x85B - 0x85A];
    /* 0x85B */ u8 field_0x85b;
    /* 0x85C */ u8 field_0x85c[0x85D - 0x85C];
    /* 0x85D */ u8 m85D;
    /* 0x85E */ bool m85E;
    /* 0x85F */ u8 m85F;
    /* 0x860 */ u8 m860;
    /* 0x861 */ u8 m861;
    /* 0x862 */ u8 m862[0x863 - 0x862];
    /* 0x863 */ u8 m863;
    /* 0x864 */ s32 field_0x864;
    /* 0x868 */ u8 field_0x868;
    /* 0x869 */ u8 m869;
    /* 0x86A */ bool m86A;
    /* 0x86B */ bool m86B;
    /* 0x86C */ s8 field_0x86c;
    /* 0x86D */ u8 field_0x86d;
    /* 0x86E */ u8 m86E;
    /* 0x86F */ u8 field_0x86f;
    /* 0x870 */ s8 m870;
    /* 0x871 */ s8 m871;
    /* 0x872 */ u8 m872[0x874 - 0x872];
    /* 0x874 */ s8 m874;
    /* 0x875 */ s8 m875;
    /* 0x876 */ s8 m876;
    /* 0x877 */ s8 m877;
    /* 0x878 */ s8 field_0x878;
    /* 0x879 */ s8 m879;
    /* 0x87A */ s8 m87A;
    /* 0x87B */ s8 field_0x87b;
};

#endif /* D_A_NPC_AC1_H */
