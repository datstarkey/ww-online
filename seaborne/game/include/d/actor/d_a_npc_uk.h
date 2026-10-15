#ifndef D_A_NPC_UK_H
#define D_A_NPC_UK_H

#include "f_op/f_op_actor.h"
#include "d/d_npc.h"
#include "d/actor/d_a_npc_mk_static.h"
#include "m_Do/m_Do_ext.h"
#include "d/d_bg_s_acch.h"

class J3DModel;
class J3DAnmTexPattern;
class mDoExt_McaMorf;

class daNpc_Uk_c : public fopAc_ac_c {
public:
    void ChkOrder(unsigned char) {}
    void ClrOrder(unsigned char) {}
    void SetOrder(unsigned char) {}
    void chkFlag(unsigned short) {}
    void clrFlag(unsigned short) {}
    s8 getBackboneJntNum() { return m_jnt.getBackboneJntNum(); }
    s16 getBackbone_x() { return m_jnt.getBackbone_x(); }
    s16 getBackbone_y() { return m_jnt.getBackbone_y(); }
    s8 getHeadJntNum() { return m_jnt.getHeadJntNum(); }
    s16 getHead_x() { return m_jnt.getHead_x(); }
    s16 getHead_y() { return m_jnt.getHead_y(); }
    void incAttnSetCount() {
        if (field_0x67f != 0xff) {
            field_0x67f++;
        }
    }
    void setAction(int (daNpc_Uk_c::*)(void*), void*) {}
    void setAttentionBasePos(cXyz i_pos) {
        field_0x670 = i_pos.x;
        field_0x674 = i_pos.y;
        field_0x678 = i_pos.z;
    }
    void setEyePos(cXyz i_pos) {
        field_0x664 = i_pos.x;
        field_0x668 = i_pos.y;
        field_0x66c = i_pos.z;
    }
    void setFlag(unsigned short) {}

