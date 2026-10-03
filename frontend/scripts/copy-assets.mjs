// Copies fonts, images and JS into dist/ (mirrors the Umbraco wwwroot/assets layout).
// Set EHC_WWWROOT to also publish straight into the Umbraco project, e.g.
//   EHC_WWWROOT=../src/EHC.Web/wwwroot/assets npm run build
import { cpSync, mkdirSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
const out = 'dist';
for (const d of ['fonts', 'img', 'js']) {
  mkdirSync(`${out}/${d}`, { recursive: true });
  cpSync(`src/${d}`, `${out}/${d}`, { recursive: true });
}
// images referenced from ehc.css get a content hash (?v=), so /assets serves them with a one-year cache (Performance.cs)
const css = `${out}/css/ehc.css`;
if (existsSync(css)) {
  writeFileSync(css, readFileSync(css, 'utf8').replace(/url\((['"]?)\.\.\/img\/([^'")?]+)\1\)/g, (m, q, file) => {
    const hash = createHash('sha256').update(readFileSync(`src/img/${file}`)).digest('hex').slice(0, 12);
    return `url(${q}../img/${file}?v=${hash}${q})`;
  }));
}
// plain stylesheets outside the Tailwind build
mkdirSync(`${out}/css`, { recursive: true });
cpSync('src/css/customizer-vt.css', `${out}/css/customizer-vt.css`);
// third-party libraries served from our own origin (no CDN, CSP 'self')
mkdirSync(`${out}/vendor/leaflet`, { recursive: true });
for (const f of ['leaflet.js', 'leaflet.css', 'images']) {
  cpSync(`node_modules/leaflet/dist/${f}`, `${out}/vendor/leaflet/${f}`, { recursive: true });
}
cpSync('node_modules/leaflet/LICENSE', `${out}/vendor/leaflet/LICENSE`);
// marker clustering (cluster look comes from ehc.css, not the plugin's default theme)
mkdirSync(`${out}/vendor/leaflet.markercluster`, { recursive: true });
for (const f of ['leaflet.markercluster.js', 'MarkerCluster.css']) {
  cpSync(`node_modules/leaflet.markercluster/dist/${f}`, `${out}/vendor/leaflet.markercluster/${f}`);
}
cpSync('node_modules/leaflet.markercluster/MIT-LICENCE.txt', `${out}/vendor/leaflet.markercluster/LICENSE.txt`);
// vector basemap renderer for the self-hosted PMTiles file (npm run tiles)
mkdirSync(`${out}/vendor/protomaps-leaflet`, { recursive: true });
cpSync('node_modules/protomaps-leaflet/dist/protomaps-leaflet.js', `${out}/vendor/protomaps-leaflet/protomaps-leaflet.js`);
cpSync('node_modules/protomaps-leaflet/LICENSE', `${out}/vendor/protomaps-leaflet/LICENSE`);
// real-visitor Core Web Vitals (components/vitals.js loads it only after consent to optional cookies)
mkdirSync(`${out}/vendor/web-vitals`, { recursive: true });
cpSync('node_modules/web-vitals/dist/web-vitals.iife.js', `${out}/vendor/web-vitals/web-vitals.iife.js`);
cpSync('node_modules/web-vitals/LICENSE', `${out}/vendor/web-vitals/LICENSE`);
const target = process.env.EHC_WWWROOT;
if (target) {
  mkdirSync(target, { recursive: true });
  cpSync(out, target, { recursive: true });
  console.log(`published dist -> ${target}`);
}
console.log('assets copied');
