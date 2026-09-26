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
