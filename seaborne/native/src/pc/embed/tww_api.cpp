// libtww's C API (native/include/pc/tww.h): the game as a shared library whose host drives the frames.
// tww_init runs the game's main (pc_harness_init, pc_aurora_init against the host's CAMetalLayer, the
// boot up to main01_init: m_Do_main.cpp returns there when pc_embedded()); tww_run_frame is one
// iteration of main01's loop. The keyboard pad is Aurora's virtual pad on port 0.
#include "pc/tww.h"
#include "pc/pc_harness.h"

#include <aurora/aurora.h>
#include <dolphin/pad.h>
#include <webgpu/webgpu.h>

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

void main01_frame();                                                                        // m_Do_main.cpp
extern "C" int aurora_main(int argc, char* argv[]);                                         // the game's main
extern "C" unsigned int pc_debug_shot_request(const char* path, int overlay);              // pc_shot.cpp
extern "C" unsigned int pc_debug_shot_done(int* ok);                                       // pc_shot.cpp
extern "C" int pc_write_png_rgb(const char* path, const uint8_t* rgb, uint32_t width, uint32_t height); // pc_shot.cpp
void* tww_view_layer(void* view);                                                           // tww_view.mm
void tww_settings_before_init();                                                            // tww_settings.cpp
void tww_settings_after_init();                                                             // tww_settings.cpp

