/**
 * puppet_draw.c - Puppet Link Drawing System
 *
 * This file contains all drawing-related code for the puppet actor.
 * Separated from puppet.c for better readability and maintainability.
 *
 * Mirrors the decompiled daPy_lk_c::draw() (tww-decomp d_a_player_main.cpp,
 * 0x80107308-0x80108204), with puppet-specific differences:
 *   - camera attention status (first-person etc.) is the LOCAL camera's state and is
 *     treated as 0 for every puppet decision; checkPlayerNoDraw is not used
 *   - equipment visibility comes from the peer's slot bytes (EQUIP_SWORD/EQUIP_SHIELD),
 *     not the local save
 *   - anything written into the J3DModelData shared with the real Link (texNo anims,
 *     texMtx anims, the linktexS3TC outfit/colour header) is restored right after the puppet's entry
 *
 * NOTE: This file is included directly by puppet.c - do not compile separately.
 * All necessary headers are included via puppet.c before this file.
 *
 * IMPORTANT: All field access uses offset macros - ww_structs.h is unreliable!
 */

#include "puppet_draw.h"

// ============================================================================
// J3D INLINE HELPERS - Replace fake function addresses with direct struct access
// ============================================================================

static inline J3DJoint *inline_J3DModelData__getJointNodePointer(J3DModelData *modelData, u16 index)
{
  if (!modelData)
    return NULL;
  J3DJoint **jointArray = J3DMODELDATA_MJOINTTREE_MPJOINTS(modelData);
  if (!jointArray)
    return NULL;
  return jointArray[index];
}

static inline J3DMaterial *inline_J3DJoint__getMesh(J3DJoint *joint)
{
  if (!joint)
    return NULL;
  return J3DJOINT_MPMATERIAL(joint);
}

static inline J3DShape *inline_J3DMaterial__getShape(J3DMaterial *mtl)
{
  if (!mtl)
    return NULL;
  return J3DMATERIAL_MPSHAPE(mtl);
}

static inline void inline_J3DShape__hide(J3DShape *shape)
{
  if (!shape)
    return;
  J3DSHAPE_MVISFLAGS(shape) |= 0x0001;
}

static inline void inline_J3DShape__show(J3DShape *shape)
{
  if (!shape)
    return;
  J3DSHAPE_MVISFLAGS(shape) &= ~0x0001;
}

static inline J3DMaterial *inline_J3DModelData__getMaterialNodePointer(J3DModelData *modelData, u16 index)
{
  if (!modelData)
    return NULL;
  J3DMaterial **materialArray = J3DMODELDATA_MMATERIALTABLE_MPMATERIAL(modelData);
  if (!materialArray)
    return NULL;
  return materialArray[index];
}

static inline J3DMaterial *inline_J3DMaterial__getNext(J3DMaterial *material)
{
  if (!material)
    return NULL;
  return J3DMATERIAL_MPNEXTMATERIAL(material);
}

#ifndef J3DModelData__getJointNodePointer
#define J3DModelData__getJointNodePointer inline_J3DModelData__getJointNodePointer
#endif
#ifndef J3DJoint__getMesh
#define J3DJoint__getMesh inline_J3DJoint__getMesh
#endif
#ifndef J3DMaterial__getShape
#define J3DMaterial__getShape inline_J3DMaterial__getShape
#endif
#ifndef J3DShape__hide
#define J3DShape__hide inline_J3DShape__hide
#endif
#ifndef J3DShape__show
#define J3DShape__show inline_J3DShape__show
#endif
#ifndef J3DModelData__getMaterialNodePointer
#define J3DModelData__getMaterialNodePointer inline_J3DModelData__getMaterialNodePointer
#endif
#ifndef J3DMaterial__getNext
#define J3DMaterial__getNext inline_J3DMaterial__getNext
#endif

// ============================================================================
// DISPLAY LIST PACKETS (per puppet instance - see PuppetDrawPackets in puppet_draw.h)
// ============================================================================

#define PUPPET_VT_ONCUPOFFAUPPACKET 0x80371B84 // __vt__24mDoExt_onCupOffAupPacket
#define PUPPET_VT_OFFCUPONAUPPACKET 0x80371B9C // __vt__24mDoExt_offCupOnAupPacket

static void puppet_initOnePacket(J3DPacket *packet, u32 vtbl)
{
  packet->vtbl = vtbl;
  packet->mpNextSibling = (J3DPacket *)0;
  packet->mpFirstChild = (J3DPacket *)0;
  packet->mpUserData = 0;
}

void puppet_initPackets(PuppetDrawPackets *packets)
{
  if (!packets)
    return;
  puppet_initOnePacket(&packets->onCupOffAup1, PUPPET_VT_ONCUPOFFAUPPACKET);
  puppet_initOnePacket(&packets->onCupOffAup2, PUPPET_VT_ONCUPOFFAUPPACKET);
  puppet_initOnePacket(&packets->offCupOnAup1, PUPPET_VT_OFFCUPONAUPPACKET);
  puppet_initOnePacket(&packets->offCupOnAup2, PUPPET_VT_OFFCUPONAUPPACKET);
}

// ============================================================================
// FLAG / ID CONSTANTS - from tww-decomp (d_a_player.h, d_a_player_main.cpp)
// ============================================================================

#define daPyFlg0_NO_DRAW 0x08000000
#define daPyFlg0_HEAVY_BOOTS 0x02000000   // daPyFlg0_EQUIP_HEAVY_BOOTS
#define daPyFlg0_UNK100 0x00000100
#define daPyFlg1_FROZEN_IN_ICE 0x00000800 // daPyFlg1_FREEZE_STATE
#define daPyFlg1_CONFUSE 0x00000100
#define daPyFlg1_CASUAL_CLOTHES 0x00000008
#define daPyFlg1_SOUP_POWER_UP 0x00008000

