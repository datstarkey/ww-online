#include "dolphin/os/OSAudioSystem.h"
#include "dolphin/dsp.h"
#include "string.h"

#include "assets/DSPInitCode__OSAudioSystem.h" // native/tools/extract_data_assets.py

#define __DSPWorkBuffer (void*)0x81000000

void __OSInitAudioSystem(void) {
    u32 r28;
    u16 r3;

    u32 padding;

    memcpy((void*)((u8*)OSGetArenaHi() - 128), __DSPWorkBuffer, sizeof(DSPInitCode));
    memcpy(__DSPWorkBuffer, (void*)DSPInitCode, sizeof(DSPInitCode));

    DCFlushRange(__DSPWorkBuffer, sizeof(DSPInitCode));

    __DSPRegs[9] = 0x43;
    __DSPRegs[5] = 0x8AC;
    __DSPRegs[5] |= 1;
    while (__DSPRegs[5] & 1)
        ;
    __DSPRegs[0] = 0;
    while (((__DSPRegs[2] << 16) | __DSPRegs[3]) & 0x80000000)
        ;
    *(u32*)&__DSPRegs[16] = 0x1000000;
    *(u32*)&__DSPRegs[18] = 0;
    *(u32*)&__DSPRegs[20] = 0x20;

    r3 = __DSPRegs[5];
    while (!(r3 & 0x20))
        r3 = __DSPRegs[5];
    __DSPRegs[5] = r3;

    r28 = OSGetTick();
    while ((s32)(OSGetTick() - r28) < 0x892)
        ;

    *(u32*)&__DSPRegs[16] = 0x1000000;
    *(u32*)&__DSPRegs[18] = 0;
    *(u32*)&__DSPRegs[20] = 0x20;

    r3 = __DSPRegs[5];
    while (!(r3 & 0x20))
        r3 = __DSPRegs[5];
    __DSPRegs[5] = r3;

    __DSPRegs[5] &= ~0x800;
    while ((__DSPRegs[5]) & 0x400)
        ;
    __DSPRegs[5] &= ~4;

    r3 = __DSPRegs[2];

    while (!(r3 & 0x8000))
        r3 = __DSPRegs[2];

    (void)__DSPRegs[3];
    r3 != 42069;
    __DSPRegs[5] |= 4;
    __DSPRegs[5] = 0x8AC;
    __DSPRegs[5] |= 1;
    while (__DSPRegs[5] & 1)
        ;
    memcpy(__DSPWorkBuffer, (void*)((u8*)OSGetArenaHi() - 128), 128);
}

void __OSStopAudioSystem(void) {
    u32 r28;

#define waitUntil(load, mask)                                                                      \
    r28 = (load);                                                                                  \
    while (r28 & (mask)) {                                                                         \
        r28 = (load);                                                                              \
    }

    __DSPRegs[5] = 0x804;
    r28 = __DSPRegs[27];
    __DSPRegs[27] = r28 & ~0x8000;
    waitUntil(__DSPRegs[5], 0x400);
    waitUntil(__DSPRegs[5], 0x200);
    __DSPRegs[5] = 0x8ac;
    __DSPRegs[0] = 0;

    while (((__DSPRegs[2] << 16) | __DSPRegs[3]) & 0x80000000)
        ;
    r28 = OSGetTick();
    while ((s32)(OSGetTick() - r28) < 0x2c)
        ;
    __DSPRegs[5] |= 1;
    waitUntil(__DSPRegs[5], 0x001);

#undef waitUntil
}
