#ifndef D_A_MANT_H
#define D_A_MANT_H

#include "f_op/f_op_actor.h"
#include "d/d_cc_d.h"
#include "JSystem/J3DGraphBase/J3DPacket.h"

struct mant_j_s {
    /* 0x000 */ cXyz mPos[9];
    /* 0x06C */ cXyz mSpeed[9];
    /* 0x0D8 */ csXyz mAngle;
    /* 0x0DE */ u8 field_0xDE[0x110 - 0xDE];
};

class daMant_packet_c : public J3DPacket {
public:
    daMant_packet_c() { mCurArr = 0; }

    MtxP getMtx() { return mMtx; }
    MtxP getMtx2() { return mMtx2; }
    cXyz* getNrm() { return mNrm[mCurArr]; }
    cXyz* getPos() { return mPos[mCurArr]; }
    u8 getTexNo() { return mTexNo; }
    void setTevStr(dKy_tevstr_c* i_tevStr) { mpTevStr = i_tevStr; }
    void setTexNo(unsigned char i_texNo) { mTexNo = i_texNo; }
    void setarg0(unsigned char i_arg0) { mArg0 = i_arg0; }

    virtual void draw();
    virtual ~daMant_packet_c() {}

public:
    /* 0x010 */ Mtx mMtx;
    /* 0x040 */ Mtx mMtx2;
    /* 0x070 */ dKy_tevstr_c* mpTevStr;
    /* 0x074 */ u8 mCurArr;
    /* 0x078 */ cXyz mPos[2][81];
    /* 0x810 */ cXyz mNrm[2][81];
    /* 0xFA8 */ u8 mTexNo;
    /* 0xFA9 */ u8 mArg0;
};  // Size: 0xFAC

class mant_class : public fopAc_ac_c {
public:
    enum CapeType {
        /* 0x0 */ Type_DARKNUT_e = 0x0,
        /* 0x1 */ Type_PHANTOM_GANON_e = 0x1,
    };

public:
    /* 0x0290 */ u8 field_0x290[0x298 - 0x290];
    /* 0x0298 */ daMant_packet_c mPacket;
    /* 0x1244 */ u8 mType;
    /* 0x1248 */ u32 mCounter;
    /* 0x124C */ u8 field_0x124C[0x1250 - 0x124C];
    /* 0x1250 */ mant_j_s mJoint[9];
    /* 0x1BE0 */ cXyz m1BE0[2];
    /* 0x1BF8 */ f32 m1BF8;
    /* 0x1BFC */ f32 m1BFC;
    /* 0x1C00 */ f32 m1C00;
    /* 0x1C04 */ f32 m1C04;
    /* 0x1C08 */ f32 m1C08;
    /* 0x1C0C */ s16 m1C0C;
    /* 0x1C0E */ s16 m1C0E;
    /* 0x1C10 */ dCcD_Stts mStts;
    /* 0x1C4C */ dCcD_Sph mWindSph;
    /* 0x1D78 */ dCcD_Sph mMeshSph[9];
    /* 0x2804 */ Mtx mCullMtx;
    /* 0x2834 */ s8 m2834;
    /* 0x2836 */ s16 m2836;
    /* 0x2838 */ s16 m2838;
};  // Size: 0x283C

#endif /* D_A_MANT_H */
