/**
 * msg.c
 * Description:
 */

#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/msg.h"
#include "TRK_MINNOW_DOLPHIN/Os/dolphin/dolphin_trk_glue.h"
#include "trk.h"

UARTError WriteUART1(s8 arg0);

DSError TRKMessageSend(TRK_Msg* msg) {
    u8 checksum;
    u8 b;
    u8 b2;
    DSError err;
    int i;

    checksum = 0;
    for (i = 0; i < msg->m_msgLength; i++) {
        checksum = (u8)(checksum + msg->m_msg[i]);
    }
    checksum = (u8)(checksum ^ 0xFF);

    err = WriteUART1(0x7E);
    if (err == 0) {
        for (i = 0; i < msg->m_msgLength; i++) {
            b = msg->m_msg[i];
            if (b == 0x7E || b == 0x7D) {
                err = WriteUART1(0x7D);
                b ^= 0x20;
                if (err != 0) {
                    break;
                }
            }
            err = WriteUART1(b);
            if (err != 0) {
                break;
            }
        }
    }
    if (err == 0) {
        b2 = checksum;
        for (i = 0; i < 1; i++) {
            if (b2 == 0x7E || b2 == 0x7D) {
                err = WriteUART1(0x7D);
                b2 ^= 0x20;
                if (err != 0) {
                    break;
                }
            }
            err = WriteUART1(b2);
            if (err != 0) {
                break;
            }
        }
    }
    if (err == 0) {
        err = WriteUART1(0x7E);
    }
    if (err == 0) {
        err = WriteUARTFlush();
    }
    return err;
}
