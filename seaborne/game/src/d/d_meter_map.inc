// Compiled ahead of the rest of d_meter.cpp: these land in their own .text section, and their
// float literals lead .sdata2.

class dMeter_map_HIO_c : public JORReflexible {
public:
    dMeter_map_HIO_c();
    virtual ~dMeter_map_HIO_c() {}

    void genMessage(JORMContext* ctx) { UNUSED(ctx); }

    /* 0x04 */ s8 mNo;
    /* 0x05 */ u8 field_0x5;
    /* 0x06 */ u8 field_0x6;
    /* 0x07 */ u8 field_0x7;
    /* 0x08 */ s16 field_0x8;
    /* 0x0A */ s16 field_0xa;
    /* 0x0C */ s16 field_0xc;
    /* 0x10 */ f32 field_0x10;
    /* 0x14 */ s16 field_0x14;
    /* 0x16 */ s16 field_0x16;
    /* 0x18 */ u8 field_0x18[0x20 - 0x18];
};

class mapCtrlDisp_c {
public:
    void initMapCtrlDisp();
    void moveMapCtrlDisp();

    void Create() {}
    void Delete() {}
    void getAlpha() {}
    void getHideOffsetX() {}
    void getShowOffsetX() {}
    void hideSEOff() {}
    void hideSEOn() {}
    void isMode() {}
    void setAlpha(u8) {}
    void setHide() {}
    void showSEOff() {}
    void showSEOn() {}

    /* 0x00 */ u8 field_0x0;
    /* 0x01 */ u8 field_0x1;
    /* 0x02 */ u8 field_0x2;
    /* 0x04 */ s16 field_0x4;
};

extern dMeter_map_HIO_c g_meter_mapHIO;

/* 80205990-802059E0       .text __ct__16dMeter_map_HIO_cFv */
dMeter_map_HIO_c::dMeter_map_HIO_c() {
    field_0x8 = 0x23;
    field_0xa = 0x152;
    field_0xc = -0xb4;
    field_0x14 = 0x24e;
    field_0x16 = 0x96;
    field_0x6 = 200;
    field_0x7 = 1;
    field_0x10 = 0.8f;
}

/* 802059E0-80205A44       .text initMapCtrlDisp__13mapCtrlDisp_cFv */
void mapCtrlDisp_c::initMapCtrlDisp() {
    dMap_setMapAlpha(0);
    dMap_setIconFreeAlpha(0);
    dMap_setMapDispMode(1);
    dMap_setMapDispPosLeftUpX(g_meter_mapHIO.field_0x8);
    dMap_setMapDispPosLeftUpY(g_meter_mapHIO.field_0xa);
    dMap_setIconFreePosX(g_meter_mapHIO.field_0x14);
    dMap_setIconFreePosY(g_meter_mapHIO.field_0x16);
    dMap_setIconFreeScale(0.0f);
    dMap_setIconSelfAlpha(0);
    dMap_setIconSelfScale(0.0f);
    dMap_setIconDispMode(0);
    field_0x0 = 0;
    field_0x1 = 0;
    field_0x2 = 0;
    field_0x4 = 0;
}

/* 80205A44-80205D24       .text moveMapCtrlDisp__13mapCtrlDisp_cFv */
void mapCtrlDisp_c::moveMapCtrlDisp() {
    f32 fVar1;
    f32 dVar7;
    if (dCam_isManual(dComIfGp_getCamera(dComIfGp_getPlayerCameraID(0))) != 0) {
        if (field_0x2 < 9) {
            field_0x2++;
            if (field_0x2 < 6) {
                dVar7 = fopMsgM_valueIncrease(6, field_0x2, 0);
                fVar1 = dVar7 * 1.2f;
            } else {
                dVar7 = fopMsgM_valueIncrease(3, field_0x2 - 6, 2);
                fVar1 = 1.2f - (dVar7 * 0.1f);
                dVar7 = 1.0f;
            }
        } else {
            dVar7 = 1.0f;
            fVar1 = 1.1f;
        }
    } else {
        if (field_0x2 > 5) {
            field_0x2 = 5;
        }
        if (field_0x2 != 0) {
            field_0x2--;
        }
        dVar7 = fopMsgM_valueIncrease(5, field_0x2, 2);
        fVar1 = 1.1f;
    }
    dMap_c::setIconDispMode(dMapIconDisp_NONE_e);
    s8 alpha = (u8)((dVar7 * 255.0f) * (g_meter_mapHIO.field_0x6 / 255.0f));
    dMap_c::setIconFreeAlpha(alpha);
    dMap_c::setIconFreeScale(fVar1 * g_meter_mapHIO.field_0x10);
    if ((u8)alpha != 0) {
        dMap_c::setIconDispMode(dMapIconDisp_FREE_e);
    }
    if (dComIfGp_checkCameraAttentionStatus(0, dCamAttnStts_00000400_e)) {
        daPy_py_c* player = daPy_getPlayerActorClass();
        BOOL temp = player->checkSubjectAccept() && !dComIfGp_event_runCheck();
        if (temp) {
            dMap_c::setIconSelfAlpha(0xCC);
            dMap_c::setIconDispMode(dMapIconDisp_SELF_e );
        }
    }
    if (dCam_getBody()->Mode() == 4) {
        dMap_c::setIconDispMode(dMapIconDisp_NONE_e);
    }
    if (field_0x0 != 0) {
        if (field_0x4 != 0 && cLib_addCalcAngleS(&field_0x4, 0, 2, 0x3c, 10) == 0 && ((field_0x1 & 4) == 0)) {
            mDoAud_seStart(JA_SE_MAP_SHOW_END);
        }
    } else {
        cLib_addCalcAngleS(&field_0x4, g_meter_mapHIO.field_0xc - g_meter_mapHIO.field_0x8, 2, 0x3c, 10);
    }
    int uVar6_2 = ((dComIfGp_getWindow(0)->getScissor()->mYOrig + dComIfGp_getWindow(0)->getScissor()->mHeight) - 452.0f);
    dMap_c::setMapDispPosLeftUpX(g_meter_mapHIO.field_0x8 + field_0x4);
    dMap_c::setMapDispPosLeftUpY(g_meter_mapHIO.field_0xa);
    if ((f32)(uVar6_2) < 0.0f) {
        dMap_c::setMapDispPosLeftUpY(uVar6_2 + g_meter_mapHIO.field_0xa);
    }
}

inline dMeter_info_c::~dMeter_info_c() {}
