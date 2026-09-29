/**
 * puppet_procs_extra.c - more of the peer's procs (see puppet_procs_extra.h).
 *
 * Same rules as puppet_applyProcInit and puppet_held.c. No proc function runs per frame for a
 * puppet, only the _init and animeUpdate, so:
 *   - where the real _init only sets anims / mode flags / its own fields, or touches globals the
 *     guard undoes (player status, rumble, hearts), it is called;
 *   - where it reads an actor the puppet doesn't have (enemy, rope, ship, Boko weapon, the Co-hit
 *     actor), takes the global event, or writes other globals (save event bits, screen fade, UI
 *     sounds, item-get lighting), the puppet gets WAIT + the same setSingleMoveAnime the init does
 *     (HIO values from d_a_player_HIO_data.inc, checked against the vanilla DOL), relabelled
 *     mCurProc = the peer's proc, as puppet_held.c's l_heldPoses;
 *   - what the vanilla per-frame proc does to the anim that matters to the look (strafe blends,
 *     a follow-up anim, the knock-back tilt, pinning the root) is done in puppet_extraStep.
 * Position and facing come from the network: puppet_applyProcInit restores them after every init.
 *
 * Included by puppet.c after puppet_held.c; uses puppet_execute.c's helpers (puppet_sideDir,
 * puppet_setSwordOut, puppet_swordReady, PUPPET_DAPY_UNDER0_*). Everything runs inside
 * puppet_execute's global guard. REL: literals are fine here.
 */

#include "puppet_procs_extra.h"

// daPyProc (d_a_player_main.h:414-632)
#define XP_SUBJECTIVITY            0x01
#define XP_CALL                    0x02
#define XP_ATN_ACTOR_WAIT          0x08
#define XP_ATN_ACTOR_MOVE          0x09
#define XP_SIDE_STEP               0x0A
#define XP_SIDE_STEP_LAND          0x0B
#define XP_SLIDE_FRONT             0x1A
#define XP_SLIDE_BACK              0x1B
#define XP_SLIDE_FRONT_LAND        0x1C
#define XP_SLIDE_BACK_LAND         0x1D
#define XP_NOCK_BACK_END           0x20
#define XP_LAND_DAMAGE             0x26
#define XP_SLOW_FALL               0x28
#define XP_SWIM_UP                 0x35
#define XP_CUT_L                   0x44
#define XP_CUT_EA                  0x45
#define XP_CUT_EX_A                0x47
#define XP_CUT_EX_B                0x48
#define XP_CUT_KESA                0x4A
#define XP_WEAPON_NORMAL_SWING     0x4B
#define XP_WEAPON_SIDE_SWING       0x4C
#define XP_WEAPON_FRONT_SWING      0x4E
#define XP_WEAPON_FRONT_SWING_END  0x4F
#define XP_WEAPON_THROW            0x50
#define XP_HAMMER_SIDE_SWING       0x51
#define XP_HAMMER_FRONT_SWING      0x53
#define XP_CUT_REVERSE             0x5A
#define XP_BT_JUMP                 0x5D
#define XP_BT_JUMP_CUT             0x5E
#define XP_BT_ROLL                 0x60
#define XP_BT_ROLL_CUT             0x61
#define XP_BT_VERTICAL_JUMP        0x62
#define XP_BT_VERTICAL_JUMP_CUT    0x63
#define XP_BT_VERTICAL_JUMP_LAND   0x64
#define XP_POLY_DAMAGE             0x67
#define XP_LARGE_DAMAGE            0x68
#define XP_LARGE_DAMAGE_UP         0x69
#define XP_LARGE_DAMAGE_WALL       0x6A
#define XP_LAVA_DAMAGE             0x6B
#define XP_ELEC_DAMAGE             0x6C
#define XP_GRAB_MISS               0x70
#define XP_GRAB_HEAVY_WAIT         0x74
#define XP_GRAB_REBOUND            0x75
#define XP_ROPE_SUBJECT            0x76
#define XP_ROPE_MOVE               0x7D
#define XP_SHIP_READY              0x86
#define XP_FAN_SWING               0x92
#define XP_VOMIT_READY             0x96
#define XP_VOMIT_JUMP              0x98
#define XP_VOMIT_LAND              0x99
#define XP_ICE_SLIP_FALL           0x9E
#define XP_ICE_SLIP_FALL_UP        0x9F
#define XP_ICE_SLIP_ALMOST_FALL    0xA0
#define XP_BOOTS_EQUIP             0xA1
#define XP_DEMO_DAMAGE             0xAB
#define XP_DEMO_HOLDUP             0xAC
#define XP_DEMO_SALUTE             0xB4
#define XP_DEMO_DOOR_OPEN          0xC1
#define XP_DEMO_NOD                0xC2
#define XP_DEMO_WARP_SHORT         0xD2
// daPyProc_CONTROLL_WAIT_e: the label for a pose whose own proc's exit handling in commonProcInit
// must not run for a puppet (d_a_player_main.cpp:5725-5766 has none for it, and neither
// procWait_init's refusals (:6043-6050) nor puppet_executeBody look at it).
#define XP_NEUTRAL_LABEL           0x03

