/* "Visitors" dashboard (Statistics section): visitors, visits and page views for a period, the daily trend, and the
   top pages, places, traffic sources and devices. Data: /umbraco/management/api/v1/ehc/stats/{summary|export}
   (users whose group has the Statistics section). Plain module using the backoffice's own Lit, element API and auth. */
import { LitElement, html, svg, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { LANGUAGE, number, country, capital, get, niceStep, pageLabel, sourceLabel, barList, listStyles } from './stats-common.js';

const PRESETS = [
  { id: 'today', label: 'Today' },
  { id: 'yesterday', label: 'Yesterday' },
  { id: '7', label: 'Last 7 days' },
  { id: '30', label: 'Last 30 days' },
  { id: '90', label: 'Last 90 days' },
  { id: '365', label: 'Last 12 months' },
  { id: 'custom', label: 'Custom…' },
];
const MEASURES = { visitors: 'Visitors', visits: 'Visits', views: 'Page views' };
const dayLabel = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', timeZone: 'UTC' });
const longDay = new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });

const toDate = (s) => new Date(s + 'T00:00:00Z');
const iso = (d) => d.toISOString().slice(0, 10);
const addDays = (s, n) => { const d = toDate(s); d.setUTCDate(d.getUTCDate() + n); return iso(d); };
const VIEWS = [{ key: 'views', title: 'Views' }, { key: 'visitors', title: 'Visitors' }];
const VISITS = [{ key: 'visits', title: 'Visits' }, { key: 'visitors', title: 'Visitors' }];

export default class EhcStatsDashboard extends UmbElementMixin(LitElement) {
  static properties = {
    _preset: { state: true },
    _from: { state: true },
    _to: { state: true },
    _measure: { state: true },
    _data: { state: true },
    _error: { state: true },
    _loading: { state: true },
    _hover: { state: true },
    _width: { state: true },
    _table: { state: true },
  };

  #auth;
  #resize = new ResizeObserver((entries) => { this._width = Math.round(entries[0].contentRect.width); });

  constructor() {
    super();
    this._preset = '30';
    this._measure = 'visitors';
    this._loading = true;
    this._hover = null;
    this._width = 0;
    this._table = false;
    this.consumeContext(UMB_AUTH_CONTEXT, (auth) => {
      this.#auth = auth;
      this.#load();
    });
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#resize.disconnect();
  }

  updated() {
    const box = this.renderRoot.querySelector('.chart');
    if (box && box !== this._observed) {
      this.#resize.disconnect();
      this.#resize.observe(box);
      this._observed = box;
    }
  }

  #fetch(path) {
    return get(this.#auth, path);
  }

