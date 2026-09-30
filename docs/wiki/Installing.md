# Installing

## What you need

- **Windows 10 or 11**, 64-bit.
- **[Dolphin](https://dolphin-emu.org/download/)**, a recent development build.
- **Your own Wind Waker disc image**: the US version, `GZLE01`, dumped from your own disc (ISO, RVZ, GCM...). The European (PAL) and Japanese versions don't work.
- **Friends to play with**, and a way to reach each other over the internet (see [Playing together](Playing-Together.md#playing-over-the-internet)).

## 1. Install WW-Online

Download `WWOnline-win-Setup.exe` from the [latest release](https://github.com/datstarkey/ww-online/releases/latest) and run it. It installs for your user only (no admin needed), adds shortcuts, and updates itself when a new version comes out. Everything it needs, including .NET, is bundled.

Windows may say **"Windows protected your PC"** because the installer isn't code-signed yet. Click **More info**, then **Run anyway**.

## 2. Get Dolphin

Download a recent development build of [Dolphin](https://dolphin-emu.org/download/) and unzip it anywhere. WW-Online starts it for you, with the settings it needs.

## 3. Run the setup

The first time you open WW-Online, a short setup walks you through everything. You can run it again any time from **Settings**, **Run setup again**.

- **Dolphin**: WW-Online looks for `Dolphin.exe` for you, or you browse to it.
- **Your game**: choose your disc image and WW-Online extracts it with DolphinTool, which comes with Dolphin. It checks it's the US version, `GZLE01`. Then choose a folder for the patched copy: your original is only read, never changed.
  - With an older Dolphin that has no DolphinTool, extract the disc yourself: add it to Dolphin's game list, right-click the game, **Properties**, **Filesystem**, right-click the disc at the top, **Extract Entire Disc...**, then choose that folder.
- **Game patches**: optional extras from Better Wind Waker. The defaults are a good start. Patches marked **All players should match** change the world, so agree on those with your room. See [Game patches](Game-Patches.md).
- **You**: your name and tunic colour.
- **Patch**: builds your patched game. The first time copies the whole game (about 1.5 GB), so it can take a minute.

## Patching again

Patch again only when WW-Online asks: after an update that changes the game code, or when you change your game patches. Until your game is patched and up to date, a banner at the top says so (with **Patch now**), and WW-Online won't start Dolphin or link up with it.

You can also patch from **Settings**, **Patch game**, which is where the game folders and patches live.

## Dolphin's memory setting

WW-Online needs Dolphin's 48 MB memory setting. It turns it on by itself (for that session only) whenever it starts Dolphin for you.

If you start Dolphin yourself, turn it on there: **Config**, **Advanced**, tick **Enable Emulated Memory Size Override** and set **MEM1** to **48 MB**. Without it the game shows a purple screen or a black screen on boot.

## Updates

WW-Online checks for a new version shortly after it starts and every so often after that (or right away from **Settings**, **Check for updates**), and downloads it in the background. When it's ready, **Restart to update** installs it; otherwise it installs the next time you start WW-Online. Everyone in a room needs the same version, so update together. If the update changed the game code, WW-Online asks you to patch again.

## Where things are

| What | Where |
|---|---|
| Settings | `%AppData%\WWOnline` |
| Logs | `%LocalAppData%\WWOnline\logs` |
| Item icons (read from your own game) | `%LocalAppData%\WWOnline\GameIcons` |
| Your patched game | the folder you chose in the setup (**Settings**, **Patched game folder**) |

To uninstall, remove WW-Online from Windows' **Settings**, **Apps**. Your patched game folder stays where it is: delete it yourself if you no longer need it.

Next: [Playing together](Playing-Together.md).
