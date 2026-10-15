/**
 * d_a_obj_tribox.cpp
 * Object - Hyrule Castle - Pushable triangle blocks
 */

#include "d/dolzel_rel.h" // IWYU pragma: keep
#include "d/actor/d_a_obj_tribox.h"
#include "d/d_bg_s_movebg_actor.h"

namespace daObjTribox {
namespace {
struct Attr_c {
    /* 0x00 */ f32 mSinkAccel;
    /* 0x04 */ f32 mSinkDecay;
    /* 0x08 */ f32 mSinkDecay1;
    /* 0x0C */ s16 mSinkDecay1Start;
    /* 0x0E */ s16 mSinkDecay1End;
    /* 0x10 */ f32 mSinkDecay2;
    /* 0x14 */ s16 mSinkDecay2Start;
    /* 0x16 */ s16 mSinkDecay2End;
    /* 0x18 */ s16 mFlashDelay;
};

static const Attr_c L_attr = {
    -0.8f,
    0.82f,
    0.5f,
    5,
    12,
    0.4f,
    0x41,
    0x4A,
    0x32,
};

inline const Attr_c& attr() { return L_attr; }
} // namespace
} // namespace daObjTribox

const char daObjTribox::Act_c::M_arcname[] = "MtryB";

namespace daObjTribox {
namespace {
static f32 L_r_in = (125.0f / 3.0f) * std::sqrtf(3.0f);
static f32 L_r_out = 2.0f * L_r_in;
} // namespace
} // namespace daObjTribox

cXyz daObjTribox::Act_c::M_post[3] = {
    cXyz(0.0f, 0.0f, -L_r_out),
    cXyz(-125.0f, 0.0f, L_r_in),
    cXyz(125.0f, 0.0f, L_r_in),
};

dBgS_ObjLinChk daObjTribox::Act_c::M_lin;

/* 000000EC-000001A4       .text set_state__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::set_state() {
    bool is_switch = prm_get_correct();
    bool is_sw_save = fopAcM_isSwitch(this, prm_get_swSave());

    if (!is_switch) {
        if (is_sw_save) {
            field_0x304 = 1;
        } else {
            field_0x304 = 0;
        }
    } else if (is_sw_save) {
        field_0x304 = 3;
    } else {
        field_0x304 = 2;
    }
}

/* 000001A4-000001C8       .text solidHeapCB__Q211daObjTribox5Act_cFP10fopAc_ac_c */
BOOL daObjTribox::Act_c::solidHeapCB(fopAc_ac_c* i_this) {
    return ((Act_c*)i_this)->create_heap();
}

#include "res/Object/MtryB.h"

/* 000001C8-000004B0       .text create_heap__Q211daObjTribox5Act_cFv */
u8 daObjTribox::Act_c::create_heap() {
    J3DModelData* model_data = static_cast<J3DModelData*>(dComIfG_getObjectRes(M_arcname, dRes_INDEX_MTRYB_BDL_MTRYB_e));
    JUT_ASSERT(320, model_data != NULL);
#if TARGET_PC
    // field_0x298/field_0x29c hold J3DModel pointers; the host needs the full pointer width,
    // and J3DModel's layout differs, so reach the base TR matrix through the accessor.
    field_0x298 = (uintptr_t)mDoExt_J3DModel__create(model_data, 0x80000, 0x11000022);
#else
    field_0x298 = (u32)mDoExt_J3DModel__create(model_data, 0x80000, 0x11000022);
#endif

    cBgD_t* bgw_data = (cBgD_t*)dComIfG_getObjectRes(M_arcname, dRes_INDEX_MTRYB_DZB_MTRYB_e);
    JUT_ASSERT(329, bgw_data != NULL);

    if (field_0x298 != 0) {
        mpBgW = dBgW_NewSet(bgw_data, cBgW::MOVE_BG_e, &((J3DModel*)field_0x298)->getBaseTRMtx());
    } else {
        mpBgW = NULL;
    }

    J3DModelData* model_ytfbl_data = static_cast<J3DModelData*>(dComIfG_getObjectRes(M_arcname, dRes_INDEX_MTRYB_BDL_YTFBL00_e));
    JUT_ASSERT(345, model_ytfbl_data != NULL);
#if TARGET_PC
    field_0x29c = (uintptr_t)mDoExt_J3DModel__create(model_ytfbl_data, 0x80000, 0x11000022);
#else
    field_0x29c = (u32)mDoExt_J3DModel__create(model_ytfbl_data, 0x80000, 0x11000022);
#endif

    J3DAnmTevRegKey* brk_app_data = static_cast<J3DAnmTevRegKey*>(dComIfG_getObjectRes(M_arcname, dRes_INDEX_MTRYB_BRK_YTFBL00_APP_e));
    JUT_ASSERT(355, brk_app_data != NULL);

    int app_init = mBrkApp.init(model_ytfbl_data, brk_app_data, true, 0);

    J3DAnmTevRegKey* brk_cmn_data = static_cast<J3DAnmTevRegKey*>(dComIfG_getObjectRes(M_arcname, dRes_INDEX_MTRYB_BRK_YTFBL00_CMN_e));
    JUT_ASSERT(365, brk_cmn_data != NULL);

    int cmn_init = mBrkCmn.init(model_ytfbl_data, brk_cmn_data, true, 2);

    bool result = field_0x298 != 0 && mpBgW != NULL && field_0x29c != 0 && app_init != 0 && cmn_init != 0;
    if (!result) {
        mpBgW = NULL;
    }
    return result;
}


