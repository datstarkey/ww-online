// Headless proof of libtww's world API (tww.h "world"), on tww_smoke's harness: no window, no focus taken.
// At frame `act` the game thread sets chest bits and memory switches in the current stage's live save data (the
// save window, as the client's world sync does) and posts them as a live-world batch, as the client does for bits
// another player set. The live pass then re-creates the matching create-only actors. PNGs at the shot frames.
//
//   clang++ -std=c++20 -fobjc-arc -framework AppKit -I native/include poc/tww_smoke/tww_world_smoke.mm -o build/poc-mac/tww_world_smoke
//   tww_world_smoke <libtww.dylib> <disc> <out dir> <frames> <shots a,b> <config> <act frame> <tbox mask> [switches a,b]
// WORLD_WALK=<from>-<to> holds the stick up (Link walks camera-forward) over those frames, to bring a target in view.
// "items" sets every item bit (mItem[0]) and names them all remote: every placed item with a bit vanishes.
// "probe" as the tbox mask posts every chest and switch bit without setting any: the log lists each create-only
// actor in the loaded rooms (re-created unchanged).
#include "pc/tww.h"

#import <AppKit/AppKit.h>
#include <dlfcn.h>

#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

namespace {

struct Api {
#define TWW_FN(name) decltype(&::name) name = nullptr;
    TWW_FN(tww_view_create) TWW_FN(tww_set_keys) TWW_FN(tww_init) TWW_FN(tww_run_frame) TWW_FN(tww_set_size) TWW_FN(tww_frame_count)
    TWW_FN(tww_save_surface_png) TWW_FN(tww_save_frame_png) TWW_FN(tww_set_fatal_callback) TWW_FN(tww_shutdown)
    TWW_FN(tww_get_location) TWW_FN(tww_get_local_state) TWW_FN(tww_save_read) TWW_FN(tww_save_write) TWW_FN(tww_world_save_slot)
    TWW_FN(tww_world_room_zone) TWW_FN(tww_world_salvage_read) TWW_FN(tww_world_get_stats) TWW_FN(tww_world_set_remote_items) TWW_FN(tww_world_live_post)
    TWW_FN(tww_world_live_status)
#undef TWW_FN
};

Api api;
std::atomic<int> sFatal{-1};

bool load(const char* path) {
    void* lib = dlopen(path, RTLD_NOW | RTLD_LOCAL);
    if (lib == nullptr) {
        fprintf(stderr, "world: %s\n", dlerror());
        return false;
    }
#define TWW_FN(name) api.name = reinterpret_cast<decltype(api.name)>(dlsym(lib, #name)); if (!api.name) return false;
    TWW_FN(tww_view_create) TWW_FN(tww_set_keys) TWW_FN(tww_init) TWW_FN(tww_run_frame) TWW_FN(tww_set_size) TWW_FN(tww_frame_count)
    TWW_FN(tww_save_surface_png) TWW_FN(tww_save_frame_png) TWW_FN(tww_set_fatal_callback) TWW_FN(tww_shutdown)
    TWW_FN(tww_get_location) TWW_FN(tww_get_local_state) TWW_FN(tww_save_read) TWW_FN(tww_save_write) TWW_FN(tww_world_save_slot)
    TWW_FN(tww_world_room_zone) TWW_FN(tww_world_salvage_read) TWW_FN(tww_world_get_stats) TWW_FN(tww_world_set_remote_items) TWW_FN(tww_world_live_post)
    TWW_FN(tww_world_live_status)
#undef TWW_FN
    return true;
}

const uint32_t kLiveMemory = 0x778; // dSv_info_c::mMemory: mTbox (BE u32), mSwitch[4] (BE u32)
const uint32_t kZones = 0x7A8;      // dSv_info_c::mZone[32], 0x4C each: s8 mRoomNo, BE u16 mSwitch[3]

uint32_t readU32(uint32_t offset) {
    uint8_t b[4] = {};
    api.tww_save_read(offset, b, 4);
    return (uint32_t)b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3];
}

void writeU32(uint32_t offset, uint32_t v) {
    const uint8_t b[4] = {(uint8_t)(v >> 24), (uint8_t)(v >> 16), (uint8_t)(v >> 8), (uint8_t)v};
    api.tww_save_write(offset, b, 4);
}

void printWorld(const char* when) {
    TwwLocalState local;
    api.tww_get_local_state(&local);
    const TwwLocation& loc = local.loc;
    const int32_t zone = api.tww_world_room_zone(loc.room);
    int8_t zoneRoom = -1;
    if (zone >= 0) {
        api.tww_save_read(kZones + (uint32_t)zone * 0x4C, &zoneRoom, 1);
    }
    TwwWorldSalvagePoint points[TWW_WORLD_SALVAGE_MAX];
    const uint32_t n = api.tww_world_salvage_read(points, TWW_WORLD_SALVAGE_MAX);
    uint32_t registered = 0;
    for (uint32_t i = 0; i < n; i++) {
        registered += points[i].room >= 0;
    }
    TwwWorldLiveStatus live;
    api.tww_world_live_status(&live);
    TwwWorldStats stats;
    api.tww_world_get_stats(&stats);
    fprintf(stderr, "world: %s frame %u Link (%.0f %.0f %.0f) stage \"%s\" room %d save slot %d zone %d (its room %d) tbox %08X switches %08X %08X "
                    "%08X %08X salvage %u/%u live seq %u done %u pokes %u retry %u despawns %u\n",
            when, api.tww_frame_count(), local.pos[0], local.pos[1], local.pos[2], loc.stage, loc.room, api.tww_world_save_slot(), zone, zoneRoom,
            readU32(kLiveMemory), readU32(kLiveMemory + 4), readU32(kLiveMemory + 8), readU32(kLiveMemory + 12),
            readU32(kLiveMemory + 16), registered, n, live.seq, live.done_seq, live.poke_count, live.retry,
            stats.despawn_count);
}

