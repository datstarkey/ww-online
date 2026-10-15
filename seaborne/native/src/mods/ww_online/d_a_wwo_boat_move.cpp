// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// daWwoBoat_c's execute (see d_a_wwo_boat.cpp): the pose from the peer's TwwBoatState, the models, the sail, and
// the boat in the local world: its collision, wake and waterline, as daShip_c::execute's.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_grid.h"
#include "d/actor/d_a_sea.h"
#include "d/d_com_inf_game.h"
#include "res/Object/Ship.h"
#include "SSystem/SComponent/c_lib.h"
#include "SSystem/SComponent/c_math.h"

#include "d_a_wwo_boat.h"
#include "ww_smooth.h"

#include <algorithm>

using namespace boatlook;

namespace {

const char kArc[] = "Ship";
const f32 kMaxCoord = 1.0e6f;
const f32 kMaxSpeed = 1000.0f;
const f32 kEase = 0.3f;
const u8 kSailOnFrame = 7;           // procSteerMove sets SAIL_ON once FN_MAST_ON2 reaches this frame
const f32 kRopeSegment = 10.0f;      // |l_rope_base_vec|

bool finite(f32 v, f32 limit) {
    return v == v && v > -limit && v < limit;
}

// daShip_c::create's colliders (its cyl_src / sph_src)
const dCcD_SrcCyl l_cylSrc = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ ~(AT_TYPE_WATER | AT_TYPE_UNK20000 | AT_TYPE_UNK400000 | AT_TYPE_LIGHT),
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsPlayer_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e| cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ 0,
        /* SrcGObjAt Spl     */ 0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ dCcG_SE_UNK23,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ 0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ 0,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGCylS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 75.0f,
        /* Height */ 115.0f,
    }},
};

const dCcD_SrcSph l_sphSrc = {
    // dCcD_SrcGObjInf
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ ~(AT_TYPE_WATER | AT_TYPE_UNK20000 | AT_TYPE_UNK400000 | AT_TYPE_LIGHT),
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsPlayer_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsOther_e| cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ 0,
        /* SrcGObjAt Spl     */ 0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ dCcG_SE_UNK23,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ 0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ 0,
        /* SrcGObjCo SPrm    */ 0,
    },
    // cM3dGSphS
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 75.0f,
    }},
};

} // namespace

// The peer's mast / head bcks at their frames; a bck change blends as daShip_c's (mast 3 frames, head 5,
// DAMAGE1 none), a boat without a pose yet takes it at once. Play speed 0: the frame is the peer's.
void daWwoBoat_c::animate(const TwwBoatState& s) {
    const u16 mast = (s.flags & TWW_BOAT_MAST_RAISED) ? dRes_INDEX_SHIP_BCK_FN_MAST_ON2_e : dRes_INDEX_SHIP_BCK_FN_MAST_OFF2_e;
    if (mast != m0392) {
        m0392 = mast;
        mpBodyAnm->setAnm((J3DAnmTransform*)dComIfG_getObjectRes(kArc, mast), J3DFrameCtrl::EMode_NONE,
                          mHasPose ? 3.0f : 0.0f, 0.0f, 0.0f, -1.0f, nullptr);
    }
    const u16 headBck = s.head_bck;
    if (headBck >= dRes_INDEX_SHIP_BCK_AKIBI1_e && headBck <= dRes_INDEX_SHIP_BCK_KYAKKAN1_e &&
        headBck != dRes_INDEX_SHIP_BCK_FN_MAST_OFF2_e && headBck != dRes_INDEX_SHIP_BCK_FN_MAST_ON2_e && headBck != m03B4) {
        m03B4 = headBck;
        mpHeadAnm->setAnm((J3DAnmTransform*)dComIfG_getObjectRes(kArc, headBck), J3DFrameCtrl::EMode_NONE,
                          mHasPose && headBck != dRes_INDEX_SHIP_BCK_DAMAGE1_e ? 5.0f : 0.0f, 0.0f, 0.0f, -1.0f, nullptr);
    }
    mpBodyAnm->setFrame(std::min<f32>(s.mast_frame, mpBodyAnm->getEndFrame()));
    mpHeadAnm->setFrame(std::min<f32>(s.head_frame, mpHeadAnm->getEndFrame()));
    mpBodyAnm->play(nullptr, 0, 0);
    mpHeadAnm->play(nullptr, 0, 0);
}

