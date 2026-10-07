/* "Visitors" dashboard (Statistics section): visitors, visits and page views for a period, the daily trend, and the
   top pages, places, traffic sources and devices. Data: /umbraco/management/api/v1/ehc/stats/{summary|export}
   (users whose group has the Statistics section). Plain module using the backoffice's own Lit, element API and auth. */
import { LitElement, html, svg, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import {
  LANGUAGE, number, big, country, capital, get, niceStep, smoothPath, sparkline, icon, pageLabel, sourceLabel,
  barList, shareBar, card, segmented, delta, about, listStyles,
} from './stats-common.js';

const PRESETS = [
  { id: 'today', label: 'Today' },
  { id: 'yesterday', label: 'Yesterday' },
  { id: '7', label: '7 days' },
  { id: '30', label: '30 days' },
  { id: '90', label: '90 days' },
  { id: '365', label: '12 months' },
  { id: 'custom', label: 'Custom', icon: 'calendar' },
];
const MEASURES = {
  visitors: { label: 'Visitors', icon: 'users', of: (d) => d.visitors },
  visits: { label: 'Visits', icon: 'visits', of: (d) => d.visits },
  views: { label: 'Page views', icon: 'views', of: (d) => d.views },
  depth: { label: 'Pages per visit', icon: 'layers', of: (d) => (d.visits ? d.views / d.visits : 0), decimals: 1 },
};
const DEVICE_ICON = { desktop: 'desktop', mobile: 'mobile', tablet: 'tablet' };
const dayLabel = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', timeZone: 'UTC' });
const longDay = new Intl.DateTimeFormat('en-GB', { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });

const toDate = (s) => new Date(s + 'T00:00:00Z');
const iso = (d) => d.toISOString().slice(0, 10);
const addDays = (s, n) => { const d = toDate(s); d.setUTCDate(d.getUTCDate() + n); return iso(d); };
const daysBetween = (a, b) => Math.round((toDate(b) - toDate(a)) / 86400000) + 1;
const VIEWS = [{ key: 'views', title: 'Views' }, { key: 'visitors', title: 'Visitors' }];
const PAGE_TABS = [{ id: 'page', label: 'Pages' }, { id: 'culture', label: 'Languages' }];
const SOURCE_TABS = [{ id: 'source', label: 'Sources' }, { id: 'campaign', label: 'Campaigns' }];
const PLACE_TABS = [{ id: 'country', label: 'Countries' }, { id: 'region', label: 'Regions' }, { id: 'city', label: 'Cities' }];
const TECH_TABS = [{ id: 'device', label: 'Type' }, { id: 'browser', label: 'Browser' }, { id: 'os', label: 'OS' }];
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
    _tabs: { state: true },
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
    this._tabs = { page: 'page', source: 'source', place: 'country', tech: 'device' };
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
    const d = this._data;
    return html`
      <header class="page-head">
        <div class="page-title">
          <h2>Visitor statistics</h2>
          <p>${icon('calendar')}${d ? html`${longDay.format(toDate(d.from))} – ${longDay.format(toDate(d.to))}` : 'Loading…'}</p>
        </div>
        <div class="controls">
          ${segmented('Period', PRESETS, this._preset, (id) => this.#choose(id))}
          <button class="btn" type="button" @click=${this.#export} ?disabled=${!d}>${icon('download')}Export CSV</button>
        </div>
      </header>
      ${this._preset === 'custom' ? html`<div class="controls custom">
        <label class="select">From <input type="date" .value=${this._from ?? ''} max=${d?.today ?? ''} @change=${(e) => this.#custom('_from', e.target.value)}></label>
        <label class="select">To <input type="date" .value=${this._to ?? ''} max=${d?.today ?? ''} @change=${(e) => this.#custom('_to', e.target.value)}></label>
      </div>` : nothing}
      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${d && !d.geoBuilt ? html`<p class="callout" role="status">${icon('info')}<span>The location file is not loaded yet, so every visitor
        is counted under "Unknown" country. It downloads automatically a minute after the site starts (the server needs
        internet access), or place a .mmdb file in umbraco/Data/Geo.</span></p>` : nothing}
      ${this._loading && !d ? html`<div class="loading"><uui-loader></uui-loader></div>` : d ? this.#body(d) : nothing}
      ${about(html`Counted without cookies: each browser gets an anonymous id that changes every day, so a visitor is
        counted once per day and cannot be followed between days. IP addresses are used only to look up the country (and
        the region and city inside Saudi Arabia) and are never stored. A visit ends after 30 minutes without a page view.
        Over several days, "visitors" is the sum of each day's visitors. Single page views are kept for 30 days, daily
        totals permanently. Search engines, link previews and other automated traffic are not counted. Locations by
        <a href="https://db-ip.com" target="_blank" rel="noopener">DB-IP</a> (CC BY 4.0); city figures are approximate,
        as mobile networks often route through Riyadh.`)}
    `;
  }

  #body(d) {
    const top = d.top ?? {};
    const m = MEASURES[this._measure];
    return html`
      <div class=${this._loading ? 'kpis busy' : 'kpis'} role="group" aria-label="Totals for the period; choose one to chart it">
        ${Object.entries(MEASURES).map(([key, def]) => this.#tile(key, def, d))}
      </div>
      ${card(`${m.label} per day`, this._table ? this.#dayTable() : this.#chart(), {
        sub: this.#chartSub(),
        actions: segmented('Show as', [{ id: 'chart', label: 'Chart' }, { id: 'table', label: 'Table' }],
          this._table ? 'table' : 'chart', (id) => { this._table = id === 'table'; }),
      })}
      <div class="cards">
        ${this.#tabbed('page', 'Pages', PAGE_TABS, (t) => t === 'page'
          ? barList(top.page, pageLabel, VIEWS, 8)
          : shareBar(top.culture, (i) => LANGUAGE[i.value] ?? i.value, 'views'),
          { page: 'Most viewed; Arabic and English counted separately', culture: 'Share of page views by site language' })}
        ${this.#tabbed('source', 'Traffic sources', SOURCE_TABS, (t) => t === 'source'
          ? barList(top.source, (i) => sourceLabel(i.value), VISITS, 8)
          : barList(top.campaign, (i) => this.#campaign(i), VISITS, 8),
          { source: 'Where visits come from', campaign: 'Links tagged with utm_campaign' })}
        ${this.#tabbed('place', 'Locations', PLACE_TABS, (t) => ({
            country: () => barList(top.country, (i) => country(i.value), VIEWS, 8),
            region: () => barList(top.region, (i) => i.value.split('|')[1] || 'Unknown', VIEWS, 8),
            city: () => barList(top.city, (i) => this.#city(i), VIEWS, 8),
          }[t]()),
          { country: 'All countries', region: 'Inside Saudi Arabia', city: 'Inside Saudi Arabia; approximate' })}
        ${this.#tabbed('tech', 'Devices', TECH_TABS, (t) => t === 'device'
          ? shareBar(top.device, (i) => capital(i.value), 'views', (i) => DEVICE_ICON[i.value])
          : barList(top[t], (i) => i.value, VIEWS, 8),
          { device: 'Share of page views by device type', browser: 'Browsers', os: 'Operating systems' })}
      </div>
    `;
  }

  /** A card whose content switches between related lists with small tabs in its header. */
  #tabbed(key, title, tabs, body, subs) {
    const current = this._tabs[key];
    return card(title, body(current), {
      sub: subs[current],
      actions: segmented(`${title}: show`, tabs, current, (id) => { this._tabs = { ...this._tabs, [key]: id }; }),
    });
  }

  #chartSub() {
    const days = this._data.days ?? [];
    if (!days.length) return null;
    const m = MEASURES[this._measure];
    const values = days.map(m.of);
    const best = values.indexOf(Math.max(...values));
    if (!values[best]) return null;
    if (m.decimals) return `Highest ${values[best].toFixed(1)} on ${longDay.format(toDate(days[best].day))}`;
    return `${number.format(values.reduce((a, b) => a + b, 0))} in total · highest ${number.format(values[best])} on ${longDay.format(toDate(days[best].day))}`;
  }

  #tile(key, def, d) {
    const value = def.of(d.totals), previous = def.of(d.previous);
    const fmt = (v) => (def.decimals ? v.toFixed(def.decimals) : big(v));
    const n = daysBetween(d.from, d.to);
    const vs = `vs previous ${n === 1 ? 'day' : `${n} days`} (${fmt(previous)})`;
    return html`<button type="button" class="kpi" aria-pressed=${this._measure === key ? 'true' : 'false'}
      @click=${() => { this._measure = key; this._hover = null; }}>
      <span class="kpi-top">${def.label}</span>
      <span class="kpi-value">${fmt(value)}</span>
      ${delta(value, previous, vs)}
      ${sparkline((d.days ?? []).map(def.of))}
    </button>`;
  }

  #chart() {
    const days = this._data.days ?? [];
    if (days.length < 2) return html`<p class="muted">Choose a period of more than one day to see the daily trend.</p>`;
    const m = MEASURES[this._measure];
    const values = days.map(m.of);
    const fmt = (v) => (m.decimals ? v.toFixed(m.decimals) : number.format(v));
    const W = Math.max(this._width || 0, 320), H = 280, mg = { t: 24, r: 18, b: 30, l: 44 };
    const peak = Math.max(...values);
    if (!peak) return html`<p class="empty chart-empty">No ${m.label.toLowerCase()} in this period yet</p>`;
    const step = m.decimals ? Math.max(0.5, Math.ceil((peak / 4) * 2) / 2) : niceStep(peak / 4 || 1);
    const max = Math.max(step, Math.ceil(peak / step) * step);
    const plotW = W - mg.l - mg.r, plotH = H - mg.t - mg.b;
    const x = (i) => mg.l + (i * plotW) / (days.length - 1);
    const y = (v) => mg.t + plotH - (v / max) * plotH;
    const line = smoothPath(values.map((v, i) => [x(i), y(v)]));
    const area = `${line}L${x(days.length - 1).toFixed(1)},${y(0)}L${x(0).toFixed(1)},${y(0)}Z`;
    const ticks = [];
    for (let v = 0; v <= max + 1e-9; v += step) ticks.push(v);
    const every = Math.max(1, Math.ceil(days.length / Math.max(2, Math.floor(plotW / 76))));
    const h = this._hover;
    const best = values.indexOf(peak);
    const summary = `${m.label} per day, ${days.length} days, highest ${fmt(peak)} on ${longDay.format(toDate(days[best].day))}. Use the arrow keys to read each day.`;

    return html`
      <div class="chart">
        <svg width=${W} height=${H} role="img" tabindex="0" aria-label=${summary}
          @pointermove=${(e) => this.#point(e, days.length, mg.l, plotW)} @pointerleave=${() => { this._hover = null; }}
          @keydown=${(e) => this.#key(e, days.length)} @blur=${() => { this._hover = null; }}>
          <defs>
            <linearGradient id="wash" x1="0" y1="0" x2="0" y2="1">
              <stop class="wash-top" offset="0"></stop>
              <stop class="wash-end" offset="1"></stop>
            </linearGradient>
          </defs>
          ${ticks.map((v, i) => svg`
            <line class=${i ? 'gridline' : 'baseline'} x1=${mg.l} x2=${W - mg.r} y1=${y(v)} y2=${y(v)}></line>
            <text class="tick" x=${mg.l - 10} y=${y(v) + 4} text-anchor="end">${m.decimals ? v.toFixed(1) : number.format(v)}</text>`)}
          ${days.map((p, i) => i % every === 0
            ? svg`<text class="tick" x=${x(i)} y=${H - 8} text-anchor="middle">${dayLabel.format(toDate(p.day))}</text>` : nothing)}
          <path class="area" d=${area}></path>
          <path class="line" d=${line}></path>
          ${h == null && peak > 0 ? svg`
            <circle class="dot" cx=${x(best)} cy=${y(peak)} r="4.5"></circle>
            <text class="peak" x=${Math.min(Math.max(x(best), mg.l + 16), W - mg.r - 16)} y=${y(peak) - 11} text-anchor="middle">${fmt(peak)}</text>` : nothing}
          ${h != null ? svg`
            <line class="cross" x1=${x(h)} x2=${x(h)} y1=${mg.t} y2=${mg.t + plotH}></line>
            <circle class="dot" cx=${x(h)} cy=${y(values[h])} r="5"></circle>` : nothing}
        </svg>
        ${h != null ? html`
          <div class="tip" style=${`left:${Math.min(Math.max(x(h), 90), W - 90)}px`}>
            <div class="tip-head">${longDay.format(toDate(days[h].day))}</div>
            ${Object.entries(MEASURES).map(([k, def]) => html`<div class="tip-row">
              <b>${def.decimals ? def.of(days[h]).toFixed(1) : number.format(def.of(days[h]))}</b><span>${def.label.toLowerCase()}</span></div>`)}
          </div>` : nothing}
        <div class="sr" aria-live="polite">${h != null ? `${longDay.format(toDate(days[h].day))}: ${fmt(values[h])}` : ''}</div>
      </div>
    `;
  }

  #point(e, n, left, plotW) {
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
    return html`<div class="scroll"><table class="plain">
      <thead><tr><th>Day</th>${Object.values(MEASURES).map((m) => html`<th class="num">${m.label}</th>`)}</tr></thead>
      <tbody>${[...this._data.days].reverse().map((d) => html`<tr>
        <td>${longDay.format(toDate(d.day))}</td>
        ${Object.values(MEASURES).map((m) => html`<td class="num">${m.decimals ? m.of(d).toFixed(1) : number.format(m.of(d))}</td>`)}
      </tr>`)}</tbody>
    </table></div>`;
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
    .custom { margin-top: -8px; }
    .chart { position: relative; width: 100%; }
    .chart svg { display: block; touch-action: pan-y; }
    .line { fill: none; stroke: var(--ehc-accent); stroke-width: 2.25; stroke-linejoin: round; stroke-linecap: round; }
    .area { fill: url(#wash); }
    .wash-top { stop-color: var(--ehc-accent); stop-opacity: .22; }
    .wash-end { stop-color: var(--ehc-accent); stop-opacity: 0; }
    .cross { stroke: var(--uui-color-text-alt); stroke-width: 1; opacity: .5; }
    .dot { fill: var(--ehc-accent); stroke: var(--uui-color-surface, #fff); stroke-width: 2.5; }
    .peak { font-size: 12px; font-weight: 700; fill: var(--uui-color-text); paint-order: stroke;
      stroke: var(--uui-color-surface, #fff); stroke-width: 4px; stroke-linejoin: round; }
    .scroll { max-height: 420px; overflow: auto; }
    .chart-empty { min-height: 280px; }
  `];
}

customElements.define('ehc-stats-dashboard', EhcStatsDashboard);
