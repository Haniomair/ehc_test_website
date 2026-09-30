/* Campaign hero ([data-hero]): tabbed slides with auto-rotation.
   - Tabs follow the WAI-ARIA tabs pattern (click, Arrow keys, Home/End).
   - Auto-rotation has a visible pause button (WCAG 2.2.2), waits while hovered or keyboard-focused, and is off under
     prefers-reduced-motion. The active tab's progress bar is the clock: the next slide comes when its animation ends,
     and holding pauses the bar itself (.is-held), so bar and slide can never disagree.
   - Slides are stacked (.hero-slides); inactive ones get .slide-off, keeping the hero at the tallest slide's height.
   Safe to load more than once and with any number of heroes on the page. */
(function () {
  'use strict';
  var reduce = document.documentElement.classList.contains('a11y-still') || (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

  function init(root) {
    if (root.hasAttribute('data-hero-ready')) return;
    root.setAttribute('data-hero-ready', '');
    var slides = [].slice.call(root.querySelectorAll('.slide'));
    var tabs = [].slice.call(root.querySelectorAll('[role="tab"]'));
    var pauseBtn = root.querySelector('[data-hero-pause]');
    if (slides.length < 2 || tabs.length !== slides.length) return;

    var interval = parseInt(root.getAttribute('data-interval') || '7000', 10);
    var cur = 0, paused = reduce || root.getAttribute('data-autoplay') === 'false', pointer = false, keyboard = false;
    root.style.setProperty('--hero-interval', interval + 'ms');

    function setBg(cls) {
      if (root.classList.contains('media-hero')) return;
      root.classList.remove('hero-bg-0', 'hero-bg-1', 'hero-bg-2');
      if (/^hero-bg-[012]$/.test(cls)) root.classList.add(cls);
    }
    function hold() { root.classList.toggle('is-held', pointer || keyboard); }
    function go(i, focus) {
      var changed = i !== cur;
      cur = i;
      slides.forEach(function (s, j) {
        s.classList.toggle('slide-off', j !== i);
        if (j === i && changed) { s.classList.remove('anim-fadeup'); void s.offsetWidth; s.classList.add('anim-fadeup'); }   // replay the entrance
      });
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
      if (root.classList.contains('media-hero')) {
        // media per slide: show this slide's layer and apply its overlay / strength / focal point (validated server-side)
        [].forEach.call(root.querySelectorAll('[data-slide-media]'), function (m) {
          var on = m.getAttribute('data-slide-media') === String(i);
          m.classList.toggle('contents', on);
          m.classList.toggle('hidden', !on);
        });
        var s = slides[i], ov = s.getAttribute('data-overlay'), st = s.getAttribute('data-strength'), fo = s.getAttribute('data-focal');
        if (/^(tint|duotone|shade)$/.test(ov || '')) root.setAttribute('data-overlay', ov); else root.setAttribute('data-overlay', 'shade');
        if (/^(light|medium|strong)$/.test(st || '')) root.setAttribute('data-strength', st);
        if (/^[0-9.]+% [0-9.]+%$/.test(fo || '')) root.style.setProperty('--focal', fo);
      }
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
    // the active bar finished filling: next slide
    root.addEventListener('animationend', function (e) {
      if (paused || e.animationName !== 'ehc-prog' || !tabs[cur].contains(e.target)) return;
      go((cur + 1) % slides.length, false);
    });
    // mouse only: a touch tap fires enter with no leave, which would stop rotation for good
    root.addEventListener('pointerenter', function (e) { if (e.pointerType === 'mouse') { pointer = true; hold(); } });
    root.addEventListener('pointerleave', function (e) { if (e.pointerType === 'mouse') { pointer = false; hold(); } });
    // only keyboard focus holds: after a mouse click focus stays on the tab and would stop rotation for good
    root.addEventListener('focusin', function (e) { keyboard = !!(e.target.matches && e.target.matches(':focus-visible')); hold(); });
    root.addEventListener('focusout', function (e) { if (!root.contains(e.relatedTarget)) { keyboard = false; hold(); } });

    setPaused(paused);
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-hero]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
