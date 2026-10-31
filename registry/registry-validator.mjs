// Website/src/integrations/settings.json
var settings_default = {
  store: { enabled: true, published: false, url: null },
  github: { owner: null, repository: null, artifact: { filenameTemplate: "Backpack-{version}-win-x64.zip", platform: "Windows 10 / 11", architecture: "x64", packageType: "portable-zip", checksumFilename: "SHA256SUMS.txt" }, cacheMinutes: 10 },
  registry: { url: null, catalogGithubUrl: null, contributeUrl: null, cacheMinutes: 5 },
  support: { monobank: { enabled: true, url: null }, buyMeACoffee: { enabled: true, url: null } },
  licensing: {
    releases: { "0.4.0-preview.4": { name: "PolyForm Strict 1.0.0", url: "https://polyformproject.org/licenses/strict/1.0.0", signature: "unsigned" } },
    sdk: { name: "PolyForm Strict 1.0.0", url: "https://polyformproject.org/licenses/strict/1.0.0", decisionPending: true },
    examples: { name: "PolyForm Strict 1.0.0", url: "https://polyformproject.org/licenses/strict/1.0.0", decisionPending: true }
  }
};

// Website/src/integrations/validation.ts
var versionPattern = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$/;
function safeHttps(value) {
  if (typeof value !== "string" || value.length > 2048) return false;
  try {
    const u = new URL(value), h = u.hostname.toLowerCase();
    return u.protocol === "https:" && !u.username && !u.password && !u.port && h.includes(".") && !h.includes(":") && !/^\d+(\.\d+){3}$/.test(h) && !["localhost", "local", "internal", "test", "invalid"].some((s) => h === s || h.endsWith("." + s)) && !/[\u0000-\u0020\\]/.test(value);
  } catch {
    return false;
  }
}
function compareVersions(a, b) {
  const parts = (s) => s.split("+")[0].split("-"), [an, ap] = parts(a), [bn, bp] = parts(b);
  for (let i = 0; i < 3; i++) {
    const d = Number(an.split(".")[i]) - Number(bn.split(".")[i]);
    if (d) return Math.sign(d);
  }
  if (ap === void 0 || bp === void 0) return ap === bp ? 0 : ap === void 0 ? 1 : -1;
  const aa = ap.split("."), bb = bp.split(".");
  for (let i = 0; i < Math.max(aa.length, bb.length); i++) {
    if (aa[i] === bb[i]) continue;
    if (aa[i] === void 0) return -1;
    if (bb[i] === void 0) return 1;
    const x = aa[i], y = bb[i], nx = /^\d+$/.test(x), ny = /^\d+$/.test(y);
    if (nx && ny) return Number(x) > Number(y) ? 1 : -1;
    if (nx !== ny) return nx ? -1 : 1;
    return x > y ? 1 : -1;
  }
  return 0;
}
var SourceError = class extends Error {
  constructor(kind, detail = kind, retryAt) {
    super(detail);
    this.kind = kind;
    this.detail = detail;
    this.retryAt = retryAt;
    this.name = "SourceError";
  }
};

// Website/src/integrations/config.ts
function validateConfig(config) {
  const issues = [];
  const url2 = (v, key, host) => {
    if (v !== null && (!safeHttps(v) || host && ![host, ...host === "buymeacoffee.com" ? ["www.buymeacoffee.com"] : []].includes(new URL(String(v)).hostname))) issues.push(key);
  };
  url2(config.store.url, "store.url", "apps.microsoft.com");
  url2(config.registry.url, "registry.url");
  url2(config.registry.catalogGithubUrl, "registry.catalogGithubUrl", "github.com");
  url2(config.registry.contributeUrl, "registry.contributeUrl", "github.com");
  url2(config.support.monobank.url, "support.monobank.url", "send.monobank.ua");
  url2(config.support.buyMeACoffee.url, "support.buyMeACoffee.url", "buymeacoffee.com");
  if (config.store.url && safeHttps(config.store.url) && !/^\/(?:detail|store\/detail)\/(?:[^/]+\/)?[A-Za-z0-9]{12}\/?$/.test(new URL(config.store.url).pathname)) issues.push("store.url");
  const g = config.github;
  if (g.owner === null !== (g.repository === null)) issues.push("github.owner/repository");
  if (g.owner !== null && (!/^[A-Za-z0-9][A-Za-z0-9-]{0,38}$/.test(g.owner) || !g.repository || !/^[A-Za-z0-9_.-]+$/.test(g.repository))) issues.push("github.owner/repository");
  if (!/^[A-Za-z0-9_.-]+\{version\}-win-x64\.zip$/.test(g.artifact.filenameTemplate) || g.artifact.packageType !== "portable-zip" || g.artifact.architecture !== "x64" || !/^[A-Za-z0-9_.-]+$/.test(g.artifact.checksumFilename)) issues.push("github.artifact");
  for (const [key, value] of [["github.cacheMinutes", g.cacheMinutes], ["registry.cacheMinutes", config.registry.cacheMinutes]]) if (!Number.isFinite(value) || value < 1 || value > 60) issues.push(key);
  for (const [v, license] of Object.entries(config.licensing.releases)) {
    if (!license.name || !safeHttps(license.url) || !["unsigned", "signed", "unknown"].includes(license.signature)) issues.push("licensing.releases." + v);
  }
  return issues;
}
var integrations = settings_default;
var configIssues = validateConfig(integrations);

