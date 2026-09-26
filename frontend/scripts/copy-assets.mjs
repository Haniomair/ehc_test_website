// Copies fonts, images and JS into dist/ (mirrors the Umbraco wwwroot/assets layout).
// Set EHC_WWWROOT to also publish straight into the Umbraco project, e.g.
//   EHC_WWWROOT=../src/EHC.Web/wwwroot/assets npm run build
import { cpSync, mkdirSync, existsSync } from 'node:fs';
const out = 'dist';
for (const d of ['fonts', 'img', 'js']) {
  mkdirSync(`${out}/${d}`, { recursive: true });
  cpSync(`src/${d}`, `${out}/${d}`, { recursive: true });
}
// third-party libraries served from our own origin (no CDN, CSP 'self')
mkdirSync(`${out}/vendor/leaflet`, { recursive: true });
for (const f of ['leaflet.js', 'leaflet.css', 'images']) {
  cpSync(`node_modules/leaflet/dist/${f}`, `${out}/vendor/leaflet/${f}`, { recursive: true });
}
cpSync('node_modules/leaflet/LICENSE', `${out}/vendor/leaflet/LICENSE`);
const target = process.env.EHC_WWWROOT;
if (target) {
  mkdirSync(target, { recursive: true });
  cpSync(out, target, { recursive: true });
  console.log(`published dist -> ${target}`);
}
console.log('assets copied');
