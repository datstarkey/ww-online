/**
 * puppet_held.c - the item the peer holds and uses (see puppet_held.h, docs/held-items.md).
 *
 * HELD ITEM. The slot's EQUIP_ITEM is the peer's mEquipItem (m3562 while a draw/put-away anim
 * plays). The puppet builds the same item in its OWN item heaps (playerInit's mpItemHeaps, flipped
 * by setItemHeap) with the stock setters, exactly as the real Link's makeItemType does
 * (d_a_player_main.cpp:3681-3760) for the model-only items. The actor items never reach
 * makeItemType: it would fopAcM_fastCreate a HOOKSHOT / BOOMERANG / HIMO2 / BOMB bound to the LOCAL
 * Link (docs/held-items.md, Tier B). Instead:
 *   - hookshot: mEquipItem + setHookshotModel alone (the body; puppet_draw NULL-checks the tip actor)
 *   - boomerang: mEquipItem only; this file draws a "Link" BDL_BOOMERANG in the hand
 *   - bottles: setBottleModel(item) (liquid / fairy / firefly shape + cap)
 *   - grappling hook, bomb bag: never held (a carried bomb is GRAB_KIND, drawn here)
 * Take-out / put-away anims are the vanilla ones (setAnimeUnequipItem, :3498) played when the peer
 * is in one too. checkItemAction swaps mEquipItem = m3562 at the anim's swap frame and then calls
 * makeItemType, so m3562 is set to PUPPET_HELD_PENDING(item): makeItemType ignores that value and
 * puppet_heldFinishSwap (right after checkItemAction) builds the item with the rules above.
 *
 * USE. Item procs call their real _init where it only sets anims / flags (the guard undoes the
 * status words, magic and hearts); the rest get WAIT + the proc's pose, relabelled (the
 * puppet_initBoatPose trick): bottle drink/open/get (global event + event camera, d_a_player_bottle.inc:96,
 * 119,212,239,550), Wind Waker (procTactWait_init(-1): event + camera, tact.inc:205,262), hookshot fly/move
 * and the grab procs (read the hookshot / grabbed actor). The peer's UPPER_MOVE2 anim (aim holds,
 * boomerang throw/catch, bow draw, carry) is mirrored from the slot's ANIM_ID; checkItemAction never
 * sees those (BOOMTHROW would throwBoomerang() a NULL actor, boomerang.inc:25).
 *
 * All of it runs inside puppet_execute's global guard. REL: literals are fine here.
 */

#include "puppet_held.h"

// dItemNo (tww-decomp include/d/d_item_data.h)
#define ITEM_TELESCOPE        0x20
#define ITEM_TINGLE_TUNER     0x21
#define ITEM_WIND_WAKER       0x22
#define ITEM_PICTO_BOX        0x23
#define ITEM_DELUXE_PICTO_BOX 0x26
#define ITEM_BOW              0x27
#define ITEM_BOOMERANG        0x2D
#define ITEM_HOOKSHOT         0x2F
#define ITEM_SKULL_HAMMER     0x33
#define ITEM_DEKU_LEAF        0x34
#define ITEM_MAGIC_ARROW      0x35
#define ITEM_LIGHT_ARROW      0x36
#define ITEM_EMPTY_BOTTLE     0x50
#define ITEM_WATER_BOTTLE     0x56
#define ITEM_FOREST_WATER     0x59  // last bottle
#define DAPY_ITEM_SHIP_TACT   0x10A // daPyItem_UNK10A_e: the Wind Waker while conducting on the ship (d_a_player_main.h:941)

// m3562 while a take-out anim runs toward an item this file builds itself (see top).
#define PUPPET_HELD_PENDING_TAG  0x1000
#define PUPPET_HELD_PENDING_MASK 0xF000

// daPy_lk_c fields (d_a_player_main.h)
#define HELD_DAPY_SWORDANIM(link)     ((mDoExt_bckAnm *)((u8 *)(link) + 0x2E9C))        // mDoExt_bckAnm mSwordAnim
#define HELD_DAPY_SWORDANIM_BCK(link) (*(void **)((u8 *)(link) + 0x2E9C + 0x08))       // mSwordAnim.mpAnm (getBckAnm)
#define HELD_DAPY_EQUIPITEMBTK(link)  (*(J3DAnmTextureSRTKey **)((u8 *)(link) + 0x2ED4)) // mpEquipItemBtk
#define HELD_DAPY_EQUIPITEMBRK(link)  (*(J3DAnmTevRegKey **)((u8 *)(link) + 0x2EDC))   // mpEquipItemBrk
#define HELD_DAPY_UPPER2_FRAME(link)  (*(f32 *)((u8 *)(link) + 0x3054 + 2 * 0x14 + 0x10)) // mFrameCtrlUpper[UPPER_MOVE2_e].mFrame

#define HELD_UPPER_MOVE2   2       // daPy_lk_c::UPPER_MOVE2_e
#define HELD_RESET_MORF    2.4f    // m_HIO->mBasic.m.field_0xC (d_a_player_HIO_data.inc:5), checkItemAction's resetActAnimeUpper
#define HELD_NO_MORF       (-1.0f)

// dRes_INDEX_LKANM_BCK_* (GZLE01 LkAnm.h)
#define LKANM_ARROWRELORD  0x0C
#define LKANM_ARROWRELORDA 0x0D
#define LKANM_ARROWSHOOT   0x0E
#define LKANM_ARROWSHOOTA  0x0F
#define LKANM_BOOMCATCH    0x33
#define LKANM_BOOMTHROW    0x34
#define LKANM_BOOMWAIT     0x35
#define LKANM_BOWWAIT      0x36
#define LKANM_BOWWAITA     0x37
#define LKANM_GRABWAIT     0x95
#define LKANM_HOOKSHOTWAIT 0xA7