// daPy_lk_c::daPy_ANM (d_a_player_main.h:635-870)
#define XANM_DASH             0x02
#define XANM_ATNRS            0x08
#define XANM_CUTREL           0x15
#define XANM_TALKA            0x1B
#define XANM_CUTBOKO          0x26
#define XANM_CUTRER           0x27
#define XANM_WALK             0x01
#define XANM_VJMP             0x4C
#define XANM_VJMPCL           0x4F
#define XANM_DAMFL            0x59
#define XANM_DAMFR            0x5A
#define XANM_DAMFF            0x5B
#define XANM_DAMFB            0x5C
#define XANM_DAMFFUP          0x5F
#define XANM_DIELONG          0x62
#define XANM_GRABP            0x65
#define XANM_GRABNG           0x67
#define XANM_MJMP             0x6C
#define XANM_MROLLL           0x6E
#define XANM_MROLLR           0x6F
#define XANM_ROPECATCH        0x75
#define XANM_ROPESWINGB       0x77
#define XANM_ROPEWAIT         0x78
#define XANM_ROPECLIMB        0x79
#define XANM_ROPEDOWN         0x7A
#define XANM_ROPETHROWCATCH   0x7B
#define XANM_BOXOPENLINK      0x8E
#define XANM_ITEMGET          0x90
#define XANM_SURPRISED        0x9A
#define XANM_VOMITJMP         0xA7
#define XANM_SLIPICE          0xA9
#define XANM_HAMSWINGA        0xAA
#define XANM_HAMSWINGBPRE     0xAB
#define XANM_HAMSWINGBHIT     0xAC
#define XANM_HAMSWINGBEND     0xAD
#define XANM_DOOROPENALINK    0xAF
#define XANM_YOBU             0xC2
#define XANM_ESAMAKI          0xC4
#define XANM_SETHYOINOMI      0xC5
#define XANM_DAMBIRI          0xCB
#define XANM_KEEP_WAIT        0xFF  // not a daPy_ANM: keep the blend procWait_init set up

// m_HIO->mBasic.m.field_0xC, the usual morf (d_a_player_HIO_data.inc:5)
#define XP_BASIC_MORF 2.4f

// daPy_lk_c fields (d_a_player_main.h:2155-2185)
#define XP_DAPY_M34D4(link) (*(s16 *)((u8 *)(link) + 0x34D4)) // mProcVar2
#define XP_DAPY_M34D6(link) (*(s16 *)((u8 *)(link) + 0x34D6)) // mProcVar3
#define XP_DAPY_M34D8(link) (*(s16 *)((u8 *)(link) + 0x34D8)) // mProcVar4
#define XP_DAPY_M34F2(link) (*(s16 *)((u8 *)(link) + 0x34F2)) // root joint tilt x (jointBeforeCB, d_a_player_main.cpp:296-298)
#define XP_DAPY_M34F4(link) (*(s16 *)((u8 *)(link) + 0x34F4)) // root joint tilt z

// ============================================================================
// RELABELLED POSES
// ============================================================================

#define XPOSE_NEUTRAL 0x01 // label XP_NEUTRAL_LABEL instead of the proc (see above)
#define XPOSE_PIN     0x02 // the proc pins the root joint every frame (puppet_extraStep)

// WAIT, then setSingleMoveAnime(anm, rate, start, end, morf) as the real _init does, then mCurProc
// = the peer's proc. {proc, daPy_ANM, flags, end, rate, start, morf}
typedef struct
{
  u8 proc;
  u8 anm;
  u8 flags;
  s16 end;
  f32 rate;
  f32 start;
  f32 morf;
} XPose;

