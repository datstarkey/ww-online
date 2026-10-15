// Headless smoke test of libtww (native/include/pc/tww.h): no window, no Dock icon, no focus taken.
// The view stays out of any window; a game thread loads the library, starts the game in it and runs
// frames; the main thread saves the hosted surface and the game's frame as PNGs at the frames asked.
//
//   clang++ -std=c++20 -fobjc-arc -framework AppKit -I native/include poc/tww_smoke/tww_smoke.mm -o build/poc-mac/tww_smoke
//   build/poc-mac/tww_smoke build/poc-mac/libtww.dylib <disc> <out dir> [frames=900] [shot frames=300,900]
//       [config, e.g. "COS_BOOT_STAGE=sea:44:8"] [peer | inv]
// Gate: TWW_SMOKE_GATE_STAGE="<frame>:<stage>:<point>:<room>:<layer>" changes the stage at that frame and
// TWW_SMOKE_GATE_WARP="<frame>:<dx>:<dz>" moves Link by (dx, dz) then; every shot prints the gate state.
// At each shot frame the game thread prints the multiplayer API's view (API version 2); "peer" also feeds
// peer slot 0 from the local player every frame, 150 units to the side, as COS_MP_TEST_PEER does.
// "inv" prints the shared-inventory API's view at each shot and, at the first, queues rupees, arrows,
// bombs, max life, max magic and a small key through it (tww_inv_add) for the later shots to show.
// "settings" lists the settings catalogue at the first shot (saved as <out dir>/settings-catalogue.json), flips a
// QoL toggle and COS_PAD_BACKGROUND through tww_settings_set, reads them back and checks settings.ini got the lines.
// An 8th argument <clothes>:<rrggbb> (e.g. 0:d02020) sets the local outfit; "peer" mirrors anims and look.
// "pvp" feeds slot 0 as "peer" does but 110 units ahead, makes it take part (tww_pvp_set_slots), prints every hit the
// local Link lands on it (tww_pvp_poll_hit), and at every shot after the first deals him a sword hit from it
// (tww_pvp_apply_hit) of TWW_SMOKE_PVP_DAMAGE quarter hearts (default 4) with TWW_SMOKE_PVP_SPL (default 0).
#include "pc/tww.h"

#import <AppKit/AppKit.h>
#include <dlfcn.h>

#include <atomic>
#include <cstdio>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

