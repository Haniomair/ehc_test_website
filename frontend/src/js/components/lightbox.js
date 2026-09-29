/* Gallery lightbox on a native <dialog>: [data-lightbox] lists hold [data-lightbox-item] links; the dialog
   ([data-lightbox-dialog]) sits in the same section. Arrow keys / buttons move, Esc closes, focus returns to the link.
   Without JS the links simply open the full-size image. */
(function () {
  'use strict';
  function init(list) {
    if (list.hasAttribute('data-lightbox-ready')) return;
    list.setAttribute('data-lightbox-ready', '');
    var root = list.parentElement, dlg = root && root.querySelector('[data-lightbox-dialog]');
    if (!dlg || typeof dlg.showModal !== 'function') return;
    var links = [].slice.call(list.querySelectorAll('[data-lightbox-item]'));
    var img = dlg.querySelector('[data-lightbox-img]'), cap = dlg.querySelector('[data-lightbox-caption]'), count = dlg.querySelector('[data-lightbox-count]');
    var prev = dlg.querySelector('[data-lightbox-prev]'), next = dlg.querySelector('[data-lightbox-next]');
    var index = 0, opener = null;
    function show(i) {
      index = (i + links.length) % links.length;
      var a = links[index], thumb = a.querySelector('img');
      img.src = a.getAttribute('href');
      img.alt = thumb ? thumb.alt : '';
      cap.textContent = a.getAttribute('data-caption') || '';
      cap.hidden = !cap.textContent;
      count.textContent = links.length > 1 ? (index + 1) + ' / ' + links.length : '';
    }
    if (links.length < 2) { prev.hidden = true; next.hidden = true; }
    links.forEach(function (a, i) {
      a.addEventListener('click', function (e) {
        e.preventDefault();
        opener = a;
        show(i);
        dlg.showModal();
      });
    });
    prev.addEventListener('click', function () { show(index - 1); });
    next.addEventListener('click', function () { show(index + 1); });
    dlg.querySelector('[data-lightbox-close]').addEventListener('click', function () { dlg.close(); });
    dlg.addEventListener('click', function (e) { if (e.target === dlg) dlg.close(); });
    dlg.addEventListener('keydown', function (e) {
      var rtl = getComputedStyle(dlg).direction === 'rtl';
      if (e.key === 'ArrowRight') { e.preventDefault(); show(index + (rtl ? -1 : 1)); }
      else if (e.key === 'ArrowLeft') { e.preventDefault(); show(index + (rtl ? 1 : -1)); }
    });
    dlg.addEventListener('close', function () { img.removeAttribute('src'); if (opener) opener.focus(); });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-lightbox]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
