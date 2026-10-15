#ifndef D_A_NPC_BMS1_H
#define D_A_NPC_BMS1_H

#include "f_op/f_op_actor.h"
#include "d/d_npc.h"
#include "d/d_shop.h"
#include "d/d_lib.h"

class daNpc_Bms1_c : public fopAc_ac_c {
public:
    typedef int (daNpc_Bms1_c::*ActionFunc)(void*);

    enum ActionStatus {
        ACTION_STARTING = 0,
        ACTION_ONGOING  = 1,
        ACTION_ENDING   = -1,
    };

    void getBackboneJntNum() {}
    void getBackbone_x() {}
    void getBackbone_y() {}
    void getHairLJntNum() {}
    void getHairRJntNum() {}
    void getHeadJntNum() {}
    void getHead_x() {}
    void getHead_y() {}
    void setAction(ActionFunc func, void* arg) {
        if (mCurrActionFunc != func) {
            if (mCurrActionFunc) {
                mActionStatus = ACTION_ENDING;
                (this->*mCurrActionFunc)(NULL);
            }
            mCurrActionFunc = func;
            mActionStatus = ACTION_STARTING;
            (this->*mCurrActionFunc)(arg);
        }
    }
    void setAttentionBasePos(cXyz i_pos) {
        field_0x7b4 = i_pos.x;
        field_0x7b8 = i_pos.y;
        field_0x7bc = i_pos.z;
    }
    void setEyePos(cXyz i_pos) {
        eyePos.x = i_pos.x;
        eyePos.y = i_pos.y;
        eyePos.z = i_pos.z;
    }

    void set_mtx();
    BOOL initTexPatternAnm(bool);
    void playTexPatternAnm();
    void setAnm(s8, f32);
    void setTexAnm(signed char);
    void setAnmFromMsgTag();
    BOOL chkAttention(cXyz, short);
    void eventOrder();
    void checkOrder();
#if TARGET_PC
    // u32 is unsigned int on the host, not unsigned long; same type as the original on MWCC.
    u16 next_msgStatus(u32*);
#else
    u16 next_msgStatus(unsigned long*);
#endif
    u32 getMsg();
    void setCollision();
    void talkInit();
    u16 normal_talk();
    u16 shop_talk();
    u16 talk();
    BOOL CreateInit();
    void setAttention(bool);
    BOOL checkPlayerLanding();
    void lookBack();
    bool wait01();
    bool talk01();
    BOOL getdemo_action(void*);
    int wait_action(void*);
    int event_action(void*);
    BOOL evn_talk_init(int);
    BOOL evn_continue_talk_init(int);
    BOOL evn_talk();
    BOOL evn_viblation_init(int);
    BOOL evn_head_swing_init(int);
    BOOL privateCut();
    BOOL demo_move();
    void demo_end_init();
    BOOL _draw();
    BOOL _execute();
    BOOL _delete();
    cPhs_State _create();
    BOOL CreateHeap();

