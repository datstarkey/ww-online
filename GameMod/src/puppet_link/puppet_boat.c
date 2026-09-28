/**
 * puppet_boat.c - the peer's King of Red Lions, drawn by their puppet (see puppet_boat.h).
 *
 * Per frame (puppet_boatExecute, before puppet_execute):
 *   1. Read the slot's boat block. FLAGS == 0 (or an inactive slot) hides the boat.
 *   2. On first use, load the "Ship" archive and build the hull + head models in our own heap.
 *   3. Predict the network position along the heading (dead reckoning: the sample is up to one
 *      20Hz tick old and a sailing boat covers ~55 units a frame), then ease toward it.
 *   4. Height and tilt from the LOCAL sea, like daShip_c::setYPos / setWaveAngle. The peer's Y
 *      comes from their sea, whose waves don't line up with ours, so it's only used in the air.
 *   5. Hull matrix = T(pos) * ZXYrot(pitch, yaw, roll); the head sits on the hull's GATTAI joint.
 *   6. Pose both morfs like daShip_c::execute (d_a_ship.cpp:3960-3976): the peer's mast / head
 *      bck at the peer's frame, then our joint callback (boat_jointCallBack) for the sail,
 *      tiller, mast scale and head look.
 *
 * The models share the "Ship" J3DModelData with the local King of Red Lions, and daShip_c
 * puts its joint callbacks on those shared joints (d_a_ship.cpp:4577-4619). They find their
 * ship through J3DModel::getUserArea(), which is 0 for our models, and the head callbacks don't
 * check it. So around our calc we swap every joint callback for ours and put them back after.
 *
 * REL: literals are fine here.
 */

#include "puppet_boat.h"

/* "Ship" archive (tww-decomp assets/GZLE01/res/Object/Ship.h) */
#define BOAT_ARC_NAME             "Ship"
#define BOAT_RES_BDL_FN_BODY      0x11  /* dRes_INDEX_SHIP_BDL_FN_BODY_e */
#define BOAT_RES_BDL_FN_HEAD_H    0x12  /* dRes_INDEX_SHIP_BDL_FN_HEAD_H_e */
#define BOAT_JNT_GATTAI           0x04  /* FN_BODY_JNT_J_FN_GATTAI_e: the head's base (d_a_ship.cpp:4743) */
#define BOAT_JNT_KAJI             0x05  /* FN_BODY_JNT_J_FN_KAJI_e: rudder */
#define BOAT_JNT_MAST             0x06  /* FN_BODY_JNT_J_FN_MAST_e */
#define BOAT_JNT_SAIL1            0x07  /* FN_BODY_JNT_J_FN_SAIL1_e: sail yard */
#define BOAT_JNT_STEER1           0x0A  /* FN_BODY_JNT_J_FN_STEER1_e: tiller */
#define BOAT_HEAD_JNT_KUBI1       0x02  /* FN_HEAD_H_JNT_J_FN_KUBI1_e .. KUBI6_e: the neck */
#define BOAT_HEAD_JNT_KUBI6       0x07

/* daShip_c's morfs (createHeap, d_a_ship.cpp:4406-4467; setPartOnAnime/OffAnime :1328-1360;
 * setHeadAnm :3523-3538): J3DFrameCtrl::EMode_NONE, morf 3 for the mast, 5 for the head (0 for
 * DAMAGE1). Our play speed is 0: the frame comes from the peer. m03E8 is 1.0 or 0.001. */
#define BOAT_BCK_MODE_NONE        0
#define BOAT_MAST_MORF            3.0f
#define BOAT_HEAD_MORF            5.0f
#define BOAT_MAST_HIDE_SCALE      0.001f
#define BOAT_CALC_TIMING_IN       0     /* J3DNodeCBCalcTiming_In (J3DNode.h) */

/* Model flags as daShip_c::createHeap (d_a_ship.cpp:4406-4461) */
#define BOAT_MODEL_FLAG           0x80000
#define BOAT_BODY_DIFF_FLAG       0x11200202
#define BOAT_HEAD_DIFF_FLAG       0x11000002

