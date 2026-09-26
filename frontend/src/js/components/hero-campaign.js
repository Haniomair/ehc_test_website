/* Campaign hero ([data-hero]): tabbed slides with auto-rotation.
   - Tabs follow the WAI-ARIA tabs pattern (click, Arrow keys, Home/End).
   - Auto-rotation has a visible pause button (WCAG 2.2.2), pauses on hover/focus, and is off under prefers-reduced-motion.
   Safe to load more than once and with any number of heroes on the page. */
(function () {
  'use strict';
  var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  function init(root) {
    if (root.hasAttribute('data-hero-ready')) return;
    root.setAttribute('data-hero-ready', '');
    var slides = [].slice.call(root.querySelectorAll('.slide'));
    var tabs = [].slice.call(root.querySelectorAll('[role="tab"]'));
    var pauseBtn = root.querySelector('[data-hero-pause]');
    if (slides.length < 2 || tabs.length !== slides.length) return;

    var interval = parseInt(root.getAttribute('data-interval') || '7000', 10);
    var cur = 0, timer = null, paused = reduce, hovering = false;
    root.style.setProperty('--hero-interval', interval + 'ms');

    function setBg(cls) {
      root.classList.remove('hero-bg-0', 'hero-bg-1', 'hero-bg-2');
      if (/^hero-bg-[012]$/.test(cls)) root.classList.add(cls);
    }
    function schedule() {
      clearTimeout(timer);
      if (!paused && !hovering) timer = setTimeout(function () { go((cur + 1) % slides.length, false); }, interval);
    }
    function go(i, focus) {
      cur = i;
      slides.forEach(function (s, j) { s.classList.toggle('hidden', j !== i); });
      tabs.forEach(function (t, j) {
        var on = j === i;
        t.setAttribute('aria-selected', on ? 'true' : 'false');
        t.setAttribute('tabindex', on ? '0' : '-1');
        t.classList.remove('prog-on');
        void t.offsetWidth; // restart the progress animation
        if (on && !paused) t.classList.add('prog-on');
        if (on && focus) t.focus();
      });
      setBg(slides[i].getAttribute('data-bg') || '');
      schedule();
    }
    function setPaused(p) {
      paused = p;
      if (pauseBtn) {
        pauseBtn.setAttribute('aria-pressed', p ? 'true' : 'false');
        var pi = pauseBtn.querySelector('[data-icon="pause"]'), pl = pauseBtn.querySelector('[data-icon="play"]');
        if (pi) pi.classList.toggle('hidden', p);
        if (pl) pl.classList.toggle('hidden', !p);
      }
      go(cur, false);
    }

    tabs.forEach(function (t, j) {
      t.addEventListener('click', function () { go(j, false); });
      t.addEventListener('keydown', function (e) {
        var rtl = document.documentElement.dir === 'rtl', n = tabs.length, k = e.key, next = null;
        if (k === 'ArrowRight') next = (j + (rtl ? -1 : 1) + n) % n;
        else if (k === 'ArrowLeft') next = (j + (rtl ? 1 : -1) + n) % n;
        else if (k === 'Home') next = 0;
        else if (k === 'End') next = n - 1;
        if (next !== null) { e.preventDefault(); go(next, true); }
      });
    });
    if (pauseBtn) pauseBtn.addEventListener('click', function () { setPaused(!paused); });
    root.addEventListener('mouseenter', function () { hovering = true; clearTimeout(timer); });
    root.addEventListener('mouseleave', function () { hovering = false; schedule(); });
    root.addEventListener('focusin', function () { hovering = true; clearTimeout(timer); });
    root.addEventListener('focusout', function (e) { if (!root.contains(e.relatedTarget)) { hovering = false; schedule(); } });

    setPaused(paused);
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-hero]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
