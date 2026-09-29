/* Page contents bar ([data-page-nav]): lists the h2 of every section after it, links to the section (giving it an id
   when it has none), and marks the section in view with aria-current. Stays hidden with fewer than two entries. */
(function () {
  'use strict';
  function slug(text, used) {
    var base = 'sec-' + (text || 'x').trim().toLowerCase().replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-+|-+$/g, '').slice(0, 40), id = base, n = 2;
    while (used[id] || document.getElementById(id)) id = base + '-' + n++;
    used[id] = true;
    return id;
  }
  function init(nav) {
    if (nav.hasAttribute('data-page-nav-ready')) return;
    nav.setAttribute('data-page-nav-ready', '');
    var list = nav.querySelector('[data-page-nav-list]'), own = nav.closest('section[data-block]');
    if (!list || !own) return;
    var header = document.getElementById('hdr');
    if (header) own.style.setProperty('--header-h', header.offsetHeight + 'px');
    // sections after this one; on the component library each section sits in its own tile
    var pool = own.closest('[data-library-stage]')
      ? [].slice.call(document.querySelectorAll('[data-library-stage] > section[data-block]'))
      : [].slice.call(own.parentElement.children);
    var entries = [], used = {};
    pool.slice(pool.indexOf(own) + 1).forEach(function (s) {
      if (!s.matches('section[data-block]')) return;
      var h = s.querySelector('h2');
      if (!h || !h.textContent.trim()) return;
      if (!s.id) s.id = slug(h.textContent, used);
      entries.push({ section: s, text: h.textContent.trim() });
    });
    if (entries.length < 2) return;
    entries.forEach(function (e) {
      var li = document.createElement('li'), a = document.createElement('a');
      a.href = '#' + e.section.id;
      a.textContent = e.text;
      a.className = 'page-nav-link';
      li.appendChild(a);
      list.appendChild(li);
      e.link = a;
    });
    nav.hidden = false;
    if (nav.hasAttribute('data-sticky')) own.classList.add('page-nav-host');
    var offset = function () { return (header ? header.offsetHeight : 0) + nav.offsetHeight + 8; };
    entries.forEach(function (e) { e.section.style.scrollMarginTop = offset() + 'px'; });
    if (!('IntersectionObserver' in window)) return;
    var visible = {};
    var io = new IntersectionObserver(function (es) {
      es.forEach(function (x) { visible[x.target.id] = x.isIntersecting; });
      var current = entries.filter(function (e) { return visible[e.section.id]; })[0];
      entries.forEach(function (e) {
        if (current && e === current) {
          e.link.setAttribute('aria-current', 'true');
          var r = e.link.getBoundingClientRect(), lr = list.getBoundingClientRect();
          if (r.left < lr.left || r.right > lr.right) list.scrollBy({ left: r.left - lr.left - 24, behavior: 'auto' });
        } else e.link.removeAttribute('aria-current');
      });
    }, { rootMargin: '-' + offset() + 'px 0px -55% 0px' });
    entries.forEach(function (e) { io.observe(e.section); });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-page-nav]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
