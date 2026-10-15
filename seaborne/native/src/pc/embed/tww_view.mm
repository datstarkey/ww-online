// libtww's view (native/include/pc/tww.h, tww_view_create): an NSView backed by a CAMetalLayer, which
// Aurora renders into (patch 0024). It reports its size and backing scale (tww_set_size) and, while it
// is the first responder, turns keys into the keyboard pad (tww_set_keys) through the keyboard bindings
// (tww_settings.cpp). Key codes are positions on the keyboard (kVK_ANSI_*), so the layout does not matter.
#include "pc/tww.h"
#include "pc/pc_bindings.h"

#import <AppKit/AppKit.h>
#import <Carbon/Carbon.h>
#import <QuartzCore/CAMetalLayer.h>

uint32_t tww_embed_key_bits(int scancode); // tww_settings.cpp

namespace {

// Key codes to SDL scancodes (USB HID usages), the names the keyboard bindings use. Modifier keys never reach
// keyDown, so they are not here.
const struct {
    unsigned short keyCode;
    int scancode;
} kScancodes[] = {
    {kVK_ANSI_A, 4},  {kVK_ANSI_B, 5},  {kVK_ANSI_C, 6},  {kVK_ANSI_D, 7},  {kVK_ANSI_E, 8},  {kVK_ANSI_F, 9},
    {kVK_ANSI_G, 10}, {kVK_ANSI_H, 11}, {kVK_ANSI_I, 12}, {kVK_ANSI_J, 13}, {kVK_ANSI_K, 14}, {kVK_ANSI_L, 15},
    {kVK_ANSI_M, 16}, {kVK_ANSI_N, 17}, {kVK_ANSI_O, 18}, {kVK_ANSI_P, 19}, {kVK_ANSI_Q, 20}, {kVK_ANSI_R, 21},
    {kVK_ANSI_S, 22}, {kVK_ANSI_T, 23}, {kVK_ANSI_U, 24}, {kVK_ANSI_V, 25}, {kVK_ANSI_W, 26}, {kVK_ANSI_X, 27},
    {kVK_ANSI_Y, 28}, {kVK_ANSI_Z, 29}, {kVK_ANSI_1, 30}, {kVK_ANSI_2, 31}, {kVK_ANSI_3, 32}, {kVK_ANSI_4, 33},
    {kVK_ANSI_5, 34}, {kVK_ANSI_6, 35}, {kVK_ANSI_7, 36}, {kVK_ANSI_8, 37}, {kVK_ANSI_9, 38}, {kVK_ANSI_0, 39},
    {kVK_Return, 40}, {kVK_Escape, 41}, {kVK_Delete, 42}, {kVK_Tab, 43}, {kVK_Space, 44},
    {kVK_ANSI_Minus, 45}, {kVK_ANSI_Equal, 46}, {kVK_ANSI_LeftBracket, 47}, {kVK_ANSI_RightBracket, 48},
    {kVK_ANSI_Backslash, 49}, {kVK_ANSI_Semicolon, 51}, {kVK_ANSI_Quote, 52}, {kVK_ANSI_Grave, 53},
    {kVK_ANSI_Comma, 54}, {kVK_ANSI_Period, 55}, {kVK_ANSI_Slash, 56},
    {kVK_F1, 58}, {kVK_F2, 59}, {kVK_F3, 60}, {kVK_F4, 61}, {kVK_F5, 62}, {kVK_F6, 63},
    {kVK_F7, 64}, {kVK_F8, 65}, {kVK_F9, 66}, {kVK_F10, 67}, {kVK_F11, 68}, {kVK_F12, 69},
    {kVK_Home, 74}, {kVK_PageUp, 75}, {kVK_ForwardDelete, 76}, {kVK_End, 77}, {kVK_PageDown, 78},
    {kVK_RightArrow, 79}, {kVK_LeftArrow, 80}, {kVK_DownArrow, 81}, {kVK_UpArrow, 82},
    {kVK_ANSI_KeypadDivide, 84}, {kVK_ANSI_KeypadMultiply, 85}, {kVK_ANSI_KeypadMinus, 86},
    {kVK_ANSI_KeypadPlus, 87}, {kVK_ANSI_KeypadEnter, 88}, {kVK_ANSI_Keypad1, 89}, {kVK_ANSI_Keypad2, 90},
    {kVK_ANSI_Keypad3, 91}, {kVK_ANSI_Keypad4, 92}, {kVK_ANSI_Keypad5, 93}, {kVK_ANSI_Keypad6, 94},
    {kVK_ANSI_Keypad7, 95}, {kVK_ANSI_Keypad8, 96}, {kVK_ANSI_Keypad9, 97}, {kVK_ANSI_Keypad0, 98},
    {kVK_ANSI_KeypadDecimal, 99}, {kVK_ANSI_KeypadEquals, 103},
};

uint32_t keyFor(unsigned short keyCode) {
    for (const auto& k : kScancodes) {
        if (k.keyCode == keyCode) {
            return tww_embed_key_bits(k.scancode);
        }
    }
    return 0;
}

} // namespace

