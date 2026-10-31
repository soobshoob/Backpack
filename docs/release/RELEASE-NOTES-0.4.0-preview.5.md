# Backpack 0.4.0-preview.5

Preview. Windows 10/11 x64, portable, self-contained. **Not digitally signed.** Not published.

## New

- **Drawing and the layout indicator are plugins.** Keys and the mouse next to the pointer are part of Backpack; everything else is one of six **plugins from the creator**: Drawing, Layout indicator, Cursor magnifier, Lens, Focus and Timer. They come with Backpack and appear in “Plugins” by themselves, turned off — no searching for packages; turning one on asks you to confirm trust the first time. The **From the creator** mark is tied to the delivery index (`Plugins\bundled.json`: file, ID, version, SHA-256), not to the author name in the manifest; it is not a publisher signature. A plugin from the creator you remove is not added back by itself (“Add back” on the Plugins page). Drawing keeps every feature (toolbar modes, radial menu, shapes, smart pen, text, numbers, colours, undo/redo, screenshots); the indicator keeps its look, monitors, duration, filtering and the “no display after start or wake” rule.
- **SDK 1.2.** `overlay.draw` (the host drawing canvas), `overlay.indicator` (a host-drawn indicator), `input.layout` (layout changes, no key presses), `UpdateSettings`, `OnToolState`, commands that work only while the plugin's tool is open (`whileTool` — F2 for screenshots stays with other apps outside drawing), default command hotkeys and icons, hidden settings, and `drawing`/`indicator` demo scenes. Plugins for SDK 1.0 and 1.1 keep working.
- **Backpack Neon and independent appearance choices.** “Appearance” now separates light or dark, the colour theme, the **surface style** (Standard, Liquid Glass, Backpack Neon) and the material with transparency. Neon: the darkest base, thin borders in the theme's glow colour with a hierarchy (calm cards, clearer fields, brightest selection and focus), a soft glow on active elements, its own light variant, a window border in the glow colour on Windows 11; works with Acrylic and Mica transparency; glow can be turned off or weakened; high contrast uses the standard style.
- **Backing material for keycaps and the mouse icon:** Fill or Transparent (no fill, a thin outline keeps the shape) with a preview on a light and a dark background; saved in styles and profiles. Acrylic and Mica are not offered for overlays: measured on click-through overlay windows, Windows applies them to the whole window rectangle, without the rounding and without fading; a profile that asks for them uses the fill and the page says so.
- **Integrated title bar.** No separate strip with a small duplicate name: the large “Backpack.” wordmark, the page title and the window buttons form one row; dragging, double-click, Snap layouts, resizing, Alt+F4 and the window menu work as before. The emerald **dot** is part of the wordmark (menu, Home, About).
- **Two labels on keycaps:** independent sizes of the main and the small label (as a share of the keycap face), corner, and horizontal/vertical offsets with a clear origin and range; the small label sits bottom right with a readable gap by default. Saved in styles and profiles (settings format 9).
- **Compact icon actions.** Style actions are one row of icons instead of three rows of buttons; rare ones move to a “…” menu in a narrow window. Plugin cards, “Reset section”, the demo play/pause and other repeated actions use the same icons with tooltips, accessible names, hotkeys and the reason when unavailable; consequential actions (turning a plugin on, removal confirmation) keep text.
- **Fn check:** `Backpack.exe --lab --fn-trace=file.txt` records only service keys for 45 s or until Esc (no letters, digits or punctuation).

## Fixed

- **Taskbar icon too small.** Cause: the window got a vector image whose size is the bounds of its lines (about 24×30 DIP), and WPF put it 1:1 into a 48×48 icon. The window now gets a multi-size ICO with a thicker outline for small sizes.
- **Second labels stuck to the main letter.** Cause: the small label was attached to a corner of the main label's area, not of the keycap face. Placement is now computed from a safe point at the face corner; offsets from the previous format are reset on reading.
- The Ukrainian Enhanced layout (and other layout variants) lost its label because the extended layout handle was truncated.
- Plugin messages with whole numbers sent from in-memory objects were read with default values (the pipe re-parses JSON, so real plugins were not affected).

## Compatibility

- Settings format 9: `appearance.keycap.twoLabels`, `group.background.material`, `mouse.indicatorMaterial`. The profile's `drawing` and `layoutIndicator` sections and `app.json` `draw` and `markers` are kept only as the source for moving them to the plugins.
- `app.json`: `material.surface`; the former material `liquidGlass` opens as Acrylic with the Liquid Glass style.
- When you update, settings, hotkeys (a removed hotkey stays removed) and menu items of the former built-in drawing and layout indicator move to the plugins; a note says so; the plugins stay off until you turn them on.

## Known limitations

- The package is unsigned. No update address is configured.
- Acrylic and Mica are not available for the keycap and mouse backing (see above).
- Hardware Fn was not traced with a physical key press yet: run `Backpack.exe --lab --no-plugins --settings=%TEMP%\bp-fn\settings.json --fn-trace=%USERPROFILE%\Desktop\backpack-fn.txt`, press Fn alone twice, then Fn+F1, Fn+F5, Fn+F6 and Fn+arrows once, then Esc, and send the file.
- The SDK and samples have no license of their own yet: they are covered by PolyForm Strict 1.0.0 (no changes, no redistribution of changed copies).
- Plugins run in their own process but are not sandboxed: turn on only plugins you trust.
