/* "Core Web Vitals" dashboard (Content section): real-visitor LCP, INP and CLS at the 75th percentile (the figure
   Google uses), per device, plus the busiest pages worst first. Data: /umbraco/management/api/v1/ehc/vitals/summary
   (Content section users only). Plain module using the backoffice's own Lit, element API and auth context. */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

const API = '/umbraco/management/api/v1/ehc/vitals/summary';
const PERIODS = [7, 28, 90];
const METRICS = {
  LCP: { name: 'Largest Contentful Paint', what: 'How quickly the main content appears', good: '≤ 2.5 s' },
  INP: { name: 'Interaction to Next Paint', what: 'How quickly the page reacts to a tap or click', good: '≤ 200 ms' },
  CLS: { name: 'Cumulative Layout Shift', what: 'How much the layout jumps while loading', good: '≤ 0.1' },
};
const RATING = { good: 'Good', 'needs-improvement': 'Needs improvement', poor: 'Poor' };

function value(metric, v) {
  if (v == null) return '—';
  if (metric === 'CLS') return v.toFixed(2);
  return metric === 'LCP' ? (v / 1000).toFixed(2) + ' s' : Math.round(v) + ' ms';
}

export default class EhcVitalsDashboard extends UmbElementMixin(LitElement) {
  static properties = {
    _days: { state: true },
    _device: { state: true },
    _data: { state: true },
    _error: { state: true },
    _loading: { state: true },
  };

  #auth;

  constructor() {
    super();
    this._days = 28;
    this._device = 'mobile';
    this._data = null;
    this._loading = true;
    this.consumeContext(UMB_AUTH_CONTEXT, (auth) => {
      this.#auth = auth;
      this.#load();
    });
  }

  async #load() {
    this._loading = true;
    this._error = null;
    try {
      const token = await this.#auth.getLatestToken();
      const response = await fetch(`${API}?days=${this._days}&device=${this._device}`, { headers: { Authorization: `Bearer ${token}` } });
      if (!response.ok) throw new Error(String(response.status));
      this._data = await response.json();
    } catch (e) {
      this._error = `Could not load Core Web Vitals (${e.message}).`;
    }
    this._loading = false;
  }

  #set(field, e) {
    this[field] = field === '_days' ? Number(e.target.value) : e.target.value;
    this.#load();
  }

  render() {
    return html`
      <uui-box headline="Core Web Vitals from real visitors">
        <div class="bar" slot="header-actions">
          <label>Period
            <select @change=${(e) => this.#set('_days', e)} .value=${String(this._days)}>
              ${PERIODS.map((d) => html`<option value=${d} ?selected=${d === this._days}>Last ${d} days</option>`)}
            </select>
          </label>
        </div>
        ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
        ${this._loading ? html`<uui-loader></uui-loader>` : this.#overall()}
      </uui-box>
      <uui-box headline="Pages with the most visits">
        <div class="bar" slot="header-actions">
          <label>Device
            <select @change=${(e) => this.#set('_device', e)} .value=${this._device}>
              <option value="mobile" ?selected=${this._device === 'mobile'}>Mobile</option>
              <option value="desktop" ?selected=${this._device === 'desktop'}>Desktop</option>
            </select>
          </label>
        </div>
        ${this._loading ? nothing : this.#pages()}
      </uui-box>
      <p class="note">Measured in visitors' own browsers, only for visitors who accepted optional cookies. No cookies,
        names, IP addresses or visitor ids are stored: just the page, language, device type and the three values.
        Figures are the 75th percentile, as in Google's Search Console; a page needs enough visits before its figure is
        reliable. Values older than the retention period (90 days by default) are deleted every day.</p>
    `;
  }

  #overall() {
    const rows = this._data?.overall ?? [];
    if (!rows.length) return html`<p>No measurements in this period yet. They start arriving as soon as visitors accept optional cookies.</p>`;
    return html`<div class="cards">
      ${Object.keys(METRICS).map((m) => html`
        <div class="card">
          <h3>${m} <small>${METRICS[m].name}</small></h3>
          <p class="what">${METRICS[m].what}; good is ${METRICS[m].good}</p>
          ${['mobile', 'desktop'].map((d) => {
            const s = rows.find((r) => r.metric === m && r.device === d);
            return html`<div class="line">
              <span class="dev">${d === 'mobile' ? 'Mobile' : 'Desktop'}</span>
              ${s ? html`<b>${value(m, s.p75)}</b><span class="pill ${s.rating}">${RATING[s.rating]}</span><span class="meta">${s.goodPercent}% good · ${s.samples} visits</span>`
                : html`<span class="meta">no data</span>`}
            </div>`;
          })}
        </div>`)}
    </div>`;
  }

  #pages() {
    const pages = this._data?.pages ?? [];
    if (!pages.length) return html`<p>No measurements for this device in this period.</p>`;
    const rank = { poor: 0, 'needs-improvement': 1, good: 2 };
    const worst = (p) => Math.min(...Object.values(p.metrics).filter(Boolean).map((s) => rank[s.rating]));
    const sorted = [...pages].sort((a, b) => worst(a) - worst(b) || b.samples - a.samples);
    return html`<table>
      <thead><tr><th>Page</th><th class="num">Visits</th>${Object.keys(METRICS).map((m) => html`<th class="num">${m}</th>`)}</tr></thead>
      <tbody>
        ${sorted.map((p) => html`<tr>
          <td>${p.url ? html`<a href=${p.url} target="_blank" rel="noopener">${p.name}</a>` : p.name}</td>
          <td class="num">${p.samples}</td>
          ${Object.keys(METRICS).map((m) => {
            const s = p.metrics[m];
            return html`<td class="num">${s ? html`<span class="pill ${s.rating}" title=${RATING[s.rating]}>${value(m, s.p75)}</span>` : '—'}</td>`;
          })}
        </tr>`)}
      </tbody>
    </table>`;
  }

  static styles = css`
    :host { display: grid; gap: var(--uui-size-layout-1); padding: var(--uui-size-layout-1); }
    .bar { display: flex; gap: var(--uui-size-space-4); align-items: center; }
    select { margin-inline-start: var(--uui-size-space-2); padding: 4px 8px; }
    .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: var(--uui-size-space-5); }
    .card { border: 1px solid var(--uui-color-border); border-radius: 6px; padding: 12px 14px; display: grid; gap: 6px; }
    .card h3 { margin: 0; font-size: 16px; }
    .card h3 small { font-weight: 400; color: var(--uui-color-text-alt); font-size: 12px; margin-inline-start: 6px; }
    .what { margin: 0 0 4px; font-size: 12px; color: var(--uui-color-text-alt); }
    .line { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .dev { min-width: 60px; font-weight: 600; }
    .meta { font-size: 12px; color: var(--uui-color-text-alt); }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: start; padding: 8px 10px; border-bottom: 1px solid var(--uui-color-border); vertical-align: top; }
    th { font-weight: 600; }
    .num { text-align: end; white-space: nowrap; }
    .pill { display: inline-block; padding: 2px 8px; border-radius: 999px; font-weight: 600; font-size: 12px; }
    .pill.good { background: #dff3e7; color: #135c32; }
    .pill.needs-improvement { background: #fdf0d5; color: #7a4b00; }
    .pill.poor { background: #fde2e1; color: #8a1c1c; }
    .error { color: var(--uui-color-danger); }
    .note { color: var(--uui-color-text-alt); font-size: 12px; margin: 0; }
  `;
}

customElements.define('ehc-vitals-dashboard', EhcVitalsDashboard);