// Returns the roll setWaveAngle aims for (setEffectData's wave pitch).
s16 daWwoBoat_c::pose(const TwwBoatState& s) {
    // The peer's states played back a little behind the newest (ww_smooth.cpp).
    f32 played[3] = {s.pos[0], s.pos[1], s.pos[2]};
    s16 playedAngle[3] = {0, s.angle_y, 0};
    g_wwoSmoothBoat[mSlot].pose(played, playedAngle);
    const cXyz net(played[0], played[1], played[2]);
    current.pos.x = net.x;
    current.pos.z = net.z;
    shape_angle.y = playedAngle[1];
    if (!mHasPose) {
        m0370 = m0372 = m0374 = m0376 = 0;
    }
    current.angle.y = shape_angle.y;

    // Height and tilt from the local water, the peer's waves not being ours: the sea or water BG (Tower of the Gods),
    // as daShip_c::getWaterY; the peer's height in the air or off water. The Acch only finds the ground and water
    // (and the room): the position stays the peer's.
    const cXyz pos = current.pos;
    mAcch.CrrPos(*dComIfG_Bgsp());
    current.pos = pos;
    const bool onWater = !(s.flags & TWW_BOAT_FLYING) &&
                         (daSea_ChkArea(current.pos.x, current.pos.z) || mAcch.ChkWaterHit());
    m03F4 = net.y; // getWaterY's answer off water
    const f32 y = onWater ? getWaterY() : net.y;
    current.pos.y = mHasPose ? current.pos.y + (y - current.pos.y) * kEase : y;
    m037C = shape_angle.x;
    m037E = shape_angle.z;
    s16 pitch, roll = m037E;
    if (onWater) {
        m03F4 = y;
        setWaveAngle(&pitch, &roll);
    } else {
        cLib_addCalcAngleS(&m0370, 0, 4, 0x400, 0x10);
        cLib_addCalcAngleS(&m0372, 0, 4, 0x400, 0x10);
    }
    shape_angle.x = m0370;
    shape_angle.z = m0372;
    speedF = s.speed_f;

    mSailAngle = s.sail_angle;
    m0366 = s.tiller;
    m03A0 = s.head_x;
    m03A2 = s.head_y;
    m03E8 = (s.flags & TWW_BOAT_MAST_HIDDEN) ? 0.001f : 1.0f;
    const u8 part = s.part <= PART_CRANE_e ? s.part : (u8)PART_WAIT_e;
    if (part != mPart || !mHasPose) {
        m0394 = s.cannon_yaw;
        m0396 = s.cannon_pitch;
        m0398 = s.crane_angle;
    } else {
        cLib_addCalcAngleS(&m0394, s.cannon_yaw, 2, 0x2000, 0x20);
        cLib_addCalcAngleS(&m0396, s.cannon_pitch, 2, 0x2000, 0x20);
        cLib_addCalcAngleS(&m0398, s.crane_angle, 2, 0x2000, 0x20);
    }
    m039C = 0;
    mPart = part;
    mRopeCnt = std::min<s16>(s.rope_length, kWwoMaxRope);
    if ((s.flags & TWW_BOAT_MAST_RAISED) && !(s.flags & TWW_BOAT_MAST_HIDDEN) && s.mast_frame >= kSailOnFrame) {
        onStateFlg(daSFLG_SAIL_ON_e);
    } else {
        offStateFlg(daSFLG_SAIL_ON_e);
    }
    return roll;
}

