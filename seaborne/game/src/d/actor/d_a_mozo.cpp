/**
 * d_a_mozo.cpp
 * Enemy - Moblin Statue / モ石像 (Mo Sekizou)
 */

#include "weak_bss_936_to_1036.h" // IWYU pragma: keep
#include "d/actor/d_a_mozo.h"
#include "res/Object/Mozo.h"
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_player.h"
#include "JSystem/JUtility/JUTAssert.h"
#include "d/d_com_inf_game.h"
#include "d/d_kankyo.h"
#include "d/d_lib.h"
#include "m_Do/m_Do_mtx.h"
#include "m_Do/m_Do_ext.h"
#include "m_Do/m_Do_hostIO.h"
#include "d/d_cc_d.h"
#include "d/actor/d_a_beam.h"
#include "d/d_a_obj.h"
#include "d/d_particle.h"
#include "m_Do/m_Do_audio.h"

class daMozo_childHIO_c {
public:
    virtual ~daMozo_childHIO_c() {}

    /* 0x04 */ f32 m04;
    /* 0x08 */ f32 m08;
    /* 0x0C */ s16 m0C;
    /* 0x0E */ s16 m0E;
    /* 0x10 */ u8 m10;
    /* 0x11 */ u8 m11[0x14 - 0x11];
};  // Size: 0x14

class daMozo_BeamChildHIO_c : public daMozo_childHIO_c {
public:
    virtual ~daMozo_BeamChildHIO_c() {}
};

class daMozo_FireChildHIO_c : public daMozo_childHIO_c {
public:
    virtual ~daMozo_FireChildHIO_c() {}
};

class daMozo_HIO_c : public JORReflexible {
public:
    daMozo_HIO_c();
    virtual ~daMozo_HIO_c() {}

    void genMessage(JORMContext* ctx) { UNUSED(ctx); }

public:
    /* 0x04 */ s8 mNo;
    /* 0x08 */ int m08;
    /* 0x0C */ daMozo_BeamChildHIO_c* mpBeamChild;
    /* 0x10 */ daMozo_FireChildHIO_c* mpFireChild;
    /* 0x14 */ daMozo_BeamChildHIO_c mBeamChild;
    /* 0x28 */ daMozo_FireChildHIO_c mFireChild;
    /* 0x3C */ cXyz mFireOffset;
};  // Size: 0x48

static dCcD_SrcCps cps_src = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ AT_TYPE_FIRE,
        /* SrcObjAt  Atp     */ 1,
        /* SrcObjAt  SPrm    */ cCcD_AtSPrm_Set_e | cCcD_AtSPrm_VsPlayer_e | cCcD_AtSPrm_VsOther_e,
        /* SrcObjTg  Type    */ AT_TYPE_ALL,
        /* SrcObjTg  SPrm    */ 0,
        /* SrcObjCo  SPrm    */ 0,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_None_e,
        /* SrcGObjAt Spl     */ dCcG_At_Spl_UNK8,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ 0,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ dCcG_Tg_Spl_UNK0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoHitMark_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCpsS
    {{
        /* Start  */ {0.0f, 0.0f, 0.0f},
        /* End    */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 50.0f,
    }},
};


static daMozo_HIO_c l_HIO;

u8 daMozo_c::m_event_flag;

/* 000000EC-000001D0       .text __ct__12daMozo_HIO_cFv */
daMozo_HIO_c::daMozo_HIO_c() {
    /* Nonmatching */
    mFireOffset.x = 0.0f;
    mFireOffset.y = -300.0f;
    mFireOffset.z = 600.0f;
    mNo = -1;
    m08 = -1;
    mpBeamChild = &mBeamChild;
    mpFireChild = &mFireChild;
    mpBeamChild->m04 = 1000.0f;
    mpBeamChild->m08 = 1200.0f;
    mpBeamChild->m0C = 0x2000;
    mpBeamChild->m0E = 0x2800;
    mpBeamChild->m10 = 0;
    mpFireChild->m04 = 1000.0f;
    mpFireChild->m08 = 1200.0f;
    mpFireChild->m0C = 0x2000;
    mpFireChild->m0E = 0x2800;
    mpFireChild->m10 = 0;
}

