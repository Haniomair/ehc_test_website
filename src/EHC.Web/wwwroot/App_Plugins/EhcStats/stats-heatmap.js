/* "Heatmaps" dashboard (Statistics section): where visitors click on a page and how far down they scroll, per device
   layout, from visitors who accepted optional cookies. The real page is shown in a same-origin frame at the device's
   width and scrolls natively (clicks and form submits inside it are cancelled); clicks are drawn on a layer inside
   that page (so they follow it while scrolling) at the recorded element and position. The list beside it shows the
   most clicked elements and highlights each one.
   Data: /umbraco/management/api/v1/ehc/stats/heatmap (users whose group has the Statistics section). */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { number, get, icon, card, segmented, about, LANGUAGE, listStyles } from './stats-common.js';

const DEVICES = {
  mobile: { label: 'Mobile', icon: 'mobile', width: 390, height: 760 },
  tablet: { label: 'Tablet', icon: 'tablet', width: 820, height: 1000 },
  desktop: { label: 'Desktop', icon: 'desktop', width: 1280, height: 800 },
};
const PERIODS = [{ id: 7, label: '7 days' }, { id: 30, label: '30 days' }];
const MODES = [{ id: 'clicks', label: 'Clicks', icon: 'click' }, { id: 'scroll', label: 'Scroll reach', icon: 'scroll' }];
const MARKS = [75, 50, 25];
/** The heat layer is drawn at half the page's resolution: the spots are soft anyway, and it keeps the canvas small. */
const RES = 0.5;
/** "Semantic heat" ramp from few clicks (pale yellow, see-through) to many (deep red); the same stops draw the legend. */
const HEAT_STOPS = [[0, 'rgba(255,235,59,0)'], [0.15, 'rgba(255,214,0,.5)'], [0.4, 'rgba(255,152,0,.72)'], [0.7, 'rgba(240,72,24,.84)'], [1, 'rgba(176,18,42,.92)']];
const HEAT_CSS = `linear-gradient(90deg, ${HEAT_STOPS.map(([at, c]) => `${c} ${at * 100}%`).join(', ')})`;
const addDays = (s, n) => { const d = new Date(s + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return d.toISOString().slice(0, 10); };

let ramp = null;
/** 256 RGBA entries, indexed by the accumulated click density (the alpha of the grey layer). */
function heatRamp() {
  if (ramp) return ramp;
  const c = document.createElement('canvas');
  c.width = 256;
  c.height = 1;
  const ctx = c.getContext('2d', { willReadFrequently: true });
  const g = ctx.createLinearGradient(0, 0, 256, 0);
  for (const [at, colour] of HEAT_STOPS) g.addColorStop(at, colour);
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 256, 1);
  ramp = ctx.getImageData(0, 0, 256, 1).data;
  return ramp;
}

