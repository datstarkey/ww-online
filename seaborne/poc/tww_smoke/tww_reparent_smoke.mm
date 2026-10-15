// Headless proof that libtww's view survives a host moving it between windows, as the client's game pop-out does
// (Avalonia re-parents the one view between its main window and the pop-out, then frames it): no Dock icon, no
// focus taken, silent (COS_HEADLESS's dummy audio driver). The two windows are borderless, off screen and never
// ordered in; the process cannot activate. The view starts in window A at 960x720; at each step it holds keys
// (key downs sent to it as its window's first responder), moves to the other window and gets a new frame (only
// setFrame: the view reports its own size). After each move no key may stay held, and a surface PNG must come out
// at the new aspect while the game keeps presenting.
//
//   clang++ -std=c++20 -fobjc-arc -framework AppKit -framework Carbon -I native/include poc/tww_smoke/tww_reparent_smoke.mm -o build/poc-mac/tww_reparent_smoke
//   tww_reparent_smoke <libtww.dylib> <disc> <out dir>
#include "pc/tww.h"

#import <AppKit/AppKit.h>
#import <Carbon/Carbon.h>
#include <dlfcn.h>

#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <string>
#include <thread>

namespace {

#define TWW_FN(name) decltype(&::name) p_##name = nullptr;
TWW_FN(tww_view_create) TWW_FN(tww_init) TWW_FN(tww_run_frame) TWW_FN(tww_frame_count) TWW_FN(tww_present_count)
TWW_FN(tww_save_surface_png) TWW_FN(tww_set_fatal_callback) TWW_FN(tww_shutdown)
#undef TWW_FN
std::atomic<int> sFatal{-1};

struct Step {
    unsigned int at;
    int window; // 0 = A, 1 = B
    double w, h;
};
constexpr Step kSteps[] = {{300, 0, 960, 720}, {420, 1, 1280, 720}, {540, 0, 720, 900}, {660, 1, 1600, 1000}};
constexpr int kStepCount = sizeof(kSteps) / sizeof(kSteps[0]);
constexpr unsigned int kSettle = 90;

NSWindow* hiddenWindow() {
    NSWindow* window = [[NSWindow alloc] initWithContentRect:NSMakeRect(-30000, -30000, 2000, 2000)
                                                   styleMask:NSWindowStyleMaskBorderless
                                                     backing:NSBackingStoreBuffered
                                                       defer:YES];
    window.releasedWhenClosed = NO;
    return window;
}

void sendKey(NSView* view, unsigned short keyCode, BOOL down) {
    NSEvent* event = [NSEvent keyEventWithType:down ? NSEventTypeKeyDown : NSEventTypeKeyUp
                                      location:NSZeroPoint
                                 modifierFlags:0
                                     timestamp:0
                                  windowNumber:view.window.windowNumber
                                       context:nil
                                    characters:@" "
                   charactersIgnoringModifiers:@" "
                                     isARepeat:NO
                                       keyCode:keyCode];
    if (down) {
        [view keyDown:event];
    } else {
        [view keyUp:event];
    }
}

// The pad bits the view holds (what it last gave tww_set_keys).
unsigned int heldKeys(NSView* view) {
    return [[view valueForKey:@"keys"] unsignedIntValue];
}

} // namespace

