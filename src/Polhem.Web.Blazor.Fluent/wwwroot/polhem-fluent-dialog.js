// Keeps Escape from closing a Fluent UI dialog that may not be dismissed, and lets it through to a dialog nested in it.
//
// WARNING: this replaces the `PreventDismissOnEscape` of `FluentDialog`. Fluent UI 5.1.0 implements that with a
// capture-phase `keydown` listener on the dialog element that swallows every Escape inside it, so a dismissable dialog
// placed in the content (a lookup opened from the row edit dialog) could not be closed with Escape either. The guard
// below swallows Escape only when the dialog nearest to where the key was pressed is its own.

const guardKey = '_polhemEscapeGuard';

export function guardEscape(id) {
    const dialog = document.getElementById(id);
    if (!dialog || dialog[guardKey]) return;
    const guard = e => {
        if (e.key !== 'Escape') return;
        const nearest = e.composedPath().find(n => n instanceof Element && n.localName === 'fluent-dialog');
        if (nearest !== dialog) return;
        e.stopImmediatePropagation();
        e.preventDefault();
    };
    dialog[guardKey] = guard;
    dialog.addEventListener('keydown', guard, true);
}

export function releaseEscape(id) {
    const dialog = document.getElementById(id);
    if (!dialog?.[guardKey]) return;
    dialog.removeEventListener('keydown', dialog[guardKey], true);
    dialog[guardKey] = null;
}