/* 000004B0-000005C0       .text block_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::block_init() {
#if TARGET_PC
    cullMtx = ((J3DModel*)field_0x298)->getBaseTRMtx();
#else
    cullMtx = (MtxP)(field_0x298 + 0x24);
#endif
    init_mtx();
    fopAcM_setCullSizeSphere(this, 0.0f, 125.0f, 0.0f, 200.0f);
    dComIfG_Bgsp()->Regist(mpBgW, this);
    mpBgW->SetCrrFunc(dBgS_MoveBGProc_TypicalRotY);
    mpBgW->Move();
    mpBgW->SetPushPullCallback(push_pullCB);
    M_correct_flag = false;
    field_0x310 = 0;
    field_0x314 = 0;
    field_0x318 = 0;
    field_0x31c = 0;
    field_0x320 = 0;
    field_0x324 = 0;
    field_0x328 = 0;
    field_0x32c = 0;
    field_0x330 = 0;
    field_0x334 = 0;
    field_0x338 = 0;
    field_0x33c = 0;
    mBasePos = current.pos;
    mPostNo = 0;
    mPostIdx = 0;
    mMoveDir = 1;
    field_0x394 = 0;
    mode_block_wait_init();
}

/* 000005C0-00000604       .text chk_light__Q211daObjTribox5Act_cCFv */
u8 daObjTribox::Act_c::chk_light() const {
    return dComIfGs_isEventBit(0x3820) ? 2 : 0;
}

/* 00000604-0000068C       .text correct_before_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::correct_before_init() {
    fopAcM_setCullSizeSphere(this, 0.0f, 125.0f, 0.0f, 200.0f);

    if (field_0x38e) {
        field_0x396 = dComIfGp_evmng_getEventIdx("MtryB_sink");
    } else {
        field_0x396 = -1;
    }

    mode_correct_off_init();
}

/* 0000068C-00000748       .text correct_after_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::correct_after_init() {
    current.pos.y = -245.0f + home.pos.y;
#if TARGET_PC
    cullMtx = ((J3DModel*)field_0x298)->getBaseTRMtx();
#else
    cullMtx = (MtxP)(field_0x298 + 0x24);
#endif
    init_mtx();
    fopAcM_setCullSizeSphere(this, 0.0f, 125.0f, 0.0f, 200.0f);
    dComIfG_Bgsp()->Regist(mpBgW, this);
    mpBgW->SetCrrFunc(NULL);
    mpBgW->Move();
    mpBgW->SetPushPullCallback(NULL);
    field_0x394 = chk_light();
    mode_correct_dummy_init();
}

/* 00000748-00000840       .text controll_set__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::controll_set() {
    if (fpcM_IsFirstCreating(this)) {
        if (prm_get_correct() == 1) {
            fpcM_SetPriority(this, 2);

            if (M_c_cont_cnt == 0) {
                field_0x38e = 1;
                M_sink_start = 0;
                M_correct_flag = false;
                M_correct_cnt = 0;
            } else {
                field_0x38e = 0;
            }

            M_c_cont_cnt++;
            sound_pos_init();
        } else {
            if (M_b_cont_cnt == 0) {
                field_0x38e = 1;
            } else {
                field_0x38e = 0;
            }

            M_b_cont_cnt++;
        }
    }
}

/* 00000840-00000904       .text controll_clear__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::controll_clear() {
    if (prm_get_correct() == 1) {
        M_c_cont_cnt--;
        JUT_ASSERT(532, M_c_cont_cnt >= 0);
    } else {
        M_b_cont_cnt--;
        JUT_ASSERT(535, M_b_cont_cnt >= 0);
    }
}

/* 00000904-00000980       .text create_block_before__Q211daObjTribox5Act_cFv */
cPhs_State daObjTribox::Act_c::create_block_before() {
    cPhs_State ret = dComIfG_resLoad(&mPhase, M_arcname);
    if (ret == cPhs_COMPLEATE_e) {
        if (fopAcM_entrySolidHeap(this, solidHeapCB, 0x11C0)) {
            block_init();
        } else {
            ret = cPhs_ERROR_e;
        }
    }
    return ret;
}

/* 00000980-00000988       .text create_block_after__Q211daObjTribox5Act_cFv */
cPhs_State daObjTribox::Act_c::create_block_after() {
    return cPhs_ERROR_e;
}

