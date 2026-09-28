; @name        Faster block pushing and pulling
; @description Link pushes and pulls blocks 40% faster and each push/pull takes 12 frames
; @description instead of 20. Includes wwrando's fix for the Forsaken Fortress door this would
; @description otherwise softlock.
; @category    Speed
; @default     off
; @match       no
; @multiplayer Local only; the block ends up where it would anyway.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
; @edit        dzb-face-property files/res/Stage/sea/Room1.arc room.dzb 0x1493 0x11
;
; betterww: tweaks.increase_block_moving_animation. The faster push animation leaves Link a
; couple of units short of the exit triangles when he enters Forsaken Fortress through the left
; half of the big 2F door, softlocking the game; the @edit gives one more collision triangle
; the exit property (wwrando tweaks.fix_forsaken_fortress_door_softlock — betterww lacks it).
; d_a_obj_movebox.rel is in RELS.arc: only that entry is edited and re-compressed.

.open "sys/main.dol"
.org 0x8035DBB0
  .float 1.4 ; Link's push animation speed (vanilla 1.0)
.org 0x8035DBB8
  .float 1.4 ; Link's pull animation speed (vanilla 1.0)
.close

; M_attr__Q212daObjMovebox5Act_c: 13 block types x 0x9C bytes from 0x54B0.
.open "files/rels/d_a_obj_movebox.rel"
.org 0x54B4
  .short 12 ; block type 0: push frames (vanilla 20)
.org 0x54BA
  .short 12 ; block type 0: pull frames (vanilla 20)
.org 0x5550
  .short 12 ; block type 1: push frames (vanilla 20)
.org 0x5556
  .short 12 ; block type 1: pull frames (vanilla 20)
.org 0x55EC
  .short 12 ; block type 2: push frames (vanilla 20)
.org 0x55F2
  .short 12 ; block type 2: pull frames (vanilla 20)
.org 0x5688
  .short 12 ; block type 3: push frames (vanilla 20)
.org 0x568E
  .short 12 ; block type 3: pull frames (vanilla 20)
.org 0x5724
  .short 12 ; block type 4: push frames (vanilla 20)
.org 0x572A
  .short 12 ; block type 4: pull frames (vanilla 20)
.org 0x57C0
  .short 12 ; block type 5: push frames (vanilla 20)
.org 0x57C6
  .short 12 ; block type 5: pull frames (vanilla 20)
.org 0x585C
  .short 12 ; block type 6: push frames (vanilla 20)
.org 0x5862
  .short 12 ; block type 6: pull frames (vanilla 20)
.org 0x58F8
  .short 12 ; block type 7: push frames (vanilla 20)
.org 0x58FE
  .short 12 ; block type 7: pull frames (vanilla 20)
.org 0x5994
  .short 12 ; block type 8: push frames (vanilla 20)
.org 0x599A
  .short 12 ; block type 8: pull frames (vanilla 20)
.org 0x5A30
  .short 12 ; block type 9: push frames (vanilla 20)
.org 0x5A36
  .short 12 ; block type 9: pull frames (vanilla 20)
.org 0x5ACC
  .short 12 ; block type 10: push frames (vanilla 20)
.org 0x5AD2
  .short 12 ; block type 10: pull frames (vanilla 20)
.org 0x5B68
  .short 12 ; block type 11: push frames (vanilla 20)
.org 0x5B6E
  .short 12 ; block type 11: pull frames (vanilla 20)
.org 0x5C04
  .short 12 ; block type 12: push frames (vanilla 20)
.org 0x5C0A
  .short 12 ; block type 12: pull frames (vanilla 20)
.close
