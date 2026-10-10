/* Health library home.
   - Body map ([data-body-map]): a point on the figure or a body-system chip ([data-system]) shows that system's panel
     ([data-system-panel]) instead of following its link (the filtered conditions list, used without JS). The chosen
     point and chip carry aria-current; the panel area is a polite live region.
   - Search ([data-lib-search]): suggestions from the page's index (names and "also known as" words; Arabic letter forms
     and diacritics ignored), combobox pattern: Arrow keys move, Enter opens the highlighted page or submits the form,
     Escape closes. */
(function () {
  'use strict';
  var reduced = document.documentElement.classList.contains('a11y-still') || (window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches);

  function norm(s) {
    return String(s || '').toLowerCase().trim()
      .replace(/[ً-ْـ]/g, '')
      .replace(/[آأإٱ]/g, 'ا').replace(/ة/g, 'ه').replace(/ى/g, 'ي')
      .replace(/\s+/g, ' ');
  }

  function bodyMap(root) {
    var triggers = [].slice.call(root.querySelectorAll('[data-system]'));
    var panels = [].slice.call(root.querySelectorAll('[data-system-panel]'));
    if (!triggers.length || !panels.length) return;
    var current = panels.filter(function (p) { return !p.hidden; })[0] || null;
    function show(key, focus) {
      var panel = panels.filter(function (p) { return p.getAttribute('data-system-panel') === key; })[0];
      if (!panel || panel === current) return;
      current = panel;
      triggers.forEach(function (t) { if (t.getAttribute('data-system') === key) t.setAttribute('aria-current', 'true'); else t.removeAttribute('aria-current'); });
      panels.forEach(function (p) { p.hidden = p !== panel; });
      // the card stays put: only its content fades in, briefly, so switching quickly between systems stays calm
      if (!reduced && panel.animate) panel.animate([{ opacity: 0.25 }, { opacity: 1 }], { duration: 160, easing: 'ease-out' });
      if (focus) {
        // on small screens the panel is below the figure: bring it into view
        var r = panel.getBoundingClientRect();
        if (r.top > window.innerHeight - 80) panel.scrollIntoView({ behavior: reduced ? 'auto' : 'smooth', block: 'nearest' });
      }
    }
    triggers.forEach(function (t) {
      t.addEventListener('click', function (e) {
        if (e.ctrlKey || e.metaKey || e.shiftKey) return;   // open the list in a new tab as usual
        e.preventDefault();
        show(t.getAttribute('data-system'), true);
      });
    });
  }

  function search(form) {
    var input = form.querySelector('[data-lib-q]'), list = form.querySelector('[data-lib-list]'), data = form.querySelector('[data-lib-index]');
    if (!input || !list || !data) return;
    var index = [];
    try { index = JSON.parse(data.textContent || '[]'); } catch (e) { return; }
    index.forEach(function (x) { x.nn = norm(x.n); x.na = norm(x.a); });
    var status = document.querySelector('[data-lib-count]');
    var matches = [], active = -1;

    function close() {
      list.hidden = true;
      input.setAttribute('aria-expanded', 'false');
      input.removeAttribute('aria-activedescendant');
      active = -1;
    }
    function rank(q) {
      var bare = q.length > 3 && q.indexOf('ال') === 0 ? q.slice(2) : q, out = [];
      index.forEach(function (x, i) {
        var s = x.nn.indexOf(q) === 0 || x.nn.indexOf(' ' + q) >= 0 ? 0 : x.nn.indexOf(bare) >= 0 ? 1 : x.na.indexOf(bare) >= 0 ? 2 : -1;
        if (s >= 0) out.push([s, i]);
      });
      out.sort(function (a, b) { return a[0] - b[0] || index[a[1]].n.localeCompare(index[b[1]].n); });
      return out.slice(0, 8).map(function (x) { return index[x[1]]; });
    }
    function render() {
      var q = norm(input.value);
      if (q.length < 2) { close(); return; }
      matches = rank(q);
      list.textContent = '';
      active = -1;
      if (!matches.length) { close(); return; }
      matches.forEach(function (m, k) {
        var li = document.createElement('li');
        li.id = input.id + '-o' + k;
        li.setAttribute('role', 'option');
        li.setAttribute('aria-selected', 'false');
        li.className = 'flex cursor-pointer items-center justify-between gap-3 rounded-xl px-3 py-2.5 text-sm text-deep-950 hover:bg-soft aria-selected:bg-brand-50 dark:text-white dark:hover:bg-white/5 dark:aria-selected:bg-white/10';
        var name = document.createElement('span');
        name.className = 'font-semibold';
        name.textContent = m.n;
        var tag = document.createElement('span');
        tag.className = 'shrink-0 rounded-full bg-soft px-2 py-0.5 text-[.7rem] font-bold text-slate-600 dark:bg-white/10 dark:text-slate-300';
        tag.textContent = m.k;
        li.appendChild(name);
        li.appendChild(tag);
        li.addEventListener('mousedown', function (e) { e.preventDefault(); });
        li.addEventListener('click', function () { location.href = m.u; });
        list.appendChild(li);
      });
      list.hidden = false;
      input.setAttribute('aria-expanded', 'true');
      if (status) status.textContent = (status.getAttribute('data-format') || '{0}').replace('{0}', String(matches.length));
    }
    function move(step) {
      if (list.hidden) render();
      if (!matches.length) return;
      active = (active + step + matches.length) % matches.length;
      [].forEach.call(list.children, function (li, k) {
        li.setAttribute('aria-selected', k === active ? 'true' : 'false');
        if (k === active) { input.setAttribute('aria-activedescendant', li.id); li.scrollIntoView({ block: 'nearest' }); }
      });
    }
    input.addEventListener('input', render);
    input.addEventListener('blur', function () { setTimeout(close, 120); });
    input.addEventListener('keydown', function (e) {
      if (e.key === 'ArrowDown') { e.preventDefault(); move(1); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); move(-1); }
      else if (e.key === 'Enter' && !list.hidden && active >= 0) { e.preventDefault(); location.href = matches[active].u; }
      else if (e.key === 'Escape' && !list.hidden) { e.preventDefault(); close(); }
    });
  }

  function all() {
    [].forEach.call(document.querySelectorAll('[data-body-map]'), function (m) {
      if (m.hasAttribute('data-ready')) return;
      m.setAttribute('data-ready', '');
      bodyMap(m);
    });
    [].forEach.call(document.querySelectorAll('[data-lib-search]'), function (f) {
      if (f.hasAttribute('data-ready')) return;
      f.setAttribute('data-ready', '');
      search(f);
    });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