// The crane's rope from the arm's tip straight down (setRopePos without its sway), the hook at its end turned as
// setRopePos turns it for a straight rope, scaled with the mast while it rises.
void daWwoBoat_c::calcRope() {
    static const Vec rope_offset = {160.0f, 0.0f, 0.0f};
    cXyz tip;
    mDoMtx_multVec(mpSalvageArmModel->getAnmMtx(VFNCR_JNT_V_CRANE_ROTATION_e), &rope_offset, &tip);
    cXyz* seg = mRopeLine.getPos(0); // [mRopeCnt - 1] is the tip, [0] the free end
    for (s16 i = 0; i < mRopeCnt; i++) {
        seg[i] = tip;
        seg[i].y -= kRopeSegment * (mRopeCnt - 1 - i);
    }
    mDoMtx_stack_c::transS(tip.x, tip.y - kRopeSegment * std::max(mRopeCnt - 1, 0), tip.z);
    mDoMtx_stack_c::ZXYrotM(-0x8000, shape_angle.y, 0);
    mDoMtx_stack_c::XrotM(-0x4000);
    if (m0392 == dRes_INDEX_SHIP_BCK_FN_MAST_ON2_e) {
        J3DTransformInfo info;
        mpBodyAnm->getAnm()->getTransform(FN_BODY_JNT_J_FN_MAST_e, &info);
        mDoMtx_stack_c::scaleM(info.mScale.x, info.mScale.y, info.mScale.z);
    }
    mpLinkModel->setBaseTRMtx(mDoMtx_stack_c::get());
}

// daShip_c::execute's grid placement, then the grid's own execute (fade and ho_move) for this ship.
void daWwoBoat_c::placeSail() {
    static cXyz top_offset(0.0f, 0.0f, -365.0f);
    static cXyz XZ_top_offset(265.0f, 0.0f, 0.0f);
    J3DModel* body = mpBodyAnm->getModel();
    MtxP sail1 = body->getAnmMtx(FN_BODY_JNT_J_FN_SAIL1_e);
    cXyz v;
    mpSail->current.pos.set(sail1[0][3], sail1[1][3], sail1[2][3]);
    mpSail->current.angle = shape_angle;
    cMtx_multVecSR(sail1, &top_offset, &v);
    mpSail->scale.y = v.abs() / 365.0f;
    cMtx_multVecSR(body->getAnmMtx(FN_BODY_JNT_J_FN_SAIL2_e), &XZ_top_offset, &v);
    mpSail->field_0x2200 = 1.0f - v.abs() / 265.0f;
    daGrid_pcSetShip(this);
    mpSail->_execute();
    daGrid_pcSetShip(nullptr);
}


// daShip_c::create's colliders and ground check. The colliders are immovable (weight 0xFF) and the boat goes where the
// peer is (its CC moves are dropped), so the local ship and Link are pushed off it; projectiles hit it and nothing
// happens to it. The Acch finds the ground and water under it (without daShip_c's fourth, sea floor circle).
void daWwoBoat_c::createCollision() {
    mStts.Init(0xFF, 0, this);
    for (dCcD_Cyl& cyl : mCyl) {
        cyl.Set(l_cylSrc);
        cyl.SetStts(&mStts);
    }
    mCyl[1].SetR(95.0f);
    mSph.Set(l_sphSrc);
    mSph.SetStts(&mStts);
    mAcch.Set(fopAcM_GetPosition_p(this), fopAcM_GetOldPosition_p(this), this, 3, &mAcchCir[0], fopAcM_GetSpeed_p(this),
              fopAcM_GetAngle_p(this), fopAcM_GetShapeAngle_p(this));
    mAcchCir[0].SetWall(0.0f, 250.0f);
    mAcchCir[1].SetWall(75.0f, 250.0f);
    mAcchCir[2].SetWall(150.0f, 250.0f);
    mAcch.ClrWaterNone();
    mAcch.SetWaterCheckOffset(10000.0f);

    // The hull's waterline texture matrix (daShip_c::create; set in draw).
    J3DModelData* data = mpBodyAnm->getModel()->getModelData();
    for (u16 mno = 0; mno < data->getMaterialNum(); mno++) {
        for (u32 i = 0; i < 8; i++) {
            J3DTexMtx* texMtx = data->getMaterialNodePointer(mno)->getTexMtx(i);
            if (texMtx != nullptr && texMtx->getTexMtxInfo().mInfo == 8) {
                m02A0 = texMtx;
                m02A4 = texMtx;
            }
        }
    }
    C_MTXOrtho(m02A8, 50.0f, -50.0f, -50.0f, 50.0f, 0.1f, 100000.0f);
}

