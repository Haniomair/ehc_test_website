/* Share bar: device share sheet (navigator.share), copy link (clipboard) and print. Each button is shown only when the
   browser supports it; results are announced through the bar's status element. */
(function () {
  'use strict';
  function init(bar) {
    if (bar.hasAttribute('data-share-ready')) return;
    bar.setAttribute('data-share-ready', '');
    var status = bar.querySelector('[data-share-status]');
    var native = bar.querySelector('[data-share-native]'), copy = bar.querySelector('[data-share-copy]'), print = bar.querySelector('[data-share-print]');
    var url = (document.querySelector('link[rel="canonical"]') || {}).href || location.href;
    if (native && navigator.share) {
      native.hidden = false;
      native.addEventListener('click', function () { navigator.share({ title: document.title, url: url }).catch(function () {}); });
    }
    if (copy && navigator.clipboard) {
      copy.hidden = false;
      copy.addEventListener('click', function () {
        navigator.clipboard.writeText(url).then(function () {
          if (!status) return;
          status.textContent = copy.getAttribute('data-done') || '';
          setTimeout(function () { status.textContent = ''; }, 3000);
        }).catch(function () {});
      });
    }
    if (print && window.print) {
      print.hidden = false;
      print.addEventListener('click', function () { window.print(); });
    }
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-share]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