// Website/src/integrations/resource.ts
var pending = /* @__PURE__ */ new Map();
var memory = /* @__PURE__ */ new Map();
var cooldowns = /* @__PURE__ */ new Map();
function storage() {
  try {
    return typeof localStorage !== "undefined" ? localStorage : void 0;
  } catch {
    return void 0;
  }
}
async function cachedResource({ key, ttl, load, validate, force = false, store = storage(), now = Date.now }) {
  let cache;
  try {
    const entry = memory.get(key) ?? JSON.parse(store?.getItem(key) ?? "null");
    if (entry && Number.isFinite(entry.checkedAt) && entry.checkedAt <= now() + 6e4 && now() - entry.checkedAt < 7 * 864e5) cache = { data: validate(entry.data), checkedAt: entry.checkedAt };
  } catch {
  }
  if (cache && !force && now() - cache.checkedAt < ttl) return { ...cache, cached: true, stale: false };
  const existing = pending.get(key);
  if (existing) return existing;
  const task = (async () => {
    try {
      let until = cooldowns.get(key) ?? 0;
      try {
        until = Math.max(until, Number(store?.getItem(key + ":retry") ?? 0));
      } catch {
      }
      if (until > now()) throw new SourceError("rate-limit", "Retry after API cooldown", until);
      const data = validate(await load()), checkedAt = now();
      memory.set(key, { data, checkedAt });
      try {
        store?.setItem(key, JSON.stringify({ data, checkedAt }));
      } catch {
      }
      return { data, checkedAt, cached: false, stale: false };
    } catch (e) {
      const error = e instanceof SourceError ? e : new SourceError("network", "Network request failed");
      if (error.retryAt) {
        cooldowns.set(key, error.retryAt);
        try {
          store?.setItem(key + ":retry", String(error.retryAt));
        } catch {
        }
      }
      if (cache) return { ...cache, cached: true, stale: true, warning: error };
      throw error;
    }
  })();
  pending.set(key, task);
  try {
    return await task;
  } finally {
    pending.delete(key);
  }
}
async function fetchText(url2, fetcher = fetch, headers = {}, maxBytes = 2 * 1024 * 1024) {
  const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 1e4);
  try {
    const response = await fetcher(url2, { headers, signal: controller.signal, cache: "no-store", credentials: "omit", referrerPolicy: "no-referrer" });
    if (!response.ok) {
      if (response.status === 429 || response.status === 403 && (response.headers.get("x-ratelimit-remaining") === "0" || response.headers.has("retry-after"))) {
        const after = response.headers.get("retry-after"), seconds = after && /^\d+$/.test(after) ? Number(after) * 1e3 : after ? Date.parse(after) - Date.now() : 0, reset = Number(response.headers.get("x-ratelimit-reset")) * 1e3;
        throw new SourceError("rate-limit", `HTTP ${response.status}`, Math.max(Date.now() + 6e4, Date.now() + Math.min(seconds || 0, 864e5), Math.min(reset || 0, Date.now() + 864e5)));
      }
      throw new SourceError(response.status === 404 ? "not-found" : "http", `HTTP ${response.status}`);
    }
    if (Number(response.headers.get("content-length")) > maxBytes) throw new SourceError("malformed", "Response too large");
    if (!response.body) {
      const value = await response.text();
      if (new TextEncoder().encode(value).length > maxBytes) throw new SourceError("malformed", "Response too large");
      return value;
    }
    const reader = response.body.getReader(), chunks = [];
    let size = 0;
    for (; ; ) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > maxBytes) {
        await reader.cancel();
        throw new SourceError("malformed", "Response too large");
      }
      chunks.push(value);
    }
    const bytes = new Uint8Array(size);
    let offset = 0;
    for (const chunk of chunks) {
      bytes.set(chunk, offset);
      offset += chunk.length;
    }
    return new TextDecoder().decode(bytes);
  } catch (e) {
    if (e instanceof SourceError) throw e;
    throw new SourceError("network", "Network request failed or timed out");
  } finally {
    clearTimeout(timer);
  }
}

