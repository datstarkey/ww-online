; @name        Reveal full sea chart
; @description New save files start with every square of the sea chart drawn in. Existing saves
; @description are not changed.
; @category    Exploration
; @default     off
; @match       no
; @multiplayer Local only: the sea chart isn't synced. Only affects saves started after patching.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/reveal_sea_chart.asm. In the new-game sea chart init loop, store 1
; (drawn) instead of 0 for every square.

.open "sys/main.dol"
.org 0x8005B2CC
  li r4, 1
.close
