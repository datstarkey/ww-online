/**
 * puppet_execute.c - Execute function implementation for Puppet Link
 *
 * Trimmed port of daPy_lk_c::execute() (tww-decomp src/d/actor/d_a_player_main.cpp)
 * using offset macros instead of struct field access (ww_structs.h has wrong field
 * sizes due to GCC vs Metrowerks compiler padding differences).
 *
 * The puppet is network-authoritative: its position/rotation/proc come from the
 * shared-memory slot written by the C# client. Anything in vanilla execute that
 * touches GLOBAL game state (room loading, player status, controller rumble,
 * attention lock, restarts) must be skipped or guarded here, because the global
 * state belongs to the local player.
 *
 * NOTE: this file is compiled into the REL (d_a_puppet.rel), where .rodata /
 * .rodata.cst4 / .rodata.str1.4 are relocated properly, so float and string
 * literals are fine here (unlike link_draw_hook.c).
 */

#include "puppet_execute.h"

// ============================================================================
// CONSTANTS
// ============================================================================

// c_m3d.cpp: const f32 G_CM3D_F_INF = 1.0e38f; "no ground" is mAcch.GetGroundH() == -G_CM3D_F_INF.
#define G_CM3D_F_INF 1.0e38f

// daPy_FLG1 (d_a_player.h)
#define DAPY_FLG1_FREEZE_STATE   0x00000800
#define DAPY_FLG1_UNK40000       0x00040000
#define DAPY_FLG1_UNK40000000    0x40000000  // vanilla: skip animeUpdate while set

// dBgS_Acch::GROUND_HIT (d_bg_s_acch.h)
#define GROUND_HIT 0x20

// g_dComIfG_gameInfo raw offsets (verified against the vanilla DOL: procSwimWait_init does
// lis/addi r3,0x803C4C08; lwz/stw r0,0x5CC8(r3); procFrontRollCrash_init passes
// gameInfo+0x12A0+0x4700 to dVibration_c::StartShock).
// play is at +0x12A0; field offsets inside play from d_com_inf_game.h (retail USA/PAL comments).
#define GAMEINFO_OFF_PLAYER_STATUS0  0x5CC8  // play.mPlayerStatus[0][0] (play+0x4A28)
#define GAMEINFO_OFF_PLAYER_STATUS1  0x5CCC  // play.mPlayerStatus[0][1]
#define GAMEINFO_OFF_VIBRATION       0x59A0  // play.mVibration, dVibration_c (play+0x4700, size 0x84)
#define DVIBRATION_WORDS             (0x84 / 4)
#define GAMEINFO_OFF_ITEM_LIFE_COUNT  0x5B5C // play.mItemLifeCount  f32 (play+0x48BC) - pending heart delta
#define GAMEINFO_OFF_ITEM_MAGIC_COUNT 0x5B78 // play.mItemMagicCount s16 (play+0x48D8) - pending magic delta
#define GAMEINFO_OFF_ACT_STATUS       0x5BCD // play.mRStatus..mDoStatusForce, 6 x u8 (play+0x492D..0x4932)
#define GAMEINFO_OFF_RSTATUS          0x5BCD // play.mRStatus (u8)
#define DACTSTTS_DEFEND               0x36   // dActStts_DEFEND_e
#define DAPY_STTS0_SHIP_RIDE         0x00010000

// JAIZelBasic::zel_basic (0x803F7710); bgmSetSwordUsing(x) only does field_0x00c9 = x
// (JAIZelBasic.cpp:426). setSwordModel/deleteEquipItem call it for "this" Link.
#define JAIZELBASIC_OFF_SWORD_USING  0xC9

// daPy_lk_c fields not (yet) in ww_inlines.h — offsets from d_a_player_main.h comments.
#define PUPPET_DAPY_MPATTENTION(link) (*(u8 **)((u8 *)(link) + 0x3480))  // dAttention_c* mpAttention
#define PUPPET_DAPY_M3724(link)       ((cXyz *)((u8 *)(link) + 0x3724))  // cXyz m3724
#define PUPPET_DAPY_MNORMALSPEED(link) (*(f32 *)((u8 *)(link) + DAPY_OFF_VELOCITY)) // f32 mNormalSpeed (0x35BC)
#define PUPPET_DAPY_MEQUIPITEM(link)  (*(u16 *)((u8 *)(link) + 0x3560))  // u16 mEquipItem
#define PUPPET_DAPY_SWORDANIM_MANM(link) (*(void **)((u8 *)(link) + 0x2E9C + 0x0C)) // mSwordAnim.mAnm (J3DMtxCalcMayaAnm*)
#define PUPPET_DAPY_SWBLUR_POSBUF(link)  (*(void **)((u8 *)(link) + 0x37E4 + 0x24)) // mSwBlur.mpPosBuffer
#define PUPPET_DAPY_M35EC(link)       (*(f32 *)((u8 *)(link) + 0x35EC))  // sword bck frame used by setItemModel
#define PUPPET_DAPY_UNDER0_FRAME(link) (*(f32 *)((u8 *)(link) + 0x302C + 0x10)) // mFrameCtrlUnder[0].mFrame

// daPy_lk_c::daPyItem_* (d_a_player_main.h:931)
#define DAPY_ITEM_NONE   0x100
#define DAPY_ITEM_SWORD  0x103

// daPy_lk_c::BTN_R (d_a_player_main.h:952); spActionButton() == mItemButton & BTN_R
#define DAPY_BTN_R 0x40

// daPy_RFLG0 UNK1/UNK2: "sword attack collider active this frame" (setCollision starts/moves
// mAtCyl/mAtCps only while these are set). Normally only set by the per-frame cut procs.
#define DAPY_RFLG0_AT_BITS 0x00000003

// dAttention_c (d_attention.h): Lockon() == (mLockOnState == LOCK || (== RELEASE && target)) || (mFlags & 0x20000000)
#define DATTENTION_OFF_LOCKON_STATE  0x18  // u8
#define DATTENTION_OFF_FLAGS         0x20  // u32
#define DATTENTION_FLAG_20000000     0x20000000

// fopAc_ac_c
#define FOPAC_SPEEDF(ac) (*(f32 *)((u8 *)(ac) + DAPY_OFF_SPEED_F))

// PUPPET_SLOT_OFF_EQUIP_ITEM / _ACTION_FLAGS / _PROC_SEQ and PUPPET_ACTION_FLAG_GUARD come
// from puppet_shared.h. A 0 in EQUIP_ITEM / ACTION_FLAGS means "not written" (older client).

// Sword draw / sheathe (d_a_player_main.cpp setAnimeEquipSword :3416, setAnimeUnequip :3535,
// checkItemAction :3893): both play the upper-body REST bck on UPPER_MOVE2 and set m3562 to
// the item Link will hold afterwards; checkItemAction swaps mEquipItem = m3562 at REST frame 7
// (deleteEquipItem + setSwordModel for the sword).
#define PUPPET_DAPY_M3562(link)       (*(u16 *)((u8 *)(link) + 0x3562))  // u16 m3562 (pending equip item)
#define PUPPET_DAPY_UPPER2_ANM(link)  (*(u16 *)((u8 *)(link) + 0x2FFC + 2 * 0x10)) // m_anm_heap_upper[UPPER_MOVE2_e].mIdx
#define LKANM_BCK_REST                0xD7  // dRes_INDEX_LKANM_BCK_REST_e (GZLE01 LkAnm.h)

// Field offsets for setStickData (use #ifndef to avoid redefinition warnings)
#ifndef DAPY_LK_MITEMTRIGGER
#define DAPY_LK_MITEMTRIGGER(link) (*(u8 *)((u8 *)(link) + 0x34C8))
#endif
#ifndef DAPY_LK_MITEMBUTTON
#define DAPY_LK_MITEMBUTTON(link) (*(u8 *)((u8 *)(link) + 0x34C9))
#endif
#ifndef DAPY_LK_M34CA
#define DAPY_LK_M34CA(link) (*(u8 *)((u8 *)(link) + 0x34CA))
#endif
#ifndef DAPY_LK_M34E8_EXEC
#define DAPY_LK_M34E8_EXEC(link) (*(s16 *)((u8 *)(link) + 0x34E8))
#endif

// daPy_PROC values (d_a_player_main.h)
#define PROC_WAIT        0x04
#define PROC_FREE_WAIT   0x05
#define PROC_MOVE        0x06
#define PROC_ATN_MOVE    0x07
#define PROC_CRAWL_MOVE  0x10
#define PROC_CRAWL_AUTO_MOVE 0x11
#define PUPPET_DAPY_UNDER0_RATE(link) (*(f32 *)((u8 *)(link) + 0x302C + 0x0C)) // mFrameCtrlUnder[0].mRate
#define PROC_MOVE_TURN   0x18
#define PROC_SWIM_MOVE   0x37

// Ledge procs (d_a_player_main.h daPyProc_*; d_a_player_hang.inc). How Link gets onto a ledge
// (checkNextMode / front-wall handling, d_a_player_main.cpp:4152-4157, 4860-4876):
//   low step (mFrontWallType 6)  -> SMALL_JUMP hop -> FALL -> LAND
//   waist-high (type 7)          -> HANG_WALL_CATCH -> HANG_CLIMB
//   tall (type 8/9)              -> VERTICAL_JUMP -> HANG_WALL_CATCH / HANG_START -> HANG_WAIT ...
//   backing off an edge          -> HANG_FALL_START -> HANG_UP -> HANG_WAIT / HANG_MOVE / HANG_CLIMB
#define PROC_SMALL_JUMP      0x29
#define PROC_VERTICAL_JUMP   0x2A
#define PROC_HANG_START      0x2B
#define PROC_HANG_FALL_START 0x2C
#define PROC_HANG_UP         0x2D
#define PROC_HANG_WAIT       0x2E
#define PROC_HANG_MOVE       0x2F
#define PROC_HANG_CLIMB      0x30
#define PROC_HANG_WALL_CATCH 0x31

// procHangMove_init's direction (getHangDirectionFromAngle, d_a_player_hang.inc:23-38):
// 2 = left (ANM_HANGMOVEL), 3 = right (ANM_HANGMOVER).
#define HANG_DIR_LEFT  2
#define HANG_DIR_RIGHT 3

// procHangClimb_init's start frame: m_HIO->mWallCatch.m.field_0x2C after a hang
// (d_a_player_hang.inc:259/412/448/483), field_0x30 after a wall catch (:624). Values from
// daPy_HIO_wallCatch_c0::m (d_a_player_HIO_data.inc:167-168).
#define HANG_CLIMB_START_FROM_HANG       0.0f
#define HANG_CLIMB_START_FROM_WALL_CATCH 2.0f

// dBgS_Acch::ROOF_HIT (d_bg_s_acch.h:75); procHangClimb_init refuses while it is set (:558).
#define ACCH_ROOF_HIT 0x200

#define PUPPET_DAPY_M352C(link) (*(s16 *)((u8 *)(link) + 0x352C))  // s16 m352C (front wall normal angle)

// daPy_lk_c::DIR_FORWARD (d_a_player_main.h:959)
#define DAPY_DIR_FORWARD 0

// Ship procs (d_a_player_main.h daPyProc_*)
#define PROC_SHIP_READY      0x86  // climbing in from the water
#define PROC_SHIP_JUMP_RIDE  0x87  // jumping in
#define PROC_SHIP_STEER      0x88
#define PROC_SHIP_PADDLE     0x89
#define PROC_SHIP_SCOPE      0x8A
#define PROC_SHIP_BOOMERANG  0x8B
#define PROC_SHIP_HOOKSHOT   0x8C
#define PROC_SHIP_BOW        0x8D
#define PROC_SHIP_CANNON     0x8E
#define PROC_SHIP_CRANE      0x8F
#define PROC_SHIP_GET_OFF    0x90  // running off and jumping out
#define PROC_SHIP_RESTART    0x91
#define PROC_DEMO_SHIP_SIT   0xD7

// This puppet's boat for the current puppet_execute (PUPPET_class.boat), set by
// daPuppet_Execute in puppet.c around the call; NULL otherwise. Same single-thread reasoning
// as l_puppetFollowState.
PuppetBoat *l_puppetBoat = NULL;

// While the peer's boat is shown (C# only sends it while they ride), seat the puppet in it for
// every proc except the ones that move Link between the boat and the water. Item procs used on
// the boat (bottles, the Wind Waker...) are not ship procs but still ride it.
static int puppet_procSitsInBoat(int proc)
{
  return proc != PROC_SHIP_READY && proc != PROC_SHIP_JUMP_RIDE && proc != PROC_SHIP_GET_OFF;
}

/**
 * puppet_initBoatPose - the ship procs' shared sitting pose: VOYAGE1 + the hair anim
 * (initShipBaseAnime, d_a_player_ship.inc:446-453; it reads nothing from the ship).
 *
 * The real procShip*_init aren't used: they read the GLOBAL ship, i.e. the local King of Red
 * Lions. Instead the puppet ends its current proc through WAIT, gets the pose, and mCurProc is
 * relabelled SHIP_PADDLE (mode flags stay WAIT's). The label is what lets it leave again:
 * procWait_init refuses when mCurProc is already WAIT (d_a_player_main.cpp:6043), and
 * commonProcInit's exit handling does nothing for SHIP_PADDLE (only SHIP_BOW deletes an arrow).
 * No proc function ever runs for a puppet (see puppet_executeBody), so the label does nothing else.
 */
static int puppet_initBoatPose(daPy_lk_c *puppet)
{
  if (DAPY_LK_MCURPROC(puppet) != PROC_WAIT && DAPY_LK_MCURPROC(puppet) != PROC_SHIP_PADDLE)
  {
    if (!daPy_lk_c__procWait_init(puppet))
      return 0;
  }
  daPy_lk_c__initShipBaseAnime(puppet);
  DAPY_LK_MCURPROC(puppet) = PROC_SHIP_PADDLE;
  return 1;
}

// Forward declarations
static void puppet_callModelCalc(J3DModel *model);

