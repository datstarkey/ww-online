/**
 * puppet_anmmirror.c - the peer's own anims for the procs the REL has no _init for (see
 * puppet_anmmirror.h).
 *
 * Every proc id the peer can be in used to fall back to WAIT unless puppet_applyProcInit knows it,
 * so side hops, slides, knock-backs, deaths, cutscene poses... showed a puppet standing still.
 * Instead, for such a proc the puppet enters WAIT (so the body's state is a plain, safe one) and
 * plays what the peer's Link plays: the C# client samples the peer's anim heaps and frame controls
 * (under[0..1], upper[0..1]: bck, frame, rate, start / end / loop, attribute, blend ratio), face
 * btp / btk + frames and hand shapes into the anim mirror block (puppet_shared.h PUPPET_ANM_*).
 *
 * The bcks are loaded the way getAnimeResource does (d_a_player_main.cpp:593-603) into the puppet's
 * own anm heaps, checked first: an LkAnm bck index only, and a whole 'J3D1bck1' file no bigger than
 * the buffer region it is read into. createHeap gives all four body tracks ONE 0xB400 buffer:
 * under[0] at +0, under[1] at +0x2400, upper[0] at +0x4800, upper[i] at +0x4800 + i * 0x2400
 * (d_a_player_main.cpp:12029, 12173-12180), and a load overwrites as much of it as the file needs
 * (setSingleMoveAnime reads under[0] with a 0xB400 cap and drops under[1] / upper[1] for it,
 * :12786-12789). So a track is only loaded where the tracks before it left its region alone, reads
 * are capped at its region's end (JKRArchive fetchResource never writes past the cap), and upper[2]
 * (+0x9000: puppet_held.c's item anims, checkItemAction's draw / sheathe) is never written.
 *
 * mCurProc stays WAIT the whole time. Relabelling it with the peer's proc (the puppet_initBoatPose
 * trick) would make the next init's commonProcInit run that proc's exit code for a proc that never
 * started (d_a_player_main.cpp:5725-5766: DEMO_PRESENT kills its event partner item, DEMO_GET_ITEM
 * turns the global item-get colour off, FAN_GLIDE rebuilds models) and FREE_WAIT would even make
 * procWait_init refuse (:6046). The one thing the label bought, a later WAIT request re-initing
 * WAIT, is done here (am_end).
 *
 * All of it runs inside puppet_execute's global guard. REL: literals are fine here.
 */

#include "puppet_anmmirror.h"

// play.mpAnmArchive, the "LkAnm" archive (d_com_inf_game.h:734, play +0x47B0): getAnimeResource
// loads it as lwz r3,0x5A50(&g_dComIfG_gameInfo) (DOL 0x8010438C).
#define AM_GAMEINFO_OFF_ANM_ARCHIVE 0x5A50

// JUTDataFileHeader (JUTDataHeader.h:13-21) of a loaded file
#define AM_HDR_MAGIC    0x4A334431 // "J3D1"
#define AM_HDR_TYPE_BCK 0x62636B31 // "bck1"
#define AM_HDR_MIN_SIZE 0x20       // header + a block header at least

// J3DAnmBase (J3DAnimation.h:328-331)
#define AM_ANM_OFF_ATTR      0x04 // u8
#define AM_ANM_OFF_FRAME_MAX 0x06 // s16
#define AM_ANM_OFF_FRAME     0x08 // f32

// The shared body anm buffer's regions (see the top).
#define AM_REGION_UNDER1 0x2400
#define AM_REGION_UPPER0 0x4800
#define AM_REGION_UPPER1 0x6C00
#define AM_REGION_UPPER2 0x9000 // puppet_held.c's; the end of everything loaded here

#define AM_TRACK_UNDER0 0
#define AM_TRACK_UNDER1 1
#define AM_TRACK_UPPER0 2
#define AM_TRACK_UPPER1 3

// Morf into the peer's bck when under[0] changes: m_HIO->mBasic.m.field_0xC, the morf most of
// Link's anim changes use (d_a_player_HIO_data.inc:5; puppet_held.c HELD_RESET_MORF).
#define AM_MORF 2.4f
// CL joints the old-frame morf covers: all of them (setSingleMoveAnime, d_a_player_main.cpp:12824).
#define AM_MORF_JOINTS 0x2A
// Frames the puppet's own playback may drift from the peer's before it snaps. Samples come ~20
// times a second, a little late but all equally late, and the rate is the peer's.
#define AM_SNAP_FRAMES 4.0f
#define AM_MAX_RATE 100.0f

