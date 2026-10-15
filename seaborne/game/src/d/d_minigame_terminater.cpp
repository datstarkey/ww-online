/**
 * d_minigame_terminater.cpp
 * Boating Course - Completion text
 */

#include "d/dolzel.h" // IWYU pragma: keep
#include "d/d_minigame_terminater.h"
#include "f_op/f_op_msg.h"
#include "f_op/f_op_msg_mng.h"
#if TARGET_PC
// The GameCube overlay structs below (*_mem_c) give names to dDlst_TerminaterScrnDraw_c's padding at
// GameCube offsets; on the host those members are real (d_minigame_terminater.h).
typedef dDlst_TerminaterScrnDraw_c dTerminaterScrn_mem_c;
typedef dDlst_TerminaterScrnDraw_c dTerminaterScrnS_mem_c;
typedef dDlst_TerminaterScrnDraw_c dTerminaterScrnP_mem_c;
#define mPaneD60 mRemainPane
#define mPane1118 mRemainKPane
#endif

struct dMgtem_append_c : public fopMsg_prm_class {
    /* 0x1C */ u32 field_0x1c;
    /* 0x20 */ u32 field_0x20;
    /* 0x24 */ s16 field_0x24;
    /* 0x26 */ s16 field_0x26;
}; // Size: 0x28

/* 80206FC0-80207164       .text _create__22dMinigame_Terminater_cFv */
cPhs_State dMinigame_Terminater_c::_create() {
    cPhs_State phase_state = dComIfG_resLoad(&mPhs, "Mgtem");
    if (phase_state == cPhs_COMPLEATE_e) {
        dRes_info_c* resInfo = dComIfG_getObjectResInfo("Mgtem");
        JUT_ASSERT(102, resInfo != NULL);

#if TARGET_PC
        // Bug B12 (decision H5, as bug B8): the minigame terminater's (the result screen) J2D
        // screen (its panes, pictures and textures hold 64-bit pointers on the host) needs 0x103C0
        // bytes on the host, more than the GameCube's 0xC0A0: a pane allocation failed and the
        // screen faulted on its NULL pane. Three times the size, from the game heap;
        // mDoExt_adjustSolidHeap gives back what the screen did not use.
        mHeap = mDoExt_createSolidHeapFromGameToCurrent(0xC0A0 * 3, 0x20);
#else
        mHeap = mDoExt_createSolidHeapFromGameToCurrent(0xC0A0, 0x20);
#endif
        if (mHeap != NULL) {
            mTerminaterScrnDraw = new dDlst_TerminaterScrnDraw_c();
            mTerminaterScrnDraw->setScreen("ship_race2.blo", resInfo->getArchive());
            mDoExt_restoreCurrentHeap();
            mDoExt_adjustSolidHeap(mHeap);
        } else {
            return cPhs_ERROR_e;
        }
    } else {
        return phase_state;
    }

    dMgtem_append_c* append = (dMgtem_append_c*)fopMsgM_GetAppend(this);
    field_0x11a = append->field_0x24;
    field_0x118 = append->field_0x26;
    field_0x10c = append->field_0x1c;
    field_0x110 = append->field_0x20;

    if (field_0x118 == 2 || field_0x118 == 1) {
        mTerminaterScrnDraw->setScrnSuccess(field_0x110, field_0x10c);
    } else {
        mTerminaterScrnDraw->setScrnFailed();
    }

    field_0x114 = 0x14a;
    field_0x11c = 1;

    return cPhs_COMPLEATE_e;
}

/* 80207164-8020725C       .text _execute__22dMinigame_Terminater_cFv */
BOOL dMinigame_Terminater_c::_execute() {
    if (field_0x114 > 0xf0) {
        field_0x114--;
        return TRUE;
    }

    if (field_0x118 == 2) {
        if (field_0x114 > 0) {
            mTerminaterScrnDraw->animeSuccess();
            mTerminaterScrnDraw->animePerfect();
            field_0x114--;
        } else {
            field_0x11c = 3;
        }
    } else if (field_0x118 == 1) {
        if (field_0x114 > 0) {
            mTerminaterScrnDraw->animeSuccess();
            field_0x114--;
        } else {
            field_0x11c = 3;
        }
    } else {
        if (field_0x114 > 0) {
            if (field_0x114 < 0x78) {
                mTerminaterScrnDraw->animeFailed2();
            } else {
                mTerminaterScrnDraw->animeFailed1();
            }
            field_0x114--;
        } else {
            field_0x11c = 3;
        }
    }

    return TRUE;
}

/* 8020725C-802072CC       .text _draw__22dMinigame_Terminater_cFv */
BOOL dMinigame_Terminater_c::_draw() {
    if (field_0x118 == 2 || field_0x118 == 1) {
        mTerminaterScrnDraw->setAlphaSuccess();
    } else {
        mTerminaterScrnDraw->setAlphaFailed();
    }

    dComIfGd_set2DOpa(mTerminaterScrnDraw);
    return TRUE;
}

/* 802072CC-80207318       .text _delete__22dMinigame_Terminater_cFv */
BOOL dMinigame_Terminater_c::_delete() {
    if (mHeap != NULL) {
        mDoExt_destroySolidHeap(mHeap);
    }

    dComIfG_resDelete(&mPhs, "Mgtem");

    return TRUE;
}

/* 80207318-802073C4       .text setScreen__26dDlst_TerminaterScrnDraw_cFPCcP10JKRArchive */
#include "JSystem/J2DGraph/J2DScreen.h"

void dDlst_TerminaterScrnDraw_c::setScreen(const char* i_name, JKRArchive* i_archive) {
    mpScrn = new J2DScreen();
    mpScrn->set(i_name, i_archive);
}

