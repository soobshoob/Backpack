# Snapshot Overlay

**Snapshot Overlay** (`com.example.snapshot-overlay`, SDK 1.3, icon `image`) keeps a **still picture** of a part of the screen in
a floating window on top of your other windows. Typical use: you model a part in a CAD program on a single monitor and keep the
technical drawing visible next to the model, instead of switching between the two windows.

It is a static snapshot, not live mirroring (for a live magnified copy use **Lens**). The picture does not change until you
capture a new one or clear it.

## Turning it on

Snapshot Overlay is one of the plugins from the creator: it comes with Backpack in `Plugins\` and appears on the **Plugins** page
turned off, with the **From the creator** mark. Turn it on there (Backpack asks you to confirm trust the first time). Like every
plugin it runs in its own process; the screen picture itself never leaves the Backpack process (see *Privacy*).

To build it from source:

```powershell
dotnet build lab/samples/SnapshotOverlay/SnapshotOverlay.csproj -c Release
powershell -ExecutionPolicy Bypass -File tools/pack-plugin.ps1 -Project lab/samples/SnapshotOverlay/SnapshotOverlay.csproj -Out artifacts/plugins
```

## Capturing

1. Press **Ctrl+Alt+Shift+S** (command “Snapshot Overlay: capture an area / replace”), or run the command from the menu, tray
   or radial menu.
2. The monitor under the pointer gets a light dim and a hint. Drag a rectangle over the drawing. A click without dragging takes a
   320 × 200 DIP area around the point. **Esc** or a right click cancels.
3. The selection window closes, Backpack waits until a frame without it has reached the screen and copies the area into memory
   (GDI `BitBlt` with `CAPTUREBLT`, physical pixels, Per-Monitor V2 DPI awareness). The picture keeps the full resolution of the
   captured pixels; it is not compressed.
4. The snapshot appears at once: by default in the top-right corner of that monitor (at most 420 DIP on the longer side and
   never larger than the captured pixels), or where your previous snapshot was.

Running the same command while a snapshot exists **replaces** it: the current snapshot is hidden during selection (so it can never
end up in the new picture), and the new picture appears in the same place and width. Cancelling the replacement keeps the old
snapshot.

## Moving and resizing

- Drag the picture itself, or the **move handle** that appears above it on hover.
- Drag the **corner handle** (bottom right) to resize. By default the window keeps the picture's proportions; the picture is
  scaled smoothly (high-quality bitmap scaling; at 100 % one picture pixel is one screen pixel).
- The handles and the buttons **Expand**, **Replace**, **Hide** and **Clear** are hidden until you point at the snapshot — the same
  hover mechanism as the Lens, Focus and Timer. “Snapshot Overlay: show controls” shows them without hovering.
- When you release the handle the snapshot snaps fully inside the monitor whose centre it is on. Moving to a monitor with another
  display scale keeps the size in DIP (can be turned off).
- The place and size are saved by Backpack next to the plugin packages (`<plugin-id>.tool.snapshot.json`: four numbers, no
  picture) and reused for the next snapshot, also after a restart.

## Expanding for a moment

| Way | Notes |
|---|---|
| Double-click the picture | on by default (Interaction → “Double-click expands and collapses”) |
| **Space** while the pointer is over the snapshot | on by default; see *Space* below |
| **Ctrl+Alt+Shift+E** (“Snapshot Overlay: expand / collapse”) | registered only while a snapshot exists |
| The **Expand** button on hover | |

The expanded snapshot fills about **90–95 %** of the work area of the snapshot's monitor (48 DIP margin by default), keeps the
picture's proportions, is centred and fully opaque. It is shown on the snapshot's monitor, the monitor under the pointer or the
primary monitor (Advanced setting). Small captures may be enlarged beyond their original size (can be turned off).

It collapses — exactly back to the saved place and size — when you:

- double-click it, press Space over it, press the expand hotkey or the **Collapse** button;
- press **Esc** (Esc is observed, not swallowed: it also reaches the active app);
- with “Collapse on a click outside or a window switch” on (default): press a mouse button **outside** the snapshot and its
  controls, or switch to another window (Alt+Tab, taskbar). The click still reaches the app under the pointer.

Expanding never changes or saves the normal geometry, so repeated expand/collapse cycles cannot drift or grow the window.

### Space

Windows offers no reliable way to know that a key press is “meant for” a window that does not take focus. Snapshot Overlay
therefore takes Space only while **all** of these are true: the pointer is over the snapshot, no mouse button is held, and the
active window has no text caret (no typing in a field). Only then it registers Space as a temporary global hotkey; when the pointer
leaves, the registration is removed and Space belongs to your apps again. If another program already owns Space as a global hotkey,
Space over the snapshot simply does nothing.

Some programs (including some CAD command lines) draw their own text cursor and do not report a system caret; while the pointer
rests over the snapshot, Space then goes to the snapshot. If that is in the way, turn off “Space over the snapshot expands and
collapses” and use the hotkey or double-click.

## Commands and hotkeys

| Command | Default hotkey | Available |
|---|---|---|
| Snapshot Overlay: capture an area / replace | Ctrl+Alt+Shift+S | always while the plugin is on |
| Snapshot Overlay: expand / collapse | Ctrl+Alt+Shift+E | only while a snapshot exists (`whileTool`) |
| Snapshot Overlay: show / hide | — | only while a snapshot exists |
| Snapshot Overlay: clear the snapshot | — | only while a snapshot exists |
| Snapshot Overlay: show controls | — | always |
| Snapshot Overlay: reset settings and position | — | always |

Hotkeys are reassigned on the plugin page (Commands card) or on “Commands and hotkeys”, with the same recorder, conflict and
reserved-key checks as all other Backpack commands. The defaults do not collide with other Backpack defaults and are not Windows
system shortcuts. Hotkeys are registered when the plugin turns on and released when it is turned off, removed or crashes;
`whileTool` hotkeys only while a snapshot exists, so outside that time the keys stay with other programs.

## Settings

The settings page is drawn by Backpack from the manifest (switches, numbers, segmented choices, colour pickers), grouped as below.
Turning the plugin on and off is on its card; Show/Hide, Replace and Clear are commands and hover buttons, because manifest
settings cannot contain action buttons.

| Group | Setting | Default | Meaning |
|---|---|---|---|
| Capture | Where a new snapshot appears | Where the previous one was | or *In a corner*, or *Over the captured area* (1:1) |
| | Corner | Top right | corner for the first snapshot and for *In a corner* |
| | Start size (longer side) | 420 DIP | never larger than the captured pixels at first |
| | Dim the screen while selecting | on | light dim on the selection monitor |
| Window | Proportions | As the snapshot | *Free, whole picture* (letterboxed on the background) or *Free, stretched* |
| | Background | #111513 | behind a letterboxed picture (only for *Free, whole picture*) |
| | Minimum size | 120 DIP | smallest side |
| | Maximum size | 90 % | of the monitor work area, normal view |
| | Distance from the screen edge | 24 DIP | for corner placement |
| | Always on top | on | off: the snapshot goes behind a window you activate |
| Interaction | Double-click expands and collapses | on | |
| | Space over the snapshot expands and collapses | on | see *Space* |
| | Collapse on a click outside or a window switch | on | Esc and the hotkey always collapse |
| | Margin when expanded | 48 DIP | ≈ 90–95 % of the work area |
| | Expanded snapshot may be larger than the original | on | off: at most 1:1 (but not smaller than the normal view) |
| | Clicks pass through the snapshot | off | on: use hover controls, Space or hotkeys; double-click is off |
| Appearance | Opacity | 100 % | normal view; expanded is always opaque |
| | Corner radius, Border, Border colour, Glow | 16 DIP, 2 DIP, #D4D4D4, off | **the same defaults as Focus** |
| | Border opacity | 100 % | |
| | Shadow, Shadow colour, Shadow blur, Shadow opacity | on, #000000, 16 DIP, 55 % | the shadow of the Lens/Focus frame (`MagnifierFrame`); Glow replaces it |
| Advanced | Monitor for the expanded view | Snapshot's monitor | or *Under the pointer*, *Primary* |
| | Keep the size when moving to a monitor with another scale | on | off: keep the size in physical pixels |
| | Keep the snapshot | Until Backpack exits | or *Also clear on lock or sleep* |

The shadow and glow are drawn in a separate click-through window **under** the picture, and the border is a thin ring on the
picture's edge, so neither darkens the picture. Colours are fixed values like in Focus (they do not change with the Backpack
theme). “Snapshot Overlay: reset settings and position” writes all defaults back and forgets the saved place. Settings live in
the active Backpack profile like those of all plugins.

## Lifecycle

| State | How you get there |
|---|---|
| No snapshot | plugin turned on; after Clear, cancelled first selection, plugin off, Backpack exit |
| Selecting | capture command or Replace button |
| Normal | after a capture; after collapse; Show |
| Expanded | double-click, Space, hotkey, Expand button |
| Hidden | Hide button or command; hiding Backpack's display (the picture stays in memory) |

Locking Windows, switching desktops or sleep cancel a selection and collapse an expanded snapshot; the snapshot itself stays
(unless “Keep the snapshot” is *Also clear on lock or sleep*). One snapshot per plugin is used now; the host engine keeps
snapshots per tool ID (up to four per plugin), so several independent snapshots need no redesign.

## Privacy

- The picture exists only in the memory of the Backpack process. It is **never written to disk**, never put on the clipboard and
  **never sent to the plugin** — the plugin only sends commands and parameters (`overlay.snapshot`) and gets a reason when the
  snapshot closes. Nothing is uploaded anywhere.
- The only thing saved is the window place and size (four numbers).
- The picture is released when you clear it, replace it, turn the plugin off, remove it, or exit Backpack.

## Known limitations

- Selection works on one monitor at a time (the one under the pointer when you start), like the Lens.
- Other Backpack overlays that are visible inside the selected area (keycaps, mouse indicator) are part of the screen and
  therefore of the picture; only the selection window and the current snapshot are hidden before capturing.
- The capture copies the composed desktop. GPU-accelerated windows (CAD viewports, browsers, PDF viewers) are captured as shown;
  DRM-protected video shows black. Exclusive full-screen applications and secure desktops (UAC, lock screen) are **not supported
  and were not tested**. On a capture error the previous snapshot stays.
- “Always on top” keeps the snapshot above normal windows; Windows still shows its own system UI (Start, task switcher) above it.
- Click-outside detection polls the mouse buttons every 40 ms while a snapshot is expanded; a click shorter than that can be
  missed (Esc, double-click and the hotkey always work).
- Hiding Backpack's display hides the snapshot; show it again with “Snapshot Overlay: show / hide”.
- The plugin page has no live demo scene for this plugin.

## Troubleshooting

| Problem | What to do |
|---|---|
| The capture hotkey does nothing | Check the plugin is on; on the plugin page the hotkey state may say *busy* (another program owns it) or *conflict* — assign another |
| Space does not expand | The pointer must be over the snapshot and no text field may be active; another program may own Space as a global hotkey; or the setting is off |
| Space is taken in my CAD program while the mouse rests on the snapshot | Turn off “Space over the snapshot…” and use Ctrl+Alt+Shift+E or double-click |
| The expanded snapshot collapses too early | Turn off “Collapse on a click outside or a window switch” |
| Double-click does nothing | “Clicks pass through the snapshot” is on, or double-click is turned off |
| The snapshot is blurry | It is shown larger than captured; capture a larger area, or turn off “Expanded snapshot may be larger than the original” |
| The snapshot disappeared after locking | “Keep the snapshot” is set to *Also clear on lock or sleep* |
| The snapshot went behind a window | “Always on top” is off |

## Testing

Automated (in `lab/tests/Backpack.Visual.Tests`, run by CI on Windows):

- `SnapshotGeometryTests` — pure geometry: limits, aspect, corner placement, 85–95 % expansion, resize, moves, DPI rescale,
  negative multi-monitor coordinates.
- `SnapshotPluginTests` — the plugin through the real SDK message loop: commands → messages, close, settings, reset, no image or
  coordinates in messages.
- `SnapshotOverlayTests` — manifest, Focus-style defaults, hotkey defaults and `whileTool` registration/release, the protocol
  boundary, saved place, and the snapshot in real host windows with a simulated pointer, mouse buttons, active window and capture:
  capture and replacement without Backpack's own windows, Esc cancel, double-click and Space expansion, exact collapse over 40
  cycles, automatic collapse, hide/show/clear, styles, and the tool engine (Esc order with Focus, lock, display hide, plugin off).

Manual checks on Windows 10/11 (real screen capture and input in other programs cannot run in CI):

1. 100 %, 125 %, 150 % and 175 % display scaling: capture text and check it is pixel-sharp at 1:1 and that the selection matches
   the dragged rectangle exactly.
2. Two monitors with different scales, one left of/above the primary (negative coordinates): capture on each, drag the snapshot
   across, expand on each.
3. A CAD program (or Blender/FreeCAD), a browser and a PDF viewer: capture a GPU-rendered viewport; keep working with the
   snapshot visible; confirm keyboard focus stays in the app and clicks outside the snapshot work.
4. Space: hover the snapshot and press Space (expands); move away and press Space in the CAD app (the app gets it); type in a
   text field with the pointer resting on the snapshot (the text gets the space).
5. Expand with each trigger; collapse with each trigger including click outside and Alt+Tab; repeat 20 times — the place and
   size must not change.
6. Replace while a snapshot is shown and partly inside the new area — the old snapshot must not appear in the new picture.
7. Lock and unlock Windows, sleep and wake, hide and show Backpack's display, turn the plugin off and on, exit Backpack.
8. Task Manager: memory returns after Clear and after several replacements of a full-screen 4K capture.
