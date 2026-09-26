/* BMI calculator ([data-bmi]). Category labels/tips come from data-categories (dictionary texts), not from this file. */
(function () {
  'use strict';
  var STYLE = {
    under: 'bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300',
    healthy: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
    over: 'bg-amber-50 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
    obese: 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300'
  };
  function init(root) {
    if (root.hasAttribute('data-bmi-ready')) return;
    root.setAttribute('data-bmi-ready', '');
    var cats;
    try { cats = JSON.parse(root.getAttribute('data-categories') || '[]'); } catch (e) { return; }
    var q = function (s) { return root.querySelector(s); };
    var h = q('[data-bmi-h]'), w = q('[data-bmi-w]');
    if (!h || !w || !cats.length) return;
    var hv = q('[data-bmi-hv]'), wv = q('[data-bmi-wv]'), val = q('[data-bmi-value]'), cat = q('[data-bmi-cat]'), tip = q('[data-bmi-tip]'), marker = q('[data-bmi-marker]');
    var fmt = window.Intl ? new Intl.NumberFormat(document.documentElement.lang || undefined, { maximumFractionDigits: 1, minimumFractionDigits: 1 }) : null;
    function update() {
      var hm = +h.value / 100, kg = +w.value, b = kg / (hm * hm), c = cats[cats.length - 1];
      for (var i = 0; i < cats.length; i++) if (b < cats[i][0]) { c = cats[i]; break; }
      if (hv) hv.textContent = h.value;
      if (wv) wv.textContent = w.value;
      if (val) val.textContent = fmt ? fmt.format(b) : b.toFixed(1);
      if (cat) { cat.className = 'mt-2 inline-block rounded-full px-3 py-1 text-sm font-bold ' + (STYLE[c[3]] || ''); cat.textContent = c[1]; }
      if (tip) tip.textContent = c[2];
      if (marker) marker.style.insetInlineStart = Math.max(0, Math.min(100, (b - 15) / 25 * 100)) + '%';
    }
    h.addEventListener('input', update);
    w.addEventListener('input', update);
    update();
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-bmi]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
