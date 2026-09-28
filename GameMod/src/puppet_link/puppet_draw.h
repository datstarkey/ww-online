/**
 * puppet_draw.h - Puppet Link Drawing System Header
 *
 * Declarations for draw functions used by the puppet actor.
 *
 * All offset macros are defined in ww_inlines.h (centralized location).
 * These are verified correct from tww-decomp.
 */

#ifndef PUPPET_DRAW_H
#define PUPPET_DRAW_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"

// ============================================================================
// PER-INSTANCE DISPLAY LIST PACKETS
// ============================================================================

/**
 * The four eye/brow z-compare state packets (mDoExt_onCupOffAupPacket /
 * mDoExt_offCupOnAupPacket, both plain J3DPacket subclasses, 0x10 bytes).
 *
 * MUST be per puppet: J3DDrawBuffer::entryImm links packet->next = list head,
 * so entering the same packet object twice in one frame makes the draw list
 * cyclic (infinite loop in J3DDrawBuffer::draw). Lives in PUPPET_class.
 */
typedef struct PuppetDrawPackets
{
  J3DPacket onCupOffAup1;
  J3DPacket onCupOffAup2;
  J3DPacket offCupOnAup1;
  J3DPacket offCupOnAup2;
} PuppetDrawPackets;

// ============================================================================
// PUBLIC FUNCTIONS
// ============================================================================

/**
 * Initialize one puppet's display list packets (vtables + null links).
 * Called from create (phase 2); puppet_draw also re-inits if the vtable is 0.
 */
void puppet_initPackets(PuppetDrawPackets *packets);

/**
 * Main puppet draw function - all draw logic consolidated here
 *
 * Called every frame by the actor system via daPuppet_Draw.
 *
 * @param link      Pointer to the puppet's daPy_lk_c instance
 * @param slotIndex PUPPET_class.slotIndex (sync slot this puppet mirrors)
 * @param packets   This puppet's own display list packets
 * @param lookBlock This puppet's tunic recolour cache (PUPPET_class.lookBlock, puppet_appearance.c)
 * @return 1 on success
 */
int puppet_draw(daPy_lk_c *link, u32 slotIndex, PuppetDrawPackets *packets, u8 **lookBlock);

#endif // PUPPET_DRAW_H
