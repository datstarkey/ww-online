/**
 * puppet_nametag.c - the peer's name above each puppet. See puppet_nametag.h for the draw path.
 * Included by puppet.c (one translation unit) after puppet_appearance.c (AppAllocFn).
 *
 * Names come from the C# client through the names block (puppet_shared.h PUPPET_NAMES_*): one
 * small game-heap block, allocated by the first REL instance and never freed, so the client can
 * write it at any time without racing a free. Each REL instance adopts it by its magic and boot
 * stamp (a block from before a soft reset is not ours any more: puppet_shared.h).
 */

#include "puppet_nametag.h"

/* Anchor: 92.5 above the model base is Link's attention point, about his eyes (daPy_lk_c::setAttentionPos,
 * d_a_player_main.cpp:10261); the extra clears his cap. */
#define NAMETAG_HEAD_Y 135.0f
#define NAMETAG_BASELINE_UP 4.0f /* 2D-port pixels between the anchor and the text baseline */

/* Distance (camera depth, game units) -> glyph height (2D-port pixels) and alpha. Sized so a name at
 * the usual third-person distance reads like the HUD text, and a boat far out at sea still shows one. */
#define NAMETAG_DEPTH_MIN 60.0f     /* closer (camera in the head, or behind the camera): hidden */
#define NAMETAG_DEPTH_NEAR 400.0f   /* up to here: NAMETAG_PX_NEAR */
#define NAMETAG_DEPTH_SMALL 4000.0f /* from here: NAMETAG_PX_FAR */
#define NAMETAG_DEPTH_FADE 4500.0f  /* fades out from here ... */
#define NAMETAG_DEPTH_MAX 6000.0f   /* ... to nothing here */
#define NAMETAG_PX_NEAR 22.0f
#define NAMETAG_PX_FAR 13.0f
#define NAMETAG_SCREEN_MARGIN 80.0f /* anchor this far off the 640x480 port: hidden */
#define NAMETAG_SHADOW 1.5f         /* drop-shadow offset, pixels */

/* dComIfGp_checkPlayerStatus0/1 bits (d_com_inf_game.h:58, :83) whose HUD dMeter_statusCheck
 * replaces (d_meter.cpp:822-824): looking through the telescope, aiming the Picto Box. */
#define NAMETAG_STTS0_TELESCOPE_LOOK 0x00200000
#define NAMETAG_STTS1_PICTO_BOX_AIM 0x00000008

typedef int (*NametagFontIntFn)(void *font);
typedef void (*NametagFontVoidFn)(void *font);
typedef void (*NametagFontWidthFn)(void *font, int code, u8 *width); /* JUTFont::TWidth {u8 offset, u8 width} */

static void nametag_draw(PuppetNameTag *tag);
static void nametag_noop(PuppetNameTag *tag) { (void)tag; }

/* dDlst_base_c vtable: {RTTI, this-offset, dtor, draw} (main.dol __vt__12dDlst_base_c 0x80375900). */
static const void *const l_nametagVtbl[4] = {NULL, NULL, (const void *)nametag_noop, (const void *)nametag_draw};

/* ---- Boot-stamped game-heap blocks (names here, live world in puppet_liveworld.c) ----
 * A small block the REL allocates once and never frees, so C# can write it at any time without racing
 * a free; each REL instance adopts it by its magic (+0) and boot stamp (+8, __OSStartTime). A block from
 * before a soft reset is not ours any more: its pointer and bytes survive, the game heap does not
 * (puppet_shared.h PUPPET_NAMES_*). */

/* __OSStartTime (u64, dolphin/os/OS.c:39): set once per boot by OSInit (:230). */
static const volatile u32 *puppet_bootTime(void)
{
  return (const volatile u32 *)&os____OSStartTime;
}

/* The block published at ptrAddr, or NULL: MEM1 (48MB), aligned, magic set and made in THIS boot. */
static u8 *puppet_bootBlock(u32 ptrAddr, u32 magic, u32 size)
{
  u32 a = *(volatile u32 *)ptrAddr;
  if (a < 0x80000000 || a > 0x83000000 - size || (a & 3) != 0 || *(volatile u32 *)a != magic)
    return NULL;
  const volatile u32 *stamp = (const volatile u32 *)(a + PUPPET_NAMES_OFF_BOOT);
  const volatile u32 *boot = puppet_bootTime();
  if (stamp[0] != boot[0] || stamp[1] != boot[1])
    return NULL;
  return (u8 *)a;
}

