; @name        Keep the Forest Haven void-out zone
; @description The void-out zone between Forest Haven and the Forbidden Woods stays after you get
; @description Farore's Pearl, so falling on the way there puts you back at the start of the trip
; @description instead of the bottom of Forest Haven.
; @category    Exploration
; @default     off
; @match       yes
; @multiplayer Changes world collision behaviour for whoever has it; mixed settings mean players
; @multiplayer land in different places after the same fall.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/necessary_fixes.asm ("death zone between Forest Haven and Forbidden
; Woods"). d_a_tag_ret.rel is a loose file (files/rels/), restored from vanilla on every build.

.open "files/rels/d_a_tag_ret.rel" ; Void-out death zone
.org 0x22C
  b 0x238 ; was beq: always act as if you don't have Farore's Pearl
.close
