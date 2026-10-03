/* Shared by status-row.js and toggle-pictures.js: whether block rows show pictures (per browser). */
export const PICTURES_KEY = 'ehc-block-pictures';
export const PICTURES_EVENT = 'ehc-block-pictures-changed';
export function picturesOn() {
  try { return localStorage.getItem(PICTURES_KEY) !== 'off'; } catch { return true; }
}
