#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/serpoll.h"
#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/nubevent.h"
#include "trk.h"

static TRKFramingState gTRKFramingState;

void* gTRKInputPendingPtr;

extern int TRKPollUART(void);

static inline DSError AppendBuffer1_ui8(TRKBuffer* buffer, const u8 data) {
    if (buffer->position >= 0x880) {
        return DS_MessageBufferOverflow;
    }

    buffer->data[buffer->position++] = data;
    buffer->length++;
    return DS_NoError;
}

static inline void ReleaseFrameBuffer(void) {
    if (gTRKFramingState.msgBufID != -1) {
        TRKReleaseBuffer(gTRKFramingState.msgBufID);
        gTRKFramingState.msgBufID = -1;
    }
}

static inline BOOL CheckFrameSize(TRKBuffer* buf) {
    if (buf->length < 2) {
        TRKStandardACK(buf, 0xff, DSREPLY_PacketSizeError);
        ReleaseFrameBuffer();
        gTRKFramingState.buffer = NULL;
        gTRKFramingState.receiveState = DSRECV_Wait;
        return FALSE;
    }

    buf->position = 0;
    buf->length--;
    return TRUE;
}

MessageBufferID TRKTestForPacket(void) {
    u8 ch;
    DSError err = DS_NoError;
    UARTError uartErr = TRKReadUARTPoll(&ch);

    while (uartErr == UART_NoError && err == DS_NoError) {
        if (gTRKFramingState.receiveState != DSRECV_InFrame) {
            gTRKFramingState.isEscape = FALSE;
        }

        switch (gTRKFramingState.receiveState) {
        case DSRECV_Wait:
            if ((int)ch == 0x7e) {
                err = TRKGetFreeBuffer(&gTRKFramingState.msgBufID, &gTRKFramingState.buffer);
                gTRKFramingState.fcsType = 0;
                gTRKFramingState.receiveState = DSRECV_Found;
            }
            break;

        case DSRECV_Found:
            if ((int)ch == 0x7e) {
                break;
            }
            gTRKFramingState.receiveState = DSRECV_InFrame;
            /* fallthrough */

        case DSRECV_InFrame:
            if ((int)(s8)ch == 0x7e) {
                if (gTRKFramingState.isEscape) {
                    TRKStandardACK(gTRKFramingState.buffer, 0xff, DSREPLY_EscapeError);
                    ReleaseFrameBuffer();
                    gTRKFramingState.buffer = NULL;
                    gTRKFramingState.receiveState = DSRECV_Wait;
                } else {
                    BOOL complete = CheckFrameSize(gTRKFramingState.buffer);

                    if (complete) {
                        MessageBufferID id = gTRKFramingState.msgBufID;

                        gTRKFramingState.msgBufID = -1;
                        gTRKFramingState.buffer = NULL;
                        gTRKFramingState.receiveState = DSRECV_Wait;
                        return id;
                    }
                    gTRKFramingState.receiveState = DSRECV_Wait;
                }
            } else {
                if (gTRKFramingState.isEscape) {
                    ch ^= 0x20;
                    gTRKFramingState.isEscape = FALSE;
                } else if ((int)(s8)ch == 0x7d) {
                    gTRKFramingState.isEscape = TRUE;
                    break;
                }

                err = AppendBuffer1_ui8(gTRKFramingState.buffer, ch);
                gTRKFramingState.fcsType += ch;
            }
            break;

        case DSRECV_FrameOverflow:
            if ((int)ch == 0x7e) {
                ReleaseFrameBuffer();
                gTRKFramingState.buffer = NULL;
                gTRKFramingState.receiveState = DSRECV_Wait;
            }
            break;

        default:
            break;
        }

        uartErr = TRKReadUARTPoll(&ch);
    }

    return -1;
}

void TRKGetInput(void) {
    u8 cmd;
    TRKBuffer* msg;
    MessageBufferID id = TRKTestForPacket();
    if (id != -1) {
        msg = TRKGetBuffer(id);

        TRKSetBufferPosition(msg, 0);
        TRKReadBuffer1_ui8(msg, &cmd);
        if (cmd < 0x80) {
            TRKEvent event;

            TRKConstructEvent(&event, NUBEVENT_Request);
            event.msgBufID = id;
            gTRKFramingState.msgBufID = -1;
            TRKPostEvent(&event);
        } else {
            TRKReleaseBuffer(id);
        }
    }
}

void TRKProcessInput(int bufferIdx) {
    TRKEvent event;

    TRKConstructEvent(&event, NUBEVENT_Request);
    event.msgBufID = bufferIdx;
    gTRKFramingState.msgBufID = -1;
    TRKPostEvent(&event);
}

DSError TRKInitializeSerialHandler() {
    gTRKFramingState.msgBufID = -1;
    gTRKFramingState.receiveState = DSRECV_Wait;
    gTRKFramingState.isEscape = FALSE;


    return DS_NoError;
}

DSError TRKTerminateSerialHandler(void) {
    return DS_NoError;
}