// daPy_lk_c fields (d_a_player_main.h)
#define AM_DAPY_TEXPAT_ANM(link)    (*(u8 **)((u8 *)(link) + 0x35C)) // J3DAnmTexPattern* mpAnmTexPatternData
#define AM_DAPY_TEXSCROLL_ANM(link) (*(u8 **)((u8 *)(link) + 0x364)) // J3DAnmTextureSRTKey* mpTexScrollResData

typedef struct
{
  u8 active;   // mirroring `proc`
  u8 proc;     // the peer's proc being mirrored
  u8 face;     // bit 0: the peer's btp is on, bit 1: their btk (their frames follow)
  u8 hands[2]; // mLeftHandIdx / mRightHandIdx when the mirror started (WAIT's)
  u32 seq;     // last sample used (PUPPET_ANM_OFF_SEQ)
  u16 want[PUPPET_ANM_TRACKS];            // the tracks the last load asked for
  J3DAnmTransform *anm[PUPPET_ANM_TRACKS]; // what it put in the ratio packs
} AnmMirrorState;

// Per slot: one puppet per slot, GameCube actors run one at a time.
static AnmMirrorState l_anmMirror[PUPPET_MAX_SLOTS];

static u32 am_slot(daPy_lk_c *link)
{
  return *(u32 *)((u8 *)link + sizeof(daPy_lk_c)); // PUPPET_class.slotIndex
}

// This slot's entry in the anim mirror block, or NULL (no block made in this boot).
static volatile u8 *am_entry(u32 slot)
{
  u8 *block;
  if (slot >= PUPPET_MAX_SLOTS)
    return NULL;
  block = puppet_bootBlock(PUPPET_ANM_PTR_ADDR, PUPPET_ANM_MAGIC, PUPPET_ANM_BLOCK_SIZE);
  return block != NULL ? block + PUPPET_ANM_OFF_SLOT0 + slot * PUPPET_ANM_SLOT_SIZE : NULL;
}

static volatile u8 *am_track(volatile u8 *e, int t)
{
  return e + PUPPET_ANM_OFF_TRACK0 + t * PUPPET_ANM_TRACK_SIZE;
}

// The track's ratio pack, anm heap and frame control in the puppet.
static u8 *am_pack(daPy_lk_c *link, int t)
{
  return (u8 *)link + (t < AM_TRACK_UPPER0 ? DAPY_OFF_ANM_RATIO_UNDER0 + t * DAPY_ANM_RATIO_SIZE
                                           : DAPY_OFF_ANM_RATIO_UPPER0 + (t - AM_TRACK_UPPER0) * DAPY_ANM_RATIO_SIZE);
}

static u8 *am_heap(daPy_lk_c *link, int t)
{
  return (u8 *)link + (t < AM_TRACK_UPPER0 ? DAPY_OFF_ANM_HEAP_UNDER0 + t * DAPY_ANM_HEAP_SIZE
                                           : DAPY_OFF_ANM_HEAP_UPPER0 + (t - AM_TRACK_UPPER0) * DAPY_ANM_HEAP_SIZE);
}

static u8 *am_frameCtrl(daPy_lk_c *link, int t)
{
  return (u8 *)link + (t < AM_TRACK_UPPER0 ? DAPY_OFF_FRAME_CTRL_UNDER0 + t * DAPY_FRAME_CTRL_SIZE
                                           : DAPY_OFF_FRAME_CTRL_UPPER0 + (t - AM_TRACK_UPPER0) * DAPY_FRAME_CTRL_SIZE);
}

#define AM_PACK_ANM(link, t) (*(J3DAnmTransform **)(am_pack((link), (t)) + ANM_RATIO_OFF_ANM))
#define AM_PACK_RATIO(link, t) (*(f32 *)(am_pack((link), (t)) + ANM_RATIO_OFF_RATIO))

static int am_isBck(u16 idx)
{
  return idx >= LKANM_BCK_FIRST && idx <= LKANM_BCK_LAST;
}

/**
 * am_loadBck - getAnimeResource (d_a_player_main.cpp:593-603) for track t, reading at most `cap`
 * bytes into its buffer, and loading only a whole bck. Returns the file's size, or 0 (the track's
 * mIdx stays 0xFFFF).
 */
