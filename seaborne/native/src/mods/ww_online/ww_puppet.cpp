// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// Puppet fidelity (tww.h, "Puppet fidelity"; the Dolphin REL's puppet_anmmirror.c, puppet_held.c and
// puppet_appearance.c). A puppet plays its peer's own anims: the bcks, frames, rates and blend ratios of
// the peer's body tracks, loaded into the puppet's anm heaps as getAnimeResource does, with its face btp /
// btk and hand shapes; its proc stays WAIT, so no proc code runs for it. Its equipment is the peer's (the
// save's equip slots swapped in around its player code), the item in hand is built by the stock model
// setters (never makeItemType's actor items: they bind to the local Link), and its clothes and tunic colour
// are its own: each puppet has its own Link.arc, so its linktexS3TC header is its own.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "m_Do/m_Do_audio.h"
#include "m_Do/m_Do_ext.h"
#include "d/actor/d_a_player_main.h"
#include "d/actor/d_a_player_main_data.h"
#include "d/d_com_inf_game.h"
#include "JSystem/J3DGraphLoader/J3DAnmLoader.h"
#include "JSystem/JKernel/JKRArchive.h"
#include "JSystem/JKernel/JKRExpHeap.h"

#include "ww_online_internal.h"
#include "ww_puppet.h"

#include <cstddef>
#include <cstring>

static_assert(sizeof(TwwPuppetTrack) == 24 && offsetof(TwwPuppetTrack, frame) == 12 &&
              offsetof(TwwPuppetTrack, ratio) == 20, "TwwPuppetTrack: tww.h layout");
static_assert(sizeof(TwwPuppetAnim) == 140 && offsetof(TwwPuppetAnim, track) == 4 &&
              offsetof(TwwPuppetAnim, face_btp) == 124 && offsetof(TwwPuppetAnim, hand_l) == 132 &&
              offsetof(TwwPuppetAnim, body_angle) == 134, "TwwPuppetAnim: tww.h layout");
static_assert(sizeof(TwwPuppetLook) == 16 && offsetof(TwwPuppetLook, tunic) == 8 &&
              offsetof(TwwPuppetLook, equip_item) == 12 && offsetof(TwwPuppetLook, grab_fuse) == 15,
              "TwwPuppetLook: tww.h layout");