// dComIfGs_getSelectEquip(0) sword ids (DOL daPy_lk_c::draw 0x80107AE0 / 0x80107D08-0x80107D24)
#define PUPPET_ITEM_SWORD 0x38          // Hero's Sword
#define PUPPET_ITEM_MASTER_SWORD_1 0x39
#define PUPPET_ITEM_MASTER_SWORD_2 0x3A
#define PUPPET_ITEM_MASTER_SWORD_3 0x3E // full-power Master Sword
#define PUPPET_ITEM_HOOKSHOT 0x2F
#define PUPPET_ITEM_NONE 0xFF

// Joint indices in cl.bdl (confirmed from DOL loads of mpJoints[i])
#define CL_JNT_LINK_ROOT 0x00
#define CL_JNT_CL_LHANDA 0x08
#define CL_JNT_CL_RHANDA 0x0C
#define CL_JNT_CL_PODA 0x0D
#define CL_JNT_CL_EYE 0x13
#define CL_JNT_CL_HANA 0x14
#define CL_JNT_CL_MAYU 0x15
#define CL_JNT_CL_BACK 0x29

// Max texNo-anim pointers the puppet may temporarily swap into shared materials
// (Link's btp updates 5 materials; eyes/brows swap a pair each => 9)
#define PUPPET_TEXNO_SAVE_MAX 16

// ============================================================================
// HANDS MODEL SHAPE VISIBILITY (shared with the real Link)
// ============================================================================
//
// Link's separate hands model (mpHandsModel, link.arc hands.bdl, one joint per hand pose)
// uses the SAME J3DModelData as the real Link's hands model. daPy_lk_c::setDrawHandModel
// (d_a_player_main.cpp:1388) only hides the two shapes THIS instance showed last time
// (mpLhandShape/mpRhandShape), shows its two new ones, and copies CL-model arm matrices into
// just those two joints of its own hands model (setAnmMtx, :1474/:1482). J3DShpFlag_Hide is
// evaluated at entry time (J3DJoint::entryIn), so any hand shape the real Link left visible
// would be entered by the puppet's hands model too, at a joint matrix the puppet never
// updated -> a detached forearm+hand floating next to the puppet (and vice versa for the
// real Link). So: hide every hands shape before the puppet's setDrawHandModel, and put the
// real Link's visibility back right after the puppet's hands entry.

#define PUPPET_HAND_SHAPE_MAX 128

typedef struct PuppetHandShapeSave
{
  J3DShape **shapes;
  u16 count;
  u32 hideBits[PUPPET_HAND_SHAPE_MAX / 32];
} PuppetHandShapeSave;

static void puppet_saveAndHideHandShapes(J3DModel *handsModel, PuppetHandShapeSave *save)
{
  u16 i;
  save->shapes = NULL;
  save->count = 0;
  for (i = 0; i < PUPPET_HAND_SHAPE_MAX / 32; i++)
    save->hideBits[i] = 0;

  if (handsModel == NULL)
    return;
  J3DModelData *handsData = *(J3DModelData **)((u8 *)handsModel + 0x04); // J3DModel::mModelData
  if (handsData == NULL)
    return;
  // J3DModelData::mShapeTable at 0x7C: { u16 mShapeNum; J3DShape** mShapeNodePointer (0x80) }
  u16 count = *(u16 *)((u8 *)handsData + 0x7C);
  J3DShape **shapes = *(J3DShape ***)((u8 *)handsData + 0x80);
  if (shapes == NULL)
    return;
  if (count > PUPPET_HAND_SHAPE_MAX)
    count = PUPPET_HAND_SHAPE_MAX;

  save->shapes = shapes;
  save->count = count;
  for (i = 0; i < count; i++)
  {
    if (shapes[i] == NULL)
      continue;
    if (J3DSHAPE_MVISFLAGS(shapes[i]) & 0x0001)
      save->hideBits[i >> 5] |= (1u << (i & 31));
    J3DSHAPE_MVISFLAGS(shapes[i]) |= 0x0001;
  }
}

static void puppet_restoreHandShapes(PuppetHandShapeSave *save)
{
  u16 i;
  for (i = 0; i < save->count; i++)
  {
    J3DShape *shape = save->shapes[i];
    if (shape == NULL)
      continue;
    if (save->hideBits[i >> 5] & (1u << (i & 31)))
      J3DSHAPE_MVISFLAGS(shape) |= 0x0001;
    else
      J3DSHAPE_MVISFLAGS(shape) &= ~0x0001;
  }
  save->count = 0;
}

// ============================================================================
// MAIN DRAW FUNCTION
// ============================================================================

/**
 * Main puppet draw function - mirrors decompiled daPy_lk_c::draw()
 *
 * Draw-list state contract (same as vanilla): eye/brow passes go to OpaListP0, the body
 * and equipment to P1, the mirror/shadow to the default lists, and the function returns
 * with j3dSys pointed at the default lists (dComIfGd_setList) so actors drawn after the
 * puppet are unaffected.
 *
 * @param link      Pointer to the puppet's daPy_lk_c instance
 * @param slotIndex PUPPET_class.slotIndex
 * @param packets   This puppet's own display list packets
 * @param lookBlock This puppet's tunic recolour cache (puppet_appearance.c)
 * @return 1 on success
 */
