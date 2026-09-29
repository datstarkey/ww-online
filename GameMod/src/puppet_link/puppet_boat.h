/**
 * puppet_boat.h - the peer's King of Red Lions, drawn by their puppet.
 *
 * A second daShip_c can't be spawned: its create takes over the global ship pointer
 * (dComIfGp_setShipActor, d_a_ship.cpp:4746), its execute writes the local player's SAIL
 * status and sail audio every frame (:4340-4347), and it spawns a sail-cloth Grid, a
 * WindArrow and a sea-encounter spawner that all assume one ship. So each puppet draws its
 * own copy of the boat instead: the hull and head models from the "Ship" archive, placed at
 * the peer's boat position (PUPPET_BOAT_* in puppet_shared.h), with height and tilt taken
 * from the local sea the way daShip_c::setYPos / setWaveAngle do. The puppet then sits in
 * it at the seat daPy_lk_c::setShipRidePos uses.
 *
 * Posed like daShip_c: both models are mDoExt_McaMorfs playing the peer's mast bck
 * (FN_MAST_ON2 / FN_MAST_OFF2) and head bck at the peer's frames, and our joint callback
 * repeats daShip_c's body and head callbacks (d_a_ship.cpp:110-231) with the peer's sail,
 * tiller and head-look angles and mast scale.
 *
 * The sail cloth is not part of the hull: daShip_c spawns a daGrid_c (d_a_grid.cpp) whose own
 * packet (daHo_packet_c) draws an 85-vertex cloth mesh with the "Ship" new_ho1.bti texture, moved
 * procedurally by ho_move (wind, mSailAngle, the mast bck's SAIL1 / SAIL2 joint scales). Each
 * puppet boat keeps an imitation daGrid_c in its heap and runs the game's own ho_move and packet
 * draw on it (see puppet_boat.c, "Sail"), recoloured with the peer's tunic colour.
 *
 * The cannon and the crane (salvage arm) sit on the mast joint like daShip_c's (bodyJointCallBack's
 * J_FN_MAST, d_a_ship.cpp:119-125), aimed with the peer's cannon / arm angles (cannonJointCallBack,
 * craneJointCallBack) and drawn while the peer's mPart is that part (daShip_c::draw, :304-317). The
 * crane's rope is drawn hanging straight down at the peer's length (the game's rope sways: its
 * physics isn't run here), with the grappling hook at its end. Nothing fires, salvages or plays a
 * sound: they are just models.
 *
 * Not drawn yet: wake effects, the rope's sway and ripples, the hook while the rope is under 3
 * segments (the arm rising / folding), the talk mouth
 * (headJointCallBack0's mAnmTransform swap) and the boat's shadow. The hull's water effect
 * texture matrix is shared material state that the local ship sets (d_a_ship.cpp:259-282), so
 * ours shows the local ship's projection. The boat has no collision.
 *
 * Compiled into the puppet REL: puppet.c #includes puppet_boat.c after puppet_execute.c.
 */
#ifndef PUPPET_BOAT_H
#define PUPPET_BOAT_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

#if !defined(PUPPET_BOAT_0) || !defined(PUPPET_BOAT_OFF_FLAGS) || !defined(PUPPET_BOAT_FLAG_ACTIVE)
#error "puppet_shared.h is missing the PUPPET_BOAT_* defines (see puppet_boat.h)"
#endif

/* PuppetBoat.state */
#define PUPPET_BOAT_STATE_NONE    0  /* nothing loaded yet */
#define PUPPET_BOAT_STATE_READY   1  /* archive loaded, models built */
#define PUPPET_BOAT_STATE_FAILED  2  /* load or model build failed: never retried for this puppet */

