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
- Other players appear as real Links: running, rolling, climbing, crouching, sword combos, shield, sheathing and the Master Sword glow.
- Their equipment shows: sword, shield (including the Mirror Shield) and hero's clothes or pajamas.
- Each player picks their own tunic colour (black Link, purple Link, anything), and everyone sees it change live.
- Player names above heads: each player's name floats above their Link, in the game's own font. It shrinks with distance and hides in cutscenes and menus (turn it off under **Appearance**).
- Out on the Great Sea you see each other sailing in your own King of Red Lions.

**Play one adventure together (a "room")**

One player hosts a room and becomes the **room owner**. The owner picks how much the room shares:

| Rule | What it means |
|------|---------------|
| **Shared wallet** | One rupee purse. Anyone's rupees count for everyone. |
| **Shared world** | Chests, switches and pickups are gone for everyone once someone takes them. |
| **Shared items** | One player finding an item unlocks it for the whole room, including heart containers and the magic meter. |
| **Shared story** | Main story progress is shared, so you can split up and finish different parts of the game. |

- **Full sync** turns everything on. **Co-op** turns everything off, so you only see each other and keep your own progress.
- Joining a room never throws away progress: if you're further ahead than the room, your progress is added to it.
- Never shared: health, magic, bomb and arrow counts, bottle contents and small keys.

**Planned:** shared small keys, shared weather, wind and time of day (off by default), a "no player collision" option (players bump into each other today), PVP and riding in each other's boats.

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
- **Your own Wind Waker disc image**: the US version, `GZLE01`, dumped from your own disc (ISO/GCM).
- **Friends to play with**, and a way to reach each other over the internet (see [Playing over the internet](#playing-over-the-internet)).

## Install

> Ready-made downloads are coming soon. Until then, see [Building from source](#building-from-source).

1. **Download WW-Online** from the [Releases](../../releases) page (`WWOnline-win-Setup.exe`) and run it. It installs for your user only (no admin needed), adds shortcuts, and updates itself when a new version comes out. Everything it needs, including .NET, is bundled.
   - Windows may say **"Windows protected your PC"** because the installer isn't code-signed yet. Click **More info → Run anyway**.
2. **Extract your game in Dolphin.** Add your ISO to Dolphin's game list, then right-click the game → **Properties** → **Filesystem** → right-click the disc at the top → **Extract Entire Disc…**, and pick an empty folder. You get a folder containing `sys/` and `files/`. (A later version will read your ISO directly.)
3. **Give Dolphin more memory.** WW-Online needs the 48 MB memory setting. In Dolphin, open **Config** → **Advanced**, tick **Enable Emulated Memory Size Override** and set **MEM1** to **48 MB**. (Only needed while playing WW-Online; untick it for other games if you like.)
4. **Patch your game.** Open WW-Online → **Settings**:
   - **Vanilla game folder**: the folder you extracted in step 2. It is only read, never changed.
   - **Patched game folder**: an empty folder where WW-Online writes the patched copy.
   - **Dolphin**: your `Dolphin.exe`.

   Then pick any **optional patches** under **Game patches** (skip the intro, instant text, Swift Sail, faster animations, crash fixes and more, all from Better Wind Waker) and press **Patch game**. This takes a few seconds and only needs doing again when WW-Online asks, after an update, or when you change your patches. Patches marked **All players should match** change the world, so agree on those with your room.

## Play

- **Host a room:** on the **Room** page choose **Host**. WW-Online starts the room on your PC (port `6969`) and launches the game. You're the room owner: press **Edit room** to pick Full sync, Co-op or your own mix of rules. Rules stay locked until you press Edit, so nothing changes by accident.
- **Join a room:** enter the host's address and your name, then **Connect**. You see the room's rules and items read-only, and anything you pick up still counts for the room.
- **Your look:** open **Appearance** to pick your clothes (game default, hero's tunic or pajamas) and your tunic colour. It changes live for everyone. **Show player names** there turns the names above the other Links on or off, for your screen only.
- **Story flags:** on the **Room** page, see which story events the room has reached. The room owner can edit them.
- **Dolphin** shows the connection to your game and your live stats.
- **Tools** holds local-only helpers (warp, stats, memory). They only affect your own game.

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

- **Purple screen with `d_s_play.cpp` / black screen on boot:** the 48 MB memory setting is off (install step 3), or the game folder isn't a patched `GZLE01` copy.
- **WW-Online says your patched game is out of date:** WW-Online was updated. Go to **Settings** and press **Patch Game** again.
- **Players don't appear:** everyone must be on the same WW-Online version, in the same room, and past the title screen.
- **Can't connect:** check the host address and port, and see [Playing over the internet](#playing-over-the-internet).

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
- `CLAUDE.md` and `GameMod/CLAUDE.md` describe the architecture, memory map and coding rules.

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
