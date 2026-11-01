Backpack {VERSION} — Windows x64 (portable)
Release date: {DATE}

Backpack is a toolkit for clear presentations, tutorial videos and screen demos: pressed keys and
mouse buttons on screen, profiles and styles, and plugins from the creator (drawing, layout indicator,
lens, focus, cursor magnifier and presentation timer).

RUN
  1. Unpack the folder anywhere (for example, your Downloads or C:\Program Files\Backpack).
     Administrator rights are not needed.
  2. Run Backpack.exe. Nothing else has to be installed: the .NET runtime is inside the folder.
  3. The main window opens; Backpack also has an icon in the notification area.
     Closing the main window keeps Backpack in the tray. Exit: tray icon menu -> "Exit".
     Starting Backpack.exe again while it runs opens the window of the running copy.
  Options: Backpack.exe --minimized    (start in the tray only)
           Backpack.exe --no-plugins   (safe start: no plugins are started)
           Backpack.exe --settings="D:\path\settings.json"   (use another settings file)

  START WITH WINDOWS: "App settings" -> "Start with Windows". Backpack then starts in the tray when you sign
  in (one value in HKCU\Software\Microsoft\Windows\CurrentVersion\Run, no administrator rights; also for the
  portable version). Turn it off before you move or delete the folder.

  The package is NOT digitally signed. Windows SmartScreen or antivirus software may show a warning
  for an unsigned program; check the SHA-256 checksum before running.

SETTINGS
  %APPDATA%\Backpack (settings.json, app.json, profiles\, styles\, plugins\).
  Portable mode: an empty file Backpack.portable next to Backpack.exe; settings are then kept in the Data
  folder next to the program, if it can be written.
  A new installation uses the Backpack Neon Emerald look (Emerald colours, Neon surfaces, light or dark as in
  Windows, Spectral font, Neon Emerald keycaps and mouse). Settings of 0.4.0-preview.6 and earlier are not read
  by this version; they are kept as *.unreadable.bak.json backups.

PLUGINS
  "Plugins" -> "Import a plugin" installs a .bppkg package without running it. A plugin is a program
  that runs in its own process with your user rights; Backpack asks you to confirm trust before the
  first start and cannot protect you from harmful code. See PLUGINS-TRUST.md and the help (F1).
  Plugins\ contains the six plugins from the creator: drawing, layout indicator, lens, focus, cursor
  magnifier and presentation timer. They are already in the catalog on the Plugins page and are off until
  you turn them on. Samples\ contains their sources and README.md (SDK 1.2, building, importing); their
  terms are in Samples\LICENSE.txt (see LICENSES below).

UPDATE
  Exit Backpack, replace this folder with the new one and start Backpack.exe again.
  Backpack never downloads or runs updates by itself.

REMOVE
  Turn off "Start with Windows" if it is on, exit Backpack and delete this folder. To remove settings too,
  delete %APPDATA%\Backpack. Backpack writes to the registry only for "Start with Windows".

PRIVACY
  Key presses are shown on screen and kept only in memory. Dynamic privacy hides input while the focus is
  in a field that reports itself as a password field (UI Automation IsPassword) or in an app from your
  list; it reads no field contents. Windows has no universal way to recognize password fields: an app that
  does not report it correctly cannot be detected automatically. Settings files contain options only: no
  typed text, key codes, key sequences, pointer positions or screen content. Focus, the magnifier and
  drawing save nothing; a screenshot is saved only when you take one.

LICENSES
  Backpack core: Mozilla Public License 2.0 — full text in LICENSE.txt, copyright notice in NOTICE.txt
  (https://www.mozilla.org/MPL/2.0/). In short: you may use, change and pass on Backpack, also
  commercially; changed MPL files stay under the MPL with their source available. The LICENSE.txt text is
  what applies. Earlier versions (up to 0.4.0-preview.5) were released under the PolyForm Strict License.
  The plugin SDK (Backpack.Sdk.dll) and the plugins from the creator (Plugins, Samples) have separate terms:
  PolyForm Strict License 1.0.0 until the author decides (Licenses\Backpack.Sdk-LICENSE.txt,
  Samples\LICENSE.txt, LICENSE.txt inside each plugin package; details in LICENSE-STATUS.md).
  Third-party components: THIRD-PARTY-NOTICES.md and the Licenses folder.
  Author: soobshoob — https://github.com/soobshoob
