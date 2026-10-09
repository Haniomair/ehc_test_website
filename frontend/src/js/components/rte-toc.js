/* "On this page" beside a long rich text ([data-rte-toc], richTextBlock): marks the heading whose section is in view
   with aria-current. The links work without this script. */
(function () {
  'use strict';
  function init(nav) {
    if (nav.hasAttribute('data-rte-toc-ready')) return;
    nav.setAttribute('data-rte-toc-ready', '');
    var links = [].slice.call(nav.querySelectorAll('a[href^="#"]'));
    var heads = links.map(function (a) { return document.getElementById(decodeURIComponent(a.getAttribute('href').slice(1))); });
    if (heads.some(function (h) { return !h; })) return;
    function mark() {
      var line = (parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--header-h')) || 76) + 120, current = 0;
      heads.forEach(function (h, i) { if (h.getBoundingClientRect().top <= line) current = i; });
      links.forEach(function (a, i) { if (i === current) a.setAttribute('aria-current', 'true'); else a.removeAttribute('aria-current'); });
    }
    var queued = false;
    window.addEventListener('scroll', function () {
      if (queued) return;
      queued = true;
      requestAnimationFrame(function () { queued = false; mark(); });
    }, { passive: true });
    mark();
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-rte-toc]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
