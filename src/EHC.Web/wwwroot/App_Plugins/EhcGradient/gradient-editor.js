/* Gradient editor (property editor UI for a Gradient's "design", stored as JSON):
   { angle, stops: [{ at, colour }], glow: { colour, x, y, strength } | null }
   colour = a theme colour label ("Deep 900", follows occasion themes) or a custom "#RRGGBB".
   Live preview with white sample text, a stop bar (drag to move, click the bar to add, Delete to remove, arrow keys
   to nudge), an angle dial, a draggable glow point and white-text contrast figures. The server re-validates
   everything (Blocks/Gradients.cs) and refuses colours too light for white text (Themes/GradientContrastGuard.cs).
   Plain module using the backoffice's own Lit and element API. */
import { LitElement, html, css, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';

/* Theme colours at their EHC default (must equal Palette.Defaults in Themes/Palette.cs; a test compares them). */
export const THEME = {
  Primary: { 50: '#EAF5FC', 100: '#D3EAF7', 200: '#A9D5EF', 300: '#7FD3FF', 400: '#2FA8DF', 500: '#2490CC', 600: '#1C7DB4', 700: '#17669A' },
  Deep: { 700: '#123F73', 800: '#0F3A6B', 900: '#0A2A4F', 950: '#061A33' },
  Night: { 900: '#0B1B31', 950: '#060F1E' },
  Accent: { 400: '#F2B544', 500: '#E3A12C' },
  Highlight: { 500: '#E0567B', 800: '#7A1F4F' },
  Support: { 50: '#E6F5F3', 300: '#9BF0E2', 400: '#3BB6A9', 500: '#0E8F87', 800: '#0B4E6B', 900: '#06324A' },
};
const HEX = /^#[0-9A-Fa-f]{6}$/;
const MAX_STOPS = 8;
const MIN = 3; // below: refused on save (fails even for headings)
const NORMAL = 4.5; // below: warning (fails for normal-size text)

const STARTERS = [
  { name: 'EHC blue', design: { angle: 135, stops: [{ at: 0, colour: 'Deep 900' }, { at: 45, colour: 'Deep 800' }, { at: 100, colour: 'Primary 500' }], glow: { colour: 'Primary 400', x: 80, y: 20, strength: 100 } } },
  { name: 'Teal', design: { angle: 135, stops: [{ at: 0, colour: 'Support 900' }, { at: 45, colour: 'Support 800' }, { at: 100, colour: 'Support 500' }], glow: { colour: 'Support 400', x: 80, y: 20, strength: 100 } } },
  { name: 'Rose', design: { angle: 135, stops: [{ at: 0, colour: '#3A1030' }, { at: 45, colour: 'Highlight 800' }, { at: 100, colour: 'Highlight 500' }], glow: { colour: '#F08AA6', x: 80, y: 20, strength: 100 } } },
  { name: 'Night', design: { angle: 180, stops: [{ at: 0, colour: 'Night 950' }, { at: 100, colour: 'Deep 800' }], glow: null } },
];

function hexOf(colour) {
  if (HEX.test(colour || '')) return colour.toUpperCase();
  const [family, shade] = String(colour || '').split(' ');
  return THEME[family]?.[shade] ?? null;
}
function luminance(hex) {
  const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
}
function ratio(hex) {
  return Math.round(((1.05) / (luminance(hex) + 0.05)) * 100) / 100;
}
const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));
const round1 = (v) => Math.round(v * 10) / 10;

/* Same cleaning as Gradients.Parse on the server. */
function normalise(raw) {
  let d = raw;
  if (typeof d === 'string') {
    try { d = JSON.parse(d); } catch { return null; }
  }
  if (!d || !Array.isArray(d.stops)) return null;
  const stops = d.stops
    .filter((s) => s && hexOf(s.colour) && Number.isFinite(Number(s.at)))
    .map((s) => ({ at: round1(clamp(Number(s.at), 0, 100)), colour: String(s.colour).trim() }))
    .sort((a, b) => a.at - b.at)
    .slice(0, MAX_STOPS);
  if (stops.length < 2) return null;
  const g = d.glow;
  const glow = g && hexOf(g.colour) ? { colour: String(g.colour).trim(), x: round1(clamp(Number(g.x) || 0, 0, 100)), y: round1(clamp(Number(g.y) || 0, 0, 100)), strength: Math.round(clamp(Number(g.strength ?? 100), 0, 100)) } : null;
  return { angle: ((Math.round(Number(d.angle) || 0) % 360) + 360) % 360, stops, glow };
}
function linear(d, angle) {
  return `linear-gradient(${angle ?? d.angle}deg, ${d.stops.map((s) => `${hexOf(s.colour)} ${s.at}%`).join(', ')})`;
}
function background(d) {
  if (!d.glow) return linear(d);
  const c = hexOf(d.glow.colour);
  const glow = d.glow.strength >= 100 ? c : `color-mix(in srgb, ${c} ${d.glow.strength}%, transparent)`;
  return `radial-gradient(900px 600px at ${d.glow.x}% ${d.glow.y}%, ${glow} 0%, transparent 60%), ${linear(d)}`;
}
const clone = (d) => JSON.parse(JSON.stringify(d));

