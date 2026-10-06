/* "Funnels" dashboard (Statistics section): editors define two to eight pages in order (e.g. Home → Find a doctor →
   Doctor page) and see how many visitors reached each step and where they left. A visitor reaches a step when, on the
   same day, they viewed every earlier step in order (other pages in between are fine).
   Data: /umbraco/management/api/v1/ehc/stats/funnels (users whose group has the Statistics section). */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import '@umbraco-cms/backoffice/document';   // defines <umb-input-document> (the page picker)
import { number, get, send, listStyles } from './stats-common.js';

const PERIODS = [
  { days: 7, label: 'Last 7 days' },
  { days: 30, label: 'Last 30 days' },
  { days: 90, label: 'Last 90 days' },
  { days: 365, label: 'Last 12 months' },
];
const MIN = 2, MAX = 8;
const longDay = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
const day = (s) => longDay.format(new Date(s + 'T00:00:00Z'));
const addDays = (s, n) => { const d = new Date(s + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10); };
const percent = (part, whole) => (whole ? Math.round((part / whole) * 100) : 0);

export default class EhcStatsFunnels extends UmbElementMixin(LitElement) {
  static properties = {
    _funnels: { state: true },
    _selected: { state: true },
    _report: { state: true },
    _days: { state: true },
    _editing: { state: true },
    _draft: { state: true },
    _error: { state: true },
    _saving: { state: true },
    _loading: { state: true },
  };

  #auth;
  #today = null;

  constructor() {
    super();
    this._funnels = null;
    this._days = 30;
    this._editing = false;
    this._loading = true;
    this.consumeContext(UMB_AUTH_CONTEXT, (auth) => {
      this.#auth = auth;
      this.#loadFunnels();
    });
  }

  async #loadFunnels(select) {
    this._error = null;
    try {
      this._funnels = await (await get(this.#auth, 'funnels')).json();
      const id = select ?? this._selected ?? this._funnels[0]?.id;
      this._selected = this._funnels.some((f) => f.id === id) ? id : this._funnels[0]?.id;
      if (this._selected != null) await this.#loadReport();
    } catch (e) {
      this._error = `Could not load funnels (${e.message}).`;
    }
    this._loading = false;
  }

  async #loadReport() {
    if (this._selected == null) return;
    const range = this.#today ? `?from=${addDays(this.#today, 1 - this._days)}&to=${this.#today}` : '';
    try {
      this._report = await (await get(this.#auth, `funnels/${this._selected}/report${range}`)).json();
      if (!this.#today) this.#today = this._report.to;
    } catch (e) {
      this._error = `Could not load the funnel (${e.message}).`;
    }
  }

  #select(id) {
    this._selected = Number(id);
    this._report = null;
    this.#loadReport();
  }

  #period(days) {
    this._days = Number(days);
    this.#loadReport();
  }

  #edit(funnel) {
    this._error = null;
    this._draft = funnel
      ? { id: funnel.id, name: funnel.name, steps: funnel.steps.map((s) => s.key) }
      : { id: null, name: '', steps: ['', ''] };
    this._editing = true;
  }