// daShip_c::execute's colliders: three cylinders along the hull, a sphere on the head.
void daWwoBoat_c::setCollision() {
    static const f32 cyl_offset[] = {100.0f, -20.0f, -100.0f};
    static const Vec sph_offset = {-5.0f, 0.0f, 0.0f};
    const f32 sin = cM_ssin(shape_angle.y);
    const f32 cos = cM_scos(shape_angle.y);
    for (int i = 0; i < 3; i++) {
        cXyz c(current.pos.x + sin * cyl_offset[i], current.pos.y - 30.0f, current.pos.z + cos * cyl_offset[i]);
        mCyl[i].SetC(c);
        mCyl[i].SetTgGrp(cCcD_TgSPrm_IsPlayer_e | cCcD_TgSPrm_IsOther_e);
        dComIfG_Ccsp()->Set(&mCyl[i]);
    }
    cXyz c;
    mDoMtx_multVec(mpHeadAnm->getModel()->getAnmMtx(FN_HEAD_H_JNT_J_FN_ATAMA_e), &sph_offset, &c);
    mSph.SetC(c);
    mSph.SetTgGrp(cCcD_TgSPrm_IsPlayer_e | cCcD_TgSPrm_IsOther_e);
    dComIfG_Ccsp()->Set(&mSph);
    mStts.ClrCcMove();
}

// daShip_c::execute's wake (setEffectData, from the speed over the water along the heading) and the waterline
// texture's height and scroll (drawn by daShip_c::draw's code in ours).
void daWwoBoat_c::setEffects(s16 roll) {
    const f32 moved = (current.pos - old.pos).absXZ();
    mFwdVel = speedF >= 0.0f ? moved : -moved;
    if (std::fabs(mFwdVel) <= 0.01f || mFwdVel * speedF < 0.0f) {
        mWaveL.stop();
        mWaveR.stop();
        mSplash.stop();
        mTrack.stop();
    }
    setEffectData(m03F4, roll);

    const f32 diff = m03F4 - current.pos.y + 5.0f;
    if (diff > m03D8) {
        m03D8 = diff;
    } else {
        cLib_chaseF(&m03D8, diff, 3.0f);
    }
    m03D4 += std::clamp(speedF * 0.1f / 55.0f, -0.05f, 0.05f);
    m03D4 -= std::floor(m03D4);
}

BOOL daWwoBoat_c::execute() {
    const WwoBoatPeer& peer = g_wwoBoats[mSlot];
    const TwwBoatState& s = peer.state;
    mVisible = false;
    if (!peer.active || !finite(s.pos[0], kMaxCoord) || !finite(s.pos[1], kMaxCoord) || !finite(s.pos[2], kMaxCoord) ||
        !finite(s.speed_f, kMaxSpeed)) {
        mHasPose = false;
        return TRUE; // wwo_boatFrame deletes it
    }
    mParked = (s.flags & TWW_BOAT_PARKED) != 0;
    old.pos = current.pos;
    const s16 roll = pose(s);
    animate(s);

    // As daShip_c::execute: the hull at T(pos) * ZXYrot(shape_angle), the head on J_FN_GATTAI, the part that's out.
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::ZXYrotM(shape_angle);
    J3DModel* body = mpBodyAnm->getModel();
    body->setBaseTRMtx(mDoMtx_stack_c::get());
    mpBodyAnm->calc();
    mpHeadAnm->getModel()->setBaseTRMtx(body->getAnmMtx(FN_BODY_JNT_J_FN_GATTAI_e));
    mpHeadAnm->calc();
    if (mPart == PART_CRANE_e) {
        mpSalvageArmModel->calc();
        calcRope();
    } else if (mPart == PART_CANNON_e) {
        mpCannonModel->calc();
    }
    placeSail();
    setCollision();
    setEffects(roll);

    // Lit for the room: the ground's, else (open water) the local stay room, or a parked boat's own.
    setRoomInfo();
    if (mParked && s.room >= 0 && mAcch.GetGroundH() == -G_CM3D_F_INF) {
        fopAcM_SetRoomNo(this, s.room);
        tevStr.mRoomNo = s.room;
    }
    color(peer.look);

    mHasPose = true;
    mVisible = true;
    return TRUE;
}