    int getType();
    u32 getPath();
    int getShapeType();
    u16 getCaughtFlag();
    u16 getFoundFlag();
    u16 getFirstTalk();
    BOOL chkGameStart();
    BOOL chkPositioning(float, float, float, short, short);
    unsigned char nextVisitMode();
    void approachRun(fopAc_ac_c*);
    void aroundWalk(fopAc_ac_c*);
    void surrender();
    void runawayInit();
    BOOL initTexPatternAnm(bool);
    void playTexPatternAnm();
    void setAnm(signed char, unsigned char);
    bool chkAttentionLocal();
    void chkAttention();
    void setAnmStatus();
    void eventOrder();
    void checkOrder();
    u16 next_msgStatus(u32*);
    u32 getMsg();
    void setCollision();
    void msgAnm(unsigned char);
    void talkInit();
    u16 talk();
    BOOL init();
    void setAttention(bool);
    u8 getLookBackMode();
    void lookBack();
    char* getStaffName();
    BOOL checkDemoStart();
    int getNowEventAction();
    void visitInit(unsigned char);
    void visitProc();
    bool wait01();
    bool talk01();
    bool talk02();
    unsigned char visitTalkInit();
    void visitSetEvent();
    bool visit01();
    bool warningB();
    bool warningC();
    bool warningD();
    bool found();
    bool runaway();
    bool jump();
    bool jitanda01();
    bool jitanda02();
    bool demo01();
    bool demo02();
    void demoInitCom();
    bool demoProc();
    BOOL wait_action(void*);
    BOOL seek_action(void*);
    BOOL hind_action(void*);
    BOOL visit_action(void*);
    BOOL _draw();
    BOOL _execute();
    BOOL _delete();
    cPhs_State _create();
    BOOL CreateHeap();

public:
    /* 0x0290 */ request_of_phase_process_class mPhs;
    /* 0x0298 */ mDoExt_McaMorf* mpMorf;
    /* 0x029C */ J3DModel* mpHeadModel;
    /* 0x02A0 */ u32 mShadowKey;
    /* 0x02A4 */ J3DAnmTexPattern* m_maba_tex_pattern;
    /* 0x02A8 */ mDoExt_btpAnm mBtpAnm;
    /* 0x02BC */ u8 field_0x2bc;
    /* 0x02BD */ u8 field_0x2bd[0x2BE - 0x2BD];
    /* 0x02BE */ s16 field_0x2be;
    /* 0x02C0 */ dBgS_ObjAcch mObjAcch;
    /* 0x0484 */ dBgS_AcchCir mAcchCir;
    /* 0x04C4 */ dCcD_Stts mStts;
    /* 0x0500 */ dCcD_Cyl mCyl;
    /* 0x0630 */ dNpc_JntCtrl_c m_jnt;
    /* 0x0664 */ f32 field_0x664;
    /* 0x0668 */ f32 field_0x668;
    /* 0x066C */ f32 field_0x66c;
    /* 0x0670 */ f32 field_0x670;
    /* 0x0674 */ f32 field_0x674;
    /* 0x0678 */ f32 field_0x678;
    /* 0x067C */ s16 field_0x67c;
    /* 0x067E */ s8 field_0x67e;
    /* 0x067F */ u8 field_0x67f;
    /* 0x0680 */ f32 field_0x680;
    /* 0x0684 */ u32 field_0x684;
    /* 0x0688 */ u16 field_0x688;
    /* 0x068A */ u8 field_0x68a;
    /* 0x068B */ u8 field_0x68b[0x68C - 0x68B];
    /* 0x068C */ u32 field_0x68c;
    /* 0x0690 */ f32 field_0x690;
    /* 0x0694 */ f32 field_0x694;
    /* 0x0698 */ dNpc_PathRun_c mPathRun;
#if TARGET_PC
    // d_a_npc_uk.cpp reaches these 12 bytes through its overlay as a member-function pointer,
    // which is 16 bytes on the host.
    /* 0x06A0 */ BOOL (daNpc_Uk_c::*mCurrActionFunc)(void*);
#else
    /* 0x06A0 */ u32 field_0x6a0;
    /* 0x06A4 */ u32 field_0x6a4;
    /* 0x06A8 */ u32 field_0x6a8;
#endif
    /* 0x06AC */ s8 field_0x6ac;
    /* 0x06AD */ s8 field_0x6ad;
    /* 0x06AE */ u8 field_0x6ae[0x6B0 - 0x6AE];
    /* 0x06B0 */ s32 field_0x6b0;
    /* 0x06B4 */ s32 field_0x6b4;
    /* 0x06B8 */ u8 field_0x6b8;
    /* 0x06B9 */ u8 field_0x6b9;
    /* 0x06BA */ u8 field_0x6ba;
    /* 0x06BB */ u8 field_0x6bb;
    /* 0x06BC */ u8 field_0x6bc;
    /* 0x06BD */ u8 field_0x6bd;
    /* 0x06BE */ u8 field_0x6be;
    /* 0x06BF */ u8 field_0x6bf;
    /* 0x06C0 */ s8 field_0x6c0;
    /* 0x06C1 */ s8 field_0x6c1;
    /* 0x06C2 */ u8 field_0x6c2;
    /* 0x06C3 */ u8 field_0x6c3[0x6C4 - 0x6C3];
    /* 0x06C4 */ u32 field_0x6c4;
    /* 0x06C8 */ s8 field_0x6c8;
    /* 0x06C9 */ u8 field_0x6c9[0x6CC - 0x6C9];
    /* 0x06CC */ cXyz field_0x6cc;
    /* 0x06D8 */ cXyz field_0x6d8;
    /* 0x06E4 */ s16 field_0x6e4;
    /* 0x06E6 */ s16 field_0x6e6;
    /* 0x06E8 */ s16 field_0x6e8;
    /* 0x06EA */ u8 field_0x6ea[0x6EC - 0x6EA];
    /* 0x06EC */ u32 field_0x6ec;
    /* 0x06F0 */ daNpc_Mk_Static_c field_0x6f0;
}; // Size: 0X700

STATIC_ASSERT(sizeof(daNpc_Uk_c) == 0X700);

#endif /* D_A_NPC_UK_H */
