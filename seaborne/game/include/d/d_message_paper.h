#ifndef D_MESSAGE_PAPER_H
#define D_MESSAGE_PAPER_H

#include "dolphin/types.h"
#include "f_op/f_op_msg.h"

#if VERSION < VERSION_PAL
#include "f_op/f_op_msg_mng.h"
#include "d/d_drawlist.h"
#include "m_Do/m_Do_ext.h"

struct sub_msg3_class : public msg_class {
    /* 0x0FC */ JKRExpHeap* Heap;
    /* 0x100 */ JMSMesgEntry_c mesgEntry;
    /* 0x118 */ fopMsgM_msgDataProc_c msgDataProc[3];
    /* 0x8F8 */ fopMsgM_msgGet_c msgGet;
    /* 0x908 */ mesg_header* head_p;
    /* 0x90C */ fopMsgM_pane_class text_pane[3];
    /* 0x9B4 */ fopMsgM_pane_class textSdw_pane[3];
    /* 0xA5C */ fopMsgM_pane_class ruby_pane[3];
    /* 0xB04 */ fopMsgM_pane_class rubySdw_pane[3];
    /* 0xBAC */ fopMsgM_pane_class field_0xbac;
    /* 0xBE4 */ fopMsgM_pane_class field_0xbe4;
    /* 0xC1C */ fopMsgM_pane_class field_0xc1c;
    /* 0xC54 */ fopMsgM_pane_class field_0xc54;
    /* 0xC8C */ fopMsgM_pane_class field_0xc8c;
    /* 0xCC4 */ fopMsgM_pane_class field_0xcc4;
    /* 0xCFC */ fopMsgM_pane_class field_0xcfc[3];
    /* 0xDA4 */ fopMsgM_pane_class field_0xda4[3];
    /* 0xE4C */ JUTFont* mx;
    /* 0xE50 */ JUTFont* rx;
    /* 0xE54 */ ResTIMG* Tex[2];
    /* 0xE5C */ char* message;
    /* 0xE60 */ char* output_text[3];
    /* 0xE6C */ char* output_ruby[3];
    /* 0xE78 */ char* output_textSdw[3];
    /* 0xE84 */ char* output_rubySdw[3];
    /* 0xE90 */ f32 field_0xe90;
    /* 0xE94 */ int field_0xe94;
    /* 0xE98 */ u8 field_0xe98;
    /* 0xE99 */ u8 field_0xe99;
    /* 0xE9A */ u8 field_0xe9a;
    /* 0xE9C */ u32 mesgNumber;
    /* 0xEA0 */ u16 colorNo;
    /* 0xEA4 */ f32 field_0xea4;
    /* 0xEA8 */ f32 field_0xea8;
    /* 0xEAC */ int field_0xeac;
    /* 0xEB0 */ int field_0xeb0;
    /* 0xEB4 */ int field_0xeb4;
    /* 0xEB8 */ int field_0xeb8;
    /* 0xEBC */ u8 field_0xebc;
    /* 0xEC0 */ int field_0xec0;
    /* 0xEC4 */ int field_0xec4;
    /* 0xEC8 */ int field_0xec8[3];
    /* 0xED4 */ u8 field_0xed4[0xED9 - 0xED4];
    /* 0xED9 */ u8 field_0xed9;
    /* 0xEDA */ u8 field_0xeda;
    /* 0xEDB */ u8 field_0xedb[4][3];
};

class dmsg3_3d_c {
public:
    dmsg3_3d_c();
    virtual ~dmsg3_3d_c();
    void setDummyTexture();
    void set_mtx();
    void exec();
    void draw();

    /* 0x04 */ u8* mpModelBuf;
    /* 0x08 */ u8 field_0x8[0xC - 0x8];
    /* 0x0C */ u8* mpBckBuf;
    /* 0x10 */ cXyz mScale;
    /* 0x1C */ u8 field_0x1c[0x28 - 0x1C];
    /* 0x28 */ csXyz mAngle;
    /* 0x30 */ J3DModel* mModel;
    /* 0x34 */ mDoExt_bckAnm mBck;
    /* 0x44 */ u8 field_0x44[0x48 - 0x44];
    /* 0x48 */ JKRSolidHeap* mpHeap;
};

class dDlst_2DMSG3_c : public dDlst_base_c {
public:
    virtual ~dDlst_2DMSG3_c() {}

    void setActorP(sub_msg3_class* i_actorp) { actorP = i_actorp; }

    virtual void draw();
    void outFontDraw();

    /* 0x4 */ sub_msg3_class* actorP;
};
#endif

#if VERSION == VERSION_PAL
struct dMessage_Paper_c : public msg_class {
    /* 0x0FC */ u8 m0FC[0x104 - 0x0FC];
};
#endif

#endif /* D_MESSAGE_PAPER_H */
