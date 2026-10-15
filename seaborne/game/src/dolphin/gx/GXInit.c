#include "dolphin/gx/GX.h"
#include "dolphin/os/OS.h"
#include "dolphin/vi/vi.h"
#include "dolphin/base/PPCArch.h"

char* __GXVersion = "<< Dolphin SDK - GX\trelease build: Sep  5 2002 05:33:28 (0x2301) >>";

static GXData gxData;

static GXFifoObj FifoObj;

GXData* const gx = &gxData;

GXTexRegion* __GXDefaultTexRegionCallback(const GXTexObj* obj, GXTexMapID id) {
    GXTexFmt format;

    format = GXGetTexObjFmt(obj);

    if (format != GX_TF_C4 && format != GX_TF_C8 && format != GX_TF_C14X2) {
        return &gx->TexRegions0[gx->nextTexRgn++ % GX_MAX_TEXMAP];
    } else {
        return &gx->TexRegions1[gx->nextTexRgnCI++ % 4];
    }
}

GXTlutRegion* __GXDefaultTlutRegionCallback(u32 tlut) {
    if (tlut >= 0x14) {
        return NULL;
    } else {
        return &gx->TlutRegions[tlut];
    }
}

vu16* __memReg;

u16* __peReg;

u16* __cpReg;

/* ############################################################################################## */
u32* __piReg;

static GXVtxAttrFmtList GXDefaultVATList[] = {
    {GX_VA_POS, GX_POS_XYZ, GX_F32, 0},
    {GX_VA_NRM, GX_NRM_XYZ, GX_F32, 0},
    {GX_VA_CLR0, GX_CLR_RGBA, GX_RGBA8, 0},
    {GX_VA_CLR1, GX_CLR_RGBA, GX_RGBA8, 0},
    {GX_VA_TEX0, GX_TEX_ST, GX_F32, 0},
    {GX_VA_TEX1, GX_TEX_ST, GX_F32, 0},
    {GX_VA_TEX2, GX_TEX_ST, GX_F32, 0},
    {GX_VA_TEX3, GX_TEX_ST, GX_F32, 0},
    {GX_VA_TEX4, GX_TEX_ST, GX_F32, 0},
    {GX_VA_TEX5, GX_TEX_ST, GX_F32, 0},
    {GX_VA_TEX6, GX_TEX_ST, GX_F32, 0},
    {GX_VA_TEX7, GX_TEX_ST, GX_F32, 0},
    {GX_VA_NULL, GX_COMPCNT_NULL, GX_COMP_NULL, 0},
};

static f32 GXDefaultProjData[] = {1.0f, 0.0f, 1.0f, 0.0f, -1.0f, -2.0f, 0.0f};

static inline void EnableWriteGatherPipe() {
    u32 hid2;  // r31
    hid2 = PPCMfhid2();
    PPCMtwpar(OSUncachedToPhysical((void*)GXFIFO_ADDR));
    hid2 |= 0x40000000;
    PPCMthid2(hid2);
}

// SET_REG_FIELD wrapped in do/while(0), as in the SDK source; the wrapper changes codegen here
#define OLD_SET_REG_FIELD(reg, size, shift, val) \
    do { \
        (reg) = ((u32)(reg) & ~(((1 << (size)) - 1) << (shift))) | ((u32)(val) << (shift)); \
    } while (0)

static inline void __GXInitRevisionBits(void) {
    u32 i;
    u32 j;

    for (i = 0, j = 0; i < 16; i += 2, j++) {
        SET_REG_FIELD(gx->vatA[j], 1, 30, 1);
        SET_REG_FIELD(gx->vatB[j], 1, 31, 1);
        GXFIFO.u8 = 0x08;
        GXFIFO.u8 = 0x80 | j;
        GXFIFO.u32 = gx->vatB[j];
    }

    GXFIFO.u8 = 0x10;
    GXFIFO.u32 = 0x1000;
    GXFIFO.u32 = 0x3F;

    GXFIFO.u8 = 0x10;
    GXFIFO.u32 = 0x1012;
    GXFIFO.u32 = 0x1;

    GX_WRITE_RAS_REG(0x5800000F);
}

