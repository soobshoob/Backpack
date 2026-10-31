# KeyOverlay 0.3.0-preview.2 (Preview)

Second packaged preview for Windows 10/11 x64. Portable, self-contained, not digitally signed. Not published.

## New

- **License**: KeyOverlay is now licensed under the PolyForm Strict License 1.0.0. The package contains `LICENSE.txt` (the official text, unchanged) and `NOTICE.txt`; the “About” page shows the license name with a link to the official page.
- **Home page** redesigned: a welcome area, a short looped demo drawn by the real overlay code (keycaps, mouse icon, drawing and lens) that can be paused or turned off and stays paused when Windows animations are off, feature cards and quick actions.
- **Navigation**: Settings now has separate “Live lens” and “Drawing” pages; the “Hotkeys” page contains only hotkeys. Diagnostics and checks moved to the “Help” page.
- **Drawing toolbar modes** (saved in the profile):
  - Always visible: drag it by its handle, and the position is remembered.
  - Auto-hide at a chosen edge (top, bottom, left, right): it opens after a short dwell at the edge and hides smoothly.
  - Radial menu only.
  - Right click opens the radial menu in every mode.
- **Text tool**: click, type several lines, move it before confirming, and confirm with Ctrl+Enter. Size and color are adjustable, and undo/redo works. It is available on the toolbar and in the radial menu.
- **Screenshots in drawing mode**: F2 by default (single key, active only while drawing) or the 📷 button.
  - The screenshot includes your drawings and leaves out the toolbar and menus.
  - It is saved as a PNG named by date and time, copied, or both.
  - Choose the folder and what happens after the shot: keep drawings, clear them, or finish drawing. If saving fails, drawings are always kept.
  - Holding F2 does not create repeated shots.
- **Radial menu**: added text, redo, screenshot and exit.
- **Simultaneity threshold** for “Any keys held together” (150 ms by default, 0–500): fast typing no longer merges into combinations.
- Toolbar and button rows: rounded buttons with consistent hover, pressed and selected states. Button groups keep even spacing and wrap without overlapping.

## Formats

- Settings format 4 (adds the drawing profile and the simultaneity threshold); formats 1–3 are read and migrated.
- Profile format 2 is unchanged; profiles carry the drawing profile through their settings.
- `app.json` gains the screenshot hotkey, the screenshot folder, the text size and the Home animation switch. Older files are read with defaults.

## Known limitations

- The update source is **not configured** in this build: the release repository has not been chosen yet.
- The package is **not signed**; Windows may show a warning.
- Screenshots capture the monitor where drawing is on. Windows that block screen capture (protected content) appear black, as in other capture tools.
- Combinations handled by Windows or other programs before KeyOverlay receives them cannot be shown.
