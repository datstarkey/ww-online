// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// Each puppet's body (a solid cylinder the local Link can't walk through, always) and PVP (tww.h, "pvp"): the
// taking-part peers' puppets as L-target battle targets whose body is also a hurtbox, the local Link's hits
// on them reported, and a peer's hit on the local Link applied as that same attack made by the peer's puppet here: an
// AT sphere over the local Link for one frame, so the CC pass and daPy_lk_c::changeDamageProc do the rest as for an
// enemy's (the damage from the attack's power, guard, knockback away from the puppet, invincibility frames, death).
// Victim-authoritative: a hit counts only once the victim's own game lands it.
//
// Both colliders belong to the puppet (its stts, weight 0xFF: the CC pass pushes only the other side, as the Dolphin
// REL's setCollision did with the puppet's own mCyl), registered from wwo_frame before the frame's executes; the CC
// pass runs in the play scene's draw, and the body's hit callback records a hit there, while the attacker still exists.
// COS_MP_TEST_PVP=1 (dev only, with COS_MP_TEST_PEER=1): slot 0 takes part, and every hit the local Link lands on it
// is logged and dealt back to him from it 30 frames later.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/actor/d_a_bomb.h"
#include "d/actor/d_a_player_main.h"
#include "d/d_cc_d.h"
#include "d/d_cc_uty.h"
#include "d/d_com_inf_game.h"
#include "d/d_meter.h"

#include "ww_online_internal.h"
#include "ww_overlay.h"

#include <cmath>
#include <cstddef>
#include <cstdio>
#include <cstdlib>
#include <cstring>

static_assert(sizeof(TwwPvpHit) == 32 && offsetof(TwwPvpHit, at_type) == 4 && offsetof(TwwPvpHit, pos) == 8 &&
              offsetof(TwwPvpHit, attacker_pos) == 20, "TwwPvpHit: the C# binding's layout");

namespace {

const u32 kLinkAtTypes = AT_TYPE_SWORD | AT_TYPE_BOMB | AT_TYPE_BOOMERANG | AT_TYPE_NORMAL_ARROW | AT_TYPE_HOOKSHOT |
                         AT_TYPE_SKULL_HAMMER | AT_TYPE_FIRE_ARROW | AT_TYPE_ICE_ARROW | AT_TYPE_LIGHT_ARROW;
const u32 kQueueMax = 16;
const u16 kHitCooldown = 10; // frames after a reported hit on a puppet before the next one counts
const u16 kApplyWait = 60;   // frames a peer's hit waits for their puppet and the local Link in play
const u16 kTestEcho = 30;    // COS_MP_TEST_PVP: frames before the test peer deals a hit back
const u16 kFlashFrames = 30; // the local flash on a puppet the local Link hit, until the victim's own comes back
const f32 kMaxCoord = 1.0e6f;
const u32 kArrowShotParam = 1; // fopAcM_SetParam(arrow, 1) at the shot (d_a_player_bow.inc) until it hits something

// The body: the player's body cylinder (l_cyl_src in d_a_player_main_data.inc): its Co pushes the local Link off; its tg
// (set only while the slot takes part) is in the enemy group, which is what Link's sword, arrows, bombs, boomerang and
// hookshot hit.
dCcD_SrcCyl l_tg_src = {
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ 0,
        /* SrcObjTg  Type    */ ~(AT_TYPE_WATER | AT_TYPE_UNK20000 | AT_TYPE_WIND | AT_TYPE_UNK400000 | AT_TYPE_LIGHT),
        /* SrcObjTg  SPrm    */ cCcD_TgSPrm_Set_e | cCcD_TgSPrm_IsEnemy_e,
        /* SrcObjCo  SPrm    */ cCcD_CoSPrm_Set_e | cCcD_CoSPrm_IsEnemy_e | cCcD_CoSPrm_VsGrpAll_e,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ 0,
        /* SrcGObjAt Spl     */ 0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ 0,
        /* SrcGObjTg HitMark */ dCcG_TgHitMark_Unk1_e,
        /* SrcGObjTg Spl     */ 0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ dCcG_TgSPrm_NoConHit_e,
        /* SrcGObjCo SPrm    */ 0,
    },
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 30.0f,
        /* Height */ 125.0f,
    }},
};

