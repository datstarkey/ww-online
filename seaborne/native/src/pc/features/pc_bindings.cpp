// Player 1's controller and keyboard bindings, dead zones and gyro (native/include/pc/pc_bindings.h): the
// options menu's Controls tab. Everything goes through Aurora's PAD mapping API; the settings file
// keeps the bindings as readable names (SDL's gamepad button and scancode names).
#include "pc_internal.h"

#include "pc/pc_bindings.h"
#include "pc/pc_controls.h"
#include "pc/pc_settings.h"

#include "d/d_camera.h"

#include <SDL3/SDL_gamepad.h>
#include <SDL3/SDL_keyboard.h>
#include <SDL3/SDL_scancode.h>
#include <dolphin/pad.h>

#include <SDL3/SDL_hints.h>

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <string>
#include <unistd.h>

namespace {

// ---- the inputs ------------------------------------------------------------------------------------

struct PadRow {
    const char* key;   // COS_BIND_PAD_<key> / COS_BIND_KEY_<key>
    const char* name;  // the game input
    PADButton button;
};

constexpr PadRow kButtons[] = {
    {"A", "A button", PAD_BUTTON_A},       {"B", "B button", PAD_BUTTON_B},
    {"X", "X button", PAD_BUTTON_X},       {"Y", "Y button", PAD_BUTTON_Y},
    {"Z", "Z button", PAD_TRIGGER_Z},      {"L", "L trigger", PAD_TRIGGER_L},
    {"R", "R trigger", PAD_TRIGGER_R},     {"START", "Start", PAD_BUTTON_START},
    {"UP", "D-pad up", PAD_BUTTON_UP},     {"DOWN", "D-pad down", PAD_BUTTON_DOWN},
    {"LEFT", "D-pad left", PAD_BUTTON_LEFT}, {"RIGHT", "D-pad right", PAD_BUTTON_RIGHT},
};
constexpr int kButtonCount = (int)(sizeof(kButtons) / sizeof(kButtons[0]));

struct AxisRow {
    const char* key;
    const char* name;
    PADAxis axis;
};

constexpr AxisRow kAxes[] = {
    {"LSTICK_UP", "Main stick up", PAD_AXIS_LEFT_Y_POS},     {"LSTICK_DOWN", "Main stick down", PAD_AXIS_LEFT_Y_NEG},
    {"LSTICK_LEFT", "Main stick left", PAD_AXIS_LEFT_X_NEG}, {"LSTICK_RIGHT", "Main stick right", PAD_AXIS_LEFT_X_POS},
    {"CSTICK_UP", "C stick up", PAD_AXIS_RIGHT_Y_POS},       {"CSTICK_DOWN", "C stick down", PAD_AXIS_RIGHT_Y_NEG},
    {"CSTICK_LEFT", "C stick left", PAD_AXIS_RIGHT_X_NEG},   {"CSTICK_RIGHT", "C stick right", PAD_AXIS_RIGHT_X_POS},
};
constexpr int kAxisCount = (int)(sizeof(kAxes) / sizeof(kAxes[0]));

// The keyboard's defaults, by row: the twelve buttons, then the eight stick directions.
constexpr SDL_Scancode kKeyDefaults[kButtonCount + kAxisCount] = {
    SDL_SCANCODE_J,      SDL_SCANCODE_K,     SDL_SCANCODE_L,    SDL_SCANCODE_I,  // A B X Y
    SDL_SCANCODE_U,      SDL_SCANCODE_Q,     SDL_SCANCODE_O,    SDL_SCANCODE_RETURN, // Z L R Start
    SDL_SCANCODE_T,      SDL_SCANCODE_G,     SDL_SCANCODE_F,    SDL_SCANCODE_H,  // D-pad
    SDL_SCANCODE_W,      SDL_SCANCODE_S,     SDL_SCANCODE_A,    SDL_SCANCODE_D,  // main stick
    SDL_SCANCODE_UP,     SDL_SCANCODE_DOWN,  SDL_SCANCODE_LEFT, SDL_SCANCODE_RIGHT, // C stick
};

std::string keyOf(PcBindDevice device, int index) {
    const char* row = index < kButtonCount ? kButtons[index].key : kAxes[index - kButtonCount].key;
    return std::string(device == PC_BIND_PAD ? "COS_BIND_PAD_" : "COS_BIND_KEY_") + row;
}

// ---- settings ----------------------------------------------------------------------------------------

// The hidden settings (no menu row of their own: the tab's binding rows show them). Their key strings
// must outlive the program.
std::vector<std::string> sKeys;
std::vector<PcSettingDesc> sDescs;
unsigned int sGeneration = 1; // bumped on every change of a binding setting: re-applied next frame

void onBindChanged(const char*, const char*, void*) {
    sGeneration++;
}

void registerHidden() {
    sKeys.reserve(2 * (kButtonCount + kAxisCount));
    sDescs.reserve(sKeys.capacity());
    for (int d = 0; d < 2; d++) {
        const int count = d == PC_BIND_PAD ? kButtonCount : kButtonCount + kAxisCount;
        for (int i = 0; i < count; i++) {
            sKeys.push_back(keyOf((PcBindDevice)d, i));
            PcSettingDesc desc{};
            desc.key = sKeys.back().c_str();
            desc.label = desc.key;
            desc.help = "";
            desc.tab = PC_SETTING_TAB_HIDDEN;
            desc.defaultValue = d == PC_BIND_KEYBOARD ? SDL_GetScancodeName(kKeyDefaults[i]) : "";
            desc.apply = onBindChanged;
            sDescs.push_back(desc);
            pc_settings_register(&sDescs.back());
        }
    }
}

bool settingOn(const char* key) {
    return strcmp(pc_settings_get(key), "1") == 0;
}

int settingInt(const char* key, int fallback) {
    const char* v = pc_settings_get(key);
    return v[0] != '\0' ? atoi(v) : fallback;
}

// ---- native names ----------------------------------------------------------------------------------

// COS_PAD_LABELS: "auto" (the controller's own labels), "xbox", "playstation", "pro" (A right, B
// bottom) or "position" (south/east/west/north).
const char* labelSet() {
    return pc_settings_get("COS_PAD_LABELS");
}

const char* faceName(SDL_GamepadButton b, const char* set) {
    static const char* const kSets[][5] = {
        // set, south, east, west, north
        {"xbox", "A", "B", "X", "Y"},
        {"playstation", "Cross", "Circle", "Square", "Triangle"},
        {"pro", "B", "A", "Y", "X"},
        {"position", "South face", "East face", "West face", "North face"},
    };
    const int slot = b == SDL_GAMEPAD_BUTTON_SOUTH ? 1 : b == SDL_GAMEPAD_BUTTON_EAST ? 2 : b == SDL_GAMEPAD_BUTTON_WEST ? 3 : 4;
    for (const auto& s : kSets) {
        if (strcmp(set, s[0]) == 0) {
            return s[slot];
        }
    }
    return kSets[3][slot];
}

SDL_Gamepad* playerPad() {
    const s32 idx = PADGetIndexForPort(0);
    return idx >= 0 ? PADGetSDLGamepadForIndex((u32)idx) : nullptr;
}

std::string nativeButtonName(u32 native) {
    if (native == PAD_NATIVE_BUTTON_INVALID || native >= SDL_GAMEPAD_BUTTON_COUNT) {
        return "-";
    }
    const SDL_GamepadButton b = (SDL_GamepadButton)native;
    std::string set = labelSet();
    if (b == SDL_GAMEPAD_BUTTON_SOUTH || b == SDL_GAMEPAD_BUTTON_EAST || b == SDL_GAMEPAD_BUTTON_WEST ||
        b == SDL_GAMEPAD_BUTTON_NORTH) {
        if (set == "auto") {
            SDL_Gamepad* gp = playerPad();
            switch (gp != nullptr ? SDL_GetGamepadButtonLabel(gp, b) : SDL_GAMEPAD_BUTTON_LABEL_UNKNOWN) {
            case SDL_GAMEPAD_BUTTON_LABEL_A: return "A";
            case SDL_GAMEPAD_BUTTON_LABEL_B: return "B";
            case SDL_GAMEPAD_BUTTON_LABEL_X: return "X";
            case SDL_GAMEPAD_BUTTON_LABEL_Y: return "Y";
            case SDL_GAMEPAD_BUTTON_LABEL_CROSS: return "Cross";
            case SDL_GAMEPAD_BUTTON_LABEL_CIRCLE: return "Circle";
            case SDL_GAMEPAD_BUTTON_LABEL_SQUARE: return "Square";
            case SDL_GAMEPAD_BUTTON_LABEL_TRIANGLE: return "Triangle";
            default: return faceName(b, "position");
            }
        }
        return faceName(b, set.c_str());
    }
    const bool ps = set == "playstation";
    const bool pro = set == "pro";
    switch (b) {
    case SDL_GAMEPAD_BUTTON_LEFT_SHOULDER: return ps ? "L1" : pro ? "L" : "Left shoulder";
    case SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER: return ps ? "R1" : pro ? "R" : "Right shoulder";
    case SDL_GAMEPAD_BUTTON_LEFT_STICK: return ps ? "L3" : "Left stick press";
    case SDL_GAMEPAD_BUTTON_RIGHT_STICK: return ps ? "R3" : "Right stick press";
    case SDL_GAMEPAD_BUTTON_BACK: return ps ? "Share" : pro ? "-" : "Back";
    case SDL_GAMEPAD_BUTTON_START: return ps ? "Options" : pro ? "+" : "Start";
    case SDL_GAMEPAD_BUTTON_GUIDE: return "Home";
    case SDL_GAMEPAD_BUTTON_DPAD_UP: return "D-pad up";
    case SDL_GAMEPAD_BUTTON_DPAD_DOWN: return "D-pad down";
    case SDL_GAMEPAD_BUTTON_DPAD_LEFT: return "D-pad left";
    case SDL_GAMEPAD_BUTTON_DPAD_RIGHT: return "D-pad right";
    case SDL_GAMEPAD_BUTTON_TOUCHPAD: return "Touchpad";
    default: break;
    }
    const char* s = SDL_GetGamepadStringForButton(b);
    return s != nullptr ? s : "?";
}

// ---- applying ----------------------------------------------------------------------------------------

// The native button a COS_BIND_PAD_* value names (PAD_NATIVE_BUTTON_INVALID for "none"); -1: unset.
s64 padSetting(int index) {
    const char* v = pc_settings_get(keyOf(PC_BIND_PAD, index).c_str());
    if (v[0] == '\0') {
        return -1;
    }
    if (strcmp(v, "none") == 0) {
        return PAD_NATIVE_BUTTON_INVALID;
    }
    const SDL_GamepadButton b = SDL_GetGamepadButtonFromString(v);
    return b == SDL_GAMEPAD_BUTTON_INVALID ? -1 : (s64)b;
}

s32 keySetting(int index) {
    const char* v = pc_settings_get(keyOf(PC_BIND_KEYBOARD, index).c_str());
    if (v[0] == '\0' || strcmp(v, "none") == 0) {
        return PAD_KEY_INVALID;
    }
    const SDL_Scancode sc = SDL_GetScancodeFromName(v);
    return sc == SDL_SCANCODE_UNKNOWN ? PAD_KEY_INVALID : (s32)sc;
}

unsigned int sPadApplied = 0;   // the generation applied to the controller below
s32 sPadIndexApplied = -2;      // the controller index it was applied to
unsigned int sKeyApplied = 0;
int sKeyboardOn = -1;

void applyPad() {
    const s32 idx = PADGetIndexForPort(0);
    if (idx < 0) {
        sPadIndexApplied = idx;
        return;
    }
    if (sPadApplied == sGeneration && sPadIndexApplied == idx) {
        return;
    }
    u32 count = 0;
    PADButtonMapping* cur = PADGetButtonMappings(0, &count);
    if (cur == nullptr || count == 0) {
        return;
    }
    for (int i = 0; i < kButtonCount; i++) {
        const s64 native = padSetting(i);
        if (native >= 0) {
            PADSetButtonMapping(0, PADButtonMapping{(u32)native, kButtons[i].button});
        }
    }
    sPadApplied = sGeneration;
    sPadIndexApplied = idx;
    pc::writef(STDERR_FILENO, "[cos] bindings: controller \"%s\" mapped\n", PADGetName(0) != nullptr ? PADGetName(0) : "?");
}

void applyKeyboard() {
    const int on = settingOn("COS_KEYBOARD") ? 1 : 0;
    if (sKeyApplied == sGeneration && sKeyboardOn == on) {
        return;
    }
    for (int i = 0; i < kButtonCount; i++) {
        PADSetKeyButtonBinding(0, PADKeyButtonBinding{keySetting(i), kButtons[i].button});
    }
    for (int i = 0; i < kAxisCount; i++) {
        PADSetKeyAxisBinding(0, PADKeyAxisBinding{keySetting(kButtonCount + i), kAxes[i].axis, 0});
    }
    PADSetKeyboardActive(0, on);
    if (on != sKeyboardOn) {
        pc::writef(STDERR_FILENO, "[cos] bindings: keyboard %s for player 1\n", on ? "on" : "off");
    }
    sKeyApplied = sGeneration;
    sKeyboardOn = on;
}

void applyDeadZones() {
    PADDeadZones* dz = PADGetDeadZones(0);
    if (dz == nullptr) {
        return;
    }
    const u16 stick = (u16)(std::clamp(settingInt("COS_STICK_DEADZONE", 24), 0, 90) * 32767 / 100);
    const u16 cstick = (u16)(std::clamp(settingInt("COS_CSTICK_DEADZONE", 24), 0, 90) * 32767 / 100);
    dz->useDeadzones = true;
    dz->stickDeadZone = stick;
    dz->substickDeadZone = cstick;
}

// ---- background input ------------------------------------------------------------------------------

std::atomic<bool> sFocused{true}; // libtww's app is active (pc_bind_set_focus)
bool sPadsAway = false;
bool sBackgroundHint = false;     // SDL's default: no joystick events while the window is in the background

void applyBackground() {
    const bool background = settingOn("COS_PAD_BACKGROUND");
    if (pc_embedded()) {
        // Aurora's external surface lets SDL read the pads in the background (pc_main.cpp); the focus is the host's.
        sPadsAway = !background && !sFocused.load(std::memory_order_relaxed);
    } else if (background != sBackgroundHint) {
        SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, background ? "1" : "0");
        sBackgroundHint = background;
    }
}

