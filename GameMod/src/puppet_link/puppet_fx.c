/**
 * puppet_fx.c - other players' bombs, boat-cannon shots and arrows, real in this world (see puppet_fx.h).
 * Included by puppet.c after puppet_nametag.c (puppet_bootBlock) and puppet_worldsync.c (ws_isValidPtr).
 *
 * SENDER (the local Link, fx_trackLocal). A hand bomb is a daBomb_c the local Link carries in
 * mActorKeepGrab (STATE_3 from the bag, STATE_2 while carried, STATE_1 once released: d_a_bomb3.inc
 * procCarry_init / procWait_init). It is tracked from the first frame Link carries it, and reported:
 *   BOMB_THROW  the frame after it left his hands (procGrabThrow / put down: freeGrabItem clears the
 *               grab, d_a_player_grab.inc:185) - its state then: position, speedF, speed.y, heading, fuse;
 *   BOMB_PICKUP when he carries it again; EXPLODE when its mRestTime is 0 (procExplode_init zeroes it;
 *               a live bomb's is > 0) - also in his hands; REMOVE when it is gone without exploding (sank).
 * The boat's cannonball is found the frame the ship reports daSFLG_SHOOT_CANNON: the one BOMB actor
 * with exactly the ship's params (prm_make(STATE_4, FALSE, TRUE), d_a_ship.cpp:4021; enemy cannons use
 * cheapEff = TRUE) - CANNON with its launch state, then EXPLODE / REMOVE like a bomb.
 * An arrow is the daArrow_c nocked in mActorKeepEquip (makeArrow, d_a_player_bow.inc:68-78): the frame
 * after it left the bow with param 1 (the shot, :136) - ARROW with its type, launch point and aim. It
 * hasn't moved yet then (the arrow executes after the puppets: lists 9 / 3). One event: the copy flies
 * the same straight line through the same stage.
 *
 * VIEWER (the peers' events, fx_apply). A copy is a real daBomb_c made by fopAcM_fastCreate (as the
 * player's own bombs and the ship's cannonball are; the game already makes bombs for a second player:
 * the Tingle Tuner's STATE_8, fopAcM_create in d_a_agb.cpp:956): a thrown bomb is a STATE_1 (lit)
 * bomb put at the sender's position with their velocity and fuse; the cannonball is the ship's own
 * STATE_4 prm with its no-gravity frames. Every copy:
 *   - has FX_BOMB_PRM_COPY in its params, so no tracker (this REL or a later one) reports it back;
 *   - has DABOMB_OWNED_BY_LINK cleared: explode / delete never call the local Link's decrementBombCnt
 *     (only STATE_3 sets it; the viewer's bomb count, gameInfo +0x5B84, is never touched here);
 *   - hits enemies, walls, switches and other bombs (AT groups enemy / other) but not the player
 *     group: the local Link, his ship and the puppets (VsPlayer cleared). No friendly fire;
 *   - holds its fuse (FX_HOLD_FUSE, after its own count reached it) for up to FX_EXPLODE_GRACE frames
 *     so it explodes when the sender's did, at the sender's position (EXPLODE snaps it there). Only while
 *     it is as spawned: once something here carries it (the local Link, an enemy), changes its state
 *     (an Armos swallowing it, d_a_am.cpp:400) or sets its fuse lower (setBombRestTime(1), :942), it is
 *     this world's bomb: its own fuse ends it where it is and the sender's EXPLODE is ignored. An
 *     EXPLODE with no copy (it blew up in the sender's hands, or the copy sank here) spawns a STATE_0
 *     bomb, which explodes at create (d_a_canon.cpp:191 does the same).
 * Copies live in the stage layer (the puppet's layer is current during its execute) and die with the
 * stage. The table is REL data: a new REL instance starts empty and old copies finish as plain bombs.
 * Explosions keep their vanilla rumble, light, wind, sound and AI noise (procExplode_init).
 *
 * ARROWS (viewer, fx_spawnArrow). A real daArrow_c, detached from the local Link: fopAcM_fastCreate with
 * no parent (so not Zelda's: checkCreater), the peer's type in the static m_keep_type and, if needed, the
 * local magic raised to 2 around the create only (setTypeByPlayer / checkRestMp would make it a normal
 * arrow; the light arrow's model and heap are chosen at create). createInit put it in the LOCAL Link's
 * hand (setKeepMatrix), so it is moved to the sender's launch point and aim; its CO sphere is switched
 * off (a stuck normal arrow touched by the local Link would give him +1 arrow, procStop_BG); its proc is
 * set to procMove and arrowShooting runs, as procWait does on the shot. Its param stays 0: procWait (which
 * spends the local magic on param 1) never runs, and the fire / ice / light effect child
 * (d_a_arrow_lighteff) only touches the local Link's USE_ARROW_EFFECT flag once the arrow's param was 1.
 * The arrow's AT is enemy / other only (m_at_cps_src): it hits the viewer's enemies, eye switches and
 * torches, never Link. arrowShooting's AT capsule registration (dCcS) is the arrow's only one in its
 * spawn frame: an actor fastCreate'd during another's execute first executes the next frame (it enters
 * the execute queue in fpcCt_Handler, before fpcEx_Handler: f_pc_create_req.cpp:100, f_pc_manager.cpp:284).
 *
 * Every event carries the projectile's own room (current.roomNo) so the client scopes it to the room
 * where it happened, not where its thrower is by the time it is sent. The link is play.mpPlayerPtr[0],
 * not dComIfGp_getPlayer(0), which is the NPC the player controls (Medli, Makar, a seagull, Hyoi).
 * (Peers' projectiles during the viewer's minigames are dropped by the client: PlayerEventService.)
 */