/* 000002D0-00000568       .text daMozo_nodeCallBackBeam__FP8daMozo_cP8J3DModelP7J3DNodei */
static BOOL daMozo_nodeCallBackBeam(daMozo_c* i_this, J3DModel* model, J3DNode* node, int calcTiming) {
    J3DJoint* joint = (J3DJoint*)node;
    int jntNo = joint->getJntNo();

    if (calcTiming == J3DNodeCBCalcTiming_In) {
        static cXyz a_beam_start(60.0f, -20.0f, 0.0f);
        static cXyz a_beam_end(1250.0f, -250.0f, 0.0f);
        static cXyz a_beamL_start(60.0f, -20.0f, 12.5f);
        static cXyz a_beamR_start(60.0f, -20.0f, -12.5f);

        MTXCopy(model->getAnmMtx(jntNo), mDoMtx_stack_c::get());
        mDoMtx_stack_c::multVec(&a_beam_start, &i_this->m30C);
        mDoMtx_stack_c::multVec(&a_beam_end, &i_this->m318);

        Mtx mtx;
        MTXCopy(mDoMtx_stack_c::get(), mtx);
        cXyz pos(mtx[0][3], mtx[1][3], mtx[2][3]);
        mtx[0][3] = mtx[1][3] = mtx[2][3] = 0.0f;

        mDoMtx_stack_c::transS(pos.x, pos.y, pos.z);
        mDoMtx_stack_c::quatM(&i_this->mQuatRotation);
        mDoMtx_stack_c::concat(mtx);
        mDoMtx_stack_c::multVec(&a_beam_start, &i_this->m2DC);
        mDoMtx_stack_c::multVec(&a_beam_end, &i_this->m2E8);
        mDoMtx_stack_c::multVec(&a_beamL_start, &i_this->mBeamPos);
        mDoMtx_stack_c::multVec(&a_beamR_start, &i_this->mFirePos);

        model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
        MTXCopy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    }

    return TRUE;
}

/* 00000568-00000728       .text daMozo_nodeCallBackFire__FP8daMozo_cP8J3DModelP7J3DNodei */
static BOOL daMozo_nodeCallBackFire(daMozo_c* i_this, J3DModel* model, J3DNode* node, int calcTiming) {
    J3DJoint* joint = (J3DJoint*)node;
    int jntNo = joint->getJntNo();

    if (calcTiming == J3DNodeCBCalcTiming_In) {
        static cXyz a_fire_start(0.0f, 0.0f, 0.0f);
        static cXyz a_fire_end(5000.0f, 0.0f, 0.0f);

        mDoMtx_stack_c::copy(model->getAnmMtx(jntNo));
        mDoMtx_stack_c::multVec(&a_fire_start, &i_this->m30C);
        mDoMtx_stack_c::multVec(&a_fire_end, &i_this->m318);

        Mtx mtx;
        MTXCopy(mDoMtx_stack_c::get(), mtx);
        cXyz pos(mtx[0][3], mtx[1][3], mtx[2][3]);
        mtx[0][3] = mtx[1][3] = mtx[2][3] = 0.0f;

        mDoMtx_stack_c::transS(pos.x, pos.y, pos.z);
        mDoMtx_stack_c::quatM(&i_this->mQuatRotation);
        mDoMtx_stack_c::concat(mtx);
        mDoMtx_stack_c::multVec(&a_fire_start, &i_this->m330);
        mDoMtx_stack_c::multVec(&a_fire_end, &i_this->m33C);

        model->setAnmMtx(jntNo, mDoMtx_stack_c::get());
        MTXCopy(mDoMtx_stack_c::get(), J3DSys::mCurrentMtx);
    }

    return TRUE;
}

/* 00000728-0000078C       .text daMozo_nodeCallBack__FP7J3DNodei */
static BOOL daMozo_nodeCallBack(J3DNode* node, int calcTiming) {
    /* Nonmatching */
    J3DJoint* joint = (J3DJoint*)node;
    s32 jntNo = joint->getJntNo();
    J3DModel* model = j3dSys.getModel();
    daMozo_c* i_this = (daMozo_c*)model->getUserArea();

    switch (i_this->field_0x376) {
    case 0: return daMozo_nodeCallBackBeam(i_this, model, node, calcTiming);
    case 1: return daMozo_nodeCallBackFire(i_this, model, node, calcTiming);
    }

    return TRUE;
}

/* 0000078C-000007AC       .text CheckCreateHeap__FP10fopAc_ac_c */
static BOOL CheckCreateHeap(fopAc_ac_c* i_this) {
    return static_cast<daMozo_c*>(i_this)->CreateHeap();
}

