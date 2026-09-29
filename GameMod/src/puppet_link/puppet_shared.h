/*
 * puppet_shared.h — THE single source of truth for every emulator address shared
 * between the injected C code and the C# client.
 *
 * C side:  every consumer (link_draw_hook.c, puppet_execute.c, puppet_draw.c) includes this.
 * C# side: WWOnline.Client generates Data/PuppetLayout.g.cs from this file on every
 *          build (see GeneratePuppetLayout in WWOnline.Client.csproj). Every
 *          `#define NAME <integer literal>` below becomes `PuppetLayout.NAME` in C#.
 *          Do NOT hand-copy these numbers into C# — reference PuppetLayout instead.
 *
 * Rules:
 *   - One value per #define, integer literal only (hex or decimal) — the generator
 *     skips expressions and function-like macros, so keep derived values as macros
 *     (C) and compute them in C# from the generated literals.
 *   - A hardcoded 0x803F.... address in a .c or .cs file is a bug. Add it here.
 *   - Changing anything here requires re-patching the game (the build stamp check
 *     in the client / dev-test.ps1 will refuse a stale build).
 */
#ifndef PUPPET_SHARED_H
#define PUPPET_SHARED_H

/* ============================================================================
 * Sync region header (16 bytes at PUPPET_SYNC_BASE) — written by C# at 20Hz.
 * ============================================================================ */
/* Actor identity: the hook spawns proc 0xB5 (PROC_RECTANGLE, whose profile slot in
 * f_pc_profile_lst is redirected to g_profile_PUPPET in REL module 0x58). The profile's
 * mPName must be the same proc name, or other actors mistake the puppet for Obj_Smplbg. */
#define PUPPET_PROC_NAME              0xB5
#define PUPPET_REL_MODULE_ID          0x58

#define PUPPET_SYNC_BASE              0x803FD000
#define PUPPET_SYNC_MAGIC             0x50555050  /* "PUPP" */
#define PUPPET_MAX_SLOTS              3
#define PUPPET_HDR_SIZE               0x10
#define PUPPET_HDR_OFF_MAGIC          0x00  /* u32 */
#define PUPPET_HDR_OFF_NUM_ACTIVE     0x04  /* u32 — hook keeps slots 0..N-1 spawned; C# writes highest active slot + 1. A spawned slot whose ACTIVE word is 0 is "parked" (no execute/collision, invisible) */
#define PUPPET_HDR_OFF_WRITE_COUNTER  0x08  /* u32 */

/* ============================================================================
 * Per-slot data — PUPPET_MAX_SLOTS × PUPPET_SLOT_SIZE bytes from PUPPET_SLOT_0.
 * Stride MUST cover every field or adjacent slots overlap (checked below).
 * ============================================================================ */
#define PUPPET_SLOT_SIZE     0x50
#define PUPPET_SLOT_0        0x803FD010
#define PUPPET_SLOT_BASE(i)  (PUPPET_SLOT_0 + ((i) * PUPPET_SLOT_SIZE))

/* Base layout (0x00..0x2F) */
#define PUPPET_SLOT_OFF_ACTIVE            0x00  /* u32 — set to 1 when slot is live */
#define PUPPET_SLOT_OFF_POSX              0x04  /* f32 */
#define PUPPET_SLOT_OFF_POSY              0x08  /* f32 */
#define PUPPET_SLOT_OFF_POSZ              0x0C  /* f32 */
#define PUPPET_SLOT_OFF_ROTY              0x10  /* s16 */
#define PUPPET_SLOT_OFF_ANIM_ID           0x12  /* u16 */
#define PUPPET_SLOT_OFF_ANIM_SPEED        0x14  /* f32 */
#define PUPPET_SLOT_OFF_STATE_FLAGS       0x18  /* u32 */
#define PUPPET_SLOT_OFF_CUR_PROC          0x1C  /* u8 — mCurProc, raw daPyProc value */
#define PUPPET_SLOT_OFF_EQUIP_SWORD       0x1D  /* u8 — 0xFF == unequipped */
#define PUPPET_SLOT_OFF_EQUIP_SHIELD      0x1E  /* u8 — 0xFF == unequipped */
#define PUPPET_SLOT_OFF_VELOCITY_F        0x20  /* f32 (compat: same as NEW SPEED_F) */
#define PUPPET_SLOT_OFF_STICK_ANGLE       0x24  /* s16 */
#define PUPPET_SLOT_OFF_SEQ_NUM           0x26  /* u16 */
#define PUPPET_SLOT_OFF_CLOTHES_TYPE      0x28  /* u8 — APPEARANCE_CLOTHES_* (0=hero, 1=casual, 2=game default) */
#define PUPPET_SLOT_OFF_COLOR_R           0x29  /* u8 — tunic colour; TUNIC_COLOR_DEFAULT_* or 0,0,0 = vanilla */
#define PUPPET_SLOT_OFF_COLOR_G           0x2A  /* u8 */
#define PUPPET_SLOT_OFF_COLOR_B           0x2B  /* u8 */
#define PUPPET_SLOT_OFF_EQUIP_ITEM        0x2C  /* u16 — peer mEquipItem (Link +0x3560); 0x103 = sword in hand, 0 = not written */
#define PUPPET_SLOT_OFF_ACTION_FLAGS      0x2E  /* u8 — PUPPET_ACTION_FLAG_* */
#define PUPPET_SLOT_OFF_PROC_SEQ          0x2F  /* u8 — bumps on every peer proc (re)start (combo swings re-init the same proc) */