#include "f_op/f_op_msg_mng.h"
#include "JSystem/J2DGraph/J2DScreen.h"

s16 dMgtem_perfect_tex = 8;
s16 dMgtem_failed_tex = 8;
s16 dMgtem_yougot_tex = 6;
s16 dMgtem_remain_tex = 13;

#if !TARGET_PC
struct dTerminaterScrn_mem_c {
    /* 0x0000 */ u8 pad_0x0000[0x4];
    /* 0x0004 */ J2DScreen* mpScrn;
    /* 0x0008 */ u8 pad_0x0008[0x6D0 - 0x8];
    /* 0x06D0 */ fopMsgM_pane_class mFailedPane[8];
    /* 0x0890 */ u8 pad_0x0890[0xD60 - 0x890];
    /* 0x0D60 */ fopMsgM_pane_class mPaneD60[8];
    /* 0x0F20 */ u8 pad_0x0F20[0x1118 - 0xF20];
    /* 0x1118 */ fopMsgM_pane_class mPane1118[8];
};
#endif

/* 802073C4-8020789C       .text setScrnFailed__26dDlst_TerminaterScrnDraw_cFv */
void dDlst_TerminaterScrnDraw_c::setScrnFailed() {
    static const u32 perfect[] = { 'pep', 'pee', 'per', 'pef', 'pee2', 'pec', 'pet', 'peex' };
    static const u32 perfect_nt[] = { 'ntt0', 'ntt1', 'ntt2', 'ntt3', 'ntt4', 'ntt5', 'ntt6', 'ntt7', 'ntt8', 'ntt9' };
    static const u32 perfect_nk[] = { 'ntk0', 'ntk1', 'ntk2', 'ntk3', 'ntk4', 'ntk5', 'ntk6', 'ntk7', 'ntk8', 'ntk9' };
    static const u32 failed[] = { 'fae', 'fal', 'fai', 'faa', 'faf', 'fau', 'fao', 'fay' };
    static const u32 remaintime[] = { 'yoy', 'ree2', 'rem2', 'rei3', 'ret', 'reg', 'ren2', 'rei2', 'ren1', 'rei1', 'rea', 'rem1', 'ree1', 'rer' };
    static const u32 remaintimek[] = { 'rke2', 'rkm2', 'rki3', 'rkt', 'rkg', 'rkn2', 'rki2', 'rkn1', 'rki1', 'rka', 'rkm1', 'rke1', 'rkr' };

    dTerminaterScrn_mem_c* p = (dTerminaterScrn_mem_c*)this;

    for (s32 i = 0; i < dMgtem_perfect_tex; i++) {
        p->mpScrn->search(perfect[i])->hide();
    }

    for (s32 i = 0; i < dMgtem_failed_tex; i++) {
        fopMsgM_setPaneData(&p->mFailedPane[i], p->mpScrn, failed[i]);
        getRotate(&p->mFailedPane[i]);
        p->mPaneD60[i].mUserArea = 0;
        p->mPane1118[i].mUserArea = 0;
    }

    for (s32 i = 0; i < dMgtem_remain_tex + 1; i++) {
        p->mpScrn->search(remaintime[i])->hide();
    }

    for (s32 i = 0; i < dMgtem_remain_tex; i++) {
        p->mpScrn->search(remaintimek[i])->hide();
    }

    for (s32 i = 0; i < 10; i++) {
        p->mpScrn->search(perfect_nt[i])->hide();
        p->mpScrn->search(perfect_nk[i])->hide();
    }

    p->mpScrn->search('dmpe')->hide();
    p->mpScrn->search('num6')->hide();
    p->mpScrn->search('num5')->hide();
    p->mpScrn->search('num4')->hide();
    p->mpScrn->search('num3')->hide();
    p->mpScrn->search('num2')->hide();
    p->mpScrn->search('num1')->hide();
    p->mpScrn->search('nmd2')->hide();
    p->mpScrn->search('nmd1')->hide();
    p->mpScrn->search('nuk6')->hide();
    p->mpScrn->search('nuk5')->hide();
    p->mpScrn->search('nuk4')->hide();
    p->mpScrn->search('nuk3')->hide();
    p->mpScrn->search('nuk2')->hide();
    p->mpScrn->search('nuk1')->hide();
    p->mpScrn->search('ndk2')->hide();
    p->mpScrn->search('ndk1')->hide();
    p->mpScrn->search('rpx')->hide();
    p->mpScrn->search('rpxk')->hide();
    p->mpScrn->search('nt00')->hide();
    p->mpScrn->search('nk00')->hide();
    p->mpScrn->search('dmmy')->hide();
}

/* 8020789C-8020805C       .text setScrnSuccess__26dDlst_TerminaterScrnDraw_cFii */
#if !TARGET_PC
struct dTerminaterScrnS_mem_c {
    /* 0x0000 */ u8 pad_0x0000[0x4];
    /* 0x0004 */ J2DScreen* mpScrn;
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
};
#endif

