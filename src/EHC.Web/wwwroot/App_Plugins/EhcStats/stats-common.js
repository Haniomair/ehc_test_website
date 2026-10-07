/* Shared by the Statistics dashboards: API access, labels, icons, chart helpers, the ranked "top list" element and
   the common look (cards, segmented controls, stat tiles). Colours derive from the backoffice theme (--uui-color-*)
   so light, dark and high-contrast themes keep working; the chart accent and the series colours are fixed. */
import { LitElement, html, svg, css, nothing } from '@umbraco-cms/backoffice/external/lit';

export const API = '/umbraco/management/api/v1/ehc/stats';
export const MEDIUM = { direct: 'Direct', search: 'Search engines', social: 'Social media', referral: 'Other websites', campaign: 'Campaign links' };
export const LANGUAGE = { 'ar-SA': 'Arabic', 'en-US': 'English' };
export const number = new Intl.NumberFormat('en');
const compact = new Intl.NumberFormat('en', { notation: 'compact', maximumFractionDigits: 1 });

/** Big standalone figures: 1,284 up to 10,000, then 12.9K / 4.2M. */
export const big = (v) => (Math.abs(v) >= 10000 ? compact.format(v) : number.format(v));

/** Series colours for part-to-whole bars, in this fixed order (checked for colour-blind separation); the last is "Other". */
export const SERIES = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4'];
const OTHER = '#8f8f8a';

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

