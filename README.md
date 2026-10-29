# GH3 Launcher

**English** · [Español](README.es.md)

A launcher for **Guitar Hero III: Legends of Rock (PC)** that puts the usual chores in one
window: importing Clone Hero songs, sorting the song lists, seeing your controllers the way the
game gets them, settings, cheats and backups — so you only have to install the game and start
playing.

> **Heads-up:** this was made to make things simpler, not as a polished product. It was tested
> on one setup and **may contain errors**. Back up your game folder before trying it, and if
> something breaks, the launcher's own backups (Tools) are there to undo song changes.

> Not affiliated with or endorsed by Activision, Neversoft, RedOctane or Aspyr. Guitar Hero is a
> trademark of Activision Publishing, Inc. **Nothing of the game is included**: the launcher reads
> the art, sounds and music from your own copy when it runs.

## Features

- **Home** — Play button, a check of everything the game and the launcher need (with a fix
  button when there is one), what each player plays with, the setlist at a glance and your
  **total play time** ("2 days and 35 minutes", "1 year, 3 months and 2 days"…: the three
  largest units).
- **Songs** — the Setlist, Bonus and Download lists with album art and audio previews. The
  Download list can be edited: divisions (tiers) you can rename and move up or down (the game shows
  them in that order), drag to reorder, move songs between divisions, delete songs, and choose the
  **singer** shown on stage for each song (the usual one, a woman, Bret Michaels, or **nobody**, as in
  Cliffs of Dover). Songs imported in the current session are marked *IMPORTED*.
- **Import** — drop Clone Hero songs (`.sng`, `.zip`, `.7z`, `.rar`, folders with `song.ini` +
  `notes.chart`/`notes.mid`, or whole packs). Check and hear them, fix title/artist/year, pick the
  count-off and the division, then install: the chart and audio are compiled with Honeycomb's
  toolkit (FFmpeg for the audio), with lip sync and the singer worked out from the artist (asks
  MusicBrainz); a song with no vocals gets no singer on stage. "Replace" rebuilds songs you
  imported before.
- **Controls** — see every controller x360ce has, live, with what GH3 gets from each button.
  For each one choose **how it's played**: *Guitar* (GH3 gets a guitar: you strum) or *Controller*
  (GH3 gets a plain Xbox controller, which it plays without strumming). **Keyboard 2**: a virtual
  guitar or controller on the keyboard for a second player (GH3's own keyboard stays player 1's).
  Whammy improvements for guitars (fix for Wii guitars' "stuck" whammy, sensitivity, turbo). Remove
  old devices from x360ce's list.
- **Themes** — the look of the notes, the highway and the HUD, from the FastGH3 community's themes
  (download them in the launcher or import a .zip/.gfx/.tex), with a live preview that plays the
  song in the background and draws its notes with the theme. Only the textures a theme has are
  changed; "Guitar Hero III (original)" puts your game's own back.
- **Enhanced graphics** — ReShade's effects (ambient light, bloom, color, contrast, sharpness,
  motion blur) plus GH3's quality settings, in presets (Remastered, Sharp, Cinematic, Vivid) or one by
  one; turn it on or off whenever you like. ReShade is downloaded from reshade.me the first time.
- **Cheats** — enter cheat codes and **automatic unlocks** (songs, store items, cheats, money)
  that GH3 applies when it loads your save; handy because the game asks for a new save after the
  song lists change.
- **Options** — GH3's video and audio settings, GH3+ settings and plugins, the launcher's
  language (**English** / Español), what the launcher does when you press Play, background menu
  music (GH3's menu theme and your songs' previews, with pause and next buttons) and GH3's menu
  sounds.
- **Tools** — Honeycomb GUI and x360ce, setlist and save **backups** (made automatically before
  every change, restorable in one click), re-read the game's art, logs, and uninstall.

## Requirements

- Windows 10 or 11, 64-bit.
- Your own **Guitar Hero III: Legends of Rock for PC**, installed. **BetterGH3** (or any copy with
  **GH3+**, its plugin loader) is recommended: the InputPlus plugin (auto-strum, keyboard 2,
  whammy) needs GH3+. Without it the launcher still imports songs and runs the game.
