/* Flip cards: [data-flip-cards] lists of .flipcard items. The front button turns the card (aria-expanded), the hidden side
   is inert so keyboard focus never lands on it, and focus moves to the side that is now showing. Esc turns it back. */
(function () {
  'use strict';
  function init(list) {
    if (list.hasAttribute('data-flip-ready')) return;
    list.setAttribute('data-flip-ready', '');
    list.classList.add('flip-js');
    [].forEach.call(list.querySelectorAll('.flipcard'), function (card) {
      var front = card.querySelector('.flip-front'), backFace = card.querySelector('.flip-back');
      var open = card.querySelector('[data-flip-open]'), close = card.querySelector('[data-flip-close]');
      if (!front || !backFace || !open || !close) return;
      open.hidden = false; close.hidden = false;
      function set(flipped, focus) {
        card.classList.toggle('is-flipped', flipped);
        open.setAttribute('aria-expanded', flipped ? 'true' : 'false');
        front.inert = flipped;
        backFace.inert = !flipped;
        if (focus) (flipped ? close : open).focus();
      }
      open.addEventListener('click', function () { set(true, true); });
      close.addEventListener('click', function () { set(false, true); });
      card.addEventListener('keydown', function (e) { if (e.key === 'Escape' && card.classList.contains('is-flipped')) { e.preventDefault(); e.stopPropagation(); set(false, true); } });
      set(false, false);
    });
  }
  function all() { [].forEach.call(document.querySelectorAll('[data-flip-cards]'), init); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', all); else all();
})();