#include "puppet_fx.h"

#define FX_TRACK_MAX        6     // the local Link's bombs + cannonballs in the air (he has at most 3 bombs out)
#define FX_COPY_MAX         8
#define FX_EVENTS_PER_FRAME 4
#define FX_MIN_FUSE         3     // a thrown copy's fuse at least: FX_HOLD_FUSE, else its first frame would look game-set
#define FX_HOLD_FUSE        3     // a held copy's mRestTime: its execute counts it to 2; anything lower was the game
#define FX_MAX_FUSE         600
#define FX_EXPLODE_GRACE    60    // frames past the sender's fuse a copy waits for their EXPLODE (then its own fuse ends it)
#define FX_LEAVE_FRAMES     30    // REMOVE: the copy gets this long to sink here too, then it is deleted
#define FX_DONE_FRAMES      300   // an exploded / gone copy is remembered this long, so a late duplicate can't respawn it
#define FX_MAX_NO_GRAVITY   60
#define FX_MAX_COORD        1.0e6f
#define FX_MAX_SPEED        1000.0f
#define FX_MAX_GRAVITY      50.0f

// Marks our copies in the bomb's own params (free bits, ww_inlines.h DABOMB_PRM_*).
#define FX_BOMB_PRM_COPY    0x01000000
#define FX_SHIP_CANNON_PRM  (DABOMB_PRM_VERSION | DABOMB_PRM_ANGXZERO | DABOMB_STATE_CANNON)

enum
{
  FX_TRACK_FREE = 0,
  FX_TRACK_CARRIED, // in the local Link's hands
  FX_TRACK_FLYING,  // thrown / put down / fired: reported
};

enum
{
  FX_COPY_FREE = 0,
  FX_COPY_LIVE,     // our copy is out
  FX_COPY_LEAVING,  // the sender's is gone without exploding: ours goes after FX_LEAVE_FRAMES
  FX_COPY_EXPLODED, // exploded (the sender's EXPLODE, or here): a late event changes nothing
  FX_COPY_GONE,     // our copy is gone without exploding here: a late EXPLODE still explodes
};

typedef struct
{
  u32 pid;
  u8 state; // FX_TRACK_*
  s8 room;  // its current.roomNo when last seen (for REMOVE, sent after it is gone)
} FxTrack;

typedef struct
{
  u32 id;     // the sender's projectile
  u32 pid;    // our copy
  u16 age;    // frames in this state
  s16 hold;   // LIVE hand bombs: the fuse is held while age < hold
  u8 slot;    // the sender's puppet slot
  u8 state;   // FX_COPY_*
  u8 cannon;  // a cannonball: no fuse (ATTR_STATE_200 skips checkExplodeTimer)
  u8 local;   // this world took it over (carried, swallowed, fuse set by the game): its own fuse ends it
} FxCopy;

typedef struct
{
  cXyz pos;
  f32 speedF;
  f32 speedY;
  f32 gravity;
  csXyz angle;
  s16 timer;
  u32 id;
  u8 kind;
  u8 slot;
  u8 variant;
} FxEvent;

