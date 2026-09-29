/* Image hotspots: a point ([data-hotspot]) opens its list entry ([data-hotspot-entry]) and closes the others; the open
   entry's point is marked with aria-current. Opening an entry from the list highlights its point too. */
(function () {
  'use strict';
  function init(root) {
    if (root.hasAttribute('data-hotspots-ready')) return;
    root.setAttribute('data-hotspots-ready', '');
    var points = [].slice.call(root.querySelectorAll('[data-hotspot]'));
    var entries = [].slice.call(root.querySelectorAll('[data-hotspot-entry]'));
    function mark() {
      points.forEach(function (p) {
        var e = entries.filter(function (x) { return x.getAttribute('data-hotspot-entry') === p.getAttribute('data-hotspot'); })[0];
        if (e && e.open) p.setAttribute('aria-current', 'true'); else p.removeAttribute('aria-current');
      });
    }
    entries.forEach(function (e) {
      e.addEventListener('toggle', function () {
        if (e.open) entries.forEach(function (o) { if (o !== e && o.open) o.open = false; });
        mark();
      });
    });
    points.forEach(function (p) {
      p.addEventListener('click', function (ev) {
        var e = entries.filter(function (x) { return x.getAttribute('data-hotspot-entry') === p.getAttribute('data-hotspot'); })[0];
        if (!e) return;
        ev.preventDefault();
        e.open = true;
        var wide = window.matchMedia('(min-width: 1024px)').matches;
        if (!wide) e.scrollIntoView({ block: 'nearest' });
        e.querySelector('summary').focus({ preventScroll: wide });
      });
    });
    mark();
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-hotspots]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