/* 00000988-000009AC       .text create_correct_before__Q211daObjTribox5Act_cFv */
cPhs_State daObjTribox::Act_c::create_correct_before() {
    correct_before_init();
    return cPhs_COMPLEATE_e;
}

/* 000009AC-00000A28       .text create_correct_after__Q211daObjTribox5Act_cFv */
cPhs_State daObjTribox::Act_c::create_correct_after() {
    cPhs_State ret = dComIfG_resLoad(&mPhase, M_arcname);
    if (ret == cPhs_COMPLEATE_e) {
        if (fopAcM_entrySolidHeap(this, solidHeapCB, 0x4000)) {
            correct_after_init();
        } else {
            ret = cPhs_ERROR_e;
        }
    }
    return ret;
}

/* 00000A28-00000BC4       .text _create__Q211daObjTribox5Act_cFv */
cPhs_State daObjTribox::Act_c::_create() {
    fopAcM_ct(this, Act_c);

    set_state();
    controll_set();

    static cPhs_State (Act_c::*create_table[4])() = {
        &Act_c::create_block_before,
        &Act_c::create_block_after,
        &Act_c::create_correct_before,
        &Act_c::create_correct_after,
    };
    return (this->*create_table[field_0x304])();
}

/* 00000CA4-00000D68       .text _delete__Q211daObjTribox5Act_cFv */
bool daObjTribox::Act_c::_delete() {
    sound_pos_delete();
    controll_clear();
    eff_smoke_remove();
    eff_sink_smoke_remove();

    if (field_0x304 == 0 || field_0x304 == 3) {
        if (mpBgW != NULL) {
            mpBgW->mpPushPullCb = NULL;
        }

        if (mpBgW != NULL && mpBgW->ChkUsed()) {
            dComIfG_Bgsp()->Release(mpBgW);
        }

        dComIfG_resDelete(&mPhase, M_arcname);
    }

    return true;
}

/* 00000D68-00000DFC       .text set_mtx__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::set_mtx() {
    mDoMtx_stack_c::transS(current.pos);
    mDoMtx_stack_c::ZXYrotM(shape_angle);
#if TARGET_PC
    MTXCopy(mDoMtx_stack_c::get(), ((J3DModel*)field_0x298)->getBaseTRMtx());
#else
    MTXCopy(mDoMtx_stack_c::get(), (MtxP)(field_0x298 + 0x24));
#endif
    mDoMtx_stack_c::get()[1][3] += 251.0f;
#if TARGET_PC
    MTXCopy(mDoMtx_stack_c::get(), ((J3DModel*)field_0x29c)->getBaseTRMtx());
#else
    MTXCopy(mDoMtx_stack_c::get(), (MtxP)(field_0x29c + 0x24));
#endif
}

/* 00000DFC-00000E54       .text init_mtx__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::init_mtx() {
    ((J3DModel*)field_0x298)->setBaseScale(scale);
    ((J3DModel*)field_0x29c)->setBaseScale(scale);
    set_mtx();
}

/* 00000E54-00000F8C       .text push_pullCB__Q211daObjTribox5Act_cFP10fopAc_ac_cP10fopAc_ac_csQ24dBgW13PushPullLabel */
fopAc_ac_c* daObjTribox::Act_c::push_pullCB(fopAc_ac_c* actor, fopAc_ac_c* i_actor2, short angle, dBgW::PushPullLabel i_pp_label) {
    Act_c* i_this = (Act_c*)actor;

    dBgW::PushPullLabel pp_label = cLib_checkBit(i_pp_label, (dBgW::PushPullLabel)(dBgW::PPLABEL_PUSH | dBgW::PPLABEL_PULL));
    if (pp_label) {
        const int pp_field = dBgW::PPLABEL_PUSH | dBgW::PPLABEL_PULL;
        JUT_ASSERT(0x2b0, pp_label != pp_field);

        if (cLib_checkBit(pp_label, dBgW::PPLABEL_PUSH)) {
            i_this->field_0x31c = 0;
        } else {
            i_this->field_0x31c = 1;
        }

        s16 move_ang = angle - 0x8000;
        s16 diff = move_ang - i_this->shape_angle.y;

        if (diff >= -0x2aaa && diff < 0x2aaa) {
            i_this->field_0x314 = 0;
        } else if (!(diff < 0x2aaa)) {
            i_this->field_0x314 = 1;
        } else {
            i_this->field_0x314 = 2;
        }

        static const s16 face_ang_offset[3] = { 0, 0x5555, 0xAAAB };

        s16 searchAngle = fopAcM_searchActorAngleY(actor, i_actor2);
        s16 face_ang = i_this->shape_angle.y + face_ang_offset[i_this->field_0x314];
        s16 diff2 = face_ang - searchAngle;

        if (!(0 > diff2)) {
            i_this->field_0x318 = 0;
        } else {
            i_this->field_0x318 = 1;
        }

        i_this->field_0x310 = 1;
    }

    return actor;
}

