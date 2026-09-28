
#pragma once

typedef unsigned char u8;
typedef unsigned short u16;
typedef unsigned int u32;

typedef signed char s8;
typedef signed short s16;
typedef signed int s32;
typedef unsigned long long u64;
typedef signed long long s64;
typedef signed char sbyte;

typedef u8 GXBool;

/* GXColor and GXColorS10 are defined in ww_structs.h as _GXColor and _GXColorS10 */
/* These typedefs map the common names to the Ghidra-exported struct names */
/* Note: These are forward declarations - actual structs are in ww_structs.h */

typedef unsigned char bool;
typedef int BOOL;
#define true 1
#define false 0
#define TRUE 1
#define FALSE 0
#define null 0

typedef unsigned char undefined;
typedef unsigned char undefined1;
typedef unsigned short undefined2;
typedef unsigned int undefined4;

typedef unsigned char byte;
typedef unsigned short ushort;
typedef unsigned int uint;
typedef unsigned char uchar;
typedef unsigned int pointer;
typedef unsigned int fpc_ProcID;
