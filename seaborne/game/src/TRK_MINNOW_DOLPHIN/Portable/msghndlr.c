#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/msghndlr.h"
#include "TRK_MINNOW_DOLPHIN/MetroTRK/Portable/nubevent.h"
#include "TRK_MINNOW_DOLPHIN/utils/common/MWTrace.h"
#include "trk.h"
#include "string.h"

static inline DSError TRKAppendBuffer1_ui8(TRKBuffer* buffer, const u8 data) {
    if (buffer->position >= 0x880) {
        return DS_MessageBufferOverflow;
    }

    buffer->data[buffer->position++] = data;
    buffer->length++;
    return DS_NoError;
}

static BOOL IsTRKConnected;

BOOL GetTRKConnected(void) {
    return IsTRKConnected;
}

void SetTRKConnected(BOOL isTRKConnected) {
    IsTRKConnected = isTRKConnected;
}

DSError TRKStandardACK(TRKBuffer* buffer, MessageCommandID commandID,
                              DSReplyError replyError) {
    DSError err;
    int retry;

    TRKResetBuffer(buffer, TRUE);

    if (buffer->position < 0x880) {
        buffer->data[buffer->position++] = (u8)commandID;
        buffer->length++;
    }

    if (buffer->position < 0x880) {
        buffer->data[buffer->position++] = (u8)replyError;
        buffer->length++;
    }

    retry = 3;
    do {
        err = TRKMessageSend(buffer);
        retry--;
    } while (err != DS_NoError && retry > 0);

    return err;
}

DSError TRKDoUnsupported(TRKBuffer* buffer) {
    DSError err;
    int retry;

    TRKResetBuffer(buffer, TRUE);
    TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
    TRKAppendBuffer1_ui8(buffer, DSREPLY_UnsupportedCommandError);

    retry = 3;
    do {
        err = TRKMessageSend(buffer);
        retry--;
    } while (err != DS_NoError && retry > 0);

    return err;
}

DSError TRKDoConnect(TRKBuffer* buffer) {
    IsTRKConnected = TRUE;
    return TRKStandardACK(buffer, 0x80, DSREPLY_NoError);
}

DSError TRKDoDisconnect(TRKBuffer* buffer) {
    int retry;
    DSError err;
    TRKEvent event;

    IsTRKConnected = FALSE;

    TRKResetBuffer(buffer, TRUE);

    if (buffer->position < 0x880) {
        buffer->data[buffer->position++] = (u8)DSMSG_ReplyACK;
        buffer->length++;
    }

    if (buffer->position < 0x880) {
        buffer->data[buffer->position++] = (u8)DSREPLY_NoError;
        buffer->length++;
    }

    retry = 3;
    do {
        err = TRKMessageSend(buffer);
        retry--;
    } while (err != DS_NoError && retry > 0);

    if (err == DS_NoError) {
        TRKConstructEvent(&event, 1);
        TRKPostEvent(&event);
    }

    return err;
}

DSError TRKDoReset(TRKBuffer* buffer) {
    TRKStandardACK(buffer, 0x80, DSREPLY_NoError);
    __TRK_copy_vectors();
    return DS_NoError;
}

static inline DSError AppendBuffer1_ui8(TRKBuffer* buffer, const u8 data) {
    u8 d = data;

    if (buffer->position >= 0x880) {
        return DS_MessageBufferOverflow;
    }

    buffer->data[buffer->position++] = d;
    buffer->length++;
    return DS_NoError;
}

static inline DSError SendReply(TRKBuffer* buffer) {
    DSError error;
    int count = 3;

    do {
        error = TRKMessageSend(buffer);
        count--;
    } while (error != DS_NoError && count > 0);

    return error;
}

DSError TRKDoVersions(TRKBuffer* buffer) {
    DSError error;
    u8 versions[4];

    if (buffer->length != 1) {
        TRKResetBuffer(buffer, TRUE);
        AppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        AppendBuffer1_ui8(buffer, DSREPLY_PacketSizeError);
        return SendReply(buffer);
    } else {
        TRKResetBuffer(buffer, TRUE);
        AppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        AppendBuffer1_ui8(buffer, DSREPLY_NoError);

        error = TRKTargetVersions(versions);
        if (error == DS_NoError) {
            error = AppendBuffer1_ui8(buffer, versions[0]);
        }
        if (error == DS_NoError) {
            error = AppendBuffer1_ui8(buffer, versions[1]);
        }
        if (error == DS_NoError) {
            error = AppendBuffer1_ui8(buffer, versions[2]);
        }
        if (error == DS_NoError) {
            error = AppendBuffer1_ui8(buffer, versions[3]);
        }

        if (error != DS_NoError) {
            TRKResetBuffer(buffer, TRUE);
            AppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
            AppendBuffer1_ui8(buffer, DSREPLY_CWDSError);
            return SendReply(buffer);
        }

        return SendReply(buffer);
    }
}

