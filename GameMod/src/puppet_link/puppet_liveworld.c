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

/* Which actors read which flag only at create, and what brings them up to date. Every param getter
 * is (prm >> shift) & mask, verified in the actor's REL (docs/live-world.md). */
typedef struct
{
  s16 proc;
  u8 shift;
  u8 mask;
  u16 lockOff; /* != 0: clear this small-key lock byte of a door in Wait (action byte at actOff); 0: re-create */
  u16 actOff;
} LwRule;

static const LwRule l_lwRules[] = {
    /* opened: create sees isTbox and comes up open and empty (funcs 7/8 read STAGE_SEA2's bits: skipped) */
    {PROC_NAME_TBOX, 7, LW_CHEST, 0, 0},
    /* create returns ERROR once the switch is set: they are simply gone */
    {PROC_NAME_WALL, 0, 0xFF, 0, 0},
    {PROC_NAME_FLOOR, 0, 0xFF, 0, 0},
    {PROC_NAME_OBJ_ICE, 0, 0xFF, 0, 0},
    {PROC_NAME_MJDOOR, 0, 0xFF, 0, 0},
    /* wooden barricade: lower half, upper half broken */
    {PROC_NAME_SAKU, 8, 0xFF, 0, 0},
    {PROC_NAME_SAKU, 16, 0xFF, 0, 0},
    /* crystal switch shows on (a timed one starts its own timer, as the peer's did) */
    {PROC_NAME_SWHIT0, 0, 0xFF, 0, 0},
    /* small-key locks: setKey would give keyOff (docs/small-keys.md §3) */
    {PROC_NAME_DOOR10, 0, 0xFF, DOOR10_KEYLOCK_OFF, DOOR10_ACTION_OFF},
    {PROC_NAME_DOOR12, 0, 0xFF, DOOR12_KEYLOCK_OFF, DOOR12_ACTION_OFF},
};
#define LW_RULE_END (l_lwRules + sizeof(l_lwRules) / sizeof(l_lwRules[0]))

typedef struct
{
  u32 work[LIVEWORLD_BIT_WORDS]; /* the batch of new bits */
  u32 zoneRoom;                  /* the room the zone bits (0xC0+) belong to */
  u32 count;                     /* hits[] used */
  u32 pending;                   /* matching actors not ready yet (being created, room loading, door busy) */
  fopAc_ac_c *hits[LW_MAX_HITS];
  const LwRule *rules[LW_MAX_HITS];
} LwPass;

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
      u32 key = (prm >> r->shift) & r->mask;
      u32 word = 0;
      if (r->mask == LW_CHEST)
      {
        if (TBOX_FUNC(prm) >= TBOX_FUNC_EXTRA_SAVE)
          continue;
      }
      else
      {
        word = 1 + (key >> 5);
        /* 0xFF = none and 0xF0-0xFE are no switches; setKey keeps a lock on a dan/zone switch on;
         * zone bits are one room's */
        if (key >= 0xF0 || (r->lockOff != 0 && key >= 0x80) || (key >= 0xC0 && (u32)room != p->zoneRoom))
          continue;
      }
      /* No need to read the live flag here: C# publishes only bits it has read back from the live save
       * data, and a re-created actor's create reads the flag itself. */
      if (((p->work[word] >> (key & 31)) & 1) == 0)
        continue;

      u8 *a = (u8 *)ac;
      if (r->lockOff != 0 && (a[r->lockOff] == 0 || a[r->actOff] == 0))
        break; /* no lock left, or Init (action 0), which re-runs setKey itself */
      if (BASE_CREATE_RESULT(ac) != BASE_CREATE_DONE || (FOPAC_ACTOR_CONDITION(ac) & FOPAC_CND_INIT) == 0 ||
          (r->lockOff != 0 && a[r->actOff] != DOOR_ACTION_WAIT) || p->count >= LW_MAX_HITS ||
          (r->lockOff == 0 && (u32)room < ROOM_MAX &&
           (ROOM_STATUS_FLAGS(room) & (ROOM_FLAG_LOADED | ROOM_FLAG_BUSY | LW_ROOM_HIDDEN)) != ROOM_FLAG_LOADED))
      {
        /* Its create may have read the flag before the bit landed; its room scene is loading, going away or
         * hidden (never create into that); a lock on a door that is closing its bars or opening (not Wait);
         * or hits[] is full: not now. The batch waits, then is acknowledged with RETRY set. */
        p->pending++;
      }
      else
      {
        p->hits[p->count] = ac;
        p->rules[p->count++] = r;
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
 * deleted actor's fields stay readable until next frame's deletor. False if nothing happened. */
static int lw_recreate(fopAc_ac_c *ac)
{
  LwDeleteFn del = (LwDeleteFn)(u32)fopAcM_delete;
  if (!del(ac))
    return 0;
  layer_class *saved = f_pc_layer__fpcLy_CurrentLayer();
  f_pc_layer__fpcLy_SetCurrentLayer(BASE_LAYER(ac));
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
    /* No OSReport per actor (REL space): C# logs each batch's bits and POKE_COUNT. */
    const LwRule *r = p.rules[i];
    if (r->lockOff != 0)
      ((u8 *)p.hits[i])[r->lockOff] = 0; /* dDoor_key2_c::keyOff */
    else if (!lw_recreate(p.hits[i]))
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
