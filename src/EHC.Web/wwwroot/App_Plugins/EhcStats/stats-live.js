/* "Real time" dashboard (Statistics section): visitors on the site now (a page view in the last 5 minutes), page views
   per minute for the last 30 minutes, a map of the Saudi cities they are in, and the pages, countries, arrivals and
   devices. Refreshes every 10 seconds while the tab is visible, with a pause button.
   Data: /umbraco/management/api/v1/ehc/stats/live (users whose group has the Statistics section). */
import { LitElement, html, svg, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { number, country, capital, get, niceStep, pageLabel, sourceLabel, barList, listStyles } from './stats-common.js';
import { WIDTH, HEIGHT, COUNTRY, REGION_LINES, NEIGHBOURS, project } from './saudi-map.js';

const EVERY = 10000;
const time = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
const COUNT = [{ key: 'count', title: 'Visitors' }];
const VISITS = [{ key: 'count', title: 'Visits' }];

export default class EhcStatsLive extends UmbElementMixin(LitElement) {
  static properties = {
    _data: { state: true },
    _error: { state: true },
    _paused: { state: true },
    _width: { state: true },
    _minute: { state: true },
    _place: { state: true },
  };

  #auth;
  #timer = null;
  #resize = new ResizeObserver((entries) => { this._width = Math.round(entries[0].contentRect.width); });
  #visibility = () => this.#schedule();

  constructor() {
    super();
    this._paused = false;
    this._width = 0;
    this._minute = null;
    this._place = null;
    this.consumeContext(UMB_AUTH_CONTEXT, (auth) => {
      this.#auth = auth;
      this.#load();
    });
  }

  connectedCallback() {
    super.connectedCallback();
    document.addEventListener('visibilitychange', this.#visibility);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener('visibilitychange', this.#visibility);
    clearTimeout(this.#timer);
    this.#resize.disconnect();
  }

  updated() {
    const box = this.renderRoot.querySelector('.minutes');
    if (box && box !== this._observed) {
      this.#resize.disconnect();
      this.#resize.observe(box);
      this._observed = box;
    }
  }

  /** Next refresh: only while the tab is visible and not paused. */
  #schedule() {
    clearTimeout(this.#timer);
    this.#timer = null;
    if (this._paused || !this.isConnected || document.visibilityState !== 'visible' || !this.#auth) return;
    this.#timer = setTimeout(() => this.#load(), EVERY);
  }

  async #load() {
    try {
      this._data = await (await get(this.#auth, 'live')).json();
      this._error = null;
    } catch (e) {
      this._error = `Could not load the real-time view (${e.message}). Retrying…`;
    }
    this.#schedule();
  }

  #togglePause() {
    this._paused = !this._paused;
    if (this._paused) this.#schedule();
    else this.#load();
  }

  render() {
    const d = this._data;
    return html`
      <div class="head">
        <div class="hero" role="status">
          <b>${d ? number.format(d.active) : '–'}</b>
          <span>${d?.active === 1 ? 'visitor' : 'visitors'} on the site now<br><small>with a page view in the last 5 minutes</small></span>
        </div>
        <div class="controls">
          ${d ? html`<span class="muted">${this._paused ? 'Paused at' : 'Updated'} ${time.format(new Date(d.at))}</span>` : nothing}
          <uui-button look="outline" compact label=${this._paused ? 'Resume updates' : 'Pause updates'} @click=${this.#togglePause}>
            ${this._paused ? 'Resume updates' : 'Pause updates'}</uui-button>
        </div>
      </div>
      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${d && !d.geoLoaded ? html`<p class="muted">The location file is not loaded yet, so the map stays empty.</p>` : nothing}
      ${d ? this.#body(d) : html`<uui-loader></uui-loader>`}
      <p class="note">Counted the same way as the Visitors dashboard (no cookies, no stored IP addresses). This view is
        kept in the server's memory only: it starts empty after a restart, and with several web servers each shows its own
        visitors. Map positions are the city, never the visitor's own location; outside Saudi Arabia only the country is
        known. Map: Natural Earth.</p>
    `;
  }

  #body(d) {
    return html`
      <uui-box headline="Page views per minute, last 30 minutes">${this.#minutes(d.perMinute ?? [])}</uui-box>
      <div class="split">
        <uui-box headline="Where visitors are now">${this.#map(d.places ?? [])}</uui-box>
        <div class="side">
          <uui-box headline="Cities in Saudi Arabia">
            ${barList(d.places, (p) => html`${p.city} <span class="sub">${p.region}</span>`, COUNT)}</uui-box>
          <uui-box headline="Countries">${barList(d.countries, (i) => country(i.value), COUNT)}</uui-box>
        </div>
      </div>
      <div class="grid">
        <uui-box headline="Pages being viewed now">${barList(d.pages, pageLabel, COUNT)}</uui-box>
        <uui-box headline="Arrived in the last 30 minutes from">${barList(d.sources, (i) => sourceLabel(i.value), VISITS)}</uui-box>
        <uui-box headline="Devices now">${barList(d.devices, (i) => capital(i.value), COUNT)}</uui-box>
      </div>
    `;
  }

  /** Columns, one per minute (oldest first); the last one is the current, still filling minute. */
  #minutes(counts) {
    const n = counts.length;
    if (!n) return nothing;
    const W = Math.max(this._width || 0, 320), H = 180, m = { t: 12, r: 8, b: 26, l: 40 };
    const plotW = W - m.l - m.r, plotH = H - m.t - m.b, slot = plotW / n;
    const bar = Math.max(2, Math.min(24, slot - 2));
    const peak = Math.max(...counts);
    const step = niceStep(peak / 3 || 1);
    const max = Math.max(step, Math.ceil(peak / step) * step);
    const y = (v) => m.t + plotH - (v / max) * plotH;
    const ticks = [];
    for (let v = 0; v <= max; v += step) ticks.push(v);
    const ago = (i) => n - 1 - i;
    const when = (i) => (ago(i) === 0 ? 'this minute' : `${ago(i)} min ago`);
    const h = this._minute;
    const total = counts.reduce((a, b) => a + b, 0);
    const column = (v, i) => {
      if (!v) return nothing;
      const x = m.l + i * slot + (slot - bar) / 2, top = y(v), r = Math.min(4, bar / 2, y(0) - top);
      // rounded data end, square at the baseline
      return svg`<path class=${i === h ? 'col on' : 'col'} d=${`M${x},${y(0)}V${top + r}Q${x},${top} ${x + r},${top}H${x + bar - r}Q${x + bar},${top} ${x + bar},${top + r}V${y(0)}Z`}></path>`;
    };
    return html`
      <div class="minutes">
        <svg width=${W} height=${H} role="img" tabindex="0"
          aria-label=${`${number.format(total)} page views in the last 30 minutes, ${number.format(counts[n - 1])} this minute. Use the arrow keys to read each minute.`}
          @pointermove=${(e) => { const r = e.currentTarget.getBoundingClientRect(); this._minute = Math.min(n - 1, Math.max(0, Math.floor((e.clientX - r.left - m.l) / slot))); }}
          @pointerleave=${() => { this._minute = null; }} @blur=${() => { this._minute = null; }}
          @keydown=${(e) => this.#key(e, n)}>
          ${ticks.map((v) => svg`<line class="gridline" x1=${m.l} x2=${W - m.r} y1=${y(v)} y2=${y(v)}></line>
            <text class="tick" x=${m.l - 6} y=${y(v) + 4} text-anchor="end">${number.format(v)}</text>`)}
          ${counts.map(column)}
          ${[0, 10, 20, n - 1].map((i) => svg`<text class="tick" x=${m.l + i * slot + slot / 2} y=${H - 8} text-anchor="middle">${ago(i) ? `−${ago(i)} min` : 'now'}</text>`)}
        </svg>
        ${h != null ? html`<div class="tip" style=${`left:${Math.min(Math.max(m.l + h * slot + slot / 2, 70), W - 70)}px`}>
          <b>${number.format(counts[h])}</b> page views<br><span>${when(h)}</span></div>` : nothing}
        <div class="sr" aria-live="polite">${h != null ? `${when(h)}: ${counts[h]} page views` : ''}</div>
      </div>`;
  }

  #key(e, n) {
    const moves = { ArrowLeft: -1, ArrowRight: 1, ArrowDown: -1, ArrowUp: 1 };
    let i = this._minute;
    if (e.key in moves) i = i == null ? n - 1 : i + moves[e.key];
    else if (e.key === 'Home') i = 0;
    else if (e.key === 'End') i = n - 1;
    else return;
    e.preventDefault();
    this._minute = Math.min(n - 1, Math.max(0, i));
  }

  /** Saudi Arabia with a bubble per city (area follows the number of visitors); the busiest cities are labelled. */
  #map(places) {
    const located = places.filter((p) => p.lat != null && p.lon != null);
    const most = Math.max(1, ...located.map((p) => p.count));
    const dots = located.map((p) => {
      const [x, y] = project(p.lat, p.lon);
      return { ...p, x, y, r: 9 + 21 * Math.sqrt(p.count / most) };
    });
    const labelled = [];
    for (const p of dots) {
      if (labelled.length === 5) break;
      if (labelled.every((q) => Math.hypot(q.x - p.x, q.y - p.y) > 70)) labelled.push(p);
    }
    const tip = this._place != null ? dots[this._place] : null;
    return html`
      <div class="map">
        <svg viewBox=${`0 0 ${WIDTH} ${HEIGHT}`} role="group" aria-label="Map of Saudi Arabia with the cities visitors are in now">
          <rect class="sea" width=${WIDTH} height=${HEIGHT}></rect>
          <path class="neighbour" d=${NEIGHBOURS}></path>
          <path class="land" d=${COUNTRY}></path>
          <path class="regions" d=${REGION_LINES}></path>
          ${[...dots].reverse().map((p) => {
            const i = dots.indexOf(p);
            return svg`<circle class=${i === this._place ? 'bubble on' : 'bubble'} cx=${p.x} cy=${p.y} r=${p.r} tabindex="0" role="img"
              aria-label=${`${p.city}, ${p.region}: ${p.count} ${p.count === 1 ? 'visitor' : 'visitors'}`}
              @pointerenter=${() => { this._place = i; }} @pointerleave=${() => { this._place = null; }}
              @focus=${() => { this._place = i; }} @blur=${() => { this._place = null; }}></circle>`;
          })}
          ${labelled.map((p) => svg`<text class="label" x=${p.x + p.r + 6} y=${p.y + 7}>${p.city}</text>`)}
        </svg>
        ${tip ? html`<div class="tip" style=${`left:${(tip.x / WIDTH) * 100}%;top:${Math.max(0, (tip.y - tip.r) / HEIGHT * 100 - 14)}%`}>
          <b>${number.format(tip.count)}</b> ${tip.count === 1 ? 'visitor' : 'visitors'}<br><span>${tip.city}, ${tip.region}</span></div>` : nothing}
        ${located.length ? nothing : html`<p class="muted empty">No visitors with a known Saudi city right now.</p>`}
      </div>`;
  }

  static styles = [listStyles, css`
    :host { display: grid; gap: var(--uui-size-layout-1); padding: var(--uui-size-layout-1); }
    .head { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-5); align-items: center; justify-content: space-between; }
    .hero { display: flex; gap: 14px; align-items: center; }
    .hero b { font-size: 56px; font-weight: 600; line-height: 1; }
    .hero span { font-size: 15px; }
    .hero small { color: var(--uui-color-text-alt); font-size: 12px; }
    .controls { display: flex; gap: var(--uui-size-space-4); align-items: center; }
    .minutes, .map { position: relative; width: 100%; }
    .minutes svg { display: block; touch-action: pan-y; }
    .col { fill: var(--uui-color-interactive); }
    .col.on { fill: var(--uui-color-interactive-emphasis, var(--uui-color-interactive)); opacity: .8; }
    .split { display: grid; grid-template-columns: minmax(0, 3fr) minmax(280px, 2fr); gap: var(--uui-size-layout-1); align-items: start; }
    .side { display: grid; gap: var(--uui-size-layout-1); }
    @media (max-width: 1000px) { .split { grid-template-columns: 1fr; } }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: var(--uui-size-layout-1); align-items: start; }
    .map svg { display: block; width: 100%; height: auto; }
    .sea { fill: color-mix(in srgb, var(--uui-color-interactive) 8%, var(--uui-color-surface, #fff)); }
    .neighbour { fill: var(--uui-color-surface-alt, #f3f4f6); stroke: var(--uui-color-border); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .land { fill: var(--uui-color-surface, #fff); stroke: var(--uui-color-border-emphasis, #8a919c); stroke-width: 1.5; vector-effect: non-scaling-stroke; }
    .regions { fill: none; stroke: var(--uui-color-border); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .bubble { fill: var(--uui-color-interactive); fill-opacity: .75; stroke: var(--uui-color-surface, #fff); stroke-width: 2; vector-effect: non-scaling-stroke; cursor: default; outline: none; }
    .bubble.on, .bubble:focus-visible { fill-opacity: 1; stroke: var(--uui-color-focus); }
    .label { font-size: 22px; font-weight: 600; fill: var(--uui-color-text); paint-order: stroke; stroke: var(--uui-color-surface, #fff); stroke-width: 5px; stroke-linejoin: round; pointer-events: none; }
    .empty { position: absolute; inset-inline: 0; bottom: 8px; text-align: center; margin: 0; }
  `];
}

customElements.define('ehc-stats-live', EhcStatsLive);
