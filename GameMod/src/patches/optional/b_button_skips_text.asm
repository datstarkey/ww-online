; @name        B button skips text
; @description Hold B to fast-forward dialogue: each line advances as soon as it can, instead of
; @description needing a fresh B press per line.
; @category    Text
; @default     off
; @match       no
; @multiplayer Local only.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: asm/patches/b_button_skips_text.asm. Each site read the "pressed this frame" button
; byte (lbz r0, 0x33(r3)); read the "held" byte at 0x31 instead (g_mDoCPd_cpadInfo).

.open "sys/main.dol"
.org 0x80212E14 ; dMsg_continueProc__FP13sub_msg_class
  lbz r0, 0x31 (r3)
.org 0x80211EF8 ; dMsg_stopProc__FP13sub_msg_class
  lbz r0, 0x31 (r3)
.org 0x80213858 ; dMsg_finishProc__FP13sub_msg_class
  lbz r0, 0x31 (r3)
.org 0x8021211C ; dMsg_selectProc__FP13sub_msg_class
  lbz r0, 0x31 (r3)
.org 0x80213718 ; dMsg_closewaitProc__FP13sub_msg_class
  lbz r0, 0x31 (r3)
.org 0x801E6BF0 ; dMesg_closewaitProc__FP14sub_mesg_class
  lbz r0, 0x31 (r3)
.org 0x801E6A0C ; dMesg_outwaitProc__FP14sub_mesg_class
  lbz r0, 0x31 (r3)
.close
