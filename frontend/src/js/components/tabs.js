/* Generic WAI-ARIA tabs for [data-tabs] containers: click, Arrow keys (RTL-aware), Home/End. Safe to load twice. */
(function () {
  'use strict';
  function init(root) {
    if (root.hasAttribute('data-tabs-ready')) return;
    root.setAttribute('data-tabs-ready', '');
    var tabs = [].slice.call(root.querySelectorAll('[role="tab"]'));
    if (tabs.length < 2) return;
    function select(i, focus) {
      tabs.forEach(function (t, j) {
        var on = i === j, panel = document.getElementById(t.getAttribute('aria-controls'));
        t.setAttribute('aria-selected', on ? 'true' : 'false');
        t.setAttribute('tabindex', on ? '0' : '-1');
        if (panel) panel.classList.toggle('hidden', !on);
        if (on && focus) t.focus();
      });
    }
    tabs.forEach(function (t, i) {
      t.addEventListener('click', function () { select(i, false); });
      t.addEventListener('keydown', function (e) {
        var rtl = document.documentElement.dir === 'rtl', n = tabs.length, next = null;
        if (e.key === 'ArrowRight') next = (i + (rtl ? -1 : 1) + n) % n;
        else if (e.key === 'ArrowLeft') next = (i + (rtl ? 1 : -1) + n) % n;
        else if (e.key === 'Home') next = 0;
        else if (e.key === 'End') next = n - 1;
        if (next !== null) { e.preventDefault(); select(next, true); }
      });
    });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-tabs]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
