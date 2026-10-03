/* Facility finder ([data-finder]): the type buttons and the (optional) health-network dropdown update the list, the
   count and the map; "Use my location" sorts by distance. Location stays in the browser (no request is made with it).
   Tablets and desktop show every match in a scrolling list beside the map; phones show the first few (data-max) and "Show more". */
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
    var empty = root.querySelector('[data-finder-empty]'), more = root.querySelector('[data-finder-more]');
    var chips = [].slice.call(root.querySelectorAll('[data-finder-filter]'));
    var select = root.querySelector('select[data-finder-network]');
    var netLinks = [].slice.call(root.querySelectorAll('[data-finder-network-link]'));
    var network = 'all';
    var step = parseInt(root.getAttribute('data-max') || '6', 10) || 6, limit = step;
    var filter = root.getAttribute('data-filter') || 'all', here = null;
    var countText = root.getAttribute('data-count-text') || '{0}';
    var lang = document.documentElement.lang || undefined;
    // Latin digits like the rest of the site; distances as "1.3 km" / "1.3 كم" in the page language
    var loc = lang ? lang + '-u-nu-latn' : undefined, fmt = null, kmFmt = null;
    try { fmt = new Intl.NumberFormat(loc, { maximumFractionDigits: 1 }); kmFmt = new Intl.NumberFormat(loc, { style: 'unit', unit: 'kilometer', unitDisplay: 'short', maximumFractionDigits: 1 }); } catch (e) { }
    var items = [].slice.call(list ? list.children : []);

    function matches(el) {
      var typeOk = filter === 'all' || (filter === 'emergency' ? el.getAttribute('data-emergency') === '1' : el.getAttribute('data-type') === filter);
      return typeOk && (network === 'all' || el.getAttribute('data-network') === network);
    }
    function render(toMap) {
      var ordered = items.slice();
      if (here) {
        ordered.forEach(function (el) {
          var lat = parseFloat(el.getAttribute('data-lat')), lng = parseFloat(el.getAttribute('data-lng'));
          el.__d = isFinite(lat) && isFinite(lng) ? km(here, [lat, lng]) : Infinity;
          var d = el.querySelector('[data-finder-distance]');
          if (d && isFinite(el.__d)) { d.textContent = kmFmt ? kmFmt.format(el.__d) : el.__d.toFixed(1) + ' km'; d.classList.remove('hidden'); }
        });
        ordered.sort(function (a, b) { return a.__d - b.__d; });
        ordered.forEach(function (el) { list.appendChild(el); });
      }
      // every match is listed (tablet / desktop scroll); on phones only the first `limit` are shown
      var total = 0;
      ordered.forEach(function (el) {
        var on = matches(el);
        el.classList.toggle('hidden', !on);
        el.classList.toggle('max-md:hidden', on && total >= limit);
        if (on) total++;
      });
      if (status) status.textContent = countText.replace('{0}', fmt ? fmt.format(total) : String(total));
      if (empty) empty.classList.toggle('hidden', total > 0);
      if (more) more.classList.toggle('hidden', total <= limit);
      netLinks.forEach(function (a) { a.classList.toggle('hidden', a.getAttribute('data-finder-network-link') !== network); });
      if (toMap === false || !map) return;
      // the map shows every matching facility: by type, or (with a network chosen) by id
      var ids = items.filter(matches).map(function (el) { var card = el.querySelector('[data-facility]'); return card ? card.getAttribute('data-facility') : ''; });
      map.dispatchEvent(new CustomEvent('map:filter', { detail: network === 'all' ? filter : ids }));
    }
    chips.forEach(function (c) {
      c.addEventListener('click', function () {
        filter = c.getAttribute('data-finder-filter');
        chips.forEach(function (x) { x.setAttribute('aria-pressed', x === c ? 'true' : 'false'); });
        limit = step;
        render();
      });
    });
    if (select) select.addEventListener('change', function () { network = select.value || 'all'; limit = step; render(); });
    if (more) more.addEventListener('click', function () {
      var first = items.filter(function (el) { return matches(el); })[limit];
      limit += step;
      render(false);
      var a = first && first.querySelector('a');
      if (a) a.focus({ preventScroll: true });
    });
    function located(pos) {
      here = pos;
      if (locate) { locate.removeAttribute('aria-busy'); locate.setAttribute('aria-pressed', 'true'); }
      if (list) list.scrollTop = 0;
      render();
    }
    // the toolbar button and the map's own "my location" button do the same: the map shows the position (map:located)
    var locate = root.querySelector('[data-finder-locate]');
    if (locate) {
      if (!navigator.geolocation || window.isSecureContext === false) locate.classList.add('hidden');
      else locate.addEventListener('click', function () {
        if (map && map.__locate) { map.__locate(); return; }
        locate.setAttribute('aria-busy', 'true');
        navigator.geolocation.getCurrentPosition(function (p) { located([p.coords.latitude, p.coords.longitude]); },
          function () { locate.removeAttribute('aria-busy'); }, { enableHighAccuracy: false, timeout: 10000, maximumAge: 300000 });
      });
    }
    if (map) {
      map.addEventListener('map:ready', function () { render(); });
      map.addEventListener('map:located', function (e) { if (e.detail) located(e.detail); });
    }
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-finder]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
