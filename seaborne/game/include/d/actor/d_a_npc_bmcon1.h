#ifndef D_A_NPC_BMCON1_H
#define D_A_NPC_BMCON1_H

#include "f_op/f_op_actor.h"
#include "d/d_cc_d.h"
#include "d/d_npc.h"

class J3DNode;

struct sBmconAnmDat {
    /* 0x0 */ u8 mAnmIdx;
    /* 0x1 */ u8 mFrame;
    /* 0x2 */ s8 mMode;
};

class daNpcBmcon_c : public fopNpc_npc_c {
public:
    enum Prm_e {
        PRM_RAILID_W = 0x08,
        PRM_RAILID_S = 0x10,
    };

    daNpcBmcon_c();

    void getNpcNo() {}
    void getPhaseP() {}
    void get_arm_L_jnt_num() {}
    void get_arm_R_jnt_num() {}
    void get_nec_jnt_num() {}
    void setResFlag(unsigned char) {}

    void nodeArmControl(J3DNode*, J3DModel*);
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
    u8 executeWalkInit();
    void executeWalk();
    u8 executeTurnInit();
    void executeTurn();
    void checkOrder();
    void eventOrder();
    void eventMove();
    void privateCut();
    void eventMesSetInit(int);
    bool eventMesSet();
    void eventGetItemInit();
    u16 talk2(int);
    virtual u16 next_msgStatus(u32* pMsgNo);
    virtual u32 getMsg();
    void chkMsg();
#if TARGET_PC
    // u32 is unsigned int on the host, not unsigned long; same type as the original on MWCC.
    void setMessage(u32);
#else
    void setMessage(unsigned long);
#endif
    void setAnmFromMsgTag();
    u8 getPrmNpcNo();
    u8 getPrmRailID();
    void setMtx();
    void chkAttention();
    void lookBack();
    void playAnm();
    void setAnm(u8, int, float);
    u8 setAnmTbl(sBmconAnmDat*);
    s16 XyCheckCB(int);
    void setCollision(dCcD_Cyl*, cXyz, float, float);
    cXyz calcFlyDist();
    s16 getFlyDistMax();
    void setFlyDistMax(short);
    s16 getFlyDistNow();
    void setFlyDistNow(short);
    BOOL chkEndEvent();
    BOOL isClear();
    BOOL initTexPatternAnm(bool i_modify);

    static const char m_arcname[];

public:
    /* 0x6C4 */ Mtx mLeftArmMtx;
    /* 0x6F4 */ Mtx mRightArmMtx;
    /* 0x724 */ request_of_phase_process_class mPhase2;
    /* 0x72C */ request_of_phase_process_class mPhase;
    /* 0x734 */ mDoExt_McaMorf* mpArmMorf;
    /* 0x738 */ J3DModel* mpHeadModel;
    /* 0x73C */ J3DAnmTexPattern* m_head_tex_pattern;
    /* 0x740 */ mDoExt_btpAnm mBtpAnm;
    /* 0x754 */ u32 mShadowId;
    /* 0x758 */ dNpc_PathRun_c mPathRun;
    /* 0x760 */ u8 field_0x760[0x76C - 0x760];
    /* 0x76C */ cXyz field_0x76c;
    /* 0x778 */ sBmconAnmDat* mpAnmDat;
    /* 0x77C */ u32* mpMsgNo;
    /* 0x780 */ f32 field_0x780;
    /* 0x784 */ f32 field_0x784;
    /* 0x788 */ f32 field_0x788;
    /* 0x78C */ u32 field_0x78c;
    /* 0x790 */ bool field_0x790;
    /* 0x791 */ u8 field_0x791[0x792 - 0x791];
    /* 0x792 */ s16 field_0x792;
    /* 0x794 */ s16 field_0x794;
    /* 0x796 */ u16 field_0x796;
    /* 0x798 */ s16 field_0x798;
    /* 0x79A */ s16 field_0x79a;
    /* 0x79C */ s16 field_0x79c;
    /* 0x79E */ u16 field_0x79e;
    /* 0x7A0 */ s16 field_0x7a0;
    /* 0x7A2 */ s16 field_0x7a2;
    /* 0x7A4 */ u8 field_0x7a4[0x7A6 - 0x7A4];
    /* 0x7A6 */ s16 field_0x7a6;
    /* 0x7A8 */ s16 field_0x7a8;
    /* 0x7AA */ s16 field_0x7aa;
    /* 0x7AC */ u8 field_0x7ac;
    /* 0x7AD */ u8 field_0x7ad;
    /* 0x7AE */ u8 field_0x7ae;
    /* 0x7AF */ u8 field_0x7af[0x7B0 - 0x7AF];
    /* 0x7B0 */ s8 m_nec_jnt_num;
    /* 0x7B1 */ s8 m_arm_L_jnt_num;
    /* 0x7B2 */ s8 m_arm_R_jnt_num;
    /* 0x7B3 */ s8 m_armL1_jnt_num;
    /* 0x7B4 */ s8 m_armR1_jnt_num;
    /* 0x7B5 */ u8 field_0x7b5;
    /* 0x7B6 */ u8 field_0x7b6;
    /* 0x7B7 */ u8 mNpcNo;
    /* 0x7B8 */ u8 field_0x7b8;
    /* 0x7B9 */ u8 mAnmIdx;
    /* 0x7BA */ u8 field_0x7ba;
    /* 0x7BB */ s8 field_0x7bb;
    /* 0x7BC */ s8 field_0x7bc;
    /* 0x7BD */ u8 field_0x7bd;
    /* 0x7BE */ s8 field_0x7be;
    /* 0x7BF */ u8 field_0x7bf;
    /* 0x7C0 */ u8 field_0x7c0;
    /* 0x7C1 */ u8 field_0x7c1;
    /* 0x7C2 */ u8 field_0x7c2;
    /* 0x7C3 */ u8 field_0x7c3;
    /* 0x7C4 */ u8 field_0x7c4;
    /* 0x7C5 */ u8 field_0x7c5;
    /* 0x7C6 */ u8 field_0x7c6[0x7C8 - 0x7C6];
};

#endif /* D_A_NPC_BMCON1_H */
