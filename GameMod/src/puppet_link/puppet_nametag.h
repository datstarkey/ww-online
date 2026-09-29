/**
 * puppet_nametag.h - the peer's name floating above each puppet (puppet REL).
 *
 * How it is drawn (no game helper draws text at a world position, so this is the pattern the
 * game's own world-anchored 2D uses, e.g. dMeter_zakoEnemyMove's enemy HP bar, d_meter.cpp:3704):
 *   - the puppet's draw projects a point above its head with mDoLib_project (m_Do_lib.cpp:52,
 *     640x480 2D-port coordinates) and enters this per-puppet packet in the 2D opaque list
 *     (dComIfGd_set2DOpa, d_com_inf_game.h:3915, the list the HUD uses, d_meter.cpp:7281);
 *   - the painter draws that list with its J2DOrthoGraph set as the current graf port
 *     (mDoGph_Painter, m_Do_graphic.cpp:1572-1576 / 1916) and calls this packet's draw(), which
 *     draws the name like JUTDbPrint::flush does (JUTDbPrint.cpp:64-80): graf port, font->setGX(),
 *     colours, JUTFont::drawString_size_scale. The font is the message font mDoExt_font0
 *     (rock_24_20_4i_usa.bfn), which d_s_logo.cpp:576 acquires for good at boot.
 *
 * Lifetime: the painter runs at the START of the next frame, before process deletes
 * (fpcM_Management, f_pc_manager.cpp:278-280), and the lists are reset before every draw pass
 * (dScnPly_BeforeOfPaint, m_Do_graphic.cpp:271). So a queued packet (and this REL's code) is
 * always still alive when it is drawn.
 */
#ifndef PUPPET_NAMETAG_H
#define PUPPET_NAMETAG_H

#include "../../include/ww_defines.h"
#include "../../include/ww_inlines.h"
#include "puppet_shared.h"

/* One puppet's name packet. A dDlst_base_c: the vtable MUST be the first word (dDlst_list_c::draw
 * calls vtable[3], d_drawlist.cpp:2018-2021). Filled by puppet_nametag_queue every drawn frame. */
typedef struct PuppetNameTag
{
  const void *vtbl;
  f32 x;          /* 2D-port x of the name's centre */
  f32 y;          /* 2D-port y of the text baseline */
  f32 px;         /* glyph cell height in 2D-port pixels (distance-scaled) */
  u32 alpha;      /* 0..255 (fades out with distance) */
  char name[PUPPET_NAME_BYTES];
} PuppetNameTag;

/* Puppet create: adopt the names block an earlier REL instance left, or allocate and publish it. */
void puppet_nametag_onCreate(void);

/* From the puppet's draw (not parked): queue its name for this frame's 2D pass, unless names are
 * off, the slot has no name, the game hides its HUD, or the puppet is too far / off screen. */
void puppet_nametag_queue(fopAc_ac_c *actor, J3DModel *model, u32 slotIndex, PuppetNameTag *tag);

#endif /* PUPPET_NAMETAG_H */
