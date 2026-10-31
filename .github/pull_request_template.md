## What and why

<!-- Short description. For a plugin catalog entry use the plugin-registry template instead:
     add ?template=plugin-registry.md to the pull request URL. -->

## Checks

- [ ] `dotnet build lab/KeyOverlay.Lab.slnx -c Release` and `dotnet test lab/KeyOverlay.Lab.slnx -c Release` pass.
- [ ] No personal data, typed text, screenshots of real desktops, logs with input or secrets are added.
- [ ] User-visible strings are in the resources (not hard-coded) and documented in the help if the behaviour changes.
- [ ] Licensing: my contribution to MPL 2.0 files is under MPL 2.0 (see `CONTRIBUTING.md`).