static u32 am_loadBck(daPy_lk_c *link, int t, u16 idx, u32 cap, J3DAnmTransform **out)
{
  u8 *heap = am_heap(link, t);
  u32 *buf = *(u32 **)(heap + ANM_HEAP_OFF_BUFFER);
  JKRSolidHeap *solid = *(JKRSolidHeap **)(heap + ANM_HEAP_OFF_HEAP);
  JKRArchive *arc = *(JKRArchive **)((u8 *)&g_dComIfG_gameInfo + AM_GAMEINFO_OFF_ANM_ARCHIVE);
  JKRHeap *old;
  J3DAnmTransform *anm;
  u32 size;

  *out = NULL;
  if (!am_isBck(idx) || buf == NULL || solid == NULL || arc == NULL)
    return 0;
  if (JKRArchive__readIdxResource(arc, buf, cap, idx) < AM_HDR_MIN_SIZE)
    return 0;
  size = buf[2]; // mFileSize: the whole file's, even when the read stopped at the cap
  if (buf[0] != AM_HDR_MAGIC || buf[1] != AM_HDR_TYPE_BCK || size < AM_HDR_MIN_SIZE || size > cap)
    return 0;
  old = daPy_lk_c__setAnimeHeap(link, solid);
  anm = (J3DAnmTransform *)J3DAnmLoaderDataBase__load(buf);
  mDoExt_setCurrentHeap(old);
  if (anm == NULL)
    return 0;
  *(u16 *)(heap + ANM_HEAP_OFF_IDX) = idx;
  *(u16 *)(heap + ANM_HEAP_OFF_PRI_IDX) = 0xFFFF;
  *out = anm;
  return size;
}

/**
 * am_syncTrack - track t's J3DFrameCtrl as the peer's: attribute, start / end / loop and rate every
 * sample, the frame on a load (snap) or once the puppet's own playback drifted off.
 */
static void am_syncTrack(daPy_lk_c *link, int t, J3DAnmTransform *anm, volatile u8 *tr, int snap)
{
  u8 *fc = am_frameCtrl(link, t);
  s16 max = *(s16 *)((u8 *)anm + AM_ANM_OFF_FRAME_MAX);
  s16 start = *(volatile s16 *)(tr + PUPPET_ANM_TR_OFF_START);
  s16 end = *(volatile s16 *)(tr + PUPPET_ANM_TR_OFF_END);
  s16 loop = *(volatile s16 *)(tr + PUPPET_ANM_TR_OFF_LOOP);
  u8 attr = *(volatile u8 *)(tr + PUPPET_ANM_TR_OFF_ATTR);
  f32 frame = *(volatile f32 *)(tr + PUPPET_ANM_TR_OFF_FRAME);
  f32 rate = *(volatile f32 *)(tr + PUPPET_ANM_TR_OFF_RATE);
  f32 d;

  // Within the bck (J3DFrameCtrl::update only clamps to start / end, J3DAnimation.cpp:143).
  if (end <= 0 || end > max)
    end = max;
  if (start < 0 || start > end)
    start = 0;
  if (loop < 0 || loop > end)
    loop = rate < 0.0f ? end : start;
  if (attr > 4) // J3DFrameCtrl::EMode_LOOP_REVERSE is the last
    attr = *((u8 *)anm + AM_ANM_OFF_ATTR);
  if (!(rate == rate) || rate > AM_MAX_RATE || rate < -AM_MAX_RATE)
    rate = 0.0f;
  if (!(frame >= 0.0f)) // also NaN
    frame = 0.0f;
  else if (frame > (f32)end)
    frame = (f32)end;

  *(fc + FRAME_CTRL_OFF_ATTR) = attr;
  *(s16 *)(fc + FRAME_CTRL_OFF_START) = start;
  *(s16 *)(fc + FRAME_CTRL_OFF_END) = end;
  *(s16 *)(fc + FRAME_CTRL_OFF_LOOP) = loop;
  *(f32 *)(fc + FRAME_CTRL_OFF_RATE) = rate;

  d = *(f32 *)(fc + FRAME_CTRL_OFF_FRAME) - frame;
  if ((attr == 2 || attr == 4) && end > loop) // looping: the short way round
  {
    f32 len = (f32)(end - loop);
    if (d > len * 0.5f)
      d -= len;
    else if (d < -len * 0.5f)
      d += len;
  }
  if (snap || d > AM_SNAP_FRAMES || d < -AM_SNAP_FRAMES)
  {
    *(f32 *)(fc + FRAME_CTRL_OFF_FRAME) = frame;
    *(f32 *)((u8 *)anm + AM_ANM_OFF_FRAME) = frame; // this frame's calc (animeUpdate already ran)
  }
}

