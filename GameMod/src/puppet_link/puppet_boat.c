/**
 * puppet_boat.c - the peer's King of Red Lions, drawn by their puppet (see puppet_boat.h).
 *
 * Per frame (puppet_boatExecute, before puppet_execute):
 *   1. Read the slot's boat block. FLAGS == 0 (or an inactive slot) hides the boat.
 *   2. On first use, load the "Ship" archive and build the hull + head models in our own heap.
 *   3. Predict the network position along the heading (dead reckoning: the sample is up to one
 *      20Hz tick old and a sailing boat covers ~55 units a frame), then ease toward it.
 *   4. Height and tilt from the LOCAL sea, like daShip_c::setYPos / setWaveAngle. The peer's Y
 *      comes from their sea, whose waves don't line up with ours, so it's only used in the air.
 *   5. Hull matrix = T(pos) * ZXYrot(pitch, yaw, roll); the head sits on the hull's GATTAI joint.
 *   6. Pose both morfs like daShip_c::execute (d_a_ship.cpp:3960-3976): the peer's mast / head
 *      bck at the peer's frame, then our joint callback (boat_jointCallBack) for the sail,
 *      tiller, mast scale and head look.
 *   7. Move the sail cloth on the posed mast (boat_sailExecute, see "Sail" below).
 *
 * The models share the "Ship" J3DModelData with the local King of Red Lions, and daShip_c
 * puts its joint callbacks on those shared joints (d_a_ship.cpp:4577-4619). They find their
 * ship through J3DModel::getUserArea(), which is 0 for our models, and the head callbacks don't
 * check it. So around our calc we swap every joint callback for ours and put them back after.
 *
 * REL: literals are fine here.
 */

#include "puppet_boat.h"

/* "Ship" archive (tww-decomp assets/GZLE01/res/Object/Ship.h) */
#define BOAT_ARC_NAME             "Ship"
#define BOAT_RES_BDL_FN_BODY      0x11  /* dRes_INDEX_SHIP_BDL_FN_BODY_e */
#define BOAT_RES_BDL_FN_HEAD_H    0x12  /* dRes_INDEX_SHIP_BDL_FN_HEAD_H_e */
#define BOAT_JNT_GATTAI           0x04  /* FN_BODY_JNT_J_FN_GATTAI_e: the head's base (d_a_ship.cpp:4743) */
#define BOAT_JNT_KAJI             0x05  /* FN_BODY_JNT_J_FN_KAJI_e: rudder */
#define BOAT_JNT_MAST             0x06  /* FN_BODY_JNT_J_FN_MAST_e */
#define BOAT_JNT_SAIL1            0x07  /* FN_BODY_JNT_J_FN_SAIL1_e: sail yard */
#define BOAT_JNT_STEER1           0x0A  /* FN_BODY_JNT_J_FN_STEER1_e: tiller */
#define BOAT_HEAD_JNT_KUBI1       0x02  /* FN_HEAD_H_JNT_J_FN_KUBI1_e .. KUBI6_e: the neck */
#define BOAT_HEAD_JNT_KUBI6       0x07
#define BOAT_JNT_SAIL2            0x08  /* FN_BODY_JNT_J_FN_SAIL2_e: folds the sail (d_a_ship.cpp:4068) */

/* daShip_c's morfs (createHeap, d_a_ship.cpp:4406-4467; setPartOnAnime/OffAnime :1328-1360;
 * setHeadAnm :3523-3538): J3DFrameCtrl::EMode_NONE, morf 3 for the mast, 5 for the head (0 for
 * DAMAGE1). Our play speed is 0: the frame comes from the peer. m03E8 is 1.0 or 0.001. */
#define BOAT_BCK_MODE_NONE        0
#define BOAT_MAST_MORF            3.0f
#define BOAT_HEAD_MORF            5.0f
#define BOAT_MAST_HIDE_SCALE      0.001f
#define BOAT_CALC_TIMING_IN       0     /* J3DNodeCBCalcTiming_In (J3DNode.h) */

/* Model flags as daShip_c::createHeap (d_a_ship.cpp:4406-4461) */
#define BOAT_MODEL_FLAG           0x80000
#define BOAT_BODY_DIFF_FLAG       0x11200202
#define BOAT_HEAD_DIFF_FLAG       0x11000002

#define BOAT_HEAP_SIZE            0x20000  /* daShip_c's own heap size; adjusted down once built */
#define BOAT_HEAP_ALIGN           0x20
#define BOAT_MAX_JOINTS           64       /* hook save buffers; FN_BODY has 11 joints, FN_HEAD_H 18 */

/* J3DSkinDeform (J3DSkinDeform.h, "Size: 0x18"); setSkinDeform flags as daShip_c (d_a_ship.cpp:4420):
 * bit 2 / bit 4 clear = CPU-skin positions / normals (J3DModel.cpp:459-480). */
#define BOAT_SKIN_DEFORM_SIZE     0x18
#define BOAT_SKIN_DEFORM_FLAGS    1

/* c_phase.h cPhs_State (ww_structs.h's values are wrong, see puppet.c) */
#define BOAT_PHS_COMPLEATE        4
#define BOAT_PHS_ERROR            5
/* request_of_phase_process_class id while dComIfG_resLoad runs: 1 loading, 2 loaded
 * (d_com_inf_game.cpp:551-575; dComIfG_resDelete asserts it isn't called while loading). */
#define BOAT_PHASE_LOADING        1
#define BOAT_PHASE_LOADED         2

/* dKy_tevstr_c (d_kankyo.h) */
#define TEVSTR_OFF_ROOM_NO        0xA9  /* s8 mRoomNo */
#define TEVSTR_OFF_ENVR_OVERRIDE  0xAA  /* u8 mEnvrIdxOverride */

/* Motion */
#define BOAT_MAX_PREDICT_FRAMES   10         /* stop extrapolating a stalled sample after this */
#define BOAT_SNAP_DIST_SQ         (1000.0f * 1000.0f)
#define BOAT_POS_EASE             0.3f
#define BOAT_MAX_SPEED            1000.0f    /* reject garbage speedF */
#define BOAT_MAX_COORD            1.0e6f

/* Seat: d_a_player_main_data.inc l_ship_offset, the point setShipRidePos pins Link to on the
 * hull matrix (d_a_player_ship.inc:165-239). */
static const cXyz l_boatSeatOffset = {0.0f, 15.0f, -35.0f};

/* daShip_c::setWaveAngle sample points (d_a_ship.cpp:833-836). The ship measures the height
 * difference over the points' horizontal distance (absXZ); the points are only yawed here,
 * so that distance is the constant span below. */
static const cXyz l_boatWaveFront = {0.0f, 0.0f, 180.0f};
static const cXyz l_boatWaveBack = {0.0f, 0.0f, -190.0f};
static const cXyz l_boatWaveRight = {-80.0f, 0.0f, 0.0f};
static const cXyz l_boatWaveLeft = {80.0f, 0.0f, 0.0f};
#define BOAT_WAVE_SPAN_FB         370.0f
#define BOAT_WAVE_SPAN_RL         160.0f

