/**
 * puppet_worldsync.h - Layer 2 of shared-world sync (live despawn of placed pickups).
 *
 * Layer 1 (C# client) OR-merges per-stage save flags between players, including the
 * LIVE dSv_info_c::mMemory of the current stage. Layer 2 (this file) makes placed daItem
 * pickups whose item bit another player set vanish immediately instead of on the next
 * stage load. Everything else (chests, switches) picks the merged flags up on its own.
 *
 * Compiled into the puppet REL: puppet.c #includes puppet_worldsync.c and calls
 * puppet_worldsync_tick() from daPuppet_Execute (runs once per game frame no matter
 * how many puppets exist, parked ones included). With no puppet it does not run at all, so
 * the C# client keeps a parked puppet alive until WORLDSYNC_PENDING_ADDR reports 0.
 */
#ifndef PUPPET_WORLDSYNC_H
#define PUPPET_WORLDSYNC_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

/* Scratch words this file needs (owned by puppet_shared.h). */
#if !defined(WORLDSYNC_ITEM_MASK_ADDR) || !defined(WORLDSYNC_STAGE_TAG_ADDR) || \
    !defined(WORLDSYNC_TAG_MAGIC) || !defined(WORLDSYNC_DESPAWN_COUNT_ADDR) || \
    !defined(WORLDSYNC_LAST_DESPAWN_ADDR) || !defined(WORLDSYNC_PENDING_ADDR) || \
    !defined(WORLDSYNC_SCAN_SEQ_ADDR) || !defined(WORLDSYNC_SCAN_TAG_ADDR) || \
    !defined(WORLDSYNC_SCAN_CAND_ADDR)
#error "puppet_shared.h is missing the WORLDSYNC_* defines (see puppet_worldsync.h)"
#endif

void puppet_worldsync_tick(void);

#endif /* PUPPET_WORLDSYNC_H */