// A sample's blend ratio, 0..1.
static f32 am_ratio(volatile u8 *e, int t)
{
  f32 r = *(volatile f32 *)(am_track(e, t) + PUPPET_ANM_TR_OFF_RATIO);
  if (!(r >= 0.0f)) // also NaN
    return 0.0f;
  return r > 1.0f ? 1.0f : r;
}

/**
 * am_follow - frames and ratios of the loaded tracks from a sample. A track has its own frame
 * control unless it plays another one's bck (an upper track on its under track: animeUpdate then
 * skips it, d_a_player_main.cpp:12887-12894). Without a blend partner the ratios are
 * setSingleMoveAnime's 1 / 0 (:12789-12792).
 */
static void am_follow(daPy_lk_c *link, AnmMirrorState *st, volatile u8 *e, int snap)
{
  int t;
  for (t = 0; t < PUPPET_ANM_TRACKS; t++)
  {
    J3DAnmTransform *anm = st->anm[t];
    if (anm != NULL && (t < AM_TRACK_UPPER0 || anm != st->anm[t - AM_TRACK_UPPER0]))
      am_syncTrack(link, t, anm, am_track(e, t), snap);
  }
  AM_PACK_RATIO(link, AM_TRACK_UNDER0) = st->anm[AM_TRACK_UNDER1] ? am_ratio(e, AM_TRACK_UNDER0) : 1.0f;
  AM_PACK_RATIO(link, AM_TRACK_UNDER1) = st->anm[AM_TRACK_UNDER1] ? am_ratio(e, AM_TRACK_UNDER1) : 0.0f;
  AM_PACK_RATIO(link, AM_TRACK_UPPER0) = st->anm[AM_TRACK_UPPER1] ? am_ratio(e, AM_TRACK_UPPER0) : 1.0f;
  AM_PACK_RATIO(link, AM_TRACK_UPPER1) = st->anm[AM_TRACK_UPPER1] ? am_ratio(e, AM_TRACK_UPPER1) : 0.0f;
}

/**
 * am_reinitWait - procWait_init for a puppet that is already in WAIT (the mirror): it refuses in WAIT
 * (d_a_player_main.cpp:6043), so the call is made under the label MOVE, which commonProcInit has no
 * exit code for (:5725-5766). Its setBlendMoveAnime reloads WAIT's bcks (the mirror's mIdx don't match).
 */
static void am_reinitWait(daPy_lk_c *link)
{
  if (DAPY_LK_MCURPROC(link) == PROC_WAIT)
    DAPY_LK_MCURPROC(link) = PROC_MOVE;
  daPy_lk_c__procWait_init(link);
}

/**
 * am_load - put the sample's four tracks in (see the top for the buffer rules), then their frames.
 * want[0] is a bck; want[1] a bck or NONE; want[2..3] a bck or SAME.
 */