/* PUPPET_SLOT_OFF_ACTION_FLAGS bits */
#define PUPPET_ACTION_FLAG_GUARD          0x01  /* peer is guarding (shield up) */

/* v2 extended state (0x30..0x47) — mirrors peer's daPy_lk_c fields 1:1. */
#define PUPPET_SLOT_OFF_SPEED_F           0x30  /* f32  (Link +0x254)  */
#define PUPPET_SLOT_OFF_MAX_NORMAL_SPEED  0x34  /* f32  (Link +0x2A8)  */
#define PUPPET_SLOT_OFF_STICK_DISTANCE    0x38  /* f32  (Link +0x35B0) */
#define PUPPET_SLOT_OFF_MODE_FLG          0x3C  /* u32  (Link +0x3618) */
#define PUPPET_SLOT_OFF_NO_RESET_FLG0     0x40  /* u32  (Link +0x29C)  */
#define PUPPET_SLOT_OFF_NO_RESET_FLG1     0x44  /* u32  (Link +0x2A0)  */

/* v3 held items (0x48..0x4F, puppet_held.c). 3 x 0x50 slots end exactly at PUPPET_PROC_IDS_ADDR,
 * so the slot can't grow again without moving the tracking arrays (checked below). */
#define PUPPET_SLOT_OFF_BODY_ANGLE_X      0x48  /* s16  mBodyAngle.x (Link +0x2B4): aim pitch */
#define PUPPET_SLOT_OFF_BODY_ANGLE_Y      0x4A  /* s16  mBodyAngle.y (Link +0x2B6): aim yaw, relative to shape_angle.y */
#define PUPPET_SLOT_OFF_GRAB_KIND         0x4C  /* u8   PUPPET_GRAB_KIND_*: what the peer carries (mActorKeepGrab) — last field */
/* 0x4D..0x4F: padding, written as 0 */

/* PUPPET_SLOT_OFF_GRAB_KIND values */
#define PUPPET_GRAB_KIND_NONE             0     /* nothing carried, or something the puppet doesn't draw (pot, barrel...) */
#define PUPPET_GRAB_KIND_BOMB             1     /* a bomb (fpcNm_BOMB_e): the REL draws one between the puppet's hands */
#define PUPPET_GRAB_KIND_MAX              1

/* Slot region end — tracking arrays below MUST start past this. */
#define PUPPET_SLOT_REGION_END  (PUPPET_SLOT_0 + (PUPPET_SLOT_SIZE * PUPPET_MAX_SLOTS))

/* ============================================================================
 * Hook tracking arrays — written by link_draw_hook, read per-frame.
 * ============================================================================ */
#define PUPPET_PROC_IDS_ADDR     0x803FD100  /* u32[3] — fopAcM process IDs */
#define PUPPET_ACTOR_PTRS_ADDR   0x803FD10C  /* u32[3] — actor pointers */
#define PUPPET_SPAWN_STATE_ADDR  0x803FD118  /* u32[3] — spawn state machine */
#define PUPPET_SPAWN_COUNTER     0x803FD124  /* u32 — total spawn counter */

/* Spawn state machine values stored in PUPPET_SPAWN_STATE_ADDR[i]. The hook owns these
 * arrays: C# must never zero a live entry (it orphans the puppet actor). */
#define SPAWN_IDLE     0
#define SPAWN_SPAWNED  2
#define SPAWN_FOUND    3

/* ============================================================================
 * Scratch map. Everything we own lives in SCRATCH_REGION_START..SCRATCH_REGION_END,
 * past the end of the DOL's .sbss2 (0x803FCF20..0x803FCFA8 = live game constants —
 * never write below SCRATCH_REGION_START). SCRATCH_REGION_START is the vanilla end of
 * the DOL's bss, where the boot-thread stack's bottom (_stack_end) used to be. main.dol
 * layout from there (DolPatcher; .sbss2 bounds are its DOL constants, not ours):
 *   - No selected patch uses free space: vanilla. The boot stack is 0x803FCFA8..0x8040CFA8
 *     (grows down; its 0xDEADBABE magic word is at 0x803FCFA8, which we never use) and
 *     main() sits near its top, so this region is the stack's never-reached bottom.
 *   - A selected optional patch uses @NextFreeSpace: free space starts at
 *     SCRATCH_REGION_END (free_space_start_offsets.txt; the patcher refuses anything
 *     lower). DolPatcher adds a Text2 section at SCRATCH_REGION_START that holds this
 *     region as zeros, then the custom code, and moves the boot stack right above the
 *     code (8-byte aligned; its magic word sits just past the last code byte). The arena
 *     then starts at the stack top rounded up to 32. This region is no longer stack.
 * Only the scratch bounds below are read by the patcher (ProtectedRegions); no patch may
 * write .sbss2 or this region, and only required patches may write the draw hook.
 * ============================================================================ */