  #query() {
    return this._from && this._to ? `from=${this._from}&to=${this._to}` : '';
  }

  async #load() {
    this._loading = true;
    this._error = null;
    this._hover = null;
    try {
      this._data = await (await this.#fetch(`summary?${this.#query()}`)).json();
      this._from = this._data.from;
      this._to = this._data.to;
    } catch (e) {
      this._error = `Could not load visitor statistics (${e.message}).`;
    }
    this._loading = false;
  }

  #choose(id) {
    this._preset = id;
    if (id === 'custom') return;
    const today = this._data?.today ?? iso(new Date());
    const ranges = { today: [today, today], yesterday: [addDays(today, -1), addDays(today, -1)] };
    [this._from, this._to] = ranges[id] ?? [addDays(today, 1 - Number(id)), today];
    this.#load();
  }

  #custom(field, value) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return;
    this[field] = value;
    if (this._from && this._to && this._from <= this._to) this.#load();
  }

  async #export() {
    try {
      const blob = await (await this.#fetch(`export?${this.#query()}`)).blob();
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `visitor-statistics-${this._from}-${this._to}.csv`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 10000);
    } catch (e) {
      this._error = `Could not export (${e.message}).`;
    }
  }

  render() {
    return html`
      <div class="filters">
        <label>Period
          <select @change=${(e) => this.#choose(e.target.value)}>
            ${PRESETS.map((p) => html`<option value=${p.id} ?selected=${p.id === this._preset}>${p.label}</option>`)}
          </select>
        </label>
        ${this._preset === 'custom' ? html`
          <label>From <input type="date" .value=${this._from ?? ''} max=${this._data?.today ?? ''} @change=${(e) => this.#custom('_from', e.target.value)}></label>
          <label>To <input type="date" .value=${this._to ?? ''} max=${this._data?.today ?? ''} @change=${(e) => this.#custom('_to', e.target.value)}></label>` : nothing}
        ${this._data ? html`<span class="range">${longDay.format(toDate(this._data.from))} – ${longDay.format(toDate(this._data.to))}</span>` : nothing}
        <uui-button look="outline" compact label="Export CSV" @click=${this.#export} ?disabled=${!this._data}>Export CSV</uui-button>
      </div>
      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${this._data && !this._data.geoBuilt ? html`<p class="warn" role="status">The location file is not loaded yet, so every visitor is counted under "Unknown" country.
        It downloads automatically a minute after the site starts (the server needs internet access), or place a .mmdb file in umbraco/Data/Geo.</p>` : nothing}
      ${this._loading && !this._data ? html`<uui-loader></uui-loader>` : this._data ? this.#body() : nothing}
      <p class="note">Counted without cookies: each browser gets an anonymous id that changes every day, so a visitor
        is counted once per day and cannot be followed between days. IP addresses are used only to look up the
        country (and the region and city inside Saudi Arabia) and are never stored. A visit ends after 30 minutes
        without a page view. Over several days, "visitors" is the sum of each day's visitors. Single page views are kept
        for 30 days, daily totals permanently. Search engines, link previews and other automated traffic are not counted.
        Locations by <a href="https://db-ip.com" target="_blank" rel="noopener">DB-IP</a> (CC BY 4.0); city figures are
        approximate, as mobile networks often route through Riyadh.</p>
    `;
  }

  #body() {
    const d = this._data;
    const top = d.top ?? {};
    return html`
      <div class=${this._loading ? 'tiles busy' : 'tiles'}>
        ${this.#tile('Visitors', d.totals.visitors, d.previous.visitors)}
        ${this.#tile('Visits', d.totals.visits, d.previous.visits)}
        ${this.#tile('Page views', d.totals.views, d.previous.views)}
        ${this.#tile('Pages per visit', d.totals.visits ? d.totals.views / d.totals.visits : 0,
          d.previous.visits ? d.previous.views / d.previous.visits : 0, 1)}
      </div>
      <uui-box>
        <div slot="headline">${MEASURES[this._measure]} per day</div>
        <div class="bar" slot="header-actions">
          <select aria-label="Measure" @change=${(e) => { this._measure = e.target.value; this._hover = null; }}>
            ${Object.entries(MEASURES).map(([k, v]) => html`<option value=${k} ?selected=${k === this._measure}>${v}</option>`)}
          </select>
          <uui-button look="secondary" compact label=${this._table ? 'Show chart' : 'Show table'} @click=${() => { this._table = !this._table; }}>
            ${this._table ? 'Show chart' : 'Show table'}</uui-button>
        </div>
        ${this._table ? this.#dayTable() : this.#chart()}
      </uui-box>
      <uui-box headline="Pages">${barList(top.page, pageLabel, VIEWS)}</uui-box>
      <div class="grid">
        <uui-box headline="Where visits come from">${barList(top.source, (i) => sourceLabel(i.value), VISITS)}</uui-box>
        <uui-box headline="Countries">${barList(top.country, (i) => country(i.value), VIEWS)}</uui-box>
        <uui-box headline="Regions in Saudi Arabia">${barList(top.region, (i) => i.value.split('|')[1] || 'Unknown', VIEWS)}</uui-box>
        <uui-box headline="Cities in Saudi Arabia">${barList(top.city, (i) => this.#city(i), VIEWS)}</uui-box>
        <uui-box headline="Devices">${barList(top.device, (i) => capital(i.value), VIEWS)}</uui-box>
        <uui-box headline="Browsers">${barList(top.browser, (i) => i.value, VIEWS)}</uui-box>
        <uui-box headline="Operating systems">${barList(top.os, (i) => i.value, VIEWS)}</uui-box>
        <uui-box headline="Site language">${barList(top.culture, (i) => LANGUAGE[i.value] ?? i.value, VIEWS)}</uui-box>
        ${top.campaign?.length ? html`<uui-box headline="Campaign links (utm_campaign)">${barList(top.campaign, (i) => this.#campaign(i), VISITS)}</uui-box>` : nothing}
      </div>
    `;
  }

  #tile(label, value, previous, decimals = 0) {
    const fmt = (v) => (decimals ? v.toFixed(decimals) : number.format(v));
    let delta = nothing;
    if (previous > 0) {
      const change = Math.round(((value - previous) / previous) * 100);
      const dir = change > 0 ? 'up' : change < 0 ? 'down' : 'flat';
      delta = html`<span class="delta ${dir}">${dir === 'up' ? '▲' : dir === 'down' ? '▼' : '■'} ${change > 0 ? '+' : ''}${change}%
        <span class="vs">vs previous period (${fmt(previous)})</span></span>`;
    } else if (value > 0) {
      delta = html`<span class="delta flat"><span class="vs">no data for the previous period</span></span>`;
    }
    return html`<div class="tile"><span class="label">${label}</span><b class="value">${fmt(value)}</b>${delta}</div>`;
  }

  #chart() {
    const days = this._data.days ?? [];
    if (days.length < 2) return html`<p class="muted">Choose a period of more than one day to see the daily trend.</p>`;
    const key = this._measure;
    const W = Math.max(this._width || 0, 320), H = 240, m = { t: 14, r: 16, b: 30, l: 52 };
    const peak = Math.max(...days.map((x) => x[key]));
    const step = niceStep(peak / 4 || 1);
    const max = Math.max(step, Math.ceil(peak / step) * step);
    const plotW = W - m.l - m.r, plotH = H - m.t - m.b;
    const x = (i) => m.l + (i * plotW) / (days.length - 1);
    const y = (v) => m.t + plotH - (v / max) * plotH;
    const line = days.map((p, i) => `${i ? 'L' : 'M'}${x(i).toFixed(1)},${y(p[key]).toFixed(1)}`).join('');
    const area = `${line}L${x(days.length - 1).toFixed(1)},${y(0)}L${x(0).toFixed(1)},${y(0)}Z`;
    const ticks = [];
    for (let v = 0; v <= max; v += step) ticks.push(v);
    const every = Math.max(1, Math.ceil(days.length / Math.max(2, Math.floor(plotW / 70))));
    const h = this._hover;
    const total = days.reduce((s, p) => s + p[key], 0);
    const best = days.reduce((a, b) => (b[key] > a[key] ? b : a));
    const summary = `${MEASURES[key]} per day, ${days.length} days, ${number.format(total)} in total, highest ${number.format(best[key])} on ${longDay.format(toDate(best.day))}. Use the arrow keys to read each day.`;

    return html`
      <div class="chart">
        <svg width=${W} height=${H} role="img" tabindex="0" aria-label=${summary}
          @pointermove=${(e) => this.#point(e, x, days.length, m.l, plotW)} @pointerleave=${() => { this._hover = null; }}
          @keydown=${(e) => this.#key(e, days.length)} @blur=${() => { this._hover = null; }}>
          ${ticks.map((v) => svg`
            <line class="gridline" x1=${m.l} x2=${W - m.r} y1=${y(v)} y2=${y(v)}></line>
            <text class="tick" x=${m.l - 8} y=${y(v) + 4} text-anchor="end">${number.format(v)}</text>`)}
          ${days.map((p, i) => i % every === 0
            ? svg`<text class="tick" x=${x(i)} y=${H - 8} text-anchor="middle">${dayLabel.format(toDate(p.day))}</text>` : nothing)}
          <path class="area" d=${area}></path>
          <path class="line" d=${line}></path>
          ${h != null ? svg`
            <line class="cross" x1=${x(h)} x2=${x(h)} y1=${m.t} y2=${m.t + plotH}></line>
            <circle class="dot" cx=${x(h)} cy=${y(days[h][key])} r="4"></circle>` : nothing}
        </svg>
        ${h != null ? html`
          <div class="tip" style=${`left:${Math.min(Math.max(x(h), 70), W - 70)}px`}>
            <b>${number.format(days[h][key])}</b> <span>${MEASURES[key].toLowerCase()}</span><br>
            <span>${longDay.format(toDate(days[h].day))}</span>
          </div>` : nothing}
        <div class="sr" aria-live="polite">${h != null ? `${longDay.format(toDate(days[h].day))}: ${number.format(days[h][key])}` : ''}</div>
      </div>
    `;
  }

  #point(e, x, n, left, plotW) {
    const rect = e.currentTarget.getBoundingClientRect();
    const i = Math.round(((e.clientX - rect.left - left) / plotW) * (n - 1));
    this._hover = Math.min(n - 1, Math.max(0, i));
  }

  #key(e, n) {
    const moves = { ArrowLeft: -1, ArrowRight: 1, ArrowDown: -1, ArrowUp: 1 };
    let i = this._hover ?? n - 1;
    if (e.key in moves) i = this._hover == null ? n - 1 : i + moves[e.key];
    else if (e.key === 'Home') i = 0;
    else if (e.key === 'End') i = n - 1;
    else return;
    e.preventDefault();
    this._hover = Math.min(n - 1, Math.max(0, i));
  }

  #dayTable() {
    return html`<table>
      <thead><tr><th>Day</th>${Object.values(MEASURES).map((v) => html`<th class="num">${v}</th>`)}</tr></thead>
      <tbody>${[...this._data.days].reverse().map((d) => html`<tr>
        <td>${longDay.format(toDate(d.day))}</td>
        <td class="num">${number.format(d.visitors)}</td><td class="num">${number.format(d.visits)}</td><td class="num">${number.format(d.views)}</td>
      </tr>`)}</tbody>
    </table>`;
  }

  #city(i) {
    const [, region, city] = i.value.split('|');
    return html`${city || 'Unknown'} <span class="sub">${region}</span>`;
  }

  #campaign(i) {
    const [source, campaign] = i.value.split('|');
    return html`${campaign} <span class="sub">${source}</span>`;
  }

  static styles = [listStyles, css`
    :host { display: grid; gap: var(--uui-size-layout-1); padding: var(--uui-size-layout-1); }
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
    .filters label { display: inline-flex; gap: var(--uui-size-space-2); align-items: center; }
    .range { color: var(--uui-color-text-alt); font-size: 13px; }
    .filters uui-button { margin-inline-start: auto; }
    select, input[type='date'] { padding: 4px 8px; font: inherit; }
    .bar { display: flex; gap: var(--uui-size-space-3); align-items: center; }
    .tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: var(--uui-size-space-5); }
    .tiles.busy { opacity: .6; }
    .tile { background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); border-radius: 6px; padding: 14px 16px; display: grid; gap: 4px; }
    .tile .label { color: var(--uui-color-text-alt); font-size: 13px; }
    .tile .value { font-size: 30px; font-weight: 600; line-height: 1.2; }
    .delta { font-size: 13px; font-weight: 600; }
    .delta.up { color: var(--uui-color-positive-standalone, #1d7a3d); }
    .delta.down { color: var(--uui-color-danger-standalone, #b3261e); }
    .delta.flat, .vs { color: var(--uui-color-text-alt); }
    .vs { font-weight: 400; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: var(--uui-size-layout-1); align-items: start; }
    .chart { position: relative; width: 100%; }
    .chart svg { display: block; touch-action: pan-y; }
    .line { fill: none; stroke: var(--uui-color-interactive); stroke-width: 2; stroke-linejoin: round; stroke-linecap: round; }
    .area { fill: var(--uui-color-interactive); opacity: .1; }
    .cross { stroke: var(--uui-color-text-alt); stroke-width: 1; }
    .dot { fill: var(--uui-color-interactive); stroke: var(--uui-color-surface); stroke-width: 2; }
    .warn { margin: 0; padding: 8px 12px; border-radius: 6px; background: var(--uui-color-warning, #fbd142); color: var(--uui-color-warning-contrast, #000); }
  `];
}

customElements.define('ehc-stats-dashboard', EhcStatsDashboard);
