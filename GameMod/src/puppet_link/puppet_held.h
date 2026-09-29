/**
 * puppet_held.h - the item the peer holds and uses (see puppet_held.c).
 *
 * Stage 0: the puppet holds the peer's item (slot EQUIP_ITEM = the peer's mEquipItem), built in
 * the puppet's own item heaps by the stock daPy_lk_c model setters.
 * Stage 1: item procs and poses (aiming, throws, bottle, Wind Waker, hammer, Deku Leaf), the
 * peer's UPPER_MOVE2 anim (slot ANIM_ID), aim angles (slot BODY_ANGLE_X/Y), and a REL-drawn
 * boomerang / bomb from the resident "Link" archive in a small heap per puppet.
 *
 * Included by puppet_execute.h; puppet_held.c is included by puppet.c after puppet_draw.c.
 */
#ifndef PUPPET_HELD_H
#define PUPPET_HELD_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

// PuppetHeld.state
#define PUPPET_HELD_STATE_NONE   0
#define PUPPET_HELD_STATE_READY  1
#define PUPPET_HELD_STATE_FAILED 2

// Per-puppet REL models (PUPPET_class.held). The heap is ours, not playerInit's.
typedef struct PuppetHeld
{
  JKRSolidHeap *heap;
  J3DModel *boomerang; // Link BDL_BOOMERANG, drawn in the left hand while the puppet holds a boomerang
  J3DModel *bomb;      // Link BDL_BOMB, drawn between the hands while the peer carries a bomb
  u32 state;           // PUPPET_HELD_STATE_*
  u32 bombBck[4];      // an mDoExt_bckAnm (0x10: its J3DMtxCalcMayaAnm is in `heap`) playing Link BCK_BOMB
  s16 fuse;            // the carried bomb's fuse, frames left, counted down here between slot updates; 0 = not lit
  u8 netFuse;          // the slot's GRAB_FUSE as last read
  u8 bckReady;         // bombBck initialised
  u8 showBomb;         // set by puppet_heldExecute for the draw
} PuppetHeld;

// ---- held item + poses (called by puppet_execute.c, inside its global guard) ----

// Every frame from puppet_readNetworkState, before the proc request: take out / put away / swap
// the peer's item (the sword keeps its REST draw/sheathe). No-op while an item proc owns the item.
void puppet_heldMirror(daPy_lk_c *puppet, u32 slotBase, int wantProc);

// Every frame after the proc request: the peer's UPPER_MOVE2 anim (whitelisted aim/throw/carry anims).
void puppet_heldMirrorUpper(daPy_lk_c *puppet, u32 slotBase);

// From puppet_applyProcInit before an item proc's init: puts the item that proc needs in the
// puppet's hand. Returns 0 if the proc can't run (its init would assert): fall back to WAIT.
int puppet_heldForProc(daPy_lk_c *puppet, int proc);

// The item procs' inits (from puppet_applyProcInit). Where the real _init is unsafe for a puppet
// (global event / camera, or it reads an item actor the puppet doesn't have): WAIT + the proc's
// pose, relabelled. Returns -1 if proc isn't handled here, else the init's result.
int puppet_heldInitProc(daPy_lk_c *puppet, int proc);

// 1 if proc belongs to an item (the peer's mEquipItem is the proc's business while it runs).
int puppet_heldIsItemProc(int proc);

// 1 for the aim-while-strafing procs that blend like ATN_MOVE.
int puppet_heldIsAtnMoveProc(int proc);

// checkItemAction must not see the mirrored upper anims (BOOMTHROW would throw a NULL boomerang).
int puppet_heldUpperMirrored(daPy_lk_c *puppet);

// After checkItemAction: build the item a take-out anim just swapped in (see PUPPET_HELD_PENDING).
void puppet_heldFinishSwap(daPy_lk_c *puppet);

// Per frame before the model calc: item bck frame (m35EC) and the peer's aim angles.
void puppet_heldPose(daPy_lk_c *puppet);

// ---- shared model data (puppet_draw.c) ----
// Around the puppet's held-item entry: its Picto Box's flash shape (hidden for the regular one) on
// the SHARED camera model data. Returns the shape to restore to *saved afterwards, or NULL.
J3DShape *puppet_heldFlashBegin(daPy_lk_c *who, u32 *saved);

// ---- REL models (puppet.c) ----
void puppet_heldInit(PuppetHeld *held);
void puppet_heldExecute(daPy_lk_c *puppet, PuppetHeld *held);
void puppet_heldDraw(daPy_lk_c *puppet, PuppetHeld *held);
void puppet_heldDelete(PuppetHeld *held);

#endif // PUPPET_HELD_H
