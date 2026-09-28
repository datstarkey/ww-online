; @name        Fix: Jalhalla's Poes and Light Arrows
; @description Jalhalla's small Poes killed with Light Arrows now count towards the fight.
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only: enemies aren't synced.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm + custom_funcs.asm poe_fix_light_arrows_bug (reimplements the
; rest of Big_pow_down_check so a Poe dying to Light Arrows also counts, and un-kills it in the
; last 4 frames before Jalhalla reforms). The REL's existing bl to fopAcM_SearchByID at 0x900 is
; re-targeted to the helper. d_a_pw.rel is a loose file. The main.dol block must come first.

.open "sys/main.dol"
.org @NextFreeSpace
.global poe_fix_light_arrows_bug
poe_fix_light_arrows_bug:
  stwu sp, -0x10 (sp)
  mflr r0
  stw r0, 0x14 (sp)

  lbz r0, 0x285 (r31) ; Poe HP
  extsb. r0, r0
  ble poe_light_arrows_poe_is_dead
  lbz r0, 0x88A (r31) ; dying-to-Light-Arrows counter
  cmpwi r0, 0
  bgt poe_light_arrows_poe_is_dead
  b poe_light_arrows_return_false

poe_light_arrows_poe_is_dead:
  bl fopAcM_SearchByID ; the call we replaced
  cmpwi r3, 0
  beq poe_light_arrows_return_false
  lwz r4, 0x18 (sp)    ; Jalhalla (the original read sp+8; our frame adds 0x10)
  cmplwi r4, 0
  beq poe_light_arrows_return_false
  lha r0, 8 (r4)
  cmpwi r0, 0xD4       ; really a bpw_class?
  bne poe_light_arrows_return_false
  lha r0, 0x446 (r4)
  cmpwi r0, 0x6F       ; Poes running around
  bne poe_light_arrows_unkill_poe
  lha r0, 0x44E (r4)   ; frames until Jalhalla reforms
  cmpwi r0, 3
  ble poe_light_arrows_unkill_poe
  lbz r3, 0x285 (r4)
  addi r0, r3, -1      ; Jalhalla HP - 1
  stb r0, 0x285 (r4)
  lwz r3, 0x18 (sp)
  lbz r0, 0x285 (r3)
  extsb. r0, r0
  bgt poe_light_arrows_not_the_last_poe
  li r0, 1
  stb r0, 0x344 (r31)
poe_light_arrows_not_the_last_poe:
  li r0, 1
  stb r0, 0x345 (r31)
  b poe_light_arrows_return_false

poe_light_arrows_unkill_poe:
  li r0, 4
  stb r0, 0x285 (r31)
  li r0, 0
  stb r0, 0x88A (r31)  ; stop dying to Light Arrows
  li r3, 1
  b poe_light_arrows_end

poe_light_arrows_return_false:
  li r3, 0

poe_light_arrows_end:
  lwz r0, 0x14 (sp)
  mtlr r0
  addi sp, sp, 0x10
  blr
.close

.open "files/rels/d_a_pw.rel" ; Poe
.org 0x8CC
  b 0x8D8 ; drop the HP <= 0 check here; the helper does (HP <= 0 || dying to Light Arrows)
.org 0x900
  bl poe_fix_light_arrows_bug
.org 0x904
  ; The helper replaces the rest of the function: return straight after it.
  lwz r31, 0x1C (r1)
  lwz r0, 0x24 (r1)
  mtlr r0
  addi r1, r1, 0x20
  blr
.close
