/**
 * puppet_boatlook.c - the local King of Red Lions in the local player's colours (see puppet_boatlook.h).
 * Included by puppet.c after puppet_seachart.c.
 */

#include "puppet_boatlook.h"

#define BL_PAL(p)   (*(u32 *)((p) + BOATLOOK_P_OFF_PAL))
#define BL_KEY(p)   (*(u32 *)((p) + BOATLOOK_P_OFF_KEY))
#define BL_SUM(p)   (*(u32 *)((p) + BOATLOOK_P_OFF_SUM))
#define BL_COUNT(p) (*(u32 *)((p) + BOATLOOK_P_OFF_COUNT))
#define BL_RES(p)   (*(u32 *)((p) + BOATLOOK_P_OFF_RES))

static u32 l_boatlookLastFrame;
static u32 l_boatlookHasRun;

/* The published block, or NULL (also when it isn't TLUT-aligned: never the case for one we made). */
static u8 *boatlook_block(void)
{
  u8 *block = puppet_bootBlock(BOATLOOK_PTR_ADDR, BOATLOOK_MAGIC, BOATLOOK_BLOCK_SIZE);
  return block != NULL && ((u32)block % BOATLOOK_ALIGN) == 0 ? block : NULL;
}

static u8 *boatlook_state(u8 *block, int part)
{
  return block + (part == BOATLOOK_PART_HULL ? BOATLOOK_OFF_HULL : BOATLOOK_OFF_SAIL);
}

static u16 *boatlook_vanilla(u8 *block, int part)
{
  return (u16 *)(block + (part == BOATLOOK_PART_HULL ? BOATLOOK_OFF_HULL_VANILLA : BOATLOOK_OFF_SAIL_VANILLA));
}

static u16 *boatlook_live(const u32 *timg)
{
  return (u16 *)((u8 *)timg + TIMG_PAL_OFFSET(timg));
}

static u32 boatlook_sum(const u16 *pal, u32 count)
{
  u32 s = 0;
  u32 i;
  for (i = 0; i < count; i++)
    s = s * BOATLOOK_SUM_MUL + pal[i];
  return s;
}

static int boatlook_isPalette(const u32 *timg)
{
  return TIMG_FORMAT(timg) == TIMG_FMT_C8 && TIMG_PAL_FORMAT(timg) == TIMG_TLUT_RGB565 &&
         TIMG_NUM_COLORS(timg) != 0 && TIMG_NUM_COLORS(timg) <= BOATLOOK_PAL_ENTRIES;
}

static const u16 *boatlook_source(int part, const u32 *timg)
{
  u8 *block = boatlook_block();
  u16 *live = boatlook_live(timg);
  if (block != NULL && BL_PAL(boatlook_state(block, part)) == (u32)live)
    return boatlook_vanilla(block, part);
  return live;
}

static const u16 *boatlook_vanillaShown(int part, const u32 *timg)
{
  u8 *block = boatlook_block();
  u8 *p;
  if (block == NULL)
    return NULL;
  p = boatlook_state(block, part);
  return BL_PAL(p) == (u32)boatlook_live(timg) && BL_KEY(p) != 0 ? boatlook_vanilla(block, part) : NULL;
}

/* The mounted "Ship" archive's dRes_info_c, or NULL (dComIfG_getObjectRes would OSReport_Error every frame
 * without it). */
static dRes_info_c *boatlook_info(void)
{
  dRes_info_c *info = dRes_control_c__getResInfo(BOAT_ARC_NAME, GAMEINFO_RES_OBJECT_INFO(&g_dComIfG_gameInfo),
                                                 GAMEINFO_RES_OBJECT_INFO_COUNT);
  if (info == NULL || DRES_INFO_ARCHIVE(info) == NULL || DRES_INFO_RES(info) == NULL)
    return NULL;
  return info;
}

/* Keep one part's live palettes (pals[0..n): the reference's and its identical copies) in want, a colour
 * for recolour (0 = vanilla). A palette that isn't what we last left there is new from disc: vanilla.
 * Written in place, so there is no second buffer as for the peers' palettes: a frame the GPU is still
 * loading the old one may show it torn, once per colour change. */
