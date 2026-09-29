// Hook for daPy_Draw - the Link actor draw function
// This replaces the STATIC function at 0x80108204
// daPy_Draw is a static function that calls i_this->draw()

// Include minimal definitions for compilation
#include "link_draw_minimal.h"
// Single source of truth for puppet sync memory layout.
#include "puppet_shared.h"

// Puppet actor constants
#define PUPPET_ACTOR_ID PUPPET_PROC_NAME // proc 0xB5 → g_profile_PUPPET (puppet_shared.h)
#define PUPPET_PARAMS 0x00000000 // Default parameters for Puppet
#define PUPPET_SUBTYPE 0xFF      // Default subtype

// Scratch BSS addresses (STATUS_ADDR, COUNTER_ADDR, DBG_*, ...) live in puppet_shared.h.
#define STAGE_NAME_ADDR 0x803C9D3C
#define ROOM_NUMBER_ADDR 0x803F6A78 // dStage_roomControl_c::mStayNo (s8). 0x803C5295 was save-file event flags

// Spawn state machine states (SPAWN_IDLE..SPAWN_FOUND) live in puppet_shared.h.

// Status codes (STATUS_HOOK/TITLE/NAME/DONE) live in puppet_shared.h: the client reads them too.

// Stage name constants
#define STAGE_SEA  0x7365615F  // "sea_"
#define STAGE_NAME_ID 0x4E616D65 // "Name"
#define CHAR_T 0x54           // 'T'