#define BOAT_HEAP_SIZE            0x20000  /* daShip_c's own heap size; adjusted down once built */
#define BOAT_HEAP_ALIGN           0x20
#define BOAT_MAX_JOINTS           64       /* hook save buffers; FN_BODY has 11 joints, FN_HEAD_H 18 */

/* J3DSkinDeform (J3DSkinDeform.h, "Size: 0x18"); setSkinDeform flags as daShip_c (d_a_ship.cpp:4420):
 * bit 2 / bit 4 clear = CPU-skin positions / normals (J3DModel.cpp:459-480). */
#define BOAT_SKIN_DEFORM_SIZE     0x18
#define BOAT_SKIN_DEFORM_FLAGS    1

/* c_phase.h cPhs_State (ww_structs.h's values are wrong, see puppet.c) */
#define BOAT_PHS_COMPLEATE        4
#define BOAT_PHS_ERROR            5
/* request_of_phase_process_class id while dComIfG_resLoad runs: 1 loading, 2 loaded
 * (d_com_inf_game.cpp:551-575; dComIfG_resDelete asserts it isn't called while loading). */
#define BOAT_PHASE_LOADING        1
#define BOAT_PHASE_LOADED         2

/* dKy_tevstr_c (d_kankyo.h) */
#define TEVSTR_OFF_ROOM_NO        0xA9  /* s8 mRoomNo */
#define TEVSTR_OFF_ENVR_OVERRIDE  0xAA  /* u8 mEnvrIdxOverride */

/* Motion */
#define BOAT_MAX_PREDICT_FRAMES   10         /* stop extrapolating a stalled sample after this */
#define BOAT_SNAP_DIST_SQ         (1000.0f * 1000.0f)
#define BOAT_POS_EASE             0.3f
#define BOAT_MAX_SPEED            1000.0f    /* reject garbage speedF */
#define BOAT_MAX_COORD            1.0e6f

/* Seat: d_a_player_main_data.inc l_ship_offset, the point setShipRidePos pins Link to on the
 * hull matrix (d_a_player_ship.inc:165-239). */
static const cXyz l_boatSeatOffset = {0.0f, 15.0f, -35.0f};

/* daShip_c::setWaveAngle sample points (d_a_ship.cpp:833-836). The ship measures the height
 * difference over the points' horizontal distance (absXZ); the points are only yawed here,
 * so that distance is the constant span below. */
static const cXyz l_boatWaveFront = {0.0f, 0.0f, 180.0f};
static const cXyz l_boatWaveBack = {0.0f, 0.0f, -190.0f};
static const cXyz l_boatWaveRight = {-80.0f, 0.0f, 0.0f};
static const cXyz l_boatWaveLeft = {80.0f, 0.0f, 0.0f};
#define BOAT_WAVE_SPAN_FB         370.0f
#define BOAT_WAVE_SPAN_RL         160.0f

void puppet_boatInit(PuppetBoat *boat)
{
  u8 *p = (u8 *)boat;
  unsigned int i;
  for (i = 0; i < sizeof(PuppetBoat); i++)
    p[i] = 0;
}

static int boat_finite(f32 v, f32 limit)
{
  return v == v && v > -limit && v < limit;
}

/* daShip_c::getMaxWaterY without the Acch water check (d_a_ship.cpp:814-829): the sea's wave
 * height where there is sea, else the caller's fallback. */
static f32 boat_waterY(f32 x, f32 z, f32 fallbackY)
{
  if (d_a_sea__daSea_ChkArea(x, z))
    return d_a_sea__daSea_calcWave(x, z);
  return fallbackY;
}

/* The hull must be CPU-skinned like the local ship's. J3DModel::prepareShapePackets (in
 * viewCalc, i.e. mDoExt_modelEntryDL) writes the model's SkinPosCpu/SkinNrmCpu into the SHARED
 * J3DShape flags (J3DModel.cpp:917-928), and the shape reads them back at GX draw time. A hull
 * without skin deform would flip the local ship's shapes to the GPU path (garbage: its draw
 * matrices aren't computed) or draw ours at the origin, whichever entered last. */
