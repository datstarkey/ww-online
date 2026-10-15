// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// ww_online's peer boats (tww-native-notes WW_ONLINE_EMBED.md; the Dolphin client's REL is
// GameMod/src/puppet_link/puppet_boat.c): a peer's King of Red Lions, drawn where the peer sails it with their
// puppet seated in it, or empty where they left it (parked).
//
// A daWwoBoat_c is its own actor (fpcNm_PUPPET_BOAT_e, created by ww_boat.cpp), so a parked boat shows without a
// puppet. It is a daShip_c, built by the stock createHeap, so the stock joint callbacks pose its models from its
// own fields (they find their ship through the model's user area); the ship's procs never run. Its execute
// (d_a_wwo_boat_move.cpp; list 4, before the puppet's list 5, as daShip_c before daPy_lk_c) sets those fields from
// the peer's TwwBoatState: the position carried forward along the heading between updates, height and tilt from the
// local water (getWaterY, setWaveAngle), the mast / head bcks at the peer's frames, the sail, tiller, head look,
// cannon and crane. The crane's rope hangs straight down (no sway). The sail is an imitation daGrid_c run by
// d_a_grid.cpp's own code with this boat as its l_ship (daGrid_pcSetShip). In the local world it has daShip_c's
// colliders (immovable: the local ship and Link are pushed off it), wake, waterline and shadow.
//
// Colours (ww_boatlook.cpp): the hull and head draw through J3DTextures of their own (their TEX1 headers copied, the
// red ones pointing at a recoloured palette; each material packet binds its table), the sail through
// daHo_pcSetPalette around its packet's draw.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_grid.h"
#include "d/actor/d_a_player_main.h"
#include "d/actor/d_a_player_main_data.h"
#include "d/actor/d_a_sea.h"
#include "d/actor/d_a_ship.h"
#include "d/d_com_inf_game.h"
#include "m_Do/m_Do_audio.h"
#include "res/Object/Ship.h"
#include "SSystem/SComponent/c_lib.h"
#include "SSystem/SComponent/c_math.h"

#include "d_a_wwo_boat.h"

#include <algorithm>
#include <cstring>
#include <new>

using namespace boatlook;

namespace {

const char kArc[] = "Ship";
const f32 kMinSailScale = 0.06f;     // daGrid_c neither moves nor draws a sail shorter than this

// The stock joint callbacks (d_a_ship.cpp, file static there), set on the shared joints by the local ship's create;
// these stand in until one has run.
BOOL boatBodyCallBack(J3DNode* node, int timing) {
    daShip_c* ship = (daShip_c*)j3dSys.getModel()->getUserArea();
    if (timing == J3DNodeCBCalcTiming_In && ship != nullptr) {
        ship->bodyJointCallBack(static_cast<J3DJoint*>(node)->getJntNo());
    }
    return TRUE;
}

BOOL boatHeadCallBack0(J3DNode*, int timing) {
    if (timing == J3DNodeCBCalcTiming_In) {
        ((daShip_c*)j3dSys.getModel()->getUserArea())->headJointCallBack0();
    }
    return TRUE;
}

BOOL boatHeadCallBack1(J3DNode* node, int timing) {
    if (timing == J3DNodeCBCalcTiming_In) {
        ((daShip_c*)j3dSys.getModel()->getUserArea())->headJointCallBack1(static_cast<J3DJoint*>(node)->getJntNo());
    }
    return TRUE;
}

BOOL boatCannonCallBack(J3DNode* node, int timing) {
    if (timing == J3DNodeCBCalcTiming_In) {
        ((daShip_c*)j3dSys.getModel()->getUserArea())->cannonJointCallBack(static_cast<J3DJoint*>(node)->getJntNo());
    }
    return TRUE;
}

BOOL boatCraneCallBack(J3DNode*, int timing) {
    if (timing == J3DNodeCBCalcTiming_In) {
        ((daShip_c*)j3dSys.getModel()->getUserArea())->craneJointCallBack();
    }
    return TRUE;
}

void setJointCallBack(J3DModel* model, u16 jno, J3DNodeCallBack callBack) {
    J3DJoint* joint = model->getModelData()->getJointNodePointer(jno);
    if (joint->getCallBack() == nullptr) {
        joint->setCallBack(callBack);
    }
}

// The sail's packet: daHo_packet_c's draw with its boat's palette (placed over the imitation grid's packet).
class WwoSailPacket : public daHo_packet_c {
public:
    void draw() override;
};
static_assert(sizeof(WwoSailPacket) == sizeof(daHo_packet_c), "the sail packet replaces daGrid_c::mPacket in place");

} // namespace

