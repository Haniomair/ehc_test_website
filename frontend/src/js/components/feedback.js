/* "Was this page helpful?" ([data-feedback]): Yes sends immediately; No shows reasons + comment, then sends.
   Posts JSON to /api/feedback. The browser remembers an answer per page for the day (localStorage, never sent),
   so the question is not shown again straight away. Focus follows each step for keyboard and screen-reader users. */
(function () {
  'use strict';
  function today() { return new Date().toISOString().slice(0, 10); }
  function store(key, value) {
    try { if (value === undefined) return localStorage.getItem(key); localStorage.setItem(key, value); } catch (e) { return null; }
    return null;
  }
  function init(box) {
    if (box.hasAttribute('data-feedback-ready')) return;
    box.setAttribute('data-feedback-ready', '');
    var page = box.getAttribute('data-page'), key = 'ehc-fb-' + page;
    if (store(key) === today()) return;     // answered today: keep hidden
    var ask = box.querySelector('[data-feedback-step="ask"]'), form = box.querySelector('[data-feedback-step="details"]');
    var done = box.querySelector('[data-feedback-step="done"]'), error = box.querySelector('[data-feedback-error]');
    if (!ask || !form || !done) return;
    box.hidden = false;

    function send(helpful, reason, comment, website) {
      error.hidden = true;
      return fetch('/api/feedback', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'omit',
        body: JSON.stringify({ pageKey: page, culture: box.getAttribute('data-culture'), helpful: helpful, reason: reason || null, comment: comment || null, website: website || null })
      }).then(function (r) {
        if (!r.ok && r.status !== 204) throw new Error(String(r.status));
        store(key, today());
        ask.hidden = true; form.hidden = true; done.hidden = false;
        done.focus();
      }).catch(function () { error.hidden = false; });
    }
    box.querySelector('[data-feedback-answer="yes"]').addEventListener('click', function () { send(true); });
    box.querySelector('[data-feedback-answer="no"]').addEventListener('click', function () {
      ask.hidden = true; form.hidden = false;
      var first = form.querySelector('input[name="reason"]');
      if (first) first.focus();
    });
    form.addEventListener('submit', function (e) {
      e.preventDefault();
      var r = form.querySelector('input[name="reason"]:checked');
      send(false, r && r.value, form.comment.value, form.website.value);
    });
    box.querySelector('[data-feedback-skip]').addEventListener('click', function () { send(false); });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-feedback]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
