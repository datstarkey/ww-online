/**
 * puppet_liveworld.h - live world: bring create-only actors up to date with bits other players set.
 *
 * Most puzzle objects poll their switch every frame and react on their own when the C# world sync
 * writes a bit into the live save data. A few read their flag ONLY when they are created, so a bit
 * that arrives while you are in the room does nothing to them until the room reloads:
 *   - chests (daTbox_c::checkOpen at create, d_a_tbox.cpp:454): the chest stays closed and opening
 *     it gives the item AGAIN (actionOpenWait/boxCheck never re-read mTbox, :553-563, :954-996);
 *   - bombable walls, breakable floors, ice blocks, the FF barricade (create returns ERROR when the
 *     switch is set: d_a_wall.cpp:141-145, d_a_floor.cpp:59-61, d_a_obj_ice.cpp:105-116/232,
 *     d_a_obj_majyuu_door.cpp:146-149);
 *   - wooden barricades (d_a_saku.cpp:709-725) and crystal switches (d_a_swhit0.cpp:166-173: shows
 *     "off" while the switch is on);
 *   - small-key locks (door setKey only in actionInit, docs/small-keys.md §3: the lock stays and
 *     using it spends a second key).
 * C# publishes the bits it applied from other players in the live-world block (puppet_shared.h
 * LIVEWORLD_*). Each new bit is handled once: every matching actor is re-created from its own params
 * in its own layer (the new instance's create reads the flag and comes up in the end state, with no
 * animation and no event), or the stale lock byte is cleared (what setKey would do).
 *
 * Never while an event runs or is queued (an event order holds actor pointers), never mid stage
 * change, never for an actor still being created or deleted, never for a room that is loading or
 * unloading. Never starts an event, grants an item or touches the local Link.
 *
 * Compiled into the puppet REL (puppet.c includes puppet_liveworld.c); puppet_worldsync_tick calls
 * puppet_liveworld_tick on its every-4th-frame gate, after its stage-change / stage-info checks. The REL only runs while a puppet exists, so C#
 * keeps a parked one alive while the block has work (SEQ != DONE_SEQ).
 */
#ifndef PUPPET_LIVEWORLD_H
#define PUPPET_LIVEWORLD_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

#if !defined(LIVEWORLD_PTR_ADDR) || !defined(LIVEWORLD_MAGIC) || !defined(LIVEWORLD_BLOCK_SIZE)
#error "puppet_shared.h is missing the LIVEWORLD_* defines (see puppet_liveworld.h)"
#endif

/* Puppet create: adopt the live-world block an earlier REL instance left, or allocate and publish it
 * (puppet_bootBlockCreate, puppet_nametag.c). */
#define puppet_liveworld_onCreate() puppet_bootBlockCreate(LIVEWORLD_PTR_ADDR, LIVEWORLD_MAGIC, LIVEWORLD_BLOCK_SIZE)

/* Handle a new batch from C#, if any. Called every 4th frame from puppet_worldsync_tick, once no stage
 * change is in flight and the stage info is valid; saveTbl is the current stage's save slot. */
void puppet_liveworld_tick(u32 saveTbl);

#endif /* PUPPET_LIVEWORLD_H */
