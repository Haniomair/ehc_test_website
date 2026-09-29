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
   `powershell -ExecutionPolicy Bypass -File deploy/deploy-staging.ps1 -Settings -SeedData`

Later deploys: `powershell -ExecutionPolicy Bypass -File deploy/deploy-staging.ps1`. The server's database, media,
logs and settings file are skipped, so content edited on the test server is kept. `-WhatIf` lists the changes
without making them. After `-SeedData`, content on the server is replaced by the local copy.

### Continuous deployment
Branches: work on `develop`, open a pull request `develop` -> `test`; merging it deploys to the test server.
`main` is kept for production. `.github/workflows/ci-deploy.yml` builds and tests every pull request to `test` or
`main`, and deploys each push to `test` with the same script (map tiles are rebuilt once a month and cached). One-time setup in the GitHub repository,
Settings > Environments > `staging`:
- variables `EHC_DEPLOY_SERVER`, `EHC_DEPLOY_SITE`, `EHC_DEPLOY_USER`, and optionally
  `EHC_DEPLOY_SELF_CONTAINED` / `EHC_DEPLOY_ALLOW_UNTRUSTED` set to `true`;
- secret `EHC_DEPLOY_PASSWORD`.

The pipeline deploys code only. The server settings file and the first `-SeedData` upload are done from a local
machine, as above.

If the site shows HTTP 500.3x, set `stdoutLogEnabled="true"` in the server's `web.config` and read `logs/stdout*`;
Umbraco's own log is in `umbraco/Logs`.

## Map tiles
Maps use a self-hosted basemap: one PMTiles file of the Eastern Province, cut from the latest Protomaps
OpenStreetMap build and served by the site itself, so visitors' browsers never contact a map provider.
```bash
cd frontend && npm run tiles    # -> src/EHC.Web/wwwroot/tiles/ehc-region.pmtiles
```
The file is not in source control; build it on each machine and before `dotnet publish` (which warns if it is
missing — maps then show their pins on a plain background). Re-run it now and then to refresh the map data.
The map data is © OpenStreetMap contributors (ODbL); keep the attribution shown on the maps.
