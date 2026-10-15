#include "dolphin/gd/GDBase.h"
#include "dolphin/gd/GDGeometry.h"
#include "dolphin/gf/GF.h"
#include "dolphin/os/OS.h"

void GFSetVtxDescv(GXVtxDescList* attrPtr) {
    u32 nnorms = 0;
    u32 ncols = 0;
    u32 ntexs = 0;
    u32 pnMtxIdx = GX_NONE;
    u32 txMtxIdxMask = 0;
    u32 posn = GX_DIRECT;
    u32 norm = GX_NONE;
    u32 col0 = GX_NONE;
    u32 col1 = GX_NONE;
    u32 tex0 = GX_NONE;
    u32 tex1 = GX_NONE;
    u32 tex2 = GX_NONE;
    u32 tex3 = GX_NONE;
    u32 tex4 = GX_NONE;
    u32 tex5 = GX_NONE;
    u32 tex6 = GX_NONE;
    u32 tex7 = GX_NONE;

    for (; attrPtr->attr != GX_VA_NULL; ++attrPtr) {

        switch (attrPtr->attr) {
        case GX_VA_PNMTXIDX:
            pnMtxIdx = attrPtr->type;
            break;
        case GX_VA_TEX0MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x1) | (attrPtr->type << 0);
            break;
        case GX_VA_TEX1MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x2) | (attrPtr->type << 1);
            break;
        case GX_VA_TEX2MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x4) | (attrPtr->type << 2);
            break;
        case GX_VA_TEX3MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x8) | (attrPtr->type << 3);
            break;
        case GX_VA_TEX4MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x10) | (attrPtr->type << 4);
            break;
        case GX_VA_TEX5MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x20) | (attrPtr->type << 5);
            break;
        case GX_VA_TEX6MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x40) | (attrPtr->type << 6);
            break;
        case GX_VA_TEX7MTXIDX:
            txMtxIdxMask = (txMtxIdxMask & ~0x80) | (attrPtr->type << 7);
            break;
        case GX_VA_POS:
            posn = attrPtr->type;
            break;
        case GX_VA_NRM:
            if (attrPtr->type != GX_NONE) {
                norm = attrPtr->type;
                nnorms = 1;
            }
            break;
        case GX_VA_NBT:
            if (attrPtr->type != GX_NONE) {
                norm = attrPtr->type;
                nnorms = 2;
            }
            break;
        case GX_VA_CLR0:
            col0 = attrPtr->type;
            ncols += (col0 != GX_NONE);
            break;
        case GX_VA_CLR1:
            col1 = attrPtr->type;
            ncols += (col1 != GX_NONE);
            break;
        case GX_VA_TEX0:
            tex0 = attrPtr->type;
            ntexs += (tex0 != GX_NONE);
            break;
        case GX_VA_TEX1:
            tex1 = attrPtr->type;
            ntexs += (tex1 != GX_NONE);
            break;
        case GX_VA_TEX2:
            tex2 = attrPtr->type;
            ntexs += (tex2 != GX_NONE);
            break;
        case GX_VA_TEX3:
            tex3 = attrPtr->type;
            ntexs += (tex3 != GX_NONE);
            break;
        case GX_VA_TEX4:
            tex4 = attrPtr->type;
            ntexs += (tex4 != GX_NONE);
            break;
        case GX_VA_TEX5:
            tex5 = attrPtr->type;
            ntexs += (tex5 != GX_NONE);
            break;
        case GX_VA_TEX6:
            tex6 = attrPtr->type;
            ntexs += (tex6 != GX_NONE);
            break;
        case GX_VA_TEX7:
            tex7 = attrPtr->type;
            ntexs += (tex7 != GX_NONE);
            break;
        }
    }

    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(CP_REG_VCD_LO_ID);
    GX_WRITE_U32(CP_REG_VCD_LO(pnMtxIdx, txMtxIdxMask, posn, norm, col0, col1));
    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(CP_REG_VCD_HI_ID);
    GX_WRITE_U32(CP_REG_VCD_HI(tex0, tex1, tex2, tex3, tex4, tex5, tex6, tex7));
    GX_WRITE_U8(GX_CMD_LOAD_XF_REG);
    GX_WRITE_U16(0);
    GX_WRITE_U16(XF_REG_INVERTEXSPEC_ID);
    GX_WRITE_U32(XF_REG_INVTXSPEC(ncols, nnorms, ntexs));
}

