/* Scrolling logo strips (.logo-strip): the pause button ([data-strip-pause], aria-controls = the strip) stops and
   restarts the movement, which also pauses on hover and keyboard focus (CSS). Hidden under prefers-reduced-motion,
   where the strip does not move. */
(function () {
  'use strict';
  function init(btn) {
    if (btn.hasAttribute('data-ready')) return;
    btn.setAttribute('data-ready', '');
    var strip = document.getElementById(btn.getAttribute('aria-controls'));
    if (!strip) return;
    btn.addEventListener('click', function () {
      var paused = btn.getAttribute('aria-pressed') !== 'true';
      btn.setAttribute('aria-pressed', paused ? 'true' : 'false');
      strip.classList.toggle('is-paused', paused);
      var pause = btn.querySelector('[data-icon="pause"]'), play = btn.querySelector('[data-icon="play"]');
      if (pause) pause.classList.toggle('hidden', paused);
      if (play) play.classList.toggle('hidden', !paused);
    });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-strip-pause]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