namespace {

struct Api {
#define TWW_FN(name) decltype(&::name) name = nullptr;
    TWW_FN(tww_api_version) TWW_FN(tww_view_create) TWW_FN(tww_init) TWW_FN(tww_run_frame) TWW_FN(tww_set_size)
    TWW_FN(tww_frame_count) TWW_FN(tww_present_count) TWW_FN(tww_save_surface_png) TWW_FN(tww_save_frame_png)
    TWW_FN(tww_set_fatal_callback) TWW_FN(tww_shutdown)
    TWW_FN(tww_get_local_state) TWW_FN(tww_get_location) TWW_FN(tww_set_peer) TWW_FN(tww_save_size)
    TWW_FN(tww_save_read) TWW_FN(tww_save_write)
    TWW_FN(tww_gate_get_state) TWW_FN(tww_gate_warp_local) TWW_FN(tww_gate_change_stage)
    TWW_FN(tww_inv_get) TWW_FN(tww_inv_add) TWW_FN(tww_inv_set_select_item)
    TWW_FN(tww_puppet_set_anim) TWW_FN(tww_puppet_set_look) TWW_FN(tww_puppet_get_local)
    TWW_FN(tww_puppet_set_local_outfit)
    TWW_FN(tww_settings_catalogue) TWW_FN(tww_settings_get) TWW_FN(tww_settings_set)
    TWW_FN(tww_pvp_set_slots) TWW_FN(tww_pvp_poll_hit) TWW_FN(tww_pvp_apply_hit)
#undef TWW_FN
};

Api api;
std::atomic<int> sFatal{-1};

bool load(const char* path) {
    void* lib = dlopen(path, RTLD_NOW | RTLD_LOCAL);
    if (lib == nullptr) {
        fprintf(stderr, "smoke: %s\n", dlerror());
        return false;
    }
#define TWW_LOAD(name) api.name = reinterpret_cast<decltype(api.name)>(dlsym(lib, #name)); if (!api.name) return false;
    TWW_LOAD(tww_api_version) TWW_LOAD(tww_view_create) TWW_LOAD(tww_init) TWW_LOAD(tww_run_frame)
    TWW_LOAD(tww_set_size) TWW_LOAD(tww_frame_count) TWW_LOAD(tww_present_count) TWW_LOAD(tww_save_surface_png)
    TWW_LOAD(tww_save_frame_png) TWW_LOAD(tww_set_fatal_callback) TWW_LOAD(tww_shutdown)
    TWW_LOAD(tww_get_local_state) TWW_LOAD(tww_get_location) TWW_LOAD(tww_set_peer) TWW_LOAD(tww_save_size)
    TWW_LOAD(tww_save_read) TWW_LOAD(tww_save_write)
    TWW_LOAD(tww_gate_get_state) TWW_LOAD(tww_gate_warp_local) TWW_LOAD(tww_gate_change_stage)
    TWW_LOAD(tww_inv_get) TWW_LOAD(tww_inv_add) TWW_LOAD(tww_inv_set_select_item)
    TWW_LOAD(tww_puppet_set_anim) TWW_LOAD(tww_puppet_set_look) TWW_LOAD(tww_puppet_get_local)
    TWW_LOAD(tww_puppet_set_local_outfit)
    TWW_LOAD(tww_settings_catalogue) TWW_LOAD(tww_settings_get) TWW_LOAD(tww_settings_set)
    TWW_LOAD(tww_pvp_set_slots) TWW_LOAD(tww_pvp_poll_hit) TWW_LOAD(tww_pvp_apply_hit)
#undef TWW_LOAD
    return true;
}

// The multiplayer API's view, on the game thread between frames. 0 if the save window round trip failed.
bool printMultiplayer() {
    TwwLocalState local;
    const int valid = api.tww_get_local_state(&local);
    fprintf(stderr, "smoke: local valid %d stage \"%s\" room %d layer %d point %d flags %u pos (%.1f %.1f %.1f) "
                    "angle (%d %d %d) speed %.2f proc %u\n",
            valid, local.loc.stage, local.loc.room, local.loc.layer, local.loc.point, local.loc.flags, local.pos[0],
            local.pos[1], local.pos[2], local.angle[0], local.angle[1], local.angle[2], local.speed_f, local.proc);
    // The save window: max life and life (big-endian u16 at 0), the player name at 0x15C
    // (dSv_player_info_c), then a host-order field after dSv_memory_c (mRestart's angle) written back
    // unchanged and read again.
    const uint32_t size = api.tww_save_size();
    uint8_t life[4] = {}, name[16] = {}, before[4], after[4];
    api.tww_save_read(0, life, sizeof(life));
    api.tww_save_read(0x15C, name, sizeof(name) - 1);
    const bool ok = api.tww_save_read(0x1128 + 0x16, before, 2) == 2 && api.tww_save_write(0x1128 + 0x16, before, 2) == 2 &&
                    api.tww_save_read(0x1128 + 0x16, after, 2) == 2 && memcmp(before, after, 2) == 0 &&
                    api.tww_save_read(size, after, 1) == 0;
    fprintf(stderr, "smoke: save size 0x%X life %d/%d name \"%s\" restart angle %02X%02X round trip %s\n", size,
            life[2] << 8 | life[3], life[0] << 8 | life[1], (char*)name, before[0], before[1], ok ? "ok" : "FAILED");
    return ok;
}

void printGate() {
    TwwGateState g;
    api.tww_gate_get_state(&g);
    fprintf(stderr, "smoke: gate stage \"%s\" start %d/%d/%d room %d link %u next %u event %u menu %u minigame %u "
                    "other %u status0 0x%08X mode 0x%08X\n",
            g.stage, g.start_point, g.start_room, g.start_layer, g.room, g.link, g.next_stage, g.event_mode,
            g.menu_paused, g.mini_game, g.other_character, g.player_status0, g.link_mode_flg);
}

// TWW_SMOKE_GATE_STAGE / TWW_SMOKE_GATE_WARP, once each at their frame.
void gateActions(unsigned int frame) {
    static bool stageDone, warpDone;
    char stage[8] = {};
    unsigned int at;
    int point, room, layer;
    const char* s = getenv("TWW_SMOKE_GATE_STAGE");
    if (!stageDone && s && sscanf(s, "%u:%7[^:]:%d:%d:%d", &at, stage, &point, &room, &layer) == 5 && frame >= at) {
        stageDone = true;
        fprintf(stderr, "smoke: gate change stage %s point %d room %d layer %d at frame %u -> %d\n", stage, point,
                room, layer, frame, api.tww_gate_change_stage(stage, (int16_t)point, (int8_t)room, (int8_t)layer));
    }
    float dx, dz;
    const char* w = getenv("TWW_SMOKE_GATE_WARP");
    TwwLocalState local;
    if (!warpDone && w && sscanf(w, "%u:%f:%f", &at, &dx, &dz) == 3 && frame >= at && api.tww_get_local_state(&local)) {
        warpDone = true;
        const float to[3] = {local.pos[0] + dx, local.pos[1], local.pos[2] + dz};
        const int ok = api.tww_gate_warp_local(to, (int16_t)(local.angle[1] + 0x4000));
        fprintf(stderr, "smoke: gate warp from (%.1f %.1f %.1f) to (%.1f %.1f %.1f) at frame %u -> %d\n", local.pos[0],
                local.pos[1], local.pos[2], to[0], to[1], to[2], frame, ok);
    }
}

// The shared-inventory view: play's pending counters and the save values they land in (big-endian:
// mMaxLife 0x0, mLife 0x2, rupees 0x4, mMaxMagic 0x13, arrows 0x69, bombs 0x6A, their maxima 0x6F / 0x70,
// spoils count 0 at 0x9C, the live mKeyNum 0x798). first: queue a change of each, and set mSelectItem[3] and
// read it back. Arrows and bombs apply only with a bow / bomb bag or when their maximum changes: it sets both
// maxima to 30 through the save window first.
bool printInventory(bool first) {
    TwwInvPlay inv;
    api.tww_inv_get(&inv);
    uint8_t save[0x800] = {};
    api.tww_save_read(0, save, sizeof(save));
    fprintf(stderr, "smoke: inv pending rupee %d key %d maxlife %d maxmagic %d arrow %d bomb %d beast0 %d | select %02X %02X "
                    "%02X | save_tbl %d key_disp %u\n",
            inv.pending[TWW_INV_RUPEE], inv.pending[TWW_INV_KEY], inv.pending[TWW_INV_MAX_LIFE], inv.pending[TWW_INV_MAX_MAGIC],
            inv.pending[TWW_INV_ARROW], inv.pending[TWW_INV_BOMB], inv.pending[TWW_INV_BEAST], inv.select_item[0],
            inv.select_item[1], inv.select_item[2], inv.save_tbl, inv.key_disp);
    fprintf(stderr, "smoke: inv save maxlife %d life %d rupees %d maxmagic %d arrows %d/%d bombs %d/%d spoils0 %d keys %d\n",
            save[0] << 8 | save[1], save[2] << 8 | save[3], save[4] << 8 | save[5], save[0x13], save[0x69], save[0x6F],
            save[0x6A], save[0x70], save[0x9C], save[0x798]);
    if (!first) {
        return true;
    }
    const uint8_t maxima[2] = {30, 30};
    api.tww_save_write(0x6F, maxima, sizeof(maxima));
    api.tww_inv_add(TWW_INV_RUPEE, 123);
    api.tww_inv_add(TWW_INV_ARROW, 10);
    api.tww_inv_add(TWW_INV_BOMB, 5);
    api.tww_inv_add(TWW_INV_MAX_LIFE, 8);
    api.tww_inv_add(TWW_INV_MAX_MAGIC, 16);
    api.tww_inv_add(TWW_INV_KEY, 2);
    api.tww_inv_add(TWW_INV_BEAST, 3);
    const uint8_t old = inv.select_item[3];
    api.tww_inv_set_select_item(3, 0x5A);
    api.tww_inv_get(&inv);
    const bool ok = inv.select_item[3] == 0x5A && inv.pending[TWW_INV_RUPEE] >= 123;
    api.tww_inv_set_select_item(3, old);
    fprintf(stderr, "smoke: inv queued, select item round trip %s\n", ok ? "ok" : "FAILED");
    return ok;
}

std::string settingValue(const char* key) {
    char value[256] = {};
    return api.tww_settings_get(key, value, sizeof(value)) != 0 ? value : "<unknown>";
}

// The settings API: the catalogue, live toggles set and read back, and their lines in settings.ini.
bool checkSettings(const std::string& out) {
    std::string json(api.tww_settings_catalogue(nullptr, 0), '\0');
    api.tww_settings_catalogue(json.data(), (uint32_t)json.size());
    json.pop_back();
    if (FILE* f = fopen((out + "/settings-catalogue.json").c_str(), "w")) {
        fputs(json.c_str(), f);
        fclose(f);
    }
    int count = 0, bindings = 0, restart = 0;
    for (size_t at = 0; (at = json.find("\"key\":", at)) != std::string::npos; at++) {
        count++;
    }
    for (size_t at = 0; (at = json.find("\"type\":\"binding\"", at)) != std::string::npos; at++) {
        bindings++;
    }
    for (size_t at = 0; (at = json.find("\"restart\":true", at)) != std::string::npos; at++) {
        restart++;
    }
    fprintf(stderr, "smoke: settings catalogue %zu bytes, %d settings (%d bindings, %d need a restart)\n", json.size(),
            count, bindings, restart);
    // A toggle set, read back and saved: a QoL one and the controls' background pad.
    std::string ini;
    const auto flip = [&](const char* key) {
        const std::string before = settingValue(key);
        const char* want = before == "1" ? "0" : "1";
        const int set = api.tww_settings_set(key, want);
        const std::string after = settingValue(key);
        ini.clear();
        if (FILE* f = fopen((out + "/user/settings.ini").c_str(), "r")) {
            char line[512];
            while (fgets(line, sizeof(line), f) != nullptr) {
                ini += line;
            }
            fclose(f);
        }
        const bool saved = ini.find(std::string(key) + "=" + want + "\n") != std::string::npos;
        const int bad = api.tww_settings_set(key, "maybe");
        fprintf(stderr, "smoke: settings %s %s -> set %s = %d, read back %s, settings.ini %s; invalid value -> %d\n", key,
                before.c_str(), want, set, after.c_str(), saved ? "has the line" : "MISSING the line", bad);
        return set == 1 && after == want && saved && bad == 0;
    };
    const bool flipped = flip("COS_QOL_INSTANT_TEXT") && flip("COS_PAD_BACKGROUND");
    const std::string keyR = settingValue("COS_BIND_KEY_R");
    fprintf(stderr, "smoke: settings COS_BIND_KEY_R = %s\n", keyR.c_str());
    const bool ok = count > 0 && bindings > 0 && flipped && keyR == "E";
    fprintf(stderr, "smoke: settings %s\n", ok ? "ok" : "FAILED");
    return ok;
}

TwwPeerState sPeer;

void feedPeer(float right, float ahead) {
    TwwLocalState local;
    if (!api.tww_get_local_state(&local)) {
        api.tww_set_peer(0, nullptr);
        return;
    }
    TwwPeerState& peer = sPeer;
    peer = {};
    const float yaw = local.angle[1] * 3.14159265f / 32768.0f;
    peer.pos[0] = local.pos[0] - right * cosf(yaw) + ahead * sinf(yaw);
    peer.pos[1] = local.pos[1];
    peer.pos[2] = local.pos[2] + right * sinf(yaw) + ahead * cosf(yaw);
    memcpy(peer.angle, local.angle, sizeof(peer.angle));
    peer.speed_f = local.speed_f;
    peer.proc = local.proc;
    memcpy(peer.stage, local.loc.stage, sizeof(peer.stage));
    peer.room = local.loc.room;
    api.tww_set_peer(0, &peer);
    TwwPuppetAnim anim;
    TwwPuppetLook look;
    api.tww_puppet_get_local(&anim, &look);
    api.tww_puppet_set_anim(0, &anim);
    api.tww_puppet_set_look(0, &look);
}

void pollPvp() {
    TwwPvpHit hit;
    while (api.tww_pvp_poll_hit(&hit)) {
        fprintf(stderr, "smoke: pvp polled a hit on slot %u: type 0x%X, %u quarter hearts, spl %u at (%.1f %.1f %.1f)\n",
                hit.slot, hit.at_type, hit.damage, hit.spl, hit.pos[0], hit.pos[1], hit.pos[2]);
    }
}

void applyPvp() {
    const char* d = getenv("TWW_SMOKE_PVP_DAMAGE");
    const char* spl = getenv("TWW_SMOKE_PVP_SPL");
    TwwPvpHit hit = {};
    hit.slot = 0;
    hit.damage = (uint8_t)(d != nullptr ? atoi(d) : 4);
    hit.spl = (uint8_t)(spl != nullptr ? atoi(spl) : 0);
    hit.at_type = 0x2; // AT_TYPE_SWORD
    memcpy(hit.pos, sPeer.pos, sizeof(hit.pos));
    memcpy(hit.attacker_pos, sPeer.pos, sizeof(hit.attacker_pos));
    fprintf(stderr, "smoke: pvp apply a sword hit from slot 0 (%u quarter hearts, spl %u) -> %d\n", hit.damage, hit.spl,
            api.tww_pvp_apply_hit(&hit));
}

} // namespace

