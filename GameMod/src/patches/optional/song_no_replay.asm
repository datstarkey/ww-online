; @name        No song replays
; @description Skips the replay where Link conducts a song again after you play it.
; @category    Songs
; @default     off
; @match       no
; @multiplayer Local only: only your own conducting is shortened.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/song_no_replay.asm (daPy_lk_c tact code).

.open "sys/main.dol"
.org 0x8014ECE0
  ; Was bne on the "You conducted..." message still being open; remove the wait.
  nop
.org 0x8014EF28
  ; Was bge waiting for Link's conducting animation to finish; remove the wait.
  nop
.close