/* 00000F8C-00000FF4       .text line_cross__Q211daObjTribox5Act_cCFPC4cXyzPC4cXyz */
bool daObjTribox::Act_c::line_cross(const cXyz* p0, const cXyz* p1) const {
    M_lin.Set((cXyz*)p0, (cXyz*)p1, (fopAc_ac_c*)this);
    M_lin.SetActorPid(fopAcM_GetID((void*)this));
    return dComIfG_Bgsp()->LineCross(&M_lin);
}

/* 00000FF4-000012A0       .text chk_wall__Q211daObjTribox5Act_cCFi */
u8 daObjTribox::Act_c::chk_wall(int num) const {
    JUT_ASSERT(0x30C, (num == 1) || (num == 2));

    int idx;
    if (mMoveDir >= 0) {
        idx = (mPostIdx + 1) % 3;
    } else {
        idx = (mPostIdx + 2) % 3;
    }

    mDoMtx_stack_c::YrotS(shape_angle.y);
    cXyz post;
    mDoMtx_stack_c::multVecSR(&M_post[idx], &post);

    cXyz p0(current.pos.x, 5.0f + current.pos.y, current.pos.z);
    cXyz p1 = p0 - post * 0.99f;

    if (line_cross(&p0, &p1)) {
        return 1;
    }

    if (num == 2) {
        p0 = p0 - post;

        mDoMtx_stack_c::YrotM(mMoveDir >= 0 ? 0x2AAA : -0x2AAA);

        mDoMtx_stack_c::multVecSR(&M_post[idx], &post);
        p1 = p0 - post * 0.49f;
        if (line_cross(&p0, &p1)) {
            return 1;
        }

        p0.y += 30.0f;
        p1 = p0 - post * 0.99f;
        if (line_cross(&p0, &p1)) {
            return 1;
        }
    }

    return 0;
}

/* 000012A0-000012E8       .text chk_space__Q211daObjTribox5Act_cCFv */
u8 daObjTribox::Act_c::chk_space() const {
    u32 iVar1 = field_0x33c != 1;
    u32 r4 = (iVar1 == 0) ? 1 : 0;
    if (chk_wall(r4 + 1)) {
        return false;
    } else {
        return true;
    }
}

/* 000012E8-00001378       .text eff_flash__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_flash() {
    cXyz pos = current.pos;
    pos.y += 251.0f;
    dComIfGp_particle_setP1(dPa_name::ID_AK_SN_TRIFORCEBLOCKFLASH00, &pos, &shape_angle);
}

/* 00001378-00001464       .text eff_smoke_start__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_smoke_start() {
    static cXyz scl(0.6f, 0.6f, 0.6f);

    eff_smoke_pos();

    JPABaseEmitter* emitter = dComIfGp_particle_setToon(
        dPa_name::ID_AK_JT_ELEMENTSMOKE00, &mSmokePos, (csXyz*)&field_0x388, &scl, 0xB9,
        &field_0x35c, fopAcM_GetRoomNo(this)
    );
    if (emitter) {
        emitter->setRate(1.0f);
        emitter->setDirectionalSpeed(15.0f);
        emitter->setSpread(0.15f);
        emitter->setLifeTime(30);
    }
}

/* 00001464-000015C8       .text eff_smoke_pos__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_smoke_pos() {
    int idx1, idx2;
    if (mMoveDir >= 0) {
        idx1 = mPostIdx + 1;
        idx2 = mPostIdx + 2;
    } else {
        idx1 = mPostIdx + 2;
        idx2 = mPostIdx + 1;
    }
    idx1 %= 3;
    idx2 %= 3;

    cXyz post1;
    cXyz post2;
    mDoMtx_stack_c::YrotS(shape_angle.y);
    mDoMtx_stack_c::multVecSR(&M_post[idx1], &post1);
    mDoMtx_stack_c::multVecSR(&M_post[idx2], &post2);

    cXyz dir = post2 - post1;
    dir.normalizeZP();
    dir *= 10.0f;

    mSmokePos = post1 + dir + current.pos;
    field_0x388 = 0;
    mSmokeAngle = cLib_targetAngleY(&post1, &post2);
    field_0x38c = 0;
}

/* 000015C8-000015F4       .text eff_smoke_end__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_smoke_end() {
    field_0x35c.end();
}

/* 000015F4-00001620       .text eff_smoke_remove__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_smoke_remove() {
    field_0x35c.remove();
}

/* 00001620-0000162C       .text eff_sink_smoke_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_sink_smoke_init() {
    field_0x3f8 = 0;
}

/* 0000162C-00001740       .text eff_sink_smoke_start__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_sink_smoke_start() {
    if (field_0x3f8) {
        return;
    }
    field_0x3f8 = 1;

    dPa_levelEcallBack* cbArr[3] = {
        &field_0x398,
        &field_0x3b8,
        &field_0x3d8,
    };

    csXyz outer_angle = shape_angle;

    for (int i = 0; i < 3; i++) {
        csXyz angle = shape_angle;
        angle.y += i * 0x5555;
        dComIfGp_particle_setToon(dPa_name::ID_AK_ST_TRIFORCEBLOCKSMOKE00, &current.pos, &angle, NULL, 0xa0, cbArr[i]);
    }
}

/* 00001740-000017B8       .text eff_sink_smoke_end__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_sink_smoke_end() {
    if (field_0x3f8 != 0) {
        field_0x3f8 = 0;
        field_0x398.remove();
        field_0x3b8.remove();
        field_0x3d8.remove();
    }
}

/* 000017B8-0000181C       .text eff_sink_smoke_remove__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::eff_sink_smoke_remove() {
    field_0x398.remove();
    field_0x3b8.remove();
    field_0x3d8.remove();
}

/* 0000181C-00001828       .text vib_sink_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::vib_sink_init() {
    field_0x3f9 = 0;
}

/* 00001828-000018FC       .text vib_sink_start__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::vib_sink_start() {
    if (field_0x3f9 == 0) {
        field_0x3f9 = 1;
        dVibration_c& vib = dComIfGp_getVibration();
        vib.StartShock(2, 1, cXyz(0.0f, 1.0f, 0.0f));
        vib.StartShock(1, 6, cXyz(0.0f, 1.0f, 0.0f));
        vib.StartQuake(3, 0xf, cXyz(0.0f, 1.0f, 0.0f));
    }
}

/* 000018FC-0000197C       .text vib_sink_end__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::vib_sink_end() {
    if (field_0x3f9 != 0) {
        field_0x3f9 = 0;
        dComIfGp_getVibration().StopQuake(-1);
        dComIfGp_getVibration().StartShock(3, 0xf, cXyz(0.0f, 1.0f, 0.0f));
    }
}

/* 0000197C-00001A08       .text search_block__Q211daObjTribox5Act_cFPvPv */
void* daObjTribox::Act_c::search_block(void* i_actor, void* i_this) {
    Act_c* block = (Act_c*)i_actor;
    Act_c* self = (Act_c*)i_this;
    if (fopAc_IsActor(block) && fopAcM_GetName(block) == fpcNm_Obj_Tribox_e && block->prm_get_correct() == 0 &&
        fopAcM_searchActorDistance2(self, block) < 225.0f)
    {
        return block;
    }
    return NULL;
}

