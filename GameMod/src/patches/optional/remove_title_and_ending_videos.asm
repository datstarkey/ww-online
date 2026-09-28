; @name        Remove title and ending videos
; @description Replaces the title-screen loop video and the ending video with a blank one
; @description (saves ~600MB of video reads; the title screen idles on a still image).
; @category    Startup
; @default     off
; @match       no
; @multiplayer Local only.
; @credit      Adapted from betterww (WideBoner) / wwrando (LagoLunatic), MIT
; @edit        replace-file files/thpdemo/title_loop.thp blank.thp
; @edit        replace-file files/thpdemo/end_st_epilogue.thp blank.thp
;
; betterww: tweaks.remove_title_and_ending_videos (assets/blank.thp -> GameMod/assets/blank.thp).
; No ASM: this patch is only file edits. (betterww's option also applied skipintro,
; misc_rando_features and fix_vanilla_bugs; those are separate options here.)
