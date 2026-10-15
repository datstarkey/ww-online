// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The shared world outside the save window (pc/tww.h, "world"): the current stage's save slot, room zones, salvage
// points, and the ww-online REL's two live passes ported to C++ (GameMod puppet_worldsync.c / puppet_liveworld.c):
// placed items other players collected vanish, and actors that read their flag only when created (chests,
// bombable walls, crystal switches, key doors...) are re-created once another player's bit lands. Both run from
// wwo_frame, between frames, every 4th frame.
#include "d/dolzel.h" // IWYU pragma: keep
#include "d/actor/d_a_door10.h"
#include "d/actor/d_a_door12.h"
#include "d/actor/d_a_item.h"
#include "d/actor/d_a_obj_warpt.h"
#include "d/actor/d_a_salvage.h"
#include "d/actor/d_a_stone2.h"
#include "d/d_com_inf_game.h"
#include "d/d_meter.h"
#include "d/d_salvage.h"
#include "f_op/f_op_actor_iter.h"
#include "f_op/f_op_actor_mng.h"
#include "f_pc/f_pc_layer.h"
#include "pc/pc_harness.h"

#include "ww_online_internal.h"

#include <cstdio>
#include <cstring>

static_assert(sizeof(TwwWorldSalvagePoint) == 4 && sizeof(TwwWorldLiveBatch) == 48 && sizeof(TwwWorldLiveStatus) == 16 &&
              sizeof(TwwWorldStats) == 8, "tww.h world structs");
static_assert(TWW_WORLD_SALVAGE_MAX == dSalvage_control_c::MAX_TOTAL, "salvage points");