static void am_load(daPy_lk_c *link, AnmMirrorState *st, volatile u8 *e, const u16 *want, u32 slot)
{
  J3DAnmTransform *anm[PUPPET_ANM_TRACKS] = {NULL, NULL, NULL, NULL};
  u16 prevUnder = *(u16 *)(am_heap(link, AM_TRACK_UNDER0) + ANM_HEAP_OFF_IDX);
  u32 s0, s2 = 0;
  int t;

  // Whatever the four buffers held is about to be overwritten; each load sets its mIdx again.
  for (t = 0; t < PUPPET_ANM_TRACKS; t++)
  {
    *(u16 *)(am_heap(link, t) + ANM_HEAP_OFF_IDX) = 0xFFFF;
    st->want[t] = want[t];
  }

  s0 = am_loadBck(link, AM_TRACK_UNDER0, want[0], AM_REGION_UPPER2, &anm[0]);
  if (s0 == 0)
  {
    // The buffer may hold part of a file now: back to WAIT's own bcks. m34C3 0: setMoveAnime then
    // doesn't read the old under[0] anm's frame (d_a_player_main.cpp:12716-12720).
    OSReport("[PUPPET] mirror %02x: bck %03x did not load (slot %d)\n", st->proc, want[0], (int)slot);
    st->active = 0;
    DAPY_LK_M34C3(link) = 0;
    am_reinitWait(link);
    return;
  }
  if (am_isBck(want[1]) && s0 <= AM_REGION_UNDER1)
    am_loadBck(link, AM_TRACK_UNDER1, want[1], AM_REGION_UPPER0 - AM_REGION_UNDER1, &anm[1]);
  if (am_isBck(want[2]) && s0 <= AM_REGION_UPPER0)
    s2 = am_loadBck(link, AM_TRACK_UPPER0, want[2], AM_REGION_UPPER2 - AM_REGION_UPPER0, &anm[2]);
  if (am_isBck(want[3]) && s0 <= AM_REGION_UPPER1 && s2 <= AM_REGION_UPPER1 - AM_REGION_UPPER0)
    am_loadBck(link, AM_TRACK_UPPER1, want[3], AM_REGION_UPPER2 - AM_REGION_UPPER1, &anm[3]);
  // An upper track without a bck of its own plays its under track (setSingleMoveAnime :12819,
  // setMoveAnime :12751, 12760).
  if (anm[AM_TRACK_UPPER0] == NULL)
    anm[AM_TRACK_UPPER0] = anm[AM_TRACK_UNDER0];
  if (anm[AM_TRACK_UPPER1] == NULL)
    anm[AM_TRACK_UPPER1] = anm[AM_TRACK_UNDER1];

  for (t = 0; t < PUPPET_ANM_TRACKS; t++)
  {
    AM_PACK_ANM(link, t) = anm[t];
    st->anm[t] = anm[t];
  }
  am_follow(link, st, e, 1);
  if (want[0] != prevUnder && DAPY_LK_M_OLD_FDATA(link) != NULL)
    mDoExt_MtxCalcOldFrame__initOldFrameMorf((mDoExt_MtxCalcOldFrame *)DAPY_LK_M_OLD_FDATA(link), AM_MORF, 0,
                                             AM_MORF_JOINTS);
  // Not a setMoveAnime blend: no step offsets (execute's setStepsOffset reads m34C3 1/4/9/10).
  // (No setSeAnime: the puppet plays no anim sounds, puppet_execute.c leaves setAnimSound out.)
  DAPY_LK_M34C3(link) = 0;
  // No OSReport per load: it fired on every mirrored anim change. A failed load still reports.
}

// setDrawHandModel shows hands.bdl joint mLeftHandIdx / mRightHandIdx (0 = the body's own hand)
// without a range check (d_a_player_main.cpp:1469-1483): only the model's joints are taken.
static void am_hands(daPy_lk_c *link, volatile u8 *e)
{
  J3DModel *hands = DAPY_LK_MPHANDSMODEL(link);
  u8 *idx = (u8 *)link + DAPY_OFF_HAND_IDX;
  u16 n;
  u8 l = *(e + PUPPET_ANM_OFF_HAND_L);
  u8 r = *(e + PUPPET_ANM_OFF_HAND_R);

  if (hands == NULL || J3DMODEL_MPMODELDATA(hands) == NULL)
    return;
  n = J3DMODELDATA_MJOINTTREE_JOINTNUM(J3DMODEL_MPMODELDATA(hands));
  if (l < n)
    idx[0] = l;
  if (r < n)
    idx[1] = r;
}

/**
 * am_face - the peer's face btp / eye btk, loaded like setTextureAnime does (d_a_player_main.cpp:
 * 782-786, 829-834), unless a priority or demo texture anim holds the puppet's face (field_0x2 /
 * field_0x4, which setTextureAnime leaves alone too). setTextureScrollResource also restarts
 * daPy_matAnm_c's eye morf and blink timer (:945-947): statics shared with the local Link, so they
 * are put back.
 */
