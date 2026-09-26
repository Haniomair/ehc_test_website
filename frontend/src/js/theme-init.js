/* Load synchronously in <head> (no defer) so dark mode is applied before first paint.
   Follows the OS setting; the header toggle overrides it for the current visit. */
(function () {
  try {
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
      document.documentElement.classList.add('dark');
    }
  } catch (e) { /* ignore */ }
})();
