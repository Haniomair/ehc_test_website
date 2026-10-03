// Backoffice "Add block" thumbnails: screenshots the first section of each block type (section[data-block]) on the
// component library, hero examples and home pages of a running local site, into
// src/EHC.Web/wwwroot/App_Plugins/EhcBlockRows/thumbs/{alias}.webp (referenced by the "EHC - Page blocks" data type).
// Usage: start the site, then `npm run thumbnails` (optional: SITE=http://localhost:35929 LANG_CODE=ar).
// Uses the installed Microsoft Edge (EDGE_PATH to override); CSP is bypassed for this local run only.
import puppeteer from 'puppeteer-core';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const out = process.argv[2] || fileURLToPath(new URL('../../src/EHC.Web/wwwroot/App_Plugins/EhcBlockRows/thumbs/', import.meta.url));
const lang = process.env.LANG_CODE || 'ar';
const base = process.env.SITE || 'http://localhost:35929';
const arabic = ['/ar/' + encodeURIComponent('مكتبة-المكونات') + '/', '/ar/' + encodeURIComponent('نماذج-الترويسات') + '/', '/ar/'];
const english = ['/en/component-library/', '/en/hero-examples/', '/en/'];
// the main language first; a section only shown in the other language (not yet translated) is taken from there
const pages = lang === 'ar' ? [...arabic, ...english] : [...english, ...arabic];
fs.mkdirSync(out, { recursive: true });

const browser = await puppeteer.launch({
  executablePath: process.env.EDGE_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  headless: true,
  args: ['--no-first-run', '--hide-scrollbars'],
});
const page = await browser.newPage();
await page.setBypassCSP(true);   // local screenshots only: allow the helper style tag
await page.setViewport({ width: 1280, height: 1600, deviceScaleFactor: 0.75 });
await page.emulateMediaFeatures([{ name: 'prefers-reduced-motion', value: 'reduce' }, { name: 'prefers-color-scheme', value: 'light' }]);
const done = new Set();
for (const url of pages) {
  await page.goto(base + url, { waitUntil: 'networkidle2', timeout: 120000 });
  // show everything as it looks once on screen; no cookie banner, chat button or animations
  await page.addStyleTag({ content: `
    html.rv-on .rv:not(.in), .rv { opacity: 1 !important; transform: none !important; }
    *, *::before, *::after { animation: none !important; transition: none !important; }
    header, [data-site-header], #consent, [data-consent-notice], [data-action="assistant-open"], #assistant, body > .fixed, [data-cookie-banner], #cookie-banner, .cookie-banner, [data-assistant-launcher], [data-chat-launcher] { display: none !important; }
  ` });
  const blocks = await page.$$eval('section[data-block]', (els) => els.map((e, i) => ({ alias: e.getAttribute('data-block'), i, h: e.getBoundingClientRect().height })));
  for (const b of blocks) {
    if (done.has(b.alias) || b.h < 40) continue;
    const handles = await page.$$('section[data-block]');
    const el = handles[b.i];
    await el.scrollIntoView();
    await new Promise((r) => setTimeout(r, 400));
    // the section's place on the page (document coordinates); tall sections are cut at 760 px
    const r = await el.evaluate((e) => { const b = e.getBoundingClientRect(); return { x: b.left + window.scrollX, y: b.top + window.scrollY, w: b.width, h: b.height }; });
    if (!r.w || !r.h) continue;
    // the component library shows sections scaled down: render those at a higher pixel density, so every picture is
    // about 960 px wide (sharp in the backoffice's enlarged view on high-density screens)
    const dpr = Math.min(4, Math.max(0.75, 960 / r.w));
    await page.setViewport({ width: 1280, height: 1600, deviceScaleFactor: dpr });
    await page.screenshot({ path: path.join(out, `${b.alias}.webp`), type: 'webp', quality: 82, captureBeyondViewport: true,
      clip: { x: r.x, y: r.y, width: r.w, height: Math.min(r.h, 760 * r.w / 1280) } });
    done.add(b.alias);
  }
}
console.log(done.size, 'thumbnails:', [...done].join(' '));

// point the "EHC - Page blocks" data type at the pictures (uSync file; import Settings to apply)
const usync = fileURLToPath(new URL('../../src/EHC.Web/uSync/v17/', import.meta.url));
const aliasByKey = {};
for (const f of fs.readdirSync(path.join(usync, 'ContentTypes'))) {
  const m = fs.readFileSync(path.join(usync, 'ContentTypes', f), 'utf8').match(/<ContentType Key="([^"]+)" Alias="([^"]+)"/);
  if (m) aliasByKey[m[1].toLowerCase()] = m[2];
}
const dtPath = path.join(usync, 'DataTypes', 'EHCPageBlocks.config');
const xml = fs.readFileSync(dtPath, 'utf8');
const cdata = xml.match(/<Config><!\[CDATA\[([\s\S]*?)\]\]><\/Config>/);
const config = JSON.parse(cdata[1]);
const added = [];
for (const block of config.blocks) {
  const alias = aliasByKey[block.contentElementTypeKey.toLowerCase()];
  const thumb = `/App_Plugins/EhcBlockRows/thumbs/${alias}.webp`;
  if (alias && fs.existsSync(path.join(out, `${alias}.webp`)) && block.thumbnail !== thumb) { block.thumbnail = thumb; added.push(alias); }
}
if (added.length) {
  const crlf = xml.includes('\r\n');
  let json = JSON.stringify(config, null, 2);
  if (crlf) json = json.replace(/\n/g, '\r\n');
  fs.writeFileSync(dtPath, xml.replace(cdata[0], `<Config><![CDATA[${json}]]></Config>`));
  console.log('Thumbnail set for:', added.join(' '), '- the running site imports it by itself (local); elsewhere run uSync > Settings > Import.');
}

await browser.close();