static const XPose l_xPoses[] = {
    // procSubjectivity_init: setSubjectMode plays a system SE (d_a_player_main.cpp:5932-5946, :5662-5665);
    // standing, it keeps WAIT's blend. The peer's look angles: puppet_extraPose.
    {XP_SUBJECTIVITY, XANM_KEEP_WAIT, 0, -1, 0.0f, 0.0f, 0.0f},
    // procCall_init: daPy_matAnm_c::offMabaFlg is static, i.e. every Link's blink (:5975-5986)
    {XP_CALL, XANM_YOBU, 0, -1, 1.0f, 0.0f, XP_BASIC_MORF},
    // Boko weapons: every init but THROW reads mActorKeepEquip (setEnemyWeaponAtParam, weapon.inc:19-41);
    // mCutBoko field_0x10/0x18/0x6/0x34 (weapon.inc:50-56), 0x48/0x4C/0x8/0x54 (:157-163),
    // 0x60/0x64/0xA/0x68 (:233-239), 4*0x6C/0x70/0xC/0 (:287-293), 0x74/0x78/0xE/0x80 (:348-354)
    {XP_WEAPON_NORMAL_SWING, XANM_CUTBOKO, XPOSE_PIN, 34, 1.1f, 1.0f, 3.0f},
    {XP_WEAPON_SIDE_SWING, XANM_HAMSWINGA, 0, 42, 0.9f, 20.0f, 6.0f},
    {0x4D, XANM_HAMSWINGBPRE, 0, 13, 0.7f, 3.0f, 3.0f}, // WEAPON_FRONT_SWING_READY
    {XP_WEAPON_FRONT_SWING, XANM_HAMSWINGBHIT, 0, 11, 3.2f, 0.0f, 0.0f},
    {XP_WEAPON_FRONT_SWING_END, XANM_HAMSWINGBEND, 0, 8, 0.6f, 5.0f, 10.0f},
    // Parry jump: procBtJump_init reads the enemy; mBJump field_0x8/0x14/-1/0x10 (battle.inc:80-90)
    {XP_BT_JUMP, XANM_MJMP, 0, -1, 1.0f, 4.0f, 1.0f},
    // procElecDamage_init takes the global event (dComIfGp_event_compulsory, :8157);
    // mElecDamage field_0x4/0/-1/0x8 (:8178)
    {XP_ELEC_DAMAGE, XANM_DAMBIRI, 0, -1, 1.0f, 0.0f, 0.0f},
    // procGrabMiss_init sets a save event bit (grab.inc:481); mGrab field_0x3C/0x40/0x4/0x44 (:472-478)
    {XP_GRAB_MISS, XANM_GRABNG, 0, 6, 1.0f, 0.0f, 1.0f},
    // Rope procs read the rope actor (d_a_player_rope.inc); mRope values HIO_data.inc:248
    {XP_ROPE_SUBJECT, XANM_ATNRS, 0, -1, 0.0f, 0.0f, 3.0f},      // :401 (field_0x54)
    {0x77, XANM_ROPECATCH, 0, 7, 1.0f, 0.0f, 3.0f},              // ROPE_READY: :468-473 (0x18/0x1C/0x4/0x20; the ROPESWINGB blend left out)
    {0x78, XANM_ROPECATCH, 0, 7, 1.0f, 6.9f, 3.0f},              // ROPE_SWING: hold the catch pose (setBlendRopeMoveAnime, :268, needs the swing)
    {0x79, XANM_ROPEWAIT, 0, -1, 0.5f, 0.0f, 4.0f},              // ROPE_HANG_WAIT: :812 (0x24/0x28)
    {0x7A, XANM_ROPECLIMB, 0, -1, 0.8f, 0.0f, 1.5f},             // ROPE_UP: :972 (0x30/0x34)
    {0x7B, XANM_ROPEDOWN, 0, -1, 1.5f, 0.0f, 4.0f},              // ROPE_DOWN: :1054 (0x38/0x3C)
    {0x7C, XANM_ROPESWINGB, 0, -1, 1.0f, 0.0f, 20.0f},           // ROPE_SWING_START: :1103
    {0x7E, XANM_ROPETHROWCATCH, 0, 11, 0.8f, 3.0f, 0.0f},        // ROPE_THROW_CATCH: :1217-1223 (0x58/0x5C/0xA/0x64)
    {0x7F, XANM_VJMP, 0, -1, 1.0f, 10.0f, 3.0f},                 // ROPE_UP_HANG: :1254
    // Boarding the boat reads the LOCAL ship (d_a_player_ship.inc): SHIP_READY mWallCatch
    // field_0x24/0x2C/0x4/0x28 (:477-483, then ANM_WALK in puppet_extraStep), JUMP_RIDE (:561),
    // GET_OFF mMove field_0x40 (:1153)
    {XP_SHIP_READY, XANM_VJMPCL, 0, 24, 0.75f, 0.0f, 5.0f},
    {0x87, XANM_SLIPICE, 0, -1, 1.0f, 0.0f, 5.0f},
    {0x90, XANM_DASH, 0, -1, 0.8f, 0.0f, 5.0f},
    // procVomitWait_init reads mCyl's Co-hit actor (vomit.inc:82-83)
    {0x97, XANM_VOMITJMP, 0, -1, 1.0f, 0.0f, 5.0f},
    // Food: global event + event camera (food.inc:42, :101, :168, :191); mFood field_0x4/0x8/0x0/0xC
    // (:90-96), 0x10/0x14/0x2/0x18 (:179-185)
    {0xA7, XANM_ESAMAKI, 0, 17, 0.6f, 0.0f, 4.0f},
    {0xA8, XANM_SETHYOINOMI, 0, 21, 1.1f, 2.0f, 4.0f},
    // Demo procs (d_a_player_dproc.inc) read the event / demo data / event partner:
    {0xAA, XANM_TALKA, 0, -1, 0.7f, 0.0f, 5.0f},                // DEMO_TALK: :363 (mTurn 0x14/0x18)
    {0xAD, XANM_BOXOPENLINK, 0, -1, 1.0f, 0.0f, 3.0f},          // DEMO_OPEN_TREASURE: :524
    // DEMO_GET_ITEM: :678; leaving it runs dKy_Itemgetcol_chg_off (global lighting, main.cpp:5748-5749)
    {0xAE, XANM_ITEMGET, XPOSE_NEUTRAL, -1, 1.0f, 0.0f, 3.0f},
    {0xAF, XANM_KEEP_WAIT, 0, -1, 0.0f, 0.0f, 0.0f},             // DEMO_UNEQUIP: :825 (the item goes away via puppet_heldMirror)
    {0xB2, XANM_DIELONG, 0, -1, 1.0f, 0.0f, 2.0f},              // DEMO_DEAD: :1028-1032 (mRestart 0xC/0x10/0x14)
    {0xB8, XANM_SURPRISED, 0, -1, 1.0f, 0.0f, 3.0f},            // DEMO_SURPRISED: :1287
    {0xBE, XANM_KEEP_WAIT, 0, -1, 0.0f, 0.0f, 0.0f},             // DEMO_LOOK_WAIT: :1447
    {XP_DEMO_DOOR_OPEN, XANM_DOOROPENALINK, XPOSE_PIN, -1, 1.0f, 0.0f, 0.0f}, // :1528-1535
};