void puppet_boatInit(PuppetBoat *boat)
{
  u8 *p = (u8 *)boat;
  unsigned int i;
  for (i = 0; i < sizeof(PuppetBoat); i++)
    p[i] = 0;
}

static int boat_finite(f32 v, f32 limit)
{
  return v == v && v > -limit && v < limit;
}

/* daShip_c::getMaxWaterY without the Acch water check (d_a_ship.cpp:814-829): the sea's wave
 * height where there is sea, else the caller's fallback. */
static f32 boat_waterY(f32 x, f32 z, f32 fallbackY)
{
  if (d_a_sea__daSea_ChkArea(x, z))
    return d_a_sea__daSea_calcWave(x, z);
  return fallbackY;
}

/* The hull must be CPU-skinned like the local ship's. J3DModel::prepareShapePackets (in
 * viewCalc, i.e. mDoExt_modelEntryDL) writes the model's SkinPosCpu/SkinNrmCpu into the SHARED
 * J3DShape flags (J3DModel.cpp:917-928), and the shape reads them back at GX draw time. A hull
 * without skin deform would flip the local ship's shapes to the GPU path (garbage: its draw
 * matrices aren't computed) or draw ours at the origin, whichever entered last. */
static int boat_addSkinDeform(J3DModel *body)
{
  J3DSkinDeform *skin = (J3DSkinDeform *)operator_new(BOAT_SKIN_DEFORM_SIZE);
  if (skin == NULL)
    return 0;
  J3DSkinDeform__J3DSkinDeform(skin);
  // J3DErrType_Success == 0 (the prototype's `undefined` return is the low byte of the int)
  return J3DModel__setSkinDeform(body, skin, BOAT_SKIN_DEFORM_FLAGS) == 0;
}

/* A "Ship" archive resource (dComIfG_getObjectRes). */
static void *boat_getRes(u32 index)
{
  return dRes_control_c__getRes(BOAT_ARC_NAME, index, GAMEINFO_RES_OBJECT_INFO(&g_dComIfG_gameInfo),
                                GAMEINFO_RES_OBJECT_INFO_COUNT);
}

/* new mDoExt_McaMorf(data, NULL, NULL, bck, EMode_NONE, 0.0f, 0, -1, 0, NULL, flags) in the
 * current heap, as daShip_c::createHeap does for mpBodyAnm / mpHeadAnm. NULL on failure. */
static mDoExt_McaMorf *boat_createMorf(u32 bdl, u32 bck, u32 diffFlag)
{
  J3DModelData *data = (J3DModelData *)boat_getRes(bdl);
  void *anm = boat_getRes(bck);
  mDoExt_McaMorf *morf;

  if (data == NULL || anm == NULL)
    return NULL;
  morf = (mDoExt_McaMorf *)operator_new(MDOEXT_MCAMORF_SIZE);
  if (morf == NULL)
    return NULL;
  // 1: this is the complete object, so the ctor builds the virtual J3DMtxCalc base.
  mDoExt_McaMorf__mDoExt_McaMorf(morf, 1, data, NULL, NULL, (J3DAnmTransformKey *)anm, BOAT_BCK_MODE_NONE,
                                 0.0f, 0, -1, 0, NULL, BOAT_MODEL_FLAG, diffFlag);
  return MDOEXT_MCAMORF_MPMODEL(morf) != NULL ? morf : NULL;
}

/* ---- Sail --------------------------------------------------------------------------------
 * Vanilla (d_a_grid.cpp): the sail cloth is its own actor, daGrid_c, spawned by daShip_c::create
 * (d_a_ship.cpp:4693). It is not in fn_body.bdl: its daHo_packet_c draws an 85-vertex mesh (a 7x12
 * grid plus a top vertex, rest shape l_pos) straight through GX with the "Ship" new_ho1.bti
 * (C8 + RGB565 palette) and the "Cloth" clothtoon.bti ramp (daHo_packet_c::draw, :221-350),
 * Z-sorted into the translucent list (daGrid_c::_draw, :822-843). There is no physics: ho_move
 * (:359-607) recomputes every vertex each frame from sines of two running phases, the wind
 * (dKyw_get_wind_vec/pow), l_ship's mSailAngle and SAIL_ON flag, and field_0x2200. The ship
 * places it every frame (d_a_ship.cpp:4049-4074): position = J_FN_SAIL1's matrix, angles =
 * shape_angle, scale.y = SAIL1's length (the mast bck shrinks it; under 0.06 it is neither moved
 * nor drawn, d_a_grid.cpp:813/823) and field_0x2200 = 1 - SAIL2's X length (the fold).
 *
 * Spawning a daGrid_c isn't an option (it finds its l_ship by name: the local ship), but all its
 * code is in main.dol. So each boat keeps an imitation daGrid_c in its heap (zeroed like a new
 * actor, field_0x1b54 as _create fills it) and runs the game's ho_move on it with l_ship
 * pointed, for that call only, at a stub holding the two fields ho_move reads (the peer's
 * mSailAngle and SAIL_ON). Its packet (vtable in the REL) draws with the game's
 * daHo_packet_c::draw, so the cloth looks and moves like the vanilla one.
 *
 * Colour: the cloth's colour is the new_ho1 texture (the TEV multiplies it by the lighting,
 * d_a_grid.cpp:289-294; no material colour is involved), and the draw fetches the palette through
 * the archive's ResTIMG header at draw time (:244-252). So our packet's draw points that header at
 * a recoloured copy of the palette and restores it right after: the local sail, drawn by its own
 * packet, never sees it. The canvas (cream) takes the peer's tunic colour at the canvas's
 * shading; the teal emblem and orange corners stay (their anti-aliased edges blend). */

/* daGrid_c (d_a_grid.h; sizeof 0x221C = g_profile_GRID's size) */
#define GRID_SIZE                 0x221C
#define GRID_OFF_PACKET           0x2A0   /* daHo_packet_c mPacket */
#define GRID_OFF_AMP              0x1B54  /* f32 field_0x1b54[85]: per-vertex flutter amplitude */
#define GRID_OFF_FOLD             0x2200  /* f32 field_0x2200: 1 - SAIL2's X length */
#define GRID_VTX                  85      /* 7 x 12 grid + the top vertex */
#define GRID_COLS                 7
/* daHo_packet_c (d_a_grid.h) */
#define HO_OFF_SHAPE_PACKET_PTR   0x2C    /* J3DMatPacket::mpShapePacket (setShapePacket in the ctor) */
#define HO_OFF_SHAPE_PACKET       0x3C    /* mShapePacket */
#define HO_OFF_MTX                0x80    /* mMtx: view * model */
#define HO_OFF_TEVSTR             0xB0    /* mpTevStr */
#define HO_OFF_ALPHA              0x18A3  /* mAlpha */
#define HO_ALPHA_OPAQUE           0xFF    /* l_HIO.field_0x30: _execute's far / event value */
/* daShip_c fields ho_move reads through l_ship: getSailOn / getSailAngle (d_a_ship.h:101, :165) */
#define SHIP_OFF_STATE_FLAG       0x358
#define SHIP_OFF_SAIL_ANGLE       0x364
#define SHIP_SFLG_SAIL_ON         0x200   /* daSFLG_SAIL_ON_e */
/* procSteerMove sets SAIL_ON once the MAST_ON2 bck reaches frame 7 with the sail part up
 * (d_a_ship.cpp:1630-1632); setPartOffAnime clears it (:1360). */