/* 000007AC-00000A24       .text CreateHeap__8daMozo_cFv */
BOOL daMozo_c::CreateHeap() {
    J3DModelData* mdlData = (J3DModelData*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BDL_MOZ_e);
    
    mDoExt_McaMorf* newMorf =  new mDoExt_McaMorf(
        mdlData,
        0,
        0,
        static_cast<J3DAnmTransformKey*>(dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BCK_MOZ_e)),
        J3DFrameCtrl::EMode_LOOP,
        1.0f,
        0,
        -1,
        0,
        0,
        0,
        0x11020203
    );
    
    mAnimMorf = newMorf;

    m_brk = (J3DAnmTevRegKey*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BRK_MOZ_e);
    JUT_ASSERT(0x16A, m_brk != NULL);

    m_btk = (J3DAnmTextureSRTKey*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BTK_MOZ_e);
    JUT_ASSERT(0x16D, m_btk != NULL);

    int brkInitResult = mBrkAnm.init(mdlData, m_brk, true, J3DFrameCtrl::EMode_NONE);
    int btkInitResult = mBtkAnm.init(mdlData, m_btk, true, J3DFrameCtrl::EMode_NONE);

    return mdlData != 0 && mAnimMorf != 0 && mAnimMorf->getModel() != 0 && brkInitResult != 0 && btkInitResult != 0;
}

/* 00000A24-00000AAC       .text set_mtx__8daMozo_cFv */
void daMozo_c::set_mtx() {
    J3DModel* mdl = mAnimMorf->getModel();
    mdl->setBaseScale(scale);

    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::YrotM(current.angle.y);
        
    mdl->setBaseTRMtx(mDoMtx_stack_c::get());
}

/* 00000AAC-00000C38       .text anime_proc__8daMozo_cFv */
void daMozo_c::anime_proc() {
    mAnimMorf->play(NULL, 0, 0);
    mBrkAnm.play();
    mBtkAnm.play();

    if (mAnimMorf->getFrame() < 24.0f) {
        fopAcM_seStart(this, JA_SE_OBJ_MO_STATUE_MOVE, 0);
    }

    if (mAnimMorf->checkFrame(35.0f)) {
        if (mAnimMorf->getPlaySpeed() > 0.0f) {
            fopAcM_seStart(this, JA_SE_OBJ_MO_STATUE_OPEN, 0);
        } else {
            mDoAud_seStopActor(&eyePos, JA_SE_OBJ_MO_STATUE_OPEN);
            fopAcM_seStart(this, JA_SE_OBJ_MO_STATUE_CLOSE, 0);
        }
    }
}

/* 00000C38-00000C90       .text wait_proc_init__8daMozo_cFv */
void daMozo_c::wait_proc_init() {
    setAnm(0, 0.0f);
    setProcess(&daMozo_c::wait_proc);
}

/* 00000C90-00000D58       .text wait_proc__8daMozo_cFv */
void daMozo_c::wait_proc() {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);

    if (checkRange(0) && !player->checkPlayerFly()) {
        switch (field_0x376) {
        case 0:
            search_beam_proc_init();
            break;
        case 1:
            search_fire_proc_init();
            break;
        case 2:
            search_beam_proc_init();
            break;
        }

        if (m_event_flag == 0) {
            field_0x377 = 1;
        }
    }
}

/* 00000D58-00000DE0       .text search_beam_proc_init__8daMozo_cFv */
void daMozo_c::search_beam_proc_init() {
    setAnm(1, 0.0f);
    setProcess(&daMozo_c::search_beam_proc);
    mBrkAnm.setFrame(0.0f);
    mBtkAnm.setFrame(0.0f);
    mBrkAnm.setPlaySpeed(1.0f);
    mBtkAnm.setPlaySpeed(1.0f);
}

