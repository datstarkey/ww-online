// SPDX-License-Identifier: MIT
// Copyright (c) 2026 datstarkey
/*
 * tww.h - libtww, the game as a shared library a host application embeds (C ABI).
 * Design: tww-native-notes WW_ONLINE_EMBED.md, sections 4.1-4.3. Proof of concept: macOS only.
 *
 * Threads:
 * - UI thread (the macOS main thread): tww_view_create / tww_view_destroy. The view is an NSView
 *   backed by a CAMetalLayer; it reports its size (tww_set_size) and turns keys into the keyboard
 *   pad (tww_set_keys) while it is the first responder.
 * - Game thread (one thread the host owns, 16 MiB of stack is plenty): the first call into the
 *   library, then tww_init and tww_run_frame. The SDK adopts the first thread that enters it as
 *   the game's main thread, and the game's static initialisers run on the thread that loads the
 *   library: load it on the game thread (call tww_api_version there first).
 * - Any thread: the rest.
 */
#ifndef TWW_H
#define TWW_H

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#define TWW_API_VERSION 2

/* Keyboard pad bits (tww_set_keys). */
enum {
    TWW_KEY_STICK_UP = 1u << 0,
    TWW_KEY_STICK_DOWN = 1u << 1,
    TWW_KEY_STICK_LEFT = 1u << 2,
    TWW_KEY_STICK_RIGHT = 1u << 3,
    TWW_KEY_C_UP = 1u << 4,
    TWW_KEY_C_DOWN = 1u << 5,
    TWW_KEY_C_LEFT = 1u << 6,
    TWW_KEY_C_RIGHT = 1u << 7,
    TWW_KEY_A = 1u << 8,
    TWW_KEY_B = 1u << 9,
    TWW_KEY_X = 1u << 10,
    TWW_KEY_Y = 1u << 11,
    TWW_KEY_Z = 1u << 12,
    TWW_KEY_L = 1u << 13,
    TWW_KEY_R = 1u << 14,
    TWW_KEY_START = 1u << 15,
    TWW_KEY_DPAD_UP = 1u << 16,
    TWW_KEY_DPAD_DOWN = 1u << 17,
    TWW_KEY_DPAD_LEFT = 1u << 18,
    TWW_KEY_DPAD_RIGHT = 1u << 19,
};

uint32_t tww_api_version(void);

/* UI thread. An NSView* (CAMetalLayer-backed) to hand to the host's view hierarchy, and to tww_init. */
void* tww_view_create(void);
void tww_view_destroy(void* view);

/* Game thread. view: from tww_view_create. disc: the GZLE01 image. config: "KEY=VALUE" pairs separated
   by newlines or ';' (COS_* options, e.g. COS_USER_DIR=<saves, settings, caches>), or NULL. Returns 0
   once the game is ready for its first frame; a failure reports through the fatal callback and parks
   the thread (the game cannot start twice in one process). */
int tww_init(void* view, const char* disc, const char* config);
/* Game thread: one game frame (paced by the game). 0: running, 1: quit requested. */
int tww_run_frame(void);
/* Game thread: stops presenting to the view. The game cannot restart in this process. */
void tww_shutdown(void);

/* Any thread. */
void tww_set_size(uint32_t width, uint32_t height, float scale); /* the view's points and backing scale */
void tww_set_keys(uint32_t keys);                                  /* TWW_KEY_* held now */
void tww_set_fatal_callback(void (*fn)(int code));                 /* before tww_init; any thread calls it */
uint32_t tww_frame_count(void);                                    /* game frames run */
uint64_t tww_present_count(void);                                  /* frames presented to the view */
/* Not the game thread (both wait for frames it renders): a PNG of the next frame. surface: what the
   view's layer shows (window size, letterboxed); frame: the game's picture at its own size. 1 = written. */
int tww_save_surface_png(const char* path);
int tww_save_frame_png(const char* path);

