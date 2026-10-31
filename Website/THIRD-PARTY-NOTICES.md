# Website resources

- **Inter 4.1**, Rasmus Andersson, SIL Open Font License 1.1. The three unmodified TTF files are copies of `lab/src/KeyOverlay.Visual/Fonts/`. Full notice: `public/licenses/Inter-OFL.txt`. Font identification is a design choice, not a claim to identify the reference typeface.
- **Lucide React 0.468.0**, Lucide Contributors (and Feather contributors). ISC; full supplied notice: `public/licenses/Lucide.txt`. Icons include the outline Backpack used by this website. The custom favicon follows the same outline idea and was authored for this site; no raster logo was extracted.
- **React / React DOM 19.1.1**, Meta Platforms, Inc. and affiliates. MIT; full notices in `public/licenses/React.txt` and `React-DOM.txt`.
- **Vite 6.4.1**, MIT, and **TypeScript 5.9.3**, Apache-2.0: development tooling, not shipped as application runtimes. Installed package licenses remain in node_modules.
- Interface illustration, CSS keycaps, diagrams and social preview SVG were authored for this website from the supplied concept. They are illustrative and do not claim to be a shipping Backpack UI. Social PNG is a raster export of the SVG.
- `public/release-notes.txt` is an unchanged copy of the verified Phase 3B release notes. `public/licenses/PolyForm-Strict.txt` is an unchanged copy of the application LICENSE. Copyright soobshoob (https://github.com/soobshoob). This website does not distribute the application or change its license.

The application is licensed under **PolyForm Strict 1.0.0**, not an open-source license. The license texts for third-party resources are separate and retain their original terms.

## Spectral (current site typography)
Spectral by The Spectral Project Authors / Production Type, SIL Open Font License 1.1. Downloaded from the official Google Fonts repository commit 8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5, first into references/fonts/Spectral and then copied separately into Website/public/fonts at the owner's request. Regular 400, SemiBold 600, Bold 700, Italic 400. License: public/licenses/Spectral-OFL.txt. Provenance and SHA-256 of all four identical copies: qa/v101/font-provenance.json. Spectral is used for large display headings, Backpack branding and keycap labels. Body text, controls and field labels use the system UI sans-serif stack after the product-model revision. Inter files from the first iteration are retained but are not the current UI font.

Playwright 1.62.1 (Microsoft Corporation, Apache-2.0) is development-only browser test tooling. It is not bundled into the public site. The tests use the locally installed Chrome with a separate disposable profile.

## Noto Serif (Greek fallback)
Noto Serif, The Noto Project Authors, SIL Open Font License 1.1. Official Google Fonts stylesheet: https://fonts.googleapis.com/css2?family=Noto+Serif:wght@400;600;700&display=swap. The unmodified 400, 600 and 700 TTF files were downloaded to references/fonts/NotoSerif first, then separately copied to public/fonts. The original stylesheet URLs and license are retained with the references; the website license is public/licenses/NotoSerif-OFL.txt. Greek and Greek Extended Unicode ranges only are used as a fallback for characters absent from Spectral. No request is made to Google Fonts by the running website. Identical-copy SHA-256 checks: qa/languages/font-provenance.json.