#define SCRATCH_REGION_START      0x803FCFA8
#define SCRATCH_REGION_END        0x803FD200

/* Hook tracking arrays live above (PUPPET_*_ADDR); hook bookkeeping below them. */
#define SPAWN_FRAME_COUNTER_ADDR  0x803FCFB0  /* u32 — settle delay; C# zeroes on scene change */
#define FRAME_COUNTER_ADDR        0x803FD130  /* u32 — ++ every hook call */
#define COUNTER_ADDR              0x803FD134  /* u32 — hook call counter */
#define STATUS_ADDR               0x803FD138  /* u32 — HOOK/TITL/NAME/DONE */
#define DEBUG_PTR_ADDR            0x803FD13C  /* u32 — last daPy_lk_c* seen */
#define RESULT_PTR_ADDR           0x803FD140  /* s32 — last daPy_lk_c::draw result */

/* Peer-gear swap (puppet_execute.c PuppetEquipSwap). While a puppet executes/draws, the save's
 * mSelectEquip[0]/[1] (sword/shield bytes, 0x803C4C16/17) hold the PEER's ids. C# readers must
 * discard samples taken mid-swap (seqlock: read SEQ, read the bytes, read SEQ again; accept only
 * if both SEQ reads are equal and even). C# writers: see puppet_execute.c — the swap end keeps a
 * byte C# changed mid-swap, but a write of the peer's own id mid-swap is indistinguishable and
 * gets reverted, so verify a write on the next tick. Written by the REL only. */
#define PUPPET_EQUIP_SWAP_ACTIVE_ADDR 0x803FD144  /* u32 C: 1 while a swap is in progress, else 0 */
#define PUPPET_EQUIP_SWAP_SEQ_ADDR    0x803FD148  /* u32 C: ++ at swap start and end (odd = in progress) */

/* ============================================================================
 * Per-Link appearance (puppet_appearance.c). Every Link on screen shares one J3DModelData, so
 * the outfit (linktexS3TC ResTIMG header in TEX1) and the tunic colour (a recoloured copy of
 * that texture) are applied per Link at draw time. The REL owns the LOCAL Link's header while
 * it is loaded; C# only publishes the local player's choice here and never writes model data.
 * The REL is only loaded while a puppet exists, so C# keeps a parked puppet alive while the
 * local Link doesn't show the published choice yet (LocalAppearance.NeedsRel).
 * ============================================================================ */
#define LOCAL_APPEARANCE_TAG_ADDR     0x803FD150  /* u32 C#: LOCAL_APPEARANCE_MAGIC while WORD is valid (else REL uses game default + vanilla colour) */
#define LOCAL_APPEARANCE_MAGIC        0x4C415050  /* "LAPP" */
#define LOCAL_APPEARANCE_WORD_ADDR    0x803FD154  /* u32 C#: (clothes << 24) | (R << 16) | (G << 8) | B — clothes = APPEARANCE_CLOTHES_* */
#define LOCAL_APPEARANCE_BLOCK_ADDR   0x803FD158  /* u32 C: game-heap block with the local Link's recoloured tunic; outlives the REL, the next REL adopts it. 0 = none */
#define LOCAL_APPEARANCE_IMAGE_ADDR   0x803FD15C  /* u32 C: image the local hero header points at while recoloured, else 0 (C# compares with the live header) */
#define LOCAL_APPEARANCE_APPLIED_ADDR 0x803FD160  /* u32 C: colour (0xRRGGBB) in IMAGE, 0 = vanilla */
#define LOCAL_APPEARANCE_STATUS_ADDR  0x803FD164  /* u32 C: LOCAL_APPEARANCE_STATUS_* bits, diagnostics only (0 when the REL is not running) */
#define LOCAL_APPEARANCE_STATUS_ACTIVE   0x01     /* headers captured, REL owns the local Link's outfit/colour */
#define LOCAL_APPEARANCE_STATUS_CASUAL   0x02     /* local Link is in casual clothes */
#define LOCAL_APPEARANCE_STATUS_COLOR    0x04     /* local hero tunic is recoloured */
#define LOCAL_APPEARANCE_STATUS_DEFERRED 0x08     /* outfit change waiting for the running event to end */

