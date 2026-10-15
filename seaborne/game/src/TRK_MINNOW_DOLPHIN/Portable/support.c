#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/support.h"
#include "TRK_MINNOW_DOLPHIN/utils/common/MWTrace.h"
#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/msgbuf.h"
#include "string.h"

static inline DSError SuppAppendBuffer1_ui8(TRKBuffer* buffer, const u8 data) {
    if (buffer->position >= 0x880) {
        return DS_MessageBufferOverflow;
    }

    buffer->data[buffer->position++] = data;
    buffer->length++;
    return DS_NoError;
}

DSError TRKSuppAccessFile(u32 file_handle, u8* data, size_t* count, DSIOResult* io_result,
                          BOOL need_reply, BOOL read) {
    DSError error;
    int replyBufferId;
    TRKBuffer* replyBuffer;
    u32 length;
    int bufferId;
    TRKBuffer* buffer;
    u32 i;
    u16 replyLength;
    u8 replyIOResult;
    BOOL exit;

    if (data == NULL || *count == 0) {
        return DS_ParameterError;
    }

    exit = FALSE;
    *(u8*)io_result = DS_IONoError;
    i = 0;
    error = DS_NoError;
    while (!exit && i < *count && error == DS_NoError && *(u8*)io_result == 0) {
        if (*count - i <= 0x800) {
            length = *count - i;
        } else {
            length = 0x800;
        }

        error = TRKGetFreeBuffer(&bufferId, &buffer);

        if (error == DS_NoError) {
            error = SuppAppendBuffer1_ui8(buffer, read ? DSMSG_ReadFile : DSMSG_WriteFile);
        }

        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui32(buffer, file_handle);
        }

        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui16(buffer, length);
        }

        if (!read && error == DS_NoError) {
            error = TRKAppendBuffer_ui8(buffer, data + i, length);
        }

        if (error == DS_NoError) {
            if (need_reply) {
                BOOL b;

                replyLength = 0;
                replyIOResult = DS_IONoError;
                b = read && file_handle == 0;

                error = TRKRequestSend(buffer, &replyBufferId, read ? 5 : 5, 3, !b);
                if (error == DS_NoError) {
                    replyBuffer = (TRKBuffer*)TRKGetBuffer(replyBufferId);
                    TRKSetBufferPosition(replyBuffer, 2);
                }

                if (error == DS_NoError) {
                    error = TRKReadBuffer1_ui8(replyBuffer, &replyIOResult);
                }

                if (error == DS_NoError) {
                    error = TRKReadBuffer1_ui16(replyBuffer, &replyLength);
                }

                if (read && error == DS_NoError) {
                    if (replyBuffer->length != replyLength + 5) {
                        replyLength = replyBuffer->length - 5;
                        if (replyIOResult == DS_IONoError) {
                            replyIOResult = DS_IOError;
                        }
                    }

                    if (replyLength <= length) {
                        error = TRKReadBuffer_ui8(replyBuffer, data + i, replyLength);
                    }
                }

                if (replyLength != length) {
                    if (!read || replyLength >= length) {
                        if (replyIOResult == DS_IONoError) {
                            replyIOResult = DS_IOError;
                        }
                    }

                    length = replyLength;
                    exit = TRUE;
                }

                *(u8*)io_result = replyIOResult;
                TRKReleaseBuffer(replyBufferId);
            } else {
                error = TRKMessageSend(buffer);
            }
        }

        TRKReleaseBuffer(bufferId);
        i += length;
    }

    *count = i;
    return error;
}

DSError TRKRequestSend(TRKBuffer* msgBuf, int* bufferId, u32 p1, u32 p2, int p3) {
    int error = DS_NoError;
    TRKBuffer* buffer;
    u32 counter;
    int count;
    u8 msgCmd;
    u8 msgReplyError;
    BOOL badReply = TRUE;

    *bufferId = -1;

    for (count = p2 + 1; count != 0 && *bufferId == -1 && error == DS_NoError; count--) {
        error = TRKMessageSend(msgBuf);
        if (error == DS_NoError) {
            if (p3) {
                counter = 0;
            }

            while (TRUE) {
                do {
                    *bufferId = TRKTestForPacket();
                    if (*bufferId != -1)
                        break;
                } while (!p3 || ++counter < 79999980);

                if (*bufferId == -1)
                    break;

                badReply = 0;

                buffer = TRKGetBuffer(*bufferId);
                TRKSetBufferPosition(buffer, 0);
                error = TRKReadBuffer1_ui8(buffer, &msgCmd);
                if (error != DS_NoError)
                    break;

                if (msgCmd >= DSMSG_ReplyACK)
                    break;

                TRKProcessInput(*bufferId);
                *bufferId = -1;
            }

            if (*bufferId != -1) {
                if (buffer->length < p1) {
                    // OSReport("MetroTRK - bad reply size %ld\n", buffer->length);
                    badReply = TRUE;
                }
                if (error == DS_NoError && !badReply) {
                    error = TRKReadBuffer1_ui8(buffer, &msgReplyError);
                }
                if (error == DS_NoError && !badReply) {
                    if (msgCmd != DSMSG_ReplyACK || msgReplyError != DSREPLY_NoError) {
                        badReply = TRUE;
                    }
                }
                if (error != DS_NoError || badReply) {
                    TRKReleaseBuffer(*bufferId);
                    *bufferId = -1;
                }
            }
        }
    }

    if (*bufferId == -1) {
        error = DS_Error800;
    }

    return error;
}

