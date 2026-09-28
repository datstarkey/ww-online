/**
 * puppet_appearance.c - per-Link outfit + tunic colour. See puppet_appearance.h for why this works
 * at entry time. Included by puppet.c (one translation unit) before puppet_draw.c.
 *
 * Header ownership (the linktexS3TC ResTIMG in TEX1, "the slot"):
 *   - the REL rewrites it every frame with the LOCAL Link's look, and keeps the local Link's
 *     mOtherLinktex pristine (the other outfit, original image) so vanilla code that swaps the two
 *     (playerDelete d_a_player_main.cpp:11786, demo shapes :10343, playerInit :12356) stays correct;
 *   - each puppet writes its own look into it around its body entry and restores it after
 *     (puppet_draw.c);
 *   - nothing else writes it: the C# client only publishes LOCAL_APPEARANCE_WORD.
 *
 * Tunic colour = a recoloured copy of the hero texture (160x96 CMPR, cl.bdl TEX1): every green
 * DXT1 endpoint is replaced by the chosen colour scaled by the endpoint's luminance, so the shading
 * survives and dark/black and saturated colours come out as picked (a multiply tint could only
 * darken the green). Skin, hair, belt and eyes are not green and are untouched. Casual clothes are
 * never recoloured. Copies live in "look blocks" on the game heap: one per puppet (freed with it)
 * and one for the local Link, which outlives the REL (the header keeps pointing at it) and is
 * adopted by the next REL instance via LOCAL_APPEARANCE_BLOCK_ADDR.
 */

#include "puppet_appearance.h"

/* linktexS3TC / linktexbci4 as shipped (vanilla Link.arc: cl.bdl TEX1 #34, linktexbci4.bti) */
#define APP_FMT_CMPR 0x0E /* GX_TF_CMPR: hero texture */
#define APP_FMT_C4 0x08   /* GX_TF_C4: casual texture */
#define APP_TEX_W 160
#define APP_TEX_H 96
#define APP_IMAGE_BYTES ((APP_TEX_W * APP_TEX_H) / 2) /* 4 bpp: 0x1E00, a multiple of 32 */

/* Recolour: an RGB565 endpoint is "tunic green" when G beats R and B by this much (the tunic's
 * greens are 5d89/24e7/2443 and the lime trim af08/8566; hair/skin/belt have R >= G). */
#define APP_GREEN_MARGIN 24
#define APP_TUNIC_REF_LUM 139 /* luminance of the main tunic green (90,178,74) = the picked colour */

/* Look block (game heap, 32-aligned): header + two image variants (double-buffered, because the
 * GPU may still be reading last frame's while a new colour is written). */
#define APP_BLOCK_MAGIC 0x4C4F4F4B /* "LOOK" */
#define APP_BLK_MAGIC(b) (*(u32 *)((u8 *)(b) + 0x00))
#define APP_BLK_SLOT(b) (*(u32 *)((u8 *)(b) + 0x04))     /* TEX1 header it was made for */
#define APP_BLK_PRISTINE(b) (*(u32 *)((u8 *)(b) + 0x08)) /* that header's own imageOffset */
#define APP_BLK_KEY(b) (*(u32 *)((u8 *)(b) + 0x0C))      /* APP_KEY(rgb) held by variant CUR, 0 = none */
#define APP_BLK_CUR(b) (*(u32 *)((u8 *)(b) + 0x10))
#define APP_BLK_HDR_BYTES 0x20
#define APP_BLK_VARIANT(b, i) ((u8 *)(b) + APP_BLK_HDR_BYTES + (i) * APP_IMAGE_BYTES)
#define APP_BLK_BYTES (APP_BLK_HDR_BYTES + 2 * APP_IMAGE_BYTES)
#define APP_KEY(rgb) ((rgb) | 0x01000000)