#define SHIP_SAIL_ON_FRAME        7
#define SAIL_MIN_SCALE            0.06f
/* J3DDrawBuffer::mpZMtx (J3DDrawBuffer.h), read by entryZSort */
#define DRAWBUF_OFF_ZMTX          0x1C
#define TEVSTR_WORDS              (0xB0 / 4)

/* "Ship" new_ho1.bti (Ship.h dRes_INDEX_SHIP_BTI_NEW_HO1_e) and "Cloth" (Cloth.h) */
#define BOAT_RES_BTI_NEW_HO1      0x17
#define BOAT_CLOTH_ARC_NAME       "Cloth"
/* ResTIMG (JUTTexture.h) */
#define TIMG_FORMAT(t)            (*(u8 *)((u8 *)(t) + 0x00))
#define TIMG_PAL_FORMAT(t)        (*(u8 *)((u8 *)(t) + 0x09))
#define TIMG_NUM_COLORS(t)        (*(u16 *)((u8 *)(t) + 0x0A))
#define TIMG_PAL_OFFSET(t)        (*(u32 *)((u8 *)(t) + 0x0C))
#define TIMG_FMT_C8               0x09    /* GX_TF_C8 (vanilla new_ho1: 128x128, 96 colours) */
#define TIMG_TLUT_RGB565          0x01    /* GX_TL_RGB565 */
#define SAIL_TLUT_ENTRIES         0x100   /* the draw loads 0x100 entries (d_a_grid.cpp:245) */
#define SAIL_PAL_BYTES            (SAIL_TLUT_ENTRIES * 2)

/* Our sail block (boat heap, 32-aligned): the imitation daGrid_c, then the palette the draw shows
 * (NULL = vanilla), the new_ho1 header, and two palettes (double-buffered like
 * puppet_appearance.c's texture: the GPU may still be loading last frame's). */
#define SAIL_OFF_SHOW             GRID_SIZE             /* u8 *: the daGrid_c's tail padding */
#define SAIL_OFF_TIMG             0x2220                /* u32 *: new_ho1 header, NULL = no recolour */
#define SAIL_OFF_PAL              0x2240                /* 32-aligned */
#define SAIL_BLOCK_BYTES          (SAIL_OFF_PAL + 2 * SAIL_PAL_BYTES)
#define SAIL_SHOW(s)              (*(u8 **)((s) + SAIL_OFF_SHOW))
#define SAIL_TIMG(s)              (*(u32 **)((s) + SAIL_OFF_TIMG))
#define SAIL_PAL(s, i)            ((u16 *)((s) + SAIL_OFF_PAL + (i) * SAIL_PAL_BYTES))

/* Recolour: new_ho1's canvas is cream (main entry 231,215,189, luminance 216: that entry becomes
 * the picked colour), the emblem teal (33,109,107), the corner patches orange (206,101,33). An
 * entry's canvas weight = how far it is from teal along R-G (canvas >= 0, teal <= -70) times how
 * far from orange along R-B (canvas <= 64, orange >= 96). */
#define SAIL_CANVAS_LUM           216
#define SAIL_TEAL_RG              70
#define SAIL_ORANGE_RB            96
#define SAIL_ORANGE_RAMP          32

typedef void (*SailDrawFn)(u8 *packet);
typedef void (*SailMoveFn)(u8 *grid);

static void boat_sailDraw(u8 *packet);
static void boat_sailNoop(u8 *packet) { (void)packet; }

/* J3DPacket vtable {RTTI, this-offset, isSame, entry, draw, dtor}, as __vt__13daHo_packet_c
 * (main.dol 0x8038BBE0) with our draw. Only draw is called (J3DDrawBuffer::drawHead/drawTail);
 * the block goes with the boat heap, never through the dtor. */
static const void *const l_sailVtbl[6] = {NULL, NULL, (const void *)J3DMatPacket__isSame,
                                          (const void *)J3DMatPacket__entry, (const void *)boat_sailDraw,
                                          (const void *)boat_sailNoop};

/* daGrid_c::_create's field_0x1b54 (d_a_grid.cpp:674-759): each vertex's flutter amplitude, a sine
 * bump across the width (z) and up the height (y) of the rest shape. The loop only reads l_pos
 * (main.dol 0x8038B1D8, constant), so these are its results for GZLE01's l_pos, rounded to 4
 * decimals (7 per grid row, then the top vertex). */
static const f32 l_sailAmp[GRID_VTX] = {
  0.0000f, 20.8999f, 35.6403f, 39.8767f, 35.6403f, 20.8999f, 0.0000f,
  14.0262f, 44.9109f, 68.6507f, 76.9920f, 72.6832f, 54.2215f, 33.7220f,
  25.7013f, 64.1262f, 89.3331f, 99.0883f, 96.9092f, 82.1124f, 63.3211f,
  33.0682f, 75.5754f, 99.7258f, 109.4389f, 109.2104f, 98.9576f, 83.2043f,
  34.8921f, 77.9729f, 101.4169f, 111.5892f, 112.8186f, 105.7061f, 91.6494f,
  33.0682f, 73.9003f, 96.5564f, 107.5516f, 110.0354f, 105.6387f, 93.1919f,
  25.7013f, 61.1313f, 84.6565f, 98.6156f, 103.2966f, 100.7826f, 88.7794f,
  14.0262f, 41.8983f, 66.2014f, 83.1088f, 90.6425f, 89.9953f, 80.3832f,
  0.0000f, 27.4212f, 51.5202f, 69.3770f, 78.8283f, 79.9817f, 74.5491f,
  15.3616f, 26.0253f, 43.0499f, 58.8057f, 71.1327f, 78.9141f, 81.5267f,
  19.5293f, 23.9177f, 33.6173f, 44.5511f, 55.0684f, 64.3923f, 72.0512f,
  15.8186f, 17.1908f, 20.9891f, 26.0253f, 31.6009f, 37.3448f, 43.0499f,
  0.0000f,
};