// ============================================================================
// GLOBAL-STATE GUARD
// ============================================================================
//
// Many daPy_lk_c functions assume `this` is the local player and read/poke global state:
//   - commonProcInit() (every procX_init) clears dComIfGp player status 0/1
//   - procSwim*_init set SWIM, procCut*_init set SWORD_SWING, etc. on player 0
//   - procFrontRollCrash/Land/Damage_init, setLightSaver: dVibration_c::StartShock (rumble)
//   - procCutRoll_init: dComIfGp_setItemMagicCount(-2); procJumpCutLand_init: setDamagePoint
//     -> dComIfGp_setItemLifeCount (LOCAL hearts/magic)
//   - setSwordModel/deleteEquipItem: mDoAud_bgmSetSwordUsing (global BGM flag)
//   - setShieldGuard: dComIfGp_setRStatus (HUD R-button icon)
//   - checkAttentionLock() == mpAttention->Lockon(), mpAttention is the GLOBAL dAttention_c
//   - jointBeforeCB (runs inside the puppet's model calc) with daPyStts0_SHIP_RIDE set poses
//     the right arm toward the LOCAL ship (setShipRideArmAngle, d_a_player_main.cpp:299-315);
//     jointCB0 / setNeckAngle also branch on SHIP_RIDE / WIND_WAKER_CONDUCT.
// The guard snapshots all of that, presents the puppet with a neutral view (player status 0,
// no Z-lock), and restores the snapshot afterwards. dVibration_c::StartShock only writes
// fields of the dVibration_c struct (d_vibration.cpp), so restoring the struct cancels it.
// puppet_execute runs entirely inside one guard; nested guards are harmless.

// Peer gear. setItemModel picks the equipped models EVERY FRAME from the save's select-equip
// bytes (d_a_player_main.cpp:1222-1300: checkNormalSwordEquip / checkFinalMasterSwordEquip ->
// mpSwgripaModel vs mpSwgripmsModel + glow brk/bck frame; checkMirrorShieldEquip ->
// mpShmsModel vs mpShaModel). Both variants are always built by createHeap (:11924-11944,
// run from playerInit via fopAcM_entrySolidHeap), so no model rebuild is needed on a gear
// change: presenting the peer's bytes while the puppet runs is enough. Same for the held
// blade built by setSwordModel (sword_model_tbl by checkNormalSwordEquip) and the Mirror
// Shield light (checkLightHit/checkMirrorShieldEquip).
#define SAVE_OFF_SELECT_EQUIP_SWORD   0x0E  // g_dComIfG_gameInfo.save...mSelectEquip[0] (0x803C4C16)
#define SAVE_OFF_SELECT_EQUIP_SHIELD  0x0F  // ...mSelectEquip[1] (0x803C4C17)

//
// The C# client reads AND writes these bytes asynchronously (room inventory apply writes the
// room's equipped sword; readers sample the local gear). So:
//   - End only restores a byte that still holds the peer id Begin wrote; if C# changed it in
//     between, C#'s value wins (the saved local value is stale by then).
//   - While any swap is in progress PUPPET_EQUIP_SWAP_ACTIVE_ADDR = 1 and
//     PUPPET_EQUIP_SWAP_SEQ_ADDR is odd (++ at the outermost Begin and End), so C# can discard
//     a sample taken mid-swap (seqlock: read SEQ, bytes, SEQ; accept if equal and even).
// Swaps nest (applyProcInit / the effects guard run inside puppet_execute's guard); only the
// outermost Begin/End touch the published words. GameCube actors run single-threaded.
typedef struct
{
  u8 sword;       // local value before the swap
  u8 shield;
  u8 wroteSword;  // peer value this swap wrote
  u8 wroteShield;
  u8 swapped;
} PuppetEquipSwap;

static u32 l_equipSwapDepth;

static void puppet_equipSwapBegin(daPy_lk_c *link, PuppetEquipSwap *e)
{
  u8 *save = (u8 *)&g_dComIfG_gameInfo;
  u32 slotIndex = *(u32 *)((u8 *)link + sizeof(daPy_lk_c)); // PUPPET_class.slotIndex
  e->swapped = 0;
  if (slotIndex >= PUPPET_MAX_SLOTS || *(volatile u32 *)PUPPET_SYNC_BASE != PUPPET_SYNC_MAGIC)
    return;
  u32 slotBase = PUPPET_SLOT_BASE(slotIndex);
  if (*(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_ACTIVE) == 0)
    return;

  // Publish BEFORE touching the bytes so a C# sample can't see a peer id with SEQ even.
  if (l_equipSwapDepth++ == 0)
  {
    (*(volatile u32 *)PUPPET_EQUIP_SWAP_SEQ_ADDR)++;
    *(volatile u32 *)PUPPET_EQUIP_SWAP_ACTIVE_ADDR = 1;
  }

  e->sword = save[SAVE_OFF_SELECT_EQUIP_SWORD];
  e->shield = save[SAVE_OFF_SELECT_EQUIP_SHIELD];
  e->wroteSword = *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_EQUIP_SWORD);
  e->wroteShield = *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_EQUIP_SHIELD);
  save[SAVE_OFF_SELECT_EQUIP_SWORD] = e->wroteSword;
  save[SAVE_OFF_SELECT_EQUIP_SHIELD] = e->wroteShield;
  e->swapped = 1;
}

static void puppet_equipSwapEnd(PuppetEquipSwap *e)
{
  volatile u8 *save = (volatile u8 *)&g_dComIfG_gameInfo;
  if (!e->swapped)
    return;
  e->swapped = 0;

  // Restore only what is still ours; a different value was written by C# meanwhile.
  if (save[SAVE_OFF_SELECT_EQUIP_SWORD] == e->wroteSword)
    save[SAVE_OFF_SELECT_EQUIP_SWORD] = e->sword;
  if (save[SAVE_OFF_SELECT_EQUIP_SHIELD] == e->wroteShield)
    save[SAVE_OFF_SELECT_EQUIP_SHIELD] = e->shield;

  // Unpublish AFTER the bytes are back.
  if (l_equipSwapDepth != 0 && --l_equipSwapDepth == 0)
  {
    *(volatile u32 *)PUPPET_EQUIP_SWAP_ACTIVE_ADDR = 0;
    (*(volatile u32 *)PUPPET_EQUIP_SWAP_SEQ_ADDR)++;
  }
}

typedef struct
{
  u32 status0;
  u32 status1;
  u32 vibration[DVIBRATION_WORDS];
  f32 itemLifeCount;
  s16 itemMagicCount;
  u8 actStatus[6];
  u8 swordUsing;
  u8 *zelBasic;
  u8 *attention;
  u8 attnLockState;
  u32 attnFlags;
  PuppetEquipSwap equip;
} PuppetGlobalGuard;

static void puppet_guardBegin(daPy_lk_c *link, PuppetGlobalGuard *g)
{
  u8 *gameInfo = (u8 *)&g_dComIfG_gameInfo;
  u32 *vib = (u32 *)(gameInfo + GAMEINFO_OFF_VIBRATION);
  int i;

  g->status0 = *(u32 *)(gameInfo + GAMEINFO_OFF_PLAYER_STATUS0);
  g->status1 = *(u32 *)(gameInfo + GAMEINFO_OFF_PLAYER_STATUS1);
  for (i = 0; i < DVIBRATION_WORDS; i++)
    g->vibration[i] = vib[i];
  g->itemLifeCount = *(f32 *)(gameInfo + GAMEINFO_OFF_ITEM_LIFE_COUNT);
  g->itemMagicCount = *(s16 *)(gameInfo + GAMEINFO_OFF_ITEM_MAGIC_COUNT);
  for (i = 0; i < 6; i++)
    g->actStatus[i] = *(gameInfo + GAMEINFO_OFF_ACT_STATUS + i);

  g->zelBasic = (u8 *)JAIZelBasic__zel_basic;
  g->swordUsing = 0;
  if (g->zelBasic != NULL)
    g->swordUsing = *(g->zelBasic + JAIZELBASIC_OFF_SWORD_USING);

  // Peer's sword/shield ids in the save's select-equip bytes (see PuppetEquipSwap).
  puppet_equipSwapBegin(link, &g->equip);

  // The puppet is not the local player: it doesn't ride the local ship, conduct, etc.
  *(u32 *)(gameInfo + GAMEINFO_OFF_PLAYER_STATUS0) = 0;
  *(u32 *)(gameInfo + GAMEINFO_OFF_PLAYER_STATUS1) = 0;

  // Hide the local player's Z-target lock from the puppet for the duration of the call.
  g->attention = PUPPET_DAPY_MPATTENTION(link);
  g->attnLockState = 0;
  g->attnFlags = 0;
  if (g->attention != NULL)
  {
    g->attnLockState = *(u8 *)(g->attention + DATTENTION_OFF_LOCKON_STATE);
    g->attnFlags = *(u32 *)(g->attention + DATTENTION_OFF_FLAGS);
    *(u8 *)(g->attention + DATTENTION_OFF_LOCKON_STATE) = 0; // LockState_NONE
    *(u32 *)(g->attention + DATTENTION_OFF_FLAGS) = g->attnFlags & ~DATTENTION_FLAG_20000000;
  }
}

static void puppet_guardEnd(PuppetGlobalGuard *g)
{
  u8 *gameInfo = (u8 *)&g_dComIfG_gameInfo;
  u32 *vib = (u32 *)(gameInfo + GAMEINFO_OFF_VIBRATION);
  int i;

  *(u32 *)(gameInfo + GAMEINFO_OFF_PLAYER_STATUS0) = g->status0;
  *(u32 *)(gameInfo + GAMEINFO_OFF_PLAYER_STATUS1) = g->status1;
  for (i = 0; i < DVIBRATION_WORDS; i++)
    vib[i] = g->vibration[i];
  *(f32 *)(gameInfo + GAMEINFO_OFF_ITEM_LIFE_COUNT) = g->itemLifeCount;
  *(s16 *)(gameInfo + GAMEINFO_OFF_ITEM_MAGIC_COUNT) = g->itemMagicCount;
  for (i = 0; i < 6; i++)
    *(gameInfo + GAMEINFO_OFF_ACT_STATUS + i) = g->actStatus[i];
  if (g->zelBasic != NULL)
    *(g->zelBasic + JAIZELBASIC_OFF_SWORD_USING) = g->swordUsing;
  puppet_equipSwapEnd(&g->equip);

  if (g->attention != NULL)
  {
    *(u8 *)(g->attention + DATTENTION_OFF_LOCKON_STATE) = g->attnLockState;
    *(u32 *)(g->attention + DATTENTION_OFF_FLAGS) = g->attnFlags;
  }
}

// ============================================================================
// SWORD STATE
// ============================================================================
//
// ROOT CAUSE of the old "mAnm != 0" halt: every procCut*_init calls
// mSwordAnim.changeBckOnly(getItemAnimeResource(...)) (d_a_player_sword.inc:569 etc.), and
// mDoExt_bckAnm::changeBckOnly asserts mAnm != NULL (m_Do_ext.cpp:464; the string is in the
// retail DOL). mSwordAnim.mAnm is only created by mSwordAnim.init() inside setSwordModel()
// (d_a_player_sword.inc:67), which the real player runs when it DRAWS the sword
// (d_a_player_main.cpp:3979, dproc.inc:2391). setSwordModel also allocates mSwBlur.mpPosBuffer
// (:82), which every cut init writes via setBlurPosResource (JKRReadIdxResource into it), and
// sets mEquipItem = daPyItem_SWORD_e so setItemModel/setDrawHandModel put the blade in the
// hand. The puppet never drew its sword, so those were NULL.

static int puppet_swordReady(daPy_lk_c *link)
{
  return PUPPET_DAPY_MEQUIPITEM(link) == DAPY_ITEM_SWORD &&
         PUPPET_DAPY_SWORDANIM_MANM(link) != NULL &&
         PUPPET_DAPY_SWBLUR_POSBUF(link) != NULL;
}

static int puppet_isSwordProc(int proc)
{
  return (proc >= 0x41 && proc <= 0x4A) || // CUT_A .. CUT_KESA
         (proc >= 0x55 && proc <= 0x5C);   // CUT_TURN .. JUMP_CUT_LAND
}

/**
 * puppet_isRestartableProc - one-shot procs the peer can re-enter back-to-back (combo
 * swings, repeated rolls, repeated hits). Looping procs (WAIT/MOVE/swim/ladder/charge-move)
 * are excluded so an anim loop wrap on the peer can never restart them.
 */
static int puppet_isRestartableProc(int proc)
{
  return (proc >= 0x41 && proc <= 0x4A) ||                       // CUT_A .. CUT_KESA
         proc == 0x55 || proc == 0x56 || proc == 0x5B ||          // CUT_TURN, CUT_ROLL, JUMP_CUT
         proc == 0x1E || proc == 0x21 || proc == 0x22 ||          // FRONT_ROLL, SIDE_ROLL, BACK_JUMP
         proc == 0x24 || proc == 0x66 ||                          // AUTO_JUMP, DAMAGE
         proc == 0x0D || proc == 0x6D || proc == 0x65;            // CROUCH_DEFENSE_SLIP, GUARD_SLIP, GUARD_CRASH
}

/**
 * puppet_setSwordOut - draw / put away the puppet's sword (model + anim + blur buffer).
 * Must run inside the global guard (setSwordModel/deleteEquipItem touch the BGM flag).
 */
static void puppet_setSwordOut(daPy_lk_c *link, int wantOut)
{
  u16 equip = PUPPET_DAPY_MEQUIPITEM(link);

  if (wantOut)
  {
    if (puppet_swordReady(link))
      return;
    if (equip != DAPY_ITEM_NONE)
      daPy_lk_c__deleteEquipItem(link, 0);
    // setSwordModel: setItemHeap() -> the puppet's own mpItemHeaps (created by playerInit),
    // initModel(swa/swms), mSwordAnim.init, blur buffer, glow/tip-stab models.
    // Blade type follows checkNormalSwordEquip() = the LOCAL save's sword (not the peer's).
    daPy_lk_c__setSwordModel(link, 0);
  }
  else if (equip == DAPY_ITEM_SWORD)
  {
    daPy_lk_c__deleteEquipItem(link, 0);
  }
}

