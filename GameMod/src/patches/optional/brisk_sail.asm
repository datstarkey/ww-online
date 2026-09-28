; @name        Brisk Sail
; @description Like Swift Sail, but KoRL stops even faster when you brake or fall out. The sail is renamed Brisk Sail.
; @category    Sailing
; @default     off
; @match       no
; @multiplayer Local only: your boat's speed and wind are yours; other players see your boat
; @multiplayer where the network puts it.
; @conflicts   swift_sail, sail_controls_wind
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
; @edit        bmg-message 463 Brisk Sail
;
; betterww: tweaks.make_sail_behave_like_brisk_sail2 ("for HD texture pack": behaviour and the
; pause-menu name, no texture swap) + asm/patches/brisk_sail.asm + custom_funcs.asm. The helpers
; live in main.dol free space; the REL's existing bl relocations into main.dol are re-targeted to
; them. At 0x2E1C betterww and wwrando both comment "read 2.0 from 0xDB24" but load 0x08(r31)
; (10.0 at 0xDB08); we keep the code that shipped and was played. d_a_ship.rel is in RELS.arc: only that entry is edited + re-compressed.
; The main.dol block must come first so the REL block can reference its symbols.

.open "sys/main.dol"
.org @NextFreeSpace
.global brisk_sail_set_wind_dir_to_ship_dir
brisk_sail_set_wind_dir_to_ship_dir:
  stwu sp, -0x10 (sp)
  mflr r0
  stw r0, 0x14 (sp)
  bl JAIZelBasic__setShipSailState ; the call this replaced (r3/r4 are still its arguments)
  ; KoRL = g_dComIfG_gameInfo.play (+0x12A0) .mpPlayerPtr[2] (+0x48AC + 8): d_com_inf_game.h:781,905
  lis r3, (g_dComIfG_gameInfo + 0x5B54)@ha
  lwz r3, (g_dComIfG_gameInfo + 0x5B54)@l (r3)
  lha r3, 0x206 (r3)   ; KoRL's Y angle
  neg r3, r3           ; the angle is backwards
  addi r4, r3, 0x4000  ; +90 degrees = the direction KoRL faces
  addi r4, r4, 0x1000  ; +22.5 degrees, then round down to 45 degrees = round to nearest
  rlwinm r4, r4, 0, 0, 18
  li r3, 0
  bl d_kankyo_wether__dKyw_tact_wind_set ; dKyw_tact_wind_set(s16 x, s16 y)
  lwz r0, 0x14 (sp)
  mtlr r0
  addi sp, sp, 0x10
  blr

.global brisk_sail_slow_down_ship_when_stopping
brisk_sail_slow_down_ship_when_stopping:
  stwu sp, -0x10 (sp)
  mflr r0
  stw r0, 0x14 (sp)
  lis r4, brisk_sail_stopping_deceleration@ha
  addi r4, r4, brisk_sail_stopping_deceleration@l
  lfs f3, 0 (r4) ; max deceleration per frame
  lfs f4, 4 (r4) ; min deceleration per frame
  bl cLib_addCalc
  lwz r0, 0x14 (sp)
  mtlr r0
  addi sp, sp, 0x10
  blr
brisk_sail_stopping_deceleration:
  .float 3.0 ; max (vanilla 1.0)
  .float 0.2 ; min (vanilla 0.1)

.global brisk_sail_slow_down_ship_when_idle
brisk_sail_slow_down_ship_when_idle:
  stwu sp, -0x10 (sp)
  mflr r0
  stw r0, 0x14 (sp)
  lis r4, brisk_sail_idle_deceleration@ha
  addi r4, r4, brisk_sail_idle_deceleration@l
  lfs f3, 0 (r4) ; max deceleration per frame
  lfs f4, 4 (r4) ; min deceleration per frame
  bl cLib_addCalc
  lwz r0, 0x14 (sp)
  mtlr r0
  addi sp, sp, 0x10
  blr
brisk_sail_idle_deceleration:
  .float 4.0 ; max (vanilla 1.0)
  .float 0.2 ; min (vanilla 0.05)
.close

.open "files/rels/d_a_ship.rel"
.org 0xB9FC
  bl brisk_sail_set_wind_dir_to_ship_dir  ; the wind always blows the way KoRL faces
.org 0x3A74
  bl brisk_sail_slow_down_ship_when_stopping ; holding A to stop
.org 0x3BC8
  bl brisk_sail_slow_down_ship_when_idle     ; knocked out of the boat while moving
.org 0x3A7C
  lfs f1, 0x44 (r31)   ; max cruising speed: read 30.0 (0xDB44) instead of 10.0 (0xDC54)
.org 0x2DD0            ; daShip_c::decrementShipSpeed
  lfs f3, 0x1A4 (r4)   ; acceleration: read 0.2 (0xDCA4) instead of 0.1 (0xDBEC)
.org 0xDC28
  .float 0.03          ; was 0.015, read only at 0x2DD4
.org 0x2E18            ; daShip_c::firstDecrementShipSpeed
  lfs f3, 0x08 (r31)   ; read 10.0 (0xDB08) instead of 5.0 (0xDB80)
.org 0x2E1C
  lfs f4, 0x08 (r31)   ; read 10.0 (0xDB08) instead of 1.0 (0xDB88) - see the header note
.org 0xDBE8
  .float 110.0         ; sailing speed (vanilla 55.0)
.org 0xDBC0
  .float 160.0         ; initial speed (vanilla 80.0)
.close
