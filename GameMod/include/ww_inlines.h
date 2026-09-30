/**
 * ww_inlines.h - Wind Waker Inline Functions and Offset Macros
 *
 * This file provides offset-based access to ALL struct fields used in puppet code.
 * DO NOT rely on ww_structs.h for field access - GCC padding differs from
 * the original Metrowerks compiler, causing offset shifts.
 *
 * All offsets are verified against tww-decomp reference headers.
 */

#ifndef WW_INLINES_H
#define WW_INLINES_H

#include "ww_defines.h"

/* Forward declarations for globals used in inline functions */
extern dComIfG_inf_c g_dComIfG_gameInfo;
extern J3DSys J3DGraphBase__j3dSys;

/* Forward declaration for fopAcM_SearchByID used by dComIfGp_att_getZHint */
u32 fopAcM_SearchByID(fpc_ProcID actorPID, fopAc_ac_c **pDstActor);

/* ============================================================================ */
/* fopAc_ac_c FIELD ACCESS (base actor class) - Size: 0x290                     */
/* From tww-decomp/include/f_op/f_op_actor.h                                    */
/* ============================================================================ */

// leafdraw_class base at 0x000 (size 0x0C0)
#define FOPAC_ACTOR_TYPE(ac)         (*(int*)((u8*)(ac) + 0x0C0))
#define FOPAC_ACTOR_TAG(ac)          ((create_tag_class*)((u8*)(ac) + 0x0C4))
#define FOPAC_DRAW_TAG(ac)           ((create_tag_class*)((u8*)(ac) + 0x0D8))
#define FOPAC_SUB_METHOD(ac)         (*(void**)((u8*)(ac) + 0x0EC))
#define FOPAC_HEAP(ac)               (*(void**)((u8*)(ac) + 0x0F0))
// JKRHeap.h: /* 0x30 */ u8* mStart; /* 0x34 */ u8* mEnd (JKRHeap::find DOL 0x802B09CC)
#define JKRHEAP_MSTART(heap)         (*(u32*)((u8*)(heap) + 0x30))
#define JKRHEAP_MEND(heap)           (*(u32*)((u8*)(heap) + 0x34))
#define FOPAC_EVENT_INFO(ac)         ((dEvt_info_c*)((u8*)(ac) + 0x0F4))
#define FOPAC_EVENT_COMMAND(ac)      (*(u16*)((u8*)(ac) + 0x0F4 + 0x04))
#define FOPAC_EVENT_CONDITION(ac)    (*(u16*)((u8*)(ac) + 0x0F4 + 0x06))
#define FOPAC_TEV_STR(ac)            ((dKy_tevstr_c*)((u8*)(ac) + 0x10C))
#define FOPAC_SET_ID(ac)             (*(u16*)((u8*)(ac) + 0x1BC))
#define FOPAC_GROUP(ac)              (*(u8*)((u8*)(ac) + 0x1BE))
#define FOPAC_CULL_TYPE(ac)          (*(u8*)((u8*)(ac) + 0x1BF))
#define FOPAC_DEMO_ACTOR_ID(ac)      (*(u8*)((u8*)(ac) + 0x1C0))
#define FOPAC_ARGUMENT(ac)           (*(s8*)((u8*)(ac) + 0x1C1))
#define FOPAC_GBA_NAME(ac)           (*(u8*)((u8*)(ac) + 0x1C2))
#define FOPAC_ACTOR_STATUS(ac)       (*(u32*)((u8*)(ac) + 0x1C4))
#define FOPAC_ACTOR_CONDITION(ac)    (*(u32*)((u8*)(ac) + 0x1C8))
#define FOPAC_PARENT_ACTOR_ID(ac)    (*(fpc_ProcID*)((u8*)(ac) + 0x1CC))
#define FOPAC_HOME(ac)               ((actor_place*)((u8*)(ac) + 0x1D0))
#define FOPAC_OLD(ac)                ((actor_place*)((u8*)(ac) + 0x1E4))
#define FOPAC_CURRENT(ac)            ((actor_place*)((u8*)(ac) + 0x1F8))
#define FOPAC_CURRENT_POS(ac)        ((cXyz*)((u8*)(ac) + 0x1F8))
#define FOPAC_CURRENT_ANGLE(ac)      ((csXyz*)((u8*)(ac) + 0x1F8 + 0x0C))
#define FOPAC_CURRENT_ROOMNO(ac)     (*(s8*)((u8*)(ac) + 0x1F8 + 0x12))
#define FOPAC_SHAPE_ANGLE(ac)        ((csXyz*)((u8*)(ac) + 0x20C))
#define FOPAC_SCALE(ac)              ((cXyz*)((u8*)(ac) + 0x214))
#define FOPAC_SPEED(ac)              ((cXyz*)((u8*)(ac) + 0x220))
#define FOPAC_CULL_MTX(ac)           (*(MtxP*)((u8*)(ac) + 0x22C))
#define FOPAC_CULL_BOX(ac)           ((void*)((u8*)(ac) + 0x230))
#define FOPAC_CULL_SIZE_FAR(ac)      (*(f32*)((u8*)(ac) + 0x248))
#define FOPAC_MODEL(ac)              (*(J3DModel**)((u8*)(ac) + 0x24C))
#define FOPAC_JNT_HIT(ac)            (*(void**)((u8*)(ac) + 0x250))
#define FOPAC_SPEED_F(ac)            (*(f32*)((u8*)(ac) + 0x254))
#define FOPAC_GRAVITY(ac)            (*(f32*)((u8*)(ac) + 0x258))
#define FOPAC_MAX_FALL_SPEED(ac)     (*(f32*)((u8*)(ac) + 0x25C))
#define FOPAC_EYE_POS(ac)            ((cXyz*)((u8*)(ac) + 0x260))
#define FOPAC_ATTENTION_INFO(ac)     ((actor_attention_types*)((u8*)(ac) + 0x26C))
#define FOPAC_ATTN_DISTANCES(ac)     ((u8*)((u8*)(ac) + 0x26C))
#define FOPAC_ATTN_POSITION(ac)      ((cXyz*)((u8*)(ac) + 0x26C + 0x08))
#define FOPAC_ATTN_FLAGS(ac)         (*(u32*)((u8*)(ac) + 0x26C + 0x14))
#define FOPAC_MAX_HEALTH(ac)         (*(s8*)((u8*)(ac) + 0x284))
#define FOPAC_HEALTH(ac)             (*(s8*)((u8*)(ac) + 0x285))
#define FOPAC_ITEM_TABLE_IDX(ac)     (*(s32*)((u8*)(ac) + 0x288))
#define FOPAC_STEAL_ITEM_BITNO(ac)   (*(u8*)((u8*)(ac) + 0x28C))
#define FOPAC_STEAL_ITEM_LEFT(ac)    (*(s8*)((u8*)(ac) + 0x28D))

/* ============================================================================ */
/* daPy_py_c FIELD ACCESS (player base class) - Size: 0x320                     */
/* From tww-decomp/include/d/actor/d_a_player.h                                 */
/* Inherits from fopAc_ac_c (0x290)                                             */
/* ============================================================================ */

#define DAPY_PY_CUT_TYPE(py)         (*(u8*)((u8*)(py) + 0x290))
#define DAPY_PY_CUT_COUNT(py)        (*(u8*)((u8*)(py) + 0x291))
#define DAPY_PY_DAMAGE_WAIT_TIMER(py) (*(s16*)((u8*)(py) + 0x294))
#define DAPY_PY_QUAKE_TIMER(py)      (*(s16*)((u8*)(py) + 0x296))
#define DAPY_PY_MFACE(py)            (*(int*)((u8*)(py) + 0x298))
#define DAPY_PY_NO_RESET_FLG0(py)    (*(u32*)((u8*)(py) + 0x29C))
#define DAPY_PY_NO_RESET_FLG1(py)    (*(u32*)((u8*)(py) + 0x2A0))
#define DAPY_PY_RESET_FLG0(py)       (*(u32*)((u8*)(py) + 0x2A4))
#define DAPY_PY_MAX_NORMAL_SPEED(py) (*(f32*)((u8*)(py) + 0x2A8))
#define DAPY_PY_HEIGHT(py)           (*(f32*)((u8*)(py) + 0x2AC))
#define DAPY_PY_FIELD_0x2B0(py)      (*(f32*)((u8*)(py) + 0x2B0))
#define DAPY_PY_BODY_ANGLE(py)       ((csXyz*)((u8*)(py) + 0x2B4))
#define DAPY_PY_BODY_ANGLE_X(py)     (*(s16*)((u8*)(py) + 0x2B4))
#define DAPY_PY_BODY_ANGLE_Y(py)     (*(s16*)((u8*)(py) + 0x2B6))
#define DAPY_PY_BODY_ANGLE_Z(py)     (*(s16*)((u8*)(py) + 0x2B8))
#define DAPY_PY_HEAD_TOP_POS(py)     ((cXyz*)((u8*)(py) + 0x2BC))
#define DAPY_PY_SWORD_TOP_POS(py)    ((cXyz*)((u8*)(py) + 0x2C8))
#define DAPY_PY_LEFT_HAND_POS(py)    ((cXyz*)((u8*)(py) + 0x2D4))
#define DAPY_PY_RIGHT_HAND_POS(py)   ((cXyz*)((u8*)(py) + 0x2E0))
#define DAPY_PY_ROPE_POS(py)         ((cXyz*)((u8*)(py) + 0x2EC))
#define DAPY_PY_FIELD_0x2F8(py)      ((cXyz*)((u8*)(py) + 0x2F8))
#define DAPY_PY_DEMO(py)             ((daPy_demo_c*)((u8*)(py) + 0x304))
#define DAPY_PY_DEMO_TYPE(py)        (*(u16*)((u8*)(py) + 0x304))
#define DAPY_PY_DEMO_MODE(py)        (*(u32*)((u8*)(py) + 0x304 + 0x10))

// Legacy compatibility - mapping old names to new offset macros
#define DAPY_PY_FIELD_0x298(py)      DAPY_PY_MFACE(py)
#define DAPY_PY_FIELD_0x2D4(py)      (*DAPY_PY_LEFT_HAND_POS(py))
#define DAPY_PY_FIELD_0x2E0(py)      (*DAPY_PY_RIGHT_HAND_POS(py))
#define DAPY_PY_MNORESETFLG0(py)     DAPY_PY_NO_RESET_FLG0(py)
#define DAPY_PY_MNORESETFLG1(py)     DAPY_PY_NO_RESET_FLG1(py)
#define DAPY_PY_MLEFTHANDPOS(py)     (*DAPY_PY_LEFT_HAND_POS(py))
#define DAPY_PY_MRIGHTHANDPOS(py)    (*DAPY_PY_RIGHT_HAND_POS(py))

/* ============================================================================ */
/* daPy_lk_c FIELD ACCESS (Link's main class) - Size: 0x4C28                    */
/* From tww-decomp/include/d/actor/d_a_player_main.h                            */
/* Inherits from daPy_py_c (0x320)                                              */
/* ============================================================================ */

// Phase and model data
#define DAPY_LK_MPHASE(link)         ((void*)((u8*)(link) + 0x0320))
#define DAPY_LK_MPCLMODELDATA(link)  (*(J3DModelData**)((u8*)(link) + 0x0328))
#define DAPY_LK_MPCLMODEL(link)      (*(J3DModel**)((u8*)(link) + 0x032C))
#define DAPY_LK_MPKATSURAMODEL(link) (*(J3DModel**)((u8*)(link) + 0x0330))
#define DAPY_LK_MPYAMUMODEL(link)    (*(J3DModel**)((u8*)(link) + 0x0334))
#define DAPY_LK_MPCURRLINKTEX(link)  (*(void**)((u8*)(link) + 0x0338))
#define DAPY_LK_MOTHERLINKTEX(link)  ((void*)((u8*)(link) + 0x033C))

// Texture animation data
#define DAPY_LK_MPANMTEXPATTERNDATA(link) (*(J3DAnmTexPattern**)((u8*)(link) + 0x035C))
#define DAPY_LK_M_TEXNOANMS(link)    (*(J3DTexNoAnm**)((u8*)(link) + 0x0360))
#define DAPY_LK_MPTEXSCROLLRESDATA(link) (*(J3DAnmTextureSRTKey**)((u8*)(link) + 0x0364))
#define DAPY_LK_M_TEXMTXANM(link)    (*(J3DTexMtxAnm**)((u8*)(link) + 0x0368))
#define DAPY_LK_M_TEX_EYE_SCROLL(link) ((void**)((u8*)(link) + 0x036C))

