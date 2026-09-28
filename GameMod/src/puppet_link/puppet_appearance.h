/**
 * puppet_appearance.h - per-Link outfit + tunic colour (puppet REL).
 *
 * Every Link on screen (the real one and each puppet) is built from the resident Link archive's
 * ONE cl.bdl J3DModelData. Two appearance inputs live in that shared data:
 *   - the outfit: the linktexS3TC ResTIMG header in TEX1 (daPy_lk_c::mpCurrLinktex, 0x338),
 *     which playerInit swaps between the hero texture and linktexbci4 (d_a_player_main.cpp:12336-12373);
 *   - the tunic colour: part of that texture (the body materials' TEV never uses the material
 *     colour: cl.bdl MAT3 stages are C0/KONST/TEXC/CPREV only, so mMatColor writes do nothing).
 * Both are read at ENTRY time, not when the packets execute: mDoExt_modelEntryDL -> J3DModel::diff ->
 * J3DTevBlock::diffTexNo -> loadTexNo (J3DTevs.cpp:138) bakes the header's image address into the
 * model's own differed display list (differed flags 0x37221222 include TexNo, J3DModel.cpp:374).
 * So each Link gets its own look by writing the header right before ITS body entry and putting it
 * back right after. The colour is a recoloured copy of the hero texture (game heap) that the
 * header's imageOffset points at.
 *
 * The local Link is drawn by vanilla code, so the REL keeps the shared header set to the local
 * player's look (outfit + colour) every frame; puppets swap theirs in around their own entry.
 */
#ifndef PUPPET_APPEARANCE_H
#define PUPPET_APPEARANCE_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

/* A shared-header write undone after one puppet's body entry. */
typedef struct PuppetLinktexSwap
{
  u32 *slot; /* NULL = nothing written */
  u32 saved[RESTIMG_WORDS];
} PuppetLinktexSwap;

/* Once per game frame (any puppet's execute, parked ones too): apply the local player's
 * published outfit/colour (LOCAL_APPEARANCE_*) to the real Link. */
void puppet_appearance_tick(void);

/* Around one puppet's body entry: write that Link's outfit (casual = its daPyFlg1_CASUAL_CLOTHES)
 * and the slot's tunic colour into the shared header, then restore it. *lookBlock is the puppet's
 * recolour cache (game heap), created on first use. */
void puppet_appearance_begin(u32 slotIndex, int casual, u8 **lookBlock, PuppetLinktexSwap *swap);
void puppet_appearance_end(PuppetLinktexSwap *swap);

/* Puppet lifetime: the last delete (the REL is unlinked right after) leaves the local look in a
 * state that needs no REL code. */
void puppet_appearance_onCreate(void);
void puppet_appearance_onDelete(u8 **lookBlock, int counted);

#endif /* PUPPET_APPEARANCE_H */
