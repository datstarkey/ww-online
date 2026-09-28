; @name        Rainbow Tingle Statue rupee
; @description The special 500-rupee reward for the Tingle Statues cycles through every rupee
; @description colour.
; @category    Cosmetic
; @default     off
; @match       no
; @multiplayer Local only (cosmetic).
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: necessary_fixes.asm (0x800F93F4) + custom_funcs.asm check_animate_rainbow_rupee_color
; + tweaks.make_tingle_statue_reward_rupee_rainbow_colored. Item 0xB8's resource colour index
; (item resource table 0x803842B0 + 0xB8 * 0x24 + 0x14) becomes 7, an unused value the helper
; treats as "animate". The keyframe word lives in main.dol free space next to the code.

.open "sys/main.dol"
.org @NextFreeSpace
.global rainbow_rupee_check_animate_color
rainbow_rupee_check_animate_color:
  cmpwi r0, 7
  beq rainbow_rupee_animate_color
  lfd f1, -0x5DF0 (r2) ; the instruction we replaced
  b 0x800F93F8

rainbow_rupee_animate_color:
  lis r5, rainbow_rupee_keyframe@ha
  addi r5, r5, rainbow_rupee_keyframe@l
  lfs f1, 0 (r5)   ; current keyframe
  lfs f0, 4 (r5)   ; step per drawn frame
  fadds f1, f1, f0
  lfs f0, 8 (r5)   ; max
  fcmpo cr0, f1, f0
  blt rainbow_rupee_store_keyframe
  lfs f1, 0xC (r5) ; wrap to -max (plays backwards)
rainbow_rupee_store_keyframe:
  stfs f1, 0 (r5)
  fabs f1, f1      ; 6 -> 0 -> 6
  b 0x800F9410

.global rainbow_rupee_keyframe
rainbow_rupee_keyframe:
  .float 0.0
  .float 0.04
  .float 6.0
  .float -6.0

.org 0x800F93F4
  b rainbow_rupee_check_animate_color

; Item resource for item 0xB8 ("Rainbow Rupee" in wwrando's item list): the colour index byte
; (vanilla 5) and its three neighbours are rewritten with their vanilla values.
.org 0x80385CA4
  .byte 7, 0x00, 0x00, 0x00
.close
