/* Embed section: the provider's frame is created only when the visitor presses "Load" (the address was validated and
   rebuilt on the server). The frame is sandboxed — scripts, forms and pop-ups for the provider's own links, but no access
   to this site — titled for screen readers, and receives focus. */
(function () {
  'use strict';
  function init(box) {
    if (box.hasAttribute('data-embed-ready')) return;
    box.setAttribute('data-embed-ready', '');
    var btn = box.querySelector('[data-embed-load]'), src = box.getAttribute('data-embed');
    if (!btn || !/^https:\/\//.test(src || '')) return;
    btn.hidden = false;
    btn.addEventListener('click', function () {
      var f = document.createElement('iframe');
      f.src = src;
      f.title = box.getAttribute('data-embed-title') || '';
      f.className = 'absolute inset-0 h-full w-full border-0 bg-white';
      f.setAttribute('sandbox', 'allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox allow-downloads');
      f.referrerPolicy = 'strict-origin-when-cross-origin';
      f.allow = 'fullscreen';
      f.allowFullscreen = true;
      box.textContent = '';
      box.appendChild(f);
      f.focus();
    });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-embed]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