// Website/src/product.ts
var product = {
  name: "Backpack",
  packageName: "Backpack",
  version: "0.4.0-preview.4",
  channel: "Preview",
  platforms: ["Windows 10", "Windows 11"],
  architecture: "x64",
  published: false,
  signed: false,
  downloadUrl: null,
  repositoryUrl: null,
  siteUrl: null,
  sizeBytes: 92634242,
  releaseNotesUrl: "/release-notes.txt",
  sha256: "7010d5f6d5c4e9a0ea464227dd87549dba09677c91252ea288bc4b15a3ab3679",
  author: { name: "soobshoob", url: "https://github.com/soobshoob" },
  license: { ...settings_default.licensing.releases["0.4.0-preview.4"], localUrl: "/licenses/PolyForm-Strict.txt" },
  release: { version: "0.4.0-preview.4", verified: true, public: false, verifiedOn: "2026-10-30", evidence: "Phase4G-Implementation-Report" },
  core: [{ id: "keys", implemented: true, verified: true }, { id: "mouse", implemented: true, verified: true }],
  plugins: [
    { id: "drawing", status: "planned", implementedAsPlugin: false, verifiedAsPlugin: false, legacyToolVerified: true, version: null, sdk: null },
    { id: "layout", status: "planned", implementedAsPlugin: false, verifiedAsPlugin: false, legacyToolVerified: true, version: null, sdk: null },
    { id: "lens", status: "verified", implementedAsPlugin: true, verifiedAsPlugin: true, legacyToolVerified: true, version: "1.0.0", sdk: "1.1" },
    { id: "focus", status: "verified", implementedAsPlugin: true, verifiedAsPlugin: true, legacyToolVerified: true, version: "1.0.0", sdk: "1.1" },
    { id: "cursorLens", status: "verified", implementedAsPlugin: true, verifiedAsPlugin: true, legacyToolVerified: true, version: "1.0.0", sdk: "1.1" },
    { id: "timer", status: "verified", implementedAsPlugin: true, verifiedAsPlugin: true, legacyToolVerified: false, version: "2.0.0", sdk: "1.1" }
  ].map((plugin) => ({ ...plugin, status: plugin.status, author: "soobshoob", public: false, packageUrl: null, documentationUrl: null, exampleUrl: null })),
  infrastructure: { profiles: true, appearance: true, inputPrivacy: true, pluginManagement: true },
  sdk: { verifiedVersion: "1.1", sourceVersion: "1.1", documentationUrl: null, exampleUrl: null, packageUrl: null, licenseResolved: false },
  verifiedOn: "2026-10-30"
};

// Website/src/catalog.ts
var categories = ["all", "tools", "mouse", "keystrokes", "focus", "drawing", "styles", "productivity", "integrations"];
var catalogEntries = product.plugins.map((p, i) => ({ ...p, index: i, slug: ["drawing", "layout-indicator", "lens", "focus", "cursor-magnifier", "presentation-timer"][i], categories: [["tools", "drawing"], ["tools", "productivity"], ["tools", "focus"], ["tools", "focus"], ["mouse", "focus"], ["productivity", "tools"]][i], tags: [["annotation", "arrow"], ["language", "keyboard"], ["magnify", "window"], ["dim", "attention"], ["cursor", "zoom"], ["countdown", "presentation"]][i], updatedAt: null, sizeBytes: null, repositoryUrl: null, permissions: i === 5 ? ["commands", "settings", "overlay.panel"] : i >= 2 ? ["commands", "settings", i === 3 ? "overlay.dim" : "overlay.magnifier"] : [], license: "PolyForm Strict 1.0.0" }));

