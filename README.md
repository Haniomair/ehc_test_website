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
