/* Horizontal rails: buttons with data-rail-prev / data-rail-next="{railId}" scroll the rail by one card (RTL-aware).
   Optional page dots: an empty element with data-rail-dots="{railId}" gets one button per page of cards;
   the current page is marked with aria-current and follows scrolling. */
(function () {
  'use strict';
  var reduce = document.documentElement.classList.contains('a11y-still') || (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  function cardWidth(rail) {
    var card = rail.firstElementChild;
    return card ? card.getBoundingClientRect().width + parseFloat(getComputedStyle(rail).columnGap || '20') : 320;
  }
  function rtl(rail) { return getComputedStyle(rail).direction === 'rtl'; }
  function step(btn, dir) {
    var rail = document.getElementById(btn.getAttribute(dir > 0 ? 'data-rail-next' : 'data-rail-prev'));
    if (!rail) return;
    rail.scrollBy({ left: dir * (rtl(rail) ? -1 : 1) * cardWidth(rail), behavior: reduce ? 'auto' : 'smooth' });
  }
  function dots(box) {
    var rail = document.getElementById(box.getAttribute('data-rail-dots'));
    if (!rail) return;
    var label = box.getAttribute('data-label') || '{0} / {1}';
    // a partly filled last page still gets its own dot; at the very end the last dot is current
    function pages() { return Math.max(1, Math.ceil((rail.scrollWidth - 4) / rail.clientWidth)); }
    function current() {
      var pos = Math.abs(rail.scrollLeft), end = rail.scrollWidth - rail.clientWidth;
      return pos >= end - 4 ? pages() - 1 : Math.min(pages() - 1, Math.round(pos / rail.clientWidth));
    }
    function build() {
      var n = pages();
      box.textContent = '';
      box.hidden = n < 2;
      for (var i = 0; i < n; i++) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'rail-dot';
        b.setAttribute('aria-label', label.replace('{0}', i + 1).replace('{1}', n));
        (function (page) {
          b.addEventListener('click', function () {
            var left = Math.min(page * rail.clientWidth, rail.scrollWidth - rail.clientWidth);
            rail.scrollTo({ left: (rtl(rail) ? -1 : 1) * left, behavior: reduce ? 'auto' : 'smooth' });
          });
        })(i);
        box.appendChild(b);
      }
      mark();
    }
    function mark() {
      var c = current();
      [].forEach.call(box.children, function (b, i) { if (i === c) b.setAttribute('aria-current', 'true'); else b.removeAttribute('aria-current'); });
    }
    var t;
    rail.addEventListener('scroll', function () { clearTimeout(t); t = setTimeout(mark, 80); }, { passive: true });
    window.addEventListener('resize', function () { clearTimeout(t); t = setTimeout(build, 150); });
    build();
  }
  function all() {
    [].forEach.call(document.querySelectorAll('[data-rail-prev]:not([data-rail-ready])'), function (b) { b.setAttribute('data-rail-ready', ''); b.addEventListener('click', function () { step(b, -1); }); });
    [].forEach.call(document.querySelectorAll('[data-rail-next]:not([data-rail-ready])'), function (b) { b.setAttribute('data-rail-ready', ''); b.addEventListener('click', function () { step(b, 1); }); });
    [].forEach.call(document.querySelectorAll('[data-rail-dots]:not([data-rail-ready])'), function (d) { d.setAttribute('data-rail-ready', ''); dots(d); });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