namespace {

const s8 kInitStateDeleting = 3;    // base_process_class::mInitState once fpcDt_Delete took it (f_pc_deletor.cpp)
const u32 kMaxDespawnPerScan = 8;
const u32 kMaxHits = 16;            // live-world actors handled per batch
const u32 kMaxWait = 16;            // passes (~2 s) an actor not ready yet may hold a batch back
const u8 kRoomLoaded = 0x01;        // dStage_roomStatus_c::mFlags
const u8 kRoomBusy = 0x06;          // loading or unloading
const u8 kRoomHidden = 0x08;        // d_door.cpp:305
const u8 kItemFlagCarried = 0x08 | 0x40; // daItem_c::mFlag: on the boomerang / the hookshot
const u8 kDoorIdle = 1;             // door10 m354 / door12 m314: Wait
const s32 kWarptClosed = 1;         // daObj_Warpt_c::m290 (mode): modeClose

struct DespawnState {
    s32 slot = -1;
    u32 mask = 0;
    TwwWorldStats stats = {};
} sDespawn;

struct LiveState {
    TwwWorldLiveBatch batch = {};
    TwwWorldLiveStatus status = {};
    u32 wait = 0;
} sLive;

fopAc_ac_c* localPlayer() {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    return player != NULL && fopAcM_SearchByName(fpcNm_PLAYER_e) == player ? player : NULL;
}

// The save slot of the stage in play: the stage info is valid only while its play scene runs and no stage change
// is pending (dStage_Delete frees it and leaves the pointer).
s32 currentSlot() {
    if (localPlayer() == NULL || dComIfGp_isEnableNextStage() || dComIfGp_getStageStagInfo() == NULL) {
        return -1;
    }
    const s32 slot = dStage_stagInfo_GetSaveTbl(dComIfGp_getStageStagInfo());
    return slot < dSv_save_c::STAGE_MAX ? slot : -1;
}

bool alive(const fopAc_ac_c* ac) {
    return ac->mCreateResult == cPhs_NEXT_e && ac->mInitState != kInitStateDeleting;
}

struct DespawnScan {
    u32 candidates;
    u32 count;
    fopAc_ac_c* hits[kMaxDespawnPerScan];
};

// Placed items whose item bit is a candidate, idle on the ground: never one the local player is collecting
// (status 5-9), one another actor holds (2/3), a boss drop or one on the boomerang / hookshot.
void* judgeItem(void* proc, void* data) {
    DespawnScan* scan = static_cast<DespawnScan*>(data);
    daItem_c* item = static_cast<daItem_c*>(proc);
    if (fopAcM_GetName(item) != fpcNm_ITEM_e || !alive(item) || !fopAcM_CheckCondition(item, fopAcCnd_INIT_e)) {
        return NULL;
    }
    const u32 prm = fopAcM_GetParam(item);
    if ((prm & 0xFF) == dItemNo_BLUE_JELLY_e) {
        return NULL; // blue Chu Jelly: its "item bit" is a save switch
    }
    const s32 bit = item->mItemBitNo;
    if (bit != (s32)((prm >> 8) & 0x7F) || bit < 0 || bit >= 32 || (scan->candidates & (1u << bit)) == 0 ||
        scan->count >= kMaxDespawnPerScan) {
        return NULL;
    }
    if (item->mItemStatus > 1 || (item->mFlag & kItemFlagCarried) != 0 || fopAcM_CheckStatus(item, fopAcStts_HOOK_CARRY_e)) {
        return NULL;
    }
    scan->hits[scan->count++] = item;
    return NULL;
}

// A Heart Container still waiting (a boss drop, or idle on the ground), never one being collected or held.
void* judgeHeart(void* proc, void* data) {
    DespawnScan* scan = static_cast<DespawnScan*>(data);
    daItem_c* item = static_cast<daItem_c*>(proc);
    if (fopAcM_GetName(item) != fpcNm_ITEM_e || !alive(item) || item->m_itemNo != dItemNo_HEART_CONTAINER_e ||
        scan->count >= kMaxDespawnPerScan) {
        return NULL;
    }
    const u8 status = item->mItemStatus;
    if (status <= 1 || status == 0xA || status == 0xB) {
        scan->hits[scan->count++] = item;
    }
    return NULL;
}

void despawn(const DespawnScan& scan, s32 slot) {
    for (u32 i = 0; i < scan.count; i++) {
        daItem_c* item = static_cast<daItem_c*>(scan.hits[i]);
        const u32 prm = fopAcM_GetParam(item);
        const bool heart = item->m_itemNo == dItemNo_HEART_CONTAINER_e;
        fopAcM_delete(item);
        sDespawn.stats.despawn_count++;
        sDespawn.stats.last_despawn = (u32)slot << 16 | (heart ? dItemNo_HEART_CONTAINER_e : prm & 0xFF) << 8 |
                                      (heart ? 0x7F : (prm >> 8) & 0x7F);
        fprintf(stderr, "[wwo] world: %s taken by another player, removed (slot %d)\n",
                heart ? "Heart Container" : "placed item", slot);
    }
}

// A boss's Heart Container has no item bit: picking it up sets the stage's STAGE_LIFE bit, which the client ORs in.
void despawnItems(s32 slot) {
    dSv_memBit_c& live = g_dComIfG_gameInfo.save.getMemory().getBit();
    DespawnScan scan = {};
    if (live.isStageLife()) {
        fopAcIt_Judge(judgeHeart, &scan);
        despawn(scan, slot);
    }
    if (sDespawn.mask == 0 || sDespawn.slot != slot) {
        return;
    }
    scan = {};
    scan.candidates = sDespawn.mask & (u32)live.mItem[0];
    if (scan.candidates != 0) {
        fopAcIt_Judge(judgeItem, &scan);
        despawn(scan, slot);
    }
}

// The actors that read their flag only when created, and what keeps one from being re-created now.
enum LwKind : u8 { LW_PLAIN, LW_DOOR10, LW_DOOR12, LW_WARPT, LW_STONE2 };
const u8 kChest = 0x1F;     // LwRule::mask of a chest bit (word 0); 0xFF: a switch number
const u8 kAngleX = 0x80;    // LwRule::shift: the key is in home.angle.x, not the params
const u8 kMemOnly = 0x40;   // memory switches only (a door's setKey reads its switch with room -1)
const u8 kShift = 0x1F;

struct LwRule {
    s16 proc;
    u8 shift;
    u8 mask;
    LwKind kind;
    u8 near;  // != 0: not while the local Link is within near * 10 units (XZ)
};

// puppet_liveworld.c l_lwRules, with its reasons (docs/live-world.md in ww-online).
const LwRule kRules[] = {
    {fpcNm_TBOX_e, 7, kChest, LW_PLAIN, 0},
    {fpcNm_WALL_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_FLOOR_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_Obj_Ice_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_OBJ_MJDOOR_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_Obj_Mkiek_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_Stone2_e, 8, 0xFF, LW_STONE2, 0},
    {fpcNm_SAKU_e, 8, 0xFF, LW_PLAIN, 0},
    {fpcNm_SAKU_e, 16, 0xFF, LW_PLAIN, 0},
    {fpcNm_SWHIT0_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_DOOR10_e, kMemOnly, 0xFF, LW_DOOR10, 25},
    {fpcNm_DOOR12_e, kMemOnly, 0xFF, LW_DOOR12, 25},
    {fpcNm_OBJ_WARPT_e, kAngleX, 0xFF, LW_WARPT, 12},
    {fpcNm_SS_e, 24, 0xFF, LW_PLAIN, 0},
    {fpcNm_Obj_Mkie_e, 16, 0xFF, LW_PLAIN, 0},
    {fpcNm_Obj_MknjD_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_Obj_Vfan_e, 0, 0xFF, LW_PLAIN, 0},
    {fpcNm_Obj_Leaves_e, 13, 0xFF, LW_PLAIN, 0},
    {fpcNm_Obj_Vmc_e, 8, 0xFF, LW_PLAIN, 30},
    {fpcNm_KOKIIE_e, 24, 0xFF, LW_PLAIN, 100},
    {fpcNm_MFLFT_e, 24, 0xFF, LW_PLAIN, 50},
};

struct LwPass {
    const TwwWorldLiveBatch* batch;
    u32 count;
    u32 pending;
    fopAc_ac_c* hits[kMaxHits];
};

// Nothing to do: no lock / lid showing; a door in Init (its actionInit re-runs setKey itself); a door12 whose
// create clears a temp event bit; a jar that is open, or a three-way dungeon jar (its lid is an event register).
bool nothingToDo(fopAc_ac_c* ac, LwKind kind) {
    switch (kind) {
    case LW_DOOR10: {
        daDoor10_c* door = static_cast<daDoor10_c*>(ac);
        return !door->mKeyLock.mbEnabled || door->m354 == 0;
    }
    case LW_DOOR12: {
        daDoor12_c* door = static_cast<daDoor12_c*>(ac);
        return !door->mKeyLock.mbEnabled || door->m314 == 0 || ((ac->home.angle.z >> 8) & 0xFF) == 8;
    }
    case LW_WARPT: {
        daObj_Warpt_c* jar = static_cast<daObj_Warpt_c*>(ac);
        return !jar->m2C6 || jar->m290 == 0 || (fopAcM_GetParam(ac) & 0xF) - 2u <= 2;
    }
    default:
        return false;
    }
}

// Not now: a door not in Wait, a jar whose lid is burning, a boulder carried or in the air.
bool busy(fopAc_ac_c* ac, LwKind kind) {
    switch (kind) {
    case LW_DOOR10:
        return static_cast<daDoor10_c*>(ac)->m354 != kDoorIdle;
    case LW_DOOR12:
        return static_cast<daDoor12_c*>(ac)->m314 != kDoorIdle;
    case LW_WARPT:
        return static_cast<daObj_Warpt_c*>(ac)->m290 != kWarptClosed;
    case LW_STONE2:
        return static_cast<daStone2::Act_c*>(ac)->m648 != 0;
    default:
        return false;
    }
}

bool nearPlayer(fopAc_ac_c* ac, u32 near) {
    fopAc_ac_c* player = localPlayer();
    if (player == NULL) {
        return true;
    }
    const f32 dx = ac->current.pos.x - player->current.pos.x;
    const f32 dz = ac->current.pos.z - player->current.pos.z;
    const f32 d = (f32)(near * 10);
    return dx * dx + dz * dz < d * d;
}

// Collects the actors whose create-time flag is in the batch. Never deletes while iterating.
void* judgeLive(void* proc, void* data) {
    LwPass* p = static_cast<LwPass*>(data);
    fopAc_ac_c* ac = static_cast<fopAc_ac_c*>(proc);
    if (ac->mInitState == kInitStateDeleting) {
        return NULL;
    }
    const u32 prm = fopAcM_GetParam(ac);
    const s32 room = ac->home.roomNo;
    for (const LwRule& r : kRules) {
        if (r.proc != fopAcM_GetName(ac)) {
            continue;
        }
        const u32 src = (r.shift & kAngleX) != 0 ? (u16)ac->home.angle.x : prm;
        const u32 key = (src >> (r.shift & kShift)) & r.mask;
        u32 word = 0;
        if (r.mask == kChest) {
            if ((prm & 0x7F) >= 7) {
                continue; // funcs 7 / 8 read another stage's chest bits
            }
        } else {
            word = 1 + (key >> 5);
            // 0xFF = none and 0xF0-0xFE are no switches; zone switches are one room's
            if (key >= 0xF0 || ((r.shift & kMemOnly) != 0 && key >= 0x80) ||
                (key >= 0xC0 && room != p->batch->zone_room)) {
                continue;
            }
        }
        if (((p->batch->bits[word] >> (key & 31)) & 1) == 0) {
            continue;
        }
        const bool init = fopAcM_CheckCondition(ac, fopAcCnd_INIT_e) != 0;
        if (init && nothingToDo(ac, r.kind)) {
            break;
        }
        const u8 roomFlags =
            room >= 0 && room < (s32)ARRAY_SIZE(dStage_roomControl_c::mStatus) ? dStage_roomControl_c::mStatus[room].mFlags : kRoomLoaded;
        if (ac->mCreateResult != cPhs_NEXT_e || !init || p->count >= kMaxHits ||
            (roomFlags & (kRoomLoaded | kRoomBusy | kRoomHidden)) != kRoomLoaded || busy(ac, r.kind) ||
            (r.near != 0 && nearPlayer(ac, r.near))) {
            p->pending++; // its create may have read the flag before the bit landed, or not now: the batch waits
        } else {
            p->hits[p->count++] = ac;
        }
        break; // one action per actor
    }
    return NULL;
}

// Deletes the actor and creates it again from its own create params in its own layer (an actor placed in a room
// lives in that room's layer; one created in the current layer would outlive the room). Only if the delete was
// accepted: two chests would give the item twice.
bool recreate(fopAc_ac_c* ac) {
    layer_class* layer = ac->mLyTg.mpLayer;
    const s16 proc = fopAcM_GetName(ac);
    const u32 prm = fopAcM_GetParam(ac);
    cXyz pos = ac->home.pos;
    csXyz angle = ac->home.angle;
    cXyz scale = ac->scale;
    const s8 room = ac->home.roomNo;
    const s8 argument = ac->argument;
    if (layer == NULL || !fopAcM_delete(ac)) {
        return false;
    }
    layer_class* saved = fpcLy_CurrentLayer();
    fpcLy_SetCurrentLayer(layer);
    const fpc_ProcID id = fopAcM_create(proc, prm, &pos, room, &angle, &scale, argument);
    fpcLy_SetCurrentLayer(saved);
    fprintf(stderr, "[wwo] world: live world %s proc 0x%03x prm 0x%08x room %d at (%.0f %.0f %.0f)\n",
            id != fpcM_ERROR_PROCESS_ID_e ? "re-created" : "NOT re-created", (u16)proc, prm, room, pos.x, pos.y, pos.z);
    return id != fpcM_ERROR_PROCESS_ID_e;
}

// One batch per pass: never while an event runs or is ordered (an order holds actor pointers), never for another
// stage's batch (the client posts one for this stage).
void liveWorld(s32 slot) {
    TwwWorldLiveStatus& st = sLive.status;
    if (st.seq == st.done_seq || sLive.batch.slot != slot) {
        return;
    }
    LwPass p = {};
    p.batch = &sLive.batch;
    fopAcIt_Judge(judgeLive, &p);
    if (p.pending != 0 && sLive.wait < kMaxWait) {
        sLive.wait++;
        return; // all together once they are ready
    }
    for (u32 i = 0; i < p.count; i++) {
        if (recreate(p.hits[i])) {
            st.poke_count++;
        } else {
            p.pending++;
        }
    }
    st.retry = p.pending;
    sLive.wait = 0;
    st.done_seq = st.seq;
}

} // namespace

