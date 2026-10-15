/*
 * pc_bindings.h - controller and keyboard bindings of player 1 (native/src/pc/features/pc_bindings.cpp),
 * the options menu's Controls tab.
 *
 * The game reads its pad through Aurora's PADRead, which maps an SDL gamepad's native buttons and a
 * keyboard's keys to the game's buttons and sticks (PADSetAllButtonMappings, PADSetKeyButtonBinding,
 * PADSetKeyAxisBinding). The bindings are kept in the settings file as readable lines:
 *
 *   COS_BIND_PAD_<B>=<SDL gamepad button name>|none   B: A B X Y Z L R START UP DOWN LEFT RIGHT
 *   COS_BIND_KEY_<B>=<SDL scancode name>|none         the same twelve, plus LSTICK_UP/DOWN/LEFT/RIGHT
 *                                                     and CSTICK_UP/DOWN/LEFT/RIGHT
 *
 * A gamepad without COS_BIND_PAD_* lines keeps Aurora's default for its type; the keyboard has this
 * file's defaults (WASD main stick, arrows C stick, J/K/L/I face buttons...). Other settings of the tab:
 * COS_KEYBOARD (the keyboard plays as player 1), COS_PAD_LAYOUT (face button layout preset),
 * COS_PAD_LABELS (how native buttons are named), COS_STICK_DEADZONE / COS_CSTICK_DEADZONE (percent),
 * COS_CAMERA_SENS (percent, pc_controls.h), COS_GYRO_AIM / COS_GYRO_SENS (a gyro, if the controller
 * has one, moves the camera, or the aim in first person), COS_PAD_BACKGROUND (a controller keeps playing
 * while the game's window is not the active one). The game thread calls all of these, except
 * pc_bind_set_focus.
 */
#ifndef PC_BINDINGS_H
#define PC_BINDINGS_H

#include <string>

enum PcBindDevice {
    PC_BIND_PAD = 0,
    PC_BIND_KEYBOARD = 1,
};

/* Registers the tab's hidden binding settings (menuInit). */
void pc_bind_init();
/* Every game frame (menuFrame): keeps player 1's mappings, dead zones, keyboard and gyro in line with
   the settings, and runs a capture. */
void pc_bind_frame();

/* The rows of a device: 12 buttons for the gamepad, 20 inputs for the keyboard. */
int pc_bind_count(PcBindDevice device);
/* The game input of a row ("A button", "Main stick up"). */
const char* pc_bind_name(PcBindDevice device, int index);
/* What it is bound to now ("Space", "Cross", "-"). */
std::string pc_bind_value(PcBindDevice device, int index);

/* The press-to-bind flow: the next button or key pressed (after everything is released) becomes the
   row's binding; a binding it took from another row goes to that row (a swap). Esc cancels, and so do
   10 s without a press. */
void pc_bind_begin(PcBindDevice device, int index);
bool pc_bind_capturing();
bool pc_bind_capturing_row(PcBindDevice device, int index);
void pc_bind_cancel();
/* Tests (COS_SMOKE=options-menu, "#bind" lines): the next poll of a capture sees this native button
   (SDL gamepad button name) or key (SDL scancode name) pressed. */
void pc_bind_test_press(const char* name);

/* The device's defaults: the gamepad back to Aurora's for its type, the keyboard to this file's. */
void pc_bind_restore(PcBindDevice device);

/* Player 1's controller ("No controller"), and the next connected one as player 1. */
std::string pc_bind_controller_name();
void pc_bind_next_controller();
/* 1 when player 1's controller reports a gyro. */
bool pc_bind_has_gyro();

/* libtww's view (tww_view.mm, any thread): its app became active or stopped being active. Until told
   otherwise the game counts as focused. */
void pc_bind_set_focus(bool focused);
/* The game has no pad input now: libtww's app is not active and COS_PAD_BACKGROUND is off (the menu
   blocks the pads then, PADBlockInput). A window of the game's own leaves this to SDL (its joystick
   background hint, set from COS_PAD_BACKGROUND). */
bool pc_bind_pads_away();

#endif /* PC_BINDINGS_H */