// ---- gyro --------------------------------------------------------------------------------------------

bool sGyroVirtual = false; // this file set player 1's virtual status (only then is it cleared here)

void applyGyro() {
    const bool on = settingOn("COS_GYRO_AIM") && PADHasSensor(0, PAD_SENSOR_GYRO);
    f32 rate[3] = {};
    if (on) {
        PADSetSensorEnabled(0, PAD_SENSOR_GYRO, TRUE);
    }
    if (!on || !PADGetSensorData(0, PAD_SENSOR_GYRO, rate, 3)) {
        if (sGyroVirtual) {
            PADClearVirtualStatus(0);
            sGyroVirtual = false;
        }
        return;
    }
    // rad/s to stick: full deflection at 2 rad/s at 100 %; x is the pitch, y the yaw (turning the
    // controller left turns the view left)
    const f32 k = 127.0f / 2.0f * (f32)std::clamp(settingInt("COS_GYRO_SENS", 100), 25, 400) / 100.0f;
    const s8 x = (s8)std::clamp((int)std::lround(-rate[1] * k), -127, 127);
    const s8 y = (s8)std::clamp((int)std::lround(rate[0] * k), -127, 127);
    PADStatus status{};
    // In first person (the camera's "Subject" type: aiming the bow, the hookshot, looking around) the
    // main stick aims; elsewhere the C stick turns the camera.
    dCamera_c* cam = dCam_getBody();
    const bool firstPerson = cam != nullptr && cam->mCurType == cam->mCamTypeSubject;
    if (firstPerson) {
        status.stickX = x;
        status.stickY = y;
    } else {
        status.substickX = (s8)(pc_camera_invert_x() ? -x : x);
        status.substickY = (s8)(pc_camera_invert_y() ? -y : y);
    }
    PADSetVirtualStatus(0, &status);
    sGyroVirtual = true;
}

