/**
 * puppet_boatlook.h - the local King of Red Lions in the local player's colours (BOATLOOK_* in puppet_shared.h).
 *
 * The local ship draws its hull from fn_body.bdl's fn_main1 palette through the material's shared display
 * list, and its sail through the new_ho1.bti header, both shared "Ship" archive data. So the local boat is
 * recoloured in place: the hull in the boat colour, the sail in the tunic colour, like the other players see
 * it (puppet_boat.c "Hull colour" / "Sail"). The vanilla palettes are kept in a boot-stamped game-heap block,
 * so a peer's boat still recolours from vanilla and shows vanilla for the classic red / default sail.
 *
 * Compiled into the puppet REL: puppet.c #includes puppet_boatlook.c after puppet_seachart.c
 * (puppet_bootBlock) and puppet_boat.c (the recolours), which uses the lookups below.
 */
#ifndef PUPPET_BOATLOOK_H
#define PUPPET_BOATLOOK_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

#define BOATLOOK_PART_HULL 0
#define BOATLOOK_PART_SAIL 1

/* The palette to recolour a peer's part from: the vanilla copy while it is of timg's live palette, else the
 * live palette (never recoloured without one). */
static const u16 *boatlook_source(int part, const u32 *timg);

/* The vanilla copy while timg's live palette shows the local recolour (a peer's classic red / default sail
 * swaps it in), else NULL. */
static const u16 *boatlook_vanillaShown(int part, const u32 *timg);

/* Once per frame, before the boats execute: keep the live palettes in the colours C# wants. */
void puppet_boatlook_tick(void);

/* Every puppet create: make / adopt the block. */
void puppet_boatlook_onCreate(void);

#endif /* PUPPET_BOATLOOK_H */