// Shape arrays for rendering
#define DAPY_LK_MPZOFFBLENDSHAPE(link) ((J3DShape**)((u8*)(link) + 0x0374))
#define DAPY_LK_MPZOFFNONESHAPE(link)  ((J3DShape**)((u8*)(link) + 0x0384))
#define DAPY_LK_MPZONSHAPE(link)       ((J3DShape**)((u8*)(link) + 0x0394))
#define DAPY_LK_MPLHANDSHAPE(link)     (*(J3DShape**)((u8*)(link) + 0x03A4))
#define DAPY_LK_MPRHANDSHAPE(link)     (*(J3DShape**)((u8*)(link) + 0x03A8))

// Collision structures - CRITICAL: Use these offset macros!
#define DAPY_LK_MACCHCIR(link)       ((dBgS_AcchCir*)((u8*)(link) + 0x03AC))  // [3] - Size: 0xC0
#define DAPY_LK_MACCH(link)          ((dBgS_Acch*)((u8*)(link) + 0x046C))     // dBgS_LinkAcch - Size: 0x1C4
#define DAPY_LK_MLINKLINCHK(link)    ((void*)((u8*)(link) + 0x0630))
#define DAPY_LK_MROPELINCHK(link)    ((void*)((u8*)(link) + 0x069C))
#define DAPY_LK_MBOOMERANGLINCHK(link) ((void*)((u8*)(link) + 0x0708))
#define DAPY_LK_MGNDCHK(link)        ((void*)((u8*)(link) + 0x0774))
#define DAPY_LK_MROOFCHK(link)       ((void*)((u8*)(link) + 0x07C8))
#define DAPY_LK_MARROWLINCHK(link)   ((void*)((u8*)(link) + 0x0814))
#define DAPY_LK_MMIRLIGHTLINCHK(link) ((void*)((u8*)(link) + 0x0880))
#define DAPY_LK_MLAVAGNDCHK(link)    ((void*)((u8*)(link) + 0x08EC))
#define DAPY_LK_MPOLYINFO(link)      ((cBgS_PolyInfo*)((u8*)(link) + 0x0940))
#define DAPY_LK_M_HIO(link)          (*(void**)((u8*)(link) + 0x0950))

// Equipment models
#define DAPY_LK_MPHANDSMODEL(link)      (*(J3DModel**)((u8*)(link) + 0x0954))
#define DAPY_LK_MPEQUIPPEDSWORDMODEL(link) (*(J3DModel**)((u8*)(link) + 0x0958))
#define DAPY_LK_MPSWGRIPAMODEL(link)    (*(J3DModel**)((u8*)(link) + 0x095C))
#define DAPY_LK_MPSWGRIPMSMODEL(link)   (*(J3DModel**)((u8*)(link) + 0x0960))
#define DAPY_LK_MSWGRIPMSABBCKANIM(link) ((void*)((u8*)(link) + 0x0964))
#define DAPY_LK_MPTSWGRIPMSABBRK(link)  (*(void**)((u8*)(link) + 0x0974))
#define DAPY_LK_MPTSWGRIPMSBTK(link)    (*(void**)((u8*)(link) + 0x0978))
#define DAPY_LK_MPPODMSMODEL(link)      (*(J3DModel**)((u8*)(link) + 0x097C))
#define DAPY_LK_MPODMSMODEL(link)       DAPY_LK_MPPODMSMODEL(link) // Alias
#define DAPY_LK_MPEQUIPPEDSHIELDMODEL(link) (*(J3DModel**)((u8*)(link) + 0x0980))
#define DAPY_LK_MPSHAMODEL(link)        (*(J3DModel**)((u8*)(link) + 0x0984))
#define DAPY_LK_MPSHMSMODEL(link)       (*(J3DModel**)((u8*)(link) + 0x0988))
#define DAPY_LK_MATNGSSHABCK(link)      ((void*)((u8*)(link) + 0x098C))
#define DAPY_LK_MPTSHMSSBTK(link)       (*(void**)((u8*)(link) + 0x099C))
#define DAPY_LK_MMIRRORPACKET(link)     ((void*)((u8*)(link) + 0x09A0))

// Additional models and animations (large gap due to mMirrorPacket size)
#define DAPY_LK_MPYMSLS00MODEL(link)    (*(J3DModel**)((u8*)(link) + 0x2E7C))
#define DAPY_LK_MPYMSLS00BTK(link)      (*(void**)((u8*)(link) + 0x2E80))
#define DAPY_LK_MPHBOOTSMODELS(link)    ((J3DModel**)((u8*)(link) + 0x2E84))
#define DAPY_LK_MPPRINGMODEL(link)      (*(J3DModel**)((u8*)(link) + 0x2E8C))
#define DAPY_LK_MPITEMHEAPS(link)       ((void**)((u8*)(link) + 0x2E90))
#define DAPY_LK_MPITEMANIMEHEAP(link)   (*(void**)((u8*)(link) + 0x2ECC)) // mpItemAnimeHeap (playerDelete DOL 0x80122F3C)
// daPy_anmHeap_c (0x10 bytes): /* 0xC */ JKRSolidHeap* mpAnimeHeap. m_anm_heap_under[2] 0x2FDC,
// m_anm_heap_upper[3] 0x2FFC, m_tex_anm_heap 0x31B8, m_tex_scroll_heap 0x31C8 (playerDelete DOL 0x80122EDC-0x80122F24)
#define DAPY_LK_ANMHEAP_UNDER(link, i)  (*(void**)((u8*)(link) + 0x2FE8 + 0x10 * (i)))
#define DAPY_LK_ANMHEAP_UPPER(link, i)  (*(void**)((u8*)(link) + 0x3008 + 0x10 * (i)))
#define DAPY_LK_TEXANMHEAP(link)        (*(void**)((u8*)(link) + 0x31C4))
#define DAPY_LK_TEXSCROLLHEAP(link)     (*(void**)((u8*)(link) + 0x31D4))
#define DAPY_LK_MPEQUIPITEMMODEL(link)  (*(J3DModel**)((u8*)(link) + 0x2E98))
#define DAPY_LK_MPHELDITEMMODEL(link)   (*(J3DModel**)((u8*)(link) + 0x2E98)) // Alias for mpEquipItemModel
#define DAPY_LK_MSWORDANIM(link)        ((void*)((u8*)(link) + 0x2E9C))
#define DAPY_LK_MPPARACHUTEFANMORF(link) (*(void**)((u8*)(link) + 0x2EAC))
#define DAPY_LK_MPBOMBBRK(link)         (*(void**)((u8*)(link) + 0x2EB0))

// From tww-decomp/include/d/actor/d_a_player_main.h - additional models
#define DAPY_LK_MPBOTTLECONTENTSMODEL(link) (*(J3DModel**)((u8*)(link) + 0x2EE0))
#define DAPY_LK_MPBOTTLECAPMODEL(link)      (*(J3DModel**)((u8*)(link) + 0x2EE4))
#define DAPY_LK_MPSWORDMODEL1(link)         (*(J3DModel**)((u8*)(link) + 0x2EE8))
#define DAPY_LK_MPSWORDTIPSTABMODEL(link)   (*(J3DModel**)((u8*)(link) + 0x2EEC))
#define DAPY_LK_MPSUIMENMUNYA_MODEL(link)   (*(J3DModel**)((u8*)(link) + 0x2F14))
#define DAPY_LK_MPSUIMENMUNYA_BTK(link)     (*(void**)((u8*)(link) + 0x2F18))
#define DAPY_LK_MPYUCHW00MODEL(link)        (*(J3DModel**)((u8*)(link) + 0x2F1C))
#define DAPY_LK_MPYUCHW00BTK(link)          (*(void**)((u8*)(link) + 0x2F30))
#define DAPY_LK_MPYUCHW00BRK(link)          (*(void**)((u8*)(link) + 0x2F34))
#define DAPY_LK_MPYBAFO00MODEL(link)        (*(J3DModel**)((u8*)(link) + 0x2F38))
#define DAPY_LK_MPYBAFO00BTK(link)          (*(void**)((u8*)(link) + 0x2F3C))
#define DAPY_LK_MMAGICARARMORAURAENTRIES(link) ((void*)((u8*)(link) + 0x2F40)) // [6] daPy_aura_c
#define DAPY_LK_MYAURA00RBRK(link)          ((void*)((u8*)(link) + 0x2F70)) // mDoExt_brkAnm
// mDoExt_brkAnm layout (m_Do_ext.h): +0 vtable, +4 J3DFrameCtrl* mFrameCtrl, +8 J3DAnmTevRegKey* mpAnm
#define MDOEXT_BRKANM_MPANM(brk)            (*(void**)((u8*)(brk) + 0x08))
#define DAPY_LK_MYAURA00RBRK_ANM(link)      MDOEXT_BRKANM_MPANM(DAPY_LK_MYAURA00RBRK(link))   // link+0x2F78 (DOL 0x801080B4)
#define DAPY_LK_MPYAURA00BTK(link)          (*(void**)((u8*)(link) + 0x2F88))
#define DAPY_LK_MPYMGCS00MODEL(link)        (*(J3DModel**)((u8*)(link) + 0x2F8C))
#define DAPY_LK_MYMGCS00BRK(link)           ((void*)((u8*)(link) + 0x2F90)) // mDoExt_brkAnm
#define DAPY_LK_MYMGCS00BRK_ANM(link)       MDOEXT_BRKANM_MPANM(DAPY_LK_MYMGCS00BRK(link))    // link+0x2F98 (DOL 0x8010810C)
#define DAPY_LK_MPYMGCS00BTK(link)          (*(void**)((u8*)(link) + 0x2FA8))

// Matrix calculators - CRITICAL for animation!
#define DAPY_LK_M_PBCALC(link)       ((void**)((u8*)(link) + 0x2FAC))   // [2] mDoExt_MtxCalcAnmBlendTblOld*
#define DAPY_LK_MANMRATIOUNDER(link) ((void*)((u8*)(link) + 0x2FB4))
#define DAPY_LK_MANMRATIOUPPER(link) ((void*)((u8*)(link) + 0x2FC4))
#define DAPY_LK_M_ANM_HEAP_UNDER(link) ((void*)((u8*)(link) + 0x2FDC))
#define DAPY_LK_M_ANM_HEAP_UPPER(link) ((void*)((u8*)(link) + 0x2FFC))
#define DAPY_LK_MFRAMECTRLUNDER(link) ((J3DFrameCtrl*)((u8*)(link) + 0x302C))
#define DAPY_LK_MFRAMECTRLUPPER(link) ((J3DFrameCtrl*)((u8*)(link) + 0x3054))
#define DAPY_LK_MSIGHTPACKET(link)   ((void*)((u8*)(link) + 0x3090))
#define DAPY_LK_MSIGHTPACKET_DRAWFLAG(link) (*(u8*)((u8*)(link) + 0x3094)) // mSightPacket (0x3090) is a dDlst_base_c: vtable at +0, mDrawFlg at +4 (DOL 0x80107338)
#define DAPY_LK_MJAIZELIANIME(link)  ((JAIZelAnime*)((u8*)(link) + 0x30E0))
#define DAPY_LK_M_SANM_BUFFER(link)  (*(void**)((u8*)(link) + 0x3178))

// Actor keep structures
#define DAPY_LK_MACTORKEEPEQUIP(link) ((daPy_actorKeep_c*)((u8*)(link) + 0x317C))
#define DAPY_LK_MACTORKEEPEQUIP_MPACTOR(link) (*(fopAc_ac_c**)((u8*)(link) + 0x317C + 0x04))
#define DAPY_LK_MACTORKEEPTHROW(link) ((daPy_actorKeep_c*)((u8*)(link) + 0x3184))
#define DAPY_LK_MACTORKEEPGRAB(link)  ((daPy_actorKeep_c*)((u8*)(link) + 0x318C))
#define DAPY_LK_MACTORKEEPROPE(link)  ((daPy_actorKeep_c*)((u8*)(link) + 0x3194))

// Attention actors
#define DAPY_LK_MPATTNACTORLOCKON(link) (*(fopAc_ac_c**)((u8*)(link) + 0x319C))
#define DAPY_LK_MPATTNACTORACTION(link) (*(fopAc_ac_c**)((u8*)(link) + 0x31A0))
#define DAPY_LK_MPATTNACTORA(link)      (*(fopAc_ac_c**)((u8*)(link) + 0x31A4))
#define DAPY_LK_MPATTNACTORX(link)      (*(fopAc_ac_c**)((u8*)(link) + 0x31A8))
#define DAPY_LK_MPATTNACTORY(link)      (*(fopAc_ac_c**)((u8*)(link) + 0x31AC))
#define DAPY_LK_MPATTNACTORZ(link)      (*(fopAc_ac_c**)((u8*)(link) + 0x31B0))