void dDlst_TerminaterScrnDraw_c::setScrnSuccess(int i_rupee, int i_time) {
    static const u32 perfect[] = { 'pep', 'pee', 'per', 'pef', 'pee2', 'pec', 'pet', 'peex' };
    static const u32 perfect_nt[] = { 'ntt0', 'ntt1', 'ntt2', 'ntt3', 'ntt4', 'ntt5', 'ntt6', 'ntt7', 'ntt8', 'ntt9' };
    static const u32 perfect_nk[] = { 'ntk0', 'ntk1', 'ntk2', 'ntk3', 'ntk4', 'ntk5', 'ntk6', 'ntk7', 'ntk8', 'ntk9' };
    static const u32 failed[] = { 'fae', 'fal', 'fai', 'faa', 'faf', 'fau', 'fao', 'fay' };
    static const u32 yougot[] = { 'yoy', 'yoo1', 'you', 'yog', 'yoo2', 'yot' };
    static const u32 remaintime[] = { 'ree2', 'rem2', 'rei3', 'ret', 'reg', 'ren2', 'rei2', 'ren1', 'rei1', 'rea', 'rem1', 'ree1', 'rer' };
    static const u32 remaintimek[] = { 'rke2', 'rkm2', 'rki3', 'rkt', 'rkg', 'rkn2', 'rki2', 'rkn1', 'rki1', 'rka', 'rkm1', 'rke1', 'rkr' };

    dTerminaterScrnS_mem_c* p = (dTerminaterScrnS_mem_c*)this;

    for (s32 i = 0; i < dMgtem_perfect_tex; i++) {
        fopMsgM_setPaneData(&p->mPerfectPane[i], p->mpScrn->search(perfect[i]));
    }

    for (s32 i = 0; i < dMgtem_failed_tex; i++) {
        p->mpScrn->search(failed[i])->hide();
    }

    for (s32 i = 0; i < dMgtem_yougot_tex; i++) {
        fopMsgM_setPaneData(&p->mYougotPane[i], p->mpScrn, yougot[i]);
    }

    for (s32 i = 0; i < dMgtem_remain_tex; i++) {
        fopMsgM_setPaneData(&p->mRemainPane[i], p->mpScrn, remaintime[i]);
        fopMsgM_setPaneData(&p->mRemainKPane[i], p->mpScrn, remaintimek[i]);
    }

    for (s32 i = 0; i < 10; i++) {
        fopMsgM_setPaneData(&p->mNtPane[i], p->mpScrn->search(perfect_nt[i]));
        fopMsgM_setPaneData(&p->mNkPane[i], p->mpScrn->search(perfect_nk[i]));
    }

    fopMsgM_setPaneData(&p->mDmpePane, p->mpScrn->search('dmpe'));
    fopMsgM_setPaneData(&p->mNumPane[0], p->mpScrn->search('num6'));
    fopMsgM_setPaneData(&p->mNumPane[1], p->mpScrn->search('num5'));
    fopMsgM_setPaneData(&p->mNumPane[2], p->mpScrn->search('nmd2'));
    fopMsgM_setPaneData(&p->mNumPane[3], p->mpScrn->search('num4'));
    fopMsgM_setPaneData(&p->mNumPane[4], p->mpScrn->search('num3'));
    fopMsgM_setPaneData(&p->mNumPane[5], p->mpScrn->search('nmd1'));
    fopMsgM_setPaneData(&p->mNumPane[6], p->mpScrn->search('num2'));
    fopMsgM_setPaneData(&p->mNumPane[7], p->mpScrn->search('num1'));
    fopMsgM_setPaneData(&p->mNumKPane[0], p->mpScrn->search('nuk6'));
    fopMsgM_setPaneData(&p->mNumKPane[1], p->mpScrn->search('nuk5'));
    fopMsgM_setPaneData(&p->mNumKPane[2], p->mpScrn->search('ndk2'));
    fopMsgM_setPaneData(&p->mNumKPane[3], p->mpScrn->search('nuk4'));
    fopMsgM_setPaneData(&p->mNumKPane[4], p->mpScrn->search('nuk3'));
    fopMsgM_setPaneData(&p->mNumKPane[5], p->mpScrn->search('ndk1'));
    fopMsgM_setPaneData(&p->mNumKPane[6], p->mpScrn->search('nuk2'));
    fopMsgM_setPaneData(&p->mNumKPane[7], p->mpScrn->search('nuk1'));
    fopMsgM_setPaneData(&p->mRpxPane, p->mpScrn->search('rpx'));
    fopMsgM_setPaneData(&p->mRpxkPane, p->mpScrn->search('rpxk'));
    fopMsgM_setPaneData(&p->mNt00Pane, p->mpScrn->search('nt00'));
    fopMsgM_setPaneData(&p->mNk00Pane, p->mpScrn->search('nk00'));
    fopMsgM_setPaneData(&p->mDmmyPane, p->mpScrn->search('dmmy'));

    p->mRupeePane[0] = p->mpScrn->search('rp3');
    p->mRupeePane[1] = p->mpScrn->search('rp2');
    p->mRupeePane[2] = p->mpScrn->search('rp1');
    p->mRupeePane[3] = p->mpScrn->search('rpk3');
    p->mRupeePane[4] = p->mpScrn->search('rpk2');
    p->mRupeePane[5] = p->mpScrn->search('rpk1');

    int sec = i_time / 100;
    i_time -= sec * 100;
    int min = sec / 60;
    sec -= min * 60;

    changeNumberTexture(p->mNumPane[7].pane, min / 10);
    changeNumberTexture(p->mNumPane[6].pane, min % 10);
    changeNumberTexture(p->mNumPane[4].pane, sec / 10);
    changeNumberTexture(p->mNumPane[3].pane, sec % 10);
    changeNumberTexture(p->mNumPane[1].pane, i_time / 10);
    changeNumberTexture(p->mNumPane[0].pane, i_time % 10);
    changeNumberTexture(p->mNumKPane[7].pane, min / 10);
    changeNumberTexture(p->mNumKPane[6].pane, min % 10);
    changeNumberTexture(p->mNumKPane[4].pane, sec / 10);
    changeNumberTexture(p->mNumKPane[3].pane, sec % 10);
    changeNumberTexture(p->mNumKPane[1].pane, i_time / 10);
    changeNumberTexture(p->mNumKPane[0].pane, i_time % 10);

    int rupee1 = i_rupee % 10;
    int rupee10 = i_rupee % 100 / 10;
    int rupee100 = i_rupee / 100;

    changeNumberTexture(p->mRupeePane[0], rupee1);
    changeNumberTexture(p->mRupeePane[1], rupee10);
    changeNumberTexture(p->mRupeePane[2], rupee100);
    changeNumberTexture(p->mRupeePane[3], rupee1);
    changeNumberTexture(p->mRupeePane[4], rupee10);
    changeNumberTexture(p->mRupeePane[5], rupee100);
}