int link_draw_hook(daPy_lk_c *i_this)
{
    // Debug counter to show we're being called
    volatile unsigned int *counter = (volatile unsigned int *)COUNTER_ADDR;
    (*counter)++;

    // Frame counter for state machine
    volatile unsigned int *frame_counter = (volatile unsigned int *)FRAME_COUNTER_ADDR;
    (*frame_counter)++;

    // Store pointer for debugging
    volatile unsigned int *debug_ptr = (volatile unsigned int *)DEBUG_PTR_ADDR;
    *debug_ptr = (unsigned int)i_this;

    volatile unsigned int *status = (volatile unsigned int *)STATUS_ADDR;
    *status = STATUS_HOOK;

    // ========================================
    // Stage check - skip puppet spawning on title/name screens
    // ========================================
    volatile unsigned int *stage_first_ptr = (volatile unsigned int *)STAGE_NAME_ADDR;
    volatile unsigned int *stage_second_ptr = (volatile unsigned int *)(STAGE_NAME_ADDR + 4);
    unsigned int stage_first = *stage_first_ptr;
    unsigned int stage_second = *stage_second_ptr;

    if (stage_first == STAGE_SEA)
    {
        if ((stage_second >> 24) == CHAR_T)
        {
            *frame_counter = 0;
            *status = STATUS_TITLE;
            return daPy_lk_c__draw(i_this);
        }
    }

    if (stage_first == STAGE_NAME_ID)
    {
        *frame_counter = 0;
        *status = STATUS_NAME;
        return daPy_lk_c__draw(i_this);
    }

    // Draw Link normally first
    volatile int *result_ptr = (volatile int *)RESULT_PTR_ADDR;
    *result_ptr = daPy_lk_c__draw(i_this);

    // ========================================
    // Multi-puppet spawn management
    // ========================================

    // Wait for player to fully initialize before spawning puppets
    volatile unsigned int *spawn_frame = (volatile unsigned int *)SPAWN_FRAME_COUNTER_ADDR;
    *spawn_frame = *spawn_frame + 1;
    if (*spawn_frame < 60)
    {
        return *result_ptr;
    }

    // Access shared memory header
    volatile unsigned int *sync_magic = (volatile unsigned int *)PUPPET_SYNC_BASE;
    volatile unsigned int *sync_numActive = (volatile unsigned int *)(PUPPET_SYNC_BASE + 0x04);

    // Debug counters — integer-only, readable from C# over memory
    volatile unsigned int *dbg_magic_detected = (volatile unsigned int *)DBG_MAGIC_DETECTED_ADDR;
    volatile unsigned int *dbg_last_desired = (volatile unsigned int *)DBG_LAST_DESIRED_ADDR;

    // Determine how many puppets to have active
    // Only spawn when the sync magic is present (the client's PuppetSyncService is attached)
    int desiredCount = 0;
    if (*sync_magic == PUPPET_SYNC_MAGIC)
    {
        (*dbg_magic_detected)++;
        desiredCount = (int)*sync_numActive;
        if (desiredCount > PUPPET_MAX_SLOTS)
            desiredCount = PUPPET_MAX_SLOTS;
        if (desiredCount < 0)
            desiredCount = 0;
    }
    *dbg_last_desired = (unsigned int)desiredCount;

    // Multi-puppet tracking arrays
    volatile unsigned int *proc_ids = (volatile unsigned int *)PUPPET_PROC_IDS_ADDR;
    volatile unsigned int *actor_ptrs = (volatile unsigned int *)PUPPET_ACTOR_PTRS_ADDR;
    volatile unsigned int *spawn_states = (volatile unsigned int *)PUPPET_SPAWN_STATE_ADDR;
    volatile unsigned int *spawn_counter = (volatile unsigned int *)PUPPET_SPAWN_COUNTER;

    // Get room number for spawning
    volatile unsigned char *current_room = (volatile unsigned char *)ROOM_NUMBER_ADDR;
    unsigned char room = *current_room;

    // Process each slot
    int i;
    for (i = 0; i < PUPPET_MAX_SLOTS; i++)
    {
        if (i < desiredCount)
        {
            // This slot should be active
            switch (spawn_states[i])
            {
            case SPAWN_IDLE:
            {
                // Need to spawn - only spawn one puppet per frame to avoid lag
                if (*spawn_counter == 0 || (*frame_counter % 30 == 0))
                {
                    // Spawn position = Link's position
                    // Use raw offsets - GCC pads fopAcM_prm_class differently than Metrowerks
                    float *currentPos = (float *)((u8 *)i_this + 0x1F8); // fopAc_ac_c::current.pos
                    float pos[3];
                    pos[0] = currentPos[0];
                    pos[1] = currentPos[1];
                    pos[2] = currentPos[2];

                    s16 *collisionRot = (s16 *)((u8 *)i_this + 0x20C); // fopAc_ac_c::shape_angle
                    short rot[3];
                    rot[0] = collisionRot[0];
                    rot[1] = collisionRot[1];
                    rot[2] = collisionRot[2];

                    // Pass slot index in upper byte of params
                    u32 params = ((u32)i << 24);

                    volatile unsigned int *dbg_create_attempts = (volatile unsigned int *)DBG_CREATE_ATTEMPTS_ADDR;
                    volatile unsigned int *dbg_create_successes = (volatile unsigned int *)DBG_CREATE_SUCCESSES_ADDR;
                    volatile unsigned int *dbg_create_failures = (volatile unsigned int *)DBG_CREATE_FAILURES_ADDR;
                    volatile unsigned int *dbg_last_pid = (volatile unsigned int *)DBG_LAST_PID_ADDR;

                    (*dbg_create_attempts)++;

                    unsigned int pid = fopAcM_create(
                        PUPPET_ACTOR_ID,
                        params,
                        (cXyz *)pos,
                        room,
                        (csXyz *)rot,
                        0,
                        PUPPET_SUBTYPE,
                        0);

                    *dbg_last_pid = pid;

                    if (pid != 0xFFFFFFFF && pid != 0)
                    {
                        (*dbg_create_successes)++;
                        proc_ids[i] = pid;
                        spawn_states[i] = SPAWN_SPAWNED;
                        *spawn_counter = *spawn_counter + 1;
                    }
                    else
                    {
                        (*dbg_create_failures)++;
                    }
                }
                break;
            }

            case SPAWN_SPAWNED:
            {
                // Have process ID, search for actor pointer
                if (proc_ids[i] != 0 && proc_ids[i] != 0xFFFFFFFF)
                {
                    void *actor = 0;
                    fopAcM_SearchByID(proc_ids[i], &actor);
                    if (actor != NULL)
                    {
                        actor_ptrs[i] = (unsigned int)actor;
                        spawn_states[i] = SPAWN_FOUND;
                    }
                }
                break;
            }

            case SPAWN_FOUND:
            {
                // Puppet is active and running
                break;
            }
            }
        }
        else
        {
            // This slot should be inactive - delete any spawned puppet
            if (spawn_states[i] == SPAWN_SPAWNED || spawn_states[i] == SPAWN_FOUND)
            {
                volatile unsigned int *dbg_delete_issued = (volatile unsigned int *)DBG_DELETE_ISSUED_ADDR;
                (*dbg_delete_issued)++;

                if (proc_ids[i] != 0 && proc_ids[i] != 0xFFFFFFFF)
                {
                    fopAcM_delete_1(proc_ids[i]);
                }
                proc_ids[i] = 0;
                actor_ptrs[i] = 0;
                spawn_states[i] = SPAWN_IDLE;
                if (*spawn_counter > 0)
                    *spawn_counter = *spawn_counter - 1;
            }
        }
    }

    // Update status based on overall state
    int activeCount = 0;
    for (i = 0; i < PUPPET_MAX_SLOTS; i++)
    {
        if (spawn_states[i] == SPAWN_FOUND)
            activeCount++;
    }

    if (activeCount > 0)
    {
        *status = STATUS_DONE;
    }

    return *result_ptr;
}
