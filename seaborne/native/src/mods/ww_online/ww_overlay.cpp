// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
// The peers' names over their puppets and the other players on the sea chart (the Dolphin client's REL:
// GameMod/src/puppet_link/puppet_nametag.c and puppet_seachart.c).
//
// Name tags: after the frame's draw pass (wwo_draw, from fapGm_After) each shown puppet's name goes into the 2D
// opaque list, the HUD's, as a packet that draws it like JUTDbPrint::flush does, in the message font
// mDoExt_font0, at a point above the puppet projected with mDoLib_project. The lists were reset at the draw
// pass's start and are painted at the next frame's start, so a static packet is always still alive then.
//
// The sea chart: dDlst_FMAP_c::draw calls g_dDlst_FMAP_afterDraw once the chart's screen has drawn; Link's own
// marker picture is drawn again once per other player, moved by their offset from him, turned to their
// heading and tinted their colour, then put back as it was.
#include "d/dolzel.h" // IWYU pragma: keep
#include "f_op/f_op_actor_mng.h"
#include "d/d_com_inf_game.h"
#include "d/d_menu_fmap.h"
#include "d/d_meter.h"
#include "JSystem/J2DGraph/J2DOrthoGraph.h"
#include "JSystem/J2DGraph/J2DPicture.h"
#include "JSystem/JUtility/JUTFont.h"
#include "m_Do/m_Do_lib.h"
#include "m_Do/m_Do_mtx.h"
#include "mods/ww_online.h"
#include "pc/pc_aspect.h"
#include "pc/pc_harness.h"

#include "ww_online_internal.h"
#include "ww_overlay.h"

#include <cmath>
#include <cstddef>
#include <cstdio>
#include <cstring>

extern JUTFont* mDoExt_font0;

static_assert(sizeof(TwwOverlaySeaMarker) == 16 && offsetof(TwwOverlaySeaMarker, angle_y) == 8 &&
              offsetof(TwwOverlaySeaMarker, r) == 10, "TwwOverlaySeaMarker: the C# binding's layout");

namespace {

// Above the top of the puppet's head (wwo_puppetHeadPos, which follows the model in the boat too); above
// his feet by Link's attention point (92.5) plus his cap if the head is not known yet.
const f32 kAboveHead = 13.5f;
const f32 kHeadY = 135.0f;
const f32 kBaselineUp = 4.0f;
// Camera depth -> glyph height (2D-port pixels) and alpha.
const f32 kDepthMin = 60.0f;
const f32 kDepthNear = 400.0f;
const f32 kDepthSmall = 4000.0f;
const f32 kDepthFade = 4500.0f;
const f32 kDepthMax = 6000.0f;
const f32 kPxNear = 22.0f;
const f32 kPxFar = 13.0f;
const f32 kScreenMargin = 80.0f;
const f32 kShadow = 1.5f;

class NameTag : public dDlst_base_c {
public:
    void draw() override;