/* ============================================================================
 * Player names above puppets (puppet_nametag.c). The names live in ONE small block on the game
 * heap, not in scratch (no room): the REL allocates it the first time a puppet is created and
 * publishes its address here. The block is never freed; the next REL instance adopts it, so C#
 * may write it whenever the pointer is in MEM1, the magic matches AND the block's BOOT stamp
 * equals the live __OSStartTime, even while no REL is loaded.
 * The boot stamp matters because this word survives a game reboot (soft reset = OSResetSystem
 * restart, d_s_logo.cpp:503: the DOL is reloaded but RAM is not cleared, and in the vanilla layout
 * the scratch region is untouched boot stack) while the game heap is created afresh: the old
 * pointer and even the old magic can outlive the reboot in memory that now belongs to someone
 * else. OSInit stamps __OSStartTime once per boot (dolphin/os/OS.c:230), so a block from an
 * earlier boot never matches: the REL allocates a new one and C# writes nothing until it has.
 * C# compares the block with what it wants and writes FLAGS / a name only when they differ.
 * Names are printable ASCII (C# sanitises: no Shift-JIS lead bytes), NUL-terminated within
 * PUPPET_NAME_BYTES; the REL copies at most PUPPET_NAME_BYTES - 1 of them.
 * 0x803FD16C..0x803FD16F stays free.
 * ============================================================================ */
#define PUPPET_NAMES_PTR_ADDR         0x803FD168  /* u32 C: names block (game heap), 0 = none yet */
#define PUPPET_NAMES_MAGIC            0x4E414D45  /* "NAME" */
#define PUPPET_NAMES_BLOCK_SIZE       0x58        /* header + PUPPET_MAX_SLOTS names */
#define PUPPET_NAMES_OFF_MAGIC        0x00        /* u32 C: PUPPET_NAMES_MAGIC once the block is set up */
#define PUPPET_NAMES_OFF_FLAGS        0x04        /* u32 C#: PUPPET_NAMES_FLAG_* (0 in a new block) */
#define PUPPET_NAMES_OFF_BOOT         0x08        /* u32[2] C: __OSStartTime (u64) of the boot that allocated the block */
#define PUPPET_NAMES_OFF_NAME0        0x10        /* char[PUPPET_NAME_BYTES] per slot, slot i at + i * PUPPET_NAME_BYTES */
#define PUPPET_NAME_BYTES             24          /* per slot, NUL included */
#define PUPPET_NAME_MAX_CHARS         16          /* C# truncates names to this many characters */
#define PUPPET_NAMES_FLAG_SHOW        0x01        /* the local player wants names shown ("Show player names") */
#define PUPPET_NAMES_NAME(block, i)   ((char *)(block) + PUPPET_NAMES_OFF_NAME0 + ((i) * PUPPET_NAME_BYTES))

#define APPEARANCE_CLOTHES_HERO       0
#define APPEARANCE_CLOTHES_CASUAL     1
#define APPEARANCE_CLOTHES_DEFAULT    2           /* follow the save: playerInit's rule, d_a_player_main.cpp:12362 */
/* "Default" tunic colour = vanilla texture, no recolour (the client's Default preset). 0,0,0 also means vanilla. */
#define TUNIC_COLOR_DEFAULT_R         30
#define TUNIC_COLOR_DEFAULT_G         100
#define TUNIC_COLOR_DEFAULT_B         35
/* dSv_event_flag_c::UNK_2A80: hero's clothes received. playerInit: casual = !isEventBit(0x2A80) || clearCount != 0 */
#define EVENT_BIT_HERO_CLOTHES        0x2A80
#define DAPY_FLG1_CASUAL_CLOTHES      0x08        /* daPyFlg1_CASUAL_CLOTHES on mNoResetFlg1 (d_a_player.h:202) */
#define RESTIMG_OFF_IMAGE_OFFSET      0x1C        /* ResTIMG::imageOffset, relative to the header (JUTTexture.h:36) */

/* ============================================================================
 * Per-slot boat: the peer's King of Red Lions while they ride it. Written by C# at 20Hz with
 * the slot; FLAGS == 0 means no boat. Read by puppet_boat.c, which draws its own copy of the
 * boat (hull + head from the "Ship" archive) and seats the puppet in it. Fields mirror the
 * peer's daShip_c (d_a_ship.h): Y is only used while flying, otherwise the local sea sets it.
 * The pose fields feed the same joint maths as daShip_c's body/head joint callbacks
 * (d_a_ship.cpp:110-231), and the frames/bcks its two mDoExt_McaMorfs play. The block is full:
 * the cannon / crane pose lives in the slot's PUPPET_BOAT_CANNON_* / PUPPET_BOAT_CRANE_* words (below).
 * ============================================================================ */