namespace {

daWwoBoat_c* sBoats[TWW_MAX_PEERS];

void WwoSailPacket::draw() {
    const void* pal = nullptr;
    for (daWwoBoat_c* boat : sBoats) {
        if (boat != nullptr && boat->mpSail != nullptr && &boat->mpSail->mPacket == this) {
            pal = boat->mpSailPal + boat->mSailCur * kPalBytes;
        }
    }
    daHo_pcSetPalette(pal);
    daHo_packet_c::draw();
    daHo_pcSetPalette(nullptr);
}

BOOL createHeapCallBack(fopAc_ac_c* actor) {
    return static_cast<daWwoBoat_c*>(actor)->createBoatHeap();
}

} // namespace

// A red model's texture table of its own: its TEX1 headers copied, offsets rebased to the copy (negative distances
// are allowed, see J3DTexture::setResTIMG), and two palettes for the red ones.
bool daWwoBoat_c::createRed(Part part) {
    RedLook& look = mRed[part];
    J3DTexture* tex = redModel(part)->getModelData()->getTexture();
    const u16 n = tex->getNum();
    ResTIMG* timg = new ResTIMG[n];
    look.pal = new (0x20) u8[2 * kPalBytes];
    if (timg == nullptr || look.pal == nullptr) {
        return false;
    }
    const ResTIMG* ref = redRef(tex);
    for (u16 i = 0; i < n; i++) {
        const ResTIMG* src = tex->getResTIMG(i);
        timg[i] = *src;
        const intptr_t delta = (u8*)src - (u8*)&timg[i];
        timg[i].imageOffset = (u32)(s32)((s32)(u32)src->imageOffset + delta);
        timg[i].paletteOffset = (u32)(s32)((s32)(u32)src->paletteOffset + delta);
        if (ref != nullptr && i < kMaxRedHeaders && redSame(src, ref)) {
            look.red |= 1u << i;
        }
    }
    memset(look.pal, 0, 2 * kPalBytes);
    look.tex = new J3DTexture(n, timg);
    return look.tex != nullptr;
}

// daShip_c::createHeap's models, plus the boat's own: the hull's and head's texture tables and palettes, the sail's
// palettes and the imitation grid.
BOOL daWwoBoat_c::createBoatHeap() {
    if (!createHeap() || !createRed(kHull) || !createRed(kHead)) {
        return FALSE;
    }
    mpSailPal = new (0x20) u8[2 * kPalBytes];
    mpSail = (daGrid_c*)JKRHeap::alloc(sizeof(daGrid_c), 0x20, nullptr);
    if (mpSailPal == nullptr || mpSail == nullptr) {
        return FALSE;
    }
    memset(mpSailPal, 0, 2 * kPalBytes);
    memset((void*)mpSail, 0, sizeof(daGrid_c));
    return TRUE;
}

