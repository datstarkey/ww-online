/**
 * puppet_worldsync.c - Layer 2 of shared-world sync: live despawn of placed pickups.
 *
 * The C# client (layer 1) OR-merges other players' per-stage save flags into this game,
 * including the LIVE mMemory copy of the current stage, and publishes in scratch memory:
 *   WORLDSYNC_ITEM_MASK_ADDR  u32  mItem bits (0..31) set by OTHER players for the current stage
 *   WORLDSYNC_STAGE_TAG_ADDR  u32  WORLDSYNC_TAG_MAGIC | saveTbl the mask belongs to (else ignored)
 *
 * A placed daItem_c (rupees, heart pieces, keys... with an item bit) checks its bit only
 * in _daItem_create (d_a_item.cpp:226-237), so a bit arriving mid-visit would leave it
 * collectable. Here we fopAcM_delete every daItem whose bit is in the remote mask AND set
 * in the live mMemory.mItem, provided it is idle on the ground (not being collected by the
 * local player, not held by the boomerang/hookshot/another actor).
 *
 * A boss's Heart Container has no item bit: picking it up sets the stage's STAGE_LIFE dungeon bit instead
 * (item_func_utuwa_heart, d_item.cpp:587-603), which the client ORs into the live mDungeonItem like any world
 * flag. ws_heartContainers deletes a container still waiting in this stage once that bit is set, so one
 * another player picked up vanishes here too (docs/hearts.md §2).
 *
 * Chests and other actors that read a flag only at create: puppet_liveworld.c (run from the draw).
 * Switch-polling actors need nothing.
 *
 * Everything is offset-macro based (ww_inlines.h, WORLD SYNC block). REL: literals OK.
 */

#include "puppet_worldsync.h"

#define WS_SCAN_INTERVAL_MASK    3  /* scan every 4th game frame (mask arrives at ~20Hz) */
#define WS_MAX_DESPAWN_PER_SCAN  8
#define WS_TAG_MAGIC_MASK        0xFFFFFF00
#define WS_TAG_SAVETBL_MASK      0x000000FF
#define WS_ITEM_BITS_PER_WORD    32 /* only mItem[0]; bits 32..63 alias mVisitedRoom[0] */

typedef struct
{
  u32 candidates; /* remote mask & live mItem */
  u32 matched;    /* every live daItem with a candidate bit, idle or not */
  u32 count;      /* idle ones collected into hits[] (despawned this scan) */
  fopAc_ac_c *hits[WS_MAX_DESPAWN_PER_SCAN];
} WsScan;

/* Scan result for the C# client (see puppet_shared.h): it keeps a (parked) puppet alive -
 * this tick only runs from daPuppet_Execute - until a fresh scan reports nothing pending. */
static void ws_publish(u32 pending, u32 tag, u32 candidates)
{
  *(volatile u32 *)WORLDSYNC_PENDING_ADDR = pending;
  *(volatile u32 *)WORLDSYNC_SCAN_TAG_ADDR = tag;
  *(volatile u32 *)WORLDSYNC_SCAN_CAND_ADDR = candidates;
  (*(volatile u32 *)WORLDSYNC_SCAN_SEQ_ADDR)++;
}

static u32 l_wsLastFrame;
static u32 l_wsHasRun;

static int ws_isValidPtr(const void *p)
{
  u32 a = (u32)p;
  /* MEM1 incl. Dolphin's extended RAM (bi2 is patched to 48MB). */
  return a >= 0x80000000 && a < 0x84000000 && (a & 3) == 0;
}

/* fopAcIt_Judge callback (cTgIt_JudgeFilter passes each actor in g_fopAcTg_Queue).
 * Return NULL to keep iterating, non-NULL to stop. Never deletes during iteration. */