#define XP_COUNT(a) ((int)(sizeof(a) / sizeof((a)[0])))

static const XPose *xp_findPose(int proc)
{
  int i;
  for (i = 0; i < XP_COUNT(l_xPoses); i++)
  {
    if (l_xPoses[i].proc == proc)
      return &l_xPoses[i];
  }
  return NULL;
}

// WAIT + the pose (procWait_init refuses when already in WAIT, d_a_player_main.cpp:6043, fine here).
static int xp_pose(daPy_lk_c *puppet, int proc, int anm, s16 end, f32 rate, f32 start, f32 morf)
{
  if (DAPY_LK_MCURPROC(puppet) != PROC_WAIT && !daPy_lk_c__procWait_init(puppet))
    return 0;
  if (anm != XANM_KEEP_WAIT)
    daPy_lk_c__setSingleMoveAnime(puppet, anm, rate, start, end, morf);
  DAPY_LK_MCURPROC(puppet) = proc;
  return 1;
}

static int xp_applyPose(daPy_lk_c *puppet, const XPose *p)
{
  if (!xp_pose(puppet, p->proc, p->anm, p->end, p->rate, p->start, p->morf))
    return 0;
  if (p->flags & XPOSE_NEUTRAL)
    DAPY_LK_MCURPROC(puppet) = XP_NEUTRAL_LABEL;
  return 1;
}

// ============================================================================
// HELPERS
// ============================================================================

/**
 * xp_blendAtnActor - setBlendAtnMoveAnime as with an actor locked on. Only the NULL test of
 * mpAttnActorLockOn picks between a forward/back walk and the side strafe (d_a_player_main.cpp:3297-3316),
 * and the puppet never has one (puppet_executeBody clears it), so point it at the puppet itself for
 * the call.
 */
static void xp_blendAtnActor(daPy_lk_c *link, f32 morf)
{
  DAPY_LK_MPATTNACTORLOCKON(link) = (fopAc_ac_c *)link;
  daPy_lk_c__setBlendAtnMoveAnime(link, morf);
  DAPY_LK_MPATTNACTORLOCKON(link) = NULL;
}

// The side in a key param (puppet_extraKey), as daPy_lk_c::DIR_LEFT/RIGHT.
static int xp_side(int param)
{
  return (param & 0x0F) == HANG_DIR_RIGHT ? DAPY_DIR_RIGHT : DAPY_DIR_LEFT;
}

/**
 * xp_cutReverseAnm - procCutReverse_init's daPy_ANM, picked by the swing that bounced
 * (changeCutReverseProc callers): CUTREL after CUT_L / CUT_EA / CUT_EX_A / CUT_EX_B / CUT_KESA
 * (sword.inc:920,1010,1184,1274,2204), the fan and the hammer (fan.inc:223, hammer.inc:209,326) and
 * the Boko side / front swings (weapon.inc:200,324); CUTRER otherwise.
 */
static int xp_cutReverseAnm(int cur)
{
  if (cur == XP_CUT_L || cur == XP_CUT_EA || cur == XP_CUT_EX_A || cur == XP_CUT_EX_B || cur == XP_CUT_KESA ||
      cur == XP_FAN_SWING || cur == XP_HAMMER_SIDE_SWING || cur == XP_HAMMER_FRONT_SWING ||
      cur == XP_WEAPON_SIDE_SWING || cur == XP_WEAPON_FRONT_SWING)
    return XANM_CUTREL;
  return XANM_CUTRER;
}

// ============================================================================
// INITS
// ============================================================================

