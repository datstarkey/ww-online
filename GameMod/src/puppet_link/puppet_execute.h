/**
 * puppet_execute.h - Execute function declarations for Puppet Link
 *
 * All offset macros are defined in ww_inlines.h (centralized location).
 * These are verified correct from tww-decomp.
 */

#ifndef PUPPET_EXECUTE_H
#define PUPPET_EXECUTE_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"
#include "puppet_boat.h"
#include "puppet_held.h"

// Forward declaration
typedef struct PUPPET_class PUPPET_class;

// Back-compat aliases — existing code uses these names. All memory layout
// comes from puppet_shared.h; these aliases keep older references compiling.
#define PUPPET_SLOT_CLOTHES_TYPE PUPPET_SLOT_OFF_CLOTHES_TYPE

// ============================================================================
// EXECUTE FUNCTION DECLARATIONS
// ============================================================================

/**
 * puppet_execute - Trimmed port of daPy_lk_c::execute() for a network-driven puppet
 *
 * Skips everything that touches global/local-player state (room loading, HUD status,
 * restarts, controller input) and guards daPy_lk_c calls that do (player status,
 * rumble, attention lock). Uses offset-based field access instead of struct fields
 * since ww_structs.h has wrong field sizes (GCC vs Metrowerks padding).
 *
 * @param link The puppet daPy_lk_c instance
 * @return 1 on success
 */
int puppet_execute(daPy_lk_c *link);

/**
 * Read puppet state from shared memory and apply to puppet.
 * Reads position, rotation, animation state from the slot corresponding
 * to the puppet's slot index. If the shared memory magic marker is not present
 * (or the slot is inactive), the puppet holds its position and idles (PROC_WAIT).
 *
 * @param puppet The puppet daPy_lk_c instance
 * @param slotIndex Which shared memory slot to read (0-2)
 */
void puppet_readNetworkState(daPy_lk_c *puppet, int slotIndex);

/**
 * Parked puppets (slot ACTIVE word == 0, see puppet_execute.c "PARKED PUPPETS"): no execute,
 * no collision/attention/effects, invisible. daPuppet_Execute calls puppet_parkEnter on the
 * transition into parked, puppet_parkTick every parked frame, and puppet_parkResume when the
 * slot becomes active again (then resets followState so the peer's proc is re-initialised).
 */
int puppet_slotIsParked(u32 slotIndex);
void puppet_parkEnter(daPy_lk_c *link);
void puppet_parkTick(daPy_lk_c *link);
void puppet_parkResume(daPy_lk_c *link, u32 slotIndex);

/**
 * Legacy wrapper - calls puppet_execute internally
 *
 * @param link The puppet daPy_lk_c instance
 * @return 1 on success
 */
int puppet_executeSection1to10(daPy_lk_c *link);

/**
 * Update puppet rotation to face the player
 */
void puppet_updateRotation(fopAc_ac_c *actor, fopAc_ac_c *playerActor);

/**
 * Update Z-targeting attention info
 * Must be called every frame to maintain lock-on capability
 */
void puppet_updateAttentionInfo(fopAc_ac_c *actor);

#endif // PUPPET_EXECUTE_H
