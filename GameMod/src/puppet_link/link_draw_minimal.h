/* Minimal definitions for link_draw_hook.c to compile and link */

#ifndef LINK_DRAW_MINIMAL_H
#define LINK_DRAW_MINIMAL_H

/* Define NULL */
#ifndef NULL
#define NULL ((void*)0)
#endif

/* Basic types */
typedef unsigned char u8;
typedef unsigned short u16;
typedef unsigned int u32;
typedef signed char s8;
typedef signed short s16;
typedef signed int s32;
typedef float f32;

/* 3D Vector types */
typedef struct cXyz {
    f32 x, y, z;
} cXyz;

typedef struct csXyz {
    s16 x, y, z;
} csXyz;

/* Current state for actors */
typedef struct fopAcM_prm_class {
    cXyz mPos;
    csXyz mAngle;
    /* 0x12 */ s8 mGbaName;
    /* 0x13 */ u8 mEnemyNo;
    /* 0x14 */ u8 mParameter;
} fopAcM_prm_class;

/* Base actor class structure (simplified) */
typedef struct fopAc_ac_c {
    /* 0x000 */ u8 base[0x100];  /* base_process_class and other base classes */
    /* 0x100 */ u32 mLinkTagId;
    /* 0x104 */ cXyz mEyePos;
    /* 0x110 */ cXyz mOldPosition;
    /* 0x11C */ cXyz mNextPosition;
    /* 0x128 */ csXyz mCollisionRot;
    /* 0x12E */ u16 mSetID;
    /* 0x130 */ u8 mCollisionRotY;
    /* 0x131 */ u8 mParam;
    /* 0x132 */ s8 mSubtype;
    /* 0x133 */ u8 mCarryType;
    /* 0x134 */ fopAcM_prm_class mOrig;
    /* 0x148 */ fopAcM_prm_class mNext;
    /* 0x15C */ fopAcM_prm_class mCurrent;
    /* More members... */
} fopAc_ac_c;

/* Player actor class (simplified) - inherits from fopAc_ac_c */
typedef struct daPy_lk_c {
    /* fopAc_ac_c members are embedded directly at the start */
    /* 0x000 */ u8 base[0x100];  /* base_process_class and other base classes */
    /* 0x100 */ u32 mLinkTagId;
    /* 0x104 */ cXyz mEyePos;
    /* 0x110 */ cXyz mOldPosition;
    /* 0x11C */ cXyz mNextPosition;
    /* 0x128 */ csXyz mCollisionRot;
    /* 0x12E */ u16 mSetID;
    /* 0x130 */ u8 mCollisionRotY;
    /* 0x131 */ u8 mParam;
    /* 0x132 */ s8 mSubtype;
    /* 0x133 */ u8 mCarryType;
    /* 0x134 */ fopAcM_prm_class mOrig;
    /* 0x148 */ fopAcM_prm_class mNext;
    /* 0x15C */ fopAcM_prm_class mCurrent;
    /* Player-specific members follow */
} daPy_lk_c;

/* Function declarations - these will be linked via ww_linker.ld */
extern int daPy_lk_c__draw(daPy_lk_c* i_this);
extern void os__DCFlushRange(void* addr, u32 size);
extern void os__ICInvalidateRange(void* addr, u32 size);
extern u32 fopAcM_create(s16 procName, u32 params, cXyz* pos, int roomNo,
                         csXyz* angle, cXyz* scale, s8 subtype, void* callback);
extern void* fopAcM_SearchByID(u32 id, void** actor);
extern void fopAcM_delete_1(u32 procId);
extern void* mDoExt_getGameHeap(void);
/* Global player pointer accessor — returns daPy_lk_c* for player 0 (Link), or NULL if not yet constructed */
extern void* dComIfGp_getPlayer(int index);

#endif /* LINK_DRAW_MINIMAL_H */