/*
 * Multiplayer (API version 2). In-process ABI, not a wire format: fixed-width fields, explicit padding,
 * host byte order. Game thread only, between tww_run_frame calls (the game is quiescent there, so no locks).
 * Angles are s16 binary angles; positions are world units; procs are daPy_lk_c::daPy_PROC values.
 */
#define TWW_MAX_PEERS 3

enum {
    TWW_LOC_IDLE = 1u << 0,   /* in play, no scene change pending: safe to sync and to show peers */
    TWW_LOC_EVENT = 1u << 1,  /* an event or cutscene is running */
};

typedef struct TwwLocation {
    char stage[8];   /* NUL-terminated current stage name ("sea", "M_NewD2", ...); "" on title/name screens */
    int8_t room;     /* dStage_roomControl_c::mStayNo, -1 if none */
    int8_t layer;    /* stage layer, -1 if unknown */
    int16_t point;   /* the start point the stage was entered from */
    uint32_t flags;  /* TWW_LOC_* */
} TwwLocation;

typedef struct TwwLocalState {
    uint32_t valid;       /* 0: no player actor (title, loading); every other field is then zero */
    float pos[3];         /* current.pos */
    int16_t angle[3];     /* shape_angle */
    int16_t pad0;
    float speed_f;        /* speedF */
    uint32_t proc;        /* mCurProc */
    TwwLocation loc;
} TwwLocalState;

typedef struct TwwPeerState {
    float pos[3];
    int16_t angle[3];
    int16_t pad0;
    float speed_f;
    uint32_t proc;
    char stage[8];        /* the puppet shows only while this equals the local stage */
    int8_t room;
    uint8_t pad1[3];
} TwwPeerState;

int tww_get_local_state(TwwLocalState* out);           /* 1 = valid */
int tww_get_location(TwwLocation* out);                /* 1 = in a stage */
void tww_set_peer(int slot, const TwwPeerState* peer); /* slot < TWW_MAX_PEERS; NULL = inactive (puppet deleted) */
/* dSv_info_c, the GameCube save layout and byte order (big-endian), as offsets from the start of the
   struct (the Dolphin client's GameInfo base). Returns bytes copied; 0 past the end. */
uint32_t tww_save_size(void);
uint32_t tww_save_read(uint32_t offset, void* dst, uint32_t n);
uint32_t tww_save_write(uint32_t offset, const void* src, uint32_t n);

/* gate (fan-out: gate): the scene facts the client's sync services gate on, and the warp. Game thread only. */
typedef struct TwwGateState {
    char stage[8];            /* play.mCurStage's name: the current stage, "" before the first */
    int16_t start_point;      /* play.mCurStage: the point, room and layer the stage was entered with */
    int8_t start_room;
    int8_t start_layer;
    int8_t room;              /* dStage_roomControl_c::mStayNo, -1 if none */
    uint8_t link;             /* 1: the Link actor exists and runs */
    uint8_t next_stage;       /* play.mNextStage.mEnable: a stage change is pending */
    uint8_t event_mode;       /* play.mEvtCtrl.mMode: nonzero while an event runs (a talk, a shop, a cutscene) */
    uint8_t menu_paused;      /* dMenu_flag(): the pause / item menu is open */
    uint8_t mini_game;        /* play.mMiniGameType */
    uint8_t other_character;  /* 1: the player controls another actor than Link (Medli, Makar, a seagull...) */
    uint8_t pad0;
    uint32_t player_status0;  /* play.mPlayerStatus[0][0] (daPyStts0_*) */
    uint32_t link_mode_flg;   /* daPy_lk_c::mModeFlg, 0 without Link */
} TwwGateState;

void tww_gate_get_state(TwwGateState* out);
/* Moves the local Link within his room: current, old and kept position, both facings, speed zeroed.
   1 = moved; 0: no Link, or a stage change is pending. */
int tww_gate_warp_local(const float pos[3], int16_t angle_y);
/* dComIfGp_setNextStage(stage, point, room, layer): Link arrives standing, with the Magic Armor, a timed
   shield and the soup's power-up carried over. 1 = requested; 0: no Link, a change is already pending, or
   stage is not 1-7 letters, digits or '_'. */