#define PUPPET_BOAT_0             0x803FD170
#define PUPPET_BOAT_SIZE          0x20  /* 3 slots end at 0x803FD1D0; 0x803FD1D0..DF is reserved (docs/small-keys.md) */
#define PUPPET_BOAT_BASE(i)       (PUPPET_BOAT_0 + ((i) * PUPPET_BOAT_SIZE))
#define PUPPET_BOAT_OFF_FLAGS     0x00  /* u32 — PUPPET_BOAT_FLAG_* */
#define PUPPET_BOAT_OFF_POSX      0x04  /* f32 — current.pos */
#define PUPPET_BOAT_OFF_POSY      0x08  /* f32 */
#define PUPPET_BOAT_OFF_POSZ      0x0C  /* f32 */
#define PUPPET_BOAT_OFF_SPEED_F   0x10  /* f32 — speedF (forward speed, for prediction) */
#define PUPPET_BOAT_OFF_ROTY      0x14  /* s16 — shape_angle.y */
#define PUPPET_BOAT_OFF_SAIL_ANGLE 0x16 /* s16 — mSailAngle: J_FN_SAIL1 turns by -this about Z */
#define PUPPET_BOAT_OFF_TILLER    0x18  /* s16 — m0366: J_FN_STEER1 / J_FN_KAJI turn by this about Z */
#define PUPPET_BOAT_OFF_HEAD_X    0x1A  /* s16 — m03A0: head look pitch (headJointCallBack1, J_FN_KUBI1..6) */
#define PUPPET_BOAT_OFF_HEAD_Y    0x1C  /* s16 — m03A2: head look yaw */
#define PUPPET_BOAT_OFF_MAST_FRAME 0x1E /* u8  — mpBodyAnm frame (the mast bck), whole frames */
#define PUPPET_BOAT_OFF_HEAD_FRAME 0x1F /* u8  — mpHeadAnm frame, whole frames */

/* PUPPET_BOAT_OFF_FLAGS bits */
#define PUPPET_BOAT_FLAG_ACTIVE   0x01  /* peer is riding their boat */
#define PUPPET_BOAT_FLAG_FLY      0x02  /* daShip_c mStateFlag daSFLG_FLY_e: airborne, use POSY */
#define PUPPET_BOAT_FLAG_MAST_ON  0x04  /* m0392 == SHIP_BCK_MAST_ON2 (mast up / rising), else SHIP_BCK_MAST_OFF2 */
#define PUPPET_BOAT_FLAG_MAST_HIDE 0x08 /* m03E8 == 0.001: J_FN_MAST scaled away (cannon or crane in its place) */
/* FLAGS bits 4..5: mPart (daShip_c::Part_e); the cannon / crane is drawn while it is SHIP_PART_CANNON / _CRANE */
#define PUPPET_BOAT_PART_SHIFT    4
#define PUPPET_BOAT_PART_MASK     0x3
/* FLAGS bits 8..15: m03B4, mpHeadAnm's bck (a SHIP_BCK_* index; 0 = keep the current one) */
#define PUPPET_BOAT_HEAD_BCK_SHIFT 8
#define PUPPET_BOAT_HEAD_BCK_MASK 0xFF

/* "Ship" archive bck indices (tww-decomp assets/GZLE01/res/Object/Ship.h, dRes_INDEX_SHIP_BCK_*) */
#define SHIP_BCK_FIRST            0x05  /* AKIBI1: first bck in the archive */
#define SHIP_BCK_DAMAGE1          0x06
#define SHIP_BCK_FN_LOOK_L        0x07  /* the head's resting bck (daShip_c::createHeap) */
#define SHIP_BCK_MAST_OFF2        0x0A
#define SHIP_BCK_MAST_ON2         0x0B
#define SHIP_BCK_LAST             0x0E  /* KYAKKAN1: last bck in the archive */

/* daShip_c::Part_e (d_a_ship.h): mPart, the thing on the mast joint */
#define SHIP_PART_WAIT            0
#define SHIP_PART_STEER           1     /* the sail */
#define SHIP_PART_CANNON          2
#define SHIP_PART_CRANE           3     /* the salvage arm */
#define SHIP_ROPE_MAX             250   /* mRopeCnt's cap: ARRAY_SIZE(mRopeLineSegments) (d_a_ship.cpp:3108-3109) */

/* ============================================================================
 * Per-slot boat parts: the pose of the peer's cannon / crane. The boat block is full, so these are
 * one word per slot in two small free gaps of the scratch region (between SPAWN_FRAME_COUNTER_ADDR
 * and the DBG_* counters, and between CLIENT_DBG_SLOT0_GATES and CLIENT_HEARTBEAT_ADDR). Written by
 * C# with the boat block, before its FLAGS word; FLAGS' PART field (PUPPET_BOAT_PART_*) says which
 * part, if any, is out and so which word the REL reads. Fields mirror the peer's daShip_c
 * (d_a_ship.h) as its cannon / crane joint callbacks use them (d_a_ship.cpp:147-178), plus the
 * crane's rope length (setRopePos, :3148-3272).
 * ============================================================================ */
