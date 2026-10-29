# Third-party notices

GH3 Launcher's own code is public domain ([The Unlicense](LICENSE)). The installer
(`GH3 Launcher Setup.exe`) also brings the programs below, unchanged, and the launcher
contains a few libraries and fonts. Each keeps its own license; the full texts are in
[`licenses/`](licenses) (installed to `Launcher\Licenses\` in the game folder).

GH3 Launcher runs FFmpeg and Honeycomb's toolkit as separate programs/libraries loaded at
run time from the game folder; it is not a derivative work of them.

**Nothing of Guitar Hero III is included.** The launcher reads the game's art, sounds and
music from the player's own copy when it runs. Guitar Hero is a trademark of Activision
Publishing, Inc.; this project is not affiliated with or endorsed by Activision, Neversoft,
RedOctane or Aspyr.

---

## Bundled programs (installed by `GH3 Launcher Setup.exe`)

### FFmpeg — `Honeycomb-GUI\ffmpeg.exe`, `ffprobe.exe`, `ffplay.exe`
- Build: gyan.dev "full" build, version `2026-10-25-git-46f23be8b1` (static, `--enable-gpl --enable-version3`).
- License: **GNU General Public License v3.0 or later** — [`licenses/GPL-3.0.txt`](licenses/GPL-3.0.txt).
- Source code: <https://ffmpeg.org/download.html> (git commit `46f23be8b1`:
  <https://github.com/FFmpeg/FFmpeg/commit/46f23be8b1>); build scripts and the sources of the
  libraries it was built with: <https://www.gyan.dev/ffmpeg/builds/>.
- Copyright © 2000-2026 the FFmpeg developers.

### Honeycomb (Honeycomb-GUI and GH-Toolkit-Core) — `Honeycomb-GUI\`
- Author: AddyMills and contributors. Release used: Honeycomb-GUI 1.0.0 (2025-10-10).
- License: **GNU General Public License v3.0** — [`licenses/GPL-3.0.txt`](licenses/GPL-3.0.txt).
- Source code: <https://github.com/AddyMills/Honeycomb-GUI>.
- Its folder also contains libraries under their own licenses (MIT unless noted):
  Melanchall.DryWetMidi, FFMpegCore, Instances, NAudio (NAudio.Core, NAudio.Asio, NAudio.Midi,
  NAudio.Wasapi, NAudio.WinForms, NAudio.WinMM), NAudio.Lame, LAME (`libmp3lame.*.dll`, LGPL),
  YamlDotNet, Newtonsoft.Json, ini-parser (INIFileParser), Ude.NetStandard (MPL 1.1 / GPL 2 / LGPL 2.1),
  SharpZipLib, System.Text.Json, System.Text.Encodings.Web, System.IO.Pipelines.
- Left out of the bundle: the bundler's own data (`QBDebug\keys_user*.txt`, `Backups\`) and debug symbols.

### x360ce (Xbox 360 Controller Emulator) — `x360ce_x86.exe`, `xinput1_3.dll`
- Author: TocaEdit (Evaldas Jocys) and contributors. `x360ce_x86.exe` 3.2.10.82 with its
  `xinput1_3.dll` 3.4.1.1357.
- License: **GNU Lesser General Public License v3.0** (since x360ce 3.1.7.58) —
  [`licenses/LGPL-3.0.txt`](licenses/LGPL-3.0.txt) together with
  [`licenses/GPL-3.0.txt`](licenses/GPL-3.0.txt).
- Source code: <https://github.com/x360ce/x360ce> · Downloads: <https://www.x360ce.com/>.
- The `x360ce.ini` that comes with it is a blank configuration written for this installer
  (no devices); an existing `x360ce.ini` in the game folder is never replaced. The launcher
  only reads `x360ce.ini`, except to remove a device when the player asks it to.

## Inside GH3 Launcher.exe

### .NET runtime
- The launcher and the installer are self-contained .NET 10 programs.
- License: MIT. Copyright © .NET Foundation and Contributors. <https://github.com/dotnet/runtime>

### Microsoft.Web.WebView2 (SDK)
- License: BSD 3-Clause. Copyright © Microsoft Corporation. All rights reserved.

> Redistribution and use in source and binary forms, with or without modification, are
> permitted provided that the following conditions are met: (1) Redistributions of source
> code must retain the above copyright notice, this list of conditions and the following
> disclaimer. (2) Redistributions in binary form must reproduce the above copyright notice,
> this list of conditions and the following disclaimer in the documentation and/or other
> materials provided with the distribution. (3) Neither the name of Microsoft Corporation nor
> the names of its contributors may be used to endorse or promote products derived from this
> software without specific prior written permission. THIS SOFTWARE IS PROVIDED BY THE
> COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
> BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
> PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
> ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
> BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
> PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
> CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
> OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The WebView2 **Runtime** itself is not bundled; it comes with Windows 11 (or from Microsoft).

### SharpCompress
- License: MIT. Copyright © Adam Hathcock. <https://github.com/adamhathcock/sharpcompress>

> Permission is hereby granted, free of charge, to any person obtaining a copy of this
> software and associated documentation files (the "Software"), to deal in the Software
> without restriction, including without limitation the rights to use, copy, modify, merge,
> publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
> to whom the Software is furnished to do so, subject to the following conditions: The above
> copyright notice and this permission notice shall be included in all copies or substantial
> portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
> EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
> FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
> HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
> CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE
> USE OR OTHER DEALINGS IN THE SOFTWARE.

### Fonts (`web/assets/fonts`)
- **Pirata One** — Copyright © 2012 Rodrigo Fuenzalida, Nicolas Massi. SIL Open Font License 1.1 — [`licenses/OFL-PirataOne.txt`](licenses/OFL-PirataOne.txt)
- **Barlow Condensed**, **Barlow Semi Condensed** — Copyright 2017 The Barlow Project Authors. SIL Open Font License 1.1 — [`licenses/OFL-BarlowCondensed.txt`](licenses/OFL-BarlowCondensed.txt), [`licenses/OFL-BarlowSemiCondensed.txt`](licenses/OFL-BarlowSemiCondensed.txt)
- **Special Elite** — Copyright Astigmatic. Apache License 2.0 — [`licenses/Apache-2.0-SpecialElite.txt`](licenses/Apache-2.0-SpecialElite.txt)

## Downloaded only when the player asks (not included)

- **ReShade** (Enhanced graphics) — <https://reshade.me>, by crosire, BSD 3-Clause. The launcher
  downloads the official setup and reads `ReShade32.dll` out of it, and the official shaders from
  <https://github.com/crosire/reshade-shaders> (branches `slim` and `legacy`; each effect keeps the
  license in its file).
- **Themes** — the FastGH3 community's themes listed at <https://donnaken15.com/fastgh3/zones.html>,
  downloaded from their authors' links. They belong to their authors; the launcher only copies their
  textures into the player's own game files.

## Online services
- **MusicBrainz** (<https://musicbrainz.org>) is asked, when a song is imported, whether its
  artist's lead singer is a man or a woman (only the artist and title are sent). Its data is
  CC0 / CC BY-NC-SA; see <https://musicbrainz.org/doc/About/Data_License>.

---

*The license summaries above are for convenience; the license texts are what count. If you
redistribute these programs, check the current terms of each one.*
