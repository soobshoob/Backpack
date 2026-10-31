# Backpack 0.4.0-preview.6

Prerelease. Windows 10/11 x64, portable, self-contained. **Not digitally signed.**

This first GitHub prerelease uses the verified local package built on 2026-10-31. Its embedded release notes describe
the state before publication; these repository and GitHub notes include the publication details. The ZIP is unchanged.

## New

- **The Backpack core is now under the Mozilla Public License 2.0.** You may use, change and share it; changed files of the core must stay available under MPL 2.0. The plugin SDK and the plugins from the creator are **not** under MPL: they stay under PolyForm Strict 1.0.0 until the owner decides on separate terms (a proposal is in `docs/release/LICENSING-PROPOSAL.md`). Packages up to 0.4.0-preview.5 keep the license they were released with. Details: `LICENSING.md`, `docs/release/LICENSE-STATUS.md`.
- **19 interface languages.** All languages of the Backpack website — English, Ukrainian, German, French, Spanish, Italian, Portuguese, Dutch, Polish, Czech, Hungarian, Romanian, Swedish, Danish, Norwegian Bokmål, Finnish, Lithuanian, Greek — plus Russian. Windows, dialogs, tooltips, the tray menu, errors, plugin pages and commands of the plugins from the creator, the drawing toolbar and “About” (with “Made in Ukraine”) are translated. “System” picks the Windows language when Backpack has it. The help is in English, Ukrainian and Russian; in the other languages it shows the English text and says so at the top.
- **Backpack Neon Emerald** is the look of a new installation: a **Neon** keycap renderer and a **Neon** mouse icon — dark surface, a thin emerald line and a soft glow that gets stronger while a key or button is held. “Glow” (0 turns it off) is saved in styles and profiles. Existing settings and styles are not changed; “Reset section” returns the brand style.
- **Smart pen: arcs and angles.** A smooth curve becomes an arc through three points with three handles (three points on one line give a straight line). Two straight strokes meeting at a corner become an angle; its size in degrees (the smaller angle) is shown next to the vertex while the button is held, away from the pointer and inside the screen. A toolbar button keeps angle sizes on screen.
- **Shift and Alt while drawing:** Shift — square or circle, lines and arrows in 15° steps, polygon rotation in 15° steps; Alt — from the centre; both together. The keys work during the drag; a short hint appears the first time each tool is picked.
- **Resize and rotate selected shapes.** Corner and side handles, a rotation knob, matching pointers; Shift keeps proportions or snaps rotation to 15°, Alt resizes from the centre, Esc cancels the drag. One drag is one undo step. Line thickness stays the same; text scales as a whole; numbered marks only move.
- **Plugin icons.** A plugin's author picks an icon in `manifest.json` (`"symbol"`, one of the icons built into Backpack); you can choose your own on the plugin page (“Display” → “Choose an icon”, with search). Your choice is stored by plugin ID and survives updates. The six plugins from the creator have distinct icons; the star is used only for “In the menu”.
- **App icon = the website logo.** The icon is drawn from the exact geometry of the website's backpack mark, in sizes 16–256.
- **Repository preparation** (local only, nothing published): README, build and contribution guides, security policy, licensing boundaries, GitHub Actions for pull-request checks and tagged releases, a plugin catalog that accepts entries by pull request (`registry/`), and a funding template. What still needs the owner is listed in `docs/release/GITHUB-SETUP.md`.

## Fixed

- **Page header alignment.** The back arrow and “Reset section” are now centred on the first line of the page title, and the reset button sits at the right edge of the content, on every page including plugin pages. Cause: the header was an adaptive row that centred its items on the whole row, so a large title or a wrapped title moved them.
- **The second (small) keycap label jumped to the top-left corner after a key was released.** Cause: on a state change the labels are recreated, but the keycap's own arrange step does not run again when its size is unchanged, so the new label stayed at (0, 0). The label layer now places the small label itself before laying out.
- **Keys stopped being displayed after some time (investigated, not reproduced).** Defects found on the way and fixed: overlay window recovery had a lifetime limit of five attempts — after that a lost overlay was never restored and only switching the display off and on helped; it is now limited to five attempts per five minutes. Only the first overlay window was checked; now every overlay window is. Messages to plugin processes were written on the UI thread, so a stuck plugin could block it; they now go through a bounded queue on a separate thread. A display pipeline watch was added (`--display-trace=file`, also in the diagnostics export) that records counters and the stage where display stops — never typed text. A 20-minute run in a test window (565 test key presses, all shown, password-field presses never shown) did not reproduce the problem, so it is **not** declared fixed.
- Plugin manifest conditions written as `{ "key", "values" }` were silently ignored (Drawing, Layout indicator); they now use the documented form.

## Compatibility

- Settings format 9 (unchanged). New optional fields: `appearance.border.glow`, `mouse.glow`, renderer `neon` for keycaps and the mouse; `plugins.json` → per-plugin `symbol`. Older Backpack versions ignore them.
- SDK 1.2: `DrawRequest.AngleLabels`. Plugin manifests may localize text in the 19 languages above and declare `"symbol"`.
- `Backpack.exe --lab --selftest-ui=languages` checks the layout of every page in all 19 languages (slow; the package check uses the faster default).

## Known limitations

- The package is unsigned. No update address is configured.
- The help is not translated into the 16 new languages (English text with a notice).
- The display stall was not reproduced: if keys stop showing, run `Backpack.exe --display-trace=%USERPROFILE%\Desktop\backpack-display.tsv`, keep it running until it happens, and send the file (it holds counters and stages, no key names).
- The GitHub workflows were checked for syntax and by running their scripts locally; they have not run on GitHub yet.
- Hardware Fn has still not been traced with a physical key press (see preview.5 notes).
- Plugins run in their own process but are not sandboxed: turn on only plugins you trust.
