# libtww proof of concept: the game inside an Avalonia window (macOS, Apple Silicon)

Design: tww-native-notes `WW_ONLINE_EMBED.md`, sections 4.1-4.4. API: `native/include/pc/tww.h`.

Build (from the repository root; `COS_DISC` is your own GZLE01 disc image):

```sh
cmake -S native -B build/poc-mac -G Ninja      # configured as in native/README.md
ninja -C build/poc-mac tww                     # build/poc-mac/libtww.dylib
dotnet build poc/AvaloniaHost -o build/poc-avalonia
```

Run (a window with a sidebar and the game; click the game for the keyboard):

```sh
build/poc-avalonia/AvaloniaHost --disc "$COS_DISC"
```

Options: `--lib <libtww.dylib>` (default `build/poc-mac/libtww.dylib`), `--user <dir>` (saves, settings,
caches; default `build/poc-mac/avalonia-user`).

Headless checks (no window on screen, no Dock icon, the focus is never taken):

```sh
build/poc-avalonia/AvaloniaHost --disc "$COS_DISC" --test build/poc-avalonia-test --frames 1500
#   title screen at frame 1500 (surface + frame PNGs), a window resize, Start on the keyboard pad; exit 0 = PASS
clang++ -std=c++20 -fobjc-arc -framework AppKit -I native/include poc/tww_smoke/tww_smoke.mm -o build/poc-mac/tww_smoke
build/poc-mac/tww_smoke build/poc-mac/libtww.dylib "$COS_DISC" <out dir> 1600 900,1500   # no Avalonia, no window
```