static void am_face(daPy_lk_c *link, AnmMirrorState *st, volatile u8 *e)
{
  u8 *tp = (u8 *)link + DAPY_OFF_TEX_ANM_HEAP;
  u8 *ts = (u8 *)link + DAPY_OFF_TEX_SCROLL_HEAP;
  u16 btp = *(volatile u16 *)(e + PUPPET_ANM_OFF_TEX_BTP);
  u16 btk = *(volatile u16 *)(e + PUPPET_ANM_OFF_TEX_BTK);

  st->face = 0;
  if (*(u16 *)(tp + ANM_HEAP_OFF_PRI_IDX) == 0xFFFF && *(u16 *)(tp + ANM_HEAP_OFF_DEMO_IDX) == 0xFFFF &&
      btp >= LKANM_BTP_FIRST && btp <= LKANM_BTP_LAST)
  {
    if (*(u16 *)(tp + ANM_HEAP_OFF_IDX) != btp)
    {
      J3DAnmTexPattern *anm = daPy_lk_c__loadTextureAnimeResource(link, btp, 0);
      *(u16 *)(tp + ANM_HEAP_OFF_IDX) = btp;
      if (anm != NULL)
        daPy_lk_c__setTextureAnimeResource(link, anm, *(volatile u16 *)(e + PUPPET_ANM_OFF_TEX_BTP_FRAME));
    }
    st->face |= 1;
  }
  if (*(u16 *)(ts + ANM_HEAP_OFF_PRI_IDX) == 0xFFFF && *(u16 *)(ts + ANM_HEAP_OFF_DEMO_IDX) == 0xFFFF &&
      btk >= LKANM_BTK_FIRST && btk <= LKANM_BTK_LAST)
  {
    if (*(u16 *)(ts + ANM_HEAP_OFF_IDX) != btk)
    {
      // m_maba_timer, m_maba_flg, m_eye_move_flg, m_morf_frame: one byte each, back to back (.sbss, symbols.txt)
      volatile u8 *mat = (volatile u8 *)&daPy_matAnm_c__m_maba_timer;
      u8 saved[4];
      int i;
      J3DAnmTextureSRTKey *anm;
      for (i = 0; i < 4; i++)
        saved[i] = mat[i];
      anm = daPy_lk_c__loadTextureScrollResource(link, btk, 0);
      *(u16 *)(ts + ANM_HEAP_OFF_IDX) = btk;
      if (anm != NULL)
        daPy_lk_c__setTextureScrollResource(link, anm, *(volatile u16 *)(e + PUPPET_ANM_OFF_TEX_BTK_FRAME));
      for (i = 0; i < 4; i++)
        mat[i] = saved[i];
    }
    st->face |= 2;
  }
}

/**
 * am_end - another proc took over. Its init replaced the anims, except for a WAIT request: the
 * mirror already sits in WAIT, so puppet_applyProcInit's WAIT case did nothing. Re-init WAIT then,
 * and give it back its hand shapes (setMoveAnime doesn't set them).
 */
static void am_end(daPy_lk_c *link, AnmMirrorState *st)
{
  st->active = 0;
  st->face = 0;
  if (DAPY_LK_MCURPROC(link) == PROC_WAIT && (l_puppetFollowState & 0xFF) == PROC_WAIT)
  {
    u8 *idx = (u8 *)link + DAPY_OFF_HAND_IDX;
    am_reinitWait(link);
    idx[0] = st->hands[0];
    idx[1] = st->hands[1];
  }
}

// ============================================================================
// ENTRY POINTS
// ============================================================================

void puppet_anmMirrorOnCreate(u32 slotIndex)
{
  puppet_bootBlockCreate(PUPPET_ANM_PTR_ADDR, PUPPET_ANM_MAGIC, PUPPET_ANM_BLOCK_SIZE);
  if (slotIndex < PUPPET_MAX_SLOTS)
    l_anmMirror[slotIndex].active = 0;
}

int puppet_anmMirrorInit(daPy_lk_c *puppet, int proc)
{
  u32 slot = am_slot(puppet);
  AnmMirrorState *st;

  if (slot >= PUPPET_MAX_SLOTS)
    return DAPY_LK_MCURPROC(puppet) == PROC_WAIT ? 1 : daPy_lk_c__procWait_init(puppet);
  st = &l_anmMirror[slot];
  if (DAPY_LK_MCURPROC(puppet) != PROC_WAIT)
  {
    if (!daPy_lk_c__procWait_init(puppet))
      return 0;
    st->active = 0; // a real proc ran: nothing of the mirror is loaded any more
  }
  if (!st->active)
  {
    u8 *idx = (u8 *)puppet + DAPY_OFF_HAND_IDX;
    int t;
    st->hands[0] = idx[0];
    st->hands[1] = idx[1];
    st->face = 0;
    st->seq = 0;
    for (t = 0; t < PUPPET_ANM_TRACKS; t++)
    {
      st->want[t] = PUPPET_ANM_BCK_NONE;
      st->anm[t] = NULL;
    }
  }
  // From one mirrored proc to the next the loaded anims stay: the next sample carries on from them.
  st->active = 1;
  st->proc = (u8)proc;
  return 1;
}

