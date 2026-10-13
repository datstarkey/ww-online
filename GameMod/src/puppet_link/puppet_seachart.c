/**
 * puppet_seachart.c - the other players on the sea chart (see puppet_seachart.h, SEACHART_* in puppet_shared.h).
 * Included by puppet.c after puppet_nametag.c (puppet_bootBlock / puppet_bootBlockCreate).
 */

#include "puppet_seachart.h"

#define SEACHART_ANY_SQUARE 0x7F /* seachart_drawOn: no square filter */

/* Is the pane drawn by its screen: it and every parent visible? J2DPane::draw stops at a hidden pane, so a
 * hidden parent hides its children, but a direct draw of the child skips that check. */
static int seachart_shown(J2DPane *pane)
{
  u8 *tree;
  int depth;

  for (depth = 0; pane != NULL && depth < 16; depth++)
  {
    if (!J2DPANE_VISIBLE(pane))
      return 0;
    tree = J2DPANE_PARENT_TREE(pane);
    pane = tree != NULL ? J2DTREE_PANE(tree) : NULL;
  }
  return 1;
}

/* dMap_c::point2Grid's square for a coordinate (d_map.cpp:1753): floor(v / 100000 + 0.5), but not clamped to
 * -3..3: a player past the edge squares is in none of them (drawn in one, they'd sit outside it). */
static int seachart_grid(f32 v)
{
  f32 s = v / FMAP_SQUARE + 0.5f;
  int g = (int)s;

  if ((f32)g > s)
    g--; // (int) truncates toward 0
  return g;
}

/* Where copies of a marker pane go, in its parent's coordinates: a player at world (x, z) is centred at
 * (baseX, baseY) + ((x, z) - (originX, originZ)) * scale, halfW x halfH either side. */
typedef struct
{
  f32 originX, originZ;
  f32 baseX, baseY;
  f32 scaleX, scaleY;
  f32 halfW, halfH;
  int squareX, squareY; /* only the players in this square, unless squareX is SEACHART_ANY_SQUARE */
} SeachartPlace;

/* Link's marker pane redrawn once per player: placed (see SeachartPlace), turned to their heading and tinted
 * their colour, then put back exactly as it was. J2DPane::draw on a child uses the parent's matrix and bounds
 * from this frame's screen draw (J2DPane.cpp: MTXConcat(parent->mDrawMtx, mMtx), mGlobalBounds += the
 * parent's), so the copies follow the chart's own animations. Drawn unclipped like the screen draws it
 * (J2DScreen::draw passes mbClipToParent, off here): clipped, every copy is cut to lnk1's parent, an 8x8 pane
 * at Link's own spot. */
