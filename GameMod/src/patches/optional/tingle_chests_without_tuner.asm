; @name        Tingle chests without the Tingle Tuner
; @description Normal bombs reveal Tingle chests (no Game Boy Advance link needed), and Tingle
; @description chests show on the map once you have the dungeon's compass.
; @category    Exploration
; @default     off
; @match       yes
; @multiplayer Changes chest behaviour: players without it can't reveal those chests themselves.
; @multiplayer Opened chests still sync through shared world as usual.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/tingle_chests_without_tuner.asm. d_a_agbsw0.rel is in RELS.arc: only
; that entry is edited + re-compressed.

.open "sys/main.dol"
; Tingle chests are recognised by their opened flag: 0xF for the five Tingle Statue chests,
; 0x10 for the yellow rupee chest in Dragon Roost Cavern. Skip the checks that hide their icon.
.org 0x801A9F8C ; In treasureSet__12dMenu_Dmap_cFv (big map)
  b 0x801A9F9C
.org 0x8004C68C ; In drawPointGc__6dMap_cFUcfffScsUcUcUcUc (minimap)
  b 0x8004C69C
.close

.open "files/rels/d_a_agbsw0.rel"
.org 0x1CA4 ; In ExeSubT__10daAgbsw0_cFv
  b 0x1CC0  ; skip the checks for an active Tingle Tuner link
.org 0x1D0C ; In ExeSubT__10daAgbsw0_cFv
  nop       ; skip the check that it's a Tingle Bomb specifically
.close
