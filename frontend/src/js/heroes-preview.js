/* Preview-only helpers for preview/heroes.html (theme chips, dark, language) +
   the background-video behaviour that the real heroVideoBlock needs (WCAG 2.2.2 pause control). */
(function () {
  var html = document.documentElement;
  var GREET = {
    'national-day': { en: 'Happy Saudi National Day', ar: 'يوم وطني سعيد' },
    'ramadan': { en: 'Ramadan Kareem. Clinic hours may change during Ramadan', ar: 'رمضان كريم. قد تتغير أوقات العيادات خلال الشهر الفضيل' },
    'eid-al-fitr': { en: 'Eid Mubarak. See our Eid emergency and clinic hours', ar: 'عيد مبارك. تعرّف على أوقات الطوارئ والعيادات خلال العيد' },
    'eid-al-adha': { en: 'Eid al-Adha Mubarak', ar: 'عيد أضحى مبارك' },
    'hajj': { en: 'Hajj season: stay safe from heat. Drink water and avoid direct sun', ar: 'موسم الحج: احمِ نفسك من الإجهاد الحراري. اشرب الماء وتجنّب الشمس المباشرة' },
    'pink-october': { en: 'Pink October: book your free screening', ar: 'أكتوبر الوردي: احجزي فحصك المجاني' }
  };
  var current = '';
  function isAr() { return html.lang === 'ar'; }
  function greet() {
    var g = document.getElementById('greet'); if (!g) return;
    var m = GREET[current]; g.classList.toggle('hidden', !m); if (m) g.textContent = isAr() ? m.ar : m.en;
  }
  document.querySelectorAll('[data-theme-preset]').forEach(function (b) {
    b.addEventListener('click', function () {
      current = b.getAttribute('data-theme-preset');
      if (current) html.setAttribute('data-theme', current); else html.removeAttribute('data-theme');
      document.querySelectorAll('[data-theme-preset]').forEach(function (x) { x.setAttribute('aria-pressed', x === b); });
      greet();
    });
  });
  var dark = document.getElementById('darkBtn');
  if (dark) dark.addEventListener('click', function () { html.classList.toggle('dark'); });
  // language (prototype swap; in Umbraco the language is the URL)
  var els = [].slice.call(document.querySelectorAll('[data-ar]')); els.forEach(function (e) { e._en = e.innerHTML; });
  var lang = document.getElementById('langBtn');
  if (lang) lang.addEventListener('click', function () {
    var ar = !isAr(); html.lang = ar ? 'ar' : 'en'; html.dir = ar ? 'rtl' : 'ltr';
    els.forEach(function (e) { e.innerHTML = ar ? e.getAttribute('data-ar') : e._en; });
    lang.textContent = ar ? 'English' : 'العربية'; greet();
  });
  // background video: respect reduced motion + visible pause/play button
  var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  document.querySelectorAll('[data-hero-video]').forEach(function (v) {
    var root = v.closest('section'); var btn = root && root.querySelector('[data-video-toggle]');
    function sync() {
      if (!btn) return; var playing = !v.paused;
      btn.querySelector('[data-icon-pause]').classList.toggle('hidden', !playing);
      btn.querySelector('[data-icon-play]').classList.toggle('hidden', playing);
      btn.setAttribute('aria-label', playing ? 'Pause background video' : 'Play background video');
    }
    if (reduce) { v.removeAttribute('autoplay'); v.pause(); }
    v.addEventListener('play', sync); v.addEventListener('pause', sync);
    if (btn) btn.addEventListener('click', function () { if (v.paused) v.play(); else v.pause(); });
    sync();
  });
})();