int tww_gate_change_stage(const char* stage, int16_t point, int8_t room, int8_t layer);

/* Shared inventory (fan-out: inv). The play-side facts the inventory sync reads, and the changes it
   queues the way the game's own pickups do (dComIfGp_setItem*Count: d_meter applies a pending change
   next frame with the HUD animation and clamping, then zeroes it). Game thread, between frames. */
enum {
    TWW_INV_RUPEE = 0,     /* mItemRupeeCount */
    TWW_INV_KEY = 1,       /* mItemKeyNumCount (applied only in a stage that shows the key HUD) */
    TWW_INV_MAX_LIFE = 2,  /* mItemMaxLifeCount (quarter hearts) */
    TWW_INV_MAX_MAGIC = 3, /* mItemMaxMagicCount */
    TWW_INV_ARROW = 4,     /* mItemArrowNumCount */
    TWW_INV_BOMB = 5,      /* mItemBombNumCount */
    TWW_INV_BEAST = 6,     /* mItemBeastNumCounts[8], dBeastIndex_e order: TWW_INV_BEAST + i */
    TWW_INV_COUNTERS = TWW_INV_BEAST + 8,
};

typedef struct TwwInvPlay {
    int32_t pending[TWW_INV_COUNTERS]; /* TWW_INV_*: changes queued, not yet applied */
    uint8_t select_item[4];  /* play mSelectItem: the item number X, Y, Z (and the 4th) show */
    int8_t save_tbl;         /* the current stage's save slot (dStage_stagInfo_GetSaveTbl), -1 if not in a stage */
    uint8_t key_disp;        /* the current stage shows the small-key HUD (dStage_stagInfo_ChkKeyDisp) */
    uint8_t pad0[2];
} TwwInvPlay;

void tww_inv_get(TwwInvPlay* out);
void tww_inv_add(uint32_t counter, int32_t delta);               /* counter: TWW_INV_*; += as the game does */
void tww_inv_set_select_item(uint32_t button, uint8_t item_no);  /* button < 4: play mSelectItem[button] */

/* world (fan-out: world): the shared world outside the save window. Game thread only, between frames. */
#define TWW_WORLD_LIVE_WORDS 9          /* word 0: chests (dSv_memBit_c::mTbox), words 1-8: switches 0x00-0xFF */
#define TWW_WORLD_ZONE_ROOM_NONE 0xFF
#define TWW_WORLD_SALVAGE_MAX 160       /* dSalvage_control_c::MAX_TOTAL */

/* A registered salvage point (dSalvage_control_c::mInfo[i]). */
typedef struct TwwWorldSalvagePoint {
    int8_t room;      /* mRoomNo, -1: not registered */
    uint8_t save_no;  /* mSaveNo */
    uint8_t kind;     /* mKind */
    uint8_t flag;     /* mFlag (bit 0: salvaged) */
} TwwWorldSalvagePoint;

/* Bits other players set in the current stage, for the actors that read them only when created: each
   matching chest, bombable wall, crystal switch, key door... is re-created (the ww-online REL's live world). */
typedef struct TwwWorldLiveBatch {
    uint32_t seq;        /* even and new; tww_world_live_status's done_seq becomes it once handled */
    int32_t slot;        /* the save slot it is for; another stage's waits */
    int32_t zone_room;   /* the room the zone switches 0xC0-0xEF belong to, or TWW_WORLD_ZONE_ROOM_NONE */
    uint32_t bits[TWW_WORLD_LIVE_WORDS];
} TwwWorldLiveBatch;

typedef struct TwwWorldLiveStatus {
    uint32_t seq;         /* the last batch posted */
    uint32_t done_seq;    /* the last batch handled */
    uint32_t poke_count;  /* actors re-created so far */
    uint32_t retry;       /* with done_seq: actors left undone (not ready in ~2 s, refused): post it again */
} TwwWorldLiveStatus;

typedef struct TwwWorldStats {
    uint32_t despawn_count; /* placed items (and Heart Containers) removed so far */
    uint32_t last_despawn;  /* (slot << 16) | (item no << 8) | item bit of the last one */
} TwwWorldStats;

