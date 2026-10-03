/* Campaign hero ([data-hero]): tabbed slides with auto-rotation.
   - Tabs follow the WAI-ARIA tabs pattern (click, Arrow keys, Home/End).
   - Auto-rotation has a visible pause button (WCAG 2.2.2), waits while hovered (unless data-hover-pause="false") or
     keyboard-focused, and is off under
     prefers-reduced-motion. The active tab's progress bar is the clock: the next slide comes when its animation ends,
     and holding pauses the bar itself (.is-held), so bar and slide can never disagree.
   - Slides are stacked (.hero-slides); inactive ones get .slide-off, keeping the hero at the tallest slide's height.
   - Slide changes animate the leaving and arriving slide together (data-transition: fade / slide / rise / zoom / blur).
   - Each slide has its own gradient layer ([data-slide-bg]), right panel ([data-slide-panel], may be shared) and can
     show or hide the spinning mark ([data-hero-mark]).
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

    // a layer per slide ([data-slide-bg="2"]) or shared by several ([data-slide-panel="0 2"]): show those listing i
    function showFor(selector, attr, i) {
      [].forEach.call(root.querySelectorAll(selector), function (el) {
        el.classList.toggle('slide-off', (' ' + el.getAttribute(attr) + ' ').indexOf(' ' + i + ' ') < 0);
      });
    }
    function hold() { root.classList.toggle('is-held', pointer || keyboard); }
    // leaving / arriving animations (CSS: .hero-out / .hero-in, chosen by data-transition). Removed when they end, or by a
    // timer when animations are switched off ("pause animations" can be turned on at any time).
    var cleanup = {};
    function animate(el, cls) {
      el.classList.add(cls);
      clearTimeout(cleanup[cls + slides.indexOf(el)]);
      cleanup[cls + slides.indexOf(el)] = setTimeout(function () { el.classList.remove(cls); }, 1300);
    }
    function go(i, focus, dir) {
      var changed = i !== cur, prev = cur;
      cur = i;
      if (changed) root.setAttribute('data-dir', dir || (i > prev ? 'next' : 'prev'));
      slides.forEach(function (s, j) {
        s.classList.remove('hero-in', 'hero-out');
        s.classList.toggle('slide-off', j !== i);
      });
      if (changed && !reduce) { void slides[i].offsetWidth; animate(slides[prev], 'hero-out'); animate(slides[i], 'hero-in'); }
      tabs.forEach(function (t, j) {
        var on = j === i;
        t.setAttribute('aria-selected', on ? 'true' : 'false');
        t.setAttribute('tabindex', on ? '0' : '-1');
        t.classList.remove('prog-on');
        void t.offsetWidth; // restart the progress animation
        if (on && !paused) t.classList.add('prog-on');
        if (on && focus) t.focus();
      });
      // this slide's gradient, its right panel (none listing it = empty column) and whether the spinning mark shows
      showFor('[data-slide-bg]', 'data-slide-bg', i);
      showFor('[data-slide-panel]', 'data-slide-panel', i);
      // .is-shown starts the panel's entrance (floating cards "Rise in" / "Slide in") when it comes into view; a panel
      // that stays (shared by consecutive slides) keeps it and does not replay
      [].forEach.call(root.querySelectorAll('[data-slide-panel]'), function (p) {
        p.classList.toggle('is-shown', !p.classList.contains('slide-off'));
      });
      var mark = root.querySelector('[data-hero-mark]');
      if (mark) mark.classList.toggle('slide-off', slides[i].getAttribute('data-mark') === 'off');
      if (root.classList.contains('media-hero')) {
        // media per slide: cross-fade to this slide's layer (only its video plays) and apply its overlay / strength /
        // focal point (validated server-side)
        showFor('[data-slide-media]', 'data-slide-media', i);
        [].forEach.call(root.querySelectorAll('[data-slide-media] video'), function (v) {
          if (v.closest('[data-slide-media]').classList.contains('slide-off')) v.pause();
          else if (!reduce) { var pr = v.play(); if (pr && pr.catch) pr.catch(function () {}); }
        });
        var s = slides[i], ov = s.getAttribute('data-overlay'), st = s.getAttribute('data-strength'), fo = s.getAttribute('data-focal');
        if (/^(tint|duotone|shade|poster)$/.test(ov || '')) root.setAttribute('data-overlay', ov); else root.setAttribute('data-overlay', 'shade');
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
      // the leaving slide is hidden as soon as its animation is done
      if (/^hero-.*-out$/.test(e.animationName) && slides.indexOf(e.target) >= 0) { e.target.classList.remove('hero-out'); return; }
      if (paused || e.animationName !== 'ehc-prog' || !tabs[cur].contains(e.target)) return;
      go((cur + 1) % slides.length, false, 'next');
    });
    // mouse only: a touch tap fires enter with no leave, which would stop rotation for good. data-hover-pause="false"
    // (hero setting "Keep rotating on mouse hover") turns this off; keyboard focus and the pause button still stop it.
    var hoverPause = root.getAttribute('data-hover-pause') !== 'false';
    root.addEventListener('pointerenter', function (e) { if (hoverPause && e.pointerType === 'mouse') { pointer = true; hold(); } });
    root.addEventListener('pointerleave', function (e) { if (e.pointerType === 'mouse') { pointer = false; hold(); } });
    // only keyboard focus holds: after a mouse click focus stays on the tab and would stop rotation for good
    root.addEventListener('focusin', function (e) { keyboard = !!(e.target.matches && e.target.matches(':focus-visible')); hold(); });
    root.addEventListener('focusout', function (e) { if (!root.contains(e.relatedTarget)) { keyboard = false; hold(); } });

    setPaused(paused);
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-hero]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