#ifdef __MWERKS__
// FAKEMATCH: asm. GC/1.2.5n gives the C version below the target code with r29/r31 swapped
// (the zero/TEV-loop offset register and the GX_TEXMAP_NULL constant).
// clang-format off
asm GXFifoObj* GXInit(void* base, u32 size) {
    nofralloc
    mflr r0
    stw r0, 0x4(r1)
    stwu r1, -0x50(r1)
    stmw r25, 0x34(r1)
    mr r26, r3
    mr r25, r4
    lis r4, gxData@ha
    lwz r3, __GXVersion(r13)
    addi r30, r4, gxData@l
    bl OSRegisterVersion
    lwz r5, gx(r2)
    li r31, 0x0
    li r0, 0x1
    stb r31, 0x4f0(r5)
    li r3, 0x1
    li r4, 0x0
    stb r0, 0x4f1(r5)
    stw r31, 0x4dc(r5)
    stw r31, 0x4e0(r5)
    bl GXSetMisc
    lis r4, 0xcc00
    addi r5, r4, 0x3000
    stw r4, __cpReg(r13)
    addi r3, r4, 0x1000
    addi r0, r4, 0x4000
    stw r5, __piReg(r13)
    stw r3, __peReg(r13)
    stw r0, __memReg(r13)
    bl __GXFifoInit
    mr r4, r26
    mr r5, r25
    addi r3, r30, 0x4f8
    bl GXInitFifoBase
    addi r3, r30, 0x4f8
    bl GXSetCPUFifo
    addi r3, r30, 0x4f8
    bl GXSetGPFifo
    bl __GXPEInit
    bl PPCMfhid2
    lis r4, 0xc01
    mr r26, r3
    addi r3, r4, -0x8000
    bl PPCMtwpar
    oris r3, r26, 0x4000
    bl PPCMthid2
    lwz r6, gx(r2)
    li r0, 0xff
    li r8, 0x0
    stw r31, 0x204(r6)
    addi r4, r6, 0x204
    addi r7, r6, 0x124
    lwz r3, 0x0(r4)
    addi r9, r6, 0x7c
    li r5, 0xc1
    clrlwi r3, r3, 8
    stw r3, 0x0(r4)
    li r4, 0xc0
    stw r0, 0x124(r6)
    lwz r0, 0x0(r7)
    clrlwi r0, r0, 8
    oris r0, r0, 0xf00
    stw r0, 0x0(r7)
    stw r31, 0x7c(r6)
    lwz r0, 0x0(r9)
    clrlwi r0, r0, 8
    oris r0, r0, 0x2200
    stw r0, 0x0(r9)
    b lbl_8031FBF8
lbl_8031FBF8:
    li r0, 0x8
    mtctr r0
    b lbl_8031FC04
lbl_8031FC04:
    b lbl_8031FC08
lbl_8031FC08:
    lwz r3, gx(r2)
    srwi r6, r8, 1
    addi r28, r31, 0x130
    li r0, 0x0
    addi r26, r31, 0x170
    stwx r0, r3, r28
    slwi r10, r6, 2
    addi r27, r10, 0x100
    stwx r0, r3, r26
    addi r9, r31, 0x49c
    stwx r0, r3, r27
    li r29, 0xff
    slwi r12, r4, 24
    stwx r29, r3, r9
    add r25, r3, r28
    addi r31, r31, 0x4
    lwz r28, 0x0(r25)
    add r26, r3, r26
    slwi r11, r5, 24
    clrlwi r28, r28, 8
    or r12, r28, r12
    stw r12, 0x0(r25)
    addi r7, r10, 0x1b0
    addi r10, r6, 0xf6
    lwz r12, 0x0(r26)
    addi r9, r6, 0x28
    addi r28, r31, 0x130
    clrlwi r6, r12, 8
    or r6, r6, r11
    stw r6, 0x0(r26)
    add r7, r3, r7
    addi r8, r8, 0x1
    lwz r11, 0x0(r7)
    srwi r6, r8, 1
    slwi r10, r10, 24
    clrlwi r11, r11, 8
    or r10, r11, r10
    stw r10, 0x0(r7)
    add r25, r3, r27
    slwi r10, r6, 2
    lwz r7, 0x0(r25)
    slwi r9, r9, 24
    addi r26, r31, 0x170
    clrlwi r7, r7, 8
    or r7, r7, r9
    stw r7, 0x0(r25)
    addi r27, r10, 0x100
    addi r9, r31, 0x49c
    stwx r0, r3, r28
    addi r4, r4, 0x2
    slwi r12, r4, 24
    stwx r0, r3, r26
    addi r5, r5, 0x2
    slwi r11, r5, 24
    stwx r0, r3, r27
    addi r7, r10, 0x1b0
    addi r10, r6, 0xf6
    stwx r29, r3, r9
    add r25, r3, r28
    addi r9, r6, 0x28
    lwz r28, 0x0(r25)
    add r26, r3, r26
    add r7, r3, r7
    clrlwi r28, r28, 8
    or r12, r28, r12
    stw r12, 0x0(r25)
    slwi r10, r10, 24
    add r25, r3, r27
    lwz r12, 0x0(r26)
    slwi r9, r9, 24
    addi r31, r31, 0x4
    clrlwi r6, r12, 8
    or r6, r6, r11
    stw r6, 0x0(r26)
    addi r4, r4, 0x2
    addi r5, r5, 0x2
    lwz r11, 0x0(r7)
    addi r8, r8, 0x1
    clrlwi r11, r11, 8
    or r10, r11, r10
    stw r10, 0x0(r7)
    lwz r7, 0x0(r25)
    clrlwi r7, r7, 8
    or r7, r7, r9
    stw r7, 0x0(r25)
    bdnz lbl_8031FC08
    stw r0, 0x120(r3)
    addi r4, r3, 0x120
    mr r9, r0
    lwz r3, 0x0(r4)
    li r10, 0x30
    li r11, 0x31
    clrlwi r3, r3, 8
    oris r3, r3, 0x2700
    stw r3, 0x0(r4)
    b lbl_8031FD88
lbl_8031FD88:
    li r3, 0x2
    mtctr r3
    b lbl_8031FD94
lbl_8031FD94:
    b lbl_8031FD98
lbl_8031FD98:
    lwz r3, gx(r2)
    addi r8, r9, 0xb8
    addi r7, r9, 0xd8
    stwx r0, r3, r8
    slwi r6, r10, 24
    slwi r4, r11, 24
    stwx r0, r3, r7
    add r12, r3, r8
    addi r9, r9, 0x4
    lwz r5, 0x0(r12)
    add r25, r3, r7
    addi r8, r9, 0xb8
    clrlwi r5, r5, 8
    or r5, r5, r6
    stw r5, 0x0(r12)
    addi r7, r9, 0xd8
    addi r10, r10, 0x2
    lwz r5, 0x0(r25)
    slwi r6, r10, 24
    add r12, r3, r8
    clrlwi r5, r5, 8
    or r4, r5, r4
    stw r4, 0x0(r25)
    addi r11, r11, 0x2
    slwi r4, r11, 24
    stwx r0, r3, r8
    addi r9, r9, 0x4
    add r25, r3, r7
    stwx r0, r3, r7
    addi r8, r9, 0xb8
    addi r7, r9, 0xd8
    lwz r5, 0x0(r12)
    addi r10, r10, 0x2
    addi r11, r11, 0x2
    clrlwi r5, r5, 8
    or r5, r5, r6
    stw r5, 0x0(r12)
    slwi r6, r10, 24
    add r12, r3, r8
    lwz r5, 0x0(r25)
    addi r9, r9, 0x4
    addi r10, r10, 0x2
    clrlwi r5, r5, 8
    or r4, r5, r4
    stw r4, 0x0(r25)
    slwi r4, r11, 24
    add r25, r3, r7
    stwx r0, r3, r8
    addi r8, r9, 0xb8
    addi r11, r11, 0x2
    stwx r0, r3, r7
    addi r7, r9, 0xd8
    addi r9, r9, 0x4
    lwz r5, 0x0(r12)
    clrlwi r5, r5, 8
    or r5, r5, r6
    stw r5, 0x0(r12)
    slwi r6, r10, 24
    add r12, r3, r8
    lwz r5, 0x0(r25)
    addi r10, r10, 0x2
    clrlwi r5, r5, 8
    or r4, r5, r4
    stw r4, 0x0(r25)
    slwi r4, r11, 24
    add r25, r3, r7
    stwx r0, r3, r8
    addi r11, r11, 0x2
    stwx r0, r3, r7
    lwz r5, 0x0(r12)
    clrlwi r5, r5, 8
    or r5, r5, r6
    stw r5, 0x0(r12)
    lwz r5, 0x0(r25)
    clrlwi r5, r5, 8
    or r4, r5, r4
    stw r4, 0x0(r25)
    bdnz lbl_8031FD98
    addi r5, r3, 0xf8
    lwz r0, 0x0(r5)
    lis r4, 0x1062
    addi r6, r3, 0xfc
    clrlwi r0, r0, 8
    oris r0, r0, 0x2000
    stw r0, 0x0(r5)
    addi r7, r3, 0x1d0
    addi r8, r3, 0x1d4
    lwz r0, 0x0(r6)
    addi r9, r3, 0x1d8
    addi r10, r3, 0x1dc
    clrlwi r0, r0, 8
    oris r0, r0, 0x2100
    stw r0, 0x0(r6)
    addi r6, r3, 0x1fc
    li r29, 0x0
    lwz r0, 0x0(r7)
    lis r5, 0x8000
    addi r4, r4, 0x4dd3
    clrlwi r0, r0, 8
    oris r0, r0, 0x4100
    stw r0, 0x0(r7)
    lwz r0, 0x0(r8)
    clrlwi r0, r0, 8
    oris r0, r0, 0x4200
    stw r0, 0x0(r8)
    lwz r0, 0x0(r9)
    clrlwi r0, r0, 8
    oris r0, r0, 0x4000
    stw r0, 0x0(r9)
    lwz r0, 0x0(r10)
    clrlwi r0, r0, 8
    oris r0, r0, 0x4300
    stw r0, 0x0(r10)
    lwz r0, 0x0(r6)
    rlwinm r0, r0, 0, 25, 22
    stw r0, 0x0(r6)
    stw r29, 0x4f4(r3)
    stb r29, 0x4f2(r3)
    lwz r0, 0xf8(r5)
    mulhwu r0, r4, r0
    srwi r25, r0, 5
    bl __GXFlushTextureState
    srwi r0, r25, 11
    oris r0, r0, 0x6900
    li r28, 0x61
    lis r31, 0xcc01
    stb r28, -0x8000(r31)
    ori r0, r0, 0x400
    stw r0, -0x8000(r31)
    bl __GXFlushTextureState
    lis r3, 0x3e10
    stb r28, -0x8000(r31)
    subi r0, r3, 0x7c1f
    mulhwu r0, r0, r25
    srwi r0, r0, 10
    oris r0, r0, 0x4600
    ori r0, r0, 0x200
    stw r0, -0x8000(r31)
    mr r7, r29
    b lbl_8031FFC8
lbl_8031FFC8:
    li r0, 0x2
    mtctr r0
    li r5, 0x8
    b lbl_8031FFD8
lbl_8031FFD8:
    b lbl_8031FFDC
lbl_8031FFDC:
    lwz r6, gx(r2)
    addi r8, r7, 0x1c
    addi r9, r7, 0x3c
    add r8, r6, r8
    lwz r0, 0x0(r8)
    ori r4, r29, 0x80
    addi r3, r7, 0x3c
    rlwinm r0, r0, 0, 2, 0
    oris r0, r0, 0x4000
    stw r0, 0x0(r8)
    add r9, r6, r9
    addi r7, r7, 0x4
    lwz r0, 0x0(r9)
    addi r8, r7, 0x1c
    add r8, r6, r8
    clrlwi r0, r0, 1
    oris r0, r0, 0x8000
    stw r0, 0x0(r9)
    addi r9, r7, 0x3c
    add r9, r6, r9
    stb r5, -0x8000(r31)
    addi r29, r29, 0x1
    stb r4, -0x8000(r31)
    ori r4, r29, 0x80
    addi r29, r29, 0x1
    lwzx r0, r6, r3
    addi r3, r7, 0x3c
    addi r7, r7, 0x4
    stw r0, -0x8000(r31)
    lwz r0, 0x0(r8)
    rlwinm r0, r0, 0, 2, 0
    oris r0, r0, 0x4000
    stw r0, 0x0(r8)
    addi r8, r7, 0x1c
    add r8, r6, r8
    lwz r0, 0x0(r9)
    clrlwi r0, r0, 1
    oris r0, r0, 0x8000
    stw r0, 0x0(r9)
    addi r9, r7, 0x3c
    add r9, r6, r9
    stb r5, -0x8000(r31)
    stb r4, -0x8000(r31)
    ori r4, r29, 0x80
    addi r29, r29, 0x1
    lwzx r0, r6, r3
    addi r3, r7, 0x3c
    addi r7, r7, 0x4
    stw r0, -0x8000(r31)
    lwz r0, 0x0(r8)
    rlwinm r0, r0, 0, 2, 0
    oris r0, r0, 0x4000
    stw r0, 0x0(r8)
    addi r8, r7, 0x1c
    add r8, r6, r8
    lwz r0, 0x0(r9)
    clrlwi r0, r0, 1
    oris r0, r0, 0x8000
    stw r0, 0x0(r9)
    addi r9, r7, 0x3c
    add r9, r6, r9
    stb r5, -0x8000(r31)
    stb r4, -0x8000(r31)
    ori r4, r29, 0x80
    addi r29, r29, 0x1
    lwzx r0, r6, r3
    addi r3, r7, 0x3c
    addi r7, r7, 0x4
    stw r0, -0x8000(r31)
    lwz r0, 0x0(r8)
    rlwinm r0, r0, 0, 2, 0
    oris r0, r0, 0x4000
    stw r0, 0x0(r8)
    lwz r0, 0x0(r9)
    clrlwi r0, r0, 1
    oris r0, r0, 0x8000
    stw r0, 0x0(r9)
    stb r5, -0x8000(r31)
    stb r4, -0x8000(r31)
    lwzx r0, r6, r3
    stw r0, -0x8000(r31)
    bdnz lbl_8031FFDC
    li r4, 0x10
    lis r6, 0xcc01
    stb r4, -0x8000(r6)
    li r0, 0x1000
    li r28, 0x0
    stw r0, -0x8000(r6)
    li r0, 0x3f
    slwi r25, r28, 15
    stw r0, -0x8000(r6)
    li r0, 0x1012
    li r5, 0x1
    stb r4, -0x8000(r6)
    li r4, 0x61
    lis r3, 0x5800
    stw r0, -0x8000(r6)
    addi r0, r3, 0xf
    addis r26, r25, 0x8
    stw r5, -0x8000(r6)
    slwi r27, r28, 4
    stb r4, -0x8000(r6)
    stw r0, -0x8000(r6)
    b lbl_8032017C
lbl_8032017C:
    b lbl_80320180
lbl_80320180:
    b lbl_80320184
lbl_80320184:
    lwz r0, gx(r2)
    addi r3, r27, 0x208
    mr r5, r25
    mr r7, r26
    add r3, r0, r3
    li r4, 0x0
    li r6, 0x0
    li r8, 0x0
    bl GXInitTexCacheRegion
    addis r26, r26, 0x1
    addis r25, r25, 0x1
    addi r26, r26, -0x8000
    addi r25, r25, -0x8000
    addi r27, r27, 0x10
    addi r28, r28, 0x1
    cmplwi r28, 0x8
    blt lbl_80320184
    li r25, 0x0
    slwi r3, r25, 1
    addi r26, r3, 0x9
    addi r27, r3, 0x8
    slwi r28, r25, 4
    b lbl_803201E0
lbl_803201E0:
    b lbl_803201E4
lbl_803201E4:
    b lbl_803201E8
lbl_803201E8:
    lwz r0, gx(r2)
    addi r3, r28, 0x288
    slwi r5, r27, 15
    add r3, r0, r3
    slwi r7, r26, 15
    li r4, 0x0
    li r6, 0x0
    li r8, 0x0
    bl GXInitTexCacheRegion
    addi r26, r26, 0x2
    addi r27, r27, 0x2
    addi r28, r28, 0x10
    addi r25, r25, 0x1
    cmplwi r25, 0x4
    blt lbl_803201E8
    li r25, 0x0
    slwi r3, r25, 13
    addis r26, r3, 0xc
    slwi r27, r25, 4
    b lbl_80320238
lbl_80320238:
    b lbl_8032023C
lbl_8032023C:
    b lbl_80320240
lbl_80320240:
    lwz r0, gx(r2)
    addi r3, r27, 0x2d0
    mr r4, r26
    add r3, r0, r3
    li r5, 0x10
    bl GXInitTlutRegion
    addi r26, r26, 0x2000
    addi r27, r27, 0x10
    addi r25, r25, 0x1
    cmplwi r25, 0x10
    blt lbl_80320240
    li r25, 0x0
    slwi r3, r25, 15
    addis r26, r3, 0xe
    b lbl_8032027C
lbl_8032027C:
    b lbl_80320280
lbl_80320280:
    b lbl_80320284
lbl_80320284:
    addi r0, r25, 0x10
    lwz r5, gx(r2)
    slwi r3, r0, 4
    addi r3, r3, 0x2d0
    mr r4, r26
    add r3, r5, r3
    li r5, 0x40
    bl GXInitTlutRegion
    addis r26, r26, 0x1
    addi r26, r26, -0x8000
    addi r25, r25, 0x1
    cmplwi r25, 0x4
    blt lbl_80320284
    lwz r4, __cpReg(r13)
    li r12, 0x0
    li r3, 0x8
    sth r12, 0x6(r4)
    lis r10, 0xcc01
    li r9, 0x20
    lwz r11, gx(r2)
    li r8, 0x10
    li r7, 0x1006
    addi r4, r11, 0x4ec
    lwz r0, 0x0(r4)
    li r6, 0x61
    lis r5, 0x2300
    rlwinm r0, r0, 0, 28, 23
    stw r0, 0x0(r4)
    lis r4, 0x2400
    lis r0, 0x6700
    stb r3, -0x8000(r10)
    li r3, 0x0
    stb r9, -0x8000(r10)
    lwz r9, 0x4ec(r11)
    stw r9, -0x8000(r10)
    stb r8, -0x8000(r10)
    stw r7, -0x8000(r10)
    stw r12, -0x8000(r10)
    stb r6, -0x8000(r10)
    stw r5, -0x8000(r10)
    stb r6, -0x8000(r10)
    stw r4, -0x8000(r10)
    stb r6, -0x8000(r10)
    stw r0, -0x8000(r10)
    bl __GXSetTmemConfig
    bl __GXInitGX
    addi r3, r30, 0x4f8
    lmw r25, 0x34(r1)
    lwz r0, 0x54(r1)
    addi r1, r1, 0x50
    mtlr r0
    blr
}
// clang-format on
#else
GXFifoObj* GXInit(void* base, u32 size) {
    u32 pad[5];
    u32 i;
    u32 reg;
    u32 freqBase;

    OSRegisterVersion(__GXVersion);
    gx->inDispList = GX_FALSE;
    gx->dlSaveContext = GX_TRUE;
    gx->tcsManEnab = 0;
    gx->tevTcEnab = 0;

    GXSetMisc(GX_MT_XF_FLUSH, 0);

    __piReg = (void*)OSPhysicalToUncached(GX_PI_ADDR);
    __cpReg = (void*)OSPhysicalToUncached(GX_CP_ADDR);
    __peReg = (void*)OSPhysicalToUncached(GX_PE_ADDR);
    __memReg = (void*)OSPhysicalToUncached(GX_MEM_ADDR);

    __GXFifoInit();

    GXInitFifoBase(&FifoObj, base, size);
    GXSetCPUFifo(&FifoObj);
    GXSetGPFifo(&FifoObj);

    __GXPEInit();
    EnableWriteGatherPipe();

    gx->genMode = 0;
    OLD_SET_REG_FIELD(gx->genMode, 8, 24, 0);
    gx->bpMask = 255;
    OLD_SET_REG_FIELD(gx->bpMask, 8, 24, 0x0F);
    gx->lpSize = 0;
    OLD_SET_REG_FIELD(gx->lpSize, 8, 24, 0x22);

    for (i = 0; i < GX_MAX_TEVSTAGE; i++) {
        gx->tevc[i] = 0;
        gx->teva[i] = 0;
        gx->tref[i / 2] = 0;
        gx->texmapId[i] = GX_TEXMAP_NULL;

        OLD_SET_REG_FIELD(gx->tevc[i], 8, 24, 0xC0 + i * 2);
        OLD_SET_REG_FIELD(gx->teva[i], 8, 24, 0xC1 + i * 2);
        OLD_SET_REG_FIELD(gx->tevKsel[i / 2], 8, 24, 0xF6 + i / 2);
        OLD_SET_REG_FIELD(gx->tref[i / 2], 8, 24, 0x28 + i / 2);
    }

    gx->iref = 0;
    OLD_SET_REG_FIELD(gx->iref, 8, 24, 0x27);

    for (i = 0; i < GX_MAXCOORD; i++) {
        gx->suTs0[i] = 0;
        gx->suTs1[i] = 0;

        OLD_SET_REG_FIELD(gx->suTs0[i], 8, 24, 0x30 + i * 2);
        OLD_SET_REG_FIELD(gx->suTs1[i], 8, 24, 0x31 + i * 2);
    }

    OLD_SET_REG_FIELD(gx->suScis0, 8, 24, 0x20);
    OLD_SET_REG_FIELD(gx->suScis1, 8, 24, 0x21);

    OLD_SET_REG_FIELD(gx->cmode0, 8, 24, 0x41);
    OLD_SET_REG_FIELD(gx->cmode1, 8, 24, 0x42);

    OLD_SET_REG_FIELD(gx->zmode, 8, 24, 0x40);
    OLD_SET_REG_FIELD(gx->peCtrl, 8, 24, 0x43);

    OLD_SET_REG_FIELD(gx->cpTex, 2, 7, 0);

    gx->dirtyState = 0;
    gx->dirtyVAT = 0;

    freqBase = OS_BUS_CLOCK / 500;

    __GXFlushTextureState();
    reg = (freqBase / 2048) | 0x400 | 0x69000000;
    GX_WRITE_RAS_REG(reg);

    __GXFlushTextureState();
    reg = (freqBase / 4224) | 0x200 | 0x46000000;
    GX_WRITE_RAS_REG(reg);

    __GXInitRevisionBits();

    for (i = 0; i < 8; i++) {
        GXInitTexCacheRegion(&gx->TexRegions0[i], 0, i * 0x8000, 0, 0x80000 + i * 0x8000, 0);
    }

    for (i = 0; i < 4; i++) {
        GXInitTexCacheRegion(&gx->TexRegions1[i], 0, (i * 2 + 8) * 0x8000, 0, (i * 2 + 9) * 0x8000, 0);
    }

    for (i = 0; i < 16; i++) {
        GXInitTlutRegion(&gx->TlutRegions[i], 0xC0000 + 0x2000 * i, GX_TLUT_256);
    }

    for (i = 0; i < 4; i++) {
        GXInitTlutRegion(&gx->TlutRegions[i + 16], 0xE0000 + 0x8000 * i, GX_TLUT_1K);
    }

    {
        u32 reg = 0;

        GX_SET_CP_REG(3, reg);

        OLD_SET_REG_FIELD(gx->perfSel, 4, 4, 0);
        GX_WRITE_U8(0x8);
        GX_WRITE_U8(0x20);
        GX_WRITE_U32(gx->perfSel);

        reg = 0;
        GX_WRITE_XF_REG(6, reg);

        reg = 0x23000000;
        GX_WRITE_RAS_REG(reg);

        reg = 0x24000000;
        GX_WRITE_RAS_REG(reg);

        reg = 0x67000000;
        GX_WRITE_RAS_REG(reg);
    }

    __GXSetTmemConfig(0);

    __GXInitGX();

    return &FifoObj;
}
#endif

