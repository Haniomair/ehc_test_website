/* Click-to-load YouTube: nothing is requested from YouTube until the visitor presses play. The player then loads from
   youtube-nocookie.com (privacy-enhanced mode) with autoplay, and focus moves into it. The id was validated server-side. */
(function () {
  'use strict';
  var ID = /^[A-Za-z0-9_-]{11}$/;
  function init(box) {
    if (box.hasAttribute('data-video-ready')) return;
    box.setAttribute('data-video-ready', '');
    var btn = box.querySelector('[data-video-play]'), id = box.getAttribute('data-video-embed');
    if (!btn || !ID.test(id || '')) return;
    btn.addEventListener('click', function () {
      var f = document.createElement('iframe');
      f.src = 'https://www.youtube-nocookie.com/embed/' + id + '?autoplay=1&rel=0&hl=' + encodeURIComponent(document.documentElement.lang || 'ar');
      f.title = box.getAttribute('data-video-title') || 'Video';
      f.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture';
      f.allowFullscreen = true;
      f.referrerPolicy = 'strict-origin-when-cross-origin';
      f.className = 'absolute inset-0 h-full w-full border-0';
      box.textContent = '';
      box.appendChild(f);
      f.focus();
    });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-video-embed]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