/* 00001A08-00001A90       .text sound_pos_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::sound_pos_init() {
    if (field_0x38e != 0) {
        M_sound_pos = home.pos * (1.0f / 3.0f);
    } else {
        M_sound_pos += home.pos * (1.0f / 3.0f);
    }
}

/* 00001A90-00001AC4       .text sound_pos_delete__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::sound_pos_delete() {
    mDoAud_seDeleteObject(&M_sound_pos);
}

/* 00001AC4-00001D0C       .text sound_get_mapinfo__Q211daObjTribox5Act_cFRC4cXyz */
u32 daObjTribox::Act_c::sound_get_mapinfo(const cXyz& i_pos) {
    dBgS_ObjGndChk gndChk;
    cXyz pos = i_pos;
    pos.y += 50.0f;
    gndChk.SetPos(&pos);
    dComIfG_Bgsp()->GroundCross(&gndChk);

    int bgIndex = gndChk.GetBgIndex();
    u32 mtrlSndId = 0xd;
    if (bgIndex >= 0 && bgIndex < 0x100) {
        mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(gndChk);
    }

    return mtrlSndId;
}

/* 000021CC-00002268       .text sound_sink_down_block__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::sound_sink_down_block() {
    if (field_0x38e) {
        mDoAud_seStart(JA_SE_OBJ_TRIF_BL_GO_DOWN, &M_sound_pos, sound_get_mapinfo(M_sound_pos), dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    }
}

/* 00002268-00002304       .text sound_sink_stop_block__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::sound_sink_stop_block() {
    if (field_0x38e) {
        mDoAud_seStart(JA_SE_OBJ_TRIF_BL_STOP, &M_sound_pos, sound_get_mapinfo(M_sound_pos), dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    }
}

/* 00002304-000023A0       .text sound_flash_shine__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::sound_flash_shine() {
    if (field_0x38e) {
        mDoAud_seStart(JA_SE_OBJ_TRIF_FL_SHINE, &M_sound_pos, sound_get_mapinfo(M_sound_pos), dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    }
}

/* 000023A0-0000243C       .text sound_flash_light__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::sound_flash_light() {
    if (field_0x38e) {
        mDoAud_seStart(JA_SE_OBJ_TRIF_FL_LIGHT, &M_sound_pos, sound_get_mapinfo(M_sound_pos), dComIfGp_getReverb(fopAcM_GetRoomNo(this)));
    }
}

/* 0000243C-00002458       .text mode_block_wait_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_wait_init() {
    fopAcM_OnStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x340 = 0;
    field_0x308 = 0;
}

/* 00002458-000025C0       .text mode_block_wait__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_wait() {
    if (M_correct_flag) {
        field_0x340 = 0;
        mode_block_sink_init();
        return;
    }

    if (field_0x310 != 0 && field_0x314 == field_0x324 && field_0x318 == field_0x328 &&
        field_0x31c == field_0x32c) {
        field_0x340++;
    } else {
        field_0x340 = 0;
    }

    if (field_0x340 < 4) {
        return;
    }

    if ((field_0x318 == 0 && field_0x31c == 1) || (field_0x318 == 1 && field_0x31c == 0)) {
        mMoveDir = 1;
    } else {
        mMoveDir = -1;
    }

    if (field_0x318 == 0) {
        mPostIdx = (field_0x314 + 2) % 3;
    } else {
        mPostIdx = (field_0x314 + 1) % 3;
    }

    field_0x330 = field_0x310;
    field_0x334 = field_0x314;
    field_0x338 = field_0x318;
    field_0x33c = field_0x31c;

    if (chk_space()) {
        mode_block_walk_init();
    }
}

/* 000025C0-0000262C       .text mode_block_walk_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_walk_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 1;
    mTimer = 20;
    mBasePos = current.pos;
    eff_smoke_start();
    daPy_getPlayerActorClass()->onPushPullKeep();
}

/* 0000262C-00002B34       .text mode_block_walk__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_walk() {
    bool is_end = --mTimer <= 0;

    f32 curve = cos((M_PI / 20.0f) * mTimer);
    f32 home_ang = cM_s2rad(home.angle.y);
    f32 now_ang = (M_PI / 3.0f) * mPostNo + home_ang;
    f32 add_ang = (M_PI / 3.0f) * (mMoveDir * (0.5f * (1.0f + curve)));

    cXyz now_post;
    cXyz next_post;
    MTXRotRad(mDoMtx_stack_c::now, 'Y', now_ang);
    mDoMtx_stack_c::multVecSR(&M_post[mPostIdx], &now_post);
    mDoMtx_stack_c::rYrotM(add_ang);
    mDoMtx_stack_c::multVecSR(&M_post[mPostIdx], &next_post);
    current.pos = mBasePos + now_post - next_post;
    shape_angle.y = (s16)(0.5f + RAD2S_CONSTANT * (now_ang + add_ang));

    if (is_end) {
        eff_smoke_end();
    } else {
        eff_smoke_pos();
    }

    if (is_end) {
        daPy_getPlayerActorClass()->offPushPullKeep();
        mPostNo = (mPostNo + mMoveDir) % 6;

        if (chk_wall(1)) {
            u32 mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(M_lin);
            mDoAud_seStart(JA_SE_LK_MOVE_ROCK_LIMIT, &eyePos, mtrlSndId,
                           dComIfGp_getReverb(current.roomNo));
        }

        mode_block_wait_init();
    } else {
        dBgS_ObjGndChk gndChk;
        cXyz chkPos(current.pos.x, 50.0f + current.pos.y, current.pos.z);
        gndChk.SetPos(&chkPos);
        dComIfG_Bgsp()->GroundCross(&gndChk);

        int bgIndex = gndChk.GetBgIndex();
        u32 mtrlSndId = 0;
        if (bgIndex >= 0 && bgIndex < 0x100) {
            mtrlSndId = dComIfG_Bgsp()->GetMtrlSndId(gndChk);
        }

        mDoAud_seStart(JA_SE_LK_MOVE_ROCK, &eyePos, mtrlSndId,
                       dComIfGp_getReverb(current.roomNo));
    }
}

/* 00002B34-00002B90       .text mode_block_sink_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_sink_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 2;
    field_0x390 = 0.0f;
    mTimer = 0;
    eff_sink_smoke_init();
    vib_sink_init();
}

/* 00002B90-00002CE4       .text mode_block_sink__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_sink() {
    if (M_sink_start) {
        eff_sink_smoke_start();
        vib_sink_start();
        f32 lim = -245.0f + home.pos.y;
        field_0x390 += attr().mSinkAccel;
        if (mTimer >= attr().mSinkDecay1Start && mTimer < attr().mSinkDecay1End) {
            field_0x390 *= attr().mSinkDecay1;
        } else if (mTimer >= attr().mSinkDecay2Start && mTimer < attr().mSinkDecay2End) {
            field_0x390 *= attr().mSinkDecay2;
        } else {
            field_0x390 *= attr().mSinkDecay;
        }
        current.pos.y += field_0x390;
        if (current.pos.y < lim) {
            current.pos.y = lim;
            eff_sink_smoke_end();
            vib_sink_end();
            sound_sink_stop_block();
            mode_block_lower_init();
        } else {
            sound_sink_down_block();
            mTimer++;
        }
    } else {
        field_0x390 = 0.0f;
        mTimer = 0;
    }
}

/* 00002CE4-00002D14       .text mode_block_lower_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_lower_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 3;
    mTimer = attr().mFlashDelay;
    field_0x3fa = 0x5E;
}

/* 00002D14-00002DA8       .text mode_block_lower__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_block_lower() {
    if (mTimer > 0) {
        mTimer--;
        if (mTimer == 0) {
            eff_flash();
            field_0x394 = 1;
        }
    }
    if (mTimer == 0) {
        if (field_0x3fa > 0) {
            field_0x3fa--;
            if (field_0x3fa == 0) {
                sound_flash_shine();
            }
        }
    } else {
        field_0x3fa = 0x5E;
    }
}

/* 00002DA8-00002DC0       .text mode_correct_off_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_off_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 4;
}

/* 00002DC0-00002E1C       .text mode_correct_off__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_off() {
    if (fopAcM_Search(search_block, this)) {
        M_correct_cnt++;
        mode_correct_on_init();
    }
}

/* 00002E1C-00002E34       .text mode_correct_on_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_on_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 5;
}

/* 00002E34-00002EEC       .text mode_correct_on__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_on() {
    if (fopAcM_Search(search_block, this) != NULL) {
        if (field_0x38e != 0 && M_correct_cnt >= 3) {
            M_correct_flag = true;
        }

        if (M_correct_flag) {
            if (field_0x38e != 0) {
                mode_correct_demoreq_init();
            } else {
                mode_correct_end_init();
            }
        }
    } else {
        M_correct_cnt--;
        mode_correct_off_init();
    }
}

/* 00002EEC-00002F0C       .text mode_correct_demoreq_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_demoreq_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 6;
    mTimer = 10;
}

/* 00002F0C-00002FB4       .text mode_correct_demoreq__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_demoreq() {
    u8 doInit = 1;

    if (dComIfGp_getPEvtManager()->getEventData(field_0x396) != NULL) {
        if (eventInfo.checkCommandDemoAccrpt()) {
            field_0x395 = 1;
        } else {
            fopAcM_orderOtherEventId(this, field_0x396, 0xff, 0xffff, 0, 1);
            eventInfo.onCondition(dEvtCnd_UNK2_e);
            doInit = 0;
        }
    }

    if (doInit) {
        mode_correct_demorun_init();
    }
}

/* 00002FB4-00003028       .text mode_correct_demorun_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_demorun_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 7;
    mTimer = 0x32;
    mDoAud_seStart(JA_SE_READ_RIDDLE_1);
}

/* 00003028-00003104       .text mode_correct_demorun__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_demorun() {
    if (mTimer > 0) {
        mTimer--;
        if (mTimer == 0) {
            fopAcM_onSwitch(this, prm_get_swSave());
            M_sink_start = true;
        }
    } else {
        u8 correct_end = 1;
        if (field_0x395 != 0) {
            if (dComIfGp_evmng_endCheck(field_0x396)) {
                dComIfGp_event_onEventFlag(8);
                field_0x395 = 0;
            } else {
                correct_end = 0;
            }
        }
        if (correct_end) {
            mode_correct_end_init();
        }
    }
}

/* 00003104-0000311C       .text mode_correct_end_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_end_init() {
    fopAcM_OnStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 8;
}

/* 0000311C-00003120       .text mode_correct_end__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_end() {
}

/* 00003120-00003138       .text mode_correct_dummy_init__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_dummy_init() {
    fopAcM_OffStatus(this, fopAcStts_NOCULLEXEC_e);
    field_0x308 = 9;
}

/* 00003138-0000313C       .text mode_correct_dummy__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_correct_dummy() {
}

/* 0000313C-00003288       .text mode_proc_call__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::mode_proc_call() {
    typedef void (daObjTribox::Act_c::*procFunc)();
    static procFunc mode_proc[] = {
        &daObjTribox::Act_c::mode_block_wait,
        &daObjTribox::Act_c::mode_block_walk,
        &daObjTribox::Act_c::mode_block_sink,
        &daObjTribox::Act_c::mode_block_lower,
        &daObjTribox::Act_c::mode_correct_off,
        &daObjTribox::Act_c::mode_correct_on,
        &daObjTribox::Act_c::mode_correct_demoreq,
        &daObjTribox::Act_c::mode_correct_demorun,
        &daObjTribox::Act_c::mode_correct_end,
        &daObjTribox::Act_c::mode_correct_dummy,
    };

    (this->*mode_proc[field_0x308])();
}

/* 00003288-00003304       .text execute_block__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::execute_block() {
    mode_proc_call();
    field_0x320 = field_0x310;
    field_0x324 = field_0x314;
    field_0x328 = field_0x318;
    field_0x32c = field_0x31c;
    field_0x310 = 0;
    eyePos = current.pos;
    set_mtx();
    mpBgW->Move();
}

/* 00003304-00003324       .text execute_correct__Q211daObjTribox5Act_cFv */
void daObjTribox::Act_c::execute_correct() {
    mode_proc_call();
}

