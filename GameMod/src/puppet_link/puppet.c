/* Puppet Link - C REL Module using extracted TWW headers
 * This creates a second player puppet that inherits from daPy_lk_c
 */

#include "../../include/ww_defines.h"

// Include execute implementation (self-contained with all its helpers)
#include "puppet_execute.c"

// Per-Link outfit + tunic colour (the local Link's every frame, each puppet's around its entry)
#include "puppet_appearance.c"

// Include draw implementation (self-contained with all its helpers)
#include "puppet_draw.c"
// The item the peer holds / uses (called from execute and draw)
#include "puppet_held.c"
// Shared-world layer 2: live despawn of placed items other players collected
#include "puppet_worldsync.c"
// The peer's King of Red Lions
#include "puppet_boat.c"
// The peer's name above the puppet
#include "puppet_nametag.c"
// Create-only actors follow bits other players set (chests, walls, crystals, key locks)
#include "puppet_liveworld.c"

/* Process condition flags */
#define PROC_CONDITION_INIT 0x08

// Inline memset replacement - avoids calling fake address
static inline void *inline_memset(void *ptr, int value, unsigned int num)
{
  unsigned char *p = (unsigned char *)ptr;
  for (unsigned int i = 0; i < num; i++)
  {
    p[i] = (unsigned char)value;
  }
  return ptr;
}

#define memset inline_memset

// Field offsets for struct members not in ww_structs.h (verified from tww-decomp)
#define OFFSET_fopAc_actor_condition 0x1C8 // u32
#define OFFSET_fopAc_home 0x1D0            // actor_place
#define OFFSET_fopAc_old 0x1E4             // actor_place

// Accessor macros for fields used in puppet.c
#define GET_ACTOR_CONDITION(actor) (*(u32 *)((u8 *)(actor) + OFFSET_fopAc_actor_condition))
#define GET_ACTOR_HOME(actor) ((actor_place *)((u8 *)(actor) + OFFSET_fopAc_home))
#define GET_ACTOR_OLD(actor) ((actor_place *)((u8 *)(actor) + OFFSET_fopAc_old))

// Puppet class definition - contains daPy_lk_c as first member
typedef struct PUPPET_class
{
  // The parent IS daPy_lk_c (Link's class)
  daPy_lk_c parent;

  // Per-instance puppet data
  // NOTE: slotIndex MUST stay first - puppet_execute.c reads it at sizeof(daPy_lk_c).
  u32 slotIndex;       // Which shared memory slot this puppet reads from (0-2)
  u32 followState;     // 0=uninitialized, 1=waiting, 2=following
  u32 parked;          // 1 while the slot is inactive (see puppet_execute.c PARKED PUPPETS)

  // Per-instance eye/brow z-compare packets (sharing them between puppets makes the
  // draw list cyclic -> hang). Initialized in daPuppet_phase_2.
  PuppetDrawPackets packets;

  // Tunic recolour cache (game heap, puppet_appearance.c), freed in daPuppet_Delete.
  u8 *lookBlock;
  u32 appearanceCounted; // 1 once counted by puppet_appearance_onCreate

  // The peer's boat, drawn by this puppet while they ride it (puppet_boat.c).
  PuppetBoat boat;

  // The peer's name, entered in the 2D list by each draw (puppet_nametag.c).
  PuppetNameTag nameTag;

  // REL-drawn held boomerang / carried bomb (puppet_held.c).
  PuppetHeld held;
} PUPPET_class;

// ww_structs.h's daPy_lk_c must be at least as large as the real class (0x4C28) or
// PUPPET_class's own fields would overlap the tail of daPy_lk_c.
typedef char puppet_check_dapy_size[(sizeof(daPy_lk_c) >= 0x4C28) ? 1 : -1];

// Actor params written over the hook's (slot << 24) once the slot is cached, before
// playerInit/makeBgWait read them: event 0xFF (no start demo), start mode 0, room 0,
// bit 0x40 set (skip dComIfGs_setRestartRoom, which would overwrite the local player's
// void-out restart point). See d_a_player_main.cpp:12296-12316, 12455-12458.
#define PUPPET_ACTOR_PARAMS_SAFE 0xFF000040
#define OFFSET_base_mParameters 0x0B0 // base_process_class::mParameters (f_pc_base.h:30)

// linktexS3TC ResTIMG header (shared by every Link model - it lives in the resident
// Link archive's cl.bdl TEX1): daPy_lk_c::mpCurrLinktex at 0x338, ResTIMG is 0x20 bytes.
#define PUPPET_RESTIMG_WORDS RESTIMG_WORDS

