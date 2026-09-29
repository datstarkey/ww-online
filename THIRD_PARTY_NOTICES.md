# Third-party notices

WW-Online includes code and data adapted from the projects below. Each is used under the MIT License, reproduced here as it requires.

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
