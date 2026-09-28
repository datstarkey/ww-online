; @name        Fix: Sea memory fragmentation
; @description Gives distant island models a fixed memory estimate so sailing fragments the game
; @description heap less, and silences a harmless console warning.
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only. Helps long sessions on the sea, where puppets also use the game heap.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm. d_a_lod_bg.rel is in RELS.arc: only that entry is edited and
; re-compressed. 0x478 / 0x4D0 are bl calls into main.dol (JKRHeap::free on a solid heap); their
; relocations are disabled so the REL loader doesn't rewrite the nops.

.open "files/rels/d_a_lod_bg.rel" ; Background island LoD model actor
.org 0xDCC
  li r5, 0xEF0 ; max estimate: Windfall's (most islands need 0x4B0, pre-destruction FF 0x970)
.org 0x478
  nop
.org 0x4D0
  nop
.close