/* The imitation daGrid_c + palettes, in the boat heap. NULL if it doesn't fit. */
static u8 *boat_createSail(JKRSolidHeap *heap)
{
  AppAllocFn alloc = (AppAllocFn)(u32)JKRHeap__alloc;
  u8 *sail = (u8 *)alloc(SAIL_BLOCK_BYTES, 32, heap);
  u8 *packet;
  u32 *timg;
  u32 i;

  if (sail == NULL)
    return NULL;
  for (i = 0; i < SAIL_BLOCK_BYTES; i += 4)
    *(u32 *)(sail + i) = 0; // like a new actor; the palettes' unused tails stay 0 too

  // daHo_packet_c's ctor (d_a_grid.h): alpha 0xFF, and the shape packet entryZSort clears.
  packet = sail + GRID_OFF_PACKET;
  *(const void *const **)packet = l_sailVtbl;
  *(u8 **)(packet + HO_OFF_SHAPE_PACKET_PTR) = packet + HO_OFF_SHAPE_PACKET;
  packet[HO_OFF_ALPHA] = HO_ALPHA_OPAQUE;
  for (i = 0; i < GRID_VTX; i++)
    ((f32 *)(sail + GRID_OFF_AMP))[i] = l_sailAmp[i];

  // Recolourable only while new_ho1 is what we expect.
  timg = (u32 *)boat_getRes(BOAT_RES_BTI_NEW_HO1);
  if (timg != NULL && TIMG_FORMAT(timg) == TIMG_FMT_C8 && TIMG_PAL_FORMAT(timg) == TIMG_TLUT_RGB565 &&
      TIMG_NUM_COLORS(timg) <= SAIL_TLUT_ENTRIES)
    SAIL_TIMG(sail) = timg;
  return sail;
}

/* 0..256 as v goes 0..span */
static int boat_ramp(int v, int span)
{
  if (v <= 0)
    return 0;
  if (v >= span)
    return 256;
  return (v * 256) / span;
}

/* new_ho1's palette with the canvas in rgb (see "Colour" above). */
static void boat_sailRecolor(u16 *dst, const u32 *timg, u32 rgb)
{
  const u16 *src = (const u16 *)((const u8 *)timg + TIMG_PAL_OFFSET(timg));
  u32 count = TIMG_NUM_COLORS(timg);
  u32 i;
  int k;

  for (i = 0; i < SAIL_TLUT_ENTRIES; i++)
  {
    u32 c = i < count ? src[i] : 0;
    int ch[3];
    int lum, w, t;

    ch[0] = (c >> 11) & 0x1F;
    ch[1] = (c >> 5) & 0x3F;
    ch[2] = c & 0x1F;
    ch[0] = (ch[0] << 3) | (ch[0] >> 2);
    ch[1] = (ch[1] << 2) | (ch[1] >> 4);
    ch[2] = (ch[2] << 3) | (ch[2] >> 2);
    w = (boat_ramp(ch[0] - ch[1] + SAIL_TEAL_RG, SAIL_TEAL_RG) *
         boat_ramp(SAIL_ORANGE_RB - (ch[0] - ch[2]), SAIL_ORANGE_RAMP)) / 256;
    lum = (77 * ch[0] + 150 * ch[1] + 29 * ch[2]) >> 8;
    for (k = 0; k < 3; k++)
    {
      t = (int)((rgb >> (16 - 8 * k)) & 0xFF) * lum / SAIL_CANVAS_LUM;
      if (t > 255)
        t = 255;
      ch[k] += ((t - ch[k]) * w) / 256;
    }
    dst[i] = (u16)(((ch[0] >> 3) << 11) | ((ch[1] >> 2) << 5) | (ch[2] >> 3));
  }
}

/* The slot's tunic colour on the sail: a recoloured palette, or NULL (vanilla) for the default. */
static void boat_sailColor(PuppetBoat *boat, u32 slotIndex)
{
  volatile u8 *slot = (volatile u8 *)PUPPET_SLOT_BASE(slotIndex);
  u32 rgb = ((u32)slot[PUPPET_SLOT_OFF_COLOR_R] << 16) | ((u32)slot[PUPPET_SLOT_OFF_COLOR_G] << 8) |
            (u32)slot[PUPPET_SLOT_OFF_COLOR_B];
  u8 *sail = boat->sail;
  AppStoreFn store = (AppStoreFn)(u32)os__DCStoreRange;
  u16 *pal;

  if (SAIL_TIMG(sail) == NULL || rgb == 0 || rgb == APP_DEFAULT_RGB)
  {
    SAIL_SHOW(sail) = NULL;
    return;
  }
  if (boat->sailKey != APP_KEY(rgb))
  {
    if (boat->sailKey != 0)
      boat->sailCur ^= 1; // leave the palette the GPU may still be loading
    pal = SAIL_PAL(sail, boat->sailCur);
    boat_sailRecolor(pal, SAIL_TIMG(sail), rgb);
    store(pal, SAIL_PAL_BYTES);
    boat->sailKey = APP_KEY(rgb);
  }
  SAIL_SHOW(sail) = (u8 *)SAIL_PAL(sail, boat->sailCur);
}

/* daShip_c's grid placement (d_a_ship.cpp:4049-4070) on our posed hull, then daGrid_c::_execute's
 * ho_move (d_a_grid.cpp:813-817) with l_ship standing for the peer's ship. */
static void boat_sailExecute(PuppetBoat *boat, u32 base, u32 flags, u32 slotIndex)
{
  u8 *sail = boat->sail;
  MTX34 *sail1;
  MTX34 *sail2;
  cXyz col;
  csXyz *angle;
  u32 ship[4]; /* daShip_c 0x358..0x367: mStateFlag .. mSailAngle */
  u32 savedShip;
  SailMoveFn move = (SailMoveFn)(u32)d_a_grid__ho_move;

  boat->sailShow = 0;
  if (sail == NULL || boat->clothState != PUPPET_SAIL_CLOTH_READY)
    return;

  sail1 = &J3DMODEL_MPNODEMTX(boat->body)[BOAT_JNT_SAIL1];
  col.x = sail1->m[0][2]; // |SAIL1 * (0, 0, -365)| / 365
  col.y = sail1->m[1][2];
  col.z = sail1->m[2][2];
  boat->sailScale = PSVECMag(&col);
  if (boat->sailScale < SAIL_MIN_SCALE)
    return; // mast down, or hidden for the cannon / crane

  sail2 = &J3DMODEL_MPNODEMTX(boat->body)[BOAT_JNT_SAIL2];
  col.x = sail2->m[0][0]; // 1 - |SAIL2 * (265, 0, 0)| / 265
  col.y = sail2->m[1][0];
  col.z = sail2->m[2][0];
  *(f32 *)(sail + GRID_OFF_FOLD) = 1.0f - PSVECMag(&col);

  FOPAC_CURRENT_POS(sail)->x = sail1->m[0][3];
  FOPAC_CURRENT_POS(sail)->y = sail1->m[1][3];
  FOPAC_CURRENT_POS(sail)->z = sail1->m[2][3];
  angle = FOPAC_CURRENT_ANGLE(sail);
  angle->x = boat->pitch;
  angle->y = boat->rotY;
  angle->z = boat->roll;

  ship[0] = ((flags & PUPPET_BOAT_FLAG_MAST_ON) && !(flags & PUPPET_BOAT_FLAG_MAST_HIDE) &&
             *(volatile u8 *)(base + PUPPET_BOAT_OFF_MAST_FRAME) >= SHIP_SAIL_ON_FRAME)
                ? SHIP_SFLG_SAIL_ON
                : 0;
  *(s16 *)((u8 *)ship + (SHIP_OFF_SAIL_ANGLE - SHIP_OFF_STATE_FLAG)) = boat->sailAngle;

  // ho_move reads only mStateFlag and mSailAngle through l_ship, and only during this call.
  savedShip = d_a_grid__l_ship;
  d_a_grid__l_ship = (u32)ship - SHIP_OFF_STATE_FLAG;
  move(sail);
  d_a_grid__l_ship = savedShip;

  boat_sailColor(boat, slotIndex);
  boat->sailShow = 1;
}

