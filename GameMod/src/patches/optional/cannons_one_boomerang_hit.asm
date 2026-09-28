; @name        Cannons break in one boomerang hit
; @description Wall-mounted cannons are destroyed by one boomerang hit instead of two.
; @category    Combat
; @default     off
; @match       no
; @multiplayer Local only: enemies aren't synced between players.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/necessary_fixes.asm. d_a_obj_canon.rel is in RELS.arc: only that entry
; is edited + re-compressed.

.open "files/rels/d_a_obj_canon.rel" ; Wall-mounted cannon
.org 0x7D0
  addi r0, r3, -2 ; subtract 2 HP per hit (vanilla 1)
.close
