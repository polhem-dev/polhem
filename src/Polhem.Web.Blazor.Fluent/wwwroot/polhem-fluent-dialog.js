// Keeps Escape from closing a Fluent UI dialog that may not be dismissed, and lets it through to a dialog above it.
//
// WARNING: this replaces the `PreventDismissOnEscape` of `FluentDialog`. Fluent UI 5.1.0 implements that with a
// capture-phase `keydown` listener on the dialog element that swallows every Escape inside it, so a dismissable dialog
// placed in the content (a lookup opened from the row edit dialog) could not be closed with Escape either, and an Escape
// pressed while the focus was on the body never passed the listener and closed the native dialog. The guard listens on
// the document instead. It swallows Escape when the dialog nearest to where the key was pressed is its own, or, when the
// key was pressed outside every dialog, when its own dialog is the open dialog that opened last, which is the one the
// browser would close.
//
// NOTE: the guard stops the key on `keydown`, not on the native dialog's `cancel`. A browser lets a page cancel that
// event only after a user activation, which Escape is not, so a second Escape in a row would close the dialog.

const guards = new Map();
const openedAt = new WeakMap();
let opened = 0;

const isDialog = node => node instanceof Element && node.localName === 'fluent-dialog';

const isOpen = dialog => dialog.shadowRoot?.querySelector('dialog')?.open === true;

// A `fluent-dialog` announces that it opened with a `toggle` event that leaves its shadow root. `PolhemDialog` loads this
// module before its first `ShowAsync`, so a dialog opened before the module loaded is older than that dialog. A dialog
// that becomes guarded while already open has no recorded order; against another dialog without one, the only order
// left is nesting, where the inner dialog opened later.
document.addEventListener('toggle', e => {
    if (isDialog(e.target) && e.detail?.newState === 'open') openedAt.set(e.target, ++opened);
}, true);

const isTopmost = dialog => {
    if (!isOpen(dialog)) return false;
    const mine = openedAt.get(dialog) ?? 0;
    return ![...document.querySelectorAll('fluent-dialog')]
        .some(other => {
            if (other === dialog || !isOpen(other)) return false;
            const theirs = openedAt.get(other) ?? 0;
            return theirs > mine || (theirs === mine && dialog.contains(other));
        });
};

export function guardEscape(id) {
    const dialog = document.getElementById(id);
    if (!dialog || guards.has(id)) return;
    const guard = e => {
        // A dialog removed without a release would otherwise keep its guard on the document.
        if (!dialog.isConnected) {
            releaseEscape(id);
            return;
        }
        if (e.key !== 'Escape') return;
        const nearest = e.composedPath().find(isDialog);
        if (nearest ? nearest !== dialog : !isTopmost(dialog)) return;
        e.stopImmediatePropagation();
        e.preventDefault();
    };
    guards.set(id, guard);
    document.addEventListener('keydown', guard, true);
}

export function releaseEscape(id) {
    const guard = guards.get(id);
    if (!guard) return;
    document.removeEventListener('keydown', guard, true);
    guards.delete(id);
}
