/* Load synchronously in <head> (no defer) so dark mode, text size, contrast and the accessibility options apply before first paint.
   A choice made with the header/utility buttons is remembered (site.js); otherwise dark mode follows the OS.
   Samsung Internet starts dark: it always reports "light" to sites and, in its "Dark sites" mode, recolours light pages
   (no site opt-out); our dark design comes through almost unchanged. Keep in step with osDark() in site.js. */
(function () {
  var html = document.documentElement;
  var osDark = /SamsungBrowser/i.test(navigator.userAgent) || !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
  try {
    var dark = localStorage.getItem('ehc-dark');
    if (dark === '1' || (dark === null && osDark)) {
      html.classList.add('dark');
    }
    var fs = parseInt(localStorage.getItem('ehc-fs') || '', 10);
    if (fs >= 14 && fs <= 20 && fs !== 16) html.style.setProperty('--fs', fs + 'px');
    if (localStorage.getItem('ehc-contrast') === '1') html.classList.add('contrast');
    (localStorage.getItem('ehc-a11y') || '').split(',').forEach(function (k) {
      if (/^(links|spacing|still|cursor|guide)$/.test(k)) html.classList.add('a11y-' + k);
    });
    // cookie notice: shown from the first paint when no valid choice is stored. Keep in step with storedConsent() in site.js.
    var c = (localStorage.getItem('ehc-consent') || '').split('|'), at = Date.parse(c[2] || '');
    if (!(c[0] === '1' && /^(all|essential)$/.test(c[1]) && at > Date.now() - 365 * 864e5)) html.classList.add('consent-ask');
  } catch (e) {
    if (osDark) html.classList.add('dark');
    html.classList.add('consent-ask');
  }
})();
