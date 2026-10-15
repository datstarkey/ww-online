#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/notify.h"
#include "trk.h"

static inline DSError AppendBuffer1_ui8(TRKBuffer* buffer, const u8 data) {
    if (buffer->position >= 0x880) {
        return DS_MessageBufferOverflow;
    }

    buffer->data[buffer->position++] = data;
    buffer->length++;
    return DS_NoError;
}

DSError TRKDoNotifyStopped(MessageCommandID cmd) {
    int reqIdx;
    int bufIdx;
    TRKBuffer* msg;
    DSError err;
    DSError bufError;

    bufError = TRKGetFreeBuffer(&bufIdx, &msg);
    if ((err = bufError) == FALSE) {
        bufError = AppendBuffer1_ui8(msg, cmd);
        err = bufError;
        if (err == DS_NoError) {
            if ((u8)cmd == DSMSG_NotifyStopped) {
                TRKTargetAddStopInfo(msg);
            } else {
                TRKTargetAddExceptionInfo(msg);
            }
        }
        bufError = TRKRequestSend(msg, &reqIdx, 2, 3, 1);
        err = bufError;
        if (err == DS_NoError) {
            TRKReleaseBuffer(reqIdx);
        }
        TRKReleaseBuffer(bufIdx);
    }
    return err;
}
