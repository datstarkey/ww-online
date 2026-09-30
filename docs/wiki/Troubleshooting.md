# Troubleshooting

## Starting the game

- **"Windows protected your PC" when installing:** the installer isn't code-signed yet. Click **More info**, then **Run anyway**.
- **WW-Online says to patch your game, and won't start Dolphin:** the game isn't patched yet, WW-Online was updated with new game code, or you changed your [game patches](Game-Patches.md). Press **Patch now** in the banner (or **Settings**, **Patch game**).
- **Purple screen with `d_s_play.cpp`, or a black screen on boot:** Dolphin's 48 MB memory setting is off (only when you start Dolphin yourself: see [Installing](Installing.md#dolphins-memory-setting)), or the game folder isn't a patched `GZLE01` copy.
- **"WW-Online only works with the US GameCube version, GZLE01" when choosing your game:** only the US version of The Wind Waker works. European (PAL) and Japanese copies, and the Wii U's Wind Waker HD, don't.
- **"No game found here" / "not a folder" / "Extract the entire disc":** WW-Online needs the whole disc extracted to a folder. Choosing the disc image in the setup does that for you (see [Installing](Installing.md#3-run-the-setup)).
- **WW-Online won't link up with a Dolphin you started yourself:** the running game must be your patched `GZLE01` copy, with the 48 MB memory setting on. A vanilla or PAL game is refused.

## Connecting

- **Can't connect:** check the host's address and port, and that the host's PC is reachable (see [Playing over the internet](Playing-Together.md#playing-over-the-internet)). On a dedicated server, check it's up: [Self-hosting](../self-hosting.md#health-check-and-logs).
- **"Different version":** everyone in a room needs the same WW-Online version, and so does a dedicated server. Update the app (it updates itself: **Restart to update**), or the server.
- **Players don't appear:** everyone must be in the same room and past the title screen. You see a player when you're in the same room of the same area (on the Great Sea, when they're close). You see up to three other players at once.

## In the game

- **Stuck after another player moved the story on:** **Warp to** them. See [Softlocks and warping](../softlocks.md).
- **Item icons are missing in the app:** **Settings**, **Item icons**, **Read icons from game** reads them from your own game files.
- **Double magic you didn't earn** (a room from before 0.4.3): the room owner sets Magic back to **Normal** on the Room items page, or you start a new room.
- **The game crashes or freezes:** please report it (below).

## Reporting a bug

Please [open an issue](https://github.com/datstarkey/ww-online/issues) with:

- what you were doing, and where (island or dungeon, and room);
- what the other players had just done;
- the room's rules (Full sync, Co-op or which rules were on);
- your WW-Online version (**Settings**);
- your logs, and the other players' if you can: `%LocalAppData%\WWOnline\logs`.

For a softlock, [Softlocks and warping](../softlocks.md#reporting-a-new-one) says what helps most.