namespace {

enum { kUnder0, kUnder1, kUpper0, kUpper1, kUpper2 };

// The four body tracks share one 0xB400 buffer (createHeap, d_a_player_main.cpp: under[0] +0, under[1]
// +0x2400, upper[i] +0x4800 + i * 0x2400): a load may only fill the region the tracks before it left alone,
// and upper[2] (+0x9000) is the item track's.
const u32 kRegionUnder1 = 0x2400;
const u32 kRegionUpper0 = 0x4800;
const u32 kRegionUpper1 = 0x6C00;
const u32 kRegionUpper2 = 0x9000;
const u32 kTrackSize = 0x2400;
const u16 kMorfJoints = 0x2A;  // setSingleMoveAnime's old-frame morf: every CL joint
const f32 kSnapFrames = 4.0f;  // drift from the peer's frame before it snaps
const f32 kMaxRate = 100.0f;

struct Input {
    bool anim;
    bool look;
    TwwPuppetAnim a;
    TwwPuppetLook l;
};

struct Mirror {
    bool active;
    u8 face;          // bit 0: the peer's btp, bit 1: their btk
    u8 hands[2];      // WAIT's, put back when the mirror ends
    bool bigLeaf;     // the item in hand is the swung leaf (setFanModel), not the held one
    u16 want[TWW_PUPPET_TRACKS];
    J3DAnmTransform* anm[TWW_PUPPET_TRACKS];
};

// The held boomerang and the carried bomb: the real ones are actors bound to their own Link, so the puppet
// draws these from its archive, in a solid heap of its own (the actor heap is playerInit's).
struct Held {
    JKRSolidHeap* heap;
    J3DModel* boomerang;
    J3DModel* bomb;
    bool failed;
};

// A Link's clothes texture: the linktexS3TC header, the hero image's own offset and a recoloured copy.
struct Outfit {
    ResTIMG* header;
    u32 heroOffset;
    u8* image;
    u32 rgb;          // in image
};

// linktexS3TC as shipped: 160x96 CMPR (cl.bdl TEX1).
const u32 kTexW = 160;
const u32 kTexH = 96;
const u32 kTexBytes = kTexW * kTexH / 2;
const u32 kGreenMargin = 24;   // an RGB565 endpoint is tunic green when G beats R and B by this much
const u32 kTunicRefLum = 139;  // the main tunic green (90,178,74) shows the picked colour exactly

Input sIn[TWW_MAX_PEERS];
Mirror sMirror[TWW_MAX_PEERS];
Outfit sOutfit[TWW_MAX_PEERS];
Held sHeld[TWW_MAX_PEERS];
daPy_lk_c* sPuppet[TWW_MAX_PEERS]; // set by its execute, cleared by wwo_puppetReset
Outfit sLocalOutfit;
bool sLocalOutfitSet;
u8 sLocalClothes = TWW_PUPPET_CLOTHES_DEFAULT;
u32 sLocalRgb;

bool isBck(u16 i) {
    return i >= dRes_INDEX_LKANM_BCK_ACTIONTAKTRDW_e && i <= dRes_INDEX_LKANM_BCK_YOBU_e;
}

bool isBtp(u16 i) {
    return i >= dRes_INDEX_LKANM_BTP_LINK_FREEZ_e && i <= dRes_INDEX_LKANM_BTP_TWIND_e;
}

bool isBtk(u16 i) {
    return i >= dRes_INDEX_LKANM_BTK_CUTFH_e && i <= dRes_INDEX_LKANM_BTK_YMSLI00_e;
}

u32 rgbOf(const u8* c) {
    const u32 rgb = (u32)c[0] << 16 | (u32)c[1] << 8 | c[2];
    return rgb == (30u << 16 | 100u << 8 | 35u) ? 0 : rgb; // the vanilla tunic
}

f32 clampRatio(f32 r) {
    return !(r >= 0.0f) ? 0.0f : r > 1.0f ? 1.0f : r;
}

u16 be16(const u8* p) {
    return (u16)(p[0] << 8 | p[1]);
}

void putBe16(u8* p, u16 v) {
    p[0] = (u8)(v >> 8);
    p[1] = (u8)v;
}

u32 tintScale(u32 target, u32 lum) {
    const u32 v = target * lum / kTunicRefLum;
    return v > 255 ? 255 : v;
}

u16 tint(u16 c, u32 rgb) {
    u32 r = (c >> 11) & 0x1F, g = (c >> 5) & 0x3F, b = c & 0x1F;
    r = r << 3 | r >> 2;
    g = g << 2 | g >> 4;
    b = b << 3 | b >> 2;
    if (g < (r > b ? r : b) + kGreenMargin) {
        return c;
    }
    const u32 lum = (77 * r + 150 * g + 29 * b) >> 8;
    r = tintScale(rgb >> 16 & 0xFF, lum);
    g = tintScale(rgb >> 8 & 0xFF, lum);
    b = tintScale(rgb & 0xFF, lum);
    return (u16)((r >> 3) << 11 | (g >> 2) << 5 | b >> 3);
}

// A CMPR image recoloured block by block (big-endian, as on the disc). An endpoint's order selects the block's
// mode (c0 > c1: four colours, else three and transparent), so it is kept and the indices remapped on a swap.
void recolour(u8* dst, const u8* src, u32 rgb) {
    for (u32 o = 0; o < kTexBytes; o += 8) {
        const u16 c0 = be16(src + o), c1 = be16(src + o + 2);
        u32 bits;
        memcpy(&bits, src + o + 4, 4);
        u16 n0 = tint(c0, rgb), n1 = tint(c1, rgb);
        if (c0 > c1) {
            if (n0 < n1) {
                const u16 t = n0;
                n0 = n1;
                n1 = t;
                bits ^= 0x55555555; // 0 <-> 1, 2 <-> 3
            } else if (n0 == n1) {
                bits = 0; // every texel n0
            }
        } else if (n0 > n1) {
            const u16 t = n0;
            n0 = n1;
            n1 = t;
            bits ^= (~bits >> 1) & 0x55555555; // 0 <-> 1; 2 (midpoint) and 3 (transparent) stay
        }
        putBe16(dst + o, n0);
        putBe16(dst + o + 2, n1);
        memcpy(dst + o + 4, &bits, 4);
    }
}

// The Link actor once it is created and running (ww_api.cpp's rule).
daPy_lk_c* localLink() {
    fopAc_ac_c* player = dComIfGp_getLinkPlayer();
    if (player == NULL || fopAcM_SearchByName(fpcNm_PLAYER_e) != player) {
        return NULL;
    }
    return static_cast<daPy_lk_c*>(player);
}

} // namespace

struct WwoPuppetLink {
    static daPy_anmHeap_c& heap(daPy_lk_c* p, int t) {
        return t < kUpper0 ? p->m_anm_heap_under[t] : p->m_anm_heap_upper[t - kUpper0];
    }
    static mDoExt_AnmRatioPack& pack(daPy_lk_c* p, int t) {
        return t < kUpper0 ? p->mAnmRatioUnder[t] : p->mAnmRatioUpper[t - kUpper0];
    }
    static J3DFrameCtrl& frameCtrl(daPy_lk_c* p, int t) {
        return t < kUpper0 ? p->mFrameCtrlUnder[t] : p->mFrameCtrlUpper[t - kUpper0];
    }

    // getAnimeResource for track t, reading at most cap bytes, and only a whole bck. Its size, or 0.
    static u32 loadBck(daPy_lk_c* p, int t, u16 idx, u32 cap, J3DAnmTransform** out) {
        daPy_anmHeap_c& h = heap(p, t);
        JKRArchive* arc = dComIfGp_getAnmArchive();
        *out = NULL;
        if (!isBck(idx) || h.m_buffer == NULL || h.mpAnimeHeap == NULL || arc == NULL) {
            return 0;
        }
        const u8* b = static_cast<const u8*>(h.m_buffer);
        if (JKRReadIdxResource(h.m_buffer, cap, idx, arc) < 0x20 || memcmp(b, "J3D1bck1", 8) != 0) {
            return 0;
        }
        const u32 size = (u32)be16(b + 8) << 16 | be16(b + 10);
        if (size < 0x20 || size > cap) {
            return 0;
        }
        JKRHeap* old = p->setAnimeHeap(h.mpAnimeHeap);
        J3DAnmTransform* anm = static_cast<J3DAnmTransform*>(J3DAnmLoaderDataBase::load(h.m_buffer));
        mDoExt_setCurrentHeap(old);
        if (anm == NULL) {
            return 0;
        }
        h.mIdx = idx;
        h.field_0x2 = 0xFFFF;
        *out = anm;
        return size;
    }

