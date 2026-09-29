/**
 * puppet_seachart.c - the other players on the sea chart (see puppet_seachart.h, SEACHART_* in puppet_shared.h).
 * Included by puppet.c after puppet_nametag.c (puppet_bootBlock / puppet_bootBlockCreate).
 */

#include "puppet_seachart.h"

/* Link's marker pane redrawn once per player: moved by their offset from Link, turned to their heading and
 * tinted their colour, then put back exactly as it was. J2DPane::draw on a child uses the parent's matrix and
 * bounds from this frame's screen draw (J2DPane.cpp: MTXConcat(parent->mDrawMtx, mMtx), mGlobalBounds += the
 * parent's), so the copies sit where the game would put Link at their position, zoom animations included,
 * and a zoomed square's copies are clipped to it. */
static void seachart_drawOn(J2DPane *pane, u8 *block, f32 linkX, f32 linkZ, f32 scaleX, f32 scaleY,
                            J2DGrafContext *graf)
{
  f32 bounds[4];
  u8 mtx[0x30];
  u8 white[4], black[4];
  f32 rotation;
  f32 *b = J2DPANE_BOUNDS(pane);
  u8 *w = J2DPICTURE_WHITE(pane);
  u8 *k = J2DPICTURE_BLACK(pane);
  u32 i, j;

  for (j = 0; j < 4; j++)
    bounds[j] = b[j];
  for (j = 0; j < 0x30; j++)
    mtx[j] = J2DPANE_MTX(pane)[j];
  for (j = 0; j < 4; j++)
  {
    white[j] = w[j];
    black[j] = k[j];
  }
  rotation = J2DPANE_ROTATION(pane);

  for (i = 0; i < SEACHART_MAX_ENTRIES; i++)
  {
    u8 *e = block + SEACHART_OFF_ENTRY0 + i * SEACHART_ENTRY_SIZE;
    f32 x = *(f32 *)(e + SEACHART_E_OFF_X);
    f32 z = *(f32 *)(e + SEACHART_E_OFF_Z);
    f32 dx, dy;

    if ((e[SEACHART_E_OFF_FLAGS] & SEACHART_FLAG_SHOW) == 0 || !(x == x) || !(z == z) || x < -SEACHART_MAX_COORD ||
        x > SEACHART_MAX_COORD || z < -SEACHART_MAX_COORD || z > SEACHART_MAX_COORD)
      continue;
    dx = (x - linkX) * scaleX;
    dy = (z - linkZ) * scaleY;
    b[0] = bounds[0] + dx;
    b[1] = bounds[1] + dy;
    b[2] = bounds[2] + dx;
    b[3] = bounds[3] + dy;
    // As setDspNormalMapLink turns Link's: (s16)(shape_angle.y + 0x8000) * 180 / 32768 degrees.
    J2DPANE_ROTATION(pane) = (f32)(s16)(*(s16 *)(e + SEACHART_E_OFF_ANGLE) + 0x8000) * 180.0f / 32768.0f;
    J2DPane__calcMtx(pane);
    w[0] = e[SEACHART_E_OFF_R];
    w[1] = e[SEACHART_E_OFF_G];
    w[2] = e[SEACHART_E_OFF_B];
    k[0] = e[SEACHART_E_OFF_R] >> 2;
    k[1] = e[SEACHART_E_OFF_G] >> 2;
    k[2] = e[SEACHART_E_OFF_B] >> 2;
    J2DPane__draw(pane, 0.0f, 0.0f, graf, 1);
  }

  for (j = 0; j < 4; j++)
  {
    b[j] = bounds[j];
    w[j] = white[j];
    k[j] = black[j];
  }
  J2DPANE_ROTATION(pane) = rotation;
  for (j = 0; j < 0x30; j++)
    J2DPANE_MTX(pane)[j] = mtx[j];
}

/* Called by seachart_players.asm from dDlst_FMAP_c::draw (0x801BB024), after the chart's J2DScreen has drawn
 * with graf set as the port. dl is the menu's fmapDl. */
void puppet_seachart_draw(u8 *dl, J2DGrafContext *graf)
{
  u8 *block = puppet_bootBlock(SEACHART_PTR_ADDR, SEACHART_MAGIC, SEACHART_BLOCK_SIZE);
  u8 *menu = dl - FMAP_OFF_DL;
  J2DPane *pane;
  f32 linkX, linkZ, size;

  if (block == NULL || graf == NULL || !FMAP_ON_SEA(menu))
    return; // Link's marker only shows on the sea stage (setDspNormalMapLink), and ours go with it
  linkX = FMAP_PLAYER_X(menu);
  linkZ = FMAP_PLAYER_Z(menu);

  // The world map: Link at the area pane's centre + pos * 56 / 100000 (setDspNormalMapLink).
  pane = FMAP_LNK1_PANE(menu);
  if (pane != NULL && J2DPANE_VISIBLE(pane))
    seachart_drawOn(pane, block, linkX, linkZ, FMAP_NORMAL_SCALE, FMAP_NORMAL_SCALE, graf);

  // A zoomed square: (size - 50) / 100000 per unit (setDspLargeMapLink). Hidden while it shows a square
  // Link isn't in; the copies are clipped to the square (the clb pane).
  pane = FMAP_LNK2_PANE(menu);
  if (pane != NULL && J2DPANE_VISIBLE(pane))
  {
    size = FMAP_CLB_SIZE_ORIG_X(menu);
    f32 sx = (size - FMAP_LARGE_MARGIN) / FMAP_SQUARE;
    size = FMAP_CLB_SIZE_ORIG_Y(menu);
    f32 sy = (size - FMAP_LARGE_MARGIN) / FMAP_SQUARE;
    seachart_drawOn(pane, block, linkX, linkZ, sx, sy, graf);
  }
}

void puppet_seachart_onCreate(void)
{
  puppet_bootBlockCreate(SEACHART_PTR_ADDR, SEACHART_MAGIC, SEACHART_BLOCK_SIZE);
  *(volatile u32 *)SEACHART_FN_ADDR = (u32)puppet_seachart_draw;
}

void puppet_seachart_onLastDelete(void)
{
  *(volatile u32 *)SEACHART_FN_ADDR = 0; // the REL is unlinked right after: the chart must not call into it
}
