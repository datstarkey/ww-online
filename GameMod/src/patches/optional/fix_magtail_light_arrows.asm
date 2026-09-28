; @name        Fix: Respawning Magtails and Light Arrows
; @description Respawning Magtails shot in the head with Light Arrows now respawn as intended.
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only: enemies aren't synced.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm + custom_funcs.asm magtail_respawn_when_head_light_arrowed.
; The REL's existing bl to dCcD_GObjInf::GetTgHitObj at 0x6000 is re-targeted to the helper,
; which re-does the lines after the call (0x6004..0x6014, nop'd here). d_a_mt.rel is a loose
; file. The main.dol block must come first.

.open "sys/main.dol"
.org @NextFreeSpace
.global magtail_respawn_when_head_light_arrowed
magtail_respawn_when_head_light_arrowed:
  stwu sp, -0x10 (sp)
  mflr r0
  stw r0, 0x14 (sp)
  bl dCcD_GObjInf__GetTgHitObj ; the call we replaced
  stw r3, 0x40 (sp)    ; original sp+0x30 (+0x10 for our frame)
  addi r0, r30, 0x1874
  stw r0, 0x54 (sp)    ; original sp+0x44
  lwz r0, 0x10 (r3)    ; damage types of the hit
  rlwinm. r0, r0, 0, 11, 11 ; Light Arrows?
  beq magtail_light_arrows_end
  li r0, 1
  stb r0, 0x1CBC (r30) ; respawn this Magtail
magtail_light_arrows_end:
  lwz r0, 0x14 (sp)
  mtlr r0
  addi sp, sp, 0x10
  blr
.close

.open "files/rels/d_a_mt.rel" ; Magtail
.org 0x6000
  bl magtail_respawn_when_head_light_arrowed
.org 0x6004
  nop
  nop
  nop
  nop
  nop
.close