    static char m_arcname[];

public:
    /* 0x0290 */ dNpc_HeadAnm_c mHeadAnm;
    /* 0x02B4 */ f32 field_0x2b4;
    /* 0x02B8 */ f32 field_0x2b8;
    /* 0x02BC */ f32 field_0x2bc;
    /* 0x02C0 */ f32 field_0x2c0;
    /* 0x02C4 */ f32 field_0x2c4;
    /* 0x02C8 */ f32 field_0x2c8;
    /* 0x02CC */ f32 field_0x2cc;
    /* 0x02D0 */ f32 field_0x2d0;
    /* 0x02D4 */ cXyz field_0x2d4;
    /* 0x02E0 */ cXyz field_0x2e0;
    /* 0x02EC */ cXyz field_0x2ec;
    /* 0x02F8 */ cXyz field_0x2f8;
    /* 0x0304 */ Quaternion field_0x304;
    /* 0x0314 */ Quaternion field_0x314;
    /* 0x0324 */ request_of_phase_process_class mPhs;
    /* 0x032C */ mDoExt_McaMorf* mpMorf;
    /* 0x0330 */ u32 mShadowId;
    /* 0x0334 */ J3DAnmTexPattern* m_head_tex_pattern;
    /* 0x0338 */ mDoExt_btpAnm mBtp;
    /* 0x034C */ u8 mBlinkFrame;
    /* 0x034D */ u8 field_0x34d[0x34E - 0x34D];
    /* 0x034E */ s16 field_0x34e;
    /* 0x0350 */ J3DModel* mpHeadModel;
    /* 0x0354 */ J3DModel* mpEyeModel;
    /* 0x0358 */ J3DModel* mpMaskModel;
    /* 0x035C */ J3DModel* mpLowModel;
    /* 0x0360 */ J3DModel* mpTaruModel;
    /* 0x0364 */ dBgS_ObjAcch mAcch;
    /* 0x0528 */ dBgS_AcchCir mAcchCir;
    /* 0x0568 */ dCcD_Stts mStts;
    /* 0x05A4 */ dCcD_Cyl mCyl;
    /* 0x06D4 */ s8 m_head_jnt_num;
    /* 0x06D5 */ s8 m_backbone_jnt_num;
    /* 0x06D6 */ s8 m_hairL_jnt_num;
    /* 0x06D7 */ s8 m_hairR_jnt_num;
    /* 0x06D8 */ s8 m_leg_jnt_num;
    /* 0x06D9 */ u8 field_0x6d9[0x6DC - 0x6D9];
    /* 0x06DC */ dNpc_JntCtrl_c mJntCtrl;
    /* 0x0710 */ dNpc_EventCut_c mEventCut;
    /* 0x077C */ STControl mStickControl;
    /* 0x07A4 */ s8 field_0x7a4;
    /* 0x07A5 */ u8 field_0x7a5[0x7B4 - 0x7A5];
    /* 0x07B4 */ f32 field_0x7b4;
    /* 0x07B8 */ f32 field_0x7b8;
    /* 0x07BC */ f32 field_0x7bc;
    /* 0x07C0 */ s16 field_0x7c0;
    /* 0x07C2 */ csXyz field_0x7c2;
    /* 0x07C8 */ u8 field_0x7c8[0x7CA - 0x7C8];
    /* 0x07CA */ u8 field_0x7ca;
    /* 0x07CB */ u8 field_0x7cb;
    /* 0x07CC */ u8 field_0x7cc;
    /* 0x07CD */ u8 field_0x7cd;
    /* 0x07CE */ u8 field_0x7ce[0x7D0 - 0x7CE];
    /* 0x07D0 */ f32 field_0x7d0;
    /* 0x07D4 */ f32 field_0x7d4;
    /* 0x07D8 */ u32 field_0x7d8;
    /* 0x07DC */ u32 field_0x7dc;
    /* 0x07E0 */ u32 field_0x7e0;
    /* 0x07E4 */ u32 field_0x7e4;
    /* 0x07E8 */ u8 field_0x7e8;
    /* 0x07E9 */ u8 field_0x7e9;
    /* 0x07EA */ u8 field_0x7ea[0x7EC - 0x7EA];
    /* 0x07EC */ ActionFunc mCurrActionFunc;
    /* 0x07F8 */ ShopCam_action_c mShopCam;
    /* 0x0850 */ ShopItems_c mShopItems;
    /* 0x0894 */ ShopCursor_c* mpShopCursor;
    /* 0x0898 */ s8 m898;
    /* 0x0899 */ s8 m899;
    /* 0x089A */ s8 m89A;
    /* 0x089B */ s8 field_0x89b;
    /* 0x089C */ s8 field_0x89c;
    /* 0x089D */ u8 field_0x89d;
    /* 0x089E */ s8 m89E;
    /* 0x089F */ u8 field_0x89f[0x8A0 - 0x89F];
    /* 0x08A0 */ s8 mActionStatus;
    /* 0x08A1 */ s8 field_0x8a1;
    /* 0x08A2 */ u8 field_0x8a2[0x8A3 - 0x8A2];
    /* 0x08A3 */ u8 field_0x8a3;
    /* 0x08A4 */ u8 field_0x8a4[0x8A5 - 0x8A4];
    /* 0x08A5 */ u8 field_0x8a5;
    /* 0x08A6 */ u8 field_0x8a6[0x8A8 - 0x8A6];
};

#endif /* D_A_NPC_BMS1_H */
