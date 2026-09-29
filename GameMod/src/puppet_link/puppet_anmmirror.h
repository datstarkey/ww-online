/**
 * puppet_anmmirror.h - the peer's own anims for every proc the REL has no _init for (see
 * puppet_anmmirror.c).
 *
 * puppet_applyProcInit's default case enters the mirror instead of plain WAIT; from then on the
 * puppet plays the bcks, frames, rates and blend ratios the peer's Link plays (under[0..1],
 * upper[0..1]), plus their face btp / btk and hand shapes, from the anim mirror block
 * (puppet_shared.h PUPPET_ANM_*). Any other proc's init ends it.
 *
 * Included by puppet_execute.h; puppet_anmmirror.c is included by puppet.c after puppet_nametag.c
 * (puppet_bootBlock).
 */
#ifndef PUPPET_ANMMIRROR_H
#define PUPPET_ANMMIRROR_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

// From puppet_applyProcInit (inside its guard) for a proc with no _init: WAIT (unless already
// there), then mirror `proc`. Returns the init result like any other case.
int puppet_anmMirrorInit(daPy_lk_c *puppet, int proc);

// Every frame from puppet_readNetworkState right after the proc request (inside puppet_execute's
// guard): load / follow the peer's anims while mirroring, or end the mirror once another proc
// took over (a WAIT request re-inits WAIT: the mirror sits in WAIT itself).
void puppet_anmMirrorTick(daPy_lk_c *puppet, int slotIndex);

// Every frame right after playTextureAnime: the peer's face frames (m3530 / m3532) while mirroring.
void puppet_anmMirrorFace(daPy_lk_c *puppet);

// puppet.c, creating a puppet: the block (first puppet since boot) and a clean state for its slot.
void puppet_anmMirrorOnCreate(u32 slotIndex);

#endif // PUPPET_ANMMIRROR_H