cPhs_State daWwoBoat_c::create() {
    fopAcM_ct(this, daWwoBoat_c);
    const u32 slot = fopAcM_GetParam(this) >> 24;
    mSlot = slot < TWW_MAX_PEERS ? (int)slot : 0;
    attention_info.flags = 0;

    cPhs_State phase = dComIfG_resLoad(&mPhs, kArc);
    const cPhs_State cloth = dComIfG_resLoad(&mClothPhs, "Cloth");
    if (phase == cPhs_ERROR_e || cloth == cPhs_ERROR_e) {
        return cPhs_ERROR_e;
    }
    if (phase != cPhs_COMPLEATE_e || cloth != cPhs_COMPLEATE_e) {
        return cPhs_INIT_e;
    }
    if (!fopAcM_entrySolidHeap(this, createHeapCallBack, 0x20000 + sizeof(daGrid_c) + 0x4000)) {
        return cPhs_ERROR_e;
    }

    // As daShip_c::create: the models find this ship through their user area.
    J3DModel* body = mpBodyAnm->getModel();
    J3DModel* head = mpHeadAnm->getModel();
    body->setUserArea((uintptr_t)this);
    head->setUserArea((uintptr_t)this);
    mpCannonModel->setUserArea((uintptr_t)this);
    mpSalvageArmModel->setUserArea((uintptr_t)this);
    for (u16 jno : {FN_BODY_JNT_J_FN_STEER1_e, FN_BODY_JNT_J_FN_KAJI_e, FN_BODY_JNT_J_FN_SAIL1_e, FN_BODY_JNT_J_FN_MAST_e}) {
        setJointCallBack(body, jno, boatBodyCallBack);
    }
    setJointCallBack(head, FN_HEAD_H_JNT_J_FN_ATAMA_e, boatHeadCallBack0);
    setJointCallBack(head, FN_HEAD_H_JNT_J_FN_AGO2_e, boatHeadCallBack0);
    for (u16 jno = FN_HEAD_H_JNT_J_FN_KUBI1_e; jno <= FN_HEAD_H_JNT_J_FN_KUBI6_e; jno++) {
        setJointCallBack(head, jno, boatHeadCallBack1);
    }
    setJointCallBack(mpCannonModel, VFNCN_JNT_CANON1_e, boatCannonCallBack);
    setJointCallBack(mpCannonModel, VFNCN_JNT_CANON2_e, boatCannonCallBack);
    setJointCallBack(mpSalvageArmModel, VFNCR_JNT_V_CRANE_ROTATION_e, boatCraneCallBack);

    // The hull and head draw with their own texture tables: each material packet binds the table it holds
    // (J3DMatPacket::draw).
    for (Part part : kRedParts) {
        J3DModel* model = redModel(part);
        for (u16 i = 0; i < model->getModelData()->getMaterialNum(); i++) {
            model->getMatPacket(i)->setTexture(mRed[part].tex);
        }
    }
    fopAcM_SetMtx(this, body->getBaseTRMtx());
    fopAcM_SetMin(this, -325.0f, -50.0f, -325.0f);
    fopAcM_SetMax(this, 325.0f, 570.0f, 240.0f);
    dKy_tevstr_init(&tevStr, dComIfGp_roomControl_getStayNo(), 0xFF);
    m03E8 = 1.0f;
    mPart = PART_WAIT_e;
    createCollision();

    // The sail: daGrid_c's _create on our block (its resources are loaded), bound to this ship; then our packet.
    daGrid_pcSetShip(this);
    mSailCreated = mpSail->_create() == cPhs_COMPLEATE_e;
    daGrid_pcSetShip(nullptr);
    if (!mSailCreated) {
        return cPhs_ERROR_e;
    }
    new (&mpSail->mPacket) WwoSailPacket();
    sBoats[mSlot] = this;
    return cPhs_COMPLEATE_e;
}

// A red part in the hull colour c (0: vanilla), written only when it changes, into the palette not in use.
void daWwoBoat_c::colorRed(Part part, u16 c) {
    RedLook& look = mRed[part];
    const ResTIMG* ref = redRef(redModel(part)->getModelData()->getTexture());
    if (ref == nullptr || (c | kKeySet) == look.key) {
        return;
    }
    look.cur ^= 1;
    u8* pal = look.pal + look.cur * kPalBytes;
    const u8* src = source(part, palOf(ref));
    if (c != 0) {
        redRecolor(pal, src, ref->numColors, c);
    } else {
        memcpy(pal, src, ref->numColors * 2);
    }
    for (u16 i = 0; i < kMaxRedHeaders; i++) {
        if (look.red & (1u << i)) {
            ResTIMG* timg = look.tex->getResTIMG(i);
            timg->paletteOffset = (u32)(s32)(pal - (u8*)timg);
            look.tex->resTIMGChanged(timg);
        }
    }
    look.key = c | kKeySet;
}