static int boat_addSkinDeform(J3DModel *body)
{
  J3DSkinDeform *skin = (J3DSkinDeform *)operator_new(BOAT_SKIN_DEFORM_SIZE);
  if (skin == NULL)
    return 0;
  J3DSkinDeform__J3DSkinDeform(skin);
  // J3DErrType_Success == 0 (the prototype's `undefined` return is the low byte of the int)
  return J3DModel__setSkinDeform(body, skin, BOAT_SKIN_DEFORM_FLAGS) == 0;
}

/* A "Ship" archive resource (dComIfG_getObjectRes). */
static void *boat_getRes(u32 index)
{
  return dRes_control_c__getRes(BOAT_ARC_NAME, index, GAMEINFO_RES_OBJECT_INFO(&g_dComIfG_gameInfo),
                                GAMEINFO_RES_OBJECT_INFO_COUNT);
}

/* new mDoExt_McaMorf(data, NULL, NULL, bck, EMode_NONE, 0.0f, 0, -1, 0, NULL, flags) in the
 * current heap, as daShip_c::createHeap does for mpBodyAnm / mpHeadAnm. NULL on failure. */
static mDoExt_McaMorf *boat_createMorf(u32 bdl, u32 bck, u32 diffFlag)
{
  J3DModelData *data = (J3DModelData *)boat_getRes(bdl);
  void *anm = boat_getRes(bck);
  mDoExt_McaMorf *morf;

  if (data == NULL || anm == NULL)
    return NULL;
  morf = (mDoExt_McaMorf *)operator_new(MDOEXT_MCAMORF_SIZE);
  if (morf == NULL)
    return NULL;
  // 1: this is the complete object, so the ctor builds the virtual J3DMtxCalc base.
  mDoExt_McaMorf__mDoExt_McaMorf(morf, 1, data, NULL, NULL, (J3DAnmTransformKey *)anm, BOAT_BCK_MODE_NONE,
                                 0.0f, 0, -1, 0, NULL, BOAT_MODEL_FLAG, diffFlag);
  return MDOEXT_MCAMORF_MPMODEL(morf) != NULL ? morf : NULL;
}

/* Build the hull and head in a solid heap of our own (the puppet's actor heap belongs to
 * playerInit). As daShip_c::createHeap, minus the cannon, crane and rope. */
static int boat_createModels(PuppetBoat *boat)
{
  JKRSolidHeap *heap;

  heap = mDoExt_createSolidHeapFromGameToCurrent(BOAT_HEAP_SIZE, BOAT_HEAP_ALIGN);
  if (heap == NULL)
    return 0;

  // All allocations (morfs, models, skin deform, its transformed vertex arrays) come from `heap`.
  boat->bodyAnm = boat_createMorf(BOAT_RES_BDL_FN_BODY, SHIP_BCK_MAST_OFF2, BOAT_BODY_DIFF_FLAG);
  boat->headAnm = NULL;
  if (boat->bodyAnm != NULL && !boat_addSkinDeform(MDOEXT_MCAMORF_MPMODEL(boat->bodyAnm)))
    boat->bodyAnm = NULL;
  if (boat->bodyAnm != NULL)
    boat->headAnm = boat_createMorf(BOAT_RES_BDL_FN_HEAD_H, SHIP_BCK_FN_LOOK_L, BOAT_HEAD_DIFF_FLAG);
  mDoExt_restoreCurrentHeap();

  if (boat->bodyAnm == NULL || boat->headAnm == NULL)
  {
    mDoExt_destroySolidHeap(heap);
    boat->bodyAnm = NULL;
    boat->headAnm = NULL;
    return 0;
  }

  // As daShip_c::createHeap (d_a_ship.cpp:4467): the ctor leaves mPrevMorf at -1, which makes
  // the next setMorf snap; spend that here so the head's first bck change blends too.
  mDoExt_McaMorf__setMorf(boat->headAnm, BOAT_HEAD_MORF);
  boat->body = MDOEXT_MCAMORF_MPMODEL(boat->bodyAnm);
  boat->head = MDOEXT_MCAMORF_MPMODEL(boat->headAnm);
  boat->mastBck = SHIP_BCK_MAST_OFF2;
  boat->headBck = SHIP_BCK_FN_LOOK_L;

  mDoExt_adjustSolidHeap(heap);
  boat->heap = heap;
  return 1;
}

