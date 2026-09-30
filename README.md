<p align="center">
  <img src="assets/branding/logo.png" alt="WW-Online" width="320">
</p>

<h3 align="center">Online multiplayer for The Legend of Zelda: The Wind Waker</h3>

<p align="center">
  Sail the Great Sea with your friends. Everyone plays their own copy of the game in Dolphin,<br>
  and WW-Online brings the other players into your world as fully animated Links and keeps your adventure in sync.
</p>

<p align="center">
  <a href="https://github.com/datstarkey/ww-online/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/datstarkey/ww-online?include_prereleases&style=for-the-badge&color=37D3BF&labelColor=0D151B"></a>
  <a href="https://github.com/datstarkey/ww-online/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/datstarkey/ww-online/total?style=for-the-badge&color=F4B942&labelColor=0D151B"></a>
  <a href="LICENSE"><img alt="MIT licence" src="https://img.shields.io/badge/licence-MIT-5BD17A?style=for-the-badge&labelColor=0D151B"></a>
  <img alt="Windows" src="https://img.shields.io/badge/Windows-10%20%7C%2011-5B9DFF?style=for-the-badge&labelColor=0D151B">
  <img alt="Dolphin" src="https://img.shields.io/badge/Dolphin-GZLE01-A98BFF?style=for-the-badge&labelColor=0D151B">
</p>

<p align="center">
  <a href="#install"><b>Install</b></a> ·
  <a href="#play"><b>Play</b></a> ·
  <a href="#what-it-does"><b>Features</b></a> ·
  <a href="#troubleshooting"><b>Troubleshooting</b></a> ·
  <a href="https://github.com/datstarkey/ww-online/wiki"><b>Wiki</b></a> ·
  <a href="#building-from-source"><b>Build from source</b></a> ·
  <a href="#credits"><b>Credits</b></a>
</p>

<p align="center">
  <img src="docs/screenshots/room-owner-edit.png" alt="The Room page: the room owner editing the room rules" width="820">
</p>

> **You need your own copy of the game.** WW-Online contains no Nintendo code, files or assets, and never will. It patches the game on your PC from a disc image you provide: a US copy of The Wind Waker (game ID `GZLE01`) that you dumped from a disc you own. Please don't ask for, or share, game files.

> **Status: early development.** Things work, and things break. Expect bugs, and expect everyone in a room to need the same WW-Online version.

## What it does

**See each other**
- Other players appear as real Links: running, rolling, crouching, sword combos, shield, sheathing and the Master Sword glow.
- They climb like you do: ledge grabs, hanging and shimmying, ladders and vine walls, sidling along walls, pushing and pulling blocks, and crawling.
- Everything else Link does plays too: Z-target side hops and strafing, knock-backs, slides, swimming up, parries, ice slips, rope swings, boarding the boat, and poses like talking, opening chests and holding up items. Anything WW-Online has no special handling for is copied straight from their game (body, arms, face and hands).
- Their equipment shows: sword, shield (including the Mirror Shield) and hero's clothes or pajamas.
- They hold and use their items: bow (with aim), boomerang, hookshot, Deku Leaf, Skull Hammer, Wind Waker, bottles, telescope, Picto Box, Tingle Tuner, and a carried bomb with a burning fuse.
- Each player picks their own tunic colour (black Link, purple Link, anything), and everyone sees it change live.
- Player names above heads: each player's name floats above their Link, in the game's own font. It shrinks with distance and hides in cutscenes and menus (turn it off under **Appearance**).
- Out on the Great Sea you see each other sailing in your own King of Red Lions, with the sail in their tunic colour, and their cannon or salvage crane when they take it out. Get off your boat and the others still see it parked where you left it. Each player picks their boat's colour (it matches their tunic by default).
- Everyone on the Great Sea shows up on your sea chart in the pause menu, in their tunic colour and pointing the way they face, even when they're too far away to see.
- You see up to three other players at once.