DSError TRKDoSupportMask(TRKBuffer* buffer) {
    DSError error;
    u8 mask[0x20];

    if (buffer->length != 1) {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_PacketSizeError);
        return SendReply(buffer);
    } else {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_NoError);

        error = TRKTargetSupportMask(mask);
        if (error == DS_NoError) {
            error = TRKAppendBuffer(buffer, mask, sizeof(mask));
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, 2);
        }

        if (error != DS_NoError) {
            TRKResetBuffer(buffer, TRUE);
            TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
            TRKAppendBuffer1_ui8(buffer, DSREPLY_CWDSError);
            return SendReply(buffer);
        }

        return SendReply(buffer);
    }
}

typedef struct DSCPUType {
    u8 cpuMajor;
    u8 cpuMinor;
    u8 bigEndian;
    u8 defaultTypeSize;
    u8 fpTypeSize;
    u8 extended1TypeSize;
    u8 extended2TypeSize;
} DSCPUType;

DSError TRKTargetCPUType(DSCPUType* cpuType);

DSError TRKDoCPUType(TRKBuffer* buffer) {
    DSError error;
    DSCPUType cpuType;

    if (buffer->length != 1) {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_PacketSizeError);
        return SendReply(buffer);
    } else {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_NoError);

        error = TRKTargetCPUType(&cpuType);
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, cpuType.cpuMajor);
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, cpuType.cpuMinor);
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, cpuType.bigEndian);
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, cpuType.defaultTypeSize);
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, cpuType.fpTypeSize);
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, cpuType.extended1TypeSize);
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui8(buffer, cpuType.extended2TypeSize);
        }

        if (error != DS_NoError) {
            TRKResetBuffer(buffer, TRUE);
            TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
            TRKAppendBuffer1_ui8(buffer, DSREPLY_CWDSError);
            return SendReply(buffer);
        }

        return SendReply(buffer);
    }
}

DSError TRKDoReadMemory(TRKBuffer* buffer) {
    u8 buf[0x800];
    int error;
    u8 commandID;
    u8 options;
    u16 length;
    u32 start;
    size_t tempLength;

    if (buffer->length != 8) {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_PacketSizeError);
        return SendReply(buffer);
    }

    TRKSetBufferPosition(buffer, 0);
    error = TRKReadBuffer1_ui8(buffer, &commandID);
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(buffer, &options);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui16(buffer, &length);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui32(buffer, &start);
    }

    if (options & DSMSGMEMORY_Extended) {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_UnsupportedOptionError);
        return SendReply(buffer);
    }

    if (length > 0x800) {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_ParameterError);
        return SendReply(buffer);
    }

    TRKResetBuffer(buffer, TRUE);
    TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
    TRKAppendBuffer1_ui8(buffer, DSREPLY_NoError);

    if (error == DS_NoError) {
        tempLength = length;
        error = TRKTargetAccessMemory(buf, start, &tempLength,
                                      options & DSMSGMEMORY_Userview ? MEMACCESS_UserMemory
                                                                     : MEMACCESS_DebuggerMemory,
                                      TRUE);
        length = (u16)tempLength;
        if (error == DS_NoError) {
            error = TRKAppendBuffer1_ui16(buffer, length);
        }
        if (error == DS_NoError) {
            error = TRKAppendBuffer(buffer, buf, tempLength);
        }
    }

    if (error != DS_NoError) {
        int replyErr;
        switch (error) {
        case DS_CWDSException:
            replyErr = DSREPLY_CWDSException;
            break;
        case DS_InvalidMemory:
            replyErr = DSREPLY_InvalidMemoryRange;
            break;
        case DS_InvalidProcessID:
            replyErr = DSREPLY_InvalidProcessID;
            break;
        case DS_InvalidThreadID:
            replyErr = DSREPLY_InvalidThreadID;
            break;
        case DS_OSError:
            replyErr = DSREPLY_OSError;
            break;
        default:
            replyErr = DSREPLY_CWDSError;
        }

        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, replyErr);
        return SendReply(buffer);
    }

    return SendReply(buffer);
}

