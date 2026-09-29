/* Background videos ([data-hero-video]): play muted only when motion is allowed; the section's [data-video-toggle]
   button pauses/plays (WCAG 2.2.2). The button's accessible name comes from data-label-pause / data-label-play. */
(function () {
  'use strict';
  var reduce = document.documentElement.classList.contains('a11y-still') || (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  function init(v) {
    if (v.hasAttribute('data-video-ready')) return;
    v.setAttribute('data-video-ready', '');
    var root = v.parentElement, btn = root && root.querySelector('[data-video-toggle]');
    function sync() {
      if (!btn) return;
      var playing = !v.paused;
      var p = btn.querySelector('[data-icon-pause]'), l = btn.querySelector('[data-icon-play]');
      if (p) p.classList.toggle('hidden', !playing);
      if (l) l.classList.toggle('hidden', playing);
      btn.setAttribute('aria-pressed', playing ? 'false' : 'true');
      var label = btn.getAttribute(playing ? 'data-label-pause' : 'data-label-play');
      if (label) btn.setAttribute('aria-label', label);
    }
    v.muted = true;
    v.addEventListener('play', sync);
    v.addEventListener('pause', sync);
    if (btn) btn.addEventListener('click', function () { if (v.paused) { var r = v.play(); if (r && r.catch) r.catch(function () {}); } else v.pause(); });
    if (!reduce) { var p = v.play(); if (p && p.catch) p.catch(function () { sync(); }); }
    sync();
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-hero-video]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