**Play one adventure together (a "room")**

One player hosts a room and becomes the **room owner**. The owner picks how much the room shares:

| Rule | What it means |
|------|---------------|
| **Shared wallet** | One rupee purse. Anyone's rupees count for everyone. |
| **Shared world** | Chests, switches and pickups are gone for everyone once someone takes them, and it happens live if you're in the same room: a chest opens empty, a bombed wall vanishes, a locked door comes back unlocked, and a ladder drops or a torch lights when another player clears the room. Small keys too: a key anyone finds is everyone's, and a door anyone unlocks uses it up for everyone. Sunken treasure too: once someone salvages it, it's gone for everyone. |
| **Shared items** | One player finding an item unlocks it for the whole room, including the magic meter, treasure and Triforce charts (owned, deciphered and salvaged) and the squares filled in on the sea chart. Max hearts come from the room's pieces: every Heart Container and Piece of Heart anyone gets counts once for everyone. |
| **Shared story** | Main story progress is shared, so you can split up and finish different parts of the game. So is the Nintendo Gallery: a figurine Carlov makes for anyone is made for everyone, so you can split the Picto Box photos between you. And the dungeon warp jars anyone has opened, Beedle's point card (the room keeps the highest) and the postboxes (a letter read by one is read for all). |
| **Shared bait bag** | One stock of All-Purpose Bait and Hyoi Pears. Anyone's purchase, pickup or use counts for everyone. |
| **Shared spoils bag** | One stock of Joy Pendants, Skull Necklaces, Chu Jellies, Knight's Crests and the other spoils. Anyone's pickup, sale or trade counts for everyone. |
| **Shared delivery bag** | One bag of quest items: trade goods, letters, the Cabana Deed and Beedle's tickets. An item anyone receives is in everyone's bag, and one anyone hands over, posts or trades is gone from everyone's. If two players trade the same item at once, only the first trade counts. |
| **Other players' projectiles** | Other players' bombs, boat-cannon shots and arrows are real in your game: they fly, explode and hit your enemies and walls. Off: you still see them carry a bomb or aim, but nothing flies. |
| **Allow warping** | A **Warp to** button on each player in the Players list. In the same area it moves you next to them. Anywhere else it takes you to the entrance they came in through. |

