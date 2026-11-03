# Backpack plugins: SDK 1.3 and the plugins from the creator

A Backpack plugin is a .NET 10 class library that references `Backpack.Sdk` and contains one class derived from
`BackpackPlugin`. Backpack runs it in a separate process (`Backpack.exe --plugin-host`) and talks to it through a
limited JSON protocol. A separate process is **not a sandbox**: see `ADR-0001-plugin-trust.md` (in the package:
`PLUGINS-TRUST.md`).

## Plugins from the creator (sources are the samples)

| Folder | Plugin ID | Shows |
|---|---|---|
| `Drawing` | `com.example.drawing` | `overlay.draw` (SDK 1.2): the host drawing canvas; `whileTool` commands (F2, undo, tools); remembering the last pen with `UpdateSettings`; `drawing` preview scene |
| `LayoutIndicator` | `com.example.layout-indicator` | `input.layout` + `overlay.indicator` (SDK 1.2): layout change events, filtering by reason, a host-drawn indicator; `indicator` preview scene |
| `CursorZoom` | `com.example.cursor-zoom` | `overlay.magnifier`; a hold command (`"hold": true`) and two toggles that replace each other |
| `Lens` | `com.example.lens` | `overlay.magnifier` in `region` mode: a movable lens with a separate source area |
| `Focus` | `com.example.focus` | `overlay.dim`: dimming around an area the user selects |
| `PresentationTimer` | `com.example.presentation-timer` | `overlay.panel` with buttons, states, a movable panel; a clock-based countdown |
| `SnapshotOverlay` | `com.example.snapshot-overlay` | `overlay.snapshot` (SDK 1.3): a still snapshot of a selected area in a floating window on top, temporary expansion, `whileTool` commands, `UpdateSettings` for “reset” — see [snapshot-overlay.md](snapshot-overlay.md) |

