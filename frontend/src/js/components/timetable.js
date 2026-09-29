/* Timetable day filter: chips in [data-timetable-filter="{id}"] show one day group ([data-day]) of #{id}, or all. */
(function () {
  'use strict';
  function init(bar) {
    if (bar.hasAttribute('data-timetable-ready')) return;
    bar.setAttribute('data-timetable-ready', '');
    var list = document.getElementById(bar.getAttribute('data-timetable-filter'));
    if (!list) return;
    var chips = [].slice.call(bar.querySelectorAll('button[data-day]'));
    var groups = [].slice.call(list.children).filter(function (g) { return g.hasAttribute('data-day'); });
    chips.forEach(function (chip) {
      chip.addEventListener('click', function () {
        var day = chip.getAttribute('data-day');
        chips.forEach(function (c) { c.setAttribute('aria-pressed', c === chip ? 'true' : 'false'); });
        groups.forEach(function (g) { g.hidden = day !== '' && g.getAttribute('data-day') !== day; });
      });
    });
    bar.hidden = false;
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-timetable-filter]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