// Website/src/integrations/registry.ts
function localized(value, lang) {
  return value[lang] ?? value[lang.split("-")[0]] ?? value.en;
}
function fail(path, kind = "malformed") {
  throw new SourceError(kind, `Registry field: ${path}`);
}
function object(value, path) {
  if (!value || typeof value !== "object" || Array.isArray(value)) fail(path);
  return value;
}
function text(value, path, max = 200) {
  if (typeof value !== "string" || !value.trim() || value.length > max || /[\u0000-\u0008\u000b\u000c\u000e-\u001f]/.test(value)) fail(path);
  return value;
}
function locale(value, path, max) {
  const v = object(value, path);
  if (!("en" in v) || Object.keys(v).length > 40) fail(path);
  return Object.fromEntries(Object.entries(v).map(([key, s]) => {
    if (!/^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(key)) fail(path);
    return [key, text(s, path + "." + key, max)];
  }));
}
function url(value, path, image = false) {
  if (!safeHttps(value) || image && !/\.(png|jpe?g|webp)$/i.test(new URL(value).pathname)) fail(path);
  return value;
}
function strings(value, path, max = 20) {
  if (!Array.isArray(value) || value.length > max) fail(path);
  return value.map((s, i) => text(s, `${path}[${i}]`, 80));
}
function keys(value, allowed, path) {
  if (Object.keys(value).some((k) => !allowed.includes(k))) fail(path + ".unknown-field");
}
function validateRegistry(raw) {
  const root = object(raw, "root");
  if (root.schemaVersion !== 1) fail("schemaVersion", "schema");
  keys(root, ["schemaVersion", "plugins"], "root");
  if (!Array.isArray(root.plugins) || root.plugins.length > 500) fail("plugins");
  const ids = /* @__PURE__ */ new Set(), slugs = /* @__PURE__ */ new Set();
  const plugins = root.plugins.map((value, i) => {
    const p = object(value, `plugins[${i}]`), at = `plugins[${i}]`, id = text(p.id, at + ".id", 150), slug = text(p.slug, at + ".slug", 80);
    if (!/^[a-z0-9]+(?:[.-][a-z0-9]+)+$/.test(id) || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug) || ["publish", "_route", "__registry_plugin__"].includes(slug)) fail(at + ".id/slug");
    if (ids.has(id) || slugs.has(slug)) fail(at + ".id/slug", "duplicate");
    ids.add(id);
    slugs.add(slug);
    const author = object(p.author, at + ".author"), distribution = object(p.distribution, at + ".distribution"), compat = object(p.compatibility, at + ".compatibility"), license = object(p.license, at + ".license");
    keys(p, ["id", "slug", "name", "author", "summary", "description", "version", "category", "tags", "distribution", "compatibility", "license", "repositoryUrl", "icon", "images", "updatedAt", "capabilities", "changelog"], at);
    keys(author, ["name", "url"], at + ".author");
    keys(distribution, ["kind", "url", "sha256", "sizeBytes"], at + ".distribution");
    keys(compat, ["minBackpack", "maxBackpack", "sdk", "platforms"], at + ".compatibility");
    keys(license, ["name", "url"], at + ".license");
    if (distribution.kind !== "package" && distribution.kind !== "release") fail(at + ".distribution.kind");
    const packageUrl = url(distribution.url, at + ".distribution.url");
    if (distribution.kind === "package" && !/\.bppkg$/i.test(new URL(packageUrl).pathname) || distribution.kind === "release" && /\.(bppkg|zip|exe|msi)$/i.test(new URL(packageUrl).pathname)) fail(at + ".distribution.url");
    if (distribution.sha256 !== void 0 && (distribution.kind !== "package" || typeof distribution.sha256 !== "string" || !/^[a-fA-F0-9]{64}$/.test(distribution.sha256))) fail(at + ".distribution.sha256");
    if (distribution.sizeBytes !== void 0 && (!Number.isSafeInteger(distribution.sizeBytes) || Number(distribution.sizeBytes) <= 0)) fail(at + ".distribution.sizeBytes");
    const version = text(p.version, at + ".version", 80), minBackpack = text(compat.minBackpack, at + ".compatibility.minBackpack", 80), sdk = text(compat.sdk, at + ".compatibility.sdk", 20);
    if (!versionPattern.test(version) || !versionPattern.test(minBackpack) || !/^\d+\.\d+$/.test(sdk)) fail(at + ".version/compatibility");
    if (compat.maxBackpack !== void 0 && (!versionPattern.test(String(compat.maxBackpack)) || compareVersions(String(compat.maxBackpack), minBackpack) < 0)) fail(at + ".compatibility.maxBackpack");
    const platforms = strings(compat.platforms, at + ".compatibility.platforms");
    if (!platforms.length || platforms.some((v) => !["windows-x64", "windows-arm64"].includes(v))) fail(at + ".compatibility.platforms");
    const category = text(p.category, at + ".category");
    if (!categories.slice(1).includes(category)) fail(at + ".category");
    if (p.images !== void 0 && (!Array.isArray(p.images) || p.images.length > 8)) fail(at + ".images");
    if (p.updatedAt !== void 0 && (typeof p.updatedAt !== "string" || !/^\d{4}-\d{2}-\d{2}T/.test(p.updatedAt) || !Number.isFinite(Date.parse(p.updatedAt)))) fail(at + ".updatedAt");
    const result = { id, slug, name: locale(p.name, at + ".name", 100), author: { name: text(author.name, at + ".author.name", 100), ...author.url ? { url: url(author.url, at + ".author.url") } : {} }, summary: locale(p.summary, at + ".summary", 300), description: locale(p.description, at + ".description", 12e3), version, category, tags: strings(p.tags, at + ".tags"), distribution: { kind: distribution.kind, url: packageUrl, ...distribution.sha256 ? { sha256: String(distribution.sha256).toLowerCase() } : {}, ...distribution.sizeBytes ? { sizeBytes: Number(distribution.sizeBytes) } : {} }, compatibility: { minBackpack, sdk, platforms, ...compat.maxBackpack ? { maxBackpack: String(compat.maxBackpack) } : {} }, license: { name: text(license.name, at + ".license.name", 200), ...license.url ? { url: url(license.url, at + ".license.url") } : {} } };
    if (p.repositoryUrl !== void 0) result.repositoryUrl = url(p.repositoryUrl, at + ".repositoryUrl");
    if (p.icon !== void 0) result.icon = url(p.icon, at + ".icon", true);
    if (p.images) result.images = p.images.map((item, n) => {
      const image = object(item, at + ".images");
      keys(image, ["url", "alt"], at + ".images");
      return { url: url(image.url, at + `.images[${n}].url`, true), alt: locale(image.alt, at + `.images[${n}].alt`, 300) };
    });
    if (p.updatedAt) result.updatedAt = String(p.updatedAt);
    if (p.capabilities !== void 0) result.capabilities = strings(p.capabilities, at + ".capabilities", 50);
    if (p.changelog !== void 0) result.changelog = locale(p.changelog, at + ".changelog", 12e3);
    return result;
  });
  return { schemaVersion: 1, plugins };
}
var registryKey = integrations.registry.url && !configIssues.includes("registry.url") ? integrations.registry.url : null;
function loadRegistry({ url: url2 = registryKey, ttl = integrations.registry.cacheMinutes * 6e4, fetcher = fetch, force = false, store, now } = {}) {
  if (!safeHttps(url2)) return Promise.reject(new SourceError("unconfigured", "Registry URL not configured"));
  return cachedResource({ key: "backpack:registry:v1:" + url2, ttl, force, store, now, validate: validateRegistry, load: async () => {
    let data;
    try {
      data = JSON.parse(await fetchText(url2, fetcher));
    } catch (e) {
      if (e instanceof SourceError) throw e;
      throw new SourceError("malformed", "Invalid registry JSON");
    }
    return data;
  } });
}
function filterRegistry(entries, query, category, lang) {
  const q = query.trim().toLocaleLowerCase();
  return entries.filter((p) => (category === "all" || p.category === category) && (!q || [localized(p.name, lang), localized(p.summary, lang), localized(p.description, lang), p.author.name, p.id, ...p.tags].join(" ").toLocaleLowerCase().includes(q)));
}
function matchesKnownHost(entry, version) {
  const sdk = { "0.4.0-preview.4": "1.1", "0.4.0-preview.3": "1.0" }[version];
  if (!sdk) return false;
  const [major, minor] = entry.compatibility.sdk.split(".").map(Number), [hostMajor, hostMinor] = sdk.split(".").map(Number);
  return major === hostMajor && minor <= hostMinor && compareVersions(version, entry.compatibility.minBackpack) >= 0 && (!entry.compatibility.maxBackpack || compareVersions(version, entry.compatibility.maxBackpack) <= 0) && entry.compatibility.platforms.includes("windows-x64");
}
export {
  filterRegistry,
  loadRegistry,
  localized,
  matchesKnownHost,
  registryKey,
  validateRegistry
};
