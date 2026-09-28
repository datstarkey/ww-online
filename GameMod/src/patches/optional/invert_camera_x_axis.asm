; @name        Invert camera horizontal axis
; @description Pushing the C-stick left turns the camera right, and vice versa.
; @category    Controls
; @default     off
; @match       no
; @multiplayer Local only.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/invert_camera_x_axis.asm + custom_funcs.asm
; invert_camera_horizontal_axis. The helper goes in main.dol free space (past the scratch region,
; see free_space_start_offsets.txt).

.open "sys/main.dol"
.org @NextFreeSpace
.global invert_camera_horizontal_axis
invert_camera_horizontal_axis:
  lfs f1, 0x10 (r3) ; C-stick X for the camera (the instruction we replaced)
  fneg f1, f1
  b 0x8016248C

.org 0x80162488 ; In updatePad__9dCamera_cFv
  b invert_camera_horizontal_axis
.close
