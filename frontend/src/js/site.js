/* EHC site chrome: dark mode, text size, contrast, mega menu, drawer, search palette.
   Loaded on every Umbraco page. No inline handlers (CSP): everything is wired through data-action attributes,
   and every feature checks that its elements exist, because a page only contains what editors added.
   Block behaviours (hero slider, navigator, …) live in their own files and are loaded by the blocks that need them. */
(function () {
  'use strict';
  var html = document.documentElement;
  var store = {
    get: function (k) { try { return localStorage.getItem(k); } catch (e) { return null; } },
    set: function (k, v) { try { localStorage.setItem(k, v); } catch (e) { /* private mode */ } }
  };
  function $(sel, root) { return (root || document).querySelector(sel); }
  function $$(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }
  function on(action, fn) { $$('[data-action="' + action + '"]').forEach(function (el) { el.addEventListener('click', function (e) { fn(el, e); }); }); }
  var focusable = 'a[href],button:not([disabled]),input:not([disabled]),select,textarea,[tabindex]:not([tabindex="-1"])';

  // ---------- dark mode: explicit choice is remembered; otherwise follow the OS ----------
  function syncDarkButtons() { $$('[data-action="dark"]').forEach(function (b) { b.setAttribute('aria-pressed', html.classList.contains('dark')); }); }
  on('dark', function () { var dark = !html.classList.contains('dark'); html.classList.toggle('dark', dark); store.set('ehc-dark', dark ? '1' : '0'); syncDarkButtons(); });
  if (window.matchMedia) {
    var mq = window.matchMedia('(prefers-color-scheme: dark)');
    var follow = function (e) { if (store.get('ehc-dark') === null) { html.classList.toggle('dark', e.matches); syncDarkButtons(); } };
    if (mq.addEventListener) mq.addEventListener('change', follow); else if (mq.addListener) mq.addListener(follow);
  }
  syncDarkButtons();

  // ---------- text size + contrast (remembered) ----------
  var size = parseInt(store.get('ehc-fs') || '16', 10);
  function applySize() { if (size === 16) html.style.removeProperty('--fs'); else html.style.setProperty('--fs', size + 'px'); }
  applySize();
  on('font', function (el) { size = Math.max(14, Math.min(20, size + parseInt(el.getAttribute('data-step') || '0', 10))); store.set('ehc-fs', String(size)); applySize(); });
  function syncContrast() { $$('[data-action="contrast"]').forEach(function (b) { b.setAttribute('aria-pressed', html.classList.contains('contrast')); }); }
  if (store.get('ehc-contrast') === '1') html.classList.add('contrast');
  syncContrast();
  on('contrast', function () { store.set('ehc-contrast', html.classList.toggle('contrast') ? '1' : '0'); syncContrast(); });

  // ---------- modal helper (drawer, search): focus in, Tab trapped, Esc closes, focus returns ----------
  var openModal = null, returnTo = null;
  function showModal(el, focusEl) {
    if (!el) return;
    returnTo = document.activeElement;
    el.classList.remove('hidden');
    document.body.style.overflow = 'hidden';
    openModal = el;
    $$('[aria-controls="' + el.id + '"]').forEach(function (b) { b.setAttribute('aria-expanded', 'true'); });
    setTimeout(function () { (focusEl || $(focusable, el) || el).focus(); }, 30);
  }
  function hideModal() {
    if (!openModal) return;
    openModal.classList.add('hidden');
    document.body.style.overflow = '';
    $$('[aria-controls="' + openModal.id + '"]').forEach(function (b) { b.setAttribute('aria-expanded', 'false'); });
    openModal = null;
    if (returnTo && returnTo.focus) returnTo.focus();
  }
  document.addEventListener('keydown', function (e) {
    if (!openModal) return;
    if (e.key === 'Escape') { e.preventDefault(); hideModal(); return; }
    if (e.key !== 'Tab') return;
    var items = $$(focusable, openModal).filter(function (x) { return x.offsetParent !== null; });
    if (!items.length) return;
    var first = items[0], last = items[items.length - 1];
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  });

  // ---------- drawer ----------
  var drawer = $('#drawer');
  on('drawer-open', function () { showModal(drawer); });
  on('drawer-close', hideModal);

  // ---------- search palette ----------
  var search = $('#search'), sIn = $('#sIn');
  on('search-open', function () { if (sIn) sIn.value = ''; showModal(search, sIn); });
  on('search-close', hideModal);
  document.addEventListener('keydown', function (e) {
    if (!search || !(e.ctrlKey || e.metaKey) || e.key.toLowerCase() !== 'k') return;
    e.preventDefault();
    if (openModal === search) hideModal(); else { if (openModal) hideModal(); showModal(search, sIn); }
  });

  // ---------- mega menu ----------
  var hdr = $('#hdr'), dim = $('#megaDim'), trigs = $$('.mtrig'), openId = null, tOpen, tClose;
  function panel(id) { return document.getElementById(id); }
  function openMega(id) {
    clearTimeout(tClose);
    if (openId === id) return;
    trigs.forEach(function (t) { t.setAttribute('aria-expanded', t.getAttribute('data-mega') === id ? 'true' : 'false'); });
    $$('.mega-panel').forEach(function (p) { p.classList.toggle('open', p.id === id); });
    openId = id;
    if (dim) dim.classList.toggle('opacity-100', !!id);
  }
  function closeMega(refocus) {
    var was = openId;
    openId = null;
    trigs.forEach(function (t) { t.setAttribute('aria-expanded', 'false'); });
    $$('.mega-panel').forEach(function (p) { p.classList.remove('open'); });
    if (dim) dim.classList.remove('opacity-100');
    if (refocus && was) { var t = $('[data-mega="' + was + '"]'); if (t) t.focus(); }
  }
  if (hdr && trigs.length) {
    var hover = window.matchMedia && window.matchMedia('(hover: hover)').matches;
    trigs.forEach(function (t, i) {
      var id = t.getAttribute('data-mega');
      if (hover) {
        t.addEventListener('mouseenter', function () { clearTimeout(tOpen); tOpen = setTimeout(function () { openMega(id); }, openId ? 0 : 120); });
        t.addEventListener('mouseleave', function () { clearTimeout(tOpen); });
      }
      t.addEventListener('click', function () { if (openId === id) closeMega(false); else openMega(id); });
      t.addEventListener('keydown', function (e) {
        if (e.key === 'ArrowDown') { e.preventDefault(); openMega(id); var f = $('a', panel(id)); if (f) f.focus(); }
        else if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
          e.preventDefault();
          var step = (e.key === 'ArrowRight') === (html.dir !== 'rtl') ? 1 : -1;
          var next = trigs[(i + step + trigs.length) % trigs.length];
          next.focus();
          if (openId) openMega(next.getAttribute('data-mega'));
        }
      });
    });
    hdr.addEventListener('mouseleave', function () { clearTimeout(tOpen); tClose = setTimeout(function () { closeMega(false); }, 180); });
    hdr.addEventListener('mouseenter', function () { clearTimeout(tClose); });
    hdr.addEventListener('focusout', function (e) { if (openId && !hdr.contains(e.relatedTarget)) closeMega(false); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && openId && !openModal) closeMega(true); });
    document.addEventListener('click', function (e) { if (openId && !hdr.contains(e.target)) closeMega(false); });
    $$('.mega-panel a').forEach(function (a) { a.addEventListener('click', function () { closeMega(false); }); });
  }

  // ---------- reveal-on-scroll for blocks that use .rv ----------
  var rv = $$('.rv');
  if (rv.length) {
    if ('IntersectionObserver' in window) {
      var io = new IntersectionObserver(function (es) { es.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } }); }, { rootMargin: '0px 0px -8% 0px' });
      rv.forEach(function (e) { io.observe(e); });
    } else rv.forEach(function (e) { e.classList.add('in'); });
  }
})();