// ---- capture -----------------------------------------------------------------------------------------

struct Capture {
    bool on = false;
    PcBindDevice device = PC_BIND_PAD;
    int index = 0;
    bool armed = false;    // everything was released since the capture began
    unsigned int frames = 0;
} sCap;
std::string sTestPress; // pc_bind_test_press

constexpr unsigned int kCaptureFrames = 600; // 10 s at 60 presents, 20 at 30

// The pressed native button (gamepad) or key (keyboard) this frame, -1 none.
int probe(PcBindDevice device) {
    if (!sTestPress.empty()) {
        const std::string name = sTestPress;
        sTestPress.clear();
        if (device == PC_BIND_PAD) {
            const SDL_GamepadButton b = SDL_GetGamepadButtonFromString(name.c_str());
            return b == SDL_GAMEPAD_BUTTON_INVALID ? -1 : (int)b;
        }
        const SDL_Scancode sc = SDL_GetScancodeFromName(name.c_str());
        return sc == SDL_SCANCODE_UNKNOWN ? -1 : (int)sc;
    }
    if (device == PC_BIND_PAD) {
        return PADGetNativeButtonPressed(0);
    }
    int count = 0;
    const bool* keys = SDL_GetKeyboardState(&count);
    if (keys == nullptr) {
        return -1;
    }
    for (int sc = SDL_SCANCODE_A; sc < count; sc++) {
        if (keys[sc] && sc != SDL_SCANCODE_ESCAPE && sc != SDL_SCANCODE_F1) {
            return sc;
        }
    }
    return -1;
}