- **Full sync** turns everything on. **Co-op** turns the seven shared-progress rules and warping off, so you see each other (and each other's projectiles) but keep your own progress.
- Joining a room never throws away progress: if you're further ahead than the room, your progress is added to it.
- Never shared: health, magic, bomb and arrow counts, bottle contents (the bait, spoils and delivery bags only with their Shared rules), and Picto Box photos (each player gives Carlov their own).
- The Room page's **Dungeons** card shows each dungeon's small keys, map, compass, big key and boss, and the Room items page shows how many of the 6 Heart Containers and 44 Pieces of Heart the room has.
- The app's item icons are read from your own game files and stay on your PC.

[Room rules](https://github.com/datstarkey/ww-online/wiki/Room-Rules) on the wiki explains every rule in detail.

**Planned:** shared weather, wind and time of day (off by default), a "no player collision" option (players bump into each other today), PVP and riding in each other's boats.

<table>
  <tr>
    <td width="50%"><img src="docs/screenshots/room-player.png" alt="A player's read-only view of the room"></td>
    <td width="50%"><img src="docs/screenshots/room-items.png" alt="The room items editor"></td>
  </tr>
  <tr>
    <td align="center"><sub>Everyone else sees the room read-only, with what's happening live.</sub></td>
    <td align="center"><sub>The room owner decides the room's items. Nothing changes until you press Edit.</sub></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/screenshots/appearance.png" alt="The Appearance page with a purple hero's tunic picked"></td>
    <td width="50%"><img src="docs/screenshots/game-patches.png" alt="The Game patches checklist in Settings"></td>
  </tr>
  <tr>
    <td align="center"><sub>Pick your clothes and tunic colour. Everyone in the room sees it live.</sub></td>
    <td align="center"><sub>Choose optional game patches, then press Patch game.</sub></td>
  </tr>
</table>

## Requirements

- **Windows 10 or 11**, 64-bit.
- **[Dolphin](https://dolphin-emu.org/download/)**, a recent development build.
- **Your own Wind Waker disc image**: the US version, `GZLE01`, dumped from your own disc (ISO, RVZ, GCM…).
- **Friends to play with**, and a way to reach each other over the internet (see [Playing over the internet](#playing-over-the-internet)).

## Install

1. **Download WW-Online** from the [Releases](../../releases) page (`WWOnline-win-Setup.exe`) and run it. It installs for your user only, updates itself, and bundles everything it needs. (Windows may say **"Windows protected your PC"** because the installer isn't code-signed yet: click **More info → Run anyway**.)
2. **Get [Dolphin](https://dolphin-emu.org/download/)** (a recent development build) and unzip it anywhere.
3. **Open WW-Online.** A short setup finds Dolphin, extracts and checks your disc image (`GZLE01`), lets you pick optional game patches, your name and tunic colour, and then patches your game. Your original is only read, never changed.

Patch again only when WW-Online asks (after an update with new game code, or when you change your patches). The full guide, including Dolphin's 48 MB memory setting if you start Dolphin yourself, is **[Installing](https://github.com/datstarkey/ww-online/wiki/Installing)** on the wiki.

## Play

- **Host a room:** on the **Room** page choose **Host a room**. WW-Online starts the room on your PC (port `6969`), starts your patched game in Dolphin and links up with it. You're the room owner: press **Edit room** to pick Full sync, Co-op or your own mix of rules.
- **Join a room:** enter the host's address and your name, then **Join**. You see the room's rules and items read-only, and anything you pick up still counts for the room.
- **Your look:** open **Appearance** to pick your clothes, tunic colour and boat colour. Everyone sees it change live.
- **Warp to a player:** with **Allow warping** on, press **Warp to** next to a player on the **Room** page.

**[Playing together](https://github.com/datstarkey/ww-online/wiki/Playing-Together)** on the wiki covers every page of the app.

### Playing over the internet

The host's PC must be reachable on port `6969`. The easiest options:
- **[Tailscale](https://tailscale.com/)** (recommended): everyone installs it and joins the host's tailnet, then connects to the host's Tailscale IP. No router changes needed.
- **Port forwarding:** forward TCP `6969` on the host's router to the host's PC, and friends connect to the host's public IP.
- **Dedicated server:** run the server on any always-on machine or VPS, and everyone connects to it. There's a Docker image (amd64 and arm64):
  ```
  docker run -d --name ww-online --restart unless-stopped -p 6969:6969 ghcr.io/datstarkey/ww-online-server:latest
  ```
  [Self-hosting](docs/self-hosting.md) covers Docker Compose, the settings, the optional owner key (so the right player owns the room), firewalls, Tailscale and TLS. The release zips run without Docker too.

## Troubleshooting

- **Stuck? Warp to a player.** Shared story and world only ever move forward, so another player's progress (say, leaving Outset Island with Tetra) can leave you with no way on. Press **Warp to** next to them on the **Room** page. See [Softlocks](docs/softlocks.md).
- **Purple screen with `d_s_play.cpp` / black screen on boot:** Dolphin's 48 MB memory setting is off (only when you start Dolphin yourself), or the game folder isn't a patched `GZLE01` copy.
- **Players don't appear:** everyone must be on the same WW-Online version, in the same room, and past the title screen. You see a player when you're in the same room of the same stage (on the Great Sea, when they're close).
- **The game crashes or freezes:** please open an issue with WW-Online's logs (`%LocalAppData%\WWOnline\logs`) and what you were doing.

More on the wiki: **[Troubleshooting](https://github.com/datstarkey/ww-online/wiki/Troubleshooting)** and **[Known limitations](https://github.com/datstarkey/ww-online/wiki/Known-Limitations)**.

## Building from source

For developers. You need the [.NET 9 SDK](https://dotnet.microsoft.com/download) and, to rebuild the in-game code, [devkitPro's devkitPPC](https://devkitpro.org/wiki/Getting_Started) (`C:\devkitPro\devkitPPC`).

```
git clone --recursive <this repo>
dotnet build "WW-Online.sln"
dotnet test  "WWOnline.Tests/WWOnline.Tests.csproj"
dotnet run --project WWOnline.Client/WWOnline.Client.csproj
```

- Copy `GameMod/config.example.json` to `GameMod/config.json` and set `vanilla_game_path` (your extracted game) and `game_path` (the patched output).
- `.\scripts\dev-test.ps1` runs a local two-player test: two Dolphins and two clients, with Player 1 hosting. `-Patch` also rebuilds the in-game code. Logs go to `logs/latest/`.
- `python scripts/dolphin-crash-context.py <dolphin pid> tww-decomp/config/GZLE01/symbols.txt` prints the registers and stack of a game crash from the running Dolphin (read-only).
- `CLAUDE.md` and `GameMod/CLAUDE.md` describe the architecture, memory map and coding rules. `docs/` has the design notes (held items and projectiles, live world, small keys, hearts, event flags, delivery bag, figurines, optional patches), [softlocks and warping](docs/softlocks.md), [self-hosting](docs/self-hosting.md) and [releasing](docs/releasing.md).
- The [wiki](https://github.com/datstarkey/ww-online/wiki) is built from `docs/wiki/` (plus the softlocks and self-hosting pages) by `scripts/build-wiki.py`, and pushed on every push to `main`. Edit the files here, never the wiki itself.

**How it works, in short:** an Avalonia desktop app reads and writes the running game's memory through Dolphin, and a SignalR server relays each player's state. On the game side, a small injected module (C, built with devkitPPC and linked against [the Wind Waker decompilation](https://github.com/zeldaret/tww)) spawns and animates the other players' Links. The game's own code does the work, so they move, fight and draw just like the real Link.

## Legal

WW-Online is a fan project. It is not affiliated with, endorsed by or sponsored by Nintendo. The Legend of Zelda and The Wind Waker are trademarks of Nintendo. This repository contains no Nintendo code, assets or game files. You must own the game and use a disc image you dumped yourself.

## Credits

**WW-Online would not be possible without these projects.** Huge thanks to everyone behind them:

- **[zeldaret/tww](https://github.com/zeldaret/tww)**, the Wind Waker decompilation. Every game structure, offset and function this project touches was understood through it.
- **[WW_Hacking_API](https://github.com/LagoLunatic/WW_Hacking_API)** by LagoLunatic. The approach to building custom code into the game (C to REL, ASM patches) and the vanilla game headers in `GameMod/include/` come from it.
- **[Wind Waker Randomizer (wwrando)](https://github.com/LagoLunatic/wwrando)** by LagoLunatic and contributors. Our archive, REL and Yaz0 handling is ported from it, and so are several game patches, including skip intro.
- **[Better Wind Waker (betterww)](https://github.com/WideBoner/betterww)** by WideBoner, brainfubar and contributors, building on wwrando. The optional quality-of-life patches come from it.
- **[Dolphin](https://dolphin-emu.org/)**, the GameCube and Wii emulator, without which none of this would run.

WW_Hacking_API, wwrando and betterww are MIT-licensed; their licence notices are in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

Fonts: Fredoka, Hanken Grotesk and JetBrains Mono, under the SIL Open Font License (see [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md)).

## License

WW-Online's own code is released under the [MIT License](LICENSE). It covers this project's code only: The Wind Waker itself belongs to Nintendo and is not part of this project.
