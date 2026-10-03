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

  // ---------- dark mode: explicit choice is remembered; otherwise follow the OS (Samsung Internet: dark, see theme-init.js) ----------
  var samsung = /SamsungBrowser/i.test(navigator.userAgent);
  function osDark() { return samsung || !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches); }
  function syncDarkButtons() { $$('[data-action="dark"]').forEach(function (b) { b.setAttribute('aria-pressed', html.classList.contains('dark')); }); }
  on('dark', function () { var dark = !html.classList.contains('dark'); html.classList.toggle('dark', dark); store.set('ehc-dark', dark ? '1' : '0'); syncDarkButtons(); });
  if (window.matchMedia) {
    var mq = window.matchMedia('(prefers-color-scheme: dark)');
    var follow = function () { if (store.get('ehc-dark') === null) { html.classList.toggle('dark', osDark()); syncDarkButtons(); } };
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

  // ---------- enter/exit transitions: .is-open is added one frame after .hidden is removed, and .hidden comes back
  // when the exit transition has run (at once when there is none: reduced motion, "pause animations") ----------
  function reveal(el) {
    clearTimeout(el._hideTimer);
    el.classList.remove('hidden');
    void el.offsetWidth;
    el.classList.add('is-open');
  }
  function conceal(el) {
    el.classList.remove('is-open');
    var ms = parseFloat(getComputedStyle($('.panel-sheet', el) || el).transitionDuration) * 1000 || 0;
    clearTimeout(el._hideTimer);
    if (ms) el._hideTimer = setTimeout(function () { el.classList.add('hidden'); }, ms); else el.classList.add('hidden');
  }

  // ---------- modal helper (drawer, search): focus in, Tab trapped, Esc closes, focus returns ----------
  var openModal = null, returnTo = null;
  function showModal(el, focusEl) {
    if (!el) return;
    returnTo = document.activeElement;
    if (el.classList.contains('panel')) reveal(el); else el.classList.remove('hidden');
    document.body.style.overflow = 'hidden';
    openModal = el;
    $$('[aria-controls="' + el.id + '"]').forEach(function (b) { b.setAttribute('aria-expanded', 'true'); });
    setTimeout(function () { (focusEl || $(focusable, el) || el).focus(); }, 30);
  }
  function hideModal() {
    if (!openModal) return;
    if (openModal.classList.contains('panel')) conceal(openModal); else openModal.classList.add('hidden');
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
  // editor links to "#accessibility" (e.g. in the footer) open the panel
  $$('a[href="#accessibility"]').forEach(function (a) {
    if (!a11y) return;
    a.setAttribute('aria-haspopup', 'dialog');
    a.addEventListener('click', function (e) { e.preventDefault(); showModal(a11y); });
  });
  on('a11y-reset', function () {
    a11yKeys.forEach(function (k) { html.classList.remove('a11y-' + k); });
    html.classList.remove('contrast'); store.set('ehc-contrast', '0'); syncContrast();
    size = 16; store.set('ehc-fs', '16'); applySize();
    try { localStorage.removeItem('ehc-dark'); } catch (e) { /* private mode */ }
    html.classList.toggle('dark', osDark()); syncDarkButtons();
    syncA11y();
  });

  // ---------- cookie notice: choice stored as "ehc-consent" = "<version>|all|essential|<ISO date>", asked again after a
  // year or when CONSENT_VERSION changes (bump it when the cookie policy changes). Optional scripts must wait for
  // html[data-consent="all"] or the "ehc:consent" event. ----------
  var consent = $('#consent'), CONSENT_VERSION = '1', consentReturn = null;
  function storedConsent() {
    var v = (store.get('ehc-consent') || '').split('|');
    var at = Date.parse(v[2] || '');
    if (v[0] !== CONSENT_VERSION || !/^(all|essential)$/.test(v[1]) || !(at > Date.now() - 365 * 864e5)) return null;
    return v[1];
  }
  function applyConsent(choice) {
    html.setAttribute('data-consent', choice);
    document.dispatchEvent(new CustomEvent('ehc:consent', { detail: { choice: choice } }));
  }
  if (consent) {
    var given = storedConsent();
    if (given) applyConsent(given); else reveal(consent);
    on('consent', function (el) {
      var choice = el.getAttribute('data-choice') === 'all' ? 'all' : 'essential';
      store.set('ehc-consent', [CONSENT_VERSION, choice, new Date().toISOString()].join('|'));
      applyConsent(choice);
      conceal(consent);
      $$('[data-action="consent-open"]').forEach(function (b) { b.setAttribute('aria-expanded', 'false'); });
      if (consentReturn && consentReturn.focus) consentReturn.focus();
      consentReturn = null;
    });
    on('consent-open', function (el) {
      consentReturn = el;
      el.setAttribute('aria-expanded', 'true');
      reveal(consent);
      consent.setAttribute('tabindex', '-1');
      consent.focus();
    });
  }

  // ---------- Samsung Internet: recommend another browser once (its "Dark sites" mode recolours the site; no opt-out).
  // Shown on the first page opened there, after the cookie notice has been answered; remembered as "ehc-browser-tip". ----------
  var browserTip = $('#browserTip');
  if (browserTip && samsung && store.get('ehc-browser-tip') !== '1') {
    var chrome = $('[data-open-chrome]', browserTip);
    if (chrome) {
      chrome.href = 'intent://' + location.host + location.pathname + location.search + '#Intent;scheme=https;package=com.android.chrome;S.browser_fallback_url=' + encodeURIComponent(location.href) + ';end';
    }
    on('browser-tip-close', function () { conceal(browserTip); });
    var showTip = function () { store.set('ehc-browser-tip', '1'); reveal(browserTip); };
    if (!consent || html.hasAttribute('data-consent')) showTip();
    else document.addEventListener('ehc:consent', function once() { document.removeEventListener('ehc:consent', once); setTimeout(showTip, 450); });
  }

  // ---------- look customizer (reviewers only; the form itself is components/customizer.js) ----------
  var customizer = $('#customizer');
  on('customizer-open', function () { showModal(customizer); });
  on('customizer-close', hideModal);
  // rendered open after a customizer change: adopt it without the slide-in; focus the control that was used
  if (customizer && customizer.hasAttribute('data-reopened')) {
    showModal(customizer, $('[data-cz-focus]', customizer));
    returnTo = $('[data-action="customizer-open"]');
  }

  // ---------- search palette (Views/Partials/site/_Search.cshtml) ----------
  // A WAI-ARIA combobox: the input owns a listbox of results (aria-activedescendant); ↑ ↓ move, Enter opens.
  // Everything from the server is added with DOM APIs (textContent), never as HTML.
  var search = $('#search'), sIn = $('#sIn');
  var sCfg = $('[data-s-config]');
  var S = search && sIn && sCfg ? {
    list: $('#sList'), results: $('[data-s-results]', search), start: $('[data-s-start]', search), msg: $('[data-s-message]', search),
    scopes: $('[data-s-scopes]', search), busy: $('[data-s-busy]', search), clear: $('[data-s-clear]', search), status: $('[data-s-status]', search),
    recent: $('[data-s-recent]', search), recentList: $('[data-s-recent-list]', search)
  } : null;
  var openSearch = function (q) {
    if (!search) return;
    if (openModal && openModal !== search) hideModal();
    showModal(search, sIn);
    if (sIn) { sIn.value = q || ''; sIn.dispatchEvent(new Event('input')); }
  };
  on('search-open', function () { openSearch(''); });
  on('search-close', hideModal);
  if (S) {
    var labels = {}; try { labels = JSON.parse(sCfg.getAttribute('data-labels') || '{}'); } catch (e) { /* keep keys */ }
    var txt = function (k) { return sCfg.getAttribute('data-' + k) || ''; };
    var lang = html.lang || 'ar', dateFmt = null;
    try { dateFmt = new Intl.DateTimeFormat(lang + '-u-nu-latn', { day: 'numeric', month: 'long', year: 'numeric' }); } catch (e) { /* plain dates */ }
    var groups = [], scope = 'all', active = -1, timer = null, seq = 0, query = '';
    var RECENT = 'ehc-search-recent';

    var show = function (el, on) { if (el) el.classList.toggle('hidden', !on); };
    var icon = function (name, cls) {
      var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); svg.setAttribute('class', cls || 'ico'); svg.setAttribute('aria-hidden', 'true');
      var use = document.createElementNS('http://www.w3.org/2000/svg', 'use'); use.setAttribute('href', '#i-' + name); svg.appendChild(use); return svg;
    };
    // the title with each word of the query marked (case-insensitive), as text nodes and <mark> elements
    var highlight = function (el, text) {
      var words = query.toLowerCase().split(/\s+/).filter(function (w) { return w.length > 1; });
      var low = text.toLowerCase(), i = 0;
      while (i < text.length) {
        var at = -1, len = 0;
        words.forEach(function (w) { var k = low.indexOf(w, i); if (k >= 0 && (at < 0 || k < at)) { at = k; len = w.length; } });
        if (at < 0) { el.appendChild(document.createTextNode(text.slice(i))); break; }
        if (at > i) el.appendChild(document.createTextNode(text.slice(i, at)));
        var m = document.createElement('mark'); m.textContent = text.slice(at, at + len); el.appendChild(m);
        i = at + len;
      }
    };

    // recent searches: this browser only, best effort
    var recentGet = function () { try { return JSON.parse(localStorage.getItem(RECENT) || '[]').filter(function (x) { return typeof x === 'string'; }).slice(0, 6); } catch (e) { return []; } };
    var recentSave = function (q) {
      q = (q || '').trim(); if (q.length < 2) return;
      try { localStorage.setItem(RECENT, JSON.stringify([q].concat(recentGet().filter(function (x) { return x !== q; })).slice(0, 6))); } catch (e) { /* storage off */ }
    };
    var renderRecent = function () {
      if (!S.recent) return;
      var items = recentGet();
      S.recentList.textContent = '';
      items.forEach(function (q) {
        var b = document.createElement('button'); b.type = 'button'; b.className = 'chip inline-flex items-center gap-1.5 hover:border-brand-500 hover:text-brand-600';
        b.setAttribute('data-s-term', q); b.appendChild(icon('clock', 'ico !h-3.5 !w-3.5 text-slate-400')); b.appendChild(document.createTextNode(q));
        S.recentList.appendChild(b);
      });
      show(S.recent, items.length > 0);
    };

    var options = function () { return [].slice.call(S.list.querySelectorAll('[role="option"]')); };
    var setActive = function (i, scroll) {
      var opts = options();
      active = opts.length ? Math.max(-1, Math.min(i, opts.length - 1)) : -1;
      opts.forEach(function (o, j) { o.setAttribute('aria-selected', j === active ? 'true' : 'false'); });
      if (active >= 0) { sIn.setAttribute('aria-activedescendant', opts[active].id); if (scroll) opts[active].scrollIntoView({ block: 'nearest' }); }
      else sIn.removeAttribute('aria-activedescendant');
    };

    var renderScopes = function () {
      S.scopes.textContent = '';
      var total = groups.reduce(function (n, g) { return n + g.items.length; }, 0);
      var chip = function (key, label, count) {
        var b = document.createElement('button'); b.type = 'button'; b.className = 'chip inline-flex shrink-0 items-center gap-1.5';
        b.setAttribute('aria-pressed', scope === key ? 'true' : 'false'); b.setAttribute('data-s-scope', key);
        b.appendChild(document.createTextNode(label));
        var c = document.createElement('span'); c.className = 'rounded-full bg-black/5 px-1.5 text-[.7rem] tabular-nums dark:bg-white/10'; c.textContent = String(count);
        b.appendChild(c); S.scopes.appendChild(b);
      };
      chip('all', S.scopes.getAttribute('data-all') || 'All', total);
      groups.forEach(function (g) { chip(g.key, labels[g.key] || g.key, g.items.length); });
      S.scopes.classList.toggle('hidden', groups.length < 2);
      S.scopes.classList.toggle('flex', groups.length >= 2);
    };

    var renderResults = function () {
      S.list.textContent = '';
      var n = 0;
      groups.forEach(function (g) {
        if (scope !== 'all' && scope !== g.key) return;
        var grp = document.createElement('div'); grp.setAttribute('role', 'group');
        var h = document.createElement('p'); h.className = 's-head px-3 pt-3'; h.id = 'sG-' + g.key; h.textContent = labels[g.key] || g.key;
        grp.setAttribute('aria-labelledby', h.id); grp.appendChild(h);
        g.items.forEach(function (it) {
          var a = document.createElement('a');
          a.href = it.url; a.id = 'sO-' + (n++); a.className = 's-opt'; a.setAttribute('role', 'option'); a.setAttribute('aria-selected', 'false'); a.tabIndex = -1;
          var ic = document.createElement('span'); ic.className = 's-opt-ic';
          if (/^[a-z0-9-]+$/.test(it.icon || '')) ic.appendChild(icon(it.icon, 'ico !h-5 !w-5'));
          var body = document.createElement('span'); body.className = 'min-w-0 flex-1';
          var t = document.createElement('span'); t.className = 'block truncate font-semibold'; highlight(t, it.title || '');
          body.appendChild(t);
          var meta = [it.meta, it.date && dateFmt ? dateFmt.format(new Date(it.date + 'T12:00:00')) : it.date].filter(Boolean).join(' · ');
          if (meta) { var m = document.createElement('span'); m.className = 'block truncate text-xs text-slate-500 dark:text-slate-400'; m.textContent = meta; body.appendChild(m); }
          var go = icon('arrow', 'ico flip s-opt-go !h-4 !w-4');
          a.appendChild(ic); a.appendChild(body); a.appendChild(go);
          grp.appendChild(a);
        });
        S.list.appendChild(grp);
      });
      setActive(-1);
    };

    var state = function (mode, text) {
      // mode: start | results | empty | error
      show(S.results, mode === 'results');
      show(S.msg, mode === 'empty' || mode === 'error');
      show(S.start, mode === 'start' || mode === 'empty');
      if (S.msg && text) S.msg.textContent = text;
      sIn.setAttribute('aria-expanded', mode === 'results' ? 'true' : 'false');
      if (mode !== 'results') { groups = []; S.scopes.classList.add('hidden'); S.scopes.classList.remove('flex'); S.list.textContent = ''; setActive(-1); }
      if (mode === 'start' || mode === 'empty') renderRecent();
    };

    var run = function () {
      query = sIn.value.trim();
      show(S.clear, sIn.value.length > 0); S.clear && S.clear.classList.toggle('grid', sIn.value.length > 0);
      if (query.length < 2) { show(S.busy, false); state('start'); if (S.status) S.status.textContent = ''; return; }
      var mine = ++seq;
      show(S.busy, true);
      fetch((sCfg.getAttribute('data-api') || '/api/search') + '?q=' + encodeURIComponent(query) + '&culture=' + encodeURIComponent(lang), { headers: { 'Accept': 'application/json' } })
        .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
        .then(function (data) {
          if (mine !== seq) return;
          show(S.busy, false);
          groups = (data || []).filter(function (g) { return g && g.items && g.items.length; });
          if (!groups.some(function (g) { return g.key === scope; })) scope = 'all';
          var total = groups.reduce(function (n, g) { return n + g.items.length; }, 0);
          if (S.status) S.status.textContent = total ? (txt('count') || '{0}').replace('{0}', total) : txt('empty');
          if (!total) { state('empty', txt('empty')); return; }
          state('results'); renderScopes(); renderResults();
        })
        .catch(function () { if (mine !== seq) return; show(S.busy, false); state('error', txt('error')); });
    };
    sIn.addEventListener('input', function () { clearTimeout(timer); timer = setTimeout(run, 220); });
    sIn.addEventListener('keydown', function (e) {
      var opts = options();
      if (e.key === 'ArrowDown' && opts.length) { e.preventDefault(); setActive(active + 1 >= opts.length ? 0 : active + 1, true); }
      else if (e.key === 'ArrowUp' && opts.length) { e.preventDefault(); setActive(active <= 0 ? opts.length - 1 : active - 1, true); }
      else if (e.key === 'Enter') {
        var pick = opts[active >= 0 ? active : 0];
        if (pick) { e.preventDefault(); recentSave(query); location.href = pick.href; }
      }
    });
    S.list.addEventListener('mousemove', function (e) {
      var o = e.target.closest && e.target.closest('[role="option"]');
      if (o) { var i = options().indexOf(o); if (i !== active) setActive(i, false); }
    });
    S.list.addEventListener('click', function (e) { if (e.target.closest('[role="option"]')) recentSave(query); });
    S.scopes.addEventListener('click', function (e) {
      var b = e.target.closest('[data-s-scope]'); if (!b) return;
      scope = b.getAttribute('data-s-scope');
      [].forEach.call(S.scopes.querySelectorAll('[data-s-scope]'), function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); });
      renderResults(); sIn.focus();
    });
    // one-tap searches (specialties, recent)
    search.addEventListener('click', function (e) {
      var t = e.target.closest('[data-s-term]'); if (!t) return;
      sIn.value = t.getAttribute('data-s-term'); sIn.focus(); clearTimeout(timer); run();
    });
    if (S.clear) S.clear.addEventListener('click', function () { sIn.value = ''; sIn.focus(); run(); });
    var rc = $('[data-s-recent-clear]', search);
    if (rc) rc.addEventListener('click', function () { try { localStorage.removeItem(RECENT); } catch (e) { /* storage off */ } renderRecent(); sIn.focus(); });
    renderRecent();
  }

  // search boxes in page sections (e.g. the search hero) hand their query to the palette
  $$('[data-search-form]').forEach(function (form) {
    form.addEventListener('submit', function (e) {
      if (!search) return;
      e.preventDefault();
      var q = form.querySelector('input[name="q"]');
      openSearch(q ? q.value : '');
    });
  });
  document.addEventListener('keydown', function (e) {
    if (!search || !(e.ctrlKey || e.metaKey) || e.key.toLowerCase() !== 'k') return;
    e.preventDefault();
    if (openModal === search) hideModal(); else openSearch('');
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

  // ---------- installable app: service worker for the offline page (sw.js; network first, nothing stale) ----------
  if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () { navigator.serviceWorker.register('/sw.js').catch(function () { /* not essential */ }); });
  }

  // ---------- reveal-on-scroll for blocks that use .rv ----------
  var rv = $$('.rv');
  if (rv.length) {
    if ('IntersectionObserver' in window) {
      // on screen at load: shown as is (no fade, so the page's main content isn't held back); the rest fades in on scroll
      rv.forEach(function (e) { if (e.getBoundingClientRect().top < window.innerHeight) e.classList.add('in'); });
      html.classList.add('rv-on');
      var io = new IntersectionObserver(function (es) { es.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } }); }, { rootMargin: '0px 0px -8% 0px' });
      rv.forEach(function (e) { if (!e.classList.contains('in')) io.observe(e); });
    } else rv.forEach(function (e) { e.classList.add('in'); });
  }
})();
