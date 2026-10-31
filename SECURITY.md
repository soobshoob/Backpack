# Security policy

## Reporting a vulnerability

Please **do not** report security problems in public issues or pull requests.

Use GitHub's private vulnerability reporting for this repository (**Security → Report a vulnerability**). If it is not
available, contact the author through the profile <https://github.com/soobshoob> and ask for a private channel; do not
include details in the first public message.

Include the Backpack version (About page), Windows version, steps to reproduce and the impact. Do not include personal
data, typed text or screenshots of your real desktop; use test data.

You will get an answer as soon as the author can review it. There is no bug bounty.

## Scope

In scope: the Backpack application and its plugin host (`lab/src`), the plugin protocol and SDK, the packaging and release
scripts, the catalog validation (`tools/registry`, `.github/workflows`).

Things that are documented limits rather than vulnerabilities:

- Plugins are programs that run with your user rights in a separate process; a separate process is not a sandbox
  ([ADR-0001](docs/plugins/ADR-0001-plugin-trust.md)). Turn on only plugins you trust.
- Dynamic privacy relies on apps reporting password fields through UI Automation; Windows has no universal way to detect
  them (see the help).
- Release packages are not code-signed yet.

## Supported versions

Only the latest published version receives fixes.