/* Load the archive (async, polled every frame) and build the models once. */
static int boat_ready(fopAc_ac_c *actor, PuppetBoat *boat)
{
  int phs;

  if (boat->state == PUPPET_BOAT_STATE_READY)
    return 1;
  if (boat->state == PUPPET_BOAT_STATE_FAILED)
    return 0;

  phs = dComIfG_resLoad(&boat->phase, BOAT_ARC_NAME);
  if (phs == BOAT_PHS_ERROR)
  {
    OSReport("[PUPPET] boat: Ship archive load failed\n");
    boat->state = PUPPET_BOAT_STATE_FAILED;
    return 0;
  }
  if (phs != BOAT_PHS_COMPLEATE)
    return 0;

  if (!boat_createModels(boat))
  {
    OSReport("[PUPPET] boat: model build failed\n");
    boat->state = PUPPET_BOAT_STATE_FAILED;
    return 0;
  }

  // dKy_tevstr_init takes an s8 room (d_kankyo.cpp:3383, compared to -1); ww_functions.h
  // declares it `char`, which GCC-PPC passes zero-extended, so call it with the real type.
  ((void (*)(dKy_tevstr_c *, s8, u8))dKy_tevstr_init)((dKy_tevstr_c *)boat->tevStr,
                                                      FOPAC_CURRENT_ROOMNO(actor), 0xFF);
  boat->state = PUPPET_BOAT_STATE_READY;
  OSReport("[PUPPET] boat: ready\n");
  return 1;
}

/* Point a morf at a "Ship" bck, as daShip_c's setAnm calls but with play speed 0 (we set the
 * frame from the peer). Returns 0 if the bck isn't there. */
static int boat_setBck(mDoExt_McaMorf *morf, u32 bck, f32 morfFrames)
{
  void *anm = boat_getRes(bck);
  if (anm == NULL)
    return 0;
  mDoExt_McaMorf__setAnm(morf, (J3DAnmTransform *)anm, BOAT_BCK_MODE_NONE, morfFrames, 0.0f, 0.0f, -1.0f, NULL);
  return 1;
}

/* The peer's whole-frame position in the bck, capped at its end (J3DFrameCtrl mEnd); the play()
 * after it pulls the end frame back to end - 0.001, where the peer's finished bck sits too. */
static void boat_setFrame(mDoExt_McaMorf *morf, u8 frame)
{
  s16 end = MDOEXT_MCAMORF_FRAME_END(morf);
  MDOEXT_MCAMORF_FRAME(morf) = (f32)(frame < end ? frame : end);
}

/* The boat being calculated and which of its models, for boat_jointCallBack (actors run
 * single-threaded; NULL outside boat_calcModel). */
static PuppetBoat *l_boatCalc = NULL;
static u8 l_boatCalcHead = 0;

/* daShip_c's joint callbacks, with the peer's values and our drawn yaw:
 *   hull (bodyJointCallBack, d_a_ship.cpp:110-131): anm = anm * Zrot(m0366) on J_FN_STEER1 /
 *     J_FN_KAJI, anm * Zrot(-mSailAngle) on J_FN_SAIL1, anm * scale(m03E8) on J_FN_MAST (the
 *     cannon / crane placement it also does there is skipped: we draw neither);
 *   head (headJointCallBack1, :214-231) on J_FN_KUBI1..6: anm = Yrot(y) * ZXYrot(m03A0, m03A2, 0)
 *     * Yrot(-y) * anm, y = shape_angle.y + m03A2 * (jno - 2), keeping anm's translation.
 * Either way the result goes back to J3DSys::mCurrentMtx for the joint's children. */