/* Adopt the block at ptrAddr, or allocate a zeroed one, stamp it and publish it there. */
static void puppet_bootBlockCreate(u32 ptrAddr, u32 magic, u32 size)
{
  if (puppet_bootBlock(ptrAddr, magic, size))
    return;
  /* None, or one from an earlier boot (never touch it: that memory is someone else's now). */
  *(volatile u32 *)ptrAddr = 0;
  /* From the heap's tail (negative alignment, JKRExpHeap::do_alloc JKRExpHeap.cpp:110) so the
   * permanent block doesn't split the free space the stage allocates from. */
  AppAllocFn alloc = (AppAllocFn)(u32)JKRHeap__alloc; /* JKRHeap::alloc(u32, int, JKRHeap*) returns void* */
  u8 *block = (u8 *)alloc(size, -4, mDoExt_getGameHeap());
  if (!block)
    return;
  /* Magic last, then the pointer: C# reads the block concurrently and needs magic + stamp to use it
   * (after a reboot the new block often lands where the stale one was). */
  volatile u32 *words = (volatile u32 *)block;
  for (u32 i = 0; i < size / 4; i++)
    words[i] = 0;
  const volatile u32 *boot = puppet_bootTime();
  words[PUPPET_NAMES_OFF_BOOT / 4] = boot[0];
  words[PUPPET_NAMES_OFF_BOOT / 4 + 1] = boot[1];
  words[0] = magic;
  *(volatile u32 *)ptrAddr = (u32)block;
}

/* The published names block, or NULL. */
static u8 *nametag_block(void)
{
  return puppet_bootBlock(PUPPET_NAMES_PTR_ADDR, PUPPET_NAMES_MAGIC, PUPPET_NAMES_BLOCK_SIZE);
}

void puppet_nametag_onCreate(void)
{
  puppet_bootBlockCreate(PUPPET_NAMES_PTR_ADDR, PUPPET_NAMES_MAGIC, PUPPET_NAMES_BLOCK_SIZE);
}

/* Would the game show its HUD now? The conditions dMeter_statusCheck hides it for (d_meter.cpp:746,
 * 767, 779, 822-824): 2D off (cutscenes, demo cameras), an event (cutscene, talking), the pause menu
 * (dMenu_flag = dMenu_pause, d_meter.cpp:661), telescope / Picto Box view. */
static int nametag_hudVisible(void)
{
  if (GAMEINFO_2D_SHOW(&g_dComIfG_gameInfo) == 0 || GAMEINFO_EVT_MODE(&g_dComIfG_gameInfo) != 0)
    return 0;
  if (*(volatile u8 *)&d_meter__dMenu_pause != 0)
    return 0;
  if ((GAMEINFO_PLAYERSTATUS(&g_dComIfG_gameInfo) & NAMETAG_STTS0_TELESCOPE_LOOK) != 0 ||
      (GAMEINFO_PLAYERSTATUS1(&g_dComIfG_gameInfo) & NAMETAG_STTS1_PICTO_BOX_AIM) != 0)
    return 0;
  return 1;
}

void puppet_nametag_queue(fopAc_ac_c *actor, J3DModel *model, u32 slotIndex, PuppetNameTag *tag)
{
  if (slotIndex >= PUPPET_MAX_SLOTS || !nametag_hudVisible())
    return;
  /* The slot went inactive after this puppet's execute: puppet_drawBody hid it, so no name either. */
  if (*(volatile u32 *)(PUPPET_SLOT_BASE(slotIndex) + PUPPET_SLOT_OFF_ACTIVE) == 0)
    return;
  u8 *block = nametag_block();
  if (!block || (*(volatile u32 *)(block + PUPPET_NAMES_OFF_FLAGS) & PUPPET_NAMES_FLAG_SHOW) == 0)
    return;

  /* Copy the name (C# writes it whenever it changes): printable ASCII only, always terminated. */
  const volatile char *src = PUPPET_NAMES_NAME(block, slotIndex);
  int n = 0;
  for (int i = 0; i < PUPPET_NAME_BYTES - 1; i++)
  {
    char c = src[i];
    if (c == 0)
      break;
    if (c >= 0x20 && c <= 0x7E)
      tag->name[n++] = c;
  }
  tag->name[n] = 0;
  if (n == 0)
    return;

  u8 *drawlist = (u8 *)GAMEINFO_DRAWLIST(&g_dComIfG_gameInfo);
  if (DDLST_LIST_MPVIEW(drawlist) == NULL || DDLST_LIST_MPVIEWPORT(drawlist) == NULL)
    return;

  /* Above the head of the model as drawn (seated in the boat, swimming, ...). */
  cXyz anchor;
  if (model)
  {
    f32 *base = (f32 *)((u8 *)model + 0x24); /* J3DModel::mBaseTransformMtx, getBaseTRMtx */
    anchor.x = base[3];
    anchor.y = base[7];
    anchor.z = base[11];
  }
  else
  {
    anchor = *FOPAC_CURRENT_POS(actor);
  }
  anchor.y += NAMETAG_HEAD_Y;

  cXyz cam;
  m_Do_lib__mDoLib_pos2camera(&anchor, &cam);
  f32 depth = -cam.z; /* the view looks down -Z */
  if (!(depth > NAMETAG_DEPTH_MIN && depth < NAMETAG_DEPTH_MAX))
    return;

  cXyz screen;
  m_Do_lib__mDoLib_project(&anchor, &screen);
  if (screen.x < -NAMETAG_SCREEN_MARGIN || screen.x > 640.0f + NAMETAG_SCREEN_MARGIN ||
      screen.y < -NAMETAG_SCREEN_MARGIN || screen.y > 480.0f + NAMETAG_SCREEN_MARGIN)
    return;

  f32 t = (depth - NAMETAG_DEPTH_NEAR) / (NAMETAG_DEPTH_SMALL - NAMETAG_DEPTH_NEAR);
  t = t < 0.0f ? 0.0f : (t > 1.0f ? 1.0f : t);
  f32 alpha = 255.0f;
  if (depth > NAMETAG_DEPTH_FADE)
    alpha = 255.0f * (NAMETAG_DEPTH_MAX - depth) / (NAMETAG_DEPTH_MAX - NAMETAG_DEPTH_FADE);

  tag->vtbl = l_nametagVtbl;
  tag->x = screen.x;
  tag->y = screen.y - NAMETAG_BASELINE_UP;
  tag->px = NAMETAG_PX_NEAR + (NAMETAG_PX_FAR - NAMETAG_PX_NEAR) * t;
  tag->alpha = (u32)alpha;
  if (tag->alpha == 0)
    return;

  /* dComIfGd_set2DOpa (d_com_inf_game.h:3915): false (ignored) when the list is full. */
  dDlst_list_c__set((dDlst_list_c *)drawlist, DDLST_LIST_MP2DOPA_P(drawlist), DDLST_LIST_MP2DOPAEND_P(drawlist),
                    (dDlst_base_c *)tag);
}