    f32 mX;  // 2D-port x of the name's centre
    f32 mY;  // of its baseline
    f32 mPx; // glyph cell height
    u8 mAlpha;
    char mName[TWW_OVERLAY_NAME_BYTES];
};

NameTag sTags[TWW_MAX_PEERS];
char sNames[TWW_MAX_PEERS][TWW_OVERLAY_NAME_BYTES];
TwwOverlaySeaMarker sMarkers[TWW_OVERLAY_SEA_MARKERS];
int sMarkerCount;

void NameTag::draw() {
    JUTFont* font = mDoExt_font0;
    J2DOrthoGraph* graf = dComIfGp_getCurrentGrafPort();
    if (font == NULL || graf == NULL || !font->isValid()) {
        return;
    }
    const int cellW = font->getCellWidth();
    const int cellH = font->getCellHeight();
    if (cellW <= 0 || cellH <= 0) {
        return;
    }
    // drawChar_scale advances each glyph by its width * (scale / cell width).
    const f32 scale = mPx / (f32)cellH;
    const u32 len = strlen(mName);
    f32 width = 0.0f;
    for (u32 i = 0; i < len; i++) {
        width += (f32)font->getWidth((u8)mName[i]) * scale;
    }
    const f32 x = mX - width * 0.5f;
    const f32 cw = (f32)cellW * scale;
    const f32 ch = (f32)cellH * scale;

    graf->setPort();
    font->setGX();
    const JUtility::TColor saved[4] = {font->mColor1, font->mColor2, font->mColor3, font->mColor4};
    font->setCharColor(JUtility::TColor(0, 0, 0, mAlpha * 3 / 4));
    font->drawString_size_scale(x + kShadow, mY + kShadow, cw, ch, mName, len, true);
    font->setCharColor(JUtility::TColor(0xFF, 0xFF, 0xFF, mAlpha));
    font->drawString_size_scale(x, mY, cw, ch, mName, len, true);
    font->mColor1 = saved[0];
    font->mColor2 = saved[1];
    font->mColor3 = saved[2];
    font->mColor4 = saved[3];
    // drawChar_scale leaves the vertex format its own: the next 2D drawer gets the port's state.
    graf->setPort();
}

// Would the game show its HUD now (dMeter_statusCheck)? Not with 2D off, during an event, in a menu, or looking
// through the telescope or the Picto Box.
bool hudVisible() {
    return dComIfGp_2dShowCheck() && !dComIfGp_event_runCheck() && dMenu_flag() == 0 &&
           !dComIfGp_checkPlayerStatus0(0, daPyStts0_TELESCOPE_LOOK_e) &&
           !dComIfGp_checkPlayerStatus1(0, daPyStts1_PICTO_BOX_AIM_e);
}

void queueTag(int slot, fopAc_ac_c* puppet, view_class* view) {
    cXyz anchor;
    if (wwo_puppetHeadPos(slot, &anchor)) {
        anchor.y += kAboveHead;
    } else {
        anchor = puppet->current.pos;
        anchor.y += kHeadY;
    }
    cXyz cam;
    cMtx_multVec(view->mViewMtx, &anchor, &cam);
    const f32 depth = -cam.z; // the view looks down -z
    if (!(depth > kDepthMin && depth < kDepthMax)) {
        return;
    }
    cXyz screen;
    mDoLib_project(&anchor, &screen);
    if (screen.x < -kScreenMargin || screen.x > 640.0f + kScreenMargin || screen.y < -kScreenMargin ||
        screen.y > 480.0f + kScreenMargin) {
        return;
    }
    // mDoLib_project spans the picture as 640x480; the 2D port spans it as pc_aspect_2d_left..right by -21..503
    // (m_Do_graphic's ortho), wider than 640 in widescreen.
    const f32 left = pc_aspect_2d_left();
    screen.x = left + screen.x * (pc_aspect_2d_right() - left) / 640.0f;
    screen.y = -21.0f + screen.y * 524.0f / 480.0f;
    f32 t = (depth - kDepthNear) / (kDepthSmall - kDepthNear);
    t = t < 0.0f ? 0.0f : (t > 1.0f ? 1.0f : t);
    const f32 alpha = depth > kDepthFade ? 255.0f * (kDepthMax - depth) / (kDepthMax - kDepthFade) : 255.0f;

    NameTag& tag = sTags[slot];
    tag.mX = screen.x;
    tag.mY = screen.y - kBaselineUp;
    tag.mPx = kPxNear + (kPxFar - kPxNear) * t;
    tag.mAlpha = (u8)alpha;
    memcpy(tag.mName, sNames[slot], sizeof(tag.mName));
    if (tag.mAlpha != 0) {
        dComIfGd_set2DOpa(&tag);
    }
}

// The pane and every parent visible: what its screen draws (a direct draw of the child skips that check).
bool paneShown(J2DPane* pane) {
    for (int depth = 0; pane != NULL && depth < 16; depth++) {
        if (!pane->isVisible()) {
            return false;
        }
        JSUTree<J2DPane>* parent = pane->mPaneTree.getParent();
        pane = parent != NULL ? parent->getObject() : NULL;
    }
    return true;
}

const s8 kAnySquare = 0x7F;
const f32 kSquare = 100000.0f;
const s8 kGridMax = 3;                         // squares are -3..3
const f32 kNormalScale = 56.0f / 100000.0f;    // setDspNormalMapLink: pane units per world unit
const f32 kLargeMargin = 50.0f;                // setDspLargeMapLink: (size - 50) / 100000
const f32 kMaxCoord = 400000.0f;
const u8 kZoomProcSelectGrid = 0;              // the world map, steady
const u8 kZoomProcLv1 = 2;                     // a zoomed square, steady

// dMap_c::point2Grid's square, not clamped: a player past the edge squares is in none of them.
int gridOf(f32 v) {
    return (int)floorf(v / kSquare + 0.5f);
}

// Where copies of a marker go, in its parent's coordinates: a player at (x, z) is centred at
// (baseX, baseY) + ((x, z) - (originX, originZ)) * scale.
struct Place {
    f32 originX, originZ;
    f32 baseX, baseY;
    f32 scaleX, scaleY;
    f32 halfW, halfH;
    int squareX, squareY; // only that square's players, unless squareX is kAnySquare
};

// The marker picture drawn once per player (J2DPane::draw on a child uses its parent's matrix and bounds from
// this frame's screen draw, so the copies follow the chart's animations), unclipped like the screen draws it.
void drawMarkers(J2DPicture* pic, const Place& pl, J2DOrthoGraph* graf) {
    const JGeometry::TBox2<f32> bounds = pic->mBounds;
    const JGeometry::TVec2<f32> basePos = pic->mBasePosition;
    Mtx mtx;
    cMtx_copy(pic->mMtx, mtx);
    const f32 rotation = pic->mRotation;
    const JUtility::TColor white = pic->getWhite();
    const JUtility::TColor black = pic->getBlack();

    // Turned about its centre, as the chart's own rotate(mSize * 0.5).
    pic->mBasePosition.x = pl.halfW;
    pic->mBasePosition.y = pl.halfH;
    for (int i = 0; i < sMarkerCount; i++) {
        const TwwOverlaySeaMarker& m = sMarkers[i];
        if (pl.squareX != kAnySquare && (gridOf(m.x) != pl.squareX || gridOf(m.z) != pl.squareY)) {
            continue;
        }
        const f32 cx = pl.baseX + (m.x - pl.originX) * pl.scaleX;
        const f32 cy = pl.baseY + (m.z - pl.originZ) * pl.scaleY;
        pic->mBounds.i.x = cx - pl.halfW;
        pic->mBounds.i.y = cy - pl.halfH;
        pic->mBounds.f.x = cx + pl.halfW;
        pic->mBounds.f.y = cy + pl.halfH;
        // As setDspNormalMapLink turns Link's.
        pic->mRotation = (f32)(s16)(m.angle_y + 0x8000) * 180.0f / 32768.0f;
        pic->calcMtx();
        pic->setBlackWhite(JUtility::TColor(m.r >> 2, m.g >> 2, m.b >> 2, black.a), JUtility::TColor(m.r, m.g, m.b, white.a));
        pic->J2DPane::draw(0.0f, 0.0f, graf, false);
    }

    pic->mBounds = bounds;
    pic->mBasePosition = basePos;
    pic->mRotation = rotation;
    cMtx_copy(mtx, pic->mMtx);
    pic->setBlackWhite(black, white);
}

J2DPicture* pictureOf(const fopMsgM_pane_class& pane) {
    return pane.pane != NULL && pane.pane->getTypeID() == 0x12 ? static_cast<J2DPicture*>(pane.pane) : NULL;
}

// Set before the first frame.
struct Install {
    Install() { g_dDlst_FMAP_afterDraw = WwoOverlay::drawSeaChart; }
} sInstall;

} // namespace