    // Track t's frame control as the peer's; the frame snaps on a load or once the puppet's own playback
    // drifted off. A frame < 0 is unknown: the track plays on from where a load put it.
    static void syncTrack(daPy_lk_c* p, int t, J3DAnmTransform* anm, const TwwPuppetTrack& tr, bool snap) {
        J3DFrameCtrl& fc = frameCtrl(p, t);
        const s16 max = anm->getFrameMax();
        s16 end = tr.end, start = tr.start, loop = tr.loop;
        u8 attr = tr.attr;
        f32 rate = tr.rate, frame = tr.frame;
        if (end <= 0 || end > max) {
            end = max;
        }
        if (start < 0 || start > end) {
            start = 0;
        }
        if (loop < 0 || loop > end) {
            loop = rate < 0.0f ? end : start;
        }
        if (attr > J3DFrameCtrl::EMode_LOOP_REVERSE) {
            attr = anm->getAttribute();
        }
        if (!(rate >= -kMaxRate && rate <= kMaxRate)) {
            rate = 0.0f;
        }
        const bool known = frame >= 0.0f;
        if (!known) {
            frame = rate < 0.0f ? end : start;
        }
        if (!(frame >= 0.0f)) {
            frame = 0.0f;
        } else if (frame > end) {
            frame = end;
        }
        fc.setAttribute(attr);
        fc.setStart(start);
        fc.setEnd(end);
        fc.setLoop(loop);
        fc.setRate(rate);
        f32 d = fc.getFrame() - frame;
        if ((attr == J3DFrameCtrl::EMode_LOOP || attr == J3DFrameCtrl::EMode_LOOP_REVERSE) && end > loop) {
            const f32 len = end - loop; // the short way round
            if (d > len * 0.5f) {
                d -= len;
            } else if (d < -len * 0.5f) {
                d += len;
            }
        }
        if (snap || (known && (d > kSnapFrames || d < -kSnapFrames))) {
            fc.setFrame(frame);
            anm->setFrame(frame);
        }
    }

    // Frames and ratios of the loaded tracks. An upper track playing its under track's anim has no frame
    // control of its own (animeUpdate skips it); without a blend partner the ratios are 1 / 0.
    static void follow(daPy_lk_c* p, Mirror& m, const TwwPuppetAnim& a, bool snap) {
        for (int t = kUnder0; t <= kUpper1; t++) {
            J3DAnmTransform* anm = m.anm[t];
            if (anm != NULL && (t < kUpper0 || anm != m.anm[t - kUpper0])) {
                syncTrack(p, t, anm, a.track[t], snap);
            }
        }
        pack(p, kUnder0).setRatio(m.anm[kUnder1] ? clampRatio(a.track[kUnder0].ratio) : 1.0f);
        pack(p, kUnder1).setRatio(m.anm[kUnder1] ? clampRatio(a.track[kUnder1].ratio) : 0.0f);
        pack(p, kUpper0).setRatio(m.anm[kUpper1] ? clampRatio(a.track[kUpper0].ratio) : 1.0f);
        pack(p, kUpper1).setRatio(m.anm[kUpper1] ? clampRatio(a.track[kUpper1].ratio) : 0.0f);
    }

    static void endMirror(daPy_lk_c* p, Mirror& m) {
        for (int t = 0; t < TWW_PUPPET_TRACKS; t++) {
            heap(p, t).mIdx = 0xFFFF; // the blend reloads its own
        }
        clearUpper2(p);
        p->m34C3 = 0;
        p->mLeftHandIdx = m.hands[0];
        p->mRightHandIdx = m.hands[1];
        m.active = false;
    }

    static void clearUpper2(daPy_lk_c* p) {
        pack(p, kUpper2).setAnmTransform(NULL);
        pack(p, kUpper2).setRatio(0.0f);
        heap(p, kUpper2).mIdx = 0xFFFF;
        frameCtrl(p, kUpper2).init(0);
    }

