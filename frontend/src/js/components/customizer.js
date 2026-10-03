/* Look customizer (site/_Customizer, only when Ehc:Customizer:Enabled). The form's values are written to the
   "ehc-customize" cookie in query-string form; the server checks every value against an allow-list (Site/Customizer.cs)
   and applies it to this browser only. Look options (classes on <html>) apply at once; theme, hero and section changes
   reload the page. That reload is made seamless: the "ehc-customize-open" cookie makes the server render the panel
   already open, this file runs synchronously at the end of <body> (render is blocked until it has, see master) to put
   the page back at the same scroll position, and customizer-vt.css crossfades old and new page where supported. */
(function () {
  'use strict';
  var form = document.querySelector('[data-customizer]');
  if (!form) return;
  var html = document.documentElement;
  var COOKIE = 'ehc-customize', OPEN = 'ehc-customize-open', REOPEN = 'ehc-customize-reopen';
  var BASE = { corners: 'soft', shadows: 'soft', density: 'normal', buttons: 'pill' };   // name: default value (no class)
  // the site's own look (Site settings › Look): a reviewer's choice is saved only when it differs from it
  var LOOKS = {}, site = {};
  try { site = JSON.parse(form.getAttribute('data-site-looks') || '{}') || {}; } catch (e) { /* none */ }
  Object.keys(BASE).forEach(function (n) { LOOKS[n] = /^[a-z]+$/.test(site[n] || '') ? site[n] : BASE[n]; });
  var list = form.querySelector('[data-cz-sections]');
  var session = {
    get: function (k) { try { return sessionStorage.getItem(k); } catch (e) { return null; } },
    set: function (k, v) { try { sessionStorage.setItem(k, v); } catch (e) { /* private mode */ } },
    remove: function (k) { try { sessionStorage.removeItem(k); } catch (e) { /* private mode */ } }
  };

  function value(name) {
    var el = form.querySelector('[name="' + name + '"]:checked');
    return el ? el.value : '';
  }
  function state() {
    var p = new URLSearchParams();
    ['theme', 'pattern', 'panel', 'side', 'frame', 'align'].forEach(function (n) { var v = value(n); if (v) p.set(n, v); });
    var rotate = form.querySelector('[name="rotate"]');
    if (rotate && !rotate.checked) p.set('rotate', '0');
    Object.keys(LOOKS).forEach(function (n) { var v = value(n); if (v && v !== LOOKS[n]) p.set(n, v); });
    if (list) {
      var items = [].slice.call(list.querySelectorAll('[data-key]'));
      var hidden = items.filter(function (li) { var c = li.querySelector('[name="show"]'); return c && !c.checked; });
      if (hidden.length) p.set('hide', hidden.map(function (li) { return li.getAttribute('data-key'); }).join('.'));
      if (list.hasAttribute('data-moved')) p.set('order', items.map(function (li) { return li.getAttribute('data-key'); }).join('.'));
    }
    return p.toString();
  }
  function save() {
    var v = state(), secure = location.protocol === 'https:' ? '; Secure' : '';
    document.cookie = COOKIE + '=' + (v ? encodeURIComponent(v) + '; max-age=31536000' : '; max-age=0') + '; path=/; SameSite=Lax' + secure;
  }
  function applyLooks() {
    Object.keys(LOOKS).forEach(function (n) {
      [].slice.call(html.classList).forEach(function (c) { if (c.indexOf('look-' + n + '-') === 0) html.classList.remove(c); });
      var v = value(n);
      if (v && v !== BASE[n] && /^[a-z]+$/.test(v)) html.classList.add('look-' + n + '-' + v);
    });
  }
  function reload(focus) {
    form.setAttribute('aria-busy', 'true');
    var busy = document.querySelector('[data-cz-busy]');
    if (busy) busy.classList.add('is-on');
    session.set(REOPEN, JSON.stringify({ y: window.scrollY, s: form.scrollTop, f: focus || '' }));
    document.cookie = OPEN + '=1; max-age=15; path=/; SameSite=Lax';
    // a same-URL "replace" navigation (not location.reload) so a cross-document view transition can run
    location.replace(location.pathname + location.search);
  }
  function selector(el) {
    if (!el || !el.name) return '';
    return el.type === 'radio' || el.type === 'checkbox' ? '[name="' + el.name + '"][value="' + el.value + '"]' : '[name="' + el.name + '"]';
  }

  // an existing "order" cookie means the list was already rearranged: keep sending it
  if (list && /(^|;\s*)ehc-customize=[^;]*order%3D/.test(document.cookie)) list.setAttribute('data-moved', '');

  form.addEventListener('change', function (e) {
    var name = e.target.name;
    save();
    if (Object.prototype.hasOwnProperty.call(LOOKS, name)) applyLooks(); else reload(selector(e.target));
  });
  if (list) {
    list.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-move]');
      if (!btn) return;
      var li = btn.closest('[data-key]'), step = parseInt(btn.getAttribute('data-move'), 10);
      var target = step < 0 ? li.previousElementSibling : li.nextElementSibling;
      if (!target) return;
      if (step < 0) list.insertBefore(li, target); else list.insertBefore(target, li);
      list.setAttribute('data-moved', '');
      save();
      reload('[data-key="' + li.getAttribute('data-key') + '"] [data-move="' + step + '"]');
    });
  }
  var reset = form.querySelector('[data-cz-reset]');
  if (reset) reset.addEventListener('click', function () {
    document.cookie = COOKIE + '=; max-age=0; path=/; SameSite=Lax';
    reload('[data-cz-reset]');
  });

  // after a reload caused by the panel (rendered open by the server): same page and panel scroll, focus back on the
  // control that was used. site.js adopts the open panel (focus trap, Esc) and focuses [data-cz-focus].
  document.cookie = OPEN + '=; max-age=0; path=/; SameSite=Lax';
  var back = session.get(REOPEN);
  session.remove(REOPEN);
  if (back && document.getElementById('customizer').hasAttribute('data-reopened')) {
    var pos = {}; try { pos = JSON.parse(back) || {}; } catch (e) { /* ignore */ }
    if (typeof pos.y === 'number') window.scrollTo({ top: pos.y, behavior: 'instant' });   // not the site's smooth scrolling
    if (typeof pos.s === 'number') form.scrollTop = pos.s;
    var target = null;
    try { target = pos.f ? form.querySelector(pos.f) : null; } catch (e) { /* stale selector */ }
    if (target) target.setAttribute('data-cz-focus', '');
  }
})();