void WwoOverlay::drawSeaChart(dDlst_FMAP_c* dl, J2DOrthoGraph* graf) {
    dMenu_Fmap_c* menu = reinterpret_cast<dMenu_Fmap_c*>(reinterpret_cast<char*>(dl) - offsetof(dMenu_Fmap_c, fmapDl));
    // Link's marker only shows on the sea stage (setDspNormalMapLink), and the others' go with it.
    if (sMarkerCount == 0 || graf == NULL || !menu->mFishmanActive) {
        return;
    }
    // The world map while it is the view: placed from Link's marker (his pos * 56 / 100000 from the area's centre).
    J2DPicture* pic = pictureOf(menu->mLnk1Pane);
    if (pic != NULL && menu->mFmapProcIdx == kZoomProcSelectGrid && paneShown(pic)) {
        Place pl;
        pl.originX = menu->mPlayerPos.x;
        pl.originZ = menu->mPlayerPos.y; // = Link's z
        pl.baseX = (pic->mBounds.i.x + pic->mBounds.f.x) * 0.5f;
        pl.baseY = (pic->mBounds.i.y + pic->mBounds.f.y) * 0.5f;
        pl.scaleX = pl.scaleY = kNormalScale;
        pl.halfW = pic->mBounds.getWidth() * 0.5f;
        pl.halfH = pic->mBounds.getHeight() * 0.5f;
        pl.squareX = kAnySquare;
        pl.squareY = 0;
        drawMarkers(pic, pl, graf);
    }

    // A zoomed square, whichever it is, while zoomed in. lnk2 is Link only on his own square, so the others are
    // placed from the square itself at its full size (setDspLargeMapLink): size / 2 + (pos - the square's
    // centre) * (size - 50) / 100000.
    pic = pictureOf(menu->mLnk2Pane);
    J2DPane* square = menu->mClbPane.pane;
    if (pic == NULL || square == NULL || menu->fmapSv == NULL || menu->mFmapProcIdx != kZoomProcLv1 || !paneShown(square)) {
        return;
    }
    Place pl;
    pl.squareX = menu->fmapSv->getCurX();
    pl.squareY = menu->fmapSv->getCurY();
    if (pl.squareX < -kGridMax || pl.squareX > kGridMax || pl.squareY < -kGridMax || pl.squareY > kGridMax) {
        return;
    }
    pl.originX = kSquare * (f32)pl.squareX;
    pl.originZ = kSquare * (f32)pl.squareY;
    pl.baseX = menu->mClbPane.mSizeOrig.x * 0.5f;
    pl.baseY = menu->mClbPane.mSizeOrig.y * 0.5f;
    pl.scaleX = (menu->mClbPane.mSizeOrig.x - kLargeMargin) / kSquare;
    pl.scaleY = (menu->mClbPane.mSizeOrig.y - kLargeMargin) / kSquare;
    pl.halfW = menu->mLnk2Pane.mSizeOrig.x * 0.5f;
    pl.halfH = menu->mLnk2Pane.mSizeOrig.y * 0.5f;
    const bool visible = pic->isVisible();
    pic->show(); // J2DPane::draw skips a hidden pane
    drawMarkers(pic, pl, graf);
    if (!visible) {
        pic->hide();
    }
}

