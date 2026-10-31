# KeyOverlay 0.3.0-preview.1 (Preview)

First packaged build of KeyOverlay for Windows 10/11 x64. Portable, self-contained, not digitally signed. Not published yet.

## New

- **Main window** with Home, an expandable Settings group, Profiles, Help and About. The tray icon opens the main window; closing it keeps KeyOverlay in the tray.
- **Style catalogs** with thumbnails for keycaps, the layout indicator and the mouse: built-in presets (never changed) and your own styles — save, save as new, duplicate, rename, delete, restore, import, export, open the folder. Profiles carry a full copy of the look and embed your styles on export.
- **Keycap renderers**: low profile, laptop, PBT, minimal (own WPF implementations of the researched Keyviz styles) and a new **3D** renderer (front view, side faces, label on the top face, L-shaped Enter).
- **Mouse icon families**: outline, solid, retro (four palettes), sketch. Separate highlight of left, right, middle and side buttons; manual side-button setup (hidden/one/two, left/right); **scroll direction arrow** (vertical and horizontal) with configurable color and duration.
- **Combinations**: new option “Any keys held together” (A+B forms a group); independent of “All keys / Combinations only”.
- **Global hotkeys** (this computer only): show/hide the display (Ctrl+Alt+Shift+O), live lens, drawing, radial menu. Conflicts, busy and reserved combinations are reported.
- **Live lens**: select an area; a rounded, slightly magnified live view with a frame and shadow; view-only, click-through, closes by Esc, the hotkey or a timer.
- **Temporary drawing**: freehand, line, arrow, rectangle, ellipse; colors, opacity, thickness, solid/dashed/dotted; undo/redo/clear; fading or until exit; compact toolbar and a radial menu.
- **About** page: version, channel, release date, author, licenses, update check via GitHub Releases (Stable/Preview), optional daily check. Nothing is downloaded or run automatically.
- **New logo** and application/tray icons.
- **Data folder** `%APPDATA%\KeyOverlay`, portable mode, automatic copy of settings and profiles from the previous `KeyOverlay.Lab` folder. Settings format 3, profile format 2, app settings format 2 (older formats are read).

## Fixed

- The pinned preview and the scrolling options are separated by a visible edge; the preview height adapts to the window height.
- Combination diagnostics: aggregate counters per input source show whether a Win combination reached KeyOverlay (no key identities are recorded).

## Known limitations

- The update source is **not configured** in this build: the repository has not been chosen yet.
- KeyOverlay’s own license is **not chosen yet** (see LICENSE-STATUS.md).
- The package is **not signed**; Windows may show a warning.
- Some Win combinations open Windows panels that can cover the overlay; combinations handled by Windows or other programs before KeyOverlay receives them cannot be shown.
- Layout detection is not confirmed by a manual check in every program.
- The lens cannot show protected content (lock screen, UAC, some elevated windows) and says so.
- Scrolling is shown on the mouse icon at the cursor, not in the keycap row.
- The overlay is shown on the primary monitor only.

## Checksums

See `SHA256SUMS.txt` next to the archive.