  #setStep(i, key) {
    const steps = [...this._draft.steps];
    steps[i] = key ?? '';
    this._draft = { ...this._draft, steps };
  }

  #moveStep(i, by) {
    const steps = [...this._draft.steps];
    [steps[i], steps[i + by]] = [steps[i + by], steps[i]];
    this._draft = { ...this._draft, steps };
  }

  #removeStep(i) {
    this._draft = { ...this._draft, steps: this._draft.steps.filter((_, j) => j !== i) };
  }

  async #save() {
    const d = this._draft;
    if (d.steps.some((s) => !s)) { this._error = 'Choose a page for every step.'; return; }
    this._saving = true;
    this._error = null;
    try {
      const saved = await send(this.#auth, d.id ? 'PUT' : 'POST', d.id ? `funnels/${d.id}` : 'funnels', { name: d.name, steps: d.steps });
      this._editing = false;
      this._report = null;
      await this.#loadFunnels(saved.id);
    } catch (e) {
      this._error = e.message;
    }
    this._saving = false;
  }

  async #delete() {
    const funnel = this._funnels.find((f) => f.id === this._draft.id);
    try {
      await umbConfirmModal(this, {
        headline: 'Delete funnel',
        content: `Delete "${funnel?.name}" and all its results? This cannot be undone.`,
        color: 'danger',
        confirmLabel: 'Delete',
      });
    } catch {
      return;   // cancelled
    }
    try {
      await send(this.#auth, 'DELETE', `funnels/${this._draft.id}`);
      this._editing = false;
      this._selected = null;
      this._report = null;
      await this.#loadFunnels();
    } catch (e) {
      this._error = `Could not delete (${e.message}).`;
    }
  }

  render() {
    if (this._loading) return html`<uui-loader></uui-loader>`;
    return html`
      ${this._error && !this._editing ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${this._editing ? this.#editor() : this._funnels?.length ? this.#viewer() : this.#empty()}
      <p class="note">A visitor reaches a step when, on the same day, they viewed every earlier step in order; other
        pages in between are fine, and the Arabic and English versions of a page count as the same page. Visitors are
        counted once per day (as on the Visitors dashboard). Results are worked out every night and kept; when the steps
        change, the funnel is recounted from the page views still available (the last 30 days).</p>
    `;
  }

  #empty() {
    return html`<uui-box headline="Funnels">
      <p>A funnel shows how many visitors go through a series of pages, and at which page they give up. For example:
        Home → Find a doctor → a doctor's page.</p>
      <uui-button look="primary" label="Create a funnel" @click=${() => this.#edit(null)}>Create a funnel</uui-button>
    </uui-box>`;
  }

  #viewer() {
    const funnel = this._funnels.find((f) => f.id === this._selected);
    return html`
      <div class="filters">
        <label>Funnel
          <select @change=${(e) => this.#select(e.target.value)}>
            ${this._funnels.map((f) => html`<option value=${f.id} ?selected=${f.id === this._selected}>${f.name}</option>`)}
          </select>
        </label>
        <label>Period
          <select @change=${(e) => this.#period(e.target.value)}>
            ${PERIODS.map((p) => html`<option value=${p.days} ?selected=${p.days === this._days}>${p.label}</option>`)}
          </select>
        </label>
        <span class="actions">
          <uui-button look="outline" compact label="Edit funnel" @click=${() => this.#edit(funnel)}>Edit</uui-button>
          <uui-button look="outline" compact label="New funnel" @click=${() => this.#edit(null)}>New funnel</uui-button>
        </span>
      </div>
      ${this._report ? this.#report(this._report) : html`<uui-loader></uui-loader>`}
    `;
  }

  #report(r) {
    const steps = r.steps;
    const start = steps[0]?.visitors ?? 0;
    const end = steps[steps.length - 1]?.visitors ?? 0;
    const late = r.since > r.from;
    return html`
      <div class="tiles">
        <div class="tile"><span class="label">Started (step 1)</span><b class="value">${number.format(start)}</b></div>
        <div class="tile"><span class="label">Completed (step ${steps.length})</span><b class="value">${number.format(end)}</b></div>
        <div class="tile"><span class="label">Completion rate</span><b class="value">${start ? percent(end, start) + '%' : '–'}</b></div>
      </div>
      <uui-box headline=${r.name}>
        <span slot="header-actions" class="muted">${day(late ? r.since : r.from)} – ${day(r.to)}${late ? ' (counted since the steps were set)' : ''}</span>
        <ol class="funnel">
          ${steps.map((s, i) => {
            const prev = i ? steps[i - 1].visitors : null;
            const left = prev != null ? prev - s.visitors : 0;
            return html`
              ${i ? html`<li class="drop" aria-hidden=${left ? 'false' : 'true'}>
                ${left ? html`▼ ${number.format(left)} left before this step (${percent(left, prev)}% of step ${i})` : html`&nbsp;`}</li>` : nothing}
              <li class="step">
                <div class="row">
                  <span class="num-badge">${i + 1}</span>
                  <span class="page">${s.url ? html`<a href=${s.url} target="_blank" rel="noopener">${s.name}</a>` : s.name}</span>
                  <span class="count"><b>${number.format(s.visitors)}</b> visitors${i ? html` · ${percent(s.visitors, start)}% of step 1` : nothing}</span>
                </div>
                <div class="share"><span style=${`width:${start ? Math.max(1, (s.visitors / start) * 100) : 0}%`}></span></div>
              </li>`;
          })}
        </ol>
        ${start === 0 ? html`<p class="muted">No visitors started this funnel in this period yet.</p>` : nothing}
      </uui-box>
    `;
  }

  #editor() {
    const d = this._draft;
    return html`
      <uui-box headline=${d.id ? 'Edit funnel' : 'New funnel'}>
        <div class="form">
          <label class="field">Name
            <uui-input label="Funnel name" .value=${d.name} maxlength="100" placeholder="e.g. Find and contact a doctor"
              @input=${(e) => { this._draft = { ...d, name: e.target.value }; }}></uui-input>
          </label>
          <ol class="steps">
            ${d.steps.map((key, i) => html`<li>
              <span class="num-badge">${i + 1}</span>
              <umb-input-document class="picker" .max=${1} .selection=${key ? [key] : []}
                @change=${(e) => this.#setStep(i, e.target.selection?.[0])}></umb-input-document>
              <span class="tools">
                <uui-button compact look="secondary" label=${`Move step ${i + 1} up`} ?disabled=${i === 0} @click=${() => this.#moveStep(i, -1)}>↑</uui-button>
                <uui-button compact look="secondary" label=${`Move step ${i + 1} down`} ?disabled=${i === d.steps.length - 1} @click=${() => this.#moveStep(i, 1)}>↓</uui-button>
                <uui-button compact look="secondary" label=${`Remove step ${i + 1}`} ?disabled=${d.steps.length <= MIN} @click=${() => this.#removeStep(i)}>Remove</uui-button>
              </span>
            </li>`)}
          </ol>
          <div>
            <uui-button look="outline" label="Add step" ?disabled=${d.steps.length >= MAX}
              @click=${() => { this._draft = { ...d, steps: [...d.steps, ''] }; }}>Add step</uui-button>
            <span class="muted">${MIN} to ${MAX} steps, in the order visitors should go through them.</span>
          </div>
          ${d.id ? html`<p class="muted">Changing the steps recounts the funnel from the last 30 days; older results are removed. Renaming keeps them.</p>` : nothing}
          ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
          <div class="buttons">
            <uui-button look="primary" label="Save" ?disabled=${this._saving} @click=${this.#save}>${this._saving ? 'Saving…' : 'Save'}</uui-button>
            <uui-button look="secondary" label="Cancel" @click=${() => { this._editing = false; this._error = null; }}>Cancel</uui-button>
            ${d.id ? html`<uui-button look="secondary" color="danger" label="Delete funnel" class="delete" @click=${this.#delete}>Delete funnel</uui-button>` : nothing}
          </div>
        </div>
      </uui-box>
    `;
  }

  static styles = [listStyles, css`
    :host { display: grid; gap: var(--uui-size-layout-1); padding: var(--uui-size-layout-1); }
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
    .filters label { display: inline-flex; gap: var(--uui-size-space-2); align-items: center; }
    .actions { margin-inline-start: auto; display: inline-flex; gap: var(--uui-size-space-3); }
    select { padding: 4px 8px; font: inherit; }
    .tiles { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: var(--uui-size-space-5); }
    .tile { background: var(--uui-color-surface); border: 1px solid var(--uui-color-border); border-radius: 6px; padding: 14px 16px; display: grid; gap: 4px; }
    .tile .label { color: var(--uui-color-text-alt); font-size: 13px; }
    .tile .value { font-size: 30px; font-weight: 600; line-height: 1.2; }
    ol { list-style: none; margin: 0; padding: 0; }
    .funnel .step { padding: 10px 0 8px; }
    .funnel .row { display: flex; flex-wrap: wrap; gap: 10px; align-items: baseline; }
    .funnel .page { flex: 1; min-width: 200px; font-weight: 600; overflow-wrap: anywhere; }
    .funnel .count { color: var(--uui-color-text-alt); font-size: 13px; }
    .funnel .count b { color: var(--uui-color-text); font-size: 16px; font-variant-numeric: tabular-nums; }
    .funnel .share { height: 14px; margin-top: 8px; border-radius: 3px; }
    .funnel .share span { height: 14px; border-radius: 0 4px 4px 0; }
    .drop { color: var(--uui-color-text-alt); font-size: 12px; padding-inline-start: 34px; }
    .num-badge { display: inline-grid; place-items: center; width: 24px; height: 24px; border-radius: 50%; flex: none;
      background: var(--uui-color-surface-alt); border: 1px solid var(--uui-color-border); font-size: 12px; font-weight: 700; }
    .form { display: grid; gap: var(--uui-size-space-5); max-width: 760px; }
    .field { display: grid; gap: 6px; font-weight: 600; }
    .steps { display: grid; gap: var(--uui-size-space-4); }
    .steps li { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
    .picker { flex: 1; min-width: 260px; }
    .tools { display: inline-flex; gap: 4px; }
    .buttons { display: flex; gap: var(--uui-size-space-3); }
    .buttons .delete { margin-inline-start: auto; }
  `];
}

customElements.define('ehc-stats-funnels', EhcStatsFunnels);
