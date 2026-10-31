# Backpack plugin catalog

`registry/index.json` is the list of plugins shown in the catalog on the Backpack website. It is the same format the
website reads (contract v1: `Website/REGISTRY.md`, schema `registry/schema/registry-v1.schema.json`, an exact copy of
`Website/public/registry.schema.json`). Entries appear only after the owner reviews and merges a pull request.

The catalog is **empty** until the first entries are approved. `registry/examples/` contains test data only (reserved
`example.com` addresses, fixture packages): it is not the published catalog.

## Add your plugin

1. Publish your `.bppkg` at a public HTTPS address you control — a direct link to the file (`"kind": "package"`) or a
   release page (`"kind": "release"`). The package must contain `manifest.json` and should contain `LICENSE.txt`.
2. Prepare the entry with the helper on the website (Plugins → Publish) or by hand. Required fields: `id` (the same as in
   your manifest), `slug`, `name`, `author`, `summary`, `description` (at least English), `version` (as in the manifest),
   `category`, `tags`, `distribution`, `compatibility` (`minBackpack`, `sdk`, `platforms`), `license` (**your plugin's own
   license** — not the Backpack core license). For a direct package add `sha256` of exactly that file:
   `(Get-FileHash plugin.bppkg -Algorithm SHA256).Hash.ToLower()`.
3. Add the entry to the `plugins` array of `registry/index.json` without changing other entries, and open a pull request
   using the **Plugin registry** template (`?template=plugin-registry.md`). Change only `registry/index.json`.
4. Automatic checks run (they never run your plugin): the JSON schema, the website's semantic validator (unique ID and
   slug, SemVer, HTTPS addresses, package vs release rules), and for direct packages a download up to 25 MiB, the SHA-256,
   ZIP limits (entries, unpacked size, compression ratio), safe paths inside the archive, and the manifest ID and version.
5. The owner reviews the entry, the package and the license and merges — or asks for changes. **Passing the checks does
   not mean a plugin is safe**, and an open pull request does not mean the plugin is published.

Check locally before opening the PR (PowerShell 7.4+; Node 22 and the website dependencies for the second command):

```powershell
pwsh tools/registry/Test-RegistryPackages.ps1 -Index registry/index.json
node tools/registry/validate-registry.mjs Website registry/index.json
```

## After merge — how the website gets the update

The website reads `index.json` from the public read-only address set in `Website/src/integrations/settings.json`
(`registry.url`, for example the raw GitHub URL of `registry/index.json` on the default branch). After a merge nothing is
rebuilt and no website component is edited: the catalog and the plugin pages fetch the file again when their cache
(5 minutes by default) expires or when a visitor presses “check now”. New slugs are served by the generic plugin route
(`Website/HOSTING.md`). Already open tabs are not updated instantly.
