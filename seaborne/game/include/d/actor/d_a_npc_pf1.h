#ifndef D_A_NPC_PF1_H
#define D_A_NPC_PF1_H

#include "d/d_npc.h"

class J3DNode;

class daNpc_Pf1_c : public fopNpc_npc_c {
public:
    struct anm_prm_c {
        /* 0x00 */ s8 m00;
        /* 0x01 */ s8 field_0x1;
        /* 0x02 */ u8 field_0x2[0x04 - 0x02];
        /* 0x04 */ f32 m04;
        /* 0x08 */ f32 field_0x8;
        /* 0x0C */ s32 field_0xc;
    };

    void _nodeCB_Head(J3DNode*, J3DModel*);
    void _nodeCB_BackBone(J3DNode*, J3DModel*);
    bool init_PF1_0();
    bool createInit();
    void play_animation();
    void setMtx(bool);
    int bckResID(int);
    s32 btpResID(int);
    bool setBtp(signed char, bool);
    bool init_texPttrnAnm(signed char, bool);
    void play_btp_anm();
    void setAnm_anm(anm_prm_c*);
    void setAnm_NUM(int, int);
    void setAnm();
    void chngAnmTag();
    void ctrlAnmTag();
    void chngAnmAtr(unsigned char);
    void ctrlAnmAtr();
    void setAnm_ATR();
    void anmAtr(unsigned short);
    u16 next_msgStatus(u32* pMsgNo);
    u32 getMsg_PF1_0();
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
    BOOL set_action(int (daNpc_Pf1_c::*)(void*), void*);
    void setStt(s8);
    void createTama(float);
    bool chk_areaIN(float, cXyz);
    bool endEvent_check();
    bool startEvent_check();
    void set_pthPoint(unsigned char);
    bool chk_attn();
    void setBikon(cXyz);
    void delBikon();
    int wait_1();
    BOOL regret();
    BOOL attk_1();
    BOOL walk_1();
    BOOL wait_2();
    BOOL wait_3();
    BOOL talk_1();
    BOOL wait_action1(void*);
    bool demo();
    void shadowDraw();
    BOOL _draw();
    BOOL _execute();
    BOOL _delete();
    cPhs_State _create();
    BOOL bodyCreateHeap();
    BOOL CreateHeap();

public:
    /* 0x06C4 */ request_of_phase_process_class field_0x6c4;
    /* 0x06CC */ s8 m_hed_jnt_num;
    /* 0x06CD */ s8 m_bbone_jnt_num;
    /* 0x06CE */ char field_0x6ce[0x6D4 - 0x6CE];
    /* 0x06D4 */ u32 field_0x6d4;
    /* 0x06D8 */ mDoExt_btpAnm mBtpAnm;
    /* 0x06EC */ u8 field_0x6ec;
    /* 0x06ED */ u8 field_0x6ed[0x6EE - 0x6ED];
    /* 0x06EE */ s16 field_0x6ee;
    /* 0x06F0 */ int (daNpc_Pf1_c::*field_0x6f0)(void*);
    /* 0x06FC */ u8 field_0x6fc[0x0700 - 0x06FC];
    /* 0x0700 */ u32 field_0x700;
    /* 0x0704 */ dNpc_PathRun_c mPathRun;
    /* 0x070C */ cXyz field_0x70c;
    /* 0x0718 */ s16 field_0x718;
    /* 0x071A */ s16 field_0x71a;
    /* 0x071C */ s16 field_0x71c;
    /* 0x071E */ s16 field_0x71e;
    /* 0x0720 */ s16 field_0x720;
    /* 0x0722 */ s16 field_0x722;
    /* 0x0724 */ u8 field_0x724[0x72C - 0x724];
    /* 0x072C */ cXyz field_0x72c;
    /* 0x0738 */ cXyz field_0x738;
    /* 0x0744 */ u8 field_0x744[0x750 - 0x744];
    /* 0x0750 */ cXyz field_0x750;
    /* 0x075C */ u8 field_0x75c[0x768 - 0x75C];
    /* 0x0768 */ cXyz field_0x768;
    /* 0x0774 */ f32 field_0x774;
    /* 0x0778 */ u8 field_0x778[0x77C - 0x778];
    /* 0x077C */ s16 field_0x77c;
    /* 0x077E */ s16 field_0x77e;
    /* 0x0780 */ s16 field_0x780;
    /* 0x0782 */ u8 field_0x782[0x784 - 0x782];
    /* 0x0784 */ s32 field_0x784;
    /* 0x0788 */ s16 mEventIdx[1];
    /* 0x078A */ s16 field_0x78a;
    /* 0x078C */ u8 field_0x78c[0x796 - 0x78C];
    /* 0x0796 */ s16 field_0x796;
    /* 0x0798 */ s16 field_0x798;
    /* 0x079A */ u8 field_0x79a[0x79E - 0x79A];
    /* 0x079E */ s16 field_0x79e;
    /* 0x07A0 */ s8 field_0x7a0;
    /* 0x07A1 */ s8 field_0x7a1;
    /* 0x07A2 */ u8 field_0x7a2;
    /* 0x07A3 */ u8 field_0x7a3;
    /* 0x07A4 */ u8 field_0x7a4;
    /* 0x07A5 */ u8 field_0x7a5;
    /* 0x07A6 */ u8 field_0x7a6;
    /* 0x07A7 */ bool field_0x7a7;
    /* 0x07A8 */ u8 field_0x7a8;
    /* 0x07A9 */ u8 field_0x7a9;
    /* 0x07AA */ u8 field_0x7aa;
    /* 0x07AB */ u8 field_0x7ab;
    /* 0x07AC */ u8 field_0x7ac;
    /* 0x07AD */ u8 field_0x7ad;
    /* 0x07AE */ u8 field_0x7ae;
    /* 0x07AF */ bool field_0x7af;
    /* 0x07B0 */ bool field_0x7b0;
    /* 0x07B1 */ bool field_0x7b1;
    /* 0x07B2 */ s8 field_0x7b2;
    /* 0x07B3 */ u8 field_0x7b3;
    /* 0x07B4 */ u8 field_0x7b4;
    /* 0x07B5 */ s8 field_0x7b5;
    /* 0x07B6 */ s8 field_0x7b6;
    /* 0x07B7 */ s8 field_0x7b7;
    /* 0x07B8 */ s8 field_0x7b8;
    /* 0x07B9 */ s8 field_0x7b9;
    /* 0x07BA */ s8 field_0x7ba;
    /* 0x07BB */ s8 field_0x7bb;
    /* 0x07BC */ s8 field_0x7bc;
    /* 0x07BD */ s8 field_0x7bd;
    /* 0x07BE */ s8 field_0x7be;
    /* 0x07BF */ u8 field_0x7bf[0x7C0 - 0x7BF];
}; // Size: 0X7C0

STATIC_ASSERT(sizeof(daNpc_Pf1_c) == 0X7C0);


#endif /* D_A_NPC_PF1_H */