/* Our packet's draw: the game's, with the new_ho1 header pointing at the peer's palette. */
static void boat_sailDraw(u8 *packet)
{
  u8 *sail = packet - GRID_OFF_PACKET;
  u32 *timg = SAIL_TIMG(sail);
  u8 *pal = SAIL_SHOW(sail);
  SailDrawFn draw = (SailDrawFn)(u32)daHo_packet_c__draw;
  u32 saved;

  if (timg == NULL || pal == NULL)
  {
    draw(packet);
    return;
  }
  saved = TIMG_PAL_OFFSET(timg);
  TIMG_PAL_OFFSET(timg) = (u32)pal - (u32)timg; // wraps, like appearance's imageOffset
  draw(packet);
  TIMG_PAL_OFFSET(timg) = saved;
}

/* daGrid_c::_draw (d_a_grid.cpp:822-843): T(pos) * YXZrot(shape_angle) * Yrot(mSailAngle) *
 * scale(1, scale.y, 1), lit by a copy of the ship's tevStr (the draw writes its C0.a), Z-sorted
 * into the translucent list. */
static void boat_sailEntry(PuppetBoat *boat)
{
  u8 *sail = boat->sail;
  u8 *packet = sail + GRID_OFF_PACKET;
  J3DDrawBuffer *xlu = DDLST_LIST_MPXLULIST(GAMEINFO_DRAWLIST(&g_dComIfG_gameInfo));
  u32 *tev = (u32 *)FOPAC_TEV_STR(sail);
  MTX34 m;
  void **zMtx;
  void *savedZ;
  int i;

  if (xlu == NULL)
    return;
  mDoMtx_ZXYrotS(&m, boat->pitch, boat->rotY, boat->roll);
  mDoMtx_YrotM(&m, boat->sailAngle);
  for (i = 0; i < 3; i++)
    m.m[i][1] *= boat->sailScale;
  m.m[0][3] = FOPAC_CURRENT_POS(sail)->x;
  m.m[1][3] = FOPAC_CURRENT_POS(sail)->y;
  m.m[2][3] = FOPAC_CURRENT_POS(sail)->z;
  PSMTXConcat((MTX34 *)&J3DGraphBase__j3dSys, &m, (MTX34 *)(packet + HO_OFF_MTX)); // j3dSys.mViewMtx is at 0

  for (i = 0; i < TEVSTR_WORDS; i++)
    tev[i] = boat->tevStr[i];
  *(u32 **)(packet + HO_OFF_TEVSTR) = tev;

  zMtx = (void **)((u8 *)xlu + DRAWBUF_OFF_ZMTX);
  savedZ = *zMtx;
  *zMtx = &m; // entryZSort reads it right away
  J3DDrawBuffer__entryZSort(xlu, (J3DMatPacket *)packet);
  *zMtx = savedZ;
}

/* Build the hull and head in a solid heap of our own (the puppet's actor heap belongs to
 * playerInit). As daShip_c::createHeap, minus the cannon, crane and rope. */
static int boat_createModels(PuppetBoat *boat)
{
  JKRSolidHeap *heap;

  heap = mDoExt_createSolidHeapFromGameToCurrent(BOAT_HEAP_SIZE, BOAT_HEAP_ALIGN);
  if (heap == NULL)
    return 0;

  // All allocations (morfs, models, skin deform, its transformed vertex arrays) come from `heap`.
  boat->bodyAnm = boat_createMorf(BOAT_RES_BDL_FN_BODY, SHIP_BCK_MAST_OFF2, BOAT_BODY_DIFF_FLAG);
  boat->headAnm = NULL;
  if (boat->bodyAnm != NULL && !boat_addSkinDeform(MDOEXT_MCAMORF_MPMODEL(boat->bodyAnm)))
    boat->bodyAnm = NULL;
  if (boat->bodyAnm != NULL)
    boat->headAnm = boat_createMorf(BOAT_RES_BDL_FN_HEAD_H, SHIP_BCK_FN_LOOK_L, BOAT_HEAD_DIFF_FLAG);
  // The sail is optional: a boat without one still draws.
  boat->sail = boat->headAnm != NULL ? boat_createSail(heap) : NULL;
  mDoExt_restoreCurrentHeap();

  if (boat->bodyAnm == NULL || boat->headAnm == NULL)
  {
    mDoExt_destroySolidHeap(heap);
    boat->bodyAnm = NULL;
    boat->headAnm = NULL;
    boat->sail = NULL;
    return 0;
  }

  // As daShip_c::createHeap (d_a_ship.cpp:4467): the ctor leaves mPrevMorf at -1, which makes
  // the next setMorf snap; spend that here so the head's first bck change blends too.
  mDoExt_McaMorf__setMorf(boat->headAnm, BOAT_HEAD_MORF);
  boat->body = MDOEXT_MCAMORF_MPMODEL(boat->bodyAnm);
  boat->head = MDOEXT_MCAMORF_MPMODEL(boat->headAnm);
  boat->mastBck = SHIP_BCK_MAST_OFF2;
  boat->headBck = SHIP_BCK_FN_LOOK_L;

  mDoExt_adjustSolidHeap(heap);
  boat->heap = heap;
  return 1;
}

/* Load the archives (async, polled every frame) and build the models once. "Cloth" (the sail's
 * toon ramp, which daHo_packet_c::draw fetches at draw time) only gates the sail. */
