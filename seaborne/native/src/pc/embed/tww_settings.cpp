// libtww's settings API (native/include/pc/tww.h, "settings"): the options menu's settings (pc_settings.h, the
// rows pc_menu.cpp and pc_bindings.cpp register) for the host's own settings page, as JSON and by key. A change
// goes through pc_settings_set like the menu's, so it is saved to settings.ini and applied by the same apply
// functions and subscribers.
//
// The embedded keyboard: the view (tww_view.mm) turns key positions into SDL scancodes, and the keyboard bindings
// (COS_BIND_KEY_*) into the pad bits of tww_set_keys, with the view's own layout as their default.
#include "pc/tww.h"
#include "pc/pc_bindings.h"
#include "pc/pc_settings.h"

#include <SDL3/SDL_gamepad.h>
#include <SDL3/SDL_keyboard.h>
#include <SDL3/SDL_scancode.h>

#include <atomic>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

int tww_view_key_scancodes(int* out, int max); // tww_view.mm

namespace {

// pc_bindings.cpp's rows, in its order: the twelve buttons (the gamepad's too), then the eight stick directions.
struct KeyRow {
    const char* row;
    uint32_t bit;
    SDL_Scancode embedDefault;
};

constexpr KeyRow kKeyRows[] = {
    {"A", TWW_KEY_A, SDL_SCANCODE_J},
    {"B", TWW_KEY_B, SDL_SCANCODE_K},
    {"X", TWW_KEY_X, SDL_SCANCODE_L},
    {"Y", TWW_KEY_Y, SDL_SCANCODE_I},
    {"Z", TWW_KEY_Z, SDL_SCANCODE_U},
    {"L", TWW_KEY_L, SDL_SCANCODE_Q},
    {"R", TWW_KEY_R, SDL_SCANCODE_E},
    {"START", TWW_KEY_START, SDL_SCANCODE_RETURN},
    {"UP", TWW_KEY_DPAD_UP, SDL_SCANCODE_1},
    {"DOWN", TWW_KEY_DPAD_DOWN, SDL_SCANCODE_2},
    {"LEFT", TWW_KEY_DPAD_LEFT, SDL_SCANCODE_3},
    {"RIGHT", TWW_KEY_DPAD_RIGHT, SDL_SCANCODE_4},
    {"LSTICK_UP", TWW_KEY_STICK_UP, SDL_SCANCODE_W},
    {"LSTICK_DOWN", TWW_KEY_STICK_DOWN, SDL_SCANCODE_S},
    {"LSTICK_LEFT", TWW_KEY_STICK_LEFT, SDL_SCANCODE_A},
    {"LSTICK_RIGHT", TWW_KEY_STICK_RIGHT, SDL_SCANCODE_D},
    {"CSTICK_UP", TWW_KEY_C_UP, SDL_SCANCODE_UP},
    {"CSTICK_DOWN", TWW_KEY_C_DOWN, SDL_SCANCODE_DOWN},
    {"CSTICK_LEFT", TWW_KEY_C_LEFT, SDL_SCANCODE_LEFT},
    {"CSTICK_RIGHT", TWW_KEY_C_RIGHT, SDL_SCANCODE_RIGHT},
};
constexpr int kKeyRowCount = (int)(sizeof(kKeyRows) / sizeof(kKeyRows[0]));
constexpr int kPadRowCount = 12;

constexpr const char kKeyPrefix[] = "COS_BIND_KEY_";
constexpr const char kPadPrefix[] = "COS_BIND_PAD_";

// The scancode each keyboard row is bound to (-1: none); the view reads it on the UI thread.
std::atomic<int> sRowScancode[kKeyRowCount];

// The row of a binding key ("COS_BIND_KEY_A" -> 0), -1 if it is not one of the device's.
int rowOf(const char* key, const char* prefix, int count) {
    const size_t n = strlen(prefix);
    if (key == nullptr || strncmp(key, prefix, n) != 0) {
        return -1;
    }
    for (int i = 0; i < count; i++) {
        if (strcmp(key + n, kKeyRows[i].row) == 0) {
            return i;
        }
    }
    return -1;
}

int scancodeOf(const char* value) {
    const SDL_Scancode sc = value[0] != '\0' && strcmp(value, "none") != 0 ? SDL_GetScancodeFromName(value)
                                                                           : SDL_SCANCODE_UNKNOWN;
    return sc != SDL_SCANCODE_UNKNOWN ? (int)sc : -1;
}

void onKeyBinding(const char* key, const char* value, void*) {
    const int row = rowOf(key, kKeyPrefix, kKeyRowCount);
    if (row >= 0) {
        sRowScancode[row] = scancodeOf(value);
    }
}

// ---- the catalogue -------------------------------------------------------------------------------

const char* const kTabIds[PC_SETTING_TABS] = {"graphics", "performance", "qol", "controls", "debug"};
const char* const kTabLabels[PC_SETTING_TABS] = {"Graphics", "Performance", "Quality of life", "Controls", "Debug"};

// The gamepad's buttons as COS_BIND_PAD_* names them (SDL_GetGamepadStringForButton), named by position.
const struct {
    const char* value;
    const char* label;
} kPadLabels[] = {
    {"a", "South face button"},        {"b", "East face button"},     {"x", "West face button"},
    {"y", "North face button"},        {"back", "Back / Minus"},      {"guide", "Home"},
    {"start", "Start / Plus"},         {"leftstick", "Left stick press"}, {"rightstick", "Right stick press"},
    {"leftshoulder", "Left shoulder"}, {"rightshoulder", "Right shoulder"}, {"dpup", "D-pad up"},
    {"dpdown", "D-pad down"},          {"dpleft", "D-pad left"},      {"dpright", "D-pad right"},
};

struct Choice {
    std::string value, label;
};

void appendJson(std::string& out, const char* s) {
    out += '"';
    for (const char* p = s != nullptr ? s : ""; *p != '\0'; p++) {
        const unsigned char c = (unsigned char)*p;
        if (c == '"' || c == '\\') {
            out += '\\';
            out += (char)c;
        } else if (c < 0x20) {
            char esc[8];
            snprintf(esc, sizeof(esc), "\\u%04x", c);
            out += esc;
        } else {
            out += (char)c;
        }
    }
    out += '"';
}

void appendField(std::string& out, const char* name, const char* value) {
    appendJson(out, name);
    out += ':';
    appendJson(out, value);
    out += ',';
}

void appendFlag(std::string& out, const char* name, bool on) {
    appendJson(out, name);
    out += on ? ":true," : ":false,";
}

// Not for a host's page: the Switch-only rows, the window (the host's), the SDL keyboard (the view's keys are the
// keyboard here), the in-game menu's button names, and hidden settings other than the bindings.
bool listed(const PcSettingDesc* d) {
    if (d == nullptr || (d->flags & PC_SETTING_SWITCH_ONLY)) {
        return false;
    }
    if (d->tab == PC_SETTING_TAB_HIDDEN) {
        return rowOf(d->key, kKeyPrefix, kKeyRowCount) >= 0 || rowOf(d->key, kPadPrefix, kPadRowCount) >= 0;
    }
    for (const char* skip : {"COS_FULLSCREEN", "COS_KEYBOARD", "COS_PAD_LABELS"}) {
        if (strcmp(d->key, skip) == 0) {
            return false;
        }
    }
    return d->tab >= 0 && d->tab < PC_SETTING_TABS;
}

const PcSettingDesc* findListed(const char* key) {
    for (int i = 0; key != nullptr && i < pc_settings_count(); i++) {
        const PcSettingDesc* d = pc_settings_at(i);
        if (d != nullptr && strcmp(d->key, key) == 0) {
            return listed(d) ? d : nullptr;
        }
    }
    return nullptr;
}

std::vector<Choice> choicesOf(const PcSettingDesc* d) {
    std::vector<Choice> out;
    if (rowOf(d->key, kKeyPrefix, kKeyRowCount) >= 0) {
        out.push_back({"none", "None"});
        int scancodes[128];
        const int n = tww_view_key_scancodes(scancodes, 128);
        for (int i = 0; i < n; i++) {
            const char* name = SDL_GetScancodeName((SDL_Scancode)scancodes[i]);
            if (name != nullptr && name[0] != '\0') {
                out.push_back({name, name});
            }
        }
        return out;
    }
    if (rowOf(d->key, kPadPrefix, kPadRowCount) >= 0) {
        out.push_back({"", "Controller default"});
        out.push_back({"none", "None"});
        for (int b = 0; b < SDL_GAMEPAD_BUTTON_COUNT; b++) {
            const char* name = SDL_GetGamepadStringForButton((SDL_GamepadButton)b);
            if (name == nullptr || name[0] == '\0') {
                continue;
            }
            const char* label = name;
            for (const auto& l : kPadLabels) {
                label = strcmp(l.value, name) == 0 ? l.label : label;
            }
            out.push_back({name, label});
        }
        return out;
    }
    for (int i = 0; i < d->choiceCount; i++) {
        const PcSettingChoice& c = d->choices[i];
        out.push_back({c.value, c.labelEn != nullptr ? c.labelEn : c.label});
    }
    return out;
}

const char* typeOf(const PcSettingDesc* d) {
    if (d->tab == PC_SETTING_TAB_HIDDEN) {
        return "binding";
    }
    const bool onOff = d->choiceCount == 2 && ((strcmp(d->choices[0].value, "0") == 0 && strcmp(d->choices[1].value, "1") == 0) ||
                                              (strcmp(d->choices[0].value, "1") == 0 && strcmp(d->choices[1].value, "0") == 0));
    return onOff ? "bool" : "choice";
}

std::string defaultOf(const PcSettingDesc* d) {
    const int row = rowOf(d->key, kKeyPrefix, kKeyRowCount);
    if (row >= 0) {
        return SDL_GetScancodeName(kKeyRows[row].embedDefault);
    }
    return d->defaultValue != nullptr ? d->defaultValue : "";
}

// A binding row's label: pc_bindings' name of the game input ("A button", "Main stick up").
std::string labelOf(const PcSettingDesc* d) {
    const int key = rowOf(d->key, kKeyPrefix, kKeyRowCount);
    const int pad = rowOf(d->key, kPadPrefix, kPadRowCount);
    if (key >= 0 || pad >= 0) {
        return pc_bind_name(key >= 0 ? PC_BIND_KEYBOARD : PC_BIND_PAD, key >= 0 ? key : pad);
    }
    return d->labelEn != nullptr ? d->labelEn : d->label;
}

void appendSetting(std::string& out, const PcSettingDesc* d) {
    const bool binding = d->tab == PC_SETTING_TAB_HIDDEN;
    const char* group = !binding ? "" : rowOf(d->key, kKeyPrefix, kKeyRowCount) >= 0 ? "keyboard" : "controller";
    const int tab = binding ? PC_SETTING_TAB_CONTROLS : d->tab;
    out += '{';
    appendField(out, "key", d->key);
    appendField(out, "label", labelOf(d).c_str());
    appendField(out, "help", d->helpEn != nullptr ? d->helpEn : d->help);
    appendField(out, "tab", kTabIds[tab]);
    appendField(out, "tabLabel", kTabLabels[tab]);
    appendField(out, "group", group);
    appendField(out, "type", typeOf(d));
    appendField(out, "default", defaultOf(d).c_str());
    appendField(out, "value", std::string(pc_settings_get(d->key)).c_str());
    appendFlag(out, "restart", (d->flags & PC_SETTING_RESTART) != 0);
    appendFlag(out, "perMode", (d->flags & PC_SETTING_PER_MODE) != 0);
    appendFlag(out, "locked", pc_settings_locked(d->key) != 0);
    appendFlag(out, "restartPending", pc_settings_restart_pending(d->key) != 0);
    out += "\"choices\":[";
    bool first = true;
    for (const Choice& c : choicesOf(d)) {
        out += first ? "{" : ",{";
        first = false;
        appendField(out, "value", c.value.c_str());
        appendJson(out, "label");
        out += ':';
        appendJson(out, c.label.c_str());
        out += '}';
    }
    out += "]}";
}

uint32_t copyOut(const std::string& s, char* buf, uint32_t size) {
    const uint32_t need = (uint32_t)s.size() + 1;
    if (buf != nullptr && size >= need) {
        memcpy(buf, s.c_str(), need);
    }
    return need;
}

} // namespace

