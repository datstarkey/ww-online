#include "dolphin/gd/GDBase.h"
#include "dolphin/gd/GDGeometry.h"
#include "dolphin/gf/GF.h"
#include "dolphin/os/OS.h"

void GFSetChanMatColor(GXChannelID chan, GXColor color) {
    u32 packed = (color.r << 24) | (color.g << 16) | (color.b << 8) | color.a;

    GX_WRITE_U8(GX_CMD_LOAD_XF_REG);
    GX_WRITE_U16(0);
    GX_WRITE_U16(0x100C + (chan & 1));
    GX_WRITE_U32(packed);
}
