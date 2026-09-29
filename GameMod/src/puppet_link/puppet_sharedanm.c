/**
 * puppet_sharedanm.c - see puppet_sharedanm.h.
 *
 * Tracked words (every J3DModelData of the "Link" archive: BDL / BDLC / BDLI / BDLM index ranges,
 * GZLE01 Link.h):
 *   - joint 0's J3DJoint::mMtxCalc (+0x58, mDoExt_bckAnm::entry DOL 0x8000F630), one per model
 *   - each material's J3DMaterial::mMaterialAnm (+0x3C, J3DJoint::entryIn DOL 0x802F5968) and, if
 *     set, the J3DMaterialAnm's 26 slots +0x04..+0x68 (mMatColorAnm[2], mTexMtxAnm[8],
 *     mTexNoAnm[8], mTevColorAnm[4], mTevKColorAnm[4]; J3DMaterialAnm::initialize DOL 0x802F3458)
 * 2598 words for the whole archive: 152 materials, 92 of them with a J3DMaterialAnm (79 resident ones
 * + the 13 face materials), 54 joint 0s; counted on a live game with scripts/dolphin-link-anm-check.py.
 *
 * Speed: puppet_shanmRefresh flattens the archive into two tables of field addresses (joint 0
 * mMtxCalc, material mMaterialAnm) and caps them to fit the snapshot, so a window is two straight
 * loops over them plus an unrolled 26-word copy (save) or xor-compare (rebuild) per J3DMaterialAnm.
 * Counted from the compiled loops: ~9 instructions per joint, ~20 per material, 2 per slot word to
 * save and ~4 to compare: ~8k to save + ~14k to rebuild, ~23k per window with the swap list (was ~180k with a
 * call per word). The restore pass follows each material's SAVED anm pointer, so the slots it visits
 * are the local Link's even when the puppet replaced the anm.
 */

#include "puppet_sharedanm.h"

#define SHANM_VT_MODELDATA 0x8039EA34 // __vt__12J3DModelData (J3DModelData ctor DOL 0x802ED150 stores it at +0)
#define SHANM_ANM_SLOTS    26
#define SHANM_MODELS_MAX   64
#define SHANM_MATS_MAX     256        // 152 in GZLE01's Link.arc
#define SHANM_WORDS_MAX    4224       // 2598 live; 4158 if all 152 materials had an anm
#define SHANM_RAM_END      0x83000000 // MEM1 end with use_extra_memory.asm (48 MB)
#define SHANM_REPORT_MAX   6

// X(0) .. X(25): one statement per J3DMaterialAnm slot, unrolled.
#define SHANM_REP26(X)   X(0) X(1) X(2) X(3) X(4) X(5) X(6) X(7) X(8) X(9) X(10) X(11) X(12)   X(13) X(14) X(15) X(16) X(17) X(18) X(19) X(20) X(21) X(22) X(23) X(24) X(25)

// A RAM address, word aligned.
#define SHANM_OK(p) ((u32)(p) - 0x80000000u < SHANM_RAM_END - 0x80000000u && ((u32)(p) & 3) == 0)

// Link.h (tww-decomp assets/GZLE01/res/Object): dRes_INDEX_LINK_BDL_* in the BDL, BDLC, BDLI and
// BDLM folders (cl.bdl = 0x18, hands.bdl = 0x1D, bomb.bdl = 0x3C, ...).
static const u8 l_shanmBdlRanges[][2] = {{0x13, 0x29}, {0x2C, 0x2F}, {0x32, 0x34}, {0x37, 0x4E}};
#define SHANM_PROBE_INDEX 0x18 // cl.bdl

static u32 *l_shanmFirstModel;                   // vtable re-checked per window (the archive is alive)
static u32 l_shanmModelNum;
static u32 *l_shanmJointField[SHANM_MODELS_MAX]; // &joint0->mMtxCalc
static u32 l_shanmJointNum;
static u32 *l_shanmMatField[SHANM_MATS_MAX];     // &material->mMaterialAnm
static u32 l_shanmMatNum;
static u32 l_shanmVal[SHANM_WORDS_MAX];
static u32 l_shanmWords;
static u8 l_shanmCapped;
static u8 l_shanmWarned; // CAPPED / LIST-FULL logged once per refresh
static u8 l_shanmDepth;
static u8 l_shanmLive;   // this window snapshotted (archive alive)
static PuppetSharedAnm *l_shanmCur;
static u32 l_shanmPrev[PUPPET_SHANM_SWAPS_MAX]; // what Begin's install overwrote
static u32 *l_shanmNewAddr[PUPPET_SHANM_SWAPS_MAX];
static u32 l_shanmNewVal[PUPPET_SHANM_SWAPS_MAX];
static u32 l_shanmNewOld[SHANM_REPORT_MAX];
static u32 l_shanmNewNum;
static u8 l_shanmNewLost;

