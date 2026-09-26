/* Facility finder ([data-finder]): filter chips update the list and the map; "Use my location" sorts by distance.
   Location stays in the browser (no request is made with it). */
(function () {
  'use strict';
  function km(a, b) {
    var R = 6371, rad = Math.PI / 180, dLat = (b[0] - a[0]) * rad, dLng = (b[1] - a[1]) * rad;
    var h = Math.sin(dLat / 2) * Math.sin(dLat / 2) + Math.cos(a[0] * rad) * Math.cos(b[0] * rad) * Math.sin(dLng / 2) * Math.sin(dLng / 2);
    return 2 * R * Math.asin(Math.sqrt(h));
  }
  function init(root) {
    if (root.hasAttribute('data-finder-ready')) return;
    root.setAttribute('data-finder-ready', '');
    var list = root.querySelector('[data-finder-list]'), map = root.querySelector('[data-map]'), status = root.querySelector('[data-finder-status]');
    var chips = [].slice.call(root.querySelectorAll('[data-finder-filter]'));
    var max = parseInt(root.getAttribute('data-max') || '6', 10), filter = root.getAttribute('data-filter') || 'all', here = null;
    var fmt = window.Intl ? new Intl.NumberFormat(document.documentElement.lang || undefined, { maximumFractionDigits: 1 }) : null;
    var items = [].slice.call(list ? list.children : []);

    function matches(el) { return filter === 'all' || (filter === 'emergency' ? el.getAttribute('data-er') === '1' : el.getAttribute('data-type') === filter); }
    function render() {
      var ordered = items.slice();
      if (here) {
        ordered.forEach(function (el) {
          var lat = parseFloat(el.getAttribute('data-lat')), lng = parseFloat(el.getAttribute('data-lng'));
          el.__d = isFinite(lat) && isFinite(lng) ? km(here, [lat, lng]) : Infinity;
          var d = el.querySelector('[data-finder-distance]');
          if (d && isFinite(el.__d)) { d.textContent = (fmt ? fmt.format(el.__d) : el.__d.toFixed(1)) + ' km'; d.classList.remove('hidden'); }
        });
        ordered.sort(function (a, b) { return a.__d - b.__d; });
        ordered.forEach(function (el) { list.appendChild(el); });
      }
      var shown = 0;
      ordered.forEach(function (el) { var on = matches(el) && shown < max; if (on) shown++; el.classList.toggle('hidden', !on); });
      if (status) status.textContent = String(shown);
      if (map) map.dispatchEvent(new CustomEvent('map:filter', { detail: filter }));
    }
    chips.forEach(function (c) {
      c.addEventListener('click', function () {
        filter = c.getAttribute('data-finder-filter');
        chips.forEach(function (x) { x.setAttribute('aria-pressed', x === c ? 'true' : 'false'); });
        render();
      });
    });
    var locate = root.querySelector('[data-finder-locate]');
    if (locate) {
      if (!navigator.geolocation) locate.classList.add('hidden');
      else locate.addEventListener('click', function () {
        locate.setAttribute('aria-busy', 'true');
        navigator.geolocation.getCurrentPosition(function (pos) {
          here = [pos.coords.latitude, pos.coords.longitude];
          locate.removeAttribute('aria-busy');
          if (map && map.__map) map.__map.setView(here, 11);
          render();
        }, function () { locate.removeAttribute('aria-busy'); }, { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 });
      });
    }
    if (map) map.addEventListener('map:ready', render);
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-finder]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