// Global game state that daPy_lk_c's playerInit/playerDelete write as if the puppet were the
// one real player. Snapshot before, restore after, so a puppet can never change the local
// game's event flow (start demo) or the local player's status bits.
typedef struct
{
  u32 evtException[GAMEINFO_EVT_EXCEPTION_WORDS]; // play.mEvtManager.mException
  u32 playerStatus[2];                            // play.mPlayerStatus[0][0..1]
} PuppetGlobalSnapshot;

static void puppet_saveGlobals(PuppetGlobalSnapshot *s)
{
  u32 *evt = GAMEINFO_EVT_EXCEPTION(&g_dComIfG_gameInfo);
  u32 *status = &GAMEINFO_PLAYERSTATUS(&g_dComIfG_gameInfo);
  for (int i = 0; i < GAMEINFO_EVT_EXCEPTION_WORDS; i++)
    s->evtException[i] = evt[i];
  s->playerStatus[0] = status[0];
  s->playerStatus[1] = status[1];
}

static void puppet_restoreGlobals(const PuppetGlobalSnapshot *s)
{
  u32 *evt = GAMEINFO_EVT_EXCEPTION(&g_dComIfG_gameInfo);
  u32 *status = &GAMEINFO_PLAYERSTATUS(&g_dComIfG_gameInfo);
  for (int i = 0; i < GAMEINFO_EVT_EXCEPTION_WORDS; i++)
    evt[i] = s->evtException[i];
  status[0] = s->playerStatus[0];
  status[1] = s->playerStatus[1];
}

// Function declarations
int daPuppet_Create(PUPPET_class *this);
int daPuppet_IsDelete(PUPPET_class *this);
int daPuppet_Delete(PUPPET_class *this);
int daPuppet_Draw(PUPPET_class *this);
int daPuppet_Execute(PUPPET_class *this);

static void *_ctors SECTION(".ctors");
static void *_dtors SECTION(".dtors");

void _prolog()
{
  OSReportEnable();
  DynamicLink__ModuleConstructorsX(&_ctors);
  DynamicLink__ModuleProlog();
}

void _epilog()
{
  DynamicLink__ModuleEpilog();
  DynamicLink__ModuleDestructorsX(&_dtors);
}

void _unresolved()
{
  DynamicLink__ModuleUnresolved();
}

// (Unused resource IDs, heap defines, and struct definitions removed)

// ============================================================================
// THREE-PHASE CREATION SYSTEM
// Based on daPy_lk_c creation pattern from tww-decomp
// ============================================================================

// Phase state constants (from tww-decomp/include/SSystem/SComponent/c_phase.h)
// WARNING: ww_structs.h has INCORRECT values (cPhs_NEXT_e = 6, should be 2)
// We must override with correct values from tww-decomp
#ifdef cPhs_NEXT_e
#undef cPhs_NEXT_e
#endif
#ifdef cPhs_COMPLEATE_e
#undef cPhs_COMPLEATE_e
#endif
#ifdef cPhs_ERROR_e
#undef cPhs_ERROR_e
#endif

#define cPhs_INIT_e 0x0      // Retry this phase
#define cPhs_LOADING_e 0x1   // Loading resource
#define cPhs_NEXT_e 0x2      // Advance to next phase (ww_structs.h incorrectly says 6!)
#define cPhs_STOP_e 0x3      // Stop (pause)
#define cPhs_COMPLEATE_e 0x4 // All phases complete
#define cPhs_ERROR_e 0x5     // Error

// Include puppet initialization functions
// puppet_init.c removed - now using original daPy_lk_c__playerInit + independent display lists
// #include "puppet_init.c"

/**
 * PHASE 1: Register with Game Systems
 *
 * This phase runs once and:
 * - Registers puppet with game's player system
 * - Sets up stage layer
 * - Initializes attention tracking
 *
 * Returns: cPhs_NEXT_e (always advances to phase 2)
 */
