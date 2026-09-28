; @name        Unrestricted King of Red Lions
; @description KoRL no longer turns you back when you sail off the route the story wants
; @description (Dragon Roost, Farore's Pearl, Master Sword checks), and you can board him before
; @description seeing the Dragon Roost intro. The edge of the map still stops you.
; @category    Sailing
; @default     off
; @match       yes
; @multiplayer Changes where you can go before the story allows it. With shared story it keeps
; @multiplayer everyone free to follow each other; mixed settings mean some players get turned back.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/KORL_control.asm (daShip_c::checkOutRange + the boarding check).
; d_a_ship.rel is in RELS.arc: only that entry is edited + re-compressed.

.open "files/rels/d_a_ship.rel"
.org 0x29EC
  b 0x2A50 ; was bne: treat the Dragon Roost intro as seen
.org 0x2A08
  b 0x2A50 ; was bne: treat Farore's Pearl as owned
.org 0x2A24
  b 0x2A34 ; was beq: treat the Master Sword as owned
.org 0xB2D8
  b 0xB2F0 ; was bne: allow boarding before the Dragon Roost intro
.close