static FxTrack l_fxTrack[FX_TRACK_MAX];
static FxCopy l_fxCopy[FX_COPY_MAX];
static u32 l_fxArrowId; // the local Link's nocked arrow (mActorKeepEquip), 0 = none
static u32 l_fxLastFrame;
static u8 l_fxHasRun;
static u8 l_fxAdopted;

#define FX_HDR(block, off) (*(volatile u32 *)((u8 *)(block) + (off)))

// ============================================================================
// EVENTS BLOCK (made and adopted like the names block: puppet_bootBlock, puppet_nametag.c)
// ============================================================================

static u8 *fx_block(void)
{
  return puppet_bootBlock(PUPPET_FX_PTR_ADDR, PUPPET_FX_MAGIC, PUPPET_FX_BLOCK_SIZE);
}

void puppet_fx_onCreate(void)
{
  u8 *old = fx_block();
  puppet_bootBlockCreate(PUPPET_FX_PTR_ADDR, PUPPET_FX_MAGIC, PUPPET_FX_BLOCK_SIZE);
  u8 *block = fx_block();
  if (!block)
    return;
  // A new REL instance adopting the block: whatever C# queued while none ran is stale.
  if (old && !l_fxAdopted)
    FX_HDR(block, PUPPET_FX_OFF_IN_READ) = FX_HDR(block, PUPPET_FX_OFF_IN_WRITE);
  l_fxAdopted = 1;
}

// A live actor of that kind by process id, or NULL (gone, being deleted, still being created, another kind).
static fopAc_ac_c *fx_find(u32 pid, s16 name)
{
  fopAc_ac_c *a = NULL;
  if (pid == 0 || pid == FPC_PROC_ID_ERROR)
    return NULL;
  fopAcM_SearchByID(pid, &a);
  if (!ws_isValidPtr(a) || BASE_INIT_STATE(a) == BASE_INIT_STATE_DELETING || BASE_PROC_NAME(a) != name)
    return NULL;
  return a;
}

#define fx_actor(pid) fx_find((pid), FPC_NAME_BOMB)

// ============================================================================
// SENDER: the local Link's projectiles -> outbox
// ============================================================================

// The event from `ac` (position, speeds, angles, timer, room), or with no actor at `at` in `room`.
static void fx_emit(u8 *block, u8 kind, u32 id, fopAc_ac_c *ac, const cXyz *at, u8 variant, s8 room)
{
  u32 w = FX_HDR(block, PUPPET_FX_OFF_OUT_WRITE);
  if (w - FX_HDR(block, PUPPET_FX_OFF_OUT_READ) >= PUPPET_FX_OUT_COUNT)
  {
    FX_HDR(block, PUPPET_FX_OFF_OUT_DROPPED)++; // C# isn't draining
    return;
  }
  u8 *ev = PUPPET_FX_EVENT(block, PUPPET_FX_OFF_OUT_RING, PUPPET_FX_OUT_COUNT, w + 1);
  for (u32 i = 0; i < PUPPET_FX_EVENT_SIZE; i += 4)
    FX_HDR(ev, i) = 0;
  const cXyz *pos = ac ? FOPAC_CURRENT_POS(ac) : at;
  *(volatile u8 *)(ev + PUPPET_FX_EV_OFF_KIND) = kind;
  *(volatile u8 *)(ev + PUPPET_FX_EV_OFF_VARIANT) = variant;
  *(volatile s8 *)(ev + PUPPET_FX_EV_OFF_ROOM) = ac ? FOPAC_CURRENT_ROOMNO(ac) : room;
  FX_HDR(ev, PUPPET_FX_EV_OFF_ID) = id;
  *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_POSX) = pos->x;
  *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_POSY) = pos->y;
  *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_POSZ) = pos->z;
  if (ac)
  {
    *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_SPEED_F) = FOPAC_SPEED_F(ac);
    *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_SPEED_Y) = FOPAC_SPEED(ac)->y;
    *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_GRAVITY) = FOPAC_GRAVITY(ac);
    *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_ANGLE_X) = FOPAC_CURRENT_ANGLE(ac)->x;
    *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_ANGLE_Y) = FOPAC_CURRENT_ANGLE(ac)->y;
    *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_ANGLE_Z) = FOPAC_SHAPE_ANGLE(ac)->z;
    if (kind != PUPPET_FX_KIND_ARROW) // (an arrow has no timer; these are daBomb_c fields)
      *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_TIMER) =
          kind == PUPPET_FX_KIND_CANNON ? DABOMB_NO_GRAVITY_TIME(ac) : DABOMB_REST_TIME(ac);
  }
  FX_HDR(block, PUPPET_FX_OFF_OUT_WRITE) = w + 1; // entry first, then the seq
}