static int puppet_drawBody(daPy_lk_c *link, u32 slotIndex, PuppetDrawPackets *packets, u8 **lookBlock)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)link;
  int i;

  if (!packets)
    return 1;

  // Packets are set up in create (phase 2); re-init defensively if that never ran.
  if (packets->onCupOffAup1.vtbl == 0)
  {
    puppet_initPackets(packets);
  }

  // Get real player for validation
  daPy_lk_c *realPlayer = (daPy_lk_c *)dComIfGp_getPlayer(0);
  if (!realPlayer || realPlayer == link)
  {
    return 1;
  }

  // ============================================================================
  // Slot state (read once)
  // ============================================================================
  if (slotIndex >= PUPPET_MAX_SLOTS)
  {
    slotIndex = 0; // Out-of-range slot index, fallback to slot 0
  }
  u32 slotBase = PUPPET_SLOT_BASE(slotIndex);

  // The hook may keep a puppet alive for a slot that is currently inactive (numActive is
  // "highest visible slot + 1"). Don't draw it. (Such a puppet is also "parked" by
  // daPuppet_Execute and daPuppet_Draw doesn't even get here; this covers the slot going
  // inactive between that puppet's execute and draw.)
  if (*(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_ACTIVE) == 0)
  {
    daPy_lk_c__offBodyEffect(link);
    return 1;
  }

  u8 slotSword = *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_EQUIP_SWORD);
  u8 slotShield = *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_EQUIP_SHIELD);
  int remoteHasSword = (slotSword != PUPPET_ITEM_NONE);
  int remoteHasShield = (slotShield != PUPPET_ITEM_NONE);
  int remoteNormalSword = (slotSword == PUPPET_ITEM_SWORD);
  int remoteMasterSword = (slotSword == PUPPET_ITEM_MASTER_SWORD_1 ||
                           slotSword == PUPPET_ITEM_MASTER_SWORD_2 ||
                           slotSword == PUPPET_ITEM_MASTER_SWORD_3);
  int remoteFinalMasterSword = (slotSword == PUPPET_ITEM_MASTER_SWORD_3);

  // Clear NO_DRAW flag
  DAPY_PY_NO_RESET_FLG0(actor) &= ~daPyFlg0_NO_DRAW;

  // Sight packet (targeting reticle) intentionally not drawn for the puppet.

  // ============================================================================
  // TEV/Lighting Setup
  // ============================================================================
  dKy_tevstr_c *tevStr = FOPAC_TEV_STR(actor);
  cXyz *currentPos = FOPAC_CURRENT_POS(actor);
  dScnKy_env_light_c__settingTevStruct(&g_env_light, settingTevStruct__LightType__Player,
                                       currentPos, tevStr);

  // Vanilla returns early on checkPlayerNoDraw(), which is true whenever the LOCAL camera
  // is first-person (d_a_player_main.h:1978) - that would hide every puppet. Skipped.
  // Camera attention status is likewise the local camera's; treated as 0 below, so body
  // effects are always on.
  daPy_lk_c__onBodyEffect(link);

  // ============================================================================
  // Save fog colors for damage/confuse effects
  // ============================================================================
  s16 savedFogR = TEVSTR_MFOGCOLOR_R(tevStr);
  s16 savedFogG = TEVSTR_MFOGCOLOR_G(tevStr);
  s16 savedFogB = TEVSTR_MFOGCOLOR_B(tevStr);
  f32 savedFogStartZ = TEVSTR_MFOGSTARTZ(tevStr);
  f32 savedFogEndZ = TEVSTR_MFOGENDZ(tevStr);

  u32 noResetFlg0 = DAPY_PY_NO_RESET_FLG0(actor);
  u32 noResetFlg1 = DAPY_PY_NO_RESET_FLG1(actor);
  int mCurProcID = DAPY_LK_MCURPROC(link);
  u32 frozenFlag = noResetFlg1 & daPyFlg1_FROZEN_IN_ICE;

  // Damage/confuse fog tint. Vanilla also checks daPy_dmEcallBack_c::checkCurse() (a global
  // for the local player) and oscillates the start with |sin(timer)|; the puppet uses the
  // midpoint of that oscillation.
  if (frozenFlag == 0 && mCurProcID != 0x6C) // daPyProc_ELEC_DAMAGE_e
  {
    s16 mDamageWaitTimer = DAPY_PY_DAMAGE_WAIT_TIMER(actor);
    if ((noResetFlg1 & daPyFlg1_CONFUSE) != 0 || mDamageWaitTimer > 0)
    {
      cXyz camPos;
      m_Do_lib__mDoLib_pos2camera(currentPos, &camPos);
      if ((noResetFlg1 & daPyFlg1_CONFUSE) != 0)
      {
        // Purple for curse/confuse
        TEVSTR_MFOGCOLOR_R(tevStr) = 0x80;
        TEVSTR_MFOGCOLOR_G(tevStr) = 0x00;
        TEVSTR_MFOGCOLOR_B(tevStr) = 0xFF;
      }
      else
      {
        // Red for damage
        TEVSTR_MFOGCOLOR_R(tevStr) = 0xFF;
        TEVSTR_MFOGCOLOR_G(tevStr) = 0x3C;
        TEVSTR_MFOGCOLOR_B(tevStr) = 0x3C;
      }
      TEVSTR_MFOGSTARTZ(tevStr) = -camPos.z - 100.0f;
      TEVSTR_MFOGENDZ(tevStr) = TEVSTR_MFOGSTARTZ(tevStr) + 300.0f;
    }
  }

  // ============================================================================
  // Model and joint validation
  // ============================================================================
  J3DModel *mpCLModel = DAPY_LK_MPCLMODEL(link);
  if (!mpCLModel)
  {
    return 1;
  }

  J3DModelData *modelData = J3DMODEL_MPMODELDATA(mpCLModel);
  if (!modelData || !J3DMODELDATA_MJOINTTREE_MPJOINTS(modelData))
  {
    return 1;
  }

  J3DJoint *link_root_joint = J3DModelData__getJointNodePointer(modelData, CL_JNT_LINK_ROOT);
  J3DJoint *cl_eye_joint = J3DModelData__getJointNodePointer(modelData, CL_JNT_CL_EYE);
  J3DJoint *cl_mayu_joint = J3DModelData__getJointNodePointer(modelData, CL_JNT_CL_MAYU);
  if (!link_root_joint || !cl_eye_joint || !cl_mayu_joint)
  {
    return 1;
  }

  // ============================================================================
  // Texture pattern animations (eyes, brows, mouth)
  //
  // The J3DMaterialAnm objects live in the J3DModelData shared with the real Link, so the
  // pointers swapped in here are recorded and restored right after the puppet's entry.
  // ============================================================================
  J3DTexNoAnm **texNoSlot[PUPPET_TEXNO_SAVE_MAX];
  J3DTexNoAnm *texNoOld[PUPPET_TEXNO_SAVE_MAX];
  int texNoSaved = 0;
  {
    J3DAnmTexPattern *texPat = DAPY_LK_MPANMTEXPATTERNDATA(link);
    J3DTexNoAnm *texNoAnms = DAPY_LK_M_TEXNOANMS(link);

    if (texPat && texNoAnms)
    {
      u16 material_num = J3DANMTEXPATTERN_MATERIALNUM(texPat);
      u16 *matID_array = J3DANMTEXPATTERN_MATIDARRAY(texPat);
      u8 *anmTable = J3DANMTEXPATTERN_ANMTABLE(texPat);
      u16 matCount = J3DMODELDATA_MMATERIALTABLE_MMATERIALCOUNT(modelData);

      if (matID_array && anmTable)
      {
        for (u16 idx = 0; idx < material_num; idx++)
        {
          u16 matID = matID_array[idx];
          if (matID == 0xFFFF)
            continue;

          u8 texNo = J3DANMTEXPATTERN_ENTRY_TEXNO(anmTable, idx);

          // Mouth (0xE) is a single material; eyeL/R and mayuL/R also update matID+1
          int count = (matID != 0xE) ? 2 : 1;
          for (int k = 0; k < count; k++)
          {
            u16 id = (u16)(matID + k);
            if (id >= matCount || texNoSaved >= PUPPET_TEXNO_SAVE_MAX)
              break;

            J3DMaterial *mtl = J3DModelData__getMaterialNodePointer(modelData, id);
            if (!mtl)
              continue;

            void *mtlAnm = J3DMATERIAL_MPMATERIALANM(mtl);
            if (!mtlAnm || (u32)mtlAnm >= 0xC0000000)
              continue;

            J3DTexNoAnm **slot = &((J3DTexNoAnm **)J3DMATERIALANM_MTEXNOANM(mtlAnm))[texNo];
            texNoSlot[texNoSaved] = slot;
            texNoOld[texNoSaved] = *slot;
            texNoSaved++;
            *slot = &texNoAnms[idx];
          }
        }
      }
    }
  }

  // ============================================================================
  // Texture scroll animations + frames
  // ============================================================================
  J3DAnmTextureSRTKey *texScrollResData = DAPY_LK_MPTEXSCROLLRESDATA(link);
  J3DMaterialTable *matTable = (J3DMaterialTable *)J3DMODELDATA_MMATERIALTABLE(modelData);
  int texMtxSwapped = 0;
  {
    J3DTexMtxAnm *texMtxAnm = DAPY_LK_M_TEXMTXANM(link);
    u16 m3530 = DAPY_LK_M3530(link);
    u16 m3532 = DAPY_LK_M3532(link);

    if (texScrollResData && texMtxAnm)
    {
      J3DMaterialTable__setTexMtxAnimator(matTable, texScrollResData, texMtxAnm, NULL);
      texMtxSwapped = 1;
    }

    J3DAnmTexPattern *texPat = DAPY_LK_MPANMTEXPATTERNDATA(link);
    if (texPat)
    {
      J3DANMTEXPATTERN_MFRAME(texPat) = (f32)m3530;
    }
    if (texScrollResData)
    {
      J3DANMTEXTURESRTKEY_MFRAME(texScrollResData) = (f32)m3532;
    }
  }

  // Set light TEV color type
  dScnKy_env_light_c__setLightTevColorType(&g_env_light, mpCLModel, tevStr);

  // ============================================================================
  // Hand shapes, hat, hand model, j3dSys model/texture
  // ============================================================================
  J3DShape *lhandShape = J3DMaterial__getShape(J3DJoint__getMesh(J3DModelData__getJointNodePointer(modelData, CL_JNT_CL_LHANDA)));
  J3DShape *rhandShape = J3DMaterial__getShape(J3DJoint__getMesh(J3DModelData__getJointNodePointer(modelData, CL_JNT_CL_RHANDA)));
  J3DShape__hide(lhandShape);
  J3DShape__hide(rhandShape);

  {
    // Show material "ear(3)" (hat) = 5th material on link_root
    J3DMaterial *hatMtl = J3DJoint__getMesh(link_root_joint);
    for (i = 0; i < 4 && hatMtl; i++)
    {
      hatMtl = J3DMATERIAL_MPNEXTMATERIAL(hatMtl);
    }
    J3DShape__show(J3DMaterial__getShape(hatMtl));
  }

  // Hide every shared hands-model shape so only the puppet's own two hand shapes get
  // entered (see puppet_saveAndHideHandShapes). Restored after the hands entry below.
  PuppetHandShapeSave handShapeSave;
  puppet_saveAndHideHandShapes(DAPY_LK_MPHANDSMODEL(link), &handShapeSave);

  daPy_lk_c__setDrawHandModel(link);

  J3DSYS_MMODEL(&J3DGraphBase__j3dSys) = mpCLModel;
  J3DSYS_MTEXTURE(&J3DGraphBase__j3dSys) = J3DMODELDATA_MMATERIALTABLE_MPTEXTURE(modelData);
  J3DModel__unlock(mpCLModel);

  // ============================================================================
  // Main rendering branch - field_0x2b0 <= -85.0 hides the upper body
  // ============================================================================
  int r24 = (DAPY_PY_FIELD_0x2B0(actor) <= -85.0f);

  J3DShape **mpZOffBlendShape = DAPY_LK_MPZOFFBLENDSHAPE(link);
  J3DShape **mpZOffNoneShape = DAPY_LK_MPZOFFNONESHAPE(link);
  J3DShape **mpZOnShape = DAPY_LK_MPZONSHAPE(link);

  if (r24)
  {
    for (i = 0; i < 4; i++)
    {
      J3DShape__hide(mpZOffBlendShape[i]);
      J3DShape__hide(mpZOffNoneShape[i]);
      J3DShape__hide(mpZOnShape[i]);
    }
    J3DShape__hide(lhandShape);
    J3DShape__hide(rhandShape);

    // Only "ear(4)" (legs) stays visible
    J3DMaterial *mtl = J3DJoint__getMesh(link_root_joint);
    for (i = 0; mtl != NULL; i++, mtl = J3DMaterial__getNext(mtl))
    {
      if (i == 3)
        J3DShape__show(J3DMaterial__getShape(mtl));
      else
        J3DShape__hide(J3DMaterial__getShape(mtl));
    }
  }
  else
  {
    // (Vanilla's camera-attention 0x20 branch is skipped: local camera state.)
    if (frozenFlag == 0)
    {
      // ============================================================================
      // Multi-pass rendering for eyes and brows (OpaListP0 for both opa and xlu)
      // ============================================================================
      dComIfGd_setListP0();

      J3DDrawBuffer__entryImm(J3DSYS_MOPADRAWBUFFER(&J3DGraphBase__j3dSys), &packets->onCupOffAup2, 0);
      for (i = 0; i < 4; i++)
      {
        J3DShape__hide(mpZOffBlendShape[i]);
        J3DShape__hide(mpZOnShape[i]);
        J3DShape__show(mpZOffNoneShape[i]);
      }
      J3DJoint__entryIn(cl_eye_joint);
      J3DJoint__entryIn(cl_mayu_joint);

      J3DDrawBuffer__entryImm(J3DSYS_MOPADRAWBUFFER(&J3DGraphBase__j3dSys), &packets->offCupOnAup2, 0);
      for (i = 0; i < 4; i++)
      {
        J3DShape__show(mpZOffBlendShape[i]);
        J3DShape__hide(mpZOffNoneShape[i]);
      }
      J3DJoint__entryIn(cl_eye_joint);
      J3DJoint__entryIn(cl_mayu_joint);

      // Face + hair only
      J3DMaterial *mtl = J3DJoint__getMesh(link_root_joint);
      for (i = 0; mtl != NULL; i++, mtl = J3DMaterial__getNext(mtl))
      {
        if (i != 2 && i != 5)
        {
          J3DShape__hide(J3DMaterial__getShape(mtl));
        }
      }
      J3DJoint__entryIn(link_root_joint);

      if (daPy_lk_c__checkMaskDraw(link))
      {
        J3DModel *mpYamuModel = DAPY_LK_MPYAMUMODEL(link);
        if (mpYamuModel)
          daPy_lk_c__entryDLSetLight(link, mpYamuModel, 0);
      }

      J3DSYS_MMODEL(&J3DGraphBase__j3dSys) = mpCLModel;
      J3DSYS_MTEXTURE(&J3DGraphBase__j3dSys) = J3DMODELDATA_MMATERIALTABLE_MPTEXTURE(modelData);

      daPy_lk_c__hideHatAndBackle(link, J3DJoint__getMesh(link_root_joint));

      J3DDrawBuffer__entryImm(J3DSYS_MOPADRAWBUFFER(&J3DGraphBase__j3dSys), &packets->onCupOffAup1, 0);
      for (i = 0; i < 4; i++)
      {
        J3DShape__hide(mpZOffBlendShape[i]);
        J3DShape__show(mpZOnShape[i]);
        J3DShape__hide(mpZOffNoneShape[i]);
      }
      J3DJoint__entryIn(cl_eye_joint);
      J3DJoint__entryIn(cl_mayu_joint);

      J3DDrawBuffer__entryImm(J3DSYS_MOPADRAWBUFFER(&J3DGraphBase__j3dSys), &packets->offCupOnAup1, 0);
      for (i = 0; i < 4; i++)
      {
        J3DShape__hide(mpZOnShape[i]);
      }
    }
    else
    {
      daPy_lk_c__hideHatAndBackle(link, J3DJoint__getMesh(link_root_joint));
    }

    // Hero's Sword scabbard (cl_podA): vanilla shows it only with the Hero's Sword equipped
    // (d_a_player_main.cpp:1868-1878). Uses the peer's sword byte. The FF1 stage exception
    // and the sword-minigame case are not mirrored.
    {
      J3DShape *podaShape = J3DMaterial__getShape(J3DJoint__getMesh(J3DModelData__getJointNodePointer(modelData, CL_JNT_CL_PODA)));
      if (!remoteNormalSword || daPy_lk_c__checkCaughtShapeHide(link) || daPy_lk_c__checkDemoShieldNoDraw(link))
        J3DShape__hide(podaShape);
      else
        J3DShape__show(podaShape);
    }
  }

  // ============================================================================
  // Main body draw (P1 lists)
  // ============================================================================
  dComIfGd_setListP1();
  {
    // This Link's outfit + tunic colour: the linktexS3TC header is read by the diff below
    // (mDoExt_modelEntryDL -> J3DModel::diff -> loadTexNo), see puppet_appearance.h.
    PuppetLinktexSwap linktexSwap;
    puppet_appearance_begin(slotIndex, (noResetFlg1 & daPyFlg1_CASUAL_CLOTHES) != 0, lookBlock, &linktexSwap);

    if (frozenFlag == 0)
    {
      mDoExt_modelEntryDL(mpCLModel);
    }
    else
    {
      dMat_ice_c__entryDL(dMat_control_c__mIce, mpCLModel, -1, (void *)0);
    }

    // Undo everything written into the shared model data for this draw
    puppet_appearance_end(&linktexSwap);
    for (i = texNoSaved - 1; i >= 0; i--)
    {
      *texNoSlot[i] = texNoOld[i];
    }
    if (texMtxSwapped)
    {
      // Put the real Link's texMtx animator back (vanilla re-sets it every draw anyway;
      // this stops the shared materials pointing into the puppet's heap after it dies).
      if (DAPY_LK_MPCLMODELDATA(realPlayer) == modelData &&
          DAPY_LK_MPTEXSCROLLRESDATA(realPlayer) && DAPY_LK_M_TEXMTXANM(realPlayer))
      {
        J3DMaterialTable__setTexMtxAnimator(matTable, DAPY_LK_MPTEXSCROLLRESDATA(realPlayer),
                                            DAPY_LK_M_TEXMTXANM(realPlayer), NULL);
      }
    }
  }

  // Restore all link_root materials to visible
  {
    J3DMaterial *mtl = J3DJoint__getMesh(link_root_joint);
    while (mtl != NULL)
    {
      J3DShape__show(J3DMaterial__getShape(mtl));
      mtl = J3DMaterial__getNext(mtl);
    }
    J3DShape__show(J3DMaterial__getShape(J3DJoint__getMesh(J3DModelData__getJointNodePointer(modelData, CL_JNT_CL_HANA))));
    J3DShape__show(J3DMaterial__getShape(J3DJoint__getMesh(J3DModelData__getJointNodePointer(modelData, CL_JNT_CL_BACK))));
  }

  // ============================================================================
  // Equipment rendering (only if !r24)
  // ============================================================================
  {
    J3DModel *mpHandsModel = DAPY_LK_MPHANDSMODEL(link);
    if (!r24 && mpHandsModel)
    {
      daPy_lk_c__entryDLSetLight(link, mpHandsModel, frozenFlag);
    }
    // Give the real Link back its hands-model shape visibility (the puppet's two shapes
    // become hidden again unless the real Link had them visible).
    puppet_restoreHandShapes(&handShapeSave);
  }

  if (!r24)
  {

    // Katsura (casual clothes wig)
    if ((noResetFlg1 & daPyFlg1_CASUAL_CLOTHES) != 0 && !daPy_lk_c__checkCaughtShapeHide(link))
    {
      J3DModel *mpKatsuraModel = DAPY_LK_MPKATSURAMODEL(link);
      if (mpKatsuraModel)
      {
        daPy_lk_c__entryDLSetLight(link, mpKatsuraModel, frozenFlag);
      }
    }

    if (frozenFlag && daPy_lk_c__checkMaskDraw(link))
    {
      J3DModel *mpYamuModel = DAPY_LK_MPYAMUMODEL(link);
      if (mpYamuModel)
      {
        daPy_lk_c__entryDLSetLight(link, mpYamuModel, frozenFlag);
      }
    }

    // Power bracelets: vanilla draws them only when checkPowerGloveEquip() (select equip
    // slot 2). The sync slot has no bracelet byte, so the puppet never draws them.

    // Master Sword scabbard (podms): vanilla only for Master Sword 1/2/3 (0x39/0x3A/0x3E)
    if (remoteMasterSword && !daPy_lk_c__checkCaughtShapeHide(link) && !daPy_lk_c__checkDemoShieldNoDraw(link))
    {
      J3DModel *mpPodmsModel = DAPY_LK_MPPODMSMODEL(link);
      if (mpPodmsModel)
      {
        daPy_lk_c__updateDLSetLight(link, mpPodmsModel, frozenFlag);
      }
    }
  }

  // Heavy boots
  if ((DAPY_PY_NO_RESET_FLG0(actor) & daPyFlg0_HEAVY_BOOTS) != 0)
  {
    J3DModel **mpHbootsModels = DAPY_LK_MPHBOOTSMODELS(link);
    if (mpHbootsModels[0])
      daPy_lk_c__entryDLSetLight(link, mpHbootsModels[0], frozenFlag);
    if (mpHbootsModels[1])
      daPy_lk_c__entryDLSetLight(link, mpHbootsModels[1], frozenFlag);
  }

  // ============================================================================
  // Restore fog colors
  // ============================================================================
  TEVSTR_MFOGCOLOR_R(tevStr) = savedFogR;
  TEVSTR_MFOGCOLOR_G(tevStr) = savedFogG;
  TEVSTR_MFOGCOLOR_B(tevStr) = savedFogB;
  TEVSTR_MFOGSTARTZ(tevStr) = savedFogStartZ;
  TEVSTR_MFOGENDZ(tevStr) = savedFogEndZ;

  // ============================================================================
  // Additional equipment and effects (only if !r24)
  // ============================================================================
  if (!r24)
  {
    u32 mModeFlg = DAPY_LK_MMODEFLG(link);

    // Sword tip stab model (daPyProc_CUT_F_e 0x42 / daPyProc_BT_VERTICAL_JUMP_CUT_e 0x63)
    if (mCurProcID == 0x42 || mCurProcID == 0x63)
    {
      J3DModel *mpSwordTipStabModel = DAPY_LK_MPSWORDTIPSTABMODEL(link);
      if (mpSwordTipStabModel)
      {
        daPy_lk_c__updateDLSetLight(link, mpSwordTipStabModel, 0);
      }
    }
    // Water surface effect: ModeFlg_SWIM, daPyFlg0_UNK100, not (DEMO_DEAD && m34D6 == 0)
    else if ((mModeFlg & 0x40000) != 0 && (noResetFlg0 & daPyFlg0_UNK100) != 0 &&
             (mCurProcID != 0xb2 || DAPY_LK_M34D6(link) != 0))
    {
      J3DModel *mpSuimenMunyaModel = DAPY_LK_MPSUIMENMUNYA_MODEL(link);
      if (mpSuimenMunyaModel)
      {
        // (Vanilla also tints its materials with the sea colour; skipped.)
        mDoExt_modelUpdateDL(mpSuimenMunyaModel);
      }
    }

    // Equipped sword (peer's sword byte instead of dComIfGs_getSelectEquip(0))
    if (remoteHasSword && !daPy_lk_c__checkDemoSwordNoDraw(link, 1))
    {
      J3DModel *mpEquippedSwordModel = DAPY_LK_MPEQUIPPEDSWORDMODEL(link);
      if (mpEquippedSwordModel)
      {
        // Master Sword grip glow: setItemModel sets mpTswgripmsabBrk frame 1.0 (Full Power)
        // or 0.0 (d_a_player_main.cpp:1224-1250), but that brk is the SHARED archive
        // resource (entryBrk -> dComIfG_getObjectRes, :12095) and the material anim is
        // evaluated at entry time, so the last Link to execute wins. Re-apply the peer's
        // value just for the puppet's entry.
        f32 *brkFrame = NULL;
        f32 savedBrkFrame = 0.0f;
        void *gripBrk = DAPY_LK_MPTSWGRIPMSABBRK(link);
        if (gripBrk != NULL && remoteMasterSword)
        {
          brkFrame = (f32 *)((u8 *)gripBrk + 0x08); // J3DAnmBase::mFrame
          savedBrkFrame = *brkFrame;
          *brkFrame = remoteFinalMasterSword ? 1.0f : 0.0f;
        }
        daPy_lk_c__entryDLSetLight(link, mpEquippedSwordModel, frozenFlag);
        if (brkFrame != NULL)
          *brkFrame = savedBrkFrame;
      }
    }

    // Equipped shield (peer's shield byte)
    if (remoteHasShield && !daPy_lk_c__checkCaughtShapeHide(link) && !daPy_lk_c__checkDemoShieldNoDraw(link))
    {
      J3DModel *mpEquippedShieldModel = DAPY_LK_MPEQUIPPEDSHIELDMODEL(link);
      if (mpEquippedShieldModel)
      {
        daPy_lk_c__entryDLSetLight(link, mpEquippedShieldModel, frozenFlag);
      }
    }

    // Mirror light model - default lists, then back to P1 (vanilla)
    dComIfGd_setList();
    daPy_lk_c__drawMirrorLightModel(link);
    dComIfGd_setListP1();

    // The held item's / bottle contents' btk and brk are registered on SHARED model data: use this
    // puppet's for its entries below, the local Link's again after (puppet_held.c).
    J3DModel *mpBottleContentsModel = DAPY_LK_MPBOTTLECONTENTSMODEL(link);
    J3DModelData *heldItemData = DAPY_LK_MPHELDITEMMODEL(link) ? J3DMODEL_MPMODELDATA(DAPY_LK_MPHELDITEMMODEL(link)) : NULL;
    J3DModelData *contentsData = mpBottleContentsModel ? J3DMODEL_MPMODELDATA(mpBottleContentsModel) : NULL;
    puppet_heldEntryItemAnms(link, heldItemData, contentsData);

    // Bottle contents model
    if (mpBottleContentsModel)
    {
      daPy_lk_c__updateDLSetLight(link, mpBottleContentsModel, 0);
    }

    // Held item model
    J3DModel *mpHeldItemModel = DAPY_LK_MPHELDITEMMODEL(link);
    if (mpHeldItemModel != 0 && !daPy_lk_c__checkCaughtShapeHide(link) &&
        !daPy_lk_c__checkDemoSwordNoDraw(link, 0))
    {
      u16 mEquipItem = DAPY_LK_MEQUIPITEM(link);

      if (!daPy_lk_c__checkBowItem(link, mEquipItem) || !daPy_lk_c__checkPlayerGuard(link))
      {
        if (mEquipItem == PUPPET_ITEM_HOOKSHOT)
        {
          // mpEquipItemModel->setAnmMtx(HOOKSHOT_JNT_HSHOTTIP_e (4), hookshot->getMtxTop())
          // J3DModel::mpNodeMtx is a POINTER at +0x8C; daHookshot_c mtxTop at +0x51C
          // (DOL 0x80107FFC-0x80108018)
          fopAc_ac_c *hookActor = DAPY_LK_MACTORKEEPEQUIP_MPACTOR(link);
          MTX34 *nodeMtx = *(MTX34 **)((u8 *)mpHeldItemModel + 0x8C);
          if (hookActor != 0 && nodeMtx != 0)
          {
            PSMTXCopy((MTX34 *)((u8 *)hookActor + 0x51C), &nodeMtx[4]);
          }
        }
        // Held Master Sword blade: setItemModel shows SWA_JNT_CL_SWA (joint 0) for Full
        // Power and hides it otherwise (d_a_player_main.cpp:1236-1260), on the SHARED blade
        // model data; J3DShpFlag_Hide is read at entry (J3DJoint::entryIn). Apply the peer's
        // state for the puppet's entry only.
        J3DShape *bladeShape = NULL;
        u32 savedBladeFlags = 0;
        if (mEquipItem == 0x103 && remoteMasterSword) // daPyItem_SWORD_e
        {
          J3DModelData *bladeData = J3DMODEL_MPMODELDATA(mpHeldItemModel);
          bladeShape = J3DMaterial__getShape(J3DJoint__getMesh(J3DModelData__getJointNodePointer(bladeData, 0)));
          if (bladeShape != NULL)
          {
            savedBladeFlags = J3DSHAPE_MVISFLAGS(bladeShape);
            if (remoteFinalMasterSword)
              J3DShape__show(bladeShape);
            else
              J3DShape__hide(bladeShape);
          }
        }
        // Picto Box flash shape: this puppet's regular / Deluxe choice, entry only (puppet_held.c).
        u32 savedFlashFlags = 0;
        J3DShape *flashShape = puppet_heldFlashBegin(link, &savedFlashFlags);
        daPy_lk_c__entryDLSetLight(link, mpHeldItemModel, frozenFlag);
        if (bladeShape != NULL)
          J3DSHAPE_MVISFLAGS(bladeShape) = savedBladeFlags;
        if (flashShape != NULL)
          J3DSHAPE_MVISFLAGS(flashShape) = savedFlashFlags;

        // Sword glow (vanilla :1987-1989: chance mode || soup || checkFinalMasterSwordEquip;
        // the puppet uses the peer's sword byte). updateDLSetLight calcs the glow model, whose
        // SHARED model data joint 0 must carry the puppet's mSwordAnim at this blade frame
        // (puppet_execute restores joint 0 after setItemModel), so entry it here and restore.
        J3DModel *mpSwordModel1 = DAPY_LK_MPSWORDMODEL1(link);
        if (mpSwordModel1)
        {
          if (daPy_lk_c__checkChanceMode(link) || (noResetFlg1 & daPyFlg1_SOUP_POWER_UP) != 0 ||
              remoteFinalMasterSword)
          {
            J3DModelData *glowData = J3DMODEL_MPMODELDATA(mpSwordModel1);
            J3DJoint *glowJoint0 = J3DModelData__getJointNodePointer(glowData, 0);
            void *savedGlowCalc = glowJoint0 ? *(void **)((u8 *)glowJoint0 + 0x58) : NULL;
            mDoExt_bckAnm *swordAnim = (mDoExt_bckAnm *)((u8 *)link + 0x2E9C); // mSwordAnim
            if (glowJoint0 != NULL && *(void **)((u8 *)swordAnim + 0x08) != NULL &&  // mpAnm
                *(void **)((u8 *)swordAnim + 0x0C) != NULL)                            // mAnm
            {
              mDoExt_bckAnm__entry(swordAnim, glowData, PUPPET_DAPY_M35EC(link));
            }
            daPy_lk_c__updateDLSetLight(link, mpSwordModel1, 0);
            if (glowJoint0 != NULL)
              *(void **)((u8 *)glowJoint0 + 0x58) = savedGlowCalc;
          }
        }
      }
    }

    // Bottle cap model
    J3DModel *mpBottleCapModel = DAPY_LK_MPBOTTLECAPMODEL(link);
    if (mpBottleCapModel != 0 && DAPY_LK_M355E(link) != 0)
    {
      daPy_lk_c__updateDLSetLight(link, mpBottleCapModel, 0);
    }
    puppet_heldEntryItemAnms(realPlayer, heldItemData, contentsData);

    // Magic armor aura (6 models, daPy_aura_c = {J3DModel*, f32 frame})
    void *auraBrkAnm = DAPY_LK_MYAURA00RBRK_ANM(link);
    if (auraBrkAnm != 0)
    {
      f32 frame = *(f32 *)((u8 *)auraBrkAnm + 0x08); // J3DAnmBase::mFrame
      if (frame > 0.0f)
      {
        u8 *auraEntries = (u8 *)DAPY_LK_MMAGICARARMORAURAENTRIES(link);
        J3DModel *aura0 = *(J3DModel **)auraEntries;
        void *auraBtk = DAPY_LK_MPYAURA00BTK(link);
        if (aura0)
        {
          mDoExt_brkAnm__entry((mDoExt_brkAnm *)DAPY_LK_MYAURA00RBRK(link), J3DMODEL_MPMODELDATA(aura0), frame);
        }
        for (i = 0; i < 6; i++)
        {
          J3DModel *auraModel = *(J3DModel **)(auraEntries + i * 8);
          if (!auraModel)
            continue;
          if (auraBtk)
          {
            *(f32 *)((u8 *)auraBtk + 0x08) = *(f32 *)(auraEntries + i * 8 + 4);
          }
          daPy_lk_c__updateDLSetLight(link, auraModel, 0);
        }
      }
    }

    // Magic circle effect (mYmgcs00)
    void *ymgcsBrkAnm = DAPY_LK_MYMGCS00BRK_ANM(link);
    if (ymgcsBrkAnm != 0)
    {
      f32 frame2 = *(f32 *)((u8 *)ymgcsBrkAnm + 0x08);
      J3DModel *mpYmgcs00Model = DAPY_LK_MPYMGCS00MODEL(link);
      if (frame2 > 0.0f && mpYmgcs00Model)
      {
        mDoExt_brkAnm__entry((mDoExt_brkAnm *)DAPY_LK_MYMGCS00BRK(link), J3DMODEL_MPMODELDATA(mpYmgcs00Model), frame2);
        mDoExt_modelEntryDL(mpYmgcs00Model);
      }
    }
  }

  // ============================================================================
  // Fan wind effects
  // ============================================================================
  if (daPy_lk_c__fanWindEffectDraw(link))
  {
    J3DModel *mpYuchw00Model = DAPY_LK_MPYUCHW00MODEL(link);
    if (mpYuchw00Model)
    {
      daPy_lk_c__updateDLSetLight(link, mpYuchw00Model, 0);
    }
  }

  if (daPy_lk_c__fanWindCrashEffectDraw(link))
  {
    J3DModel *mpYbafo00Model = DAPY_LK_MPYBAFO00MODEL(link);
    if (mpYbafo00Model)
    {
      daPy_lk_c__updateDLSetLight(link, mpYbafo00Model, 0);
    }
  }

  // ============================================================================
  // Shadow - default lists; this is also the vanilla end state of the draw lists
  // ============================================================================
  dComIfGd_setList();

  // mCurProc != daPyProc_DEMO_CAUGHT_e && !dComIfGp_checkPlayerStatus0(0, daPyStts0_SHIP_RIDE_e)
  u32 playerStatus = GAMEINFO_PLAYERSTATUS(&g_dComIfG_gameInfo);
  if (mCurProcID != 0xBD && (playerStatus & 0x10000) == 0)
  {
    daPy_lk_c__drawShadow(link);
  }

  return 1;
}

/**
 * puppet_draw - draw with the peer's sword/shield ids in the save's select-equip bytes, so the
 * daPy_lk_c helpers the draw calls (setDrawHandModel -> checkShieldEquip, checkChanceMode,
 * checkNormalSwordEquip for the sheath shape, ...) see the peer's gear. Execute does the same
 * through its global guard (puppet_execute.c PuppetEquipSwap). The swap publishes
 * PUPPET_EQUIP_SWAP_ACTIVE_ADDR / _SEQ_ADDR for the C# client and only restores bytes C#
 * didn't change meanwhile.
 */
int puppet_draw(daPy_lk_c *link, u32 slotIndex, PuppetDrawPackets *packets, u8 **lookBlock)
{
  PuppetEquipSwap equip;
  int ret;

  puppet_equipSwapBegin(link, &equip);
  ret = puppet_drawBody(link, slotIndex, packets, lookBlock);
  puppet_equipSwapEnd(&equip);
  return ret;
}