// daPy_lk_c::daPy_ANM (d_a_player_main.h:635)
#define ANM_GRABP        0x65
#define ANM_GRABUP       0x66
#define ANM_GRABWAIT     0x68
#define ANM_GRABTHROW    0x6A
#define ANM_HOOKSHOTJMP  0xA6
#define ANM_WAITTAKT     0xA8
#define ANM_BINDRINKPRE  0xB6
#define ANM_BINOPENPRE   0xB9
#define ANM_BINSWINGS    0xBC
#define ANM_BINGET       0xBE
#define ANM_USETCEIVER   0xC1

// daPyProc (d_a_player_main.h:414-632)
#define PROC_HAMMER_SIDE_SWING         0x51
#define PROC_HAMMER_FRONT_SWING_READY  0x52
#define PROC_HAMMER_FRONT_SWING        0x53
#define PROC_HAMMER_FRONT_SWING_END    0x54
#define PROC_BOOMERANG_SUBJECT         0x80
#define PROC_BOOMERANG_MOVE            0x81
#define PROC_BOOMERANG_CATCH           0x82
#define PROC_HOOKSHOT_SUBJECT          0x83
#define PROC_HOOKSHOT_MOVE             0x84
#define PROC_FAN_SWING                 0x92
#define PROC_FAN_GLIDE                 0x93
#define PROC_BOW_SUBJECT               0x94
#define PROC_BOW_MOVE                  0x95
#define PROC_SCOPE                     0x00

// ============================================================================
// ITEM TABLES
// ============================================================================

enum
{
  HELD_FAM_NONE = 0,
  HELD_FAM_MODEL,     // telescope, Picto Box, Tingle Tuner: model only
  HELD_FAM_BOW,
  HELD_FAM_HAMMER,
  HELD_FAM_LEAF,
  HELD_FAM_TACT,
  HELD_FAM_BOOMERANG, // REL-drawn
  HELD_FAM_HOOKSHOT,  // body only
  HELD_FAM_BOTTLE,
  HELD_FAM_OWN,       // FAN_GLIDE: its init builds (and its exit rebuilds) the model itself
};

// Items the puppet can hold (bottles are a range). {dItemNo, HELD_FAM_*}
static const u8 l_heldItems[][2] = {
    {ITEM_TELESCOPE, HELD_FAM_MODEL},      {ITEM_TINGLE_TUNER, HELD_FAM_MODEL},
    {ITEM_WIND_WAKER, HELD_FAM_TACT},      {ITEM_PICTO_BOX, HELD_FAM_MODEL},
    {ITEM_DELUXE_PICTO_BOX, HELD_FAM_MODEL}, {ITEM_BOW, HELD_FAM_BOW},
    {ITEM_MAGIC_ARROW, HELD_FAM_BOW},      {ITEM_LIGHT_ARROW, HELD_FAM_BOW},
    {ITEM_BOOMERANG, HELD_FAM_BOOMERANG},  {ITEM_HOOKSHOT, HELD_FAM_HOOKSHOT},
    {ITEM_SKULL_HAMMER, HELD_FAM_HAMMER},  {ITEM_DEKU_LEAF, HELD_FAM_LEAF},
};

// Item procs: {first, last, HELD_FAM_*}. The item they need (the peer's, or the family default).
static const u8 l_heldProcs[][3] = {
    {PROC_HAMMER_SIDE_SWING, PROC_HAMMER_FRONT_SWING_END, HELD_FAM_HAMMER},
    {PROC_BOOMERANG_SUBJECT, PROC_BOOMERANG_CATCH, HELD_FAM_BOOMERANG},
    {PROC_HOOKSHOT_SUBJECT, 0x85, HELD_FAM_HOOKSHOT}, // .. HOOKSHOT_FLY
    {PROC_FAN_SWING, PROC_FAN_SWING, HELD_FAM_LEAF},
    {PROC_FAN_GLIDE, PROC_FAN_GLIDE, HELD_FAM_OWN},
    {PROC_BOW_SUBJECT, PROC_BOW_MOVE, HELD_FAM_BOW},
    {0x9A, 0x9D, HELD_FAM_TACT},                      // TACT_WAIT .. TACT_PLAY_ORIGINAL
    {0xA3, 0xA6, HELD_FAM_BOTTLE},                    // BOTTLE_DRINK .. BOTTLE_GET
};

static const u8 l_heldFamDefault[] = {
    [HELD_FAM_BOW] = ITEM_BOW,           [HELD_FAM_HAMMER] = ITEM_SKULL_HAMMER,
    [HELD_FAM_LEAF] = ITEM_DEKU_LEAF,    [HELD_FAM_TACT] = ITEM_WIND_WAKER,
    [HELD_FAM_BOOMERANG] = ITEM_BOOMERANG, [HELD_FAM_HOOKSHOT] = ITEM_HOOKSHOT,
    [HELD_FAM_BOTTLE] = ITEM_EMPTY_BOTTLE, [HELD_FAM_OWN] = 0,
};

// Mirrored UPPER_MOVE2 anims, set with the peer's own setActAnimeUpper arguments. itemBck: the
// bow's matching string bck (mSwordAnim.changeBckOnly).
typedef struct
{
  u16 bck;
  u16 itemBck;
  f32 rate;
  f32 start;
  s16 end;
  f32 morf;
} HeldUpperAnm;