typedef struct PuppetBoat
{
  request_of_phase_process_class phase; /* "Ship" archive: dComIfG_resLoad / dComIfG_resDelete */
  u8 state;                             /* PUPPET_BOAT_STATE_* */
  u8 visible;                           /* posed this frame: draw it and seat the puppet */
  u8 hasPose;                           /* pos/angles hold a pose (0: snap on the next sample) */
  u8 framesSinceNet;                    /* game frames since the network position last changed */
  u8 seated;                            /* the puppet was pinned to the seat last frame */
  u8 mastBck;                           /* SHIP_BCK_* bodyAnm plays (daShip_c::m0392) */
  u8 headBck;                           /* SHIP_BCK_* headAnm plays (daShip_c::m03B4) */
  u8 mastHide;                          /* PUPPET_BOAT_FLAG_MAST_HIDE this frame */
  JKRSolidHeap *heap;                   /* our own heap for the two morfs */
  mDoExt_McaMorf *bodyAnm;              /* FN_BODY + mast bck, as daShip_c::mpBodyAnm */
  mDoExt_McaMorf *headAnm;              /* FN_HEAD_H + head bck, as daShip_c::mpHeadAnm */
  J3DModel *body;                       /* bodyAnm's model: hull, mast, tiller */
  J3DModel *head;                       /* headAnm's model: the King of Red Lions' head */
  cXyz net;                             /* last network position (a change = a new sample) */
  cXyz pos;                             /* drawn position */
  s16 rotY;
  s16 pitch;                            /* shape_angle.x */
  s16 roll;                             /* shape_angle.z */
  s16 sailAngle;                        /* mSailAngle */
  s16 tiller;                           /* m0366 */
  s16 headX;                            /* m03A0 */
  s16 headY;                            /* m03A2 */
  s16 pad2;
  u32 tevStr[0xB0 / 4];                 /* dKy_tevstr_c (d_kankyo.h, size 0xB0) */
  request_of_phase_process_class clothPhase; /* "Cloth" archive: the sail's toon texture */
  u8 clothState;                        /* PUPPET_SAIL_CLOTH_* */
  u8 sailShow;                          /* the sail is up this frame: enter its packet */
  u8 sailCur;                           /* palette buffer the recolour key is in */
  u8 pad3;
  u8 *sail;                             /* imitation daGrid_c + palettes (in `heap`), NULL = none */
  u32 sailKey;                          /* recoloured palette's colour | 0x01000000, 0 = none yet */
  f32 sailScale;                        /* daGrid_c scale.y: SAIL1's length (d_a_ship.cpp:4066) */
  /* Cannon / crane (see puppet_boat.c, "Cannon and crane"); the models are in `heap`, NULL = none */
  J3DModel *cannon;                     /* "Ship" vfncn.bdl, as daShip_c::mpCannonModel */
  J3DModel *crane;                      /* "Ship" vfncr.bdl, as daShip_c::mpSalvageArmModel */
  J3DModel *hook;                       /* "Link" ropeend.bdl, as daShip_c::mpLinkModel */
  u8 *rope;                             /* mDoExt_3DlineMat1_c, as daShip_c::mRopeLine (2 points) */
  f32 mastScale;                        /* J_FN_MAST's length before the hide scale (the hook's scale) */
  s16 cannonYaw;                        /* m0394 */
  s16 cannonPitch;                      /* m0396 */
  s16 craneAngle;                       /* m0398 + m039C */
  u8 part;                              /* SHIP_PART_CANNON / _CRANE posed this frame, else 0 */
  u8 ropeCnt;                           /* mRopeCnt */
  u8 hookShow;                          /* the hook is posed this frame */
  u8 pad4[3];
} PuppetBoat;

/* PuppetBoat.clothState */
#define PUPPET_SAIL_CLOTH_LOADING 0
#define PUPPET_SAIL_CLOTH_READY   1
#define PUPPET_SAIL_CLOTH_FAILED  2

/* Zero the boat. Call once from the puppet's create. */
void puppet_boatInit(PuppetBoat *boat);

/* Load / pose the boat for this frame from the slot's PUPPET_BOAT_* block. Call before
 * puppet_execute so the puppet can be seated in this frame's pose. */
void puppet_boatExecute(fopAc_ac_c *actor, PuppetBoat *boat, u32 slotIndex);

/* The seat (setShipRidePos's l_ship_offset on the hull matrix) and the boat's angles.
 * Returns 0 when the boat isn't posed this frame. */
int puppet_boatSeat(PuppetBoat *boat, cXyz *pos, csXyz *angle);

void puppet_boatDraw(fopAc_ac_c *actor, PuppetBoat *boat);

/* Free the models and drop our reference on the "Ship" archive. Call from the puppet's delete. */
void puppet_boatDelete(PuppetBoat *boat);

#endif /* PUPPET_BOAT_H */