static int boat_ready(fopAc_ac_c *actor, PuppetBoat *boat)
{
  int phs;

  if (boat->clothState == PUPPET_SAIL_CLOTH_LOADING)
  {
    phs = dComIfG_resLoad(&boat->clothPhase, BOAT_CLOTH_ARC_NAME);
    if (phs == BOAT_PHS_COMPLEATE)
      boat->clothState = PUPPET_SAIL_CLOTH_READY;
    else if (phs == BOAT_PHS_ERROR)
    {
      OSReport("[PUPPET] boat: Cloth archive load failed (no sail)\n");
      boat->clothState = PUPPET_SAIL_CLOTH_FAILED;
    }
  }

  if (boat->state == PUPPET_BOAT_STATE_READY)
    return 1;
  if (boat->state == PUPPET_BOAT_STATE_FAILED)
    return 0;

  phs = dComIfG_resLoad(&boat->phase, BOAT_ARC_NAME);
  if (phs == BOAT_PHS_ERROR)
  {
    OSReport("[PUPPET] boat: Ship archive load failed\n");
    boat->state = PUPPET_BOAT_STATE_FAILED;
    return 0;
  }
  if (phs != BOAT_PHS_COMPLEATE)
    return 0;

  if (!boat_createModels(boat))
  {
    OSReport("[PUPPET] boat: model build failed\n");
    boat->state = PUPPET_BOAT_STATE_FAILED;
    return 0;
  }

  // dKy_tevstr_init takes an s8 room (d_kankyo.cpp:3383, compared to -1); ww_functions.h
  // declares it `char`, which GCC-PPC passes zero-extended, so call it with the real type.
  ((void (*)(dKy_tevstr_c *, s8, u8))dKy_tevstr_init)((dKy_tevstr_c *)boat->tevStr,
                                                      FOPAC_CURRENT_ROOMNO(actor), 0xFF);
  boat->state = PUPPET_BOAT_STATE_READY;
  OSReport("[PUPPET] boat: ready\n");
  return 1;
}

/* Point a morf at a "Ship" bck, as daShip_c's setAnm calls but with play speed 0 (we set the
 * frame from the peer). Returns 0 if the bck isn't there. */
static int boat_setBck(mDoExt_McaMorf *morf, u32 bck, f32 morfFrames)
{
  void *anm = boat_getRes(bck);
  if (anm == NULL)
    return 0;
  mDoExt_McaMorf__setAnm(morf, (J3DAnmTransform *)anm, BOAT_BCK_MODE_NONE, morfFrames, 0.0f, 0.0f, -1.0f, NULL);
  return 1;
}

/* The peer's whole-frame position in the bck, capped at its end (J3DFrameCtrl mEnd); the play()
 * after it pulls the end frame back to end - 0.001, where the peer's finished bck sits too. */
static void boat_setFrame(mDoExt_McaMorf *morf, u8 frame)
{
  s16 end = MDOEXT_MCAMORF_FRAME_END(morf);
  MDOEXT_MCAMORF_FRAME(morf) = (f32)(frame < end ? frame : end);
}

/* The boat being calculated and which of its models, for boat_jointCallBack (actors run
 * single-threaded; NULL outside boat_calcModel). */
static PuppetBoat *l_boatCalc = NULL;
static u8 l_boatCalcHead = 0;

/* daShip_c's joint callbacks, with the peer's values and our drawn yaw:
 *   hull (bodyJointCallBack, d_a_ship.cpp:110-131): anm = anm * Zrot(m0366) on J_FN_STEER1 /
 *     J_FN_KAJI, anm * Zrot(-mSailAngle) on J_FN_SAIL1, anm * scale(m03E8) on J_FN_MAST (the
 *     cannon / crane placement it also does there is skipped: we draw neither);
 *   head (headJointCallBack1, :214-231) on J_FN_KUBI1..6: anm = Yrot(y) * ZXYrot(m03A0, m03A2, 0)
 *     * Yrot(-y) * anm, y = shape_angle.y + m03A2 * (jno - 2), keeping anm's translation.
 * Either way the result goes back to J3DSys::mCurrentMtx for the joint's children. */
static int boat_jointCallBack(J3DNode *node, int timing)
{
  PuppetBoat *boat = l_boatCalc;
  int jno = J3DJOINT_MJNTNO(node);
  MTX34 *anm;
  MTX34 m;
  f32 tx, ty, tz;
  s16 yaw;

  if (timing != BOAT_CALC_TIMING_IN || boat == NULL)
    return 1;

  if (l_boatCalcHead)
  {
    if (jno < BOAT_HEAD_JNT_KUBI1 || jno > BOAT_HEAD_JNT_KUBI6)
      return 1;
    anm = &J3DMODEL_MPNODEMTX(boat->head)[jno];
    tx = anm->m[0][3];
    ty = anm->m[1][3];
    tz = anm->m[2][3];
    yaw = (s16)(boat->rotY + boat->headY * (jno - BOAT_HEAD_JNT_KUBI1));
    mDoMtx_YrotS(&m, yaw);
    mDoMtx_ZXYrotM((undefined4)&m, boat->headX, boat->headY, 0);
    mDoMtx_YrotM(&m, (s16)-yaw);
    PSMTXConcat(&m, anm, &m);
    m.m[0][3] = tx;
    m.m[1][3] = ty;
    m.m[2][3] = tz;
    PSMTXCopy(&m, anm);
  }
  else
  {
    anm = &J3DMODEL_MPNODEMTX(boat->body)[jno];
    if (jno == BOAT_JNT_STEER1 || jno == BOAT_JNT_KAJI)
      mDoMtx_ZrotS(&m, boat->tiller);
    else if (jno == BOAT_JNT_SAIL1)
      mDoMtx_ZrotS(&m, (s16)-boat->sailAngle);
    else if (jno == BOAT_JNT_MAST && boat->mastHide)
      PSMTXScale(BOAT_MAST_HIDE_SCALE, BOAT_MAST_HIDE_SCALE, BOAT_MAST_HIDE_SCALE, &m);
    else
      return 1; // no callback on this joint in daShip_c (or m03E8 == 1: identity)
    PSMTXConcat(anm, &m, anm);
  }
  PSMTXCopy(anm, &J3DSys__mCurrentMtx);
  return 1;
}

/* mDoExt_McaMorf::calc with the local ship's hooks on the (shared) joints swapped out: every
 * joint callback becomes ours and every joint's mMtxCalc is cleared. mDoExt_McaMorf::calc sets
 * joint 0's mMtxCalc to the calling morf and never clears it (m_Do_ext.cpp:1540-1549), so the
 * local ship's morf is left there; J3DJoint::calcIn would run it for our model too
 * (J3DJoint.cpp:212-214), animating our boat with the local ship's anim or calling a freed
 * object once the local ship is gone. Our morf's calc puts itself on joint 0 instead. */