#define PUPPET_BOAT_CANNON_0          0x803FCFB4
#define PUPPET_BOAT_CANNON_SIZE       0x04  /* 3 slots end at 0x803FCFC0 = DBG_MAGIC_DETECTED_ADDR */
#define PUPPET_BOAT_CANNON_BASE(i)    (PUPPET_BOAT_CANNON_0 + ((i) * PUPPET_BOAT_CANNON_SIZE))
#define PUPPET_BOAT_CANNON_OFF_YAW    0x00  /* s16 — m0394: VFNCN J CANON1 turns by this about X */
#define PUPPET_BOAT_CANNON_OFF_PITCH  0x02  /* s16 — m0396: J CANON2 turns by -this about Y (0..0x4000) */
#define PUPPET_BOAT_CRANE_0           0x803FCFE0
#define PUPPET_BOAT_CRANE_SIZE        0x04  /* 3 slots end at 0x803FCFEC = CLIENT_HEARTBEAT_ADDR */
#define PUPPET_BOAT_CRANE_BASE(i)     (PUPPET_BOAT_CRANE_0 + ((i) * PUPPET_BOAT_CRANE_SIZE))
#define PUPPET_BOAT_CRANE_OFF_ANGLE   0x00  /* s16 — m0398 + m039C: VFNCR V_CRANE_ROTATION turns by -this about Z */
#define PUPPET_BOAT_CRANE_OFF_ROPE    0x02  /* u8  — mRopeCnt: 10-unit rope segments (0..SHIP_ROPE_MAX); 0x03 is spare */

/* Hook debug counters — read by the C# client diagnostics */
#define DBG_MAGIC_DETECTED_ADDR   0x803FCFC0  /* u32 — times magic marker seen */
#define DBG_CREATE_ATTEMPTS_ADDR  0x803FCFC4  /* u32 — fopAcM_create attempts */
#define DBG_CREATE_SUCCESSES_ADDR 0x803FCFC8  /* u32 — fopAcM_create successes */
#define DBG_CREATE_FAILURES_ADDR  0x803FCFCC  /* u32 — fopAcM_create failures */
#define DBG_LAST_DESIRED_ADDR     0x803FCFD0  /* u32 — last desiredCount from header */
#define DBG_LAST_PID_ADDR         0x803FCFD4  /* u32 — last PID from fopAcM_create */
#define DBG_DELETE_ISSUED_ADDR    0x803FCFD8  /* u32 — puppet deletes issued */

/* Shared-world layer 2 (puppet_worldsync.c): C# writes the item bits other players collected
 * in the current stage; the REL despawns matching placed items live. Protocol: C# writes tag=0,
 * then the mask, then tag = WORLDSYNC_TAG_MAGIC | saveTbl. Bits must also be set in live mItem. */
#define WORLDSYNC_ITEM_MASK_ADDR     0x803FD1E0  /* u32 C#: remote-collected mItem bits 0..31 */
#define WORLDSYNC_STAGE_TAG_ADDR     0x803FD1E4  /* u32 C#: WORLDSYNC_TAG_MAGIC | saveTbl, else mask ignored */
#define WORLDSYNC_TAG_MAGIC          0x57530000  /* "WS" */
#define WORLDSYNC_DESPAWN_COUNT_ADDR 0x803FD1E8  /* u32 C: ++ per despawn */
#define WORLDSYNC_LAST_DESPAWN_ADDR  0x803FD1EC  /* u32 C: (saveTbl<<16)|(itemNo<<8)|bit */
/* Scan result, published after every completed scan (every 4th frame while a puppet — parked
 * or not — exists). The tick only runs from daPuppet_Execute, so C# keeps a (parked) puppet
 * alive until a scan newer than its last change reports PENDING == 0 for the current
 * tag + candidates. Written in this order: PENDING, SCAN_TAG, SCAN_CAND, then SCAN_SEQ++. */
#define WORLDSYNC_PENDING_ADDR       0x803FD1F0  /* u32 C: matching placed items still alive after the scan */
#define WORLDSYNC_SCAN_SEQ_ADDR      0x803FD1F4  /* u32 C: ++ per completed scan */
#define WORLDSYNC_SCAN_TAG_ADDR      0x803FD1F8  /* u32 C: the stage tag the scan used */
#define WORLDSYNC_SCAN_CAND_ADDR     0x803FD1FC  /* u32 C: candidates the scan used (mask & live mItem[0]) */

/* Written by the C# client only (diagnostics + heartbeat) */
#define CLIENT_DBG_SLOT0_GATES    0x803FCFDC  /* u32 — slot 0 visibility-gate bitmap */
#define CLIENT_HEARTBEAT_ADDR     0x803FCFEC  /* u32 — ++ every client sync tick */
#define CLIENT_DBG_UNSTABLE_HITS  0x803FCFF0  /* u32 — ticks spent in unstable/hold branch */
#define CLIENT_DBG_STABILITY_BITS 0x803FCFF4  /* u32 — bit0 stable, bit1 sceneChanged, bit2 linkAlive */
#define CLIENT_DBG_LAST_ACTIVE    0x803FCFF8  /* u32 — active count C# last wrote */
#define CLIENT_DBG_WRITE_COUNTER  0x803FCFFC  /* u32 — C# header write counter */