static const HeldUpperAnm l_heldUpperAnms[] = {
    // procBoomerangSubject_init (d_a_player_boomerang.inc:148)
    {LKANM_BOOMWAIT, 0, 0.0f, 0.0f, -1, HELD_NO_MORF},
    // checkNextActionBoomerangReady: mBoom field_0x4 / 0x8 / 0x0 / 0xC (d_a_player_boomerang.inc:76-83, d_a_player_HIO_data.inc:253)
    {LKANM_BOOMTHROW, 0, 1.5f, 2.0f, 10, 3.0f},
    // changeBoomerangCatchProc: mBoom field_0x20 / 0x24 / 0x2 / 0x28 (boomerang.inc:119-126)
    {LKANM_BOOMCATCH, 0, 1.0f, 1.0f, 11, 0.0f},
    // procHookshotSubject_init (d_a_player_hook.inc:207)
    {LKANM_HOOKSHOTWAIT, 0, 0.0f, 0.0f, -1, HELD_NO_MORF},
    // setBowReloadAnime: mBow field_0x10 / 0x14 / 0x2 / 0x18 (bow.inc:109-121, HIO_data.inc:281)
    {LKANM_ARROWRELORD, LKANM_ARROWRELORDA, 1.0f, 0.0f, 6, 1.0f},
    // checkNextActionBowReady shot: mBow field_0x4 / 0x8 / 0x0 / 0xC (bow.inc:130-138)
    {LKANM_ARROWSHOOT, LKANM_ARROWSHOOTA, 0.9f, 0.0f, 4, 0.0f},
    // checkNextActionBowReady reload end (bow.inc:162-165)
    {LKANM_BOWWAIT, LKANM_BOWWAITA, 1.0f, 0.0f, -1, HELD_NO_MORF},
    // makeItemType bomb (d_a_player_main.cpp:3697); also a carried pot's hold
    {LKANM_GRABWAIT, 0, 0.0f, 0.0f, -1, 5.0f},
};

// Relabelled poses: WAIT, then setSingleMoveAnime(anm, rate, start, end, morf) as the real _init
// does, then mCurProc = the peer's proc. {first proc, last proc, daPy_ANM, end, rate, start, morf}
typedef struct
{
  u8 first;
  u8 last;
  u8 anm;
  s16 end;
  f32 rate;
  f32 start;
  f32 morf;
} HeldPose;

static const HeldPose l_heldPoses[] = {
    // Grab procs read the grabbed actor (d_a_player_grab.inc); mGrab values HIO_data.inc:227
    {0x6E, 0x6E, ANM_GRABP, 4, 0.8f, 0.0f, 1.0f},      // GRAB_READY: field_0x20/0x24/0x0/0x28
    {0x6F, 0x6F, ANM_GRABUP, 9, 0.9f, 0.0f, 1.0f},     // GRAB_UP: field_0x2C/0x30/0x2/0x38
    {0x71, 0x71, ANM_GRABTHROW, 14, 0.8f, 1.0f, 0.0f}, // GRAB_THROW: field_0x58/0x5C/0xA/0x64
    {0x72, 0x72, ANM_GRABUP, 7, -1.1f, 0.0f, 0.0f},    // GRAB_PUT: field_0x68/0x6C/0xC/0x74
    {0x73, 0x73, ANM_GRABWAIT, -1, 1.0f, 0.0f, 3.0f},  // GRAB_WAIT: field_0x78 / 0x7C
    // procHookshotFly_init reads the hookshot's angle (hook.inc:305-323)
    {0x85, 0x85, ANM_HOOKSHOTJMP, -1, 1.0f, 0.0f, 0.0f},
    // procTactWait_init(-1): event + camera (tact.inc:205,262); mTact mAnmRate / field_0x4 (:251)
    {0x9A, 0x9D, ANM_WAITTAKT, -1, 0.8f, 0.0f, 6.0f},
    // Bottles: event + camera; mBottle values HIO_data.inc:275
    {0xA3, 0xA3, ANM_BINDRINKPRE, 81, 1.0f, 0.0f, 5.0f}, // DRINK: 1.0 / 0x5C / 0xC / 0x60
    {0xA4, 0xA4, ANM_BINOPENPRE, 45, 1.0f, 0.0f, 2.0f},  // OPEN: 1.0 / 0x3C / 0x6 / 0x40
    {0xA5, 0xA5, ANM_BINSWINGS, 16, 1.2f, 0.0f, 0.0f},   // SWING (reads the catch target): 0x10/0x14/0x0/0x1C
    {0xA6, 0xA6, ANM_BINGET, 50, 1.0f, 0.0f, 4.0f},      // GET: 0x30/0x34/0x4/0x38
    // DEMO_AGB_USE (Tingle Tuner): dProcAgbUse_init plays a system SE (dproc.inc:1908-1919)
    {0xCA, 0xCA, ANM_USETCEIVER, -1, 1.0f, 0.0f, HELD_RESET_MORF},
};

#define HELD_COUNT(a) ((int)(sizeof(a) / sizeof((a)[0])))

static int held_family(u16 item)
{
  int i;
  if (item >= ITEM_EMPTY_BOTTLE && item <= ITEM_FOREST_WATER)
    return HELD_FAM_BOTTLE;
  for (i = 0; i < HELD_COUNT(l_heldItems); i++)
  {
    if (item == l_heldItems[i][0])
      return l_heldItems[i][1];
  }
  return HELD_FAM_NONE;
}

static int held_procFamily(int proc)
{
  int i;
  for (i = 0; i < HELD_COUNT(l_heldProcs); i++)
  {
    if (proc >= l_heldProcs[i][0] && proc <= l_heldProcs[i][1])
      return l_heldProcs[i][2];
  }
  return HELD_FAM_NONE;
}

int puppet_heldIsItemProc(int proc)
{
  return held_procFamily(proc) != HELD_FAM_NONE;
}

int puppet_heldIsAtnMoveProc(int proc)
{
  return proc == PROC_BOOMERANG_MOVE || proc == PROC_HOOKSHOT_MOVE || proc == PROC_BOW_MOVE;
}

static const HeldUpperAnm *held_findUpper(u16 bck)
{
  int i;
  for (i = 0; i < HELD_COUNT(l_heldUpperAnms); i++)
  {
    if (l_heldUpperAnms[i].bck == bck)
      return &l_heldUpperAnms[i];
  }
  return NULL;
}

// REST / TAKE / TAKEBOTH / TAKEL / TAKER: checkEquipAnime (d_a_player_main.cpp:3565-3574)
static int held_isEquipAnm(u16 bck)
{
  return bck == DAPY_UPPER_ANM_REST || (bck >= DAPY_UPPER_ANM_TAKE && bck <= DAPY_UPPER_ANM_TAKER);
}

