/* Visitor statistics: one small report per page view to our own /api/stats (navigator.sendBeacon). No cookies or
   browser storage, no third party: the server derives a daily visitor hash, country and device group and stores no IP
   address. Sends the page key, language, referring site, utm_source / utm_campaign and whether the screen is a touch
   screen. data-consent="required" on the script tag: wait for consent to optional cookies (Ehc:Stats:RequireConsent).
   Prerendered pages are counted only once actually shown; pages restored from the back/forward cache count again. */
(function () {
  'use strict';
  var me = document.currentScript;
  if (!me || !navigator.sendBeacon || navigator.webdriver) return;
  var page = me.getAttribute('data-page'), culture = me.getAttribute('data-culture');
  var api = me.getAttribute('data-api') || '/api/stats';
  if (!page) return;
  var html = document.documentElement, allowed = me.getAttribute('data-consent') !== 'required';

  function param(name) {
    try { return new URLSearchParams(location.search).get(name); } catch (e) { return null; }
  }

  function send() {
    var body = JSON.stringify({
      pageKey: page,
      culture: culture,
      referrer: document.referrer || null,
      source: param('utm_source'),
      campaign: param('utm_campaign'),
      touch: navigator.maxTouchPoints > 1
    });
    try { navigator.sendBeacon(api, new Blob([body], { type: 'application/json' })); } catch (e) { /* not essential */ }
  }

  function start() {
    if (document.prerendering) document.addEventListener('prerenderingchange', send, { once: true });
    else send();
    window.addEventListener('pageshow', function (e) { if (e.persisted) send(); });
  }

  if (allowed || html.getAttribute('data-consent') === 'all') start();
  else document.addEventListener('ehc:consent', function (e) {
    if (!allowed && e.detail && e.detail.choice === 'all') { allowed = true; start(); }
  });
})();