// Old frame data - CRITICAL for animation morph
#define DAPY_LK_M_OLD_FDATA(link)    (*(void**)((u8*)(link) + 0x31B4))
#define DAPY_LK_M_TEX_ANM_HEAP(link) ((void*)((u8*)(link) + 0x31B8))
#define DAPY_LK_M_TEX_SCROLL_HEAP(link) ((void*)((u8*)(link) + 0x31C8))

// Current procedure
#define DAPY_LK_MCURPROC(link)       (*(int*)((u8*)(link) + 0x31D8))

// Get current process function pointer (offset 0x31DC)
typedef void (*daPy_ProcFunc)(daPy_lk_c *);
static inline daPy_ProcFunc dapy_lk_getCurProcFunc(daPy_lk_c *link)
{
  return *(daPy_ProcFunc *)((u8 *)link + 0x31DC);
}

// Foot effects
#define DAPY_LK_MFOOTEFFECT(link)    ((void*)((u8*)(link) + 0x31E8))

// Various fields
#define DAPY_LK_MDIRECTION(link)         (*(u8*)((u8*)(link) + 0x34B8))
#define DAPY_LK_MFRONTWALLTYPE(link)     (*(u8*)((u8*)(link) + 0x34B9))
#define DAPY_LK_M34BA(link)              (*(u8*)((u8*)(link) + 0x34BA))
#define DAPY_LK_MCURRITEMHEAPIDX(link)   (*(u8*)((u8*)(link) + 0x34BB))
#define DAPY_LK_M34BC(link)              (*(u8*)((u8*)(link) + 0x34BC))
#define DAPY_LK_MREADYITEMBTN(link)      (*(u8*)((u8*)(link) + 0x34BD))
#define DAPY_LK_M34BE(link)              (*(u8*)((u8*)(link) + 0x34BE))
#define DAPY_LK_MREVERB(link)            (*(s8*)((u8*)(link) + 0x34BF))
#define DAPY_LK_MLEFTHANDIDX(link)       (*(u8*)((u8*)(link) + 0x34C0))
#define DAPY_LK_MRIGHTHANDIDX(link)      (*(u8*)((u8*)(link) + 0x34C1))
#define DAPY_LK_M34C2(link)              (*(u8*)((u8*)(link) + 0x34C2))
#define DAPY_LK_M34C3(link)              (*(u8*)((u8*)(link) + 0x34C3))
#define DAPY_LK_M34C4(link)              (*(u8*)((u8*)(link) + 0x34C4))
#define DAPY_LK_M34C5(link)              (*(u8*)((u8*)(link) + 0x34C5))
#define DAPY_LK_M34C6(link)              (*(u8*)((u8*)(link) + 0x34C6))
#define DAPY_LK_MACTIVEPLAYERBOMBS(link) (*(u8*)((u8*)(link) + 0x34C7))
#define DAPY_LK_MITEMTRIGGER(link)       (*(u8*)((u8*)(link) + 0x34C8))
#define DAPY_LK_MITEMBUTTON(link)        (*(u8*)((u8*)(link) + 0x34C9))
#define DAPY_LK_M34CA(link)              (*(u8*)((u8*)(link) + 0x34CA))
#define DAPY_LK_MDEKUSPRESTARTPOINT(link) (*(u8*)((u8*)(link) + 0x34CB))

// s16 fields
#define DAPY_LK_M34D0(link)          (*(s16*)((u8*)(link) + 0x34D0))
#define DAPY_LK_M34D2(link)          (*(s16*)((u8*)(link) + 0x34D2))
#define DAPY_LK_M34D4(link)          (*(s16*)((u8*)(link) + 0x34D4))
#define DAPY_LK_M34D6(link)          (*(s16*)((u8*)(link) + 0x34D6))
#define DAPY_LK_M34D8(link)          (*(s16*)((u8*)(link) + 0x34D8))
#define DAPY_LK_M34DA(link)          (*(s16*)((u8*)(link) + 0x34DA))
#define DAPY_LK_M34DC(link)          (*(s16*)((u8*)(link) + 0x34DC))
#define DAPY_LK_M34DE(link)          (*(s16*)((u8*)(link) + 0x34DE))
#define DAPY_LK_M34E0(link)          (*(s16*)((u8*)(link) + 0x34E0))
#define DAPY_LK_M34E2(link)          (*(s16*)((u8*)(link) + 0x34E2))  // Ground angle
#define DAPY_LK_M34E4(link)          (*(s16*)((u8*)(link) + 0x34E4))
#define DAPY_LK_M34E6(link)          (*(s16*)((u8*)(link) + 0x34E6))
#define DAPY_LK_M34E8(link)          (*(s16*)((u8*)(link) + 0x34E8))
#define DAPY_LK_M34EA(link)          (*(s16*)((u8*)(link) + 0x34EA))  // Relative facing dir save
#define DAPY_LK_M34EC(link)          (*(s16*)((u8*)(link) + 0x34EC))
#define DAPY_LK_MSEANMIDX(link)      (*(u16*)((u8*)(link) + 0x34EE))
#define DAPY_LK_M34F0(link)          (*(u16*)((u8*)(link) + 0x34F0))

// Texture animation frames
#define DAPY_LK_M3530(link)          (*(u16*)((u8*)(link) + 0x3530))
#define DAPY_LK_M3532(link)          (*(u16*)((u8*)(link) + 0x3532))

// More s16 fields
#define DAPY_LK_M3540(link)          (*(s16*)((u8*)(link) + 0x3540))
#define DAPY_LK_M3542(link)          (*(s16*)((u8*)(link) + 0x3542))
#define DAPY_LK_M3544(link)          (*(s16*)((u8*)(link) + 0x3544))
#define DAPY_LK_MSHIELDFRONTRANGEYANGL(link) (*(s16*)((u8*)(link) + 0x3546))
#define DAPY_LK_MTINKLEHOVERTI(link) (*(s16*)((u8*)(link) + 0x354C))
#define DAPY_LK_MTINKLESHIELDTI(link) (*(s16*)((u8*)(link) + 0x354E))
#define DAPY_LK_M3550(link)          (*(s16*)((u8*)(link) + 0x3550))
#define DAPY_LK_MKEEPITEM(link)      (*(u16*)((u8*)(link) + 0x3552))
#define DAPY_LK_M3554(link)          (*(s16*)((u8*)(link) + 0x3554))
#define DAPY_LK_M3558(link)          (*(s16*)((u8*)(link) + 0x3558))
#define DAPY_LK_M355A(link)          (*(s16*)((u8*)(link) + 0x355A))
#define DAPY_LK_M355C(link)          (*(s16*)((u8*)(link) + 0x355C))
#define DAPY_LK_M355E(link)          (*(s16*)((u8*)(link) + 0x355E))
#define DAPY_LK_MEQUIPITEM(link)     (*(u16*)((u8*)(link) + 0x3560))
#define DAPY_LK_M3562(link)          (*(u16*)((u8*)(link) + 0x3562))
#define DAPY_LK_M3564(link)          (*(s16*)((u8*)(link) + 0x3564))
#define DAPY_LK_M3566(link)          (*(s16*)((u8*)(link) + 0x3566))
#define DAPY_LK_M3568(link)          (*(s16*)((u8*)(link) + 0x3568))
#define DAPY_LK_MCAMERAINFOIDX(link) (*(int*)((u8*)(link) + 0x356C))

// Integer and float fields
#define DAPY_LK_M3570(link)          (*(s32*)((u8*)(link) + 0x3570))
#define DAPY_LK_M3574(link)          (*(s32*)((u8*)(link) + 0x3574))  // Ground flag
#define DAPY_LK_M3578(link)          (*(int*)((u8*)(link) + 0x3578))
#define DAPY_LK_M357C(link)          (*(int*)((u8*)(link) + 0x357C))  // Previous ground code
#define DAPY_LK_M3580(link)          (*(int*)((u8*)(link) + 0x3580))  // Ground code
#define DAPY_LK_MCURRATTRIBUTECODE(link) (*(int*)((u8*)(link) + 0x3584))
#define DAPY_LK_M3588(link)          (*(int*)((u8*)(link) + 0x3588))
#define DAPY_LK_MSTAFFIDX(link)      (*(int*)((u8*)(link) + 0x358C))
#define DAPY_LK_MEVENTIDX(link)      (*(int*)((u8*)(link) + 0x3590))
#define DAPY_LK_MRESTARTPOINT(link)  (*(int*)((u8*)(link) + 0x3594))

// Float fields
#define DAPY_LK_M3598(link)          (*(f32*)((u8*)(link) + 0x3598))
#define DAPY_LK_M359C(link)          (*(f32*)((u8*)(link) + 0x359C))
#define DAPY_LK_M35A0(link)          (*(f32*)((u8*)(link) + 0x35A0))
#define DAPY_LK_M35A4(link)          (*(f32*)((u8*)(link) + 0x35A4))
#define DAPY_LK_M35A8(link)          (*(f32*)((u8*)(link) + 0x35A8))
#define DAPY_LK_M35AC(link)          (*(f32*)((u8*)(link) + 0x35AC))
#define DAPY_LK_MSTICKDISTANCE(link) (*(f32*)((u8*)(link) + 0x35B0))
#define DAPY_LK_M35B4(link)          (*(f32*)((u8*)(link) + 0x35B4))  // Stick distance save
#define DAPY_LK_M35B8(link)          (*(f32*)((u8*)(link) + 0x35B8))
#define DAPY_LK_MVELOCITY(link)      (*(f32*)((u8*)(link) + 0x35BC))
#define DAPY_LK_M35C4(link)          (*(f32*)((u8*)(link) + 0x35C4))
#define DAPY_LK_M35C8(link)          (*(f32*)((u8*)(link) + 0x35C8))
#define DAPY_LK_M35CC(link)          (*(f32*)((u8*)(link) + 0x35CC))
#define DAPY_LK_M35D0(link)          (*(f32*)((u8*)(link) + 0x35D0))  // Swim fall Y position
#define DAPY_LK_M35D4(link)          (*(f32*)((u8*)(link) + 0x35D4))
#define DAPY_LK_M35D8(link)          (*(f32*)((u8*)(link) + 0x35D8))
#define DAPY_LK_M35DC(link)          (*(f32*)((u8*)(link) + 0x35DC))
#define DAPY_LK_M35E0(link)          (*(f32*)((u8*)(link) + 0x35E0))
#define DAPY_LK_MSEANMRATE(link)     (*(f32*)((u8*)(link) + 0x360C))
#define DAPY_LK_M3610(link)          (*(f32*)((u8*)(link) + 0x3610))
#define DAPY_LK_M3614(link)          (*(int*)((u8*)(link) + 0x3614))
#define DAPY_LK_MMODEFLG(link)       (*(u32*)((u8*)(link) + 0x3618))
#define DAPY_LK_MMTRLSNDID(link)     (*(u32*)((u8*)(link) + 0x361C))

// Sword blur at 0x37E4 - daPy_swBlur_c (size ~0x308)
#define DAPY_LK_MSWBLUR(link)        ((void*)((u8*)(link) + 0x37E4))
#define DAPY_LK_MSWBLUR_FIELD2(link) (*(s16*)((u8*)(link) + 0x37E4 + 0x14)) // field2_0x14 from decompile
#define DAPY_LK_MSWBLUR_FIELD9(link) (*(f32*)((u8*)(link) + 0x37E4 + 0x304)) // field9_0x304 from decompile
#define DAPY_LK_M3620(link)          (*(u32*)((u8*)(link) + 0x3620))
#define DAPY_LK_M3624(link)          (*(u32*)((u8*)(link) + 0x3624))
#define DAPY_LK_M3628(link)          (*(fpc_ProcID*)((u8*)(link) + 0x3628))
#define DAPY_LK_MTACTZEVPARTNERID(link) (*(fpc_ProcID*)((u8*)(link) + 0x362C))
#define DAPY_LK_M3630(link)          (*(fpc_ProcID*)((u8*)(link) + 0x3630))
#define DAPY_LK_MWHIRLID(link)       (*(fpc_ProcID*)((u8*)(link) + 0x3634))
#define DAPY_LK_MMSGID(link)         (*(fpc_ProcID*)((u8*)(link) + 0x3638))
#define DAPY_LK_MPSEANMFRAMECTRL(link) (*(J3DFrameCtrl**)((u8*)(link) + 0x363C))

