/* Facility maps ([data-map]) with Leaflet (served from /assets/vendor/leaflet).
   Points come from, in order: the facility finder's own list (same section), data-points (JSON written by Razor),
   or data-src (GET /api/facilities…). The list next to the map is the accessible alternative; the map is an enhancement.
   Look: theme-tinted tiles (ehc.css), pins with the facility-type icon, clusters when there are many points,
   card popups with details / directions, and list cards that highlight their pin on hover or focus.
   Basemap: the self-hosted PMTiles file (data-tiles on the script tag, built by `npm run tiles`) drawn by protomaps-leaflet;
   no third-party map requests. Without the file the pins show on a plain background. */
(function () {
  'use strict';
  // OpenStreetMap data is ODbL: the attribution must stay on every map
  var ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> · <a href="https://protomaps.com">Protomaps</a>';
  var TILES_MAX_ZOOM = 14; // highest zoom stored in the file (npm run tiles); deeper zooms scale that data
  var CENTER = [26.42, 50.09]; // Eastern Province (Dammam) when there are no points
  var CLUSTER_FROM = 15;       // cluster only maps with many points
  var ICONS = { hospital: 'hosp', primaryCare: 'clinic', specialist: 'star' };

  var me = document.currentScript;
  function attr(n, d) { return (me && me.getAttribute(n)) || d; }
  var T = {
    zoomIn: attr('data-label-zoom-in', 'Zoom in'), zoomOut: attr('data-label-zoom-out', 'Zoom out'),
    showAll: attr('data-label-show-all', 'Show all'), details: attr('data-label-details', 'Details'),
    directions: attr('data-label-directions', 'Directions'),
    twoFingers: attr('data-label-two-fingers', 'Use two fingers to move the map'),
    myLocation: attr('data-label-my-location', 'My location'),
    locateFailed: attr('data-label-locate-failed', 'Your location could not be found')
  };
  // geolocation only works on https (and localhost); the position never leaves the browser
  var CAN_LOCATE = !!navigator.geolocation && window.isSecureContext !== false;
  var rtl = document.documentElement.dir === 'rtl';

  function svgIcon(name, cls) {
    var ns = 'http://www.w3.org/2000/svg', svg = document.createElementNS(ns, 'svg'), use = document.createElementNS(ns, 'use');
    svg.setAttribute('class', cls || 'ico'); svg.setAttribute('aria-hidden', 'true'); use.setAttribute('href', '#i-' + name); svg.appendChild(use);
    return svg;
  }
  // teardrop pin with the type icon; its tip sits on the coordinate
  function pinIcon(p) {
    return window.L.divIcon({
      className: 'ehc-pin' + (p.er ? ' is-er' : ''),
      html: '<span class="ehc-pin-body"><svg class="ico" aria-hidden="true"><use href="#i-' + (ICONS[p.type] || 'hosp') + '"></use></svg></span>',
      iconSize: [32, 40], iconAnchor: [16, 39], popupAnchor: [0, -36]
    });
  }
  function clusterIcon(c) {
    var kids = c.getAllChildMarkers(), n = kids.length;
    var er = kids.some(function (m) { return m.options.ehcEr; });
    var size = n < 10 ? 38 : n < 50 ? 44 : 50;
    return window.L.divIcon({
      className: 'ehc-cluster' + (er ? ' has-er' : ''),
      html: '<span>' + n + '</span>', iconSize: [size, size]
    });
  }
  function popup(p) {
    var box = document.createElement('div'); box.className = 'ehc-popup';
    var title = document.createElement(p.url ? 'a' : 'b'); title.className = 'ehc-popup-title'; title.textContent = p.name;
    if (p.url) title.href = p.url;
    box.appendChild(title);
    if (p.meta) { var m = document.createElement('p'); m.className = 'ehc-popup-meta'; m.textContent = p.meta; box.appendChild(m); }
    var row = document.createElement('div'); row.className = 'ehc-popup-actions';
    if (p.url) { var d = document.createElement('a'); d.href = p.url; d.className = 'ehc-popup-btn is-primary'; d.textContent = T.details; row.appendChild(d); }
    var g = document.createElement('a');
    g.href = 'https://www.openstreetmap.org/directions?to=' + p.lat + '%2C' + p.lng; g.target = '_blank'; g.rel = 'noopener';
    g.className = 'ehc-popup-btn'; g.appendChild(svgIcon('pin', 'ico !h-4 !w-4')); g.appendChild(document.createTextNode(T.directions));
    row.appendChild(g); box.appendChild(row);
    return box;
  }

  // Sea names are letter-spaced in the basemap style, which protomaps-leaflet can't draw for Arabic (joined letters
  // break or get cut). On Arabic pages those labels are left out; place names are unaffected.
  function dropSeaLabels(rules) {
    for (var i = (rules || []).length - 1; i >= 0; i--) if (rules[i].dataLayer === 'water') rules.splice(i, 1);
  }

  // shown briefly when a single finger drags across the map (the page scrolls instead)
  function twoFingerHint(el) {
    var hint = document.createElement('div'), timer = null, moved = false, x0 = 0, y0 = 0;
    hint.className = 'ehc-map-hint'; hint.setAttribute('aria-hidden', 'true'); hint.textContent = T.twoFingers;
    el.appendChild(hint);
    el.addEventListener('touchstart', function (e) {
      moved = false; x0 = e.touches[0].clientX; y0 = e.touches[0].clientY;
      if (e.touches.length > 1) { clearTimeout(timer); hint.classList.remove('is-on'); }
    }, { passive: true });
    el.addEventListener('touchmove', function (e) {
      if (moved || e.touches.length !== 1 || Math.abs(e.touches[0].clientX - x0) + Math.abs(e.touches[0].clientY - y0) < 12) return;
      moved = true;
      hint.classList.add('is-on');
      clearTimeout(timer); timer = setTimeout(function () { hint.classList.remove('is-on'); }, 1500);
    }, { passive: true });
  }

  // a short message over the map (e.g. location not available)
  function flash(el, text) {
    var box = el.querySelector('.ehc-map-msg');
    if (!box) { box = document.createElement('div'); box.className = 'ehc-map-hint ehc-map-msg'; box.setAttribute('role', 'status'); el.appendChild(box); }
    box.textContent = text; box.classList.add('is-on');
    clearTimeout(box.__t); box.__t = setTimeout(function () { box.classList.remove('is-on'); }, 2600);
  }

  function draw(el, points) {
    var L = window.L;
    var map = L.map(el, { scrollWheelZoom: false, zoomControl: false, maxZoom: 18 });
    // fingers and pens scroll the page over the map; two fingers move / zoom it (pinch also pans); a mouse drags it.
    // Decided per press, not from media queries: Samsung devices report hover: hover on phones (S Pen / DeX).
    // Runs before Leaflet's own pointerdown handler (capture), and CSS keeps touch-action: pan-x pan-y (ehc.css).
    el.addEventListener('pointerdown', function (e) {
      if (e.pointerType === 'mouse') map.dragging.enable(); else map.dragging.disable();
    }, true);
    twoFingerHint(el);
    var tiles = attr('data-tiles', '');
    if (tiles && window.protomapsL) {
      // one light style; dark mode recolours it in CSS, so switching theme needs no reload
      var lang = document.documentElement.lang || 'ar';
      var base = window.protomapsL.leafletLayer({ url: tiles, flavor: 'light', lang: lang, maxDataZoom: TILES_MAX_ZOOM, attribution: ATTRIBUTION });
      if (lang === 'ar') dropSeaLabels(base.labelRules);
      base.addTo(map);
    }
    L.control.zoom({ position: rtl ? 'topright' : 'topleft', zoomInTitle: T.zoomIn, zoomOutTitle: T.zoomOut }).addTo(map);

    var clustered = points.length >= CLUSTER_FROM && L.markerClusterGroup;
    var layer = clustered
      ? L.markerClusterGroup({ showCoverageOnHover: false, maxClusterRadius: 46, spiderfyDistanceMultiplier: 1.6, iconCreateFunction: clusterIcon, chunkedLoading: true })
      : L.layerGroup();
    layer.addTo(map);

    var bounds = [], markers = [];
    points.forEach(function (raw) {
      var p = { id: raw.id, name: raw.name, url: raw.url, meta: raw.meta || raw.city || '', type: raw.type || '', er: !!(raw.er || raw.hasEmergency), lat: +raw.lat, lng: +raw.lng };
      if (!isFinite(p.lat) || !isFinite(p.lng) || (!p.lat && !p.lng)) return;
      var m = L.marker([p.lat, p.lng], { icon: pinIcon(p), title: p.name, alt: p.name, keyboard: true, riseOnHover: true, ehcEr: p.er });
      m.bindPopup(popup(p), { closeButton: true, autoPanPadding: [24, 24], maxWidth: 280, minWidth: 220 });
      m.on('click', function () {
        var card = document.querySelector('[data-facility="' + p.id + '"]');
        if (!card || card.offsetParent === null) return;
        card.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        card.classList.add('is-map-active');
        setTimeout(function () { card.classList.remove('is-map-active'); }, 1600);
      });
      bounds.push([p.lat, p.lng]);
      markers.push({ marker: m, id: p.id, type: p.type, er: p.er });
    });
    layer.addLayers ? layer.addLayers(markers.map(function (x) { return x.marker; })) : markers.forEach(function (x) { layer.addLayer(x.marker); });

    var single = parseInt(el.getAttribute('data-zoom') || '13', 10);
    function fitAll() {
      var vis = markers.filter(function (x) { return layer.hasLayer(x.marker); }).map(function (x) { return x.marker.getLatLng(); });
      if (vis.length > 1) map.fitBounds(vis, { padding: [40, 40], maxZoom: 13 });
      else if (vis.length === 1) map.setView(vis[0], single);
      else if (bounds.length === 1) map.setView(bounds[0], single);
      else map.setView(CENTER, 9);
    }
    fitAll();

    // "my location": a "you are here" dot (with its accuracy circle) and the map centred on it. Announced to the page
    // as map:located (detail: [lat, lng]); the facility finder sorts its list by distance from it.
    var meDot = null, meArea = null, meBtn = null;
    function locate() {
      if (!CAN_LOCATE) return;
      if (meBtn) { meBtn.classList.add('is-busy'); meBtn.setAttribute('aria-busy', 'true'); }
      navigator.geolocation.getCurrentPosition(function (pos) {
        var here = [pos.coords.latitude, pos.coords.longitude], acc = Math.min(pos.coords.accuracy || 0, 50000);
        if (meBtn) { meBtn.classList.remove('is-busy'); meBtn.removeAttribute('aria-busy'); meBtn.classList.add('is-on'); }
        if (!meDot) {
          meArea = L.circle(here, { radius: acc, className: 'ehc-me-area', interactive: false }).addTo(map);
          meDot = L.marker(here, { icon: L.divIcon({ className: 'ehc-me', html: '<span></span>', iconSize: [22, 22] }), title: T.myLocation, alt: T.myLocation, keyboard: false, zIndexOffset: 2000 }).addTo(map);
        } else { meArea.setLatLng(here).setRadius(acc); meDot.setLatLng(here); }
        // a rough fix (e.g. a desktop located by its network) shows its whole circle; a precise one zooms in
        if (acc > 1500) map.fitBounds(meArea.getBounds(), { maxZoom: 13, padding: [20, 20] });
        else map.setView(here, Math.max(map.getZoom(), 13));
        el.dispatchEvent(new CustomEvent('map:located', { detail: here }));
      }, function () {
        if (meBtn) { meBtn.classList.remove('is-busy'); meBtn.removeAttribute('aria-busy'); }
        flash(el, T.locateFailed);
      }, { enableHighAccuracy: true, timeout: 12000, maximumAge: 60000 });
    }
    el.__locate = CAN_LOCATE ? locate : null;

    // under the zoom buttons: "my location", and "show all" when there are several points
    if (CAN_LOCATE || bounds.length > 1) {
      var Tools = L.Control.extend({
        options: { position: rtl ? 'topright' : 'topleft' },
        onAdd: function () {
          var bar = L.DomUtil.create('div', 'leaflet-bar ehc-map-fit');
          function button(label, icon, run) {
            var b = L.DomUtil.create('a', '', bar);
            b.href = '#'; b.title = label; b.setAttribute('role', 'button'); b.setAttribute('aria-label', label);
            b.appendChild(svgIcon(icon, 'ico !h-[18px] !w-[18px]'));
            L.DomEvent.on(b, 'click', function (e) { L.DomEvent.preventDefault(e); run(); });
            return b;
          }
          if (CAN_LOCATE) meBtn = button(T.myLocation, 'locate', locate);
          if (bounds.length > 1) button(T.showAll, 'target', fitAll);
          L.DomEvent.disableClickPropagation(bar);
          return bar;
        }
      });
      map.addControl(new Tools());
    }

    // highlight a pin (or the cluster holding it) while its list card is hovered / focused
    var lit = null;
    function light(id) {
      if (lit) { L.DomUtil.removeClass(lit, 'is-active'); lit = null; }
      var x = id && markers.filter(function (k) { return k.id === id; })[0];
      if (!x || !layer.hasLayer(x.marker)) return;
      var shown = layer.getVisibleParent ? layer.getVisibleParent(x.marker) : x.marker;
      var node = shown && shown.getElement && shown.getElement();
      if (node) { L.DomUtil.addClass(node, 'is-active'); lit = node; }
    }
    [].forEach.call(document.querySelectorAll('[data-facility]'), function (card) {
      var id = card.getAttribute('data-facility');
      card.addEventListener('mouseenter', function () { light(id); });
      card.addEventListener('focus', function () { light(id); });
      card.addEventListener('mouseleave', function () { light(null); });
      card.addEventListener('blur', function () { light(null); });
    });

    // filter from outside: el.dispatchEvent(new CustomEvent('map:filter', { detail: 'hospital' | 'emergency' | 'all' | [ids] }))
    el.addEventListener('map:filter', function (e) {
      var f = e.detail || 'all';
      var keep = markers.filter(function (x) {
        if (Array.isArray(f)) return f.indexOf(String(x.id)) >= 0;
        return f === 'all' || (f === 'emergency' ? x.er : x.type === f);
      }).map(function (x) { return x.marker; });
      layer.clearLayers();
      if (layer.addLayers) layer.addLayers(keep); else keep.forEach(function (m) { layer.addLayer(m); });
    });
    el.__markers = markers;
    return map;
  }

  // a facility finder already lists every facility with its coordinates: use those, no request needed
  function listPoints(el) {
    var root = el.closest('[data-finder]');
    if (!root) return [];
    return [].slice.call(root.querySelectorAll('[data-finder-list] > [data-lat]')).map(function (row) {
      var card = row.querySelector('[data-facility]'), name = card && card.querySelector('b'), meta = card && card.querySelector('b + span');
      return {
        id: card ? card.getAttribute('data-facility') : '', name: name ? name.textContent.trim() : '', url: card ? card.getAttribute('href') : '',
        meta: meta ? meta.textContent.replace(/\s+/g, ' ').trim() : '',
        lat: row.getAttribute('data-lat'), lng: row.getAttribute('data-lng'), type: row.getAttribute('data-type') || '', er: row.getAttribute('data-emergency') === '1'
      };
    }).filter(function (p) { return p.name && p.lat && p.lng; });
  }
  function points(el) {
    var listed = listPoints(el);
    if (listed.length) return Promise.resolve(listed);
    var src = el.getAttribute('data-src');
    if (src) {
      return fetch(src, { headers: { 'Accept': 'application/json' } })
        .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
        .catch(function () { return []; });
    }
    try { return Promise.resolve(JSON.parse(el.getAttribute('data-points') || '[]')); } catch (e) { return Promise.resolve([]); }
  }

  // Leaflet (and the cluster plugin, when a map needs it) is only downloaded when a map is about to be seen
  var loaded = {};
  function script(url) {
    if (loaded[url]) return loaded[url];
    return (loaded[url] = new Promise(function (resolve, reject) { var s = document.createElement('script'); s.src = url; s.onload = function () { resolve(); }; s.onerror = reject; document.head.appendChild(s); }));
  }
  function css(url) {
    if (document.querySelector('link[href="' + url + '"]')) return;
    var l = document.createElement('link'); l.rel = 'stylesheet'; l.href = url; document.head.appendChild(l);
  }
  function leaflet() {
    css(attr('data-leaflet-css', '/assets/vendor/leaflet/leaflet.css'));
    return (window.L ? Promise.resolve() : script(attr('data-leaflet-js', '/assets/vendor/leaflet/leaflet.js'))).then(function () {
      // the basemap renderer needs Leaflet first; a failure only costs the background, not the pins
      if (attr('data-tiles', '') && !window.protomapsL) return script(attr('data-protomaps-js', '/assets/vendor/protomaps-leaflet/protomaps-leaflet.js')).catch(function () { });
    });
  }
  function cluster() {
    if (window.L && window.L.markerClusterGroup) return Promise.resolve();
    css(attr('data-cluster-css', '/assets/vendor/leaflet.markercluster/MarkerCluster.css'));
    return script(attr('data-cluster-js', '/assets/vendor/leaflet.markercluster/leaflet.markercluster.js')).catch(function () { /* plain pins */ });
  }

  function start(el) {
    if (el.hasAttribute('data-map-ready')) return;
    el.setAttribute('data-map-ready', '');
    Promise.all([leaflet(), points(el)]).then(function (r) {
      var items = r[1] || [];
      return (items.length >= CLUSTER_FROM ? cluster() : Promise.resolve()).then(function () {
        el.__map = draw(el, items);
        el.dispatchEvent(new CustomEvent('map:ready', { detail: items }));
      });
    }).catch(function () { /* list stays usable without the map */ });
  }
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
