/* "Listen" ([data-listen="<css selector of the text>"]): reads the page title and the text aloud with the device's
   own speech synthesis, paragraph by paragraph (long single utterances get cut off in some browsers), marking the
   paragraph being read. The button stays hidden unless the device has a voice for the page language — an Arabic page
   is never read with an English voice. Play / pause / resume on the main button, a separate stop button. */
(function () {
  'use strict';
  var synth = window.speechSynthesis;
  var roots = [].slice.call(document.querySelectorAll('[data-listen]'));
  if (!roots.length || !synth || !window.SpeechSynthesisUtterance) return;
  var lang = (document.documentElement.lang || 'ar').toLowerCase().slice(0, 2);

  function voiceFor() {
    var vs = synth.getVoices();
    return vs.filter(function (v) { return (v.lang || '').toLowerCase().indexOf(lang) === 0; })
      .sort(function (a, b) { return (b.localService ? 1 : 0) - (a.localService ? 1 : 0); })[0] || null;
  }

  function init(root) {
    var play = root.querySelector('[data-listen-play]'), stop = root.querySelector('[data-listen-stop]');
    var label = play && play.querySelector('[data-listen-label]');
    var source = document.querySelector(root.getAttribute('data-listen'));
    if (!play || !source) return;
    var L = { listen: root.getAttribute('data-label-listen'), pause: root.getAttribute('data-label-pause'), resume: root.getAttribute('data-label-resume') };
    var state = 'idle', queue = [], current = null;

    function blocks() {
      var list = [];
      var h1 = document.querySelector('main h1');
      if (h1 && h1.textContent.trim()) list.push(h1);
      [].forEach.call(source.querySelectorAll('h2, h3, h4, p, li, blockquote'), function (el) {
        if (el.closest('li') && el.tagName !== 'LI') return;            // a <p> inside a list item is read with it
        if (el.tagName === 'P' && el.closest('blockquote')) return;     // read as part of the quote
        if (el.textContent.trim()) list.push(el);
      });
      return list;
    }
    function mark(el) {
      if (current) current.classList.remove('is-reading');
      current = el;
      if (el && el !== document.querySelector('main h1')) el.classList.add('is-reading');
    }
    function set(s) {
      state = s;
      play.setAttribute('aria-pressed', s === 'playing' ? 'true' : 'false');
      if (label) label.textContent = s === 'playing' ? L.pause : s === 'paused' ? L.resume : L.listen;
      play.querySelector('[data-icon="play"]').classList.toggle('hidden', s === 'playing');
      play.querySelector('[data-icon="pause"]').classList.toggle('hidden', s !== 'playing');
      if (stop) stop.classList.toggle('hidden', s === 'idle');
    }
    function next() {
      var el = queue.shift();
      if (!el) { mark(null); set('idle'); return; }
      var u = new SpeechSynthesisUtterance(el.textContent.replace(/\s+/g, ' ').trim());
      var v = voiceFor();
      if (v) u.voice = v;
      u.lang = v ? v.lang : lang;
      u.onstart = function () { mark(el); };
      u.onend = function () { if (state === 'playing') next(); };
      u.onerror = function () { if (state === 'playing') next(); };
      synth.speak(u);
    }
    function reset() { synth.cancel(); queue = []; mark(null); set('idle'); }

    play.addEventListener('click', function () {
      if (state === 'playing') { synth.pause(); set('paused'); return; }
      if (state === 'paused') { synth.resume(); set('playing'); return; }
      synth.cancel();
      queue = blocks();
      set('playing');
      next();
    });
    if (stop) stop.addEventListener('click', reset);
    window.addEventListener('pagehide', function () { synth.cancel(); });
    root.hidden = false;
    set('idle');
  }

  // voices load asynchronously in some browsers
  var started = false;
  function start() {
    if (started || !voiceFor()) return;
    started = true;
    roots.forEach(init);
  }
  start();
  if (!started && 'onvoiceschanged' in synth) synth.addEventListener('voiceschanged', start);
})();