/** POST / PUT / DELETE with a JSON body; throws the server's message (ProblemDetails title) on failure. */
export async function send(auth, method, path, body) {
  const token = await auth.getLatestToken();
  const response = await fetch(`${API}/${path}`, {
    method,
    headers: { Authorization: `Bearer ${token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) {
    let message = String(response.status);
    try { message = (await response.json()).title || message; } catch { /* not JSON */ }
    throw new Error(message);
  }
  return response.status === 204 ? null : response.json();
}

/** Clean axis step: 1, 2 or 5 × a power of ten. */
export function niceStep(raw) {
  const pow = Math.pow(10, Math.floor(Math.log10(Math.max(raw, 1))));
  const n = raw / pow;
  return Math.max(1, (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * pow);
}

/** Smooth line through [x, y] points that never overshoots the data (monotone cubic), as an SVG path. */
export function smoothPath(points) {
  const n = points.length;
  if (!n) return '';
  const f = (v) => v.toFixed(1);
  if (n < 3) return points.map(([x, y], i) => `${i ? 'L' : 'M'}${f(x)},${f(y)}`).join('');
  const dx = [], slope = [], t = [];
  for (let i = 0; i < n - 1; i++) {
    dx[i] = points[i + 1][0] - points[i][0];
    slope[i] = dx[i] ? (points[i + 1][1] - points[i][1]) / dx[i] : 0;
  }
  t[0] = slope[0];
  t[n - 1] = slope[n - 2];
  for (let i = 1; i < n - 1; i++) {
    t[i] = slope[i - 1] * slope[i] <= 0 ? 0
      : (3 * (dx[i - 1] + dx[i])) / ((2 * dx[i] + dx[i - 1]) / slope[i - 1] + (dx[i] + 2 * dx[i - 1]) / slope[i]);
  }
  let d = `M${f(points[0][0])},${f(points[0][1])}`;
  for (let i = 0; i < n - 1; i++) {
    const [x0, y0] = points[i], [x1, y1] = points[i + 1], h = dx[i] / 3;
    d += `C${f(x0 + h)},${f(y0 + t[i] * h)} ${f(x1 - h)},${f(y1 - t[i + 1] * h)} ${f(x1)},${f(y1)}`;
  }
  return d;
}

/** Small trend line for a stat tile (no axes; the tile's figure carries the value). */
export function sparkline(values) {
  if (!values || values.length < 2 || !values.some((v) => v > 0)) return nothing;   // a flat zero line reads as a border
  const W = 120, H = 32, max = Math.max(...values, 1);
  const pts = values.map((v, i) => [(i * W) / (values.length - 1), H - 2 - (v / max) * (H - 4)]);
  const line = smoothPath(pts);
  return html`<svg class="spark" viewBox=${`0 0 ${W} ${H}`} preserveAspectRatio="none" aria-hidden="true">
    <path class="spark-line" d=${line}></path>
  </svg>`;
}

const ICONS = {
  users: svg`<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>`,
  visits: svg`<path d="M22 12h-4l-3 9L9 3l-3 9H2"/>`,
  views: svg`<path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/><circle cx="12" cy="12" r="3"/>`,
  layers: svg`<path d="m12 2 10 5-10 5L2 7l10-5Z"/><path d="m2 17 10 5 10-5"/><path d="m2 12 10 5 10-5"/>`,
  download: svg`<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="m7 10 5 5 5-5"/><path d="M12 15V3"/>`,
  desktop: svg`<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/>`,
  mobile: svg`<rect x="6" y="2" width="12" height="20" rx="2"/><path d="M11 18h2"/>`,
  tablet: svg`<rect x="4" y="2" width="16" height="20" rx="2"/><path d="M11 18h2"/>`,
  info: svg`<circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/>`,
  pause: svg`<rect x="6" y="4" width="4" height="16" rx="1"/><rect x="14" y="4" width="4" height="16" rx="1"/>`,
  play: svg`<path d="m7 4 13 8-13 8V4Z"/>`,
  calendar: svg`<rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/>`,
  funnel: svg`<path d="M22 3H2l8 9.46V19l4 2v-8.54L22 3Z"/>`,
  check: svg`<circle cx="12" cy="12" r="10"/><path d="m8 12 3 3 5-6"/>`,
  percent: svg`<path d="M19 5 5 19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/>`,
  globe: svg`<circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10Z"/>`,
  edit: svg`<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4Z"/>`,
  plus: svg`<path d="M12 5v14M5 12h14"/>`,
  click: svg`<path d="m9 9 5 12 1.8-5.2L21 14Z"/><path d="M7.2 2.2 8 5.1M5.1 8l-2.9-.8M14 4.1 12 6M6 12l-1.9 2"/>`,
  scroll: svg`<path d="M12 3v14M6 11l6 6 6-6M5 21h14"/>`,
  clock: svg`<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>`,
  pin: svg`<path d="M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 0 1 16 0Z"/><circle cx="12" cy="10" r="3"/>`,
  page: svg`<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6M8 13h8M8 17h5"/>`,
  arrival: svg`<path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4M10 17l5-5-5-5M15 12H3"/>`,
  up: svg`<path d="m6 15 6-6 6 6"/>`,
  down: svg`<path d="m6 9 6 6 6-6"/>`,
};

/** A line icon (decorative; the text beside it carries the meaning). */
export const icon = (name) => html`<svg class="i" viewBox="0 0 24 24" aria-hidden="true">${ICONS[name] ?? nothing}</svg>`;

/** A page row: language badge, then the page name linked to the live page. Value is "pageKey|culture". */
export function pageLabel(i) {
  const culture = i.value.split('|')[1];
  const lang = culture === 'ar-SA' ? 'AR' : culture === 'en-US' ? 'EN' : '';
  const name = i.url ? html`<a href=${i.url} target="_blank" rel="noopener" dir="auto">${i.label}</a>` : html`<span dir="auto">${i.label}</span>`;
  return html`${lang ? html`<span class="lang" title=${LANGUAGE[culture]}>${lang}</span>` : nothing}${name}`;
}

/** A traffic source row. Value is "medium|source". */
export function sourceLabel(value) {
  const [medium, source] = value.split('|');
  if (medium === 'direct') return html`Direct <span class="sub">typed, bookmarked or from an app</span>`;
  return html`${source || 'Unknown'} <span class="sub">${MEDIUM[medium] ?? medium}</span>`;
}

/** Ranked list: each row's label sits on a tinted bar for its share of the largest row; the first `limit` rows show. */
class EhcTopList extends LitElement {
  static properties = {
    items: { attribute: false },
    label: { attribute: false },
    columns: { attribute: false },
    limit: { type: Number },
    _all: { state: true },
  };

  constructor() {
    super();
    this.limit = 8;
    this._all = false;
  }

  render() {
    const items = this.items ?? [], columns = this.columns ?? [];
    if (!items.length || !columns.length) return html`<p class="empty">No data for this period</p>`;
    const key = columns[0].key;
    const most = Math.max(...items.map((i) => i[key]), 1);
    const shown = this._all ? items : items.slice(0, this.limit);
    return html`
      <table>
        <thead><tr><th><span class="sr">Name</span></th>${columns.map((c) => html`<th class="num">${c.title}</th>`)}</tr></thead>
        <tbody>${shown.map((i) => html`<tr>
          <td class="name"><span class="fill" style=${`width:${Math.max(1.5, (i[key] / most) * 100)}%`}></span><span class="text">${this.label(i)}</span></td>
          ${columns.map((c) => html`<td class="num">${number.format(i[c.key])}</td>`)}
        </tr>`)}</tbody>
      </table>
      ${items.length > this.limit ? html`<button class="more" aria-expanded=${this._all ? 'true' : 'false'} @click=${() => { this._all = !this._all; }}>
        ${this._all ? 'Show fewer' : `Show all ${number.format(items.length)}`}</button>` : nothing}`;
  }

  static styles = css`
    :host { display: block; }
    table { width: 100%; border-collapse: separate; border-spacing: 0 3px; table-layout: auto; }
    th { font-size: 11px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; color: var(--uui-color-text-alt);
      text-align: end; padding: 0 0 4px 14px; white-space: nowrap; }
    td { padding: 0; font-size: 13px; }
    .name { position: relative; width: 100%; }
    .fill { position: absolute; inset-block: 0; inset-inline-start: 0; border-radius: 6px;
      background: color-mix(in srgb, var(--ehc-accent, #2a78d6) 14%, transparent); }
    tr:hover .fill { background: color-mix(in srgb, var(--ehc-accent, #2a78d6) 24%, transparent); }
    .text { position: relative; display: block; padding: 7px 10px; line-height: 1.35; overflow-wrap: anywhere; }
    .num { text-align: end; white-space: nowrap; font-variant-numeric: tabular-nums; padding-inline-start: 14px; font-weight: 600; }
    .num + .num { color: var(--uui-color-text-alt); font-weight: 400; }
    a { color: inherit; text-decoration: none; }
    a:hover { text-decoration: underline; color: var(--uui-color-interactive-emphasis, inherit); }
    a:focus-visible, .more:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; border-radius: 3px; }
    .sub { color: var(--uui-color-text-alt); font-size: 12px; margin-inline-start: 4px; }
    .lang { display: inline-block; font-size: 10px; font-weight: 700; padding: 1px 5px; border-radius: 4px; margin-inline-end: 7px;
      background: var(--uui-color-surface, #fff); border: 1px solid var(--uui-color-border); color: var(--uui-color-text-alt); vertical-align: 1px; }
    .empty { display: grid; place-items: center; min-height: 160px; margin: 0; text-align: center; font-size: 13px;
      color: var(--uui-color-text-alt); border-radius: 6px; background: var(--uui-color-surface-alt, #f6f6f8); }
    .more { all: unset; cursor: pointer; margin-top: 8px; font-size: 13px; font-weight: 600; color: var(--uui-color-interactive); }
    .more:hover { text-decoration: underline; }
    .sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
  `;
}
if (!customElements.get('ehc-top-list')) customElements.define('ehc-top-list', EhcTopList);

/**
 * A top list: label on a bar for its share of the largest row, then one column per entry of `columns` ({ key, title });
 * the bar follows the first column. Longer lists show the first `limit` rows with a "Show all" button.
 */
export function barList(items, label, columns, limit = 8) {
  return html`<ehc-top-list .items=${items ?? []} .label=${label} .columns=${columns} limit=${limit}></ehc-top-list>`;
}

/**
 * Part-to-whole: one bar split by share (2px surface gaps), with a legend that always shows each name and value, so
 * colour is never the only way to tell the parts apart. More than five parts fold into "Other".
 */
export function shareBar(items, label, key, iconFor = null) {
  if (!items?.length) return html`<p class="empty">No data for this period</p>`;
  const total = items.reduce((s, i) => s + i[key], 0) || 1;
  let parts = items.map((i, n) => ({ name: label(i), value: i[key], icon: iconFor?.(i), color: SERIES[n] }));
  if (parts.length > SERIES.length) {
    const rest = parts.slice(SERIES.length - 1).reduce((s, p) => s + p.value, 0);
    parts = [...parts.slice(0, SERIES.length - 1), { name: 'Other', value: rest, color: OTHER }];
  }
  const pct = (v) => { const p = (v / total) * 100; return p > 0 && p < 1 ? '<1' : String(Math.round(p)); };
  return html`
    <div class="share-bar" aria-hidden="true">
      ${parts.map((p) => html`<span style=${`flex-grow:${p.value};background:${p.color}`}></span>`)}
    </div>
    <ul class="legend">
      ${parts.map((p) => html`<li>
        <span class="swatch" style=${`background:${p.color}`}></span>
        ${p.icon ? html`<span class="legend-icon">${icon(p.icon)}</span>` : nothing}
        <span class="legend-name">${p.name}</span>
        <b>${pct(p.value)}%</b>
        <span class="legend-count">${number.format(p.value)}</span>
      </li>`)}
    </ul>`;
}

/** A section card with a title, optional subtitle and actions. */
export function card(title, body, { sub = null, actions = nothing, wide = false } = {}) {
  return html`<section class=${wide ? 'card wide' : 'card'}>
    <header class="card-head">
      <div class="card-titles"><h3>${title}</h3>${sub ? html`<p class="card-sub">${sub}</p>` : nothing}</div>
      ${actions !== nothing ? html`<div class="card-actions">${actions}</div>` : nothing}
    </header>
    ${body}
  </section>`;
}

/** Segmented control: one pressed button among `options` ({ id, label, icon? }). */
export function segmented(label, options, current, onPick) {
  return html`<div class="seg" role="group" aria-label=${label}>
    ${options.map((o) => html`<button type="button" aria-pressed=${String(o.id) === String(current) ? 'true' : 'false'}
      @click=${() => onPick(o.id)}>${o.icon ? icon(o.icon) : nothing}<span>${o.label}</span></button>`)}
  </div>`;
}

/** Change against the previous period as a signed pill; up is good for every measure on these dashboards. */
export function delta(value, previous, vs) {
  if (!(previous > 0)) return value > 0 ? html`<span class="delta-note">No earlier data to compare</span>` : nothing;
  const change = Math.round(((value - previous) / previous) * 100);
  const dir = change > 0 ? 'up' : change < 0 ? 'down' : 'flat';
  return html`<span class="delta-row"><span class="delta ${dir}">${dir === 'flat' ? '±' : icon(dir)}${change > 0 ? '+' : ''}${change}%</span>
    <span class="delta-note">${vs}</span></span>`;
}

/** "About these numbers": the long explanation, folded away by default. */
export function about(body) {
  return html`<details class="about"><summary>${icon('info')}<span>How these numbers are counted</span></summary><div>${body}</div></details>`;
}

export const listStyles = css`
  :host {
    --ehc-accent: #2a78d6;
    --ehc-accent-soft: color-mix(in srgb, var(--ehc-accent) 12%, transparent);
    --ehc-good: color-mix(in srgb, #0ca30c 72%, var(--uui-color-text, #000));
    --ehc-bad: color-mix(in srgb, #d03b3b 78%, var(--uui-color-text, #000));
    --ehc-radius: 8px;
    display: grid; grid-template-columns: minmax(0, 1fr); gap: 20px; padding: 24px clamp(16px, 2.4vw, 32px) 32px; align-content: start;
    color: var(--uui-color-text); font-size: 14px;
  }
  h1, h2, h3, p { margin: 0; }
  .i { width: 18px; height: 18px; flex: none; fill: none; stroke: currentColor; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }

  /* page header: title and period on one side, the controls on the other */
  .page-head { display: flex; flex-wrap: wrap; gap: 16px 24px; align-items: center; justify-content: space-between; }
  .page-title h2 { font-size: 22px; font-weight: 800; letter-spacing: -.01em; }
  .page-title p { color: var(--uui-color-text-alt); font-size: 13px; margin-top: 3px; display: flex; align-items: center; gap: 6px; }
  .page-title .i { width: 15px; height: 15px; }
  .controls { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }

  .seg { display: inline-flex; flex-wrap: wrap; gap: 2px; padding: 2px; border-radius: 8px;
    background: var(--uui-color-surface-alt, #f1f1f3); border: 1px solid var(--uui-color-border); }
  .seg button { all: unset; box-sizing: border-box; display: inline-flex; align-items: center; gap: 6px; cursor: pointer;
    padding: 5px 12px; border-radius: 6px; border: 1px solid transparent; font-size: 13px; font-weight: 600; color: var(--uui-color-text-alt); white-space: nowrap; }
  .seg button .i { width: 15px; height: 15px; }
  .seg button:hover { color: var(--uui-color-text); }
  .seg button[aria-pressed='true'] { background: var(--uui-color-surface, #fff); color: var(--uui-color-text); border-color: var(--uui-color-border-emphasis, var(--uui-color-border)); }
  .seg button:focus-visible, .btn:focus-visible, .kpi:focus-visible, .about summary:focus-visible {
    outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }

  .btn { all: unset; box-sizing: border-box; display: inline-flex; align-items: center; gap: 7px; cursor: pointer; padding: 7px 14px;
    border-radius: 8px; font-size: 13px; font-weight: 600; background: var(--uui-color-surface, #fff); color: var(--uui-color-text);
    border: 1px solid var(--uui-color-border-emphasis, var(--uui-color-border)); }
  .btn:hover { background: var(--uui-color-surface-emphasis, var(--uui-color-surface-alt)); }
  .btn[disabled] { opacity: .5; cursor: default; }
  .btn.primary { background: var(--uui-color-interactive); color: var(--uui-color-surface, #fff); border-color: transparent; }
  .btn.primary:hover { background: var(--uui-color-interactive-emphasis, var(--uui-color-interactive)); }
  .btn .i { width: 16px; height: 16px; }

  .select { position: relative; display: inline-flex; align-items: center; gap: 8px; font-size: 13px; font-weight: 600; color: var(--uui-color-text-alt); }
  .select select, .select input, input[type='date'] { font: inherit; font-weight: 500; color: var(--uui-color-text); background: var(--uui-color-surface, #fff);
    border: 1px solid var(--uui-color-border-emphasis, var(--uui-color-border)); border-radius: 8px; padding: 7px 12px; min-height: 36px; box-sizing: border-box; }
  .select select { appearance: none; padding-inline-end: 34px; max-width: 460px; text-overflow: ellipsis; }
  .select:has(select)::after { content: ''; position: absolute; inset-inline-end: 14px; bottom: 15px; width: 6px; height: 6px; pointer-events: none;
    border: solid var(--uui-color-text-alt); border-width: 0 2px 2px 0; transform: rotate(45deg); }
  select:focus-visible, input:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 1px; }

  /* cards */
  .card { background: var(--uui-color-surface, #fff); border: 1px solid var(--uui-color-border); border-radius: var(--ehc-radius);
    padding: 16px 18px; min-width: 0; }
  .card-head { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; }
  .card-titles { min-width: 0; }
  .card-head h3 { font-size: 14px; font-weight: 700; }
  .card-sub { font-size: 12px; color: var(--uui-color-text-alt); margin-top: 2px; }
  .card-actions { margin-inline-start: auto; display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
  .card-actions .seg button { padding: 3px 10px; font-size: 12px; }
  .card-head { flex-wrap: wrap; row-gap: 10px; }
  .cards { display: grid; gap: 20px; align-items: stretch; grid-template-columns: repeat(2, minmax(0, 1fr)); }
  @media (max-width: 1000px) { .cards { grid-template-columns: minmax(0, 1fr); } }
  .empty { display: grid; place-items: center; min-height: 160px; margin: 0; text-align: center; font-size: 13px;
    color: var(--uui-color-text-alt); border-radius: 6px; background: var(--uui-color-surface-alt, #f6f6f8); }

  /* stat tiles */
  .kpis { display: grid; gap: 16px; grid-template-columns: repeat(auto-fit, minmax(min(100%, 210px), 1fr)); }
  .kpis.busy { opacity: .55; transition: opacity .2s; }
  .kpi { all: unset; box-sizing: border-box; position: relative; display: flex; flex-direction: column; gap: 6px; overflow: hidden;
    padding: 14px 16px; min-height: 128px; border-radius: var(--ehc-radius); background: var(--uui-color-surface, #fff);
    border: 1px solid var(--uui-color-border); }
  button.kpi { cursor: pointer; transition: border-color .15s, background-color .15s; }
  button.kpi:hover { border-color: color-mix(in srgb, var(--ehc-accent) 45%, var(--uui-color-border)); }
  .kpi[aria-pressed='true'] { border-color: var(--ehc-accent); background: color-mix(in srgb, var(--ehc-accent) 4%, var(--uui-color-surface, #fff)); }
  .kpi-top { display: flex; align-items: center; gap: 10px; color: var(--uui-color-text-alt); font-size: 13px; font-weight: 600; }
  .kpi-value { font-size: 30px; font-weight: 800; line-height: 1.1; letter-spacing: -.02em; color: var(--uui-color-text); }
  .kpi .spark { display: block; width: 100%; height: 28px; margin-top: auto; }
  .kpi .spark-line { fill: none; stroke: var(--ehc-accent); stroke-width: 2; vector-effect: non-scaling-stroke; stroke-linejoin: round; }
  .kpi:not([aria-pressed='true']) .spark-line { stroke: color-mix(in srgb, var(--ehc-accent) 55%, var(--uui-color-border)); }
  .delta-row { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
  .delta { display: inline-flex; align-items: center; gap: 2px; padding: 2px 8px 2px 5px; border-radius: 999px; font-size: 12px; font-weight: 700; }
  .delta .i { width: 14px; height: 14px; stroke-width: 2.6; }
  .delta.up { color: var(--ehc-good); background: color-mix(in srgb, #0ca30c 13%, transparent); }
  .delta.down { color: var(--ehc-bad); background: color-mix(in srgb, #d03b3b 12%, transparent); }
  .delta.flat { color: var(--uui-color-text-alt); background: var(--uui-color-surface-alt, #f1f1f3); padding-inline-start: 8px; }
  .delta-note { font-size: 12px; color: var(--uui-color-text-alt); }

  /* part-to-whole bar */
  .share-bar { display: flex; gap: 2px; height: 12px; border-radius: 6px; overflow: hidden; margin: 4px 0 14px; }
  .share-bar span { min-width: 3px; }
  .legend { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; }
  .legend li { display: flex; align-items: center; gap: 9px; padding: 6px 2px; font-size: 13px; border-bottom: 1px solid var(--uui-color-divider, var(--uui-color-border)); }
  .legend li:last-child { border-bottom: 0; }
  .swatch { width: 10px; height: 10px; border-radius: 3px; flex: none; }
  .legend-icon { color: var(--uui-color-text-alt); display: inline-flex; }
  .legend-icon .i { width: 15px; height: 15px; }
  .legend-name { flex: 1; min-width: 0; overflow-wrap: anywhere; }
  .legend b { font-variant-numeric: tabular-nums; }
  .legend-count { color: var(--uui-color-text-alt); font-variant-numeric: tabular-nums; min-width: 44px; text-align: end; }

  /* charts */
  .tip { position: absolute; top: 0; transform: translateX(-50%); pointer-events: none; z-index: 1; min-width: 120px;
    background: var(--uui-color-surface, #fff); border: 1px solid var(--uui-color-border-emphasis, var(--uui-color-border)); border-radius: 8px; padding: 8px 12px;
    font-size: 12px; white-space: nowrap; color: var(--uui-color-text-alt); }
  .tip b { font-size: 16px; color: var(--uui-color-text); }
  .tip-head { font-weight: 700; color: var(--uui-color-text); margin-bottom: 4px; font-size: 12px; }
  .tip-row { display: flex; align-items: baseline; gap: 8px; }
  .tip-row b { font-size: 14px; min-width: 32px; }
  .tick { fill: var(--uui-color-text-alt); font-size: 11px; font-variant-numeric: tabular-nums; }
  .gridline { stroke: var(--uui-color-divider-standalone, #e9e9ec); stroke-width: 1; }
  .baseline { stroke: var(--uui-color-border-emphasis, #c9c9cf); stroke-width: 1; }
  svg:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; border-radius: 6px; }

  table.plain { width: 100%; border-collapse: collapse; font-size: 13px; }
  table.plain th, table.plain td { text-align: start; padding: 8px 10px; border-bottom: 1px solid var(--uui-color-divider, var(--uui-color-border)); }
  table.plain th { font-size: 11px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; color: var(--uui-color-text-alt); }
  table.plain tbody tr:hover { background: var(--uui-color-surface-alt, #f6f6f8); }
  .num { text-align: end !important; white-space: nowrap; font-variant-numeric: tabular-nums; width: 1%; }

  .callout { display: flex; gap: 10px; align-items: flex-start; padding: 12px 14px; border-radius: 8px; font-size: 13px;
    background: color-mix(in srgb, #fab219 16%, var(--uui-color-surface, #fff)); border: 1px solid color-mix(in srgb, #fab219 55%, transparent); }
  .callout .i { color: color-mix(in srgb, #b77900 80%, var(--uui-color-text)); margin-top: 1px; }
  .error { display: flex; gap: 8px; align-items: center; padding: 10px 14px; border-radius: 8px; font-size: 13px; color: var(--ehc-bad);
    background: color-mix(in srgb, #d03b3b 9%, var(--uui-color-surface, #fff)); border: 1px solid color-mix(in srgb, #d03b3b 35%, transparent); }
  .muted { color: var(--uui-color-text-alt); }
  .note { color: var(--uui-color-text-alt); font-size: 12px; }
  .sub { color: var(--uui-color-text-alt); font-size: 12px; margin-inline-start: 4px; }
  .sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }
  .loading { display: grid; place-items: center; min-height: 240px; }

  .about { border-radius: 8px; border: 1px dashed var(--uui-color-border-emphasis, var(--uui-color-border)); padding: 0 14px; color: var(--uui-color-text-alt); font-size: 12.5px; line-height: 1.6; }
  .about summary { display: flex; gap: 8px; align-items: center; cursor: pointer; padding: 11px 0; font-weight: 600; font-size: 13px; list-style: none; border-radius: 6px; }
  .about summary::-webkit-details-marker { display: none; }
  .about summary .i { width: 16px; height: 16px; }
  .about[open] summary { color: var(--uui-color-text); }
  .about > div { padding-bottom: 14px; max-width: 980px; }
  .about a { color: inherit; }

  @media (prefers-reduced-motion: reduce) { *, *::before { transition: none !important; animation: none !important; } }
`;