void wwo_world_frame() {
    if ((pc_frame_count() & 3) != 0) {
        return;
    }
    const s32 slot = currentSlot();
    if (slot < 0 || dMenu_flag() != 0) {
        return;
    }
    despawnItems(slot);
    if (!dComIfGp_event_runCheck() && g_dComIfG_gameInfo.play.getEvent()->mOrderCount == 0) {
        liveWorld(slot);
    }
}

extern "C" {

int32_t tww_world_save_slot(void) {
    return currentSlot();
}

int32_t tww_world_room_zone(int32_t room) {
    if (room < 0 || room >= (s32)ARRAY_SIZE(dStage_roomControl_c::mStatus)) {
        return -1;
    }
    return dStage_roomControl_c::getZoneNo(room);
}

uint32_t tww_world_salvage_read(TwwWorldSalvagePoint* out, uint32_t max) {
    // The control lives in the salvage actor's heap: the pointer outlives it until the next play scene.
    dSalvage_control_c* ctl = daSalvage_c::mTagData_p;
    if (ctl == NULL || fopAcM_SearchByName(fpcNm_Salvage_e) == NULL) {
        return 0;
    }
    const uint32_t n = max < dSalvage_control_c::MAX_TOTAL ? max : dSalvage_control_c::MAX_TOTAL;
    for (uint32_t i = 0; i < n; i++) {
        const dSalvage_info_c& info = ctl->mInfo[i];
        out[i] = TwwWorldSalvagePoint{info.mRoomNo, info.mSaveNo, info.mKind, info.mFlag};
    }
    return n;
}

int tww_world_salvage_set_flag(uint32_t index, uint8_t bits) {
    if (daSalvage_c::mTagData_p == NULL || fopAcM_SearchByName(fpcNm_Salvage_e) == NULL ||
        index >= dSalvage_control_c::MAX_TOTAL) {
        return 0;
    }
    daSalvage_c::mTagData_p->setFlag(index, bits);
    return 1;
}

void tww_world_set_remote_items(int32_t slot, uint32_t mask) {
    sDespawn.slot = slot;
    sDespawn.mask = mask;
}

void tww_world_get_stats(TwwWorldStats* out) {
    *out = sDespawn.stats;
}

void tww_world_live_post(const TwwWorldLiveBatch* batch) {
    sLive.batch = *batch;
    sLive.status.seq = batch->seq;
    sLive.wait = 0;
}

void tww_world_live_status(TwwWorldLiveStatus* out) {
    *out = sLive.status;
}

} // extern "C"
