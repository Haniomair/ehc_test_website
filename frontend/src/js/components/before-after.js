/* Before / after slider: turns the side-by-side panes into one image with a divider driven by a native range input
   (mouse, touch, keyboard and screen readers all work). */
(function () {
  'use strict';
  function init(box) {
    if (box.classList.contains('is-ready')) return;
    var range = box.querySelector('.compare-range');
    if (!range) return;
    function set() { box.style.setProperty('--pos', range.value + '%'); range.setAttribute('aria-valuetext', range.value + '%'); }
    range.hidden = false;
    box.classList.add('is-ready');
    range.addEventListener('input', set);
    set();
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-compare]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