// The three bow items are one model.
static int held_same(u16 a, u16 b)
{
  return a == b || (held_family(a) == HELD_FAM_BOW && held_family(b) == HELD_FAM_BOW);
}

// What the puppet should hold for the peer's mEquipItem: the sword, an item above, or nothing.
static u16 held_want(u16 netEquip)
{
  if (netEquip == DAPY_ITEM_SWORD || held_family(netEquip) != HELD_FAM_NONE)
    return netEquip;
  return DAPY_ITEM_NONE;
}

// The active slot of this puppet, or 0.
static u32 held_slotBase(daPy_lk_c *puppet)
{
  u32 slotIndex = *(u32 *)((u8 *)puppet + sizeof(daPy_lk_c)); // PUPPET_class.slotIndex
  u32 slotBase;
  if (slotIndex >= PUPPET_MAX_SLOTS || *(volatile u32 *)PUPPET_SYNC_BASE != PUPPET_SYNC_MAGIC)
    return 0;
  slotBase = PUPPET_SLOT_BASE(slotIndex);
  return *(volatile u32 *)(slotBase + PUPPET_SLOT_OFF_ACTIVE) != 0 ? slotBase : 0;
}

/**
 * held_make - put `item` in the puppet's hand now (DAPY_ITEM_NONE: empty it). Never makes an actor.
 */
static void *held_getRes(u32 index);

// setPhotoBoxModel hides / shows the flash (CAMERA_JNT_FRASH_e, Link.h) on the SHARED camera model
// data for the regular / Deluxe Picto Box (d_a_player_main.cpp:3747-3756). J3DShpFlag_Hide is read at
// entry, so the puppet sets it only around its own entry (puppet_heldFlashBegin) and never leaves
// its choice on the local Link's camera.
#define HELD_RES_BDL_CAMERA  0x17  // dRes_INDEX_LINK_BDL_CAMERA_e (GZLE01 Link.h)
#define HELD_CAMERA_JNT_FRASH 0x02 // CAMERA_JNT_FRASH_e

static J3DShape *held_flashShape(J3DModelData *camera)
{
  return J3DMaterial__getShape(J3DJoint__getMesh(J3DModelData__getJointNodePointer(camera, HELD_CAMERA_JNT_FRASH)));
}

J3DShape *puppet_heldFlashBegin(daPy_lk_c *who, u32 *saved)
{
  u16 item = PUPPET_DAPY_MEQUIPITEM(who);
  J3DModel *model = DAPY_LK_MPHELDITEMMODEL(who);
  J3DShape *flash;

  if ((item != ITEM_PICTO_BOX && item != ITEM_DELUXE_PICTO_BOX) || model == NULL)
    return NULL;
  flash = held_flashShape(J3DMODEL_MPMODELDATA(model));
  if (flash == NULL)
    return NULL;
  *saved = J3DSHAPE_MVISFLAGS(flash);
  if (item == ITEM_PICTO_BOX)
    J3DShape__hide(flash);
  else
    J3DShape__show(flash);
  return flash;
}

static void held_make(daPy_lk_c *puppet, u16 item)
{
  int fam = held_family(item);
  J3DModelData *camera = NULL;
  J3DShape *flash = NULL;
  u32 flashFlags = 0;

  // deleteEquipItem resets a boomerang anim when it deletes a boomerang (d_a_player_main.cpp:3599); the peer's hand
  // empties mid-BOOMTHROW (throwBoomerang, d_a_player_boomerang.inc:34), so keep the throw playing.
  if (PUPPET_DAPY_MEQUIPITEM(puppet) == ITEM_BOOMERANG)
    PUPPET_DAPY_MEQUIPITEM(puppet) = DAPY_ITEM_NONE;
  daPy_lk_c__deleteEquipItem(puppet, 0);
  if (fam == HELD_FAM_NONE)
    return;
  if (fam == HELD_FAM_BOTTLE)
  {
    // setBottleModel sets mEquipItem itself (bottle.inc:30). Forest Water would also start an
    // emitter bound to the model (:80); it looks like plain water.
    daPy_lk_c__setBottleModel(puppet, item == ITEM_FOREST_WATER ? ITEM_WATER_BOTTLE : item);
    return;
  }
  PUPPET_DAPY_MEQUIPITEM(puppet) = item;
  if (fam == HELD_FAM_HOOKSHOT)
    daPy_lk_c__setHookshotModel(puppet); // makeItemType's hookshot branch minus the actor (:3685-3688)
  else if (fam != HELD_FAM_BOOMERANG)
  {
    if (item == ITEM_PICTO_BOX || item == ITEM_DELUXE_PICTO_BOX)
    {
      camera = (J3DModelData *)held_getRes(HELD_RES_BDL_CAMERA);
      flash = camera != NULL ? held_flashShape(camera) : NULL;
      if (flash != NULL)
        flashFlags = J3DSHAPE_MVISFLAGS(flash);
    }
    daPy_lk_c__makeItemType(puppet); // model-only branches (:3704-3729); the Wind Waker's resets mEquipItem itself
    if (flash != NULL)
      J3DSHAPE_MVISFLAGS(flash) = flashFlags; // the local Link's camera keeps its flash
  }
}

// ============================================================================
// HELD ITEM MIRROR
// ============================================================================

