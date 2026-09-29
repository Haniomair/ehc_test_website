/* Count-up for [data-count] numbers when they scroll into view. Only plain numbers animate ("1,200", "98.5");
   placeholders like "##" and anything else stay exactly as typed. Off under prefers-reduced-motion. */
(function () {
  'use strict';
  var reduce = document.documentElement.classList.contains('a11y-still') || (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  function run(el) {
    var raw = (el.getAttribute('data-count') || '').trim();
    if (!/^\d[\d,]*(\.\d+)?$/.test(raw)) return;
    var target = parseFloat(raw.replace(/,/g, '')), decimals = (raw.split('.')[1] || '').length, grouped = raw.indexOf(',') >= 0;
    var fmt = function (v) {
      var s = v.toFixed(decimals);
      return grouped ? s.replace(/\B(?=(\d{3})+(?!\d))/g, ',') : s;
    };
    var start = null, dur = 1400;
    function step(t) {
      if (start === null) start = t;
      var p = Math.min(1, (t - start) / dur), eased = 1 - Math.pow(1 - p, 3);
      el.textContent = fmt(target * eased);
      if (p < 1) requestAnimationFrame(step); else el.textContent = raw;
    }
    el.textContent = fmt(0);
    requestAnimationFrame(step);
  }
  function all() {
    var els = [].slice.call(document.querySelectorAll('[data-count]:not([data-count-ready])'));
    els.forEach(function (e) { e.setAttribute('data-count-ready', ''); });
    if (reduce || !('IntersectionObserver' in window)) return;
    var io = new IntersectionObserver(function (es) {
      es.forEach(function (e) { if (e.isIntersecting) { io.unobserve(e.target); run(e.target); } });
    }, { threshold: 0.4 });
    els.forEach(function (e) { io.observe(e); });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
