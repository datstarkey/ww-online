#ifndef D_A_NPC_KG1_H
#define D_A_NPC_KG1_H

#include "d/d_npc.h"
#include "m_Do/m_Do_ext.h"
#include "SSystem/SComponent/c_phase.h"
#include "m_Do/m_Do_hostIO.h"

class daNpc_Kg1_c : public fopNpc_npc_c {
public:
    daNpc_Kg1_c();
    inline cPhs_State _create();
    inline bool _delete();
    inline bool _draw();
    inline bool _execute();
    void getAttentionBasePos() {}
    void getEyePos() {}
    void setAction(void (daNpc_Kg1_c::*i_func)()) { mAction = i_func; }

    void lookBack();
    BOOL chkAttention();
    BOOL initTexPatternAnm(int, bool);
    void playTexPatternAnm();
    void set_mtx();
    BOOL CreateHeap();
    cPhs_State CreateInit();
    void eventOrder();
    void checkOrder();
    void kg1_talk_camera();
    void wait_action_init();
    void wait_action();
    void clr_seq_flag();
    u32 getMsg();
    u16 next_msgStatus(u32* pMsgNo);
    void anmAtr(u16);
    void setAnm();

    static const char m_arcname[];
    static const f32 m_camera_fovy;
    static cXyz m_camera_ctr;
    static cXyz m_camera_eye;
    static const u8 m_highscore;

public:
    /* 0x6C4 */ J3DModel* m6C4;
    /* 0x6C8 */ mDoExt_btpAnm mBtpAnm;
    /* 0x6DC */ request_of_phase_process_class mPhs;
    /* 0x6E4 */ void (daNpc_Kg1_c::*mAction)();
    /* 0x6F0 */ u8 m6F0[0x6F4 - 0x6F0];
    /* 0x6F4 */ J3DAnmTexPattern* m_eye_tex_pattern;
    /* 0x6F8 */ mDoExt_btpAnm m6F8;
    /* 0x70C */ mDoExt_btpAnm m70C;
    /* 0x720 */ u8 m720;
    /* 0x721 */ u8 m721[0x724 - 0x721];
    /* 0x724 */ u32 mShadowId;
    /* 0x728 */ u8 m728[0x72C - 0x728];
    /* 0x72C */ s16 m72C;
    /* 0x72E */ s16 m72E;
    /* 0x730 */ u8 m730;
    /* 0x731 */ u8 m731;
    /* 0x732 */ u8 m732;
    /* 0x733 */ u8 m733[0x734 - 0x733];
    /* 0x734 */ cXyz m734;
    /* 0x740 */ cXyz m740;
    /* 0x74C */ s8 m74C;
    /* 0x74D */ s8 m74D;
    /* 0x74E */ s8 m74E;
    /* 0x74F */ u8 field_0x74f;
    /* 0x750 */ u8 m750;
    /* 0x751 */ u8 m751;
    /* 0x752 */ u8 m752[0x770 - 0x752];
    /* 0x770 */ u8 field_0x770;
    /* 0x771 */ u8 m771;
    /* 0x772 */ u8 field_0x772;
    /* 0x773 */ u8 mbDidClearGame;
    /* 0x774 */ u8 mbGameEnd;
    /* 0x775 */ u8 field_0x775;
    /* 0x776 */ u8 field_0x776;
    /* 0x777 */ u8 field_0x777;
    /* 0x778 */ u8 m778;
    /* 0x779 */ u8 m779;
    /* 0x77A */ u8 mGameBoardScore;
    /* 0x77B */ u8 mEndGameTimer;
    /* 0x77C */ fpc_ProcID m77C;
    /* 0x780 */ int mWaitMode;
    /* 0x784 */ s16 m784;
    /* 0x786 */ s16 m786;
    /* 0x788 */ s16 m788;
    /* 0x78A */ u8 m78A[0x78C - 0x78A];
};


#endif /* D_A_NPC_KG1_H */
