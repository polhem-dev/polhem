// Moves the focus into the query pane: to what a condition row takes, or to "add condition" when there are no rows.
// Keeps an Escape meant for an open drop-down from closing the pane.
const ADD = '.polhem-criteria-panel__add button';
const TYPED = 'input:not([type=hidden]):not([type=checkbox]):not([readonly]):not([disabled])';
const FOCUSABLE = 'input:not([type=hidden]):not([disabled]), button:not([disabled]), [tabindex]:not([tabindex="-1"])';
// How long a closing menu may take to hand the focus back to its button, over a slow link included.
const RECLAIM_MS = 2000;

// NOTE: the pane calls only `focusRow`, which also installs the Escape guard. A browser can keep an older copy of this
// module after an update, and a call to a function that copy lacks would end the circuit.
export function focusRow(pane, index) {
    if (!pane) return;
    guardEscape(pane);
    const rows = pane.querySelectorAll('.polhem-criteria-row');
    const row = index >= 0 && index < rows.length ? rows[index] : null;
    // A row's caption line holds only its remove button; what the user enters is on the line under it.
    const line = row?.querySelector('.polhem-criteria-row__line');
    const add = pane.querySelector(ADD);
    const target = line ? line.querySelector(TYPED) ?? line.querySelector(FOCUSABLE) : add ?? pane.querySelector(FOCUSABLE);
    if (!target) return;
    target.focus();
    if (!line || !add) return;
    // WARNING: the "add condition" menu returns the focus to its button when it closes, which can happen after the row
    // it added took the focus; MudBlazor's menu does. The row takes it back once.
    const reclaim = () => {
        add.removeEventListener('focus', reclaim);
        if (target.isConnected) target.focus();
    };
    add.addEventListener('focus', reclaim);
    setTimeout(() => add.removeEventListener('focus', reclaim), RECLAIM_MS);
}

// An Escape pressed in an expanded control closes that control, not the pane: the pane's own handler runs in .NET and
// cannot tell. Stopping the key here keeps it from Blazor's handler, which listens above the pane.
function guardEscape(pane) {
    if (!pane || pane.polhemEscapeGuarded) return;
    pane.polhemEscapeGuarded = true;
    pane.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        if (e.composedPath().some(n => n.getAttribute?.('aria-expanded') === 'true')) e.stopPropagation();
    });
}
