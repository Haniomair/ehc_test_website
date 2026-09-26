/* Load synchronously in <head> (no defer) so dark mode, text size and contrast apply before first paint.
   A choice made with the header/utility buttons is remembered (site.js); otherwise dark mode follows the OS. */
(function () {
  var html = document.documentElement;
  try {
    var dark = localStorage.getItem('ehc-dark');
    if (dark === '1' || (dark === null && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
      html.classList.add('dark');
    }
    var fs = parseInt(localStorage.getItem('ehc-fs') || '', 10);
    if (fs >= 14 && fs <= 20 && fs !== 16) html.style.setProperty('--fs', fs + 'px');
    if (localStorage.getItem('ehc-contrast') === '1') html.classList.add('contrast');
  } catch (e) {
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) html.classList.add('dark');
  }
})();
