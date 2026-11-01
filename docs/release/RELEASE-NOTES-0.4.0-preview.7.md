# Backpack 0.4.0-preview.7

Prerelease. Windows 10/11 x64, portable, self-contained. **Not digitally signed** — Windows SmartScreen may warn; check
the SHA-256 in `SHA256SUMS.txt`.

## Important: settings start fresh

The internal names of the projects and of all settings formats were changed to Backpack-only names. Settings, profiles
and styles saved by **0.4.0-preview.6 and earlier are not read** by this version: on the first start Backpack uses the
new default look and keeps the old files as backups next to the new ones (`settings.unreadable.bak.json`,
`app.unreadable.bak.json`; old files in `profiles\` and `styles\` stay untouched). Installed plugins, their trust
decisions and their packages are kept. Settings saved by this version are kept by later updates.

## New

- **Start with Windows.** “App settings” → “Start with Windows” starts Backpack in the notification area when you sign
  in, without opening the main window. No administrator rights are needed, and it works for the portable version: the
  entry (one value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) points to where `Backpack.exe` is now. The
  switch shows the real state of Windows: an entry of another copy, an entry turned off in the Windows startup apps list,
  or an entry pointing to a file that no longer exists are shown under it. When the program folder was moved or a new
  version was unpacked elsewhere and the old file is gone, the entry is moved to the running copy automatically.
- **One instance.** Starting Backpack again while it runs opens the window of the running copy instead of starting a
  second one; a second start from autostart simply exits. Copies with their own data folder (`--settings`, portable
  folders) still run side by side.
- **Default look: Backpack Neon Emerald.** A new installation, “Reset section” and “Reset all” use Backpack Emerald
  colours with the Backpack Neon surface style, “Light or dark” = System, the Mica material with 25 % more transparency
  than before (0.35 → 0.4375, shown as 44 %), the built-in Spectral font for headings and keycap labels, the Backpack Neon
  Emerald keycap and mouse styles and an emerald click circle at the pointer. The Windows mouse pointer itself is not
  changed. Choices you save are never overwritten by an update.
- New README illustrations rendered from the real application windows in the default look.

## Fixed

- **The Lens plugin was slow while being resized.** Every mouse move during a resize ran synchronously on the UI thread:
  the transparent (layered) frame and source-outline windows were resized twice — once in pixels and again through
  `Width`/`Height` with a different rounding — and each resize of such a window makes WPF re-render and copy the whole
  window; the unchanged frame style was re-applied every time (new shadow effects, brushes and window region); the
  Magnification window was resized and repainted synchronously, and the magnified image was refreshed twice per frame;
  and mouse moves arriving faster than a frame queued up. Now pointer moves are coalesced to one update per rendered frame
  (the last position is always applied), the transparent windows and the magnifier grow in steps with spare room while
  you drag (the visible shape is exact, the magnifier returns to its exact size when you release), and nothing is
  recreated when the style did not change. Image quality and refresh rate are unchanged. Measured on a 2560×1600 monitor
  at 150 %: one resize step 55 ms → about 22 ms on average (median 54 → 20 ms), allocations 35.6 → 16 KB per step;
  after a 1000 Hz burst of mouse moves the lens catches up in about 80 ms instead of 360 ms.
- The demonstration on Home draws its arrow and numbered mark in the default drawing colour (emerald) instead of red.

## Compatibility

- Settings format `backpack-settings` 9, app preferences `backpack-app` 5, profiles `backpack-profile`, styles
  `backpack-style`, themes `backpack-theme`; built-in style IDs start with `backpack.`. Files of the earlier formats are
  not read (see above). Plugin packages, `plugins.json` and SDK 1.2 are unchanged.
- New start option `--autostart` (used by the autostart entry; like `--minimized`).
- The migration of data from folders of very old versions was removed.

## Known limitations

- The package is unsigned. Releases are built by GitHub Actions from the tag; interactive checks (UI self-test, lens,
  autostart) were run locally on a Windows desktop.
- Settings of 0.4.0-preview.6 and earlier are not carried over (see above).
- The help is not translated into the 16 newer interface languages (English text with a notice).
- The display stall reported for preview.5 has not been reproduced: if keys stop showing, run
  `Backpack.exe --display-trace=%USERPROFILE%\Desktop\backpack-display.tsv` and send the file.
- Plugins run in their own process but are not sandboxed: turn on only plugins you trust.