@interface TwwView : NSView
@end

@implementation TwwView {
    uint32_t _keys;
}

- (instancetype)initWithFrame:(NSRect)frame {
    if ((self = [super initWithFrame:frame])) {
        self.wantsLayer = YES;
        self.layerContentsRedrawPolicy = NSViewLayerContentsRedrawNever;
        // The game's focus is the app's: any of the host's windows active (COS_PAD_BACKGROUND, pc_bindings.h).
        NSNotificationCenter* center = NSNotificationCenter.defaultCenter;
        [center addObserver:self selector:@selector(appBecameActive:) name:NSApplicationDidBecomeActiveNotification
                     object:nil];
        [center addObserver:self selector:@selector(appResignedActive:) name:NSApplicationDidResignActiveNotification
                     object:nil];
    }
    return self;
}

- (void)appBecameActive:(NSNotification*)note {
    pc_bind_set_focus(true);
}

// Key ups never come in the background: no key stays held.
- (void)appResignedActive:(NSNotification*)note {
    [self releaseKeys];
    pc_bind_set_focus(false);
}

- (void)releaseKeys {
    _keys = 0;
    tww_set_keys(0);
}

- (CALayer*)makeBackingLayer {
    CAMetalLayer* layer = [CAMetalLayer layer];
    layer.opaque = YES;
    return layer;
}

- (BOOL)wantsUpdateLayer {
    return YES;
}

- (BOOL)isOpaque {
    return YES;
}

- (BOOL)acceptsFirstResponder {
    return YES;
}

- (BOOL)acceptsFirstMouse:(NSEvent*)event {
    return YES;
}

- (void)mouseDown:(NSEvent*)event {
    [self.window makeFirstResponder:self];
}

- (void)setKey:(unsigned short)keyCode down:(BOOL)down {
    const uint32_t key = keyFor(keyCode);
    _keys = down ? (_keys | key) : (_keys & ~key);
    tww_set_keys(_keys);
}

// Handled keys are not passed on (no system beep); others go up the responder chain.
- (void)keyDown:(NSEvent*)event {
    if (keyFor(event.keyCode) == 0) {
        [super keyDown:event];
        return;
    }
    [self setKey:event.keyCode down:YES];
}

- (void)keyUp:(NSEvent*)event {
    if (keyFor(event.keyCode) == 0) {
        [super keyUp:event];
        return;
    }
    [self setKey:event.keyCode down:NO];
}

- (BOOL)resignFirstResponder {
    [self releaseKeys];
    return YES;
}

- (void)reportSize {
    const CGFloat scale = self.window != nil ? self.window.backingScaleFactor : NSScreen.mainScreen.backingScaleFactor;
    self.layer.contentsScale = scale;
    const NSSize size = self.bounds.size;
    if (size.width >= 1 && size.height >= 1) {
        tww_set_size((uint32_t)size.width, (uint32_t)size.height, (float)scale);
    }
}

- (void)setFrameSize:(NSSize)size {
    [super setFrameSize:size];
    [self reportSize];
}

// The host moves the view between its windows (a pop-out): the key ups go to the old window, so none stays held.
- (void)viewWillMoveToWindow:(NSWindow*)window {
    [super viewWillMoveToWindow:window];
    if (window != self.window) {
        [self releaseKeys];
    }
}

- (void)viewDidMoveToWindow {
    [super viewDidMoveToWindow];
    [self reportSize];
}

- (void)viewDidChangeBackingProperties {
    [super viewDidChangeBackingProperties];
    [self reportSize];
}

@end

// The scancodes the view reports (the keyboard bindings' choices), at most max into out; returns how many.
int tww_view_key_scancodes(int* out, int max) {
    int n = 0;
    for (const auto& k : kScancodes) {
        if (n < max) {
            out[n++] = k.scancode;
        }
    }
    return n;
}

// The CAMetalLayer of a view from tww_view_create (tww_init), else nullptr.
void* tww_view_layer(void* view) {
    NSView* nsView = (__bridge NSView*)view;
    if (nsView == nil || ![nsView.layer isKindOfClass:[CAMetalLayer class]]) {
        return nullptr;
    }
    return (__bridge void*)nsView.layer;
}

extern "C" {

void* tww_view_create(void) {
    TwwView* view = [[TwwView alloc] initWithFrame:NSMakeRect(0, 0, 640, 480)];
    return (__bridge_retained void*)view;
}

void tww_view_destroy(void* view) {
    TwwView* nsView = (__bridge_transfer TwwView*)view;
    [nsView removeFromSuperview];
}

} // extern "C"