bool escapeHeld() {
    int count = 0;
    const bool* keys = SDL_GetKeyboardState(&count);
    return keys != nullptr && count > SDL_SCANCODE_ESCAPE && keys[SDL_SCANCODE_ESCAPE];
}

std::string settingValueFor(PcBindDevice device, int code) {
    if (device == PC_BIND_PAD) {
        const char* s = SDL_GetGamepadStringForButton((SDL_GamepadButton)code);
        return s != nullptr ? s : "none";
    }
    const char* s = SDL_GetScancodeName((SDL_Scancode)code);
    return s != nullptr && s[0] != '\0' ? s : "none";
}

// The current binding of a row as its setting value (for the swap).
std::string currentSetting(PcBindDevice device, int index) {
    if (device == PC_BIND_KEYBOARD) {
        return pc_settings_get(keyOf(device, index).c_str());
    }
    u32 count = 0;
    if (PADButtonMapping* cur = PADGetButtonMappings(0, &count); cur != nullptr) {
        for (u32 j = 0; j < count; j++) {
            if (cur[j].padButton == kButtons[index].button) {
                return cur[j].nativeButton == PAD_NATIVE_BUTTON_INVALID
                           ? std::string("none")
                           : settingValueFor(PC_BIND_PAD, (int)cur[j].nativeButton);
            }
        }
    }
    return pc_settings_get(keyOf(device, index).c_str()); // "" while unset and no controller: stays unset
}