void GFSetVtxAttrFmtv(GXVtxFmt format, GXVtxAttrFmtList* attrPtr) {
    GXCompCnt posCompCnt = GX_POS_XYZ;
    GXCompType posCompType = GX_F32;
    u32 posCompShift = 0;

    GXCompCnt nrmCompCnt = GX_NRM_XYZ;
    GXCompType nrmCompType = GX_F32;
    bool nbt3 = false;

    GXCompCnt clr0CompCnt = GX_CLR_RGBA;
    GXCompType clr0CompType = GX_RGBA8;
    GXCompCnt clr1CompCnt = GX_CLR_RGBA;
    GXCompType clr1CompType = GX_RGBA8;

    GXCompCnt tex0CompCnt = GX_TEX_ST;
    GXCompType tex0CompType = GX_F32;
    u32 tex0CompShift = 0;
    GXCompCnt tex1CompCnt = GX_TEX_ST;
    GXCompType tex1CompType = GX_F32;
    u32 tex1CompShift = 0;
    GXCompCnt tex2CompCnt = GX_TEX_ST;
    GXCompType tex2CompType = GX_F32;
    u32 tex2CompShift = 0;
    GXCompCnt tex3CompCnt = GX_TEX_ST;
    GXCompType tex3CompType = GX_F32;
    u32 tex3CompShift = 0;
    GXCompCnt tex4CompCnt = GX_TEX_ST;
    GXCompType tex4CompType = GX_F32;
    u32 tex4CompShift = 0;
    GXCompCnt tex5CompCnt = GX_TEX_ST;
    GXCompType tex5CompType = GX_F32;
    u32 tex5CompShift = 0;
    GXCompCnt tex6CompCnt = GX_TEX_ST;
    GXCompType tex6CompType = GX_F32;
    u32 tex6CompShift = 0;
    GXCompCnt tex7CompCnt = GX_TEX_ST;
    GXCompType tex7CompType = GX_F32;
    u32 tex7CompShift = 0;

    for (; attrPtr->attr != GX_VA_NULL; ++attrPtr) {
        switch (attrPtr->attr) {
        case GX_VA_POS:
            posCompCnt = attrPtr->cnt;
            posCompType = attrPtr->type;
            posCompShift = attrPtr->frac;
            break;
        case GX_VA_NRM:
        case GX_VA_NBT:
            nrmCompType = attrPtr->type;
            if (attrPtr->cnt == GX_NRM_NBT3) {
                nrmCompCnt = GX_NRM_NBT;
                nbt3 = true;
            } else {
                nrmCompCnt = attrPtr->cnt;
                nbt3 = false;
            }
            break;
        case GX_VA_CLR0:
            clr0CompCnt = attrPtr->cnt;
            clr0CompType = attrPtr->type;
            break;
        case GX_VA_CLR1:
            clr1CompCnt = attrPtr->cnt;
            clr1CompType = attrPtr->type;
            break;
        case GX_VA_TEX0:
            tex0CompCnt = attrPtr->cnt;
            tex0CompType = attrPtr->type;
            tex0CompShift = attrPtr->frac;
            break;
        case GX_VA_TEX1:
            tex1CompCnt = attrPtr->cnt;
            tex1CompType = attrPtr->type;
            tex1CompShift = attrPtr->frac;
            break;
        case GX_VA_TEX2:
            tex2CompCnt = attrPtr->cnt;
            tex2CompType = attrPtr->type;
            tex2CompShift = attrPtr->frac;
            break;
        case GX_VA_TEX3:
            tex3CompCnt = attrPtr->cnt;
            tex3CompType = attrPtr->type;
            tex3CompShift = attrPtr->frac;
            break;
        case GX_VA_TEX4:
            tex4CompCnt = attrPtr->cnt;
            tex4CompType = attrPtr->type;
            tex4CompShift = attrPtr->frac;
            break;
        case GX_VA_TEX5:
            tex5CompCnt = attrPtr->cnt;
            tex5CompType = attrPtr->type;
            tex5CompShift = attrPtr->frac;
            break;
        case GX_VA_TEX6:
            tex6CompCnt = attrPtr->cnt;
            tex6CompType = attrPtr->type;
            tex6CompShift = attrPtr->frac;
            break;
        case GX_VA_TEX7:
            tex7CompCnt = attrPtr->cnt;
            tex7CompType = attrPtr->type;
            tex7CompShift = attrPtr->frac;
            break;
        }
    }

    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(CP_REG_VAT_GRP0_ID + format);
    GX_WRITE_U32(CP_REG_VAT_GRP0(posCompCnt, posCompType, posCompShift, nrmCompCnt, nrmCompType,
                                 clr0CompCnt, clr0CompType, clr1CompCnt, clr1CompType,
                                 tex0CompCnt, tex0CompType, tex0CompShift, 1, nbt3));
    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(CP_REG_VAT_GRP1_ID + format);
    GX_WRITE_U32(CP_REG_VAT_GRP1(tex1CompCnt, tex1CompType, tex1CompShift, tex2CompCnt,
                                 tex2CompType, tex2CompShift, tex3CompCnt, tex3CompType,
                                 tex3CompShift, tex4CompCnt, tex4CompType, 1));
    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(CP_REG_VAT_GRP2_ID + format);
    GX_WRITE_U32(CP_REG_VAT_GRP2(tex4CompShift, tex5CompCnt, tex5CompType, tex5CompShift,
                                 tex6CompCnt, tex6CompType, tex6CompShift, tex7CompCnt,
                                 tex7CompType, tex7CompShift));
}

