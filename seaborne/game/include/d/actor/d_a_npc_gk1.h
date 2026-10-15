#ifndef D_A_NPC_GK1_H
#define D_A_NPC_GK1_H

#include "d/d_npc.h"
#include "m_Do/m_Do_hostIO.h"

class J3DNode;

class daNpc_Gk1_c : public fopNpc_npc_c {
public:
    struct anm_prm_c {
        /* 0x00 */ s8 mBckIdx;
        /* 0x01 */ s8 mBtpIdx;
        /* 0x02 */ u8 field_0x2[0x4 - 0x2];
        /* 0x04 */ f32 mMorf;
        /* 0x08 */ f32 mSpeed;
        /* 0x0C */ int mMode;
    };

    void _nodeCB_Head(J3DNode*, J3DModel*);
    void _nodeCB_Neck(J3DNode*, J3DModel*);
    void _nodeCB_BackBone(J3DNode*, J3DModel*);
    bool init_GK1_0();
    bool createInit();
    void play_animation();
    void setMtx(bool);
    int bckResID(int);
    int btpResID(int);
    bool setBtp(signed char, bool);
    bool init_texPttrnAnm(signed char, bool);
    void play_btp_anm();
    void setAnm_anm(anm_prm_c*);
    void setAnm();
    void chngAnmTag();
    void ctrlAnmTag();
    void chngAnmAtr(unsigned char);
    void ctrlAnmAtr();
    void setAnm_ATR();
    void anmAtr(unsigned short);
    u16 next_msgStatus(u32* pMsgNo);
    u32 getMsg_GK1_0();
    u32 getMsg();
    void eventOrder();
    void checkOrder();
    bool chk_talk();
    bool chk_parts_notMov();
    fopAc_ac_c* searchByID(fpc_ProcID, int*);
    void lookBack();
    bool chkAttention();
    void setAttention(bool);
    bool decideType(int);
    void privateCut(int);
    void endEvent();
    int isEventEntry();
    void event_proc(int);
    BOOL set_action(int (daNpc_Gk1_c::*)(void*), void*);
    void setStt(s8);
    bool chk_attn();
    BOOL wait_1();
    BOOL talk_1();
    int wait_action1(void*);
    u8 demo();
    void shadowDraw();
    BOOL _draw();
    BOOL _execute();
    BOOL _delete();
    cPhs_State _create();
    BOOL bodyCreateHeap();
    BOOL itemCreateHeap();
    BOOL hat_CreateHeap();
    BOOL CreateHeap();

public:
    /* 0x06C4 */ request_of_phase_process_class mPhs;
    /* 0x06CC */ s8 m_hed_jnt_num;
    /* 0x06CD */ s8 m_bbone_jnt_num;
    /* 0x06CE */ s8 m_nck_jnt_num;
    /* 0x06CF */ u8 field_0x6cf[0x6D0 - 0x6CF];
#if TARGET_PC
    /* 0x06D0 */ uintptr_t field_0x6d0; // holds a pointer
#else
    /* 0x06D0 */ u32 field_0x6d0;
#endif
#if TARGET_PC
    /* 0x06D4 */ uintptr_t field_0x6d4; // holds a pointer
#else
    /* 0x06D4 */ u32 field_0x6d4;
#endif
    /* 0x06D8 */ char mArcName[4];
    /* 0x06DC */ u32 field_0x6dc;
    /* 0x06E0 */ mDoExt_btpAnm mBtpAnm;
    /* 0x06F4 */ u8 field_0x6f4;
    /* 0x06F5 */ u8 field_0x6f5[0x6F6 - 0x6F5];
    /* 0x06F6 */ s16 field_0x6f6;
    /* 0x06F8 */ int (daNpc_Gk1_c::*mCurrProcFunc)(void*);
    /* 0x0704 */ u8 field_0x704[0x708 - 0x704];
    /* 0x0708 */ fpc_ProcID field_0x708;
    /* 0x070C */ u8 field_0x70c[0x714 - 0x70C];
    /* 0x0714 */ f32 field_0x714;
    /* 0x0718 */ f32 field_0x718;
    /* 0x071C */ f32 field_0x71c;
    /* 0x0720 */ u16 field_0x720;
    /* 0x0722 */ s16 field_0x722;
    /* 0x0724 */ u16 field_0x724;
    /* 0x0726 */ s16 field_0x726;
    /* 0x0728 */ s16 field_0x728;
    /* 0x072A */ s16 field_0x72a;
    /* 0x072C */ u8 field_0x72c[0x734 - 0x72C];
    /* 0x0734 */ f32 field_0x734;
    /* 0x0738 */ f32 field_0x738;
    /* 0x073C */ f32 field_0x73c;
    /* 0x0740 */ cXyz field_0x740;
    /* 0x074C */ u8 field_0x74c[0x758 - 0x74C];
    /* 0x0758 */ cXyz field_0x758;
    /* 0x0764 */ u8 field_0x764[0x770 - 0x764];
    /* 0x0770 */ f32 field_0x770;
    /* 0x0774 */ u8 field_0x774[0x778 - 0x774];
    /* 0x0778 */ s16 field_0x778;
    /* 0x077A */ s16 field_0x77a;
    /* 0x077C */ s16 field_0x77c;
    /* 0x077E */ u8 field_0x77e[0x780 - 0x77E];
    /* 0x0780 */ s32 field_0x780;
    /* 0x0784 */ s16 mEventIdx[1];
    /* 0x0786 */ s16 field_0x786;
    /* 0x0788 */ u8 field_0x788[0x790 - 0x788];
    /* 0x0790 */ u16 field_0x790;
    /* 0x0792 */ u8 field_0x792[0x79A - 0x792];
    /* 0x079A */ s16 field_0x79a;
    /* 0x079C */ u8 field_0x79c;
    /* 0x079D */ u8 field_0x79d;
    /* 0x079E */ u8 field_0x79e;
    /* 0x079F */ bool field_0x79f;
    /* 0x07A0 */ u8 field_0x7a0;
    /* 0x07A1 */ u8 field_0x7a1;
    /* 0x07A2 */ u8 field_0x7a2;
    /* 0x07A3 */ u8 field_0x7a3;
    /* 0x07A4 */ u8 field_0x7a4;
    /* 0x07A5 */ u8 field_0x7a5;
    /* 0x07A6 */ bool field_0x7a6;
    /* 0x07A7 */ u8 field_0x7a7;
    /* 0x07A8 */ u8 field_0x7a8;
    /* 0x07A9 */ s8 field_0x7a9;
    /* 0x07AA */ u8 field_0x7aa;
    /* 0x07AB */ u8 field_0x7ab;
    /* 0x07AC */ s8 field_0x7ac;
    /* 0x07AD */ s8 field_0x7ad;
    /* 0x07AE */ s8 field_0x7ae;
    /* 0x07AF */ s8 field_0x7af;
    /* 0x07B0 */ s8 field_0x7b0;
    /* 0x07B1 */ s8 field_0x7b1;
    /* 0x07B2 */ s8 field_0x7b2;
    /* 0x07B3 */ s8 field_0x7b3;
    /* 0x07B4 */ s8 field_0x7b4;
    /* 0x07B5 */ s8 field_0x7b5;
    /* 0x07B6 */ u8 field_0x7b6[0x7B8 - 0x7B6];
}; // Size: 0X7B8

STATIC_ASSERT(sizeof(daNpc_Gk1_c) == 0X7B8);


#endif /* D_A_NPC_GK1_H */