void bind(PcBindDevice device, int index, int code) {
    const std::string value = settingValueFor(device, code);
    const std::string old = currentSetting(device, index);
    const int count = pc_bind_count(device);
    // A gamepad's bindings are all written once one changes, so the file holds the whole layout.
    for (int j = 0; j < count; j++) {
        std::string v = j == index ? value : currentSetting(device, j);
        if (j != index && v == value && !old.empty()) {
            v = old; // the row that had this button gets the old one: a swap
            pc::writef(STDERR_FILENO, "[cos] bindings: %s takes %s from %s, which gets %s\n", pc_bind_name(device, index),
                       value.c_str(), pc_bind_name(device, j), old.c_str());
        }
        pc_settings_set(keyOf(device, j).c_str(), v.c_str());
    }
    pc::writef(STDERR_FILENO, "[cos] bindings: %s %s = %s\n", device == PC_BIND_PAD ? "gamepad" : "keyboard",
               pc_bind_name(device, index), value.c_str());
}

void captureFrame() {
    if (!sCap.on) {
        return;
    }
    if (escapeHeld() || ++sCap.frames > kCaptureFrames) {
        pc::writef(STDERR_FILENO, "[cos] bindings: capture of %s cancelled\n", pc_bind_name(sCap.device, sCap.index));
        sCap.on = false;
        return;
    }
    const int code = probe(sCap.device);
    if (!sCap.armed) {
        sCap.armed = code < 0;
        return;
    }
    if (code >= 0) {
        bind(sCap.device, sCap.index, code);
        sCap.on = false;
    }
}

} // namespace