/* 00003324-000033D8       .text _execute__Q211daObjTribox5Act_cFv */
bool daObjTribox::Act_c::_execute() {
    if (prm_get_correct() == 0) {
        execute_block();
    } else {
        execute_correct();
    }

    if (field_0x29c != 0) {
        if (field_0x394 == 1) {
            if (mBrkApp.play()) {
                field_0x394 = 2;
            }
        } else if (field_0x394 == 2) {
            mBrkCmn.play();
        }

        if (field_0x394 == 1 || field_0x394 == 2) {
            sound_flash_light();
        }
    }

    return true;
}

/* 000033D8-00003538       .text _draw__Q211daObjTribox5Act_cFv */
bool daObjTribox::Act_c::_draw() {
    if (field_0x304 == 0 || field_0x304 == 3) {
        g_env_light.settingTevStruct(0, &current.pos, &tevStr);
        g_env_light.setLightTevColorType((J3DModel*)field_0x298, &tevStr);
        if (field_0x394 != 0) {
            g_env_light.setLightTevColorType((J3DModel*)field_0x29c, &tevStr);
            if (field_0x394 == 1) {
                mBrkApp.entry(((J3DModel*)field_0x29c)->getModelData());
            } else {
                mBrkCmn.entry(((J3DModel*)field_0x29c)->getModelData());
            }
        }
        if (field_0x394 != 0) {
            j3dSys.mDrawBuffer[0] = g_dComIfG_gameInfo.drawlist.mpOpaListP1;
            j3dSys.mDrawBuffer[1] = g_dComIfG_gameInfo.drawlist.mpXluListP1;
            mDoExt_modelUpdateDL((J3DModel*)field_0x29c);
            j3dSys.mDrawBuffer[0] = g_dComIfG_gameInfo.drawlist.mpOpaList;
            j3dSys.mDrawBuffer[1] = g_dComIfG_gameInfo.drawlist.mpXluList;
        }
        j3dSys.mDrawBuffer[0] = g_dComIfG_gameInfo.drawlist.mpOpaListBG;
        j3dSys.mDrawBuffer[1] = g_dComIfG_gameInfo.drawlist.mpXluListBG;
        mDoExt_modelUpdateDL((J3DModel*)field_0x298);
        j3dSys.mDrawBuffer[0] = g_dComIfG_gameInfo.drawlist.mpOpaList;
        j3dSys.mDrawBuffer[1] = g_dComIfG_gameInfo.drawlist.mpXluList;
    }
    return true;
}

