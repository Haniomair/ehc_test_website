/* "Real time" dashboard (Statistics section): visitors on the site now (a page view in the last 5 minutes), page views
   per minute for the last 30 minutes, a map of the Saudi cities they are in, and the pages, countries, arrivals and
   devices. Refreshes every 10 seconds while the tab is visible, with a pause button.
   Data: /umbraco/management/api/v1/ehc/stats/live (users whose group has the Statistics section). */
import { LitElement, html, svg, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import {
  number, big, country, capital, get, niceStep, icon, pageLabel, sourceLabel, barList, shareBar, card, segmented, about, listStyles,
} from './stats-common.js';
import { WIDTH, HEIGHT, COUNTRY, REGION_LINES, NEIGHBOURS, project } from './saudi-map.js';

const EVERY = 10000;
const time = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
const COUNT = [{ key: 'count', title: 'Visitors' }];
const VISITS = [{ key: 'count', title: 'Visits' }];
const DEVICE_ICON = { desktop: 'desktop', mobile: 'mobile', tablet: 'tablet' };

export default class EhcStatsLive extends UmbElementMixin(LitElement) {
  static properties = {
    _data: { state: true },
    _error: { state: true },
    _paused: { state: true },
    _width: { state: true },
    _minute: { state: true },
    _place: { state: true },
    _where: { state: true },
    _now: { state: true },
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
    this._where = 'city';
    this._now = 'source';
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
    const minutes = d?.perMinute ?? [];
    const views = minutes.reduce((a, b) => a + b, 0);
    const visits = (d?.sources ?? []).reduce((a, s) => a + s.count, 0);
    return html`
      <header class="page-head">
        <div class="page-title">
          <h2>Real time</h2>
          <p>${icon('clock')}${d ? html`${this._paused ? 'Paused at' : 'Updated'} ${time.format(new Date(d.at))}` : 'Connecting…'}
            ${this._paused ? nothing : html`· refreshes every 10 seconds`}</p>
        </div>
        <div class="controls">
          <button class="btn" type="button" @click=${this.#togglePause}>
            ${icon(this._paused ? 'play' : 'pause')}${this._paused ? 'Resume updates' : 'Pause updates'}</button>
        </div>
      </header>
      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${d && !d.geoLoaded ? html`<p class="callout">${icon('info')}<span>The location file is not loaded yet, so the map stays empty.</span></p>` : nothing}
      ${d ? html`
        <div class="kpis">
          <div class="kpi hero" role="status">
            <span class="kpi-top"><span class=${this._paused ? 'live paused' : 'live'}><span></span>${this._paused ? 'Paused' : 'Live'}</span>On the site now</span>
            <span class="kpi-value">${big(d.active)}</span>
            <span class="delta-note">${d.active === 1 ? 'visitor' : 'visitors'} with a page view in the last 5 minutes</span>
          </div>
          <div class="kpi">
            <span class="kpi-top">Page views</span>
            <span class="kpi-value">${big(views)}</span>
            <span class="delta-note">in the last 30 minutes · ${number.format(minutes[minutes.length - 1] ?? 0)} this minute</span>
          </div>
          <div class="kpi">
            <span class="kpi-top">Visits</span>
            <span class="kpi-value">${big(visits)}</span>
            <span class="delta-note">started in the last 30 minutes</span>
          </div>
          <div class="kpi">
            <span class="kpi-top">Pages being viewed</span>
            <span class="kpi-value">${number.format(d.pages?.length ?? 0)}</span>
            <span class="delta-note">different pages right now</span>
          </div>
        </div>
        ${this.#body(d)}` : html`<div class="loading"><uui-loader></uui-loader></div>`}
      ${about(html`Counted the same way as the Visitors dashboard (no cookies, no stored IP addresses). This view is kept
        in the server's memory only: it starts empty after a restart, and with several web servers each shows its own
        visitors. Map positions are the city, never the visitor's own location; outside Saudi Arabia only the country is
        known. Map: Natural Earth.`)}
    `;
  }

  #body(d) {
    return html`
      ${card('Page views per minute', this.#minutes(d.perMinute ?? []), { sub: 'Last 30 minutes; the lighter column is the minute still in progress' })}
      ${card('Where visitors are now', html`<div class="where">
          ${this.#map(d.places ?? [])}
          <div>${this._where === 'city'
            ? barList(d.places, (p) => html`${p.city} <span class="sub">${p.region}</span>`, COUNT, 10)
            : barList(d.countries, (i) => country(i.value), COUNT, 10)}</div>
        </div>`, {
        sub: 'Saudi cities on the map; bubble size follows the number of visitors',
        actions: segmented('Where: show', [{ id: 'city', label: 'Cities' }, { id: 'country', label: 'Countries' }], this._where, (id) => { this._where = id; }),
      })}
      <div class="cards">
        ${card('Pages being viewed now', barList(d.pages, pageLabel, COUNT, 8))}
        ${card('Visitors now', this._now === 'source'
          ? barList(d.sources, (i) => sourceLabel(i.value), VISITS, 8)
          : shareBar(d.devices, (i) => capital(i.value), 'count', (i) => DEVICE_ICON[i.value]), {
          sub: this._now === 'source' ? 'Where visits in the last 30 minutes came from' : 'Devices of visitors on the site now',
          actions: segmented('Visitors now: show', [{ id: 'source', label: 'Arrived from' }, { id: 'device', label: 'Devices' }], this._now, (id) => { this._now = id; }),
        })}
      </div>
    `;
  }

  /** Columns, one per minute (oldest first); the last one is the current, still filling minute. */
  #minutes(counts) {
    const n = counts.length;
    if (!n) return nothing;
    const W = Math.max(this._width || 0, 320), H = 200, m = { t: 12, r: 8, b: 26, l: 40 };
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
      const cls = `col${i === n - 1 ? ' now' : ''}${i === h ? ' on' : ''}`;
      return svg`<path class=${cls} d=${`M${x},${y(0)}V${top + r}Q${x},${top} ${x + r},${top}H${x + bar - r}Q${x + bar},${top} ${x + bar},${top + r}V${y(0)}Z`}></path>`;
    };
    return html`
      <div class="minutes">
        <svg width=${W} height=${H} role="img" tabindex="0"
          aria-label=${`${number.format(total)} page views in the last 30 minutes, ${number.format(counts[n - 1])} this minute. Use the arrow keys to read each minute.`}
          @pointermove=${(e) => { const r = e.currentTarget.getBoundingClientRect(); this._minute = Math.min(n - 1, Math.max(0, Math.floor((e.clientX - r.left - m.l) / slot))); }}
          @pointerleave=${() => { this._minute = null; }} @blur=${() => { this._minute = null; }}
          @keydown=${(e) => this.#key(e, n)}>
          ${ticks.map((v, i) => svg`<line class=${i ? 'gridline' : 'baseline'} x1=${m.l} x2=${W - m.r} y1=${y(v)} y2=${y(v)}></line>
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
    .kpi { min-height: 0; }
    .kpi.hero .kpi-value { font-size: 44px; }
    .live { display: inline-flex; align-items: center; gap: 7px; padding: 3px 10px 3px 8px; border-radius: 999px; font-size: 11px; font-weight: 800;
      letter-spacing: .06em; text-transform: uppercase; color: var(--ehc-good); background: color-mix(in srgb, #0ca30c 13%, transparent); }
    .live span { position: relative; width: 8px; height: 8px; border-radius: 50%; background: #0ca30c; }
    .live span::after { content: ''; position: absolute; inset: 0; border-radius: 50%; background: #0ca30c; animation: ping 1.8s ease-out infinite; }
    .live.paused { color: var(--uui-color-text-alt); background: var(--uui-color-surface-alt, #f1f1f3); }
    .live.paused span { background: var(--uui-color-text-alt); }
    .live.paused span::after { display: none; }
    @keyframes ping { from { transform: scale(1); opacity: .7; } to { transform: scale(2.8); opacity: 0; } }
    .minutes, .map { position: relative; width: 100%; }
    .minutes svg { display: block; touch-action: pan-y; }
    .col { fill: var(--ehc-accent); }
    .col.now { fill: color-mix(in srgb, var(--ehc-accent) 45%, var(--uui-color-surface, #fff)); }
    .col.on { fill: color-mix(in srgb, var(--ehc-accent) 75%, var(--uui-color-text, #000)); }
    .where { display: grid; grid-template-columns: minmax(0, 3fr) minmax(260px, 2fr); gap: 24px; align-items: start; }
    @media (max-width: 1000px) { .where { grid-template-columns: minmax(0, 1fr); } }
    .map svg { display: block; width: 100%; height: auto; border-radius: 10px; }
    .sea { fill: color-mix(in srgb, var(--ehc-accent) 7%, var(--uui-color-surface, #fff)); }
    .neighbour { fill: var(--uui-color-surface-alt, #f3f4f6); stroke: var(--uui-color-border); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .land { fill: var(--uui-color-surface, #fff); stroke: var(--uui-color-border-emphasis, #8a919c); stroke-width: 1.5; vector-effect: non-scaling-stroke; }
    .regions { fill: none; stroke: var(--uui-color-border); stroke-width: 1; vector-effect: non-scaling-stroke; }
    .bubble { fill: var(--ehc-accent); fill-opacity: .7; stroke: var(--uui-color-surface, #fff); stroke-width: 2; vector-effect: non-scaling-stroke; cursor: default; outline: none; }
    .bubble.on, .bubble:focus-visible { fill-opacity: 1; stroke: var(--uui-color-focus); }
    .label { font-size: 22px; font-weight: 700; fill: var(--uui-color-text); paint-order: stroke; stroke: var(--uui-color-surface, #fff); stroke-width: 5px; stroke-linejoin: round; pointer-events: none; }
    .empty { position: absolute; inset-inline: 0; bottom: 10px; text-align: center; margin: 0; font-size: 13px; }
  `];
}

customElements.define('ehc-stats-live', EhcStatsLive);