void puppet_heldMirror(daPy_lk_c *puppet, u32 slotBase, int wantProc)
{
  u16 want = held_want(*(volatile u16 *)(slotBase + PUPPET_SLOT_OFF_EQUIP_ITEM));
  u16 netAnm = *(volatile u16 *)(slotBase + PUPPET_SLOT_OFF_ANIM_ID);
  u16 equip = PUPPET_DAPY_MEQUIPITEM(puppet);
  u16 pending = PUPPET_DAPY_M3562(puppet) & ~PUPPET_HELD_PENDING_MASK;
  int equipAnm = held_isEquipAnm(PUPPET_DAPY_UPPER2_ANM(puppet));
  int cur = DAPY_LK_MCURPROC(puppet);
  int curFam = held_procFamily(cur);

  // Cuts draw the sword themselves (puppet_applyProcInit).
  if (puppet_isSwordProc(wantProc) || puppet_isSwordProc(cur))
    return;
  // An item proc owns the item; only follow a change within its family (a bottle filled/emptied).
  if (curFam != HELD_FAM_NONE || puppet_heldIsItemProc(wantProc))
  {
    if (curFam != HELD_FAM_NONE && curFam != HELD_FAM_OWN && held_family(want) == curFam && !held_same(want, equip))
      held_make(puppet, want);
    return;
  }

  if (held_same(want, equip) || (equipAnm && held_same(pending, want)))
    return;

  if (want == DAPY_ITEM_SWORD)
  {
    // setAnimeEquipSword returns early if !checkSwordEquip() (the peer's select-equip byte, swapped
    // in by the guard); then draw it instantly.
    daPy_lk_c__setAnimeEquipSword(puppet, 1);
    if (PUPPET_DAPY_UPPER2_ANM(puppet) != LKANM_BCK_REST)
      puppet_setSwordOut(puppet, 1);
    return;
  }
  if (equip == DAPY_ITEM_SWORD && want == DAPY_ITEM_NONE)
  {
    daPy_lk_c__setAnimeUnequip(puppet); // REST anim, m3562 = NONE
    return;
  }

  if (held_isEquipAnm(netAnm))
  {
    // The peer is drawing / putting away too: same anim, item swapped at its swap frame.
    if (want == DAPY_ITEM_NONE)
    {
      daPy_lk_c__setAnimeUnequip(puppet); // TAKEL/TAKER/TAKEBOTH/TAKE, m3562 = NONE
    }
    else
    {
      PUPPET_DAPY_M3562(puppet) = PUPPET_HELD_PENDING_TAG | want;
      daPy_lk_c__setAnimeUnequipItem(puppet, want);
    }
    return;
  }

  // Instant on the peer's side (a throw, an item proc ending, an old client): instant here too.
  if (equipAnm)
    daPy_lk_c__resetActAnimeUpper(puppet, HELD_UPPER_MOVE2, HELD_NO_MORF);
  held_make(puppet, want);
}

void puppet_heldFinishSwap(daPy_lk_c *puppet)
{
  u16 equip = PUPPET_DAPY_MEQUIPITEM(puppet);
  if ((equip & PUPPET_HELD_PENDING_MASK) != PUPPET_HELD_PENDING_TAG)
    return;
  PUPPET_DAPY_MEQUIPITEM(puppet) = DAPY_ITEM_NONE;
  held_make(puppet, equip & ~PUPPET_HELD_PENDING_MASK);
}

int puppet_heldUpperMirrored(daPy_lk_c *puppet)
{
  return held_findUpper(PUPPET_DAPY_UPPER2_ANM(puppet)) != NULL;
}

void puppet_heldMirrorUpper(daPy_lk_c *puppet, u32 slotBase)
{
  u16 net = *(volatile u16 *)(slotBase + PUPPET_SLOT_OFF_ANIM_ID);
  u16 cur = PUPPET_DAPY_UPPER2_ANM(puppet);
  const HeldUpperAnm *want;

  // Our own draw / put-away finishes first (its swap frame builds the item).
  if (net == cur || held_isEquipAnm(cur))
    return;
  want = held_findUpper(net);
  if (want == NULL)
  {
    // Only drop what this mirror owns (guard / equip anims belong to other code).
    if (held_findUpper(cur) != NULL)
      daPy_lk_c__resetActAnimeUpper(puppet, HELD_UPPER_MOVE2, HELD_RESET_MORF);
    return;
  }
  daPy_lk_c__setActAnimeUpper(puppet, want->bck, HELD_UPPER_MOVE2, want->rate, want->start, want->end, want->morf);
  // The bow string follows (setBowReloadAnime etc.); changeBckOnly asserts mAnm != NULL (m_Do_ext.cpp:464).
  if (want->itemBck != 0 && held_family(PUPPET_DAPY_MEQUIPITEM(puppet)) == HELD_FAM_BOW &&
      PUPPET_DAPY_SWORDANIM_MANM(puppet) != NULL)
  {
    mDoExt_bckAnm__changeBckOnly(HELD_DAPY_SWORDANIM(puppet), daPy_lk_c__getItemAnimeResource(puppet, want->itemBck));
    PUPPET_DAPY_M35EC(puppet) = want->start;
  }
}

// ============================================================================
// ITEM PROCS
// ============================================================================

int puppet_heldForProc(daPy_lk_c *puppet, int proc)
{
  int fam = held_procFamily(proc);
  u32 slotBase;
  u16 item;

  if (fam == HELD_FAM_NONE || fam == HELD_FAM_OWN)
    return 1;
  slotBase = held_slotBase(puppet);
  item = slotBase ? held_want(*(volatile u16 *)(slotBase + PUPPET_SLOT_OFF_EQUIP_ITEM)) : DAPY_ITEM_NONE;
  if (held_family(item) != fam)
    item = l_heldFamDefault[fam];
  if (!held_same(item, PUPPET_DAPY_MEQUIPITEM(puppet)))
  {
    // A draw / put-away in progress would swap its own item in at its swap frame.
    if (held_isEquipAnm(PUPPET_DAPY_UPPER2_ANM(puppet)))
      daPy_lk_c__resetActAnimeUpper(puppet, HELD_UPPER_MOVE2, HELD_NO_MORF);
    held_make(puppet, item);
  }
  // Bow / hammer inits call mSwordAnim.changeBckOnly, which asserts mAnm != NULL (m_Do_ext.cpp:464):
  // only the matching setBowModel / setHammerModel creates it (bow.inc:220, hammer.inc:25).
  if (fam == HELD_FAM_BOW || fam == HELD_FAM_HAMMER)
    return held_family(PUPPET_DAPY_MEQUIPITEM(puppet)) == fam && PUPPET_DAPY_SWORDANIM_MANM(puppet) != NULL;
  return 1;
}

