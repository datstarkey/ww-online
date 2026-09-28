; @name        Fix: Morths that fall out of bounds
; @description Morths that fall off the level are deleted, so rooms that wait for every enemy to
; @description die can't get stuck on one you can't reach.
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only: enemies aren't synced.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm. d_a_ks.rel is in RELS.arc: only that entry is edited and
; re-compressed.

.open "files/rels/d_a_ks.rel" ; Morth
.org 0x678 ; In naraku_check__FP8ks_class
  ; When there's no collision below, run the same deletion as for void-out collision.
  beq 0x6B0
.close
