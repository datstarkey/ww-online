; @name        Fix: Hookshot sight crash on Helmaroc's mask
; @description Fixes a vanilla crash when you aim the hookshot at the broken shards of Helmaroc
; @description King's mask.
; @category    Bug fixes
; @default     on
; @match       no
; @multiplayer Local only.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
;
; betterww: fix_vanilla_bugs.asm / necessary_fixes.asm + custom_funcs.asm
; hookshot_sight_failsafe_check. The mask shards lack the pointer the hookshot sight reads
; (r30); when it's null, take the "hide the sight" path.

.open "sys/main.dol"
.org @NextFreeSpace
.global hookshot_sight_failsafe_check
hookshot_sight_failsafe_check:
  cmplwi r30, 0
  beq hookshot_sight_failsafe
  lwz r0, 0x01C4 (r30) ; the instruction we replaced
  b 0x800F13AC
hookshot_sight_failsafe:
  b 0x800F13C0         ; the code that hides the hookshot sight

.org 0x800F13A8 ; In daHookshot_rockLineCallback
  b hookshot_sight_failsafe_check
.close
