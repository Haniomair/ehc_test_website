/* Countdown: [data-countdown="ISO date"] updates its [data-unit] numbers every second; when the time has passed the
   units hide and [data-countdown-done] shows (or the section is hidden when there is no finished text). */
(function () {
  'use strict';
  function pad(n) { return n < 10 ? '0' + n : String(n); }
  function init(box) {
    if (box.hasAttribute('data-countdown-ready')) return;
    box.setAttribute('data-countdown-ready', '');
    var target = Date.parse(box.getAttribute('data-countdown'));
    if (isNaN(target)) return;
    var units = box.querySelector('[data-countdown-units]'), done = box.querySelector('[data-countdown-done]');
    var el = {};
    [].forEach.call(box.querySelectorAll('[data-unit]'), function (u) { el[u.getAttribute('data-unit')] = u; });
    var timer;
    function tick() {
      var left = Math.max(0, Math.floor((target - Date.now()) / 1000));
      if (el.d) el.d.textContent = pad(Math.floor(left / 86400));
      if (el.h) el.h.textContent = pad(Math.floor(left % 86400 / 3600));
      if (el.m) el.m.textContent = pad(Math.floor(left % 3600 / 60));
      if (el.s) el.s.textContent = pad(left % 60);
      if (left === 0) {
        clearInterval(timer);
        if (done) { if (units) units.classList.add('hidden'); done.classList.remove('hidden'); }
        else { (box.closest('section') || box).hidden = true; }
      }
    }
    tick();
    timer = setInterval(tick, 1000);
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-countdown]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