#define APP_DEFAULT_RGB (((u32)TUNIC_COLOR_DEFAULT_R << 16) | ((u32)TUNIC_COLOR_DEFAULT_G << 8) | TUNIC_COLOR_DEFAULT_B)

typedef void *(*AppAllocFn)(u32 size, int alignment, void *heap); /* JKRHeap::alloc(u32, int, JKRHeap*) JKRHeap.cpp:122 */
typedef void (*AppStoreFn)(void *start, u32 bytes);               /* DCStoreRange */

static u32 *l_appSlot;                  /* TEX1 linktexS3TC header (daPy_lk_c::mpCurrLinktex) */
static u32 l_appHero[RESTIMG_WORDS];    /* pristine hero header, offsets relative to l_appSlot */
static u32 l_appCasual[RESTIMG_WORDS];  /* pristine casual header, same */
static u32 l_appValid;
static u8 *l_appLocalBlock;
static u32 l_appLiveCount;
static u32 l_appHasRun;
static u32 l_appLastFrame;

static int app_isValidPtr(const void *p)
{
  u32 a = (u32)p;
  return a >= 0x80000000 && a < 0x84000000 && (a & 3) == 0; /* MEM1, 48MB bi2 */
}

static void app_copy(u32 *dst, const u32 *src)
{
  int i;
  for (i = 0; i < RESTIMG_WORDS; i++)
    dst[i] = src[i];
}

static int app_isFormat(const u32 *timg, u8 format)
{
  return RESTIMG_FORMAT(timg) == format && RESTIMG_WIDTH(timg) == APP_TEX_W && RESTIMG_HEIGHT(timg) == APP_TEX_H;
}

/* Image a header copy points at once written to the slot. */
static const u8 *app_imageOf(const u32 *timg)
{
  return (const u8 *)l_appSlot + RESTIMG_IMAGE_OFFSET(timg);
}

static int app_blockHolds(const u8 *block, const u8 *image)
{
  return image >= APP_BLK_VARIANT(block, 0) && image < APP_BLK_VARIANT(block, 2);
}

static u8 *app_newBlock(void)
{
  u8 *block = (u8 *)((AppAllocFn)JKRHeap__alloc)(APP_BLK_BYTES, 32, mDoExt_getGameHeap());
  if (!block)
    return NULL;
  APP_BLK_MAGIC(block) = APP_BLOCK_MAGIC;
  APP_BLK_SLOT(block) = (u32)l_appSlot;
  APP_BLK_PRISTINE(block) = RESTIMG_IMAGE_OFFSET(l_appHero);
  APP_BLK_KEY(block) = 0;
  APP_BLK_CUR(block) = 0;
  return block;
}

static void app_freeBlock(u8 *block)
{
  if (block)
    JKRHeap__free(block, (JKRHeap *)mDoExt_getGameHeap());
}

static u32 app_scale(u32 target, u32 lum)
{
  u32 v = (target * lum) / APP_TUNIC_REF_LUM;
  return v > 255 ? 255 : v;
}

static u16 app_tint(u16 c, u32 rgb)
{
  u32 r = (c >> 11) & 0x1F;
  u32 g = (c >> 5) & 0x3F;
  u32 b = c & 0x1F;
  r = (r << 3) | (r >> 2);
  g = (g << 2) | (g >> 4);
  b = (b << 3) | (b >> 2);
  u32 other = r > b ? r : b;
  if (g < other + APP_GREEN_MARGIN)
    return c;
  u32 lum = (77 * r + 150 * g + 29 * b) >> 8;
  r = app_scale((rgb >> 16) & 0xFF, lum);
  g = app_scale((rgb >> 8) & 0xFF, lum);
  b = app_scale(rgb & 0xFF, lum);
  return (u16)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
}

/* Recolour a CMPR (DXT1) image block by block. Endpoint order selects the block mode
 * (c0 > c1: 4 colours, else 3 + transparent), so keep it and remap the indices when a swap is needed. */