/* 00000DE0-00001230       .text search_beam_proc__8daMozo_cFv */
void daMozo_c::search_beam_proc() {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);

    if (mAnimMorf->getFrame() > 30.0f) {
        cXyz now_vec = player->current.pos - m30C;
        cXyz vec = m318 - m30C;
        Quaternion rot;
        daObj::quat_rotVec(&rot, vec, now_vec);
        C_QUATSlerp(&mQuatRotation, &rot, &mQuatRotation, 0.2f);
    }

    anime_proc();

    if (mAnimMorf->getFrame() > mAnimMorf->getEndFrame() - 1.0f) {
        daBeam_c* beamL = (daBeam_c*)getBeamActor(mBeamActorID);
        daBeam_c* beamR = (daBeam_c*)getBeamActor(mFireActorID);

        if (beamL != NULL && beamR != NULL) {
            cXyz diff = m2E8 - m2DC;
            csXyz angle = csXyz::Zero;
            angle.y = cM_atan2s(diff.x, diff.z);
            angle.x = cM_atan2s(-diff.y, diff.absXZ());

            if (!beamL->beamCheck()) {
                beamL->beamOn();
            }
            if (beamL->m5F4 == 0) {
                beamL->m5A8 = 0.0f;
                if (beamL->m588 < 5.0f) {
                    beamL->m588 += 1.0f;
                } else {
                    beamL->m5A8 = 0.0f;
                    beamL->m588 = 5.0f;
                    beamL->m5F4 = 1;
                }
            } else {
                beamL->m5A8 = 0.0f;
                beamL->m588 = 5.0f;
            }
            beamL->setPosAngle(mBeamPos, angle);
            beamL->m694 = 15.0f;

            if (!beamR->beamCheck()) {
                beamR->beamOn();
            }
            if (beamR->m5F4 == 0) {
                beamR->m5A8 = 0.0f;
                if (beamR->m588 < 5.0f) {
                    beamR->m588 += 1.0f;
                } else {
                    beamR->m5A8 = 0.0f;
                    beamR->m588 = 5.0f;
                    beamR->m5F4 = 1;
                }
            } else {
                beamR->m5A8 = 0.0f;
                beamR->m588 = 5.0f;
            }
            beamR->setPosAngle(mFirePos, angle);
            beamR->m694 = 15.0f;
        }
    }

    if (!checkRange(1)) {
        towait_proc_init();
    }
}

/* 00001230-000012C0       .text search_fire_proc_init__8daMozo_cFv */
void daMozo_c::search_fire_proc_init() {
    setAnm(1, 0.0f);
    setProcess(&daMozo_c::search_fire_proc);
    mBrkAnm.setFrame(0.0f);
    mBtkAnm.setFrame(0.0f);
    mBrkAnm.setPlaySpeed(1.0f);
    mBtkAnm.setPlaySpeed(1.0f);
    field_0x370 = 0;
}

/* 000012C0-000017F4       .text search_fire_proc__8daMozo_cFv */
void daMozo_c::search_fire_proc() {
    if (mAnimMorf->getFrame() > 30.0f || mAnmIdx == 4 || mAnmIdx == 3) {
        cXyz now_vec;
        mDoMtx_stack_c::YrotS(current.angle.y);
        mDoMtx_stack_c::multVec(&l_HIO.mFireOffset, &now_vec);
        PSVECAdd(&now_vec, &current.pos, &now_vec);
        PSVECSubtract(&now_vec, &m30C, &now_vec);
        cXyz vec = m318 - m30C;
        Quaternion rot;
        daObj::quat_rotVec(&rot, vec, now_vec);
        C_QUATSlerp(&mQuatRotation, &rot, &mQuatRotation, 0.2f);
    }

    anime_proc();

    if (mAnmIdx == 1 || mAnmIdx == 3) {
        if (mAnimMorf->getFrame() > mAnimMorf->getEndFrame() - 50.0f) {
            if (mpEmitter1 == NULL) {
                mpEmitter1 = dComIfGp_particle_set(dPa_name::ID_AK_SN_MOZFIRE00, &current.pos);
            }
            if (mpEmitter2 == NULL) {
                mpEmitter2 = dComIfGp_particle_setToon(dPa_name::ID_AK_SN_MOZFIRE01, &current.pos);
            }

            MTXCopy(mAnimMorf->getModel()->getAnmMtx(MOZ_JNT_ATAMA_J_e), mDoMtx_stack_c::get());
            mDoMtx_stack_c::XYZrotM(0x640, 0x4000, 0);
            mDoMtx_stack_c::transM(0.0f, 50.0f, 52.0f);
            if (mpEmitter1 != NULL) {
                mpEmitter1->setGlobalRTMatrix(mDoMtx_stack_c::get());
            }
            if (mpEmitter2 != NULL) {
                mpEmitter2->setGlobalRTMatrix(mDoMtx_stack_c::get());
            }

            cXyz dir = m33C - m330;
            if (!dir.normalizeRS()) {
                dir = cXyz::Zero;
            }
            cXyz end = dir;
            f32 len = 45.0f * field_0x370;
            f32 radius = 3.0f * field_0x370;
            len = len > 600.0f ? 600.0f : len;
            radius = radius > 80.0f ? 80.0f : radius;
            PSVECScale(&end, &end, len);
            PSVECAdd(&end, &m330, &end);

            mCps.SetStartEnd(m330, end);
            mCps.SetR(radius);
            mCps.SetAtVec(dir);
            dComIfG_Ccsp()->Set(&mCps);

            mSePos = end;
            mDoAud_seStart(JA_SE_OBJ_MO_STATUE_FIRE, &mSePos, 0, 0);

            if (field_0x370++ > 60) {
                setAnm(4, 0.0f);
                if (mpEmitter1 != NULL) {
                    mpEmitter1->setRate(0.0f);
                    mpEmitter1->becomeInvalidEmitter();
                    mpEmitter1 = NULL;
                }
                if (mpEmitter2 != NULL) {
                    mpEmitter2->setRate(0.0f);
                    mpEmitter2->becomeInvalidEmitter();
                    mpEmitter2 = NULL;
                }
                towait_proc_init();
            }
        }
    } else if (mAnmIdx == 4) {
        if (mAnimMorf->getFrame() < 30.0f) {
            setAnm(3, 8.0f);
            field_0x370 = 0;
        }
    }
}

