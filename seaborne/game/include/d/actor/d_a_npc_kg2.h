#ifndef D_A_NPC_KG2_H
#define D_A_NPC_KG2_H

#include "d/d_npc.h"
#include "m_Do/m_Do_hostIO.h"

class daNpc_Kg2_c : public fopNpc_npc_c {
public:
    typedef BOOL (daNpc_Kg2_c::*ActionFunc)(void*);

    enum ActionStatus {
        ACTION_STARTING = 0,
        ACTION_ONGOING  = 1,
        ACTION_ENDING   = -1,
    };

    void getAttentionBasePos() {}
    void getEyePos() {}
    void seStart(u32) {}
    void setAction(ActionFunc func, void* arg) {
        if (mCurrActionFunc != func) {
            if (mCurrActionFunc) {
                mActionStatus = ACTION_ENDING;
                (this->*mCurrActionFunc)(NULL);
            }
            mCurrActionFunc = func;
            mActionStatus = ACTION_STARTING;
            (this->*mCurrActionFunc)(arg);
        }
    }
    static void setCanonGameResult(u8 result) { canon_game_result = result; }

    void set_mtx();
    BOOL initTexPatternAnm(bool);
    void playTexPatternAnm();
    void setAnm(s8, f32);
    void subAnm();
    bool chkAttention(cXyz, s16);
    void eventOrder();
    void checkOrder();
    u32 getMsg();
    u16 next_msgStatus(u32* pMsgNo);
    void anmAtr(u16);
    void setAttention();
    void lookBack();
    BOOL CreateHeap();
    BOOL CreateInit();
    void wait01();
    void talk01();
    BOOL evn_setAnm_init(int);
    BOOL evn_setAnm();
    BOOL evn_jnt_lock_init(int);
    BOOL evn_talk_init(int);
    BOOL evn_talk();
    BOOL evn_createItem_init(int);
    BOOL privateCut();
    BOOL processMove();
    BOOL wait_action(void*);
    BOOL event_wait_action(void*);
    cPhs_State _create();
    BOOL _delete();
    BOOL _execute();
    BOOL _draw();

    static void init() {
        canon_game_result = 0;
    }

    static s8 canon_game_result;
    static daNpc_Kg2_c* l_kg2_pointer;

public:
    /* 0x06C4 */ s8 m_handL_num;
    /* 0x06C5 */ u8 field_0x6c5[0x6C8 - 0x6C5];
    /* 0x06C8 */ request_of_phase_process_class mPhase;
    /* 0x06D0 */ u32 field_0x6d0;
    /* 0x06D4 */ J3DModel* mpPlateModel;
    /* 0x06D8 */ mDoExt_btpAnm mBtpAnm;
    /* 0x06EC */ J3DAnmTexPattern* m_btp;
    /* 0x06F0 */ mDoExt_btpAnm mBtpAnm2;
    /* 0x0704 */ u8 field_0x704;
    /* 0x0705 */ u8 field_0x705[0x706 - 0x705];
    /* 0x0706 */ s16 field_0x706;
    /* 0x0708 */ cXyz field_0x708;
    /* 0x0714 */ cXyz field_0x714;
    /* 0x0720 */ s16 field_0x720;
    /* 0x0722 */ u8 field_0x722[0x724 - 0x722];
    /* 0x0724 */ csXyz field_0x724;
    /* 0x072A */ u8 field_0x72a[0x72C - 0x72A];
    /* 0x072C */ u8 field_0x72c;
    /* 0x072D */ u8 field_0x72d;
    /* 0x072E */ u8 field_0x72e[0x734 - 0x72E];
    /* 0x0734 */ u8 field_0x734;
    /* 0x0735 */ u8 field_0x735;
    /* 0x0736 */ u8 field_0x736;
    /* 0x0737 */ u8 field_0x737;
    /* 0x0738 */ u8 field_0x738;
    /* 0x0739 */ s8 field_0x739;
    /* 0x073A */ u8 field_0x73a[0x73C - 0x73A];
    /* 0x073C */ ActionFunc mCurrActionFunc;
    /* 0x0748 */ s8 field_0x748;
    /* 0x0749 */ s8 field_0x749;
    /* 0x074A */ u8 field_0x74a[0x74C - 0x74A];
    /* 0x074C */ u32 field_0x74c;
    /* 0x0750 */ s8 field_0x750;
    /* 0x0751 */ u8 field_0x751[0x754 - 0x751];
    /* 0x0754 */ s32 field_0x754;
    /* 0x0758 */ s16 mEventIdx[4];
    /* 0x0760 */ s8 field_0x760;
    /* 0x0761 */ u8 field_0x761[0x763 - 0x761];
    /* 0x0763 */ s8 mActionStatus;
}; // Size: 0X764

STATIC_ASSERT(sizeof(daNpc_Kg2_c) == 0X764);


#endif /* D_A_NPC_KG2_H */