int puppet_extraInitProc(daPy_lk_c *puppet, int proc, int param)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)puppet;
  int cur = DAPY_LK_MCURPROC(puppet);
  const XPose *pose;
  int ok;

  switch (proc)
  {
  // Z-target on an actor (d_a_player_main.cpp:4461-4466). The inits only test mpAttnActorLockOn, so
  // they are safe with the puppet's NULL one; both refuse when already in the proc (:6242, :6279).
  // ATN_ACTOR_WAIT would take the battle stance for an enemy and a plain wait otherwise
  // (checkAtnWaitAnime, :2951-2965); the peer's target isn't known, and Z-targets are mostly
  // enemies, so the stance.
  case XP_ATN_ACTOR_WAIT:
    if (cur == proc)
      return 1;
    ok = daPy_lk_c__procAtnActorWait_init(puppet);
    if (ok)
      xp_blendAtnActor(puppet, XP_BASIC_MORF);
    return ok;
  case XP_ATN_ACTOR_MOVE:
    if (cur == proc)
      return 1;
    DAPY_LK_MPATTNACTORLOCKON(puppet) = (fopAc_ac_c *)puppet; // see xp_blendAtnActor
    ok = daPy_lk_c__procAtnActorMove_init(puppet);
    DAPY_LK_MPATTNACTORLOCKON(puppet) = NULL;
    return ok;

  // Side hop with the Z-target (:4298-4303): ANM_ATNJL/R by the side from the request key, then the
  // landing in the same side (the land init reads mDirection, :6353-6358). Its rumble is guarded.
  case XP_SIDE_STEP:
    return daPy_lk_c__procSideStep_init(puppet, xp_side(param));
  case XP_SIDE_STEP_LAND:
    DAPY_LK_MDIRECTION(puppet) = (u8)xp_side(param);
    return daPy_lk_c__procSideStepLand_init(puppet);

  // Sliding down a slope (:6683-6781). The front / back inits refuse while in either slide, so a
  // switch between them goes through WAIT; the param is only the move angle (restored afterwards).
  case XP_SLIDE_FRONT:
  case XP_SLIDE_BACK:
    if (cur == proc)
      return 1;
    if ((cur == XP_SLIDE_FRONT || cur == XP_SLIDE_BACK) && !daPy_lk_c__procWait_init(puppet))
      return 0;
    if (proc == XP_SLIDE_FRONT)
      return daPy_lk_c__procSlideFront_init(puppet, FOPAC_SHAPE_ANGLE(actor)->y);
    return daPy_lk_c__procSlideBack_init(puppet, (s16)(FOPAC_SHAPE_ANGLE(actor)->y + 0x8000));
  case XP_SLIDE_FRONT_LAND: return daPy_lk_c__procSlideFrontLand_init(puppet);
  case XP_SLIDE_BACK_LAND:  return daPy_lk_c__procSlideBackLand_init(puppet);

  case XP_NOCK_BACK_END: return daPy_lk_c__procNockBackEnd_init(puppet); // :6919
  // Param 0: only a rumble, no damage (:7232-7261); the stand-up anim follows in puppet_extraStep.
  case XP_LAND_DAMAGE:   return daPy_lk_c__procLandDamage_init(puppet, 0);
  case XP_SLOW_FALL:     return daPy_lk_c__procSlowFall_init(puppet);     // :7391
  case XP_SWIM_UP:       return daPy_lk_c__procSwimUp_init(puppet, 0);    // swim.inc:330 (0: no splash)

  // A swing bounced off a shield (sword.inc:1815-1843): anims only, no blade needed; the held item
  // (sword, hammer, leaf) follows the peer (puppet_heldMirror).
  case XP_CUT_REVERSE: return daPy_lk_c__procCutReverse_init(puppet, xp_cutReverseAnm(cur));

  // Parry (battle.inc). The cuts' inits only take a point; they set the blade bck and blur, so the
  // sword must be drawn (mSwordAnim.changeBckOnly asserts mAnm, see SWORD STATE). BT_VERTICAL_JUMP
  // never reads its enemy (:399-417); BT_VERTICAL_JUMP_CUT needs setSwordModel's mpCutfBpk (:440,
  // sword.inc:117). BT_ROLL reads the enemy: a pose, in the side from the request key (mBRoll
  // field_0xC/0x10/0x6/0x14, :244-258). The roll cut picks its side from current - shape (:314-325).
  case XP_BT_JUMP_CUT:
  case XP_BT_ROLL_CUT:
  case XP_BT_VERTICAL_JUMP_CUT:
    puppet_setSwordOut(puppet, 1);
    if (!puppet_swordReady(puppet))
      return xp_pose(puppet, proc, XANM_KEEP_WAIT, -1, 0.0f, 0.0f, 0.0f);
    if (proc == XP_BT_JUMP_CUT)
      return daPy_lk_c__procBtJumpCut_init(puppet, FOPAC_CURRENT_POS(actor));
    if (proc == XP_BT_VERTICAL_JUMP_CUT)
      return daPy_lk_c__procBtVerticalJumpCut_init(puppet);
    FOPAC_CURRENT_ANGLE(actor)->y = (s16)(FOPAC_SHAPE_ANGLE(actor)->y +
                                          (xp_side(param) == DAPY_DIR_LEFT ? 0x4000 : -0x4000));
    return daPy_lk_c__procBtRollCut_init(puppet, FOPAC_CURRENT_POS(actor));
  case XP_BT_ROLL:
    return xp_pose(puppet, proc, xp_side(param) == DAPY_DIR_LEFT ? XANM_MROLLL : XANM_MROLLR, 22, 0.8f, 4.0f, 5.0f);
  case XP_BT_VERTICAL_JUMP:      return daPy_lk_c__procBtVerticalJump_init(puppet, NULL);
  case XP_BT_VERTICAL_JUMP_LAND: return daPy_lk_c__procBtVerticalJumpLand_init(puppet); // hearts guarded (:480-486)

  // Damage. POLY_DAMAGE (:7602) and LAVA_DAMAGE (:8108: setDamagePoint = the pending heart delta,
  // restored by the guard; the fire emitter is the puppet's own) are safe.
  case XP_POLY_DAMAGE: return daPy_lk_c__procPolyDamage_init(puppet);
  case XP_LAVA_DAMAGE: return daPy_lk_c__procLavaDamage_init(puppet);
  // LARGE_DAMAGE param -10 reads no damage vector and hurts nobody (:7693-7694): the knock-back
  // anim comes from current.angle.y - shape_angle.y. Behind the facing = hit from the front, the
  // usual case (-1/-6/-9 blow Link along the hit, :7687-7690): ANM_DAMFF, shape_angle kept. It
  // refuses when already in the proc (:7633). The tilt is chased in puppet_extraStep.
  case XP_LARGE_DAMAGE:
    if (cur == proc)
      return 1;
    FOPAC_CURRENT_ANGLE(actor)->y = (s16)(FOPAC_SHAPE_ANGLE(actor)->y + 0x8000);
    return daPy_lk_c__procLargeDamage_init(puppet, -10, 1, 0, 0);
  // Getting up (:7851-7977): for a knock-back anim the init checks the event manager for
  // "ICE_FAILED" and then runs dProcFreezeDamage_init_sub, a global screen fade (:7883-7895,
  // dproc.inc:904). Wall hit (:8005-8097): reads the puppet's stale line check. Poses for the
  // DAMFF knock-back LARGE_DAMAGE takes, mLaDamage field_0x24/0x28/0x4/0x30 and 0x68/0x6C/0xC/0x70,
  // blended from the tilt like the inits' setOldRootQuaternion (the wall sets the tilt it lies at).
  case XP_LARGE_DAMAGE_UP:
  case XP_LARGE_DAMAGE_WALL:
  {
    s16 tiltX = XP_DAPY_M34F2(puppet);
    s16 tiltZ = XP_DAPY_M34F4(puppet);
    if (cur == proc)
      return 1;
    if (proc == XP_LARGE_DAMAGE_UP)
      ok = xp_pose(puppet, proc, XANM_DAMFFUP, 36, 0.7f, 0.0f, 2.0f);
    else
      ok = xp_pose(puppet, proc, XANM_DAMFFUP, 19, 1.5f, 18.0f, 1.0f);
    if (ok)
    {
      daPy_lk_c__setOldRootQuaternion(puppet, tiltX, 0, tiltZ);
      if (proc == XP_LARGE_DAMAGE_WALL)
        XP_DAPY_M34F2(puppet) = 0x4000; // m34F2 = 0x4000 - the wall's pitch, 0 for an upright wall (:8087)
    }
    return ok;
  }

  // GRAB_HEAVY_WAIT (grab.inc:788-797) and GRAB_REBOUND (:846-856): anims only.
  case XP_GRAB_HEAVY_WAIT: return daPy_lk_c__procGrabHeavyWait_init(puppet);
  case XP_GRAB_REBOUND:    return daPy_lk_c__procGrabRebound_init(puppet);

  // ROPE_MOVE (rope.inc:1162-1179) reads the rope actor: strafe like ATN_MOVE, relabelled (the
  // HOOKSHOT_MOVE way, puppet_held.c).
  case XP_ROPE_MOVE:
    if (cur != proc && cur != PROC_ATN_MOVE && !daPy_lk_c__procAtnMove_init(puppet))
      return 0;
    DAPY_LK_MCURPROC(puppet) = proc;
    return 1;

  // Boko Baba bulb (vomit.inc). VOMIT_READY's param 1 only scales the speed; VOMIT_JUMP 0 = the
  // launch (its blur emitter is the puppet's own, ended by the next commonProcInit).
  case XP_VOMIT_READY: return daPy_lk_c__procVomitReady_init(puppet, FOPAC_SHAPE_ANGLE(actor)->y, 0.0f);
  case XP_VOMIT_JUMP:  return daPy_lk_c__procVomitJump_init(puppet, 0);
  case XP_VOMIT_LAND:  return daPy_lk_c__procVomitLand_init(puppet); // stand-up in puppet_extraStep

  // Ice (:8297-8427). The fall reads the puppet's own slide vector (its direction is a guess); the
  // get-up takes the fall's knock-back anim (procIceSlipFall passes mProcVar6, :8338) and tilt.
  case XP_ICE_SLIP_FALL:        return daPy_lk_c__procIceSlipFall_init(puppet);
  case XP_ICE_SLIP_FALL_UP:
    return daPy_lk_c__procIceSlipFallUp_init(puppet, cur == XP_ICE_SLIP_FALL ? DAPY_LK_M3570(puppet) : XANM_DAMFB,
                                             XP_DAPY_M34F2(puppet), XP_DAPY_M34F4(puppet));
  case XP_ICE_SLIP_ALMOST_FALL: return daPy_lk_c__procIceSlipAlmostFall_init(puppet);
  // The per-frame proc toggles the boots at frame 11 (:8465-8471); the param is only kept for it.
  case XP_BOOTS_EQUIP:          return daPy_lk_c__procBootsEquip_init(puppet, 0);
  // Throwing the Boko weapon: anims only (weapon.inc:379-386; the throw itself is per-frame).
  case XP_WEAPON_THROW:         return daPy_lk_c__procWeaponThrow_init(puppet);

  // Demo procs whose inits only set anims (d_a_player_dproc.inc:432, :459, :1159, :1567, :2195);
  // three refuse when already in the proc.
  case XP_DEMO_DAMAGE:
  case XP_DEMO_HOLDUP:
  case XP_DEMO_SALUTE:
    if (cur == proc)
      return 1;
    if (proc == XP_DEMO_DAMAGE)
      return daPy_lk_c__dProcDamage_init(puppet);
    if (proc == XP_DEMO_HOLDUP)
      return daPy_lk_c__dProcHoldup_init(puppet); // the sidling variant from the puppet's own WHIDE mode
    return daPy_lk_c__dProcSalute_init(puppet);
  case XP_DEMO_NOD:        return daPy_lk_c__dProcNod_init(puppet);
  case XP_DEMO_WARP_SHORT: return daPy_lk_c__dProcWarpShort_init(puppet);

  default:
    break;
  }

  pose = xp_findPose(proc);
  if (pose == NULL)
    return -1;
  ok = xp_applyPose(puppet, pose);
  if (ok && (proc == XP_GRAB_MISS || proc == XP_SHIP_READY))
  {
    // Their follow-up anims (puppet_extraStep) count from here, as the inits set them
    // (grab.inc:479-480: mGrab field_0x6; ship.inc:499).
    DAPY_LK_M3570(puppet) = 0;
    DAPY_LK_M34D0(puppet) = proc == XP_GRAB_MISS ? 12 : 0;
  }
  return ok;
}

