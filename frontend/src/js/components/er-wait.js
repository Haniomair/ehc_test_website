/* ER wait times ([data-er]): fetches /api/er-wait, fills cards whose data-er-feed matches, refreshes every 60 s.
   No data → the "coming soon" note stays and no numbers are shown. Illustrative (demo) data is labelled. */
(function () {
  'use strict';
  var LEVEL = {
    quiet: { bar: 'bg-emerald-500', badge: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300' },
    moderate: { bar: 'bg-accent-400', badge: 'bg-amber-50 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300' },
    busy: { bar: 'bg-highlight-500', badge: 'bg-rose-50 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300' }
  };
  function init(root) {
    if (root.hasAttribute('data-er-ready')) return;
    root.setAttribute('data-er-ready', '');
    var labels = {}; try { labels = JSON.parse(root.getAttribute('data-labels') || '{}'); } catch (e) { /* */ }
    var q = function (s, el) { return (el || root).querySelector(s); };
    var soon = q('[data-er-soon]'), status = q('[data-er-status]'), illus = q('[data-er-illustrative]');
    function show(el, on, cls) { if (!el) return; el.classList.toggle('hidden', !on); if (cls) el.classList.toggle(cls, on); }
    function apply(data) {
      var byId = {};
      (data && data.available ? data.items || [] : []).forEach(function (i) { byId[i.feedId] = i; });
      var any = false;
      [].forEach.call(root.querySelectorAll('[data-er-feed]'), function (card) {
        var d = byId[card.getAttribute('data-er-feed')], lv = d && LEVEL[d.level];
        show(q('[data-er-value]', card), !!lv, 'flex');
        show(q('[data-er-track]', card), !!lv);
        var badge = q('[data-er-badge]', card);
        show(badge, !!lv);
        if (!lv) return;
        any = true;
        q('[data-er-min]', card).textContent = d.minutes;
        badge.className = 'whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-bold ' + lv.badge;
        badge.textContent = labels[d.level] || d.level;
        var bar = q('[data-er-bar]', card);
        if (!bar) return;  // compact rows (facility finder): no bar
        bar.className = 'block h-full rounded-full transition-all duration-700 ' + lv.bar;
        bar.style.width = Math.min(100, d.minutes / 90 * 100) + '%';
      });
      show(soon, !any);
      show(status, any, 'inline-flex');
      show(illus, any && !!(data && data.illustrative));
    }
    function load() {
      fetch(root.getAttribute('data-api') || '/api/er-wait', { headers: { 'Accept': 'application/json' } })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(apply)
        .catch(function () { apply(null); });
    }
    load();
    setInterval(function () { if (!document.hidden) load(); }, 60000);
  }
  // [data-api] too: facility-finder rows also carry data-er ("0"/"1" = has an emergency department)
  function all() { [].forEach.call(document.querySelectorAll('[data-er][data-api]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