// The pad bits a key (SDL scancode) holds through the keyboard bindings; Space is also A while no row has it.
uint32_t tww_embed_key_bits(int scancode) {
    uint32_t bits = 0;
    for (int i = 0; i < kKeyRowCount; i++) {
        bits |= scancode >= 0 && sRowScancode[i].load(std::memory_order_relaxed) == scancode ? kKeyRows[i].bit : 0;
    }
    return bits != 0 || scancode != SDL_SCANCODE_SPACE ? bits : TWW_KEY_A;
}

// tww_init, before the game starts: the keyboard bindings' defaults are the view's layout.
void tww_settings_before_init() {
    for (int i = 0; i < kKeyRowCount; i++) {
        const std::string key = std::string(kKeyPrefix) + kKeyRows[i].row;
        for (int mode = 0; mode < PC_MODES; mode++) {
            pc_settings_set_mode_default(key.c_str(), (PcOperationMode)mode, SDL_GetScancodeName(kKeyRows[i].embedDefault));
        }
    }
}

// tww_init, once the settings are loaded: the bindings in effect, and every later change of them.
void tww_settings_after_init() {
    for (int i = 0; i < kKeyRowCount; i++) {
        const std::string key = std::string(kKeyPrefix) + kKeyRows[i].row;
        onKeyBinding(key.c_str(), pc_settings_get(key.c_str()), nullptr);
        pc_settings_subscribe(key.c_str(), onKeyBinding, nullptr);
    }
}

extern "C" {

uint32_t tww_settings_catalogue(char* buf, uint32_t size) {
    std::string out = "{";
    appendField(out, "mode", pc_settings_mode() == PC_MODE_DOCKED ? "docked" : "handheld");
    appendField(out, "path", pc_settings_path());
    out += "\"settings\":[";
    bool first = true;
    for (int i = 0; i < pc_settings_count(); i++) {
        const PcSettingDesc* d = pc_settings_at(i);
        if (!listed(d)) {
            continue;
        }
        out += first ? "" : ",";
        first = false;
        appendSetting(out, d);
    }
    out += "]}";
    return copyOut(out, buf, size);
}

uint32_t tww_settings_get(const char* key, char* buf, uint32_t size) {
    return findListed(key) != nullptr ? copyOut(pc_settings_get(key), buf, size) : 0;
}

int tww_settings_set(const char* key, const char* value) {
    const PcSettingDesc* d = findListed(key);
    if (d == nullptr || value == nullptr || pc_settings_locked(key)) {
        return 0;
    }
    for (const Choice& c : choicesOf(d)) {
        if (c.value == value) {
            return pc_settings_set(key, value) == 0 ? 1 : 0;
        }
    }
    return 0;
}

} // extern "C"