void puppet_shanmInit(PuppetSharedAnm *s, u32 slot)
{
  s->count = 0;
  s->slot = (u16)slot;
}

void puppet_shanmRefresh(void)
{
  int r;
  u32 i;
  l_shanmFirstModel = NULL;
  l_shanmModelNum = 0;
  l_shanmJointNum = 0;
  l_shanmMatNum = 0;
  l_shanmWarned = 0;
  l_shanmCapped = 0;
  // One probe first: getRes logs an error per call while the archive isn't loaded.
  if (dRes_control_c__getRes("Link", SHANM_PROBE_INDEX, GAMEINFO_RES_OBJECT_INFO(&g_dComIfG_gameInfo),
                             GAMEINFO_RES_OBJECT_INFO_COUNT) == NULL)
    return;
  for (r = 0; r < (int)(sizeof(l_shanmBdlRanges) / sizeof(l_shanmBdlRanges[0])); r++)
  {
    for (i = l_shanmBdlRanges[r][0]; i <= l_shanmBdlRanges[r][1] && l_shanmModelNum < SHANM_MODELS_MAX; i++)
    {
      J3DModelData *data = (J3DModelData *)dRes_control_c__getRes("Link", i, GAMEINFO_RES_OBJECT_INFO(&g_dComIfG_gameInfo),
                                                                   GAMEINFO_RES_OBJECT_INFO_COUNT);
      if (!SHANM_OK(data) || *(u32 *)data != SHANM_VT_MODELDATA)
        continue;
      if (l_shanmFirstModel == NULL)
        l_shanmFirstModel = (u32 *)data;
      l_shanmModelNum++;

      J3DMaterial **mats = J3DMODELDATA_MMATERIALTABLE_MPMATERIAL(data);
      u16 matNum = J3DMODELDATA_MMATERIALTABLE_MMATERIALCOUNT(data);
      u16 m;
      if (SHANM_OK(mats))
      {
        for (m = 0; m < matNum && l_shanmMatNum < SHANM_MATS_MAX; m++)
        {
          if (SHANM_OK(mats[m]))
            l_shanmMatField[l_shanmMatNum++] = (u32 *)&J3DMATERIAL_MPMATERIALANM(mats[m]);
        }
      }
      J3DJoint **joints = J3DMODELDATA_MJOINTTREE_MPJOINTS(data);
      if (J3DMODELDATA_MJOINTTREE_JOINTNUM(data) != 0 && SHANM_OK(joints) && SHANM_OK(joints[0]))
        l_shanmJointField[l_shanmJointNum++] = (u32 *)&J3DJOINT_MMTXCALC(joints[0]);
    }
  }
  // Worst case every material has an anm: cap the table once here, not per word in the windows.
  if (l_shanmJointNum + l_shanmMatNum * (1 + SHANM_ANM_SLOTS) > SHANM_WORDS_MAX)
  {
    l_shanmMatNum = (SHANM_WORDS_MAX - l_shanmJointNum) / (1 + SHANM_ANM_SLOTS);
    l_shanmCapped = 1;
  }
}

// Snapshot.
static void shanm_save(void)
{
  u32 *v = l_shanmVal;
  u32 **f = l_shanmJointField;
  u32 n;

  for (n = l_shanmJointNum; n != 0; n--)
    *v++ = **f++;
  f = l_shanmMatField;
  for (n = l_shanmMatNum; n != 0; n--)
  {
    u32 anm = **f++;
    *v++ = anm;
    if (SHANM_OK(anm))
    {
      const u32 *src = (const u32 *)(anm + 4);
#define SHANM_COPY(i) v[i] = src[i];
      SHANM_REP26(SHANM_COPY)
#undef SHANM_COPY
      v += SHANM_ANM_SLOTS;
    }
  }
  l_shanmWords = (u32)(v - l_shanmVal);
}

// A word the puppet changed: note it for the puppet, give the local value back. Rare (a few
// words per window at most), so out of line.
static void shanm_changed(u32 *addr, u32 cur, u32 saved)
{
  if (l_shanmNewNum < PUPPET_SHANM_SWAPS_MAX)
  {
    if (l_shanmNewNum < SHANM_REPORT_MAX)
      l_shanmNewOld[l_shanmNewNum] = saved;
    l_shanmNewAddr[l_shanmNewNum] = addr;
    l_shanmNewVal[l_shanmNewNum] = cur;
    l_shanmNewNum++;
  }
  else
  {
    l_shanmNewLost = 1; // restored for the local Link, but this puppet loses the value
  }
  *addr = saved;
}

