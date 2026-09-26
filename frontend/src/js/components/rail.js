/* Horizontal rails: buttons with data-rail-prev / data-rail-next="{railId}" scroll the rail by one card (RTL-aware). */
(function () {
  'use strict';
  function step(btn, dir) {
    var rail = document.getElementById(btn.getAttribute(dir > 0 ? 'data-rail-next' : 'data-rail-prev'));
    if (!rail) return;
    var card = rail.firstElementChild, width = card ? card.getBoundingClientRect().width + 20 : 320;
    var rtl = getComputedStyle(rail).direction === 'rtl';
    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    rail.scrollBy({ left: dir * (rtl ? -1 : 1) * width, behavior: reduce ? 'auto' : 'smooth' });
  }
  function all() {
    [].forEach.call(document.querySelectorAll('[data-rail-prev]:not([data-rail-ready])'), function (b) { b.setAttribute('data-rail-ready', ''); b.addEventListener('click', function () { step(b, -1); }); });
    [].forEach.call(document.querySelectorAll('[data-rail-next]:not([data-rail-ready])'), function (b) { b.setAttribute('data-rail-ready', ''); b.addEventListener('click', function () { step(b, 1); }); });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
