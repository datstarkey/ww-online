; @name        Faster Ballad of Gales
; @description Warping with the Ballad of Gales skips the cyclone flight cutscene.
; @category    Songs
; @default     off
; @match       no
; @multiplayer Local only. Other players just see you appear at the destination.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/ballad.asm. d_a_ship.rel lives in RELS.arc: the patcher edits only that
; entry and re-compresses it. 0x7680 is a bl into main.dol (the warp music); its relocation is
; disabled so the REL loader doesn't rewrite the nop.

.open "files/rels/d_a_ship.rel"
.org 0x7A10
  ; Was ble waiting for KoRL to fly high enough before warping.
  nop
.org 0x7680
  ; Was bl to start the warp music, which would keep playing after the warp.
  nop
.close