    // The body tracks; false (back in WAIT's own anims) if under[0] did not load.
    static bool load(daPy_lk_c* p, Mirror& m, const TwwPuppetAnim& a, const u16* want) {
        J3DAnmTransform* anm[4] = {};
        const u16 prevUnder = heap(p, kUnder0).mIdx;
        for (int t = kUnder0; t <= kUpper1; t++) {
            heap(p, t).mIdx = 0xFFFF;
            m.want[t] = want[t];
        }
        const u32 s0 = loadBck(p, kUnder0, want[kUnder0], kRegionUpper2, &anm[kUnder0]);
        if (s0 == 0) {
            p->m34C3 = 0;
            return false;
        }
        u32 s2 = 0;
        if (s0 <= kRegionUnder1) {
            loadBck(p, kUnder1, want[kUnder1], kRegionUpper0 - kRegionUnder1, &anm[kUnder1]);
        }
        if (s0 <= kRegionUpper0) {
            s2 = loadBck(p, kUpper0, want[kUpper0], kRegionUpper2 - kRegionUpper0, &anm[kUpper0]);
        }
        if (s0 <= kRegionUpper1 && s2 <= kRegionUpper1 - kRegionUpper0) {
            loadBck(p, kUpper1, want[kUpper1], kRegionUpper2 - kRegionUpper1, &anm[kUpper1]);
        }
        if (anm[kUpper0] == NULL) {
            anm[kUpper0] = anm[kUnder0];
        }
        if (anm[kUpper1] == NULL) {
            anm[kUpper1] = anm[kUnder1];
        }
        for (int t = kUnder0; t <= kUpper1; t++) {
            pack(p, t).setAnmTransform(anm[t]);
            m.anm[t] = anm[t];
        }
        follow(p, m, a, true);
        if (want[kUnder0] != prevUnder) {
            p->m_old_fdata->initOldFrameMorf(p->m_HIO->mBasic.m.field_0xC, 0, kMorfJoints);
        }
        p->m34C3 = 0; // not a move blend: no step offsets
        return true;
    }

    // upper[2], the item track (aim holds, throws, carrying), over the body's.
    static void upper2(daPy_lk_c* p, Mirror& m, const TwwPuppetTrack& tr) {
        mDoExt_AnmRatioPack& pk = pack(p, kUpper2);
        if (!isBck(tr.bck)) {
            if (pk.getAnmTransform() != NULL) {
                clearUpper2(p);
            }
            m.want[kUpper2] = TWW_PUPPET_ANM_NONE;
            m.anm[kUpper2] = NULL;
            return;
        }
        bool snap = false;
        if (tr.bck != m.want[kUpper2] || pk.getAnmTransform() != m.anm[kUpper2] || m.anm[kUpper2] == NULL) {
            J3DAnmTransform* anm;
            m.want[kUpper2] = tr.bck;
            if (loadBck(p, kUpper2, tr.bck, kTrackSize, &anm) == 0) {
                clearUpper2(p);
                m.anm[kUpper2] = NULL;
                return;
            }
            pk.setAnmTransform(anm);
            m.anm[kUpper2] = anm;
            snap = true;
        }
        pk.setRatio(clampRatio(tr.ratio));
        syncTrack(p, kUpper2, m.anm[kUpper2], tr, snap);
    }

    // setDrawHandModel shows hands.bdl joints mLeftHandIdx / mRightHandIdx without a range check.
    static void hands(daPy_lk_c* p, const TwwPuppetAnim& a) {
        J3DModel* model = p->mpHandsModel;
        if (model == NULL) {
            return;
        }
        const u16 n = model->getModelData()->getJointNum();
        if (a.hand_l < n) {
            p->mLeftHandIdx = a.hand_l;
        }
        if (a.hand_r < n) {
            p->mRightHandIdx = a.hand_r;
        }
    }

    // The face btp / eye btk, loaded as setTextureAnime does, unless a priority or cutscene one holds the
    // face. setTextureScrollResource restarts daPy_matAnm_c's blink, statics the local Link shares.
    static void face(daPy_lk_c* p, Mirror& m, const TwwPuppetAnim& a) {
        m.face = 0;
        daPy_anmHeap_c& tp = p->m_tex_anm_heap;
        daPy_anmHeap_c& ts = p->m_tex_scroll_heap;
        if (tp.field_0x2 == 0xFFFF && tp.field_0x4 == 0xFFFF && isBtp(a.face_btp)) {
            if (tp.mIdx != a.face_btp) {
                J3DAnmTexPattern* btp = p->loadTextureAnimeResource(a.face_btp, FALSE);
                tp.mIdx = a.face_btp;
                if (btp != NULL) {
                    p->setTextureAnimeResource(btp, a.face_btp_frame);
                }
            }
            m.face |= 1;
        }
        if (ts.field_0x2 == 0xFFFF && ts.field_0x4 == 0xFFFF && isBtk(a.face_btk)) {
            if (ts.mIdx != a.face_btk) {
                const u8 timer = daPy_matAnm_c::getMabaTimer(), flg = daPy_matAnm_c::getMabaFlg(),
                         eye = daPy_matAnm_c::getEyeMoveFlg(), morf = daPy_matAnm_c::getMorfFrame();
                J3DAnmTextureSRTKey* btk = p->loadTextureScrollResource(a.face_btk, FALSE);
                ts.mIdx = a.face_btk;
                if (btk != NULL) {
                    p->setTextureScrollResource(btk, a.face_btk_frame);
                }
                daPy_matAnm_c::setMabaTimer(timer);
                daPy_matAnm_c::m_maba_flg = flg;
                daPy_matAnm_c::m_eye_move_flg = eye;
                daPy_matAnm_c::setMorfFrame(morf);
            }
            m.face |= 2;
        }
    }

