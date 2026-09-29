/**
 * puppet_sharedanm.h - keep a puppet's animator objects out of Link's SHARED model data.
 *
 * Every Link (the local one and each puppet) is a daPy_lk_c built on the same J3DModelData from
 * the resident "Link" archive. daPy_lk_c code writes pointers to its OWN heap objects into that
 * shared data and never takes them back:
 *   - createHeap: initTextureAnime / initTextureScroll give cl.bdl's eye, brow and mouth materials
 *     a new J3DMaterialAnm / daPy_matAnm_c (J3DMaterial::mMaterialAnm, d_a_player_main.cpp:11819-11908);
 *     entryBtk / entryBrk put new J3DTexMtxAnm / J3DTevColorAnm objects in the resident
 *     J3DMaterialAnm slots of 14 effect / equipment models (:12081-12101, J3DMaterialAttach.cpp).
 *   - execute: setItemModel's mDoExt_bckAnm::entry (joint 0 mMtxCalc), the item setters'
 *     entryTexMtxAnimator / entryTevRegAnimator / entryMatColorAnimator (sword, bottle, baton).
 *   - draw: the texNo / texMtx animators, the aura / magic circle brk entries.
 * The J3DMaterialAnm of a material is only set at create, so after a puppet's playerInit the LOCAL
 * Link drew its face through the puppet's objects; once the puppet was deleted and its heap reused,
 * the local draw called a garbage vtable (J3DJoint::entryIn -> J3DMaterialAnm::calc from
 * daPy_lk_c::draw's first cl_eye entry).
 *
 * The fix is generic: every pointer-sized "animator slot" of every model in the Link archive
 * (each material's mMaterialAnm, the 26 slots of each J3DMaterialAnm, joint 0's mMtxCalc) is
 * snapshotted when a puppet window opens (create, execute, draw) and restored when it closes.
 * The words the puppet changed are kept in the puppet's own swap list and written back in at the
 * start of its next window, so a puppet always sees its own animators and nothing else ever does.
 */
#ifndef PUPPET_SHAREDANM_H
#define PUPPET_SHAREDANM_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"

// Room for one puppet's own values: 70 after create and 72-82 with items in the first in-game test;
// sword / cut effects / bottle / baton / aura / magic circle add more (the log prints the count).
#define PUPPET_SHANM_SWAPS_MAX 256

// The words one puppet has changed in the shared data, with its values (PUPPET_class.shanm).
typedef struct PuppetSharedAnm
{
  u16 count;
  u16 slot; // for the log
  u32 *addr[PUPPET_SHANM_SWAPS_MAX];
  u32 val[PUPPET_SHANM_SWAPS_MAX];
} PuppetSharedAnm;

void puppet_shanmInit(PuppetSharedAnm *s, u32 slot);

// Rebuild the table of Link archive model datas (create and delete only; cheap getRes calls).
void puppet_shanmRefresh(void);

// A puppet window: Begin snapshots the local state and writes the puppet's values in; End records
// what the puppet changed (its new list) and puts the local state back. report=1 logs the result.
// Windows never nest (a nested Begin/End pair is a no-op). Open one only around player code that
// runs (create, makeBgWait, an unparked execute, a draw that draws, delete): ~2600 words each.
// Cost: see puppet_sharedanm.c.
void puppet_shanmBegin(PuppetSharedAnm *s);
void puppet_shanmEnd(int report);

// Delete-time proof: count tracked words pointing into [lo,hi) ranges (all of the puppet's heaps
// and its instance), log them. Must be 0.
int puppet_shanmScan(const u32 *ranges, int rangeNum, u32 slot);

#endif // PUPPET_SHAREDANM_H