/* dStage_stagInfo_GetSaveTbl of the current stage: the dSv_save_c::mMemory index; -1 without Link or while
   a stage change is pending (the stage info is stale then). */
int32_t tww_world_save_slot(void);
/* dStage_roomControl_c::getZoneNo(room): the room's dSv_info_c::mZone index, -1 if none or room out of range. */
int32_t tww_world_room_zone(int32_t room);
/* The salvage points (daSalvage_c::mTagData_p) into out, at most max; 0 while there is no salvage actor. */
uint32_t tww_world_salvage_read(TwwWorldSalvagePoint* out, uint32_t max);
/* ORs bits into point index's mFlag (dSalvage_control_c::setFlag). 1 = done. */
int tww_world_salvage_set_flag(uint32_t index, uint8_t bits);
/* The item bits (mItem[0]) other players collected in save slot `slot`: placed items whose bit is also set in
   the live save data vanish from the current stage. mask 0: none. */
void tww_world_set_remote_items(int32_t slot, uint32_t mask);
void tww_world_get_stats(TwwWorldStats* out);
void tww_world_live_post(const TwwWorldLiveBatch* batch);
void tww_world_live_status(TwwWorldLiveStatus* out);

/* overlay (fan-out: overlay): the peers' name tags, the other players on the sea chart, and projectiles (a
   peer's bombs, boat cannonballs and arrows made real here; the local Link's reported). Game thread, between
   tww_run_frame calls, like the rest of the multiplayer API. */
#define TWW_OVERLAY_NAME_BYTES 24   /* a name tag, NUL included: printable ASCII is drawn, other bytes dropped */
#define TWW_OVERLAY_SEA_MARKERS 8

enum {
    TWW_OVERLAY_EVENT_BOMB_THROW = 1,  /* a bomb left the sender's hands (thrown / put down), lit and in flight */
    TWW_OVERLAY_EVENT_BOMB_PICKUP = 2, /* the sender picked their bomb up again */
    TWW_OVERLAY_EVENT_EXPLODE = 3,     /* the sender's bomb / cannonball exploded at pos */
    TWW_OVERLAY_EVENT_REMOVE = 4,      /* the sender's bomb / cannonball is gone without exploding (sank) */
    TWW_OVERLAY_EVENT_CANNON = 5,      /* the sender's boat cannon fired: the cannonball in flight */
    TWW_OVERLAY_EVENT_ARROW = 6,       /* the sender shot an arrow (variant = type) from pos along angle[0..1] */
};

typedef struct TwwOverlaySeaMarker {
    float x, z;        /* world position on the sea stage (the boat's while sailing) */
    int16_t angle_y;   /* heading */
    uint8_t r, g, b;   /* marker colour (the tunic's) */
    uint8_t pad0[3];
} TwwOverlaySeaMarker;

typedef struct TwwOverlayEvent {
    uint8_t kind;      /* TWW_OVERLAY_EVENT_* */
    uint8_t slot;      /* a peer's event: the sender's slot; a local event: 0 */
    uint8_t variant;   /* ARROW: the arrow type, 0 normal, 1 fire, 2 ice, 3 light; else 0 */
    int8_t room;       /* a local event: the projectile's room (current.roomNo), -1 if unknown; a peer's: unused */
    uint32_t id;       /* the sender's projectile (its process id there) */
    float pos[3];      /* current.pos */
    float speed_f;     /* speedF */
    float speed_y;     /* speed.y */
    float gravity;
    int16_t angle[3];  /* current.angle.x, current.angle.y, shape_angle.z */
    int16_t timer;     /* a bomb: fuse frames left; a cannonball: frames without gravity */
} TwwOverlayEvent;

