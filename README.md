# EHC Website

The new public website for the Eastern Health Cluster (ehc.med.sa), built on **Umbraco 17 LTS** (.NET 10)
from the design.

## Repository layout
| Folder | Contents |
|---|---|
| `frontend/` | Tailwind CSS v4 design system: colour tokens as CSS variables, occasion themes, Cairo font, JS, preview pages |
| `reference/` | Reference C#/Razor (theme resolver, block wrapper, layouts, block partials, tests) to be ported into `src/EHC.Web` |
| `src/EHC.Web/` | The Umbraco project |

## Prerequisites
.NET 10 SDK, Node.js 20+, git.

## Frontend
```bash
cd frontend
npm install
npm run build              # -> frontend/dist
npm run publish:umbraco    # -> src/EHC.Web/wwwroot/assets
npm run watch              # rebuild CSS while editing views
npm run tiles              # map tiles -> src/EHC.Web/wwwroot/tiles (once, ~60 MB; see below)
```
Preview without Umbraco: open `frontend/preview/index.html` (homepage) or `frontend/preview/heroes.html`
(hero library) in a browser and use the theme chips — EHC default · Saudi National Day · Ramadan · Eid al-Fitr ·
Hajj · Eid al-Adha · Pink October. Dark mode and Arabic/English can be toggled too.

## Run the site
```bash
dotnet run --project src/EHC.Web
```
First run opens the Umbraco installer (choose SQLite for local development). The installer writes the
connection string to `src/EHC.Web/appsettings.Local.json`, which is not in source control. Keep secrets such as
`Umbraco:CMS:Imaging:HMACSecretKey` there locally, and in environment variables on servers.

Schema, settings, dictionary and demo content are stored as uSync files in `src/EHC.Web/uSync` and imported on the
first boot of an empty database. Media *files* (`wwwroot/media`) are not in source control: after a fresh setup,
upload the images in `frontend/src/img/placeholder` again to the "Placeholders" media folder (same names), or pick
new images in the hero sections.

Backoffice "Add block" thumbnails live in `src/EHC.Web/wwwroot/App_Plugins/EhcBlockRows/thumbs`. After a section's
design changes, or for a new section type, publish it on the home page (or the component library), start the site and
run `npm run thumbnails` in `frontend` (uses Microsoft Edge). The script also sets the thumbnails in the
"EHC - Page blocks" data type, which the running site picks up by itself (see below).

Locally (Development), the site imports the uSync Settings group (document, element and data types, templates,
languages, dictionary) after start-up and whenever a file under `src/EHC.Web/uSync` changes, e.g. after a `git pull`
or a schema change made in the files; no uSync > Settings > Import needed. Content is never imported automatically.
Switch it off with `Ehc:USync:AutoImport: false` in `appsettings.Development.json`.

## Publish
`src/EHC.Web/wwwroot/assets` is generated, so build the frontend first:
```bash
cd frontend && npm ci && npm run publish:umbraco
dotnet publish src/EHC.Web -c Release
```
`dotnet publish` stops with an error if the assets are missing.

## Test server (Staging)
The test site runs on shared Windows/IIS hosting with Web Deploy (currently InterServer, Plesk) in the `Staging`
environment: `appsettings.Staging.json` turns on the ER wait and assistant demo data and `Ehc:Seo:NoIndex` (robots.txt
disallows everything and every response carries `X-Robots-Tag: noindex`). Not for real patient or feedback data.