void pc_bind_init() {
    registerHidden();
}

void pc_bind_frame() {
    captureFrame();
    applyKeyboard();
    applyPad();
    applyDeadZones();
    applyGyro();
    applyBackground();
}

int pc_bind_count(PcBindDevice device) {
    return device == PC_BIND_PAD ? kButtonCount : kButtonCount + kAxisCount;
}

const char* pc_bind_name(PcBindDevice device, int index) {
    (void)device;
    return index < kButtonCount ? kButtons[index].name : kAxes[index - kButtonCount].name;
}

std::string pc_bind_value(PcBindDevice device, int index) {
    if (device == PC_BIND_KEYBOARD) {
        const s32 sc = keySetting(index);
        return sc == PAD_KEY_INVALID ? "-" : SDL_GetScancodeName((SDL_Scancode)sc);
    }
    u32 count = 0;
    if (PADButtonMapping* cur = PADGetButtonMappings(0, &count); cur != nullptr) {
        for (u32 j = 0; j < count; j++) {
            if (cur[j].padButton == kButtons[index].button) {
                if (cur[j].nativeButton == PAD_NATIVE_BUTTON_INVALID && (index == 5 || index == 6)) {
                    return index == 5 ? "Left trigger" : "Right trigger"; // the analog triggers
                }
                return nativeButtonName(cur[j].nativeButton);
            }
        }
    }
    const s64 native = padSetting(index);
    return native >= 0 ? nativeButtonName((u32)native) : "-";
}

void pc_bind_begin(PcBindDevice device, int index) {
    sCap = Capture{true, device, index, false, 0};
    pc::writef(STDERR_FILENO, "[cos] bindings: press a %s for %s\n", device == PC_BIND_PAD ? "button" : "key",
               pc_bind_name(device, index));
}

bool pc_bind_capturing() {
    return sCap.on;
}

bool pc_bind_capturing_row(PcBindDevice device, int index) {
    return sCap.on && sCap.device == device && sCap.index == index;
}

void pc_bind_cancel() {
    sCap.on = false;
}

void pc_bind_test_press(const char* name) {
    sTestPress = name != nullptr ? name : "";
}

void pc_bind_restore(PcBindDevice device) {
    const int count = pc_bind_count(device);
    for (int i = 0; i < count; i++) {
        pc_settings_set(keyOf(device, i).c_str(), device == PC_BIND_KEYBOARD ? SDL_GetScancodeName(kKeyDefaults[i]) : "");
    }
    if (device == PC_BIND_PAD) {
        PADRestoreDefaultMapping(0);
        sGeneration++;
    }
    pc::writef(STDERR_FILENO, "[cos] bindings: %s defaults restored\n", device == PC_BIND_PAD ? "gamepad" : "keyboard");
}

std::string pc_bind_controller_name() {
    const char* name = PADGetIndexForPort(0) >= 0 ? PADGetName(0) : nullptr;
    return name != nullptr ? name : "No controller";
}

void pc_bind_next_controller() {
    const u32 count = PADCount();
    if (count == 0) {
        return;
    }
    const s32 cur = PADGetIndexForPort(0);
    const u32 next = cur < 0 ? 0 : (u32)(cur + 1) % count;
    PADSetPortForIndex(next, 0);
    pc::writef(STDERR_FILENO, "[cos] bindings: player 1 is now \"%s\"\n", PADGetNameForControllerIndex(next));
}

bool pc_bind_has_gyro() {
    return PADHasSensor(0, PAD_SENSOR_GYRO) != FALSE;
}

void pc_bind_set_focus(bool focused) {
    sFocused.store(focused, std::memory_order_relaxed);
}

bool pc_bind_pads_away() {
    return sPadsAway;
}
