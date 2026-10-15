#ifndef D_A_NPC_AJ1_H
#define D_A_NPC_AJ1_H

#include "d/d_npc.h"
#include "m_Do/m_Do_hostIO.h"
#include "d/d_particle.h"

class J3DNode;
class JPABaseEmitter;

class daNpc_Aj1_c : public fopNpc_npc_c {
public:
    struct anm_prm_c {
        s8 field0;
        s8 field1;
        f32 field4;
        f32 field8;
        s32 fieldC;
    };

    void _nodeCB_Head(J3DNode*, J3DModel*);
    void _nodeCB_BackBone(J3DNode*, J3DModel*);
    bool _XyCheckCB(int);
    bool init_AJ1_0();
    bool init_AJ1_1();
    bool init_AJ1_2();
    bool createInit();
    void play_animation();
    void ctrl_WAITanm();
    void ctrl_TIREanm();
    void setMtx(bool);
    s32 bckResID(int);
    s32 btpResID(int);
    bool init_texPttrnAnm(signed char, bool);
    void play_texPttrnAnm();
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
    u32 getMsg_AJ1_0();
    u32 getMsg_AJ1_1();
    u32 getMsg_AJ1_2();
    u32 getMsg();
    void eventOrder();
    void checkOrder();
    u8 chk_talk();
    u8 chk_parts_notMov();
    void lookBack();
    bool chkAttention();
    void setAttention(bool);
    bool decideType(int);
    void cut_init_AJ1_TLK();
    bool cut_move_AJ1_TLK();
    void cut_init_INI_ANGRY();
    bool cut_move_INI_ANGRY();
    void cut_init_VIVRATE();
    bool cut_move_VIVRATE();
    void cut_init_JMP();
    bool cut_move_JMP();
    void cut_init_SPPRISE();
    bool cut_move_SPPRISE();
    void cut_init_LOK();
    bool cut_move_LOK();
    void cut_init_DAN();
    bool cut_move_DAN();
    void cut_init_INVIT();
    bool cut_move_INVIT();
    void privateCut(int);
    void endEvent();
    s32 isEventEntry();
    void event_proc(int);
    void set_pa_pun();
    void set_pa_aka();
    void set_pa_don();
    void set_pa_smk();
    void flw_pa_pun();
    void flw_pa_aka();
    void del_pa_aka();
    void del_pa(JPABaseEmitter**);
    void setSmoke();
    BOOL set_action(int (daNpc_Aj1_c::*)(void*), void*);
    void setStt(s8);
    bool chk_areaIN(float, short, cXyz);
    int FARwai();
    BOOL call_1();
    BOOL wait_1();
    int talk_1();
    BOOL wait_action1(void*);
    BOOL wait_action2(void*);
    bool demo();
    void shadowDraw();
    BOOL _draw();
    BOOL _execute();
    BOOL _delete();
    cPhs_State _create();
    BOOL bodyCreateHeap();
    BOOL itemCreateHeap();
    BOOL CreateHeap();

public:
    /* 0x06C4 */ u8 field_0x6c4[0x6CC - 0x6C4];
    /* 0x06CC */ s8 m_hed_jnt_num;
    /* 0x06CD */ s8 m_bbone_jnt_num;
    /* 0x06CE */ s8 m_hnd_L_jnt_num;
    /* 0x06CF */ s8 m_fot_L_jnt_num;
    /* 0x06D0 */ J3DModel* field_0x6d0;
    /* 0x06D4 */ u32 field_0x6d4;
    /* 0x06D8 */ mDoExt_btpAnm mBtpAnm;
    /* 0x06EC */ u8 field_0x6ec;
    /* 0x06ED */ u8 field_0x6ed[0x6EE - 0x6ED];
    /* 0x06EE */ s16 field_0x6ee;
    /* 0x06F0 */ int (daNpc_Aj1_c::*mpAction)(void*);
    /* 0x06FC */ f32 field_0x6fc;
    /* 0x0700 */ f32 field_0x700;
    /* 0x0704 */ f32 field_0x704;
    /* 0x0708 */ u16 field_0x708;
    /* 0x070A */ s16 field_0x70a;
    /* 0x070C */ u16 field_0x70c;
    /* 0x070E */ s16 field_0x70e;
    /* 0x0710 */ s16 field_0x710;
    /* 0x0712 */ s16 field_0x712;
    /* 0x0714 */ cXyz field_0x714;
    /* 0x0720 */ cXyz field_0x720;
    /* 0x072C */ cXyz field_0x72c;
    /* 0x0738 */ s16 field_0x738;
    /* 0x073A */ s16 field_0x73a;
    /* 0x073C */ s16 field_0x73c;
    /* 0x073E */ u8 field_0x73e[0x740 - 0x73E];
    /* 0x0740 */ f32 field_0x740;
    /* 0x0744 */ s16 mEventIdx[1];
    /* 0x0746 */ s16 field_0x746;
    /* 0x0748 */ s16 field_0x748;
    /* 0x074A */ s16 field_0x74a;
    /* 0x074C */ s16 field_0x74c;
    /* 0x074E */ u16 field_0x74e;
    /* 0x0750 */ s16 field_0x750;
    /* 0x0752 */ u8 field_0x752[0x754 - 0x752];
    /* 0x0754 */ s16 field_0x754;
    /* 0x0756 */ s16 field_0x756;
    /* 0x0758 */ s8 field_0x758;
    /* 0x0759 */ u8 field_0x759;
    /* 0x075A */ u8 field_0x75a;
    /* 0x075B */ u8 field_0x75b;
    /* 0x075C */ u8 field_0x75c;
    /* 0x075D */ u8 field_0x75d[0x75E - 0x75D];
    /* 0x075E */ u8 field_0x75e;
    /* 0x075F */ u8 field_0x75f;
    /* 0x0760 */ u8 field_0x760;
    /* 0x0761 */ u8 field_0x761;
    /* 0x0762 */ u8 field_0x762[0x764 - 0x762];
    /* 0x0764 */ s32 field_0x764;
    /* 0x0768 */ u8 field_0x768;
    /* 0x0769 */ u8 field_0x769;
    /* 0x076A */ bool field_0x76a;
    /* 0x076B */ bool field_0x76b;
    /* 0x076C */ dPa_smokeEcallBack mSmokeCB;
    /* 0x078C */ JPABaseEmitter* field_0x78c;
    /* 0x0790 */ JPABaseEmitter* field_0x790;
    /* 0x0794 */ JPABaseEmitter* field_0x794;
    /* 0x0798 */ JPABaseEmitter* field_0x798;
    /* 0x079C */ cXyz field_0x79c;
    /* 0x07A8 */ cXyz field_0x7a8;
    /* 0x07B4 */ u8 field_0x7b4;
    /* 0x07B5 */ s8 field_0x7b5;
    /* 0x07B6 */ u8 field_0x7b6;
    /* 0x07B7 */ u8 field_0x7b7;
    /* 0x07B8 */ s8 field_0x7b8;
    /* 0x07B9 */ s8 field_0x7b9;
    /* 0x07BA */ s8 field_0x7ba;
    /* 0x07BB */ s8 field_0x7bb;
    /* 0x07BC */ s8 field_0x7bc;
    /* 0x07BD */ u8 field_0x7bd;
    /* 0x07BE */ s8 field_0x7be;
    /* 0x07BF */ s8 field_0x7bf;
    /* 0x07C0 */ s8 field_0x7c0;
    /* 0x07C1 */ s8 field_0x7c1;
    /* 0x07C2 */ u8 field_0x7c2[0x7C4 - 0x7C2];
}; // Size: 0X7C4

STATIC_ASSERT(sizeof(daNpc_Aj1_c) == 0X7C4);

#endif /* D_A_NPC_AJ1_H */
