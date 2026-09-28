; @name        Instant text boxes
; @description Every text box appears all at once instead of letter by letter, and the
; @description scripted pauses inside messages are removed. Pairs well with "B button skips text".
; @category    Text
; @default     off
; @match       no
; @multiplayer Local only: dialogue isn't shared.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
; @edit        bmg-instant-text
;
; betterww: tweaks.make_all_text_instant. Edits files/res/Msg/bmgres.arc zel_00.bmg: every
; message's initial draw type = 1 (instant), and the control codes 1A 07 00 00 07 (wait) and
; 1A 07 00 00 03 (wait, then dismiss) are removed. No ASM.
