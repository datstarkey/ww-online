; WW-Online - other players on the sea chart (required)
;
; The sea chart menu (dMenu_Fmap_c) runs while no actor does, so the puppet REL can't draw on it from a
; puppet. dDlst_FMAP_c::draw (0x801BB024, d_menu_fmap.cpp:3485-3489) sets the graf port (r31) and draws the
; chart's J2DScreen; right after that call, with this (r30) and the port (r31) still live, call the REL's
; puppet_seachart_draw(this, port) if one is published at SEACHART_FN_ADDR (puppet_shared.h, 0x803FD12C;
; PuppetLayoutTests checks the address below against the header). The REL sets it while it is loaded and clears
; it when the last puppet is deleted; the scratch region is part of Text2 (this patch's free space), zeroed
; at every boot, so a stale pointer can't survive a reset.

.open "sys/main.dol"
.org @NextFreeSpace
.global seachart_players_hook
seachart_players_hook:
  lis r12, 0x8040
  lwz r12, -0x2ED4 (r12) ; SEACHART_FN_ADDR = 0x803FD12C
  cmpwi r12, 0
  beq seachart_players_done
  mr r3, r30             ; dDlst_FMAP_c* (the menu's fmapDl)
  mr r4, r31             ; the graf port J2DScreen::draw just used
  mtctr r12
  bctrl
seachart_players_done:
  lwz r31, 0xC (r1)      ; the instruction we replaced
  b 0x801BB074

.org 0x801BB070 ; In draw__12dDlst_FMAP_cFv, after bl draw__9J2DScreenFffPC14J2DGrafContext
  b seachart_players_hook
.close
