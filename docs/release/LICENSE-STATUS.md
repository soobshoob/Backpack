# Backpack license status

**From 0.4.0-preview.6 the Backpack core is licensed under the [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/).**
The plugin SDK and the plugins from the creator are licensed separately (see below).

- `LICENSE` (repository root; `LICENSE.txt` in the program package) is the official MPL 2.0 text, unchanged, as published
  at <https://www.mozilla.org/media/MPL/2.0/index.txt> (identical to the SPDX `MPL-2.0` text apart from trailing whitespace),
  SHA-256 `3f3d9e0024b1921b067d6f7f88deb4a60cbe7a78e76c64e3f1d7fc3b779b9d04`. `tools/verify-package.ps1` checks this hash.
- `NOTICE` (`NOTICE.txt` in the package): `Copyright soobshoob (https://github.com/soobshoob)`, the license of each part.
- Which files the MPL covers: `LICENSING.md` in the repository. In short: the application, its libraries, scripts, tests,
  schemas and the built-in help. No extra terms are added to the MPL, in the files or in the program.

## Earlier packages

0.3.0-preview.1 … 0.4.0-preview.5 were released under the [PolyForm Strict License 1.0.0](https://polyformproject.org/licenses/strict/1.0.0)
(SHA-256 of that text `9eb48619fbc193ab7bb327b090cfcc703000265b83e670f81f231d0b1c43c56e`). Those archives and their
checksums are not changed, and the new license is not claimed for copies already received.

## What MPL 2.0 allows, in short

Orientation only; the text in `LICENSE` applies.

- Use, copy, change and distribute the core, for any purpose, including commercial use.
- A distributed modified MPL file stays under MPL 2.0, with its source available to recipients (file-level copyleft).
- Combining with separate files under other terms (a "Larger Work") is allowed.
- No trademark rights; no warranty.

## Plugin SDK and plugins from the creator

| Part | Now | Proposed (owner decision pending) |
|---|---|---|
| `Backpack.Sdk` (`lab/sdk`, `Backpack.Sdk.dll` in the package) | PolyForm Strict 1.0.0 (`lab/sdk/LICENSE`) | MIT |
| Six plugins from the creator (`lab/samples`, `Samples\`, `Plugins\*.bppkg`) | PolyForm Strict 1.0.0 (`LICENSE.txt` in each package) | PolyForm Perimeter 1.0.0 |

Reasons, the rights each option gives and the boundary between an independent plugin and a derived copy are in
[`LICENSING-PROPOSAL.md`](LICENSING-PROPOSAL.md). PolyForm Strict allows only noncommercial use and does not allow changes
or distribution — until the decision, third parties cannot lawfully build on the SDK or adapt the sample plugins.

## Third-party components

Third-party components keep their own licenses and are not relicensed: see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
The package ships their license texts in `Licenses\`.

| Component | License | Distributed | Compatible with shipping inside Backpack |
|---|---|---|---|
| Inter 4.1 (font) | SIL OFL 1.1 | yes, embedded | yes: OFL allows bundling with software; the license text is included |
| Spectral (font) | SIL OFL 1.1 | yes, embedded | yes: license text included |
| Lucide 1.59.0 icons, including the `backpack` icon used as the Backpack mark (from lucide-react 0.468.0, same geometry), some derived from Feather | ISC; Feather icons — MIT | yes, path data compiled in | yes: permissive; both notices are included (THIRD-PARTY-NOTICES.md, Licenses/Lucide-LICENSE.txt) |
| .NET 10 runtime, WPF, Windows Forms | MIT (+ .NET third-party notices) | yes, self-contained | yes: license and notices are included |
| xunit, Microsoft.NET.Test.Sdk, JsonSchema.Net | Apache-2.0 / MIT | no (tests only) | not shipped |
| Keyviz | GPL-3.0 | no | no code, data or images are copied |

MPL 2.0 is compatible with shipping these components: their licenses are permissive (OFL for the fonts) and their files
are not MPL files. Keycap and mouse renderers, help images and review sheets are rendered by Backpack's own code.

## Still to be decided by the owner

1. **Terms for the SDK and the plugins from the creator** — `LICENSING-PROPOSAL.md`.
2. **Copyright holder name.** `soobshoob` is used in `NOTICE` and the assembly metadata. If the legal holder is a different
   person or entity, replace it there; the license texts stay unchanged.
3. **Repository for releases and updates** (`owner/repository`), **support addresses**, the published plugin registry URL —
   see `docs/release/GITHUB-SETUP.md`.
4. **Code signing**: packages are unsigned; Windows may warn about them.
