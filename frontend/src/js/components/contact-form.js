/* Contact form ([data-contact-form]): checks the fields in the browser (the same rules as Contact/ContactStore.cs, which
   checks them again), lists problems in a summary that takes focus and links to each field, posts JSON to /api/contact
   and shows the reference number. Fields the server rejects are marked the same way. The form stays hidden without
   this script (a plain submit would put the answers in the address bar). */
(function () {
  'use strict';
  var FIELDS = ['service', 'name', 'phone', 'email', 'message', 'consent'];

  function digits(s) {
    // Arabic-Indic (٠-٩) and Persian (۰-۹) digits count as digits
    return s.replace(/[٠-٩]/g, function (d) { return String(d.charCodeAt(0) - 0x0660); })
            .replace(/[۰-۹]/g, function (d) { return String(d.charCodeAt(0) - 0x06F0); });
  }
  function phoneOk(v) {
    var t = digits(v.trim()), s = '';
    for (var i = 0; i < t.length; i++) {
      var ch = t[i];
      if (ch >= '0' && ch <= '9') s += ch;
      else if (ch === '+' && s === '') s = '+';
      else if (' -.() '.indexOf(ch) < 0) return false;
    }
    if (s.indexOf('00') === 0) s = '+' + s.slice(2);
    if (s.indexOf('+966') === 0) s = '0' + s.slice(4);
    else if (s.indexOf('966') === 0 && s.length === 12) s = '0' + s.slice(3);
    else if (s.length === 9 && s[0] === '5') s = '0' + s;
    if (s[0] === '+') return s.length >= 9 && s.length <= 16 && s[1] !== '0';
    return /^0(5\d|1[1-7])\d{7}$/.test(s);
  }
  function value(form, name) {
    var el = form.elements[name];
    if (!el) return '';
    if (name === 'consent') return el.checked;
    if (name === 'service') { var c = form.querySelector('input[name="service"]:checked'); return c ? c.value : ''; }
    return el.value;
  }
  function check(form, name) {
    var v = value(form, name);
    switch (name) {
      case 'service': return !!v;
      case 'name': v = v.trim(); return v.length >= 2 && v.length <= 100 && /\p{L}/u.test(v);
      case 'email': v = v.trim(); return v.length <= 254 && /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v);
      case 'phone': return v.length <= 30 && phoneOk(v);
      case 'message': v = v.trim(); return v.length >= 10 && v.length <= 2000;
      case 'consent': return v === true;
    }
    return true;
  }

  function init(box) {
    if (box.hasAttribute('data-contact-ready')) return;
    box.setAttribute('data-contact-ready', '');
    var form = box.querySelector('[data-contact-step="form"]'), done = box.querySelector('[data-contact-step="done"]');
    if (!form || !done) return;
    var summary = form.querySelector('[data-contact-summary]'), list = form.querySelector('[data-contact-summary-list]');
    var fail = form.querySelector('[data-contact-fail]'), submit = form.querySelector('[data-contact-submit]');
    var submitText = form.querySelector('[data-contact-submit-text]'), sendLabel = submitText ? submitText.textContent : '';
    var counter = form.querySelector('[data-contact-counter]'), message = form.elements.message;
    var busy = false;
    form.hidden = false;

    function inputs(name) { return [].slice.call(form.querySelectorAll('[data-contact-field="' + name + '"]')); }
    function errorOf(name) { return form.querySelector('[data-contact-error="' + name + '"]'); }
    function mark(name, bad) {
      var err = errorOf(name);
      if (err) err.hidden = !bad;
      inputs(name).forEach(function (el) { if (bad) el.setAttribute('aria-invalid', 'true'); else el.removeAttribute('aria-invalid'); });
    }
    function showSummary(names) {
      list.textContent = '';
      names.forEach(function (name) {
        var err = errorOf(name), target = inputs(name)[0];
        if (!err || !target) return;
        var li = document.createElement('li'), a = document.createElement('a');
        a.href = '#' + target.id;
        a.className = 'link text-emerg-600 dark:text-red-200';
        a.textContent = err.textContent;
        a.addEventListener('click', function (e) { e.preventDefault(); target.focus(); });
        li.appendChild(a);
        list.appendChild(li);
      });
      summary.hidden = false;
      summary.focus();
    }
    function invalid(names) {
      FIELDS.forEach(function (n) { mark(n, names.indexOf(n) >= 0); });
      showSummary(names);
    }
    function updateCounter() {
      if (!counter || !message) return;
      var left = Number(counter.getAttribute('data-max')) - message.value.length;
      counter.textContent = (box.getAttribute('data-counter') || '{0}').replace('{0}', String(left));
    }
    function setBusy(on) {
      busy = on;
      submit.disabled = on;
      submit.setAttribute('aria-busy', on ? 'true' : 'false');
      if (submitText) submitText.textContent = on ? box.getAttribute('data-sending') : sendLabel;
    }

    // a field's error clears as soon as it is fixed
    FIELDS.forEach(function (name) {
      inputs(name).forEach(function (el) {
        el.addEventListener(el.type === 'radio' || el.type === 'checkbox' ? 'change' : 'input', function () {
          if (el.getAttribute('aria-invalid') === 'true' && check(form, name)) mark(name, false);
        });
      });
    });
    if (message) message.addEventListener('input', updateCounter);
    updateCounter();

    form.addEventListener('submit', function (e) {
      e.preventDefault();
      if (busy) return;
      fail.hidden = true;
      var bad = FIELDS.filter(function (n) { return !check(form, n); });
      if (bad.length) { invalid(bad); return; }
      summary.hidden = true;
      setBusy(true);
      fetch('/api/contact', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'omit',
        body: JSON.stringify({
          pageKey: box.getAttribute('data-page'), culture: box.getAttribute('data-culture'),
          service: value(form, 'service'), name: value(form, 'name').trim(), email: value(form, 'email').trim(),
          phone: value(form, 'phone').trim(), message: value(form, 'message'), consent: value(form, 'consent'),
          website: form.elements.website ? form.elements.website.value : ''
        })
      }).then(function (r) {
        if (r.ok) return r.json().then(success);
        if (r.status === 400) {
          return r.json().then(function (body) {
            var names = body && body.errors ? Object.keys(body.errors).filter(function (n) { return FIELDS.indexOf(n) >= 0; }) : [];
            if (names.length) invalid(names); else throw new Error('400');
          });
        }
        throw new Error(String(r.status));
      }).catch(function (err) {
        fail.textContent = box.getAttribute(err && err.message === '429' ? 'data-error-busy' : 'data-error-send');
        fail.hidden = false;
      }).then(function () { setBusy(false); });
    });

    function success(body) {
      box.querySelector('[data-contact-reference]').textContent = body.reference || '';
      form.hidden = true;
      done.hidden = false;
      // the card shrinks: bring its top back into view, below the sticky header (scroll-margin on the card)
      if (box.getBoundingClientRect().top < 120) box.scrollIntoView({ block: 'start', behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' });
      box.querySelector('[data-contact-done-title]').focus({ preventScroll: true });
    }

    var copy = done.querySelector('[data-contact-copy]');
    if (copy && navigator.clipboard) {
      copy.hidden = false;
      var copyLabel = copy.textContent;
      copy.addEventListener('click', function () {
        navigator.clipboard.writeText(box.querySelector('[data-contact-reference]').textContent).then(function () {
          copy.textContent = box.getAttribute('data-copied');
          setTimeout(function () { copy.textContent = copyLabel; }, 2000);
        });
      });
    }

    done.querySelector('[data-contact-again]').addEventListener('click', function () {
      form.reset();
      FIELDS.forEach(function (n) { mark(n, false); });
      summary.hidden = true;
      updateCounter();
      done.hidden = true;
      form.hidden = false;
      var first = form.querySelector('input[name="service"]');
      if (first) first.focus();
    });
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-contact-form]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
