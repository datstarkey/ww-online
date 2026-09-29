/**
 * puppet_fx.h - other players' projectiles made real in this world (docs/held-items.md sections 7-8).
 *
 * SENDER: every frame the REL watches the LOCAL Link's projectiles and reports them in the events
 * block's outbox (puppet_shared.h PUPPET_FX_*), which the C# client sends to the players who can
 * see us: a bomb leaving Link's hands (thrown / put down), picked up again, exploding (in hand or
 * not) or vanishing (sank), the boat cannon firing and its cannonball exploding / sinking, an arrow shot.
 * VIEWER: C# writes the peers' events in the inbox; the REL spawns a real daBomb_c for each (lit,
 * in flight, fuse and velocity as the sender's) or the boat's cannonball, holds the copy's fuse
 * until the sender's explosion arrives and then explodes it at the sender's position; an arrow flies
 * from the sender's launch point along their aim.
 *
 * Compiled into the puppet REL (puppet.c includes puppet_fx.c). Runs only while a puppet exists,
 * parked or not; a peer who can see us has a puppet for us here too, so that covers the sender.
 */
#ifndef PUPPET_FX_H
#define PUPPET_FX_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

// From daPuppet phase 1 (every puppet): adopt the events block, or allocate it the first time.
void puppet_fx_onCreate(void);

// From every daPuppet_Execute, before the puppet's own execute (parked puppets too); runs once per frame.
void puppet_fx_tick(void);

#endif // PUPPET_FX_H