#if TARGET_PC
// Aurora has no CP_REG_ARRAYBASE: it logs "not supported" and ignores the register, because a
// 32-bit physical address cannot hold a host pointer. Its replacement, GX_AURORA_LOAD_ARRAYBASE,
// carries the 64-bit pointer, the array's size in bytes (Aurora uploads that many bytes for indexed
// draws) and its byte order, as Aurora's GDSetArraySized writes it. GFSetArray has no size, so the
// game's callers use GFSetArraySized (step 2.7), and GFSetArray itself stops loudly, as Aurora's
// GDSetArray does.
void GFSetArraySized(GXAttr attr, void* base_ptr, u32 size, u8 stride, bool le) {
    s32 cpAttr;
    if (attr == GX_VA_NBT) {
        cpAttr = GX_VA_TEX0MTXIDX;
    } else {
        cpAttr = attr - GX_VA_POS;
    }

    const u64 addr = (u64)(uintptr_t)base_ptr;
    GFWrite_u8(GX_AURORA);
    GFWrite_u16(GX_AURORA_LOAD_ARRAYBASE + cpAttr);
    GFWrite_u32((u32)(addr >> 32));
    GFWrite_u32((u32)addr);
    GFWrite_u32(size);
    GFWrite_u8(le ? 1 : 0);
    GFWriteCPCmd(cpAttr + CP_REG_ARRAYSTRIDE_ID, stride);
}

void GFSetArray(GXAttr attr, void* base_ptr, u8 stride) {
    OSPanic(__FILE__, __LINE__,
            "GFSetArray(attr %d, %p, stride %u) has no array size on Aurora: use GFSetArraySized",
            (int)attr, base_ptr, (unsigned)stride);
}
#else
void GFSetArray(GXAttr attr, void* base_ptr, u8 stride) {
    u32 idx;

    if (attr == GX_VA_NBT) {
        idx = 1;
    } else {
        idx = attr - GX_VA_POS;
    }

    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(idx + 0xA0);
    GX_WRITE_U32((u32)OSCachedToPhysical(base_ptr));
    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(idx + 0xB0);
    GX_WRITE_U32(stride);
}
#endif

void GFSetCullMode(GXCullMode mode) {
    static u8 cm2hw[] = { 0, 2, 1, 3 };

    GFWriteBPCmd(0xFE00C000);
    GFWriteBPCmd(cm2hw[mode] << 14);
}