static void boat_calcModel(PuppetBoat *boat, mDoExt_McaMorf *morf, u8 isHead)
{
  J3DModelData *data = J3DMODEL_MPMODELDATA(MDOEXT_MCAMORF_MPMODEL(morf));
  J3DJoint **joints;
  void *savedCallBack[BOAT_MAX_JOINTS];
  void *savedMtxCalc[BOAT_MAX_JOINTS];
  int count;
  int i;

  if (data == NULL)
    return;
  joints = J3DMODELDATA_MJOINTTREE_MPJOINTS(data);
  count = J3DMODELDATA_MJOINTTREE_JOINTNUM(data);
  if (joints == NULL || count > BOAT_MAX_JOINTS)
    return; // can't protect the local ship's hooks: leave the model unposed

  for (i = 0; i < count; i++)
  {
    savedCallBack[i] = J3DNODE_MCALLBACK(joints[i]);
    savedMtxCalc[i] = J3DJOINT_MMTXCALC(joints[i]);
    J3DNODE_MCALLBACK(joints[i]) = (void *)boat_jointCallBack;
    J3DJOINT_MMTXCALC(joints[i]) = NULL;
  }

  l_boatCalc = boat;
  l_boatCalcHead = isHead;
  mDoExt_McaMorf__calc(morf);
  l_boatCalc = NULL;

  for (i = 0; i < count; i++)
  {
    J3DNODE_MCALLBACK(joints[i]) = savedCallBack[i];
    J3DJOINT_MMTXCALC(joints[i]) = savedMtxCalc[i];
  }
}

/* daShip_c::setWaveAngle (d_a_ship.cpp:832-873): pitch from the front/back wave heights,
 * roll from right/left. */
static void boat_waveAngles(PuppetBoat *boat, s16 *pitch, s16 *roll)
{
  MTX34 m;
  cXyz front, back, right, left;

  mDoMtx_YrotS(&m, boat->rotY);
  m.m[0][3] = boat->pos.x;
  m.m[1][3] = boat->pos.y;
  m.m[2][3] = boat->pos.z;
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveFront, &front);
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveBack, &back);
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveRight, &right);
  PSMTXMultVec(&m, (cXyz *)&l_boatWaveLeft, &left);

  front.y = boat_waterY(front.x, front.z, boat->pos.y);
  back.y = boat_waterY(back.x, back.z, boat->pos.y);
  right.y = boat_waterY(right.x, right.z, boat->pos.y);
  left.y = boat_waterY(left.x, left.z, boat->pos.y);

  *pitch = (s16)cM_atan2s(-(front.y - back.y), BOAT_WAVE_SPAN_FB);
  *roll = (s16)cM_atan2s(-(right.y - left.y), BOAT_WAVE_SPAN_RL);
}

/* The peer's mast / head bcks, frames and joint angles for this frame's calc. A bck change
 * blends like daShip_c's setAnm calls (mast 3 frames, head 5, DAMAGE1 none); a boat that has
 * just appeared (no pose yet) takes it without blending. Speed 0: the frame is the peer's. */
static void boat_animate(PuppetBoat *boat, u32 base, u32 flags)
{
  u32 mastBck = (flags & PUPPET_BOAT_FLAG_MAST_ON) ? SHIP_BCK_MAST_ON2 : SHIP_BCK_MAST_OFF2;
  u32 headBck = (flags >> PUPPET_BOAT_HEAD_BCK_SHIFT) & PUPPET_BOAT_HEAD_BCK_MASK;

  if (mastBck != boat->mastBck &&
      boat_setBck(boat->bodyAnm, mastBck, boat->hasPose ? BOAT_MAST_MORF : 0.0f))
    boat->mastBck = (u8)mastBck;

  // Head bcks only (never the mast's); 0 or anything else keeps the current one.
  if (headBck >= SHIP_BCK_FIRST && headBck <= SHIP_BCK_LAST && headBck != SHIP_BCK_MAST_OFF2 &&
      headBck != SHIP_BCK_MAST_ON2 && headBck != boat->headBck &&
      boat_setBck(boat->headAnm, headBck,
                  (boat->hasPose && headBck != SHIP_BCK_DAMAGE1) ? BOAT_HEAD_MORF : 0.0f))
    boat->headBck = (u8)headBck;

  boat_setFrame(boat->bodyAnm, *(volatile u8 *)(base + PUPPET_BOAT_OFF_MAST_FRAME));
  boat_setFrame(boat->headAnm, *(volatile u8 *)(base + PUPPET_BOAT_OFF_HEAD_FRAME));
  // play() only steps the blend here (J3DFrameCtrl rate 0), as execute does (d_a_ship.cpp:3605-3606).
  mDoExt_McaMorf__play(boat->bodyAnm, NULL, 0, 0);
  mDoExt_McaMorf__play(boat->headAnm, NULL, 0, 0);

  boat->mastHide = (flags & PUPPET_BOAT_FLAG_MAST_HIDE) != 0;
  boat->sailAngle = *(volatile s16 *)(base + PUPPET_BOAT_OFF_SAIL_ANGLE);
  boat->tiller = *(volatile s16 *)(base + PUPPET_BOAT_OFF_TILLER);
  boat->headX = *(volatile s16 *)(base + PUPPET_BOAT_OFF_HEAD_X);
  boat->headY = *(volatile s16 *)(base + PUPPET_BOAT_OFF_HEAD_Y);
}

