/* Care navigator ([data-navigator]): options come from data-options (JSON written by the Razor partial), never from this file.
   Selecting "who" and "need" updates the result; urgent needs switch the result to emergency styling. Safe to load twice. */
(function () {
  'use strict';
  var URGENT = ['bg-emerg-50', 'text-emerg-600', 'dark:bg-emerg-500/15', 'dark:text-red-300'];
  var NORMAL = ['bg-brand-50', 'text-brand-500', 'dark:bg-brand-500/15', 'dark:text-brand-300'];

  function init(root) {
    if (root.hasAttribute('data-navigator-ready')) return;
    root.setAttribute('data-navigator-ready', '');
    var data;
    try { data = JSON.parse(root.getAttribute('data-options') || '{}'); } catch (e) { return; }
    var needs = data.needs || [], who = data.who || [];
    if (!needs.length) return;

    var q = function (s) { return root.querySelector(s); };
    var title = q('[data-nav-title]'), text = q('[data-nav-text]'), cta = q('[data-nav-cta]'), icon = q('[data-nav-icon]');
    var whoBtns = [].slice.call(root.querySelectorAll('[data-who]')), needBtns = [].slice.call(root.querySelectorAll('[data-need]'));
    var cur = { who: who.length ? who[0].key : null, need: (needs.filter(function (n) { return !n.urgent; })[0] || needs[0]).key };

    function find(list, key) { for (var i = 0; i < list.length; i++) if (list[i].key === key) return list[i]; return null; }
    function render() {
      var n = find(needs, cur.need), w = find(who, cur.who);
      if (!n) return;
      if (title) title.textContent = n.title;
      if (text) text.textContent = n.text + (w && w.note && !n.urgent ? ' ' + w.note : '');
      if (cta) {
        cta.textContent = n.cta;
        cta.setAttribute('href', n.href || '#');
        cta.classList.toggle('hidden', !n.cta);
        cta.classList.toggle('!bg-emerg-600', !!n.urgent);
        cta.classList.toggle('!shadow-none', !!n.urgent);
      }
      if (icon) {
        (n.urgent ? NORMAL : URGENT).forEach(function (c) { icon.classList.remove(c); });
        (n.urgent ? URGENT : NORMAL).forEach(function (c) { icon.classList.add(c); });
        var use = icon.querySelector('use');
        if (use && /^[a-z0-9-]+$/.test(n.icon || '')) use.setAttribute('href', '#i-' + n.icon);
      }
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
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-navigator]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