/* 000017F4-00001874       .text towait_proc_init__8daMozo_cFv */
void daMozo_c::towait_proc_init() {
    setAnm(2, 0.0f);
    mAnimMorf->setPlaySpeed(-0.5f);
    setProcess(&daMozo_c::towait_proc);
    mBrkAnm.setPlaySpeed(-1.0f);
    mBtkAnm.setPlaySpeed(-1.0f);
}

/* 00001874-00001B3C       .text towait_proc__8daMozo_cFv */
static inline BOOL beam_off(daBeam_c* beam) {
    if (beam->beamCheck()) {
        beam->beamOff();
    }
    if (beam->m5F4 == 1) {
        if (beam->m588 < 5.0f) {
            beam->m588 += 1.0f;
        }
        if (beam->m5A8 < 4.0f) {
            beam->m5A8 += 1.0f;
            return FALSE;
        }
        beam->m5A8 = 0.0f;
        beam->m588 = 0.0f;
        beam->m5F4 = 0;
    } else {
        beam->m588 = 0.0f;
        beam->m5A8 = 0.0f;
    }
    return TRUE;
}

void daMozo_c::towait_proc() {
    daBeam_c* beamL = (daBeam_c*)getBeamActor(mBeamActorID);
    daBeam_c* beamR = (daBeam_c*)getBeamActor(mFireActorID);

    C_QUATSlerp(&mQuatRotation, &ZeroQuat, &mQuatRotation, 0.05f);
    anime_proc();

    if (field_0x376 == 0) {
        if (beamL != NULL && beamR != NULL) {
            BOOL offL = beam_off(beamL);
            BOOL offR = beam_off(beamR);
            if (offL && offR && mQuatRotation.w > 0.99f && mAnimMorf->getFrame() < 25.0f &&
                mBrkAnm.getFrame() < 1.0f && mBtkAnm.getFrame() < 1.0f)
            {
                wait_proc_init();
            }
        }
    } else if (field_0x376 == 1) {
        if (mQuatRotation.w > 0.99f && mAnimMorf->getFrame() < 25.0f &&
            mBrkAnm.getFrame() < 1.0f && mBtkAnm.getFrame() < 1.0f)
        {
            wait_proc_init();
        }
    }
}

/* 00001B3C-00001D8C       .text checkRange__8daMozo_cFi */
BOOL daMozo_c::checkRange(int i_type) {
    daPy_py_c* player = (daPy_py_c*)dComIfGp_getPlayer(0);

    if (i_type == 0 && player->checkGrabWear()) {
        return FALSE;
    }

    cXyz diff = player->current.pos - current.pos;
    f32 dist = diff.absXZ();

    if (diff.y > -280.0f) {
        return FALSE;
    }

    f32 range;
    s16 angleLimit;

    if (i_type == 0) {
        daMozo_childHIO_c* child = ((daMozo_childHIO_c**)&l_HIO.mpBeamChild)[field_0x376];
        range = child->m04;
        angleLimit = child->m0C;
    } else {
        daMozo_childHIO_c* child = ((daMozo_childHIO_c**)&l_HIO.mpBeamChild)[field_0x376];
        range = child->m08;
        angleLimit = child->m0E;
    }

    cXyz dir(JMASSin(current.angle.y), 0.0f, JMASCos(current.angle.y));
    f32 dot = diff.getDotProduct(dir);

    if (cLib_distanceAngleS(fopAcM_searchActorAngleY(this, dComIfGp_getPlayer(0)), current.angle.y) < angleLimit &&
        dist < range && dot > 200.0f) {
        return true;
    }

    return FALSE;
}