static int daPuppet_phase_1(PUPPET_class *this)
{

  fopAc_ac_c *base = (fopAc_ac_c *)&this->parent;

  // Extract slot index from actor params upper byte
  // link_draw_hook.c passes: params = (slotIndex << 24)
  u32 params = *(u32 *)((u8 *)base + OFFSET_base_mParameters); // actor parameters
  this->slotIndex = (params >> 24) & 0xFF;

  // The slot is cached; replace the params so playerInit/makeBgWait don't treat the
  // slot byte as a start event or clobber the local player's restart room.
  *(u32 *)((u8 *)base + OFFSET_base_mParameters) = PUPPET_ACTOR_PARAMS_SAFE;

  // Initialize per-instance state
  this->followState = 0;
  this->parked = 0;
  this->lookBlock = NULL;
  puppet_appearance_onCreate();
  this->appearanceCounted = 1;
  puppet_boatInit(&this->boat);
  puppet_nametag_onCreate();
  puppet_heldInit(&this->held);
  puppet_liveworld_onCreate();

  // Setup actor in stage layer system
  fopAcM_setStageLayer(base);

  // Z-targeting attention setup is handled every frame in puppet_updateAttentionInfo()
  // No need to duplicate it here in phase_1

  return cPhs_NEXT_e; // Always advance to next phase
}

/**
 * PHASE 2: Constructor & playerInit()
 *
 * NEW APPROACH:
 * - Use original daPy_lk_c__playerInit (proven to work for player)
 * - Then create independent display lists to avoid sharing with player
 *
 * Returns: cPhs_NEXT_e (advance to phase 3)
 */
