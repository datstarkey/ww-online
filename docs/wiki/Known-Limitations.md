# Known limitations

WW-Online is in early development. These are the known gaps; the [releases](https://github.com/datstarkey/ww-online/releases) say what each version changed.

## Players

- You see up to three other players at once.
- Players bump into each other. A "no player collision" option is planned.
- Players can't hurt each other, and other players' projectiles never hurt you. PVP is planned.
- Other players' boomerang and hookshot show in their hands, but the thrown boomerang, the hookshot chain and the grappling hook don't. Carrying a pot or barrel shows the pose only.
- Props that go with an animation aren't shown: the rope, the item held up after a chest, a Boko weapon in hand. Animations that come from cutscene data aren't copied.
- Some moves are a best guess: a knock-back always looks like a hit from the front, and a side hop with the stick let go goes left.
- The zoomed-in sea chart shows the other players only on the square you're in (that's when it shows Link).

## The shared world

- Enemies aren't shared: each player fights their own. Rooms that open when every enemy is defeated need your own kills, except where the room's "cleared" switch opens something for everyone.
- When another player hits a switch in your room, your game may play its own short cutscene for it.
- A few timed puzzles stay solved for the others once one player solves them.
- Two players opening the same chest at the same moment can both get its contents.
- A warp jar whose lid is still on screen when another player opens it updates when you next enter that room (the jar itself works straight away).
- Items that only reached you from another player (bait, spoils, delivery items) count as obtained, so you won't see their first-pickup message later.

## Warping

- A warp to another area needs the other player to have come in through a door or loading zone (not a void-out or respawn). If their entrance plays a cutscene, it plays for you too.

## Rooms

- A room lives in the server's memory: restarting the server empties it. Everyone's own game keeps its progress, and the room fills up again when players rejoin.
- Only the US version of the game (`GZLE01`) is supported.
- Shared weather, wind and time of day are planned (off by default), and so is riding in each other's boats.
