; @name        Faster crawling and rolling
; @description Doubles crawling speed, and rolls start fast (20 to 26 speed instead of 0.5 to 26).
; @category    Speed
; @default     off
; @match       no
; @multiplayer Local only: other players see your position from the network, not your speed.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: tweaks.increase_player_movement_speeds (daPy_lk_c HIO constants in .data).

.open "sys/main.dol"
.org 0x8035D3D0
  .float 0.35294117647058826 ; roll: multiplier on walking speed (6/17; vanilla 1.5)
  .float 20.0                ; roll: base speed (vanilla 0.5)
.org 0x8035DB94
  .float 6.0                 ; crawl speed (vanilla 3.0)
.close
