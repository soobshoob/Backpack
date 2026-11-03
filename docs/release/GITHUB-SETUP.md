# GitHub publication setup

Repository: [soobshoob/Backpack](https://github.com/soobshoob/Backpack), public, default branch `main`.
The owner authorized source publication and prerelease 0.4.0-preview.6 on 2026-11-01. Website deployment is excluded.

## First prerelease: one publication path

`v0.4.0-preview.6` imports the already verified local `Backpack-0.4.0-preview.6-win-x64.zip` without rebuilding it.
Its SHA-256 is `29e8212f8deb17be05b5c8b5be478e40c3c20a90c50a4d25eb2fcd6c047606ce`.
Publish it manually with `SHA256SUMS.txt` containing that ZIP's exact name and with the current release notes.
Keep `prerelease=true`; it is unsigned and the reported display stall has not been reproduced or declared fixed.
The package was built on 2026-10-31 and has no configured update repository. Do not rewrite it just to change metadata.

`release.yml` explicitly skips its build/publish jobs for this first tag, preventing a duplicate release or a different ZIP.
Publish the tag with a normal push, never force. Verify source CI first, then verify the published assets by downloading
the ZIP again and comparing SHA-256 with the local original and the uploaded checksum file.

## Subsequent releases

0.4.0-preview.7 is the first version released through this path: the tag builds a draft on GitHub Actions, the owner's
local package of the same commit is checked with `verify-package.ps1 -SelfTestUi` on a Windows desktop, and the draft is
published once after its ZIP and `SHA256SUMS.txt` were downloaded and compared.

For subsequent `v*` tags, `release.yml` checks that the tag matches `lab/Directory.Build.props`, builds/tests/packages on
Windows, verifies the package, builds and silently installs/uninstalls the installer, and creates the release with the
installer, the zip and their checksums. From 0.4.0-preview.8 (owner's authorization) the release is **published directly**;
the repository variable `RELEASE_AS_DRAFT=true` restores the draft-then-review path described below for earlier versions.
Up to 0.4.0-preview.7 the workflow created a **draft** release. The build has read-only permissions; only the publish job
has `contents: write`. Existing releases are not overwritten. Review the draft and publish it once; do not also run a
separate manual release creation for that tag. A preview suffix always means prerelease, never stable.

The `release` environment can require owner review and allow only release tags. Configure it before using the automated
path, without adding paid services or credentials. No signing certificate is purchased; packages remain unsigned.
Interactive UI, input/privacy, hover, multi-monitor/DPI and hardware Fn checks need a suitable Windows desktop and cannot
be inferred from a successful ordinary hosted-runner build.

## Repository settings

- Keep Actions default workflow permissions **read-only**; leave automated PR approval and auto-merge disabled.
- After initial push and successful CI, protect main against force push/deletion and use PRs for further work. Select
  required checks only from actual successful runs. Do not require a second person's approval for the sole owner's own
  PRs; review third-party catalog submissions manually.
- Restrict creation of future release tags to the owner when configuring tag rulesets.
- `ci.yml` validates source on main pushes/PRs. `registry.yml` validates catalog JSON using trusted base tooling and never
  executes plugin code or receives publication permissions/secrets.

## Non-blocking external setup

The owner currently keeps SDK and creator plugins under **PolyForm Strict 1.0.0**. The licensing proposal is a proposal,
not permission to change them. The core is MPL 2.0. Preserve third-party licenses and notices.

Domain/hosting, Microsoft Store, Monobank and Buy Me a Coffee URLs are not supplied and must not be invented.
`FUNDING.yml.example` stays inactive. The empty `registry/index.json` is a schema-valid placeholder with no approved
packages; do not advertise a populated marketplace or activate website downloads/catalog links without checking them.
The website source is retained for its shared validation contract; publishing this Git repository does not deploy it.
