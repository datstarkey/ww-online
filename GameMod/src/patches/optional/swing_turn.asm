; @name        Turn while swinging
; @description Steer left and right while swinging on a rope or the grappling hook.
; @category    Controls
; @default     off
; @match       no
; @multiplayer Local only: other players' Links take their facing from the network.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/swing_turn.asm + custom_funcs.asm turn_while_swinging. Borrows the rope
; hang turning logic: the rope-hang rotational acceleration (float at 0x803FA2E8 = -0x5A18(r2))
; is used as a constant turn speed, scaled by the control stick, ignoring < 25% deflection.
; r31 = daPy_lk_c in procRopeSwing__9daPy_lk_cFv; sp+0x68 is a float-conversion slot that
; function already uses.

.open "sys/main.dol"
.org @NextFreeSpace
.global turn_while_swinging
turn_while_swinging:
  lis r3, m_Do_controller_pad__g_mDoCPd_cpadInfo@ha
  addi r3, r3, m_Do_controller_pad__g_mDoCPd_cpadInfo@l
  lfs f0, 0 (r3)        ; main stick X (-1.0 .. 1.0)
  lfs f1, -0x5A18 (r2)  ; base rotational speed
  fmuls f0, f1, f0

  fctiwz f0, f0         ; this frame's speed as an integer
  stfd f0, 0x68 (sp)
  lwz r0, 0x6C (sp)

  fctiwz f1, f1         ; base speed as an integer
  stfd f1, 0x68 (sp)
  lwz r3, 0x6C (sp)

  rlwinm r3, r3, 30, 2, 31 ; base / 4 = the 25% dead zone
  cmpw r0, r3
  bge turn_while_swinging_update_angle
  neg r3, r3
  cmpw r0, r3
  ble turn_while_swinging_update_angle
  b turn_while_swinging_return

turn_while_swinging_update_angle:
  lha r3, 0x020E (r31)  ; shape angle Y
  sub r0, r3, r0
  sth r0, 0x020E (r31)
  sth r0, 0x0206 (r31)  ; current angle Y

turn_while_swinging_return:
  lfs f0, -0x5BA8 (r2)  ; the instruction we replaced
  b 0x8014564C

.org 0x80145648 ; In procRopeSwing__9daPy_lk_cFv
  b turn_while_swinging
.close
