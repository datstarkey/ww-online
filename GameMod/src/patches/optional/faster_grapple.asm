; @name        Faster grappling hook
; @description The grappling hook flies out, latches and wraps around its target much faster,
; @description like Wind Waker HD.
; @category    Speed
; @default     off
; @match       no
; @multiplayer Local only.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: tweaks.increase_grapple_animation_speed. r2 (rtoc) = 0x803FFD00, so -0x5F54(r2)
; is 40.0 at 0x803F9DAC and -0x5FD8(r2) is 20.0 at 0x803F9D28 (.sdata2 constants shared with
; other code, so the loads are redirected instead of changing the values).

.open "sys/main.dol"
.org 0x800EE0E4
  lfs f0, -0x5F54 (r2)  ; throw speed: read 40.0 instead of 20.0 (-0x5FD8)
.org 0x800EDB74
  addi r0, r3, 20       ; first-person extend: 20 frames (vanilla 40)
.org 0x800EDEA4
  addi r0, r3, 10       ; third-person extend: 10 frames (vanilla 20)
.org 0x800EEC40
  lfs f3, -0x5FD8 (r2)  ; fall onto the target: read 20.0 instead of 10.0 (-0x60BC)
.org 0x800EECA8
  addi r5, r3, 6        ; wrap-around counter: +6 per frame (vanilla +1)
.org 0x803F9D60
  .float 25.0           ; wrap-around speed (vanilla 17.0; read in one place only)
.close
