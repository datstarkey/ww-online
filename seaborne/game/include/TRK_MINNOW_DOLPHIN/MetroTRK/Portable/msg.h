#ifndef METROTRK_PORTABLE_MSG_H
#define METROTRK_PORTABLE_MSG_H

#include "dolphin/types.h"

typedef struct _TRK_Msg {
	u8 _00[8];
	u32 m_msgLength;
	u32 _0C;
	u8 m_msg[0x800];
} TRK_Msg;

#endif /* METROTRK_PORTABLE_MSG_H */