/* 8020805C-802080B0       .text changeNumberTexture__26dDlst_TerminaterScrnDraw_cFP7J2DPanei */
void dDlst_TerminaterScrnDraw_c::changeNumberTexture(J2DPane* i_pane, int i_num) {
    if (i_num < 0 || i_num >= 10) {
        i_num = 0;
    }

    ((J2DPicture*)i_pane)->changeTexture(getNumber(i_num), 0);
}

/* 802080B0-802080C4       .text getNumber__26dDlst_TerminaterScrnDraw_cFi */
const char* dDlst_TerminaterScrnDraw_c::getNumber(int i_num) {
    static const char* number[] = {
        "rupy_num_00.bti",
        "rupy_num_01.bti",
        "rupy_num_02.bti",
        "rupy_num_03.bti",
        "rupy_num_04.bti",
        "rupy_num_05.bti",
        "rupy_num_06.bti",
        "rupy_num_07.bti",
        "rupy_num_08.bti",
        "rupy_num_09.bti",
    };

    return number[i_num];
}

/* 802080C4-80208120       .text setAlphaFailed__26dDlst_TerminaterScrnDraw_cFv */
void dDlst_TerminaterScrnDraw_c::setAlphaFailed() {
    dTerminaterScrn_mem_c* p = (dTerminaterScrn_mem_c*)this;

    for (s32 i = 0; i < dMgtem_failed_tex; i++) {
        fopMsgM_setAlpha(&p->mFailedPane[i]);
    }
}

/* 80208120-802081F8       .text setAlphaSuccess__26dDlst_TerminaterScrnDraw_cFv */
void dDlst_TerminaterScrnDraw_c::setAlphaSuccess() {
    dTerminaterScrnS_mem_c* p = (dTerminaterScrnS_mem_c*)this;

    fopMsgM_setAlpha(p->mPerfectPane);
    fopMsgM_setAlpha(p->mNtPane);
    fopMsgM_setAlpha(p->mNkPane);
    fopMsgM_setAlpha(&p->mDmpePane);
    fopMsgM_setAlpha(p->mYougotPane);

    for (s32 i = 0; i < dMgtem_remain_tex; i++) {
        fopMsgM_setAlpha(&p->mRemainPane[i]);
        fopMsgM_setAlpha(&p->mRemainKPane[i]);
    }

    for (s32 i = 0; i < 8; i++) {
        fopMsgM_setAlpha(&p->mNumPane[i]);
        fopMsgM_setAlpha(&p->mNumKPane[i]);
    }

    fopMsgM_setAlpha(&p->mRpxPane);
    fopMsgM_setAlpha(&p->mRpxkPane);
    fopMsgM_setAlpha(&p->mNt00Pane);
    fopMsgM_setAlpha(&p->mNk00Pane);
}

/* 802081F8-80208230       .text getRotate__26dDlst_TerminaterScrnDraw_cFP18fopMsgM_pane_class */
void dDlst_TerminaterScrnDraw_c::getRotate(fopMsgM_pane_class* i_pane) {
    f32 temp = ((J2DPicture*)i_pane->pane)->getRotate();
    if (temp > 180.0f) {
        temp -= 360.0f;
    }

    i_pane->mUserArea = temp;
}

/* 80208230-802082D0       .text setRotate__26dDlst_TerminaterScrnDraw_cFP18fopMsgM_pane_classf */
void dDlst_TerminaterScrnDraw_c::setRotate(fopMsgM_pane_class* i_pane, f32 i_rot) {
    i_pane->pane->rotate((s32)(i_pane->mSize.x / 2), (s32)(i_pane->mSize.y / 2), ROTATE_Z, i_rot);
}

/* 802082D0-80208324       .text setScale__26dDlst_TerminaterScrnDraw_cFP18fopMsgM_pane_classf */
void dDlst_TerminaterScrnDraw_c::setScale(fopMsgM_pane_class* i_pane, f32 i_scale) {
    i_pane->mPosCenter.x = i_pane->mPosCenterOrig.x * i_scale;
    i_pane->mPosCenter.y = i_pane->mPosCenterOrig.y * i_scale;
    i_pane->mSize.x = i_pane->mSizeOrig.x * i_scale;
    i_pane->mSize.y = i_pane->mSizeOrig.y * i_scale;
    fopMsgM_cposMove(i_pane);
}

/* 80208324-80208440       .text animeF1__26dDlst_TerminaterScrnDraw_cFi */
bool dDlst_TerminaterScrnDraw_c::animeF1(int i_no) {
    dTerminaterScrn_mem_c* p = (dTerminaterScrn_mem_c*)this;

    bool ret = false;

    if (p->mPaneD60[i_no].mUserArea < 26) {
        p->mPaneD60[i_no].mUserArea++;

        f32 t = acc(26, p->mPaneD60[i_no].mUserArea, 0);
        f32 r = 720.0f * (1.0f - t) + p->mFailedPane[i_no].mUserArea;

        fopMsgM_paneTrans(&p->mFailedPane[i_no], -150.0f * (1.0f - t), 0.0f);
        setRotate(&p->mFailedPane[i_no], r);
        fopMsgM_setNowAlpha(&p->mFailedPane[i_no], t);
    } else {
        ret = true;
    }

    return ret;
}