static void shanm_rebuild(void)
{
  const u32 *v = l_shanmVal;
  u32 **f = l_shanmJointField;
  u32 n;

  for (n = l_shanmJointNum; n != 0; n--, f++, v++)
  {
    if (**f != *v)
      shanm_changed(*f, **f, *v);
  }
  f = l_shanmMatField;
  for (n = l_shanmMatNum; n != 0; n--, f++)
  {
    u32 saved = *v++;
    if (**f != saved)
      shanm_changed(*f, **f, saved);
    if (SHANM_OK(saved))
    {
      u32 *dst = (u32 *)(saved + 4);
      u32 diff = 0;
#define SHANM_XOR(i) diff |= dst[i] ^ v[i];
      SHANM_REP26(SHANM_XOR)
#undef SHANM_XOR
      if (diff != 0)
      {
        int i;
        for (i = 0; i < SHANM_ANM_SLOTS; i++)
        {
          if (dst[i] != v[i])
            shanm_changed(&dst[i], dst[i], v[i]);
        }
      }
      v += SHANM_ANM_SLOTS;
    }
  }
}

void puppet_shanmBegin(PuppetSharedAnm *s)
{
  int i;
  if (l_shanmDepth++ != 0)
    return;
  l_shanmCur = s;
  // The archive is resident; if it ever went away, don't touch its memory.
  l_shanmLive = l_shanmFirstModel != NULL && *l_shanmFirstModel == SHANM_VT_MODELDATA;
  if (!l_shanmLive)
    return;
  shanm_save();
  for (i = 0; i < s->count; i++)
  {
    l_shanmPrev[i] = *s->addr[i];
    *s->addr[i] = s->val[i];
  }
}

void puppet_shanmEnd(int report)
{
  PuppetSharedAnm *s = l_shanmCur;
  int i;
  u32 old;

  if (l_shanmDepth == 0 || --l_shanmDepth != 0)
    return;
  l_shanmCur = NULL;
  if (!l_shanmLive)
    return;

  l_shanmNewNum = 0;
  l_shanmNewLost = 0;
  shanm_rebuild();
  // A listed word outside this walk (none expected) still gets its pre-install value back.
  for (i = s->count - 1; i >= 0; i--)
    *s->addr[i] = l_shanmPrev[i];

  old = s->count;
  s->count = (u16)l_shanmNewNum;
  for (i = 0; i < (int)l_shanmNewNum; i++)
  {
    s->addr[i] = l_shanmNewAddr[i];
    s->val[i] = l_shanmNewVal[i];
  }

  // Logged only when the list size changes (a take-out adds entries once; installed values stay
  // different from the local ones, so a steady list logs nothing) or on the first cap per refresh.
  if ((l_shanmNewLost || l_shanmCapped) && !l_shanmWarned)
  {
    l_shanmWarned = 1;
    report = 1;
  }
  if (report || old != l_shanmNewNum)
  {
    OSReport("[PUPPET] shared anm slot=%d swaps %d->%d models=%d words=%d%s%s\n", (int)s->slot, (int)old,
             (int)l_shanmNewNum, (int)l_shanmModelNum, (int)l_shanmWords,
             l_shanmCapped ? " CAPPED" : "", l_shanmNewLost ? " LIST-FULL" : "");
    if (report)
    {
      for (i = 0; i < (int)l_shanmNewNum && i < SHANM_REPORT_MAX; i++)
        OSReport("[PUPPET]   %p: local %08x puppet %08x\n", s->addr[i], l_shanmNewOld[i], s->val[i]);
    }
  }
}

// Delete-time check (rare): count tracked words whose value lies in one of the ranges.
static int shanm_scanWord(u32 *addr, const u32 *ranges, int rangeNum, int hits)
{
  u32 cur = *addr;
  int i;
  for (i = 0; i < rangeNum; i++)
  {
    if (cur >= ranges[2 * i] && cur < ranges[2 * i + 1])
    {
      if (hits < SHANM_REPORT_MAX)
        OSReport("[PUPPET]   into puppet: %p = %08x\n", addr, cur);
      return hits + 1;
    }
  }
  return hits;
}

int puppet_shanmScan(const u32 *ranges, int rangeNum, u32 slot)
{
  u32 words = 0;
  int hits = 0;
  u32 i;
  int s;
  if (l_shanmFirstModel != NULL && *l_shanmFirstModel == SHANM_VT_MODELDATA)
  {
    for (i = 0; i < l_shanmJointNum; i++, words++)
      hits = shanm_scanWord(l_shanmJointField[i], ranges, rangeNum, hits);
    for (i = 0; i < l_shanmMatNum; i++, words++)
    {
      u32 anm = *l_shanmMatField[i];
      hits = shanm_scanWord(l_shanmMatField[i], ranges, rangeNum, hits);
      if (SHANM_OK(anm))
      {
        for (s = 0; s < SHANM_ANM_SLOTS; s++, words++)
          hits = shanm_scanWord((u32 *)(anm + 4 + 4 * s), ranges, rangeNum, hits);
      }
    }
  }
  OSReport("[PUPPET] shared anm delete slot=%d models=%d words=%d into-puppet=%d\n", (int)slot,
           (int)l_shanmModelNum, (int)words, hits);
  return hits;
}