/* The name over the slot's puppet; NULL or "" = none. */
void tww_overlay_set_peer_name(int slot, const char* utf8);
/* The other players on the sea chart (every zoom), replacing the last set; count <= TWW_OVERLAY_SEA_MARKERS. */
void tww_overlay_set_sea_markers(const TwwOverlaySeaMarker* markers, int count);
/* 1: report the local Link's projectiles (tww_overlay_poll_event); 0 (the default): none are. */
void tww_overlay_set_event_send(int on);
/* 1 = the oldest unread local event, taken off the queue; 0 = none. */
int tww_overlay_poll_event(TwwOverlayEvent* out);
/* A peer's event, applied within the next frames: 1 = queued, 0 = queue full or values out of range. */
int tww_overlay_push_peer_event(const TwwOverlayEvent* e);

/* Puppet fidelity (fan-out: puppet). A peer's body anims, face and equipment, and the local Link's to send.
   The puppet plays the anims it is given (any proc), else the wait / walk / run blend from TwwPeerState. */
#define TWW_PUPPET_TRACKS 5         /* under[0], under[1], upper[0], upper[1], upper[2] (item anims: aim, carry) */
#define TWW_PUPPET_ANM_NONE 0xFFFF  /* no anim, or one that cannot be mirrored (a cutscene's) */
#define TWW_PUPPET_ANM_SAME 0xFFFE  /* upper[0] / upper[1]: plays the under track of the same index */

enum {
    TWW_PUPPET_CLOTHES_HERO = 0,
    TWW_PUPPET_CLOTHES_CASUAL = 1,
    TWW_PUPPET_CLOTHES_DEFAULT = 2, /* as the game dresses Link from the save */
};

enum {
    TWW_PUPPET_LOOK_IRON_BOOTS = 1u << 0,
    TWW_PUPPET_LOOK_DAMAGE_FLASH = 1u << 1, /* (fan-out: pvp) hurt, flashing red: daPy_lk_c::mDamageWaitTimer > 0 */
};

enum {
    TWW_PUPPET_GRAB_NONE = 0,
    TWW_PUPPET_GRAB_BOMB = 1, /* a carried bomb, drawn between the puppet's hands */
};

typedef struct TwwPuppetTrack {
    uint16_t bck;     /* LkAnm bck index, TWW_PUPPET_ANM_NONE or TWW_PUPPET_ANM_SAME */
    int16_t start;    /* J3DFrameCtrl */
    int16_t end;
    int16_t loop;
    uint8_t attr;
    uint8_t pad0[3];
    float frame;      /* < 0: unknown (the track plays on from its start) */
    float rate;
    float ratio;      /* the track's share of the blend, 0..1 */
} TwwPuppetTrack;

typedef struct TwwPuppetAnim {
    uint32_t valid;   /* 0: none */
    TwwPuppetTrack track[TWW_PUPPET_TRACKS];
    uint16_t face_btp; /* LkAnm btp index (mouth, eyes), TWW_PUPPET_ANM_NONE */
    uint16_t face_btk; /* LkAnm btk index (eye scroll), TWW_PUPPET_ANM_NONE */
    uint16_t face_btp_frame;
    uint16_t face_btk_frame;
    uint8_t hand_l;   /* hands.bdl joint shown as the hand, 0 = the body's own */
    uint8_t hand_r;
    int16_t body_angle[3]; /* mBodyAngle: the aim's pitch and yaw */
} TwwPuppetAnim;

typedef struct TwwPuppetLook {
    uint32_t valid;   /* 0: none; the puppet shows the local save's equipment and clothes */
    uint8_t sword;    /* the save's equip slots: dItemNo, 0xFF none */
    uint8_t shield;
    uint8_t bracelet;
    uint8_t clothes;  /* TWW_PUPPET_CLOTHES_* */
    uint8_t tunic[3]; /* RGB; 0,0,0 or 30,100,35: the vanilla tunic */
    uint8_t flags;    /* TWW_PUPPET_LOOK_* */
    uint16_t equip_item; /* mEquipItem: the item in hand (dItemNo, or 0x100 none, 0x103 the sword) */
    uint8_t grab;     /* TWW_PUPPET_GRAB_* */
    uint8_t grab_fuse; /* the carried bomb's frames left, capped at 255; 0 unknown */
} TwwPuppetLook;