/* 80208440-80208E38       .text animeF2__26dDlst_TerminaterScrnDraw_cFi */
bool dDlst_TerminaterScrnDraw_c::animeF2(int i_no) {
    static const s16 animeFrame[] = { 26, 29, 31, 34, 37, 40, 43 };
    static const s16 transX[] = { -150, 10, -12, -28, -28, -15, 2, 0 };
    static const s16 transY[] = { 0, 0, -60, 0, 5, 5, 14, 0 };
    static const s16 rot[] = { 0, 10, 0, 0, -20, -10, 0, 10 };

    bool ret = false;
    f32 t, x, y, r;
    dTerminaterScrn_mem_c* p = (dTerminaterScrn_mem_c*)this;

    if (p->mPaneD60[i_no].mUserArea < animeFrame[6]) {
        p->mPaneD60[i_no].mUserArea++;

        if (p->mPaneD60[i_no].mUserArea <= animeFrame[0]) {
            t = acc(animeFrame[0], p->mPaneD60[i_no].mUserArea, 0);
            x = transX[1] + (1.0f - t) * (transX[0] - transX[1]);
            y = transY[1] + (1.0f - t) * (transY[0] - transY[1]);
            r = 710.0f * (1.0f - t) + rot[1];
            fopMsgM_paneTrans(&p->mFailedPane[i_no], x, y);
            setRotate(&p->mFailedPane[i_no], r);
            fopMsgM_setNowAlpha(&p->mFailedPane[i_no], t);
        } else if (p->mPaneD60[i_no].mUserArea <= animeFrame[1]) {
            t = acc(animeFrame[1], p->mPaneD60[i_no].mUserArea, animeFrame[0]);
            x = transX[2] + (1.0f - t) * (transX[1] - transX[2]);
            y = transY[2] + (1.0f - t) * (transY[1] - transY[2]);
            r = rot[2] + (1.0f - t) * (rot[1] - rot[2]);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], x, y);
            setRotate(&p->mFailedPane[i_no], r);
        } else if (p->mPaneD60[i_no].mUserArea <= animeFrame[2]) {
            t = acc(animeFrame[2], p->mPaneD60[i_no].mUserArea, animeFrame[1]);
            x = transX[3] + (1.0f - t) * (transX[2] - transX[3]);
            y = transY[3] + (1.0f - t) * (transY[2] - transY[3]);
            r = rot[3] + (1.0f - t) * (rot[2] - rot[3]);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], x, y);
            setRotate(&p->mFailedPane[i_no], r);
        } else if (p->mPaneD60[i_no].mUserArea <= animeFrame[3]) {
            t = acc(animeFrame[3], p->mPaneD60[i_no].mUserArea, animeFrame[2]);
            x = transX[4] + (1.0f - t) * (transX[3] - transX[4]);
            y = transY[4] + (1.0f - t) * (transY[3] - transY[4]);
            r = rot[4] + (1.0f - t) * (rot[3] - rot[4]);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], x, y);
            setRotate(&p->mFailedPane[i_no], r);
        } else if (p->mPaneD60[i_no].mUserArea <= animeFrame[4]) {
            t = acc(animeFrame[4], p->mPaneD60[i_no].mUserArea, animeFrame[3]);
            x = transX[5] + (1.0f - t) * (transX[4] - transX[5]);
            y = transY[5] + (1.0f - t) * (transY[4] - transY[5]);
            r = rot[5] + (1.0f - t) * (rot[4] - rot[5]);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], x, y);
            setRotate(&p->mFailedPane[i_no], r);
        } else if (p->mPaneD60[i_no].mUserArea <= animeFrame[5]) {
            t = acc(animeFrame[5], p->mPaneD60[i_no].mUserArea, animeFrame[4]);
            x = transX[6] + (1.0f - t) * (transX[5] - transX[6]);
            y = transY[6] + (1.0f - t) * (transY[5] - transY[6]);
            r = rot[6] + (1.0f - t) * (rot[5] - rot[6]);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], x, y);
            setRotate(&p->mFailedPane[i_no], r);
        } else if (p->mPaneD60[i_no].mUserArea <= animeFrame[6]) {
            t = acc(animeFrame[6], p->mPaneD60[i_no].mUserArea, animeFrame[5]);
            x = transX[7] + (1.0f - t) * (transX[6] - transX[7]);
            y = transY[7] + (1.0f - t) * (transY[6] - transY[7]);
            r = rot[7] + (1.0f - t) * (rot[6] - rot[7]);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], x, y);
            setRotate(&p->mFailedPane[i_no], r);
        }
    } else {
        ret = true;
    }

    return ret;
}

#include "SSystem/SComponent/c_math.h"

/* 80208E38-802091D8       .text animeF3__26dDlst_TerminaterScrnDraw_cFi */
bool dDlst_TerminaterScrnDraw_c::animeF3(int i_no) {
    static const s16 animeFrame[] = { 5, 10 };
    static const s16 transY[] = { 0, -25, 150 };
    static const s16 rot[] = { 40, 30, 20, -20, -30, -40 };

    bool ret = false;
    dTerminaterScrn_mem_c* p = (dTerminaterScrn_mem_c*)this;

    if (p->mPane1118[i_no].mUserArea == 0) {
        int idx = (int)cM_rndF(6.0f);

        if (idx > 5) {
            idx = 5;
        }

        p->mPaneD60[i_no].mUserArea = rot[idx];
    }

    if (p->mPane1118[i_no].mUserArea < animeFrame[1]) {
        p->mPane1118[i_no].mUserArea++;

        f32 t, y, r;
        if (p->mPane1118[i_no].mUserArea <= animeFrame[0]) {
            t = acc(animeFrame[0], p->mPane1118[i_no].mUserArea, 0);
            y = transY[1] + (1.0f - t) * (transY[0] - transY[1]);
            r = p->mFailedPane[i_no].mUserArea + p->mPaneD60[i_no].mUserArea * acc(animeFrame[1], p->mPane1118[i_no].mUserArea, 0);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], 0.0f, y);
            setRotate(&p->mFailedPane[i_no], r);
        } else {
            t = acc(animeFrame[1], p->mPane1118[i_no].mUserArea, animeFrame[0]);
            y = transY[2] + (1.0f - t) * (transY[1] - transY[2]);
            r = p->mFailedPane[i_no].mUserArea + p->mPaneD60[i_no].mUserArea * acc(animeFrame[1], p->mPane1118[i_no].mUserArea, 0);
            fopMsgM_paneTrans(&p->mFailedPane[i_no], 0.0f, y);
            fopMsgM_setNowAlpha(&p->mFailedPane[i_no], 1.0f - t);
            setRotate(&p->mFailedPane[i_no], r);
        }
    } else {
        ret = true;
    }

    return ret;
}

