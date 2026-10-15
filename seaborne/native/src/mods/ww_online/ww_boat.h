// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The boats (ww_boat.cpp, d_a_wwo_boat.cpp) for the rest of the mod: their frame, their peer table and the seat
// a puppet rides in.
#ifndef WW_BOAT_H
#define WW_BOAT_H

#include "pc/tww.h"

class cXyz;
class csXyz;

// tww_boat_set_peer's table: slot i's boat is shown by the boat actor created with parameter (i << 24).
struct WwoBoatPeer {
    bool active;
    TwwBoatState state;
    TwwBoatLook look;
};
extern WwoBoatPeer g_wwoBoats[TWW_MAX_PEERS];

const int kWwoMaxRope = 250; // daShip_c::mRopeLineSegments, incRopeCnt's cap

// wwo_frame, before fapGm_Execute: creates and deletes the peers' boats and keeps the local boat's colours.
void wwo_boatFrame(const TwwLocation& loc, bool inPlay);

// d_a_wwo_boat.cpp: the seat in slot's boat this frame (setShipRidePos's l_ship_offset on the hull) and the hull's angles
// for a puppet in proc; false while the slot has no boat shown, or a parked one, or proc is not one that sits in it
// (every ship proc but getting in or out; items used aboard are not ship procs and still ride).
bool wwo_boatSeat(int slot, u32 proc, cXyz* pos, csXyz* angle);

#endif
