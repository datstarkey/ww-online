// COS_ASPECT: the widescreen option (native/include/pc/pc_aspect.h, docs/WIDESCREEN.md).
#include "pc/pc_aspect.h"

#include "pc_internal.h"

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <unistd.h>

#if defined(__APPLE__)
#include <CoreGraphics/CoreGraphics.h>
#endif

namespace {

struct Aspect {
    const char* name;
    float ratio;
};

constexpr Aspect kAspects[] = {
    {"4:3", 4.0f / 3.0f},
    {"16:9", 16.0f / 9.0f},
    {"16:10", 16.0f / 10.0f},
};

// The widest picture accepted: 32:9 (t = 5) and a little more; the HUD then sits at the edges
// of a super-ultrawide screen.
constexpr float kMaxRatio = 4.0f;

// -1 until the first call reads COS_ASPECT (pc_harness_init, before any game thread starts).
int sAspect = -1;
float sRatio = 4.0f / 3.0f;
char sName[32] = "4:3";
// Set when COS_ASPECT names no aspect; pc_aspect_init reports it and exits.
const char* sBadValue = nullptr;

// The main display's width / height ("auto"), 0 when unknown. CoreGraphics, not SDL: this runs
// before the video subsystem starts (and before the headless hint, Aurora patch 0022). Embedded
// (libtww), the host's view is the screen the picture fills, whatever display it is on.
float displayRatio() {
    if (const pc::Embed& e = pc::gEmbed; e.metalLayer != nullptr) {
        return e.width != 0 && e.height != 0 ? (float)e.width / (float)e.height : 0.0f;
    }
#if defined(__APPLE__)
    const CGDirectDisplayID id = CGMainDisplayID();
    const size_t w = CGDisplayPixelsWide(id);
    const size_t h = CGDisplayPixelsHigh(id);
    if (w != 0 && h != 0) {
        return (float)w / (float)h;
    }
#endif
    return 0.0f;
}

// "W:H" with positive numbers (e.g. 21:9, 32:9, 1512:982), or 0.
float parseRatio(const char* v) {
    char* end = nullptr;
    const float w = strtof(v, &end);
    if (end == v || *end != ':') {
        return 0.0f;
    }
    const char* hs = end + 1;
    const float h = strtof(hs, &end);
    if (end == hs || *end != '\0' || !(w > 0.0f) || !(h > 0.0f)) {
        return 0.0f;
    }
    return w / h;
}

void set(int aspect, float ratio, const char* name) {
    sAspect = aspect;
    sRatio = ratio;
    snprintf(sName, sizeof(sName), "%s", name);
}

void parse() {
    const char* v = getenv("COS_ASPECT");
    if (v == nullptr || v[0] == '\0') {
#if defined(__SWITCH__)
        // the Switch harness sets 16:9 (or the settings file's value) before the first call
        set(PC_ASPECT_4_3, kAspects[PC_ASPECT_4_3].ratio, kAspects[PC_ASPECT_4_3].name);
        return;
#else
        v = "auto"; // the options menu's default on the desktop: the display's shape
#endif
    }
    for (int i = 0; i < (int)(sizeof(kAspects) / sizeof(kAspects[0])); i++) {
        if (strcmp(v, kAspects[i].name) == 0) {
            set(i, kAspects[i].ratio, kAspects[i].name);
            return;
        }
    }
    float ratio = 0.0f;
    if (strcmp(v, "auto") == 0) {
        // The display's own shape, so fullscreen fills it; 16:9 when it is unknown, and 4:3 at least
        // (a portrait or square display gets the GameCube's picture, pillarboxed).
        ratio = displayRatio();
        if (ratio == 0.0f) {
            ratio = kAspects[PC_ASPECT_16_9].ratio;
        }
        ratio = std::min(std::max(ratio, kAspects[PC_ASPECT_4_3].ratio), kMaxRatio);
    } else {
        ratio = parseRatio(v);
        if (ratio < kAspects[PC_ASPECT_4_3].ratio - 1e-4f || ratio > kMaxRatio) {
            sBadValue = v;
            set(PC_ASPECT_4_3, kAspects[PC_ASPECT_4_3].ratio, kAspects[PC_ASPECT_4_3].name);
            return;
        }
    }
    for (int i = 0; i < (int)(sizeof(kAspects) / sizeof(kAspects[0])); i++) {
        if (fabsf(ratio - kAspects[i].ratio) < 1e-4f) {
            set(i, kAspects[i].ratio, kAspects[i].name);
            return;
        }
    }
    char name[32];
    if (strcmp(v, "auto") == 0) {
        snprintf(name, sizeof(name), "%.3f:1", (double)ratio);
    } else {
        snprintf(name, sizeof(name), "%s", v);
    }
    set(PC_ASPECT_OTHER, ratio, name);
}

} // namespace

extern "C" {

int pc_aspect(void) {
    if (sAspect < 0) {
        parse();
    }
    return sAspect;
}

const char* pc_aspect_name(void) {
    pc_aspect();
    return sName;
}

float pc_aspect_ratio(void) {
    pc_aspect();
    return sRatio;
}

float pc_aspect_t(void) {
    const float a43 = kAspects[PC_ASPECT_4_3].ratio;
    return (pc_aspect_ratio() - a43) / (kAspects[PC_ASPECT_16_9].ratio - a43);
}

void pc_aspect_init(void) {
    pc_aspect();
    if (sBadValue != nullptr) {
        pc::writef(STDERR_FILENO, "[cos] COS_ASPECT=\"%s\" is not auto or W:H from 4:3 to 4:1 (e.g. 16:9, 21:9)\n",
                   sBadValue);
        pc_exit(PC_EXIT_USAGE);
    }
    if (pc_aspect_wide()) {
        pc::writef(STDERR_FILENO, "[cos] aspect: %s (widescreen, %.4f, t=%.4f)\n", pc_aspect_name(),
                   (double)pc_aspect_ratio(), (double)pc_aspect_t());
    }
}

} // extern "C"