    static bool mirror(daPy_lk_c* p, int slot) {
        Mirror& m = sMirror[slot];
        const Input& in = sIn[slot];
        const TwwPuppetAnim& a = in.a;
        if (!in.anim || (!isBck(a.track[kUnder0].bck) && !m.active)) {
            if (m.active) {
                endMirror(p, m);
            }
            return false;
        }
        if (p->mCurProc != daPy_lk_c::daPyProc_WAIT_e) {
            p->procWait_init();
            m.active = false;
        }
        if (!m.active) {
            m.active = true;
            m.hands[0] = p->mLeftHandIdx;
            m.hands[1] = p->mRightHandIdx;
            for (int t = 0; t < TWW_PUPPET_TRACKS; t++) {
                m.want[t] = TWW_PUPPET_ANM_NONE;
                m.anm[t] = NULL;
            }
        }
        p->animeUpdate();
        // No bck on under[0] (a cutscene's): the pose stays.
        if (isBck(a.track[kUnder0].bck)) {
            u16 want[4];
            bool reload = false;
            for (int t = kUnder0; t <= kUpper1; t++) {
                const u16 w = a.track[t].bck;
                want[t] = isBck(w) ? w : t < kUpper0 ? TWW_PUPPET_ANM_NONE : TWW_PUPPET_ANM_SAME;
                reload = reload || want[t] != m.want[t] || pack(p, t).getAnmTransform() != m.anm[t];
            }
            if (reload ? !load(p, m, a, want) : (follow(p, m, a, false), false)) {
                endMirror(p, m);
                return false;
            }
        }
        upper2(p, m, a.track[kUpper2]);
        hands(p, a);
        face(p, m, a);
        p->mBodyAngle.x = a.body_angle[0];
        p->mBodyAngle.y = a.body_angle[1];
        p->mBodyAngle.z = a.body_angle[2];
        return true;
    }

    static void faceFrames(daPy_lk_c* p, int slot) {
        const Mirror& m = sMirror[slot];
        if (!m.active || m.face == 0) {
            return;
        }
        const TwwPuppetAnim& a = sIn[slot].a;
        if ((m.face & 1) && p->mpAnmTexPatternData != NULL) {
            const s16 max = p->mpAnmTexPatternData->getFrameMax();
            p->m3530 = max <= 0 ? 0 : a.face_btp_frame > max ? max : a.face_btp_frame;
        }
        if ((m.face & 2) && p->mpTexScrollResData != NULL) {
            const s16 max = p->mpTexScrollResData->getFrameMax();
            p->m3532 = max <= 0 ? 0 : a.face_btk_frame > max ? max : a.face_btk_frame;
        }
    }

    // The item in hand, built in the puppet's own item heaps by the stock model setters. The boomerang,
    // grappling hook and bomb bag are never built (makeItemType spawns their actors for the local Link). The
    // leaf's swing and glide use their own models (procFanSwing_init, procFanGlide_init).
    static void equipItem(daPy_lk_c* p, Mirror& m, u16 item, u32 proc) {
        u16 want = daPy_lk_c::daPyItem_NONE_e;
        const bool bigLeaf = item == dItemNo_DEKU_LEAF_e && proc == daPy_lk_c::daPyProc_FAN_SWING_e;
        if (item == daPy_lk_c::daPyItem_UNK102_e) {
            want = item;
        } else if (item == daPy_lk_c::daPyItem_SWORD_e ? p->checkSwordEquip() != FALSE
                                                 : item == dItemNo_TELESCOPE_e || item == dItemNo_TINGLE_TUNER_e ||
                                                       item == dItemNo_WIND_WAKER_e || p->checkPhotoBoxItem(item) ||
                                                       p->checkBowItem(item) || item == dItemNo_SKULL_HAMMER_e ||
                                                       item == dItemNo_DEKU_LEAF_e || item == dItemNo_HOOKSHOT_e ||
                                                       item == dItemNo_BOOMERANG_e || p->checkBottleItem(item)) {
            want = item;
        }
        if (p->mEquipItem == want && m.bigLeaf == bigLeaf) {
            return;
        }
        p->mEquipItem = daPy_lk_c::daPyItem_NONE_e; // deleteEquipItem then skips its sound, BGM and actor parts
        p->deleteEquipItem(FALSE);
        m.bigLeaf = bigLeaf;
        if (want == daPy_lk_c::daPyItem_UNK102_e) {
            p->mEquipItem = want;
            p->setParachuteFanModel(p->m_HIO->mItem.mFan.m.field_0x28);
        } else if (bigLeaf) {
            p->mEquipItem = want;
            p->setFanModel();
        } else if (want == daPy_lk_c::daPyItem_SWORD_e) {
            p->setSwordModel(FALSE);
        } else if (want == dItemNo_WIND_WAKER_e) {
            p->setTactModel();
        } else if (p->checkBottleItem(want)) {
            p->setBottleModel(want);
        } else if (want == dItemNo_HOOKSHOT_e) {
            p->mEquipItem = want;
            p->setHookshotModel(); // the body only: draw skips the tip without the hookshot actor
        } else if (want == dItemNo_BOOMERANG_e) {
            p->mEquipItem = want;
        } else if (want != daPy_lk_c::daPyItem_NONE_e) {
            p->mEquipItem = want;
            p->makeItemType();
        }
    }