// A peer's hit, around the local Link's body; it hits the player group only.
dCcD_SrcSph l_at_src = {
    {
        /* Flags             */ 0,
        /* SrcObjAt  Type    */ 0,
        /* SrcObjAt  Atp     */ 0,
        /* SrcObjAt  SPrm    */ cCcD_AtSPrm_Set_e | cCcD_AtSPrm_VsPlayer_e,
        /* SrcObjTg  Type    */ 0,
        /* SrcObjTg  SPrm    */ 0,
        /* SrcObjCo  SPrm    */ 0,
        /* SrcGObjAt Se      */ 0,
        /* SrcGObjAt HitMark */ dCcG_AtHitMark_Nrm_e,
        /* SrcGObjAt Spl     */ 0,
        /* SrcGObjAt Mtrl    */ 0,
        /* SrcGObjAt SPrm    */ 0,
        /* SrcGObjTg Se      */ 0,
        /* SrcGObjTg HitMark */ 0,
        /* SrcGObjTg Spl     */ 0,
        /* SrcGObjTg Mtrl    */ 0,
        /* SrcGObjTg SPrm    */ 0,
        /* SrcGObjCo SPrm    */ 0,
    },
    {{
        /* Center */ {0.0f, 0.0f, 0.0f},
        /* Radius */ 40.0f,
    }},
};

struct Queue {
    TwwPvpHit e[kQueueMax];
    u16 age[kQueueMax];
    u32 read = 0;
    u32 write = 0;

    bool push(const TwwPvpHit& hit, u16 wait = 0) {
        if (write - read >= kQueueMax) {
            return false;
        }
        age[write % kQueueMax] = wait;
        e[write++ % kQueueMax] = hit;
        return true;
    }
    bool pop(TwwPvpHit* out) {
        if (read == write) {
            return false;
        }
        *out = e[read++ % kQueueMax];
        return true;
    }
};

struct Slot {
    fpc_ProcID puppet = fpcM_ERROR_PROCESS_ID_e; // the puppet the colliders are bound to
    dCcD_Stts stts;
    dCcD_Cyl tg;
    dCcD_Sph at;
    u16 cooldown = 0;
    u16 flash = 0;
    bool landed = false; // the body's tg callback: the local Link hit it in the last CC pass
    TwwPvpHit hit = {};
};

Slot sSlots[TWW_MAX_PEERS];
u32 sMask;
Queue sOut;
Queue sIn;
Queue sEcho;
int sTest = -1;
int sTook = -1;   // the slot whose hit fired last: how the local Link took it is logged once his execute has seen it
u8 sTookWait = 0; // the CC pass lands it at the end of the frame, his next execute takes it

bool inRange(f32 v) {
    return v > -kMaxCoord && v < kMaxCoord; // false for NaN and infinity
}

fopAc_ac_c* linkPlayer() {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    return player != NULL && fopAcM_SearchByName(fpcNm_PLAYER_e) == player ? player : NULL;
}

// The local Link's own attack, or his projectile's; never a peer's copy (ww_overlay_fx.cpp), an enemy's or the world's.
bool fromLocalLink(fopAc_ac_c* ac) {
    if (ac == NULL) {
        return false;
    }
    switch (fopAcM_GetName(ac)) {
    case fpcNm_PLAYER_e:
        return ac == dComIfGp_getLinkPlayer();
    case fpcNm_ARROW_e:
        return fopAcM_GetParam(ac) == kArrowShotParam; // a peer's copy keeps 0
    case fpcNm_BOMB_e:
        return !WwoOverlay::isCopy(static_cast<daBomb_c*>(ac));
    case fpcNm_Bomb2_e:
    case fpcNm_BOOMERANG_e:
    case fpcNm_HOOKSHOT_e:
        return true;
    default:
        return false;
    }
}

void onHurtboxHit(fopAc_ac_c* puppet, dCcD_GObjInf* tg, fopAc_ac_c* attacker, dCcD_GObjInf* at) {
    Slot* slot = NULL;
    for (Slot& s : sSlots) {
        if (&s.tg == tg) {
            slot = &s;
        }
    }
    const u32 type = at->GetAtType() & kLinkAtTypes;
    fopAc_ac_c* link = dComIfGp_getLinkPlayer();
    if (slot == NULL || slot->landed || slot->cooldown > 0 || type == 0 || at->GetAtAtp() == 0 ||
        !fromLocalLink(attacker) || link == NULL) {
        return;
    }
    TwwPvpHit& hit = slot->hit;
    hit.slot = (u8)(slot - sSlots);
    hit.damage = (u8)(at->GetAtAtp() > TWW_PVP_MAX_DAMAGE ? TWW_PVP_MAX_DAMAGE : at->GetAtAtp());
    hit.spl = (u8)(at->GetAtSpl() > TWW_PVP_SPL_MAX ? 0 : at->GetAtSpl());
    hit.pad0 = 0;
    hit.at_type = type;
    const cXyz* pos = tg->GetTgHitPosP();
    hit.pos[0] = pos->x;
    hit.pos[1] = pos->y;
    hit.pos[2] = pos->z;
    hit.attacker_pos[0] = link->current.pos.x;
    hit.attacker_pos[1] = link->current.pos.y;
    hit.attacker_pos[2] = link->current.pos.z;
    slot->landed = true;
    fopAcM_seStart(puppet, at_se_get(at), 0x20);
}