// Item procs whose real _init is safe for a puppet: anims, mode flags, player status 0/1 (guarded),
// magic (guarded), AT params whose AT bits puppet_applyProcInit clears. The second argument is
// FAN_GLIDE's (0); the other inits take none (PPC: r4 is simply ignored).
typedef int (*HeldInitFn)(daPy_lk_c *, int);
typedef struct
{
  u8 proc;
  u8 once; // the init refuses re-entry (returns FALSE while already in the proc): already there = ok
  HeldInitFn init;
} HeldInit;

static const HeldInit l_heldInits[] = {
    {PROC_BOOMERANG_SUBJECT, 1, (HeldInitFn)daPy_lk_c__procBoomerangSubject_init}, // boomerang.inc:139
    {PROC_BOOMERANG_MOVE, 1, (HeldInitFn)daPy_lk_c__procBoomerangMove_init},       // :192
    {PROC_BOOMERANG_CATCH, 1, (HeldInitFn)daPy_lk_c__procBoomerangCatch_init},     // :241
    {PROC_HOOKSHOT_SUBJECT, 1, (HeldInitFn)daPy_lk_c__procHookshotSubject_init},   // hook.inc:198
    {PROC_BOW_SUBJECT, 1, (HeldInitFn)daPy_lk_c__procBowSubject_init},             // bow.inc:236 (needs setBowModel)
    {PROC_BOW_MOVE, 1, (HeldInitFn)daPy_lk_c__procBowMove_init},
    // setFanModel; its exit (commonProcInit) puts the small leaf back (d_a_player_main.cpp:5732)
    {PROC_FAN_SWING, 0, (HeldInitFn)daPy_lk_c__procFanSwing_init},
    // parachute model; its exit restores the leaf (:5734-5743)
    {PROC_FAN_GLIDE, 1, (HeldInitFn)daPy_lk_c__procFanGlide_init},
    // hammer.inc: anims, the blur buffer setHammerModel allocated, mSwordAnim (needs setHammerModel)
    {PROC_HAMMER_SIDE_SWING, 0, (HeldInitFn)daPy_lk_c__procHammerSideSwing_init},
    {PROC_HAMMER_FRONT_SWING_READY, 0, (HeldInitFn)daPy_lk_c__procHammerFrontSwingReady_init},
    {PROC_HAMMER_FRONT_SWING, 0, (HeldInitFn)daPy_lk_c__procHammerFrontSwing_init},
    {PROC_HAMMER_FRONT_SWING_END, 0, (HeldInitFn)daPy_lk_c__procHammerFrontSwingEnd_init},
};

int puppet_heldInitProc(daPy_lk_c *puppet, int proc)
{
  int cur = DAPY_LK_MCURPROC(puppet);
  int i;

  for (i = 0; i < HELD_COUNT(l_heldInits); i++)
  {
    if (proc == l_heldInits[i].proc)
      return (l_heldInits[i].once && cur == proc) || l_heldInits[i].init(puppet, 0);
  }

  // procHookshotMove_init reads the hookshot actor (d_a_player_hook.inc:259): strafe like ATN_MOVE with the
  // aim hold, relabelled (puppet_executeBody blends it like ATN_MOVE).
  if (proc == PROC_HOOKSHOT_MOVE)
  {
    if (cur != proc && cur != PROC_ATN_MOVE && !daPy_lk_c__procAtnMove_init(puppet))
      return 0;
    daPy_lk_c__setActAnimeUpper(puppet, LKANM_HOOKSHOTWAIT, HELD_UPPER_MOVE2, 1.0f, 0.0f, -1, HELD_NO_MORF);
    DAPY_LK_MCURPROC(puppet) = proc;
    return 1;
  }

  for (i = 0; i < HELD_COUNT(l_heldPoses); i++)
  {
    const HeldPose *e = &l_heldPoses[i];
    if (proc < e->first || proc > e->last)
      continue;
    // procWait_init refuses when already in WAIT (d_a_player_main.cpp:6043), which is fine here.
    if (cur != PROC_WAIT && !daPy_lk_c__procWait_init(puppet))
      return 0;
    daPy_lk_c__setSingleMoveAnime(puppet, e->anm, e->rate, e->start, e->end, e->morf);
    DAPY_LK_MCURPROC(puppet) = proc;
    return 1;
  }
  return -1;
}

// ============================================================================
// PER-FRAME POSE
// ============================================================================

// Procs whose aim the peer's mBodyAngle carries: scope/picto, boomerang, hookshot, bow, and the
// same on the boat (SHIP_SCOPE .. SHIP_BOW).
static int held_isAimProc(int proc)
{
  return proc == PROC_SCOPE || proc == PROC_BOOMERANG_SUBJECT || proc == PROC_BOOMERANG_MOVE ||
         proc == PROC_HOOKSHOT_SUBJECT || proc == PROC_HOOKSHOT_MOVE || proc == PROC_BOW_SUBJECT ||
         proc == PROC_BOW_MOVE || (proc >= 0x8A && proc <= 0x8D);
}

