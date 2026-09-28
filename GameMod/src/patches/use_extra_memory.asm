; WW-Online - Extended Memory Patch (48MB)
; Based on LagoLunatic's use_extra_dolphin_memory.asm
;
; This patch updates the game's code to use more than the standard 24MB of RAM.
; Specifically for WW-Online, we need extra memory for:
; - Puppet REL modules
; - Additional player data structures
; - Animation buffers for multiple players
;
; To enable this feature in Dolphin:
; * Go to Config -> Advanced -> Memory Override
; * Tick the checkbox that says "Enable Emulated Memory Size Override"
; * Change the MEM1 size to 48MB
;
; Note: This is not usable on real hardware - emulator only.

; Memory distribution - we add 24MB total for multiplayer support:
; - 4MB to Archive heap (for additional game resources)
; - 4MB to Game heap (for puppet actors and their data)
; - System heap gets the remaining ~16MB
.set added_total_size,        24 * 1024*1024
.set added_command_heap_size,  0 * 1024*1024
.set added_archive_heap_size,  4 * 1024*1024
.set added_game_heap_size,     4 * 1024*1024

; Vanilla heap sizes
.set vanilla_command_heap_size, 0x00001000
.set vanilla_archive_heap_size, 0x00A51400
.set vanilla_game_heap_size,    0x002CE800

; Calculate new heap sizes
.set command_heap_size, vanilla_command_heap_size + added_command_heap_size
.set archive_heap_size, vanilla_archive_heap_size + added_archive_heap_size
.set game_heap_size, vanilla_game_heap_size + added_game_heap_size
.set command_archive_game_heaps_combined_size, command_heap_size+archive_heap_size+game_heap_size

; Total memory: 24MB + 24MB = 48MB
.set new_total_mem1_size, 24 * 1024*1024 + added_total_size
.set mem1_end_address, 0x80000000 + new_total_mem1_size

; Note: bi2.bin needs to be patched separately (see apply_patches.py for that)
; The bi2.bin patch is handled in the build script

.open "sys/main.dol"

; In mDoMch_Create(void) - set new memory end address (0x83000000 for 48MB)
.org 0x8000C7A4
  lis r0, mem1_end_address@h

; In mDoMch_Create(void) - set command heap size (unchanged at 0x1000)
.org 0x8000C9D0
  li r3, command_heap_size

; In mDoMch_Create(void) - set archive heap size (0x00E51400 = 14.3MB)
.org 0x8000C9DC
  lis r3, archive_heap_size@ha
  addi r3, r3, archive_heap_size@l

; In mDoMch_Create(void) - set game heap size (0x006CE800 = 6.8MB)
.org 0x8000C9EC
  lis r3, game_heap_size@ha
  addi r3, r3, game_heap_size@l

; In mDoMch_Create(void) - update system heap calculation
; System heap size = remaining memory (calculated as negation of combined heaps)
.org 0x8000C844
  addis r3, r31, -command_archive_game_heaps_combined_size@ha
  addi r0, r3, -command_archive_game_heaps_combined_size@l

.close
