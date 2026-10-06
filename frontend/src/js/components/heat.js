/* Click and scroll heatmaps: only for visitors who accepted optional cookies (html[data-consent="all"] or the
   "ehc:consent" event); nothing is recorded before that. Per page view: up to 50 clicks, each as the clicked element's
   structural path (stable ids only) and where inside it (percent), plus the deepest point of the page that was on
   screen. Never anything typed, no page text, no visitor id. Sent with navigator.sendBeacon to /api/stats/heat when
   the page is hidden. Not active inside frames (the backoffice draws the heatmap over the page in one).
   data-sample (0–1) on the script tag records only that share of page views. */
(function () {
  'use strict';
  var me = document.currentScript;
  if (!me || !navigator.sendBeacon || navigator.webdriver || window.self !== window.top) return;
  var page = me.getAttribute('data-page'), culture = me.getAttribute('data-culture');
  var api = me.getAttribute('data-api') || '/api/stats/heat';
  var rate = parseFloat(me.getAttribute('data-sample') || '1');
  if (!page) return;
  var html = document.documentElement, started = false;
  var MAX = 50, TARGETS = 'a,button,input,select,textarea,summary,label,[role="button"],[data-action],img,video,iframe';
  var ID = /^[A-Za-z][A-Za-z0-9_-]{0,80}$/;
  var clicks, depth, depthSent, count;

  function device() {
    var w = window.innerWidth;
    return w < 768 ? 'mobile' : w < 1024 ? 'tablet' : 'desktop';
  }

  /** "#stable-id>tag:nth-of-type(n)>…" or "body>…": the same path the backoffice resolves when drawing the heatmap. */
  function path(el) {
    var parts = [];
    for (var node = el; node && node.nodeType === 1 && parts.length < 12; node = node.parentElement) {
      if (node === document.body) return ['body'].concat(parts).join('>');
      if (node.id && ID.test(node.id)) return ['#' + node.id].concat(parts).join('>');
      var tag = node.localName, i = 1, sib = node;
      if (!/^[a-z][a-z0-9-]{0,30}$/.test(tag)) return null;
      while ((sib = sib.previousElementSibling)) if (sib.localName === tag) i++;
      parts.unshift(tag + ':nth-of-type(' + i + ')');
    }
    return null;
  }

  function onClick(e) {
    if (!e.isTrusted || count >= MAX || !(e.target instanceof Element)) return;
    var el = e.target.closest(TARGETS) || e.target;
    var s = path(el);
    if (!s) return;
    var r = el.getBoundingClientRect();
    if (!r.width || !r.height) return;
    var clamp = function (v) { return Math.max(0, Math.min(100, Math.round(v))); };
    clicks.push({ s: s, x: clamp(((e.clientX - r.left) / r.width) * 100), y: clamp(((e.clientY - r.top) / r.height) * 100) });
    count++;
  }

  var ticking = false;
  function measure() {
    ticking = false;
    var h = document.documentElement.scrollHeight;
    if (h > 0) depth = Math.max(depth, Math.min(100, Math.round(((window.scrollY + window.innerHeight) / h) * 100)));
  }
  function onScroll() {
    if (!ticking) { ticking = true; requestAnimationFrame(measure); }
  }

  function flush() {
    if (depthSent && !clicks.length) return;
    var body = { pageKey: page, culture: culture, device: device(), depth: depthSent ? null : depth, clicks: clicks };
    clicks = [];
    depthSent = true;
    try { navigator.sendBeacon(api, new Blob([JSON.stringify(body)], { type: 'application/json' })); } catch (e) { /* not essential */ }
  }

  function reset() {
    clicks = [];
    depth = 0;
    depthSent = false;
    count = 0;
    measure();
  }

  function start() {
    if (started) return;
    started = true;
    if (!(Math.random() < rate)) return;
    reset();
    document.addEventListener('click', onClick, true);
    window.addEventListener('scroll', onScroll, { passive: true });
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') flush(); });
    window.addEventListener('pagehide', flush);
    // back/forward cache: a restored page is a new page view
    window.addEventListener('pageshow', function (e) { if (e.persisted) reset(); });
  }

  if (html.getAttribute('data-consent') === 'all') start();
  document.addEventListener('ehc:consent', function (e) { if (e.detail && e.detail.choice === 'all') start(); });
})();
