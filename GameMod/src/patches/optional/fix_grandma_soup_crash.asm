; @name        Fix: Grandma's soup crash
; @description Fixes a vanilla crash when you heal Grandma after swapping your empty bottle away
; @description too quickly (she tries to say a message that doesn't exist).
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/fix_vanilla_bugs.asm. d_a_npc_ba1.rel is a loose file (files/rels/).

.open "files/rels/d_a_npc_ba1.rel" ; Grandma
.org 0x16DC
  li r0, 2037 ; was 2041 (missing "no empty bottle" message); use the normal soup message
.close