/* 802091D8-80209420       .text animeS1__26dDlst_TerminaterScrnDraw_cFv */
bool dDlst_TerminaterScrnDraw_c::animeS1() {
    static const s16 animeFrame[] = { 8, 10 };

    bool ret = false;
    dTerminaterScrnS_mem_c* p = (dTerminaterScrnS_mem_c*)this;

    f32 pivotX = (p->mYougotPane[0].mPosTopLeftOrig.x + p->mYougotPane[5].mPosTopLeftOrig.x +
                  p->mYougotPane[5].mSizeOrig.x + p->mYougotPane[0].mPosTopLeftOrig.x) / 2.0f;

    if (p->mYougotPane[0].mUserArea < animeFrame[1]) {
        p->mYougotPane[0].mUserArea++;
    } else {
        ret = true;
    }

    f32 t;
    f32 alpha;
    f32 scale;

    if (p->mYougotPane[0].mUserArea <= animeFrame[0]) {
        alpha = 0.0f;
        t = acc(animeFrame[0], p->mYougotPane[0].mUserArea, 0);
        scale = 0.3f + (1.2f - 0.3f) * t;
    } else {
        t = acc(animeFrame[1], p->mYougotPane[0].mUserArea, animeFrame[0]);
        alpha = t;
        scale = 1.2f + (1.0f - 1.2f) * t;
    }

    for (s32 i = 0; i < dMgtem_yougot_tex; i++) {
        if (i != 0) {
            p->mYougotPane[i].mPosCenter.x = p->mYougotPane[i].mPosCenterOrig.x * scale;
            p->mYougotPane[i].mPosCenter.y = p->mYougotPane[i].mPosCenterOrig.y * scale;
        } else {
            p->mYougotPane[i].mPosCenter.x = pivotX - scale * (pivotX - p->mYougotPane[i].mPosCenterOrig.x);
            p->mYougotPane[i].mPosCenter.y = p->mYougotPane[i].mPosCenterOrig.y;
        }

        p->mYougotPane[i].mSize.x = p->mYougotPane[i].mSizeOrig.x * scale;
        p->mYougotPane[i].mSize.y = p->mYougotPane[i].mSizeOrig.y * scale;

        fopMsgM_cposMove(&p->mYougotPane[i]);
        fopMsgM_setNowAlpha(&p->mYougotPane[i], alpha);
    }

    return ret;
}

/* 80209420-802094AC       .text animeS2__26dDlst_TerminaterScrnDraw_cFP18fopMsgM_pane_class */
bool dDlst_TerminaterScrnDraw_c::animeS2(fopMsgM_pane_class* pane) {
    bool ret = false;
    pane->mUserArea += 1;
    if (!(pane->mUserArea > 7)) {
        fopMsgM_setNowAlpha(pane, acc(7, pane->mUserArea, 0));
    } else {
        ret = true;
    }
    return ret;
}

#include "m_Do/m_Do_audio.h"

/* 802094AC-80209AAC       .text animeS3__26dDlst_TerminaterScrnDraw_cFv */
bool dDlst_TerminaterScrnDraw_c::animeS3() {
    static const s16 animeFrame[] = { 5, 7, 9, 11, 13 };
    static const s16 transY[] = { -110, 0, -30, 10, 15, 0 };
    static const s16 rot[] = { 0, 0, 5, -5, 0, 0 };

    bool ret = false;
    f32 t, y, r;
    dTerminaterScrnS_mem_c* p = (dTerminaterScrnS_mem_c*)this;

    if (p->mDmmyPane.mUserArea < animeFrame[4]) {
        p->mDmmyPane.mUserArea++;

        if (p->mDmmyPane.mUserArea <= animeFrame[0]) {
            t = acc(animeFrame[0], p->mDmmyPane.mUserArea, 0);
            y = transY[1] + (1.0f - t) * (transY[0] - transY[1]);
            fopMsgM_paneTrans(&p->mDmmyPane, 0.0f, y);
            fopMsgM_setNowAlpha(&p->mRpxPane, t);
            fopMsgM_setNowAlpha(&p->mRpxkPane, t);
            fopMsgM_setNowAlpha(&p->mNt00Pane, t);
            fopMsgM_setNowAlpha(&p->mNk00Pane, t);

            if (p->mDmmyPane.mUserArea == animeFrame[0]) {
                mDoAud_seStart(JA_SE_SGAME_DISP_LUPY);
            }
        } else if (p->mDmmyPane.mUserArea <= animeFrame[1]) {
            t = acc(animeFrame[1], p->mDmmyPane.mUserArea, animeFrame[0]);
            y = transY[2] + (1.0f - t) * (transY[1] - transY[2]);
            r = rot[2] + (1.0f - t) * (rot[1] - rot[2]);
            fopMsgM_paneTrans(&p->mDmmyPane, 0.0f, y);
            p->mDmmyPane.pane->rotate(0.0f, 0.0f, ROTATE_Z, r);
        } else if (p->mDmmyPane.mUserArea <= animeFrame[2]) {
            t = acc(animeFrame[2], p->mDmmyPane.mUserArea, animeFrame[1]);
            y = transY[3] + (1.0f - t) * (transY[2] - transY[3]);
            r = rot[3] + (1.0f - t) * (rot[2] - rot[3]);
            fopMsgM_paneTrans(&p->mDmmyPane, 0.0f, y);
            p->mDmmyPane.pane->rotate(0.0f, 0.0f, ROTATE_Z, r);
        } else if (p->mDmmyPane.mUserArea <= animeFrame[3]) {
            t = acc(animeFrame[3], p->mDmmyPane.mUserArea, animeFrame[2]);
            y = transY[4] + (1.0f - t) * (transY[3] - transY[4]);
            r = rot[4] + (1.0f - t) * (rot[3] - rot[4]);
            fopMsgM_paneTrans(&p->mDmmyPane, 0.0f, y);
            p->mDmmyPane.pane->rotate(0.0f, 0.0f, ROTATE_Z, r);
        } else if (p->mDmmyPane.mUserArea <= animeFrame[4]) {
            t = acc(animeFrame[4], p->mDmmyPane.mUserArea, animeFrame[3]);
            y = transY[5] + (1.0f - t) * (transY[4] - transY[5]);
            fopMsgM_paneTrans(&p->mDmmyPane, 0.0f, y);
        }
    } else {
        ret = true;
    }

    return ret;
}

