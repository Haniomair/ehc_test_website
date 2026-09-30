// Builds the self-hosted map tiles: one PMTiles file for EHC's area, cut from the latest Protomaps daily
// OpenStreetMap build. The site serves it itself (HTTP range requests), so maps make no third-party calls.
//   npm run tiles                     -> ../src/EHC.Web/wwwroot/tiles/ehc-region.pmtiles
// Options (env): TILES_BBOX=minLon,minLat,maxLon,maxLat  TILES_MAXZOOM=14  TILES_OUT=path  TILES_BUILD=YYYYMMDD
// Needs the `pmtiles` CLI (https://github.com/protomaps/go-pmtiles); it is downloaded into frontend/.tools if missing.
// Data: © OpenStreetMap contributors (ODbL) — keep the attribution on every map.
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync, statSync, chmodSync, rmSync, renameSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { platform, arch } from 'node:os';

const PMTILES_VERSION = '1.31.2';
// EHC facilities span ~24.1–28.5 N, 46.9–51.5 E; the box adds a margin (includes Bahrain and the Al-Ahsa oases)
const bbox = process.env.TILES_BBOX || '45.8,23.6,52.2,29.1';
const maxzoom = process.env.TILES_MAXZOOM || '14';
const out = resolve(process.env.TILES_OUT || '../src/EHC.Web/wwwroot/tiles/ehc-region.pmtiles');

async function cli() {
  const exe = platform() === 'win32' ? 'pmtiles.exe' : 'pmtiles';
  const local = resolve('.tools', exe);
  if (existsSync(local)) return local;
  try { execFileSync(exe, ['version'], { stdio: 'ignore' }); return exe; } catch { /* not on PATH */ }
  const os = { win32: 'Windows', linux: 'Linux', darwin: 'Darwin' }[platform()];
  const cpu = { x64: 'x86_64', arm64: 'arm64' }[arch()];
  if (!os || !cpu) throw new Error(`No pmtiles build for ${platform()}/${arch()} — install it and put it on PATH.`);
  const ext = os === 'Linux' ? 'tar.gz' : 'zip';
  const url = `https://github.com/protomaps/go-pmtiles/releases/download/v${PMTILES_VERSION}/go-pmtiles_${PMTILES_VERSION}_${os}_${cpu}.${ext}`;
  console.log(`downloading pmtiles CLI ${PMTILES_VERSION} …`);
  const res = await fetch(url);
  if (!res.ok) throw new Error(`download failed (${res.status}): ${url}`);
  mkdirSync('.tools', { recursive: true });
  const archive = resolve('.tools', `pmtiles.${ext}`);
  writeFileSync(archive, Buffer.from(await res.arrayBuffer()));
  // bsdtar (Windows 10+ System32, macOS) also unpacks zip files; on Windows call it by path, since a GNU tar from
  // Git Bash may come first on PATH and cannot
  const tar = platform() === 'win32' ? join(process.env.SystemRoot || 'C:\Windows', 'System32', 'tar.exe') : 'tar';
  execFileSync(tar, ['-xf', `pmtiles.${ext}`, exe], { cwd: resolve('.tools') });
  rmSync(archive);
  if (platform() !== 'win32') chmodSync(local, 0o755);
  return local;
}

// newest first; the newest daily build can still be in progress, so older ones are kept as fallbacks
async function candidateBuilds(max = 3) {
  if (process.env.TILES_BUILD) return [process.env.TILES_BUILD];
  const builds = [];
  for (let i = 0; i < 10 && builds.length < max; i++) {
    const d = new Date(Date.now() - i * 86400000).toISOString().slice(0, 10).replace(/-/g, '');
    const r = await fetch(`https://build.protomaps.com/${d}.pmtiles`, { method: 'HEAD' });
    if (r.ok) builds.push(d);
  }
  if (!builds.length) throw new Error('No Protomaps build found in the last 10 days — set TILES_BUILD=YYYYMMDD.');
  return builds;
}

const pmtiles = await cli();
mkdirSync(dirname(out), { recursive: true });
// extract to a temporary file so a failed download never leaves a truncated tiles file behind
const partial = `${out}.partial`;
// the host sometimes answers a single range request with HTTP 500, so each build gets a second attempt
let done = false;
for (const build of await candidateBuilds()) {
  for (let attempt = 1; attempt <= 2 && !done; attempt++) {
    console.log(`extracting build ${build} (attempt ${attempt}), bbox ${bbox}, zoom 0–${maxzoom} -> ${out}`);
    try {
      execFileSync(pmtiles, ['extract', `https://build.protomaps.com/${build}.pmtiles`, partial, `--bbox=${bbox}`,
        `--maxzoom=${maxzoom}`, '--download-threads=2'], { stdio: 'inherit' });
      renameSync(partial, out);
      done = true;
    } catch {
      rmSync(partial, { force: true });
      console.warn(`build ${build} attempt ${attempt} failed`);
    }
  }
  if (done) break;
}
if (!done) throw new Error('Could not extract map tiles from any recent Protomaps build.');
console.log(`done: ${(statSync(out).size / 1048576).toFixed(1)} MB`);