void puppet_heldPose(daPy_lk_c *puppet)
{
  int cur = DAPY_LK_MCURPROC(puppet);
  u32 slotBase;

  // The item's bck frame (setItemModel plays mSwordAnim at m35EC, d_a_player_main.cpp:1211),
  // as the per-frame procs set it: fan / hammer from the body anim (fan.inc:320, hammer.inc:191/
  // 265/316), the bow from the upper anim (bow.inc:265/315).
  if (HELD_DAPY_SWORDANIM_BCK(puppet) != NULL && PUPPET_DAPY_SWORDANIM_MANM(puppet) != NULL)
  {
    if (cur == PROC_FAN_SWING || (cur >= PROC_HAMMER_SIDE_SWING && cur <= PROC_HAMMER_FRONT_SWING_END))
      PUPPET_DAPY_M35EC(puppet) = PUPPET_DAPY_UNDER0_FRAME(puppet);
    else if (held_family(PUPPET_DAPY_MEQUIPITEM(puppet)) == HELD_FAM_BOW &&
             held_findUpper(PUPPET_DAPY_UPPER2_ANM(puppet)) != NULL)
      PUPPET_DAPY_M35EC(puppet) = HELD_DAPY_UPPER2_FRAME(puppet);
  }

  // Aim: the peer's mBodyAngle x/y (pitch, yaw relative to the facing) while they aim. Set after
  // execute's body-angle decay and before the model calc, whose joint callbacks read it.
  slotBase = held_slotBase(puppet);
  if (slotBase != 0 && held_isAimProc(*(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_CUR_PROC)))
  {
    DAPY_PY_BODY_ANGLE_X(puppet) = *(volatile s16 *)(slotBase + PUPPET_SLOT_OFF_BODY_ANGLE_X);
    DAPY_PY_BODY_ANGLE_Y(puppet) = *(volatile s16 *)(slotBase + PUPPET_SLOT_OFF_BODY_ANGLE_Y);
  }
}

// ============================================================================
// REL MODELS: held boomerang, carried bomb
// ============================================================================

#define HELD_ARC_NAME          "Link"
#define HELD_RES_BDL_BOOMERANG 0x14        // dRes_INDEX_LINK_BDL_BOOMERANG_e (GZLE01 Link.h)
#define HELD_RES_BDL_BOMB      0x3C        // dRes_INDEX_LINK_BDL_BOMB_e
#define HELD_RES_BCK_BOMB      0x0B        // dRes_INDEX_LINK_BCK_BOMB_e: the fuse bck daBomb_c plays (createHeap, d_a_bomb3.inc:1350)
#define HELD_BCK_LOOP          2           // J3DFrameCtrl::EMode_LOOP (d_a_bomb3.inc:1351)
#define J3DANM_OFF_FRAME_MAX   0x06        // J3DAnmBase::mFrameMax (s16; draw_norm DOL 0x800D97EC: lha r4,6(brk))
#define J3DANM_OFF_FRAME       0x08        // J3DAnmBase::mFrame (f32; draw_norm 0x800D9868: stfs f2,8(brk))
#define HELD_MODEL_FLAG        0x80000
#define HELD_BOOMERANG_DIFF    0x37220202  // daBoomerang_c::createHeap (d_a_boomerang.cpp:750)
#define HELD_BOMB_DIFF         0x11000002  // daBomb_c::createHeap (d_a_bomb3.inc:1344)
#define HELD_HEAP_SIZE         0x8000      // adjusted down once built
#define HELD_HEAP_ALIGN        0x20
#define HELD_CL_JNT_LHANDA     0x08        // cl.bdl CL_JNT_CL_LHANDA_e (getLeftHandMatrix, d_a_player_main.h:1946)
#define J3DJOINT_OFF_MTXCALC   0x58        // J3DJoint::mMtxCalc

void puppet_heldInit(PuppetHeld *held)
{
  held->heap = NULL;
  held->boomerang = NULL;
  held->bomb = NULL;
  held->state = PUPPET_HELD_STATE_NONE;
  held->bombBck[0] = held->bombBck[1] = held->bombBck[2] = held->bombBck[3] = 0;
  held->fuse = 0;
  held->netFuse = 0;
  held->bckReady = 0;
  held->showBomb = 0;
}

static void *held_getRes(u32 index)
{
  return dRes_control_c__getRes(HELD_ARC_NAME, index, GAMEINFO_RES_OBJECT_INFO(&g_dComIfG_gameInfo),
                                GAMEINFO_RES_OBJECT_INFO_COUNT);
}

// Both models in a solid heap of our own (the puppet's actor heap belongs to playerInit).
static int held_build(PuppetHeld *held)
{
  J3DModelData *boomData = (J3DModelData *)held_getRes(HELD_RES_BDL_BOOMERANG);
  J3DModelData *bombData = (J3DModelData *)held_getRes(HELD_RES_BDL_BOMB);
  JKRSolidHeap *heap;

  if (boomData == NULL || bombData == NULL)
    return 0;
  heap = mDoExt_createSolidHeapFromGameToCurrent(HELD_HEAP_SIZE, HELD_HEAP_ALIGN);
  if (heap == NULL)
    return 0;
  held->boomerang = mDoExt_J3DModel__create(boomData, HELD_MODEL_FLAG, HELD_BOOMERANG_DIFF);
  held->bomb = mDoExt_J3DModel__create(bombData, HELD_MODEL_FLAG, HELD_BOMB_DIFF);
  // The fuse bck, as daBomb_c::createHeap: init(modelData, bck, FALSE, LOOP) makes a J3DMtxCalcMayaAnm
  // in the current (our solid) heap; no frame control (we set the frame). Optional: without it the
  // carried bomb just keeps its bind pose.
  {
    J3DAnmTransform *bck = (J3DAnmTransform *)held_getRes(HELD_RES_BCK_BOMB);
    held->bckReady = bck != NULL &&
                     mDoExt_bckAnm__init((mDoExt_bckAnm *)held->bombBck, bombData, bck, 0, HELD_BCK_LOOP, 1.0f, 0, -1, 0);
  }
  mDoExt_restoreCurrentHeap();
  if (held->boomerang == NULL || held->bomb == NULL)
  {
    mDoExt_destroySolidHeap(heap);
    held->boomerang = NULL;
    held->bomb = NULL;
    return 0;
  }
  mDoExt_adjustSolidHeap(heap);
  held->heap = heap;
  return 1;
}

