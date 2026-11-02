# Terminal fonts

The following fonts are listed first in
**Edit → Settings → Standard fonts**. They work without a system-wide font
installation, including in the portable build. Six families ship with the app;
**Iosevka, Cascadia Mono NF and JetBrains Mono NF** are optional downloads.
Their rows are dimmed with a **Download** button until installed. Only the four
required TTF files are fetched using HTTP byte ranges from the official ZIP;
the full 134–177 MB archives and their unused weights are not downloaded.
The reader fetches the ZIP directory and bounded 512 KiB blocks containing the
selected entries, with a small amount of surrounding data. Actual transfer size
depends on upstream ZIP compression. Servers/proxies that ignore Range, return
incorrect Content-Range or change the asset are refused; there is no silent full
archive fallback. Strong ETags are checked where supplied, and font hashes always
remain mandatory. The in-memory block cache is limited to 2 MiB.
Verification progress and Cancel appear in the same row. Downloaded fonts become
selectable immediately, without restarting, and Apply activates the selection.
**All fonts** also lists other
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

The six included families are embedded in the executable. Optional fonts are
stored privately under `%LOCALAPPDATA%\RtlTerminal\Fonts\<id>\<version>` and survive
application updates. No administrator approval or Windows font installation is
needed. Release versions and SHA-256 hashes are pinned in `sources.json`.
Downloads use HTTPS from the upstream GitHub releases, honor the runtime's normal
proxy configuration, run off the UI thread and are cancelled when Settings closes.
Only allowlisted font filenames are extracted to a staging directory; path names
from ZIP entries are never used as output paths. Each font is hash-verified before
the complete family is atomically committed. Cancellation, HTTP failure, disk and
permission errors leave the family uninstalled and allow a retry. A corrupt
existing installation is conservatively refused rather than deleting user files.
An unavailable saved optional font falls back to Cascadia Mono. Downloaded fonts
also embed into HTML previews so the browser needs no network access.

Keep the `Assets/Fonts` notices with the
portable build; the installer includes them automatically. Original license texts,
upstream release URLs and per-file SHA-256 checksums are under
[`src/RtlTerminal/Assets/Fonts`](../src/RtlTerminal/Assets/Fonts).
Fonts retain their upstream licenses, separately from the application GPLv3.