static int daPuppet_phase_2(PUPPET_class *this)
{

  fopAc_ac_c *base = (fopAc_ac_c *)&this->parent;
  daPy_lk_c *link = &this->parent;

  // Real-Link-ready gate: don't construct the puppet until the real player exists AND
  // has finished its own construction. Copying position/collision state from a
  // half-constructed Link during a scene transition is what produces the invisible-Link
  // softlock on room re-entry. The phase handler retries this phase next frame.
  {
    daPy_lk_c *realPlayerGate = (daPy_lk_c *)dComIfGp_getPlayer(0);
    if (!realPlayerGate)
      return cPhs_INIT_e;
    fopAc_ac_c *realBase = (fopAc_ac_c *)&realPlayerGate->parent.parent;
    if ((GET_ACTOR_CONDITION(realBase) & PROC_CONDITION_INIT) == 0)
      return cPhs_INIT_e;
  }

  // Check if constructor has already been run
  if ((GET_ACTOR_CONDITION(base) & PROC_CONDITION_INIT) == 0)
  {
    daPy_lk_c__daPy_lk_c(link);
    GET_ACTOR_CONDITION(base) |= PROC_CONDITION_INIT;

    // CRITICAL: Initialize positions AFTER constructor (which zeroes memory) but BEFORE playerInit
    // playerInit calls mAcch.Set() which needs old.pos to be valid!
    daPy_lk_c *realPlayer = (daPy_lk_c *)dComIfGp_getPlayer(0);
    if (realPlayer)
    {
      fopAc_ac_c *playerActor = &realPlayer->parent.parent;

      // Copy player's position to puppet
      base->current.pos.x = playerActor->current.pos.x;
      base->current.pos.y = playerActor->current.pos.y;
      base->current.pos.z = playerActor->current.pos.z;

      // Also initialize home position
      actor_place *home = GET_ACTOR_HOME(base);
      home->pos.x = playerActor->current.pos.x;
      home->pos.y = playerActor->current.pos.y;
      home->pos.z = playerActor->current.pos.z;

      // CRITICAL: Initialize old.pos to current.pos
      // playerInit will call mAcch.Set(fopAcM_GetOldPosition_p(this), ...) which needs this!
      actor_place *old = GET_ACTOR_OLD(base);
      old->pos.x = base->current.pos.x;
      old->pos.y = base->current.pos.y;
      old->pos.z = base->current.pos.z;
    }
  }

  // Initialize this puppet's own display list packets
  puppet_initPackets(&this->packets);

  // playerInit rewrites the linktexS3TC ResTIMG header in the model data it shares with
  // the real Link (casual/hero clothes swap, d_a_player_main.cpp:12356-12373). Snapshot the
  // real Link's header and put it back afterwards so the local player's clothes texture
  // is untouched. (The puppet's own mpCurrLinktex/mOtherLinktex are never used afterwards:
  // puppet_appearance.c builds each puppet's header from the local Link's pristine pair.)
  u32 linktexBackup[PUPPET_RESTIMG_WORDS];
  u32 *linktex = NULL;
  {
    daPy_lk_c *realPlayerTex = (daPy_lk_c *)dComIfGp_getPlayer(0);
    if (realPlayerTex)
    {
      linktex = (u32 *)DAPY_LK_MPCURRLINKTEX(realPlayerTex);
      if ((u32)linktex < 0x80000000 || ((u32)linktex & 3) != 0)
        linktex = NULL;
    }
    if (linktex)
    {
      for (int i = 0; i < PUPPET_RESTIMG_WORDS; i++)
        linktexBackup[i] = linktex[i];
    }
  }

  // Call the ORIGINAL playerInit function (void - no return value). It behaves as if it
  // were THE player: it registers a start demo in the global event manager (based on the
  // LOCAL game's restart state) and edits the local player-status words. Guard it so a
  // puppet spawning mid-cutscene can't cancel the local player's event (that froze the
  // local game at the Outset start event with no HUD).
  PuppetGlobalSnapshot globals;
  puppet_saveGlobals(&globals);
  daPy_lk_c__playerInit(link);
  puppet_restoreGlobals(&globals);

  if (linktex)
  {
    for (int i = 0; i < PUPPET_RESTIMG_WORDS; i++)
      linktex[i] = linktexBackup[i];
  }

  // ============================================================================
  // CRITICAL FIX: Manually initialize collision checker structures
  // ============================================================================
  // When calling daPy_lk_c__daPy_lk_c() from C, the C++ member constructors don't run.
  // This means dBgS_LinkAcch::dBgS_LinkAcch() never calls SetLink(), so mbLinkThrough
  // is never set to true.
  //
  // IMPORTANT: GroundCheckInit calls m_gnd.SetExtChk(*(cBgS_Chk*)this) which COPIES
  // the mPolyPassChk and mGrpPassChk pointers from the PARENT dBgS_Acch to m_gnd.
  // So we must set up the parent's cBgS_Chk pointers, NOT m_gnd's directly!
  //
  // mAcch is at offset 0x046C in daPy_lk_c
  // ============================================================================
  {
    u8 *mAcch = (u8 *)link + 0x046C;

    // Set up the PARENT dBgS_Acch's cBgS_Chk pointers
    // These will be copied to m_gnd, m_roof, m_wtr via SetExtChk() during CrrPos
    *(u32 *)(mAcch + 0x00) = (u32)(mAcch + 0x14); // mPolyPassChk -> dBgS_PolyPassChk
    *(u32 *)(mAcch + 0x04) = (u32)(mAcch + 0x20); // mGrpPassChk -> dBgS_GrpPassChk

    // CRITICAL: Set mActorPid to THIS puppet's process ID (not the player's!)
    *(u32 *)(mAcch + 0x08) = fopAcM_GetID((fopAc_ac_c *)link);

    // Set mbLinkThrough on mAcch's dBgS_PolyPassChk (offset 0x14 + 0x6 = 0x1A)
    *(u8 *)(mAcch + 0x1A) = 1; // mbLinkThrough = true
  }

  // CRITICAL: Initialize mDemo fields to safe values
  // The puppet doesn't need demo mode, but setGetDemo() validates these fields
  // mDemo.mDemoType = 0 means no demo active
  // mDemo.mDemoMode must be < DEMO_LAST_e (0x4B) or == DEMO_NEW_ANM0_e (0x200)
  daPy_py_c *py = &link->parent;
  py->mDemo.mDemoType = 0;
  py->mDemo.mDemoMode = 0; // DEMO_UNK00_e = 0 (safe value)

  // OSReport("PUPPET: phase 2 complete\n");
  return cPhs_NEXT_e;
}

/**
 * PHASE 3: Background Wait & Process Init
 *
 * This phase:
 * - Calls makeBgWait() which waits for ground collision
 * - Initializes the first active process (procWait_init, procMove_init, etc.)
 *
 * Returns: cPhs_INIT_e (retry) or cPhs_NEXT_e (creation complete)
 */
static int daPuppet_phase_3(PUPPET_class *this)
{
  daPy_lk_c *link = &this->parent;

  // OSReport("PUPPET: phase 3 - calling makeBgWait\n");

  // Call makeBgWait() - this handles:
  // 1. Updating position on terrain
  // 2. Waiting for ground collision (returns cPhs_INIT_e if not ready)
  // 3. Initializing first process (procWait_init, etc.)
  int result = daPy_lk_c__makeBgWait(link);

  // OSReport("PUPPET: makeBgWait returned %d\n", result);

  if (result == cPhs_INIT_e)
  {
    return cPhs_INIT_e; // Retry next frame
  }

  return result; // Should be cPhs_NEXT_e, which completes creation
}