static FxTrack *fx_track(u32 pid)
{
  for (int i = 0; i < FX_TRACK_MAX; i++)
  {
    if (l_fxTrack[i].state != FX_TRACK_FREE && l_fxTrack[i].pid == pid)
      return &l_fxTrack[i];
  }
  return NULL;
}

static FxTrack *fx_trackAdd(u32 pid, u8 state)
{
  for (int i = 0; i < FX_TRACK_MAX; i++)
  {
    if (l_fxTrack[i].state == FX_TRACK_FREE)
    {
      l_fxTrack[i].pid = pid;
      l_fxTrack[i].state = state;
      l_fxTrack[i].room = -1;
      return &l_fxTrack[i];
    }
  }
  return NULL; // full: this one isn't shared
}

// A lit hand bomb of ours: STATE_1..3 (not a Tingle bomb, a bomb nut or a cannonball), not a copy.
static int fx_isHandBomb(fopAc_ac_c *a)
{
  u32 prm = BASE_PARAMETERS(a);
  u32 state = prm & DABOMB_PRM_STATE_MASK;
  return (prm & FX_BOMB_PRM_COPY) == 0 && state >= DABOMB_STATE_LIT && state <= DABOMB_STATE_NEW &&
         DABOMB_REST_TIME(a) > 0;
}

// fopAcIt_Judge callback: the boat's new cannonball (exact params: never a copy or an enemy cannon's).
static void *fx_judgeCannon(void *proc, void *data)
{
  (void)data;
  if (!ws_isValidPtr(proc) || BASE_PROC_NAME(proc) != FPC_NAME_BOMB || BASE_PARAMETERS(proc) != FX_SHIP_CANNON_PRM ||
      BASE_INIT_STATE(proc) == BASE_INIT_STATE_DELETING || DABOMB_REST_TIME(proc) == 0 ||
      fx_track(BASE_PROC_ID(proc)) != NULL)
    return NULL;
  return proc;
}

static void fx_trackLocal(u8 *block, fopAc_ac_c *link)
{
  u32 grabId = DAPY_LK_GRAB_ID(link);
  void *ship = GAMEINFO_SHIP_ACTOR(&g_dComIfG_gameInfo);

  if (grabId != 0 && grabId != FPC_PROC_ID_ERROR && fx_track(grabId) == NULL)
  {
    fopAc_ac_c *grab = fx_actor(grabId);
    if (grab && fx_isHandBomb(grab))
      fx_trackAdd(grabId, FX_TRACK_CARRIED);
  }

  // The nocked arrow is no longer in the bow: shot (param 1), or put away / swapped (deleted, or param 0).
  u32 equipId = DAPY_LK_EQUIP_ID(link);
  fopAc_ac_c *nocked = fx_find(equipId, FPC_NAME_ARROW);
  if (l_fxArrowId != 0 && (nocked == NULL || equipId != l_fxArrowId))
  {
    fopAc_ac_c *shot = fx_find(l_fxArrowId, FPC_NAME_ARROW);
    if (shot && BASE_PARAMETERS(shot) == DAARROW_SHOT_PARAM && DAARROW_TYPE(shot) <= PUPPET_FX_ARROW_TYPE_MAX)
      fx_emit(block, PUPPET_FX_KIND_ARROW, l_fxArrowId, shot, NULL, DAARROW_TYPE(shot), 0);
  }
  l_fxArrowId = nocked ? equipId : 0;

  // The ship sets SHOOT_CANNON the frame it fires and clears it at its next execute (d_a_ship.cpp:4030, 3594).
  if (ws_isValidPtr(ship) && (DASHIP_STATE_FLAG(ship) & DASHIP_SFLG_SHOOT_CANNON) != 0)
  {
    fopAc_ac_c *ball = (fopAc_ac_c *)fopAcIt_Judge((undefined *)fx_judgeCannon, NULL);
    if (ball && fx_trackAdd(BASE_PROC_ID(ball), FX_TRACK_FLYING))
      fx_emit(block, PUPPET_FX_KIND_CANNON, BASE_PROC_ID(ball), ball, NULL, 0, 0);
  }

  for (int i = 0; i < FX_TRACK_MAX; i++)
  {
    FxTrack *t = &l_fxTrack[i];
    if (t->state == FX_TRACK_FREE)
      continue;
    fopAc_ac_c *a = fx_actor(t->pid);
    if (a == NULL)
    {
      // Gone without exploding (sank, fell out of the world, the stage ended). A carried one had no copy.
      if (t->state == FX_TRACK_FLYING)
        fx_emit(block, PUPPET_FX_KIND_REMOVE, t->pid, NULL, FOPAC_CURRENT_POS(link), 0, t->room);
      t->state = FX_TRACK_FREE;
      continue;
    }
    t->room = FOPAC_CURRENT_ROOMNO(a);
    if (DABOMB_REST_TIME(a) == 0)
    {
      fx_emit(block, PUPPET_FX_KIND_EXPLODE, t->pid, a, NULL, 0, 0);
      t->state = FX_TRACK_FREE;
      continue;
    }
    int carried = grabId == t->pid;
    if (t->state == FX_TRACK_CARRIED && !carried)
    {
      fx_emit(block, PUPPET_FX_KIND_BOMB_THROW, t->pid, a, NULL, 0, 0);
      t->state = FX_TRACK_FLYING;
    }
    else if (t->state == FX_TRACK_FLYING && carried)
    {
      fx_emit(block, PUPPET_FX_KIND_BOMB_PICKUP, t->pid, a, NULL, 0, 0);
      t->state = FX_TRACK_CARRIED;
    }
  }
}

