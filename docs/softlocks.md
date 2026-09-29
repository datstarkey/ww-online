# Softlocks and "Warp to player"

A room can share story and world progress (**Shared story**, **Shared world**). Both merge in one direction only: once any player sets a story flag or opens a way, it is set for everyone, and it is never taken back. That is what lets players split up and finish different parts of the game.

The catch: some of the game's progress moves you as well as setting a flag. When one player does it, the flag reaches everyone else, but they are still standing where they were. Their game now believes that step is done, so the thing that would have moved them (a character to talk to, a boat to board, a door that opens once) may no longer be there. They are **softlocked**: nothing is broken, but they have no way forward from where they stand.

## How to recover: Warp to player

Each other player on the **Room** page's **Players** list has a **Warp to** button. It is there while the room's **Allow warping** rule is on (on in Full sync, off in Co-op, where nothing syncs and nobody can get stuck this way). The room owner can change it under **Edit room**. A dedicated server sets its starting value with `WWO_ALLOW_WARPING` / `--no-warping` ([self-hosting](self-hosting.md)).

What a warp does depends on where the other player is:

- **Same area** (the same stage and the same room): you are moved straight to them, facing the way they face. There is no loading screen.
- **Anywhere else** (another stage, or another room of the same stage): their area loads for you **at the entrance they came in through**, meaning the door, loading zone or spawn point they last entered it by. You arrive there, not next to them. If that entrance plays a cutscene, it plays for you too.

A line under the Players list says what happened, or why the warp was refused. It is refused when:

- the room's **Allow warping** rule is off;
- you are on the title screen or file select, in a cutscene or conversation, between areas, down (no hearts), in a minigame, in the pause menu, or controlling another character (Medli, Makar, a seagull...);
- the other player is in any of those states, or hasn't sent a position for a few seconds (they may have closed the game);
- you are in the same area but holding on to something (a ledge, a ladder, a rope, a block) or sitting in your boat: let go or get off first;
- they are in another area but their entrance isn't known. This happens when they arrived there by respawning (a void-out, a game over, leaving a ship's interior) or when their app started while they were already there. It becomes known again as soon as they go through a door or a loading zone.

Every warp writes `[warp]` lines to your client log (`logs/latest/client-Player<N>.log` in a dev test, `%LocalAppData%\WWOnline\logs` for an installed app): where you were, where they were, what the warp did, and whether it worked.

## Known softlocks

Each entry says where it happens, what triggers it, what the stuck player sees, and how to get out.

### Outset Island: leaving with Tetra

- **Where:** Outset Island, at the start of the game.
- **Trigger:** one player talks to Tetra and boards the pirate ship.
- **Symptom:** the other players can no longer board. Tetra and the ship's way on are gone for them, so they are stuck on Outset.
- **Fix:** **Warp to** the player who left. You arrive where they entered their current area and carry on from there.

## Reporting a new one

If you get stuck and warping doesn't help, or you find a softlock that isn't listed, please [open an issue](https://github.com/datstarkey/ww-online/issues) with:

- where you were (island or dungeon and room) and what you were trying to do;
- what the other players had just done (who talked to whom, who opened what);
- the room's rules (Full sync, Co-op or which rules were on);
- your client log, and the other players' if you can: `%LocalAppData%\WWOnline\logs`, or `logs/latest/` in a dev test (its `client-Player<N>.log` has the `[story]`, `[world]` and `[warp]` lines).