/* 80209AAC-80209B64       .text animeFailed1__26dDlst_TerminaterScrnDraw_cFv */
bool dDlst_TerminaterScrnDraw_c::animeFailed1() {
    s32 delay, i;
    for (i = 0, delay = 0; i < dMgtem_failed_tex; i++, delay += 3) {
        if (i < dMgtem_failed_tex - 1) {
            if (field_0x1980 >= delay && field_0x1980 < delay + 26) {
                animeF1(i);
            }
        } else {
            if (field_0x1980 >= delay && field_0x1980 < delay + 44) {
                animeF2(i);
            }
        }
    }
    field_0x1980++;
    return false;
}

/* 80209B64-80209BEC       .text animeFailed2__26dDlst_TerminaterScrnDraw_cFv */
bool dDlst_TerminaterScrnDraw_c::animeFailed2() {
    s32 delay, i;
    for (i = 0, delay = 0; i < dMgtem_failed_tex; i++, delay += 3) {
        if (field_0x1982 >= delay && field_0x1982 < delay + 10) {
            animeF3(dMgtem_failed_tex - 1 - i);
        }
    }

    field_0x1982++;
    return false;
}

/* 80209BEC-80209D6C       .text animeSuccess__26dDlst_TerminaterScrnDraw_cFv */
bool dDlst_TerminaterScrnDraw_c::animeSuccess() {
    dTerminaterScrnS_mem_c* p = (dTerminaterScrnS_mem_c*)this;

    if (field_0x1982 <= 10) {
        if (field_0x1982 == 0) {
            mDoAud_seStart(JA_SE_SGAME_YOU_GOT);
        }
        animeS1();
    }

    if (field_0x1982 <= 19) {
        for (s32 i = 0; i < dMgtem_remain_tex; i++) {
            if (field_0x1982 >= i && field_0x1982 < i + 7) {
                animeS2(&p->mRemainPane[dMgtem_remain_tex - 1 - i]);
                animeS2(&p->mRemainKPane[dMgtem_remain_tex - 1 - i]);
            }
        }
    }

    if (field_0x1982 >= 2 && field_0x1982 <= 19) {
        for (s32 i = 0; i < 8; i++) {
            if (field_0x1982 >= i + 2 && field_0x1982 < i + 9) {
                animeS2(&p->mNumPane[7 - i]);
                animeS2(&p->mNumKPane[7 - i]);
            }
        }
    }

    if (field_0x1982 >= 19 && field_0x1982 <= 32) {
        animeS3();
    }

    field_0x1982++;
    return false;
}

#include "JSystem/J2DGraph/J2DPicture.h"
#include "JAZelAudio/JAIZelBasic.h"
#include "d/d_meter.h"
#include "JSystem/J2DGraph/J2DOrthoGraph.h"

#if !TARGET_PC
struct dTerminaterScrnP_mem_c {
    /* 0x0000 */ u8 pad_0x0000[0x8];
    /* 0x0008 */ fopMsgM_pane_class mPerfectPane[10];
    /* 0x0238 */ fopMsgM_pane_class mNtPane[10];
    /* 0x0468 */ fopMsgM_pane_class mNkPane[10];
    /* 0x0698 */ fopMsgM_pane_class mDmpePane;
    /* 0x06D0 */ u8 pad_0x06D0[0x1982 - 0x6D0];
    /* 0x1982 */ s16 field_0x1982;
    /* 0x1984 */ s16 field_0x1984;
};
#endif

