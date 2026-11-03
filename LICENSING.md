# Licensing of this repository

This file says which license applies to which part of the repository. The license texts themselves are unchanged
official texts; nothing in this file adds to or changes them.

| Part | Path | License | Text |
|---|---|---|---|
| **Backpack core** — the application, its libraries, build and packaging scripts, tests, JSON schemas and the help pages compiled into the program | `lab/src/`, `lab/tests/`, `lab/Directory.Build.props`, `lab/Backpack.slnx`, `tools/`, `docs/help/`, `docs/settings/`, `docs/themes/`, `docs/plugins/*.schema.json`, `docs/release/`, `.github/` | [Mozilla Public License 2.0](https://www.mozilla.org/MPL/2.0/) | [`LICENSE`](LICENSE) |
| **Plugin SDK** (`Backpack.Sdk`) | `lab/sdk/` | PolyForm Strict License 1.0.0 — kept until the owner decides (see below) | [`lab/sdk/LICENSE`](lab/sdk/LICENSE) |
| **Plugins from the creator** (Drawing, Layout indicator, Lens, Focus, Cursor magnifier, Presentation timer, Snapshot overlay) | `lab/samples/` | PolyForm Strict License 1.0.0 — kept until the owner decides (see below) | [`lab/samples/LICENSE`](lab/samples/LICENSE), `LICENSE.txt` in each plugin folder and package |
| **Plugins by other authors** | outside this repository | each plugin's own license | the plugin's package |
| **Third-party components** — Inter and Spectral fonts, Lucide icons (including the backpack mark), .NET runtime | see the notices | their own licenses (SIL OFL 1.1, ISC/MIT, MIT) | [`docs/release/THIRD-PARTY-NOTICES.md`](docs/release/THIRD-PARTY-NOTICES.md), `Licenses\` in the package |
| **Website** | `Website/` | not covered by this file; the website project has its own notices | `Website/THIRD-PARTY-NOTICES.md` |

Material that is not published with the repository (see `docs/release/REPOSITORY-CONTENTS.md`) — `references/`,
`research/`, `TEMP/`, `artifacts/`, local diagnostics — is not licensed by this file.

## Since when

- **0.4.0-preview.6** is the first version whose core is released under MPL 2.0.
- Packages released earlier (0.3.0-preview.1 … 0.4.0-preview.5) were published under the PolyForm Strict License 1.0.0,
  and their `LICENSE.txt` still says so. Changing the license of the repository does not automatically change the terms
  of copies people already received; those packages and their checksums are left as they were.

## What MPL 2.0 means for the core, in short

Orientation only; the text in `LICENSE` is what applies. MPL 2.0 is a file-level copyleft license: you may use, copy,
modify and distribute the core, also commercially. If you distribute a modified version of an MPL-covered file, that file
must remain under MPL 2.0 and its source must be available. Your own separate files can be under other terms, including
proprietary ones (a "Larger Work"). MPL 2.0 itself has no restriction on selling.

## SDK and plugins from the creator

The owner intends different terms for the SDK and for the six plugins from the creator than for the core. The proposal
is in [`docs/release/LICENSING-PROPOSAL.md`](docs/release/LICENSING-PROPOSAL.md). Until the owner confirms it, these parts
stay under the PolyForm Strict License 1.0.0, recorded in their own `LICENSE` files so that the MPL text in the root does
not relicense them by accident.

## Trademarks

No license here covers the name "Backpack" or the way it is presented as a product name.
