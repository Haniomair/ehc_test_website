/* Care navigator ([data-navigator]): options come from data-options (JSON written by the Razor partial), never from this file.
   Selecting "who" and "need" updates the result; urgent needs switch the result to emergency styling. Safe to load twice.
   Symptom guide (when the block has symptoms): the "By symptom" tile slides the panel to a second view with a search
   (combobox over symptom names and other words; Arabic diacritics and letter variants are ignored), every symptom as
   chips grouped by care level, and the chosen symptom's advice; it fills the main view's box, so the card never jumps. Levels with "nearest" offer the three closest suitable
   facilities: the position comes from the browser's geolocation and never leaves it; ER wait times are added when
   /api/er-wait has them. The slide follows the reading direction and is skipped under prefers-reduced-motion. */
(function () {
  'use strict';
  var URGENT = ['bg-emerg-50', 'text-emerg-600', 'dark:bg-emerg-500/15', 'dark:text-red-300'];
  var NORMAL = ['bg-brand-50', 'text-brand-500', 'dark:bg-brand-500/15', 'dark:text-brand-300'];
  var BADGE_URGENT = ['bg-emerg-50', 'text-emerg-600', 'dark:bg-emerg-500/15', 'dark:text-red-200'];
  var BADGE_NORMAL = ['bg-brand-50', 'text-brand-700', 'dark:bg-brand-500/15', 'dark:text-brand-200'];
  var LEVEL_ICON = { emergency: 'er', urgent: 'clock', primary: 'clinic', virtual: 'phone', self: 'home' };
  var MAX_SUGGESTIONS = 7;

  /* lower case, no Arabic diacritics or tatweel, one form for alef / yaa / taa marbuta / hamza carriers */
  function norm(s) {
    return String(s || '').toLowerCase()
      .replace(/[ً-ٰٟـ]/g, '')
      .replace(/[آأإٱ]/g, 'ا')
      .replace(/ى/g, 'ي')
      .replace(/ة/g, 'ه')
      .replace(/ؤ/g, 'و').replace(/ئ/g, 'ي')
      .replace(/[^\p{L}\p{N}]+/gu, ' ').trim();
  }

  function km(a, b) {
    var r = Math.PI / 180, dLat = (b[0] - a[0]) * r, dLng = (b[1] - a[1]) * r;
    var h = Math.sin(dLat / 2) * Math.sin(dLat / 2) + Math.cos(a[0] * r) * Math.cos(b[0] * r) * Math.sin(dLng / 2) * Math.sin(dLng / 2);
    return 12742 * Math.asin(Math.sqrt(h));
  }

  function swapClasses(el, off, on) {
    off.forEach(function (c) { el.classList.remove(c); });
    on.forEach(function (c) { el.classList.add(c); });
  }

  function setIcon(box, urgent, name) {
    if (!box) return;
    swapClasses(box, urgent ? NORMAL : URGENT, urgent ? URGENT : NORMAL);
    var use = box.querySelector('use');
    if (use && /^[a-z0-9-]+$/.test(name || '')) use.setAttribute('href', '#i-' + name);
  }

  function setCta(a, label, href, urgent) {
    if (!a) return;
    a.textContent = label || '';
    a.setAttribute('href', href || '#');
    var show = !!(label && href);
    a.classList.toggle('hidden', !show);
    a.hidden = !show;
    a.classList.toggle('!bg-emerg-600', !!urgent);
    a.classList.toggle('!shadow-none', !!urgent);
  }

  function init(root) {
    if (root.hasAttribute('data-navigator-ready')) return;
    root.setAttribute('data-navigator-ready', '');
    var data;
    try { data = JSON.parse(root.getAttribute('data-options') || '{}'); } catch (e) { return; }
    var needs = data.needs || [], who = data.who || [], symptoms = data.symptoms || [], levels = data.levels || {};
    if (!needs.length) return;

    var q = function (s) { return root.querySelector(s); };
    var qa = function (s) { return [].slice.call(root.querySelectorAll(s)); };
    var title = q('[data-nav-title]'), text = q('[data-nav-text]'), cta = q('[data-nav-cta]'), icon = q('[data-nav-icon]');
    var whoBtns = qa('[data-who]'), needBtns = qa('[data-need]');
    var cur = { who: who.length ? who[0].key : null, need: (needs.filter(function (n) { return !n.urgent; })[0] || needs[0]).key };

    function find(list, key) { for (var i = 0; i < list.length; i++) if (list[i].key === key) return list[i]; return null; }
    function render() {
      var n = find(needs, cur.need), w = find(who, cur.who);
      if (!n) return;
      if (title) title.textContent = n.title;
      if (text) text.textContent = n.text + (w && w.note && !n.urgent ? ' ' + w.note : '');
      setCta(cta, n.cta, n.href, n.urgent);
      setIcon(icon, n.urgent, n.icon);
    }
    function bind(btns, attr, field) {
      btns.forEach(function (b) {
        b.addEventListener('click', function () {
          cur[field] = b.getAttribute(attr);
          btns.forEach(function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); });
          render();
        });
      });
    }
    bind(whoBtns, 'data-who', 'who');
    bind(needBtns, 'data-need', 'need');
    render();

    /* ---- symptom guide ---- */
    var mainView = q('[data-nav-view="main"]'), symView = q('[data-nav-view="symptom"]');
    var openBtn = q('[data-need-symptom]'), backBtn = q('[data-nav-back]'), heading = q('[data-nav-sym-heading]');
    var search = q('[data-nav-search]'), list = q('[data-nav-list]'), count = q('[data-nav-count]'), browse = q('[data-nav-browse]');
    var browseTitle = q('[data-nav-browse-title]'), out = q('[data-nav-sym-out]'), nearBtn = q('[data-nav-near-btn]'), near = q('[data-nav-near]');
    if (!symptoms.length || !mainView || !symView || !openBtn || !search || !list || !out) return;

    var reduced = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
    var rtl = (root.closest('[dir]') || document.documentElement).getAttribute('dir') === 'rtl';
    var busy = false;

    /* the symptom view lies over the main view in the same box (the main view keeps its place, invisible and inert), so
       the card keeps its height; switching cross-fades the two with a short slide: forward goes towards the reading
       start, back towards the end */
    function setActive(symptom) {
      symView.hidden = !symptom;
      mainView.classList.toggle('invisible', symptom);
      mainView.inert = symptom;
      if (symptom) mainView.setAttribute('aria-hidden', 'true'); else mainView.removeAttribute('aria-hidden');
    }
    function go(toSymptom, done) {
      if (busy) return;
      if (reduced || !mainView.animate) { setActive(toSymptom); if (done) done(); return; }
      busy = true;
      var dx = (rtl ? -1 : 1) * 28;
      var leave = toSymptom ? mainView : symView, enter = toSymptom ? symView : mainView;
      symView.hidden = false;
      mainView.classList.remove('invisible');
      var ease = 'cubic-bezier(.2,.8,.2,1)', shift = toSymptom ? -dx : dx;
      leave.animate([{ opacity: 1, transform: 'none' }, { opacity: 0, transform: 'translateX(' + shift + 'px)' }], { duration: 220, easing: ease, fill: 'forwards' });
      enter.animate([{ opacity: 0, transform: 'translateX(' + (-shift) + 'px)' }, { opacity: 1, transform: 'none' }], { duration: 280, easing: ease }).onfinish = function () {
        leave.getAnimations().forEach(function (a) { a.cancel(); });
        setActive(toSymptom);
        busy = false;
        if (done) done();
      };
    }
    function openSymptoms() { go(true, function () { (heading || search).focus({ preventScroll: true }); }); }
    function closeSymptoms() { closeList(); go(false, function () { openBtn.focus({ preventScroll: true }); }); }

    var levelNames = {};
    try { levelNames = JSON.parse(root.getAttribute('data-sym-levels') || '{}'); } catch (e) { levelNames = {}; }
    var index = symptoms.map(function (s, i) {
      return { i: i, name: norm(s.label), words: String(s.words || '').split(/[,،\n]+/).map(norm).filter(Boolean) };
    });
    var matches = [], active = -1;

    function rank(query) {
      var qn = norm(query), scored = [];
      if (!qn) return [];
      index.forEach(function (x) {
        var score = x.name.indexOf(qn) === 0 ? 0 : x.name.indexOf(qn) > 0 ? 1
          : x.words.some(function (w) { return w.indexOf(qn) === 0; }) ? 2
          : x.words.some(function (w) { return w.indexOf(qn) >= 0; }) ? 3 : -1;
        if (score >= 0) scored.push([score, x.i]);
      });
      scored.sort(function (a, b) { return a[0] - b[0] || a[1] - b[1]; });
      return scored.slice(0, MAX_SUGGESTIONS).map(function (x) { return x[1]; });
    }
    function closeList() {
      list.hidden = true;
      search.setAttribute('aria-expanded', 'false');
      search.removeAttribute('aria-activedescendant');
      active = -1;
    }
    function showList() {
      if (!search.value.trim()) { closeList(); return; }
      matches = rank(search.value);
      list.textContent = '';
      active = -1;
      if (!matches.length) {
        var none = document.createElement('li');
        none.className = 'px-3 py-2.5 text-sm text-slate-500 dark:text-slate-400';
        none.textContent = root.getAttribute('data-sym-none') || '';
        list.appendChild(none);
      }
      matches.forEach(function (si, k) {
        var s = symptoms[si], li = document.createElement('li');
        li.id = search.id + '-o' + k;
        li.setAttribute('role', 'option');
        li.setAttribute('aria-selected', 'false');
        li.className = 'flex cursor-pointer items-center justify-between gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold hover:bg-soft aria-selected:bg-brand-50 dark:hover:bg-white/5 dark:aria-selected:bg-white/10';
        var name = document.createElement('span');
        name.textContent = s.label;
        var tag = document.createElement('span');
        tag.className = 'shrink-0 rounded-full px-2 py-0.5 text-[.7rem] font-bold ' + (s.level === 'emergency' ? 'bg-emerg-50 text-emerg-600 dark:bg-emerg-500/15 dark:text-red-200' : 'bg-soft text-slate-600 dark:bg-white/10 dark:text-slate-300');
        tag.textContent = levelNames[s.level] || '';
        li.appendChild(name);
        li.appendChild(tag);
        li.addEventListener('mousedown', function (e) { e.preventDefault(); });   // keep focus in the input
        li.addEventListener('click', function () { choose(si); });
        list.appendChild(li);
      });
      list.hidden = false;
      search.setAttribute('aria-expanded', 'true');
      if (count) count.textContent = (root.getAttribute('data-sym-count') || '{0}').replace('{0}', String(matches.length));
    }
    function move(step) {
      if (list.hidden) showList();
      if (!matches.length) return;
      active = (active + step + matches.length) % matches.length;
      qa('[data-nav-list] [role="option"]').forEach(function (li, k) {
        li.setAttribute('aria-selected', k === active ? 'true' : 'false');
        if (k === active) { search.setAttribute('aria-activedescendant', li.id); li.scrollIntoView({ block: 'nearest' }); }
      });
    }

    function choose(si) {
      var s = symptoms[si], lv = levels[s.level];
      if (!s || !lv) return;
      var urgent = s.level === 'emergency';
      closeList();
      search.value = s.label;
      q('[data-nav-sym-name]').textContent = s.label;
      var badge = q('[data-nav-sym-badge]');
      badge.textContent = levelNames[s.level] || '';
      swapClasses(badge, urgent ? BADGE_NORMAL : BADGE_URGENT, urgent ? BADGE_URGENT : BADGE_NORMAL);
      setIcon(q('[data-nav-sym-icon]'), urgent, LEVEL_ICON[s.level] || 'arrow');
      q('[data-nav-sym-title]').textContent = lv.title;
      q('[data-nav-sym-text]').textContent = lv.text + (s.note ? ' ' + s.note : '');
      setCta(q('[data-nav-sym-cta]'), lv.cta, lv.href, urgent);
      if (near) { near.hidden = true; near.textContent = ''; }
      if (nearBtn) {
        nearBtn.hidden = !(lv.nearest && navigator.geolocation);
        nearBtn.setAttribute('data-type', lv.nearest || '');
      }
      qa('[data-sym]').forEach(function (b) { b.setAttribute('aria-pressed', Number(b.getAttribute('data-sym')) === si ? 'true' : 'false'); });
      if (browseTitle) browseTitle.hidden = false;
      browse.scrollTop = 0;
      showAnswer();
    }
    /* the answer appears above the symptom chips (which stay, to pick another): a short fade and rise the first time,
       a quick fade when it changes */
    function showAnswer() {
      var first = out.hidden;
      out.hidden = false;
      out.scrollTop = 0;
      if (!reduced && out.animate) {
        out.animate(first
          ? [{ opacity: 0, transform: 'translateY(8px)' }, { opacity: 1, transform: 'none' }]
          : [{ opacity: .4 }, { opacity: 1 }], { duration: first ? 240 : 160, easing: 'cubic-bezier(.2,.8,.2,1)' });
      }
      out.focus({ preventScroll: true });
    }

    function nearest(type) {
      near.hidden = false;
      near.textContent = '';
      var status = document.createElement('p');
      status.className = 'text-sm text-slate-500 dark:text-slate-400';
      status.textContent = root.getAttribute('data-near-locating') || '';
      near.appendChild(status);
      var culture = root.getAttribute('data-near-culture') || 'ar';
      navigator.geolocation.getCurrentPosition(function (pos) {
        var here = [pos.coords.latitude, pos.coords.longitude];
        var facilities = fetch('/api/facilities?type=' + encodeURIComponent(type) + '&culture=' + encodeURIComponent(culture)).then(function (r) { return r.ok ? r.json() : []; });
        var waits = type === 'emergency'
          ? fetch('/api/er-wait').then(function (r) { return r.ok ? r.json() : null; }).catch(function () { return null; })
          : Promise.resolve(null);
        Promise.all([facilities, waits]).then(function (res) {
          var minutes = {};
          if (res[1] && res[1].available) (res[1].items || []).forEach(function (w) { minutes[w.feedId] = w.minutes; });
          var top = (res[0] || []).filter(function (f) { return f.lat != null && f.lng != null; })
            .map(function (f) { return { f: f, d: km(here, [Number(f.lat), Number(f.lng)]) }; })
            .sort(function (a, b) { return a.d - b.d; }).slice(0, 3);
          near.textContent = '';
          top.forEach(function (x) { near.appendChild(row(x.f, x.d, x.f.erFeedId ? minutes[x.f.erFeedId] : undefined)); });
          allLink();
        }).catch(failed);
      }, failed, { timeout: 10000, maximumAge: 300000 });

      function failed() {
        near.textContent = '';
        var p = document.createElement('p');
        p.className = 'text-sm text-slate-500 dark:text-slate-400';
        p.textContent = root.getAttribute('data-near-failed') || '';
        near.appendChild(p);
        allLink();
      }
    }
    function row(f, d, wait) {
      var a = document.createElement('a');
      a.href = f.url;
      a.className = 'flex items-center gap-3 rounded-xl border border-slate-200 px-3 py-2.5 text-sm transition hover:border-brand-500 dark:border-white/10 dark:hover:border-brand-400';
      var body = document.createElement('span');
      body.className = 'min-w-0 flex-1';
      var name = document.createElement('b');
      name.className = 'block truncate';
      name.textContent = f.name;
      var meta = document.createElement('span');
      meta.className = 'block text-xs text-slate-500 dark:text-slate-400';
      meta.textContent = (root.getAttribute('data-near-km') || '{0}').replace('{0}', d < 10 ? d.toFixed(1) : String(Math.round(d))) + (f.city ? ' · ' + f.city : '');
      body.appendChild(name);
      body.appendChild(meta);
      a.appendChild(body);
      if (typeof wait === 'number') {
        var badge = document.createElement('span');
        badge.className = 'shrink-0 rounded-full bg-soft px-2 py-0.5 text-xs font-bold tabular-nums dark:bg-white/10';
        badge.textContent = wait + ' ' + (root.getAttribute('data-near-min') || '');
        a.appendChild(badge);
      }
      return a;
    }
    function allLink() {
      var href = root.getAttribute('data-near-all');
      if (!href) return;
      var a = document.createElement('a');
      a.href = href;
      a.className = 'link text-sm font-semibold';
      a.textContent = root.getAttribute('data-near-all-label') || '';
      near.appendChild(a);
    }

    openBtn.hidden = false;
    openBtn.addEventListener('click', openSymptoms);
    if (backBtn) backBtn.addEventListener('click', closeSymptoms);
    symView.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && list.hidden) { e.preventDefault(); closeSymptoms(); }
    });
    qa('[data-sym]').forEach(function (b) {
      b.addEventListener('click', function () { choose(Number(b.getAttribute('data-sym'))); });
    });
    search.addEventListener('input', showList);
    search.addEventListener('blur', function () { setTimeout(closeList, 120); });
    search.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); move(1); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
      else if (e.key === 'Enter') {
        if (!list.hidden && matches.length && (active >= 0 || matches.length === 1)) { e.preventDefault(); choose(matches[Math.max(active, 0)]); }
      }
      else if (e.key === 'Escape' && !list.hidden) { e.preventDefault(); e.stopPropagation(); closeList(); }
    });
    if (nearBtn && near) nearBtn.addEventListener('click', function () { nearBtn.hidden = true; nearest(nearBtn.getAttribute('data-type')); });
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-navigator]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
