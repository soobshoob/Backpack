# Backpack 0.4.0-preview.3

Preview. Windows 10/11 x64, portable, self-contained. **Not digitally signed.** Not published.

## New

- **Adaptive layout of every page.** Two layouts instead of squeezing: in a wide window the live preview is a fixed-width column on the right, as tall as the page area; when there is not enough room, the page switches to a vertical layout with the preview as a strip above the options. The preview never scrolls with the page; the top edge of the scrolling area fades softly. Long labels wrap, a control that does not fit next to its label moves under it, segmented choices and groups of buttons wrap, sliders keep their number box visible. The page title wraps and the “Reset section” button moves under it when needed. A narrow window shows an icon-only menu in one straight column with a square highlight; the menu button opens the full menu over the page. The help window collapses its table of contents in a narrow window. Minimum window size 560 × 440 DIP.
- **Profiles save themselves.** Every accepted change applies at once and is written to the current profile; quick series (sliders) are combined. Pending changes are written before switching profiles, before exporting and on exit. “Export current profile…” always contains the current state. A failed write is shown with the reason and “Try again” — never as success. A read-only (reference) profile is never overwritten: a working copy is created. The “Save / Don’t save” dialog, the “not saved” mark and the profile chip in the page header are gone.
- **Direct handles for Focus.** In view mode the lens, the source area and the dimmed area have small handles: move the lens, move the source, resize at the corner — no Ctrl and no separate mode needed; everything else still lets clicks through. With the source shown the lens and the source move independently; with it hidden the lens magnifies what is under it and the source follows. Moving across monitors keeps the size in DIP.
- **Plugin cards.** “Plugins” shows installed plugins as cards (icon or letter tile, name, version and author, description, state, “In the menu”, details and actions) or as a list; the choice is remembered. Optional `icon` in `manifest.json` (a PNG inside the package). Viewing a card runs no plugin code; the trust confirmation is unchanged.
- **Fonts: Standard or Spectral.** Both selectors offer only “Standard” (built-in Inter) and “Spectral” (built in, OFL 1.1, not installed in Windows). For the interface, Spectral is used only for the page title, large help headings and the program name; all other text stays in the standard font. Keycap labels can use Spectral independently.
- **One entry to help.** The separate “Help” page is removed; “Help” at the bottom of the menu opens the topic of the current page (F1 does the same). Checks, the diagnostics window, settings file information and full reset moved to “App settings”.

## Fixed

- **Privacy turned on by mistake on the Windows desktop and stayed on.** Cause: the desktop icon list (`SysListView32` in `Progman`/`WorkerW`) does not report `IsPassword` through UI Automation; this was treated as “no data”, input was hidden, and no further focus events arrived until another window was activated. Now only this exact context counts as an ordinary element; everything else without data stays hidden. “No data” is re-checked every 1.5 s, so the state recovers without switching windows. Repeated identical answers no longer clear the screen again.
- **Drawing preview.** The toolbar in the preview was scaled separately from the screen and placed by its own rules; now the real toolbar is placed by the same function as in drawing mode, on a screen with the proportions of the work area, in every mode (always visible, every auto-hide edge, compact, radial only).
- The source frame of the lens used the scale of the lens monitor when the source was on another monitor.
- Buttons with long labels no longer leave their cards: labels wrap inside the button.

## Compatibility

- Settings format 7: `fonts.ui` and `fonts.keycap` accept only `"Spectral"` (absent — Standard). Format 6 files with any other font are read and switch to Standard.
- Profile format 3 (written by the autosave model); formats 1 and 2 are read. On first start, if the applied settings differed from the saved profile, the saved version is kept as a separate profile.
- `app.json` gains `pluginsView` (`cards` / `list`).

## Known limitations

- The package is unsigned. No update address is configured.
- When the lens and its source are on monitors with different scale, the zoom is kept in physical pixels.
- Menus and tooltips are separate pop-up windows without the system material.
- Windows reports focus changes asynchronously: in a rare race, keys typed right after a focus change can be attributed to the previous field.
- Plugins run in their own process but are not sandboxed: turn on only plugins you trust.
