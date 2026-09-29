/* Visual acuity screening ([data-vt]): tumbling-E test, right eye then left eye, Snellen lines 6/60 → 6/6.
   Letter height = distance × tan(5 arc-minutes × line/6); the screen scale comes from matching a bank card
   (85.6 mm wide) on screen. A line is passed with 3 of 4 correct answers; the test stops at the first failed line.
   Runs entirely in the browser; only the card calibration is remembered on this device (localStorage). */
(function () {
  'use strict';
  var CARD_MM = 85.6, ARC = Math.tan(5 / 60 * Math.PI / 180), LINES = [60, 36, 24, 18, 12, 9, 6], TRIES = 4, PASS = 3;
  var DIRS = { right: 0, down: 90, left: 180, up: 270 };
  var KEYS = { ArrowRight: 'right', ArrowLeft: 'left', ArrowUp: 'up', ArrowDown: 'down' };
  var STORE = 'ehc-vt-card';

  function setup(root) {
    if (root.hasAttribute('data-vt-ready')) return;
    root.setAttribute('data-vt-ready', '');
    var q = function (s) { return root.querySelector(s); };
    var qa = function (s) { return [].slice.call(root.querySelectorAll(s)); };
    var t = function (k) { return root.getAttribute('data-' + k) || ''; };
    var card = q('[data-card]'), range = q('[data-card-range]'), letter = q('[data-e]');
    var state = null;

    try { var saved = parseInt(localStorage.getItem(STORE), 10); if (saved && range) range.value = String(saved); } catch (e) { /* storage off */ }

    function sizeCard() { if (card && range) card.style.width = range.value + 'px'; }
    if (range) range.addEventListener('input', function () {
      sizeCard();
      try { localStorage.setItem(STORE, range.value); } catch (e) { /* storage off */ }
    });
    sizeCard();

    function step(name) {
      qa('[data-step]').forEach(function (s) { s.hidden = s.getAttribute('data-step') !== name; });
      var cur = q('[data-step="' + name + '"]'), h = cur && cur.querySelector('h3');
      if (h) h.focus({ preventScroll: false });
    }

    function pxPerMm() { return (range ? +range.value : 324) / CARD_MM; }
    function distanceMm() { var r = q('input[name="distance"]:checked'); return (r ? +r.value : 2) * 1000; }
    // smallest line this screen can draw honestly: the E needs at least ~10 device pixels
    function heightPx(line) { return distanceMm() * ARC * (line / 6) * pxPerMm(); }
    function drawable(line) { return heightPx(line) * (window.devicePixelRatio || 1) >= 10; }

    function startEye(eye) {
      state = { eye: eye, line: 0, tries: 0, right: 0, last: null, results: state ? state.results : {} };
      var title = q('[data-eye-title]'), cover = q('[data-cover]');
      if (title) title.textContent = t(eye === 'right' ? 'eye-right' : 'eye-left');
      if (cover) cover.textContent = t(eye === 'right' ? 'cover-left' : 'cover-right');
      step('test');
      next();
    }

    function next() {
      var line = LINES[state.line];
      var dirs = Object.keys(DIRS).filter(function (d) { return d !== state.last; });
      state.dir = dirs[Math.floor(Math.random() * dirs.length)];
      state.last = state.dir;
      var px = Math.max(4, heightPx(line));
      letter.style.width = letter.style.height = px + 'px';
      letter.style.transform = 'rotate(' + DIRS[state.dir] + 'deg)';
      var info = q('[data-line]');
      if (info) info.textContent = t('line-format').replace('{0}', '6/' + line).replace('{1}', String(state.tries + 1)).replace('{2}', String(TRIES));
    }

    function answer(dir) {
      if (!state || q('[data-step="test"]').hidden) return;
      if (dir === state.dir) state.right++;
      state.tries++;
      if (state.tries < TRIES) { next(); return; }
      var passed = state.right >= PASS;
      if (passed) state.best = LINES[state.line];
      var more = passed && state.line + 1 < LINES.length && drawable(LINES[state.line + 1]);
      if (passed && state.line + 1 < LINES.length && !more) state.limited = true;
      if (more) { state.line++; state.tries = 0; state.right = 0; next(); return; }
      state.results[state.eye] = { best: state.best || null, limited: !!state.limited };
      if (state.eye === 'right') step('switch'); else finish();
    }

    function label(r) { return r && r.best ? '6/' + r.best : t('below'); }

    function finish() {
      var r = state.results.right, l = state.results.left;
      var set = function (k, v) { var el = q('[data-v="' + k + '"]'); if (el) el.textContent = v; };
      set('right', label(r));
      set('left', label(l));
      // normal screening result: 6/9 or better in each eye
      var ok = function (x) { return x && x.best && x.best <= 9; };
      var li = function (x) { return x && x.best ? LINES.indexOf(x.best) : -1; };
      var normal = ok(r) && ok(l);
      var verdict = q('[data-v="verdict"]');
      if (verdict) { verdict.textContent = normal ? t('normal') : t('check'); verdict.setAttribute('data-tone', normal ? 'good' : 'warn'); }
      var notes = [];
      if (Math.abs(li(r) - li(l)) >= 2) notes.push(t('diff'));
      if ((r && r.limited) || (l && l.limited)) notes.push(t('too-small'));
      set('advice', notes.join(' '));
      step('result');
    }

    root.addEventListener('click', function (e) {
      var go = e.target.closest('[data-go]');
      if (go) {
        var to = go.getAttribute('data-go');
        if (to === 'test') { state = null; startEye('right'); }
        else if (to === 'test2') startEye('left');
        else step(to);
        return;
      }
      var a = e.target.closest('[data-answer]');
      if (a) answer(a.getAttribute('data-answer'));
    });
    root.addEventListener('keydown', function (e) {
      var dir = KEYS[e.key];
      if (dir && !q('[data-step="test"]').hidden) { e.preventDefault(); answer(dir); }
    });
    // arrow keys work while the test is on screen, even if focus is on the page
    document.addEventListener('keydown', function (e) {
      if (root.contains(document.activeElement)) return;
      var dir = KEYS[e.key], test = q('[data-step="test"]');
      if (dir && test && !test.hidden) { e.preventDefault(); answer(dir); }
    });
  }

  function all() { [].forEach.call(document.querySelectorAll('[data-vt]'), setup); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
