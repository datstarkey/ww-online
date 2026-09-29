/**
 * puppet_procs_extra.h - more of the peer's procs, with the real _init or a relabelled pose
 * (see puppet_procs_extra.c).
 *
 * Included by puppet_execute.h; puppet_procs_extra.c is included by puppet.c after puppet_held.c
 * and uses puppet_execute.c's helpers (single translation unit).
 */
#ifndef PUPPET_PROCS_EXTRA_H
#define PUPPET_PROCS_EXTRA_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

// From puppet_applyProcInit, inside its global guard, after the item procs: the init for the procs
// handled here. param = request-key bits 16-23 (puppet_extraKey). Returns -1 if proc isn't handled
// here, else the init's result.
int puppet_extraInitProc(daPy_lk_c *puppet, int proc, int param);

// From puppet_readNetworkState, after the other key bits: the side (HANG_DIR_LEFT/RIGHT) a side hop
// or a parry roll goes, in bits 16-19. prevKey = the last key that was initialised.
int puppet_extraKey(int key, int prevKey, f32 stickDistance, s16 stickAngle, s16 rotY);

// 1 for the procs that strafe like ATN_MOVE from the peer's stick (setStickData gets the stick).
int puppet_extraIsAtnMoveProc(int proc);

// Per frame from puppet_executeBody, after animeUpdate: what the vanilla per-frame proc does to
// the anim for these procs (strafe blends, follow-up anims, body tilt, root pinning).
void puppet_extraStep(daPy_lk_c *link, int proc);

// Per frame before the model calc (after the body-angle decay): the peer's look angles.
void puppet_extraPose(daPy_lk_c *link);

#endif // PUPPET_PROCS_EXTRA_H