void puppet_heldExecute(daPy_lk_c *puppet, PuppetHeld *held)
{
  u32 slotBase = held_slotBase(puppet);

  held->showBomb = slotBase != 0 &&
                   *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_GRAB_KIND) == PUPPET_GRAB_KIND_BOMB;

  // The carried bomb's fuse: the peer's mRestTime arrives 20 times a second; count it down here in
  // between (as their bomb's checkExplodeTimer does), and snap to each new value.
  {
    u8 net = held->showBomb ? *(volatile u8 *)(slotBase + PUPPET_SLOT_OFF_GRAB_FUSE) : 0;
    if (net == 0)
      held->fuse = 0;
    else if (net != held->netFuse)
      held->fuse = net;
    else if (held->fuse > 1)
      held->fuse--;
    held->netFuse = net;
  }
  if (held->state == PUPPET_HELD_STATE_NONE &&
      (held->showBomb || PUPPET_DAPY_MEQUIPITEM(puppet) == ITEM_BOOMERANG))
  {
    held->state = held_build(held) ? PUPPET_HELD_STATE_READY : PUPPET_HELD_STATE_FAILED;
    if (held->state == PUPPET_HELD_STATE_FAILED)
      OSReport("[PUPPET] held: model build failed\n");
  }
}

void puppet_heldDraw(daPy_lk_c *puppet, PuppetHeld *held)
{
  J3DModel *clModel = DAPY_LK_MPCLMODEL(puppet);
  MTX34 *nodeMtx;
  MTX34 m;

  // (An inactive slot: puppet_draw drew nothing either.)
  if (held->state != PUPPET_HELD_STATE_READY || clModel == NULL || held_slotBase(puppet) == 0 ||
      daPy_lk_c__checkCaughtShapeHide(puppet))
    return;
  nodeMtx = *(MTX34 **)((u8 *)clModel + 0x8C); // J3DModel::mpNodeMtx (getAnmMtx)
  if (nodeMtx == NULL)
    return;

  dComIfGd_setListP1();

  // Boomerang in the left hand: daBoomerang_c::setKeepMatrix (d_a_boomerang.cpp:434-440).
  if (PUPPET_DAPY_MEQUIPITEM(puppet) == ITEM_BOOMERANG)
  {
    PSMTXTrans(10.3f, 36.6f, 1.1f, &m);
    mDoMtx_XYZrotM(&m, -0x39F4, -0x357, -0x2AF3);
    PSMTXConcat(&nodeMtx[HELD_CL_JNT_LHANDA], &m, (MTX34 *)((u8 *)held->boomerang + 0x24)); // mBaseTransformMtx
    daPy_lk_c__updateDLSetLight(puppet, held->boomerang, 0);
  }

  // Carried bomb between the hands, as setGrabItemPos places the grabbed actor (d_a_player_grab.inc:
  // 131) and daBomb_c::set_mtx orients it (d_a_bomb3.inc:1212-1222).
  if (held->showBomb)
  {
    cXyz *l = DAPY_PY_LEFT_HAND_POS(puppet);
    cXyz *r = DAPY_PY_RIGHT_HAND_POS(puppet);
    J3DModelData *data = J3DMODEL_MPMODELDATA(held->bomb);
    J3DJoint **joints = J3DMODELDATA_MJOINTTREE_MPJOINTS(data);
    J3DAnmTevRegKey *brk = (J3DAnmTevRegKey *)DAPY_LK_MPBOMBBRK(puppet);
    void *savedCalc = NULL;

    PSMTXTrans((l->x + r->x) * 0.5f, (l->y + r->y) * 0.5f, (l->z + r->z) * 0.5f, &m);
    mDoMtx_YrotM(&m, FOPAC_SHAPE_ANGLE((fopAc_ac_c *)puppet)->y);
    PSMTXCopy(&m, (MTX34 *)((u8 *)held->bomb + 0x24));
    // Joint 0 of the SHARED bomb data carries the last daBomb's fuse bck (mBck0.entry, d_a_bomb3.inc:
    // 143), possibly a deleted bomb's: calc ours with our own bck calc (or the bind pose) and put it back
    // right after, so nothing of ours stays in the shared data (and nothing a puppet window records).
    if (joints != NULL && joints[0] != NULL)
    {
      savedCalc = *(void **)((u8 *)joints[0] + J3DJOINT_OFF_MTXCALC);
      *(void **)((u8 *)joints[0] + J3DJOINT_OFF_MTXCALC) = NULL;
      // daBomb_c::draw_norm's bck frame: end - fuse, in [0, end) (d_a_bomb3.inc:139-148).
      if (held->fuse > 0 && held->bckReady)
      {
        u8 *bck = (u8 *)held->bombBck[2]; // mDoExt_bckAnm::mpAnm (+0x08)
        f32 end = (f32) * (s16 *)(bck + J3DANM_OFF_FRAME_MAX);
        f32 f = end - (f32)held->fuse;
        f = f < 0.0f ? 0.0f : (f >= end ? end - 0.001f : f);
        mDoExt_bckAnm__entry((mDoExt_bckAnm *)held->bombBck, data, f); // frame + joint 0's calc = ours
      }
    }
    // The fuse glow brk is the archive's, shared by every bomb (entryBrk, d_a_player_main.cpp:11990),
    // and each bomb sets its frame before its draw: end - fuse + 2 (d_a_bomb3.inc:128-137), so it
    // flashes faster as the fuse runs down. Frame 0 = not flashing (fuse unknown).
    if (brk != NULL)
    {
      f32 f = 0.0f;
      if (held->fuse > 0)
      {
        f32 end = (f32) * (s16 *)((u8 *)brk + J3DANM_OFF_FRAME_MAX);
        f = end - (f32)held->fuse + 2.0f;
        f = f < 0.0f ? 0.0f : (f >= end ? end - 0.001f : f);
      }
      *(f32 *)((u8 *)brk + J3DANM_OFF_FRAME) = f;
    }
    daPy_lk_c__updateDLSetLight(puppet, held->bomb, 0);
    if (joints != NULL && joints[0] != NULL)
      *(void **)((u8 *)joints[0] + J3DJOINT_OFF_MTXCALC) = savedCalc;
  }

  dComIfGd_setList();
}

void puppet_heldDelete(PuppetHeld *held)
{
  if (held->heap != NULL)
    mDoExt_destroySolidHeap(held->heap);
  puppet_heldInit(held);
}
