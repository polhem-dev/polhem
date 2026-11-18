// Keeps Escape from closing a Fluent UI dialog that may not be dismissed, and lets it through to a dialog nested in it.
//
// WARNING: this replaces the `PreventDismissOnEscape` of `FluentDialog`. Fluent UI 5.1.0 implements that with a
// capture-phase `keydown` listener on the dialog element that swallows every Escape inside it, so a dismissable dialog
// placed in the content (a lookup opened from the row edit dialog) could not be closed with Escape either, and an Escape
// pressed while the focus was on the body never passed the listener and closed the native dialog. The guard listens on
// the document instead. It swallows Escape when the dialog nearest to where the key was pressed is its own, or, when the
// key was pressed outside every dialog, when its own dialog is open with no open dialog nested in it.
//
// NOTE: the guard stops the key on `keydown`, not on the native dialog's `cancel`. A browser lets a page cancel that
// event only after a user activation, which Escape is not, so a second Escape in a row would close the dialog.

const guardKey = '_polhemEscapeGuard';

const isDialog = node => node instanceof Element && node.localName === 'fluent-dialog';

const isOpen = dialog => dialog.shadowRoot?.querySelector('dialog')?.open === true;

const isTopmost = dialog => isOpen(dialog) && ![...dialog.querySelectorAll('fluent-dialog')].some(isOpen);

export function guardEscape(id) {
    const dialog = document.getElementById(id);
    if (!dialog || dialog[guardKey]) return;
    const guard = e => {
        // A dialog removed without a release would otherwise keep its guard on the document.
        if (!dialog.isConnected) {
            document.removeEventListener('keydown', guard, true);
            return;
        }
        if (e.key !== 'Escape') return;
        const nearest = e.composedPath().find(isDialog);
        if (nearest ? nearest !== dialog : !isTopmost(dialog)) return;
        e.stopImmediatePropagation();
        e.preventDefault();
    };
    dialog[guardKey] = guard;
    document.addEventListener('keydown', guard, true);
}

export function releaseEscape(id) {
    const dialog = document.getElementById(id);
    if (!dialog?.[guardKey]) return;
    document.removeEventListener('keydown', dialog[guardKey], true);
    dialog[guardKey] = null;
}
