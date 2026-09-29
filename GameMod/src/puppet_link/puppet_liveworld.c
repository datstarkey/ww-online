/**
 * puppet_liveworld.c - live world: create-only actors follow bits other players set (see the header).
 * Included by puppet.c after puppet_nametag.c (puppet_bootBlock) and puppet_worldsync.c (ws_isValidPtr).
 */

#include "puppet_liveworld.h"

/* Size: -Og shrink-wraps every early return into its own copy of the epilogue. This file is all
 * early-outs and REL space is scarce (GameMod/CLAUDE.md rule 6), so not here. */
#pragma GCC push_options
#pragma GCC optimize("no-shrink-wrap")

#define LW_MAX_HITS 16 /* actors handled per batch; more (never seen in one room) are left alone */
#define LW_MAX_WAIT 16 /* passes (every 4th frame: ~2 s) an actor still being created may hold a batch back */
#define LW_ROOM_HIDDEN 0x08 /* dStage_roomStatus_c::mFlags: room hidden (d_door.cpp:305) */
#define LW_CHEST 0x1F  /* LwRule.mask of a chest bit (BITS word 0); 0xFF = a switch number */
/* LwRule.shift flags (the shift itself is shift & LW_SHIFT) */
#define LW_SHIFT 0x1F
#define LW_ANGLE_X 0x80  /* the key is in home.angle.x, not the params */
#define LW_MEM_ONLY 0x40 /* memory switches only (a door's setKey reads its switch with room -1) */
/* LwRule.near: a door is re-created only while the local Link is further than 250 (XZ) from it, its own reach
 * (checkArea's distXZSqMax, d_a_door10.cpp:375): between the delete and the new door's create (it may reload
 * the "Key" archive) there is no door collision, so never with Link at the door. A warp jar: not while Link is
 * on it (lid radius 70, body 85): he would drop into the jar and warp. */
#define LW_NEAR_DOOR 25 /* x10 units */
#define LW_NEAR_JAR 12

/* Which actors read which flag only at create, and what brings them up to date. Every key is
 * (prm >> shift) & mask (or home.angle.x's), verified in the actor's REL (docs/live-world.md). */
typedef struct
{
  s16 proc;
  u8 shift;    /* | LW_ANGLE_X, LW_MEM_ONLY */
  u8 mask;
  u16 lockOff; /* != 0: skip it unless this byte is set (a door's dDoor_key2_c::mbEnabled, a warp jar's lid) */
  u16 actOff;  /* != 0: its state byte; not `idle` = not now (a door not in Wait, a jar not closed, a carried rock) */
  u8 idle;
  u8 near;     /* != 0: not while the local Link is within near * 10 units (XZ) */
} LwRule;

