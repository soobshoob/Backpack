# Magnifier, lens, focus and timer

These tools are **plugins**: each has its own ID, version, settings, commands, page and demo. They come with Backpack in the `Plugins` folder next to the program but are not turned on: import them on the Plugins page and turn on the ones you need (Backpack asks you to confirm trust). How plugins work is described in [Plugins](15-plugins.md).

![Example: the “Focus” plugin page with its demo](../images/en/settings-plugin-focus.png)

## Magnifier at the pointer

Three independent commands, each with its own hotkey — there is no mode to choose:

| Command | Behavior |
|---|---|
| “Magnifier: while held” | the magnifier is shown while the hotkey is held; releasing it closes it |
| “Magnifier: follow the pointer on / off” | the magnifier follows the pointer until you press it again |
| “Magnifier: pin here / remove” | a live magnifier stays where the pointer was |

- There is never more than one magnifier on screen: “follow” and “pin” replace each other; holding while a magnifier is already open adds nothing.
- Pressing the same command again closes the magnifier. Esc, locking Windows, turning the plugin off or removing it closes it at once.
- The magnifier has no buttons or handles: the pointer is always inside it, so it is controlled only with commands and Esc.
- Windows does not report releasing a hotkey: Backpack checks it every 30 ms and closes a held magnifier after 5 minutes at the latest.
- Clicks pass through the magnifier. The system pointer and Backpack’s own windows are not in the picture.

![Example: the “Cursor magnifier” plugin page](../images/en/settings-plugin-cursor-zoom.png)

## Lens

“Lens: open / close” — select an area with the mouse (Esc cancels; a click without dragging uses the size from the settings). A movable window shows a live magnified copy of the source area; the source is marked with a dashed frame when “Show the source” is on. The lens and the source move independently.

## Focus

“Focus: dim around an area / off” — select an area; the rest of the monitor (or all monitors, as set) is dimmed. The area itself is unchanged: it is not magnified and not a screenshot.

## Snapshot overlay

The “Snapshot Overlay” plugin keeps a **still picture** of part of the screen on top of your other windows — for example a technical drawing next to a CAD model on a single monitor. It is not a live mirror: the picture does not change until you capture a new one.

| Command (default hotkey) | Action |
|---|---|
| “Snapshot Overlay: capture an area / replace” (Ctrl+Alt+Shift+S) | select an area with the mouse; the picture appears at once. With a snapshot open, the new one replaces it |
| “Snapshot Overlay: expand / collapse” (Ctrl+Alt+Shift+E, only while a snapshot exists) | expand almost to the full screen and back |
| “Snapshot Overlay: show / hide”, “clear the snapshot”, “show controls”, “reset settings and position” | no hotkey by default — assign one on the plugin page |

- **Select:** drag a rectangle; a click without dragging takes the default size; Esc or right click cancels (an older snapshot stays). The selection window and the current snapshot are hidden before the picture is taken, so they are not in it. The picture keeps the full resolution of the captured pixels.
- **Move and resize:** drag the picture itself or the handle above it; the corner handle changes the size and keeps the proportions. Handles and the buttons “Expand”, “Replace”, “Hide” and “Clear” appear on hover, as with the lens. The place and size are remembered.
- **Expand:** double-click the picture, press **Space while the pointer is over it**, use the hotkey or the button. The picture fills about 90–95 % of the monitor's work area in its own proportions. Collapse the same way, with Esc, by clicking outside it or by switching to another window — it returns exactly to its previous place and size.
- **Space** is taken only while the pointer is over the snapshot, no mouse button is held and no text is being typed in the active window; otherwise Space stays with your apps. If that is in the way, turn “Space over the snapshot” off and use the hotkey.
- The picture does not take keyboard focus, and clicks outside it reach the app below. “Clicks pass through the snapshot” makes the picture itself click-through too.
- The picture is kept **only in memory**: it is never saved to disk or passed to the plugin. It disappears when you clear it, turn the plugin off or exit Backpack (and, if you choose so, when Windows is locked).

## Controls on hover

Buttons, labels and move and resize handles of the lens, focus and timer are hidden by default — only the content is visible (the magnified picture, the frame, the digits).

- Point at the tool or its compact control area and the panel fades in; move away and it hides after a short delay, without flicker.
- Moving from the tool to its panel does not close the panel. It stays while you drag, resize or have a menu open.
- Hidden elements do not take clicks, and hovering does not take focus from the app below. Clicks on the lens and the focus area reach the app.
- The commands “Lens: show controls” and “Focus: show controls” show the panel without hovering.
- With Windows animations off the panel appears at once. The panel stays within the current monitor.

## Timer

The “Presentation timer” plugin shows a countdown above other windows.

| Command | Action |
|---|---|
| “Timer: start / pause” | start, pause and resume |
| “Timer: reset” | back to the set time |
| “Timer: show / hide the panel” | the panel only; the countdown is not changed |

- Move and resize the panel with the handles shown on hover; digit size, opacity and accent color are in the plugin settings. Settings are kept in the profile.
- The countdown follows the clock, not the ticks of an interface timer: load and drawing delays do not stretch seconds.
- **Sleep:** time keeps running while the computer sleeps. If the countdown ended during sleep, the panel shows 00:00 and the finished state after waking.
- When it ends, 00:00 stays with a subtle accent frame. Sound is a separate setting, off by default.
- After Backpack restarts, the timer does not start by itself and the panel does not appear until the first command.

## Moving over from the built-in tools

In earlier versions the magnifier, lens and focus were built in. When you import the bundled plugins, Backpack moves their settings, hotkeys and menu items over: the old shared “Focus” command goes to the command of the mode that was selected. Moving over does not turn a plugin on and does not confirm trust — you do that.