/* 00001D8C-00001F70       .text setAnm__8daMozo_cFif */
void daMozo_c::setAnm(int anmIdx, float morf) {
    J3DAnmTransform* anm;
    int mode;
    f32 speed;
    f32 start;
    f32 end;

    mAnmIdx = anmIdx;
    switch (anmIdx) {
    case 0:
        anm = (J3DAnmTransform*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BCK_MOZ_e);
        mode = J3DFrameCtrl::EMode_NONE;
        start = 0.0f;
        end = -1.0f;
        speed = 0.0f;
        break;
    case 1:
        anm = (J3DAnmTransform*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BCK_MOZ_e);
        mode = J3DFrameCtrl::EMode_NONE;
        start = 0.0f;
        end = -1.0f;
        speed = 1.0f;
        break;
    case 2:
        anm = (J3DAnmTransform*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BCK_MOZ_e);
        mode = J3DFrameCtrl::EMode_NONE;
        start = 24.0f;
        end = 36.0f;
        speed = -0.25f;
        break;
    case 3:
        anm = (J3DAnmTransform*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BCK_MOZ_e);
        mode = J3DFrameCtrl::EMode_NONE;
        start = 25.0f;
        end = -1.0f;
        speed = 1.0f;
        break;
    case 4:
        anm = (J3DAnmTransform*)dComIfG_getObjectRes("Mozo", dRes_INDEX_MOZO_BCK_MOZ_e);
        mode = J3DFrameCtrl::EMode_NONE;
        start = 32.0f;
        end = 36.0f;
        speed = -0.25f;
        break;
    default:
        return;
    }

    mAnimMorf->setAnm(anm, mode, morf, speed, start, end, NULL);
}

/* 00001F70-00002228       .text CreateInit__8daMozo_cFv */
cPhs_State daMozo_c::CreateInit() {
    J3DModelData* mdlData = mAnimMorf->getModel()->getModelData();

    u8 temp = fopAcM_GetParam(this) & 0xFF;
    if (temp == 0xFF) {
        temp = 0;
    }
    field_0x376 = cLib_minMaxLimit<u8>(temp, 0, 2);

    mAnimMorf->getModel()->setUserArea((uintptr_t)this);
    for (u16 i = 0; i < mdlData->getJointNum(); i++) {
        if (i == MOZ_JNT_ATAMA_J_e) {
            mdlData->getJointNodePointer(i)->setCallBack(daMozo_nodeCallBack);
        }
    }

    mQuatRotation = ZeroQuat;

    fopAcM_SetMin(this, -1000.0f, -1000.0f, -1000.0f);
    fopAcM_SetMax(this, 1000.0f, 1000.0f, 1000.0f);
    fopAcM_SetMtx(this, mAnimMorf->getModel()->getBaseTRMtx());

    mBeamPos = current.pos;
    mFirePos = current.pos;

    if (field_0x376 == 0) {
        cXyz childScale(1.5f, 1.5f, 20.0f);
        mBeamActorID = fopAcM_createChild(fpcNm_Beam_e, fopAcM_GetID(this), 0, &mBeamPos,
                                           tevStr.mRoomNo, NULL, &childScale, -1);
        mFireActorID = fopAcM_createChild(fpcNm_Beam_e, fopAcM_GetID(this), 0x30000000, &mFirePos,
                                           tevStr.mRoomNo, NULL, &childScale, -1);
    } else {
        mStts.Init(0xff, 0xff, this);
        mCps.Set(cps_src);
        mCps.SetStts(&mStts);
        mCps.SetStartEnd(current.pos, current.pos);
        mCps.SetR(25.0f);
    }

    set_mtx();
    mAnimMorf->calc();
    wait_proc_init();

    if (l_HIO.mNo < 0) {
        l_HIO.mNo = mDoHIO_createChild("モ石像", &l_HIO); // "Mo Statue" (mo sekizou)
    }

    return cPhs_COMPLEATE_e;
}

