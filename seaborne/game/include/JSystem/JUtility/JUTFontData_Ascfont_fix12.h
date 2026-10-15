#ifndef JUTFONTDATA_ASCFONT_FIX12_H
#define JUTFONTDATA_ASCFONT_FIX12_H

#include "global.h"

#if TARGET_PC
// filled from the disc at start-up in a COS_RUNTIME_ASSETS build (native/tools/extract_data_assets.py)
extern u8 JUTResFONT_Ascfont_fix12[];
#else
ALIGN_DECL(32, extern const u8 JUTResFONT_Ascfont_fix12[]);
#endif

#endif /* JUTFONTDATA_ASCFONT_FIX12_H */
