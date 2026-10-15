#ifndef D_A_GY_H
#define D_A_GY_H

#include "f_op/f_op_actor.h"
#include "d/d_cc_d.h"
#include "d/d_bg_s_lin_chk.h"
#include "d/d_bg_s_acch.h"
#include "d/d_particle.h"
#include "c/c_damagereaction.h"

class daGy_Ctrl_c;

class daGy_c : public fopEn_enemy_c {
public:
    daGy_c();
    void _nodeControl(J3DNode*, J3DModel*);
    BOOL _createHeap();
    void setMtx();
    void setAnm();
    void setAtCollision();
    void setCollision();
    void setAimSpeedF();
    void modeDiveInit();
    void modeDive();
    void modeCircleInit();
    void modeCircle();
    void modeWithCircleInit();
    void modeWithCircle();
    void modeAttackInit();
    void modeWithAttackInit();
    void modeWithAttack();
    void modeAttack();
    void modeAttackPlayerInit();
    void modeAttackPlayer();
    void modeAttackBackInit();
    void modeAttackBack();
    void modeDamageInit();
    void modeDamage();
    void modeDeleteInit();
    void modeDelete();
    void modeDeleteBombInit();
    void modeDeleteBomb();
    void modeProcCall();
    void createWave();
    void setWave();
    void lineCheck(cXyz*, cXyz*);
    void checkTgHit();
    f32 getWaterY();
    bool _execute();
    void drawDebug();
    bool _draw();
    void createInit();
    cPhs_State _create();
    bool _delete();

    static const char m_arc_name[];
    static const u32 m_heapsize;

public:
    /* 0x02AC */ s32 field_0x2ac;
    /* 0x02B0 */ s32 m2B0;
    /* 0x02B4 */ s32 m2B4;
    /* 0x02B8 */ daGy_Ctrl_c* field_0x2b8;
    /* 0x02BC */ cXyz m2BC;
    /* 0x02C8 */ request_of_phase_process_class mPhs;
    /* 0x02D0 */ u8 field_0x2d0[0x2D4 - 0x2D0];
    /* 0x02D4 */ mDoExt_McaMorf* mpMorf;
    /* 0x02D8 */ mDoExt_invisibleModel m2D8;
    /* 0x02E0 */ dBgS_ObjAcch mAcch;
    /* 0x04A4 */ dBgS_AcchCir mAcchCir;
    /* 0x04E4 */ f32 field_0x4e4;
    /* 0x04E8 */ f32 field_0x4e8;
    /* 0x04EC */ f32 field_0x4ec;
    /* 0x04F0 */ f32 field_0x4f0;
    /* 0x04F4 */ u8 field_0x4f4[0x4F8 - 0x4F4];
    /* 0x04F8 */ s32 field_0x4f8;
    /* 0x04FC */ int m4FC;
    /* 0x0500 */ int m500;
    /* 0x0504 */ u8 field_0x504;
    /* 0x0505 */ u8 field_0x505[0x508 - 0x505];
    /* 0x0508 */ f32 field_0x508;
    /* 0x050C */ dCcD_Sph m50C;
    /* 0x0638 */ dCcD_Sph m638;
    /* 0x0764 */ dCcD_Cps m764;
    /* 0x089C */ cXyz m89C;
    /* 0x08A8 */ dCcD_Stts m8A8;
    /* 0x08E4 */ u32 m8E4;
    /* 0x08E8 */ u8 field_0x8e8[0x8EC - 0x8E8];
    /* 0x08EC */ int field_0x8ec;
    /* 0x08F0 */ int field_0x8f0;
    /* 0x08F4 */ s16 m8F4;
    /* 0x08F6 */ u8 field_0x8f6[0x8F8 - 0x8F6];
    /* 0x08F8 */ f32 field_0x8f8;
    /* 0x08FC */ f32 field_0x8fc;
    /* 0x0900 */ f32 field_0x900;
    /* 0x0904 */ cXyz m904;
    /* 0x0910 */ u8 field_0x910[0x914 - 0x910];
    /* 0x0914 */ int field_0x914;
    /* 0x0918 */ f32 field_0x918;
    /* 0x091C */ f32 field_0x91c;
    /* 0x0920 */ f32 m920;
    /* 0x0924 */ s32 field_0x924;
    /* 0x0928 */ s32 field_0x928;
    /* 0x092C */ enemyice mEnemyIce;
    /* 0x0CE4 */ JntHit_c* mpJntHit;
    /* 0x0CE8 */ u8 mCE8;
    /* 0x0CE9 */ u8 field_0xce9[0xCEC - 0xCE9];
    /* 0x0CEC */ cXyz mCEC;
    /* 0x0CF8 */ Quaternion field_0xcf8;
    /* 0x0D08 */ cXyz mD08;
    /* 0x0D14 */ s8 field_0xd14;
    /* 0x0D15 */ s8 mD15;
    /* 0x0D16 */ s8 field_0xd16;
    /* 0x0D17 */ u8 field_0xd17[0xD18 - 0xD17];
    /* 0x0D18 */ dPa_waveEcallBack mD18;
    /* 0x0D7C */ dPa_waveEcallBack mD7C;
    /* 0x0DE0 */ dPa_splashEcallBack mDE0;
    /* 0x0DFC */ f32 field_0xdfc;
    /* 0x0E00 */ f32 field_0xe00;
    /* 0x0E04 */ f32 field_0xe04;
    /* 0x0E08 */ u8 field_0xe08[0xE0A - 0xE08];
    /* 0x0E0A */ u16 field_0xe0a;
    /* 0x0E0C */ u8 field_0xe0c[0xE10 - 0xE0C];
    /* 0x0E10 */ f32 field_0xe10;
    /* 0x0E14 */ dBgS_ObjLinChk mE14;
    /* 0x0E80 */ u8 field_0xe80;
    /* 0x0E81 */ u8 field_0xe81[0xE84 - 0xE81];
    /* 0x0E84 */ cXyz field_0xe84;
};


#endif /* D_A_GY_H */