/** Cancels anything that would leave or change the previewed page, before the page's own handlers see it. */
const block = (e) => { e.preventDefault(); e.stopImmediatePropagation(); };

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
      this.#redraw([300, 1200]);
    } catch (e) {
      this._error = `Could not load the heatmap (${e.message}).`;
    }
  }

  #redraw(delays) {
    this.#timers.forEach(clearTimeout);
    this.#timers = delays.map((t) => setTimeout(() => this.#draw(), t));
  }

  #change(field, value) {
    this[field] = field === '_days' ? Number(value) : value;
    if (field === '_days') this.#loadPages();
    else if (field === '_mode') this.#draw();
    else this.#loadReport();
  }

  get #frame() { return this.renderRoot.querySelector('iframe'); }

  /**
   * Page loaded in the frame: add heat-frame.css (hides the cookie notice, stops animations and smooth scrolling,
   * drops blur effects that are slow to repaint while scrolling, styles the markers; a same-origin file, as the site's
   * Content-Security-Policy blocks inline styles), cancel clicks and form submits so the preview stays on this page,
   * then draw, again once images and fonts settle.
   */
  #loaded() {
    const win = this.#frame?.contentWindow, doc = this.#frame?.contentDocument;
    if (!doc?.head || !win) return;
    const link = doc.createElement('link');
    link.rel = 'stylesheet';
    link.href = new URL('./heat-frame.css', import.meta.url).pathname;
    doc.head.appendChild(link);
    for (const type of ['click', 'auxclick', 'dblclick', 'submit', 'contextmenu']) win.addEventListener(type, block, true);
    this.#redraw([400, 1500, 3500]);
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

    // elements, most clicked first (used by the list and for highlighting)
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

    if (this._mode === 'clicks') this.#heat(doc, win, layer, W, H, report.clicks);
    else this.#reach(doc, layer, H, report.reach);
  }

  /** Click heat: soft grey spots add up where clicks overlap, then each pixel's density is coloured from the ramp. */
  #heat(doc, win, layer, W, H, clicks) {
    const spots = [];
    for (const c of clicks) {
      const at = this.#locate(doc, win, c.selector);
      if (at) spots.push({ x: at.left + (c.x / 100) * at.width, y: at.top + (c.y / 100) * at.height, count: c.count });
    }
    if (!spots.length) return;
    const cw = Math.max(1, Math.ceil(W * RES)), ch = Math.max(1, Math.ceil(H * RES));
    const canvas = doc.createElement('canvas');
    canvas.width = cw;
    canvas.height = ch;
    layer.appendChild(canvas);
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    const most = Math.max(...spots.map((s) => s.count));
    const r = (this._device === 'mobile' ? 22 : 28) * RES;
    let top = ch, bottom = 0;
    for (const s of spots) {
      const x = s.x * RES, y = s.y * RES, a = 0.25 + 0.75 * (s.count / most);
      const g = ctx.createRadialGradient(x, y, 0, x, y, r);
      g.addColorStop(0, `rgba(0,0,0,${a})`);
      g.addColorStop(1, 'rgba(0,0,0,0)');
      ctx.fillStyle = g;
      ctx.fillRect(x - r, y - r, r * 2, r * 2);
      top = Math.min(top, y - r);
      bottom = Math.max(bottom, y + r);
    }
    // colour only the band that has spots
    top = Math.max(0, Math.floor(top));
    bottom = Math.min(ch, Math.ceil(bottom));
    if (bottom <= top) return;
    const image = ctx.getImageData(0, top, cw, bottom - top), px = image.data, colours = heatRamp();
    for (let i = 3; i < px.length; i += 4) {
      const a = px[i];
      if (!a) continue;
      const o = a * 4;
      px[i - 3] = colours[o];
      px[i - 2] = colours[o + 1];
      px[i - 1] = colours[o + 2];
      px[i] = colours[o + 3];
    }
    ctx.putImageData(image, 0, top);
  }

  /** Scroll reach: the page darkens as fewer page views got that far, with lines where 75 / 50 / 25 % remain. */
  #reach(doc, layer, H, reach) {
    if (!reach?.length || reach[0] === 0) return;
    const fullHeight = doc.documentElement.scrollHeight;
    const step = 100 / (reach.length - 1);
    const fade = doc.createElement('div');
    fade.className = 'ehc-heat-fade';
    fade.style.height = `${H}px`;
    fade.style.background = `linear-gradient(180deg, ${reach.map((v, i) => `rgba(12,18,38,${((1 - v / 100) * 0.62).toFixed(3)}) ${(i * step).toFixed(1)}%`).join(', ')})`;
    layer.appendChild(fade);
    for (const mark of MARKS) {
      const depth = this.#depthFor(reach, mark);
      if (depth == null) continue;
      const line = doc.createElement('div');
      line.className = 'ehc-heat-mark';
      line.style.top = `${Math.round((depth / 100) * fullHeight)}px`;
      const tag = doc.createElement('span');
      tag.textContent = `${mark}% of page views reached here`;
      line.appendChild(tag);
      layer.appendChild(line);
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
    at.el.scrollIntoView({ block: 'center', behavior: 'instant' });
    const again = this.#locate(doc, win, selector);
    const box = doc.createElement('div');
    box.className = 'ehc-heat-box';
    box.style.cssText = `left:${again.left}px;top:${again.top}px;width:${again.width}px;height:${again.height}px`;
    doc.getElementById('ehc-heat-layer')?.appendChild(box);
  }

  render() {
    const pages = this._pages, r = this._report;
    return html`
      <header class="page-head">
        <div class="page-title">
          <h2>Heatmaps</h2>
          <p>${icon('click')}Where visitors click and how far they scroll</p>
        </div>
      </header>
      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      <div class="layout">
        <aside class="tools">
          <section class="card panel">
            <label class="field"><span class="field-label">Page</span>
              <span class="select"><select @change=${(e) => this.#change('_page', e.target.value)} ?disabled=${!pages?.length}>
                ${pages?.length
                  ? pages.map((p) => html`<option value=${p.value} ?selected=${p.value === this._page}>
                      ${p.label} (${LANGUAGE[p.value.split('|')[1]] ?? ''}) · ${number.format(p.views)} views</option>`)
                  : html`<option>${pages ? 'No pages recorded yet' : 'Loading…'}</option>`}
              </select></span>
            </label>
            <div class="field"><span class="field-label">Period</span>
              ${segmented('Period', PERIODS, this._days, (id) => this.#change('_days', id))}</div>
            <div class="field"><span class="field-label">Layout</span>
              ${segmented('Layout', Object.entries(DEVICES).map(([id, d]) => ({ id, label: d.label, icon: d.icon })), this._device, (id) => this.#change('_device', id))}</div>
            <div class="field"><span class="field-label">Show</span>
              ${segmented('Show', MODES, this._mode, (id) => this.#change('_mode', id))}</div>
            ${r ? this.#summary(r) : nothing}
          </section>
          ${r ? (this._mode === 'clicks' ? this.#clickList() : this.#reachList(r)) : nothing}
        </aside>
        <div class="stage">
          ${pages && !pages.length ? html`<section class="card intro">
            <span class="intro-icon">${icon('click')}</span>
            <h2>No clicks or scrolling recorded yet</h2>
            <p>They are recorded only for visitors who choose "Accept all" in the cookie notice, so it can take a while for
              the first heatmap to appear.</p>
          </section>` : r ? this.#preview(r) : html`<div class="loading"><uui-loader></uui-loader></div>`}
        </div>
      </div>
      ${about(html`Recorded only for visitors who accepted optional cookies, for at most 50 clicks per page view: the
        clicked element and where inside it, and how far down the page they got. Nothing typed, no page text and no
        visitor id is recorded; data is deleted after 30 days. The page is shown as it is now, so clicks on parts that
        changed or that this layout hides cannot be placed. Scroll reach counts the lowest point of the page that was on
        screen. Links and buttons in the preview do nothing, so it always stays on the chosen page.`)}
    `;
  }

  #summary(r) {
    const total = r.clicks.reduce((s, c) => s + c.count, 0);
    return html`<div class="summary">
      <div><b>${number.format(r.views)}</b><span>page views</span></div>
      <div><b>${number.format(total)}</b><span>clicks</span></div>
      ${this._missing ? html`<p class="missing">${icon('info')}<span>${number.format(this._missing)} clicks are on elements not in this layout now</span></p>` : nothing}
    </div>`;
  }

  #preview(r) {
    if (!r.url) return html`<p class="empty">This page has been deleted, so it cannot be shown.</p>`;
    const d = DEVICES[this._device];
    const scale = this.#scale;
    let path = r.url;
    try { path = decodeURI(new URL(r.url, location.href).pathname); } catch { /* keep the raw url */ }
    return html`
      <div class="browser" style=${`width:${d.width * scale}px`}>
        <div class="chrome" aria-hidden="true">
          <span class="dots"><i></i><i></i><i></i></span>
          <span class="address" dir="auto">${path}</span>
          <span class="size">${d.label} · ${d.width} px</span>
        </div>
        <div class="viewport" style=${`height:${d.height * scale}px`}>
          <div class="scaled" style=${`width:${d.width}px;height:${d.height}px;transform:scale(${scale})`}>
            <iframe title=${`Preview of ${r.label}`} src=${r.url} sandbox="allow-same-origin allow-scripts"
              width=${d.width} height=${d.height} @load=${this.#loaded}></iframe>
          </div>
        </div>
      </div>`;
  }

  #clickList() {
    const legend = html`<div class="scale"><span class="scale-bar" style=${`background:${HEAT_CSS}`}></span>
      <span class="scale-ends"><span>Fewer clicks</span><span>More clicks</span></span></div>`;
    const top = this._elements.slice(0, 25);
    return card('Most clicked', html`${legend}
      ${top.length ? html`<ol class="clicks">${top.map((e, i) => html`<li>
        <span class="rank">${i + 1}</span>
        <div class="what">
          <span class="fill" style=${`width:${Math.max(2, (e.count / top[0].count) * 100)}%`}></span>
          ${e.label
            ? html`<button class="el" type="button" @mouseenter=${() => this.#highlight(e.selector)} @focus=${() => this.#highlight(e.selector)}
                @mouseleave=${() => this.#highlight(null)} @blur=${() => this.#highlight(null)}>
                <span class="kind">${e.label.kind}</span><span dir="auto">${e.label.text || '(no text)'}</span></button>`
            : html`<span class="el gone">Not in this layout now</span>`}
        </div>
        <span class="count"><b>${number.format(e.count)}</b><span>${Math.round(e.share * 100)}%</span></span>
      </li>`)}</ol>` : html`<p class="empty">No clicks recorded for this layout</p>`}`,
      { sub: 'Point at a row to find it on the page' });
  }

  #reachList(r) {
    const reach = r.reach ?? [];
    return card('Scroll reach', r.views ? html`
      <div class="scale"><span class="scale-bar reach"></span>
        <span class="scale-ends"><span>Seen by most</span><span>Seen by few</span></span></div>
      <ol class="reach-list">${reach.map((v, i) => i % 2 === 0 ? html`<li>
        <span class="depth">${i * 5}%</span>
        <span class="track"><span style=${`width:${Math.max(1, v)}%`}></span></span>
        <b>${v}%</b>
      </li>` : nothing)}</ol>` : html`<p class="empty">No scrolling recorded for this layout</p>`,
      { sub: 'Share of page views that got this far down the page' });
  }

  static styles = [listStyles, css`
    .layout { display: grid; grid-template-columns: 340px minmax(0, 1fr); gap: 20px; align-items: start; }
    @media (max-width: 1000px) { .layout { grid-template-columns: minmax(0, 1fr); } }
    .tools { display: grid; gap: 20px; position: sticky; top: 12px; min-width: 0; }
    .panel { display: grid; gap: 16px; }
    .field { display: grid; gap: 6px; min-width: 0; }
    .field-label { font-size: 12px; font-weight: 700; color: var(--uui-color-text-alt); }
    .field .select { display: flex; }
    .field select { width: 100%; max-width: none; }
    .tools .seg { display: flex; }
    .tools .seg button { flex: 1; justify-content: center; padding-inline: 8px; }
    .summary { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; padding-top: 14px; border-top: 1px solid var(--uui-color-divider, var(--uui-color-border)); }
    .summary > div { display: grid; }
    .summary b { font-size: 22px; font-weight: 800; }
    .summary span { font-size: 12px; color: var(--uui-color-text-alt); }
    .missing { grid-column: 1 / -1; display: flex; gap: 8px; align-items: flex-start; font-size: 12px; color: var(--uui-color-text-alt); }
    .missing .i { width: 15px; height: 15px; flex: none; color: color-mix(in srgb, #b77900 80%, var(--uui-color-text)); }
    .stage .intro { min-height: 420px; align-content: center; }
    .stage { min-width: 0; }
    .browser { margin-inline: auto; max-width: 100%; border-radius: var(--ehc-radius); overflow: hidden; background: var(--uui-color-surface, #fff);
      border: 1px solid var(--uui-color-border-emphasis, var(--uui-color-border)); }
    .chrome { display: flex; align-items: center; gap: 12px; padding: 7px 12px; background: var(--uui-color-surface-alt, #f1f1f3);
      border-bottom: 1px solid var(--uui-color-border); }
    .dots { display: inline-flex; gap: 6px; }
    .dots i { width: 10px; height: 10px; border-radius: 50%; background: var(--uui-color-border-emphasis, #c9c9cf); }
    .size { font-size: 12px; color: var(--uui-color-text-alt); white-space: nowrap; }
    .address { flex: 1; min-width: 0; padding: 3px 10px; border-radius: 6px; font-size: 12px; color: var(--uui-color-text-alt);
      background: var(--uui-color-surface, #fff); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .viewport { overflow: hidden; }
    .scaled { position: relative; transform-origin: top left; }
    iframe { display: block; border: 0; background: #fff; }
    .scale { display: grid; gap: 5px; margin-bottom: 14px; }
    .scale-bar { display: block; height: 10px; border-radius: 5px; border: 1px solid var(--uui-color-border); }
    .scale-bar.reach { background: linear-gradient(90deg, rgba(12,18,38,0), rgba(12,18,38,.62)); }
    .scale-ends { display: flex; justify-content: space-between; font-size: 11.5px; color: var(--uui-color-text-alt); }
    ol { list-style: none; margin: 0; padding: 0; }
    .clicks { display: grid; gap: 4px; max-height: 52vh; overflow: auto; }
    .clicks li { display: flex; align-items: center; gap: 10px; }
    .rank { width: 22px; flex: none; text-align: center; font-size: 12px; font-weight: 700; color: var(--uui-color-text-alt); font-variant-numeric: tabular-nums; }
    .what { position: relative; flex: 1; min-width: 0; }
    .what .fill { position: absolute; inset-block: 0; inset-inline-start: 0; border-radius: 7px;
      background: color-mix(in srgb, #f04818 13%, transparent); }
    .el { all: unset; position: relative; box-sizing: border-box; display: block; width: 100%; cursor: pointer; padding: 7px 9px; border-radius: 7px;
      font-size: 13px; line-height: 1.35; overflow-wrap: anywhere; }
    .el:hover { background: color-mix(in srgb, #f04818 10%, transparent); }
    .el:focus-visible { outline: 2px solid var(--uui-color-focus); outline-offset: 1px; }
    .el.gone { cursor: default; color: var(--uui-color-text-alt); font-style: italic; }
    .kind { display: inline-block; margin-inline-end: 7px; padding: 0 6px; border-radius: 4px; font-size: 10.5px; font-weight: 700;
      text-transform: uppercase; letter-spacing: .04em; color: var(--uui-color-text-alt); background: var(--uui-color-surface, #fff);
      border: 1px solid var(--uui-color-border); vertical-align: 1px; }
    .count { display: grid; justify-items: end; flex: none; min-width: 44px; font-variant-numeric: tabular-nums; }
    .count b { font-size: 13px; }
    .count span { font-size: 11px; color: var(--uui-color-text-alt); }
    .reach-list { display: grid; gap: 8px; }
    .reach-list li { display: grid; grid-template-columns: 44px 1fr 44px; align-items: center; gap: 10px; font-size: 13px; }
    .depth { color: var(--uui-color-text-alt); font-variant-numeric: tabular-nums; }
    .reach-list b { text-align: end; font-variant-numeric: tabular-nums; }
    .track { height: 10px; border-radius: 5px; background: var(--uui-color-surface-alt, #f1f1f3); overflow: hidden; }
    .track span { display: block; height: 100%; border-radius: 5px; background: var(--ehc-accent); }
    .intro { display: grid; justify-items: center; text-align: center; gap: 12px; padding: 48px 24px; }
    .intro-icon { display: grid; place-items: center; width: 56px; height: 56px; border-radius: 16px; background: var(--ehc-accent-soft); color: var(--ehc-accent); }
    .intro-icon .i { width: 28px; height: 28px; }
    .intro h2 { font-size: 20px; font-weight: 800; }
    .intro p { max-width: 520px; color: var(--uui-color-text-alt); }
  `];
}

customElements.define('ehc-stats-heatmap', EhcStatsHeatmap);