/**
 * Main Create Function - Phase Handler
 *
 * This is called EVERY FRAME during creation until it returns cPhs_COMPLEATE_e.
 * Uses Wind Waker's built-in cPhs_Handler to manage phase progression.
 */
int daPuppet_Create(PUPPET_class *this)
{
  // DEBUG: log spawn — visible in Dolphin's OSReport log
  OSReport("[PUPPET] Create this=%p slot=%d\n", this, (int)this->slotIndex);

  // NOTE: We CANNOT use the real daPy_Create here because it calls:
  //   dComIfGp_setPlayer(0, i_this);
  //   dComIfGp_setLinkPlayer(i_this);
  // This would replace the global player pointer with our puppet, which is wrong!
  // Instead, we use custom phase functions that skip those calls.

  // Phase handler function table
  // These are called in order: phase_1 -> phase_2 -> phase_3
  static int (*l_method[])(void *) = {
      (int (*)(void *))daPuppet_phase_1,
      (int (*)(void *))daPuppet_phase_2,
      (int (*)(void *))daPuppet_phase_3,
      NULL, // Terminator
  };

  daPy_lk_c *link = &this->parent;

  // Get pointer to mPhase struct at offset 0x0320 in daPy_lk_c
  // This is a request_of_phase_process_class that tracks phase state
  void *mPhase_ptr = (void *)((u8 *)link + 0x0320);

  // Use Wind Waker's phase handler - this handles all the phase advancement logic
  // dComLbG_PhaseHandler(request_of_phase_process_class*, handler_table, this)
  // It will:
  // 1. Get current phase from mPhase->id
  // 2. Call the phase function
  // 3. If phase returns cPhs_NEXT_e, advance to next phase
  // 4. Return cPhs_COMPLEATE_e when all phases done
  // 5. Return cPhs_INIT_e to retry current phase
  int result = d_com_lib_game__dComLbG_PhaseHandler(mPhase_ptr, (int (*)(void *))l_method, this);

  return result;
}

int daPuppet_IsDelete(PUPPET_class *this)
{
  return 1;
}

int daPuppet_Delete(PUPPET_class *this)
{
  // DEBUG: log despawn — visible in Dolphin's OSReport log
  OSReport("[PUPPET] Delete this=%p slot=%d\n", this, (int)this->slotIndex);

  // Call the real player delete function to clean up:
  // - Foot effect / swim tail / smoke / damage emitter callbacks
  // - Light influence (dKy_plight_cut)
  // - Audio objects (mDoAud_seDeleteObject)
  // - Animation heaps (mDoExt_destroySolidHeap)
  // Without this, room transitions crash because the game's audio/effect
  // systems still hold pointers to our now-freed puppet memory.
  //
  // playerDelete does `*mpCurrLinktex = mOtherLinktex` when CASUAL_CLOTHES is set
  // (d_a_player_main.cpp:11786-11788). mpCurrLinktex is the linktexS3TC header shared
  // with the real Link, and the puppet's flag mirrors the PEER, so that write would swap
  // the local player's clothes texture. Clear the flag first.
  DAPY_PY_NO_RESET_FLG1((fopAc_ac_c *)&this->parent) &= ~daPyFlg1_CASUAL_CLOTHES;

  // playerDelete also clears bits in the LOCAL player's status words (d_a_player_main.cpp
  // playerDelete: clearPlayerStatus0/1) — keep the local player's state intact.
  PuppetGlobalSnapshot globals;
  puppet_saveGlobals(&globals);
  daPy_lk_c__playerDelete(&this->parent);
  puppet_restoreGlobals(&globals);

  // Frees this puppet's tunic copy; the last puppet also settles the local Link's look for
  // the REL being unloaded. Packets live in this (about to be freed) instance.
  puppet_appearance_onDelete(&this->lookBlock, (int)this->appearanceCounted);
  this->appearanceCounted = 0;

  puppet_boatDelete(&this->boat);
  puppet_heldDelete(&this->held);

  return 1;
}

/**
 * Main Draw Function
 *
 * Delegates to the puppet_draw.c implementation.
 * All draw logic is fully abstracted in puppet_draw().
 */
int daPuppet_Draw(PUPPET_class *this)
{
  // Parked this frame (execute skipped the model calc): never draw, even if C# flipped the
  // slot ACTIVE between our execute and this draw.
  if (this->parked)
  {
    daPy_lk_c__offBodyEffect(&this->parent);
    return 1;
  }
  puppet_draw(&this->parent, this->slotIndex, &this->packets, &this->lookBlock);
  puppet_heldDraw(&this->parent, &this->held);
  puppet_boatDraw((fopAc_ac_c *)&this->parent, &this->boat);
  puppet_nametag_queue((fopAc_ac_c *)&this->parent, DAPY_LK_MPCLMODEL(&this->parent), this->slotIndex, &this->nameTag);
  return 1;
}

