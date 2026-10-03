/* Block List ⋯ menu: "Show / hide section pictures". Remembered in this browser (localStorage); every block row
   (status-row.js) updates at once. */
import { UmbPropertyActionBase } from '@umbraco-cms/backoffice/property-action';
import { PICTURES_KEY, PICTURES_EVENT, picturesOn } from './pictures.js';

export default class EhcTogglePicturesAction extends UmbPropertyActionBase {
  async execute() {
    try { localStorage.setItem(PICTURES_KEY, picturesOn() ? 'off' : 'on'); } catch { /* private mode: nothing to remember */ }
    window.dispatchEvent(new Event(PICTURES_EVENT));
  }
}

export { EhcTogglePicturesAction as api };
