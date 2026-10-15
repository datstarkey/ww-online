#ifndef OS_DOLPHIN_DOLPHIN_TRK_GLUE_H
#define OS_DOLPHIN_DOLPHIN_TRK_GLUE_H

#include "trk.h"
#include "dolphin/os/OS.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef int (*DBCommFunc)();
typedef int (*DBCommInitFunc)(void*, __OSInterruptHandler);
typedef int (*DBCommReadFunc)(u8*, int);
typedef int (*DBCommWriteFunc)(const u8*, int);

typedef struct DBCommTable {
    /* 0x00 */ DBCommInitFunc initialize_func;
    /* 0x04 */ DBCommFunc init_interrupts_func;
    /* 0x08 */ DBCommFunc peek_func;
    /* 0x0C */ DBCommReadFunc read_func;
    /* 0x10 */ DBCommWriteFunc write_func;
    /* 0x14 */ DBCommFunc open_func;
    /* 0x18 */ DBCommFunc close_func;
} DBCommTable;

void UnreserveEXI2Port();
void ReserveEXI2Port();
UARTError TRKWriteUARTN(const void*, u32);
void TRKLoadContext(OSContext* ctx, u32 r4);
int InitMetroTRKCommTable(int hwId);

#ifdef __cplusplus
}
#endif


#endif /* OS_DOLPHIN_DOLPHIN_TRK_GLUE_H */