int daPuppet_Execute(PUPPET_class *this)
{
  daPy_lk_c *link = &this->parent;

  // Get the real player to follow
  daPy_lk_c *realPlayer = (daPy_lk_c *)dComIfGp_getPlayer(0);
  if (!realPlayer || realPlayer == link)
  {
    return 1; // No valid player or we ARE the player somehow
  }

  // Once per game frame regardless of puppet count (guarded inside). Runs for parked
  // puppets too: C# keeps a parked slot-0 puppet alive while despawns are pending, or while
  // the local Link doesn't show the local player's outfit/colour yet.
  puppet_worldsync_tick();
  puppet_appearance_tick();

  // Inactive slot -> parked: no execute, no collision/attention, invisible.
  if (puppet_slotIsParked(this->slotIndex))
  {
    if (!this->parked)
    {
      OSReport("[PUPPET] park slot=%d\n", (int)this->slotIndex);
      puppet_parkEnter(link);
      this->parked = 1;
    }
    this->boat.visible = 0;
    puppet_parkTick(link);
    return 1;
  }
  if (this->parked)
  {
    OSReport("[PUPPET] resume slot=%d\n", (int)this->slotIndex);
    puppet_parkResume(link, this->slotIndex);
    this->parked = 0;
    this->followState = 0; // force puppet_requestProc to (re)init the peer's proc
  }

  // Bridge per-instance follow state to global static in puppet_execute.c.
  //
  // THREAD SAFETY: This global is safe because GameCube is single-threaded
  // and the actor framework executes actors sequentially (never in parallel).
  // Each puppet saves/restores l_puppetFollowState around its execute call,
  // so multiple puppet instances do not interfere with each other.
  //
  // If multiple puppets ever needed to execute concurrently (e.g., on a
  // multi-core system), this would need to be replaced with per-instance
  // storage -- either by passing a pointer into puppet_execute or by
  // reading/writing PUPPET_class.followState directly inside the execute code.
  extern int l_puppetFollowState;
  l_puppetFollowState = (int)this->followState;

  // Pose the peer's boat first so puppet_execute can seat the puppet in this frame's pose.
  puppet_boatExecute((fopAc_ac_c *)link, &this->boat, this->slotIndex);
  l_puppetBoat = &this->boat;

  puppet_execute(link);
  puppet_heldExecute(link, &this->held);

  l_puppetBoat = NULL;
  this->followState = (u32)l_puppetFollowState;

  return 1;
}

profile_method_class l_daPuppet_Method =
    {
        .parent = {
            .mpCreate = &daPuppet_Create,
            .mpDelete = &daPuppet_Delete,
            .mpExecute = &daPuppet_Execute,
            .mpIsDelete = &daPuppet_IsDelete,
            .mpDraw = &daPuppet_Draw,
        },
        .mpUnkFunc1 = 0,
        .mpUnkFunc2 = 0,
        .mpUnkFunc3 = 0,
};

const actor_process_profile_definition g_profile_PUPPET = {
    .parent = {
        .mLayerID = -3,
        .mListID = 3, // Affects execution order of actors in a given frame. Lower numbers execute first.
        .mListPrio = -3,
        .mPName = PUPPET_PROC_NAME, // 0xB5 (PROC_RECTANGLE) - the proc id the hook spawns with.
                                    // NOT 0x58 (Obj_Smplbg): d_a_npc_tc treats that name as its tower.
        .field_0x0A = {0},
        .mpMtd0 = &g_fpcLf_Method,
        .mSize = sizeof(PUPPET_class),
        .mSizeOther = 0,
        .mDefaultParameters = 0,
        .mpMtd1 = &g_fopAc_Method,
    },

    .mDrawPriority = 0x9F,
    .field_0x22 = {0},
    .mpMtd2 = &l_daPuppet_Method,
    .mStatus = 0x00000400,                    // fopAcStts_FREEZE_e - Standard status flag for actors
    .mActorType = fopAc_ac_c__Group__Regular, // Keep as regular actor, NOT player
    .mCullType = 0x0,                         // fopAc_CULLBOX_0_e - Standard cull type like player
    .field_0x2E = {0},
};