// The client's part: the bits land in the live save data first, then the batch names them.
void act(const char* tbox, const char* switches) {
    TwwWorldLiveBatch batch = {};
    batch.seq = 2;
    batch.slot = api.tww_world_save_slot();
    batch.zone_room = TWW_WORLD_ZONE_ROOM_NONE;
    if (strcmp(tbox, "items") == 0) {
        writeU32(kLiveMemory + 0x14, 0xFFFFFFFF);
        api.tww_world_set_remote_items(batch.slot, 0xFFFFFFFF);
        printWorld("items");
        return;
    }
    if (strcmp(tbox, "probe") == 0) {
        for (uint32_t& w : batch.bits) {
            w = 0xFFFFFFFF;
        }
    } else {
        batch.bits[0] = (uint32_t)strtoul(tbox, nullptr, 16);
        for (const char* p = switches; *p != '\0';) {
            const uint32_t sw = (uint32_t)strtoul(p, const_cast<char**>(&p), 16) & 0x7F;
            p += *p == ',' ? 1 : 0;
            batch.bits[1 + (sw >> 5)] |= 1u << (sw & 31);
        }
        writeU32(kLiveMemory, readU32(kLiveMemory) | batch.bits[0]);
        for (uint32_t w = 0; w < 4; w++) {
            writeU32(kLiveMemory + 4 + w * 4, readU32(kLiveMemory + 4 + w * 4) | batch.bits[1 + w]);
        }
    }
    api.tww_world_live_post(&batch);
    printWorld("posted");
}

} // namespace

int main(int argc, char* argv[]) {
    // A test tool never shows a window, a Dock icon or takes the focus, whatever the caller set.
    setenv("COS_HEADLESS", "1", 1);
    if (argc < 9) {
        fprintf(stderr, "usage: tww_world_smoke <libtww.dylib> <disc> <out dir> <frames> <shots> <config> <act frame> "
                        "<tbox mask|probe> [switches]\n");
        return 2;
    }
    const std::string out = argv[3];
    const unsigned int frames = (unsigned int)atoi(argv[4]);
    const unsigned int actFrame = (unsigned int)atoi(argv[7]);
    const char* switches = argc > 9 ? argv[9] : "";
    std::vector<unsigned int> shots;
    for (const char* p = argv[5]; *p != '\0';) {
        shots.push_back((unsigned int)strtoul(p, const_cast<char**>(&p), 10));
        p += *p == ',' ? 1 : 0;
    }

    std::atomic<bool> loaded{false}, viewReady{false}, done{false};
    std::atomic<void*> view{nullptr};
    int initResult = -1;
    std::thread game([&] {
        if (!load(argv[1])) {
            fprintf(stderr, "world: cannot load %s\n", argv[1]);
            exit(2);
        }
        api.tww_set_fatal_callback([](int code) { sFatal = code; });
        loaded = true;
        while (!viewReady) {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
        const std::string config = "COS_USER_DIR=" + out + "/user;COS_SYNC_PIPELINES=1;" + argv[6];
        initResult = api.tww_init(view.load(), argv[2], config.c_str());
        fprintf(stderr, "world: tww_init -> %d\n", initResult);
        unsigned int walkFrom = 0, walkTo = 0;
        if (const char* walk = getenv("WORLD_WALK")) {
            sscanf(walk, "%u-%u", &walkFrom, &walkTo);
        }
        while (initResult == 0 && api.tww_frame_count() < frames && sFatal < 0) {
            const unsigned int f = api.tww_frame_count() + 1;
            api.tww_set_keys(f >= walkFrom && f < walkTo ? TWW_KEY_STICK_UP : 0);
            api.tww_run_frame();
            if (f == actFrame) {
                act(argv[8], switches);
            } else if (f % 60 == 0) {
                printWorld("at");
            }
        }
        api.tww_shutdown();
        done = true;
    });

    @autoreleasepool {
        while (!loaded) {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
        view = api.tww_view_create();
        api.tww_set_size(960, 720, 2.0f);
        viewReady = true;
        size_t next = 0;
        while (!done) {
            if (next < shots.size() && api.tww_frame_count() >= shots[next]) {
                char path[1024];
                snprintf(path, sizeof(path), "%s/frame-%06u.png", out.c_str(), shots[next]);
                fprintf(stderr, "world: frame %u png %d\n", api.tww_frame_count(), api.tww_save_frame_png(path));
                next++;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
    }
    game.join();
    fprintf(stderr, "world: done at frame %u, fatal %d\n", api.tww_frame_count(), sFatal.load());
    fflush(stderr);
    _exit(initResult == 0 && sFatal < 0 ? 0 : 1);
}