/* 80209D6C-8020A4D8       .text animePerfect__26dDlst_TerminaterScrnDraw_cFv */
bool dDlst_TerminaterScrnDraw_c::animePerfect() {
    static const s16 rot[] = { 0, 7, 0, 5, 2, 7 };

    dTerminaterScrnP_mem_c* p = (dTerminaterScrnP_mem_c*)this;
    JUtility::TColor color[8];

    f32 t, r, s, scale;
    s16 frame[6];
    s16 addFrame = g_menuHIO.field_0xae;
    frame[0] = g_menuHIO.field_0xa4;
    frame[1] = frame[0] + g_menuHIO.field_0xa6;
    frame[2] = frame[1] + g_menuHIO.field_0xa8;
    frame[3] = frame[2] + g_menuHIO.field_0xaa;
    frame[4] = frame[3] + g_menuHIO.field_0xac;
    frame[5] = frame[4] + addFrame * 2;

    if (p->field_0x1982 <= g_menuHIO.field_0xb0 + 32) {
        return false;
    }

    if (p->field_0x1984 == 0) {
        JAIZelBasic::getInterface()->seStart(JA_SE_SGAME_PERFECT);
    }

    p->field_0x1984++;

    if (p->field_0x1984 <= frame[0]) {
        t = acc(frame[0], p->field_0x1984, 0);
        r = rot[0] + t * (rot[1] - rot[0]);
        s = acc(frame[0], frame[0] - p->field_0x1984, 0);
        scale = 1.0f + (g_menuHIO.field_0x8e / 100.0f - 1.0f) * s;

        fopMsgM_setNowAlpha(&p->mPerfectPane[0], t);
        fopMsgM_setNowAlpha(&p->mNtPane[0], t);
        fopMsgM_setNowAlpha(&p->mNkPane[0], t);
        setRotate(&p->mDmpePane, r);

        for (s32 i = 0; i < dMgtem_perfect_tex; i++) {
            setScale(&p->mPerfectPane[i], scale);
        }

        for (s32 i = 0; i < 10; i++) {
            setScale(&p->mNtPane[i], scale);
            setScale(&p->mNkPane[i], scale);
        }
    } else if (p->field_0x1984 <= frame[1]) {
        t = acc(frame[1], p->field_0x1984, frame[0]);
        r = rot[1] + t * (rot[2] - rot[1]);
        setRotate(&p->mDmpePane, r);
    } else if (p->field_0x1984 <= frame[2]) {
        t = acc(frame[2], p->field_0x1984, frame[1]);
        r = rot[2] + t * (rot[3] - rot[2]);
        setRotate(&p->mDmpePane, r);
    } else if (p->field_0x1984 <= frame[3]) {
        t = acc(frame[3], p->field_0x1984, frame[2]);
        r = rot[3] + t * (rot[4] - rot[3]);
        setRotate(&p->mDmpePane, r);
    } else if (p->field_0x1984 <= frame[4]) {
        t = acc(frame[4], p->field_0x1984, frame[3]);
        r = rot[4] + t * (rot[5] - rot[4]);
        setRotate(&p->mDmpePane, r);
    } else {
        if (p->field_0x1984 >= frame[5]) {
            p->field_0x1984 = frame[4];
        }

        u8 rev = 0;
        f32 hue = 240.0f + -140.0f * ((p->field_0x1984 - frame[4]) / (f32)addFrame);

        if (hue > 240.0f) {
            hue = 480.0f - hue;
            rev = 1;
        }

        for (s32 i = 0; i < dMgtem_perfect_tex; i++) {
            f32 v;

            if (rev) {
                v = hue - 20.0f * i;
            } else {
                v = hue + 20.0f * i;
            }

            if (v > 240.0f) {
                v = 480.0f - v;
            }

            if (v < 100.0f) {
                v = 200.0f - v;
            }

            color[i].r = 255;
            color[i].g = v;
            color[i].b = 0;
            color[i].a = 255;
            ((J2DPicture*)p->mPerfectPane[i].pane)->setWhite(color[i]);
        }
    }

    return false;
}

/* 8020A4D8-8020A53C       .text draw__26dDlst_TerminaterScrnDraw_cFv */
void dDlst_TerminaterScrnDraw_c::draw() {
    J2DOrthoGraph* graf = dComIfGp_getCurrentGrafPort();
    graf->setPort();
    mpScrn->draw(0.0f, 0.0f, graf);
}

/* 8020A53C-8020A55C       .text dMinigame_Terminater_Draw__FP22dMinigame_Terminater_c */
static void dMinigame_Terminater_Draw(dMinigame_Terminater_c* i_this) {
    ((dMinigame_Terminater_c*)i_this)->_draw();
}

/* 8020A55C-8020A57C       .text dMinigame_Terminater_Execute__FP22dMinigame_Terminater_c */
static BOOL dMinigame_Terminater_Execute(dMinigame_Terminater_c* i_this) {
    return ((dMinigame_Terminater_c*)i_this)->_execute();
}

/* 8020A57C-8020A584       .text dMinigame_Terminater_IsDelete__FP22dMinigame_Terminater_c */
static BOOL dMinigame_Terminater_IsDelete(dMinigame_Terminater_c*) {
    return TRUE;
}

/* 8020A584-8020A5A4       .text dMinigame_Terminater_Delete__FP22dMinigame_Terminater_c */
static BOOL dMinigame_Terminater_Delete(dMinigame_Terminater_c* i_this) {
    return ((dMinigame_Terminater_c*)i_this)->_delete();
}

/* 8020A5A4-8020A5C4       .text dMinigame_Terminater_Create__FP9msg_class */
static cPhs_State dMinigame_Terminater_Create(msg_class* i_this) {
    return ((dMinigame_Terminater_c*)i_this)->_create();
}

static msg_method_class l_dMinigame_Terminater_Method = {
    (process_method_func)dMinigame_Terminater_Create,
    (process_method_func)dMinigame_Terminater_Delete,
    (process_method_func)dMinigame_Terminater_Execute,
    (process_method_func)dMinigame_Terminater_IsDelete,
    (process_method_func)dMinigame_Terminater_Draw,
};

msg_process_profile_definition g_profile_MINIGAME_TERMINATER = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 12,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_MINIGAME_TERMINATER_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(dMinigame_Terminater_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopMsg_Method,
    /* Draw Prio    */ fpcDwPi_MINIGAME_TERMINATER_e,
    /* Msg SubMtd   */ &l_dMinigame_Terminater_Method,
};
