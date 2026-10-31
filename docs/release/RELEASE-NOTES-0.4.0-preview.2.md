# Backpack 0.4.0-preview.2

Preview. Windows 10/11 x64, portable, self-contained. **Not digitally signed.** Not published.

## New

- **One design system for every window.** Rectangular buttons with one corner radius, square icon buttons, the same hover, pressed, focus and disabled states in the main window, help, diagnostics, dialogs, menus and the drawing toolbars. Round shapes remain only where the shape means something: the radial menu, the circle tool and the cursor indicator.
- **Window title bar merged with the window.** Own title strip with the logo; drag, double-click, resize, the window menu (right click, Alt+Space), Alt+F4, maximize and snap layouts behave as in Windows.
- **Navigation.** Groups with nested pages (Keys, Mouse, Plugins, App settings), a Back button (Alt+←, mouse back button), no loops in the history; buttons inside pages, F1, help links, favourite plugins and the tray keep the menu in sync. A wide window shows the live preview on the right; moving previews are clipped to the rounded frame.
- **Window material:** Opaque, Mica, Acrylic (Windows system materials) and Liquid Glass (Backpack's own look over Acrylic), with transparency; readable opaque fallback when transparency is off, in high contrast or on older Windows. **Accent glow** in the gem themes, on by default, with strength.
- **Theme-aware pictures.** Previews and help pictures follow the light or dark theme without inverting colours; keycap, mouse and Focus styles keep their colours. The logo is the backpack outline only; the tray icon follows the taskbar colour.
- **Fonts.** Separate interface font and keycap font, saved in the profile; missing characters come from the built-in Inter.
- **Second layout on keycaps.** A small character of another installed layout in the top-left corner of English keycaps (active layout of the target app or a chosen layout). Privacy is applied first.
- **Drawing.** Smart pen by default for new settings, with a live suggestion while drawing that does not flicker and adds nothing to the history; polygon recognition (3–8 corners); regular polygon tool (3–12 vertices); rounded rectangle with corner radius; select and move; any colour with `#RRGGBBAA`; a compact toolbar; Lucide icons instead of text glyphs.
- **Nested radial menu.** Categories in the inner ring, their actions in an outer ring, Back in the centre, Esc closes one level at a time, the same command catalog as hotkeys and the tray, plugin commands, edge-aware placement.
- **Cursor highlight** — a soft glow that always follows the pointer; the click circle now appears only while a button is pressed.
- **Focus:** default area size, an optional source frame and zoom label.

## Fixed

- The Focus hotkey always started the lens even when Dim was selected. “Focus: start the selected mode” now starts the mode chosen on the Focus page from every entry point (hotkey, tray, radial menu, buttons); “Focus: magnifier” and “Focus: dim surroundings” start exactly their mode.
- Privacy: Tab, Win combinations and other keys pressed in an ordinary field were sometimes dropped. A key now belongs to the field where it was pressed; protected fields hide everything; presses waiting for a check are shown only when that field is confirmed ordinary and are discarded otherwise — hidden input never appears later.
- The unstable Phase 4A privacy self-check: focus events that arrive before the UI Automation subscription is ready no longer confirm a field as ordinary; input waits for the subscription.

## Compatibility

- Settings format 6 (`fonts`, `labels`, cursor highlight, compact toolbar), app preferences format 4 (window material, command bindings). Older formats are read and migrated.
- The five separate hotkeys of earlier versions are moved to commands with the same keys; the old “Live lens” hotkey becomes “Focus: start the selected mode”. The Commands page shows a note once.
- A click circle that was always visible is moved to the cursor highlight with the same colour and size.

## Known limitations

- The package is unsigned. No update address is configured.
- Menus and tooltips are separate pop-up windows; Windows gives them no system material, so they use a dense fill.
- Windows reports focus changes asynchronously: in a rare race, keys typed right after a focus change can be attributed to the previous field.
- Display scaling other than 150 %, a second physical monitor and drawing with a real mouse by hand were not checked on this build; see the Phase 4E report.
- Plugins run in their own process but are not sandboxed: turn on only plugins you trust.
