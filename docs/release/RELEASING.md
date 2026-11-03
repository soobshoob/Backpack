# Releasing Backpack

Public repository: [soobshoob/Backpack](https://github.com/soobshoob/Backpack). The first prerelease, 0.4.0-preview.6,
imports the owner's verified local ZIP; its tag is excluded from automated release creation. Future tags use the
workflow described below. See [GITHUB-SETUP.md](GITHUB-SETUP.md) for the single publication path for each version.

## Before a release

1. Version in `lab/Directory.Build.props` (`VersionPrefix`, `VersionSuffix`; SemVer). Stable releases have no suffix.
2. Release notes in `docs/release/RELEASE-NOTES-<version>.md` (the packaging script copies them as `RELEASE-NOTES.md`).
3. Tests: `dotnet test lab/Backpack.slnx -c Release`.
4. Check that `LICENSE` is the unchanged MPL 2.0 text, that `lab/sdk/LICENSE` and `lab/samples/LICENSE` carry the terms the owner chose for the SDK and the plugins from the creator (the package check compares all SHA-256 hashes), and that `NOTICE` names the right copyright holder. Releases are built by `.github/workflows/release.yml` from a tag (see `docs/release/GITHUB-SETUP.md`).

## Build the package

```powershell
pwsh tools/package-release.ps1 -ReleaseDate 2026-10-29
pwsh tools/verify-package.ps1 -Zip artifacts/release/Backpack-<version>-win-x64.zip
```

Add `-UpdateRepository owner/repository` only when the release repository is known; without it the program shows “release source not configured” and the update check does not contact anything.

The output in `artifacts/release/`:

| File | Purpose |
|---|---|
| `Backpack-<version>-win-x64.zip` | the portable, self-contained package |
| `Backpack-<version>-win-x64-setup.exe` (+ `.sha256`) | the installer (Inno Setup 6), built from the same zip by `tools/build-installer.ps1` |
| `SHA256SUMS.txt` | SHA-256 of the zip |
| `Backpack-<version>-win-x64.files.sha256` | SHA-256 of every file in the package |

The package contains `Backpack.exe` with the .NET runtime, `LICENSE.txt`, `NOTICE.txt`, `README.txt`, `RELEASE-NOTES.md`, `THIRD-PARTY-NOTICES.md`, `LICENSE-STATUS.md` and `Licenses\` (third-party license texts). It never contains `.pdb` files, diagnostic exports, `references/`, `research/` or screenshots.

## Build the installer

```powershell
pwsh tools/build-installer.ps1 -Zip artifacts/release/Backpack-<version>-win-x64.zip -Verify
```

Requires Inno Setup 6 (`-InstallInnoSetup` installs it with Chocolatey, as on GitHub Actions). `-Verify` installs silently
into a temporary folder for the current user, checks the files and the bundled plugins, and uninstalls again. The installer
is per-user by default (no administrator rights), keeps `%APPDATA%\Backpack` on update and uninstall, replaces `Plugins\`
and `Samples\` on update, and removes the autostart entry only if it points to the installed copy. Every pull request
builds and verifies the zip and the installer in CI (artifact `installer-<version>-unreleased`, not published).

## GitHub Releases structure

| Item | Value |
|---|---|
| Tag | `v<version>`, for example `v0.3.0-preview.2` or `v0.3.0` |
| Title | `Backpack <version>` |
| Pre-release flag | set for versions with a suffix (`-preview.N`); the program's Preview channel sees them, Stable does not |
| Body | the release notes for this version |
| Assets | `Backpack-<version>-win-x64-setup.exe`, `Backpack-<version>-win-x64-setup.exe.sha256`, `Backpack-<version>-win-x64.zip`, `SHA256SUMS.txt` |

The update check in the program reads the public list of releases of the configured repository, compares SemVer versions and links to the release page. It never downloads or runs anything.

## Not done automatically

- Code signing (the package is unsigned until the owner provides a certificate).
- Creating the tag. The release itself is created by `release.yml` from the tag and published directly (from 0.4.0-preview.8;
  set the repository variable `RELEASE_AS_DRAFT=true` to get a draft for manual review instead). Existing releases are
  never overwritten.

## Current package and desktop verification

The package includes SDK contract 1.2, `PLUGINS-TRUST.md`, six ready plugins in `Plugins/`, their hashed delivery index
`Plugins/bundled.json`, sources in `Samples/`, and the separate PolyForm Strict license for SDK/plugins.
Run `pwsh tools/verify-package.ps1 -Zip <zip> -SelfTestUi` in a suitable interactive Windows session with isolated settings.
Even verification without `-SelfTestUi` launches WPF windows; hosted runner behavior must be checked, not assumed.
Keep all previous packages and their hashes unchanged. SDK/plugins retain their current terms until explicitly changed
by the owner; see LICENSE-STATUS.md.
