# Backpack 0.4.0-preview.8

Prerelease. Windows 10/11 x64, self-contained (the .NET runtime is included). New: a **Windows installer** in addition to
the portable ZIP. **Not digitally signed** — Windows SmartScreen may warn; check the SHA-256 checksums.

## Installation

**Installer (recommended)**

1. Download `Backpack-0.4.0-preview.8-win-x64-setup.exe` from this release.
2. Run it. If SmartScreen says “Windows protected your PC”, choose “More info” → “Run anyway” (the file is not signed;
   compare its SHA-256 with `Backpack-0.4.0-preview.8-win-x64-setup.exe.sha256` first if you like).
3. No administrator rights are needed: Backpack installs for your user into `%LOCALAPPDATA%\Programs\Backpack` (the setup
   dialog also offers an installation for all users). It adds a Start menu entry and, if you tick it, a desktop shortcut.
4. To update later, run the installer of the newer version: settings in `%APPDATA%\Backpack` are kept. Uninstall from
   Windows Settings → Apps; your settings are kept there too (delete `%APPDATA%\Backpack` yourself to remove them).

**Portable ZIP**: unpack `Backpack-0.4.0-preview.8-win-x64.zip` anywhere and run `Backpack.exe` (see `README.txt`).

After the first start, turn on the plugins you need on the **Plugins** page (Focus, Lens, Snapshot Overlay and the other
plugins from the creator come with Backpack, turned off).

## New

- **Snapshot Overlay** (plugin from the creator, SDK 1.3 `overlay.snapshot`): select an area of the screen and keep a still
  picture of it in a floating window on top of your other windows — for example a technical drawing next to a CAD model
  on a single monitor. Move and resize it; double-click it, press Space while pointing at it or press Ctrl+Alt+Shift+E to
  expand it to about 90–95 % of the screen, and the same way, Esc, a click outside or switching windows to return it to
  exactly its previous place and size. The picture stays only in memory: it is never saved to disk or passed to the plugin.
  Capture: Ctrl+Alt+Shift+S. Details: `Samples\README.md` and the built-in help (Magnifier, lens, focus and timer).
- **One default border colour** for Focus, Lens and Snapshot Overlay: Backpack emerald `#31C99A` (it was light grey
  `#D4D4D4` for Focus and Lens). A colour you chose yourself is kept. The old grey that earlier versions saved together with
  all other fields of the plugin page (even if you never touched it) counts as the old default and becomes emerald; your
  other plugin settings are not changed. If you want the grey back, pick `#D4D4D4` on the plugin page — it is then kept.
- **Windows installer** built by GitHub Actions from the same verified package as the ZIP.

## Fixed

- **Dynamic Privacy kept querying the application you work in (keyboard problems reported in SOLIDWORKS).**
  Backpack's keyboard and mouse hooks only observe input and always pass it on — they never block keys. What Dynamic
  Privacy does in other applications is ask Windows UI Automation whether the focused element is a password field. When the
  element does not report this property, the answer is “no data”. A CAD 3D viewport is such an element; a numeric input
  field is not (it answers “not a password”). On “no data”, Backpack re-queried the application **every 1.5 seconds for as
  long as the focus stayed there**. Every query is a call into that application's user-interface thread. In addition,
  each repeated focus notification for the same element started a new check. So while you worked in the 3D viewport,
  Backpack called into SOLIDWORKS continuously; in a dimension field it stopped after one check. That matches the reported
  difference (fields work, viewport shortcuts do not).
  Now checks happen only on real focus changes and at most four times per change (the first check and three bounded
  re-checks); there is no periodic polling. Repeated focus notifications for the same element within a second are ignored.
  The UI Automation focus subscription only asks for the element's identity. Privacy protection is unchanged: password
  fields and blocked apps are still hidden, and elements without data are still treated as possibly private.
  **This was not tested with SOLIDWORKS** (it was not available); please verify with the checklist below.

## Manual verification (Windows)

1. Install with the installer; start Backpack; Plugins → turn on Focus, Lens and Snapshot Overlay.
2. Focus, Lens, Snapshot Overlay on a clean installation: the frame is emerald (`#31C99A`). A colour you set before
   updating is unchanged.
3. App settings → Privacy → Dynamic privacy **on**. In SOLIDWORKS (or another CAD program): in the 3D viewport use
   single-key shortcuts (e.g. S, F, Space, arrow keys), Ctrl/Alt/Shift combinations (Ctrl+Z, Ctrl+S, Ctrl+1…), Esc,
   repeated presses and held keys; type dimensions into input fields; switch windows with Alt+Tab and back.
   Everything must work as with Dynamic Privacy **off**. Repeat with it off.
4. Shortcuts of other programs and Backpack's own hotkeys (for example Ctrl+Alt+Shift+O, Ctrl+Alt+Shift+S) still work.
5. Privacy itself: type into a Windows password field (e.g. a browser login) — the keys are not shown.

## Compatibility

- SDK 1.3 (`overlay.snapshot`); plugins for SDK 1.0–1.2 keep working. Focus and Lens are now 1.0.2 and are updated
  automatically with this version (your on/off choice is kept).
- Plugin manifests may list `previousDefaults` for a setting (see `docs/plugins/README.md`).
- Settings of 0.4.0-preview.7 are read; settings of 0.4.0-preview.6 and earlier are not (as in 0.4.0-preview.7).

## Known limitations

- The installer and the package are unsigned.
- In a CAD 3D viewport that does not report whether it is a password field, Dynamic Privacy still hides the keys **in
  Backpack's display** (privacy-first: “no data” is treated as possibly private). This only affects what Backpack shows,
  not the keys that reach the program. Turn Dynamic Privacy off while recording such programs if you need the keys shown.
- Interactive checks (privacy with real applications, snapshot capture at different DPI, multi-monitor) were not run on a
  Windows desktop for this release; automated tests and the installer's silent install/uninstall ran on GitHub Actions.
- The help is not translated into the 16 newer interface languages (English text with a notice).