void puppet_boatExecute(fopAc_ac_c *actor, PuppetBoat *boat, u32 slotIndex)
{
  u32 base;
  u32 flags;
  cXyz net;
  f32 speedF;
  s16 netRotY;
  MTX34 m;
  cXyz ahead, offset;
  f32 targetX, targetZ, targetY, dx, dz;
  int flying, onSea;
  s16 pitchTarget = 0;
  s16 rollTarget = 0;
  MTX34 *bodyMtx;

  boat->visible = 0;

  if (slotIndex >= PUPPET_MAX_SLOTS || *(volatile u32 *)PUPPET_SYNC_BASE != PUPPET_SYNC_MAGIC ||
      *(volatile u32 *)(PUPPET_SLOT_BASE(slotIndex) + PUPPET_SLOT_OFF_ACTIVE) == 0)
  {
    boat->hasPose = 0;
    return;
  }

  base = PUPPET_BOAT_BASE(slotIndex);
  flags = *(volatile u32 *)(base + PUPPET_BOAT_OFF_FLAGS);
  if ((flags & PUPPET_BOAT_FLAG_ACTIVE) == 0)
  {
    boat->hasPose = 0;
    return;
  }

  net.x = *(volatile f32 *)(base + PUPPET_BOAT_OFF_POSX);
  net.y = *(volatile f32 *)(base + PUPPET_BOAT_OFF_POSY);
  net.z = *(volatile f32 *)(base + PUPPET_BOAT_OFF_POSZ);
  speedF = *(volatile f32 *)(base + PUPPET_BOAT_OFF_SPEED_F);
  netRotY = *(volatile s16 *)(base + PUPPET_BOAT_OFF_ROTY);
  if (!boat_finite(net.x, BOAT_MAX_COORD) || !boat_finite(net.y, BOAT_MAX_COORD) ||
      !boat_finite(net.z, BOAT_MAX_COORD) || !boat_finite(speedF, BOAT_MAX_SPEED))
    return;

  if (!boat_ready(actor, boat))
    return;

  // A changed position is a new sample; otherwise the sample is getting older.
  if (net.x != boat->net.x || net.y != boat->net.y || net.z != boat->net.z)
  {
    boat->net = net;
    boat->framesSinceNet = 0;
  }
  else if (boat->framesSinceNet < BOAT_MAX_PREDICT_FRAMES)
  {
    boat->framesSinceNet++;
  }

  // Dead reckoning: carry the sample forward along the peer's heading at their speed.
  mDoMtx_YrotS(&m, netRotY);
  ahead.x = 0.0f;
  ahead.y = 0.0f;
  ahead.z = speedF * (f32)boat->framesSinceNet;
  PSMTXMultVec(&m, &ahead, &offset);
  targetX = net.x + offset.x;
  targetZ = net.z + offset.z;

  if (!boat->hasPose)
  {
    boat->pos.x = targetX;
    boat->pos.z = targetZ;
    boat->pos.y = net.y;
    boat->rotY = netRotY;
    boat->pitch = 0;
    boat->roll = 0;
  }
  else
  {
    dx = targetX - boat->pos.x;
    dz = targetZ - boat->pos.z;
    if (dx * dx + dz * dz > BOAT_SNAP_DIST_SQ)
    {
      boat->pos.x = targetX;
      boat->pos.z = targetZ;
    }
    else
    {
      boat->pos.x += dx * BOAT_POS_EASE;
      boat->pos.z += dz * BOAT_POS_EASE;
    }
    cLib_addCalcAngleS(&boat->rotY, netRotY, 2, 0x2000, 0x20);
  }

  // Height and tilt from the local sea; the peer's Y only in the air or off the sea.
  flying = (flags & PUPPET_BOAT_FLAG_FLY) != 0;
  onSea = !flying && d_a_sea__daSea_ChkArea(boat->pos.x, boat->pos.z);
  targetY = onSea ? d_a_sea__daSea_calcWave(boat->pos.x, boat->pos.z) : net.y;
  if (!boat->hasPose)
    boat->pos.y = targetY;
  else
    boat->pos.y += (targetY - boat->pos.y) * BOAT_POS_EASE;

  if (onSea)
    boat_waveAngles(boat, &pitchTarget, &rollTarget);
  cLib_addCalcAngleS(&boat->pitch, pitchTarget, 4, 0x400, 0x10);
  cLib_addCalcAngleS(&boat->roll, rollTarget, 4, 0x400, 0x10);

  // Hull at T(pos) * ZXYrot (as daShip_c::create, d_a_ship.cpp:4733-4736); head on GATTAI.
  bodyMtx = (MTX34 *)J3DMODEL_MBASETRMTX(boat->body);
  mDoMtx_ZXYrotS(bodyMtx, boat->pitch, boat->rotY, boat->roll);
  bodyMtx->m[0][3] = boat->pos.x;
  bodyMtx->m[1][3] = boat->pos.y;
  bodyMtx->m[2][3] = boat->pos.z;
  boat_animate(boat, base, flags);
  boat_calcModel(boat, boat->bodyAnm, 0);
  PSMTXCopy(&J3DMODEL_MPNODEMTX(boat->body)[BOAT_JNT_GATTAI], (MTX34 *)J3DMODEL_MBASETRMTX(boat->head));
  boat_calcModel(boat, boat->headAnm, 1);
  boat_sailExecute(boat, base, flags, slotIndex);

  boat->hasPose = 1;
  boat->visible = 1;
}

int puppet_boatSeat(PuppetBoat *boat, cXyz *pos, csXyz *angle)
{
  if (!boat->visible)
    return 0;
  PSMTXMultVec((MTX34 *)J3DMODEL_MBASETRMTX(boat->body), (cXyz *)&l_boatSeatOffset, pos);
  angle->x = boat->pitch;
  angle->y = boat->rotY;
  angle->z = boat->roll;
  return 1;
}

void puppet_boatDraw(fopAc_ac_c *actor, PuppetBoat *boat)
{
  u8 *tev = (u8 *)boat->tevStr;
  u8 *actorTev = (u8 *)FOPAC_TEV_STR(actor);

  if (!boat->visible)
    return;

  // Light it for the room below. On open water daShip_c has no ground and uses the stay room
  // (setRoomInfo, d_a_ship.cpp:3081-3092); the puppet's own setRoomInfo keeps its tevStr on
  // the room under it, which is the same place.
  tev[TEVSTR_OFF_ROOM_NO] = actorTev[TEVSTR_OFF_ROOM_NO];
  tev[TEVSTR_OFF_ENVR_OVERRIDE] = actorTev[TEVSTR_OFF_ENVR_OVERRIDE];

  // daShip_c::draw (d_a_ship.cpp:253-299), hull and head only.
  dScnKy_env_light_c__settingTevStruct(&g_env_light, settingTevStruct__LightType__Actor,
                                       &boat->pos, (dKy_tevstr_c *)tev);
  dScnKy_env_light_c__setLightTevColorType(&g_env_light, boat->body, (dKy_tevstr_c *)tev);
  dScnKy_env_light_c__setLightTevColorType(&g_env_light, boat->head, (dKy_tevstr_c *)tev);

  dComIfGd_setListP1();
  mDoExt_modelEntryDL(boat->body);
  mDoExt_modelEntryDL(boat->head);
  dComIfGd_setList();

  if (boat->sailShow)
    boat_sailEntry(boat);
}

/* dComIfG_resDelete for a phase we may have started; resDelete asserts mid-load, so a reference
 * taken while loading is never dropped and that archive stays loaded for the session (harmless at
 * sea, where d_s_play preloads "Ship"; "Cloth" is tiny). */
static void boat_releaseRes(request_of_phase_process_class *phase, char *name)
{
  if (phase->mStep == BOAT_PHASE_LOADED)
    dComIfG_resDelete(phase, name);
  else if (phase->mStep == BOAT_PHASE_LOADING)
    OSReport("[PUPPET] boat: deleted while %s was loading; its reference stays\n", name);
}

void puppet_boatDelete(PuppetBoat *boat)
{
  if (boat->heap != NULL)
  {
    // The morfs live in it too; like daShip_c's (freed with its actor heap) they get no dtor.
    mDoExt_destroySolidHeap(boat->heap);
    boat->heap = NULL;
    boat->bodyAnm = NULL;
    boat->headAnm = NULL;
    boat->body = NULL;
    boat->head = NULL;
    boat->sail = NULL; // was in the heap
  }

  boat_releaseRes(&boat->phase, BOAT_ARC_NAME);
  boat_releaseRes(&boat->clothPhase, BOAT_CLOTH_ARC_NAME);

  boat->state = PUPPET_BOAT_STATE_NONE;
  boat->clothState = PUPPET_SAIL_CLOTH_LOADING;
  boat->visible = 0;
  boat->sailShow = 0;
}