static const LwRule l_lwRules[] = {
    /* opened: create sees isTbox and comes up open and empty (funcs 7/8 read STAGE_SEA2's bits: skipped) */
    {PROC_NAME_TBOX, 7, LW_CHEST, 0, 0, 0, 0},
    /* create returns ERROR (MkieK: STOP) once the switch is set: they are simply gone */
    {PROC_NAME_WALL, 0, 0xFF, 0, 0, 0, 0},
    {PROC_NAME_FLOOR, 0, 0xFF, 0, 0, 0, 0},
    {PROC_NAME_OBJ_ICE, 0, 0xFF, 0, 0, 0, 0},
    {PROC_NAME_MJDOOR, 0, 0xFF, 0, 0, 0, 0},
    {PROC_NAME_MKIEK, 0, 0xFF, 0, 0, 0, 0},
    /* black boulder (on DRC's / ET's warp jars: the jar under it polls the switch and opens itself), only lying
     * still (mode wait): a carried or thrown one breaks itself (d_a_stone2.cpp:495-590, 729). Its create zeroes
     * home.angle.z (prmZ_init), but with the switch set it returns ERROR before that matters */
    {PROC_NAME_STONE2, 8, 0xFF, 0, STONE2_MODE_OFF, STONE2_MODE_WAIT, 0},
    /* wooden barricade: lower half, upper half broken */
    {PROC_NAME_SAKU, 8, 0xFF, 0, 0, 0, 0},
    {PROC_NAME_SAKU, 16, 0xFF, 0, 0, 0, 0},
    /* crystal switch shows on (a timed one starts its own timer, as the peer's did) */
    {PROC_NAME_SWHIT0, 0, 0xFF, 0, 0, 0, 0},
    /* small-key / boss doors: re-created, the new one's actionInit setKey sees the switch and gives keyOff, as
     * on a reload (docs/live-world.md 0.1). Never write the lock byte: that froze the game at the door. */
    {PROC_NAME_DOOR10, LW_MEM_ONLY, 0xFF, DOOR10_KEYLOCK_OFF, DOOR10_ACTION_OFF, DOOR_ACTION_WAIT, LW_NEAR_DOOR},
    {PROC_NAME_DOOR12, LW_MEM_ONLY, 0xFF, DOOR12_KEYLOCK_OFF, DOOR12_ACTION_OFF, DOOR_ACTION_WAIT, LW_NEAR_DOOR},
    /* a normal warp jar with its lid on: its create sees the lid switch and builds it open (no lid, no break
     * effect, no event), as on a reload. A lidded jar never polls it (modeClose, d_a_obj_warpt.cpp:455-474). */
    {PROC_NAME_OBJ_WARPT, LW_ANGLE_X, 0xFF, WARPT_LID_OFF, WARPT_MODE_OFF, WARPT_MODE_CLOSE, LW_NEAR_JAR},
};
#define LW_RULE_END (l_lwRules + sizeof(l_lwRules) / sizeof(l_lwRules[0]))

typedef struct
{
  u32 work[LIVEWORLD_BIT_WORDS]; /* the batch of new bits */
  u32 zoneRoom;                  /* the room the zone bits (0xC0+) belong to */
  u32 count;                     /* hits[] used */
  u32 pending;                   /* matching actors not ready yet (being created, room loading, door busy) */
  fopAc_ac_c *hits[LW_MAX_HITS];
} LwPass;

/* True while the local Link is within near * 10 units (XZ) of the actor, or there is no player. */
static int lw_nearPlayer(fopAc_ac_c *ac, u32 near)
{
  fopAc_ac_c *pl = (fopAc_ac_c *)dComIfGp_getPlayer(0);
  if (!ws_isValidPtr(pl))
    return 1;
  cXyz *a = FOPAC_CURRENT_POS(ac);
  cXyz *b = FOPAC_CURRENT_POS(pl);
  f32 dx = a->x - b->x;
  f32 dz = a->z - b->z;
  f32 d = (f32)(near * 10);
  return dx * dx + dz * dz < d * d;
}