static inline DSError AppendBuffer1_ui8(TRKBuffer* buffer, const u8 data) {
    if (buffer->position >= 0x880) {
        return DS_MessageBufferOverflow;
    }

    buffer->data[buffer->position++] = data;
    buffer->length++;
    return DS_NoError;
}

DSError HandleOpenFileSupportRequest(const char* path, u8 replyError, u32* param_3,
                                     DSIOResult* ioResult) {
    DSError error;
    int bufferId2;
    int bufferId1;
    TRKBuffer* tempBuffer;
    TRKBuffer* buffer;

    *param_3 = 0;
    error = TRKGetFreeBuffer(&bufferId1, &buffer);

    if (error == DS_NoError) {
        error = AppendBuffer1_ui8(buffer, DSMSG_OpenFile);
    }

    if (error == DS_NoError) {
        error = AppendBuffer1_ui8(buffer, replyError);
    }

    if (error == DS_NoError) {
        error = TRKAppendBuffer1_ui16(buffer, strlen(path) + 1);
    }

    if (error == DS_NoError) {
        error = TRKAppendBuffer_ui8(buffer, (u8*)path, strlen(path) + 1);
    }

    if (error == DS_NoError) {
        *(u8*)ioResult = DS_IONoError;
        error = TRKRequestSend(buffer, &bufferId2, 7, 3, 0);

        if (error == DS_NoError) {
            tempBuffer = TRKGetBuffer(bufferId2);
            TRKSetBufferPosition(tempBuffer, 2);
        }

        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui8(tempBuffer, (u8*)ioResult);
        }

        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui32(tempBuffer, param_3);
        }

        TRKReleaseBuffer(bufferId2);
    }
    TRKReleaseBuffer(bufferId1);
    return error;
}

static inline DSError TRKAppendBuffer1_ui8(TRKBuffer* buffer, const u8 data) {
    if (buffer->position >= 0x880) {
        return DS_MessageBufferOverflow;
    }

    buffer->data[buffer->position++] = data;
    buffer->length++;
    return DS_NoError;
}

DSError HandleCloseFileSupportRequest(u32 fileHandle, u8* ioResult) {
    DSError error;
    int replyBufferId;
    int bufferId;
    TRKBuffer* buffer1;
    TRKBuffer* buffer2;

    error = TRKGetFreeBuffer(&bufferId, &buffer1);

    if (error == DS_NoError) {
        error = TRKAppendBuffer1_ui8(buffer1, DSMSG_CloseFile);
    }

    if (error == DS_NoError) {
        error = TRKAppendBuffer1_ui32(buffer1, fileHandle);
    }

    if (error == DS_NoError) {
        *ioResult = DS_IONoError;
        error = TRKRequestSend(buffer1, &replyBufferId, 3, 3, 0);

        if (error == DS_NoError) {
            buffer2 = TRKGetBuffer(replyBufferId);
            TRKSetBufferPosition(buffer2, 2);
        }

        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui8(buffer2, ioResult);
        }

        TRKReleaseBuffer(replyBufferId);
    }

    TRKReleaseBuffer(bufferId);
    return error;
}

DSError HandlePositionFileSupportRequest(DSReplyError replyErr, u32* param_2, u8 param_3,
                                         DSIOResult* ioResult) {
    DSError error;
    int bufferId2;
    int bufferId1;
    TRKBuffer* buffer1;
    TRKBuffer* buffer2;

    error = TRKGetFreeBuffer(&bufferId1, &buffer1);

    if (error == DS_NoError) {
        error = SuppAppendBuffer1_ui8(buffer1, DSMSG_PositionFile);
    }

    if (error == DS_NoError) {
        error = TRKAppendBuffer1_ui32(buffer1, replyErr);
    }

    if (error == DS_NoError) {
        error = TRKAppendBuffer1_ui32(buffer1, *param_2);
    }

    if (error == DS_NoError) {
        error = SuppAppendBuffer1_ui8(buffer1, param_3);
    }

    if (error == DS_NoError) {
        *(u8*)ioResult = DS_IONoError;
        error = TRKRequestSend(buffer1, &bufferId2, 3, 3, 0);

        if (error == DS_NoError) {
            buffer2 = TRKGetBuffer(bufferId2);
            TRKSetBufferPosition(buffer2, 2);
        }

        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui8(buffer2, (u8*)ioResult);
        }

        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui32(buffer2, param_2);
        } else {
            *param_2 = -1;
        }

        TRKReleaseBuffer(bufferId2);
    }

    TRKReleaseBuffer(bufferId1);
    return error;
}
