; @name        Sail controls the wind
; @description The wind always blows behind you while the sail is out, at normal sailing speed.
; @category    Sailing
; @default     off
; @match       no
; @multiplayer Local only: wind direction isn't shared.
; @conflicts   swift_sail, brisk_sail
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: "Normal sail controls wind" (normal_sail2 -> asm/patches/normal_sail_wind.asm +
; custom_funcs.asm set_wind_dir_to_ship_dir). The REL's bl into main.dol at 0xB9FC is
; re-targeted to the helper in main.dol free space.

.open "sys/main.dol"
.org @NextFreeSpace
.global sail_controls_wind_set_wind_dir_to_ship_dir
sail_controls_wind_set_wind_dir_to_ship_dir:
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
.close

.open "files/rels/d_a_ship.rel"
.org 0xB9FC
  bl sail_controls_wind_set_wind_dir_to_ship_dir
.close