/* 00002228-000023B0       .text _create__8daMozo_cFv */
cPhs_State daMozo_c::_create() {
    fopAcM_ct(this, daMozo_c);

    cPhs_State result = dComIfG_resLoad(&mPhs, "Mozo");

    if (result == cPhs_COMPLEATE_e) {
        if (fopAcM_entrySolidHeap(this, CheckCreateHeap, 0x1AA0)) {
            result = CreateInit();
            _execute();
        } else {
            return cPhs_ERROR_e;
        }
    }

    return result;
}

/* 000023B0-0000242C       .text _delete__8daMozo_cFv */
bool daMozo_c::_delete() {
    dComIfG_resDelete(&mPhs, "Mozo");
    mDoAud_seDeleteObject(&mSePos);

    if (l_HIO.mNo >= 0) {
        mDoHIO_deleteChild(l_HIO.mNo);
        l_HIO.mNo = -1;
    }

    return true;
}

/* 0000242C-00002498       .text getBeamActor__8daMozo_cFUi */
fopAc_ac_c* daMozo_c::getBeamActor(fpc_ProcID apid) {
    fopAc_ac_c* ac = fopAcM_SearchByID(apid);
    if (ac == NULL)
        return NULL;
    if (fopAcM_IsActor(ac) && fopAcM_GetProfName(ac) == fpcNm_Beam_e)
        return ac;
    return NULL;
}

/* 00002498-00002588       .text event_move__8daMozo_cFv */
void daMozo_c::event_move() {
    if (m_event_flag != 2) {
        if (eventInfo.checkCommandDemoAccrpt()) {
            field_0x377 = 0;
            m_event_flag = 1;
        }
        if (m_event_flag == 1 && dComIfGp_evmng_endCheck("MOZO_CAM")) {
            dComIfGp_event_reset();
            m_event_flag = 2;
        }
        if (m_event_flag == 0 && field_0x377 == 1) {
            fopAcM_orderOtherEvent2(this, "MOZO_CAM", dEvtFlag_NOPARTNER_e);
            eventInfo.onCondition(dEvtCnd_UNK2_e);
        }
    }
}

/* 00002588-000025DC       .text _execute__8daMozo_cFv */
bool daMozo_c::_execute() {
    mAnimMorf->calc();

    (this->*mCurrentProc)();
    event_move();
    set_mtx();

    return true;
}

/* 000025DC-0000267C       .text _draw__8daMozo_cFv */
bool daMozo_c::_draw() {
    /* Nonmatching */
    J3DModelData* mdlData = mAnimMorf->getModel()->getModelData();
    g_env_light.settingTevStruct(TEV_TYPE_BG0, &current.pos, &tevStr);
    g_env_light.setLightTevColorType(mAnimMorf->getModel(), &tevStr);

    mBrkAnm.entry(mdlData);
    mBtkAnm.entry(mdlData);
    mAnimMorf->entryDL();

    return true;
}

/* 0000267C-000026A0       .text daMozo_Draw__FP8daMozo_c */
static BOOL daMozo_Draw(daMozo_c* i_this) {
    return i_this->_draw();
}

/* 000026A0-000026C4       .text daMozo_Execute__FP8daMozo_c */
static BOOL daMozo_Execute(daMozo_c* i_this) {
    return i_this->_execute();
}

/* 000026C4-000026CC       .text daMozo_IsDelete__FP8daMozo_c */
static BOOL daMozo_IsDelete(daMozo_c*) {
    return TRUE;
}

/* 000026CC-000026F0       .text daMozo_Delete__FP8daMozo_c */
static BOOL daMozo_Delete(daMozo_c* i_this) {
    return i_this->_delete();
}

/* 000026F0-00002710       .text daMozo_Create__FP10fopAc_ac_c */
static cPhs_State daMozo_Create(fopAc_ac_c* i_this) {
    return static_cast<daMozo_c*>(i_this)->_create();
}

static actor_method_class l_daMozo_Method = {
    (process_method_func)daMozo_Create,
    (process_method_func)daMozo_Delete,
    (process_method_func)daMozo_Execute,
    (process_method_func)daMozo_IsDelete,
    (process_method_func)daMozo_Draw,
};

actor_process_profile_definition g_profile_MOZO = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0007,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_MOZO_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daMozo_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_MOZO_e,
    /* Actor SubMtd */ &l_daMozo_Method,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