static void nametag_setColor(u8 *font, u32 rgba)
{
  u32 *colors = JUTFONT_COLORS(font);
  colors[0] = colors[1] = colors[2] = colors[3] = rgba;
}

/* dDlst_base_c::draw, in the painter's 2D pass (the current graf port is the painter's J2DOrthoGraph). */
static void nametag_draw(PuppetNameTag *tag)
{
  u8 *font = (u8 *)mDoExt_font0;
  J2DOrthoGraph *graf = GAMEINFO_CURRENT_GRAF_PORT(&g_dComIfG_gameInfo);
  if ((u32)font < 0x80000000 || (u32)graf < 0x80000000 || !JUTFONT_VALID(font))
    return;
  void **vt = *(void ***)font;
  if ((u32)vt < 0x80000000)
    return;
  int cellW = ((NametagFontIntFn)vt[JUTFONT_VT_GET_CELL_WIDTH])(font);
  int cellH = ((NametagFontIntFn)vt[JUTFONT_VT_GET_CELL_HEIGHT])(font);
  if (cellW <= 0 || cellH <= 0)
    return;

  /* drawChar_scale advances each glyph by TWidth.width * (scaleX / cellWidth) (JUTResFont.cpp:244-251). */
  f32 scale = tag->px / (f32)cellH;
  f32 width = 0.0f;
  u32 len = 0;
  for (; tag->name[len] != 0; len++)
  {
    u8 w[2];
    ((NametagFontWidthFn)vt[JUTFONT_VT_GET_WIDTH_ENTRY])(font, (u8)tag->name[len], w);
    width += (f32)w[1] * scale;
  }
  f32 x = tag->x - width * 0.5f;
  f32 cw = (f32)cellW * scale;
  f32 ch = (f32)cellH * scale;

  /* Every game 2D drawer starts from the port's state (d_2dnumber.cpp:36-39); then the font's
   * TEV/vertex setup, as JUTDbPrint::flush. The message font's colours are put back afterwards. */
  J2DOrthoGraph__setPort(graf);
  ((NametagFontVoidFn)vt[JUTFONT_VT_SET_GX])(font);
  u32 saved[4];
  for (int i = 0; i < 4; i++)
    saved[i] = JUTFONT_COLORS(font)[i];

  nametag_setColor(font, (tag->alpha * 3) / 4); /* black shadow */
  JUTFont__drawString_size_scale((JUTFont *)font, x + NAMETAG_SHADOW, tag->y + NAMETAG_SHADOW, cw, ch, tag->name, len, true);
  nametag_setColor(font, 0xFFFFFF00 | tag->alpha); /* white */
  JUTFont__drawString_size_scale((JUTFont *)font, x, tag->y, cw, ch, tag->name, len, true);

  for (int i = 0; i < 4; i++)
    JUTFONT_COLORS(font)[i] = saved[i];
  /* drawChar_scale leaves POS as F32 (JUTResFont.cpp:264): hand the next 2D drawer the port's state. */
  J2DOrthoGraph__setPort(graf);
}