// ============================================================================
// VIEWER: inbox -> copies
// ============================================================================

static FxCopy *fx_copy(u8 slot, u32 id)
{
  for (int i = 0; i < FX_COPY_MAX; i++)
  {
    FxCopy *c = &l_fxCopy[i];
    if (c->state != FX_COPY_FREE && c->slot == slot && c->id == id)
      return c;
  }
  return NULL;
}

// A free entry, else the oldest finished one; NULL while every entry is a copy still in the air.
static FxCopy *fx_copyAlloc(u8 slot, u32 id)
{
  FxCopy *best = NULL;
  for (int i = 0; i < FX_COPY_MAX; i++)
  {
    FxCopy *c = &l_fxCopy[i];
    if (c->state == FX_COPY_FREE)
    {
      best = c;
      break;
    }
    if ((c->state == FX_COPY_EXPLODED || c->state == FX_COPY_GONE) && (best == NULL || c->age > best->age))
      best = c;
  }
  if (best)
  {
    best->slot = slot;
    best->id = id;
    best->pid = 0;
    best->age = 0;
    best->hold = 0;
    best->cannon = 0;
    best->local = 0;
    best->state = FX_COPY_GONE;
  }
  return best;
}

static fopAc_ac_c *fx_spawn(u8 *block, u32 prm, cXyz *pos, csXyz *angle)
{
  fopAc_ac_c *a = fopAcM_fastCreate(FPC_NAME_BOMB, prm | FX_BOMB_PRM_COPY, pos, -1, angle, NULL, (byte)-1, 0, NULL);
  if (!ws_isValidPtr(a))
    return NULL;
  DABOMB_OWNED_BY_LINK(a) = 0;
  DABOMB_AT_SPRM(a) &= ~CCD_AT_SPRM_VS_PLAYER;
  FX_HDR(block, PUPPET_FX_OFF_SPAWNED)++;
  return a;
}

// Put a copy where the sender's projectile is, moving as it moves.
static void fx_place(fopAc_ac_c *a, const FxEvent *e)
{
  *FOPAC_CURRENT_POS(a) = e->pos;
  FOPAC_OLD(a)->pos = e->pos;
  FOPAC_SPEED_F(a) = e->speedF;
  FOPAC_SPEED(a)->y = e->speedY;
  FOPAC_GRAVITY(a) = e->gravity;
  *FOPAC_CURRENT_ANGLE(a) = e->angle;
  FOPAC_SHAPE_ANGLE(a)->y = e->angle.y;
}

