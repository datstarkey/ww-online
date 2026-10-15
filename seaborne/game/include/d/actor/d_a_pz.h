#ifndef D_A_PZ_H
#define D_A_PZ_H

#include "res/Object/Pz.h"
#include "f_op/f_op_actor.h"
#include "d/d_npc.h"
#include "d/d_particle.h"
#include "c/c_damagereaction.h"
#include "SSystem/SComponent/c_phase.h"
#include "SSystem/SComponent/c_lib.h"
#include "d/d_bg_s_lin_chk.h"
#include "d/d_lib.h"
#include "m_Do/m_Do_ext.h"

class daPz_matAnm_c : public J3DMaterialAnm {
public:
    void clrMoveFlag() { field_0x7C = false; }
    f32* getNowOffsetX() { return &mOffset.x; }
    f32* getNowOffsetY() { return &mOffset.y; }
    void setMoveFlag() { field_0x7C = true; }

    daPz_matAnm_c();
    virtual ~daPz_matAnm_c() {}
    void calc(J3DMaterial*) const;

public:
    /* 0x6C */ cXy mOffset;
    /* 0x74 */ cXy field_0x74;
    /* 0x7C */ bool field_0x7C;
};

class daPz_c : public fopNpc_npc_c {
public:
    enum Proc_e {
        PROC_INIT_e = 0,
        PROC_EXEC_e = 1,
    };

    MtxP getRightHandMatrix() { return mpMorf->getModel()->getAnmMtx(PZ_JNT_HAND_R1_e); }
    void isAnm(s8) {}
    void isEyeAnm(s8) {}
    void modeProcInit(int) {}
    void setAfraid() { m073F = 1; }
    u8 calcTgHitTimer() { return cLib_calcTimer(&mTgHitTimer); }
    void setDown() { m08B0 = 1; }
    void setMove() { m0740 = 1; }
    void setStand() { m08B0 = 2; }

    void _nodeHeadControl(J3DNode*, J3DModel*);
    void _nodeWaistControl(J3DNode*, J3DModel*);
    void _nodeWaist2Control(J3DNode*, J3DModel*);
    void _nodeSkirtControl(J3DNode*, J3DModel*);
    BOOL bodyCreateHeap();
    BOOL bowCreateHeap();
    BOOL _createHeap();
    void getGndPos();
    bool checkEyeArea(cXyz&);
    virtual u32 getMsg();
    virtual u16 next_msgStatus(u32*);
    virtual void anmAtr(u16);
    void eventOrder();
    void checkOrder();
    void setFallSplash();
    void setHeadSplash();
    void setRipple();
    void setJntStatus();
    u8 demo();
    bool checkTgHit();
    void getArg();
    void setAttention();
    void setBowAnm(s8, bool);
    void setBowString(bool);
    void setAnm(s8, bool, int);
    void setAnmRunSpeed();
    void setEyeBtp(int);
    void setEyeBtk(int);
    void setEyeAnm(s8);
    void ctrlEye();
    void playEyeAnm();
    void setMtx();
    void modeWaitInit();
    void modeWait();
    void modeMoveInit();
    void modeMove();
    void modeAttackWaitInit();
    void modeAttackWait();
    void modeAttackInit();
    void modeAttack();
    void modeDefendInit();
    void modeDefend();
    void modeDownInit();
    void modeDown();
    void modeAfraidInit();
    void modeAfraid();
    void modeSideStepInit();
    void modeSideStep();
    void modeBackStepInit();
    void modeBackStep();
    void modeTalkInit();
    void modeTalk();
    void modeFollowInit();
    void modeFollow();
    void modeProc(Proc_e, int);
    bool _execute();
    void bowDraw();
    void bodyDraw();
    void drawShadow();
    bool _draw();
    void bodyCreateInit();
    void createInit();
    cPhs_State _create();
    bool _delete();