/* fopAcIt_Judge callback: collect the actors whose create-time flag is in the batch. Never deletes here. */
static void *lw_judge(void *proc, void *data)
{
  LwPass *p = (LwPass *)data;
  fopAc_ac_c *ac = (fopAc_ac_c *)proc;
  if (ws_isValidPtr(ac) && BASE_INIT_STATE(ac) != BASE_INIT_STATE_DELETING)
  {
    u32 prm = BASE_PARAMETERS(ac);
    s32 room = FOPAC_HOME_ROOMNO(ac);
    for (const LwRule *r = l_lwRules; r < LW_RULE_END; r++)
    {
      if (r->proc != BASE_PROC_NAME(ac))
        continue;
      u32 src = (r->shift & LW_ANGLE_X) != 0 ? (u16)FOPAC_HOME_ANGLE(ac)->x : prm;
      u32 key = (src >> (r->shift & LW_SHIFT)) & r->mask;
      u32 word = 0;
      if (r->mask == LW_CHEST)
      {
        if (TBOX_FUNC(prm) >= TBOX_FUNC_EXTRA_SAVE)
          continue;
      }
      else
      {
        word = 1 + (key >> 5);
        /* 0xFF = none and 0xF0-0xFE are no switches; zone bits are one room's */
        if (key >= 0xF0 || ((r->shift & LW_MEM_ONLY) != 0 && key >= 0x80) ||
            (key >= 0xC0 && (u32)room != p->zoneRoom))
          continue;
      }
      /* No need to read the live flag here: C# publishes only bits it has read back from the live save
       * data, and a re-created actor's create reads the flag itself. */
      if (((p->work[word] >> (key & 31)) & 1) == 0)
        continue;

      u8 *a = (u8 *)ac;
      if (r->lockOff != 0 && (FOPAC_ACTOR_CONDITION(ac) & FOPAC_CND_INIT) != 0 &&
          (a[r->lockOff] == 0 || a[r->actOff] == 0 ||
           (r->proc == PROC_NAME_DOOR12 && ((FOPAC_HOME_ANGLE(ac)->z >> 8) & 0xFF) == DOOR12_ARG1_TMPBIT) ||
           (r->proc == PROC_NAME_OBJ_WARPT && WARPT_TYPE(prm) - WARPT_TYPE_SP_FIRST <= 2)))
        break; /* no lock / lid showing; or a door in Init (action 0: the player isn't next to it), whose actionInit
                * re-runs setKey itself (execute, d_a_door10.cpp:794-831), or a jar that is open (mode 0); or a
                * door12 whose create clears a temp event bit; or a three-way dungeon jar (its lid is an event
                * register; angle.x is the switch of the boulder / wall on it, which it polls itself: modeClose,
                * d_a_obj_warpt.cpp:471): nothing to do / not ours to do */
      if (BASE_CREATE_RESULT(ac) != BASE_CREATE_DONE || (FOPAC_ACTOR_CONDITION(ac) & FOPAC_CND_INIT) == 0 ||
          p->count >= LW_MAX_HITS ||
          ((u32)room < ROOM_MAX &&
           (ROOM_STATUS_FLAGS(room) & (ROOM_FLAG_LOADED | ROOM_FLAG_BUSY | LW_ROOM_HIDDEN)) != ROOM_FLAG_LOADED) ||
          (r->actOff != 0 && a[r->actOff] != r->idle) || (r->near != 0 && lw_nearPlayer(ac, r->near)))
      {
        /* Its create may have read the flag before the bit landed; its room scene is loading, going away or
         * hidden (never create into that); a door not in Wait, a jar whose lid is burning or a boulder in
         * Link's hands or in the air; the local Link at the door / on the jar; or hits[] is full: not now. The
         * batch waits, then is acknowledged with RETRY set. */
        p->pending++;
      }
      else
      {
        p->hits[p->count++] = ac;
      }
      break; /* one action per actor */
    }
  }
  return NULL;
}

typedef int (*LwDeleteFn)(fopAc_ac_c *ac); /* fopAcM_delete(fopAc_ac_c*) returns fpcDt_Delete's BOOL */

/* Delete the actor, then create it again from its own create params, in its own layer: the room scene's
 * (an actor placed in a room lives in that room's layer). The current layer is not (f_pc_base.cpp:47), and an
 * actor created there would outlive the room and duplicate on reload. Only if the delete was accepted
 * (fpcDt_Delete refuses one being created or already deleting): two chests would give the item twice. The
 * layer is read BEFORE the delete: an accepted delete moves the actor to the delete queue, and
 * fpcLyTg_QueueTo clears mLyTg.mpLayer (+0x2C) on the way (f_pc_layer_tag.cpp:38, DOL 0x8003E044), so
 * afterwards it is NULL and a create there writes through NULL. The deleted actor's other fields stay
 * readable until next frame's deletor. False if nothing happened. */
static int lw_recreate(fopAc_ac_c *ac)
{
  LwDeleteFn del = (LwDeleteFn)(u32)fopAcM_delete;
  layer_class *layer = BASE_LAYER(ac);
  if (layer == NULL || !del(ac))
    return 0;
  layer_class *saved = f_pc_layer__fpcLy_CurrentLayer();
  f_pc_layer__fpcLy_SetCurrentLayer(layer);
  fpc_ProcID id = fopAcM_create(BASE_PROC_NAME(ac), BASE_PARAMETERS(ac), FOPAC_HOME_POS(ac), FOPAC_HOME_ROOMNO(ac),
                                FOPAC_HOME_ANGLE(ac), FOPAC_SCALE(ac), (byte)FOPAC_ARGUMENT(ac), 0);
  f_pc_layer__fpcLy_SetCurrentLayer(saved);
  return id != 0xFFFFFFFF; /* fpcM_ERROR_PROCESS_ID_e: gone until the room reloads (it then reads the flag) */
}

