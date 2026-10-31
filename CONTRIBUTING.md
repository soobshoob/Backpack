# Contributing to Backpack

Thank you for helping. Backpack is a Windows desktop app in C# / .NET 10 / WPF.

## Before you start

- For a larger change, open an issue first so the approach can be agreed.
- To list your plugin in the catalog, see [registry/README.md](registry/README.md) — that is a separate kind of pull request.
- Security problems: do not open a public issue; see [SECURITY.md](SECURITY.md).

## Build and test

```powershell
dotnet build lab/KeyOverlay.Lab.slnx -c Release
dotnet test  lab/KeyOverlay.Lab.slnx -c Release
```

Warnings are errors. UI tests open windows off screen; run them on an interactive Windows desktop. The app has a lab mode
for diagnostics (`Backpack.exe --lab`, see `lab/README.md`); the long display check is `--lab --soak=<minutes>` with a
separate `--settings` file.

## Rules

- **Privacy first.** Never record typed text, key sequences, clipboard, window titles or screen content — not in logs,
  diagnostics, tests or screenshots. Synthetic input in tests goes only to the test's own window while it is in front.
- Keep changes focused; match the surrounding code style and comment density. Comments in the code base are Russian.
- User-visible text goes into `lab/src/KeyOverlay.Visual/Localization/Strings*.resx` for **every** supported language;
  do not leave English in another language file. Stable IDs, API names and settings keys are not translated.
- New behaviour needs tests; a fixed bug needs a regression test.
- Do not commit build output, packages, personal settings, diagnostics exports or screenshots of your real desktop.

## License of contributions

- Files of the Backpack core are under the Mozilla Public License 2.0 ([LICENSING.md](LICENSING.md)). By submitting a
  change to such a file you agree that it is licensed under MPL 2.0.
- `lab/sdk` and `lab/samples` have separate terms (currently PolyForm Strict 1.0.0, decision pending — see
  [LICENSING-PROPOSAL](docs/release/LICENSING-PROPOSAL.md)). Changes there are accepted only after the owner confirms the
  terms; please ask in an issue first.
- Do not add third-party code, fonts, icons or images without their license and an entry in
  `docs/release/THIRD-PARTY-NOTICES.md`.