static int boat_jointCallBack(J3DNode *node, int timing)
{
  PuppetBoat *boat = l_boatCalc;
  int jno = J3DJOINT_MJNTNO(node);
  MTX34 *anm;
  MTX34 m;
  f32 tx, ty, tz;
  s16 yaw;

  if (timing != BOAT_CALC_TIMING_IN || boat == NULL)
    return 1;

  if (l_boatCalcHead)
  {
    if (jno < BOAT_HEAD_JNT_KUBI1 || jno > BOAT_HEAD_JNT_KUBI6)
      return 1;
    anm = &J3DMODEL_MPNODEMTX(boat->head)[jno];
    tx = anm->m[0][3];
    ty = anm->m[1][3];
    tz = anm->m[2][3];
    yaw = (s16)(boat->rotY + boat->headY * (jno - BOAT_HEAD_JNT_KUBI1));
    mDoMtx_YrotS(&m, yaw);
    mDoMtx_ZXYrotM((undefined4)&m, boat->headX, boat->headY, 0);
    mDoMtx_YrotM(&m, (s16)-yaw);
    PSMTXConcat(&m, anm, &m);
    m.m[0][3] = tx;
    m.m[1][3] = ty;
    m.m[2][3] = tz;
    PSMTXCopy(&m, anm);
  }
  else
  {
    anm = &J3DMODEL_MPNODEMTX(boat->body)[jno];
    if (jno == BOAT_JNT_STEER1 || jno == BOAT_JNT_KAJI)
      mDoMtx_ZrotS(&m, boat->tiller);
    else if (jno == BOAT_JNT_SAIL1)
      mDoMtx_ZrotS(&m, (s16)-boat->sailAngle);
    else if (jno == BOAT_JNT_MAST && boat->mastHide)
      PSMTXScale(BOAT_MAST_HIDE_SCALE, BOAT_MAST_HIDE_SCALE, BOAT_MAST_HIDE_SCALE, &m);
    else
      return 1; // no callback on this joint in daShip_c (or m03E8 == 1: identity)
    PSMTXConcat(anm, &m, anm);
  }
  PSMTXCopy(anm, &J3DSys__mCurrentMtx);
  return 1;
}

/* mDoExt_McaMorf::calc with the local ship's hooks on the (shared) joints swapped out: every
 * joint callback becomes ours and every joint's mMtxCalc is cleared. mDoExt_McaMorf::calc sets
 * joint 0's mMtxCalc to the calling morf and never clears it (m_Do_ext.cpp:1540-1549), so the
 * local ship's morf is left there; J3DJoint::calcIn would run it for our model too
 * (J3DJoint.cpp:212-214), animating our boat with the local ship's anim or calling a freed
 * object once the local ship is gone. Our morf's calc puts itself on joint 0 instead. */
static void boat_calcModel(PuppetBoat *boat, mDoExt_McaMorf *morf, u8 isHead)
{
  J3DModelData *data = J3DMODEL_MPMODELDATA(MDOEXT_MCAMORF_MPMODEL(morf));
  J3DJoint **joints;
  void *savedCallBack[BOAT_MAX_JOINTS];
  void *savedMtxCalc[BOAT_MAX_JOINTS];
  int count;
  int i;

  if (data == NULL)
    return;
  joints = J3DMODELDATA_MJOINTTREE_MPJOINTS(data);
  count = J3DMODELDATA_MJOINTTREE_JOINTNUM(data);
  if (joints == NULL || count > BOAT_MAX_JOINTS)
    return; // can't protect the local ship's hooks: leave the model unposed

  for (i = 0; i < count; i++)
  {
    savedCallBack[i] = J3DNODE_MCALLBACK(joints[i]);
    savedMtxCalc[i] = J3DJOINT_MMTXCALC(joints[i]);
    J3DNODE_MCALLBACK(joints[i]) = (void *)boat_jointCallBack;
    J3DJOINT_MMTXCALC(joints[i]) = NULL;
  }

  l_boatCalc = boat;
  l_boatCalcHead = isHead;
  mDoExt_McaMorf__calc(morf);
  l_boatCalc = NULL;

  for (i = 0; i < count; i++)
  {
    J3DNODE_MCALLBACK(joints[i]) = savedCallBack[i];
    J3DJOINT_MMTXCALC(joints[i]) = savedMtxCalc[i];
  }
}