// Position vectors
#define DAPY_LK_M3688(link)          ((cXyz*)((u8*)(link) + 0x3688))
#define DAPY_LK_MOLDSPEED(link)      ((cXyz*)((u8*)(link) + 0x3694))
#define DAPY_LK_M36A0(link)          ((cXyz*)((u8*)(link) + 0x36A0))
#define DAPY_LK_M36AC(link)          ((cXyz*)((u8*)(link) + 0x36AC))
#define DAPY_LK_M36B8(link)          ((cXyz*)((u8*)(link) + 0x36B8))
#define DAPY_LK_M36C4(link)          ((cXyz*)((u8*)(link) + 0x36C4))  // Sword base pos
#define DAPY_LK_M36D0(link)          ((cXyz*)((u8*)(link) + 0x36D0))  // Old sword top pos
#define DAPY_LK_M36DC(link)          ((cXyz*)((u8*)(link) + 0x36DC))  // Old sword base pos
#define DAPY_LK_MHOOKSHOTROOTPOS(link) ((cXyz*)((u8*)(link) + 0x36E8))
#define DAPY_LK_M36F4(link)          ((cXyz*)((u8*)(link) + 0x36F4))  // Boomerang catch pos
#define DAPY_LK_M3700(link)          ((cXyz*)((u8*)(link) + 0x3700))
#define DAPY_LK_M370C(link)          ((cXyz*)((u8*)(link) + 0x370C))
#define DAPY_LK_M3718(link)          ((cXyz*)((u8*)(link) + 0x3718))
#define DAPY_LK_M3724(link)          ((cXyz*)((u8*)(link) + 0x3724))
#define DAPY_LK_M3730(link)          ((cXyz*)((u8*)(link) + 0x3730))
#define DAPY_LK_M373C(link)          ((cXyz*)((u8*)(link) + 0x373C))
#define DAPY_LK_M3748(link)          ((cXyz*)((u8*)(link) + 0x3748))  // Position save
#define DAPY_LK_MLEDGELOCATION(link) ((cXyz*)((u8*)(link) + 0x3748))  // Alias for ledge tracking

// Matrix at 0x37B4
#define DAPY_LK_M37B4(link)          ((Mtx*)((u8*)(link) + 0x37B4))

// Collision status - mStts at daPy_lk_c + 0x3FE8
// cCcD_Stts layout: m_cc_move(0x00), mp_actor(0x0C), m_apid(0x10), m_weight(0x14)
#define DAPY_LK_MSTTS(link)              ((dCcD_Stts*)((u8*)(link) + 0x3FE8))
#define DAPY_LK_MSTTS_CCMOVE(link)       ((cXyz*)((u8*)(link) + 0x3FE8 + 0x00))
#define DAPY_LK_MSTTS_WEIGHT(link)       (*(u8*)((u8*)(link) + 0x3FE8 + 0x14))

// Legacy compatibility macros - map old field names to new offset macros
#define DAPY_LK_FIELD_0x3748(link)   (*DAPY_LK_M3748(link))
#define DAPY_LK_FIELD_0x35B4(link)   DAPY_LK_M35B4(link)
#define DAPY_LK_FIELD_0x34EA(link)   DAPY_LK_M34EA(link)
#define DAPY_LK_FIELD_0x34C2(link)   DAPY_LK_M34C2(link)
#define DAPY_LK_FIELD_0x34C3(link)   DAPY_LK_M34C3(link)
#define DAPY_LK_FIELD_0x3574(link)   DAPY_LK_M3574(link)
#define DAPY_LK_FIELD_0x35D0(link)   DAPY_LK_M35D0(link)
#define DAPY_LK_FIELD_0x34E2(link)   DAPY_LK_M34E2(link)
#define DAPY_LK_FIELD_0x3580(link)   DAPY_LK_M3580(link)
#define DAPY_LK_FIELD_0x357C(link)   DAPY_LK_M357C(link)
#define DAPY_LK_FIELD_0x36F4(link)   (*DAPY_LK_M36F4(link))
#define DAPY_LK_FIELD_0x36D0(link)   (*DAPY_LK_M36D0(link))
#define DAPY_LK_FIELD_0x36DC(link)   (*DAPY_LK_M36DC(link))
#define DAPY_LK_FIELD_0x36C4(link)   (*DAPY_LK_M36C4(link))
#define DAPY_LK_FIELD_0x3634(link)   DAPY_LK_MWHIRLID(link)

/* ============================================================================ */
/* dBgS_Acch FIELD ACCESS (actor collision checker) - Size: 0x1C4              */
/* From tww-decomp/include/d/d_bg_s_acch.h                                     */
/* Inherits from cBgS_Chk (0x14) + dBgS_Chk (0x14) = base 0x28                 */
/* ============================================================================ */

#define ACCH_M_FLAGS(acch)           (*(u32*)((u8*)(acch) + 0x028))
#define ACCH_PM_POS(acch)            (*(cXyz**)((u8*)(acch) + 0x02C))
#define ACCH_PM_OLD_POS(acch)        (*(cXyz**)((u8*)(acch) + 0x030))
#define ACCH_PM_SPEED(acch)          (*(cXyz**)((u8*)(acch) + 0x034))
#define ACCH_PM_ANGLE(acch)          (*(csXyz**)((u8*)(acch) + 0x038))
#define ACCH_PM_SHAPE_ANGLE(acch)    (*(csXyz**)((u8*)(acch) + 0x03C))
#define ACCH_M_LIN(acch)             ((cM3dGLin*)((u8*)(acch) + 0x040))
#define ACCH_M_WALL_CYL(acch)        ((cM3dGCyl*)((u8*)(acch) + 0x05C))
#define ACCH_M_BG_INDEX(acch)        (*(int*)((u8*)(acch) + 0x074))
#define ACCH_FIELD_0x78(acch)        (*(void**)((u8*)(acch) + 0x078))
#define ACCH_M_AP_ID(acch)           (*(fpc_ProcID*)((u8*)(acch) + 0x07C))
#define ACCH_M_MY_AC(acch)           (*(fopAc_ac_c**)((u8*)(acch) + 0x080))
#define ACCH_M_TBL_SIZE(acch)        (*(int*)((u8*)(acch) + 0x084))
#define ACCH_PM_ACCH_CIR(acch)       (*(void**)((u8*)(acch) + 0x088))
#define ACCH_M_GROUND_UP_H(acch)     (*(f32*)((u8*)(acch) + 0x08C))
#define ACCH_M_GROUND_UP_H_DIFF(acch) (*(f32*)((u8*)(acch) + 0x090))
#define ACCH_M_GROUND_H(acch)        (*(f32*)((u8*)(acch) + 0x094))
#define ACCH_M_GROUND_CHECK_OFFSET(acch) (*(f32*)((u8*)(acch) + 0x098))
#define ACCH_M_PLA(acch)             ((cM3dGPla*)((u8*)(acch) + 0x09C))
#define ACCH_FIELD_0xB0(acch)        (*(u8*)((u8*)(acch) + 0x0B0))
#define ACCH_FIELD_0xB4(acch)        (*(f32*)((u8*)(acch) + 0x0B4))
#define ACCH_FIELD_0xB8(acch)        (*(f32*)((u8*)(acch) + 0x0B8))
#define ACCH_M_ROOF_HEIGHT(acch)     (*(f32*)((u8*)(acch) + 0x0BC))
#define ACCH_M_ROOF_CRR_HEIGHT(acch) (*(f32*)((u8*)(acch) + 0x0C0))
#define ACCH_FIELD_0xC4(acch)        (*(f32*)((u8*)(acch) + 0x0C4))
#define ACCH_M_WTR_CHECK_OFFSET(acch) (*(f32*)((u8*)(acch) + 0x0C8))
#define ACCH_PM_OUT_POLY_INFO(acch)  (*(void**)((u8*)(acch) + 0x0CC))
#define ACCH_M_SEA_HEIGHT(acch)      (*(f32*)((u8*)(acch) + 0x0D0))
#define ACCH_M_GND(acch)             ((dBgS_GndChk*)((u8*)(acch) + 0x0D4))
#define ACCH_M_ROOF(acch)            ((void*)((u8*)(acch) + 0x128))
#define ACCH_M_WTR(acch)             ((void*)((u8*)(acch) + 0x174))

/* ============================================================================ */
/* J3D STRUCTURE OFFSETS (from tww-decomp)                                     */
/* ============================================================================ */

// J3DModelData offsets
// mJointTree at +0x10 (J3DJointTree.h: /* 0x18 */ u16 mJointNum, /* 0x1C */ J3DJoint** mJointNodePointer)
#define J3DMODELDATA_MJOINTTREE_JOINTNUM(modelData) (*(u16*)((u8*)(modelData) + 0x28))
#define J3DMODELDATA_MJOINTTREE_MPJOINTS(modelData) (*(J3DJoint***)((u8*)(modelData) + 0x2C))
#define J3DMODELDATA_MMATERIALTABLE(modelData)      ((void*)((u8*)(modelData) + 0x58))
#define J3DMODELDATA_MMATERIALTABLE_MMATERIALCOUNT(modelData) (*(u16*)((u8*)(modelData) + 0x58 + 0x04))
#define J3DMODELDATA_MMATERIALTABLE_MPMATERIALS(modelData) (*(J3DMaterial***)((u8*)(modelData) + 0x58 + 0x08))
#define J3DMODELDATA_MMATERIALTABLE_MPMATERIAL(modelData) (*(J3DMaterial***)((u8*)(modelData) + 0x60))
#define J3DMODELDATA_MMATERIALTABLE_MPTEXTURE(modelData)  (*(J3DTexture**)((u8*)(modelData) + 0x70))

// J3DModel offsets
#define J3DMODEL_MPMODELDATA(model)       (*(J3DModelData**)((u8*)(model) + 0x04))
#define J3DMODEL_MBASETRMTX(model)        ((void*)((u8*)(model) + 0x24))
#define J3DMODEL_MPNODEMTX(model)         (*(MTX34**)((u8*)(model) + 0x8C))  // getAnmMtx(i) == mpNodeMtx[i]

// J3DNode offsets (J3DNode.h: /* 0x08 */ J3DNodeCallBack mCallBack); J3DJoint derives from J3DNode
#define J3DNODE_MCALLBACK(node)           (*(void**)((u8*)(node) + 0x08))

// J3DJoint offsets
#define J3DJOINT_MJNTNO(joint)            (*(u16*)((u8*)(joint) + 0x18))    // J3DJoint.h /* 0x18 */ u16 mJntNo
#define J3DJOINT_MMTXCALC(joint)          (*(void**)((u8*)(joint) + 0x58))  // J3DJoint.h /* 0x58 */ J3DMtxCalc* mMtxCalc

// mDoExt_McaMorf offsets (m_Do_ext.h; checked against the vanilla ctor at 0x80012650, which
// NULLs 0x50/0x6C/0x70/0x80 and builds the J3DFrameCtrl at 0x58). `new` size: 0xB4 (li r3,0xB4
// before every ctor call in main.dol). The ctor takes a hidden 2nd arg (1 = build the virtual
// J3DMtxCalc base), which ww_functions.h spells `ushort param_1`.
#define MDOEXT_MCAMORF_SIZE               0xB4
#define MDOEXT_MCAMORF_MPMODEL(morf)      (*(J3DModel**)((u8*)(morf) + 0x50))  // J3DModel* mpModel
#define MDOEXT_MCAMORF_FRAME_END(morf)    (*(s16*)((u8*)(morf) + 0x58 + 0x08)) // mFrameCtrl.mEnd
#define MDOEXT_MCAMORF_FRAME(morf)        (*(f32*)((u8*)(morf) + 0x58 + 0x10)) // mFrameCtrl.mFrame
#define J3DJOINT_MPMATERIAL(joint)        (*(J3DMaterial**)((u8*)(joint) + 0x60))

// J3DMaterial offsets
#define J3DMATERIAL_MPSHAPE(material)     (*(J3DShape**)((u8*)(material) + 0x08))
#define J3DMATERIAL_MPNEXTMATERIAL(material) (*(J3DMaterial**)((u8*)(material) + 0x04))
#define J3DMATERIAL_MPMATERIALANM(material)  (*(void**)((u8*)(material) + 0x3C))

// J3DShape offsets
#define J3DSHAPE_MVISFLAGS(shape)         (*(u32*)((u8*)(shape) + 0x0C))
#define J3DShpFlag_Hide 0x0001

