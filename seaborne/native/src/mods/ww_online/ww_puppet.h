// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// ww_puppet.cpp: what a puppet shows of its peer beyond position and the wait / walk / run blend (its anims,
// face, hands, aim, equipment and clothes), and the local Link's to send (tww.h, "Puppet fidelity").
#ifndef WW_PUPPET_H
#define WW_PUPPET_H

#include "dolphin/types.h"
#include "pc/tww.h"

class daPy_lk_c;

// For the length of a PuppetWindow: the peer's equipment in the save's equip slots, and the local player's
// audio state (sword / shield / boots types, sword-using BGM flag) and first-person camera bits kept from the
// puppet's player code (in first person draw hides Link, or his head).
class WwoPuppetScope {
public:
    WwoPuppetScope(daPy_lk_c* puppet, int slot);
    ~WwoPuppetScope();

private:
    bool mEquipSwapped;
    u8 mEquip[3];
    u8 mAudio[6];
    u8 mSwordUsing;
    u32 mCameraBits;
};

// Execute, inside the window, before the anims: equipment, item in hand, boots and clothes.
void wwo_puppetLook(daPy_lk_c* puppet, int slot);
// Execute, inside the window: the peer's own anims, face, hands and aim. False: none (the caller plays the
// wait / walk / run blend). Calls animeUpdate itself when true.
bool wwo_puppetMirror(daPy_lk_c* puppet, int slot);
// Execute, right after playTextureAnime: the peer's face frames.
void wwo_puppetFace(daPy_lk_c* puppet, int slot);
// Draw, inside the window, after Link's: the held boomerang and a carried bomb.
void wwo_puppetDraw(daPy_lk_c* puppet, int slot);
// Create / remove: a clean state for the slot, and its recoloured tunic freed.
void wwo_puppetReset(int slot);
// tww_puppet_set_anim / _set_look's own work: the slot's anims and look the puppet plays now (ww_smooth.cpp hands
// them on when the puppet's drawn time reaches them).
extern "C" void wwo_puppetSetAnim(int slot, const TwwPuppetAnim* anim);
extern "C" void wwo_puppetSetLook(int slot, const TwwPuppetLook* look);
// wwo_frame: the local Link's outfit (tww_puppet_set_local_outfit).
void wwo_puppetLocalOutfit();

#endif