/* daShip_c::setWaveAngle (d_a_ship.cpp:832-873): pitch from the front/back wave heights,
 * roll from right/left. */
static void boat_waveAngles(PuppetBoat *boat, s16 *pitch, s16 *roll)
{
  MTX34 m;
  cXyz front, back, right, left;

  mDoMtx_YrotS(&m, boat->rotY);
  m.m[0][3] = boat->pos.x;
  m.m[1][3] = boat->pos.y;
  m.m[2][3] = boat->pos.z;
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveFront, &front);
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveBack, &back);
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveRight, &right);
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveLeft, &left);

  front.y = boat_waterY(front.x, front.z, boat->pos.y);
  back.y = boat_waterY(back.x, back.z, boat->pos.y);
  right.y = boat_waterY(right.x, right.z, boat->pos.y);
  left.y = boat_waterY(left.x, left.z, boat->pos.y);

  *pitch = (s16)cM_atan2s(-(front.y - back.y), BOAT_WAVE_SPAN_FB);
  *roll = (s16)cM_atan2s(-(right.y - left.y), BOAT_WAVE_SPAN_RL);
}

/* The peer's mast / head bcks, frames and joint angles for this frame's calc. A bck change
 * blends like daShip_c's setAnm calls (mast 3 frames, head 5, DAMAGE1 none); a boat that has
 * just appeared (no pose yet) takes it without blending. Speed 0: the frame is the peer's. */
static void boat_animate(PuppetBoat *boat, u32 base, u32 flags)
{
  u32 mastBck = (flags & PUPPET_BOAT_FLAG_MAST_ON) ? SHIP_BCK_MAST_ON2 : SHIP_BCK_MAST_OFF2;
  u32 headBck = (flags >> PUPPET_BOAT_HEAD_BCK_SHIFT) & PUPPET_BOAT_HEAD_BCK_MASK;

  if (mastBck != boat->mastBck &&
      boat_setBck(boat->bodyAnm, mastBck, boat->hasPose ? BOAT_MAST_MORF : 0.0f))
    boat->mastBck = (u8)mastBck;

  // Head bcks only (never the mast's); 0 or anything else keeps the current one.
  if (headBck >= SHIP_BCK_FIRST && headBck <= SHIP_BCK_LAST && headBck != SHIP_BCK_MAST_OFF2 &&
      headBck != SHIP_BCK_MAST_ON2 && headBck != boat->headBck &&
      boat_setBck(boat->headAnm, headBck,
                  (boat->hasPose && headBck != SHIP_BCK_DAMAGE1) ? BOAT_HEAD_MORF : 0.0f))
    boat->headBck = (u8)headBck;

  boat_setFrame(boat->bodyAnm, *(volatile u8 *)(base + PUPPET_BOAT_OFF_MAST_FRAME));
  boat_setFrame(boat->headAnm, *(volatile u8 *)(base + PUPPET_BOAT_OFF_HEAD_FRAME));
  // play() only steps the blend here (J3DFrameCtrl rate 0), as execute does (d_a_ship.cpp:3605-3606).
  mDoExt_McaMorf__play(boat->bodyAnm, NULL, 0, 0);
  mDoExt_McaMorf__play(boat->headAnm, NULL, 0, 0);

  boat->mastHide = (flags & PUPPET_BOAT_FLAG_MAST_HIDE) != 0;
  boat->sailAngle = *(volatile s16 *)(base + PUPPET_BOAT_OFF_SAIL_ANGLE);
  boat->tiller = *(volatile s16 *)(base + PUPPET_BOAT_OFF_TILLER);
  boat->headX = *(volatile s16 *)(base + PUPPET_BOAT_OFF_HEAD_X);
  boat->headY = *(volatile s16 *)(base + PUPPET_BOAT_OFF_HEAD_Y);
}