// J3DAnmTexPattern offsets (J3DAnimation.h:538-545; DOL daPy_lk_c::draw 0x801074C8-0x80107524)
// +0x0C is J3DAnmBase::mKind (s32), NOT the material count.
#define J3DANMTEXPATTERN_MFRAME(anm)      (*(f32*)((u8*)(anm) + 0x08))
#define J3DANMTEXPATTERN_MATERIALNUM(anm) (*(u16*)((u8*)(anm) + 0x1A))   // mUpdateMaterialNum
#define J3DANMTEXPATTERN_MATIDARRAY(anm)  (*(u16**)((u8*)(anm) + 0x1C))  // mUpdateMaterialID (pointer)
#define J3DANMTEXPATTERN_ANMTABLE(anm)    (*(u8**)((u8*)(anm) + 0x14))   // J3DAnmTexPatternFullTable[], stride 8
#define J3DANMTEXPATTERN_ENTRY_TEXNO(tbl, i) (((u8*)(tbl))[(i) * 8 + 4]) // FullTable.mTexNo at +4

// J3DAnmTextureSRTKey offsets
#define J3DANMTEXTURESRTKEY_MFRAME(anm)   (*(f32*)((u8*)(anm) + 0x08))

// J3DMaterialAnm offsets
#define J3DMATERIALANM_MTEXNOANM(anm)     ((void**)((u8*)(anm) + 0x2C))

// J3DGraphBase__j3dSys offsets
#define J3DSYS_MMODEL(j3dsys)             (*(J3DModel**)((u8*)(j3dsys) + 0x38))
#define J3DSYS_MOPADRAWBUFFER(j3dsys)     (*(J3DDrawBuffer**)((u8*)(j3dsys) + 0x48))
#define J3DSYS_MXLUDRAWBUFFER(j3dsys)     (*(J3DDrawBuffer**)((u8*)(j3dsys) + 0x4C))
#define J3DSYS_MTEXTURE(j3dsys)           (*(J3DTexture**)((u8*)(j3dsys) + 0x58))

/* ============================================================================ */
/* g_dComIfG_gameInfo OFFSETS                                                  */
/* ============================================================================ */

// dComIfG_inf_c (d_com_inf_game.h:904-906): save 0x0, play 0x12A0, drawlist 0x5D1C
#define GAMEINFO_DRAWLIST(gameInfo)       ((dDlst_list_c*)((u8*)(gameInfo) + 0x5D1C))
#define GAMEINFO_PLAYERSTATUS(gameInfo)   (*(u32*)((u8*)(gameInfo) + 0x5CC8))  // play.mPlayerStatus[0][0] (0x12A0+0x4A28; DOL 0x8010819C)
#define GAMEINFO_BGS(gameInfo)            ((void*)((u8*)(gameInfo) + 0x12A0))  // play.mBgS (first member of play)
// mResControl (d_com_inf_game.h "/* 0x1BFC0 */ dRes_control_c mResControl") .mObjectInfo[64] at +0
// (d_resorce.h) — what dComIfG_getObjectRes(arc, idx) passes to dRes_control_c::getRes.
#define GAMEINFO_RES_OBJECT_INFO(gameInfo) ((dRes_info_c*)((u8*)(gameInfo) + 0x1BFC0))
#define GAMEINFO_RES_OBJECT_INFO_COUNT    64
// play.mEvtManager (0x12A0+0x402C) .mException (+0x24): dEvent_exception_c {s32 mEventInfoIdx; s32 field_0x4; s32 mState;}
// — the global "start demo" record written by dComIfGp_evmng_startDemo (d_event_manager.cpp setStartDemo).
#define GAMEINFO_EVT_EXCEPTION(gameInfo)  ((u32*)((u8*)(gameInfo) + 0x52F0))
#define GAMEINFO_EVT_EXCEPTION_WORDS      3
// play.mEvtCtrl (d_com_inf_game.h:718, 0x12A0+0x3F38) .mMode (d_event.h:154, +0xC2): dComIfGp_event_runCheck() is mMode != 0
#define GAMEINFO_EVT_MODE(gameInfo)       (*(volatile u8*)((u8*)(gameInfo) + 0x529A))
// save.mSavedata.mEvent.mFlags (d_save.h:911, dSv_save_c 0x624): dSv_event_c::isEventBit(no) = mFlags[no >> 8] & (no & 0xFF) (d_save.cpp:1197)
#define GAMEINFO_EVENT_BITS(gameInfo)     ((volatile u8*)((u8*)(gameInfo) + 0x624))
// save.mSavedata.mPlayer.mInfo (d_save.h:600, 0x148) .mClearCount (d_save.h:467, +0x58) — dComIfGs_getClearCount()
#define GAMEINFO_CLEAR_COUNT(gameInfo)    (*(volatile u8*)((u8*)(gameInfo) + 0x1A0))
// play.m2dShow (d_com_inf_game.h:866, 0x12A0+0x4979): dComIfGp_2dShowCheck(), 0 while the game hides its
// 2D/HUD (dMeter_statusCheck d_meter.cpp:746; DOL 0x801EFC68: lbz r0,0x5C19(gameInfo))
#define GAMEINFO_2D_SHOW(gameInfo)        (*(volatile u8*)((u8*)(gameInfo) + 0x5C19))
// play.mPlayerStatus[0][1] (d_com_inf_game.h:874, 0x12A0+0x4A2C): dComIfGp_checkPlayerStatus1(0, ...)
#define GAMEINFO_PLAYERSTATUS1(gameInfo)  (*(u32*)((u8*)(gameInfo) + 0x5CCC))
// play.mCurrentGrafPort (d_com_inf_game.h:883, 0x12A0+0x4A60): the painter's 2D J2DOrthoGraph, set before the 2D
// lists draw (m_Do_graphic.cpp:1572-1576). DOL dDlst_2DNumber_c::draw 0x800C8664: lwz r3,0x5D00(gameInfo)
#define GAMEINFO_CURRENT_GRAF_PORT(gameInfo) (*(J2DOrthoGraph**)((u8*)(gameInfo) + 0x5D00))

// ResTIMG (JSystem/JUtility/JUTTexture.h:14-37). Offsets are relative to the header itself;
// J3DTexture::setResTIMG (J3DTexture.h:47) rebases them when a header is copied into TEX1.
#define RESTIMG_FORMAT(t)        (*(u8*)((u8*)(t) + 0x00))
#define RESTIMG_WIDTH(t)         (*(u16*)((u8*)(t) + 0x02))
#define RESTIMG_HEIGHT(t)        (*(u16*)((u8*)(t) + 0x04))
#define RESTIMG_INDEX_TEXTURE(t) (*(u8*)((u8*)(t) + 0x08))
#define RESTIMG_IMAGE_OFFSET(t)  (*(u32*)((u8*)(t) + 0x1C))
#define RESTIMG_WORDS            (0x20 / 4)

/* ============================================================================ */
/* dKy_tevstr_c FIELD ACCESS (TEV string for lighting)                         */
/* From tww-decomp/include/d/d_kankyo.h                                        */
/* ============================================================================ */

// dKy_tevstr_c at fopAc_ac_c + 0x10C (from FOPAC_TEV_STR)
// d_kankyo.h:109-118: +0x00 J3DLightObj mLightObj, +0x90 GXColorS10 mFogColor, +0x98 mFogStartZ, +0x9C mFogEndZ
// (confirmed by DOL daPy_lk_c::draw 0x801073CC: actor+0x19C/0x19E/0x1A0/0x1A4/0x1A8)
#define TEVSTR_MFOGCOLOR_R(tevstr)        (*(s16*)((u8*)(tevstr) + 0x90))
#define TEVSTR_MFOGCOLOR_G(tevstr)        (*(s16*)((u8*)(tevstr) + 0x92))
#define TEVSTR_MFOGCOLOR_B(tevstr)        (*(s16*)((u8*)(tevstr) + 0x94))
#define TEVSTR_MFOGCOLOR_A(tevstr)        (*(s16*)((u8*)(tevstr) + 0x96))
#define TEVSTR_MFOGSTARTZ(tevstr)         (*(f32*)((u8*)(tevstr) + 0x98))
#define TEVSTR_MFOGENDZ(tevstr)           (*(f32*)((u8*)(tevstr) + 0x9C))

/* ============================================================================ */
/* dDlst_list_c FIELD ACCESS (draw list)                                       */
/* From tww-decomp/include/d/d_drawlist.h                                      */
/* ============================================================================ */

// d_drawlist.h:664-678 (confirmed by DOL daPy_lk_c::draw: P0 = gameInfo+0x5D24, P1 = +0x5D28/+0x5D2C,
// dComIfGd_setList() = +0x5D38/+0x5D3C)
#define DDLST_LIST_MPOPALISTSKY(drawlist)       (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x00))
#define DDLST_LIST_MPXLULISTSKY(drawlist)       (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x04))
#define DDLST_LIST_MPOPALISTP0(drawlist)        (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x08))
#define DDLST_LIST_MPOPALISTP1(drawlist)        (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x0C))
#define DDLST_LIST_MPXLULISTP1(drawlist)        (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x10))
#define DDLST_LIST_MPOPALISTBG(drawlist)        (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x14))
#define DDLST_LIST_MPXLULISTBG(drawlist)        (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x18))
#define DDLST_LIST_MPOPALIST(drawlist)          (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x1C))
#define DDLST_LIST_MPXLULIST(drawlist)          (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x20))
#define DDLST_LIST_MPOPALISTINVISIBLE(drawlist) (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x30))
#define DDLST_LIST_MPXLULISTINVISIBLE(drawlist) (*(J3DDrawBuffer**)((u8*)(drawlist) + 0x34))
// 2D opaque list cursor/end (d_drawlist.h:686-687), what dComIfGd_set2DOpa passes to dDlst_list_c::set
// (DOL dPn_c::_draw 0x80161570: addi r4,drawlist,0x19C; addi r5,drawlist,0x1A0)
#define DDLST_LIST_MP2DOPA_P(drawlist)          ((dDlst_base_c***)((u8*)(drawlist) + 0x19C))
#define DDLST_LIST_MP2DOPAEND_P(drawlist)       ((dDlst_base_c***)((u8*)(drawlist) + 0x1A0))
// mpViewPort / mpView (d_drawlist.h:692-693): NULL until a camera has set them; mDoLib_pos2camera and
// mDoLib_project dereference them unchecked (m_Do_lib.cpp:82, 109)
#define DDLST_LIST_MPVIEWPORT(drawlist)         (*(void**)((u8*)(drawlist) + 0x230))
#define DDLST_LIST_MPVIEW(drawlist)             (*(void**)((u8*)(drawlist) + 0x234))
// m3DLineMatSortPacket[2] (d_drawlist.h:699, size 0x14 each), what dComIfGd_set3DlineMat passes to
// mDoExt_3DlineMatSortPacket::setMat with the line's getMaterialID() (DOL daHimo2_Draw 0x800EC56C:
// mulli id,0x14; addis 1; addi 0x6078 onto drawlist = gameInfo+0x5D1C)
#define DDLST_LIST_3DLINEMATSORTPACKET(drawlist, id) \
  ((mDoExt_3DlineMatSortPacket*)((u8*)(drawlist) + 0x16078 + ((id) * 0x14)))

// JUTFont (JUTFont.h): vtable at +0 (verified against __vt__10JUTResFont 0x8039D1A8 in main.dol),
// mValid +0x04, mColor1..4 (TColor = RGBA u32, what drawChar_scale's GXColor1u32 sends) +0x0C..+0x18.
#define JUTFONT_VALID(font)                     (*(u8*)((u8*)(font) + 0x04))
#define JUTFONT_COLORS(font)                    ((u32*)((u8*)(font) + 0x0C))
#define JUTFONT_VT_SET_GX                       (0x0C / 4)  // setGX()
#define JUTFONT_VT_GET_WIDTH_ENTRY              (0x2C / 4)  // getWidthEntry(int, TWidth*) const
#define JUTFONT_VT_GET_CELL_WIDTH               (0x30 / 4)
#define JUTFONT_VT_GET_CELL_HEIGHT              (0x34 / 4)
// Legacy names (previously pointed at the sky lists). Kept as aliases to the lists they were
// meant to name so nothing silently regresses if an old caller reappears.
#define DDLST_LIST_MPLINKBUF(drawlist)              DDLST_LIST_MPOPALISTP0(drawlist)
#define DDLST_LIST_MPBUFINVISIBLEMODELOPA(drawlist) DDLST_LIST_MPOPALISTINVISIBLE(drawlist)
#define DDLST_LIST_MPBUFINVISIBLEMODELXLU(drawlist) DDLST_LIST_MPXLULISTINVISIBLE(drawlist)

/* ============================================================================ */
/* INLINE FUNCTIONS                                                            */
/* ============================================================================ */

/* Actor condition flag operations */
static inline u32 fopAcM_CheckCondition(fopAc_ac_c *p_actor, u32 flag)
{
    return FOPAC_ACTOR_CONDITION(p_actor) & flag;
}

