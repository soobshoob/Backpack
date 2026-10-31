// Семантическая проверка реестра плагинов тем же валидатором, что и сайт (Website/src/integrations/registry.ts):
// уникальные ID и slug, SemVer, допустимые HTTPS-адреса, тип распространения, SHA-256 только у прямого пакета.
// Запуск только из доверенного кода (базовый коммит), реестр из PR — просто данные:
//   node tools/registry/validate-registry.mjs <папка Website (доверенная)> <index.json>
// Код плагинов не скачивается и не выполняется.
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

const [website, indexPath] = process.argv.slice(2);
if (!website || !indexPath) {
  console.error('usage: node validate-registry.mjs <Website> <index.json>');
  process.exit(2);
}

const maxBytes = 2 * 1024 * 1024; // контракт сайта: не больше 2 МиБ UTF-8
const bytes = fs.readFileSync(indexPath);
if (bytes.length > maxBytes) {
  console.error(`index.json: ${bytes.length} bytes > ${maxBytes}`);
  process.exit(1);
}

let raw;
try {
  raw = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(bytes));
} catch (e) {
  console.error(`index.json: not valid UTF-8 JSON (${e.message})`);
  process.exit(1);
}

const require = createRequire(path.resolve(website, 'package.json'));
const { build } = createRequire(require.resolve('vite/package.json'))('esbuild');
const bundled = await build({
  entryPoints: [path.resolve(website, 'src/integrations/registry.ts')],
  bundle: true, platform: 'node', format: 'esm', write: false, target: 'node22', logLevel: 'silent',
});
const out = path.resolve(process.env.RUNNER_TEMP ?? path.dirname(indexPath), 'registry-validator.mjs');
fs.writeFileSync(out, bundled.outputFiles[0].text);
const { validateRegistry } = await import(pathToFileURL(out).href);

try {
  const registry = validateRegistry(raw);
  console.log(`OK: ${registry.plugins.length} plugin(s); IDs and slugs unique; URLs, versions and distribution valid.`);
} catch (e) {
  console.error(`INVALID (${e.kind ?? 'error'}): ${e.message}`);
  process.exit(1);
}
