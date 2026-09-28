; @name        Fix: Miniblins and Light Arrows
; @description Miniblins killed with Light Arrows now set their death switch, like any other kill.
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer The switch it sets is world state; with shared world it syncs to everyone either way.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm + custom_funcs.asm miniblin_set_death_switch_when_light_arrowed.
; The REL's existing bl to dCcD_Sph::Set at 0x4B44 is re-targeted to the helper in main.dol
; free space. d_a_pt.rel is a loose file. The main.dol block must come first.

.open "sys/main.dol"
.org @NextFreeSpace
.global miniblin_set_death_switch_when_light_arrowed
miniblin_set_death_switch_when_light_arrowed:
  stwu sp, -0x10 (sp)
  mflr r0
  stw r0, 0x14 (sp)
  bl dCcD_Sph__Set     ; the call we replaced
  lbz r0, 0x2B4 (r29)  ; behaviour type param
  cmpwi r0, 0          ; 0 = respawning Miniblin: sets no switch
  beq miniblin_light_arrows_end
  lbz r0, 0x2B8 (r29)  ; switch index to set on death
  stb r0, 0x995 (r29)  ; ...handed to the enemy_ice (Light Arrows death) struct
miniblin_light_arrows_end:
  lwz r0, 0x14 (sp)
  mtlr r0
  addi sp, sp, 0x10
  blr
.close

.open "files/rels/d_a_pt.rel" ; Miniblin
.org 0x4B44
  bl miniblin_set_death_switch_when_light_arrowed
.close
