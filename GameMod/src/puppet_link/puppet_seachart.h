/**
 * puppet_seachart.h - the other players on the sea chart (the pause menu's map of the Great Sea).
 *
 * The chart (dMenu_Fmap_c, d_menu_fmap.cpp) draws Link as the 'lnk1' picture on the world map and 'lnk2' on a
 * zoomed square. While the menu is open no actor runs, so the REL can't draw from a puppet: a required DOL
 * patch (src/patches/seachart_players.asm) calls puppet_seachart_draw through SEACHART_FN_ADDR from
 * dDlst_FMAP_c::draw, right after the chart's screen has drawn. We redraw Link's own picture once per other
 * player on the sea (the SEACHART_* block C# fills), moved by their offset from Link, turned to their heading
 * and tinted their tunic colour, and put everything back.
 *
 * Compiled into the puppet REL: puppet.c #includes puppet_seachart.c after puppet_nametag.c.
 */
#ifndef PUPPET_SEACHART_H
#define PUPPET_SEACHART_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

#define SEACHART_MAX_COORD 400000.0f /* the sea is 7 x 100000 units across, centred */

/* Called by the DOL patch with dDlst_FMAP_c* (the menu's fmapDl) and the chart's graf port. */
void puppet_seachart_draw(u8 *dl, J2DGrafContext *graf);

/* Every puppet create: make / adopt the block and publish the draw function. */
void puppet_seachart_onCreate(void);

/* The last puppet's delete, before the REL is unlinked: unpublish the draw function. */
void puppet_seachart_onLastDelete(void);

#endif /* PUPPET_SEACHART_H */
