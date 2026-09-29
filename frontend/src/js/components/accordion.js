/* Accordions built on <details>: in a [data-accordion="single"] group, opening one item closes the others. */
(function () {
  'use strict';
  function init(group) {
    if (group.hasAttribute('data-accordion-ready')) return;
    group.setAttribute('data-accordion-ready', '');
    var items = [].slice.call(group.querySelectorAll('details'));
    items.forEach(function (d) {
      d.addEventListener('toggle', function () {
        if (!d.open) return;
        items.forEach(function (o) { if (o !== d && o.open) o.open = false; });
      });
    });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-accordion="single"]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