// ============================================================================
// LEDGE HANG
// ============================================================================
//
// No proc function ever runs for a puppet (see puppet_executeBody), so only the _init matters:
// it picks the bck and sets mModeFlg (HANG), and animeUpdate plays it. The per-frame hang procs
// (changeHangEndProc, setHangShapeOffset, the procHangMove wall line checks) never run, and the
// position/facing an init writes is restored by puppet_applyProcInit, then overridden by the
// network position every frame (FINAL POSITION OVERRIDE). What remains are the inits that
// check the grabbed ledge before committing; for those the ledge is rebuilt from the peer's
// network position and facing, and a failed check falls back to procHangWait_init (the same
// VJMPCHA hang pose, frozen; no checks, d_a_player_hang.inc:423-432).

/**
 * puppet_netTargetPos - the peer's position from the slot (what the FINAL POSITION OVERRIDE
 * converges to). While a grab starts the puppet can still be lerping up to the ledge, so ledge
 * checks use this instead of the current position. Returns 0 (out untouched) if unusable.
 */
static int puppet_netTargetPos(daPy_lk_c *puppet, cXyz *out)
{
  u32 slotIndex = *(u32 *)((u8 *)puppet + sizeof(daPy_lk_c)); // PUPPET_class.slotIndex
  u32 slotBase;
  f32 x, y, z;

  if (slotIndex >= PUPPET_MAX_SLOTS || *(volatile u32 *)PUPPET_SYNC_BASE != PUPPET_SYNC_MAGIC)
    return 0;
  slotBase = PUPPET_SLOT_BASE(slotIndex);
  if (*(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_ACTIVE) == 0)
    return 0;
  x = *(volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSX);
  y = *(volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSY);
  z = *(volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSZ);
  if (!(x == x && y == y && z == z) ||
      !(x > -1.0e6f && x < 1.0e6f && y > -1.0e6f && y < 1.0e6f && z > -1.0e6f && z < 1.0e6f))
    return 0;
  out->x = x;
  out->y = y;
  out->z = z;
  return 1;
}

/**
 * puppet_hangCatchInit - HANG_START (grab after a vertical jump) / HANG_WALL_CATCH (grab a
 * waist-high ledge).
 *
 * Both inits (d_a_player_hang.inc:172-235, :587-620) read the ledge anchor m3724 and the wall
 * normal angle m352C that the peer's front-wall check left behind; the puppet's are stale. They
 * refuse unless |m3724 - pos|xz <= wallR + 20 and a GroundCross from m3724 - 1.5 * normal +
 * (0, 10, 0) lands within 30.1 of m3724.y, then set pos = that ground point and
 * shape_angle.y = m352C + 0x8000. The peer's network position IS that ground point (the ledge
 * top) and its facing is m352C + 0x8000, so use them as the anchor: the distance check is 0 and
 * the ground probe lands 1.5 units into the ledge top. mCurProc is never ROPE_UP(_HANG) for a
 * puppet (unsupported -> WAIT), so HangStart's unchecked rope path isn't taken.
 */
static int puppet_hangCatchInit(daPy_lk_c *puppet, int proc)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)puppet;
  cXyz anchor = *FOPAC_CURRENT_POS(actor);
  int ok;

  puppet_netTargetPos(puppet, &anchor);
  *DAPY_LK_M3724(puppet) = anchor;
  PUPPET_DAPY_M352C(puppet) = (s16)(FOPAC_SHAPE_ANGLE(actor)->y + 0x8000);
  *FOPAC_CURRENT_POS(actor) = anchor; // restored by puppet_applyProcInit

  if (proc == PROC_HANG_START)
    ok = daPy_lk_c__procHangStart_init(puppet);
  else
    ok = daPy_lk_c__procHangWallCatch_init(puppet);
  if (!ok)
    ok = daPy_lk_c__procHangWait_init(puppet);
  return ok;
}

/**
 * puppet_hangFallStartInit - HANG_FALL_START (backing off an edge and catching it).
 *
 * procHangFallStart_init(cM3dGPla*) (d_a_player_hang.inc:270-349) takes the wall polygon the
 * peer backed over but only reads its normal (GetNP) before replacing it with its own line
 * checks, so a stack plane whose normal points away from the facing is enough: atan2s of it is
 * facing + 0x8000 and the init sets shape_angle.y = that + 0x8000 = the network facing, with a
 * zero setOldRootQuaternion turn. Its ground probes (pos + 50 down, then +/-30 sideways) run from
 * the peer's position, i.e. the ledge top it snapped to; if they miss, hang still.
 */
static int puppet_hangFallStartInit(daPy_lk_c *puppet)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)puppet;
  s16 facing = FOPAC_SHAPE_ANGLE(actor)->y;
  cM3dGPla wall;
  cXyz pos = *FOPAC_CURRENT_POS(actor);

  wall.mNorm.x = -cM_ssin(facing);
  wall.mNorm.y = 0.0f;
  wall.mNorm.z = -cM_scos(facing);
  wall.mDist = 0.0f; // never read by the init
  wall.vtbl = 0;     // never called: GetNP is inline

  puppet_netTargetPos(puppet, &pos);
  *FOPAC_CURRENT_POS(actor) = pos; // restored by puppet_applyProcInit

  if (daPy_lk_c__procHangFallStart_init(puppet, &wall))
    return 1;
  return daPy_lk_c__procHangWait_init(puppet);
}

/**
 * puppet_hangMoveDir - the shimmy direction for HANG_MOVE, from the peer's stick like
 * getHangDirectionFromAngle (m34E8 - shape_angle.y, d_a_player_hang.inc:23-38; the slot's
 * STICK_ANGLE is the peer's m34E8, ROTY its shape_angle.y). Without a usable stick (released,
 * or pushed toward/away from the wall) the previous direction is kept.
 */
static int puppet_hangMoveDir(int prevDir, f32 stickDistance, s16 stickAngle, s16 shapeY)
{
  s16 rel = (s16)(stickAngle - shapeY);

  if (prevDir != HANG_DIR_LEFT && prevDir != HANG_DIR_RIGHT)
    prevDir = HANG_DIR_LEFT;
  if (!(stickDistance > 0.05f) || rel > 0x78E4 || rel < -0x78E4)
    return prevDir;
  if (rel >= 0x071C)
    return HANG_DIR_LEFT;
  if (rel <= -0x071C)
    return HANG_DIR_RIGHT;
  return prevDir;
}

/**
 * puppet_applyProcInit - the ONLY place procX_init functions are called from.
 *
 * Unsupported procs fall back to WAIT; if the puppet is already in WAIT that's a no-op.
 * The puppet's position and facing are network-authoritative, so they are restored after
 * the init (ladder/damage/cut/hang inits reposition or re-aim Link).
 *
 * @param param  extra per-proc data carried in bits 16-23 of the request key (HANG_MOVE:
 *               HANG_DIR_*); 0 otherwise.
 * @return nonzero if the puppet is now in the requested (or fallback) proc. Several
 *         _init functions return FALSE without changing proc (e.g. procWait_init,
 *         procFall_init in demo modes); callers should retry on a later frame.
 */
static int puppet_applyProcInit(daPy_lk_c *puppet, int proc, int param)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)puppet;
  PuppetGlobalGuard guard;
  cXyz savedPos = *FOPAC_CURRENT_POS(actor);
  s16 savedAngleY = FOPAC_CURRENT_ANGLE(actor)->y;
  s16 savedShapeY = FOPAC_SHAPE_ANGLE(actor)->y;
  int ok;

  puppet_guardBegin(puppet, &guard);

  // Sword procs need the sword drawn first (see SWORD STATE). If it can't be drawn, fall
  // back to WAIT rather than halting on the mAnm assert.
  if (puppet_isSwordProc(proc))
  {
    puppet_setSwordOut(puppet, 1);
    if (!puppet_swordReady(puppet))
      proc = PROC_WAIT;
  }

  // Extra parameters: 0 / current facing = the common "no special setup" default.
  switch (proc)
  {
  // Locomotion
  case PROC_MOVE:      ok = daPy_lk_c__procMove_init(puppet); break;
  case PROC_ATN_MOVE:
    // procAtnMove_init returns FALSE if already in ATN_MOVE
    ok = (DAPY_LK_MCURPROC(puppet) == PROC_ATN_MOVE) ? 1 : daPy_lk_c__procAtnMove_init(puppet);
    break;
  case 0x17:           ok = daPy_lk_c__procWaitTurn_init(puppet); break;

  // Crouch / crawl (d_a_player_main.cpp:6384-6511, d_a_player_crawl.inc). Hold procs
  // (0x0C, 0x0E, 0x10-0x12) are keyed by proc id only; the slips are one-shot.
  case 0x0C:           ok = daPy_lk_c__procCrouchDefense_init(puppet); break;
  case 0x0D:           ok = daPy_lk_c__procCrouchDefenseSlip_init(puppet); break; // getDamageVec(&mCyl): embedded, NULL-safe
  case 0x0E:           ok = daPy_lk_c__procCrouch_init(puppet); break;            // also deleteEquipItem (vanilla)
  case 0x0F:           ok = daPy_lk_c__procCrawlStart_init(puppet); break;
  case PROC_CRAWL_MOVE:
  case PROC_CRAWL_AUTO_MOVE:
    // CRAWL_AUTO_MOVE (crawl-space tunnels) only differs in its per-frame logic and in
    // setCrawlMoveDirectionArrow (HUD arrow for the LOCAL player) -> use plain CRAWL_MOVE.
    // Slope pitch/roll (shape_angle.x/z) = 0; the anim rate is driven per frame in execute.
    ok = (DAPY_LK_MCURPROC(puppet) == PROC_CRAWL_MOVE) ? 1 : daPy_lk_c__procCrawlMove_init(puppet, 0, 0);
    break;
  case 0x12:           ok = daPy_lk_c__procCrawlEnd_init(puppet, 1, 0, 0); break;
  // GUARD_CRASH 0x65 has no _init in the game (only procGuardCrash); GUARD_SLIP gives the
  // same shield-knockback look.
  case 0x65:
  case 0x6D:           ok = daPy_lk_c__procGuardSlip_init(puppet); break;
  case PROC_MOVE_TURN: ok = daPy_lk_c__procMoveTurn_init(puppet, 0); break;
  case 0x19:           ok = daPy_lk_c__procSlip_init(puppet); break;
  case 0x1E:           ok = daPy_lk_c__procFrontRoll_init(puppet, 0.0f); break;
  case 0x1F:           ok = daPy_lk_c__procFrontRollCrash_init(puppet); break;
  case 0x21:           ok = daPy_lk_c__procSideRoll_init(puppet); break;
  case 0x22:           ok = daPy_lk_c__procBackJump_init(puppet); break;
  case 0x23:           ok = daPy_lk_c__procBackJumpLand_init(puppet); break;
  case 0x24:           ok = daPy_lk_c__procAutoJump_init(puppet); break;
  case 0x25:           ok = daPy_lk_c__procLand_init(puppet, 0.0f, 0); break;
  case 0x27:           ok = daPy_lk_c__procFall_init(puppet, 0, 0.0f); break;
  case 0x36:           ok = daPy_lk_c__procSwimWait_init(puppet, 0); break;
  case PROC_SWIM_MOVE: ok = daPy_lk_c__procSwimMove_init(puppet, 0); break;

  // Ladder (d_a_player_ladder.inc). The anchor fields m3724/m352C are stale for the puppet;
  // the inits only use them to place/aim Link, which is restored below. One anim per proc
  // change: the hand-over-hand alternation lives in the per-frame procs we don't run.
  case 0x38:           ok = daPy_lk_c__procLadderUpStart_init(puppet); break;
  case 0x39:           ok = daPy_lk_c__procLadderUpEnd_init(puppet, 0); break;
  case 0x3A:           ok = daPy_lk_c__procLadderDownStart_init(puppet); break;
  case 0x3B:           ok = daPy_lk_c__procLadderDownEnd_init(puppet, 0); break;
  case 0x3C:
  {
    cXyz anchor = savedPos;
    ok = daPy_lk_c__procLadderMove_init(puppet, 0, DAPY_DIR_FORWARD, &anchor);
    break;
  }

  // Ledges (see LEDGE HANG). One anim per proc change, like the ladder.
  // SMALL_JUMP (d_a_player_main.cpp:7417-7440): param 0 derives speed.y from the stale m3724
  // (sqrtf of a possibly negative height); param 1 is the fixed-speed variant. Both play
  // ANM_JMPST; speed/mNormalSpeed don't move a puppet (posMove's displacement is discarded).
  case PROC_SMALL_JUMP:      ok = daPy_lk_c__procSmallJump_init(puppet, 1); break;
  // VERTICAL_JUMP (:7451-7466): ANM_VJMP; m352C / mFrontWallType only feed per-frame vars.
  case PROC_VERTICAL_JUMP:   ok = daPy_lk_c__procVerticalJump_init(puppet); break;
  case PROC_HANG_START:
  case PROC_HANG_WALL_CATCH: ok = puppet_hangCatchInit(puppet, proc); break;
  case PROC_HANG_FALL_START: ok = puppet_hangFallStartInit(puppet); break;
  // HANG_UP (hang.inc:383-396): ANM_HANGUP, no checks; the param is the follow-up direction
  // for the per-frame proc only.
  case PROC_HANG_UP:         ok = daPy_lk_c__procHangUp_init(puppet, 0); break;
  // HANG_WAIT (:423-432): the frozen hang pose, no checks.
  case PROC_HANG_WAIT:       ok = daPy_lk_c__procHangWait_init(puppet); break;
  // HANG_MOVE (:459-475): ANM_HANGMOVEL/R by direction; reads only the puppet's own hand
  // positions. The anim rate follows the peer's stick per frame (puppet_executeBody).
  case PROC_HANG_MOVE:
    ok = daPy_lk_c__procHangMove_init(puppet, param == HANG_DIR_RIGHT ? HANG_DIR_RIGHT : HANG_DIR_LEFT);
    break;
  // HANG_CLIMB (:557-574): ANM_VJMPCL; refuses only on mAcch roof hit. The puppet's Acch sits
  // wherever the network put it (can be inside the ledge lip) and CrrPos recomputes the flag
  // every frame, so clear it for the init. Start frame as vanilla picks it.
  case PROC_HANG_CLIMB:
  {
    dBgS_Acch *acch = (dBgS_Acch *)((u8 *)puppet + 0x46C); // mAcch
    ACCH_M_FLAGS(acch) &= ~ACCH_ROOF_HIT;
    ok = daPy_lk_c__procHangClimb_init(puppet, DAPY_LK_MCURPROC(puppet) == PROC_HANG_WALL_CATCH
                                                   ? HANG_CLIMB_START_FROM_WALL_CATCH
                                                   : HANG_CLIMB_START_FROM_HANG);
    break;
  }

  // Sword (puppet_swordReady is guaranteed here). s16 param = target facing.
  case 0x41: ok = daPy_lk_c__procCutA_init(puppet, savedShapeY); break;
  case 0x42: ok = daPy_lk_c__procCutF_init(puppet, savedShapeY); break;
  case 0x43: ok = daPy_lk_c__procCutR_init(puppet, savedShapeY); break;
  case 0x44: ok = daPy_lk_c__procCutL_init(puppet, savedShapeY); break;
  case 0x45: ok = daPy_lk_c__procCutEA_init(puppet); break;
  case 0x46: ok = daPy_lk_c__procCutEB_init(puppet); break;
  case 0x47: ok = daPy_lk_c__procCutExA_init(puppet); break;
  case 0x48: ok = daPy_lk_c__procCutExB_init(puppet); break;
  case 0x49: ok = daPy_lk_c__procCutExMJ_init(puppet, 0); break;
  case 0x4A: ok = daPy_lk_c__procCutKesa_init(puppet); break;
  case 0x55: ok = daPy_lk_c__procCutTurn_init(puppet, 0); break;
  case 0x56: ok = daPy_lk_c__procCutRoll_init(puppet); break;
  case 0x57: ok = daPy_lk_c__procCutRollEnd_init(puppet); break;
  case 0x58: ok = daPy_lk_c__procCutTurnCharge_init(puppet); break;
  case 0x59: ok = daPy_lk_c__procCutTurnMove_init(puppet); break;
  case 0x5B: ok = daPy_lk_c__procJumpCut_init(puppet, 0); break;
  case 0x5C: ok = daPy_lk_c__procJumpCutLand_init(puppet); break;
  // Not supported -> WAIT: 0x5A CUT_REVERSE (needs the daPy_ANM of the interrupted cut),
  // 0x4B-0x54 Boko weapon / hammer (need the weapon actor / hammer model),
  // 0x5D-0x64 parry BT_* (need the target enemy actor).

  // Damage
  case 0x66:           ok = daPy_lk_c__procDamage_init(puppet); break;

  // Sitting in the peer's boat (puppet_boat.c seats it)
  case PROC_SHIP_STEER:
  case PROC_SHIP_PADDLE:
  case PROC_SHIP_SCOPE:
  case PROC_SHIP_BOOMERANG:
  case PROC_SHIP_HOOKSHOT:
  case PROC_SHIP_BOW:
  case PROC_SHIP_CANNON:
  case PROC_SHIP_CRANE:
  case PROC_SHIP_RESTART:
  case PROC_DEMO_SHIP_SIT: ok = puppet_initBoatPose(puppet); break;

  case PROC_WAIT:
  default:
    if (DAPY_LK_MCURPROC(puppet) == PROC_WAIT)
      ok = 1;
    else
      ok = daPy_lk_c__procWait_init(puppet);
    break;
  }

  // No sword hitbox for the puppet: setCollision only enables mAtCyl/mAtCps while resetFlg0
  // UNK1/UNK2 are set, which procJumpCutLand_init sets directly. Remote attacks must not
  // damage the local world.
  DAPY_PY_RESET_FLG0(puppet) &= ~DAPY_RFLG0_AT_BITS;

  *FOPAC_CURRENT_POS(actor) = savedPos;
  FOPAC_CURRENT_ANGLE(actor)->y = savedAngleY;
  FOPAC_SHAPE_ANGLE(actor)->y = savedShapeY;

  puppet_guardEnd(&guard);
  return ok;
}