void __GXInitGX(void) {
    GXRenderModeObj* renderObj;
    Mtx ident;
    GXColor clearColor = {64, 64, 64, 255};
    GXColor ambColor = {0, 0, 0, 0};
    GXColor matColor = {255, 255, 255, 255};
    u32 i;

    switch (VIGetTvFormat()) {
    case VI_NTSC:
        renderObj = &GXNtsc480IntDf;
        break;

    case VI_PAL:
        renderObj = &GXPal528IntDf;
        break;

    case VI_EURGB60:
        renderObj = &GXEurgb60Hz480IntDf;
        break;

    case VI_MPAL:
        renderObj = &GXMpal480IntDf;
        break;

    default:
        renderObj = &GXNtsc480IntDf;
        break;
    }

    GXSetCopyClear(clearColor, 0xFFFFFF);

    GXSetTexCoordGen(GX_TEXCOORD0, GX_TG_MTX2x4, GX_TG_TEX0, GX_IDENTITY);
    GXSetTexCoordGen(GX_TEXCOORD1, GX_TG_MTX2x4, GX_TG_TEX1, GX_IDENTITY);
    GXSetTexCoordGen(GX_TEXCOORD2, GX_TG_MTX2x4, GX_TG_TEX2, GX_IDENTITY);
    GXSetTexCoordGen(GX_TEXCOORD3, GX_TG_MTX2x4, GX_TG_TEX3, GX_IDENTITY);
    GXSetTexCoordGen(GX_TEXCOORD4, GX_TG_MTX2x4, GX_TG_TEX4, GX_IDENTITY);
    GXSetTexCoordGen(GX_TEXCOORD5, GX_TG_MTX2x4, GX_TG_TEX5, GX_IDENTITY);
    GXSetTexCoordGen(GX_TEXCOORD6, GX_TG_MTX2x4, GX_TG_TEX6, GX_IDENTITY);
    GXSetTexCoordGen(GX_TEXCOORD7, GX_TG_MTX2x4, GX_TG_TEX7, GX_IDENTITY);

    GXSetNumTexGens(1);
    GXClearVtxDesc();
    GXInvalidateVtxCache();

    for (i = GX_VA_POS; i <= GX_LIGHT_ARRAY; i++) {
        GXSetArray((GXAttr)i, gx, 0);
    }

    for (i = 0; i < GX_MAX_VTXFMT; i++) {
        GXSetVtxAttrFmtv((GXVtxFmt)i, GXDefaultVATList);
    }

    GXSetLineWidth(6, GX_TO_ZERO);
    GXSetPointSize(6, GX_TO_ZERO);
    GXEnableTexOffsets(GX_TEXCOORD0, GX_FALSE, GX_FALSE);
    GXEnableTexOffsets(GX_TEXCOORD1, GX_FALSE, GX_FALSE);
    GXEnableTexOffsets(GX_TEXCOORD2, GX_FALSE, GX_FALSE);
    GXEnableTexOffsets(GX_TEXCOORD3, GX_FALSE, GX_FALSE);
    GXEnableTexOffsets(GX_TEXCOORD4, GX_FALSE, GX_FALSE);
    GXEnableTexOffsets(GX_TEXCOORD5, GX_FALSE, GX_FALSE);
    GXEnableTexOffsets(GX_TEXCOORD6, GX_FALSE, GX_FALSE);
    GXEnableTexOffsets(GX_TEXCOORD7, GX_FALSE, GX_FALSE);

    ident[0][0] = 1.0f;
    ident[0][1] = 0.0f;
    ident[0][2] = 0.0f;
    ident[0][3] = 0.0f;

    ident[1][0] = 0.0f;
    ident[1][1] = 1.0f;
    ident[1][2] = 0.0f;
    ident[1][3] = 0.0f;

    ident[2][0] = 0.0f;
    ident[2][1] = 0.0f;
    ident[2][2] = 1.0f;
    ident[2][3] = 0.0f;

    GXLoadPosMtxImm(ident, GX_PNMTX0);
    GXLoadNrmMtxImm(ident, GX_PNMTX0);
    GXSetCurrentMtx(GX_PNMTX0);

    GXLoadTexMtxImm(ident, GX_IDENTITY, GX_MTX3x4);
    GXLoadTexMtxImm(ident, GX_PTIDENTITY, GX_MTX3x4);

    GXSetViewport(0.0f, 0.0f, renderObj->fbWidth, renderObj->xfbHeight, 0.0f, 1.0f);

    GXSetProjectionv(GXDefaultProjData);

    GXSetCoPlanar(GX_FALSE);
    GXSetCullMode(GX_CULL_BACK);
    GXSetClipMode(GX_CLIP_ENABLE);

    GXSetScissor(0, 0, renderObj->fbWidth, renderObj->efbHeight);
    GXSetScissorBoxOffset(0, 0);

    GXSetNumChans(0);
    GXSetChanCtrl(GX_COLOR0A0, GX_FALSE, GX_SRC_REG, GX_SRC_VTX, 0, GX_DF_NONE, GX_AF_NONE);
    GXSetChanAmbColor(GX_COLOR0A0, ambColor);
    GXSetChanMatColor(GX_COLOR0A0, matColor);

    GXSetChanCtrl(GX_COLOR1A1, GX_FALSE, GX_SRC_REG, GX_SRC_VTX, 0, GX_DF_NONE, GX_AF_NONE);
    GXSetChanAmbColor(GX_COLOR1A1, ambColor);
    GXSetChanMatColor(GX_COLOR1A1, matColor);

    GXInvalidateTexAll();
    gx->nextTexRgn = 0;
    gx->nextTexRgnCI = 0;
    GXSetTexRegionCallback(__GXDefaultTexRegionCallback);
    GXSetTlutRegionCallback(__GXDefaultTlutRegionCallback);

    GXSetTevOrder(GX_TEVSTAGE0, GX_TEXCOORD0, GX_TEXMAP0, GX_COLOR0A0);
    GXSetTevOrder(GX_TEVSTAGE1, GX_TEXCOORD1, GX_TEXMAP1, GX_COLOR0A0);
    GXSetTevOrder(GX_TEVSTAGE2, GX_TEXCOORD2, GX_TEXMAP2, GX_COLOR0A0);
    GXSetTevOrder(GX_TEVSTAGE3, GX_TEXCOORD3, GX_TEXMAP3, GX_COLOR0A0);
    GXSetTevOrder(GX_TEVSTAGE4, GX_TEXCOORD4, GX_TEXMAP4, GX_COLOR0A0);
    GXSetTevOrder(GX_TEVSTAGE5, GX_TEXCOORD5, GX_TEXMAP5, GX_COLOR0A0);
    GXSetTevOrder(GX_TEVSTAGE6, GX_TEXCOORD6, GX_TEXMAP6, GX_COLOR0A0);
    GXSetTevOrder(GX_TEVSTAGE7, GX_TEXCOORD7, GX_TEXMAP7, GX_COLOR0A0);

    GXSetTevOrder(GX_TEVSTAGE8, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);
    GXSetTevOrder(GX_TEVSTAGE9, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);
    GXSetTevOrder(GX_TEVSTAGE10, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);
    GXSetTevOrder(GX_TEVSTAGE11, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);
    GXSetTevOrder(GX_TEVSTAGE12, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);
    GXSetTevOrder(GX_TEVSTAGE13, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);
    GXSetTevOrder(GX_TEVSTAGE14, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);
    GXSetTevOrder(GX_TEVSTAGE15, GX_TEXCOORD_NULL, GX_TEXMAP_NULL, GX_COLOR_NULL);

    GXSetNumTevStages(1);
    GXSetTevOp(GX_TEVSTAGE0, GX_REPLACE);

    GXSetAlphaCompare(GX_ALWAYS, 0, GX_AOP_AND, GX_ALWAYS, 0);

    GXSetZTexture(GX_ZT_DISABLE, GX_TF_Z8, 0);

    for (i = 0; i < GX_MAX_TEVSTAGE; i++) {
        GXSetTevKColorSel((GXTevStageID)i, GX_TEV_KCSEL_1_4);
        GXSetTevKAlphaSel((GXTevStageID)i, GX_TEV_KASEL_1);
        GXSetTevSwapMode((GXTevStageID)i, GX_TEV_SWAP0, GX_TEV_SWAP0);
    }

    GXSetTevSwapModeTable(GX_TEV_SWAP0, GX_CH_RED, GX_CH_GREEN, GX_CH_BLUE, GX_CH_ALPHA);
    GXSetTevSwapModeTable(GX_TEV_SWAP1, GX_CH_RED, GX_CH_RED, GX_CH_RED, GX_CH_ALPHA);
    GXSetTevSwapModeTable(GX_TEV_SWAP2, GX_CH_GREEN, GX_CH_GREEN, GX_CH_GREEN, GX_CH_ALPHA);
    GXSetTevSwapModeTable(GX_TEV_SWAP3, GX_CH_BLUE, GX_CH_BLUE, GX_CH_BLUE, GX_CH_ALPHA);

    for (i = 0; i < GX_MAX_TEVSTAGE; i++) {
        GXSetTevDirect((GXTevStageID)i);
    }

    GXSetNumIndStages(0);
    GXSetIndTexCoordScale(GX_INDTEXSTAGE0, GX_ITS_1, GX_ITS_1);
    GXSetIndTexCoordScale(GX_INDTEXSTAGE1, GX_ITS_1, GX_ITS_1);
    GXSetIndTexCoordScale(GX_INDTEXSTAGE2, GX_ITS_1, GX_ITS_1);
    GXSetIndTexCoordScale(GX_INDTEXSTAGE3, GX_ITS_1, GX_ITS_1);

    GXSetFog(GX_FOG_NONE, 0.0f, 1.0f, 0.1f, 1.0f, ambColor);
    GXSetFogRangeAdj(GX_FALSE, 0, NULL);

    GXSetBlendMode(GX_BM_NONE, GX_BL_SRC_ALPHA, GX_BL_INV_SRC_ALPHA, GX_LO_CLEAR);

    GXSetColorUpdate(GX_TRUE);
    GXSetAlphaUpdate(GX_TRUE);

    GXSetZMode(GX_TRUE, GX_LEQUAL, GX_TRUE);
    GXSetZCompLoc(GX_TRUE);

    GXSetDither(GX_TRUE);

    GXSetDstAlpha(GX_FALSE, 0);
    GXSetPixelFmt(GX_PF_RGB8_Z24, GX_ZC_LINEAR);

    GXSetFieldMask(GX_TRUE, GX_TRUE);
    GXSetFieldMode(
        (GXBool)renderObj->field_rendering,
        (GXBool)((renderObj->viHeight == 2 * renderObj->xfbHeight) ? GX_TRUE : GX_FALSE));

    GXSetDispCopySrc(0, 0, renderObj->fbWidth, renderObj->efbHeight);
    GXSetDispCopyDst(renderObj->fbWidth, renderObj->efbHeight);
    GXSetDispCopyYScale((f32)renderObj->xfbHeight / (f32)renderObj->efbHeight);
    GXSetCopyClamp(GX_CLAMP_BOTH);

    GXSetCopyFilter(renderObj->antialiasing, renderObj->sample_pattern, GX_TRUE,
                    renderObj->vfilter);
    GXSetDispCopyGamma(GX_GM_1_0);
    GXSetDispCopyFrame2Field(GX_COPY_PROGRESSIVE);
    GXClearBoundingBox();

    GXPokeColorUpdate(GX_TRUE);
    GXPokeAlphaUpdate(GX_TRUE);
    GXPokeDither(GX_FALSE);
    GXPokeBlendMode(GX_BM_NONE, GX_BL_ZERO, GX_BL_ONE, GX_LO_SET);
    GXPokeAlphaMode(GX_ALWAYS, 0);
    GXPokeAlphaRead(GX_READ_FF);
    GXPokeDstAlpha(GX_FALSE, 0);
    GXPokeZMode(GX_TRUE, GX_ALWAYS, GX_TRUE);
    GXSetGPMetric(GX_PERF0_NONE, GX_PERF1_NONE);
    GXClearGPMetric();
}