static void boatlook_apply(u8 *block, int part, void *res, const u32 *ref, u16 **pals, u32 n, u32 want)
{
  AppStoreFn store = (AppStoreFn)(u32)os__DCStoreRange;
  u8 *p = boatlook_state(block, part);
  u16 *van = boatlook_vanilla(block, part);
  u16 *live = boatlook_live(ref);
  u32 count = TIMG_NUM_COLORS(ref);
  u32 key = want != 0 ? (want | BOATLOOK_KEY_SET) : 0;
  u32 expect = BL_KEY(p) != 0 ? BL_SUM(p) : boatlook_sum(van, BL_COUNT(p));
  u32 i, j;

  if (BL_PAL(p) != (u32)live || BL_COUNT(p) != count || boatlook_sum(live, count) != expect)
  {
    for (i = 0; i < BOATLOOK_PAL_ENTRIES; i++)
      van[i] = i < count ? live[i] : 0;
    store(van, BOATLOOK_PAL_ENTRIES * 2);
    BL_PAL(p) = (u32)live;
    BL_COUNT(p) = count;
    BL_KEY(p) = 0;
    BL_SUM(p) = boatlook_sum(van, count);
  }
  BL_RES(p) = (u32)res;
  if (BL_KEY(p) == key)
    return;

  for (j = 0; j < n; j++)
  {
    if (want == 0)
      for (i = 0; i < count; i++)
        pals[j][i] = van[i];
    else if (part == BOATLOOK_PART_HULL)
      boat_hullRecolor(pals[j], van, count, count, want);
    else
      boat_sailRecolor(pals[j], van, count, count, want);
    store(pals[j], count * 2);
  }
  BL_KEY(p) = key;
  BL_SUM(p) = boatlook_sum(live, count);
}

static void boatlook_hull(u8 *block, dRes_info_c *info)
{
  J3DModelData *data = (J3DModelData *)DRES_INFO_RES(info)[BOATLOOK_RES_HULL];
  u8 *base;
  u32 *ref;
  u16 *pals[HULL_MAX_HEADERS];
  u16 *pal;
  u32 n, i, j, count = 0;

  if (data == NULL || (n = boat_texHeaders(data, &ref)) == 0 || ref == NULL || !boatlook_isPalette(ref))
    return;
  // fn_main1 is listed twice (puppet_boat.c "Hull colour"): every header with the reference's colours.
  base = (u8 *)J3DMODELDATA_MMATERIALTABLE_MPTEXTURE(data)->mpTexData;
  for (i = 0; i < n; i++)
  {
    if (!boat_hullSame(base + i * 0x20, ref))
      continue;
    pal = boatlook_live((u32 *)(base + i * 0x20));
    for (j = 0; j < count && pals[j] != pal; j++)
      ;
    if (j == count)
      pals[count++] = pal;
  }
  boatlook_apply(block, BOATLOOK_PART_HULL, data, ref, pals, count,
                 *(volatile u16 *)(block + BOATLOOK_OFF_WANT_HULL));
}

static void boatlook_sail(u8 *block, dRes_info_c *info)
{
  u32 *timg = (u32 *)DRES_INFO_RES(info)[BOATLOOK_RES_SAIL];
  u32 rgb = *(volatile u32 *)(block + BOATLOOK_OFF_WANT_SAIL) & 0xFFFFFF;
  u16 *pal;

  if (timg == NULL || !boatlook_isPalette(timg))
    return;
  pal = boatlook_live(timg);
  boatlook_apply(block, BOATLOOK_PART_SAIL, timg, timg, &pal, 1, rgb == APP_DEFAULT_RGB ? 0 : rgb);
}

void puppet_boatlook_tick(void)
{
  u32 frame = SCOMPONENT_FRAME_COUNTER();
  dRes_info_c *info;
  u8 *block;

  if (l_boatlookHasRun && frame == l_boatlookLastFrame)
    return;
  l_boatlookHasRun = 1;
  l_boatlookLastFrame = frame;

  block = boatlook_block();
  info = boatlook_info();
  if (block == NULL || info == NULL)
    return;
  *(u32 *)(block + BOATLOOK_OFF_INFO) = (u32)info;
  boatlook_hull(block, info);
  boatlook_sail(block, info);
}

void puppet_boatlook_onCreate(void)
{
  puppet_bootBlockCreateAligned(BOATLOOK_PTR_ADDR, BOATLOOK_MAGIC, BOATLOOK_BLOCK_SIZE, BOATLOOK_ALIGN);
}