int main(int argc, char* argv[]) {
    setenv("COS_HEADLESS", "1", 1);
    setenv("SDL_AUDIO_DRIVER", "dummy", 1);
    if (argc < 4) {
        fprintf(stderr, "usage: tww_reparent_smoke <libtww.dylib> <disc> <out dir>\n");
        return 2;
    }
    const std::string out = argv[3];
    void* lib = dlopen(argv[1], RTLD_NOW | RTLD_LOCAL);
    if (lib == nullptr) {
        fprintf(stderr, "reparent: %s\n", dlerror());
        return 2;
    }
#define TWW_LOAD(name) p_##name = reinterpret_cast<decltype(p_##name)>(dlsym(lib, #name)); if (!p_##name) return 2;
    TWW_LOAD(tww_view_create) TWW_LOAD(tww_init) TWW_LOAD(tww_run_frame) TWW_LOAD(tww_frame_count)
    TWW_LOAD(tww_present_count) TWW_LOAD(tww_save_surface_png) TWW_LOAD(tww_set_fatal_callback) TWW_LOAD(tww_shutdown)
#undef TWW_LOAD
    p_tww_set_fatal_callback([](int code) { sFatal = code; });

    std::atomic<bool> viewReady{false}, done{false};
    std::atomic<void*> view{nullptr};
    std::atomic<int> initResult{-1};
    std::thread game([&] {
        while (!viewReady) {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
        const std::string config = "COS_USER_DIR=" + out + "/user;COS_SYNC_PIPELINES=1";
        initResult = p_tww_init(view.load(), argv[2], config.c_str());
        fprintf(stderr, "reparent: tww_init -> %d\n", initResult.load());
        while (initResult == 0 && p_tww_frame_count() < kSteps[kStepCount - 1].at + kSettle + 30 && sFatal < 0) {
            p_tww_run_frame();
        }
        p_tww_shutdown();
        done = true;
    });

    int failures = 0;
    @autoreleasepool {
        [NSApplication sharedApplication];
        [NSApp setActivationPolicy:NSApplicationActivationPolicyProhibited];
        NSWindow* windows[2] = {hiddenWindow(), hiddenWindow()};
        void* v = p_tww_view_create();
        NSView* nsView = (__bridge NSView*)v;
        [windows[0].contentView addSubview:nsView];
        nsView.frame = NSMakeRect(0, 0, kSteps[0].w, kSteps[0].h);
        view = v;
        viewReady = true;

        int cur = 0, shots = 0;
        bool shotPending = true;
        unsigned int shotAt = kSteps[0].at;
        unsigned long long lastPresents = 0;
        while (!done) {
            const unsigned int frame = p_tww_frame_count();
            if (!shotPending && cur + 1 < kStepCount && frame >= kSteps[cur + 1].at) {
                const Step& s = kSteps[++cur];
                [nsView.window makeFirstResponder:nsView];
                sendKey(nsView, kVK_Space, YES);
                sendKey(nsView, kVK_UpArrow, YES);
                const unsigned int before = heldKeys(nsView);
                [nsView removeFromSuperview];
                [windows[s.window].contentView addSubview:nsView];
                nsView.frame = NSMakeRect(0, 0, s.w, s.h);
                const unsigned int after = heldKeys(nsView);
                const bool released = before != 0 && after == 0;
                fprintf(stderr, "reparent: frame %u moved to window %c, frame %.0fx%.0f (in it %s); keys held 0x%x -> 0x%x %s\n",
                        frame, 'A' + s.window, s.w, s.h, nsView.window == windows[s.window] ? "yes" : "NO", before, after,
                        released ? "ok" : "FAILED");
                failures += released ? 0 : 1;
                sendKey(nsView, kVK_Space, NO);
                sendKey(nsView, kVK_UpArrow, NO);
                shotPending = true;
                shotAt = s.at + kSettle;
            }
            if (shotPending && frame >= shotAt) {
                const Step& s = kSteps[cur];
                char path[1024];
                snprintf(path, sizeof(path), "%s/surface-step%d.png", out.c_str(), cur);
                const int ok = p_tww_save_surface_png(path);
                const unsigned long long presents = p_tww_present_count();
                NSImage* img = [[NSImage alloc] initWithContentsOfFile:[NSString stringWithUTF8String:path]];
                NSBitmapImageRep* rep = img != nil ? (NSBitmapImageRep*)img.representations.firstObject : nil;
                const long pw = rep != nil ? (long)rep.pixelsWide : -1, ph = rep != nil ? (long)rep.pixelsHigh : -1;
                const double aspect = ph > 0 ? (double)pw / ph : 0;
                const bool good =
                    ok == 1 && presents > lastPresents && aspect > s.w / s.h - 0.01 && aspect < s.w / s.h + 0.01;
                fprintf(stderr, "reparent: frame %u step %d surface %ldx%ld (scale %.2f) for %.0fx%.0f, presents %llu -> %s\n",
                        frame, cur, pw, ph, pw > 0 ? pw / s.w : 0.0, s.w, s.h, presents, good ? "ok" : "FAILED");
                failures += good ? 0 : 1;
                lastPresents = presents;
                shotPending = false;
                shots++;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        failures += kStepCount - shots;
    }
    game.join();
    fprintf(stderr, "reparent: done at frame %u, presents %llu, fatal %d, failures %d\n", p_tww_frame_count(),
            (unsigned long long)p_tww_present_count(), sFatal.load(), failures);
    fflush(stderr);
    _exit(initResult == 0 && sFatal < 0 && failures == 0 ? 0 : 1);
}