void bind(Slot& s, fopAc_ac_c* puppet) {
    s.puppet = fopAcM_GetID(puppet);
    s.stts.Init(0xFF, 0xFF, puppet);
    s.tg.Set(l_tg_src);
    s.tg.SetStts(&s.stts);
    s.tg.SetTgHitCallback(onHurtboxHit);
    s.at.Set(l_at_src);
    s.at.SetStts(&s.stts);
    s.cooldown = 0;
    s.landed = false;
}

// The lock-on cursor over the puppet's head; eyePos also aims the guard (dCcS::ChkShieldFrontRange).
void setAttention(fopAc_ac_c* puppet, int slot, bool target) {
    puppet->attention_info.flags = target ? fopAc_Attn_LOCKON_BATTLE_e : 0;
    puppet->attention_info.distances[fopAc_Attn_TYPE_BATTLE_e] = 3;
    cXyz head;
    if (!wwo_puppetHeadPos(slot, &head)) {
        head = puppet->current.pos;
        head.y += 125.0f;
    }
    puppet->eyePos = head;
    puppet->eyePos.y -= 20.0f;
    puppet->attention_info.position = head;
    puppet->attention_info.position.y += 30.0f;
}

void reportLanded(Slot& s) {
    s.landed = false;
    s.cooldown = kHitCooldown;
    s.flash = kFlashFrames;
    const TwwPvpHit& hit = s.hit;
    fprintf(stderr, "[wwo] pvp: hit slot %u (type 0x%X, %u quarter hearts, spl %u) at (%.1f %.1f %.1f)\n", hit.slot,
            hit.at_type, hit.damage, hit.spl, hit.pos[0], hit.pos[1], hit.pos[2]);
    if (sTest == 1 && hit.slot == 0) {
        TwwPvpHit back = hit;
        const cXyz* from = &wwo_puppetActor(0)->current.pos;
        back.attacker_pos[0] = from->x;
        back.attacker_pos[1] = from->y;
        back.attacker_pos[2] = from->z;
        sEcho.push(back, kTestEcho);
        return;
    }
    if (!sOut.push(hit)) {
        fprintf(stderr, "[wwo] pvp: hit on slot %u dropped (the client is not polling)\n", hit.slot);
    }
}

// A peer's hit as their puppet's attack on the local Link, this frame. False: not yet (no puppet, no Link in play).
bool fire(const TwwPvpHit& hit) {
    fopAc_ac_c* link = linkPlayer();
    fopAc_ac_c* puppet = wwo_puppetActor(hit.slot);
    if (link == NULL || puppet == NULL || dMenu_flag() || sSlots[hit.slot].puppet != fopAcM_GetID(puppet)) {
        return false;
    }
    Slot& s = sSlots[hit.slot];
    // Away from the attacker: getDamageVec takes the at's vector as the knockback when it has one.
    cXyz away(link->current.pos.x - hit.attacker_pos[0], 0.0f, link->current.pos.z - hit.attacker_pos[2]);
    const f32 len = away.absXZ();
    away = len > 1.0f ? away * (10.0f / len) : cXyz(0.0f, 0.0f, 0.0f);
    s.at.SetAtType(hit.at_type);
    s.at.SetAtAtp(hit.damage);
    s.at.SetAtSpl((dCcG_At_Spl)hit.spl);
    s.at.SetAtVec(away);
    cXyz center = link->current.pos;
    center.y += 60.0f;
    s.at.SetC(center);
    dComIfG_Ccsp()->Set(&s.at);
    sTook = hit.slot;
    sTookWait = 2;
    fprintf(stderr, "[wwo] pvp: slot %u hits the local Link (type 0x%X, %u quarter hearts, spl %u), life %d\n",
            hit.slot, hit.at_type, hit.damage, hit.spl, dComIfGs_getLife());
    return true;
}

