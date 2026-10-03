/* Real-visitor Core Web Vitals (LCP, INP, CLS) with Google's web-vitals library, self-hosted (vendor/web-vitals).
   Only for visitors who accepted optional cookies (html[data-consent="all"] or the "ehc:consent" event) — nothing is
   loaded or sent before that. Sends the page key, language, device class and the three values once the page is hidden
   (navigator.sendBeacon to /api/vitals): no cookies, no ids, no address or user-agent string.
   data-sample (0–1) on the script tag measures only that share of page views, to keep the volume down on busy days. */
(function () {
  'use strict';
  var me = document.currentScript;
  if (!me || !navigator.sendBeacon || !window.PerformanceObserver) return;
  var page = me.getAttribute('data-page'), culture = me.getAttribute('data-culture'), lib = me.getAttribute('data-lib');
  var api = me.getAttribute('data-api') || '/api/vitals';
  var rate = parseFloat(me.getAttribute('data-sample') || '1');
  if (!page || !lib) return;
  var html = document.documentElement, started = false;

  function device() {
    var ua = navigator.userAgentData;
    if (ua && typeof ua.mobile === 'boolean') return ua.mobile ? 'mobile' : 'desktop';
    return window.matchMedia && window.matchMedia('(max-width: 767px)').matches ? 'mobile' : 'desktop';
  }

  function collect() {
    var wv = window.webVitals;
    if (!wv) return;
    var latest = {}, sent = {};
    function keep(m) { latest[m.name] = m.value; }
    wv.onLCP(keep); wv.onINP(keep); wv.onCLS(keep);
    // registered after web-vitals' own listeners, so its final values are in by the time this runs
    function flush() {
      var metrics = {}, any = false;
      ['LCP', 'INP', 'CLS'].forEach(function (k) {
        // each metric once per page view (its final value at the first hide), so a visit is never counted twice
        if (latest[k] != null && !sent[k]) { metrics[k] = latest[k]; sent[k] = true; any = true; }
      });
      if (!any) return;
      var body = JSON.stringify({ pageKey: page, culture: culture, device: device(), metrics: metrics });
      try { navigator.sendBeacon(api, new Blob([body], { type: 'application/json' })); } catch (e) { /* not essential */ }
    }
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') flush(); });
    window.addEventListener('pagehide', flush);
  }

  function start() {
    if (started) return;
    started = true;
    if (!(Math.random() < rate)) return;
    var s = document.createElement('script');
    s.src = lib;
    s.onload = collect;
    document.head.appendChild(s);
  }

  if (html.getAttribute('data-consent') === 'all') start();
  document.addEventListener('ehc:consent', function (e) { if (e.detail && e.detail.choice === 'all') start(); });
})();
