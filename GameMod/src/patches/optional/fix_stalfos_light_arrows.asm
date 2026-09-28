; @name        Fix: Stalfos and Light Arrows
; @description If you cut a Stalfos in half and then kill the upper body with Light Arrows, the
; @description lower body now dies too instead of becoming unkillable (a vanilla softlock).
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only: enemies aren't synced.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm + custom_funcs.asm
; stalfos_kill_lower_body_when_upper_body_light_arrowed. The REL's existing bl to fopAcIt_Judge
; at 0x85CC is re-targeted to the helper in main.dol free space. d_a_st.rel is a loose file.
; The main.dol block must come first so the REL block can reference its symbol.

.open "sys/main.dol"
.org @NextFreeSpace
.global stalfos_kill_lower_body_when_upper_body_light_arrowed
stalfos_kill_lower_body_when_upper_body_light_arrowed:
  stwu sp, -0x10 (sp)
  mflr r0
  stw r0, 0x14 (sp)
  bl fopAcIt_Judge     ; the call we replaced: find the upper body
  cmplwi r3, 0
  beq stalfos_light_arrows_end
  lbz r0, 0x1FAE (r3)  ; frames the upper body has been dying to Light Arrows
  cmpwi r0, 0
  beq stalfos_light_arrows_end
  lbz r0, 0x1FAE (r31) ; lower body's counter
  cmpwi r0, 0
  bne stalfos_light_arrows_end
  li r0, 1
  stb r0, 0x1FAE (r31) ; start the lower body dying to Light Arrows too
stalfos_light_arrows_end:
  lwz r0, 0x14 (sp)
  mtlr r0
  addi sp, sp, 0x10
  blr
.close

.open "files/rels/d_a_st.rel" ; Stalfos
.org 0x85CC
  bl stalfos_kill_lower_body_when_upper_body_light_arrowed
.close