/**
 * puppet_polyValid - cBgS_PolyInfo::ChkSetInfo() plus the 0..255 bg-index bound that
 * dBgS::GetPolyId0/1 only JUT_ASSERT (compiled out in retail). An unset poly has
 * mPolyIndex 0xFFFF / mBgIndex 0x100 and would index m_chk_element[256] out of bounds.
 */
static int puppet_polyValid(cBgS_PolyInfo *polyInfo)
{
  u16 polyIndex;
  u16 bgIndex;

  if (polyInfo == NULL)
    return 0;
  polyIndex = *(u16 *)((u8 *)polyInfo + 0x00); // mPolyIndex
  bgIndex = *(u16 *)((u8 *)polyInfo + 0x02);   // mBgIndex
  return polyIndex != 0xFFFF && bgIndex < 0x100;
}

// ============================================================================
// PUPPET STICK DATA - Simplified version for non-player actor
// ============================================================================

/**
 * puppet_setStickData - Set stick/button data for puppet based on movement state
 *
 * Unlike the real player which reads from controller, we set stick data based on
 * what the puppet SHOULD be doing (the peer's network proc, or idle).
 *
 * @param link          The puppet
 * @param stickDistance 0 for idle, (0.05..1.0] when moving
 * @param moveAngle     World-space move angle (vanilla m34E8 = stick angle + camera Y)
 */
static void puppet_setStickData(daPy_lk_c *link, f32 stickDistance, s16 moveAngle)
{
  // Save previous button state
  DAPY_LK_M34CA(link) = DAPY_LK_MITEMBUTTON(link);

  // Clear triggers and buttons (puppet doesn't use items)
  DAPY_LK_MITEMTRIGGER(link) = 0;
  DAPY_LK_MITEMBUTTON(link) = 0;

  if (stickDistance > 0.0f)
  {
    DAPY_LK_MSTICKDISTANCE(link) = stickDistance;
    DAPY_LK_M34E8_EXEC(link) = moveAngle;
  }
  else
  {
    DAPY_LK_MSTICKDISTANCE(link) = 0.0f;
    DAPY_LK_M34E8_EXEC(link) = 0;
  }
}

// Talk event system removed - causes softlock because clearing puppet's event
// fields doesn't release the player's global event system.

void puppet_updateRotation(fopAc_ac_c *actor, fopAc_ac_c *playerActor)
{
  s16 angleToPlayer = fopAcM_searchActorAngleY(actor, playerActor);
  FOPAC_CURRENT_ANGLE(actor)->y = angleToPlayer;
  FOPAC_SHAPE_ANGLE(actor)->y = angleToPlayer;
}

/**
 * puppet_updateAttentionInfo - Keep the puppet off every lock-on list
 *
 * Z-target is intentionally disabled for puppets — letting peers lock onto each
 * other is annoying (they're players, not NPCs/enemies) and the talk lock-on
 * flashed softlock corners on older builds. Zeroing the attn flags keeps the
 * puppet off every lock-on list (talk/battle/misc) while leaving all other
 * actor behaviour intact.
 */
void puppet_updateAttentionInfo(fopAc_ac_c *actor)
{
  FOPAC_ATTN_FLAGS(actor) = 0;
}

// ============================================================================
// INLINE HELPER FUNCTIONS
// ============================================================================

static inline void mDoMtx_multVecZero(MTX34 *m, cXyz *dst)
{
  dst->x = m->m[0][3];
  dst->y = m->m[1][3];
  dst->z = m->m[2][3];
}

static inline void cMtx_multVec(MTX34 *m, const cXyz *src, cXyz *dst)
{
  dst->x = m->m[0][0] * src->x + m->m[0][1] * src->y + m->m[0][2] * src->z + m->m[0][3];
  dst->y = m->m[1][0] * src->x + m->m[1][1] * src->y + m->m[1][2] * src->z + m->m[1][3];
  dst->z = m->m[2][0] * src->x + m->m[2][1] * src->y + m->m[2][2] * src->z + m->m[2][3];
}

// ============================================================================
// MODEL CALC HELPER
// ============================================================================

/**
 * puppet_callModelCalc - Call J3DModel::calc() via vtable (vtable[2] = calc)
 */
static void puppet_callModelCalc(J3DModel *model)
{
  if (!model)
    return;

  void **modelVtable = *(void ***)model;
  void (*j3dModelCalc)(J3DModel *) = (void (*)(J3DModel *))modelVtable[2];
  j3dModelCalc(model);
}

// ============================================================================
// MAIN EXECUTE FUNCTION
// ============================================================================

/**
 * puppet_execute - Trimmed daPy_lk_c::execute() for a network-driven puppet.
 * Line references below are to daPy_lk_c::execute() in d_a_player_main.cpp.
 */