    // Clothes and tunic colour: the hero / casual headers swapped as playerInit does, and for the hero
    // clothes in a custom colour the header pointed at a recoloured copy of the hero image.
    static void outfit(daPy_lk_c* p, Outfit& o, u8 clothes, u32 rgb) {
        ResTIMG* h = p->mpCurrLinktex;
        if (h == NULL || p->mpCLModelData == NULL) {
            return;
        }
        const bool casual = p->checkNoResetFlg1(daPy_py_c::daPyFlg1_CASUAL_CLOTHES) != 0;
        if (o.header != h) {
            o.header = h;
            o.heroOffset = casual ? (u32)p->mOtherLinktex.imageOffset : (u32)h->imageOffset;
            o.rgb = 0;
        }
        ResTIMG& hero = casual ? p->mOtherLinktex : *h;
        if (hero.format != GX_TF_CMPR || hero.width != kTexW || hero.height != kTexH) {
            return;
        }
        const bool wantCasual = clothes == TWW_PUPPET_CLOTHES_CASUAL || (clothes != TWW_PUPPET_CLOTHES_HERO && casual);
        const u32 wantRgb = wantCasual ? 0 : rgb;
        const u32 recoloured = o.image != NULL ? (u32)(s32)(o.image - (u8*)h) : 0; // offsets are h's in either header
        const u32 haveRgb = o.image != NULL && (u32)hero.imageOffset == recoloured ? o.rgb : 0;
        if (wantCasual == casual && wantRgb == haveRgb) {
            return;
        }
        hero.imageOffset = o.heroOffset;
        if (wantCasual != casual) {
            const ResTIMG other = *h;
            *h = p->mOtherLinktex;
            p->mOtherLinktex = other;
            if (wantCasual) {
                p->onNoResetFlg1(daPy_py_c::daPyFlg1_CASUAL_CLOTHES);
            } else {
                p->offNoResetFlg1(daPy_py_c::daPyFlg1_CASUAL_CLOTHES);
            }
        }
        if (wantRgb != 0) {
            if (o.image == NULL) {
                o.image = static_cast<u8*>(JKRHeap::alloc(kTexBytes, 32, mDoExt_getGameHeap()));
            }
            const intptr_t delta = o.image != NULL ? (intptr_t)(o.image - (u8*)h) : 0;
            if (o.image != NULL && delta == (s32)delta) {
                recolour(o.image, (u8*)h + (s32)o.heroOffset, wantRgb);
                h->imageOffset = (u32)(s32)delta;
                o.rgb = wantRgb;
            }
        }
        p->mpCLModelData->getTexture()->resTIMGChanged(h);
    }

    static bool headPos(int slot, cXyz* out) {
        daPy_lk_c* p = sPuppet[slot];
        if (p == NULL || !g_wwoPeers[slot].active || p->mpCLModel == NULL) {
            return false;
        }
        static const Vec head_offset = {40.0f, 0.0f, 0.0f};
        cMtx_multVec(p->mpCLModel->getAnmMtx(CL_JNT_HEAD_JNT_e), &head_offset, out);
        return true;
    }

    static void look(daPy_lk_c* p, int slot) {
        sPuppet[slot] = p;
        const Input& in = sIn[slot];
        // The stock draw's red damage flicker runs while this is above 0; nothing else of the puppet's reads it.
        p->mDamageWaitTimer = wwo_pvpFlashing(slot) || (in.look && (in.l.flags & TWW_PUPPET_LOOK_DAMAGE_FLASH)) ? 1 : 0;
        if (!in.look) {
            return;
        }
        if (in.l.flags & TWW_PUPPET_LOOK_IRON_BOOTS) {
            p->onNoResetFlg0(daPy_py_c::daPyFlg0_EQUIP_HEAVY_BOOTS);
        } else {
            p->offNoResetFlg0(daPy_py_c::daPyFlg0_EQUIP_HEAVY_BOOTS);
        }
        equipItem(p, sMirror[slot], in.l.equip_item, g_wwoPeers[slot].state.proc);
        outfit(p, sOutfit[slot], in.l.clothes, rgbOf(in.l.tunic));
    }

    // In the puppet's window (l_arcName is its archive).
    static void buildHeld(Held& h) {
        J3DModelData* boomerang = static_cast<J3DModelData*>(dComIfG_getObjectRes(l_arcName, dRes_INDEX_LINK_BDL_BOOMERANG_e));
        J3DModelData* bomb = static_cast<J3DModelData*>(dComIfG_getObjectRes(l_arcName, dRes_INDEX_LINK_BDL_BOMB_e));
        h.failed = true;
        if (boomerang == NULL || bomb == NULL) {
            return;
        }
        h.heap = mDoExt_createSolidHeapFromGameToCurrent(0x10000, 0x20);
        if (h.heap == NULL) {
            return;
        }
        h.boomerang = mDoExt_J3DModel__create(boomerang, 0x80000, 0x37220202); // daBoomerang_c::createHeap's
        h.bomb = mDoExt_J3DModel__create(bomb, 0x80000, 0x11000002);           // daBomb_c::createHeap's
        mDoExt_restoreCurrentHeap();
        if (h.boomerang == NULL || h.bomb == NULL) {
            mDoExt_destroySolidHeap(h.heap);
            h = Held();
            h.failed = true;
            return;
        }
        mDoExt_adjustSolidHeap(h.heap);
        h.failed = false;
    }

