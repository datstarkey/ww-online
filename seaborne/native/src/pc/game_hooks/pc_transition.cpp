// Scene and room transitions: what the 60 fps gate (pc_fps60_paint_b_allowed) and the fast
// transitions option (COS_QOL_FAST_TRANSITIONS, pc_qol.h) both look at.
//
// - pc_transition_reason: a scene request pending, an overlap (the fades and wipes between scenes,
//   d_ovlp_fade*.cpp), the drawlist's wipe, the screen fade (mDoGph_gInf_c) or JUTFader fading.
// - pc_transition_turbo: with COS_QOL_FAST_TRANSITIONS on and a transition in progress, the frame
//   pacing does not wait (pc_frame_pace): the game runs those frames as fast as the display presents
//   them (twice the speed at 60 Hz, four times at 120), so every fixed fade, wipe and loading wait of
//   a scene change, warp or door shortens alike. Not during an event of the player's own (a cutscene
//   that only fades the screen keeps its speed) unless the screen is changing scenes.
// - COS_TRACE=transition: one line per change of the reason, with the frame and the time since the
//   last change (how long each part of a scene change takes).
#include "d/dolzel.h" // IWYU pragma: keep
#include "pc/game_hooks.h"
#include "pc/pc_harness.h"
#include "pc/pc_qol.h"
#include "pc_internal.h"

#include "d/d_com_inf_game.h"
#include "d/d_drawlist.h"
#include "f_op/f_op_overlap_mng.h"
#include "JSystem/JUtility/JUTFader.h"
#include "m_Do/m_Do_graphic.h"

#include <ctime>
#include <unistd.h>

namespace {

bool sTurbo = false;

uint64_t nowNs() {
    timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint64_t)ts.tv_sec * 1000000000ull + (uint64_t)ts.tv_nsec;
}

} // namespace

const char* pc_transition_reason(void) {
    JUTFader* fader = mDoGph_gInf_c::mFader;
    if (pc_fpcNdRq_pending()) {
        return "scene request";
    }
    if (fopOvlpM_IsDoingReq() || fopOvlpM_IsPeek()) {
        return "overlap";
    }
    if (dDlst_list_c::mWipe) {
        return "wipe";
    }
    if (mDoGph_gInf_c::isFade()) {
        return "fade";
    }
    if (fader != nullptr && (fader->getStatus() == JUTFader::FadeIn || fader->getStatus() == JUTFader::FadeOut)) {
        return "fader";
    }
    return nullptr;
}

void pc_transition_frame(unsigned int frame) {
    static const bool trace = pc_trace_enabled("transition") != 0;
    static const char* last = nullptr;
    static uint64_t lastNs = 0;
    const char* why = pc_transition_reason();
    // A fade of the screen alone (no scene change) during an event is the event's own effect.
    const bool sceneChange = why != nullptr && (fopOvlpM_IsDoingReq() || fopOvlpM_IsPeek() || pc_fpcNdRq_pending() ||
                                                !dComIfGp_event_runCheck());
    sTurbo = sceneChange && pc_qol(PC_QOL_FAST_TRANSITIONS) != 0;
    if (trace && why != last) {
        const uint64_t now = nowNs();
        pc::writef(STDERR_FILENO, "[cos] transition: frame %u: %s (%s for %.0f ms)%s\n", frame,
                   why != nullptr ? why : "none", last != nullptr ? last : "none",
                   lastNs != 0 ? (now - lastNs) / 1e6 : 0.0, sTurbo ? " turbo" : "");
        last = why;
        lastNs = now;
    }
}

int pc_transition_turbo(void) {
    return sTurbo ? 1 : 0;
}