// A real arrow in flight from the sender's launch point (see the top of this file).
static int fx_spawnArrow(u8 *block, const FxEvent *e, u8 type)
{
  u8 keepType = daArrow_c__m_keep_type;
  u8 magic = GAMEINFO_MAGIC(&g_dComIfG_gameInfo);
  cXyz pos = e->pos;
  csXyz aim = e->angle;
  daArrow_c__m_keep_type = type;
  if (magic < 2)
    GAMEINFO_MAGIC(&g_dComIfG_gameInfo) = 2; // use_mp: light 2, fire / ice 1 (d_a_arrow.cpp:1026-1031)
  fopAc_ac_c *a = fopAcM_fastCreate(FPC_NAME_ARROW, 0, &pos, -1, &aim, NULL, (byte)-1, 0, NULL);
  GAMEINFO_MAGIC(&g_dComIfG_gameInfo) = magic;
  daArrow_c__m_keep_type = keepType;
  if (!ws_isValidPtr(a) || DAARROW_MODEL(a) == NULL)
    return 0;

  *FOPAC_CURRENT_POS(a) = pos;
  FOPAC_OLD(a)->pos = pos;
  FOPAC_CURRENT_ANGLE(a)->x = e->angle.x; // up is +x here, as setKeepMatrix sets it (d_a_arrow.cpp:552)
  FOPAC_CURRENT_ANGLE(a)->y = e->angle.y;
  FOPAC_CURRENT_ANGLE(a)->z = 0;
  FOPAC_SHAPE_ANGLE(a)->x = -e->angle.x;
  FOPAC_SHAPE_ANGLE(a)->y = e->angle.y;
  FOPAC_SHAPE_ANGLE(a)->z = 0;
  DAARROW_CO_SPRM(a) &= ~CCD_CO_SPRM_SET;
  u32 *proc = DAARROW_PROC_FUNC(a); // = &daArrow_c::procMove, the PTMF procWait copies (0x803898B4)
  proc[0] = 0;
  proc[1] = 0xFFFFFFFF;
  proc[2] = (u32)daArrow_c__procMove;
  // Its AT capsule's only registration this frame: the arrow itself first executes next frame.
  daArrow_c__arrowShooting((daArrow_c *)a);
  // Its model is still in the local Link's hand until its first procMove: draw it at the launch point.
  MTX34 *m = (MTX34 *)((u8 *)DAARROW_MODEL(a) + 0x24); // J3DModel::mBaseTransformMtx
  PSMTXTrans(pos.x, pos.y, pos.z, m);
  mDoMtx_ZXYrotM((undefined4)(u32)m, -e->angle.x, e->angle.y, 0);
  FX_HDR(block, PUPPET_FX_OFF_SPAWNED)++;
  return 1;
}

static int fx_ok(f32 v, f32 limit)
{
  return v > -limit && v < limit; // false for NaN / infinity
}

static s16 fx_clamp(s16 v, s16 lo, s16 hi)
{
  return v < lo ? lo : (v > hi ? hi : v);
}