    static void drawHeld(daPy_lk_c* p, int slot) {
        Held& h = sHeld[slot];
        const bool bomb = sIn[slot].look && sIn[slot].l.grab == TWW_PUPPET_GRAB_BOMB;
        const bool boomerang = p->mEquipItem == dItemNo_BOOMERANG_e;
        if ((!bomb && !boomerang) || p->checkCaughtShapeHide() || p->checkPlayerNoDraw()) {
            return;
        }
        if (h.heap == NULL && !h.failed) {
            buildHeld(h);
        }
        if (h.heap == NULL) {
            return;
        }
        dComIfGd_setListP1();
        if (boomerang) {
            // daBoomerang_c::setKeepMatrix
            mDoMtx_stack_c::transS(10.3f, 36.6f, 1.1f);
            mDoMtx_stack_c::XYZrotM(-0x39F4, -0x357, -0x2AF3);
            mDoMtx_stack_c::revConcat(p->mpCLModel->getAnmMtx(CL_JNT_CL_LHANDA_e));
            h.boomerang->setBaseTRMtx(mDoMtx_stack_c::get());
            p->updateDLSetLight(h.boomerang, 0);
        }
        if (bomb) {
            // Between the hands (setGrabItemPos), turned with Link (daBomb_c::set_mtx); its fuse glow off.
            MtxP l = p->mpCLModel->getAnmMtx(CL_JNT_CL_LHANDA_e);
            MtxP r = p->mpCLModel->getAnmMtx(CL_JNT_CL_RHANDA_e);
            mDoMtx_stack_c::transS((l[0][3] + r[0][3]) * 0.5f, (l[1][3] + r[1][3]) * 0.5f, (l[2][3] + r[2][3]) * 0.5f);
            mDoMtx_stack_c::YrotM(p->shape_angle.y);
            h.bomb->setBaseTRMtx(mDoMtx_stack_c::get());
            if (p->mpBombBrk != NULL) {
                p->mpBombBrk->setFrame(0.0f);
            }
            p->updateDLSetLight(h.bomb, 0);
        }
        dComIfGd_setList();
    }

    // The local camera's first-person bits: checkPlayerNoDraw hides the whole Link on SUBJECT, draw his head
    // on 0x20.
    static u32 cameraBits(daPy_lk_c* p) {
        const u32 bits = dCamAttnStts_SUBJECT_e | dCamAttnStts_00000020_e;
        return p->mCameraInfoIdx == 0 ? dComIfGp_checkCameraAttentionStatus(0, bits) : 0;
    }

    static int readLocal(TwwPuppetAnim* a, TwwPuppetLook* l) {
        daPy_lk_c* p = localLink();
        if (p == NULL) {
            return 0;
        }
        if (a != NULL) {
            memset(a, 0, sizeof(*a));
            a->valid = 1;
            for (int t = 0; t < TWW_PUPPET_TRACKS; t++) {
                J3DAnmTransform* anm = pack(p, t).getAnmTransform();
                const J3DFrameCtrl& fc = frameCtrl(p, t);
                TwwPuppetTrack& tr = a->track[t];
                const bool same = (t == kUpper0 || t == kUpper1) && anm == pack(p, t - kUpper0).getAnmTransform();
                tr.bck = anm == NULL ? TWW_PUPPET_ANM_NONE : same ? TWW_PUPPET_ANM_SAME : heap(p, t).mIdx;
                if (tr.bck != TWW_PUPPET_ANM_SAME && !isBck(tr.bck)) {
                    tr.bck = TWW_PUPPET_ANM_NONE; // a cutscene's (mIdx 0xFFFF)
                }
                tr.start = fc.getStart();
                tr.end = fc.getEnd();
                tr.loop = (s16)fc.getLoop();
                tr.attr = fc.getAttribute();
                tr.frame = fc.getFrame();
                tr.rate = fc.getRate();
                tr.ratio = pack(p, t).getRatio();
            }
            a->face_btp = texAnime(p->m_tex_anm_heap);
            a->face_btk = texAnime(p->m_tex_scroll_heap);
            a->face_btp_frame = p->m3530;
            a->face_btk_frame = p->m3532;
            a->hand_l = p->mLeftHandIdx;
            a->hand_r = p->mRightHandIdx;
            a->body_angle[0] = p->mBodyAngle.x;
            a->body_angle[1] = p->mBodyAngle.y;
            a->body_angle[2] = p->mBodyAngle.z;
        }
        if (l != NULL) {
            memset(l, 0, sizeof(*l));
            l->valid = 1;
            l->sword = dComIfGs_getSelectEquip(0);
            l->shield = dComIfGs_getSelectEquip(1);
            l->bracelet = dComIfGs_getSelectEquip(2);
            l->clothes = p->checkNoResetFlg1(daPy_py_c::daPyFlg1_CASUAL_CLOTHES) ? TWW_PUPPET_CLOTHES_CASUAL
                                                                                 : TWW_PUPPET_CLOTHES_HERO;
            if (l->clothes == TWW_PUPPET_CLOTHES_HERO) {
                l->tunic[0] = (u8)(sLocalRgb >> 16);
                l->tunic[1] = (u8)(sLocalRgb >> 8);
                l->tunic[2] = (u8)sLocalRgb;
            }
            l->flags = (p->checkEquipHeavyBoots() ? TWW_PUPPET_LOOK_IRON_BOOTS : 0) |
                       (p->mDamageWaitTimer > 0 ? TWW_PUPPET_LOOK_DAMAGE_FLASH : 0);
            l->equip_item = p->mEquipItem;
            fopAc_ac_c* grab = p->mActorKeepGrab.getActor();
            if (grab != NULL && (fopAcM_GetName(grab) == fpcNm_BOMB_e || fopAcM_GetName(grab) == fpcNm_Bomb2_e)) {
                l->grab = TWW_PUPPET_GRAB_BOMB;
            }
        }
        return 1;
    }

