/* Facility maps ([data-map]) with Leaflet (served from /assets/vendor/leaflet).
   Points come from data-points (JSON written by Razor) or data-src (GET /api/facilities…).
   The list next to the map is the accessible alternative; the map is an enhancement.
   Tiles: OpenStreetMap for development. Production tile provider is an open decision (docs/06-integrations.md). */
(function () {
  'use strict';
  var TILES = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
  var ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';
  var CENTER = [26.42, 50.09]; // Eastern Province (Dammam) when there are no points

  function pinIcon(er) {
    return window.L.divIcon({
      className: 'ehc-pin',
      html: '<span class="block h-5 w-5 rounded-full border-[3px] border-white shadow-soft ' + (er ? 'bg-emerg-600' : 'bg-brand-600') + '"></span>',
      iconSize: [20, 20], iconAnchor: [10, 10]
    });
  }

  function draw(el, points) {
    var L = window.L;
    var map = L.map(el, { scrollWheelZoom: false, zoomControl: true });
    L.tileLayer(TILES, { maxZoom: 18, detectRetina: true, attribution: ATTRIBUTION }).addTo(map);
    var bounds = [], markers = [];
    points.forEach(function (p) {
      var lat = +p.lat, lng = +p.lng;
      if (!isFinite(lat) || !isFinite(lng) || (!lat && !lng)) return;
      var m = L.marker([lat, lng], { icon: pinIcon(!!(p.er || p.hasEmergency)), title: p.name, alt: p.name, keyboard: true }).addTo(map);
      var a = document.createElement(p.url ? 'a' : 'b');
      a.textContent = p.name;
      if (p.url) a.href = p.url;
      m.bindPopup(a);
      bounds.push([lat, lng]);
      markers.push({ marker: m, type: p.type || '', er: !!(p.er || p.hasEmergency), lat: lat, lng: lng, id: p.id });
      // highlight the matching card in a list on the same page
      m.on('click', function () {
        var card = document.querySelector('[data-facility="' + p.id + '"]');
        if (card) card.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      });
    });
    if (bounds.length > 1) map.fitBounds(bounds, { padding: [30, 30], maxZoom: 13 });
    else if (bounds.length === 1) map.setView(bounds[0], parseInt(el.getAttribute('data-zoom') || '13', 10));
    else map.setView(CENTER, 9);
    // filter from outside: el.dispatchEvent(new CustomEvent('map:filter', { detail: 'hospital' | 'emergency' | 'all' }))
    el.addEventListener('map:filter', function (e) {
      var f = e.detail || 'all';
      markers.forEach(function (x) {
        var on = f === 'all' || (f === 'emergency' ? x.er : x.type === f);
        if (on && !map.hasLayer(x.marker)) x.marker.addTo(map);
        if (!on && map.hasLayer(x.marker)) map.removeLayer(x.marker);
      });
    });
    el.__markers = markers;
    return map;
  }

  function init(el) {
    if (el.hasAttribute('data-map-ready') || !window.L) return;
    el.setAttribute('data-map-ready', '');
    var src = el.getAttribute('data-src');
    if (src) {
      fetch(src, { headers: { 'Accept': 'application/json' } })
        .then(function (r) { return r.ok ? r.json() : []; })
        .then(function (items) { el.__map = draw(el, items || []); el.dispatchEvent(new CustomEvent('map:ready', { detail: items })); })
        .catch(function () { el.__map = draw(el, []); });
      return;
    }
    var points;
    try { points = JSON.parse(el.getAttribute('data-points') || '[]'); } catch (e) { points = []; }
    el.__map = draw(el, points);
  }

  // Leaflet is only downloaded when a map is about to be seen (keeps it off the critical path)
  var me = document.currentScript;
  var jsUrl = (me && me.getAttribute('data-leaflet-js')) || '/assets/vendor/leaflet/leaflet.js';
  var cssUrl = (me && me.getAttribute('data-leaflet-css')) || '/assets/vendor/leaflet/leaflet.css';
  var loading = null;
  function leaflet() {
    if (window.L) return Promise.resolve();
    if (loading) return loading;
    loading = new Promise(function (resolve, reject) {
      if (!document.querySelector('link[data-leaflet]')) {
        var l = document.createElement('link'); l.rel = 'stylesheet'; l.href = cssUrl; l.setAttribute('data-leaflet', ''); document.head.appendChild(l);
      }
      var s = document.createElement('script'); s.src = jsUrl; s.onload = function () { resolve(); }; s.onerror = reject; document.head.appendChild(s);
    });
    return loading;
  }
  function start(el) { leaflet().then(function () { init(el); }).catch(function () { /* list stays usable without the map */ }); }
  function all() {
    var maps = [].slice.call(document.querySelectorAll('[data-map]:not([data-map-ready])'));
    if (!maps.length) return;
    if (!('IntersectionObserver' in window)) { maps.forEach(start); return; }
    var io = new IntersectionObserver(function (es) {
      es.forEach(function (e) { if (e.isIntersecting) { io.unobserve(e.target); start(e.target); } });
    }, { rootMargin: '400px 0px' });
    maps.forEach(function (m) { io.observe(m); });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
