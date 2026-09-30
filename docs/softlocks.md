# Softlocks and "Warp to player"

A room can share story and world progress (**Shared story**, **Shared world**). Both merge in one direction only: once any player sets a story flag or opens a way, it is set for everyone, and it is never taken back. That is what lets players split up and finish different parts of the game.

The catch: some of the game's progress moves you as well as setting a flag. When one player does it, the flag reaches everyone else, but they are still standing where they were. Their game now believes that step is done, so the thing that would have moved them (a character to talk to, a boat to board, a door that opens once) may no longer be there. They are **softlocked**: nothing is broken, but they have no way forward from where they stand.

## How to recover: Warp to player

Each other player on the **Room** page's **Players** list has a **Warp to** button. It is there while the room's **Allow warping** rule is on (on in Full sync, off in Co-op, where no progress is shared and nobody can get stuck this way). The room owner can change it under **Edit room**. A dedicated server sets its starting value with `WWO_ALLOW_WARPING` / `--no-warping` ([self-hosting](self-hosting.md)).

What a warp does depends on where the other player is:

- **Same area** (the same stage and the same room), **or both of you out on the Great Sea**: you are moved straight to them, facing the way they face. There is no loading screen. (The sea's grid squares are rooms that load wherever Link is, so a warp across the sea is a move too.)
- **Anywhere else** (another stage, or another room of the same stage): their area loads for you **at the entrance they came in through**, meaning the door, loading zone or spawn point they last entered it by. You arrive there, not next to them. If that entrance plays a cutscene, it plays for you too.

A line under the Players list says what happened, or why the warp was refused. It is refused when:

- the room's **Allow warping** rule is off;
- you are on the title screen or file select, in a cutscene or conversation, between areas, down (no hearts), in a minigame, in the pause menu, or controlling another character (Medli, Makar, a seagull...);
- the other player is in any of those states, or hasn't sent a position for a few seconds (they may have closed the game);
- you are in the same area but holding on to something (a ledge, a ladder, a rope, a block) or sitting in your boat: let go or get off first;
- they are in another area but their entrance isn't known. This happens when they arrived there by respawning (a void-out, a game over, leaving a ship's interior) or when their app started while they were already there. It becomes known again as soon as they go through a door or a loading zone.

Every warp writes `[warp]` lines to your client log (`logs/latest/client-Player<N>.log` in a dev test, `%LocalAppData%\WWOnline\logs` for an installed app): where you were, where they were, what the warp did, and whether it worked.

## Known softlocks

Each entry says where it happens, what triggers it, what the stuck player sees, and how to get out. The list comes from play and from an audit of the story flags that change where a save loads, which island layout you see, and whether you can board the King of Red Lions (the event flag catalogue's risky flags, checked in the game's code).

**Two general ways out**, besides **Warp to**:
- **Reload your save.** Save, go back to the title screen and load. Where a save loads follows the story (Outset, the pirate ship, the Forsaken Fortress, then Windfall once anyone has met the King of Red Lions), so after another player got ahead in the prologue, a reload puts you where they are in the story.
- **Leave and come back.** Island layouts (who stands where, what's open) are chosen when the area loads, so a change another player caused shows after you leave the area and return.

### Outset Island: leaving with Tetra

- **Where:** Outset Island, at the start of the game.
- **Trigger:** one player talks to Tetra and boards the pirate ship.
- **Symptom:** the other players can no longer board. Tetra and the ship's way on are gone for them, so they are stuck on Outset.
- **Fix:** **Warp to** the player who left. You arrive where they entered their current area and carry on from there.

### The pirate ship and the Forsaken Fortress, before the boat

- **Where:** the pirate ship, the first visit to the Forsaken Fortress, and Windfall before you've sailed the King of Red Lions.
- **Trigger:** another player moves the prologue on: the barrel launch to the Forsaken Fortress, then being thrown out of it and meeting the King of Red Lions at Windfall.
- **Symptom:** the event that would move you on (Tetra's barrel, the Helmaroc King throwing you out) has already happened as far as your game knows, so it doesn't play, and there's no other way off the ship or out of the fortress.
- **Fix:** **Warp to** the player ahead, as many times as it takes: each warp takes you to where they came in, so if they've moved on again, warp again. Or reload your save: once anyone has met the King of Red Lions, your save loads at Windfall.

### Boarding the King of Red Lions after the first trip to Hyrule (fixed in 0.6.2)

- **Where:** anywhere on the Great Sea, from another player's first descent into Hyrule until the Master Sword.
- **What happened:** the game doesn't let you board the King of Red Lions after the descent until the Master Sword is equipped (`d_a_ship.cpp:4225`). The descent's flag reached everyone, so anyone who got off their boat couldn't get back on.
- **Now:** that flag waits until your game has the Master Sword equipped (it comes through Shared items once anyone pulls it). Your client log says `[story] holding back UNK_2D10 until the Master Sword is equipped here`.

### Boarding the King of Red Lions after Hyrule's courtyard (fixed in 0.6.2)

- **Where:** anywhere on the Great Sea, from another player's Hyrule courtyard scene until Zelda awakens.
- **What happened:** the same boarding lock, until ZELDA_AWAKENED (`d_a_ship.cpp:4226`).
- **Now:** the courtyard flag waits until your game has ZELDA_AWAKENED. If you reach the courtyard first yourself, the scene plays for you as normal.

### Medli and Makar

- **Where:** Dragon Roost / Headstone Island and the Earth Temple (Medli); Forest Haven / Gale Isle and the Wind Temple (Makar).
- **Trigger:** another player recruits the companion.
- **What happens:** your game takes the companion as recruited. Medli then rides your King of Red Lions to Headstone Island (`d_a_npc_md.cpp:604`, until the island's landing), as she would if you had recruited her.
- **If you're stuck:** follow the player who has the companion through the temple: switches and doors they open open for you too (Shared world). Or **Warp to** them.

### Changed islands

- **Where:** Outset, Windfall, the Forsaken Fortress, Forest Haven and Hyrule change their layout at story points (who is there, what's open).
- **Symptom:** after another player reaches one of those points, someone you needed to talk to is gone, or a place looks further along than you are.
- **Fix:** the room's story is past that step, so carry on from where the room is: **Warp to** the player ahead, or leave and come back to see the new layout.

## Reporting a new one

If you get stuck and warping doesn't help, or you find a softlock that isn't listed, please [open an issue](https://github.com/datstarkey/ww-online/issues) with:

- where you were (island or dungeon and room) and what you were trying to do;
- what the other players had just done (who talked to whom, who opened what);
- the room's rules (Full sync, Co-op or which rules were on);
- your bug report (**Settings → Bug report → Save bug report** zips your logs), and the other players' if you can; or the logs themselves: `%LocalAppData%\WWOnline\logs`, or `logs/latest/` in a dev test (its `client-Player<N>.log` has the `[story]`, `[world]` and `[warp]` lines).
