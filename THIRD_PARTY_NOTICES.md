# Third-party notices

WW-Online vendors the source of the Seaborne game engine (first section, with its own licence files), and includes code and data adapted from the other projects below. Each of those is used under the MIT License, reproduced here as it requires.

## Seaborne (the native game engine, `seaborne/`)

Seaborne is vendored in `seaborne/` as source (`seaborne/VENDORED.md` names the commit). It is **based on [SwitchWaker](https://github.com/centollOS/SwitchWaker) by Pulpparty and depende3000** and the SwitchWaker contributors, and its `game/` folder is the [zeldaret/tww](https://github.com/zeldaret/tww) decompilation (CC0-1.0). Its own licence files govern it: [`seaborne/LICENSE`](seaborne/LICENSE) and, for every component it contains, builds against or fetches at build time, [`seaborne/THIRD_PARTY.md`](seaborne/THIRD_PARTY.md). In short:

- the port's own code: MIT, under the notice below;
- `seaborne/game/`: CC0-1.0 (`seaborne/game/LICENSE`);
- `seaborne/native/dsp_hle/` (audio): **GPL-3.0-or-later** (`seaborne/native/dsp_hle/LICENSE`); the Dolphin DSP HLE sources the build fetches from [RecompCore](https://github.com/elliotttate/RecompCore) are GPL-2.0-or-later;
- [Aurora](https://github.com/encounter/aurora), fetched at build time with its dependencies: MIT and the licences `seaborne/THIRD_PARTY.md` lists.

A `libtww` built from `seaborne/` is GPL-3.0-or-later as a whole. WW-Online distributes Seaborne as source only and never ships a built `libtww`.

```
MIT License

Copyright (c) 2026 Pulpparty and depende3000, and the SwitchWaker contributors
Copyright (c) 2026 datstarkey (Seaborne)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## WW_Hacking_API

https://github.com/LagoLunatic/WW_Hacking_API

Used for: the vanilla game headers and linker symbols in `GameMod/include/` (`ww_functions.h`, `ww_structs.h`, `ww_variables.h`, `ww_linker.ld`) and the C-to-REL / ASM patch approach.

```
The MIT License (MIT)

Copyright (c) 2020

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Wind Waker Randomizer (wwrando)

https://github.com/LagoLunatic/wwrando

Used for: the RARC, REL and Yaz0 code in `WWOnline.Patcher/BinaryFormats/` (ported from Python), and game patches such as `GameMod/src/patches/optional/skip_intro.asm`.

```
The MIT License (MIT)

Copyright (c) 2018 LagoLunatic

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Better Wind Waker (betterww)

https://github.com/WideBoner/betterww

Used for: the optional game patches in `GameMod/src/patches/optional/` (quality-of-life tweaks and vanilla bug fixes, ported from its `asm/patches`, `custom_funcs.asm` and `tweaks.py`), the BMG reader in `WWOnline.Patcher/BinaryFormats/Bmg/` (from `wwlib/bmg.py`) and `GameMod/assets/blank.thp`. betterww is based on wwrando and is distributed under the same MIT License:

```
The MIT License (MIT)

Copyright (c) 2018 LagoLunatic

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Fonts

The client bundles three fonts, each under the SIL Open Font License 1.1. The full licence texts are next to the font files and ship with every build in `licenses/`:

- **Fredoka**: Copyright 2016 The Fredoka Project Authors. [`Fredoka-OFL.txt`](WWOnline.Client/Assets/Fonts/Fredoka-OFL.txt)
- **Hanken Grotesk**: Copyright 2021 The Hanken Grotesk Project Authors. [`HankenGrotesk-OFL.txt`](WWOnline.Client/Assets/Fonts/HankenGrotesk-OFL.txt)
- **JetBrains Mono**: Copyright 2020 The JetBrains Mono Project Authors. [`JetBrainsMono-OFL.txt`](WWOnline.Client/Assets/Fonts/JetBrainsMono-OFL.txt)
