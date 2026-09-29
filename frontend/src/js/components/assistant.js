/* "Ask EHC" assistant — demo (Views/Partials/site/_Assistant.cshtml).
   Pre-written replies chosen by keyword, entirely in the browser: nothing typed is sent anywhere. Emergency words always
   win and answer with the emergency number. "Talk to a person" switches to a simulated live-chat agent.
   Messages are built with DOM APIs (textContent), never innerHTML. */
(function () {
  'use strict';
  var box = document.querySelector('[data-assistant]');
  if (!box) return;
  var data;
  try { data = JSON.parse(box.getAttribute('data-assistant') || '{}'); } catch (e) { return; }
  var t = data.text || {}, intents = data.intents || [];
  var log = box.querySelector('[data-assistant-log]'), chips = box.querySelector('[data-assistant-chips]');
  var form = box.querySelector('[data-assistant-form]'), input = box.querySelector('#assistant-input');
  var nameEl = box.querySelector('[data-assistant-name]'), avatar = box.querySelector('[data-assistant-avatar] use');
  var openers = Array.prototype.slice.call(document.querySelectorAll('[data-action="assistant-open"]'));
  var reduce = document.documentElement.classList.contains('a11y-still') || (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  var mode = 'bot', started = false, returnTo = null;

  function norm(s) {
    // lower-case, drop Arabic diacritics and tatweel, unify alef / ya / ta marbuta so keywords match common spellings
    return (s || '').toLowerCase().replace(/[ً-ْـ]/g, '').replace(/[أإآ]/g, 'ا').replace(/ى/g, 'ي').replace(/ة/g, 'ه');
  }
  function has(text, words) { var n = norm(text); return (words || []).some(function (w) { return w && n.indexOf(norm(w)) !== -1; }); }

  function scroll() { log.scrollTop = log.scrollHeight; }
  function bubble(who, text, opts) {
    opts = opts || {};
    var row = document.createElement('div');
    row.className = 'assistant-msg ' + (who === 'me' ? 'is-me' : 'is-them') + (opts.urgent ? ' is-urgent' : '');
    var sr = document.createElement('span'); sr.className = 'sr-only';
    sr.textContent = (who === 'me' ? t.you : (mode === 'agent' ? t.agentName : t.botName)) + ': ';
    var p = document.createElement('p'); p.appendChild(sr); p.appendChild(document.createTextNode(text));
    row.appendChild(p);
    if (opts.url && opts.linkLabel) {
      var a = document.createElement('a');
      a.href = opts.url; a.className = 'assistant-link';
      a.textContent = opts.linkLabel;
      row.appendChild(a);
    }
    log.appendChild(row); scroll();
  }
  // a short "typing…" pause so replies read as a conversation (skipped when motion is reduced)
  function reply(fn, ms) {
    if (reduce) { fn(); return; }
    var dots = document.createElement('div');
    dots.className = 'assistant-msg is-them assistant-typing';
    dots.setAttribute('aria-hidden', 'true');
    dots.appendChild(document.createElement('i')); dots.appendChild(document.createElement('i')); dots.appendChild(document.createElement('i'));
    log.appendChild(dots); scroll();
    setTimeout(function () { dots.remove(); fn(); }, ms || 650);
  }

  function renderChips() {
    chips.textContent = '';
    var list = mode === 'bot'
      ? intents.filter(function (i) { return i.label; }).map(function (i) { return { label: i.label, run: function () { ask(i.label, i); } }; })
          .concat([{ label: t.agent, agent: true, run: function () { say(t.agent); toAgent(); } }])
      : [{ label: t.back, run: toBot }];
    list.forEach(function (c) {
      var b = document.createElement('button');
      b.type = 'button'; b.className = 'assistant-chip' + (c.agent ? ' is-agent' : '');
      b.textContent = c.label;
      b.addEventListener('click', c.run);
      chips.appendChild(b);
    });
  }
  function say(text) { bubble('me', text); }
  function answer(intent) {
    reply(function () { bubble('them', intent.answer, { url: intent.url, linkLabel: intent.url ? (intent.link || intent.label) : null, urgent: intent.urgent }); });
  }
  function ask(text, intent) { say(text); answer(intent); }

  function setMode(m) {
    mode = m;
    if (nameEl) nameEl.textContent = m === 'agent' ? t.agentName : t.botName;
    if (avatar) avatar.setAttribute('href', m === 'agent' ? '#i-agent' : '#i-bot');
    renderChips();
  }
  function toAgent() {
    setMode('agent');
    reply(function () { bubble('them', t.connecting); reply(function () { bubble('them', t.agentHello); }, 1400); }, 500);
  }
  function toBot() { setMode('bot'); reply(function () { bubble('them', t.welcome); }); }

  function handle(text) {
    say(text);
    if (mode === 'agent') { reply(function () { bubble('them', t.agentReply); }, 1200); return; }
    var urgent = intents.filter(function (i) { return i.urgent && has(text, i.keywords); })[0];
    if (urgent) { answer(urgent); return; }
    if (has(text, data.agentKeywords)) { toAgent(); return; }
    var hit = intents.filter(function (i) { return has(text, i.keywords); })[0];
    if (hit) { answer(hit); return; }
    if (has(text, data.greetKeywords)) { reply(function () { bubble('them', t.greeting); }); return; }
    reply(function () { bubble('them', t.fallback); });
  }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    var text = (input.value || '').trim();
    if (!text) return;
    input.value = '';
    handle(text.slice(0, 300));
  });

  function open() {
    returnTo = document.activeElement;
    box.hidden = false;
    openers.forEach(function (b) { b.setAttribute('aria-expanded', 'true'); b.hidden = true; });
    if (!started) { started = true; setMode('bot'); bubble('them', t.welcome); }
    setTimeout(function () { input.focus(); }, 30);
  }
  function close() {
    box.hidden = true;
    openers.forEach(function (b) { b.setAttribute('aria-expanded', 'false'); b.hidden = false; });
    var back = returnTo && document.contains(returnTo) && returnTo.offsetParent !== null ? returnTo : openers[0];
    if (back && back.focus) back.focus();
  }
  openers.forEach(function (b) { b.addEventListener('click', open); });
  Array.prototype.forEach.call(box.querySelectorAll('[data-action="assistant-close"]'), function (b) { b.addEventListener('click', close); });
  box.addEventListener('keydown', function (e) { if (e.key === 'Escape') { e.preventDefault(); close(); } });
})();
