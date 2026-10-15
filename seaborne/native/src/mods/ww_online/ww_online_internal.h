// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// What the mod's files share: the peer table (tww_set_peer) and the puppet's view of a player.
#ifndef WW_ONLINE_INTERNAL_H
#define WW_ONLINE_INTERNAL_H

#include "f_pc/f_pc_base.h"
#include "pc/tww.h"

#include <functional>

class daPy_lk_c;
class fopAc_ac_c;
class cXyz;

struct WwoPeer {
    bool active;
    TwwPeerState state;
};

// Slot i is shown by the puppet created with parameter (i << 24).
extern WwoPeer g_wwoPeers[TWW_MAX_PEERS];

// d_a_puppet.cpp: daPy_lk_c's proc (a private member the puppet class may read).
int wwo_playerProc(const daPy_lk_c* player);

// ww_world.cpp: the world sync's live passes (placed-item despawn, live world), once per frame from wwo_frame.
void wwo_world_frame();

// ww_online.cpp: the slot's puppet once it is created and running, else NULL.
fopAc_ac_c* wwo_puppetActor(int slot);

// ww_puppet.cpp: the top of slot's puppet's head (as daPy_lk_c::mHeadTopPos: 40 units along the head joint)
// after its last joint calc, in the boat too; false while the slot has no shown puppet. For the name tag.
bool wwo_puppetHeadPos(int slot, cXyz* out);

// ww_pvp.cpp: every puppet's solid body; the taking-part puppets' attention and hurtboxes, the local Link's hits on them and the peers' on him;
// once per frame from wwo_frame, after the puppets' lifetimes.
void wwo_pvpFrame();
// ww_pvp.cpp: the local Link just hit slot's puppet: it flashes red at once, before the victim's own flash comes back.
bool wwo_pvpFlashing(int slot);

// ww_online.cpp, COS_MP_TEST_PEER (dev): deliver hands the test peer's state for slot 0 to the game, now or, with
// COS_MP_TEST_RATE, later and only on the frames that send one.
void wwo_testSend(std::function<void()> deliver);

// ww_online.cpp: the mod's actor process id (running or still creating), or NULL once it is gone.
base_process_class* wwo_findProcess(fpc_ProcID id);

#endif