export default class EhcGradientEditor extends UmbElementMixin(LitElement) {
  static properties = {
    value: { attribute: false },
    readonly: { type: Boolean, reflect: true },
    _design: { state: true },
    _sel: { state: true },
    _hexDraft: { state: true },
  };

  constructor() {
    super();
    this._design = null;
    this._sel = 0;
    this._hexDraft = '';
  }

  willUpdate(changed) {
    if (changed.has('value') && !this._dragging) {
      this._design = normalise(this.value);
      if (this._design && typeof this._sel === 'number' && this._sel >= this._design.stops.length) this._sel = 0;
    }
  }

  /* ----------------------------------------------------------------- changes */
  #commit(design) {
    this._design = design;
    this.value = clone(design);
    this.dispatchEvent(new UmbChangeEvent());
  }
  #edit(fn) {
    if (this.readonly || !this._design) return;
    const d = clone(this._design);
    fn(d);
    d.stops.sort((a, b) => a.at - b.at);
    this.#commit(d);
  }
  #selected() {
    if (!this._design) return null;
    return this._sel === 'glow' ? this._design.glow : this._design.stops[this._sel];
  }
  #setColour(colour) {
    this.#edit((d) => {
      if (this._sel === 'glow') d.glow.colour = colour;
      else d.stops[this._sel].colour = colour;
    });
    this._hexDraft = '';
  }

  /* --------------------------------------------------------------- stop bar */
  #pos(e, el) {
    const r = el.getBoundingClientRect();
    return round1(clamp(((e.clientX - r.left) / r.width) * 100, 0, 100));
  }
  #addAt(e) {
    if (this.readonly || this._design.stops.length >= MAX_STOPS) return;
    const at = this.#pos(e, e.currentTarget);
    const near = this._design.stops.reduce((a, b) => (Math.abs(b.at - at) < Math.abs(a.at - at) ? b : a));
    const d = clone(this._design);
    d.stops.push({ at, colour: near.colour });
    d.stops.sort((a, b) => a.at - b.at);
    this._sel = d.stops.findIndex((s) => s.at === at && s.colour === near.colour);
    this.#commit(d);
  }
  #stopDown(e, i) {
    if (this.readonly) return;
    e.stopPropagation();
    this._sel = i;
    const track = this.renderRoot.querySelector('.track');
    const handle = e.currentTarget;
    handle.setPointerCapture(e.pointerId);
    this._dragging = true;
    const stop = this._design.stops[i];
    const move = (ev) => {
      stop.at = this.#pos(ev, track);
      this.requestUpdate();
    };
    const up = () => {
      handle.removeEventListener('pointermove', move);
      handle.removeEventListener('pointerup', up);
      handle.removeEventListener('pointercancel', up);
      this._dragging = false;
      const d = clone(this._design);
      d.stops.sort((a, b) => a.at - b.at);
      this._sel = d.stops.findIndex((s) => s.at === stop.at && s.colour === stop.colour);
      this.#commit(d);
    };
    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', up);
    handle.addEventListener('pointercancel', up);
  }
  #stopKey(e, i) {
    if (this.readonly) return;
    const step = e.shiftKey ? 10 : 1;
    if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
      e.preventDefault();
      const stop = this._design.stops[i];
      const at = round1(clamp(stop.at + (e.key === 'ArrowRight' ? step : -step), 0, 100));
      this.#edit((d) => { d.stops[i].at = at; });
      this._sel = this._design.stops.findIndex((s) => s.at === at && s.colour === stop.colour);
      this.updateComplete.then(() => this.renderRoot.querySelector(`.stop[data-i="${this._sel}"]`)?.focus());
    } else if (e.key === 'Delete' || e.key === 'Backspace') {
      e.preventDefault();
      this.#removeStop(i);
    }
  }
  #removeStop(i) {
    if (this._design.stops.length <= 2) return;
    this.#edit((d) => { d.stops.splice(i, 1); });
    this._sel = Math.max(0, i - 1);
  }

  /* ------------------------------------------------------------------ angle */
  #dialDown(e) {
    if (this.readonly) return;
    const dial = e.currentTarget;
    dial.setPointerCapture(e.pointerId);
    this._dragging = true;
    const set = (ev) => {
      const r = dial.getBoundingClientRect();
      const deg = (Math.atan2(ev.clientX - (r.left + r.width / 2), -(ev.clientY - (r.top + r.height / 2))) * 180) / Math.PI;
      let a = Math.round((deg + 360) % 360);
      if (ev.shiftKey) a = Math.round(a / 15) * 15 % 360;
      this._design.angle = a;
      this.requestUpdate();
    };
    set(e);
    const up = () => {
      dial.removeEventListener('pointermove', set);
      dial.removeEventListener('pointerup', up);
      dial.removeEventListener('pointercancel', up);
      this._dragging = false;
      this.#commit(clone(this._design));
    };
    dial.addEventListener('pointermove', set);
    dial.addEventListener('pointerup', up);
    dial.addEventListener('pointercancel', up);
  }
  #dialKey(e) {
    const step = e.shiftKey ? 15 : 1;
    const delta = { ArrowRight: step, ArrowUp: step, ArrowLeft: -step, ArrowDown: -step }[e.key];
    if (delta === undefined || this.readonly) return;
    e.preventDefault();
    this.#edit((d) => { d.angle = (d.angle + delta + 360) % 360; });
  }

  /* ------------------------------------------------------------------- glow */
  #glowDown(e) {
    if (this.readonly || !this._design.glow) return;
    const preview = this.renderRoot.querySelector('.preview');
    const handle = e.currentTarget;
    handle.setPointerCapture(e.pointerId);
    this._sel = 'glow';
    this._dragging = true;
    const move = (ev) => {
      const r = preview.getBoundingClientRect();
      this._design.glow.x = round1(clamp(((ev.clientX - r.left) / r.width) * 100, 0, 100));
      this._design.glow.y = round1(clamp(((ev.clientY - r.top) / r.height) * 100, 0, 100));
      this.requestUpdate();
    };
    const up = () => {
      handle.removeEventListener('pointermove', move);
      handle.removeEventListener('pointerup', up);
      handle.removeEventListener('pointercancel', up);
      this._dragging = false;
      this.#commit(clone(this._design));
    };
    handle.addEventListener('pointermove', move);
    handle.addEventListener('pointerup', up);
    handle.addEventListener('pointercancel', up);
  }
  #glowKey(e) {
    const step = e.shiftKey ? 10 : 1;
    const dx = { ArrowRight: step, ArrowLeft: -step }[e.key] ?? 0;
    const dy = { ArrowDown: step, ArrowUp: -step }[e.key] ?? 0;
    if ((!dx && !dy) || this.readonly) return;
    e.preventDefault();
    this.#edit((d) => { d.glow.x = round1(clamp(d.glow.x + dx, 0, 100)); d.glow.y = round1(clamp(d.glow.y + dy, 0, 100)); });
  }

  /* ----------------------------------------------------------------- render */
  render() {
    const d = this._design;
    if (!d) return this.#renderStart();
    const sel = this.#selected();
    const ratios = d.stops.map((s) => ratio(hexOf(s.colour)));
    const lowest = Math.min(...ratios);
    return html`
      <div class="preview" style="background:${background(d)}">
        <div class="sample">
          <strong>Sample heading</strong>
          <span>Normal-size text on this gradient</span>
        </div>
        ${d.glow ? html`<button type="button" class="glow ${this._sel === 'glow' ? 'is-sel' : ''}" style="left:${d.glow.x}%;top:${d.glow.y}%"
            aria-label="Glow position ${d.glow.x}% across, ${d.glow.y}% down. Drag or use the arrow keys."
            ?disabled=${this.readonly} @pointerdown=${(e) => this.#glowDown(e)} @keydown=${(e) => this.#glowKey(e)} @focus=${() => { this._sel = 'glow'; }}></button>` : nothing}
      </div>

      <div class="row">
        <div class="stops">
          <div class="label" id="stops-label">Colours <small>click the bar to add · drag to move · Delete removes</small></div>
          <div class="track" style="background:${linear(d, 90)}" @click=${(e) => this.#addAt(e)} title="Click to add a colour"></div>
          <div class="handles" role="group" aria-labelledby="stops-label">
            ${d.stops.map((s, i) => html`<button type="button" class="stop ${this._sel === i ? 'is-sel' : ''}" data-i=${i}
                style="left:${s.at}%;--c:${hexOf(s.colour)}"
                aria-label="Colour ${i + 1}: ${s.colour} at ${s.at}%" aria-pressed=${this._sel === i ? 'true' : 'false'}
                ?disabled=${this.readonly}
                @pointerdown=${(e) => this.#stopDown(e, i)} @keydown=${(e) => this.#stopKey(e, i)} @focus=${() => { if (!this._dragging) this._sel = i; }}></button>`)}
          </div>
        </div>
        <div class="angle">
          <div class="label">Direction</div>
          <div class="dial" role="slider" tabindex=${this.readonly ? -1 : 0} aria-label="Direction" aria-valuemin="0" aria-valuemax="359" aria-valuenow=${d.angle} aria-valuetext="${d.angle} degrees"
              @pointerdown=${(e) => this.#dialDown(e)} @keydown=${(e) => this.#dialKey(e)}>
            <span class="needle" style="transform:rotate(${d.angle}deg)"></span>
          </div>
          <div class="angle-input">
            <input type="number" min="0" max="359" .value=${String(d.angle)} ?disabled=${this.readonly} aria-label="Direction in degrees"
              @change=${(e) => this.#edit((x) => { x.angle = ((Math.round(Number(e.target.value) || 0) % 360) + 360) % 360; })}>°
          </div>
          <div class="presets">
            ${[[90, '→'], [135, '↘'], [180, '↓'], [45, '↗']].map(([a, arrow]) => html`<button type="button" class="mini ${d.angle === a ? 'is-sel' : ''}" ?disabled=${this.readonly}
                aria-label="${a} degrees" @click=${() => this.#edit((x) => { x.angle = a; })}>${arrow}</button>`)}
          </div>
        </div>
      </div>

      ${sel ? this.#renderSelected(sel) : nothing}

      <div class="footer">
        <uui-button look="secondary" compact label="Reverse colours" ?disabled=${this.readonly}
          @click=${() => this.#edit((x) => { x.stops = x.stops.map((s) => ({ ...s, at: round1(100 - s.at) })); })}>Reverse colours</uui-button>
        <uui-button look="secondary" compact label="Space colours evenly" ?disabled=${this.readonly}
          @click=${() => this.#edit((x) => { x.stops.forEach((s, i) => { s.at = round1((i * 100) / (x.stops.length - 1)); }); })}>Space evenly</uui-button>
        ${d.glow
          ? nothing
          : html`<uui-button look="secondary" compact label="Add glow" ?disabled=${this.readonly}
              @click=${() => { this.#edit((x) => { x.glow = { colour: x.stops[x.stops.length - 1].colour, x: 80, y: 20, strength: 60 }; }); this._sel = 'glow'; }}>Add glow</uui-button>`}
        <span class="verdict ${lowest < MIN ? 'bad' : lowest < NORMAL ? 'warn' : 'good'}" role="status">
          White text: lowest ${lowest}:1 —
          ${lowest < MIN ? 'too light, cannot be saved' : lowest < NORMAL ? 'fine for headings, small text may be hard to read' : 'readable for all text'}
        </span>
      </div>
      <p class="note">Theme colours are shown in the EHC default theme; occasion themes (National Day, Pink October…) recolour them. Custom colours stay the same in every theme.</p>
    `;
  }

  #renderSelected(sel) {
    const isGlow = this._sel === 'glow';
    const hex = hexOf(sel.colour);
    const custom = HEX.test(sel.colour);
    const r = ratio(hex);
    return html`
      <div class="selected">
        <div class="sel-head">
          <span class="chip" style="--c:${hex}"></span>
          <b>${isGlow ? 'Glow' : `Colour ${this._sel + 1}`}</b>
          <span class="muted">${custom ? `custom ${sel.colour}` : sel.colour}</span>
          ${isGlow
            ? html`<label class="inline">Strength <input type="range" min="10" max="100" .value=${String(sel.strength)} ?disabled=${this.readonly}
                  @change=${(e) => this.#edit((x) => { x.glow.strength = Number(e.target.value); })}> ${sel.strength}%</label>
                <uui-button look="secondary" color="danger" compact label="Remove glow" ?disabled=${this.readonly}
                  @click=${() => { this.#edit((x) => { x.glow = null; }); this._sel = 0; }}>Remove glow</uui-button>`
            : html`<label class="inline">Position <input type="number" min="0" max="100" step="1" .value=${String(sel.at)} ?disabled=${this.readonly}
                  @change=${(e) => { const at = round1(clamp(Number(e.target.value) || 0, 0, 100)); const c = sel.colour; this.#edit((x) => { x.stops[this._sel].at = at; }); this._sel = this._design.stops.findIndex((s) => s.at === at && s.colour === c); }}>%</label>
                <span class="ratio ${r < MIN ? 'bad' : r < NORMAL ? 'warn' : 'good'}">White text ${r}:1</span>
                <uui-button look="secondary" color="danger" compact label="Remove this colour" ?disabled=${this.readonly || this._design.stops.length <= 2}
                  @click=${() => this.#removeStop(this._sel)}>Remove</uui-button>`}
        </div>
        <div class="swatches" role="group" aria-label="Theme colours">
          ${Object.entries(THEME).map(([family, shades]) => html`
            <div class="family"><span>${family}</span>
              ${Object.entries(shades).map(([shade, h]) => {
                const label = `${family} ${shade}`;
                return html`<button type="button" class="swatch ${sel.colour === label ? 'is-sel' : ''}" style="--c:${h}" title=${label} aria-label=${label}
                  aria-pressed=${sel.colour === label ? 'true' : 'false'} ?disabled=${this.readonly} @click=${() => this.#setColour(label)}></button>`;
              })}
            </div>`)}
        </div>
        <div class="custom">
          <span>Custom colour</span>
          <input type="color" .value=${hex.toLowerCase()} ?disabled=${this.readonly} aria-label="Pick a custom colour"
            @change=${(e) => this.#setColour(e.target.value.toUpperCase())}>
          <input type="text" class="hex" maxlength="7" placeholder="#1C7DB4" .value=${this._hexDraft || (custom ? sel.colour : '')} ?disabled=${this.readonly} aria-label="Custom colour as #RRGGBB"
            @input=${(e) => { this._hexDraft = e.target.value; }}
            @change=${(e) => { const v = e.target.value.trim(); if (HEX.test(v)) this.#setColour(v.toUpperCase()); }}>
          ${this._hexDraft && !HEX.test(this._hexDraft.trim()) ? html`<small class="bad">Use a colour like #1C7DB4.</small>` : nothing}
        </div>
      </div>`;
  }

  #renderStart() {
    return html`
      <div class="start">
        <p>Start from one of these, then change it:</p>
        <div class="starters">
          ${STARTERS.map((s) => html`<button type="button" class="starter" ?disabled=${this.readonly} @click=${() => { this._sel = 0; this.#commit(clone(s.design)); }}>
              <span style="background:${background(s.design)}"></span>${s.name}</button>`)}
        </div>
      </div>`;
  }

  static styles = css`
    :host { display: grid; gap: var(--uui-size-space-5); max-width: 880px; }
    button { font: inherit; cursor: pointer; }
    button:disabled { cursor: default; }
    :focus-visible { outline: 2px solid var(--uui-color-focus, #3544b1); outline-offset: 2px; }
    .label { font-weight: 600; margin-bottom: var(--uui-size-space-2); }
    .label small, .muted, .note { color: var(--uui-color-text-alt); font-weight: 400; }
    .note { margin: 0; font-size: 12px; }

    .preview { position: relative; height: 200px; border-radius: 14px; overflow: hidden; color: #fff; }
    .sample { position: absolute; inset-inline-start: 24px; bottom: 22px; display: grid; gap: 4px; pointer-events: none; }
    .sample strong { font-size: 28px; line-height: 1.15; }
    .sample span { font-size: 14px; opacity: .9; }
    .glow { position: absolute; width: 26px; height: 26px; margin: -13px 0 0 -13px; border-radius: 50%; border: 2px solid #fff; background: rgb(255 255 255 / .25); box-shadow: 0 0 0 1px rgb(0 0 0 / .35); touch-action: none; }
    .glow.is-sel { box-shadow: 0 0 0 3px var(--uui-color-selected, #3544b1); }

    .row { display: grid; grid-template-columns: 1fr auto; gap: var(--uui-size-space-6); align-items: start; }
    .stops { min-width: 0; }
    .track { height: 30px; border-radius: 8px; border: 1px solid var(--uui-color-border); cursor: copy; }
    .handles { position: relative; height: 34px; margin: 0 2px; }
    .stop { position: absolute; top: 4px; width: 22px; height: 26px; margin-left: -11px; padding: 0; border: 2px solid #fff; border-radius: 4px 4px 8px 8px; background: var(--c); box-shadow: 0 0 0 1px rgb(0 0 0 / .45); touch-action: none; }
    .stop::before { content: ""; position: absolute; left: 50%; top: -8px; margin-left: -5px; border: 5px solid transparent; border-bottom-color: rgb(0 0 0 / .55); }
    .stop.is-sel { box-shadow: 0 0 0 3px var(--uui-color-selected, #3544b1); z-index: 1; }

    .angle { display: grid; justify-items: center; gap: var(--uui-size-space-2); }
    .dial { position: relative; width: 64px; height: 64px; border-radius: 50%; border: 1px solid var(--uui-color-border); background: var(--uui-color-surface-alt); touch-action: none; cursor: grab; }
    .needle { position: absolute; left: 50%; top: 8px; bottom: 50%; width: 2px; margin-left: -1px; background: var(--uui-color-text); transform-origin: 50% 100%; }
    .needle::before { content: ""; position: absolute; top: -4px; left: -4px; border: 5px solid transparent; border-bottom-color: var(--uui-color-text); border-top: 0; }
    .angle-input input { width: 56px; }
    .presets { display: flex; gap: 4px; }
    .mini { width: 28px; height: 28px; border: 1px solid var(--uui-color-border); border-radius: 6px; background: var(--uui-color-surface); }
    .mini.is-sel { border-color: var(--uui-color-selected); background: var(--uui-color-selected); color: var(--uui-color-selected-contrast, #fff); }

    .selected { display: grid; gap: var(--uui-size-space-4); padding: var(--uui-size-space-5); border: 1px solid var(--uui-color-border); border-radius: 10px; background: var(--uui-color-surface); }
    .sel-head { display: flex; flex-wrap: wrap; align-items: center; gap: var(--uui-size-space-4); }
    .chip { width: 22px; height: 22px; border-radius: 6px; background: var(--c); box-shadow: inset 0 0 0 1px rgb(0 0 0 / .2); }
    .inline { display: inline-flex; align-items: center; gap: 6px; }
    .inline input[type=number] { width: 64px; }
    .swatches { display: grid; gap: 6px; }
    .family { display: flex; align-items: center; gap: 6px; }
    .family > span { width: 72px; font-size: 12px; color: var(--uui-color-text-alt); }
    .swatch { width: 26px; height: 26px; padding: 0; border-radius: 6px; border: 1px solid rgb(0 0 0 / .15); background: var(--c); }
    .swatch.is-sel { box-shadow: 0 0 0 2px var(--uui-color-surface), 0 0 0 4px var(--uui-color-selected, #3544b1); }
    .custom { display: flex; flex-wrap: wrap; align-items: center; gap: var(--uui-size-space-3); }
    .custom input[type=color] { width: 40px; height: 30px; padding: 0; border: 1px solid var(--uui-color-border); border-radius: 6px; background: none; }
    .hex { width: 96px; font-family: monospace; }

    .footer { display: flex; flex-wrap: wrap; align-items: center; gap: var(--uui-size-space-3); }
    .verdict, .ratio { font-size: 13px; padding: 4px 10px; border-radius: 999px; }
    .good { background: #e3f4e8; color: #14532d; }
    .warn { background: #fdf3d8; color: #6b4a00; }
    .bad { background: #fde4e4; color: #8a1c1c; }
    small.bad { background: none; }

    .start p { margin: 0 0 var(--uui-size-space-3); }
    .starters { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-4); }
    .starter { display: grid; gap: 6px; padding: 6px; border: 1px solid var(--uui-color-border); border-radius: 10px; background: var(--uui-color-surface); text-align: center; }
    .starter span { display: block; width: 140px; height: 64px; border-radius: 6px; }
    .starter:hover { border-color: var(--uui-color-selected); }

    @media (max-width: 640px) { .row { grid-template-columns: 1fr; } }
    @media (prefers-reduced-motion: reduce) { * { transition: none !important; } }
  `;
}

customElements.define('ehc-gradient-editor', EhcGradientEditor);
