# Terminal fonts

The following fonts are bundled with RtlTerminal and always appear first in
**Edit → Settings → Standard fonts**. They work without a system-wide font
installation, including in the portable build. **All fonts** also lists other
installed Windows families. Amiri, Amiri Typewriter and Simplified Arabic Fixed
are excluded from the app's picker; no Windows fonts are uninstalled.

| Selection | Upstream release | Included styles |
| --- | --- | --- |
| JetBrains Mono | [2.304](https://github.com/JetBrains/JetBrainsMono/releases/tag/v2.304) | Regular, Bold, Italic, Bold Italic |
| Cascadia Mono | [2407.24](https://github.com/microsoft/cascadia-code/releases/tag/v2407.24) | Regular, Bold, Italic, Bold Italic |
| Fira Code | [6.2](https://github.com/tonsky/FiraCode/releases/tag/6.2) | Regular, Bold |
| Iosevka | [34.9.1](https://github.com/be5invis/Iosevka/releases/tag/v34.9.1) | Regular, Bold, Italic, Bold Italic |
| Hack | [3.003](https://github.com/source-foundry/Hack/releases/tag/v3.003) | Regular, Bold, Italic, Bold Italic |
| Geist Mono | [1.8.2](https://github.com/vercel/geist-font/releases/tag/v1.8.2) | Regular, Bold, Italic, Bold Italic |
| Vazir Code | [1.1.2](https://github.com/rastikerdar/vazir-code-font/releases/tag/v1.1.2) | Regular |
| Cascadia Mono NF | [2407.24](https://github.com/microsoft/cascadia-code/releases/tag/v2407.24) | Regular, Bold, Italic, Bold Italic |
| JetBrains Mono NF | [Nerd Fonts 3.5.1](https://github.com/ryanoasis/nerd-fonts/releases/tag/v3.5.1) | Regular, Bold, Italic, Bold Italic |

Missing bold/italic faces are simulated by the text renderer. Vazir Code contains
Persian glyphs; fonts without a needed script use the existing Windows fallback.
NF variants include prompt and developer icons. The JetBrains NF selection maps
to the original internal family name `JetBrainsMono NFM`; font files are not renamed
internally or modified.

The default terminal family is bundled Cascadia Mono. Saved selections of removed
families resolve to that default. Zoom and settings retain the readable family
name. Browser previews embed the selected font faces as local data, so a browser
does not need those fonts installed and makes no font network requests.

Fonts are embedded in the executable. Keep the `Assets/Fonts` notices with the
portable build; the installer includes them automatically. Original license texts,
upstream release URLs and per-file SHA-256 checksums are under
[`src/RtlTerminal/Assets/Fonts`](../src/RtlTerminal/Assets/Fonts).
Fonts retain their upstream licenses, separately from the application GPLv3.