/* ============================================================================
 * daPy_lk_c field offsets — used when mirroring peer state onto the puppet.
 * These come from tww-decomp (include/d/actor/d_a_player[_main].h).
 * ============================================================================ */
#define DAPY_OFF_SPEED_F          0x254
#define DAPY_OFF_NO_RESET_FLG0    0x29C
#define DAPY_OFF_NO_RESET_FLG1    0x2A0
#define DAPY_OFF_MAX_NORMAL_SPEED 0x2A8
#define DAPY_OFF_ANM_RATIO_UNDER0 0x2FB4  /* mAnmRatioUnder[0].mRatio */
#define DAPY_OFF_CURR_LINKTEX     0x338   /* ResTIMG* mpCurrLinktex — the linktexS3TC header in the shared TEX1 */
#define DAPY_OFF_CUR_PROC         0x31D8
#define DAPY_OFF_STICK_DISTANCE   0x35B0
#define DAPY_OFF_VELOCITY         0x35BC
#define DAPY_OFF_MODE_FLG         0x3618
#define DAPY_OFF_EQUIP_ITEM       0x3560  /* u16 mEquipItem */
#define DAPY_OFF_STICK_ANGLE      0x34E8  /* s16 m34E8 — stick direction + camera (what ATN_MOVE blends on) */
#define DAPY_OFF_UPPER_ANM_IDX    0x301C  /* u16 — m_anm_heap_upper[UPPER_MOVE2].mIdx; guard/REST anims below */
#define DAPY_OFF_UNDER_FRAME      0x303C  /* f32 — mFrameCtrlUnder[0].mFrame; goes backwards on a proc restart */
#define DAPY_OFF_CUT_COMBO        0x34C4  /* u8  — m34C4 combo step (changeCutProc ++) */
#define DAPY_OFF_NEXT_EQUIP_ITEM  0x3562  /* u16 — m3562: item held once the REST (draw/sheathe) anim finishes */
#define DAPY_UPPER_ANM_REST       0xD7    /* draw/sheathe upper anim */
/* Item take-out / put-away upper anims (dRes_INDEX_LKANM_BCK_*, GZLE01 LkAnm.h; setAnimeUnequipItem,
 * d_a_player_main.cpp:3498). Like REST, m3562 holds the item Link will hold after the swap frame. */
#define DAPY_UPPER_ANM_TAKE       0x103
#define DAPY_UPPER_ANM_TAKEBOTH   0x104
#define DAPY_UPPER_ANM_TAKEL      0x105
#define DAPY_UPPER_ANM_TAKER      0x106
#define DAPY_OFF_BODY_ANGLE_X     0x2B4   /* s16 — daPy_py_c::mBodyAngle.x (d_a_player.h:491) */
#define DAPY_OFF_BODY_ANGLE_Y     0x2B6   /* s16 — mBodyAngle.y */
#define DAPY_OFF_GRAB_ACTOR       0x3190  /* fopAc_ac_c* — mActorKeepGrab (0x318C, d_a_player_main.h:2088) .mActor (+0x4, :86) */
#define FPC_OFF_PROC_NAME         0x08    /* s16 — base_process_class::mProcName (f_pc_base.h:16) */
#define FPC_NAME_BOMB             0x128   /* fpcNm_BOMB_e (f_pc_name.h:308) */

/* Guard detection (C# sets PUPPET_ACTION_FLAG_GUARD from the local Link's state) */
#define DAPY_PROC_GUARD_0         0x0C
#define DAPY_PROC_GUARD_1         0x0D
#define DAPY_PROC_GUARD_2         0x6D
#define DAPY_UPPER_ANM_GUARD_0    0x16
#define DAPY_UPPER_ANM_GUARD_1    0x1B

/* ============================================================================
 * Compile-time layout checks (typedefs only — emit no code).
 * ============================================================================ */
typedef char puppet_check_slot_fits[
    (PUPPET_SLOT_OFF_NO_RESET_FLG1 + 4 <= PUPPET_SLOT_OFF_BODY_ANGLE_X &&
     PUPPET_SLOT_OFF_BODY_ANGLE_X + 2 <= PUPPET_SLOT_OFF_BODY_ANGLE_Y &&
     PUPPET_SLOT_OFF_BODY_ANGLE_Y + 2 <= PUPPET_SLOT_OFF_GRAB_KIND &&
     PUPPET_SLOT_OFF_GRAB_KIND + 1 <= PUPPET_SLOT_SIZE && (PUPPET_SLOT_SIZE % 4) == 0) ? 1 : -1];
typedef char puppet_check_slots_before_tracking[
    (PUPPET_SLOT_REGION_END <= PUPPET_PROC_IDS_ADDR) ? 1 : -1];