namespace {

std::atomic<uint32_t> sKeys{0};
std::atomic<uint32_t> sWidth{640}, sHeight{480};
std::atomic<float> sScale{1.f};
std::atomic<void (*)(int)> sFatal{nullptr};
bool sStarted = false;
bool sStopped = false;

void fatal(int code) {
    if (auto fn = sFatal.load()) {
        fn(code);
    }
}

// "KEY=VALUE" pairs separated by newlines or ';' into the environment, which the harness reads.
void applyConfig(const char* config) {
    if (config == nullptr) {
        return;
    }
    std::string all(config);
    size_t begin = 0;
    while (begin <= all.size()) {
        size_t end = all.find_first_of(";\n", begin);
        if (end == std::string::npos) {
            end = all.size();
        }
        const std::string item = all.substr(begin, end - begin);
        const size_t eq = item.find('=');
        if (eq != std::string::npos && eq > 0) {
            setenv(item.substr(0, eq).c_str(), item.substr(eq + 1).c_str(), 1);
        }
        begin = end + 1;
    }
}

PADStatus padFromKeys(uint32_t k) {
    PADStatus s{};
    auto axis = [k](uint32_t neg, uint32_t pos) -> s8 { return (s8)(((k & pos) ? 100 : 0) - ((k & neg) ? 100 : 0)); };
    s.stickX = axis(TWW_KEY_STICK_LEFT, TWW_KEY_STICK_RIGHT);
    s.stickY = axis(TWW_KEY_STICK_DOWN, TWW_KEY_STICK_UP);
    s.substickX = axis(TWW_KEY_C_LEFT, TWW_KEY_C_RIGHT);
    s.substickY = axis(TWW_KEY_C_DOWN, TWW_KEY_C_UP);
    static const struct {
        uint32_t key;
        u16 button;
    } kButtons[] = {
        {TWW_KEY_A, PAD_BUTTON_A},          {TWW_KEY_B, PAD_BUTTON_B},           {TWW_KEY_X, PAD_BUTTON_X},
        {TWW_KEY_Y, PAD_BUTTON_Y},          {TWW_KEY_Z, PAD_TRIGGER_Z},          {TWW_KEY_L, PAD_TRIGGER_L},
        {TWW_KEY_R, PAD_TRIGGER_R},         {TWW_KEY_START, PAD_BUTTON_START},   {TWW_KEY_DPAD_UP, PAD_BUTTON_UP},
        {TWW_KEY_DPAD_DOWN, PAD_BUTTON_DOWN}, {TWW_KEY_DPAD_LEFT, PAD_BUTTON_LEFT}, {TWW_KEY_DPAD_RIGHT, PAD_BUTTON_RIGHT},
    };
    for (const auto& b : kButtons) {
        if (k & b.key) {
            s.button |= b.button;
        }
    }
    s.triggerLeft = (k & TWW_KEY_L) ? 180 : 0;
    s.triggerRight = (k & TWW_KEY_R) ? 180 : 0;
    s.analogA = (k & TWW_KEY_A) ? 255 : 0;
    s.analogB = (k & TWW_KEY_B) ? 255 : 0;
    return s;
}

// Waits up to 5 s for done(); 1 when it held.
template <typename F>
bool waitFor(F done) {
    const auto until = std::chrono::steady_clock::now() + std::chrono::seconds(5);
    while (!done()) {
        if (std::chrono::steady_clock::now() > until) {
            return false;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    return true;
}

struct SurfaceShot {
    std::string path;
    std::atomic<int> result{-1}; // -1 pending, 0 failed, 1 written
};

// On Aurora's render worker: the surface texels (BGRA or RGBA, 8 bits) to an RGB PNG.
void surfaceShotDone(void* user, const uint8_t* texels, uint32_t width, uint32_t height, uint32_t bytesPerRow,
                     uint32_t format) {
    std::shared_ptr<SurfaceShot> shot(*static_cast<std::shared_ptr<SurfaceShot>*>(user));
    delete static_cast<std::shared_ptr<SurfaceShot>*>(user);
    const bool bgra = format == WGPUTextureFormat_BGRA8Unorm || format == WGPUTextureFormat_BGRA8UnormSrgb;
    const bool rgba = format == WGPUTextureFormat_RGBA8Unorm || format == WGPUTextureFormat_RGBA8UnormSrgb;
    if (texels == nullptr || (!bgra && !rgba)) {
        fprintf(stderr, "[tww] surface readback: no texels or format %u not 8-bit RGBA/BGRA\n", format);
        shot->result = 0;
        return;
    }
    std::vector<uint8_t> rgb((size_t)width * height * 3);
    for (uint32_t y = 0; y < height; y++) {
        const uint8_t* row = texels + (size_t)y * bytesPerRow;
        uint8_t* out = rgb.data() + (size_t)y * width * 3;
        for (uint32_t x = 0; x < width; x++, row += 4, out += 3) {
            out[0] = bgra ? row[2] : row[0];
            out[1] = row[1];
            out[2] = bgra ? row[0] : row[2];
        }
    }
    shot->result = pc_write_png_rgb(shot->path.c_str(), rgb.data(), width, height);
}

} // namespace

extern "C" {

uint32_t tww_api_version(void) {
    return TWW_API_VERSION;
}

int tww_init(void* view, const char* disc, const char* config) {
    if (sStarted) {
        fprintf(stderr, "[tww] tww_init: the game starts once per process\n");
        return PC_EXIT_USAGE;
    }
    void* layer = tww_view_layer(view);
    if (layer == nullptr) {
        fprintf(stderr, "[tww] tww_init: the view has no CAMetalLayer (use tww_view_create)\n");
        return PC_EXIT_USAGE;
    }
    sStarted = true;
    applyConfig(config);
    if (disc != nullptr) {
        setenv("COS_DISC", disc, 1);
    }
    // The options menu's settings file next to the saves (pc_settings.cpp, otherwise next to the executable).
    if (const char* user = getenv("COS_USER_DIR"); user != nullptr && user[0] != '\0' && getenv("COS_SETTINGS") == nullptr) {
        setenv("COS_SETTINGS", (std::string(user) + "/settings.ini").c_str(), 1);
    }
    pc_embed_configure(layer, sWidth.load(), sHeight.load(), sScale.load(), fatal);
    tww_settings_before_init();
    char arg0[] = "libtww";
    char* argv[] = {arg0, nullptr};
    aurora_main(1, argv);
    tww_settings_after_init();
    // The keyboard pad: port 0 reads as connected from the first frame.
    const PADStatus idle = padFromKeys(0);
    PADSetVirtualStatus(0, &idle);
    return 0;
}

int tww_run_frame(void) {
    if (sStopped) {
        return 1;
    }
    const PADStatus pad = padFromKeys(sKeys.load(std::memory_order_relaxed));
    PADSetVirtualStatus(0, &pad);
    main01_frame();
    return pc_embed_quit_requested();
}

void tww_shutdown(void) {
    // The last frame on the GPU; no more frames after this. The game's threads and Aurora stay up:
    // the host ends the process (its static destructors assume a game that never ends; use _exit).
    sStopped = true;
    aurora_frame_sync();
}

void tww_set_size(uint32_t width, uint32_t height, float scale) {
    sWidth = width;
    sHeight = height;
    sScale = scale;
    aurora_set_external_size(width, height, scale);
}

void tww_set_keys(uint32_t keys) {
    sKeys.store(keys, std::memory_order_relaxed);
}

void tww_set_fatal_callback(void (*fn)(int code)) {
    sFatal = fn;
}

uint32_t tww_frame_count(void) {
    return pc_frame_count();
}

uint64_t tww_present_count(void) {
    return aurora_get_external_present_count();
}

int tww_save_surface_png(const char* path) {
    auto shot = std::make_shared<SurfaceShot>();
    shot->path = path;
    aurora_request_surface_readback(surfaceShotDone, new std::shared_ptr<SurfaceShot>(shot));
    return waitFor([&] { return shot->result.load() >= 0; }) ? shot->result.load() : 0;
}

int tww_save_frame_png(const char* path) {
    const unsigned int ticket = pc_debug_shot_request(path, 0);
    int ok = 0;
    return waitFor([&] { return pc_debug_shot_done(&ok) >= ticket; }) && ok ? 1 : 0;
}

} // extern "C"
