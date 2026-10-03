/* Saudi Model of Care ([data-care]): moves the choice to the next system of care by itself. tabs.js does the choosing
   (click, arrow keys); this script only adds the timer. The timer is the fill animation of the chosen system's arc
   (.care-progress, length data-interval): when it ends, the next system is chosen. Pausing the animation pauses the
   timer: while the mouse is over the section (unless data-hover-pause="false"), a control inside it has keyboard focus, it is off screen, the tab is hidden,
   or the pause button is pressed. Never runs with prefers-reduced-motion. */
(function () {
  'use strict';
  var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  function init(root) {
    if (root.hasAttribute('data-care-ready')) return;
    root.setAttribute('data-care-ready', '');
    var ms = parseInt(root.getAttribute('data-interval') || '0', 10);
    var tabs = [].slice.call(root.querySelectorAll('[data-care-tab]'));
    var bars = [].slice.call(root.querySelectorAll('[data-care-progress]'));
    var btn = root.querySelector('[data-care-pause]');
    if (reduce || !ms || tabs.length < 2) return;
    root.style.setProperty('--care-ms', ms + 'ms');
    root.classList.add('care-auto');
    if (btn) btn.classList.replace('hidden', 'grid');

    var stopped = false, hover = false, focus = false, seen = true;
    function current() { for (var i = 0; i < tabs.length; i++) if (tabs[i].getAttribute('aria-selected') === 'true') return i; return 0; }
    function update() { root.classList.toggle('care-paused', stopped || hover || focus || !seen || document.hidden); }
    // (re)start the fill of the chosen system's arc
    function start() {
      var i = current();
      bars.forEach(function (b, j) {
        b.classList.remove('is-on');
        if (j === i) { void b.getBoundingClientRect(); b.classList.add('is-on'); }
      });
    }
    bars.forEach(function (b) {
      b.addEventListener('animationend', function () {
        if (!b.classList.contains('is-on')) return;
        tabs[(current() + 1) % tabs.length].click();   // tabs.js shows it, then start() runs from the click below
      });
    });
    tabs.forEach(function (t) { t.addEventListener('click', function () { setTimeout(start, 0); }); });

    // the mouse over the section pauses, unless the editor chose to keep changing (data-hover-pause="false")
    if (root.getAttribute('data-hover-pause') !== 'false') {
      root.addEventListener('pointerenter', function (e) { if (e.pointerType === 'mouse') { hover = true; update(); } });
      root.addEventListener('pointerleave', function () { hover = false; update(); });
    }
    // keyboard focus inside pauses; a mouse click on a system does not (the timer just restarts)
    root.addEventListener('focusin', function (e) { if (e.target.matches(':focus-visible')) { focus = true; update(); } });
    root.addEventListener('focusout', function (e) { if (!root.contains(e.relatedTarget)) { focus = false; update(); } });
    document.addEventListener('visibilitychange', update);
    if ('IntersectionObserver' in window) {
      new IntersectionObserver(function (es) { seen = es[0].isIntersecting; update(); }, { threshold: .35 }).observe(root);
    }
    if (btn) btn.addEventListener('click', function () {
      stopped = !stopped;
      btn.setAttribute('aria-pressed', stopped ? 'true' : 'false');
      btn.querySelector('[data-icon="pause"]').classList.toggle('hidden', stopped);
      btn.querySelector('[data-icon="play"]').classList.toggle('hidden', !stopped);
      update();
    });
    update();
    start();
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-care]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
