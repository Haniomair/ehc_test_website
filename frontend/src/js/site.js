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
  if (!(size >= 14 && size <= 20)) size = 16;
  function applySize() {
    if (size === 16) html.style.removeProperty('--fs'); else html.style.setProperty('--fs', size + 'px');
    $$('[data-a11y-size]').forEach(function (el) { el.textContent = Math.round(size / 16 * 100) + '%'; });
    $$('[data-action="font"]').forEach(function (b) { var s = parseInt(b.getAttribute('data-step') || '0', 10); b.disabled = s < 0 ? size <= 14 : size >= 20; });
  }
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

  // ---------- accessibility panel: each option is an "a11y-<key>" class on <html>, remembered as "ehc-a11y" ----------
  var a11y = $('#a11y');
  var a11yKeys = ['links', 'spacing', 'still', 'cursor', 'guide'];
  var guide = null;
  function a11yOn(k) { return html.classList.contains('a11y-' + k); }
  function pauseMotion() {
    $$('[data-hero-pause][aria-pressed="false"]').forEach(function (b) { b.click(); });
    $$('video').forEach(function (v) { if (!v.paused) v.pause(); });
  }
  function moveGuide(y) { if (guide) guide.style.transform = 'translateY(' + Math.round(y) + 'px)'; }
  function syncGuide() {
    if (a11yOn('guide') && !guide) {
      guide = document.createElement('div'); guide.className = 'a11y-guide-bar'; guide.setAttribute('aria-hidden', 'true');
      document.body.appendChild(guide); moveGuide(window.innerHeight / 2);
    } else if (!a11yOn('guide') && guide) { guide.remove(); guide = null; }
  }
  document.addEventListener('pointermove', function (e) { if (guide) moveGuide(e.clientY); }, { passive: true });
  document.addEventListener('focusin', function (e) { if (guide && e.target.getBoundingClientRect) { var r = e.target.getBoundingClientRect(); moveGuide(r.top + r.height / 2); } });
  function syncA11y() {
    $$('[data-a11y]').forEach(function (b) { b.setAttribute('aria-pressed', a11yOn(b.getAttribute('data-a11y'))); });
    store.set('ehc-a11y', a11yKeys.filter(a11yOn).join(','));
    syncGuide();
  }
  $$('[data-a11y]').forEach(function (b) {
    b.addEventListener('click', function () {
      var k = b.getAttribute('data-a11y');
      html.classList.toggle('a11y-' + k);
      if (k === 'still' && a11yOn('still')) pauseMotion();
      syncA11y();
    });
  });
  if (a11yOn('still')) pauseMotion();
  syncA11y();
  on('a11y-open', function () { showModal(a11y); });
  on('a11y-close', hideModal);
  on('a11y-reset', function () {
    a11yKeys.forEach(function (k) { html.classList.remove('a11y-' + k); });
    html.classList.remove('contrast'); store.set('ehc-contrast', '0'); syncContrast();
    size = 16; store.set('ehc-fs', '16'); applySize();
    try { localStorage.removeItem('ehc-dark'); } catch (e) { /* private mode */ }
    html.classList.toggle('dark', !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)); syncDarkButtons();
    syncA11y();
  });

  // ---------- search palette ----------
  var search = $('#search'), sIn = $('#sIn');
  on('search-open', function () { if (sIn) { sIn.value = ''; sIn.dispatchEvent(new Event('input')); } showModal(search, sIn); });
  on('search-close', hideModal);
  // live results from /api/search (debounced; built with DOM APIs, never innerHTML with server text)
  var sRes = $('#sRes');
  if (sIn && sRes) {
    var labels = {}; try { labels = JSON.parse(sRes.getAttribute('data-labels') || '{}'); } catch (e) { /* keep keys */ }
    var timer = null, seq = 0;
    var message = function (text) { sRes.textContent = ''; var p = document.createElement('p'); p.className = 'px-3 py-8 text-center text-slate-500'; p.textContent = text; sRes.appendChild(p); };
    var render = function (groups) {
      sRes.textContent = '';
      if (!groups.length) { message(sRes.getAttribute('data-empty') || ''); return; }
      groups.forEach(function (g) {
        var h = document.createElement('p');
        h.className = 'px-3 pb-1 pt-3 text-xs font-bold uppercase tracking-widest text-slate-400 rtl:tracking-normal';
        h.textContent = labels[g.key] || g.key;
        sRes.appendChild(h);
        g.items.forEach(function (it) {
          var a = document.createElement('a');
          a.href = it.url;
          a.className = 'flex items-center gap-3 rounded-xl px-3 py-2.5 hover:bg-brand-50 focus:bg-brand-50 dark:hover:bg-white/5 dark:focus:bg-white/5';
          var ic = document.createElement('span'); ic.className = 'mega-ic !h-9 !w-9';
          if (/^[a-z0-9-]+$/.test(it.icon || '')) {
            var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); svg.setAttribute('class', 'ico'); svg.setAttribute('aria-hidden', 'true');
            var use = document.createElementNS('http://www.w3.org/2000/svg', 'use'); use.setAttribute('href', '#i-' + it.icon); svg.appendChild(use); ic.appendChild(svg);
          }
          var t = document.createElement('span'); t.className = 'flex-1'; t.textContent = it.title;
          a.appendChild(ic); a.appendChild(t);
          sRes.appendChild(a);
        });
      });
    };
    var run = function () {
      var q = sIn.value.trim();
      if (q.length < 2) { message(sRes.getAttribute('data-hint') || ''); return; }
      var mine = ++seq;
      fetch((sRes.getAttribute('data-api') || '/api/search') + '?q=' + encodeURIComponent(q) + '&culture=' + encodeURIComponent(html.lang || 'ar'), { headers: { 'Accept': 'application/json' } })
        .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
        .then(function (groups) { if (mine === seq) render(groups || []); })
        .catch(function () { if (mine === seq) message(sRes.getAttribute('data-error') || ''); });
    };
    sIn.addEventListener('input', function () { clearTimeout(timer); timer = setTimeout(run, 250); });
  }

  // search boxes in page sections (e.g. the search hero) hand their query to the palette
  $$('[data-search-form]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      if (!search) return;
      e.preventDefault();
      var q = form.querySelector('input[name="q"]');
      showModal(search, sIn);
      if (sIn && q) { sIn.value = q.value; sIn.dispatchEvent(new Event('input', { bubbles: true })); }
    });
  });
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