static inline void fopAcM_OnCondition(fopAc_ac_c *p_actor, u32 flag)
{
    FOPAC_ACTOR_CONDITION(p_actor) |= flag;
}

static inline void fopAcM_OffCondition(fopAc_ac_c *p_actor, u32 flag)
{
    FOPAC_ACTOR_CONDITION(p_actor) &= ~flag;
}

/* Actor status flag operations */
static inline bool fopAcM_CheckStatus(fopAc_ac_c *pActor, u32 status)
{
    return FOPAC_ACTOR_STATUS(pActor) & status;
}

static inline void fopAcM_SetStatus(fopAc_ac_c *actor, u32 status)
{
    FOPAC_ACTOR_STATUS(actor) = status;
}

/* Carry status check */
#define fopAcStts_CARRY_e 0x00002000
static inline u32 fopAcM_checkCarryNow(fopAc_ac_c *pActor)
{
    return FOPAC_ACTOR_STATUS(pActor) & fopAcStts_CARRY_e;
}

/* Actor position and transform accessors */
static inline cXyz *fopAcM_GetPosition_p(fopAc_ac_c *pActor)
{
    return FOPAC_CURRENT_POS(pActor);
}

static inline cXyz *fopAcM_GetOldPosition_p(fopAc_ac_c *pActor)
{
    return &FOPAC_OLD(pActor)->pos;
}

static inline cXyz *fopAcM_GetSpeed_p(fopAc_ac_c *pActor)
{
    return FOPAC_SPEED(pActor);
}

static inline csXyz *fopAcM_GetAngle_p(fopAc_ac_c *pActor)
{
    return FOPAC_CURRENT_ANGLE(pActor);
}

static inline csXyz *fopAcM_GetShapeAngle_p(fopAc_ac_c *pActor)
{
    return FOPAC_SHAPE_ANGLE(pActor);
}

/* Actor room number operations */
static inline s8 fopAcM_GetRoomNo(fopAc_ac_c *pActor)
{
    return FOPAC_CURRENT_ROOMNO(pActor);
}

static inline void fopAcM_SetRoomNo(fopAc_ac_c *actor, s8 roomNo)
{
    FOPAC_CURRENT_ROOMNO(actor) = roomNo;
}

static inline s8 fopAcM_GetHomeRoomNo(fopAc_ac_c *pActor)
{
    return FOPAC_HOME(pActor)->roomNo;
}

static inline void fopAcM_SetHomeRoomNo(fopAc_ac_c *pActor, s8 roomNo)
{
    FOPAC_HOME(pActor)->roomNo = roomNo;
}

/* Model operations */
static inline void fopAcM_SetModel(fopAc_ac_c *actor, J3DModel *model)
{
    FOPAC_MODEL(actor) = model;
}

static inline J3DModel *fopAcM_GetModel(fopAc_ac_c *actor)
{
    return FOPAC_MODEL(actor);
}

/* Matrix operations */
static inline MTX34 *fopAcM_GetMtx(fopAc_ac_c *pActor)
{
    return pActor->cullMtx;
}

static inline void fopAcM_SetMtx(fopAc_ac_c *actor, MTX34 *m)
{
    actor->cullMtx = m;
}

/* Physics operations */
static inline void fopAcM_SetGravity(fopAc_ac_c *actor, f32 gravity)
{
    FOPAC_GRAVITY(actor) = gravity;
}

static inline void fopAcM_SetMaxFallSpeed(fopAc_ac_c *actor, f32 speed)
{
    FOPAC_MAX_FALL_SPEED(actor) = speed;
}

static inline void fopAcM_SetSpeedF(fopAc_ac_c *actor, f32 f)
{
    FOPAC_SPEED_F(actor) = f;
}

/* Group operations */
static inline u8 fopAcM_GetGroup(fopAc_ac_c *p_actor)
{
    return FOPAC_GROUP(p_actor);
}

static inline void fopAcM_SetGroup(fopAc_ac_c *pActor, u8 group)
{
    FOPAC_GROUP(pActor) = group;
}

/* J3DShape visibility operations */
static inline void J3DShape__show(J3DShape *shape)
{
    if (shape) J3DSHAPE_MVISFLAGS(shape) &= ~J3DShpFlag_Hide;
}

static inline void J3DShape__hide(J3DShape *shape)
{
    if (shape) J3DSHAPE_MVISFLAGS(shape) |= J3DShpFlag_Hide;
}

/* Draw list priority operations */
#define OFFSET_gameinfo_drawlist 0x05D1C
#define OFFSET_drawlist_mpOpaListP0 0x08
#define OFFSET_drawlist_mpOpaListP1 0x0C
#define OFFSET_drawlist_mpXluListP1 0x10
#define OFFSET_drawlist_mpOpaList 0x1C
#define OFFSET_drawlist_mpXluList 0x20
#define OFFSET_j3dSys_mDrawBuffer 0x48

/* dComIfGd_setList - the default (vanilla end-of-draw) opa/xlu lists */
static inline void dComIfGd_setList(void)
{
    u8 *drawlist = (u8 *)&g_dComIfG_gameInfo + OFFSET_gameinfo_drawlist;
    J3DSYS_MOPADRAWBUFFER(&J3DGraphBase__j3dSys) = *(J3DDrawBuffer **)(drawlist + OFFSET_drawlist_mpOpaList);
    J3DSYS_MXLUDRAWBUFFER(&J3DGraphBase__j3dSys) = *(J3DDrawBuffer **)(drawlist + OFFSET_drawlist_mpXluList);
}

static inline void dComIfGd_setListP0(void)
{
    u8 *drawlist = (u8 *)&g_dComIfG_gameInfo + OFFSET_gameinfo_drawlist;
    J3DDrawBuffer **mpOpaListP0 = (J3DDrawBuffer **)((u8 *)drawlist + OFFSET_drawlist_mpOpaListP0);
    J3DSYS_MOPADRAWBUFFER(&J3DGraphBase__j3dSys) = *mpOpaListP0;
    J3DSYS_MXLUDRAWBUFFER(&J3DGraphBase__j3dSys) = *mpOpaListP0;
}

static inline void dComIfGd_setListP1(void)
{
    u8 *drawlist = (u8 *)&g_dComIfG_gameInfo + OFFSET_gameinfo_drawlist;
    J3DDrawBuffer **mpOpaListP1 = (J3DDrawBuffer **)((u8 *)drawlist + OFFSET_drawlist_mpOpaListP1);
    J3DDrawBuffer **mpXluListP1 = (J3DDrawBuffer **)((u8 *)drawlist + OFFSET_drawlist_mpXluListP1);
    J3DSYS_MOPADRAWBUFFER(&J3DGraphBase__j3dSys) = *mpOpaListP1;
    J3DSYS_MXLUDRAWBUFFER(&J3DGraphBase__j3dSys) = *mpXluListP1;
}

/* J3DAnmTexPattern inline helpers */
typedef struct {
    u16 mMaxFrame;
    u16 mOffset;
    u8 mTexNo;
    u16 _padding;
} PuppetAnimTableEntry;

#define OFFSET_J3DAnmTexPattern_mUpdateMaterialNum 0x1A
#define OFFSET_J3DAnmTexPattern_mUpdateMaterialID 0x1C
#define OFFSET_J3DAnmTexPattern_mAnmTable 0x14
#define OFFSET_AnimTableEntry_mTexNo 0x04

static inline u16 J3DAnmTexPattern__getUpdateMaterialNum(J3DAnmTexPattern *p_anm)
{
    return *(u16 *)((u8 *)p_anm + OFFSET_J3DAnmTexPattern_mUpdateMaterialNum);
}

static inline u16 J3DAnmTexPattern__getUpdateMaterialID(J3DAnmTexPattern *p_anm, u16 idx)
{
    u16 **mUpdateMaterialID = (u16 **)((u8 *)p_anm + OFFSET_J3DAnmTexPattern_mUpdateMaterialID);
    return (*mUpdateMaterialID)[idx];
}

static inline PuppetAnimTableEntry *J3DAnmTexPattern__getAnmTable(J3DAnmTexPattern *p_anm)
{
    return *(PuppetAnimTableEntry **)((u8 *)p_anm + OFFSET_J3DAnmTexPattern_mAnmTable);
}

static inline u8 PuppetAnimTableEntry__getTexNo(PuppetAnimTableEntry *entry)
{
    return *(u8 *)((u8 *)entry + OFFSET_AnimTableEntry_mTexNo);
}

/* J3DMaterialAnm inline helpers */
#define OFFSET_J3DMaterial_mMaterialAnm 0x3C
#define OFFSET_J3DMaterialAnm_mTexNoAnm 0x2C

static inline J3DMaterialAnm *J3DMaterial__getMaterialAnm(J3DMaterial *mtl)
{
    J3DMaterialAnm *anm = *(J3DMaterialAnm **)((u8 *)mtl + OFFSET_J3DMaterial_mMaterialAnm);
    if ((u32)anm >= 0xC0000000) {
        return NULL;
    }
    return anm;
}

static inline void J3DMaterialAnm__setTexNoAnm(J3DMaterialAnm *mtlAnm, int i, J3DTexNoAnm *pAnm)
{
    J3DTexNoAnm **mTexNoAnm = (J3DTexNoAnm **)((u8 *)mtlAnm + OFFSET_J3DMaterialAnm_mTexNoAnm);
    mTexNoAnm[i] = pAnm;
}

/* Camera and event helpers */
/*
 * play.mCameraInfo[idx].mCameraAttentionStatus
 * play at 0x12A0, mCameraInfo at play+0x4870, sizeof(dComIfG_camera_info_class) = 0x34,
 * mCameraAttentionStatus at +0x08  => gameInfo + 0x5B18 + idx*0x34
 * (d_com_inf_game.h:145,776,905; DOL daPy_lk_c::draw 0x801073A0-0x801073AC)
 */
static inline u32 dComIfGp_checkCameraAttentionStatus(int idx, u32 flag)
{
    u32 *status = (u32 *)((u8 *)&g_dComIfG_gameInfo + 0x5B18 + (idx * 0x34));
    return *status & flag;
}

/**
 * dComIfGp_att_getZHint - Get Z-target hint actor
 *
 * From tww-decomp: dComIfGp_getAttention().getZHintTarget()
 * - g_dComIfG_gameInfo.play is at offset 0x12A0 in dComIfG_inf_c
 * - mAttention is at offset 0x4568 in dComIfG_play_c
 * - mHint is at offset 0x124 in dAttention_c (d_attention.h:289)
 * - mZHintTargetID is at offset 0x8 in dAttHint_c (it's a fpc_ProcID)
 *   => gameInfo + 0x5934
 *
 * We need to convert the ProcID to an actor pointer using fopAcM_SearchByID
 */
static inline fopAc_ac_c *dComIfGp_att_getZHint(void)
{
    // g_dComIfG_gameInfo.play.mAttention.mHint.mZHintTargetID
    // 0x12A0 (play) + 0x4568 (mAttention) + 0x124 (mHint) + 0x8 (mZHintTargetID) = 0x5934
    fpc_ProcID *zHintID = (fpc_ProcID *)((u8 *)&g_dComIfG_gameInfo + 0x5934);
    if (*zHintID == (fpc_ProcID)-1)
    {
        return NULL;
    }
    fopAc_ac_c *result = NULL;
    fopAcM_SearchByID(*zHintID, &result);
    return result;
}

/* ============================================================================ */
/* WORLD SYNC (puppet_worldsync.c) — process header, save flags, stage info,    */
/* daItem_c fields. All offsets from tww-decomp, verified in the GZLE01 DOL.    */
/* ============================================================================ */

// base_process_class (f_pc_base.h:13-32)
#define BASE_PROC_NAME(p)        (*(s16*)((u8*)(p) + 0x08))  // mProcName (fpcBs_Create copies profile mProcName)
#define BASE_INIT_STATE(p)       (*(s8*)((u8*)(p) + 0x0C))   // mInitState: 3 = already queued for delete (fpcDt_ToDeleteQ)
#define BASE_CREATE_RESULT(p)    (*(s8*)((u8*)(p) + 0x0D))   // mCreateResult: cPhs_NEXT_e (2) once create finished (fpcBs_SubCreate)
#define BASE_PARAMETERS(p)       (*(u32*)((u8*)(p) + 0xB0))  // mParameters (DOL 0x800F5550: lwz r0,0xB0(r31))
#define BASE_CREATE_DONE         2                           // cPhs_NEXT_e as stored in mCreateResult
#define BASE_INIT_STATE_DELETING 3