static int puppet_executeBody(daPy_lk_c *link)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)link;
  dBgS *bgsp = (dBgS *)((u8 *)&g_dComIfG_gameInfo + 0x12A0); // play.mBgS
  dBgS_Acch *acch = (dBgS_Acch *)((u8 *)link + 0x46C);       // mAcch

  f32 fVar2, fVar3, fVar4;
  int iVar9;

  // setGetDemo / forest water / equipment checks / event mode / debug pos restore:
  // REMOVED for puppet (never participates in demos, has no inventory).

  // checkNoControll() branch — the puppet is never the controlled player.
  FOPAC_ATTN_FLAGS(actor) = 0;
  FOPAC_ACTOR_STATUS(actor) = (FOPAC_ACTOR_STATUS(actor) & 0xFFFFFFC0) | 0x30; // fopAcM_SetStatusMap(this, 0x10)

  // mStts weight setup based on player-0 ship status. puppet_execute masks player status to 0
  // for the whole body, so the puppet is always treated as "not riding the (local) ship".
  u32 playerStatus0 = *(u32 *)((u8 *)&g_dComIfG_gameInfo + GAMEINFO_OFF_PLAYER_STATUS0);
  if ((playerStatus0 & DAPY_STTS0_SHIP_RIDE) == 0)
  {
    DAPY_LK_MSTTS_WEIGHT(link) = 0xFF;
    DAPY_LK_MSTTS_CCMOVE(link)->x = 0.0f;
    DAPY_LK_MSTTS_CCMOVE(link)->y = 0.0f;
    DAPY_LK_MSTTS_CCMOVE(link)->z = 0.0f;
  }
  else
  {
    DAPY_LK_MSTTS_WEIGHT(link) = 0x78;
  }

  // mCameraInfoIdx = dComIfGp_getPlayerCameraID(0): play.mPlayerInfo[0].mCameraID = 0x12A0 + 0x48A8
  DAPY_LK_MCAMERAINFOIDX(link) = (int)(s8)(*(u8 *)((u8 *)&g_dComIfG_gameInfo + 0x5B48));

  // m3748 = current.pos
  DAPY_LK_M3748(link)->x = FOPAC_CURRENT_POS(actor)->x;
  DAPY_LK_M3748(link)->y = FOPAC_CURRENT_POS(actor)->y;
  DAPY_LK_M3748(link)->z = FOPAC_CURRENT_POS(actor)->z;

  // Moving background correction
  dBgS_GndChk *gndChk = ACCH_M_GND(acch);
  cBgS_PolyInfo *polyInfo = (cBgS_PolyInfo *)((u8 *)gndChk + 0x14); // cBgS_GndChk: cBgS_Chk (0x14) then cBgS_PolyInfo

  if (DAPY_LK_MCURPROC(link) != 0x85 &&
      (playerStatus0 & DAPY_STTS0_SHIP_RIDE) == 0 &&
      (DAPY_LK_MMODEFLG(link) & 0x410800) == 0 &&
      DAPY_LK_MCURPROC(link) != 0xA9 &&
      ACCH_M_GROUND_H(acch) != -G_CM3D_F_INF &&
      (DAPY_PY_NO_RESET_FLG0(link) & 0xA0000000) == 0 &&
      puppet_polyValid(polyInfo) &&
      cBgS__ChkPolySafe((cBgS *)bgsp, polyInfo) &&
      dBgS__ChkMoveBG(bgsp, polyInfo))
  {
    dBgS__MoveBgCrrPos(bgsp, polyInfo, (ACCH_M_FLAGS(acch) >> 5) & 1,
                       FOPAC_CURRENT_POS(actor), FOPAC_CURRENT_ANGLE(actor), FOPAC_SHAPE_ANGLE(actor));
    dBgS__MoveBgCrrPos(bgsp, polyInfo, (ACCH_M_FLAGS(acch) >> 5) & 1,
                       (cXyz *)FOPAC_OLD(actor), NULL, NULL);
  }

  // m34DE / m35B4 / m34EA saves
  DAPY_LK_M34DE(link) = FOPAC_SHAPE_ANGLE(actor)->y;
  DAPY_LK_M35B4(link) = DAPY_LK_MSTICKDISTANCE(link);
  DAPY_LK_M34EA(link) = DAPY_LK_M34DC(link);

  // Do/R/A status: SKIP for puppet (don't modify HUD)

  DAPY_LK_MFRONTWALLTYPE(link) = 0;

  if ((DAPY_PY_RESET_FLG0(link) & 0x100000) != 0) // daPyRFlg0_POISON_CURSE
  {
    daPy_lk_c__setDamageCurseEmitter(link);
  }
  if ((DAPY_PY_RESET_FLG0(link) & 0x8000000) != 0) // daPyRFlg0_NOT_ATTACKING
  {
    DAPY_PY_CUT_TYPE(link) = 0;
  }

  DAPY_PY_RESET_FLG0(link) = 0;

  // daPy_matAnm_c::decMorfFrame/decMabaTimer - handled globally by the real player

  if (DAPY_LK_MCURPROC(link) == 0xA5 && FOPAC_EVENT_COMMAND(actor) == 6)
  {
    DAPY_PY_DEMO_TYPE(link) = 5;
  }

  // setActorPointer/setAtnList/stopDoButtonQuake - REMOVED for puppet (global tables / controller)

  // Frame morph counter (m34C2)
  if (DAPY_LK_M34C2(link) == 8)
  {
    void *old_fdata = DAPY_LK_M_OLD_FDATA(link);
    if (old_fdata != NULL)
    {
      f32 *morfCounter = (f32 *)((u8 *)old_fdata + 0x04); // mOldFrameMorfCounter
      if (*morfCounter <= 0.0f)
      {
        DAPY_LK_M34C2(link) = 9;
      }
    }
  }
  else
  {
    DAPY_LK_M34C2(link) = 0;
  }

  // Grab/equip actor management - REMOVED for puppet

  // Animation update
  u32 noResetFlg1 = DAPY_PY_NO_RESET_FLG1(link);
  if ((noResetFlg1 & DAPY_FLG1_FREEZE_STATE) == 0)
  {
    if ((noResetFlg1 & DAPY_FLG1_UNK40000000) == 0)
    {
      daPy_lk_c__animeUpdate(link);
    }
  }
  else if ((noResetFlg1 & DAPY_FLG1_UNK40000) == 0)
  {
    // mDoGph_gInf_c::fadeIn for frozen - skip for puppet (global screen fade)
    DAPY_PY_NO_RESET_FLG1(link) |= DAPY_FLG1_UNK40000;
  }

  // setDemoData - skipped. setStickData equivalent is done in puppet_readNetworkState.
  // Demo/dead/swim/damage proc changes, item actions and attack checks are skipped: proc
  // transitions come from the network.

  // Network-driven movement. PUPPET_class.slotIndex is the first field after the
  // embedded daPy_lk_c. sizeof(daPy_lk_c) here is the GCC size (0x4C30), which is
  // >= the real Metrowerks size (0x4C28), so the field never overlaps game data;
  // the profile's mSize is sizeof(PUPPET_class) so the allocation covers it.
  {
    u32 slotIndex = *(u32 *)((u8 *)link + sizeof(daPy_lk_c));
    if (slotIndex >= PUPPET_MAX_SLOTS)
    {
      slotIndex = 0; // Out-of-range slot index, fallback to slot 0
    }
    puppet_readNetworkState(link, (int)slotIndex);
  }

  // The real proc function (mCurProcFunc) is NOT run: procMove/procWait call checkNextMode(),
  // which reads GLOBAL input state (dComIfGp_getDoStatus etc.) and would make the puppet
  // mirror the local player's actions. Proc transitions come from the network instead.
  //
  // What vanilla procMove() does every frame that we still need is the walk/run blend:
  // setBlendMoveAnime(-1.0f) (d_a_player_main.cpp procMove). procMove_init already did the
  // initial setBlendMoveAnime(m_HIO->mBasic.m.field_0xC). mNormalSpeed/mMaxNormalSpeed were
  // written from the network by puppet_readNetworkState. The guard hides the local player's
  // Z-lock so checkAttentionLock() can't select ATN (strafe) anims for the puppet.
  {
    int curProcNow = DAPY_LK_MCURPROC(link);
    if (curProcNow == PROC_MOVE)
    {
      daPy_lk_c__setBlendMoveAnime(link, -1.0f);
    }
    else if (curProcNow == PROC_ATN_MOVE)
    {
      // procAtnMove(): setSpeedAndAngleAtn() then setBlendAtnMoveAnime(-1.0f). The strafe
      // direction comes from current.angle.y (move dir) vs shape_angle.y (facing); use the
      // network stick angle (STICK_ANGLE = the peer's m34E8, Link +0x34E8) as the move direction.
      if (DAPY_LK_MSTICKDISTANCE(link) > 0.05f)
        FOPAC_CURRENT_ANGLE(actor)->y = DAPY_LK_M34E8_EXEC(link);
      daPy_lk_c__setBlendAtnMoveAnime(link, -1.0f);
    }
    else if (curProcNow == PROC_CRAWL_MOVE)
    {
      // procCrawlMove (d_a_player_crawl.inc): frameCtrl.setRate(getCrawlMoveAnmSpeed()) while
      // the stick is pushed, 0 otherwise. Backward crawling (negative rate) isn't mirrored.
      PUPPET_DAPY_UNDER0_RATE(link) =
          (DAPY_LK_MSTICKDISTANCE(link) > 0.05f) ? daPy_lk_c__getCrawlMoveAnmSpeed(link) : 0.0f;
    }
    else if (curProcNow == PROC_HANG_MOVE)
    {
      // procHangMove: frameCtrl.setRate(getHangMoveAnmSpeed()) every frame
      // (d_a_player_hang.inc:485-486); a pure function of mStickDistance (the peer's stick).
      PUPPET_DAPY_UNDER0_RATE(link) = daPy_lk_c__getHangMoveAnmSpeed(link);
    }
    else if (puppet_isSwordProc(curProcNow) && puppet_swordReady(link))
    {
      // Every cut proc does m35EC = mFrameCtrlUnder[UNDER_MOVE0_e].getFrame() per frame
      // (e.g. procCutA, d_a_player_sword.inc:591); setItemModel uses m35EC as the blade's
      // bck frame (mSwordAnim.entry, d_a_player_main.cpp:1211).
      PUPPET_DAPY_M35EC(link) = PUPPET_DAPY_UNDER0_FRAME(link);
    }
  }

  // Item action (vanilla execute :11369, every frame): advances the sword draw/sheathe
  // REST upper-body anim started in puppet_readNetworkState and swaps mEquipItem = m3562 at
  // frame 7 (deleteEquipItem, + setSwordModel when drawing), then drops the upper anim
  // when it finishes. Side effects are puppet-local apart from what the execute guard undoes.
  daPy_lk_c__checkItemAction(link);

  // Shield guard (R held). Vanilla execute calls setShieldGuard() every frame
  // (d_a_player_main.cpp:11382); it raises the ATNG upper-body anim while
  // spActionButton() (mItemButton & BTN_R) && dComIfGp_getRStatus() == dActStts_DEFEND_e,
  // and blends it out otherwise. Drive both from the peer's guard flag; the global RStatus
  // write is undone by the execute-wide guard.
  {
    u8 actionFlags = 0;
    u32 slotIndex = *(u32 *)((u8 *)link + sizeof(daPy_lk_c));
    if (slotIndex < PUPPET_MAX_SLOTS && *(volatile u32 *)PUPPET_SYNC_BASE == PUPPET_SYNC_MAGIC)
    {
      u32 slotBase = PUPPET_SLOT_BASE(slotIndex);
      if (*(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_ACTIVE) != 0)
        actionFlags = *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_ACTION_FLAGS);
    }
    if ((actionFlags & PUPPET_ACTION_FLAG_GUARD) != 0)
    {
      DAPY_LK_MITEMBUTTON(link) |= DAPY_BTN_R;
      *((u8 *)&g_dComIfG_gameInfo + GAMEINFO_OFF_RSTATUS) = DACTSTTS_DEFEND;
    }
    else
    {
      DAPY_LK_MITEMBUTTON(link) &= ~DAPY_BTN_R;
    }
    daPy_lk_c__setShieldGuard(link);
  }

  daPy_lk_c__playTextureAnime(link);

  // Position before movement
  fVar2 = FOPAC_CURRENT_POS(actor)->x;
  fVar3 = FOPAC_CURRENT_POS(actor)->y;
  fVar4 = FOPAC_CURRENT_POS(actor)->z;

  // posMove(): run it for its animation bookkeeping (m34C2 old-frame translate state,
  // m3700, mFootData, m359C, stts cc-move clear), but DISCARD its displacement. Its
  // posMoveFromFootPos() recomputes speedF from mNormalSpeed (which holds the network
  // velocity for the walk/run blend), applies gravity to speed.y and does
  // current.pos += speed — that would double-apply the peer's movement on top of the
  // network lerp. The puppet is network-authoritative, so we restore pos/old.pos and
  // zero speed afterwards.
  {
    cXyz *oldPos = (cXyz *)FOPAC_OLD(actor);
    f32 oldX = oldPos->x;
    f32 oldY = oldPos->y;
    f32 oldZ = oldPos->z;

    daPy_lk_c__posMove(link);

    FOPAC_CURRENT_POS(actor)->x = fVar2;
    FOPAC_CURRENT_POS(actor)->y = fVar3;
    FOPAC_CURRENT_POS(actor)->z = fVar4;
    oldPos->x = oldX;
    oldPos->y = oldY;
    oldPos->z = oldZ;
    FOPAC_SPEED(actor)->x = 0.0f;
    FOPAC_SPEED(actor)->y = 0.0f;
    FOPAC_SPEED(actor)->z = 0.0f;
  }

  // mOldSpeed = speed
  DAPY_LK_MOLDSPEED(link)->x = FOPAC_SPEED(actor)->x;
  DAPY_LK_MOLDSPEED(link)->y = FOPAC_SPEED(actor)->y;
  DAPY_LK_MOLDSPEED(link)->z = FOPAC_SPEED(actor)->z;

  // mAcch.ClrGroundHit(); mAcch.CrrPos(*dComIfG_Bgsp());
  ACCH_M_FLAGS(acch) &= ~GROUND_HIT;
  dBgS_Acch__CrrPos(acch, bgsp);

  daPy_lk_c__setWaterY(link);
  daPy_lk_c__autoGroundHit(link);

  // checkLavaFace - REMOVED for puppet (would trigger lava death/restart)

  // Mode-specific position corrections
  if ((DAPY_LK_MMODEFLG(link) & 0x40000) == 0) // !ModeFlg_SWIM
  {
    iVar9 = DAPY_LK_MCURPROC(link);
    if (iVar9 == 0xA9) // DEMO_TOOL
    {
      FOPAC_CURRENT_POS(actor)->x = fVar2;
      FOPAC_CURRENT_POS(actor)->y = fVar3;
      FOPAC_CURRENT_POS(actor)->z = fVar4;
      if (DAPY_LK_M3574(link) != 0 && ACCH_M_GROUND_H(acch) != -G_CM3D_F_INF)
      {
        FOPAC_CURRENT_POS(actor)->y = ACCH_M_GROUND_H(acch);
      }
    }
    // Vanilla restores the post-posMove position (sp8), undoing CrrPos, for these procs.
    // posMove's displacement is discarded above, so sp8 == (fVar2, fVar3, fVar4) here.
    else if (iVar9 == 0x85 ||                                  // HOOKSHOT_FLY
             (playerStatus0 & DAPY_STTS0_SHIP_RIDE) != 0 ||
             (DAPY_LK_MMODEFLG(link) & 0x10400800) != 0)       // ROPE|LADDER|CAUGHT
    {
      FOPAC_CURRENT_POS(actor)->x = fVar2;
      FOPAC_CURRENT_POS(actor)->y = fVar3;
      FOPAC_CURRENT_POS(actor)->z = fVar4;
    }
    else if (iVar9 == 0xC1 || iVar9 == 0x2E ||                 // DEMO_DOOR_OPEN / HANG_WAIT
             iVar9 == 0x2F || iVar9 == 0x2D ||                 // HANG_MOVE / HANG_UP
             iVar9 == 0x2B || iVar9 == 0x2C)                   // HANG_START / HANG_FALL_START
    {
      FOPAC_CURRENT_POS(actor)->x = fVar2;
      FOPAC_CURRENT_POS(actor)->z = fVar4;
    }
  }
  else
  {
    // Swimming mode
    if ((DAPY_PY_NO_RESET_FLG0(link) & 0x100) != 0)
    {
      if (daPy_lk_c__checkSwimFallCheck(link) == 0)
      {
        FOPAC_CURRENT_POS(actor)->y = DAPY_LK_M35D0(link); // mWaterY
      }
    }

    if (DAPY_LK_MCURPROC(link) == 0xB2) // DEMO_DEAD
    {
      if ((ACCH_M_FLAGS(acch) & GROUND_HIT) != 0)
      {
        FOPAC_CURRENT_POS(actor)->y = ACCH_M_GROUND_H(acch);
        if (FOPAC_SPEED(actor)->y < 0.0f)
        {
          FOPAC_SPEED(actor)->y = 0.0f;
        }
      }
    }
    else
    {
      f32 swimGroundY = ACCH_M_GROUND_H(acch) + 84.9f;
      if (FOPAC_CURRENT_POS(actor)->y < swimGroundY &&
          (ACCH_M_FLAGS(acch) & GROUND_HIT) != 0)
      {
        FOPAC_CURRENT_POS(actor)->y = swimGroundY;
        if (FOPAC_SPEED(actor)->y < 0.0f)
        {
          FOPAC_SPEED(actor)->y = 0.0f;
        }
      }
    }
  }

  // Ground code and room info. Vanilla gates on GroundH != -G_CM3D_F_INF; we also require
  // a valid ground poly so GetRoomId/GetGroundCode/GetAttributeCode never see an unset one.
  int hasGround = ACCH_M_GROUND_H(acch) != -G_CM3D_F_INF && puppet_polyValid(polyInfo);
  if (!hasGround)
  {
    DAPY_LK_MRESTARTPOINT(link) = 0xFF;
    DAPY_LK_M34E2(link) = 0;
    DAPY_LK_MMTRLSNDID(link) = 0;
    DAPY_LK_M357C(link) = DAPY_LK_M3580(link);
    DAPY_LK_M3580(link) = -1;
    DAPY_LK_MCURRATTRIBUTECODE(link) = 0x1B; // dBgS_Attr_UNK1B_e
    // checkFallCode removed for puppet - puppet shouldn't trigger room restarts
  }
  else
  {
    daPy_lk_c__setRoomInfo(link);
    DAPY_LK_M357C(link) = DAPY_LK_M3580(link);
    DAPY_LK_M3580(link) = dBgS__GetGroundCode(bgsp, polyInfo);

    if ((DAPY_PY_NO_RESET_FLG0(link) & 0x80000000) == 0)
    {
      DAPY_LK_MCURRATTRIBUTECODE(link) = dBgS__GetAttributeCode(bgsp, polyInfo);
    }
    else
    {
      DAPY_LK_MCURRATTRIBUTECODE(link) = 0;
    }

    // dStage_RoomCheck(&mAcch.m_gnd) - REMOVED for puppet. It is GLOBAL: it runs
    // roomControl zoneCountCheck (clears room switches, removes zones) and loadRoom with the
    // puppet's room load list, which flags every loaded room not in that list for deletion
    // (d_stage.cpp dStage_roomControl_c::loadRoom) — i.e. it would unload the local player's
    // rooms. setRoomInfo() above already sets the puppet's own roomNo/tevStr.
    // checkFallCode and startRestartRoom removed for puppet - puppet shouldn't trigger restarts

    if ((ACCH_M_FLAGS(acch) & GROUND_HIT) == 0)
    {
      // Not on ground
      DAPY_LK_M34E2(link) = 0;
      iVar9 = DAPY_LK_MCURPROC(link);
      if (iVar9 == 0xA9)
      {
        DAPY_LK_MMTRLSNDID(link) = dBgS__GetMtrlSndId(bgsp, polyInfo);
      }
      else if (iVar9 == 0x86 || iVar9 == 0x90) // SHIP_READY / SHIP_GET_OFF
      {
        DAPY_LK_MMTRLSNDID(link) = 9;
      }
      else
      {
        DAPY_LK_MMTRLSNDID(link) = 0;
      }
    }
    else
    {
      // On ground. checkLavaFace removed for puppet - go straight to flag management.
      if ((DAPY_PY_NO_RESET_FLG0(link) & 0x20000) != 0)
      {
        DAPY_PY_RESET_FLG0(link) |= 0x40; // AUTO_JUMP_LAND
      }
      DAPY_PY_NO_RESET_FLG0(link) &= ~0x20000;

      if ((DAPY_PY_NO_RESET_FLG0(link) & 0x400000) != 0)
      {
        DAPY_PY_RESET_FLG0(link) |= 0x200; // ROPE_JUMP_LAND
      }
      DAPY_PY_NO_RESET_FLG0(link) &= ~0x400000;

      if (FOPAC_CURRENT_POS(actor)->y < 2000.0f)
      {
        if (dBgS__GetSpecialCode(bgsp, polyInfo) != 1)
        {
          cM3dGPla *triPla = cBgS__GetTriPla_1((cBgS *)bgsp,
                                               (int)(u16)polyInfo->mBgIndex,
                                               (int)(u16)polyInfo->mTriIdx);
          if (triPla != NULL && triPla->mNorm.y >= 0.5f)
          {
            DAPY_PY_NO_RESET_FLG0(link) &= ~0x10; // DEKU_SP_RETURN_FLG
          }
        }
      }

      // Moving background: vanilla translates m3724 (not m3748)
      if ((DAPY_PY_NO_RESET_FLG0(link) & 0xA0000000) == 0 && dBgS__ChkMoveBG(bgsp, polyInfo))
      {
        dBgS__MoveBgTransPos(bgsp, polyInfo, 1, PUPPET_DAPY_M3724(link), NULL, NULL);
      }

      if ((DAPY_PY_NO_RESET_FLG0(link) & 0x80000000) == 0)
      {
        DAPY_LK_MMTRLSNDID(link) = dBgS__GetMtrlSndId(bgsp, polyInfo);
        DAPY_LK_M34E2(link) = daPy_lk_c__getGroundAngle(link, polyInfo, FOPAC_SHAPE_ANGLE(actor)->y);
      }
      else
      {
        DAPY_LK_MMTRLSNDID(link) = 0;
        DAPY_LK_M34E2(link) = 0;
      }

      if ((DAPY_LK_MMODEFLG(link) & 0x2000000) != 0)
      {
        daPy_lk_c__setShapeAngleOnGround(link);
      }
    }

    // Animation sound - REMOVED for puppet

    // Steps offset (!checkPlayerFly() && proc != DEMO_TOOL)
    if ((DAPY_LK_MMODEFLG(link) & 0x10452822) == 0 && DAPY_LK_MCURPROC(link) != 0xA9)
    {
      u8 bVar1 = DAPY_LK_M34C3(link);
      if (bVar1 == 1 || bVar1 == 4 || bVar1 == 10 || bVar1 == 9)
      {
        daPy_lk_c__setStepsOffset(link);
      }
    }

    // mDoAud_setLinkGroupInfo - REMOVED for puppet (GLOBAL audio group state)
  }

  // Equipment actor room sync - REMOVED for puppet (no equipped actors)

  // ========================================
  // FINAL POSITION OVERRIDE — puppet is network-authoritative
  // ========================================
  // Acch CrrPos / autoGroundHit above are the real-Link collision pipeline: push out of
  // walls, snap to floor, clamp Y at ceilings. For a puppet that mirrors a remote peer,
  // those clamps would make the puppet fall behind whenever the peer climbs a ledge, jumps
  // off a cliff, or is anywhere the local collider can't walk. Re-apply the peer's target
  // with the same lerp/snap as puppet_readNetworkState. Done BEFORE setWorldMatrix / model
  // calc so the drawn body, hands, equipment and colliders all use this frame's position.
  {
    u32 slotIndex = *(u32 *)((u8 *)link + sizeof(daPy_lk_c));
    if (slotIndex < PUPPET_MAX_SLOTS)
    {
      u32 slotBase = PUPPET_SLOT_0 + (slotIndex * PUPPET_SLOT_SIZE);
      volatile u32 *sync_magic = (volatile u32 *)PUPPET_SYNC_BASE;
      volatile u32 *slot_active = (volatile u32 *)slotBase;
      if (*sync_magic == PUPPET_SYNC_MAGIC && *slot_active != 0)
      {
        u8 peerProc = *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_CUR_PROC);
        cXyz seat;
        csXyz seatAngle;
        if (l_puppetBoat != NULL && puppet_procSitsInBoat(peerProc) &&
            puppet_boatSeat(l_puppetBoat, &seat, &seatAngle))
        {
          // In the peer's boat (puppet_boat.c): pinned to the seat and tilted with the hull,
          // as setShipRidePos does (d_a_player_ship.inc:165-239). The puppet isn't SHIP_RIDE,
          // so setWorldMatrix applies shape_angle x/y/z directly (d_a_player_main.cpp:9544).
          // The peer's own Link position is seat-on-THEIR-sea; ours matches our boat.
          *FOPAC_CURRENT_POS(actor) = seat;
          *FOPAC_SHAPE_ANGLE(actor) = seatAngle;
          FOPAC_CURRENT_ANGLE(actor)->y = seatAngle.y;
          l_puppetBoat->seated = 1;
        }
        else
        {
          if (l_puppetBoat != NULL && l_puppetBoat->seated)
          {
            // Out of the boat: drop the hull's tilt (nothing else sets the puppet's x/z).
            FOPAC_SHAPE_ANGLE(actor)->x = 0;
            FOPAC_SHAPE_ANGLE(actor)->z = 0;
            l_puppetBoat->seated = 0;
          }

          volatile f32 *slot_posX = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSX);
          volatile f32 *slot_posY = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSY);
          volatile f32 *slot_posZ = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSZ);
          f32 targetX = *slot_posX;
          f32 targetY = *slot_posY;
          f32 targetZ = *slot_posZ;
          f32 curX = FOPAC_CURRENT_POS(actor)->x;
          f32 curY = FOPAC_CURRENT_POS(actor)->y;
          f32 curZ = FOPAC_CURRENT_POS(actor)->z;
          f32 dx = targetX - curX;
          f32 dy = targetY - curY;
          f32 dz = targetZ - curZ;
          f32 distSq = dx * dx + dy * dy + dz * dz;
          if (distSq > 90000.0f) // 200^2 — snap for big deltas (climb / teleport)
          {
            FOPAC_CURRENT_POS(actor)->x = targetX;
            FOPAC_CURRENT_POS(actor)->y = targetY;
            FOPAC_CURRENT_POS(actor)->z = targetZ;
          }
          else
          {
            FOPAC_CURRENT_POS(actor)->x = curX + dx * 0.25f;
            FOPAC_CURRENT_POS(actor)->y = curY + dy * 0.25f;
            FOPAC_CURRENT_POS(actor)->z = curZ + dz * 0.25f;
          }
        }
      }
    }
  }

  daPy_lk_c__setWorldMatrix(link);
  daPy_lk_c__setWaistAngle(link);

  void *old_fdata = DAPY_LK_M_OLD_FDATA(link);
  if (old_fdata != NULL && *(u8 *)old_fdata != 0) // getOldFrameFlg()
  {
    daPy_lk_c__footBgCheck(link);
  }

  // Body angle adjustments (vanilla: cLib_addCalcAngleS(..., 4, 0xC00, 0x180))
  if (DAPY_LK_MCURPROC(link) != 0x66) // DAMAGE
  {
    cLib_addCalcAngleS(&DAPY_PY_BODY_ANGLE_Z(link), 0, 4, 0xC00, 0x180);
    if (DAPY_LK_MCURPROC(link) != 0x93) // FAN_GLIDE
    {
      daPy_lk_c__setMoveSlantAngle(link);
    }
    if ((DAPY_LK_MMODEFLG(link) & 0x20000000) == 0) // !ModeFlg_SUBJECT
    {
      cLib_addCalcAngleS(&DAPY_PY_BODY_ANGLE_X(link), 0, 4, 0xC00, 0x180);
    }
    if ((DAPY_LK_MMODEFLG(link) & 0x40000000) == 0)
    {
      cLib_addCalcAngleS(&DAPY_PY_BODY_ANGLE_Y(link), 0, 4, 0xC00, 0x180);
    }
  }

  // Neck and hat angles. Make the puppet look at the local PLAYER's face: setNeckAngle
  // targets mpAttnActorLockOn->eyePos, so point it at the player and temporarily raise the
  // player's eyePos to head height. mpAttnActorLockOn is cleared again afterwards so no
  // stale actor pointer survives into later frames / scene transitions.
  {
    fopAc_ac_c *playerActor = (fopAc_ac_c *)dComIfGp_getPlayer(0);
    if (playerActor != NULL && playerActor != actor)
    {
      cXyz originalEyePos = *FOPAC_EYE_POS(playerActor);
      FOPAC_EYE_POS(playerActor)->y = FOPAC_CURRENT_POS(playerActor)->y + 100.0f;

      DAPY_LK_MPATTNACTORLOCKON(link) = playerActor;
      daPy_lk_c__setNeckAngle(link);

      *FOPAC_EYE_POS(playerActor) = originalEyePos;
    }
    else
    {
      DAPY_LK_MPATTNACTORLOCKON(link) = NULL;
      daPy_lk_c__setNeckAngle(link);
    }
    DAPY_LK_MPATTNACTORLOCKON(link) = NULL;
  }
  daPy_lk_c__setHatAngle(link);

  // Joint matrix calculators.
  // J3DModelData (and its joints) is SHARED between player and puppet, so whoever sets
  // mMtxCalc last wins; set ours right before calc(). J3DJoint.mMtxCalc is at 0x58.
  //   CL_JNT_LINK_ROOT_e (0x00) -> m_pbCalc[PART_UNDER_e]
  //   CL_JNT_BODY_CHN_e  (0x02) -> m_pbCalc[PART_UPPER_e]
  //   CL_JNT_WAIST_CHN_e (0x1D) -> m_pbCalc[PART_UNDER_e]
  // mDoExt_MtxCalcAnmBlendTblOld derives from J3DMtxCalc via VIRTUAL inheritance; the
  // J3DMtxCalc* conversion reads the virtual-base pointer stored at offset 0 of the object
  // (matches Ghidra output of the vanilla execute; not verifiable from the decomp source).
  J3DModelData *mpCLModelData = DAPY_LK_MPCLMODELDATA(link);
  J3DJoint **mpJoints = J3DMODELDATA_MJOINTTREE_MPJOINTS(mpCLModelData);
  void **m_pbCalc = DAPY_LK_M_PBCALC(link);

  //
  // Vanilla sets these every frame right before mpCLModel->calc() (d_a_player_main.cpp:
  // 11561-11578; DOL 0x8012266C: lwz r4,0x2FAC(r31); lwz r4,0(r4); stw r4,0x58(r3)), and
  // nothing else calcs the CL model (draw only entries/viewCalcs), so sharing is safe as
  // long as each Link sets them before its own calc. We additionally put the previous
  // values back after our calc so no pointer into the puppet's heap is left in the shared
  // model data (matters when the puppet is deleted).
  void *savedJointCalc[3] = {NULL, NULL, NULL};
  if (mpJoints != NULL)
  {
    void *pbCalc0_vbase = m_pbCalc[0] ? *(void **)m_pbCalc[0] : NULL;
    void *pbCalc1_vbase = m_pbCalc[1] ? *(void **)m_pbCalc[1] : NULL;
    savedJointCalc[0] = *(void **)((u8 *)mpJoints[0x00] + 0x58);
    savedJointCalc[1] = *(void **)((u8 *)mpJoints[0x02] + 0x58);
    savedJointCalc[2] = *(void **)((u8 *)mpJoints[0x1D] + 0x58);
    *(void **)((u8 *)mpJoints[0x00] + 0x58) = pbCalc0_vbase;
    *(void **)((u8 *)mpJoints[0x02] + 0x58) = pbCalc1_vbase;
    *(void **)((u8 *)mpJoints[0x1D] + 0x58) = pbCalc0_vbase;
  }

  daPy_lk_c__checkOriginalHatAnimation(link);

  J3DModel *mpCLModel = DAPY_LK_MPCLMODEL(link);
  puppet_callModelCalc(mpCLModel);

  if (mpJoints != NULL)
  {
    *(void **)((u8 *)mpJoints[0x00] + 0x58) = savedJointCalc[0];
    *(void **)((u8 *)mpJoints[0x02] + 0x58) = savedJointCalc[1];
    *(void **)((u8 *)mpJoints[0x1D] + 0x58) = savedJointCalc[2];
  }

  // Head top position (CL_JNT_HEAD_JNT_e = 0x0F)
  static const cXyz head_offset = {40.0f, 0.0f, 0.0f};
  MTX34 *nodeMtx = *(MTX34 **)((u8 *)mpCLModel + 0x8C); // J3DModel::mpNodeMtx
  cMtx_multVec(&nodeMtx[0x0F], &head_offset, DAPY_PY_HEAD_TOP_POS(link));

  // checkRoofRestart - skipped (would restart the room)

  // Hand positions (CL_LHANDA 0x08, CL_RHANDA 0x0C)
  DAPY_PY_LEFT_HAND_POS(link)->x = nodeMtx[8].m[0][3];
  DAPY_PY_LEFT_HAND_POS(link)->y = nodeMtx[8].m[1][3];
  DAPY_PY_LEFT_HAND_POS(link)->z = nodeMtx[8].m[2][3];
  DAPY_PY_RIGHT_HAND_POS(link)->x = nodeMtx[0xC].m[0][3];
  DAPY_PY_RIGHT_HAND_POS(link)->y = nodeMtx[0xC].m[1][3];
  DAPY_PY_RIGHT_HAND_POS(link)->z = nodeMtx[0xC].m[2][3];

  // Boomerang and hookshot positions
  static const cXyz boomerang_catch = {12.5f, 47.5f, 36.6f};
  static const cXyz hookshot_root = {22.0f, 0.0f, 0.0f};
  cMtx_multVec(&nodeMtx[0x00], &boomerang_catch, DAPY_LK_M36F4(link));
  cMtx_multVec(&nodeMtx[0x08], &hookshot_root, DAPY_LK_MHOOKSHOTROOTPOS(link));

  // Hat and face model calc
  J3DModel *katsuraModel = DAPY_LK_MPKATSURAMODEL(link);
  J3DModel *yamuModel = DAPY_LK_MPYAMUMODEL(link);
  if (katsuraModel != NULL)
  {
    PSMTXCopy(&nodeMtx[0x0F], (MTX34 *)((u8 *)katsuraModel + 0x24));
    puppet_callModelCalc(katsuraModel);
  }
  if (yamuModel != NULL)
  {
    PSMTXCopy(&nodeMtx[0x0F], (MTX34 *)((u8 *)yamuModel + 0x24));
    puppet_callModelCalc(yamuModel);
  }

  // Position all equipment models (sword, shield, hands, sheath, etc.)
  // setItemModel does mDoExt_bckAnm::entry(modelData) = setMtxCalc on joint 0 of SHARED
  // model data (m_Do_ext.cpp:469-471) for the blade + glow (mSwordAnim, :1211-1213) and the
  // Master Sword grip (mSwgripmsabBckAnim, :1224) and then calcs. Put joint 0 of those model
  // datas back afterwards so shared data never keeps a pointer into the puppet's heaps (the
  // real Link always re-entries before its own calc).
  {
    static const u16 sharedAnimModelOffsets[3] = {
        0x2E98, // mpEquipItemModel (held blade)
        0x2EE8, // mpSwordModel1 (blade glow)
        0x0960, // mpSwgripmsModel (Master Sword grip)
    };
    J3DJoint *joint0[3] = {NULL, NULL, NULL};
    void *savedCalc[3] = {NULL, NULL, NULL};
    int k;
    for (k = 0; k < 3; k++)
    {
      J3DModel *m = *(J3DModel **)((u8 *)link + sharedAnimModelOffsets[k]);
      if (m != NULL && J3DMODEL_MPMODELDATA(m) != NULL &&
          J3DMODELDATA_MJOINTTREE_MPJOINTS(J3DMODEL_MPMODELDATA(m)) != NULL)
      {
        joint0[k] = J3DMODELDATA_MJOINTTREE_MPJOINTS(J3DMODEL_MPMODELDATA(m))[0];
        savedCalc[k] = *(void **)((u8 *)joint0[k] + 0x58);
      }
    }

    daPy_lk_c__setItemModel(link);

    for (k = 0; k < 3; k++)
    {
      if (joint0[k] != NULL)
        *(void **)((u8 *)joint0[k] + 0x58) = savedCalc[k];
    }
  }

  // Sword tip stab model - skip for puppet

  // Eye position (CL_JNT_CL_EYE_e = 0x13). l_eye_offset from d_a_player_main_data.inc.
  static const cXyz l_eye_offset = {11.25f, 18.75f, 0.0f};
  cMtx_multVec(&nodeMtx[0x13], &l_eye_offset, FOPAC_EYE_POS(actor));

  // Sword position tracking
  DAPY_LK_M36D0(link)->x = DAPY_PY_SWORD_TOP_POS(link)->x;
  DAPY_LK_M36D0(link)->y = DAPY_PY_SWORD_TOP_POS(link)->y;
  DAPY_LK_M36D0(link)->z = DAPY_PY_SWORD_TOP_POS(link)->z;
  DAPY_LK_M36DC(link)->x = DAPY_LK_M36C4(link)->x;
  DAPY_LK_M36DC(link)->y = DAPY_LK_M36C4(link)->y;
  DAPY_LK_M36DC(link)->z = DAPY_LK_M36C4(link)->z;

  // Equipment-specific sword position - use default (CL_JNT_CL_PODA_e = 0x0D)
  mDoMtx_multVecZero(&nodeMtx[0x0D], DAPY_LK_M36C4(link));
  mDoMtx_multVecZero(&nodeMtx[0x0D], DAPY_PY_SWORD_TOP_POS(link));

  // Effects and collision. Guarded: setLightSaver can StartShock (rumble) and these
  // functions assume `this` is the local player.
  {
    PuppetGlobalGuard guard;
    puppet_guardBegin(link, &guard);
    daPy_lk_c__checkLightHit(link);
    daPy_lk_c__setFootEffect(link);
    daPy_lk_c__setCollision(link);
    // setAttentionPos skipped - that's for the player's targeting, not for being targeted
    daPy_lk_c__setGrabItemPos(link);
    daPy_lk_c__setWaterRipple(link);
    daPy_lk_c__setAuraEffect(link);
    daPy_lk_c__setHammerWaterSplash(link);
    daPy_lk_c__setLightSaver(link);
    puppet_guardEnd(&guard);
  }

  // Event condition flags (matches vanilla): CANTALK|CANDOOR|CANGETITEM|UNK10|CANCATCH = 0x5D
  // when grounded and not flying. Harmless because attention flags are 0 (nobody can target
  // the puppet) and events are ordered to player 0. The ship/swim CANTALK branch and the
  // unconditional CANGETITEM are intentionally left out.
  u32 noResetFlg0 = DAPY_PY_NO_RESET_FLG0(link);
  if ((noResetFlg0 & 0xA0000000) == 0)
  {
    if ((ACCH_M_FLAGS(acch) & GROUND_HIT) != 0 && (DAPY_LK_MMODEFLG(link) & 0x10452822) == 0)
    {
      FOPAC_EVENT_CONDITION(actor) |= 0x5D;
    }
  }

  // Smoke emitter - skip for puppet

  daPy_lk_c__setWaterDrop(link);

  // Damage timer and effects - skip for puppet

  // offNoResetFlg1(NPC_CALL_COMMAND | VINE_CATCH)
  DAPY_PY_NO_RESET_FLG1(link) &= ~0x02000002;

  // Do/R status for current player - skip for puppet

  // offNoResetFlg1(UNK4 | FORCE_VOMIT_JUMP | FORCE_VOMIT_JUMP_SHORT | UNK10000000)
  DAPY_PY_NO_RESET_FLG1(link) &= ~0x10010014;

  // On board status / Link HP audio / cursed sound / bow status - skip for puppet (global)

  DAPY_LK_MSHIELDFRONTRANGEYANGL(link) = FOPAC_SHAPE_ANGLE(actor)->y + DAPY_PY_BODY_ANGLE_Y(link);
  DAPY_PY_MFACE(link) = 0xAD; // daPyFace_NONE
  DAPY_LK_MWHIRLID(link) = -1; // fpcM_ERROR_PROCESS_ID_e

  // Keep the puppet off every lock-on list
  puppet_updateAttentionInfo(actor);

  return 1;
}

