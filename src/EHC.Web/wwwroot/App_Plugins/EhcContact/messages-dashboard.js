/* "Contact messages" dashboard (Messages section): an inbox of the messages sent with the contact form — status tiles
   that filter, search and subject filter, the message list and a reading pane with reply / call actions and the status.
   Data: /umbraco/management/api/v1/ehc/contact/* (user groups with the Messages section only). Shares the look of the
   Statistics dashboards (stats-common.js); colours derive from the backoffice theme, so light and dark both work.
   Plain module using the backoffice's own Lit, element API and auth context — no build step. */
import { LitElement, html, svg, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { listStyles, segmented } from '/App_Plugins/EhcStats/stats-common.js';

const API = '/umbraco/management/api/v1/ehc/contact/';
const SERVICES = {
  complaint: { label: 'Complaint', color: '#d03b3b' },
  question: { label: 'Question', color: '#2a78d6' },
  suggestion: { label: 'Suggestion', color: '#1baf7a' },
  volunteering: { label: 'Health volunteering', color: '#eb6834' },
  research: { label: 'Research', color: '#8a5cd6' },
  other: { label: 'Other', color: '#8f8f8a' },
};
const STATUSES = {
  new: { label: 'New', icon: 'inbox', color: '#eda100' },
  progress: { label: 'In progress', icon: 'clock', color: '#2a78d6' },
  closed: { label: 'Closed', icon: 'check', color: '#1baf7a' },
};
const PERIODS = [{ id: 7, label: '7 days' }, { id: 30, label: '30 days' }, { id: 90, label: '90 days' }, { id: 365, label: '12 months' }];

const ICONS = {
  inbox: svg`<path d="M22 12h-6l-2 3h-4l-2-3H2"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11Z"/>`,
  clock: svg`<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>`,
  check: svg`<circle cx="12" cy="12" r="10"/><path d="m8 12 3 3 5-6"/>`,
  all: svg`<path d="M4 6h16M4 12h16M4 18h10"/>`,
  mail: svg`<rect x="2" y="4" width="20" height="16" rx="2"/><path d="m22 7-10 6L2 7"/>`,
  phone: svg`<path d="M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.1 4.2 2 2 0 0 1 4.1 2h3a2 2 0 0 1 2 1.7c.1 1 .4 1.9.7 2.8a2 2 0 0 1-.5 2.1L8 9.9a16 16 0 0 0 6 6l1.3-1.3a2 2 0 0 1 2.1-.4c.9.3 1.8.6 2.8.7a2 2 0 0 1 1.7 2Z"/>`,
  copy: svg`<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>`,
  search: svg`<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>`,
  download: svg`<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="m7 10 5 5 5-5"/><path d="M12 15V3"/>`,
  page: svg`<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6"/>`,
  globe: svg`<circle cx="12" cy="12" r="10"/><path d="M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10Z"/>`,
  shield: svg`<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z"/>`,
  user: svg`<circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/>`,
  back: svg`<path d="m15 18-6-6 6-6"/>`,
  refresh: svg`<path d="M21 12a9 9 0 1 1-2.6-6.4L21 8"/><path d="M21 3v5h-5"/>`,
};
const ico = (name) => html`<svg class="i" viewBox="0 0 24 24" aria-hidden="true">${ICONS[name] ?? nothing}</svg>`;

const utc = (s) => new Date(/(Z|[+-]\d\d:\d\d)$/.test(s) ? s : s + 'Z');
const fullDate = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' });
const shortDate = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short' });
const timeOnly = new Intl.DateTimeFormat('en-GB', { timeStyle: 'short' });
const relative = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

/** "5 min ago" today, "Yesterday", then "9 Oct". */
function when(date) {
  const minutes = Math.round((Date.now() - date) / 60000);
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return relative.format(-minutes, 'minute');
  const today = new Date(); today.setHours(0, 0, 0, 0);
  if (date >= today) return timeOnly.format(date);
  if (date >= today - 86400000) return 'Yesterday';
  return shortDate.format(date);
}

/** Two initials for the round badge (Latin names only: Arabic names get a person icon, initials are not used there). */
const latin = (name) => /^[A-Za-z]/.test((name || '').trim());
function initials(name) {
  const parts = (name || '?').trim().split(/\s+/).filter(Boolean);
  return (parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : (parts[0] || '?').slice(0, 2)).toUpperCase();
}

export default class EhcMessagesDashboard extends UmbElementMixin(LitElement) {
  static properties = {
    _days: { state: true },
    _service: { state: true },
    _status: { state: true },
    _query: { state: true },
    _rows: { state: true },
    _counts: { state: true },
    _selected: { state: true },
    _error: { state: true },
    _loading: { state: true },
    _copied: { state: true },
    _saving: { state: true },
  };

  #auth;

  constructor() {
    super();
    this._days = 30;
    this._service = '';
    this._status = '';
    this._query = '';
    this._rows = [];
    this._counts = {};
    this._selected = null;
    this._loading = true;
    this.consumeContext(UMB_AUTH_CONTEXT, (auth) => {
      this.#auth = auth;
      this.#load();
    });
  }

  async #fetch(path, init = {}) {
    const token = await this.#auth.getLatestToken();
    const response = await fetch(API + path, { ...init, headers: { ...(init.headers || {}), Authorization: `Bearer ${token}` } });
    if (!response.ok) throw new Error(String(response.status));
    return response;
  }

  #query() {
    const q = new URLSearchParams({ days: String(this._days) });
    if (this._service) q.set('service', this._service);
    if (this._status) q.set('status', this._status);
    return q.toString();
  }

  async #load() {
    this._loading = true;
    this._error = null;
    try {
      const [rows, counts] = await Promise.all([
        this.#fetch(`messages?${this.#query()}&take=500`).then((r) => r.json()),
        this.#fetch(`counts?days=${this._days}`).then((r) => r.json()),
      ]);
      this._rows = rows;
      this._counts = counts;
      // keep the open message when it is still in the list; otherwise open the newest on wide screens
      if (!rows.some((r) => r.id === this._selected)) this._selected = rows.length && matchMedia('(min-width: 1100px)').matches ? rows[0].id : null;
    } catch (e) {
      this._error = e.message === '403' ? 'Your user group does not have access to the Messages section.' : `Could not load messages (${e.message}).`;
    }
    this._loading = false;
  }

  async #export() {
    try {
      const blob = await (await this.#fetch(`export?${this.#query()}`)).blob();
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `contact-messages-last-${this._days}-days.csv`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    } catch (e) {
      this._error = `Export failed (${e.message}).`;
    }
  }

  async #setStatus(row, status) {
    if (row.status === status || this._saving) return;
    this._saving = true;
    try {
      await this.#fetch(`messages/${row.id}/status`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }) });
      this._counts = { ...this._counts, [row.status]: Math.max(0, (this._counts[row.status] || 1) - 1), [status]: (this._counts[status] || 0) + 1 };
      this._rows = this._rows.map((r) => (r.id === row.id ? { ...r, status, updatedUtc: new Date().toISOString() } : r));
    } catch (e) {
      this._error = `Could not change the status (${e.message}).`;
    }
    this._saving = false;
  }

  #set(key, value) {
    this[key] = value;
    this.#load();
  }

  async #copy(text) {
    try {
      await navigator.clipboard.writeText(text);
      this._copied = text;
      setTimeout(() => { if (this._copied === text) this._copied = null; }, 1800);
    } catch { /* clipboard blocked: nothing to do */ }
  }

  get #visible() {
    const q = this._query.trim().toLowerCase();
    if (!q) return this._rows;
    return this._rows.filter((r) => [r.name, r.email, r.phone, r.reference, r.message].some((v) => (v || '').toLowerCase().includes(q)));
  }

  /** Up / down arrows move through the list, like a mail client. */
  #listKeys(e, rows) {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    e.preventDefault();
    const i = rows.findIndex((r) => r.id === this._selected);
    const next = rows[Math.min(rows.length - 1, Math.max(0, i + (e.key === 'ArrowDown' ? 1 : -1)))];
    if (!next) return;
    this._selected = next.id;
    this.updateComplete.then(() => this.renderRoot.querySelector(`[data-id="${next.id}"]`)?.focus());
  }

  render() {
    const total = Object.values(this._counts).reduce((a, b) => a + b, 0);
    const rows = this.#visible;
    const open = rows.find((r) => r.id === this._selected) ?? null;
    return html`
      <header class="page-head">
        <div class="page-title">
          <h2>Contact messages</h2>
          <p>${ico('inbox')}Sent with the contact form on the website</p>
        </div>
        <div class="controls">
          ${segmented('Period', PERIODS, this._days, (id) => this.#set('_days', id))}
          <button class="btn" type="button" @click=${this.#load} title="Refresh">${ico('refresh')}<span class="sr">Refresh</span></button>
          <button class="btn" type="button" @click=${this.#export}>${ico('download')}Export CSV</button>
        </div>
      </header>

      <div class="kpis status-tiles">
        ${this.#tile('', 'All messages', 'all', total, null)}
        ${Object.entries(STATUSES).map(([k, s]) => this.#tile(k, s.label, s.icon, this._counts[k] ?? 0, s.color))}
      </div>

      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}

      <section class="inbox ${open ? 'has-open' : ''}">
        <div class="list-pane">
          <div class="filters">
            <label class="search">${ico('search')}<span class="sr">Search messages</span>
              <input type="search" placeholder="Search name, e-mail, phone, reference or text" .value=${this._query}
                @input=${(e) => { this._query = e.target.value; }}>
            </label>
            <label class="select">Subject
              <select @change=${(e) => this.#set('_service', e.target.value)}>
                <option value="" ?selected=${!this._service}>All subjects</option>
                ${Object.entries(SERVICES).map(([k, s]) => html`<option value=${k} ?selected=${k === this._service}>${s.label}</option>`)}
              </select>
            </label>
          </div>
          ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : this.#list(rows)}
        </div>
        <div class="read-pane">${open ? this.#detail(open) : html`<div class="placeholder">${ico('mail')}<p>${rows.length ? 'Select a message to read it.' : 'Nothing to show.'}</p></div>`}</div>
      </section>

      <p class="note privacy">${ico('shield')}<span>Messages contain personal data. Use them only to answer the message, do not copy them
        elsewhere and keep exported files secure. Messages are deleted automatically after the retention period (12 months by default).</span></p>
    `;
  }

  #tile(key, label, iconName, value, color) {
    const pressed = this._status === key;
    return html`<button type="button" class="kpi status" style=${color ? `--tone:${color}` : ''} aria-pressed=${pressed ? 'true' : 'false'}
      @click=${() => this.#set('_status', key)}>
      <span class="kpi-top"><span class="tone-icon">${ico(iconName)}</span>${label}</span>
      <span class="kpi-value">${value}</span>
      <span class="kpi-foot">${pressed ? 'Showing these' : 'Show these'}</span>
    </button>`;
  }

  #list(rows) {
    if (!rows.length) {
      const filtered = this._query || this._service || this._status;
      return html`<div class="empty-list">${ico('inbox')}<b>${filtered ? 'No messages match' : 'No messages yet'}</b>
        <span>${filtered ? 'Try another subject, status or search.' : `Nothing was sent in the last ${this._days} days.`}</span></div>`;
    }
    return html`<ul class="list" aria-label="Messages" @keydown=${(e) => this.#listKeys(e, rows)}>
      ${rows.map((r) => {
        const s = SERVICES[r.service] ?? SERVICES.other;
        const st = STATUSES[r.status] ?? STATUSES.new;
        const date = utc(r.createdUtc);
        return html`<li>
          <button type="button" class="item ${r.id === this._selected ? 'selected' : ''} ${r.status === 'new' ? 'unread' : ''}" data-id=${r.id}
            aria-current=${r.id === this._selected ? 'true' : 'false'} @click=${() => { this._selected = r.id; }}>
            <span class="avatar ${latin(r.name) ? '' : 'one'}" style="--tone:${s.color}" aria-hidden="true">${latin(r.name) ? initials(r.name) : ico('user')}</span>
            <span class="item-body">
              <span class="item-line">
                <span class="item-name"><bdi>${r.name}</bdi></span>
                <time datetime=${date.toISOString()} title=${fullDate.format(date)}>${when(date)}</time>
              </span>
              <span class="item-line meta">
                <span class="tag" style="--tone:${s.color}">${s.label}</span>
                <span class="status-dot" style="--tone:${st.color}">${st.label}</span>
              </span>
              <span class="preview" dir="auto">${r.message}</span>
            </span>
          </button>
        </li>`;
      })}
    </ul>`;
  }

  #detail(r) {
    const s = SERVICES[r.service] ?? SERVICES.other;
    const date = utc(r.createdUtc);
    const subject = encodeURIComponent(`${r.reference} — ${r.culture === 'ar-SA' ? 'تجمع الشرقية الصحي' : 'Eastern Health Cluster'}`);
    return html`<article class="detail" aria-label="Message ${r.reference}">
      <button type="button" class="btn back" @click=${() => { this._selected = null; }}>${ico('back')}All messages</button>
      <header class="detail-head">
        <span class="avatar big ${latin(r.name) ? '' : 'one'}" style="--tone:${s.color}" aria-hidden="true">${latin(r.name) ? initials(r.name) : ico('user')}</span>
        <div class="who">
          <h3><bdi>${r.name}</bdi></h3>
          <p class="muted">${fullDate.format(date)} · <span class="tag" style="--tone:${s.color}">${s.label}</span></p>
        </div>
        <button type="button" class="ref" title="Copy reference" @click=${() => this.#copy(r.reference)}>
          <span class="ref-label">${this._copied === r.reference ? 'Copied' : 'Reference'}</span>
          <code>${r.reference}</code>
        </button>
      </header>

      <div class="status-row">
        <span class="label">Status</span>
        <div class="seg status-seg" role="group" aria-label="Status">
          ${Object.entries(STATUSES).map(([k, st]) => html`<button type="button" style="--tone:${st.color}" aria-pressed=${r.status === k ? 'true' : 'false'}
            ?disabled=${this._saving} @click=${() => this.#setStatus(r, k)}>${ico(st.icon)}<span>${st.label}</span></button>`)}
        </div>
        ${r.updatedUtc ? html`<span class="muted small">Changed ${when(utc(r.updatedUtc))}</span>` : nothing}
      </div>

      <div class="message" dir="auto" lang=${r.culture === 'ar-SA' ? 'ar' : 'en'}>${r.message}</div>

      <div class="contact">
        <a class="contact-card" href=${`mailto:${r.email}?subject=${subject}`}>
          <span class="contact-icon">${ico('mail')}</span>
          <span><span class="contact-label">Reply by e-mail</span><span class="contact-value">${r.email}</span></span>
        </a>
        <a class="contact-card" href=${`tel:${r.phone}`}>
          <span class="contact-icon">${ico('phone')}</span>
          <span><span class="contact-label">Call</span><span class="contact-value" dir="ltr">${r.phone}</span></span>
        </a>
      </div>

      <dl class="facts">
        <div><dt>${ico('globe')}Language</dt><dd>${r.culture === 'ar-SA' ? 'Arabic' : 'English'}</dd></div>
        <div><dt>${ico('page')}Sent from</dt><dd>${r.pageUrl ? html`<a href=${r.pageUrl} target="_blank" rel="noopener">${r.page}</a>` : r.page}</dd></div>
      </dl>
    </article>`;
  }

  static styles = [listStyles, css`
    .i { width: 18px; height: 18px; }

    /* status tiles: a tile is a filter; its colour comes from the status */
    .status-tiles { grid-template-columns: repeat(auto-fit, minmax(min(100%, 180px), 1fr)); }
    .kpi.status { --tone: var(--ehc-accent); min-height: 104px; cursor: pointer; }
    .kpi.status[aria-pressed='true'] { border-color: var(--tone); background: color-mix(in srgb, var(--tone) 6%, var(--uui-color-surface, #fff)); }
    .kpi.status:hover { border-color: color-mix(in srgb, var(--tone) 50%, var(--uui-color-border)); }
    .tone-icon { display: inline-grid; place-items: center; width: 28px; height: 28px; border-radius: 8px;
      color: color-mix(in srgb, var(--tone) 80%, var(--uui-color-text)); background: color-mix(in srgb, var(--tone) 14%, transparent); }
    .tone-icon .i { width: 16px; height: 16px; }
    .kpi-foot { margin-top: auto; font-size: 12px; color: var(--uui-color-text-alt); }
    .kpi.status:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }

    /* inbox: list beside the reading pane */
    .inbox { display: grid; grid-template-columns: minmax(320px, 400px) minmax(0, 1fr); min-height: 560px;
      background: var(--uui-color-surface, #fff); border: 1px solid var(--uui-color-border); border-radius: var(--ehc-radius); overflow: hidden; }
    .list-pane { display: flex; flex-direction: column; min-width: 0; border-inline-end: 1px solid var(--uui-color-border); }
    .read-pane { min-width: 0; background: var(--uui-color-surface, #fff); }
    .filters { display: grid; gap: 10px; padding: 14px; border-bottom: 1px solid var(--uui-color-border); }
    .filters .select { justify-content: space-between; }
    .filters .select select { flex: 1; max-width: none; }
    .search { position: relative; display: flex; align-items: center; color: var(--uui-color-text-alt); }
    .search .i { position: absolute; inset-inline-start: 11px; width: 16px; height: 16px; pointer-events: none; }
    .search input { width: 100%; box-sizing: border-box; font: inherit; color: var(--uui-color-text); padding: 8px 12px; padding-inline-start: 34px; min-height: 36px;
      background: var(--uui-color-surface-alt, #f6f6f8); border: 1px solid transparent; border-radius: 8px; }
    .search input:focus { background: var(--uui-color-surface, #fff); border-color: var(--uui-color-border-emphasis, var(--uui-color-border)); }

    .list { list-style: none; margin: 0; padding: 6px; overflow-y: auto; max-height: 680px; display: grid; gap: 2px; align-content: start; }
    .item { all: unset; box-sizing: border-box; width: 100%; display: flex; gap: 12px; padding: 12px; border-radius: 8px; cursor: pointer; position: relative; }
    .item:hover { background: var(--uui-color-surface-alt, #f6f6f8); }
    .item.selected { background: color-mix(in srgb, var(--ehc-accent) 9%, var(--uui-color-surface, #fff)); }
    .item.selected::before { content: ''; position: absolute; inset-block: 10px; inset-inline-start: 0; width: 3px; border-radius: 3px; background: var(--ehc-accent); }
    .item:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: -2px; }
    .item-body { display: grid; gap: 4px; min-width: 0; flex: 1; }
    .item-line { display: flex; align-items: center; gap: 8px; min-width: 0; }
    .item-name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
    .item.unread .item-name { font-weight: 800; }
    .item time { flex: none; font-size: 12px; color: var(--uui-color-text-alt); }
    .item.unread time { color: var(--uui-color-text); font-weight: 700; }
    .preview { display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; color: var(--uui-color-text-alt); font-size: 13px; line-height: 1.5; }

    .avatar { --tone: var(--ehc-accent); flex: none; display: grid; place-items: center; width: 38px; height: 38px; border-radius: 50%;
      font-size: 13px; font-weight: 800; color: color-mix(in srgb, var(--tone) 75%, var(--uui-color-text));
      background: color-mix(in srgb, var(--tone) 16%, var(--uui-color-surface, #fff)); }
    .avatar.big { width: 52px; height: 52px; font-size: 17px; }
    .avatar.one .i { width: 20px; height: 20px; }
    .avatar.big.one .i { width: 26px; height: 26px; }
    .tag { --tone: #8f8f8a; display: inline-flex; align-items: center; padding: 1px 8px; border-radius: 999px; font-size: 11.5px; font-weight: 700;
      color: color-mix(in srgb, var(--tone) 75%, var(--uui-color-text)); background: color-mix(in srgb, var(--tone) 13%, transparent); white-space: nowrap; }
    .status-dot { display: inline-flex; align-items: center; gap: 5px; font-size: 12px; color: var(--uui-color-text-alt); white-space: nowrap; }
    .status-dot::before { content: ''; width: 7px; height: 7px; border-radius: 50%; background: var(--tone); }

    .empty-list, .placeholder { display: grid; place-items: center; align-content: center; gap: 6px; text-align: center; padding: 48px 24px; color: var(--uui-color-text-alt); flex: 1; }
    .empty-list .i, .placeholder .i { width: 34px; height: 34px; stroke-width: 1.5; margin-bottom: 6px; }
    .empty-list b { color: var(--uui-color-text); font-size: 14px; }
    .placeholder { height: 100%; box-sizing: border-box; }

    /* reading pane */
    .detail { display: grid; gap: 22px; padding: 24px 28px 28px; align-content: start; }
    .back { display: none; justify-self: start; }
    .detail-head { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
    .who { flex: 1; min-width: 200px; display: grid; gap: 4px; }
    .who h3 { font-size: 19px; font-weight: 800; }
    .who p { font-size: 13px; display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
    .ref { all: unset; box-sizing: border-box; cursor: pointer; display: grid; gap: 2px; padding: 8px 14px; border-radius: 8px; text-align: end;
      border: 1px dashed var(--uui-color-border-emphasis, var(--uui-color-border)); }
    .ref:hover { background: var(--uui-color-surface-alt, #f6f6f8); }
    .ref:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
    .ref-label { font-size: 11px; font-weight: 700; letter-spacing: .04em; text-transform: uppercase; color: var(--uui-color-text-alt); }
    .ref code { font: 700 14px/1.3 ui-monospace, Consolas, monospace; letter-spacing: .03em; }

    .status-row { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; padding: 12px 14px; border-radius: 8px; background: var(--uui-color-surface-alt, #f6f6f8); }
    .status-row .label { font-size: 13px; font-weight: 700; }
    .status-seg { background: var(--uui-color-surface, #fff); }
    .status-seg button[aria-pressed='true'] { color: color-mix(in srgb, var(--tone) 75%, var(--uui-color-text));
      background: color-mix(in srgb, var(--tone) 13%, var(--uui-color-surface, #fff)); border-color: color-mix(in srgb, var(--tone) 45%, transparent); }
    .status-seg button[disabled] { cursor: progress; }
    .small { font-size: 12px; }

    .message { white-space: pre-wrap; overflow-wrap: anywhere; font-size: 15px; line-height: 1.85; padding: 20px 22px; border-radius: 8px;
      border: 1px solid var(--uui-color-border); max-width: 78ch; }

    .contact { display: grid; gap: 12px; grid-template-columns: repeat(auto-fit, minmax(min(100%, 240px), 1fr)); max-width: 78ch; }
    .contact-card { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border-radius: 8px; text-decoration: none; color: inherit;
      border: 1px solid var(--uui-color-border); min-width: 0; }
    .contact-card:hover { border-color: var(--ehc-accent); background: var(--ehc-accent-soft); }
    .contact-card:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
    .contact-card > span:last-child { display: grid; min-width: 0; }
    .contact-icon { display: grid; place-items: center; width: 36px; height: 36px; border-radius: 8px; flex: none; color: var(--ehc-accent); background: var(--ehc-accent-soft); }
    .contact-label { font-size: 12px; font-weight: 700; color: var(--uui-color-text-alt); }
    .contact-value { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

    .facts { display: flex; flex-wrap: wrap; gap: 10px 32px; margin: 0; padding-top: 18px; border-top: 1px solid var(--uui-color-divider, var(--uui-color-border)); }
    .facts div { display: grid; gap: 3px; }
    .facts dt { display: flex; align-items: center; gap: 6px; font-size: 12px; font-weight: 700; color: var(--uui-color-text-alt); }
    .facts dt .i { width: 14px; height: 14px; }
    .facts dd { margin: 0; font-weight: 600; }
    .facts a { color: var(--ehc-accent); }

    .privacy { display: flex; gap: 8px; align-items: flex-start; max-width: 980px; }
    .privacy .i { width: 15px; height: 15px; margin-top: 1px; }

    /* narrow screens: the list, or the open message with a way back */
    @media (max-width: 1099px) {
      .inbox { grid-template-columns: minmax(0, 1fr); }
      .list-pane { border-inline-end: 0; }
      .inbox.has-open .list-pane { display: none; }
      .inbox:not(.has-open) .read-pane { display: none; }
      .back { display: inline-flex; }
      .detail { padding: 18px; }
    }
  `];
}

customElements.define('ehc-messages-dashboard', EhcMessagesDashboard);