static void app_recolor(u8 *dst, const u8 *src, u32 rgb)
{
  u32 o;
  for (o = 0; o < APP_IMAGE_BYTES; o += 8)
  {
    u16 c0 = *(const u16 *)(src + o);
    u16 c1 = *(const u16 *)(src + o + 2);
    u32 bits = *(const u32 *)(src + o + 4);
    u16 n0 = app_tint(c0, rgb);
    u16 n1 = app_tint(c1, rgb);
    if (c0 > c1)
    {
      if (n0 < n1)
      {
        u16 t = n0;
        n0 = n1;
        n1 = t;
        bits ^= 0x55555555; /* 0<->1, 2<->3 */
      }
      else if (n0 == n1)
      {
        /* All four palette entries are now n0, so every texel is n0: use index 0 only (valid in
         * either mode, never the 3-colour transparent index). Nudging an endpoint by 1 instead
         * borrows across RGB565 fields when B is 0 (e.g. 0x0820 - 1 = 0x081F: full blue). */
        bits = 0;
      }
    }
    else if (n0 > n1)
    {
      u16 t = n0;
      n0 = n1;
      n1 = t;
      bits ^= (~bits >> 1) & 0x55555555; /* 0<->1; 2 (midpoint) and 3 (transparent) stay */
    }
    *(u16 *)(dst + o) = n0;
    *(u16 *)(dst + o + 2) = n1;
    *(u32 *)(dst + o + 4) = bits;
  }
}

/* The recoloured hero image for rgb, or NULL for the vanilla texture (default colour, no memory). */
static const u8 *app_imageFor(u8 **blockp, u32 rgb)
{
  if (!l_appValid || rgb == 0 || rgb == APP_DEFAULT_RGB)
    return NULL;
  u8 *block = *blockp;
  if (!block)
  {
    block = app_newBlock();
    if (!block)
      return NULL;
    *blockp = block;
  }
  u32 cur = APP_BLK_CUR(block) & 1;
  if (APP_BLK_KEY(block) == APP_KEY(rgb))
    return APP_BLK_VARIANT(block, cur);
  if (APP_BLK_KEY(block) != 0)
    cur ^= 1;
  u8 *dst = APP_BLK_VARIANT(block, cur);
  app_recolor(dst, (const u8 *)l_appSlot + APP_BLK_PRISTINE(block), rgb);
  ((AppStoreFn)os__DCStoreRange)(dst, APP_IMAGE_BYTES);
  APP_BLK_CUR(block) = cur;
  APP_BLK_KEY(block) = APP_KEY(rgb);
  return dst;
}

static void app_writeHeader(u32 *dst, int casual, const u8 *image)
{
  app_copy(dst, casual ? l_appCasual : l_appHero);
  if (image && !casual)
    RESTIMG_IMAGE_OFFSET(dst) = (u32)image - (u32)l_appSlot;
}

/* Learn the pristine headers from the real Link: the slot holds its current outfit, mOtherLinktex
 * the other one (playerInit :12356-12373). */