static void fx_apply(u8 *block, u8 *ev)
{
  FxEvent e;
  e.kind = *(volatile u8 *)(ev + PUPPET_FX_EV_OFF_KIND);
  e.slot = *(volatile u8 *)(ev + PUPPET_FX_EV_OFF_SLOT);
  e.variant = *(volatile u8 *)(ev + PUPPET_FX_EV_OFF_VARIANT);
  e.id = FX_HDR(ev, PUPPET_FX_EV_OFF_ID);
  e.pos.x = *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_POSX);
  e.pos.y = *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_POSY);
  e.pos.z = *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_POSZ);
  e.speedF = *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_SPEED_F);
  e.speedY = *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_SPEED_Y);
  e.gravity = *(volatile f32 *)(ev + PUPPET_FX_EV_OFF_GRAVITY);
  e.angle.x = *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_ANGLE_X);
  e.angle.y = *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_ANGLE_Y);
  e.angle.z = *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_ANGLE_Z);
  e.timer = *(volatile s16 *)(ev + PUPPET_FX_EV_OFF_TIMER);

  // C# validated it (PlayerEvent.IsValid) and that it happened in our room; check the values again. The
  // sender may have walked on since (their puppet hidden here): the event's own room is what counts.
  if (e.kind < PUPPET_FX_KIND_BOMB_THROW || e.kind > PUPPET_FX_KIND_MAX || e.slot >= PUPPET_MAX_SLOTS ||
      e.variant > (e.kind == PUPPET_FX_KIND_ARROW ? PUPPET_FX_ARROW_TYPE_MAX : 0) ||
      !fx_ok(e.pos.x, FX_MAX_COORD) || !fx_ok(e.pos.y, FX_MAX_COORD) || !fx_ok(e.pos.z, FX_MAX_COORD) ||
      !fx_ok(e.speedF, FX_MAX_SPEED) || !fx_ok(e.speedY, FX_MAX_SPEED) || !fx_ok(e.gravity, FX_MAX_GRAVITY))
  {
    FX_HDR(block, PUPPET_FX_OFF_IN_REJECTED)++;
    return;
  }

  FxCopy *c = fx_copy(e.slot, e.id);
  fopAc_ac_c *a = c ? fx_actor(c->pid) : NULL;
  switch (e.kind)
  {
  case PUPPET_FX_KIND_BOMB_THROW:
    if (c && (c->state == FX_COPY_EXPLODED || c->state == FX_COPY_LEAVING || (a && c->local)))
      return; // late, or this world's bomb now
    if (a == NULL)
    {
      c = c ? c : fx_copyAlloc(e.slot, e.id);
      a = c ? fx_spawn(block, DABOMB_PRM_VERSION | DABOMB_STATE_LIT, &e.pos, &e.angle) : NULL;
      if (a == NULL)
      {
        if (c)
          c->state = FX_COPY_FREE;
        FX_HDR(block, PUPPET_FX_OFF_IN_REJECTED)++;
        return;
      }
      c->pid = BASE_PROC_ID(a);
      c->cannon = 0;
      c->local = 0;
    }
    fx_place(a, &e);
    DABOMB_REST_TIME(a) = fx_clamp(e.timer, FX_MIN_FUSE, FX_MAX_FUSE);
    c->hold = DABOMB_REST_TIME(a) + FX_EXPLODE_GRACE;
    c->state = FX_COPY_LIVE;
    c->age = 0;
    return;

  case PUPPET_FX_KIND_CANNON:
    if (c)
      return; // duplicate
    c = fx_copyAlloc(e.slot, e.id);
    a = c ? fx_spawn(block, FX_SHIP_CANNON_PRM, &e.pos, &e.angle) : NULL;
    if (a == NULL)
    {
      if (c)
        c->state = FX_COPY_FREE;
      FX_HDR(block, PUPPET_FX_OFF_IN_REJECTED)++;
      return;
    }
    fx_place(a, &e); // d_a_ship.cpp:4026-4033: no-gravity frames, speeds, gravity
    DABOMB_NO_GRAVITY_TIME(a) = fx_clamp(e.timer, 0, FX_MAX_NO_GRAVITY);
    c->pid = BASE_PROC_ID(a);
    c->cannon = 1;
    c->state = FX_COPY_LIVE;
    c->age = 0;
    return;

  case PUPPET_FX_KIND_BOMB_PICKUP:
    // The puppet carries it again (GRAB_KIND draws the bomb in its hands).
    if (c && a && c->state == FX_COPY_LIVE && !c->local && !fopAcM_checkCarryNow(a) && DABOMB_REST_TIME(a) != 0)
    {
      fopAcM_delete((base_process_class *)a);
      c->state = FX_COPY_FREE;
    }
    return;

  case PUPPET_FX_KIND_EXPLODE:
    if (c && c->state == FX_COPY_EXPLODED)
      return;
    if (a && c->local)
      return; // this world's bomb now: its own fuse (or whoever took it) decides
    if (a && DABOMB_REST_TIME(a) != 0)
    {
      *FOPAC_CURRENT_POS(a) = e.pos;
      FOPAC_OLD(a)->pos = e.pos;
      daBomb_c__procExplode_init((daBomb_c *)a);
    }
    else if (a == NULL)
    {
      // Nothing of ours to explode (it blew up in the sender's hands, or ours sank): an explosion there.
      c = c ? c : fx_copyAlloc(e.slot, e.id);
      a = fx_spawn(block, DABOMB_PRM_VERSION | DABOMB_STATE_EXPLODE, &e.pos, NULL);
      if (c)
        c->pid = a ? BASE_PROC_ID(a) : 0;
    }
    if (c)
    {
      c->state = FX_COPY_EXPLODED;
      c->age = 0;
    }
    return;

  case PUPPET_FX_KIND_REMOVE:
    if (c && c->state == FX_COPY_LIVE && !c->local) // (this world's bomb now: it isn't the sender's to remove)
    {
      c->state = FX_COPY_LEAVING;
      c->age = 0;
    }
    return;

  case PUPPET_FX_KIND_ARROW:
    if (c)
      return; // duplicate
    c = fx_copyAlloc(e.slot, e.id);
    if (c == NULL || !fx_spawnArrow(block, &e, e.variant))
    {
      if (c)
        c->state = FX_COPY_FREE;
      FX_HDR(block, PUPPET_FX_OFF_IN_REJECTED)++;
      return;
    }
    c->state = FX_COPY_EXPLODED; // fire and forget: kept only so a repeat does nothing
    c->age = 0;
    return;
  }
}

