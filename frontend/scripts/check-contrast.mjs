// WCAG contrast check for the default tokens and the reference occasion themes (preview/presets.css).
// Backoffice themes are checked when saved (Themes/Palette.cs, same pairs). Run: npm run check:contrast
// Fails (exit 1) if a required pair drops below its threshold.
import { readFileSync } from 'node:fs';
const read = f => readFileSync(new URL(`../${f}`, import.meta.url), 'utf8');
const vars = block => Object.fromEntries([...block.matchAll(/--([a-z0-9-]+):\s*(#[0-9A-Fa-f]{6})/g)].map(m => [m[1], m[2]]));
const root = vars(read('src/css/_tokens.css').split('@theme')[0]);
const themes = { default: root };
for (const m of read('preview/presets.css').matchAll(/\[data-theme="([^"]+)"\]\s*\{([^}]*)\}/g)) themes[m[1]] = { ...root, ...vars(m[2]) };

const lum = h => { const c = [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16) / 255).map(v => v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4); return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; };
const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };

const checks = [
  ['white on brand-600 (primary button)', t => ['#FFFFFF', t['brand-600']], 4.5],
  ['brand-600 on white (links, icons)', t => [t['brand-600'], '#FFFFFF'], 4.5],
  ['brand-700 on soft (links, eyebrows on light bands)', t => [t['brand-700'], t['soft']], 4.5],
  ['deep-900 on soft (headings)', t => [t['deep-900'], t['soft']], 7],
  ['brand-300 on deep-900 (hero highlight)', t => [t['brand-300'], t['deep-900']], 4.5],
  ['on-deep on deep-900 (muted text on dark)', t => [t['on-deep'], t['deep-900']], 4.5],
  ['dark text on accent-400 (sand button)', t => ['#2B1D00', t['accent-400']], 4.5],
];
let failed = 0;
for (const [name, t] of Object.entries(themes)) {
  const row = checks.map(([label, pick, min]) => { const r = ratio(...pick(t)); if (r < min) failed++; return `${r < min ? '✗' : '✓'} ${label} ${r.toFixed(2)}`; });
  console.log(`\n${name}\n  ` + row.join('\n  '));
}
if (failed) { console.error(`\n${failed} contrast check(s) failed`); process.exit(1); }
console.log('\nAll themes pass.');
