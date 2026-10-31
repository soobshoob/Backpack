# Backpack 0.4.0-preview.4

Preview. Windows 10/11 x64, portable, self-contained. **Not digitally signed.** Not published.

## New

- **Cursor magnifier, Lens, Focus and Timer are plugins.** Each is a real importable plugin with its own ID, version, manifest, settings, commands, page, demo, translations and life cycle (`com.example.cursor-zoom`, `com.example.lens`, `com.example.focus`, `com.example.presentation-timer` 2.0.0). Packages are in `Plugins\`, sources with build instructions in `Samples\`. The built-in copies are gone. The “Tools became plugins” card imports them and moves your earlier settings, hotkeys and menu items; importing never starts code, and turning a plugin on still asks you to confirm trust.
- **SDK 1.1: tools drawn by the host.** `overlay.magnifier` (follow, pinned or region) and `overlay.dim`: Backpack captures and draws; the plugin receives neither screen images nor pointer coordinates. Hold commands (`"hold": true`), panel buttons, states and a movable panel, grouped settings with units, `showWhen`, colours and a host-drawn demo. See `Samples\README.md` and the ADR addendum in `PLUGINS-TRUST.md`.
- **Independent magnifier commands.** “While held”, “Follow the pointer on/off” and “Pin here/remove” each have their own hotkey; the “Mode” setting is gone. Never more than one magnifier at a time; Esc, lock, disabling or removing the plugin closes it.
- **Controls on hover.** Buttons, labels and handles of the lens, focus and timer are hidden until you point at them, through one shared host mechanism: no flicker, the panel stays while dragging or with a menu open, hidden parts take no clicks, hovering takes no focus. “Show controls” commands reveal them from the keyboard.
- **Timer.** Clock-based countdown (load and sleep do not stretch seconds), start/pause/resume/reset, movable and resizable panel, 00:00 with a subtle finished state, optional sound (off by default), never starts by itself after a restart.
- **Navigation.** The Backpack logo above the menu opens Home (tooltip and accessible name “Home”, Enter/Space); the separate “Home” item is gone. The menu has no visible scroll bar but still scrolls with the wheel, touchpad and keyboard, and the selected page is always scrolled into view. The menu and the page area fade softly at their edges; the last item and the last card are fully visible at the end.
- **Previews are virtual monitors.** A tall preview is a portrait monitor, a wide one a landscape monitor, with no small screen inside empty margins. The drawing toolbar keeps its real size and switches to its compact form when it does not fit along the chosen edge.
- **Adaptive catalogs.** Keycap, mouse and layout indicator styles use as many columns as fit; names wrap instead of “Low profile — cla…”.
- **Shorter pages.** Options that apply only to another variant are hidden (for example, Gradient only for Laptop and PBT, L-shaped Enter only for Volumetric) and keep their values. Long explanations were shortened in all three languages.
- **Second label on keycaps** in a chosen corner (bottom right by default) with size and offsets; checked with Russian, Ukrainian, Greek and German (QWERTZ) layouts of the target app.
- **Media keys** show Mute, Vol −/+, Play, Next, Prev, Stop and F13–F24 instead of “SC E0 20”. Diagnostics: `Backpack.exe --lab --input-trace=file.txt`.
- **Drawing:** neon palette with glow (default neon emerald; the old default red moves to it), a palette switch and an opacity slider. **Mouse:** outline thickness for every family and an optional outline for the solid one.
- **Taskbar icon.** The backpack outline now fills about 95 % of the icon height (was about 78 %) and is centred in every ICO size and the window icon.
- “Made in Ukraine” on the About page.

## Fixed

- A hotkey assigned to a command that is currently off was shown as “Not set”; it is now “Unavailable”.
- Plugin capabilities `overlay.magnifier` and `overlay.dim` showed raw resource keys on the plugin page.

## Compatibility

- Settings format 8: `plugins` (plugin values in the profile) and secondary label `corner`, `size`, `offsetX`, `offsetY`. Earlier formats are read.
- `app.json` format 5. Earlier settings of the built-in Focus and magnifier stay in the file only as a source for moving them to the plugins.
- SDK 1.0 plugins keep working.

## Known limitations

- The package is unsigned. No update address is configured.
- Fn on the test laptop: Fn+F1 arrives as the Mute key; whether Fn itself reaches Windows was not confirmed with a physical key press, and Fn+F6 (Win+Shift+S) was not traced on hardware.
- The SDK and samples have no license of their own yet: they are covered by PolyForm Strict 1.0.0 (no changes, no redistribution of changed copies).
- Plugins run in their own process but are not sandboxed: turn on only plugins you trust.
