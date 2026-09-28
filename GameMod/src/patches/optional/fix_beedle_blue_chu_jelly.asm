; @name        Fix: Beedle won't buy Blue Chu Jelly
; @description Beedle no longer buys Blue Chu Jelly, so you can't sell away the jelly Doc Bandam
; @description needs for the Blue Potion.
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only: spoils bag contents never sync.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm / necessary_fixes.asm. d_a_npc_bs1.rel is a loose file.

.open "files/rels/d_a_npc_bs1.rel" ; Beedle
.org 0x214C
  b 0x21DC ; don't treat Blue Chu Jelly as a spoil
.close