DSError TRKDoWriteMemory(TRKBuffer* b) {
    int error;
    DSError sendError;
    int retries;
    u8 commandID;
    u8 options;
    u16 length;
    u32 start;
    size_t tempLength;
    u8 buf[0x800];

    if (b->length <= 8) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_PacketSizeError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    TRKSetBufferPosition(b, 0);
    error = TRKReadBuffer1_ui8(b, &commandID);
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(b, &options);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui16(b, &length);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui32(b, &start);
    }

    if (options & DSMSGMEMORY_Extended) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_UnsupportedOptionError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    if (b->length != length + 8 || length > 0x800) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_ParameterError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    if (error == DS_NoError) {
        tempLength = length;
        error = TRKReadBuffer(b, buf, tempLength);
        if (error == DS_NoError) {
            error = TRKTargetAccessMemory(buf, start, &tempLength,
                                          options & DSMSGMEMORY_Userview ? 0 : 1, FALSE);
        }
        length = tempLength;
    }

    if (error == DS_NoError) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_NoError);
    }
    if (error == DS_NoError) {
        error = TRKAppendBuffer1_ui16(b, length);
    }

    if (error != DS_NoError) {
        int replyError;
        switch (error) {
        case DS_CWDSException:
            replyError = DSREPLY_CWDSException;
            break;
        case DS_InvalidMemory:
            replyError = DSREPLY_InvalidMemoryRange;
            break;
        case DS_InvalidProcessID:
            replyError = DSREPLY_InvalidProcessID;
            break;
        case DS_InvalidThreadID:
            replyError = DSREPLY_InvalidThreadID;
            break;
        case DS_OSError:
            replyError = DSREPLY_OSError;
            break;
        default:
            replyError = DSREPLY_CWDSError;
            break;
        }
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, replyError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    retries = 3;
    do {
        sendError = TRKMessageSend(b);
        retries--;
    } while (sendError != DS_NoError && retries > 0);
    return sendError;
}

DSError TRKDoReadRegisters(TRKBuffer* b) {
    int error;
    DSError sendError;
    int retries;
    u8 commandID;
    u8 options;
    u16 firstRegister;
    u16 lastRegister;
    size_t registersLength;

    if (b->length != 6) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_PacketSizeError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    TRKSetBufferPosition(b, 0);
    error = TRKReadBuffer1_ui8(b, &commandID);
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(b, &options);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui16(b, &firstRegister);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui16(b, &lastRegister);
    }

    if (firstRegister > lastRegister) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_InvalidRegisterRange);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    if (error == DS_NoError) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_NoError);
    }

    switch (options & 0x7) {
    case DSREG_Default:
        error = TRKTargetAccessDefault(firstRegister, lastRegister, b, &registersLength, TRUE);
        break;
    case DSREG_FP:
        error = TRKTargetAccessFP(firstRegister, lastRegister, b, &registersLength, TRUE);
        break;
    case DSREG_Extended1:
        error = TRKTargetAccessExtended1(firstRegister, lastRegister, b, &registersLength, TRUE);
        break;
    case DSREG_Extended2:
        error = TRKTargetAccessExtended2(firstRegister, lastRegister, b, &registersLength, TRUE);
        break;
    default:
        error = DS_UnsupportedError;
        break;
    }

    // Check if there was an error, and respond accordingly
    if (error != DS_NoError) {
        int replyError;
        switch (error) {
        case DS_UnsupportedError:
            replyError = DSREPLY_UnsupportedOptionError;
            break;
        case DS_InvalidRegister:
            replyError = DSREPLY_InvalidRegisterRange;
            break;
        case DS_CWDSException:
            replyError = DSREPLY_CWDSException;
            break;
        case DS_InvalidProcessID:
            replyError = DSREPLY_InvalidProcessID;
            break;
        case DS_InvalidThreadID:
            replyError = DSREPLY_InvalidThreadID;
            break;
        case DS_OSError:
            replyError = DSREPLY_OSError;
            break;
        default:
            replyError = DSREPLY_CWDSError;
        }

        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, replyError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    retries = 3;
    do {
        sendError = TRKMessageSend(b);
        retries--;
    } while (sendError != DS_NoError && retries > 0);
    return sendError;
}