// ============================================================================
// REQUEST KEY
// ============================================================================

/**
 * puppet_extraKey - the side of a side hop / parry roll, from the peer's stick at its start (the
 * hop is chosen by getDirectionFromShapeAngle = m34E8 - shape_angle.y, d_a_player_main.cpp:4300;
 * puppet_sideDir reads it the same way). It is kept for the rest of the hop / roll (same proc and
 * PROC_SEQ) and for the landing / roll cut that follows, which play the same side.
 */
int puppet_extraKey(int key, int prevKey, f32 stickDistance, s16 stickAngle, s16 rotY)
{
  int proc = key & 0xFF;
  int prevProc = prevKey & 0xFF;
  int dir = -1;

  if (proc != XP_SIDE_STEP && proc != XP_SIDE_STEP_LAND && proc != XP_BT_ROLL && proc != XP_BT_ROLL_CUT)
    return key;
  if (((prevKey ^ key) & 0xFFFF) == 0 || (proc == XP_SIDE_STEP_LAND && prevProc == XP_SIDE_STEP) ||
      (proc == XP_BT_ROLL_CUT && prevProc == XP_BT_ROLL))
    dir = (prevKey >> 16) & 0x0F;
  if (dir != HANG_DIR_LEFT && dir != HANG_DIR_RIGHT)
    dir = puppet_sideDir(0, stickDistance, stickAngle, rotY);
  return key | (dir << 16);
}

