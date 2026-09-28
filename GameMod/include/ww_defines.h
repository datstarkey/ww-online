
#pragma once

#include "stddef.h"
#include "types.h"

// #include "ww_.h"
// Skip tww_c_structs.h - the extracted structs have too many issues
// #include "tww_c_structs.h"
#include "ww_structs.h"
#include "ww_inlines.h"
#include "ww_functions.h"
#include "ww_variables.h"

// The below definition is for ctors/dtors.
#define SECTION(S) __attribute__((section(S)))

// Common defines
#ifndef FALSE
#define FALSE 0
#endif
#ifndef TRUE
#define TRUE 1
#endif

// J3DFrameCtrl loop modes
#define J3DFrameCtrl__ONCE_e 0
#define J3DFrameCtrl__LOOP_e 2
#define J3DFrameCtrl__NONE_e 0

// TEV (Texture Environment) light types for settingTevStruct
// From tww-decomp/include/d/d_kankyo.h
#define TEV_TYPE_ACTOR       0
#define TEV_TYPE_BG0         1
#define TEV_TYPE_BG1         2
#define TEV_TYPE_BG2         3
#define TEV_TYPE_BG3         4
#define TEV_TYPE_BG0_FULL    5
#define TEV_TYPE_BG1_FULL    6
#define TEV_TYPE_BG2_FULL    7
#define TEV_TYPE_BG3_FULL    8
#define TEV_TYPE_PLAYER      9
#define TEV_TYPE_BG0_PLIGHT  91
#define TEV_TYPE_BG1_PLIGHT  92
#define TEV_TYPE_BG2_PLIGHT  93
#define TEV_TYPE_ACTOR_NOLIGHT 94
#define TEV_TYPE_UNK99       99

// Actor group types (from f_op_actor.h fopAc_Group_e)
#define fopAc_ACTOR_e  0x0
#define fopAc_PLAYER_e 0x1
#define fopAc_ENEMY_e  0x2
#define fopAc_ENV_e    0x3
#define fopAc_NPC_e    0x4