    static const u32 m_heapsize;
    static const char m_arc_name[];
    static const f32 m_smoke_ef;
    static const f32 m_grass_ef;
    static const f32 m_splash_ef;
    static const dCcD_SrcCyl m_cyl_src;

public:
    /* 0x06C4 */ int mMode;
    /* 0x06C8 */ int field_0x6c8;
    /* 0x06CC */ int field_0x6cc;
    /* 0x06D0 */ u8 mTgHitTimer;
    /* 0x06D1 */ u8 mHitType;
    /* 0x06D2 */ s8 m06D2;
    /* 0x06D3 */ s8 m06D3;
    /* 0x06D4 */ s8 m06D4;
    /* 0x06D5 */ s8 mCurEye;
    /* 0x06D8 */ int field_0x6d8;
    /* 0x06DC */ bool field_0x6dc;
    /* 0x06DD */ s8 m06DD;
    /* 0x06DE */ s8 field_0x6de;
    /* 0x06DF */ s8 field_0x6df;
    /* 0x06E0 */ dPa_rippleEcallBack mRippleCb;
    /* 0x06F4 */ dPa_followEcallBack mFallSplashCb;
    /* 0x0708 */ dPa_followEcallBack mHeadSplashCb;
    /* 0x071C */ cXyz mFallSplashPos;
    /* 0x0728 */ int mFallRippleTimer;
    /* 0x072C */ cXyz field_0x72c;
    /* 0x0738 */ u8 field_0x738[0x73E - 0x738];
    /* 0x073E */ u8 mbEyesFollowGanondorf;
    /* 0x073F */ u8 m073F;
    /* 0x0740 */ u8 m0740;
    /* 0x0744 */ int m0744;
    /* 0x0748 */ cXyz mHitPos;
    /* 0x0754 */ int m0754;
    /* 0x0758 */ int m0758;
    /* 0x075C */ u8 m075C;
    /* 0x0760 */ int m0760;
    /* 0x0764 */ int m0764;
    /* 0x0768 */ u8 m0768;
    /* 0x076C */ request_of_phase_process_class mPhs;
    /* 0x0774 */ u32 m0774;
    /* 0x0778 */ mDoExt_invisibleModel mInvisibleModel;
    /* 0x0780 */ dKy_tevstr_c mTevstr;
    /* 0x0830 */ cXyz m0830;
    /* 0x083C */ dBgS_ObjLinChk mLinChk;
    /* 0x08A8 */ daPz_matAnm_c* m08A8[2];
    /* 0x08B0 */ int m08B0;
    /* 0x08B4 */ fpc_ProcID m08B4;
    /* 0x08B8 */ cXyz field_0x8b8;
    /* 0x08C4 */ cXyz m08C4;
    /* 0x08D0 */ cXyz mEyeTargetPos;
    /* 0x08DC */ cXyz mEyePos;
    /* 0x08E8 */ s16 mTurnSpeed;
    /* 0x08EA */ bool m08EA;
    /* 0x08EC */ int mTimer1;
    /* 0x08F0 */ int mTimer2;
    /* 0x08F4 */ int mTimer3;
    /* 0x08F8 */ int m08F8;
    /* 0x08FC */ dLib_circle_path_c mCirclePath;
    /* 0x0920 */ int m0920;
    /* 0x0924 */ f32 m0924;
    /* 0x0928 */ enemyice mEventIce;
    /* 0x0CE0 */ enemyfire mEnemyFire;
    /* 0x0F08 */ Mtx mWaistMtx;
    /* 0x0F38 */ cXyz mWaistPos;
    /* 0x0F44 */ f32 m0F44;
    /* 0x0F48 */ u8 m0F48;
    /* 0x0F4C */ cXyz mGanondorfPosCurrent;
    /* 0x0F58 */ cXyz mGanondorfPos4;
    /* 0x0F64 */ u8 mbHasGanondorf;
    /* 0x0F65 */ u8 m0F65;
    /* 0x0F66 */ u8 mbDemo;
    /* 0x0F67 */ u8 mBtpFrame;
    /* 0x0F68 */ f32 m0F68;
    /* 0x0F6C */ s16 m0F6C;
    /* 0x0F70 */ f32 m0F70;
    /* 0x0F74 */ u8 field_0xf74[0xF78 - 0xF74];
    /* 0x0F78 */ s16 field_0xf78;
    /* 0x0F7A */ s16 field_0xf7a;
    /* 0x0F7C */ int m0F7C;
    /* 0x0F80 */ u8 m0F80;
    /* 0x0F81 */ u8 m0F81;
    /* 0x0F82 */ u8 m0F82;
    /* 0x0F84 */ int mArg;
    /* 0x0F88 */ u8 m0F88;
    /* 0x0F8C */ mDoExt_McaMorf* mpBowMcaMorf;
    /* 0x0F90 */ mDoExt_brkAnm mBrkAnm;
    /* 0x0FA8 */ mDoExt_btkAnm mBtkAnm;
    /* 0x0FBC */ mDoExt_btpAnm mBtpAnm;
    /* 0x0FD0 */ J3DJoint* field_0xfd0;
    /* 0x0FD4 */ J3DMaterial* field_0xfd4[6];
    /* 0x0FEC */ J3DMaterial* field_0xfec[6];
    /* 0x1004 */ J3DMaterial* field_0x1004[4];
    /* 0x1014 */ J3DShape* field_0x1014[6];
    /* 0x102C */ J3DShape* field_0x102c[6];
    /* 0x1044 */ J3DShape* field_0x1044[4];
    /* 0x1054 */ mDoExt_offCupOnAupPacket mOffCupOnAupPacket1;
    /* 0x1064 */ mDoExt_offCupOnAupPacket mOffCupOnAupPacket2;
    /* 0x1074 */ mDoExt_onCupOffAupPacket mOnCupOffAupPacket1;
    /* 0x1084 */ mDoExt_onCupOffAupPacket mOnCupOffAupPacket2;
};


#endif /* D_A_PZ_H */
