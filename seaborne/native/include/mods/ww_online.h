// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
/*
 * ww_online.h - the ww-online multiplayer mod's call into the game loop (native/src/mods/ww_online,
 * built with COS_WITH_WWONLINE). Its C API is the multiplayer part of pc/tww.h.
 */
#ifndef MODS_WW_ONLINE_H
#define MODS_WW_ONLINE_H

/* main01's frame, between the pad read and fapGm_Execute: creates and deletes the peers' puppets. */
void wwo_frame();
/* fapGm_After, after the frame's draw pass: the peers' name tags into the 2D list. */
void wwo_draw();

#endif /* MODS_WW_ONLINE_H */