static void *ws_judge(void *proc, void *data)
{
  WsScan *scan = (WsScan *)data;
  if (!scan || !ws_isValidPtr(proc))
    return NULL;
  if (BASE_PROC_NAME(proc) != PROC_NAME_ITEM)
    return NULL;
  /* Fully created and not already on its way out (fpcDt_Delete refuses those anyway). */
  if (BASE_CREATE_RESULT(proc) != BASE_CREATE_DONE)
    return NULL;
  if (BASE_INIT_STATE(proc) == BASE_INIT_STATE_DELETING)
    return NULL;

  fopAc_ac_c *ac = (fopAc_ac_c *)proc;
  if ((FOPAC_ACTOR_CONDITION(ac) & FOPAC_CND_INIT) == 0)
    return NULL;

  u32 prm = BASE_PARAMETERS(proc);
  if (DAITEM_PRM_ITEM_NO(prm) == DAITEM_ITEMNO_BLUE_JELLY)
    return NULL; /* its "item bit" is a STAGE_BLUE_CHU_JELLY save switch */

  s32 bit = DAITEM_ITEM_BIT_NO(proc);
  if (bit != (s32)DAITEM_PRM_ITEM_BIT(prm))
    return NULL; /* fields disagree with params -> not a normally-created daItem */
  if (bit < 0 || bit >= WS_ITEM_BITS_PER_WORD)
    return NULL; /* 0x7F = no bit; 32..63 alias visited rooms; 0x40+ are zone bits */
  if ((scan->candidates & (1u << bit)) == 0)
    return NULL;

  scan->matched++;
  if (scan->count >= WS_MAX_DESPAWN_PER_SCAN)
    return NULL; /* hits[] full: keep counting, the rest go next scan */

  /* Only idle pickups. 5..9 = the LOCAL player is collecting it (bit already set by
   * itemGetExecute), 2/3 = held by another actor, 0xA/0xB = boss drop. */
  u8 status = DAITEM_ITEM_STATUS(proc);
  if (status != DAITEM_STATUS_IDLE0 && status != DAITEM_STATUS_IDLE1)
    return NULL;
  if (DAITEM_FLAG(proc) & (DAITEM_FLAG_BOOMERANG | DAITEM_FLAG_HOOK))
    return NULL;
  if (FOPAC_ACTOR_STATUS(ac) & FOPAC_STTS_HOOK_CARRY)
    return NULL;

  scan->hits[scan->count++] = ac;
  return NULL;
}

/* fopAcIt_Judge callback: a Heart Container still waiting to be picked up (boss drop wait, or idle on the
 * ground), never one the local player is collecting (7-9) or that another actor holds (2/3). */
static void *ws_judgeHeart(void *proc, void *data)
{
  WsScan *scan = (WsScan *)data;
  if (!scan || !ws_isValidPtr(proc) || scan->count >= WS_MAX_DESPAWN_PER_SCAN)
    return NULL;
  if (BASE_PROC_NAME(proc) != PROC_NAME_ITEM || BASE_CREATE_RESULT(proc) != BASE_CREATE_DONE ||
      BASE_INIT_STATE(proc) == BASE_INIT_STATE_DELETING)
    return NULL;
  if (DAITEM_ITEM_NO(proc) != DAITEM_ITEMNO_HEART_CONTAINER)
    return NULL;
  u8 status = DAITEM_ITEM_STATUS(proc);
  if (status != DAITEM_STATUS_WAIT_BOSS1 && status != DAITEM_STATUS_WAIT_BOSS2 &&
      status != DAITEM_STATUS_IDLE0 && status != DAITEM_STATUS_IDLE1)
    return NULL;
  scan->hits[scan->count++] = (fopAc_ac_c *)proc;
  return NULL;
}

/* Delete this stage's waiting Heart Containers once its STAGE_LIFE bit is set in the live copy: the room has
 * the container (another player picked theirs up), so this one must not be taken again. Independent of the item
 * mask. Counted with the placed-item despawns (item 0x08, bit 0x7F) for the client's [world] log line. */