- **Microsoft Edge WebView2 Runtime** — already in Windows 11 and in up-to-date Windows 10. If it
  is missing, the launcher says so and links to it.
- Nothing else: the launcher and the installer bring their own .NET. Only Honeycomb's own window
  (Tools › Honeycomb) needs the .NET 8 Desktop Runtime; the launcher doesn't.

## Install

1. Download **`GH3 Launcher Setup.exe`** and run it.
2. Choose your Guitar Hero III folder (it is found by itself when it can be) and the parts:
   - **GH3 Launcher** — `GH3 Launcher.exe` in the game folder.
   - **Honeycomb + FFmpeg** — `Honeycomb-GUI\` with `ffmpeg.exe`, `ffprobe.exe` and `ffplay.exe`
     (FFmpeg build of 25 Oct 2026). Needed to import songs.
   - **x360ce** — `x360ce_x86.exe`, `xinput1_3.dll` and a blank `x360ce.ini` (yours is kept if
     there is one). Needed for guitars and controllers.
   - Desktop and Start menu shortcuts.
3. Install. If the folder needs administrator rights (e.g. under `Program Files`), Windows asks.

Files the installer replaces are kept in `Launcher\backups\setup\` and put back on uninstall.
The installer also accepts `--target <folder> --components launcher,honeycomb,x360ce,desktop,startmenu --lang en|es --silent`.

On the **first start** the launcher reads the art, the loading movie and the menu sounds from
your copy of the game (about a minute), then checks the folder. After that: import songs and play.

## Controllers (x360ce)

GH3 talks to Xbox controllers only; **x360ce** (`xinput1_3.dll` next to `GH3.exe`) makes any
guitar or gamepad look like one. Its app, `x360ce_x86.exe`, is **where controllers are set up**:
which device is PAD 1–4 and what each button does. The launcher doesn't change that mapping.

To set up a guitar or controller:

1. Controls › **Open x360ce** (or Tools › x360ce). If it offers to create `xinput1_3.dll` or to
   search settings online, you can say yes to the first and no to the second.
2. Add your device to a PAD, map its buttons (for a guitar pick a guitar device type: the Wii
   Harmonix guitar works as *Guitar Alternate*), and **Save**.
3. Back in the launcher, Controls shows it with its PAD and lights up each fret as you press it.
4. Choose **how it's played**: *Guitar* (you strum) or *Controller* (an Xbox controller: no
   strumming, frets on LT, LB, RB, RT and A). The launcher switches x360ce's device type and moves
   the fret buttons so each keeps its color (close x360ce and the game first). A real Xbox
   controller that x360ce passes through is always a controller. This is also on Home: click a
   player's card to switch.

Notes:

- **Keyboard 1** is GH3's own keyboard; set its keys in the game: Options › Controller Settings ›
  Configure Keyboard (the game lets you change the 5 frets, Star Power, Whammy and strum up/down;
  pause and menu left/right are fixed). **Keyboard 2** is emulated by the InputPlus plugin on a PAD
  x360ce leaves free, as a guitar or as an Xbox controller. Give it keys keyboard 1 doesn't use: the
  game stops seeing keyboard 2's keys so none does two things. Two physical keyboards work the
  same way (Windows joins them into one).
- GH3 decides who is player 1 by who presses a button first; x360ce beeps when the game starts.
- If **`xinput1_3.dll` is missing** or isn't x360ce's, Home shows it: open x360ce and let it create
  the file. Without it no guitar or gamepad reaches the game the way x360ce maps it.
- "Remove from x360ce" deletes a device's section from `x360ce.ini` (a copy goes to
  `Launcher\backups\` first). Close x360ce and the game before removing.

## Where things are

Everything the launcher keeps is in the game folder:

| Path | What |
| --- | --- |
| `GH3 Launcher.exe` | the launcher |
| `Launcher\config.json` | its settings, play time, controller styles |
| `Launcher\backups\` | setlist and save backups, files replaced by the installer |
| `Launcher\game\` | art and sounds read from your copy of the game |
| `Launcher\themes\` | the themes you downloaded or imported (`backups\theme\` keeps your original global zone) |
| `Launcher\reshade\` | ReShade and its effects, when Enhanced graphics is on (`d3d9.dll` + `ReShade.ini` next to `GH3.exe`) |
| `Launcher\launcher.log`, `songtool.log` | logs |
| `Launcher\Licenses\` | licenses of everything included |
| `PLUGINS\InputPlus.dll`, `InputPlus.ini` | the launcher's GH3+ plugin |

Nothing is sent anywhere, except the artist and title of songs you import, which are looked up
on MusicBrainz to tell whether the singer is a man or a woman. The launcher downloads only when
you ask: a theme (from its author's Google Drive) or ReShade (reshade.me and its shaders on GitHub).

## Uninstall

Windows Settings › Apps › **GH3 Launcher** › Uninstall, or Tools › Uninstall. It removes what the
installer added and puts back what it replaced; you choose whether to delete the launcher's data.
Guitar Hero III, your saves and the songs you installed stay.

## Troubleshooting

- **The guitar doesn't respond, or every fret strums by itself** — Controls: check that it has a
  PAD and that *How it's played* is *Guitar*. Check Home for an `xinput1_3.dll` warning.
- **GH3 says the save is damaged / asks for a new save** after changing songs — expected: GH3 ties
  the save to its song lists. Cheats › automatic unlocks gets everything back in seconds, and
  Tools has the backups.
- **A song fails to import** — the reason is next to it in the list; `Launcher\songtool.log` has
  the details. If FFmpeg is missing, Home says so.
- **GH3 closes on start** — very long folder paths can do it: move the game to e.g. `C:\Games\GH3`.

## Building from source

Needs the **.NET 10 SDK** and a **Honeycomb-GUI** folder to compile against (the game's, or
`-Honeycomb <folder>`). Visual Studio's C++ tools are optional (they rebuild the InputPlus plugin;
otherwise `plugin\InputPlus.dll` is used as it is).

```powershell
powershell -NoProfile -File build.ps1 -Game "C:\Games\Guitar Hero III"            # dist\GH3 Launcher.exe (also copied to the game)
powershell -NoProfile -File build.ps1 -Package -Game "C:\Games\Guitar Hero III"   # dist\GH3 Launcher Setup.exe
```

`-Package` bundles Honeycomb-GUI, FFmpeg and x360ce from that game folder, leaving out the
bundler's own data (`QBDebug\keys_user*.txt`, `Backups\`).

| Folder | What |
| --- | --- |
| `host\` | the launcher (WinForms + WebView2): game, songs, import, controllers, settings |
| `songtool\` | the song compiler, built into the launcher (`GH3 Launcher.exe songtool …`) |
| `web\` | the interface (HTML/CSS/JS, embedded in the exe) |
| `plugin\` | InputPlus, the GH3+ plugin (C) |
| `installer\` | `GH3 Launcher Setup.exe` |

## License

GH3 Launcher's code and its programs (`GH3 Launcher.exe`, `GH3 Launcher Setup.exe`, the InputPlus
plugin) are released into the **public domain** under [The Unlicense](LICENSE): use them, copy
them, change them, share them or sell them, as source or as executables, for any purpose, without
asking and without credit.

The installer also brings third-party programs that keep their own licenses: **FFmpeg** (GPL-3.0),
**Honeycomb** (GPL-3.0) and **x360ce** (LGPL-3.0); the launcher contains WebView2's SDK (BSD-3),
SharpCompress (MIT) and fonts (OFL / Apache 2.0). See
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and [`licenses/`](licenses). If you redistribute
the installer, those licenses ask you to point to their source code too (the notices have the links).

## Thanks

AddyMills for **Honeycomb** / GH Toolkit, the **x360ce** project, the **FFmpeg** developers, and the
GH3+ / BetterGH3 and Clone Hero communities.