static u32 l_lwLastFrame;

/* From the puppet's draw: after every actor's execute this frame, so every event order the player or an
 * actor placed this frame is in mOrderCount (the player orders a chest's TREASURE event with the chest's
 * pointer in its execute, after ours). fopAc_Draw skips draws while the menu is open or the actor is
 * stopped by an event. Once per frame, every 4th frame, like the despawn.
 * One batch: an even SEQ != DONE_SEQ, read whole, for this stage, while no stage change is in flight (the
 * stag pointer is stale then) and no event runs or is queued (an event order holds actor pointers).
 * Another stage's batch waits: C# publishes one for this stage. */
void puppet_liveworld_draw(void)
{
  u32 frame = SCOMPONENT_FRAME_COUNTER();
  if (frame == l_lwLastFrame || (frame & 3) != 0)
    return;
  l_lwLastFrame = frame;
  u8 *gi = (u8 *)&g_dComIfG_gameInfo;
  u8 *stag = GAMEINFO_STAGE_STAGINFO(gi);
  volatile u32 *w = (volatile u32 *)puppet_bootBlock(LIVEWORLD_PTR_ADDR, LIVEWORLD_MAGIC, LIVEWORLD_BLOCK_SIZE);
  if (w == NULL || GAMEINFO_NEXT_STAGE_ENABLE(gi) != 0 || !ws_isValidPtr(stag) || GAMEINFO_EVT_MODE(gi) != 0 ||
      GAMEINFO_EVT_ORDER_COUNT(gi) != 0)
    return;
  u32 seq = w[LIVEWORLD_OFF_SEQ / 4];
  if ((seq & 1) != 0 || seq == w[LIVEWORLD_OFF_DONE_SEQ / 4])
    return;
  LwPass p;
  u32 tag = w[LIVEWORLD_OFF_TAG / 4];
  p.zoneRoom = w[LIVEWORLD_OFF_ZONE_ROOM / 4];
  for (u32 i = 0; i < LIVEWORLD_BIT_WORDS; i++)
    p.work[i] = w[LIVEWORLD_OFF_BITS / 4 + i];
  if (w[LIVEWORLD_OFF_SEQ / 4] != seq || tag != (LIVEWORLD_TAG_MAGIC | STAGINFO_SAVE_TBL(stag)))
    return; /* rewritten while we read (next time), or not this stage's */

  p.count = 0;
  p.pending = 0;
  fopAcIt_Judge((undefined *)lw_judge, &p);
  if (p.pending != 0 && w[LIVEWORLD_OFF_WAIT / 4] < LW_MAX_WAIT)
  {
    w[LIVEWORLD_OFF_WAIT / 4]++;
    return; /* act on all of them together once they are ready */
  }
  for (u32 i = 0; i < p.count; i++)
  {
    fopAc_ac_c *ac = p.hits[i];
    /* One line per actor in the Dolphin log (OSReport): which actor, and whether it was re-created. Its fields
     * are read before the delete. Room -1 = placed in Stage.arc (created with room -1 at stage load too). */
    int proc = (u16)BASE_PROC_NAME(ac);
    u32 prm = BASE_PARAMETERS(ac);
    int room = FOPAC_HOME_ROOMNO(ac);
    int done = lw_recreate(ac);
    OSReport("[PUPPET] live world: %s proc 0x%03x prm 0x%08x room %d\n", done ? "re-created" : "NOT re-created", proc,
             prm, room);
    if (!done)
    {
      p.pending++;
      continue;
    }
    w[LIVEWORLD_OFF_POKE_COUNT / 4]++;
  }
  /* Anything left undone: C# publishes the batch again (a done actor is just re-created once more). */
  w[LIVEWORLD_OFF_RETRY / 4] = p.pending;
  w[LIVEWORLD_OFF_WAIT / 4] = 0;
  w[LIVEWORLD_OFF_DONE_SEQ / 4] = seq;
}

#pragma GCC pop_options