/**
 * puppet_execute - runs the whole puppet execute inside one global-state guard (player
 * status masked to 0, local Z-lock hidden, rumble/HUD/heart/magic/BGM changes undone).
 */
int puppet_execute(daPy_lk_c *link)
{
  PuppetGlobalGuard guard;
  int ret;

  puppet_guardBegin(link, &guard);
  ret = puppet_executeBody(link);
  puppet_guardEnd(&guard);
  return ret;
}

// ============================================================================
// PARKED PUPPETS (inactive slot)
// ============================================================================
//
// The hook keeps slots 0..numActive-1 spawned and C# writes numActive = highest active slot
// + 1, so a lower slot can have a live puppet while its ACTIVE word is 0 (also: C# keeps a
// slot-0 puppet alive with no peer so puppet_worldsync_tick runs). Such a puppet is "parked":
//   - no puppet_execute: no posMove / Acch CrrPos (so no dBgS ride callbacks -> can't hold
//     floor switches), no setCollision (mCyl/mWindCyl/mLightCyl are never Set into dCcS; the
//     dCcS lists are rebuilt every frame - cCcS::Move zeroes the counts - so an unregistered
//     collider can't push, block, hit or be hit), no effects / light / sound.
//   - attention flags 0 every frame (not targetable); eventInfo.mCondition stays 0 because
//     fopAc_Execute's beforeProc() clears it and nothing sets it again.
//   - invisible: puppet_draw skips inactive slots, and daPuppet_Draw skips parked puppets.
// It stays where it is (not teleported): nothing above lets anything interact with it, and a
// far-away position would only feed the Acch a huge old->current line on resume.

