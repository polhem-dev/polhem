// Keys typed on a resting cell before its editor arrives are held and replayed, because opening an editor takes a round
// trip on Blazor Server and those keys would otherwise be lost.
const TEXT_CELL = 'polhem-grid-cell__text';
let held = [];
let heldTab = null;

document.addEventListener('keydown', event => {
    const active = document.activeElement;
    if (!active || !active.classList || !active.classList.contains(TEXT_CELL)) return;
    if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
        held.push(event.key);
        event.preventDefault();
    } else if (event.key === 'Tab' && held.length > 0) {
        // Leaving now would strand the held text, so the move waits for the editor and happens after the replay.
        heldTab = { backwards: event.shiftKey };
        event.preventDefault();
    } else if (event.key === 'Escape') {
        held = [];
        heldTab = null;
    }
}, true);

// A click elsewhere while keys are held would give the next editor to another cell, and the keys would land there. The
// click waits until the keys reached the cell they were typed on.
document.addEventListener('mousedown', event => {
    if (held.length === 0) return;
    event.preventDefault();
    event.stopPropagation();
}, true);

function nextStop(from, backwards) {
    const table = from.closest('table');
    if (!table) return null;
    const stops = [...table.querySelectorAll(`.${TEXT_CELL}, .polhem-grid-cell__editor`)];
    return stops[stops.indexOf(from) + (backwards ? -1 : 1)] ?? null;
}

export function focusEditor(editor, cell) {
    const input = editor && editor.querySelector('input:not([type=checkbox]), textarea, select, input');
    if (!input) return;
    const original = input.value;
    input.focus();
    input.select && input.select();
    if (held.length > 0 && !input.readOnly) {
        // WARNING: never set `value` here. A value set from script does not fire `change` when the input is left, so the
        // text would show and then be lost. Inserted text counts as typing.
        document.execCommand('insertText', false, held.join(''));
    }
    held = [];
    const pendingTab = heldTab;
    heldTab = null;

    editor.addEventListener('keydown', event => {
        if (event.key !== 'Escape' || document.querySelector('.mud-popover-open')) return;
        // Back to the value on focus, so leaving fires no `change`, and whatever was typed is dropped.
        input.value = original;
        event.stopPropagation();
        cell.invokeMethodAsync('CloseEditor');
    }, true);

    if (pendingTab) {
        input.blur();
        const next = nextStop(editor, pendingTab.backwards);
        next && next.focus();
    }
}
