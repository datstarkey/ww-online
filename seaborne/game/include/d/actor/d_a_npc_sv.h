#ifndef D_A_NPC_SV_H
#define D_A_NPC_SV_H

#include "d/d_npc.h"
#include "d/d_cc_d.h"

struct sSvAnmDat {
    /* 0x0 */ u8 field_0x0;
    /* 0x1 */ u8 field_0x1;
    /* 0x2 */ u8 field_0x2;
}; // Size: 0x3

class daNpcSv_c : public fopNpc_npc_c {
public:
    void getMsgPtn() {}
    void getNpcNo() {}
    void getPhaseP() {}
    void setResFlag(unsigned char) {}

    daNpcSv_c();
    cPhs_State _create();
    BOOL createHeap();
    cPhs_State createInit();
    bool _delete();
    bool _draw();
    bool _execute();
    u8 executeCommon();
    void executeSetMode(unsigned char);
    u8 executeWaitInit();
    void executeWait();
    u8 executeTalkInit();
    void executeTalk();
    void checkOrder();
    void eventOrder();
    void eventMove();
    void privateCut();
    void eventMesSetInit(int);
    bool eventMesSet();
    void eventGetItemInit(int);
    bool eventGetItem();
    void eventSetAngleInit();
    void eventAttentionInit(int);
    bool eventAttention();
    void eventTurnOkInit();
    u16 talk2(int, fopAc_ac_c*);
    u16 next_msgStatus(u32* pMsgNo);
    u32 getMsg();
    void setMessage(u32);
    void setAnmFromMsgTag();
    u8 getPrmNpcNo();
    void setMtx();
    void chkAttention();
    void lookBack();
    void playAnm();
    void setAnm(u8, int, f32);
    u8 setAnmTbl(sSvAnmDat*);
    void setCollision(dCcD_Cyl*, cXyz, float, float);
    u8 getTalkNo();
    BOOL isTalkOK();

    static const char m_arcname[];

public:
    /* 0x06C4 */ request_of_phase_process_class mPhase2;
    /* 0x06CC */ request_of_phase_process_class mPhase;
    /* 0x06D4 */ u32 field_0x6d4;
    /* 0x06D8 */ u32 field_0x6d8;
    /* 0x06DC */ u8 field_0x6dc[0x6EC - 0x6DC];
    /* 0x06EC */ cXyz field_0x6ec;
    /* 0x06F8 */ u8 field_0x6f8[0x704 - 0x6F8];
    /* 0x0704 */ sSvAnmDat* mpAnmDat;
    /* 0x0708 */ u32* mpMsgNo;
    /* 0x070C */ f32 field_0x70c;
    /* 0x0710 */ f32 field_0x710;
    /* 0x0714 */ u32 field_0x714;
    /* 0x0718 */ s32 field_0x718;
    /* 0x071C */ bool field_0x71c;
    /* 0x071D */ u8 field_0x71d;
    /* 0x071E */ s16 field_0x71e[3];
    /* 0x0724 */ s16 field_0x724;
    /* 0x0726 */ s16 field_0x726;
    /* 0x0728 */ s16 field_0x728;
    /* 0x072A */ s16 field_0x72a;
    /* 0x072C */ s16 field_0x72c;
    /* 0x072E */ s16 field_0x72e;
    /* 0x0730 */ s16 field_0x730;
    /* 0x0732 */ s16 field_0x732;
    /* 0x0734 */ u16 field_0x734;
    /* 0x0736 */ u8 field_0x736;
    /* 0x0737 */ u8 field_0x737;
    /* 0x0738 */ u8 field_0x738;
    /* 0x0739 */ u8 field_0x739;
    /* 0x073A */ u8 field_0x73a;
    /* 0x073B */ u8 field_0x73b;
    /* 0x073C */ u8 field_0x73c;
    /* 0x073D */ u8 field_0x73d;
    /* 0x073E */ u8 field_0x73e;
    /* 0x073F */ s8 field_0x73f;
    /* 0x0740 */ s8 field_0x740;
    /* 0x0741 */ s8 field_0x741;
    /* 0x0742 */ u8 field_0x742;
    /* 0x0743 */ u8 field_0x743;
    /* 0x0744 */ u8 field_0x744;
    /* 0x0745 */ u8 field_0x745;
    /* 0x0746 */ u8 field_0x746;
    /* 0x0747 */ u8 field_0x747;
    /* 0x0748 */ u8 field_0x748;
    /* 0x0749 */ u8 field_0x749;
    /* 0x074A */ u8 field_0x74a;
    /* 0x074B */ u8 field_0x74b[0x74C - 0x74B];
}; // Size: 0X74C

STATIC_ASSERT(sizeof(daNpcSv_c) == 0X74C);

#endif /* D_A_NPC_SV_H */
