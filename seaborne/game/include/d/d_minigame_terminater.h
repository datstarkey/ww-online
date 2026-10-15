#ifndef D_MINIGAME_TERMINATER_H
#define D_MINIGAME_TERMINATER_H

#include "d/d_drawlist.h"
#if TARGET_PC
#include "f_op/f_op_msg_mng.h" // fopMsgM_pane_class members
#endif

class JKRArchive;
class J2DPane;
class J2DScreen;
struct fopMsgM_pane_class;

class dDlst_TerminaterScrnDraw_c : public dDlst_base_c {
public:
    dDlst_TerminaterScrnDraw_c() {
        field_0x1980 = 0;
        field_0x1982 = 0;
        field_0x1984 = 0;
    }

    f32 acc(s16 param_0, s16 param_1, s16 param_2) {
        return SQUARE((f32)(param_1 - param_2)) / SQUARE((f32)(param_0 - param_2));
    }
    
    void setScreen(const char*, JKRArchive*);

    /* 0x04 */ J2DScreen* mpScrn;
    void setScrnFailed();
    void setScrnSuccess(int, int);
    void changeNumberTexture(J2DPane*, int);
    const char* getNumber(int);
    void setAlphaFailed();
    void setAlphaSuccess();
    void getRotate(fopMsgM_pane_class*);
    void setRotate(fopMsgM_pane_class*, f32);
    void setScale(fopMsgM_pane_class*, f32);
    bool animeF1(int);
    bool animeF2(int);
    bool animeF3(int);
    bool animeS1();
    bool animeS2(fopMsgM_pane_class*);
    bool animeS3();
    bool animeFailed1();
    bool animeFailed2();
    bool animeSuccess();
    bool animePerfect();
    void draw();

#if TARGET_PC
    // The GameCube code reaches these through overlay structs over the blob below
    // (d_minigame_terminater.cpp, *_mem_c), at GameCube offsets; the panes hold pointers, so on the
    // host they are real members and the overlay names alias this class.
    /* 0x0008 */ fopMsgM_pane_class mPerfectPane[10];
    /* 0x0238 */ fopMsgM_pane_class mNtPane[10];
    /* 0x0468 */ fopMsgM_pane_class mNkPane[10];
    /* 0x0698 */ fopMsgM_pane_class mDmpePane;
    /* 0x06D0 */ fopMsgM_pane_class mFailedPane[17];
    /* 0x0A88 */ fopMsgM_pane_class mYougotPane[13];
    /* 0x0D60 */ fopMsgM_pane_class mRemainPane[17];
    /* 0x1118 */ fopMsgM_pane_class mRemainKPane[17];
    /* 0x14D0 */ fopMsgM_pane_class mNumPane[8];
    /* 0x1690 */ fopMsgM_pane_class mNumKPane[8];
    /* 0x1850 */ fopMsgM_pane_class mRpxPane;
    /* 0x1888 */ fopMsgM_pane_class mRpxkPane;
    /* 0x18C0 */ fopMsgM_pane_class mNt00Pane;
    /* 0x18F8 */ fopMsgM_pane_class mNk00Pane;
    /* 0x1930 */ fopMsgM_pane_class mDmmyPane;
    /* 0x1968 */ J2DPane* mRupeePane[6];
#else
    /* 0x0008 */ u8 pad_0x0008[0x1980 - 0x8];
#endif
    /* 0x1980 */ s16 field_0x1980;
    /* 0x1982 */ s16 field_0x1982;
    /* 0x1984 */ s16 field_0x1984;
    /* 0x1986 */ u8 pad_0x1986[0x1988 - 0x1986];
};

class dMinigame_Terminater_c : public msg_class {
public:
    void getResultRupee() {}
    void getResultTime() {}
    void setResultRupee(int) {}
    void setResultTime(int) {}

    cPhs_State _create();
    BOOL _execute();
    BOOL _draw();
    BOOL _delete();

    /* 0x0FC */ dDlst_TerminaterScrnDraw_c* mTerminaterScrnDraw;
    /* 0x100 */ request_of_phase_process_class mPhs;
    /* 0x108 */ JKRSolidHeap* mHeap;
    /* 0x10C */ u32 field_0x10c;
    /* 0x110 */ u32 field_0x110;
    /* 0x114 */ s16 field_0x114;
    /* 0x116 */ u16 field_0x116;
    /* 0x118 */ s16 field_0x118;
    /* 0x11A */ s16 field_0x11a;
    /* 0x11C */ u8 field_0x11c;
};

#endif /* D_MINIGAME_TERMINATER_H */