static int app_capture(daPy_lk_c *real)
{
  u32 *slot = (u32 *)DAPY_LK_MPCURRLINKTEX(real);
  if (l_appValid && slot == l_appSlot)
    return 1;
  l_appValid = 0;
  if (!app_isValidPtr(slot))
    return 0;
  u32 *other = (u32 *)DAPY_LK_MOTHERLINKTEX(real);
  int casual = (DAPY_PY_NO_RESET_FLG1(real) & DAPY_FLG1_CASUAL_CLOTHES) != 0;
  l_appSlot = slot;
  app_copy(l_appHero, casual ? other : slot);
  app_copy(l_appCasual, casual ? slot : other);

  if (l_appLocalBlock && APP_BLK_SLOT(l_appLocalBlock) != (u32)slot)
    l_appLocalBlock = NULL; /* made for another TEX1: never seen in play; leak it rather than guess */

  /* The hero header may still show the local recolour (it outlives the REL). Adopt the block an
   * earlier REL instance left behind only while this Link's hero header points into it (after a
   * game reboot TEX1 is fresh from disc and never does), and take the pristine offset from it. */
  u8 *block = l_appLocalBlock;
  if (!block)
  {
    block = (u8 *)*(volatile u32 *)LOCAL_APPEARANCE_BLOCK_ADDR;
    if (!app_isValidPtr(block) || APP_BLK_MAGIC(block) != APP_BLOCK_MAGIC || APP_BLK_SLOT(block) != (u32)slot)
      block = NULL;
  }
  if (block && app_blockHolds(block, app_imageOf(l_appHero)))
  {
    RESTIMG_IMAGE_OFFSET(l_appHero) = APP_BLK_PRISTINE(block);
    l_appLocalBlock = block;
  }
  *(volatile u32 *)LOCAL_APPEARANCE_BLOCK_ADDR = (u32)l_appLocalBlock;

  if (!app_isFormat(l_appHero, APP_FMT_CMPR) || !app_isFormat(l_appCasual, APP_FMT_C4))
    return 0;
  l_appValid = 1;
  return 1;
}

/* APPEARANCE_CLOTHES_DEFAULT = playerInit's rule (d_a_player_main.cpp:12362) evaluated live, so the
 * hero's-clothes flag arriving mid-visit (shared story) switches the outfit now, not on the next load. */
static int app_wantCasual(u32 clothes)
{
  if (clothes == APPEARANCE_CLOTHES_HERO)
    return 0;
  if (clothes == APPEARANCE_CLOTHES_CASUAL)
    return 1;
  u8 bits = GAMEINFO_EVENT_BITS(&g_dComIfG_gameInfo)[EVENT_BIT_HERO_CLOTHES >> 8];
  return (bits & (EVENT_BIT_HERO_CLOTHES & 0xFF)) == 0 || GAMEINFO_CLEAR_COUNT(&g_dComIfG_gameInfo) != 0;
}

void puppet_appearance_tick(void)
{
  u32 frame = SCOMPONENT_FRAME_COUNTER();
  if (l_appHasRun && frame == l_appLastFrame)
    return;
  l_appHasRun = 1;
  l_appLastFrame = frame;

  daPy_lk_c *real = (daPy_lk_c *)dComIfGp_getPlayer(0);
  if (!real || !app_capture(real))
  {
    *(volatile u32 *)LOCAL_APPEARANCE_STATUS_ADDR = 0;
    return;
  }

  u32 word = (u32)APPEARANCE_CLOTHES_DEFAULT << 24;
  if (*(volatile u32 *)LOCAL_APPEARANCE_TAG_ADDR == LOCAL_APPEARANCE_MAGIC)
    word = *(volatile u32 *)LOCAL_APPEARANCE_WORD_ADDR;

  u32 status = LOCAL_APPEARANCE_STATUS_ACTIVE;
  int casual = (DAPY_PY_NO_RESET_FLG1(real) & DAPY_FLG1_CASUAL_CLOTHES) != 0;
  if (app_wantCasual(word >> 24) != casual)
  {
    /* Same change as the demo shape swap (:10343-10356); cutscenes do their own, so wait for them. */
    if (GAMEINFO_EVT_MODE(&g_dComIfG_gameInfo) == 0)
    {
      DAPY_PY_NO_RESET_FLG1(real) ^= DAPY_FLG1_CASUAL_CLOTHES;
      casual = !casual;
    }
    else
    {
      status |= LOCAL_APPEARANCE_STATUS_DEFERRED;
    }
  }

  const u8 *image = casual ? NULL : app_imageFor(&l_appLocalBlock, word & 0xFFFFFF);
  app_writeHeader(l_appSlot, casual, image);
  app_writeHeader((u32 *)DAPY_LK_MOTHERLINKTEX(real), !casual, NULL);

  *(volatile u32 *)LOCAL_APPEARANCE_BLOCK_ADDR = (u32)l_appLocalBlock;
  *(volatile u32 *)LOCAL_APPEARANCE_IMAGE_ADDR = (u32)image;
  *(volatile u32 *)LOCAL_APPEARANCE_APPLIED_ADDR = image ? (word & 0xFFFFFF) : 0;
  if (casual)
    status |= LOCAL_APPEARANCE_STATUS_CASUAL;
  if (image)
    status |= LOCAL_APPEARANCE_STATUS_COLOR;
  *(volatile u32 *)LOCAL_APPEARANCE_STATUS_ADDR = status;
}

