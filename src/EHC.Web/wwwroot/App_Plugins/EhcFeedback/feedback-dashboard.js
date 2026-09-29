/* "Page feedback" dashboard (Content section): answers to "Was this page helpful?" per page, least helpful first,
   with reasons, recent comments and a CSV export. Data: /umbraco/management/api/v1/ehc/feedback/* (Content section
   users only). Plain module using the backoffice's own Lit, element API and auth context — no build step. */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';

const API = '/umbraco/management/api/v1/ehc/feedback/';
const REASONS = {
  outdated: 'Outdated',
  unclear: 'Hard to understand',
  missing: 'Missing information',
  broken: 'Broken link or error',
  other: 'Other',
};
const PERIODS = [30, 90, 180, 365];

export default class EhcFeedbackDashboard extends UmbElementMixin(LitElement) {
  static properties = {
    _days: { state: true },
    _rows: { state: true },
    _comments: { state: true },
    _page: { state: true },
    _error: { state: true },
    _loading: { state: true },
  };

  #auth;

  constructor() {
    super();
    this._days = 90;
    this._rows = [];
    this._comments = [];
    this._page = null;
    this._loading = true;
    this.consumeContext(UMB_AUTH_CONTEXT, (auth) => {
      this.#auth = auth;
      this.#load();
    });
  }

  async #get(path, asBlob) {
    const token = await this.#auth.getLatestToken();
    const response = await fetch(API + path, { headers: { Authorization: `Bearer ${token}` } });
    if (!response.ok) throw new Error(String(response.status));
    return asBlob ? response.blob() : response.json();
  }

  async #load() {
    this._loading = true;
    this._error = null;
    try {
      const page = this._page ? `&pageKey=${encodeURIComponent(this._page.pageKey)}` : '';
      const [rows, comments] = await Promise.all([
        this.#get(`summary?days=${this._days}`),
        this.#get(`comments?days=${this._days}${page}&take=100`),
      ]);
      this._rows = rows;
      this._comments = comments;
    } catch (e) {
      this._error = `Could not load feedback (${e.message}).`;
    }
    this._loading = false;
  }

  async #export() {
    try {
      const blob = await this.#get(`export?days=${this._days}`, true);
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `page-feedback-last-${this._days}-days.csv`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    } catch (e) {
      this._error = `Export failed (${e.message}).`;
    }
  }

  #setDays(e) {
    this._days = Number(e.target.value);
    this.#load();
  }

  #select(row) {
    this._page = this._page?.pageKey === row.pageKey ? null : row;
    this.#load();
  }

  #reasons(r) {
    const list = Object.entries(r || {}).sort((a, b) => b[1] - a[1]);
    return list.length ? list.map(([k, n]) => `${REASONS[k] || k} (${n})`).join(', ') : '—';
  }

  render() {
    const totals = this._rows.reduce((t, r) => ({ yes: t.yes + r.yes, no: t.no + r.no }), { yes: 0, no: 0 });
    const all = totals.yes + totals.no;
    return html`
      <uui-box headline="Was this page helpful?">
        <div class="bar" slot="header-actions">
          <label>Period
            <select @change=${this.#setDays} .value=${String(this._days)}>
              ${PERIODS.map((d) => html`<option value=${d} ?selected=${d === this._days}>Last ${d} days</option>`)}
            </select>
          </label>
          <uui-button look="secondary" label="Export CSV" @click=${this.#export}>Export CSV</uui-button>
        </div>
        ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
        ${this._loading ? html`<uui-loader></uui-loader>` : this.#table(all, totals)}
      </uui-box>
      <uui-box headline=${this._page ? `Comments — ${this._page.name}` : 'Recent comments'}>
        ${this._page ? html`<uui-button slot="header-actions" look="secondary" label="Show all pages" @click=${() => this.#select(this._page)}>Show all pages</uui-button>` : nothing}
        ${this.#commentList()}
      </uui-box>
      <p class="note">Answers are anonymous (no names, e-mail or IP addresses are stored). Numbers and e-mail addresses
        typed in comments are removed automatically. Answers older than the retention period (12 months by default)
        are deleted every day.</p>
    `;
  }

  #table(all, totals) {
    if (!this._rows.length) return html`<p>No answers in this period yet.</p>`;
    return html`
      <p class="summary"><b>${all}</b> answers · <b>${Math.round((100 * totals.yes) / all)}%</b> helpful</p>
      <table>
        <thead><tr><th>Page</th><th class="num">Helpful</th><th class="num">Yes</th><th class="num">No</th><th>Reasons for "No"</th><th></th></tr></thead>
        <tbody>
          ${this._rows.map((r) => html`
            <tr class=${this._page?.pageKey === r.pageKey ? 'selected' : ''}>
              <td>${r.url ? html`<a href=${r.url} target="_blank" rel="noopener">${r.name}</a>` : r.name}</td>
              <td class="num"><span class="pct ${r.percent < 50 ? 'low' : r.percent < 75 ? 'mid' : 'high'}">${r.percent}%</span></td>
              <td class="num">${r.yes}</td>
              <td class="num">${r.no}</td>
              <td>${this.#reasons(r.reasons)}</td>
              <td><uui-button compact look="outline" label="Comments for ${r.name}" @click=${() => this.#select(r)}>Comments</uui-button></td>
            </tr>`)}
        </tbody>
      </table>`;
  }

  #commentList() {
    if (this._loading) return nothing;
    if (!this._comments.length) return html`<p>No comments in this period.</p>`;
    return html`<ul class="comments">
      ${this._comments.map((c) => html`
        <li>
          <div class="meta">${new Date(/(Z|[+-]\d\d:\d\d)$/.test(c.createdUtc) ? c.createdUtc : c.createdUtc + 'Z').toLocaleString()} · ${c.page} · ${c.culture}${c.reason ? html` · ${REASONS[c.reason] || c.reason}` : nothing}</div>
          <div dir="auto">${c.comment}</div>
        </li>`)}
    </ul>`;
  }

  static styles = css`
    :host { display: grid; gap: var(--uui-size-layout-1); padding: var(--uui-size-layout-1); }
    .bar { display: flex; gap: var(--uui-size-space-4); align-items: center; }
    select { margin-inline-start: var(--uui-size-space-2); padding: 4px 8px; }
    table { width: 100%; border-collapse: collapse; }
    th, td { text-align: start; padding: 8px 10px; border-bottom: 1px solid var(--uui-color-border); vertical-align: top; }
    th { font-weight: 600; }
    .num { text-align: end; white-space: nowrap; }
    tr.selected td { background: var(--uui-color-surface-emphasis); }
    .pct { display: inline-block; min-width: 3.2em; padding: 2px 8px; border-radius: 999px; text-align: center; font-weight: 600; }
    .pct.low { background: #fde2e1; color: #8a1c1c; }
    .pct.mid { background: #fdf0d5; color: #7a4b00; }
    .pct.high { background: #dff3e7; color: #135c32; }
    .summary { margin-top: 0; }
    .comments { list-style: none; margin: 0; padding: 0; display: grid; gap: 12px; }
    .comments li { padding: 10px 12px; border: 1px solid var(--uui-color-border); border-radius: 6px; }
    .meta { font-size: 12px; color: var(--uui-color-text-alt); margin-bottom: 4px; }
    .error { color: var(--uui-color-danger); }
    .note { color: var(--uui-color-text-alt); font-size: 12px; margin: 0; }
  `;
}

customElements.define('ehc-feedback-dashboard', EhcFeedbackDashboard);