// fopAc_ac_c extras (f_op_actor.h)
#define FOPAC_HOME_ROOMNO(ac)    (*(s8*)((u8*)(ac) + 0x1D0 + 0x12))  // home.roomNo (DOL 0x800F55B4: lbz r5,0x1E2)
#define FOPAC_CND_INIT           0x08        // fopAcCnd_INIT_e (f_op_actor.h:51)
#define FOPAC_STTS_HOOK_CARRY    0x00100000  // fopAcStts_HOOK_CARRY_e (f_op_actor.h:39)

// dSv_info_c lives at g_dComIfG_gameInfo + 0 (d_save.h:991-1001):
//   mSavedata.mMemory[16] at +0x380 (stride 0x24), live mMemory at +0x778, mDan at +0x79C, mZone[32] at +0x7A8.
// dSv_memBit_c (d_save.h:684-689): mTbox 0x00, mSwitch[4] 0x04, mItem[1] 0x14, mVisitedRoom[2] 0x18,
//   mKeyNum 0x20, mDungeonItem 0x21. NOTE dSv_memBit_c::onItem accepts 0..63 but mItem has ONE word, so
//   item bits 32..63 alias mVisitedRoom[0] (d_save.cpp:1112-1121).
#define GAMEINFO_SAVE_MEMORY(gameInfo, i)  ((u8*)(gameInfo) + 0x380 + ((i) * 0x24))  // mSavedata.mMemory[i]
#define GAMEINFO_LIVE_MEMORY(gameInfo)     ((u8*)(gameInfo) + 0x778)                 // mMemory (current stage), 0x803C5380
#define MEMBIT_TBOX(mb)                    (*(u32*)((u8*)(mb) + 0x00))
#define MEMBIT_SWITCH(mb, i)               (*(u32*)((u8*)(mb) + 0x04 + ((i) * 4)))
#define MEMBIT_ITEM0(mb)                   (*(u32*)((u8*)(mb) + 0x14))
#define MEMBIT_VISITED_ROOM(mb, i)         (*(u32*)((u8*)(mb) + 0x18 + ((i) * 4)))
#define MEMBIT_KEY_NUM(mb)                 (*(u8*)((u8*)(mb) + 0x20))
#define MEMBIT_DUNGEON_ITEM(mb)            (*(u8*)((u8*)(mb) + 0x21))
#define DSV_STAGE_MAX                      16    // dSv_save_c::STAGE_MAX
#define DSV_MEMORY_ITEM_MAX                0x40  // dSv_info_c::MEMORY_ITEM (item bits >= this are per-room zone bits)
#define DSV_ITEM_BIT_NONE                  0x7F

// play.mStageData (dStage_stageDt_c, d_com_inf_game.h:716) at play 0x3EB0 => gameInfo + 0x5150 (0x803C9D58).
// mpStagInfo at +0x48 (d_stage.h:586; DOL getStagInfo 0x80044EE4: lwz r3,0x48(r3)).
// stage_stag_info_class.mProp at +0x09 (d_stage.h:70); dStage_stagInfo_GetSaveTbl = (mProp >> 1) & 0x7F
// (d_stage.h:1027; DOL dComIfGs_onStageTbox 0x800538C0: lbz r0,9(r3); rlwinm r0,r0,31,25,31).
#define GAMEINFO_STAGE_STAGINFO(gameInfo)  (*(u8**)((u8*)(gameInfo) + 0x5150 + 0x48))
#define STAGINFO_SAVE_TBL(stag)            ((*((u8*)(stag) + 0x09) >> 1) & 0x7F)
// play.mNextStage.mEnable (d_com_inf_game.h:714, d_stage.h:966): 0x12A0 + 0x3EA0 + 0xC => nonzero while a stage change is pending
#define GAMEINFO_NEXT_STAGE_ENABLE(gameInfo) (*(s8*)((u8*)(gameInfo) + 0x514C))

// g_Counter.mCounter0 (c_counter.h) — ++ once per game frame in fapGm_Execute (f_ap_game.cpp:80)
#define SCOMPONENT_FRAME_COUNTER()         (*(volatile u32*)&SComponent__g_Counter)

// daItem_c (d_a_item.h / d_a_itembase.h, STATIC_ASSERT sizeof 0x6C0). fpcNm_ITEM_e = 0x101 (f_pc_name.h:269;
// DOL 0x800F681C li r0,0x101 before fopAcIt_Judge). Params: itemNo = prm & 0xFF, itemBitNo = (prm >> 8) & 0xFF,
// masked to 0x7F unless itemNo is BLUE_JELLY (0x4B), which uses it as a STAGE_BLUE_CHU_JELLY save switch
// (_daItem_create, DOL 0x800F5584-0x800F55A4).
#define PROC_NAME_ITEM                     0x101
#define DAITEM_ITEMNO_BLUE_JELLY           0x4B
#define DAITEM_PRM_ITEM_NO(prm)            ((prm) & 0xFF)
#define DAITEM_PRM_ITEM_BIT(prm)           (((prm) >> 8) & 0x7F)
#define DAITEM_ITEM_BIT_NO(it)             (*(s32*)((u8*)(it) + 0x630))  // daItemBase_c::mItemBitNo
#define DAITEM_ITEM_NO(it)                 (*(u8*)((u8*)(it) + 0x63A))   // daItemBase_c::m_itemNo
#define DAITEM_FLAG(it)                    (*(u8*)((u8*)(it) + 0x669))   // daItem_c::mFlag
#define DAITEM_ITEM_STATUS(it)             (*(u8*)((u8*)(it) + 0x66B))   // daItem_c::mItemStatus (DOL 0x800F644C)
#define DAITEM_FLAG_BOOMERANG              0x08
#define DAITEM_FLAG_HOOK                   0x40
// mItemStatus: 0/1 = idle on the ground; 2 = locked by another actor (setLock); 3 = carried (rat, startControl);
// 5/6 = picked up (normal), 7/8/9 = get-item demo; 0xA/0xB = boss drop wait. itemGetExecute sets 5 or 7 AND
// sets the save item bit in the same call (d_a_item.cpp:582-768).
#define DAITEM_STATUS_IDLE0                0
#define DAITEM_STATUS_IDLE1                1
// A boss's Heart Container waits in 0xA/0xB (execWaitMainFromBoss, d_a_item.cpp:298-300, 488-500) until checkGetItem
// starts the get demo (7-9).
#define DAITEM_STATUS_WAIT_BOSS1           0xA
#define DAITEM_STATUS_WAIT_BOSS2           0xB
#define DAITEM_ITEMNO_HEART_CONTAINER      0x08  // dItemNo_HEART_CONTAINER_e (d_item_data.h:15)
// dSv_memBit_c::mDungeonItem bit STAGE_LIFE (d_save.h:644; isDungeonItem = mDungeonItem & (1 << i), d_save.cpp:1147):
// set only by item_func_utuwa_heart, when a Heart Container is picked up (d_item.cpp:587-603).
#define MEMBIT_DUNGEON_STAGE_LIFE          (1 << 4)

/* ============================================================================ */
/* LIVE WORLD (puppet_liveworld.c) — actors that read a flag only at create.    */
/* Offsets verified in the GZLE01 DOL / the actor RELs (docs/live-world.md).    */
/* ============================================================================ */

// base_process_class::mLyTg.mpLayer (f_pc_base.h:21 + f_pc_layer_tag.h: create_tag_class is 0x14):
// the layer the process lives in. fpcBs_Execute makes it current around every execute (DOL 0x8003C924:
// lwz r3,0x2C(r31); bl fpcLy_SetCurrentLayer), and fopAcM_create creates in the CURRENT layer (DOL 0x80024568).
#define BASE_LAYER(p)            (*(layer_class**)((u8*)(p) + 0x2C))
// fopAc_Create copies the create append (DOL 0x80023CD4..): home.pos 0x1D0, home.angle 0x1DC, argument 0x1C1,
// scale 0x214 (= append scale * 0.1; x10 -> u8 -> x0.1 round-trips every u8 exactly), home.roomNo 0x1E2.
#define FOPAC_HOME_POS(ac)       ((cXyz*)((u8*)(ac) + 0x1D0))
#define FOPAC_HOME_ANGLE(ac)     ((csXyz*)((u8*)(ac) + 0x1DC))

// play.mEvtCtrl.mOrderCount (d_event.h:152, +0xC0; DOL dEvt_control_c::order 0x8006FF04: lbz r0,0xC0(r30)):
// events ordered this frame, started by the next dEvt_control_c::check. An order holds ACTOR POINTERS
// (d_event.cpp:45), so never delete an actor while one is queued.
#define GAMEINFO_EVT_ORDER_COUNT(gameInfo) (*(volatile s8*)((u8*)(gameInfo) + 0x5298))

// dStage_roomControl_c::mStatus[64] (d_stage.h:855-869, stride 0x114). mFlags +0x104: 0x01 loaded, 0x02 loading,
// 0x04 unloading (d_stage.cpp:216-240, d_s_room.cpp:93-99); mZoneNo +0x107 (DOL checkRoomDisp 0x80040E48: lbz r3,0x104;
// getZoneNo 0x8005DCE0: lbz r3,0x107).
#define ROOM_STATUS(room)        ((u8*)dStage_roomControl_c__mStatus + ((room) * 0x114))
#define ROOM_STATUS_FLAGS(room)  (*(volatile u8*)(ROOM_STATUS(room) + 0x104))
#define ROOM_STATUS_ZONE_NO(room) (*(volatile s8*)(ROOM_STATUS(room) + 0x107))
#define ROOM_FLAG_LOADED         0x01
#define ROOM_FLAG_BUSY           0x06  // loading or unloading
#define ROOM_MAX                 64
#define ZONE_MAX                 32    // dSv_info_c::ZONE_MAX; isSwitch ASSERTS (OSPanic) on a zone switch of a room without one

// Proc names, read from each REL's g_profile (+0x08) in the vanilla RELS.arc / files/rels.
#define PROC_NAME_TBOX           0x126 // d_a_tbox
#define PROC_NAME_WALL           0x1B1 // d_a_wall (bombable wall)
#define PROC_NAME_FLOOR          0x062 // d_a_floor (breakable floor)
#define PROC_NAME_OBJ_ICE        0x1D2 // d_a_obj_ice (ice block)
#define PROC_NAME_MJDOOR         0x047 // d_a_obj_majyuu_door (FF barricade)
#define PROC_NAME_SAKU           0x191 // d_a_saku (wooden barricade)
#define PROC_NAME_SWHIT0         0x1C9 // d_a_swhit0 (crystal switch)
#define PROC_NAME_DOOR10         0x12E // d_a_door10
#define PROC_NAME_DOOR12         0x12F // d_a_door12
#define PROC_NAME_STONE2         0x1CD // d_a_stone2 (black boulder Ebrock / Ekao: covers DRC's and ET's warp jars)
#define PROC_NAME_MKIEK          0x04E // d_a_obj_mkiek (wall that dissolves in mirror-shield light)
#define PROC_NAME_OBJ_WARPT      0x043 // d_a_obj_warpt (warp jar)
#define PROC_NAME_SS             0x0FD // d_a_ss (Forbidden Woods door plant)
#define PROC_NAME_MKIE           0x04D // d_a_obj_mkie (Earth Temple statue that dissolves in mirror-shield light)
#define PROC_NAME_MKNJD          0x04F // d_a_obj_mknjd (Earth God's Lyric / Wind God's Aria statue)
#define PROC_NAME_VMC            0x036 // d_a_obj_vmc (Wind Temple soil mound + Makar tree)
#define PROC_NAME_VFAN           0x038 // d_a_obj_vfan (Ganon's Tower Phantom Ganon door)
#define PROC_NAME_LEAVES         0x092 // d_a_obj_leaves (orange leaf pile, Deku Leaf)
#define PROC_NAME_KOKIIE         0x064 // d_a_kokiie (Forbidden Woods hanging flower house)
#define PROC_NAME_MFLFT          0x05D // d_a_mflft (Dragon Roost Cavern flame lift)

// daTbox_c (d_a_tbox.h:35-36, d_a_tbox.cpp:23-31): tbox no. = (prm >> 7) & 0x1F, swNo = (prm >> 12) & 0xFF,
// funcType = prm & 0x7F. checkOpen (REL .text 0xCD8) reads STAGE_SEA2's tbox for funcs 7/8, else the live one.
#define TBOX_FUNC(prm)           ((prm) & 0x7F)
#define TBOX_FUNC_ENEMIES        2     // appears when your enemies are dead; its switch is read only at create
#define TBOX_FUNC_EXTRA_SAVE     7     // 7 and 8 read another stage's chest bits

