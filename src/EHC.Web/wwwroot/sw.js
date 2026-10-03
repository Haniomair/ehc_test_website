/* EHC service worker: makes the site installable and gives it an offline page. Pages and files always come from the
   network (no stale content is ever served); only when a page can't be loaded at all does it fall back to
   /offline.html, which lists the emergency numbers. The back-office is never touched. Bump VERSION when the offline
   page or its files change. */
const VERSION = 'ehc-offline-v1';
const OFFLINE = '/offline.html';
const FILES = [OFFLINE, '/assets/css/ehc.css', '/assets/img/logo-ar.png', '/assets/img/icons/icon-192.png'];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(VERSION).then(cache => cache.addAll(FILES)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', event => {
  event.waitUntil(caches.keys()
    .then(keys => Promise.all(keys.filter(k => k !== VERSION).map(k => caches.delete(k))))
    .then(() => self.clients.claim()));
});

self.addEventListener('fetch', event => {
  const req = event.request;
  const url = new URL(req.url);
  if (url.origin !== location.origin || url.pathname.startsWith('/umbraco')) return;
  if (req.mode === 'navigate') {
    event.respondWith(fetch(req).catch(() => caches.match(OFFLINE)));
  } else if (FILES.includes(url.pathname)) {
    // the offline page's own files: network first, cache when offline
    event.respondWith(fetch(req).catch(() => caches.match(url.pathname)));
  }
});
