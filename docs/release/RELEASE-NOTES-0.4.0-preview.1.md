# Backpack 0.4.0-preview.1

Preview. Windows 10/11 x64, portable, self-contained. **Not digitally signed.** Not published.

KeyOverlay is now **Backpack** — a toolkit for presentations, tutorial videos and screen demos. Your KeyOverlay settings, profiles and styles are copied to `%APPDATA%\Backpack` on the first start; the old folder stays as a backup.

## New

- **Dynamic privacy.** Keys are hidden while the input focus is in a field that reports itself as a password field (UI Automation `IsPassword`), while the focus is being checked, when there is no answer, and in apps from your list. All keys, modifiers and combinations are hidden (Ctrl+V too); nothing is shown first and hidden later; hidden presses are never replayed; plugins get only what is displayed. Optional lock badge with the reason. Limits: apps that do not report `IsPassword` correctly cannot be detected automatically.
- **Monitors.** Keycaps and the layout indicator: primary, one selected, several selected, or the monitor under the mouse pointer. Per-monitor DPI and work areas, negative coordinates, connecting and disconnecting, fallback to the primary monitor.
- **Focus** (replaces “Live lens”): one tool, two modes — Lens (live 1.0–3.0×, source and output window separate, explicit edit mode) and Dim (the rest of the monitor dimmed). Neutral and Emerald presets.
- **Magnifier at the pointer** (Mouse page): while held, always on, or in place; circle or rounded rectangle; smoothing; the mouse icon stays beside or hides and then returns.
- **Smart pen** (straight line, ellipse, rectangle; undo returns the stroke) and **numbered marks** (start number, reset, move, undo/redo with consistent numbering).
- **Commands and hotkeys** for every action of the tray, menu and tools, with global, window-only and tool-only scopes.
- **Plugins**: import `.bppkg` packages, details, trust confirmation, turn on/off, update, remove, side-menu favorites, settings pages drawn by Backpack; each plugin runs in its own process. SDK 1.0 and a sample plugin (presentation timer) in `Samples\`. Start without plugins: `--no-plugins`.
- **Design**: Backpack logo, Backpack Emerald theme (default for new installs; existing installs switch with a note — “Classic” returns the previous look), Sapphire and Amber themes, Inter, larger type, Lucide icons, new navigation and Home with a demo.

## Fixed

- Volumetric L-shaped Enter: gaps and a hidden edge drawn over the top face at the notch.
- Keycap labels: symbol and text are centered as one group on all renderers; side marks (R, Num) no longer shift it.
- Laptop renderer: the shadow was cut off by 1 DIP at the bottom.

## Compatibility

- Settings format 5 (`displays`), app preferences format 3; older formats are read and migrated.
- The executable is `Backpack.exe`. `KeyOverlay.portable` is still recognized.
- Global hotkeys and plugin trust are settings of the computer; importing a profile does not change them.

## Known limitations

- The package is unsigned. No update address is configured.
- Privacy and multi-monitor behavior were checked with the app's own test windows and the two monitors of the development machine; other apps, other monitor setups and real typing need manual checks (see the Phase 4 reports).
- Plugins are not sandboxed: turn on only plugins you trust.
