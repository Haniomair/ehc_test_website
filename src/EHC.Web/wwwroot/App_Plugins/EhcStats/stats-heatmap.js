/* "Heatmaps" dashboard (Statistics section): where visitors click on a page and how far down they scroll, per device
   layout, from visitors who accepted optional cookies. The real page is shown in a same-origin frame at the device's
   width; clicks are drawn on a layer inside that page (so they follow it while scrolling) at the recorded element and
   position. The table beside it lists the most clicked elements and highlights each one.
   Data: /umbraco/management/api/v1/ehc/stats/heatmap (users whose group has the Statistics section). */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { number, get, LANGUAGE, listStyles } from './stats-common.js';

const DEVICES = {
  mobile: { label: 'Mobile', width: 390, height: 760 },
  tablet: { label: 'Tablet', width: 820, height: 1000 },
  desktop: { label: 'Desktop', width: 1280, height: 800 },
};
const PERIODS = [{ days: 7, label: 'Last 7 days' }, { days: 30, label: 'Last 30 days' }];
const HEAT = '214, 40, 40';          // one hue; overlapping clicks build up to a deeper red
const MARKS = [75, 50, 25];
const addDays = (s, n) => { const d = new Date(s + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10); };

export default class EhcStatsHeatmap extends UmbElementMixin(LitElement) {
  static properties = {
    _pages: { state: true },
    _page: { state: true },
    _device: { state: true },
    _days: { state: true },
    _mode: { state: true },
    _report: { state: true },
    _elements: { state: true },
    _missing: { state: true },
    _stageWidth: { state: true },
    _error: { state: true },
  };

  #auth;
  #today = null;
  #timers = [];
  #resize = new ResizeObserver((entries) => { this._stageWidth = entries[0].contentRect.width; });