// Each queued hit fires once its puppet and the local Link are there, at most one per slot a frame; a hit that waited
// kApplyWait frames is dropped.
void fireQueued() {
    bool fired[TWW_MAX_PEERS] = {};
    const u32 n = sIn.write - sIn.read;
    for (u32 i = 0; i < n; i++) {
        const u16 age = sIn.age[sIn.read % kQueueMax];
        TwwPvpHit hit;
        sIn.pop(&hit);
        if ((sMask & (1u << hit.slot)) == 0) {
            continue;
        }
        if (!fired[hit.slot] && fire(hit)) {
            fired[hit.slot] = true;
        } else if (age < kApplyWait) {
            sIn.push(hit, age + 1);
        } else {
            fprintf(stderr, "[wwo] pvp: hit from slot %u dropped (no puppet or no Link in play)\n", hit.slot);
        }
    }
}

// COS_MP_TEST_PVP: the test peer's hits back, each once its delay has run out.
void echoQueued() {
    const u32 n = sEcho.write - sEcho.read;
    for (u32 i = 0; i < n; i++) {
        const u16 wait = sEcho.age[sEcho.read % kQueueMax];
        TwwPvpHit hit;
        sEcho.pop(&hit);
        if (wait > 0) {
            sEcho.push(hit, wait - 1);
        } else {
            sIn.push(hit);
        }
    }
}

} // namespace

bool wwo_pvpFlashing(int slot) {
    return slot >= 0 && slot < TWW_MAX_PEERS && sSlots[slot].flash > 0;
}

void wwo_pvpFrame() {
    if (sTest < 0) {
        const char* v = getenv("COS_MP_TEST_PVP");
        sTest = v != NULL && strcmp(v, "1") == 0 ? 1 : 0;
    }
    if (sTest == 1) {
        sMask |= 1;
    }
    const bool inPlay = linkPlayer() != NULL && !dComIfGp_isEnableNextStage();
    if (sTook >= 0 && --sTookWait == 0) {
        if (inPlay) {
            fprintf(stderr, "[wwo] pvp: the local Link took slot %d's hit: proc %d, life %d\n", sTook,
                    wwo_playerProc(static_cast<daPy_lk_c*>(linkPlayer())), dComIfGs_getLife());
        }
        sTook = -1;
    }
    for (int i = 0; i < TWW_MAX_PEERS; i++) {
        Slot& s = sSlots[i];
        fopAc_ac_c* puppet = wwo_puppetActor(i);
        const bool part = inPlay && (sMask & (1u << i)) != 0;
        if (puppet == NULL) {
            s.puppet = fpcM_ERROR_PROCESS_ID_e;
            s.landed = false;
            s.flash = 0;
            continue;
        }
        if (s.puppet != fopAcM_GetID(puppet)) {
            bind(s, puppet);
        }
        if (s.landed) {
            if (part) {
                reportLanded(s);
            }
            s.landed = false;
        }
        if (s.cooldown > 0) {
            s.cooldown--;
        }
        if (s.flash > 0) {
            s.flash--;
        }
        s.stts.Move();
        s.stts.ClrCcMove(); // the peer's position is the network's
        setAttention(puppet, i, part);
        if (part) {
            s.tg.OnTgSetBit();
        } else {
            s.tg.OffTgSetBit();
        }
        if (inPlay) {
            s.tg.SetC(puppet->current.pos);
            dComIfG_Ccsp()->Set(&s.tg);
        }
    }
    if (!inPlay) {
        sIn.read = sIn.write;
        sEcho.read = sEcho.write;
        return;
    }
    echoQueued();
    fireQueued();
}

extern "C" {

void tww_pvp_set_slots(uint32_t slot_mask) {
    sMask = slot_mask & ((1u << TWW_MAX_PEERS) - 1);
}

int tww_pvp_poll_hit(TwwPvpHit* out) {
    return out != NULL && sOut.pop(out) ? 1 : 0;
}

int tww_pvp_apply_hit(const TwwPvpHit* hit) {
    if (hit == NULL || hit->slot >= TWW_MAX_PEERS || (sMask & (1u << hit->slot)) == 0 || hit->damage == 0 ||
        hit->damage > TWW_PVP_MAX_DAMAGE || hit->spl > TWW_PVP_SPL_MAX || hit->at_type == 0 ||
        (hit->at_type & ~kLinkAtTypes) != 0 || !inRange(hit->attacker_pos[0]) || !inRange(hit->attacker_pos[1]) ||
        !inRange(hit->attacker_pos[2]) || !inRange(hit->pos[0]) || !inRange(hit->pos[1]) || !inRange(hit->pos[2])) {
        return 0;
    }
    return sIn.push(*hit) ? 1 : 0;
}

} // extern "C"