void puppet_boatExecute(fopAc_ac_c *actor, PuppetBoat *boat, u32 slotIndex)
{
  u32 base;
  u32 flags;
  cXyz net;
  f32 speedF;
  s16 netRotY;
  MTX34 m;
  cXyz ahead, offset;
  f32 targetX, targetZ, targetY, dx, dz;
  int flying, onSea;
  s16 pitchTarget = 0;
  s16 rollTarget = 0;
  MTX34 *bodyMtx;

  boat->visible = 0;

  if (slotIndex >= PUPPET_MAX_SLOTS || *(volatile u32 *)PUPPET_SYNC_BASE != PUPPET_SYNC_MAGIC ||
      *(volatile u32 *)(PUPPET_SLOT_BASE(slotIndex) + PUPPET_SLOT_OFF_ACTIVE) == 0)
  {
    boat->hasPose = 0;
    return;
  }

  base = PUPPET_BOAT_BASE(slotIndex);
  flags = *(volatile u32 *)(base + PUPPET_BOAT_OFF_FLAGS);
  if ((flags & PUPPET_BOAT_FLAG_ACTIVE) == 0)
  {
    boat->hasPose = 0;
    return;
  }

  net.x = *(volatile f32 *)(base + PUPPET_BOAT_OFF_POSX);
  net.y = *(volatile f32 *)(base + PUPPET_BOAT_OFF_POSY);
  net.z = *(volatile f32 *)(base + PUPPET_BOAT_OFF_POSZ);
  speedF = *(volatile f32 *)(base + PUPPET_BOAT_OFF_SPEED_F);
  netRotY = *(volatile s16 *)(base + PUPPET_BOAT_OFF_ROTY);
  if (!boat_finite(net.x, BOAT_MAX_COORD) || !boat_finite(net.y, BOAT_MAX_COORD) ||
      !boat_finite(net.z, BOAT_MAX_COORD) || !boat_finite(speedF, BOAT_MAX_SPEED))
    return;

  if (!boat_ready(actor, boat))
    return;

  // A changed position is a new sample; otherwise the sample is getting older.
  if (net.x != boat->net.x || net.y != boat->net.y || net.z != boat->net.z)
  {
    boat->net = net;
    boat->framesSinceNet = 0;
  }
  else if (boat->framesSinceNet < BOAT_MAX_PREDICT_FRAMES)
  {
    boat->framesSinceNet++;
  }

  // Dead reckoning: carry the sample forward along the peer's heading at their speed.
  mDoMtx_YrotS(&m, netRotY);
  ahead.x = 0.0f;
  ahead.y = 0.0f;
  ahead.z = speedF * (f32)boat->framesSinceNet;
  PSMTXMultVec(&m, &ahead, &offset);
  targetX = net.x + offset.x;
  targetZ = net.z + offset.z;

  if (!boat->hasPose)
  {
    boat->pos.x = targetX;
    boat->pos.z = targetZ;
    boat->pos.y = net.y;
    boat->rotY = netRotY;
    boat->pitch = 0;
    boat->roll = 0;
  }
  else
  {
    dx = targetX - boat->pos.x;
    dz = targetZ - boat->pos.z;
    if (dx * dx + dz * dz > BOAT_SNAP_DIST_SQ)
    {
      boat->pos.x = targetX;
      boat->pos.z = targetZ;
    }
    else
    {
      boat->pos.x += dx * BOAT_POS_EASE;
      boat->pos.z += dz * BOAT_POS_EASE;
    }
    cLib_addCalcAngleS(&boat->rotY, netRotY, 2, 0x2000, 0x20);
  }

  // Height and tilt from the local sea; the peer's Y only in the air or off the sea.
  flying = (flags & PUPPET_BOAT_FLAG_FLY) != 0;
  onSea = !flying && d_a_sea__daSea_ChkArea(boat->pos.x, boat->pos.z);
  targetY = onSea ? d_a_sea__daSea_calcWave(boat->pos.x, boat->pos.z) : net.y;
  if (!boat->hasPose)
    boat->pos.y = targetY;
  else
    boat->pos.y += (targetY - boat->pos.y) * BOAT_POS_EASE;

  if (onSea)
    boat_waveAngles(boat, &pitchTarget, &rollTarget);
  cLib_addCalcAngleS(&boat->pitch, pitchTarget, 4, 0x400, 0x10);
  cLib_addCalcAngleS(&boat->roll, rollTarget, 4, 0x400, 0x10);

  // Hull at T(pos) * ZXYrot (as daShip_c::create, d_a_ship.cpp:4733-4736); head on GATTAI.
  bodyMtx = (MTX34 *)J3DMODEL_MBASETRMTX(boat->body);
  mDoMtx_ZXYrotS(bodyMtx, boat->pitch, boat->rotY, boat->roll);
  bodyMtx->m[0][3] = boat->pos.x;
  bodyMtx->m[1][3] = boat->pos.y;
  bodyMtx->m[2][3] = boat->pos.z;
  boat_animate(boat, base, flags);
  boat_calcModel(boat, boat->bodyAnm, 0);
  PSMTXCopy(&J3DMODEL_MPNODEMTX(boat->body)[BOAT_JNT_GATTAI], (MTX34 *)J3DMODEL_MBASETRMTX(boat->head));
  boat_calcModel(boat, boat->headAnm, 1);

  boat->hasPose = 1;
  boat->visible = 1;
}