// Doors (d_door.cpp: getSwbit = prm & 0xFF, DOL 0x8006B39C). dDoor_key2_c mbEnabled at +0 (keyOn/keyOff DOL
// 0x8006C948/0x8006C954): read only, to skip a door with no lock showing. The action byte selects l_action[]
// (1 = Wait). Live world re-creates a locked door; it never writes these.
#define DOOR10_KEYLOCK_OFF       0x308 // door10 setKey: addi r3,r31,0x308 before keyOn/keyOff
#define DOOR10_ACTION_OFF        0x354 // door10 actionInit: li r0,1; stb r0,0x354(r31)
#define DOOR12_KEYLOCK_OFF       0x2E4 // door12 setKey: addi r3,r31,0x2E4
#define DOOR12_ACTION_OFF        0x314 // door12 actionInit: stb r0,0x314(r31)
#define DOOR_ACTION_WAIT         1
#define DOOR12_ARG1_TMPBIT       8     // door12 create: arg1 (home.angle.z >> 8) 8 clears tmp bit 0x0440 (d_a_door12.cpp:581)

// daStone2::Act_c (d_a_stone2.h): swSave = (prm >> 8) & 0xFF (is_switch, REL .text 0x594: PrmAbstract(8, 8));
// m648 mode s32 +0x648, 0 wait / 1 carried / 2 thrown / 3 breaking (mode_wait_init 0x1BB4: stw r0,0x648(r3)).
// Read only: its low byte (big-endian).
#define STONE2_MODE_OFF          0x64B
#define STONE2_MODE_WAIT         0
// daObj_Mkiek: swSave = prm & 0xFF (Mthd_Create, REL .text 0x414: PrmAbstract(8, 0)); create stops once it is set.

// daObj_Warpt_c (d_a_obj_warpt.h, getArg REL .text 0x1D7C): type m2B4 = prm & 0xF (2-4: the three-way dungeon jars,
// lid in an event register); a normal jar's lid switch m2AC = home.angle.x & 0xFF (lha 0x1DC; stw 0x2AC). m2C6 isHuta
// byte +0x2C6 (isHuta 0x750: lbz r3,0x2C6(r3)): a lid is on. m290 mode s32 +0x290 (modeProc 0x1984: stw r5,0x290(r3)):
// 0 open, 1 closed, 2 lid burning, 3 warp event, 4 open event. Read only: its low byte.
#define WARPT_TYPE(prm)          ((prm) & 0xF)
#define WARPT_TYPE_SP_FIRST      2     // types 2, 3, 4 (isSp)
#define WARPT_LID_OFF            0x2C6
#define WARPT_MODE_OFF           0x293
#define WARPT_MODE_CLOSE         1
/* PROJECTILES (puppet_fx.c) — the local Link's bombs / cannonballs and the    */
/* copies of other players'. d_a_bomb (daBomb_c, d_a_bomb.h) is in main.dol;   */
/* offsets verified in the GZLE01 DOL at the cited instructions.              */
/* ============================================================================ */

#define BASE_PROC_ID(p)                    (*(u32*)((u8*)(p) + 0x04))  // base_process_class::mBsPcId (f_pc_base.h:14), fopAcM_GetID
#define FPC_PROC_ID_ERROR                  0xFFFFFFFF                  // fpcM_ERROR_PROCESS_ID_e (daPy_actorKeep_c::clearData)

// daPy_lk_c::mActorKeepGrab (d_a_player_main.h:2088) = daPy_actorKeep_c {mID +0, mActor +4}
#define DAPY_LK_GRAB_ID(link)              (*(u32*)((u8*)(link) + 0x318C))

// daBomb_c (d_a_bomb.h:192-240)
#define DABOMB_AT_SPRM(b)                  (*(u32*)((u8*)(b) + 0x5A0))  // mSph's AT SPrm (setBombNoHit 0x800680A0; procExplode_init ORs Set only, 0x800DB498)
#define DABOMB_OWNED_BY_LINK(b)            (*(u8*)((u8*)(b) + 0x6F0))   // field_0x6F0: STATE_3 sets it (create_init 0x800DD118); explode/delete then call the
                                                                        // LOCAL Link's decrementBombCnt (d_a_bomb3.inc:856,1293; 0x800DB448)
#define DABOMB_REST_TIME(b)                (*(s16*)((u8*)(b) + 0x6FC))  // mRestTime: fuse, 150 at create (0x800DD01C); procExplode_init zeroes it (0x800DB5D0); = DABOMB_OFF_REST_TIME
#define DABOMB_NO_GRAVITY_TIME(b)          (*(s16*)((u8*)(b) + 0x700))  // mNoGravityTime (setNoGravityTime 0x80068228)
// daBomb_c::prm_make (d_a_bomb_static.cpp:95, 0x80068244): 0x80000000 | angXZero << 17 | cheapEff << 16 | state.
// prm_get_state / cheapEff / angXZero / version read only bits 0-7, 16, 17 and 31 (daObj::PrmAbstract), and
// change_state keeps the rest (0x800682C0), so the other bits are free for a marker.
#define DABOMB_PRM_VERSION                 0x80000000
#define DABOMB_PRM_ANGXZERO                0x00020000
#define DABOMB_PRM_STATE_MASK              0x000000FF
#define DABOMB_STATE_EXPLODE               0  // STATE_0: explodes at create (attr A1), not drawn — d_a_canon.cpp:191
#define DABOMB_STATE_LIT                   1  // STATE_1: a lit bomb lying / flying (attr 1C: fuse effect + SE); a released hand bomb
#define DABOMB_STATE_CARRIED               2  // STATE_2: being carried (procCarry_init)
#define DABOMB_STATE_NEW                   3  // STATE_3: fresh from the bomb bag (makeItemType, d_a_player_main.cpp:3693)
#define DABOMB_STATE_CANNON                4  // STATE_4: a cannonball (the boat's: prm_make(STATE_4, FALSE, TRUE), d_a_ship.cpp:4021)

// cCcD_ObjAt SPrm (c_cc_d.h:20-25): the AT groups an attack hits
#define CCD_AT_SPRM_VS_PLAYER              0x04  // cCcD_AtSPrm_VsPlayer_e: Tg IsPlayer = Link, the ship, puppets (d_a_player_main_data.inc:51)

// play.mpPlayerPtr[0]: always Link, even while the player controls Medli / Makar / a seagull / Hyoi, when
// dComIfGp_getPlayer(0) (play.mpPlayer[0], +0x5B44) is that NPC (d_com_inf_game.h:781; procExplode_init
// reads it for decrementBombCnt, 0x800DB454: lwz r4,0x5B4C(gameInfo))
#define GAMEINFO_LINK_ACTOR(gameInfo)      (*(void**)((u8*)(gameInfo) + 0x12A0 + 0x48AC))

// daShip_c (d_a_ship.h, REL d_a_ship; the same offsets the client reads: GameMemoryAddresses.Sea)
#define GAMEINFO_SHIP_ACTOR(gameInfo)      (*(void**)((u8*)(gameInfo) + 0x12A0 + 0x48AC + 0x08))  // play.mpPlayerPtr[2] (d_com_inf_game.h:781)
#define DASHIP_STATE_FLAG(ship)            (*(u32*)((u8*)(ship) + 0x358))  // mStateFlag (d_a_ship.h:313)
#define DASHIP_SFLG_SHOOT_CANNON           0x00020000  // daSFLG_SHOOT_CANNON_e: set the frame the cannon fires (d_a_ship.cpp:4030), cleared at the next execute (:3594)

// daArrow_c (d_a_arrow.h; main.dol 0x800D455C-0x800D8288). fpcNm_ARROW_e = 0x1DE (f_pc_name.h:491).
#define FPC_NAME_ARROW                     0x1DE
#define DAPY_LK_EQUIP_ID(link)             (*(u32*)((u8*)(link) + 0x317C))  // mActorKeepEquip.mID: the nocked arrow (makeArrow, d_a_player_bow.inc:68-78)
#define DAARROW_MODEL(a)                   (*(J3DModel**)((u8*)(a) + 0x294)) // mpModel (procWait 0x800D5A90)
#define DAARROW_CO_SPRM(a)                 (*(u32*)((u8*)(a) + 0x500))       // mCoSph (+0x4D4, createInit 0x800D7400) CO SPrm, +0x2C as the bomb's mSph
#define DAARROW_TYPE(a)                    (*(u8*)((u8*)(a) + 0x601))        // mArrowType: 0 normal, 1 fire, 2 ice, 3 light (setTypeByPlayer 0x800D7308)
#define DAARROW_PROC_FUNC(a)               ((u32*)((u8*)(a) + 0x68C))        // mCurrProcFunc, a 12-byte PTMF {0, -1, fn} (procWait 0x800D5AF0-0x800D5AFC)
#define DAARROW_TYPE_LIGHT                 3
#define DAARROW_SHOT_PARAM                 1  // fopAcM_SetParam(arrow, 1) at the shot (d_a_player_bow.inc:136); 2 stuck, 3 rebound, 4 water
// dSv_player_status_a_c::mMagic (dComIfGs_getMagic): checkRestMp reads it (0x800D72C4: lbz r4,0x14(gameInfo))
#define GAMEINFO_MAGIC(gameInfo)           (*(u8*)((u8*)(gameInfo) + 0x14))
#define CCD_CO_SPRM_SET                    0x01  // cCcD_CoSPrm_Set_e (c_cc_d.h:47)

// dMenu_Fmap_c (d_menu_fmap.h), the sea chart menu: offsets checked in main.dol (setDspNormalMapLink 0x801B3A2C:
// lbz 0x517F, lwz 0x2E04, lfs 0x511C/0x5120; setDspLargeMapLink 0x801B3C4C: lfs 0x30C8/0x30CC, stfs 0x2EF8;
// _draw 0x801B4E14: addi r6,r31,0x1C).
#define FMAP_OFF_DL                        0x1C    // dDlst_FMAP_c fmapDl: what dDlst_FMAP_c::draw's this points at
#define FMAP_LNK1_PANE(menu)               (*(J2DPane**)((u8*)(menu) + 0x2E04))  // mLnk1Pane.pane: Link on the world map
#define FMAP_LNK2_PANE(menu)               (*(J2DPane**)((u8*)(menu) + 0x2EE4))  // mLnk2Pane.pane: Link on a zoomed square
#define FMAP_CLB_SIZE_ORIG_X(menu)         (*(f32*)((u8*)(menu) + 0x30C8))       // mClbPane.mSizeOrig.x
#define FMAP_CLB_SIZE_ORIG_Y(menu)         (*(f32*)((u8*)(menu) + 0x30CC))
#define FMAP_PLAYER_X(menu)                (*(f32*)((u8*)(menu) + 0x511C))       // mPlayerPos.x = Link's current.pos.x
#define FMAP_PLAYER_Z(menu)                (*(f32*)((u8*)(menu) + 0x5120))       // mPlayerPos.y = Link's current.pos.z
#define FMAP_ON_SEA(menu)                  (*(u8*)((u8*)(menu) + 0x517F))        // mFishmanActive: the chart opened on the sea stage
#define FMAP_NORMAL_SCALE                  (56.0f / 100000.0f)                  // setDspNormalMapLink: pane units per world unit
#define FMAP_LARGE_MARGIN                  50.0f                                 // setDspLargeMapLink: (size - 50) / 100000
#define FMAP_SQUARE                        100000.0f

// J2DPane / J2DPicture (J2DPane.h, J2DPicture.h), checked in main.dol: J2DPane::draw 0x802D0078 (lbz 0xAA, lfs 0x0C..0x18),
// makeMatrix 0x802D0714 (0x9C/0xA0 base position, 0xA4 rotation), playerPointGridAnime (stb 0x104 / 0x108).
#define J2DPANE_BOUNDS(p)                  ((f32*)((u8*)(p) + 0x0C))   // TBox2<f32> mBounds: i.x, i.y, f.x, f.y
#define J2DPANE_MTX(p)                     ((u8*)(p) + 0x3C)           // Mtx mMtx (0x30 bytes), rebuilt by calcMtx
#define J2DPANE_ROTATION(p)                (*(f32*)((u8*)(p) + 0xA4))  // mRotation (degrees)
#define J2DPANE_VISIBLE(p)                 (*(u8*)((u8*)(p) + 0xAA))   // mVisible
#define J2DPICTURE_WHITE(p)                ((u8*)(p) + 0x104)          // TColor mColorWhite (r, g, b, a)
#define J2DPICTURE_BLACK(p)                ((u8*)(p) + 0x108)          // TColor mColorBlack

#endif /* WW_INLINES_H */