    // The texture anim a face heap shows: none for a cutscene's (field_0x4), else the priority one, else mIdx.
    static u16 texAnime(const daPy_anmHeap_c& h) {
        if (h.field_0x4 != 0xFFFF) {
            return TWW_PUPPET_ANM_NONE;
        }
        return h.field_0x2 != 0xFFFF ? h.field_0x2 : h.mIdx;
    }

    static void localOutfit() {
        daPy_lk_c* p = localLink();
        if (p != NULL && sLocalOutfitSet) {
            outfit(p, sLocalOutfit, sLocalClothes, sLocalRgb);
        }
    }
};

WwoPuppetScope::WwoPuppetScope(daPy_lk_c* puppet, int slot) : mEquipSwapped(false) {
    JAIZelBasic* audio = mDoAud_zelAudio_c::getInterface();
    memcpy(mAudio, &audio->field_0x0041, sizeof(mAudio));
    mSwordUsing = audio->field_0x00c9;
    if (slot >= 0 && slot < TWW_MAX_PEERS && sIn[slot].look) {
        dSv_player_status_a_c& status = g_dComIfG_gameInfo.save.getPlayer().getPlayerStatusA();
        const TwwPuppetLook& l = sIn[slot].l;
        const u8 equip[3] = {l.sword, l.shield, l.bracelet};
        for (int i = 0; i < 3; i++) {
            mEquip[i] = status.getSelectEquip(i);
            status.setSelectEquip(i, equip[i]);
        }
        mEquipSwapped = true;
    }
    mCameraBits = WwoPuppetLink::cameraBits(puppet);
    g_dComIfG_gameInfo.play.offCameraAttentionStatus(0, mCameraBits);
}

WwoPuppetScope::~WwoPuppetScope() {
    g_dComIfG_gameInfo.play.onCameraAttentionStatus(0, mCameraBits);
    if (mEquipSwapped) {
        dSv_player_status_a_c& status = g_dComIfG_gameInfo.save.getPlayer().getPlayerStatusA();
        for (int i = 0; i < 3; i++) {
            status.setSelectEquip(i, mEquip[i]);
        }
    }
    JAIZelBasic* audio = mDoAud_zelAudio_c::getInterface();
    memcpy(&audio->field_0x0041, mAudio, sizeof(mAudio));
    audio->field_0x00c9 = mSwordUsing;
}

void wwo_puppetLook(daPy_lk_c* puppet, int slot) {
    WwoPuppetLink::look(puppet, slot);
}

bool wwo_puppetMirror(daPy_lk_c* puppet, int slot) {
    return WwoPuppetLink::mirror(puppet, slot);
}

void wwo_puppetFace(daPy_lk_c* puppet, int slot) {
    WwoPuppetLink::faceFrames(puppet, slot);
}

void wwo_puppetDraw(daPy_lk_c* puppet, int slot) {
    WwoPuppetLink::drawHeld(puppet, slot);
}

bool wwo_puppetHeadPos(int slot, cXyz* out) {
    return slot >= 0 && slot < TWW_MAX_PEERS && WwoPuppetLink::headPos(slot, out);
}

void wwo_puppetReset(int slot) {
    sPuppet[slot] = NULL;
    sMirror[slot] = Mirror();
    if (sHeld[slot].heap != NULL) {
        mDoExt_destroySolidHeap(sHeld[slot].heap);
    }
    sHeld[slot] = Held();
    if (sOutfit[slot].image != NULL) {
        JKRHeap::free(sOutfit[slot].image, mDoExt_getGameHeap());
    }
    sOutfit[slot] = Outfit();
}

void wwo_puppetLocalOutfit() {
    WwoPuppetLink::localOutfit();
}

extern "C" {

void wwo_puppetSetAnim(int slot, const TwwPuppetAnim* anim) {
    if (slot < 0 || slot >= TWW_MAX_PEERS) {
        return;
    }
    sIn[slot].anim = anim != NULL && anim->valid != 0;
    if (sIn[slot].anim) {
        sIn[slot].a = *anim;
    }
}

void wwo_puppetSetLook(int slot, const TwwPuppetLook* look) {
    if (slot < 0 || slot >= TWW_MAX_PEERS) {
        return;
    }
    sIn[slot].look = look != NULL && look->valid != 0;
    if (sIn[slot].look) {
        sIn[slot].l = *look;
    }
}

int tww_puppet_get_local(TwwPuppetAnim* anim, TwwPuppetLook* look) {
    return WwoPuppetLink::readLocal(anim, look);
}

void tww_puppet_set_local_outfit(uint8_t clothes, uint8_t r, uint8_t g, uint8_t b) {
    const u8 rgb[3] = {r, g, b};
    sLocalClothes = clothes;
    sLocalRgb = rgbOf(rgb);
    sLocalOutfitSet = true;
}

} // extern "C"
