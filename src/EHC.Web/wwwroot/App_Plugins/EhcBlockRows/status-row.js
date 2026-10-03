/* Block List rows: a compact row per block — position, the block type's picture (its "Add block" thumbnail, from the
   data type's block configuration; hover to enlarge) or icon, the label, and small tags from the block's settings
   (blockSettings: hide, showFrom, showUntil, anchorId, themeOverride) and from its language ("not in this language").
   A click opens the block like Umbraco's own row; Umbraco's edit / settings / copy / delete buttons stay as they are.
   Pictures can be switched off per browser from the field's ⋯ menu (toggle-pictures.js). Emphasis markers (*word*)
   are left out of labels. Plain module using the backoffice's own Lit, element API and UUI. */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_BLOCK_ENTRY_CONTEXT } from '@umbraco-cms/backoffice/block';

import { PICTURES_EVENT, picturesOn } from './pictures.js';

/** A date-time value as stored by the date picker with time zone ({ date, timeZone }) or a plain string. */
function toDate(value) {
  const raw = value && typeof value === 'object' ? value.date : value;
  if (!raw || typeof raw !== 'string') return null;
  const d = new Date(raw);
  return Number.isNaN(d.getTime()) ? null : d;
}
const isOn = (v) => v === true || v === 1 || v === '1' || v === 'true';
const hasValue = (v) => (Array.isArray(v) ? v.length > 0 : v !== undefined && v !== null && v !== '');

export default class EhcStatusRow extends UmbElementMixin(LitElement) {
  static properties = {
    label: { attribute: false },
    icon: { attribute: false },
    content: { attribute: false },
    settings: { attribute: false },
    index: { attribute: false },
    config: { attribute: false },
    unpublished: { type: Boolean },
    contentInvalid: { attribute: false },
    settingsInvalid: { attribute: false },
    _thumbnail: { state: true },
    _pictures: { state: true },
    _wide: { state: true },
  };

  #onToggle = () => { this._pictures = picturesOn(); };

  constructor() {
    super();
    this._pictures = picturesOn();
    this.consumeContext(UMB_BLOCK_ENTRY_CONTEXT, (entry) => {
      if (!entry) return;
      this.observe(entry.blockType, (type) => { this._thumbnail = type?.thumbnail || undefined; }, 'ehcThumbnail');
    });
  }

  connectedCallback() {
    super.connectedCallback();
    window.addEventListener(PICTURES_EVENT, this.#onToggle);
  }

  disconnectedCallback() {
    window.removeEventListener(PICTURES_EVENT, this.#onToggle);
    super.disconnectedCallback();
  }

  #tags() {
    const s = this.settings ?? {};
    const tags = [];
    const short = (d) => d.toLocaleDateString(undefined, { day: 'numeric', month: 'short' });
    const now = new Date();
    if (this.unpublished) tags.push({ text: 'Not in this language', color: 'warning', title: 'This block is not shown in the language you are editing. Open it in this language, or use "Fill empty … from …" in the page menu.' });
    if (isOn(s.hide)) tags.push({ text: 'Hidden', color: 'danger', title: 'Not shown on the site ("Hide" is on in this block\'s settings).' });
    const from = toDate(s.showFrom);
    const until = toDate(s.showUntil);
    if (until && until <= now) tags.push({ text: `Ended ${short(until)}`, color: 'danger', title: `"Show until" has passed: not shown since ${until.toLocaleString()}.` });
    else if (from && from > now) tags.push({ text: `From ${short(from)}`, color: 'warning', title: `Scheduled: shown from ${from.toLocaleString()}.` });
    else if (until) tags.push({ text: `Until ${short(until)}`, color: 'default', title: `Shown until ${until.toLocaleString()}.` });
    if (hasValue(s.themeOverride)) tags.push({ text: 'Own theme', color: 'default', title: 'This section uses another theme (settings › Theme for this section).' });
    if (typeof s.anchorId === 'string' && s.anchorId.trim()) tags.push({ text: `#${s.anchorId.trim()}`, color: 'default', title: 'Anchor id, for menu links to this section.' });
    return tags;
  }

  // ------------------------------------------------------------------ larger picture on hover (top layer: never clipped)
  #peek(show, e) {
    const peek = this.renderRoot.querySelector('.peek');
    if (!peek?.showPopover) return;
    if (!show) { if (peek.matches(':popover-open')) peek.hidePopover(); return; }
    const r = e.currentTarget.getBoundingClientRect();
    const width = Math.min(460, window.innerWidth - r.right - 24);
    if (width < 200) return;
    peek.style.width = `${width}px`;
    peek.style.left = `${r.right + 12}px`;
    peek.style.top = `${Math.max(8, Math.min(r.top - 40, window.innerHeight - width * 0.6 - 8))}px`;
    peek.showPopover();
  }

