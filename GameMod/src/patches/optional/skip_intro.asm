; @name        Skip intro movie
; @description Skips the long intro movie that plays when the game starts.
; @category    Startup
; @default     on
; @match       no
; @multiplayer Local only: nothing about the intro is shared.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/skipintro.asm. Vanilla 0x80232C78 is a bl (movie start) and
; 0x80232C88 a bne on the intro state; nop both.

.open "sys/main.dol"
.org 0x80232C78
  nop
.org 0x80232C88
  nop
.close