One-time setup:
1. In the hosting panel: create the site, turn on SSL (Let's Encrypt), enable Web Deploy and give the site write
   permission on `umbraco/Data`, `umbraco/Logs`, `umbraco/mediacache` and `wwwroot/media` (Plesk: File Manager >
   Change Permissions, or "Additional write/modify permissions" in the hosting settings). If the server has no
   .NET 10 runtime, deploy with `-SelfContained`. If Web Deploy's certificate is self-signed, add `-AllowUntrusted`.
2. Copy `deploy/staging.env.example` to `deploy/staging.env` and fill in the Web Deploy server, site and user.
3. Copy `deploy/appsettings.Staging.local.example.json` to `src/EHC.Web/appsettings.Staging.local.json` and set the
   site URL and a new `HMACSecretKey`. For SQL Server instead of SQLite, replace the connection string. The file
   holds secrets: it is never committed or published and is only sent with `-Settings`.
4. First deploy, with the local database and media (stop the local site first):
   `powershell -ExecutionPolicy Bypass -File deploy/deploy-staging.ps1 -Code -Settings -SeedData`

Later deploys: `powershell -ExecutionPolicy Bypass -File deploy/deploy-staging.ps1`. The server's database, media,
logs and settings file are skipped, so content edited on the test server is kept. `-WhatIf` lists the changes
without making them. After `-SeedData`, content on the server is replaced by the local copy.
`-Settings`, `-SeedData` and `-Media` upload only that; add `-Code` to build and deploy the local code as well
(normally code reaches the test server through CI, so uncommitted local changes are not shipped by accident).

Moving content from a local machine to the test server without replacing it:
1. Content: deploy as usual (the `src/EHC.Web/uSync/Content` files are part of the site), then on the test server open
   Settings > uSync and import Content. Pages that exist only on the server are kept.
2. Media files (uSync carries the media items, not the files):
   `powershell -ExecutionPolicy Bypass -File deploy/deploy-staging.ps1 -Media` uploads new and changed files
   in `wwwroot/media` and never deletes files on the server (no code is deployed).

### Continuous deployment
Branches: work on `develop`, open a pull request `develop` -> `test`; merging it deploys to the test server.
`main` is kept for production. `.github/workflows/ci-deploy.yml` builds and tests every pull request to `test` or
`main`, and deploys each push to `test` with the same script (map tiles are rebuilt once a month and cached). One-time setup in the GitHub repository,
Settings > Environments > `staging`:
- variables `EHC_DEPLOY_SERVER`, `EHC_DEPLOY_SITE`, `EHC_DEPLOY_USER`, and optionally
  `EHC_DEPLOY_SELF_CONTAINED` / `EHC_DEPLOY_ALLOW_UNTRUSTED` set to `true`;
- secret `EHC_DEPLOY_PASSWORD`.

On every start (so after every deploy) the server imports the schema from `src/EHC.Web/uSync` (document, element
and data types, templates, languages, dictionary items); content is not imported. Make schema changes locally, not on the test server:
they are replaced by the repository version on the next deploy.

The pipeline deploys code only. The server settings file and the first `-SeedData` upload are done from a local
machine, as above.

Web application firewall (ModSecurity, OWASP CRS): the backoffice login and API trip a few generic rules. Switch off
rule IDs `920230` (multiple URL encoding, login ReturnUrl), `942430` (SQL special characters, same URL) and `911100`
(HTTP method policy, blocks DELETE). If the backoffice shows an IIS "403 - Forbidden" page, look up the rule ID in the
ModSecurity log and add it.

If the site shows HTTP 500.3x, set `stdoutLogEnabled="true"` in the server's `web.config` and read `logs/stdout*`;
Umbraco's own log is in `umbraco/Logs`.

### Linux server (Oracle Cloud or any Ubuntu VM)
`deploy/deploy-oracle.ps1` deploys over SSH to an Ubuntu VM (arm64 or x64). The site runs as the `ehc` systemd
service on `127.0.0.1:5000` behind Caddy, which obtains the HTTPS certificate and serves HTTP/2 and HTTP/3.
1. Create the VM (Ubuntu, e.g. the Ampere A1 shape), save its SSH key, and allow TCP 80 and 443 (and UDP 443) in
   the subnet's security list. Point the domain's DNS A record to the VM's public IP.
2. Copy `deploy/oracle.env.example` to `deploy/oracle.env` and fill it in.
3. Prepare the server once: `powershell -ExecutionPolicy Bypass -File deploy/deploy-oracle.ps1 -Setup`
4. Settings and first deploy, with the local database and media (stop the local site first):
   `powershell -ExecutionPolicy Bypass -File deploy/deploy-oracle.ps1 -Settings -Code -SeedData`

Later deploys: `deploy/deploy-oracle.ps1`; `-Media` and `-Settings` work as for the Windows host, and `-Rollback`
restores the previous code. On the server: site files in `/var/www/ehc`, logs with `journalctl -u ehc` and in
`/var/www/ehc/umbraco/Logs`, restart with `sudo systemctl restart ehc`.

## Map tiles
Maps use a self-hosted basemap: one PMTiles file of the Eastern Province, cut from the latest Protomaps
OpenStreetMap build and served by the site itself, so visitors' browsers never contact a map provider.
```bash
cd frontend && npm run tiles    # -> src/EHC.Web/wwwroot/tiles/ehc-region.pmtiles
```
The file is not in source control; build it on each machine and before `dotnet publish` (which warns if it is
missing — maps then show their pins on a plain background). Re-run it now and then to refresh the map data.
The map data is © OpenStreetMap contributors (ODbL); keep the attribution shown on the maps.