  render() {
    // the site marks a highlighted word as *word*: show the plain words in the label
    const plain = Object.fromEntries(Object.entries(this.content ?? {}).map(([k, v]) => [k, typeof v === 'string' ? v.replace(/\*/g, '') : v]));
    const value = { ...plain, $settings: this.settings, $index: this.index };
    const tags = this.#tags();
    const muted = tags.some((t) => t.color === 'danger') || this.unpublished;
    const href = this.config?.showContentEdit !== false ? this.config?.editContentPath : undefined;
    const picture = this._pictures && this._thumbnail;
    const body = html`
      <span class="num" aria-hidden="true">${(this.index ?? 0) + 1}</span>
      ${picture
        ? html`<img class="thumb ${this._wide ? 'wide' : ''}" src=${this._thumbnail} alt="" loading="lazy" decoding="async"
            @load=${(e) => { this._wide = e.target.naturalWidth / Math.max(1, e.target.naturalHeight) > 2.6; }}
            @mouseenter=${(e) => this.#peek(true, e)} @mouseleave=${() => this.#peek(false)}>`
        : html`<umb-icon class="icon" name=${this.icon ?? 'icon-document'}></umb-icon>`}
      <span class="text">
        <umb-ufm-render inline .markdown=${this.label ?? ''} .value=${value}></umb-ufm-render>
        ${tags.length ? html`<span class="tags">${tags.map((t) => html`<uui-tag look="secondary" color=${t.color} title=${t.title}>${t.text}</uui-tag>`)}</span>` : nothing}
      </span>`;
    return html`
      ${href ? html`<a class="row ${muted ? 'muted' : ''}" href=${href}>${body}</a>` : html`<div class="row ${muted ? 'muted' : ''}">${body}</div>`}
      ${picture ? html`<img class="peek" popover="manual" src=${this._thumbnail} alt="">` : nothing}
    `;
  }

  static styles = css`
    :host { display: block; }
    .row { display: flex; align-items: center; gap: 12px; min-height: 52px; padding: 8px 12px; border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius, 3px); background: var(--uui-color-surface); color: var(--uui-color-text); text-decoration: none; }
    a.row:hover { border-color: var(--uui-color-border-emphasis); }
    a.row:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; }
    .num { min-width: 1.5em; text-align: end; font-size: 12px; color: var(--uui-color-text-alt); font-variant-numeric: tabular-nums; }
    .thumb { width: 96px; height: 54px; flex-shrink: 0; object-fit: cover; object-position: top; border-radius: 4px;
      border: 1px solid var(--uui-color-border); background: var(--uui-color-surface-alt); }
    .thumb.wide { object-fit: contain; object-position: center; }
    .icon { font-size: 20px; flex-shrink: 0; width: 24px; }
    .text { display: flex; flex-wrap: wrap; align-items: center; gap: 4px 10px; min-width: 0; }
    .tags { display: inline-flex; flex-wrap: wrap; gap: 4px; }
    uui-tag { font-size: 11px; }
    .muted .thumb, .muted .icon, .muted umb-ufm-render { opacity: .6; }
    .peek { margin: 0; padding: 0; border: 1px solid var(--uui-color-border); border-radius: 8px; box-shadow: var(--uui-shadow-depth-3, 0 8px 24px rgb(0 0 0 / .2));
      position: fixed; inset: auto; height: auto; background: var(--uui-color-surface); pointer-events: none; }
  `;
}

if (!customElements.get('ehc-status-row')) customElements.define('ehc-status-row', EhcStatusRow);
