/* A–Z index ([data-az]): shows the name filter and hides entries, groups and letters that don't match.
   Arabic matching ignores diacritics, hamza forms of alef, taa marbuta / haa and alef maqsura / yaa. */
(function () {
  'use strict';
  function norm(s) {
    return (s || '').toLowerCase()
      .replace(/[ً-ْـ]/g, '')
      .replace(/[آأإٱ]/g, 'ا')
      .replace(/ة/g, 'ه')
      .replace(/ى/g, 'ي');
  }
  function setup(root) {
    if (root.hasAttribute('data-az-ready')) return;
    root.setAttribute('data-az-ready', '');
    var box = root.querySelector('[data-az-search]'), input = root.querySelector('[data-az-filter]');
    if (!box || !input) return;
    box.hidden = false;
    var items = [].slice.call(root.querySelectorAll('[data-az-item]'));
    items.forEach(function (li) { li.setAttribute('data-text', norm(li.textContent)); });
    var groups = [].slice.call(root.querySelectorAll('[data-az-group]'));
    var none = root.querySelector('[data-az-none]'), status = root.querySelector('[data-az-status]');
    var timer;
    function apply() {
      var q = norm(input.value.trim()), shown = 0;
      items.forEach(function (li) { var on = !q || li.getAttribute('data-text').indexOf(q) >= 0; li.hidden = !on; if (on) shown++; });
      groups.forEach(function (g) {
        var any = !!g.querySelector('[data-az-item]:not([hidden])');
        g.hidden = !any;
        var link = root.querySelector('a[href="#' + g.id + '"]');
        if (link) link.classList.toggle('az-off', !any);
      });
      if (none) none.hidden = shown > 0;
      if (status) status.textContent = q ? (status.getAttribute('data-format') || '{0}').replace('{0}', shown) : '';
    }
    input.addEventListener('input', function () { clearTimeout(timer); timer = setTimeout(apply, 120); });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-az]'), setup); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