static void seachart_drawOn(J2DPane *pane, u8 *block, const SeachartPlace *pl, J2DGrafContext *graf)
{
  f32 bounds[4], basePos[2];
  u8 mtx[0x30];
  u8 white[4], black[4];
  f32 rotation;
  f32 *b = J2DPANE_BOUNDS(pane);
  f32 *pivot = J2DPANE_BASE_POS(pane);
  u8 *w = J2DPICTURE_WHITE(pane);
  u8 *k = J2DPICTURE_BLACK(pane);
  u32 i, j;

  for (j = 0; j < 4; j++)
    bounds[j] = b[j];
  basePos[0] = pivot[0];
  basePos[1] = pivot[1];
  for (j = 0; j < 0x30; j++)
    mtx[j] = J2DPANE_MTX(pane)[j];
  for (j = 0; j < 4; j++)
  {
    white[j] = w[j];
    black[j] = k[j];
  }
  rotation = J2DPANE_ROTATION(pane);

  // Turned about its centre, as the chart's rotate(mSize * 0.5) does.
  pivot[0] = pl->halfW;
  pivot[1] = pl->halfH;
  for (i = 0; i < SEACHART_MAX_ENTRIES; i++)
  {
    u8 *e = block + SEACHART_OFF_ENTRY0 + i * SEACHART_ENTRY_SIZE;
    f32 x = *(f32 *)(e + SEACHART_E_OFF_X);
    f32 z = *(f32 *)(e + SEACHART_E_OFF_Z);
    f32 cx, cy;

    if ((e[SEACHART_E_OFF_FLAGS] & SEACHART_FLAG_SHOW) == 0 || !(x == x) || !(z == z) || x < -SEACHART_MAX_COORD ||
        x > SEACHART_MAX_COORD || z < -SEACHART_MAX_COORD || z > SEACHART_MAX_COORD)
      continue;
    if (pl->squareX != SEACHART_ANY_SQUARE && (seachart_grid(x) != pl->squareX || seachart_grid(z) != pl->squareY))
      continue;
    cx = pl->baseX + (x - pl->originX) * pl->scaleX;
    cy = pl->baseY + (z - pl->originZ) * pl->scaleY;
    b[0] = cx - pl->halfW;
    b[1] = cy - pl->halfH;
    b[2] = cx + pl->halfW;
    b[3] = cy + pl->halfH;
    // As setDspNormalMapLink turns Link's: (s16)(shape_angle.y + 0x8000) * 180 / 32768 degrees.
    J2DPANE_ROTATION(pane) = (f32)(s16)(*(s16 *)(e + SEACHART_E_OFF_ANGLE) + 0x8000) * 180.0f / 32768.0f;
    J2DPane__calcMtx(pane);
    w[0] = e[SEACHART_E_OFF_R];
    w[1] = e[SEACHART_E_OFF_G];
    w[2] = e[SEACHART_E_OFF_B];
    k[0] = e[SEACHART_E_OFF_R] >> 2;
    k[1] = e[SEACHART_E_OFF_G] >> 2;
    k[2] = e[SEACHART_E_OFF_B] >> 2;
    J2DPane__draw(pane, 0.0f, 0.0f, graf, 0);
  }

  for (j = 0; j < 4; j++)
  {
    b[j] = bounds[j];
    w[j] = white[j];
    k[j] = black[j];
  }
  pivot[0] = basePos[0];
  pivot[1] = basePos[1];
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
  J2DPane *pane, *clb;
  u8 *sv;
  f32 *b;
  SeachartPlace pl;
  u8 visible;

  if (block == NULL || graf == NULL || !FMAP_ON_SEA(menu))
    return; // Link's marker only shows on the sea stage (setDspNormalMapLink), and ours go with it

  // The world map, while it is the view (the zoomed views cover lnk1, and ours draw after the whole screen): Link
  // at the area pane's centre + pos * 56 / 100000 (setDspNormalMapLink), so the others are placed from his marker.
  pane = FMAP_LNK1_PANE(menu);
  if (pane != NULL && FMAP_ZOOM_PROC(menu) == FMAP_ZOOM_PROC_SELECT_GRID && seachart_shown(pane))
  {
    b = J2DPANE_BOUNDS(pane);
    pl.originX = FMAP_PLAYER_X(menu);
    pl.originZ = FMAP_PLAYER_Z(menu);
    pl.baseX = (b[0] + b[2]) * 0.5f;
    pl.baseY = (b[1] + b[3]) * 0.5f;
    pl.scaleX = FMAP_NORMAL_SCALE;
    pl.scaleY = FMAP_NORMAL_SCALE;
    pl.halfW = (b[2] - b[0]) * 0.5f;
    pl.halfH = (b[3] - b[1]) * 0.5f;
    pl.squareX = SEACHART_ANY_SQUARE;
    pl.squareY = 0;
    seachart_drawOn(pane, block, &pl, graf);
  }

  // A zoomed square, whichever it is, once it has zoomed in and until it zooms out (when the game shows lnk2,
  // d_menu_fmap.cpp:2040 / 2073; not over an island's detail chart). lnk2 is Link only on his own square
  // (checkDspLargeMapLink hides it on the others, and its bounds keep its last zoom animation's), so the others
  // are placed from the square itself, at its full size: setDspLargeMapLink's size / 2 + (pos - the square's
  // centre) * (size - 50) / 100000. Only that square's players.
  pane = FMAP_LNK2_PANE(menu);
  clb = FMAP_CLB_PANE(menu);
  sv = FMAP_SV(menu);
  if (pane == NULL || clb == NULL || sv == NULL || FMAP_ZOOM_PROC(menu) != FMAP_ZOOM_PROC_LV1 || !seachart_shown(clb))
    return;
  pl.squareX = FMAPSV_CUR_X(sv);
  pl.squareY = FMAPSV_CUR_Y(sv);
  if (pl.squareX < -FMAP_GRID_MAX || pl.squareX > FMAP_GRID_MAX || pl.squareY < -FMAP_GRID_MAX ||
      pl.squareY > FMAP_GRID_MAX)
    return;
  pl.originX = FMAP_SQUARE * (f32)pl.squareX;
  pl.originZ = FMAP_SQUARE * (f32)pl.squareY;
  pl.baseX = FMAP_CLB_SIZE_ORIG_X(menu) * 0.5f;
  pl.baseY = FMAP_CLB_SIZE_ORIG_Y(menu) * 0.5f;
  pl.scaleX = (FMAP_CLB_SIZE_ORIG_X(menu) - FMAP_LARGE_MARGIN) / FMAP_SQUARE;
  pl.scaleY = (FMAP_CLB_SIZE_ORIG_Y(menu) - FMAP_LARGE_MARGIN) / FMAP_SQUARE;
  pl.halfW = FMAP_LNK2_SIZE_ORIG_X(menu) * 0.5f;
  pl.halfH = FMAP_LNK2_SIZE_ORIG_Y(menu) * 0.5f;
  visible = J2DPANE_VISIBLE(pane);
  J2DPANE_VISIBLE(pane) = 1; // J2DPane::draw skips a hidden pane
  seachart_drawOn(pane, block, &pl, graf);
  J2DPANE_VISIBLE(pane) = visible;
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
