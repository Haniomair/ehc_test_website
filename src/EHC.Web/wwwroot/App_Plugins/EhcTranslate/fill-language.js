/* Page menu action "Fill empty Arabic from English" (and the reverse): asks for confirmation, then calls
   POST /umbraco/management/api/v1/ehc/translate/copy-missing (Api/TranslateManagementController.cs), which copies every
   text that is filled in one language and empty in the other — also inside sections, slides and cards — and shows
   those blocks in the other language. Filled texts are never overwritten; a draft is saved, nothing is published.
   Plain module using the backoffice's own APIs. */
import { UmbEntityActionBase, UmbRequestReloadStructureForEntityEvent } from '@umbraco-cms/backoffice/entity-action';
import { UMB_ACTION_EVENT_CONTEXT } from '@umbraco-cms/backoffice/action';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { umbConfirmModal } from '@umbraco-cms/backoffice/modal';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';

const API = '/umbraco/management/api/v1/ehc/translate/copy-missing';

export default class EhcFillLanguageAction extends UmbEntityActionBase {
  #workspace;

  constructor(host, args) {
    super(host, args);
    // present when the action runs from the page's own ⋯ menu (the page is open in the editor)
    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (ctx) => { this.#workspace = ctx; });
  }

  /** The open editor for this page, if the action was started from it. */
  #editor() {
    const w = this.#workspace;
    return w && w.getUnique?.() === this.args.unique ? w : undefined;
  }

  async execute() {
    const meta = this.args.meta ?? {};
    const { from, to, fromName, toName } = meta;
    if (!from || !to || !this.args.unique) return;
    const notify = await this.getContext(UMB_NOTIFICATION_CONTEXT);

    // the editor would later save its own (older) copy of the page over the filled draft: never with unsaved changes
    const editor = this.#editor();
    if (editor?.getHasUnpersistedChanges?.()) {
      notify?.peek('warning', { data: { headline: 'Save the page first', message: `This page has unsaved changes. Save (or discard) them, then use "Fill empty ${toName} from ${fromName}" again.` } });
      return;
    }

    await umbConfirmModal(this, {
      headline: `Fill empty ${toName} from ${fromName}`,
      content: `Every text that is empty in ${toName} gets the ${fromName} text (page fields, sections, slides, cards…), and blocks that are only shown in ${fromName} are shown in ${toName} too. Texts already written in ${toName} are not changed. A draft is saved (nothing is published) and the page is reloaded so you see it. Then translate the copied texts.`,
      confirmLabel: 'Fill empty texts',
    }); // rejects when cancelled

    try {
      const auth = await this.getContext(UMB_AUTH_CONTEXT);
      const token = await auth.getLatestToken();
      const response = await fetch(API, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({ documentKey: this.args.unique, from, to }),
      });
      if (response.status === 403) throw new Error(`you are not allowed to edit this page in ${toName}`);
      if (!response.ok) throw new Error(`the server answered ${response.status}`);
      const r = await response.json();
      const nothing = !r.fields && !r.blocks && !r.nameCopied;
      const parts = [];
      if (r.fields) parts.push(`${r.fields} text${r.fields === 1 ? '' : 's'} copied`);
      if (r.blocks) parts.push(`${r.blocks} block${r.blocks === 1 ? '' : 's'} now shown in ${toName}`);
      if (r.nameCopied) parts.push('page name copied');
      if (r.skipped) parts.push(`${r.skipped} field${r.skipped === 1 ? '' : 's'} with their own blocks per language left as they are`);
      notify?.peek(nothing ? 'default' : 'positive', {
        data: {
          headline: nothing ? `Nothing to fill: ${toName} has all the texts` : `${toName} filled from ${fromName}`,
          message: nothing ? '' : `${parts.join(', ')}. Saved as a draft: translate the copied texts, then publish.`,
        },
      });
      if (nothing) return;
      const events = await this.getContext(UMB_ACTION_EVENT_CONTEXT);
      events?.dispatchEvent(new UmbRequestReloadStructureForEntityEvent({ unique: this.args.unique, entityType: this.args.entityType }));
      // show the filled draft: reload the open editor, so its older copy can never be saved over it
      if (editor?.reload) await editor.reload();
      else if (location.pathname.includes(this.args.unique)) location.reload();
    } catch (e) {
      notify?.peek('danger', { data: { headline: 'Could not fill the texts', message: `Nothing was changed: ${e.message}.` } });
    }
  }
}

export { EhcFillLanguageAction as api };