int puppet_boatSeat(PuppetBoat *boat, cXyz *pos, csXyz *angle)
{
  if (!boat->visible)
    return 0;
  PSMTXMultVec((MTX34 *)J3DMODEL_MBASETRMTX(boat->body), (cXyz *)&l_boatSeatOffset, pos);
  angle->x = boat->pitch;
  angle->y = boat->rotY;
  angle->z = boat->roll;
  return 1;
}

void puppet_boatDraw(fopAc_ac_c *actor, PuppetBoat *boat)
{
  u8 *tev = (u8 *)boat->tevStr;
  u8 *actorTev = (u8 *)FOPAC_TEV_STR(actor);

  if (!boat->visible)
    return;

  // Light it for the room below. On open water daShip_c has no ground and uses the stay room
  // (setRoomInfo, d_a_ship.cpp:3081-3092); the puppet's own setRoomInfo keeps its tevStr on
  // the room under it, which is the same place.
  tev[TEVSTR_OFF_ROOM_NO] = actorTev[TEVSTR_OFF_ROOM_NO];
  tev[TEVSTR_OFF_ENVR_OVERRIDE] = actorTev[TEVSTR_OFF_ENVR_OVERRIDE];

  // daShip_c::draw (d_a_ship.cpp:253-299), hull and head only.
  dScnKy_env_light_c__settingTevStruct(&g_env_light, settingTevStruct__LightType__Actor,
                                       &boat->pos, (dKy_tevstr_c *)tev);
  dScnKy_env_light_c__setLightTevColorType(&g_env_light, boat->body, (dKy_tevstr_c *)tev);
  dScnKy_env_light_c__setLightTevColorType(&g_env_light, boat->head, (dKy_tevstr_c *)tev);

  dComIfGd_setListP1();
  mDoExt_modelEntryDL(boat->body);
  mDoExt_modelEntryDL(boat->head);
  dComIfGd_setList();
}

void puppet_boatDelete(PuppetBoat *boat)
{
  if (boat->heap != NULL)
  {
    // The morfs live in it too; like daShip_c's (freed with its actor heap) they get no dtor.
    mDoExt_destroySolidHeap(boat->heap);
    boat->heap = NULL;
    boat->bodyAnm = NULL;
    boat->headAnm = NULL;
    boat->body = NULL;
    boat->head = NULL;
  }

  if (boat->phase.mStep == BOAT_PHASE_LOADED)
    dComIfG_resDelete(&boat->phase, BOAT_ARC_NAME);
  else if (boat->phase.mStep == BOAT_PHASE_LOADING)
    // resDelete asserts mid-load, so this reference is never dropped and the archive stays
    // loaded for the session. Harmless at sea (d_s_play preloads "Ship" there anyway).
    OSReport("[PUPPET] boat: deleted while the Ship archive was loading; its reference stays\n");

  boat->state = PUPPET_BOAT_STATE_NONE;
  boat->visible = 0;
}
