/* "Funnels" dashboard (Statistics section): editors define two to eight pages in order (e.g. Home → Find a doctor →
   Doctor page) and see how many visitors reached each step and where they left. A visitor reaches a step when, on the
   same day, they viewed every earlier step in order (other pages in between are fine).
   Data: /umbraco/management/api/v1/ehc/stats/funnels (users whose group has the Statistics section). */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import '@umbraco-cms/backoffice/document';   // defines <umb-input-document> (the page picker)
import { number, big, get, send, icon, card, segmented, about, listStyles } from './stats-common.js';

const PERIODS = [
  { id: 7, label: '7 days' },
  { id: 30, label: '30 days' },
  { id: 90, label: '90 days' },
  { id: 365, label: '12 months' },
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
    if (this._loading) return html`<div class="loading"><uui-loader></uui-loader></div>`;
    return html`
      ${this._error && !this._editing ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${this._editing ? this.#editor() : this._funnels?.length ? this.#viewer() : this.#empty()}
      ${about(html`A visitor reaches a step when, on the same day, they viewed every earlier step in order; other pages in
        between are fine, and the Arabic and English versions of a page count as the same page. Visitors are counted once
        per day (as on the Visitors dashboard). Results are worked out every night and kept; when the steps change, the
        funnel is recounted from the page views still available (the last 30 days).`)}
    `;
  }

  #empty() {
    return html`<section class="card intro">
      <span class="intro-icon">${icon('funnel')}</span>
      <h2>See where visitors give up</h2>
      <p>A funnel shows how many visitors go through a series of pages, and at which page they leave. For example:
        Home → Find a doctor → a doctor's page.</p>
      <button class="btn primary" type="button" @click=${() => this.#edit(null)}>${icon('plus')}Create a funnel</button>
    </section>`;
  }

  #viewer() {
    const funnel = this._funnels.find((f) => f.id === this._selected);
    return html`
      <header class="page-head">
        <div class="page-title">
          <h2>Funnels</h2>
          <p>${icon('funnel')}${this._funnels.length} ${this._funnels.length === 1 ? 'funnel' : 'funnels'} defined</p>
        </div>
        <div class="controls">
          <label class="select"><span class="sr">Funnel</span>
            <select @change=${(e) => this.#select(e.target.value)}>
              ${this._funnels.map((f) => html`<option value=${f.id} ?selected=${f.id === this._selected}>${f.name}</option>`)}
            </select>
          </label>
          ${segmented('Period', PERIODS, this._days, (id) => this.#period(id))}
          <button class="btn" type="button" @click=${() => this.#edit(funnel)}>${icon('edit')}Edit</button>
          <button class="btn" type="button" @click=${() => this.#edit(null)}>${icon('plus')}New funnel</button>
        </div>
      </header>
      ${this._report ? this.#report(this._report) : html`<div class="loading"><uui-loader></uui-loader></div>`}
    `;
  }

  #report(r) {
    const steps = r.steps;
    const start = steps[0]?.visitors ?? 0;
    const end = steps[steps.length - 1]?.visitors ?? 0;
    const late = r.since > r.from;
    // the step that loses the largest share of the visitors who reached the one before it
    let worst = -1, worstShare = 0;
    steps.forEach((s, i) => {
      if (!i || !steps[i - 1].visitors) return;
      const share = (steps[i - 1].visitors - s.visitors) / steps[i - 1].visitors;
      if (share > worstShare) { worst = i; worstShare = share; }
    });
    return html`
      <div class="kpis">
        <div class="kpi"><span class="kpi-top">Started</span>
          <span class="kpi-value">${big(start)}</span><span class="delta-note">visitors on step 1</span></div>
        <div class="kpi"><span class="kpi-top">Completed</span>
          <span class="kpi-value">${big(end)}</span><span class="delta-note">visitors on step ${steps.length}</span></div>
        <div class="kpi"><span class="kpi-top">Completion rate</span>
          <span class="kpi-value">${start ? percent(end, start) + '%' : '–'}</span>
          <span class="meter" aria-hidden="true"><span style=${`width:${start ? percent(end, start) : 0}%`}></span></span></div>
        <div class="kpi"><span class="kpi-top">Biggest drop</span>
          <span class="kpi-value">${worst > 0 ? `${Math.round(worstShare * 100)}%` : '–'}</span>
          <span class="delta-note">${worst > 0 ? html`left before step ${worst + 1} <span dir="auto">(${steps[worst].name})</span>` : 'No visitors lost between steps'}</span></div>
      </div>
      ${card(r.name, html`
        <ol class="funnel">
          ${steps.map((s, i) => {
            const prev = i ? steps[i - 1].visitors : null;
            const left = prev != null ? prev - s.visitors : 0;
            const share = start ? (s.visitors / start) * 100 : 0;
            return html`
              ${i ? html`<li class=${i === worst ? 'drop worst' : 'drop'}>
                <span class="drop-line" aria-hidden="true"></span>
                ${left > 0
                  ? html`<span class="drop-chip">${icon('down')}${number.format(left)} left · ${percent(left, prev)}%</span>
                    <span class="muted">${percent(s.visitors, prev)}% continued to the next step</span>`
                  : html`<span class="muted">${prev ? 'Everyone continued' : ''}</span>`}
              </li>` : nothing}
              <li class="step">
                <span class="step-num">${i + 1}</span>
                <div class="step-body">
                  <div class="step-row">
                    <span class="page" dir="auto">${s.url ? html`<a href=${s.url} target="_blank" rel="noopener">${s.name}</a>` : s.name}</span>
                    <span class="count"><b>${number.format(s.visitors)}</b> visitors${i ? html` <span class="pct">${percent(s.visitors, start)}%</span>` : nothing}</span>
                  </div>
                  <div class="track"><span style=${`width:${start ? Math.max(1, share) : 0}%`}></span></div>
                </div>
              </li>`;
          })}
        </ol>
        ${start === 0 ? html`<p class="muted">No visitors started this funnel in this period yet.</p>` : nothing}`,
      { sub: `${day(late ? r.since : r.from)} – ${day(r.to)}${late ? ' (counted since the steps were set)' : ''}` })}
    `;
  }

  #editor() {
    const d = this._draft;
    return card(d.id ? 'Edit funnel' : 'New funnel', html`
      <div class="form">
        <label class="field">Name
          <uui-input label="Funnel name" .value=${d.name} maxlength="100" placeholder="e.g. Find and contact a doctor"
            @input=${(e) => { this._draft = { ...d, name: e.target.value }; }}></uui-input>
        </label>
        <div class="field">Steps <span class="muted">${MIN} to ${MAX} pages, in the order visitors should go through them</span></div>
        <ol class="steps">
          ${d.steps.map((key, i) => html`<li>
            <span class="step-num">${i + 1}</span>
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
          <button class="btn" type="button" ?disabled=${d.steps.length >= MAX}
            @click=${() => { this._draft = { ...d, steps: [...d.steps, ''] }; }}>${icon('plus')}Add step</button>
        </div>
        ${d.id ? html`<p class="callout">${icon('info')}<span>Changing the steps recounts the funnel from the last 30 days; older results are removed. Renaming keeps them.</span></p>` : nothing}
        ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
        <div class="buttons">
          <uui-button look="primary" label="Save" ?disabled=${this._saving} @click=${this.#save}>${this._saving ? 'Saving…' : 'Save'}</uui-button>
          <uui-button look="secondary" label="Cancel" @click=${() => { this._editing = false; this._error = null; }}>Cancel</uui-button>
          ${d.id ? html`<uui-button look="secondary" color="danger" label="Delete funnel" class="delete" @click=${this.#delete}>Delete funnel</uui-button>` : nothing}
        </div>
      </div>`);
  }

  static styles = [listStyles, css`
    .kpi { min-height: 0; }
    .meter { display: block; height: 6px; border-radius: 3px; margin-top: 6px; background: var(--ehc-accent-soft); }
    .meter span { display: block; height: 6px; border-radius: 3px; background: var(--ehc-accent); }
    .intro { display: grid; justify-items: center; text-align: center; gap: 12px; padding: 48px 24px; }
    .intro-icon { display: grid; place-items: center; width: 56px; height: 56px; border-radius: 16px; background: var(--ehc-accent-soft); color: var(--ehc-accent); }
    .intro-icon .i { width: 28px; height: 28px; }
    .intro h2 { font-size: 20px; font-weight: 800; }
    .intro p { max-width: 520px; color: var(--uui-color-text-alt); }
    ol { list-style: none; margin: 0; padding: 0; }
    .step { display: flex; gap: 14px; align-items: flex-start; }
    .step-num { display: inline-grid; place-items: center; width: 30px; height: 30px; border-radius: 50%; flex: none;
      background: var(--ehc-accent); color: #fff; font-size: 13px; font-weight: 800; }
    .step-body { flex: 1; min-width: 0; }
    .step-row { display: flex; flex-wrap: wrap; gap: 6px 14px; align-items: baseline; justify-content: space-between; padding-top: 4px; }
    .page { font-weight: 700; font-size: 14px; overflow-wrap: anywhere; }
    .page a { color: inherit; text-decoration: none; }
    .page a:hover { text-decoration: underline; }
    .count { color: var(--uui-color-text-alt); font-size: 13px; }
    .count b { color: var(--uui-color-text); font-size: 17px; }
    .pct { display: inline-block; margin-inline-start: 4px; padding: 1px 7px; border-radius: 999px; font-size: 12px; font-weight: 700;
      background: var(--ehc-accent-soft); color: var(--uui-color-text); }
    .track { height: 20px; margin-top: 8px; border-radius: 6px; background: var(--uui-color-surface-alt, #f1f1f3); overflow: hidden; }
    .track span { display: block; height: 100%; border-radius: 6px;
      background: var(--ehc-accent); }
    .drop { position: relative; display: flex; flex-wrap: wrap; gap: 6px 12px; align-items: center; min-height: 44px;
      padding-inline-start: 44px; font-size: 12px; }
    .drop-line { position: absolute; inset-inline-start: 14px; inset-block: 2px; width: 2px; border-radius: 1px; background: var(--uui-color-border); }
    .drop-chip { display: inline-flex; align-items: center; gap: 3px; padding: 2px 9px 2px 6px; border-radius: 999px; font-weight: 700;
      color: var(--uui-color-text); background: var(--uui-color-surface-alt, #f1f1f3); border: 1px solid var(--uui-color-border); }
    .drop-chip .i { width: 13px; height: 13px; stroke-width: 2.6; }
    .drop.worst .drop-chip { color: var(--ehc-bad); background: color-mix(in srgb, #d03b3b 10%, transparent); border-color: color-mix(in srgb, #d03b3b 35%, transparent); }
    .form { display: grid; gap: 18px; max-width: 780px; }
    .field { display: grid; gap: 6px; font-weight: 700; }
    .field .muted { font-weight: 400; font-size: 12px; }
    .steps { display: grid; gap: 12px; }
    .steps li { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; padding: 10px 12px; border-radius: 8px;
      border: 1px solid var(--uui-color-border); background: var(--uui-color-surface-alt, #f8f8fa); }
    .picker { flex: 1; min-width: 260px; }
    .tools { display: inline-flex; gap: 4px; }
    .buttons { display: flex; gap: 10px; padding-top: 6px; border-top: 1px solid var(--uui-color-divider, var(--uui-color-border)); padding-top: 16px; }
    .buttons .delete { margin-inline-start: auto; }
  `];
}

customElements.define('ehc-stats-funnels', EhcStatsFunnels);
