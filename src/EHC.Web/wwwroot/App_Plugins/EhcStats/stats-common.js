/* Shared by the Statistics dashboards (stats-dashboard.js, stats-live.js): API access, labels and the "top list" table. */
import { html, css, nothing } from '@umbraco-cms/backoffice/external/lit';

export const API = '/umbraco/management/api/v1/ehc/stats';
export const MEDIUM = { direct: 'Direct', search: 'Search engines', social: 'Social media', referral: 'Other websites', campaign: 'Campaign links' };
export const LANGUAGE = { 'ar-SA': 'Arabic', 'en-US': 'English' };
export const number = new Intl.NumberFormat('en');

let regions;
try { regions = new Intl.DisplayNames(['en'], { type: 'region' }); } catch { regions = null; }

export const country = (code) => (!code ? 'Unknown' : (regions?.of(code) ?? code));
export const capital = (s) => (s ? s.charAt(0).toUpperCase() + s.slice(1) : s);

/** GET from the statistics API with the signed-in user's token. */
export async function get(auth, path) {
  const token = await auth.getLatestToken();
  const response = await fetch(`${API}/${path}`, { headers: { Authorization: `Bearer ${token}` } });
  if (!response.ok) throw new Error(String(response.status));
  return response;
}

/** Clean axis step: 1, 2 or 5 × a power of ten. */
export function niceStep(raw) {
  const pow = Math.pow(10, Math.floor(Math.log10(Math.max(raw, 1))));
  const n = raw / pow;
  return Math.max(1, (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * pow);
}

/** A page row: language badge, then the page name linked to the live page. Value is "pageKey|culture". */
export function pageLabel(i) {
  const culture = i.value.split('|')[1];
  const lang = culture === 'ar-SA' ? 'AR' : culture === 'en-US' ? 'EN' : '';
  const name = i.url ? html`<a href=${i.url} target="_blank" rel="noopener">${i.label}</a>` : i.label;
  return html`${lang ? html`<span class="lang" title=${LANGUAGE[culture]}>${lang}</span>` : nothing}${name}`;
}

/** A traffic source row. Value is "medium|source". */
export function sourceLabel(value) {
  const [medium, source] = value.split('|');
  if (medium === 'direct') return html`Direct <span class="sub">typed, bookmarked or from an app</span>`;
  return html`${source || 'Unknown'} <span class="sub">${MEDIUM[medium] ?? medium}</span>`;
}

/**
 * A top list: label with a thin bar for its share of the largest row, then one column per entry of `columns`
 * ({ key, title }); the bar follows the first column.
 */
export function barList(items, label, columns) {
  if (!items?.length) return html`<p class="muted">No data for this period.</p>`;
  const key = columns[0].key;
  const most = Math.max(...items.map((i) => i[key]), 1);
  return html`<table class="list">
    <thead><tr><th><span class="sr">Name</span></th>${columns.map((c) => html`<th class="num">${c.title}</th>`)}</tr></thead>
    <tbody>${items.map((i) => html`<tr>
      <td><div class="name">${label(i)}</div><div class="share"><span style=${`width:${Math.max(2, (i[key] / most) * 100)}%`}></span></div></td>
      ${columns.map((c) => html`<td class="num">${number.format(i[c.key])}</td>`)}
    </tr>`)}</tbody>
  </table>`;
}

export const listStyles = css`
  table { width: 100%; border-collapse: collapse; }
  th, td { text-align: start; padding: 7px 8px; border-bottom: 1px solid var(--uui-color-border); vertical-align: top; }
  th { font-weight: 600; font-size: 12px; color: var(--uui-color-text-alt); }
  .num { text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums; width: 1%; }
  .list .name { overflow-wrap: anywhere; }
  .share { height: 4px; margin-top: 5px; background: var(--uui-color-divider, #f0f0f0); border-radius: 2px; }
  .share span { display: block; height: 4px; background: var(--uui-color-interactive); border-radius: 0 2px 2px 0; }
  .sub { color: var(--uui-color-text-alt); font-size: 12px; margin-inline-start: 4px; }
  .lang { display: inline-block; font-size: 10px; font-weight: 700; padding: 1px 5px; border-radius: 3px; margin-inline-end: 6px;
    border: 1px solid var(--uui-color-border); color: var(--uui-color-text-alt); vertical-align: 1px; }
  .muted { color: var(--uui-color-text-alt); }
  .error { color: var(--uui-color-danger); margin: 0; }
  .note { color: var(--uui-color-text-alt); font-size: 12px; margin: 0; }
  .sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
  .tip { position: absolute; top: 0; transform: translateX(-50%); pointer-events: none; background: var(--uui-color-surface);
    border: 1px solid var(--uui-color-border); border-radius: 6px; padding: 6px 10px; font-size: 12px; white-space: nowrap;
    box-shadow: var(--uui-shadow-depth-1); color: var(--uui-color-text-alt); z-index: 1; }
  .tip b { font-size: 15px; color: var(--uui-color-text); }
  .tick { fill: var(--uui-color-text-alt); font-size: 11px; font-variant-numeric: tabular-nums; }
  .gridline { stroke: var(--uui-color-divider-standalone, #e3e3e3); stroke-width: 1; }
  svg:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; border-radius: 4px; }
`;
