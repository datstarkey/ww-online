#ifndef D_A_NPC_AUCTION_H
#define D_A_NPC_AUCTION_H

#include "f_op/f_op_actor.h"
#include "d/d_npc.h"
#include "SSystem/SComponent/c_phase.h"

struct sAnmDat {
    /* 0x00 */ u8 field_0x00;
    /* 0x01 */ u8 field_0x01;
    /* 0x02 */ u8 field_0x02;
}; // Size: 0x03

class daNpcAuction_c : public fopNpc_npc_c {
public:
    daNpcAuction_c();

    inline cPhs_State _create();
    inline bool _delete();

    inline bool _execute();
    void getMdlNo() {}
    void getPhaseP() {}
    f32 getPiconOfsY() { return mPiconOfsY; }
    void setAction(void (daNpcAuction_c::*)()) {}
    void setBmtNo(int) {}
    void setBtpNo(int) {}
    void setMdlNo(unsigned char) {}
    void setNpcNo(unsigned char) {}

    BOOL createHeap();
    cPhs_State createInit();
    void wait_action_init();
    void wait_action();
    void checkOrder();
    void eventOrder();
    void eventMove();
    void privateCut();
    void eventMainInit();
    bool eventMain();
    void eventMesSetInit(int);
    bool eventMesSet();
    s16 XyCheckCB(int);
    s16 XyEventCB(int);
    u16 next_msgStatus(u32*);
    u32 getMsg();
    void setAnmFromMsgTag();
    u8 getPrmNpcNo();
    void setMtx();
    void lookBack();
    BOOL initTexPatternAnm(bool);
    void playTexPatternAnm();
    void playAnm();
    void setAnm(unsigned char, int, float);
    void setAnm2(unsigned char, int, float);
    void setAnmTbl(sAnmDat*);
    BOOL isExecute();
    int getRand(int);
    void clrEmitter();

    static const char m_arcname[];

public:
    /* 0x6C4 */ void (daNpcAuction_c::*field_0x6c4)();
    /* 0x6D0 */ request_of_phase_process_class mPhs;
    /* 0x6D8 */ request_of_phase_process_class mCreatePhs;
    /* 0x6E0 */ J3DModel* field_0x6e0;
#if TARGET_PC
    /* 0x6E4 */ uintptr_t m_head_tex_pattern; // holds a pointer
#else
    /* 0x6E4 */ u32 m_head_tex_pattern;
#endif
    /* 0x6E8 */ mDoExt_btpAnm mBtpAnm;
    /* 0x6FC */ u32 field_0x6fc;
    /* 0x700 */ JPABaseEmitter* field_0x700;
    /* 0x704 */ u32 field_0x704;
    /* 0x708 */ u8 field_0x708[0x71C - 0x708];
    /* 0x71C */ sAnmDat* mpAnmDat;
    /* 0x720 */ f32 field_0x720;
    /* 0x724 */ f32 mPiconOfsY;
    /* 0x728 */ u32 field_0x728;
    /* 0x72C */ s32 field_0x72c;
    /* 0x730 */ s32 field_0x730;
    /* 0x734 */ s16 field_0x734;
    /* 0x736 */ s16 field_0x736;
    /* 0x738 */ s16 field_0x738;
    /* 0x73A */ s16 field_0x73a;
    /* 0x73C */ u8 m73C;
    /* 0x73D */ u8 m73D;
    /* 0x73E */ u8 field_0x73e;
    /* 0x73F */ u8 m73F;
    /* 0x740 */ u8 field_0x740;
    /* 0x741 */ u8 field_0x741;
    /* 0x742 */ u8 m742;
    /* 0x743 */ u8 field_0x743;
    /* 0x744 */ u8 m744;
    /* 0x745 */ u8 field_0x745;
    /* 0x746 */ u8 field_0x746;
    /* 0x747 */ u8 field_0x747;
    /* 0x748 */ u8 field_0x748;
    /* 0x749 */ u8 field_0x749;
    /* 0x74A */ s8 mAction;
};

#endif /* D_A_NPC_AUCTION_H */