  /** The preview is shrunk to fit; the page inside keeps the device's real width. */
  get #scale() {
    return this._stageWidth ? Math.min(1, this._stageWidth / DEVICES[this._device].width) : 1;
  }

  constructor() {
    super();
    this._device = 'desktop';
    this._days = 30;
    this._mode = 'clicks';
    this._stageWidth = 0;
    this._elements = [];
    this.consumeContext(UMB_AUTH_CONTEXT, (auth) => {
      this.#auth = auth;
      this.#loadPages();
    });
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.#resize.disconnect();
    this.#timers.forEach(clearTimeout);
  }

  updated() {
    const box = this.renderRoot.querySelector('.stage');
    if (box && box !== this._observed) {
      this.#resize.disconnect();
      this.#resize.observe(box);
      this._observed = box;
    }
  }

  #range() {
    return this.#today ? `from=${addDays(this.#today, 1 - this._days)}&to=${this.#today}` : '';
  }

  async #loadPages() {
    this._error = null;
    try {
      this._pages = await (await get(this.#auth, `heatmap/pages?${this.#range()}`)).json();
      if (!this._pages.some((p) => p.value === this._page)) this._page = this._pages[0]?.value;
      await this.#loadReport();
    } catch (e) {
      this._error = `Could not load heatmaps (${e.message}).`;
    }
  }

  async #loadReport() {
    this._report = null;
    this._elements = [];
    if (!this._page) return;
    try {
      const page = encodeURIComponent(this._page);
      this._report = await (await get(this.#auth, `heatmap?page=${page}&device=${this._device}&${this.#range()}`)).json();
      if (!this.#today) this.#today = this._report.to;
      // same page already in the frame (another layout or period): no load event, so draw once the layout settles
      await this.updateComplete;
      this.#timers.forEach(clearTimeout);
      this.#timers = [300, 1200].map((t) => setTimeout(() => this.#draw(), t));
    } catch (e) {
      this._error = `Could not load the heatmap (${e.message}).`;
    }
  }

  #change(field, value) {
    this[field] = field === '_days' ? Number(value) : value;
    if (field === '_days') this.#loadPages();
    else if (field === '_mode') this.#draw();
    else this.#loadReport();
  }

  get #frame() { return this.renderRoot.querySelector('iframe'); }

  /**
   * Page loaded in the frame: add heat-frame.css (hides the cookie notice, stops animations, styles the markers; a
   * same-origin file, as the site's Content-Security-Policy blocks inline styles), then draw, again once images and
   * fonts settle.
   */
  #loaded() {
    const doc = this.#frame?.contentDocument;
    if (!doc?.head) return;
    const link = doc.createElement('link');
    link.rel = 'stylesheet';
    link.href = new URL('./heat-frame.css', import.meta.url).pathname;
    doc.head.appendChild(link);
    this.#timers.forEach(clearTimeout);
    this.#timers = [400, 1500, 3500].map((t) => setTimeout(() => this.#draw(), t));
  }

  #layer(doc) {
    let layer = doc.getElementById('ehc-heat-layer');
    if (!layer) {
      layer = doc.createElement('div');
      layer.id = 'ehc-heat-layer';
      layer.setAttribute('aria-hidden', 'true');
      doc.body.appendChild(layer);
    }
    const W = doc.documentElement.scrollWidth, H = Math.min(doc.documentElement.scrollHeight, 16000);
    layer.style.cssText = `position:absolute;left:0;top:0;width:${W}px;height:${H}px;pointer-events:none;z-index:2147483646`;
    layer.replaceChildren();
    return { layer, W, H };
  }

  /** Where a recorded click is on the page now, or null when its element is not in this layout. */
  #locate(doc, win, selector) {
    let el = null;
    try { el = doc.querySelector(selector); } catch { el = null; }
    if (!el) return null;
    const r = el.getBoundingClientRect();
    if (!r.width || !r.height) return null;
    return { el, left: r.left + win.scrollX, top: r.top + win.scrollY, width: r.width, height: r.height };
  }

  #draw() {
    const frame = this.#frame, doc = frame?.contentDocument, win = frame?.contentWindow, report = this._report;
    if (!doc?.body || !report) return;
    const { layer, W, H } = this.#layer(doc);

    // elements, most clicked first (used by the table and for highlighting)
    const groups = new Map();
    for (const c of report.clicks) groups.set(c.selector, (groups.get(c.selector) ?? 0) + c.count);
    const total = report.clicks.reduce((s, c) => s + c.count, 0);
    let missing = 0;
    this._elements = [...groups].sort((a, b) => b[1] - a[1]).map(([selector, count]) => {
      const at = this.#locate(doc, win, selector);
      if (!at) missing += count;
      return { selector, count, share: total ? count / total : 0, label: at ? this.#label(at.el) : null };
    });
    this._missing = missing;

    if (this._mode === 'clicks') {
      const canvas = doc.createElement('canvas');
      canvas.width = W;
      canvas.height = H;
      layer.appendChild(canvas);
      const ctx = canvas.getContext('2d');
      const most = Math.max(1, ...report.clicks.map((c) => c.count));
      const radius = this._device === 'mobile' ? 18 : 22;
      for (const c of report.clicks) {
        const at = this.#locate(doc, win, c.selector);
        if (!at) continue;
        const x = at.left + (c.x / 100) * at.width, y = at.top + (c.y / 100) * at.height;
        const a = 0.3 + 0.6 * (c.count / most);
        const g = ctx.createRadialGradient(x, y, 0, x, y, radius);
        g.addColorStop(0, `rgba(${HEAT}, ${a})`);
        g.addColorStop(1, `rgba(${HEAT}, 0)`);
        ctx.fillStyle = g;
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fill();
      }
    } else {
      for (const mark of MARKS) {
        const depth = this.#depthFor(report.reach, mark);
        if (depth == null) continue;
        const line = doc.createElement('div');
        line.className = 'ehc-heat-mark';
        line.style.top = `${Math.round((depth / 100) * doc.documentElement.scrollHeight)}px`;
        const tag = doc.createElement('span');
        tag.textContent = `${mark}% of page views reached here`;
        line.appendChild(tag);
        layer.appendChild(line);
      }
    }
  }

  /** Depth (percent of the page) that the given share of page views reached, interpolated between 5 % steps. */
  #depthFor(reach, share) {
    if (!reach?.length || reach[0] === 0) return null;
    for (let i = 1; i < reach.length; i++) {
      if (reach[i] < share) {
        const step = 100 / (reach.length - 1), hi = reach[i - 1], lo = reach[i];
        return (i - 1) * step + (hi === lo ? 0 : ((hi - share) / (hi - lo)) * step);
      }
    }
    return 100;
  }

  #label(el) {
    const text = el.getAttribute('aria-label') || el.getAttribute('alt') || el.getAttribute('title') || el.textContent || '';
    const clean = text.replace(/\s+/g, ' ').trim();
    const kind = { a: 'Link', button: 'Button', img: 'Image', input: 'Field', select: 'List', textarea: 'Text box', summary: 'Expander', label: 'Label' }[el.localName] ?? 'Area';
    return { kind, text: clean.length > 70 ? clean.slice(0, 69) + '…' : clean };
  }

  #highlight(selector) {
    const frame = this.#frame, doc = frame?.contentDocument, win = frame?.contentWindow;
    if (!doc?.body) return;
    doc.querySelectorAll('.ehc-heat-box').forEach((b) => b.remove());
    if (!selector) return;
    const at = this.#locate(doc, win, selector);
    if (!at) return;
    at.el.scrollIntoView({ block: 'center' });
    const again = this.#locate(doc, win, selector);
    const box = doc.createElement('div');
    box.className = 'ehc-heat-box';
    box.style.cssText = `left:${again.left}px;top:${again.top}px;width:${again.width}px;height:${again.height}px`;
    doc.getElementById('ehc-heat-layer')?.appendChild(box);
  }

  #wheel(e) {
    e.preventDefault();
    this.#frame?.contentWindow?.scrollBy({ top: e.deltaY / this.#scale, left: 0 });
  }

  render() {
    const pages = this._pages;
    return html`
      <div class="filters">
        <label>Page
          <select @change=${(e) => this.#change('_page', e.target.value)} ?disabled=${!pages?.length}>
            ${(pages ?? []).map((p) => html`<option value=${p.value} ?selected=${p.value === this._page}>
              ${p.label} (${LANGUAGE[p.value.split('|')[1]] ?? ''}) · ${number.format(p.views)} views</option>`)}
          </select>
        </label>
        <label>Layout
          <select @change=${(e) => this.#change('_device', e.target.value)}>
            ${Object.entries(DEVICES).map(([k, d]) => html`<option value=${k} ?selected=${k === this._device}>${d.label} (${d.width} px)</option>`)}
          </select>
        </label>
        <label>Period
          <select @change=${(e) => this.#change('_days', e.target.value)}>
            ${PERIODS.map((p) => html`<option value=${p.days} ?selected=${p.days === this._days}>${p.label}</option>`)}
          </select>
        </label>
        <uui-button-group>
          <uui-button look=${this._mode === 'clicks' ? 'primary' : 'outline'} label="Clicks" @click=${() => this.#change('_mode', 'clicks')}>Clicks</uui-button>
          <uui-button look=${this._mode === 'scroll' ? 'primary' : 'outline'} label="Scroll reach" @click=${() => this.#change('_mode', 'scroll')}>Scroll reach</uui-button>
        </uui-button-group>
      </div>
      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${pages && !pages.length ? html`<uui-box headline="Heatmaps"><p>No clicks or scrolling recorded in this period yet. They are
        recorded only for visitors who choose "Accept all" in the cookie notice.</p></uui-box>` : nothing}
      ${this._report ? this.#body(this._report) : nothing}
      <p class="note">Recorded only for visitors who accepted optional cookies, for at most 50 clicks per page view: the
        clicked element and where inside it, and how far down the page they got. Nothing typed, no page text and no visitor
        id is recorded; data is deleted after 30 days. The page is shown as it is now, so clicks on parts that changed or
        that this layout hides cannot be placed. Scroll reach counts the lowest point of the page that was on screen.</p>
    `;
  }

  #body(r) {
    const d = DEVICES[this._device];
    const total = r.clicks.reduce((s, c) => s + c.count, 0);
    const reach = r.reach ?? [];
    return html`
      <div class="summary muted">${number.format(r.views)} page views and ${number.format(total)} clicks recorded on
        ${d.label.toLowerCase()} layouts${this._missing ? html` · ${number.format(this._missing)} clicks are on elements not shown in this layout now` : nothing}</div>
      <div class="split">
        <div class="stage">
          ${r.url ? html`
            <div class="viewport" style=${`width:${d.width * this.#scale}px;height:${d.height * this.#scale}px`}>
              <div class="scaled" style=${`width:${d.width}px;height:${d.height}px;transform:scale(${this.#scale})`}>
                <iframe title=${`Preview of ${r.label}`} src=${r.url} sandbox="allow-same-origin allow-scripts"
                  width=${d.width} height=${d.height} @load=${this.#loaded}></iframe>
                <div class="blocker" @wheel=${(e) => this.#wheel(e)}></div>
              </div>
            </div>` : html`<p class="muted">This page has been deleted, so it cannot be shown.</p>`}
        </div>
        <div class="side">
          ${this._mode === 'clicks' ? html`
            <uui-box headline="Most clicked">
              ${this._elements.length ? html`<table class="list">
                <thead><tr><th>Element</th><th class="num">Clicks</th></tr></thead>
                <tbody>${this._elements.slice(0, 25).map((e) => html`<tr>
                  <td>${e.label
                    ? html`<button class="el" @mouseenter=${() => this.#highlight(e.selector)} @focus=${() => this.#highlight(e.selector)}
                        @mouseleave=${() => this.#highlight(null)} @blur=${() => this.#highlight(null)}>
                        <span class="sub">${e.label.kind}</span> <span dir="auto">${e.label.text || '(no text)'}</span></button>`
                    : html`<span class="muted">Not in this layout now</span>`}
                    <div class="share"><span style=${`width:${Math.max(2, e.share * 100)}%`}></span></div></td>
                  <td class="num">${number.format(e.count)}<br><span class="sub">${Math.round(e.share * 100)}%</span></td>
                </tr>`)}</tbody>
              </table>` : html`<p class="muted">No clicks recorded for this layout.</p>`}
            </uui-box>` : html`
            <uui-box headline="Scroll reach">
              ${r.views ? html`<table class="list">
                <thead><tr><th>Down to</th><th class="num">Page views</th></tr></thead>
                <tbody>${reach.map((v, i) => i % 2 === 0 ? html`<tr>
                  <td>${i * 5}% of the page<div class="share"><span style=${`width:${Math.max(1, v)}%`}></span></div></td>
                  <td class="num">${v}%</td></tr>` : nothing)}</tbody>
              </table>` : html`<p class="muted">No scrolling recorded for this layout.</p>`}
            </uui-box>`}
        </div>
      </div>
    `;
  }

  static styles = [listStyles, css`
    :host { display: grid; gap: var(--uui-size-layout-1); padding: var(--uui-size-layout-1); }
    .filters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); align-items: center; }
    .filters label { display: inline-flex; gap: var(--uui-size-space-2); align-items: center; }
    .filters uui-button-group { margin-inline-start: auto; }
    select { padding: 4px 8px; font: inherit; max-width: 420px; }
    .summary { font-size: 13px; }
    .split { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: var(--uui-size-layout-1); align-items: start; }
    @media (max-width: 1100px) { .split { grid-template-columns: 1fr; } }
    .stage { min-width: 0; }
    .viewport { overflow: hidden; border: 1px solid var(--uui-color-border); border-radius: 6px; background: var(--uui-color-surface); margin-inline: auto; }
    .scaled { position: relative; transform-origin: top left; }
    iframe { display: block; border: 0; background: #fff; }
    .blocker { position: absolute; inset: 0; cursor: ns-resize; }
    .el { all: unset; cursor: pointer; display: block; overflow-wrap: anywhere; }
    .el:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 2px; border-radius: 2px; }
    .side { display: grid; gap: var(--uui-size-layout-1); }
  `];
}

customElements.define('ehc-stats-heatmap', EhcStatsHeatmap);