DSError TRKDoWriteRegisters(TRKBuffer* b) {
    int error;
    DSError sendError;
    int retries;
    u8 commandID;
    u8 options;
    u16 firstRegister;
    u16 lastRegister;
    size_t registersLength;

    if (b->length <= 6) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_PacketSizeError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    TRKSetBufferPosition(b, 0);
    error = TRKReadBuffer1_ui8(b, &commandID);
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(b, &options);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui16(b, &firstRegister);
    }
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui16(b, &lastRegister);
    }

    if (firstRegister > lastRegister) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_InvalidRegisterRange);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    switch (options) {
    case DSREG_Default:
        error = TRKTargetAccessDefault(firstRegister, lastRegister, b, &registersLength, FALSE);
        break;
    case DSREG_FP:
        error = TRKTargetAccessFP(firstRegister, lastRegister, b, &registersLength, FALSE);
        break;
    case DSREG_Extended1:
        error = TRKTargetAccessExtended1(firstRegister, lastRegister, b, &registersLength, FALSE);
        break;
    case DSREG_Extended2:
        error = TRKTargetAccessExtended2(firstRegister, lastRegister, b, &registersLength, FALSE);
        break;
    default:
        // invalid option
        error = DS_UnsupportedError;
        break;
    }

    if (error == DS_NoError) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_NoError);
    }

    // Check if there was an error, and respond accordingly
    if (error != DS_NoError) {
        int replyError;
        switch (error) {
        case DS_UnsupportedError:
            replyError = DSREPLY_UnsupportedOptionError;
            break;
        case DS_InvalidRegister:
            replyError = DSREPLY_InvalidRegisterRange;
            break;
        case DS_MessageBufferReadError:
            replyError = DSREPLY_PacketSizeError;
            break;
        case DS_CWDSException:
            replyError = DSREPLY_CWDSException;
            break;
        case DS_InvalidProcessID:
            replyError = DSREPLY_InvalidProcessID;
            break;
        case DS_InvalidThreadID:
            replyError = DSREPLY_InvalidThreadID;
            break;
        case DS_OSError:
            replyError = DSREPLY_OSError;
            break;
        default:
            replyError = DSREPLY_CWDSError;
        }

        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, replyError);

        retries = 3;
        do {
            sendError = TRKMessageSend(b);
            retries--;
        } while (sendError != DS_NoError && retries > 0);
        return sendError;
    }

    retries = 3;
    do {
        sendError = TRKMessageSend(b);
        retries--;
    } while (sendError != DS_NoError && retries > 0);
    return sendError;
}

DSError TRKDoFlushCache(TRKBuffer* buffer) {
    DSError error;
    u8 unused;
    u8 options;
    u32 start;
    u32 end;
    DSReplyError replyError;
    int retries;

    if (buffer->length != 10) {
        return TRKStandardACK(buffer, DSMSG_ReplyACK, DSREPLY_PacketSizeError);
    }

    TRKSetBufferPosition(buffer, 0);
    error = TRKReadBuffer1_ui8(buffer, &unused);

    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(buffer, &options);
    }

    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui32(buffer, &start);
    }

    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui32(buffer, &end);
    }

    if (start > end) {
        return TRKStandardACK(buffer, DSMSG_ReplyACK, DSREPLY_InvalidMemoryRange);
    }

    if (error == DS_NoError) {
        error = TRKTargetFlushCache(options, start, end);
    }

    if (error == DS_NoError) {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, DSREPLY_NoError);
    }

    if (error != DS_NoError) {
        switch (error) {
        case DS_UnsupportedError:
            replyError = DSREPLY_UnsupportedOptionError;
            break;
        default:
            replyError = DSREPLY_CWDSError;
            break;
        }

        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(buffer, replyError);

        retries = 3;
        do {
            error = TRKMessageSend(buffer);
            retries--;
        } while (error != DS_NoError && retries > 0);
        return error;
    }

    retries = 3;
    do {
        error = TRKMessageSend(buffer);
        retries--;
    } while (error != DS_NoError && retries > 0);
    return error;
}

DSError TRKDoContinue(TRKBuffer* buffer) {
    DSError err;
    int retries;

    if (!TRKTargetStopped()) {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, 0x80);
        TRKAppendBuffer1_ui8(buffer, 0x16);

        retries = 3;
        do {
            err = TRKMessageSend(buffer);
            retries--;
        } while (err != DS_NoError && retries > 0);
    } else {
        TRKResetBuffer(buffer, TRUE);
        TRKAppendBuffer1_ui8(buffer, 0x80);
        TRKAppendBuffer1_ui8(buffer, 0x00);

        retries = 3;
        do {
            err = TRKMessageSend(buffer);
            retries--;
        } while (err != DS_NoError && retries > 0);

        if (err == DS_NoError) {
            err = TRKTargetContinue();
        }
    }

    return err;
}