static void ws_heartContainers(void)
{
  if (GAMEINFO_NEXT_STAGE_ENABLE(&g_dComIfG_gameInfo) != 0)
    return;
  u8 *stag = GAMEINFO_STAGE_STAGINFO(&g_dComIfG_gameInfo);
  if (!ws_isValidPtr(stag))
    return;
  u32 saveTbl = STAGINFO_SAVE_TBL(stag);
  if (saveTbl >= DSV_STAGE_MAX)
    return;
  if ((MEMBIT_DUNGEON_ITEM(GAMEINFO_LIVE_MEMORY(&g_dComIfG_gameInfo)) & MEMBIT_DUNGEON_STAGE_LIFE) == 0)
    return;

  WsScan scan;
  scan.candidates = 0;
  scan.matched = 0;
  scan.count = 0;
  fopAcIt_Judge((undefined *)ws_judgeHeart, &scan);
  for (u32 i = 0; i < scan.count && i < WS_MAX_DESPAWN_PER_SCAN; i++)
  {
    fopAcM_delete((base_process_class *)scan.hits[i]);
    (*(volatile u32 *)WORLDSYNC_DESPAWN_COUNT_ADDR)++;
    *(volatile u32 *)WORLDSYNC_LAST_DESPAWN_ADDR = (saveTbl << 16) | (DAITEM_ITEMNO_HEART_CONTAINER << 8) | 0x7F;
    OSReport("[PUPPET] world sync: Heart Container taken by another player, removed (slot %d)\n", saveTbl);
  }
}

void puppet_worldsync_tick(void)
{
  /* Once per game frame, however many puppets call us. */
  u32 frame = SCOMPONENT_FRAME_COUNTER();
  if (l_wsHasRun && frame == l_wsLastFrame)
    return;
  l_wsHasRun = 1;
  l_wsLastFrame = frame;
  if ((frame & WS_SCAN_INTERVAL_MASK) != 0)
    return;

  ws_heartContainers();

  volatile u32 *tagp = (volatile u32 *)WORLDSYNC_STAGE_TAG_ADDR;
  volatile u32 *maskp = (volatile u32 *)WORLDSYNC_ITEM_MASK_ADDR;

  u32 tag = *tagp;
  if ((tag & WS_TAG_MAGIC_MASK) != WORLDSYNC_TAG_MAGIC)
    return;
  u32 mask = *maskp;
  if (mask == 0 || *tagp != tag)
    return; /* nothing to do, or C# rewrote the pair mid-read */

  /* No stage change in flight (dStage_Delete does putSave, then the next stage's
   * dStage_stagInfoInit does getSave; the stag pointer is stale in between). */
  if (GAMEINFO_NEXT_STAGE_ENABLE(&g_dComIfG_gameInfo) != 0)
    return;
  u8 *stag = GAMEINFO_STAGE_STAGINFO(&g_dComIfG_gameInfo);
  if (!ws_isValidPtr(stag))
    return;
  u32 saveTbl = STAGINFO_SAVE_TBL(stag);
  if (saveTbl >= DSV_STAGE_MAX || saveTbl != (tag & WS_TAG_SAVETBL_MASK))
    return;

  /* The bit must also be set in the live copy: that is what the game itself trusts, and it
   * makes a stale mask harmless (an item whose bit is set can only still exist if the bit
   * arrived after it was created, or the local player is collecting it — filtered above). */
  u32 live = MEMBIT_ITEM0(GAMEINFO_LIVE_MEMORY(&g_dComIfG_gameInfo));

  WsScan scan;
  scan.candidates = mask & live;
  scan.matched = 0;
  scan.count = 0;
  if (scan.candidates == 0)
  {
    ws_publish(0, tag, 0);
    return;
  }

  fopAcIt_Judge((undefined *)ws_judge, &scan);

  /* Everything matched but not despawned now (held / being collected / boss drop / past the
   * per-scan cap) is still pending. fopAcM_delete is deferred, but ws_judge skips actors in
   * BASE_INIT_STATE_DELETING, so the ones deleted below aren't counted by the next scan. */
  ws_publish(scan.matched - scan.count, tag, scan.candidates);

  for (u32 i = 0; i < scan.count && i < WS_MAX_DESPAWN_PER_SCAN; i++)
  {
    fopAc_ac_c *ac = scan.hits[i];
    u32 prm = BASE_PARAMETERS(ac);
    fopAcM_delete((base_process_class *)ac);
    (*(volatile u32 *)WORLDSYNC_DESPAWN_COUNT_ADDR)++;
    *(volatile u32 *)WORLDSYNC_LAST_DESPAWN_ADDR =
        (saveTbl << 16) | (DAITEM_PRM_ITEM_NO(prm) << 8) | DAITEM_PRM_ITEM_BIT(prm);
  }
}
