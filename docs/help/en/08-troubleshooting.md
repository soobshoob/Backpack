# What to do if…

## …the windows are gone but the overlay works

The windows are in the tray. Click the Backpack icon in the notification area. If you cannot see it, click the ^ arrow next to the clock: Windows may have put it among the hidden icons.

## …you cannot exit

“Close” in the settings window closes only that window, and × of the diagnostics window hides the windows to the tray by default. To quit completely, choose “Exit Backpack” in the icon menu or click “Exit” in the diagnostics window. You can change what × does on the “App settings” page.

## …keys are not shown

1. Check “Show display” in the icon menu (or press the show/hide hotkey).
2. Check whether “Combinations only” is on.
3. In “App settings” → “Checks”, click “Show” next to “Example on the overlay”: if the example is visible, display works.
4. Check the “Position and monitors” page: the row may stick to another part of the screen.
5. In the diagnostics window, check whether the event counter grows when you press keys.

## …a key is “stuck”

Press and release it once more or click “Reset state” in the diagnostics window. After locking the computer, a UAC prompt or waking from sleep, keys that were held are shown with “?”: Backpack does not know whether they were released and removes them after the linger time.

## …there is no layout indicator

See [Layout indicator](05-keyboard-layout.md#if-the-indicator-does-not-appear).

## …letters flicker when typing fast

Choose “Trail: Calm” on the “Keys” page — each letter takes the place of the previous one. See [Keys, combinations and labels](03-keys-and-labels.md#calm-typing-or-a-trail).

## …a profile does not load or import

Backpack shows the list of problems in the file. The current settings are not changed. Common causes: a file from a newer version, a mistake in a manual edit, an unknown field. See [Profiles](07-profiles.md#import-and-export).

## …settings were reset

“App settings” → “Checks” shows the settings file path and any problems found when loading it. A damaged file does not stop the program: Backpack uses defaults and keeps a copy of the file as `settings.unreadable.bak.json`. Settings in the old format are upgraded automatically; the previous file is kept as `settings.v1.bak.json`.

## …file dialogs are light in the dark theme

They are Windows system dialogs: they follow the Windows theme and language, not the Backpack settings.

## …the overlay is missing from a screen recording

The overlay is a transparent window on top of others. Some screen capture methods do not capture such windows. How particular recording programs see it has not been checked.

## …a Win combination is not shown

1. Check that the combination is visible with “All keys” (with “Combinations only” a single Win is hidden on purpose).
2. Some Win combinations open Windows panels (Start, search, Task view). Such panels can cover the overlay: this is a limit of display, not of Backpack settings.
3. Some combinations are handled by Windows or another program before Backpack receives them. Backpack does not invent combinations it did not receive.
4. To check what reaches Backpack, see the Win combination counters in the diagnostics window (“App settings” → “Checks” → “Open”). Only numbers are shown, no keys.

## …the lens shows no picture

The lens shows the picture only when Windows gives it: on the lock screen, in a UAC prompt or over some protected windows it says “not available”. Close it with Esc or the hotkey.

## …the mouse does not work after drawing

Exit drawing with Esc, ✕ on the toolbar or the drawing hotkey; hiding the display also closes drawing. After exit, no Backpack window takes mouse clicks.
