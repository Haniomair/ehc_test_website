/* Component library page: turns the full-size list into a grid of live, scaled-down previews with search and category
   filters. Selecting a tile moves that section into a full-screen <dialog> (so it keeps its state and behaviour);
   closing moves it back and returns focus. Previews are inert so their links and buttons are not tabbable. */
(function () {
  'use strict';
  var root = document.querySelector('[data-library]');
  if (!root) return;
  var script = document.currentScript || document.querySelector('script[data-count-many]');
  var tiles = [].slice.call(root.querySelectorAll('.lib-tile'));
  var live = tiles.filter(function (t) { return t.querySelector('[data-library-stage]'); });
  var toolbar = root.querySelector('[data-library-toolbar]');
  var search = root.querySelector('[data-library-search]');
  var chips = [].slice.call(root.querySelectorAll('[data-library-cat]'));
  var count = root.querySelector('[data-library-count]');
  var empty = root.querySelector('[data-library-empty]');
  var dlg = document.querySelector('[data-library-dialog]');
  var body = dlg && dlg.querySelector('[data-library-dialog-body]');
  var header = document.getElementById('hdr');
  var category = '', current = -1, opener = null;

  root.classList.add('lib-js');
  if (toolbar) toolbar.hidden = false;
  if (header) root.style.setProperty('--header-h', header.offsetHeight + 'px');
  live.forEach(function (t) {
    t.querySelector('[data-library-stage]').inert = true;
    var btn = t.querySelector('[data-library-open]');
    if (btn) { btn.hidden = false; btn.addEventListener('click', function () { open(live.indexOf(t), btn); }); }
  });

  function label(n) {
    var one = script && script.getAttribute('data-count-one'), many = script && script.getAttribute('data-count-many');
    return n === 1 ? (one || '1') : (many || '{0}').replace('{0}', n);
  }
  function filter() {
    var q = (search && search.value || '').trim().toLowerCase(), shown = 0;
    tiles.forEach(function (t) {
      var ok = (!category || t.getAttribute('data-cat') === category) && (!q || (t.getAttribute('data-text') || '').indexOf(q) >= 0);
      t.hidden = !ok;
      if (ok) shown++;
    });
    if (count) count.textContent = label(shown);
    if (empty) empty.classList.toggle('hidden', shown > 0);
  }
  chips.forEach(function (c) {
    c.addEventListener('click', function () {
      category = c.getAttribute('data-library-cat') || '';
      chips.forEach(function (x) { x.setAttribute('aria-pressed', x === c ? 'true' : 'false'); });
      filter();
    });
  });
  if (search) search.addEventListener('input', filter);
  filter();

  // ---------- full-size viewer ----------
  if (!dlg || typeof dlg.showModal !== 'function') return;
  var title = dlg.querySelector('[data-library-dialog-title]'), cat = dlg.querySelector('[data-library-dialog-cat]');
  function stageOf(i) { return live[i].querySelector('[data-library-stage]'); }
  function park() {
    if (current < 0) return;
    var stage = stageOf(current);
    while (body.firstChild) stage.appendChild(body.firstChild);
  }
  function show(i) {
    park();
    current = (i + live.length) % live.length;
    var t = live[current], stage = stageOf(current);
    while (stage.firstChild) body.appendChild(stage.firstChild);
    var h = t.querySelector('.lib-meta h2'), sub = t.querySelector('[data-library-sub]'), c = t.querySelector('.lib-meta span');
    title.textContent = (h ? h.textContent : '') + (sub ? ' — ' + sub.textContent : '');
    cat.textContent = c ? c.textContent : '';
    body.scrollTop = 0;
    // sections measure themselves (maps, rails): let them know the size changed
    window.dispatchEvent(new Event('resize'));
    [].forEach.call(body.querySelectorAll('.rv'), function (e) { e.classList.add('in'); });
  }
  function open(i, from) {
    opener = from;
    show(i);
    dlg.showModal();
    dlg.querySelector('[data-library-close]').focus();
  }
  dlg.querySelector('[data-library-prev]').addEventListener('click', function () { show(current - 1); });
  dlg.querySelector('[data-library-next]').addEventListener('click', function () { show(current + 1); });
  dlg.querySelector('[data-library-close]').addEventListener('click', function () { dlg.close(); });
  dlg.addEventListener('close', function () {
    park();
    current = -1;
    window.dispatchEvent(new Event('resize'));
    // after the browser's own focus restore
    if (opener) setTimeout(function () { opener.focus(); }, 0);
  });
})();