namespace daObjTribox {
namespace {
/* 00003538-00003558       .text Mthd_Create__Q211daObjTribox28@unnamed@d_a_obj_tribox_cpp@FPv */
cPhs_State Mthd_Create(void* i_this) {
    return ((daObjTribox::Act_c*)i_this)->_create();
}

/* 00003558-0000357C       .text Mthd_Delete__Q211daObjTribox28@unnamed@d_a_obj_tribox_cpp@FPv */
BOOL Mthd_Delete(void* i_this) {
    return ((daObjTribox::Act_c*)i_this)->_delete();
}

/* 0000357C-000035A0       .text Mthd_Execute__Q211daObjTribox28@unnamed@d_a_obj_tribox_cpp@FPv */
BOOL Mthd_Execute(void* i_this) {
    return ((daObjTribox::Act_c*)i_this)->_execute();
}

/* 000035A0-000035C4       .text Mthd_Draw__Q211daObjTribox28@unnamed@d_a_obj_tribox_cpp@FPv */
BOOL Mthd_Draw(void* i_this) {
    return ((daObjTribox::Act_c*)i_this)->_draw();
}

/* 000035C4-000035CC       .text Mthd_IsDelete__Q211daObjTribox28@unnamed@d_a_obj_tribox_cpp@FPv */
BOOL Mthd_IsDelete(void*) {
    return TRUE;
}

static actor_method_class Mthd_Table = {
    (process_method_func)Mthd_Create,
    (process_method_func)Mthd_Delete,
    (process_method_func)Mthd_Execute,
    (process_method_func)Mthd_IsDelete,
    (process_method_func)Mthd_Draw,
};
}; // namespace
}; // namespace daObjTribox

actor_process_profile_definition g_profile_Obj_Tribox = {
    /* Layer ID     */ fpcLy_CURRENT_e,
    /* List ID      */ 0x0003,
    /* List Prio    */ fpcPi_CURRENT_e,
    /* Proc Name    */ fpcNm_Obj_Tribox_e,
    /* Proc SubMtd  */ &g_fpcLf_Method.base,
    /* Size         */ sizeof(daObjTribox::Act_c),
    /* Size Other   */ 0,
    /* Parameters   */ 0,
    /* Leaf SubMtd  */ &g_fopAc_Method.base,
    /* Draw Prio    */ fpcDwPi_Obj_Tribox_e,
    /* Actor SubMtd */ &daObjTribox::Mthd_Table,
    /* Status       */ fopAcStts_CULL_e | fopAcStts_UNK40000_e,
    /* Group        */ fopAc_ACTOR_e,
    /* Cull Type    */ fopAc_CULLSPHERE_CUSTOM_e,
};