static void fx_updateCopies(void)
{
  for (int i = 0; i < FX_COPY_MAX; i++)
  {
    FxCopy *c = &l_fxCopy[i];
    if (c->state == FX_COPY_FREE)
      continue;
    if (c->age < 0xFFFF)
      c->age++;
    if (c->state == FX_COPY_EXPLODED || c->state == FX_COPY_GONE)
    {
      if (c->age > FX_DONE_FRAMES)
        c->state = FX_COPY_FREE;
      continue;
    }
    fopAc_ac_c *a = fx_actor(c->pid);
    if (a == NULL || DABOMB_REST_TIME(a) == 0)
    {
      c->state = a == NULL ? FX_COPY_GONE : FX_COPY_EXPLODED; // sank here / exploded here (something hit it)
      c->age = 0;
      continue;
    }
    if (c->state == FX_COPY_LEAVING && c->age > FX_LEAVE_FRAMES && !fopAcM_checkCarryNow(a))
    {
      fopAcM_delete((base_process_class *)a);
      c->state = FX_COPY_GONE;
      c->age = 0;
      continue;
    }
    if (c->cannon || c->local)
      continue;
    // Taken over here: carried, swallowed / caught (an enemy changes its state), or its fuse set lower
    // than our hold leaves it (setBombRestTime(1)): no more holding, no snapping.
    s16 rest = DABOMB_REST_TIME(a);
    if (fopAcM_checkCarryNow(a) || (BASE_PARAMETERS(a) & DABOMB_PRM_STATE_MASK) != DABOMB_STATE_LIT ||
        rest < FX_HOLD_FUSE - 1)
    {
      c->local = 1;
      continue;
    }
    // Wait for the sender's explosion (runs before the bomb's execute: puppets are list 3, bombs 7).
    if ((s32)c->age < c->hold && rest < FX_HOLD_FUSE)
      DABOMB_REST_TIME(a) = FX_HOLD_FUSE;
  }
}

void puppet_fx_tick(void)
{
  u32 frame = SCOMPONENT_FRAME_COUNTER();
  if (l_fxHasRun && frame == l_fxLastFrame)
    return; // once per game frame, however many puppets call us
  l_fxHasRun = 1;
  l_fxLastFrame = frame;

  u8 *block = fx_block();
  fopAc_ac_c *link = (fopAc_ac_c *)GAMEINFO_LINK_ACTOR(&g_dComIfG_gameInfo);
  if (!block)
    return;
  if (!ws_isValidPtr(link) || GAMEINFO_NEXT_STAGE_ENABLE(&g_dComIfG_gameInfo) != 0)
  {
    // No spawning or reporting across a stage change; what arrived meanwhile belongs to the old stage.
    FX_HDR(block, PUPPET_FX_OFF_IN_READ) = FX_HDR(block, PUPPET_FX_OFF_IN_WRITE);
    return;
  }

  fx_updateCopies();

  u32 r = FX_HDR(block, PUPPET_FX_OFF_IN_READ);
  u32 w = FX_HDR(block, PUPPET_FX_OFF_IN_WRITE);
  if (w - r > PUPPET_FX_IN_COUNT)
    r = w - PUPPET_FX_IN_COUNT; // (C# never overruns the ring; if it did, the oldest are lost)
  for (int n = 0; r != w && n < FX_EVENTS_PER_FRAME; n++)
  {
    r++;
    fx_apply(block, PUPPET_FX_EVENT(block, PUPPET_FX_OFF_IN_RING, PUPPET_FX_IN_COUNT, r));
  }
  FX_HDR(block, PUPPET_FX_OFF_IN_READ) = r;

  if (FX_HDR(block, PUPPET_FX_OFF_FLAGS) & PUPPET_FX_FLAG_SEND)
    fx_trackLocal(block, link);
  else
  {
    for (int i = 0; i < FX_TRACK_MAX; i++)
      l_fxTrack[i].state = FX_TRACK_FREE; // the room rule is off: nothing is reported
  }
}