typedef char puppet_check_boat_cannon_words[
    (PUPPET_BOAT_CANNON_0 >= SPAWN_FRAME_COUNTER_ADDR + 4 && PUPPET_BOAT_CANNON_BASE(PUPPET_MAX_SLOTS) <= DBG_MAGIC_DETECTED_ADDR &&
     PUPPET_BOAT_CANNON_0 >= SCRATCH_REGION_START && (PUPPET_BOAT_CANNON_0 % 4) == 0 &&
     PUPPET_BOAT_CANNON_OFF_PITCH + 2 <= PUPPET_BOAT_CANNON_SIZE && (PUPPET_BOAT_CANNON_SIZE % 4) == 0) ? 1 : -1];
typedef char puppet_check_boat_crane_words[
    (PUPPET_BOAT_CRANE_0 >= CLIENT_DBG_SLOT0_GATES + 4 && PUPPET_BOAT_CRANE_BASE(PUPPET_MAX_SLOTS) <= CLIENT_HEARTBEAT_ADDR &&
     PUPPET_BOAT_CRANE_0 >= DBG_DELETE_ISSUED_ADDR + 4 && (PUPPET_BOAT_CRANE_0 % 4) == 0 &&
     PUPPET_BOAT_CRANE_OFF_ROPE + 1 <= PUPPET_BOAT_CRANE_SIZE && (PUPPET_BOAT_CRANE_SIZE % 4) == 0 &&
     PUPPET_BOAT_CRANE_BASE(PUPPET_MAX_SLOTS) <= PUPPET_SYNC_BASE && SHIP_ROPE_MAX <= 0xFF) ? 1 : -1];
typedef char puppet_check_header_before_slots[
    (PUPPET_SYNC_BASE + PUPPET_HDR_SIZE <= PUPPET_SLOT_0) ? 1 : -1];
typedef char puppet_check_bookkeeping_after_tracking[
    (PUPPET_SPAWN_COUNTER + 4 <= FRAME_COUNTER_ADDR) ? 1 : -1];
typedef char puppet_check_scratch_above_sbss2[
    (SPAWN_FRAME_COUNTER_ADDR >= SCRATCH_REGION_START && FRAME_COUNTER_ADDR >= SCRATCH_REGION_START) ? 1 : -1];
typedef char puppet_check_equip_swap_words[
    (PUPPET_EQUIP_SWAP_ACTIVE_ADDR >= RESULT_PTR_ADDR + 4 &&
     PUPPET_EQUIP_SWAP_SEQ_ADDR + 4 <= LOCAL_APPEARANCE_TAG_ADDR) ? 1 : -1];
typedef char puppet_check_appearance_words[
    (LOCAL_APPEARANCE_STATUS_ADDR + 4 <= WORLDSYNC_ITEM_MASK_ADDR) ? 1 : -1];
typedef char puppet_check_worldsync_in_region[
    (WORLDSYNC_SCAN_CAND_ADDR + 4 <= SCRATCH_REGION_END) ? 1 : -1];
typedef char puppet_check_boat_fits[
    (PUPPET_BOAT_OFF_HEAD_FRAME + 1 <= PUPPET_BOAT_SIZE && (PUPPET_BOAT_SIZE % 4) == 0 &&
     PUPPET_BOAT_FLAG_MAST_HIDE < (1 << PUPPET_BOAT_PART_SHIFT) &&
     (PUPPET_BOAT_PART_MASK << PUPPET_BOAT_PART_SHIFT) < (1 << PUPPET_BOAT_HEAD_BCK_SHIFT) &&
     SHIP_PART_CRANE <= PUPPET_BOAT_PART_MASK) ? 1 : -1];
typedef char puppet_check_boats_after_appearance[
    (PUPPET_BOAT_0 >= LOCAL_APPEARANCE_STATUS_ADDR + 4) ? 1 : -1];
typedef char puppet_check_boats_before_worldsync[
    (PUPPET_BOAT_BASE(PUPPET_MAX_SLOTS) <= WORLDSYNC_ITEM_MASK_ADDR) ? 1 : -1];
typedef char puppet_check_names_word[
    (PUPPET_NAMES_PTR_ADDR >= LOCAL_APPEARANCE_STATUS_ADDR + 4 && PUPPET_NAMES_PTR_ADDR + 4 <= PUPPET_BOAT_0 &&
     (PUPPET_NAMES_PTR_ADDR % 4) == 0) ? 1 : -1];
typedef char puppet_check_names_block[
    (PUPPET_NAMES_OFF_NAME0 + (PUPPET_MAX_SLOTS * PUPPET_NAME_BYTES) <= PUPPET_NAMES_BLOCK_SIZE &&
     PUPPET_NAMES_OFF_FLAGS + 4 <= PUPPET_NAMES_OFF_BOOT && PUPPET_NAMES_OFF_BOOT + 8 <= PUPPET_NAMES_OFF_NAME0 &&
     (PUPPET_NAMES_OFF_BOOT % 4) == 0 && PUPPET_NAME_MAX_CHARS < PUPPET_NAME_BYTES &&
     (PUPPET_NAMES_BLOCK_SIZE % 4) == 0) ? 1 : -1];

#endif /* PUPPET_SHARED_H */