void puppet_anmMirrorTick(daPy_lk_c *puppet, int slotIndex)
{
  AnmMirrorState *st;
  volatile u8 *e;
  u16 want[PUPPET_ANM_TRACKS];
  u32 seq;
  int t, reload;

  if ((u32)slotIndex >= PUPPET_MAX_SLOTS || am_slot(puppet) != (u32)slotIndex)
    return;
  st = &l_anmMirror[slotIndex];
  if (!st->active)
    return;
  if ((l_puppetFollowState & 0xFF) != st->proc || DAPY_LK_MCURPROC(puppet) != PROC_WAIT)
  {
    am_end(puppet, st);
    return;
  }

  // Only new samples change anything; in between the frame controls play on at the peer's rates.
  e = am_entry((u32)slotIndex);
  if (e == NULL)
    return;
  seq = *(volatile u32 *)(e + PUPPET_ANM_OFF_SEQ);
  if (seq == 0 || seq == st->seq || *(e + PUPPET_ANM_OFF_PROC) != st->proc)
    return; // nothing new, or a sample of another proc (C# writes the slot and this block apart)
  st->seq = seq;

  for (t = 0; t < PUPPET_ANM_TRACKS; t++)
  {
    u16 w = *(volatile u16 *)(am_track(e, t) + PUPPET_ANM_TR_OFF_BCK);
    if (!am_isBck(w))
      w = t < AM_TRACK_UPPER0 ? PUPPET_ANM_BCK_NONE : PUPPET_ANM_BCK_SAME;
    want[t] = w;
  }
  // No bck on under[0] (a demo anim from the LkD00 archive, or garbage): keep the current pose.
  if (want[AM_TRACK_UNDER0] == PUPPET_ANM_BCK_NONE)
    return;

  // Reload when the peer's bcks changed, or when something else replaced ours in the meantime.
  reload = 0;
  for (t = 0; t < PUPPET_ANM_TRACKS; t++)
  {
    if (want[t] != st->want[t] || AM_PACK_ANM(puppet, t) != st->anm[t])
      reload = 1;
  }
  if (reload)
    am_load(puppet, st, e, want, (u32)slotIndex);
  else
    am_follow(puppet, st, e, 0);
  if (!st->active) // the load failed and went back to WAIT
    return;
  am_hands(puppet, e);
  am_face(puppet, st, e);
}

// A texture anim frame, clamped to 0..frameMax like playTextureAnime does (d_a_player_main.cpp:1020-1033).
static u16 am_texFrame(u16 f, u8 *anm)
{
  s16 max = *(s16 *)(anm + AM_ANM_OFF_FRAME_MAX);
  if (max <= 0)
    return 0;
  return f > (u16)max ? (u16)max : f;
}

void puppet_anmMirrorFace(daPy_lk_c *puppet)
{
  u32 slot = am_slot(puppet);
  AnmMirrorState *st;
  volatile u8 *e;
  u16 *frames = (u16 *)((u8 *)puppet + DAPY_OFF_TEX_FRAMES);

  if (slot >= PUPPET_MAX_SLOTS)
    return;
  st = &l_anmMirror[slot];
  if (!st->active || st->face == 0 || (e = am_entry(slot)) == NULL)
    return;
  if ((st->face & 1) && AM_DAPY_TEXPAT_ANM(puppet) != NULL)
    frames[0] = am_texFrame(*(volatile u16 *)(e + PUPPET_ANM_OFF_TEX_BTP_FRAME), AM_DAPY_TEXPAT_ANM(puppet));
  if ((st->face & 2) && AM_DAPY_TEXSCROLL_ANM(puppet) != NULL)
    frames[1] = am_texFrame(*(volatile u16 *)(e + PUPPET_ANM_OFF_TEX_BTK_FRAME), AM_DAPY_TEXSCROLL_ANM(puppet));
}