// The peer's colours into this boat's palettes.
void daWwoBoat_c::color(const TwwBoatLook& look) {
    for (Part part : kRedParts) {
        colorRed(part, look.hull_rgb565);
    }
    const ResTIMG* sail = (const ResTIMG*)dComIfG_getObjectRes(kArc, dRes_ID_SHIP_BTI_NEW_HO1_e);
    const u32 sailRgb = look.sail_rgb & 0xFFFFFF;
    if (sail != nullptr && isPalette(sail) && (sailRgb | kKeySet) != mSailKey) {
        mSailCur ^= 1;
        u8* pal = mpSailPal + mSailCur * kPalBytes;
        const u8* src = source(kSail, palOf(sail));
        if (sailRgb != 0) {
            sailRecolor(pal, src, sail->numColors, sailRgb);
        } else {
            memcpy(pal, src, sail->numColors * 2);
        }
        mSailKey = sailRgb | kKeySet;
    }
}

BOOL daWwoBoat_c::draw() {
    static const GXColor rope_color = {0xC8, 0x96, 0x32, 0xFF};
    if (!mVisible) {
        return TRUE;
    }
    J3DModel* body = mpBodyAnm->getModel();
    J3DModel* head = mpHeadAnm->getModel();
    g_env_light.settingTevStruct(TEV_TYPE_ACTOR, &current.pos, &tevStr);
    if (m02A0 != nullptr && m02A4 != nullptr) {
        // daShip_c::draw: the waterline texture, projected from the water's height along the hull (a material of
        // the shared model data: set before our entry, as the local ship sets it before its own).
        cXyz eye(current.pos.x, current.pos.y + m03D8 + 5.0f, current.pos.z);
        cXyz at(current.pos.x + cM_scos(m037C) * 10000.0f * cM_ssin(shape_angle.y),
                current.pos.y + m03D8 + 5.0f - cM_ssin(m037C) * 10000.0f,
                current.pos.z + cM_scos(m037C) * 10000.0f * cM_scos(shape_angle.y));
        Mtx lookAt;
        cMtx_lookAt(lookAt, &eye, &at, m037E);
        mDoMtx_stack_c::YrotS(0xC000);
        mDoMtx_stack_c::XrotM(-0x8000);
        mDoMtx_stack_c::scaleM(1.0f, 2.0f, 0.25f);
        mDoMtx_stack_c::concat(lookAt);
        mDoMtx_stack_c::revConcat(m02A8);
        m02A0->setEffectMtx(mDoMtx_stack_c::get());
        m02A4->mSRT.mTranslationX = m03D4;
    }
    g_env_light.setLightTevColorType(body, &tevStr);
    g_env_light.setLightTevColorType(head, &tevStr);

    // daShip_c::draw, everything always shown. The hull and head are entered with our texture tables: a packet whose
    // display list is rebuilt at entry records the model data's (J3DMatPacket::recordTexture).
    dComIfGd_setListP1();
    for (Part part : kRedParts) {
        J3DModelData* data = redModel(part)->getModelData();
        J3DTexture* tex = data->getTexture();
        data->setTexture(mRed[part].tex);
        mDoExt_modelEntryDL(redModel(part));
        data->setTexture(tex);
    }
    if (mPart == PART_CANNON_e) {
        g_env_light.setLightTevColorType(mpCannonModel, &tevStr);
        mDoExt_modelEntryDL(mpCannonModel);
    } else if (mPart == PART_CRANE_e) {
        g_env_light.setLightTevColorType(mpSalvageArmModel, &tevStr);
        mDoExt_modelEntryDL(mpSalvageArmModel);
        if (mRopeCnt >= 2) {
            mRopeLine.update(mRopeCnt, 5.0f, rope_color, 0, &tevStr);
            dComIfGd_set3DlineMat(&mRopeLine);
        }
        g_env_light.setLightTevColorType(mpLinkModel, &tevStr);
        mDoExt_modelUpdateDL(mpLinkModel);
    }
    dComIfGd_setList();

    mShadowId = dComIfGd_setShadow(mShadowId, 0, body, &current.pos, 1500.0f, 250.0f, current.pos.y, mAcch.GetGroundH(),
                                   mAcch.m_gnd, &tevStr);
    if (mShadowId != 0) {
        dComIfGd_addRealShadow(mShadowId, head);
        if (mPart == PART_CANNON_e) {
            dComIfGd_addRealShadow(mShadowId, mpCannonModel);
        } else if (mPart == PART_CRANE_e) {
            dComIfGd_addRealShadow(mShadowId, mpSalvageArmModel);
        }
    }

    if (mpSail->scale.y >= kMinSailScale) {
        daGrid_pcSetShip(this);
        mpSail->_draw();
        daGrid_pcSetShip(nullptr);
    }
    return TRUE;
}

