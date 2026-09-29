/* Health tools ([data-tool] > form[data-calc]): ideal weight, daily calories, ovulation, due date and the
   self-assessments (prediabetes risk, asthma control). Everything is calculated here, in the browser; nothing is sent
   or stored. Texts come from data-* attributes (dictionary items), never from this file. (BMI: components/bmi.js.) Formulas:
   - ideal weight (Devine): 50 kg (men) / 45.5 kg (women) + 2.3 kg per inch over 5 ft; healthy range = BMI 18.5–24.9
   - calories (Mifflin–St Jeor): 10·kg + 6.25·cm − 5·age + 5 (men) / − 161 (women), × activity; ±500 kcal a day
   - ovulation ≈ next period − 14 days; fertile window = ovulation − 5 … ovulation + 1
   - due date (Naegele) = last period + 280 days + (cycle − 28) */
(function () {
  'use strict';
  var DAY = 864e5;
  var lang = document.documentElement.lang || 'ar';
  var num = function (d) { return new Intl.NumberFormat(lang + '-u-nu-latn', { maximumFractionDigits: d, minimumFractionDigits: d }); };
  var n0 = num(0), n1 = num(1);
  var greg = new Intl.DateTimeFormat(lang + '-u-ca-gregory-nu-latn', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC' });
  var gregShort = new Intl.DateTimeFormat(lang + '-u-ca-gregory-nu-latn', { day: 'numeric', month: 'long', timeZone: 'UTC' });
  var hijriParts;
  try { hijriParts = new Intl.DateTimeFormat('en-u-ca-islamic-umalqura-nu-latn', { day: 'numeric', month: 'numeric', year: 'numeric', timeZone: 'UTC' }); } catch (e) { hijriParts = null; }

  function fill(s, args) { return String(s || '').replace(/\{(\d)\}/g, function (_, i) { return args[+i]; }); }
  function today() { var d = new Date(); return Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()); }
  function parseDate(v) { var m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v || ''); return m ? Date.UTC(+m[1], +m[2] - 1, +m[3]) : NaN; }

  function tone(el, t) { if (el) el.setAttribute('data-tone', t || 'info'); }

  function setup(tool) {
    var form = tool.querySelector('form[data-calc]');
    if (!form || form.hasAttribute('data-ready')) return;
    form.setAttribute('data-ready', '');
    var kind = form.getAttribute('data-calc');
    var err = form.querySelector('[data-tool-error]');
    var result = form.querySelector('[data-tool-result]');
    var out = form.querySelector('[data-out]');
    var empty = form.querySelector('[data-empty]');
    var months = [];
    try { months = JSON.parse(tool.getAttribute('data-hijri-months') || '[]'); } catch (e) { months = []; }
    var suffix = tool.getAttribute('data-hijri-suffix') || '';

    function v(name) { return form.querySelector('[data-v="' + name + '"]'); }
    function set(name, text) { var el = v(name); if (el) el.textContent = text; }
    function field(name) { return form.elements[name]; }
    function value(name) { var el = field(name); return el ? el.value : ''; }
    function number(name) {
      var el = field(name);
      if (!el || el.value === '') return NaN;
      var x = parseFloat(el.value), min = parseFloat(el.min), max = parseFloat(el.max);
      if (isNaN(x) || (!isNaN(min) && x < min) || (!isNaN(max) && x > max)) return NaN;
      return x;
    }
    function hijri(t) {
      if (!hijriParts || months.length !== 12) return '';
      var p = {};
      hijriParts.formatToParts(new Date(t)).forEach(function (x) { p[x.type] = x.value; });
      var m = months[(+p.month) - 1];
      return m ? p.day + ' ' + m + ' ' + parseInt(p.year, 10) + suffix : '';
    }
    function range(a, b) { return fill(form.getAttribute('data-fmt-range') || '{0} – {1}', [a, b]); }

    function fail(on) { if (err) err.classList.toggle('hidden', !on); }
    function show() {
      fail(false);
      if (empty) empty.hidden = true;
      if (out) out.hidden = false;
      if (result) result.hidden = false;
    }
    function clear() {
      fail(false);
      if (empty) empty.hidden = false;
      if (out && empty) out.hidden = true;
      if (result && !empty) result.hidden = true;
      [].forEach.call(form.querySelectorAll('[data-only-if]'), function (q) { q.hidden = true; });
    }

    // no future dates for "first day of the last period"
    [].forEach.call(form.querySelectorAll('[data-max-today]'), function (el) { el.max = new Date(today()).toISOString().slice(0, 10); });

    var calc = {
      idealWeight: function () {
        var sex = value('sex'), h = number('height');
        if (!sex || isNaN(h)) return false;
        var m = h / 100, inches = h / 2.54 - 60, devine = inches >= 0;
        var ibw = (sex === 'male' ? 50 : 45.5) + 2.3 * inches;
        var box = v('ibwBox'), short = v('short');
        if (box) box.hidden = !devine;
        if (short) short.classList.toggle('hidden', devine);
        set('ibw', n1.format(ibw));
        set('range', n0.format(18.5 * m * m) + ' – ' + n0.format(24.9 * m * m));
        return true;
      },
      calories: function () {
        var sex = value('sex'), age = number('age'), h = number('height'), w = number('weight'), act = parseFloat(value('activity'));
        if (!sex || isNaN(age) || isNaN(h) || isNaN(w) || isNaN(act)) return false;
        var bmr = 10 * w + 6.25 * h - 5 * age + (sex === 'male' ? 5 : -161);
        var keep = Math.round(bmr * act / 10) * 10, floor = sex === 'male' ? 1500 : 1200;
        set('maintain', n0.format(keep));
        set('lose', n0.format(Math.max(floor, keep - 500)));
        set('gain', n0.format(keep + 500));
        return true;
      },
      ovulation: function () {
        var lmp = parseDate(value('lmp')), cycle = number('cycle'), now = today();
        if (isNaN(lmp) || isNaN(cycle) || lmp > now || now - lmp > 366 * DAY) return false;
        // first cycle whose fertile window has not ended yet
        var start = lmp;
        while (start + (cycle - 14 + 1) * DAY < now) start += cycle * DAY;
        var list = v('cycles');
        if (list) list.textContent = '';
        for (var i = 0; i < 3; i++) {
          var s = start + i * cycle * DAY, ov = s + (cycle - 14) * DAY, a = ov - 5 * DAY, b = ov + DAY;
          if (i === 0) {
            set('window', range(gregShort.format(a), greg.format(b)));
            var ha = hijri(a), hb = hijri(b);
            set('windowHijri', ha && hb ? range(ha, hb) : '');
            set('ovulation', greg.format(ov));
            set('period', greg.format(s + cycle * DAY));
          } else if (list) {
            var li = document.createElement('li');
            li.className = 'rounded-xl bg-white px-4 py-3 dark:bg-white/5';
            li.textContent = range(gregShort.format(a), greg.format(b));
            list.appendChild(li);
          }
        }
        return true;
      },
      dueDate: function () {
        var lmp = parseDate(value('lmp')), cycle = number('cycle'), now = today();
        if (isNaN(lmp) || isNaN(cycle) || lmp > now) return false;
        var shift = (cycle - 28) * DAY, due = lmp + 280 * DAY + shift;
        var days = Math.floor((now - lmp - shift) / DAY);
        if (days > 44 * 7) return false;
        set('due', greg.format(due));
        set('dueHijri', hijri(due));
        days = Math.max(0, days);
        var weeks = Math.floor(days / 7);
        set('age', fill(form.getAttribute('data-fmt-age'), [weeks, days % 7]));
        var tri; try { tri = JSON.parse(form.getAttribute('data-trimesters') || '[]'); } catch (e) { tri = []; }
        set('trimester', tri[weeks < 13 ? 0 : weeks < 27 ? 1 : 2] || '');
        var p = v('progress'), bar = v('bar'), pct = Math.min(100, days / 280 * 100);
        if (p) { p.setAttribute('aria-valuenow', String(Math.min(40, weeks))); p.setAttribute('aria-valuetext', v('age') ? v('age').textContent : ''); }
        if (bar) bar.style.width = pct + '%';
        return true;
      },
      questionnaire: function () {
        var total = 0, ok = true;
        [].forEach.call(form.querySelectorAll('[data-question]'), function (q) {
          if (q.hidden) return;
          var key = q.getAttribute('data-question');
          if (key === 'weight') {
            var h = number('height'), w = number('weight');
            if (isNaN(h) || isNaN(w)) { ok = false; return; }
            var b = w / Math.pow(h / 100, 2);
            total += b < 25 ? 0 : b < 30 ? 1 : b < 40 ? 2 : 3;
            return;
          }
          var picked = q.querySelector('input:checked');
          if (!picked) { ok = false; return; }
          total += parseInt(picked.value, 10) || 0;
        });
        if (!ok) return false;
        var bands; try { bands = JSON.parse(form.getAttribute('data-bands') || '[]'); } catch (e) { bands = []; }
        var band = null;
        for (var i = 0; i < bands.length; i++) if (total >= bands[i][0]) { band = bands[i]; break; }
        set('score', fill(form.getAttribute('data-score-format') || '{0} / {1}', [total, form.getAttribute('data-max')]));
        if (band) { set('band', band[2]); tone(v('band'), band[1]); set('text', band[3]); }
        return true;
      }
    };

    // questions that apply only for a given answer (e.g. gestational diabetes: women only)
    function conditions() {
      [].forEach.call(form.querySelectorAll('[data-only-if]'), function (q) {
        var rule = (q.getAttribute('data-only-if') || '').split(':');
        var picked = form.querySelector('[data-question="' + rule[0] + '"] input:checked');
        var on = !!picked && picked.getAttribute('data-option') === rule[1];
        q.hidden = !on;
        if (!on) [].forEach.call(q.querySelectorAll('input'), function (i) { i.checked = false; });
      });
    }
    form.addEventListener('change', conditions);

    form.addEventListener('submit', function (e) {
      e.preventDefault();
      var fn = calc[kind];
      if (fn && fn()) {
        show();
        if (result && result.scrollIntoView && result.getBoundingClientRect().top > window.innerHeight) {
          result.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'center' });
        }
      } else {
        fail(true);
      }
    });
    form.addEventListener('reset', function () { setTimeout(clear, 0); });
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-tool]'), setup); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