void tww_puppet_set_anim(int slot, const TwwPuppetAnim* anim); /* NULL = none */
void tww_puppet_set_look(int slot, const TwwPuppetLook* look); /* NULL = none */
/* The local Link's anims and look, as a peer's puppet takes them. 1 = valid (a Link actor runs). */
int tww_puppet_get_local(TwwPuppetAnim* anim, TwwPuppetLook* look);
/* The local Link's clothes (TWW_PUPPET_CLOTHES_*; DEFAULT leaves them to the game) and tunic colour. */
void tww_puppet_set_local_outfit(uint8_t clothes, uint8_t r, uint8_t g, uint8_t b);

/* boat (fan-out: boat)
 * The King of Red Lions (daShip_c): the local one's state, each peer's boat (drawn with the peer's puppet
 * seated in it, or empty where it lies when parked), and the boats' colours. Same rules as above. */
enum {
    TWW_BOAT_PARKED = 1u << 0,       /* nobody aboard: drawn empty where it is, speed 0 */
    TWW_BOAT_FLYING = 1u << 1,       /* daSFLG_FLY_e: airborne, pos[1] is used (else the local sea sets it) */
    TWW_BOAT_MAST_RAISED = 1u << 2,  /* the mast bck is FN_MAST_ON2 (raised / rising), else FN_MAST_OFF2 */
    TWW_BOAT_MAST_HIDDEN = 1u << 3,  /* m03E8 0.001: the mast scaled away, the cannon or crane out */
};

typedef struct TwwBoatState {
    float pos[3];           /* current.pos */
    float speed_f;          /* speedF, for prediction between updates (0 while parked) */
    int16_t angle_y;        /* shape_angle.y */
    int16_t sail_angle;     /* mSailAngle */
    int16_t tiller;         /* m0366 */
    int16_t head_x;         /* m03A0: head look pitch */
    int16_t head_y;         /* m03A2: head look yaw */
    int16_t cannon_yaw;     /* m0394 */
    int16_t cannon_pitch;   /* m0396 */
    int16_t crane_angle;    /* m0398 + m039C */
    uint32_t flags;         /* TWW_BOAT_* */
    uint8_t part;           /* mPart (daShip_c::Part_e): 0 wait, 1 steer, 2 cannon, 3 crane */
    uint8_t mast_frame;     /* mpBodyAnm's frame in the mast bck, whole frames */
    uint8_t head_bck;       /* m03B4, the head's "Ship" bck index; 0 = unknown (the receiver keeps its own) */
    uint8_t head_frame;     /* mpHeadAnm's frame, whole frames */
    uint8_t rope_length;    /* mRopeCnt: the crane rope in 10-unit segments */
    int8_t room;            /* current.roomNo, -1 if none */
    uint8_t pad0[2];
    char stage[8];          /* a peer's boat shows only while this equals the local stage */
} TwwBoatState;

typedef struct TwwBoatLook {
    uint16_t hull_rgb565;   /* the hull's red, RGB565; 0 = the classic red */
    uint8_t pad0[2];
    uint32_t sail_rgb;      /* 0xRRGGBB on the sail's canvas (the tunic colour); 0 = the vanilla sail */
} TwwBoatLook;

int tww_boat_get_local(TwwBoatState* out); /* 1 = the local boat is in this stage, ridden or parked */
/* slot < TWW_MAX_PEERS; boat NULL = none (the boat is deleted), look NULL = the vanilla colours */
void tww_boat_set_peer(int slot, const TwwBoatState* boat, const TwwBoatLook* look);
void tww_boat_set_local_look(const TwwBoatLook* look); /* the local boat's colours; NULL = vanilla */

/* settings (fan-out: settings): the options menu's settings (pc_settings.h) for a host's own settings page:
 * graphics, performance, quality of life, controls (the keyboard bindings are the view's keys) and debug. Values
 * are strings, as in settings.ini. Game thread, between tww_run_frame calls. */

