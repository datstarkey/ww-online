#include "dolphin/gd/GDBase.h"
#include "dolphin/gd/GDGeometry.h"
#include "dolphin/gf/GF.h"

void GFLoadPosMtxImm(MtxP mtx, u32 id) {
    GX_WRITE_U8(GX_CMD_LOAD_XF_REG);
    GX_WRITE_U16(11);
    GX_WRITE_U16(id * 4);
    GFWrite_f32(mtx[0][0]);
    GFWrite_f32(mtx[0][1]);
    GFWrite_f32(mtx[0][2]);
    GFWrite_f32(mtx[0][3]);
    GFWrite_f32(mtx[1][0]);
    GFWrite_f32(mtx[1][1]);
    GFWrite_f32(mtx[1][2]);
    GFWrite_f32(mtx[1][3]);
    GFWrite_f32(mtx[2][0]);
    GFWrite_f32(mtx[2][1]);
    GFWrite_f32(mtx[2][2]);
    GFWrite_f32(mtx[2][3]);
}

void GFLoadNrmMtxImm(MtxP mtx, u32 id) {
    GX_WRITE_U8(GX_CMD_LOAD_XF_REG);
    GX_WRITE_U16(8);
    GX_WRITE_U16(id * 3 + 0x400);
    GFWrite_f32(mtx[0][0]);
    GFWrite_f32(mtx[0][1]);
    GFWrite_f32(mtx[0][2]);
    GFWrite_f32(mtx[1][0]);
    GFWrite_f32(mtx[1][1]);
    GFWrite_f32(mtx[1][2]);
    GFWrite_f32(mtx[2][0]);
    GFWrite_f32(mtx[2][1]);
    GFWrite_f32(mtx[2][2]);
}

void GFSetCurrentMtx(u32 pnMtx, u32 tex0, u32 tex1, u32 tex2, u32 tex3, u32 tex4, u32 tex5,
                     u32 tex6, u32 tex7) {
    u32 reg0 = pnMtx | (tex0 << 6) | (tex1 << 12) | (tex2 << 18) | (tex3 << 24);
    u32 reg1 = tex4 | (tex5 << 6) | (tex6 << 12) | (tex7 << 18);

    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(0x30);
    GX_WRITE_U32(reg0);
    GX_WRITE_U8(GX_CMD_LOAD_CP_REG);
    GX_WRITE_U8(0x40);
    GX_WRITE_U32(reg1);

    GX_WRITE_U8(GX_CMD_LOAD_XF_REG);
    GX_WRITE_U16(1);
    GX_WRITE_U16(0x1018);
    GX_WRITE_U32(reg0);
    GX_WRITE_U32(reg1);
}