DSError TRKDoStep(TRKBuffer* b) {
    DSError error;
    int retries;
    u8 unused;
    u8 options;
    u8 count;
    u32 rangeStart;
    u32 rangeEnd;
    u32 pc;

    if (b->length < 3) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_PacketSizeError);

        retries = 3;
        do {
            error = TRKMessageSend(b);
            retries--;
        } while (error != DS_NoError && retries > 0);
        return error;
    }

    TRKSetBufferPosition(b, 0);
    error = TRKReadBuffer1_ui8(b, &unused);
    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(b, &options);
    }

    switch (options) {
    case DSSTEP_IntoCount:
    case DSSTEP_OverCount:
        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui8(b, &count);
        }

        if (count >= 1) {
            break;
        }

        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_ParameterError);

        retries = 3;
        do {
            error = TRKMessageSend(b);
            retries--;
        } while (error != DS_NoError && retries > 0);
        return error;

    case DSSTEP_IntoRange:
    case DSSTEP_OverRange:
        if (b->length != 10) {
            TRKResetBuffer(b, TRUE);
            TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
            TRKAppendBuffer1_ui8(b, DSREPLY_PacketSizeError);

            retries = 3;
            do {
                error = TRKMessageSend(b);
                retries--;
            } while (error != DS_NoError && retries > 0);
            return error;
        }

        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui32(b, &rangeStart);
        }
        if (error == DS_NoError) {
            error = TRKReadBuffer1_ui32(b, &rangeEnd);
        }

        pc = TRKTargetGetPC();
        if (pc >= rangeStart && pc <= rangeEnd) {
            break;
        }

        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_ParameterError);

        retries = 3;
        do {
            error = TRKMessageSend(b);
            retries--;
        } while (error != DS_NoError && retries > 0);
        return error;

    default:
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_UnsupportedOptionError);

        retries = 3;
        do {
            error = TRKMessageSend(b);
            retries--;
        } while (error != DS_NoError && retries > 0);
        return error;
    }

    if (!TRKTargetStopped()) {
        TRKResetBuffer(b, TRUE);
        TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(b, DSREPLY_NotStopped);

        retries = 3;
        do {
            error = TRKMessageSend(b);
            retries--;
        } while (error != DS_NoError && retries > 0);
        return error;
    }

    TRKResetBuffer(b, TRUE);
    TRKAppendBuffer1_ui8(b, DSMSG_ReplyACK);
    TRKAppendBuffer1_ui8(b, DSREPLY_NoError);

    retries = 3;
    do {
        error = TRKMessageSend(b);
        retries--;
    } while (error != DS_NoError && retries > 0);

    if (error == DS_NoError) {
        switch (options) {
        case DSSTEP_IntoCount:
        case DSSTEP_OverCount:
            error = TRKTargetSingleStep(count, (options == DSSTEP_OverCount));
            break;
        case DSSTEP_IntoRange:
        case DSSTEP_OverRange:
            error = TRKTargetStepOutOfRange(rangeStart, rangeEnd, (options == DSSTEP_OverRange));
            break;
        }
    }

    return error;
}

DSError TRKDoStop(TRKBuffer* b) {
    MessageCommandID c;

    switch (TRKTargetStop()) {
    case DS_NoError:
        c = DSMSG_Ping;
        break;
    case DS_InvalidProcessID:
        c = '!';
        break;
    case DS_InvalidThreadID:
        c = '\"';
        break;
    case DS_OSError:
        c = ' ';
        break;
    default:
        c = DSMSG_Connect;
        break;
    }

    return TRKStandardACK(b, DSMSG_ReplyACK, c);
}

DSError TRKDoSetOption(TRKBuffer* message) {
    u8 unused = 0;
    u8 optionID = 0;
    u8 optionValue = 0;
    DSError error;
    int retry;

    TRKSetBufferPosition(message, 0);
    error = TRKReadBuffer1_ui8(message, &unused);

    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(message, &optionID);
    }

    if (error == DS_NoError) {
        error = TRKReadBuffer1_ui8(message, &optionValue);
    }

    if (error != DS_NoError) {
        TRKResetBuffer(message, TRUE);
        TRKAppendBuffer1_ui8(message, DSMSG_ReplyACK);
        TRKAppendBuffer1_ui8(message, DS_StepError);

        retry = 3;
        do {
            error = TRKMessageSend(message);
            retry--;
        } while (error != DS_NoError && retry > 0);
    } else {
        if (optionID == 1) {
            SetUseSerialIO(optionValue);
        }
    }

    TRKResetBuffer(message, TRUE);
    TRKAppendBuffer1_ui8(message, DSMSG_ReplyACK);
    TRKAppendBuffer1_ui8(message, DS_NoError);

    retry = 3;
    do {
        error = TRKMessageSend(message);
        retry--;
    } while (error != DS_NoError && retry > 0);

    return error;
}