/* The catalogue as JSON (UTF-8) into buf, NUL-terminated: {"mode": "handheld"|"docked", "path": settings.ini,
   "settings": [{"key", "label", "help", "tab", "tabLabel", "group" ("keyboard" / "controller" for a binding, else ""),
   "type" ("bool": "0"/"1", "choice", "binding"), "default", "value", "restart" (applies at the next start),
   "perMode" (a value per operation mode: the current mode's is shown and set), "locked" (set by the environment),
   "restartPending", "choices": [{"value", "label"}]}]} in menu order. Returns the bytes it needs, NUL included;
   writes nothing when buf is NULL or smaller. */
uint32_t tww_settings_catalogue(char* buf, uint32_t size);
/* A catalogue setting's value in effect, NUL-terminated into buf; returns the bytes it needs, 0 for a key not in
   the catalogue. */
uint32_t tww_settings_get(const char* key, char* buf, uint32_t size);
/* Sets it as the options menu does: saved to settings.ini and applied now (a "restart" setting at the next start).
   1 = set; 0: not in the catalogue, locked, or not one of its choices. */
int tww_settings_set(const char* key, const char* value);

/* pvp (fan-out: pvp): other players as L-target battle targets, and hits between Links. Victim-authoritative: the
 * attacker's game reports each hit its Link (sword, arrows, bombs, boomerang, hookshot...) lands on a peer's puppet
 * (tww_pvp_poll_hit); the client relays it to that peer, whose game applies it to its own Link as the same attack
 * from the attacker's puppet (tww_pvp_apply_hit): the stock damage, guard, knockback, invincibility and death.
 * Game thread, between tww_run_frame calls. */
#define TWW_PVP_MAX_DAMAGE 80   /* quarter hearts (20 hearts) */
#define TWW_PVP_SPL_MAX 0x0B    /* dCcG_At_Spl: the attack's special reaction (0 normal; 8 and up can't be guarded) */

typedef struct TwwPvpHit {
    uint8_t slot;          /* polled: the peer whose puppet was hit; applied: the peer who hit the local Link */
    uint8_t damage;        /* quarter hearts, 1..TWW_PVP_MAX_DAMAGE (the attack's power) */
    uint8_t spl;           /* 0..TWW_PVP_SPL_MAX */
    uint8_t pad0;
    uint32_t at_type;      /* the attack's AT_TYPE_* bits: sword 0x2, bomb 0x20, boomerang 0x40, arrow 0x4000, hookshot
                              0x8000, skull hammer 0x10000, fire / ice / light arrow 0x40000 / 0x80000 / 0x100000 */
    float pos[3];          /* where it hit, in the attacker's world */
    float attacker_pos[3]; /* the attacker's Link (current.pos): the knockback points away from it */
} TwwPvpHit;

/* Bit i: slot i's player takes part: their puppet is an L-target battle target and can be hit, and their hits on the
   local Link are applied. 0 (the default): no PVP, puppets are not targets. */
void tww_pvp_set_slots(uint32_t slot_mask);
/* 1 = the oldest hit the local Link landed on a taking-part puppet, taken off the queue; 0 = none. */
int tww_pvp_poll_hit(TwwPvpHit* out);
/* A peer's hit on the local Link, applied within the next frames: 1 = queued; 0: the slot does not take part, the
   queue is full or a value is out of range. */
int tww_pvp_apply_hit(const TwwPvpHit* hit);

/* smooth (fan-out: smooth): peers drawn from timed states. tww_set_peer and tww_boat_set_peer keep each slot's newest
 * states with their send times; the puppet and the boat are drawn a little behind the newest (COS_MP_INTERP_MS, default
 * 100), between the two states around that time, carried on along their way for up to 150 ms when none came, and at
 * once where a state jumps (300 units for the puppet, 1000 for the boat, or another stage). Game thread only. */
/* When the states slot's next tww_set_peer / tww_boat_set_peer calls carry were sampled: seconds on the sender's
   clock (any epoch: only the differences count). 0 (the default) = unknown: each state is stamped as it arrives. */
void tww_smooth_set_time(int slot, double sent_at);

#ifdef __cplusplus
}
#endif

#endif