void puppet_appearance_begin(u32 slotIndex, int casual, u8 **lookBlock, PuppetLinktexSwap *swap)
{
  swap->slot = NULL;
  if (!l_appValid || slotIndex >= PUPPET_MAX_SLOTS)
    return;
  volatile u8 *slotData = (volatile u8 *)PUPPET_SLOT_BASE(slotIndex);
  u32 rgb = ((u32)slotData[PUPPET_SLOT_OFF_COLOR_R] << 16) | ((u32)slotData[PUPPET_SLOT_OFF_COLOR_G] << 8) |
            (u32)slotData[PUPPET_SLOT_OFF_COLOR_B];
  const u8 *image = casual ? NULL : app_imageFor(lookBlock, rgb);
  app_copy(swap->saved, l_appSlot);
  swap->slot = l_appSlot;
  app_writeHeader(l_appSlot, casual, image);
}

void puppet_appearance_end(PuppetLinktexSwap *swap)
{
  if (swap->slot)
  {
    app_copy(swap->slot, swap->saved);
    swap->slot = NULL;
  }
}

void puppet_appearance_onCreate(void)
{
  l_appLiveCount++;
}

/* Does either local header (the slot, or the real Link's mOtherLinktex; both relative to the slot)
 * still point into the local block? Errs towards "yes": a wrong yes only keeps 15KB alive. */
static int app_localBlockInUse(void)
{
  if (!l_appSlot)
    return 0;
  if (app_blockHolds(l_appLocalBlock, app_imageOf(l_appSlot)))
    return 1;
  daPy_lk_c *real = (daPy_lk_c *)dComIfGp_getPlayer(0);
  return app_isValidPtr(real) && (u32 *)DAPY_LK_MPCURRLINKTEX(real) == l_appSlot &&
         app_blockHolds(l_appLocalBlock, app_imageOf((const u32 *)DAPY_LK_MOTHERLINKTEX(real)));
}

void puppet_appearance_onDelete(u8 **lookBlock, int counted)
{
  app_freeBlock(*lookBlock);
  *lookBlock = NULL;
  if (!counted)
    return;
  if (l_appLiveCount > 0)
    l_appLiveCount--;
  if (l_appLiveCount != 0)
    return;

  /* Last puppet: DynamicModuleControlBase::unlink (DynamicLink.cpp:94-98) unloads the REL next.
   * The local look needs no REL code, so it stays; keep the local block only while the shared
   * header or the real Link's mOtherLinktex still shows it (then the next REL instance adopts it),
   * else free it. (A demo shape swap after this frame's tick moves the recoloured hero header into
   * mOtherLinktex, :10343-10350; freeing then would leave it pointing at freed heap.) */
  if (l_appLocalBlock && !app_localBlockInUse())
  {
    app_freeBlock(l_appLocalBlock);
    l_appLocalBlock = NULL;
    *(volatile u32 *)LOCAL_APPEARANCE_BLOCK_ADDR = 0;
    *(volatile u32 *)LOCAL_APPEARANCE_IMAGE_ADDR = 0;
    *(volatile u32 *)LOCAL_APPEARANCE_APPLIED_ADDR = 0;
  }
  *(volatile u32 *)LOCAL_APPEARANCE_STATUS_ADDR = 0;
}