int puppet_slotIsParked(u32 slotIndex)
{
  if (slotIndex >= PUPPET_MAX_SLOTS)
    return 1;
  return *(volatile u32 *)(PUPPET_SLOT_BASE(slotIndex) + PUPPET_SLOT_OFF_ACTIVE) == 0;
}

static void puppet_parkQuiet(daPy_lk_c *link)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)link;
  FOPAC_ATTN_FLAGS(actor) = 0;
  FOPAC_EVENT_CONDITION(actor) = 0;
  FOPAC_SPEED(actor)->x = 0.0f;
  FOPAC_SPEED(actor)->y = 0.0f;
  FOPAC_SPEED(actor)->z = 0.0f;
  FOPAC_SPEEDF(actor) = 0.0f;
  PUPPET_DAPY_MNORMALSPEED(link) = 0.0f;
  DAPY_PY_RESET_FLG0(link) = 0; // incl. the sword AT bits
}

void puppet_parkEnter(daPy_lk_c *link)
{
  // Foot dust/splash emitters follow the puppet via callbacks; drop them so nothing keeps
  // emitting at the parked spot. (Body effects are stopped by puppet_draw every frame.)
  daPy_lk_c__resetFootEffect(link);
  daPy_lk_c__offBodyEffect(link);
  puppet_parkQuiet(link);
}

void puppet_parkTick(daPy_lk_c *link)
{
  puppet_parkQuiet(link);
}

/**
 * puppet_parkResume - the slot is active again: snap to the peer's position so the first full
 * execute doesn't lerp/line-check from the parked spot, and forget the stale ground poly so the
 * moving-BG correction can't apply a bg delta accumulated while parked. The caller also resets
 * PUPPET_class.followState so the peer's proc is (re)initialised by puppet_requestProc.
 */
void puppet_parkResume(daPy_lk_c *link, u32 slotIndex)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)link;
  dBgS_Acch *acch = (dBgS_Acch *)((u8 *)link + 0x46C); // mAcch
  u8 *polyInfo = (u8 *)ACCH_M_GND(acch) + 0x14;        // m_gnd's cBgS_PolyInfo

  // cBgS_PolyInfo::ClearPi (c_bg_s_poly_info.h)
  *(u16 *)(polyInfo + 0x00) = 0xFFFF;     // mPolyIndex
  *(u16 *)(polyInfo + 0x02) = 0x100;      // mBgIndex
  *(u32 *)(polyInfo + 0x04) = 0;          // mpBgW
  *(u32 *)(polyInfo + 0x08) = 0xFFFFFFFF; // mActorId = fpcM_ERROR_PROCESS_ID_e
  ACCH_M_GROUND_H(acch) = -G_CM3D_F_INF;
  ACCH_M_FLAGS(acch) &= ~GROUND_HIT;

  if (slotIndex < PUPPET_MAX_SLOTS && *(volatile u32 *)PUPPET_SYNC_BASE == PUPPET_SYNC_MAGIC)
  {
    u32 slotBase = PUPPET_SLOT_BASE(slotIndex);
    f32 x = *(volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSX);
    f32 y = *(volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSY);
    f32 z = *(volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSZ);
    s16 rotY = *(volatile s16 *)(slotBase + PUPPET_SLOT_OFF_ROTY);
    // Reject NaN / absurd values (a torn or zeroed slot); the per-frame lerp/snap fixes it.
    if (x == x && y == y && z == z &&
        x > -1.0e6f && x < 1.0e6f && y > -1.0e6f && y < 1.0e6f && z > -1.0e6f && z < 1.0e6f)
    {
      cXyz *pos = FOPAC_CURRENT_POS(actor);
      cXyz *oldPos = (cXyz *)FOPAC_OLD(actor);
      pos->x = x;
      pos->y = y;
      pos->z = z;
      *oldPos = *pos;
      FOPAC_CURRENT_ANGLE(actor)->y = rotY;
      FOPAC_SHAPE_ANGLE(actor)->y = rotY;
    }
  }
  puppet_parkQuiet(link);
}

// ============================================================================
// LEGACY SECTION FUNCTIONS (kept for compatibility)
// ============================================================================

int puppet_executeSection1to10(daPy_lk_c *link)
{
  return puppet_execute(link);
}

