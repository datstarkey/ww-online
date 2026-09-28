; @name        Faster climbing, sidling and NPC chat zoom
; @description Speeds up starting a climb, climbing ladders and vines, sidling along walls, and
; @description halves the camera zoom-in time when you start talking to someone.
; @category    Speed
; @default     off
; @match       no
; @multiplayer Local only.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: tweaks.increase_misc_animations (daPy_lk_c HIO constants + dCamera talk zoom).

.open "sys/main.dol"
.org 0x8035D738
  .float 1.6   ; start a climb (vanilla 0.8)
.org 0x8035DB18
  .float 1.6   ; start climbing a ladder/vine (vanilla 1.0)
.org 0x8035DB20
  .float 1.4   ; finish climbing a ladder/vine (vanilla 0.9)
.org 0x8035DB38
  .float 1.6   ; climb a ladder/vine (vanilla 1.2)
.org 0x8035D6AC
  .float 2.0   ; sidle (vanilla 1.6)
.org 0x8016DA2C
  li r0, 10    ; frames to zoom in on an NPC for a conversation (vanilla 20)
.close