BOOL daWwoBoat_c::remove() {
    mWaveL.remove();
    mWaveR.remove();
    mSplash.remove();
    mTrack.remove();
    mDoAud_seDeleteObject(&current.pos); // setEffectData's landing
    if (sBoats[mSlot] == this) {
        sBoats[mSlot] = nullptr;
    }
    if (mSailCreated) {
        mpSail->_delete();
    }
    // dComIfG_resDelete asserts a load in progress: such a reference stays (the archive stays loaded).
    if (mPhs.id != 1) {
        dComIfG_resDelete(&mPhs, kArc);
    }
    if (mClothPhs.id != 1) {
        dComIfG_resDelete(&mClothPhs, "Cloth");
    }
    return TRUE;
}

bool wwo_boatSeat(int slot, u32 proc, cXyz* pos, csXyz* angle) {
    const bool shipProc = (proc >= daPy_lk_c::daPyProc_SHIP_STEER_e && proc <= daPy_lk_c::daPyProc_SHIP_RESTART_e &&
                           proc != daPy_lk_c::daPyProc_SHIP_GET_OFF_e) ||
                          proc == daPy_lk_c::daPyProc_DEMO_SHIP_SIT_e;
    daWwoBoat_c* boat = shipProc && slot >= 0 && slot < TWW_MAX_PEERS ? sBoats[slot] : nullptr;
    if (boat == nullptr || !boat->mVisible || boat->mParked) {
        return false;
    }
    mDoMtx_multVec(boat->getBodyMtx(), &l_ship_offset, pos);
    *angle = boat->shape_angle;
    return true;
}

static cPhs_State daWwoBoat_Create(fopAc_ac_c* i_this) {
    return static_cast<daWwoBoat_c*>(i_this)->create();
}

static BOOL daWwoBoat_Delete(daWwoBoat_c* i_this) {
    return i_this->remove();
}

static BOOL daWwoBoat_Execute(daWwoBoat_c* i_this) {
    return i_this->execute();
}

static BOOL daWwoBoat_IsDelete(daWwoBoat_c*) {
    return TRUE;
}

static BOOL daWwoBoat_Draw(daWwoBoat_c* i_this) {
    return i_this->draw();
}

static actor_method_class l_daWwoBoat_Method = {
    (process_method_func)daWwoBoat_Create,
    (process_method_func)daWwoBoat_Delete,
    (process_method_func)daWwoBoat_Execute,
    (process_method_func)daWwoBoat_IsDelete,
    (process_method_func)daWwoBoat_Draw,
};

actor_process_profile_definition g_profile_PUPPET_BOAT = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 4, // daShip_c's: executes before the puppets (5), which sit in it
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_PUPPET_BOAT_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daWwoBoat_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_SHIP_e,
    /* Actor SubMtd */ &l_daWwoBoat_Method,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_FREEZE_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLBOX_CUSTOM_e,
};