In the repository the sources are in `lab/samples`; in the Backpack package they are in `Samples\` and the ready
packages are in `Plugins\` together with the delivery index `bundled.json` (file, plugin ID, version, SHA-256 of each
package). On start Backpack adds every package that matches the index to the catalog **turned off**, without running
its code, and marks it **From the creator**. The mark comes from this delivery check, not from the author name in the
manifest, and it is not a cryptographic publisher signature. A plugin from the creator that you removed is not added
back by itself (Plugins → “Add back”). An update delivered with a new Backpack keeps your on/off choice; a package you
import yourself is turned off until you turn it on.

The samples carry a `legacy` block that moves settings, hotkeys and the menu item of the former built-in tools (drawing,
layout indicator, magnifier, lens, focus) when Backpack is updated — Backpack itself has no code for these plugin IDs.

## Building and packing

Repository:

```powershell
dotnet build lab/samples/Drawing/Drawing.csproj -c Release
powershell -ExecutionPolicy Bypass -File tools/pack-plugin.ps1 -Project lab/samples/Drawing/Drawing.csproj -Out artifacts/plugins
powershell -ExecutionPolicy Bypass -File tools/write-bundled-index.ps1 -Dir artifacts/plugins   # only for a delivery folder
```

Backpack package (`Samples\<Name>\<Name>.csproj` references `..\..\Backpack.Sdk.dll` next to `Backpack.exe`):

```powershell
dotnet build Samples\Drawing\Drawing.csproj -c Release
```

Then zip `manifest.json` and `lib\<Name>.dll` (plus `LICENSE.txt` and files listed in `resources`) into
`<id>-<version>.bppkg`. `Backpack.Sdk.dll` is never put into a package: Backpack provides it.

## Importing

Backpack → **Plugins** → **Import a plugin** → choose the `.bppkg` file. Import checks the package and installs it
**turned off**; it never runs code. Turning it on asks you to confirm trust the first time.

## SDK contract

| Member | Since | Meaning |
|---|---|---|
| `OnStart()` / `OnStop()` | 1.0 | the plugin is turned on / off (also when Backpack exits) |
| `OnCommand(id)` | 1.0 | a command from the manifest was run (hotkey, menu, tray, radial menu) |
| `OnCommandReleased(id)` | 1.1 | the hotkey of a `"hold": true` command was released (the host polls it, at most 5 minutes) |
| `OnSettingsChanged()` | 1.0 | values in `Context.Settings` changed (the settings page is drawn by Backpack from the manifest) |
| `OnToolClosed(id, reason)` | 1.1 | a host tool closed: `cancelled`, `escape`, `system`, `host`, `busy` (canvas used by another plugin), `unavailable`; SDK 1.3 snapshot also `cleared` (the user removed it with its button) |
| `OnPanelButton(id)` | 1.1 | a panel button was pressed |
| `OnLayoutChanged(e)` | 1.2 | the keyboard layout of the active window changed (`input.layout`): code, name, reason `switch` or `window` |
| `OnToolState(id, values)` | 1.2 | the user changed the open tool itself (drawing: tool, colour, thickness, stroke, toolbar place) |
| `Context.OpenMagnifier(id, request)` | 1.1 | magnifier drawn by the host: `follow`, `pinned` or `region` (`overlay.magnifier`) |
| `Context.OpenDim(id, request)` | 1.1 | dimming around an area the user selects (`overlay.dim`) |
| `Context.OpenDrawing(id, request)` | 1.2 | the drawing canvas drawn by the host (`overlay.draw`); a repeat updates the open canvas, drawings stay |
| `Context.DrawAction(id, action)` | 1.2 | `undo`, `redo`, `clear`, `screenshot`, `radial`, `markers.reset`, `tool:<name>` |
| `Context.ShowIndicator(request)` / `HideIndicator()` | 1.2 | the indicator drawn by the host (`overlay.indicator`): code, second line, duration, animation, place, look |
| `Context.UpdateSettings(values)` | 1.2 | write your own setting values (checked against the manifest schema) |
| `Context.OpenSnapshot(id, request)` | 1.3 | a still snapshot drawn by the host (`overlay.snapshot`): a new ID starts the area selection, a repeat updates the parameters of the open snapshot |
| `Context.SnapshotAction(id, action)` | 1.3 | `capture` (replace), `show`, `hide`, `toggleVisible`, `expand`, `collapse`, `toggleExpand`, `resetPlace` (also without an open snapshot) |
| `Context.EditTool(id)` / `CloseTool(id)` | 1.1 | show the controls of a tool or panel without hover / close it |
| `Context.ShowPanel(...)` / `HidePanel()` | 1.0/1.1 | host-drawn panel: title, text, progress, buttons, state, accent, movable (`overlay.panel`) |

**What a plugin never receives:** screen images (the `screen` capability is declared but not granted; an `overlay.snapshot` picture stays in Backpack's memory and is never sent to the plugin or written to disk), drawings, pointer
coordinates, input hidden by dynamic privacy, raw keyboard hooks, field contents, window titles. `input.layout` sends
only the layout code and name — no key presses. Starting Backpack, unlocking and waking from sleep are not layout
changes. Tool windows, their hover controls (`HoverReveal`), Esc handling and closing on lock, disable, removal or a
crash are all done by the host. At most 4 tools per plugin and one drawing canvas at a time; every message is checked
and numbers are clamped to their ranges.

Manifest additions:

- SDK 1.1: `"hold": true` on a command; for settings `group`, `hint`, `step`, `unit`, `showWhen`, `choiceTitles`, type
  `color`; `preview` (a host-drawn demo scene with `bind` from scene parameters to setting keys); `legacy`.
- SDK 1.2: on a command `"whileTool": true` (its hotkey is registered only while a tool of this plugin is open — e.g. F2
  for a screenshot, outside drawing the key stays with other apps), `"hotkey"` (default hotkey; a busy or reserved one is
  shown as a conflict), `"icon"` (Lucide icon for menus); on a setting `"hidden": true` (stored and checked, not shown —
  for values the plugin writes itself); preview scenes `drawing` and `indicator`.

- SDK 1.3: capability `overlay.snapshot` (`SnapshotRequest`, `SnapshotActions`). `SettingValues.Number` also reads values the plugin
  itself wrote as integers through `UpdateSettings`.

- Backpack 0.4.0-preview.6 (no SDK change): top-level `"symbol"` — the plugin icon in the side menu (compact and full),
  on the plugin card and in the page header, for example `"symbol": "pen-tool"`. It is the **name** of an icon from the
  Lucide catalog built into Backpack (lower-case letters, digits and hyphens, up to 40 characters; the catalog with
  search words is `PluginSymbols.Catalog`, the icon set is listed in `tools/gen-lucide.py`). XAML, file paths and URLs are
  not accepted, and the icon is shown without running plugin code. A name this Backpack version does not have, or no
  `symbol` at all, shows `puzzle`; `star` is not available because the star means “in favourites”. Older Backpack
  versions ignore the field. The user can pick another icon from the same catalog (plugin page → **Display** → **Icon**)
  and go back to yours; the choice is stored by plugin ID in `plugins.json`, outside the package, and survives updates.
  A PNG `"icon"` (SDK 1.1 card picture) is still shown for plugins without `"symbol"`.

Icons of the plugins from the creator: Drawing `pen-tool`, Layout indicator `globe`, Lens `scan-search`, Focus `focus`,
Cursor magnifier `zoom-in`, Presentation timer `timer`, Snapshot overlay `image`.

A plugin built for SDK 1.0, 1.1 or 1.2 keeps working; a plugin that needs 1.3 is rejected by an older Backpack.

## Limits on using the SDK and the samples

The Backpack core is under the Mozilla Public License 2.0 from 0.4.0-preview.6, but that does **not** cover the SDK or
the plugins from the creator: they have their own `LICENSE` files (`lab/sdk/LICENSE`, `lab/samples/LICENSE`, `LICENSE.txt`
in each package). Until the owner decides on their terms (proposal: MIT for the SDK, PolyForm Perimeter 1.0.0 for the
plugins from the creator — `docs/release/LICENSING-PROPOSAL.md`), both are under the **PolyForm Strict License 1.0.0**:
you may read, build and use them for noncommercial purposes, but the license does not permit changing them or
distributing changed copies. A plugin you write yourself is your own code under your own license (put it in the
manifest `license` field and ship `LICENSE.txt` in the package). This document does not change any license.