// Per-puppet "last requested proc" (a daPyProc value; 0 = nothing requested yet).
// Used to call procX_init only on transitions so the animation isn't reset every frame.
// Loaded/saved per-puppet around puppet_execute by daPuppet_Execute in puppet.c
// (PUPPET_class.followState), so multiple puppets don't share it.
int l_puppetFollowState = 0;

/**
 * puppet_requestProc - init a proc once per transition.
 *
 * `key` is the daPyProc value in bits 0-7, plus (for one-shot procs, see
 * puppet_isRestartableProc) the peer's PROC_SEQ in bits 8-15, plus a per-proc init parameter
 * in bits 16-23 (HANG_MOVE's direction, so a shimmy reversal re-inits). A new swing of a combo is a
 * re-init of the SAME proc (changeCutProc, d_a_player_sword.inc:404-467: standing combo is
 * CUT_L, CUT_L, CUT_L, CUT_EB with m34C4 = 1,2,3,4), so the proc id alone can't tell swings
 * apart; the sequence byte changes on every peer (re)start and forces a re-init.
 * l_puppetFollowState (PUPPET_class.followState, u32) holds the whole key. It only advances
 * when the _init succeeded, so an init that returns FALSE is retried on the next frame.
 */
static void puppet_requestProc(daPy_lk_c *puppet, int key, int slotIndex)
{
  if (l_puppetFollowState == key)
    return;

  if (puppet_applyProcInit(puppet, key & 0xFF, (key >> 16) & 0xFF))
  {
    OSReport("[PUPPET] proc %04x -> %04x (slot %d)\n",
             l_puppetFollowState & 0xFFFF, key & 0xFFFF, slotIndex);
    l_puppetFollowState = key;
  }
}

// ============================================================================
// NETWORK-DRIVEN MOVEMENT
// ============================================================================

/**
 * puppet_readNetworkState - Read state from shared memory and apply to puppet
 *
 * Reads position, rotation, animation data from the shared memory slot
 * corresponding to the puppet's slot index. Uses smooth interpolation
 * to move towards the target position.
 *
 * Without the sync magic (client not attached) or with an inactive slot, the puppet
 * holds its position and idles in place.
 */
void puppet_readNetworkState(daPy_lk_c *puppet, int slotIndex)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)puppet;

  volatile u32 *sync_magic = (volatile u32 *)PUPPET_SYNC_BASE;
  u32 slotBase = PUPPET_SLOT_0 + (slotIndex * PUPPET_SLOT_SIZE);
  volatile u32 *slot_active = (volatile u32 *)slotBase;

  if (*sync_magic != PUPPET_SYNC_MAGIC || *slot_active == 0)
  {
    // No sync data (client detached) or slot inactive - hold position and idle in place
    puppet_setStickData(puppet, 0.0f, 0);
    FOPAC_SPEEDF(actor) = 0.0f;
    PUPPET_DAPY_MNORMALSPEED(puppet) = 0.0f;
    puppet_requestProc(puppet, PROC_WAIT, slotIndex);
    return;
  }

  // Read slot data via volatile pointers.
  //
  // RACE CONDITION NOTE: The C# client writes these fields from the host PC while the
  // emulated CPU reads them here. Individual 32-bit reads are atomic, but posX/posY/posZ
  // are not written as a coherent group; the lerp smoothing masks single-frame tearing.
  // A future improvement is a sequence-number handshake (read seqNum, copy slot, re-read).
  volatile f32 *slot_posX = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSX);
  volatile f32 *slot_posY = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSY);
  volatile f32 *slot_posZ = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_POSZ);
  volatile s16 *slot_rotY = (volatile s16 *)(slotBase + PUPPET_SLOT_OFF_ROTY);
  volatile u8 *slot_curProc = (volatile u8 *)(slotBase + PUPPET_SLOT_OFF_CUR_PROC);
  volatile f32 *slot_velocityF = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_VELOCITY_F);
  volatile s16 *slot_stickAngle = (volatile s16 *)(slotBase + PUPPET_SLOT_OFF_STICK_ANGLE);

  volatile u8 *slot_clothesType = (volatile u8 *)(slotBase + PUPPET_SLOT_CLOTHES_TYPE);

  // v2 extended state (slot +0x30..0x47) — mirrors peer's daPy_lk_c fields.
  volatile f32 *slot_maxNormalSpeed = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_MAX_NORMAL_SPEED);
  volatile f32 *slot_stickDistance = (volatile f32 *)(slotBase + PUPPET_SLOT_OFF_STICK_DISTANCE);

  f32 targetX = *slot_posX;
  f32 targetY = *slot_posY;
  f32 targetZ = *slot_posZ;
  s16 targetRotY = *slot_rotY;
  u8 targetProc = *slot_curProc;
  f32 targetVelocity = *slot_velocityF;
  s16 targetStickAngle = *slot_stickAngle;
  f32 netMaxNormalSpeed = *slot_maxNormalSpeed;
  f32 netStickDistance = *slot_stickDistance;

  // mMaxNormalSpeed feeds setBlendMoveAnime's walk/run ratio (mNormalSpeed / mMaxNormalSpeed).
  // Only accept sane values so a zero/garbage slot (old client, NaN) can't divide by zero.
  if (netMaxNormalSpeed > 0.1f && netMaxNormalSpeed < 1000.0f)
  {
    *(f32 *)((u8 *)puppet + DAPY_OFF_MAX_NORMAL_SPEED) = netMaxNormalSpeed;
  }
  // Mirror just the peer's Elixir-Soup power-up bit (daPyFlg1_SOUP_POWER_UP 0x8000): it only
  // drives the sword glow (draw) and cut attack power. 0 from an old client = no soup.
  {
    u32 netFlg1 = *(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_NO_RESET_FLG1);
    if (netFlg1 & 0x00008000)
      DAPY_PY_NO_RESET_FLG1(puppet) |= 0x00008000;
    else
      DAPY_PY_NO_RESET_FLG1(puppet) &= ~0x00008000;
  }
  // Deliberately NOT syncing mModeFlg / mNoResetFlg0 / the rest of mNoResetFlg1 (yet): writing those
  // bits without the matching resources (swim anims, water surface setup, heavy-boots
  // physics) puts the puppet in states this simplified execute can't service.

  // Lerp toward the peer's position each frame, snapping when far off (teleports, scene
  // loads, big jumps). The end-of-execute override re-applies the target after the
  // collision pipeline; this lerp is what animeUpdate/CrrPos see mid-frame.
  {
    f32 curX = FOPAC_CURRENT_POS(actor)->x;
    f32 curY = FOPAC_CURRENT_POS(actor)->y;
    f32 curZ = FOPAC_CURRENT_POS(actor)->z;
    f32 dx = targetX - curX;
    f32 dy = targetY - curY;
    f32 dz = targetZ - curZ;
    f32 distSq = dx * dx + dy * dy + dz * dz;
    if (distSq > 90000.0f) // 200^2
    {
      FOPAC_CURRENT_POS(actor)->x = targetX;
      FOPAC_CURRENT_POS(actor)->y = targetY;
      FOPAC_CURRENT_POS(actor)->z = targetZ;
    }
    else
    {
      FOPAC_CURRENT_POS(actor)->x = curX + dx * 0.25f;
      FOPAC_CURRENT_POS(actor)->y = curY + dy * 0.25f;
      FOPAC_CURRENT_POS(actor)->z = curZ + dz * 0.25f;
    }
  }

  // Rotation comes only from the network.
  FOPAC_CURRENT_ANGLE(actor)->y = targetRotY;
  FOPAC_SHAPE_ANGLE(actor)->y = targetRotY;

  // speedF is not a movement source for the puppet (posMove's displacement is discarded
  // in puppet_execute); keep it at 0 between frames.
  FOPAC_SPEEDF(actor) = 0.0f;

  // Appearance: the outfit flag. It drives the hat/buckle/wig shapes (puppet_draw) and which
  // linktexS3TC header puppet_appearance_begin writes for this puppet's entry; the tunic colour is
  // read from the slot there. APPEARANCE_CLOTHES_HERO / _CASUAL = the peer's override in the client
  // Settings; _DEFAULT = follow the PEER's own save, i.e. the casual bit of their mNoResetFlg1
  // mirrored in the slot. (Keeping the puppet's init-time value would show the LOCAL save's
  // outfit, since playerInit reads this game's save.)
  {
    u8 clothesType = *slot_clothesType;
    u32 peerFlg1 = *(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_NO_RESET_FLG1);
    int casual = clothesType == APPEARANCE_CLOTHES_CASUAL ||
                 (clothesType != APPEARANCE_CLOTHES_HERO && (peerFlg1 & DAPY_FLG1_CASUAL_CLOTHES) != 0);
    if (casual)
    {
      DAPY_PY_NO_RESET_FLG1(puppet) |= DAPY_FLG1_CASUAL_CLOTHES;
    }
    else
    {
      DAPY_PY_NO_RESET_FLG1(puppet) &= ~DAPY_FLG1_CASUAL_CLOTHES;
    }
  }

  // Stick data. For locomotion procs use the peer's analog stick distance (so walk vs run
  // matches), falling back to full stick when the slot doesn't carry a usable value
  // (old client / 0 / NaN). Keep it >= 0.05: setBlendMoveAnime treats < 0.05 as "no input"
  // and can switch to the slip/stop animation.
  if (targetProc == PROC_CRAWL_MOVE || targetProc == PROC_CRAWL_AUTO_MOVE || targetProc == PROC_HANG_MOVE)
  {
    // Crawling: 0 means lying still (anim paused), so no "full stick" fallback here.
    // Shimmying: getHangMoveAnmSpeed maps the stick to the anim rate (0 = its slowest rate).
    f32 stick = netStickDistance;
    if (!(stick > 0.0f))
      stick = 0.0f;
    else if (stick > 1.0f)
      stick = 1.0f;
    puppet_setStickData(puppet, stick, targetStickAngle);
  }
  else if (targetProc == PROC_MOVE || targetProc == PROC_ATN_MOVE || targetProc == PROC_MOVE_TURN ||
           targetProc == PROC_SWIM_MOVE)
  {
    f32 stick = netStickDistance;
    if (!(stick > 0.05f))
      stick = 1.0f;
    else if (stick > 1.0f)
      stick = 1.0f;
    puppet_setStickData(puppet, stick, targetStickAngle);
  }
  else
  {
    puppet_setStickData(puppet, 0.0f, 0);
  }

  // Proc transitions based on the remote player's raw mCurProc (daPyProc values,
  // d_a_player_main.h). FREE_WAIT (0x05) is normalised to WAIT (0x04): the game flips
  // between them while standing still and we'd otherwise re-init WAIT constantly.
  {
    int wantProc = (int)targetProc;
    if (wantProc == PROC_FREE_WAIT)
      wantProc = PROC_WAIT;

    // Sword in hand: mirror the peer's mEquipItem (slot EQUIP_ITEM; 0 = not written by an
    // older client -> treated as "no sword", so it's sheathed after each cut). Sword procs
    // always force it out instantly (puppet_applyProcInit). Outside cuts, draw/sheathe the
    // way the real Link does: REST upper-body anim, item swap at frame 7 in checkItemAction
    // (called every frame from puppet_execute).
    if (!puppet_isSwordProc(wantProc) && !puppet_isSwordProc(DAPY_LK_MCURPROC(puppet)))
    {
      u16 netEquip = *(volatile u16 *)(slotBase + PUPPET_SLOT_OFF_EQUIP_ITEM);
      int wantSword = (netEquip == DAPY_ITEM_SWORD);
      u16 equip = PUPPET_DAPY_MEQUIPITEM(puppet);
      int restAnim = (PUPPET_DAPY_UPPER2_ANM(puppet) == LKANM_BCK_REST);
      u16 pending = PUPPET_DAPY_M3562(puppet);

      if (wantSword && equip != DAPY_ITEM_SWORD &&
          !(restAnim && pending == DAPY_ITEM_SWORD))
      {
        // setAnimeEquipSword returns early if !checkSwordEquip() (LOCAL save has no sword);
        // in that case just draw it instantly.
        daPy_lk_c__setAnimeEquipSword(puppet, 1);
        if (PUPPET_DAPY_UPPER2_ANM(puppet) != LKANM_BCK_REST)
          puppet_setSwordOut(puppet, 1);
      }
      else if (!wantSword && equip == DAPY_ITEM_SWORD &&
               !(restAnim && pending == DAPY_ITEM_NONE))
      {
        daPy_lk_c__setAnimeUnequip(puppet); // REST anim, m3562 = NONE
      }
    }

    // One-shot procs carry the peer's (re)start sequence so a repeated swing re-inits.
    {
      int key = wantProc;
      if (puppet_isRestartableProc(wantProc))
      {
        u8 netSeq = *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_PROC_SEQ);
        key |= ((int)netSeq << 8);
      }
      if (wantProc == PROC_HANG_MOVE)
      {
        int prevDir = ((l_puppetFollowState & 0xFF) == PROC_HANG_MOVE) ? ((l_puppetFollowState >> 16) & 0xFF) : 0;
        key |= puppet_hangMoveDir(prevDir, netStickDistance, targetStickAngle, targetRotY) << 16;
      }
      puppet_requestProc(puppet, key, slotIndex);
    }
  }

  // mNormalSpeed LAST — after any proc_init above that may have reset it. The walk/run
  // blend (setBlendMoveAnime) reads it, so WAIT->MOVE runs immediately when the peer runs.
  PUPPET_DAPY_MNORMALSPEED(puppet) = targetVelocity;

  // Diagnostic: log the network velocity while moving, throttled to ~3 Hz.
  if (targetProc == PROC_MOVE || targetProc == PROC_MOVE_TURN)
  {
    volatile unsigned int *fc = (volatile unsigned int *)FRAME_COUNTER_ADDR;
    if (((*fc) % 20) == 0)
    {
      OSReport("[PUPPET] MOVE vel=%d/100 (slot %d)\n",
               (int)(targetVelocity * 100.0f), slotIndex);
    }
  }
}