int puppet_extraIsAtnMoveProc(int proc)
{
  return proc == XP_ATN_ACTOR_MOVE || proc == XP_ROPE_MOVE;
}

// ============================================================================
// PER FRAME
// ============================================================================

void puppet_extraStep(daPy_lk_c *link, int proc)
{
  fopAc_ac_c *actor = (fopAc_ac_c *)link;
  const XPose *pose;

  switch (proc)
  {
  // procAtnActorWait / procAtnActorMove (d_a_player_main.cpp:6265-6275, :6289-6295) and procRopeMove
  // (rope.inc:1208): the strafe blend each frame, moving along the peer's stick (m34E8) like
  // ATN_MOVE in puppet_executeBody. Only the actor-locked ones strafe sideways all the time.
  case XP_ATN_ACTOR_WAIT:
  case XP_ATN_ACTOR_MOVE:
  case XP_ROPE_MOVE:
    if (DAPY_LK_MSTICKDISTANCE(link) > 0.05f)
      FOPAC_CURRENT_ANGLE(actor)->y = DAPY_LK_M34E8_EXEC(link);
    if (proc == XP_ROPE_MOVE)
      daPy_lk_c__setBlendAtnMoveAnime(link, -1.0f);
    else
      xp_blendAtnActor(link, -1.0f);
    return;

  // procLandDamage / procVomitLand (main.cpp:7264-7287, vomit.inc:266-289): once the fall anim has
  // ended and the wait count is out, the stand-up anim. That branch never calls checkNextMode, so
  // the vanilla function runs as is until it has started the stand-up (m3570 = 1).
  case XP_LAND_DAMAGE:
    if (DAPY_LK_M3570(link) == 0)
      daPy_lk_c__procLandDamage(link);
    return;
  case XP_VOMIT_LAND:
    if (DAPY_LK_M3570(link) == 0)
      daPy_lk_c__procVomitLand(link);
    return;

  // procGrabMiss (grab.inc:490-505): after the miss and mGrab field_0x6 frames, back out with
  // ANM_GRABP reversed (field_0x48/0x4C/0x8/0x54). procShipReady (ship.inc:525-533): after the
  // climb, walk (mMove field_0x40) onto the deck.
  case XP_GRAB_MISS:
  case XP_SHIP_READY:
    if (DAPY_LK_M3570(link) != 0 || PUPPET_DAPY_UNDER0_RATE(link) >= 0.01f)
      return;
    if (DAPY_LK_M34D0(link) > 0)
    {
      DAPY_LK_M34D0(link)--;
      return;
    }
    if (proc == XP_GRAB_MISS)
      daPy_lk_c__setSingleMoveAnime(link, XANM_GRABP, -0.8f, 0.0f, 4, 4.0f);
    else
      daPy_lk_c__setSingleMoveAnime(link, XANM_WALK, 0.8f, 0.0f, -1, 5.0f);
    DAPY_LK_M3570(link) = 1;
    return;

  // procLargeDamage / procIceSlipFall (main.cpp:7804-7810, :8331-8336): the body tips over while
  // flying, on the root joint (m34F2 / m34F4, jointBeforeCB :296-298).
  case XP_LARGE_DAMAGE:
  case XP_ICE_SLIP_FALL:
  {
    int onX = proc == XP_LARGE_DAMAGE ? (XP_DAPY_M34D6(link) & 4) != 0 : XP_DAPY_M34D6(link) == 1;
    s16 step = proc == XP_LARGE_DAMAGE ? XP_DAPY_M34D8(link) : 8000; // mIceSlip field_0x2
    cLib_chaseAngleS(onX ? &XP_DAPY_M34F2(link) : &XP_DAPY_M34F4(link), XP_DAPY_M34D4(link), step);
    return;
  }

  // procBtJumpCut / procBtRollCut (battle.inc:176, :360): the blade bck follows the body anim.
  case XP_BT_JUMP_CUT:
  case XP_BT_ROLL_CUT:
    if (puppet_swordReady(link))
      PUPPET_DAPY_M35EC(link) = PUPPET_DAPY_UNDER0_FRAME(link);
    return;

  // The Boko throw's per-frame proc keeps m34C2 = 1 (weapon.inc:384 at init; procWeaponThrow)
  case XP_WEAPON_THROW:
    if (DAPY_LK_M34C2(link) == 0)
      DAPY_LK_M34C2(link) = 1;
    return;

  default:
    break;
  }

  // Poses whose per-frame proc keeps m34C2 = 1 (weapon.inc:113, dproc.inc:1555-1558): the root joint
  // stays at x/z 0 and posMove turns the anim's root motion into movement (d_a_player_main.cpp:336-339,
  // :2609-2617). The peer's position already carries that movement, so without it the puppet's
  // body would walk off by the anim's root offset.
  pose = xp_findPose(proc);
  if (pose != NULL && (pose->flags & XPOSE_PIN) && DAPY_LK_M34C2(link) == 0)
    DAPY_LK_M34C2(link) = 1;
}