void wwo_draw() {
    view_class* view = NULL;
    for (int i = 0; i < TWW_MAX_PEERS; i++) {
        fopAc_ac_c* puppet = sNames[i][0] != '\0' && g_wwoPeers[i].active ? wwo_puppetActor(i) : NULL;
        if (puppet == NULL) {
            continue;
        }
        if (view == NULL) {
            view = dComIfGd_getView();
            if (view == NULL || dComIfGd_getViewport() == NULL || !hudVisible()) {
                return;
            }
        }
        queueTag(i, puppet, view);
    }
}

void wwo_overlayTestFeed(const TwwLocalState& local, const TwwPeerState& peer) {
    tww_overlay_set_peer_name(0, "Test Peer");
    // Far enough from the local Link to stand apart from his marker on the chart.
    const TwwOverlaySeaMarker marker = {peer.pos[0] + 20000.0f, peer.pos[2] + 10000.0f, peer.angle[1], 220, 40, 40, {}};
    tww_overlay_set_sea_markers(&marker, 1);
    const u32 frame = pc_frame_count();
    if (frame % 300 == 150) {
        TwwOverlayEvent e = {};
        e.kind = TWW_OVERLAY_EVENT_BOMB_THROW;
        e.id = frame;
        e.pos[0] = peer.pos[0];
        e.pos[1] = peer.pos[1] + 100.0f;
        e.pos[2] = peer.pos[2];
        e.speed_f = 8.0f;
        e.speed_y = 20.0f;
        e.gravity = -2.9f;
        e.angle[1] = peer.angle[1];
        e.timer = 90;
        tww_overlay_push_peer_event(&e);
    }
    tww_overlay_set_event_send(1);
    TwwOverlayEvent e;
    while (tww_overlay_poll_event(&e)) {
        fprintf(stderr, "[wwo] fx: local event %u #%u room %d at (%.0f %.0f %.0f) timer %d variant %u\n", e.kind, e.id,
                e.room, e.pos[0], e.pos[1], e.pos[2], e.timer, e.variant);
        for (int i = 0; i < 3; i++) {
            e.pos[i] += peer.pos[i] - local.pos[i];
        }
        e.id |= 0x80000000; // apart from the periodic bomb's ids
        tww_overlay_push_peer_event(&e);
    }
}

extern "C" {

void tww_overlay_set_peer_name(int slot, const char* utf8) {
    if (slot < 0 || slot >= TWW_MAX_PEERS) {
        return;
    }
    // The message font has the game's English characters: printable ASCII only.
    int n = 0;
    for (const char* p = utf8; p != NULL && *p != '\0' && n < TWW_OVERLAY_NAME_BYTES - 1; p++) {
        if (*p >= 0x20 && *p <= 0x7E) {
            sNames[slot][n++] = *p;
        }
    }
    sNames[slot][n] = '\0';
}

void tww_overlay_set_sea_markers(const TwwOverlaySeaMarker* markers, int count) {
    sMarkerCount = 0;
    for (int i = 0; markers != NULL && i < count && sMarkerCount < TWW_OVERLAY_SEA_MARKERS; i++) {
        const TwwOverlaySeaMarker& m = markers[i];
        if (m.x > -kMaxCoord && m.x < kMaxCoord && m.z > -kMaxCoord && m.z < kMaxCoord) { // (false for NaN)
            sMarkers[sMarkerCount++] = m;
        }
    }
}

} // extern "C"