int main(int argc, char* argv[]) {
    // A test tool never shows a window, a Dock icon or takes the focus, whatever the caller set.
    setenv("COS_HEADLESS", "1", 1);
    if (argc < 4) {
        fprintf(stderr, "usage: tww_smoke <libtww.dylib> <disc> <out dir> [frames] [shot frames a,b]\n");
        return 2;
    }
    const std::string out = argv[3];
    const unsigned int frames = argc > 4 ? (unsigned int)atoi(argv[4]) : 900;
    const std::string extraConfig = argc > 6 ? argv[6] : "";
    const bool pvp = argc > 7 && strcmp(argv[7], "pvp") == 0;
    const bool peer = pvp || (argc > 7 && strcmp(argv[7], "peer") == 0);
    const bool inv = argc > 7 && strcmp(argv[7], "inv") == 0;
    const bool settings = argc > 7 && strcmp(argv[7], "settings") == 0;
    unsigned int outfitClothes = TWW_PUPPET_CLOTHES_DEFAULT, outfitRgb = 0;
    const bool outfit = argc > 8 && sscanf(argv[8], "%u:%x", &outfitClothes, &outfitRgb) == 2;
    std::vector<unsigned int> shots;
    for (const char* p = argc > 5 ? argv[5] : "300,900"; *p != '\0';) {
        shots.push_back((unsigned int)strtoul(p, const_cast<char**>(&p), 10));
        p += *p == ',' ? 1 : 0;
    }

    std::atomic<bool> loaded{false}, viewReady{false}, done{false}, mpFailed{false};
    std::atomic<void*> view{nullptr};
    int initResult = -1;
    // The game thread loads the library first, so its static initialisers and the SDK's main thread are its.
    std::thread game([&] {
        if (!load(argv[1])) {
            fprintf(stderr, "smoke: cannot load %s\n", argv[1]);
            exit(2);
        }
        api.tww_set_fatal_callback([](int code) { sFatal = code; });
        loaded = true;
        while (!viewReady) {
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
        const std::string config = "COS_USER_DIR=" + out + "/user;COS_SYNC_PIPELINES=1;" + extraConfig;
        initResult = api.tww_init(view.load(), argv[2], config.c_str());
        fprintf(stderr, "smoke: tww_init -> %d\n", initResult);
        if (outfit) {
            api.tww_puppet_set_local_outfit((uint8_t)outfitClothes, (uint8_t)(outfitRgb >> 16), (uint8_t)(outfitRgb >> 8),
                                            (uint8_t)outfitRgb);
        }
        size_t nextPrint = 0;
        while (initResult == 0 && api.tww_frame_count() < frames && sFatal < 0) {
            api.tww_run_frame();
            if (peer) {
                feedPeer(pvp ? 0.0f : 150.0f, pvp ? 110.0f : 0.0f);
            }
            if (pvp) {
                api.tww_pvp_set_slots(1);
                pollPvp();
            }
            gateActions(api.tww_frame_count());
            // TWW_SMOKE_RESIZE="<frame>:<w>:<h>": the host resizes the view then (points, scale 2).
            if (const char* r = getenv("TWW_SMOKE_RESIZE"); r != nullptr) {
                unsigned int at, w, h;
                if (sscanf(r, "%u:%u:%u", &at, &w, &h) == 3 && api.tww_frame_count() == at) {
                    api.tww_set_size(w, h, 2.0f);
                }
            }
            if (nextPrint < shots.size() && api.tww_frame_count() >= shots[nextPrint]) {
                printGate();
                mpFailed = mpFailed || !printMultiplayer() || (inv && !printInventory(nextPrint == 0)) ||
                           (settings && nextPrint == 0 && !checkSettings(out));
                if (pvp && nextPrint > 0) {
                    applyPvp();
                }
                nextPrint++;
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
                snprintf(path, sizeof(path), "%s/surface-%06u.png", out.c_str(), shots[next]);
                const int s = api.tww_save_surface_png(path);
                snprintf(path, sizeof(path), "%s/frame-%06u.png", out.c_str(), shots[next]);
                const int f = api.tww_save_frame_png(path);
                fprintf(stderr, "smoke: frame %u presents %llu surface png %d frame png %d\n", api.tww_frame_count(),
                        (unsigned long long)api.tww_present_count(), s, f);
                next++;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
    }
    game.join();
    fprintf(stderr, "smoke: done at frame %u, presents %llu, fatal %d\n", api.tww_frame_count(),
            (unsigned long long)api.tww_present_count(), sFatal.load());
    fflush(stderr);
    _exit(initResult == 0 && sFatal < 0 && !mpFailed ? 0 : 1);
}