/**
 * puppet_extraPose - the peer's look direction in first person (procSubjectivity ->
 * setBodyAngleToCamera, d_a_player_main.cpp:5965) and while aiming the grappling hook
 * (procRopeSubject, rope.inc:434): the slot's mBodyAngle x/y, set after the execute's decay
 * like puppet_heldPose does for the item aims.
 */
void puppet_extraPose(daPy_lk_c *link)
{
  u32 slotIndex = *(u32 *)((u8 *)link + sizeof(daPy_lk_c)); // PUPPET_class.slotIndex
  int cur = DAPY_LK_MCURPROC(link);
  u32 slotBase;

  if (cur != XP_SUBJECTIVITY && cur != XP_ROPE_SUBJECT)
    return;
  if (slotIndex >= PUPPET_MAX_SLOTS || *(volatile u32 *)PUPPET_SYNC_BASE != PUPPET_SYNC_MAGIC)
    return;
  slotBase = PUPPET_SLOT_BASE(slotIndex);
  if (*(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_ACTIVE) == 0)
    return;
  DAPY_PY_BODY_ANGLE_X(link) = *(volatile s16 *)(slotBase + PUPPET_SLOT_OFF_BODY_ANGLE_X);
  DAPY_PY_BODY_ANGLE_Y(link) = *(volatile s16 *)(slotBase + PUPPET_SLOT_OFF_BODY_ANGLE_Y);
}
